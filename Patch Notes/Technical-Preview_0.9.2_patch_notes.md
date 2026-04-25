# Pitmasters Grill — Technical Preview v0.9.2 Patch Notes

## Release Theme

Technical Preview v0.9.2 is a defensive hardening release focused on clipboard safety, graceful failure handling, and diagnostic packaging.

This release makes PMG safer to leave running while using the system clipboard for normal work outside of EVE.

## Features and Improvements

### Clipboard Guardrails

PMG now classifies clipboard text before treating it as an EVE local list.

Clipboard content that looks like code, shell output, XML/XAML, markdown, logs, stack traces, commands, filesystem paths, or oversized text is rejected safely instead of being sent into the resolver/board population pipeline.

This helps prevent hangs when copying non-EVE content from sources such as ChatGPT, PowerShell, editors, logs, or documentation.

### Safer Local List Parsing

Local-list parsing now applies stricter pilot-name plausibility checks.

Candidate pilot names are filtered before processing, and duplicate names are removed case-insensitively.

Valid EVE local lists should continue to process normally.

### Clipboard Processing Debounce

Clipboard-change handling now includes a short debounce window before processing.

This reduces duplicate or bursty clipboard events from triggering repeated processing.

### Graceful Clipboard Rejection

Rejected clipboard payloads now produce high-level status/log information instead of attempting expensive invalid processing.

Rejection logs record metadata only, such as reason, character count, non-empty line count, plausible-name count, and suspicious-line count.

Raw clipboard contents are not logged by default.

### Diagnostic Bundle Packaging

PMG now supports diagnostic bundle creation for troubleshooting.

Diagnostic bundles can include app logs, clipboard classification logs, database logs, UI logs, debug traces, and a manifest.

A manual diagnostics package action was added under Settings > Diagnostics.

### Diagnostics Folder Access

The Diagnostics settings area now includes a shortcut for opening the diagnostics folder, making it easier to find generated bundles.

### Diagnostic Privacy Hygiene

Diagnostic bundle manifests redact machine name and app-data path by default.

Bundled logs and debug traces now sanitize common local Windows user/profile paths before being written into the ZIP.

Bundle notes warn users that logs may still contain EVE character names, public zKill URLs, public provider URLs, public IDs, and normal app activity context. Users should review bundles before sharing them publicly.

### Automatic Diagnostic Bundle Hooks

PMG now attempts best-effort diagnostic bundle creation for several failure paths, including startup failures, dispatcher unhandled exceptions, AppDomain unhandled exceptions, unobserved task exceptions, and clipboard-processing exceptions.

## Validation Performed

Validated locally before commit:

- Windows build succeeded.
- Regression testing succeeded.
- Valid EVE local-list clipboard input still processes normally.
- Non-local-list clipboard content is rejected safely.
- Clipboard rejection avoids UI hangs.
- Manual diagnostic package creation works.
- Diagnostic bundle structure reviewed.
- Diagnostic manifest redaction verified.
- Clipboard logs reviewed for metadata-only behavior.
- Log/path sanitization pass applied and rebuilt successfully.

## Known Notes

Diagnostic bundles intentionally preserve useful troubleshooting context. They may still include EVE character names, public URLs, public IDs, and normal app activity details.

Users should review diagnostic bundles before attaching them to public GitHub issues.

## Developer Notes

This release should be committed together as the full v0.9.2 clipboard/error-handling hardening pass.

Expected areas touched include:

- clipboard ingest/classification,
- local-list parsing heuristics,
- board population rejection handling,
- diagnostics UI,
- diagnostic bundle service,
- global exception handling hooks,
- version metadata.
