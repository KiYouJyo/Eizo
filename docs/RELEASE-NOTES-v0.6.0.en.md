[简体中文](RELEASE-NOTES-v0.6.0.md) | [日本語](RELEASE-NOTES-v0.6.0.ja.md) | English

# Eizo v0.6.0

## Player and Detail Experience

Eizo 0.6.0 focuses on player interaction, the title-detail first view, and update-management polish, while promoting decoded-frame timeline previews into the Playback runtime.

## Included

- Redesign the title-detail Hero to match the Home hero: information and actions on the left, a landscape Backdrop on the right.
- Keep the Hero height compact so episodes, cast and staff enter the first viewport sooner.
- Expose Re-scrape and Manual match directly instead of hiding them in an overflow menu.
- Add fullscreen control auto-hide tiers of 2 / 4 / 6 / 10 seconds.
- In fullscreen, tap Left/Right to seek -10/+10 seconds; hold Left for temporary 0.5× or Right for temporary 2×, restoring the previous rate on release.
- Fix playback controls becoming unresponsive after opening and dismissing the fullscreen sidebar.
- Replace the numeric timeline tooltip with decoded-frame preview thumbnails and reliably observe Slider/Thumb drag events even when WinUI marks them handled.
- Upgrade to Eizo.Playback 0.2.3 with a backend-neutral frame capture capability.
- Preserve shared column and button alignment across the Eizo and Metadata update cards.
- Add a Release Notes button to the Metadata card. After an update check it opens the matching Eizo.Metadata release; otherwise it opens the Metadata releases page.

## Compatibility

- Windows 10 2004 / Windows 11, x64.
- Continues to use independently updateable Eizo.Metadata and Eizo.Playback runtimes.
