using Eizo.Views;

namespace Eizo;

public sealed partial class MainWindow
{
    private ResponsiveLayoutMode _responsiveMode = ResponsiveLayoutMode.Large;
    private bool _responsiveLayoutApplied;

    private void ApplyResponsiveLayout(bool force = false)
    {
        if (!_shellReady) return;

        var logicalWidth = RootGrid.ActualWidth;
        if (logicalWidth <= 0) logicalWidth = Content.XamlRoot?.Size.Width ?? 0;
        if (logicalWidth <= 0) return;

        var mode = logicalWidth >= 1280
            ? ResponsiveLayoutMode.Large
            : logicalWidth >= 760
                ? ResponsiveLayoutMode.Medium
                : ResponsiveLayoutMode.Small;

        if (!force && _responsiveLayoutApplied && mode == _responsiveMode) return;

        _responsiveMode = mode;
        _responsiveLayoutApplied = true;

        if (_selectedTabKey is null ||
            !_tabs.TryGetValue(_selectedTabKey, out var selected))
        {
            return;
        }

        if (selected.View is HomeView home)
            home.SetResponsiveMode(mode);
        else if (selected.View is CategoryView category)
            category.SetResponsiveMode(mode);
    }
}
