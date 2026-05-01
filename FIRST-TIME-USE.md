# First-Time Use

This guide walks through the first launch of Pitmaster's Grill, or PMG.

PMG is a Windows desktop intel companion for EVE Online. It turns copied EVE local lists into a readable public-intel board. PMG uses public data, local caching, and user-triggered freshness tools to help you review local faster.

---

## Before You Start

You need:

- Windows
- the latest PMG release ZIP
- internet access for public EVE/zKill/ESI lookups
- an EVE local list to copy

PMG does not require private ESI login, character authorization, or EVE client integration.

PMG does not read EVE client memory, inspect network traffic, automate gameplay, or use private ESI scopes.

---

## Download PMG

Download the latest release from:

```text
https://github.com/SmokeyForged/Pitmasters-Grill/releases/latest
```

Use the release asset provided on the GitHub release page.

---

## Extract PMG

Extract the release ZIP to a folder you control.

Good examples:

```text
C:\Users\YourName\Apps\PitmastersGrill
D:\Games\Tools\PitmastersGrill
```

Avoid running PMG directly from inside the ZIP file.

---

## Launch PMG

Run:

```text
PitmastersGrill.exe
```

On first launch, PMG may create local app data and prepare its local public-intel cache.

If Windows SmartScreen appears, review the prompt and choose the option appropriate for your own trust decision.

---

## First Local Copy

The normal workflow is:

1. Open EVE.
2. Select or copy the visible local pilot list.
3. Copy it to the clipboard.
4. PMG detects the copied pilot list.
5. PMG populates the Grill board.

PMG expects copied text that resembles an EVE local list. It has guardrails to reject unrelated clipboard content such as code, logs, XML/XAML, shell output, markdown, oversized text, and other non-local input.

---

## The Grill Board

The Grill is the main pilot board.

It can show:

- pilot name
- corporation
- alliance
- public kill/loss context
- average fleet-size context
- last public ship seen
- relative last-seen timing
- cyno hull observations
- signal markers
- watchlist treatment

Useful actions:

- Press `Insert` to toggle Board Mode on or off.
- If you enter Board Mode and want the normal layout back, press `Insert` again.
- right-click a row for details
- double-click a row to open zKill
- resize columns
- hide optional columns from Settings
- use Settings -> Board Columns to hide Grill grid lines, reduce board text size, or switch the board font
- reset/recover the window with `Ctrl+Home`

---

## Analysis

The Analysis tab summarizes the current visible board.

Use it when you have time to look beyond individual pilots.

It can help answer questions like:

- how many pilots are visible
- how many unique corps/alliances are present
- which alliances are most represented
- whether confirmed cyno/tackle/bait signals are present
- what PMG knows about current EVE session context

---

## Intel Status

The Intel tab shows PMG’s local public-intel state.

You may see information about:

- requested history window
- local archive coverage
- latest completed/published archive day
- missing or not-yet-published archive days
- live R2Z2 state
- Today’s Freshness
- Historical Freshness
- Background Historical Repair
- diagnostics export

If PMG says an archive day is not published yet, that usually means PMG reached the latest public archive available and will retry later.

---

## Freshness Tools

PMG has several public-intel freshness layers.

### Archive Backfill

Archive Backfill builds a historical baseline from completed public zKill archive days.

On first use or after changing history settings, PMG may download/import missing archive days.

### Today’s Freshness

Today’s Freshness checks currently visible pilots for recent public zKill-known activity.

Use it when a pilot is visible and you want PMG to repair same-day/recent data.

### Historical Freshness

Historical Freshness checks currently visible pilots across recent completed days.

Use it when public killmails may have been posted after PMG first imported an archive day.

### Background Historical Repair

Background Historical Repair runs after startup and checks a bounded local pool of known/recent pilots. It respects cooldowns and rate limits.

### R2Z2 Live Feed

R2Z2 is optional and disabled by default.

When enabled, it can ingest live zKill-known killmail deltas. It improves freshness, but it does not guarantee complete same-day coverage.

---

## Ignore List

The Ignore List tab lets you manage ignored:

- pilots
- corporations
- alliances

Ignored entries can be hidden from the active board to reduce noise.

---

## Settings

Settings include app behavior and visual options such as:

- PMG Themes
- column visibility
- board layout reset
- background historical repair
- R2Z2 live feed
- cache/history controls

Panel-style behavior is the default PMG experience.

---

## Help Tab

The Help tab lists common PMG shortcuts and interactions.

Use it as a quick in-app reference.

---

## Diagnostics

If something breaks, export diagnostics from the Intel tab.

Diagnostics help with troubleshooting by collecting app state, selected settings, freshness summaries, and logs.

Before sharing diagnostics publicly, review the contents. Do not post private credentials, secrets, or unrelated local files.

---

## Common First-Run Questions

### Why is PMG downloading/importing data?

PMG uses local public-intel caching. It may need to build a baseline from public archive data.

### Why does a pilot not show the newest ship I saw on zKill?

Public data may not have been imported locally yet. Try Today’s Freshness for visible pilots.

### Why does Historical Freshness find missing killmails after an archive day was already imported?

Some killmails are posted late. A completed archive day can become stale after PMG first imported it.

### Does PMG know what ship someone is currently flying?

No. PMG shows public evidence such as last public ship observation. That does not prove current ship.

### Does PMG read my EVE client?

No. PMG is clipboard/public-data driven and does not read EVE client memory or automate gameplay.

---

## Good First Smoke Test

After installing PMG:

1. Launch PMG.
2. Copy a local list.
3. Confirm Grill populates.
4. Open Analysis.
5. Open Intel.
6. Run Today’s Freshness.
7. Export diagnostics.
8. Close and reopen PMG.
9. Confirm layout and settings persist.

---

## Next Docs

Continue with:

- [`PMG-FEATURES.md`](./PMG-FEATURES.md)
- [`HOW-IT-WORKS.md`](./HOW-IT-WORKS.md)
- [`EVE-TOS-COMPLIANCE.md`](./EVE-TOS-COMPLIANCE.md)
- [`HOW-TO-NAVIGATE-THIS-REPO.md`](./HOW-TO-NAVIGATE-THIS-REPO.md)
