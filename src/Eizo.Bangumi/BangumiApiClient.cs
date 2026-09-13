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

    public Task<string> GetSeasonAsync(
        int year,
        int startMonth,
        int limit,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"v0/subjects?type=2&cat=1&sort=date&year={year}&month={startMonth}&limit={limit}&offset=0"),
            cancellationToken);

    public Task<string> GetRankedAnimeAsync(
        int limit,
        CancellationToken cancellationToken) =>
        GetStringAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"v0/subjects?type=2&sort=rank&limit={limit}&offset=0"),
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

    private async Task<string> GetStringAsync(
        string relativeUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            relativeUri);
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "KiYouJyo/Eizo/0.4.2 (Windows) (https://github.com/KiYouJyo/Eizo)");
        return client;
    }
}
