using System.Collections.Concurrent;
using Eizo.Cache;
using Eizo.Playback;

namespace Eizo.Models;

internal sealed class WebDavCachedRandomAccessSource
    : IPlaybackRandomAccessSource
{
    private const int BlockSize = 4 * 1024 * 1024;
    private const int MemoryBlockLimit = 4;
    private const int GlobalMaintenanceWriteInterval = 8;

    private readonly WebDavMediaSourceProvider _provider;
    private readonly MediaSourceDefinition _source;
    private readonly Uri _mediaUri;
    private readonly string _displayName;
    private readonly SemaphoreSlim _probeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _blockGates = new();
    private readonly ConcurrentDictionary<long, byte[]> _memoryBlocks = new();
    private readonly ConcurrentDictionary<long, byte> _prefetching = new();

    private WebDavMediaProbeResult? _probe;
    private string? _groupKey;
    private int _writesSinceGlobalMaintenance;

    public WebDavCachedRandomAccessSource(
        WebDavMediaSourceProvider provider,
        MediaSourceDefinition source,
        Uri mediaUri,
        string? displayName = null,
        WebDavMediaProbeResult? initialProbe = null)
    {
        _provider =
            provider ??
            throw new ArgumentNullException(nameof(provider));
        _source =
            source ??
            throw new ArgumentNullException(nameof(source));
        _mediaUri =
            mediaUri ??
            throw new ArgumentNullException(nameof(mediaUri));

        _displayName =
            string.IsNullOrWhiteSpace(displayName)
                ? Path.GetFileName(
                    Uri.UnescapeDataString(
                        mediaUri.AbsolutePath))
                : displayName;

        if (initialProbe is
            {
                IsAvailable: true,
                SupportsRanges: true,
                ContentLength: > 0
            })
        {
            _probe = initialProbe;
            _groupKey = BuildGroupKey(initialProbe);
        }
    }

    public async ValueTask<PlaybackRandomAccessInfo> GetInfoAsync(
        CancellationToken cancellationToken = default)
    {
        var probe =
            await EnsureProbeAsync(cancellationToken);

        return new PlaybackRandomAccessInfo(
            probe.ContentLength,
            CanSeek: true);
    }

    public async ValueTask<int> ReadAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (buffer.Length == 0)
            return 0;

        var probe =
            await EnsureProbeAsync(cancellationToken);
        var length =
            probe.ContentLength ??
            throw new IOException(
                "The WebDAV media length is unknown.");

        if (offset >= length)
            return 0;

        var requested =
            (int)Math.Min(
                buffer.Length,
                length - offset);

        var written = 0;
        var position = offset;
        long lastBlockIndex = -1;

        while (written < requested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blockIndex =
                position /
                BlockSize;
            var blockOffset =
                checked(
                    (int)(
                        position %
                        BlockSize));

            var block =
                await GetBlockAsync(
                    blockIndex,
                    probe,
                    cancellationToken);

            if (blockOffset >= block.Length)
                break;

            var take =
                Math.Min(
                    requested - written,
                    block.Length - blockOffset);

            block.AsMemory(
                    blockOffset,
                    take)
                .CopyTo(
                    buffer.Slice(
                        written,
                        take));

            written += take;
            position += take;
            lastBlockIndex = blockIndex;
        }

        if (written > 0 &&
            lastBlockIndex >= 0)
        {
            SchedulePrefetch(
                lastBlockIndex + 1,
                probe);
        }

        return written;
    }

    private async Task<WebDavMediaProbeResult> EnsureProbeAsync(
        CancellationToken cancellationToken)
    {
        if (_probe is
            {
                IsAvailable: true,
                SupportsRanges: true,
                ContentLength: > 0
            } ready)
        {
            return ready;
        }

        await _probeGate.WaitAsync(
            cancellationToken);

        try
        {
            if (_probe is
                {
                    IsAvailable: true,
                    SupportsRanges: true,
                    ContentLength: > 0
                } cached)
            {
                return cached;
            }

            var probe =
                await _provider.ProbeMediaAsync(
                    _source,
                    _mediaUri,
                    cancellationToken);

            if (!probe.IsAvailable)
            {
                throw new IOException(
                    string.IsNullOrWhiteSpace(probe.Detail)
                        ? "The WebDAV media is unavailable."
                        : probe.Detail);
            }

            if (!probe.SupportsRanges)
            {
                throw new IOException(
                    "The WebDAV server does not support byte-range playback.");
            }

            if (probe.ContentLength is not > 0)
            {
                throw new IOException(
                    "The WebDAV media length is unknown.");
            }

            _probe = probe;
            _groupKey = BuildGroupKey(probe);
            return probe;
        }
        finally
        {
            _probeGate.Release();
        }
    }

    private async Task<byte[]> GetBlockAsync(
        long blockIndex,
        WebDavMediaProbeResult probe,
        CancellationToken cancellationToken)
    {
        if (_memoryBlocks.TryGetValue(
                blockIndex,
                out var memoryHit))
        {
            return memoryHit;
        }

        var gate =
            _blockGates.GetOrAdd(
                blockIndex,
                static _ =>
                    new SemaphoreSlim(1, 1));

        await gate.WaitAsync(
            cancellationToken);

        try
        {
            if (_memoryBlocks.TryGetValue(
                    blockIndex,
                    out memoryHit))
            {
                return memoryHit;
            }

            var length =
                probe.ContentLength ??
                throw new IOException(
                    "The WebDAV media length is unknown.");
            var start =
                checked(
                    blockIndex *
                    (long)BlockSize);

            if (start >= length)
                return [];

            var expected =
                checked(
                    (int)Math.Min(
                        BlockSize,
                        length - start));

            var groupKey =
                _groupKey ??
                BuildGroupKey(probe);
            var cacheKey =
                BuildBlockKey(
                    groupKey,
                    blockIndex);

            var cached =
                await global::Eizo.CacheRuntime.Store.ReadBytesAsync(
                    CacheCategory.Media,
                    cacheKey,
                    cancellationToken);

            if (cached is not null &&
                cached.Length == expected)
            {
                RememberMemoryBlock(
                    blockIndex,
                    cached);
                return cached;
            }

            var bytes =
                await _provider.DownloadRangeAsync(
                    _source,
                    _mediaUri,
                    start,
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
                    _displayName,
                    _source.DisplayName,
                    ".blk",
                    Pinned: false,
                    GroupKey: groupKey),
                cancellationToken);

            RememberMemoryBlock(
                blockIndex,
                bytes);

            var policy =
                global::Eizo.CacheRuntime.CurrentPolicy;
            var perMediaLimit =
                policy.RemotePrecacheBytes > 0
                    ? policy.RemotePrecacheBytes
                    : CacheDefaults.RemotePrecacheBytes;

            await global::Eizo.CacheRuntime.Store.TrimGroupAsync(
                groupKey,
                perMediaLimit,
                preservePinned: policy.PreservePinned,
                cancellationToken);

            if (Interlocked.Increment(
                    ref _writesSinceGlobalMaintenance) >=
                GlobalMaintenanceWriteInterval)
            {
                Interlocked.Exchange(
                    ref _writesSinceGlobalMaintenance,
                    0);

                await global::Eizo.CacheRuntime.EnforcePolicyAsync(
                    cancellationToken);
            }

            return bytes;
        }
        finally
        {
            gate.Release();
        }
    }

    private void SchedulePrefetch(
        long blockIndex,
        WebDavMediaProbeResult probe)
    {
        var length =
            probe.ContentLength ??
            0;
        var start =
            blockIndex *
            (long)BlockSize;

        if (start >= length ||
            _memoryBlocks.ContainsKey(blockIndex) ||
            !_prefetching.TryAdd(
                blockIndex,
                0))
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await GetBlockAsync(
                        blockIndex,
                        probe,
                        CancellationToken.None);
                }
                catch
                {
                    // Read-ahead is opportunistic. Foreground reads retry normally.
                }
                finally
                {
                    _prefetching.TryRemove(
                        blockIndex,
                        out _);
                }
            });
    }

    private void RememberMemoryBlock(
        long blockIndex,
        byte[] bytes)
    {
        _memoryBlocks[blockIndex] =
            bytes;

        if (_memoryBlocks.Count <=
            MemoryBlockLimit)
        {
            return;
        }

        foreach (var key in _memoryBlocks.Keys
                     .OrderBy(key =>
                         Math.Abs(
                             key -
                             blockIndex))
                     .Skip(MemoryBlockLimit)
                     .ToArray())
        {
            _memoryBlocks.TryRemove(
                key,
                out _);
        }
    }

    private string BuildGroupKey(
        WebDavMediaProbeResult probe)
    {
        var version =
            !string.IsNullOrWhiteSpace(
                probe.EntityTag)
                ? "etag:" + probe.EntityTag
                : probe.LastModified is { } modified
                    ? "modified:" +
                      modified.UtcDateTime.Ticks.ToString(
                          System.Globalization.CultureInfo.InvariantCulture)
                    : "length:" +
                      probe.ContentLength?.ToString(
                          System.Globalization.CultureInfo.InvariantCulture);

        return string.Join(
            ":",
            "webdav-media",
            _source.Id,
            _mediaUri.AbsoluteUri,
            version);
    }

    private static string BuildBlockKey(
        string groupKey,
        long blockIndex) =>
        groupKey +
        ":block:" +
        blockIndex.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
}
