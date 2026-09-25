using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

    public Task<string> SearchAnimeAsync(
        string keyword,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        return SearchAnimeAsync(
            new BangumiAnimeSearchQuery(
                keyword.Trim(),
                "match",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Year: null),
            limit,
            offset,
            cancellationToken);
    }

    public Task<string> SearchAnimeAsync(
        BangumiAnimeSearchQuery search,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var sort = string.IsNullOrWhiteSpace(search.Sort)
            ? "rank"
            : search.Sort.Trim().ToLowerInvariant();
        if (sort is not ("match" or "heat" or "rank" or "score"))
            throw new ArgumentOutOfRangeException(nameof(search));

        var filter = new Dictionary<string, object>
        {
            ["type"] = new[] { 2 },
            ["nsfw"] = false,
        };

        var metaTags = NormalizeSearchValues(search.MetaTags);
        if (metaTags.Length > 0)
            filter["meta_tags"] = metaTags;

        var tags = NormalizeSearchValues(search.Tags);
        if (tags.Length > 0)
            filter["tag"] = tags;

        if (search.Year is { } year)
        {
            if (year is < 1900 or > 2200)
                throw new ArgumentOutOfRangeException(nameof(search));

            filter["air_date"] = new[]
            {
                $">={year:0000}-01-01",
                $"<{year + 1:0000}-01-01",
            };
        }

        var body = JsonSerializer.Serialize(
            new
            {
                keyword = (search.Keyword ?? string.Empty).Trim(),
                sort,
                filter,
            });

        return SendJsonAsync(
            HttpMethod.Post,
            "v0/search/subjects?limit=" +
            limit.ToString(CultureInfo.InvariantCulture) +
            "&offset=" +
            offset.ToString(CultureInfo.InvariantCulture),
            body,
            cancellationToken);
    }

    private static string[] NormalizeSearchValues(
        IReadOnlyList<string>? values) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? Array.Empty<string>();

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

    public Task<string> GetSubjectCharactersAsync(
        int subjectId,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"v0/subjects/{subjectId}/characters"),
            cancellationToken);

    public Task<string> GetSubjectPersonsAsync(
        int subjectId,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"v0/subjects/{subjectId}/persons"),
            cancellationToken);

    public Task<string> GetMyselfAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            "v0/me",
            cancellationToken,
            NormalizeAccessToken(accessToken));

    public Task<string> GetUserSubjectCollectionAsync(
        string userName,
        int subjectId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId));

        return GetStringAsync(
            "v0/users/" +
            Uri.EscapeDataString(userName.Trim()) +
            "/collections/" +
            subjectId.ToString(CultureInfo.InvariantCulture),
            cancellationToken,
            NormalizeAccessToken(accessToken));
    }

    public Task<string> SetUserSubjectCollectionTypeAsync(
        int subjectId,
        BangumiCollectionType type,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var body = JsonSerializer.Serialize(
            new
            {
                type = (int)type,
            });

        return SendJsonAsync(
            HttpMethod.Post,
            "v0/users/-/collections/" +
            subjectId.ToString(CultureInfo.InvariantCulture),
            body,
            cancellationToken,
            NormalizeAccessToken(accessToken));
    }

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

    private async Task<string> SendJsonAsync(
        HttpMethod method,
        string relativeUri,
        string json,
        CancellationToken cancellationToken,
        string? bearerToken = null)
    {
        using var content =
            new StringContent(
                json,
                Encoding.UTF8);
        content.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/json");

        using var request = new HttpRequestMessage(
            method,
            relativeUri)
        {
            Content = content,
        };

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

        return await ReadResponseAsync(
            response,
            cancellationToken);
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

        return await ReadResponseAsync(
            response,
            cancellationToken);
    }

    private static async Task<string> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (response.IsSuccessStatusCode)
            return body;

        throw new BangumiApiException(
            response.StatusCode,
            response.ReasonPhrase,
            body);
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
            "KiYouJyo/Eizo/0.5.11 (Windows) (https://github.com/KiYouJyo/Eizo)");
        return client;
    }

    private static string NormalizeAccessToken(
        string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var token = accessToken.Trim();
        const string bearerPrefix = "Bearer ";
        if (token.StartsWith(
                bearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            token = token[bearerPrefix.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException(
                "Bangumi access token is empty.",
                nameof(accessToken));

        return token;
    }
}


public sealed class BangumiApiException : HttpRequestException
{
    public BangumiApiException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string? responseBody)
        : base(
            BuildMessage(statusCode, reasonPhrase, responseBody),
            inner: null,
            statusCode)
    {
        ReasonPhrase = reasonPhrase?.Trim();
        ResponseBody = responseBody?.Trim() ?? string.Empty;
        (ServerTitle, ServerDescription) = ParseError(ResponseBody);
    }

    public string? ReasonPhrase { get; }
    public string ResponseBody { get; }
    public string? ServerTitle { get; }
    public string? ServerDescription { get; }

    public string DisplayDetail
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ServerTitle) &&
                !string.IsNullOrWhiteSpace(ServerDescription))
            {
                return string.Equals(
                    ServerTitle,
                    ServerDescription,
                    StringComparison.OrdinalIgnoreCase)
                    ? ServerDescription
                    : ServerTitle + ": " + ServerDescription;
            }

            if (!string.IsNullOrWhiteSpace(ServerDescription))
                return ServerDescription;
            if (!string.IsNullOrWhiteSpace(ServerTitle))
                return ServerTitle;
            if (!string.IsNullOrWhiteSpace(ResponseBody))
                return Compact(ResponseBody, 240);
            if (!string.IsNullOrWhiteSpace(ReasonPhrase))
                return ReasonPhrase;

            return "Unknown Bangumi API error";
        }
    }

    private static string BuildMessage(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string? responseBody)
    {
        var status = ((int)statusCode).ToString(CultureInfo.InvariantCulture);
        var reason = string.IsNullOrWhiteSpace(reasonPhrase)
            ? statusCode.ToString()
            : reasonPhrase.Trim();
        var (_, description) = ParseError(responseBody?.Trim() ?? string.Empty);

        return string.IsNullOrWhiteSpace(description)
            ? $"Bangumi API returned HTTP {status} {reason}."
            : $"Bangumi API returned HTTP {status} {reason}: {description}";
    }

    private static (string? Title, string? Description)
        ParseError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return (null, null);

        try
        {
            using var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return (null, null);

            var root = json.RootElement;
            var title = ReadString(root, "title") ?? ReadString(root, "error");
            var description =
                ReadString(root, "description") ??
                ReadString(root, "message") ??
                ReadString(root, "detail");

            if (string.IsNullOrWhiteSpace(description) &&
                root.TryGetProperty("details", out var details))
            {
                description = details.ValueKind switch
                {
                    JsonValueKind.String => details.GetString(),
                    JsonValueKind.Object =>
                        ReadString(details, "error") ??
                        ReadString(details, "message"),
                    _ => null,
                };
            }

            return (
                string.IsNullOrWhiteSpace(title) ? null : Compact(title, 120),
                string.IsNullOrWhiteSpace(description) ? null : Compact(description, 240));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? ReadString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }

    private static string Compact(string value, int maxLength)
    {
        var normalized = string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "…";
    }
}
