using System.Globalization;
using Eizo.Bangumi;
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
    private readonly BangumiRepository _bangumi =
        BangumiRepository.Default;
    private CancellationTokenSource? _creditsLoadCancellation;

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
        CreditsStatusText.Text =
            L("暂无演职人员信息。", "キャスト・スタッフ情報はありません。", "No cast or staff information.");
        CreditsStatusText.Visibility = Visibility.Visible;
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
        _ = LoadCreditsAsync();
    }

    private void DetailView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        PlaybackHistoryStore.Default.Changed -=
            PlaybackHistoryStore_Changed;

        _creditsLoadCancellation?.Cancel();
        _creditsLoadCancellation?.Dispose();
        _creditsLoadCancellation = null;
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
        CastTitle.Text = L("角色与声优", "キャラクターと声優", "Characters & cast");
        StaffTitle.Text = L("制作人员", "スタッフ", "Staff");
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

    private async Task LoadCreditsAsync()
    {
        if (_subject is null)
            return;

        var subjectId = ResolveBangumiSubjectId();
        if (subjectId is null)
        {
            CharactersList.ItemsSource = null;
            StaffList.ItemsSource = null;
            CreditsStatusText.Text =
                L(
                    "暂无可用的演职人员数据。",
                    "利用可能なキャスト・スタッフ情報がありません。",
                    "No cast or staff data is available.");
            CreditsStatusText.Visibility = Visibility.Visible;
            return;
        }

        _creditsLoadCancellation?.Cancel();
        _creditsLoadCancellation?.Dispose();
        _creditsLoadCancellation =
            new CancellationTokenSource();
        var token = _creditsLoadCancellation.Token;

        CreditsLoadingRing.IsActive = true;
        CreditsLoadingRing.Visibility = Visibility.Visible;
        CreditsStatusText.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _bangumi.GetSubjectCreditsAsync(
                subjectId.Value,
                forceRefresh: false,
                token);

            var characters = result.Value.Characters
                .OrderBy(static item => CharacterPriority(item.Relation))
                .ThenBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(item =>
                    new CharacterCreditViewModel(
                        item.Name,
                        string.IsNullOrWhiteSpace(item.Relation)
                            ? L("角色", "キャラクター", "Character")
                            : item.Relation,
                        item.Actors.Count > 0
                            ? string.Join(
                                " / ",
                                item.Actors
                                    .Select(static actor => actor.Name)
                                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                                    .Take(2))
                            : L("声优未收录", "声優未登録", "No cast listed"),
                        CreateRemoteImage(item.ImageUrl, 160)))
                .ToArray();

            var staff = result.Value.Staff
                .Where(static item => !string.IsNullOrWhiteSpace(item.Relation))
                .OrderBy(static item => StaffPriority(item.Relation))
                .ThenBy(static item => item.Relation, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(12)
                .Select(static item =>
                    new StaffCreditViewModel(
                        item.Relation,
                        item.Name))
                .ToArray();

            CharactersList.ItemsSource = characters;
            StaffList.ItemsSource = staff;

            if (characters.Length == 0 && staff.Length == 0)
            {
                CreditsStatusText.Text =
                    L(
                        "暂无演职人员信息。",
                        "キャスト・スタッフ情報はありません。",
                        "No cast or staff information.");
                CreditsStatusText.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            CharactersList.ItemsSource = null;
            StaffList.ItemsSource = null;
            CreditsStatusText.Text =
                L(
                    "演职人员信息暂时无法加载。",
                    "キャスト・スタッフ情報を読み込めません。",
                    "Cast and staff information could not be loaded.");
            CreditsStatusText.Visibility = Visibility.Visible;
        }
        finally
        {
            CreditsLoadingRing.IsActive = false;
            CreditsLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private int? ResolveBangumiSubjectId()
    {
        var metadata = _subject?.Metadata;
        if (metadata is null)
            return null;

        string? value = null;
        if (string.Equals(
                metadata.Provider,
                "bangumi",
                StringComparison.OrdinalIgnoreCase))
        {
            value = metadata.ProviderSubjectId;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            value = metadata.ExternalIds
                .FirstOrDefault(pair =>
                    string.Equals(
                        pair.Key,
                        "bangumi",
                        StringComparison.OrdinalIgnoreCase))
                .Value;
        }

        return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var subjectId) &&
               subjectId > 0
            ? subjectId
            : null;
    }

    private static int CharacterPriority(string? relation) =>
        relation?.Trim() switch
        {
            "主角" => 0,
            "配角" => 1,
            "客串" => 2,
            _ => 9,
        };

    private static int StaffPriority(string? relation)
    {
        var value = relation?.Trim() ?? string.Empty;
        if (value.Contains("原作", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (value.Contains("监督", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("監督", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("导演", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (value.Contains("系列构成", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("シリーズ構成", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (value.Contains("脚本", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("脚本", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (value.Contains("人物设定", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("キャラクターデザイン", StringComparison.OrdinalIgnoreCase))
            return 4;
        if (value.Contains("音乐", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("音楽", StringComparison.OrdinalIgnoreCase))
            return 5;
        if (value.Contains("动画制作", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("アニメーション制作", StringComparison.OrdinalIgnoreCase))
            return 6;
        return 20;
    }

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
            var itemBackdrop =
                episode.PrimaryItem.Metadata?.BackdropUrl;
            var subjectBackdrop =
                _subject?.Metadata?.BackdropUrl;

            if (!string.IsNullOrWhiteSpace(itemBackdrop) &&
                !string.Equals(
                    itemBackdrop,
                    subjectBackdrop,
                    StringComparison.OrdinalIgnoreCase))
            {
                thumbnailUrl = itemBackdrop;
            }
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

    private sealed record CharacterCreditViewModel(
        string Name,
        string Relation,
        string ActorText,
        BitmapImage? Image);

    private sealed record StaffCreditViewModel(
        string Relation,
        string Name);

    private sealed record SeasonOption(
        int Number,
        string Label);
}
