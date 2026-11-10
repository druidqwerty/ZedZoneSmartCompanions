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

        public static bool CanMergeCompletelyIntoExistingStack(InventoryData destination, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(item) || destination.itemList == null)
                return false;
            if (destination.disableStack) return false;
            try
            {
                var attr = ItemManager.instance?.GetItemAttrById(item.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr) || attr.stackNumber <= 1 || !destination.IsStackAllowed(attr))
                    return false;

                float needed = Mathf.Max(1f, item.itemNumberFloat);
                float spare = 0f;
                for (int i = 0; i < destination.itemList.Count; i++)
                {
                    var ex = destination.itemList[i];
                    if (!Il2CppSafe.IsAlive(ex) || ex.itemId != item.itemId) continue;
                    if (!InventoryData.CanMergeStack(ex, item)) continue;
                    if (ex.itemNumberFloat < attr.stackNumber)
                        spare += (attr.stackNumber - ex.itemNumberFloat);
                    if (spare + 0.001f >= needed)
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static bool TryDepositOrStackWhole(InventoryData destination, ItemData sourceItem, bool notify = true)
        {
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(sourceItem)) return false;
            try
            {
                if (CanMergeCompletelyIntoExistingStack(destination, sourceItem))
                {
                    var copy = sourceItem.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(copy))
                    {
                        bool accepted = destination.TryAddItem(copy, out bool partial, notify, true);
                        if (accepted && !partial)
                            return true;
                        if (Contains(destination, copy))
                            destination.RemoveItem(copy, notify);
                    }
                }
                return TryAddWholeCopy(destination, sourceItem, out _, notify);
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
                if (TryAddWholeCopy(destination, item, out var probe, notify: false))
                {
                    destination.RemoveItem(probe, false);
                    return true;
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
                if (Il2CppSafe.IsAlive(fallbackDest))
                {
                    if (TryDepositOrStackWhole(fallbackDest, carriedItem)) return true;
                    var copy2 = carriedItem.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(copy2) && fallbackDest.TryAddItemWithAutoSorting(copy2)) return true;
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
            try
            {
                if (CanMergeCompletelyIntoExistingStack(destination, item))
                {
                    var copy = item.CopyItemData(null);
                    if (Il2CppSafe.IsAlive(copy) && destination.TryAddItem(copy, out bool partial, true, true) && !partial)
                    {
                        if (source.RemoveItem(item, true)) return true;
                    }
                }
                if (!TryAddWholeCopy(destination, item, out added)) return false;
                if (source.RemoveItem(item, true)) return true;
            }
            catch (Exception error)
            {
                // If a notification threw after successful source removal, keep the completed
                // destination transfer; rolling it back here would lose the only remaining item.
                if (added != null && !Contains(source, item) && Contains(destination, added)) return true;
                SmartCompanionsPlugin.LogWarning("Whole-item transfer: " + error.Message);
            }
            if (Il2CppSafe.IsAlive(added) && Contains(destination, added)) destination.RemoveItem(added, true);
            return false;
        }
    }
}
