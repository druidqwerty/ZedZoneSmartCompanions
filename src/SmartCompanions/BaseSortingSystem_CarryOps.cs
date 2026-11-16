using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
        private static bool TryAssignNextCarrySource(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> allContainers,
            List<DiscoveredContainer> destBoxes,
            InventoryData extraSource,
            HashSet<IntPtr> claimedSources,
            bool onlyOwnBackpack = false)
        {
            var fallbackDropoffBoxes = GetAllDropoffFallbackBoxes(allContainers);
            if ((destBoxes == null || destBoxes.Count == 0) && fallbackDropoffBoxes.Count == 0) return false;
            Transform selfAnchor = Il2CppSafe.IsAliveUnity(ag?.humanCharacter) ? ag.humanCharacter.transform : null;
            Vector2 selfPos = Il2CppSafe.IsAliveUnity(selfAnchor)
                ? (Vector2)selfAnchor.position
                : Vector2.zero;
            string npcGuid = ag?.npcData?.npcGUID ?? st?.NpcGuid ?? "";

            var candidates = new List<(InventoryData inv, Vector2 pos, Transform anchor, bool ownBackpack, bool allowDropoffFallback, string id, string name, StorageCategory stayMask, float dist)>();

            // 1. Companion's own sharedInventoryData & personal inventoryData (0m pickup, physical walk to deposit!)
            var shared = ag?.npcData?.sharedInventoryData;
            if (Il2CppSafe.IsAlive(shared) && shared.itemList != null && shared.itemList.Count > 0)
            {
                candidates.Add((shared, selfPos, selfAnchor, true, true, "shared_" + npcGuid,
                    CompanionConfigStore.L("Рюкзак напарника", "Companion Backpack", "队友背包"), StorageCategory.None, 0f));
            }

            var personalInv = ag?.npcData?.characterData?.inventoryData;
            if (Il2CppSafe.IsAlive(personalInv) && personalInv.itemList != null && personalInv.itemList.Count > 0 &&
                (!Il2CppSafe.IsAlive(shared) || personalInv.Pointer != shared.Pointer))
            {
                candidates.Add((personalInv, selfPos, selfAnchor, true, true, "inv_" + npcGuid,
                    CompanionConfigStore.L("Инвентарь напарника", "Companion Inventory", "队友物品栏"), StorageCategory.None, 0f));
            }

            if (!onlyOwnBackpack)
            {
                // 2. Explicit extraSource (when player clicks "Разобрать этот ящик" or orders Vehicle Trunk Unload)
                if (Il2CppSafe.IsAlive(extraSource) && extraSource.itemList != null && extraSource.itemList.Count > 0)
                {
                    var anchor = ResolveAnchorTransformForInventory(extraSource);
                    var tobj = FindTerrainObjectByInventory(extraSource);
                    Vector2 pos = Il2CppSafe.IsAliveUnity(anchor)
                        ? (Vector2)anchor.position
                        : (extraSource.worldposition.sqrMagnitude > 0.001f ? extraSource.worldposition : selfPos);
                    string id = GetContainerStableId(extraSource, tobj);
                    string label = KnownInvToExternalLabel.TryGetValue(extraSource.Pointer, out var extLbl) && !string.IsNullOrEmpty(extLbl)
                        ? extLbl
                        : (!string.IsNullOrEmpty(extraSource.inventoryTitleName) ? extraSource.inventoryTitleName : "Container");
                    if (!PhysicalMovementHelper.IsTargetTemporarilyUnreachable(id))
                        candidates.Add((extraSource, pos, anchor, false, true, id, label, StorageCategory.None, Vector2.Distance(selfPos, pos)));
                }

                // 3. Player-marked Drop-off boxes first, then player-marked categorized boxes with misplaced items
                if (allContainers != null)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int i = 0; i < allContainers.Count; i++)
                        {
                            var c = allContainers[i];
                            if (c == null || !Il2CppSafe.IsAlive(c.Inventory) || c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                                continue;
                            if (c.Rule == null || !c.Rule.IsMarkedByPlayer) continue;
                            if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId)) continue;
                            Transform cAnchor = Il2CppSafe.IsAliveUnity(c.TerrainObj) ? c.TerrainObj.transform : ResolveAnchorTransformForInventory(c.Inventory);

                            if (pass == 0 && c.Rule.IsDropoffSource)
                            {
                                // If player marked BOTH [Drop-off] and specific Categories on this box, keep matching items here and sort non-matching items out!
                                candidates.Add((c.Inventory, c.WorldPos, cAnchor, false, false, c.ContainerId, c.DisplayName, c.Rule.Categories, Vector2.Distance(selfPos, c.WorldPos)));
                            }
                            else if (pass == 1 && !c.Rule.IsDropoffSource && c.Rule.Categories != StorageCategory.None && c.Rule.Categories != StorageCategory.All)
                            {
                                candidates.Add((c.Inventory, c.WorldPos, cAnchor, false, false, c.ContainerId, c.DisplayName, c.Rule.Categories, Vector2.Distance(selfPos, c.WorldPos)));
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (claimedSources != null && claimedSources.Contains(cand.inv.Pointer))
                    continue;
                HashSet<IntPtr> protectedPtrs = cand.ownBackpack
                    ? CompanionProfileSystem.BuildProtectedBackpackPointers(ag, st, cand.inv)
                    : null;
                var fallbackList = cand.allowDropoffFallback ? fallbackDropoffBoxes : null;
                if (!HasMovableItemInSource(cand.inv, cand.stayMask, destBoxes, protectedPtrs, fallbackList))
                    continue;

                if (claimedSources != null)
                    claimedSources.Add(cand.inv.Pointer);

                st.IsSortingActive = true;
                st.SortingPhase = 1;
                st.SourceContainerGuid = cand.id;
                st.SourceContainerName = cand.name;
                st.SourceContainerInv = cand.inv;
                st.SourceContainerAnchor = cand.anchor;
                st.SourceIsOwnBackpack = cand.ownBackpack;
                st.SourceAllowDropoffFallback = cand.allowDropoffFallback;
                st.SourceAllowedStayMask = cand.stayMask;
                st.TargetContainerGuid = cand.id;
                st.TargetContainerName = cand.name;
                st.TargetContainerAnchor = cand.anchor;
                st.TargetContainerPos = cand.pos;
                PhysicalMovementHelper.BeginLeg(st, selfPos, cand.pos, cand.anchor);
                st.CurrentTaskText = cand.ownBackpack
                    ? CompanionConfigStore.L("Достаёт вещи из рюкзака...", "Taking items from backpack...", "正从背包取出物品...")
                    : CompanionConfigStore.L(
                        $"Идёт к «{cand.name}» за предметами...",
                        $"Walking to '{cand.name}' to pick up items...",
                        $"正在前往「{cand.name}」取物...");
                return true;
            }

            return false;
        }

        private static bool HasMovableItemInSource(
            InventoryData srcInv,
            StorageCategory stayMask,
            List<DiscoveredContainer> destBoxes,
            HashSet<IntPtr> protectedPtrs = null,
            List<DiscoveredContainer> fallbackDropoffBoxes = null)
        {
            if (!Il2CppSafe.IsAlive(srcInv) || srcInv.itemList == null) return false;
            for (int i = 0; i < srcInv.itemList.Count; i++)
            {
                var item = srcInv.itemList[i];
                if (!Il2CppSafe.IsAlive(item)) continue;
                if (protectedPtrs != null && protectedPtrs.Contains(item.Pointer)) continue;
                var cat = ItemCategorizer.ClassifyItem(item);
                if (stayMask != StorageCategory.None && (stayMask & cat) != 0)
                    continue;
                if (destBoxes != null)
                {
                    for (int d = 0; d < destBoxes.Count; d++)
                    {
                        var dest = destBoxes[d];
                        if (dest?.Inventory == null || dest.Inventory.Pointer == srcInv.Pointer) continue;
                        if ((dest.Rule.Categories & cat) == 0) continue;
                        if (InventoryTransfers.CanAcceptWholeItem(dest.Inventory, item))
                            return true;
                    }
                }
                if (fallbackDropoffBoxes != null)
                {
                    for (int d = 0; d < fallbackDropoffBoxes.Count; d++)
                    {
                        var drop = fallbackDropoffBoxes[d];
                        if (drop?.Inventory == null || drop.Inventory.Pointer == srcInv.Pointer) continue;
                        if (InventoryTransfers.CanAcceptWholeItem(drop.Inventory, item))
                            return true;
                    }
                }
            }
            return false;
        }

        private static DiscoveredContainer TryPickCarryDestination(
            ItemData item,
            StorageCategory cat,
            InventoryData srcInv,
            List<DiscoveredContainer> candidateBoxes,
            bool requireCategoryMatch,
            Dictionary<IntPtr, int> remainingFreeCells,
            Dictionary<(IntPtr, int), float> claimedMergeUnits,
            ref int neededCells)
        {
            if (candidateBoxes == null) return null;
            for (int d = 0; d < candidateBoxes.Count; d++)
            {
                var dest = candidateBoxes[d];
                if (dest?.Inventory == null || dest.Inventory.Pointer == srcInv.Pointer) continue;
                if (requireCategoryMatch && (dest.Rule == null || (dest.Rule.Categories & cat) == 0)) continue;

                if (InventoryTransfers.CanMergeCompletelyIntoExistingStack(dest.Inventory, item))
                {
                    claimedMergeUnits.TryGetValue((dest.Inventory.Pointer, item.itemId), out float usedUnits);
                    if (InventoryTransfers.GetTotalMergeSpareCapacity(dest.Inventory, item, out _) - usedUnits >= item.itemNumberFloat - 0.001f)
                    {
                        neededCells = 0;
                        return dest;
                    }
                }

                remainingFreeCells.TryGetValue(dest.Inventory.Pointer, out int availCells);
                if (availCells < neededCells) continue;

                if (InventoryTransfers.CanAcceptWholeItem(dest.Inventory, item))
                    return dest;
            }
            return null;
        }

        private static int PickupBatchIntoCarryBuffer(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> destBoxes,
            List<DiscoveredContainer> fallbackDropoffBoxes = null)
        {
            var srcInv = st.SourceContainerInv;
            bool allowDropFallback = st.SourceAllowDropoffFallback && fallbackDropoffBoxes != null && fallbackDropoffBoxes.Count > 0;
            if (!Il2CppSafe.IsAlive(srcInv) || srcInv.itemList == null || srcInv.itemList.Count == 0 ||
                ((destBoxes == null || destBoxes.Count == 0) && !allowDropFallback))
                return 0;

            int picked = 0;
            try
            {
                // Track remaining free grid cells and claimed stack-merge units per destination chest
                // across this batch so we never over-pick into chests with limited slots or merge capacity!
                var remainingFreeCells = new Dictionary<IntPtr, int>();
                var claimedMergeUnits = new Dictionary<(IntPtr, int), float>();
                if (destBoxes != null)
                {
                    for (int d = 0; d < destBoxes.Count; d++)
                    {
                        var dest = destBoxes[d];
                        if (dest?.Inventory != null && !remainingFreeCells.ContainsKey(dest.Inventory.Pointer))
                        {
                            try { remainingFreeCells[dest.Inventory.Pointer] = dest.Inventory.CountFreeCells(); }
                            catch { remainingFreeCells[dest.Inventory.Pointer] = 36; }
                        }
                    }
                }
                if (allowDropFallback)
                {
                    for (int d = 0; d < fallbackDropoffBoxes.Count; d++)
                    {
                        var drop = fallbackDropoffBoxes[d];
                        if (drop?.Inventory != null && !remainingFreeCells.ContainsKey(drop.Inventory.Pointer))
                        {
                            try { remainingFreeCells[drop.Inventory.Pointer] = drop.Inventory.CountFreeCells(); }
                            catch { remainingFreeCells[drop.Inventory.Pointer] = 36; }
                        }
                    }
                }

                var snapshot = new List<ItemData>();
                for (int i = 0; i < srcInv.itemList.Count; i++)
                {
                    var it = srcInv.itemList[i];
                    if (Il2CppSafe.IsAlive(it)) snapshot.Add(it);
                }

                HashSet<IntPtr> protectedPtrs = st.SourceIsOwnBackpack
                    ? CompanionProfileSystem.BuildProtectedBackpackPointers(ag, st, srcInv)
                    : null;

                foreach (var item in snapshot)
                {
                    if (st.CarryBuffer.Count >= MaxCarryBatchPerTrip)
                        break;
                    if (!Il2CppSafe.IsAlive(item)) continue;
                    if (protectedPtrs != null && protectedPtrs.Contains(item.Pointer)) continue;

                    var cat = ItemCategorizer.ClassifyItem(item);
                    if (st.SourceAllowedStayMask != StorageCategory.None && (st.SourceAllowedStayMask & cat) != 0)
                        continue;

                    int neededCells = InventoryTransfers.EstimateItemCells(item);
                    DiscoveredContainer chosenDest = TryPickCarryDestination(
                        item, cat, srcInv, destBoxes, requireCategoryMatch: true,
                        remainingFreeCells, claimedMergeUnits, ref neededCells);

                    if (chosenDest == null && allowDropFallback)
                    {
                        chosenDest = TryPickCarryDestination(
                            item, cat, srcInv, fallbackDropoffBoxes, requireCategoryMatch: false,
                            remainingFreeCells, claimedMergeUnits, ref neededCells);
                    }

                    if (chosenDest == null) continue;

                    if (InventoryTransfers.TryTakeIntoCarryBuffer(srcInv, item, out var carriedCopy))
                    {
                        if (neededCells == 0)
                        {
                            var mergeKey = (chosenDest.Inventory.Pointer, item.itemId);
                            claimedMergeUnits.TryGetValue(mergeKey, out float curClaimed);
                            claimedMergeUnits[mergeKey] = curClaimed + carriedCopy.itemNumberFloat;
                        }
                        else if (remainingFreeCells.TryGetValue(chosenDest.Inventory.Pointer, out int curAvail))
                        {
                            remainingFreeCells[chosenDest.Inventory.Pointer] = Mathf.Max(0, curAvail - neededCells);
                        }

                        st.CarryBuffer.Add(new CarryItemEntry
                        {
                            Item = carriedCopy,
                            FallbackSourceInv = srcInv,
                            TargetContainerId = chosenDest.ContainerId,
                            TargetInv = chosenDest.Inventory,
                            TargetAnchor = Il2CppSafe.IsAliveUnity(chosenDest.TerrainObj)
                                ? chosenDest.TerrainObj.transform
                                : ResolveAnchorTransformForInventory(chosenDest.Inventory),
                            TargetPos = chosenDest.WorldPos,
                            TargetDisplayName = chosenDest.DisplayName,
                            Category = cat
                        });
                        picked++;
                    }
                }

                if (picked > 0)
                {
                    RefreshOpenLootPanelIfNeeded();
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("PickupBatchIntoCarryBuffer error: " + e.Message);
            }

            return picked;
        }

        private static void SelectNextDestinationFromBuffer(GOAPAgent ag, CompanionRuntimeState st, float now)
        {
            if (st.CarryBuffer.Count == 0) return;

            Vector2 selfPos = Il2CppSafe.IsAliveUnity(ag?.humanCharacter)
                ? (Vector2)ag.humanCharacter.transform.position
                : Vector2.zero;

            CarryItemEntry bestEntry = st.CarryBuffer[0];
            float bestDist = Vector2.Distance(selfPos, bestEntry.TargetPos);

            for (int i = 1; i < st.CarryBuffer.Count; i++)
            {
                var entry = st.CarryBuffer[i];
                float d = Vector2.Distance(selfPos, entry.TargetPos);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestEntry = entry;
                }
            }

            int countForTarget = 0;
            for (int i = 0; i < st.CarryBuffer.Count; i++)
            {
                if (string.Equals(st.CarryBuffer[i].TargetContainerId, bestEntry.TargetContainerId, StringComparison.OrdinalIgnoreCase))
                    countForTarget++;
            }

            st.SortingPhase = 2;
            st.TargetContainerGuid = bestEntry.TargetContainerId;
            st.TargetContainerName = bestEntry.TargetDisplayName;
            st.TargetContainerAnchor = Il2CppSafe.IsAliveUnity(bestEntry.TargetAnchor)
                ? bestEntry.TargetAnchor
                : ResolveAnchorTransformForInventory(bestEntry.TargetInv);
            st.TargetContainerPos = bestEntry.TargetPos;
            PhysicalMovementHelper.BeginLeg(st, selfPos, bestEntry.TargetPos, st.TargetContainerAnchor);
            st.PendingTransferCount = st.CarryBuffer.Count;
            st.CurrentTaskText = CompanionConfigStore.L(
                $"Несёт {countForTarget}/{st.CarryBuffer.Count} предм. в «{bestEntry.TargetDisplayName}»...",
                $"Carrying {countForTarget}/{st.CarryBuffer.Count} items to '{bestEntry.TargetDisplayName}'...",
                $"正将 {countForTarget}/{st.CarryBuffer.Count} 件物品搬往「{bestEntry.TargetDisplayName}」...");
        }

        private static bool TryFallbackDepositOrReroute(
            CarryItemEntry entry,
            List<DiscoveredContainer> candidateBoxes,
            bool requireCategoryMatch,
            InventoryData sourceInv,
            Vector2 selfPos,
            out InventoryData usedDestInv,
            out bool usedCompact,
            out bool reroutedToWalk)
        {
            usedDestInv = null;
            usedCompact = true;
            reroutedToWalk = false;
            if (candidateBoxes == null || candidateBoxes.Count == 0)
                return false;

            for (int d = 0; d < candidateBoxes.Count; d++)
            {
                var alt = candidateBoxes[d];
                if (alt?.Inventory == null || alt.Rule == null) continue;
                if (Il2CppSafe.IsAlive(sourceInv) && alt.Inventory.Pointer == sourceInv.Pointer) continue;
                if (Il2CppSafe.IsAlive(entry.TargetInv) && alt.Inventory.Pointer == entry.TargetInv.Pointer) continue;
                if (requireCategoryMatch && (alt.Rule.Categories & entry.Category) == 0) continue;
                if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(alt.ContainerId)) continue;
                if (!InventoryTransfers.CanAcceptWholeItem(alt.Inventory, entry.Item)) continue;

                Transform altAnchor = Il2CppSafe.IsAliveUnity(alt.TerrainObj)
                    ? alt.TerrainObj.transform
                    : ResolveAnchorTransformForInventory(alt.Inventory);
                float altDist = PhysicalMovementHelper.ComputeSurfaceDistance(selfPos, alt.WorldPos, altAnchor);
                if (altDist <= PhysicalMovementHelper.MaxReachableInteractRadius &&
                    InventoryTransfers.TryDepositOrStackWhole(alt.Inventory, entry.Item))
                {
                    usedDestInv = alt.Inventory;
                    usedCompact = alt.Rule.AutoCompact;
                    return true;
                }
                else if (altDist > PhysicalMovementHelper.MaxReachableInteractRadius)
                {
                    entry.TargetContainerId = alt.ContainerId;
                    entry.TargetInv = alt.Inventory;
                    entry.TargetAnchor = altAnchor;
                    entry.TargetPos = alt.WorldPos;
                    entry.TargetDisplayName = alt.DisplayName;
                    reroutedToWalk = true;
                    return false;
                }
            }
            return false;
        }

        private static int DepositBatchAtCurrentDestination(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> destBoxes,
            out StorageCategory dominantCategory,
            List<DiscoveredContainer> fallbackDropoffBoxes = null)
        {
            dominantCategory = StorageCategory.None;
            if (st.CarryBuffer.Count == 0) return 0;

            int deposited = 0;
            var categoryCounts = new Dictionary<StorageCategory, int>();
            var touchedDestInventories = new HashSet<IntPtr>();
            var touchedDestRules = new List<(InventoryData inv, bool autoCompact)>();

            try
            {
                string targetBoxId = st.TargetContainerGuid;
                for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                {
                    var entry = st.CarryBuffer[i];
                    if (entry == null || !Il2CppSafe.IsAlive(entry.Item))
                    {
                        st.CarryBuffer.RemoveAt(i);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(targetBoxId) &&
                        !string.Equals(entry.TargetContainerId, targetBoxId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool placed = false;
                    InventoryData usedDestInv = null;
                    bool usedCompact = true;

                    if (Il2CppSafe.IsAlive(entry.TargetInv) &&
                        InventoryTransfers.TryDepositOrStackWhole(entry.TargetInv, entry.Item))
                    {
                        placed = true;
                        usedDestInv = entry.TargetInv;
                        if (CompanionConfigStore.ContainerRules.TryGetValue(entry.TargetContainerId ?? "", out var r))
                            usedCompact = r.AutoCompact;
                    }
                    else
                    {
                        // Never teleport into a distant chest! If another valid chest exists, re-route CarryBuffer entry so the NPC walks there next!
                        Vector2 selfPos = Il2CppSafe.IsAliveUnity(ag?.humanCharacter)
                            ? (Vector2)ag.humanCharacter.transform.position
                            : st.TargetContainerPos;
                        bool reroutedToWalk = false;
                        placed = TryFallbackDepositOrReroute(
                            entry, destBoxes, requireCategoryMatch: true, st.SourceContainerInv, selfPos,
                            out usedDestInv, out usedCompact, out reroutedToWalk);
                        if (!placed && !reroutedToWalk && st.SourceAllowDropoffFallback && fallbackDropoffBoxes != null)
                        {
                            placed = TryFallbackDepositOrReroute(
                                entry, fallbackDropoffBoxes, requireCategoryMatch: false, st.SourceContainerInv, selfPos,
                                out usedDestInv, out usedCompact, out reroutedToWalk);
                        }
                        if (reroutedToWalk)
                            continue;
                    }

                    if (placed)
                    {
                        deposited++;
                        categoryCounts.TryGetValue(entry.Category, out int cur);
                        categoryCounts[entry.Category] = cur + 1;
                        if (Il2CppSafe.IsAlive(usedDestInv) && touchedDestInventories.Add(usedDestInv.Pointer))
                            touchedDestRules.Add((usedDestInv, usedCompact));
                    }
                    else
                    {
                        // Keep unplaceable item physically in the companion's own backpack rather than teleporting across the map!
                        StoreCarriedItemOnCompanionOrSafeFallback(ag, entry);
                    }

                    st.CarryBuffer.RemoveAt(i);
                }

                for (int i = 0; i < touchedDestRules.Count; i++)
                {
                    var td = touchedDestRules[i];
                    if (td.autoCompact && Il2CppSafe.IsAlive(td.inv))
                    {
                        InventoryTransfers.SafeCompactInventory(td.inv);
                    }
                }

                if (deposited > 0)
                {
                    RefreshOpenLootPanelIfNeeded();
                }

                int bestCount = 0;
                foreach (var kv in categoryCounts)
                {
                    if (kv.Value > bestCount)
                    {
                        bestCount = kv.Value;
                        dominantCategory = kv.Key;
                    }
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("DepositBatchAtCurrentDestination error: " + e.Message);
            }

            return deposited;
        }

        private static void FinishCompanionSorting(GOAPAgent ag, CompanionRuntimeState st, List<DiscoveredContainer> containers)
        {
            FlushCarryBufferSafely(st);
            if (st != null && !string.IsNullOrEmpty(st.NpcGuid))
                _ownBackpackOnlyJobs.Remove(st.NpcGuid);
            st.IsSortingActive = false;
            st.SortingPhase = 0;
            st.PendingTransferCount = 0;
            st.CurrentTaskText = CompanionConfigStore.L("Склад отсортирован", "Base storage sorted", "仓库已整理完毕");
            if (Il2CppSafe.IsAliveUnity(ag))
                SmartChatterSystem.Speak(ag, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 6.0f, force: true);

            int remDrop = InspectRemainingDropoffItems(containers, out var remMask);
            if (remDrop > 0)
            {
                string remCats = ItemCategorizer.FormatCategoriesSummary(remMask, false);
                st.NoteBlocked(CompanionBlockReason.NoStorageSpace, remCats);
                SetBanner(CompanionConfigStore.L(
                    $"{st.LastKnownName}: перенос завершён. В приёмнике осталось {remDrop} шт. (нет места/ящика для: {remCats})",
                    $"{st.LastKnownName}: carry finished. {remDrop} items remain in Drop-off (need space/chest for: {remCats})",
                    $"{st.LastKnownName}：搬运完成。中转箱剩余 {remDrop} 件（缺少空位/分类箱：{remCats}）"), 8f);
            }
            else
            {
                SetBanner(CompanionConfigStore.L(
                    $"{st.LastKnownName}: перенос и сортировка ящиков завершены (уложено за сессию: {st.SortedItemsThisSession} шт.)",
                    $"{st.LastKnownName}: finished carrying & sorting chests (session total: {st.SortedItemsThisSession})",
                    $"{st.LastKnownName}：储物箱搬运与整理已完成（本局累计整理：{st.SortedItemsThisSession} 件）"));
            }
        }

        private static void StoreCarriedItemOnCompanionOrSafeFallback(GOAPAgent ag, CarryItemEntry entry)
        {
            if (entry == null || !Il2CppSafe.IsAlive(entry.Item)) return;
            try
            {
                if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAlive(ag.npcData))
                {
                    var shared = ag.npcData.GetOrCreateSharedInventory();
                    if (Il2CppSafe.IsAlive(shared) && InventoryTransfers.TryDepositOrStackWhole(shared, entry.Item))
                        return;
                    var npcInv = ag.npcData.characterData?.inventoryData;
                    if (Il2CppSafe.IsAlive(npcInv) && InventoryTransfers.TryDepositOrStackWhole(npcInv, entry.Item))
                        return;
                }
            }
            catch { }
            InventoryTransfers.RestoreCarriedItemSafely(entry.FallbackSourceInv, entry.TargetInv, entry.Item);
        }

        public static void ReturnCarryBufferWithoutTeleport(GOAPAgent ag, CompanionRuntimeState st)
        {
            if (st == null || st.CarryBuffer.Count == 0) return;
            try
            {
                for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                {
                    StoreCarriedItemOnCompanionOrSafeFallback(ag, st.CarryBuffer[i]);
                }
                st.CarryBuffer.Clear();
                st.PendingTransferCount = 0;
                RefreshOpenLootPanelIfNeeded();
            }
            catch { }
        }

        public static void FlushCarryBufferSafely(CompanionRuntimeState st)
        {
            if (st == null || st.CarryBuffer.Count == 0) return;
            try
            {
                for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                {
                    var entry = st.CarryBuffer[i];
                    if (entry != null && Il2CppSafe.IsAlive(entry.Item))
                    {
                        InventoryTransfers.RestoreCarriedItemSafely(entry.FallbackSourceInv, entry.TargetInv, entry.Item);
                    }
                }
                st.CarryBuffer.Clear();
                st.PendingTransferCount = 0;
                RefreshOpenLootPanelIfNeeded();
            }
            catch { }
        }

        public static void FlushAllCarryBuffers()
        {
            try
            {
                foreach (var kv in CompanionConfigStore.CompanionStates)
                {
                    FlushCarryBufferSafely(kv.Value);
                }
            }
            catch { }
        }

        internal static void RefreshOpenLootPanelIfNeeded()
        {
            try
            {
                var panel = LootInventoryPanel._instance;
                if (Il2CppSafe.IsAliveUnity(panel) && panel.gameObject.activeInHierarchy &&
                    Il2CppSafe.IsAlive(LootInventoryPanel.CurrentOpenInventoryData))
                {
                    panel.Refresh();
                }
            }
            catch { }
        }

        private static void SpeakCategoryDeposit(GOAPAgent agent, StorageCategory cat)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return;
            ChatterTopic topic = ChatterTopic.SortDepositMaterials;
            if ((cat & (StorageCategory.MeleeWeapons | StorageCategory.RangedWeapons | StorageCategory.AmmoAndMags)) != 0)
                topic = ChatterTopic.SortDepositWeapons;
            else if ((cat & (StorageCategory.FreshFood | StorageCategory.CannedAndDrinks)) != 0)
                topic = ChatterTopic.SortDepositFood;
            else if ((cat & StorageCategory.Medicine) != 0)
                topic = ChatterTopic.SortDepositMeds;

            SmartChatterSystem.Speak(agent, topic, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
        }

        private static void TryAutoDropoffAtBase(GOAPAgent ag, CompanionRuntimeState st, float now)
        {
            try
            {
                var shared = ag.npcData?.sharedInventoryData;
                var personalInv = ag.npcData?.characterData?.inventoryData;
                bool hasSharedItems = Il2CppSafe.IsAlive(shared) && shared.itemList != null && shared.itemList.Count > 0;
                bool hasPersonalItems = Il2CppSafe.IsAlive(personalInv) && personalInv.itemList != null && personalInv.itemList.Count > 0;
                if (!hasSharedItems && !hasPersonalItems)
                    return;

                var containers = ScanNearbyContainers(force: false);
                if (containers.Count == 0)
                {
                    st.LastAutoDropoffTime = now;
                    return;
                }

                bool nearDropoffOrCatBox = false;
                Vector2 selfPos = ag.humanCharacter.transform.position;
                var destBoxes = GetCategorizedDestinationBoxes(containers);
                var fallbackDropoff = GetAllDropoffFallbackBoxes(containers);

                foreach (var c in containers)
                {
                    if (c?.Rule == null || !c.Rule.IsMarkedByPlayer || !Il2CppSafe.IsAlive(c.Inventory)) continue;
                    if ((c.Rule.IsDropoffSource || c.Rule.Categories != StorageCategory.None) &&
                        Vector2.Distance(selfPos, c.WorldPos) <= 18.0f)
                    {
                        nearDropoffOrCatBox = true;
                        break;
                    }
                }

                if (!nearDropoffOrCatBox || (destBoxes.Count == 0 && fallbackDropoff.Count == 0))
                {
                    st.LastAutoDropoffTime = now;
                    return;
                }

                if (!string.IsNullOrEmpty(st.NpcGuid))
                    _ownBackpackOnlyJobs.Add(st.NpcGuid);

                bool started = TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null, onlyOwnBackpack: true);
                st.LastAutoDropoffTime = now;
                if (started)
                {
                    SmartChatterSystem.Speak(ag, ChatterTopic.BaseAutoDropoff, minCooldown: 2f, bubbleDuration: 6f, force: true);
                }
                else if (!string.IsNullOrEmpty(st.NpcGuid))
                {
                    _ownBackpackOnlyJobs.Remove(st.NpcGuid);
                }
            }
            catch { }
        }

        public static void SetBanner(string msg, float seconds = 6f)
        {
            LastSortStatusBanner = msg;
            LastSortStatusBannerUntil = Time.unscaledTime + seconds;
        }
    }
}
