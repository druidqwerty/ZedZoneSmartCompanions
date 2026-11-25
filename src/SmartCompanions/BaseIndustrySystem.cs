using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public enum IndustryTaskType { None = 0, UnloadOutput = 1, RefuelStation = 2 }

    /// <summary>
    /// v2.7.0+ Industrial Base Automation: generators, battery chargers &amp; furnaces
    /// serviced via two-phase physical A* walks and CarryBuffer with strictly zero teleportation.
    /// </summary>
    public static class BaseIndustrySystem
    {
        public const float StationInteractRadius = PhysicalMovementHelper.MaxReachableInteractRadius; // 2.45m
        public const float RefuelFuelRatioThreshold = 0.35f;
        public const float DepletedBatteryChargeThreshold = 0.85f;
        public const float FullBatteryChargeRatioThreshold = 0.999f;
        public const float FullBatteryRemainingEpsilonWh = 0.001f;
        public static bool EnablePowerGridAutomation = true;
        public static bool EnableFurnaceAndKitchenAutomation = true;
        private static float _lastIndustryTickAt = -999f;

        private static readonly string[] BlockedFuelSubstrings =
        {
            "weapon", "rifle", "pistol", "shotgun", "smg", "gun", "sword", "knife", "dagger", "axe", "machete", "spear", "bow", "crossbow",
            "ammo", "bullet", "magazine", "shell", "grenade", "molotov", "dynamite", "explosive", "bomb", "mine", "gunpowder",
            "armor", "vest", "helmet", "jacket", "coat", "pants", "boots", "gloves", "backpack", "mask", "cloak", "shirt",
            "activated", "tablet", "pill", "capsule", "bandage", "medkit", "first aid", "serum", "syringe", "vaccine", "medicine",
            "iodine", "antibiotic", "painkiller", "steak", "meat", "soup", "stew", "bread", "canned", "fish", "salad", "snack",
            "pie", "cooking oil", "olive oil", "vegetable oil", "sunflower oil", "fish oil", "water", "soda", "cola", "juice",
            "beer", "wine", "vodka", "whiskey", "alcohol", "ration", "book", "manual", "guide", "blueprint", "schematic", "recipe",
            "journal", "diary", "note", "money", "cash", "dollar", "coin", "banknote", "gold", "silver", "diamond", "gem",
            "jewelry", "ring", "watch", "keycard", "pump", "injector", "filter", "carburetor", "alternator", "engine", "motor",
            "spark plug", "piston", "radiator", "battery", "accumulator", "charger", "solar", "circuit", "microchip", "chip",
            "processor", "motherboard", "sensor", "module", "turret", "plank", "plywood", "lumber", "furniture", "rare",
            "legendary", "unique", "artifact", "prototype", "quest", "empty",
            "оружи", "винтовк", "пистолет", "дробовик", "автомат", "нож", "кинжал", "топор", "мачете", "патрон", "магазин", "гранат",
            "молотов", "взрыв", "порох", "брон", "жилет", "шлем", "каск", "куртк", "плащ", "штаны", "ботинк", "перчатк", "рюкзак",
            "сумк", "маск", "одежд", "активирован", "таблет", "капсул", "бинт", "аптеч", "сыворотк", "шприц", "лекарств",
            "медицин", "антибиотик", "обезбол", "йод", "стейк", "мясо", "суп", "рагу", "хлеб", "консерв", "рыб", "пирог",
            "подсолнеч", "оливков", "пищев", "вода", "кола", "пиво", "вино", "водк", "спирт", "алкогол", "еда", "паек", "книг",
            "учебник", "руководств", "чертеж", "схем", "рецепт", "дневник", "записк", "деньг", "доллар", "купюр", "банкнот",
            "монет", "золот", "серебр", "кольц", "часы", "ключ", "карт", "насос", "инжектор", "фильтр", "карбюратор", "двигател",
            "мотор", "свеч", "поршн", "радиатор", "аккумулятор", "батаре", "зарядн", "солнечн", "микросхем", "плат", "процессор",
            "чип", "датчик", "модул", "турел", "доск", "фанер", "мебел", "редк", "уникал", "легендар", "артефакт", "прототип",
            "квест", "пуст", "步枪", "手枪", "匕首", "子弹", "炸药", "手雷", "护甲", "头盔", "外套", "背包", "面具", "绷带", "药片", "急救",
            "血清", "活性炭", "牛排", "罐头", "食用油", "啤酒", "书", "图纸", "配方", "钞票", "美元", "金币", "电池", "充电", "发动机",
            "油泵", "喷油", "滤清器", "芯片", "木板", "稀有", "传奇", "任务", "空桶", "空的"
        };

        private static readonly string[] ApprovedFuelSubstrings =
        {
            "firewood", "wood log", "wooden log", "split wood", "dry wood", "fuel wood", "kindling", "branch", "twig", "charcoal", "coal",
            "briquette", "peat", "gasoline", "petrol", "diesel", "kerosene", "biofuel", "fuel can", "fuel canister", "gas can",
            "gas canister", "jerrycan", "jerry can", "liquid fuel", "fuel barrel", "fuel drum", "fuel bottle", "generator fuel",
            "stove fuel", "дрова", "полен", "хворост", "ветк", "древесин", "уголь", "угля", "брикет", "торф", "бензин", "дизель", "дизтоплив",
            "солярк", "керосин", "биотоплив", "горючее", "горюч", "топливо", "канистра с бензин", "канистра бензин",
            "канистра топлив", "канистра с топлив", "канистра горюч", "топливная канистра",
            "柴火", "木柴", "原木", "木头", "木材", "树枝", "木炭", "煤炭", "煤块", "蜂窝煤", "汽油", "柴油", "煤油", "生物燃料", "燃油", "汽油桶", "燃料桶", "燃料罐"
        };

        public static bool AllowsIndustryRole(CompanionRuntimeState st) =>
            !BaseWorkManager.Paused && st != null &&
            st.WorkRole != CompanionWorkRole.Off &&
            (st.RosterMode == CompanionRosterMode.CampGarrison ||
             st.WorkRole == CompanionWorkRole.Any ||
             st.WorkRole == CompanionWorkRole.Camp ||
             st.WorkRole == CompanionWorkRole.Sorter);

        public static bool IsPowerGridStationKind(string kind) =>
            string.Equals(kind, "StirlingGenerator", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Heater", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "BatteryCharger", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "EnergyStorage", StringComparison.OrdinalIgnoreCase);

        public static bool IsFurnaceOrKitchenKind(string kind) =>
            string.Equals(kind, "Furnace", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "ElectricFurnace", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "Stove", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "CookingHob", StringComparison.OrdinalIgnoreCase);

        public static bool IsBatteryChargeFull(float remainingWh, float capacityWh)
        {
            if (float.IsNaN(remainingWh) || float.IsInfinity(remainingWh) ||
                float.IsNaN(capacityWh) || float.IsInfinity(capacityWh) ||
                capacityWh <= 0.0001f)
                return false;
            return remainingWh >= capacityWh - FullBatteryRemainingEpsilonWh ||
                   (remainingWh / capacityWh) >= FullBatteryChargeRatioThreshold;
        }

        public static bool IsBatteryChargeDepleted(float remainingWh, float capacityWh)
        {
            if (float.IsNaN(remainingWh) || float.IsInfinity(remainingWh) ||
                float.IsNaN(capacityWh) || float.IsInfinity(capacityWh) ||
                capacityWh <= 0.0001f)
                return false;
            if (IsBatteryChargeFull(remainingWh, capacityWh))
                return false;
            float clampedRem = Mathf.Max(0f, remainingWh);
            return (clampedRem / capacityWh) < DepletedBatteryChargeThreshold;
        }

        public static bool IsApprovedStationFuelItem(int itemId, StorageCategory cat, string itemNameLower) =>
            IsApprovedStationFuelItem(itemId, cat, itemNameLower, hasCombustibleFeature: true);

        public static bool IsApprovedStationFuelItem(int itemId, StorageCategory cat, string itemNameLower, bool hasCombustibleFeature)
        {
            if (!hasCombustibleFeature || itemId <= 0 || string.IsNullOrWhiteSpace(itemNameLower) || CompanionProfileSystem.IsRareMedItem(itemId))
                return false;
            if ((cat & ~(StorageCategory.Materials | StorageCategory.VehicleAndPower)) != 0) return false;
            string s = itemNameLower.Trim().ToLowerInvariant();
            for (int i = 0; i < BlockedFuelSubstrings.Length; i++)
                if (s.Contains(BlockedFuelSubstrings[i], StringComparison.Ordinal)) return false;
            for (int i = 0; i < ApprovedFuelSubstrings.Length; i++)
                if (s.Contains(ApprovedFuelSubstrings[i], StringComparison.Ordinal)) return true;
            return ContainsWord(s, "wood") || ContainsWord(s, "log") || ContainsWord(s, "logs") || ContainsWord(s, "fuel");
        }

        private static bool ContainsWord(string text, string word)
        {
            for (int idx = 0; (idx = text.IndexOf(word, idx, StringComparison.Ordinal)) >= 0; idx += word.Length)
            {
                int end = idx + word.Length;
                if ((idx == 0 || !char.IsLetterOrDigit(text[idx - 1])) && (end >= text.Length || !char.IsLetterOrDigit(text[end])))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasItemFeature(ItemAttr attr, ItemFeatureType ft)
        {
            if (!Il2CppSafe.IsAliveUnity(attr)) return false;
            try
            {
                if (attr.itemFeatures != null && attr.itemFeatures.Contains(ft))
                    return true;
                if (attr.itemFeatureDataDic != null && attr.itemFeatureDataDic.ContainsKey(ft))
                    return true;
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryGetBatteryChargeState(ItemData it, out float remainingWh, out float capacityWh)
        {
            remainingWh = 0f;
            capacityWh = 0f;
            if (!Il2CppSafe.IsAlive(it)) return false;
            try
            {
                var im = ItemManager.instance;
                if (!Il2CppSafe.IsAlive(im)) return false;
                var attr = im.GetItemAttrById(it.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr) || !HasItemFeature(attr, ItemFeatureType.Battery))
                    return false;
                var batFeat = attr.GetItemFeatureByFeatureType(ItemFeatureType.Battery)?.TryCast<ItemFeature_Battery>();
                if (!Il2CppSafe.IsAliveUnity(batFeat))
                    return false;
                float cap = batFeat.batteryCapacityFloat;
                if (float.IsNaN(cap) || float.IsInfinity(cap) || cap <= 0.0001f)
                    return false;
                float rem = ItemFeature_Battery.GetBatteryRemainingPower(it);
                if (float.IsNaN(rem) || float.IsInfinity(rem))
                    return false;
                capacityWh = cap;
                remainingWh = Mathf.Clamp(rem, 0f, cap);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsBatteryFullyChargedData(ItemData it) =>
            TryGetBatteryChargeState(it, out float rem, out float cap) && IsBatteryChargeFull(rem, cap);

        public static bool IsDepletedBatteryData(ItemData it) =>
            TryGetBatteryChargeState(it, out float rem, out float cap) && IsBatteryChargeDepleted(rem, cap);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsNativeCombustibleData(ItemData it)
        {
            if (!Il2CppSafe.IsAlive(it)) return false;
            try
            {
                var im = ItemManager.instance;
                if (!Il2CppSafe.IsAlive(im)) return false;
                var attr = im.GetItemAttrById(it.itemId);
                return Il2CppSafe.IsAliveUnity(attr) && HasItemFeature(attr, ItemFeatureType.Combustible);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsNonCombustibleAshData(ItemData it)
        {
            if (!Il2CppSafe.IsAlive(it)) return false;
            try
            {
                var im = ItemManager.instance;
                if (!Il2CppSafe.IsAlive(im)) return false;
                var attr = im.GetItemAttrById(it.itemId);
                return Il2CppSafe.IsAliveUnity(attr) &&
                       HasItemFeature(attr, ItemFeatureType.Ash) &&
                       !HasItemFeature(attr, ItemFeatureType.Combustible);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsStationFuelData(ItemData it, InventoryData targetFuelInv = null)
        {
            if (!Il2CppSafe.IsAlive(it)) return false;
            try
            {
                var im = ItemManager.instance;
                if (!Il2CppSafe.IsAlive(im)) return false;
                var attr = im.GetItemAttrById(it.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr)) return false;
                bool hasCombustible = HasItemFeature(attr, ItemFeatureType.Combustible);
                if (!hasCombustible) return false;
                if (Il2CppSafe.IsAlive(targetFuelInv) && !targetFuelInv.PassesFeatureLimit(attr))
                    return false;
                return IsApprovedStationFuelItem(it.itemId, ItemCategorizer.ClassifyItem(it), FullItemNameLower(it), hasCombustible);
            }
            catch
            {
                return false;
            }
        }

        public static (string stationId, IndustryTaskType taskType) SelectNextIndustryTask(
            IReadOnlyList<(string id, string kind, float fuelRatio, int outputItems, float dist)> stations,
            bool allowPowerGrid, bool allowFurnaces)
        {
            if (stations == null || stations.Count == 0) return ("", IndustryTaskType.None);
            string bestUnloadId = "", bestRefuelId = "";
            int bestOut = 0;
            float bestUnloadDist = float.MaxValue, bestFuel = RefuelFuelRatioThreshold, bestRefuelDist = float.MaxValue;
            bool bestRefuelPower = false;

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];
                if (string.IsNullOrWhiteSpace(st.id) || float.IsNaN(st.dist) || float.IsInfinity(st.dist) || st.dist < 0f) continue;
                bool isPower = IsPowerGridStationKind(st.kind), isFurn = IsFurnaceOrKitchenKind(st.kind);
                if ((!isPower && !isFurn) || (isPower && !allowPowerGrid) || (isFurn && !allowFurnaces)) continue;

                if (st.outputItems > 0 && (st.outputItems > bestOut || (st.outputItems == bestOut && st.dist < bestUnloadDist)))
                { bestOut = st.outputItems; bestUnloadDist = st.dist; bestUnloadId = st.id; }

                if (!float.IsNaN(st.fuelRatio) && !float.IsInfinity(st.fuelRatio) && st.fuelRatio >= 0f && st.fuelRatio < RefuelFuelRatioThreshold)
                {
                    bool better = string.IsNullOrEmpty(bestRefuelId) || st.fuelRatio < bestFuel - 0.05f ||
                                  (Mathf.Abs(st.fuelRatio - bestFuel) <= 0.05f &&
                                   ((isPower && !bestRefuelPower) || (isPower == bestRefuelPower && st.dist < bestRefuelDist)));
                    if (better) { bestFuel = st.fuelRatio; bestRefuelPower = isPower; bestRefuelDist = st.dist; bestRefuelId = st.id; }
                }
            }
            if (!string.IsNullOrEmpty(bestUnloadId)) return (bestUnloadId, IndustryTaskType.UnloadOutput);
            return !string.IsNullOrEmpty(bestRefuelId) ? (bestRefuelId, IndustryTaskType.RefuelStation) : ("", IndustryTaskType.None);
        }

        private struct StationEntry
        {
            public string Id, Kind; public TerrainObject_Production Prod; public ProductionData Data;
            public InventoryData FuelInv, OutInv; public ItemData OutItem; public float FuelRatio, Dist; public int OutputCount;
        }

        public static void TickBaseIndustry(float now)
        {
            if (!CompanionConfigStore.EnableBaseIndustry ||
                (!EnablePowerGridAutomation && !EnableFurnaceAndKitchenAutomation) ||
                !CompanionConfigStore.EnableBaseChores || BaseWorkManager.Paused ||
                MultiplayerSyncSystem.IsMultiplayerClient) return;
            if (now - _lastIndustryTickAt < 4.5f) return;
            _lastIndustryTickAt = now;
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || gc.isPlayerInCombat) return;

            GOAPAgent worker = null; CompanionRuntimeState workerSt = null;
            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag || ag.humanCharacter.isSleeping) continue;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData));
                if (!st.IsSortingActive && !st.IsChoreWalkActive && st.CarryBuffer.Count > 0)
                    BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                if (worker == null &&
                    st.AllowBaseChores &&
                    !st.HasAnyPhysicalTask && st.CarryBuffer.Count == 0 &&
                    now - st.LastChoreTime >= 12f &&
                    BaseWorkManager.MayStartAutomaticWork(st, AllowsIndustryRole) &&
                    CompanionSurvivalSystem.IsAtCampOrPlayerBase(ag.humanCharacter.transform.position, st))
                { worker = ag; workerSt = st; }
            }
            if (worker == null || !CompanionSurvivalSystem.IsAtCampOrPlayerBase(worker.humanCharacter.transform.position, workerSt)) return;

            Vector2 playerPos = gc.playerCharacter.transform.position, selfPos = worker.humanCharacter.transform.position;
            Vector2? homeAnchor = (workerSt.RosterMode == CompanionRosterMode.CampGarrison && workerSt.HasHomeAnchor)
                ? workerSt.HomeAnchorPos
                : (Vector2?)null;
            sbyte workerZ = worker.npcData.zLevel;
            float radius = homeAnchor.HasValue
                ? 28f
                : Mathf.Clamp(Mathf.Min(CompanionConfigStore.BaseScanRadius, 40f), 16f, 40f);
            var chests = BaseSortingSystem.FilterContainersForWorker(BaseSortingSystem.ScanNearbyContainers(force: false), worker, workerSt);
            var stations = new List<StationEntry>(); var seen = new HashSet<IntPtr>();
            BaseSortingSystem.InvalidateTerrainScanCache();
            var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(playerPos, radius);
            for (int i = 0; i < worldObjs.Count; i++)
                InspectStation(worldObjs[i]?.TryCast<TerrainObject_Production>(), playerPos, homeAnchor, selfPos, workerZ, radius, chests, seen, stations);
            if (homeAnchor.HasValue && Vector2.Distance(homeAnchor.Value, playerPos) > 4f)
            {
                BaseSortingSystem.InvalidateTerrainScanCache();
                var homeObjs = BaseSortingSystem.CollectWorldTerrainObjects(homeAnchor.Value, radius);
                for (int i = 0; i < homeObjs.Count; i++)
                    InspectStation(homeObjs[i]?.TryCast<TerrainObject_Production>(), playerPos, homeAnchor, selfPos, workerZ, radius, chests, seen, stations);
            }
            var activeProds = TerrainObject_Production.ActiveObjects_Production;
            if (activeProds != null)
                for (int i = 0; i < activeProds.Count; i++)
                    InspectStation(activeProds[i], playerPos, homeAnchor, selfPos, workerZ, radius, chests, seen, stations);
            if (stations.Count == 0) return;

            var snaps = new List<(string id, string kind, float fuelRatio, int outputItems, float dist)>(stations.Count);
            for (int i = 0; i < stations.Count; i++)
                snaps.Add((stations[i].Id, stations[i].Kind, stations[i].FuelRatio, stations[i].OutputCount, stations[i].Dist));
            var (chosenId, taskType) = SelectNextIndustryTask(snaps, EnablePowerGridAutomation, EnableFurnaceAndKitchenAutomation);
            for (int i = 0; i < stations.Count && taskType != IndustryTaskType.None; i++)
                if (string.Equals(stations[i].Id, chosenId, StringComparison.Ordinal))
                { StartTwoPhaseTransfer(worker, workerSt, stations[i], chests, taskType == IndustryTaskType.UnloadOutput, now); break; }
        }

        private static void InspectStation(TerrainObject_Production prod, Vector2 playerPos, Vector2? homeAnchor, Vector2 selfPos, sbyte workerZ, float radius,
            List<DiscoveredContainer> chests, HashSet<IntPtr> seen, List<StationEntry> output)
        {
            if (!Il2CppSafe.IsAliveUnity(prod) || !prod.gameObject.activeInHierarchy || !seen.Add(prod.Pointer)) return;
            if (!Il2CppSafe.IsAlive(prod.objectData) || prod.objectData.zLayer != workerZ || !Il2CppSafe.IsAlive(prod.objectData.productionData)) return;
            var pd = prod.objectData.productionData;
            if (!pd.craftFinishFlag) return;

            Vector2 prodPos = prod.transform.position;
            if (homeAnchor.HasValue)
            {
                if (Vector2.Distance(prodPos, homeAnchor.Value) > radius) return;
            }
            else
            {
                if (Vector2.Distance(prodPos, playerPos) > radius || Vector2.Distance(prodPos, selfPos) > radius) return;
                if (!AutoLootSystem.IsPositionNearPlayerBase(prodPos, Mathf.Max(32f, radius))) return;
            }

            var stirling = prod.TryCast<TerrainObject_Production_StirlingGenerator>();
            var charger = prod.TryCast<TerrainObject_Production_BatteryCharger>();
            var furnace = prod.TryCast<TerrainObject_Production_Furnace>();
            var electricFurnace = prod.TryCast<TerrainObject_Production_ElectricFurnace>();

            string kind = Il2CppSafe.IsAliveUnity(stirling) ? "StirlingGenerator" :
                          Il2CppSafe.IsAliveUnity(charger) ? "BatteryCharger" :
                          Il2CppSafe.IsAliveUnity(furnace) ? "Furnace" :
                          Il2CppSafe.IsAliveUnity(electricFurnace) ? "ElectricFurnace" : "";
            string id = string.IsNullOrEmpty(kind) ? "" : $"ind_{kind}_{prod.Pointer.ToInt64():X}";
            if (string.IsNullOrEmpty(id) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(id)) return;

            InventoryData fuelInv = null;
            InventoryData outSlot = null;
            if (kind == "StirlingGenerator")
            {
                // StirlingGenerator.fuelInventoryData is always pd.inventoryData1 (itemFeatureLimit: Combustible, Ash)
                fuelInv = (Il2CppSafe.IsAlive(pd.inventoryData1) && pd.inventoryData1.inventorySizeX > 0)
                    ? pd.inventoryData1
                    : stirling.fuelInventoryData;
            }
            else if (kind == "BatteryCharger")
            {
                // BatteryCharger slots live in pd.inventoryData1 (itemFeatureLimit: Battery)
                fuelInv = pd.inventoryData1;
            }
            else if (kind == "Furnace")
            {
                // Stone Furnace native slots: inventoryData1 = Material (Meltable), inventoryData2 = Output, inventoryData3 = Fuel (Combustible, Ash)
                fuelInv = (Il2CppSafe.IsAlive(pd.inventoryData3) && pd.inventoryData3.inventorySizeX > 0)
                    ? pd.inventoryData3
                    : furnace.fuelInventoryData;
                outSlot = (Il2CppSafe.IsAlive(pd.inventoryData2) && pd.inventoryData2.inventorySizeX > 0)
                    ? pd.inventoryData2
                    : furnace.outputInventoryData;
            }
            else if (kind == "ElectricFurnace")
            {
                // Electric Furnace native slots: inventoryData1 = Material, inventoryData2 = Output (no fuel slot)
                outSlot = (Il2CppSafe.IsAlive(pd.inventoryData2) && pd.inventoryData2.inventorySizeX > 0)
                    ? pd.inventoryData2
                    : electricFurnace.outputInventoryData;
            }

            InventoryData outInv = null; ItemData outItem = null; int outCount = 0;
            if (kind == "BatteryCharger" && Il2CppSafe.IsAlive(fuelInv) && fuelInv.itemList != null)
            {
                for (int i = 0; i < fuelInv.itemList.Count; i++)
                {
                    var cand = fuelInv.itemList[i];
                    if (!IsBatteryFullyChargedData(cand)) continue;
                    if (FindSupplyOrDest(chests, prod, fuelInv, cand, batteryMode: true, findDest: true, out _, out _))
                    {
                        if (outItem == null) { outInv = fuelInv; outItem = cand; }
                        outCount++;
                    }
                }
            }

            if (outCount == 0 && Il2CppSafe.IsAlive(outSlot) && outSlot.inventorySizeX > 0 && outSlot.itemList != null)
            {
                for (int i = 0; i < outSlot.itemList.Count; i++)
                {
                    var cand = outSlot.itemList[i];
                    if (!Il2CppSafe.IsAlive(cand) || IsNativeCombustibleData(cand)) continue;
                    if (FindSupplyOrDest(chests, prod, outSlot, cand, batteryMode: false, findDest: true, out _, out _))
                    {
                        if (outItem == null) { outInv = outSlot; outItem = cand; }
                        outCount++;
                    }
                }
            }

            // Also unload non-combustible ash/charcoal byproduct from StirlingGenerator / Furnace fuel slots
            // when fuel space is tight, a stack >= 5 has accumulated, or no combustible fuel remains (avoids 1-piece churn).
            if (outCount == 0 && (kind == "StirlingGenerator" || kind == "Furnace") &&
                Il2CppSafe.IsAlive(fuelInv) && fuelInv.inventorySizeX > 0 && fuelInv.itemList != null)
            {
                bool fuelSlotTightOrEmptyFuel = fuelInv.CountFreeCells() <= 2 || ProductionManager.FindLastCombustibleIndex(fuelInv) < 0;
                for (int i = 0; i < fuelInv.itemList.Count; i++)
                {
                    var cand = fuelInv.itemList[i];
                    if (!IsNonCombustibleAshData(cand)) continue;
                    if (!fuelSlotTightOrEmptyFuel && cand.itemNumberFloat < 5f) continue;
                    if (FindSupplyOrDest(chests, prod, fuelInv, cand, batteryMode: false, findDest: true, out _, out _))
                    {
                        if (outItem == null) { outInv = fuelInv; outItem = cand; }
                        outCount++;
                    }
                }
            }

            float fuelRatio = -1f;
            if (kind == "BatteryCharger")
            {
                if (Il2CppSafe.IsAlive(fuelInv) && fuelInv.inventorySizeX > 0 &&
                    prod.isPowerSwitchedOn && pd.powerInputSufficientFloat > 0.001f)
                {
                    fuelRatio = (fuelInv.itemList != null && fuelInv.itemList.Count > 0) ? 1f :
                                (FindSupplyOrDest(chests, prod, fuelInv, null, batteryMode: true, findDest: false, out _, out _) ? 0f : -1f);
                }
            }
            else if ((kind == "StirlingGenerator" || kind == "Furnace") &&
                     Il2CppSafe.IsAlive(fuelInv) && fuelInv.inventorySizeX > 0)
            {
                bool stationSwitchAllowsRefuel = kind != "StirlingGenerator" || prod.isPowerSwitchedOn;
                if (stationSwitchAllowsRefuel)
                {
                    float units = 0f;
                    if (fuelInv.itemList != null)
                    {
                        for (int i = 0; i < fuelInv.itemList.Count; i++)
                            if (IsNativeCombustibleData(fuelInv.itemList[i]))
                                units += Mathf.Max(1f, fuelInv.itemList[i].itemNumberFloat);
                    }
                    fuelRatio = Mathf.Clamp01(units / 10f);
                    if (fuelRatio < RefuelFuelRatioThreshold &&
                        !FindSupplyOrDest(chests, prod, fuelInv, null, batteryMode: false, findDest: false, out _, out _))
                    {
                        fuelRatio = -1f;
                    }
                }
            }

            if (outCount <= 0 && (fuelRatio < 0f || fuelRatio >= RefuelFuelRatioThreshold))
                return;

            output.Add(new StationEntry { Id = id, Kind = kind, Prod = prod, Data = pd, FuelInv = fuelInv, OutInv = outInv, OutItem = outItem, OutputCount = outCount, FuelRatio = fuelRatio, Dist = Vector2.Distance(selfPos, prod.transform.position) });
        }

        private static string FullItemNameLower(ItemData it)
        {
            var attr = Il2CppSafe.IsAlive(it) ? ItemManager.instance?.GetItemAttrById(it.itemId) : null;
            return ((attr?.ItemName_EN ?? "") + " " + (attr?.itemName ?? "") + " " + (it?.GetItemName() ?? "")).ToLowerInvariant();
        }

        private static bool FindSupplyOrDest(
            List<DiscoveredContainer> chests,
            TerrainObject_Production stationProd,
            InventoryData targetStationInv,
            ItemData outItem,
            bool batteryMode,
            bool findDest,
            out DiscoveredContainer matchedChest,
            out ItemData matchedItem)
        {
            matchedChest = null; matchedItem = null;
            if (chests == null) return false;
            StorageCategory outCat = findDest && Il2CppSafe.IsAlive(outItem) ? ItemCategorizer.ClassifyItem(outItem) : StorageCategory.None;
            for (int i = 0; i < chests.Count; i++)
            {
                var c = chests[i];
                if (c?.Rule == null || !c.Rule.IsMarkedByPlayer || !Il2CppSafe.IsAliveUnity(c.TerrainObj) ||
                    !Il2CppSafe.IsAlive(c.Inventory) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId)) continue;
                if (Il2CppSafe.IsAliveUnity(stationProd) && c.TerrainObj.Pointer == stationProd.Pointer) continue;
                if (Il2CppSafe.IsAlive(targetStationInv) && c.Inventory.Pointer == targetStationInv.Pointer) continue;
                if (c.TerrainObj.TryCast<TerrainObject_Production_BatteryCharger>() != null) continue;

                if (findDest && !c.Rule.IsDropoffSource && (c.Rule.Categories & outCat) != 0 && InventoryTransfers.CanAcceptWholeItem(c.Inventory, outItem))
                { matchedChest = c; matchedItem = outItem; return true; }
                if (!findDest && c.Inventory.itemList != null && Il2CppSafe.IsAlive(targetStationInv))
                {
                    for (int j = 0; j < c.Inventory.itemList.Count; j++)
                    {
                        var cand = c.Inventory.itemList[j];
                        bool eligible = batteryMode
                            ? IsDepletedBatteryData(cand)
                            : IsStationFuelData(cand, targetStationInv);
                        if (eligible && InventoryTransfers.CanAcceptWholeItem(targetStationInv, cand))
                        { matchedChest = c; matchedItem = cand; return true; }
                    }
                }
            }
            return false;
        }

        private static void StartTwoPhaseTransfer(GOAPAgent ag, CompanionRuntimeState st, StationEntry station, List<DiscoveredContainer> chests, bool unload, float now)
        {
            bool isCharger = station.Kind == "BatteryCharger";
            if (!FindSupplyOrDest(chests, station.Prod, unload ? station.OutInv : station.FuelInv, station.OutItem, isCharger, unload, out var chest, out var item)) return;
            InventoryData srcInv = unload ? station.OutInv : chest.Inventory, dstInv = unload ? chest.Inventory : station.FuelInv;
            Transform srcTr = unload ? station.Prod.transform : chest.TerrainObj.transform, dstTr = unload ? chest.TerrainObj.transform : station.Prod.transform;
            if (!Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(dstInv) || !Il2CppSafe.IsAlive(item)) return;

            string phase1Key = unload ? station.Id : chest.ContainerId;
            string phase2Key = unload ? chest.ContainerId : station.Id;

            string walkPhase1Text = unload
                ? (isCharger
                    ? CompanionConfigStore.L("Идёт забирать заряженную батарею...", "Walking to collect charged battery...", "前往充电器取回满电电池...")
                    : CompanionConfigStore.L("Идёт забирать продукцию со станции...", "Walking to collect station output...", "前往工作站收取产物..."))
                : (isCharger
                    ? CompanionConfigStore.L("Идёт к складу за разряженной батареей...", "Walking to chest for depleted battery...", "前往储物箱拿取亏电电池...")
                    : CompanionConfigStore.L("Идёт к складу за топливом...", "Walking to chest for station fuel...", "前往储物箱拿取燃料..."));

            string walkPhase2Text = unload
                ? (isCharger
                    ? CompanionConfigStore.L("Несёт заряженную батарею на склад...", "Carrying charged battery to storage...", "正将满电电池搬运至仓库...")
                    : CompanionConfigStore.L("Несёт продукцию со станции на склад...", "Carrying station output to storage...", "正将工作站产物搬运至仓库..."))
                : (isCharger
                    ? CompanionConfigStore.L("Несёт батарею в зарядную станцию...", "Carrying battery to charger...", "正将电池搬运至充电器...")
                    : CompanionConfigStore.L("Несёт топливо к станции...", "Carrying fuel to station...", "正将燃料搬运至工作站..."));

            string doneText = unload
                ? (isCharger
                    ? CompanionConfigStore.L("Забрал заряженную батарею", "Stored charged battery", "已收纳满电电池")
                    : CompanionConfigStore.L("Выгрузил продукцию со станции", "Unloaded station output", "已卸载工作站产物"))
                : (isCharger
                    ? CompanionConfigStore.L("Поставил батарею на зарядку", "Loaded battery into charger", "已将电池放入充电器")
                    : CompanionConfigStore.L("Заправил и запустил станцию", "Refueled and started station", "已补充燃料并启动工作站"));

            st.LastChoreTime = now;
            BaseSortingSystem.RegisterExternalInventoryAnchor(unload ? station.OutInv : station.FuelInv, station.Prod.transform, station.Kind);
            BaseChoreSystem.StartPhysicalChoreWalk(ag, st, srcTr.position, srcTr,
                walkPhase1Text,
                0, () =>
                {
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(srcTr) || !Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(item) ||
                        PhysicalMovementHelper.ComputeSurfaceDistance(ag.humanCharacter.transform.position, srcTr.position, srcTr) > StationInteractRadius)
                        return;
                    // Re-validate item state on arrival so partially-charged batteries or non-combustible items are never moved mid-walk.
                    if (isCharger)
                    {
                        if (unload && !IsBatteryFullyChargedData(item)) return;
                        if (!unload && !IsDepletedBatteryData(item)) return;
                    }
                    else
                    {
                        if (unload && IsNativeCombustibleData(item)) return;
                        if (!unload && !IsStationFuelData(item, dstInv)) return;
                    }
                    if (!InventoryTransfers.TryTakeIntoCarryBuffer(srcInv, item, out var carried)) return;
                    st.CarryBuffer.Add(new CarryItemEntry { Item = carried, FallbackSourceInv = srcInv, TargetContainerId = unload ? chest.ContainerId : station.Id, TargetInv = dstInv, TargetAnchor = dstTr, TargetPos = dstTr.position, TargetDisplayName = unload ? chest.DisplayName : station.Kind, Category = ItemCategorizer.ClassifyItem(carried) });
                    BaseChoreSystem.StartPhysicalChoreWalk(ag, st, dstTr.position, dstTr,
                        walkPhase2Text,
                        carried.itemId, () =>
                        {
                            if (!Il2CppSafe.IsAliveUnity(ag)) return;
                            if (Il2CppSafe.IsAliveUnity(dstTr) && Il2CppSafe.IsAlive(dstInv) &&
                                PhysicalMovementHelper.ComputeSurfaceDistance(ag.humanCharacter.transform.position, dstTr.position, dstTr) <= StationInteractRadius &&
                                InventoryTransfers.TryDepositOrStackWhole(dstInv, carried))
                            {
                                st.CarryBuffer.Clear();
                                if (!unload) ActivateStationAtHand(station.Prod, station.Data, station.FuelInv); else st.SortedItemsThisSession++;
                                st.ChoresDoneThisSession++; st.AddExperience(unload ? 8 : 10, ag);
                                st.CurrentTaskText = doneText;
                            }
                            else BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                        },
                        phase2Key);
                },
                phase1Key);
        }

        private static void ActivateStationAtHand(TerrainObject_Production prod, ProductionData pd, InventoryData fuelInv)
        {
            try
            {
                var stirling = prod.TryCast<TerrainObject_Production_StirlingGenerator>();
                if (Il2CppSafe.IsAliveUnity(stirling))
                {
                    if (prod.isPowerSwitchedOn && !stirling.isRunning &&
                        Il2CppSafe.IsAlive(fuelInv) && ProductionManager.FindLastCombustibleIndex(fuelInv) >= 0)
                    {
                        stirling.OnGeneratorStart();
                    }
                    return;
                }
                var charger = prod.TryCast<TerrainObject_Production_BatteryCharger>();
                if (Il2CppSafe.IsAliveUnity(charger))
                {
                    charger.UpdateBatteryCharging();
                    return;
                }
                var furnace = prod.TryCast<TerrainObject_Production_Furnace>();
                if (Il2CppSafe.IsAliveUnity(furnace))
                {
                    bool hasMaterial = Il2CppSafe.IsAlive(pd?.inventoryData1) &&
                                       pd.inventoryData1.itemList != null &&
                                       pd.inventoryData1.itemList.Count > 0;
                    if (!pd.isFurnaceBurning && hasMaterial && Il2CppSafe.IsAlive(fuelInv) && ProductionManager.FindLastCombustibleIndex(fuelInv) >= 0)
                        furnace.OnIgnite();
                    return;
                }
            }
            catch { }
        }
    }
}
