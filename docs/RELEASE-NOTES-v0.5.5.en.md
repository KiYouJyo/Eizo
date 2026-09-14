# Eizo v0.5.5

## Stage 3 / Slice B: field-level multi-source merge

- Resolved fallback providers now remain available as supplemental field sources after the Provider Router chooses the primary provider.
- The primary provider keeps work identity and canonical title. A Bangumi-primary anime is not blindly overwritten by TMDB; TMDB-primary live action follows the same rule.
- Missing fields are filled from supplemental providers: overview, release date, episode count, genres, runtime, production status, original language, origin countries, production companies, cast and crew.
- List fields such as genres, production companies and origin countries merge across providers with de-duplication.
- Subject ExternalIds merge provider identities so one EizoMedia can retain Bangumi/TMDB/IMDb/TVDB mappings together.
- Adds SeasonExternalIds and EpisodeExternalIds so season and episode identities can hold multiple exact provider IDs at the same time.
- Bangumi-primary anime can therefore retain exact TMDB season/episode IDs and TMDB episode stills.
- Adds FieldSources to record which provider supplied final fields such as CanonicalTitle, Overview, RuntimeMinutes and EpisodeThumbnailUrl.
- Adds MergeContributors to record all providers contributing to the current Metadata snapshot.
- Recognition diagnostics export scoped external IDs, contributors and field provenance.
- Adds field-merge and multi-provider scoped-ID unit coverage.

The next slice will refine artwork priorities and content-specific merge profiles for anime, Japanese live action and Western live action.
