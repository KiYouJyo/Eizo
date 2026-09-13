using System.Collections.ObjectModel;
using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Views;

public sealed partial class BangumiFollowingView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiAccountService _account =
        BangumiAccountService.Default;
    private readonly ObservableCollection<BangumiCardViewModel> _items = [];
    private CancellationTokenSource? _loadCancellation;
    private int _nextOffset;

    public BangumiFollowingView()
    {
        InitializeComponent();

        ResultsList.ItemsSource = _items;
        ApplyText();
        ApplyAccountState();

        Loaded += BangumiFollowingView_Loaded;
        Unloaded += BangumiFollowingView_Unloaded;
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
        PageTitle.Text = T("Nav_MyFollowing");
        PageSubtitle.Text = T("Bangumi_FollowingSubtitle");
        RefreshButtonText.Text = T("Bangumi_Refresh");
        ConnectButton.Content = T("Bangumi_Connect");
        DisconnectButton.Content = T("Bangumi_Disconnect");
        LoadMoreButton.Content = T("Bangumi_LoadMore");
    }

    private async void BangumiFollowingView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _account.Changed -= BangumiAccount_Changed;
        _account.Changed += BangumiAccount_Changed;

        await LoadAsync(
            forceProfileRefresh: false,
            append: false);
    }

    private void BangumiFollowingView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _account.Changed -= BangumiAccount_Changed;
    }

    private async void BangumiAccount_Changed(
        object? sender,
        EventArgs e)
    {
        ApplyAccountState();
        await LoadAsync(
            forceProfileRefresh: true,
            append: false);
    }

    private async void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (XamlRoot is null)
            return;

        if (await BangumiAccountDialogService.ShowConnectAsync(
                XamlRoot))
        {
            ApplyAccountState();
        }
    }

    private async void DisconnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (XamlRoot is null)
            return;

        await BangumiAccountDialogService.ConfirmDisconnectAsync(
            XamlRoot);
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(
            forceProfileRefresh: true,
            append: false);
    }

    private async void LoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(
            forceProfileRefresh: false,
            append: true);
    }

    private async Task LoadAsync(
        bool forceProfileRefresh,
        bool append)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        SetBusy(true);

        try
        {
            var profile =
                await _account.GetProfileAsync(
                    forceProfileRefresh,
                    cancellationToken);

            if (profile is null)
            {
                _items.Clear();
                _nextOffset = 0;
                ApplyDisconnectedState();
                return;
            }

            ApplyConnectedProfile(profile);
            var page =
                await _account.GetFollowingAsync(
                    append ? _nextOffset : 0,
                    cancellationToken);

            if (page is null)
            {
                _items.Clear();
                _nextOffset = 0;
                ApplyDisconnectedState();
                return;
            }

            if (append)
                AppendItems(page.Items);
            else
                SetItems(page.Items);

            _nextOffset =
                page.Offset +
                page.Items.Count;

            LoadMoreButton.Visibility =
                page.HasMore
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            StatusText.Text = _items.Count == 0
                ? T("Bangumi_FollowingEmpty")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_FollowingCountFormat"),
                    _items.Count,
                    page.Total);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!append)
                _items.Clear();

            StatusText.Text =
                T("Bangumi_AccountNetworkError");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyAccountState()
    {
        if (_account.CachedProfile is { } profile)
        {
            ApplyConnectedProfile(profile);
            return;
        }

        if (_account.IsConnected)
        {
            AccountTitleText.Text =
                T("Bangumi_AccountChecking");
            AccountSubtitleText.Text =
                T("Bangumi_AccountCheckingDescription");
            ConnectButton.Visibility =
                Visibility.Collapsed;
            DisconnectButton.Visibility =
                Visibility.Visible;
            RefreshButton.Visibility =
                Visibility.Visible;
            return;
        }

        ApplyDisconnectedState();
    }

    private void ApplyDisconnectedState()
    {
        AvatarImage.Source = null;
        AccountTitleText.Text =
            T("Bangumi_AccountNotConnected");
        AccountSubtitleText.Text =
            T("Bangumi_AccountNotConnectedDescription");
        ConnectButton.Visibility =
            Visibility.Visible;
        DisconnectButton.Visibility =
            Visibility.Collapsed;
        RefreshButton.Visibility =
            Visibility.Collapsed;
        LoadMoreButton.Visibility =
            Visibility.Collapsed;
        StatusText.Text =
            T("Bangumi_FollowingConnectPrompt");
    }

    private void ApplyConnectedProfile(
        BangumiUserProfile profile)
    {
        AccountTitleText.Text =
            string.IsNullOrWhiteSpace(profile.NickName)
                ? profile.UserName
                : profile.NickName;
        AccountSubtitleText.Text =
            "@" + profile.UserName;
        AvatarImage.Source =
            CreateArtwork(
                FirstNonEmpty(
                    profile.AvatarMedium,
                    profile.AvatarLarge,
                    profile.AvatarSmall));
        ConnectButton.Visibility =
            Visibility.Collapsed;
        DisconnectButton.Visibility =
            Visibility.Visible;
        RefreshButton.Visibility =
            Visibility.Visible;
    }

    private void SetBusy(bool busy)
    {
        LoadingRing.IsActive = busy;
        LoadingRing.Visibility =
            busy
                ? Visibility.Visible
                : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;
        ConnectButton.IsEnabled = !busy;
        DisconnectButton.IsEnabled = !busy;
        LoadMoreButton.IsEnabled = !busy;
    }

    private void SetItems(
        IReadOnlyList<BangumiUserCollectionItem> items)
    {
        _items.Clear();
        AppendItems(items);
    }

    private void AppendItems(
        IReadOnlyList<BangumiUserCollectionItem> items)
    {
        var existingIds = _items
            .Select(static item => item.Subject.Id)
            .ToHashSet();

        foreach (var item in items)
        {
            if (!existingIds.Add(item.Subject.Id))
                continue;

            _items.Add(CreateViewModel(item));
        }
    }

    private BangumiCardViewModel CreateViewModel(
        BangumiUserCollectionItem collection)
    {
        var subject = collection.Subject;
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

        var progress = subject.EpisodeCount > 0
            ? L(
                $"{collection.EpisodeStatus} / {subject.EpisodeCount} 集",
                $"{collection.EpisodeStatus} / {subject.EpisodeCount} 話",
                $"{collection.EpisodeStatus} / {subject.EpisodeCount} eps")
            : L(
                $"已看 {collection.EpisodeStatus} 集",
                $"{collection.EpisodeStatus} 話視聴",
                $"{collection.EpisodeStatus} eps watched");

        if (collection.Rate > 0)
        {
            progress +=
                L(
                    $" · 我的评分 {collection.Rate}",
                    $" · 評価 {collection.Rate}",
                    $" · My score {collection.Rate}");
        }

        return new BangumiCardViewModel(
            subject,
            CreateArtwork(subject.PosterUrl),
            title,
            subtitle,
            string.Join(" · ", meta),
            progress,
            T("Bangumi_Watching"));
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
                DecodePixelWidth = 360,
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
