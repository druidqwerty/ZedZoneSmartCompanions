# ZED ZONE Smart Companions v1.6.0 — Forensic Audit & Rebuild Verification

- **Version:** `1.6.0`
- **Build:** `dotnet build src\SmartCompanions\SmartCompanions.csproj -c Release -t:Rebuild --no-restore -warnaserror` -> `0 warnings, 0 errors`
- **Verified DLL SHA-256:** `52330628d659b0213be8803ec47152930977ebbef2e1ec978fe3d99f526393dd` (`358,400` bytes)
- **Private Co-op Test Archive:** `backups/SmartCompanions-1.6.0-coop-test.zip` (`139,980` bytes, SHA-256 `e13f4fdc699a4c54a363b5083c7485d5f6d15d8f7bdf09da935ca58d0f6d6f25`)
- **Harmony Patches Bound:** `55`
- **Startup `[SelfVerify]`:** `PASS v1.6.0`
- **Live Engine Acceptance (`docs/combat-acceptance-1.6.0.log`):** `PASS` (Phases 0–3 + v1.6.0 regression suite)
- **Save Integrity (`GameData`):** `8,383 / 8,383` files byte-for-byte SHA-256 identical before and after live runs (`gamedata_identical: true`)
- **Weapon/Hand IK NREs in `Player.log`:** `0` (down from `2,903` in `Player-prev.log`)
- **Two-Player Co-op Human NPC Visibility Status:** `UNVERIFIED_TWO_PLAYER_COOP` (local single-instance & puppet/equipment lifecycle verified in live ZED ZONE engine; actual two-player co-op session still required before claiming full co-op visibility fix)

## Critical Defects Found & Fixed in v1.6.0

1. **`MonsterAttr` ID Corruption (`202` = `SecurityGuard`, `0` = `Player`, `200` = `Looter`, `201` = `Survivor`)**
   - Verified in `ZEDZONE_Data/resources.assets` and `GOAPNPCManager.IsWorldHumanAttrId` (`0x9D46F0`) / `GetDisplayMonsterAttrId` (`0x9D29B0`):
     - `0` = `Player` (`prefab = 'Human'`)
     - `200` = `Looter` (`强盗`, `prefab = 'Looter'`)
     - `201` = `Survivor` (`幸存者`, `prefab = 'Survivor'`)
     - `202` = `SecurityGuard` (`Augmented Security Officer`, Second Island cyborg guard)
   - Earlier code set `LooterMonsterAttrId = 202` and ran `NormalizeHumanCharacterData(..., isLooterHint: true)` inside `SetThreatData_Prefix` on **all** `HumanCharacterController`s without excluding players or friendly NPCs, overwriting player (`0`) and survivor (`201`) `characterAttrId`s to `202` and inflating peaceful `Survivor(201)` `MonsterAttr` threat values globally.
   - **Fix:** Set `LooterMonsterAttrId = 200`, `SurvivorMonsterAttrId = 201`, added `IsAnyPlayerCharacterData` hard guard (local + remote co-op players), automatically repaired legacy `202` on non-guard humans back to `201`/`200`, and restricted `MonsterAttr` threat configuration to `Looter(200)` only.

2. **Equipment Init Crash & 2,897 `LateUpdate` NREs + Skipped `GameController.AddCharacter`**
   - Verified in `GameAssembly.dll` (`BasicCharacterController.Start` `0xC5E7B0`, `HumanCharacterController.Init` `0xED0F40`, `NetPlayerSync.ApplyEquipmentVisuals` `0xC0E440`, `EquipRangedWeapon` `0xEC5A00`, `UpdateHand_RangedWeapon` `0xF03870`):
     - `NetPlayerSync.ApplyEquipmentVisuals` called `EquipRangedWeapon` on remote player puppets without allocating `characterData.inventoryData` (`+0x80`), throwing `NullReferenceException` when a co-op peer joined (`NikoSik` in `Player-prev.log`).
     - Earlier `HumanNpcEquipmentGuard` was gated behind `IsMultiplayerOnline`, missing all NPCs initialized before hosting (`orlov` item `469`, `Time` item `461`, `Jason` item `445`) and missing `EquipMeleeWeapon` (`Charles` item `309`, `Sarah` item `302`). Any unhandled exception inside `LoadEquipments` aborted `Init()` before `hand1Elbow`/`hand2Elbow` (`+0xB30`/`+0xB38`) were bound and aborted `Start()` before `GameController.AddCharacter`, making the NPC invisible to `NetMonsterSync.HostUpdateInterest` and causing 60/sec NREs in `UpdateHand_RangedWeapon`.
   - **Fix:** Rebuilt `HumanNpcEquipmentGuard` to cover all non-local-player humans regardless of `IsMultiplayerOnline`, auto-allocate backing `InventoryData` in `LoadEquipments`/`EquipRangedWeapon`/`EquipMeleeWeapon`, clean up half-equipped weapon state on failure, guard `UpdateHand_RangedWeapon` on `rangedWeapon`, `weaponAttr`, `characterData`, `hand1Elbow`, `hand2Elbow`, `hand1Transform`, `hand2Transform`, and rescue any active host NPCs missing from `GameController.characterList`.

3. **Combat AI, Cease-Fire (`StopFire`/`Passive`), Wild Leader Guard & Ghost Threat Pruning**
   - Hard-zeroed friendly/neutral human threat scores at the top of `SmartCombatAI.AdjustThreatScore` across all doctrines (including `Passive`) and scoped `SetThreatData_Postfix` threat boosts strictly to player teammates targeting actual hostile humans.
   - Added `CeaseFireEliminateThreatGate` (`GOAPGoal_EliminateThreat.IsValid` + `GetPriority`) so `StopFire` / `Passive` strictly prevents ranged discharge even if `EliminateThreat` was active on the prior tick.
   - Pruned dead/disabled/inactive (`!activeInHierarchy`) keys from `agent.threatDataDic` in `CombatResponse.Refresh` so chunk-culled or shadow-layered enemies never block live target acquisition.
   - Fixed `GOAPAgent_IsPlayerTeammate_Postfix` to require `IsAnyAlliedPlayerCharacterId(leaderCharacterGUID)` instead of any non-empty GUID (preventing wild NPC duos from being treated as player teammates).
   - Fixed `CheckPlayerRearFlank` and `CountNearbyHostiles` via `IsActualHostileCharacter` so peaceful camp NPCs, pets, and co-op friends never trigger ambush callouts.
   - Added `NPC_DISMISS` multiplayer packet sync, `AutoCompact` chest rule sync, and `SmartCompanionsInput.CanProcessWorldHotkeys()` (`NetChatBar.isTypingChat` + `GUIUtility.keyboardControl` + live `playerCharacter` check + `Esc` to close `F8`).
