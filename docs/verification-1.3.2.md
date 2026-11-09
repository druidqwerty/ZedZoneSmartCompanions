# 1.3.2 verification — NOT RELEASED

RELEASE_READY: false

## Passed

- Main mod and temporary test plugins build with zero warnings/errors.
- Native command-tree provider returns exactly four vanilla groups and 13 vanilla + 11 custom commands without injected `NPCCommand:` categories.
- Actual native BuildPanelUI/CreateButton text assertions pass in RU, EN and Chinese; all custom leaf/path/legacy lookups resolve.
- Seven actual native command executions confirm stored combat, weapon and basic fields contain bare leaf values.
- Actual native menu screenshots captured and inspected. Custom labels subsequently shortened to fit native one-line buttons.
- Native inventory tests pass: full-destination rejection, repeated rejection preserves quantities, successful whole-item transfer, exact single-food consumption and no-food rejection.
- Startup data verification passes v1.3.2.

## Blocked: combat acceptance

The copied-world test uses the owner's three saved native NPCs and an actual native zombie, a loaded firearm, genuine line-of-sight, native planning and native firing (no forced shot).

Final result:

```text
FAIL: no native shot within12s phase=0, distance=8,00m acquired=False, sight=28,00, command=FireAtWill.
```

Final diagnostics: enabled/active player NPC and zombie, same height level, hostile factions, clear native fire line, IsInSight=true, IsTeammateCombatSuppressed=false, Basic.FollowPlayer, native FollowLeader goal/action, threatDataDic.Count=0. No target is acquired. Earlier unsuccessful setups included a suppressed ReturnToBase order; that fixture issue was corrected, but final valid follow-order test still fails.

Repeated live combat attempts stopped under the workspace three-identical-failures circuit breaker. **Do not publish this candidate to Workshop or announce the combat issue fixed.** No v1.3.2 release/Boosty queue item was created.

## Resume point

Investigate the native perception pipeline between UpdatePreceptionSystem, UpdateAgentSight, RefreshThreatsNow, UpdateThreats and SetThreatData. API names alone are insufficient proof that the current refresh discovers new characters. Inspect native execution before another equivalent live test. Preserve native hostility, LOS, height-level, stealth and cease-fire policies; do not make the acceptance test pass by injecting threats or firing manually.

The next acceptance run needs successful native target acquisition and actual ammunition consumption at ranged distances, plus a StopFire negative case, before release packaging is enabled.

## Local safety and artifacts

- Current installed game mod restored to published **1.3.1**, DLL SHA-256 `1d1f58ed2d4375c216555a2c882d832f6a759cc0587d35116c56ac9b7c1ac932`.
- Temporary ZLiveSmoke/ZUiSmoke folders moved out of active Mods into `backups/live132/`; no test process remains.
- Original 8,357 GameData files match the checksum baseline. All world manipulation occurred in the copied profile.
- Diagnostic candidate and final failed log retained locally in `backups/live132/SmartCompanions-unverified-1.3.2.dll` and `blocked-combat-final.log`.
- Native menu results: [native-menu-1.3.2.log](native-menu-1.3.2.log). Test source remains under tools/LiveSmoke and tools/UiSmoke, never release content.

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
