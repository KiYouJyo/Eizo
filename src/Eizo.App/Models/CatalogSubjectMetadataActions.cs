using Eizo.MetadataIntegration;

namespace Eizo.Models;

public static class CatalogSubjectMetadataActions
{
    public static MediaIdentityBindingHint? GetBinding(
        CatalogSubjectModel subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return MediaCatalogStore.Default.GetIdentityBinding(
            subject.Media.Id);
    }

    public static async Task<CatalogSubjectModel?> RefreshAsync(
        CatalogSubjectModel subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        await MediaScanCoordinator.Default
            .ScrapeSubjectMetadataAsync(
                subject,
                cancellationToken)
            .ConfigureAwait(false);

        return FindRefreshedSubject(subject);
    }

    public static async Task<CatalogSubjectModel?> ApplyManualMatchAsync(
        CatalogSubjectModel subject,
        MediaMetadataMatchCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(candidate);

        MediaCatalogStore.Default.SetManualIdentityBinding(
            subject.Media.Id,
            candidate.Provider,
            candidate.ProviderSubjectId,
            makePrimary: true);

        return await RefreshAsync(
                subject,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<CatalogSubjectModel?> ClearManualMatchAsync(
        CatalogSubjectModel subject,
        string provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        MediaCatalogStore.Default.ClearManualIdentityBinding(
            subject.Media.Id,
            provider,
            removeExternalId: true);

        return await RefreshAsync(
                subject,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static CatalogSubjectModel? FindRefreshedSubject(
        CatalogSubjectModel previous)
    {
        var aggregation =
            CatalogSubjectAggregator.Build(
                MediaCatalogStore.Default
                    .SnapshotForDisplay());

        var refreshed = aggregation.Subjects
            .FirstOrDefault(subject =>
                string.Equals(
                    subject.Media.Id,
                    previous.Media.Id,
                    StringComparison.OrdinalIgnoreCase));
        if (refreshed is not null)
            return refreshed;

        var locations = previous.Items
            .Select(static item =>
                item.Location is null
                    ? null
                    : $"{item.Location.SourceId}|{item.Location.Locator}")
            .Where(static value =>
                value is not null)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        return aggregation.Subjects
            .FirstOrDefault(subject =>
                subject.Items.Any(item =>
                    item.Location is not null &&
                    locations.Contains(
                        $"{item.Location.SourceId}|{item.Location.Locator}")));
    }
}
