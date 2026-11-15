# 3.0.2 verification — PASS (Steam Workshop community bugfix & QoL release)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `293f821e034ea0996a4c822e8e838d447a27d59c8d428ce0992293d9992206f8`.
- Release build with warnings treated as errors (`dotnet build -c Release -t:Rebuild -warnaserror` against ZED ZONE's `2026-11-15` regenerated `Assembly-CSharp.dll`, `27,264,000` bytes): **0 warnings, 0 errors**.
- Real engine startup: **81** Harmony-patched methods; `[SelfVerify] PASS v3.0.2` (60 chatter topics, 1,179 lines, 12 storage categories, 20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261115-214409`. Graphical matrix & deterministic suites: `backups/ui302-20261115-214748` (**300/300** uninterrupted actual IMGUI repaint cases, zero `[UiSmoke] FAIL`).

## Development history (not approval evidence)

- Initial live smoke run (`backups/live-20261115-210007`) caught `Coconut` (`itemId = 230`, `SweetAndSnack`) falling into `MiscAndValuables` at self-verify and a pre-snap ground loot coordinate reference in `PhysicalCarryLiveRegression`; fixed in `ItemCategorizer.cs` (`Coconut 230 -> FreshFood`) and `PhysicalCarryLiveRegression.cs`.
- Subsequent harness runs (`backups/live-20261115-211640`..`213830`) updated `SoakAndScaleLiveRegression` for intentionally enabled hybrid `[Drop-off + Category]` chests, added an `IsPathClear` shelf-collider depenetration check in `CombatLivePlacement`, parked Phase 3's provoked `Survivor(Clone)` in `CombatLiveArena`, and kept Phase B container pairs in the same supermarket aisle.
- Initial UI smoke run (`backups/ui302-20261115-214528`) caught a hardcoded `"ModVersion: 3.0.1"` assertion in `ScaleAndPrivacyRegression.cs`; updated the harness assertion to `SmartCompanionsPlugin.PluginVersion` without rebuilding `SmartCompanions.dll` and re-verified `293f821e…` end-to-end in `backups/ui302-20261115-214748`.

## Scope of the change (all 8 Steam Workshop bug & QoL reports)

1. **Startup WASD / Esc / UI input freeze (`SmartCompanionsUI*.cs`, `BaseSortingSystem_*.cs`):** replaced every read of ZED ZONE's lazy `LootInventoryPanel.instance` singleton getter (which instantiated `LootInventoryPanel` on frame 1 and locked `CharacterInput.instance.stopAllInput`) with the raw backing field `LootInventoryPanel._instance`.
2. **Vehicle boarding, exit & seat-overflow restore (`ExpeditionSystems.cs`, `CorridorNavigationHelper.cs`):** replaced permanent `RosterMode = CampGarrison` mutation on seat overflow with transient `IsTransientVehicleOverflow` cleared unthrottled on exit, counted passengers across `isDriving || drivingVehicle`, skipped formation/yield/combat movement nudges while driving, and removed the `0.36 m` `IsPathClear` widening around vehicle hulls.
3. **Long-range Hold Position & Camp Garrison anchor (>100 m) (`HarmonyPatches.cs`, `CampGarrisonSystem.cs`, `SmartCompanionsPlugin.cs`):** patched `GOAPNPCData.isStayAtPositionOrder`, `GOAPNPCManager.IsStayingBehindOrder`, and `GOAPAction_FollowLeader.TryCatchUpTeleport`, clamped `GOAPGoal_FollowLeader.GetPriority <= 0.35f` at all distances, guarded `(0,0)` anchors, and made squad-wide hotkeys (`IssueOrder(null, ...)`, `Alt+G`, `Alt+F`, `F6`, `F9`) skip distant `CampGarrison` defenders.
4. **Co-op companion kneeling & revival (`HarmonyPatches.cs`, `MultiplayerSyncSystem*.cs`, `SmartCompanionsUI_Hub.cs`):** scoped a thread-local `NetworkManager.isOnline -> false` override strictly around `IsNonPlayerNpcForAid` (`MultiplayerTeammateAidPatches`) in `CharacterAidManager` and `PlayerAidInteract` on Host so zero-HP companions enter ZED ZONE's native kneeling revival state instead of dying immediately, broadcast `NPC_DOWNED|<guid>|1/0` via `IsMultiplayerHost`, synced remote leader positions every frame, and added **`[+] Revive`** (`REVIVE_NPC`, `<= 4.6 m`) in `F8 → Squad`.
5. **World loot hotkey (`SmartCompanionsPlugin.cs`, `SmartCompanionsUI*.cs`):** added **`Alt+F`** world hotkey for `CommandSweepNearbyLoot` and updated localized button captions in RU/EN/ZH.
6. **Stack merging (`InventoryTransfers.cs`, `AutoLootSystem_Transfer.cs`, `BaseSortingSystem_CarryOps.cs`):** added `TryAddWholeWithStacking` (full merge in 0 new cells, partial top-up + remainder placement, `SafeCompactInventory` retry via `TryAutoSortingWithoutOverflow`, atomic rollback on failure).
7. **Hybrid `[Drop-off + Category]` chests, Coconut (`230`), & vehicle trunk unloading (`BaseSortingSystem_Worker.cs`, `BaseChoreSystem.cs`, `ItemCategorizer.cs`):** included marked `[Drop-off + Category]` chests in `GetCategorizedDestinationBoxes`, separated `GetPureDropoffDestinationBoxes` (`14.0 m` auto-dropoff radius), added vehicle trunk unloading into `[Drop-off]` chests, and classified Coconut (`230`) into `FreshFood`.
8. **Reset All Settings (`CompanionModels.cs`, `SmartCompanionsUI_Hub.cs`):** added a two-step **Reset All Settings to Default** button at the bottom of `F8 → Settings` (`preserveContainerRules: true`, preserving marked base chests and companion XP).

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261115-214409`):
  - `[LiveLab] WORLD READY`: `LootInventoryPanel._instance == null` and `CharacterInput.stopAllInput == false` verified immediately after save load.
  - `[CombatLive] PASS`: 8 m & 12 m zombie ranged shots, `StopFire` negative test, MP Sync & 8 m hostile human NPC ranged discharge.
  - `[PhysicalLive] PASS`: supermarket indoor ground loot walk (`6.37 m`, arrival `1.38 m`, 0 teleports) + 4 s mid-carry pause/resume + chest-to-chest carry walk (`3.35 m`, `minSrcDist = 0.39 m`, `minDestDist = 1.30 m`, 0 teleports).
  - `[TacticalLive] PASS`: Hold Position (`0.90 m`), Fall Back Here (`8.60 m`), Cover Sector, formations, backpack reserves & rare-steak ration guard.
  - `[Soak230] PASS`: 80-chest / 960-item warehouse routed in `0.18 ms` (pure `[Drop-off]` + hybrid `[Drop-off + Category]`), 15-NPC scale, AVO compatibility, 20-cycle live soak, area-transition carry flush, co-op reconnect & zero-PII diagnostic audit.
  - `[GarrisonLive] PASS v2.4–v3.0.2 Live Suite`: Camp Garrison (`0.32 m`), SafeDismiss physical walk (`5.79 m`, 0 teleports) + `TeamNPCPanel` purge, `InputFreeze=false`, 250 m long-range stay/garrison clamp, `CampGarrison` hotkey isolation, `MpAidSelector`, `CorridorNav`, `GrenadeSafetyGate`, `SavedMask`, `FuelWhitelist`, `VehicleSeats` & `Pets`.
- Graphical & deterministic (`backups/ui302-20261115-214748`):
  - **300/300** actual IMGUI repaint cases across 6 resolutions + manual 75%/200% scales in RU, EN, and ZH; RU/EN/ZH `1920x1080` Squad (`Loot [Alt+F]`), Base, Work, and bottom-scrolled Settings (`Reset All Settings to Default`) visually inspected (`docs/images/ui-3.0.2-*.png`).
  - All **10** deterministic regression suites PASS (`ProtocolRegression`, `TacticalRegression`, `SpecializationRegression`, `ScaleAndPrivacyRegression`, `GarrisonRegression` v2.4–v3.0.2, `FieldTacticsRegression`, `SurvivalRegression`, `IndustryRegression`, `ExpeditionRegression`, and `InventoryRegression` v3.0.2 native stack merge + conservation).
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical across both runs; all installs, configs, logs and screen preferences restored; zero temporary `Mods\Z*` or `steam_appid.txt` left behind.

## Remaining limits

A live two-machine / two-account host + client session and automated hardware mouse-wheel injection remain unverified.
