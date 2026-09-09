English | [简体中文](RELEASE-NOTES-v0.2.1.md) | [日本語](RELEASE-NOTES-v0.2.1.ja.md)

# Eizo v0.2.1 — Player experience, Eizo.Playback integration & stability fixes

- Integrates the Eizo.Playback layer while keeping Eizo UI isolated from LibVLCSharp types. Playback packages are restored from a pinned version and commit for reproducible builds.
- Substantially rebuilds the player page with real media playback, play/pause, seeking, chapters, subtitle/audio entry points, playback rate, volume, and fullscreen controls.
- Adds a volume button and slider; fullscreen mouse-wheel input adjusts volume in 5% steps.
- Fixes fullscreen control auto-hide and restores controls on pointer movement or interaction.
- Refines the right-side queue/subtitle/audio pane: fullscreen uses Overlay with outside-click dismissal, while windowed mode keeps the normal Inline pane behavior.
- Forces the fullscreen player UI to use the dark theme even when the app is in light mode, then restores the original light appearance after leaving fullscreen.
- Fixes light-mode sidebar and center-status visual regressions in windowed playback.
- Reworks fullscreen exit and video-tab teardown so Playback Surface, LibVLC, and D3D resources are released before the visual tree is removed, reducing hangs when exiting fullscreen or closing player tabs.
- Improves playback-state persistence and catalog content so media context and playback position survive tab switching more reliably.
- Removes the placeholder Eizo.Playback independent-update card from About. Eizo.Playback remains a separate development repository and is version-pinned by Eizo instead of pretending to be independently deployable.
- Keeps the x64 GitHub release contract: signed MSIXBundle, one-click package, and SHA256SUMS.txt.
