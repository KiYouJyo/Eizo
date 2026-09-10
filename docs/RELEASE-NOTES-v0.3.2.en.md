English | [简体中文](RELEASE-NOTES-v0.3.2.md) | [日本語](RELEASE-NOTES-v0.3.2.ja.md)

# Eizo v0.3.2 Playback Stability & Controls

Eizo v0.3.2 focuses on playback concurrency, lifecycle safety, and stability under rapid user interaction, while also improving fullscreen and keyboard controls. The WebDAV loading-card display issue is not pursued further in this release and is documented as a known issue for a later pass.

## Playback stability

- Reworked playback-operation serialization to reduce hangs and state races between media open, subtitle switching, seeking, and native playback callbacks.
- Added playback-session cancellation and lifecycle guards so stale operations do not touch a replacement engine after unload or disposal.
- Fixed race conditions around playback-surface initialization, release, and reattachment to reduce hangs during tab and page lifecycle transitions.
- Completed the missing `PlaybackOperationQueue` and diagnostic tracing infrastructure in Eizo.Playback, and fixed dependency restore on a fresh workspace.
- Added concurrency regression coverage and moved the test runner to the .NET 10 Microsoft Testing Platform experience.

## Subtitles and playback controls

- Reduced the WinUI crash risk caused by rebuilding ComboBox data during rapid subtitle-track switching.
- Subtitle and audio selection now preserve the latest user intent while superseded queued operations are cancelled.
- Added window-level Space key play / pause support.
- Added ESC to exit fullscreen and strengthened generation-safe fullscreen transitions.
- Coalesced high-frequency playback-position UI updates to reduce main-thread pressure.

## Build and release

- Fixed restoration of the pinned Eizo.Playback patch in fresh `--no-checkout` worktrees.
- Release x64 compilation, concurrency tests, MSIX packaging, and signing have been validated.
- The official release continues to provide a signed x64 MSIXBundle, one-click installer package, and SHA256SUMS.txt.

## Known issue

- **The loading-status card may still fail to appear when WebDAV video playback begins.** Further work on this UI issue is intentionally deferred in v0.3.2. Playback itself does not depend on the card being visible; the loading-state model will be revisited separately.
