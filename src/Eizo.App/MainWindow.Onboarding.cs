namespace Eizo;

public sealed partial class MainWindow
{
    internal void ShowFirstRunGuideFromSettings() =>
        FirstRunGuideHost.ShowFromStart();
}
