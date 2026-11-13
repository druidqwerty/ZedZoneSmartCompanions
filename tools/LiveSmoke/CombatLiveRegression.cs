using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    // Disposable copied world only. No direct Fire calls: pass requires loaded ammo
    // consumption or the weapon's native fireFrameFlag advancing during normal AI.
    internal static partial class CombatLiveRegression
    {
        private static float _start = -1f, _phaseStart, _seenAt = -1f, _nextEquip;
        private static int _phase = -1, _initialAmmo, _initialFireFrame;
        private static IntPtr _observedGun, _observedWeaponData;
        private static bool _wasReloading;
        private static float _stableWeaponSince;
        private static GOAPAgent _agent;
        private static ZombieController _zombie;
        private static HumanCharacterController _hostileHuman;
        private static Vector2 _originalHumanPosition;
        private static float _originalHumanHp;
        private static int _originalHumanCamp, _originalHumanAttrId;
        private static string _originalHumanLeaderGuid;
        private static bool _originalHumanBandit, _originalHumanInTeamList, _originalHumanInPlayerGuids, _humanWasFrozen, _humanAgentWasEnabled;
        private static Vector2 _originalZombiePosition;
        private static bool _zombieEnabled, _zombieWasFrozen;
        private static float _originalHp;
        private static string _originalCombat, _originalWeapon, _originalBasic;
        private static CompanionRuntimeState _state;
        private static TacticalDoctrine _originalDoctrine;
        private static Vector2 _placement;
        internal static bool Succeeded;
        internal static bool Finished;

        internal static bool Tick()
        {
            if (Finished) return true;
            float now = Time.realtimeSinceStartup;
            if (_start < 0f) _start = now;
            try
            {
                if (_phase < 0)
                {
                    if (now - _start < 8f) return false;
                    bool prepare = now >= _nextEquip;
                    if (prepare) _nextEquip = now + 1f;
                    foreach (var candidate in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        var weapon = candidate.humanCharacter.rangedWeapon;
                        if (prepare)
                        {
                            bool equipped = Il2CppSafe.IsAliveUnity(weapon) || GOAPAction_RangeAttack.EquipBestRangedWeapon(candidate);
                            weapon = candidate.humanCharacter.rangedWeapon;
                            int loaded = Il2CppSafe.IsAliveUnity(weapon) && Il2CppSafe.IsAlive(weapon.weaponData)
                                ? BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData) : -1;
                            int reserve = loaded >= 0 ? BasicRangedWeapon.GetInventoryAmmoTotal(candidate.humanCharacter, weapon.weaponData, 0) : -1;
                            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Prep {candidate.npcData.characterData?.characterName}: equipped={equipped} loaded={loaded} reserve={reserve} range={(weapon != null ? weapon.fireRange : 0f)}.");
                            if (loaded >= 0 && loaded < 4 && reserve > 0 && !candidate.humanCharacter.reloadingMagazineFlag)
                                candidate.CheckAndReloadMagazine();
                        }
                        if (Il2CppSafe.IsAliveUnity(weapon) && Il2CppSafe.IsAlive(weapon.weaponData) &&
                            BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData) >= 4 && weapon.fireRange >= 12f)
                        { _agent = candidate; break; }
                    }
                    var gc = GameController.instance;
                    if (_agent == null)
                    {
                        if (now - _start < 20f) return false;
                        return End(false, "BLOCKED: no active teammate with a loaded ranged weapon (>=4 rounds, range>=12m).");
                    }
                    float nearestZombie = float.MaxValue;
                    foreach (var character in gc.characterList)
                    {
                        if (!Il2CppSafe.IsAliveUnity(character) || character.deadFlag) continue;
                        var zombie = character.TryCast<ZombieController>();
                        if (Il2CppSafe.IsAliveUnity(zombie) && Il2CppSafe.IsAlive(zombie.characterData) &&
                            zombie.characterData.zLevel == _agent.npcData.zLevel &&
                            !zombie.name.Contains("Blast", StringComparison.OrdinalIgnoreCase))
                        {
                            float spawnDistance = Vector2.Distance(zombie.transform.position, gc.playerCharacter.transform.position);
                            if (spawnDistance < nearestZombie) { nearestZombie = spawnDistance; _zombie = zombie; }
                        }
                    }
                    if (_zombie == null)
                    {
                        if (now - _start < 22f) { _agent = null; return false; }
                        return End(false, "BLOCKED: no live native zombie in the loaded area.");
                    }
                    _originalCombat = _agent.npcData.combatCommand;
                    _originalWeapon = _agent.npcData.weaponPreference;
                    _originalBasic = _agent.npcData.basicCommand;
                    _state = CompanionConfigStore.GetOrCreateCompanion(_agent.npcData.npcGUID);
                    _originalDoctrine = _state.Doctrine;
                    _originalZombiePosition = _zombie.transform.position;
                    _zombieEnabled = _zombie.enabled;
                    _zombieWasFrozen = _zombie.isScriptedMoveFreezeActive;
                    _originalHp = _zombie.hp;
                    IsolateArena();
                    RememberActorPositions();
                    // A disabled character may be excluded by native perception. Freeze
                    // movement through the native API, keeping its component alive/active.
                    if (!_zombieWasFrozen) _zombie.BeginScriptedMoveFreeze();
                    // Survive the owner's high-damage/critical weapon rolls through
                    // the cease-fire observation. We verify shots, not target damage.
                    _zombie.hp = 10000000f;
                    if (Il2CppSafe.IsAlive(_zombie.characterData?.characterStatusData))
                    {
                        _zombie.characterData.characterStatusData.maxhp = 10000000f;
                        _zombie.characterData.characterStatusData.hp = 10000000f;
                    }
                    var zAttr = MonsterManager.instance?.GetMonsterAttrById(_zombie.characterData.characterAttrId);
                    if (Il2CppSafe.IsAliveUnity(zAttr)) zAttr.deathShotRate = 0f;
                    _state.Doctrine = TacticalDoctrine.SniperKite;
                    _state.AllowAutoLootNearby = false;
                    _state.AllowAutoDropoffAtBase = false;
                    _state.AllowBaseChores = false;
                    PhysicalMovementHelper.CancelAllPhysicalTasksOnCombat(_agent, _state);
                    // Returning-to-base deliberately suppresses native combat. Put the
                    // disposable test NPC on the normal escort order, not a forced goal.
                    _agent.npcData.ExecuteNPCCommand("Basic.FollowPlayer");
                    SmartCombatAI.ApplyDoctrineToNpcData(_agent, _state);
                    _agent.SelectPriorityGoalExecute();
                    if (_agent.npcData.combatCommand != "FireAtWill" || _agent.npcData.weaponPreference != "RangedWeaponOnly")
                        return End(false, "FAIL: doctrine is not stored in native bare-leaf format.");
                    _phase = 0;
                    StartPhase(now);
                    return false;
                }

                if (_zombie.deadFlag || _agent.humanCharacter.deadFlag) return End(false, "FAIL: participant died; target="+_zombie.deadFlag+", NPC="+_agent.humanCharacter.deadFlag);
                IsolateArena(log: false);
                DiagnosePhase(now);
                var gun = _agent.humanCharacter.rangedWeapon;
                if (!Il2CppSafe.IsAliveUnity(gun) || !Il2CppSafe.IsAlive(gun.weaponData))
                    return End(false, "FAIL: loaded ranged weapon/data was lost during test.");
                float requiredRange = _phase == 1 ? 12f : 8f;
                if (_phase < 2 && gun.fireRange + 0.01f < requiredRange && now - _phaseStart > 2f)
                    return End(false, $"BLOCKED: native weapon selection uses weaponId={gun.weaponData.itemId} range={gun.fireRange:F2}m for the {requiredRange:F0}m phase; target={DescribeCurrentTarget()}. A suitable weapon/arena fixture is required.");
                float distance = _phase == 3 && Il2CppSafe.IsAliveUnity(_hostileHuman)
                    ? Vector2.Distance(_agent.humanCharacter.transform.position, _hostileHuman.transform.position)
                    : Vector2.Distance(_agent.humanCharacter.transform.position, _zombie.transform.position);
                if (_phase == 3 && Il2CppSafe.IsAliveUnity(_hostileHuman))
                    _hostileHuman.movementInput = Vector2.zero;
                var expectedTarget = _phase == 3 && Il2CppSafe.IsAliveUnity(_hostileHuman)
                    ? (BasicCharacterController)_hostileHuman
                    : (BasicCharacterController)_zombie;
                bool acquired = Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter) &&
                    _agent.mostThreateningCharacter.Pointer == expectedTarget.Pointer;
                if (acquired && _seenAt < 0f)
                {
                    _seenAt = now;
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Acquired phase={_phase} target={expectedTarget.name} after={now-_phaseStart:F2}s distance={distance:F2}m sight={_agent.sightRange:F2}.");
                }
                bool fired = ObserveDischarge(gun, now);
                if ((_phase < 2 || _phase == 3) && fired)
                {
                    if (gun.fireRange + 0.01f < requiredRange)
                        return End(false, $"BLOCKED: weaponId={gun.weaponData.itemId} range={gun.fireRange:F2}m discharged during the {requiredRange:F0}m phase; this does not verify the requested ranged fixture.");
                    if (!acquired)
                        return End(false, "BLOCKED: native AI discharged while selecting another threat: " + DescribeCurrentTarget() + "; isolate the test arena before attributing this shot to the intended target.");
                    if (distance < 6f) return End(false, "FAIL: discharge was not at the ranged test target/distance.");
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] PASS native AI discharge phase={_phase} target={expectedTarget.name} distance={distance:F2}m after={now-_phaseStart:F2}s.");
                    if (_phase == 3)
                        return End(true, "PASS ranged acquisition/discharge at 8m & 12m (Zombie), StopFire negative case, v1.4.0 MP Sync & 8m Hostile Human NPC ranged discharge.");
                    _phase++;
                    StartPhase(now);
                }
                else if (_phase == 2)
                {
                    if (fired) return End(false, "FAIL: native StopFire permitted a shot.");
                    // A gun replacement is not a shot, but it starts a fresh full
                    // cease-fire observation window rather than shortening this check.
                    if (now - _stableWeaponSince > 3f)
                    {
                        VerifyV140MultiplayerSyncInEngine();
                        _phase = 3;
                        StartPhase(now);
                    }
                }
                else if (now - _phaseStart > 12f)
                {
                    return End(false, $"FAIL: no native shot within 12s phase={_phase}, distance={distance:F2}m acquired={acquired}, sight={_agent.sightRange:F2}, command={_agent.npcData.combatCommand}, target={DescribeCurrentTarget()}.");
                }
            }
            catch (Exception error) { return End(false, "FAIL: " + error); }
            return false;
        }

        private static void VerifyV140MultiplayerSyncInEngine()
        {
            var testData = new CharacterData { characterAttrId = 0, camp = 21, characterName = "TestLooter" };
            MultiplayerSyncSystem.NormalizeHumanCharacterData(testData, isLooterHint: true);
            if (testData.characterAttrId != MultiplayerSyncSystem.LooterMonsterAttrId ||
                MultiplayerSyncSystem.LooterMonsterAttrId != 200 ||
                testData.camp != MultiplayerSyncSystem.RemappedLooterCamp)
                throw new Exception($"NormalizeHumanCharacterData looter failed: attr={testData.characterAttrId}, camp={testData.camp}");

            // v1.6.0 Test A: Legacy 202 (SecurityGuard) corruption on ordinary survivor is repaired to 201 (Survivor)
            var legacyCorruptedSurvivor = new CharacterData { characterAttrId = 202, camp = 2, characterName = "TestSurvivorFemale", gender = 1 };
            MultiplayerSyncSystem.NormalizeHumanCharacterData(legacyCorruptedSurvivor, isLooterHint: false);
            if (legacyCorruptedSurvivor.characterAttrId != 201 || legacyCorruptedSurvivor.camp != 2)
                throw new Exception($"v1.6.0 legacy 202->201 repair failed: attr={legacyCorruptedSurvivor.characterAttrId}, camp={legacyCorruptedSurvivor.camp}");

            var attr200 = MonsterManager.instance.GetMonsterAttrById(MultiplayerSyncSystem.LooterMonsterAttrId);
            if (!Il2CppSafe.IsAliveUnity(attr200) || attr200.monsterThreatLevel < 25f || attr200.monsterThreatRange < 28f)
                throw new Exception("MonsterAttr(200 Looter) threat level/range not configured!");

            // v1.6.0 Test B: Player CharacterData (characterAttrId=0) is NEVER overwritten by NormalizeHumanCharacterData or SetThreatData,
            // and AdjustThreatScore zeroes out friendly player/ally targets!
            var gcPlayer = GameController.instance.playerCharacter;
            int origPlayerAttr = gcPlayer.characterData.characterAttrId;
            int origPlayerCamp = gcPlayer.characterData.camp;
            MultiplayerSyncSystem.NormalizeHumanCharacterData(gcPlayer.characterData, isLooterHint: true);
            if (gcPlayer.characterData.characterAttrId != origPlayerAttr || gcPlayer.characterData.camp != origPlayerCamp)
                throw new Exception($"v1.6.0 PLAYER CORRUPTION: NormalizeHumanCharacterData modified player attr={gcPlayer.characterData.characterAttrId} camp={gcPlayer.characterData.camp}!");
            float friendlyScore = 50f;
            SmartCombatAI.AdjustThreatScore(_agent, gcPlayer, ref friendlyScore);
            if (friendlyScore != 0f)
                throw new Exception($"v1.6.0 friendly player threat score was not zeroed: {friendlyScore}!");

            // v1.6.0 Test C: NPC_DISMISS multiplayer sync packet clears ownership cleanly
            var dismissSyncState = CompanionConfigStore.GetOrCreateCompanion("test_mp_dismiss_npc", "TestDismissNpc");
            dismissSyncState.OwnerCharacterId = "test_owner_id";
            dismissSyncState.IsDismissedOrNotInTeam = false;
            var dismissMsg = new ZedZone.Net.MsgChat
            {
                senderId = 2,
                senderName = "CoopFriend",
                text = MultiplayerSyncSystem.PacketPrefix + "NPC_DISMISS|test_mp_dismiss_npc"
            };
            if (!MultiplayerSyncSystem.TryHandleIncomingChatPacket(dismissMsg, null, receivedOnHost: false) ||
                !dismissSyncState.IsDismissedOrNotInTeam || !string.IsNullOrEmpty(dismissSyncState.OwnerCharacterId))
                throw new Exception("v1.6.0 NPC_DISMISS packet did not clear companion ownership!");
            CompanionConfigStore.CompanionStates.Remove("test_mp_dismiss_npc");

            var warnGoal = new GOAPGoal_WarningHumanEnemy();
            if (warnGoal.IsValid(_agent))
                throw new Exception("GOAPGoal_WarningHumanEnemy.IsValid was not suppressed for player teammate!");

            var boxMsg = new ZedZone.Net.MsgChat
            {
                senderId = 2,
                senderName = "CoopFriend",
                text = MultiplayerSyncSystem.PacketPrefix + "BOX|test_mp_box|12|1|4|10.5|20.5|CoopAmmoBox"
            };
            if (!MultiplayerSyncSystem.TryHandleIncomingChatPacket(boxMsg, null, receivedOnHost: false))
                throw new Exception("TryHandleIncomingChatPacket did not intercept BOX sync packet!");
            if (!CompanionConfigStore.ContainerRules.TryGetValue("test_mp_box", out var syncedRule) ||
                (int)syncedRule.Categories != 12 || !syncedRule.IsDropoffSource || syncedRule.Priority != 4 || syncedRule.CustomName != "CoopAmmoBox")
                throw new Exception("BOX sync packet did not apply container rule!");
            CompanionConfigStore.ContainerRules.Remove("test_mp_box");

            // Verify v1.4.1 World Container Discovery & Unique Box ID Isolation
            var gc = GameController.instance;
            Vector2 savedPlayerPos = gc.playerCharacter.transform.position;
            gc.playerCharacter.TeleportTo(_originalPlayerPosition, false);
            var scanned = BaseSortingSystem.ScanNearbyContainers(force: true);
            gc.playerCharacter.TeleportTo(savedPlayerPos, false);
            if (scanned.Count < 2)
                throw new Exception($"ScanNearbyContainers found only {scanned.Count} containers near base without opening them (expected >= 2)!");
            var seenIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in scanned)
            {
                if (string.IsNullOrEmpty(c.ContainerId) || c.ContainerId == "unknown_box" || c.ContainerId == "unknown")
                    throw new Exception("Invalid container ID: " + c.ContainerId);
                if (!seenIds.Add(c.ContainerId))
                    throw new Exception("Duplicate container ID between distinct boxes: " + c.ContainerId);
            }
            bool origDrop0 = scanned[0].Rule.IsDropoffSource;
            bool origDrop1 = scanned[1].Rule.IsDropoffSource;
            scanned[0].Rule.IsDropoffSource = !origDrop1;
            if (scanned[1].Rule.IsDropoffSource != origDrop1)
                throw new Exception("Toggling IsDropoffSource on box 0 mutated box 1!");
            scanned[0].Rule.IsDropoffSource = origDrop0;

            // Verify v1.4.1 Physical Carry Buffer (Phase 1 pickup into CarryBuffer -> Phase 2 deposit at destination)
            var srcInv = new InventoryData { inventorySize = new Vector2Int(6, 6), worldposition = new Vector2(100f, 100f), inventoryId = "pos_1000_1000_0" };
            var dstInv = new InventoryData { inventorySize = new Vector2Int(6, 6), worldposition = new Vector2(110f, 100f), inventoryId = "pos_1100_1000_0" };
            var medAttr = ItemManager.instance.GetItemAttrById(220);
            var testMed = ItemManager.instance.GetRandomDataByAttr(medAttr, false, false);
            if (!InventoryTransfers.TryAddWholeCopy(srcInv, testMed, out var addedMed))
                throw new Exception("Failed to add test medicine item to srcInv");
            if (!InventoryTransfers.CanAcceptWholeItem(dstInv, addedMed))
                throw new Exception("CanAcceptWholeItem returned false on empty dstInv");
            if (!InventoryTransfers.TryTakeIntoCarryBuffer(srcInv, addedMed, out var carriedMed) || srcInv.itemList.Count != 0)
                throw new Exception("TryTakeIntoCarryBuffer did not remove item from srcInv into CarryBuffer");
            _state.CarryBuffer.Add(new CarryItemEntry
            {
                Item = carriedMed,
                FallbackSourceInv = srcInv,
                TargetContainerId = "pos_1100_1000_0",
                TargetInv = dstInv,
                TargetPos = new Vector2(110f, 100f),
                TargetDisplayName = "TestMedBox",
                Category = StorageCategory.Medicine
            });
            BaseSortingSystem.FlushCarryBufferSafely(_state);
            if (_state.CarryBuffer.Count != 0 || srcInv.itemList.Count != 1)
                throw new Exception("FlushCarryBufferSafely did not restore carried item back to srcInv!");

            // Verify v1.4.2 ItemCategorizer with real ItemAttr objects (regression guard for HasFeature(Bandage))
            var gunItem = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(406), false, false);
            var knifeItem = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(352), false, false);
            var steakItem = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(248), false, false);
            if (ItemCategorizer.ClassifyItem(gunItem) != StorageCategory.RangedWeapons ||
                ItemCategorizer.ClassifyItem(knifeItem) != StorageCategory.MeleeWeapons ||
                ItemCategorizer.ClassifyItem(steakItem) != StorageCategory.FreshFood)
            {
                throw new Exception($"ItemCategorizer real-ItemAttr check failed: gun={ItemCategorizer.ClassifyItem(gunItem)}, knife={ItemCategorizer.ClassifyItem(knifeItem)}, steak={ItemCategorizer.ClassifyItem(steakItem)}");
            }

            var filteredUi = BaseSortingSystem.GetFilteredContainersForUI(forceScan: false);
            if (filteredUi.Count >= scanned.Count)
                throw new Exception($"GetFilteredContainersForUI did not filter distant unmarked chests: filtered={filteredUi.Count}, total={scanned.Count}");

            // Verify v1.5.0 Test 1: Companion Dismissal (QuitTeam / RemoveTeamNPC) does NOT resurrect leaderCharacterGUID
            GOAPAgent dismissTarget = null;
            foreach (var mate in SmartChatterSystem.GetActiveTeammateAgents())
            {
                if (Il2CppSafe.IsAliveUnity(mate) && mate.Pointer != _agent.Pointer)
                {
                    dismissTarget = mate;
                    break;
                }
            }
            if (Il2CppSafe.IsAliveUnity(dismissTarget) && Il2CppSafe.IsAlive(dismissTarget.npcData))
            {
                string origLeader = dismissTarget.npcData.leaderCharacterGUID;
                var stDismiss = CompanionConfigStore.GetOrCreateCompanion(dismissTarget.npcData.npcGUID);
                stDismiss.OwnerCharacterId = gc.playerCharacter.characterData.characterId;
                gc.playerCharacter.characterData.RemoveTeamNPC(dismissTarget.npcData);
                if (GOAPNPCManager.instance?.teammateNPCs != null)
                    GOAPNPCManager.instance.teammateNPCs.Remove(dismissTarget.npcData);
                dismissTarget.npcData.ExecuteNPCCommand("QuitTeam");
                MultiplayerSyncSystem.SyncCompanionOwnerAndLeader(dismissTarget);
                MultiplayerSyncSystem.SyncCompanionOwnerAndLeader(dismissTarget);
                if (!string.IsNullOrEmpty(dismissTarget.npcData.leaderCharacterGUID) ||
                    Il2CppSafe.IsAliveUnity(dismissTarget.leaderCharacter) ||
                    !stDismiss.IsDismissedOrNotInTeam)
                {
                    throw new Exception("Dismissed companion was re-bound to player by SyncCompanionOwnerAndLeader!");
                }
                // Re-recruit back for lab restoration
                dismissTarget.npcData.leaderCharacterGUID = origLeader;
                gc.playerCharacter.characterData.teamNPCGuids.Add(dismissTarget.npcData.npcGUID);
                GOAPNPCManager.instance?.teammateNPCs?.Add(dismissTarget.npcData);
                MultiplayerSyncSystem.OnCompanionRecruited(gc.playerCharacter.characterData, dismissTarget.npcData);
                MultiplayerSyncSystem.SyncCompanionOwnerAndLeader(dismissTarget);
                if (stDismiss.IsDismissedOrNotInTeam || string.IsNullOrEmpty(dismissTarget.npcData.leaderCharacterGUID))
                    throw new Exception("Re-recruited companion did not restore team state!");
            }

            // Verify v1.5.0 Test 2: Draggable UI & ResetUiPositions
            CompanionConfigStore.HudBadgePosX = 420f;
            CompanionConfigStore.HudBadgePosY = 14f;
            CompanionConfigStore.ResetUiPositions();
            if (CompanionConfigStore.HudBadgePosX != -1f || !CompanionConfigStore.ShowHudBadge || CompanionConfigStore.HudBadgeCollapsed)
                throw new Exception("ResetUiPositions failed to reset HUD badge coordinates!");

            // Verify v1.5.0 Test 3: MaxTeamSize up to 15
            int prevMaxTeam = CharacterData.maxTeamNPCNumber;
            CompanionConfigStore.MaxTeamSize = 12;
            CompanionConfigStore.ApplyMaxTeamSizeIfConfigured();
            if (CharacterData.maxTeamNPCNumber != 12)
                throw new Exception($"MaxTeamSize failed to apply: expected 12, got {CharacterData.maxTeamNPCNumber}");
            CompanionConfigStore.MaxTeamSize = 0;
            CharacterData.maxTeamNPCNumber = prevMaxTeam;

            // Verify v1.5.0 Test 4: CollectResource 20m priority & Base Chest Protection
            string savedBasicCmd = _agent.npcData.basicCommand;
            _agent.npcData.basicCommand = "CollectResource";
            _agent.agentState.basicCommand = "CollectResource";
            if (_agent.agentState.IsTeammateScavengeRestricted())
                throw new Exception("IsTeammateScavengeRestricted was not lifted for CollectResource!");
            float followPrio = new GOAPGoal_FollowLeader().GetPriority(_agent.agentState);
            if (followPrio > 2.0f)
                throw new Exception($"FollowLeader priority ({followPrio}) was not suppressed during CollectResource!");
            _agent.npcData.basicCommand = savedBasicCmd;
            _agent.agentState.basicCommand = savedBasicCmd;

            var baseBox = scanned[0];
            bool prevMarked = baseBox.Rule.IsMarkedByPlayer;
            baseBox.Rule.IsMarkedByPlayer = true;
            if (Il2CppSafe.IsAliveUnity(baseBox.TerrainObj) &&
                _agent.agentState.IsContainerSearchableByNPC(baseBox.TerrainObj, (GOAPAgentState.TeammateLootPolicy)0, false))
            {
                throw new Exception("Marked base container was not protected from NPC scavenging in IsContainerSearchableByNPC!");
            }
            baseBox.Rule.IsMarkedByPlayer = prevMarked;

            // Verify v1.6.0 Test D: Wild NPC with non-player leaderCharacterGUID is NOT treated as player teammate,
            // and EquipmentGuard skips invalid weapon items without throwing NRE or breaking ranged state!
            if (Il2CppSafe.IsAliveUnity(dismissTarget) && Il2CppSafe.IsAlive(dismissTarget.npcData))
            {
                string savedLeader = dismissTarget.npcData.leaderCharacterGUID;
                var stWild = CompanionConfigStore.GetOrCreateCompanion(dismissTarget.npcData.npcGUID);
                bool savedDismissed = stWild.IsDismissedOrNotInTeam;
                gc.playerCharacter.characterData.RemoveTeamNPC(dismissTarget.npcData);
                GOAPNPCManager.instance?.teammateNPCs?.Remove(dismissTarget.npcData);
                stWild.IsDismissedOrNotInTeam = false;
                dismissTarget.npcData.leaderCharacterGUID = "non_existent_wild_npc_leader_guid";
                if (dismissTarget.IsPlayerTeammate())
                    throw new Exception("v1.6.0 wild NPC with non-player leaderCharacterGUID falsely matched IsPlayerTeammate!");
                dismissTarget.npcData.leaderCharacterGUID = savedLeader;
                stWild.IsDismissedOrNotInTeam = savedDismissed;
                gc.playerCharacter.characterData.teamNPCGuids.Add(dismissTarget.npcData.npcGUID);
                GOAPNPCManager.instance?.teammateNPCs?.Add(dismissTarget.npcData);

                // EquipmentGuard: pass invalid/non-ranged ItemData (knife 352) to EquipRangedWeapon on non-player NPC -> must NOT throw or break NPC!
                var savedWeapon = dismissTarget.humanCharacter.rangedWeapon;
                dismissTarget.humanCharacter.EquipRangedWeapon(knifeItem, false);
                if (!Il2CppSafe.IsAlive(dismissTarget.humanCharacter.characterData.inventoryData))
                    throw new Exception("v1.6.0 EquipmentGuard did not preserve/ensure backing inventoryData!");
                if (Il2CppSafe.IsAliveUnity(savedWeapon) && Il2CppSafe.IsAlive(savedWeapon.weaponData))
                    dismissTarget.humanCharacter.EquipRangedWeapon(savedWeapon.weaponData, false);
            }

            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] PASS v1.6.0 Player/Looter(200)/Survivor(201) Guard, Legacy 202 Repair, Wild Leader Guard, EquipmentGuard, QuitTeam Dismissal, Draggable UI, MaxTeamSize=12, CollectResource & Base Chest Protection, Real ItemAttr Categorizer, Filtered UI (filtered={filteredUi.Count}/{scanned.Count}), & Invisible CarryBuffer checks.");
        }

        private static void StartPhase(float now)
        {
            float distance = _phase == 1 ? 12f : 8f;
            bool found = FindOpenTestPlacement(distance);
            if (!found) throw new Exception("BLOCKED: no clear native fire line at " + distance + "m.");
            if (_phase == 3)
            {
                SetupHostileHumanTarget();
                _zombie.gameObject.SetActive(false);
                _hostileHuman.TeleportTo(_placement, false);
                _agent.humanCharacter.LookAtPosition(_placement, true);
                _state.Doctrine = TacticalDoctrine.SniperKite;
                SmartCombatAI.ApplyDoctrineToNpcData(_agent, _state);
                MultiplayerSyncSystem.NormalizeHumanCharacterData(_hostileHuman.characterData, isLooterHint: true);
            }
            else
            {
                _zombie.TeleportTo(_placement, false);
                _agent.humanCharacter.LookAtPosition(_placement, true);
                if (_phase == 2)
                {
                    _state.Doctrine = TacticalDoctrine.Passive;
                    SmartCombatAI.ApplyDoctrineToNpcData(_agent, _state);
                }
            }
            _phaseStart = now;
            _seenAt = -1f;
            IsolateArena(log: false);
            var gun = _agent.humanCharacter.rangedWeapon;
            ResetDischargeBaseline(gun, now);
            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Begin phase={_phase} distance={distance}m rounds={_initialAmmo} weaponId={gun.weaponData.itemId} range={gun.fireRange:F2} command={_agent.npcData.combatCommand}.");
        }

        private static void SetupHostileHumanTarget()
        {
            var gc = GameController.instance;
            var mgr = GOAPNPCManager.instance;
            foreach (var ch in gc.characterList)
            {
                if (!Il2CppSafe.IsAliveUnity(ch) || ch.deadFlag || ch.isPlayer) continue;
                var h = ch.TryCast<HumanCharacterController>();
                if (!Il2CppSafe.IsAliveUnity(h) || h.Pointer == _agent.humanCharacter.Pointer) continue;
                _hostileHuman = h;
                break;
            }
            if (!Il2CppSafe.IsAliveUnity(_hostileHuman))
                throw new Exception("BLOCKED: no second HumanCharacterController in lab scene for Phase 3 hostile human combat test.");

            _originalHumanPosition = _hostileHuman.transform.position;
            _originalHumanHp = _hostileHuman.hp;
            _originalHumanCamp = _hostileHuman.characterData.camp;
            _originalHumanAttrId = _hostileHuman.characterData.characterAttrId;
            _humanWasFrozen = _hostileHuman.isScriptedMoveFreezeActive;

            var ag = _hostileHuman.GetCachedGOAPAgent();
            if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAlive(ag.npcData))
            {
                _originalHumanLeaderGuid = ag.npcData.leaderCharacterGUID;
                _originalHumanBandit = ag.npcData.isBandit;
                _originalHumanInTeamList = mgr.teammateNPCs != null && mgr.teammateNPCs.Contains(ag.npcData);
                _originalHumanInPlayerGuids = gc.playerCharacter?.characterData?.teamNPCGuids != null &&
                    gc.playerCharacter.characterData.teamNPCGuids.Contains(ag.npcData.npcGUID);

                ag.npcData.leaderCharacterGUID = "";
                ag.npcData.isBandit = true;
                if (_originalHumanInTeamList) mgr.teammateNPCs.Remove(ag.npcData);
                if (_originalHumanInPlayerGuids) gc.playerCharacter.characterData.teamNPCGuids.Remove(ag.npcData.npcGUID);
                ag.leaderCharacter = null;
                _humanAgentWasEnabled = ag.enabled;
                ag.enabled = false;
            }

            // Start with vanilla Looter camp=21 and characterAttrId=0 to verify our fix normalizes it!
            _hostileHuman.characterData.camp = 21;
            _hostileHuman.characterData.characterAttrId = 0;
            _hostileHuman.hp = 10000000f;
            if (Il2CppSafe.IsAlive(_hostileHuman.characterData?.characterStatusData))
            {
                _hostileHuman.characterData.characterStatusData.maxhp = 10000000f;
                _hostileHuman.characterData.characterStatusData.hp = 10000000f;
            }
            _hostileHuman.movementInput = Vector2.zero;
            if (!_humanWasFrozen) _hostileHuman.BeginScriptedMoveFreeze();
        }

        private static void ResetDischargeBaseline(BasicRangedWeapon gun, float now)
        {
            _observedGun = gun.Pointer;
            _observedWeaponData = gun.weaponData.Pointer;
            _initialAmmo = BasicRangedWeapon.GetLoadedAmmo(gun.weaponData);
            _initialFireFrame = gun.fireFrameFlag;
            _wasReloading = gun.isReloading || _agent.humanCharacter.reloadingMagazineFlag;
            _stableWeaponSince = now;
        }

        private static bool ObserveDischarge(BasicRangedWeapon gun, float now)
        {
            if (gun.Pointer != _observedGun || gun.weaponData.Pointer != _observedWeaponData)
            {
                LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Weapon changed phase={_phase}: weaponId={gun.weaponData.itemId} range={gun.fireRange:F2} loaded={BasicRangedWeapon.GetLoadedAmmo(gun.weaponData)} target={DescribeCurrentTarget()}; resetting shot baseline (NOT a discharge).");
                ResetDischargeBaseline(gun, now);
                return false;
            }
            int ammunition = BasicRangedWeapon.GetLoadedAmmo(gun.weaponData);
            int fireFrame = gun.fireFrameFlag;
            bool reloading = gun.isReloading || _agent.humanCharacter.reloadingMagazineFlag;
            bool frameAdvanced = fireFrame > _initialFireFrame;
            bool consumedAmmo = ammunition < _initialAmmo && !reloading && !_wasReloading;
            if (frameAdvanced || consumedAmmo)
                LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Same-weapon discharge evidence phase={_phase} weaponId={gun.weaponData.itemId} range={gun.fireRange:F2} ammo={_initialAmmo}->{ammunition} fireFrame={_initialFireFrame}->{fireFrame} reloading={reloading}.");
            // Roll the same-weapon baseline forward, including reload transitions.
            // Magazine removal/replacement must not be counted as ammunition fired.
            _initialAmmo = ammunition;
            _initialFireFrame = fireFrame;
            _wasReloading = reloading;
            return frameAdvanced || consumedAmmo;
        }

        private static string DescribeCurrentTarget()
        {
            var target = _agent.mostThreateningCharacter;
            if (!Il2CppSafe.IsAliveUnity(target)) return "none";
            float distance = Vector2.Distance(_agent.humanCharacter.transform.position, target.transform.position);
            var expected = _phase == 3 && Il2CppSafe.IsAliveUnity(_hostileHuman) ? _hostileHuman.Pointer : _zombie.Pointer;
            return $"{target.name}#{target.GetInstanceID()} at {distance:F2}m (testTarget={target.Pointer == expected})";
        }

        private static bool End(bool success, string message)
        {
            Succeeded = success;
            Finished = true;
            if (success) LiveSmokePlugin.Logger.LogInfo("[CombatLive] " + message);
            else LiveSmokePlugin.Logger.LogError("[CombatLive] " + message);
            try
            {
                if (_state != null) _state.Doctrine = _originalDoctrine;
                if (Il2CppSafe.IsAliveUnity(_agent) && _originalCombat != null)
                {
                    _agent.npcData.combatCommand = _originalCombat;
                    _agent.npcData.weaponPreference = _originalWeapon;
                    _agent.agentState.combatCommand = _originalCombat;
                    _agent.agentState.weaponPreference = _originalWeapon;
                    if (!string.IsNullOrEmpty(_originalBasic))
                        _agent.npcData.ExecuteNPCCommand("Basic." + _originalBasic);
                    _agent.executeSelectPriorityGoalFlag = true;
                }
                if (Il2CppSafe.IsAliveUnity(_hostileHuman))
                {
                    _hostileHuman.TeleportTo(_originalHumanPosition, false);
                    _hostileHuman.hp = _originalHumanHp;
                    if (Il2CppSafe.IsAlive(_hostileHuman.characterData))
                    {
                        _hostileHuman.characterData.camp = _originalHumanCamp;
                        _hostileHuman.characterData.characterAttrId = _originalHumanAttrId;
                    }
                    var ag = _hostileHuman.GetCachedGOAPAgent();
                    if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAlive(ag.npcData))
                    {
                        ag.npcData.leaderCharacterGUID = _originalHumanLeaderGuid;
                        ag.npcData.isBandit = _originalHumanBandit;
                        if (_originalHumanInTeamList && GOAPNPCManager.instance?.teammateNPCs != null &&
                            !GOAPNPCManager.instance.teammateNPCs.Contains(ag.npcData))
                            GOAPNPCManager.instance.teammateNPCs.Add(ag.npcData);
                        if (_originalHumanInPlayerGuids && GameController.instance?.playerCharacter?.characterData?.teamNPCGuids != null &&
                            !GameController.instance.playerCharacter.characterData.teamNPCGuids.Contains(ag.npcData.npcGUID))
                            GameController.instance.playerCharacter.characterData.teamNPCGuids.Add(ag.npcData.npcGUID);
                        ag.enabled = _humanAgentWasEnabled;
                    }
                    if (!_humanWasFrozen) _hostileHuman.EndScriptedMoveFreeze();
                }
                if (Il2CppSafe.IsAliveUnity(_zombie))
                {
                    _zombie.gameObject.SetActive(true);
                    _zombie.TeleportTo(_originalZombiePosition, false);
                    _zombie.hp = _originalHp;
                    _zombie.enabled = _zombieEnabled;
                    if (!_zombieWasFrozen) _zombie.EndScriptedMoveFreeze();
                }
                RestoreActorPositions();
                RestoreArena();
            }
            catch (Exception error) { LiveSmokePlugin.Logger.LogWarning("[CombatLive] Lab cleanup: " + error.Message); }
            return true;
        }
    }
}
