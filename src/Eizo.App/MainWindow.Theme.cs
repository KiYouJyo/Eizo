using Microsoft.UI.Xaml;

namespace Eizo;

public sealed partial class MainWindow
{
    private void WindowRoot_Loaded(object sender, RoutedEventArgs e)
    {
        WindowRoot.Loaded -= WindowRoot_Loaded;

        // ActualTheme can still report the construction-time fallback while the
        // window is disconnected from an XamlRoot. Re-apply the persisted choice
        // once the root is live, then refresh every custom chrome surface on the
        // next dispatcher turn. Native ThemeResource-backed controls update through
        // inheritance; the title buttons/tabs are code-painted and need this pass.
        WindowRoot.RequestedTheme = ThemePreferenceStore.Load();
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateTitleBarColors();
            RefreshTabVisuals();
            QueueNavigationPaneBackgroundUpdate();
        });
    }
}
