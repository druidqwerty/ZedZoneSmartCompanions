using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public struct SpecializationPreset
    {
        public CompanionSpecialization Kind;
        public TacticalDoctrine Doctrine;
        public CompanionWorkRole Role;
        public PreferredLoadout Loadout;
        public bool FieldMedic;
        public bool AmmoShare;
        public bool AutoLoot;
        public bool AutoDropoff;
        public bool CampChores;
        public int ReserveAmmo;
        public int ReserveFood;
        public int ReserveMed;
    }

    public struct ProfileDiffPreview
    {
        public CompanionSpecialization Spec;
        public int TargetCount;
        public int DoctrineChanges;
        public int RoleChanges;
        public int LoadoutChanges;
        public int ToggleChanges;
        public int ReserveChanges;
        public string SummaryText;
    }

    /// <summary>
    /// 2.2.0: Companion specialization presets (Fighter, Medic, Quartermaster, Scavenger),
    /// squad-wide diff preview before applying, non-reducible backpack reserves,
    /// separate rare-supply permissions, configurable loot zones &amp; base work priorities.
    /// </summary>
    public static partial class CompanionProfileSystem
    {
        private static readonly HashSet<int> RareDishItemIds = new HashSet<int>
        {
            248, // Spicy Meat Steak
            268, // Rabbit & Radish Stew
            289, // Grilled Salmon Steak
            291, // Seared Tuna Steak
            296, // Salt-Roasted Chicken
            297, // Chicken & Mushroom Soup
            298  // Spicy Chicken & Potatoes
        };

        private static readonly HashSet<int> RareMedicalItemIds = new HashSet<int>
        {
            223, // Blood Bag
            249, // Spore Suppressant
            250, 251, 252, // Red / Green / Blue Dose
            270, 271, 272, 273, 274, 275, // Enhancement Serums & Potions
            277  // Anti-Radiation Medicine
        };

        public static SpecializationPreset GetPreset(CompanionSpecialization spec)
        {
            switch (spec)
            {
                case CompanionSpecialization.Fighter:
                    return new SpecializationPreset
                    {
                        Kind = spec,
                        Doctrine = TacticalDoctrine.Assault,
                        Role = CompanionWorkRole.Off,
                        Loadout = PreferredLoadout.RangedFirst,
                        FieldMedic = false,
                        AmmoShare = false,
                        AutoLoot = false,
                        AutoDropoff = true,
                        CampChores = false,
                        ReserveAmmo = 45,
                        ReserveFood = 2,
                        ReserveMed = 2
                    };
                case CompanionSpecialization.Medic:
                    return new SpecializationPreset
                    {
                        Kind = spec,
                        Doctrine = TacticalDoctrine.Bodyguard,
                        Role = CompanionWorkRole.Camp,
                        Loadout = PreferredLoadout.RangedFirst,
                        FieldMedic = true,
                        AmmoShare = true,
                        AutoLoot = false,
                        AutoDropoff = true,
                        CampChores = true,
                        ReserveAmmo = 24,
                        ReserveFood = 2,
                        ReserveMed = 4
                    };
                case CompanionSpecialization.Quartermaster:
                    return new SpecializationPreset
                    {
                        Kind = spec,
                        Doctrine = TacticalDoctrine.Balanced,
                        Role = CompanionWorkRole.Sorter,
                        Loadout = PreferredLoadout.Auto,
                        FieldMedic = true,
                        AmmoShare = true,
                        AutoLoot = false,
                        AutoDropoff = true,
                        CampChores = true,
                        ReserveAmmo = 30,
                        ReserveFood = 3,
                        ReserveMed = 2
                    };
                case CompanionSpecialization.Scavenger:
                default:
                    return new SpecializationPreset
                    {
                        Kind = CompanionSpecialization.Scavenger,
                        Doctrine = TacticalDoctrine.Balanced,
                        Role = CompanionWorkRole.Scavenger,
                        Loadout = PreferredLoadout.Auto,
                        FieldMedic = true,
                        AmmoShare = true,
                        AutoLoot = true,
                        AutoDropoff = true,
                        CampChores = false,
                        ReserveAmmo = 24,
                        ReserveFood = 2,
                        ReserveMed = 1
                    };
            }
        }

        /// <summary>Pure squad diff preview calculated BEFORE mutating any companion state.</summary>
        public static ProfileDiffPreview BuildSquadPreview(
            CompanionSpecialization spec, IReadOnlyList<CompanionRuntimeState> states, ModLanguage lang)
        {
            var p = GetPreset(spec);
            var diff = new ProfileDiffPreview { Spec = spec, TargetCount = states?.Count ?? 0 };
            if (states != null)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    var s = states[i];
                    if (s == null) continue;
                    if (s.Doctrine != p.Doctrine) diff.DoctrineChanges++;
                    if (s.WorkRole != p.Role) diff.RoleChanges++;
                    if (s.Loadout != p.Loadout) diff.LoadoutChanges++;
                    if (s.AllowFieldMedic != p.FieldMedic || s.AllowAmmoShare != p.AmmoShare ||
                        s.AllowAutoLootNearby != p.AutoLoot || s.AllowAutoDropoffAtBase != p.AutoDropoff ||
                        s.AllowBaseChores != p.CampChores)
                        diff.ToggleChanges++;
                    if (s.ReserveAmmoMin != p.ReserveAmmo || s.ReserveFoodMin != p.ReserveFood || s.ReserveMedMin != p.ReserveMed)
                        diff.ReserveChanges++;
                }
            }

            string specLabel = GetSpecializationLabel(spec, lang);
            string roleLabel = BaseWorkManager.GetRoleLabel(p.Role, lang);
            string loadLabel = GetLoadoutLabel(p.Loadout, lang);
            diff.SummaryText = CompanionConfigStore.L(lang,
                $"Профиль «{specLabel}» ({diff.TargetCount} чел.): Роль -> {roleLabel} ({diff.RoleChanges} изм.), Снаряж. -> {loadLabel}, Доктрина ({diff.DoctrineChanges} изм.), Резерв БК/Еда/Мед -> {p.ReserveAmmo}/{p.ReserveFood}/{p.ReserveMed} ({diff.ReserveChanges} изм.)",
                $"Profile '{specLabel}' ({diff.TargetCount}): Role -> {roleLabel} ({diff.RoleChanges} chg), Gear -> {loadLabel}, Doctrine ({diff.DoctrineChanges} chg), Reserve Ammo/Food/Med -> {p.ReserveAmmo}/{p.ReserveFood}/{p.ReserveMed} ({diff.ReserveChanges} chg)",
                $"专精「{specLabel}」（{diff.TargetCount}人）：职责->{roleLabel}（{diff.RoleChanges}项变更），装备->{loadLabel}，战术（{diff.DoctrineChanges}项），保底弹药/食物/医疗->{p.ReserveAmmo}/{p.ReserveFood}/{p.ReserveMed}（{diff.ReserveChanges}项）");
            return diff;
        }

        public static bool IsRareFoodItem(int itemId, FoodKind kind) =>
            kind == FoodKind.SteakAndRoastMeat || RareDishItemIds.Contains(itemId);

        public static bool IsRareMedItem(int itemId) => RareMedicalItemIds.Contains(itemId);

        public static bool IsRareAmmoStackSize(int maxStack) => maxStack > 0 && maxStack <= 16;

        /// <summary>
        /// Pure non-reducible reserve selector: returns indices in the backpack list that MUST stay on the companion
        /// to satisfy equipped items and minimum ammo, food, and medicine reserves.
        /// </summary>
        public static HashSet<int> ComputeProtectedBackpackIndices(
            IReadOnlyList<(StorageCategory cat, int units, bool equipped)> items,
            int reserveAmmo, int reserveFood, int reserveMed)
        {
            var kept = new HashSet<int>();
            if (items == null || items.Count == 0) return kept;

            int haveAmmo = 0, haveFood = 0, haveMed = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int u = Mathf.Max(1, it.units);
                if (it.equipped)
                {
                    kept.Add(i);
                    continue;
                }
                if (it.cat == StorageCategory.AmmoAndMags && haveAmmo < reserveAmmo)
                {
                    kept.Add(i);
                    haveAmmo += u;
                }
                else if ((it.cat == StorageCategory.FreshFood || it.cat == StorageCategory.CannedAndDrinks) && haveFood < reserveFood)
                {
                    kept.Add(i);
                    haveFood += u;
                }
                else if (it.cat == StorageCategory.Medicine && haveMed < reserveMed)
                {
                    kept.Add(i);
                    haveMed += u;
                }
            }
            return kept;
        }
    }
}
