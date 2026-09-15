[简体中文](RELEASE-NOTES-v0.5.11.md) | [日本語](RELEASE-NOTES-v0.5.11.ja.md) | English

# Eizo v0.5.11

## Real Content UI / Slice C: per-title scrape and library actions

- Re-scrape is now a title-level operation instead of re-scraping an entire local/WebDAV source from one work card.
- Adds ScrapeSubjectMetadataAsync, which enriches only the media items belonging to the selected CatalogSubject and commits only those items.
- Adds ScrapeItemMetadataAsync as the lower-level path for future per-episode refresh.
- Keeps ScrapeSourceMetadataAsync for the explicit whole-source action on the Media Sources page.
- Detail-page re-scrape, manual match and clear-manual-match now use title-level APIs.
- Library work cards expose the same three actions through their right-click menu.
- Manual matching uses one shared MetadataMatchDialog for Bangumi/TMDB candidates.
- Applying a manual match refreshes only that work instead of the whole source.
