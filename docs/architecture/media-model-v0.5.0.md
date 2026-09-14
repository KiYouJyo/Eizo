# Eizo 0.5.0 media model foundation

Eizo 0.5.0 separates media identity from metadata providers.

## Stage 1 scope

- Eizo.Media owns provider-agnostic media identity.
- MediaFormat describes the playback/library shape: Movie, TvSeries, Ova, Ona and Special.
- MediaContentDomain describes the content domain: Animation, LiveAction and Documentary.
- MediaOrigin is independent from both format and provider.
- ExternalIds can retain TMDB, Bangumi, AniList and other provider IDs at the same time.
- EizoSeries, EizoSeason and EizoEpisode establish separate identity scopes for future provider routing.
- Existing Recognition and Metadata snapshots remain source/provenance data; they are projected into the internal model instead of defining the library category directly.

## Compatibility

The catalog schema moves from 1 to 2. Schema 1 catalogs are loaded and projected in memory, then written as schema 2 on the next save. No media source or playback behavior changes in Stage 1.

## Deferred to later stages

Stage 1 intentionally does not add TMDB or AniList network traffic and does not change the established 0.4.7 detail-page layout. Provider routing, field-level merge rules, TMDB episode stills and Bangumi/AniList enhancement remain later 0.5/0.6 stages.
