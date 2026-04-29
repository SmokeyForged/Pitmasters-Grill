# First-Time Use

Welcome to **Pitmasters Grill (PMG)**.

This guide is for first-time setup and first-time use of the current released build, **v0.9.5.1**. It covers startup, the main intel loop, compact/panel behavior, and a few current release notes that are useful to know before you settle into normal use.

## 1. Download the latest release build

Go to the repository's **Releases** page and download the newest PMG build.

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

A good first-time pattern is:

1. launch PMG
2. let normal startup work settle
3. run the broader recent-history pull if you want fuller baseline context
4. begin using local intel normally

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

PMG detail now opens as a compact sidecar inspector rather than taking over the whole board.

Use it like this:

- scan the board first for names, corps, alliances, and signal states that stand out
- right-click the row that matters
- use the sidecar as a confirmation layer before deciding whether to escalate or dig deeper

The sidecar is designed to stay near the board so you can keep the main intel surface visible while reading the selected pilot.

Placement behavior:

- PMG opens the sidecar beside the board when there is room
- PMG follows the saved left/right preference when possible
- near monitor edges, PMG flips or clamps the sidecar so it stays on-screen

That monitor-edge behavior was tightened in the `v0.9.5.1` hotfix.

---

## 7. Compact and panel mode

Compact mode is the board-first operational view. Panel mode keeps PMG in a slimmer custom-shell style for users who want it to behave more like a lightweight companion window.

Current behavior in `v0.9.5.1`:

- compact enter/exit should remain stable
- panel/custom shell behavior is more reliable than earlier builds
- configured transparency should still be preserved in panel mode
- row selection, right-click sidecar, double-click zKill, and note access should continue to work

App-local hotkeys:

- `Insert` toggles compact/normal mode
- `Delete` clears the board
- `Home` refreshes or reprocesses clipboard intel
- `Esc` three times exits PMG

These hotkeys are app-local PMG controls. They are not gameplay automation.

---

## 8. Notes and manual overrides

PMG supports pilot notes and a small set of manual overrides.

Current expectations:

- use the board note/flag icon to open pilot notes
- treat notes as human judgment, not public-evidence automation
- treat Known-Cyno and Bait overrides as manual operator context, separate from derived evidence

The saved-note flag should now be easier to spot while scanning in the current released build.

---

## 9. Rebuild Killmail Derived Intel

Use **Rebuild Killmail Derived Intel** when you need to rebuild derived tables from existing local killmail archive data.

Typical reasons include:

- derived-intel schema changes
- derived-evidence backfills
- rebuilding existing local data into a newer derived format

Important release note:

**A rebuild is not required solely because of the `v0.9.5.1` hotfix.**

If you already had usable derived intel before `v0.9.5.1`, the hotfix itself is not the reason to rebuild.

---

## 10. Proton note

Current tester feedback indicates the Windows PMG build works under Proton.

Practical expectations:

- functional compatibility looks promising
- visual polish may not fully match native Windows
- a Linux-native release and Linux-native polish pass are still deferred

If you are using Proton, treat current support as validated enough to try, but still technical-preview quality rather than polished native Linux packaging.

---

## 11. Suggested first-use workflow

If you are opening PMG for the first time, this is a good starting pattern:

1. Download the latest release.
2. Extract the ZIP fully.
3. Run `PitmastersGrill.exe`.
4. Let startup complete.
5. Allow the automatic recent killmail refresh to do its work.
6. Run the broader recent-history pull if you want a fuller baseline.
7. Paste or import your pilot list.
8. Scan the main board first.
9. Right-click only the pilots that actually stand out.
10. Use notes and deeper source checks selectively.

That gives you the intended PMG flow much faster than trying to inspect every row in depth.

---

## 12. Good habits

A few simple habits make PMG more useful:

- keep the app in its extracted folder
- let startup/update work finish before judging first-run behavior
- use rebuild tools only when there is a real reason
- use the board for triage and the sidecar for confirmation
- treat PMG as decision support, not decision replacement

---

## 13. If something looks off

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

## 14. Final note

PMG is still in technical preview, but the goal is already clear:

take the noise of local, reduce the friction, and help you get to the useful part faster.
