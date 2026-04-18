# Pitmasters Grill v0.8.3 Patch Notes

## Overview
v0.8.3 is a cleanup and usability release focused on two things:

- a major `MainWindow` refactor to reduce monolithic code and improve maintainability
- a small UI/settings layout refresh to make the app cleaner to navigate in daily use

This is not a feature-heavy release. It is a stability and quality release meant to put PMG on better footing for future work.

## Highlights

### MainWindow refactor
- Completed a substantial refactor of `MainWindow`
- Relocated over **1,000 lines of code** out of the main window into focused services/controllers
- Reduced `MainWindow` to a much thinner shell/wrapper responsible primarily for WPF lifecycle, event wiring, and top-level UI ownership
- Split major responsibilities into dedicated components, including:
  - board population entry flow
  - row processing
  - pass/finalize orchestration
  - retry orchestration
  - detail pane control
  - settings/appearance handling
  - status handling
  - intel banner handling
  - timing marker tracking
  - composition root / dependency assembly
- Cleaned up leftover constructor/composition sludge and removed dead or no-longer-needed retained fields

### UI refresh
- Broke up the old Settings tab into clearer sections:
  - **General**
  - **Version**
  - **Diagnostics**
  - **Intel Config**
- Moved version information into its own tab
- Moved diagnostics tooling into its own tab
- Moved intel-related configuration into **Intel Config**, including:
  - max killmail age
  - enable local killmail DB pull
  - killmail data path controls
- Moved **Clear** next to **Compact Mode** so it remains available in compact view
- Moved board status text and last refreshed timestamp into the persistent strip above the main tabs so they remain visible more consistently
- Freed up more horizontal space for the intel banner across the top of the app
- Updated the Version tab so the GitHub repo link is clickable from inside the app

## Technical notes
- Multiple refactor passes were validated with repeated regression testing before finalizing the new structure
- Startup/runtime issues introduced during composition-root extraction were isolated and corrected by preserving startup-sensitive construction order before `InitializeComponent()`
- Cleanup/stabilization passes were completed after the refactor to confirm the remaining `MainWindow` weight was legitimate shell ownership rather than hidden dead code

## User impact
- No major workflow changes
- No major new end-user feature set in this release
- Cleaner settings organization
- Better compact-mode usability
- Better maintainability for future updates

## Version
**Technical Preview v0.8.3**
