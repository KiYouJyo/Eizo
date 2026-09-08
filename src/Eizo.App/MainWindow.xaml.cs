using Eizo.Localization;
using Eizo.Models;
using Eizo.Views;
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
    private string? _selectedTabKey;
    private bool _navigationChromeHiddenForImmersive;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Eizo";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        UpdateTitleBarColors();

        AppWindow.Resize(new SizeInt32(1440, 960));
        ApplyLocalizedShellText();

        RootGrid.ActualThemeChanged += (_, _) =>
        {
            UpdateTitleBarColors();
            RefreshTabVisuals();
            QueueNavigationPaneBackgroundUpdate();
        };

        CreateHomeTab(select: true);
    }

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

        switch (item.Tag?.ToString())
        {
            case "home":
                SelectMostRecentHome();
                break;
            case "anime":
                OpenCategory(MediaCategoryKind.Anime, item);
                break;
            case "movies":
                OpenCategory(MediaCategoryKind.Movies, item);
                break;
            case "series":
                OpenCategory(MediaCategoryKind.Series, item);
                break;
            case "sources":
                OpenSingleton("sources", T("Nav_Sources"), "\uE753", new SourcesView(), item);
                break;
            case "cache":
                OpenSingleton("cache", T("Nav_Cache"), "\uE7C5", new CacheView(), item);
                break;
            case "about":
                OpenSingleton("about", T("Nav_About"), "\uE897", new AboutView(), item);
                break;
            case "settings":
                OpenSingleton("settings", T("Nav_Settings"), "\uE713", new SettingsView(), item);
                break;
        }
    }

    private void ShellNewTabButton_Click(object sender, RoutedEventArgs e) => CreateHomeTab(select: true);

    private void CreateHomeTab(bool select)
    {
        var key = "home:" + Guid.NewGuid().ToString("N");
        var view = new HomeView();
        view.DetailRequested += (_, title) => OpenDetail(title);
        view.PlayRequested += (_, title) => OpenPlayer(title, "第18话");

        AddTab(
            new ShellTabState(key, T("Nav_Home"), "\uE80F", view, HomeNav, immersive: false, PreferredTabWidth),
            select);
    }

    private void SelectMostRecentHome()
    {
        var home = _tabs.Values.LastOrDefault(tab => tab.Key.StartsWith("home:", StringComparison.Ordinal));
        if (home is null) CreateHomeTab(select: true);
        else SelectTab(home.Key);
    }

    private void OpenCategory(MediaCategoryKind kind, NavigationViewItem navItem)
    {
        var key = kind switch
        {
            MediaCategoryKind.Anime => "anime",
            MediaCategoryKind.Movies => "movies",
            _ => "series"
        };

        var title = kind switch
        {
            MediaCategoryKind.Anime => T("Nav_Anime"),
            MediaCategoryKind.Movies => T("Nav_Movies"),
            _ => T("Nav_Series")
        };

        if (_tabs.TryGetValue(key, out var existing))
        {
            SelectTab(existing.Key);
            return;
        }

        var view = new CategoryView(kind);
        view.DetailRequested += (_, name) => OpenDetail(name);
        view.PlayRequested += (_, name) => OpenPlayer(name, "第18话");

        AddTab(new ShellTabState(key, title, "\uE8B2", view, navItem, immersive: false, PreferredTabWidth), select: true);
    }

    private void OpenSingleton(string key, string title, string glyph, FrameworkElement view, NavigationViewItem navItem)
    {
        if (_tabs.TryGetValue(key, out var existing))
        {
            SelectTab(existing.Key);
            return;
        }

        AddTab(new ShellTabState(key, title, glyph, view, navItem, immersive: false, PreferredTabWidth), select: true);
    }

    private void OpenDetail(string title)
    {
        var key = "detail:" + title;
        if (_tabs.TryGetValue(key, out var existing))
        {
            SelectTab(existing.Key);
            return;
        }

        var view = new DetailView(title);
        view.PlayRequested += (_, episode) => OpenPlayer(title, episode);
        AddTab(new ShellTabState(key, title, "\uE8B2", view, navItem: null, immersive: true, PreferredTabWidth), select: true);
    }

    private void OpenPlayer(string title, string episode)
    {
        var key = "player:" + title + ":" + episode;
        if (_tabs.TryGetValue(key, out var existing))
        {
            SelectTab(existing.Key);
            return;
        }

        var label = title + "—" + episode;
        AddTab(new ShellTabState(key, label, "\uE768", new PlayerView(title, episode), navItem: null, immersive: true, PreferredTabWidth), select: true);
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

        var visual = new ShellTabVisual(container, headerText);
        ApplyTabVisual(visual, selected: false, RootGrid.ActualTheme == ElementTheme.Dark);
        ShellTabItems.Children.Add(container);
        ConfigureTabInteractions(container, state.PreferredWidth);
        return visual;
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
            if (next is null) CreateHomeTab(select: true);
            else SelectTab(next.Key);
        }

        RefreshTabVisuals();
    }

    private void SelectTab(string key)
    {
        if (!_tabs.TryGetValue(key, out var state)) return;
        if (string.Equals(_selectedTabKey, key, StringComparison.Ordinal))
        {
            MainContent.Content = state.View;
            return;
        }

        var previousKey = _selectedTabKey;
        _selectedTabKey = key;

        if (state.Immersive) ShowImmersiveChrome();
        else ShowNavigationChrome();

        MainContent.Content = state.View;

        if (state.NavItem is not null)
            SelectShellItem(state.NavItem);
        else
            SelectShellItem(null);

        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (previousKey is not null && _tabs.TryGetValue(previousKey, out var previous) && previous.Visual is not null)
            ApplyTabVisual(previous.Visual, selected: false, dark);
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

    private sealed class ShellTabState(
        string key,
        string title,
        string glyph,
        FrameworkElement view,
        NavigationViewItem? navItem,
        bool immersive,
        double preferredWidth)
    {
        public string Key { get; } = key;
        public string Title { get; } = title;
        public string Glyph { get; } = glyph;
        public FrameworkElement View { get; } = view;
        public NavigationViewItem? NavItem { get; } = navItem;
        public bool Immersive { get; } = immersive;
        public double PreferredWidth { get; } = preferredWidth;
        public ShellTabVisual? Visual { get; set; }
    }
}

internal sealed record ShellTabVisual(Border Container, TextBlock HeaderText);
