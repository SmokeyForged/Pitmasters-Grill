# Pitmaster's Grill 0.7.0
**Technical Preview**

PMG 0.7.0 focuses on making the board more tactically useful, more trustworthy, and more usable during live operation. This build adds richer local killmail-derived context, cleaner risk signaling, a practical killmail DB bootstrap path for testers, and several UI and quality-of-life improvements.

## Highlights

- Added **Last Ship Seen** and **Last Seen** columns for faster pilot-context reading.
- Added persistent operator tags:
  - **Known-Cyno Override**
  - **Bait**
- Added a new **Sig** column with non-color indicators:
  - `B` = Bait
  - `!` = Possible Risk
  - `!!` = High Risk
- Added a tester-focused **KillMail DB bootstrap / pull** workflow.
- Improved the background updater path so seeded test instances can stay current more reliably.
- Added visible version labeling for **Technical Preview-v0.7.0**.

## Added

### Board intelligence
- **Last Ship Seen** now shows the most recent valid ship hull seen for a pilot.
- **Last Seen** now shows the associated day for that observation.
- Added a **Sig** column so board state is readable even without relying only on color.

### Persistent tags
- Added persistent **Known-Cyno Override** support.
- Added persistent **Bait** support.
- Tags survive restarts and feed directly into board presentation.

### Tester bootstrap workflow
- Added a manual **KillMail DB pull/bootstrap** path so testers can seed a meaningful recent local killmail window without external prep work.
- Intended flow is to bootstrap the recent window once, then let PMG continue forward from there.

### Version labeling
- Added `Technical Preview-v0.7.0` to:
  - the window title
  - the in-app header

## Changed

### Risk presentation
- Refined risk semantics to:
  - **Yellow** = Bait
  - **Orange** = Possible Risk
  - **Red** = High Risk
- Updated the legend to include symbol-based meanings alongside color meanings.

### Compact/live-use UX
- Added **Compact Mode** for a board-focused live-use view.
- Refined header, legend, and general board layout for cleaner real-time use.
- Reworked the detail pane into a less intrusive right-side overlay.

### Intel banner
- Coverage messaging now reflects the actual local complete-day span once local killmail data exists.
- Intel/status presentation was refined to reduce noise while keeping state meaningful.

## Fixed

### Data integrity / stale-state cleanup
- Fixed `Last Ship Seen` so non-ship killmails like deployables and similar objects do not populate that field.
- Fixed stale `Cyno Hull Seen` leakage when no local cyno observation exists.
- Fixed stale `Avg Fleet Size` leakage when no local fleet observation exists.
- Clean local state now renders honestly: unsupported tactical columns clear instead of showing old cached/dev-era values.

### Threat logic
- Refined risk escalation behavior so row state follows the intended semantics more reliably.
- Excluded inappropriate hull classes from the high-risk path where needed.
- Expanded approved/recognized risk hull coverage for more realistic board signaling.

### Accessibility progress
- Added symbol-based board indicators so board meaning is not color-only.
- This is the first accessibility step ahead of a future full colorblind mode.

## Operational notes

- PMG 0.7.0 is stricter about evidence-backed local tactical columns. In practice, that means some fields may now show blank instead of showing stale or weakly-backed values. This is intentional.
- The new bootstrap workflow is designed to make tester setup easier while keeping the app usable during import.

## Known follow-up areas after 0.7.0

- Full colorblind mode / alternate accessibility palette
- Further long-run hardening of autonomous killmail updater behavior
- Additional tag semantics as real tester workflows emerge

## Summary

0.7.0 turns PMG into a much stronger technical preview by improving:
- board trustworthiness
- risk readability
- accessibility via symbols
- operator tagging
- tester bootstrap/recovery workflow
- live-use ergonomics

