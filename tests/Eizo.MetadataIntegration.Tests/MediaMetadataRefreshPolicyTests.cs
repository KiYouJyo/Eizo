namespace Eizo.MetadataIntegration.Tests;

public sealed class MediaMetadataRefreshPolicyTests
{
    [Fact]
    public void SuccessfulCandidateBecomesFresh()
    {
        var candidate = Snapshot(
            MediaMetadataStatus.Resolved,
            resolved: true);
        var attemptedAt =
            new DateTimeOffset(
                2026,
                9,
                14,
                20,
                0,
                0,
                TimeSpan.Zero);

        var selected = MediaMetadataRefreshPolicy.Select(
            existing: null,
            candidate,
            forceRefresh: false,
            attemptedAt);

        Assert.NotNull(selected);
        Assert.Equal(
            MediaMetadataRefreshPolicy.Fresh,
            selected.RefreshState);
        Assert.Equal(
            attemptedAt,
            selected.LastRefreshAttemptUtc);
    }

    [Fact]
    public void NormalScanRetainsLastKnownGoodWhenRefreshFails()
    {
        var existing = Snapshot(
            MediaMetadataStatus.Resolved,
            resolved: true) with
        {
            CanonicalTitle = "Stable title",
            PosterUrl = "https://example.test/poster.jpg",
        };
        var failure = Snapshot(
            MediaMetadataStatus.Error,
            resolved: false) with
        {
            Errors =
            [
                new MetadataProviderErrorSnapshot(
                    "tmdb",
                    nameof(HttpRequestException),
                    "503 temporarily unavailable"),
            ],
        };

        var selected = MediaMetadataRefreshPolicy.Select(
            existing,
            failure,
            forceRefresh: false,
            DateTimeOffset.UtcNow);

        Assert.NotNull(selected);
        Assert.Equal(
            "Stable title",
            selected.CanonicalTitle);
        Assert.Equal(
            "https://example.test/poster.jpg",
            selected.PosterUrl);
        Assert.Equal(
            MediaMetadataRefreshPolicy.RetainedLastKnownGood,
            selected.RefreshState);
        Assert.Single(selected.RefreshErrors);
    }

    [Fact]
    public void ForcedRefreshStillRetainsLastKnownGoodOnTransportFailure()
    {
        var existing = Snapshot(
            MediaMetadataStatus.Resolved,
            resolved: true) with
        {
            CanonicalTitle = "Existing",
        };
        var failure = Snapshot(
            MediaMetadataStatus.Error,
            resolved: false) with
        {
            Errors =
            [
                new MetadataProviderErrorSnapshot(
                    "bangumi",
                    nameof(TaskCanceledException),
                    "timed out"),
            ],
        };

        var selected = MediaMetadataRefreshPolicy.Select(
            existing,
            failure,
            forceRefresh: true,
            DateTimeOffset.UtcNow);

        Assert.NotNull(selected);
        Assert.Equal(
            "Existing",
            selected.CanonicalTitle);
        Assert.Equal(
            MediaMetadataRefreshPolicy.RetainedLastKnownGood,
            selected.RefreshState);
    }

    [Fact]
    public void ForcedRefreshCanReplaceOldDataWithRealUnresolvedResult()
    {
        var existing = Snapshot(
            MediaMetadataStatus.Resolved,
            resolved: true);
        var unresolved = Snapshot(
            MediaMetadataStatus.Unresolved,
            resolved: false);

        var selected = MediaMetadataRefreshPolicy.Select(
            existing,
            unresolved,
            forceRefresh: true,
            DateTimeOffset.UtcNow);

        Assert.Same(unresolved.Status, selected!.Status);
        Assert.Equal(
            MediaMetadataRefreshPolicy.Failed,
            selected.RefreshState);
    }

    [Fact]
    public void ReusedMetadataIsMarkedWithoutChangingContent()
    {
        var existing = Snapshot(
            MediaMetadataStatus.Resolved,
            resolved: true) with
        {
            CanonicalTitle = "Reuse me",
        };

        var reused =
            MediaMetadataRefreshPolicy.MarkReused(
                existing);

        Assert.Equal(
            "Reuse me",
            reused.CanonicalTitle);
        Assert.Equal(
            MediaMetadataRefreshPolicy.Reused,
            reused.RefreshState);
    }

    private static MediaMetadataSnapshot Snapshot(
        MediaMetadataStatus status,
        bool resolved)
    {
        return new MediaMetadataSnapshot(
            RuntimeVersion: "0.2.23",
            RecognitionRuntimeVersion: "0.1.6",
            Status: status,
            Provider: resolved ? "tmdb" : null,
            ProviderSubjectId: resolved ? "1396" : null,
            SubjectKind: resolved ? "Series" : null,
            CanonicalTitle: resolved ? "Breaking Bad" : null,
            OriginalTitle: resolved ? "Breaking Bad" : null,
            LocalizedTitles:
                new Dictionary<string, string>(),
            Aliases: [],
            Overview: null,
            ReleaseDate: null,
            EpisodeCount: null,
            PosterUrl: null,
            BackdropUrl: null,
            ExternalIds:
                resolved
                    ? new Dictionary<string, string>
                    {
                        ["tmdb"] = "1396",
                    }
                    : new Dictionary<string, string>(),
            EpisodeNumber: 1,
            EpisodeTitle: null,
            EpisodeOriginalTitle: null,
            EpisodeOverview: null,
            EpisodeAirDate: null,
            EpisodeThumbnailUrl: null,
            Confidence: resolved ? 1 : 0,
            Errors: [],
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }
}
