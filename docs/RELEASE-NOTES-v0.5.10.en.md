[简体中文](RELEASE-NOTES-v0.5.10.md) | [日本語](RELEASE-NOTES-v0.5.10.ja.md) | English

# Eizo v0.5.10

## Real Content UI / Slice B: unified presentation

- Adds CatalogSubjectPresentation so library cards and the detail page no longer assemble the same metadata independently.
- Library work cards now read real poster, preferred/secondary title, year, type, episode count, genres and source state from one presentation model.
- The detail header uses the same model for titles, overview, poster, backdrop, year, genres, runtime, origin, production companies and production status.
- Provider/debug identity stays out of normal UI.
- Selecting a season keeps the existing layout but uses the real season poster and exposes season air date/overview through the existing selector tooltip.
- Episode cards add EpisodeAirDate to their secondary information line.
- Episode Still remains the image priority; a work poster is never repeated across every episode as a fake still.
- No page-framework redesign is introduced; this slice replaces content plumbing inside the finalized UI.
