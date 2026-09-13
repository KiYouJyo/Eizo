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

public sealed partial class BangumiTopicDetailView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiCommunityRepository _community =
        BangumiCommunityRepository.Default;
    private readonly BangumiAccountService _account =
        BangumiAccountService.Default;
    private readonly ObservableCollection<BangumiReplyViewModel> _replies = [];
    private readonly BangumiSubjectTopic _topic;
    private BangumiSubjectCard _subject;
    private int _viewerUserId;
    private CancellationTokenSource? _loadCancellation;

    public BangumiTopicDetailView(
        BangumiSubjectTopic topic,
        BangumiSubjectCard subject)
    {
        _topic = topic;
        _subject = subject;

        InitializeComponent();
        RepliesList.ItemsSource = _replies;

        PageTitle.Text = T("Bangumi_TopicDetailPageTitle");
        OpenSubjectButton.Content = T("Bangumi_BackToSubject");
        OpenBangumiButton.Content = T("Bangumi_OpenOnBangumi");
        RepliesTitle.Text = T("Bangumi_TopicReplies");
        ReplyButton.Content = T("Bangumi_WriteReply");
        ReplyButton.IsEnabled = false;
        TitleText.Text = topic.Title;
        SubjectText.Text = FirstNonEmpty(
            subject.ChineseTitle,
            subject.NativeTitle);
        AuthorText.Text = FirstNonEmpty(
            topic.User.NickName,
            topic.User.UserName);
        AuthorAvatar.Source = CreateArtwork(
            FirstNonEmpty(
                topic.User.AvatarMedium,
                topic.User.AvatarLarge,
                topic.User.AvatarSmall),
            96);

        Loaded += BangumiTopicDetailView_Loaded;
        Unloaded += BangumiTopicDetailView_Unloaded;
    }

    public event EventHandler<BangumiSubjectCard>? SubjectRequested;

    private string T(string key) =>
        _localization.GetString(key);

    private async void BangumiTopicDetailView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private void BangumiTopicDetailView_Unloaded(
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
            ReplyButton.IsEnabled =
                _account.IsConnected &&
                await BangumiTurnstileDialogService.IsAvailableAsync();

            if (_account.IsConnected)
            {
                var profile = await _account.GetProfileAsync(
                    forceRefresh: false,
                    cancellationToken);
                _viewerUserId = profile?.Id ?? 0;
            }
            else
            {
                _viewerUserId = 0;
            }

            var detail = await _community.GetSubjectTopicAsync(
                _topic.Id,
                _account.GetAccessTokenForRequest(),
                cancellationToken);

            _subject = detail.Subject;
            TitleText.Text = detail.Title;
            SubjectText.Text = FirstNonEmpty(
                detail.Subject.ChineseTitle,
                detail.Subject.NativeTitle);
            AuthorText.Text = FirstNonEmpty(
                detail.User.NickName,
                detail.User.UserName);
            AuthorAvatar.Source = CreateArtwork(
                FirstNonEmpty(
                    detail.User.AvatarMedium,
                    detail.User.AvatarLarge,
                    detail.User.AvatarSmall),
                96);

            if (detail.RootPost is { } root)
            {
                RootContentText.Text =
                    BangumiCommunityText.ToPlainText(
                        root.Content);
                RootMetaText.Text = root.CreatedAt is { } rootTime
                    ? FormatTime(rootTime)
                    : string.Empty;
            }
            else
            {
                RootContentText.Text =
                    T("Bangumi_CommunityContentUnavailable");
                RootMetaText.Text = string.Empty;
            }

            _replies.Clear();
            foreach (var reply in FlattenReplies(detail.Replies))
                _replies.Add(reply);

            RepliesStatusText.Text =
                _replies.Count == 0
                    ? T("Bangumi_TopicNoReplies")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityLoadedCountFormat"),
                        _replies.Count);
            StatusText.Text = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            StatusText.Text =
                T("Bangumi_CommunityUnavailable");
            RepliesStatusText.Text =
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

        var reacted =
            _viewerUserId > 0 &&
            reply.ReactionUserIds.Contains(_viewerUserId);

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
            ReactionLabel(
                reacted,
                reply.ReactionCount),
            _viewerUserId > 0,
            reacted,
            nested ? new Thickness(32, 0, 0, 0) : new Thickness(0));
    }

    private async void ReplyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var token = _account.GetAccessTokenForRequest();
        if (string.IsNullOrWhiteSpace(token))
        {
            RepliesStatusText.Text =
                T("Bangumi_CommunitySignInToInteract");
            return;
        }

        var content =
            await BangumiCommunityWriteDialogService
                .PromptReplyAsync(
                    XamlRoot,
                    T("Bangumi_WriteTopicReplyTitle"));
        if (string.IsNullOrWhiteSpace(content))
            return;

        var turnstile =
            await BangumiTurnstileDialogService.AcquireAsync(
                XamlRoot);
        if (string.IsNullOrWhiteSpace(turnstile))
        {
            RepliesStatusText.Text =
                T("Bangumi_TurnstileUnavailable");
            return;
        }

        ReplyButton.IsEnabled = false;
        try
        {
            await _community.CreateSubjectReplyAsync(
                _topic.Id,
                content,
                replyTo: 0,
                turnstile,
                token,
                _loadCancellation?.Token ??
                CancellationToken.None);

            var detail =
                await _community.GetSubjectTopicAsync(
                    _topic.Id,
                    token,
                    _loadCancellation?.Token ??
                    CancellationToken.None);

            _replies.Clear();
            foreach (var reply in FlattenReplies(detail.Replies))
                _replies.Add(reply);

            RepliesStatusText.Text =
                string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CommunityLoadedCountFormat"),
                    _replies.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            RepliesStatusText.Text =
                T("Bangumi_CommunityWriteFailed");
        }
        finally
        {
            ReplyButton.IsEnabled =
                _account.IsConnected &&
                await BangumiTurnstileDialogService.IsAvailableAsync();
        }
    }

    private async void ReplyReactionButton_Click(
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

        var token = _account.GetAccessTokenForRequest();
        if (string.IsNullOrWhiteSpace(token))
        {
            RepliesStatusText.Text =
                T("Bangumi_CommunitySignInToInteract");
            return;
        }

        var index = _replies.IndexOf(item);
        if (index < 0)
            return;

        var button = (Button)sender;
        button.IsEnabled = false;

        try
        {
            if (item.IsReacted)
            {
                await _community.UnlikeSubjectPostAsync(
                    item.Reply.Id,
                    token,
                    _loadCancellation?.Token ??
                    CancellationToken.None);
            }
            else
            {
                await _community.LikeSubjectPostAsync(
                    item.Reply.Id,
                    value: 0,
                    token,
                    _loadCancellation?.Token ??
                    CancellationToken.None);
            }

            var reacted = !item.IsReacted;
            var count = Math.Max(
                0,
                item.Reply.ReactionCount +
                (reacted ? 1 : -1));
            _replies[index] = item with
            {
                IsReacted = reacted,
                ReactionText = ReactionLabel(
                    reacted,
                    count),
            };
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            RepliesStatusText.Text =
                T("Bangumi_CommunityWriteFailed");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private static string ReactionLabel(
        bool reacted,
        int count) =>
        $"{(reacted ? "♥" : "♡")} {Math.Max(0, count)}";

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
                $"https://bgm.tv/subject/topic/{_topic.Id}",
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
