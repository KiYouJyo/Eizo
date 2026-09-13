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
}
