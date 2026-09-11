namespace Eizo.Models;

internal static class MediaCatalogStoreDisplayExtensions
{
    public static IReadOnlyList<CatalogMediaItemModel> SnapshotForDisplay(
        this MediaCatalogStore catalog) =>
        catalog.Snapshot();
}
