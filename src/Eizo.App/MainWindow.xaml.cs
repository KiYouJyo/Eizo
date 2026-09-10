using Eizo.Localization;
using Eizo.Models;
using Eizo.Playback;
using Eizo.Views;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Windows.System;

namespace Eizo;

public sealed partial class MainWindow : Window
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly Dictionary<string, ShellTabState> _tabs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closingTabKeys = new(StringComparer.Ordinal);
    private readonly WindowPlacementService _windowPlacement = new();
    private SizeInt32 _lastNormalWindowSize;
    private bool _wasWindowMaximized;
    private PlayerView? _fullscreenOwner;
    private bool _playerFullscreen;
    private bool _restoreMaximizedAfterPlayerFullscreen;
    private string? _selectedTabKey;
    private bool _navigationChromeHiddenForImmersive;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Eizo 映藏";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        WindowRoot.Loaded += WindowRoot_Loaded;
        WindowRoot.RequestedTheme = ThemePreferenceStore.Load();
        UpdateTitleBarColors();

        RestoreWindowPlacement();
        AppWindow.Changed += MainWindow_AppWindowChanged;
        Closed += MainWindow_WindowPlacementClosed;

        ApplyLocalizedShellText();
        _localization.LanguageChanged += ShellLocalization_LanguageChanged;
        Closed += MainWindow_Closed;

        WindowRoot.ActualThemeChanged += (_, _) =>
        {
            UpdateTitleBarColors();
            RefreshTabVisuals();
            QueueNavigationPaneBackgroundUpdate();
        };

        CreateWorkspaceTab(select: true);
    }

    private void RestoreWindowPlacement()
    {
        var workArea = DisplayArea
            .GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)
            .WorkArea;

        var placement = _windowPlacement.Load(
            new SizeInt32(workArea.Width, workArea.Height));

        _lastNormalWindowSize = new SizeInt32(placement.Width, placement.Height);
        _wasWindowMaximized = placement.WasMaximized;

        AppWindow.Resize(_lastNormalWindowSize);

        if (_wasWindowMaximized &&
            AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void MainWindow_AppWindowChanged(
        AppWindow sender,
        AppWindowChangedEventArgs args)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter) return;

        switch (presenter.State)
        {
            case OverlappedPresenterState.Maximized:
                _wasWindowMaximized = true;
                break;

            case OverlappedPresenterState.Restored:
                _wasWindowMaximized = false;
                if (args.DidSizeChange)
                    _lastNormalWindowSize = AppWindow.Size;
                break;

            case OverlappedPresenterState.Minimized:
                // Never persist a minimized state; retain the last usable size.
                break;
        }
    }

    private void MainWindow_WindowPlacementClosed(object sender, WindowEventArgs e)
    {
        AppWindow.Changed -= MainWindow_AppWindowChanged;
        Closed -= MainWindow_WindowPlacementClosed;

        try
        {
            _windowPlacement.Save(_lastNormalWindowSize, _wasWindowMaximized);
        }
        catch
        {
            // Window placement persistence must never turn a normal close into a crash.
        }
    }

    public void SetPlayerVideoFullscreen(bool enabled, PlayerView? owner = null)
    {
        if (!enabled && owner is not null && !ReferenceEquals(_fullscreenOwner, owner)) return;
        _fullscreenOwner = enabled ? owner : null;
        if (_playerFullscreen == enabled)
            return;

        if (enabled)
        {
            _restoreMaximizedAfterPlayerFullscreen =
                AppWindow.Presenter is OverlappedPresenter
                {
                    State: OverlappedPresenterState.Maximized
                };

            _playerFullscreen = true;

            AppTitleBar.Visibility = Visibility.Collapsed;
            RootGrid.RowDefinitions[0].Height = new GridLength(0);
            Grid.SetRow(ShellNavigation, 0);
            Grid.SetRowSpan(ShellNavigation, 2);

            ShellNavigation.IsPaneOpen = false;
            ShellNavigation.IsPaneToggleButtonVisible = false;
            ShellNavigation.CompactPaneLength = 0;
            ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

            // SetPresenter is re-entrant; avoid asking for the presenter we already have.
            if (AppWindow.Presenter is not { Kind: AppWindowPresenterKind.FullScreen })
            {
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }

            return;
        }

        _playerFullscreen = false;

        Grid.SetRow(ShellNavigation, 1);
        Grid.SetRowSpan(ShellNavigation, 1);
        RootGrid.RowDefinitions[0].Height = new GridLength(48);
        AppTitleBar.Visibility = Visibility.Visible;

        if (AppWindow.Presenter is not { Kind: AppWindowPresenterKind.Overlapped })
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        if (_restoreMaximizedAfterPlayerFullscreen)
        {
            // Maximize on the next dispatcher pass; calling it from inside the
            // presenter-transition callback can re-enter window layout and deadlock.
            _restoreMaximizedAfterPlayerFullscreen = false;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (AppWindow.Presenter is OverlappedPresenter restoredPresenter)
                {
                    restoredPresenter.Maximize();
                }
            });
        }
    }

    public void RestoreAndActivate()
    {
        AppWindow.Show();

        if (AppWindow.Presenter is OverlappedPresenter presenter &&
            presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    private void WindowRoot_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_selectedTabKey is null ||
            !_tabs.TryGetValue(_selectedTabKey, out var state) ||
            state.View is not PlayerView player)
        {
            return;
        }

        // ESC on a fullscreen player means "exit fullscreen" only, and it must win
        // over every other control.
        if (e.Key == VirtualKey.Escape)
        {
            if (player.IsVideoFullscreen)
            {
                player.ExitFullscreenFromKeyboard();
                e.Handled = true;
            }
            return;
        }

        if (e.Key != VirtualKey.Space) return;

        // PreviewKeyDown tunnels from the window root to the focused element, so we
        // run BEFORE any button/navigation item. Space on a PlayerView tab means
        // pause/play only; mark it handled so it can never activate tabs or buttons.
        if (Content.XamlRoot is { } xamlRoot)
        {
            var focused = FocusManager.GetFocusedElement(xamlRoot);
            if (focused is TextBox or PasswordBox or AutoSuggestBox or RichEditBox or ComboBox or Slider)
                return;
        }

        _ = player.TogglePlayPauseAsync();
        e.Handled = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    private string T(string key) => _localization.GetString(key);

    private void ApplyLocalizedShellText()
    {
        HomeNav.Content = T("Nav_Home");
        BangumiNav.Content = T("Nav_Bangumi");
        CalendarNav.Content = T("Nav_BroadcastCalendar");
        SeasonalNav.Content = T("Nav_SeasonalAnime");
        DiscoverNav.Content = T("Nav_RankDiscover");
        CategoryNav.Content = T("Nav_Library");
        AnimeNav.Content = T("Nav_Anime");
        MoviesNav.Content = T("Nav_Movies");
        SeriesNav.Content = T("Nav_Series");
        SourcesNav.Content = T("Nav_Sources");
        CacheNav.Content = T("Nav_Cache");
        AboutNav.Content = T("Nav_About");
        SettingsNav.Content = T("Nav_Settings");
    }

    private void ShellNavigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item) return;
        var pageKey = item.Tag?.ToString();

        if (pageKey is null) return;
        NavigateSelectedWorkspace(pageKey, item);
    }

    private void ShellNewTabButton_Click(object sender, RoutedEventArgs e) =>
        CreateWorkspaceTab(select: true);

    private void CreateWorkspaceTab(bool select)
    {
        var key = "workspace:" + Guid.NewGuid().ToString("N");
        var view = CreateWorkspaceView("home");

        var state = new ShellTabState(
            key,
            ShellTabKind.Workspace,
            "home",
            T("Nav_Home"),
            "\uE80F",
            view,
            HomeNav,
            PreferredTabWidth);

        AddTab(state, select);
    }

    private void NavigateSelectedWorkspace(string pageKey, NavigationViewItem navItem)
    {
        if (_selectedTabKey is null ||
            !_tabs.TryGetValue(_selectedTabKey, out var state) ||
            state.Kind != ShellTabKind.Workspace)
        {
            CreateWorkspaceTab(select: true);
            state = _tabs[_selectedTabKey!];
        }

        if (string.Equals(state.PageKey, pageKey, StringComparison.Ordinal))
        {
            SelectShellItem(navItem);
            return;
        }

        var descriptor = DescribeWorkspacePage(pageKey);
        state.PageKey = pageKey;
        state.Title = descriptor.Title;
        state.Glyph = descriptor.Glyph;
        state.NavItem = navItem;
        ReplaceTabView(state, CreateWorkspaceView(pageKey));

        UpdateTabIdentity(state);

        if (string.Equals(_selectedTabKey, state.Key, StringComparison.Ordinal))
        {
            ShowNavigationChrome();
            ShowTabView(state);
            ApplyResponsiveLayout(force: true);
            SelectShellItem(navItem);
        }
    }

    private FrameworkElement CreateWorkspaceView(string pageKey)
    {
        switch (pageKey)
        {
            case "home":
            {
                var view = new HomeView();
                WireWorkspaceMediaView(view);
                return view;
            }
            case "bangumi-calendar":
                return new BangumiPlaceholderView(BangumiPlaceholderKind.Calendar);
            case "bangumi-seasonal":
                return new BangumiPlaceholderView(BangumiPlaceholderKind.Seasonal);
            case "bangumi-discover":
                return new BangumiPlaceholderView(BangumiPlaceholderKind.Discover);
            case "bangumi-following":
                return new BangumiPlaceholderView(BangumiPlaceholderKind.Following);
            case "categories":
            {
                var view = new CatalogView();
                WireWorkspaceMediaView(view);
                return view;
            }
            case "anime":
            {
                var view = new CategoryView(MediaCategoryKind.Anime);
                WireWorkspaceMediaView(view);
                return view;
            }
            case "movies":
            {
                var view = new CategoryView(MediaCategoryKind.Movies);
                WireWorkspaceMediaView(view);
                return view;
            }
            case "series":
            {
                var view = new CategoryView(MediaCategoryKind.Series);
                WireWorkspaceMediaView(view);
                return view;
            }
            case "sources":
                return new SourcesView();
            case "cache":
                return new CacheView();
            case "about":
                return new AboutView();
            case "settings":
                return new SettingsView();
            default:
                return new HomeView();
        }
    }

    private void WireWorkspaceMediaView(HomeView view)
    {
        view.DetailRequested += (_, title) => OpenDetail(title, startPlaying: false);
        view.PlayRequested += (_, title) => OpenDetail(title, startPlaying: true);
    }

    private void WireWorkspaceMediaView(CategoryView view)
    {
        view.DetailRequested += (_, title) => OpenDetail(title, startPlaying: false);
        view.PlayRequested += (_, title) => OpenDetail(title, startPlaying: true);
    }

    private void WireWorkspaceMediaView(CatalogView view) =>
        view.MediaRequested += async (_, item) =>
            await OpenCatalogMediaAsync(item);

    private (string Title, string Glyph) DescribeWorkspacePage(string pageKey) => pageKey switch
    {
        "home" => (T("Nav_Home"), "\uE80F"),
        "bangumi-calendar" => (T("Nav_BroadcastCalendar"), "\uE787"),
        "bangumi-seasonal" => (T("Nav_SeasonalAnime"), "\uE8B2"),
        "bangumi-discover" => (T("Nav_RankDiscover"), "\uE721"),
        "bangumi-following" => (T("Nav_MyFollowing"), "\uE77B"),
        "categories" => (
            T("Nav_Library"),
            char.ConvertFromUtf32((int)Symbol.Library)),
        "anime" => (T("Nav_Anime"), "\uE8B2"),
        "movies" => (T("Nav_Movies"), "\uE714"),
        "series" => (T("Nav_Series"), "\uE8FD"),
        "sources" => (T("Nav_Sources"), "\uE753"),
        "cache" => (T("Nav_Cache"), "\uE7C5"),
        "about" => (T("Nav_About"), "\uE897"),
        "settings" => (T("Nav_Settings"), "\uE713"),
        _ => (T("Nav_Home"), "\uE80F")
    };

    private async Task OpenCatalogMediaAsync(
        CatalogMediaItemModel item)
    {
        if (item.Location is not { } location)
            return;

        var sourceDefinition =
            MediaSourceStore.Default.Find(location.SourceId);

        PlaybackSource playbackSource;

        if (location.Kind == MediaLocationKind.LocalFile)
        {
            if (!File.Exists(location.Locator))
                return;

            playbackSource = PlaybackSource.FromFile(
                location.Locator,
                item.DisplayTitle);
        }
        else if (location.Kind == MediaLocationKind.RemoteUri &&
                 sourceDefinition is
                 {
                     Kind: MediaSourceKind.WebDav
                 } webDavSource &&
                 Uri.TryCreate(
                     location.Locator,
                     UriKind.Absolute,
                     out var remoteUri))
        {
            if (MediaSourceProviderRegistry.TryGet(
                    MediaSourceKind.WebDav,
                    out var provider) &&
                provider is WebDavMediaSourceProvider webDavProvider)
            {
                var probe = await webDavProvider.ProbeMediaAsync(
                    webDavSource,
                    remoteUri);

                if (!probe.IsAvailable)
                {
                    await ShowCatalogMediaErrorAsync(
                        probe.ErrorCode,
                        probe.Detail);
                    return;
                }
            }

            var credential =
                MediaCredentialStore.Default.GetWebDav(
                    webDavSource);

            var access = credential is null
                ? null
                : new PlaybackNetworkAccess(
                    credential.UserName,
                    credential.Password);

            playbackSource = PlaybackSource.FromUri(
                remoteUri,
                item.DisplayTitle,
                access);
        }
        else
        {
            if (item.IsParsed)
                OpenDetail(item.DisplayTitle, startPlaying: false);

            return;
        }

        var sourceId = location.SourceId;
        var key = "media:" + sourceId + ":" + location.Locator;

        if (_tabs.TryGetValue(key, out var existing))
        {
            SelectTab(existing.Key);
            return;
        }

        var sourceLabel = sourceDefinition is null
            ? T("Source_Local")
            : sourceDefinition.IsBuiltIn
                ? T("Source_Local")
                : sourceDefinition.DisplayName;

        var state = new ShellTabState(
            key,
            ShellTabKind.Detail,
            pageKey: null,
            item.DisplayTitle,
            "\uE768",
            new PlayerView(
                item.DisplayTitle,
                sourceLabel,
                playbackSource),
            navItem: null,
            PreferredTabWidth)
        {
            MediaTitle = item.DisplayTitle
        };

        AddTab(state, select: true);
    }

    private async Task ShowCatalogMediaErrorAsync(
        string? errorCode,
        string? detail)
    {
        if (RootGrid.XamlRoot is null)
            return;

        var message = errorCode switch
        {
            "AuthenticationFailed" =>
                T("Sources_ErrorAuthentication"),
            "Forbidden" =>
                T("Sources_ErrorForbidden"),
            "NotFound" =>
                T("Sources_ErrorNotFound"),
            "Timeout" =>
                T("Sources_ErrorTimeout"),
            "NetworkError" =>
                T("Sources_ErrorNetwork"),
            _ =>
                T("Sources_ErrorGeneric")
        };

        if (!string.IsNullOrWhiteSpace(detail))
            message += "\n" + detail;

        await new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = T("Playback_RemoteOpenFailed"),
            Content = message,
            CloseButtonText = T("Common_Close")
        }.ShowAsync();
    }

    private void OpenDetail(string title, bool startPlaying)
    {
        var key = "detail:" + title;

        if (_tabs.TryGetValue(key, out var existing))
        {
            if (startPlaying)
                ShowPlayerInDetailTab(existing, title, "第18话");
            else
                ShowDetailInTab(existing, title);

            SelectTab(existing.Key);
            return;
        }

        var state = new ShellTabState(
            key,
            ShellTabKind.Detail,
            pageKey: null,
            title,
            "\uE8B2",
            new Grid(),
            navItem: null,
            PreferredTabWidth)
        {
            MediaTitle = title
        };

        if (startPlaying)
            ShowPlayerInDetailTab(state, title, "第18话");
        else
            ShowDetailInTab(state, title);

        AddTab(state, select: true);
    }

    private void ShowDetailInTab(ShellTabState state, string title)
    {
        var view = new DetailView(title);
        view.PlayRequested += (_, episode) =>
            ShowPlayerInDetailTab(state, title, episode);

        state.MediaTitle = title;
        state.Episode = null;
        ReplaceTabView(state, view);
        state.Title = title;
        state.Glyph = "\uE8B2";
        UpdateTabIdentity(state);
    }

    private void ShowPlayerInDetailTab(ShellTabState state, string title, string episode)
    {
        state.MediaTitle = title;
        state.Episode = episode;
        ReplaceTabView(state, new PlayerView(title, episode));
        state.Title = title + "—" + episode;
        state.Glyph = "\uE768";
        UpdateTabIdentity(state);
    }

    private void AddTab(ShellTabState state, bool select)
    {
        state.Visual = CreateTabVisual(state);
        _tabs.Add(state.Key, state);
        AttachTabView(state);

        if (select)
            SelectTab(state.Key);
    }

    private void AttachTabView(ShellTabState state)
    {
        if (state.View.Parent is null)
        {
            state.View.Visibility = Visibility.Collapsed;
            MainContentHost.Children.Add(state.View);
            return;
        }

        if (!ReferenceEquals(state.View.Parent, MainContentHost))
            throw new InvalidOperationException("A tab view is already attached to another visual parent.");
    }

    private void ReplaceTabView(ShellTabState state, FrameworkElement nextView)
    {
        if (ReferenceEquals(state.View, nextView))
            return;

        if (state.View is PlayerView player)
            _ = player.PrepareForDetachAsync().AsTask();

        if (state.View.Parent is Panel currentParent)
            currentParent.Children.Remove(state.View);

        state.View = nextView;
        AttachTabView(state);

        if (string.Equals(_selectedTabKey, state.Key, StringComparison.Ordinal))
            ShowTabView(state);
    }

    private void ShowTabView(ShellTabState selected)
    {
        foreach (var child in MainContentHost.Children)
            child.Visibility = ReferenceEquals(child, selected.View)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private ShellTabVisual CreateTabVisual(ShellTabState state)
    {
        var headerText = new TextBlock
        {
            Text = state.Title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var icon = new FontIcon
        {
            Glyph = state.Glyph,
            FontSize = 14,
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new Grid { ColumnSpacing = 6 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(icon);
        Grid.SetColumn(headerText, 1);
        content.Children.Add(headerText);

        var selectButton = new Button
        {
            Tag = state.Key,
            Padding = new Thickness(10, 0, 36, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        selectButton.Click += ShellTabSelect_Click;

        var closeButton = new Button
        {
            Tag = state.Key,
            Width = 28,
            Height = 28,
            MinWidth = 28,
            Margin = new Thickness(0, 2, 3, 2),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = "×", FontSize = 12, Opacity = 0.68 }
        };
        closeButton.Click += ShellTabClose_Click;

        var layer = new Grid();
        layer.Children.Add(selectButton);
        layer.Children.Add(closeButton);

        var container = new Border
        {
            Tag = state.Key,
            Width = state.PreferredWidth,
            Height = 32,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(0),
            Child = layer,
            Transitions = [new RepositionThemeTransition()]
        };

        var visual = new ShellTabVisual(container, headerText, icon);
        ApplyTabVisual(visual, selected: false, RootGrid.ActualTheme == ElementTheme.Dark);
        ShellTabItems.Children.Add(container);
        ConfigureTabInteractions(container, state.PreferredWidth);
        return visual;
    }

    private static void UpdateTabIdentity(ShellTabState state)
    {
        if (state.Visual is null) return;
        state.Visual.HeaderText.Text = state.Title;
        state.Visual.Icon.Glyph = state.Glyph;
    }

    private void ShellTabSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) SelectTab(key);
    }

    private async void ShellTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
            await CloseTabAsync(key);
    }

    private async Task CloseTabAsync(string key)
    {
        if (!_closingTabKeys.Add(key))
            return;

        try
        {
            if (!_tabs.TryGetValue(key, out var state))
                return;

            if (state.View is PlayerView player)
                await player.PrepareForDetachAsync();

            if (!_tabs.Remove(key, out state))
                return;

            if (state.Visual is not null)
                ShellTabItems.Children.Remove(state.Visual.Container);

            if (state.View.Parent is Panel parent)
                parent.Children.Remove(state.View);

            if (_selectedTabKey == key)
            {
                var next = _tabs.Values.LastOrDefault();
                if (next is null) CreateWorkspaceTab(select: true);
                else SelectTab(next.Key);
            }

            RefreshTabVisuals();
        }
        finally
        {
            _closingTabKeys.Remove(key);
        }
    }

    private void SelectTab(string key)
    {
        if (!_tabs.TryGetValue(key, out var state)) return;

        var previousKey = _selectedTabKey;
        _selectedTabKey = key;

        if (state.Kind == ShellTabKind.Detail)
        {
            ShowImmersiveChrome();
            SelectShellItem(null);
        }
        else
        {
            ShowNavigationChrome();
            SelectShellItem(state.NavItem);
        }

        ShowTabView(state);
        ApplyResponsiveLayout(force: true);

        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (previousKey is not null &&
            _tabs.TryGetValue(previousKey, out var previous) &&
            previous.Visual is not null)
        {
            ApplyTabVisual(previous.Visual, selected: false, dark);
        }

        if (state.Visual is not null)
            ApplyTabVisual(state.Visual, selected: true, dark);
    }

    private void SelectShellItem(NavigationViewItem? item)
    {
        if (ReferenceEquals(ShellNavigation.SelectedItem, item)) return;
        ShellNavigation.SelectedItem = item;
    }

    private void ShowNavigationChrome()
    {
        if (!_navigationChromeHiddenForImmersive) return;

        ShellNavigation.CompactPaneLength = 48;
        ShellNavigation.IsPaneToggleButtonVisible = true;
        ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
        _navigationChromeHiddenForImmersive = false;
        QueueNavigationPaneBackgroundUpdate();
    }

    private void ShowImmersiveChrome()
    {
        if (_navigationChromeHiddenForImmersive) return;

        _navigationChromeHiddenForImmersive = true;
        ShellNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
        ShellNavigation.CompactPaneLength = 0;
        ShellNavigation.IsPaneOpen = false;
        ShellNavigation.IsPaneToggleButtonVisible = false;
    }

    private enum ShellTabKind
    {
        Workspace,
        Detail
    }

    private sealed class ShellTabState(
        string key,
        ShellTabKind kind,
        string? pageKey,
        string title,
        string glyph,
        FrameworkElement view,
        NavigationViewItem? navItem,
        double preferredWidth)
    {
        public string Key { get; } = key;
        public ShellTabKind Kind { get; } = kind;
        public string? PageKey { get; set; } = pageKey;
        public string Title { get; set; } = title;
        public string Glyph { get; set; } = glyph;
        public FrameworkElement View { get; set; } = view;
        public NavigationViewItem? NavItem { get; set; } = navItem;
        public double PreferredWidth { get; } = preferredWidth;
        public string? MediaTitle { get; set; }
        public string? Episode { get; set; }
        public ShellTabVisual? Visual { get; set; }
    }
}

internal sealed record ShellTabVisual(Border Container, TextBlock HeaderText, FontIcon Icon);
