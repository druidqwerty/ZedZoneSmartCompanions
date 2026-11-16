# 3.1.0 verification — PASS (Camp survival & farming, Fireteams, Anti-Wander, Dual-Inventory Sorting & UX Release)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `8a4a942366bd509b5b84a2f4039a1b18afb52f08863c1d0d89c73e94dc879c5e`.
- Release build with warnings treated as errors (`dotnet build -c Release -t:Rebuild -warnaserror` against ZED ZONE's `2026-11-15` regenerated `Assembly-CSharp.dll`): **0 warnings, 0 errors**.
- Real engine startup: **86** Harmony-patched methods; `[SelfVerify] PASS v3.1.0` (61 chatter topics, **1,215** unique lines: 577 RU / 302 EN / 336 ZH, 12 storage categories, 20 custom NPC commands in RU/EN/ZH, 5 archetypes, 5 veteran ranks).
- Reproducible live gameplay acceptance (two consecutive clean runs on the frozen DLL):
  - Run #1: `backups/live-20261117-012137` (`47.2s`, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` save files SHA-256 identical).
  - Run #2: `backups/live-20261117-012242` (`55.2s`, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` save files SHA-256 identical).
- Graphical UI matrix & 11 deterministic suites: `backups/ui310-20261117-012352` (**300/300** actual IMGUI repaint cases + 3 `[?]` Quick Guide screenshots = `303` PNGs, zero `[UiSmoke] FAIL`, **8,386/8,386** save files identical).
- Win32 OS input probes (`PostMessageW` exact-PID window):
  - Scale `0.75`: `backups/ui310-20261117-012928` (`pid=67080`, `effective_scale=0.75`, `passed=true`, **8,386/8,386** save files identical).
  - Scale `2.0`: `backups/ui310-20261117-012958` (`pid=81032`, `effective_scale=1.6167665`, `passed=true`, **8,386/8,386** save files identical).

## Development history (caught and fixed before freeze)

- Earlier pre-freeze candidates (`backups/live-20261116-234156`, `234626`, `235200`, `live-20261117-010626`, `011024`, `011550`) exposed and drove fixes for:
  1. `TryTriggerCoverUnderRangedFire` taking cover against holstered human shooters before ranged engagement started;
  2. `ReadyShotPriority` / `FollowLeader_GetPriority` losing to `FollowLeader` at 3.5–10m leader offsets, plus `CombatResponse.Refresh` not triggering re-selection when `currentGoal == null` after doctrine transitions;
  3. `CompanionSurvivalSystem` dispatching camp resupply / sleep chores while a companion held a tactical `HoldPosition`/`CoverDirection`/`FallBackHere` anchor, and `TacticalSquadSystem_Runtime` not cancelling active tasks or enforcing a tight per-frame Hold leash (`1.15m` out-of-combat / `1.65m` in-combat);
  4. `CampGarrisonSystem.ToggleCampGarrison` leaving stale `IsChoreWalkActive` running on recall and `PhysicalMovementHelper.StepWalkAndInteract` aborting `SafeDismiss` before checking motion stalls.
- All fixes were frozen into `8a4a9423…` before running both passing live runs, the 300-case UI matrix, and both Win32 OS probes.

## Scope of changes in v3.1.0

1. **Autonomous camp survival, resupply & offline garrison sustain (`CompanionSurvivalSystem`, `CampGarrisonSystem`, `HarmonyPatches`):**
   - Fixed `HumanCharacterController.defaultDrinkVolume` static access; added self-care from own backpacks (drinking containers, hunger/thirst consumables, bandaging/healing), camp chest food/drink/medicine walks and `TerrainObject_WaterWell` hydration walks (strictly gated to idle Follow / CampGarrison teammates, never breaking tactical `HoldPosition`/`CoverDirection`), offline garrison sustain (`UpdateOfflineNPC` Postfix gated by `EnableAutoBioAndSleep`), `isSleeping` guard in `TryPrepareGarrisonTick`, `1.85m` garrison anchor leash while settled `TickGarrisonGoalOverride` yields `false` to native `UpdateGoal()`, and instant priority goal refresh on `ToggleCampGarrison`.
2. **Base crop auto-harvesting & 32m farming radius (`BaseChoreSystem`):**
   - Added `AutoHarvestCrops` toggle in `F8 → Work` alongside expanded `32m` crop watering, fertilizing, and ripe harvest runs, plus 45s unreachable-target cooldowns (`MarkTargetTemporarilyUnreachable`) and base/`isPlayerBuilt`/`zLayer` guards for planting pots and campfires.
3. **Fireteam groups (`Alpha` / `Bravo`) & `Alt+1/2/3` command channels (`CompanionModels`, `SmartCompanionsPlugin`, `SmartCompanionsUI*`, `MultiplayerSyncSystem_Protocol`):**
   - Assign `[--] / [A] / [B]` per companion card in `F8 → Squad` and switch the active command channel via header button or `Alt+1` (All), `Alt+2` (Alpha), `Alt+3` (Bravo) across tactical orders, sorting, auto-loot, battle cry, field aid, and ration sharing; backwards-compatible with 23-field `SC14` co-op packets.
4. **Anti-wander & leash hardening (`HarmonyPatches`, `TacticalSquadSystem_Runtime`):**
   - Disabled native `GOAPGoal_Explore` on teammates and `GOAPGoal_TerritoryPatrol` on anchored defenders, suppressed native `SearchForFood`/`SearchForWater` when Smart Combat + Auto Bio/Sleep handles survival, clamped `FollowLeader` priority to `<= 0f` for anchored/Hold/Overflow teammates, tightened `HoldPosition` enforcement (`<= 1.15m` out-of-combat / `<= 1.65m` in-combat), and added distance leashes (`16.5m` loot / `32m` chores / `14m` pet care).
5. **Dual-inventory sorting, Fighter auto-dropoff & stack caps (`BaseSortingSystem_*`, `CompanionProfileSystem_Runtime`, `AutoLootSystem`):**
   - Enabled default `AllowAutoDropoffAtBase` on `Fighter` preset, added unloading of surplus items from both `sharedInventoryData` and `characterData.inventoryData` while subtracting personal reserves and protecting the first flashlight/canteen across shared+personal inventories, fixed partial-stack batch merge over-allocation (`claimedMergeUnits`), added `[Drop-off]` fallback rerouting on full destinations, and enforced `EnableBaseIndustry` toggle across ticks.
6. **Vehicles, field tactics, cursor guard & combat LOS (`ExpeditionSystems`, `FieldTacticsSystem`, `SmartCombatAI`, `SmartCompanionsInput`):**
   - Excluded `HoldPosition`/`CoverDirection` defenders from auto vehicle boarding, zeroed movement before `GetInVehicle`, added `!selfInVehicle` follow-priority guard, fixed `FieldTacticsSystem.TryGetCharacterHealth`/`TryApplyFieldHeal` via `characterStatusData.maxhp`, added controller-transition cursor unlock + `OnFocusRegained` + `CanProcessGameplayWorldOrders` modal gating, added distance/floor/cooldown guards to Emergency Aid & `PickBestFirstAidCompanion`, and preserved ranged combat LOS before taking cover or side-weaving.
7. **Quick In-Game Guide `[?]` & Vehicle Combat Chatter (`SmartCompanionsUI_Hub`, `ChatterDatabase*`):**
   - Added `[?]` Quick Mod Guide modal in RU/EN/ZH summarizing Fireteams, Camp Garrison, Chests, Vehicles, and Tactical Hotkeys, plus `VehicleCombatCallout` (bringing totals to **61 topics** and **1,215 unique lines**: 577 RU / 302 EN / 336 ZH).

## Live gameplay & graphical acceptance — PASS

- Live Run #1 (`backups/live-20261117-012137`, `47.2s`) & Run #2 (`backups/live-20261117-012242`, `55.2s`):
  - `[SelfVerify] PASS v3.1.0`: 61 chatter topics (577 RU / 302 EN / 336 ZH = 1,215 unique lines), 12 storage categories, 20 custom commands, 86 Harmony patches.
  - `[CombatLive] PASS`: 8m (`8.99m` / `6.48m`) & 12m (`12.01m` / `11.73m`) zombie ranged fire, `StopFire` negative test, MP Sync, & 8m hostile human `Survivor(Clone)` discharge (`9.71m` / `9.70m`).
  - `[PhysicalLive] PASS`: supermarket indoor ground loot walk (`6.11m` / `6.67m`, 0 teleports) + 4s mid-carry pause/resume + chest-to-chest carry walk (`3.24m` / `3.36m`, 0 teleports).
  - `[TacticalLive] PASS`: Hold Position (`0.00m` / `0.00m` drift across 2.5s while player 9m away), Fall Back Here (`7.48m` / `7.45m`), Cover Sector, Column formation restore & v2.2.0 reserve guards.
  - `[Soak230] PASS`: 80-chest/960-item warehouse (`0.14ms` / `0.14ms`, `1,876` / `1,245` memo hits), 15-NPC scale, AVO guard, 20-cycle soak, transition flush & co-op reconnect sync, zero-PII diagnostic audit.
  - `[GarrisonLive] PASS v2.4–v3.1.0`: Camp Garrison 12m raid hold (`1.73m` / `0.78m`), SafeDismiss physical handover (`5.42m` / `5.59m`, 0 teleports) + `TeamNPCPanel` purge, non-starving `UpdateGoal()`, offline garrison sustain, AntiWander gate, Fireteams, `InputFreeze=false`, long-range stay-behind, `MpAidSelector`, `CorridorNav`, `GrenadeSafetyGate`, `SavedMask`, `FuelWhitelist`, `VehicleSeats`, `Pets`.
- Graphical & deterministic (`backups/ui310-20261117-012352` + `ui-3.1.0-*.png` visually verified):
  - **300/300** actual IMGUI repaint cases + 3 `[?]` Quick Guide renders (RU/EN/ZH) across 10 resolution profiles and manual scales `0.75x`/`2.0x`.
  - All **11** deterministic regression suites PASS: `ProtocolRegression`, `TacticalRegression`, `SpecializationRegression`, `ScaleAndPrivacyRegression`, `GarrisonRegression`, `FieldTacticsRegression`, `SurvivalRegression`, `IndustryRegression`, `ExpeditionRegression`, `InventoryRegression`, and `V310Regression` (Fireteams `Alpha`/`Bravo`, legacy 23-field MP packet, zero-cell partial-stack batch cap, industry flag sync, dual-inventory reserve subtraction, hybrid drop-off, `Fighter` auto-dropoff, `VehicleCombatCallout`, `QuickGuide`).
- OS Win32 input probes (`backups/ui310-20261117-012928` @ `0.75x`, `backups/ui310-20261117-012958` @ `2.0x`): both **PASS** (tab switching, scrollbar arrow scrolling, title drag/release, close button + cursor quarantine release).
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical across all 5 runs; all installs, configs, logs and screen preferences restored; zero temporary `Mods\Z*` or `steam_appid.txt` left behind.

## Remaining limits

A live two-machine / two-account host + client network session and automated hardware mouse-wheel injection remain unverified (`wheel_verified: false` in background `PostMessageW`).
