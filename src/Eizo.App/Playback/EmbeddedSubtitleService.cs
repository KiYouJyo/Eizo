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
        if (source is null ||
            BitmapCodecs.Contains(track.Codec ?? string.Empty) ||
            (source.RandomAccessSource is null && !source.Uri.IsFile))
        {
            return false;
        }

        var extension = Path.GetExtension(
            Uri.UnescapeDataString(source.Uri.AbsolutePath));

        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
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

        var extension = Path.GetExtension(
            Uri.UnescapeDataString(source.Uri.AbsolutePath));

        if (extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadMatroskaDocumentAsync(
                reader,
                length.Value,
                selectedTrack,
                subtitleTracks,
                cancellationToken);
        }

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
            var endUnits = Math.Max(startUnits, startUnits + duration);

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

        return cues.Count == 0
            ? null
            : new SubtitleDocument(cues);
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


    private const ulong EbmlSegmentId = 0x18538067;
    private const ulong EbmlInfoId = 0x1549A966;
    private const ulong EbmlTracksId = 0x1654AE6B;
    private const ulong EbmlClusterId = 0x1F43B675;
    private const ulong EbmlTimecodeScaleId = 0x2AD7B1;
    private const ulong EbmlTrackEntryId = 0xAE;
    private const ulong EbmlTrackNumberId = 0xD7;
    private const ulong EbmlTrackTypeId = 0x83;
    private const ulong EbmlCodecId = 0x86;
    private const ulong EbmlLanguageId = 0x22B59C;
    private const ulong EbmlLanguageIetfId = 0x22B59D;
    private const ulong EbmlClusterTimecodeId = 0xE7;
    private const ulong EbmlSimpleBlockId = 0xA3;
    private const ulong EbmlBlockGroupId = 0xA0;
    private const ulong EbmlBlockId = 0xA1;
    private const ulong EbmlBlockDurationId = 0x9B;
    private const ulong MatroskaSubtitleTrackType = 0x11;
    private const long MaxEbmlMetadataBytes = 8L * 1024 * 1024;

    private static async Task<SubtitleDocument?> LoadMatroskaDocumentAsync(
        IRangeReader reader,
        long length,
        SubtitleTrackInfo selectedTrack,
        IReadOnlyList<SubtitleTrackInfo> subtitleTracks,
        CancellationToken cancellationToken)
    {
        var segment = await FindEbmlElementAsync(
            reader,
            0,
            length,
            EbmlSegmentId,
            cancellationToken);

        if (segment is null)
            return null;

        var segmentEnd = segment.Value.End(length);
        long timecodeScale = 1_000_000;
        List<MatroskaSubtitleTrack>? parsedTracks = null;

        var cursor = segment.Value.DataOffset;
        while (cursor < segmentEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = await ReadEbmlElementAsync(
                reader,
                cursor,
                segmentEnd,
                cancellationToken);

            if (element is null)
                break;

            if (element.Value.Id == EbmlInfoId &&
                element.Value.Size is >= 0 and <= MaxEbmlMetadataBytes)
            {
                timecodeScale = await ReadMatroskaTimecodeScaleAsync(
                    reader,
                    element.Value,
                    timecodeScale,
                    cancellationToken);
            }
            else if (element.Value.Id == EbmlTracksId &&
                     element.Value.Size is >= 0 and <= MaxEbmlMetadataBytes)
            {
                parsedTracks = await ReadMatroskaSubtitleTracksAsync(
                    reader,
                    element.Value,
                    cancellationToken);
            }

            var next = element.Value.NextOffset(segmentEnd);
            if (next <= cursor)
                break;

            cursor = next;

            if (parsedTracks is not null && timecodeScale > 0)
                break;
        }

        if (parsedTracks is null || parsedTracks.Count == 0)
            return null;

        var target = SelectMatroskaTrack(
            parsedTracks,
            selectedTrack,
            subtitleTracks);

        if (target is null || !IsSupportedMatroskaTextCodec(target.CodecId))
            return null;

        var pending = new List<MatroskaCue>();
        cursor = segment.Value.DataOffset;

        while (cursor < segmentEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = await ReadEbmlElementAsync(
                reader,
                cursor,
                segmentEnd,
                cancellationToken);

            if (element is null)
                break;

            if (element.Value.Id == EbmlClusterId)
            {
                await ReadMatroskaClusterAsync(
                    reader,
                    element.Value,
                    target,
                    timecodeScale,
                    pending,
                    cancellationToken);
            }

            var next = element.Value.NextOffset(segmentEnd);
            if (next <= cursor)
                break;

            cursor = next;
        }

        if (pending.Count == 0)
            return null;

        pending.Sort(static (left, right) =>
            left.Start.CompareTo(right.Start));

        var cues = new List<SubtitleCue>(pending.Count);

        for (var index = 0; index < pending.Count; index++)
        {
            var cue = pending[index];
            var end = cue.End;

            if (end <= cue.Start)
            {
                end = index + 1 < pending.Count
                    ? pending[index + 1].Start
                    : cue.Start + TimeSpan.FromSeconds(5);
            }

            if (end <= cue.Start)
                end = cue.Start + TimeSpan.FromMilliseconds(500);

            cues.Add(new SubtitleCue(cue.Start, end, cue.Text));
        }

        return new SubtitleDocument(cues);
    }

    private static async Task<long> ReadMatroskaTimecodeScaleAsync(
        IRangeReader reader,
        EbmlElement info,
        long fallback,
        CancellationToken cancellationToken)
    {
        var end = info.End(long.MaxValue);
        var cursor = info.DataOffset;

        while (cursor < end)
        {
            var child = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            if (child.Value.Id == EbmlTimecodeScaleId &&
                child.Value.Size is > 0 and <= 8)
            {
                var value = await ReadEbmlUnsignedAsync(
                    reader,
                    child.Value,
                    cancellationToken);

                return value is > 0 and <= long.MaxValue
                    ? (long)value.Value
                    : fallback;
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;
            cursor = next;
        }

        return fallback;
    }

    private static async Task<List<MatroskaSubtitleTrack>>
        ReadMatroskaSubtitleTracksAsync(
            IRangeReader reader,
            EbmlElement tracks,
            CancellationToken cancellationToken)
    {
        var result = new List<MatroskaSubtitleTrack>();
        var end = tracks.End(long.MaxValue);
        var cursor = tracks.DataOffset;

        while (cursor < end)
        {
            var child = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            if (child.Value.Id == EbmlTrackEntryId &&
                child.Value.Size is >= 0 and <= MaxEbmlMetadataBytes)
            {
                var track = await ReadMatroskaTrackEntryAsync(
                    reader,
                    child.Value,
                    cancellationToken);

                if (track is not null &&
                    track.TrackType == MatroskaSubtitleTrackType)
                {
                    result.Add(track);
                }
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;
            cursor = next;
        }

        return result;
    }

    private static async Task<MatroskaSubtitleTrack?>
        ReadMatroskaTrackEntryAsync(
            IRangeReader reader,
            EbmlElement entry,
            CancellationToken cancellationToken)
    {
        ulong trackNumber = 0;
        ulong trackType = 0;
        string? codecId = null;
        string? language = null;

        var end = entry.End(long.MaxValue);
        var cursor = entry.DataOffset;

        while (cursor < end)
        {
            var child = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            switch (child.Value.Id)
            {
                case EbmlTrackNumberId:
                    trackNumber =
                        await ReadEbmlUnsignedAsync(
                            reader,
                            child.Value,
                            cancellationToken) ?? 0;
                    break;

                case EbmlTrackTypeId:
                    trackType =
                        await ReadEbmlUnsignedAsync(
                            reader,
                            child.Value,
                            cancellationToken) ?? 0;
                    break;

                case EbmlCodecId:
                    codecId = await ReadEbmlStringAsync(
                        reader,
                        child.Value,
                        cancellationToken);
                    break;

                case EbmlLanguageIetfId:
                    language = await ReadEbmlStringAsync(
                        reader,
                        child.Value,
                        cancellationToken) ?? language;
                    break;

                case EbmlLanguageId:
                    language ??= await ReadEbmlStringAsync(
                        reader,
                        child.Value,
                        cancellationToken);
                    break;
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;
            cursor = next;
        }

        if (trackNumber == 0 || trackType == 0 ||
            string.IsNullOrWhiteSpace(codecId))
        {
            return null;
        }

        return new MatroskaSubtitleTrack(
            trackNumber,
            trackType,
            codecId,
            language);
    }

    private static MatroskaSubtitleTrack? SelectMatroskaTrack(
        IReadOnlyList<MatroskaSubtitleTrack> parsedTracks,
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

    private static bool IsSupportedMatroskaTextCodec(string codecId) =>
        codecId.Equals("S_TEXT/UTF8", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/ASCII", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/ASS", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/SSA", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/WEBVTT", StringComparison.OrdinalIgnoreCase);

    private static async Task ReadMatroskaClusterAsync(
        IRangeReader reader,
        EbmlElement cluster,
        MatroskaSubtitleTrack target,
        long timecodeScale,
        List<MatroskaCue> cues,
        CancellationToken cancellationToken)
    {
        var end = cluster.End(long.MaxValue);
        var cursor = cluster.DataOffset;
        long clusterTimecode = 0;

        while (cursor < end)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var child = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            if (child.Value.Id == EbmlClusterTimecodeId)
            {
                clusterTimecode = (long)(
                    await ReadEbmlUnsignedAsync(
                        reader,
                        child.Value,
                        cancellationToken) ?? 0);
            }
            else if (child.Value.Id == EbmlSimpleBlockId)
            {
                var cue = await ReadMatroskaBlockAsync(
                    reader,
                    child.Value,
                    target,
                    clusterTimecode,
                    timecodeScale,
                    durationUnits: null,
                    cancellationToken);

                if (cue is not null)
                    cues.Add(cue);
            }
            else if (child.Value.Id == EbmlBlockGroupId)
            {
                var cue = await ReadMatroskaBlockGroupAsync(
                    reader,
                    child.Value,
                    target,
                    clusterTimecode,
                    timecodeScale,
                    cancellationToken);

                if (cue is not null)
                    cues.Add(cue);
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;
            cursor = next;
        }
    }

    private static async Task<MatroskaCue?> ReadMatroskaBlockGroupAsync(
        IRangeReader reader,
        EbmlElement group,
        MatroskaSubtitleTrack target,
        long clusterTimecode,
        long timecodeScale,
        CancellationToken cancellationToken)
    {
        var end = group.End(long.MaxValue);
        var cursor = group.DataOffset;
        EbmlElement? block = null;
        ulong? duration = null;

        while (cursor < end)
        {
            var child = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            if (child.Value.Id == EbmlBlockId)
                block = child.Value;
            else if (child.Value.Id == EbmlBlockDurationId)
                duration = await ReadEbmlUnsignedAsync(
                    reader,
                    child.Value,
                    cancellationToken);

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;
            cursor = next;
        }

        if (block is null)
            return null;

        return await ReadMatroskaBlockAsync(
            reader,
            block.Value,
            target,
            clusterTimecode,
            timecodeScale,
            duration,
            cancellationToken);
    }

    private static async Task<MatroskaCue?> ReadMatroskaBlockAsync(
        IRangeReader reader,
        EbmlElement block,
        MatroskaSubtitleTrack target,
        long clusterTimecode,
        long timecodeScale,
        ulong? durationUnits,
        CancellationToken cancellationToken)
    {
        if (block.Size is <= 4 or > MaxSubtitleSampleBytes)
            return null;

        var prefixLength = (int)Math.Min(block.Size, 16);
        var prefix = new byte[prefixLength];

        if (!await ReadExactAsync(
                reader,
                block.DataOffset,
                prefix,
                cancellationToken))
        {
            return null;
        }

        if (!TryReadEbmlVint(
                prefix,
                0,
                keepMarker: false,
                out var trackNumber,
                out var trackBytes))
        {
            return null;
        }

        if (trackBytes + 3 > prefix.Length)
            return null;

        if (trackNumber != target.TrackNumber)
            return null;

        var relativeTimecode = BinaryPrimitives.ReadInt16BigEndian(
            prefix.AsSpan(trackBytes, 2));
        var flags = prefix[trackBytes + 2];

        // Text subtitle blocks are normally not laced. Skipping laced payloads is
        // safer than accidentally rendering binary lacing headers as text.
        if ((flags & 0x06) != 0)
            return null;

        var payloadOffset = block.DataOffset + trackBytes + 3;
        var payloadLength = checked(
            (int)(block.Size - trackBytes - 3));

        if (payloadLength <= 0 ||
            payloadLength > MaxSubtitleSampleBytes)
        {
            return null;
        }

        var payload = new byte[payloadLength];
        if (!await ReadExactAsync(
                reader,
                payloadOffset,
                payload,
                cancellationToken))
        {
            return null;
        }

        var text = DecodeMatroskaSubtitlePayload(
            target.CodecId,
            payload);

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var absoluteUnits = Math.Max(
            0L,
            clusterTimecode + relativeTimecode);

        var start = MatroskaTimeToTimeSpan(
            absoluteUnits,
            timecodeScale);

        var end = durationUnits is > 0
            ? start + MatroskaTimeToTimeSpan(
                checked((long)durationUnits.Value),
                timecodeScale)
            : start;

        return new MatroskaCue(start, end, text);
    }

    private static string? DecodeMatroskaSubtitlePayload(
        string codecId,
        byte[] payload)
    {
        var value = Encoding.UTF8.GetString(payload)
            .Trim('\0', ' ', '\t', '\r', '\n');

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (codecId.Equals("S_TEXT/ASS", StringComparison.OrdinalIgnoreCase) ||
            codecId.Equals("S_TEXT/SSA", StringComparison.OrdinalIgnoreCase))
        {
            var fields = value.Split(',', 9);
            if (fields.Length == 9)
                value = fields[8];

            value = StripAssFormatting(value);
        }

        return value
            .Replace("\\N", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\h", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string StripAssFormatting(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inOverride = false;

        foreach (var character in value)
        {
            if (character == '{')
            {
                inOverride = true;
                continue;
            }

            if (character == '}' && inOverride)
            {
                inOverride = false;
                continue;
            }

            if (!inOverride)
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static TimeSpan MatroskaTimeToTimeSpan(
        long units,
        long timecodeScaleNanoseconds)
    {
        if (units <= 0 || timecodeScaleNanoseconds <= 0)
            return TimeSpan.Zero;

        var ticks = (double)units *
            timecodeScaleNanoseconds /
            100d;

        return TimeSpan.FromTicks(
            (long)Math.Min(ticks, TimeSpan.MaxValue.Ticks));
    }

    private static async Task<EbmlElement?> FindEbmlElementAsync(
        IRangeReader reader,
        long start,
        long end,
        ulong targetId,
        CancellationToken cancellationToken)
    {
        var cursor = start;

        while (cursor < end)
        {
            var element = await ReadEbmlElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (element is null)
                return null;

            if (element.Value.Id == targetId)
                return element;

            var next = element.Value.NextOffset(end);
            if (next <= cursor)
                return null;

            cursor = next;
        }

        return null;
    }

    private static async Task<EbmlElement?> ReadEbmlElementAsync(
        IRangeReader reader,
        long offset,
        long parentEnd,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || offset >= parentEnd)
            return null;

        var header = new byte[16];
        var toRead = (int)Math.Min(
            header.Length,
            parentEnd - offset);

        var read = await reader.ReadAsync(
            offset,
            header.AsMemory(0, toRead),
            cancellationToken);

        if (read <= 1)
            return null;

        if (!TryReadEbmlVint(
                header.AsSpan(0, read),
                0,
                keepMarker: true,
                out var id,
                out var idBytes))
        {
            return null;
        }

        if (!TryReadEbmlVint(
                header.AsSpan(0, read),
                idBytes,
                keepMarker: false,
                out var sizeValue,
                out var sizeBytes,
                out var unknownSize))
        {
            return null;
        }

        var dataOffset = offset + idBytes + sizeBytes;
        long size;

        if (unknownSize)
        {
            size = -1;
        }
        else
        {
            if (sizeValue > long.MaxValue)
                return null;

            size = (long)sizeValue;
            if (dataOffset + size > parentEnd)
                return null;
        }

        return new EbmlElement(
            id,
            offset,
            dataOffset,
            size);
    }

    private static bool TryReadEbmlVint(
        ReadOnlySpan<byte> data,
        int offset,
        bool keepMarker,
        out ulong value,
        out int length) =>
        TryReadEbmlVint(
            data,
            offset,
            keepMarker,
            out value,
            out length,
            out _);

    private static bool TryReadEbmlVint(
        ReadOnlySpan<byte> data,
        int offset,
        bool keepMarker,
        out ulong value,
        out int length,
        out bool unknownSize)
    {
        value = 0;
        length = 0;
        unknownSize = false;

        if ((uint)offset >= (uint)data.Length ||
            data[offset] == 0)
        {
            return false;
        }

        var first = data[offset];
        var mask = 0x80;

        while (length < 8 && (first & mask) == 0)
        {
            mask >>= 1;
            length++;
        }

        length++;
        if (length > 8 || offset + length > data.Length)
            return false;

        value = keepMarker
            ? first
            : (ulong)(first & (mask - 1));

        for (var index = 1; index < length; index++)
            value = (value << 8) | data[offset + index];

        if (!keepMarker)
        {
            var payloadBits = 7 * length;
            var allOnes = payloadBits == 56
                ? 0x00FFFFFFFFFFFFFFUL
                : (1UL << payloadBits) - 1UL;
            unknownSize = value == allOnes;
        }

        return true;
    }

    private static async Task<ulong?> ReadEbmlUnsignedAsync(
        IRangeReader reader,
        EbmlElement element,
        CancellationToken cancellationToken)
    {
        if (element.Size is <= 0 or > 8)
            return null;

        var data = new byte[(int)element.Size];
        if (!await ReadExactAsync(
                reader,
                element.DataOffset,
                data,
                cancellationToken))
        {
            return null;
        }

        ulong value = 0;
        foreach (var current in data)
            value = (value << 8) | current;

        return value;
    }

    private static async Task<string?> ReadEbmlStringAsync(
        IRangeReader reader,
        EbmlElement element,
        CancellationToken cancellationToken)
    {
        if (element.Size is <= 0 or > 4096)
            return null;

        var data = new byte[(int)element.Size];
        if (!await ReadExactAsync(
                reader,
                element.DataOffset,
                data,
                cancellationToken))
        {
            return null;
        }

        return Encoding.UTF8.GetString(data)
            .Trim('\0', ' ', '\t', '\r', '\n');
    }

    private readonly record struct EbmlElement(
        ulong Id,
        long Offset,
        long DataOffset,
        long Size)
    {
        internal long End(long fallbackEnd) =>
            Size < 0
                ? fallbackEnd
                : DataOffset + Size;

        internal long NextOffset(long fallbackEnd) =>
            End(fallbackEnd);
    }

    private sealed record MatroskaSubtitleTrack(
        ulong TrackNumber,
        ulong TrackType,
        string CodecId,
        string? Language);

    private sealed record MatroskaCue(
        TimeSpan Start,
        TimeSpan End,
        string Text);

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
