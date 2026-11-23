using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    /// <summary>
    /// 3.1.2 live engine regressions:
    /// (a) Equipped backpack + sharedInventoryData unloading to base chests while a distant (>12m) hostile is active
    ///     (verifying CombatResponse.Refresh does NOT flush CarryBuffer and both items arrive at chest);
    /// (b) Autonomous bandaging & healing from nested bag in sharedInventoryData
    ///     (including pure bandage with hp <= 0 on active bleeding at >= 80% HP, and medkit healing from nested bag);
    /// (c) Vehicle boarding yield with passengerSeatLimit == -1 (IsTransientVehicleOverflow == false, ShouldYield == true);
    /// (d) CampGarrison exit resetting stale distant npcStayPosition + CampGarrisonSystem.ShouldBlockVanillaReturnToBase.
    /// </summary>
    internal static class V312LiveRegression
    {
        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool Finished { get; private set; }
        public static bool Succeeded { get; private set; }
        internal static bool ArenaActive;

        private static int _phase = -1;
        private static float _phaseStart;
        private static GOAPAgent _agent;
        private static CompanionRuntimeState _state;
        private static Vector2 _origPlayerPos, _origAgentPos;
        private static DiscoveredContainer _destChest;
        private static int _matItemId;
        private static int _destInitialUnits;
        private static ItemData _origBackpackSlot;
        private static List<ItemData> _savedSharedItems;
        private static bool _checkedDistantCombat;
        private static bool _loggedPhase0Tick;
        private static bool _loggedSortStopped;
        private static bool _prevBasePaused;
        private static bool _prevSmartCombat;
        private static bool _prevAutoBio;

        public static bool Tick()
        {
            if (Finished) return true;
            float now = Time.realtimeSinceStartup;

            try
            {
                if (_phase < 0)
                    return SetupAndRunInstantChecks(now);

                if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter) || !Il2CppSafe.IsAlive(_agent.npcData))
                    return End(false, "FAIL [V312Live]: companion became invalid.");

                if (Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter))
                    _agent.mostThreateningCharacter = null;

                if (_phase == 0)
                {
                    if (!_loggedPhase0Tick)
                    {
                        _loggedPhase0Tick = true;
                        LiveSmokePlugin.Trace(
                            $"[V312Live] Phase0 first tick: sorting={_state.IsSortingActive}, carry={_state.CarryBuffer.Count}, sortPhase={_state.SortingPhase}, doctrine={_state.Doctrine}, suppressed={_agent.IsTeammateCombatSuppressed()}, sleeping={_agent.humanCharacter.isSleeping}, downed={_agent.downedStateFlag}, onScreen={_agent.humanCharacter.isOnScreen}, visualOnScreen={_agent.humanCharacter.isVisualOnScreen}, threats={_agent.threatDataDic?.Count ?? 0}");
                    }

                    // Trigger CombatResponse.Refresh with a synthetic >12m hostile while sorting + CarryBuffer > 0
                    if (!_checkedDistantCombat && _state.IsSortingActive && _state.CarryBuffer.Count > 0)
                    {
                        LiveSmokePlugin.Trace($"[V312Live] Phase0 triggering VerifyDistantHostileDoesNotFlushCarryBuffer (carry={_state.CarryBuffer.Count}, sortPhase={_state.SortingPhase})");
                        VerifyDistantHostileDoesNotFlushCarryBuffer();
                        _checkedDistantCombat = true;
                        LiveSmokePlugin.Trace("[V312Live] Phase0 VerifyDistantHostileDoesNotFlushCarryBuffer passed");
                    }

                    int curUnits = CountItemUnits(_destChest.Inventory, _matItemId);
                    if (curUnits >= _destInitialUnits + 2 && !_state.IsSortingActive)
                    {
                        if (!_checkedDistantCombat)
                        {
                            // In case pickup+deposit completed within a single tick interval, still explicitly verify distant threat guard under active sorting state
                            LiveSmokePlugin.Trace($"[V312Live] Phase0 completed in 1 tick (curUnits={curUnits}); invoking VerifyDistantHostileWithSyntheticActiveSort");
                            VerifyDistantHostileWithSyntheticActiveSort();
                            LiveSmokePlugin.Trace("[V312Live] Phase0 VerifyDistantHostileWithSyntheticActiveSort passed");
                        }
                        LiveSmokePlugin.Logger.LogInfo(
                            $"[V312Live] PASS (a) Equipped backpack + sharedInventoryData unloaded (+2 units -> {curUnits}) under >12m hostile without CarryBuffer flush in {now - _phaseStart:F2}s.");
                        return End(true,
                            "PASS v3.1.2 live regressions: (a) backpack+shared unload & >12m CombatResponse guard, (b) shared nested-bag bleeding bandage @90% HP + medkit, (c) passengerSeatLimit==-1 vehicle yield, (d) CampGarrison exit StayPos reset.");
                    }

                    else if (!_state.IsSortingActive && !_loggedSortStopped)
                    {
                        _loggedSortStopped = true;
                        LiveSmokePlugin.Trace($"[V312Live] Phase0 sorting became inactive early: curUnits={curUnits}/{_destInitialUnits + 2}, carry={_state.CarryBuffer.Count}, sortPhase={_state.SortingPhase}, task='{_state.CurrentTaskText}'");
                    }

                    if (now - _phaseStart > 16f)
                    {
                        return End(false,
                            $"FAIL [V312Live Phase 0 timeout]: curUnits={curUnits}/{_destInitialUnits + 2}, sorting={_state.IsSortingActive}, carry={_state.CarryBuffer.Count}, phase={_state.SortingPhase}, task='{_state.CurrentTaskText}'.");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LiveSmokePlugin.Trace("[V312Live] Tick caught exception: " + ex);
                return End(false, "FAIL [V312Live Exception]: " + ex);
            }
        }

        private static bool SetupAndRunInstantChecks(float now)
        {
            var gc = GameController.instance;
            var player = gc?.playerCharacter;
            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            if (!Il2CppSafe.IsAliveUnity(player) || squad.Count == 0)
                return End(false, "BLOCKED [V312Live]: missing player or active teammate.");

            _agent = squad[0];
            if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter) || !Il2CppSafe.IsAlive(_agent.npcData) ||
                !Il2CppSafe.IsAlive(_agent.npcData.characterData?.characterStatusData))
                return End(false, "BLOCKED [V312Live]: invalid teammate character/status data.");

            _state = CompanionConfigStore.GetOrCreateCompanion(_agent.npcData.npcGUID, _agent.npcData.characterData.characterName);
            _origPlayerPos = player.transform.position;
            _origAgentPos = _agent.humanCharacter.transform.position;
            _prevBasePaused = BaseWorkManager.Paused;
            _prevSmartCombat = CompanionConfigStore.EnableSmartCombat;
            _prevAutoBio = CompanionConfigStore.EnableAutoBioAndSleep;

            ArenaActive = true;
            CombatLiveRegression.SuppressAllNearbyHostilesForLogistics(_agent);
            BaseWorkManager.CancelWork(_agent, _state);
            BaseWorkManager.SetPaused(false);
            CompanionConfigStore.EnableSmartCombat = true;
            CompanionConfigStore.EnableAutoBioAndSleep = true;
            _agent.mostThreateningCharacter = null;
            _agent.threatDataDic?.Clear();

            // (b) Live autonomous bandaging & healing from nested bag inside sharedInventoryData
            RunLiveNestedBagAndBandageCheck();

            // (c) Live vehicle boarding yield with passengerSeatLimit == -1
            RunLiveVehicleSeatYieldCheck(player);

            // (d) Live CampGarrison exit resetting stale >120m npcStayPosition + ShouldBlockVanillaReturnToBase
            RunLiveGarrisonExitStayPosCheck();

            // (e) Live automatic NPC equipment & inventory repair (equipped ranged/parts/mag/broken armor + personal/shared/nested inventory + MaterialsOnly vs MaterialsOrField)
            RunLiveAutoGearRepairCheck();

            // (f) Live v3.2.1 High Fever (buff 7) freeze prevention & shared-bag Fever Reducer (234) cure
            RunLiveFeverFreezeAndMedicineCheck();

            // (g) Live v3.2.2 EventLogRecorder in active Gaming world (WORLD/SQUAD/SNAP_NPC/BUG_MARK + privacy)
            RunLiveEventLogRecorderCheck();

            // (a) Prepare equipped backpack + sharedInventoryData material unload check
            return SetupBackpackAndSharedUnloadTest(now);
        }

        private static void RunLiveEventLogRecorderCheck()
        {
            bool orig = CompanionConfigStore.EnableEventLogging;
            try
            {
                EventLogRecorder.ClearLogFile();
                EventLogRecorder.SetEnabled(true, "livesmoke_v322");
                EventLogRecorder.Tick();
                EventLogRecorder.Log("LIVE_TEST", "Live Gaming world event log probe", immediateFlush: true);
                bool marked = EventLogRecorder.MarkBugNow("livesmoke_bug_mark");
                if (!marked || !System.IO.File.Exists(EventLogRecorder.LogFilePath))
                    throw new Exception("EventLogRecorder did not write log file in live Gaming world.");
                string content = System.IO.File.ReadAllText(EventLogRecorder.LogFilePath, System.Text.Encoding.UTF8);
                if (!content.Contains("[WORLD]") || !content.Contains("[SNAP_NPC]") || !content.Contains("[BUG_MARK #"))
                    throw new Exception("EventLogRecorder live log missing expected WORLD/SNAP_NPC/BUG_MARK sections.");
                if (content.IndexOf("C:\\Users\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception("EventLogRecorder leaked OS user path in live Gaming world!");
                LiveSmokePlugin.Logger.LogInfo($"[V312Live] PASS (g) EventLogRecorder live Gaming world: bytes={content.Length}, events={EventLogRecorder.SessionEventCount}, privacy=clean.");
            }
            finally
            {
                EventLogRecorder.SetEnabled(orig, "livesmoke_restore");
                if (!orig) EventLogRecorder.ClearLogFile();
            }
        }

        private static void RunLiveFeverFreezeAndMedicineCheck()
        {
            var hc = _agent.humanCharacter;
            var cd = _agent.npcData.characterData;
            var status = cd.characterStatusData;
            var im = ItemManager.instance;
            var sharedInv = _agent.npcData.GetOrCreateSharedInventory();
            if (!Il2CppSafe.IsAliveUnity(hc) || !Il2CppSafe.IsAlive(cd) || !Il2CppSafe.IsAlive(status) ||
                !Il2CppSafe.IsAlive(im) || !Il2CppSafe.IsAlive(sharedInv))
                throw new Exception("BLOCKED [V312Live(f)]: missing character/inventory/ItemManager.");

            float origTemp = status.bodyTemperature;
            var origOrder = _state.ActiveOrder;
            var origAnchor = _state.OrderAnchorPos;
            var origThreat = _agent.mostThreateningCharacter;
            bool hadHighFever = CompanionSurvivalSystem.HasBuffSafe(cd, CompanionSurvivalSystem.BuffIdHighFever);
            bool hadLowFever = CompanionSurvivalSystem.HasBuffSafe(cd, CompanionSurvivalSystem.BuffIdLowFever);
            ItemData addedFeverReducer = null;
            try
            {
                float worldTime = Il2CppSafe.IsAlive(GameController.instance?.gameData)
                    ? GameController.instance.gameData.inGameTime : 1000f;
                float stdTemp = CharacterStatusData.standardBodyTemperature > 30f
                    ? CharacterStatusData.standardBodyTemperature : 37f;

                // Clear any existing fever suppression key before testing
                string suppressKey = GOAPGoal_ClearDebuff.feverSuppressedUntilKey;
                if (string.IsNullOrEmpty(suppressKey)) suppressKey = "feverSuppressedUntil";
                _agent.agentState.SetState(suppressKey, (Il2CppSystem.Object)null);

                // Inflict High Fever (buffId 7) + elevated body temp
                status.bodyTemperature = stdTemp + 2.5f;
                hc.AddBuff(CompanionSurvivalSystem.BuffIdHighFever, worldTime + 600f);
                if (!CompanionSurvivalSystem.HasBuffSafe(cd, CompanionSurvivalSystem.BuffIdHighFever))
                    throw new Exception("Failed to apply native High Fever (buff 7) to companion.");

                var clearGoal = new GOAPGoal_ClearDebuff();
                // 1) Out of combat, Follow: priority must be clamped <= 45 (not vanilla 150!)
                _state.ActiveOrder = TacticalOrderKind.Follow;
                _agent.mostThreateningCharacter = null;
                float followPrio = clearGoal.GetPriority(_agent.agentState);
                if (followPrio > 45.01f)
                    throw new Exception($"High Fever Follow priority not clamped: got {followPrio:F1} (expected <= 45).");

                // 2) Under active player HoldPosition order: priority must be suppressed (< 0)
                _state.ActiveOrder = TacticalOrderKind.HoldPosition;
                _state.OrderAnchorPos = hc.transform.position;
                float holdPrio = clearGoal.GetPriority(_agent.agentState);
                if (holdPrio > 0f)
                    throw new Exception($"High Fever under HoldPosition must be <= 0, got {holdPrio:F1}.");

                // 3) GOAPAction_ClearDebuff_Fever.Execute under HoldPosition must abort immediately (return false) and set IsSuppressed
                var feverAction = new GOAPAction_ClearDebuff_Fever();
                feverAction.OnActionStarted(_agent);
                bool execHold = feverAction.Execute(_agent);
                if (execHold)
                    throw new Exception("GOAPAction_ClearDebuff_Fever.Execute did not abort under HoldPosition.");
                if (!GOAPGoal_ClearDebuff.IsSuppressed(_agent.agentState, CompanionSurvivalSystem.BuffIdHighFever))
                    throw new Exception("GOAPGoal_ClearDebuff.IsSuppressed(7) was not set after aborting fever action.");

                // 4) Put Fever Reducer (234) into sharedInventoryData and verify autonomous cure removes buff 7 & restores temp
                _state.ActiveOrder = TacticalOrderKind.Follow;
                var frAttr = im.GetItemAttrById(CompanionSurvivalSystem.ItemIdFeverReducer);
                if (!Il2CppSafe.IsAliveUnity(frAttr))
                    throw new Exception("ItemAttr 234 (Fever Reducer) not found in ItemManager.");
                addedFeverReducer = im.GetRandomDataByAttr(frAttr, false, true);
                addedFeverReducer.itemNumberFloat = 1f;
                sharedInv.itemList.Add(addedFeverReducer);

                bool cured = CompanionSurvivalSystem.TryImmediateFeverMedicine(_agent, _state);
                bool stillHasFever = CompanionSurvivalSystem.HasBuffSafe(cd, CompanionSurvivalSystem.BuffIdHighFever);
                if (!cured || stillHasFever || status.bodyTemperature > stdTemp + 0.05f)
                    throw new Exception($"Autonomous Fever Reducer cure failed: cured={cured}, stillHasFever={stillHasFever}, temp={status.bodyTemperature:F2}/{stdTemp:F2}");

                LiveSmokePlugin.Logger.LogInfo(
                    $"[V312Live] PASS (f) Fever/Cold Freeze Fix: followPrio={followPrio:F1} (<=45), holdPrio={holdPrio:F1} (<=0), execAbort=True, IsSuppressed=True, sharedBagFeverReducerCured=True (temp={status.bodyTemperature:F1}).");
            }
            finally
            {
                _state.ActiveOrder = origOrder;
                _state.OrderAnchorPos = origAnchor;
                _agent.mostThreateningCharacter = origThreat;
                status.bodyTemperature = origTemp;
                if (!hadHighFever) try { hc.RemoveBuff(CompanionSurvivalSystem.BuffIdHighFever); } catch { }
                if (!hadLowFever) try { hc.RemoveBuff(CompanionSurvivalSystem.BuffIdLowFever); } catch { }
                if (Il2CppSafe.IsAlive(addedFeverReducer) && sharedInv.itemList.Contains(addedFeverReducer))
                    sharedInv.itemList.Remove(addedFeverReducer);
                string suppressKey = GOAPGoal_ClearDebuff.feverSuppressedUntilKey;
                if (string.IsNullOrEmpty(suppressKey)) suppressKey = "feverSuppressedUntil";
                try { _agent.agentState.SetState(suppressKey, (Il2CppSystem.Object)null); } catch { }
            }
        }

        private static void RunLiveAutoGearRepairCheck()
        {
            var hc = _agent.humanCharacter;
            var cd = _agent.npcData.characterData;
            var eq = cd.characterEquipmentData;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(eq) || !Il2CppSafe.IsAlive(im))
                throw new Exception("BLOCKED [V312Live(e)]: missing equipment or ItemManager.");

            var origMode = CompanionConfigStore.AutoRepairMode;
            var origWeapon0 = eq.GetWeaponData(0);
            var origCurWeapon = eq.currentWeaponItemData;
            var origTorso = eq.torseItemData;
            var origBackpack = eq.backpackItemData;
            var origPersonal = SnapshotAndClearInventory(cd.inventoryData);
            var origShared = SnapshotAndClearInventory(_agent.npcData.GetOrCreateSharedInventory());

            try
            {
                // Find real repairable ranged weapon (with rw.hp > 100 and repairData), repairable clothing, repairable gun part, and repairable magazine
                ItemAttr_RangedWeapon rangedAttr = null;
                var rangedList = im.GetEligibleLootAttrsByType(ItemType.RangedWeapon);
                if (rangedList != null)
                {
                    for (int i = 0; i < rangedList.Count; i++)
                    {
                        var rw = rangedList[i]?.TryCast<ItemAttr_RangedWeapon>();
                        if (Il2CppSafe.IsAliveUnity(rw) && rw.hp > 100f && (rw.Repairable || Il2CppSafe.IsAliveUnity(im.GetItemAttrById(rw.itemId + 1000))))
                        {
                            rangedAttr = rw;
                            if ((int)rw.feedingType == 0) break;
                        }
                    }
                }

                ItemAttr clothingAttr = null;
                var clothList = im.GetEligibleLootAttrsByType(ItemType.Clothing);
                if (clothList != null)
                {
                    for (int i = 0; i < clothList.Count; i++)
                    {
                        var ca = clothList[i];
                        if (Il2CppSafe.IsAliveUnity(ca) && ca.Repairable &&
                            Il2CppSafe.IsAlive(ca.repairData) && ca.repairData.recipeItems != null && ca.repairData.recipeItems.Count > 0)
                        {
                            bool allSolid = true;
                            for (int r = 0; r < ca.repairData.recipeItems.Count; r++)
                            {
                                var rid = ca.repairData.recipeItems[r];
                                if (!Il2CppSafe.IsAlive(rid) || RepairLiquidMaterial.IsLiquidMaterial(rid.itemId) || !Il2CppSafe.IsAliveUnity(im.GetItemAttrById(rid.itemId)))
                                {
                                    allSolid = false;
                                    break;
                                }
                            }
                            if (allSolid) { clothingAttr = ca; break; }
                        }
                    }
                }

                ItemAttr_GunPart gunPartAttr = null;
                var gpList = im.GetEligibleLootAttrsByType(ItemType.GunPart);
                if (gpList != null)
                {
                    for (int i = 0; i < gpList.Count; i++)
                    {
                        var gp = gpList[i]?.TryCast<ItemAttr_GunPart>();
                        if (Il2CppSafe.IsAliveUnity(gp) && gp.Repairable && gp.firingDurability > 10f)
                        {
                            gunPartAttr = gp;
                            break;
                        }
                    }
                }

                if (!Il2CppSafe.IsAliveUnity(rangedAttr) || !Il2CppSafe.IsAliveUnity(clothingAttr))
                    throw new Exception("BLOCKED [V312Live(e)]: could not resolve real rangedWeapon or repairable clothing ItemAttr.");

                // 1. Equipped damaged & jammed ranged weapon (properties[0] at 30% of rw.hp + attached damaged gun part if found)
                var eqRifle = im.GetRandomDataByAttr(rangedAttr, false, true);
                if (eqRifle.properties == null || eqRifle.properties.Length < 14)
                    eqRifle.properties = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(14);
                float rifleStartHp = rangedAttr.hp * 0.30f;
                eqRifle.properties[0] = rifleStartHp;
                eqRifle.SetProperty("MalfunctionFlag", "true");
                float attachedPartStart = -1f;
                if (Il2CppSafe.IsAliveUnity(gunPartAttr))
                {
                    eqRifle.properties[8] = gunPartAttr.itemId;
                    attachedPartStart = gunPartAttr.firingDurability * 0.25f;
                    eqRifle.properties[9] = attachedPartStart;
                }
                eq.SetWeaponData(eqRifle, 0);
                eq.currentWeaponItemData = eqRifle;

                // 2. Equipped completely broken armor (durability == 0f!)
                var eqArmor = im.GetRandomDataByAttr(clothingAttr, false, true);
                eqArmor.durability = 0f;
                eq.torseItemData = eqArmor;

                // 3. Inventory damaged gear: personal inv + shared inv + nested pouch inside equipped backpack + non-repairable food
                var invArmorPersonal = im.GetRandomDataByAttr(clothingAttr, false, true);
                invArmorPersonal.durability = 25f;
                cd.inventoryData.itemList.Add(invArmorPersonal);

                var invRifleShared = im.GetRandomDataByAttr(rangedAttr, false, true);
                if (invRifleShared.properties == null || invRifleShared.properties.Length == 0)
                    invRifleShared.properties = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(14);
                float sharedRifleStart = rangedAttr.hp * 0.40f;
                invRifleShared.properties[0] = sharedRifleStart;
                var sharedInv = _agent.npcData.GetOrCreateSharedInventory();
                sharedInv.itemList.Add(invRifleShared);

                var nonRepairableFood = im.GetRandomDataByAttr(im.GetItemAttrById(232), false, true);
                nonRepairableFood.durability = 35f;
                sharedInv.itemList.Add(nonRepairableFood);

                ItemData nestedGunPart = null;
                float nestedGpStart = -1f;
                var equippedPack = CreateEmptyBackpackItem(8, 8, clothingAttr);
                var nestedPouch = CreateEmptyBackpackItem(6, 6, clothingAttr);
                var invArmorNested = im.GetRandomDataByAttr(clothingAttr, false, true);
                invArmorNested.durability = 20f;
                nestedPouch.inventoryData.itemList.Add(invArmorNested);
                if (Il2CppSafe.IsAliveUnity(gunPartAttr))
                {
                    nestedGunPart = im.GetRandomDataByAttr(gunPartAttr, false, true);
                    if (nestedGunPart.properties == null || nestedGunPart.properties.Length == 0)
                        nestedGunPart.properties = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(4);
                    nestedGpStart = gunPartAttr.firingDurability * 0.20f;
                    nestedGunPart.properties[0] = nestedGpStart;
                    nestedPouch.inventoryData.itemList.Add(nestedGunPart);
                }
                equippedPack.inventoryData.itemList.Add(nestedPouch);
                eq.backpackItemData = equippedPack;

                // Phase e1: MaterialsOnly WITHOUT materials -> MUST repair 0 items
                // Isolate from any nearby player-marked base chests containing real materials in the live save
                var savedCachedContainers = new List<DiscoveredContainer>(BaseSortingSystem.CachedContainers);
                BaseSortingSystem.CachedContainers.Clear();
                try
                {
                    CompanionConfigStore.AutoRepairMode = GearAutoRepairMode.MaterialsOnly;
                    var noMatRes = CompanionGearRepairSystem.TryRunGearRepairCycleForAgent(_agent, _state, Time.unscaledTime + 100f, ignoreCooldown: true, allowSpeak: false);
                    if (noMatRes.TotalRepaired != 0 || eqArmor.durability > 0.01f)
                        throw new Exception($"MaterialsOnly repaired without materials: count={noMatRes.TotalRepaired}, armor={eqArmor.durability:F1}");
                }
                finally
                {
                    BaseSortingSystem.CachedContainers.Clear();
                    BaseSortingSystem.CachedContainers.AddRange(savedCachedContainers);
                }

                // Phase e2: Add exact real repairData materials for clothingAttr into sharedInv -> MaterialsOnly MUST repair eqArmor (0 -> 100) and consume exact materials!
                int firstMatId = clothingAttr.repairData.recipeItems[0].itemId;
                int matAddedTotal = 0;
                for (int r = 0; r < clothingAttr.repairData.recipeItems.Count; r++)
                {
                    var rid = clothingAttr.repairData.recipeItems[r];
                    var matAttr = im.GetItemAttrById(rid.itemId);
                    var matItem = im.GetRandomDataByAttr(matAttr, false, true);
                    int qty = Mathf.CeilToInt(rid.itemNumber);
                    matItem.itemNumberFloat = qty;
                    sharedInv.itemList.Add(matItem);
                    if (rid.itemId == firstMatId) matAddedTotal += qty;
                }
                var matRes = CompanionGearRepairSystem.TryRunGearRepairCycleForAgent(_agent, _state, Time.unscaledTime + 200f, ignoreCooldown: true, allowSpeak: false);
                int firstMatLeft = CountItemUnits(sharedInv, firstMatId);
                if (matRes.MaterialsUsed < 1 || matRes.EquippedRepaired < 1 || eqArmor.durability < 99.5f || firstMatLeft >= matAddedTotal)
                    throw new Exception($"MaterialsOnly with real recipe failed: matUsed={matRes.MaterialsUsed}, eqRep={matRes.EquippedRepaired}, armor={eqArmor.durability:F1}, matLeft={firstMatLeft}/{matAddedTotal}");

                // Phase e3: MaterialsOrField -> run cycles until equipped rifle (properties[0] + MalfunctionFlag), attached part, personal armor, shared rifle, nested armor & nested gun part are ALL repaired while food(232) stays at 35f!
                CompanionConfigStore.AutoRepairMode = GearAutoRepairMode.MaterialsOrField;
                int totalCyclesRepaired = 0;
                for (int c = 0; c < 6; c++)
                {
                    var r = CompanionGearRepairSystem.TryRunGearRepairCycleForAgent(_agent, _state, Time.unscaledTime + 300f + c * 30f, ignoreCooldown: true, allowSpeak: false);
                    totalCyclesRepaired += r.TotalRepaired;
                }

                bool rifleRepaired = eqRifle.properties[0] > rifleStartHp + 10f &&
                                     !string.Equals(eqRifle.GetProperty("MalfunctionFlag"), "true", StringComparison.OrdinalIgnoreCase);
                bool attachedOk = !Il2CppSafe.IsAliveUnity(gunPartAttr) || eqRifle.properties[9] > attachedPartStart + 1f;
                bool personalOk = invArmorPersonal.durability > 30f;
                bool sharedRifleOk = invRifleShared.properties[0] > sharedRifleStart + 10f;
                bool nestedArmorOk = invArmorNested.durability > 25f;
                bool nestedGpOk = !Il2CppSafe.IsAlive(nestedGunPart) || nestedGunPart.properties[0] > nestedGpStart + 1f;
                bool foodUntouched = Mathf.Abs(nonRepairableFood.durability - 35f) < 0.01f;

                if (!rifleRepaired || !attachedOk || !personalOk || !sharedRifleOk || !nestedArmorOk || !nestedGpOk || !foodUntouched)
                    throw new Exception(
                        $"MaterialsOrField repair incomplete: cycles={totalCyclesRepaired}, rifle={eqRifle.properties[0]:F1}/{rangedAttr.hp:F1} jam={eqRifle.GetProperty("MalfunctionFlag")}, attached={attachedOk}, personal={invArmorPersonal.durability:F1}, sharedRifle={invRifleShared.properties[0]:F1}, nestedArmor={invArmorNested.durability:F1}, nestedGp={nestedGpOk}, food={nonRepairableFood.durability:F1}");

                LiveSmokePlugin.Logger.LogInfo(
                    $"[V312Live] PASS (e) Auto Gear Repair: MaterialsOnly armor 0->{eqArmor.durability:F0} (mat {matAddedTotal}->{firstMatLeft}), MaterialsOrField eqRifle {rifleStartHp:F0}->{eqRifle.properties[0]:F0}/{rangedAttr.hp:F0} (unjammed), attachedPart={attachedOk}, personalArmor 25->{invArmorPersonal.durability:F0}, sharedRifle {sharedRifleStart:F0}->{invRifleShared.properties[0]:F0}, nestedArmor 20->{invArmorNested.durability:F0}, nestedGunPart={nestedGpOk}, foodUntouched={foodUntouched}.");
            }
            finally
            {
                CompanionConfigStore.AutoRepairMode = origMode;
                eq.SetWeaponData(origWeapon0, 0);
                eq.currentWeaponItemData = origCurWeapon;
                eq.torseItemData = origTorso;
                eq.backpackItemData = origBackpack;
                RestoreInventory(cd.inventoryData, origPersonal);
                RestoreInventory(_agent.npcData.GetOrCreateSharedInventory(), origShared);
                try { hc.RefreshEquipmentGains(); } catch { }
            }
        }

        private static void RunLiveNestedBagAndBandageCheck()
        {
            var hc = _agent.humanCharacter;
            var cd = _agent.npcData.characterData;
            var status = cd.characterStatusData;
            var eq = cd.characterEquipmentData;
            string guid = _agent.npcData.npcGUID ?? _state.NpcGuid ?? "";

            float origHp = status.hp;
            float origBlood = status.bloodVolume;
            float maxHp = status.maxhp > 1f ? status.maxhp : 100f;
            float origHunger = status.hunger;
            float origThirst = status.thirst;

            cd.bodypartsData = CharacterBodyPartsData.EnsureHumanBodyParts(cd.bodypartsData);
            var part0 = cd.bodypartsData.bodypartDataAry[0];
            float origPartHp = part0.hp;
            float origBleedStop = part0.bleedingStopTime;
            var origBandage = part0.bandageData;

            var shared = _agent.npcData.GetOrCreateSharedInventory();
            _savedSharedItems = SnapshotAndClearInventory(shared);
            var personalInv = cd.inventoryData;
            List<ItemData> savedPersonal = SnapshotAndClearInventory(personalInv);
            var savedEqPack = eq?.backpackItemData;
            if (eq != null) eq.backpackItemData = null;

            try
            {
                var nextTickDict = (Dictionary<string, float>)typeof(CompanionSurvivalSystem).GetField("NextTickByGuid", StaticAny)?.GetValue(null);
                var lastSelfDict = (Dictionary<string, float>)typeof(CompanionSurvivalSystem).GetField("LastSelfCareAtByGuid", StaticAny)?.GetValue(null);

                int bandageId = FindPureBandageItemId();
                var bandageAttr = ItemManager.instance.GetItemAttrById(bandageId);
                if (!Il2CppSafe.IsAliveUnity(bandageAttr))
                    throw new Exception($"Bandage ItemAttr {bandageId} unavailable");

                var bagHolder = CreateEmptyBackpackItem(4, 4);
                var bagInv = bagHolder.inventoryData;
                var bandageItem = ItemManager.instance.GetRandomDataByAttr(bandageAttr, false, true);
                bandageItem.itemNumberFloat = 1f;
                bagInv.itemList.Add(bandageItem);
                shared.itemList.Add(bagHolder);

                // Keep hunger/thirst topped up so only bandaging triggers
                status.hunger = status.maxHunger > 1f ? status.maxHunger : 100f;
                status.thirst = status.maxThirst > 1f ? status.maxThirst : 100f;

                // Case b1: Active bleeding at >= 80% HP (90% HP) with pure bandage inside nested bag in sharedInventoryData
                status.hp = maxHp * 0.90f;
                hc.hp = status.hp;
                _agent.npcData.health = status.hp;
                part0.hp = Mathf.Max(1f, part0.maxHP * 0.50f);
                float worldTime = Mathf.Max(Time.time, Il2CppSafe.IsAlive(GameController.instance?.gameData) ? GameController.instance.gameData.inGameTime : 0f);
                part0.bleedingStopTime = worldTime + 3600f;
                part0.bandageData = null;
                if (!part0.isBleeding)
                    throw new Exception("Live fixture bodypart0.isBleeding did not become true");

                nextTickDict?.Remove(guid);
                lastSelfDict?.Remove(guid);
                CompanionSurvivalSystem.TickCompanionSurvival(_agent, _state, Time.unscaledTime + 10f);

                if (CountItemUnits(bagInv, bandageId) != 0 || part0.isBleeding)
                {
                    throw new Exception(
                        $"Pure bandage @90% HP failed (remaining={CountItemUnits(bagInv, bandageId)}, isBleeding={part0.isBleeding})");
                }

                // Case b2: Healing med inside nested bag in sharedInventoryData at 52% HP
                int medId = FindHealingMedItemId();
                var medAttr = ItemManager.instance.GetItemAttrById(medId);
                var medItem = ItemManager.instance.GetRandomDataByAttr(medAttr, false, true);
                medItem.itemNumberFloat = 1f;
                bagInv.itemList.Add(medItem);

                float damagedHp = maxHp * 0.52f;
                status.hp = damagedHp;
                hc.hp = damagedHp;
                _agent.npcData.health = damagedHp;

                nextTickDict?.Remove(guid);
                lastSelfDict?.Remove(guid);
                CompanionSurvivalSystem.TickCompanionSurvival(_agent, _state, Time.unscaledTime + 25f);

                if (CountItemUnits(bagInv, medId) != 0 || status.hp <= damagedHp + 0.5f)
                {
                    throw new Exception(
                        $"Nested-bag med healing failed (bagId={bagHolder.itemId}, medId={medId}, remaining={CountItemUnits(bagInv, medId)}, hp={damagedHp:F1}->{status.hp:F1})");
                }

                nextTickDict?.Remove(guid);
                lastSelfDict?.Remove(guid);
                LiveSmokePlugin.Logger.LogInfo(
                    $"[V312Live] PASS (b) Nested bag in sharedInventoryData: pure bandage stopped bleeding @90% HP (id={bandageId}) & medkit healed {damagedHp:F1}->{status.hp:F1} (id={medId}).");
            }
            finally
            {
                part0.hp = origPartHp;
                part0.bleedingStopTime = origBleedStop;
                part0.bandageData = origBandage;
                status.hp = origHp;
                hc.hp = origHp;
                _agent.npcData.health = origHp;
                status.bloodVolume = origBlood;
                status.hunger = origHunger;
                status.thirst = origThirst;
                RestoreInventory(shared, _savedSharedItems);
                _savedSharedItems = null;
                RestoreInventory(personalInv, savedPersonal);
                if (eq != null) eq.backpackItemData = savedEqPack;
            }
        }

        private static void RunLiveVehicleSeatYieldCheck(HumanCharacterController player)
        {
            BasicVehicle existingVeh = null;
            if (BasicVehicle.s_liveVehicles != null && BasicVehicle.s_liveVehicles.Count > 0)
            {
                for (int i = 0; i < BasicVehicle.s_liveVehicles.Count; i++)
                {
                    if (Il2CppSafe.IsAliveUnity(BasicVehicle.s_liveVehicles[i]))
                    {
                        existingVeh = BasicVehicle.s_liveVehicles[i];
                        break;
                    }
                }
            }

            GameObject tempVehGo = null;
            BasicVehicle veh = existingVeh;
            int origLimit = 0;
            var origDriven = player.drivingVehicle;
            bool origOverflow = _state.IsTransientVehicleOverflow;
            var origOrder = _state.ActiveOrder;
            var origRoster = _state.RosterMode;

            try
            {
                if (!Il2CppSafe.IsAliveUnity(veh))
                {
                    tempVehGo = new GameObject("V312LiveTempVehicle");
                    veh = tempVehGo.AddComponent<BasicVehicle>();
                }
                origLimit = veh.passengerSeatLimit;
                veh.passengerSeatLimit = -1;

                _state.RosterMode = CompanionRosterMode.ActiveSquad;
                _state.ActiveOrder = TacticalOrderKind.Follow;
                _state.IsTransientVehicleOverflow = false;
                player.drivingVehicle = veh;

                ExpeditionSystems.TickVehicleExpedition(Time.unscaledTime + 30f);

                if (_state.IsTransientVehicleOverflow)
                    throw new Exception("IsTransientVehicleOverflow became true when passengerSeatLimit == -1");
                if (!ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(_agent, _state))
                    throw new Exception("ShouldYieldToVanillaVehicleBoarding returned false when player driving vehicle");

                LiveSmokePlugin.Logger.LogInfo(
                    $"[V312Live] PASS (c) Vehicle boarding yield verified with passengerSeatLimit == -1 (overflow=false, yield=true).");
            }
            finally
            {
                player.drivingVehicle = origDriven;
                if (Il2CppSafe.IsAliveUnity(veh))
                    veh.passengerSeatLimit = origLimit;
                _state.IsTransientVehicleOverflow = origOverflow;
                _state.ActiveOrder = origOrder;
                _state.RosterMode = origRoster;
                if (Il2CppSafe.IsAliveUnity(tempVehGo))
                    UnityEngine.Object.DestroyImmediate(tempVehGo);
                ExpeditionSystems.ClearAllTransientVehicleOverflow();
            }
        }

        private static void RunLiveGarrisonExitStayPosCheck()
        {
            var origRoster = _state.RosterMode;
            var origStay = _agent.npcData.npcStayPosition;
            var origCmd = _agent.npcData.basicCommand;

            try
            {
                _state.RosterMode = CompanionRosterMode.ActiveSquad;
                CampGarrisonSystem.ToggleCampGarrison(_agent, _state, quiet: true);
                if (_state.RosterMode != CompanionRosterMode.CampGarrison)
                    throw new Exception("ToggleCampGarrison failed to enter CampGarrison");

                Vector2 curPos = _agent.humanCharacter.transform.position;
                _agent.npcData.npcStayPosition = new Vector3(curPos.x + 250f, curPos.y + 250f, _agent.npcData.zLevel);
                _agent.npcData.basicCommand = "Basic.StayAtPosition";

                if (!CampGarrisonSystem.ShouldBlockVanillaReturnToBase(_agent, _state))
                    throw new Exception("ShouldBlockVanillaReturnToBase failed to block distant stale camp while in CampGarrison");

                // Exit CampGarrison -> SyncLiveStayPositionOnExit resets npcStayPosition to curPos
                CampGarrisonSystem.ToggleCampGarrison(_agent, _state, quiet: true);
                if (_state.RosterMode != CompanionRosterMode.ActiveSquad)
                    throw new Exception("ToggleCampGarrison failed to exit CampGarrison");

                float distAfter = Vector2.Distance(
                    new Vector2(_agent.npcData.npcStayPosition.x, _agent.npcData.npcStayPosition.y),
                    (Vector2)_agent.humanCharacter.transform.position);
                if (distAfter > 2.5f)
                    throw new Exception($"CampGarrison exit left stale npcStayPosition (dist={distAfter:F2}m)");

                // Verify stale >120m stays blocked even after switching to Follow/StayAtPosition checks
                _agent.npcData.npcStayPosition = new Vector3(curPos.x + 180f, curPos.y, _agent.npcData.zLevel);
                if (!CampGarrisonSystem.ShouldBlockVanillaReturnToBase(_agent, _state))
                    throw new Exception("ShouldBlockVanillaReturnToBase failed to block >120m stay point");

                _agent.npcData.npcStayPosition = new Vector3(curPos.x, curPos.y, _agent.npcData.zLevel);
                if (CampGarrisonSystem.ShouldBlockVanillaReturnToBase(_agent, _state))
                    throw new Exception("ShouldBlockVanillaReturnToBase blocked valid live local StayAtPosition");

                LiveSmokePlugin.Logger.LogInfo(
                    $"[V312Live] PASS (d) CampGarrison exit reset npcStayPosition within {distAfter:F2}m & ShouldBlockVanillaReturnToBase verified.");
            }
            finally
            {
                _state.RosterMode = origRoster;
                _agent.npcData.npcStayPosition = origStay;
                _agent.npcData.basicCommand = origCmd;
            }
        }

        private static bool SetupBackpackAndSharedUnloadTest(float now)
        {
            var containers = BaseSortingSystem.ScanNearbyContainers(force: true);
            DiscoveredContainer bestChest = null;
            for (int pass = 0; pass < 2 && bestChest == null; pass++)
            {
                for (int i = 0; i < containers.Count; i++)
                {
                    var c = containers[i];
                    if (c?.Inventory == null || !Il2CppSafe.IsAliveUnity(c.TerrainObj)) continue;
                    if (c.Inventory.inventorySizeX < 3 || c.Inventory.inventorySizeY < 3) continue;
                    if (c.DistanceToPlayer < 2.2f || c.DistanceToPlayer > 11f) continue;
                    Vector2 stand = Vector2.MoveTowards(c.WorldPos, _origPlayerPos, 1.1f);
                    if (pass == 0 && !_agent.IsPathClear(_origPlayerPos, stand, 0.25f)) continue;
                    bestChest = c;
                    break;
                }
            }
            if (bestChest == null)
                return End(false, "BLOCKED [V312Live]: could not find reachable base chest for backpack/shared unload test.");

            _destChest = bestChest;
            foreach (var c in containers)
            {
                if (c?.Rule == null) continue;
                c.Rule.IsMarkedByPlayer = false;
                c.Rule.IsDropoffSource = false;
                c.Rule.Categories = StorageCategory.None;
                MultiplayerSyncSystem.WriteRuleToTerrainObject(c.TerrainObj, c.Rule);
            }

            _matItemId = FindSimpleMaterialItemId();
            var matAttr = ItemManager.instance.GetItemAttrById(_matItemId);
            var sampleItem = ItemManager.instance.GetRandomDataByAttr(matAttr, false, true);
            sampleItem.itemNumberFloat = 1f;
            StorageCategory cat = ItemCategorizer.ClassifyItem(sampleItem);
            if (cat == StorageCategory.None) cat = StorageCategory.Materials;

            while (_destChest.Inventory.itemList != null && _destChest.Inventory.itemList.Count > 0)
                _destChest.Inventory.RemoveItem(_destChest.Inventory.itemList[_destChest.Inventory.itemList.Count - 1], false);

            _destChest.Rule.IsMarkedByPlayer = true;
            _destChest.Rule.IsDropoffSource = false;
            _destChest.Rule.Categories = cat;
            _destChest.Rule.Priority = 10;
            _destInitialUnits = CountItemUnits(_destChest.Inventory, _matItemId);

            // Seed 1 material in sharedInventoryData AND 1 material inside equipped backpackItemData.inventoryData
            var shared = _agent.npcData.GetOrCreateSharedInventory();
            _savedSharedItems = SnapshotAndClearInventory(shared);
            var sharedItem = ItemManager.instance.GetRandomDataByAttr(matAttr, false, true);
            sharedItem.itemNumberFloat = 1f;
            shared.itemList.Add(sharedItem);

            var eq = _agent.npcData.characterData.characterEquipmentData;
            _origBackpackSlot = eq.backpackItemData;
            var packItem = CreateEquippedBackpackWithItem(matAttr);
            eq.backpackItemData = packItem;

            _agent.humanCharacter.TeleportTo(_origPlayerPos + new Vector2(1.0f, 0f), false);
            GameController.instance.playerCharacter.TeleportTo(_origPlayerPos, false);
            Physics2D.SyncTransforms();
            CombatLiveRegression.SuppressAllNearbyHostilesForLogistics(_agent);
            _agent.mostThreateningCharacter = null;
            _agent.threatDataDic?.Clear();

            _checkedDistantCombat = false;
            BaseSortingSystem.CommandSortBase(_agent, null, instant: false);
            if (!_state.IsSortingActive)
                return End(false, "FAIL [V312Live Phase 0]: CommandSortBase failed to start backpack+shared unload job.");

            _phase = 0;
            _phaseStart = now;
            _loggedPhase0Tick = false;
            _loggedSortStopped = false;
            string startMsg = $"[V312Live] Started Phase 0: unloading backpack + sharedInventoryData (itemId={_matItemId}, cat={cat}) -> chest '{_destChest.DisplayName}'.";
            LiveSmokePlugin.Logger.LogInfo(startMsg);
            LiveSmokePlugin.Trace($"{startMsg} (sorting={_state.IsSortingActive}, sortPhase={_state.SortingPhase}, pack={packItem?.itemId})");
            return false;
        }

        private static void VerifyDistantHostileDoesNotFlushCarryBuffer()
        {
            LiveSmokePlugin.Trace("[V312Live] VerifyDistantHostile: locating existing native ZombieController");
            CombatLiveRegression.SuppressAllNearbyHostilesForLogistics(_agent);

            ZombieController borrowed = null;
            sbyte agentZ = _agent.npcData.zLevel;
            var gc = GameController.instance;
            if (Il2CppSafe.IsAliveUnity(gc) && gc.characterList != null)
            {
                for (int i = 0; i < gc.characterList.Count; i++)
                {
                    var candidate = gc.characterList[i]?.TryCast<ZombieController>();
                    if (!Il2CppSafe.IsAliveUnity(candidate) || candidate.deadFlag || !Il2CppSafe.IsAlive(candidate.characterData))
                        continue;
                    if (candidate.name.Contains("Blast", StringComparison.OrdinalIgnoreCase))
                        continue;
                    borrowed = candidate;
                    if (candidate.characterData.zLevel == agentZ)
                        break;
                }
            }
            if (!Il2CppSafe.IsAliveUnity(borrowed))
                throw new Exception("BLOCKED: no initialized ZombieController found in characterList for VerifyDistantHostileDoesNotFlushCarryBuffer.");

            bool origActive = borrowed.gameObject.activeSelf;
            bool origEnabled = borrowed.enabled;
            bool origFreeze = borrowed.isScriptedMoveFreezeActive;
            Vector2 origPos = borrowed.transform.position;
            Vector2 origDataPos = borrowed.characterData.characterPosition;
            sbyte origZ = borrowed.characterData.zLevel;

            try
            {
                Vector2 selfPos = _agent.humanCharacter.transform.position;
                Vector2 farPos = selfPos + new Vector2(18f, 0f);
                borrowed.transform.position = new Vector3(farPos.x, farPos.y, 0f);
                borrowed.characterData.characterPosition = farPos;
                borrowed.characterData.zLevel = agentZ;
                borrowed.enabled = true;
                borrowed.gameObject.SetActive(true);
                if (!origFreeze) borrowed.BeginScriptedMoveFreeze();
                Physics2D.SyncTransforms();

                LiveSmokePlugin.Trace($"[V312Live] VerifyDistantHostile: zombie ready (name='{borrowed.name}', alive={Il2CppSafe.IsAliveUnity(borrowed)}, dead={borrowed.deadFlag}, enabled={borrowed.enabled}, active={borrowed.gameObject.activeInHierarchy}, dist={Vector2.Distance(selfPos, borrowed.transform.position):F2}m)");

                _agent.threatDataDic ??= new Il2CppSystem.Collections.Generic.Dictionary<BasicCharacterController, GOAPAgent.ThreatData>();
                _agent.threatDataDic.Clear();
                _agent.threatDataDic[borrowed] = new GOAPAgent.ThreatData
                {
                    character = borrowed,
                    zLevel = agentZ,
                    position = farPos,
                    memoryExpirationTimeFlag = Time.time + 30f,
                    threatLevel = 100f,
                    threatAffectRange = 40f
                };
                _agent.mostThreateningCharacter = borrowed;

                int carryBefore = _state.CarryBuffer.Count;
                LiveSmokePlugin.Trace($"[V312Live] VerifyDistantHostile: calling CombatResponse.Refresh (carryBefore={carryBefore}, sorting={_state.IsSortingActive}, doctrine={_state.Doctrine}, suppressed={_agent.IsTeammateCombatSuppressed()})");
                CombatResponse.Refresh(_agent, _state, Time.unscaledTime + 0.5f);
                LiveSmokePlugin.Trace($"[V312Live] VerifyDistantHostile: Refresh returned (sorting={_state.IsSortingActive}, carryAfter={_state.CarryBuffer.Count}, mostThreatAlive={Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter)})");

                if (!_state.IsSortingActive || _state.CarryBuffer.Count != carryBefore)
                    throw new Exception($"Distant >12m hostile flushed/aborted sorting (sorting={_state.IsSortingActive}, carry={_state.CarryBuffer.Count}/{carryBefore})");
                var remThreat = _agent.mostThreateningCharacter;
                if (Il2CppSafe.IsAliveUnity(remThreat))
                {
                    float remDist = Vector2.Distance(_agent.humanCharacter.transform.position, remThreat.transform.position);
                    throw new Exception(
                        $"CombatResponse.Refresh did not clear distant >12m threat during active sorting (rem='{remThreat.name}', same={remThreat.Pointer == borrowed.Pointer}, dist={remDist:F2}m, suppressed={_agent.IsTeammateCombatSuppressed()}, onScreen={_agent.humanCharacter.isOnScreen}, visualOnScreen={_agent.humanCharacter.isVisualOnScreen}, sorting={_state.IsSortingActive}, threats={_agent.threatDataDic?.Count ?? 0})");
                }
            }
            finally
            {
                LiveSmokePlugin.Trace("[V312Live] VerifyDistantHostile: entering finally cleanup");
                try
                {
                    if (Il2CppSafe.IsAliveUnity(_agent))
                    {
                        _agent.threatDataDic?.Clear();
                        _agent.mostThreateningCharacter = null;
                    }
                    if (Il2CppSafe.IsAliveUnity(borrowed))
                    {
                        if (!origFreeze) borrowed.EndScriptedMoveFreeze();
                        borrowed.gameObject.SetActive(origActive);
                        borrowed.transform.position = new Vector3(origPos.x, origPos.y, 0f);
                        if (Il2CppSafe.IsAlive(borrowed.characterData))
                        {
                            borrowed.characterData.characterPosition = origDataPos;
                            borrowed.characterData.zLevel = origZ;
                        }
                        borrowed.enabled = origEnabled;
                        Physics2D.SyncTransforms();
                    }
                }
                catch { }
                LiveSmokePlugin.Trace("[V312Live] VerifyDistantHostile: cleanup complete");
            }
        }

        private static void VerifyDistantHostileWithSyntheticActiveSort()
        {
            bool prevSort = _state.IsSortingActive;
            _state.IsSortingActive = true;
            _state.CarryBuffer.Add(new CarryItemEntry { TargetContainerId = "v312_synth" });
            try
            {
                VerifyDistantHostileDoesNotFlushCarryBuffer();
            }
            finally
            {
                _state.CarryBuffer.RemoveAll(e => e != null && e.TargetContainerId == "v312_synth");
                _state.IsSortingActive = prevSort;
            }
        }

        private static int FindPureBandageItemId()
        {
            int candidate = AidTreatUtil.itemIdBandage;
            var attr = ItemManager.instance?.GetItemAttrById(candidate)?.TryCast<ItemAttr_Food>();
            if (Il2CppSafe.IsAliveUnity(attr) && attr.hp <= 0f)
                return candidate;

            var list = ItemManager.instance?.GetEligibleLootAttrsByType(ItemType.Food);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i]?.TryCast<ItemAttr_Food>();
                    if (Il2CppSafe.IsAliveUnity(a) && AidTreatUtil.IsBandageItem(a.itemId) && a.hp <= 0f)
                        return a.itemId;
                }
            }
            return candidate > 0 ? candidate : 220;
        }

        private static int FindHealingMedItemId()
        {
            int kitId = AidTreatUtil.itemIdFirstAidKit;
            var kitAttr = ItemManager.instance?.GetItemAttrById(kitId)?.TryCast<ItemAttr_Food>();
            if (Il2CppSafe.IsAliveUnity(kitAttr) && kitAttr.hp > 0f && AidTreatUtil.IsMedicalConsumable(kitId))
                return kitId;

            var list = ItemManager.instance?.GetEligibleLootAttrsByType(ItemType.Food);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i]?.TryCast<ItemAttr_Food>();
                    if (Il2CppSafe.IsAliveUnity(a) && a.hp > 0f && AidTreatUtil.IsMedicalConsumable(a.itemId))
                        return a.itemId;
                }
            }
            return kitId > 0 ? kitId : 221;
        }

        private static int FindSimpleMaterialItemId()
        {
            var list = ItemManager.instance?.GetEligibleLootAttrsByType(ItemType.Material);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    if (Il2CppSafe.IsAliveUnity(a) && a.itemSize.x * a.itemSize.y <= 2)
                        return a.itemId;
                }
            }
            return 232;
        }

        private static ItemData CreateEmptyBackpackItem(int width, int height, ItemAttr fallbackAttr = null)
        {
            var packs = ItemManager.instance?.GetEligibleLootAttrsByType(ItemType.Backpack);
            ItemAttr packAttr = (packs != null && packs.Count > 0) ? packs[0] : (ItemManager.instance?.GetItemAttrById(500) ?? fallbackAttr);
            ItemData packItem = Il2CppSafe.IsAliveUnity(packAttr)
                ? ItemManager.instance.GetRandomDataByAttr(packAttr, false, true)
                : new ItemData { itemId = 500 };
            packItem.itemNumberFloat = 1f;
            packItem.inventoryData = MakeInventory(width, height);
            return packItem;
        }

        private static ItemData CreateEquippedBackpackWithItem(ItemAttr contentAttr)
        {
            var packItem = CreateEmptyBackpackItem(6, 6, contentAttr);
            var itemInside = ItemManager.instance.GetRandomDataByAttr(contentAttr, false, true);
            itemInside.itemNumberFloat = 1f;
            packItem.inventoryData.itemList.Add(itemInside);
            return packItem;
        }

        private static InventoryData MakeInventory(int width, int height)
        {
            var inv = new InventoryData
            {
                inventorySizeX = width,
                inventorySizeY = height,
                itemList = new Il2CppSystem.Collections.Generic.List<ItemData>()
            };
            inv.SetOccupyDataDirty();
            return inv;
        }

        private static List<ItemData> SnapshotAndClearInventory(InventoryData inv)
        {
            var list = new List<ItemData>();
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return list;
            for (int i = 0; i < inv.itemList.Count; i++)
                if (Il2CppSafe.IsAlive(inv.itemList[i])) list.Add(inv.itemList[i]);
            while (inv.itemList.Count > 0)
                inv.RemoveItem(inv.itemList[inv.itemList.Count - 1], false);
            return list;
        }

        private static void RestoreInventory(InventoryData inv, List<ItemData> items)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return;
            while (inv.itemList.Count > 0)
                inv.RemoveItem(inv.itemList[inv.itemList.Count - 1], false);
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
                if (Il2CppSafe.IsAlive(items[i])) inv.TryAddItem(items[i], out _, false, false);
        }

        private static int CountItemUnits(InventoryData inv, int itemId)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return 0;
            int total = 0;
            for (int i = 0; i < inv.itemList.Count; i++)
                if (Il2CppSafe.IsAlive(inv.itemList[i]) && inv.itemList[i].itemId == itemId)
                    total += Mathf.Max(1, Mathf.RoundToInt(inv.itemList[i].itemNumberFloat));
            return total;
        }

        private static bool End(bool ok, string msg)
        {
            ArenaActive = false;
            Succeeded = ok;
            Finished = true;
            LiveSmokePlugin.Trace($"[V312Live] End(ok={ok}): {msg}");
            if (ok) LiveSmokePlugin.Logger.LogInfo("[V312Live] " + msg);
            else LiveSmokePlugin.Logger.LogError("[V312Live] " + msg);
            LiveSmokePlugin.FlushLogs();
            try
            {
                if (_agent != null && _state != null)
                    BaseWorkManager.CancelWork(_agent, _state);
                BaseWorkManager.SetPaused(_prevBasePaused);
                CompanionConfigStore.EnableSmartCombat = _prevSmartCombat;
                CompanionConfigStore.EnableAutoBioAndSleep = _prevAutoBio;
                if (Il2CppSafe.IsAlive(_agent?.npcData?.characterData?.characterEquipmentData))
                    _agent.npcData.characterData.characterEquipmentData.backpackItemData = _origBackpackSlot;
                if (_savedSharedItems != null && Il2CppSafe.IsAlive(_agent?.npcData?.sharedInventoryData))
                {
                    RestoreInventory(_agent.npcData.sharedInventoryData, _savedSharedItems);
                    _savedSharedItems = null;
                }
                CombatLiveRegression.RestoreLogisticsArena();
                if (Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                    _agent.humanCharacter.TeleportTo(_origAgentPos, false);
                if (Il2CppSafe.IsAliveUnity(GameController.instance?.playerCharacter))
                    GameController.instance.playerCharacter.TeleportTo(_origPlayerPos, false);
            }
            catch { }
            return true;
        }
    }
}
