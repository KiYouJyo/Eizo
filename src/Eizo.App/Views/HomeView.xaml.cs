using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Eizo.Views;

public sealed partial class HomeView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiRepository _bangumi =
        BangumiRepository.Default;
    private MediaCatalogStore? _catalog;
    private PlaybackHistoryStore? _history;
    private Task? _initializationTask;
    private bool _storeEventsAttached;

    private readonly Dictionary<string, BangumiSubjectCard> _bangumiSeasonSubjects =
        new(StringComparer.Ordinal);

    private const int SearchPageSize = 30;

    private IReadOnlyList<HomeContinueCardModel> _continueItems = [];
    private IReadOnlyList<MediaCardModel> _seasonItems = [];
    private IReadOnlyList<HomeSearchSuggestion> _searchSuggestions = [];
    private IReadOnlyList<HomeSearchResultItem> _searchResults = [];
    private string _activeSearchQuery = string.Empty;
    private int _searchNextOffset;
    private int _searchTotal;
    private CatalogSubjectModel? _featuredSubject;
    private CatalogMediaItemModel? _featuredItem;
    private CancellationTokenSource? _seasonCancellation;
    private CancellationTokenSource? _searchCancellation;
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;

    public event EventHandler<CatalogMediaItemModel>? CatalogMediaRequested;
    public event EventHandler<CatalogSubjectModel>? CatalogSubjectRequested;
    public event EventHandler<BangumiSubjectCard>? BangumiSubjectRequested;
    public event EventHandler? BangumiSeasonalRequested;
    public event EventHandler? LibraryRequested;

    public HomeView()
    {
        Eizo.StartupTrace.Mark("HomeView.ctor:begin");
        InitializeComponent();
        Eizo.StartupTrace.Mark("HomeView.InitializeComponent:end");
        ApplyText();
        RebuildMediaGrids();

        Loaded += HomeView_Loaded;
        Unloaded += HomeView_Unloaded;
        Eizo.StartupTrace.Mark("HomeView.ctor:end");
    }

    internal void SetResponsiveMode(ResponsiveLayoutMode mode)
    {
        if (_responsiveMode == mode &&
            ContinueGrid.Children.Count > 0)
        {
            UpdateHeroLayout();
            return;
        }

        _responsiveMode = mode;
        RebuildMediaGrids();
        UpdateHeroLayout();
    }

    private async void HomeView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Eizo.StartupTrace.Mark("HomeView.Loaded:begin");
        await EnsureInitialContentAsync();
        AttachStoreEvents();
        Eizo.StartupTrace.Mark("HomeView.Loaded:end");
    }

    internal Task EnsureInitialContentAsync() =>
        _initializationTask ??= InitializeInitialContentCoreAsync();

    private async Task InitializeInitialContentCoreAsync()
    {
        Eizo.StartupTrace.Mark("HomeView.InitializeInitialContent:begin");

        // MediaCatalogStore / PlaybackHistoryStore both perform synchronous disk
        // reads in their constructors. Warm them on a worker after the splash
        // has painted instead of blocking WinUI's first compositor frame.
        var stores = await Task.Run(static () =>
            (Catalog: MediaCatalogStore.Default,
             History: PlaybackHistoryStore.Default));

        _catalog = stores.Catalog;
        _history = stores.History;

        Eizo.StartupTrace.Mark("HomeView.RefreshLibraryContent(deferred):begin");
        RefreshLibraryContent();
        Eizo.StartupTrace.Mark("HomeView.RefreshLibraryContent(deferred):end");

        // Network/cache-backed seasonal content is deliberately not part of the
        // startup gate. It can fill in after the local home surface is ready.
        _ = LoadCurrentSeasonAsync();

        Eizo.StartupTrace.Mark("HomeView.InitializeInitialContent:end");
    }

    private void AttachStoreEvents()
    {
        if (_storeEventsAttached ||
            _catalog is null ||
            _history is null)
        {
            return;
        }

        _catalog.Changed += Catalog_Changed;
        _history.Changed += History_Changed;
        _storeEventsAttached = true;
    }

    private void DetachStoreEvents()
    {
        if (!_storeEventsAttached)
            return;

        if (_catalog is not null)
            _catalog.Changed -= Catalog_Changed;
        if (_history is not null)
            _history.Changed -= History_Changed;

        _storeEventsAttached = false;
    }

    private void HomeView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        DetachStoreEvents();

        _seasonCancellation?.Cancel();
        _seasonCancellation?.Dispose();
        _seasonCancellation = null;

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
    }

    private void Catalog_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshLibraryContent);

    private void History_Changed(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshLibraryContent);

    private void RefreshLibraryContent()
    {
        var catalog = _catalog;
        var history = _history;
        if (catalog is null || history is null)
            return;

        var items = catalog.SnapshotForDisplay();
        Eizo.StartupTrace.Mark($"HomeView.SnapshotForDisplay:end items={items.Count}");
        var aggregation = CatalogSubjectAggregator.Build(items);
        Eizo.StartupTrace.Mark($"HomeView.CatalogSubjectAggregator.Build:end subjects={aggregation.Subjects.Count} standalone={aggregation.StandaloneItems.Count}");

        var itemsByKey = items.ToDictionary(
            MediaCatalogStore.ItemKey,
            StringComparer.Ordinal);

        var subjectsByItemKey = aggregation.Subjects
            .SelectMany(subject =>
                subject.Items.Select(item =>
                    (Key: MediaCatalogStore.ItemKey(item), Subject: subject)))
            .GroupBy(static value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.First().Subject,
                StringComparer.Ordinal);

        _continueItems = history.Snapshot()
            .Where(entry => itemsByKey.ContainsKey(entry.ItemKey))
            .Take(4)
            .Select(entry =>
            {
                var item = itemsByKey[entry.ItemKey];
                subjectsByItemKey.TryGetValue(
                    entry.ItemKey,
                    out var subject);

                return new HomeContinueCardModel(
                    item,
                    subject?.Title ?? item.DisplayTitle,
                    subject?.NativeTitle ?? item.SecondaryTitle,
                    BuildContinueMeta(item),
                    entry.ProgressPercent,
                    FirstNonEmpty(
                        item.Metadata?.EpisodeThumbnailUrl,
                        subject?.Metadata?.BackdropUrl,
                        item.Metadata?.BackdropUrl,
                        subject?.Metadata?.PosterUrl,
                        item.Metadata?.PosterUrl));
            })
            .ToArray();

        var illustratedSubjects = aggregation.Subjects
            .Where(static subject =>
                subject.FirstPlayableItem is not null &&
                HasArtwork(subject))
            .OrderByDescending(static subject =>
                !string.IsNullOrWhiteSpace(
                    subject.Metadata?.BackdropUrl))
            .ThenByDescending(static subject =>
                subject.Metadata is { IsResolved: true })
            .ThenBy(static subject =>
                subject.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var fallbackSubjects = aggregation.Subjects
            .Where(static subject =>
                subject.FirstPlayableItem is not null)
            .OrderByDescending(static subject =>
                subject.Metadata is { IsResolved: true })
            .ThenBy(static subject =>
                subject.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var heroCandidates =
            illustratedSubjects.Length > 0
                ? illustratedSubjects
                : fallbackSubjects;

        _featuredSubject = heroCandidates.Length == 0
            ? null
            : heroCandidates[
                DateTimeOffset.Now.DayOfYear %
                Math.Min(heroCandidates.Length, 8)];

        _featuredItem =
            _featuredSubject?.FirstPlayableItem ??
            aggregation.StandaloneItems
                .OrderByDescending(static item =>
                    HasArtwork(item))
                .ThenByDescending(static item =>
                    item.Metadata is { IsResolved: true })
                .FirstOrDefault();

        ApplyHero();
        RebuildMediaGrids();
    }

    private void ApplyHero()
    {
        if (_featuredSubject is { } subject &&
            _featuredItem is { } subjectItem)
        {
            var presentation =
                CatalogSubjectPresentation.Create(subject);

            FeaturedTitle.Text = presentation.Title;
            FeaturedNativeTitle.Text = presentation.SecondaryTitle;
            FeaturedNativeTitle.Visibility =
                string.IsNullOrWhiteSpace(
                    presentation.SecondaryTitle)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            FeaturedMeta.Text = subject.Meta;
            FeaturedMeta.Visibility =
                string.IsNullOrWhiteSpace(subject.Meta)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            FeaturedDescription.Text =
                presentation.Overview;
            FeaturedDescription.Visibility =
                string.IsNullOrWhiteSpace(FeaturedDescription.Text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            HeroArtworkImage.Source = CreateArtwork(
                ResolveHeroArtworkUrl(subject),
                decodePixelWidth: 1200);

            FeaturedPlayButton.IsEnabled = true;
            FeaturedPlayButton.Visibility = Visibility.Visible;
            FeaturedDetailsButton.IsEnabled = true;
            FeaturedDetailsButton.Visibility = Visibility.Visible;
            UpdateHeroLayout();
            return;
        }

        if (_featuredItem is { } item)
        {
            FeaturedTitle.Text = item.DisplayTitle;
            FeaturedNativeTitle.Text = item.SecondaryTitle;
            FeaturedNativeTitle.Visibility =
                string.IsNullOrWhiteSpace(item.SecondaryTitle)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            FeaturedMeta.Text = item.Meta;
            FeaturedMeta.Visibility =
                string.IsNullOrWhiteSpace(item.Meta)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            FeaturedDescription.Text =
                item.Metadata?.Overview ?? string.Empty;
            FeaturedDescription.Visibility =
                string.IsNullOrWhiteSpace(FeaturedDescription.Text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            HeroArtworkImage.Source = CreateArtwork(
                FirstNonEmpty(
                    item.Metadata?.BackdropUrl,
                    item.Metadata?.PosterUrl),
                decodePixelWidth: 1200);

            FeaturedPlayButton.IsEnabled = true;
            FeaturedPlayButton.Visibility = Visibility.Visible;
            FeaturedDetailsButton.IsEnabled = false;
            FeaturedDetailsButton.Visibility = Visibility.Collapsed;
            UpdateHeroLayout();
            return;
        }

        FeaturedTitle.Text = T("Status_EmptyLibrary");
        FeaturedNativeTitle.Text = string.Empty;
        FeaturedNativeTitle.Visibility = Visibility.Collapsed;
        FeaturedMeta.Text = string.Empty;
        FeaturedMeta.Visibility = Visibility.Collapsed;
        FeaturedDescription.Text = string.Empty;
        FeaturedDescription.Visibility = Visibility.Collapsed;
        HeroArtworkImage.Source = null;
        FeaturedPlayButton.IsEnabled = false;
        FeaturedPlayButton.Visibility = Visibility.Collapsed;
        FeaturedDetailsButton.IsEnabled = false;
        FeaturedDetailsButton.Visibility = Visibility.Collapsed;
        UpdateHeroLayout();
    }

    private void UpdateHeroLayout()
    {
        var showArtwork =
            _responsiveMode == ResponsiveLayoutMode.Large &&
            HeroArtworkImage.Source is not null;

        HeroArtwork.Visibility =
            showArtwork
                ? Visibility.Visible
                : Visibility.Collapsed;

        Grid.SetColumnSpan(
            HeroText,
            showArtwork ? 1 : 2);
    }

    private async Task LoadCurrentSeasonAsync()
    {
        _seasonCancellation?.Cancel();
        _seasonCancellation?.Dispose();
        _seasonCancellation = new CancellationTokenSource();
        var cancellationToken = _seasonCancellation.Token;

        SeasonLoadingRing.IsActive = true;
        SeasonLoadingRing.Visibility = Visibility.Visible;
        SeasonStatusText.Text = T("Bangumi_Loading");

        try
        {
            var result =
                await _bangumi.GetCurrentSeasonAsync(
                    DateTimeOffset.Now,
                    forceRefresh: false,
                    cancellationToken);

            var subjects = result.Value.Items
                .OrderBy(static subject =>
                    subject.Rank <= 0
                        ? int.MaxValue
                        : subject.Rank)
                .ThenByDescending(static subject => subject.Score)
                .ThenBy(static subject => subject.AirDate, StringComparer.Ordinal)
                .Take(8)
                .ToArray();

            _bangumiSeasonSubjects.Clear();
            _seasonItems = subjects
                .Select(CreateSeasonCard)
                .ToArray();

            RebuildMediaGrids();

            var localTime =
                result.FetchedAtUtc.ToLocalTime().ToString(
                    "g",
                    CultureInfo.CurrentCulture);

            SeasonStatusText.Text = result.IsStale
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
            _seasonItems = [];
            _bangumiSeasonSubjects.Clear();
            RebuildMediaGrids();
            SeasonStatusText.Text =
                T("Bangumi_NetworkError");
        }
        finally
        {
            SeasonLoadingRing.IsActive = false;
            SeasonLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private MediaCardModel CreateSeasonCard(
        BangumiSubjectCard subject)
    {
        var key =
            "bangumi:" +
            subject.Id.ToString(
                CultureInfo.InvariantCulture);
        _bangumiSeasonSubjects[key] = subject;

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

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.AirDate))
            meta.Add(subject.AirDate!);
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");

        return new MediaCardModel(
            title,
            nativeTitle,
            string.Join(" · ", meta),
            Progress: 0,
            ExternalKey: key,
            ArtworkUrl: subject.PosterUrl);
    }

    private async void SearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;

        var query = sender.Text.Trim();
        if (query.Length < 1)
        {
            _searchSuggestions = [];
            sender.ItemsSource = null;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(260),
                cancellation.Token);

            var page = await _bangumi.SearchAnimeAsync(
                query,
                limit: 10,
                offset: 0,
                cancellation.Token);

            if (!ReferenceEquals(
                    _searchCancellation,
                    cancellation))
            {
                return;
            }

            _searchSuggestions = page.Items
                .Select(CreateSearchSuggestion)
                .ToArray();
            sender.ItemsSource = _searchSuggestions;
            sender.IsSuggestionListOpen =
                _searchSuggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (ReferenceEquals(
                    _searchCancellation,
                    cancellation))
            {
                _searchSuggestions = [];
                sender.ItemsSource = null;
            }
        }
    }

    private void SearchBox_SuggestionChosen(
        AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is HomeSearchSuggestion suggestion)
            sender.Text = suggestion.DisplayText;
    }

    private async void SearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.ChosenSuggestion is HomeSearchSuggestion selected
            ? FirstNonEmpty(
                selected.Subject.ChineseTitle,
                selected.Subject.NativeTitle)
            : args.QueryText.Trim();

        if (string.IsNullOrWhiteSpace(query))
            return;

        sender.IsSuggestionListOpen = false;
        await LoadSearchResultsAsync(
            query,
            append: false);
    }

    private async Task LoadSearchResultsAsync(
        string query,
        bool append)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        if (!append)
        {
            _activeSearchQuery = query.Trim();
            _searchResults = [];
            _searchNextOffset = 0;
            _searchTotal = 0;
            SearchResultsGrid.ItemsSource = _searchResults;
            SetSearchMode(true);
        }

        SearchResultsLoadingRing.IsActive = true;
        SearchResultsLoadingRing.Visibility = Visibility.Visible;
        SearchResultsLoadMoreButton.Visibility = Visibility.Collapsed;
        SearchResultsStatus.Text = T("Bangumi_Loading");

        try
        {
            var page = await _bangumi.SearchAnimeAsync(
                _activeSearchQuery,
                limit: SearchPageSize,
                offset: append ? _searchNextOffset : 0,
                cancellationToken);

            var newItems = page.Items
                .Select(CreateSearchResultItem)
                .ToArray();

            _searchResults = append
                ? _searchResults
                    .Concat(newItems)
                    .GroupBy(static item => item.Subject.Id)
                    .Select(static group => group.First())
                    .ToArray()
                : newItems;

            _searchNextOffset =
                page.Offset + page.Items.Count;
            _searchTotal = page.Total;
            SearchResultsGrid.ItemsSource = _searchResults;

            SearchResultsTitle.Text = string.Format(
                CultureInfo.CurrentCulture,
                T("Home_BangumiSearchResultsTitleFormat"),
                _activeSearchQuery);

            SearchResultsStatus.Text = _searchResults.Count == 0
                ? T("Bangumi_NoResults")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T("Home_BangumiSearchResultsCountFormat"),
                    _searchResults.Count,
                    _searchTotal);

            SearchResultsLoadMoreButton.Visibility =
                page.HasMore
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            SearchResultsStatus.Text =
                T("Bangumi_NetworkError");
        }
        finally
        {
            SearchResultsLoadingRing.IsActive = false;
            SearchResultsLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void SetSearchMode(bool enabled)
    {
        SearchResultsPanel.Visibility =
            enabled
                ? Visibility.Visible
                : Visibility.Collapsed;

        var normalVisibility =
            enabled
                ? Visibility.Collapsed
                : Visibility.Visible;

        HeroPanel.Visibility = normalVisibility;
        ContinueHeader.Visibility = normalVisibility;
        ContinueGrid.Visibility = normalVisibility;
        SeasonHeader.Visibility = normalVisibility;
        SeasonGrid.Visibility = normalVisibility;
    }

    private async void SearchResultsLoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeSearchQuery))
            return;

        await LoadSearchResultsAsync(
            _activeSearchQuery,
            append: true);
    }

    private void SearchResultsBackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _activeSearchQuery = string.Empty;
        _searchResults = [];
        _searchNextOffset = 0;
        _searchTotal = 0;
        SearchResultsGrid.ItemsSource = null;
        SearchResultsStatus.Text = string.Empty;
        SetSearchMode(false);
    }

    private void SearchResultCard_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: BangumiSubjectCard subject
            })
        {
            BangumiSubjectRequested?.Invoke(
                this,
                subject);
        }
    }

    private HomeSearchResultItem CreateSearchResultItem(
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

        return new HomeSearchResultItem(
            subject,
            title,
            subtitle,
            string.Join(" · ", meta),
            subject.PosterUrl);
    }

    private HomeSearchSuggestion CreateSearchSuggestion(
        BangumiSubjectCard subject)
    {
        var preferNative =
            _localization.CurrentLanguage is "ja-JP" or "en-US";

        var primary = preferNative
            ? FirstNonEmpty(
                subject.NativeTitle,
                subject.ChineseTitle)
            : FirstNonEmpty(
                subject.ChineseTitle,
                subject.NativeTitle);

        var secondary = preferNative
            ? subject.ChineseTitle
            : subject.NativeTitle;

        var display = string.IsNullOrWhiteSpace(secondary) ||
                      string.Equals(
                          primary,
                          secondary,
                          StringComparison.CurrentCultureIgnoreCase)
            ? primary
            : $"{primary} · {secondary}";

        return new HomeSearchSuggestion(
            subject,
            display);
    }

    private void RebuildMediaGrids()
    {
        var columns = _responsiveMode switch
        {
            ResponsiveLayoutMode.Large => 4,
            ResponsiveLayoutMode.Medium => 2,
            _ => 1
        };

        PopulateGrid(
            ContinueGrid,
            _continueItems,
            columns,
            "ContinueCardTemplate");
        PopulateGrid(
            SeasonGrid,
            _seasonItems,
            columns,
            "SeasonCardTemplate");
    }

    private void PopulateGrid<T>(
        Grid grid,
        IReadOnlyList<T> items,
        int columns,
        string templateKey)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        columns = Math.Max(
            1,
            columns);

        for (var column = 0; column < columns; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());

        var rows =
            (items.Count + columns - 1) /
            columns;
        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });
        }

        var template =
            (DataTemplate)Resources[templateKey];

        for (var index = 0; index < items.Count; index++)
        {
            var presenter = new ContentControl
            {
                Content = items[index],
                ContentTemplate = template,
                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch,
                VerticalContentAlignment =
                    VerticalAlignment.Stretch
            };

            Grid.SetRow(
                presenter,
                index / columns);
            Grid.SetColumn(
                presenter,
                index % columns);
            grid.Children.Add(presenter);
        }
    }

    private string T(string key) =>
        _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_Home");
        SearchBox.PlaceholderText =
            T("Home_BangumiSearchPlaceholder");
        SearchResultsBackButton.Content =
            T("Common_Back");
        SearchResultsLoadMoreButton.Content =
            T("Bangumi_LoadMore");
        FeaturedEyebrow.Text = T("Home_Featured");
        FeaturedDescription.Text =
            T("Home_FeaturedDescription");
        FeaturedPlayText.Text = T("Common_Play");
        FeaturedDetailsButton.Content =
            T("Common_ViewDetails");
        ContinueTitle.Text =
            T("Section_ContinueWatching");
        SeasonTitle.Text =
            T("Section_CurrentSeason");
        ContinueAllButton.Content =
            T("Common_ViewAll");
        SeasonAllButton.Content =
            T("Common_ViewAll");
    }

    private void FeaturedButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_featuredSubject is { } subject)
        {
            CatalogSubjectRequested?.Invoke(
                this,
                subject);
            return;
        }

        // Standalone/unparsed media has no real subject detail model.
        // Keep the details button disabled rather than routing to legacy sample UI.
    }

    private void FeaturedPlayButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_featuredItem is { } item)
        {
            CatalogMediaRequested?.Invoke(
                this,
                item);
        }
    }

    private void MediaCard_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: CatalogMediaItemModel item
            })
        {
            CatalogMediaRequested?.Invoke(
                this,
                item);
        }
    }

    private void ContinueAllButton_Click(
        object sender,
        RoutedEventArgs e) =>
        LibraryRequested?.Invoke(
            this,
            EventArgs.Empty);

    private void SeasonCard_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key } ||
            !_bangumiSeasonSubjects.TryGetValue(
                key,
                out var subject))
        {
            return;
        }

        BangumiSubjectRequested?.Invoke(
            this,
            subject);
    }

    private void SeasonAllButton_Click(
        object sender,
        RoutedEventArgs e) =>
        BangumiSeasonalRequested?.Invoke(
            this,
            EventArgs.Empty);

    private static string BuildContinueMeta(
        CatalogMediaItemModel item)
    {
        var parts = new List<string>();
        var recognition = item.Recognition;

        if (recognition?.SeasonNumber is { } season &&
            recognition.EpisodeNumber is { } episode)
        {
            parts.Add(
                $"S{season:00}E{episode:0.##}");
        }
        else if (recognition?.EpisodeNumber is { } standaloneEpisode)
        {
            parts.Add(
                $"EP{standaloneEpisode:0.##}");
        }

        var episodeTitle = FirstNonEmpty(
            item.Metadata?.EpisodeTitle,
            recognition?.EpisodeTitle);

        if (!string.IsNullOrWhiteSpace(episodeTitle))
            parts.Add(episodeTitle);

        if (parts.Count == 0 &&
            !string.IsNullOrWhiteSpace(item.Meta))
        {
            parts.Add(item.Meta);
        }

        return string.Join(" · ", parts);
    }

    private static string ResolveHeroArtworkUrl(
        CatalogSubjectModel subject)
    {
        var metadata = subject.Items
            .Select(static item => item.Metadata)
            .OfType<Eizo.MetadataIntegration.MediaMetadataSnapshot>()
            .Where(static value => value.IsResolved)
            .ToArray();

        return FirstNonEmpty(
            metadata
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value.BackdropUrl))
                .OrderByDescending(static value =>
                    !string.IsNullOrWhiteSpace(value.CanonicalTitle))
                .Select(static value => value.BackdropUrl)
                .FirstOrDefault(),
            subject.Metadata?.BackdropUrl,
            metadata
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value.PosterUrl))
                .Select(static value => value.PosterUrl)
                .FirstOrDefault(),
            subject.Metadata?.PosterUrl,
            subject.FirstPlayableItem?.Metadata?.BackdropUrl,
            subject.FirstPlayableItem?.Metadata?.PosterUrl);
    }

    private static bool HasArtwork(
        CatalogSubjectModel subject) =>
        !string.IsNullOrWhiteSpace(
            subject.Metadata?.BackdropUrl) ||
        !string.IsNullOrWhiteSpace(
            subject.Metadata?.PosterUrl) ||
        (subject.FirstPlayableItem is { } item &&
         HasArtwork(item));

    private static bool HasArtwork(
        CatalogMediaItemModel item) =>
        !string.IsNullOrWhiteSpace(
            item.Metadata?.BackdropUrl) ||
        !string.IsNullOrWhiteSpace(
            item.Metadata?.PosterUrl) ||
        !string.IsNullOrWhiteSpace(
            item.Metadata?.EpisodeThumbnailUrl);

    private static ImageSource? CreateArtwork(
        string? url,
        int decodePixelWidth) =>
        ArtworkImageSource.Create(url, decodePixelWidth);

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            static value =>
                !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;

    private sealed record HomeSearchResultItem(
        BangumiSubjectCard Subject,
        string Title,
        string Subtitle,
        string Meta,
        string? ArtworkUrl);

    private sealed record HomeSearchSuggestion(
        BangumiSubjectCard Subject,
        string DisplayText)
    {
        public override string ToString() =>
            DisplayText;
    }
}
