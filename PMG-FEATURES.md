# Pitmaster's Grill — Current Feature Snapshot

This document describes what **Pitmaster's Grill (PMG)** can do **right now** during the current tech-preview period.

It is meant to set clear expectations for testers, early followers, and anyone checking the repository to understand the tool's current practical value.

---

## Purpose

PMG is built to turn a local pilot list into quick, readable context that helps a player answer a simple question:

**Who is here, and what matters right now?**

This is not a full promise-of-future-features document.
This is the current functional snapshot.

---

## What PMG can do right now

### 1. Process a pilot list into a usable intel board

PMG can take a list of pilot names and populate a board view that turns those names into readable intel.

This gives the user a working overview instead of forcing them to manually look up pilots one by one.

### 2. Display pilot-level intel in a board layout

The current board presents pilot information in a compact table designed for quick scanning during live gameplay.

Current visible columns include:

* **Character**
* **Alliance**
* **Corp**
* **Kills**
* **Losses**
* **Avg Fleet Size**
* **Cyno Hull Seen**

This allows users to quickly identify likely threats, known groups, fleet tendencies, and potential cyno-related risk signals.

### 3. Show recent kill/loss activity context

PMG currently surfaces recent activity context through kill and loss counts, helping the user get a quick sense of whether a pilot appears active, dangerous, or relatively quiet.

This is meant to be practical and readable, not a wall of raw history.

### 4. Surface average fleet-size context

PMG currently shows **average fleet size** as part of the board.

This helps give quick directional context on whether a pilot tends to appear in smaller engagements or larger supported groups.

### 5. Surface cyno-related hull context

PMG currently surfaces the latest observed **cyno-capable hull context** where available.

This helps users quickly spot whether a pilot has recently been associated with ships that may matter for escalation risk.

### 6. Show corp and alliance affiliation

PMG currently resolves and displays both **corporation** and **alliance** information for pilots.

This helps users recognize group identity quickly and spot patterns across a local population.

### 7. Provide a detail pane for a selected pilot

PMG currently includes a lower detail area that expands on the selected pilot.

Based on the current interface, this includes fields such as:

* selected character name
* full corp
* full alliance
* freshness / timestamp context
* notes / tags area

This gives a quick single-pilot focus view without losing the main board.

### 8. Link directly to zKill for deeper follow-up

PMG currently includes an **Open zKill** action for a selected pilot.

This lets the user jump directly from the board into deeper manual review when needed.

That keeps the main board fast while still allowing more detailed investigation.

### 9. Show board freshness / refresh context

PMG currently shows freshness metadata in the interface, including board population status and refresh timing.

This helps the user understand whether the visible intel is current and how recently the board was populated.

### 10. Allow the board to be cleared

PMG currently includes a **Clear** action, allowing the current board contents to be reset.

That supports quick reuse during repeated local checks.

---

## What PMG is good at right now

In its current state, PMG is already useful for:

* getting a quick read on a local spike
* identifying corp/alliance groupings
* spotting active or historically dangerous pilots at a glance
* flagging possible cyno-related context
* reducing one-by-one manual lookups
* moving from "raw names" to "decision-grade summary" faster

That is the current strength of the tool: **speed to useful context**.

---

## What this document intentionally does not claim

This document is intentionally limited to the current tech-preview feature set.

It does **not** claim that PMG already includes every planned workflow, automation path, or future quality-of-life feature.

As development continues, this document should be updated to reflect what is actually working in the live tech-preview build rather than what is merely planned.

---

## Current maturity note

PMG is currently in **tech preview / active development**.

That means:

* real functionality exists today
* active testers are using it
* feedback is shaping fixes and improvements
* features may still evolve before broader release

The goal during this phase is simple:

**make the tool more useful, more stable, and more practical with each iteration**

---

## Suggested future structure for this file

As PMG grows, this file can later be split into sections such as:

* **Current Features**
* **In Progress**
* **Planned / Not Yet Implemented**
* **Known Limitations**

For now, this version is meant to clearly answer:

**What can PMG do today?**
