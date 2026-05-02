# Pitmasters Grill - General Release 1.2.0

## Theme

Pitmasters Grill `1.2.0` is a support, maintainability, and validation release.

This release does not try to add new EVE intel sources or expand PMG's public-data boundaries. Instead, it makes PMG safer to maintain, easier to validate, and better prepared for future fixes and improvements.

Core theme:

> make PMG easier to change without making it easier to break.

---

## Headline Changes

- Added a Windows GitHub Actions build/test workflow.
- Added the `PitmastersGrill.Tests` automated test project.
- Expanded deterministic automated coverage to `98` tests at the time these notes were written.
- Reduced `MainWindow.xaml.cs` through focused controller extractions.
- Added regression coverage for extracted controllers and important existing service behavior.
- Preserved PMG's existing public-intel model and EVE client safety boundaries.

---

## MainWindow Maintainability Work

`MainWindow.xaml.cs` remains the WPF shell for live UI events, named controls, lifecycle wiring, async population flows, and provider/cache/database orchestration.

The 1.2.0 work moved lower-risk, behavior-preserving logic into focused services so the main window is easier to reason about.

Extracted slices include:

- board display settings coordination
- board column layout validation and persistence helpers
- window layout and snapshot logic
- settings-tab mapping helpers
- Analysis-tab deterministic summary logic

This was intentionally not a broad UI rewrite. PMG did not move to MVVM, Avalonia, or a new application architecture in this release.

---

## Test and CI Improvements

The repository now includes:

- a dedicated `PitmastersGrill.Tests` project
- Windows-only CI build/test validation
- deterministic tests for controller logic and key non-UI services

Covered areas include examples such as:

- clipboard payload guardrails
- local-list parsing
- settings persistence behavior
- ignore-list behavior
- window layout helpers
- board display/layout controllers
- settings-tab helpers
- Analysis-tab summary helpers
- freshness/status formatting helpers
- zKill URL construction helpers

These tests are not a replacement for manual PMG smoke testing, but they provide a stronger safety net for future changes.

---

## User-Facing Impact

For most users, PMG should feel the same.

The point of this release is not to change the way pilots are analyzed. The point is to make the app safer to support and less fragile when future updates are made.

Expected user-visible impact:

- no new workflow required
- no new private data access
- no new EVE client integration
- no new automated gameplay behavior
- continued Windows desktop app behavior
- improved confidence in future maintenance through CI and tests

---

## What Did Not Change

This release does **not** claim:

- new user-facing EVE intel features
- new private-data access
- Linux, Proton, or macOS automated CI coverage
- a migration to MVVM, Avalonia, or a new application architecture
- complete proof that PMG is bug-free

PMG remains a Windows-targeted WPF desktop application built around copied local lists, public data enrichment, local caching, and explicit evidence limits.

---

## Validation Notes

The 1.2.0 release line added a repeatable validation foundation:

- local build validation
- local automated test validation
- GitHub Actions Windows build/test validation
- manual smoke testing for UI behavior that CI cannot honestly prove

Manual validation remains important for:

- app startup
- clipboard/local-list workflow
- Grill board behavior
- Analysis tab rendering
- Settings tab behavior
- window layout persistence/reset behavior
- Board Mode / compact mode
- diagnostics and support workflows

---

## Upgrade Notes

No special migration steps are expected for normal users.

Recommended upgrade path:

1. Close PMG if it is running.
2. Download the `v1.2.0` release package.
3. Extract it to a folder you control.
4. Launch `PitmastersGrill.exe`.
5. Confirm the app starts and your normal workflow still behaves as expected.

As always, diagnostics may be requested if a startup, settings, freshness, or board behavior issue appears after upgrade.

---

## Related Docs

- [`../README.md`](../README.md)
- [`../HOW-TO-NAVIGATE-THIS-REPO.md`](../HOW-TO-NAVIGATE-THIS-REPO.md)
- [`../DEVELOPER-NOTES.md`](../DEVELOPER-NOTES.md)
- [`../PMG-FEATURES.md`](../PMG-FEATURES.md)
- [`../HOW-IT-WORKS.md`](../HOW-IT-WORKS.md)
