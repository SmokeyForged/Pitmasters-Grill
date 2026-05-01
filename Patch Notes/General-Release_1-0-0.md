# Pitmasters Grill — General Release v1.0.0 Patch Notes

## Release Theme

Pitmasters Grill v1.0.0 is the first **General Release** of PMG.

This release moves PMG out of technical-preview framing and into a fuller daily-use build: a faster, cleaner, more durable local-intel board with stronger public killmail freshness, better cockpit layout behavior, improved diagnostics, and a more complete application identity.

The big 1.0 theme is simple:

> PMG should feel like a cockpit instrument: fast to read, hard to break, and useful during live play without pretending to know more than the public evidence supports.

v1.0.0 folds in the unreleased v0.9.6 candidate work and the final 1.0 hardening passes.

---

## Highlights

- Graduated PMG from Technical Preview to **General Release**.
- Added the **Grill / Analysis / Intel / Ignore List / Settings / Help** top-level workflow.
- Added the PMG application icon, header branding, executable icon support, and Windows tray icon.
- Made Panel/Board-style behavior the default PMG experience.
- Renamed the theme toggle to **Enable PMG Themes**.
- Added **Today’s Freshness** for visible-pilot same-day/recent zKill repair.
- Added **Historical Freshness** for visible-pilot recent completed-day repair.
- Added **Background Historical Repair** after startup with cooldown/checkpoint protection.
- Added optional **R2Z2 live zKill feed** support for ambient live killmail ingestion.
- Added killmail DB write coordination to reduce SQLite write-conflict risk.
- Added pilot **Watchlist** support with watched-first sorting.
- Added persistent window position/size and improved multi-monitor restore behavior.
- Added saved board column layout, reset behavior, default Character A-Z sorting, and better column resizing/autofit.
- Added relative **Last Seen** values such as `3h ago` or `4d ago`.
- Added Analysis summary surfaces for visible-pilot composition and context review.
- Added EVE Session Context confidence display for copied-local context.
- Expanded diagnostics for freshness, live feed state, checkpoints, and background repair settings.
- Added a Help tab with PMG-relevant shortcuts and workflow reminders.

---

## General Release Status

PMG is no longer presented as a technical preview in the application/version framing.

This does not mean PMG claims perfect intelligence or complete data coverage. PMG remains evidence-first:

- It works from user-provided local lists and public data.
- It does not read EVE client memory.
- It does not inspect network traffic.
- It does not automate gameplay.
- It does not use private ESI character data.
- It does not claim live grid, cloak, jump, or current-location certainty.

General Release means the app is now intended as a usable public release build, with clearer boundaries, better diagnostics, and stronger default workflows.

---

## New Freshness Architecture

v1.0.0 introduces a much stronger killmail freshness model.

PMG now has four complementary freshness layers:

1. **Archive Backfill**
   - Maintains the local historical baseline from completed public zKill archive days.

2. **R2Z2 Live Feed**
   - Optional live zKill feed ingestion for ambient near-live public killmail deltas.
   - Disabled by default and user-controlled.

3. **Today’s Freshness**
   - Manual visible-pilot repair for same-day/recent zKill-known killmails that may not have reached PMG through R2Z2.

4. **Historical Freshness**
   - Manual visible-pilot repair for recent completed days where public killmails may have appeared after the original archive pull.

5. **Background Historical Repair**
   - Startup enrichment pass that checks a bounded local candidate pool after the UI loads.
   - Uses cooldown/checkpointing so PMG does not repeatedly hammer the same pilots/windows.

The result is a more resilient intel flow: archive baseline, optional live deltas, manual visible-pilot repair, and bounded background cleanup.

---

## Today’s Freshness

Today’s Freshness is a user-triggered repair tool for currently visible pilots.

It helps with cases where:

- a pilot has a recent zKill-known kill,
- R2Z2 did not ingest that exact killmail,
- the board still shows older local derived intel.

Today’s Freshness queries recent zKill activity for visible pilots, fetches missing full killmails through ESI where needed, and imports them through PMG’s incremental derived-intel path.

Validated behavior during the 1.0 cycle included updating visible board data for pilots whose recent same-day zKill activity was missing locally.

---

## Historical Freshness

Historical Freshness repairs recent completed days for currently visible pilots.

This exists because a completed archive day can still become stale if killmails are posted to zKill after PMG originally imported that day.

Historical Freshness now:

- checks recent completed UTC days,
- compares zKill-known killmail IDs against PMG’s freshness-seen records,
- fetches missing ESI killmails when needed,
- imports missing observations without replacing archive-day state,
- dedupes on rerun so the same repairs are not double-counted.

Important design boundary:

Historical Freshness improves PMG’s local view for known/visible pilots. It is not a global proof that every public killmail for every completed archive day exists locally.

---

## Background Historical Repair

PMG can now perform bounded historical repair after startup.

The background repair pass:

- starts only after the UI has rendered,
- waits a configured delay,
- uses watched, visible, and recently seen local pilots as candidates,
- respects a max-pilots-per-run cap,
- uses checkpoint/cooldown state,
- exits cleanly instead of running as an open-ended daemon,
- avoids long startup-blocking rate-limit sleeps,
- remains user-visible/configurable.

Manual Today’s Freshness and Historical Freshness remain available even when background repair is disabled.

---

## Optional R2Z2 Live zKill Feed

v1.0.0 adds optional R2Z2 live zKill feed support.

The live feed can ingest recent zKill-known killmails into PMG’s local derived-intel database and help bridge the gap between daily archive updates and current public activity.

R2Z2 behavior in PMG:

- disabled by default,
- user-controlled from Intel,
- uses a non-blank PMG user agent,
- includes pacing/backoff behavior,
- honors caught-up and rate-limit states,
- tracks seen killmail IDs to avoid duplicate imports,
- writes through the shared incremental import path.

R2Z2 is a live freshness enhancer, not a complete same-day backfill guarantee. Today’s Freshness and Historical Freshness exist to repair targeted gaps when needed.

---

## Killmail Import and Database Safety

v1.0.0 adds stronger coordination around killmail DB writes.

PMG now uses shared write coordination for killmail import/update paths to reduce the chance of overlapping SQLite writes from:

- archive backfill,
- R2Z2 live feed,
- Today’s Freshness,
- Historical Freshness,
- Background Historical Repair,
- reset/rebuild flows.

Full reset/reseed behavior now clears historical freshness checkpoints when the underlying local freshness/seen state is rebuilt. This prevents old cooldown checkpoints from suppressing repairs after a reset.

Freshness repair paths remain separate from archive-complete semantics:

- they do not mark archive days complete,
- they do not replace archive days,
- they do not take over `day_import_state`,
- archive backfill remains the historical baseline authority.

---

## Board, Grill, and Layout Improvements

The board received major daily-use hardening.

### Grill workflow

The former board workflow has been reshaped around the **Grill** tab and Panel/Board-style behavior.

The Grill remains the primary live-read surface: paste local, scan visible pilots, right-click for detail, double-click for zKill.

### Column layout

Board column behavior was substantially hardened:

- column order and width persist,
- reset restores a safe default layout,
- invalid saved layouts are discarded defensively,
- manual column resizing works,
- columns resize with the window,
- the rightmost visible column fills to the inner right edge,
- hiding right-side columns transfers that fill behavior to the next visible column.

The default board sort is now Character A-Z while preserving watched-first grouping.

### Last Seen display

The `Last Seen` display now uses relative time:

- same-day sightings show rough hour/minute recency,
- older sightings show day distance,
- sorting still uses the underlying timestamp rather than the display text.

### Window persistence

PMG now better remembers window position, size, and mode, including multi-monitor restore behavior.

The restore logic avoids blindly forcing PMG back to the primary monitor unless saved bounds no longer intersect a visible work area.

---

## Analysis Tab

v1.0.0 adds an Analysis workflow for when the user has time to review the pasted local set more deliberately.

Analysis surfaces include aggregated local context such as:

- visible pilot counts,
- corp/alliance concentration,
- top groups by visible count,
- confirmed cyno / bait / signal breakdowns,
- clickable zKill-oriented list behavior where supported.

This is intended for slower reads and broader composition review, while Grill remains the fast board view.

---

## EVE Session Context

PMG can now surface an EVE Session Context confidence read when local is copied.

This helps distinguish likely EVE-client local copies from unrelated clipboard content and can help PMG display contextual session hints when available.

The feature is intentionally confidence-based and bounded:

- it is used for context, not gameplay automation,
- it does not claim perfect live state,
- non-EVE clipboard sources are still treated carefully.

---

## Watchlist

v1.0.0 includes manual pilot Watchlist support.

Users can:

- watch/unwatch pilots,
- see a watch marker beside watched pilots,
- keep notes and watch state separate,
- keep watched pilots pinned above non-watched pilots.

Watchlist is manual attention state. It is not a threat signal and does not change cyno, bait, tackle, or killmail evidence semantics.

Sorting behavior was hardened so watched pilots remain pinned while column sorts apply inside watched and non-watched groups.

---

## Help Tab and Shortcuts

A new Help tab gives users a built-in quick reference for PMG behavior and shortcuts.

The Help tab covers core PMG interactions such as:

- copying EVE local to populate the Grill,
- right-clicking a row for PMG pilot details,
- double-clicking rows or supported Analysis items for zKill,
- using `Ctrl+Home` to reset/recenter PMG if it gets pushed off-screen,
- using Today’s Freshness and Historical Freshness from Intel,
- understanding background repair and R2Z2 live feed behavior.

---

## Application Identity and Tray Icon

PMG now has a stronger application identity:

- new PMG app icon,
- executable icon support,
- in-app header branding,
- Windows tray / notification-area icon,
- tray menu for showing PMG or exiting cleanly.

Icon loading was hardened so startup-critical XAML no longer crashes if an icon asset cannot be decoded in that path.

---

## Settings and Theme Updates

The old dark-mode label has been replaced with **Enable PMG Themes**.

Panel/Board-style behavior is now the default experience. The older panel-mode control was hidden for General Release so PMG presents a cleaner, more intentional cockpit-style layout.

Background Historical Repair has a visible setting so users understand and can control startup repair behavior.

---

## Diagnostics Improvements

Diagnostics were expanded for General Release.

Diagnostics now include broader visibility into:

- R2Z2 live feed status,
- Today’s Freshness status,
- Historical Freshness status,
- Background Historical Repair settings,
- historical freshness checkpoint counts,
- `live_killmail_seen` counts by source,
- archive status and boundary-day state,
- settings relevant to freshness and repair behavior.

Diagnostics remain intended for troubleshooting without exposing private credentials, unrelated local files, raw chat logs, or raw killmail JSON dumps.

---

## Intel Status and Archive Boundary Wording

Intel status wording was improved around the daily zKill archive boundary.

If PMG has reached the latest published archive but the newest expected UTC archive day is not published yet, the app now communicates that state more clearly instead of making the local cache look broken.

This helps users distinguish between:

- PMG missing data it should import,
- zKill archive data not being published yet,
- background freshness/repair layers still operating normally.

---

## Bug Fixes and Hardening

v1.0.0 includes a large number of fixes and release-hardening changes, including:

- fixed startup/runtime crashes from icon resource decoding in startup-critical XAML,
- fixed board column right-edge gutter behavior,
- fixed column resizing/autofit behavior after hiding right-side columns,
- fixed default sort behavior for Character A-Z with watched-first grouping,
- fixed archive boundary status wording when the latest archive day is not published yet,
- fixed historical freshness checkpoints surviving reset/reseed in a way that could suppress repair,
- fixed potential overlapping manual freshness operations,
- improved DB write coordination for concurrent killmail update paths,
- improved R2Z2 rate-limit and caught-up handling,
- improved background repair behavior during rate-limit states,
- improved diagnostics for freshness/background repair states,
- preserved manual column resizing while keeping the board flush to the inner right edge,
- preserved PMG startup responsiveness by deferring background work until after UI render.

---

## Upgrade Notes

### Existing users

Existing PMG users can update normally.

On first launch after updating, PMG may perform background freshness checks or archive status evaluation. This is expected.

If local killmail intel appears stale:

1. Open Intel.
2. Let archive status settle.
3. Use Today’s Freshness for visible current/recent pilot repair.
4. Use Historical Freshness for visible recent completed-day repair.
5. Use reset/rebuild tools only if normal freshness/update paths do not recover the expected data.

### R2Z2

R2Z2 live feed is optional and disabled by default. Users can enable it from Intel if they want ambient live zKill-known killmail ingestion.

### Background repair

Background Historical Repair is bounded and cooldown-protected. It improves local freshness for known/recent pilots, but it is not a global zKill archive audit.

---

## Validation Notes

Validated during the 1.0 cycle:

- app builds successfully,
- runtime startup succeeds,
- PMG opens without splash/MainWindow icon crashes,
- tray icon appears and can restore/exit PMG,
- board columns resize and fill correctly,
- column visibility changes preserve right-edge fill behavior,
- manual column resizing works,
- default Character sort works,
- Last Seen relative display works,
- Today’s Freshness updates visible-pilot intel,
- Historical Freshness imports missing recent completed-day killmails and dedupes on rerun,
- Background Historical Repair schedules after UI render and respects cooldown/checkpoints,
- R2Z2 live feed ingests data and handles caught-up/rate-limit states,
- diagnostics export works and includes the new freshness fields,
- Help tab renders,
- PMG Themes / Panel behavior changes work as expected.

---

## Known Notes and Limitations

PMG remains evidence-first.

Public data can be delayed, incomplete, or missing. zKill, ESI, and daily archives can disagree temporarily or lag behind real activity.

PMG does not claim:

- live jumps,
- current grid presence,
- current location certainty,
- cloak status,
- private fitting visibility,
- private ESI character data,
- gameplay automation.

Freshness tools improve PMG’s local public-evidence cache. They do not make public data complete or private data visible.

---

## Release Summary

Pitmasters Grill v1.0.0 is the release where PMG becomes a fuller daily-driver local intel companion.

The core loop is now stronger:

1. Copy local.
2. Read the Grill.
3. Use Analysis when time allows.
4. Let archive/R2Z2/freshness repair improve public killmail-derived context.
5. Open zKill when deeper manual review is needed.

PMG still does not play the game for you. It gives you a faster, cleaner read from the public evidence you already could have checked manually — and keeps that evidence organized enough to matter during live play.
