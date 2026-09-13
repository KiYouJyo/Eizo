using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace Eizo.Views;

public sealed partial class BangumiSubjectDetailView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiRepository _repository =
        BangumiRepository.Default;
    private readonly int _subjectId;
    private BangumiSubjectCard _subject;
    private CancellationTokenSource? _loadCancellation;

    public BangumiSubjectDetailView(
        BangumiSubjectCard subject)
    {
        _subject = subject;
        _subjectId = subject.Id;

        InitializeComponent();
        ApplyText();
        ApplyCard(subject);

        Loaded += BangumiSubjectDetailView_Loaded;
        Unloaded += BangumiSubjectDetailView_Unloaded;
    }

    private string T(string key) =>
        _localization.GetString(key);

    private string L(
        string zhCn,
        string jaJp,
        string enUs) =>
        _localization.CurrentLanguage switch
        {
            "ja-JP" => jaJp,
            "en-US" => enUs,
            _ => zhCn,
        };

    private void ApplyText()
    {
        PageTitle.Text = T("Bangumi_DetailPageTitle");
        RefreshButton.Content = T("Bangumi_Refresh");
        OpenBangumiButton.Content =
            T("Bangumi_OpenOnBangumi");
        SummaryTitle.Text = T("Bangumi_Summary");
        TagsTitle.Text = T("Bangumi_Tags");
    }

    private async void BangumiSubjectDetailView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(forceRefresh: false);
    }

    private void BangumiSubjectDetailView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(forceRefresh: true);
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        RefreshButton.IsEnabled = false;
        StatusText.Text = T("Bangumi_Loading");

        try
        {
            var result =
                await _repository.GetSubjectAsync(
                    _subjectId,
                    forceRefresh,
                    cancellationToken);

            _subject = result.Value.Card;
            ApplyDetail(result.Value);

            var localTime =
                result.FetchedAtUtc.ToLocalTime().ToString(
                    "g",
                    CultureInfo.CurrentCulture);
            StatusText.Text = result.IsStale
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_StaleCacheFormat"),
                    localTime)
                : result.IsFromCache
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CacheFormat"),
                        localTime)
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_UpdatedFormat"),
                        localTime);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            StatusText.Text =
                T("Bangumi_NetworkError");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
        }
    }

    private void ApplyDetail(
        BangumiSubjectDetail detail)
    {
        ApplyCard(detail.Card);

        var tags = detail.MetaTags
            .Concat(detail.Tags)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(
                StringComparer.CurrentCultureIgnoreCase)
            .Take(16)
            .ToArray();

        TagsText.Text = tags.Length > 0
            ? string.Join(" · ", tags)
            : T("Bangumi_NoTags");
    }

    private void ApplyCard(
        BangumiSubjectCard subject)
    {
        var preferNative =
            _localization.CurrentLanguage is "ja-JP" or "en-US";

        var title = preferNative
            ? FirstNonEmpty(
                subject.NativeTitle,
                subject.ChineseTitle)
            : FirstNonEmpty(
                subject.ChineseTitle,
                subject.NativeTitle);

        var nativeTitle = preferNative
            ? subject.ChineseTitle
            : subject.NativeTitle;
        if (string.Equals(
                title,
                nativeTitle,
                StringComparison.CurrentCultureIgnoreCase))
        {
            nativeTitle = string.Empty;
        }

        TitleText.Text = title;
        NativeTitleText.Text = nativeTitle;
        SummaryText.Text =
            string.IsNullOrWhiteSpace(subject.Summary)
                ? T("Bangumi_NoSummary")
                : subject.Summary;

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.AirDate))
            meta.Add(subject.AirDate!);
        if (!string.IsNullOrWhiteSpace(subject.Platform))
            meta.Add(subject.Platform!);
        if (subject.EpisodeCount > 0)
        {
            meta.Add(
                L(
                    $"{subject.EpisodeCount} 集",
                    $"{subject.EpisodeCount} 話",
                    $"{subject.EpisodeCount} eps"));
        }
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");
        if (subject.CollectionTotal > 0)
        {
            meta.Add(
                L(
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} collections"));
        }

        MetaText.Text = string.Join(" · ", meta);
        PosterImage.Source =
            CreateArtwork(subject.PosterUrl);
    }

    private async void OpenBangumiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(
            new Uri(
                $"https://bgm.tv/subject/{_subjectId}",
                UriKind.Absolute));
    }

    private static BitmapImage? CreateArtwork(
        string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return null;
        }

        try
        {
            return new BitmapImage
            {
                UriSource = uri,
                DecodePixelWidth = 520,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            static value =>
                !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
