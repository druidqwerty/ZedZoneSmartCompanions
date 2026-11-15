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

            // v3.0.2: Coconut (230) classified as SweetAndSnack + FreshFood (never DrinkAndSoda / CannedAndDrinks)
            var coconut = MakeItem(230, 1f);
            if (ItemCategorizer.ClassifyFoodKind(coconut) != FoodKind.SweetAndSnack)
                throw new Exception("Coconut (230) did not classify as FoodKind.SweetAndSnack: " + ItemCategorizer.ClassifyFoodKind(coconut));
            if (ItemCategorizer.ClassifyItem(coconut) != StorageCategory.FreshFood)
                throw new Exception("Coconut (230) did not classify as StorageCategory.FreshFood: " + ItemCategorizer.ClassifyItem(coconut));

            // v3.0.2: Hybrid [Drop-off + Medicine] vs Pure [Drop-off] container routing
            var hybridBox = new DiscoveredContainer
            {
                ContainerId = "v302_hybrid_med",
                Inventory = MakeInventory(4, 4),
                DistanceToPlayer = 2f,
                Rule = new ContainerRuleProfile
                {
                    ContainerId = "v302_hybrid_med",
                    IsMarkedByPlayer = true,
                    IsDropoffSource = true,
                    Categories = StorageCategory.Medicine,
                    Priority = 4
                }
            };
            AddFixture(hybridBox.Inventory, MakeItem(220, 1f)); // Bandage belongs in hybrid Medicine chest -> 0 remaining dropoff items!
            var pureDropBox = new DiscoveredContainer
            {
                ContainerId = "v302_pure_drop",
                Inventory = MakeInventory(4, 4),
                DistanceToPlayer = 3f,
                Rule = new ContainerRuleProfile
                {
                    ContainerId = "v302_pure_drop",
                    IsMarkedByPlayer = true,
                    IsDropoffSource = true,
                    Categories = StorageCategory.None,
                    Priority = 3
                }
            };
            var boxList = new System.Collections.Generic.List<DiscoveredContainer> { hybridBox, pureDropBox };
            var catDest = BaseSortingSystem.GetCategorizedDestinationBoxes(boxList);
            var pureDest = BaseSortingSystem.GetPureDropoffDestinationBoxes(boxList);
            int remDrop = BaseSortingSystem.InspectRemainingDropoffItems(boxList, out _);
            if (catDest.Count != 1 || catDest[0].ContainerId != "v302_hybrid_med")
                throw new Exception("Hybrid [Drop-off + Medicine] chest was excluded from GetCategorizedDestinationBoxes.");
            if (pureDest.Count != 1 || pureDest[0].ContainerId != "v302_pure_drop")
                throw new Exception("GetPureDropoffDestinationBoxes failed to isolate pure [Drop-off] chest.");
            if (remDrop != 0)
                throw new Exception("Hybrid [Drop-off + Medicine] chest reported its own Medicine item as unplaced Drop-off remainder.");

            UiSmokePlugin.Logger.LogInfo("[InventoryRegression] PASS v3.0.2 stack merge + top-up, conservation, Coconut(230), hybrid Drop-off routing & rations.");
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

            if (stack >= 2)
            {
                // v3.0.2: 1-piece stackable item merges cleanly into occupied 1-slot destination (0 free cells!)
                var srcOne = MakeInventory(width * 2, height * 2);
                var dstOne = MakeInventory(width, height);
                var onePiece = MakeItem(itemId, 1f);
                AddFixture(srcOne, onePiece);
                AddFixture(dstOne, MakeItem(itemId, 1f));
                if (!InventoryTransfers.TryMoveWhole(srcOne, dstOne, onePiece) ||
                    Total(srcOne) != 0f || Mathf.Abs(Total(dstOne) - 2f) > 0.001f || dstOne.itemList.Count != 1)
                {
                    throw new Exception($"v3.0.2 full stack merge into occupied slot failed for item {itemId} (dstTotal={Total(dstOne)}, count={dstOne.itemList.Count}).");
                }

                // v3.0.2: Partial stack top-up + remainder into free slot preserves exact total & compacts stacks
                var srcTwo = MakeInventory(width * 2, height * 2);
                var dstTop = MakeInventory(width * 2, height);
                var twoPieces = MakeItem(itemId, 2f);
                AddFixture(srcTwo, twoPieces);
                AddFixture(dstTop, MakeItem(itemId, stack - 1));
                if (!InventoryTransfers.TryMoveWhole(srcTwo, dstTop, twoPieces) ||
                    Total(srcTwo) != 0f || Mathf.Abs(Total(dstTop) - (stack + 1)) > 0.001f || dstTop.itemList.Count != 2)
                {
                    throw new Exception($"v3.0.2 partial stack top-up + remainder failed for item {itemId} (dstTotal={Total(dstTop)}, count={dstTop.itemList.Count}).");
                }
            }

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
