namespace Eizo.Bangumi;

public sealed class BangumiRepository
{
    private static readonly TimeSpan SeasonCacheLifetime =
        TimeSpan.FromHours(6);
    private static readonly TimeSpan RankingCacheLifetime =
        TimeSpan.FromHours(2);
    private static readonly TimeSpan CalendarCacheLifetime =
        TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SubjectCacheLifetime =
        TimeSpan.FromHours(24);

    private readonly BangumiApiClient _client;
    private readonly BangumiCacheStore _cache;

    internal BangumiRepository(
        BangumiApiClient? client = null,
        BangumiCacheStore? cache = null)
    {
        _client = client ?? new BangumiApiClient();
        _cache = cache ?? new BangumiCacheStore();
    }

    public static BangumiRepository Default { get; } = new();

    public Task<BangumiLoadResult<BangumiSeasonSnapshot>>
        GetCurrentSeasonAsync(
            DateTimeOffset now,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        var startMonth = GetSeasonStartMonth(now.Month);
        return GetSeasonAsync(
            now.Year,
            startMonth,
            forceRefresh,
            cancellationToken);
    }

    public async Task<BangumiLoadResult<BangumiSeasonSnapshot>>
        GetSeasonAsync(
            int year,
            int startMonth,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (startMonth is not (1 or 4 or 7 or 10))
            throw new ArgumentOutOfRangeException(nameof(startMonth));

        var key = $"season:{year}:{startMonth}";
        return await GetCachedAsync(
            key,
            SeasonCacheLifetime,
            ct => _client.GetSeasonAsync(
                year,
                startMonth,
                limit: 50,
                ct),
            payload =>
                new BangumiSeasonSnapshot(
                    year,
                    startMonth,
                    BangumiJsonParser.ParsePagedSubjects(payload)),
            forceRefresh,
            cancellationToken);
    }

    public Task<BangumiLoadResult<IReadOnlyList<BangumiSubjectCard>>>
        GetRankedAnimeAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default) =>
        GetCachedAsync(
            "ranked-anime",
            RankingCacheLifetime,
            ct => _client.GetRankedAnimeAsync(
                limit: 50,
                ct),
            BangumiJsonParser.ParsePagedSubjects,
            forceRefresh,
            cancellationToken);

    public Task<BangumiLoadResult<BangumiCalendarSnapshot>>
        GetCalendarAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default) =>
        GetCachedAsync(
            "calendar",
            CalendarCacheLifetime,
            _client.GetCalendarAsync,
            BangumiJsonParser.ParseCalendar,
            forceRefresh,
            cancellationToken);

    public Task<BangumiLoadResult<BangumiSubjectDetail>>
        GetSubjectAsync(
            int subjectId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId));

        return GetCachedAsync(
            $"subject:{subjectId}",
            SubjectCacheLifetime,
            ct => _client.GetSubjectAsync(
                subjectId,
                ct),
            BangumiJsonParser.ParseSubject,
            forceRefresh,
            cancellationToken);
    }

    internal static int GetSeasonStartMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        return ((month - 1) / 3 * 3) + 1;
    }

    private async Task<BangumiLoadResult<T>> GetCachedAsync<T>(
        string key,
        TimeSpan lifetime,
        Func<CancellationToken, Task<string>> fetch,
        Func<string, T> parse,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.ReadAsync(
            key,
            cancellationToken);

        if (!forceRefresh &&
            cached is not null &&
            DateTimeOffset.UtcNow - cached.FetchedAtUtc <= lifetime)
        {
            try
            {
                return new BangumiLoadResult<T>(
                    parse(cached.Payload),
                    IsFromCache: true,
                    IsStale: false,
                    cached.FetchedAtUtc);
            }
            catch
            {
                cached = null;
            }
        }

        try
        {
            var payload = await fetch(cancellationToken);
            var value = parse(payload);
            var fetchedAt = DateTimeOffset.UtcNow;

            await _cache.WriteAsync(
                key,
                new BangumiCachedPayload(
                    fetchedAt,
                    payload),
                cancellationToken);

            return new BangumiLoadResult<T>(
                value,
                IsFromCache: false,
                IsStale: false,
                fetchedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch when (cached is not null)
        {
            return new BangumiLoadResult<T>(
                parse(cached.Payload),
                IsFromCache: true,
                IsStale: true,
                cached.FetchedAtUtc);
        }
    }
}
