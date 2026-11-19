using System;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>Whole-item transfers, safe stack merging, overflow-free compaction &amp; invisible carry buffer helpers.</summary>
    public static class InventoryTransfers
    {
        public static bool Contains(InventoryData inventory, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(inventory) || !Il2CppSafe.IsAlive(item) || inventory.itemList == null) return false;
            for (int i = 0; i < inventory.itemList.Count; i++)
                if (Il2CppSafe.IsAlive(inventory.itemList[i]) && inventory.itemList[i].Pointer == item.Pointer) return true;
            return false;
        }

        public static int EstimateItemCells(ItemData item)
        {
            if (!Il2CppSafe.IsAlive(item)) return 1;
            try
            {
                var attr = ItemManager.instance?.GetItemAttrById(item.itemId);
                if (Il2CppSafe.IsAliveUnity(attr))
                    return Mathf.Max(1, attr.itemSize.x * attr.itemSize.y);
            }
            catch { }
            return 1;
        }

        public static float GetTotalMergeSpareCapacity(InventoryData destination, ItemData item, out ItemAttr resolvedAttr)
        {
            resolvedAttr = null;
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(item) || destination.itemList == null)
                return 0f;
            if (destination.disableStack) return 0f;
            try
            {
                var attr = ItemManager.instance?.GetItemAttrById(item.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr) || attr.stackNumber <= 1 || !destination.IsStackAllowed(attr))
                    return 0f;
                resolvedAttr = attr;

                float spare = 0f;
                for (int i = 0; i < destination.itemList.Count; i++)
                {
                    var ex = destination.itemList[i];
                    if (!Il2CppSafe.IsAlive(ex) || ex.itemId != item.itemId) continue;
                    if (!InventoryData.CanMergeStack(ex, item)) continue;
                    if (ex.itemNumberFloat < attr.stackNumber)
                        spare += (attr.stackNumber - ex.itemNumberFloat);
                }
                return spare;
            }
            catch { return 0f; }
        }

        public static bool CanMergeCompletelyIntoExistingStack(InventoryData destination, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(item)) return false;
            float needed = Mathf.Max(1f, item.itemNumberFloat);
            float spare = GetTotalMergeSpareCapacity(destination, item, out _);
            return spare + 0.001f >= needed;
        }

        /// <summary>
        /// Adds a whole copy of <paramref name="sourceItem"/> to <paramref name="destination"/>, merging into existing
        /// compatible stacks first (both full merges and partial top-up + remainder slot), and compacting fragmented
        /// grid cells via <see cref="SafeCompactInventory"/> if needed. Never drops or truncates any item count.
        /// </summary>
        public static bool TryAddWholeWithStacking(
            InventoryData destination,
            ItemData sourceItem,
            out ItemData addedRemainder,
            out Action rollbackAction,
            bool notify = true)
        {
            addedRemainder = null;
            rollbackAction = null;
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(sourceItem)) return false;

            // 1. Complete merge into existing stack(s) -> 0 new grid cells required!
            if (CanMergeCompletelyIntoExistingStack(destination, sourceItem))
            {
                var beforeCounts = new System.Collections.Generic.List<(ItemData ex, float count)>();
                if (destination.itemList != null)
                {
                    for (int i = 0; i < destination.itemList.Count; i++)
                    {
                        var ex = destination.itemList[i];
                        if (Il2CppSafe.IsAlive(ex) && ex.itemId == sourceItem.itemId)
                            beforeCounts.Add((ex, ex.itemNumberFloat));
                    }
                }

                var copy = sourceItem.CopyItemData(null);
                if (Il2CppSafe.IsAlive(copy))
                {
                    bool accepted = destination.TryAddItem(copy, out bool partial, notify, true);
                    if (accepted && !partial)
                    {
                        var placedRef = Contains(destination, copy) ? copy : null;
                        addedRemainder = placedRef;
                        rollbackAction = () =>
                        {
                            for (int i = 0; i < beforeCounts.Count; i++)
                            {
                                var (ex, cnt) = beforeCounts[i];
                                if (Il2CppSafe.IsAlive(ex))
                                {
                                    ex.itemNumberFloat = cnt;
                                    try { destination.NotifyDataChanged(true, ex, false); } catch { }
                                }
                            }
                            if (placedRef != null && Contains(destination, placedRef))
                                destination.RemoveItem(placedRef, notify);
                        };
                        return true;
                    }
                    // Restore exact stack counts if partial/rejected
                    for (int i = 0; i < beforeCounts.Count; i++)
                        if (Il2CppSafe.IsAlive(beforeCounts[i].ex))
                            beforeCounts[i].ex.itemNumberFloat = beforeCounts[i].count;
                    if (Contains(destination, copy))
                        destination.RemoveItem(copy, notify);
                }
            }

            // 2. Partial top-up into existing stack(s) + remainder placed into a free slot
            float totalQty = sourceItem.itemNumberFloat;
            float spare = GetTotalMergeSpareCapacity(destination, sourceItem, out var attr);
            if (spare >= 0.999f && spare + 0.001f < totalQty && Il2CppSafe.IsAliveUnity(attr))
            {
                float remQty = totalQty - spare;
                var remCopy = sourceItem.CopyItemData(null);
                if (Il2CppSafe.IsAlive(remCopy))
                {
                    remCopy.itemNumberFloat = remQty;
                    if (TryAddWholeCopyWithCompactRetry(destination, remCopy, out var placedRem, notify))
                    {
                        var topped = new System.Collections.Generic.List<(ItemData ex, float prev)>();
                        float leftToTop = spare;
                        for (int i = 0; i < destination.itemList.Count && leftToTop > 0.001f; i++)
                        {
                            var ex = destination.itemList[i];
                            if (!Il2CppSafe.IsAlive(ex) || ex.Pointer == placedRem.Pointer || ex.itemId != sourceItem.itemId) continue;
                            if (!InventoryData.CanMergeStack(ex, sourceItem)) continue;
                            float room = attr.stackNumber - ex.itemNumberFloat;
                            if (room <= 0.001f) continue;
                            float delta = Mathf.Min(room, leftToTop);
                            topped.Add((ex, ex.itemNumberFloat));
                            ex.itemNumberFloat += delta;
                            leftToTop -= delta;
                            if (notify) { try { destination.NotifyDataChanged(true, ex, false); } catch { } }
                        }

                        if (leftToTop <= 0.01f)
                        {
                            addedRemainder = placedRem;
                            rollbackAction = () =>
                            {
                                for (int i = 0; i < topped.Count; i++)
                                {
                                    if (Il2CppSafe.IsAlive(topped[i].ex))
                                    {
                                        topped[i].ex.itemNumberFloat = topped[i].prev;
                                        try { destination.NotifyDataChanged(true, topped[i].ex, false); } catch { }
                                    }
                                }
                                if (placedRem != null && Contains(destination, placedRem))
                                    destination.RemoveItem(placedRem, notify);
                            };
                            return true;
                        }

                        for (int i = 0; i < topped.Count; i++)
                            if (Il2CppSafe.IsAlive(topped[i].ex))
                                topped[i].ex.itemNumberFloat = topped[i].prev;
                        if (Contains(destination, placedRem))
                            destination.RemoveItem(placedRem, notify);
                    }
                }
            }

            // 3. Whole item into a free grid slot (with overflow-free compaction retry if cells are fragmented)
            if (TryAddWholeCopyWithCompactRetry(destination, sourceItem, out var wholePlaced, notify))
            {
                addedRemainder = wholePlaced;
                rollbackAction = () =>
                {
                    if (wholePlaced != null && Contains(destination, wholePlaced))
                        destination.RemoveItem(wholePlaced, notify);
                };
                return true;
            }

            return false;
        }

        private static bool TryAddWholeCopyWithCompactRetry(InventoryData destination, ItemData sourceItem, out ItemData added, bool notify)
        {
            if (TryAddWholeCopy(destination, sourceItem, out added, notify))
                return true;
            try
            {
                if (destination.CountFreeCells() >= EstimateItemCells(sourceItem))
                {
                    SafeCompactInventory(destination);
                    if (TryAddWholeCopy(destination, sourceItem, out added, notify))
                        return true;
                }
            }
            catch { }
            added = null;
            return false;
        }

        public static bool TryDepositOrStackWhole(InventoryData destination, ItemData sourceItem, bool notify = true)
        {
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(sourceItem)) return false;
            try
            {
                return TryAddWholeWithStacking(destination, sourceItem, out _, out _, notify);
            }
            catch
            {
                return TryAddWholeCopy(destination, sourceItem, out _, notify);
            }
        }

        public static void SafeCompactInventory(InventoryData inv)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null || inv.itemList.Count <= 1) return;
            try
            {
                // TryAutoSortingWithoutOverflow captures a LayoutSnapshot and restores it if any item would overflow!
                // Never call AutoSorting(false, ...) directly, which discards overflow items when executeDropFlag is false.
                inv.TryAutoSortingWithoutOverflow(false, out _);
            }
            catch { }
        }

        public static bool TryAddWholeCopy(InventoryData destination, ItemData sourceItem, out ItemData added, bool notify = true)
        {
            added = null;
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(sourceItem)) return false;
            var copy = sourceItem.CopyItemData(null);
            if (!Il2CppSafe.IsAlive(copy)) return false;
            try
            {
                // executeStack=false deliberately declines partial merges when a bag is full.
                bool accepted = destination.TryAddItem(copy, out bool partial, notify, false);
                if (accepted && !partial && Contains(destination, copy))
                {
                    added = copy;
                    return true;
                }
                if (Contains(destination, copy)) destination.RemoveItem(copy, notify);
                return false;
            }
            catch
            {
                if (Contains(destination, copy)) destination.RemoveItem(copy, notify);
                throw;
            }
        }

        public static bool CanAcceptWholeItem(InventoryData destination, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(item)) return false;
            try
            {
                if (CanMergeCompletelyIntoExistingStack(destination, item))
                    return true;

                float spare = GetTotalMergeSpareCapacity(destination, item, out var attr);
                if (spare >= 0.999f && spare + 0.001f < item.itemNumberFloat && Il2CppSafe.IsAliveUnity(attr))
                {
                    var remProbe = item.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(remProbe))
                    {
                        remProbe.itemNumberFloat = item.itemNumberFloat - spare;
                        if (TryAddWholeCopy(destination, remProbe, out var placedRem, notify: false))
                        {
                            destination.RemoveItem(placedRem, false);
                            return true;
                        }
                    }
                }

                if (TryAddWholeCopy(destination, item, out var probe, notify: false))
                {
                    destination.RemoveItem(probe, false);
                    return true;
                }

                if (destination.CountFreeCells() >= EstimateItemCells(item) &&
                    destination.itemList != null && destination.itemList.Count > 1)
                {
                    SafeCompactInventory(destination);
                    if (TryAddWholeCopy(destination, item, out var probe2, notify: false))
                    {
                        destination.RemoveItem(probe2, false);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool TryTakeIntoCarryBuffer(InventoryData source, ItemData item, out ItemData carriedCopy)
        {
            carriedCopy = null;
            if (!Il2CppSafe.IsAlive(source) || !Il2CppSafe.IsAlive(item)) return false;
            if (!Contains(source, item)) return false;
            try
            {
                var copy = item.CopyItemData(null);
                if (!Il2CppSafe.IsAlive(copy)) return false;
                if (source.RemoveItem(item, true))
                {
                    carriedCopy = copy;
                    return true;
                }
            }
            catch (Exception error)
            {
                SmartCompanionsPlugin.LogWarning("TryTakeIntoCarryBuffer error: " + error.Message);
            }
            return false;
        }

        public static bool RestoreCarriedItemSafely(InventoryData fallbackSource, InventoryData fallbackDest, ItemData carriedItem)
        {
            if (!Il2CppSafe.IsAlive(carriedItem)) return false;
            try
            {
                if (Il2CppSafe.IsAlive(fallbackSource))
                {
                    if (TryDepositOrStackWhole(fallbackSource, carriedItem)) return true;
                    var copy1 = carriedItem.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(copy1) && fallbackSource.TryAddItemWithAutoSorting(copy1)) return true;
                }
                var gc = GameController.instance;
                if (Il2CppSafe.IsAliveUnity(gc))
                {
                    var copy3 = carriedItem.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(copy3) && gc.AddItemToPlayer(copy3, false)) return true;
                }
            }
            catch (Exception error)
            {
                SmartCompanionsPlugin.LogWarning("RestoreCarriedItemSafely error: " + error.Message);
            }
            return false;
        }

        public static bool TryMoveWhole(InventoryData source, InventoryData destination, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(source) || !Il2CppSafe.IsAlive(destination) || source.Pointer == destination.Pointer)
                return false;
            if (!Contains(source, item)) return false;
            ItemData added = null;
            Action rollback = null;
            try
            {
                if (!TryAddWholeWithStacking(destination, item, out added, out rollback, notify: true))
                    return false;
                if (source.RemoveItem(item, true))
                    return true;
            }
            catch (Exception error)
            {
                // If a notification threw after successful source removal, keep the completed
                // destination transfer; rolling it back here would lose the only remaining item.
                if (!Contains(source, item)) return true;
                SmartCompanionsPlugin.LogWarning("Whole-item transfer: " + error.Message);
            }
            try { rollback?.Invoke(); }
            catch
            {
                if (Il2CppSafe.IsAlive(added) && Contains(destination, added)) destination.RemoveItem(added, true);
            }
            return false;
        }
    }
}
