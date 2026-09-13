using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eizo.Bangumi;

internal static class BangumiJsonParser
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
        };

    public static IReadOnlyList<BangumiSubjectCard> ParsePagedSubjects(
        string json)
    {
        var payload = JsonSerializer.Deserialize<PagedSubjectDto>(
            json,
            SerializerOptions);

        return payload?.Data?
            .Where(static subject => subject.Type == 2)
            .Select(ToCard)
            .ToArray()
            ?? [];
    }

    public static BangumiCalendarSnapshot ParseCalendar(
        string json)
    {
        var payload = JsonSerializer.Deserialize<CalendarDayDto[]>(
            json,
            SerializerOptions)
            ?? [];

        var days = payload
            .Where(static day => day.Weekday is not null)
            .Select(day =>
                new BangumiCalendarDayModel(
                    day.Weekday!.Id,
                    day.Weekday.En ?? string.Empty,
                    day.Weekday.Cn ?? string.Empty,
                    day.Weekday.Ja ?? string.Empty,
                    day.Items?
                        .Where(static item => item.Type == 2)
                        .Select(ToCard)
                        .ToArray()
                        ?? []))
            .OrderBy(static day => day.WeekdayId)
            .ToArray();

        return new BangumiCalendarSnapshot(days);
    }

    public static BangumiSubjectDetail ParseSubject(
        string json)
    {
        var subject = JsonSerializer.Deserialize<SubjectDto>(
            json,
            SerializerOptions)
            ?? throw new JsonException(
                "Bangumi subject response was empty.");

        return new BangumiSubjectDetail(
            ToCard(subject),
            subject.MetaTags ?? [],
            subject.Tags?
                .Select(static tag => tag.Name)
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToArray()
                ?? []);
    }

    private static BangumiSubjectCard ToCard(SubjectDto subject)
    {
        var collection = subject.Collection;
        var collectionTotal =
            (collection?.Wish ?? 0) +
            (collection?.Collect ?? 0) +
            (collection?.Doing ?? 0) +
            (collection?.OnHold ?? 0) +
            (collection?.Dropped ?? 0);

        var rating = subject.Rating;
        var episodes =
            subject.TotalEpisodes > 0
                ? subject.TotalEpisodes
                : subject.Eps;

        return new BangumiSubjectCard(
            subject.Id,
            subject.Name ?? string.Empty,
            subject.NameCn ?? string.Empty,
            subject.Summary ?? string.Empty,
            subject.Date,
            ResolvePoster(subject.Images),
            subject.Platform,
            rating?.Score ?? 0,
            rating?.Rank ?? 0,
            episodes,
            collectionTotal);
    }

    private static BangumiSubjectCard ToCard(
        LegacySubjectDto subject)
    {
        var collection = subject.Collection;
        var collectionTotal =
            (collection?.Wish ?? 0) +
            (collection?.Collect ?? 0) +
            (collection?.Doing ?? 0) +
            (collection?.OnHold ?? 0) +
            (collection?.Dropped ?? 0);

        return new BangumiSubjectCard(
            subject.Id,
            subject.Name ?? string.Empty,
            subject.NameCn ?? string.Empty,
            subject.Summary ?? string.Empty,
            subject.AirDate,
            ResolvePoster(subject.Images),
            null,
            subject.Rating?.Score ?? 0,
            subject.Rank,
            subject.EpsCount > 0
                ? subject.EpsCount
                : subject.Eps,
            collectionTotal);
    }

    private static string? ResolvePoster(ImagesDto? images) =>
        FirstNonEmpty(
            images?.Large,
            images?.Common,
            images?.Medium,
            images?.Grid,
            images?.Small);

    private static string? FirstNonEmpty(
        params string?[] candidates) =>
        candidates.FirstOrDefault(
            static value => !string.IsNullOrWhiteSpace(value));

    private sealed class PagedSubjectDto
    {
        [JsonPropertyName("data")]
        public List<SubjectDto>? Data { get; set; }
    }

    private sealed class SubjectDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }

        [JsonPropertyName("eps")]
        public int Eps { get; set; }

        [JsonPropertyName("total_episodes")]
        public int TotalEpisodes { get; set; }

        [JsonPropertyName("rating")]
        public RatingDto? Rating { get; set; }

        [JsonPropertyName("collection")]
        public CollectionDto? Collection { get; set; }

        [JsonPropertyName("meta_tags")]
        public List<string>? MetaTags { get; set; }

        [JsonPropertyName("tags")]
        public List<TagDto>? Tags { get; set; }
    }

    private sealed class CalendarDayDto
    {
        [JsonPropertyName("weekday")]
        public WeekdayDto? Weekday { get; set; }

        [JsonPropertyName("items")]
        public List<LegacySubjectDto>? Items { get; set; }
    }

    private sealed class WeekdayDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("en")]
        public string? En { get; set; }

        [JsonPropertyName("cn")]
        public string? Cn { get; set; }

        [JsonPropertyName("ja")]
        public string? Ja { get; set; }
    }

    private sealed class LegacySubjectDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("air_date")]
        public string? AirDate { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }

        [JsonPropertyName("eps")]
        public int Eps { get; set; }

        [JsonPropertyName("eps_count")]
        public int EpsCount { get; set; }

        [JsonPropertyName("rating")]
        public RatingDto? Rating { get; set; }

        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("collection")]
        public CollectionDto? Collection { get; set; }
    }

    private sealed class ImagesDto
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

    private sealed class RatingDto
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    private sealed class CollectionDto
    {
        [JsonPropertyName("wish")]
        public int Wish { get; set; }

        [JsonPropertyName("collect")]
        public int Collect { get; set; }

        [JsonPropertyName("doing")]
        public int Doing { get; set; }

        [JsonPropertyName("on_hold")]
        public int OnHold { get; set; }

        [JsonPropertyName("dropped")]
        public int Dropped { get; set; }
    }

    private sealed class TagDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
