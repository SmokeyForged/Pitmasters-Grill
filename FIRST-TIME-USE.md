# First-Time Use

Welcome to **Pitmasters Grill (PMG)**.

This guide is for first-time setup and first-time use of PMG. The latest tagged release is **v0.9.5.1**, but this document also calls out **current main / 0.9.6 candidate** behavior where that is helpful for new users.

## 1. Download the latest release build

Go to the repository's **Releases** page and download the newest tagged PMG build.

After downloading:

1. Extract the ZIP to its own folder.
2. Open the extracted folder.
3. Run `PitmastersGrill.exe`.

Keep all extracted files together in the same folder. Do not run the app directly from inside the ZIP.

---

## 2. First launch

On first launch, PMG may need a moment to initialize its local app data, cache files, and local databases.

Once open, the main goal is simple:

- bring in a pilot list
- let PMG resolve supporting intel
- use the board and sidecar details to decide who deserves attention

PMG is built to help you move from raw local data to usable context without replacing player judgment.

---

## 3. Automatic killmail update behavior

PMG includes an automatic update step for the most recent completed day of public killmail data.

Why this matters:

- it helps keep recent activity context current
- it reduces the need to manually rebuild from scratch every time
- it gives PMG a fresher baseline before you begin active use

In normal use, let this happen in the background as part of startup behavior.

---

## 4. Broader history pull

PMG also includes a larger killmail pull path for users who want a broader recent baseline than the normal startup refresh provides.

Good times to use it:

- first-time setup on a new install
- after a long gap between uses
- when you want a stronger recent activity baseline
- when you think your local intel history needs a manual catch-up pass

Think of it as a stronger recent-history refresh, not something you need constantly during normal use.

---

## 5. Main board controls

The main board is the primary scanning surface.

Current row behavior:

- Left click selects a row.
- Right click opens the PMG pilot detail sidecar.
- Double-click opens zKill for that pilot.
- The note/flag icon opens pilot notes for that row.

Those actions remain separate from each other, so opening notes should not trigger zKill, and row actions should still behave normally in compact mode.

---

## 6. Using the detail sidecar effectively

PMG detail opens as a compact sidecar inspector rather than taking over the whole board.

Use it like this:

- scan the board first for names, corps, alliances, and signal states that stand out
- right-click the row that matters
- use the sidecar as a confirmation layer before deciding whether to escalate or dig deeper

Placement behavior:

- PMG opens the sidecar beside the board when there is room
- PMG follows the saved left/right preference when possible
- near monitor edges, PMG flips or clamps the sidecar so it stays on-screen

---

## 7. Watchlist on current main

Current main branch includes an **unreleased 0.9.6 candidate** watchlist feature.

Practical behavior:

- use **Watch** or **Unwatch** from the pilot detail sidecar
- watched pilots show a star to the left of the pilot name
- the notes flag remains on the right
- watched pilots stay pinned above non-watched pilots
- board column sorts still preserve watched-first grouping

Watchlist is local/manual attention state. It is not a threat color or evidence signal.

---

## 8. Compact and panel mode

Compact mode is the board-first operational view. Panel mode keeps PMG in a slimmer custom-shell style for users who want it to behave more like a lightweight companion window.

App-local hotkeys:

- `Insert` toggles compact/normal mode
- `Delete` clears the board
- `Home` refreshes or reprocesses clipboard intel
- `Esc` three times exits PMG

Current main branch also includes **unreleased 0.9.6 candidate** persistence for:

- main window position
- main window size
- compact-mode state across restart
- panel-mode startup behavior across restart

Multi-monitor restore has been validated in operator testing, with off-screen clamping only when saved bounds are no longer visible.

---

## 9. Summary banner and hover explanation

Current main branch includes two small **0.9.6 candidate** board-readability features:

- a bottom-board summary banner for visible composition
- concise hover explanations for row/signal reasoning

The summary banner gives a quick read of the currently visible board after filtering.

Hover explanations are meant to explain a color or flag briefly. They are useful for fast scanning, but they are not a replacement for the full detail sidecar.

---

## 10. Notes and manual overrides

PMG supports pilot notes and a small set of manual overrides.

Current expectations:

- use the board note/flag icon to open pilot notes
- treat notes as human judgment, not public-evidence automation
- treat Known-Cyno and Bait overrides as manual operator context, separate from derived evidence

The saved-note flag is intended to remain easy to spot while scanning.

---

## 11. Column layout save and reset

Current main branch includes **unreleased 0.9.6 candidate** board layout save/reset controls.

Use them when you want PMG to remember:

- column order
- column width

Reset returns the board layout to defaults. This is separate from the existing column visibility settings.

---

## 12. Rebuild Killmail Derived Intel

Use **Rebuild Killmail Derived Intel** when you need to rebuild derived tables from existing local killmail archive data.

Typical reasons include:

- derived-intel schema changes
- derived-evidence backfills
- rebuilding existing local data into a newer derived format

Important guidance:

- a rebuild is **not required solely because of `v0.9.5.1`**
- a rebuild is also **not expected solely because of the current `0.9.6` candidate UI/manual-state features**

---

## 13. Proton note

Current tester feedback indicates the Windows PMG build works under Proton.

Practical expectations:

- functional compatibility looks promising
- visual polish may not fully match native Windows
- a Linux-native release and Linux-native polish pass are still deferred

If you are using Proton, treat current support as validated enough to try, but still technical-preview quality rather than polished native Linux packaging.

---

## 14. Suggested first-use workflow

If you are opening PMG for the first time, this is a good starting pattern:

1. Download the latest tagged release.
2. Extract the ZIP fully.
3. Run `PitmastersGrill.exe`.
4. Let startup complete.
5. Allow the automatic recent killmail refresh to do its work.
6. Run the broader recent-history pull if you want a fuller baseline.
7. Paste or import your pilot list.
8. Scan the main board first.
9. Right-click only the pilots that actually stand out.
10. Use notes, watchlist, and deeper source checks selectively.

---

## 15. Good habits

A few simple habits make PMG more useful:

- keep the app in its extracted folder
- let startup/update work finish before judging first-run behavior
- use rebuild tools only when there is a real reason
- use the board for triage and the sidecar for confirmation
- treat PMG as decision support, not decision replacement

---

## 16. If something looks off

If PMG behaves strangely on first use:

- close the app and relaunch it
- make sure you extracted the full ZIP before running
- make sure you are launching the correct executable
- note what step you were on when the issue happened
- report what you saw as specifically as possible

Useful bug reports are things like:

- what you clicked
- what you expected
- what actually happened
- whether it was first launch or later use
- whether the issue happened during startup, refresh, compact-mode switching, or pilot review

---

## 17. Final note

PMG is still in technical preview, but the goal is already clear:

take the noise of local, reduce the friction, and help you get to the useful part faster.
