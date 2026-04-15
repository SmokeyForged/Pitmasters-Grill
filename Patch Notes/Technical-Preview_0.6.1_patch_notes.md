## 0.6.1 — Resolver/retry patch and alpha confidence pass
**Focus**
- Fix post-release behavior issues and harden PMG for alpha testers.

**Features**
- Resolver path updated so bad/fake names no longer poison the retry loop.
- Quiet real characters can still populate corp/alliance even with no zKill history.
- Board completion behavior became much more trustworthy under mixed real/fake populations.
- First-run continuity behavior appeared to anchor on the latest published archive day and then move forward from there.
- Live alpha validation done against larger local/Jita-style samples.

**Fixes**
- Fixed the major issue where a true zKill miss could keep consuming retries and trigger misleading `retry limit reached` states.
- Added terminal-not-found handling to stop a single bad toon from poisoning the board.
- Preserved useful behavior for real quiet characters that exist in EVE but lack zKill presence.
- Improved live-use reliability under larger copied populations.
- Confirmed that apparent “late final row” behavior was more of a status/finalization timing issue than a real data-path failure.

**Other changes**
- 0.6.1 was the patch that made the app feel responsible to hand to alpha testers.
- Alpha testers began reacting to PMG as a real tool, not just a prototype.
- Feedback started shifting from “does this work?” to “what views/profiles/details should it support next?”
- This was the build that drove discussions around future lenses such as **Hauler**, **Hunter**, and **Scout** using the same underlying data.
