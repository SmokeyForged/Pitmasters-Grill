# PMG Runtime Composition and Lifetime Ownership

This document defines the construction and lifetime boundary for the normal Pitmasters Grill desktop runtime.

The goal is deliberately modest: make it obvious **who constructs a service, how long it lives, and who is responsible for stopping or disposing it** without introducing a DI container or service locator.

## Composition entry point

Normal runtime composition starts in:

```text
PitmastersGrill/App.xaml.cs
    -> ApplicationCompositionRoot.ComposeNormalRuntime(...)
    -> ApplicationCompositionRoot.ComposeMainWindowRuntime(...)
    -> MainWindow(backgroundIntelUpdateService, mainWindowRuntime)
```

`ApplicationCompositionRoot` is split across focused partial files so application-lifetime and window-lifetime construction can be reviewed independently.

## Application lifetime

`ApplicationRuntimeDependencies` contains the long-lived killmail/background graph:

- shared `AppSettingsService`
- `KillmailDatabaseBootstrap`
- `KillmailDatasetMetadataRepository`
- killmail write/import coordination
- R2Z2 live-feed service
- Today's Freshness service
- Historical Freshness service
- `BackgroundIntelUpdateService`

The same `AppSettingsService` instance is used by startup update-awareness, R2Z2 configuration, historical freshness, and window-runtime composition.

The same `KillmailDatasetMetadataRepository` instance is used for startup metadata bookkeeping and by `BackgroundIntelUpdateService`.

Do not create a second long-lived settings/background graph inside `MainWindow`.

## Window lifetime: non-control graph

`MainWindowRuntimeDependencies` contains non-control dependencies that are constructed before the window and then handed to it:

- local application database bootstrap/repositories
- resolver and stats providers/services
- board population processors/controllers
- board/settings/layout/shell controllers that do not require concrete WPF controls
- `MainWindowDiagnostics`
- `EveSessionContextService`
- ignore-list coordination
- browser/zKill helpers

`App` constructs this packet. `MainWindow` owns the window lifetime once the packet is handed over.

`MainWindowDiagnostics` is stopped/disposed from `MainWindow.OnClosed` with the rest of window-owned runtime cleanup.

## MainWindow/control lifetime

`MainWindow` should construct only objects whose meaning is tied to the concrete window/control tree or dispatcher behavior, including:

- WPF surfaces and presenters that receive controls
- dispatcher timers
- native-input attachment/detachment
- detail-window lifecycle/placement helpers
- current-board session state
- control-specific update/check surfaces
- small stateless maintenance helpers used directly by those control surfaces

Adding a new repository/provider/background service directly to `MainWindow` should be treated as an ownership smell. Prefer adding it to the appropriate composition packet unless its lifetime is genuinely control-bound.

## Operation lifetime

Short-lived operation objects remain local to the operation that owns them. Examples include:

- SQLite connections and commands
- seed-build-only services
- one-shot update-awareness requests
- bounded import/update operations

Do not promote operation-lifetime objects into the application composition graph without a lifetime reason.

## Startup ordering contract

Composition must not change PMG's UI-first startup behavior.

Normal startup order is:

1. initialize logging and show the startup splash
2. perform fail-open release awareness using the shared settings service
3. compose application-lifetime services
4. initialize the killmail database and write startup metadata
5. compose window-lifetime non-control services
6. construct and show `MainWindow`
7. wait for the first `ContentRendered`
8. close the splash
9. schedule background startup at dispatcher background priority
10. start archive backfill, configured R2Z2 startup, and bounded historical repair

Background archive/live/repair work must not be started from constructors or before the first window render.

## Shutdown ownership contract

Construction ownership and shutdown coordination are intentionally distinct:

- `App` owns the application-lifetime object graph and tray-icon lifetime.
- `MainWindow` owns its control/window lifetime and cleans up diagnostics, timers, native input, board session subscriptions, and detail state.
- `MainWindow` also coordinates the deterministic `BackgroundIntelUpdateService.StopAsync()` barrier as part of window close because the barrier is what prevents the WPF window from disappearing before PMG-owned background work has quiesced.
- `App` must not create or start a second background service instance during shutdown.

The deterministic shutdown sequence established before this composition refactor remains authoritative:

```text
stop admitting work
-> signal/cancel producers
-> await owned background work
-> wait for foreground freshness gate
-> wait for killmail write-gate quiescence
-> permit final WPF close
```

## Shared-instance invariants

The following are intentional invariants:

- one normal-runtime `AppSettingsService`
- one normal-runtime `BackgroundIntelUpdateService`
- one application killmail write/import graph
- one startup/runtime `KillmailDatasetMetadataRepository`
- one `MainWindowDiagnostics` instance per MainWindow lifetime
- no second resolver/stats/board-processing graph constructed inside the active MainWindow constructor

Automated composition tests should fail if these ownership boundaries are bypassed.

## Non-goals

This boundary does **not** introduce:

- `Microsoft.Extensions.DependencyInjection`
- a service locator
- runtime reflection-based activation
- an IoC framework
- a generic lifetime framework
- MVVM conversion

PMG composition should remain explicit C# that can be followed from `App` into the root and then into the window.

## Review checklist for new services

When introducing a new service, answer these before choosing where to construct it:

1. Is it application-, window-, or operation-lifetime?
2. Does it require a concrete WPF control or only pure dependencies?
3. Who stops/disposes it?
4. Can two instances create conflicting state, callbacks, timers, network work, or database writers?
5. Does its startup work need to wait until after first render?

If those answers are not clear, preserve the ambiguity as a design question instead of hiding it behind a container.
