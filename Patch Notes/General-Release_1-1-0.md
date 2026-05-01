# Pitmasters Grill - General Release v1.1.0 Patch Notes

## Release Theme

Pitmasters Grill v1.1.0 is a usability-focused General Release update.

This release does not try to reinvent PMG. Instead, it makes the existing Board Mode workflow easier to discover, easier to escape, easier to read in compact overlays, and easier to operate with documented shortcuts.

The core v1.1.0 theme is:

> make the live board easier to understand at a glance without changing PMG's public-intel boundaries.

---

## Highlights

- Added clearer in-app documentation for the `Insert` Board Mode toggle.
- Added a temporary non-blocking Board Mode reminder when the user enters Board Mode.
- Added board-only compact display controls for:
  - Grill grid-line visibility
  - Grill board text size
  - Grill board font family
- Reduced Board Mode minimum height so PMG can be resized down toward a one-row live board.
- Audited and documented the user-facing keyboard shortcuts in the Help tab.
- Preserved the existing Board Mode toggle behavior, clipboard-driven workflow, and non-Board tabs.

---

## Board Mode Discoverability

Earlier PMG builds already supported Board Mode, but it was possible for users to enter that layout and not immediately realize how to get back out.

v1.1.0 addresses that by:

- documenting the `Insert` toggle directly in the Help tab
- reinforcing the toggle behavior in user-facing docs
- showing a brief in-app reminder when Board Mode is enabled

The reminder is intentionally temporary and non-blocking so it does not interfere with gameplay use or clipboard parsing.

---

## Compact Board Display Options

The Grill board now has optional compact display controls in Settings.

Users can now choose whether to:

- show or hide Grill grid lines
- reduce or increase board text size within safe bounds
- switch the board font family between the supported in-app options

These controls affect the Grill/Board presentation only. They are not intended to compress Analysis, Intel, Ignore List, or unrelated tabs.

---

## Board Mode Sizing Improvement

Board Mode can now be resized much smaller than the normal PMG layout.

The goal is to let users keep PMG as a more compact overlay when they want, including shrinking it toward a one-row board view, while still preserving a safer normal-mode minimum size.

This is an optional capability, not a forced default size change.

---

## Keyboard Shortcut Documentation

v1.1.0 includes a direct review of PMG's user-facing shortcuts so the Help tab better reflects the app's real behavior.

Documented shortcuts now include:

- `Insert` for Board Mode
- `Delete` to clear the current Grill board
- `Home` to reprocess clipboard input
- `Ctrl+Home` for window recovery/reset
- `Esc` behaviors for closing auxiliary windows or exiting PMG
- `Ctrl+S` in the Pilot Notes window

`Ctrl+Home` remains a recovery shortcut rather than a normal content-editing command. Depending on focus and Windows routing behavior, users may need to click the PMG window background or another non-editing area first.

---

## What Did Not Change

v1.1.0 does **not** change PMG's public-intel boundaries.

PMG still does not:

- read EVE client memory
- inspect network traffic
- use private ESI character scopes
- automate gameplay
- claim live certainty beyond public/local evidence

This release is about usability and operator clarity, not broader data access.

---

## Publishing Trust / Signing Status

Windows publishing-trust and signing options were documented and evaluated during the v1.1.0 cycle, but they were **not** implemented in this release.

That means:

- no Authenticode signing was added yet
- no SignPath integration was added yet
- no release automation signing changes were shipped yet

Publishing trust work remains deferred until GitHub Actions and release-workflow maturity are in a better place for secure signing operations.

---

## Upgrade Notes

No data migration or manual user action should be required for the v1.1.0 usability changes.

Existing users should mainly notice:

- clearer Board Mode behavior
- denser optional Grill display controls
- improved Help-tab shortcut coverage

---

## Related Docs

- [`../README.md`](../README.md)
- [`../FIRST-TIME-USE.md`](../FIRST-TIME-USE.md)
- [`../PMG-FEATURES.md`](../PMG-FEATURES.md)
- [`../HOW-IT-WORKS.md`](../HOW-IT-WORKS.md)
