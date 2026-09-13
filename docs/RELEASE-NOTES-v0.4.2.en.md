English | [简体中文](RELEASE-NOTES-v0.4.2.md) | [日本語](RELEASE-NOTES-v0.4.2.ja.md)

# Eizo v0.4.2

## Bangumi public content integration

- Adds a dedicated `Eizo.Bangumi` data layer, keeping Bangumi network content separate from local `Eizo.Metadata` scraping responsibilities.
- Replaces the Broadcast Calendar, Seasonal Anime, and Rank & Discover placeholders with live Bangumi data while leaving My Following for the later account-integration release.
- Seasonal Anime now combines and de-duplicates all three months in a quarter and supports previous, next, and return-to-current-season navigation.
- Rank & Discover supports all-time, current-season, and current-year ranking scopes; all-time and current-year results support pagination.
- Broadcast Calendar uses the public `/calendar` endpoint and defaults to today's weekday.
- Title cards open an in-app Bangumi detail page with artwork, titles, air date, episode count, score, rank, collection count, summary, tags, and a link to the original Bangumi page.
- The Home current-season section no longer uses hard-coded examples and instead reads the current Bangumi quarter; View All navigates directly to Seasonal Anime.
- Adds local caching with 30-minute calendar, 2-hour ranking, 6-hour season, and 24-hour subject-detail lifetimes, including stale-cache fallback after network failures.
- Sends an Eizo 0.4.2 User-Agent compatible with Bangumi guidance and respects the public API's 50-item page limit.
- Adds Bangumi JSON/parser unit tests, public-integration contracts, and a live public API smoke workflow.
- Account login, collection writes, episode-progress sync, and automatic Bangumi-to-local-library binding remain out of scope for this release.

This release also regression-tests Eizo 0.4.1 library category pages, the external Metadata runtime, and Eizo 0.4.0 playback queue / external subtitle / dual-subtitle functionality.
