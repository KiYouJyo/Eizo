# Eizo 0.5.12 — Real-content UI Close-out

Eizo 0.5.12 keeps the established UI framework and closes the remaining presentation gaps around the real media data wired in during 0.5.9–0.5.11. It does not add AniList or redesign the library/detail layout.

## Included

- Remove recognition-confidence and technical source/file lines from normal library cards.
- Keep per-title Re-scrape / Manual match / Clear manual match actions while removing recognition/metadata debug entries from the normal card menu.
- Add in-progress, success, warning, and error feedback for per-title metadata actions in the library.
- Hide missing native title, year, metadata summary, and overview fields on the detail page instead of showing duplicate values or dash placeholders.
- Add detail-page feedback for re-scrape and manual-match operations.
- Show an explicit empty state when the selected season has no real playable media.
- Remove sample seasons, episodes, and Sample source labels from the legacy title-only detail fallback.
- Prevent standalone/unaggregated home items from opening the legacy sample detail path while keeping direct playback of the real media item.

## Scope

- No redesign of the established library/detail framework.
- AniList remains intentionally skipped.
- Recognition-report export remains available for diagnostics, but diagnostic fields no longer leak into normal content cards.
