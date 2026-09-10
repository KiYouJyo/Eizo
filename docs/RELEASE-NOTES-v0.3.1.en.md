English | [简体中文](RELEASE-NOTES-v0.3.1.md) | [日本語](RELEASE-NOTES-v0.3.1.ja.md)

# Eizo v0.3.1 WebDAV Source Management & Bangumi Navigation

Eizo v0.3.1 further develops the local / WebDAV media library and establishes the navigation and page structure for future Bangumi tracking and seasonal-anime integration.

## Media sources and WebDAV

- Completed WebDAV source editing for display name, address, username, and password.
- Leaving the password field empty preserves the existing credential; sensitive values remain stored in Windows PasswordVault.
- Split “Edit media source” and “Edit scanned folders” into independent flows so scan-scope changes do not modify connection settings.
- Rebuilt the WebDAV folder picker as a native WinUI window with Mica, BreadcrumbBar navigation, folder glyphs, multi-selection, and double-click navigation.
- A single WebDAV source can persist multiple scan roots and build the catalog from the selected scope.
- Added per-source “Scan now” actions and moved scanning to asynchronous execution. Adding or editing a source no longer starts a full scan automatically.
- Scanning uses a native ProgressRing; the idle button stays compact and expands only while scanning.
- Removed the redundant “Test connection” action and the opened-local-video pseudo-source card.

## UI and theme fixes

- Fixed light-theme inconsistencies in WebDAV editors, folder pickers, and text colors.
- Unified title-bar and content surfaces in the WebDAV folder window and increased its usable height.
- Rebuilt the media-source card template so corner radius applies to the actual visible surface while preserving native pointer-over and pressed states.
- Fixed Scan now text alignment and the idle/scanning size transition.

## Navigation

- Reorganized the hamburger menu as Home → Bangumi → Media library, with Cache, About, and Settings fixed at the bottom.
- The Bangumi parent item now opens the My Following workspace directly, with Broadcast calendar, Seasonal anime, and Rankings & discovery as its three child entries.
- The Media library parent continues to open the aggregate catalog, with Anime, Movies, TV series, and Media sources as child entries.
- Renamed the aggregate catalog page heading from “Categories” to “Media library”.
- Added Bangumi workspace routes and native WinUI placeholder pages for future official-account sign-in, tracking synchronization, broadcast calendar, and seasonal metadata integration.
- v0.3.1 only ships the navigation and page scaffolding; Bangumi account sign-in and online data are not enabled yet.

## Stability and compatibility

- Existing player, workspace tabs, single-instance behavior, window-size persistence, WebDAV playback, and catalog behavior remain intact.
- Added a navigation information-architecture regression contract to prevent accidental removal of existing media routes or reintroduction of a duplicate My Following child item.
- Maintains consistent Simplified Chinese, Japanese, and English resources.
- Official assets continue to include the signed x64 MSIXBundle, one-click installer package, and SHA256SUMS.txt.
