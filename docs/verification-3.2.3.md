# 3.2.3 verification — PASS (Battery Charger & Charcoal Fuel Fix, ZED ZONE Patch 1.0.20)

RELEASE_READY: true

## Candidate identity, game target, and build

- **Target game version:** ZED ZONE **Patch `1.0.20`** (`buildid 26773715`, Depot `1211602` manifest `7274078474911224572`, Unity `2023.1.18f1` IL2CPP x64).
- **Production DLL:** `src/SmartCompanions/bin/SmartCompanions.dll`, `711,168` bytes, SHA-256 **`0c2dae8f21d0a47e527fb53fa8b0fc67b3a8aa9636d33d09fcf3be6b420c58e7`** (`Assembly-CSharp` `AssemblyRef` normalized from `2.0.0.668` to `0.0.0.0`, `0 warnings, 0 errors` with `-warnaserror`).
- **Real engine startup (`1.0.20`):** **92** Harmony-patched methods (`SafeHarmonyPatcher.FailedPatchCount == 0`); `[SelfVerify] PASS v3.2.3`.

## Live in-engine test matrix (3/3 runs on Patch `1.0.20`, frozen `0c2dae8f...`)

| # | Runner / Mode | Backup / Evidence Dir | Observed Results | Save & Log Parity |
|---|---|---|---|---|
| 1 | `tools/run_ui_smoke.py` | `backups/ui323-20261125-110150` | `exit 0`, `Patched 92 (failed=0)`, `[SelfVerify] PASS v3.2.3`, **13/13** suites (incl. **`IndustryRegression`** live `ItemManager` catalog: `combustibles=17, approvedFuels=2, nonCombustibleCharcoal=1, batteries=7, vehicleBatteries=2`), **`300/300`** actual repaint cases + `3` QuickGuide PNGs (`303` PNGs), `0` UI errors/fallbacks | `8,386/8,386` `GameData` SHA-256 identical; `Player.log`: `1,145` lines, `0` exceptions |
| 2 | `tools/run_live_acceptance.py` | `backups/live-20261125-110709` | `53.7s`, `exit 0`, `has_fail_in_log=false`, `Patched 92 (failed=0)`, `[SelfVerify] PASS v3.2.3`, all live suites PASS including **`V312Live (h)` Industry Battery & Fuel fixes** (`batteries=7 (vehicle=2), rejectedNonCombustibleCharcoal=1, approvedCombustibleFuels=2`, native `ChargeBattery`, `FindLastCombustibleIndex`, `PassesFeatureLimit`) plus `(a)–(g)` | `8,386/8,386` `GameData` SHA-256 identical; `Player.log`: `1,505` lines, `0` exceptions |
| 3 | `tools/run_real_menu_load.py` | `backups/menuload-20261125-110806` | `25.5s`, `exit 0`, `real_menu_load_pass=true`, `self_verify_pass=true`, `screenshot_valid=true` (`5,974,701` B), `Patched 92 (failed=0)`, `[SelfVerify] PASS v3.2.3`, live `F8` toggle & gameplay verified | `8,386/8,386` `GameData` SHA-256 identical; `Player.log`: `1,839` lines, `0` exceptions; `modkit save-guard verify zedzone` PASS (`0` temp artifacts) |

## Root causes verified & resolved

1. **Battery Charger premature unload & full Vehicle Battery reload loop (`BaseIndustrySystem.cs`, `BaseSortingSystem.cs`):**
   - `ItemFeature_Battery` stores charge in `ItemData` string property `"RemainingBattery"` (Wh, defaulting to `batteryCapacityFloat` when unset), not `ItemData.durability` (casing durability).
   - `ProductionManager.UpdateBatteryCharger` writes 2-bit cell states into `ProductionData.batteryChargeStateTemp`: `0` = empty, `1` = charged (`FULL`), `2` = charging (`CHARGING`), `-1` = power off. Previous code treated `state >= 2` and `durability >= 99.5f` as charged, unloading charging batteries and reloading `100%` Vehicle Batteries (`id=85, 1200 Wh`).
   - Replaced durability/state checks with native `ItemFeature_Battery.GetBatteryRemainingPower` / `batteryCapacityFloat` (`>= 99.9%` / within `0.001 Wh` for full, `< 85%` for depleted + active powered charger `isPowerSwitchedOn && powerInputSufficientFloat > 0.001f`), excluded `TerrainObject_Production_BatteryCharger` from regular storage scans and `FindSupplyOrDest`, and re-validated charge/container state on physical arrival.
2. **Non-combustible Charcoal fed to furnaces & Stirling generators (`BaseIndustrySystem.cs`):**
   - In ZED ZONE `1.0.20`, `Charcoal` (`id=6`, `ProductionManager.ashItemId = 6`) has `ItemFeatureType.Ash (8)` and lacks `ItemFeatureType.Combustible (1)`. Furnace/StirlingGenerator fuel slots accept `[Combustible, Ash]`, so Charcoal fit into the fuel slot without burning in `ProductionManager.FindLastCombustibleIndex`.
   - Enforced native `ItemFeatureType.Combustible` + `PassesFeatureLimit` before approving station fuel (blocking `Charcoal` and `id=71 Wood Dagger`), mapped explicit native slots (`StirlingGenerator` → `inventoryData1`, `BatteryCharger` → `inventoryData1`, `Furnace` → `material=1, output=2, fuel=3`, `ElectricFurnace` → `output=2`), guarded `craftFinishFlag` / manual `isPowerSwitchedOn` / destination space, and added clean unloading of accumulated non-combustible Charcoal/ash from fuel slots when space is low (`<= 2` free cells, stack `>= 5`, or no combustible fuel left).

## Open verification boundaries

1. **Automated hardware mouse wheel:** `wheel_verified=false` (carried over from `3.2.0` OS probes).
2. **Live two-client Steam co-op session:** Two-machine / two-account `Host + Client` live gameplay was not launched (`UiSmoke` exercises co-op authority decision logic `13/13` in-fixture only).
3. **Physical multi-hour camp soak with real placed `BatteryCharger` & `Furnace` tiles:** `V312Live (h)` and `IndustryRegression` verified real ZED ZONE `1.0.20` `ItemManager` catalog, native `ChargeBattery`, `FindLastCombustibleIndex`, and `PassesFeatureLimit` against all 7 batteries and Charcoal/fuels in the live engine; real player-built base tile placement soak was not separately simulated.
