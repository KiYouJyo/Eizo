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
        try
        {
            await EnforcePolicyAsync();
        }
        catch
        {
            // Cache maintenance is best-effort and must never block app startup.
        }
    }
}
