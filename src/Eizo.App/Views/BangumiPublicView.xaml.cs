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

internal enum BangumiDiscoverScope
{
    AllTime = 0,
    CurrentSeason = 1,
    CurrentYear = 2,
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
    private bool _controlsReady;
    private int _selectedSeasonYear;
    private int _selectedSeasonStartMonth;
    private int _discoverNextOffset;

    internal BangumiPublicView(BangumiPublicPageKind kind)
    {
        _kind = kind;

        var now = DateTimeOffset.Now;
        _selectedSeasonYear = now.Year;
        _selectedSeasonStartMonth =
            BangumiRepository.GetSeasonStartMonth(now.Month);

        InitializeComponent();

        ResultsList.ItemsSource = _items;
        ApplyText();
        _controlsReady = true;

        Loaded += BangumiPublicView_Loaded;
        Unloaded += BangumiPublicView_Unloaded;
    }

    public event EventHandler<BangumiSubjectCard>? SubjectRequested;

    private BangumiDiscoverScope DiscoverScope =>
        DiscoverScopeCombo.SelectedIndex switch
        {
            1 => BangumiDiscoverScope.CurrentSeason,
            2 => BangumiDiscoverScope.CurrentYear,
            _ => BangumiDiscoverScope.AllTime,
        };

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
        CurrentSeasonButton.Content =
            T("Bangumi_CurrentSeason");
        LoadMoreButton.Content =
            T("Bangumi_LoadMore");

        DiscoverScopeCombo.ItemsSource = new[]
        {
            T("Bangumi_RankingAllTime"),
            T("Bangumi_RankingCurrentSeason"),
            T("Bangumi_RankingCurrentYear"),
        };
        DiscoverScopeCombo.SelectedIndex = 0;

        DayList.Visibility =
            _kind == BangumiPublicPageKind.Calendar
                ? Visibility.Visible
                : Visibility.Collapsed;
        SeasonControls.Visibility =
            _kind == BangumiPublicPageKind.Seasonal
                ? Visibility.Visible
                : Visibility.Collapsed;
        DiscoverControls.Visibility =
            _kind == BangumiPublicPageKind.Discover
                ? Visibility.Visible
                : Visibility.Collapsed;

        UpdateSeasonHeader();
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

    private async void PreviousSeasonButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShiftSeason(-1);
        await LoadAsync(forceRefresh: false);
    }

    private async void NextSeasonButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShiftSeason(1);
        await LoadAsync(forceRefresh: false);
    }

    private async void CurrentSeasonButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        _selectedSeasonYear = now.Year;
        _selectedSeasonStartMonth =
            BangumiRepository.GetSeasonStartMonth(now.Month);
        UpdateSeasonHeader();
        await LoadAsync(forceRefresh: false);
    }

    private async void DiscoverScopeCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_controlsReady)
            return;

        _discoverNextOffset = 0;
        await LoadAsync(forceRefresh: false);
    }

    private async void LoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_kind != BangumiPublicPageKind.Discover ||
            DiscoverScope == BangumiDiscoverScope.CurrentSeason)
        {
            return;
        }

        await LoadDiscoverAsync(
            forceRefresh: false,
            append: true);
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        SetBusy(true);
        StatusText.Text = T("Bangumi_Loading");
        LoadMoreButton.Visibility = Visibility.Collapsed;

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
                        await _repository.GetSeasonAsync(
                            _selectedSeasonYear,
                            _selectedSeasonStartMonth,
                            forceRefresh,
                            cancellationToken);
                    SetItems(result.Value.Items);
                    UpdateSeasonHeader();
                    ApplyLoadStatus(
                        result.IsFromCache,
                        result.IsStale,
                        result.FetchedAtUtc);
                    break;
                }

                default:
                    await LoadDiscoverCoreAsync(
                        forceRefresh,
                        append: false,
                        cancellationToken);
                    break;
            }

            if (_items.Count == 0)
                StatusText.Text = T("Bangumi_NoResults");
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _items.Clear();
            StatusText.Text = T("Bangumi_NetworkError");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadDiscoverAsync(
        bool forceRefresh,
        bool append)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        SetBusy(true);
        LoadMoreButton.Visibility = Visibility.Collapsed;
        StatusText.Text = append
            ? T("Bangumi_LoadingMore")
            : T("Bangumi_Loading");

        try
        {
            await LoadDiscoverCoreAsync(
                forceRefresh,
                append,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!append)
                _items.Clear();

            StatusText.Text = append
                ? T("Bangumi_LoadMoreError")
                : T("Bangumi_NetworkError");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadDiscoverCoreAsync(
        bool forceRefresh,
        bool append,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var offset = append
            ? _discoverNextOffset
            : 0;

        BangumiLoadResult<BangumiSubjectPage> result;

        switch (DiscoverScope)
        {
            case BangumiDiscoverScope.CurrentSeason:
                result =
                    await _repository.GetRankedAnimeForSeasonAsync(
                        now.Year,
                        BangumiRepository.GetSeasonStartMonth(now.Month),
                        forceRefresh,
                        cancellationToken);
                break;

            case BangumiDiscoverScope.CurrentYear:
                result =
                    await _repository.GetRankedAnimeForYearAsync(
                        now.Year,
                        offset,
                        forceRefresh,
                        cancellationToken);
                break;

            default:
                result =
                    await _repository.GetRankedAnimeAsync(
                        offset,
                        forceRefresh,
                        cancellationToken);
                break;
        }

        if (append)
            AppendItems(result.Value.Items);
        else
            SetItems(result.Value.Items);

        _discoverNextOffset =
            result.Value.Offset +
            result.Value.Items.Count;

        LoadMoreButton.Visibility =
            DiscoverScope != BangumiDiscoverScope.CurrentSeason &&
            result.Value.HasMore
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyLoadStatus(
            result.IsFromCache,
            result.IsStale,
            result.FetchedAtUtc);
    }

    private void ShiftSeason(int delta)
    {
        var shifted =
            BangumiRepository.ShiftSeason(
                _selectedSeasonYear,
                _selectedSeasonStartMonth,
                delta);

        _selectedSeasonYear = shifted.Year;
        _selectedSeasonStartMonth = shifted.StartMonth;
        UpdateSeasonHeader();
    }

    private void UpdateSeasonHeader()
    {
        if (_kind != BangumiPublicPageKind.Seasonal)
            return;

        SelectedSeasonText.Text = string.Format(
            CultureInfo.CurrentCulture,
            T("Bangumi_SeasonLabelFormat"),
            _selectedSeasonYear,
            SeasonName(_selectedSeasonStartMonth));

        PageSubtitle.Text = string.Format(
            CultureInfo.CurrentCulture,
            T("Bangumi_SeasonalSubtitleFormat"),
            _selectedSeasonYear,
            SeasonName(_selectedSeasonStartMonth));
    }

    private void SetBusy(bool busy)
    {
        LoadingRing.IsActive = busy;
        LoadingRing.Visibility =
            busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;
        PreviousSeasonButton.IsEnabled = !busy;
        NextSeasonButton.IsEnabled = !busy;
        CurrentSeasonButton.IsEnabled = !busy;
        DiscoverScopeCombo.IsEnabled = !busy;
        LoadMoreButton.IsEnabled = !busy;
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
        AppendItems(subjects);
    }

    private void AppendItems(
        IReadOnlyList<BangumiSubjectCard> subjects)
    {
        var existingIds = _items
            .Select(static item => item.Subject.Id)
            .ToHashSet();

        foreach (var subject in subjects)
        {
            if (existingIds.Add(subject.Id))
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
