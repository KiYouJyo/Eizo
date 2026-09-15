[简体中文](RELEASE-NOTES-v0.5.14.md) | [日本語](RELEASE-NOTES-v0.5.14.ja.md) | English

# Eizo v0.5.14

## Scraping Architecture Simplification

Eizo 0.5.14 makes TMDB the single online metadata authority for the local media library and restructures scan/scrape scheduling. Bangumi is removed from the local Metadata Provider / Router / Merge path while its tracking, calendar, discovery, ranking, comment and community features remain intact.

## Included

- Stop creating the Bangumi provider in the production library MetadataService; TMDB is the sole online metadata authority.
- Simplify manual matching to TMDB-only and remove the provider chooser from the normal user flow.
- Automatically treat legacy Bangumi-led library snapshots as migration candidates on the first 0.5.14 scan.
- Remove legacy Bangumi library identity after successful TMDB enrichment while preserving generic IDs such as IMDb.
- Ignore legacy Bangumi bindings in library scraping and prefer an existing TMDB subject ID whenever available.
- Replace global per-file serial enrichment with logical subject batches.
- Resolve one representative item first to establish a persisted TMDB subject identity, then reuse it for follower episodes.
- Organize each subject by season; process episodes inside a season in order so the first request warms the TMDB season cache.
- Allow up to three subjects and two seasons per subject to make progress concurrently while avoiding unbounded request fan-out and same-season cache stampedes.
- Keep catalog commit behavior batched so individual episode completion does not cause full-library refresh churn.
- Preserve Bangumi as an independent product module for tracking, calendar, discovery, ranking and community features.
