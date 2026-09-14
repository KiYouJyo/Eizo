# Eizo v0.5.1

## Stage 2 / Slice A: TMDB core media identity

- Starts the real TMDB Series / Episode identity path on top of the 0.5.0 multi-source model.
- Adds EpisodeSeasonNumber and ProviderEpisodeId to Metadata snapshots so a series ID and an exact episode ID are different scopes.
- Persists the exact TMDB Episode ID when resolution succeeds while retaining the matching Episode Still.
- Projects exact episode ExternalIds into the EizoSeries → EizoSeason → EizoEpisode hierarchy.
- Never copies a Series ID into Episode ExternalIds; for example TMDB Series 1396 and Episode 62085 remain distinct identities.
- Extends recognition diagnostics with ProviderEpisodeId and MetadataEpisodeSeason.
- Adds TMDB Series/Episode/Still integration coverage.

This slice deliberately does not introduce the full Stage 3 Provider Router. TMDB Season IDs, cast/crew and richer general-media fields remain follow-up 0.5.x work.
