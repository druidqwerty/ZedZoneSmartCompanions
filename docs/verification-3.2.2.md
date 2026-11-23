# 3.2.2 verification — PASS (Built-in Event Logging for Bug Reports, ZED ZONE Patch 1.0.20)

RELEASE_READY: true

## Candidate identity, game target, and build

- **Target game version:** ZED ZONE **Patch `1.0.20`** (`buildid 26773715`, Depot `1211602` manifest `7274078474911224572`, Unity `2023.1.18f1` IL2CPP x64).
- **Production DLL:** `src/SmartCompanions/bin/SmartCompanions.dll`, SHA-256 **`56b1db0b954cbd050b7529760b24f304553b77c14c20feb86fc72b348c7a96f8`** (`Assembly-CSharp` `AssemblyRef` normalized to `0.0.0.0`, `0 warnings, 0 errors`).
- **Real engine startup (`1.0.20`):** **92** Harmony-patched methods (`SafeHarmonyPatcher.FailedPatchCount == 0`); `[SelfVerify] PASS v3.2.2` (including `EventLogRecorder.SelfVerify()`).

## Live in-engine test matrix (2/2 runs on Patch `1.0.20`, frozen `56b1db0b...`)

| # | Runner / Mode | Backup / Evidence Dir | Observed Results | Save & Log Parity |
|---|---|---|---|---|
| 1 | `tools/run_live_acceptance.py` | `backups/live-20261123-141810` | `55.7s`, `exit 0`, `has_fail_in_log=false`, `Patched 92 (failed=0)`: all live suites PASS including **`V312Live (g)` EventLogRecorder in live `Gaming` world** (`bytes=6632`, `WORLD`/`SNAP_NPC`/`BUG_MARK`, `privacy=clean`) plus `(a)–(f)` | `8,386/8,386` `GameData` SHA-256 identical; `Player.log`: `0` exceptions |
| 2 | `tools/run_ui_smoke.py` | `backups/ui322-20261123-142034` | `exit 0`, `Patched 92 (failed=0)`, **13/13** suites (incl. **`V312Regression (j)`** `EventLogRecorder` toggle/write/mark/privacy/clear & `ScaleAndPrivacyRegression`), **`300/300`** actual repaint cases + `3` QuickGuide PNGs (`303` PNGs), `0` UI errors/fallbacks; visual PNG check RU & EN PASS | `8,386/8,386` `GameData` SHA-256 identical; `modkit save-guard verify zed-zone` PASS (`0` temp artifacts) |

## Open verification boundaries

1. **Automated hardware mouse wheel:** `wheel_verified=false` (carried over from `3.2.0` OS probes).
2. **Live two-client Steam co-op session:** Two-machine / two-account `Host + Client` live gameplay was not launched (`UiSmoke` exercises co-op authority decision logic `13/13` in-fixture only).
3. **In-match manual `1.0.20` nickname rename flow:** Verified deterministically in `UiSmoke V312Regression (h)`.
