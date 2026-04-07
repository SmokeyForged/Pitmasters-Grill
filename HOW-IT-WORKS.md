# Pitmaster's Grill — How It Works

This document explains, at a practical level, how **Pitmaster's Grill (PMG)** works during the current tech-preview period.

It is written to answer a simple question:

**How does PMG take a local list of pilot names and turn it into usable intel?**

This is not meant to be a full low-level engineering specification. It is a technical overview of the current working model.

---

## Core idea

PMG is built around a straightforward workflow:

1. a user provides a list of pilot names
2. PMG resolves that list against external/public intel sources and local cached data
3. PMG normalizes the results into a consistent internal view
4. PMG displays the result as a fast-scanning board with pilot-level context
5. the user can drill into a pilot or jump out to deeper source material when needed

The goal is not to replace player judgment.
The goal is to reduce the time between **"local spiked"** and **"I understand what I am looking at."**

---

## Input model

PMG starts with a pilot list.

In practice, that means the tool is built around the reality of EVE usage:

* names arrive in batches
* the user wants answers quickly
* the input needs to support real gameplay flow, not an idealized workflow

The current design direction is centered on processing a local population into a board view rather than forcing one-pilot-at-a-time manual lookups.

---

## Processing model

Once PMG has a list of pilot names, it begins resolving useful context for each pilot.

At a high level, the processing flow looks like this:

### 1. Name intake

PMG receives the pilot list and prepares it for lookup.

This stage exists to make sure the tool is operating on a clean set of pilots before enrichment begins.

### 2. Data resolution

PMG resolves pilot-related context from the sources available to it.

This includes practical information such as:

* character identity context
* corporation affiliation
* alliance affiliation
* recent kill/loss-derived activity context
* fleet-size pattern context
* recent cyno-hull-related context where available

### 3. Normalization

The raw source data is not useful if it stays fragmented.

PMG converts source responses into a consistent internal representation so the board can display a clean, readable summary across all pilots.

This is important because the user is not trying to inspect multiple different source formats during a live moment. They are trying to answer questions quickly.

### 4. Board population

Once pilot data has been resolved and normalized, PMG populates the board.

The board is designed to support rapid visual scanning so the user can:

* spot familiar alliances or corps
* identify active or dangerous pilots
* recognize likely support patterns
* notice possible cyno-related escalation indicators

### 5. Detail and follow-up

When a pilot needs a closer look, PMG supports a more focused view through the detail pane and direct follow-up actions such as opening the selected pilot in zKill.

This lets the main board stay fast while still supporting deeper manual investigation.

---

## Data sources and source philosophy

PMG is built around pulling useful context from public data sources rather than making the user manually chase the same information across multiple tabs.

The exact source mix may continue to evolve during tech preview, but the operating idea is consistent:

* use public data sources that provide meaningful pilot activity context
* resolve corp and alliance identity where useful
* reduce repeated lookups through local caching where practical
* keep the user close to the source of truth when deeper inspection is needed

That is why PMG includes direct handoff to deeper source material instead of pretending the board itself should be the only thing a user ever needs.

---

## What the board is actually showing

The board is not just a list of names.
It is a summarized operational view.

In the current tech-preview shape, it is built to surface fields such as:

* **Character**
* **Alliance**
* **Corp**
* **Kills**
* **Losses**
* **Avg Fleet Size**
* **Cyno Hull Seen**

These fields are intended to answer practical questions like:

* Who is affiliated together?
* Which pilots appear active?
* Do any of these names suggest escalation risk?
* Are these likely solo players, small-gang pilots, or people who tend to arrive with support?

The board is meant to compress useful signal into something a player can read quickly.

---

## Why caching matters

PMG is being built for a situation where speed matters.

Repeatedly resolving the same information from scratch is slower, noisier, and more dependent on external timing. Local caching helps by:

* reducing repeated lookups
* improving responsiveness
* supporting faster board population
* making the tool more practical during real use

The intent is not to hide source freshness. The intent is to balance speed with enough visibility into how current the visible board data is.

---

## Freshness and trust

Intel has a shelf life.

PMG therefore includes board freshness and refresh context so the user can understand how current the displayed board is.

That matters because a fast board only helps if the user can also judge whether the information is fresh enough to trust for the moment they are in.

---

## What PMG is optimized for

PMG is optimized for:

* fast transition from raw names to useful context
* readable summaries over raw source browsing
* real gameplay usability over perfect theoretical completeness
* preserving the player's role in interpretation and decision-making

This means PMG is intentionally biased toward **useful and timely** rather than **maximally exhaustive**.

---

## What PMG is not trying to be

PMG is not trying to be:

* a replacement for player judgment
* a giant wall of every possible data point
* a one-screen substitute for every deep source
* a claim that raw data alone is the same thing as good intel

The board is a front-end for faster understanding, not a promise that every question is solved at a glance.

---

## Current limitations of this overview

Because PMG is still in tech preview, some implementation details are still evolving.

That means this document intentionally does **not** lock in every internal mechanism such as:

* exact provider wiring
* exact cache implementation details
* exact internal processing orchestration
* exact future feature boundaries

Those are better documented once they settle.

This document is meant to describe the working technical shape of the tool without pretending the internal design is already final.

---

## Summary

PMG works by taking a local pilot list, enriching it with useful public intel context, normalizing that information into a consistent internal model, and presenting it as a board built for quick operational reading.

In plain language:

**drop in names, let PMG cook, and get back a faster read on who is actually in your local.**
