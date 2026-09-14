using System.Globalization;

namespace Eizo.Models;

internal static class WebDavMediaCacheKeys
{
    public const int BlockSize = 4 * 1024 * 1024;

    public static string BuildGroupKey(
        MediaSourceDefinition source,
        Uri mediaUri,
        WebDavMediaProbeResult probe)
    {
        var version =
            !string.IsNullOrWhiteSpace(probe.EntityTag)
                ? "etag:" + probe.EntityTag
                : probe.LastModified is { } modified
                    ? "modified:" + modified.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture)
                    : "length:" + probe.ContentLength?.ToString(CultureInfo.InvariantCulture);

        return string.Join(
            ":",
            "webdav-media",
            source.Id,
            mediaUri.AbsoluteUri,
            version);
    }

    public static string BuildBlockKey(
        string groupKey,
        long blockIndex) =>
        groupKey + ":block:" +
        blockIndex.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseGroupKey(
        string groupKey,
        out string sourceId,
        out Uri? mediaUri)
    {
        sourceId = string.Empty;
        mediaUri = null;

        const string prefix = "webdav-media:";

        if (string.IsNullOrWhiteSpace(groupKey) ||
            !groupKey.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        var sourceSeparator =
            groupKey.IndexOf(
                ':',
                prefix.Length);

        if (sourceSeparator <= prefix.Length)
            return false;

        var versionSeparator =
            FindVersionSeparator(
                groupKey);

        if (versionSeparator <= sourceSeparator + 1)
            return false;

        sourceId =
            groupKey[
                prefix.Length..
                sourceSeparator];

        var uriText =
            groupKey[
                (sourceSeparator + 1)..
                versionSeparator];

        return Uri.TryCreate(
            uriText,
            UriKind.Absolute,
            out mediaUri);
    }

    private static int FindVersionSeparator(
        string groupKey)
    {
        var result = -1;

        foreach (var marker in new[]
                 {
                     ":etag:",
                     ":modified:",
                     ":length:"
                 })
        {
            var index =
                groupKey.LastIndexOf(
                    marker,
                    StringComparison.Ordinal);

            if (index > result)
                result = index;
        }

        return result;
    }

    public static int ExpectedBlockLength(
        long contentLength,
        long blockIndex)
    {
        var start = checked(blockIndex * (long)BlockSize);
        if (start >= contentLength)
            return 0;

        return checked((int)Math.Min(
            BlockSize,
            contentLength - start));
    }

    public static long BlockCount(long contentLength) =>
        contentLength <= 0
            ? 0
            : (contentLength + BlockSize - 1L) / BlockSize;
}
