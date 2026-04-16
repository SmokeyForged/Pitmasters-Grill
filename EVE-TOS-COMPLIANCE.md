# Pitmasters Grill — EVE Policy / ToS Compliance Position

This document explains the intended policy posture of **Pitmasters Grill (PMG)** with respect to EVE Online third-party tool boundaries.

It is written to answer a practical question:

**How is PMG being designed to stay on the conservative side of CCP's published rules for third-party tools?**

This document is **not legal advice**, **not a guarantee of CCP approval**, and **not a certification of compliance**. It is a good-faith statement of design intent, operating boundaries, and current project posture based on publicly available CCP policy material.

---

## Important policy notice

CCP has publicly stated that it does **not** authorize or otherwise sanction third-party software in advance, and that players use third-party applications at their own risk.

At the same time, CCP has also said it may tolerate applications that simply enhance player enjoyment in a way that maintains fair gameplay.

PMG is therefore not presented as a CCP-approved tool.
It is presented as a project that is being intentionally designed to fit the most conservative reasonable reading of CCP's tolerated third-party companion-tool model.

In plain language:

**PMG is being built to help a player understand information faster, not to play EVE for them, modify the client, or create unfair advantage through automation.**

---

## Compliance posture in one sentence

PMG is being built as a **read-only, player-supporting intel companion** that helps a human player understand public or otherwise permitted context faster, without modifying the EVE client, automating gameplay, scraping restricted client data, or replacing player decision-making.

---

## What PMG is intended to be

PMG is intended to be:

* a desktop companion tool
* a pilot-intel summarization tool
* a board that helps users read local population context faster
* a support tool for human decision-making
* a free community project by project choice

PMG is **not** intended to act on the EVE client, play the game for the user, or create gameplay actions that the player did not personally perform.

---

## Core policy boundaries

The project is being shaped around a few hard boundaries.

### 1. No client modification

PMG is not intended to modify the EVE client, rewrite the user interface, inject into the game, or alter game content.

That means the project should remain outside any design approach that changes how the EVE client itself behaves.

### 2. No gameplay automation

PMG is not intended to automate gameplay.

It should not:

* control ships
* activate modules
* perform navigation
* automate market actions
* issue in-game commands
* play the game while the user is absent

PMG may help a user understand information faster, but the player remains the one who must interpret that information and act on it.

### 3. No macro or stored-input behavior

PMG is not intended to generate, relay, or orchestrate gameplay input patterns in a way that would function as macro behavior, stored rapid keystrokes, or similar input automation.

If a future feature would cross that line, it should be treated as out of bounds unless CCP policy clearly allows it.

### 4. No bypass of CCP access architecture

PMG is not intended to bypass login architecture, simulate direct game access, emulate CCP services, or provide an alternate access path into EVE.

It is a companion tool, not a substitute game client.

### 5. No cache scraping, packet inspection, or reverse-engineering behavior

PMG is not intended to scrape the EVE client cache, inspect packet traffic, sniff transmissions, reverse engineer client behavior, or derive information from restricted client/system paths.

If a feature idea depends on those methods, it should be treated as out of bounds.

### 6. No unfair hidden advantage

PMG is not being built to create advantage through concealed automation, client manipulation, prohibited data extraction, or background play.

Its role is to summarize context, not to covertly produce gameplay outcomes.

### 7. No unreasonable load on CCP systems

PMG should be operated in a way that avoids imposing unreasonable or disproportionate load on CCP-operated systems or related official services.

That means source usage, request pacing, and refresh behavior should be designed conservatively and revisited if scale increases.

---

## Data and source posture

PMG is being designed around public or otherwise permitted third-party-accessible information sources and project-local processing.

The operating model is:

* resolve relevant external context
* normalize it into a readable board
* show freshness where practical
* preserve the human player's role in judgment

The intended posture is to use information in a way that supports player understanding, not to extract unauthorized data from the EVE client.

Where CCP-provided APIs or official services are used, PMG should identify itself appropriately and operate with reasonable request hygiene.

---

## Human-in-the-loop principle

A central part of PMG's policy posture is that the user stays in control.

PMG can:

* summarize
* highlight
* sort
* surface patterns
* link out to deeper review

PMG should not:

* decide for the player
* execute actions for the player
* transform intel into automatic gameplay behavior

This matters because the tool is intended to remain a companion, not an operator.

---

## Free project note

PMG is intended to be released as a **free community tool**.

That is a project-values choice and a conservative public posture.
It should not be read as a claim that all paid or monetized third-party EVE applications are automatically disallowed in every form.

For PMG, the design goal is straightforward: keep the project simple, community-oriented, and clearly separated from any questionable "pay for advantage" framing.

---

## Project commitments

The PMG project intends to stay aligned with the following commitments:

* do not modify the EVE client
* do not automate gameplay
* do not introduce macro-like behavior
* do not scrape restricted client or packet data
* do not impose unreasonable load on official services
* do not misrepresent PMG as an official CCP product
* do not hide material behavior from users
* do not use the tool to replace player agency
* do not knowingly move into questionable territory without re-reviewing policy first

If a proposed feature creates uncertainty against these commitments, the safer path is to stop, review, and either redesign the feature or leave it out.

---

## Branding and relationship to CCP

PMG is an independent third-party project.

It is **not affiliated with, endorsed by, sponsored by, or operated by CCP**.

Any use of EVE-related names, references, or context should be handled in a way that respects CCP ownership and avoids implying official status.

---

## Living-document warning

CCP policies can change.
Interpretations can change.
Enforcement priorities can change.

Because of that, this document should be treated as a **living compliance-position note**, not a permanent one-time certification.

If PMG expands in a way that touches:

* authenticated EVE data
* deeper integrations
* overlays
* input handling
* monetization
* automation-adjacent workflows
* broader public scale

then this document should be revisited against the then-current CCP EULA, Terms of Service, third-party policies, developer rules, and any applicable API guidance.

---

## Conservative rule for future features

If a future feature causes the question

**"Is this still just helping the player understand information, or is it starting to play for them, extract prohibited data, or create hidden advantage?"**

then that feature deserves immediate policy review before it is built or released.

That is the safest line in the sand for this project.

---

## Summary

PMG's intended EVE policy posture is straightforward:

* read, summarize, and present useful context
* keep the player in control
* avoid client modification
* avoid automation
* avoid macro behavior
* avoid cache scraping and packet inspection
* avoid unfair hidden advantage
* avoid excessive load on official services
* remain an independent free community tool
* make no claim of CCP approval

In plain language:

**PMG should help a pilot think faster, not play EVE for them.**
