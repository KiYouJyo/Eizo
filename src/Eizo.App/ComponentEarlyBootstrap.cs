using System.Runtime.CompilerServices;

namespace Eizo;

internal static class ComponentEarlyBootstrap
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        // WinUI generated metadata may resolve PlaybackView before App() executes.
        // Select staged external components (or their bundled fallback) before any
        // static reference to Playback/Recognition implementation assemblies binds.
        ComponentRuntimeBootstrapper.Initialize();
    }
}
