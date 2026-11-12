using System;
using System.Collections.Generic;

namespace ZedZone.SmartCompanions
{
    public static class RationSharing
    {
        public static bool TryConsumePlayerPortion()
        {
            var player = GameController.instance?.playerCharacter;
            if (!Il2CppSafe.IsAliveUnity(player) || !Il2CppSafe.IsAlive(player.characterData)) return false;
            var seen = new HashSet<IntPtr>();
            var main = player.characterData.inventoryData;
            if (Il2CppSafe.IsAlive(main))
            {
                seen.Add(main.Pointer);
                if (TryConsumePortion(main)) return true;
            }
            var carried = player.characterData.GetCarriedInventoryList();
            if (carried == null) return false;
            foreach (var inventory in carried)
                if (Il2CppSafe.IsAlive(inventory) && seen.Add(inventory.Pointer) && TryConsumePortion(inventory)) return true;
            return false;
        }

        public static bool TryConsumePortion(InventoryData inventory)
        {
            if (!Il2CppSafe.IsAlive(inventory) || inventory.itemList == null || ItemManager.instance == null) return false;
            // Pass 0: ordinary rations first. Pass 1: rare gourmet dishes only when AllowSpendRareFood is enabled.
            int passes = CompanionConfigStore.AllowSpendRareFood ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < inventory.itemList.Count; i++)
                {
                    var item = inventory.itemList[i];
                    if (!Il2CppSafe.IsAlive(item) || item.itemNumberFloat < 1f) continue;
                    var attr = ItemManager.instance.GetItemAttrById(item.itemId);
                    if (!Il2CppSafe.IsAlive(attr) || attr.TryCast<ItemAttr_Food>() == null || ItemData.IsFoodExpired(item, attr)) continue;
                    var kind = ItemCategorizer.ClassifyFoodKind(item.itemId, attr, item);
                    if (kind == FoodKind.None || kind == FoodKind.RawMeat || kind == FoodKind.RottenFood ||
                        kind == FoodKind.MedicineBandage || kind == FoodKind.MedicinePills) continue;
                    bool isRare = CompanionProfileSystem.IsRareFoodItem(item.itemId, kind);
                    if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;

                    // Consume this exact eligible stack, not another raw/expired stack sharing its ID.
                    var consumed = item.CopyItemData(null);
                    consumed.itemNumberFloat = 1f;
                    if (item.itemNumberFloat <= 1f)
                    {
                        if (!inventory.RemoveItem(item, true)) return false;
                    }
                    else
                    {
                        item.itemNumberFloat -= 1f;
                        inventory.NotifyDataChanged(true, item, false);
                    }
                    inventory.NotifyItemConsumed(consumed);
                    return true;
                }
            }
            return false;
        }
    }
}
