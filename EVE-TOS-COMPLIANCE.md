# EVE ToS Compliance and Client Boundaries

This document explains the safety boundaries Pitmaster's Grill follows around EVE Online.

This is not legal advice. It is the project’s design and operating posture.

PMG is built as a public-intel companion. It is intended to help users interpret copied local lists and public data without automating gameplay or touching private client internals.

---

## Short Version

PMG does:

- parse copied local-style text provided by the user
- use public zKill/ESI-style data where available
- cache public intel locally
- open public zKill pages
- show evidence summaries
- export diagnostics for troubleshooting

PMG does not:

- read EVE client memory
- inject into the EVE client
- automate gameplay
- send EVE input
- inspect network traffic
- scrape hidden client internals
- use private ESI character scopes
- bypass game mechanics
- claim hidden, cloaked, grid, or location certainty

---

## Input Boundary

PMG is clipboard-driven.

The user copies text, and PMG attempts to determine whether it looks like an EVE local-style pilot list.

PMG does not require:

- EVE client hooks
- file watchers for private client internals
- input automation
- memory inspection
- private character authentication

Clipboard guardrails are used to reject unrelated clipboard content.

---

## Public Data Boundary

PMG works with public evidence.

Examples include:

- public pilot/corp/alliance identifiers
- public zKill-known killmail data
- public ESI killmail retrieval where killmail ID/hash are available
- locally cached public-derived data

PMG does not require private ESI OAuth scopes.

PMG does not ask users to authorize a character.

---

## Local Chat / Session Context Boundary

PMG may use local, user-owned client-side context only for limited display context where implemented, such as identifying likely EVE session/system context from user-accessible local information.

This is not used to automate gameplay.

This is not treated as omniscient state.

Any displayed session context should be treated as contextual evidence and may be wrong, stale, or incomplete.

---

## No Automation

PMG is not a bot.

PMG does not:

- click buttons for the player
- control the EVE client
- issue commands
- automate targeting
- automate movement
- automate intel reporting into EVE
- automate combat decisions

PMG only assists review of information.

---

## No Client Memory Access

PMG does not read or inspect EVE client memory.

PMG does not inject code into the EVE process.

PMG does not hook rendering or client internals.

---

## No Network Inspection

PMG does not inspect, intercept, or decode EVE network traffic.

Public internet requests made by PMG are for public data enrichment and normal app functionality.

---

## No Private ESI Scopes

PMG is designed around public data and local user-provided text.

PMG does not require character login.

PMG does not use private ESI character scopes such as mail, contacts, location, skills, assets, wallet, or fleet.

---

## zKill and Public Killmail Limits

PMG uses public killmail-derived evidence where available.

Important limits:

- zKill may not have every killmail.
- Some killmails are posted late.
- Archive days can change after initial import.
- Live feeds can miss data outside PMG’s local window.
- Public ship observations do not prove current ship.

PMG’s freshness tools improve local public evidence. They do not create certainty.

---

## What PMG Claims

PMG may claim:

- this pilot was present in a copied list
- PMG resolved this pilot/corp/alliance from public data
- PMG has local public evidence for a recent ship observation
- PMG imported public killmail-derived context
- PMG sees public evidence of cyno/tackle/bait-related patterns
- PMG has stale/missing/partial local intel

PMG should not claim:

- this pilot is currently flying a specific ship
- this pilot is currently in a specific system unless supported by local context and clearly framed
- this pilot is cloaked
- this pilot is on grid
- this pilot is hostile by certainty
- public data is complete
- hidden/private game state is known

---

## Human Judgment Boundary

PMG supports human judgment.

The user remains responsible for interpreting evidence.

The app should make unknowns and limits visible rather than hiding them behind overconfident labels.

---

## Diagnostics Boundary

Diagnostics are intended for troubleshooting.

Diagnostics should not include:

- private credentials
- secrets
- unrelated local files
- private browser data
- raw killmail JSON
- unnecessary chat logs
- private EVE client data

Users should review diagnostics before posting them publicly.

---

## Reporting Concerns

If you believe PMG crosses a safety boundary, open an issue and describe:

- the feature or behavior
- why it may be risky
- what PMG did
- what you expected instead
- any relevant screenshots or diagnostics

Do not include private credentials or unrelated sensitive files.

---

## Maintainer Rule of Thumb

When in doubt:

```text
Prefer copied user-provided text.
Prefer public APIs.
Prefer explicit user actions.
Prefer local caching of public data.
Avoid automation.
Avoid hidden client state.
Avoid private scopes.
Avoid claiming certainty PMG does not have.
```

PMG should stay a companion tool, not a gameplay automation tool.
