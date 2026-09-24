[简体中文](RELEASE-NOTES-v1.2.1.md) | [日本語](RELEASE-NOTES-v1.2.1.ja.md) | English

# Eizo v1.2.1

## Single-file manual video cache

Eizo 1.2.1 changes only the explicit/manual video-cache path. Videos that the user deliberately caches are no longer persisted as many 4 MB `.blk` files. They are assembled into one complete media file with the source container extension. Automatic playback pre-cache and random-access caching continue to use the existing block model.

## Included

- Added a dedicated `manual-video:` cache group so explicit downloads are isolated from automatic playback cache groups.
- Keeps WebDAV range downloads, pause/resume, progress, and speed tracking, but writes each range sequentially into one temporary file instead of persisting every range as a cache block.
- Commits the completed download as one media file while preserving source extensions such as `.mkv`, `.mp4`, and `.webm`.
- Reuses matching automatic-cache blocks when they already exist, avoiding unnecessary network downloads without changing the final manual-cache format.
- Plays completed manual caches directly from the local media file while retaining compatibility playback for legacy block-based offline caches.
- Keeps cache accounting, pinned-cache protection, open-folder actions, and deletion in the unified cache index.
- Existing legacy `.blk` manual caches are not forcibly rewritten during upgrade to avoid expensive large-file copies at startup. Delete and cache them again to obtain the single-file format.
- Automatic WebDAV playback caching is intentionally unchanged and continues to use small blocks for random access and working-set trimming.

## Acceptance focus

- A newly created manual cache should persist as one complete media file under `Cache\media`, not hundreds of `.blk` files.
- The file extension should match the WebDAV source file.
- Completed manual cache playback should work offline from that single file.
- Deleting the manual cache should remove the complete media file.
- Ordinary WebDAV online playback should continue to use the existing `.blk` automatic cache.

## Compatibility

- Windows 10 2004 / Windows 11
- x64
