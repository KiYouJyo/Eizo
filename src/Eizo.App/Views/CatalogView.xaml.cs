using System.Globalization;
using System.Text;
using Eizo.Localization;
using Eizo.Models;
using Eizo.Recognition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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

    public event EventHandler<CatalogMediaItemModel>? MediaRequested;

    private string T(string key) => _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Library");
        PageSubtitle.Text = T("Catalog_Subtitle");
        SearchBox.PlaceholderText = T("Catalog_SearchPlaceholder");
        ClearSearchButton.Content = T("Catalog_Clear");
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
                    Text = string.IsNullOrWhiteSpace(query)
                        ? T("Catalog_Empty")
                        : T("Catalog_NoResults"),
                    Margin = new Thickness(0, 10, 0, 0),
                    FontSize = 14,
                    Opacity = 0.68,
                    TextWrapping = TextWrapping.Wrap
                });
            return;
        }

        foreach (var group in items.GroupBy(item => item.Category, CatalogCategoryComparer.Default))
        {
            var heading = new TextBlock
            {
                Text = CategoryLabel(group.Key),
                Margin = new Thickness(0, ResultsPanel.Children.Count == 0 ? 0 : 12, 0, 2),
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
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

        if (item.Location is { } location)
        {
            var extensionSource = location.Locator;
            if (location.Kind == MediaLocationKind.RemoteUri &&
                Uri.TryCreate(
                    location.Locator,
                    UriKind.Absolute,
                    out var remoteUri))
            {
                extensionSource =
                    Uri.UnescapeDataString(
                        remoteUri.AbsolutePath);
            }

            var extension = Path.GetExtension(extensionSource)
                .TrimStart('.')
                .ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(extension))
                secondaryParts.Add(extension);

            if (location.SizeBytes is > 0)
                secondaryParts.Add(FormatBytes(location.SizeBytes.Value));

            var source = MediaSourceStore.Default.Find(location.SourceId);
            if (source is not null)
            {
                secondaryParts.Add(
                    source.IsBuiltIn
                        ? T("Sources_OpenedLocalFiles")
                        : source.DisplayName);
            }
        }

        var secondary = new TextBlock
        {
            Text = string.Join(" · ", secondaryParts),
            Opacity = 0.68,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        text.Children.Add(title);
        text.Children.Add(secondary);

        var type = new TextBlock
        {
            Text = RecognitionLabel(item) ?? CategoryLabel(item.Category),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.68,
            FontSize = 12
        };

        row.Children.Add(icon);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(type, 2);
        row.Children.Add(type);

        var button = new Button
        {
            Tag = item,
            Style = (Style)Resources["CatalogRowButtonStyle"],
            Content = row
        };

        if (item.Recognition is not null)
        {
            var detailsItem = new MenuFlyoutItem
            {
                Text = "Recognition details",
                Tag = item
            };
            detailsItem.Click += RecognitionDetails_Click;

            var flyout = new MenuFlyout();
            flyout.Items.Add(detailsItem);
            button.ContextFlyout = flyout;
        }

        button.Click += CatalogRow_Click;
        return button;
    }

    private void CatalogRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CatalogMediaItemModel item })
            MediaRequested?.Invoke(this, item);
    }

    private async void RecognitionDetails_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem
            {
                Tag: CatalogMediaItemModel
                {
                    Recognition: { } recognition
                }
            })
        {
            return;
        }

        var details = new TextBox
        {
            Text = BuildRecognitionDetails(recognition),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono"),
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            Title = "Recognition details",
            Content = details,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private static string BuildRecognitionDetails(
        MediaRecognitionSnapshot recognition)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Logical path: {recognition.LogicalPath}");
        builder.AppendLine($"Status: {recognition.Status}");
        builder.AppendLine($"MediaKind: {recognition.MediaKind}");
        builder.AppendLine($"SpecialKind: {recognition.SpecialKind}");
        builder.AppendLine($"EpisodePart: {recognition.EpisodePart}");
        builder.AppendLine($"Final episode: {recognition.IsFinalEpisode}");
        builder.AppendLine($"Title: {recognition.Title ?? "-"}");
        builder.AppendLine($"EpisodeTitle: {recognition.EpisodeTitle ?? "-"}");
        builder.AppendLine($"Season: {recognition.SeasonNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Cour: {recognition.CourNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Episode: {FormatNullableNumber(recognition.EpisodeNumber)}");
        builder.AppendLine($"EpisodeEnd: {FormatNullableNumber(recognition.EpisodeEndNumber)}");
        builder.AppendLine($"Special: {FormatNullableNumber(recognition.SpecialNumber)}");
        builder.AppendLine($"Year: {recognition.Year?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Confidence: {recognition.Confidence:0.000} ({recognition.ConfidenceLevel})");
        builder.AppendLine($"Ambiguous: {recognition.IsAmbiguous}");

        if (!string.IsNullOrWhiteSpace(recognition.ErrorCode))
            builder.AppendLine($"Error: {recognition.ErrorCode}");

        builder.AppendLine();
        builder.AppendLine("Title candidates:");
        if (recognition.TitleCandidates.Count == 0)
        {
            builder.AppendLine("  - none");
        }
        else
        {
            foreach (var candidate in recognition.TitleCandidates)
            {
                builder.AppendLine(
                    $"  - {candidate.Title} | {candidate.Confidence:0.000} | {candidate.Source} | primary={candidate.IsPrimary}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Evidence:");
        if (recognition.Evidence.Count == 0)
        {
            builder.AppendLine("  - none");
        }
        else
        {
            foreach (var evidence in recognition.Evidence)
            {
                builder.AppendLine(
                    $"  - {evidence.Code} | {evidence.Value ?? "-"} | {evidence.Weight:0.000}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string? RecognitionLabel(CatalogMediaItemModel item) =>
        item.Recognition switch
        {
            { Status: MediaRecognitionStatus.Recognized } recognition =>
                $"Recognition · {recognition.ConfidenceLevel}",
            { Status: MediaRecognitionStatus.Ambiguous } =>
                "Recognition · Ambiguous",
            { Status: MediaRecognitionStatus.Unresolved } =>
                "Recognition · Unresolved",
            { Status: MediaRecognitionStatus.Error } =>
                "Recognition · Error",
            _ => null
        };

    private static bool Matches(CatalogMediaItemModel item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return item.DisplayTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               item.SourceTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(item.NativeTitle) &&
                item.NativeTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
               item.Meta.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               (item.Recognition is { } recognition &&
                ((!string.IsNullOrWhiteSpace(recognition.Title) &&
                  recognition.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(recognition.EpisodeTitle) &&
                  recognition.EpisodeTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 recognition.LogicalPath.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
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

    private static string FormatNullableNumber(decimal? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0d, bytes);
        var unit = 0;

        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }

    private sealed class CatalogCategoryComparer : IEqualityComparer<MediaCategoryKind?>
    {
        public static CatalogCategoryComparer Default { get; } = new();

        public bool Equals(MediaCategoryKind? x, MediaCategoryKind? y) => x == y;

        public int GetHashCode(MediaCategoryKind? obj) =>
            obj.HasValue ? (int)obj.Value + 1 : 0;
    }
}
