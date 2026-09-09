using Eizo.Localization;
using Eizo.Models;
using Eizo.Views;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Eizo;

public sealed partial class MainWindow : Window
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly Dictionary<string, ShellTabState> _tabs = new(StringComparer.Ordinal);
    private readonly WindowPlacementService _windowPlacement = new();
    private SizeInt32 _lastNormalWindowSize;
    private bool _wasWindowMaximized;
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

    public void SetPlayerVideoFullscreen(bool enabled)
    {
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

            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            return;
        }

        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        Grid.SetRow(ShellNavigation, 1);
        Grid.SetRowSpan(ShellNavigation, 1);
        RootGrid.RowDefinitions[0].Height = new GridLength(48);
        AppTitleBar.Visibility = Visibility.Visible;

        _playerFullscreen = false;

        if (_restoreMaximizedAfterPlayerFullscreen &&
            AppWindow.Presenter is OverlappedPresenter restoredPresenter)
        {
            restoredPresenter.Maximize();
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    private string T(string key) => _localization.GetString(key);

    private void ApplyLocalizedShellText()
    {
        HomeNav.Content = T("Nav_Home");
        CategoryNav.Content = T("Nav_Categories");
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

        if (pageKey is null or "categories") return;
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
        state.View = CreateWorkspaceView(pageKey);

        UpdateTabIdentity(state);

        if (string.Equals(_selectedTabKey, state.Key, StringComparison.Ordinal))
        {
            ShowNavigationChrome();
            MainContent.Content = state.View;
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

    private (string Title, string Glyph) DescribeWorkspacePage(string pageKey) => pageKey switch
    {
        "home" => (T("Nav_Home"), "\uE80F"),
        "anime" => (T("Nav_Anime"), "\uE8B2"),
        "movies" => (T("Nav_Movies"), "\uE714"),
        "series" => (T("Nav_Series"), "\uE8FD"),
        "sources" => (T("Nav_Sources"), "\uE753"),
        "cache" => (T("Nav_Cache"), "\uE7C5"),
        "about" => (T("Nav_About"), "\uE897"),
        "settings" => (T("Nav_Settings"), "\uE713"),
        _ => (T("Nav_Home"), "\uE80F")
    };

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
        {
            ShowPlayerInDetailTab(state, title, episode);
            if (string.Equals(_selectedTabKey, state.Key, StringComparison.Ordinal))
                MainContent.Content = state.View;
        };

        state.MediaTitle = title;
        state.Episode = null;
        state.View = view;
        state.Title = title;
        state.Glyph = "\uE8B2";
        UpdateTabIdentity(state);
    }

    private void ShowPlayerInDetailTab(ShellTabState state, string title, string episode)
    {
        state.MediaTitle = title;
        state.Episode = episode;
        state.View = new PlayerView(title, episode);
        state.Title = title + "—" + episode;
        state.Glyph = "\uE768";
        UpdateTabIdentity(state);

        if (string.Equals(_selectedTabKey, state.Key, StringComparison.Ordinal))
            MainContent.Content = state.View;
    }

    private void AddTab(ShellTabState state, bool select)
    {
        state.Visual = CreateTabVisual(state);
        _tabs.Add(state.Key, state);
        if (select) SelectTab(state.Key);
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

    private void ShellTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) CloseTab(key);
    }

    private void CloseTab(string key)
    {
        if (!_tabs.Remove(key, out var state)) return;

        if (state.Visual is not null)
            ShellTabItems.Children.Remove(state.Visual.Container);

        if (_selectedTabKey == key)
        {
            var next = _tabs.Values.LastOrDefault();
            if (next is null) CreateWorkspaceTab(select: true);
            else SelectTab(next.Key);
        }

        RefreshTabVisuals();
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

        MainContent.Content = state.View;
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
