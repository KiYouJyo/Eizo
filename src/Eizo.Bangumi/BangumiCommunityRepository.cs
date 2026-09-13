namespace Eizo.Bangumi;

public sealed class BangumiCommunityRepository
{
    private readonly BangumiCommunityClient _client;

    internal BangumiCommunityRepository(
        BangumiCommunityClient? client = null)
    {
        _client = client ?? new BangumiCommunityClient();
    }

    public static BangumiCommunityRepository Default { get; } = new();

    public async Task<int> CreateSubjectCommentAsync(
        int subjectId,
        string comment,
        BangumiCollectionType? type,
        int? rate,
        string turnstileToken,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.CreateSubjectCommentAsync(
            subjectId,
            comment,
            type,
            rate,
            turnstileToken,
            accessToken,
            cancellationToken);
        return ParseCreatedId(payload);
    }

    public async Task<int> CreateBlogCommentAsync(
        int entryId,
        string content,
        int replyTo,
        string turnstileToken,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.CreateBlogCommentAsync(
            entryId,
            content,
            replyTo,
            turnstileToken,
            accessToken,
            cancellationToken);
        return ParseCreatedId(payload);
    }

    public async Task<int> CreateSubjectReplyAsync(
        int topicId,
        string content,
        int replyTo,
        string turnstileToken,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.CreateSubjectReplyAsync(
            topicId,
            content,
            replyTo,
            turnstileToken,
            accessToken,
            cancellationToken);
        return ParseCreatedId(payload);
    }

    public Task LikeSubjectCommentAsync(
        int commentId,
        int value,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            _client.LikeSubjectCommentAsync(
                commentId,
                value,
                accessToken,
                cancellationToken));

    public Task UnlikeSubjectCommentAsync(
        int commentId,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            _client.UnlikeSubjectCommentAsync(
                commentId,
                accessToken,
                cancellationToken));

    public Task LikeSubjectPostAsync(
        int postId,
        int value,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            _client.LikeSubjectPostAsync(
                postId,
                value,
                accessToken,
                cancellationToken));

    public Task UnlikeSubjectPostAsync(
        int postId,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(
            _client.UnlikeSubjectPostAsync(
                postId,
                accessToken,
                cancellationToken));

    public async Task<BangumiBlogDetail> GetBlogEntryAsync(
        int entryId,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetBlogEntryAsync(
            entryId,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseBlogEntry(payload);
    }

    public async Task<IReadOnlyList<BangumiCommunityReply>>
        GetBlogCommentsAsync(
            int entryId,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetBlogCommentsAsync(
            entryId,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseBlogComments(payload);
    }

    public async Task<BangumiTopicDetail> GetSubjectTopicAsync(
        int topicId,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectTopicAsync(
            topicId,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseTopicDetail(payload);
    }

    public async Task<BangumiCommunityPage<BangumiSubjectComment>>
        GetSubjectCommentsAsync(
            int subjectId,
            int offset = 0,
            int limit = 20,
            string? accessToken = null,
            BangumiCollectionType? type = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectCommentsAsync(
            subjectId,
            limit,
            offset,
            accessToken,
            cancellationToken,
            type);
        return BangumiCommunityJsonParser.ParseComments(payload) with
        {
            Offset = offset,
        };
    }

    public async Task<BangumiCommunityPage<BangumiSubjectReview>>
        GetSubjectReviewsAsync(
            int subjectId,
            int offset = 0,
            int limit = 10,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectReviewsAsync(
            subjectId,
            limit,
            offset,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseReviews(payload) with
        {
            Offset = offset,
        };
    }

    public async Task<BangumiCommunityPage<BangumiSubjectTopic>>
        GetSubjectTopicsAsync(
            int subjectId,
            int offset = 0,
            int limit = 20,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectTopicsAsync(
            subjectId,
            limit,
            offset,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseTopics(payload) with
        {
            Offset = offset,
        };
    }

    public async Task<BangumiCommunityPage<BangumiSubjectRecommendation>>
        GetSubjectRecommendationsAsync(
            int subjectId,
            int offset = 0,
            int limit = 10,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectRecommendationsAsync(
            subjectId,
            limit,
            offset,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseRecommendations(payload) with
        {
            Offset = offset,
        };
    }

    public async Task<BangumiCommunityPage<BangumiSubjectRelation>>
        GetSubjectRelationsAsync(
            int subjectId,
            int offset = 0,
            int limit = 20,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetSubjectRelationsAsync(
            subjectId,
            limit,
            offset,
            accessToken,
            cancellationToken);
        return BangumiCommunityJsonParser.ParseRelations(payload) with
        {
            Offset = offset,
        };
    }
    private static int ParseCreatedId(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("id", out var id) ||
            !id.TryGetInt32(out var value) ||
            value <= 0)
        {
            throw new System.Text.Json.JsonException(
                "Bangumi create response did not include a valid id.");
        }

        return value;
    }

    private static async Task CompleteAsync(Task<string> task)
    {
        _ = await task;
    }

}
