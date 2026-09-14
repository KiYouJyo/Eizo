using Eizo.Cache;

namespace Eizo.Models;

internal sealed record WebDavVideoCacheProgress(
    long CompletedBytes,
    long TotalBytes,
    long CompletedBlocks,
    long TotalBlocks);

internal sealed record WebDavVideoCacheResult(
    string GroupKey,
    long SizeBytes,
    long BlockCount);

internal sealed class WebDavVideoCacheService
{
    public static WebDavVideoCacheService Default { get; } = new();

    public bool CanCache(
        CatalogMediaItemModel item) =>
        item.Location is
        {
            Kind: MediaLocationKind.RemoteUri
        } location &&
        MediaSourceStore.Default.Find(
            location.SourceId) is
        {
            Kind: MediaSourceKind.WebDav
        } &&
        Uri.TryCreate(
            location.Locator,
            UriKind.Absolute,
            out _);

    public async Task<WebDavVideoCacheResult> CacheAsync(
        CatalogMediaItemModel item,
        IProgress<WebDavVideoCacheProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (item.Location is not
            {
                Kind: MediaLocationKind.RemoteUri
            } location ||
            MediaSourceStore.Default.Find(
                location.SourceId) is not
            {
                Kind: MediaSourceKind.WebDav
            } source ||
            !Uri.TryCreate(
                location.Locator,
                UriKind.Absolute,
                out var mediaUri))
        {
            throw new InvalidOperationException(
                "Only WebDAV media can be cached for offline playback.");
        }

        if (!MediaSourceProviderRegistry.TryGet(
                MediaSourceKind.WebDav,
                out var provider) ||
            provider is not WebDavMediaSourceProvider webDavProvider)
        {
            throw new InvalidOperationException(
                "The WebDAV provider is unavailable.");
        }

        var probe =
            await webDavProvider.ProbeMediaAsync(
                source,
                mediaUri,
                cancellationToken);

        if (!probe.IsAvailable)
        {
            throw new IOException(
                string.IsNullOrWhiteSpace(probe.Detail)
                    ? "The WebDAV media is unavailable."
                    : probe.Detail);
        }

        if (!probe.SupportsRanges ||
            probe.ContentLength is not > 0)
        {
            throw new IOException(
                "The WebDAV server does not expose a cacheable byte-range media stream.");
        }

        var contentLength =
            probe.ContentLength.Value;
        var groupKey =
            WebDavMediaCacheKeys.BuildGroupKey(
                source,
                mediaUri,
                probe);
        var blockCount =
            WebDavMediaCacheKeys.BlockCount(
                contentLength);
        long completedBytes = 0;

        for (long blockIndex = 0;
             blockIndex < blockCount;
             blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expected =
                WebDavMediaCacheKeys.ExpectedBlockLength(
                    contentLength,
                    blockIndex);
            var cacheKey =
                WebDavMediaCacheKeys.BuildBlockKey(
                    groupKey,
                    blockIndex);

            var bytes =
                await global::Eizo.CacheRuntime.Store.ReadBytesAsync(
                    CacheCategory.Media,
                    cacheKey,
                    cancellationToken);

            if (bytes is null ||
                bytes.Length != expected)
            {
                var offset =
                    checked(
                        blockIndex *
                        (long)WebDavMediaCacheKeys.BlockSize);

                bytes =
                    await webDavProvider.DownloadRangeAsync(
                        source,
                        mediaUri,
                        offset,
                        expected,
                        cancellationToken);

                if (bytes.Length != expected)
                {
                    throw new IOException(
                        $"WebDAV returned {bytes.Length} bytes for a {expected}-byte media block.");
                }

                await global::Eizo.CacheRuntime.Store.WriteBytesAsync(
                    CacheCategory.Media,
                    cacheKey,
                    bytes,
                    new CacheWriteOptions(
                        item.DisplayTitle,
                        source.DisplayName,
                        ".blk",
                        Pinned: false,
                        GroupKey: groupKey),
                    cancellationToken);
            }

            completedBytes += expected;
            progress?.Report(
                new WebDavVideoCacheProgress(
                    completedBytes,
                    contentLength,
                    blockIndex + 1,
                    blockCount));
        }

        await global::Eizo.CacheRuntime.Store.SetGroupPinnedAsync(
            groupKey,
            pinned: true,
            cancellationToken);

        return new WebDavVideoCacheResult(
            groupKey,
            contentLength,
            blockCount);
    }
}
