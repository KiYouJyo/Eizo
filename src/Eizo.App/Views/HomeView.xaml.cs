using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class HomeView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly IReadOnlyList<MediaCardModel> _continueItems;
    private readonly IReadOnlyList<MediaCardModel> _seasonItems;
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;

    public event EventHandler<string>? DetailRequested;
    public event EventHandler<string>? PlayRequested;

    public HomeView()
    {
        InitializeComponent();
        ApplyText();

        _continueItems =
        [
            new MediaCardModel("葬送的芙莉莲", "葬送のフリーレン", "18 / 28", 52),
            new MediaCardModel("药屋少女的呢喃", "薬屋のひとりごと", "14 / 24", 36),
            new MediaCardModel("VIVANT", "VIVANT", "6 / 10", 64),
            new MediaCardModel("非自然死亡", "アンナチュラル", "5 / 10", 44)
        ];

        _seasonItems =
        [
            new MediaCardModel("胆大党 第3期", "ダンダダン", "2026 秋"),
            new MediaCardModel("间谍过家家 第4期", "SPY×FAMILY", "2026 秋"),
            new MediaCardModel("链锯人", "チェンソーマン", "更新中"),
            new MediaCardModel("Re:从零开始", "Re:ゼロから始める異世界生活", "更新中"),
            new MediaCardModel("蓝色监狱", "ブルーロック", "更新中")
        ];

        RebuildMediaGrids();
    }

    internal void SetResponsiveMode(ResponsiveLayoutMode mode)
    {
        if (_responsiveMode == mode && ContinueGrid.Children.Count > 0) return;
        _responsiveMode = mode;
        RebuildMediaGrids();
    }

    private void RebuildMediaGrids()
    {
        var columns = _responsiveMode switch
        {
            ResponsiveLayoutMode.Large => 4,
            ResponsiveLayoutMode.Medium => 2,
            _ => 1
        };

        PopulateGrid(ContinueGrid, _continueItems, columns, "ContinueCardTemplate");
        PopulateGrid(SeasonGrid, _seasonItems, columns, "SeasonCardTemplate");
    }

    private void PopulateGrid(
        Grid grid,
        IReadOnlyList<MediaCardModel> items,
        int columns,
        string templateKey)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        columns = Math.Max(1, Math.Min(columns, Math.Max(1, items.Count)));
        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());

        var rows = (items.Count + columns - 1) / columns;
        for (var row = 0; row < rows; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var template = (DataTemplate)Resources[templateKey];
        for (var index = 0; index < items.Count; index++)
        {
            var presenter = new ContentControl
            {
                Content = items[index],
                ContentTemplate = template,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(presenter, index / columns);
            Grid.SetColumn(presenter, index % columns);
            grid.Children.Add(presenter);
        }
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Home");
        SearchBox.PlaceholderText = T("Search_Placeholder");
        FeaturedEyebrow.Text = T("Home_Featured");
        FeaturedDescription.Text = T("Home_FeaturedDescription");
        FeaturedPlayText.Text = T("Common_Play");
        FeaturedDetailsButton.Content = T("Common_ViewDetails");
        ContinueTitle.Text = T("Section_ContinueWatching");
        SeasonTitle.Text = T("Section_CurrentSeason");
        ContinueAllButton.Content = T("Common_ViewAll");
        SeasonAllButton.Content = T("Common_ViewAll");
    }

    private void FeaturedButton_Click(object sender, RoutedEventArgs e) =>
        DetailRequested?.Invoke(this, "葬送的芙莉莲");

    private void FeaturedPlayButton_Click(object sender, RoutedEventArgs e) =>
        PlayRequested?.Invoke(this, "葬送的芙莉莲");

    private void MediaCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string title })
            DetailRequested?.Invoke(this, title);
    }
}
