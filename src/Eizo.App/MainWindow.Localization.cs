using Eizo.Localization;
using Microsoft.UI.Xaml;

namespace Eizo;

public sealed partial class MainWindow
{
    private void ShellLocalization_LanguageChanged(object? sender, AppLanguageChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(ReloadLocalizedShell);

    private void MainWindow_Closed(object sender, WindowEventArgs e)
    {
        _localization.LanguageChanged -= ShellLocalization_LanguageChanged;
        Closed -= MainWindow_Closed;
    }

    private void ReloadLocalizedShell()
    {
        var selectedKey = _selectedTabKey;

        ApplyLocalizedShellText();

        foreach (var state in _tabs.Values.ToArray())
        {
            if (state.Kind == ShellTabKind.Workspace && state.PageKey is { } pageKey)
            {
                var descriptor = DescribeWorkspacePage(pageKey);
                state.Title = descriptor.Title;
                state.Glyph = descriptor.Glyph;
                ReplaceTabView(state, CreateWorkspaceView(pageKey));
                UpdateTabIdentity(state);
                continue;
            }

            // Real-content detail/player tabs keep their current bound model and
            // playback state across language changes. Rebuilding them from a title
            // alone would lose the catalog subject identity and reintroduce the
            // retired title-only fallback path.
        }

        if (selectedKey is not null && _tabs.ContainsKey(selectedKey))
            SelectTab(selectedKey);
    }
}
