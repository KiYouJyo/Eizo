# Eizo v0.4.0

## Player content acceptance build

- Connects the playback queue to real aggregated library episodes instead of demo entries.
- Queue selection switches the actual media source and playback auto-advances at end of media.
- Previous/next chapter controls fall back to previous/next episode when the media has no chapters.
- Automatically discovers matching external subtitles beside local and WebDAV media.
- Supports SRT, VTT, ASS and SSA external subtitles.
- Keeps the primary subtitle on Eizo.Playback / LibVLC and adds an independent secondary subtitle overlay for dual subtitles.
- Secondary subtitles remain synchronized across playback, seeking and episode changes.
- Preserves independently updateable Playback and Recognition/Metadata components with bundled fallback.

This package is intended for hands-on Eizo 0.4.0 player acceptance testing.
