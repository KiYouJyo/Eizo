using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class CatalogView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly MediaCatalogStore _catalog = MediaCatalogStore.Default;

    public CatalogView()
    {
        InitializeComponent();

        ApplyText();
        Loaded += CatalogView_Loaded;
        Unloaded += CatalogView_Unloaded;
        RebuildResults();
    }

    public event EventHandler<string>? DetailRequested;

    public event EventHandler<string>? PlayRequested;

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Categories");
        PageSubtitle.Text = T("Catalog_Subtitle");
        SearchBox.PlaceholderText = T("Catalog_SearchPlaceholder");
        ClearSearchButton.Content = T("Common_Clear");
    }

    private void CatalogView_Loaded(object sender, RoutedEventArgs e)
    {
        _catalog.Changed -= Catalog_Changed;
        _catalog.Changed += Catalog_Changed;
        RebuildResults();
    }

    private void CatalogView_Unloaded(object sender, RoutedEventArgs e) =>
        _catalog.Changed -= Catalog_Changed;

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RebuildResults);

    private void SearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            return;

        RebuildResults();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        RebuildResults();
    }

    private void RebuildResults()
    {
        if (ResultsPanel is null)
            return;

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var items = _catalog.Snapshot()
            .Where(item => Matches(item, query))
            .OrderBy(item => CategoryOrder(item.Category))
            .ThenBy(item => item.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        ResultsPanel.Children.Clear();

        if (items.Length == 0)
        {
            ResultsPanel.Children.Add(
                new TextBlock
                {
                    Text = T("Catalog_NoResults"),
                    Margin = new Thickness(0, 10, 0, 0),
                    Style = (Style)Resources["BodyText"]
                });
            return;
        }

        foreach (var group in items.GroupBy(item => item.Category, CatalogCategoryComparer.Default))
        {
            var heading = new TextBlock
            {
                Text = CategoryLabel(group.Key),
                Margin = new Thickness(0, ResultsPanel.Children.Count == 0 ? 0 : 12, 0, 2),
                Style = (Style)Resources["SectionTitleText"]
            };
            ResultsPanel.Children.Add(heading);

            foreach (var item in group)
                ResultsPanel.Children.Add(CreateRow(item));
        }
    }

    private Button CreateRow(CatalogMediaItemModel item)
    {
        var row = new Grid
        {
            ColumnSpacing = 12
        };

        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(34)
        });
        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        var icon = new FontIcon
        {
            Glyph = item.Category switch
            {
                MediaCategoryKind.Anime => "\uE8B2",
                MediaCategoryKind.Series => "\uE8FD",
                MediaCategoryKind.Movies => "\uE714",
                _ => "\uE8A5"
            },
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new StackPanel
        {
            Grid.ColumnProperty = 1,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = new TextBlock
        {
            Text = item.DisplayTitle,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var secondaryParts = new List<string>();

        if (item.IsParsed &&
            !string.IsNullOrWhiteSpace(item.SecondaryTitle) &&
            !string.Equals(
                item.SecondaryTitle,
                item.DisplayTitle,
                StringComparison.CurrentCultureIgnoreCase))
        {
            secondaryParts.Add(item.SecondaryTitle);
        }

        if (!string.IsNullOrWhiteSpace(item.Meta))
            secondaryParts.Add(item.Meta);

        if (!item.IsParsed)
            secondaryParts.Add(T("Catalog_Unparsed"));

        var secondary = new TextBlock
        {
            Text = string.Join(" · ", secondaryParts),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        text.Children.Add(title);
        text.Children.Add(secondary);

        var type = new TextBlock
        {
            Text = CategoryLabel(item.Category),
            Grid.ColumnProperty = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        };

        row.Children.Add(icon);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(type, 2);
        row.Children.Add(type);

        var button = new Button
        {
            Tag = item.DisplayTitle,
            Style = (Style)Resources["CatalogRowButtonStyle"],
            Content = row
        };

        button.Click += CatalogRow_Click;
        return button;
    }

    private void CatalogRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string title })
            DetailRequested?.Invoke(this, title);
    }

    private static bool Matches(CatalogMediaItemModel item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return item.DisplayTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               item.SourceTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(item.NativeTitle) &&
                item.NativeTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
               item.Meta.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private string CategoryLabel(MediaCategoryKind? category) => category switch
    {
        MediaCategoryKind.Anime => T("Nav_Anime"),
        MediaCategoryKind.Series => T("Nav_Series"),
        MediaCategoryKind.Movies => T("Nav_Movies"),
        _ => T("Catalog_Unparsed")
    };

    private static int CategoryOrder(MediaCategoryKind? category) => category switch
    {
        MediaCategoryKind.Anime => 0,
        MediaCategoryKind.Series => 1,
        MediaCategoryKind.Movies => 2,
        _ => 3
    };

    private sealed class CatalogCategoryComparer : IEqualityComparer<MediaCategoryKind?>
    {
        public static CatalogCategoryComparer Default { get; } = new();

        public bool Equals(MediaCategoryKind? x, MediaCategoryKind? y) => x == y;

        public int GetHashCode(MediaCategoryKind? obj) =>
            obj.HasValue ? (int)obj.Value + 1 : 0;
    }
}
