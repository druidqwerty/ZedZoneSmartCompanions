# 1.4.0 verification — PASS

RELEASE_READY: true

## Final build

- Release build with warnings treated as errors (`--warnaserror`): 0 warnings, 0 errors.
- DLL SHA-256: `557cd4e0341cb089c4f669b72fbeab5c7e4ea9618e63ec6c7cb506fa12bd9be8`.
- BepInEx startup: 41 native methods patched; `[SelfVerify] PASS v1.4.0`.
- Final live combat & MP sync run (`docs/combat-acceptance-1.4.0.log`): zero error entries and zero exceptions.

## Root causes resolved in v1.4.0

1. **Bug 1 — Fire-lighting menu opening on local player's screen when friend opens a container in MP:**
   - Native disassembly of `TerrainObject_FirePlace.IgnitionFire(bool)` (`0x5B29F0`) showed that calling `IgnitionFire(true)` from `BaseChoreSystem` invoked `ToolSelectManager.SelectTool_Player` (`0x1529D30`) on the local player's screen.
   - Fixed by guarding `BaseChoreSystem` on MP clients (`NetworkManager.isClient`), igniting fueled campfires directly via `TerrainObject_FirePlace.OnIgnite()` (`0x5B4450`) after checking `ProductionManager.FindLastCombustibleIndex(fp.fuelInventoryData) >= 0`, and adding a Harmony prefix guard on `TerrainObject_FirePlace.IgnitionFire(true)` when `LootInventoryPanel` is active.

2. **Bug 2 — NPCs invisible/missing on MP Clients & Per-Player Companion Ownership + Cross-Player Healing:**
   - Native disassembly of `NetMonsterSync.SpawnPuppet` (`0xBD0840`) showed that at `0xBD0A37` it calls `MonsterManager.GetMonsterAttrById(data.characterAttrId)` and aborts puppet creation whenever `characterAttrId == 0` (`ScavengerCamp` human NPCs leave `characterAttrId = 0`). Furthermore, `BasicCharacterController.Start()` sets `inSightFlag = 1` after `Init() -> OnExitSight()`, leaving client puppet `spriteParent` disabled.
   - Fixed via `MultiplayerSyncSystem`: normalizes human `CharacterData.characterAttrId` (`201` Survivor / `202` Looter) before `NetMonsterSync.SerializeCharacterData` and after `DeserializeCharacterData`, clears stale `m_spawnBytesScratch` entries on Host, calls `HostResendSpawn` & `HostSendEquipment`, activates client puppet `spriteParent` (`OnEnterScreen()` / `OnEnterSight()`), binds `GOAPNPCData` on client puppets, tracks per-player companion ownership (`leaderCharacterGUID` / `OwnerPlayerName`), syncs remote player leader position & `zLevel`, and allows any player's companions to heal/rescue injured allied co-op players (`HEAL` packet sync).

3. **Bug 3 — Companions not shooting at hostile human NPCs:**
   - Native disassembly of `GOAPAgent.UpdateAgentSight` (`0x679CC3`) revealed a modulo-10 camp check (`(target.camp % 10) == (self.camp % 10)`), which caused companions (`camp = 1`) to treat Looters (`camp = 21`, since `21 % 10 == 1`) as same-camp allies! In addition, `GOAPGoal_WarningHumanEnemy` (`0x8B1AB0`, priority `100`) preempted `GOAPGoal_EliminateThreat` / `GOAPGoal_EliminateTargetEnemy`, and human `MonsterAttr` entries (`201`/`202`) had `0` threat level/range in `SetThreatData` (`0x675FE0`).
   - Fixed by remapping Looter `camp = 21 -> 23` (`CharacterData.IsHostileCamp(23) == true`, `23 % 10 != 1 % 10`), configuring `MonsterAttr(201/202)` threat values (`>= 25` threat, `>= 28m` range), suppressing `GOAPGoal_WarningHumanEnemy.IsValid` for player teammates, guarding `GOAPAgent.SetThreatData` against duplicate `threatDataDic` keys, and setting `hasTargetEnemy` / `targetEnemyInSight` / `isThreatened` when a hostile human target is acquired.

4. **Full Multiplayer Base Container Category & Doctrine Sync:**
   - Synchronizes container category assignments (`ContainerRuleProfile`) and companion tactical doctrines across Host and Clients via `TerrainObjectData` property slot `7` (`SC14:...`) and silent `MsgChat` sync packets (`\u200B[SC14]|BOX|...`, `NPC|...`, `HEAL|...`, `SORT`, `LOOT`, `REQ_SYNC`) intercepted before `NetChatSync.Append`.

## Real native firing & MP sync verification in a copied world

| Final acceptance case | Observed result |
|---|---|
| Phase 0 — Zombie 8 m away | Acquired in 0.09 s; actual rifle shot (`weaponId=406`, `ammo 8->7`, `fireFrame -10->1570`) in 1.65 s |
| Phase 1 — Zombie 12 m away | Acquired in 0.04 s; actual rifle shot (`ammo 7->6`, `fireFrame 1570->1577`) in 0.29 s |
| Phase 2 — StopFire negative case | Immediate goal reselection (`FollowLeader`); 0 shots over full 3.0 s window |
| v1.4.0 MP Sync & Warning checks | `NormalizeHumanCharacterData` (`attr=202`, `camp=23`), `MonsterAttr(202)` (`threat=25/28`), `WarningHumanEnemy.IsValid == false`, `BOX` packet rule sync verified |
| Phase 3 — Hostile Human NPC 8 m away | Acquired `Survivor(Clone)` in 0.01 s; actual rifle shot (`ammo 6->5`, `fireFrame -10->1918`) in 1.01 s |

Evidence: [combat-acceptance-1.4.0.log](combat-acceptance-1.4.0.log).

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
