# Eizo v0.5.0

## Stage 1: multi-source media model foundation

- Adds a provider-agnostic Eizo.Media domain layer.
- Introduces EizoMedia, EizoSeries, EizoSeason and EizoEpisode hierarchy models and projects aggregated library titles into that hierarchy.
- Separates MediaFormat, MediaContentDomain and MediaOrigin into independent dimensions.
- Allows one media identity to retain TMDB, Bangumi, AniList and other External IDs without using a provider ID as the Eizo internal ID.
- Moves CatalogSubjectModel.Key to the Eizo internal media ID. Legacy metadata|... / recognition|... values are retained as GroupingKey match evidence and diagnostics only.
- Promotes a provider ID to a higher scope only when every member at that scope carries the same ID, preventing a single season ID from becoming the identity of an entire series.
- Keeps Recognition and Metadata as provenance/evidence and projects them into the internal model.
- Moves the media catalog to schema v2 while retaining schema v1 read migration.
- Makes library classification prefer MediaFormat / MediaContentDomain while preserving the existing fallback.
- Extends the recognition report with Eizo item/subject IDs, GroupingKey, format, domain, origin and External IDs for real-library verification.
- Updates Bangumi / AniList provider User-Agent strings to Eizo 0.5.0.

Stage 1 intentionally adds no new TMDB or AniList network traffic and does not redesign the 0.4.7 detail-page framework. Core TMDB movie/TV integration remains the next stage.
