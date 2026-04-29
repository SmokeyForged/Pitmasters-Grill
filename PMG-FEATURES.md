# Pitmaster's Grill - Current Feature Snapshot

This document separates the latest tagged release from the current main branch.

- Latest tagged release: **v0.9.5.1**
- Current main branch: includes **unreleased 0.9.6 candidate work**

Unless a section explicitly says otherwise, release-specific statements below refer to the current main branch feature snapshot, not a released build.

---

## Release posture

`v0.9.5` was the larger community-feedback foundation release.

`v0.9.5.1` is the latest tagged hotfix on top of that release.

Current main branch includes additional **0.9.6 candidate** quality-of-life work that is planned for the next technical preview but is not yet tagged as released.

PMG remains a technical preview in the `0.9.x` stabilization period, not a `1.0` release.

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

The board population path resolves or derives:

- pilot identity
- corporation
- alliance
- kill/loss counts
- average fleet-size context
- recent public ship observations
- cyno-capable hull observations
- derived cyno/tackle/bait evidence

Resolution is cached locally where possible to improve repeat performance.

Current testing feedback indicates PMG handles large local-list style inputs well, but that should still be treated as a practical test result rather than a hard performance guarantee.

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
- Right click opens the PMG pilot detail sidecar.
- Double-click opens zKill for that pilot.
- Board-level note/flag icons open pilot notes.

Note/flag icon clicks are separate from row zKill and compact-drag behavior.

---

## Pilot Watchlist

Current main branch includes an **unreleased 0.9.6 candidate** watchlist system.

Watchlist is a local, manual attention marker. It is **not** a threat signal and does not change cyno, bait, tackle, or row-evidence semantics.

Current behavior:

- Watch/Unwatch is available from the pilot detail sidecar.
- Watched pilots show a star marker to the **left** of the pilot name.
- The existing notes flag remains on the **right**.
- Watched pilots stay pinned above non-watched pilots.
- Column sorting preserves the watched-first partition and sorts within watched/non-watched groups.
- Watch state is local app state keyed by pilot ID where available.

This feature is current-main-branch-only and should not be read as part of `v0.9.5.1`.

---

## Compact Mode and Panel Mode

`v0.9.5` redesigned compact mode as a true board-first operational view, and `v0.9.5.1` hardened the shell behavior around that view.

Compact/panel behavior aims to:

- keep the board as the primary surface
- preserve row colors and Sig icons
- preserve board resizing behavior
- preserve always-on-top behavior
- preserve configured transparency behavior in panel mode
- avoid compact-toggle freezes or unstable native shell transitions
- support delayed click-and-hold drag from rows, column headers, or blank board space
- keep row selection, right-click details, and double-click zKill behavior intact

Current main branch also includes **unreleased 0.9.6 candidate** UI-state persistence for compact/panel workflows:

- PMG remembers main window position and size
- PMG restores compact mode state across restart
- PMG preserves panel-mode startup behavior across restart
- PMG restores sane placement on multi-monitor setups and clamps only when saved bounds are no longer visible

App-local hotkeys:

- **Insert** - toggle compact/normal mode
- **Delete** - clear board
- **Home** - refresh/reprocess clipboard intel
- **Esc three times** - exit PMG

These hotkeys are app-local and are intended for PMG operation, not gameplay automation.

---

## Pilot Detail Sidecar

Pilot details open as a compact sidecar inspector instead of a large board overlay.

The sidecar:

- opens beside the board when possible
- follows the configured detail placement preference
- clamps or flips placement near monitor edges when needed
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
- Watch / Unwatch on current main branch

Notes are handled through the board-level note/flag icon rather than a bulky inline detail editor.

---

## Notes, Manual Overrides, and Ignore State

PMG intentionally keeps user-owned judgment separate from public-data-backed evidence.

Current manual or local state can include:

- pilot notes
- Watchlist state on current main branch
- Known-Cyno override
- Bait override
- typed ignores

`v0.9.5.1` improved saved-note flag visibility so board-level note state is easier to spot while scanning.

Manual Bait remains a user-controlled signal and is not the same thing as derived industrial-cyno bait evidence.

Ignored rows are suppressed from the current board. Watchlist only affects visible rows and sort priority; it does not bypass ignores.

---

## Local Composition Summary Banner

Current main branch includes an **unreleased 0.9.6 candidate** summary banner at the bottom of the board.

It provides a fast, one-line read of the currently visible board composition after filtering. Depending on the visible rows, it can surface:

- visible pilot count
- watched count
- possible cyno count
- confirmed cyno count
- bait count
- top visible corp or alliance concentration

It is primarily useful in normal mode where there is room to keep a lightweight board summary visible without crowding the main grid.

---

## Board Column Layout Save and Reset

Current main branch includes **unreleased 0.9.6 candidate** board layout persistence.

Users can now:

- save board column order
- save board column widths
- restore that layout on startup
- reset the layout back to defaults

This is separate from column visibility settings. The feature is intended as a cockpit quality-of-life improvement for users who tune PMG to specific monitor widths or compact layouts.

---

## Hover Explanation

Current main branch includes **unreleased 0.9.6 candidate** concise hover explanations for row/signal reasoning.

These tooltips are intended to:

- explain why a row is colored or flagged
- summarize the signal/evidence briefly
- stay concise and low-noise
- help scanning without replacing the full detail pane

This is an explanation aid, not a replacement for the evidence-first sidecar or zKill follow-up.

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

`v0.9.5` expanded and corrected the cyno-capable hull catalog, including missing covert/industrial-capable hulls, and fixed **Cyno Hull Seen** semantics.

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

Run **Rebuild Killmail Derived Intel** after derived-intel schema changes, derived-evidence backfills, or when you need to rebuild existing local archive data into the current derived tables.

That rebuild is **not required solely because of the `v0.9.5.1` hotfix**, and none of the current `0.9.6` candidate UI/manual-state features are expected to require it by themselves.

---

## Killmail Update and Proton Compatibility

PMG downloads and extracts public killmail archive data for local processing when configured.

`v0.9.5` improved Linux/Proton compatibility by removing the fragile dependency on launching external Windows `tar.exe` for archive extraction. Archive extraction now uses managed .NET library support already packaged with PMG.

This avoids assuming that `tar.exe` exists in System32 or in a Proton/Wine prefix.

Current tester feedback indicates the Windows build works under Proton. Visual polish may not fully match native Windows, and a Linux-native release/polish pass remains deferred.

---

## Settings and Configuration

Current settings/configuration areas include:

- themes and visual appearance
- opacity-aware backgrounds
- compact mode
- always-on-top behavior
- board column visibility
- board column layout save/reset on current main branch
- corp/alliance count visibility
- typed ignore list
- diagnostics export
- cache maintenance
- killmail intel status/configuration
- detail sidecar placement preference
- killmail derived-intel rebuild
- color-blind signal palettes

Color-blind mode visibly changes board signal colors. It is a real accessibility setting, but deeper review from a color-blind tester is still pending.

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
- compact signals and hover explanations are intentionally concise and may still require opening details or zKill for context
- cyno/tackle/bait indicators are based on public evidence and conservative inference, not live visibility
- Proton compatibility is promising through the Windows build, but native Linux polish is still deferred
- accessibility palettes are useful now, but broader user validation is still welcome

The current goal is practical usefulness, not exhaustive automation.
