# How PMG Works

This document explains the technical model behind Pitmaster's Grill, or PMG.

PMG is a Windows desktop intel companion for EVE Online. It helps turn copied local lists into readable public-intel context while preserving a clear boundary: PMG assists human review; it does not automate gameplay or claim private certainty.

---

## High-Level Flow

The common flow is:

```text
Copied local list
→ clipboard guardrails
→ pilot parsing
→ entity resolution
→ local cache lookup
→ public-intel enrichment
→ Grill / Analysis / Intel display
```

PMG combines immediate clipboard input with locally cached public data.

---

## Input Model

PMG is clipboard-driven.

The user copies a local-style list, and PMG decides whether the clipboard content looks like valid pilot input.

Clipboard guardrails reject likely non-local content, including:

- code
- stack traces
- shell output
- XML/XAML
- markdown
- logs
- filesystem paths
- oversized text
- obviously invalid pilot-name candidates

This protects PMG from trying to resolve unrelated clipboard content.

---

## Pilot Parsing

After clipboard content passes guardrails, PMG extracts plausible pilot names.

Parsing is intentionally conservative. The goal is to avoid turning unrelated text into lookup work.

PMG also removes duplicate candidate names case-insensitively.

---

## Entity Resolution

PMG resolves pilots, corporations, and alliances through available public data sources.

Resolved IDs allow PMG to:

- open zKill links
- cache public context
- apply watchlist/ignore behavior
- enrich visible board rows
- run targeted freshness checks

Resolution can fail or be delayed if a public service is unavailable or if the input does not match known public entities.

---

## Local Data Model

PMG stores public-intel-derived data locally.

Local storage supports:

- faster repeated board population
- historical baseline intel
- recent public ship observations
- cyno/tackle/bait-derived context
- freshness repair
- diagnostics

The local database is a cache and evidence store, not a source of absolute truth.

---

## Public Intel Sources

PMG uses public data sources, including public zKill/ESI-style workflows.

PMG does not use private ESI character scopes.

PMG does not require character login.

PMG does not read EVE client memory or inspect network traffic.

---

## Archive Backfill

Archive Backfill is PMG’s historical baseline.

It imports completed public archive days into local derived tables.

Archive Backfill answers:

```text
What historical public intel should PMG already know for this configured history window?
```

Archive Backfill is the complete-day authority. Other freshness layers do not mark archive days complete.

---

## R2Z2 Live Feed

R2Z2 is an optional live public killmail feed.

When enabled, PMG can ingest live zKill-known killmail deltas.

R2Z2 helps bridge the gap between daily archives and current public activity. It is disabled by default.

R2Z2 does not guarantee complete same-day coverage. It only improves PMG’s local view for killmails PMG receives and processes from the stream.

---

## Today’s Freshness

Today’s Freshness is a targeted visible-pilot repair flow.

It checks recent public zKill-known activity for pilots currently visible in the Grill.

Flow:

```text
visible pilots
→ targeted zKill character queries
→ missing killmail IDs
→ ESI full killmail fetch
→ incremental local import
→ derived intel update
→ Grill/Analysis refresh
```

This repairs gaps where a visible pilot has newer public activity that PMG has not ingested yet.

---

## Historical Freshness

Historical Freshness repairs recent completed-day data for visible pilots.

It exists because a completed archive day can become stale after import if a killmail is posted late.

Flow:

```text
visible pilots
→ recent completed-day target window
→ targeted zKill summaries
→ killmail ID comparison
→ ESI full killmail fetch when needed
→ exact killmail_time day filtering
→ incremental import
→ checkpoint/source tracking
```

Historical Freshness does not replace Archive Backfill.

It does not mark archive days complete.

---

## Background Historical Repair

Background Historical Repair is a bounded startup enrichment pass.

It starts after the UI is usable, waits a configured delay, and checks a bounded local pool of known/recent pilots.

It is designed to be:

- one-shot
- delayed
- bounded
- cancellable
- cooldown-protected
- rate-limit aware
- non-blocking

The candidate pool is local-data-derived. PMG does not globally crawl zKill.

---

## Incremental Import

Freshness repair and live feed ingestion use incremental import.

Incremental import dedupes by killmail ID and updates derived local tables without replacing whole archive days.

This avoids double-counting and keeps freshness repair separate from archive-day completeness.

---

## Write Coordination

PMG has multiple possible writers to the local killmail database:

- archive backfill
- R2Z2
- Today’s Freshness
- Historical Freshness
- Background Historical Repair
- reset/reseed workflows

These paths use a shared write coordination approach to reduce SQLite contention and avoid reset/write overlap.

---

## Analysis and Grill Display

The Grill shows enriched pilot rows.

Analysis summarizes the visible set.

Displayed values may include:

- public kill/loss context
- average fleet-size context
- last public ship seen
- relative last-seen timing
- corporation/alliance context
- cyno/tackle/bait signals
- watchlist state
- ignore filtering

Display values are based on what PMG knows locally. They are evidence summaries, not current certainty.

### Board Mode Discoverability and Compact Display

PMG includes a dense **Board Mode** intended for live play.

- `Insert` toggles Board Mode on or off.
- When Board Mode is enabled, PMG shows a brief non-blocking in-app hint reminding the user to press `Insert` again to return to normal mode.
- The hint is temporary and is intended to avoid interrupting clipboard-driven board use.

Board display density is configurable without changing the rest of the app layout.

Board-only display options include:

- showing or hiding Grill grid lines
- changing Grill board text size within bounded safe values
- changing the Grill board font family from the supported in-app options

These settings apply to the Grill/Board presentation only. They do not retheme or compress Analysis, Intel, Ignore List, or unrelated tabs.

### Board Mode Minimum Height

Board Mode can be resized much smaller than the normal PMG layout.

The intent is to allow users to shrink PMG down toward a one-row live board if they want, while still keeping normal mode at a safer minimum size.

The minimum Board Mode height is based on the current compact layout and board presentation rather than a fixed per-screen assumption. That means the practical minimum can vary slightly with the current board text size and board row/header sizing.

### Keyboard Shortcuts and Window Recovery

PMG documents its user-facing shortcuts in the Help tab.

Important shortcut behaviors include:

- `Insert` for Board Mode
- `Delete` to clear the current Grill board
- `Home` to reprocess the clipboard
- `Ctrl+Home` to recover/reset the PMG window position
- `Esc` three times quickly to exit PMG

`Ctrl+Home` is intended as a window recovery shortcut, not a normal workflow command. Depending on current focus and Windows routing behavior, users may need to click the PMG window background, title/header area, or another non-editing part of the window first before retrying the shortcut.

---

## Last Seen

`Last Seen` uses relative display formatting.

Examples:

```text
12m ago
3h ago
3d ago
```

This is easier to scan than raw timestamps.

Sorting should use the underlying timestamp, not the display text.

---

## Ignore and Watchlist

Ignore lists reduce noise by hiding or filtering pilots, corporations, and alliances.

Watchlist marks entities the user wants to track more closely.

These are user judgment tools. They do not automate decisions.

---

## Diagnostics

Diagnostics bundle selected state and logs so PMG issues can be investigated.

Diagnostics can include:

- app/runtime information
- settings summaries
- freshness status
- live feed status
- checkpoint counts
- source counts
- recent app logs

Diagnostics should not include raw killmail JSON, secrets, unrelated local files, or private credentials.

---

## Error and Rate-Limit Behavior

PMG treats public providers as fallible.

Expected conditions include:

- network timeouts
- remote connection resets
- rate limits
- missing archive days
- not-yet-published archive days
- unavailable public endpoints

PMG should surface these states without crashing and retry or defer where appropriate.

---

## Data Boundaries

PMG works from public and user-provided evidence.

PMG cannot prove:

- current ship
- current grid position
- cloak status
- fleet intent
- private standings
- hidden alts
- complete public history

PMG can only summarize evidence it has.

---

## Design Principle

PMG is an awareness tool.

It is intended to help players ask better questions faster:

```text
Who is here?
What public evidence matters?
What is stale or unknown?
Where should I manually verify?
```

PMG supports judgment. It does not replace it.

---

## Related Docs

- [`PMG-FEATURES.md`](./PMG-FEATURES.md)
- [`FIRST-TIME-USE.md`](./FIRST-TIME-USE.md)
- [`EVE-TOS-COMPLIANCE.md`](./EVE-TOS-COMPLIANCE.md)
- [`DEVELOPER-NOTES.md`](./DEVELOPER-NOTES.md)
