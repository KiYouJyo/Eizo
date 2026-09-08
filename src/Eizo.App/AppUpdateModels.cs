namespace Eizo;

internal enum AppUpdateState
{
    NotChecked,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Verifying,
    ReadyToInstall,
    Installing,
    Restarting,
    Completed,
    Cancelled,
    Failed
}

internal sealed record AppUpdateInfo(
    AppUpdateState State,
    string? AvailableVersion = null,
    string? Detail = null,
    string? ErrorCode = null,
    GitHubReleaseInfo? Release = null)
{
    public bool IsUpdateAvailable => State == AppUpdateState.UpdateAvailable;
    public bool IsReadyToInstall => State == AppUpdateState.ReadyToInstall;
}

internal sealed record AppUpdateProgress(AppUpdateState State, double? Value = null, string? Detail = null)
{
    public static double? NormalizeValue(double? value) =>
        value is null ? null : double.IsFinite(value.Value) ? Math.Clamp(value.Value, 0d, 1d) : null;
}

internal sealed record AppUpdateResult(AppUpdateState State, string? ErrorCode = null, string? Detail = null);
