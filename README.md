# Pitmaster's Grill

<p align="center">
  <img src="./PitmastersGrill/Assets/AppIcon.png" alt="Pitmaster's Grill icon" width="120" />
</p>

<p align="center">
  <strong>Fast, readable local intel for EVE Online.</strong>
</p>

Pitmaster's Grill, or PMG, is a free Windows desktop intel companion for EVE Online.

PMG turns copied EVE local lists into a readable board of public pilot context. It is built for quick, practical use during live play: copy local, scan the Grill, review Analysis when time allows, refresh public intel when needed, and open zKill for deeper manual review.



https://github.com/user-attachments/assets/179a57b2-ae23-46d0-b53c-fb7f76ce99a7




PMG is now in **General Release**.

Current release: **Pitmasters Grill v1.2.0**  
Latest release: **[v1.2.0 General Release](https://github.com/SmokeyForged/Pitmasters-Grill/releases/tag/v1.2.0)**  
Full release history: **[GitHub Releases](https://github.com/SmokeyForged/Pitmasters-Grill/releases)**

Repository status on `main`: **1.2.0 general release foundation and support improvements are present**. This release focuses on maintainability, automated validation, and safer future iteration. It does **not** add new EVE intel sources or change PMG's public-data boundaries.

---

## Quick Start

1. Download the latest release from the [Releases page](https://github.com/SmokeyForged/Pitmasters-Grill/releases).
2. Extract PMG to a folder you control.
3. Launch `PitmastersGrill.exe`.
4. Copy an EVE local list.
5. Review the Grill board.
6. Right-click a pilot for PMG details, or double-click to open zKill.

PMG uses public data and local caching. On first use, it may build or refresh local public killmail-derived intel.

---

## What PMG Does

PMG helps answer the local-spike question:

> Who is here, and what matters right now?

PMG can:

- parse copied EVE local-style pilot lists
- resolve pilots, corporations, and alliances where available
- show public kill/loss context
- show recent public ship observations
- surface cyno, tackle, bait, and watchlist context
- summarize visible-pilot composition in Analysis
- repair recent public intel with Today's Freshness and Historical Freshness
- optionally use R2Z2 for live zKill-known killmail ingestion
- cache public intel locally to reduce repeated lookup cost
- export diagnostics for troubleshooting
- open zKill for deeper manual review

PMG does **not** read EVE client memory, inspect network traffic, automate gameplay, use private ESI character data, or claim live grid/location/cloak certainty.

---

## Main App Areas

PMG is organized around a few top-level tabs:

- **Analysis** — summary view for the current visible board.
  <img width="761" height="660" alt="image" src="https://github.com/user-attachments/assets/a92772d7-3460-4ee5-8fef-2a6adeb68459" />

- **Grill** — the main pilot board.
  <img width="1134" height="409" alt="image" src="https://github.com/user-attachments/assets/009de61b-7824-4bd3-94ee-69e03878b22e" />

- **Intel** — killmail intel status, freshness tools, R2Z2, diagnostics, and cache controls.
- **Ignore List** — manage ignored pilots, corporations, and alliances.
  <img width="730" height="800" alt="image" src="https://github.com/user-attachments/assets/5ff3b8aa-35e1-4e8f-a52e-900735f6b3d9" />

- **Settings** — app behavior, PMG themes, visibility, and layout options.
  <img width="732" height="802" alt="image" src="https://github.com/user-attachments/assets/4c6ded45-6ba5-4cc5-b902-4ad4b5e26c46" />

- **Help** — shortcut and workflow reference.

---

## Public Intel Freshness Model

PMG uses layered public-data freshness:

1. **Archive Backfill**  
   Builds the historical baseline from completed public zKill archive days.

2. **R2Z2 Live Feed**  
   Optional live zKill-known killmail ingestion. Disabled by default.

3. **Today's Freshness**  
   Manual visible-pilot same-day/recent repair.

4. **Historical Freshness**  
   Manual visible-pilot recent completed-day repair.

5. **Background Historical Repair**  
   Bounded startup enrichment over known/recent pilots with cooldown protection.

These features improve PMG's local view of public evidence. They do not make public data complete, and they do not expose private data.

---

## Useful Repo Links

New here? Start with:

- **[How to Navigate This Repo](./HOW-TO-NAVIGATE-THIS-REPO.md)**  
  A top-level guide to the repo structure and where different types of information live.

- **[Latest Release](https://github.com/SmokeyForged/Pitmasters-Grill/releases/latest)**  
  Download the current release build.

- **[Patch Notes](./Patch%20Notes/)**  
  Full release history and version notes.

- **[1.2.0 General Release Notes](./Patch%20Notes/General-Release_1-2-0.md)**  
  Summary of the 1.2.0 support, maintainability, CI, and validation work.

- **[Current Feature Snapshot](./PMG-FEATURES.md)**  
  A deeper overview of PMG's current feature set and limitations.

- **[How It Works](./HOW-IT-WORKS.md)**  
  Technical overview of PMG's data flow and evidence model.

- **[First-Time Use](./FIRST-TIME-USE.md)**  
  Setup and first-run guidance.

- **[EVE ToS Compliance](./EVE-TOS-COMPLIANCE.md)**  
  PMG's safety framing around EVE client boundaries.

- **[Developer Notes](./DEVELOPER-NOTES.md)**  
  Implementation notes for maintainers and contributors.

- **[Application Source](./PitmastersGrill/)**  
  Main WPF application source.

- **[Automated Tests](./PitmastersGrill.Tests/)**  
  Deterministic test coverage for non-UI services and extracted controllers.

- **[Issues](https://github.com/SmokeyForged/Pitmasters-Grill/issues)**  
  Bug reports, enhancement requests, and community feedback.

---

## Feedback

Useful bug reports include:

- PMG version
- what you were trying to do
- what you expected
- what happened instead
- whether the issue affected Grill, Analysis, Intel, freshness, diagnostics, settings, or startup
- diagnostics bundle if requested by a maintainer

Please do not post private credentials, launcher data, browser data, raw logs with secrets, or unrelated local files.

---

## Why PMG Is Free

PMG is intended to remain a free community tool.

No paywall.  
No required donation.  
No nonsense.

If PMG is useful and you want to give something back, the preferred gesture is to do something useful for someone else: help feed someone, cook for someone, donate to a local food pantry, or otherwise pass something practical forward.
