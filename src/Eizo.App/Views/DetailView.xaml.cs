using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.MetadataIntegration;
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

    public event EventHandler<CatalogMediaItemModel>? MediaPlayRequested;
    public event EventHandler<CatalogSubjectModel>? SubjectUpdated;

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
        RescrapeButton.Content =
            L("重新刮削", "メタデータを再取得", "Re-scrape");
        ManualMatchButton.Content =
            L("手动匹配…", "手動で照合…", "Manual match…");
        ClearManualMatchButton.Content =
            L("清除匹配", "手動照合を解除", "Clear match");
        EpisodesTitle.Text = _subject?.IsMovieSubject == true
            ? L("影片", "作品", "Films")
            : T("Media_Episodes");
        var isAnimation =
            _subject is null ||
            _subject.Category == MediaCategoryKind.Anime;
        CastTitle.Text = isAnimation
            ? L("角色与声优", "キャラクターと声優", "Characters & cast")
            : L("演员", "キャスト", "Cast");
        StaffTitle.Text = isAnimation
            ? L("制作人员", "スタッフ", "Staff")
            : L("主创与制作人员", "主要スタッフ", "Crew");
    }

    private void ApplySubject()
    {
        if (_subject is null)
        {
            return;
        }

        var presentation =
            CatalogSubjectPresentation.Create(_subject);

        TitleText.Text = presentation.Title;

        NativeTitleText.Text =
            presentation.SecondaryTitle;
        NativeTitleText.Visibility =
            string.IsNullOrWhiteSpace(
                presentation.SecondaryTitle)
                ? Visibility.Collapsed
                : Visibility.Visible;

        MetaText.Text =
            BuildMetadataSummary(presentation);
        MetaText.Visibility =
            string.IsNullOrWhiteSpace(MetaText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

        OverviewText.Text =
            presentation.Overview;
        var hasOverview =
            !string.IsNullOrWhiteSpace(
                presentation.Overview);
        OverviewText.Visibility =
            hasOverview
                ? Visibility.Visible
                : Visibility.Collapsed;
        OverviewCard.Visibility =
            hasOverview
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyPoster(presentation.PosterUrl);
        ApplyBackdrop(presentation.BackdropUrl);

        var sourceKind =
            (presentation.HasLocalSource,
             presentation.HasRemoteSource) switch
            {
                (true, true) =>
                    L("本地 + 网盘", "ローカル + リモート", "Local + remote"),
                (true, false) =>
                    L("本地", "ローカル", "Local"),
                (false, true) =>
                    L("网盘", "リモート", "Remote"),
                _ => string.Empty,
            };

        ReleaseStatText.Text =
            presentation.ReleaseYear?.ToString(
                CultureInfo.CurrentCulture) ??
            string.Empty;
        ReleaseStatBadge.Visibility =
            presentation.ReleaseYear is null
                ? Visibility.Collapsed
                : Visibility.Visible;

        EpisodeStatText.Text =
            presentation.IsMovie
                ? L(
                    $"{presentation.EpisodeCount} 部",
                    $"{presentation.EpisodeCount} 作品",
                    $"{presentation.EpisodeCount} films")
                : L(
                    $"{presentation.EpisodeCount} 集",
                    $"{presentation.EpisodeCount} 話",
                    $"{presentation.EpisodeCount} episodes");
        EpisodeStatBadge.Visibility =
            presentation.EpisodeCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        SourceStatText.Text =
            presentation.SourceCount > 0 &&
            !string.IsNullOrWhiteSpace(sourceKind)
                ? $"{sourceKind} · {presentation.SourceCount}"
                : string.Empty;
        SourceStatBadge.Visibility =
            string.IsNullOrWhiteSpace(SourceStatText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

        var hasPlayableItem =
            _subject.FirstPlayableItem is not null;
        PlayButton.IsEnabled = hasPlayableItem;
        PlayButton.Visibility =
            hasPlayableItem
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (_subject.IsMovieSubject)
        {
            SeasonComboBox.ItemsSource =
                Array.Empty<SeasonOption>();
            SeasonComboBox.SelectedIndex = -1;
            SeasonComboBox.Visibility =
                Visibility.Collapsed;
        }
        else
        {
            var seasons = _subject.SeasonNumbers
                .Select(season =>
                {
                    var seasonModel =
                        _subject.Series?.Seasons
                            .FirstOrDefault(item =>
                                item.Number == season);
                    var fallbackLabel =
                        season == 0
                            ? L(
                                "特别篇",
                                "スペシャル",
                                "Specials")
                            : $"Season {season}";

                    return new SeasonOption(
                        season,
                        string.IsNullOrWhiteSpace(
                            seasonModel?.Title)
                            ? fallbackLabel
                            : seasonModel!.Title!);
                })
                .ToArray();

            SeasonComboBox.ItemsSource = seasons;
            SeasonComboBox.SelectedIndex =
                seasons.Length > 0 ? 0 : -1;
            SeasonComboBox.Visibility =
                seasons.Length > 1
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        UpdateMetadataActionButtons();
        ApplySeasonContext();
        RebuildEpisodeList();
    }

    private async Task LoadCreditsAsync()
    {
        if (_subject is null)
            return;

        var metadata = _subject.Metadata;
        var preferProviderCredits =
            _subject.Category != MediaCategoryKind.Anime &&
            HasMetadataCredits(metadata);
        if (preferProviderCredits)
        {
            ApplyMetadataCredits(metadata!);
            return;
        }

        var subjectId = ResolveBangumiSubjectId();
        if (subjectId is null)
        {
            if (HasMetadataCredits(metadata))
            {
                ApplyMetadataCredits(metadata!);
                return;
            }

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
            if (HasMetadataCredits(metadata))
            {
                ApplyMetadataCredits(metadata!);
            }
            else
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
        }
        finally
        {
            CreditsLoadingRing.IsActive = false;
            CreditsLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private string BuildMetadataSummary(
        CatalogSubjectPresentation presentation)
    {
        var parts = new List<string>();

        if (presentation.Genres.Count > 0)
        {
            parts.Add(
                string.Join(
                    " / ",
                    presentation.Genres.Take(4)));
        }

        if (presentation.RuntimeMinutes is > 0)
        {
            parts.Add(
                L(
                    $"{presentation.RuntimeMinutes} 分钟",
                    $"{presentation.RuntimeMinutes}分",
                    $"{presentation.RuntimeMinutes} min"));
        }

        if (presentation.OriginCountryCodes.Count > 0)
        {
            parts.Add(
                string.Join(
                    " / ",
                    presentation.OriginCountryCodes.Take(3)));
        }

        if (presentation.ProductionCompanies.Count > 0)
        {
            parts.Add(
                string.Join(
                    " / ",
                    presentation.ProductionCompanies.Take(2)));
        }

        if (!string.IsNullOrWhiteSpace(
                presentation.ProductionStatus))
        {
            parts.Add(
                presentation.ProductionStatus!);
        }

        return string.Join(
            " · ",
            parts.Where(static value =>
                !string.IsNullOrWhiteSpace(value)));
    }

    private static bool HasMetadataCredits(
        MediaMetadataSnapshot? metadata) =>
        metadata is not null &&
        (metadata.Cast.Count > 0 ||
         metadata.Crew.Count > 0);

    private void ApplyMetadataCredits(
        MediaMetadataSnapshot metadata)
    {
        var cast = metadata.Cast
            .OrderBy(static item => item.Order)
            .ThenBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .Select(item =>
                new CharacterCreditViewModel(
                    item.Name,
                    L("演员", "キャスト", "Cast"),
                    string.IsNullOrWhiteSpace(item.Role)
                        ? L("角色未收录", "役名未登録", "Role not listed")
                        : item.Role!,
                    CreateRemoteImage(
                        item.ProfileUrl,
                        160)))
            .ToArray();

        var crew = metadata.Crew
            .Where(static item =>
                !string.IsNullOrWhiteSpace(item.Role))
            .OrderBy(static item =>
                MetadataCrewPriority(
                    item.Role,
                    item.Department))
            .ThenBy(static item => item.Order)
            .ThenBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(12)
            .Select(static item =>
                new StaffCreditViewModel(
                    item.Role!,
                    item.Name))
            .ToArray();

        CharactersList.ItemsSource = cast;
        StaffList.ItemsSource = crew;

        CreditsStatusText.Visibility =
            cast.Length == 0 &&
            crew.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        if (cast.Length == 0 &&
            crew.Length == 0)
        {
            CreditsStatusText.Text =
                L(
                    "暂无演职人员信息。",
                    "キャスト・スタッフ情報はありません。",
                    "No cast or staff information.");
        }
    }

    private static int MetadataCrewPriority(
        string? role,
        string? department)
    {
        var value = $"{department} {role}";
        if (value.Contains(
                "Director",
                StringComparison.OrdinalIgnoreCase))
            return 0;
        if (value.Contains(
                "Writer",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "Screenplay",
                StringComparison.OrdinalIgnoreCase))
            return 1;
        if (value.Contains(
                "Creator",
                StringComparison.OrdinalIgnoreCase))
            return 2;
        if (value.Contains(
                "Producer",
                StringComparison.OrdinalIgnoreCase))
            return 3;
        if (value.Contains(
                "Music",
                StringComparison.OrdinalIgnoreCase))
            return 4;
        if (value.Contains(
                "Camera",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains(
                "Photography",
                StringComparison.OrdinalIgnoreCase))
            return 5;
        return 20;
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

    private async void RescrapeButton_Click(
        object sender,
        RoutedEventArgs e) =>
        await RefreshSubjectMetadataAsync();

    private async void ManualMatchButton_Click(
        object sender,
        RoutedEventArgs e) =>
        await ShowManualMatchDialogAsync();

    private async void ClearManualMatchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_subject is null)
            return;

        var binding =
            CatalogSubjectMetadataActions.GetBinding(
                _subject);
        if (binding is not
            {
                IsManual: true,
                PrimaryProvider.Length: > 0,
            })
        {
            return;
        }

        await ClearManualMatchAsync(
            binding.PrimaryProvider);
    }

    private void UpdateMetadataActionButtons()
    {
        if (_subject is null)
            return;

        var binding =
            CatalogSubjectMetadataActions.GetBinding(
                _subject);
        ClearManualMatchButton.Visibility =
            binding is
            {
                IsManual: true,
                PrimaryProvider.Length: > 0,
            }
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void SetMetadataActionButtonsEnabled(
        bool enabled)
    {
        RescrapeButton.IsEnabled = enabled;
        ManualMatchButton.IsEnabled = enabled;
        ClearManualMatchButton.IsEnabled = enabled;
    }

    private async Task ShowManualMatchDialogAsync()
    {
        if (_subject is null ||
            XamlRoot is null)
        {
            return;
        }

        var candidate =
            await MetadataMatchDialog.ShowAsync(
                XamlRoot,
                _subject);
        if (candidate is null)
            return;

        SetMetadataActionButtonsEnabled(false);
        ShowMetadataActionStatus(
            L("正在应用手动匹配…", "手動照合を適用しています…", "Applying manual match…"),
            InfoBarSeverity.Informational);

        try
        {
            var refreshed =
                await CatalogSubjectMetadataActions
                    .ApplyManualMatchAsync(
                        _subject,
                        candidate);
            if (refreshed is null)
            {
                ShowMetadataActionStatus(
                    L("匹配已保存，但未找到更新后的作品。", "照合は保存されましたが、更新後の作品を確認できませんでした。", "The match was saved, but the updated title could not be found."),
                    InfoBarSeverity.Warning);
                return;
            }

            ShowMetadataActionStatus(
                L("手动匹配已应用。", "手動照合を適用しました。", "Manual match applied."),
                InfoBarSeverity.Success);
            PublishRefreshedSubject(refreshed);
        }
        catch
        {
            ShowMetadataActionStatus(
                L("应用手动匹配失败，请稍后重试。", "手動照合の適用に失敗しました。後でもう一度お試しください。", "Could not apply the manual match. Try again later."),
                InfoBarSeverity.Error);
        }
        finally
        {
            SetMetadataActionButtonsEnabled(true);
        }
    }

    private async Task RefreshSubjectMetadataAsync()
    {
        if (_subject is null)
            return;

        SetMetadataActionButtonsEnabled(false);
        ShowMetadataActionStatus(
            L("正在重新刮削此作品…", "この作品のメタデータを再取得しています…", "Refreshing this title…"),
            InfoBarSeverity.Informational);

        try
        {
            var refreshed =
                await CatalogSubjectMetadataActions
                    .RefreshAsync(_subject);
            if (refreshed is null)
            {
                ShowMetadataActionStatus(
                    L("重新刮削已完成，但未找到更新后的作品。", "再取得は完了しましたが、更新後の作品を確認できませんでした。", "Refresh finished, but the updated title could not be found."),
                    InfoBarSeverity.Warning);
                return;
            }

            ShowMetadataActionStatus(
                L("作品信息已更新。", "作品情報を更新しました。", "Title metadata updated."),
                InfoBarSeverity.Success);
            PublishRefreshedSubject(refreshed);
        }
        catch
        {
            ShowMetadataActionStatus(
                L("重新刮削失败，请稍后重试。", "メタデータの再取得に失敗しました。後でもう一度お試しください。", "Metadata refresh failed. Try again later."),
                InfoBarSeverity.Error);
        }
        finally
        {
            SetMetadataActionButtonsEnabled(true);
        }
    }

    private async Task ClearManualMatchAsync(
        string provider)
    {
        if (_subject is null)
            return;

        SetMetadataActionButtonsEnabled(false);
        ShowMetadataActionStatus(
            L("正在清除手动匹配…", "手動照合を解除しています…", "Clearing manual match…"),
            InfoBarSeverity.Informational);

        try
        {
            var refreshed =
                await CatalogSubjectMetadataActions
                    .ClearManualMatchAsync(
                        _subject,
                        provider);
            if (refreshed is null)
            {
                ShowMetadataActionStatus(
                    L("手动匹配已清除，但未找到更新后的作品。", "手動照合は解除されましたが、更新後の作品を確認できませんでした。", "The manual match was cleared, but the updated title could not be found."),
                    InfoBarSeverity.Warning);
                return;
            }

            ShowMetadataActionStatus(
                L("手动匹配已清除。", "手動照合を解除しました。", "Manual match cleared."),
                InfoBarSeverity.Success);
            PublishRefreshedSubject(refreshed);
        }
        catch
        {
            ShowMetadataActionStatus(
                L("清除手动匹配失败，请稍后重试。", "手動照合の解除に失敗しました。後でもう一度お試しください。", "Could not clear the manual match. Try again later."),
                InfoBarSeverity.Error);
        }
        finally
        {
            SetMetadataActionButtonsEnabled(true);
        }
    }

    private void ShowMetadataActionStatus(
        string message,
        InfoBarSeverity severity)
    {
        MetadataActionStatusBar.Message = message;
        MetadataActionStatusBar.Severity = severity;
        MetadataActionStatusBar.IsOpen = true;
    }

    private void PublishRefreshedSubject(
        CatalogSubjectModel? refreshed)
    {
        if (refreshed is not null)
        {
            SubjectUpdated?.Invoke(
                this,
                refreshed);
        }
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
        SelectionChangedEventArgs e)
    {
        ApplySeasonContext();
        RebuildEpisodeList();
    }

    private void ApplySeasonContext()
    {
        if (_subject is null)
            return;

        var subjectPoster =
            _subject.Metadata?.PosterUrl;
        if (SeasonComboBox.SelectedItem is not
            SeasonOption option)
        {
            ApplyPoster(subjectPoster);
            ToolTipService.SetToolTip(
                SeasonComboBox,
                null);
            return;
        }

        var season = _subject.Series?.Seasons
            .FirstOrDefault(item =>
                item.Number == option.Number);
        ApplyPoster(
            string.IsNullOrWhiteSpace(
                season?.PosterUrl)
                ? subjectPoster
                : season!.PosterUrl);

        var seasonContext = new List<string>();
        if (!string.IsNullOrWhiteSpace(
                season?.AirDate))
        {
            seasonContext.Add(season!.AirDate!);
        }
        if (!string.IsNullOrWhiteSpace(
                season?.Overview))
        {
            seasonContext.Add(season!.Overview!);
        }

        ToolTipService.SetToolTip(
            SeasonComboBox,
            seasonContext.Count == 0
                ? null
                : string.Join(
                    Environment.NewLine,
                    seasonContext));
    }

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

        var hasEpisodes = episodes.Length > 0;
        EpisodeList.Visibility =
            hasEpisodes
                ? Visibility.Visible
                : Visibility.Collapsed;
        EpisodeEmptyStateText.Text =
            L(
                "当前季没有可播放的媒体条目。",
                "現在のシーズンには再生できるメディア項目がありません。",
                "No playable media items are available for this season.");
        EpisodeEmptyStateText.Visibility =
            hasEpisodes
                ? Visibility.Collapsed
                : Visibility.Visible;
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
                360))
        {
            AirDate =
                episode.PrimaryItem.Metadata?
                    .EpisodeAirDate ?? string.Empty,
        };
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
