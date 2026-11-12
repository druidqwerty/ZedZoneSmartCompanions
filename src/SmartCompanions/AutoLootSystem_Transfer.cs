using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class AutoLootSystem
    {
        private static int ExecutePhysicalPickupAtStop(GOAPAgent agent, CompanionRuntimeState st, PhysicalLootStop stop)
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                return 0;

            int collected = 0;
            float now = Time.unscaledTime;
            Vector2 selfPos = agent.humanCharacter.transform.position;
            sbyte floor = Il2CppSafe.IsAlive(agent.npcData) ? agent.npcData.zLevel : (sbyte)0;

            if (stop.Kind == PhysicalLootKind.GroundCluster)
            {
                float maxPickupSqr = PhysicalMovementHelper.MaxReachableInteractRadius * PhysicalMovementHelper.MaxReachableInteractRadius;
                var candidates = new List<BasicItem>();
                var seen = new HashSet<IntPtr>();

                if (Il2CppSafe.IsAliveUnity(stop.PrimaryGroundItem))
                {
                    seen.Add(stop.PrimaryGroundItem.Pointer);
                    candidates.Add(stop.PrimaryGroundItem);
                }

                var activeItems = BasicItem.ActiveObjects;
                if (activeItems != null)
                {
                    for (int i = 0; i < activeItems.Count; i++)
                    {
                        var bi = activeItems[i];
                        if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                            continue;
                        if (bi.itemData.deployedFlag || bi.itemData.zLevel != floor) continue;
                        if (((Vector2)bi.transform.position - selfPos).sqrMagnitude > maxPickupSqr) continue;
                        if (seen.Add(bi.Pointer))
                            candidates.Add(bi);
                    }
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    var bi = candidates[i];
                    if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                        continue;
                    if (PhysicalMovementHelper.ComputeSurfaceDistance(selfPos, bi.transform.position, bi.transform) > PhysicalMovementHelper.MaxReachableInteractRadius)
                        continue;
                    if (!PassesSmartOverweightFilter(agent, bi.itemData, now))
                        continue;
                    if (TryCollectGroundItem(agent, bi.itemData, gc))
                        collected++;
                }
            }
            else if (Il2CppSafe.IsAlive(stop.TargetInventory) && !stop.TargetInventory.isLocked &&
                     stop.TargetInventory.itemList != null && stop.TargetInventory.itemList.Count > 0)
            {
                var snap = new List<ItemData>();
                for (int i = 0; i < stop.TargetInventory.itemList.Count; i++)
                {
                    var it = stop.TargetInventory.itemList[i];
                    if (Il2CppSafe.IsAlive(it)) snap.Add(it);
                }

                for (int i = 0; i < snap.Count; i++)
                {
                    var item = snap[i];
                    if (!Il2CppSafe.IsAlive(item)) continue;
                    if (!PassesSmartOverweightFilter(agent, item, now))
                        continue;
                    if (TryTransferToCompanion(agent, stop.TargetInventory, item))
                        collected++;
                }
            }

            return collected;
        }

        private static void FinishLootingSweep(GOAPAgent agent, CompanionRuntimeState st)
        {
            int picked = st.LootPickedInCurrentSweep;
            bool silent = st.IsSilentAutoLoot;
            st.LootQueue.Clear();
            st.IsLootingActive = false;
            st.ArrivalInteractStarted = false;
            st.LootPickedInCurrentSweep = 0;

            if (picked > 0)
            {
                st.CurrentTaskText = CompanionConfigStore.L(
                    $"Собрал трофеи ({picked} шт.)",
                    $"Looted {picked} item(s)",
                    $"已拾取 {picked} 件战利品");
                if (!silent)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        $"🎒 {st.LastKnownName} обошёл местность и собрал предметов: {picked} шт.",
                        $"🎒 {st.LastKnownName} scavenged nearby spots and collected {picked} item(s).",
                        $"🎒 {st.LastKnownName} 已巡回搜刮完毕，共拾取 {picked} 件物品。"));
                }
            }
            else
            {
                st.CurrentTaskText = "";
            }
        }

        public static void TickPassiveAutoLoot()
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient) return;
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || gc.isPlayerInCombat)
                return;

            float now = Time.unscaledTime;
            foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (st.HasAnyPhysicalTask) continue;

                string basicCmd = ag.npcData.basicCommand ?? "";
                bool isOrderedCollect = basicCmd.EndsWith("CollectResource", StringComparison.OrdinalIgnoreCase) ||
                                        basicCmd.EndsWith("CollectSpecifiedItem", StringComparison.OrdinalIgnoreCase);
                if (!isOrderedCollect && (!st.AllowAutoLootNearby ||
                    !BaseWorkManager.MayStartAutomaticWork(st, BaseWorkManager.AllowsScavenging))) continue;
                if (now - st.LastAutoLootTickTime < (isOrderedCollect ? 3.5f : 5.5f)) continue;

                st.LastAutoLootTickTime = now;
                float configuredRadius = CompanionConfigStore.LootSweepRadius;
                float sweepRadius = isOrderedCollect
                    ? configuredRadius
                    : (CompanionConfigStore.LootZonePinned
                        ? configuredRadius
                        : Mathf.Min(configuredRadius, st.EffectiveArchetype == CompanionArchetype.JollyScavenger ? 15.0f : 11.5f));
                CommandSweepNearbyLoot(ag, sweepRadius, silentIfEmpty: true);
            }
        }

        private static bool PassesAnyItemOverweightFilter(GOAPAgent agent, PhysicalLootStop stop, float now)
        {
            if (stop == null) return false;
            if (stop.Kind == PhysicalLootKind.GroundCluster)
                return Il2CppSafe.IsAlive(stop.PrimaryGroundItem?.itemData) &&
                       PassesSmartOverweightFilter(agent, stop.PrimaryGroundItem.itemData, now);

            if (Il2CppSafe.IsAlive(stop.TargetInventory) && stop.TargetInventory.itemList != null)
            {
                for (int i = 0; i < stop.TargetInventory.itemList.Count; i++)
                {
                    var it = stop.TargetInventory.itemList[i];
                    if (Il2CppSafe.IsAlive(it) && PassesSmartOverweightFilter(agent, it, now))
                        return true;
                }
                return false;
            }
            return true;
        }

        private static bool PassesSmartOverweightFilter(GOAPAgent agent, ItemData item, float now)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) || !Il2CppSafe.IsAlive(item))
                return true;
            try
            {
                var shared = agent.npcData.GetOrCreateSharedInventory();
                if (!Il2CppSafe.IsAlive(shared)) return true;

                int totalCells = Mathf.Max(1, shared.inventorySizeX * shared.inventorySizeY);
                int freeCells = shared.CountFreeCells();
                float fillRatio = 1f - ((float)freeCells / totalCells);
                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID, agent.npcData.characterData?.characterName);
                float maxRatio = st.EffectiveArchetype == CompanionArchetype.JollyScavenger ? 0.84f : 0.70f;

                if (fillRatio >= maxRatio)
                {
                    var cat = ItemCategorizer.ClassifyItem(item);
                    if ((cat & PriorityLootMask) == 0)
                    {
                        if (now - st.LastOverweightWarnTime > 28f)
                        {
                            st.LastOverweightWarnTime = now;
                            SmartChatterSystem.Speak(agent, ChatterTopic.LootOverweight, minCooldown: 6f, bubbleDuration: 6f);
                        }
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        private static bool TryTransferToCompanion(GOAPAgent agent, InventoryData source, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(item)) return false;
            try
            {
                if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData))
                {
                    var shared = agent.npcData.GetOrCreateSharedInventory();
                    if (Il2CppSafe.IsAlive(shared) && InventoryTransfers.TryMoveWhole(source, shared, item))
                        return true;

                    var npcInv = agent.npcData.characterData?.inventoryData;
                    if (Il2CppSafe.IsAlive(npcInv) && InventoryTransfers.TryMoveWhole(source, npcInv, item))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryCollectGroundItem(GOAPAgent agent, ItemData item, GameController gc)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return false;
            var destinations = new[] { agent.npcData.GetOrCreateSharedInventory(), agent.npcData.characterData?.inventoryData };
            foreach (var destination in destinations)
            {
                ItemData added = null;
                try
                {
                    if (!InventoryTransfers.TryAddWholeCopy(destination, item, out added)) continue;
                    gc.RemoveItemDataFromGround(item, true);
                    return true;
                }
                catch
                {
                    if (added != null && InventoryTransfers.Contains(destination, added)) destination.RemoveItem(added, true);
                    return false;
                }
            }
            return false;
        }

        private static Transform itemParentIfExistsSafe(this InGameController igc)
        {
            try { return igc.itemParent; }
            catch { return null; }
        }
    }
}
