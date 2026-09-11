using Eizo.MetadataIntegration;
using Eizo.Recognition;

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

            WriteRecognitionRuntimeProbe();
            WriteMetadataRuntimeProbe();
        }
        catch (Exception) when (true)
        {
            // Diagnostics must never block application startup or bundled fallback.
        }
    }

    private static void WriteRecognitionRuntimeProbe()
    {
        var path = Path.Combine(ComponentRuntimeBootstrapper.ComponentsRoot, "recognition-runtime.log");
        try
        {
            var runtime = MediaRecognitionService.ProbeRuntime();
            var status = ComponentRuntimeBootstrapper.GetStatus(EizoComponents.Recognition);
            var line =
                $"{DateTimeOffset.UtcNow:O}\tversion={runtime.Version}\texternal={status.IsUsingExternalComponent}\tprobe={runtime.ProbeStatus}\tassembly={Sanitize(runtime.AssemblyPath)}{Environment.NewLine}";

            lock (Gate)
                File.AppendAllText(path, line);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ComponentRuntimeBootstrapper.MarkActivationFailed(EizoComponents.Recognition, exception.Message);
            var line =
                $"{DateTimeOffset.UtcNow:O}\tversion=-\texternal=failed\tprobe=failed\tassembly=-\terror={Sanitize(exception.ToString())}{Environment.NewLine}";

            lock (Gate)
                File.AppendAllText(path, line);
        }
    }

    private static void WriteMetadataRuntimeProbe()
    {
        var path = Path.Combine(
            ComponentRuntimeBootstrapper.ComponentsRoot,
            "metadata-runtime.log");

        try
        {
            var runtime = MediaMetadataService.ProbeRuntime();
            var status = ComponentRuntimeBootstrapper.GetStatus(
                EizoComponents.Recognition);

            var line =
                $"{DateTimeOffset.UtcNow:O}\tversion={runtime.Version}\texternal={status.IsUsingExternalComponent}\tprobe={runtime.ProbeStatus}\tcore={Sanitize(runtime.CoreAssemblyPath)}\tproviders={Sanitize(runtime.ProvidersAssemblyPath)}{Environment.NewLine}";

            lock (Gate)
                File.AppendAllText(path, line);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            ComponentRuntimeBootstrapper.MarkActivationFailed(
                EizoComponents.Recognition,
                exception.Message);

            var line =
                $"{DateTimeOffset.UtcNow:O}\tversion=-\texternal=failed\tprobe=failed\tcore=-\tproviders=-\terror={Sanitize(exception.ToString())}{Environment.NewLine}";

            lock (Gate)
                File.AppendAllText(path, line);
        }
    }

    private static string Format(string displayName, ComponentRuntimeStatus status) =>
        $"{DateTimeOffset.UtcNow:O}\t{displayName}\tcurrent={ComponentRuntimeBootstrapper.FormatVersion(status.CurrentVersion)}\tbundled={ComponentRuntimeBootstrapper.FormatVersion(status.BundledVersion)}\texternal={status.IsUsingExternalComponent}\tpending={(status.PendingVersion is null ? "-" : ComponentRuntimeBootstrapper.FormatVersion(status.PendingVersion))}\terror={Sanitize(status.LastActivationError)}";

    private static string Sanitize(string? value) => string.IsNullOrWhiteSpace(value)
        ? "-"
        : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
