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
        string json) =>
        ParsePagedSubjectPage(json).Items;

    public static BangumiSubjectPage ParsePagedSubjectPage(
        string json)
    {
        var payload = JsonSerializer.Deserialize<PagedSubjectDto>(
            json,
            SerializerOptions)
            ?? new PagedSubjectDto();

        var items = payload.Data?
            .Where(static subject => subject.Type == 2)
            .Select(ToCard)
            .ToArray()
            ?? [];

        return new BangumiSubjectPage(
            payload.Total,
            payload.Limit,
            payload.Offset,
            items);
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

    public static IReadOnlyList<BangumiCharacterCredit>
        ParseSubjectCharacters(string json)
    {
        var payload = JsonSerializer.Deserialize<RelatedCharacterDto[]>(
            json,
            SerializerOptions)
            ?? [];

        return payload
            .Where(static item =>
                item.Id > 0 &&
                !string.IsNullOrWhiteSpace(item.Name))
            .Select(static item =>
                new BangumiCharacterCredit(
                    item.Id,
                    item.Name ?? string.Empty,
                    item.Relation ?? string.Empty,
                    ResolvePersonImage(item.Images),
                    item.Actors?
                        .Where(static actor =>
                            actor.Id > 0 &&
                            !string.IsNullOrWhiteSpace(actor.Name))
                        .Select(static actor =>
                            new BangumiCreditPerson(
                                actor.Id,
                                actor.Name ?? string.Empty,
                                string.Empty,
                                ResolvePersonImage(actor.Images)))
                        .ToArray()
                        ?? []))
            .ToArray();
    }

    public static IReadOnlyList<BangumiCreditPerson>
        ParseSubjectPersons(string json)
    {
        var payload = JsonSerializer.Deserialize<RelatedPersonDto[]>(
            json,
            SerializerOptions)
            ?? [];

        return payload
            .Where(static item =>
                item.Id > 0 &&
                !string.IsNullOrWhiteSpace(item.Name))
            .Select(static item =>
                new BangumiCreditPerson(
                    item.Id,
                    item.Name ?? string.Empty,
                    item.Relation ?? string.Empty,
                    ResolvePersonImage(item.Images)))
            .ToArray();
    }

    public static BangumiUserProfile ParseUserProfile(
        string json)
    {
        var user = JsonSerializer.Deserialize<UserDto>(
            json,
            SerializerOptions)
            ?? throw new JsonException(
                "Bangumi user response was empty.");

        return new BangumiUserProfile(
            user.Id,
            user.UserName ?? string.Empty,
            user.NickName ?? string.Empty,
            user.Sign ?? string.Empty,
            user.Avatar?.Large,
            user.Avatar?.Medium,
            user.Avatar?.Small);
    }

    public static BangumiUserCollectionPage ParseUserCollectionPage(
        string json)
    {
        var payload =
            JsonSerializer.Deserialize<PagedUserCollectionDto>(
                json,
                SerializerOptions)
            ?? new PagedUserCollectionDto();

        var items = payload.Data?
            .Where(static item =>
                item.Subject is { Type: 2 })
            .Select(static item =>
                new BangumiUserCollectionItem(
                    ToCard(item.Subject!),
                    Enum.IsDefined(
                        typeof(BangumiCollectionType),
                        item.Type)
                        ? (BangumiCollectionType)item.Type
                        : BangumiCollectionType.Doing,
                    item.EpisodeStatus,
                    item.Rate,
                    item.IsPrivate,
                    ParseDateTimeOffset(item.UpdatedAt)))
            .ToArray()
            ?? [];

        return new BangumiUserCollectionPage(
            payload.Total,
            payload.Limit,
            payload.Offset,
            items);
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

    private static BangumiSubjectCard ToCard(
        SlimSubjectDto subject) =>
        new(
            subject.Id,
            subject.Name ?? string.Empty,
            subject.NameCn ?? string.Empty,
            subject.ShortSummary ?? string.Empty,
            subject.Date,
            ResolvePoster(subject.Images),
            null,
            subject.Score,
            subject.Rank,
            subject.Eps,
            subject.CollectionTotal);

    private static DateTimeOffset? ParseDateTimeOffset(
        string? value) =>
        DateTimeOffset.TryParse(
            value,
            out var parsed)
            ? parsed
            : null;

    private static string? ResolvePersonImage(ImagesDto? images) =>
        FirstNonEmpty(
            images?.Large,
            images?.Medium,
            images?.Grid,
            images?.Small);

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

    private sealed class UserDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string? UserName { get; set; }

        [JsonPropertyName("nickname")]
        public string? NickName { get; set; }

        [JsonPropertyName("sign")]
        public string? Sign { get; set; }

        [JsonPropertyName("avatar")]
        public AvatarDto? Avatar { get; set; }
    }

    private sealed class AvatarDto
    {
        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }
    }

    private sealed class RelatedCharacterDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("relation")]
        public string? Relation { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }

        [JsonPropertyName("actors")]
        public List<RelatedActorDto>? Actors { get; set; }
    }

    private sealed class RelatedActorDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }
    }

    private sealed class RelatedPersonDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("relation")]
        public string? Relation { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }
    }

    private sealed class PagedUserCollectionDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("data")]
        public List<UserCollectionDto>? Data { get; set; }
    }

    private sealed class UserCollectionDto
    {
        [JsonPropertyName("subject_id")]
        public int SubjectId { get; set; }

        [JsonPropertyName("subject_type")]
        public int SubjectType { get; set; }

        [JsonPropertyName("rate")]
        public int Rate { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("ep_status")]
        public int EpisodeStatus { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("private")]
        public bool IsPrivate { get; set; }

        [JsonPropertyName("subject")]
        public SlimSubjectDto? Subject { get; set; }
    }

    private sealed class SlimSubjectDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("name_cn")]
        public string? NameCn { get; set; }

        [JsonPropertyName("short_summary")]
        public string? ShortSummary { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("images")]
        public ImagesDto? Images { get; set; }

        [JsonPropertyName("eps")]
        public int Eps { get; set; }

        [JsonPropertyName("collection_total")]
        public int CollectionTotal { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("rank")]
        public int Rank { get; set; }
    }

    private sealed class PagedSubjectDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

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
