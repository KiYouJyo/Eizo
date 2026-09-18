[简体中文](RELEASE-NOTES-v1.1.0.md) | [日本語](RELEASE-NOTES-v1.1.0.ja.md) | English

# Eizo v1.1.0

## Native picture-in-picture and playback refinements

Eizo 1.1 adds native picture-in-picture through Windows App SDK CompactOverlay while keeping the current playback session alive instead of rebuilding the playback engine.

## Included

- Added a PiP entry button on the right side of the player header using the native WinUI Button template and Segoe Fluent MiniExpand icon.
- Keeps the existing PlaybackView loaded when entering PiP, avoiding LibVLC engine recreation and media reopening.
- Makes PiP and fullscreen mutually exclusive, and hides app title/navigation/player sidebar chrome in compact mode.
- Keeps only high-frequency transport controls: back 10 seconds, play/pause, forward 10 seconds, and return to the normal window.
- Expands video across the full PiP client area and renders transport controls as a transparent bottom gradient overlay; the whole overlay auto-hides during playback and reappears on pointer movement.
- Supports Escape to leave PiP and mouse-wheel volume adjustment while the compact window is active.
- Scales external subtitle typography with the compact-window height and uses collision-free primary/secondary stacking; subtitles move above visible controls so bilingual lines do not overlap each other or the transport overlay.
- Restores the pre-PiP window position, size, maximized state, and sidebar state. Eizo also synchronizes its UI if Windows leaves CompactOverlay outside the PiP button path.
- Added Simplified Chinese, Japanese, and English PiP tooltips and accessibility names.
- Bumped product / MSIX versions to 1.1.0 / 1.1.0.0 and added a dedicated acceptance contract plus signed x64 acceptance-package pipeline.

## Acceptance focus

- Enter PiP during playback and confirm video/audio continue without reopening the media from the beginning.
- Confirm CompactOverlay stays above ordinary windows, remains resizable, and automatically hides/reveals controls as expected.
- Confirm primary and secondary external subtitles remain visible with compact sizing.
- Exit with Escape or the return-window button and confirm the original window geometry/maximized state, sidebar, and normal player layout are restored.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
