# PMG Developer Notes

## Release posture

The latest tagged release is **v0.9.5.1**, a hotfix on top of `v0.9.5`.

This repo currently also contains **unreleased 0.9.6 candidate** work on main. Supporting docs must keep that distinction explicit:

- `v0.9.5.1` is the latest tagged release
- current main includes unreleased candidate work
- do not describe `0.9.6` candidate features as already released unless a tag and release pass actually happen

PMG remains technical preview / `0.9.x` stabilization, not `1.0`.

## Diagnostics Export

Use **Settings > Diagnostics > Export Diagnostics Package** to create a local ZIP under PMG app data diagnostics. The package includes app/runtime metadata, safe settings summaries, provider health, recent performance timings, cache stats, recent provider failures, active logs, and debug traces.

The export intentionally avoids secrets, browser/launcher auth data, raw clipboard contents, and full raw cache database contents. Local profile/app-data paths are redacted where practical.

## Provider Health

The Diagnostics tab includes a provider health grid. It is populated from normal app activity and local telemetry, so it does not call external APIs just to fill the table. Providers can show Healthy, Degraded, Offline, or Unknown, with last success/failure times, failure count, average latency, and cache hit/miss counts.

## Performance Timings

PMG records lightweight timings for clipboard classification, local-list parsing, normalization/deduplication, cache lookup, ESI lookup, zKill lookup, resolver total, board population/render, and total clipboard-to-board handling. Recent timings are exported in diagnostics.

Large local-list testing currently looks healthy, but performance notes should still be framed as observed behavior, not immutable guarantees.

## Cache Maintenance

The Diagnostics tab exposes safe local cache maintenance:

- Refresh Cache Stats
- Clear Expired Cache
- Compact Cache DB
- Clear All Cache

Destructive actions require confirmation. Maintenance is blocked while a board lookup is active. The clear-all action is scoped to PMG resolver/stat cache rows and does not delete unrelated files.

## Release Helper

Run the local release helper from the repo root:

```powershell
.\tools\publish-release.ps1
```

It publishes locally, creates `PMG-tech-preview-vX_Y_Z.zip`, writes a SHA256 checksum, and creates a release notes template under `artifacts\release`. It does not push, tag, upload, or sign anything.

## Watchlist Implementation Notes

Current main includes an unreleased watchlist feature. Maintain these rules:

- watch state is local/manual state, not threat semantics
- key watch state by pilot/character ID when available
- do not promote display name to the primary key when an ID exists
- unresolved rows should not allow watch toggles as if they were safely keyed
- watchlist persistence is local repository/database state
- row state application should happen after row identity is known
- board ordering should treat watched-first as a pinned partition
- later column sorts must sort inside watched/non-watched groups rather than mixing them
- watchlist must not affect cyno, bait, tackle, or killmail evidence semantics

## Compact and Panel Shell Rules

`v0.9.5.1` tightened compact/panel mode stability. Treat these as maintainer guardrails:

- do not live-mutate native `WindowStyle`, `ResizeMode`, or `WindowChrome` as part of normal compact toggling
- compact mode should stay layout-driven inside a stable shell
- panel/custom shell behavior should preserve configured transparency
- compact enter/exit should not freeze the app
- row interactions must remain intact in compact mode

Current main also persists compact-mode state across restart. Preserve that behavior unless there is a very strong reason to redesign it.

## Window Persistence Notes

Current main persists:

- main window position
- main window size
- compact-mode state
- panel-mode startup behavior

Maintain these rules:

- save/restore must use one coordinate system consistently
- WPF window bounds are DIPs; monitor/work-area comparisons must also be converted into DIPs before comparison
- do not assume the primary monitor
- valid negative coordinates are expected on multi-monitor setups
- only clamp when saved bounds are no longer visible on any current monitor work area
- avoid off-screen restore after monitor-layout changes
- avoid saving minimized junk bounds
- avoid startup ordering where later shell/layout behavior overwrites saved `Left`/`Top`

Low-noise logging around restore/clamp decisions is acceptable if needed for future debugging.

## Column Layout Persistence

Current main includes board column layout save/reset.

Maintain these rules:

- use stable column keys
- treat layout persistence separately from column visibility
- ignore missing/unknown saved columns safely
- do not let stale saved layout data break startup if columns are renamed or removed
- reset should restore default order/width without requiring the user to hand-rebuild the board layout

## Summary Banner

Current main includes a lightweight board composition summary banner.

Important behavior:

- compute from currently visible rows
- compute after ignore filtering
- update when board content changes
- update when watch state changes
- keep it concise and low-noise
- avoid turning it into a second dashboard

## Hover Explanation

Current main includes concise hover explanations for row/signal reasoning.

Maintain these rules:

- keep them short
- derive them from existing row evidence/state rather than inventing a second logic path
- do not let them overstate certainty
- do not let them replace the detail sidecar
- keep them low-noise and delayed enough that normal board movement is still comfortable

## Pilot Explainability

The pilot detail pane includes a compact explainability line showing known source/freshness context: identity source, corp/alliance freshness, kill/loss source, recent activity basis, cyno-capable hull signal basis, fleet-size basis, and fallback/retry state when present.

## Detail Sidecar Placement Notes

Pilot detail behaves like a sidecar inspector rather than a full-board takeover.

Maintainer expectations:

- prefer opening beside the board
- honor saved placement preference when possible
- flip or clamp near monitor edges instead of rendering off-screen
- keep owner theme and opacity in sync

The `v0.9.5.1` hotfix specifically tightened monitor-edge behavior, so regressions here matter even if the board itself still populates correctly.

## Optional PMG Themes

Theme palettes live under `PitmastersGrill/Themes/`:

- `CharcoalOps.xaml`: clean charcoal default with ember accents
- `TacticalGrill.xaml`: darker command-console variant with stronger threat accents
- `ClassicPmgGrill.xaml`: closest to the current PMG feel with grill/ember board styling

The selected theme is persisted in `AppSettings.VisualTheme` and is applied by `MainWindowAppearanceController`. Dark Mode remains the switch for the grill-style dark palettes; disabling Dark Mode falls back to the legacy light palette.

Theme resource dictionaries define centralized tokens such as `BackgroundBase`, `PanelBackground`, `BoardGridLine`, `AccentEmber`, `ThreatCritical`, `SuccessGreen`, `WarningAmber`, and `ErrorRed`. UI code should consume dynamic brushes rather than hardcoding new colors in code-behind.

## Accessibility Notes

Settings > Accessibility includes color-blind signal palettes. These visibly change board/legend signal brushes while preserving Sig icons and text labels.

Current public wording should stay cautious:

- color-blind mode is real and visibly changes board colors
- broader review by a color-blind tester is still pending

Avoid claiming full accessibility validation that has not happened yet.

## Detail Panel Realism Rules

The detail view is evidence-first. It must not claim live jumps, grid presence, cloaks, current location, private tracking, or movement unless PMG has a legitimate source for that exact data. Current activity text is labeled **Recent Public Kill/Loss Activity** and only uses public zKill/local-cache summary fields.

If PMG only has aggregate counts or a last public ship observation, the UI says so. It does not infer current presence from older kill/loss data.

## Cyno Signal

`CynoSignalAnalyzer` owns deterministic Cyno Signal scoring. Module anchors:

- Normal cyno: `Cynosural Field Generator I`, type ID `21096`
- Covert cyno: `Covert Cynosural Field Generator I`, type ID `28646`
- Industrial cyno: `Industrial Cynosural Field Generator`, type ID `52694`

Confirmed evidence requires victim/loss item data showing one of those modules. Attacker-only appearances do not prove fitted cyno modules. If PMG only has public summary data, the analyzer returns hull/history-based `Likely`, `Possible`, `Inferred`, or `Unknown`, never `Confirmed`.

Hull capability is an inference only. A ship capable of fitting a cyno does not prove the pilot fitted or used one. The current hull map is conservative and name-based because PMG does not yet carry complete SDE group/category data through the detail panel.

Scoring summary:

- Recent confirmed victim/loss cyno module: strong confirmed evidence
- Older confirmed module: likely/stale confirmed-history evidence
- Recent cyno-capable hull activity: inference
- Public activity within 30 days: small supporting context
- Local known-cyno override: local context hint only

Confirmed module observations are stored in the killmail intel database table `pilot_cyno_module_observations_day`. The importer scans victim item lists recursively and records only module metadata: pilot ID, killmail ID/time, victim ship type ID, module type/name, destroyed/dropped quantities, item state, and source.

Industrial-cyno bait observations are stored separately in `pilot_bait_observations_day`. PMG records these only when the same public loss victim item list contains `Industrial Cynosural Field Generator` plus a warp scrambler or warp disruptor. Attacker-only appearances are not fitted-module evidence and must not create bait observations. Current tackle detection uses exact known T1/T2 type IDs where available and conservative item-name matching for names containing `Warp Scrambler` or `Warp Disruptor`.

Use **Diagnostics > Cache Maintenance > Rebuild Killmail Derived Intel** after schema/backfill changes or when rebuilding derived intel from existing local extracted killmail archive data. It rebuilds only derived confirmed cyno-module observations and industrial-cyno bait observations from local extracted killmail archive data. It does not clear settings, notes, ignore lists, themes, manual overrides, resolver cache, or unrelated cache data.

Important release note: `v0.9.5.1` does not require this rebuild by itself, and current watchlist/window/layout/hover UI work is not expected to require it by itself either.

Diagnostics export includes safe Cyno Signal summaries and derived bait counts/examples. It does not export full raw killmail dumps.

Future work: if PMG keeps richer cached killmail detail records, use them cache-first and lazily from the details window. Do not fetch full killmails for every board row blindly.

## Proton note

Current tester feedback indicates the Windows PMG build works under Proton.

Maintainer framing should stay careful:

- Windows build under Proton is a practical compatibility note
- visual polish may not match native Windows
- Linux-native packaging and polish remain deferred

Avoid overselling Proton support as equivalent to a finished native Linux release.

## Validation Checklist

- Build with `dotnet build PitmastersGrill.slnx --no-restore -m:1`.
- Launch PMG and confirm existing settings, dark mode, tray/icon behavior, and ignore list behavior still load.
- Copy an EVE local list to the clipboard and confirm board population still works.
- Confirm watchlist persists and watched-first sorting survives board-column sorts.
- Confirm compact enter/exit does not freeze.
- Confirm panel/custom shell behavior still preserves intended transparency.
- Confirm main window position/size restore correctly on a two-monitor setup.
- Confirm compact/panel persistence survives restart.
- Confirm pilot detail sidecar placement respects monitor edges.
- Confirm the saved-note flag remains visually distinct while scanning.
- Confirm summary banner counts update as visible rows, ignores, and watch state change.
- Confirm board column layout save/reset works and ignores stale saved columns safely.
- Confirm hover explanations do not interfere with row selection, right-click detail, double-click zKill, note clicks, or compact drag.
- Select a pilot and confirm the detail sidecar still opens and double-click still opens zKill.
- Change Settings > General > PMG Theme across all three themes and confirm the board remains readable.
- Confirm detail text says Recent Public Kill/Loss Activity and does not imply live movement.
- Confirm Cyno Signal is Unknown with no evidence and cannot show Confirmed without module evidence.
- Run Rebuild Killmail Derived Intel when testing confirmed cyno module or industrial-cyno bait backfill from existing local archive data.
- Verify industrial cyno + scram/disruptor victim-item evidence gives Sig `B`, while industrial cyno alone, tackle alone, and attacker-only appearances do not.
- Export Diagnostics Package and inspect the ZIP contents for safe summaries, logs, provider health, timings, and cache stats.
- Refresh cache stats, clear expired cache, and verify clear-all/compact require confirmation.
- Perform a Proton extraction/runtime sanity check when practical.
- Run `.\tools\publish-release.ps1` only when local release artifacts are desired.

## v0.9.4 and v0.9.5.x Notes

- Known-Cyno Override is a manual high-confidence board signal and should render as the confirmed covert/lavender Sig state.
- Ignore entries are typed by ID: Pilot, Corporation, or Alliance. Existing `ignore-alliances.json` alliance IDs migrate automatically into typed Alliance entries, and matching precedence is Pilot, then Corporation, then Alliance.
- Window opacity is applied to background/surface/board background brushes only; foreground text and grid borders should remain opaque/readable.
- Clipboard local-list regression fixture: `test-fixtures/clipboard-large-local-list-valid.txt`. It should be accepted as an EVE local-list shaped payload while code, markup, stack traces, shell output, paths, and logs remain rejected.
