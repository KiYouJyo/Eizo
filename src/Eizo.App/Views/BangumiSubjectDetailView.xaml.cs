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

public sealed partial class BangumiSubjectDetailView : UserControl
{
    private readonly AppLocalizationService _localization =
        AppLocalizationService.Default;
    private readonly BangumiRepository _repository =
        BangumiRepository.Default;
    private readonly BangumiCommunityRepository _community =
        BangumiCommunityRepository.Default;
    private readonly BangumiAccountService _account =
        BangumiAccountService.Default;
    private readonly ObservableCollection<BangumiCommentViewModel> _comments = [];
    private readonly ObservableCollection<BangumiReviewViewModel> _reviews = [];
    private readonly ObservableCollection<BangumiTopicViewModel> _topics = [];
    private readonly ObservableCollection<BangumiRelatedSubjectViewModel> _recommendations = [];
    private readonly ObservableCollection<BangumiRelatedSubjectViewModel> _relations = [];
    private readonly int _subjectId;

    private BangumiSubjectCard _subject;
    private CancellationTokenSource? _loadCancellation;
    private int _commentsOffset;
    private int _reviewsOffset;
    private int _topicsOffset;

    public BangumiSubjectDetailView(
        BangumiSubjectCard subject)
    {
        _subject = subject;
        _subjectId = subject.Id;

        InitializeComponent();

        CommentsList.ItemsSource = _comments;
        ReviewsList.ItemsSource = _reviews;
        TopicsList.ItemsSource = _topics;
        RecommendationsList.ItemsSource = _recommendations;
        RelationsList.ItemsSource = _relations;

        ApplyText();
        ApplyCard(subject);

        Loaded += BangumiSubjectDetailView_Loaded;
        Unloaded += BangumiSubjectDetailView_Unloaded;
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
        PageTitle.Text = T("Bangumi_DetailPageTitle");
        RefreshButton.Content = T("Bangumi_Refresh");
        OpenBangumiButton.Content =
            T("Bangumi_OpenOnBangumi");
        SummaryTitle.Text = T("Bangumi_Summary");
        TagsTitle.Text = T("Bangumi_Tags");

        CommentsTab.Header = T("Bangumi_CommunityComments");
        ReviewsTab.Header = T("Bangumi_CommunityReviews");
        TopicsTab.Header = T("Bangumi_CommunityTopics");
        RelatedTab.Header = T("Bangumi_CommunityRelated");
        RecommendationsTitle.Text =
            T("Bangumi_CommunityRecommendations");
        RelationsTitle.Text =
            T("Bangumi_CommunityRelations");
        CommentsLoadMoreButton.Content =
            T("Bangumi_LoadMore");
        ReviewsLoadMoreButton.Content =
            T("Bangumi_LoadMore");
        TopicsLoadMoreButton.Content =
            T("Bangumi_LoadMore");
    }

    private async void BangumiSubjectDetailView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadAsync(forceRefresh: false);
    }

    private void BangumiSubjectDetailView_Unloaded(
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
        await LoadAsync(forceRefresh: true);
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        RefreshButton.IsEnabled = false;
        StatusText.Text = T("Bangumi_Loading");

        var communityTask =
            LoadCommunityAsync(
                reset: true,
                cancellationToken);

        try
        {
            var result =
                await _repository.GetSubjectAsync(
                    _subjectId,
                    forceRefresh,
                    cancellationToken);

            _subject = result.Value.Card;
            ApplyDetail(result.Value);

            var localTime =
                result.FetchedAtUtc.ToLocalTime().ToString(
                    "g",
                    CultureInfo.CurrentCulture);
            StatusText.Text = result.IsStale
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

            await communityTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            StatusText.Text =
                T("Bangumi_NetworkError");

            try
            {
                await communityTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
        }
    }

    private async Task LoadCommunityAsync(
        bool reset,
        CancellationToken cancellationToken)
    {
        if (reset)
        {
            _comments.Clear();
            _reviews.Clear();
            _topics.Clear();
            _recommendations.Clear();
            _relations.Clear();
            _commentsOffset = 0;
            _reviewsOffset = 0;
            _topicsOffset = 0;

            CommentsStatusText.Text =
                T("Bangumi_CommunityLoading");
            ReviewsStatusText.Text =
                T("Bangumi_CommunityLoading");
            TopicsStatusText.Text =
                T("Bangumi_CommunityLoading");
            RelatedStatusText.Text =
                T("Bangumi_CommunityLoading");
        }

        var commentsTask =
            LoadCommentsPageAsync(
                append: false,
                cancellationToken);
        var reviewsTask =
            LoadReviewsPageAsync(
                append: false,
                cancellationToken);
        var topicsTask =
            LoadTopicsPageAsync(
                append: false,
                cancellationToken);
        var relatedTask =
            LoadRelatedAsync(cancellationToken);

        await Task.WhenAll(
            commentsTask,
            reviewsTask,
            topicsTask,
            relatedTask);
    }

    private async Task LoadCommentsPageAsync(
        bool append,
        CancellationToken cancellationToken)
    {
        try
        {
            var page =
                await _community.GetSubjectCommentsAsync(
                    _subjectId,
                    append ? _commentsOffset : 0,
                    limit: 20,
                    accessToken:
                        _account.GetAccessTokenForRequest(),
                    cancellationToken: cancellationToken);

            if (!append)
                _comments.Clear();

            foreach (var item in page.Items)
                _comments.Add(CreateCommentViewModel(item));

            _commentsOffset =
                page.Offset +
                page.Items.Count;
            CommentsLoadMoreButton.Visibility =
                page.HasMore
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            CommentsStatusText.Text =
                _comments.Count == 0
                    ? T("Bangumi_CommunityNoComments")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityCountFormat"),
                        _comments.Count,
                        page.Total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            CommentsStatusText.Text =
                T("Bangumi_CommunityUnavailable");
            CommentsLoadMoreButton.Visibility =
                Visibility.Collapsed;
        }
    }

    private async Task LoadReviewsPageAsync(
        bool append,
        CancellationToken cancellationToken)
    {
        try
        {
            var page =
                await _community.GetSubjectReviewsAsync(
                    _subjectId,
                    append ? _reviewsOffset : 0,
                    limit: 10,
                    accessToken:
                        _account.GetAccessTokenForRequest(),
                    cancellationToken: cancellationToken);

            if (!append)
                _reviews.Clear();

            foreach (var item in page.Items)
                _reviews.Add(CreateReviewViewModel(item));

            _reviewsOffset =
                page.Offset +
                page.Items.Count;
            ReviewsLoadMoreButton.Visibility =
                page.HasMore
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            ReviewsStatusText.Text =
                _reviews.Count == 0
                    ? T("Bangumi_CommunityNoReviews")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityCountFormat"),
                        _reviews.Count,
                        page.Total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ReviewsStatusText.Text =
                T("Bangumi_CommunityUnavailable");
            ReviewsLoadMoreButton.Visibility =
                Visibility.Collapsed;
        }
    }

    private async Task LoadTopicsPageAsync(
        bool append,
        CancellationToken cancellationToken)
    {
        try
        {
            var page =
                await _community.GetSubjectTopicsAsync(
                    _subjectId,
                    append ? _topicsOffset : 0,
                    limit: 20,
                    _account.GetAccessTokenForRequest(),
                    cancellationToken);

            if (!append)
                _topics.Clear();

            foreach (var item in page.Items)
                _topics.Add(CreateTopicViewModel(item));

            _topicsOffset =
                page.Offset +
                page.Items.Count;
            TopicsLoadMoreButton.Visibility =
                page.HasMore
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            TopicsStatusText.Text =
                _topics.Count == 0
                    ? T("Bangumi_CommunityNoTopics")
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        T("Bangumi_CommunityCountFormat"),
                        _topics.Count,
                        page.Total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            TopicsStatusText.Text =
                T("Bangumi_CommunityUnavailable");
            TopicsLoadMoreButton.Visibility =
                Visibility.Collapsed;
        }
    }

    private async Task LoadRelatedAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken =
                _account.GetAccessTokenForRequest();

            var recsTask =
                _community.GetSubjectRecommendationsAsync(
                    _subjectId,
                    limit: 10,
                    accessToken: accessToken,
                    cancellationToken: cancellationToken);
            var relationsTask =
                _community.GetSubjectRelationsAsync(
                    _subjectId,
                    limit: 20,
                    accessToken: accessToken,
                    cancellationToken: cancellationToken);

            await Task.WhenAll(
                recsTask,
                relationsTask);

            _recommendations.Clear();
            foreach (var item in recsTask.Result.Items)
            {
                _recommendations.Add(
                    CreateRecommendationViewModel(item));
            }

            _relations.Clear();
            foreach (var item in relationsTask.Result.Items)
            {
                _relations.Add(
                    CreateRelationViewModel(item));
            }

            RelatedStatusText.Text =
                _recommendations.Count == 0 &&
                _relations.Count == 0
                    ? T("Bangumi_CommunityNoRelated")
                    : string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            RelatedStatusText.Text =
                T("Bangumi_CommunityUnavailable");
        }
    }

    private async void CommentsLoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadCancellation is null)
            return;

        CommentsLoadMoreButton.IsEnabled = false;
        await LoadCommentsPageAsync(
            append: true,
            _loadCancellation.Token);
        CommentsLoadMoreButton.IsEnabled = true;
    }

    private async void ReviewsLoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadCancellation is null)
            return;

        ReviewsLoadMoreButton.IsEnabled = false;
        await LoadReviewsPageAsync(
            append: true,
            _loadCancellation.Token);
        ReviewsLoadMoreButton.IsEnabled = true;
    }

    private async void TopicsLoadMoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_loadCancellation is null)
            return;

        TopicsLoadMoreButton.IsEnabled = false;
        await LoadTopicsPageAsync(
            append: true,
            _loadCancellation.Token);
        TopicsLoadMoreButton.IsEnabled = true;
    }

    private async void ReviewItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: BangumiSubjectReview review
            })
        {
            await Launcher.LaunchUriAsync(
                new Uri(
                    $"https://bgm.tv/blog/{review.EntryId}",
                    UriKind.Absolute));
        }
    }

    private async void TopicItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: BangumiSubjectTopic topic
            })
        {
            await Launcher.LaunchUriAsync(
                new Uri(
                    $"https://bgm.tv/subject/topic/{topic.Id}",
                    UriKind.Absolute));
        }
    }

    private void RelatedSubject_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button
            {
                Tag: BangumiSubjectCard subject
            })
        {
            SubjectRequested?.Invoke(
                this,
                subject);
        }
    }

    private BangumiCommentViewModel CreateCommentViewModel(
        BangumiSubjectComment comment)
    {
        var meta = new List<string>
        {
            CollectionLabel(comment.Type),
        };

        if (comment.Rate > 0)
            meta.Add($"★ {comment.Rate}/10");
        if (comment.UpdatedAt is { } updatedAt)
            meta.Add(FormatCommunityTime(updatedAt));

        return new BangumiCommentViewModel(
            comment,
            CreateArtwork(
                FirstNonEmpty(
                    comment.User.AvatarMedium,
                    comment.User.AvatarLarge,
                    comment.User.AvatarSmall),
                80),
            FirstNonEmpty(
                comment.User.NickName,
                comment.User.UserName),
            string.Join(" · ", meta),
            comment.Comment,
            comment.ReactionCount > 0
                ? $"♥ {comment.ReactionCount}"
                : string.Empty);
    }

    private BangumiReviewViewModel CreateReviewViewModel(
        BangumiSubjectReview review)
    {
        var meta = new List<string>();
        if (review.CreatedAt is { } createdAt)
            meta.Add(FormatCommunityTime(createdAt));
        meta.Add(
            string.Format(
                CultureInfo.CurrentCulture,
                T("Bangumi_CommunityRepliesFormat"),
                review.ReplyCount));

        return new BangumiReviewViewModel(
            review,
            CreateArtwork(
                FirstNonEmpty(
                    review.User.AvatarMedium,
                    review.User.AvatarLarge,
                    review.User.AvatarSmall),
                80),
            FirstNonEmpty(
                review.User.NickName,
                review.User.UserName),
            review.Title,
            review.Summary,
            string.Join(" · ", meta));
    }

    private BangumiTopicViewModel CreateTopicViewModel(
        BangumiSubjectTopic topic)
    {
        var meta = new List<string>
        {
            string.Format(
                CultureInfo.CurrentCulture,
                T("Bangumi_CommunityRepliesFormat"),
                topic.ReplyCount),
        };

        if (topic.UpdatedAt is { } updatedAt)
            meta.Add(FormatCommunityTime(updatedAt));

        return new BangumiTopicViewModel(
            topic,
            CreateArtwork(
                FirstNonEmpty(
                    topic.User.AvatarMedium,
                    topic.User.AvatarLarge,
                    topic.User.AvatarSmall),
                80),
            FirstNonEmpty(
                topic.User.NickName,
                topic.User.UserName),
            topic.Title,
            string.Join(" · ", meta));
    }

    private BangumiRelatedSubjectViewModel
        CreateRecommendationViewModel(
            BangumiSubjectRecommendation recommendation)
    {
        var meta = new List<string>();

        if (recommendation.Count > 0)
        {
            meta.Add(
                string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CommunityRecommendationCountFormat"),
                    recommendation.Count));
        }

        if (recommendation.Similarity > 0)
        {
            meta.Add(
                string.Format(
                    CultureInfo.CurrentCulture,
                    T("Bangumi_CommunitySimilarityFormat"),
                    recommendation.Similarity));
        }

        AddSubjectScore(
            meta,
            recommendation.Subject);

        return CreateRelatedViewModel(
            recommendation.Subject,
            string.Join(" · ", meta));
    }

    private BangumiRelatedSubjectViewModel
        CreateRelationViewModel(
            BangumiSubjectRelation relation)
    {
        var meta = new List<string>();
        var label =
            _localization.CurrentLanguage switch
            {
                "ja-JP" =>
                    FirstNonEmpty(
                        relation.RelationJapanese,
                        relation.RelationChinese,
                        relation.RelationEnglish),
                "en-US" =>
                    FirstNonEmpty(
                        relation.RelationEnglish,
                        relation.RelationChinese,
                        relation.RelationJapanese),
                _ =>
                    FirstNonEmpty(
                        relation.RelationChinese,
                        relation.RelationJapanese,
                        relation.RelationEnglish),
            };

        if (!string.IsNullOrWhiteSpace(label))
            meta.Add(label);

        AddSubjectScore(
            meta,
            relation.Subject);

        return CreateRelatedViewModel(
            relation.Subject,
            string.Join(" · ", meta));
    }

    private BangumiRelatedSubjectViewModel
        CreateRelatedViewModel(
            BangumiSubjectCard subject,
            string metaLine)
    {
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

        return new BangumiRelatedSubjectViewModel(
            subject,
            CreateArtwork(
                subject.PosterUrl,
                120),
            title,
            subtitle,
            metaLine);
    }

    private static void AddSubjectScore(
        ICollection<string> meta,
        BangumiSubjectCard subject)
    {
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");
    }

    private string CollectionLabel(
        BangumiCollectionType type) =>
        type switch
        {
            BangumiCollectionType.Wish =>
                T("Bangumi_CollectionWish"),
            BangumiCollectionType.Done =>
                T("Bangumi_CollectionDone"),
            BangumiCollectionType.Doing =>
                T("Bangumi_CollectionDoing"),
            BangumiCollectionType.OnHold =>
                T("Bangumi_CollectionOnHold"),
            BangumiCollectionType.Dropped =>
                T("Bangumi_CollectionDropped"),
            _ => string.Empty,
        };

    private static string FormatCommunityTime(
        DateTimeOffset value) =>
        value.ToLocalTime().ToString(
            "g",
            CultureInfo.CurrentCulture);

    private void ApplyDetail(
        BangumiSubjectDetail detail)
    {
        ApplyCard(detail.Card);

        var tags = detail.MetaTags
            .Concat(detail.Tags)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(
                StringComparer.CurrentCultureIgnoreCase)
            .Take(16)
            .ToArray();

        TagsText.Text = tags.Length > 0
            ? string.Join(" · ", tags)
            : T("Bangumi_NoTags");
    }

    private void ApplyCard(
        BangumiSubjectCard subject)
    {
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

        TitleText.Text = title;
        NativeTitleText.Text = nativeTitle;
        SummaryText.Text =
            string.IsNullOrWhiteSpace(subject.Summary)
                ? T("Bangumi_NoSummary")
                : subject.Summary;

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject.AirDate))
            meta.Add(subject.AirDate!);
        if (!string.IsNullOrWhiteSpace(subject.Platform))
            meta.Add(subject.Platform!);
        if (subject.EpisodeCount > 0)
        {
            meta.Add(
                L(
                    $"{subject.EpisodeCount} 集",
                    $"{subject.EpisodeCount} 話",
                    $"{subject.EpisodeCount} eps"));
        }
        if (subject.Score > 0)
            meta.Add($"★ {subject.Score:0.0}");
        if (subject.Rank > 0)
            meta.Add($"#{subject.Rank}");
        if (subject.CollectionTotal > 0)
        {
            meta.Add(
                L(
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} 收藏",
                    $"{subject.CollectionTotal} collections"));
        }

        MetaText.Text = string.Join(" · ", meta);
        PosterImage.Source =
            CreateArtwork(
                subject.PosterUrl,
                520);
    }

    private async void OpenBangumiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(
            new Uri(
                $"https://bgm.tv/subject/{_subjectId}",
                UriKind.Absolute));
    }

    private static BitmapImage? CreateArtwork(
        string? url,
        int decodeWidth)
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
