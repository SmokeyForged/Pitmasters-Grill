# PMG Product Maturity Hardening — Engineering History

Date: 2026-08-14

Status at pause: Phase 4 cleanup complete on the feature branch; final GitHub CI and a short cleaned-head Windows startup/exit smoke remain before PR #104 can leave Draft.

## Why this work happened

PMG had reached a point where its operational discipline was stronger than parts of its internal architecture. The hardening goal shifted from proving that PMG could acquire more engineering machinery to proving that another engineer could understand, remove, and change pieces without being afraid of hidden ownership or lifecycle consequences.

The working progression was:

> Can we make this work? → Can we make this safe? → Can we make this boring?

The refactor principle adopted for this sequence was:

> Reduce ambiguous ownership and delete transitional code before inventing new abstractions.

## Starting posture

The sequence began after deterministic shutdown hardening had landed. PMG already had strong validation, evidence, and shutdown discipline, but several structural pressure points remained:

- duplicate/transitional runtime paths;
- a very large `MainWindow.xaml`;
- mutable board state owned directly by `MainWindow`;
- runtime construction split among `App`, `MainWindowCompositionRoot`, and `MainWindow`;
- large background-intel and R2Z2 services still awaiting later decomposition.

The accepted seven-phase roadmap was:

1. remove duplicate/transitional runtime code;
2. decompose `MainWindow` XAML;
3. extract current-board session ownership;
4. centralize application composition and service lifetimes;
5. decompose `BackgroundIntelUpdateService` behind its facade;
6. reassess/simplify R2Z2 live-feed responsibilities;
7. split `MainWindowAppearanceController` by responsibility.

This history record closes the night after Phase 4 implementation/cleanup, before beginning Phase 5.

---

## Phase 1 — Remove duplicate and transitional runtime code

Engineering issue: GitHub #92  
PR: #99 — `Remove duplicate and transitional runtime code`

Primary outcomes:

- removed the duplicate MainWindow-owned `SystemTrayIconService`;
- retained `PmgTrayIconService` as the single application-owned notification-area implementation;
- removed disconnected R2Z2 direct-derived-evidence/seen SQL helpers after confirming active sequence processing delegates through `KillmailIncrementalImportService`;
- retained active R2Z2 feed-state/checkpoint persistence and `live_killmail_seen` reporting;
- removed the superseded untracked background live-feed startup path;
- added runtime-ownership regression coverage.

### Important defect discovered during acceptance

Manual Exit testing exposed a pre-existing deterministic-shutdown deadlock that CI had not detected.

The deadlock cycle was:

- a background intel worker held `BackgroundIntelUpdateService._sync`;
- `PublishLocked()` raised `StatusChanged` while the lock was held;
- the MainWindow status path synchronously called WPF `Dispatcher.Invoke`;
- during Exit, the UI thread entered deterministic shutdown and tried to acquire the same `_sync`;
- background worker waited for the UI dispatcher while the UI thread waited for `_sync`.

The fix moved the off-thread UI handoff to non-blocking dispatcher scheduling and added regression coverage forbidding synchronous `Dispatcher.Invoke` in the intel-status path.

The result was stronger than the original deletion objective: Phase 1 removed dead runtime code and converted a hidden lock/UI lifecycle defect into a known, tested contract.

---

## Phase 2 — Decompose MainWindow XAML

Engineering issue: GitHub #97  
Jira story: PMG-3  
PR: #100

Merged main after Phase 2: `1e1e44d6e99ae10c06367b20cdab96a7f3005f01`

Primary outcomes:

- extracted the large `Window.Resources` block into an explicit resource dictionary while preserving window-scope runtime theme ownership;
- extracted the Analysis visual tree into a focused `AnalysisView`;
- kept business/session behavior outside the view code-behind;
- materially reduced `MainWindow.xaml` without forcing Grill/Settings/Help into artificial child views merely for line-count reduction;
- added structural and runtime WPF regression coverage.

### Runtime resource-scope defect discovered

The first Windows acceptance run failed during `MainWindow.InitializeComponent`:

`XamlParseException: Cannot find resource named 'SettingsLabelStyle'.`

The style still existed. The defect was lookup timing: the extracted `AnalysisView` used `StaticResource` references to a resource owned by the parent window's merged dictionary. The child `UserControl` parsed before attachment to the parent resource tree.

Correction:

- changed the inherited Analysis style references to `DynamicResource`;
- added an STA/WPF runtime test that actually constructs `AnalysisView`, so the same failure class is now CI-detectable.

A secondary failed-startup shutdown weakness was also discovered: `MainWindow.OnClosing` can dereference `_mainWindowShellSurface` when initialization did not complete. That was deliberately split out rather than smuggled into the XAML refactor:

- GitHub #101 — guard MainWindow closing when initialization fails;
- Jira PMG-9 — Bug under the Product Maturity Growth epic.

Phase 2 then passed full Windows acceptance: startup, all tabs, Analysis layout, theme/resource transitions, Board Mode, normal Exit, tray restore, and tray Exit.

---

## Phase 3 — Extract current-board session ownership

Engineering issue: GitHub #98  
Jira story: PMG-4  
PR: #103

Merged main after Phase 3: `bb71fa33cbcbe30f58fc91ca3a1de6acb6e4b5a8`

Before moving mutable board ownership, a deterministic cross-layer proof was added:

controlled killmail JSON → real SQLite incremental import → real derived-evidence repositories → `PilotBoardRow` → user-visible/domain classification.

The integration proof drives a row to the same Bait classification consumed by the application. This became the safety net required before a medium structural extraction.

Primary outcomes:

- introduced `CurrentBoardSession` as the single mutable owner of active board rows;
- moved processing generation / stale-generation rejection into the session;
- moved row subscription lifecycle into the session;
- centralized replace/remove/batch-remove/reorder/clear+invalidate behavior;
- removed `_currentRows`, `_processingGeneration`, and MainWindow-owned row-subscription plumbing;
- changed sorting and board-clear paths so policy is calculated outside but mutation occurs through the session;
- kept DataGrid selection, presentation, repositories, and presenters out of the session.

Windows acceptance proved the intended state churn:

populate → enrich → analyze → reorder → clear/invalidate → wait for stale work → repopulate → analyze again → deterministic shutdown.

No stale row reappeared after clear, and a new generation populated normally afterward.

---

## Phase 4 — Centralize application composition and service lifetimes

Engineering issue: GitHub #93  
Jira story: PMG-5  
PR: #104  
Branch: `refactor/application-composition`

Phase 4 started from main `bb71fa33cbcbe30f58fc91ca3a1de6acb6e4b5a8`.

### Problem

Runtime construction was split across three places:

- `App.xaml.cs`;
- `MainWindowCompositionRoot`;
- the `MainWindow` constructor.

That made locally understandable code globally ambiguous: it was harder than necessary to answer who creates a dependency, how long it lives, whether an equivalent instance already exists, and who owns shutdown/disposal.

### Implemented lifetime boundary

`ApplicationCompositionRoot` now provides the normal application composition path.

Application lifetime includes:

- one shared `AppSettingsService`;
- killmail database bootstrap/metadata state;
- write/import coordination;
- R2Z2 live feed;
- Today's/Historical freshness services;
- `BackgroundIntelUpdateService`.

The startup metadata path and `BackgroundIntelUpdateService` now share one `KillmailDatasetMetadataRepository` instance.

Application-composed window lifetime includes the non-control graph:

- local application DB bootstrap/repositories;
- resolver/stats providers;
- board population processors/controllers;
- settings/layout/shell pure controllers;
- `MainWindowDiagnostics`;
- `EveSessionContextService`;
- ignore coordination;
- zKill/browser helpers.

`MainWindow` retains only concrete window/control-support ownership such as:

- WPF surfaces and control-bound presenters;
- dispatcher timers;
- native input attachment;
- detail-window lifecycle/placement;
- `CurrentBoardSession`;
- small stateless UI-support helpers.

No DI container, service locator, MVVM rewrite, or generic lifetime framework was introduced.

### Startup ordering preserved

The active path remains explicitly UI-first:

1. splash;
2. shared settings service;
3. fail-open update awareness;
4. application runtime composition;
5. killmail DB bootstrap/startup metadata;
6. window non-control runtime composition;
7. composed MainWindow construction;
8. window show;
9. first `ContentRendered`;
10. deferred archive/R2Z2/background historical repair startup.

### Runtime acceptance

Exact runtime-validated head before cleanup:

`6341827209130fab90700eefa0ab9cf92d81bf9b`

Results:

- Release build: PASS, 0 warnings / 0 errors;
- splash/MainWindow startup: PASS;
- one PMG process / one tray icon;
- first render occurred before background start;
- archive start count: 1;
- R2Z2 enabled, start count: 1;
- background historical repair enabled with 30-second delay, schedule/start count: 1/1;
- no duplicate runtime/service evidence;
- Grill/Analysis/Intel/Settings: PASS;
- representative board population/enrichment: PASS;
- deterministic normal Exit: PASS, exit code 0;
- tray restore/tray Exit: PASS, no orphan process;
- no new Windows Application Hang/Application Error/.NET Runtime/WER events;
- no active PMG `errors.log` created.

Three archive-not-published boundary warnings were observed and classified as expected.

### Legacy deletion pass

Codex then removed the now-dead transitional path on the same feature branch.

Cleanup commit:

`d186c167c7ce4f9106fd544f56aafd2866dbe9e4` — `Remove legacy MainWindow composition path`

Cleanup results:

- legacy one-argument `MainWindow` constructor removed;
- duplicate MainWindow-local retry-delay constant removed;
- `MainWindowCompositionRoot.cs` deleted;
- active path confirmed as `ComposeNormalRuntime` → `ComposeMainWindowRuntime` → two-argument `MainWindow`;
- absence regression guard added;
- two existing ownership tests adjusted to read the active composed partial constructor;
- changed files: 5;
- diff stat: 34 insertions / 563 deletions;
- `git diff --check`: PASS;
- Release build: PASS, 0 warnings / 0 errors;
- full tests: PASS, 256/256;
- normal push to `origin/refactor/application-composition`: PASS.

This is the exact code state immediately before this history entry was added.

---

## Governance / planning state at pause

GitHub:

- PR #104 remains OPEN and DRAFT;
- engineering issue #93 remains open through the PR;
- branch: `refactor/application-composition`;
- pre-history code head: `d186c167c7ce4f9106fd544f56aafd2866dbe9e4`.

Jira:

- PMG-5 is **In Review**;
- it must not move to Done until Phase 4 is actually merged.

Intentional pause:

- do not begin Phase 5 tonight;
- do not merge #104 as part of this wrap-up;
- do not mark #104 Ready until the cleaned head has its final validation gate.

### Remaining Phase 4 gate for next session

1. Re-read GitHub `main`, PR #104, and branch head before doing anything.
2. Confirm the history-only commit is the only change after cleanup commit `d186c167...`.
3. Confirm exact-head GitHub CI is green on the current branch head.
4. Run one short cleaned-head Windows smoke:
   - startup reaches MainWindow;
   - one process / one tray icon;
   - background starts remain single and after first render;
   - normal Exit completes with deterministic shutdown and exit code 0;
   - no new PMG/Windows runtime errors.
5. Update PR #104 with final cleaned-head evidence.
6. Mark PR #104 Ready for review.
7. Leave merge to the operator.
8. After merge, verify main/issue closure and move PMG-5 to Done.

Only then should Phase 5 begin.

---

## Deferred roadmap

Next engineering phase after Phase 4 merge:

- GitHub #94 — decompose `BackgroundIntelUpdateService` behind its existing facade.

Do not start by inventing new architecture. Rehydrate the latest merged code, re-read the Phase 5 GitHub issue and Jira story, and preserve the composition/lifetime contract established here.

Later phases remain:

- R2Z2 responsibility reassessment/simplification;
- `MainWindowAppearanceController` responsibility split.

The governing principle remains:

> Make ownership explicit, remove transitional code, preserve behavior, and prefer boring/inheritable structure over clever machinery.

---

## Session closure

This session completed or materially advanced four consecutive maturity phases and repeatedly used real acceptance testing to expose defects that static structure/CI alone could not see.

The most important pattern was not the amount of code moved. It was the validation loop:

> establish an ownership invariant → make the smallest structural move → let CI challenge it → exercise the actual Windows application → preserve unexpected findings as governed work → delete transitional machinery only after the new path is proven.

At pause, PMG is materially less ambiguous than it was at the start of the hardening sequence. Phase 5 can wait for a fresh session.