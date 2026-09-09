using Eizo.Playback.WinUI;
using System.Reflection;

namespace Eizo;

internal static class PlaybackVersionProvider
{
    public static Version GetCurrentVersion()
    {
        var assembly = typeof(PlaybackView).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var normalized = informational.Trim();
            var separator = normalized.IndexOfAny(['-', '+']);
            if (separator >= 0) normalized = normalized[..separator];

            if (Version.TryParse(normalized, out var parsed))
                return Normalize(parsed);
        }

        return Normalize(assembly.GetName().Version ?? new Version(0, 1, 0, 0));
    }

    public static string DisplayVersion
    {
        get
        {
            var version = GetCurrentVersion();
            return $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }

    private static Version Normalize(Version version) =>
        new(
            version.Major,
            version.Minor,
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
}
