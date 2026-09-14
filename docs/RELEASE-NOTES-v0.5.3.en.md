# Eizo v0.5.3

## Stage 2 / Slice C: TMDB general media details and credits

- Pins the published Eizo.Metadata 0.2.23 runtime.
- Brings TMDB genres, runtime, production status, original language, origin countries and production companies into Eizo snapshots.
- Adds general Cast / Crew records with TMDB person ID, role/job, department, profile image and ordering.
- Live-action TV and movies prefer TMDB cast/crew; Anime keeps the Bangumi character/voice-actor experience and falls back to Metadata credits when Bangumi data is unavailable.
- Preserves the established detail-page framework: the existing MetaText now naturally adds genre, runtime, origin and production-company information.
- Reuses the existing person cards while switching live-action semantics to Cast / Crew.
- Extends recognition diagnostics with Genres, Runtime, ProductionCompanies, Origin, Status, Language, CastCount and CrewCount.
- Breaking Bad integration coverage includes Bryan Cranston, Vince Gilligan, 47-minute runtime, US origin and production company data.

The next step is Stage 3: the formal Provider Router and field-level merge priorities.
