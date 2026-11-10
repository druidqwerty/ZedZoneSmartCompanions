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

            if (instant || workers.Count == 0)
            {
                int moved = ExecuteInstantSortPass(workers, containers, destBoxes, extraSourceContainer, out var dominantCategory);
                var speaker = workers.Count > 0 ? workers[0] : null;
                if (Il2CppSafe.IsAliveUnity(speaker))
                {
                    if (moved > 0)
                    {
                        var stSpk = CompanionConfigStore.GetOrCreateCompanion(speaker.npcData?.npcGUID ?? "npc");
                        stSpk.SortedItemsThisSession += moved;
                        stSpk.AddExperience(Mathf.Clamp(moved * 2, 2, 40), speaker);
                        SpeakCategoryDeposit(speaker, dominantCategory);
                    }
                    SmartChatterSystem.Speak(speaker, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 6f, force: true);
                }

                int remDrop = InspectRemainingDropoffItems(containers, out var remMask);
                if (remDrop > 0)
                {
                    string remCats = ItemCategorizer.FormatCategoriesSummary(remMask, false);
                    SetBanner(CompanionConfigStore.L(
                        $"Перемещено: {moved} шт. В ящике-приёмнике осталось {remDrop} шт. (нет места или ящика для: {remCats})",
                        $"Moved: {moved}. {remDrop} items remain in Drop-off box (need chest/space for: {remCats})",
                        $"已移动：{moved} 件。中转箱剩余 {remDrop} 件（缺少对应分类箱或空位：{remCats}）"), 8f);
                }
                else
                {
                    SetBanner(CompanionConfigStore.L(
                        $"Мгновенная сортировка выполнена: перемещено предметов — {moved} шт.",
                        $"Instant sorting complete: {moved} item stacks sorted into marked chests.",
                        $"一键整理完成：已将 {moved} 组物品按类别归入已标记储物箱。"));
                }
                return;
            }

            int assignedWorkers = 0;
            var claimedSourcePointers = new HashSet<IntPtr>();
            foreach (var ag in workers)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                FlushCarryBufferSafely(st);

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
                    $"📦 Companions ({assignedWorkers}) started carrying items between your marked base chests!",
                    $"📦 队友（{assignedWorkers} 人）已开始在已标记的基地储物箱之间搬运分拣物品！"));
            }
            else
            {
                int remDrop = InspectRemainingDropoffItems(containers, out var remMask);
                if (remDrop > 0)
                {
                    string remCats = ItemCategorizer.FormatCategoriesSummary(remMask, false);
                    SetBanner(CompanionConfigStore.L(
                        $"⚠ В ящике-приёмнике осталось {remDrop} предм. — нет места или не отмечен ящик для: {remCats}",
                        $"⚠ {remDrop} items remain in Drop-off box — no free space or marked chest for: {remCats}",
                        $"⚠ 收货中转箱仍有 {remDrop} 件物品无法转移——缺少空位或未标记对应分类箱：{remCats}"), 8f);
                }
                else
                {
                    SetBanner(CompanionConfigStore.L(
                        "Все предметы в отмеченных ящиках базы уже разложены по своим категориям!",
                        "All items in your marked base containers are already sorted into their categories!",
                        "所有已标记基地储物箱中的物品均已按分类归位！"));
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
                    !c.Rule.IsDropoffSource && Il2CppSafe.IsAlive(c.Inventory))
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
            try
            {
                var teammates = SmartChatterSystem.GetActiveTeammateAgents();
                for (int i = 0; i < teammates.Count; i++)
                {
                    var ag = teammates[i];
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                        !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                        continue;

                    if (!CompanionConfigStore.CompanionStates.TryGetValue(ag.npcData.npcGUID ?? "", out var st) ||
                        st == null || !st.IsSortingActive)
                        continue;

                    if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                    {
                        FlushCarryBufferSafely(st);
                        st.IsSortingActive = false;
                        st.SortingPhase = 0;
                        continue;
                    }

                    Vector2 selfPos = ag.humanCharacter.transform.position;
                    float dist = Vector2.Distance(selfPos, st.TargetContainerPos);
                    if (dist > 1.60f)
                    {
                        if (ag.isStuck || ag.IsStuckRecovering)
                            ag.MoveTo(st.TargetContainerPos, 1.4f, true);
                        else
                            ag.MoveTo_Direct(st.TargetContainerPos, true, true, false);
                        ag.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                    }
                    else
                    {
                        ag.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                    }
                }
            }
            catch { }
        }

        public static void TickActiveSortingJobs()
        {
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

                // Automatic Base Drop-off when returning to a marked base box out of combat!
                if (!st.IsSortingActive && CompanionConfigStore.EnableBaseAutoDropoff && st.AllowAutoDropoffAtBase &&
                    !Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && now - st.LastAutoDropoffTime > 25f)
                {
                    TryAutoDropoffAtBase(ag, st, now);
                }

                if (!st.IsSortingActive) continue;

                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                {
                    FlushCarryBufferSafely(st);
                    st.IsSortingActive = false;
                    st.SortingPhase = 0;
                    continue;
                }

                float elapsed = now - st.SortingStateStartTime;
                Vector2 selfPos = ag.humanCharacter.transform.position;
                float distToBox = Vector2.Distance(selfPos, st.TargetContainerPos);

                if (st.SortingPhase == 1)
                {
                    if (distToBox > 1.85f && elapsed < 6.5f)
                        continue;

                    var containers = ScanNearbyContainers(force: true);
                    var destBoxes = GetCategorizedDestinationBoxes(containers);
                    int picked = PickupBatchIntoCarryBuffer(ag, st, destBoxes);

                    if (picked > 0 && st.CarryBuffer.Count > 0)
                    {
                        SelectNextDestinationFromBuffer(ag, st, now);
                        SmartChatterSystem.Speak(ag, ChatterTopic.SortPickUp, minCooldown: 1.0f, bubbleDuration: 5.0f, force: true);
                    }
                    else
                    {
                        if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null))
                            FinishCompanionSorting(ag, st, containers);
                    }
                }
                else if (st.SortingPhase == 2)
                {
                    if (distToBox > 1.85f && elapsed < 6.5f)
                        continue;

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
                    {
                        SelectNextDestinationFromBuffer(ag, st, now);
                    }
                    else
                    {
                        if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null))
                            FinishCompanionSorting(ag, st, containers);
                    }
                }
            }
        }
    }
}
