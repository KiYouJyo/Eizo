using System.Buffers.Binary;
using System.Text;
using Eizo.Models;
using Eizo.Playback;

namespace Eizo.PlaybackSupport;

internal static class MatroskaCueSubtitleService
{
    private const ulong SegmentId = 0x18538067;
    private const ulong SeekHeadId = 0x114D9B74;
    private const ulong SeekId = 0x4DBB;
    private const ulong SeekTargetId = 0x53AB;
    private const ulong SeekPositionId = 0x53AC;
    private const ulong InfoId = 0x1549A966;
    private const ulong TracksId = 0x1654AE6B;
    private const ulong CuesId = 0x1C53BB6B;
    private const ulong ClusterId = 0x1F43B675;

    private const ulong TimecodeScaleId = 0x2AD7B1;
    private const ulong TrackEntryId = 0xAE;
    private const ulong TrackNumberId = 0xD7;
    private const ulong TrackTypeId = 0x83;
    private const ulong CodecIdId = 0x86;
    private const ulong LanguageId = 0x22B59C;
    private const ulong LanguageIetfId = 0x22B59D;
    private const ulong SubtitleTrackType = 0x11;

    private const ulong CuePointId = 0xBB;
    private const ulong CueTimeId = 0xB3;
    private const ulong CueTrackPositionsId = 0xB7;
    private const ulong CueTrackId = 0xF7;
    private const ulong CueClusterPositionId = 0xF1;
    private const ulong CueRelativePositionId = 0xF0;
    private const ulong CueDurationId = 0xB2;
    private const ulong CueBlockNumberId = 0x5378;

    private const ulong SimpleBlockId = 0xA3;
    private const ulong BlockGroupId = 0xA0;
    private const ulong BlockId = 0xA1;
    private const ulong BlockDurationId = 0x9B;

    private const int MaxMetadataBytes = 16 * 1024 * 1024;
    private const int MaxSubtitlePayloadBytes = 2 * 1024 * 1024;
    private const int MaxClusterChildrenWithoutRelativePosition = 512;

    internal static bool CanRenderAsOverlay(
        PlaybackSource? source,
        SubtitleTrackInfo track)
    {
        if (source?.RandomAccessSource is not WebDavCachedRandomAccessSource)
            return false;

        var extension = Path.GetExtension(
            Uri.UnescapeDataString(source.Uri.AbsolutePath));

        return extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<SubtitleDocument?> LoadDocumentAsync(
        PlaybackSource source,
        SubtitleTrackInfo selectedTrack,
        IReadOnlyList<SubtitleTrackInfo> subtitleTracks,
        CancellationToken cancellationToken)
    {
        if (source.RandomAccessSource is not WebDavCachedRandomAccessSource webDav ||
            !CanRenderAsOverlay(source, selectedTrack))
        {
            return null;
        }

        var info = await webDav.GetInfoAsync(cancellationToken);
        if (info.Length is not > 0)
            return null;

        var reader = new SparseReader(webDav, info.Length.Value);
        var segment = await FindSegmentAsync(reader, cancellationToken);
        if (segment is null)
            return null;

        var locations = await ReadSegmentLocationsAsync(
            reader,
            segment.Value,
            cancellationToken);

        if (locations.TracksPosition is null ||
            locations.CuesPosition is null)
        {
            PlaybackFallbackDiagnostics.Write(
                "matroska-index-missing",
                source,
                selectedTrack,
                $"tracks={locations.TracksPosition is not null};cues={locations.CuesPosition is not null}");
            return null;
        }

        var timescale = 1_000_000L;
        if (locations.InfoPosition is long infoPosition)
        {
            var infoElement = await ReadElementAtSegmentPositionAsync(
                reader,
                segment.Value,
                infoPosition,
                cancellationToken);

            if (infoElement is { Id: InfoId })
            {
                var payload = await ReadPayloadAsync(
                    reader,
                    infoElement.Value,
                    cancellationToken);

                if (payload is not null)
                    timescale = ReadTimecodeScale(payload, timescale);
            }
        }

        var tracksElement = await ReadElementAtSegmentPositionAsync(
            reader,
            segment.Value,
            locations.TracksPosition.Value,
            cancellationToken);

        if (tracksElement is not { Id: TracksId })
            return null;

        var tracksPayload = await ReadPayloadAsync(
            reader,
            tracksElement.Value,
            cancellationToken);

        if (tracksPayload is null)
            return null;

        var parsedTracks = ParseTracks(tracksPayload);
        var target = SelectTrack(
            parsedTracks,
            selectedTrack,
            subtitleTracks);

        if (target is null || !IsSupportedTextCodec(target.CodecId))
        {
            PlaybackFallbackDiagnostics.Write(
                "matroska-track-unsupported",
                source,
                selectedTrack,
                target?.CodecId ?? "unresolved");
            return null;
        }

        var cuesElement = await ReadElementAtSegmentPositionAsync(
            reader,
            segment.Value,
            locations.CuesPosition.Value,
            cancellationToken);

        if (cuesElement is not { Id: CuesId })
            return null;

        var cuesPayload = await ReadPayloadAsync(
            reader,
            cuesElement.Value,
            cancellationToken);

        if (cuesPayload is null)
            return null;

        var cueEntries = ParseCues(
            cuesPayload,
            target.TrackNumber);

        if (cueEntries.Count == 0)
        {
            PlaybackFallbackDiagnostics.Write(
                "matroska-subtitle-cues-missing",
                source,
                selectedTrack,
                $"codec={target.CodecId}");
            return null;
        }

        var extracted = new List<SubtitleCue>(cueEntries.Count);

        for (var index = 0; index < cueEntries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cue = cueEntries[index];
            var text = await ReadCueTextAsync(
                reader,
                segment.Value,
                target,
                cue,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var start = ScaleTime(cue.Time, timescale);
            var end = cue.Duration is > 0
                ? start + ScaleTime(cue.Duration.Value, timescale)
                : index + 1 < cueEntries.Count
                    ? ScaleTime(cueEntries[index + 1].Time, timescale)
                    : start + TimeSpan.FromSeconds(7);

            if (end <= start)
                end = start + TimeSpan.FromMilliseconds(500);

            extracted.Add(new SubtitleCue(start, end, text));
        }

        PlaybackFallbackDiagnostics.Write(
            extracted.Count > 0
                ? "matroska-cue-extraction-complete"
                : "matroska-cue-extraction-empty",
            source,
            selectedTrack,
            $"codec={target.CodecId};cues={cueEntries.Count};extracted={extracted.Count}");

        return extracted.Count == 0
            ? null
            : new SubtitleDocument(extracted);
    }

    private static async Task<Element?> FindSegmentAsync(
        SparseReader reader,
        CancellationToken cancellationToken)
    {
        long cursor = 0;

        for (var count = 0; count < 8 && cursor < reader.Length; count++)
        {
            var element = await ReadElementAsync(
                reader,
                cursor,
                reader.Length,
                cancellationToken);

            if (element is null)
                return null;

            if (element.Value.Id == SegmentId)
                return element;

            var next = element.Value.NextOffset(reader.Length);
            if (next <= cursor)
                return null;

            cursor = next;
        }

        return null;
    }

    private static async Task<SegmentLocations> ReadSegmentLocationsAsync(
        SparseReader reader,
        Element segment,
        CancellationToken cancellationToken)
    {
        long? info = null;
        long? tracks = null;
        long? cues = null;

        var cursor = segment.DataOffset;
        var end = segment.End(reader.Length);

        for (var count = 0; count < 32 && cursor < end; count++)
        {
            var child = await ReadElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            var relative = child.Value.Offset - segment.DataOffset;

            if (child.Value.Id == SeekHeadId)
            {
                var payload = await ReadPayloadAsync(
                    reader,
                    child.Value,
                    cancellationToken);

                if (payload is not null)
                {
                    var seek = ParseSeekHead(payload);
                    info ??= seek.InfoPosition;
                    tracks ??= seek.TracksPosition;
                    cues ??= seek.CuesPosition;
                }
            }
            else if (child.Value.Id == InfoId)
            {
                info ??= relative;
            }
            else if (child.Value.Id == TracksId)
            {
                tracks ??= relative;
            }
            else if (child.Value.Id == CuesId)
            {
                cues ??= relative;
            }
            else if (child.Value.Id == ClusterId)
            {
                break;
            }

            if (info is not null &&
                tracks is not null &&
                cues is not null)
            {
                break;
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;

            cursor = next;
        }

        return new SegmentLocations(info, tracks, cues);
    }

    private static SegmentLocations ParseSeekHead(byte[] payload)
    {
        long? info = null;
        long? tracks = null;
        long? cues = null;

        foreach (var seek in EnumerateChildren(payload, 0, payload.Length))
        {
            if (seek.Id != SeekId)
                continue;

            ulong? targetId = null;
            ulong? position = null;

            foreach (var child in EnumerateChildren(
                         payload,
                         seek.DataOffset,
                         seek.End))
            {
                if (child.Id == SeekTargetId)
                    targetId = ReadBinaryId(payload, child);
                else if (child.Id == SeekPositionId)
                    position = ReadUnsigned(payload, child);
            }

            if (targetId is null ||
                position is null ||
                position > long.MaxValue)
            {
                continue;
            }

            var value = (long)position.Value;

            if (targetId == InfoId)
                info = value;
            else if (targetId == TracksId)
                tracks = value;
            else if (targetId == CuesId)
                cues = value;
        }

        return new SegmentLocations(info, tracks, cues);
    }

    private static long ReadTimecodeScale(
        byte[] payload,
        long fallback)
    {
        foreach (var child in EnumerateChildren(
                     payload,
                     0,
                     payload.Length))
        {
            if (child.Id != TimecodeScaleId)
                continue;

            var value = ReadUnsigned(payload, child);
            if (value is > 0 and <= long.MaxValue)
                return (long)value.Value;
        }

        return fallback;
    }

    private static List<MatroskaTrack> ParseTracks(byte[] payload)
    {
        var result = new List<MatroskaTrack>();

        foreach (var entry in EnumerateChildren(
                     payload,
                     0,
                     payload.Length))
        {
            if (entry.Id != TrackEntryId)
                continue;

            ulong number = 0;
            ulong type = 0;
            string? codec = null;
            string? language = null;

            foreach (var child in EnumerateChildren(
                         payload,
                         entry.DataOffset,
                         entry.End))
            {
                switch (child.Id)
                {
                    case TrackNumberId:
                        number = ReadUnsigned(payload, child) ?? 0;
                        break;
                    case TrackTypeId:
                        type = ReadUnsigned(payload, child) ?? 0;
                        break;
                    case CodecIdId:
                        codec = ReadString(payload, child);
                        break;
                    case LanguageIetfId:
                        language = ReadString(payload, child) ?? language;
                        break;
                    case LanguageId:
                        language ??= ReadString(payload, child);
                        break;
                }
            }

            if (number > 0 &&
                type == SubtitleTrackType &&
                !string.IsNullOrWhiteSpace(codec))
            {
                result.Add(
                    new MatroskaTrack(
                        number,
                        codec,
                        language));
            }
        }

        return result;
    }

    private static MatroskaTrack? SelectTrack(
        IReadOnlyList<MatroskaTrack> parsed,
        SubtitleTrackInfo selected,
        IReadOnlyList<SubtitleTrackInfo> visibleTracks)
    {
        var selectedLanguage = NormalizeLanguage(selected.Language);

        if (selectedLanguage is not null)
        {
            var matches = parsed
                .Where(track =>
                    LanguageMatches(
                        selectedLanguage,
                        NormalizeLanguage(track.Language)))
                .ToArray();

            if (matches.Length == 1)
                return matches[0];
        }

        var ordinal = -1;
        for (var index = 0; index < visibleTracks.Count; index++)
        {
            if (visibleTracks[index].Id == selected.Id)
            {
                ordinal = index;
                break;
            }
        }

        if (ordinal >= 0 && ordinal < parsed.Count)
            return parsed[ordinal];

        return parsed.Count == 1 ? parsed[0] : null;
    }

    private static List<CueEntry> ParseCues(
        byte[] payload,
        ulong targetTrack)
    {
        var result = new List<CueEntry>();

        foreach (var cuePoint in EnumerateChildren(
                     payload,
                     0,
                     payload.Length))
        {
            if (cuePoint.Id != CuePointId)
                continue;

            ulong? time = null;

            foreach (var child in EnumerateChildren(
                         payload,
                         cuePoint.DataOffset,
                         cuePoint.End))
            {
                if (child.Id == CueTimeId)
                {
                    time = ReadUnsigned(payload, child);
                    continue;
                }

                if (child.Id != CueTrackPositionsId || time is null)
                    continue;

                ulong? track = null;
                ulong? cluster = null;
                ulong? relative = null;
                ulong? duration = null;
                ulong? blockNumber = null;

                foreach (var position in EnumerateChildren(
                             payload,
                             child.DataOffset,
                             child.End))
                {
                    switch (position.Id)
                    {
                        case CueTrackId:
                            track = ReadUnsigned(payload, position);
                            break;
                        case CueClusterPositionId:
                            cluster = ReadUnsigned(payload, position);
                            break;
                        case CueRelativePositionId:
                            relative = ReadUnsigned(payload, position);
                            break;
                        case CueDurationId:
                            duration = ReadUnsigned(payload, position);
                            break;
                        case CueBlockNumberId:
                            blockNumber = ReadUnsigned(payload, position);
                            break;
                    }
                }

                if (track == targetTrack &&
                    cluster is not null &&
                    cluster <= long.MaxValue &&
                    time <= long.MaxValue)
                {
                    result.Add(
                        new CueEntry(
                            (long)time.Value,
                            (long)cluster.Value,
                            relative is <= long.MaxValue
                                ? (long?)relative
                                : null,
                            duration is <= long.MaxValue
                                ? (long?)duration
                                : null,
                            blockNumber is <= int.MaxValue
                                ? (int?)blockNumber
                                : null));
                }
            }
        }

        return result
            .OrderBy(static cue => cue.Time)
            .ThenBy(static cue => cue.ClusterPosition)
            .ToList();
    }

    private static async Task<string?> ReadCueTextAsync(
        SparseReader reader,
        Element segment,
        MatroskaTrack target,
        CueEntry cue,
        CancellationToken cancellationToken)
    {
        var clusterOffset = checked(
            segment.DataOffset + cue.ClusterPosition);

        var cluster = await ReadElementAsync(
            reader,
            clusterOffset,
            segment.End(reader.Length),
            cancellationToken);

        if (cluster is not { Id: ClusterId })
            return null;

        if (cue.RelativePosition is long relative)
        {
            var referencedOffset = checked(
                cluster.Value.DataOffset + relative);

            var referenced = await ReadElementAsync(
                reader,
                referencedOffset,
                cluster.Value.End(reader.Length),
                cancellationToken);

            if (referenced is not null)
            {
                return await ReadReferencedTextAsync(
                    reader,
                    referenced.Value,
                    target,
                    cancellationToken);
            }
        }

        var cursor = cluster.Value.DataOffset;
        var end = cluster.Value.End(reader.Length);
        var blockOrdinal = 0;

        for (var count = 0;
             count < MaxClusterChildrenWithoutRelativePosition &&
             cursor < end;
             count++)
        {
            var child = await ReadElementAsync(
                reader,
                cursor,
                end,
                cancellationToken);

            if (child is null)
                break;

            if (child.Value.Id is SimpleBlockId or BlockGroupId)
            {
                blockOrdinal++;

                if (cue.BlockNumber is null ||
                    cue.BlockNumber == blockOrdinal)
                {
                    var text = await ReadReferencedTextAsync(
                        reader,
                        child.Value,
                        target,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            var next = child.Value.NextOffset(end);
            if (next <= cursor)
                break;

            cursor = next;
        }

        return null;
    }

    private static async Task<string?> ReadReferencedTextAsync(
        SparseReader reader,
        Element element,
        MatroskaTrack target,
        CancellationToken cancellationToken)
    {
        if (element.Id == SimpleBlockId)
        {
            var payload = await ReadPayloadAsync(
                reader,
                element,
                cancellationToken,
                MaxSubtitlePayloadBytes);

            return payload is null
                ? null
                : DecodeBlockPayload(
                    payload,
                    target.TrackNumber,
                    target.CodecId);
        }

        if (element.Id != BlockGroupId ||
            element.Size is < 0 or > MaxSubtitlePayloadBytes)
        {
            return null;
        }

        var group = await ReadPayloadAsync(
            reader,
            element,
            cancellationToken,
            MaxSubtitlePayloadBytes);

        if (group is null)
            return null;

        foreach (var child in EnumerateChildren(
                     group,
                     0,
                     group.Length))
        {
            if (child.Id != BlockId)
                continue;

            var payload = group.AsSpan(
                child.DataOffset,
                child.End - child.DataOffset);

            return DecodeBlockPayload(
                payload,
                target.TrackNumber,
                target.CodecId);
        }

        return null;
    }

    private static string? DecodeBlockPayload(
        ReadOnlySpan<byte> payload,
        ulong expectedTrack,
        string codecId)
    {
        if (!TryReadVint(
                payload,
                0,
                keepMarker: false,
                out var track,
                out var trackBytes,
                out _))
        {
            return null;
        }

        if (track != expectedTrack ||
            trackBytes + 3 > payload.Length)
        {
            return null;
        }

        var flags = payload[trackBytes + 2];
        if ((flags & 0x06) != 0)
            return null;

        var textBytes = payload[(trackBytes + 3)..];
        if (textBytes.IsEmpty)
            return null;

        var text = Encoding.UTF8
            .GetString(textBytes)
            .Trim('\0', ' ', '\t', '\r', '\n');

        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (codecId.Equals(
                "S_TEXT/ASS",
                StringComparison.OrdinalIgnoreCase) ||
            codecId.Equals(
                "S_TEXT/SSA",
                StringComparison.OrdinalIgnoreCase))
        {
            var fields = text.Split(',', 9);
            if (fields.Length == 9)
                text = fields[8];

            text = StripAssOverrides(text);
        }

        return text
            .Replace("\\N", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\n", Environment.NewLine, StringComparison.Ordinal)
            .Replace("\\h", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string StripAssOverrides(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inside = false;

        foreach (var ch in value)
        {
            if (ch == '{')
            {
                inside = true;
                continue;
            }

            if (ch == '}' && inside)
            {
                inside = false;
                continue;
            }

            if (!inside)
                builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool IsSupportedTextCodec(string codecId) =>
        codecId.Equals("S_TEXT/UTF8", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/ASCII", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/ASS", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/SSA", StringComparison.OrdinalIgnoreCase) ||
        codecId.Equals("S_TEXT/WEBVTT", StringComparison.OrdinalIgnoreCase);

    private static async Task<Element?> ReadElementAtSegmentPositionAsync(
        SparseReader reader,
        Element segment,
        long position,
        CancellationToken cancellationToken)
    {
        if (position < 0)
            return null;

        var offset = checked(segment.DataOffset + position);
        return await ReadElementAsync(
            reader,
            offset,
            segment.End(reader.Length),
            cancellationToken);
    }

    private static async Task<Element?> ReadElementAsync(
        SparseReader reader,
        long offset,
        long parentEnd,
        CancellationToken cancellationToken)
    {
        if (offset < 0 || offset >= parentEnd)
            return null;

        var header = new byte[16];
        var read = await reader.ReadAsync(
            offset,
            header,
            cancellationToken);

        if (read < 2)
            return null;

        if (!TryReadVint(
                header.AsSpan(0, read),
                0,
                keepMarker: true,
                out var id,
                out var idBytes,
                out _))
        {
            return null;
        }

        if (!TryReadVint(
                header.AsSpan(0, read),
                idBytes,
                keepMarker: false,
                out var sizeValue,
                out var sizeBytes,
                out var unknownSize))
        {
            return null;
        }

        var dataOffset = checked(offset + idBytes + sizeBytes);

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

        return new Element(
            id,
            offset,
            dataOffset,
            size);
    }

    private static async Task<byte[]?> ReadPayloadAsync(
        SparseReader reader,
        Element element,
        CancellationToken cancellationToken,
        int maxBytes = MaxMetadataBytes)
    {
        if (element.Size is < 0 or > int.MaxValue ||
            element.Size > maxBytes)
        {
            return null;
        }

        var data = new byte[(int)element.Size];
        return await reader.ReadExactAsync(
            element.DataOffset,
            data,
            cancellationToken)
            ? data
            : null;
    }

    private static IEnumerable<MemoryElement> EnumerateChildren(
        byte[] data,
        int start,
        int end)
    {
        var offset = start;

        while (offset < end)
        {
            var element = ReadMemoryElement(
                data,
                offset,
                end);

            if (element is null)
                yield break;

            yield return element.Value;

            if (element.Value.End <= offset)
                yield break;

            offset = element.Value.End;
        }
    }

    private static MemoryElement? ReadMemoryElement(
        byte[] data,
        int offset,
        int end)
    {
        if (offset < 0 || offset >= end || end > data.Length)
            return null;

        if (!TryReadVint(
                data,
                offset,
                keepMarker: true,
                out var id,
                out var idBytes,
                out _))
        {
            return null;
        }

        if (!TryReadVint(
                data,
                offset + idBytes,
                keepMarker: false,
                out var size,
                out var sizeBytes,
                out var unknownSize) ||
            unknownSize ||
            size > int.MaxValue)
        {
            return null;
        }

        var dataOffset = offset + idBytes + sizeBytes;
        var elementEndLong = (long)dataOffset + (long)size;

        if (elementEndLong > end)
            return null;

        return new MemoryElement(
            id,
            dataOffset,
            (int)elementEndLong);
    }

    private static bool TryReadVint(
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

        while (length < 8 &&
               (first & mask) == 0)
        {
            mask >>= 1;
            length++;
        }

        length++;

        if (length > 8 ||
            offset + length > data.Length)
        {
            return false;
        }

        value = keepMarker
            ? first
            : (ulong)(first & (mask - 1));

        for (var index = 1; index < length; index++)
            value = (value << 8) | data[offset + index];

        if (!keepMarker)
        {
            var bits = 7 * length;
            var allOnes = bits == 56
                ? 0x00FFFFFFFFFFFFFFUL
                : (1UL << bits) - 1UL;

            unknownSize = value == allOnes;
        }

        return true;
    }

    private static ulong? ReadUnsigned(
        byte[] data,
        MemoryElement element)
    {
        var length = element.End - element.DataOffset;
        if (length is <= 0 or > 8)
            return null;

        ulong value = 0;
        for (var index = element.DataOffset;
             index < element.End;
             index++)
        {
            value = (value << 8) | data[index];
        }

        return value;
    }

    private static ulong? ReadBinaryId(
        byte[] data,
        MemoryElement element)
    {
        var length = element.End - element.DataOffset;
        if (length is <= 0 or > 8)
            return null;

        ulong value = 0;
        for (var index = element.DataOffset;
             index < element.End;
             index++)
        {
            value = (value << 8) | data[index];
        }

        return value;
    }

    private static string? ReadString(
        byte[] data,
        MemoryElement element)
    {
        if (element.End <= element.DataOffset)
            return null;

        var value = Encoding.UTF8
            .GetString(
                data,
                element.DataOffset,
                element.End - element.DataOffset)
            .Trim('\0', ' ', '\t', '\r', '\n');

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static string? NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value
            .Trim()
            .ToLowerInvariant();

        var separator = normalized.IndexOfAny(['-', '_']);
        if (separator > 0)
            normalized = normalized[..separator];

        return normalized;
    }

    private static bool LanguageMatches(
        string left,
        string? right)
    {
        if (right is null)
            return false;

        if (string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (left, right) switch
        {
            ("chi", "zho") or
            ("zho", "chi") or
            ("chi", "zh") or
            ("zh", "chi") or
            ("zho", "zh") or
            ("zh", "zho") => true,

            ("jpn", "ja") or
            ("ja", "jpn") => true,

            ("eng", "en") or
            ("en", "eng") => true,

            _ => false
        };
    }

    private static TimeSpan ScaleTime(
        long value,
        long timecodeScaleNanoseconds)
    {
        if (value <= 0 ||
            timecodeScaleNanoseconds <= 0)
        {
            return TimeSpan.Zero;
        }

        var ticks =
            (double)value *
            timecodeScaleNanoseconds /
            100d;

        return TimeSpan.FromTicks(
            (long)Math.Min(
                ticks,
                TimeSpan.MaxValue.Ticks));
    }

    private sealed class SparseReader(
        WebDavCachedRandomAccessSource source,
        long length)
    {
        internal long Length { get; } = length;

        internal async Task<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            return await source.ReadSparseAsync(
                offset,
                buffer,
                cancellationToken);
        }

        internal async Task<bool> ReadExactAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            var completed = 0;

            while (completed < buffer.Length)
            {
                var read = await ReadAsync(
                    offset + completed,
                    buffer[completed..],
                    cancellationToken);

                if (read <= 0)
                    return false;

                completed += read;
            }

            return true;
        }
    }

    private readonly record struct Element(
        ulong Id,
        long Offset,
        long DataOffset,
        long Size)
    {
        internal long End(long fallback) =>
            Size < 0
                ? fallback
                : DataOffset + Size;

        internal long NextOffset(long fallback) =>
            End(fallback);
    }

    private readonly record struct MemoryElement(
        ulong Id,
        int DataOffset,
        int End);

    private sealed record MatroskaTrack(
        ulong TrackNumber,
        string CodecId,
        string? Language);

    private sealed record CueEntry(
        long Time,
        long ClusterPosition,
        long? RelativePosition,
        long? Duration,
        int? BlockNumber);

    private readonly record struct SegmentLocations(
        long? InfoPosition,
        long? TracksPosition,
        long? CuesPosition);
}
