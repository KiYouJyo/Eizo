# Eizo v0.5.0

## Stage 1: multi-source media model foundation

- Adds a provider-agnostic Eizo.Media domain layer.
- Introduces EizoMedia, EizoSeries, EizoSeason and EizoEpisode hierarchy models.
- Separates MediaFormat, MediaContentDomain and MediaOrigin into independent dimensions.
- Allows one media identity to retain TMDB, Bangumi, AniList and other external IDs together.
- Keeps Recognition and Metadata as provenance/evidence and projects them into the internal model.
- Moves the media catalog to schema v2 while retaining schema v1 read migration.
- Makes library classification prefer the internal media model while preserving the existing fallback.

Stage 1 intentionally adds no new TMDB or AniList network traffic and does not redesign the 0.4.7 detail-page framework.
