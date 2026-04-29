# Pitmaster's Grill - Current Feature Snapshot

Public snapshot for **Pitmasters Grill Technical Preview v0.9.5**.

This document describes what PMG can do in the current public release. It is not a roadmap and does not describe planned features as if they already exist.

Release: **Pitmasters Grill 0.9.5**  
Tag: **v0.9.5**  
Release commit: **a3fa467**

---

## Purpose

PMG turns a copied EVE local pilot list into a quick, readable intel board.

The core question is:

**Who is here, and what matters right now?**

PMG is a community technical-preview tool. It uses copied/local user input and public/cached data. It does not automate gameplay or claim access to private/live EVE state.

---

## Local List Processing

PMG can process copied local-style pilot lists and populate a board of pilot rows.

Current guardrails are designed to reject obvious non-local clipboard content such as code, markup, stack traces, shell output, logs, paths, and oversized unrelated text while still accepting realistic EVE pilot names.

The board population path resolves available:

- pilot identity
- corporation
- alliance
- kill/loss counts
- average fleet-size context
- recent public ship observations
- cyno-capable hull observations
- derived cyno/tackle/bait evidence

Resolution is cached locally where possible to improve repeat performance.

---

## Board View

The main board is built for scanning under pressure.

Current board data can include:

- Character
- Sig icon / signal state
- Alliance
- Corp
- Kills
- Losses
- Avg Fleet Size
- Last Ship Seen
- Last Seen
- Cyno Hull Seen

Optional columns can be configured in settings.

### Board interactions

- Left click selects a row.
- Right click opens PMG pilot details.
- Double-click opens zKill for that pilot.
- Board-level note/flag icons open pilot notes.

Note/flag icon clicks are separate from row zKill and compact-drag behavior.

---

## Compact Mode

v0.9.5 redesigned compact mode as a true board-first operational view.

Compact mode:

- keeps the board as the primary surface
- preserves row colors and Sig icons
- preserves board resizing behavior
- preserves always-on-top behavior
- supports delayed click-and-hold drag from rows, column headers, or blank board space
- keeps row selection, right-click details, and double-click zKill behavior intact

App-local hotkeys:

- **Insert** - toggle compact/normal mode
- **Delete** - clear board
- **Home** - refresh/reprocess clipboard intel
- **Esc three times** - exit PMG

These hotkeys are app-local and are intended for PMG operation, not gameplay automation.

---

## Pilot Detail Sidecar

Pilot details now open as a compact sidecar inspector instead of a large board overlay.

The sidecar:

- opens beside the board when possible
- follows the configured detail placement preference
- inherits PMG theme and opacity
- uses compact, evidence-first wording
- keeps the board visible and usable

Detail placement preference is available under Intel configuration.

Current detail content can include:

- pilot summary
- corp/alliance summary
- recent public kill/loss context
- compact cyno signal
- compact bait signal
- compact evidence lines
- limitations/freshness/source text
- ignore actions
- Open zKill

Notes were removed from the detail pane and moved to board-level note/flag icons.

---

## Notes and Manual Overrides

PMG supports pilot notes through board-level note/flag icons.

Manual flags/overrides remain separate from derived public evidence. This keeps user judgment distinct from data PMG extracted from public killmail/cache sources.

Current manual state can include:

- pilot notes
- Known-Cyno override
- Bait override

Manual Bait remains a user-controlled signal and is not the same thing as derived industrial-cyno bait evidence.

---

## Ignore List

PMG supports typed ignore entries.

Ignore entries can target:

- Pilot IDs
- Corporation IDs
- Alliance IDs

Ignore matching is ID-based. Resolved names are display/help text and do not control suppression.

Ignore precedence:

1. Pilot
2. Corporation
3. Alliance

Ignored rows are suppressed from the current board. Corp/alliance board counts are calculated after ignore filtering.

---

## Corp and Alliance Counts

PMG can optionally show visible-board concentration counts beside corporation and alliance names.

Example:

- `Corp Name [3]`
- `Alliance Name [8]`

Count behavior:

- disabled by default
- based only on currently visible board rows
- applied after ignore filtering
- prefers corporation/alliance IDs where available
- falls back to normalized exact names when IDs are unavailable
- hides solo `[1]` counts
- does not add counts to the pilot detail sidecar

---

## Cyno Hull Detection

PMG tracks public killmail-derived ship observations and surfaces recent cyno-capable hull context.

v0.9.5 expanded and corrected the cyno-capable hull catalog, including missing covert/industrial-capable hulls, and fixed **Cyno Hull Seen** semantics.

Current rule:

**Cyno Hull Seen shows the newest public killmail-derived ship observation for that pilot where the ship is cyno-capable.**

It does not rely on dictionary order, first match, hull priority, or async completion order.

Supported cyno evidence wording is compact:

- `hard` = Cynosural Field Generator I
- `covert` = Covert Cynosural Field Generator I
- `indi` = Industrial Cynosural Field Generator

---

## Cyno, Tackle, and Bait Evidence

PMG scans public victim killmail item lists where available.

Current recognized cyno module families:

- Cynosural Field Generator I
- Covert Cynosural Field Generator I
- Industrial Cynosural Field Generator

Current recognized tackle module families:

- Warp Scrambler variants
- Warp Disruptor variants

Evidence rules:

- Confirmed cyno module evidence comes from public victim/loss item lists.
- PMG does not infer fitted modules from attacker-only appearances.
- Industrial cyno + tackle on the same public victim loss supports derived bait evidence.
- Broader cyno-capable hull + tackle evidence can surface as a compact tackle marker.
- Cyno-capable hull + tackle alone does not automatically make every pilot bait.

Example compact evidence:

- `Evidence: 26/04/25 - indi + Warp Scrambler II`
- `Evidence: 26/04/25 - covert`
- `Tackle: 26/04/27 - Loki + Warp Disruptor II`

Public evidence can be incomplete or delayed, so these are operational indicators rather than live fit visibility.

---

## Killmail-Derived Intel

PMG maintains local killmail-derived intel tables for public-data-backed board context.

Current derived intel can include:

- pilot registry observations
- fleet-size observations
- latest ship observations
- latest cyno-capable hull observations
- confirmed cyno module observations
- industrial-cyno bait observations
- cyno-capable hull tackle observations

Users with existing killmail data should run **Rebuild Killmail Derived Intel** after updating to v0.9.5. This backfills derived evidence from locally available killmail archives without clearing notes, themes, ignore entries, settings, or manual overrides.

---

## Killmail Update and Proton Compatibility

PMG downloads and extracts public killmail archive data for local processing when configured.

v0.9.5 improved Linux/Proton compatibility by removing the fragile dependency on launching external Windows `tar.exe` for archive extraction. Archive extraction now uses managed .NET library support already packaged with PMG.

This avoids assuming that `tar.exe` exists in System32 or in a Proton/Wine prefix.

---

## Settings and Configuration

Current settings/configuration areas include:

- themes and visual appearance
- opacity-aware backgrounds
- compact mode
- always-on-top behavior
- board column visibility
- corp/alliance count visibility
- typed ignore list
- diagnostics export
- cache maintenance
- killmail intel status/configuration
- detail sidecar placement preference
- killmail derived-intel rebuild

Settings are local to the app.

---

## Diagnostics

PMG includes diagnostics export for troubleshooting.

Diagnostics are intended to summarize useful app state without dumping unrelated private files. They can include app settings summaries, provider/cache status, logs, performance timing summaries, ignore summaries, and cyno/bait signal summaries.

Do not share diagnostics publicly if you have not reviewed them and are not comfortable with the contents.

---

## Safety and EVE Compliance Framing

PMG is designed as a local companion tool around copied lists and public data.

PMG does not:

- read EVE client memory
- inspect network traffic
- automate input
- use EVE SSO
- read private ESI location
- claim current grid presence
- claim cloak state
- claim live movement tracking

The app helps summarize evidence. It does not replace player judgment.

---

## Current Limitations

PMG is still a technical preview.

Known expectations:

- public killmail evidence can be delayed or incomplete
- provider lookups can fail, throttle, or return partial data
- cache rebuilds may be needed after derived-intel schema changes
- compact signals are intentionally concise and may require opening details/zKill for context
- cyno/tackle/bait indicators are based on public evidence and conservative inference, not live visibility

The current goal is practical usefulness, not exhaustive automation.
