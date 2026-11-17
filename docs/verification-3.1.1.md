# 3.1.1 verification — PASS (Patch 1.0.19 Compatibility, Native Save-Load Gate & Profile Self-Repair Hotfix)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `0e997ed19b4d014f9117c730560454cb23c9a571e267f68f5803e4ca1f4422ed` (`Assembly-CSharp` `AssemblyRef` normalized to `0.0.0.0`).
- Release build with warnings treated as errors (`dotnet build -c Release -t:Rebuild -warnaserror` against ZED ZONE Patch `1.0.19` / `buildid 26711990` regenerated `Assembly-CSharp.dll` `v2.0.0.668`): **0 warnings, 0 errors**.
- Real engine startup: **86** Harmony-patched methods (`SafeHarmonyPatcher.FailedPatchCount == 0`, **100%** `[EH > 0]` patch entrypoints); `[SelfVerify] PASS v3.1.1` (61 chatter topics, **1,215** unique lines: 577 RU / 302 EN / 336 ZH, 12 storage categories, 20 custom NPC commands in RU/EN/ZH, 5 archetypes, 5 veteran ranks, `RuntimeGate` verified).
- Reproducible real-player native save-load acceptance (`MainMenu -> SinglePlayer -> Sandbox -> Slot 5 -> CharacterSelectOverlay -> Gaming`, without overriding `Time.timeScale` or `isSoftPaused`):
  - Run #1: `backups/menuload-20261117-101552` (`26.2s` total, `10.1s` native `Gaming` streaming `timeScale 0.010 -> 1.000`, `sawGateBlockedDuringLoad=true`, player `characterAttrId 201->0` self-healed, live `F8` open/close + `2.0s` world soak PASS, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` & `GameSettings` files SHA-256 identical).
  - Run #2: `backups/menuload-20261117-101713` (`26.3s` total, `10.2s` native `Gaming` streaming `timeScale 0.010 -> 1.000`, `sawGateBlockedDuringLoad=true`, player `characterAttrId 201->0` self-healed, live `F8` open/close + `2.0s` world soak PASS, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` & `GameSettings` files SHA-256 identical).
- Reproducible full live gameplay acceptance (`CombatLive`, `PhysicalLive`, `TacticalLive`, `Soak230`, `GarrisonLive` on the frozen DLL):
  - Run #1: `backups/live-20261117-102345` (`57.7s`, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` save files SHA-256 identical).
  - Run #2: `backups/live-20261117-102507` (`59.8s`, `returncode=0`, zero `[FAIL]`, **8,386/8,386** `GameData` save files SHA-256 identical).
- Graphical UI matrix & 12 deterministic suites: `backups/ui311-20261117-102849` (**300/300** actual IMGUI repaint cases + 3 `[?]` Quick Guide renders = `303` PNGs, all **12** deterministic suites including `V311Regression` PASS, zero `[UiSmoke] FAIL`, **8,386/8,386** save files identical).
- Win32 OS input probes (`PostMessageW` exact-PID window):
  - Scale `0.75`: `backups/ui311-20261117-103750` (`effective_scale=0.75`, `passed=true`, **8,386/8,386** save files identical).
  - Scale `2.0`: `backups/ui311-20261117-103840` (`effective_scale=1.6167665`, `passed=true`, **8,386/8,386** save files identical).

## Root-cause investigation & fixes in v3.1.1

1. **Premature execution during `MainMenu` and `Gaming` chunk-streaming load (`SmartCompanionsRuntimeGate`):**
   - Previous isolated `LiveSmoke` runs bypassed `MainMenu` and forced `Time.timeScale = 1f` + `InGameController.ResumeGame()` on Frame 1, hiding the fact that in real Steam play ZED ZONE instantiates `playerCharacter` inside `InGameController.Init()` while keeping `Time.timeScale == 0.010f`, `isSoftPaused == true`, and `GameInitPanel._instance` / `LoadingInfoPanel._instance` active across hundreds of frames as `MapController._InitMapTexture_d__658` and `GameInitPanel._EnterGameCoroutine_d__20` stream chunks.
   - `SmartCompanionsController.Update()` previously ticked on `Time.unscaledTime` during that window, invoking `EnforceSettledHoldPositionsEveryFrame` (A* pathfinding before nav data finished initializing) and `BaseSortingSystem.ScanNearbyContainers` (`EnsureLootGenerated()` on streaming chunks).
   - Fixed by gating all gameplay ticks, survival/chore hooks, UI rendering, and input blocking on `SmartCompanionsRuntimeGate` (`IsAnyLoadingScreenOrTransitionActive`, `IsGameplayWorldReady`, `IsGameplaySimulationActive`, `IsUiAllowedToRenderAndCapture`), reading only raw `_instance` fields so lazy UI singletons (`LootInventoryPanel`, `LoadingInfoPanel`, `GameInitPanel`, `GenericConfirmPanel`) are never prematurely instantiated.
2. **Native `/OPT:ICF` `GameAssembly.dll` RVA sharing & Harmony fault isolation (`HarmonyPatches`, `SafeHarmonyPatcher`):**
   - Full `GameAssembly.dll` (`141,103` MethodDefs) RVA audit of all 86 patched targets identified an MSVC `/OPT:ICF` collision at `RVA 0x0567A10`: `GOAPAction_SearchForFood::Execute` shares the exact native function body with `GOAPAction_SearchForMeleeWeapon::Execute` and `GOAPAction_SearchForRangedWeapon::Execute`. Added `__instance.TryCast<GOAPAction_SearchForFood>()` / `TryCast<GOAPAction_SearchForWater>()` guards (`V311Regression` verified).
   - Replaced monolithic `Harmony.PatchAll` with per-target `SafeHarmonyPatcher.PatchAssemblySafely`, added `[EH > 0]` `try/catch` blocks to all Harmony patch entrypoints (previously 38 had `[EH == 0]`), isolated newly referenced `1.0.19` types/methods behind `[NoInlining]` helpers, normalized `Assembly-CSharp` `AssemblyRef` (`2.0.0.668 -> 0.0.0.0`), and removed the duplicate `workshop/Contents/Mods/` copy so `ZedModsLoader` loads a single clean `SmartCompanions/` directory.
3. **`MainMenu` / `CharacterSelectOverlay` portrait mannequin corrupting player `CharacterData.characterAttrId` (`0 -> 201`) + automatic self-repair (`HarmonyPatches`, `MultiplayerSyncSystem`):**
   - Discovered during the first `RealMenuLoad` run (`backups/menuload-20261117-100027`, saves immediately restored & verified) and confirmed via x64 disassembly of `CharacterSelectOverlay.AddPortrait` (`0x8CF3A0`) -> `CharacterData.<GetHumanCharacterPortrait>d__267.MoveNext` (`0x104D630`) and `GOAPNPCManager.IsPlayerCharacterData` (`0x9E8DF0`): in `MainMenu`, `GameController.instance.gameData` is `null` (so `IsPlayerCharacterData` returns `false`), and `GetHumanCharacterPortrait` spawns a `HumanCharacterController` mannequin (`isPlayer == false`, `GetCachedGOAPAgent() == null`) whose `BasicCharacterController.Start()` invokes `HumanCharacterController.Init()`, mutating player `characterAttrId` from `0` (`Player`) to `201` (`Survivor`).
   - Fixed by gating `HumanCharacterController_Init_Prefix` (and `NetMonsterSync` serialize/deserialize hooks) on `Il2CppSafe.IsAliveUnity(InGameController.instance)` and requiring a live `GOAPAgent` on SP/Host, plus adding `MultiplayerSyncSystem.SelfHealPlayerCharacterData` (`characterAttrId != 0 -> 0` on `isPlayer` init and `Tick()`), which automatically repaired `Ванько` (`201 -> 0`) in live runs.

## Remaining limits

A live two-machine / two-account host + client network session and automated hardware mouse-wheel injection remain unverified (`wheel_verified: false` in background `PostMessageW`).
