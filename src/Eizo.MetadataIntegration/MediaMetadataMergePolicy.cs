using Core = Eizo.Metadata.Core;

namespace Eizo.MetadataIntegration;

public enum MediaMetadataMergeProfile
{
    Unknown = 0,
    Anime = 1,
    JapaneseLiveAction = 2,
    GeneralLiveAction = 3,
}

public sealed record MediaMetadataProviderSource(
    string Provider,
    Core.MetadataSubject Subject,
    Core.MetadataEpisode? Episode);

public sealed record MediaMetadataMergeResult(
    Core.MetadataSubject Subject,
    Core.MetadataEpisode? Episode,
    Dictionary<string, string> SeasonExternalIds,
    Dictionary<string, string> EpisodeExternalIds,
    Dictionary<string, string> FieldSources,
    List<string> Contributors,
    MediaMetadataMergeProfile Profile);

public static class MediaMetadataMergePolicy
{
    public static MediaMetadataMergeResult Merge(
        MediaMetadataProviderSource primary,
        IEnumerable<MediaMetadataProviderSource>? supplements = null)
    {
        ArgumentNullException.ThrowIfNull(primary);

        var sources = new[] { primary }
            .Concat(supplements ?? [])
            .Where(static source =>
                source.Subject is not null &&
                !string.IsNullOrWhiteSpace(source.Provider))
            .GroupBy(
                static source => source.Provider,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

        var profile = ResolveProfile(sources);
        var identitySources = OrderIdentitySources(
            sources,
            primary.Provider);
        var detailSources = OrderDetailSources(
            sources,
            primary.Provider,
            profile);
        var visualSources = OrderVisualSources(
            sources,
            primary.Provider,
            profile);
        var creditSources = OrderCreditSources(
            sources,
            primary.Provider,
            profile);

        var fieldSources = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var subjectExternalIds =
            MergeSubjectExternalIds(sources);
        var titles = MergeTitles(
            identitySources,
            fieldSources);

        var overview = FirstValue(
            detailSources,
            static source => source.Subject.Overview,
            "Overview",
            fieldSources);
        var releaseDate = FirstValue(
            detailSources,
            static source => source.Subject.ReleaseDate,
            "ReleaseDate",
            fieldSources);
        var episodeCount = FirstValue(
            detailSources,
            static source => source.Subject.EpisodeCount,
            "EpisodeCount",
            fieldSources);

        var artwork = new Core.MetadataArtwork(
            FirstValue(
                visualSources,
                static source => source.Subject.Artwork.PosterUrl,
                "PosterUrl",
                fieldSources),
            FirstValue(
                visualSources,
                static source => source.Subject.Artwork.BackdropUrl,
                "BackdropUrl",
                fieldSources),
            FirstValue(
                visualSources,
                static source => source.Subject.Artwork.ThumbnailUrl,
                "ThumbnailUrl",
                fieldSources));

        var genres = MergeList(
            detailSources,
            static source => source.Subject.Genres,
            "Genres",
            fieldSources);
        var companies = MergeList(
            detailSources,
            static source => source.Subject.ProductionCompanies,
            "ProductionCompanies",
            fieldSources);
        var origins = MergeList(
            detailSources,
            static source => source.Subject.OriginCountryCodes,
            "OriginCountryCodes",
            fieldSources);

        var runtime = FirstValue(
            detailSources,
            static source => source.Subject.RuntimeMinutes,
            "RuntimeMinutes",
            fieldSources);
        var status = FirstValue(
            detailSources,
            static source => source.Subject.Status,
            "ProductionStatus",
            fieldSources);
        var language = FirstValue(
            detailSources,
            static source => source.Subject.OriginalLanguage,
            "OriginalLanguage",
            fieldSources);

        var cast = FirstNonEmptyList(
            creditSources,
            static source => source.Subject.Cast,
            "Cast",
            fieldSources);
        var crew = FirstNonEmptyList(
            creditSources,
            static source => source.Subject.Crew,
            "Crew",
            fieldSources);

        var contentKind = identitySources
            .Select(static source =>
                (source.Provider, source.Subject.ContentKind))
            .FirstOrDefault(static item =>
                item.ContentKind != Core.MetadataContentKind.Unknown);
        if (contentKind.ContentKind != Core.MetadataContentKind.Unknown)
        {
            fieldSources["ContentKind"] =
                contentKind.Provider;
        }

        var subject = primary.Subject with
        {
            Titles = titles,
            Overview = overview,
            ReleaseDate = releaseDate,
            EpisodeCount = episodeCount,
            Artwork = artwork,
            ExternalIds = subjectExternalIds,
            ContentKind =
                contentKind.ContentKind == Core.MetadataContentKind.Unknown
                    ? primary.Subject.ContentKind
                    : contentKind.ContentKind,
            Genres = genres,
            ProductionCompanies = companies,
            OriginCountryCodes = origins,
            RuntimeMinutes = runtime,
            Status = status,
            OriginalLanguage = language,
            Cast = cast,
            Crew = crew,
        };

        var episode = MergeEpisode(
            sources,
            visualSources,
            detailSources,
            fieldSources);
        var seasonExternalIds =
            MergeScopedExternalIds(
                sources,
                static source =>
                    source.Episode?.ProviderSeasonId);
        var episodeExternalIds =
            MergeScopedExternalIds(
                sources,
                static source =>
                    source.Episode?.ProviderEpisodeId);

        return new MediaMetadataMergeResult(
            subject,
            episode,
            seasonExternalIds,
            episodeExternalIds,
            fieldSources,
            sources
                .Select(static source => source.Provider)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            profile);
    }

    private static Core.MetadataTitles MergeTitles(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        IDictionary<string, string> fieldSources)
    {
        var primary = sources[0];
        var primaryTitle = primary.Subject.Titles.Primary;
        fieldSources["CanonicalTitle"] = primary.Provider;

        var original = FirstValue(
            sources,
            static source => source.Subject.Titles.Original,
            "OriginalTitle",
            fieldSources);

        var localized =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources.Reverse())
        {
            foreach (var pair in source.Subject.Titles.Localized)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    localized[pair.Key] = pair.Value;
            }
        }

        var aliases = sources
            .SelectMany(static source =>
                source.Subject.Titles.Aliases)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new Core.MetadataTitles(
            primaryTitle,
            original,
            localized,
            aliases);
    }

    private static Dictionary<string, string>
        MergeSubjectExternalIds(
            IReadOnlyList<MediaMetadataProviderSource> sources)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Reverse())
        {
            foreach (var pair in source.Subject.ExternalIds)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    result[pair.Key] = pair.Value;
            }

            if (!string.IsNullOrWhiteSpace(
                    source.Subject.Id.Value))
            {
                result[source.Provider] =
                    source.Subject.Id.Value;
            }
        }

        return result;
    }

    private static Dictionary<string, string>
        MergeScopedExternalIds(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            Func<MediaMetadataProviderSource, string?> selector)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Reverse())
        {
            var value = selector(source);
            if (!string.IsNullOrWhiteSpace(value))
                result[source.Provider] = value;
        }

        return result;
    }

    private static Core.MetadataEpisode? MergeEpisode(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        IReadOnlyList<MediaMetadataProviderSource> visualSources,
        IReadOnlyList<MediaMetadataProviderSource> detailSources,
        IDictionary<string, string> fieldSources)
    {
        var episodeSources = sources
            .Where(static source =>
                source.Episode is not null)
            .ToArray();
        var episodeDetailSources = detailSources
            .Where(static source =>
                source.Episode is not null)
            .ToArray();
        var episodeVisualSources = visualSources
            .Where(static source =>
                source.Episode is not null)
            .ToArray();
        if (episodeSources.Length == 0)
            return null;

        var basis = episodeSources[0];
        var basisEpisode = basis.Episode!;

        var primaryTitle = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.Titles.Primary,
            "EpisodeTitle",
            fieldSources) ??
            basisEpisode.Titles.Primary;
        var originalTitle = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.Titles.Original,
            "EpisodeOriginalTitle",
            fieldSources);
        var overview = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.Overview,
            "EpisodeOverview",
            fieldSources);
        var airDate = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.AirDate,
            "EpisodeAirDate",
            fieldSources);
        var thumbnail = FirstValue(
            episodeVisualSources,
            static source => source.Episode!.ThumbnailUrl,
            "EpisodeThumbnailUrl",
            fieldSources);
        var seasonTitle = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.SeasonTitle,
            "SeasonTitle",
            fieldSources);
        var seasonOverview = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.SeasonOverview,
            "SeasonOverview",
            fieldSources);
        var seasonAirDate = FirstValue(
            episodeDetailSources,
            static source => source.Episode!.SeasonAirDate,
            "SeasonAirDate",
            fieldSources);
        var seasonPoster = FirstValue(
            episodeVisualSources,
            static source => source.Episode!.SeasonPosterUrl,
            "SeasonPosterUrl",
            fieldSources);

        var localized =
            new Dictionary<string, string>(
                basisEpisode.Titles.Localized,
                StringComparer.OrdinalIgnoreCase);
        var aliases = episodeSources
            .SelectMany(static source =>
                source.Episode!.Titles.Aliases)
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return basisEpisode with
        {
            Titles = new Core.MetadataTitles(
                primaryTitle,
                originalTitle,
                localized,
                aliases),
            Overview = overview,
            AirDate = airDate,
            ThumbnailUrl = thumbnail,
            SeasonTitle = seasonTitle,
            SeasonOverview = seasonOverview,
            SeasonAirDate = seasonAirDate,
            SeasonPosterUrl = seasonPoster,
        };
    }

    private static MediaMetadataMergeProfile ResolveProfile(
        IReadOnlyList<MediaMetadataProviderSource> sources)
    {
        if (sources.Any(static source =>
                source.Subject.ContentKind ==
                    Core.MetadataContentKind.Animation))
        {
            return MediaMetadataMergeProfile.Anime;
        }

        var hasLiveAction = sources.Any(static source =>
            source.Subject.ContentKind ==
                Core.MetadataContentKind.LiveAction);
        if (!hasLiveAction)
            return MediaMetadataMergeProfile.Unknown;

        var isJapanese = sources.Any(static source =>
            source.Subject.OriginCountryCodes.Any(
                static code =>
                    string.Equals(
                        code,
                        "JP",
                        StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(
                source.Subject.OriginalLanguage,
                "ja",
                StringComparison.OrdinalIgnoreCase));

        return isJapanese
            ? MediaMetadataMergeProfile.JapaneseLiveAction
            : MediaMetadataMergeProfile.GeneralLiveAction;
    }

    private static IReadOnlyList<MediaMetadataProviderSource>
        OrderIdentitySources(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            string primaryProvider) =>
        OrderSources(
            sources,
            primaryProvider);

    private static IReadOnlyList<MediaMetadataProviderSource>
        OrderDetailSources(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            string primaryProvider,
            MediaMetadataMergeProfile profile)
    {
        return profile switch
        {
            MediaMetadataMergeProfile.Anime =>
                OrderSources(
                    sources,
                    primaryProvider,
                    "bangumi",
                    "tmdb"),
            MediaMetadataMergeProfile.JapaneseLiveAction =>
                OrderSources(
                    sources,
                    primaryProvider,
                    "tmdb",
                    "bangumi"),
            MediaMetadataMergeProfile.GeneralLiveAction =>
                OrderSources(
                    sources,
                    primaryProvider,
                    "tmdb"),
            _ => OrderSources(
                sources,
                primaryProvider),
        };
    }

    private static IReadOnlyList<MediaMetadataProviderSource>
        OrderVisualSources(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            string primaryProvider,
            MediaMetadataMergeProfile profile)
    {
        return profile switch
        {
            MediaMetadataMergeProfile.Anime =>
                OrderSources(
                    sources,
                    "tmdb",
                    primaryProvider,
                    "bangumi"),
            MediaMetadataMergeProfile.JapaneseLiveAction =>
                OrderSources(
                    sources,
                    "tmdb",
                    primaryProvider,
                    "bangumi"),
            MediaMetadataMergeProfile.GeneralLiveAction =>
                OrderSources(
                    sources,
                    "tmdb",
                    primaryProvider),
            _ => OrderSources(
                sources,
                primaryProvider,
                "tmdb"),
        };
    }

    private static IReadOnlyList<MediaMetadataProviderSource>
        OrderCreditSources(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            string primaryProvider,
            MediaMetadataMergeProfile profile)
    {
        return profile switch
        {
            MediaMetadataMergeProfile.Anime =>
                OrderSources(
                    sources,
                    primaryProvider,
                    "bangumi",
                    "tmdb"),
            MediaMetadataMergeProfile.JapaneseLiveAction or
            MediaMetadataMergeProfile.GeneralLiveAction =>
                OrderSources(
                    sources,
                    "tmdb",
                    primaryProvider,
                    "bangumi"),
            _ => OrderSources(
                sources,
                primaryProvider,
                "tmdb"),
        };
    }

    private static IReadOnlyList<MediaMetadataProviderSource>
        OrderSources(
            IReadOnlyList<MediaMetadataProviderSource> sources,
            params string[] preferredProviders)
    {
        var priority = preferredProviders
            .Where(static value =>
                !string.IsNullOrWhiteSpace(value))
            .Select((provider, index) => (provider, index))
            .GroupBy(
                static item => item.provider,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.First().index,
                StringComparer.OrdinalIgnoreCase);

        return sources
            .Select((source, index) => (source, index))
            .OrderBy(item =>
                priority.TryGetValue(
                    item.source.Provider,
                    out var rank)
                    ? rank
                    : int.MaxValue)
            .ThenBy(static item => item.index)
            .Select(static item => item.source)
            .ToArray();
    }

    private static T? FirstValue<T>(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, T?> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        foreach (var source in sources)
        {
            var value = selector(source);
            if (HasValue(value))
            {
                fieldSources[field] = source.Provider;
                return value;
            }
        }

        return default;
    }

    private static IReadOnlyList<T> FirstNonEmptyList<T>(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, IReadOnlyList<T>> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        foreach (var source in sources)
        {
            var values = selector(source);
            if (values.Count == 0)
                continue;

            fieldSources[field] = source.Provider;
            return values.ToArray();
        }

        return Array.Empty<T>();
    }

    private static IReadOnlyList<string> MergeList(
        IReadOnlyList<MediaMetadataProviderSource> sources,
        Func<MediaMetadataProviderSource, IReadOnlyList<string>> selector,
        string field,
        IDictionary<string, string> fieldSources)
    {
        var contributors = new List<string>();
        var values = new List<string>();

        foreach (var source in sources)
        {
            var sourceValues = selector(source)
                .Where(static value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (sourceValues.Length == 0)
                continue;

            contributors.Add(source.Provider);
            values.AddRange(sourceValues);
        }

        if (contributors.Count > 0)
        {
            fieldSources[field] = string.Join(
                "+",
                contributors.Distinct(
                    StringComparer.OrdinalIgnoreCase));
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasValue<T>(T? value)
    {
        if (value is null)
            return false;

        if (value is string text)
            return !string.IsNullOrWhiteSpace(text);

        return true;
    }
}
