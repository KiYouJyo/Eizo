English | [简体中文](RELEASE-NOTES-v0.3.0.md) | [日本語](RELEASE-NOTES-v0.3.0.ja.md)

# Eizo v0.3.0 — WebDAV Media Sources & Remote Playback

v0.3.0 moves Eizo from a local-player foundation to a usable remote-media playback path, with WebDAV as the first fully integrated remote source.

## WebDAV media sources

- Introduces a dedicated media-source provider architecture, with local and WebDAV providers registered separately so additional remote sources can be added cleanly later.
- Adds WebDAV source creation, persistence, and management. Credentials are stored in Windows PasswordVault and accessed through a separate credential contract rather than being embedded in the provider.
- Supports authenticated PROPFIND discovery, directory parsing, and remote media scanning, with runtime regression coverage for 401, 404, and HTTP Range behavior.
- Fixes resolution of absolute-path WebDAV href values so they are correctly resolved against the active HTTP(S) origin.
- Shows the real file extension for remote videos that have not yet been parsed into catalog metadata.

## Remote playback

- Updates Eizo.Playback to 0.2.1 and integrates the authenticated network-media open path.
- Videos discovered through WebDAV can now be opened directly in the Eizo player.
- Remote playback, seeking, and playback-speed changes have been validated end to end.
- The network playback path supports HTTP Range requests and covers authentication and missing-resource error paths.

## Player and media-source UI

- Replaces plain media-source rows with proper card-based surfaces.
- Reworks loading feedback to use a Mica surface with the native WinUI ProgressRing, plus loading progress and live download speed.
- Fixes the mismatch between playback-speed slider labels and the actual slider position.
- Removes duplicated playback information from the bottom of the sidebar.
- Removes duplicate audio/subtitle controls from the bottom transport bar and consolidates them under the sidebar's subtitle/audio section.
- Removes the player-local "Open local video" entry so the player focuses on the currently selected media context.
- Adds a player-UI cleanup contract test and CI guard to prevent these duplicated controls and loading-feedback regressions from returning.

## Version and distribution

- The user-facing release remains **v0.3.0**.
- The Windows package version is **0.3.0.1**; the final digit is the package revision after the WebDAV authentication/playback fix.
- The GitHub Release continues the three-asset contract: signed x64 MSIXBundle, one-click installer package, and SHA256SUMS.txt.
