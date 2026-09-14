# Eizo v0.5.8

## Stage 5: scan, scrape and persistence pipeline close-out

- Formalizes the source scan order as Discover → reuse Metadata → scrape Metadata → Commit → UI Changed.
- Local media and WebDAV continue through the same ScanSourceAsync metadata pipeline.
- Resolved Metadata is reused when Recognition and runtime provenance are unchanged, avoiding repeated provider traffic on every scan.
- Adds last-known-good protection: normal scans keep existing titles, artwork, episode data and credits when providers hit temporary failures such as 429, 503 or timeouts.
- Explicit re-scrape can still replace old data with a genuine unresolved result, while transport failures keep the last successful snapshot.
- Adds Fresh / Reused / RetainedLastKnownGood / Failed refresh states.
- Adds LastRefreshAttemptUtc and RefreshErrors to snapshots and recognition diagnostics.
- Identity bindings are read before enrichment and updated only after successful resolution.
- Moves catalog persistence to schema v3 while retaining v1/v2/v3 migration reads.
- Final UI Changed notification remains after the complete source commit, so partial scans do not replace the previous stable library state.

This completes the infrastructure tasks before full real-content integration: media model, TMDB, routing, multi-source merge, content profiles, persistent identities and the stable scan/persistence pipeline.

- Removes the old two-phase coordinator behavior. Auto-scrape now runs inside the same ScanSourceAsync instead of committing discovery first and launching a second Metadata job.
- Local and WebDAV sources commit once only after Recognition and Metadata processing complete, so the library never switches to a partial intermediate state.
