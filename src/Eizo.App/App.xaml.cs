using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Eizo.Models;

namespace Eizo;

public partial class App : Application
{
    private static readonly object ActivationGate = new();
    private static bool _redirectedActivationPending;
    private static Uri? _pendingBangumiAuth;
    private Window? _window;
    private StartupWindow? _startupWindow;

    internal static MainWindow? MainWindow { get; private set; }

    public App()
    {
        try
        {
            Localization.AppLocalizationService.Default.ApplyPersistedLanguage(AppSettingsStore.Current);
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteStartupFailure("App.InitializeComponent", ex);
            throw;
        }
    }

    internal static void OnRedirectedActivation(AppActivationArguments activationArguments)
    {
        QueueProtocolActivation(activationArguments);
        MainWindow? existingWindow;
        lock (ActivationGate)
        {
            existingWindow = MainWindow;
            if (existingWindow is null)
            {
                _redirectedActivationPending = true;
                return;
            }
        }

        existingWindow.DispatcherQueue.TryEnqueue(() =>
        {
            existingWindow.RestoreAndActivate();
            _ = ProcessPendingBangumiAuthAsync();
        });
    }

    internal static void OnInitialActivation(AppActivationArguments activationArguments) =>
        QueueProtocolActivation(activationArguments);

    private static void QueueProtocolActivation(AppActivationArguments activationArguments)
    {
        if (activationArguments.Data is not IProtocolActivatedEventArgs protocol ||
            protocol.Uri.Scheme != "eizo" || protocol.Uri.Host != "bangumi-auth") return;
        lock (ActivationGate) _pendingBangumiAuth = protocol.Uri;
    }

    private static async Task ProcessPendingBangumiAuthAsync()
    {
        Uri? uri;
        lock (ActivationGate)
        {
            uri = _pendingBangumiAuth;
            _pendingBangumiAuth = null;
        }
        if (uri is not null) await BangumiOAuthService.Default.CompleteAsync(uri);
    }

    private static void ActivatePendingRedirectedWindow()
    {
        MainWindow? existingWindow;
        lock (ActivationGate)
        {
            if (!_redirectedActivationPending || MainWindow is null) return;
            _redirectedActivationPending = false;
            existingWindow = MainWindow;
        }

        existingWindow.DispatcherQueue.TryEnqueue(() =>
        {
            existingWindow.RestoreAndActivate();
            _ = ProcessPendingBangumiAuthAsync();
        });
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            // Paint a tiny dedicated startup surface first. MainWindow currently
            // constructs the full shell (NavigationView, tabs, pages, resources)
            // synchronously, which can leave the native HWND showing only its
            // default black/white background before the in-window overlay exists.
            _startupWindow = new StartupWindow();
            _startupWindow.Activate();

            // Do not start the expensive shell construction until at least one
            // actual compositor frame of the startup surface has been presented.
            await _startupWindow.WaitForFirstFrameAsync();

            _window = MainWindow = new MainWindow();

            _ = CacheRuntime.RunStartupMaintenanceAsync();

            ActivatePendingRedirectedWindow();
            _window.Activate();

            // Keep the startup window top-most until MainWindow has painted its
            // own splash/logo frame. This prevents a second black/white gap while
            // ownership is transferred between the lightweight and real windows.
            await Task.WhenAny(
                MainWindow.WaitForStartupSurfaceAsync(),
                Task.Delay(TimeSpan.FromSeconds(3)));

            var startupWindow = _startupWindow;
            _startupWindow = null;
            startupWindow?.Close();

            MainWindow.RestoreAndActivate();
            _ = ProcessPendingBangumiAuthAsync();
        }
        catch (Exception ex)
        {
            WriteStartupFailure("App.OnLaunched", ex);

            try
            {
                _startupWindow?.Close();
                _startupWindow = null;
            }
            catch
            {
            }

            throw;
        }
    }

    private static void WriteStartupFailure(string stage, Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eizo",
                "Logs");
            Directory.CreateDirectory(directory);

            var text =
                $"Stage: {stage}{Environment.NewLine}" +
                $"UTC: {DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
                exception;

            File.WriteAllText(Path.Combine(directory, "startup-failure.log"), text);

            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "Eizo-startup-failure.log"), text);
            }
            catch
            {
            }

            try
            {
                File.WriteAllText(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Eizo-startup-failure.log"), text);
            }
            catch
            {
            }
        }
        catch
        {
        }
    }
}
