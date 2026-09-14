# Eizo v0.5.2

## Stage 2 / Slice B: TMDB season identity and content

- Pins Eizo.Metadata to the published 0.2.22 runtime.
- Keeps TMDB Series / Season / Episode provider IDs in distinct scopes.
- Adds ProviderSeasonId, SeasonTitle, SeasonOverview, SeasonAirDate and SeasonPosterUrl to Metadata snapshots.
- Makes EizoSeason carry a real title, overview, air date and season poster instead of only a season number.
- Projects exact Season ExternalIds and Episode ExternalIds separately into the EizoSeries hierarchy; Series IDs are never copied downward.
- Preserves the established detail-page layout while allowing the season selector to prefer provider-localized season titles.
- Extends recognition diagnostics with season ID/title/date/poster fields.
- Covers Breaking Bad TMDB Series 1396 → Season 3572 → Episode 62085.

The full Provider Router remains deferred. The next Stage 2 slice will add TMDB cast, crew, production companies and richer general-media detail fields.
