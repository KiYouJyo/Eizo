using Core = Eizo.Metadata.Core;

namespace Eizo.MetadataIntegration;

public sealed record MediaProviderRoute(
    string? PrimaryProvider,
    IReadOnlyList<string> FallbackProviders,
    string Reason)
{
    public IEnumerable<string> EnumerateProviders()
    {
        if (!string.IsNullOrWhiteSpace(PrimaryProvider))
            yield return PrimaryProvider;

        foreach (var provider in FallbackProviders)
        {
            if (!string.IsNullOrWhiteSpace(provider))
                yield return provider;
        }
    }
}

public static class MediaProviderRouter
{
    private const double AutoResolveThreshold = 0.82;
    private const double StrongEvidenceLead = 0.08;
    private const double PolicyTolerance = 0.06;

    public static MediaProviderRoute Choose(
        Core.MetadataResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        var rankedByProvider = resolution.Candidates
            .GroupBy(
                static item => item.Candidate.Id.Provider,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
                group
                    .OrderByDescending(static item => item.Score)
                    .ThenBy(static item =>
                        item.Candidate.ProviderRank)
                    .First())
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item =>
                item.Candidate.ProviderRank)
            .ThenBy(static item =>
                item.Candidate.Id.Provider,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rankedByProvider.Length == 0)
        {
            return new MediaProviderRoute(
                null,
                Array.Empty<string>(),
                "NoProviderCandidates");
        }

        var eligible = rankedByProvider
            .Where(static item =>
                item.Score >= AutoResolveThreshold)
            .ToArray();

        if (eligible.Length == 0)
        {
            return BuildFromEvidence(
                rankedByProvider[0],
                rankedByProvider,
                "BelowRoutingThreshold");
        }

        var top = eligible[0];
        var second = eligible.Skip(1).FirstOrDefault();

        if (second is null)
        {
            return BuildFromEvidence(
                top,
                rankedByProvider,
                "SingleEligibleProvider");
        }

        if (top.Score - second.Score >= StrongEvidenceLead)
        {
            return BuildFromEvidence(
                top,
                rankedByProvider,
                "StrongScoreLead");
        }

        var bangumi = eligible.FirstOrDefault(
            static item =>
                string.Equals(
                    item.Candidate.Id.Provider,
                    "bangumi",
                    StringComparison.OrdinalIgnoreCase));
        var tmdb = eligible.FirstOrDefault(
            static item =>
                string.Equals(
                    item.Candidate.Id.Provider,
                    "tmdb",
                    StringComparison.OrdinalIgnoreCase));

        if (bangumi is not null &&
            tmdb is not null)
        {
            var bangumiKind =
                bangumi.Candidate.ContentKind;
            var tmdbKind =
                tmdb.Candidate.ContentKind;

            if (bangumiKind != Core.MetadataContentKind.Unknown &&
                tmdbKind != Core.MetadataContentKind.Unknown &&
                bangumiKind != tmdbKind)
            {
                return BuildFromEvidence(
                    top,
                    rankedByProvider,
                    "ProviderContentConflictUseScore");
            }

            if (bangumiKind == Core.MetadataContentKind.Animation &&
                tmdbKind is
                    Core.MetadataContentKind.Animation or
                    Core.MetadataContentKind.Unknown &&
                bangumi.Score + PolicyTolerance >= tmdb.Score)
            {
                return BuildFromEvidence(
                    bangumi,
                    rankedByProvider,
                    "AnimeIdentityPrefersBangumi");
            }

            if (tmdbKind == Core.MetadataContentKind.LiveAction &&
                bangumiKind is
                    Core.MetadataContentKind.LiveAction or
                    Core.MetadataContentKind.Unknown &&
                tmdb.Score + PolicyTolerance >= bangumi.Score)
            {
                return BuildFromEvidence(
                    tmdb,
                    rankedByProvider,
                    "GeneralMediaPrefersTmdb");
            }

            if (top.Candidate.Id.Kind ==
                    Core.MetadataSubjectKind.Movie &&
                bangumiKind != Core.MetadataContentKind.Animation &&
                tmdb.Score + PolicyTolerance >= top.Score)
            {
                return BuildFromEvidence(
                    tmdb,
                    rankedByProvider,
                    "GeneralMoviePrefersTmdb");
            }
        }

        return BuildFromEvidence(
            top,
            rankedByProvider,
            "HighestScoredProvider");
    }

    private static MediaProviderRoute BuildFromEvidence(
        Core.MetadataResolutionCandidate primary,
        IReadOnlyList<Core.MetadataResolutionCandidate> ranked)
    {
        return BuildFromEvidence(
            primary,
            ranked,
            "HighestScoredProvider");
    }

    private static MediaProviderRoute BuildFromEvidence(
        Core.MetadataResolutionCandidate primary,
        IReadOnlyList<Core.MetadataResolutionCandidate> ranked,
        string reason)
    {
        var provider = primary.Candidate.Id.Provider;
        var fallbacks = ranked
            .Select(static item =>
                item.Candidate.Id.Provider)
            .Where(candidate =>
                !string.Equals(
                    candidate,
                    provider,
                    StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MediaProviderRoute(
            provider,
            fallbacks,
            reason);
    }
}
