using System.Collections.ObjectModel;
using System.Globalization;
using Eizo.Bangumi;
using Eizo.Localization;
using Eizo.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace Eizo.Views;

public sealed partial class BangumiAnimeBlogsView : UserControl
{
    private const int AnimeSubjectType = 2;
    private const int PageSize = 20;

    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiCommunityRepository _community =
        BangumiCommunityRepository.Default;
    private readonly BangumiAccountService _account =
        BangumiAccountService.Default;
    private readonly ObservableCollection<BangumiChannelBlogViewModel> _items = [];

    private CancellationTokenSource? _loadCancellation;
    private int _nextOffset;
    private int _total;

    public BangumiAnimeBlogsView()
    {
        InitializeComponent();
        BlogsList.ItemsSource = _items;
        ApplyText();

        Loaded += BangumiAnimeBlogsView_Loaded;
        Unloaded += BangumiAnimeBlogsView_Unloaded;
    }

    public event EventHandler<BangumiChannelBlog>? BlogRequested;

    private string T(string key) =>
        _localization.GetString(key);

    private void ApplyText()
    {
        PageTitle.Text = T("Nav_AnimeBlogs");
        PageSubtitle.Text = T("Bangumi_AnimeBlogsSubtitle");
        RefreshButton.Content = T("Bangumi_Refresh");
        OpenBangumiButton.Content = T("Bangumi_OpenOnBangumi");
        LoadMoreButton.Content = T("Bangumi_LoadMore");
    }

    private async void BangumiAnimeBlogsView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(
            append: false,
            forceRefresh: false);
    }

    private void BangumiAnimeBlogsView_Unloaded(
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
        await LoadAsync(
            append: false,
            forceRefresh: true);
    }

    private async void LoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(
            append: true,
            forceRefresh: false);
    }

    private async Task LoadAsync(
        bool append,
        bool forceRefresh)
    {
        _ = forceRefresh;

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        SetBusy(true);
        StatusText.Text = append
            ? T("Bangumi_LoadingMore")
            : T("Bangumi_Loading");
        LoadMoreButton.Visibility = Visibility.Collapsed;

        try
        {
            var offset = append
                ? _nextOffset
                : 0;

            var page =
                await _community.GetChannelBlogsAsync(
                    AnimeSubjectType,
                    offset,
                    PageSize,
                    _account.GetAccessTokenForRequest(),
                    cancellationToken);

            if (!append)
                _items.Clear();

            foreach (var blog in page.Items)
            {
                if (_items.Any(item =>
                        item.Blog.EntryId == blog.EntryId))
                {
                    continue;
                }

                _items.Add(CreateViewModel(blog));
            }

            _nextOffset =
                page.Offset +
                page.Items.Count;
            _total = page.Total;

            LoadMoreButton.Visibility =
                _nextOffset < page.Total
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            StatusText.Text =
                _items.Count == 0
                    ? T("Bangumi_AnimeBlogsEmpty")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_AnimeBlogsCountFormat"),
                        _items.Count,
                        _total);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!append)
                _items.Clear();

            StatusText.Text =
                T("Bangumi_CommunityUnavailable");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private BangumiChannelBlogViewModel CreateViewModel(
        BangumiChannelBlog blog)
    {
        var meta = new List<string>();

        if (blog.CreatedAt is { } createdAt)
        {
            meta.Add(
                createdAt.ToLocalTime().ToString(
                    "g",
                    CultureInfo.CurrentCulture));
        }

        meta.Add(
            string.Format(
                CultureInfo.CurrentCulture,
                T("Bangumi_CommunityRepliesFormat"),
                blog.ReplyCount));

        return new BangumiChannelBlogViewModel(
            blog,
            CreateArtwork(
                FirstNonEmpty(
                    blog.User.AvatarMedium,
                    blog.User.AvatarLarge,
                    blog.User.AvatarSmall)),
            FirstNonEmpty(
                blog.User.NickName,
                blog.User.UserName,
                T("Bangumi_UnknownUser")),
            blog.Title,
            BangumiCommunityText.ToPlainText(
                blog.Summary),
            string.Join(" · ", meta));
    }

    private void BlogItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: BangumiChannelBlog blog
            })
        {
            BlogRequested?.Invoke(this, blog);
        }
    }

    private async void OpenBangumiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(
            new Uri(
                "https://bgm.tv/anime/blog",
                UriKind.Absolute));
    }

    private void SetBusy(bool busy)
    {
        LoadingRing.IsActive = busy;
        LoadingRing.Visibility =
            busy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !busy;
        OpenBangumiButton.IsEnabled = !busy;
        LoadMoreButton.IsEnabled = !busy;
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
                DecodePixelWidth = 96,
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
