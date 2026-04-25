# Pitmasters Grill — Technical Preview v0.9.1 Release Notes

## Release Summary

Technical Preview v0.9.1 is a stabilization and usability release focused on making large local-list processing feel smooth, improving Ignore List behavior, and giving users more control over the board layout.

The core improvement is that PMG now shows name-only rows immediately after a local list is copied, then enriches those rows progressively in the background. This removes the previous “chunky” behavior where the UI could pause for several seconds before showing a batch of populated rows.

Published commit:

```text
aae708c Stabilize board responsiveness and add column controls
```

## Highlights

### Smooth board population

Board population now starts by displaying lightweight name-only rows quickly, then resolves identity, affiliation, stats, and historical context progressively.

This improves the experience during both normal local-list use and very large stress tests. In validation, a local-list stress test of roughly 1,100 names showed strong progressive population behavior and healthy provider load balancing.

### Ignore List responsiveness improvements

Ignored alliance rows now stop earlier in the enrichment pipeline.

Once a pilot’s alliance is known and matches the Ignore List, PMG removes the row and skips unnecessary stats/history enrichment. This avoids spending resolver/cache/local-history work on rows that are about to disappear anyway.

The detail-pane Ignore Alliance button was also hardened so it can still add the displayed pilot’s alliance ID even when selection state changes during board updates.

### Cache and resolver hot-path cleanup

PMG now uses more efficient cache lookup paths during board population.

Resolver cache hydration now uses the repository’s bulk lookup path instead of reflection-based per-name lookup. Stats cache hydration also now supports bulk-style reads, reducing repeated SQLite setup/check work during local-list processing.

### Smaller window support

The main window can now be resized much smaller than before.

The minimum window size was lowered from `760x430` to `420x300`, making PMG easier to fit beside EVE or other tools on constrained monitor layouts.

### Board column controls

A new settings area allows users to choose which optional board columns are visible.

Optional columns include:

- Sig
- Alliance
- Corp
- Kills
- Losses
- Avg Fleet Size
- Last Ship Seen
- Last Seen
- Cyno Hull Seen

The Character column remains mandatory so rows are always identifiable.

Column visibility settings are saved automatically and restored on launch. Users can also Show All or Reset Defaults.

### Small-window UI fit fixes

Text wrapping was improved across the app so labels and explanatory text behave better when the window is resized smaller.

The Ignore List view now supports scrolling in the main content area, making it usable at smaller window sizes. The current ignore list box also supports inner scrolling for larger lists.

## Validation Performed

Validated locally before publishing:

- Windows WPF build succeeded.
- Publish/smoke test succeeded.
- Regression testing passed.
- Board population tested with normal and large local lists.
- Approximately 1,100-name local-list stress test performed.
- Provider load balancing observed as healthy during stress testing.
- Ignore List behavior tested after responsiveness changes.
- Smaller window resizing tested.
- Column visibility toggles tested.
- Text wrapping and Ignore List scrolling tested.
- Git working tree confirmed clean after push.

## Known Notes

The build currently succeeds with warnings only. Existing warnings include nullability warnings and helper-method naming warnings around `IsEnabled(...)` hiding the inherited WPF `UIElement.IsEnabled` member. These did not block the v0.9.1 validation pass but should be considered cleanup candidates for a future maintenance pass.

## User Impact

v0.9.1 should feel significantly smoother when copying local lists into PMG. Users should see immediate feedback, progressive enrichment, fewer visible stalls, better small-window behavior, and more control over which board columns they want visible.
