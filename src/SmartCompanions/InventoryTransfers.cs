using System;

namespace ZedZone.SmartCompanions
{
    /// <summary>Whole-item transfers: never merge a partial stack before removing its source.</summary>
    public static class InventoryTransfers
    {
        public static bool Contains(InventoryData inventory, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(inventory) || !Il2CppSafe.IsAlive(item) || inventory.itemList == null) return false;
            for (int i = 0; i < inventory.itemList.Count; i++)
                if (Il2CppSafe.IsAlive(inventory.itemList[i]) && inventory.itemList[i].Pointer == item.Pointer) return true;
            return false;
        }

        public static bool TryAddWholeCopy(InventoryData destination, ItemData sourceItem, out ItemData added)
        {
            added = null;
            if (!Il2CppSafe.IsAlive(destination) || !Il2CppSafe.IsAlive(sourceItem)) return false;
            var copy = sourceItem.CopyItemData(null);
            if (!Il2CppSafe.IsAlive(copy)) return false;
            try
            {
                // executeStack=false deliberately declines partial merges when a bag is full.
                // TryAddItemWithoutChangeItem is NOT assumed to be a dry-run capacity query.
                bool accepted = destination.TryAddItem(copy, out bool partial, true, false);
                if (accepted && !partial && Contains(destination, copy))
                {
                    added = copy;
                    return true;
                }
                if (Contains(destination, copy)) destination.RemoveItem(copy, true);
                return false;
            }
            catch
            {
                if (Contains(destination, copy)) destination.RemoveItem(copy, true);
                throw;
            }
        }

        public static bool TryMoveWhole(InventoryData source, InventoryData destination, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(source) || !Il2CppSafe.IsAlive(destination) || source.Pointer == destination.Pointer)
                return false;
            if (!Contains(source, item)) return false;
            ItemData added = null;
            try
            {
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
