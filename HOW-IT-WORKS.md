# Pitmaster's Grill - How It Works

This document explains, at a practical level, how **Pitmaster's Grill (PMG)** works in the current released technical-preview build, **v0.9.5.1**.

It is written to answer a simple question:

**How does PMG take a local list of pilot names and turn it into usable intel?**

This is not a full low-level engineering specification. It is a technical overview of the current working model.

---

## Core idea

PMG is built around a straightforward human-in-the-loop workflow:

1. a user provides a local-style pilot list
2. PMG classifies and accepts only plausible intel input
3. PMG resolves pilot identity and public context through cache and public providers
4. PMG adds killmail-derived signals where public evidence supports them
5. PMG layers in user-owned notes, overrides, and ignores
6. PMG presents the result as a fast-scanning board plus a sidecar detail view

The goal is not to replace player judgment.
The goal is to reduce the time between **"local spiked"** and **"I understand what I am looking at."**

---

## Board flow

The current board flow is easiest to understand as a staged pipeline.

### 1. Clipboard or local-list intake

PMG starts with a pilot list, usually from copied local-style text.

The app is designed around the reality of actual use:

- names arrive in batches
- the user wants answers quickly
- the input needs to support real gameplay flow rather than idealized one-pilot-at-a-time lookup

PMG can also handle large local-list shaped inputs well in current testing, though that should still be treated as observed test behavior rather than an absolute performance guarantee.

### 2. Guardrails and classification

Before enrichment starts, PMG applies intake guardrails so it does not treat arbitrary clipboard noise as a local list.

That means it tries to reject obvious non-local content such as:

- code
- markup
- stack traces
- shell output
- logs
- file paths
- oversized unrelated text

The goal is to keep the board pipeline focused on plausible pilot-name input instead of blindly trusting every clipboard payload.

### 3. Name resolution

Once PMG accepts the input, it resolves identity context for each pilot.

That stage can include:

- character identity
- corporation affiliation
- alliance affiliation

PMG uses local cache where possible so repeat lookups are faster and less dependent on external timing.

### 4. Public and cached enrichment

After identity resolution, PMG enriches rows with readable board context such as:

- kill counts
- loss counts
- average fleet-size context
- recent ship observations
- recent cyno-capable hull observations
- freshness/retry state

The purpose is not to mirror raw provider output one-for-one. The purpose is to normalize those results into a consistent operational view.

### 5. Killmail-derived intel

PMG also maintains local derived intel built from public killmail archive data.

That derived layer can provide:

- confirmed cyno-module observations
- industrial cyno plus tackle bait observations
- cyno-capable hull tackle observations
- supporting recent-activity context used by the board and detail views

This lets PMG surface useful historical evidence without pretending it has live fit or location visibility.

### 6. Manual notes, overrides, and ignores

PMG intentionally keeps user-owned judgment separate from public-data-backed evidence.

That means the user can apply:

- pilot notes
- Known-Cyno override
- Bait override
- typed ignore entries for pilots, corporations, or alliances

Notes and overrides are manual context. They are not the same thing as public evidence extracted from killmail-derived data.

Ignore entries suppress matching rows from the visible board after resolution.

### 7. Board presentation

Once those layers are combined, PMG presents the result as a board built for quick scanning under pressure.

The board is designed to let the user quickly notice:

- recognizable groupings
- active pilots
- suspicious public-evidence patterns
- likely escalation signals worth opening in details or zKill

---

## What the board is actually showing

The board is not just a list of names. It is a summarized operational view.

In the current release, it can surface fields such as:

- **Character**
- **Sig**
- **Alliance**
- **Corp**
- **Kills**
- **Losses**
- **Avg Fleet Size**
- **Last Ship Seen**
- **Last Seen**
- **Cyno Hull Seen**

These fields help answer practical questions like:

- who is affiliated together?
- which pilots appear active?
- who looks likely to matter?
- what public evidence suggests escalation risk?

---

## Compact mode and panel mode

PMG supports a board-first compact mode and a lighter panel/custom-shell style.

At a high level, compact/panel mode exists to keep PMG usable as a quick operational companion rather than a bulky desktop app.

In the current released build:

- the board remains the main surface
- compact mode is layout-driven rather than a separate feature fork
- panel-mode transparency should still be preserved
- normal row interactions should remain intact

That means left-click selection, right-click details, double-click zKill, and note access should continue to work even when PMG is running in a tighter board-first shape.

---

## Detail sidecar behavior

PMG opens pilot details in a sidecar inspector rather than taking over the whole board.

The current sidecar behavior is:

- open beside the board when there is room
- honor the saved left/right preference when possible
- flip or clamp placement near monitor edges when necessary
- keep the board visible while the user reads evidence and freshness context

This keeps PMG usable as a fast-scanning tool first, while still supporting closer inspection of a selected pilot.

---

## Evidence model

PMG is careful about the distinction between confirmed evidence and inference.

### Confirmed cyno module evidence

Confirmed cyno-module evidence comes from public victim item data on killmails.

If a public victim/loss item list shows a cyno module, PMG can treat that as strong evidence for that historical fit state.

### Hull context as inference

A cyno-capable hull observation is useful context, but it is still inference.

A pilot being seen in a cyno-capable hull does **not** prove that they fit or used a cyno.

### Industrial cyno plus tackle as derived bait evidence

PMG separately tracks public losses where industrial cyno and tackle appear together on the same victim item list.

That combination supports a derived bait signal because it is stronger than hull context alone.

### Tackle markers on cyno-capable hulls

PMG can also surface tackle context on cyno-capable hulls.

That can matter operationally, but it does **not** mean every cyno-capable hull with tackle should automatically be treated as bait.

This distinction is important because PMG is trying to be useful without overstating what public evidence actually proves.

---

## Caching and freshness

PMG is built for a situation where speed matters.

Repeatedly resolving the same information from scratch is slower and more dependent on external timing. Local caching helps by:

- reducing repeated lookups
- improving responsiveness
- supporting faster board population
- making the tool more practical during real use

PMG also surfaces freshness and retry context so the user can judge whether visible intel is current enough for the moment they are in.

---

## Human-in-the-loop model

A core design rule is that PMG supports the player's judgment instead of replacing it.

PMG can:

- summarize
- highlight
- sort
- surface patterns
- hand off to zKill or deeper review

PMG should not be treated as:

- a live-visibility oracle
- a claim of current location or current fit
- a replacement for player interpretation

The board helps a pilot think faster. It does not eliminate the need to think.

---

## Current limitations

Because PMG is still a technical preview, some practical limitations remain:

- public killmail evidence can be delayed or incomplete
- provider lookups can fail, throttle, or return partial data
- cache rebuilds may be needed after derived-intel schema/backfill changes
- some sidecar and compact-mode behavior is intentionally conservative to stay stable
- Proton compatibility looks good through current tester feedback, but native Linux polish remains deferred

This overview is meant to describe the current working shape of PMG without pretending that the technical design is already final.

---

## Summary

PMG works by taking a local pilot list, classifying it, enriching it with cached and public intel context, applying killmail-derived evidence where supported, layering in user-owned notes and ignores, and presenting the result as a board plus sidecar built for quick operational reading.

In plain language:

**drop in names, let PMG cook, and get back a faster read on who is actually in your local.**
