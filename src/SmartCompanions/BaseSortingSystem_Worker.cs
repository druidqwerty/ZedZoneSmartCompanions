using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
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
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            float now = Time.unscaledTime;

            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);

                // Automatic Base Drop-off when returning to a base drop-off box out of combat!
                if (!st.IsSortingActive && CompanionConfigStore.EnableBaseAutoDropoff && st.AllowAutoDropoffAtBase &&
                    !Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && now - st.LastAutoDropoffTime > 25f)
                {
                    TryAutoDropoffAtBase(ag, st, now);
                }

                if (!st.IsSortingActive) continue;

                // Abort sorting and safely return carried items if companion enters combat
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
                    // Phase 1: Walking to source container to pick up items into invisible CarryBuffer
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
                        // Source had no more movable items; check if another source box has items
                        if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null))
                            FinishCompanionSorting(ag, st);
                    }
                }
                else if (st.SortingPhase == 2)
                {
                    // Phase 2: Walking with items in CarryBuffer to destination container
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
                        // Still carrying items for another destination box -> walk there next!
                        SelectNextDestinationFromBuffer(ag, st, now);
                    }
                    else
                    {
                        // Buffer empty -> check if any source box still has unsorted items for another trip!
                        if (!TryAssignNextCarrySource(ag, st, containers, destBoxes, null, null))
                            FinishCompanionSorting(ag, st);
                    }
                }
            }
        }

        private static bool TryAssignNextCarrySource(
            GOAPAgent ag,
            CompanionRuntimeState st,
            List<DiscoveredContainer> allContainers,
            List<DiscoveredContainer> destBoxes,
            InventoryData extraSource,
            HashSet<IntPtr> claimedSources)
        {
            if (destBoxes == null || destBoxes.Count == 0) return false;
            Vector2 selfPos = Il2CppSafe.IsAliveUnity(ag?.humanCharacter)
                ? (Vector2)ag.humanCharacter.transform.position
                : Vector2.zero;

            var candidates = new List<(InventoryData inv, Vector2 pos, string id, string name, StorageCategory stayMask, float dist)>();

            // 1. Companion's own sharedInventoryData (already carried on companion -> 0m distance!)
            var shared = ag?.npcData?.sharedInventoryData;
            if (Il2CppSafe.IsAlive(shared) && shared.itemList != null && shared.itemList.Count > 0)
            {
                candidates.Add((shared, selfPos, "shared_" + ag.npcData.npcGUID,
                    CompanionConfigStore.L("Рюкзак напарника", "Companion Backpack", "队友背包"), StorageCategory.None, 0f));
            }

            // 2. Explicit extraSource (if requested)
            if (Il2CppSafe.IsAlive(extraSource) && extraSource.itemList != null && extraSource.itemList.Count > 0)
            {
                var tobj = FindTerrainObjectByInventory(extraSource);
                Vector2 pos = Il2CppSafe.IsAliveUnity(tobj) ? (Vector2)tobj.transform.position : selfPos;
                string id = GetContainerStableId(extraSource, tobj);
                candidates.Add((extraSource, pos, id, extraSource.inventoryTitleName ?? "Container", StorageCategory.None, Vector2.Distance(selfPos, pos)));
            }

            // 3. Drop-off boxes first, then categorized boxes with misplaced items
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < allContainers.Count; i++)
                {
                    var c = allContainers[i];
                    if (c == null || !Il2CppSafe.IsAlive(c.Inventory) || c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                        continue;
                    if (c.Rule == null) continue;

                    if (pass == 0 && c.Rule.IsDropoffSource)
                    {
                        candidates.Add((c.Inventory, c.WorldPos, c.ContainerId, c.DisplayName, StorageCategory.None, Vector2.Distance(selfPos, c.WorldPos)));
                    }
                    else if (pass == 1 && !c.Rule.IsDropoffSource && c.Rule.Categories != StorageCategory.None && c.Rule.Categories != StorageCategory.All)
                    {
                        candidates.Add((c.Inventory, c.WorldPos, c.ContainerId, c.DisplayName, c.Rule.Categories, Vector2.Distance(selfPos, c.WorldPos)));
                    }
                }
            }

            // Pick closest candidate that actually has at least 1 movable item with a valid destination
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (claimedSources != null && claimedSources.Contains(cand.inv.Pointer))
                    continue;
                if (!HasMovableItemInSource(cand.inv, cand.stayMask, destBoxes))
                    continue;

                if (claimedSources != null)
                    claimedSources.Add(cand.inv.Pointer);

                st.IsSortingActive = true;
                st.SortingPhase = 1;
                st.SourceContainerGuid = cand.id;
                st.SourceContainerName = cand.name;
                st.SourceContainerInv = cand.inv;
                st.SourceAllowedStayMask = cand.stayMask;
                st.TargetContainerGuid = cand.id;
                st.TargetContainerName = cand.name;
                st.TargetContainerPos = cand.pos;
                st.SortingStateStartTime = Time.unscaledTime;
                st.CurrentTaskText = CompanionConfigStore.L(
                    $"Идёт к «{cand.name}» за предметами...",
                    $"Walking to '{cand.name}' to pick up items...",
                    $"正在前往「{cand.name}」取物...");
                return true;
            }

            return false;
        }

        private static bool HasMovableItemInSource(InventoryData srcInv, StorageCategory stayMask, List<DiscoveredContainer> destBoxes)
        {
            if (!Il2CppSafe.IsAlive(srcInv) || srcInv.itemList == null) return false;
            for (int i = 0; i < srcInv.itemList.Count; i++)
            {
                var item = srcInv.itemList[i];
                if (!Il2CppSafe.IsAlive(item)) continue;
                var cat = ItemCategorizer.ClassifyItem(item);
                if (stayMask != StorageCategory.None && (stayMask & cat) != 0)
                    continue;
                for (int d = 0; d < destBoxes.Count; d++)
                {
                    var dest = destBoxes[d];
                    if (dest?.Inventory == null || dest.Inventory.Pointer == srcInv.Pointer) continue;
                    if ((dest.Rule.Categories & cat) == 0) continue;
                    if (InventoryTransfers.CanAcceptWholeItem(dest.Inventory, item))
                        return true;
                }
            }
            return false;
        }
    }
}
