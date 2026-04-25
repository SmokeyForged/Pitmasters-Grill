# Pitmasters-Grill — Technical Preview v0.9.0 Patch Notes

## Overview
Technical Preview **v0.9.0** is a major internal quality and usability milestone for Pitmasters-Grill.

This release focuses on three big areas:
- a large-scale **MainWindow refactor** to reduce monolithic code and improve maintainability
- a meaningful **UI refresh** to better organize settings and preserve key controls/status visibility
- the first **Ignore List MVP**, built around exact **Alliance ID** suppression

While not a full 1.0 feature-complete release, v0.9.0 substantially improves the app’s internal structure, operator workflow, and live-board usability.

---

## Highlights

### MainWindow Refactor
The core window logic was heavily refactored and broken into focused services/controllers.

This work:
- relocated over 1000 lines of logic out of `MainWindow`
- reduced `MainWindow` size by roughly half
- preserved behavior through staged regression-tested passes
- left `MainWindow` functioning much more as a shell/wrapper instead of a monolithic controller

New extracted service/controller boundaries include areas such as:
- row enrichment application
- retry policy and retry orchestration
- row detail formatting and detail pane control
- appearance/settings handling
- board status control
- row processing
- pass/finalize orchestration
- clipboard/board entry orchestration
- intel banner handling
- timing marker tracking
- composition root wiring

This is a foundational maintainability improvement for future development.

---

## New Features

### Ignore List MVP (Alliance ID)
Added the first Ignore List implementation focused on **exact Alliance ID** matching.

Included in this MVP:
- top-level **Ignore List** tab
- operator-managed alliance ID list
- manual add/save/clear workflow
- persistence of ignored alliance IDs
- immediate removal of matching current-board rows
- suppression of rows once alliance affiliation is known
- pilot-detail **Ignore Alliance** action for live workflow use

Behavior notes:
- matching is based on exact numeric **Alliance ID** only
- this MVP does **not** yet include corp-level or pilot-level ignore
- ignored rows are removed safely, including selected-row/detail cleanup when needed

---

## UI Updates

### Settings Layout Refresh
The Settings area was reorganized into cleaner sub-tabs:
- **General**
- **Version**
- **Diagnostics**
- **Intel Config**

This improves discoverability and reduces clutter in the main settings flow.

### Top Bar / Persistent Controls Refresh
Updated the main UI layout so that:
- **Clear** persists in compact mode
- key board status and last-refresh visibility persist more naturally
- the intel banner has more room across the top

### Version Info Page
The version page was cleaned up so the only thing intended to change each release is the version number, with a direct GitHub repo link available from the UI.

---

## Improvements

### Board/Data Pipeline
- alliance ID carriage was added through the relevant model/cache pipeline needed to support exact ignore behavior
- current-board ignore application was wired into live board behavior
- post-enrichment ignore removal is now supported when alliance data resolves later

### Repo / Codebase Cleanup
- removed unused converters identified during cleanup work
- stabilized the refactored window structure after multiple regression-tested passes
- cleaned up stale structure from the older monolithic window implementation

### Workflow / Operator Quality
- improved ability to remove known-friendly alliance noise from the board during active use
- strengthened the architecture so future feature work is less likely to reintroduce `MainWindow` bloat

---

## Fixes

### Ignore List Performance Correction
Initial ignore-list integration introduced too much board-population overhead.

This was corrected by narrowing ignore application so that:
- full-board ignore application happens only when appropriate
- per-row checks are limited to the row that actually changed when possible

The result is a much more reasonable performance profile while keeping Ignore List behavior functional.

### Board/Detail Safety on Removal
Fixed the row-removal flow so that when an ignored row is removed:
- selection is safely cleared if necessary
- stale detail-pane state is not left behind

---

## Known Scope Boundaries
v0.9.0 intentionally does **not** yet include:
- corp-level ignore
- pilot-level ignore
- color blind mode support
- final transparent overlay/panel mode

These remain future-facing items beyond this technical preview milestone.

---

## Upgrade Notes
If you are updating from earlier technical previews:
- settings/UI organization will look different
- the app architecture underneath `MainWindow` has changed significantly
- Ignore List data is now persisted separately for alliance-ignore behavior

---

## Release Summary
**Technical Preview v0.9.0** is a major structural and usability release.

It delivers:
- a substantially cleaner architecture
- a better-organized UI
- and the first high-value Ignore List MVP for live board triage

This release is less about flashy expansion and more about making PMG more sustainable, more usable in combat-time workflows, and better positioned for future growth.
