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
        MainContent.Content = null;

        ApplyLocalizedShellText();

        foreach (var state in _tabs.Values.ToArray())
        {
            if (state.Kind == ShellTabKind.Workspace && state.PageKey is { } pageKey)
            {
                var descriptor = DescribeWorkspacePage(pageKey);
                state.Title = descriptor.Title;
                state.Glyph = descriptor.Glyph;
                state.View = CreateWorkspaceView(pageKey);
                UpdateTabIdentity(state);
                continue;
            }

            if (state.Kind == ShellTabKind.Detail && state.MediaTitle is { } mediaTitle)
            {
                if (state.Episode is { } episode)
                    ShowPlayerInDetailTab(state, mediaTitle, episode);
                else
                    ShowDetailInTab(state, mediaTitle);
            }
        }

        if (selectedKey is not null && _tabs.ContainsKey(selectedKey))
            SelectTab(selectedKey);
    }
}
