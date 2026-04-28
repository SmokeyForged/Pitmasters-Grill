# PMG Developer Notes

## Diagnostics Export

Use **Settings > Diagnostics > Export Diagnostics Package** to create a local ZIP under PMG app data diagnostics. The package includes app/runtime metadata, safe settings summaries, provider health, recent performance timings, cache stats, recent provider failures, active logs, and debug traces.

The export intentionally avoids secrets, browser/launcher auth data, raw clipboard contents, and full raw cache database contents. Local profile/app-data paths are redacted where practical.

## Provider Health

The Diagnostics tab includes a provider health grid. It is populated from normal app activity and local telemetry, so it does not call external APIs just to fill the table. Providers can show Healthy, Degraded, Offline, or Unknown, with last success/failure times, failure count, average latency, and cache hit/miss counts.

## Performance Timings

PMG records lightweight timings for clipboard classification, local-list parsing, normalization/deduplication, cache lookup, ESI lookup, zKill lookup, resolver total, board population/render, and total clipboard-to-board handling. Recent timings are exported in diagnostics.

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

## Pilot Explainability

The pilot detail pane now includes a compact explainability line showing known source/freshness context: identity source, corp/alliance freshness, kill/loss source, recent activity basis, cyno-capable hull signal basis, fleet-size basis, and fallback/retry state when present.

## Optional PMG Themes

Theme palettes live under `PitmastersGrill/Themes/`:

- `CharcoalOps.xaml`: clean charcoal default with ember accents.
- `TacticalGrill.xaml`: darker command-console variant with stronger threat accents.
- `ClassicPmgGrill.xaml`: closest to the current PMG feel with grill/ember board styling.

The selected theme is persisted in `AppSettings.VisualTheme` and is applied by `MainWindowAppearanceController`. Dark Mode remains the switch for the grill-style dark palettes; disabling Dark Mode falls back to the legacy light palette.

Theme resource dictionaries define centralized tokens such as `BackgroundBase`, `PanelBackground`, `BoardGridLine`, `AccentEmber`, `ThreatCritical`, `SuccessGreen`, `WarningAmber`, and `ErrorRed`. UI code should consume dynamic brushes rather than hardcoding new colors in code-behind.

## Detail Panel Realism Rules

The right-side panel is evidence-first. It must not claim live jumps, grid presence, cloaks, current location, private tracking, or movement unless PMG has a legitimate source for that exact data. Current activity text is labeled **Recent Public Kill/Loss Activity** and only uses public zKill/local-cache summary fields.

If PMG only has aggregate counts or a last public ship observation, the UI says so. It does not infer current presence from older kill/loss data.

## Cyno Signal

`CynoSignalAnalyzer` owns deterministic Cyno Signal scoring. Module anchors:

- Normal cyno: `Cynosural Field Generator I`, type ID `21096`
- Covert cyno: `Covert Cynosural Field Generator I`, type ID `28646`
- Industrial cyno: `Industrial Cynosural Field Generator`, type ID `52694`

Confirmed evidence requires victim/loss item data showing one of those modules. Attacker-only appearances do not prove fitted cyno modules. If PMG only has public summary data, the analyzer returns hull/history-based `Likely`, `Possible`, `Inferred`, or `Unknown`, never `Confirmed`.

Hull capability is an inference only. A ship capable of fitting a cyno does not prove the pilot fitted or used one. The current hull map is conservative and name-based because PMG does not yet carry complete SDE group/category data through the detail panel.

Scoring summary:

- Recent confirmed victim/loss cyno module: strong confirmed evidence.
- Older confirmed module: likely/stale confirmed-history evidence.
- Recent cyno-capable hull activity: inference.
- Public activity within 30 days: small supporting context.
- Local known-cyno override: local context hint only.

Confirmed module observations are stored in the killmail intel database table `pilot_cyno_module_observations_day`. The importer scans victim item lists recursively and records only module metadata: pilot ID, killmail ID/time, victim ship type ID, module type/name, destroyed/dropped quantities, item state, and source.

Use **Diagnostics > Cache Maintenance > Rebuild Killmail Derived Intel** after schema/backfill changes. It rebuilds only derived confirmed cyno-module observations from local extracted killmail archive data. It does not clear settings, notes, ignore lists, themes, manual overrides, resolver cache, or unrelated cache data. If local extracted archives are unavailable, run Enable KillMail DB Pull or refresh the killmail cache first.

Diagnostics export includes safe Cyno Signal summaries recorded during detail-window analysis. It does not export full raw killmail dumps.

Future work: if PMG keeps richer cached killmail detail records, use them cache-first and lazily from the details window. Do not fetch full killmails for every board row blindly.

## Validation Checklist

- Build with `dotnet build PitmastersGrill.slnx --no-restore -m:1`.
- Launch PMG and confirm existing settings, dark mode, tray/icon behavior, and ignore list behavior still load.
- Copy an EVE local list to the clipboard and confirm board population still works.
- Select a pilot and confirm the detail pane still opens and double-click still opens zKill.
- Change Settings > General > PMG Theme across all three themes and confirm the board remains readable.
- Confirm detail panel text says Recent Public Kill/Loss Activity and does not imply live movement.
- Confirm Cyno Signal is Unknown with no evidence and cannot show Confirmed without module evidence.
- Run Rebuild Killmail Derived Intel when testing confirmed cyno module backfill from existing local archive data.
- Export Diagnostics Package and inspect the ZIP contents for safe summaries, logs, provider health, timings, and cache stats.
- Refresh cache stats, clear expired cache, and verify clear-all/compact require confirmation.
- Run `.\tools\publish-release.ps1` only when local release artifacts are desired.

## v0.9.4 Polish Notes

- Known-Cyno Override is a manual high-confidence board signal and should render as the confirmed covert/lavender Sig state with the `✦` icon.
- Ignore entries are typed by ID: Pilot, Corporation, or Alliance. Existing `ignore-alliances.json` alliance IDs migrate automatically into typed Alliance entries, and matching precedence is Pilot, then Corporation, then Alliance.
- `resolver_cache` schema version 10 adds `corp_id` so corporation ignores can match by ID after affiliation is known. Existing rows backfill naturally as affiliation refreshes.
- Settings > Accessibility contains color-blind signal palettes. These adjust board/legend signal brushes while preserving Sig icons and text labels.
- Window opacity is applied to background/surface/board background brushes only; foreground text and grid borders should remain opaque/readable.
- Clipboard local-list regression fixture: `test-fixtures/clipboard-large-local-list-valid.txt`. It should be accepted as an EVE local-list shaped payload while code, markup, stack traces, shell output, paths, and logs remain rejected.
