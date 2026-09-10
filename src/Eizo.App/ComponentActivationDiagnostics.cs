namespace Eizo;

internal static class ComponentActivationDiagnostics
{
    private static readonly object Gate = new();

    public static void WriteStartupSnapshot()
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(ComponentRuntimeBootstrapper.ComponentsRoot);
                var path = Path.Combine(ComponentRuntimeBootstrapper.ComponentsRoot, "activation.log");
                var playback = ComponentRuntimeBootstrapper.GetStatus(EizoComponents.Playback);
                var metadata = ComponentRuntimeBootstrapper.GetStatus(EizoComponents.Recognition);
                var lines = new[]
                {
                    Format("Playback", playback),
                    Format("Metadata", metadata)
                };
                File.AppendAllLines(path, lines);
            }
        }
        catch (Exception) when (true)
        {
            // Diagnostics must never block application startup or bundled fallback.
        }
    }

    private static string Format(string displayName, ComponentRuntimeStatus status) =>
        $"{DateTimeOffset.UtcNow:O}\t{displayName}\tcurrent={ComponentRuntimeBootstrapper.FormatVersion(status.CurrentVersion)}\tbundled={ComponentRuntimeBootstrapper.FormatVersion(status.BundledVersion)}\texternal={status.IsUsingExternalComponent}\tpending={(status.PendingVersion is null ? "-" : ComponentRuntimeBootstrapper.FormatVersion(status.PendingVersion))}\terror={Sanitize(status.LastActivationError)}";

    private static string Sanitize(string? value) => string.IsNullOrWhiteSpace(value)
        ? "-"
        : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
