using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class AutoLootSystem
    {
        private static List<PhysicalLootStop> DiscoverPhysicalLootStops(
            Vector2 center,
            float radius,
            sbyte floor,
            HashSet<string> excludedKeys,
            bool isPassiveAutoLoot = false)
        {
            var stops = new List<PhysicalLootStop>();
            float rSqr = radius * radius;

            // 1. Loose ground items (clustered within 1.45m so one physical walk picks up a pile)
            var seenItemPtrs = new HashSet<IntPtr>();
            var groundItems = new List<BasicItem>();

            var activeItems = BasicItem.ActiveObjects;
            if (activeItems != null)
            {
                for (int i = 0; i < activeItems.Count; i++)
                {
                    var bi = activeItems[i];
                    if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                        continue;
                    if (bi.itemData.deployedFlag || bi.itemData.zLevel != floor) continue;
                    if (isPassiveAutoLoot && (bi.standVz > 0 || IsPositionNearPlayerBase(bi.transform.position, 18f)))
                        continue;
                    if (((Vector2)bi.transform.position - center).sqrMagnitude > rSqr) continue;
                    if (seenItemPtrs.Add(bi.Pointer))
                        groundItems.Add(bi);
                }
            }

            var igc = InGameController.instance;
            var itemParent = Il2CppSafe.IsAliveUnity(igc) ? igc.itemParentIfExistsSafe() : null;
            if (Il2CppSafe.IsAliveUnity(itemParent))
            {
                int childCount = itemParent.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var tr = itemParent.GetChild(i);
                    if (!Il2CppSafe.IsAliveUnity(tr) || !tr.gameObject.activeInHierarchy) continue;
                    if (((Vector2)tr.position - center).sqrMagnitude > rSqr) continue;
                    var bi = tr.GetComponent<BasicItem>();
                    if (!Il2CppSafe.IsAliveUnity(bi) || !Il2CppSafe.IsAlive(bi.itemData) || bi.itemData.deployedFlag)
                        continue;
                    if (bi.itemData.zLevel != floor) continue;
                    if (isPassiveAutoLoot && (bi.standVz > 0 || IsPositionNearPlayerBase(bi.transform.position, 18f)))
                        continue;
                    if (seenItemPtrs.Add(bi.Pointer))
                        groundItems.Add(bi);
                }
            }

            for (int i = 0; i < groundItems.Count; i++)
            {
                var bi = groundItems[i];
                Vector2 pos = bi.transform.position;
                bool merged = false;
                for (int s = 0; s < stops.Count; s++)
                {
                    if (stops[s].Kind == PhysicalLootKind.GroundCluster &&
                        (stops[s].WorldPos - pos).sqrMagnitude <= 1.45f * 1.45f)
                    {
                        merged = true;
                        break;
                    }
                }
                if (merged) continue;

                string key = $"gnd_{Mathf.RoundToInt(pos.x * 10f)}_{Mathf.RoundToInt(pos.y * 10f)}_{floor}";
                if (excludedKeys.Contains(key) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(key))
                    continue;

                stops.Add(new PhysicalLootStop
                {
                    Kind = PhysicalLootKind.GroundCluster,
                    WorldPos = pos,
                    TargetTransform = bi.transform,
                    PrimaryGroundItem = bi,
                    DisplayName = bi.itemData.GetItemName() ?? CompanionConfigStore.L("Предмет на земле", "Ground Item", "地面物品"),
                    TargetKey = key
                });
            }

            // 2. Zombie corpses & unmarked wild furniture/fridge containers
            var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(center, radius);
            for (int i = 0; i < worldObjs.Count; i++)
            {
                var tobj = worldObjs[i];
                if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) continue;
                if (tobj.objectData.zLayer != floor) continue;

                InventoryData inv = null;
                PhysicalLootKind kind = PhysicalLootKind.WildContainer;
                string label = null;

                var corpse = tobj.TryCast<TerrainObject_Corpse>();
                if (Il2CppSafe.IsAliveUnity(corpse))
                {
                    inv = corpse.objectData.inventoryData;
                    kind = PhysicalLootKind.Corpse;
                    label = CompanionConfigStore.L("Труп", "Corpse", "尸体");
                }
                else if (!isPassiveAutoLoot)
                {
                    var furn = tobj.TryCast<TerrainObject_Furniture_Inventory>();
                    var fridge = tobj.TryCast<TerrainObject_Production_Fridge>();
                    if (Il2CppSafe.IsAliveUnity(furn) || Il2CppSafe.IsAliveUnity(fridge))
                    {
                        if (IsProtectedPlayerBaseContainer(tobj, tobj.objectData.inventoryData))
                            continue;
                        inv = BaseSortingSystem.ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: true);
                        label = tobj.attr?.name ?? CompanionConfigStore.L("Ящик", "Container", "容器");
                    }
                }

                if (!Il2CppSafe.IsAlive(inv) || inv.isLocked || inv.itemList == null || inv.itemList.Count == 0)
                    continue;

                string key = $"lobj_{tobj.Pointer.ToInt64():X}";
                if (excludedKeys.Contains(key) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(key))
                    continue;

                stops.Add(new PhysicalLootStop
                {
                    Kind = kind,
                    WorldPos = tobj.transform.position,
                    TargetTransform = tobj.transform,
                    TargetTerrainObj = tobj,
                    TargetInventory = inv,
                    DisplayName = label,
                    TargetKey = key
                });
            }

            return stops;
        }

        private static void BeginCurrentLootLeg(GOAPAgent agent, CompanionRuntimeState st)
        {
            while (st.LootQueue.Count > 0 && !IsLootStopStillValid(st.LootQueue[0]))
                st.LootQueue.RemoveAt(0);

            if (st.LootQueue.Count == 0)
            {
                FinishLootingSweep(agent, st);
                return;
            }

            var stop = st.LootQueue[0];
            Vector2 selfPos = agent.humanCharacter.transform.position;
            PhysicalMovementHelper.BeginLeg(st, selfPos, stop.WorldPos, stop.TargetTransform);
            st.CurrentTaskText = CompanionConfigStore.L(
                $"Идёт за лутом: «{stop.DisplayName}»...",
                $"Walking to loot '{stop.DisplayName}'...",
                $"正前往搜刮「{stop.DisplayName}」...");
        }

        private static bool IsLootStopStillValid(PhysicalLootStop stop)
        {
            if (stop == null) return false;
            if (stop.Kind == PhysicalLootKind.GroundCluster)
            {
                if (Il2CppSafe.IsAliveUnity(stop.PrimaryGroundItem) &&
                    stop.PrimaryGroundItem.gameObject.activeInHierarchy &&
                    Il2CppSafe.IsAlive(stop.PrimaryGroundItem.itemData))
                    return true;
                return false;
            }
            return Il2CppSafe.IsAliveUnity(stop.TargetTerrainObj) &&
                   stop.TargetTerrainObj.gameObject.activeInHierarchy &&
                   Il2CppSafe.IsAlive(stop.TargetInventory) &&
                   !stop.TargetInventory.isLocked &&
                   stop.TargetInventory.itemList != null &&
                   stop.TargetInventory.itemList.Count > 0;
        }

        public static void StepLootingJobForAgent(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st.LootQueue.Count == 0 || !IsLootStopStillValid(st.LootQueue[0]))
            {
                BeginCurrentLootLeg(agent, st);
                return;
            }

            var stop = st.LootQueue[0];
            if (Il2CppSafe.IsAliveUnity(stop.TargetTransform))
                stop.WorldPos = stop.TargetTransform.position;

            int previewPropId = 0;
            if (stop.Kind == PhysicalLootKind.GroundCluster && Il2CppSafe.IsAlive(stop.PrimaryGroundItem?.itemData))
                previewPropId = stop.PrimaryGroundItem.itemData.itemId;
            else if (Il2CppSafe.IsAlive(stop.TargetInventory) && stop.TargetInventory.itemList != null && stop.TargetInventory.itemList.Count > 0)
                previewPropId = stop.TargetInventory.itemList[0]?.itemId ?? 0;

            PhysicalMovementHelper.StepWalkAndInteract(
                agent, st, stop.WorldPos, stop.TargetTransform, previewPropId, 0.45f,
                out bool ready, out bool unreachable,
                arrivalRadius: PhysicalMovementHelper.DefaultArrivalRadius,
                stallSeconds: PhysicalMovementHelper.DefaultStallSeconds);

            if (st.ArrivalInteractStarted && !ready)
            {
                st.CurrentTaskText = CompanionConfigStore.L(
                    $"Обыскивает «{stop.DisplayName}»...",
                    $"Looting '{stop.DisplayName}'...",
                    $"正在搜刮「{stop.DisplayName}」...");
                return;
            }

            if (unreachable)
            {
                PhysicalMovementHelper.MarkTargetTemporarilyUnreachable(stop.TargetKey, 45f);
                st.LootQueue.RemoveAt(0);
                BeginCurrentLootLeg(agent, st);
                return;
            }

            if (!ready) return;

            // Hard zero-teleport verification: NPC must actually be within physical reach!
            Vector2 selfPos = agent.humanCharacter.transform.position;
            float finalSurfDist = PhysicalMovementHelper.ComputeSurfaceDistance(selfPos, stop.WorldPos, stop.TargetTransform);
            if (finalSurfDist <= PhysicalMovementHelper.MaxReachableInteractRadius)
            {
                int collectedHere = ExecutePhysicalPickupAtStop(agent, st, stop);
                if (collectedHere > 0)
                {
                    st.LootPickedInCurrentSweep += collectedHere;
                    st.LootedItemsThisSession += collectedHere;
                    st.AddExperience(Mathf.Clamp(collectedHere * 2, 2, 16), agent);
                    SmartChatterSystem.Speak(agent, ChatterTopic.LootFoundGood, minCooldown: 6f, bubbleDuration: 5.0f);
                }
            }

            st.LootQueue.RemoveAt(0);
            BeginCurrentLootLeg(agent, st);
        }
    }
}
