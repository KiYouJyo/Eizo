using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Eizo.Localization;
using Eizo.Models;
using Eizo.Recognition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Eizo.Views;

public sealed partial class CatalogView : UserControl
{
    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly MediaCatalogStore _catalog = MediaCatalogStore.Default;
    private readonly MediaSourceStore _sources = MediaSourceStore.Default;
    private readonly DispatcherQueueTimer _searchTimer;

    public CatalogView()
    {
        InitializeComponent();

        _searchTimer = DispatcherQueue.CreateTimer();
        _searchTimer.Interval = TimeSpan.FromMilliseconds(180);
        _searchTimer.IsRepeating = false;
        _searchTimer.Tick += SearchTimer_Tick;

        ApplyText();
        Loaded += CatalogView_Loaded;
        Unloaded += CatalogView_Unloaded;
    }

    public event EventHandler<CatalogMediaItemModel>? MediaRequested;

    private string T(string key) => _localization.GetString(key);

    private string L(string zhCn, string jaJp, string enUs) =>
        _localization.CurrentLanguage switch
        {
            "ja-JP" => jaJp,
            "en-US" => enUs,
            _ => zhCn
        };

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Library");
        PageSubtitle.Text = T("Catalog_Subtitle");
        SearchBox.PlaceholderText = T("Catalog_SearchPlaceholder");
        ClearSearchButton.Content = T("Catalog_Clear");
        ExportRecognitionButton.Content = L(
            "导出识别报告",
            "認識レポートを出力",
            "Export recognition report");
    }

    private void CatalogView_Loaded(object sender, RoutedEventArgs e)
    {
        _catalog.Changed -= Catalog_Changed;
        _catalog.Changed += Catalog_Changed;
        RefreshResults();
    }

    private void CatalogView_Unloaded(object sender, RoutedEventArgs e)
    {
        _catalog.Changed -= Catalog_Changed;
        _searchTimer.Stop();
    }

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshResults);

    private void SearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            return;

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        RefreshResults();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        _searchTimer.Stop();
        SearchBox.Text = string.Empty;
        RefreshResults();
    }

    private void RefreshResults()
    {
        if (ResultsList is null)
            return;

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var snapshot = _catalog.SnapshotForDisplay();
        UpdateRecognitionSummary(snapshot);

        var filtered = snapshot
            .Where(item => Matches(item, query))
            .OrderBy(item => CategoryOrder(item.Category))
            .ThenBy(item => item.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (filtered.Length == 0)
        {
            ((CollectionViewSource)Resources["GroupedCatalogItems"]).Source =
                Array.Empty<CatalogGroup>();
            EmptyStateText.Text = string.IsNullOrWhiteSpace(query)
                ? T("Catalog_Empty")
                : T("Catalog_NoResults");
            EmptyStateText.Visibility = Visibility.Visible;
            ResultsList.Visibility = Visibility.Collapsed;
            return;
        }

        var sourceLabels = _sources
            .Snapshot()
            .ToDictionary(
                source => source.Id,
                source => source.IsBuiltIn
                    ? T("Sources_OpenedLocalFiles")
                    : source.DisplayName,
                StringComparer.Ordinal);

        var groups = filtered
            .GroupBy(item => item.Category, CatalogCategoryComparer.Default)
            .Select(group =>
                new CatalogGroup(
                    CategoryLabel(group.Key),
                    group.Select(item => CreateListItem(item, sourceLabels))))
            .ToArray();

        ((CollectionViewSource)Resources["GroupedCatalogItems"]).Source = groups;
        EmptyStateText.Visibility = Visibility.Collapsed;
        ResultsList.Visibility = Visibility.Visible;
    }

    private CatalogListItemViewModel CreateListItem(
        CatalogMediaItemModel item,
        IReadOnlyDictionary<string, string> sourceLabels)
    {
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
                Uri.TryCreate(location.Locator, UriKind.Absolute, out var remoteUri))
            {
                extensionSource = Uri.UnescapeDataString(remoteUri.AbsolutePath);
            }

            var extension = Path.GetExtension(extensionSource)
                .TrimStart('.')
                .ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(extension))
                secondaryParts.Add(extension);

            if (location.SizeBytes is > 0)
                secondaryParts.Add(FormatBytes(location.SizeBytes.Value));

            if (sourceLabels.TryGetValue(location.SourceId, out var sourceLabel))
                secondaryParts.Add(sourceLabel);
        }

        return new CatalogListItemViewModel(
            item,
            item.Category switch
            {
                MediaCategoryKind.Anime => "\uE8B2",
                MediaCategoryKind.Series => "\uE8FD",
                MediaCategoryKind.Movies => "\uE714",
                _ => "\uE8A5"
            },
            item.DisplayTitle,
            string.Join(" · ", secondaryParts),
            RecognitionLabel(item) ?? CategoryLabel(item.Category));
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CatalogListItemViewModel viewModel)
            MediaRequested?.Invoke(this, viewModel.Item);
    }

    private void ResultsList_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container ||
            args.Item is not CatalogListItemViewModel viewModel)
        {
            return;
        }

        if (viewModel.Item.Recognition is null)
        {
            container.ContextFlyout = null;
            return;
        }

        var detailsItem = new MenuFlyoutItem
        {
            Text = "Recognition details",
            Tag = viewModel.Item
        };
        detailsItem.Click += RecognitionDetails_Click;

        var flyout = new MenuFlyout();
        flyout.Items.Add(detailsItem);
        container.ContextFlyout = flyout;
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

    private async void ExportRecognitionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (App.MainWindow is null)
            return;

        await _catalog.EnsureRecognitionRuntimeCurrentAsync();
        var snapshot = _catalog.SnapshotForDisplay();
        if (snapshot.Count == 0)
            return;

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"Eizo-Recognition-Report-{DateTime.Now:yyyyMMdd-HHmmss}"
        };
        picker.FileTypeChoices.Add(
            "CSV",
            new List<string> { ".csv" });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        var sourceLabels = _sources
            .Snapshot()
            .ToDictionary(
                source => source.Id,
                source => source.IsBuiltIn
                    ? T("Sources_OpenedLocalFiles")
                    : source.DisplayName,
                StringComparer.Ordinal);

        var csv = await Task.Run(
            () => BuildRecognitionCsv(snapshot, sourceLabels));

        await FileIO.WriteTextAsync(file, csv, UnicodeEncoding.Utf8);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = L("识别报告已导出", "認識レポートを出力しました", "Recognition report exported"),
            Content = L(
                "CSV 已包含原始文件名、逻辑路径、识别标题、状态、置信度、候选标题和证据。可直接按 NeedsReview 或 ReviewPriority 筛选后交给我分析。",
                "CSV には元ファイル名、論理パス、認識タイトル、状態、信頼度、タイトル候補、根拠が含まれます。NeedsReview または ReviewPriority で絞り込めます。",
                "The CSV includes original names, logical paths, recognized titles, status, confidence, title candidates and evidence. Filter NeedsReview or ReviewPriority before sharing it for analysis."),
            CloseButtonText = L("关闭", "閉じる", "Close")
        };
        await dialog.ShowAsync();
    }

    private void UpdateRecognitionSummary(
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var recognized = 0;
        var ambiguous = 0;
        var unresolved = 0;
        var errors = 0;
        var missing = 0;
        var review = 0;

        foreach (var item in items)
        {
            var recognition = item.Recognition;
            if (recognition is null)
            {
                missing++;
                review++;
                continue;
            }

            switch (recognition.Status)
            {
                case MediaRecognitionStatus.Recognized:
                    recognized++;
                    break;
                case MediaRecognitionStatus.Ambiguous:
                    ambiguous++;
                    break;
                case MediaRecognitionStatus.Unresolved:
                    unresolved++;
                    break;
                case MediaRecognitionStatus.Error:
                    errors++;
                    break;
            }

            if (NeedsReview(recognition))
                review++;
        }

        RecognitionSummary.Text = L(
            $"识别报告：共 {items.Count} · 已识别 {recognized} · 歧义 {ambiguous} · 未解决 {unresolved} · 错误 {errors} · 无快照 {missing} · 建议复核 {review}",
            $"認識レポート：合計 {items.Count} · 認識済み {recognized} · 曖昧 {ambiguous} · 未解決 {unresolved} · エラー {errors} · スナップショットなし {missing} · 要確認 {review}",
            $"Recognition report: {items.Count} total · {recognized} recognized · {ambiguous} ambiguous · {unresolved} unresolved · {errors} errors · {missing} missing snapshots · {review} review candidates");
    }

    private static string BuildRecognitionCsv(
        IReadOnlyList<CatalogMediaItemModel> items,
        IReadOnlyDictionary<string, string> sourceLabels)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "NeedsReview,ReviewPriority,ReviewReason,RuntimeVersion,Source,OriginalName,LogicalPath,Status,ConfidenceLevel,Confidence,IsAmbiguous,AppliedDisplayTitle,RecognizedTitle,EpisodeTitle,MediaKind,SpecialKind,EpisodePart,IsFinalEpisode,Season,Cour,Episode,EpisodeEnd,Special,Year,ErrorCode,TitleCandidates,Evidence");

        foreach (var item in items)
        {
            var recognition = item.Recognition;
            var source = item.Location is { } location &&
                         sourceLabels.TryGetValue(location.SourceId, out var sourceLabel)
                ? sourceLabel
                : string.Empty;

            var reviewPriority = ReviewPriority(recognition);
            var needsReview = !string.IsNullOrEmpty(reviewPriority);
            var titleCandidates = recognition is null
                ? string.Empty
                : string.Join(
                    " || ",
                    recognition.TitleCandidates.Select(candidate =>
                        $"{candidate.Title} [{candidate.Confidence:0.000}; {candidate.Source}; primary={candidate.IsPrimary}]"));
            var evidence = recognition is null
                ? string.Empty
                : string.Join(
                    " || ",
                    recognition.Evidence.Select(itemEvidence =>
                        $"{itemEvidence.Code}={itemEvidence.Value ?? "-"} [{itemEvidence.Weight:0.000}]"));

            AppendCsvRow(
                builder,
                needsReview ? "true" : "false",
                reviewPriority,
                ReviewReason(recognition),
                recognition?.RuntimeVersion ?? string.Empty,
                source,
                item.SourceTitle,
                recognition?.LogicalPath ?? string.Empty,
                recognition?.Status.ToString() ?? "Missing",
                recognition?.ConfidenceLevel ?? string.Empty,
                recognition?.Confidence.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                recognition?.IsAmbiguous.ToString() ?? string.Empty,
                recognition?.ShouldApplyDisplayTitle.ToString() ?? "false",
                recognition?.Title ?? string.Empty,
                recognition?.EpisodeTitle ?? string.Empty,
                recognition?.MediaKind ?? string.Empty,
                recognition?.SpecialKind ?? string.Empty,
                recognition?.EpisodePart ?? string.Empty,
                recognition?.IsFinalEpisode.ToString() ?? string.Empty,
                recognition?.SeasonNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                recognition?.CourNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                recognition is null ? string.Empty : FormatNullableNumber(recognition.EpisodeNumber),
                recognition is null ? string.Empty : FormatNullableNumber(recognition.EpisodeEndNumber),
                recognition is null ? string.Empty : FormatNullableNumber(recognition.SpecialNumber),
                recognition?.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                recognition?.ErrorCode ?? string.Empty,
                titleCandidates,
                evidence);
        }

        return builder.ToString();
    }

    private static void AppendCsvRow(
        StringBuilder builder,
        params string[] values)
    {
        builder.AppendLine(string.Join(',', values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool NeedsReview(MediaRecognitionSnapshot recognition) =>
        !string.IsNullOrEmpty(ReviewPriority(recognition));

    private static string ReviewPriority(MediaRecognitionSnapshot? recognition)
    {
        if (recognition is null)
            return "High";

        if (recognition.Status is MediaRecognitionStatus.Error or
            MediaRecognitionStatus.Unresolved or
            MediaRecognitionStatus.Ambiguous ||
            recognition.IsAmbiguous)
        {
            return "High";
        }

        if (string.IsNullOrWhiteSpace(recognition.Title) ||
            string.Equals(recognition.ConfidenceLevel, "Low", StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        if (string.Equals(recognition.ConfidenceLevel, "Medium", StringComparison.OrdinalIgnoreCase))
            return "Low";

        return string.Empty;
    }

    private static string ReviewReason(MediaRecognitionSnapshot? recognition)
    {
        if (recognition is null)
            return "MissingSnapshot";
        if (recognition.Status == MediaRecognitionStatus.Error)
            return string.IsNullOrWhiteSpace(recognition.ErrorCode)
                ? "Error"
                : $"Error:{recognition.ErrorCode}";
        if (recognition.Status == MediaRecognitionStatus.Unresolved)
            return "Unresolved";
        if (recognition.Status == MediaRecognitionStatus.Ambiguous || recognition.IsAmbiguous)
            return "Ambiguous";
        if (string.IsNullOrWhiteSpace(recognition.Title))
            return "MissingTitle";
        if (string.Equals(recognition.ConfidenceLevel, "Low", StringComparison.OrdinalIgnoreCase))
            return "LowConfidence";
        if (string.Equals(recognition.ConfidenceLevel, "Medium", StringComparison.OrdinalIgnoreCase))
            return "MediumConfidence";
        return string.Empty;
    }

    private static string BuildRecognitionDetails(
        MediaRecognitionSnapshot recognition)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Logical path: {recognition.LogicalPath}");
        builder.AppendLine($"Runtime version: {recognition.RuntimeVersion ?? "legacy/unknown"}");
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
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

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

    private sealed record CatalogListItemViewModel(
        CatalogMediaItemModel Item,
        string IconGlyph,
        string Title,
        string Secondary,
        string TypeLabel);

    private sealed class CatalogGroup : ObservableCollection<CatalogListItemViewModel>
    {
        public CatalogGroup(
            string key,
            IEnumerable<CatalogListItemViewModel> items)
            : base(items)
        {
            Key = key;
        }

        public string Key { get; }
    }

    private sealed class CatalogCategoryComparer : IEqualityComparer<MediaCategoryKind?>
    {
        public static CatalogCategoryComparer Default { get; } = new();

        public bool Equals(MediaCategoryKind? x, MediaCategoryKind? y) => x == y;

        public int GetHashCode(MediaCategoryKind? obj) =>
            obj.HasValue ? (int)obj.Value + 1 : 0;
    }
}
