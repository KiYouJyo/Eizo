English | [简体中文](RELEASE-NOTES-v0.4.1.md) | [日本語](RELEASE-NOTES-v0.4.1.ja.md)

# Eizo v0.4.1

## Real library category pages and scraped-media priority

- Connects the Anime, Movies and Series library pages to the real Catalog data instead of CategoryView demo content.
- Category pages share the existing Catalog → Subject → Detail → Playback path with the aggregate library and only filter by the matching MediaCategoryKind.
- Aggregate and category libraries prioritize titles that have both a scraped poster and useful resolved Metadata.
- Within each priority tier, the existing title A–Z ordering is preserved. The aggregate library also retains its current category ordering.
- When an aggregated title has multiple resolved Metadata snapshots, poster-backed Metadata is preferred for title-level presentation so an available poster is not replaced by an empty card.
- Updates Navigation IA and Library Aggregation UI contracts to cover real category-page routing, category filtering, Metadata-priority ordering and poster-preferred aggregation.
- Preserves the real playback queue, external subtitles, dual subtitles, WebDAV playback and external Metadata runtime mechanisms delivered in Eizo 0.4.0 and keeps them in regression acceptance.

Acceptance for this version covers Release compilation, signed Windows x64 MSIXBundle installation and launch, Recognition / Metadata tests, library contracts, player / subtitle regression checks, and one-click package validation.
