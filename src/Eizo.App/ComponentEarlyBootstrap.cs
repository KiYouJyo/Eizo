using System.Runtime.CompilerServices;

namespace Eizo;

internal static class ComponentEarlyBootstrap
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        StartupTrace.Mark("ModuleInitializer:begin");

        // WinUI generated metadata may resolve PlaybackView before App() executes.
        // Select staged external components (or their bundled fallback) before any
        // static reference to Playback/Metadata implementation assemblies binds.
        StartupTrace.Mark("ComponentRuntimeBootstrapper.Initialize:begin");
        ComponentRuntimeBootstrapper.Initialize();
        StartupTrace.Mark("ComponentRuntimeBootstrapper.Initialize:end");

        StartupTrace.Mark("ComponentActivationDiagnostics.WriteStartupSnapshot:begin");
        ComponentActivationDiagnostics.WriteStartupSnapshot();
        StartupTrace.Mark("ComponentActivationDiagnostics.WriteStartupSnapshot:end");
        StartupTrace.Mark("ModuleInitializer:end");
    }
}
