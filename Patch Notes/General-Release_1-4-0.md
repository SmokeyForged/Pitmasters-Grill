# v1.4.0 Patch Notes

## Overview

v1.4.0 is a MainWindow cleanup, supportability, and release-hardening update for Pitmaster's Grill.

This release completes the staged MainWindow responsibility extraction work, finishes the MainWindow under-2,000-line gate, reduces shell/layout glue in the WPF host, and rolls in the SharpCompress dependency update needed to clear vulnerability checks cleanly.

## Added

### MainWindow Structure Hardening

- Completed the MainWindow responsibility extraction work that was tracked for issue `#54`.
- Added focused board-layout and board-column settings surfaces to reduce MainWindow-owned layout orchestration.
- Added focused shell/compact-window surface wiring to reduce MainWindow-owned window-state and compact-mode coordination.
- Added additional presenter/controller boundaries around Intel, diagnostics, board analysis, board sorting, session context, native input routing, and pilot detail support.

### Validation and Release Hardening

- Added release-ready validation coverage for the final MainWindow reduction pass with build, test, vulnerability, diff-check, and smoke verification.
- Updated SharpCompress to `0.48.0`, clearing the dependency vulnerability check that previously needed follow-up.

## Changed

- `MainWindow.xaml.cs` is now under `2,000` total lines.
- MainWindow now acts more as orchestration glue and less as the direct owner of diagnostics, Intel, board layout, shell behavior, and analysis presentation logic.
- Wrapper-only shell/layout/detail/support methods were collapsed so existing extracted services are called more directly.

## Fixed / Improved

- Reduced MainWindow maintenance risk by removing leftover passthrough glue that no longer carried independent behavior.
- Improved reviewability of remaining MainWindow responsibilities by keeping board/detail/Win32 boundaries in place while extracting the lower-risk support surfaces around them.
- Preserved existing PMG behavior while completing the line-count gate rather than forcing a riskier WPF rewrite.

## Validation

Validated during release preparation with:

- `dotnet build .\PitmastersGrill\PitmastersGrill.csproj`
- `dotnet test .\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj`
- `.\tools\dependencies\check-dotnet-vulnerabilities.ps1`
- `git --no-pager diff --check`
- `.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -ResponsivenessSeconds 30 -BoardPopulationTimeoutSeconds 300`
- `.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -SkipBoardPopulationSmoke -ResponsivenessSeconds 30`

## Notes for Users

This release is primarily about maintainability, shell cleanup, and release confidence rather than new gameplay-facing capability.

PMG continues to use user-provided input and public EVE data only.
