\# Pitmasters Grill — Technical Preview v0.9.3 Patch Notes



\## Release Theme



Technical Preview v0.9.3 is a focused usability and reliability release for Panel Mode, Compact Mode, and local-list parsing.



This release improves PMG’s overlay-style behavior, makes compact usage cleaner at smaller window sizes, adds a direct in-app Exit path, and fixes numeric-only EVE pilot names being rejected during board population.



\## Features and Improvements



\### Panel Mode Opacity Improvements



Panel Mode now has a proper translucent window body instead of making individual controls appear to float independently.



The opacity slider now produces a cleaner overlay-style effect, giving PMG a unified panel silhouette while preserving board readability.



\### Compact Mode Control Cleanup



Compact Mode now has a more efficient top control strip designed for smaller window widths.



The command area now focuses on core actions:



\- Move

\- Clear

\- Compact / Normal toggle

\- Exit



This reduces wrapping and prevents compact layouts from becoming distorted when the window is narrow.



\### Move Handle for Panel Mode



A dedicated `Move` control has been added for Panel Mode.



This allows the borderless window to be repositioned even when Compact Mode hides the normal top header area.



\### In-App Exit Control



PMG now includes an in-app `Exit` button.



This gives users a direct way to close the application from inside the PMG interface, including while running in Compact Mode.



\### Cleaner Shutdown Handling



The Exit path now routes through PMG’s normal shutdown flow and requests PMG-owned background work to stop before the application closes.



This helps avoid leaving background update activity running after the UI is closed.



\### Bottom Board Status Streamer



Board population status and last refreshed information have been moved out of the top command area and into a bottom status streamer beneath the board.



This keeps operational status visible without competing with compact-mode controls.



The streamer trims long text cleanly instead of forcing the layout to wrap.



\## Fixes



\### Numeric-Only Pilot Names



Fixed an issue where numeric-only EVE pilot names, such as `749`, could be rejected as unlikely pilot names during local-list parsing.



PMG now treats digits as valid pilot-name characters while preserving the existing clipboard safety checks against obviously invalid input.



\### Reduced Compact Layout Distortion



Fixed a layout issue where status text and action buttons could wrap badly at smaller window sizes, causing the board area and controls to distort.



\### Improved Panel Mode Visual Cohesion



Fixed the “floating controls” effect caused by transparent shell behavior without a full-window translucent backing surface.



\## Version and Diagnostics



Version references have been updated to `Technical Preview-v0.9.3`.



Diagnostic bundles now report the correct v0.9.3 version label.



\## Notes



This release does not change killmail lookup behavior, resolver provider behavior, ESI behavior, or zKill integration.



Clipboard guardrails remain in place for code, XML/XAML, markdown, shell output, logs, stack traces, filesystem paths, oversized text, and other non-EVE clipboard content.



