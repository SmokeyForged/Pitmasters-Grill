# Developer Notes

This document is for maintainers and contributors working on Pitmaster's Grill.

PMG is a Windows WPF desktop app for EVE Online public intel review. It is built around copied local lists, public data enrichment, local caching, and user-visible evidence boundaries.

---

## Project Goals

PMG should be:

- fast enough for live play
- understandable to users
- conservative with claims
- respectful of EVE client boundaries
- resilient to public API/provider failures
- useful without private ESI scopes
- easy to diagnose
- safe to leave running

PMG should not become:

- a bot
- an automation layer
- a private data collector
- a hidden client-state scraper
- an overconfident oracle

---

## Main Project

The main WPF project lives in:

```text
PitmastersGrill/
```

Important files/folders:

```text
PitmastersGrill/PitmastersGrill.csproj
PitmastersGrill.Tests/PitmastersGrill.Tests.csproj
.github/workflows/dotnet-build-test.yml
PitmastersGrill/App.xaml
PitmastersGrill/App.xaml.cs
PitmastersGrill/MainWindow.xaml
PitmastersGrill/MainWindow.xaml.cs
PitmastersGrill/Assets/
PitmastersGrill/Models/
PitmastersGrill/Services/
PitmastersGrill/Persistence/
PitmastersGrill/Providers/
PitmastersGrill/Views/
PitmastersGrill/Diagnostics/
```

---

## High-Level Architecture

Core flow:

```text
Clipboard input
→ guardrails
→ local pilot parsing
→ entity resolution
→ local cache lookup
→ public intel enrichment
→ board/analysis/intel display
```

Freshness layers:

```text
Archive Backfill
→ R2Z2 Live Feed
→ Today’s Freshness
→ Historical Freshness
→ Background Historical Repair
```

---

## Startup

Startup should prioritize user-visible readiness.

Expected pattern:

1. show splash/startup flow
2. run optional fail-open release awareness checks without blocking startup
3. initialize core services safely
4. show main window
5. schedule background work after UI render
6. avoid blocking UI thread with network/database work

Background Historical Repair and live feed behavior should not run inside constructors or `InitializeComponent`.

---

## Update Awareness
Update awareness is intentionally not a full self-updater.

Startup and Settings -> Version may check GitHub for the latest stable PMG release. These checks must remain fail-open, cancellable where practical, and user-confirmed before opening a browser. They must not download release assets, replace executable files, restart PMG, or introduce rollback behavior unless a future release explicitly scopes and tests a real updater.

Manual checks should be able to verify the release-check pipeline even when a startup prompt was skipped for a specific version.

## Freshness Model

### Archive Backfill

Archive Backfill is the historical baseline.

It imports completed public archive days and remains the complete-day authority.

Freshness repair should not mark archive days complete.

### R2Z2

R2Z2 is optional and disabled by default.

It ingests live zKill-known public deltas when enabled.

It is not a completeness guarantee.

### Today’s Freshness

Manual visible-pilot recent/same-day repair.

It should be user-triggered, bounded, cancellable, and rate-limit aware.

### Historical Freshness

Manual visible-pilot recent completed-day repair.

It compares public killmail IDs against local freshness-seen state and imports missing public killmails.

### Background Historical Repair

Startup enrichment over a bounded local candidate pool.

It must remain:

- delayed until after UI is usable
- one-shot
- bounded
- cooldown-protected
- cancellable
- rate-limit aware
- non-blocking

---

## SQLite and Write Coordination

PMG has several possible write paths:

- archive backfill
- live R2Z2 ingest
- Today’s Freshness
- Historical Freshness
- Background Historical Repair
- reset/reseed paths

These should coordinate through the shared write gate/import coordination path.

Avoid uncoordinated SQLite writes.

Avoid reset while live/freshness writes are active.

Do not introduce nested non-reentrant gate acquisition.

---

## Archive vs Freshness Boundaries

Freshness repair must not become archive replacement.

Rules:

- Archive Backfill owns complete-day import state.
- Freshness repair imports incremental public killmails.
- Freshness repair should not call full-day replace logic.
- Freshness repair should not mark archive days complete.
- Reset/reseed should invalidate freshness checkpoints when local seen/derived state is cleared.

---

## Diagnostics

Diagnostics should be useful and safe.

Good diagnostic content:

- app/runtime info
- settings summary
- freshness snapshots
- R2Z2 status
- checkpoint counts
- `live_killmail_seen` counts by source
- recent app logs

Avoid:

- raw killmail JSON
- private credentials
- unrelated local files
- chat log contents unless explicitly designed and justified
- sensitive OS/user data

---

## UI Notes

Main UI areas:

- Analysis
- Grill
- Intel
- Ignore List
- Settings
- Help

`MainWindow.xaml.cs` is still one of the main pressure points. Avoid expanding it unnecessarily after 1.0. Prefer extracting behavior into services/helpers when safe.

Recent low-risk extraction areas now include:

- board display settings coordination
- board column layout persistence/validation helpers
- window layout and snapshot logic
- settings-tab mapping helpers
- Analysis-tab deterministic summary helpers

Keep UI behavior predictable:

- no startup-blocking work
- no hidden long-running operations without status
- no crash-prone XAML resource loading
- graceful provider/network failures
- readable board layout
- usable diagnostics

---

## Board Layout

Board layout should support:

- visible-column persistence
- manual column resizing
- rightmost visible column filling the inner grid edge
- window resize behavior
- compact/panel-style use
- safe reset behavior

Avoid saving automatic layout calculations as user-driven layout changes.

---

## Icons and Branding

PMG uses app branding assets under:

```text
PitmastersGrill/Assets/
```

Avoid startup-critical XAML icon decoding where it can crash app startup.

If an icon/image is optional visual polish, failure to load it should not prevent PMG from launching.

---

## Networking

PMG interacts with public services.

Network code should expect:

- rate limits
- connection resets
- temporary outages
- missing data
- late-posted killmails
- unavailable archive days

Use clear status, backoff, cancellation, and diagnostics.

Do not hammer public services.

---

## Rate Limits

Rate-limit behavior should be conservative.

Manual user-triggered repair may show wait/backoff behavior.

Background startup repair should generally record rate-limit status and exit rather than waiting indefinitely.

---

## Error Handling

Prefer:

- visible status
- diagnostics context
- low-noise logs
- safe fallback
- no crash where recovery is possible

Avoid swallowing important errors silently.

Avoid letting optional UI assets crash startup.

---

## Build

Common build command:

```powershell
dotnet build .\PitmastersGrill\PitmastersGrill.slnx --configuration Release -m:1
```

Common test command:

```powershell
dotnet test .\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj --configuration Release -v minimal
```

Windows CI currently restores, builds, and tests through:

```text
.github/workflows/dotnet-build-test.yml
```

Known warning classes may include:

- helper method naming that hides inherited members
- nullable sender delegate mismatch
- analyzer style suggestions

Warnings should be reviewed, but not every analyzer suggestion is release-blocking.

---

## Release Checklist

Before release:

- build succeeds
- automated tests pass
- app launches
- tray icon and app branding do not crash startup
- Grill populates
- columns resize and persist
- Analysis renders
- Intel renders
- Today’s Freshness works
- Historical Freshness works or skips cleanly
- Background Historical Repair does not block startup
- R2Z2 remains disabled by default
- diagnostics export works
- Settings -> Version update check works or fails with a clear message
- startup update awareness continues if GitHub/network is unavailable
- README/docs match release
- patch notes exist
- version metadata is correct
- no generated artifacts are committed

Avoid committing:

```text
.codex-temp/
.vs/
bin/
obj/
*.db
*.sqlite
diagnostics bundles
release ZIPs
local user files
```

---

## Testing Suggestions

Useful smoke tests:

1. launch PMG
2. copy/paste local
3. verify Grill rows
4. resize board
5. toggle columns
6. run Today’s Freshness
7. run Historical Freshness
8. check Intel status
9. export diagnostics
10. close/reopen PMG
11. verify settings/layout persist

Useful edge tests:

- no internet
- zKill/ESI temporary failure
- not-yet-published archive day
- large local list
- ignored corp/alliance
- visible pilot with late-posted killmail
- narrow window size
- multiple monitor/window restore

Deterministic automated coverage now exists for a growing set of non-UI services and extracted controllers. Prefer extending `PitmastersGrill.Tests/` before adding brittle UI automation or live-network tests.

---

## Contribution Guidance

Good contribution areas:

- docs
- diagnostics
- UI copy
- bug reports
- safe display formatting
- tests/probes
- small provider resilience improvements

Advanced areas requiring extra care:

- SQLite schema changes
- archive import
- freshness repair
- write coordination
- R2Z2
- board layout persistence
- startup flow

When changing advanced areas, include validation notes.

---

## Maintainer Principle

PMG should help players see public evidence faster without overstating certainty.

When adding features, ask:

```text
What evidence supports this?
What can be stale?
What can fail?
What should the user know?
Can this crash startup?
Can this hammer a public service?
Does this preserve EVE client boundaries?
```

Prefer clarity over cleverness.
