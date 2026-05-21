# YouTubeSearcher

Windows desktop app that walks a local MP3 library, finds the YouTube video for each track, scores the match, and writes results + daily view-count stats to a SQL Server database. Runs interactively or on a schedule.

Repo: https://github.com/davidSarki/YouTubeSearcher
Owner: David Sarkisian / Media World Vision

---

## What it does (end-to-end flow)

1. **User picks folders.** A TreeView with checkboxes ([DirectoryTreeViewModule.vb](DirectoryTreeViewModule.vb)) lets the user select which directories to process. Tree state is persisted between sessions.
2. **Scan MP3 files.** For each file, [`ParseMp3InfoEnhanced`](GeneralModule.vb#L59) uses TagLibSharp to read ID3 tags: artist, title, duration, genres, and custom `TXXX` user-text frames (including a `RATING` field that becomes `Mp3Info.Rating`).
3. **Search YouTube.** Two modes, selectable per session ([YouTubeSearcherModule.vb](YouTubeSearcherModule.vb)):
   - **API mode** — YouTube Data API v3 with a user-supplied key (`SearchYouTubeAPI`)
   - **Basic mode** — web-scrape of YouTube search results (no key, regex/JSON parsing of the embedded payload)
   - Both fall back to regex extraction if Newtonsoft.Json parsing fails on the response.
4. **Score candidates.** [`ArtistTitleMatcher2.GetMatchPercentage`](ArtistTitleMatcher2.vb#L40) does language-aware fuzzy matching of `"Artist - Title"` strings (separates main vs featured artists, normalizes title words). Returns 0–100. The best result above threshold wins.
5. **Persist.** Best match is upserted into `YT_Songs`. On scheduled runs the app re-fetches view counts and upserts into `YT_Stats` keyed on `(YTVideoID, StatDate)` — either via the manual exists-check path ([`InsertYTStats`](GeneralModule.vb#L252)) or via a single `MERGE` ([`InsertYTStatsWithMerge`](GeneralModule.vb#L299)).
6. **Auto mode.** When `AutoProcessingEnabled` is on, `Timer1` fires at the configured `ScheduledTime` daily and re-runs steps 2–5 unattended.

---

## Database schema (inferred from code)

| Table | Columns |
|---|---|
| `YT_Songs` | `YTVideoID` (PK), `YTArtistTitle` (YouTube video title), `ALIAS` (MP3 `Artist - Title`), `PublishDate`, `YTChannelName`, `Lang` |
| `YT_Stats` | `YTVideoID`, `StatDate`, `ViewCount` — unique on `(YTVideoID, StatDate)` |

Lookup path: [`GetVideoIdByAliasLike`](GeneralModule.vb#L167) tries `LIKE` first (fast path with `^`-escaped wildcards), then falls back to scanning all rows and applying `ArtistTitleMatcher2` for fuzzy match — default threshold 80%.

No migrations folder; schema lives only in DDL outside this repo.

---

## Tech stack

- **Language / runtime:** VB.NET, `net8.0-windows`, WinForms (`MySubMain=true`, single-instance off)
- **NuGet:** `Microsoft.Data.SqlClient` 6.1.1, `Newtonsoft.Json` 13.0.3, `TagLibSharp` 2.3.0
- **IDE:** Visual Studio 2022 (solution format 17.10)
- **Network:** shared `HttpClient` (initialized lazily in [`InitializeYouTubeSearch`](YouTubeSearcherModule.vb#L41), disposed on form close)

---

## Module map

| File | Role |
|---|---|
| [YouTubeSearcher.vb](YouTubeSearcher.vb) | Main form. Form load/close, TreeView wiring, Start/Stop processing, log textbox, cancellation token, scheduled timer |
| [YouTubeSearcher.Designer.vb](YouTubeSearcher.Designer.vb) | Designer-generated controls for main form |
| [GeneralModule.vb](GeneralModule.vb) | `Mp3Info`, `YTSongRecord`, `FormSettings` classes; MP3 parsing; all DB operations |
| [YouTubeSearcherModule.vb](YouTubeSearcherModule.vb) | YouTube search: API + basic scrape, JSON/regex parsing, best-match selection |
| [YouTubeStatsModule.vb](YouTubeStatsModule.vb) | Daily view-count fetching for stored videos |
| [ArtistTitleMatcher2.vb](ArtistTitleMatcher2.vb) | Fuzzy matcher: tokenization, transliteration, featured-artist split, scoring |
| [DirectoryTreeViewModule.vb](DirectoryTreeViewModule.vb) | TreeView init, image list, checkbox cascade, persisted state |
| [AddEditForm.vb](AddEditForm.vb) | Dialog to add/edit a `YT_Songs` row manually |
| [CustomFileForm.vb](CustomFileForm.vb) | Dialog for ad-hoc file selection outside the tree |
| [ApplicationEvents.vb](ApplicationEvents.vb) | `MyApplication` partial class (stubs only — no custom startup logic yet) |
| [My Project/](My%20Project/) | VB.NET project metadata, `launchSettings.json`, publish profile |

---

## Persisted user settings

[`FormSettings`](GeneralModule.vb#L7) is serialized between runs and exposed in the main form UI:

| Property | Purpose |
|---|---|
| `ApiKey` | YouTube Data API v3 key |
| `UseAPI` / `UseBasic` | Which search backend to use |
| `DebugMode` | Verbose logging to the in-form log textbox |
| `AutoProcessingEnabled` | Enable the daily scheduled run |
| `ScheduledTime` | Time of day for auto runs (default 02:00) |
| `TreeViewStatePath` | Where the persisted tree-check state is saved |
| `langText` | Language hint passed to the matcher |
| `SQLCpnnection` | DB connection string (note: field is misspelled — `Cpnnection`) |

`ConnectionString` is set from the form's `SQLString` textbox at form load — there is no hardcoded server. An example commented-out string lives at [GeneralModule.vb:23](GeneralModule.vb#L23).

---

## Build & run

```powershell
dotnet restore
dotnet build
dotnet run --project YouTubeSearcher.vbproj
```

Or open [YouTubeSearcher.sln](YouTubeSearcher.sln) in Visual Studio 2022 and press F5.

The first run needs: a SQL Server reachable from the configured connection string with the `YT_Songs` and `YT_Stats` tables, and (for API mode) a YouTube Data API v3 key.

---

## Current situation

_Newest entries on top. Update freely as the project evolves._

- **2026-05-21** — Fixed two build-blocking issues: removed the hardcoded `G:\…\Applications (17).ico` reference and `<PackageIcon>` from [YouTubeSearcher.vbproj](YouTubeSearcher.vbproj) (icon file wasn't present on this machine; element was NuGet-pack-only and not used by `dotnet build`), and corrected `<MainForm>Form1</MainForm>` → `<MainForm>YouTubeSearcher</MainForm>` in [Application.myapp](My%20Project/Application.myapp#L4). `dotnet build` now succeeds with 0 warnings, 0 errors. To re-add an app icon, drop an `.ico` in the repo and use `<ApplicationIcon>` (not `<PackageIcon>`).
- **2026-05-21** — Project documented in this file (`CLAUDE.md`) after first GitHub push.
- **2026-05-21** — Repository initialized and pushed to GitHub. Misspelling `YouTubeSeracher` → `YouTubeSearcher` corrected in `.sln`, `.vbproj`, and `launchSettings.json` (commit `e57b5d4`). Local working folder still named `YouTubeSeracher`; not changed by user decision.
- **2026-05-21** — `.gitignore` set up for VS / VB.NET: excludes `.vs/`, `bin/`, `obj/`, `*.user`, NuGet caches, `desktop.ini`.

---

## Known issues / things to watch

- **Two `InsertYTStats` paths** exist (manual check + `MERGE`). Pick one and delete the other to avoid divergence.
- **Regex JSON fallback in YouTube parsing** is fragile by design — when YouTube changes their payload shape, watch for parser warnings in the in-form log.
- **`SQLCpnnection` field name is misspelled.** Renaming requires migrating any persisted settings file that uses the old name.
- **Single-instance is off** (`<SingleInstance>false`), so two copies of the app can race on the same DB if launched twice with auto-processing enabled.
- **Local folder name typo.** `D:\Coding\YouTubeSeracher` — cosmetic, intentional, does not affect build or repo.

---

## Notes for future work

- No automated tests.
- No CI configured.
- No DB migrations — schema must be created manually.
- No README on GitHub yet (this file is the canonical project brief).
