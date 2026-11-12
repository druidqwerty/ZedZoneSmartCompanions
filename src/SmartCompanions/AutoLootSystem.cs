using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class AutoLootSystem
    {
        private const StorageCategory PriorityLootMask =
            StorageCategory.AmmoAndMags |
            StorageCategory.Medicine |
            StorageCategory.FreshFood |
            StorageCategory.CannedAndDrinks;

        public static bool IsPositionNearPlayerBase(Vector2 pos, float sanctuaryRadius = 18f)
        {
            try
            {
                float rSqr = sanctuaryRadius * sanctuaryRadius;
                foreach (var kv in CompanionConfigStore.ContainerRules)
                {
                    var r = kv.Value;
                    if (r != null && r.IsMarkedByPlayer && r.LastKnownPos.sqrMagnitude > 0.01f &&
                        (pos - r.LastKnownPos).sqrMagnitude <= rSqr)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

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

                if (Il2CppSafe.IsAliveUnity(tobj) && IsPositionNearPlayerBase(tobj.transform.position, 18f))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        public static int CommandSweepNearbyLoot(
            GOAPAgent specificAgent = null,
            float radius = 20f,
            bool silentIfEmpty = false,
            Vector2? explicitSearchCenter = null)
        {
            if (BaseWorkManager.Paused && !silentIfEmpty)
                BaseWorkManager.SetPaused(false);
            if (MultiplayerSyncSystem.IsMultiplayerClient)
            {
                MultiplayerSyncSystem.SendPacketToHost("LOOT|1");
                if (!silentIfEmpty)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        "🎒 Приказ отправлен напарникам: пойти и обыскать окрестности!",
                        "🎒 Ordered companions to walk and scavenge nearby loot!",
                        "🎒 已命令队友前往搜刮附近战利品！"));
                }
                return 0;
            }

            int queuedStops = 0;
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return 0;

                var workers = new List<GOAPAgent>();
                if (Il2CppSafe.IsAliveUnity(specificAgent))
                {
                    workers.Add(specificAgent);
                }
                else
                {
                    foreach (var mate in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        if (!Il2CppSafe.IsAliveUnity(mate) || !Il2CppSafe.IsAlive(mate.npcData) ||
                            !Il2CppSafe.IsAliveUnity(mate.humanCharacter) || mate.humanCharacter.deadFlag)
                            continue;
                        if (Il2CppSafe.IsAliveUnity(mate.mostThreateningCharacter) && !mate.mostThreateningCharacter.deadFlag)
                            continue;
                        var mst = CompanionConfigStore.GetOrCreateCompanion(mate.npcData.npcGUID);
                        if (mst.IsSortingActive) continue;
                        workers.Add(mate);
                    }
                }

                if (workers.Count == 0) return 0;

                Vector2 center = explicitSearchCenter ?? (Il2CppSafe.IsAliveUnity(specificAgent) && Il2CppSafe.IsAliveUnity(specificAgent.humanCharacter)
                    ? (Vector2)specificAgent.humanCharacter.transform.position
                    : (Vector2)gc.playerCharacter.transform.position);
                sbyte floor = Il2CppSafe.IsAliveUnity(specificAgent) && Il2CppSafe.IsAlive(specificAgent.npcData)
                    ? specificAgent.npcData.zLevel
                    : (gc.playerCharacter.characterData != null ? gc.playerCharacter.characterData.zLevel : (sbyte)0);

                var alreadyClaimedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in CompanionConfigStore.CompanionStates)
                {
                    var cst = kv.Value;
                    if (cst == null || !cst.IsLootingActive) continue;
                    for (int i = 0; i < cst.LootQueue.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(cst.LootQueue[i]?.TargetKey))
                            alreadyClaimedKeys.Add(cst.LootQueue[i].TargetKey);
                    }
                }

                var stops = DiscoverPhysicalLootStops(center, radius, floor, alreadyClaimedKeys, isPassiveAutoLoot: silentIfEmpty);
                if (stops.Count == 0)
                {
                    if (!silentIfEmpty)
                    {
                        BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                            "Поблизости нет доступных предметов на земле, трупов или диких ящиков с лутом.",
                            "No reachable ground items, corpse loot, or wild containers found nearby.",
                            "附近没有可前往拾取的地面物品、尸体或野生储物箱。"));
                    }
                    return 0;
                }

                // Assign stops nearest-first across workers so squadmates physically walk to separate spots
                int maxStopsTotal = Mathf.Min(stops.Count, workers.Count * 6);
                var workerLastPos = new Vector2[workers.Count];
                for (int w = 0; w < workers.Count; w++)
                    workerLastPos[w] = workers[w].humanCharacter.transform.position;

                var remaining = new List<PhysicalLootStop>(stops);
                int wTurn = 0;
                while (remaining.Count > 0 && queuedStops < maxStopsTotal)
                {
                    int wIdx = wTurn % workers.Count;
                    wTurn++;
                    var worker = workers[wIdx];
                    var st = CompanionConfigStore.GetOrCreateCompanion(worker.npcData.npcGUID, worker.npcData.characterData?.characterName);
                    if (st.LootQueue.Count >= 6) continue;

                    int bestIdx = 0;
                    float bestDist = Vector2.Distance(workerLastPos[wIdx], remaining[0].WorldPos);
                    for (int r = 1; r < remaining.Count; r++)
                    {
                        float d = Vector2.Distance(workerLastPos[wIdx], remaining[r].WorldPos);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestIdx = r;
                        }
                    }

                    var chosen = remaining[bestIdx];
                    remaining.RemoveAt(bestIdx);

                    if (!PassesAnyItemOverweightFilter(worker, chosen, Time.unscaledTime))
                        continue;

                    st.LootQueue.Add(chosen);
                    workerLastPos[wIdx] = chosen.WorldPos;
                    queuedStops++;
                }

                int startedWorkers = 0;
                for (int w = 0; w < workers.Count; w++)
                {
                    var ag = workers[w];
                    var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                    if (st.LootQueue.Count > 0 && !st.IsLootingActive)
                    {
                        st.IsLootingActive = true;
                        st.IsSilentAutoLoot = silentIfEmpty;
                        st.LootPickedInCurrentSweep = 0;
                        BeginCurrentLootLeg(ag, st);
                        startedWorkers++;
                    }
                }

                if (queuedStops > 0 && !silentIfEmpty)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        $"🎒 Напарники ({Mathf.Max(1, startedWorkers)} чел.) пошли обыскивать точки лута поблизости ({queuedStops} шт.)!",
                        $"🎒 Companions ({Mathf.Max(1, startedWorkers)}) are walking out to scavenge {queuedStops} nearby loot spot(s)!",
                        $"🎒 队友（{Mathf.Max(1, startedWorkers)} 人）已出发前往搜刮附近 {queuedStops} 处战利品点！"));
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandSweepNearbyLoot failed: " + e.Message);
            }

            return queuedStops;
        }
    }
}
