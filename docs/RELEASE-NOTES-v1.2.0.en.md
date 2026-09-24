[简体中文](RELEASE-NOTES-v1.2.0.md) | [日本語](RELEASE-NOTES-v1.2.0.ja.md) | English

# Eizo v1.2.0

## Unified card design and cache management

Eizo 1.2 consolidates the card UI architecture. Media Library and Bangumi My Following no longer maintain parallel card markup; they now share one component, sizing contract, and interaction surface. Video-cache cards also gain complete context-menu actions.

## Included

- Added shared card resources for poster-card sizing, corner radius, border, hover, pressed, keyboard focus, and typography.
- Added reusable `MediaPosterCard` and migrated both Media Library and Bangumi My Following to it.
- Fixed the My Following hover highlight so its interaction surface exactly matches the card bounds and rounded corners.
- Keeps library source diagnostics hidden while allowing My Following to use the same component's optional footer for collection progress.
- Moved video-cache rows to the shared horizontal card style and attached the context menu to the whole item container.
- Added “Open file location” and “Delete cache” to video-cache right-click menus.
- “Open file location” resolves the folder containing the cached media blocks, with the Eizo cache root as a fallback for active tasks that do not yet have a completed group.
- “Delete cache” remains an immediate action with no confirmation dialog and uses the destructive-action visual style.
- Added a dedicated v1.2 card UI regression contract and wired it into repository validation.
- Bumped product / MSIX versions to 1.2.0 / 1.2.0.0.

## Acceptance focus

- Media Library and My Following cards of the same size should share identical bounds, rounding, and interaction feedback.
- My Following hover feedback must neither extend beyond nor fall short of the actual card.
- Right-clicking anywhere on a cache card should expose Open file location and Delete cache.
- Delete should execute directly without a confirmation dialog.
- Card boundaries should remain consistent in light/dark themes and keyboard focus states.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
