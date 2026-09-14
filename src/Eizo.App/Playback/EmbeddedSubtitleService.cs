using System.Buffers.Binary;
using System.Text;
using Eizo.Playback;

namespace Eizo.PlaybackSupport;

internal static class EmbeddedSubtitleService
{
    private const long MaxMoovBytes = 64L * 1024 * 1024;
    private const int MaxSubtitleSampleBytes = 1024 * 1024;

    private static readonly HashSet<string> BitmapCodecs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "PGS",
            "HDMV",
            "DVBS",
            "DVD",
            "SPU",
            "XSUB"
        };

    internal static bool CanRenderAsOverlay(
        PlaybackSource? source,
        SubtitleTrackInfo track)
    {
        if (source is null || BitmapCodecs.Contains(track.Codec ?? string.Empty))
            return false;

        var extension = Path.GetExtension(
            Uri.UnescapeDataString(source.Uri.AbsolutePath));

        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mov", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<SubtitleDocument?> LoadDocumentAsync(
        PlaybackSource source,
        SubtitleTrackInfo selectedTrack,
        IReadOnlyList<SubtitleTrackInfo> subtitleTracks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selectedTrack);
        ArgumentNullException.ThrowIfNull(subtitleTracks);

        if (!CanRenderAsOverlay(source, selectedTrack))
            return null;

        var reader = await OpenReaderAsync(source, cancellationToken);
        if (reader is null)
            return null;

        await using var ownedReader = reader;

        var length = await reader.GetLengthAsync(cancellationToken);
        if (length is null || length <= 0)
            return null;

        var moov = await ReadMoovAsync(
            reader,
            length.Value,
            cancellationToken);

        if (moov is null)
            return null;

        var parsedTracks = ParseSubtitleTracks(moov);
        if (parsedTracks.Count == 0)
            return null;

        var target = SelectTrack(
            parsedTracks,
            selectedTrack,
            subtitleTracks);

        if (target is null)
            return null;

        var cues = new List<SubtitleCue>(target.SampleSizes.Length);
        long decodeTime = 0;

        for (var index = 0; index < target.SampleSizes.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var duration = target.SampleDurations[index];
            var compositionOffset =
                index < target.CompositionOffsets.Length
                    ? target.CompositionOffsets[index]
                    : 0L;
            var startUnits = Math.Max(0L, decodeTime + compositionOffset);
            var endUnits = Math.Max(startUnits, decodeTime + duration);

            var text = await ReadTimedTextSampleAsync(
                reader,
                target.SampleOffsets[index],
                target.SampleSizes[index],
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(text) && endUnits > startUnits)
            {
                cues.Add(
                    new SubtitleCue(
                        ToTimeSpan(startUnits, target.Timescale),
                        ToTimeSpan(endUnits, target.Timescale),
                        text));
            }

            decodeTime += duration;
        }

        return new SubtitleDocument(cues);
    }

    private static Mp4SubtitleTrack? SelectTrack(
        IReadOnlyList<Mp4SubtitleTrack> parsedTracks,
        SubtitleTrackInfo selectedTrack,
        IReadOnlyList<SubtitleTrackInfo> subtitleTracks)
    {
        var selectedLanguage = NormalizeLanguage(selectedTrack.Language);
        if (selectedLanguage is not null)
        {
            var matches = parsedTracks
                .Where(track =>
                    LanguagesEquivalent(
                        selectedLanguage,
                        NormalizeLanguage(track.Language)))
                .ToArray();

            if (matches.Length == 1)
                return matches[0];
        }

        var ordinal = -1;
        for (var index = 0; index < subtitleTracks.Count; index++)
        {
            if (subtitleTracks[index].Id == selectedTrack.Id)
            {
                ordinal = index;
                break;
            }
        }

        if (ordinal >= 0 && ordinal < parsedTracks.Count)
            return parsedTracks[ordinal];

        return parsedTracks.Count == 1 ? parsedTracks[0] : null;
    }

    private static bool LanguagesEquivalent(string? left, string? right)
    {
        if (left is null || right is null)
            return false;

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        return (left, right) switch
        {
            ("chi", "zho") or ("zho", "chi") => true,
            ("fre", "fra") or ("fra", "fre") => true,
            ("ger", "deu") or ("deu", "ger") => true,
            _ => false
        };
    }

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var value = language.Trim().ToLowerInvariant();
        var separator = value.IndexOfAny(['-', '_']);
        if (separator > 0)
            value = value[..separator];

        return value;
    }

    private static async Task<string?> ReadTimedTextSampleAsync(
        IRangeReader reader,
        long offset,
        int size,
        CancellationToken cancellationToken)
    {
        if (size < 2 || size > MaxSubtitleSampleBytes)
            return null;

        var data = new byte[size];
        if (!await ReadExactAsync(
                reader,
                offset,
                data,
                cancellationToken))
        {
            return null;
        }

        var textLength = Math.Min(
            BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(0, 2)),
            size - 2);

        if (textLength <= 0)
            return null;

        var textBytes = data.AsSpan(2, textLength);
        string text;

        if (textBytes.Length >= 2 &&
            textBytes[0] == 0xfe &&
            textBytes[1] == 0xff)
        {
            text = Encoding.BigEndianUnicode.GetString(textBytes[2..]);
        }
        else if (textBytes.Length >= 2 &&
                 textBytes[0] == 0xff &&
                 textBytes[1] == 0xfe)
        {
            text = Encoding.Unicode.GetString(textBytes[2..]);
        }
        else
        {
            text = Encoding.UTF8.GetString(textBytes);
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim('\0', ' ', '\t', '\n');
    }

    private static TimeSpan ToTimeSpan(long units, uint timescale)
    {
        if (timescale == 0)
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds((double)units / timescale);
    }

    private static async Task<byte[]?> ReadMoovAsync(
        IRangeReader reader,
        long length,
        CancellationToken cancellationToken)
    {
        long offset = 0;

        while (offset + 8 <= length)
        {
            var header = await ReadRemoteBoxHeaderAsync(
                reader,
                offset,
                length,
                cancellationToken);

            if (header is null || header.Value.Size <= 0)
                return null;

            if (header.Value.Type == "moov")
            {
                if (header.Value.Size > MaxMoovBytes ||
                    header.Value.Size > int.MaxValue)
                {
                    return null;
                }

                var buffer = new byte[(int)header.Value.Size];
                return await ReadExactAsync(
                    reader,
                    offset,
                    buffer,
                    cancellationToken)
                    ? buffer
                    : null;
            }

            offset += header.Value.Size;
        }

        return null;
    }

    private static async Task<RemoteBox?> ReadRemoteBoxHeaderAsync(
        IRangeReader reader,
        long offset,
        long length,
        CancellationToken cancellationToken)
    {
        var header = new byte[16];
        var read = await reader.ReadAsync(
            offset,
            header.AsMemory(0, 8),
            cancellationToken);

        if (read < 8)
            return null;

        var size32 = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
        var type = Encoding.ASCII.GetString(header, 4, 4);

        long size;
        var headerSize = 8;

        if (size32 == 1)
        {
            if (!await ReadExactAsync(
                    reader,
                    offset + 8,
                    header.AsMemory(8, 8),
                    cancellationToken))
            {
                return null;
            }

            size = checked((long)BinaryPrimitives.ReadUInt64BigEndian(
                header.AsSpan(8, 8)));
            headerSize = 16;
        }
        else if (size32 == 0)
        {
            size = length - offset;
        }
        else
        {
            size = size32;
        }

        if (size < headerSize || offset + size > length)
            return null;

        return new RemoteBox(offset, size, type);
    }

    private static List<Mp4SubtitleTrack> ParseSubtitleTracks(byte[] moov)
    {
        var root = ReadBox(moov, 0, moov.Length);
        if (root is null || root.Value.Type != "moov")
            return [];

        var tracks = new List<Mp4SubtitleTrack>();

        foreach (var trak in EnumerateChildren(moov, root.Value))
        {
            if (trak.Type != "trak")
                continue;

            var parsed = ParseSubtitleTrack(moov, trak);
            if (parsed is not null)
                tracks.Add(parsed);
        }

        return tracks;
    }

    private static Mp4SubtitleTrack? ParseSubtitleTrack(
        byte[] data,
        Box trak)
    {
        var mdia = FindChild(data, trak, "mdia");
        if (mdia is null)
            return null;

        var hdlr = FindChild(data, mdia.Value, "hdlr");
        var mdhd = FindChild(data, mdia.Value, "mdhd");
        var minf = FindChild(data, mdia.Value, "minf");

        if (hdlr is null || mdhd is null || minf is null)
            return null;

        var handler = ReadHandlerType(data, hdlr.Value);
        if (handler is not ("text" or "sbtl" or "subt" or "clcp"))
            return null;

        var stbl = FindChild(data, minf.Value, "stbl");
        if (stbl is null)
            return null;

        var stsd = FindChild(data, stbl.Value, "stsd");
        var stts = FindChild(data, stbl.Value, "stts");
        var stsc = FindChild(data, stbl.Value, "stsc");
        var stsz = FindChild(data, stbl.Value, "stsz");
        var stco = FindChild(data, stbl.Value, "stco");
        var co64 = FindChild(data, stbl.Value, "co64");

        if (stsd is null || stts is null || stsc is null ||
            stsz is null || (stco is null && co64 is null))
        {
            return null;
        }

        var codec = ReadSampleEntryCodec(data, stsd.Value);
        if (codec is not ("tx3g" or "text"))
            return null;

        var (timescale, language) = ReadMediaHeader(data, mdhd.Value);
        if (timescale == 0)
            return null;

        var sampleSizes = ReadSampleSizes(data, stsz.Value);
        if (sampleSizes.Length == 0)
            return null;

        var sampleDurations = ReadSampleDurations(
            data,
            stts.Value,
            sampleSizes.Length);

        if (sampleDurations.Length != sampleSizes.Length)
            return null;

        var compositionOffsets =
            FindChild(data, stbl.Value, "ctts") is { } ctts
                ? ReadCompositionOffsets(
                    data,
                    ctts,
                    sampleSizes.Length)
                : new long[sampleSizes.Length];

        var sampleOffsets = BuildSampleOffsets(
            data,
            stsc.Value,
            stco ?? co64!.Value,
            stco is null,
            sampleSizes);

        if (sampleOffsets.Length != sampleSizes.Length)
            return null;

        return new Mp4SubtitleTrack(
            codec,
            language,
            timescale,
            sampleSizes,
            sampleOffsets,
            sampleDurations,
            compositionOffsets);
    }

    private static string? ReadHandlerType(byte[] data, Box box)
    {
        var offset = box.DataOffset + 8;
        return offset + 4 <= box.End
            ? Encoding.ASCII.GetString(data, offset, 4)
            : null;
    }

    private static string? ReadSampleEntryCodec(byte[] data, Box stsd)
    {
        var entryOffset = stsd.DataOffset + 8;
        if (entryOffset + 8 > stsd.End)
            return null;

        return Encoding.ASCII.GetString(data, entryOffset + 4, 4);
    }

    private static (uint Timescale, string? Language) ReadMediaHeader(
        byte[] data,
        Box mdhd)
    {
        if (mdhd.DataOffset + 4 > mdhd.End)
            return (0, null);

        var version = data[mdhd.DataOffset];
        var timescaleOffset = mdhd.DataOffset + (version == 1 ? 20 : 12);
        var languageOffset = mdhd.DataOffset + (version == 1 ? 32 : 20);

        if (timescaleOffset + 4 > mdhd.End ||
            languageOffset + 2 > mdhd.End)
        {
            return (0, null);
        }

        var timescale = ReadUInt32(data, timescaleOffset);
        var packed = ReadUInt16(data, languageOffset);
        return (timescale, DecodeLanguage(packed));
    }

    private static string? DecodeLanguage(ushort packed)
    {
        if (packed == 0)
            return null;

        Span<char> chars = stackalloc char[3];
        chars[0] = (char)(((packed >> 10) & 0x1f) + 0x60);
        chars[1] = (char)(((packed >> 5) & 0x1f) + 0x60);
        chars[2] = (char)((packed & 0x1f) + 0x60);

        if (chars[0] is < 'a' or > 'z' ||
            chars[1] is < 'a' or > 'z' ||
            chars[2] is < 'a' or > 'z')
        {
            return null;
        }

        return new string(chars);
    }

    private static int[] ReadSampleSizes(byte[] data, Box stsz)
    {
        var offset = stsz.DataOffset;
        if (offset + 12 > stsz.End)
            return [];

        var fixedSize = ReadUInt32(data, offset + 4);
        var count = ReadUInt32(data, offset + 8);
        if (count == 0 || count > 1_000_000)
            return [];

        var sizes = new int[(int)count];

        if (fixedSize != 0)
        {
            if (fixedSize > int.MaxValue)
                return [];

            Array.Fill(sizes, (int)fixedSize);
            return sizes;
        }

        var cursor = offset + 12;
        if ((long)cursor + (long)count * 4 > stsz.End)
            return [];

        for (var index = 0; index < sizes.Length; index++)
        {
            var size = ReadUInt32(data, cursor);
            if (size > int.MaxValue)
                return [];

            sizes[index] = (int)size;
            cursor += 4;
        }

        return sizes;
    }

    private static long[] ReadSampleDurations(
        byte[] data,
        Box stts,
        int sampleCount)
    {
        var offset = stts.DataOffset;
        if (offset + 8 > stts.End)
            return [];

        var entryCount = ReadUInt32(data, offset + 4);
        var cursor = offset + 8;
        var durations = new long[sampleCount];
        var sampleIndex = 0;

        for (var entry = 0u; entry < entryCount; entry++)
        {
            if (cursor + 8 > stts.End)
                return [];

            var count = ReadUInt32(data, cursor);
            var delta = ReadUInt32(data, cursor + 4);
            cursor += 8;

            for (var index = 0u;
                 index < count && sampleIndex < sampleCount;
                 index++)
            {
                durations[sampleIndex++] = delta;
            }
        }

        return sampleIndex == sampleCount ? durations : [];
    }

    private static long[] ReadCompositionOffsets(
        byte[] data,
        Box ctts,
        int sampleCount)
    {
        var offset = ctts.DataOffset;
        if (offset + 8 > ctts.End)
            return new long[sampleCount];

        var version = data[offset];
        var entryCount = ReadUInt32(data, offset + 4);
        var cursor = offset + 8;
        var offsets = new long[sampleCount];
        var sampleIndex = 0;

        for (var entry = 0u; entry < entryCount; entry++)
        {
            if (cursor + 8 > ctts.End)
                break;

            var count = ReadUInt32(data, cursor);
            var raw = ReadUInt32(data, cursor + 4);
            long value = version == 1
                ? unchecked((int)raw)
                : raw;
            cursor += 8;

            for (var index = 0u;
                 index < count && sampleIndex < sampleCount;
                 index++)
            {
                offsets[sampleIndex++] = value;
            }
        }

        return offsets;
    }

    private static long[] BuildSampleOffsets(
        byte[] data,
        Box stsc,
        Box chunkTable,
        bool is64Bit,
        IReadOnlyList<int> sampleSizes)
    {
        var stscOffset = stsc.DataOffset;
        if (stscOffset + 8 > stsc.End)
            return [];

        var entryCount = ReadUInt32(data, stscOffset + 4);
        var entries = new List<StscEntry>((int)Math.Min(entryCount, 4096u));
        var cursor = stscOffset + 8;

        for (var index = 0u; index < entryCount; index++)
        {
            if (cursor + 12 > stsc.End)
                return [];

            entries.Add(
                new StscEntry(
                    ReadUInt32(data, cursor),
                    ReadUInt32(data, cursor + 4)));
            cursor += 12;
        }

        if (entries.Count == 0)
            return [];

        var chunkOffset = chunkTable.DataOffset;
        if (chunkOffset + 8 > chunkTable.End)
            return [];

        var chunkCount = ReadUInt32(data, chunkOffset + 4);
        cursor = chunkOffset + 8;
        if (chunkCount > 1_000_000)
            return [];

        var chunks = new long[(int)chunkCount];

        for (var index = 0; index < chunks.Length; index++)
        {
            var bytes = is64Bit ? 8 : 4;
            if (cursor + bytes > chunkTable.End)
                return [];

            chunks[index] = is64Bit
                ? checked((long)ReadUInt64(data, cursor))
                : ReadUInt32(data, cursor);
            cursor += bytes;
        }

        var offsets = new long[sampleSizes.Count];
        var sampleIndex = 0;
        var stscIndex = 0;

        for (var chunkIndex = 1;
             chunkIndex <= chunks.Length && sampleIndex < sampleSizes.Count;
             chunkIndex++)
        {
            while (stscIndex + 1 < entries.Count &&
                   entries[stscIndex + 1].FirstChunk <= chunkIndex)
            {
                stscIndex++;
            }

            var samplesPerChunk = entries[stscIndex].SamplesPerChunk;
            var sampleOffset = chunks[chunkIndex - 1];

            for (var sampleInChunk = 0u;
                 sampleInChunk < samplesPerChunk &&
                 sampleIndex < sampleSizes.Count;
                 sampleInChunk++)
            {
                offsets[sampleIndex] = sampleOffset;
                sampleOffset += sampleSizes[sampleIndex];
                sampleIndex++;
            }
        }

        return sampleIndex == sampleSizes.Count ? offsets : [];
    }

    private static Box? FindChild(byte[] data, Box parent, string type) =>
        EnumerateChildren(data, parent)
            .FirstOrDefault(box => box.Type == type) is { Size: > 0 } match
                ? match
                : null;

    private static List<Box> EnumerateChildren(byte[] data, Box parent)
    {
        var boxes = new List<Box>();
        var offset = parent.DataOffset;

        while (offset + 8 <= parent.End)
        {
            var box = ReadBox(data, offset, parent.End);
            if (box is null)
                break;

            boxes.Add(box.Value);
            offset = box.Value.End;
        }

        return boxes;
    }

    private static Box? ReadBox(byte[] data, int offset, int end)
    {
        if (offset < 0 || offset + 8 > end || end > data.Length)
            return null;

        var size32 = ReadUInt32(data, offset);
        var type = Encoding.ASCII.GetString(data, offset + 4, 4);
        long size = size32;
        var headerSize = 8;

        if (size32 == 1)
        {
            if (offset + 16 > end)
                return null;

            size = checked((long)ReadUInt64(data, offset + 8));
            headerSize = 16;
        }
        else if (size32 == 0)
        {
            size = end - offset;
        }

        if (size < headerSize ||
            size > int.MaxValue ||
            offset + size > end)
        {
            return null;
        }

        return new Box(
            offset,
            (int)size,
            headerSize,
            type);
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static ulong ReadUInt64(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8));

    private static async Task<bool> ReadExactAsync(
        IRangeReader reader,
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var completed = 0;

        while (completed < buffer.Length)
        {
            var read = await reader.ReadAsync(
                offset + completed,
                buffer[completed..],
                cancellationToken);

            if (read <= 0)
                return false;

            completed += read;
        }

        return true;
    }

    private static Task<IRangeReader?> OpenReaderAsync(
        PlaybackSource source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source.RandomAccessSource is not null)
        {
            return Task.FromResult<IRangeReader?>(
                new PlaybackRangeReader(source.RandomAccessSource));
        }

        if (source.Uri.IsFile && File.Exists(source.Uri.LocalPath))
        {
            return Task.FromResult<IRangeReader?>(
                new FileRangeReader(source.Uri.LocalPath));
        }

        return Task.FromResult<IRangeReader?>(null);
    }

    private interface IRangeReader : IAsyncDisposable
    {
        ValueTask<long?> GetLengthAsync(
            CancellationToken cancellationToken);

        ValueTask<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken);
    }

    private sealed class PlaybackRangeReader(
        IPlaybackRandomAccessSource source) : IRangeReader
    {
        public async ValueTask<long?> GetLengthAsync(
            CancellationToken cancellationToken) =>
            (await source.GetInfoAsync(cancellationToken)).Length;

        public ValueTask<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken) =>
            source.ReadAsync(offset, buffer, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FileRangeReader : IRangeReader
    {
        private readonly FileStream _stream;

        internal FileRangeReader(string path)
        {
            _stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
        }

        public ValueTask<long?> GetLengthAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<long?>(_stream.Length);
        }

        public ValueTask<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken) =>
            RandomAccess.ReadAsync(
                _stream.SafeFileHandle,
                buffer,
                offset,
                cancellationToken);

        public ValueTask DisposeAsync() => _stream.DisposeAsync();
    }

    private readonly record struct RemoteBox(
        long Offset,
        long Size,
        string Type);

    private readonly record struct Box(
        int Offset,
        int Size,
        int HeaderSize,
        string Type)
    {
        internal int DataOffset => Offset + HeaderSize;
        internal int End => Offset + Size;
    }

    private readonly record struct StscEntry(
        uint FirstChunk,
        uint SamplesPerChunk);

    private sealed record Mp4SubtitleTrack(
        string Codec,
        string? Language,
        uint Timescale,
        int[] SampleSizes,
        long[] SampleOffsets,
        long[] SampleDurations,
        long[] CompositionOffsets);
}
