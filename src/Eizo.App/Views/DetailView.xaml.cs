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
        InfoText.Text = T("Catalog_Unparsed");
        ExternalIdsText.Text = "-";
    }

    public DetailView(CatalogSubjectModel subject)
    {
        _subject = subject ?? throw new ArgumentNullException(nameof(subject));

        InitializeComponent();
        ApplyText();
        ApplySubject();
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

        InfoText.Text = string.Join(
            Environment.NewLine,
            [
                $"{L("数据来源", "データ提供元", "Provider")}: {provider}",
                $"{L("作品 ID", "作品 ID", "Subject ID")}: {subjectId}",
                $"{L("发布日期", "公開日", "Release date")}: {release}",
                _subject.IsMovieSubject
                    ? $"{L("本地影片", "ローカル作品", "Local films")}: {_subject.EpisodeCount}"
                    : $"{L("本地集数", "ローカル話数", "Local episodes")}: {_subject.EpisodeCount}",
                $"{L("媒体来源", "メディアソース", "Media sources")}: {sourceCount}",
                $"{L("聚合依据", "グループ基準", "Grouping basis")}: {_subject.GroupingBasis}",
            ]);

        ExternalIdsText.Text = metadata?.ExternalIds.Count > 0
            ? string.Join(
                Environment.NewLine,
                metadata.ExternalIds
                    .OrderBy(static pair => pair.Key)
                    .Select(static pair => $"{pair.Key}: {pair.Value}"))
            : "-";

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

        var sourceStatus = episode.SourceCount > 1
            ? L(
                $"{episode.SourceCount} 个版本",
                $"{episode.SourceCount} バージョン",
                $"{episode.SourceCount} versions")
            : episode.PrimaryItem.Location?.Kind switch
            {
                MediaLocationKind.RemoteUri =>
                    L("网盘", "リモート", "Remote"),
                MediaLocationKind.LocalFile =>
                    L("本地", "ローカル", "Local"),
                _ => string.Empty,
            };

        return new EpisodeDisplayItemModel(
            number,
            episode.Title,
            episode.NativeTitle,
            string.Empty,
            sourceStatus,
            0,
            episode.PrimaryItem,
            CreateRemoteImage(
                episode.PrimaryItem.Metadata?.EpisodeThumbnailUrl,
                320));
    }

    private static string FormatEpisodeNumber(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.CurrentCulture)
            : value.ToString("0.##", CultureInfo.CurrentCulture);

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
        if (_subject?.FirstPlayableItem is { } item)
        {
            MediaPlayRequested?.Invoke(this, item);
            return;
        }

        PlayRequested?.Invoke(this, "第18话");
    }

    private sealed record SeasonOption(
        int Number,
        string Label);
}
