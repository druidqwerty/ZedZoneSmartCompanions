using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Autonomous Camp Life, Base Chores &amp; Vehicle Trunk Logistics for ZED ZONE Smart Companions v1.3.0:
    /// - Campfire Keeper &amp; Warming (ignites unlit campfires in cold/rain/night, warms hands, tells campfire stories)
    /// - Base Gardener (waters &amp; fertilizes nearby PlantingPots automatically)
    /// - Field Armorer &amp; Mechanic (cleans/repairs squad &amp; player weapons/armor and inspects player vehicles)
    /// - Vehicle Trunk Unloader (unloads items from nearby player vehicle trunks straight into categorized base chests)
    /// </summary>
    public static class BaseChoreSystem
    {
        private static float _lastCampChoreScanTime = -999f;
        private static float _lastVehicleRideCalloutTime = -999f;

        public static void TickBaseChoresAndCampLife()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || !Il2CppSafe.IsAlive(gc.gameData))
                return;

            float now = Time.unscaledTime;
            var player = gc.playerCharacter;

            // Check if player is currently driving a vehicle -> companion road-trip callout!
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

            if (!CompanionConfigStore.EnableBaseChores) return;
            try
            {
                if (ZedZone.Net.NetworkManager.isClient) return;
            }
            catch { }
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
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter)) continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (!st.AllowBaseChores || st.IsSortingActive) continue;
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

                    // If campfire is unlit at night or during rain, companion lights it up!
                    // Never call fp.IgnitionFire(true) here: native IgnitionFire(true) (0x5B29F0)
                    // opens the local player's ToolSelectManager fire-lighting UI (0x1529D30)!
                    // Instead, check fuel and call fp.OnIgnite() (0x5B4450) directly.
                    if (!fp.isBurning && isNightOrRain)
                    {
                        var fuelInv = fp.fuelInventoryData;
                        if (Il2CppSafe.IsAlive(fuelInv) && ProductionManager.FindLastCombustibleIndex(fuelInv) >= 0)
                        {
                            fp.OnIgnite();
                            st.LastChoreTime = now;
                            st.ChoresDoneThisSession++;
                            st.AddExperience(8, ag);
                            st.CurrentTaskText = CompanionConfigStore.L("🔥 Разжёг костёр в лагере", "🔥 Lit the camp fire", "🔥 已点燃营地篝火");
                            SmartChatterSystem.Speak(ag, ChatterTopic.CampfireStory, minCooldown: 5f, bubbleDuration: 6f);
                            return true;
                        }
                    }

                    // If campfire is burning and companion is nearby, warm hands & restore comfort/morale
                    if (fp.isBurning && dist <= 6.8f)
                    {
                        var status = ag.npcData.characterData?.characterStatusData;
                        bool needsWarmthOrRest = (status != null && (status.fatigue < 85f || status.sanity < 90f));
                        if (needsWarmthOrRest || UnityEngine.Random.value < 0.28f)
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
                var parent = gc.terrainObjectsParent;
                if (!Il2CppSafe.IsAliveUnity(parent)) return false;

                Vector2 selfPos = ag.humanCharacter.transform.position;
                int childCount = parent.childCount;
                int caredCount = 0;
                Vector2 lastPotPos = selfPos;

                for (int i = 0; i < childCount; i++)
                {
                    var tr = parent.GetChild(i);
                    if (!Il2CppSafe.IsAliveUnity(tr) || !tr.gameObject.activeInHierarchy) continue;

                    Vector2 pos = tr.position;
                    if ((pos - selfPos).sqrMagnitude > 16f * 16f) continue;

                    var pot = tr.GetComponent<TerrainObject_PlantingPot>();
                    if (!Il2CppSafe.IsAliveUnity(pot) || pot.currentDef == null || pot.IsHarvested())
                        continue;

                    bool didCare = false;
                    // Check if pot needs watering or fertilizer
                    float wateredUntil = pot.ParseProp(TerrainObject_PlantingPot.PROP_WATERED_UNTIL, 0f);
                    if (gc.gameData.inGameTime >= wateredUntil)
                    {
                        pot.ApplyWatering();
                        didCare = true;
                    }
                    if (!pot.IsFertilized())
                    {
                        pot.ApplyFertilizer(1.25f, 1, 0);
                        didCare = true;
                    }

                    if (didCare)
                    {
                        caredCount++;
                        lastPotPos = pos;
                        if (caredCount >= 4) break;
                    }
                }

                if (caredCount > 0)
                {
                    st.LastChoreTime = now;
                    st.ChoresDoneThisSession += caredCount;
                    st.AddExperience(8 * caredCount, ag);
                    try
                    {
                        ag.LookAtPositionSmoothCompat(lastPotPos, 12f);
                    }
                    catch { }
                    st.CurrentTaskText = CompanionConfigStore.L(
                        $"🌱 Уход за грядками ({caredCount} шт.)",
                        $"🌱 Tended crops ({caredCount} pots)",
                        $"🌱 照料农作物（{caredCount} 盆）");
                    SmartChatterSystem.Speak(ag, ChatterTopic.ChoreGardening, minCooldown: 8f, bubbleDuration: 6f);
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

                int repairedItems = 0;
                repairedItems += RepairCharacterEquippedGear(ag.npcData?.characterData, repairBonus);
                if (Il2CppSafe.IsAliveUnity(player) && Vector2.Distance(ag.humanCharacter.transform.position, player.transform.position) <= 10f)
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

                int unloaded = CommandUnloadVehiclesToBase(ag, silentIfEmpty: true);
                if (unloaded > 0)
                {
                    st.LastChoreTime = now;
                    st.ChoresDoneThisSession++;
                    st.AddExperience(12, ag);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Unloads items from nearby vehicles' trunks/storage into categorized base chests (or drop-off chests),
        /// and performs a light field tune-up on vehicle bodywork.
        /// </summary>
        public static int CommandUnloadVehiclesToBase(GOAPAgent specificAgent = null, bool silentIfEmpty = false)
        {
            int totalMoved = 0;
            try
            {
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
                var destBoxes = new List<DiscoveredContainer>();
                foreach (var c in containers)
                {
                    if (c.Rule != null && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
                        destBoxes.Add(c);
                }

                if (destBoxes.Count == 0)
                {
                    if (!silentIfEmpty)
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            "🚗 Нет размеченных по категориям ящиков базы рядом с машиной!",
                            "🚗 No categorized base chests found near the vehicle!",
                            "🚗 载具附近未找到已设置分类的基地储物箱！"));
                    return 0;
                }

                var workers = new List<GOAPAgent>();
                if (Il2CppSafe.IsAliveUnity(specificAgent))
                    workers.Add(specificAgent);
                else
                    workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

                for (int i = 0; i < vehicles.Count; i++)
                {
                    var v = vehicles[i];
                    if (!Il2CppSafe.IsAliveUnity(v) || !Il2CppSafe.IsAlive(v.vehicleData)) continue;
                    float dist = Vector2.Distance(v.transform.position, playerPos);
                    if (dist > 28f) continue;

                    // Light bodywork maintenance while inspecting vehicle
                    try
                    {
                        if (Il2CppSafe.IsAlive(v.bodyworkData) && v.bodyworkData.durability < 95f)
                            v.bodyworkData.durability = Mathf.Min(100f, v.bodyworkData.durability + 5f);
                    }
                    catch { }

                    var trunkInv = v.vehicleData.inventoryData;
                    if (Il2CppSafe.IsAlive(trunkInv) && trunkInv.itemList != null && trunkInv.itemList.Count > 0)
                    {
                        BaseSortingSystem.CommandSortBase(specificAgent, trunkInv, instant: true);
                        totalMoved++;
                    }
                }

                if (totalMoved > 0)
                {
                    var speaker = workers.Count > 0 ? workers[0] : null;
                    if (Il2CppSafe.IsAliveUnity(speaker))
                    {
                        var st = CompanionConfigStore.GetOrCreateCompanion(speaker.npcData.npcGUID, speaker.npcData.characterData?.characterName);
                        st.SortedItemsThisSession += totalMoved;
                        st.ChoresDoneThisSession++;
                        st.AddExperience(15, speaker);
                        st.CurrentTaskText = CompanionConfigStore.L(
                            "🚗 Разгрузил багажник машины на склад",
                            "🚗 Unloaded vehicle trunk to base",
                            "🚗 已将车载后备箱物资卸入仓库");
                        SmartChatterSystem.Speak(speaker, ChatterTopic.ChoreVehicleCheck, minCooldown: 1f, bubbleDuration: 6f, force: true);
                    }
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "🚗 Напарники разгрузили багажник автомобиля и разложили трофеи по ящикам базы!",
                        "🚗 Companions unloaded vehicle trunk cargo into categorized base chests!",
                        "🚗 队友已将汽车后备箱战利品全部卸下并按类别归入基地储物箱！"));
                }
                else if (!silentIfEmpty)
                {
                    var speaker = workers.Count > 0 ? workers[0] : null;
                    if (Il2CppSafe.IsAliveUnity(speaker))
                        SmartChatterSystem.Speak(speaker, ChatterTopic.ChoreVehicleCheck, minCooldown: 1f, bubbleDuration: 5.5f, force: true);
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "🚗 Машина осмотрена, багажник уже пуст (или все вещи на своих местах).",
                        "🚗 Vehicle inspected — trunk is already empty or sorted.",
                        "🚗 车辆巡检完毕——后备箱已清空或无可分拣物品。"));
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandUnloadVehiclesToBase error: " + e.Message);
            }
            return totalMoved;
        }
    }
}
