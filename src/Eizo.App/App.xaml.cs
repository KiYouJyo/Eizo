using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Eizo;

public partial class App : Application
{
    private static readonly object ActivationGate = new();
    private static bool _redirectedActivationPending;
    private Window? _window;

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

        existingWindow.DispatcherQueue.TryEnqueue(existingWindow.RestoreAndActivate);
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

        existingWindow.DispatcherQueue.TryEnqueue(existingWindow.RestoreAndActivate);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = MainWindow = new MainWindow();
            ActivatePendingRedirectedWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteStartupFailure("App.OnLaunched", ex);
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
