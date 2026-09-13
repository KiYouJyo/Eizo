[简体中文](RELEASE-NOTES-v0.4.5.md) | [日本語](RELEASE-NOTES-v0.4.5.ja.md) | English

# Eizo v0.4.5

Bangumi subject details now act as a desktop entry point for browsing community content inside Eizo. This release remains read-only: it does not change the local-library scraping path and does not write collections, replies, or reactions back to Bangumi.

- Added isolated `BangumiCommunityClient` / `BangumiCommunityRepository` layers so the Private API `/p1` does not contaminate the stable Public API `/v0` path.
- Added Short Comments, Reviews, Discussions, and Related tabs to Bangumi subject details.
- Short comments show user identity, collection state, rating, update time, and reaction count, with paging and filters for Wish / Completed / Watching / On hold / Dropped.
- Long reviews now open inside Eizo with full content, author, tags, view/reply counts, comments, and one level of nested replies.
- Subject discussions now open inside Eizo with the root post, replies, and one level of nested replies.
- Recommendations and related titles navigate directly to additional Bangumi detail tabs inside Eizo.
- Community BBCode is rendered as safe plain text, with Open on Bangumi retained as a web fallback.
- When signed in, Eizo reuses the existing 0.4.4 OAuth credential as an optional Bearer token; public community content remains readable while signed out.
- Failure of an individual Private API section does not take down the existing Bangumi subject-information page.
- Added Chinese, Japanese, and English community UI resources, parser/client unit tests, static integration contracts, and a live Community API smoke test.

Posting comments, creating topics, replying, liking, and playback-progress writeback are intentionally deferred to later releases.
