using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class CategoryView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly MediaCategoryKind _kind;
    private IReadOnlyList<MediaCardModel> _libraryItems = [];
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;

    public event EventHandler<string>? DetailRequested;
    public event EventHandler<string>? PlayRequested;

    public CategoryView(MediaCategoryKind kind)
    {
        _kind = kind;
        InitializeComponent();
        ApplyText();
        ConfigureDemoContent();
        RebuildLibraryGrid();
    }

    internal void SetResponsiveMode(ResponsiveLayoutMode mode)
    {
        if (_responsiveMode == mode && LibraryGrid.Children.Count > 0) return;
        _responsiveMode = mode;
        RebuildLibraryGrid();
    }

    private void RebuildLibraryGrid()
    {
        LibraryGrid.Children.Clear();
        LibraryGrid.ColumnDefinitions.Clear();
        LibraryGrid.RowDefinitions.Clear();

        var columns = _responsiveMode switch
        {
            ResponsiveLayoutMode.Large => 3,
            ResponsiveLayoutMode.Medium => 2,
            _ => 1
        };
        columns = Math.Max(1, Math.Min(columns, Math.Max(1, _libraryItems.Count)));

        for (var column = 0; column < columns; column++)
            LibraryGrid.ColumnDefinitions.Add(new ColumnDefinition());

        var rows = (_libraryItems.Count + columns - 1) / columns;
        for (var row = 0; row < rows; row++)
            LibraryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var template = (DataTemplate)Resources["LibraryCardTemplate"];
        for (var index = 0; index < _libraryItems.Count; index++)
        {
            var presenter = new ContentControl
            {
                Content = _libraryItems[index],
                ContentTemplate = template,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(presenter, index / columns);
            Grid.SetColumn(presenter, index % columns);
            LibraryGrid.Children.Add(presenter);
        }
    }

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = _kind switch
        {
            MediaCategoryKind.Anime => T("Nav_Anime"),
            MediaCategoryKind.Movies => T("Nav_Movies"),
            _ => T("Nav_Series")
        };

        SearchBox.PlaceholderText = T("Search_Placeholder");
        LibraryTitle.Text = T("Category_MyLibrary");
        FeaturedPlayButton.Content = T("Common_Play");
        FeaturedDetailsButton.Content = T("Common_ViewDetails");

        FilterList.ItemsSource =
        [
            T("Common_All"),
            _kind == MediaCategoryKind.Anime ? "2026 秋" : T("Common_Recent"),
            T("Category_Following"),
            T("Category_Unwatched"),
            _kind == MediaCategoryKind.Anime ? "OVA / OAD" : T("Common_Favorites")
        ];
    }

    private void ConfigureDemoContent()
    {
        if (_kind == MediaCategoryKind.Anime)
        {
            FeaturedTitle.Text = "葬送的芙莉莲";
            FeaturedNativeTitle.Text = "葬送のフリーレン";
            FeaturedMeta.Text = "2023 · NTV · 28 · MADHOUSE";
            _libraryItems =
            [
                new MediaCardModel("胆大党", "ダンダダン", "2026 秋"),
                new MediaCardModel("间谍过家家", "SPY×FAMILY", "2026 秋"),
                new MediaCardModel("链锯人", "チェンソーマン", "12"),
                new MediaCardModel("蓝色监狱", "ブルーロック", "24"),
                new MediaCardModel("药屋少女的呢喃", "薬屋のひとりごと", "24"),
                new MediaCardModel("孤独摇滚！", "ぼっち・ざ・ろっく！", "12")
            ];
        }
        else if (_kind == MediaCategoryKind.Series)
        {
            FeaturedTitle.Text = "海中沉睡的钻石";
            FeaturedNativeTitle.Text = "海に眠るダイヤモンド";
            FeaturedMeta.Text = "2024 · TBS · 10";
            _libraryItems =
            [
                new MediaCardModel("非自然死亡", "アンナチュラル", "2018 · TBS"),
                new MediaCardModel("VIVANT", "VIVANT", "2023 · TBS"),
                new MediaCardModel("半泽直树", "半沢直樹", "2020 · TBS"),
                new MediaCardModel("重启人生", "ブラッシュアップライフ", "2023")
            ];
        }
        else
        {
            FeaturedTitle.Text = "Sample Movie";
            FeaturedNativeTitle.Text = "Sample Movie";
            FeaturedMeta.Text = "2026 · 120 min";
            _libraryItems =
            [
                new MediaCardModel("Movie A", "Movie A", "2026"),
                new MediaCardModel("Movie B", "Movie B", "2025"),
                new MediaCardModel("Movie C", "Movie C", "2024")
            ];
        }

        FeaturedDescription.Text = T("Category_FeaturedDescription");
    }

    private void FeaturedButton_Click(object sender, RoutedEventArgs e) =>
        DetailRequested?.Invoke(this, FeaturedTitle.Text);

    private void FeaturedPlayButton_Click(object sender, RoutedEventArgs e) =>
        PlayRequested?.Invoke(this, FeaturedTitle.Text);

    private void MediaCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string title })
            DetailRequested?.Invoke(this, title);
    }
}
