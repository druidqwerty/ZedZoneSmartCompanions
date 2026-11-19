using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
        private static readonly HashSet<string> _ownBackpackOnlyJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void CommandSortBase(GOAPAgent specificAgent = null, InventoryData extraSourceContainer = null, bool instant = false)
        {
            // A direct whole-squad order ends a base-work pause.
            if (BaseWorkManager.Paused && specificAgent == null && extraSourceContainer == null)
                BaseWorkManager.SetPaused(false);
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
            var allDropoffFallbackBoxes = GetAllDropoffFallbackBoxes(containers);

            if (destBoxes.Count == 0 && allDropoffFallbackBoxes.Count == 0)
            {
                foreach (var idle in SmartChatterSystem.GetActiveTeammateAgents())
                    if (Il2CppSafe.IsAliveUnity(idle) && Il2CppSafe.IsAlive(idle.npcData))
                        CompanionConfigStore.GetOrCreateCompanion(idle.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(idle.npcData.characterData))
                            .NoteBlocked(CompanionBlockReason.NoMarkedBoxes);
                SetBanner(CompanionConfigStore.L(
                    "Нет отмеченных ящиков категорий! Откройте нужные ящики на базе (или в F8) и отметьте их категории.",
                    "No marked category chests found! Open your base chests (or press F8) and select their categories first.",
                    "未找到已标记分类的储物箱！请先打开基地箱子（或按 F8）勾选物品分类。"));
                return;
            }

            var workers = new List<(GOAPAgent agent, bool onlyOwnBackpack)>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                workers.Add((specificAgent, false));
            else
            {
                var player = GameController.instance?.playerCharacter;
                bool hasPlayer = Il2CppSafe.IsAliveUnity(player);
                Vector2 playerPos = hasPlayer ? (Vector2)player.transform.position : Vector2.zero;
                float garrisonRadius = Mathf.Clamp(CompanionConfigStore.BaseScanRadius, 32f, 80f);

                var eligible = new List<(GOAPAgent agent, CompanionRuntimeState state, bool isGarrison)>();
                var channelMatched = new List<(GOAPAgent agent, bool onlyOwnBackpack)>();
                foreach (var candidate in SmartChatterSystem.GetActiveTeammateAgents())
                {
                    if (!Il2CppSafe.IsAliveUnity(candidate) || !Il2CppSafe.IsAlive(candidate.npcData)) continue;
                    if (Il2CppSafe.IsAliveUnity(candidate.humanCharacter) && candidate.humanCharacter.deadFlag) continue;
                    var candidateState = CompanionConfigStore.GetOrCreateCompanion(candidate.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(candidate.npcData.characterData));
                    bool isGarrison = candidateState.RosterMode == CompanionRosterMode.CampGarrison;
                    if (isGarrison)
                    {
                        if (!candidateState.HasHomeAnchor) continue;
                        if (hasPlayer && Vector2.Distance(playerPos, candidateState.HomeAnchorPos) > garrisonRadius)
                            continue;
                    }
                    eligible.Add((candidate, candidateState, isGarrison));
                    bool channelOk = CompanionConfigStore.ActiveCommandGroup == SquadFireteam.None ||
                                     isGarrison ||
                                     CompanionConfigStore.MatchesActiveCommandChannel(candidateState);
                    if (channelOk)
                        channelMatched.Add((candidate, !BaseWorkManager.AllowsSorting(candidateState)));
                }

                if (channelMatched.Count > 0)
                    workers.AddRange(channelMatched);
                else
                {
                    for (int i = 0; i < eligible.Count; i++)
                        workers.Add((eligible[i].agent, !BaseWorkManager.AllowsSorting(eligible[i].state)));
                }
            }

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
            foreach (var (ag, onlyOwnBackpack) in workers)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData));
                ReturnCarryBufferWithoutTeleport(ag, st);
                st.LootQueue.Clear();
                st.IsLootingActive = false;
                st.IsChoreWalkActive = false;
                st.ChoreTargetKey = "";
                st.PendingChoreArrivalAction = null;

                if (onlyOwnBackpack) _ownBackpackOnlyJobs.Add(st.NpcGuid);
                else _ownBackpackOnlyJobs.Remove(st.NpcGuid);

                var workerContainers = FilterContainersForWorker(containers, ag, st);
                var workerDestBoxes = GetCategorizedDestinationBoxes(workerContainers);
                if (TryAssignNextCarrySource(ag, st, workerContainers, workerDestBoxes, extraSourceContainer, claimedSourcePointers, onlyOwnBackpack))
                {
                    assignedWorkers++;
                    SmartChatterSystem.Speak(ag, ChatterTopic.SortStart, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
                }
                else
                {
                    _ownBackpackOnlyJobs.Remove(st.NpcGuid);
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
                string remCatsForNote = remDrop > 0 ? ItemCategorizer.FormatCategoriesSummary(remMask, false) : "";
                foreach (var (ag, _) in workers)
                    if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAlive(ag.npcData))
                        CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData))
                            .NoteBlocked(remDrop > 0 ? CompanionBlockReason.NoStorageSpace : CompanionBlockReason.NothingToDo, remCatsForNote);
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
                    var itemCat = ItemCategorizer.ClassifyItem(it);
                    // Items already matching a hybrid [Drop-off + Category] box's own categories belong there!
                    if (c.Rule.Categories != StorageCategory.None && (c.Rule.Categories & itemCat) != 0)
                        continue;
                    count++;
                    remainingCategoriesMask |= itemCat;
                }
            }
            return count;
        }

        public static List<DiscoveredContainer> GetCategorizedDestinationBoxes(List<DiscoveredContainer> containers)
        {
            var destBoxes = new List<DiscoveredContainer>();
            if (containers == null) return destBoxes;
            foreach (var c in containers)
            {
                if (c?.Rule != null && c.Rule.IsMarkedByPlayer && c.Rule.Categories != StorageCategory.None &&
                    Il2CppSafe.IsAlive(c.Inventory) &&
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

        public static List<DiscoveredContainer> GetPureDropoffDestinationBoxes(List<DiscoveredContainer> containers)
        {
            var dropBoxes = new List<DiscoveredContainer>();
            if (containers == null) return dropBoxes;
            foreach (var c in containers)
            {
                if (c?.Rule != null && c.Rule.IsMarkedByPlayer && c.Rule.IsDropoffSource &&
                    c.Rule.Categories == StorageCategory.None && Il2CppSafe.IsAlive(c.Inventory) &&
                    !PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                {
                    dropBoxes.Add(c);
                }
            }
            dropBoxes.Sort((a, b) =>
            {
                int cmp = b.Rule.Priority.CompareTo(a.Rule.Priority);
                return cmp != 0 ? cmp : a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
            });
            return dropBoxes;
        }

        public static List<DiscoveredContainer> GetAllDropoffFallbackBoxes(List<DiscoveredContainer> containers)
        {
            var dropBoxes = new List<DiscoveredContainer>();
            if (containers == null) return dropBoxes;
            foreach (var c in containers)
            {
                if (c?.Rule != null && c.Rule.IsMarkedByPlayer && c.Rule.IsDropoffSource &&
                    Il2CppSafe.IsAlive(c.Inventory) &&
                    !PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                {
                    dropBoxes.Add(c);
                }
            }
            dropBoxes.Sort((a, b) =>
            {
                bool aPure = a.Rule.Categories == StorageCategory.None;
                bool bPure = b.Rule.Categories == StorageCategory.None;
                if (aPure != bPure) return aPure ? -1 : 1;
                int cmp = b.Rule.Priority.CompareTo(a.Rule.Priority);
                return cmp != 0 ? cmp : a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
            });
            return dropBoxes;
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

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData));

                // Automatic Base Drop-off when returning near marked base chests out of close LoS combat (walks to each chest!)
                if (!st.HasAnyPhysicalTask && CompanionConfigStore.EnableBaseAutoDropoff && st.AllowAutoDropoffAtBase &&
                    BaseWorkManager.MayStartAutomaticWork(st, BaseWorkManager.AllowsAutoDropoff) &&
                    !CombatResponse.HasCloseLineOfSightThreat(ag, ag.mostThreateningCharacter, 12f) && now - st.LastAutoDropoffTime > 3.5f)
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

                var containers = FilterContainersForWorker(ScanNearbyContainers(force: true), ag, st);
                var destBoxes = GetCategorizedDestinationBoxes(containers);
                var fallbackDropoff = GetAllDropoffFallbackBoxes(containers);
                int picked = PickupBatchIntoCarryBuffer(ag, st, destBoxes, fallbackDropoff);

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
                    string failedTargetId = st.TargetContainerGuid;
                    if (!string.IsNullOrEmpty(failedTargetId))
                        PhysicalMovementHelper.MarkTargetTemporarilyUnreachable(failedTargetId, 45f);
                    var containers = FilterContainersForWorker(ScanNearbyContainers(force: true), ag, st);
                    var destBoxes = GetCategorizedDestinationBoxes(containers);
                    var fallbackDropoff = st.SourceAllowDropoffFallback ? GetAllDropoffFallbackBoxes(containers) : null;
                    bool hasRealSelfPos = Il2CppSafe.IsAliveUnity(ag?.humanCharacter);
                    Vector2 selfPos = hasRealSelfPos
                        ? (Vector2)ag.humanCharacter.transform.position
                        : Vector2.zero;

                    for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                    {
                        var entry = st.CarryBuffer[i];
                        if (entry == null || !Il2CppSafe.IsAlive(entry.Item))
                        {
                            st.CarryBuffer.RemoveAt(i);
                            continue;
                        }
                        if (string.Equals(entry.TargetContainerId, failedTargetId, StringComparison.OrdinalIgnoreCase))
                        {
                            bool reroutedToWalk = false;
                            bool placed = false;
                            InventoryData usedDestInv = null;
                            bool usedCompact = false;
                            if (hasRealSelfPos)
                            {
                                placed = TryFallbackDepositOrReroute(
                                    entry, destBoxes, requireCategoryMatch: true, st.SourceContainerInv, selfPos,
                                    out usedDestInv, out usedCompact, out reroutedToWalk);
                                if (!placed && !reroutedToWalk && fallbackDropoff != null)
                                {
                                    placed = TryFallbackDepositOrReroute(
                                        entry, fallbackDropoff, requireCategoryMatch: false, st.SourceContainerInv, selfPos,
                                        out usedDestInv, out usedCompact, out reroutedToWalk);
                                }
                            }
                            if (placed)
                            {
                                st.SortedItemsThisSession++;
                                if (usedCompact && Il2CppSafe.IsAlive(usedDestInv))
                                    InventoryTransfers.SafeCompactInventory(usedDestInv);
                                st.CarryBuffer.RemoveAt(i);
                                continue;
                            }
                            if (reroutedToWalk)
                                continue;

                            StoreCarriedItemOnCompanionOrSafeFallback(ag, entry);
                            st.CarryBuffer.RemoveAt(i);
                        }
                    }
                    if (st.CarryBuffer.Count > 0)
                        SelectNextDestinationFromBuffer(ag, st, now);
                    else
                        AdvanceOrFinishSorting(ag, st, containers, destBoxes);
                    return;
                }

                if (!ready) return;

                if (!Il2CppSafe.IsAliveUnity(ag?.humanCharacter))
                {
                    for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                    {
                        var entry = st.CarryBuffer[i];
                        if (entry != null && Il2CppSafe.IsAlive(entry.Item))
                            StoreCarriedItemOnCompanionOrSafeFallback(ag, entry);
                        st.CarryBuffer.RemoveAt(i);
                    }
                    var deadContainers = FilterContainersForWorker(ScanNearbyContainers(force: true), ag, st);
                    AdvanceOrFinishSorting(ag, st, deadContainers, GetCategorizedDestinationBoxes(deadContainers));
                    return;
                }

                Vector2 arrivalSelfPos = (Vector2)ag.humanCharacter.transform.position;
                float arrivalSurfaceDist = PhysicalMovementHelper.ComputeSurfaceDistance(
                    arrivalSelfPos, st.TargetContainerPos, st.TargetContainerAnchor);
                if (arrivalSurfaceDist > PhysicalMovementHelper.MaxReachableInteractRadius)
                {
                    PhysicalMovementHelper.BeginLeg(st, arrivalSelfPos, st.TargetContainerPos, st.TargetContainerAnchor);
                    return;
                }

                var arrivalContainers = FilterContainersForWorker(ScanNearbyContainers(force: true), ag, st);
                var arrivalDestBoxes = GetCategorizedDestinationBoxes(arrivalContainers);
                var fallbackDropoffBoxes = st.SourceAllowDropoffFallback ? GetAllDropoffFallbackBoxes(arrivalContainers) : null;
                int deposited = DepositBatchAtCurrentDestination(ag, st, arrivalDestBoxes, out var domCat, fallbackDropoffBoxes);

                if (deposited > 0)
                {
                    st.SortedItemsThisSession += deposited;
                    st.AddExperience(Mathf.Clamp(deposited * 2, 2, 40), ag);
                    SpeakCategoryDeposit(ag, domCat);
                }

                if (st.CarryBuffer.Count > 0)
                    SelectNextDestinationFromBuffer(ag, st, now);
                else
                    AdvanceOrFinishSorting(ag, st, arrivalContainers, arrivalDestBoxes);
            }
        }

        private static void AdvanceOrFinishSorting(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> containers = null,
            List<DiscoveredContainer> destBoxes = null)
        {
            containers = FilterContainersForWorker(containers ?? ScanNearbyContainers(force: true), ag, st);
            destBoxes = GetCategorizedDestinationBoxes(containers);
            bool onlyOwnBackpack = st != null && !string.IsNullOrEmpty(st.NpcGuid) && _ownBackpackOnlyJobs.Contains(st.NpcGuid);
            if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null, onlyOwnBackpack))
            {
                FinishCompanionSorting(ag, st, containers);
            }
        }
    }
}
