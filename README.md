# Pitmaster's Grill

> Fast, readable local intel for EVE Online.

Pitmaster's Grill (PMG) is a Windows desktop intel companion built to turn a copied local list into useful pilot context quickly. The goal is simple: get the useful part of local without fighting your tools while space is getting loud.

PMG is built for practical use during real gameplay. Paste the names, let the app resolve what it can, and get back a board that is readable at a glance.

---

## Project Status

**Current status:** Technical Preview / active development.

PMG is in active development and the public repository is its current home. The project is already usable in tech preview form, but it is still evolving, and the UI, feature set, and implementation details may continue to change as testing and feedback continue.

---

## What PMG does today

PMG is designed to help with a live local spike by giving you a cleaner view of who is present and what kind of attention they deserve.

Current workflow and capabilities include:

- paste or otherwise feed a local list into the app
- resolve pilot identity and affiliation context
- surface recent activity and other fast-read pilot intel
- use local caching to improve repeat performance
- open deeper source intel when you need it
- keep the board readable during live use instead of burying everything in browser tabs

The emphasis is speed, clarity, and practical value.

---

## Quick Links

- [Download the latest release](https://github.com/SmokeyForged/Pitmasters-Grill/releases)
- [First-Time Use Guide](FIRST-TIME-USE.md)
- [Current Feature Snapshot](PMG-FEATURES.md)
- [EVE Policy / ToS Compliance Position](EVE-TOS-COMPLIANCE.md)
- [Patch Notes](Patch%20Notes)

* * *

---

## Screenshot Gallery

> Replace the placeholder paths below with your real screenshots when you are ready.

### Main board

<img width="1190" height="1384" alt="image" src="https://github.com/user-attachments/assets/73dab4e3-f137-4459-a0eb-538c9fd9da77" />


### Pilot details pane

<img width="1862" height="1396" alt="image" src="https://github.com/user-attachments/assets/620069dd-7977-4dce-adec-284d9b39c9bb" />


### Compact mode

<img width="1189" height="1386" alt="image" src="https://github.com/user-attachments/assets/4c8c34b4-ed86-4438-ac5a-58704065fe7f" />



### Settings 

<img width="1185" height="619" alt="image" src="https://github.com/user-attachments/assets/1d374a44-c974-479e-a66d-ba596a5134ea" />


### Ingore List

<img width="1193" height="854" alt="image" src="https://github.com/user-attachments/assets/dc785eb4-779c-4436-8ab9-0ddca5857d79" />

### Intel Config

<img width="1186" height="914" alt="image" src="https://github.com/user-attachments/assets/fbee2627-3153-4c4a-bbde-f42169ef1abf" />

---


## Why PMG exists

When local spikes, the first question is usually not "how many tabs can I open?"

It is:

> Who are these people, and which ones matter right now?

PMG exists to shorten the distance between that question and a useful answer.

The tool is built for the moment where you have names in local, limited attention, and a need for quick context. PMG turns a copied pilot list into a readable board, enriches what it can from public/local cached intel, and lets the player decide what matters.

This project is inspired in part by the older spirit of community-made EVE tools that were practical, shared, and built because somebody cared enough to make something useful for other players.

* * *

## Design Goals

PMG is being shaped around a few working rules.

### Fast enough to matter

If intel shows up after the fight, it is garnish.

PMG is designed to get from a pilot list to useful context quickly. Rows should appear, resolve, and improve progressively instead of forcing the user to wait for every lookup before seeing anything useful.

### Clear over clever

The UI should help you make a decision, not demand attention for its own sake.

The board is meant to be scanned under pressure. Details are available when needed, but the main grid should remain readable and practical.

### Support judgment, do not replace it

PMG is not a threat oracle.

It does not know pilot intent, fleet context, voice comms, bait plans, or what is about to land on grid. It presents signals and context so a human player can think faster.

### Keep the trust boundary obvious

PMG should remain a companion tool, not gameplay automation.

The player supplies the input, reads the output, and makes the decisions.

* * *

## What PMG does today

PMG is currently a Windows technical-preview EVE local-intel companion.

Current capabilities include:

- processing copied pilot lists into a readable intel board
- resolving pilot, corporation, and alliance context
- showing kill/loss and activity context from public/local cached data
- showing average fleet-size context
- surfacing cyno-related evidence and signal states
- distinguishing confirmed cyno module evidence from hull inference
- opening a standalone pilot details window for focused review
- linking selected pilots out to zKill for deeper manual review
- supporting typed ignores for pilots, corporations, and alliances
- maintaining local cache data for better repeat performance
- providing diagnostics packages for troubleshooting
- using clipboard guardrails to reject obvious non-EVE content
- supporting compact/panel-style usage for monitor-constrained setups
- offering accessibility/color-blind signal support

For a deeper breakdown, see:

[Current Feature Snapshot](PMG-FEATURES.md)

* * *

## Current UI / Workflow Notes

Recent UI work has focused on making PMG more practical during live use.

The main board remains the fast triage surface. It is where users can scan pilots, affiliations, activity, signal state, and relevant quick-read context.

The standalone details window is the slower confirmation layer. It is where users can inspect a selected pilot more closely, review signal evidence, open zKill, or ignore a pilot/corporation/alliance.

Current workflow intent:

1. Copy a local pilot list.
2. Let PMG populate the board.
3. Scan the board for pilots or groups that matter.
4. Open details only when deeper review is useful.
5. Ignore known noise so future boards stay cleaner.
6. Jump to zKill when manual review is needed.

The app is meant to reduce context switching, not create a second cockpit.

* * *

## Cyno Signal and Evidence Notes

PMG includes cyno-related signal handling based on public/local cached intel.

Current cyno signal behavior can include:

- confirmed cyno module evidence from public loss victim item data
- inferred cyno capability from known cyno-capable hull history
- possible or weaker signal states where evidence is less certain
- unknown/no-signal handling where evidence is absent

Confirmed module evidence is treated differently from hull inference.

A cyno-capable hull is not proof that a cyno was fit. PMG tries to preserve that distinction so the signal is useful without overstating certainty.

* * *

## Ignore List

PMG supports typed ignore entries for:

- pilots
- corporations
- alliances

This is useful for removing known-friendly, known-alt, blue, test, or otherwise irrelevant rows from the board.

Ignore entries are ID-based. Display names may resolve for readability, but the ignore behavior is designed around the underlying IDs.

The details window can also be used to ignore a selected pilot, corporation, or alliance.

* * *

## Installation

1. Go to the [GitHub Releases page](https://github.com/SmokeyForged/Pitmasters-Grill/releases).
2. Download the latest PMG release ZIP.
3. Extract the ZIP into its own folder.
4. Open the extracted folder.
5. Run `PitmastersGrill.exe`.

Do not run PMG directly from inside the ZIP. Extract it first so the application can keep its files together correctly.

For a more detailed first-run walkthrough, see:

[First-Time Use Guide](FIRST-TIME-USE.md)

* * *

## Basic Use

A simple operating pattern for PMG is:

1. Launch PMG.
2. Let startup/update work complete.
3. Copy a local pilot list.
4. Let PMG populate and enrich the board.
5. Scan the board for pilots, groups, or signals that matter.
6. Open details when a specific pilot needs closer review.
7. Use ignore actions to remove known noise.
8. Open zKill only when deeper manual investigation is needed.

PMG is meant to make local intel easier to read, not to make decisions for the player.

* * *

## Policy / Trust Boundaries

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

PMG uses user-provided input and public/local cached context to help a human player understand local faster.

PMG is not affiliated with, endorsed by, sponsored by, or operated by CCP.

For the longer policy position, see:

[EVE Policy / ToS Compliance Position](EVE-TOS-COMPLIANCE.md)

* * *

## Limitations / Expectations

PMG is still in technical preview.

That means:

- behavior may continue to change between releases
- the UI may continue to evolve
- public data may be incomplete, stale, delayed, or unavailable
- inferred signals should not be treated as certainty
- confirmed module evidence depends on available public loss data
- PMG has no live fit visibility
- PMG does not know pilot intent
- diagnostics should be reviewed before public sharing
- feedback from active use is still shaping the product

This is normal for the current project stage.

PMG should not be treated as:

- a finished General Release product
- a perfect threat detector
- a replacement for player judgment
- an official CCP-approved tool
- a guarantee that a pilot is dangerous or safe
- a guarantee that public data is complete

The goal right now is practical technical-preview usefulness with honest expectations.

* * *

## Feedback

During tech preview, the most useful thing you can provide is honest feedback from real use.

Good feedback usually includes:

- PMG version
- what you were doing
- what you expected to happen
- what actually happened
- whether the issue was cosmetic, workflow-related, or disruptive
- screenshots if useful
- a reviewed diagnostics package if appropriate

Useful feedback areas include:

- board readability
- install/setup friction
- local-list parsing behavior
- false positives or false negatives in clipboard guardrails
- signal readability
- details-window usefulness
- ignore-list behavior
- information that feels useful versus noisy
- anything that feels questionable from an EVE policy/trust perspective

Issues can be opened on the GitHub repo when something needs tracking.

* * *

## Why PMG is free

PMG is intended to remain a free community tool.

No paywall. No required donation. No nonsense.

If PMG ends up being useful to you and you want to give something back, the preferred gesture is not tipping the developer. A better tribute would be to do something good for somebody else: help feed someone, cook for someone, donate to a local food pantry, or otherwise pass something useful forward.

That is more in the spirit of the project.

* * *

## Road Ahead

PMG is moving toward a stronger General Release candidate, but it is not there yet.

Near-term priorities are likely to remain focused on:

- better reliability
- cleaner setup
- better diagnostics
- clearer documentation
- stronger public trust posture
- cleaner release packaging
- signal-quality improvements
- continued UI readability improvements

The goal is not to do everything at once.

The goal is to keep the grill hot and make each release more useful than the last.
