# Pitmaster's Grill
<img width="250" height="250" alt="PMG Icon" src="https://github.com/user-attachments/assets/95b30b3f-1227-49f8-b153-d71aacf6c0a3" />

> Fast, readable local intel for EVE Online.

Pitmaster's Grill (PMG) is a Windows desktop intel companion for turning copied EVE local lists into a board of useful public pilot context. It is built for quick, practical reads during live play: paste local, let PMG resolve what it can, scan the board, and open deeper intel only when needed.

PMG is still a **technical preview**. It is useful today, but behavior, UI details, and data coverage can continue to change between releases.

Current release: **Pitmasters Grill 0.9.5.1**  
Tag: **v0.9.5.1**  
Release commit: **172c810**

---


## How to Navigate This Repo

New here? Start with these:

1. **[Latest Release: v0.9.5.1](https://github.com/SmokeyForged/Pitmasters-Grill/releases/tag/v0.9.5.1)**  
   Current hotfix build with compact/panel mode stability fixes, sidecar placement improvements, and saved-note flag visibility polish. For older builds and full release history, see **[all releases](https://github.com/SmokeyForged/Pitmasters-Grill/releases)**.

2. **[Current Feature Snapshot](./PMG-FEATURES.md)**  
   Detailed overview of what PMG can do right now, including compact mode, board interactions, cyno/tackle evidence, killmail-derived intel, and known limitations.

3. **[Application Source](./PitmastersGrill/)**  
   Main WPF application code. This is where the PMG UI, resolver services, persistence, providers, models, and killmail-derived intel logic live.

4. **[Developer Notes](./DEVELOPER-NOTES.md)**  
   Technical notes for maintainers and contributors. Useful for understanding current implementation decisions, design constraints, and project direction.

5. **[Issues](https://github.com/SmokeyForged/Pitmasters-Grill/issues)**  
   Active bug reports, enhancement requests, community feedback, and work tracking.

6. **[README](./README.md)**  
   High-level project overview, basic usage, safety framing, feedback guidance, and release status.


---

## What PMG Does

PMG helps answer the immediate local-spike question:

**Who is here, and what matters right now?**

Current PMG builds can:

- parse copied EVE local-style pilot lists
- resolve pilot identities, corporations, and alliances where available
- show public kill/loss context and recent ship observations
- highlight cyno-related public evidence and cyno-capable hull history
- surface compact bait/tackle evidence from public victim killmail item data
- cache results locally to reduce repeated lookup cost
- open zKill for deeper manual review
- let you ignore pilots, corporations, or alliances by typed ID
- export diagnostics for troubleshooting without including secrets or raw unrelated user files

PMG works from user-provided local lists and public data. It does **not** read client memory, inspect network traffic, automate gameplay, use EVE SSO, or claim live grid/location/cloak visibility.

---

## v0.9.5.1 Highlights

### Hotfix focus

v0.9.5.1 is a focused hotfix on top of the v0.9.5 community-feedback release.

This patch improves:

- compact/panel mode stability
- custom shell behavior
- transparency preservation
- pilot detail sidecar placement near monitor edges
- saved-note flag visibility on colored rows

No killmail-derived intel rebuild is required solely for this hotfix.

### v0.9.5 community-feedback foundation

## v0.9.5 Highlights

### Board-first compact mode

<img width="768" height="756" alt="image" src="https://github.com/user-attachments/assets/f60ac21a-a54b-4317-b5b3-a2be0f5e9feb" />



Compact mode was redesigned as a true operational board view. It preserves row colors, board resizing, and always-on-top behavior while removing unnecessary chrome.

App-local hotkeys:

- **Insert** - toggle compact/normal mode
- **Delete** - clear board
- **Home** - refresh/reprocess clipboard intel
- **Esc three times** - exit PMG

Board interactions:

- left click selects a row
- right click opens PMG pilot details
- double-click opens zKill
- in compact mode, delayed left-click-hold can drag the window from rows, column headers, or blank board space

### Compact pilot detail sidecar

Pilot details now open as a compact sidecar inspector beside the board when possible. The sidecar inherits PMG theme, uses compact evidence wording, and keeps the board usable while details are open.

Notes were moved out of the detail pane and into board-level note/flag icons.

### Better board grouping context

Optional corp/alliance concentration counts can be enabled on the board. Counts are based only on visible rows after ignore filtering and hide solo `[1]` counts.

Example:

- `Some Corp [3]`
- `Some Alliance [8]`

### Cyno, tackle, and bait evidence

The cyno-capable hull catalog was expanded and corrected. **Cyno Hull Seen** now uses newest-observation semantics, so the board shows the most recent public killmail-derived cyno-capable hull observation for each pilot.

PMG recognizes Warp Scrambler and Warp Disruptor modules from public victim killmail item lists as part of threat analysis.

Evidence terminology is intentionally short:

- `hard` = standard Cynosural Field Generator I
- `covert` = Covert Cynosural Field Generator I
- `indi` = Industrial Cynosural Field Generator

Industrial cyno + tackle still supports PMG's bait evidence signal. Broader tackle evidence can also surface for cyno-capable hulls without automatically marking every cyno-capable hull with tackle as bait.

### Killmail update compatibility

PMG no longer depends on launching external Windows `tar.exe` for killmail archive extraction. This improves Linux/Proton compatibility and avoids fragile assumptions about System32 tools existing in a Wine prefix.

Users with existing killmail data should run **Rebuild Killmail Derived Intel** after updating so PMG can populate the latest derived cyno/tackle/bait evidence tables.

---

## Basic Use

1. Download the latest release zip from the GitHub Releases page.
2. Extract it to a folder you control.
3. Launch PMG.
4. Copy an EVE local list.
5. Use **Home** or the refresh/reprocess action if you want PMG to reprocess the clipboard.
6. Scan the board.
7. Right click a row for PMG details, or double-click a row to open zKill.

For best results, configure killmail intel/cache settings and let PMG build or refresh its local public killmail-derived intel.

---

## Settings Worth Checking

- **Theme / dark mode / opacity**: PMG supports readable dark operational themes and opacity-aware backgrounds.
- **Compact mode**: use Insert to switch between normal and board-first views.
- **Detail placement**: the pilot detail sidecar placement preference is under Intel configuration.
- **Ignore List**: typed ignore entries can suppress pilots, corporations, or alliances by ID.
- **Corp/alliance counts**: optional board counts show visible local concentration after ignore filtering.
- **Killmail intel**: refresh/update controls maintain the local public killmail-derived cache.
- **Rebuild Killmail Derived Intel**: run this after updating from earlier builds with existing killmail data.

---

## Limitations and Expectations

PMG is evidence-first. It can summarize public and cached evidence, but it does not know private or live state.

PMG does not claim:

- live jumps
- current grid presence
- current location
- cloak status
- private fitting visibility
- private ESI character data
- gameplay automation

Public killmail data can be incomplete or delayed. Cyno, tackle, and bait indicators are based on available public evidence and should be treated as operational hints, not certainty.

Technical-preview expectations:

- UI and workflows may continue to evolve.
- Provider availability and public data freshness can vary.
- Cache rebuilds may be needed after schema or derived-intel changes.
- Diagnostics are intended to help testers report issues clearly.

---

## Feedback

Useful feedback includes:

- PMG version
- what you pasted or tried to process, summarized safely
- what you expected
- what happened
- whether the issue affected board population, details, compact mode, killmail intel, ignore behavior, or UI readability
- diagnostics bundle if requested by a maintainer

Do not post private credentials, launcher data, browser data, raw logs with secrets, or unrelated local files.

Repository and releases:

**https://github.com/SmokeyForged/Pitmasters-Grill**

---

## Why PMG Is Free

PMG is intended to remain a free community tool.

No paywall. No required donation. No nonsense.

If PMG is useful and you want to give something back, the preferred gesture is to do something useful for someone else: help feed someone, cook for someone, donate to a local food pantry, or otherwise pass something practical forward.
