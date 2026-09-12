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
    public event EventHandler<CatalogSubjectModel>? SubjectRequested;

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

        var aggregation = CatalogSubjectAggregator.Build(snapshot);
        var displayEntries = aggregation.Subjects
            .Select(static subject =>
                CatalogDisplayEntry.FromSubject(subject))
            .Concat(
                aggregation.StandaloneItems.Select(static item =>
                    CatalogDisplayEntry.FromItem(item)))
            .Where(entry => Matches(entry, query))
            .OrderBy(entry => CategoryOrder(entry.Category))
            .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        if (displayEntries.Length == 0)
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

        var groups = displayEntries
            .GroupBy(entry => entry.Category, CatalogCategoryComparer.Default)
            .Select(group =>
                new CatalogGroup(
                    CategoryLabel(group.Key),
                    group.Select(entry =>
                        entry.Subject is { } subject
                            ? CreateSubjectListItem(subject)
                            : CreateListItem(entry.Item!, sourceLabels))))
            .ToArray();

        ((CollectionViewSource)Resources["GroupedCatalogItems"]).Source = groups;
        EmptyStateText.Visibility = Visibility.Collapsed;
        ResultsList.Visibility = Visibility.Visible;
    }

    private CatalogListItemViewModel CreateSubjectListItem(
        CatalogSubjectModel subject)
    {
        var secondaryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(subject.NativeTitle) &&
            !string.Equals(
                subject.NativeTitle,
                subject.Title,
                StringComparison.CurrentCultureIgnoreCase))
        {
            secondaryParts.Add(subject.NativeTitle);
        }

        if (!string.IsNullOrWhiteSpace(subject.Meta))
        {
            secondaryParts.Add(subject.Meta);
        }

        var sources = subject.Items
            .Select(static item => item.Location?.SourceId)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (sources > 1)
        {
            secondaryParts.Add(
                L(
                    $"{sources} 个来源",
                    $"{sources} ソース",
                    $"{sources} sources"));
        }

        return new CatalogListItemViewModel(
            subject,
            null,
            subject.Category,
            subject.Category switch
            {
                MediaCategoryKind.Anime => "\uE8B2",
                MediaCategoryKind.Series => "\uE8FD",
                _ => "\uE8FD",
            },
            subject.Title,
            string.Join(" · ", secondaryParts),
            L(
                $"{subject.EpisodeCount} 集",
                $"{subject.EpisodeCount} 話",
                $"{subject.EpisodeCount} episodes"));
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

        if (item.Metadata is { IsResolved: true } metadata)
        {
            var metadataParts = new List<string>
            {
                $"Metadata:{metadata.Provider}"
            };

            if (DateOnly.TryParse(metadata.ReleaseDate, out var releaseDate))
                metadataParts.Add(releaseDate.Year.ToString(CultureInfo.InvariantCulture));

            secondaryParts.Add(string.Join(" ", metadataParts));
        }

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
            null,
            item,
            item.Category,
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
        if (e.ClickedItem is not CatalogListItemViewModel viewModel)
        {
            return;
        }

        if (viewModel.Subject is { } subject)
        {
            SubjectRequested?.Invoke(this, subject);
            return;
        }

        if (viewModel.Item is { } item)
        {
            MediaRequested?.Invoke(this, item);
        }
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

        var flyout = new MenuFlyout();

        if (viewModel.Item is { Recognition: { } } item)
        {
            var detailsItem = new MenuFlyoutItem
            {
                Text = "Recognition details",
                Tag = item
            };
            detailsItem.Click += RecognitionDetails_Click;
            flyout.Items.Add(detailsItem);
        }

        var metadataOwner = viewModel.Item?.Metadata is not null
            ? viewModel.Item
            : viewModel.Subject?.Items.FirstOrDefault(static item =>
                item.Metadata is not null);

        if (metadataOwner is not null)
        {
            var metadataItem = new MenuFlyoutItem
            {
                Text = "Metadata details",
                Tag = metadataOwner
            };
            metadataItem.Click += MetadataDetails_Click;
            flyout.Items.Add(metadataItem);
        }

        container.ContextFlyout =
            flyout.Items.Count > 0
                ? flyout
                : null;
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

    private async void MetadataDetails_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem
            {
                Tag: CatalogMediaItemModel
                {
                    Metadata: { } metadata
                }
            })
        {
            return;
        }

        var details = new TextBox
        {
            Text = BuildMetadataDetails(metadata),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono"),
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        await new ContentDialog
        {
            Title = "Metadata details",
            Content = details,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        }.ShowAsync();
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
                "CSV 已同时包含 Recognition 与 Metadata 诊断：除原始文件名、逻辑路径、识别证据外，还记录实际 Provider 搜索词、候选数量、前两名分数与分差、解析阈值、ResolutionReason、Top Candidates 及评分证据。可直接筛选 MetadataResolutionReason 定位未刮削原因。",
                "CSV には Recognition と Metadata の診断情報を統合しています。元ファイル名、論理パス、認識根拠、Metadata 状態、Provider、Subject ID、信頼度、外部 ID、Provider エラーを確認できます。",
                "The CSV combines Recognition and Metadata diagnostics, including source names, logical paths, recognition evidence, Metadata status, provider, subject ID, confidence, external IDs and provider errors."),
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

            if (NeedsReview(recognition, item.Metadata))
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
            "NeedsReview,ReviewPriority,ReviewReason,RuntimeVersion,Source,OriginalName,LogicalPath,Status,ConfidenceLevel,Confidence,IsAmbiguous,AppliedDisplayTitle,RecognizedTitle,EpisodeTitle,MediaKind,SpecialKind,EpisodePart,IsFinalEpisode,Season,Cour,Episode,EpisodeEnd,Special,Year,ErrorCode,TitleCandidates,Evidence,MetadataRuntimeVersion,MetadataRecognitionRuntimeVersion,MetadataRecognitionRuntimeMatch,MetadataStatus,MetadataNeedsReview,MetadataFailureStage,MetadataFailureReason,MetadataResolutionReason,MetadataSearchTitles,MetadataCandidateCount,MetadataAutoResolveThreshold,MetadataMinimumLead,MetadataBestScore,MetadataSecondScore,MetadataLead,MetadataTopCandidates,MetadataProvider,MetadataSubjectId,MetadataSubjectKind,MetadataConfidence,MetadataCanonicalTitle,MetadataOriginalTitle,MetadataLocalizedTitles,MetadataAliases,MetadataReleaseDate,MetadataEpisodeCount,MetadataEpisodeNumber,MetadataEpisodeTitle,MetadataEpisodeOriginalTitle,MetadataEpisodeAirDate,MetadataPosterUrl,MetadataBackdropUrl,MetadataExternalIds,MetadataErrors,MetadataUpdatedAtUtc");

        foreach (var item in items)
        {
            var recognition = item.Recognition;
            var source = item.Location is { } location &&
                         sourceLabels.TryGetValue(location.SourceId, out var sourceLabel)
                ? sourceLabel
                : string.Empty;

            var metadata = item.Metadata;
            var reviewPriority = ReviewPriority(recognition, metadata);
            var reviewReason = ReviewReason(recognition, metadata);
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

            var metadataLocalizedTitles = metadata is null
                ? string.Empty
                : string.Join(
                    " || ",
                    metadata.LocalizedTitles
                        .OrderBy(static pair => pair.Key)
                        .Select(static pair => $"{pair.Key}={pair.Value}"));
            var metadataAliases = metadata is null
                ? string.Empty
                : string.Join(" || ", metadata.Aliases);
            var metadataExternalIds = metadata is null
                ? string.Empty
                : string.Join(
                    " || ",
                    metadata.ExternalIds
                        .OrderBy(static pair => pair.Key)
                        .Select(static pair => $"{pair.Key}={pair.Value}"));
            var metadataErrors = metadata is null
                ? string.Empty
                : string.Join(
                    " || ",
                    metadata.Errors.Select(static error =>
                        $"{error.Provider}|{error.ErrorType}|{error.Message}"));
            var metadataStatus = metadata?.Status.ToString()
                ?? MetadataDiagnosticState(recognition);
            var metadataSearchTitles = metadata is null
                ? string.Empty
                : string.Join(" || ", metadata.SearchTitles);
            var metadataTopCandidates = metadata is null
                ? string.Empty
                : string.Join(
                    " || ",
                    metadata.TopCandidates.Select(static candidate =>
                        $"{candidate.Provider}:{candidate.ProviderSubjectId}|{candidate.SubjectKind}|{candidate.Title}|year={candidate.Year?.ToString(CultureInfo.InvariantCulture) ?? "-"}|rank={candidate.ProviderRank}|score={candidate.Score:0.000}|evidence={string.Join(";", candidate.Evidence)}"));

            AppendCsvRow(
                builder,
                needsReview ? "true" : "false",
                reviewPriority,
                reviewReason,
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
                evidence,
                metadata?.RuntimeVersion ?? string.Empty,
                metadata?.RecognitionRuntimeVersion ?? string.Empty,
                metadata is null || recognition is null
                    ? string.Empty
                    : metadata.MatchesRecognitionRuntime(recognition.RuntimeVersion).ToString(),
                metadataStatus,
                metadata?.NeedsReview.ToString() ?? string.Empty,
                metadata?.FailureStage ?? string.Empty,
                metadata?.FailureReason ?? string.Empty,
                metadata?.ResolutionReason ?? string.Empty,
                metadataSearchTitles,
                metadata?.CandidateCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.AutoResolveThreshold.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.MinimumLead.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.BestScore?.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.SecondScore?.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.Lead?.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadataTopCandidates,
                metadata?.Provider ?? string.Empty,
                metadata?.ProviderSubjectId ?? string.Empty,
                metadata?.SubjectKind ?? string.Empty,
                metadata?.Confidence.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
                metadata?.CanonicalTitle ?? string.Empty,
                metadata?.OriginalTitle ?? string.Empty,
                metadataLocalizedTitles,
                metadataAliases,
                metadata?.ReleaseDate ?? string.Empty,
                metadata?.EpisodeCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                metadata is null ? string.Empty : FormatNullableNumber(metadata.EpisodeNumber),
                metadata?.EpisodeTitle ?? string.Empty,
                metadata?.EpisodeOriginalTitle ?? string.Empty,
                metadata?.EpisodeAirDate ?? string.Empty,
                metadata?.PosterUrl ?? string.Empty,
                metadata?.BackdropUrl ?? string.Empty,
                metadataExternalIds,
                metadataErrors,
                metadata?.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return builder.ToString();
    }

    private static string MetadataDiagnosticState(
        MediaRecognitionSnapshot? recognition)
    {
        if (recognition is null)
            return "NotAttempted:MissingRecognition";

        if (recognition.Status != MediaRecognitionStatus.Recognized ||
            recognition.IsAmbiguous)
        {
            return $"NotAttempted:{recognition.Status}";
        }

        if (recognition.ConfidenceLevel is not ("Medium" or "High"))
            return $"NotAttempted:{recognition.ConfidenceLevel}Confidence";

        if (string.IsNullOrWhiteSpace(recognition.Title))
            return "NotAttempted:MissingTitle";

        return "MissingAfterScan";
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

    private static bool NeedsReview(
        MediaRecognitionSnapshot? recognition,
        Eizo.MetadataIntegration.MediaMetadataSnapshot? metadata) =>
        !string.IsNullOrEmpty(ReviewPriority(recognition, metadata));

    private static string ReviewPriority(
        MediaRecognitionSnapshot? recognition,
        Eizo.MetadataIntegration.MediaMetadataSnapshot? metadata)
    {
        var recognitionPriority = RecognitionReviewPriority(recognition);
        var metadataPriority = MetadataReviewPriority(recognition, metadata);

        return PriorityRank(metadataPriority) > PriorityRank(recognitionPriority)
            ? metadataPriority
            : recognitionPriority;
    }

    private static string RecognitionReviewPriority(
        MediaRecognitionSnapshot? recognition)
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

    private static string MetadataReviewPriority(
        MediaRecognitionSnapshot? recognition,
        Eizo.MetadataIntegration.MediaMetadataSnapshot? metadata)
    {
        if (metadata is null)
        {
            if (recognition is not null &&
                recognition.Status == MediaRecognitionStatus.Recognized &&
                !recognition.IsAmbiguous &&
                !string.IsNullOrWhiteSpace(recognition.Title) &&
                recognition.ConfidenceLevel is "Medium" or "High")
            {
                return "High";
            }

            return string.Empty;
        }

        if (metadata.Status is Eizo.MetadataIntegration.MediaMetadataStatus.Error or
            Eizo.MetadataIntegration.MediaMetadataStatus.Unresolved)
        {
            return "High";
        }

        return metadata.NeedsReview
            ? "Medium"
            : string.Empty;
    }

    private static int PriorityRank(string value) =>
        value switch
        {
            "High" => 3,
            "Medium" => 2,
            "Low" => 1,
            _ => 0,
        };

    private static string ReviewReason(
        MediaRecognitionSnapshot? recognition,
        Eizo.MetadataIntegration.MediaMetadataSnapshot? metadata)
    {
        var recognitionReason = RecognitionReviewReason(recognition);
        var metadataReason = MetadataReviewReason(recognition, metadata);

        if (string.IsNullOrWhiteSpace(recognitionReason))
            return metadataReason;

        if (string.IsNullOrWhiteSpace(metadataReason))
            return recognitionReason;

        return $"Recognition:{recognitionReason} || Metadata:{metadataReason}";
    }

    private static string RecognitionReviewReason(
        MediaRecognitionSnapshot? recognition)
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

    private static string MetadataReviewReason(
        MediaRecognitionSnapshot? recognition,
        Eizo.MetadataIntegration.MediaMetadataSnapshot? metadata)
    {
        if (metadata is null)
        {
            return recognition is not null &&
                   recognition.Status == MediaRecognitionStatus.Recognized &&
                   !recognition.IsAmbiguous &&
                   !string.IsNullOrWhiteSpace(recognition.Title) &&
                   recognition.ConfidenceLevel is "Medium" or "High"
                ? "MissingAfterScan"
                : string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(metadata.FailureReason))
            return metadata.FailureReason;

        if (!string.IsNullOrWhiteSpace(metadata.ResolutionReason) &&
            !string.Equals(metadata.ResolutionReason, "Resolved", StringComparison.OrdinalIgnoreCase))
        {
            return metadata.ResolutionReason;
        }

        if (metadata.Status == Eizo.MetadataIntegration.MediaMetadataStatus.Error)
            return "Error";
        if (metadata.Status == Eizo.MetadataIntegration.MediaMetadataStatus.Unresolved)
            return "Unresolved";

        return string.Empty;
    }

    private static string BuildMetadataDetails(
        Eizo.MetadataIntegration.MediaMetadataSnapshot metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Runtime version: {metadata.RuntimeVersion}");
        builder.AppendLine($"Recognition runtime: {metadata.RecognitionRuntimeVersion ?? "-"}");
        builder.AppendLine($"Provider: {metadata.Provider ?? "-"}");
        builder.AppendLine($"Subject ID: {metadata.ProviderSubjectId ?? "-"}");
        builder.AppendLine($"Subject kind: {metadata.SubjectKind ?? "-"}");
        builder.AppendLine($"Confidence: {metadata.Confidence:0.000}");
        builder.AppendLine($"Resolution reason: {metadata.ResolutionReason ?? "-"}");
        builder.AppendLine($"Failure stage: {metadata.FailureStage ?? "-"}");
        builder.AppendLine($"Failure reason: {metadata.FailureReason ?? "-"}");
        builder.AppendLine($"Needs review: {metadata.NeedsReview}");
        builder.AppendLine($"Candidates: {metadata.CandidateCount}");
        builder.AppendLine($"Threshold: {metadata.AutoResolveThreshold:0.000}");
        builder.AppendLine($"Minimum lead: {metadata.MinimumLead:0.000}");
        builder.AppendLine($"Best score: {metadata.BestScore?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Second score: {metadata.SecondScore?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Lead: {metadata.Lead?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Canonical title: {metadata.CanonicalTitle ?? "-"}");
        builder.AppendLine($"Original title: {metadata.OriginalTitle ?? "-"}");
        builder.AppendLine($"Release date: {metadata.ReleaseDate ?? "-"}");
        builder.AppendLine($"Episode count: {metadata.EpisodeCount?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        builder.AppendLine($"Episode: {FormatNullableNumber(metadata.EpisodeNumber)}");
        builder.AppendLine($"Episode title: {metadata.EpisodeTitle ?? "-"}");
        builder.AppendLine($"Episode original title: {metadata.EpisodeOriginalTitle ?? "-"}");
        builder.AppendLine($"Episode air date: {metadata.EpisodeAirDate ?? "-"}");
        builder.AppendLine($"Poster: {metadata.PosterUrl ?? "-"}");
        builder.AppendLine($"Backdrop: {metadata.BackdropUrl ?? "-"}");
        builder.AppendLine($"Updated: {metadata.UpdatedAtUtc:O}");

        if (metadata.SearchTitles.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Provider search titles:");
            foreach (var title in metadata.SearchTitles)
                builder.AppendLine($"  - {title}");
        }

        if (metadata.TopCandidates.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Top metadata candidates:");
            foreach (var candidate in metadata.TopCandidates)
            {
                builder.AppendLine(
                    $"  - {candidate.Provider}:{candidate.ProviderSubjectId} | {candidate.Title} | score={candidate.Score:0.000} | year={candidate.Year?.ToString(CultureInfo.InvariantCulture) ?? "-"} | rank={candidate.ProviderRank}");
                foreach (var evidence in candidate.Evidence)
                    builder.AppendLine($"      {evidence}");
            }
        }

        if (metadata.ExternalIds.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("External IDs:");
            foreach (var pair in metadata.ExternalIds.OrderBy(static item => item.Key))
                builder.AppendLine($"  - {pair.Key}: {pair.Value}");
        }

        if (metadata.Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Provider warnings:");
            foreach (var error in metadata.Errors)
                builder.AppendLine($"  - {error.Provider} | {error.ErrorType} | {error.Message}");
        }

        return builder.ToString().TrimEnd();
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

    private static bool Matches(
        CatalogDisplayEntry entry,
        string query)
    {
        if (entry.Subject is { } subject)
        {
            return Matches(subject, query);
        }

        return entry.Item is { } item && Matches(item, query);
    }

    private static bool Matches(
        CatalogSubjectModel subject,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (subject.Title.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase) ||
            subject.NativeTitle.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase) ||
            subject.Meta.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        if (subject.Metadata is { } metadata &&
            (metadata.LocalizedTitles.Values.Any(title =>
                 title.Contains(
                     query,
                     StringComparison.CurrentCultureIgnoreCase)) ||
             metadata.Aliases.Any(title =>
                 title.Contains(
                     query,
                     StringComparison.CurrentCultureIgnoreCase))))
        {
            return true;
        }

        return subject.Episodes.Any(episode =>
            episode.Title.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase) ||
            episode.NativeTitle.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool Matches(CatalogMediaItemModel item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return item.DisplayTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               item.SourceTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(item.NativeTitle) &&
                item.NativeTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
               item.Meta.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               (item.Metadata is { } metadata &&
                ((!string.IsNullOrWhiteSpace(metadata.CanonicalTitle) &&
                  metadata.CanonicalTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(metadata.OriginalTitle) &&
                  metadata.OriginalTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 metadata.LocalizedTitles.Values.Any(title =>
                    title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 metadata.Aliases.Any(title =>
                    title.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(metadata.EpisodeTitle) &&
                  metadata.EpisodeTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)))) ||
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

    private sealed record CatalogDisplayEntry(
        CatalogSubjectModel? Subject,
        CatalogMediaItemModel? Item,
        MediaCategoryKind? Category,
        string Title)
    {
        public static CatalogDisplayEntry FromSubject(
            CatalogSubjectModel subject) =>
            new(
                subject,
                null,
                subject.Category,
                subject.Title);

        public static CatalogDisplayEntry FromItem(
            CatalogMediaItemModel item) =>
            new(
                null,
                item,
                item.Category,
                item.DisplayTitle);
    }

    private sealed record CatalogListItemViewModel(
        CatalogSubjectModel? Subject,
        CatalogMediaItemModel? Item,
        MediaCategoryKind? Category,
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
