# Pitmasters Grill — Technical Preview v0.8.0 Patch Notes

## Release Summary
Technical Preview v0.8.0 focuses on source-control readiness, identity cleanup, logging and cache hygiene, and safer killmail bootstrap behavior. This release also formalizes the move from the legacy **PitmastersLittleGrill** naming to **PitmastersGrill** and shifts version information into Settings for a cleaner main UI.

## Shipped Changes

### Source control and project baseline
- Published the PMG source baseline to GitHub so code changes are now versioned from the app source itself.
- Established the working repository structure for ongoing tracked development.

### Logging and killmail path groundwork
- Added the diagnostic logging foundation for PMG.
- Added a configurable killmail data path to support non-default storage layouts, including Proton-style usage.
- Added the Settings path controls and supporting logic for default vs. override path handling.

### Legacy identity cleanup
- Renamed the legacy internal/project identity from **PitmastersLittleGrill** to **PitmastersGrill**.
- Completed the internal identity migration so the project now builds and publishes under the new name.
- This release uses the new **PitmastersGrill** local identity and storage root.

### Settings and branding cleanup
- Moved version information into the **Settings** tab instead of the main header area.
- Updated the preview version display to **Technical Preview-v0.8.0**.
- Cleaned up branding text around the Settings version section.

### Killmail archive cache hygiene
- Added **24-hour TTL cleanup** for the killmail archive cache.
- Old cached archive tarballs and extracted day folders are now cleaned up automatically instead of accumulating indefinitely.

### Killmail bootstrap controls
- Added a **Max Killmail Age** setting in Settings.
- The fresh pull/bootstrap flow now uses the configured age window instead of a fixed hardcoded seed window.
- Updated the fresh pull reset path so the reset/bootstrap work runs asynchronously and no longer hard-locks the app window during the operation.

## Operational Notes
- This release is intended to be used with the current **PitmastersGrill** identity and Settings-based killmail bootstrap controls.
- For the cleanest validation on v0.8.0, testers may choose to start with a fresh local PMG data state and then run a new killmail bootstrap from the Settings screen.

## Deferred from v0.8.0
The following work was explored during this session but is **not part of the v0.8.0 shipped scope**:
- ignore lists
- opacity / transparency rework
- icon / logo window-taskbar integration
- color blind mode work

## Commit Scope Included in v0.8.0
This patch note reflects the changes committed on April 16, 2026 for:
- `f90cbc5` — Add configurable killmail data path and diagnostic logging foundation
- `1b15b7a` — Rename legacy PitmastersLittleGrill identity to PitmastersGrill
- `4e653e1` — Move version info to settings and clean up branding text
- `4387ebf` — Complete internal PitmastersGrill identity migration
- `bbaff03` — Add 24-hour TTL cleanup for killmail archive cache
- `bb72bb5` — Add max killmail age setting and async fresh-pull reset
