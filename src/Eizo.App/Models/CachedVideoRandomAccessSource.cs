using Eizo.Cache;
using Eizo.Playback;

namespace Eizo.Models;

internal sealed class CachedVideoRandomAccessSource
    : IPlaybackRandomAccessSource
{
    private const int MemoryBlockLimit = 4;

    private readonly string _groupKey;
    private readonly long _length;
    private readonly Dictionary<long, byte[]> _memoryBlocks = [];
    private readonly object _memorySync = new();

    public CachedVideoRandomAccessSource(
        string groupKey,
        long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);

        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _groupKey = groupKey;
        _length = length;
    }

    public ValueTask<PlaybackRandomAccessInfo> GetInfoAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(
            new PlaybackRandomAccessInfo(
                _length,
                CanSeek: true));

    public async ValueTask<int> ReadAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (offset >= _length ||
            buffer.Length == 0)
        {
            return 0;
        }

        var requested =
            (int)Math.Min(
                buffer.Length,
                _length - offset);

        var written = 0;
        var position = offset;

        while (written < requested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blockIndex =
                position /
                WebDavMediaCacheKeys.BlockSize;
            var blockOffset =
                checked(
                    (int)(
                        position %
                        WebDavMediaCacheKeys.BlockSize));
            var cacheKey =
                WebDavMediaCacheKeys.BuildBlockKey(
                    _groupKey,
                    blockIndex);

            byte[]? block;

            lock (_memorySync)
            {
                _memoryBlocks.TryGetValue(
                    blockIndex,
                    out block);
            }

            if (block is null)
            {
                block =
                    await global::Eizo.CacheRuntime.Store.ReadBytesAsync(
                        CacheCategory.Media,
                        cacheKey,
                        cancellationToken);

                if (block is not null)
                    RememberBlock(
                        blockIndex,
                        block);
            }

            if (block is null ||
                blockOffset >= block.Length)
            {
                throw new IOException(
                    "A required offline video cache block is missing.");
            }

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
        }

        return written;
    }

    private void RememberBlock(
        long blockIndex,
        byte[] block)
    {
        lock (_memorySync)
        {
            _memoryBlocks[blockIndex] =
                block;

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
                _memoryBlocks.Remove(key);
            }
        }
    }
}
