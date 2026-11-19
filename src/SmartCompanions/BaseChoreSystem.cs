using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Autonomous Camp Life, Base Chores &amp; Physical Vehicle Trunk Unloading (v1.7.0):
    /// - Companions physically walk up to campfires, planting pots, and parked vehicle trunks
    ///   using native A* pathfinding and PlayFarmAction hand interactions — zero item teleportation!
    /// </summary>
    public static class BaseChoreSystem
    {
        private static float _lastCampChoreScanTime = -999f;
        private static float _lastVehicleRideCalloutTime = -999f;

        public static void StepChoreWalkForAgent(GOAPAgent ag, CompanionRuntimeState st)
        {
            if (Il2CppSafe.IsAliveUnity(st.ChoreTargetAnchor))
                st.ChoreTargetPos = st.ChoreTargetAnchor.position;

            PhysicalMovementHelper.StepWalkAndInteract(
                ag, st, st.ChoreTargetPos, st.ChoreTargetAnchor, st.ChorePropItemId, 0.48f,
                out bool ready, out bool unreachable);

            if (unreachable)
            {
                if (!string.IsNullOrEmpty(st.ChoreTargetKey))
                    PhysicalMovementHelper.MarkTargetTemporarilyUnreachable(st.ChoreTargetKey, 45f);
                if (st.CarryBuffer.Count > 0)
                    BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                st.IsChoreWalkActive = false;
                st.ChoreTargetKey = "";
                st.PendingChoreArrivalAction = null;
                st.CurrentTaskText = "";
                return;
            }

            if (!ready) return;

            var act = st.PendingChoreArrivalAction;
            st.IsChoreWalkActive = false;
            st.ChoreTargetKey = "";
            st.PendingChoreArrivalAction = null;
            st.ArrivalInteractStarted = false;
            try { act?.Invoke(); } catch { }
        }

        public static void StartPhysicalChoreWalk(
            GOAPAgent ag,
            CompanionRuntimeState st,
            Vector2 targetPos,
            Transform anchor,
            string walkTaskText,
            int propItemId,
            Action onArrived,
            string targetKey = "")
        {
            st.IsChoreWalkActive = true;
            st.ChoreTargetKey = targetKey ?? "";
            st.ChoreTargetPos = targetPos;
            st.ChoreTargetAnchor = anchor;
            st.ChoreDisplayText = walkTaskText;
            st.ChorePropItemId = propItemId;
            st.PendingChoreArrivalAction = onArrived;
            st.CurrentTaskText = walkTaskText;
            PhysicalMovementHelper.BeginLeg(st, ag.humanCharacter.transform.position, targetPos, anchor);
        }

        public static bool AllowsCampChoresOrGarrison(CompanionRuntimeState st) =>
            !BaseWorkManager.Paused &&
            CompanionConfigStore.EnableBaseChores &&
            st != null &&
            st.AllowBaseChores &&
            st.WorkRole != CompanionWorkRole.Off &&
            (st.RosterMode == CompanionRosterMode.CampGarrison || BaseWorkManager.AllowsChores(st));

        public static void TickBaseChoresAndCampLife()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || !Il2CppSafe.IsAlive(gc.gameData))
                return;

            float now = Time.unscaledTime;
            var player = gc.playerCharacter;

            if (player.isDriving && Il2CppSafe.IsAliveUnity(player.drivingVehicle) && now - _lastVehicleRideCalloutTime > 48f)
            {
                if (player.drivingVehicle.speedFloat > 2.2f)
                {
                    _lastVehicleRideCalloutTime = now;
                    var rider = SmartChatterSystem.PickNearestTeammate(player.transform.position, 22f);
                    if (Il2CppSafe.IsAliveUnity(rider))
                        SmartChatterSystem.Speak(rider, ChatterTopic.PlayerDrivingRide, minCooldown: 20f, bubbleDuration: 5.5f);
                }
            }

            if (!CompanionConfigStore.EnableBaseChores || MultiplayerSyncSystem.IsMultiplayerClient) return;
            if (gc.isPlayerInCombat) return;
            if (now - _lastCampChoreScanTime < 6.5f) return;
            _lastCampChoreScanTime = now;

            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData));
                if (st.HasAnyPhysicalTask || !AllowsCampChoresOrGarrison(st)) continue;
                if (now - st.LastChoreTime < 22f) continue;

                bool atBase = CompanionSurvivalSystem.IsAtCampOrPlayerBase(ag.humanCharacter.transform.position, st);
                if (atBase && TryPerformCampfireChore(ag, st, gc, now)) continue;
                if (atBase && TryPerformGardeningChore(ag, st, gc, now)) continue;
                if (TryPerformWeaponAndArmorMaintenance(ag, st, player, now)) continue;
                if (atBase && TryPerformVehicleCheckAndUnload(ag, st, player, now)) continue;
            }
        }

        private static bool TryPerformCampfireChore(GOAPAgent ag, CompanionRuntimeState st, GameController gc, float now)
        {
            try
            {
                var fires = TerrainObject_FirePlace.ActiveObjects_FirePlace;
                if (fires == null || fires.Count == 0) return false;

                Vector2 selfPos = ag.humanCharacter.transform.position;
                float hours = (gc.gameData.inGameTime / 60f) % 24f;
                bool isNightOrRain = (hours < 6.5f || hours >= 19.5f) || gc.gameData.isRaining || gc.gameData.isBloodRaining;

                for (int i = 0; i < fires.Count; i++)
                {
                    var fp = fires[i];
                    if (!Il2CppSafe.IsAliveUnity(fp) || !fp.gameObject.activeInHierarchy) continue;
                    if (Il2CppSafe.IsAlive(fp.objectData) && fp.objectData.zLayer != ag.npcData.zLevel) continue;

                    Vector2 firePos = fp.transform.position;
                    float dist = Vector2.Distance(selfPos, firePos);
                    if (dist > 14f) continue;

                    string fireKey = $"fire_{fp.Pointer.ToInt64():X}";
                    if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(fireKey)) continue;

                    if (!fp.isBurning && isNightOrRain)
                    {
                        var fuelInv = fp.fuelInventoryData;
                        if (Il2CppSafe.IsAlive(fuelInv) && ProductionManager.FindLastCombustibleIndex(fuelInv) >= 0)
                        {
                            st.LastChoreTime = now;
                            StartPhysicalChoreWalk(
                                ag, st, firePos, fp.transform,
                                CompanionConfigStore.L("Идёт разжигать костёр...", "Walking to light campfire...", "正前往点燃篝火..."),
                                0,
                                () =>
                                {
                                    if (!Il2CppSafe.IsAliveUnity(fp) || fp.isBurning) return;
                                    if (Il2CppSafe.IsAlive(fp.fuelInventoryData) && ProductionManager.FindLastCombustibleIndex(fp.fuelInventoryData) >= 0)
                                    {
                                        fp.OnIgnite();
                                        st.ChoresDoneThisSession++;
                                        st.AddExperience(8, ag);
                                        st.CurrentTaskText = CompanionConfigStore.L("🔥 Разжёг костёр в лагере", "🔥 Lit the camp fire", "🔥 已点燃营地篝火");
                                        SmartChatterSystem.Speak(ag, ChatterTopic.CampfireStory, minCooldown: 5f, bubbleDuration: 6f);
                                    }
                                },
                                fireKey);
                            return true;
                        }
                    }

                    if (fp.isBurning && dist <= 5.5f)
                    {
                        var status = ag.npcData.characterData?.characterStatusData;
                        bool needsWarmthOrRest = (status != null && (status.fatigue < 85f || status.sanity < 90f));
                        if (needsWarmthOrRest || UnityEngine.Random.value < 0.25f)
                        {
                            st.LastChoreTime = now;
                            st.ChoresDoneThisSession++;
                            if (status != null)
                            {
                                status.fatigue = Mathf.Min(status.maxFatigue > 0 ? status.maxFatigue : 100f, status.fatigue + 12f);
                                status.sanity = Mathf.Min(status.maxSanity > 0 ? status.maxSanity : 100f, status.sanity + 10f);
                                status.energy = Mathf.Min(status.maxEnergy > 0 ? status.maxEnergy : 100f, status.energy + 20f);
                                status.bodyTemperature = Mathf.Lerp(status.bodyTemperature, 37.0f, 0.5f);
                            }
                            try { ag.LookAtPositionSmoothCompat(firePos, 10f); } catch { }
                            st.CurrentTaskText = CompanionConfigStore.L("🔥 Греется у костра", "🔥 Warming by campfire", "🔥 在篝火旁取暖休整");
                            SmartChatterSystem.Speak(ag, ChatterTopic.CampfireStory, minCooldown: 14f, bubbleDuration: 6f);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryPerformGardeningChore(GOAPAgent ag, CompanionRuntimeState st, GameController gc, float now)
        {
            try
            {
                Vector2 selfPos = ag.humanCharacter.transform.position;
                Vector2 playerPos = Il2CppSafe.IsAliveUnity(gc.playerCharacter) ? (Vector2)gc.playerCharacter.transform.position : selfPos;
                bool isGarrison = st.RosterMode == CompanionRosterMode.CampGarrison && st.HasHomeAnchor;
                Vector2 scanOrigin = isGarrison ? st.HomeAnchorPos : selfPos;
                const float maxGardenRadius = 28f;
                sbyte selfZ = ag.npcData.zLevel;
                BaseSortingSystem.InvalidateTerrainScanCache();
                var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(scanOrigin, maxGardenRadius);

                TerrainObject_PlantingPot carePot = null;

                for (int i = 0; i < worldObjs.Count; i++)
                {
                    var tobj = worldObjs[i];
                    if (!Il2CppSafe.IsAliveUnity(tobj) || !tobj.gameObject.activeInHierarchy) continue;
                    if (Il2CppSafe.IsAlive(tobj.objectData) && tobj.objectData.zLayer != selfZ) continue;

                    var pot = tobj.TryCast<TerrainObject_PlantingPot>();
                    if (Il2CppSafe.IsAliveUnity(pot))
                    {
                        if (!Il2CppSafe.IsAlive(pot.currentDef) || pot.IsHarvested())
                            continue;
                        Vector2 potPos = pot.transform.position;
                        if (Vector2.Distance(potPos, scanOrigin) > maxGardenRadius)
                            continue;
                        if (!isGarrison && Vector2.Distance(potPos, playerPos) > 40f)
                            continue;
                        bool allowedPot = isGarrison ||
                                          AutoLootSystem.IsPositionNearPlayerBase(potPos, 28f) ||
                                          (Il2CppSafe.IsAlive(pot.objectData) && pot.objectData.isPlayerBuilt);
                        if (!allowedPot)
                            continue;
                        string potKey = $"pot_{pot.Pointer.ToInt64():X}";
                        if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(potKey))
                            continue;

                        if (CompanionConfigStore.AutoHarvestCrops && TryIsPlantingPotRipe(pot))
                        {
                            st.LastChoreTime = now;
                            StartPhysicalChoreWalk(
                                ag, st, potPos, pot.transform,
                                CompanionConfigStore.L("🌾 Идёт собирать урожай...", "🌾 Walking to harvest crop...", "🌾 前往收获成熟作物..."),
                                0,
                                () =>
                                {
                                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                                        !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag ||
                                        !TryIsPlantingPotRipe(pot))
                                        return;

                                    var ids = new Il2CppSystem.Collections.Generic.List<int>();
                                    var nums = new Il2CppSystem.Collections.Generic.List<int>();
                                    if (TryHarvestPlantingPot(pot, ids, nums))
                                        CompleteCropHarvest(ag, st, GameController.instance, ids, nums, pot.transform.position);
                                },
                                potKey);
                            return true;
                        }

                        if (!Il2CppSafe.IsAliveUnity(carePot))
                        {
                            bool needsWater = TryGetPlantingPotWateredUntil(pot, out float wateredUntil) &&
                                              gc.gameData.inGameTime >= wateredUntil;
                            bool needsFert = !pot.IsFertilized();
                            if (needsWater || needsFert)
                                carePot = pot;
                        }
                        continue;
                    }

                    if (!CompanionConfigStore.AutoHarvestCrops) continue;
                    if (TryStartWorldCropHarvestChore(ag, st, tobj, scanOrigin, playerPos, isGarrison, now))
                        return true;
                }

                if (Il2CppSafe.IsAliveUnity(carePot))
                {
                    Vector2 potPos = carePot.transform.position;
                    st.LastChoreTime = now;
                    StartPhysicalChoreWalk(
                        ag, st, potPos, carePot.transform,
                        CompanionConfigStore.L("Идёт ухаживать за грядкой...", "Walking to tend crop pot...", "正前往照料农作物..."),
                        0,
                        () =>
                        {
                            var curGc = GameController.instance;
                            if (!Il2CppSafe.IsAliveUnity(carePot) || !Il2CppSafe.IsAlive(carePot.currentDef) ||
                                carePot.IsHarvested() || !Il2CppSafe.IsAliveUnity(curGc) || !Il2CppSafe.IsAlive(curGc.gameData))
                                return;

                            if (TryGetPlantingPotWateredUntil(carePot, out float wu) && curGc.gameData.inGameTime >= wu)
                            {
                                TryWaterPlantingPot(carePot);
                            }
                            if (!carePot.IsFertilized()) carePot.ApplyFertilizer(1.25f, 1, 0);
                            st.ChoresDoneThisSession++;
                            st.AddExperience(8, ag);
                            st.CurrentTaskText = CompanionConfigStore.L("🌱 Полил и удобрил грядку", "🌱 Watered & fertilized crop", "🌱 已为农作物浇水施肥");
                            SmartChatterSystem.Speak(ag, ChatterTopic.ChoreGardening, minCooldown: 8f, bubbleDuration: 6f);
                        },
                        $"pot_{carePot.Pointer.ToInt64():X}");
                    return true;
                }
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsPlantingPotRipe(TerrainObject_PlantingPot pot)
        {
            try
            {
                return IsPlantingPotRipeNoInline(pot);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsPlantingPotRipeNoInline(TerrainObject_PlantingPot pot)
        {
            if (!Il2CppSafe.IsAliveUnity(pot) || !Il2CppSafe.IsAlive(pot.currentDef) || pot.IsHarvested())
                return false;
            var stages = pot.currentDef.cropStageLengthAry;
            if (stages == null || stages.Length == 0)
                return false;
            float growth = pot.ParseProp(TerrainObject_PlantingPot.PROP_GROWTH, 0f);
            float matureTime = pot.currentDef.CumulativeStageTime(stages.Length - 1);
            return matureTime > 0.01f && growth >= matureTime;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryHarvestPlantingPot(
            TerrainObject_PlantingPot pot,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            if (!Il2CppSafe.IsAliveUnity(pot)) return false;
            if (MultiplayerSyncSystem.IsMultiplayerHost)
            {
                try
                {
                    return HarvestPlantingPotNetHostNoInline(pot, ids, nums);
                }
                catch { }
            }
            try
            {
                return HarvestPlantingPotCoreNoInline(pot, ids, nums);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarvestPlantingPotNetHostNoInline(
            TerrainObject_PlantingPot pot,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            return pot.NetHostHarvest(ids, nums);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarvestPlantingPotCoreNoInline(
            TerrainObject_PlantingPot pot,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            return pot.HarvestCore(ids, nums);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetPlantingPotWateredUntil(TerrainObject_PlantingPot pot, out float wateredUntil)
        {
            wateredUntil = 0f;
            if (!Il2CppSafe.IsAliveUnity(pot)) return false;
            try
            {
                wateredUntil = GetPlantingPotWateredUntilNoInline(pot);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetPlantingPotWateredUntilNoInline(TerrainObject_PlantingPot pot)
        {
            return pot.ParseProp(TerrainObject_PlantingPot.PROP_WATERED_UNTIL, 0f);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryWaterPlantingPot(TerrainObject_PlantingPot pot)
        {
            if (!Il2CppSafe.IsAliveUnity(pot)) return;
            if (MultiplayerSyncSystem.IsMultiplayerHost)
            {
                try
                {
                    WaterPlantingPotNetHostNoInline(pot);
                    return;
                }
                catch { }
            }
            try
            {
                pot.ApplyWatering();
            }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WaterPlantingPotNetHostNoInline(TerrainObject_PlantingPot pot)
        {
            pot.NetHostWater();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryStartWorldCropHarvestChore(
            GOAPAgent ag, CompanionRuntimeState st, TerrainObject tobj,
            Vector2 scanOrigin, Vector2 playerPos, bool isGarrison, float now)
        {
            try
            {
                return TryStartWorldCropHarvestChoreNoInline(ag, st, tobj, scanOrigin, playerPos, isGarrison, now);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryStartWorldCropHarvestChoreNoInline(
            GOAPAgent ag, CompanionRuntimeState st, TerrainObject tobj,
            Vector2 scanOrigin, Vector2 playerPos, bool isGarrison, float now)
        {
            var crop = tobj.TryCast<TerrainObject_Crop>();
            if (!Il2CppSafe.IsAliveUnity(crop) || !TryIsWorldCropRipe(crop))
                return false;

            Vector2 cropPos = crop.transform.position;
            if (Vector2.Distance(cropPos, scanOrigin) > 28f)
                return false;
            if (!isGarrison && Vector2.Distance(cropPos, playerPos) > 40f)
                return false;
            if (!AutoLootSystem.IsPositionNearPlayerBase(cropPos, 24f) && !isGarrison)
                return false;

            string cropKey = $"crop_{crop.Pointer.ToInt64():X}";
            if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(cropKey))
                return false;

            st.LastChoreTime = now;
            StartPhysicalChoreWalk(
                ag, st, cropPos, crop.transform,
                CompanionConfigStore.L("🌾 Идёт собирать урожай...", "🌾 Walking to harvest crop...", "🌾 前往收获成熟作物..."),
                0,
                () =>
                {
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                        !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag ||
                        !Il2CppSafe.IsAliveUnity(crop) || !TryIsWorldCropRipe(crop))
                        return;

                    var ids = new Il2CppSystem.Collections.Generic.List<int>();
                    var nums = new Il2CppSystem.Collections.Generic.List<int>();
                    if (TryHarvestWorldCrop(crop, ids, nums))
                        CompleteCropHarvest(ag, st, GameController.instance, ids, nums, crop.transform.position);
                },
                cropKey);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsWorldCropRipe(TerrainObject_Crop crop)
        {
            try
            {
                return IsWorldCropRipeNoInline(crop);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsWorldCropRipeNoInline(TerrainObject_Crop crop)
        {
            return Il2CppSafe.IsAliveUnity(crop) &&
                   !crop.harvested &&
                   crop.cropStageLengthAry != null &&
                   crop.cropStageLengthAry.Length > 0 &&
                   crop.cropCurrentStage >= crop.cropStageLengthAry.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryHarvestWorldCrop(
            TerrainObject_Crop crop,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            if (!Il2CppSafe.IsAliveUnity(crop)) return false;
            if (MultiplayerSyncSystem.IsMultiplayerHost)
            {
                try
                {
                    return HarvestWorldCropNetHostNoInline(crop, ids, nums);
                }
                catch { }
            }
            try
            {
                return HarvestWorldCropCoreNoInline(crop, ids, nums);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarvestWorldCropNetHostNoInline(
            TerrainObject_Crop crop,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            return crop.NetHostHarvest(ids, nums);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HarvestWorldCropCoreNoInline(
            TerrainObject_Crop crop,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums)
        {
            return crop.HarvestCore(ids, nums);
        }

        private static void CompleteCropHarvest(
            GOAPAgent ag, CompanionRuntimeState st, GameController gc,
            Il2CppSystem.Collections.Generic.List<int> ids,
            Il2CppSystem.Collections.Generic.List<int> nums,
            Vector2 dropPos)
        {
            try
            {
                var im = ItemManager.instance;
                var sharedInv = ag.npcData.GetOrCreateSharedInventory();
                var cdInv = ag.npcData.characterData?.inventoryData;

                if (Il2CppSafe.IsAlive(im) && ids != null && nums != null)
                {
                    int pairCount = Mathf.Min(ids.Count, nums.Count);
                    for (int k = 0; k < pairCount; k++)
                    {
                        int itemId = ids[k];
                        int remaining = nums[k];
                        if (itemId <= 0 || remaining <= 0) continue;

                        var attr = im.GetItemAttrById(itemId);
                        if (!Il2CppSafe.IsAliveUnity(attr)) continue;
                        int maxStack = attr.stackNumber > 0 ? attr.stackNumber : remaining;

                        while (remaining > 0)
                        {
                            int batch = Mathf.Min(remaining, maxStack);
                            remaining -= batch;
                            var itemData = im.GetRandomDataByAttr(attr, false, false);
                            if (!Il2CppSafe.IsAlive(itemData)) continue;
                            itemData.itemNumberFloat = batch;

                            if (Il2CppSafe.IsAlive(sharedInv) && InventoryTransfers.TryDepositOrStackWhole(sharedInv, itemData))
                                continue;
                            if (Il2CppSafe.IsAlive(cdInv) && InventoryTransfers.TryDepositOrStackWhole(cdInv, itemData))
                                continue;

                            if (Il2CppSafe.IsAliveUnity(gc))
                            {
                                var dropped = gc.DropItem(itemData, dropPos, 0f, 0f, false, 0f, ag.npcData.zLevel, ag.humanCharacter.standVz);
                                if (!Il2CppSafe.IsAliveUnity(dropped))
                                    gc.AddItemToPlayer(itemData, true);
                            }
                        }
                    }
                }

                st.LastAutoDropoffTime = -999f;
                st.ChoresDoneThisSession++;
                st.AddExperience(10, ag);
                st.CurrentTaskText = CompanionConfigStore.L(
                    "🌾 Собрал урожай с грядки",
                    "🌾 Harvested ripe crop",
                    "🌾 收获了成熟作物");
                SmartChatterSystem.Speak(ag, ChatterTopic.ChoreGardening, minCooldown: 8f, bubbleDuration: 6f);
            }
            catch { }
        }

        private static bool TryPerformWeaponAndArmorMaintenance(GOAPAgent ag, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            try
            {
                var mode = CompanionConfigStore.AutoRepairMode;
                if (mode == GearAutoRepairMode.Off)
                    return false;

                var npcResult = CompanionGearRepairSystem.TryRunGearRepairCycleForAgent(
                    ag, st, now, ignoreCooldown: false, allowSpeak: false);
                int repairedItems = npcResult.TotalRepaired;

                if ((mode == GearAutoRepairMode.MaterialsOrField || mode == GearAutoRepairMode.FreeMaintenance) &&
                    Il2CppSafe.IsAliveUnity(player) &&
                    Vector2.Distance(ag.humanCharacter.transform.position, player.transform.position) <= 2.8f)
                {
                    float maintFraction = CompanionGearRepairSystem.ComputeMaintenanceFraction(st);
                    repairedItems += CompanionGearRepairSystem.RepairEquippedGearMaintenanceOnly(
                        player, player.characterData, maintFraction);
                }

                if (repairedItems > 0)
                {
                    st.LastChoreTime = now;
                    if (npcResult.TotalRepaired == 0)
                    {
                        st.ChoresDoneThisSession++;
                        st.AddExperience(10, ag);
                        st.CurrentTaskText = CompanionConfigStore.L(
                            "🔧 Чистка оружия и ремонт брони",
                            "🔧 Weapon & armor maintenance",
                            "🔧 武器擦拭与护具维护");
                    }
                    SmartChatterSystem.Speak(ag, ChatterTopic.ChoreWeaponMaint, minCooldown: 12f, bubbleDuration: 5.5f);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryPerformVehicleCheckAndUnload(GOAPAgent ag, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            try
            {
                if (!CompanionConfigStore.EnableBaseAutoDropoff || !st.AllowAutoDropoffAtBase) return false;
                if (!AllowsCampChoresOrGarrison(st)) return false;
                if (player.isDriving) return false;

                int started = CommandUnloadVehiclesToBase(ag, silentIfEmpty: true);
                if (started > 0)
                {
                    st.LastChoreTime = now;
                    st.ChoresDoneThisSession++;
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static int CommandUnloadVehiclesToBase(GOAPAgent specificAgent = null, bool silentIfEmpty = false)
        {
            int startedVehicles = 0;
            try
            {
                if (MultiplayerSyncSystem.IsMultiplayerClient)
                {
                    if (!silentIfEmpty)
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            "🚗 Разгрузка машин выполняется напарниками на стороне хоста.",
                            "🚗 Vehicle trunk unloading runs on the host's companions.",
                            "🚗 载具后备箱卸载由主机端队友执行。"));
                    return 0;
                }

                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return 0;

                var vehicles = BasicVehicle.s_liveVehicles;
                if (vehicles == null || vehicles.Count == 0)
                {
                    if (!silentIfEmpty)
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            "🚗 Рядом не найдено автомобилей для разгрузки багажника.",
                            "🚗 No nearby vehicles found to unload.",
                            "🚗 附近未发现可卸载后备箱的载具。"));
                    return 0;
                }

                Vector2 playerPos = gc.playerCharacter.transform.position;
                var containers = BaseSortingSystem.ScanNearbyContainers(force: !silentIfEmpty);
                bool hasMarkedDest = false;
                for (int i = 0; i < containers.Count; i++)
                {
                    var c = containers[i];
                    if (c?.Rule != null && c.Rule.IsMarkedByPlayer && (c.Rule.Categories != StorageCategory.None || c.Rule.IsDropoffSource))
                    {
                        hasMarkedDest = true;
                        break;
                    }
                }

                if (!hasMarkedDest)
                {
                    if (!silentIfEmpty)
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            "🚗 Нет отмеченных по категориям ящиков базы рядом с машиной!",
                            "🚗 No player-marked category chests found near the vehicle!",
                            "🚗 载具附近未找到已标记分类的基地储物箱！"));
                    return 0;
                }

                for (int i = 0; i < vehicles.Count; i++)
                {
                    var v = vehicles[i];
                    if (!Il2CppSafe.IsAliveUnity(v) || !Il2CppSafe.IsAlive(v.vehicleData)) continue;
                    if (Vector2.Distance(v.transform.position, playerPos) > 28f) continue;

                    var trunkInv = v.vehicleData.inventoryData;
                    if (!Il2CppSafe.IsAlive(trunkInv) || trunkInv.itemList == null || trunkInv.itemList.Count == 0)
                        continue;

                    string carLabel = CompanionConfigStore.L("Багажник авто", "Vehicle Trunk", "汽车后备箱");
                    BaseSortingSystem.RegisterExternalInventoryAnchor(trunkInv, v.transform, carLabel);
                    BaseSortingSystem.CommandSortBase(specificAgent, trunkInv, instant: false);
                    startedVehicles++;
                    break;
                }

                if (startedVehicles > 0 && !silentIfEmpty)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "🚗 Напарники пошли к автомобилю выгружать багажник и разносить хабар по ящикам базы!",
                        "🚗 Companions are walking to the vehicle trunk to carry cargo into your base chests!",
                        "🚗 队友正前往汽车后备箱，将战利品搬运至基地分类储物箱！"));
                }
                else if (startedVehicles == 0 && !silentIfEmpty)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "🚗 Багажник ближайшего автомобиля уже пуст (или нет подходящих ящиков).",
                        "🚗 Nearby vehicle trunk is already empty (or no matching chests found).",
                        "🚗 附近车辆后备箱已空（或未找到匹配的分类储物箱）。"));
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandUnloadVehiclesToBase error: " + e.Message);
            }
            return startedVehicles;
        }
    }
}
