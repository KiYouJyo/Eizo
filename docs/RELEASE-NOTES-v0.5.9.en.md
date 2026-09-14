[简体中文](RELEASE-NOTES-v0.5.9.md) | [日本語](RELEASE-NOTES-v0.5.9.ja.md) | English

# Eizo v0.5.9

## Real Content UI / Slice A

- Keeps the established detail-page layout and adds one compact More entry instead of redesigning the page.
- Re-scrape uses the 0.5.8 metadata persistence pipeline and rebuilds the current detail tab from the latest CatalogSubject.
- Adds the actual manual-match UI: editable query, All/Bangumi/TMDB provider filter, candidate results and persistent provider Subject ID binding through the 0.5.7 identity layer.
- After manual matching, affected media sources are re-scraped and the detail page switches to the resulting real metadata.
- Existing manual bindings expose a Clear manual match action.
- Provider names are removed from normal user-facing subject metadata; provider IDs and provenance remain available in diagnostics.
- Adds a provider-neutral metadata candidate search API so the UI never talks directly to Bangumi/TMDB implementations.

This starts real-content UI integration while preserving the finalized layout. The next slice will close out library cards, header presentation, season/episode content and credit presentation.
