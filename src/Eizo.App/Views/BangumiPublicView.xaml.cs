using System.Collections.ObjectModel;
using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Views;

internal enum BangumiPublicPageKind
{
    Calendar,
    Seasonal,
    Discover,
}

public sealed partial class BangumiPublicView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiRepository _repository =
        BangumiRepository.Default;
    private readonly BangumiPublicPageKind _kind;
    private readonly ObservableCollection<BangumiCardViewModel> _items = [];
    private BangumiCalendarSnapshot? _calendar;
    private CancellationTokenSource? _loadCancellation;
    private bool _daySelectionSynchronizing;

    internal BangumiPublicView(BangumiPublicPageKind kind)
    {
        _kind = kind;
        InitializeComponent();

        ResultsList.ItemsSource = _items;
        ApplyText();

        Loaded += BangumiPublicView_Loaded;
        Unloaded += BangumiPublicView_Unloaded;
    }

    public event EventHandler<BangumiSubjectCard>? SubjectRequested;

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
        PageTitle.Text = _kind switch
        {
            BangumiPublicPageKind.Calendar =>
                T("Nav_BroadcastCalendar"),
            BangumiPublicPageKind.Seasonal =>
                T("Nav_SeasonalAnime"),
            _ =>
                T("Nav_RankDiscover"),
        };

        PageSubtitle.Text = _kind switch
        {
            BangumiPublicPageKind.Calendar =>
                T("Bangumi_CalendarSubtitle"),
            BangumiPublicPageKind.Seasonal =>
                T("Bangumi_SeasonalSubtitle"),
            _ =>
                T("Bangumi_DiscoverSubtitle"),
        };

        RefreshButtonText.Text = T("Bangumi_Refresh");
        DayList.Visibility =
            _kind == BangumiPublicPageKind.Calendar
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private async void BangumiPublicView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(forceRefresh: false);
    }

    private void BangumiPublicView_Unloaded(
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
            switch (_kind)
            {
                case BangumiPublicPageKind.Calendar:
                {
                    var result =
                        await _repository.GetCalendarAsync(
                            forceRefresh,
                            cancellationToken);
                    _calendar = result.Value;
                    RebuildCalendarDays();
                    ApplyCalendarDay();
                    ApplyLoadStatus(
                        result.IsFromCache,
                        result.IsStale,
                        result.FetchedAtUtc);
                    break;
                }

                case BangumiPublicPageKind.Seasonal:
                {
                    var result =
                        await _repository.GetCurrentSeasonAsync(
                            DateTimeOffset.Now,
                            forceRefresh,
                            cancellationToken);
                    SetItems(result.Value.Items);
                    PageSubtitle.Text = string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_SeasonalSubtitleFormat"),
                        result.Value.Year,
                        SeasonName(result.Value.StartMonth));
                    ApplyLoadStatus(
                        result.IsFromCache,
                        result.IsStale,
                        result.FetchedAtUtc);
                    break;
                }

                default:
                {
                    var result =
                        await _repository.GetRankedAnimeAsync(
                            forceRefresh,
                            cancellationToken);
                    SetItems(result.Value);
                    ApplyLoadStatus(
                        result.IsFromCache,
                        result.IsStale,
                        result.FetchedAtUtc);
                    break;
                }
            }

            if (_items.Count == 0)
            {
                StatusText.Text =
                    T("Bangumi_NoResults");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _items.Clear();
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

    private void RebuildCalendarDays()
    {
        if (_calendar is null)
            return;

        var options = _calendar.Days
            .Select(day =>
                new BangumiDayOption(
                    day.WeekdayId,
                    ResolveWeekdayLabel(day)))
            .ToArray();

        _daySelectionSynchronizing = true;
        try
        {
            DayList.ItemsSource = options;

            var today = ToBangumiWeekdayId(
                DateTimeOffset.Now.DayOfWeek);
            var index = Array.FindIndex(
                options,
                option => option.WeekdayId == today);
            DayList.SelectedIndex =
                index >= 0 ? index : 0;
        }
        finally
        {
            _daySelectionSynchronizing = false;
        }
    }

    private void DayList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_daySelectionSynchronizing)
            return;

        ApplyCalendarDay();
    }

    private void ApplyCalendarDay()
    {
        if (_calendar is null ||
            DayList.SelectedItem is not BangumiDayOption option)
        {
            _items.Clear();
            return;
        }

        var day = _calendar.Days.FirstOrDefault(
            value => value.WeekdayId == option.WeekdayId);
        SetItems(day?.Items ?? []);
    }

    private void SetItems(
        IReadOnlyList<BangumiSubjectCard> subjects)
    {
        _items.Clear();
        foreach (var subject in subjects)
        {
            _items.Add(CreateViewModel(subject));
        }
    }

    private BangumiCardViewModel CreateViewModel(
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

        var subtitle = preferNative
            ? subject.ChineseTitle
            : subject.NativeTitle;
        if (string.Equals(
                title,
                subtitle,
                StringComparison.CurrentCultureIgnoreCase))
        {
            subtitle = string.Empty;
        }

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.AirDate))
            meta.Add(subject.AirDate!);
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");

        var stats = new List<string>();
        if (subject.EpisodeCount > 0)
        {
            stats.Add(
                L(
                    $"{subject.EpisodeCount} 集",
                    $"{subject.EpisodeCount} 話",
                    $"{subject.EpisodeCount} eps"));
        }

        if (subject.CollectionTotal > 0)
        {
            stats.Add(
                L(
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} collections"));
        }

        return new BangumiCardViewModel(
            subject,
            CreateArtwork(subject.PosterUrl),
            title,
            subtitle,
            string.Join(" · ", meta),
            string.Join(" · ", stats),
            "Bangumi");
    }

    private static BitmapImage? CreateArtwork(
        string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri) ||
            (!string.Equals(
                 uri.Scheme,
                 Uri.UriSchemeHttp,
                 StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(
                 uri.Scheme,
                 Uri.UriSchemeHttps,
                 StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        try
        {
            return new BitmapImage
            {
                UriSource = uri,
                DecodePixelWidth = 360,
            };
        }
        catch
        {
            return null;
        }
    }

    private void ResultsList_ItemClick(
        object sender,
        ItemClickEventArgs e)
    {
        if (e.ClickedItem is BangumiCardViewModel item)
        {
            SubjectRequested?.Invoke(
                this,
                item.Subject);
        }
    }

    private void ApplyLoadStatus(
        bool isFromCache,
        bool isStale,
        DateTimeOffset fetchedAtUtc)
    {
        var localTime =
            fetchedAtUtc.ToLocalTime().ToString(
                "g",
                CultureInfo.CurrentCulture);

        StatusText.Text = isStale
            ? string.Format(
                CultureInfo.CurrentCulture,
                T("Bangumi_StaleCacheFormat"),
                localTime)
            : isFromCache
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CacheFormat"),
                    localTime)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_UpdatedFormat"),
                    localTime);
    }

    private string ResolveWeekdayLabel(
        BangumiCalendarDayModel day) =>
        _localization.CurrentLanguage switch
        {
            "ja-JP" =>
                FirstNonEmpty(
                    day.JapaneseName,
                    day.EnglishName),
            "en-US" =>
                FirstNonEmpty(
                    day.EnglishName,
                    day.ChineseName),
            _ =>
                FirstNonEmpty(
                    day.ChineseName,
                    day.EnglishName),
        };

    private string SeasonName(int startMonth) =>
        startMonth switch
        {
            1 => L("冬", "冬", "Winter"),
            4 => L("春", "春", "Spring"),
            7 => L("夏", "夏", "Summer"),
            _ => L("秋", "秋", "Fall"),
        };

    private static int ToBangumiWeekdayId(
        DayOfWeek dayOfWeek) =>
        dayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)dayOfWeek;

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            static value =>
                !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
