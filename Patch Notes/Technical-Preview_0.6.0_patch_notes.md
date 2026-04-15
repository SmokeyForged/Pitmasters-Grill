## 0.6.0 — Major usability and local-intel milestone
**Focus**
- Turn PMG into a much stronger real-world tech preview.

**Features**
- Passive/background killmail intel updating during runtime.
- Truthful killmail-intel banner showing current/stale/update states.
- Settings tab added as a real user-facing control surface.
- Dark mode added.
- Always-on-top option added.
- Opacity control added.
- Clear button added for the board.
- Avg Fleet Size rounded for readability.
- Cyno Hull Seen column added.
- Cyno-capable hull detection expanded to include covert/industrial paths and user-requested T3 cruisers.
- Cyno rows highlighted red for fast visual target recognition.
- Test Killmail Import moved into Settings/Diagnostics.
- Better board/status wording and UI consistency.
- Real distinction between main-board signal and detail-pane context started to emerge.

**Fixes**
- Multiple startup and XAML issues during the settings/appearance pass.
- Fixed killmail-day import plumbing problems and refactor breakage during cleanup.
- Corrected missing handlers and constructor mismatches from UI rewrites.
- Fixed schema/version issues related to cyno observation fields.
- Solved the initial “cyno data exists in theory but does not display” problem through DB/backfill and detection path work.
- Improved board retry behavior under rate limiting.
- Removed or reduced regressions introduced during the rapid UI pass.

**Other changes**
- This was the release where PMG started feeling clearly useful on its own.
- Even without a long historical seed DB, the app proved it could be valuable immediately from fresh killmail-day continuity.
