using System.Text;

namespace Eizo.Models;

internal static class PlaybackSelectionTrace
{
    private static readonly string TracePath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "Logs",
        "playback-selection.log");

    internal static void Write(
        string stage,
        string? sourceTitle,
        string? locator,
        int queueIndex,
        int queueCount)
    {
        try
        {
            var directory = Path.GetDirectoryName(TracePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var fileName = ResolveFileName(locator);

            var line =
                DateTimeOffset.UtcNow.ToString("O") +
                "\tstage=" + Sanitize(stage) +
                "\ttitle=" + Sanitize(sourceTitle ?? string.Empty) +
                "\tfile=" + Sanitize(fileName) +
                "\tqueueIndex=" + queueIndex +
                "\tqueueCount=" + queueCount;

            File.AppendAllText(
                TracePath,
                line + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Playback diagnostics must never affect playback.
        }
    }

    private static string ResolveFileName(string? locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
            return string.Empty;

        if (Uri.TryCreate(locator, UriKind.Absolute, out var uri))
        {
            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            return Path.GetFileName(path);
        }

        return Path.GetFileName(locator);
    }

    private static string Sanitize(string value) =>
        value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal);
}
