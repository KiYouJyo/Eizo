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
