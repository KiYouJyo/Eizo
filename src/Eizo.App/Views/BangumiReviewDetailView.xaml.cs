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
    private readonly BangumiSubjectCard? _subject;
    private CancellationTokenSource? _loadCancellation;
    private int _replyToCommentId;

    public BangumiReviewDetailView(
        BangumiSubjectReview review,
        BangumiSubjectCard? subject = null)
    {
        _review = review;
        _subject = subject;

        InitializeComponent();
        CommentsList.ItemsSource = _comments;

        PageTitle.Text = T("Bangumi_ReviewDetailPageTitle");
        OpenSubjectButton.Content = T("Bangumi_BackToSubject");
        OpenSubjectButton.Visibility =
            subject is null
                ? Visibility.Collapsed
                : Visibility.Visible;
        OpenBangumiButton.Content = T("Bangumi_OpenOnBangumi");
        CommentsTitle.Text = T("Bangumi_ReviewComments");
        ReplyButton.Content = T("Bangumi_Publish");
        ReplyInputBox.PlaceholderText =
            T("Bangumi_WriteReplyPlaceholder");
        ReplyInputBox.IsEnabled =
            _account.IsConnected;
        UpdateReplyComposerState();
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
            ReplyInputBox.IsEnabled =
                _account.IsConnected;
            UpdateReplyComposerState();

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
            RenderContent(detail.Content);

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
            CanReact: false,
            IsReacted: false,
            Indent: nested ? new Thickness(32, 0, 0, 0) : new Thickness(0));
    }

    private async void ReplyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var token = _account.GetAccessTokenForRequest();
        if (string.IsNullOrWhiteSpace(token))
        {
            CommentsStatusText.Text =
                T("Bangumi_CommunitySignInToInteract");
            return;
        }

        var content = ReplyInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return;

        var turnstile =
            await BangumiTurnstileDialogService.AcquireAsync(
                XamlRoot);
        if (string.IsNullOrWhiteSpace(turnstile))
        {
            CommentsStatusText.Text =
                T("Bangumi_TurnstileUnavailable");
            return;
        }

        ReplyButton.IsEnabled = false;
        try
        {
            await _community.CreateBlogCommentAsync(
                _review.EntryId,
                content,
                replyTo: _replyToCommentId,
                turnstile,
                token,
                _loadCancellation?.Token ??
                CancellationToken.None);

            var comments =
                await _community.GetBlogCommentsAsync(
                    _review.EntryId,
                    token,
                    _loadCancellation?.Token ??
                    CancellationToken.None);

            ReplyInputBox.Text = string.Empty;
            ClearReplyTarget();
            _comments.Clear();
            foreach (var reply in FlattenReplies(comments))
                _comments.Add(reply);

            CommentsStatusText.Text =
                string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CommunityLoadedCountFormat"),
                    _comments.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            CommentsStatusText.Text =
                T("Bangumi_CommunityWriteFailed");
        }
        finally
        {
            UpdateReplyComposerState();
        }
    }

    private void CommentReplyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button
            {
                Tag: BangumiReplyViewModel item
            })
        {
            return;
        }

        _replyToCommentId = item.Reply.Id;
        ReplyTargetText.Text = string.Format(
            CultureInfo.CurrentCulture,
            T("Bangumi_ReplyingToFormat"),
            item.UserName);
        ReplyTargetPanel.Visibility = Visibility.Visible;
        ReplyInputBox.Focus(FocusState.Programmatic);
    }

    private void CancelReplyTargetButton_Click(
        object sender,
        RoutedEventArgs e) =>
        ClearReplyTarget();

    private void ClearReplyTarget()
    {
        _replyToCommentId = 0;
        ReplyTargetText.Text = string.Empty;
        ReplyTargetPanel.Visibility = Visibility.Collapsed;
    }

    private void ReplyInputBox_TextChanged(
        object sender,
        TextChangedEventArgs e) =>
        UpdateReplyComposerState();

    private void UpdateReplyComposerState()
    {
        ReplyButton.IsEnabled =
            _account.IsConnected &&
            !string.IsNullOrWhiteSpace(
                ReplyInputBox.Text);
    }

    private void OpenSubjectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_subject is not null)
            SubjectRequested?.Invoke(this, _subject);
    }

    private async void OpenBangumiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(
            new Uri(
                $"https://bgm.tv/blog/{_review.EntryId}",
                UriKind.Absolute));
    }

    private void RenderContent(string? content)
    {
        ContentBlocksPanel.Children.Clear();

        var blocks =
            BangumiCommunityText.ParseContentBlocks(content);
        if (blocks.Count == 0)
            return;

        foreach (var block in blocks)
        {
            if (block.IsImage &&
                CreateArtwork(
                    block.ImageUrl,
                    1600) is { } imageSource)
            {
                ContentBlocksPanel.Children.Add(
                    new Image
                    {
                        Source = imageSource,
                        MaxWidth = 900,
                        Stretch =
                            Microsoft.UI.Xaml.Media.Stretch.Uniform,
                        HorizontalAlignment =
                            HorizontalAlignment.Left,
                    });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(block.Text))
            {
                ContentBlocksPanel.Children.Add(
                    new TextBlock
                    {
                        Text = block.Text,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                    });
            }
        }
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
