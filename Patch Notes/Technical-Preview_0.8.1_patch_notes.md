# Pitmasters Grill — Technical Preview v0.8.1 Patch Notes

Technical Preview v0.8.1 focuses on board-ingest stability, clipboard intake hardening, and cleaner separation of responsibility around clipboard-driven population.

## Highlights

- Fixed board-ingest regressions tied to clipboard handling.
- Prevented copied terminal / Git command content from being treated as pilot names.
- Preserved bait / cyno risk-state visibility while rows are selected.
- Formalized application version metadata for 0.8.1.

## Fixes

### Board and clipboard handling
- Fixed an ingest issue where non-local clipboard content could enter the board pipeline.
- Added stronger clipboard payload screening before board processing begins.
- Improved ignored-clipboard handling so invalid payloads are rejected cleanly instead of being processed as pilots.
- Added clearer ignore-reason reporting for clipboard rejections.

### Board visuals
- Fixed selected-row styling masking risk-state colors.
- Bait and cyno highlighting now remain visible while a row is selected.
- Selected rows now use a lighter selection treatment that preserves the underlying board signal color.

## Refactor

### Clipboard intake boundary
- Refactored clipboard acceptance logic out of the main window flow and into a cleaner service boundary.
- Added a new `ClipboardPayloadInspector` to classify clipboard payloads before parsing/processing.
- Updated `ClipboardIngestService` to:
  - use payload inspection before processing
  - return explicit accept/ignore outcomes
  - carry ignore reasons for logging and diagnostics

### Main window cleanup
- Reduced clipboard policy logic inside `MainWindow`.
- Kept `MainWindow` focused on UI orchestration while clipboard acceptance decisions now flow through the intake service.

## Versioning
- Updated the visible preview version to **Technical Preview-v0.8.1**.
- Added explicit project version metadata:
  - `Version`
  - `AssemblyVersion`
  - `FileVersion`
  - `InformationalVersion`

## Commit scope included in v0.8.1

- `PitmastersGrill/MainWindow.xaml`
- `PitmastersGrill/MainWindow.xaml.cs`
- `PitmastersGrill/PitmastersGrill.csproj`
- `PitmastersGrill/Services/ClipboardIngestService.cs`
- `PitmastersGrill/Services/ClipboardPayloadInspector.cs`
