# Pitmasters Grill Technical Preview v0.9.6

**DRAFT - unreleased**

This document is a draft for the next technical preview. It describes current main-branch `0.9.6` candidate work and must not be treated as a released patch note until a real release/tagging pass happens.

## Release theme

**Watchlist and cockpit QoL pass**

This candidate release focuses on quality-of-life improvements around pilot attention management, board readability, persistent cockpit layout, and lighter-weight explanation surfaces.

## Candidate features

### Pilot Watchlist

- Added Watch/Unwatch from the pilot detail sidecar.
- Added a watched star marker to the left of the pilot name.
- Kept the notes icon on the right side of the pilot row.
- Watched pilots stay pinned above non-watched pilots.
- Board column sorts preserve watched-first ordering by sorting within watched and non-watched groups.

Watchlist is local/manual attention state. It is not a threat signal and does not change cyno, bait, tackle, or killmail evidence semantics.

### Persistent window position, size, and mode

- PMG now remembers main window position and size.
- Compact-mode state persists across restart.
- Panel-mode startup behavior persists across restart.
- Multi-monitor restore behavior has been validated, including sane restore on monitors with non-primary placement.

### Local composition summary banner

- Added a bottom-board summary banner for quick visible-board composition reads.
- Summary is based on currently visible rows after filtering.
- Intended as a fast operational read, especially in normal mode.

### Board column layout save/reset

- Added board column layout save for user-tuned order and width.
- Added reset-to-default behavior for board layout.
- Layout persistence is separate from column visibility.

### Hover explanation

- Added concise hover explanations for row/signal reasoning.
- Tooltips provide a short signal/evidence explanation without replacing the full detail sidecar.

## Upgrade notes

- No killmail-derived intel rebuild is expected solely because of these UI/manual-state features.
- Rebuild guidance can still change in the future if later schema or derived-evidence changes land before release.

## Validation checklist

- Build PMG successfully.
- Confirm watchlist persists and watched-first sorting survives board-column sorts.
- Confirm main window position/size restore correctly, including on a two-monitor setup.
- Confirm compact/panel mode persists across restart.
- Confirm summary banner updates with visible-row changes.
- Confirm board column layout save/reset works.
- Confirm hover explanations remain concise and do not interfere with row interactions.
- Confirm notes, right-click detail, double-click zKill, and ignore behavior still work.

## Release-status reminder

This file is intentionally a **draft**. Do not treat `v0.9.6` as released until the repo is tagged and the release notes are finalized.
