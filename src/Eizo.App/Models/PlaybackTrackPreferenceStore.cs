using System.Text;
using System.Text.Json;

namespace Eizo.Models;

internal sealed record SubtitleTrackPreference(
    string Kind,
    string? Language,
    string? Name);

internal static class PlaybackTrackPreferenceStore
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "track-preferences.json");

    private static Dictionary<string, SubtitleTrackPreference> _subtitlePreferences =
        LoadCore();

    public static SubtitleTrackPreference? GetSubtitlePreference(
        CatalogMediaItemModel? item)
    {
        var key = BuildSubjectKey(item);
        if (key is null)
            return null;

        lock (Sync)
        {
            return _subtitlePreferences.TryGetValue(key, out var value)
                ? value
                : null;
        }
    }

    public static void SaveSubtitlePreference(
        CatalogMediaItemModel? item,
        SubtitleTrackPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        var key = BuildSubjectKey(item);
        if (key is null)
            return;

        lock (Sync)
        {
            _subtitlePreferences[key] = preference;
            SaveCore(_subtitlePreferences);
        }
    }

    public static string NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", "-", StringComparison.Ordinal);

        if (normalized.StartsWith("ja", StringComparison.Ordinal) ||
            normalized is "jpn" or "jp" ||
            normalized.Contains("japanese", StringComparison.Ordinal) ||
            normalized.Contains("日本", StringComparison.Ordinal))
        {
            return "ja";
        }

        if (normalized.StartsWith("zh", StringComparison.Ordinal) ||
            normalized is "zho" or "chi" or "chs" or "cht" or "cn" ||
            normalized.Contains("chinese", StringComparison.Ordinal) ||
            normalized.Contains("中文", StringComparison.Ordinal) ||
            normalized.Contains("简体", StringComparison.Ordinal) ||
            normalized.Contains("繁体", StringComparison.Ordinal))
        {
            return "zh";
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal) ||
            normalized is "eng" ||
            normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        return normalized.Split('-')[0];
    }

    private static string? BuildSubjectKey(CatalogMediaItemModel? item)
    {
        if (item is null)
            return null;

        if (item.Metadata is
            {
                IsResolved: true,
                Provider.Length: > 0,
                ProviderSubjectId.Length: > 0
            } metadata)
        {
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"metadata|{metadata.Provider!.Trim().ToLowerInvariant()}|{metadata.ProviderSubjectId}");
        }

        var title =
            item.Recognition?.Title ??
            item.ParsedTitle ??
            item.SourceTitle;
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var normalized = title
            .Normalize(NormalizationForm.FormKC)
            .ToUpperInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        if (builder.Length == 0)
            return null;

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"recognition|{builder}|{item.Recognition?.Year?.ToString() ?? "-"}");
    }

    private static Dictionary<string, SubtitleTrackPreference> LoadCore()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new(StringComparer.Ordinal);

            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<
                       Dictionary<string, SubtitleTrackPreference>>(
                       json) is { } values
                ? new Dictionary<string, SubtitleTrackPreference>(
                    values,
                    StringComparer.Ordinal)
                : new(StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return new(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    private static void SaveCore(
        IReadOnlyDictionary<string, SubtitleTrackPreference> values)
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(StorePath)!);
            var temporaryPath =
                $"{StorePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(values, SerializerOptions));
            File.Move(
                temporaryPath,
                StorePath,
                overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
