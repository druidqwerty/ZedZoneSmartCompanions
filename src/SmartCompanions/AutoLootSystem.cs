using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class AutoLootSystem
    {
        public static int CommandSweepNearbyLoot(GOAPAgent specificAgent = null, float radius = 18f)
        {
            int totalCollected = 0;
            try
            {
                var gc = GameController.instance;
                if (gc == null || gc.playerCharacter == null) return 0;

                var workers = new List<GOAPAgent>();
                if (specificAgent != null)
                    workers.Add(specificAgent);
                else
                    workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

                if (workers.Count == 0) return 0;

                Vector2 center = gc.playerCharacter.transform.position;
                float rSqr = radius * radius;

                // 1. Collect ground items (BasicItem under InGameController.instance.itemParent)
                var igc = InGameController.instance;
                var itemParent = igc != null ? igc.itemParentIfExistsSafe() : null;
                if (itemParent != null)
                {
                    var itemsToPick = new List<BasicItem>();
                    int childCount = itemParent.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        var tr = itemParent.GetChild(i);
                        if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                        if (((Vector2)tr.position - center).sqrMagnitude > rSqr) continue;

                        var bi = tr.GetComponent<BasicItem>();
                        if (bi == null || bi.itemData == null || bi.itemData.deployedFlag) continue;
                        itemsToPick.Add(bi);
                    }

                    int workerIdx = 0;
                    foreach (var bi in itemsToPick)
                    {
                        if (bi == null || bi.itemData == null) continue;
                        var agent = workers[workerIdx % workers.Count];
                        workerIdx++;

                        if (TryGiveItemToCompanionOrPlayer(agent, bi.itemData))
                        {
                            gc.RemoveItemDataFromGround(bi.itemData, true);
                            totalCollected++;
                            var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                            st.LootedItemsThisSession++;
                        }
                    }
                }

                // 2. Also sweep inspected/nearby zombie corpses (TerrainObject_Corpse) within radius
                var terrainParent = gc.terrainObjectsParent;
                if (terrainParent != null)
                {
                    int childCount = terrainParent.childCount;
                    int workerIdx = 0;
                    for (int i = 0; i < childCount; i++)
                    {
                        var tr = terrainParent.GetChild(i);
                        if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                        if (((Vector2)tr.position - center).sqrMagnitude > rSqr) continue;

                        var corpse = tr.GetComponent<TerrainObject_Corpse>();
                        if (corpse == null || corpse.objectData == null || corpse.objectData.inventoryData == null)
                            continue;

                        var inv = corpse.objectData.inventoryData;
                        if (inv.itemList == null || inv.itemList.Count == 0) continue;

                        var snap = new List<ItemData>();
                        for (int k = 0; k < inv.itemList.Count; k++)
                        {
                            if (inv.itemList[k] != null) snap.Add(inv.itemList[k]);
                        }

                        foreach (var item in snap)
                        {
                            var agent = workers[workerIdx % workers.Count];
                            workerIdx++;
                            var copy = item.CopyItemData(null);
                            if (copy != null && TryGiveItemToCompanionOrPlayer(agent, copy))
                            {
                                inv.RemoveItem(item, true);
                                totalCollected++;
                                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                                st.LootedItemsThisSession++;
                            }
                        }
                    }
                }

                if (totalCollected > 0 && workers.Count > 0)
                {
                    SmartChatterSystem.Speak(workers[0], ChatterTopic.LootFoundGood, minCooldown: 1f, bubbleDuration: 5.5f, force: true);
                    bool ru = CompanionConfigStore.UseRussian;
                    BaseSortingSystem.SetBanner(ru
                        ? $"Отряд собрал трофеи в радиусе {radius:F0}м: подобрано предметов — {totalCollected} шт."
                        : $"Squad scavenged area ({radius:F0}m): collected {totalCollected} items.");
                }
                else
                {
                    bool ru = CompanionConfigStore.UseRussian;
                    BaseSortingSystem.SetBanner(ru
                        ? "Поблизости нет бесхозных предметов или трофеев для сбора."
                        : "No loose ground items or corpse loot found nearby.");
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandSweepNearbyLoot failed: " + e.Message);
            }

            return totalCollected;
        }

        public static void TickPassiveAutoLoot()
        {
            var gc = GameController.instance;
            if (gc == null || gc.playerCharacter == null || gc.isPlayerInCombat) return;

            float now = Time.unscaledTime;
            foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
            {
                if (ag == null || ag.npcData == null || ag.humanCharacter == null || ag.humanCharacter.deadFlag)
                    continue;
                if (ag.mostThreateningCharacter != null) continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (!st.AllowAutoLootNearby || st.IsSortingActive) continue;
                if (now - st.LastAutoLootTickTime < 6.0f) continue;

                st.LastAutoLootTickTime = now;
                CommandSweepNearbyLoot(ag, 9f);
            }
        }

        private static bool TryGiveItemToCompanionOrPlayer(GOAPAgent agent, ItemData item)
        {
            if (item == null) return false;
            try
            {
                if (agent != null && agent.npcData != null)
                {
                    var shared = agent.npcData.GetOrCreateSharedInventory();
                    if (shared != null && shared.TryAddItem(item, true, true))
                        return true;

                    var npcInv = agent.npcData.characterData?.inventoryData;
                    if (npcInv != null && npcInv.TryAddItem(item, true, true))
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
