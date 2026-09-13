using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eizo.Bangumi;

internal static class BangumiCommunityJsonParser
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
        };

    public static BangumiCommunityPage<BangumiSubjectComment>
        ParseComments(string json)
    {
        var payload = DeserializePage<SubjectCommentDto>(json);
        return new BangumiCommunityPage<BangumiSubjectComment>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Where(static item => item.User is not null)
                .Select(static item =>
                    new BangumiSubjectComment(
                        item.Id,
                        ToUser(item.User!),
                        Enum.IsDefined(
                            typeof(BangumiCollectionType),
                            item.Type)
                            ? (BangumiCollectionType)item.Type
                            : BangumiCollectionType.Doing,
                        item.Rate,
                        item.Comment ?? string.Empty,
                        FromUnixSeconds(item.UpdatedAt),
                        CountReactions(item.Reactions),
                        ReactionUserIds(item.Reactions)))
                .ToArray()
                ?? []);
    }

    public static BangumiCommunityPage<BangumiChannelBlog>
        ParseChannelBlogs(string json)
    {
        var payload = DeserializePage<SlimBlogEntryDto>(json);
        return new BangumiCommunityPage<BangumiChannelBlog>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Select(static item =>
                    new BangumiChannelBlog(
                        item.Id,
                        item.Type,
                        item.User is null
                            ? EmptyUser()
                            : ToUser(item.User),
                        item.Title ?? string.Empty,
                        item.Summary ?? string.Empty,
                        item.Replies,
                        item.IsPublic,
                        FromUnixSeconds(item.CreatedAt),
                        FromUnixSeconds(item.UpdatedAt)))
                .ToArray()
                ?? []);
    }

    public static BangumiCommunityPage<BangumiSubjectReview>
        ParseReviews(string json)
    {
        var payload = DeserializePage<SubjectReviewDto>(json);
        return new BangumiCommunityPage<BangumiSubjectReview>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Where(static item =>
                    item.User is not null &&
                    item.Entry is not null)
                .Select(static item =>
                    new BangumiSubjectReview(
                        item.Id,
                        ToUser(item.User!),
                        item.Entry!.Id,
                        item.Entry.Title ?? string.Empty,
                        item.Entry.Summary ?? string.Empty,
                        item.Entry.Replies,
                        FromUnixSeconds(item.Entry.CreatedAt),
                        FromUnixSeconds(item.Entry.UpdatedAt)))
                .ToArray()
                ?? []);
    }

    public static BangumiCommunityPage<BangumiSubjectTopic>
        ParseTopics(string json)
    {
        var payload = DeserializePage<TopicDto>(json);
        return new BangumiCommunityPage<BangumiSubjectTopic>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Where(static item => item.Creator is not null)
                .Select(static item =>
                    new BangumiSubjectTopic(
                        item.Id,
                        ToUser(item.Creator!),
                        item.Title ?? string.Empty,
                        item.ReplyCount,
                        FromUnixSeconds(item.CreatedAt),
                        FromUnixSeconds(item.UpdatedAt)))
                .ToArray()
                ?? []);
    }

    public static BangumiCommunityPage<BangumiSubjectRecommendation>
        ParseRecommendations(string json)
    {
        var payload = DeserializePage<SubjectRecommendationDto>(json);
        return new BangumiCommunityPage<BangumiSubjectRecommendation>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Where(static item => item.Subject is not null)
                .Select(static item =>
                    new BangumiSubjectRecommendation(
                        ToCard(item.Subject!),
                        item.Similarity,
                        item.Count))
                .ToArray()
                ?? []);
    }

    public static BangumiCommunityPage<BangumiSubjectRelation>
        ParseRelations(string json)
    {
        var payload = DeserializePage<SubjectRelationDto>(json);
        return new BangumiCommunityPage<BangumiSubjectRelation>(
            payload.Total,
            payload.Offset,
            payload.Data?
                .Where(static item => item.Subject is not null)
                .Select(static item =>
                {
                    var relation = item.Relation;
                    return new BangumiSubjectRelation(
                        ToCard(item.Subject!),
                        relation?.Id ?? item.Type,
                        relation?.En ?? string.Empty,
                        relation?.Cn ?? string.Empty,
                        relation?.Jp ?? string.Empty,
                        relation?.Desc ?? string.Empty,
                        item.Order);
                })
                .ToArray()
                ?? []);
    }

    public static BangumiBlogDetail ParseBlogEntry(string json)
    {
        var entry = JsonSerializer.Deserialize<BlogEntryDto>(
            json,
            SerializerOptions)
            ?? throw new JsonException(
                "Bangumi blog response was empty.");

        if (entry.User is null)
        {
            throw new JsonException(
                "Bangumi blog response did not include an author.");
        }

        return new BangumiBlogDetail(
            entry.Id,
            ToUser(entry.User),
            entry.Title ?? string.Empty,
            entry.Content ?? string.Empty,
            entry.Tags ?? [],
            entry.Views,
            entry.Replies,
            FromUnixSeconds(entry.CreatedAt),
            FromUnixSeconds(entry.UpdatedAt),
            entry.IsPublic);
    }

    public static IReadOnlyList<BangumiCommunityReply>
        ParseBlogComments(string json)
    {
        var comments =
            JsonSerializer.Deserialize<List<CommentDto>>(
                json,
                SerializerOptions)
            ?? [];

        return comments
            .Where(static item => ResolveReplyUser(item) is not null)
            .Select(ToReply)
            .ToArray();
    }

    public static BangumiTopicDetail ParseTopicDetail(string json)
    {
        var topic = JsonSerializer.Deserialize<TopicDetailDto>(
            json,
            SerializerOptions)
            ?? throw new JsonException(
                "Bangumi topic response was empty.");

        if (topic.Creator is null ||
            topic.Subject is null)
        {
            throw new JsonException(
                "Bangumi topic response was incomplete.");
        }

        var posts = topic.Replies?
            .Where(static item => ResolveReplyUser(item) is not null)
            .Select(ToReply)
            .ToArray()
            ?? [];

        var rootPost =
            posts.Length > 0
                ? posts[0]
                : null;
        var replies =
            posts.Length > 1
                ? posts[1..]
                : [];

        return new BangumiTopicDetail(
            topic.Id,
            ToUser(topic.Creator),
            ToCard(topic.Subject),
            topic.Title ?? string.Empty,
            FromUnixSeconds(topic.CreatedAt),
            FromUnixSeconds(topic.UpdatedAt),
            rootPost,
            replies);
    }

    private static BangumiCommunityReply ToReply(CommentBaseDto item)
    {
        var user = ResolveReplyUser(item);
        return new BangumiCommunityReply(
            item.Id,
            user is null
                ? EmptyUser()
                : ToUser(user),
            item.Content ?? string.Empty,
            FromUnixSeconds(item.CreatedAt),
            CountReactions(item.Reactions),
            ReactionUserIds(item.Reactions),
            item.Replies?
                .Where(static reply => ResolveReplyUser(reply) is not null)
                .Select(ToReply)
                .ToArray()
                ?? []);
    }

    private static SlimUserDto? ResolveReplyUser(CommentBaseDto item) =>
        item.User ?? item.Creator;

    private static BangumiCommunityUser EmptyUser() =>
        new(
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null);

    private static PagedDto<T> DeserializePage<T>(string json) =>
        JsonSerializer.Deserialize<PagedDto<T>>(
            json,
            SerializerOptions)
        ?? new PagedDto<T>();

    private static BangumiCommunityUser ToUser(SlimUserDto user) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.NickName ?? string.Empty,
            user.Sign ?? string.Empty,
            user.Avatar?.Large,
            user.Avatar?.Medium,
            user.Avatar?.Small);

    private static BangumiSubjectCard ToCard(SlimSubjectDto subject) =>
        new(
            subject.Id,
            subject.Name ?? string.Empty,
            subject.NameCn ?? string.Empty,
            subject.Info ?? string.Empty,
            null,
            FirstNonEmpty(
                subject.Images?.Large,
                subject.Images?.Common,
                subject.Images?.Medium,
                subject.Images?.Grid,
                subject.Images?.Small),
            null,
            subject.Rating?.Score ?? 0,
            subject.Rating?.Rank ?? 0,
            0,
            0);

    private static int CountReactions(
        IReadOnlyList<ReactionDto>? reactions) =>
        reactions?.Sum(static reaction =>
            reaction.Users?.Count ?? 0)
        ?? 0;

    private static IReadOnlySet<int> ReactionUserIds(
        IReadOnlyList<ReactionDto>? reactions) =>
        reactions?
            .SelectMany(static reaction =>
                reaction.Users ?? [])
            .Select(static user => user.Id)
            .ToHashSet()
        ?? new HashSet<int>();

    private static DateTimeOffset? FromUnixSeconds(long value)
    {
        if (value <= 0)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? FirstNonEmpty(
        params string?[] values) =>
        values.FirstOrDefault(static value =>
            !string.IsNullOrWhiteSpace(value));

    private sealed class BlogEntryDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("user")]
        public SlimUserDto? User { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("views")]
        public int Views { get; set; }

        [JsonPropertyName("replies")]
        public int Replies { get; set; }

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("public")]
        public bool IsPublic { get; set; }
    }

    private sealed class TopicDetailDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("creator")]
        public SlimUserDto? Creator { get; set; }

        [JsonPropertyName("subject")]
        public SlimSubjectDto? Subject { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("replies")]
        public List<CommentBaseDto>? Replies { get; set; }
    }

    private sealed class CommentDto : CommentBaseDto
    {
    }

    private class CommentBaseDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("user")]
        public SlimUserDto? User { get; set; }

        [JsonPropertyName("creator")]
        public SlimUserDto? Creator { get; set; }

        [JsonPropertyName("reactions")]
        public List<ReactionDto>? Reactions { get; set; }

        [JsonPropertyName("replies")]
        public List<CommentBaseDto>? Replies { get; set; }
    }

    private sealed class PagedDto<T>
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("data")]
        public List<T>? Data { get; set; }
    }

    private sealed class SubjectCommentDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("user")]
        public SlimUserDto? User { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("rate")]
        public int Rate { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("reactions")]
        public List<ReactionDto>? Reactions { get; set; }
    }

    private sealed class SubjectReviewDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("user")]
        public SlimUserDto? User { get; set; }

        [JsonPropertyName("entry")]
        public SlimBlogEntryDto? Entry { get; set; }
    }

    private sealed class SlimBlogEntryDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("uid")]
        public int UserId { get; set; }

        [JsonPropertyName("user")]
        public SlimUserDto? User { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("replies")]
        public int Replies { get; set; }

        [JsonPropertyName("public")]
        public bool IsPublic { get; set; }

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }
    }

    private sealed class TopicDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("creator")]
        public SlimUserDto? Creator { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("replyCount")]
        public int ReplyCount { get; set; }

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }
    }

    private sealed class SubjectRecommendationDto
    {
        [JsonPropertyName("subject")]
        public SlimSubjectDto? Subject { get; set; }

        [JsonPropertyName("sim")]
        public double Similarity { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private sealed class SubjectRelationDto
    {
        [JsonPropertyName("subject")]
        public SlimSubjectDto? Subject { get; set; }

        [JsonPropertyName("relation")]
        public RelationTypeDto? Relation { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }
    }

    private sealed class RelationTypeDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("en")]
        public string? En { get; set; }

        [JsonPropertyName("cn")]
        public string? Cn { get; set; }

        [JsonPropertyName("jp")]
        public string? Jp { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }
    }

    private sealed class SlimSubjectDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nameCN")]
        public string? NameCn { get; set; }

        [JsonPropertyName("info")]
        public string? Info { get; set; }

        [JsonPropertyName("images")]
        public SubjectImagesDto? Images { get; set; }

        [JsonPropertyName("rating")]
        public SubjectRatingDto? Rating { get; set; }
    }

    private sealed class SubjectImagesDto
    {
        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("common")]
        public string? Common { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("grid")]
        public string? Grid { get; set; }
    }

    private sealed class SubjectRatingDto
    {
        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("rank")]
        public int Rank { get; set; }
    }

    private sealed class SlimUserDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string? UserName { get; set; }

        [JsonPropertyName("nickname")]
        public string? NickName { get; set; }

        [JsonPropertyName("sign")]
        public string? Sign { get; set; }

        [JsonPropertyName("avatar")]
        public AvatarDto? Avatar { get; set; }
    }

    private sealed class AvatarDto
    {
        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }
    }

    private sealed class ReactionDto
    {
        [JsonPropertyName("users")]
        public List<SimpleUserDto>? Users { get; set; }
    }

    private sealed class SimpleUserDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
}
