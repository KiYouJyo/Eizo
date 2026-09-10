English | [简体中文](RELEASE-NOTES-v0.3.3.md) | [日本語](RELEASE-NOTES-v0.3.3.ja.md)

# Eizo v0.3.3

## Recognition Integration & Library Validation

- Integrates the standalone offline `Eizo.Metadata.Recognition` engine, pinned to the validated Recognition v0.1.0 build.
- Local-folder and WebDAV scans feed logical relative paths only into Recognition. WebDAV authorities, usernames and credentials never enter Recognition input or diagnostic evidence.
- Library rows can show recognized series titles, season/episode numbers, episode titles, year and confidence while preserving the original filename as secondary information.
- Right-click a library item and choose `Recognition details` to inspect its Recognition Snapshot, title candidates and evidence against real media files.
- Ambiguous, low-confidence, unresolved and error results remain usable and never block scanning or playback or forcibly replace the original title.
- Playback `Locator` values remain untouched; Recognition performs offline parsing only.
- Adds cross-platform integration tests covering real-world Re:Zero, all-bracket Death Note, Ghost in the Shell ARISE Remux and Bilibili-tagged filename patterns.
