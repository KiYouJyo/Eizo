using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Views;

public sealed partial class HomeView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiRepository _bangumi =
        BangumiRepository.Default;
    private readonly IReadOnlyList<MediaCardModel> _continueItems;
    private readonly Dictionary<string, BangumiSubjectCard> _bangumiSeasonSubjects =
        new(StringComparer.Ordinal);
    private IReadOnlyList<MediaCardModel> _seasonItems = [];
    private CancellationTokenSource? _seasonCancellation;
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;

    public event EventHandler<string>? DetailRequested;
    public event EventHandler<string>? PlayRequested;
    public event EventHandler<BangumiSubjectCard>? BangumiSubjectRequested;
    public event EventHandler? BangumiSeasonalRequested;

    public HomeView()
    {
        InitializeComponent();
        ApplyText();

        _continueItems =
        [
            new MediaCardModel("葬送的芙莉莲", "葬送のフリーレン", "18 / 28", 52),
            new MediaCardModel("药屋少女的呢喃", "薬屋のひとりごと", "14 / 24", 36),
            new MediaCardModel("VIVANT", "VIVANT", "6 / 10", 64),
            new MediaCardModel("非自然死亡", "アンナチュラル", "5 / 10", 44)
        ];

        RebuildMediaGrids();

        Loaded += HomeView_Loaded;
        Unloaded += HomeView_Unloaded;
    }

    internal void SetResponsiveMode(ResponsiveLayoutMode mode)
    {
        if (_responsiveMode == mode &&
            ContinueGrid.Children.Count > 0)
        {
            return;
        }

        _responsiveMode = mode;
        RebuildMediaGrids();
    }

    private async void HomeView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadCurrentSeasonAsync();
    }

    private void HomeView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _seasonCancellation?.Cancel();
        _seasonCancellation?.Dispose();
        _seasonCancellation = null;
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
            ExternalKey: key);
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

    private void PopulateGrid(
        Grid grid,
        IReadOnlyList<MediaCardModel> items,
        int columns,
        string templateKey)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        columns = Math.Max(
            1,
            Math.Min(
                columns,
                Math.Max(1, items.Count)));

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
        SearchBox.PlaceholderText = T("Search_Placeholder");
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
        RoutedEventArgs e) =>
        DetailRequested?.Invoke(
            this,
            "葬送的芙莉莲");

    private void FeaturedPlayButton_Click(
        object sender,
        RoutedEventArgs e) =>
        PlayRequested?.Invoke(
            this,
            "葬送的芙莉莲");

    private void MediaCard_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: string title })
            DetailRequested?.Invoke(this, title);
    }

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

    private static string FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(
            static value =>
                !string.IsNullOrWhiteSpace(value))
        ?? string.Empty;
}
