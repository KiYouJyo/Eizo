English | [简体中文](RELEASE-NOTES-v0.2.2.md) | [日本語](RELEASE-NOTES-v0.2.2.ja.md)

# Eizo v0.2.2 — Updater Fixes & Medium-Width Navigation Improvements

This release fixes updater and responsive-UI regressions found after v0.2.1 and is intended to serve as a real application-update regression test.

- Fixes a post-download update failure where WinVerifyTrust could still hold the MSIXBundle file handle while Eizo reopened the bundle to inspect its embedded signature, causing the failure to be misreported as `BundleDownloadFailed`.
- Changes signature verification order so WinTrust state is closed and file handles are released before reading `AppxSignature.p7x`. The formal Release CI now verifies the final signed bundle through Eizo's own `MsixBundleSignatureVerifier`.
- Separates download, signature-read, and pending-update storage failures instead of mapping unrelated post-download I/O errors to a download failure.
- Reworks compact player controls into a responsive Grid; transport controls move to a second row when needed to prevent overlap with subtitle, audio, rate, and volume controls.
- Uses `ShellNavigation.ActualTheme` for medium/small NavigationView pane colors and refreshes the pane after theme changes and collapse, fixing the light-mode pane turning black after expand/collapse.
- Restores the native WinUI child Flyout for the medium-width Category menu instead of forcing a custom Mica surface.
- Unifies the Category workspace-tab icon with the left navigation by using the same `Symbol.Library` source.
- Connects the Project & Open Source cards to the real GitHub repository, Releases page, and privacy statement.
- Includes the fullscreen, sidebar, volume, lifecycle, and light-theme player fixes from v0.2.1.

> Updating from v0.2.1 to v0.2.2 through Eizo's in-app updater is the recommended way to validate this updater fix.
