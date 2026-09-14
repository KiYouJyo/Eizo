using System.Text;
using Eizo.Playback;

namespace Eizo.Models;

internal static class PlaybackFallbackDiagnostics
{
    private static readonly object Gate = new();

    internal static readonly string TracePath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "Logs",
        "playback-fallback.log");

    internal static void Write(
        string stage,
        PlaybackSource? source,
        SubtitleTrackInfo? track,
        string? detail = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(TracePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var fileName = source is null
                ? string.Empty
                : ResolveFileName(source.Uri);

            var line =
                DateTimeOffset.UtcNow.ToString("O") +
                "\tstage=" + Sanitize(stage) +
                "\tfile=" + Sanitize(fileName) +
                "\trandomAccess=" +
                    (source?.RandomAccessSource is not null) +
                "\ttrackId=" + (track?.Id.ToString() ?? "-") +
                "\tlanguage=" + Sanitize(track?.Language ?? "-") +
                "\tcodec=" + Sanitize(track?.Codec ?? "-") +
                "\tname=" + Sanitize(track?.Name ?? "-") +
                "\tdetail=" + Sanitize(detail ?? "-");

            lock (Gate)
            {
                File.AppendAllText(
                    TracePath,
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never affect playback.
        }
    }

    private static string ResolveFileName(Uri uri)
    {
        try
        {
            return Path.GetFileName(
                Uri.UnescapeDataString(uri.AbsolutePath));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Sanitize(string value) =>
        value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal);
}
