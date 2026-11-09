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

        public static int CommandSweepNearbyLoot(GOAPAgent specificAgent = null, float radius = 18f, bool silentIfEmpty = false)
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

                Vector2 center = gc.playerCharacter.transform.position;
                float rSqr = radius * radius;
                float now = Time.unscaledTime;

                // 1. Collect ground items
                var igc = InGameController.instance;
                var itemParent = Il2CppSafe.IsAliveUnity(igc) ? igc.itemParentIfExistsSafe() : null;
                if (Il2CppSafe.IsAliveUnity(itemParent))
                {
                    var itemsToPick = new List<BasicItem>();
                    int childCount = itemParent.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        var tr = itemParent.GetChild(i);
                        if (!Il2CppSafe.IsAliveUnity(tr) || !tr.gameObject.activeInHierarchy) continue;
                        if (((Vector2)tr.position - center).sqrMagnitude > rSqr) continue;

                        var bi = tr.GetComponent<BasicItem>();
                        if (!Il2CppSafe.IsAliveUnity(bi) || !Il2CppSafe.IsAlive(bi.itemData) || bi.itemData.deployedFlag)
                            continue;
                        itemsToPick.Add(bi);
                    }

                    int workerIdx = 0;
                    foreach (var bi in itemsToPick)
                    {
                        if (!Il2CppSafe.IsAliveUnity(bi) || !Il2CppSafe.IsAlive(bi.itemData)) continue;
                        var agent = workers[workerIdx % workers.Count];
                        workerIdx++;

                        if (!PassesSmartOverweightFilter(agent, bi.itemData, now))
                            continue;

                        if (TryGiveItemToCompanionOrPlayer(agent, bi.itemData))
                        {
                            gc.RemoveItemDataFromGround(bi.itemData, true);
                            totalCollected++;
                            var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                            st.LootedItemsThisSession++;
                            st.AddExperience(2, agent);
                        }
                    }
                }

                // 2. Sweep nearby zombie corpses
                var terrainParent = gc.terrainObjectsParent;
                if (Il2CppSafe.IsAliveUnity(terrainParent))
                {
                    int childCount = terrainParent.childCount;
                    int workerIdx = 0;
                    for (int i = 0; i < childCount; i++)
                    {
                        var tr = terrainParent.GetChild(i);
                        if (!Il2CppSafe.IsAliveUnity(tr) || !tr.gameObject.activeInHierarchy) continue;
                        if (((Vector2)tr.position - center).sqrMagnitude > rSqr) continue;

                        var corpse = tr.GetComponent<TerrainObject_Corpse>();
                        if (!Il2CppSafe.IsAliveUnity(corpse) || !Il2CppSafe.IsAlive(corpse.objectData) ||
                            !Il2CppSafe.IsAlive(corpse.objectData.inventoryData))
                            continue;

                        var inv = corpse.objectData.inventoryData;
                        if (inv.itemList == null || inv.itemList.Count == 0) continue;

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

                            var copy = item.CopyItemData(null);
                            if (Il2CppSafe.IsAlive(copy) && TryGiveItemToCompanionOrPlayer(agent, copy))
                            {
                                inv.RemoveItem(item, true);
                                totalCollected++;
                                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                                st.LootedItemsThisSession++;
                                st.AddExperience(2, agent);
                            }
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
                        "Поблизости нет бесхозных предметов или трофеев для сбора.",
                        "No loose ground items or corpse loot found nearby.",
                        "附近没有可拾取的地面物品或尸体战利品。"));
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

                // When companion's pack is >= maxRatio full, only accept PriorityLootMask (Ammo, Meds, Food/Drinks)
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
                if (!st.AllowAutoLootNearby || st.IsSortingActive) continue;
                if (now - st.LastAutoLootTickTime < 5.5f) continue;

                st.LastAutoLootTickTime = now;
                float sweepRadius = st.EffectiveArchetype == CompanionArchetype.JollyScavenger ? 13.0f : 9.5f;
                CommandSweepNearbyLoot(ag, sweepRadius, silentIfEmpty: true);
            }
        }

        private static bool TryGiveItemToCompanionOrPlayer(GOAPAgent agent, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(item)) return false;
            try
            {
                if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData))
                {
                    var shared = agent.npcData.GetOrCreateSharedInventory();
                    if (Il2CppSafe.IsAlive(shared) && shared.TryAddItem(item, true, true))
                        return true;

                    var npcInv = agent.npcData.characterData?.inventoryData;
                    if (Il2CppSafe.IsAlive(npcInv) && npcInv.TryAddItem(item, true, true))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static Transform itemParentIfExistsSafe(this InGameController igc)
        {
            try { return igc.itemParent; }
            catch { return null; }
        }
    }
}
