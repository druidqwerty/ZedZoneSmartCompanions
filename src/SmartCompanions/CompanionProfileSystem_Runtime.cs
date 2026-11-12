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
                }
            }
            catch { }

            var tuples = new List<(StorageCategory cat, int units, bool equipped)>(backpack.itemList.Count);
            var ptrs = new List<IntPtr>(backpack.itemList.Count);
            for (int i = 0; i < backpack.itemList.Count; i++)
            {
                var item = backpack.itemList[i];
                if (!Il2CppSafe.IsAlive(item)) continue;
                var cat = ItemCategorizer.ClassifyItem(item);
                int units = Mathf.Max(1, Mathf.RoundToInt(item.itemNumberFloat));
                bool isEq = equippedPtrs.Contains(item.Pointer);
                tuples.Add((cat, units, isEq));
                ptrs.Add(item.Pointer);
            }

            var keptIdx = ComputeProtectedBackpackIndices(tuples, st.ReserveAmmoMin, st.ReserveFoodMin, st.ReserveMedMin);
            foreach (int idx in keptIdx)
            {
                if (idx >= 0 && idx < ptrs.Count)
                    result.Add(ptrs[idx]);
            }
            return result;
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
                    BaseSortingSystem.TickActiveSortingJobs();
                    AutoLootSystem.TickPassiveAutoLoot();
                    break;
                default:
                    BaseSortingSystem.TickActiveSortingJobs();
                    AutoLootSystem.TickPassiveAutoLoot();
                    BaseChoreSystem.TickBaseChoresAndCampLife();
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
    }
}
