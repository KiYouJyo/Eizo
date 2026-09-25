using System.Collections.ObjectModel;
using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Views;

public sealed partial class BangumiAnimeIndexView : UserControl
{
    private const int PageSize = 50;

    private readonly AppLocalizationService _localization = AppLocalizationService.Default;
    private readonly BangumiRepository _repository = BangumiRepository.Default;
    private readonly ObservableCollection<BangumiCardViewModel> _items = [];

    private CancellationTokenSource? _loadCancellation;
    private bool _controlsReady;
    private bool _synchronizingFilters;
    private bool _isLoading;
    private bool _hasMore = true;
    private int _nextOffset;
    private int _total;

    private sealed record FilterOption(string Value, string ZhCn, string JaJp, string EnUs);
    private sealed record DisplayOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record YearOption(int? Year, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly FilterOption[] FormatOptions =
    [
        new("", "全部", "すべて", "All"),
        new("TV", "TV", "TV", "TV"),
        new("WEB", "WEB", "WEB", "WEB"),
        new("OVA", "OVA", "OVA", "OVA"),
        new("剧场版", "剧场版", "劇場版", "Theatrical"),
    ];
    private static readonly FilterOption[] SourceOptions =
    [
        new("", "全部", "すべて", "All"),
        new("原创", "原创", "オリジナル", "Original"),
        new("漫画改", "漫画改", "漫画原作", "Manga"),
        new("游戏改", "游戏改", "ゲーム原作", "Game"),
        new("小说改", "小说改", "小説原作", "Novel"),
        new("动画改", "动画改", "アニメ原作", "Anime"),
        new("影视改", "影视改", "映像原作", "Film / TV"),
    ];
    private static readonly FilterOption[] GenreOptions =
    [
        new("", "全部", "すべて", "All"),
        new("科幻", "科幻", "SF", "Sci-Fi"),
        new("喜剧", "喜剧", "コメディ", "Comedy"),
        new("校园", "校园", "学園", "School"),
        new("惊悚", "惊悚", "スリラー", "Thriller"),
        new("后宫", "后宫", "ハーレム", "Harem"),
        new("机战", "机战", "ロボット", "Mecha"),
        new("悬疑", "悬疑", "ミステリー", "Mystery"),
        new("恋爱", "恋爱", "恋愛", "Romance"),
        new("奇幻", "奇幻", "ファンタジー", "Fantasy"),
        new("推理", "推理", "推理", "Detective"),
        new("运动", "运动", "スポーツ", "Sports"),
        new("音乐", "音乐", "音楽", "Music"),
        new("战斗", "战斗", "バトル", "Battle"),
        new("冒险", "冒险", "冒険", "Adventure"),
        new("萌系", "萌系", "萌え", "Moe"),
        new("穿越", "穿越", "異世界", "Isekai"),
        new("恐怖", "恐怖", "ホラー", "Horror"),
        new("历史", "历史", "歴史", "Historical"),
        new("日常", "日常", "日常", "Slice of life"),
        new("剧情", "剧情", "ドラマ", "Drama"),
        new("美食", "美食", "グルメ", "Food"),
        new("职场", "职场", "仕事", "Workplace"),
    ];
    private static readonly FilterOption[] RegionOptions =
    [
        new("", "全部", "すべて", "All"),
        new("日本", "日本", "日本", "Japan"),
        new("欧美", "欧美", "欧米", "Europe / US"),
        new("中国", "中国", "中国", "China"),
        new("美国", "美国", "アメリカ", "United States"),
        new("韩国", "韩国", "韓国", "South Korea"),
        new("法国", "法国", "フランス", "France"),
        new("英国", "英国", "イギリス", "United Kingdom"),
        new("中国香港", "中国香港", "香港", "Hong Kong"),
        new("俄罗斯", "俄罗斯", "ロシア", "Russia"),
        new("中国台湾", "中国台湾", "台湾", "Taiwan"),
    ];
    private static readonly FilterOption[] AudienceOptions =
    [
        new("", "全部", "すべて", "All"),
        new("BL", "BL", "BL", "BL"),
        new("GL", "GL", "GL", "GL"),
        new("子供向", "子供向", "子供向け", "Children"),
        new("女性向", "女性向", "女性向け", "Female-oriented"),
        new("少女向", "少女向", "少女向け", "Shoujo"),
        new("少年向", "少年向", "少年向け", "Shounen"),
        new("青年向", "青年向", "青年向け", "Seinen"),
        new("无cp", "无 CP", "CPなし", "No pairing"),
    ];
    private static readonly FilterOption[] SortOptions =
    [
        new("rank", "排名", "ランキング", "Rank"),
        new("heat", "热度", "人気", "Popularity"),
        new("score", "评分", "評価", "Score"),
        new("match", "匹配度", "一致度", "Relevance"),
    ];

    public BangumiAnimeIndexView()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _items;
        ApplyText();
        InitializeFilters();
        _controlsReady = true;
        Loaded += BangumiAnimeIndexView_Loaded;
        Unloaded += BangumiAnimeIndexView_Unloaded;
    }

    public event EventHandler<BangumiSubjectCard>? SubjectRequested;

    private string T(string key) => _localization.GetString(key);
    private string L(string zhCn, string jaJp, string enUs) =>
        _localization.CurrentLanguage switch
        {
            "ja-JP" => jaJp,
            "en-US" => enUs,
            _ => zhCn,
        };

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_AnimeIndex");
        PageSubtitle.Text = T("Bangumi_AnimeIndexSubtitle");
        SearchBox.PlaceholderText = T("Bangumi_AnimeSearchPlaceholder");
        SearchButtonText.Text = T("Bangumi_Search");
        ResetButton.Content = T("Bangumi_ResetFilters");
        RefreshButtonText.Text = T("Bangumi_Refresh");
        FilterTitle.Text = T("Bangumi_FilterTitle");
        FormatFilterLabel.Text = T("Bangumi_FilterFormat");
        SourceFilterLabel.Text = T("Bangumi_FilterSource");
        GenreFilterLabel.Text = T("Bangumi_FilterGenre");
        RegionFilterLabel.Text = T("Bangumi_FilterRegion");
        AudienceFilterLabel.Text = T("Bangumi_FilterAudience");
        YearFilterLabel.Text = T("Bangumi_FilterYear");
    }

    private void InitializeFilters()
    {
        _synchronizingFilters = true;
        try
        {
            FormatFilter.ItemsSource = Localize(FormatOptions);
            SourceFilter.ItemsSource = Localize(SourceOptions);
            GenreFilter.ItemsSource = Localize(GenreOptions);
            RegionFilter.ItemsSource = Localize(RegionOptions);
            AudienceFilter.ItemsSource = Localize(AudienceOptions);
            SortCombo.ItemsSource = Localize(SortOptions);

            var currentYear = DateTimeOffset.Now.Year;
            var years = new List<YearOption>
            {
                new(null, L("全部", "すべて", "All years")),
            };
            for (var year = currentYear + 1; year >= 2000; year--)
                years.Add(new YearOption(year, year.ToString(CultureInfo.InvariantCulture)));
            YearFilter.ItemsSource = years;

            FormatFilter.SelectedIndex = 0;
            SourceFilter.SelectedIndex = 0;
            GenreFilter.SelectedIndex = 0;
            RegionFilter.SelectedIndex = 0;
            AudienceFilter.SelectedIndex = 0;
            YearFilter.SelectedIndex = 0;
            SortCombo.SelectedIndex = 0;
        }
        finally
        {
            _synchronizingFilters = false;
        }
    }

    private DisplayOption[] Localize(IReadOnlyList<FilterOption> options) =>
        options.Select(option =>
            new DisplayOption(option.Value, L(option.ZhCn, option.JaJp, option.EnUs))).ToArray();

    private async void BangumiAnimeIndexView_Loaded(object sender, RoutedEventArgs e) =>
        await LoadAsync(false, false);

    private void BangumiAnimeIndexView_Unloaded(object sender, RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private async void SearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args) =>
        await LoadAsync(false, false);

    private async void SearchButton_Click(object sender, RoutedEventArgs e) =>
        await LoadAsync(false, false);

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await LoadAsync(true, false);

    private async void PageScrollViewer_ViewChanged(
        object sender,
        ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer ||
            !_hasMore ||
            _isLoading ||
            _items.Count == 0)
        {
            return;
        }

        const double preloadDistance = 520d;
        if (scrollViewer.VerticalOffset <
            Math.Max(
                0d,
                scrollViewer.ScrollableHeight -
                preloadDistance))
        {
            return;
        }

        await LoadAsync(false, true);
    }

    private async void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _synchronizingFilters = true;
        try
        {
            SearchBox.Text = string.Empty;
            FormatFilter.SelectedIndex = 0;
            SourceFilter.SelectedIndex = 0;
            GenreFilter.SelectedIndex = 0;
            RegionFilter.SelectedIndex = 0;
            AudienceFilter.SelectedIndex = 0;
            YearFilter.SelectedIndex = 0;
            SortCombo.SelectedIndex = 0;
        }
        finally
        {
            _synchronizingFilters = false;
        }
        await LoadAsync(false, false);
    }

    private async void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_controlsReady || _synchronizingFilters)
            return;
        await LoadAsync(false, false);
    }

    private async void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_controlsReady || _synchronizingFilters)
            return;
        await LoadAsync(false, false);
    }

    private BangumiAnimeSearchQuery BuildSearchQuery()
    {
        var metaTags = new List<string>();
        AddSelected(metaTags, FormatFilter);
        AddSelected(metaTags, SourceFilter);

        var tags = new List<string>();
        AddSelected(tags, GenreFilter);
        AddSelected(tags, RegionFilter);
        AddSelected(tags, AudienceFilter);

        var year = YearFilter.SelectedItem is YearOption yearOption
            ? yearOption.Year
            : null;
        var sort = SortCombo.SelectedItem is DisplayOption sortOption &&
                   !string.IsNullOrWhiteSpace(sortOption.Value)
            ? sortOption.Value
            : "rank";

        return new BangumiAnimeSearchQuery(
            SearchBox.Text?.Trim() ?? string.Empty,
            sort,
            metaTags,
            tags,
            year);
    }

    private static void AddSelected(ICollection<string> values, RadioButtons buttons)
    {
        if (buttons.SelectedItem is DisplayOption option &&
            !string.IsNullOrWhiteSpace(option.Value))
            values.Add(option.Value);
    }

    private static bool IsDefaultRanking(BangumiAnimeSearchQuery query) =>
        string.IsNullOrWhiteSpace(query.Keyword) &&
        string.Equals(query.Sort, "rank", StringComparison.Ordinal) &&
        query.MetaTags.Count == 0 &&
        query.Tags.Count == 0 &&
        query.Year is null;

    private async Task LoadAsync(bool forceRefresh, bool append)
    {
        if (append && (_isLoading || !_hasMore))
            return;

        if (!append)
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
        }

        var requestCancellation = new CancellationTokenSource();
        _loadCancellation = requestCancellation;
        var cancellationToken = requestCancellation.Token;
        _isLoading = true;

        SetBusy(true);
        StatusText.Text = append
            ? T("Bangumi_LoadingMore")
            : T("Bangumi_Loading");

        try
        {
            var query = BuildSearchQuery();
            var offset = append ? _nextOffset : 0;
            BangumiSubjectPage page;
            string? sourceStatus = null;

            if (IsDefaultRanking(query))
            {
                var result = await _repository.GetRankedAnimeAsync(
                    offset,
                    forceRefresh,
                    cancellationToken);
                page = result.Value;
                sourceStatus = BuildCacheStatus(
                    result.IsFromCache,
                    result.IsStale,
                    result.FetchedAtUtc);
            }
            else
            {
                page = await _repository.SearchAnimeAsync(
                    query,
                    PageSize,
                    offset,
                    cancellationToken);
            }

            if (append)
                AppendItems(page.Items);
            else
                SetItems(page.Items);

            _nextOffset = page.Offset + page.Items.Count;
            _total = page.Total;
            _hasMore = page.HasMore;

            if (_items.Count == 0)
            {
                StatusText.Text = T("Bangumi_NoResults");
            }
            else
            {
                var count = string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_AnimeIndexCountFormat"),
                    _items.Count,
                    _total);
                StatusText.Text = string.IsNullOrWhiteSpace(sourceStatus)
                    ? count
                    : count + " · " + sourceStatus;
            }
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
            if (ReferenceEquals(_loadCancellation, requestCancellation))
            {
                _loadCancellation = null;
                _isLoading = false;
                SetBusy(false);
            }

            requestCancellation.Dispose();
        }
    }

    private string BuildCacheStatus(
        bool isFromCache,
        bool isStale,
        DateTimeOffset fetchedAtUtc)
    {
        var localTime = fetchedAtUtc.ToLocalTime().ToString(
            "g",
            CultureInfo.CurrentCulture);

        return isStale
            ? string.Format(CultureInfo.CurrentCulture, T("Bangumi_StaleCacheFormat"), localTime)
            : isFromCache
                ? string.Format(CultureInfo.CurrentCulture, T("Bangumi_CacheFormat"), localTime)
                : string.Format(CultureInfo.CurrentCulture, T("Bangumi_UpdatedFormat"), localTime);
    }

    private void SetItems(IReadOnlyList<BangumiSubjectCard> subjects)
    {
        _items.Clear();
        AppendItems(subjects);
    }

    private void AppendItems(IReadOnlyList<BangumiSubjectCard> subjects)
    {
        var existingIds = _items.Select(static item => item.Subject.Id).ToHashSet();
        foreach (var subject in subjects)
            if (existingIds.Add(subject.Id))
                _items.Add(CreateViewModel(subject));
    }

    private BangumiCardViewModel CreateViewModel(BangumiSubjectCard subject)
    {
        var title = FirstNonEmpty(subject.NativeTitle, subject.ChineseTitle);
        var subtitle = FirstNonEmpty(subject.ChineseTitle, subject.NativeTitle);
        if (string.Equals(title, subtitle, StringComparison.CurrentCultureIgnoreCase))
            subtitle = string.Empty;

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.AirDate))
            meta.Add(subject.AirDate!);
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");

        var footer = new List<string>();
        if (subject.EpisodeCount > 0)
            footer.Add(L(
                $"{subject.EpisodeCount} 集",
                $"{subject.EpisodeCount} 話",
                $"{subject.EpisodeCount} eps"));
        if (subject.CollectionTotal > 0)
            footer.Add(L(
                $"{subject.CollectionTotal} 收藏",
                $"{subject.CollectionTotal} 收藏",
                $"{subject.CollectionTotal} collections"));

        return new BangumiCardViewModel(
            subject,
            CreateArtwork(subject.PosterUrl),
            title,
            subtitle,
            string.Join(" · ", meta),
            string.Join(" · ", footer),
            FirstNonEmpty(subject.Platform, "Bangumi"));
    }

    private static BitmapImage? CreateArtwork(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            return null;

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

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BangumiCardViewModel item)
            SubjectRequested?.Invoke(this, item.Subject);
    }

    private void SetBusy(bool busy)
    {
        LoadingRing.IsActive = busy;
        LoadingRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;
        SearchBox.IsEnabled = !busy;
        SearchButton.IsEnabled = !busy;
        ResetButton.IsEnabled = !busy;
        SortCombo.IsEnabled = !busy;
        FormatFilter.IsEnabled = !busy;
        SourceFilter.IsEnabled = !busy;
        GenreFilter.IsEnabled = !busy;
        RegionFilter.IsEnabled = !busy;
        AudienceFilter.IsEnabled = !busy;
        YearFilter.IsEnabled = !busy;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
