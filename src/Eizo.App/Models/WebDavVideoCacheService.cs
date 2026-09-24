using Eizo.Cache;

namespace Eizo.Models;

internal sealed record WebDavVideoCacheProgress(
    string GroupKey,
    long CompletedBytes,
    long TotalBytes,
    long CompletedBlocks,
    long TotalBlocks,
    long NetworkDownloadedBytes);

internal sealed record WebDavVideoCacheResult(
    string GroupKey,
    string Path,
    long SizeBytes,
    long BlockCount);

internal sealed class WebDavVideoCacheService
{
    private const string ManualStagingFolder =
        "manual-video-staging";

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
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? waitForResume = null,
        string? displayMeta = null)
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
        var automaticGroupKey =
            WebDavMediaCacheKeys.BuildGroupKey(
                source,
                mediaUri,
                probe);
        var manualGroupKey =
            WebDavMediaCacheKeys.BuildManualGroupKey(
                source,
                mediaUri,
                probe);
        var manualFileKey =
            WebDavMediaCacheKeys.BuildManualFileKey(
                manualGroupKey);
        var blockCount =
            WebDavMediaCacheKeys.BlockCount(
                contentLength);

        var existingPath =
            await global::Eizo.CacheRuntime.Store.TryGetPathAsync(
                CacheCategory.Media,
                manualFileKey,
                cancellationToken);

        if (existingPath is not null &&
            new FileInfo(existingPath).Length ==
            contentLength)
        {
            progress?.Report(
                new WebDavVideoCacheProgress(
                    manualGroupKey,
                    contentLength,
                    contentLength,
                    blockCount,
                    blockCount,
                    NetworkDownloadedBytes: 0));

            return new WebDavVideoCacheResult(
                manualGroupKey,
                existingPath,
                contentLength,
                blockCount);
        }

        var stagingRoot =
            Path.Combine(
                global::Eizo.CacheRuntime.Store.RootPath,
                ManualStagingFolder);
        Directory.CreateDirectory(
            stagingRoot);
        CleanupStaleStagingFiles(
            stagingRoot);

        var temporaryPath =
            Path.Combine(
                stagingRoot,
                Guid.NewGuid().ToString("N") +
                ".part");

        long completedBytes = 0;
        long networkDownloadedBytes = 0;

        try
        {
            await using (var output =
                         new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.Read,
                             1024 * 1024,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan))
            {
                for (long blockIndex = 0;
                     blockIndex < blockCount;
                     blockIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (waitForResume is not null)
                    {
                        await waitForResume(
                            cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var expected =
                        WebDavMediaCacheKeys.ExpectedBlockLength(
                            contentLength,
                            blockIndex);
                    var automaticCacheKey =
                        WebDavMediaCacheKeys.BuildBlockKey(
                            automaticGroupKey,
                            blockIndex);

                    var bytes =
                        await global::Eizo.CacheRuntime.Store.ReadBytesAsync(
                            CacheCategory.Media,
                            automaticCacheKey,
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
                                $"WebDAV returned {bytes.Length} bytes for a {expected}-byte media range.");
                        }

                        networkDownloadedBytes +=
                            bytes.Length;
                    }

                    await output.WriteAsync(
                        bytes,
                        cancellationToken);

                    completedBytes += expected;
                    progress?.Report(
                        new WebDavVideoCacheProgress(
                            manualGroupKey,
                            completedBytes,
                            contentLength,
                            blockIndex + 1,
                            blockCount,
                            networkDownloadedBytes));
                }

                await output.FlushAsync(
                    cancellationToken);
            }

            var imported =
                await global::Eizo.CacheRuntime.Store.ImportFileAsync(
                    CacheCategory.Media,
                    manualFileKey,
                    temporaryPath,
                    new CacheWriteOptions(
                        item.DisplayTitle,
                        string.IsNullOrWhiteSpace(displayMeta)
                            ? source.DisplayName
                            : displayMeta,
                        ResolveMediaExtension(mediaUri),
                        Pinned: true,
                        GroupKey: manualGroupKey),
                    cancellationToken);

            return new WebDavVideoCacheResult(
                manualGroupKey,
                imported.Path,
                imported.SizeBytes,
                blockCount);
        }
        finally
        {
            TryDelete(
                temporaryPath);
        }
    }

    private static string ResolveMediaExtension(
        Uri mediaUri)
    {
        var decodedPath =
            Uri.UnescapeDataString(
                mediaUri.AbsolutePath);
        var extension =
            Path.GetExtension(
                decodedPath);

        if (string.IsNullOrWhiteSpace(extension) ||
            extension.Length > 16 ||
            extension.Skip(1).Any(
                static character =>
                    !char.IsLetterOrDigit(character)))
        {
            return ".media";
        }

        return extension.ToLowerInvariant();
    }

    private static void CleanupStaleStagingFiles(
        string stagingRoot)
    {
        var cutoff =
            DateTimeOffset.UtcNow -
            TimeSpan.FromDays(1);

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                         stagingRoot,
                         "*.part",
                         SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) <
                        cutoff.UtcDateTime)
                    {
                        File.Delete(path);
                    }
                }
                catch (
                    Exception exception)
                    when (exception is IOException or
                          UnauthorizedAccessException)
                {
                }
            }
        }
        catch (
            Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (
            Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
        }
    }
}
