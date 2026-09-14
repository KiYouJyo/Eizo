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

    private const int PageSize = 50;

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
        ValidateSeason(year, startMonth);

        var requests = Enumerable.Range(startMonth, 3)
            .Select(month =>
                GetAnimePageAsync(
                    $"season:{year}:{month}",
                    SeasonCacheLifetime,
                    sort: "date",
                    year,
                    month,
                    category: 1,
                    offset: 0,
                    forceRefresh,
                    cancellationToken))
            .ToArray();

        var pages = await Task.WhenAll(requests);

        var items = pages
            .SelectMany(static result => result.Value.Items)
            .GroupBy(static item => item.Id)
            .Select(static group => group.First())
            .OrderBy(static item => item.AirDate, StringComparer.Ordinal)
            .ThenBy(static item => item.NativeTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new BangumiLoadResult<BangumiSeasonSnapshot>(
            new BangumiSeasonSnapshot(
                year,
                startMonth,
                items),
            IsFromCache: pages.All(static result => result.IsFromCache),
            IsStale: pages.Any(static result => result.IsStale),
            FetchedAtUtc: pages.Min(static result => result.FetchedAtUtc));
    }

    public async Task<BangumiSubjectPage> SearchAnimeAsync(
        string keyword,
        int limit = 12,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var payload = await _client.SearchAnimeAsync(
            keyword,
            limit,
            offset,
            cancellationToken);

        return BangumiJsonParser.ParsePagedSubjectPage(payload);
    }

    public Task<BangumiLoadResult<BangumiSubjectPage>>
        GetRankedAnimeAsync(
            int offset = 0,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default) =>
        GetAnimePageAsync(
            $"ranked-anime:{offset}",
            RankingCacheLifetime,
            sort: "rank",
            year: null,
            month: null,
            category: null,
            offset,
            forceRefresh,
            cancellationToken);

    public Task<BangumiLoadResult<BangumiSubjectPage>>
        GetRankedAnimeForYearAsync(
            int year,
            int offset = 0,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year));

        return GetAnimePageAsync(
            $"ranked-anime:year:{year}:{offset}",
            RankingCacheLifetime,
            sort: "rank",
            year,
            month: null,
            category: null,
            offset,
            forceRefresh,
            cancellationToken);
    }

    public async Task<BangumiLoadResult<BangumiSubjectPage>>
        GetRankedAnimeForSeasonAsync(
            int year,
            int startMonth,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        ValidateSeason(year, startMonth);

        var requests = Enumerable.Range(startMonth, 3)
            .Select(month =>
                GetAnimePageAsync(
                    $"ranked-anime:season:{year}:{month}",
                    RankingCacheLifetime,
                    sort: "rank",
                    year,
                    month,
                    category: 1,
                    offset: 0,
                    forceRefresh,
                    cancellationToken))
            .ToArray();

        var pages = await Task.WhenAll(requests);

        var items = pages
            .SelectMany(static result => result.Value.Items)
            .GroupBy(static item => item.Id)
            .Select(static group => group.First())
            .OrderBy(static item => item.Rank <= 0 ? int.MaxValue : item.Rank)
            .ThenByDescending(static item => item.Score)
            .ThenBy(static item => item.NativeTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new BangumiLoadResult<BangumiSubjectPage>(
            new BangumiSubjectPage(
                Total: items.Length,
                Limit: items.Length,
                Offset: 0,
                Items: items),
            IsFromCache: pages.All(static result => result.IsFromCache),
            IsStale: pages.Any(static result => result.IsStale),
            FetchedAtUtc: pages.Min(static result => result.FetchedAtUtc));
    }

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

    public async Task<BangumiLoadResult<BangumiSubjectCredits>>
        GetSubjectCreditsAsync(
            int subjectId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
    {
        if (subjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(subjectId));

        var charactersTask = GetCachedAsync(
            $"subject-characters:{subjectId}",
            SubjectCacheLifetime,
            ct => _client.GetSubjectCharactersAsync(
                subjectId,
                ct),
            BangumiJsonParser.ParseSubjectCharacters,
            forceRefresh,
            cancellationToken);

        var personsTask = GetCachedAsync(
            $"subject-persons:{subjectId}",
            SubjectCacheLifetime,
            ct => _client.GetSubjectPersonsAsync(
                subjectId,
                ct),
            BangumiJsonParser.ParseSubjectPersons,
            forceRefresh,
            cancellationToken);

        await Task.WhenAll(
            charactersTask,
            personsTask);

        var characters = await charactersTask;
        var persons = await personsTask;

        return new BangumiLoadResult<BangumiSubjectCredits>(
            new BangumiSubjectCredits(
                characters.Value,
                persons.Value),
            IsFromCache:
                characters.IsFromCache &&
                persons.IsFromCache,
            IsStale:
                characters.IsStale ||
                persons.IsStale,
            FetchedAtUtc:
                characters.FetchedAtUtc < persons.FetchedAtUtc
                    ? characters.FetchedAtUtc
                    : persons.FetchedAtUtc);
    }

    public async Task<BangumiUserProfile> GetMyselfAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var payload = await _client.GetMyselfAsync(
            accessToken,
            cancellationToken);
        return BangumiJsonParser.ParseUserProfile(payload);
    }

    public Task<BangumiUserCollectionPage>
        GetFollowingAsync(
            string accessToken,
            string userName,
            int offset = 0,
            CancellationToken cancellationToken = default) =>
        GetUserCollectionAsync(
            accessToken,
            userName,
            BangumiCollectionType.Doing,
            offset,
            cancellationToken);

    public async Task<BangumiUserCollectionPage>
        GetUserCollectionAsync(
            string accessToken,
            string userName,
            BangumiCollectionType type,
            int offset = 0,
            CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var payload =
            await _client.GetUserCollectionsAsync(
                userName,
                type,
                PageSize,
                offset,
                accessToken,
                cancellationToken);

        return BangumiJsonParser.ParseUserCollectionPage(
            payload);
    }


    public static int GetSeasonStartMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        return ((month - 1) / 3 * 3) + 1;
    }

    public static (int Year, int StartMonth) ShiftSeason(
        int year,
        int startMonth,
        int delta)
    {
        ValidateSeason(year, startMonth);

        var index =
            (year * 4) +
            ((startMonth - 1) / 3) +
            delta;

        var shiftedYear = Math.DivRem(index, 4, out var quarterIndex);
        if (quarterIndex < 0)
        {
            quarterIndex += 4;
            shiftedYear--;
        }

        return (
            shiftedYear,
            (quarterIndex * 3) + 1);
    }

    private Task<BangumiLoadResult<BangumiSubjectPage>>
        GetAnimePageAsync(
            string cacheKey,
            TimeSpan lifetime,
            string sort,
            int? year,
            int? month,
            int? category,
            int offset,
            bool forceRefresh,
            CancellationToken cancellationToken)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return GetCachedAsync(
            cacheKey,
            lifetime,
            ct => _client.GetAnimeAsync(
                PageSize,
                offset,
                sort,
                year,
                month,
                category,
                ct),
            BangumiJsonParser.ParsePagedSubjectPage,
            forceRefresh,
            cancellationToken);
    }

    private static void ValidateSeason(
        int year,
        int startMonth)
    {
        if (year is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (startMonth is not (1 or 4 or 7 or 10))
            throw new ArgumentOutOfRangeException(nameof(startMonth));
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
