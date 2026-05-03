# v1.3.0 Patch Notes

## Overview

v1.3.0 is a clarity, supportability, UI polish, and release-awareness update for Pitmaster's Grill.

This release improves public-data uncertainty messaging, signal/help references, board/session workflow, nested tab behavior, release checklist hygiene, dependency monitoring, and safe update awareness.

PMG’s update behavior in this release is intentionally awareness-only. PMG may check whether a newer stable GitHub release exists, but it does not automatically download, install, replace files, restart, or force updates.

## Added

### Update Awareness

- Added startup/splash update awareness for the latest stable PMG GitHub release.
- Added a manual update checker under Settings > Version.
- Added skipped-version support for startup update prompts.
- Added version comparison support for stable release tags such as `v1.3.0`.
- Added GitHub release-checking service for latest stable release lookup.
- Added tests for release version comparison and update-check decision behavior.

### Signal Reference and Help

- Added Signal Explanation reference material to Help/public documentation.
- Added signal reference support for explaining PMG signal meanings.
- Improved Help-area organization around signal meaning and user-facing explanation content.

### Board and Session Workflow

- Added independent saved position/size behavior for Normal and Board modes.
- Added global hotkeys for clear board and mode toggle.
- Improved Eve Session Context visibility by moving it under visible Board Analysis.
- Added board layout persistence improvements.
- Added board summary text builder extraction and tests.

### Public Data Visibility

- Added clearer public-data status wording for killmail intel updates.
- Added explicit messaging for provider-check failures.
- Added clearer messaging for partial local coverage windows.
- Added clearer messaging for missing local archive days.
- Added fixture-based tests for public-data status workflows.

### Release and Maintenance

- Added repeatable release checklist and artifact-verification support.
- Added dependency/vulnerability monitoring automation.
- Added dependency-monitoring/release-checklist documentation improvements.
- Added Dependabot updates for GitHub Actions workflow dependencies.

## Changed

- MainWindow delegates more user-facing text generation to focused services.
- Killmail intel status text now better distinguishes between:
  - current data,
  - missing local archive days,
  - partial requested coverage,
  - update failures,
  - and provider uncertainty.
- Settings > Version now includes a manual update-check section.
- Startup splash flow now briefly checks for PMG update awareness and continues if the check is unavailable.
- Intel and Settings nested tabs received dark-mode styling polish.
- Help tab scrolling behavior was improved.
- Session/context and global-hotkey behavior were refined.

## Fixed / Improved

- Fixed mouse wheel scrolling in the Help General tab.
- Polished nested tabs and Help scrolling behavior.
- Fixed a nullable warning in the MainWindow layout/session path.
- Improved user-facing clarity around public-data freshness and uncertainty.
- Improved supportability when zKill/public-data provider checks fail or are unavailable.
- Improved test coverage for release/version comparison logic.
- Improved test coverage for public-data workflow status behavior.
- Reduced MainWindow-owned formatting/counting logic with small, test-backed extractions.
- Updated GitHub Actions dependencies:
  - `actions/checkout`
  - `actions/setup-dotnet`

## Not Changed

- PMG does not automatically update itself.
- PMG does not download release assets automatically.
- PMG does not replace or modify its own executable files.
- PMG does not restart itself after checking for updates.
- PMG does not force users onto newer versions.
- PMG continues to use user-provided input and public EVE data only.

## Validation

Validated during development with:

- `dotnet test .\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj`
- `dotnet build .\PitmastersGrill\PitmastersGrill.csproj`
- Runtime smoke test for startup/splash update awareness
- Runtime smoke test for Settings > Version manual update check
- GitHub PR validation before merge

## Notes for Users

The update checker is informational. When PMG detects a newer stable release, it can open the GitHub release page so the user can manually download and install the update.

If the update check fails because GitHub is unavailable, the network is offline, or the request is blocked, PMG logs the failure and continues startup normally.

PMG signal/public-data outputs should be treated as public-evidence assistance, not hidden-state certainty.
