English | [简体中文](RELEASE-NOTES-v0.4.0.md) | [日本語](RELEASE-NOTES-v0.4.0.ja.md)

# Eizo v0.4.0

## Playback queue, external subtitles and dual subtitles

- Connects the playback queue to real aggregated library episodes, switches the actual media source from queue selection, and auto-advances at end of media.
- Previous / next chapter controls fall back to previous / next episode when the media has no chapters.
- Automatically discovers matching external subtitles beside local and WebDAV media, with SRT, VTT, ASS and SSA support.
- External primary and secondary subtitles now use Eizo's native WinUI overlay renderer, avoiding legacy ASS / SSA font styling.
- Primary and secondary subtitles each have independent vertical-position and background-opacity controls; all four appearance settings persist across episode changes, queue switches and app restarts.
- Subtitle overlays now use a near-full-width horizontal layout, preferring a single line and wrapping only when necessary.
- Fixes special / OVA grouping and playback-source selection. Explicit OVA / OAD / ONA / SP / SPECIAL files no longer collapse into regular episodes with the same number, including when stale Recognition snapshots are present.
- Prevents special-episode titles from being polluted by regular-season Metadata.
- Removes the Playback runtime row from About > independently updateable components, leaving Metadata as the visible component-update entry while preserving the Playback runtime and bundled fallback architecture.
- Continues to use Eizo.Metadata v0.2.20 with version, SHA-256, compatibility, pending → restart → active and failure-fallback safeguards for the external Metadata runtime.

This release is validated through Release compilation, signed Windows x64 MSIXBundle installation and launch, player/subtitle contracts, Stage 7 Playback Integration, and real Metadata bundled-fallback checks.
