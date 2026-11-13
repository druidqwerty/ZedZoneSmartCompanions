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

            const string passMsg = "[UiSmoke] PASS v2.7.0 industrial fuel whitelist, station priority & zero-teleport routing";
            UiSmokePlugin.Logger?.LogInfo(passMsg);
            Console.WriteLine(passMsg);
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
