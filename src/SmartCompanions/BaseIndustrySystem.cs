using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public enum IndustryTaskType { None = 0, UnloadOutput = 1, RefuelStation = 2 }

    /// <summary>
    /// v2.7.0 Industrial Base Automation: generators, battery chargers, furnaces &amp; stoves
    /// serviced via two-phase physical A* walks and CarryBuffer with strictly zero teleportation.
    /// </summary>
    public static class BaseIndustrySystem
    {
        public const float StationInteractRadius = PhysicalMovementHelper.MaxReachableInteractRadius; // 2.45m
        public const float RefuelFuelRatioThreshold = 0.35f;
        public static bool EnablePowerGridAutomation = true;
        public static bool EnableFurnaceAndKitchenAutomation = true;
        private static float _lastIndustryTickAt = -999f;

        private static readonly string[] BlockedFuelSubstrings =
        {
            "weapon", "rifle", "pistol", "shotgun", "smg", "gun", "sword", "knife", "axe", "machete", "spear", "bow", "crossbow",
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
            "оружи", "винтовк", "пистолет", "дробовик", "автомат", "нож", "топор", "мачете", "патрон", "магазин", "гранат",
            "молотов", "взрыв", "порох", "брон", "жилет", "шлем", "каск", "куртк", "плащ", "штаны", "ботинк", "перчатк", "рюкзак",
            "сумк", "маск", "одежд", "активирован", "таблет", "капсул", "бинт", "аптеч", "сыворотк", "шприц", "лекарств",
            "медицин", "антибиотик", "обезбол", "йод", "стейк", "мясо", "суп", "рагу", "хлеб", "консерв", "рыб", "пирог",
            "подсолнеч", "оливков", "пищев", "вода", "кола", "пиво", "вино", "водк", "спирт", "алкогол", "еда", "паек", "книг",
            "учебник", "руководств", "чертеж", "схем", "рецепт", "дневник", "записк", "деньг", "доллар", "купюр", "банкнот",
            "монет", "золот", "серебр", "кольц", "часы", "ключ", "карт", "насос", "инжектор", "фильтр", "карбюратор", "двигател",
            "мотор", "свеч", "поршн", "радиатор", "аккумулятор", "батаре", "зарядн", "солнечн", "микросхем", "плат", "процессор",
            "чип", "датчик", "модул", "турел", "доск", "фанер", "мебел", "редк", "уникал", "легендар", "артефакт", "прототип",
            "квест", "пуст", "步枪", "手枪", "子弹", "炸药", "手雷", "护甲", "头盔", "外套", "背包", "面具", "绷带", "药片", "急救",
            "血清", "活性炭", "牛排", "罐头", "食用油", "啤酒", "书", "图纸", "配方", "钞票", "美元", "金币", "电池", "充电", "发动机",
            "油泵", "喷油", "滤清器", "芯片", "木板", "稀有", "传奇", "任务", "空桶", "空的"
        };

        private static readonly string[] ApprovedFuelSubstrings =
        {
            "firewood", "wood log", "wooden log", "split wood", "dry wood", "fuel wood", "kindling", "charcoal", "coal",
            "briquette", "peat", "gasoline", "petrol", "diesel", "kerosene", "biofuel", "fuel can", "fuel canister", "gas can",
            "gas canister", "jerrycan", "jerry can", "liquid fuel", "fuel barrel", "fuel drum", "fuel bottle", "generator fuel",
            "stove fuel", "дрова", "полен", "хворост", "уголь", "угля", "брикет", "торф", "бензин", "дизель", "дизтоплив",
            "солярк", "керосин", "биотоплив", "горючее", "горюч", "топливо", "канистра с бензин", "канистра бензин",
            "канистра топлив", "канистра с топлив", "канистра горюч", "топливная канистра",
            "柴火", "木柴", "原木", "木炭", "煤炭", "煤块", "蜂窝煤", "汽油", "柴油", "煤油", "生物燃料", "燃油", "汽油桶", "燃料桶", "燃料罐"
        };

        public static bool AllowsIndustryRole(CompanionRuntimeState st) =>
            st != null && (st.WorkRole == CompanionWorkRole.Any || st.WorkRole == CompanionWorkRole.Camp || st.WorkRole == CompanionWorkRole.Sorter);

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

        public static bool IsApprovedStationFuelItem(int itemId, StorageCategory cat, string itemNameLower)
        {
            if (itemId <= 0 || string.IsNullOrWhiteSpace(itemNameLower) || CompanionProfileSystem.IsRareMedItem(itemId)) return false;
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
            if (!CompanionConfigStore.EnableBaseChores || BaseWorkManager.Paused || MultiplayerSyncSystem.IsMultiplayerClient) return;
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
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (!st.IsSortingActive && !st.IsChoreWalkActive && st.CarryBuffer.Count > 0)
                    BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                if (worker == null && st.AllowBaseChores && !st.HasAnyPhysicalTask && st.CarryBuffer.Count == 0 &&
                    now - st.LastChoreTime >= 12f && BaseWorkManager.MayStartAutomaticWork(st, AllowsIndustryRole))
                { worker = ag; workerSt = st; }
            }
            if (worker == null) return;

            Vector2 playerPos = gc.playerCharacter.transform.position, selfPos = worker.humanCharacter.transform.position;
            float radius = Mathf.Clamp(CompanionConfigStore.BaseScanRadius, 16f, 48f);
            var chests = BaseSortingSystem.ScanNearbyContainers(force: false);
            var stations = new List<StationEntry>(); var seen = new HashSet<IntPtr>();
            var worldObjs = BaseSortingSystem.CollectWorldTerrainObjects(playerPos, radius);
            for (int i = 0; i < worldObjs.Count; i++)
                InspectStation(worldObjs[i]?.TryCast<TerrainObject_Production>(), playerPos, selfPos, radius, chests, seen, stations);
            var activeProds = TerrainObject_Production.ActiveObjects_Production;
            if (activeProds != null)
                for (int i = 0; i < activeProds.Count; i++)
                    InspectStation(activeProds[i], playerPos, selfPos, radius, chests, seen, stations);
            if (stations.Count == 0) return;

            var snaps = new List<(string id, string kind, float fuelRatio, int outputItems, float dist)>(stations.Count);
            for (int i = 0; i < stations.Count; i++)
                snaps.Add((stations[i].Id, stations[i].Kind, stations[i].FuelRatio, stations[i].OutputCount, stations[i].Dist));
            var (chosenId, taskType) = SelectNextIndustryTask(snaps, EnablePowerGridAutomation, EnableFurnaceAndKitchenAutomation);
            for (int i = 0; i < stations.Count && taskType != IndustryTaskType.None; i++)
                if (string.Equals(stations[i].Id, chosenId, StringComparison.Ordinal))
                { StartTwoPhaseTransfer(worker, workerSt, stations[i], chests, taskType == IndustryTaskType.UnloadOutput, now); break; }
        }

        private static void InspectStation(TerrainObject_Production prod, Vector2 playerPos, Vector2 selfPos, float radius,
            List<DiscoveredContainer> chests, HashSet<IntPtr> seen, List<StationEntry> output)
        {
            if (!Il2CppSafe.IsAliveUnity(prod) || !prod.gameObject.activeInHierarchy || !seen.Add(prod.Pointer)) return;
            if (Vector2.Distance(prod.transform.position, playerPos) > radius || !Il2CppSafe.IsAlive(prod.objectData?.productionData)) return;
            var pd = prod.objectData.productionData;
            string kind = prod.TryCast<TerrainObject_Production_StirlingGenerator>() != null ? "StirlingGenerator" :
                          prod.TryCast<TerrainObject_Production_BatteryCharger>() != null ? "BatteryCharger" :
                          prod.TryCast<TerrainObject_Production_Furnace>() != null ? "Furnace" :
                          prod.TryCast<TerrainObject_Production_ElectricFurnace>() != null ? "ElectricFurnace" :
                          prod.TryCast<TerrainObject_Production_Stove>() != null ? "Stove" :
                          prod.TryCast<TerrainObject_Production_Heater>() != null ? "Heater" :
                          prod.TryCast<TerrainObject_CookingHob>() != null ? "CookingHob" :
                          prod.TryCast<TerrainObject_Production_EnergyStorage>() != null ? "EnergyStorage" : "";
            string id = string.IsNullOrEmpty(kind) ? "" : $"ind_{kind}_{prod.Pointer.ToInt64():X}";
            if (string.IsNullOrEmpty(id) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(id)) return;

            bool inv3Alive = Il2CppSafe.IsAlive(pd.inventoryData3) && pd.inventoryData3.inventorySizeX > 0;
            bool inv1HasNonFuel = Il2CppSafe.IsAlive(pd.inventoryData1) && pd.inventoryData1.itemList != null &&
                                  pd.inventoryData1.itemList.Count > 0 && ProductionManager.FindLastCombustibleIndex(pd.inventoryData1) < 0;
            bool inv3IsFuel = inv3Alive && (ProductionManager.FindLastCombustibleIndex(pd.inventoryData3) >= 0 || inv1HasNonFuel);
            InventoryData fuelInv = inv3IsFuel ? pd.inventoryData3 : pd.inventoryData1;
            InventoryData outSlot = (inv3Alive && !inv3IsFuel) ? pd.inventoryData3 : pd.inventoryData2;

            InventoryData outInv = null; ItemData outItem = null; int outCount = 0;
            if (kind == "BatteryCharger" && Il2CppSafe.IsAlive(pd.inventoryData1) && pd.inventoryData1.itemList != null)
                for (int i = 0; i < pd.inventoryData1.itemList.Count; i++)
                    if (Il2CppSafe.IsAlive(pd.inventoryData1.itemList[i]) && (pd.inventoryData1.itemList[i].durability >= 99.5f || pd.GetBatteryState(i) >= 2))
                    { outInv = pd.inventoryData1; outItem = pd.inventoryData1.itemList[i]; outCount = 1; break; }

            if (outCount == 0 && Il2CppSafe.IsAlive(outSlot) && outSlot.inventorySizeX > 0 && outSlot.itemList != null)
                for (int i = 0; i < outSlot.itemList.Count; i++)
                    if (Il2CppSafe.IsAlive(outSlot.itemList[i]) && !IsStationFuelData(outSlot.itemList[i]))
                    { if (outItem == null) { outInv = outSlot; outItem = outSlot.itemList[i]; } outCount++; }

            float fuelRatio = -1f;
            if (kind == "BatteryCharger" && Il2CppSafe.IsAlive(pd.inventoryData1) && pd.inventoryData1.inventorySizeX > 0)
                fuelRatio = (pd.inventoryData1.itemList != null && pd.inventoryData1.itemList.Count > 0) ? 1f :
                            (FindSupplyOrDest(chests, pd.inventoryData1, null, true, false, out _, out _) ? 0f : -1f);
            else if (kind != "ElectricFurnace" && kind != "EnergyStorage" && kind != "BatteryCharger" && Il2CppSafe.IsAlive(fuelInv) && fuelInv.inventorySizeX > 0)
            {
                float units = 0f;
                if (fuelInv.itemList != null)
                    for (int i = 0; i < fuelInv.itemList.Count; i++)
                        if (IsStationFuelData(fuelInv.itemList[i])) units += Mathf.Max(1f, fuelInv.itemList[i].itemNumberFloat);
                fuelRatio = Mathf.Clamp01(units / 10f);
                if (fuelRatio < RefuelFuelRatioThreshold && !FindSupplyOrDest(chests, fuelInv, null, false, false, out _, out _))
                    fuelRatio = -1f;
            }
            output.Add(new StationEntry { Id = id, Kind = kind, Prod = prod, Data = pd, FuelInv = fuelInv, OutInv = outInv, OutItem = outItem, OutputCount = outCount, FuelRatio = fuelRatio, Dist = Vector2.Distance(selfPos, prod.transform.position) });
        }

        private static string FullItemNameLower(ItemData it)
        {
            var attr = Il2CppSafe.IsAlive(it) ? ItemManager.instance?.GetItemAttrById(it.itemId) : null;
            return ((attr?.ItemName_EN ?? "") + " " + (attr?.itemName ?? "") + " " + (it?.GetItemName() ?? "")).ToLowerInvariant();
        }

        private static bool IsStationFuelData(ItemData it) =>
            Il2CppSafe.IsAlive(it) && IsApprovedStationFuelItem(it.itemId, ItemCategorizer.ClassifyItem(it), FullItemNameLower(it));

        private static bool IsDepletedBatteryData(ItemData it) =>
            Il2CppSafe.IsAlive(it) && it.durability < 85f && ItemCategorizer.ClassifyItem(it) == StorageCategory.VehicleAndPower &&
            !IsStationFuelData(it) && (FullItemNameLower(it) is string n) &&
            (n.Contains("batt", StringComparison.Ordinal) || n.Contains("аккум", StringComparison.Ordinal) || n.Contains("батар", StringComparison.Ordinal) || n.Contains("电池", StringComparison.Ordinal));

        private static bool FindSupplyOrDest(List<DiscoveredContainer> chests, InventoryData targetStationInv, ItemData outItem,
            bool batteryMode, bool findDest, out DiscoveredContainer matchedChest, out ItemData matchedItem)
        {
            matchedChest = null; matchedItem = null;
            if (chests == null) return false;
            StorageCategory outCat = findDest && Il2CppSafe.IsAlive(outItem) ? ItemCategorizer.ClassifyItem(outItem) : StorageCategory.None;
            for (int i = 0; i < chests.Count; i++)
            {
                var c = chests[i];
                if (c?.Rule == null || !c.Rule.IsMarkedByPlayer || !Il2CppSafe.IsAliveUnity(c.TerrainObj) ||
                    !Il2CppSafe.IsAlive(c.Inventory) || PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId)) continue;
                if (findDest && !c.Rule.IsDropoffSource && (c.Rule.Categories & outCat) != 0 && InventoryTransfers.CanAcceptWholeItem(c.Inventory, outItem))
                { matchedChest = c; matchedItem = outItem; return true; }
                if (!findDest && c.Inventory.itemList != null && Il2CppSafe.IsAlive(targetStationInv))
                    for (int j = 0; j < c.Inventory.itemList.Count; j++)
                        if ((batteryMode ? IsDepletedBatteryData(c.Inventory.itemList[j]) : IsStationFuelData(c.Inventory.itemList[j])) &&
                            InventoryTransfers.CanAcceptWholeItem(targetStationInv, c.Inventory.itemList[j]))
                        { matchedChest = c; matchedItem = c.Inventory.itemList[j]; return true; }
            }
            return false;
        }

        private static void StartTwoPhaseTransfer(GOAPAgent ag, CompanionRuntimeState st, StationEntry station, List<DiscoveredContainer> chests, bool unload, float now)
        {
            if (!FindSupplyOrDest(chests, station.FuelInv, station.OutItem, station.Kind == "BatteryCharger", unload, out var chest, out var item)) return;
            InventoryData srcInv = unload ? station.OutInv : chest.Inventory, dstInv = unload ? chest.Inventory : station.FuelInv;
            Transform srcTr = unload ? station.Prod.transform : chest.TerrainObj.transform, dstTr = unload ? chest.TerrainObj.transform : station.Prod.transform;
            if (!Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(dstInv) || !Il2CppSafe.IsAlive(item)) return;

            st.LastChoreTime = now;
            BaseSortingSystem.RegisterExternalInventoryAnchor(unload ? station.OutInv : station.FuelInv, station.Prod.transform, station.Kind);
            BaseChoreSystem.StartPhysicalChoreWalk(ag, st, srcTr.position, srcTr,
                unload ? CompanionConfigStore.L("Идёт забирать продукцию со станции...", "Walking to collect station output...", "前往工作站收取产物...")
                       : CompanionConfigStore.L("Идёт к складу за топливом...", "Walking to chest for station fuel...", "前往储物箱拿取燃料..."),
                0, () =>
                {
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(srcTr) || !Il2CppSafe.IsAlive(srcInv) ||
                        PhysicalMovementHelper.ComputeSurfaceDistance(ag.humanCharacter.transform.position, srcTr.position, srcTr) > StationInteractRadius ||
                        !InventoryTransfers.TryTakeIntoCarryBuffer(srcInv, item, out var carried)) return;
                    st.CarryBuffer.Add(new CarryItemEntry { Item = carried, TargetContainerId = unload ? chest.ContainerId : station.Id, TargetInv = dstInv, TargetAnchor = dstTr, TargetPos = dstTr.position, TargetDisplayName = unload ? chest.DisplayName : station.Kind, Category = ItemCategorizer.ClassifyItem(carried) });
                    BaseChoreSystem.StartPhysicalChoreWalk(ag, st, dstTr.position, dstTr,
                        unload ? CompanionConfigStore.L("Несёт продукцию со станции на склад...", "Carrying station output to storage...", "正将工作站产物搬运至仓库...")
                               : CompanionConfigStore.L("Несёт топливо к станции...", "Carrying fuel to station...", "正将燃料搬运至工作站..."),
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
                                st.CurrentTaskText = unload ? CompanionConfigStore.L("Выгрузил продукцию со станции", "Unloaded station output", "已卸载工作站产物")
                                                            : CompanionConfigStore.L("Заправил и запустил станцию", "Refueled and started station", "已补充燃料并启动工作站");
                            }
                            else BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                        });
                });
        }

        private static void ActivateStationAtHand(TerrainObject_Production prod, ProductionData pd, InventoryData fuelInv)
        {
            try
            {
                var stirling = prod.TryCast<TerrainObject_Production_StirlingGenerator>();
                if (Il2CppSafe.IsAliveUnity(stirling)) { if (!prod.isPowerSwitchedOn) prod.SetPowerSwitch(true); stirling.OnGeneratorStart(); return; }
                var charger = prod.TryCast<TerrainObject_Production_BatteryCharger>();
                if (Il2CppSafe.IsAliveUnity(charger)) { if (!prod.isPowerSwitchedOn) prod.SetPowerSwitch(true); charger.UpdateBatteryCharging(); return; }
                var furnace = prod.TryCast<TerrainObject_Production_Furnace>();
                if (Il2CppSafe.IsAliveUnity(furnace)) { if (!pd.isFurnaceBurning && Il2CppSafe.IsAlive(fuelInv) && ProductionManager.FindLastCombustibleIndex(fuelInv) >= 0) furnace.OnIgnite(); return; }
                if (!prod.isPowerSwitchedOn) prod.SetPowerSwitch(true);
            }
            catch { }
        }
    }
}
