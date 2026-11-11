# ZED ZONE — Smart Companions (Умные напарники) — MODLOG

## Status: 1.7.0 VERIFIED / Locally Installed / Released on GitHub, Steam Workshop & Boosty

- **Game:** ZED ZONE (`1211600`, Unity `2023.1.18f1` IL2CPP x64)
- **Loader:** BepInEx `6.0.0-be.793` IL2CPP + `ZedModsLoader` `1.1.0` (Workshop `3834614258`)
- **Installed Mod Path:** `G:\Steam\steamapps\common\ZED ZONE\Mods\SmartCompanions\SmartCompanions.dll` (+ `mod.json`)
- **Source Path:** `C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions\src\SmartCompanions\` (и синхронизировано в `G:\Steam\steamapps\common\ZED ZONE\ZedModKit\src\SmartCompanions\`)
- **Save Backup:** `C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions\backups\ZEDZONE-saves-backup`

## Historical 1.0.0 Startup Evidence (`BepInEx\LogOutput.log` & `ZedModsLoader-report.txt`)
- `Patched 13 game method(s) via Harmony.`
- `Registered SmartCompanionsController MonoBehaviour in IL2CPP domain.`
- `[SelfVerify] PASS: 44 chatter topics (171 RU lines, 92 EN lines), 12 storage categories, 8 custom NPC commands verified.`
- `Code mods loaded: 1 — ZED ZONE Smart Companions 1.0.0 [BepInEx plugin]`

## Completed Files
- [x] `SmartCompanions.csproj`
- [x] `CompanionModels.cs`
- [x] `ItemCategorizer.cs`
- [x] `ChatterDatabase.cs`
- [x] `ChatterDatabaseCombat.cs`
- [x] `SmartChatterSystem.cs`
- [x] `SmartCombatAI.cs`
- [x] `BaseSortingSystem.cs`
- [x] `AutoLootSystem.cs`
- [x] `HarmonyPatches.cs`
- [x] `SmartCompanionsUI.cs`
- [x] `SmartCompanionsUI_Hub.cs`
- [x] `SmartCompanionsPlugin.cs`
- [x] `MultiplayerSyncSystem.cs`
- [x] `MultiplayerSyncSystem_Npcs.cs`
- [x] `MultiplayerSyncSystem_Protocol.cs`
- [x] `BaseSortingSystem_PhysicalCarry.cs`
- [x] `BaseSortingSystem_Worker.cs`
- [x] `BaseSortingSystem_CarryOps.cs`
- [x] `PhysicalMovementHelper.cs`
- [x] `AutoLootSystem_JobRunner.cs`
- [x] `AutoLootSystem_Transfer.cs`

## Cover 2026 — completed (2026-11-09)

- Goal: square series-style Steam Workshop cover for ZED ZONE: Smart Companions.
- [x] docs/cover-2026-master.png: original image_generate output, 1254 x 1254, byte-identical to source.
- [x] docs/preview.png: Pillow LANCZOS optimized PNG, 512 x 512, 532393 bytes (less than 1 MiB).
- [x] workshop/preview.png: byte-identical copy of docs/preview.png.
- [x] docs/cover-2026-prompt.md: exact prompt, generation details, dimensions, byte counts and SHA-256 hashes.
- [x] Master and final 512px preview visually inspected via view_image; all required text correctly spelled and readable.
- Gotcha: provider ignored aspectRatio override; actual generated output is square, so no crop required.
- Next: no remaining cover tasks. No upload or publication performed by this artwork task.

## 1.3.1 UI repair — 2026-11-09 (in progress)
- User reports: F8 has blank companion content on every language; Chinese labels are squares; LMB in F8 attacks and destroys base objects.
- Starting state: clean git HEAD 4046a60, released plugin 1.3.0 (csproj incorrectly still 1.2.0). No running ZEDZONE process.
- Existing log repeats `SmartCompanionsController.OnGUI exception: Method unstripping failed`; quick row renders before stripped scroll view aborts.
- Owners: UI font/scroll repair; gameplay input gate; public Steam feedback read; root integration/live regression/release.
- [x] Plugin/assembly version unified at 1.3.1; GUI failures retain full exception and throttle duplicates to 30s.
- Next: complete UI and game input fixes, live graphical verification (startup tests alone did not exercise GUI), package and synchronize GitHub/Steam/Boosty.
- [x] First graphical run (not release acceptance): native YaHei glyph checks passed, real populated cards rendered in RU/EN/ZH. Found settings tab unguarded player access in main menu; fixed and settings column made scrollable. Re-run must fail on any UI fallback (first probe had counted fallback as repaint).
- [x] Steam public feedback: all 7 comments + 2 pinned empty discussions, archived docs/feedback-1.3.1.md. Independent material-gathering trip is a separate feature suggestion, not part of this hotfix.
- [x] Backups before verification/install: backups/ui131-before (old DLL/manifest/config/logs plus full GameData). No save opened in menu-only UI fixture tests.
- [x] CompanionModels JSON summary now takes version from PluginVersion (removed hardcoded 1.3.0); both Workshop content layouts must receive identical final 1.3.1 DLL/manifest.
- [x] FINAL graphical run backups/ui131-final8: PID 66408 exited 0, `[UiSmoke] PASS 15/15`, font Latin/Cyrillic/Chinese coverage verified, native player Fire/Fire_Gun/MeleeAttack/held-melee blocked while modal, companions allowed, closing-frame quarantine and full release restoration PASS; 0 error entries, 0 mod warning entries.
- [x] Removed temporary probe from game Mods by moving exact verified directory to backups/ZUiSmoke-verified. Source harness remains tools/UiSmoke, not release content.
- [x] 8,355 GameData files matched backup SHA256s; player saves never loaded. Tests use inert native fixtures in graphical engine; no damage/physics test on loaded base is claimed.
- [x] Final DLL SHA256 1d1f58ed2d4375c216555a2c882d832f6a759cc0587d35116c56ac9b7c1ac932. ZIP126795 bytes SHA2564d861a2f9f30ba9f1758dd66ea1ba1c3becc452dbb96713d4d6a8fbb5a51886f. Both staging layouts and game/kit synchronized.
- Gotcha: patch concrete Human weapon overrides only. Additional Basic Fire_Gun/Melee merged/no-op stubs caused early native exit; removed. Fixture movement setter needs animation rig, so native CharacterInput own field is used. GUI color tests compare engine-normalized readback, not raw hardcoded color components.
- Next: GitHub commit/push/release, Steam upload EN/RU/ZH visibility0 and dependencies/topics verification, Boosty queue.

- [x] RELEASED 1.3.1: GitHub runtime/source commit 4d3e2f60804c3777c0e82892c325c4cd7d442efa, release https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.3.1.
- [x] Final ZIP expanded to include screenshot/log documentation: 1,262,503 bytes, SHA256 bc52644b75c1c4f3348cc0f435f94e0fc1c21082c607de4e081f3e2835e16ed2 (supersedes earlier packaging-only ZIP checksum above). Anonymous download byte/hash + manifest + DLL checks passed.
- [x] Steam upload content, preview, EN/RU/ZH pages acknowledged; dependency3834614258 already present EResult29. Anonymous API result1, visibility0, file_size576732, title/description version1.3.1.
- [x] Correctforum40676593 AJAX verified exactly2 pinned topics (570428419877202927,570428419877202938). Public EN/RU/ZH HTML returned429; post-upload localized HTML/sidebar rendering not claimed verified. Evidence docs/publication-1.3.1.json.
- [x] Boosty1.3.1 queued, replaced only pending1.3.0 entry; other projects untouched. Queue acceptance is not publication.
- Completed. No game process or temporary probe remains active. Full boundary/limitations in docs/verification-1.3.1.md.

## 1.3.2 native menu / combat audit (in progress)
- Owner reports native companion commands blank and late threat reaction. Different UI from repaired F8.
- Native runtime probe confirmed vanilla data commands are unprefixed Basic.FollowPlayer etc. Injected NPCCommand: prefix creates extra blank groups. Native label lookup receives bare leaf name, not full path; existing label hook missed it.
- Combat audit: invalid prefixed doctrine state, no proactive native perception refresh, sorting skips combat entirely, anti-friendly-fire moves even when merely carrying a gun. Fix owners separate.
- Broader audit: player-centered passive loot, XP only persisted on rankup, free unlimited manual ration buff, ignored inventory partial merges. Investigating/fixing with quantity-conservation probes.
- Lab profile copied to backups/live132/profile; temporary LiveSmoke redirects GameConfigs save/config getters there and suppresses cloud saves. Original profile must remain hash-identical.

- CIRCUIT BREAKER: three repeated native combat failures `no native shot within12s phase=0, distance=8,00m acquired=False, sight=28,00, command=FireAtWill`. Stopped repeated launches and informed owner. Final valid fixture: enabled NPC/zombie, LOS/inSight true, hostile samelevel, suppressedFalse, FollowPlayer, threats0. Investigate UpdatePreceptionSystem/native threat acquisition next; do not force threats/shots to pass.
- Menu actualnativepanels RU/EN/ZH + exactcanonicalcommands and inventoryconservation/food tests PASS. Build0warnings0errors. No1.3.2publication orBoostyqueue. See docs/verification-1.3.2.md with RELEASE_READY:false; package_v132 refuses publication package until true.
- Local game restored verified1.3.1 DLL+manifest. Testpluginsmoved to backups/live132 disabled; no gameprocess. Original8357GameDatafiles SHA256 unchanged. CandidateDLL/failedlog preservedbackup.

## Resume authorized: owner says «чини и в релиз»
- Continued from e4399ae; no completed menu/inventory work restarted. New diagnostic launch observed native GOAP Update/perception actively running, started=true, goapPause=false, target physics simulated=true. Missing UpdatePreceptionSystem and frozen physics ruled out.
- Exact GameAssembly native analysis: UpdateAgentSight returns when own/candidate isOnScreen false (both gates use agent.perceiveOffScreen; shared tiny false getter unsafe to detour), and only runs when (frameCount + frameOffset) % 7 == 1. RefreshThreatsNow only updates existing threats. Candidate's torso/sprite render children are inactive in current test.
- Implement scoped original-native sensing: temporarily expose bounded same-floor visible candidates to sight scan, align frame phase, run original native UpdateAgentSight, restore render flags/frameOffset in finally. No manual threats, targets or shots. Keep native hostility/targetability/visibility and cease-fire policies. Native dump retained backups/live132/profile/native-addresses.json, no game binary/source redistribution.
- Native camera/frame fix now admits the real 8m target in 0.06–0.19s; initial ammo25000→8 was a weapon SWAP, notshot. Shot test strengthened to require same native weapon/data identity and real ammo decrease/fireFrame advancement; no per-frame forcedaim. Copied-world arena isolates otherhostiles so native target choice is uncontaminated.
- Second native issue confirmed: DrawWeaponViaSlot and EquipRangedWeapon action unconditionally call Human.EquipWeapon_WeaponSlot even ifsame itemslot alreadydrawn, creating repeated weaponobjects andresettingaim. Added narrow teammate-activecombat idempotenceguards atbothrealcallpaths; exact slot+data+drawn weapon identity mustmatch, otherwise nativeoriginalruns. No firemethod override.
- Current pendingtest: lower-slot guard +native perception, actual8/12m shots +StopFire, preciseweapon/actionstate diagnostics. No release untilsuccess.
- Exact equip-loop root: FollowLeader priority is2 nearleader; FireAtWill adds0 to EliminateThreat native threatscore. FollowLeader.OnActionStarted calls GOAPAgent.PullOutWeapon, which selects FIRSTnonempty slot0 (knife), undoingnativegun selection. Trace confirmed slot2gun406 returnscorrectthenexplicit slot0 equip. currentWeaponItemData is indexedproperty, NOTcache; never mutate it to sync.
- Added RangedEngagementPolicy.cs: only FireAtWill companions within8m ofleader withnative-selected visible in-range threat +usable ownedgun raiseEliminate justabove nearbyFollow (2.5), leavingnativevalidity/emergencygoals/ceasefire/stealth/catch-up. GOAP PullOutWeapon preservesexactalreadyselectedgun insteadfirstslot. No manualFire/forcedgoal/inventory changes. Build0warn0error; awaitingacceptance.
- Further exact native mismatch: EquipRangedWeapon excludes LiquidWeapon8/Energy5/EnergyCharge6/Batteryfeeding2; FindBestRangedWeapon/RangeAttack includes them. Ownedliquidweapon469 (25000units) keptwinningRangeStart overrifle406 (8rounds,24m), whileEquipaction switchedback. Added RangedWeaponCompatibility filteredCOPY of AIeligibleGetRangedWeapons for playercompanions; inventory andnativeammo/scoring/locks untouched, bolts NOTexcluded (feedingType F4, gunActionType F0). Pendingfreshsameweaponshotacceptance.

- FINAL acceptance PASS on freshcopy: native8m acquisition0.75s, actualsamegunshot1.36s ammo8->7;12mshot0.36s ammo7->6; nativeStopFire3s nofire. Earlierpass .15/.49/.42s also retained; timingsobserved, notguaranteed. ProtectedtargetHP avoidsownerweaponcritdeath invalidatingstopfiretest. No forcedFire/SetThreatData. NativeMenuactualpanels3languages +7nativecommands +inventoryconservation +F8UI15/15 andnativeinputrestore PASS;0modwarnings/errors in finalruns.
- FinalDLL d4e8daf8b4f6d87e384eefa9f6a37b3b451f1e7c1c5b5a44b4cd31e676bb144b;29nativepatches. SourceKit/install/bothWorkshoplayouts synchronized1.3.2. Testplugins moved outofMods;8357originalsavefiles SHA256unchanged. ZIP1622475bytes SHA25699d9f598934929351a6b7ca477771f22d170819e92218e0b738d24e151d85813.
- Verification report RELEASE_READY:true; independent native-code review completed, no criticalinventory/globalhook/ceasefire defect. Currentremainingstep: publishGitHub/Steam/Boosty andverifypublicresults.

- GitHub1.3.2 release published at https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.3.2 (runtime commitfd0e753). Anonymous final ZIP download1622465bytes SHA256f5def39bce6ef530eb20878e869cd033ee3aa7ac2b8e95ea46c17a891cedff8a; earlierarchivehash superseded by RU/ZH install-path documentation correction only, DLLunchanged.
- Steam currentpublic item remains1.3.1 visibility0 banned0. Fullupload thencontent-onlybothrejected EResult15 AccessDenied; authBLoggedOntrue, ownerSteamIDmatches. Contentstaging succeededbutfinalpublishedmetadata/serverupdate denied. Do notclaimSteam1.3.2 live.
- One deferred retry scheduled2026-11-10 08:05Europe/Moscow, job54cdd362-c77a-4d4a-ba79-a95bbb28586d, exactverifiedhelper tools/retry_workshop_132.py (guardsnewerversion/staginghashes; one attemptonly). Bothcorrectforumdiscussionsremainpinned.
- BOOSTY1.3.2 queued replacingpending1.3.1 only; carddirectsuserstoGitHub ifWorkshopold. DiscoveredexistingBoostypublisherautomation a6fd0309-cb41-4b52-819d-912b369704eb disabled; leftunchanged toavoidpublishingunrelatedqueuedPZposts. Queueacceptance NOT publication.

## 1.4.0 Co-op Multiplayer Synchronization & Hostile Human Combat — completed (2026-11-10)
- **Root-Cause Disassembly & Fixes:**
  1. **Bug 1 (Fire-lighting UI opening on Host when friend opens container):** `BaseChoreSystem` called `TerrainObject_FirePlace.IgnitionFire(true)` (`0x5B29F0`), which invokes `ToolSelectManager.SelectTool_Player` (`0x1529D30`) on the local screen. Fixed by skipping chores on MP Clients (`NetworkManager.isClient`), igniting fueled campfires via `TerrainObject_FirePlace.OnIgnite()` (`0x5B4450`) after checking `ProductionManager.FindLastCombustibleIndex(fp.fuelInventoryData) >= 0`, and guarding `IgnitionFire(true)` in Harmony while `LootInventoryPanel` is active.
  2. **Bug 2 (NPCs invisible on MP Clients & per-player companion ownership + cross-player healing):** `ScavengerCamp` human NPCs have `characterAttrId = 0`, causing `NetMonsterSync.SpawnPuppet` (`0xBD0A37`) to abort on Clients, and `BasicCharacterController.Start()` sets `inSightFlag = 1` after `Init() -> OnExitSight()`, leaving `spriteParent` disabled. Fixed in `MultiplayerSyncSystem`: normalizes human `characterAttrId` (`201` Survivor / `202` Looter) before `SerializeCharacterData` and after `DeserializeCharacterData`, clears stale `m_spawnBytesScratch` entries on Host, calls `HostResendSpawn` & `HostSendEquipment`, enables Client puppet `spriteParent` (`OnEnterScreen()` / `OnEnterSight()`), binds `GOAPNPCData`, tracks companion ownership (`leaderCharacterGUID` / `OwnerPlayerName`), syncs remote leader position & `zLevel`, and adds cross-player companion healing/rescue (`HEAL` packet sync).
  3. **Bug 3 (Companions not shooting hostile human NPCs):** Native `GOAPAgent.UpdateAgentSight` (`0x679CC3`) checks `(target.camp % 10) == (self.camp % 10)`, treating Looters (`camp = 21`, `21 % 10 == 1`) as same-camp allies of companions (`camp = 1`). Furthermore, `GOAPGoal_WarningHumanEnemy` (`0x8B1AB0`, priority 100) preempted `EliminateThreat`, and human `MonsterAttr` (`201`/`202`) lacked threat values. Fixed by remapping Looter `camp = 21 -> 23`, configuring `MonsterAttr(201/202)` threat values (`>= 25` threat, `>= 28m` range), suppressing `GOAPGoal_WarningHumanEnemy.IsValid` for player teammates, guarding `SetThreatData` against duplicate `threatDataDic` keys, and setting `hasTargetEnemy` / `targetEnemyInSight` / `isThreatened`.
  4. **Full MP Container Category & Doctrine Sync:** Syncs box categories (`ContainerRuleProfile`) and companion doctrines across Host and Clients via `TerrainObjectData` property slot `7` (`SC14:...`) and silent `MsgChat` packets (`\u200B[SC14]|BOX|...`, `NPC|...`, `HEAL|...`, `SORT`, `LOOT`, `REQ_SYNC`) intercepted before `NetChatSync.Append`.
- **Verification & Packaging:**
  - Build: 0 warnings, 0 errors (`--warnaserror`).
  - Final DLL SHA-256: `557cd4e0341cb089c4f669b72fbeab5c7e4ea9618e63ec6c7cb506fa12bd9be8` (41 Harmony patches, `[SelfVerify] PASS v1.4.0`).
  - Live in-engine verification (`docs/combat-acceptance-1.4.0.log`): Phase 0 (8m Zombie shot in 1.65s), Phase 1 (12m Zombie shot in 0.29s), Phase 2 (StopFire 0 shots), MP Sync & Warning checks PASS, Phase 3 (8m Hostile Human NPC `Survivor(Clone)` acquired in 0.01s, rifle shot in 1.01s), 0 errors/exceptions.
  - Release ZIP: `dist/ZedZoneSmartCompanions-1.4.0.zip` (`1,639,881` bytes, SHA-256 `8be98fb41fe31628ec72cc21f973dafb9520531ad54ebb0fe3c88fc1e1be8b9f`).
  - `BOOSTY_1.4.0.md` queued via `queue.py add`.

## 1.4.1 World Container Discovery, Per-Box Isolation & Physical Item Carrying — completed (2026-11-10)
- **Root-Cause Analysis & Fixes:**
  1. **World Container Discovery Without Opening Chests (`CollectWorldTerrainObjects`):** Previously `ScanNearbyContainers()` only scanned `GameController.instance.terrainObjectsParent`, which had `0` chunk/building chests, and read stale `LootInventoryPanel.CurrentOpenInventoryData` (`0.0m`) even when `LootInventoryPanel` was closed. Fixed by scanning `MapController.instance.currentChunkList` (`chunkData.terrainObjectListTemp` + `chunkData.terrainObjectParent`), `BasicBuilding.ActiveObjects` (`building.terrainObjectParent` + `floor.insideTerrainObjectList` + `floor.terrainObjectParent`), and `BasicItem.ActiveObjects` (deployed ground containers), calling `EnsureLootGenerated()` / `EnsureLootGeneratedNet()` on unopened furniture/fridge chests, and auto-selecting the nearest chest when **F8** opens.
  2. **Unique Per-Box Identification (Fixing Shared Drop-Off / Category Bug):** Replaced `"unknown_box"` fallback with a 1-to-1 `InventoryData <-> TerrainObject` registry (`KnownInvToTerrainObj`, `KnownInvToStableId`, `KnownStableIdToInv`) and unique IDs (`box_{rx100}_{ry100}_{z}_{attrId}_{standVz}_{guid}`) plus `[x,y]` coordinate labels in **F8**.
  3. **Physical Companion Item Carrying via Invisible Carry Buffer (`CarryBuffer`):** Companions no longer teleport items between chests. In Phase 1 (`WalkToSource`), the companion walks to the source chest (`GOAPAction_FollowLeader.Execute` redirected + per-frame `TickActiveSortingMovementEveryFrame`) and pulls up to 10 item stacks into `st.CarryBuffer` (separate from their main backpack/weapons). In Phase 2 (`WalkToDestination`), the companion walks to each matching destination chest, deposits the carried items, and compacts the grid (`AutoSortMode.Dense`). `FlushCarryBufferSafely` returns all in-transit items if combat interrupts or the game exits.
- **Verification & Packaging:**
  - Build: 0 warnings, 0 errors (`--warnaserror`).
  - Final DLL SHA-256: `cfb9a1ec0c1ec4fb5291b8def4a425b43d3e56aa35fa443da672ca9cac1f680c` (43 Harmony patches, `[SelfVerify] PASS v1.4.1`).
  - Live in-engine verification (`docs/combat-acceptance-1.4.1.log`): `ScanNearbyContainers` discovered **133 unique containers** in player base without opening them (`0` duplicates, `0` `"unknown_box"`), `CarryBuffer` roundtrip PASS, Phases 0–3 (8m/12m Zombie, StopFire, 8m Hostile Human NPC) PASS, 0 errors/exceptions.
  - Release ZIP: `dist/ZedZoneSmartCompanions-1.4.1.zip` (`1,653,245` bytes, SHA-256 `30d81c84fef30ca45fe715cac07995ed85a7335e9545b76965cf9db013f3b61a`).

## Loader attribution correction — prepared (2026-11-10)
- Removed the third-party Code Mod Loader from the misleading "My ZED ZONE mods" section in all three Workshop descriptions and the description generator (`tools/prepare_v131.py`).
- Kept the loader as an explicitly third-party required dependency in EN/RU/ZH descriptions and READMEs; Smart Companions copyright, MIT license and druid authorship/testing footer are unchanged.
- Documentation-only change; binaries, versions and existing release archives are untouched. Steam upload and repository publication remain with the release owner.

## 1.4.2 ItemCategorizer Fix, Explicit Base Box Selection, 3-Mode F8 Filter & Zero-Overflow Sorting — completed (2026-11-10)
- **Root-Cause Analysis & Fixes (post-game update `2026-11-10 08:49`):**
  1. **Fixed `ItemCategorizer.HasFeature` Classifying All Items as `[Medicine]` (`0x13A9D30`):** Native `ItemAttr.GetItemFeatureByFeatureType(ItemFeatureType)` (`0x13A9D30`) falls back to `ItemManager.instance.GetItemFeatureByFeatureType` (non-null for all features) when `this.itemFeatureDataDic.ContainsKey` is false. Because `ClassifyItem` called `HasFeature(attr, ItemFeatureType.Bandage)` first, every item with a non-null `ItemAttr` was misclassified as `StorageCategory.Medicine`! Fixed by checking only `attr.itemFeatures.Contains(ft)` and `attr.itemFeatureDataDic.ContainsKey(ft)`.
  2. **Sort Only Player-Marked Base Chests (`IsMarkedByPlayer`) & 3-Mode F8 Filter:** Added `ContainerRuleProfile.IsMarkedByPlayer` (`[*] Отмечен как ящик моей базы`), automatically enabled when toggling any category or `Ящик-приёмник`, and migrated save format (`v1.4.2` / `SC142:` property prefix) to purge legacy `v1.4.1` `Medicine`-only auto-tags. Sorting (`F7`) now touches **only** player-marked chests. Added a 3-mode filter in **F8** Tab 2 (`Мои + у ног ≤2м` default, `Рядом ≤7м`, `Все 18м`).
  3. **Overflow-Free Compaction, Stack Merging & Full Drop-Off Unloading:** Replaced `AutoSorting(false, ...)` (which discarded overflow items when `executeDropFlag == false`) with `InventoryTransfers.SafeCompactInventory` (`TryAutoSortingWithoutOverflow`), added `TryDepositOrStackWhole` for stackable items, tracked `remainingFreeCells` per destination chest in `PickupBatchIntoCarryBuffer`, and added `InspectRemainingDropoffItems` so `F7` reports the exact remaining item count and missing categories when a Drop-off box isn't completely unloaded.
- **Verification & Packaging:**
  - Rebuilt against updated interop assemblies (`0` warnings, `0` errors).
  - Final DLL SHA-256: `bd26c9f67bac995a0477003b5cab9c9ff1f918e8c3c252cc44972a32977a341a` (43 Harmony patches, `[SelfVerify] PASS v1.4.2`).
  - Live in-engine verification (`docs/combat-acceptance-1.4.2.log`): Real `ItemAttr` Categorizer (`gun/knife/steak != Medicine`), Filtered UI, `CarryBuffer` roundtrip, and Phases 0–3 combat PASS with `0` errors/exceptions.
  - Release ZIP: `dist/ZedZoneSmartCompanions-1.4.2.zip` (`1,664,369` bytes, SHA-256 `8cb746e2500ce84abf2a3563d1f6dec620a264908272ceaf404f8a06fe47dfd9`).

## 1.5.0 Companion Dismissal Fix, Draggable UI, Squad Limit (15), AVO Compatibility & CollectResource Overhaul — completed (2026-11-10)
- **Root-Cause Analysis & Fixes (per `docs/PLAN_1.5.0.md`):**
  1. **Block 1 (QuitTeam / RemoveTeamNPC / UnFollowPlayer Dismissal Fix):** Hooked `CharacterData.RemoveTeamNPC`, `CharacterData.AddTeamNPC`, `GOAPNPCData.ExecuteNPCCommand("QuitTeam" / "JoinPlayerTeam")`, and `GOAPAgent.SetLeader(null)`. `MultiplayerSyncSystem.OnCompanionDismissed` flushes carried items (`FlushCarryBufferSafely`), clears `OwnerCharacterId` / `OwnerPlayerName`, sets `IsDismissedOrNotInTeam = true`, clears `agent.leaderCharacter = null` and `agentState["hasLeader"] = false`, and `SyncCompanionOwnerAndLeader` never restores a blank `leaderCharacterGUID` unless the NPC is still in `GOAPNPCManager.instance.teammateNPCs` or `teamNPCGuids`.
  2. **Block 2 (Draggable UI & Safe Non-Overlapping Default Positions):** Added mouse drag-and-drop (`HandleWindowDrag`) to `MainHubWindow [F8]`, `ChestCategoryBar` (expanded & collapsed), and `HudBadge` (expanded & collapsed) with persistent coordinates in `CompanionConfigStore` and `ResetUiPositions()`. Moved default `HudBadge` out of the top-right minimap area to upper left-center (`[-]` collapse to `F8` pill, `[x]` hide), and moved default `ChestCategoryBar` to bottom-center so it never overlaps the inventory header or backpack sort button. Updated `IsPointerOverOverlay` to use live dynamic `Rect`s and active drag state.
  3. **Block 3 (Configurable `MaxTeamSize` up to 15 & `AVO Expanded` Compatibility):** Added `MaxTeamSize` (`0 = Auto/3/AVO`, `4`, `6`, `8`, `10`, `12`, `15`) in **F8** Tab 1 & Tab 3 (`CharacterData.maxTeamNPCNumber`), plus automatic + manual `AVO Expanded` (`3836458339`) detection switching the ration hotkey from `F6` to `Alt + F6`.
  4. **Block 4 (`Basic.CollectResource` / `CollectSpecifiedItem` & Base Chest Protection):** Patched `GOAPGoal_FollowLeader.GetPriority` to stay at `1.5f` up to `42 m` during `CollectResource` / `CollectSpecifiedItem`, patched `GOAPAgentState.IsTeammateScavengeRestricted` (`false`), protected all player-marked/player-built base chests in `GOAPAgentState.IsContainerSearchableByNPC` (`false`), and extended `AutoLootSystem.CommandSweepNearbyLoot` to scavenge unmarked wild containers nearby.
  5. **Block 5 (Obstacle-Aware Carry Pathing & Cache Pruning):** Switches to pathfinding `agent.MoveTo(st.TargetContainerPos, 1.4f, true)` when `agent.isStuck` during carry jobs, and prunes destroyed `TerrainObject` pointers in `BaseSortingSystem.PruneStaleCacheEntries()`.
- **Verification & Packaging:**
  - Build: `0` warnings, `0` errors (`--warnaserror`).
  - Final DLL SHA-256: `059393d525389a104734d0a78f2547666abbeb4352f725051859668f09facbc8` (49 Harmony patches, `[SelfVerify] PASS v1.5.0`).
  - Live in-engine verification (`docs/combat-acceptance-1.5.0.log`): `QuitTeam` Dismissal, Draggable UI, `MaxTeamSize=12`, `CollectResource` & Base Chest Protection, Real `ItemAttr` Categorizer, Filtered UI, `CarryBuffer`, and Phases 0–3 combat PASS (`0` errors/exceptions).

- **Publication & Verification (2026-11-10):**
  - **GitHub Release (`v1.4.2`):** https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.4.2
  - **Steam Workshop (`3836613312`):** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312 (`visibility=0` public, `file_size=676060`, `time_updated=1794308487`). EN/RU/ZH descriptions updated and SDK-verified (`docs/publication-1.4.2.json`): removed misleading "My ZED ZONE mods: Code Mod Loader" section, explicitly credited `ZED ZONE Code Mod Loader` (`3834614258`) as a third-party dependency (`not a mod by druidqwerty`), and preserved the `© 2026 druidqwerty` MIT license & authorship footer.
  - **Boosty:** https://boosty.to/lrgpp/posts/20a8456b-8b21-4e68-9747-48f9e9cc8d1c (`published=true`, `anonymousAccess=true`, verified via `boosty_post.py check`).




## 1.4.2 publication completed (2026-11-10)
- Published verified 1.4.2 content, preview and EN/RU/ZH descriptions to existing Workshop item 3836613312. Anonymous API: result1, public visibility0, banned0, file_size676060. Fresh uncached ISteamUGC queries confirmed all three complete locale descriptions and titles match staging, with required child3834614258 preserved.
- Removed third-party Code Mod Loader from misleading My Mods advertising; retained explicit third-party install requirement, MIT and druid authorship/testing footer. Updated README install archive names to1.4.2.
- GitHub release already public; independently downloaded anonymous ZIP matched1664369bytes SHA2568cb746e2500ce84abf2a3563d1f6dec620a264908272ceaf404f8a06fe47dfd9, manifest1.4.2 and verified DLLbd26c9f67bac995a0477003b5cab9c9ff1f918e8c3c252cc44972a32977a341a. Existing release archive left immutable.
- Published EN/RU player-facing Boosty card directly (not queued): https://boosty.to/lrgpp/posts/20a8456b-8b21-4e68-9747-48f9e9cc8d1c . API200/publishedtrue, anonymous access and both language markers plus Workshop link PASS; browser post snapshot confirms links.
- Removed this project's completed/superseded pending Boosty1.3.2,1.4.0,1.4.1,1.4.2 queue entries; unrelated projects and scheduler states unchanged.
- Evidence docs/publication-1.4.2.json. Steam HTML429 and browser screenshot relay timeout prevent claiming visual screenshots; server locale text/dependencies, public file state, archive and Boosty contents verified.

## 1.5.1 Experimental publication completed (2026-11-11)
- Owner explicitly authorized publication (`публикуй`) ahead of the two-player co-op test; published as an explicitly labeled experimental update without claiming client NPC visibility is fixed.
- **Steam Workshop (`3836613312`):** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312 (`result=1`, `visibility=0` public, `banned=0`, `file_size=706780`, `time_updated=1794351752`). Anonymous `english`, `russian`, and `schinese` HTML fetches all returned `HTTP 200` with the `1.5.1 Experimental` titles, explicit co-op caveat, and required dependency `3834614258`. Local subscribed Workshop cache synced to `1.5.1` (`3252934c6414f4b8d3a69d6f35146ed8192aa109817038a989e30fc1a3f5b53b`).
- **GitHub Prerelease (`v1.5.1`):** https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.5.1 — anonymous download of `ZedZoneSmartCompanions-1.5.1.zip` verified (`706,997` bytes, ZIP SHA-256 `5a7edbc2e1e3d0f21d9a8690272017ddb842c8a3980d058cb35065c6299a8180`, embedded DLL SHA-256 `3252934c6414f4b8d3a69d6f35146ed8192aa109817038a989e30fc1a3f5b53b`).
- **Boosty queue:** `BOOSTY_1.5.1.md` queued via `queue.py add` for automatic publication on https://boosty.to/lrgpp.
- Evidence recorded in `docs/publication-1.5.1.json`. Real two-player co-op visibility verification remains open.

## 1.6.0 Full Forensic Audit & Clean Rebuild — completed locally (2026-11-11)
- **Audit & Root-Cause Disassembly (`GameAssembly.dll` + `resources.assets` + `Player-prev.log`):**
  1. **Fixed `MonsterAttr` ID Corruption (`0=Player`, `200=Looter`, `201=Survivor`, `202=SecurityGuard`):** Earlier weak-model code set `LooterMonsterAttrId = 202` (`Augmented Security Officer` cyborg) and ran `NormalizeHumanCharacterData(..., isLooterHint: true)` inside `SetThreatData_Prefix` on **all** `HumanCharacterController`s (including the local/remote player whose native `characterAttrId` is `0`), plus inflated peaceful `Survivor(201)` `MonsterAttr` threat values globally. Fixed `LooterMonsterAttrId = 200`, added `IsAnyPlayerCharacterData` hard guard (local + remote players), added automatic repair for legacy `202` on non-guard humans back to `201`/`200`, and restricted `MonsterAttr` threat setup to `Looter(200)` only.
  2. **Fixed Puppet `InventoryData` NRE (`2,897` `LateUpdate` NREs in `Player-prev.log`) & Skipped `GameController.AddCharacter`:** Native `NetPlayerSync.ApplyEquipmentVisuals` (`0xC0E440`) called `EquipRangedWeapon` on remote player puppets without allocating `characterData.inventoryData` (`+0x80`), throwing `NullReferenceException`. Earlier `HumanNpcEquipmentGuard` was gated behind `IsMultiplayerOnline`, missing companions initialized before hosting (`orlov` `469`, `Time` `461`, `Jason` `445`) and missing `EquipMeleeWeapon` (`Charles` `309`, `Sarah` `302`). Rebuilt `HumanNpcEquipmentGuard` to cover all non-local-player humans regardless of `IsMultiplayerOnline`, auto-allocate backing `InventoryData`, clean up half-equipped state on failure, guard `UpdateHand_RangedWeapon` on `rangedWeapon`/`weaponAttr`/`characterData`/`hand1Elbow`/`hand2Elbow`/`hand1Transform`/`hand2Transform`, and rescue active host NPCs missing from `GameController.characterList`.
  3. **Fixed `StopFire`/`Passive` Cease-Fire Leak, Friendly Human Threat Zeroing, Wild Leader False-Positive, Ghost Threat Pruning & Chat Hotkey Gating:** Added `CeaseFireEliminateThreatGate` (`GOAPGoal_EliminateThreat.IsValid` + `GetPriority`), moved friendly-human `score = 0f` guard above `Passive` in `SmartCombatAI.AdjustThreatScore`, pruned inactive/culled (`!activeInHierarchy`) threats in `CombatResponse.Refresh`, required `IsAnyAlliedPlayerCharacterId` in `GOAPAgent_IsPlayerTeammate_Postfix`, fixed `CheckPlayerRearFlank`/`CountNearbyHostiles` via `IsActualHostileCharacter`, added `NPC_DISMISS` & `AutoCompact` MP sync, and gated `F6`/`F7`/`F8`/`F9` on `NetChatBar.isTypingChat` + live `playerCharacter` (`Esc` closes `F8`).
- **Build & Live Engine Verification (`docs/verification-1.6.0.md`, `docs/combat-acceptance-1.6.0.log`):**
  - Build: `dotnet build -c Release -t:Rebuild --no-restore -warnaserror` -> `0 warnings, 0 errors`.
  - Final DLL SHA-256: `52330628d659b0213be8803ec47152930977ebbef2e1ec978fe3d99f526393dd` (`358,400` bytes, `55` Harmony patches, `[SelfVerify] PASS v1.6.0`).
  - Live ZED ZONE engine acceptance: Phases 0–3 (`8m` & `12m` Zombie ranged shots, `StopFire` negative test, `8m` Hostile Human `Looter(200)` ranged shot) + all `v1.6.0` regression checks `PASS`; `0` weapon/hand IK NREs in `Player.log`; `8,383 / 8,383` real `GameData` save files SHA-256 identical before and after (`gamedata_identical: true`).
  - Staged locally in `workshop/Contents` and local Workshop cache (`3836613312`), with `1.5.1` backup preserved at `backups/v160-before-live/3836613312` and private co-op test zip at `backups/SmartCompanions-1.6.0-coop-test.zip` (`139,980` bytes, SHA-256 `e13f4fdc699a4c54a363b5083c7485d5f6d15d8f7bdf09da935ca58d0f6d6f25`).
- **Publication & Verification (2026-11-11):**
  - **GitHub Release (`v1.6.0`):** https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.6.0 (commit `c1b5ba6`). Anonymous download of `ZedZoneSmartCompanions-1.6.0.zip` verified (`710,752` bytes, ZIP SHA-256 `2930b61a8761a2c6020ea2f1e7b02eac3a31907ffaee7c842a1b58597e5e9cf0`, embedded DLL SHA-256 `52330628d659b0213be8803ec47152930977ebbef2e1ec978fe3d99f526393dd`).
  - **Steam Workshop (`3836613312`):** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312 (`result=1`, `visibility=0` public, `banned=0`, `file_size=717008`, `time_updated=1794358821`). Anonymous `english`, `russian`, and `schinese` HTML checks returned `HTTP 200` with `1.6.0` titles/descriptions and required dependency `3834614258`. Local Workshop subscription cache synced to `1.6.0`.
  - **Boosty auto-queue:** `BOOSTY_1.6.0.md` queued via `queue.py add` (superseded unpublished `BOOSTY_1.5.1.md` entry removed; unrelated projects untouched).
  - Evidence recorded in `docs/publication-1.6.0.json`. Real two-player host+client human NPC visibility remains open for live co-op confirmation.

## 1.7.0 True Physical Looting & Item Transfer (Zero Item Teleportation) — completed (2026-11-11)
- **Root-Cause Analysis & Implementation:**
  1. **Unified Physical Movement & Surface-Distance Interaction (`PhysicalMovementHelper.cs`):** All companion physical tasks (ground/corpse/wild-container looting, base chest sorting, and base chores) now run through `GOAPAgent.UpdateGoal -> PhysicalMovementHelper.TickPhysicalFrameForAgent` using `ComputeSurfaceDistance(selfPos, targetPos, targetAnchor)` (`Collider2D.ClosestPoint`) so large shelves/chests measure distance to their outer edge rather than interior center, with A* obstacle pathfinding (`agent.MoveTo`) and a `0.35s–0.45s` arrival interaction pause within `MaxReachableInteractRadius = 2.45m`.
  2. **Physical Ground, Corpse & Wild-Container Loot Queue (`AutoLootSystem_JobRunner.cs`, `AutoLootSystem_Transfer.cs`):** Removed instant pickup loops in `CommandSweepNearbyLoot`; companions now build a distance-ordered `st.LootQueue` and physically walk to each ground item, corpse, or unmarked wild container before transferring items at `<= 2.45m`.
  3. **Physical Base Chest Carry & Non-Teleporting Rollback (`BaseSortingSystem_Worker.cs`, `BaseSortingSystem_PhysicalCarry.cs`, `BaseSortingSystem_CarryOps.cs`, `BaseChoreSystem.cs`):** Removed the instant-sort button (`⚡ Instant Sort`) and remote chore fallback; if combat interrupts a carry job or a destination chest is unreachable, `ReturnCarryBufferWithoutTeleport` keeps items in the companion's pack or drops them at their feet instead of teleporting items back across the base.
- **Build & 6-Phase Live Engine Verification (`docs/verification-1.7.0.md`, `docs/combat-acceptance-1.7.0.log`):**
  - Build: `dotnet build -c Release -t:Rebuild --no-restore -warnaserror` -> `0 warnings, 0 errors`.
  - Final DLL SHA-256: `e2fefde9349acec87fb5f20792eb96f7ee1bfd81f8cca93d12ea15ff63017224` (`371,200` bytes, `57` Harmony patches, `[SelfVerify] PASS v1.7.0`).
  - Live ZED ZONE engine 6-phase acceptance: Phases 0–3 (`8m` & `12m` Zombie ranged shots, `StopFire` negative test, `8m` Hostile Human `Looter(200)` ranged shot) + Phase A (`6.77m` Ground Loot Walk, `arrivalDist = 1.40m`, `0` teleports) + Phase B (`1.64m` Chest-to-Chest Carry Walk, `minSrcDist = 0.45m`, `minDestDist = 1.56m`, `0` teleports) `PASS`; `0` weapon/hand IK NREs in `Player.log`; `8,386 / 8,386` real `GameData` save files SHA-256 identical (`gamedata_identical: true`).
- **Publication & Verification (2026-11-11):**
  - **GitHub Release (`v1.7.0`):** https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.7.0 (commit `66191f5`). Release asset `ZedZoneSmartCompanions-1.7.0.zip` verified (`715,212` bytes, ZIP SHA-256 `e1bba9f466744566201a8749522ab07a4bb792bd1fb2b672a569e119dd408059`, embedded DLL SHA-256 `e2fefde9349acec87fb5f20792eb96f7ee1bfd81f8cca93d12ea15ff63017224`).
  - **Steam Workshop (`3836613312`):** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312 (`result=1`, `visibility=0` public, `banned=0`, `file_size=742606`, `time_updated=1794402000`). Localized `english`, `russian`, and `schinese` pages uploaded and verified with required dependency `3834614258`. Local Workshop subscription cache synced to `1.7.0`.
  - **Boosty auto-queue:** `BOOSTY_1.7.0.md` queued via `queue.py add` (superseded pending `BOOSTY_1.6.0.md` entry removed).
  - Evidence recorded in `docs/publication-1.7.0.json`.

