using System;
using System.Collections.Generic;
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
                st.IsChoreWalkActive = false;
                st.PendingChoreArrivalAction = null;
                st.CurrentTaskText = "";
                return;
            }

            if (!ready) return;

            var act = st.PendingChoreArrivalAction;
            st.IsChoreWalkActive = false;
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
            Action onArrived)
        {
            st.IsChoreWalkActive = true;
            st.ChoreTargetPos = targetPos;
            st.ChoreTargetAnchor = anchor;
            st.ChoreDisplayText = walkTaskText;
            st.ChorePropItemId = propItemId;
            st.PendingChoreArrivalAction = onArrived;
            st.CurrentTaskText = walkTaskText;
            PhysicalMovementHelper.BeginLeg(st, ag.humanCharacter.transform.position, targetPos, anchor);
        }

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

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (!st.AllowBaseChores || st.HasAnyPhysicalTask) continue;
                if (now - st.LastChoreTime < 22f) continue;

                if (TryPerformCampfireChore(ag, st, gc, now)) continue;
                if (TryPerformGardeningChore(ag, st, gc, now)) continue;
                if (TryPerformWeaponAndArmorMaintenance(ag, st, player, now)) continue;
                if (TryPerformVehicleCheckAndUnload(ag, st, player, now)) continue;
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
                                });
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
                var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(selfPos, 15f);

                for (int i = 0; i < worldObjs.Count; i++)
                {
                    var tobj = worldObjs[i];
                    var pot = Il2CppSafe.IsAliveUnity(tobj) ? tobj.TryCast<TerrainObject_PlantingPot>() : null;
                    if (!Il2CppSafe.IsAliveUnity(pot) || pot.currentDef == null || pot.IsHarvested())
                        continue;

                    float wateredUntil = pot.ParseProp(TerrainObject_PlantingPot.PROP_WATERED_UNTIL, 0f);
                    bool needsWater = gc.gameData.inGameTime >= wateredUntil;
                    bool needsFert = !pot.IsFertilized();
                    if (!needsWater && !needsFert) continue;

                    Vector2 potPos = pot.transform.position;
                    st.LastChoreTime = now;
                    StartPhysicalChoreWalk(
                        ag, st, potPos, pot.transform,
                        CompanionConfigStore.L("Идёт ухаживать за грядкой...", "Walking to tend crop pot...", "正前往照料农作物..."),
                        0,
                        () =>
                        {
                            if (!Il2CppSafe.IsAliveUnity(pot) || pot.currentDef == null || pot.IsHarvested()) return;
                            float wu = pot.ParseProp(TerrainObject_PlantingPot.PROP_WATERED_UNTIL, 0f);
                            if (gc.gameData.inGameTime >= wu) pot.ApplyWatering();
                            if (!pot.IsFertilized()) pot.ApplyFertilizer(1.25f, 1, 0);
                            st.ChoresDoneThisSession++;
                            st.AddExperience(8, ag);
                            st.CurrentTaskText = CompanionConfigStore.L("🌱 Полил и удобрил грядку", "🌱 Watered & fertilized crop", "🌱 已为农作物浇水施肥");
                            SmartChatterSystem.Speak(ag, ChatterTopic.ChoreGardening, minCooldown: 8f, bubbleDuration: 6f);
                        });
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryPerformWeaponAndArmorMaintenance(GOAPAgent ag, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            try
            {
                float repairBonus = st.EffectiveArchetype == CompanionArchetype.Veteran ? 14f : 9f;
                if (st.Rank >= CompanionRank.Veteran) repairBonus += 5f;

                int repairedItems = RepairCharacterEquippedGear(ag.npcData?.characterData, repairBonus);
                if (Il2CppSafe.IsAliveUnity(player) && Vector2.Distance(ag.humanCharacter.transform.position, player.transform.position) <= 2.8f)
                {
                    repairedItems += RepairCharacterEquippedGear(player.characterData, repairBonus);
                }

                if (repairedItems > 0)
                {
                    st.LastChoreTime = now;
                    st.ChoresDoneThisSession++;
                    st.AddExperience(10, ag);
                    st.CurrentTaskText = CompanionConfigStore.L(
                        "🔧 Чистка оружия и ремонт брони",
                        "🔧 Weapon & armor maintenance",
                        "🔧 武器擦拭与护具维护");
                    SmartChatterSystem.Speak(ag, ChatterTopic.ChoreWeaponMaint, minCooldown: 12f, bubbleDuration: 5.5f);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static int RepairCharacterEquippedGear(CharacterData cd, float repairAmount)
        {
            if (!Il2CppSafe.IsAlive(cd) || !Il2CppSafe.IsAlive(cd.characterEquipmentData)) return 0;
            int count = 0;
            var eq = cd.characterEquipmentData;

            var candidates = new List<ItemData>
            {
                eq.currentWeaponItemData,
                eq.torseItemData,
                eq.hatItemData,
                eq.legItemData,
                eq.feetItemData
            };
            if (eq.weaponItemDataAry != null)
            {
                for (int i = 0; i < eq.weaponItemDataAry.Length; i++)
                    candidates.Add(eq.weaponItemDataAry[i]);
            }

            foreach (var it in candidates)
            {
                if (!Il2CppSafe.IsAlive(it)) continue;
                if (it.durability > 1f && it.durability < 92f)
                {
                    it.durability = Mathf.Min(100f, it.durability + repairAmount);
                    count++;
                }
            }
            return count;
        }

        private static bool TryPerformVehicleCheckAndUnload(GOAPAgent ag, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            try
            {
                if (!CompanionConfigStore.EnableBaseAutoDropoff || !st.AllowAutoDropoffAtBase) return false;
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
                    if (c?.Rule != null && c.Rule.IsMarkedByPlayer && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
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
