using Core = Eizo.Metadata.Core;

namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaProviderRouterTests
{
    [Fact]
    public void AnimeNearTiePrefersBangumiIdentity()
    {
        var resolution = Resolution(
            Candidate(
                "tmdb",
                "42509",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.Animation,
                0.91),
            Candidate(
                "bangumi",
                "10380",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.Animation,
                0.89));

        var route = MediaProviderRouter.Choose(
            resolution);

        Assert.Equal("bangumi", route.PrimaryProvider);
        Assert.Equal(
            "AnimeIdentityPrefersBangumi",
            route.Reason);
        Assert.Contains(
            "tmdb",
            route.FallbackProviders,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiveActionNearTiePrefersTmdb()
    {
        var resolution = Resolution(
            Candidate(
                "bangumi",
                "500",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.LiveAction,
                0.91),
            Candidate(
                "tmdb",
                "1396",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.LiveAction,
                0.89));

        var route = MediaProviderRouter.Choose(
            resolution);

        Assert.Equal("tmdb", route.PrimaryProvider);
        Assert.Equal(
            "GeneralMediaPrefersTmdb",
            route.Reason);
        Assert.Contains(
            "bangumi",
            route.FallbackProviders,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrongEvidenceLeadBeatsProviderPreference()
    {
        var resolution = Resolution(
            Candidate(
                "tmdb",
                "1396",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.Animation,
                0.95),
            Candidate(
                "bangumi",
                "100",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.Animation,
                0.84));

        var route = MediaProviderRouter.Choose(
            resolution);

        Assert.Equal("tmdb", route.PrimaryProvider);
        Assert.Equal(
            "StrongScoreLead",
            route.Reason);
    }

    [Fact]
    public void ConflictingContentKindsUseEvidenceInsteadOfPolicy()
    {
        var resolution = Resolution(
            Candidate(
                "tmdb",
                "1000",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.LiveAction,
                0.90),
            Candidate(
                "bangumi",
                "2000",
                Core.MetadataSubjectKind.Series,
                Core.MetadataContentKind.Animation,
                0.88));

        var route = MediaProviderRouter.Choose(
            resolution);

        Assert.Equal("tmdb", route.PrimaryProvider);
        Assert.Equal(
            "ProviderContentConflictUseScore",
            route.Reason);
    }

    private static Core.MetadataResolution Resolution(
        params Core.MetadataResolutionCandidate[] candidates) =>
        new(
            candidates.FirstOrDefault(),
            IsResolved: candidates.Length > 0,
            Confidence:
                candidates.FirstOrDefault()?.Score ?? 0,
            candidates,
            Array.Empty<Core.MetadataProviderError>());

    private static Core.MetadataResolutionCandidate Candidate(
        string provider,
        string id,
        Core.MetadataSubjectKind subjectKind,
        Core.MetadataContentKind contentKind,
        double score)
    {
        var candidate = new Core.MetadataSearchCandidate(
            new Core.MetadataProviderItemId(
                provider,
                id,
                subjectKind),
            new Core.MetadataTitles(
                $"{provider}-{id}",
                Original: null,
                new Dictionary<string, string>(),
                Array.Empty<string>()),
            Year: 2020,
            ProviderRank: 0)
        {
            ContentKind = contentKind,
        };

        return new Core.MetadataResolutionCandidate(
            candidate,
            score,
            Array.Empty<string>());
    }
}
