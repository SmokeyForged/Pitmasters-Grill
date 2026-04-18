# Pitmasters Grill — Technical Preview v0.8.2 Patch Notes

Technical Preview v0.8.2 focuses on observability, troubleshooting readiness, and lower-risk diagnostics refactoring after the 0.8.1 stability work.

## Highlights

- Added a configurable application **Log Level** in Settings.
- Added a new **Debug** logging mode for troubleshooting freezes, hangs, and ingest anomalies.
- Added startup, shutdown, and global exception logging for better post-incident evidence.
- Added targeted board lifecycle diagnostics without reverting to broad UI log spam.
- Began pulling diagnostics concerns out of `MainWindow` into a dedicated diagnostics class.

## New

### Log level setting
- Added a **Log Level** selector in Settings near the **Logs** button.
- Supported log levels:
  - **Normal** — default everyday logging
  - **Debug** — targeted troubleshooting mode

### Diagnostics foundation
- Added a dedicated `MainWindowDiagnostics` class to centralize main-window diagnostics behavior.
- Moved lower-risk diagnostics concerns out of `MainWindow` so future instrumentation changes are more localized.

## Improvements

### Debug logging
When **Log Level** is set to **Debug**, PMG now records higher-value lifecycle markers such as:
- clipboard handler boundaries
- clipboard accepted / ignored outcomes
- board process requested
- initial board build start / complete
- cache hydrate completion
- board process start / settled / superseded
- finalize and retry flow markers
- clear-board lifecycle markers

This is intentionally phase-level logging rather than per-row or per-event spam, to reduce the chance of diagnostics themselves causing significant slowdown.

### Exception and startup logging
- Added global exception capture for:
  - dispatcher unhandled exceptions
  - AppDomain unhandled exceptions
  - unobserved task exceptions
- Added startup and shutdown logging for better timeline reconstruction after hangs or forced closes.

### Settings and persistence
- Added persistent storage for the selected log level.
- The selected log level now survives app restart.

## Refactor

### Diagnostics extraction
- Extracted a slim main-window diagnostics boundary into a dedicated diagnostics class.
- Kept the extraction intentionally narrow to reduce risk:
  - clipboard lifecycle diagnostics
  - board lifecycle diagnostics
  - retry lifecycle diagnostics
  - clear-board diagnostics
  - in-memory UI heartbeat age reporting
- Left more entangled timing/first-generation internals in `MainWindow` for later refactor passes.

## Operator note for testers
- **Normal** should remain the default mode for everyday use.
- **Debug** should be enabled when reproducing freezes, hangs, or ingestion anomalies.
- If a freeze occurs in **Debug** mode, zip the logs before restarting the app.
- The current Debug implementation is intentionally lighter than the earlier verbose attempt, but it may still add some overhead during active board population.

## Commit scope included in v0.8.2

- `PitmastersGrill/Models/AppLogLevel.cs`
- `PitmastersGrill/Models/AppSettings.cs`
- `PitmastersGrill/Services/AppSettingsService.cs`
- `PitmastersGrill/Persistence/AppLogger.cs`
- `PitmastersGrill/MainWindow.xaml`
- `PitmastersGrill/MainWindow.xaml.cs`
- `PitmastersGrill/App.xaml.cs`
- `PitmastersGrill/Diagnostics/MainWindowDiagnostics.cs`
