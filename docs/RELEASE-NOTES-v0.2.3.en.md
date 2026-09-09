English | [简体中文](RELEASE-NOTES-v0.2.3.md) | [日本語](RELEASE-NOTES-v0.2.3.ja.md)

# Eizo v0.2.3 — Medium-Width Light Navigation Background Fix

This release fixes the remaining v0.2.2 regression where the left CompactPane could turn black after opening and closing the NavigationView in a medium-width light-theme window.

The root cause was WinUI NavigationView's own visual-state machine:

- Opening CompactOverlay enters `PaneOverlaying`.
- Closing enters `PaneNotOverlaying`, where WinUI explicitly restores `RootSplitView.PaneBackground` from `NavigationViewExpandedPaneBackground`.
- Eizo previously overrode both `NavigationViewDefaultPaneBackground` and `NavigationViewExpandedPaneBackground` with `StaticResource` aliases inside `ShellNavigation.Resources`.
- Those aliases captured the Brush selected when the NavigationView was constructed. After a later switch to light theme, Eizo could repaint the pane temporarily, but the next close transition wrote the stale construction-time dark Brush back.

v0.2.3 fixes this by:

- removing both local NavigationView pane-background overrides;
- never repainting `PaneBackground` from Eizo while CompactOverlay is closed;
- letting WinUI restore its native transparent closed-pane background so the window Mica shows through naturally;
- keeping Eizo's custom expanded-pane surface only for pane opening, theme changes, and window activation changes;
- adding a CI regression guard that prevents the static pane aliases or PaneClosed repaint logic from being reintroduced.

All v0.2.2 fixes remain included, including the updater, compact player controls, Project & Open Source links, unified category icon, and native category child Flyout.

> The recommended validation path is to update from v0.2.2 to v0.2.3 through Eizo's in-app updater, then repeatedly open and close the hamburger pane in a medium-width light-theme window.
