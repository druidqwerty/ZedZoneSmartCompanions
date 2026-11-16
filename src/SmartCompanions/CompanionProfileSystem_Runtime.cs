using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class CompanionProfileSystem
    {
        public static void ApplyPresetToCompanion(
            GOAPAgent agent, CompanionRuntimeState st, CompanionSpecialization spec, bool sync = true)
        {
            if (st == null || spec == CompanionSpecialization.Custom) return;
            var p = GetPreset(spec);
            st.Specialization = spec;
            st.Doctrine = p.Doctrine;
            st.WorkRole = p.Role;
            st.Loadout = p.Loadout;
            st.AllowFieldMedic = p.FieldMedic;
            st.AllowAmmoShare = p.AmmoShare;
            st.AllowAutoLootNearby = p.AutoLoot;
            st.AllowAutoDropoffAtBase = p.AutoDropoff;
            st.AllowBaseChores = p.CampChores;
            st.ReserveAmmoMin = p.ReserveAmmo;
            st.ReserveFoodMin = p.ReserveFood;
            st.ReserveMedMin = p.ReserveMed;

            ApplyPreferredLoadoutAndDoctrine(agent, st);
            CompanionConfigStore.Save();
            if (sync) MultiplayerSyncSystem.SyncCompanionStateChange(st);
        }

        public static int ApplyPresetToSquad(CompanionSpecialization spec)
        {
            if (spec == CompanionSpecialization.Custom) return 0;
            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            int count = 0;
            foreach (var ag in squad)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                if (MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                    continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                ApplyPresetToCompanion(ag, st, spec, sync: true);
                count++;
            }

            if (count > 0)
            {
                string lbl = GetSpecializationLabel(spec, CompanionConfigStore.EffectiveLanguage);
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                    $"Профиль «{lbl}» применён к отряду ({count} чел.)!",
                    $"Specialization '{lbl}' applied to squad ({count})!",
                    $"专精方案「{lbl}」已应用至小队（{count} 人）！"), 5f);
            }
            return count;
        }

        public static void CycleCompanionSpecialization(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            int next = ((int)st.Specialization % 4) + 1; // cycles Fighter(1) -> Medic(2) -> Quartermaster(3) -> Scavenger(4)
            ApplyPresetToCompanion(agent, st, (CompanionSpecialization)next, sync: true);
        }

        public static void CycleCompanionLoadout(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            st.Loadout = (PreferredLoadout)(((int)st.Loadout + 1) % 3);
            st.Specialization = CompanionSpecialization.Custom;
            ApplyPreferredLoadoutAndDoctrine(agent, st);
            CompanionConfigStore.Save();
            MultiplayerSyncSystem.SyncCompanionStateChange(st);
        }

        public static void CycleCompanionReserves(CompanionRuntimeState st)
        {
            if (st == null) return;
            if (st.ReserveAmmoMin <= 0) { st.ReserveAmmoMin = 24; st.ReserveFoodMin = 2; st.ReserveMedMin = 1; }
            else if (st.ReserveAmmoMin <= 24) { st.ReserveAmmoMin = 45; st.ReserveFoodMin = 3; st.ReserveMedMin = 2; }
            else if (st.ReserveAmmoMin <= 45) { st.ReserveAmmoMin = 60; st.ReserveFoodMin = 4; st.ReserveMedMin = 4; }
            else { st.ReserveAmmoMin = 0; st.ReserveFoodMin = 0; st.ReserveMedMin = 0; }
            st.Specialization = CompanionSpecialization.Custom;
            CompanionConfigStore.Save();
            MultiplayerSyncSystem.SyncCompanionStateChange(st);
        }

        public static void ApplyPreferredLoadoutAndDoctrine(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) || st == null) return;
            SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
            if (st.Doctrine == TacticalDoctrine.Passive || st.Doctrine == TacticalDoctrine.StealthMelee)
                return;
            try
            {
                if (st.Loadout == PreferredLoadout.RangedFirst)
                    agent.npcData.ExecuteNPCCommand("WeaponPreference.RangedWeaponOnly");
                else if (st.Loadout == PreferredLoadout.MeleeFirst)
                    agent.npcData.ExecuteNPCCommand("WeaponPreference.MeleeWeaponOnly");
            }
            catch { }
        }

        /// <summary>Builds the set of ItemData pointers in a companion's backpack protected by their non-reducible reserve.</summary>
        public static HashSet<IntPtr> BuildProtectedBackpackPointers(GOAPAgent agent, CompanionRuntimeState st, InventoryData backpack)
        {
            var result = new HashSet<IntPtr>();
            if (st == null || !Il2CppSafe.IsAlive(backpack) || backpack.itemList == null || backpack.itemList.Count == 0)
                return result;

            var sharedInv = agent?.npcData?.sharedInventoryData;
            var personalInv = agent?.npcData?.characterData?.inventoryData;
            bool isSharedInv = Il2CppSafe.IsAlive(sharedInv) && backpack.Pointer == sharedInv.Pointer;

            int effectiveAmmoMin = Mathf.Max(0, st.ReserveAmmoMin);
            int effectiveFoodMin = Mathf.Max(0, st.ReserveFoodMin);
            int effectiveMedMin = Mathf.Max(0, st.ReserveMedMin);

            if (isSharedInv && Il2CppSafe.IsAlive(personalInv) && personalInv.Pointer != backpack.Pointer)
            {
                int personalAmmo = 0;
                int personalFood = 0;
                int personalMed = 0;
                try
                {
                    if (personalInv.itemList != null)
                    {
                        for (int i = 0; i < personalInv.itemList.Count; i++)
                        {
                            var pItem = personalInv.itemList[i];
                            if (!Il2CppSafe.IsAlive(pItem)) continue;
                            var pCat = ItemCategorizer.ClassifyItem(pItem);
                            int pUnits = Mathf.Max(1, Mathf.RoundToInt(pItem.itemNumberFloat));
                            if (pCat == StorageCategory.AmmoAndMags)
                                personalAmmo += pUnits;
                            else if (pCat == StorageCategory.FreshFood || pCat == StorageCategory.CannedAndDrinks)
                                personalFood += pUnits;
                            else if (pCat == StorageCategory.Medicine)
                                personalMed += pUnits;
                        }
                    }

                    if (Il2CppSafe.IsAliveUnity(agent?.humanCharacter?.rangedWeapon) &&
                        Il2CppSafe.IsAlive(agent.humanCharacter.rangedWeapon.weaponData))
                    {
                        personalAmmo += Mathf.Max(0, BasicRangedWeapon.GetLoadedAmmo(agent.humanCharacter.rangedWeapon.weaponData));
                    }
                }
                catch { }

                effectiveAmmoMin = Mathf.Max(0, effectiveAmmoMin - personalAmmo);
                effectiveFoodMin = Mathf.Max(0, effectiveFoodMin - personalFood);
                effectiveMedMin = Mathf.Max(0, effectiveMedMin - personalMed);
            }

            var equippedPtrs = new HashSet<IntPtr>();
            try
            {
                var eq = agent?.npcData?.characterData?.characterEquipmentData;
                if (Il2CppSafe.IsAlive(eq))
                {
                    if (Il2CppSafe.IsAlive(eq.currentWeaponItemData))
                        equippedPtrs.Add(eq.currentWeaponItemData.Pointer);
                    for (int s = 0; s < 3; s++)
                    {
                        var wd = eq.GetWeaponData(s);
                        if (Il2CppSafe.IsAlive(wd)) equippedPtrs.Add(wd.Pointer);
                    }
                    if (!isSharedInv)
                    {
                        if (Il2CppSafe.IsAlive(eq.torseItemData)) equippedPtrs.Add(eq.torseItemData.Pointer);
                        if (Il2CppSafe.IsAlive(eq.hatItemData)) equippedPtrs.Add(eq.hatItemData.Pointer);
                        if (Il2CppSafe.IsAlive(eq.legItemData)) equippedPtrs.Add(eq.legItemData.Pointer);
                        if (Il2CppSafe.IsAlive(eq.feetItemData)) equippedPtrs.Add(eq.feetItemData.Pointer);
                        if (Il2CppSafe.IsAlive(eq.faceMaskItemData)) equippedPtrs.Add(eq.faceMaskItemData.Pointer);
                        if (Il2CppSafe.IsAlive(eq.backpackItemData)) equippedPtrs.Add(eq.backpackItemData.Pointer);
                    }
                }
            }
            catch { }

            if (!isSharedInv)
            {
                if (Il2CppSafe.IsAliveUnity(agent))
                {
                    try
                    {
                        var lights = agent.GetAvailableFlashLights();
                        if (lights != null)
                        {
                            for (int l = 0; l < lights.Count; l++)
                            {
                                var fl = lights[l];
                                if (Il2CppSafe.IsAlive(fl)) equippedPtrs.Add(fl.Pointer);
                            }
                        }
                    }
                    catch { }

                    var drink = FindFirstDrinkingContainerInInventory(agent, backpack);
                    if (Il2CppSafe.IsAlive(drink))
                        equippedPtrs.Add(drink.Pointer);
                }
            }
            else
            {
                bool personalHasFlashlight = Il2CppSafe.IsAlive(personalInv) && personalInv.Pointer != backpack.Pointer &&
                    Il2CppSafe.IsAlive(FindFirstAvailableFlashlightInInventory(agent, personalInv));
                if (!personalHasFlashlight)
                {
                    var sharedLight = FindFirstAvailableFlashlightInInventory(agent, backpack)
                        ?? FindFirstLightingToolInInventory(backpack);
                    if (Il2CppSafe.IsAlive(sharedLight))
                        equippedPtrs.Add(sharedLight.Pointer);
                }

                bool personalHasDrink = Il2CppSafe.IsAlive(personalInv) && personalInv.Pointer != backpack.Pointer &&
                    Il2CppSafe.IsAlive(FindFirstDrinkingContainerInInventory(agent, personalInv));
                if (!personalHasDrink)
                {
                    var sharedDrink = FindFirstDrinkingContainerInInventory(agent, backpack);
                    if (Il2CppSafe.IsAlive(sharedDrink))
                        equippedPtrs.Add(sharedDrink.Pointer);
                }
            }

            var tuples = new List<(StorageCategory cat, int units, bool equipped)>(backpack.itemList.Count);
            var ptrs = new List<IntPtr>(backpack.itemList.Count);
            for (int i = 0; i < backpack.itemList.Count; i++)
            {
                var item = backpack.itemList[i];
                if (!Il2CppSafe.IsAlive(item)) continue;
                var cat = ItemCategorizer.ClassifyItem(item);
                int units = Mathf.Max(1, Mathf.RoundToInt(item.itemNumberFloat));
                bool isEq = equippedPtrs.Contains(item.Pointer) ||
                            CompanionSurvivalSystem.IsProtectedSavedMask(st.NpcGuid, item.Pointer);
                tuples.Add((cat, units, isEq));
                ptrs.Add(item.Pointer);
            }

            var keptIdx = ComputeProtectedBackpackIndices(tuples, effectiveAmmoMin, effectiveFoodMin, effectiveMedMin);
            foreach (int idx in keptIdx)
            {
                if (idx >= 0 && idx < ptrs.Count)
                    result.Add(ptrs[idx]);
            }
            return result;
        }

        private static ItemData FindFirstContainedItem(
            Il2CppSystem.Collections.Generic.List<ItemData> candidates,
            InventoryData inv)
        {
            if (candidates == null || !Il2CppSafe.IsAlive(inv)) return null;
            for (int i = 0; i < candidates.Count; i++)
            {
                var it = candidates[i];
                if (Il2CppSafe.IsAlive(it) && InventoryTransfers.Contains(inv, it))
                    return it;
            }
            return null;
        }

        private static ItemData FindFirstAvailableFlashlightInInventory(GOAPAgent agent, InventoryData inv)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(inv)) return null;
            try
            {
                return FindFirstContainedItem(agent.GetAvailableFlashLights(), inv);
            }
            catch { }
            return null;
        }

        private static ItemData FindFirstLightingToolInInventory(InventoryData inv)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return null;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im)) return null;
            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var item = inv.itemList[i];
                if (!Il2CppSafe.IsAlive(item)) continue;
                try
                {
                    var attr = im.GetItemAttrById(item.itemId);
                    if (!Il2CppSafe.IsAliveUnity(attr)) continue;
                    bool isLight = (attr.itemFeatures != null && attr.itemFeatures.Contains(ItemFeatureType.LightingTool)) ||
                                   (attr.itemFeatureDataDic != null && attr.itemFeatureDataDic.ContainsKey(ItemFeatureType.LightingTool));
                    if (isLight)
                        return item;
                }
                catch { }
            }
            return null;
        }

        private static ItemData FindFirstDrinkingContainerInInventory(GOAPAgent agent, InventoryData inv)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null || inv.itemList.Count == 0)
                return null;

            if (Il2CppSafe.IsAliveUnity(agent))
            {
                try
                {
                    var found = FindFirstContainedItem(agent.GetDrinkingItems(), inv);
                    if (Il2CppSafe.IsAlive(found)) return found;
                }
                catch { }
            }

            if (Il2CppSafe.IsAlive(agent?.npcData))
            {
                try
                {
                    var found = FindFirstContainedItem(agent.npcData.GetDrinkingItems(), inv);
                    if (Il2CppSafe.IsAlive(found)) return found;
                }
                catch { }
            }

            try
            {
                var im = ItemManager.instance;
                if (Il2CppSafe.IsAlive(im))
                {
                    var contCand = new Il2CppSystem.Collections.Generic.List<ItemData>();
                    for (int i = 0; i < inv.itemList.Count; i++)
                    {
                        var it = inv.itemList[i];
                        if (!Il2CppSafe.IsAlive(it)) continue;
                        var attr = im.GetItemAttrById(it.itemId);
                        if (Il2CppSafe.IsAliveUnity(attr) && attr.TryCast<ItemAttr_Container>() != null)
                            contCand.Add(it);
                    }
                    if (contCand.Count > 0)
                    {
                        var validDrinks = new Il2CppSystem.Collections.Generic.List<ItemData>();
                        GOAPNPCData.AppendDrinkingContainers(contCand, validDrinks);
                        if (validDrinks.Count > 0 && Il2CppSafe.IsAlive(validDrinks[0]))
                            return validDrinks[0];
                    }
                }
            }
            catch { }

            return null;
        }

        public static void CycleBaseWorkPriority()
        {
            CompanionConfigStore.BasePriorityMode = (BaseWorkPriority)(((int)CompanionConfigStore.BasePriorityMode + 1) % 3);
            CompanionConfigStore.Save();
        }

        public static void CycleLootSweepRadius()
        {
            float cur = CompanionConfigStore.LootSweepRadius;
            CompanionConfigStore.LootSweepRadius = cur < 20f ? 24f : cur < 30f ? 36f : cur < 44f ? 48f : 14f;
            CompanionConfigStore.Save();
        }

        public static void TogglePinnedLootZone()
        {
            if (CompanionConfigStore.LootZonePinned)
            {
                CompanionConfigStore.LootZonePinned = false;
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                    "Зона сбора лута снова привязана к позиции игрока.",
                    "Loot zone reset to follow the player.",
                    "搜刮区域已恢复为跟随玩家位置。"), 4.5f);
                return;
            }

            var player = GameController.instance?.playerCharacter;
            if (!Il2CppSafe.IsAliveUnity(player)) return;
            CompanionConfigStore.LootZonePinned = true;
            CompanionConfigStore.LootZoneCenter = player.transform.position;
            CompanionConfigStore.LootZoneFloor = player.characterData != null ? player.characterData.zLevel : (sbyte)0;
            BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                $"Точка сбора лута закреплена здесь (радиус {CompanionConfigStore.LootSweepRadius:F0}м)!",
                $"Loot zone pinned here (radius {CompanionConfigStore.LootSweepRadius:F0}m)!",
                $"搜刮区域已固定在此处（半径 {CompanionConfigStore.LootSweepRadius:F0} 米）！"), 5f);
        }

        public static void TickPrioritizedBaseSystems()
        {
            switch (CompanionConfigStore.BasePriorityMode)
            {
                case BaseWorkPriority.LootFirst:
                    AutoLootSystem.TickPassiveAutoLoot();
                    BaseSortingSystem.TickActiveSortingJobs();
                    BaseChoreSystem.TickBaseChoresAndCampLife();
                    break;
                case BaseWorkPriority.CampFirst:
                    BaseChoreSystem.TickBaseChoresAndCampLife();
                    if (CompanionConfigStore.EnableBaseIndustry) BaseIndustrySystem.TickBaseIndustry(Time.unscaledTime);
                    BaseSortingSystem.TickActiveSortingJobs();
                    AutoLootSystem.TickPassiveAutoLoot();
                    break;
                default:
                    BaseSortingSystem.TickActiveSortingJobs();
                    AutoLootSystem.TickPassiveAutoLoot();
                    BaseChoreSystem.TickBaseChoresAndCampLife();
                    if (CompanionConfigStore.EnableBaseIndustry) BaseIndustrySystem.TickBaseIndustry(Time.unscaledTime);
                    break;
            }
        }

        public static string GetSpecializationLabel(CompanionSpecialization s, ModLanguage lang)
        {
            switch (s)
            {
                case CompanionSpecialization.Fighter: return CompanionConfigStore.L(lang, "Боец", "Fighter", "战斗员");
                case CompanionSpecialization.Medic: return CompanionConfigStore.L(lang, "Медик", "Medic", "医疗兵");
                case CompanionSpecialization.Quartermaster: return CompanionConfigStore.L(lang, "Снабженец", "Supplier", "军需官");
                case CompanionSpecialization.Scavenger: return CompanionConfigStore.L(lang, "Сборщик", "Scavenger", "拾荒者");
                default: return CompanionConfigStore.L(lang, "Свой", "Custom", "自定义");
            }
        }

        public static string GetLoadoutLabel(PreferredLoadout l, ModLanguage lang)
        {
            switch (l)
            {
                case PreferredLoadout.RangedFirst: return CompanionConfigStore.L(lang, "Стрелок", "Ranged", "远程优先");
                case PreferredLoadout.MeleeFirst: return CompanionConfigStore.L(lang, "Ближний", "Melee", "近战优先");
                default: return CompanionConfigStore.L(lang, "Авто-оруж.", "Auto gear", "自动武器");
            }
        }

        public static string GetBasePriorityLabel(BaseWorkPriority p, ModLanguage lang)
        {
            switch (p)
            {
                case BaseWorkPriority.LootFirst: return CompanionConfigStore.L(lang, "Лут -> Склад", "Loot -> Sort", "搜刮优先");
                case BaseWorkPriority.CampFirst: return CompanionConfigStore.L(lang, "Лагерь -> Склад", "Camp -> Sort", "内务优先");
                default: return CompanionConfigStore.L(lang, "Склад -> Лут", "Sort -> Loot", "整理优先");
            }
        }

        public static string GetRoleShortLabel(CompanionWorkRole role, ModLanguage lang)
        {
            switch (role)
            {
                case CompanionWorkRole.Sorter: return CompanionConfigStore.L(lang, "Склад", "Sorter", "仓管");
                case CompanionWorkRole.Scavenger: return CompanionConfigStore.L(lang, "Сборщик", "Scavenger", "拾荒");
                case CompanionWorkRole.Camp: return CompanionConfigStore.L(lang, "Лагерь", "Camp", "营地");
                case CompanionWorkRole.Off: return CompanionConfigStore.L(lang, "Выкл", "No work", "不干活");
                default: return CompanionConfigStore.L(lang, "Любая", "Any work", "任意");
            }
        }
    }
}
