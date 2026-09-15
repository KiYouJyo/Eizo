using Eizo.Cache;

namespace Eizo;

internal static class CacheRuntime
{
    private static readonly DiskCacheStore StoreInstance = new();

    internal static DiskCacheStore Store => StoreInstance;

    internal static CachePolicy CurrentPolicy
    {
        get
        {
            var settings = AppSettingsStore.Current;
            return new CachePolicy(
                settings.CacheLimitBytes > 0
                    ? settings.CacheLimitBytes
                    : CacheDefaults.LimitBytes,
                settings.CacheAutoCleanup,
                settings.PreserveOfflineCache,
                settings.RemotePrecacheBytes > 0
                    ? settings.RemotePrecacheBytes
                    : CacheDefaults.RemotePrecacheBytes);
        }
    }

    internal static Task<CacheCleanupResult> EnforcePolicyAsync(
        CancellationToken cancellationToken = default) =>
        StoreInstance.EnforceLimitAsync(
            CurrentPolicy,
            cancellationToken);

    internal static async Task RunStartupMaintenanceAsync()
    {
        Eizo.StartupTrace.Mark("CacheRuntime.StartupMaintenance:begin");
        try
        {
            await EnforcePolicyAsync();
            Eizo.StartupTrace.Mark("CacheRuntime.StartupMaintenance:end");
        }
        catch
        {
            Eizo.StartupTrace.Mark("CacheRuntime.StartupMaintenance:error");
            // Cache maintenance is best-effort and must never block app startup.
        }
    }
}
