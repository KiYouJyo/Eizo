namespace Eizo.Bangumi;

public sealed record BangumiSubjectCard(
    int Id,
    string NativeTitle,
    string ChineseTitle,
    string Summary,
    string? AirDate,
    string? PosterUrl,
    string? Platform,
    double Score,
    int Rank,
    int EpisodeCount,
    int CollectionTotal);

public sealed record BangumiCalendarDayModel(
    int WeekdayId,
    string EnglishName,
    string ChineseName,
    string JapaneseName,
    IReadOnlyList<BangumiSubjectCard> Items);

public sealed record BangumiCalendarSnapshot(
    IReadOnlyList<BangumiCalendarDayModel> Days);

public sealed record BangumiSeasonSnapshot(
    int Year,
    int StartMonth,
    IReadOnlyList<BangumiSubjectCard> Items);

public sealed record BangumiSubjectPage(
    int Total,
    int Limit,
    int Offset,
    IReadOnlyList<BangumiSubjectCard> Items)
{
    public bool HasMore =>
        Offset + Items.Count < Total;
}

public sealed record BangumiSubjectDetail(
    BangumiSubjectCard Card,
    IReadOnlyList<string> MetaTags,
    IReadOnlyList<string> Tags);

public sealed record BangumiLoadResult<T>(
    T Value,
    bool IsFromCache,
    bool IsStale,
    DateTimeOffset FetchedAtUtc);

public enum BangumiCollectionType
{
    Wish = 1,
    Done = 2,
    Doing = 3,
    OnHold = 4,
    Dropped = 5,
}

public sealed record BangumiUserProfile(
    int Id,
    string UserName,
    string NickName,
    string Sign,
    string? AvatarLarge,
    string? AvatarMedium,
    string? AvatarSmall);

public sealed record BangumiUserCollectionItem(
    BangumiSubjectCard Subject,
    BangumiCollectionType Type,
    int EpisodeStatus,
    int Rate,
    bool IsPrivate,
    DateTimeOffset? UpdatedAt);

public sealed record BangumiUserCollectionPage(
    int Total,
    int Limit,
    int Offset,
    IReadOnlyList<BangumiUserCollectionItem> Items)
{
    public bool HasMore =>
        Offset + Items.Count < Total;
}

public sealed record BangumiCommunityUser(
    int Id,
    string UserName,
    string NickName,
    string Sign,
    string? AvatarLarge,
    string? AvatarMedium,
    string? AvatarSmall);

public sealed record BangumiCommunityPage<T>(
    int Total,
    int Offset,
    IReadOnlyList<T> Items)
{
    public bool HasMore =>
        Offset + Items.Count < Total;
}

public sealed record BangumiSubjectComment(
    int Id,
    BangumiCommunityUser User,
    BangumiCollectionType Type,
    int Rate,
    string Comment,
    DateTimeOffset? UpdatedAt,
    int ReactionCount);

public sealed record BangumiSubjectReview(
    int Id,
    BangumiCommunityUser User,
    int EntryId,
    string Title,
    string Summary,
    int ReplyCount,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BangumiSubjectTopic(
    int Id,
    BangumiCommunityUser User,
    string Title,
    int ReplyCount,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record BangumiSubjectRecommendation(
    BangumiSubjectCard Subject,
    double Similarity,
    int Count);

public sealed record BangumiSubjectRelation(
    BangumiSubjectCard Subject,
    int RelationId,
    string RelationEnglish,
    string RelationChinese,
    string RelationJapanese,
    string RelationDescription,
    int Order);

public sealed record BangumiCommunityReply(
    int Id,
    BangumiCommunityUser User,
    string Content,
    DateTimeOffset? CreatedAt,
    int ReactionCount,
    IReadOnlyList<BangumiCommunityReply> Replies);

public sealed record BangumiBlogDetail(
    int EntryId,
    BangumiCommunityUser User,
    string Title,
    string Content,
    IReadOnlyList<string> Tags,
    int ViewCount,
    int ReplyCount,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsPublic);

public sealed record BangumiTopicDetail(
    int TopicId,
    BangumiCommunityUser User,
    BangumiSubjectCard Subject,
    string Title,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    BangumiCommunityReply? RootPost,
    IReadOnlyList<BangumiCommunityReply> Replies);
