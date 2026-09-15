[简体中文](RELEASE-NOTES-v0.6.1.md) | [日本語](RELEASE-NOTES-v0.6.1.ja.md) | English

# Eizo v0.6.1

## Visual assets, startup experience, and detail polish

Eizo 0.6.1 refreshes the Windows visual identity and closes out startup and playback-detail experience work.

## Included

- Updated the multi-size `Eizo.ico` used by the EXE, shortcuts, and installer.
- Replaced AppList, Square44, Square150, Square71, Square310, Wide, StoreLogo, SplashScreen, and corresponding DPI assets.
- Standardized the app icon on the white-background / black-line artwork instead of maintaining a black-background light-theme variant.
- Added the in-app Mica startup surface with the native Windows blue `ProgressRing`.
- Deferred media-library, playback-history, and cache-maintenance work until after the startup surface has rendered its first frame.
- Kept the early runtime bootstrap in place so external Playback / Recognition runtime activation remains intact.
- Unwatched episodes on the playback detail page now show only the unwatched state and no longer render an empty 0% progress bar.
- Rebuilt the repository landing documentation and added a GitHub Pages product site, support page, privacy page, and release-process documentation.

## Release validation

- x64 Release build passed.
- Signed MSIXBundle packaging, installation, and launch passed.
- LibVLC payload validation passed (323 plugins).
- Bundled Metadata 0.2.23 fallback and real Recognition / Core / Providers calls passed.
- GitHub one-click installer and SHA-256 manifest validation passed.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
