using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class InventoryRegression
    {
        internal static void Run()
        {
            // No world inventory is referenced. Exercise retained native APIs on throwaway data.
            foreach (int itemId in new[] { 220, 232 }) VerifyItem(itemId);
            var empty = MakeInventory(8, 8);
            if (RationSharing.TryConsumePortion(empty)) throw new Exception("Empty inventory produced a ration.");
            var food = MakeItem(232, 2f);
            AddFixture(empty, food);
            if (!RationSharing.TryConsumePortion(empty) || Mathf.Abs(Total(empty) - 1f) > 0.001f)
                throw new Exception("One ration did not consume exactly one food portion.");
            if (!RationSharing.TryConsumePortion(empty) || Total(empty) != 0f)
                throw new Exception("Final food portion was not removed.");
            if (RationSharing.TryConsumePortion(empty)) throw new Exception("Exhausted rations remained usable.");
            UiSmokePlugin.Logger.LogInfo("[InventoryRegression] PASS full-destination/retry conservation, whole-item transfer, exact food consumption and empty-food rejection.");
        }

        private static void VerifyItem(int itemId)
        {
            var attr = ItemManager.instance.GetItemAttrById(itemId);
            if (attr == null) throw new Exception("Missing fixture item " + itemId);
            int stack = Math.Max(1, attr.stackNumber);
            int width = Math.Max(1, attr.itemSize.x), height = Math.Max(1, attr.itemSize.y);
            var source = MakeInventory(width * 2, height * 2);
            var destination = MakeInventory(width, height);
            var item = MakeItem(itemId, Math.Min(2, stack));
            AddFixture(source, item);
            AddFixture(destination, MakeItem(itemId, Math.Max(1, stack - 1)));
            float before = Total(source) + Total(destination);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                if (InventoryTransfers.TryMoveWhole(source, destination, item))
                    throw new Exception("Whole-item move unexpectedly fit occupied inventory " + itemId);
                if (Mathf.Abs(Total(source) + Total(destination) - before) > 0.001f)
                    throw new Exception("Failed/repeated transfer changed total quantity " + itemId);
            }
            var roomy = MakeInventory(width * 2, height * 2);
            if (!InventoryTransfers.TryMoveWhole(source, roomy, item) || Total(source) != 0f ||
                Mathf.Abs(Total(roomy) + Total(destination) - before) > 0.001f)
                throw new Exception("Successful transfer failed quantity conservation " + itemId);
            if (InventoryTransfers.TryMoveWhole(source, roomy, item))
                throw new Exception("Already removed source item transferred twice.");

            // Observe legacy merge semantics separately; do not let this mutate production state.
            var legacy = MakeInventory(width, height);
            AddFixture(legacy, MakeItem(itemId, Math.Max(1, stack - 1)));
            var candidate = MakeItem(itemId, Math.Min(2, stack));
            float previous = Total(legacy);
            bool accepted = legacy.TryAddItem(candidate, out bool partial, false, true);
            UiSmokePlugin.Logger.LogInfo($"[InventoryRegression] Native merge item={itemId}: accepted={accepted}, partial={partial}, destinationDelta={Total(legacy) - previous}, remainder={candidate.itemNumberFloat}.");
        }

        private static InventoryData MakeInventory(int width, int height)
        {
            var inventory = new InventoryData
            {
                inventorySizeX = width, inventorySizeY = height,
                itemList = new Il2CppSystem.Collections.Generic.List<ItemData>()
            };
            inventory.SetOccupyDataDirty();
            return inventory;
        }

        private static ItemData MakeItem(int id, float quantity)
        {
            var item = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(id), false, true);
            item.itemNumberFloat = quantity;
            return item;
        }

        private static void AddFixture(InventoryData inventory, ItemData item)
        {
            if (!inventory.TryAddItem(item, out bool partial, false, false) || partial)
                throw new Exception("Could not seed inventory fixture.");
        }

        private static float Total(InventoryData inventory)
        {
            float total = 0f;
            foreach (var item in inventory.itemList) total += item.itemNumberFloat;
            return total;
        }
    }
}
