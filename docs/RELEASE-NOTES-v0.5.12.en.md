[简体中文](RELEASE-NOTES-v0.5.12.md) | [日本語](RELEASE-NOTES-v0.5.12.ja.md) | English

# Eizo v0.5.12

## Real Content UI / Close-out

## Included

- Remove recognition-confidence and technical source/file lines from normal library cards.
- Keep per-title Re-scrape / Manual match / Clear manual match actions while removing recognition/metadata debug entries from the normal card menu.
- Add in-progress, success, warning, and error feedback for per-title metadata actions in the library.
- Hide missing native title, year, metadata summary, and overview fields on the detail page instead of showing duplicate values or dash placeholders.
- Add detail-page feedback for re-scrape and manual-match operations.
- Show an explicit empty state when the selected season has no real playable media.
- Remove sample seasons, episodes, and Sample source labels from the legacy title-only detail fallback.
- Prevent standalone/unaggregated home items from opening the legacy sample detail path while keeping direct playback of the real media item.
- Hide missing Hero subtitle/metadata/overview fields and suppress meaningless actions for an empty library.
- Remove the obsolete CategoryView that still contained Sample Movie and other demo-only data.

## Scope

- No redesign of the established library/detail framework.
- AniList remains intentionally skipped.
- Recognition-report export remains available for diagnostics, but diagnostic fields no longer leak into normal content cards.
