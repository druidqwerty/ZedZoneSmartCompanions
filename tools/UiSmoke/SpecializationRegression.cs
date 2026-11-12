using System;
using System.Collections.Generic;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 2.2.0: deterministic checks for specialization presets, squad diff preview (non-mutating),
    // non-reducible backpack reserves, and rare-supply protection gates.
    internal static class SpecializationRegression
    {
        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("SpecializationRegression failed: " + msg);
        }

        public static void Run()
        {
            // 1. All 4 specialization presets have distinct coherent roles & positive reserves
            var fighter = CompanionProfileSystem.GetPreset(CompanionSpecialization.Fighter);
            var medic = CompanionProfileSystem.GetPreset(CompanionSpecialization.Medic);
            var qm = CompanionProfileSystem.GetPreset(CompanionSpecialization.Quartermaster);
            var scav = CompanionProfileSystem.GetPreset(CompanionSpecialization.Scavenger);

            Assert(fighter.Doctrine == TacticalDoctrine.Assault && fighter.Role == CompanionWorkRole.Off && fighter.ReserveAmmo >= 45, "fighter preset");
            Assert(medic.FieldMedic && medic.ReserveMed >= 4 && medic.Role == CompanionWorkRole.Camp, "medic preset");
            Assert(qm.Role == CompanionWorkRole.Sorter && qm.AutoDropoff && qm.CampChores, "quartermaster preset");
            Assert(scav.Role == CompanionWorkRole.Scavenger && scav.AutoLoot, "scavenger preset");

            // 2. Squad diff preview accurately counts changes WITHOUT mutating existing states
            var s1 = new CompanionRuntimeState { Doctrine = TacticalDoctrine.Balanced, WorkRole = CompanionWorkRole.Any, ReserveAmmoMin = 24, ReserveFoodMin = 2, ReserveMedMin = 1 };
            var s2 = new CompanionRuntimeState { Doctrine = TacticalDoctrine.Assault, WorkRole = CompanionWorkRole.Off, Loadout = PreferredLoadout.RangedFirst, AllowFieldMedic = false, AllowAmmoShare = false, AllowAutoLootNearby = false, AllowAutoDropoffAtBase = true, AllowBaseChores = false, ReserveAmmoMin = 45, ReserveFoodMin = 2, ReserveMedMin = 2 };
            var diff = CompanionProfileSystem.BuildSquadPreview(CompanionSpecialization.Fighter, new[] { s1, s2 }, ModLanguage.English);
            Assert(diff.TargetCount == 2 && diff.DoctrineChanges == 1 && diff.RoleChanges == 1 && diff.ReserveChanges == 1, "diff preview counts");
            Assert(s1.Doctrine == TacticalDoctrine.Balanced && s1.WorkRole == CompanionWorkRole.Any, "preview must never mutate state");

            // 3. Non-reducible backpack reserve keeps equipped items + minimum ammo/food/meds, releasing only surplus
            var backpack = new List<(StorageCategory cat, int units, bool equipped)>
            {
                (StorageCategory.RangedWeapons, 1, true),   // 0: equipped weapon -> keep
                (StorageCategory.AmmoAndMags, 20, false),   // 1: 20/30 ammo -> keep
                (StorageCategory.AmmoAndMags, 20, false),   // 2: reaches 40 >= 30 ammo -> keep
                (StorageCategory.AmmoAndMags, 20, false),   // 3: surplus ammo -> movable!
                (StorageCategory.CannedAndDrinks, 2, false),// 4: 2/2 food -> keep
                (StorageCategory.FreshFood, 1, false),      // 5: surplus food -> movable!
                (StorageCategory.Medicine, 1, false),       // 6: 1/2 med -> keep
                (StorageCategory.Medicine, 1, false),       // 7: 2/2 med -> keep
                (StorageCategory.Medicine, 1, false),       // 8: surplus med -> movable!
                (StorageCategory.Materials, 5, false)       // 9: materials -> movable!
            };
            var kept = CompanionProfileSystem.ComputeProtectedBackpackIndices(backpack, 30, 2, 2);
            Assert(kept.Contains(0) && kept.Contains(1) && kept.Contains(2) && !kept.Contains(3), "ammo & equipped reserve");
            Assert(kept.Contains(4) && !kept.Contains(5), "food reserve");
            Assert(kept.Contains(6) && kept.Contains(7) && !kept.Contains(8) && !kept.Contains(9), "med reserve & surplus materials");

            // 4. Rare supply classifiers
            Assert(CompanionProfileSystem.IsRareFoodItem(248, FoodKind.SteakAndRoastMeat) &&
                   !CompanionProfileSystem.IsRareFoodItem(210, FoodKind.CannedFood), "rare food classifier");
            Assert(CompanionProfileSystem.IsRareMedItem(250) && !CompanionProfileSystem.IsRareMedItem(220), "rare med classifier");
            Assert(CompanionProfileSystem.IsRareAmmoStackSize(10) && !CompanionProfileSystem.IsRareAmmoStackSize(30), "rare ammo stack classifier");

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v2.2.0 specialization presets, non-mutating squad preview, backpack reserves & rare-supply gates (12/12).");
        }
    }
}
