using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
        public static void CommandSortBase(GOAPAgent specificAgent = null, InventoryData extraSourceContainer = null, bool instant = false)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient)
            {
                MultiplayerSyncSystem.SendPacketToHost("SORT|1");
                SetBanner(CompanionConfigStore.L(
                    "📦 Приказ отправлен напарникам: пойти и разнести предметы по ящикам базы!",
                    "📦 Ordered companions to walk and carry items between marked base chests!",
                    "📦 已命令队友前往搬运整理基地储物箱！"));
                return;
            }

            var containers = ScanNearbyContainers(force: true);
            var destBoxes = GetCategorizedDestinationBoxes(containers);

            if (destBoxes.Count == 0)
            {
                SetBanner(CompanionConfigStore.L(
                    "Нет отмеченных ящиков категорий! Откройте нужные ящики на базе (или в F8) и отметьте их категории.",
                    "No marked category chests found! Open your base chests (or press F8) and select their categories first.",
                    "未找到已标记分类的储物箱！请先打开基地箱子（或按 F8）勾选物品分类。"));
                return;
            }

            var workers = new List<GOAPAgent>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                workers.Add(specificAgent);
            else
                workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

            if (workers.Count == 0)
            {
                SetBanner(CompanionConfigStore.L(
                    "Рядом нет активных напарников, которые могли бы отнести и разложить вещи!",
                    "No active companions nearby to physically carry and sort items!",
                    "附近没有已招募的队友可前往搬运整理物品！"));
                return;
            }

            int assignedWorkers = 0;
            var claimedSourcePointers = new HashSet<IntPtr>();
            foreach (var ag in workers)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                ReturnCarryBufferWithoutTeleport(ag, st);
                st.LootQueue.Clear();
                st.IsLootingActive = false;
                st.IsChoreWalkActive = false;

                if (TryAssignNextCarrySource(ag, st, containers, destBoxes, extraSourceContainer, claimedSourcePointers))
                {
                    assignedWorkers++;
                    SmartChatterSystem.Speak(ag, ChatterTopic.SortStart, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
                }
            }

            if (assignedWorkers > 0)
            {
                SetBanner(CompanionConfigStore.L(
                    $"📦 Напарники ({assignedWorkers} чел.) пошли переносить предметы по отмеченным ящикам базы!",
                    $"📦 Companions ({assignedWorkers}) started walking and carrying items between your marked base chests!",
                    $"📦 队友（{assignedWorkers} 人）已开始前往并在已标记的基地储物箱之间搬运物品！"));
            }
            else
            {
                int remDrop = InspectRemainingDropoffItems(containers, out var remMask);
                if (remDrop > 0)
                {
                    string remCats = ItemCategorizer.FormatCategoriesSummary(remMask, false);
                    SetBanner(CompanionConfigStore.L(
                        $"⚠ В ящике-приёмнике осталось {remDrop} предм. — нет места, пути или ящика для: {remCats}",
                        $"⚠ {remDrop} items remain in Drop-off box — no space, path, or marked chest for: {remCats}",
                        $"⚠ 收货中转箱仍有 {remDrop} 件物品无法搬运——缺少空位、路径或分类箱：{remCats}"), 8f);
                }
                else
                {
                    SetBanner(CompanionConfigStore.L(
                        "Все доступные предметы в отмеченных ящиках базы уже разложены по своим категориям!",
                        "All reachable items in your marked base containers are already sorted into their categories!",
                        "所有可达的已标记基地储物箱物品均已按分类归位！"));
                }
            }
        }

        public static int InspectRemainingDropoffItems(List<DiscoveredContainer> containers, out StorageCategory remainingCategoriesMask)
        {
            remainingCategoriesMask = StorageCategory.None;
            int count = 0;
            if (containers == null) return 0;

            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c?.Rule == null || !c.Rule.IsMarkedByPlayer || !c.Rule.IsDropoffSource) continue;
                if (!Il2CppSafe.IsAlive(c.Inventory) || c.Inventory.itemList == null) continue;

                for (int j = 0; j < c.Inventory.itemList.Count; j++)
                {
                    var it = c.Inventory.itemList[j];
                    if (!Il2CppSafe.IsAlive(it)) continue;
                    count++;
                    remainingCategoriesMask |= ItemCategorizer.ClassifyItem(it);
                }
            }
            return count;
        }

        private static List<DiscoveredContainer> GetCategorizedDestinationBoxes(List<DiscoveredContainer> containers)
        {
            var destBoxes = new List<DiscoveredContainer>();
            foreach (var c in containers)
            {
                if (c?.Rule != null && c.Rule.IsMarkedByPlayer && c.Rule.Categories != StorageCategory.None &&
                    !c.Rule.IsDropoffSource && Il2CppSafe.IsAlive(c.Inventory) &&
                    !PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                {
                    destBoxes.Add(c);
                }
            }
            destBoxes.Sort((a, b) =>
            {
                int cmp = b.Rule.Priority.CompareTo(a.Rule.Priority);
                return cmp != 0 ? cmp : a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
            });
            return destBoxes;
        }

        public static void TickActiveSortingMovementEveryFrame()
        {
            // Movement and arrival interactions are now driven authoritatively via
            // GOAPAgent.UpdateGoal -> PhysicalMovementHelper.TickPhysicalFrameForAgent.
        }

        public static void TickActiveSortingJobs()
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient) return;
            PruneStaleCacheEntries();
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            float now = Time.unscaledTime;
            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);

                // Automatic Base Drop-off when returning near marked base chests out of combat (walks to each chest!)
                if (!st.HasAnyPhysicalTask && CompanionConfigStore.EnableBaseAutoDropoff && st.AllowAutoDropoffAtBase &&
                    !Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && now - st.LastAutoDropoffTime > 20f)
                {
                    TryAutoDropoffAtBase(ag, st, now);
                }
            }
        }

        public static void StepSortingJobForAgent(GOAPAgent ag, CompanionRuntimeState st)
        {
            float now = Time.unscaledTime;

            if (st.SortingPhase == 1)
            {
                if (!Il2CppSafe.IsAlive(st.SourceContainerInv))
                {
                    AdvanceOrFinishSorting(ag, st);
                    return;
                }

                if (Il2CppSafe.IsAliveUnity(st.SourceContainerAnchor))
                    st.TargetContainerPos = st.SourceContainerAnchor.position;

                bool ready;
                bool unreachable = false;

                if (st.SourceIsOwnBackpack)
                {
                    ready = true;
                }
                else
                {
                    int previewItemId = st.SourceContainerInv.itemList != null && st.SourceContainerInv.itemList.Count > 0
                        ? (st.SourceContainerInv.itemList[0]?.itemId ?? 0) : 0;
                    PhysicalMovementHelper.StepWalkAndInteract(
                        ag, st, st.TargetContainerPos, st.SourceContainerAnchor, previewItemId, 0.45f,
                        out ready, out unreachable);

                    if (st.ArrivalInteractStarted && !ready)
                    {
                        st.CurrentTaskText = CompanionConfigStore.L(
                            $"Берёт вещи из «{st.SourceContainerName}»...",
                            $"Taking items from '{st.SourceContainerName}'...",
                            $"正从「{st.SourceContainerName}」取物...");
                        return;
                    }
                }

                if (unreachable)
                {
                    PhysicalMovementHelper.MarkTargetTemporarilyUnreachable(st.SourceContainerGuid, 45f);
                    AdvanceOrFinishSorting(ag, st);
                    return;
                }

                if (!ready) return;

                var containers = ScanNearbyContainers(force: true);
                var destBoxes = GetCategorizedDestinationBoxes(containers);
                int picked = PickupBatchIntoCarryBuffer(ag, st, destBoxes);

                if (picked > 0 && st.CarryBuffer.Count > 0)
                {
                    SelectNextDestinationFromBuffer(ag, st, now);
                    if (!st.SourceIsOwnBackpack)
                        SmartChatterSystem.Speak(ag, ChatterTopic.SortPickUp, minCooldown: 2.0f, bubbleDuration: 5.0f, force: false);
                }
                else
                {
                    AdvanceOrFinishSorting(ag, st, containers, destBoxes);
                }
            }
            else if (st.SortingPhase == 2)
            {
                if (Il2CppSafe.IsAliveUnity(st.TargetContainerAnchor))
                    st.TargetContainerPos = st.TargetContainerAnchor.position;

                int previewCarriedId = st.CarryBuffer.Count > 0 && Il2CppSafe.IsAlive(st.CarryBuffer[0]?.Item)
                    ? st.CarryBuffer[0].Item.itemId : 0;

                PhysicalMovementHelper.StepWalkAndInteract(
                    ag, st, st.TargetContainerPos, st.TargetContainerAnchor, previewCarriedId, 0.40f,
                    out bool ready, out bool unreachable);

                if (st.ArrivalInteractStarted && !ready)
                {
                    st.CurrentTaskText = CompanionConfigStore.L(
                        $"Укладывает вещи в «{st.TargetContainerName}»...",
                        $"Placing items in '{st.TargetContainerName}'...",
                        $"正将物品放入「{st.TargetContainerName}」...");
                    return;
                }

                if (unreachable)
                {
                    PhysicalMovementHelper.MarkTargetTemporarilyUnreachable(st.TargetContainerGuid, 45f);
                    for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                    {
                        var entry = st.CarryBuffer[i];
                        if (entry != null && string.Equals(entry.TargetContainerId, st.TargetContainerGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            StoreCarriedItemOnCompanionOrSafeFallback(ag, entry);
                            st.CarryBuffer.RemoveAt(i);
                        }
                    }
                    if (st.CarryBuffer.Count > 0)
                        SelectNextDestinationFromBuffer(ag, st, now);
                    else
                        AdvanceOrFinishSorting(ag, st);
                    return;
                }

                if (!ready) return;

                var containers = ScanNearbyContainers(force: true);
                var destBoxes = GetCategorizedDestinationBoxes(containers);
                int deposited = DepositBatchAtCurrentDestination(ag, st, destBoxes, out var domCat);

                if (deposited > 0)
                {
                    st.SortedItemsThisSession += deposited;
                    st.AddExperience(Mathf.Clamp(deposited * 2, 2, 40), ag);
                    SpeakCategoryDeposit(ag, domCat);
                }

                if (st.CarryBuffer.Count > 0)
                    SelectNextDestinationFromBuffer(ag, st, now);
                else
                    AdvanceOrFinishSorting(ag, st, containers, destBoxes);
            }
        }

        private static void AdvanceOrFinishSorting(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> containers = null,
            List<DiscoveredContainer> destBoxes = null)
        {
            containers ??= ScanNearbyContainers(force: true);
            destBoxes ??= GetCategorizedDestinationBoxes(containers);
            if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null))
            {
                FinishCompanionSorting(ag, st, containers);
            }
        }
    }
}
