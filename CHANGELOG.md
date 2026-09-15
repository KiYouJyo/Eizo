# Changelog

All notable user-facing changes are summarized here. Detailed multilingual notes remain under `docs/RELEASE-NOTES-v*.md`.

## 0.6.1 — 2026-09-15

- Refreshed the Windows app icon, AppList/MSIX assets, splash assets, and Microsoft Store artwork.
- Added the in-app Mica startup surface with the native blue loading ring.
- Deferred media-library, playback-history, and cache startup work until after the startup surface has rendered.
- Kept component runtime bootstrap on the early path so external Playback / Recognition activation remains intact.
- Improved the detail episode list: unwatched episodes now show only the unwatched state instead of an empty progress bar.
- Preserved signed x64 MSIXBundle and one-click GitHub distribution validation.

See: [简体中文](docs/RELEASE-NOTES-v0.6.1.md) · [日本語](docs/RELEASE-NOTES-v0.6.1.ja.md) · [English](docs/RELEASE-NOTES-v0.6.1.en.md)

## 0.6.0 — 2026-09-15

- Refined the player and title-detail experience.
- Improved hero/detail layout, metadata actions, episode artwork and playback state.
- Added configurable fullscreen control persistence and keyboard seek behavior.

See: [简体中文](docs/RELEASE-NOTES-v0.6.0.md) · [日本語](docs/RELEASE-NOTES-v0.6.0.ja.md) · [English](docs/RELEASE-NOTES-v0.6.0.en.md)

## 0.5.x

The 0.5 series established the real-content media model, TMDB production integration, provider routing, field-level metadata merge, identity binding, scan/scrape persistence, and the real-content library UI.

See the individual 0.5.x notes under [docs](docs/).

## 0.4.x

The 0.4 series expanded playback, subtitles, Bangumi integration, cache/download behavior, settings, and library aggregation.

## 0.3.x

The 0.3 series introduced WebDAV source management, remote playback, metadata/runtime activation, and library aggregation work.

## 0.2.x and 0.1.x

Early releases established recognition, localization, updater/runtime infrastructure, and the WinUI 3 application shell.
