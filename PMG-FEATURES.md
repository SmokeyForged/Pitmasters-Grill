# PMG Features

Pitmaster's Grill, or PMG, is a Windows desktop intel companion for EVE Online.

PMG turns copied EVE local lists into a readable public-intel board. It is built for fast scanning during live play, with deeper analysis and freshness repair available when time allows.

This page explains what PMG currently does, how the main features fit together, and what PMG intentionally does not do.

---

## Feature Summary

PMG provides:

- copied-local parsing
- pilot, corporation, and alliance resolution
- a fast pilot board called the Grill
- public kill/loss context
- recent public ship observations
- cyno, tackle, bait, and watchlist signals
- visible-pilot analysis summaries
- ignore lists for pilots, corporations, and alliances
- local public killmail-derived intel caching
- archive backfill for historical baseline intel
- Today’s Freshness for recent visible-pilot repair
- Historical Freshness for recent completed-day repair
- bounded Background Historical Repair after startup
- optional R2Z2 live zKill feed
- zKill open-link workflows
- diagnostics bundle export
- saved window and board layout behavior
- PMG themes
- tray icon support
- Help tab with shortcuts and workflow reminders

PMG is designed to help players interpret public evidence faster. It does not replace judgment.

---

## Core Workflow

The basic PMG workflow is simple:

1. Launch PMG.
2. Copy an EVE local list.
3. Review the Grill board.
4. Use Analysis for summary context.
5. Use Intel freshness tools when public data needs repair.
6. Open zKill for deeper manual review when needed.

PMG is intentionally clipboard-driven. It does not hook into the EVE client, read client memory, inspect network traffic, or automate gameplay.

---

## Grill

The Grill is PMG’s main pilot board.

It shows visible pilots from a copied local list and enriches them with public context where available.

The Grill can show:

- pilot name
- corporation
- alliance
- public kill count
- public loss count
- average fleet-size context
- last public ship seen
- relative last-seen timing
- cyno hull observations
- watchlist status
- signal markers

Common Grill actions:

- copy EVE local to populate the board
- press `Insert` to toggle Board Mode on or off, then press `Insert` again to return to the normal layout
- right-click a row for pilot details
- double-click a row to open zKill
- hover over a row for quick sig details
- resize and reorder columns
- reset board layout from settings/help workflows
- hide optional columns from Settings
- hide board grid lines, reduce board text size, or switch the Grill board font from Settings -> Board Columns

The board is designed to stay readable in a compact panel-style workflow.

---

## Analysis

The Analysis tab gives a higher-level summary of the current visible board.

It is intended for moments when time allows a broader review instead of only scanning rows.

Analysis can surface:

- visible pilot count
- unique corporation count
- unique alliance count
- top visible alliances
- confirmed cyno breakdown
- visible composition signals
- current EVE session context when available

Supported Analysis lists can open zKill links where PMG has enough entity information.

---

## Intel

The Intel tab is where PMG reports local public-intel freshness and gives access to repair tools.

Intel includes:

- local killmail-derived intel status
- archive coverage status
- missing-day / latest-published archive messaging
- R2Z2 live feed controls
- Today’s Freshness
- Historical Freshness
- Diagnostics
- background historical repair status
- killmail cache controls

The Intel tab is also where PMG makes its limits more visible. Public intel can be stale, incomplete, delayed, or missing. PMG reports what it knows locally and provides tools to repair that local view where possible.

---

## Public Intel Freshness Model

PMG uses a layered freshness model.

### Archive Backfill

Archive Backfill builds the historical baseline from completed public zKill archive days.

It is the broadest baseline layer and is used to populate local killmail-derived intel over the configured history window.

Archive Backfill is the complete-day authority. Freshness repair does not replace archive semantics.

### R2Z2 Live Feed

R2Z2 is an optional live zKill-known killmail feed.

When enabled, PMG can ingest live public killmail deltas into the local intel database. This helps bridge the gap between completed archive days and recent public activity.

R2Z2 is disabled by default.

R2Z2 improves freshness for killmails PMG ingests from the stream. It does not guarantee complete same-day coverage.

### Today’s Freshness

Today’s Freshness is a manual repair tool for currently visible pilots.

It checks recent zKill-known activity for visible pilots, fetches missing public killmails where available, and imports them into PMG’s local derived intel.

Today’s Freshness is useful when a pilot is visible on the board and you want PMG to check for newer public activity that may not have arrived through R2Z2 or archive backfill yet.

### Historical Freshness

Historical Freshness is a manual repair tool for recent completed days.

It checks visible pilots across a small recent historical window, compares zKill-known killmail IDs against PMG’s local freshness-seen records, and imports missing public killmails where available.

Historical Freshness exists because a completed archive day can later become stale if a killmail is posted after PMG first imported that day.

### Background Historical Repair

Background Historical Repair is a bounded startup enrichment feature.

After PMG loads, it can check a limited local pool of known/recent pilots for missing historical public killmails. It respects cooldowns, rate limits, and local candidate limits.

This makes historical repair less dependent on users remembering to click a button.

Background Historical Repair is designed to be:

- delayed until after the UI is usable
- bounded
- cooldown-protected
- cancellable
- rate-limit aware
- non-blocking

---

## Ignore List

PMG supports ignore lists for:

- pilots
- corporations
- alliances

Ignored entities can be excluded from the visible board to reduce noise.

The Ignore List tab provides a central place to review and manage ignored entries.

---

## Watchlist

PMG includes watchlist support for pilots the user wants to track more closely.

Watchlisted pilots can receive more prominent row treatment and remain important during board review.

Watchlist behavior is intended for user-prioritized awareness, not automated decision-making.

---

## Settings

Settings control PMG behavior and presentation.

Settings may include:

- PMG Themes
- column visibility
- board layout reset
- background historical repair behavior
- R2Z2 live feed enablement
- window/layout behavior
- cache/history controls

Panel-style behavior is the default PMG experience.

---

## Help

The Help tab provides quick in-app reminders for relevant PMG shortcuts and interactions.

Examples include:

- copying EVE local to populate the Grill
- using `Insert` to toggle Board Mode and return to normal mode
- using `Ctrl+Home` to recover/reset the PMG window position
- right-clicking Grill rows for details
- double-clicking supported entities to open zKill
- using Intel freshness tools
- exporting diagnostics

---

## Diagnostics

PMG can export a diagnostics bundle for troubleshooting.

Diagnostics are intended to help identify app state and failures without requiring users to manually dig through local files.

Diagnostics may include:

- app/runtime information
- selected settings
- public-intel freshness status
- R2Z2 status
- Today’s Freshness status
- Historical Freshness status
- background repair status
- checkpoint counts
- freshness-seen counts by source
- recent log context

Diagnostics should not include secrets, private credentials, raw killmail JSON, chat log contents, or unrelated local files.

Users should still review diagnostics before posting them publicly.

---

## Branding and App Integration

PMG includes:

- application icon
- taskbar/window integration
- notification-area/tray icon
- in-app PMG branding
- General Release naming

The tray icon gives PMG a normal desktop-app presence while running.

---

## zKill Links

PMG uses zKill links as a manual verification path.

Where entity IDs are available, PMG can open zKill pages for:

- pilots
- corporations
- alliances
- public activity review

PMG’s links are intended to support human review, not replace it.

---

## Local Caching

PMG stores public-intel data locally to reduce repeated lookup cost and improve responsiveness.

Local caching supports:

- faster repeated board population
- historical baseline use
- public ship observation context
- cyno/tackle/bait-derived signals
- freshness repair imports
- diagnostics and troubleshooting context

Large history windows can increase local database size and startup/update workload.

---

## What PMG Does Not Do

PMG does not:

- read EVE client memory
- automate gameplay
- inspect network traffic
- scrape private client internals
- use private ESI character scopes
- bypass EVE mechanics
- identify cloaked ships
- prove live grid position
- prove real-time location certainty
- guarantee public data completeness
- replace player judgment

PMG is a public-evidence assistant.

---

## Data and Evidence Limits

PMG works with public and locally cached evidence.

That means PMG can be wrong, stale, incomplete, or delayed when public data is wrong, stale, incomplete, or delayed.

Important limits:

- zKill may not have every killmail.
- Some killmails are posted late.
- Archive days can become stale after initial import.
- Live feeds can miss items outside PMG’s local stream window.
- Entity resolution may fail or be delayed.
- Public ship observations do not prove current ship.
- “Last seen” means last public/local evidence known to PMG, not current certainty.

PMG tries to expose these limits rather than hiding them.

---

## Intended Use

PMG is best used as:

- a fast local-list triage tool
- a public-intel summarizer
- a board awareness aid
- a zKill workflow accelerator
- a local public-data cache
- a human judgment support tool

PMG is not an autopilot, fleet commander, bot, or oracle.

---

## Current Release Status

PMG is in General Release.

Current release:

- **Pitmasters Grill v1.0.0**
- General Release
- Windows desktop application

Latest release downloads are available from the GitHub Releases page.

---

## Related Docs

Useful follow-up documents:

- [`README.md`](./README.md)
- [`HOW-TO-NAVIGATE-THIS-REPO.md`](./HOW-TO-NAVIGATE-THIS-REPO.md)
- [`FIRST-TIME-USE.md`](./FIRST-TIME-USE.md)
- [`HOW-IT-WORKS.md`](./HOW-IT-WORKS.md)
- [`EVE-TOS-COMPLIANCE.md`](./EVE-TOS-COMPLIANCE.md)
- [`DEVELOPER-NOTES.md`](./DEVELOPER-NOTES.md)
- [`Patch Notes/`](./Patch%20Notes/)
