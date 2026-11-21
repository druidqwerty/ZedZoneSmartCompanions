# 3.2.1 verification — PASS (Fever / Cold Companion Freeze Hotfix, ZED ZONE Patch 1.0.20)

RELEASE_READY: true

## Candidate identity, game target, and build

- **Target game version:** ZED ZONE **Patch `1.0.20`** (`buildid 26773715`, Depot `1211602` manifest `7274078474911224572`, Unity `2023.1.18f1` IL2CPP x64).
- **Root-cause disassembly (`GameAssembly.dll` `1.0.20`):**
  - `GOAPGoal_ClearDebuff..ctor` (`0x8C9AD0`) assigns priority **`150.0f`** to `buffId = 7` (`HIGH FEVER`) and `30.0f` to `buffId = 14` (`LOW FEVER`), overriding combat (`GOAPGoal_EliminateThreat`), following, and player commands.
  - `GOAPAction_ClearDebuff_Fever.Execute` (`0x4461E0`) returns `true` (action running) for **`giveUpSeconds = 120.0f`** seconds while standing completely still whenever `FindWarmSourceCovering(pos) == null`, then suppresses for `60.0f` and repeats until a `Fever Reducer` (`234`) clears the buff.
- **Production DLL:** `src/SmartCompanions/bin/SmartCompanions.dll`, SHA-256 **`36b8136732292e3e0f1b81e4f89c7df65681dd2d76d071ca2b2eebcc60d1f1f1`** (`Assembly-CSharp` `AssemblyRef` normalized to `0.0.0.0`, `0 warnings, 0 errors`).
- **Real engine startup (`1.0.20`):** **92** Harmony-patched methods (`SafeHarmonyPatcher.FailedPatchCount == 0`); `[SelfVerify] PASS v3.2.1`.

## Live in-engine test matrix (2/2 runs on Patch `1.0.20`, frozen `36b81367...`)

| # | Runner / Mode | Backup / Evidence Dir | Observed Results | Save & Log Parity |
|---|---|---|---|---|
| 1 | `tools/run_live_acceptance.py` | `backups/live-20261121-021240` | `54.8s`, `exit 0`, `has_fail_in_log=false`, `Patched 92 (failed=0)`: all live suites PASS including **`V312Live (f)` Fever/Cold Freeze Fix** (`followPrio=45.0 <= 45`, `holdPrio=-100.0 <= 0`, `execAbort=True`, `IsSuppressed=True`, `sharedBagFeverReducerCured=True`) | `8,386/8,386` `GameData` SHA-256 identical; `Player.log`: `0` exceptions |
| 2 | `tools/run_ui_smoke.py` | `backups/ui321-20261121-021351` | `exit 0`, `Patched 92 (failed=0)`, **13/13** suites (incl. `SurvivalRegression #12`: priority clamping, idle abort, and native IL2CPP boxed `Single` roundtrip into `GOAPGoal_ClearDebuff.IsSuppressed(7/14)`), **`300/300`** actual repaint cases + `3` QuickGuide PNGs (`303` PNGs), `0` UI errors/fallbacks | `8,386/8,386` `GameData` SHA-256 identical; `modkit save-guard verify zed-zone` PASS (`0` temp artifacts) |

## Open verification boundaries

1. **Automated hardware mouse wheel:** `wheel_verified=false` (carried over from `3.2.0` OS probes).
2. **Live two-client Steam co-op session:** Two-machine / two-account `Host + Client` live gameplay was not launched (`UiSmoke` exercises co-op authority decision logic `13/13` in-fixture only).
3. **In-match manual `1.0.20` nickname rename flow:** Verified deterministically in `UiSmoke V312Regression (h)`.
