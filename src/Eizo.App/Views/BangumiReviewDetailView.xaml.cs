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

public sealed partial class BangumiReviewDetailView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiCommunityRepository _community =
        BangumiCommunityRepository.Default;
    private readonly BangumiAccountService _account =
        BangumiAccountService.Default;
    private readonly ObservableCollection<BangumiReplyViewModel> _comments = [];
    private readonly BangumiSubjectReview _review;
    private readonly BangumiSubjectCard _subject;
    private CancellationTokenSource? _loadCancellation;

    public BangumiReviewDetailView(
        BangumiSubjectReview review,
        BangumiSubjectCard subject)
    {
        _review = review;
        _subject = subject;

        InitializeComponent();
        CommentsList.ItemsSource = _comments;

        PageTitle.Text = T("Bangumi_ReviewDetailPageTitle");
        OpenSubjectButton.Content = T("Bangumi_BackToSubject");
        OpenBangumiButton.Content = T("Bangumi_OpenOnBangumi");
        CommentsTitle.Text = T("Bangumi_ReviewComments");
        TitleText.Text = review.Title;
        AuthorText.Text = FirstNonEmpty(
            review.User.NickName,
            review.User.UserName);
        AuthorAvatar.Source = CreateArtwork(
            FirstNonEmpty(
                review.User.AvatarMedium,
                review.User.AvatarLarge,
                review.User.AvatarSmall),
            96);

        Loaded += BangumiReviewDetailView_Loaded;
        Unloaded += BangumiReviewDetailView_Unloaded;
    }

    public event EventHandler<BangumiSubjectCard>? SubjectRequested;

    private string T(string key) =>
        _localization.GetString(key);

    private async void BangumiReviewDetailView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private void BangumiReviewDetailView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
    }

    private async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        StatusText.Text = T("Bangumi_CommunityLoading");

        try
        {
            var accessToken = _account.GetAccessTokenForRequest();
            var detailTask = _community.GetBlogEntryAsync(
                _review.EntryId,
                accessToken,
                cancellationToken);
            var commentsTask = _community.GetBlogCommentsAsync(
                _review.EntryId,
                accessToken,
                cancellationToken);

            await Task.WhenAll(detailTask, commentsTask);

            var detail = detailTask.Result;
            TitleText.Text = detail.Title;
            AuthorText.Text = FirstNonEmpty(
                detail.User.NickName,
                detail.User.UserName);
            AuthorAvatar.Source = CreateArtwork(
                FirstNonEmpty(
                    detail.User.AvatarMedium,
                    detail.User.AvatarLarge,
                    detail.User.AvatarSmall),
                96);
            ContentText.Text =
                BangumiCommunityText.ToPlainText(
                    detail.Content);

            var meta = new List<string>();
            if (detail.CreatedAt is { } createdAt)
                meta.Add(FormatTime(createdAt));
            if (detail.ViewCount > 0)
            {
                meta.Add(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityViewsFormat"),
                        detail.ViewCount));
            }
            meta.Add(
                string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CommunityRepliesFormat"),
                    detail.ReplyCount));
            MetaText.Text = string.Join(" · ", meta);

            TagsText.Text = detail.Tags.Count > 0
                ? string.Join(" · ", detail.Tags)
                : string.Empty;

            _comments.Clear();
            foreach (var reply in FlattenReplies(commentsTask.Result))
                _comments.Add(reply);

            CommentsStatusText.Text =
                _comments.Count == 0
                    ? T("Bangumi_ReviewNoComments")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityLoadedCountFormat"),
                        _comments.Count);
            StatusText.Text = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            StatusText.Text =
                T("Bangumi_CommunityUnavailable");
            CommentsStatusText.Text =
                T("Bangumi_CommunityUnavailable");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private IEnumerable<BangumiReplyViewModel> FlattenReplies(
        IReadOnlyList<BangumiCommunityReply> replies)
    {
        foreach (var reply in replies)
        {
            yield return CreateReplyViewModel(reply, nested: false);
            foreach (var nested in reply.Replies)
                yield return CreateReplyViewModel(nested, nested: true);
        }
    }

    private BangumiReplyViewModel CreateReplyViewModel(
        BangumiCommunityReply reply,
        bool nested)
    {
        var meta = reply.CreatedAt is { } createdAt
            ? FormatTime(createdAt)
            : string.Empty;

        return new BangumiReplyViewModel(
            reply,
            CreateArtwork(
                FirstNonEmpty(
                    reply.User.AvatarMedium,
                    reply.User.AvatarLarge,
                    reply.User.AvatarSmall),
                76),
            FirstNonEmpty(
                reply.User.NickName,
                reply.User.UserName,
                T("Bangumi_UnknownUser")),
            BangumiCommunityText.ToPlainText(reply.Content),
            meta,
            reply.ReactionCount > 0
                ? $"♥ {reply.ReactionCount}"
                : string.Empty,
            nested ? "32,0,0,0" : "0");
    }

    private void OpenSubjectButton_Click(
        object sender,
        RoutedEventArgs e) =>
        SubjectRequested?.Invoke(this, _subject);

    private async void OpenBangumiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(
            new Uri(
                $"https://bgm.tv/blog/{_review.EntryId}",
                UriKind.Absolute));
    }

    private static string FormatTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString(
            "g",
            CultureInfo.CurrentCulture);

    private static BitmapImage? CreateArtwork(
        string? url,
        int decodeWidth)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
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
                DecodePixelWidth = decodeWidth,
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
