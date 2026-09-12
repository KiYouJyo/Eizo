using Windows.ApplicationModel;

namespace Eizo;

internal static class AppVersionProvider
{
    public static string Version
    {
        get
        {
            var version = GetCurrentVersion();
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }
    }

    public static string DisplayVersion => $"v{Version}";

    public static Version GetCurrentVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return new Version(
                (int)version.Major,
                (int)version.Minor,
                (int)version.Build,
                (int)version.Revision);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            var assemblyVersion =
                typeof(AppVersionProvider).Assembly.GetName().Version ??
                new Version(0, 0, 0, 0);
            return new Version(
                assemblyVersion.Major,
                assemblyVersion.Minor,
                Math.Max(0, assemblyVersion.Build),
                Math.Max(0, assemblyVersion.Revision));
        }
    }

    public static string GetPackageVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            var version = GetCurrentVersion();
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
