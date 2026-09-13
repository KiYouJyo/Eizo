using Eizo.Bangumi;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eizo.Models;

public sealed record BangumiCardViewModel(
    BangumiSubjectCard Subject,
    BitmapImage? Artwork,
    string Title,
    string Subtitle,
    string MetaLine,
    string SourceLabel,
    string TypeLabel);

public sealed record BangumiDayOption(
    int WeekdayId,
    string Label);

public sealed record BangumiCommentViewModel(
    BangumiSubjectComment Comment,
    BitmapImage? Avatar,
    string UserName,
    string MetaLine,
    string CommentText,
    string ReactionText);

public sealed record BangumiReviewViewModel(
    BangumiSubjectReview Review,
    BitmapImage? Avatar,
    string UserName,
    string Title,
    string Summary,
    string MetaLine);

public sealed record BangumiTopicViewModel(
    BangumiSubjectTopic Topic,
    BitmapImage? Avatar,
    string UserName,
    string Title,
    string MetaLine);

public sealed record BangumiRelatedSubjectViewModel(
    BangumiSubjectCard Subject,
    BitmapImage? Artwork,
    string Title,
    string Subtitle,
    string MetaLine);

public sealed record BangumiReplyViewModel(
    BangumiCommunityReply Reply,
    BitmapImage? Avatar,
    string UserName,
    string Content,
    string MetaLine,
    string ReactionText,
    Thickness Indent);

public sealed record BangumiCommunityDetailHeaderViewModel(
    BitmapImage? Avatar,
    string UserName,
    string MetaLine);
