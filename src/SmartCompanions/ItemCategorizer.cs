using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class ItemCategorizer
    {
        public static readonly StorageCategory[] AllCategories = new[]
        {
            StorageCategory.MeleeWeapons,
            StorageCategory.RangedWeapons,
            StorageCategory.AmmoAndMags,
            StorageCategory.Medicine,
            StorageCategory.FreshFood,
            StorageCategory.CannedAndDrinks,
            StorageCategory.Materials,
            StorageCategory.ToolsAndRepair,
            StorageCategory.ClothingAndArmor,
            StorageCategory.VehicleAndPower,
            StorageCategory.FarmingAndPets,
            StorageCategory.MiscAndValuables
        };

        private static readonly HashSet<int> SteakAndMeatIds = new HashSet<int>
        {
            240, // Dry Meat
            241, // Fried Meat
            248, // Spicy Meat Steak
            268, // Rabbit and Radish Stew
            289, // Grilled Salmon Steak
            291, // Seared Tuna Steak
            296, // Salt-Roasted Chicken
            297, // Chicken and Mushroom Soup
            298  // Spicy Chicken and Potatoes
        };

        private static readonly HashSet<int> RawOrBadFoodIds = new HashSet<int>
        {
            204, // Raw Meat
            205, // Rotten Meat
            260, 261, 262, 263, 264, 265, 266 // Dead rat/crow/fish/seagull/rabbit/chicken
        };

        private static readonly HashSet<int> CannedIds = new HashSet<int>
        {
            210, // SPUM
            211, // Canned Fruit
            213, // Homemade Meat Can
            214, // Homemade Fruit Can
            217, 218, 219, 225, 226, 227, 228, 229, 232, 233, 246
        };

        private static readonly HashSet<int> MedicalIds = new HashSet<int>
        {
            220, // Bandage
            221, // First Aid Kit
            222, // Painkiller
            223, // Blood Bag
            224, // Melatonin
            234, // Fever Reducer
            249, // Spore Suppressant
            250, 251, 252, // Red/Green/Blue Dose
            270, 271, 272, 273, 274, 275, // Potions & Enhancement Serum
            276, // Iodine Tablets
            277  // Anti-Radiation Medicine
        };

        private static readonly HashSet<int> DrinkIds = new HashSet<int>
        {
            200, // Coco-Cola
            206, // Fenda
            207, // Sqrite
            230  // Coconut
        };

        private static readonly HashSet<int> AlcoholIds = new HashSet<int>
        {
            212 // Lager
        };

        public static string GetCategoryName(StorageCategory cat, bool ru)
        {
            return cat switch
            {
                StorageCategory.MeleeWeapons     => ru ? "Холодное оружие" : "Melee Weapons",
                StorageCategory.RangedWeapons    => ru ? "Огнестрел и обвесы" : "Firearms & Parts",
                StorageCategory.AmmoAndMags      => ru ? "Патроны и магазины" : "Ammo & Magazines",
                StorageCategory.Medicine         => ru ? "Медицина и аптечки" : "Medicine & First Aid",
                StorageCategory.FreshFood        => ru ? "Еда, мясо и кулинария" : "Fresh Food & Meals",
                StorageCategory.CannedAndDrinks  => ru ? "Консервы и напитки" : "Canned Food & Drinks",
                StorageCategory.Materials        => ru ? "Материалы и крафт" : "Crafting Materials",
                StorageCategory.ToolsAndRepair   => ru ? "Инструменты и ремонт" : "Tools & Repair",
                StorageCategory.ClothingAndArmor => ru ? "Одежда, броня, сумки" : "Clothing & Backpacks",
                StorageCategory.VehicleAndPower  => ru ? "Авто, батареи, топливо" : "Vehicle & Power",
                StorageCategory.FarmingAndPets   => ru ? "Фермерство и питомцы" : "Farming, Bait & Pets",
                StorageCategory.MiscAndValuables => ru ? "Взрывчатка, турели, разное" : "Explosives & Misc",
                _                                => ru ? "Не выбрано" : "None"
            };
        }

        public static string GetCategoryShortTag(StorageCategory cat, bool ru)
        {
            return cat switch
            {
                StorageCategory.MeleeWeapons     => ru ? "[Клинки]" : "[Melee]",
                StorageCategory.RangedWeapons    => ru ? "[Огнестрел]" : "[Guns]",
                StorageCategory.AmmoAndMags      => ru ? "[Патроны]" : "[Ammo]",
                StorageCategory.Medicine         => ru ? "[Медицина]" : "[Meds]",
                StorageCategory.FreshFood        => ru ? "[Свежая еда]" : "[Meals]",
                StorageCategory.CannedAndDrinks  => ru ? "[Консервы/Вода]" : "[Cans/Drink]",
                StorageCategory.Materials        => ru ? "[Материалы]" : "[Mats]",
                StorageCategory.ToolsAndRepair   => ru ? "[Инструменты]" : "[Tools]",
                StorageCategory.ClothingAndArmor => ru ? "[Одежда/Броня]" : "[Gear]",
                StorageCategory.VehicleAndPower  => ru ? "[Авто/Энергия]" : "[Auto/Pwr]",
                StorageCategory.FarmingAndPets   => ru ? "[Огород/Питомцы]" : "[Farm/Pet]",
                StorageCategory.MiscAndValuables => ru ? "[Разное]" : "[Misc]",
                _                                => ""
            };
        }

        public static string FormatCategoriesSummary(StorageCategory mask, bool isDropoff, bool ru)
        {
            if (isDropoff && mask == StorageCategory.None)
                return ru ? "📥 Ящик-приёмник (разбор хабара)" : "📥 Drop-off Box (Unsorted Source)";
            if (mask == StorageCategory.None)
                return ru ? "Нет категорий" : "No Categories";
            if (mask == StorageCategory.All)
                return ru ? "Все категории (Общий склад)" : "All Categories (General)";

            var parts = new List<string>();
            if (isDropoff) parts.Add(ru ? "📥Приёмник" : "📥Drop-off");
            foreach (var c in AllCategories)
            {
                if ((mask & c) != 0)
                    parts.Add(GetCategoryShortTag(c, ru));
            }
            return string.Join(" ", parts);
        }

        public static StorageCategory ClassifyItem(ItemData itemData)
        {
            if (itemData == null) return StorageCategory.MiscAndValuables;
            int id = itemData.itemId;
            ItemAttr attr = null;
            try
            {
                if (ItemManager.instance != null)
                    attr = ItemManager.instance.GetItemAttrById(id);
            }
            catch { }

            return ClassifyItem(id, attr, itemData);
        }

        public static StorageCategory ClassifyItem(int itemId, ItemAttr attr, ItemData itemData = null)
        {
            if (MedicalIds.Contains(itemId))
                return StorageCategory.Medicine;

            if (attr != null)
            {
                try
                {
                    if (HasFeature(attr, ItemFeatureType.Bandage))
                        return StorageCategory.Medicine;

                    if (HasFeature(attr, ItemFeatureType.VehicleBattery) ||
                        HasFeature(attr, ItemFeatureType.VehicleHeadLight) ||
                        HasFeature(attr, ItemFeatureType.VehicleTire) ||
                        HasFeature(attr, ItemFeatureType.TirePump) ||
                        HasFeature(attr, ItemFeatureType.Battery) ||
                        HasFeature(attr, ItemFeatureType.BatteryBox) ||
                        HasFeature(attr, ItemFeatureType.PortableCharger) ||
                        HasFeature(attr, ItemFeatureType.SolarCharger))
                    {
                        return StorageCategory.VehicleAndPower;
                    }

                    if (HasFeature(attr, ItemFeatureType.Crop) ||
                        HasFeature(attr, ItemFeatureType.Fertilizer) ||
                        HasFeature(attr, ItemFeatureType.FishBait) ||
                        HasFeature(attr, ItemFeatureType.PetCollar) ||
                        HasFeature(attr, ItemFeatureType.PetVest) ||
                        HasFeature(attr, ItemFeatureType.PetPouch) ||
                        HasFeature(attr, ItemFeatureType.PetFood))
                    {
                        return StorageCategory.FarmingAndPets;
                    }

                    if (HasFeature(attr, ItemFeatureType.BreakDoor) ||
                        HasFeature(attr, ItemFeatureType.PickLock) ||
                        HasFeature(attr, ItemFeatureType.SlaughterTool) ||
                        HasFeature(attr, ItemFeatureType.LightingTool) ||
                        HasFeature(attr, ItemFeatureType.SprayPaint) ||
                        HasFeature(attr, ItemFeatureType.Decoder) ||
                        HasFeature(attr, ItemFeatureType.Talkie))
                    {
                        if (attr.itemType != ItemType.MeleeWeapon || itemId == 310 || itemId == 311 || itemId == 324 || itemId == 360 || itemId == 361 || itemId == 370)
                            return StorageCategory.ToolsAndRepair;
                    }
                }
                catch { }

                var itemType = attr.itemType;
                switch (itemType)
                {
                    case ItemType.MeleeWeapon:
                        // Repair hammer, wrench, fishing rod, shovel -> Tools
                        if (itemId == 310 || itemId == 311 || itemId == 324 || itemId == 360 || itemId == 361 || itemId == 370)
                            return StorageCategory.ToolsAndRepair;
                        return StorageCategory.MeleeWeapons;

                    case ItemType.RangedWeapon:
                    case ItemType.GunPart:
                        return StorageCategory.RangedWeapons;

                    case ItemType.Ammo:
                    case ItemType.Magazine:
                    case ItemType.Arrow:
                        return StorageCategory.AmmoAndMags;

                    case ItemType.Clothing:
                    case ItemType.FaceMask:
                    case ItemType.Backpack:
                    case ItemType.Cloak:
                        return StorageCategory.ClothingAndArmor;

                    case ItemType.Food:
                        var fk = ClassifyFoodKind(itemId, attr, itemData);
                        if (fk == FoodKind.MedicineBandage || fk == FoodKind.MedicinePills)
                            return StorageCategory.Medicine;
                        if (fk == FoodKind.CannedFood || fk == FoodKind.DrinkAndSoda || fk == FoodKind.Alcohol)
                            return StorageCategory.CannedAndDrinks;
                        return StorageCategory.FreshFood;

                    case ItemType.Liqiud:
                    case ItemType.Container:
                        var enName = SafeLower(attr.ItemName_EN) + " " + SafeLower(attr.itemName);
                        if (enName.Contains("gas") || enName.Contains("fuel") || enName.Contains("oil") || enName.Contains("бенз") || enName.Contains("топл"))
                            return StorageCategory.VehicleAndPower;
                        if (enName.Contains("water") || enName.Contains("bottle") || enName.Contains("kettle") || enName.Contains("вод"))
                            return StorageCategory.CannedAndDrinks;
                        return StorageCategory.ToolsAndRepair;

                    case ItemType.Material:
                        return StorageCategory.Materials;

                    case ItemType.Throwable:
                    case ItemType.Deployable:
                        return StorageCategory.MiscAndValuables;
                }
            }

            if (CannedIds.Contains(itemId) || DrinkIds.Contains(itemId) || AlcoholIds.Contains(itemId))
                return StorageCategory.CannedAndDrinks;
            if (SteakAndMeatIds.Contains(itemId) || RawOrBadFoodIds.Contains(itemId))
                return StorageCategory.FreshFood;

            return StorageCategory.MiscAndValuables;
        }

        public static FoodKind ClassifyFoodKind(ItemData itemData)
        {
            if (itemData == null) return FoodKind.None;
            ItemAttr attr = null;
            try
            {
                if (ItemManager.instance != null)
                    attr = ItemManager.instance.GetItemAttrById(itemData.itemId);
            }
            catch { }
            return ClassifyFoodKind(itemData.itemId, attr, itemData);
        }

        public static FoodKind ClassifyFoodKind(int itemId, ItemAttr attr, ItemData itemData = null)
        {
            if (itemId == 220 || itemId == 221 || itemId == 223)
                return FoodKind.MedicineBandage;
            if (MedicalIds.Contains(itemId))
                return FoodKind.MedicinePills;

            var foodAttr = attr != null ? attr.TryCast<ItemAttr_Food>() : null;
            if (foodAttr != null)
            {
                if (foodAttr.stopBleeding || foodAttr.healAllInjuries || foodAttr.bloodVolumeRecover > 0.1f)
                    return FoodKind.MedicineBandage;
                if (foodAttr.hp > 5f && foodAttr.hunger <= 1f && foodAttr.thirst <= 1f)
                    return FoodKind.MedicinePills;
            }

            var name = SafeLower(attr?.ItemName_EN) + " " + SafeLower(attr?.itemName) + " " + SafeLower(itemData?.GetItemName());

            // Check if raw meat was cooked on BBQ (Zed Zone uses itemId 204 with cooked property or Fried Meat 241 / Steak 248)
            bool isCookedProperty = false;
            if (itemData != null)
            {
                try
                {
                    if (ItemData.IsHotMeal(itemData, attr)) isCookedProperty = true;
                    else if (ItemManager.instance != null && ItemManager.instance.GetCookStateBucket(itemData) > 0)
                        isCookedProperty = true;
                }
                catch { }
            }

            if (SteakAndMeatIds.Contains(itemId) || (itemId == 204 && isCookedProperty) ||
                name.Contains("steak") || name.Contains("стейк") || name.Contains("fried meat") ||
                name.Contains("жареное мясо") || name.Contains("roast") || name.Contains("запеч"))
            {
                return FoodKind.SteakAndRoastMeat;
            }

            if (itemId == 205 || name.Contains("rotten") || name.Contains("гнил"))
                return FoodKind.RottenFood;

            if (RawOrBadFoodIds.Contains(itemId) && !isCookedProperty)
                return FoodKind.RawMeat;

            if (foodAttr != null && foodAttr.badStomach && !isCookedProperty)
                return FoodKind.RawMeat;

            if (AlcoholIds.Contains(itemId) || name.Contains("lager") || name.Contains("beer") || name.Contains("wine") || name.Contains("пиво") || name.Contains("лагер"))
                return FoodKind.Alcohol;

            if (DrinkIds.Contains(itemId) || name.Contains("cola") || name.Contains("fenda") || name.Contains("sqrite") || name.Contains("water") || name.Contains("вода") || name.Contains("кола"))
                return FoodKind.DrinkAndSoda;

            if (CannedIds.Contains(itemId) || name.Contains("canned") || name.Contains("can ") || name.Contains("spum") || name.Contains("консерв") || name.Contains("тушёнк") || name.Contains("спум"))
                return FoodKind.CannedFood;

            if ((foodAttr != null && foodAttr.isFish) || (itemId >= 279 && itemId <= 293) || name.Contains("fish") || name.Contains("salmon") || name.Contains("tuna") || name.Contains("carp") || name.Contains("trout") || name.Contains("рыб") || name.Contains("лосос") || name.Contains("тунец") || name.Contains("окун") || name.Contains("карась"))
                return FoodKind.FishDish;

            if (itemId == 209 || itemId == 215 || itemId == 216 || itemId == 269 || itemId == 295 || name.Contains("gingerbread") || name.Contains("pie") || name.Contains("jam") || name.Contains("пряник") || name.Contains("пирог") || name.Contains("джем"))
                return FoodKind.SweetAndSnack;

            if (foodAttr != null && foodAttr.thirst > foodAttr.hunger * 1.5f && foodAttr.hunger < 10f)
                return FoodKind.DrinkAndSoda;

            if (foodAttr != null && foodAttr.hunger >= 25f)
                return FoodKind.CookedMeal;

            return FoodKind.OtherFood;
        }

        private static bool HasFeature(ItemAttr attr, ItemFeatureType ft)
        {
            try
            {
                if (attr.itemFeatures != null && attr.itemFeatures.Contains(ft))
                    return true;
                return attr.GetItemFeatureByFeatureType(ft) != null;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeLower(string s) => string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant();
    }
}
