[简体中文](RELEASE-NOTES-v0.4.7.md) | [日本語](RELEASE-NOTES-v0.4.7.ja.md) | English

# Eizo v0.4.7

## Real Settings Integration

Eizo 0.4.7 reduces Settings to three focused pages—General, Metadata, and Playback—and removes display-only filler. Every retained option is persisted and connected to live application behavior.

### Highlights

- Simplifies Settings to General, Metadata, and Playback.
- General keeps UI language, appearance, and Bangumi account management.
- Removes text-only rules such as the former Japanese-media display card; title selection is handled internally according to the app language.
- Metadata adds automatic post-scan scraping, artwork enrichment, whole-library rescraping, and Metadata cache cleanup.
- Automatic scraping preserves the established independent pipeline: scan first, then start a separate Metadata job.
- Playback adds auto-play next episode, remembered playback rate, default rate, preferred audio language, preferred subtitle language, and preferred second subtitle language.
- Subtitle memory is semantic and title-scoped: embedded/external type, language, and name characteristics are stored instead of unstable per-episode Track IDs.
- Resolved titles use Provider + SubjectId as the preference key; unresolved media falls back to Recognition title/year.
- Primary/secondary subtitle positions and background opacity are editable from Settings and share the same persisted values used by the player.
- Simplified Chinese, Japanese, and English resources are updated together.

## Acceptance

The 0.4.7 acceptance chain covers Settings UI/runtime contracts, audio/subtitle preferences, independent Metadata auto-chaining, Recognition/Metadata/Cache/Bangumi/WebDAV regressions, Release x64 compilation, signed MSIX install/launch, protocol activation, real bundled component fallback invocation, and the one-click package.
