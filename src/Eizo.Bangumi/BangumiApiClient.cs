using System.Globalization;
using System.Net.Http.Headers;

namespace Eizo.Bangumi;

internal sealed class BangumiApiClient
{
    private static readonly HttpClient SharedClient = CreateHttpClient();
    private readonly HttpClient _httpClient;

    public BangumiApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedClient;
    }

    public Task<string> GetAnimeAsync(
        int limit,
        int offset,
        string sort,
        int? year,
        int? month,
        int? category,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (sort is not ("date" or "rank"))
            throw new ArgumentOutOfRangeException(nameof(sort));

        var query = new List<string>
        {
            "type=2",
            "sort=" + sort,
            "limit=" + limit.ToString(CultureInfo.InvariantCulture),
            "offset=" + offset.ToString(CultureInfo.InvariantCulture),
        };

        if (category is { } categoryValue)
            query.Add("cat=" + categoryValue.ToString(CultureInfo.InvariantCulture));
        if (year is { } yearValue)
            query.Add("year=" + yearValue.ToString(CultureInfo.InvariantCulture));
        if (month is { } monthValue)
            query.Add("month=" + monthValue.ToString(CultureInfo.InvariantCulture));

        return GetStringAsync(
            "v0/subjects?" + string.Join("&", query),
            cancellationToken);
    }

    public Task<string> GetSeasonMonthAsync(
        int year,
        int month,
        int limit,
        CancellationToken cancellationToken) =>
        GetAnimeAsync(
            limit,
            offset: 0,
            sort: "date",
            year,
            month,
            category: 1,
            cancellationToken);

    public Task<string> GetRankedAnimeAsync(
        int limit,
        int offset,
        int? year,
        int? month,
        CancellationToken cancellationToken) =>
        GetAnimeAsync(
            limit,
            offset,
            sort: "rank",
            year,
            month,
            category: null,
            cancellationToken);

    public Task<string> GetCalendarAsync(
        CancellationToken cancellationToken) =>
        GetStringAsync("calendar", cancellationToken);

    public Task<string> GetSubjectAsync(
        int subjectId,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"v0/subjects/{subjectId}"),
            cancellationToken);

    public Task<string> GetMyselfAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            "v0/me",
            cancellationToken,
            NormalizeAccessToken(accessToken));

    public Task<string> GetUserCollectionsAsync(
        string userName,
        BangumiCollectionType type,
        int limit,
        int offset,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var relativeUri =
            "v0/users/" +
            Uri.EscapeDataString(userName.Trim()) +
            "/collections?subject_type=2&type=" +
            ((int)type).ToString(CultureInfo.InvariantCulture) +
            "&limit=" +
            limit.ToString(CultureInfo.InvariantCulture) +
            "&offset=" +
            offset.ToString(CultureInfo.InvariantCulture);

        return GetStringAsync(
            relativeUri,
            cancellationToken,
            NormalizeAccessToken(accessToken));
    }

    private async Task<string> GetStringAsync(
        string relativeUri,
        CancellationToken cancellationToken,
        string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            relativeUri);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    bearerToken);
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
            BaseAddress = new Uri("https://api.bgm.tv/", UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15),
        };

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "KiYouJyo/Eizo/0.4.3 (Windows) (https://github.com/KiYouJyo/Eizo)");
        return client;
    }

    private static string NormalizeAccessToken(
        string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return accessToken.Trim();
    }
}
