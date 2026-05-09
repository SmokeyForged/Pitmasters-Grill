# v1.4.0 Patch Notes

Pitmasters Grill v1.4.0 is a major maintainability, release-readiness, update-awareness, validation, and supportability release.

This release is not just a refactor release. It completes the staged `MainWindow.xaml.cs` responsibility extraction goal, but it also introduces manual update-check support, centralizes release version metadata, improves README/current-release wording, documents and implements guarded release-helper tooling, adds automated smoke regression coverage, extracts diagnostics and support controllers, improves release validation, and resolves a dependency vulnerability check failure.

PMG remains the same kind of tool: a lightweight public-data EVE local-intel assistant. v1.4.0 improves how PMG is maintained, validated, packaged, updated, and supported without adding private-data access, gameplay automation, EVE client memory inspection, traffic inspection, hidden-state certainty, or automatic self-install behavior.

## Highlights

- Completed the v1.4.0 MainWindow responsibility extraction goal.
- Reduced `MainWindow.xaml.cs` below the 2,000 total-line gate.
- Extracted multiple focused controllers/surfaces from MainWindow.
- Added manual update-check support through a dedicated update-check controller.
- Added current-release/update-awareness surfaces without automatic self-install behavior.
- Centralized PMG release version metadata through `PmgReleaseVersion`.
- Removed redundant README release-status wording.
- Documented release automation design and guardrails.
- Documented canonical release artifact naming.
- Added release-readiness validation tooling.
- Added release package preparation tooling.
- Added GitHub release draft helper tooling.
- Added automated PMG smoke regression coverage.
- Extracted diagnostics action handling into a focused controller.
- Updated SharpCompress from `0.47.3` to `0.48.0` to clear the dependency vulnerability check.
- Updated app and repository release metadata from `1.3.0` to `1.4.0`.
- Added v1.4.0 general release notes and updated current-release documentation.
- Preserved PMG’s safety posture: public data only, no gameplay automation, no private ESI data, no automatic update installation, and no automatic release publication.

## User-Facing Improvements

### Manual Update Check Support

v1.4.0 adds manual update-check support.

PMG can now help users check for newer stable PMG releases from the app without downloading, installing, replacing files, or restarting automatically. This improves update awareness while keeping the user in control.

This release does **not** add automatic self-update installation.

The intended boundary is:

- PMG may tell the user that a newer stable release exists.
- PMG may point the user toward release information.
- PMG does not download and replace itself.
- PMG does not restart itself into a new version.
- PMG does not silently install updates.
- The user remains responsible for choosing when and whether to upgrade.

### Settings / Version Awareness

The Settings area now includes manual version/update-check behavior as part of the app’s supportability surface.

This gives users a clearer path to answer:

> Am I on the current stable PMG release?

without turning PMG into a self-updating application.

## Current Release Visibility

PMG now identifies v1.4.0 as the current release line.

Updated current-release surfaces include:

- app release metadata
- README current-release link
- README repository status text
- Patch Notes index
- PMG-FEATURES current release line
- v1.4.0 general release notes

Historical v1.3.0 release notes were intentionally preserved.

## MainWindow Responsibility Extraction

v1.4.0 completes the major MainWindow cleanup target tracked for this release.

`MainWindow.xaml.cs` had accumulated too many responsibilities across board layout, diagnostics, Intel controls, manual update checking, shell behavior, hotkeys, detail-pane/window routing, board population, native input, release/support UI glue, and window-layout state. The v1.4.0 work moved much of that behavior into focused surfaces/controllers so MainWindow can act more like an orchestration shell rather than a single oversized owner of everything.

### Result

- `MainWindow.xaml.cs` is now under the 2,000 total-line gate.
- The cleanup was completed through staged, reviewable passes.
- The final pass avoided broad XAML rewrites.
- The final pass avoided risky WndProc surgery.
- The last reduction was achieved through safer surface extraction and wrapper-glue cleanup.
- Future work should preserve this boundary and avoid moving responsibilities back into MainWindow without a clear reason.

### Extracted / Consolidated Areas

The release includes focused extraction and cleanup across these areas:

- manual update-check controller
- diagnostics action controller
- diagnostics support surface wiring
- diagnostics/cache maintenance composition
- Intel support surface wiring
- Intel update/status/details presentation
- board sort coordination
- watched-first board ordering
- pilot detail pane/window lifecycle coordination
- detail-window placement decisions
- EVE session-context refresh and Analysis-tab display coordination
- board population orchestration
- retry and clear-board flow
- native input lifecycle for clipboard listener and global hotkeys
- MainWindow shell mode coordination
- compact-window behavior
- board layout and column settings behavior
- window layout restore/save/reset behavior
- remaining MainWindow passthrough wrapper cleanup

The practical result is a more maintainable UI shell with clearer ownership boundaries.

## Manual Update Check Controller

v1.4.0 extracts manual update-check behavior into a dedicated controller.

This keeps update-awareness logic out of MainWindow and makes the update-check flow easier to test, reason about, and evolve later.

This work supports PMG’s release-awareness direction while preserving an important boundary:

- PMG can help the user know that a newer release exists.
- PMG does not automatically install updates.
- PMG does not replace itself.
- PMG does not silently modify release files.
- The user remains in control of downloading and applying releases.

## Diagnostics Improvements

Diagnostics action handling was extracted into a focused diagnostics action controller.

Diagnostics/support cleanup includes clearer ownership around:

- diagnostics action routing
- provider-health refresh behavior
- cache-stat refresh behavior
- diagnostics/cache maintenance action composition
- diagnostics support surface wiring
- status messaging around support actions

This reduces MainWindow responsibility and makes diagnostics behavior easier to validate independently.

## Intel Support and Status Surfaces

Intel support wiring was consolidated so MainWindow no longer directly owns all support-surface controller composition.

This includes cleaner routing around:

- Intel update banner behavior
- Intel status/details presentation
- Intel maintenance and configuration actions
- live zKill/R2Z2-related support controls
- background repair toggle behavior
- Intel support surface state routing

The goal is clearer separation between the UI shell and the Intel/support surfaces that own those behaviors.

## Board, Detail, and Session Coordination

Several board-adjacent coordination seams were moved out of MainWindow or reduced to thinner event routing.

This includes:

- manual board sort state
- watched-first ordering
- board sort indicators
- selected/displayed pilot detail resolution
- detail pane show/hide routing
- detail-window lifecycle coordination
- detail-window placement behavior
- watch action UI state
- ignore action UI state
- pending session-context startup apply
- EVE session-context refresh/apply flow
- Analysis-tab session-context display updates

These changes are intended to make future PMG behavior changes easier to reason about and safer to validate.

## Board Population, Native Input, and Window Layout

v1.4.0 moved several higher-risk coordination paths into more focused ownership while keeping WPF/Win32 boundaries conservative.

This includes:

- board population orchestration
- board retry flow
- clear-board/reset glue
- clipboard listener lifecycle routing
- global hotkey lifecycle routing
- native input shell coordination
- window layout restore/save/reset behavior

The goal was not to rewrite PMG’s input model. The goal was to reduce MainWindow responsibility while preserving the existing clipboard-driven workflow and recovery behavior.

## Board Layout and Display Settings

Board layout behavior was moved behind a dedicated board-layout surface.

This includes the surrounding glue for:

- board column visibility
- saved column layout application
- canonical/default column layout application
- board display settings
- grid-line display behavior
- text-size and font selection routing
- show-all-columns behavior
- reset-board-columns behavior
- reset-board-layout behavior
- column resize/reorder persistence
- fit-visible-columns scheduling

The board should behave the same for users, but the code now has clearer ownership around layout and display settings.

## Shell, Compact Window, and Window Layout Behavior

Shell and compact-window behavior was extracted into a focused shell surface.

This includes routing and coordination for:

- compact mode application
- tab selection shell updates
- board footer visibility
- board mode hint display
- window minimum-size updates
- minimize / maximize / restore shell behavior
- window layout reset routing
- keyboard-shell flow such as Escape handling and related shell hotkey behavior

The raw WPF/Win32 boundary was intentionally kept conservative. v1.4.0 improves ownership without turning the shell pass into a risky platform-boundary rewrite.

## Automated Smoke Regression

v1.4.0 adds automated PMG smoke regression coverage.

This is important because build/test/diff checks are necessary but not enough for a WPF desktop app. During the v1.4.0 MainWindow refactor, runtime-only behavior needed validation beyond unit tests.

The smoke regression work helped validate behavior such as:

- app launch and normal close
- tab navigation
- clipboard-triggered board population
- Analysis tab board reflection
- board layout/display settings
- compact/shell settings
- window minimize/maximize/restore behavior
- reset window layout
- diagnostics refresh buttons
- Intel configuration toggles
- responsiveness checks

This improves confidence that refactors preserve user-visible behavior.

## Release Version Metadata

v1.4.0 centralizes PMG release version metadata.

The app version now flows from `PmgReleaseVersion` in the project file, with related version fields deriving from that value.

Updated metadata includes:

- `PmgReleaseVersion`
- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`

This reduces version drift and gives release tooling a canonical version source.

## README and Current Release Cleanup

v1.4.0 removes redundant release-status wording and updates current-release documentation.

This includes:

- README current-release reference updated to v1.4.0
- README repository status updated for the v1.4.0 release line
- Patch Notes index updated to v1.4.0
- PMG-FEATURES current release line updated to v1.4.0

Historical v1.3.0 release notes were intentionally preserved.

## Release Automation Design and Helpers

v1.4.0 adds the foundation for a safer, more repeatable release process.

This includes:

- release automation design documentation
- canonical release artifact naming documentation
- release readiness check helper
- release package preparation helper
- release draft helper

The release-helper path is intentionally guarded:

- `Check-ReleaseReadiness.ps1` validates version metadata, release naming, README state, patch notes, diff hygiene, build, and tests.
- `Prepare-ReleasePackage.ps1` packages an existing publish output folder and prepares local release output.
- `New-ReleaseDraft.ps1` validates local package output and can create a GitHub release draft only when explicitly told to do so.
- Publishing remains a deliberate operator action.

Important boundary:

- GitHub Actions currently cover build/test and dependency vulnerability validation.
- General-release packaging and draft creation are currently handled by local helper scripts.
- The current flow does not automatically publish releases.
- The current flow does not automatically create tags.
- The current flow does not automatically upload artifacts unless the operator explicitly invokes the draft helper.

This keeps PMG’s release path reviewable and hard to trigger accidentally.

## Canonical Release Artifact Naming

v1.4.0 documents the forward release naming convention.

Going forward, general release assets should use the full semantic version in the package name.

Canonical pattern:

```text
Release title: Pitmasters Grill <major>.<minor>.<patch>
Tag: v<major>.<minor>.<patch>
ZIP asset: PMG_General-Release_v<major>.<minor>.<patch>.zip
