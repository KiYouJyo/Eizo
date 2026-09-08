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
using Windows.UI;

namespace Eizo;

public sealed partial class MainWindow : Window
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly Dictionary<string, ShellTabState> _tabs = new(StringComparer.Ordinal);
    private string? _selectedTabKey;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Eizo";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.Resize(new SizeInt32(1440, 960));
        ApplyLocalizedShellText();
        WindowRoot.ActualThemeChanged += (_, _) => RefreshTabVisuals();
        CreateHomeTab(true);
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyLocalizedShellText()
    {
        HomeNav.Content=T("Nav_Home"); CategoryNav.Content=T("Nav_Categories"); AnimeNav.Content=T("Nav_Anime");
        MoviesNav.Content=T("Nav_Movies"); SeriesNav.Content=T("Nav_Series"); SourcesNav.Content=T("Nav_Sources");
        CacheNav.Content=T("Nav_Cache"); AboutNav.Content=T("Nav_About"); SettingsNav.Content=T("Nav_Settings");
    }

    private void ShellNavigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item) return;
        switch (item.Tag?.ToString())
        {
            case "home": SelectMostRecentHome(); break;
            case "anime": OpenCategory(MediaCategoryKind.Anime,item); break;
            case "movies": OpenCategory(MediaCategoryKind.Movies,item); break;
            case "series": OpenCategory(MediaCategoryKind.Series,item); break;
            case "sources": OpenSingleton("sources",T("Nav_Sources"),"\uE753",new SourcesView(),item); break;
            case "cache": OpenSingleton("cache",T("Nav_Cache"),"\uE7C5",new CacheView(),item); break;
            case "about": OpenSingleton("about",T("Nav_About"),"\uE897",new AboutView(),item); break;
            case "settings": OpenSingleton("settings",T("Nav_Settings"),"\uE713",new SettingsView(),item); break;
        }
    }

    private void ShellNewTabButton_Click(object sender,RoutedEventArgs e)=>CreateHomeTab(true);

    private void CreateHomeTab(bool select)
    {
        var key="home:"+Guid.NewGuid().ToString("N");
        var view=new HomeView();
        view.DetailRequested+=(_,title)=>OpenDetail(title);
        view.PlayRequested+=(_,title)=>OpenPlayer(title,"第18话");
        AddTab(new(key,T("Nav_Home"),"\uE80F",view,HomeNav,false,190),select);
    }

    private void SelectMostRecentHome()
    {
        var home=_tabs.Values.LastOrDefault(t=>t.Key.StartsWith("home:",StringComparison.Ordinal));
        if(home is null) CreateHomeTab(true); else SelectTab(home.Key);
    }

    private void OpenCategory(MediaCategoryKind kind,NavigationViewItem nav)
    {
        var key=kind==MediaCategoryKind.Anime?"anime":kind==MediaCategoryKind.Movies?"movies":"series";
        var title=kind==MediaCategoryKind.Anime?T("Nav_Anime"):kind==MediaCategoryKind.Movies?T("Nav_Movies"):T("Nav_Series");
        if(_tabs.TryGetValue(key,out var existing)){SelectTab(existing.Key);return;}
        var view=new CategoryView(kind);
        view.DetailRequested+=(_,name)=>OpenDetail(name);
        view.PlayRequested+=(_,name)=>OpenPlayer(name,"第18话");
        AddTab(new(key,title,"\uE8B2",view,nav,false,190),true);
    }

    private void OpenSingleton(string key,string title,string glyph,FrameworkElement view,NavigationViewItem nav)
    {
        if(_tabs.TryGetValue(key,out var existing)){SelectTab(existing.Key);return;}
        AddTab(new(key,title,glyph,view,nav,false,190),true);
    }

    private void OpenDetail(string title)
    {
        var key="detail:"+title;
        if(_tabs.TryGetValue(key,out var existing)){SelectTab(existing.Key);return;}
        var view=new DetailView(title);
        view.PlayRequested+=(_,episode)=>OpenPlayer(title,episode);
        AddTab(new(key,title,"\uE8B2",view,null,true,236),true);
    }

    private void OpenPlayer(string title,string episode)
    {
        var key="player:"+title+":"+episode;
        if(_tabs.TryGetValue(key,out var existing)){SelectTab(existing.Key);return;}
        AddTab(new(key,title+"—"+episode,"\uE768",new PlayerView(title,episode),null,true,260),true);
    }

    private void AddTab(ShellTabState state,bool select)
    {
        state.Visual=CreateTabVisual(state); _tabs.Add(state.Key,state); if(select) SelectTab(state.Key);
    }

    private Border CreateTabVisual(ShellTabState state)
    {
        var icon=new FontIcon{Glyph=state.Glyph,FontSize=13,Width=16,Height=16,VerticalAlignment=VerticalAlignment.Center};
        var title=new TextBlock{Text=state.Title,FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxLines=1};
        var content=new Grid{ColumnSpacing=6}; content.ColumnDefinitions.Add(new(){Width=new GridLength(16)}); content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        content.Children.Add(icon); Grid.SetColumn(title,1); content.Children.Add(title);
        var select=new Button{Tag=state.Key,Padding=new Thickness(10,0,34,0),Background=new SolidColorBrush(Colors.Transparent),BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(7),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=content};
        select.Click+=ShellTabSelect_Click;
        var close=new Button{Tag=state.Key,Width=28,Height=28,MinWidth=28,Margin=new Thickness(0,2,3,2),Padding=new Thickness(0),Background=new SolidColorBrush(Colors.Transparent),BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(6),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Content=new FontIcon{Glyph="\uE711",FontSize=10}};
        close.Click+=ShellTabClose_Click;
        var layer=new Grid(); layer.Children.Add(select); layer.Children.Add(close);
        var border=new Border{Tag=state.Key,Width=state.Width,Height=32,CornerRadius=new CornerRadius(7),BorderThickness=new Thickness(1),Child=layer,Transitions=[new RepositionThemeTransition()]};
        ShellTabItems.Children.Add(border); return border;
    }

    private void ShellTabSelect_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:string key})SelectTab(key);}
    private void ShellTabClose_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:string key}){CloseTab(key);}}

    private void CloseTab(string key)
    {
        if(!_tabs.Remove(key,out var state))return;
        ShellTabItems.Children.Remove(state.Visual);
        if(_selectedTabKey==key){var next=_tabs.Values.LastOrDefault();if(next is null)CreateHomeTab(true);else SelectTab(next.Key);}
        RefreshTabVisuals();
    }

    private void SelectTab(string key)
    {
        if(!_tabs.TryGetValue(key,out var state))return;
        _selectedTabKey=key; MainContent.Content=state.View; ShellNavigation.IsPaneVisible=!state.Immersive;
        ShellNavigation.SelectedItem=state.NavItem; RefreshTabVisuals();
    }

    private void RefreshTabVisuals()
    {
        var selected=ResolveBrush("EizoSelectionBrush");var normal=ResolveBrush("EizoPanelBrush");var border=ResolveBrush("EizoBorderBrush");
        foreach(var state in _tabs.Values) if(state.Visual is not null){state.Visual.Background=state.Key==_selectedTabKey?selected:normal;state.Visual.BorderBrush=border;}
    }

    private static Brush ResolveBrush(string key)=>Application.Current.Resources.TryGetValue(key,out var value)&&value is Brush brush?brush:new SolidColorBrush(Colors.Transparent);

    private sealed class ShellTabState(string key,string title,string glyph,FrameworkElement view,NavigationViewItem? navItem,bool immersive,double width)
    {
        public string Key{get;}=key; public string Title{get;}=title; public string Glyph{get;}=glyph; public FrameworkElement View{get;}=view;
        public NavigationViewItem? NavItem{get;}=navItem; public bool Immersive{get;}=immersive; public double Width{get;}=width; public Border? Visual{get;set;}
    }
}
