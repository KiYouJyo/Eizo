[简体中文](RELEASE-NOTES-v0.2.0.md) | [日本語](RELEASE-NOTES-v0.2.0.ja.md) | English

# Eizo v0.2.0 Stability, Localization & In-App Update

- Fixes intermittent dark-mode rendering inconsistencies by making the window root the single theme owner, while retaining live System, Light and Dark switching.
- Reworks responsive layout under extreme window resizing, removes unstable automatic-grid paths from Home and category pages, and restores the SpatialViewer-style adaptive tab behavior. The build passes both tiny-window and rapid continuous-resize regression tests.
- Completes runtime switching between Simplified Chinese, Japanese and English, including Follow system. Cached pages and tab text are refreshed after a language switch.
- Adds a GitHub Releases in-app update flow covering version checks, download, SHA-256 verification, MSIX signature verification, installation and restart. The update button changes with state instead of exposing a permanent standalone restart button.
- Adopts the UrbanPlanToolbox single-instance startup model. Activating Eizo again while it is already running now foregrounds the existing window instead of opening a second process/window.
- Persists the last usable normal window size and maximized state, restoring them on the next launch while safely clamping to the current display work area.
- Continues the x64 GitHub release contract with a signed MSIXBundle, lightweight one-click package and SHA256SUMS.txt.
- v0.2.0 remains focused on shell stability and infrastructure; metadata scraping, media sources and the Eizo.Playback adapter will be integrated in later releases.
