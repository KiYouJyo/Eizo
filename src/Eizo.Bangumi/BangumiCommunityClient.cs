using System.Globalization;
using System.Net.Http.Headers;

namespace Eizo.Bangumi;

internal sealed class BangumiCommunityClient
{
    private static readonly HttpClient SharedClient = CreateHttpClient();
    private readonly HttpClient _httpClient;

    public BangumiCommunityClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedClient;
    }

    public Task<string> GetSubjectCommentsAsync(
        int subjectId,
        int limit,
        int offset,
        string? accessToken,
        CancellationToken cancellationToken) =>
        GetSubjectPageAsync(
            subjectId,
            "comments",
            limit,
            offset,
            maximumLimit: 100,
            accessToken,
            cancellationToken);

    public Task<string> GetSubjectReviewsAsync(
        int subjectId,
        int limit,
        int offset,
        string? accessToken,
        CancellationToken cancellationToken) =>
        GetSubjectPageAsync(
            subjectId,
            "reviews",
            limit,
            offset,
            maximumLimit: 20,
            accessToken,
            cancellationToken);

    public Task<string> GetSubjectTopicsAsync(
        int subjectId,
        int limit,
        int offset,
        string? accessToken,
        CancellationToken cancellationToken) =>
        GetSubjectPageAsync(
            subjectId,
            "topics",
            limit,
            offset,
            maximumLimit: 100,
            accessToken,
            cancellationToken);

    public Task<string> GetSubjectRecommendationsAsync(
        int subjectId,
        int limit,
        int offset,
        string? accessToken,
        CancellationToken cancellationToken) =>
        GetSubjectPageAsync(
            subjectId,
            "recs",
            limit,
            offset,
            maximumLimit: 10,
            accessToken,
            cancellationToken);

    public Task<string> GetSubjectRelationsAsync(
        int subjectId,
        int limit,
        int offset,
        string? accessToken,
        CancellationToken cancellationToken) =>
        GetSubjectPageAsync(
            subjectId,
            "relations",
            limit,
            offset,
            maximumLimit: 100,
            accessToken,
            cancellationToken);

    public Task<string> GetBlogEntryAsync(
        int entryId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (entryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(entryId));

        return GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"p1/blogs/{entryId}"),
            accessToken,
            cancellationToken);
    }

    public Task<string> GetBlogCommentsAsync(
        int entryId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (entryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(entryId));

        return GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"p1/blogs/{entryId}/comments"),
            accessToken,
            cancellationToken);
    }

    public Task<string> GetSubjectTopicAsync(
        int topicId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (topicId <= 0)
            throw new ArgumentOutOfRangeException(nameof(topicId));

        return GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"p1/subjects/-/topics/{topicId}"),
            accessToken,
            cancellationToken);
    }

    private Task<string> GetSubjectPageAsync(
        int subjectId,
        string resource,
        int limit,
        int offset,
        int maximumLimit,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId));
        if (limit is < 1 || limit > maximumLimit)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"p1/subjects/{subjectId}/{resource}?limit={limit}&offset={offset}"),
            accessToken,
            cancellationToken);
    }

    private async Task<string> GetStringAsync(
        string relativeUri,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            relativeUri);

        var token = NormalizeAccessToken(accessToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(
                "https://next.bgm.tv/",
                UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15),
        };

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "KiYouJyo/Eizo/0.4.5 (Windows) (https://github.com/KiYouJyo/Eizo)");
        return client;
    }

    private static string? NormalizeAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var token = accessToken.Trim();
        const string bearerPrefix = "Bearer ";
        if (token.StartsWith(
                bearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            token = token[bearerPrefix.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(token)
            ? null
            : token;
    }
}
