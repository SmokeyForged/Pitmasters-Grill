# PMG local smoke/regression validation



`Invoke-PmgSmokeTest.ps1` is a local validation script for Pitmasters Grill development and release-prep work.



It exists because build/test/diff checks are necessary but not sufficient for PMG. During the v1.4.0 MainWindow refactor, runtime-only issues were caught by manual smoke testing:



- a `StaticResourceExtension` startup failure after extracting a UserControl

- a render-then-stall failure after extracting settings coordination



This script automates the parts of that manual workflow that are practical to automate.



## Quick run



From the repository root:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1

```



Shorter responsiveness sample:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -ResponsivenessSeconds 30

```



## Full UI smoke



Run deeper UI Automation checks:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -ResponsivenessSeconds 30

```



The full UI smoke attempts to:



- navigate major tabs

- navigate Intel nested tabs

- toggle reversible settings and restore them

- adjust window opacity and restore it

- run safe diagnostics refresh buttons

- verify confirmable maintenance buttons are present

- keep PMG responsive and close normally



To also click confirmation-based maintenance buttons and cancel/No the dialog:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -ExerciseConfirmableActions

```



Use `-ExerciseConfirmableActions` only when you are comfortable with the script clicking those buttons. The script attempts to cancel/No resulting dialogs and warns if no dialog appears.



## Helpful options



Require a clean working tree:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -RequireClean

```



Enforce the current MainWindow line-count gate:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -EnforceLineGate

```



Write an inventory of discoverable UI Automation elements:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -UiInventory -SkipDotNet -ResponsivenessSeconds 5

```



Leave the app open after validation:



```powershell

.\scripts\Invoke-PmgSmokeTest.ps1 -KeepAppOpen

```



## What it checks



The script currently checks:



- current Git branch

- working tree status

- `MainWindow.xaml.cs` and `MainWindow.xaml` line counts

- explicit `dotnet test` path

- explicit `dotnet build` path

- `git --no-pager diff --check`

- WPF process launch from the built executable

- main window handle creation

- responsiveness samples over a configurable interval

- normal close via `CloseMainWindow()`

- optional UI Automation navigation/action checks

- recent PMG log discovery and tail capture on failure



Validation transcripts are written under:



```text

artifacts/smoke-tests/<timestamp>/pmg-smoke-test-transcript.txt

```



## What remains manual or needs future app hooks



This script is intended to reduce manual validation drift, but some checks may still require manual validation or future test hooks:



- visual polish, spacing, wrapping, and readability

- real external ESI/zKill/R2Z2 success paths

- destructive maintenance actions without a guaranteed confirmation/test mode

- OS File Explorer/dialog behavior

- exact user-facing visual comparison after large XAML refactors



## Future improvement



If UI Automation cannot consistently find controls by visible text, add stable `AutomationProperties.AutomationId` values to important PMG controls and update this script to prefer those IDs.



Root-level `dotnet test` and `dotnet build` are intentionally not used because this repository does not have a root solution/project file. The script uses explicit project paths instead.
## UI Automation selectors

The full UI smoke path prefers stable `AutomationProperties.AutomationId` selectors. Named WPF controls should keep their automation IDs aligned with their `x:Name` values so the script can find controls by intent instead of fragile screen coordinates or visible text.

When adding new buttons/settings that should be part of release validation, give the control a stable `x:Name` and keep the smoke script selector list updated.

## Board population fixture smoke

The full UI smoke can also populate the board from the checked-in large local-list fixture:

```text
test-fixtures/clipboard-large-local-list-valid.txt
```

This fixture is expected to contain 100 pilot names. The smoke script changes the clipboard to a unique sentinel value, then copies the fixture text to the clipboard while PMG is running, waits for `BoardPopulationStatusText` to reach `Board population complete` and requires the board summary to show at least the fixture line count.

Run:

```powershell
.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke
```

Skip only the board fixture portion:

```powershell
.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -SkipBoardPopulationSmoke
```

Adjust the timeout for slower systems or cold caches:

```powershell
.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -BoardPopulationTimeoutSeconds 300
```
