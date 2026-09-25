[简体中文](RELEASE-NOTES-v1.3.0.md) | [日本語](RELEASE-NOTES-v1.3.0.ja.md) | English

# Eizo v1.3.0

## Native anime search and index

Eizo 1.3 removes the standalone Bangumi Anime Blogs workspace and replaces that navigation entry with a native WinUI 3 Anime Search experience. It consumes Bangumi's anime index directly without embedding a web page and keeps the unified Eizo card system introduced in 1.2.

## Included

- Removed the Anime Blogs page, navigation route, workspace wiring, and dedicated UI.
- Added Anime Search with a native WinUI 3 `AutoSuggestBox`.
- Added native `RadioButtons` filters for format, source, genre, region, audience, and year.
- Added Rank, Popularity, Score, and Relevance sorting.
- Extended Bangumi subject search with `meta_tags`, `tag`, and `air_date` filters while constraining results to anime and non-NSFW entries.
- Reused `MediaPosterCard` and the shared poster sizing, pointer, pressed, and keyboard-focus contract.
- Keeps the cached Bangumi all-time ranking as the default index and uses live subject search for explicit searches and filters.
- Search results continue into the existing Eizo Bangumi subject-detail flow, including collection and community features.
- Cards prefer the original/native title and show the Chinese title as the secondary label.
- Added a v1.3 anime-index regression contract and advanced Bangumi-search unit coverage.
- Moved Ranking & Discover directly below Anime Search and replaced the duplicate search glyph with a dedicated discovery/star icon.
- Moved the six Anime Search filter groups from the right sidebar into full-width horizontal rows above the results.
- Removed the Load More button; reaching the end of the list automatically loads the next 50 entries.
- When embedded subtitles exist, the primary subtitle selector is reserved for embedded tracks; the primary position/background controls stay disabled and a note explains that embedded-subtitle styling cannot be adjusted. External subtitles are selected from the secondary subtitle section.
- Fullscreen left/right long-press temporary speed no longer summons the bottom controls; a compact translucent top-center speed banner is shown instead.
- Clicking the video surface toggles play/pause in both windowed and fullscreen modes.
- Bumped product / MSIX versions to 1.3.0 / 1.3.0.0.

## Acceptance focus

- Bangumi navigation must no longer expose Anime Blogs and must show Anime Search instead.
- The page must not use WebView or WebView2.
- Search, sorting, six top horizontal filter groups, poster results, and automatic paging must be native WinUI 3 UI.
- Filter combinations must map to Bangumi subject-search anime, tag, and year constraints.
- Clicking a result must open the existing Bangumi subject detail.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
