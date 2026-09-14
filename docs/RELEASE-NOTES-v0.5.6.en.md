[简体中文](RELEASE-NOTES-v0.5.6.md) | [日本語](RELEASE-NOTES-v0.5.6.ja.md) | English

# Eizo v0.5.6

## Stage 3 / Slice C: content-specific merge profiles

- Formalizes three merge profiles: Anime, Japanese live action and general live action.
- Anime keeps Bangumi work identity/text while TMDB is preferred for poster, backdrop, season poster and episode stills.
- Japanese live action uses TMDB for structure, visuals and general-media details while Bangumi remains a Japanese/Chinese semantic and community supplement.
- Western and other general live action uses TMDB as the core provider.
- AniList network access is explicitly skipped before real-content integration to keep the remaining stabilization work focused.
- Existing Bangumi character/voice-actor UX remains intact for Anime; TMDB credits stay supplemental at the Metadata layer.
- MergeProfile is persisted into Metadata snapshots and recognition diagnostics.
- Artwork now follows content-specific provider priority instead of first-non-empty behavior.

This closes the provider routing, field merge and artwork-policy foundation before identity persistence and scan-pipeline stabilization.
