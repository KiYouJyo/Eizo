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


## Catalog hierarchy integration

The second Stage 1 pass projects aggregated catalog subjects into the internal model as well:

- each catalog subject now exposes an EizoMedia identity;
- non-movie subjects expose an EizoSeries with deterministic EizoSeason and EizoEpisode identities;
- subject-level ExternalIds keep only provider IDs that are consistent across the aggregated work;
- provider IDs with different values across seasons are not incorrectly promoted to the series scope;
- season and episode ExternalIds remain explicit scopes and are only populated when a provider supplies IDs for that exact level.

This keeps provider identity separate from the library hierarchy before TMDB routing is introduced.
