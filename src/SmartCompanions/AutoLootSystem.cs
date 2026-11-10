using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class AutoLootSystem
    {
        private const StorageCategory PriorityLootMask =
            StorageCategory.AmmoAndMags |
            StorageCategory.Medicine |
            StorageCategory.FreshFood |
            StorageCategory.CannedAndDrinks;

        public static bool IsProtectedPlayerBaseContainer(TerrainObject tobj, InventoryData inv = null)
        {
            try
            {
                if (!Il2CppSafe.IsAlive(inv) && Il2CppSafe.IsAliveUnity(tobj))
                    inv = BaseSortingSystem.ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: false);

                if (Il2CppSafe.IsAliveUnity(tobj))
                {
                    var furn = tobj.TryCast<TerrainObject_Furniture_Inventory>();
                    if (Il2CppSafe.IsAliveUnity(furn) && furn.isPlayerBuiltContainer)
                        return true;
                    if (Il2CppSafe.IsAlive(tobj.objectData) && tobj.objectData.isPlayerBuilt)
                        return true;
                }

                string id = BaseSortingSystem.GetContainerStableId(inv, tobj);
                if (!string.IsNullOrEmpty(id) && CompanionConfigStore.ContainerRules.TryGetValue(id, out var rule) && rule != null)
                {
                    if (rule.IsMarkedByPlayer || rule.IsDropoffSource || rule.Categories != StorageCategory.None)
                        return true;
                }

                if (Il2CppSafe.IsAliveUnity(tobj))
                {
                    Vector2 pos = tobj.transform.position;
                    foreach (var kv in CompanionConfigStore.ContainerRules)
                    {
                        var r = kv.Value;
                        if (r != null && r.IsMarkedByPlayer && r.LastKnownPos.sqrMagnitude > 0.01f &&
                            Vector2.Distance(pos, r.LastKnownPos) <= 6.5f)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static int CommandSweepNearbyLoot(GOAPAgent specificAgent = null, float radius = 20f, bool silentIfEmpty = false)
        {
            int totalCollected = 0;
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return 0;

                var workers = new List<GOAPAgent>();
                if (Il2CppSafe.IsAliveUnity(specificAgent))
                    workers.Add(specificAgent);
                else
                    workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

                if (workers.Count == 0) return 0;

                Vector2 center = Il2CppSafe.IsAliveUnity(specificAgent) && Il2CppSafe.IsAliveUnity(specificAgent.humanCharacter)
                    ? (Vector2)specificAgent.humanCharacter.transform.position
                    : (Vector2)gc.playerCharacter.transform.position;
                sbyte floor = Il2CppSafe.IsAliveUnity(specificAgent) && Il2CppSafe.IsAlive(specificAgent.npcData)
                    ? specificAgent.npcData.zLevel
                    : (gc.playerCharacter.characterData != null ? gc.playerCharacter.characterData.zLevel : (sbyte)0);
                float rSqr = radius * radius;
                float now = Time.unscaledTime;

                // 1. Collect ground items from BasicItem.ActiveObjects (and itemParent fallback)
                var itemsToPick = new List<BasicItem>();
                var seenItems = new HashSet<IntPtr>();
                var activeItems = BasicItem.ActiveObjects;
                if (activeItems != null)
                {
                    for (int i = 0; i < activeItems.Count; i++)
                    {
                        var bi = activeItems[i];
                        if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                            continue;
                        if (bi.itemData.deployedFlag || bi.itemData.zLevel != floor) continue;
                        if (((Vector2)bi.transform.position - center).sqrMagnitude > rSqr) continue;
                        if (seenItems.Add(bi.Pointer))
                            itemsToPick.Add(bi);
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
                        if (seenItems.Add(bi.Pointer))
                            itemsToPick.Add(bi);
                    }
                }

                int workerIdx = 0;
                foreach (var bi in itemsToPick)
                {
                    if (!Il2CppSafe.IsAliveUnity(bi) || !Il2CppSafe.IsAlive(bi.itemData)) continue;
                    var agent = workers[workerIdx % workers.Count];
                    workerIdx++;

                    if (!PassesSmartOverweightFilter(agent, bi.itemData, now))
                        continue;

                    if (TryCollectGroundItem(agent, bi.itemData, gc))
                    {
                        totalCollected++;
                        var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                        st.LootedItemsThisSession++;
                        st.AddExperience(2, agent);
                    }
                }

                // 2. Sweep nearby zombie corpses & unmarked wild containers across active chunks/buildings
                var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(center, radius);
                for (int i = 0; i < worldObjs.Count; i++)
                {
                    var tobj = worldObjs[i];
                    if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) continue;
                    if (tobj.objectData.zLayer != floor) continue;

                    InventoryData inv = null;
                    var corpse = tobj.TryCast<TerrainObject_Corpse>();
                    if (Il2CppSafe.IsAliveUnity(corpse))
                    {
                        inv = corpse.objectData.inventoryData;
                    }
                    else
                    {
                        var furn = tobj.TryCast<TerrainObject_Furniture_Inventory>();
                        if (Il2CppSafe.IsAliveUnity(furn))
                        {
                            if (IsProtectedPlayerBaseContainer(tobj, tobj.objectData.inventoryData))
                                continue;
                            try { furn.EnsureLootGenerated(); } catch { }
                            inv = tobj.objectData.inventoryData;
                        }
                    }

                    if (!Il2CppSafe.IsAlive(inv) || inv.isLocked || inv.itemList == null || inv.itemList.Count == 0)
                        continue;

                    var snap = new List<ItemData>();
                    for (int k = 0; k < inv.itemList.Count; k++)
                    {
                        if (Il2CppSafe.IsAlive(inv.itemList[k])) snap.Add(inv.itemList[k]);
                    }

                    foreach (var item in snap)
                    {
                        var agent = workers[workerIdx % workers.Count];
                        workerIdx++;

                        if (!PassesSmartOverweightFilter(agent, item, now))
                            continue;

                        if (TryTransferToCompanion(agent, inv, item))
                        {
                            totalCollected++;
                            var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                            st.LootedItemsThisSession++;
                            st.AddExperience(2, agent);
                        }
                    }
                }

                if (totalCollected > 0 && workers.Count > 0)
                {
                    SmartChatterSystem.Speak(workers[0], ChatterTopic.LootFoundGood, minCooldown: 1f, bubbleDuration: 5.5f, force: !silentIfEmpty);
                    if (!silentIfEmpty)
                    {
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            $"🎒 Отряд собрал трофеи в радиусе {radius:F0}м: подобрано предметов — {totalCollected} шт.",
                            $"🎒 Squad scavenged area ({radius:F0}m): collected {totalCollected} items.",
                            $"🎒 小队已搜刮周围 {radius:F0}米 区域：共拾取 {totalCollected} 件战利品。"));
                    }
                }
                else if (!silentIfEmpty)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "Поблизости нет бесхозных предметов или диких ящиков с лутом.",
                        "No loose ground items, corpse loot, or wild containers found nearby.",
                        "附近没有可拾取的地面物品、尸体或野生储物箱战利品。"));
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandSweepNearbyLoot failed: " + e.Message);
            }

            return totalCollected;
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

        public static void TickPassiveAutoLoot()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || gc.isPlayerInCombat)
                return;

            float now = Time.unscaledTime;
            foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter)) continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (st.IsSortingActive) continue;

                string basicCmd = ag.npcData.basicCommand ?? "";
                bool isOrderedCollect = basicCmd.EndsWith("CollectResource", StringComparison.OrdinalIgnoreCase) ||
                                        basicCmd.EndsWith("CollectSpecifiedItem", StringComparison.OrdinalIgnoreCase);
                if (!st.AllowAutoLootNearby && !isOrderedCollect) continue;
                if (now - st.LastAutoLootTickTime < (isOrderedCollect ? 3.5f : 5.5f)) continue;

                st.LastAutoLootTickTime = now;
                float sweepRadius = isOrderedCollect ? 22.0f : (st.EffectiveArchetype == CompanionArchetype.JollyScavenger ? 13.0f : 9.5f);
                CommandSweepNearbyLoot(ag, sweepRadius, silentIfEmpty: true);
            }
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
