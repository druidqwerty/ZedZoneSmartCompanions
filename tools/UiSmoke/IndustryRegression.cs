using System;
using System.Collections.Generic;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class IndustryRegression
    {
        public static void Run()
        {
            // 1. Approved solid & liquid station fuels (EN / RU / ZH)
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(101, StorageCategory.Materials, "dry firewood bundle"),
                "Firewood in Materials must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(102, StorageCategory.Materials, "дрова березовые"),
                "Russian firewood in Materials must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(103, StorageCategory.Materials, "charcoal briquette"),
                "Charcoal briquette in Materials must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(104, StorageCategory.Materials, "каменный уголь"),
                "Russian coal in Materials must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(105, StorageCategory.VehicleAndPower, "jerrycan of gasoline"),
                "Gasoline jerrycan in VehicleAndPower must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(106, StorageCategory.VehicleAndPower, "канистра горючего"),
                "Russian fuel canister in VehicleAndPower must be approved as station fuel");
            AssertTrue(
                BaseIndustrySystem.IsApprovedStationFuelItem(107, StorageCategory.VehicleAndPower, "汽油桶"),
                "Chinese gasoline canister in VehicleAndPower must be approved as station fuel");

            // 1b. Native Combustible feature gate (e.g. vanilla Charcoal without ItemFeatureType.Combustible must be rejected!)
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(103, StorageCategory.Materials, "charcoal", hasCombustibleFeature: false),
                "Charcoal without native ItemFeatureType.Combustible must never be approved as station fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(101, StorageCategory.Materials, "dry firewood bundle", hasCombustibleFeature: false),
                "Any item lacking native ItemFeatureType.Combustible must be rejected");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(105, StorageCategory.VehicleAndPower, "jerrycan of gasoline", hasCombustibleFeature: false),
                "Liquid gasoline lacking native ItemFeatureType.Combustible must not be put into solid-fuel furnaces/generators");

            // 1c. Battery charge thresholds & hysteresis (RemainingBattery Wh vs capacity Wh, never durability!)
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(0f, 100f), "0/100 Wh battery must not be considered full");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeDepleted(0f, 100f), "0/100 Wh battery must be considered depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(50f, 100f), "50/100 Wh battery must not be considered full");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeDepleted(50f, 100f), "50/100 Wh battery must be considered depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(84.9f, 100f), "84.9/100 Wh battery must not be considered full");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeDepleted(84.9f, 100f), "84.9/100 Wh battery must be considered depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(85.0f, 100f), "85/100 Wh battery must not be considered full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(85.0f, 100f), "85/100 Wh battery must respect hysteresis and not be depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(95.0f, 100f), "95/100 Wh partially charged battery must stay in charger");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(95.0f, 100f), "95/100 Wh battery must not be pulled from chest");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeFull(99.95f, 100f), "99.95/100 Wh battery must be considered full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(99.95f, 100f), "99.95/100 Wh battery must never be depleted");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeFull(100f, 100f), "100/100 Wh battery must be considered full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(100f, 100f), "100/100 Wh battery must never be depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(1.58f, 1.6f), "1.58/1.6 Wh AAA battery must stay charging until full");
            AssertTrue(BaseIndustrySystem.IsBatteryChargeFull(1.5995f, 1.6f), "1.5995/1.6 Wh AAA battery must be considered full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(0f, 0f), "Zero-capacity battery must never be considered full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(0f, 0f), "Zero-capacity battery must never be considered depleted");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeFull(float.NaN, 100f), "NaN remaining must not be full");
            AssertFalse(BaseIndustrySystem.IsBatteryChargeDepleted(float.NaN, 100f), "NaN remaining must not be depleted");

            // 1d. Live ItemManager verification of real game items (Charcoal, Combustibles, Batteries, Vehicle Batteries)
            VerifyLiveGameItemCatalog();

            // 2. Strict rejection of weapons, armor, medicine, food, books, money, planks, batteries & rare parts
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(0, StorageCategory.Materials, "coal"),
                "Invalid itemId <= 0 must never be approved");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(101, StorageCategory.Materials, "   "),
                "Blank item name must never be approved");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(301, StorageCategory.MeleeWeapons, "firewood splitting axe"),
                "Melee weapons must never be burned as fuel even if name mentions firewood");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(302, StorageCategory.RangedWeapons, "gasoline-washed rifle"),
                "Ranged weapons must never be burned as fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(303, StorageCategory.ClothingAndArmor, "charcoal tactical vest"),
                "Armor and clothing must never be burned as fuel even if name mentions charcoal");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(220, StorageCategory.Medicine, "activated charcoal tablets"),
                "Medicine must never be burned as fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(220, StorageCategory.Materials, "уголь активированный в таблетках"),
                "Activated charcoal pills must be blocked even if miscategorized as Materials");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(249, StorageCategory.Materials, "coal"),
                "Rare serum IDs (249) must never pass fuel whitelist");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(248, StorageCategory.FreshFood, "charcoal grilled steak"),
                "Food items must never be burned as fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(215, StorageCategory.Materials, "sunflower cooking oil"),
                "Cooking oil must never be treated as generator/furnace fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(401, StorageCategory.MiscAndValuables, "pre-war dollar cash"),
                "Money and valuables must never be burned as fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(402, StorageCategory.Materials, "пачка банкнот и деньги"),
                "Banknotes/cash must be blocked even if passed under Materials");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(403, StorageCategory.Materials, "skill book: coal smelting manual"),
                "Skill books and manuals must never be burned even if title mentions coal");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(404, StorageCategory.VehicleAndPower, "rare fuel injector prototype"),
                "Rare vehicle parts (fuel injector/pump) must never be mistaken for fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(405, StorageCategory.VehicleAndPower, "empty jerrycan"),
                "Empty fuel canisters must never be selected as fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(406, StorageCategory.VehicleAndPower, "car battery 12v"),
                "Batteries must never be burned as combustible fuel");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(407, StorageCategory.Materials, "wood plank"),
                "Crafting wood planks must not be burned in place of firewood");
            AssertFalse(
                BaseIndustrySystem.IsApprovedStationFuelItem(71, StorageCategory.Materials, "wood dagger", hasCombustibleFeature: true),
                "Wood Dagger (id=71, Material + Combustible) must never be burned as fuel");

            // 3. SelectNextIndustryTask: null/empty & disabled category gates
            AssertEqual(
                ("", IndustryTaskType.None),
                BaseIndustrySystem.SelectNextIndustryTask(null, allowPowerGrid: true, allowFurnaces: true),
                "Null station list must return None");
            AssertEqual(
                ("", IndustryTaskType.None),
                BaseIndustrySystem.SelectNextIndustryTask(
                    Array.Empty<(string, string, float, int, float)>(), allowPowerGrid: true, allowFurnaces: true),
                "Empty station list must return None");

            var mixedStations = new (string id, string kind, float fuelRatio, int outputItems, float dist)[]
            {
                ("gen_1", "StirlingGenerator", 0.10f, 0, 8.0f),
                ("furn_1", "Furnace", 0.50f, 3, 6.0f),
                ("stove_1", "Stove", 0.20f, 1, 4.0f)
            };

            // 4. Ready output unloading takes priority so station output slots never clog
            AssertEqual(
                ("furn_1", IndustryTaskType.UnloadOutput),
                BaseIndustrySystem.SelectNextIndustryTask(mixedStations, allowPowerGrid: true, allowFurnaces: true),
                "Furnace with 3 ready output items must be unloaded first");

            // 5. Disabling furnaces routes companion strictly to PowerGrid refuel task
            AssertEqual(
                ("gen_1", IndustryTaskType.RefuelStation),
                BaseIndustrySystem.SelectNextIndustryTask(mixedStations, allowPowerGrid: true, allowFurnaces: false),
                "When allowFurnaces is false, StirlingGenerator low on fuel must be selected");

            // 6. Disabling both automation toggles yields None
            AssertEqual(
                ("", IndustryTaskType.None),
                BaseIndustrySystem.SelectNextIndustryTask(mixedStations, allowPowerGrid: false, allowFurnaces: false),
                "When both allowPowerGrid and allowFurnaces are false, task must be None");

            // 7. Refuel priority: critically low power generator beats furnace at similar fuel ratio, and >= 0.35f threshold is ignored
            var refuelCandidates = new (string id, string kind, float fuelRatio, int outputItems, float dist)[]
            {
                ("furn_ok", "Furnace", 0.35f, 0, 2.0f),
                ("furn_low", "Furnace", 0.14f, 0, 3.0f),
                ("gen_low", "StirlingGenerator", 0.15f, 0, 9.0f),
                ("elec_furn", "ElectricFurnace", -1.0f, 0, 1.5f)
            };
            AssertEqual(
                ("gen_low", IndustryTaskType.RefuelStation),
                BaseIndustrySystem.SelectNextIndustryTask(refuelCandidates, allowPowerGrid: true, allowFurnaces: true),
                "PowerGrid station within 0.05 fuel ratio band must take priority over furnace; 0.35f & -1f must be ignored");

            // 8. Zero-teleport physical reach constant & work-role eligibility checks
            AssertEqual(
                PhysicalMovementHelper.MaxReachableInteractRadius,
                BaseIndustrySystem.StationInteractRadius,
                "StationInteractRadius must match PhysicalMovementHelper.MaxReachableInteractRadius (2.45m)");
            AssertTrue(
                BaseIndustrySystem.StationInteractRadius <= 2.45f,
                "StationInteractRadius must be <= 2.45m for zero-teleport hand interaction");

            var roleProbe = new CompanionRuntimeState();
            roleProbe.WorkRole = CompanionWorkRole.Any;
            AssertTrue(BaseIndustrySystem.AllowsIndustryRole(roleProbe), "Role Any must allow industry work");
            roleProbe.WorkRole = CompanionWorkRole.Camp;
            AssertTrue(BaseIndustrySystem.AllowsIndustryRole(roleProbe), "Role Camp must allow industry work");
            roleProbe.WorkRole = CompanionWorkRole.Sorter;
            AssertTrue(BaseIndustrySystem.AllowsIndustryRole(roleProbe), "Role Sorter must allow industry work");
            roleProbe.WorkRole = CompanionWorkRole.Scavenger;
            AssertFalse(BaseIndustrySystem.AllowsIndustryRole(roleProbe), "Role Scavenger must not be pulled into station chores");
            roleProbe.WorkRole = CompanionWorkRole.Off;
            AssertFalse(BaseIndustrySystem.AllowsIndustryRole(roleProbe), "Role Off must never auto-run industry tasks");

            const string passMsg = "[UiSmoke] PASS v2.7.0+ industrial fuel whitelist, battery charge state, station priority & zero-teleport routing";
            UiSmokePlugin.Logger?.LogInfo(passMsg);
            Console.WriteLine(passMsg);
        }

        private static void VerifyLiveGameItemCatalog()
        {
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im))
            {
                UiSmokePlugin.Logger?.LogWarning("[IndustryRegression] ItemManager.instance unavailable in MainMenu; live item catalog checks deferred to LiveSmoke.");
                return;
            }

            int combustibleCount = 0;
            int approvedFuelCount = 0;
            int charcoalChecked = 0;
            int batteryChecked = 0;
            int vehicleBatteryChecked = 0;

            for (int id = 1; id <= 2500; id++)
            {
                var attr = im.GetItemAttrById(id);
                if (!Il2CppSafe.IsAliveUnity(attr)) continue;

                bool hasCombustible = attr.itemFeatures != null && attr.itemFeatures.Contains(ItemFeatureType.Combustible);
                bool hasAsh = attr.itemFeatures != null && attr.itemFeatures.Contains(ItemFeatureType.Ash);
                bool hasBattery = attr.itemFeatures != null && attr.itemFeatures.Contains(ItemFeatureType.Battery);
                bool hasVehBattery = attr.itemFeatures != null && attr.itemFeatures.Contains(ItemFeatureType.VehicleBattery);
                string en = attr.ItemName_EN ?? "";
                string low = en.ToLowerInvariant();

                if (hasCombustible || hasAsh || hasBattery || hasVehBattery || low.Contains("charcoal") || low.Contains("coal") || low.Contains("firewood") || low.Contains("battery"))
                {
                    float cap = 0f;
                    if (hasBattery)
                    {
                        var bf = attr.GetItemFeatureByFeatureType(ItemFeatureType.Battery)?.TryCast<ItemFeature_Battery>();
                        if (Il2CppSafe.IsAliveUnity(bf)) cap = bf.batteryCapacityFloat;
                    }
                    UiSmokePlugin.Logger?.LogInfo(
                        $"[IndustryCatalog] id={id} EN='{en}' type={attr.itemType} comb={hasCombustible} ash={hasAsh} bat={hasBattery} vehBat={hasVehBattery} capWh={cap:F1}");
                }

                if (hasCombustible)
                {
                    combustibleCount++;
                    var sample = im.GetRandomDataByAttr(attr, false, true);
                    if (Il2CppSafe.IsAlive(sample) && BaseIndustrySystem.IsStationFuelData(sample))
                        approvedFuelCount++;
                }

                if (low.Contains("charcoal") && !hasCombustible)
                {
                    charcoalChecked++;
                    var ch = im.GetRandomDataByAttr(attr, false, true);
                    AssertFalse(BaseIndustrySystem.IsNativeCombustibleData(ch), $"Non-combustible charcoal id={id} ('{en}') must not report IsNativeCombustibleData");
                    AssertFalse(BaseIndustrySystem.IsStationFuelData(ch), $"Non-combustible charcoal id={id} ('{en}') must never be accepted as station fuel");
                    if (hasAsh)
                        AssertTrue(BaseIndustrySystem.IsNonCombustibleAshData(ch), $"Charcoal id={id} ('{en}') with Ash && !Combustible must report IsNonCombustibleAshData");
                }

                if (id == 71)
                {
                    var woodDagger = im.GetRandomDataByAttr(attr, false, true);
                    AssertFalse(BaseIndustrySystem.IsStationFuelData(woodDagger), $"Wood Dagger id=71 ('{en}') must never be selected as station fuel");
                }

                if (hasBattery)
                {
                    var bf = attr.GetItemFeatureByFeatureType(ItemFeatureType.Battery)?.TryCast<ItemFeature_Battery>();
                    if (Il2CppSafe.IsAliveUnity(bf) && bf.batteryCapacityFloat > 0.01f)
                    {
                        batteryChecked++;
                        float cap = bf.batteryCapacityFloat;

                        // Case A: durability == 100f (brand new casing) but only 30% charged -> MUST NOT unload from charger, MUST count as depleted
                        var partialBat = im.GetRandomDataByAttr(attr, false, true);
                        partialBat.durability = 100f;
                        partialBat.SetProperty("RemainingBattery", (cap * 0.30f).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        AssertFalse(BaseIndustrySystem.IsBatteryFullyChargedData(partialBat),
                            $"30% charged battery id={id} ('{en}') with durability=100 must NOT be considered fully charged");
                        AssertTrue(BaseIndustrySystem.IsDepletedBatteryData(partialBat),
                            $"30% charged battery id={id} ('{en}') with durability=100 must be considered depleted");

                        // Case B: durability == 35f (worn vehicle/regular battery) but 100% charged -> MUST NOT be pulled into charger!
                        var wornFullBat = im.GetRandomDataByAttr(attr, false, true);
                        wornFullBat.durability = 35f;
                        wornFullBat.SetProperty("RemainingBattery", cap.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        AssertTrue(BaseIndustrySystem.IsBatteryFullyChargedData(wornFullBat),
                            $"100% charged battery id={id} ('{en}') with durability=35 must be considered fully charged");
                        AssertFalse(BaseIndustrySystem.IsDepletedBatteryData(wornFullBat),
                            $"100% charged battery id={id} ('{en}') with durability=35 must NOT be considered depleted");

                        // Case C: 90% charged (hysteresis band) -> neither full nor depleted
                        var ninetyBat = im.GetRandomDataByAttr(attr, false, true);
                        ninetyBat.durability = 100f;
                        ninetyBat.SetProperty("RemainingBattery", (cap * 0.90f).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        AssertFalse(BaseIndustrySystem.IsBatteryFullyChargedData(ninetyBat),
                            $"90% charged battery id={id} ('{en}') must stay in charger until full");
                        AssertFalse(BaseIndustrySystem.IsDepletedBatteryData(ninetyBat),
                            $"90% charged battery id={id} ('{en}') must not be pulled from storage");

                        if (hasVehBattery) vehicleBatteryChecked++;
                    }
                }
                else if (hasVehBattery)
                {
                    vehicleBatteryChecked++;
                    var vb = im.GetRandomDataByAttr(attr, false, true);
                    vb.durability = 35f;
                    AssertFalse(BaseIndustrySystem.IsDepletedBatteryData(vb),
                        $"Non-Battery-feature vehicle battery id={id} ('{en}') must never be treated as chargeable");
                    AssertFalse(BaseIndustrySystem.IsBatteryFullyChargedData(vb),
                        $"Non-Battery-feature vehicle battery id={id} ('{en}') must never report fully charged");
                }
            }

            AssertTrue(combustibleCount > 0, "Expected at least one native Combustible item in ItemManager");
            AssertTrue(approvedFuelCount > 0, "Expected at least one approved Combustible station fuel item in ItemManager");
            AssertTrue(batteryChecked > 0, "Expected at least one chargeable Battery item in ItemManager");
            UiSmokePlugin.Logger?.LogInfo(
                $"[IndustryRegression] PASS live ItemManager catalog: combustibles={combustibleCount}, approvedFuels={approvedFuelCount}, nonCombustibleCharcoal={charcoalChecked}, batteries={batteryChecked}, vehicleBatteries={vehicleBatteryChecked}");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[IndustryRegression] ASSERT TRUE FAILED: " + message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException("[IndustryRegression] ASSERT FALSE FAILED: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[IndustryRegression] ASSERT EQUAL FAILED ({message}): expected '{expected}', got '{actual}'");
            }
        }
    }
}
