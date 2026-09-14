using System.Globalization;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Views;

public sealed partial class DetailView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly CatalogSubjectModel? _subject;

    public event EventHandler<string>? PlayRequested;
    public event EventHandler<CatalogMediaItemModel>? MediaPlayRequested;

    public DetailView(string title)
    {
        InitializeComponent();
        ApplyText();

        TitleText.Text = title;
        NativeTitleText.Text = title;
        MetaText.Text = string.Empty;
        OverviewText.Text = T("Detail_Overview");
        ReleaseStatText.Text = "-";
        EpisodeStatText.Text = L("2 集", "2 話", "2 episodes");
        SourceStatText.Text = L("示例", "サンプル", "Sample");

        EpisodeList.ItemsSource = new EpisodeDisplayItemModel[]
        {
            new(
                "18",
                "一级魔法使考试",
                "一級魔法使試験",
                "23:41",
                "12:08",
                52),
            new(
                "19",
                "周密的计划",
                "入念な計画",
                "24:03",
                T("Category_Unwatched")),
        };

        SeasonComboBox.ItemsSource = new SeasonOption[]
        {
            new(1, "Season 1"),
        };
        SeasonComboBox.SelectedIndex = 0;
        EpisodeCountText.Text = "2";
        MediaTypeInfoValue.Text = "-";
        ReleaseDateInfoValue.Text = "-";
        TotalEpisodesInfoValue.Text = "-";
        LocalEpisodesInfoValue.Text = "-";
        MediaSourcesInfoValue.Text = "-";
        MetadataProviderInfoValue.Text = "-";
        ExternalIdsText.Text = "-";
        ExternalIdsSection.Visibility = Visibility.Collapsed;
    }

    public DetailView(CatalogSubjectModel subject)
    {
        _subject = subject ?? throw new ArgumentNullException(nameof(subject));

        InitializeComponent();
        ApplyText();
        ApplySubject();

        Loaded += DetailView_Loaded;
        Unloaded += DetailView_Unloaded;
    }

    private void DetailView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        PlaybackHistoryStore.Default.Changed -=
            PlaybackHistoryStore_Changed;
        PlaybackHistoryStore.Default.Changed +=
            PlaybackHistoryStore_Changed;

        RebuildEpisodeList();
    }

    private void DetailView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        PlaybackHistoryStore.Default.Changed -=
            PlaybackHistoryStore_Changed;
    }

    private void PlaybackHistoryStore_Changed(
        object? sender,
        EventArgs e)
    {
        DispatcherQueue.TryEnqueue(
            RebuildEpisodeList);
    }

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
        PlayButton.Content = T("Common_Continue");
        FavoriteButton.Content = T("Common_Favorite");
        EpisodesTitle.Text = _subject?.IsMovieSubject == true
            ? L("影片", "作品", "Films")
            : T("Media_Episodes");
        InfoTitle.Text = L("作品信息", "作品情報", "Title information");
        MediaTypeInfoLabel.Text = L("作品类型", "作品種別", "Type");
        ReleaseDateInfoLabel.Text = L("发布日期", "公開日", "Release date");
        TotalEpisodesInfoLabel.Text = L("总集数", "総話数", "Total episodes");
        LocalEpisodesInfoLabel.Text = L("本地集数", "ローカル話数", "Local episodes");
        MediaSourcesInfoLabel.Text = L("媒体来源", "メディアソース", "Media sources");
        MetadataProviderInfoLabel.Text = L("元数据来源", "メタデータ提供元", "Metadata provider");
        ExternalIdsTitle.Text = L("外部 ID", "外部 ID", "External IDs");
    }

    private void ApplySubject()
    {
        if (_subject is null)
        {
            return;
        }

        TitleText.Text = _subject.Title;
        NativeTitleText.Text = string.IsNullOrWhiteSpace(_subject.NativeTitle)
            ? _subject.Title
            : _subject.NativeTitle;
        MetaText.Text = _subject.Meta;

        var metadata = _subject.Metadata;
        OverviewText.Text = string.IsNullOrWhiteSpace(metadata?.Overview)
            ? L(
                "尚无作品简介。",
                "作品概要はまだありません。",
                "No title overview is available yet.")
            : metadata!.Overview;

        ApplyPoster(metadata?.PosterUrl);
        ApplyBackdrop(metadata?.BackdropUrl);

        var provider = metadata?.Provider ?? "-";
        var subjectId = metadata?.ProviderSubjectId ?? "-";
        var release = metadata?.ReleaseDate ?? "-";
        var sourceCount = _subject.Items
            .Select(static item => item.Location?.SourceId)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var hasLocal = _subject.Items.Any(static item =>
            item.Location?.Kind == MediaLocationKind.LocalFile);
        var hasRemote = _subject.Items.Any(static item =>
            item.Location?.Kind == MediaLocationKind.RemoteUri);
        var sourceKind = (hasLocal, hasRemote) switch
        {
            (true, true) => L("本地 + 网盘", "ローカル + リモート", "Local + remote"),
            (true, false) => L("本地", "ローカル", "Local"),
            (false, true) => L("网盘", "リモート", "Remote"),
            _ => L("未知来源", "不明なソース", "Unknown source"),
        };

        var releaseYear = metadata?.ReleaseDate is { Length: > 0 } releaseText &&
                          DateOnly.TryParse(releaseText, out var releaseDate)
            ? releaseDate.Year.ToString(CultureInfo.CurrentCulture)
            : _subject.Items
                .Select(static item => item.Recognition?.Year)
                .FirstOrDefault(static year => year is not null)?
                .ToString() ?? "-";

        ReleaseStatText.Text = releaseYear;
        EpisodeStatText.Text = _subject.IsMovieSubject
            ? L(
                $"{_subject.EpisodeCount} 部",
                $"{_subject.EpisodeCount} 作品",
                $"{_subject.EpisodeCount} films")
            : L(
                $"{_subject.EpisodeCount} 集",
                $"{_subject.EpisodeCount} 話",
                $"{_subject.EpisodeCount} episodes");
        SourceStatText.Text = sourceCount > 0
            ? L(
                $"{sourceKind} · {sourceCount}",
                $"{sourceKind} · {sourceCount}",
                $"{sourceKind} · {sourceCount}")
            : sourceKind;

        MediaTypeInfoValue.Text = ResolveMediaTypeLabel(_subject);
        ReleaseDateInfoValue.Text = release;

        if (_subject.IsMovieSubject)
        {
            TotalEpisodesInfoRow.Visibility = Visibility.Collapsed;
            TotalEpisodesInfoDivider.Visibility = Visibility.Collapsed;
            LocalEpisodesInfoLabel.Text =
                L("本地影片", "ローカル作品", "Local films");
        }
        else
        {
            TotalEpisodesInfoRow.Visibility = Visibility.Visible;
            TotalEpisodesInfoDivider.Visibility = Visibility.Visible;
            LocalEpisodesInfoLabel.Text =
                L("本地集数", "ローカル話数", "Local episodes");

            var totalEpisodes =
                metadata?.EpisodeCount is > 0
                    ? metadata.EpisodeCount.Value
                    : _subject.EpisodeCount;
            TotalEpisodesInfoValue.Text =
                totalEpisodes.ToString(CultureInfo.CurrentCulture);
        }

        LocalEpisodesInfoValue.Text =
            _subject.EpisodeCount.ToString(
                CultureInfo.CurrentCulture);
        MediaSourcesInfoValue.Text =
            ResolveMediaSourceSummary(_subject.Items);
        MetadataProviderInfoValue.Text =
            FormatMetadataProvider(provider);

        var externalIds = metadata?.ExternalIds
            .Where(pair =>
                !IsSameProviderIdentifier(
                    pair.Key,
                    pair.Value,
                    provider,
                    subjectId))
            .OrderBy(static pair => pair.Key)
            .ToArray() ?? [];

        ExternalIdsSection.Visibility =
            externalIds.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        ExternalIdsText.Text = externalIds.Length > 0
            ? string.Join(
                Environment.NewLine,
                externalIds.Select(pair =>
                    $"{FormatMetadataProvider(pair.Key)}: {pair.Value}"))
            : string.Empty;

        if (_subject.IsMovieSubject)
        {
            SeasonComboBox.ItemsSource = Array.Empty<SeasonOption>();
            SeasonComboBox.SelectedIndex = -1;
            SeasonComboBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            var seasons = _subject.SeasonNumbers
                .Select(season => new SeasonOption(
                    season,
                    season == 0
                        ? L("特别篇", "スペシャル", "Specials")
                        : $"Season {season}"))
                .ToArray();

            SeasonComboBox.ItemsSource = seasons;
            SeasonComboBox.SelectedIndex = seasons.Length > 0 ? 0 : -1;
            SeasonComboBox.Visibility = Visibility.Visible;
        }

        RebuildEpisodeList();
    }

    private string ResolveMediaTypeLabel(
        CatalogSubjectModel subject)
    {
        if (subject.IsMovieSubject ||
            subject.Category == MediaCategoryKind.Movies)
        {
            return L("电影", "映画", "Movie");
        }

        return subject.Category switch
        {
            MediaCategoryKind.Anime =>
                L("动画", "アニメ", "Anime"),
            MediaCategoryKind.Series =>
                L("电视剧", "テレビシリーズ", "TV series"),
            _ =>
                L("剧集", "シリーズ", "Series"),
        };
    }

    private string ResolveMediaSourceSummary(
        IReadOnlyList<CatalogMediaItemModel> items)
    {
        var labels = items
            .Where(static item =>
                item.Location is not null)
            .GroupBy(
                static item => item.Location!.SourceId,
                StringComparer.Ordinal)
            .Select(group =>
            {
                var source =
                    MediaSourceStore.Default.Find(group.Key);
                if (source is { IsBuiltIn: true })
                {
                    return L(
                        "本地媒体",
                        "ローカルメディア",
                        "Local media");
                }

                if (source is not null &&
                    !string.IsNullOrWhiteSpace(
                        source.DisplayName))
                {
                    return source.DisplayName;
                }

                return group.First().Location?.Kind ==
                       MediaLocationKind.LocalFile
                    ? L(
                        "本地媒体",
                        "ローカルメディア",
                        "Local media")
                    : L(
                        "远程媒体",
                        "リモートメディア",
                        "Remote media");
            })
            .Distinct(
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return labels.Length > 0
            ? string.Join(" · ", labels)
            : L(
                "未知来源",
                "不明なソース",
                "Unknown source");
    }

    private static string FormatMetadataProvider(
        string? provider) =>
        provider?.Trim().ToLowerInvariant() switch
        {
            "bangumi" => "Bangumi",
            "anilist" => "AniList",
            "tmdb" => "TMDB",
            { Length: > 0 } value => value,
            _ => "-",
        };

    private static bool IsSameProviderIdentifier(
        string key,
        string value,
        string? provider,
        string? subjectId) =>
        !string.IsNullOrWhiteSpace(provider) &&
        !string.IsNullOrWhiteSpace(subjectId) &&
        string.Equals(
            key.Trim(),
            provider.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            value.Trim(),
            subjectId.Trim(),
            StringComparison.OrdinalIgnoreCase);

    private void ApplyPoster(string? posterUrl)
    {
        PosterImage.Source = CreateRemoteImage(posterUrl, 480);
        PosterPlaceholder.Visibility = PosterImage.Source is null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyBackdrop(string? backdropUrl) =>
        BackdropImage.Source = CreateRemoteImage(backdropUrl, 1400);

    private static BitmapImage? CreateRemoteImage(
        string? url,
        int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        try
        {
            return new BitmapImage
            {
                UriSource = uri,
                DecodePixelWidth = decodePixelWidth,
            };
        }
        catch
        {
            return null;
        }
    }

    private void SeasonComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        RebuildEpisodeList();

    private void RebuildEpisodeList()
    {
        if (_subject is null)
        {
            return;
        }

        var selectedSeason =
            SeasonComboBox.SelectedItem is SeasonOption option
                ? option.Number
                : _subject.SeasonNumbers.FirstOrDefault();

        var episodes = (_subject.IsMovieSubject
                ? _subject.Episodes
                : _subject.Episodes.Where(episode =>
                    (episode.SeasonNumber ?? 1) == selectedSeason))
            .Select(CreateEpisodeItem)
            .ToArray();

        EpisodeList.ItemsSource = episodes;
        EpisodeCountText.Text = string.Create(
            CultureInfo.CurrentCulture,
            $"{episodes.Length} / {_subject.EpisodeCount}");
    }

    private EpisodeDisplayItemModel CreateEpisodeItem(
        CatalogEpisodeModel episode)
    {
        var number = episode.EpisodeNumber is { } value
            ? FormatEpisodeNumber(value)
            : "-";

        var history = PlaybackHistoryStore.Default.GetLatestEntry(
            new CatalogMediaItemModel?[]
            {
                episode.PrimaryItem,
            }.Concat(episode.AlternateItems));

        var progress = history?.ProgressPercent ?? 0d;
        var status = history is null ||
                     history.PositionSeconds <= 0d
            ? T("Category_Unwatched")
            : progress >= 95d
                ? L("已看完", "視聴済み", "Watched")
                : L(
                    $"已播放 {FormatPlaybackTime(history.PositionSeconds)}",
                    $"{FormatPlaybackTime(history.PositionSeconds)} まで視聴",
                    $"Played {FormatPlaybackTime(history.PositionSeconds)}");

        var duration = history is { DurationSeconds: > 0d }
            ? FormatPlaybackTime(history.DurationSeconds)
            : string.Empty;

        var thumbnailUrl =
            episode.PrimaryItem.Metadata?.EpisodeThumbnailUrl;

        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            thumbnailUrl =
                episode.PrimaryItem.Metadata?.BackdropUrl;
        }

        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            thumbnailUrl = _subject?.Metadata?.BackdropUrl;
        }

        return new EpisodeDisplayItemModel(
            number,
            episode.Title,
            episode.NativeTitle,
            duration,
            status,
            progress,
            episode.PrimaryItem,
            CreateRemoteImage(
                thumbnailUrl,
                360));
    }

    private static string FormatPlaybackTime(
        double totalSeconds)
    {
        var value = TimeSpan.FromSeconds(
            Math.Max(0d, totalSeconds));

        return value.TotalHours >= 1d
            ? value.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture)
            : value.ToString(@"m\:ss", CultureInfo.CurrentCulture);
    }

    private static string FormatEpisodeNumber(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.CurrentCulture)
            : value.ToString("0.##", CultureInfo.CurrentCulture);

    private async void CacheEpisodeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: EpisodeDisplayItemModel episode
            } button ||
            episode.MediaItem is not { } item ||
            !WebDavVideoCacheService.Default.CanCache(item))
        {
            return;
        }

        var episodeLabel =
            episode.Number == "-"
                ? episode.Title
                : L(
                    $"第 {episode.Number} 集",
                    $"第{episode.Number}話",
                    $"Episode {episode.Number}");

        button.IsEnabled = false;
        button.Content =
            new ProgressRing
            {
                Width = 18,
                Height = 18,
                IsActive = true
            };

        try
        {
            await VideoCacheDownloadManager.Default.StartAsync(
                item,
                episodeLabel);

            button.Content =
                new FontIcon
                {
                    Glyph = "\uE73E",
                    FontSize = 15
                };
        }
        catch
        {
            button.Content =
                new FontIcon
                {
                    Glyph = "\uE896",
                    FontSize = 15
                };
            button.IsEnabled = true;
        }
    }

    private void EpisodeList_ItemClick(
        object sender,
        ItemClickEventArgs e)
    {
        if (e.ClickedItem is EpisodeDisplayItemModel
            {
                MediaItem: { } item
            })
        {
            MediaPlayRequested?.Invoke(this, item);
        }
    }

    private void PlayButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_subject is not null)
        {
            var selectedSeason =
                SeasonComboBox.SelectedItem is SeasonOption option
                    ? option.Number
                    : _subject.SeasonNumbers.FirstOrDefault();

            var item = (_subject.IsMovieSubject
                    ? _subject.Episodes
                    : _subject.Episodes.Where(episode =>
                        (episode.SeasonNumber ?? 1) == selectedSeason))
                .Select(static episode => episode.PrimaryItem)
                .FirstOrDefault(static media =>
                    media.Location is not null);

            item ??= _subject.FirstPlayableItem;

            if (item is not null)
            {
                MediaPlayRequested?.Invoke(this, item);
                return;
            }
        }

        PlayRequested?.Invoke(this, "第18话");
    }

    private sealed record SeasonOption(
        int Number,
        string Label);
}
