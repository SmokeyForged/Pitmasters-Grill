# Pitmasters Grill — Current Feature Snapshot

This document describes what Pitmasters Grill (PMG) can do right now during the current technical-preview period.

It is meant to set clear expectations for testers, early followers, and anyone checking the repository to understand the tool's current practical value.

Current public snapshot: **Technical Preview v0.9.4**

---

## Purpose

PMG is built to turn a copied EVE local pilot list into quick, readable context that helps a player answer a simple question:

> Who is here, and what matters right now?

PMG is not meant to replace player judgment. It is meant to reduce the amount of manual browser-tab work needed to get useful pilot context during live gameplay.

This is not a promise-of-future-features document. This is the current functional snapshot.

---

## Platform

PMG is currently a **Windows desktop application**.

Current public project shape:

- Windows desktop app
- WPF-based interface
- .NET 10 Windows target
- local cache/database support
- packaged technical-preview releases through GitHub Releases

---

## Core Workflow

PMG is built around a simple operating pattern:

1. Copy or otherwise provide a local pilot list.
2. PMG screens the clipboard/input to make sure it looks like an EVE local list.
3. The board shows pilot rows quickly.
4. PMG progressively enriches those rows with identity, corporation, alliance, activity, and signal context.
5. The user scans the board, opens details when needed, and decides what matters.

The goal is to move from raw names to useful context quickly without turning the tool into gameplay automation.

---

## What PMG Can Do Right Now

### 1. Process a Pilot List into a Usable Intel Board

PMG can take a list of pilot names and populate a board view that turns those names into readable intel.

The current board is designed for quick scanning rather than deep manual lookup. PMG can show lightweight name-only rows quickly and then enrich those rows progressively as data resolves.

This helps avoid the feeling that the app has stalled while it waits for every external lookup to complete.

---

### 2. Display Pilot-Level Intel in a Board Layout

The main board presents pilot information in a compact table designed for live-use scanning.

Current board context includes items such as:

- character name
- signal / Sig indicator
- alliance
- corporation
- kill count
- loss count
- average fleet size
- last ship seen
- last seen timing/context
- cyno hull context

The exact visible columns may depend on the user's column visibility settings.

---

### 3. Customize Board Columns

PMG includes board column visibility controls.

Optional columns can be shown or hidden so users can tune the board for their monitor size, play style, and information tolerance.

The Character column remains core because every row needs to stay identifiable.

---

### 4. Resolve Corporation and Alliance Context

PMG resolves and displays corporation and alliance information where available.

This helps users quickly spot group patterns, familiar organizations, hostile blocs, friendly/noise groups, or pilots that belong together.

---

### 5. Show Recent Public Activity Context

PMG surfaces public kill/loss-derived activity context.

This includes practical summary values such as kills, losses, and related historical context intended to help users quickly judge whether a pilot appears active, dangerous, quiet, or worth deeper review.

PMG is not trying to present a full raw history wall in the main board. The board is meant to be readable under pressure.

---

### 6. Show Average Fleet-Size Context

PMG can display average fleet-size context where enough public/local-derived information exists.

This gives directional context about whether a pilot tends to appear in smaller engagements, larger supported groups, or fleet-like activity patterns.

This should be treated as a useful signal, not a certainty.

---

### 7. Surface Cyno-Related Context

PMG currently includes cyno-related board signaling.

Current cyno signal behavior includes:

- confirmed cyno module evidence from public loss victim item data
- inferred cyno capability from known cyno-capable hull history
- possible/weak signal handling where evidence is less certain
- unknown/no-signal handling when evidence is absent

Supported confirmed module evidence includes:

- Cynosural Field Generator I
- Covert Cynosural Field Generator I
- Industrial Cynosural Field Generator

PMG distinguishes between confirmed module evidence and hull inference.

A cyno-capable hull is not treated as proof that a cyno was fitted. Confirmed public loss module evidence is treated as stronger evidence than hull capability alone.

---

### 8. Display Signal States Visually

PMG uses signal labels/icons and board coloring to help draw attention to important rows.

Current signal intent includes states such as:

- confirmed cyno evidence
- inferred cyno-related capability
- possible signal
- bait-style signal
- unknown/no signal

The UI is intended to avoid relying on color alone. Signal labels and icons remain important, especially for readability and accessibility.

---

### 9. Open a Standalone Pilot Details Window

PMG now uses a standalone pilot details window for focused review.

The details window gives a cleaner place to inspect a selected pilot without cramming everything into the main board.

The details view can include focused context such as:

- selected pilot identity
- corporation and alliance context
- freshness/source context
- cyno signal evidence
- concise evidence bullets
- action buttons for deeper review or ignore handling

---

### 10. Link Out to zKill for Deeper Review

PMG includes an Open zKill action for selected pilots.

This keeps the main board fast while still allowing users to jump out to deeper manual review when needed.

PMG is meant to reduce unnecessary browser-tab hunting, not prevent deeper investigation.

---

### 11. Ignore Pilots, Corporations, and Alliances

PMG includes typed ignore-list support.

Current ignore types include:

- pilot ID
- corporation ID
- alliance ID

The Ignore List can display entries with:

- ID
- resolved name
- type

Name resolution is non-blocking. If PMG cannot resolve the display name immediately, the ignore entry can still function by ID.

The pilot details window also supports ignore actions for:

- Ignore Pilot
- Ignore Corp
- Ignore Alliance

This is useful for removing known-friendly, known-alt, or otherwise irrelevant rows from the board during active use.

---

### 12. Maintain Local Cache and Public Intel Data

PMG uses local cached data to improve repeat performance and reduce unnecessary repeated lookups.

Current related behavior includes:

- local cache/database support
- startup update behavior for recent zKill data
- a 30-day zKill pull option for broader recent-history refresh
- cache maintenance tooling
- bounded rebuild support for killmail-derived intel

The cache is intended to make PMG more responsive over time and give the app a better local baseline for public intel context.

---

### 13. Rebuild Killmail-Derived Intel

PMG includes a bounded “Rebuild Killmail Derived Intel” workflow.

This is intended for rebuilding derived intel such as confirmed cyno module observations from imported/extracted public killmail data without requiring a full wipe-and-rebuild approach.

This is a maintenance feature, not something users should need to mash constantly during normal play.

---

### 14. Use Clipboard Guardrails

PMG includes clipboard/input guardrails intended to prevent non-EVE content from being treated as a local list.

The app is designed to reject obvious non-local-list content such as:

- code
- markup
- shell output
- logs
- stack traces
- command text
- filesystem paths
- oversized or suspicious clipboard payloads

Recent improvements also make large valid EVE local lists less likely to be rejected incorrectly.

This helps make PMG safer to leave open while using the clipboard normally outside of EVE.

---

### 15. Generate Diagnostics Packages

PMG includes diagnostics support for troubleshooting.

Diagnostics are intended to help identify issues with areas such as:

- provider health
- cache state
- performance timing
- clipboard processing
- resolver behavior
- ignore-list decisions
- app logs and debug traces

Users should review diagnostic bundles before sharing them publicly. Diagnostics may still include useful troubleshooting context such as EVE character names, public URLs, public IDs, and normal PMG activity.

---

### 16. Show Provider Health and Performance Context

PMG includes provider-health and performance instrumentation intended to help separate external data/provider issues from local application behavior.

This is useful during technical preview because many apparent “app problems” may actually be provider timing, API behavior, cache state, or local data freshness issues.

---

### 17. Use Compact and Panel-Oriented Layouts

PMG includes UI work aimed at making the app usable beside EVE and other tools.

Current interface behavior includes:

- compact mode support
- smaller-window support
- persistent core controls such as Clear / Compact / Exit in compact layouts
- panel-style opacity behavior
- a move control for repositioning borderless/panel-style windows

The intent is to support real monitor-constrained gameplay setups, not just ideal full-size screenshots.

---

### 18. Adjust Opacity While Preserving Readability

PMG includes opacity/panel improvements.

The design intent is that background surfaces can become more transparent while board text, row signals, and grid/cell borders remain readable.

This is still part of the technical-preview UI evolution.

---

### 19. Use Multiple Visual Themes

PMG includes multiple visual themes with a stronger tactical grill/BBQ-inspired identity.

The current theme direction is meant to make the board feel more like the central “grill” surface of the application while keeping the UI practical and readable.

---

### 20. Use Accessibility / Color-Blind Support

PMG includes color-blind/accessibility support for board signal colors.

The app continues to use labels and icons so users are not forced to rely only on color to interpret signal state.

---

## What PMG Is Good At Right Now

In its current technical-preview state, PMG is already useful for:

- getting a quick read on a local spike
- turning a copied pilot list into a readable board
- identifying corp/alliance groupings
- spotting active or historically notable pilots
- surfacing public kill/loss context
- surfacing cyno-related context with evidence strength
- reducing one-by-one manual lookups
- ignoring known-noise pilots, corps, or alliances
- jumping to deeper zKill review when needed
- providing enough diagnostics to support early tester feedback

That is the current strength of the tool: speed to useful context.

---

## Important Policy / Trust Boundaries

PMG is designed as a human-in-the-loop companion tool.

PMG is not intended to:

- modify the EVE client
- automate gameplay
- issue in-game commands
- control ships
- activate modules
- perform navigation
- automate market actions
- act as a bot
- scrape restricted client cache data
- inspect packets
- reverse-engineer the EVE client
- replace player decision-making

PMG is intended to summarize public or otherwise permitted context and present it in a form that helps a human player think faster.

PMG is not affiliated with, endorsed by, sponsored by, or operated by CCP.

---

## Current Limitations and Expectations

PMG is still a technical preview.

That means:

- behavior may continue to change between releases
- the UI may continue to evolve
- public data may be incomplete, stale, delayed, or unavailable
- inferred signals should not be treated as certainty
- confirmed cyno module evidence depends on available public loss data
- PMG has no live fit visibility
- PMG does not know player intent
- diagnostics may need user review before public sharing
- feedback from active use is still shaping the product

This is normal for the current project stage.

---

## What PMG Is Not Yet

PMG should not be treated as:

- a finished General Release product
- a perfect threat detector
- a replacement for player judgment
- a replacement for manual intel review
- an official CCP-approved tool
- a guarantee that a pilot is dangerous or safe
- a guarantee that public data is complete
- a cross-platform polished release

The goal right now is practical technical-preview usefulness with honest expectations.

---

## Basic First-Time Use Pattern

A good first-time use pattern is:

1. Download the latest release ZIP from GitHub Releases.
2. Extract the ZIP into its own folder.
3. Run `PitmastersGrill.exe`.
4. Let startup/update work complete.
5. Optionally run the 30-day zKill pull for a stronger recent baseline.
6. Copy a local pilot list.
7. Let PMG populate and enrich the board.
8. Open details or zKill only when deeper review is needed.
9. Use ignores to remove known noise from future board views.

Do not run PMG directly from inside the ZIP. Extract it first.

---

## Feedback PMG Needs Most

During technical preview, useful feedback includes:

- did the board populate correctly?
- did valid local input get rejected?
- did non-local clipboard content get ignored safely?
- did any pilot/corp/alliance resolve incorrectly?
- did ignore actions work as expected?
- were signal labels/colors readable?
- was the details window useful?
- was the UI readable beside EVE?
- did diagnostics help explain a problem?
- what information was useful versus noise?

Good bug reports usually include:

- PMG version
- what you were doing
- what you expected to happen
- what actually happened
- whether it was cosmetic, workflow-related, or disruptive
- screenshots if useful
- a reviewed diagnostic bundle if appropriate

---

## Summary

Pitmasters Grill is currently a Windows technical-preview EVE local-intel companion.

It can process copied pilot lists, populate a readable board, enrich pilots with public/local cached intel, show corp/alliance/activity context, surface cyno-related evidence, support typed ignores, open deeper zKill review, and provide diagnostics for troubleshooting.

The project focus remains simple:

> Help a player understand local faster without playing the game for them.
