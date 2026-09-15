using System.Text.Json;

namespace Eizo;

internal enum AppLanguagePreference
{
    SimplifiedChinese = 0,
    Japanese = 1,
    English = 2,
    System = 3
}

internal sealed record AppSettings(
    AppLanguagePreference Language = AppLanguagePreference.System,
    int? LastNormalWindowWidth = null,
    int? LastNormalWindowHeight = null,
    bool WasWindowMaximized = false,
    double PrimarySubtitleVerticalPosition = 12d,
    double SecondarySubtitleVerticalPosition = 24d,
    double PrimarySubtitleBackgroundOpacity = 70d,
    double SecondarySubtitleBackgroundOpacity = 70d,
    bool CacheAutoCleanup = true,
    long CacheLimitBytes = 32L * 1024 * 1024 * 1024,
    long RemotePrecacheBytes = 256L * 1024 * 1024,
    bool PreserveOfflineCache = true,
    bool MetadataAutoScrapeOnScan = true,
    bool MetadataArtworkEnrichment = true,
    bool AutoPlayNextEpisode = true,
    int FullscreenControlsTimeoutSeconds = 4,
    bool RememberPlaybackRate = true,
    double DefaultPlaybackRate = 1d,
    double LastPlaybackRate = 1d,
    bool RememberSubtitleTrack = true,
    string PreferredAudioLanguage = "auto",
    string PreferredSubtitleLanguage = "auto",
    string PreferredSecondarySubtitleLanguage = "auto");

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Eizo",
        "settings.json");

    private static AppSettings _current = LoadCore();

    public static event EventHandler? Changed;
    public static AppSettings Current => _current;

    public static void Update(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var next = update(_current);
        if (next == _current) return;

        _current = next;
        SaveCore(next);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var loaded =
                JsonSerializer.Deserialize<AppSettings>(json) ??
                new AppSettings();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return loaded with
            {
                CacheAutoCleanup =
                    root.TryGetProperty(
                        nameof(AppSettings.CacheAutoCleanup),
                        out _)
                        ? loaded.CacheAutoCleanup
                        : true,
                CacheLimitBytes =
                    root.TryGetProperty(
                        nameof(AppSettings.CacheLimitBytes),
                        out _) &&
                    loaded.CacheLimitBytes > 0
                        ? loaded.CacheLimitBytes
                        : 32L * 1024 * 1024 * 1024,
                RemotePrecacheBytes =
                    root.TryGetProperty(
                        nameof(AppSettings.RemotePrecacheBytes),
                        out _) &&
                    loaded.RemotePrecacheBytes > 0
                        ? loaded.RemotePrecacheBytes
                        : 256L * 1024 * 1024,
                PreserveOfflineCache =
                    root.TryGetProperty(
                        nameof(AppSettings.PreserveOfflineCache),
                        out _)
                        ? loaded.PreserveOfflineCache
                        : true,
                MetadataAutoScrapeOnScan =
                    root.TryGetProperty(
                        nameof(AppSettings.MetadataAutoScrapeOnScan),
                        out _)
                        ? loaded.MetadataAutoScrapeOnScan
                        : true,
                MetadataArtworkEnrichment =
                    root.TryGetProperty(
                        nameof(AppSettings.MetadataArtworkEnrichment),
                        out _)
                        ? loaded.MetadataArtworkEnrichment
                        : true,
                AutoPlayNextEpisode =
                    root.TryGetProperty(
                        nameof(AppSettings.AutoPlayNextEpisode),
                        out _)
                        ? loaded.AutoPlayNextEpisode
                        : true,
                FullscreenControlsTimeoutSeconds =
                    NormalizeFullscreenControlsTimeout(
                        loaded.FullscreenControlsTimeoutSeconds),
                RememberPlaybackRate =
                    root.TryGetProperty(
                        nameof(AppSettings.RememberPlaybackRate),
                        out _)
                        ? loaded.RememberPlaybackRate
                        : true,
                DefaultPlaybackRate =
                    root.TryGetProperty(
                        nameof(AppSettings.DefaultPlaybackRate),
                        out _) &&
                    loaded.DefaultPlaybackRate is >= 0.5d and <= 2d
                        ? loaded.DefaultPlaybackRate
                        : 1d,
                LastPlaybackRate =
                    root.TryGetProperty(
                        nameof(AppSettings.LastPlaybackRate),
                        out _) &&
                    loaded.LastPlaybackRate is >= 0.5d and <= 2d
                        ? loaded.LastPlaybackRate
                        : 1d,
                RememberSubtitleTrack =
                    root.TryGetProperty(
                        nameof(AppSettings.RememberSubtitleTrack),
                        out _)
                        ? loaded.RememberSubtitleTrack
                        : true,
                PreferredAudioLanguage =
                    NormalizeLanguagePreference(
                        loaded.PreferredAudioLanguage),
                PreferredSubtitleLanguage =
                    NormalizeLanguagePreference(
                        loaded.PreferredSubtitleLanguage),
                PreferredSecondarySubtitleLanguage =
                    NormalizeLanguagePreference(
                        loaded.PreferredSecondarySubtitleLanguage)
            };
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    internal static int NormalizeFullscreenControlsTimeout(
        int seconds) =>
        seconds switch
        {
            2 => 2,
            4 => 4,
            6 => 6,
            10 => 10,
            _ => 4
        };

    private static string NormalizeLanguagePreference(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ja" => "ja",
            "zh" => "zh",
            "en" => "en",
            _ => "auto"
        };

    private static void SaveCore(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = $"{SettingsPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (IOException)
        {
            // A settings write must never break an interactive language switch.
        }
    }
}
