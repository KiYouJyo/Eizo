using Microsoft.UI.Xaml;

namespace Eizo;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteStartupFailure("App.InitializeComponent", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
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

            var path = Path.Combine(directory, "startup-failure.log");
            File.WriteAllText(
                path,
                $"Stage: {stage}{Environment.NewLine}" +
                $"UTC: {DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
                exception);
        }
        catch
        {
        }
    }
}
