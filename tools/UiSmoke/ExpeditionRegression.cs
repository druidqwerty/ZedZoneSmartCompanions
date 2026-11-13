using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class ExpeditionRegression
    {
        public static void Run()
        {
            // 1. Null and empty squad inputs return empty non-null lists
            var (nullBoard, nullOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(null, 3);
            AssertEqual(0, nullBoard.Count, "Null squad board count must be 0");
            AssertEqual(0, nullOverflow.Count, "Null squad overflow count must be 0");

            var emptySquad = Array.Empty<(string, float, CompanionSpecialization, CompanionRosterMode)>();
            var (emptyBoard, emptyOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(emptySquad, 4);
            AssertEqual(0, emptyBoard.Count, "Empty squad board count must be 0");
            AssertEqual(0, emptyOverflow.Count, "Empty squad overflow count must be 0");

            // 2. 6-member squad into 3 free seats: skip CampGarrison, board wounded (<0.5f) -> Medic -> Fighter, overflow rest
            var sixSquad = new List<(string guid, float hpRatio, CompanionSpecialization spec, CompanionRosterMode roster)>
            {
                ("garrison_medic", 0.15f, CompanionSpecialization.Medic, CompanionRosterMode.CampGarrison),
                ("healthy_scav", 0.95f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                ("healthy_qm", 0.90f, CompanionSpecialization.Quartermaster, CompanionRosterMode.ActiveSquad),
                ("healthy_fighter", 0.88f, CompanionSpecialization.Fighter, CompanionRosterMode.ActiveSquad),
                ("wounded_scav", 0.32f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                ("healthy_medic", 0.92f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad)
            };
            var (board3, overflow3) = ExpeditionSystems.PlanVehicleSeatAllocation(sixSquad, 3);
            AssertEqual(3, board3.Count, "Exactly 3 active companions must board");
            AssertEqual("wounded_scav", board3[0], "Heavily wounded (<0.5f) companion must take seat #1");
            AssertEqual("healthy_medic", board3[1], "Healthy Medic must take seat #2");
            AssertEqual("healthy_fighter", board3[2], "Healthy Fighter must take seat #3");
            AssertEqual(2, overflow3.Count, "Remaining 2 active companions must enter overflow hold");
            AssertEqual("healthy_qm", overflow3[0], "Quartermaster must be first in overflow");
            AssertEqual("healthy_scav", overflow3[1], "Scavenger must be second in overflow");
            AssertFalse(board3.Contains("garrison_medic") || overflow3.Contains("garrison_medic"),
                "CampGarrison companion must stay at base and never enter board or overflow lists");

            // 3. Multiple heavily wounded companions board in ascending hpRatio order
            var multiWounded = new List<(string, float, CompanionSpecialization, CompanionRosterMode)>
            {
                ("w_fighter_42", 0.42f, CompanionSpecialization.Fighter, CompanionRosterMode.ActiveSquad),
                ("w_scav_14", 0.14f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                ("w_medic_29", 0.29f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad)
            };
            var (wBoard, wOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(multiWounded, 2);
            AssertEqual(2, wBoard.Count, "2 seats for 3 wounded companions");
            AssertEqual("w_scav_14", wBoard[0], "Most critically wounded (0.14f) must board first");
            AssertEqual("w_medic_29", wBoard[1], "Second most wounded (0.29f) must board second");
            AssertEqual(1, wOverflow.Count, "Third wounded companion overflows when only 2 seats exist");
            AssertEqual("w_fighter_42", wOverflow[0], "0.42f companion must be in overflow");

            // 4. Exact 0.5f HP boundary is NOT heavily wounded (< 0.5f strictly required)
            var boundarySquad = new List<(string, float, CompanionSpecialization, CompanionRosterMode)>
            {
                ("scav_exact_50", 0.50f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                ("medic_95", 0.95f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad),
                ("scav_49", 0.49f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad)
            };
            var (bBoard, bOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(boundarySquad, 2);
            AssertEqual("scav_49", bBoard[0], "0.49f HP is strictly < 0.5f and must board first");
            AssertEqual("medic_95", bBoard[1], "0.50f HP is not heavily wounded so Medic boards ahead of 0.50f Scavenger");
            AssertEqual("scav_exact_50", bOverflow[0], "0.50f Scavenger must overflow behind Medic");

            // 5. Zero and negative freeSeats put all ActiveSquad into overflow while still skipping CampGarrison
            var (zeroBoard, zeroOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(sixSquad, 0);
            AssertEqual(0, zeroBoard.Count, "0 free seats must produce 0 boarders");
            AssertEqual(5, zeroOverflow.Count, "All 5 ActiveSquad companions must enter overflow on 0 seats");
            var (negBoard, negOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(sixSquad, -2);
            AssertEqual(0, negBoard.Count, "Negative free seats must clamp to 0 boarders");
            AssertEqual(5, negOverflow.Count, "Negative free seats must overflow all 5 ActiveSquad companions");

            // 6. Ample seats + duplicate/blank GUID sanitization + NaN HP safety
            var dirtySquad = new List<(string, float, CompanionSpecialization, CompanionRosterMode)>
            {
                ("  ", 0.10f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad),
                ("alpha", float.NaN, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                ("ALPHA", 0.20f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad),
                ("bravo", 0.40f, CompanionSpecialization.Fighter, CompanionRosterMode.ActiveSquad)
            };
            var (cleanBoard, cleanOverflow) = ExpeditionSystems.PlanVehicleSeatAllocation(dirtySquad, 6);
            AssertEqual(2, cleanBoard.Count, "Blank and duplicate GUIDs must be filtered out");
            AssertEqual("bravo", cleanBoard[0], "Real wounded companion (0.40f) must beat NaN HP companion");
            AssertEqual("alpha", cleanBoard[1], "NaN HP must default safely to healthy");
            AssertEqual(0, cleanOverflow.Count, "Ample seats must produce empty overflow");

            // 7. FindBestFloorTransitionPortal: null, empty, and same-floor queries return -1
            Vector2 self = new Vector2(10f, 10f);
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(self, 0, -1, null), "Null portals must return -1");
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(
                self, 0, -1, Array.Empty<(Vector2, sbyte, bool, bool)>()), "Empty portals must return -1");
            var samplePortals = new List<(Vector2 pos, sbyte zLayer, bool goesUp, bool goesDown)>
            {
                (new Vector2(10f, 10f), -1, false, true),  // 0: right at selfXY on z = -1, but goesDown only (cannot go -1 -> 0)
                (new Vector2(12f, 10f), 0, true, false),   // 1: on z = 0, but goesUp only (cannot go 0 -> -1)
                (new Vector2(25f, 10f), 0, false, true),   // 2: on z = 0, goesDown, 15m away
                (new Vector2(14f, 13f), 0, false, true),   // 3: on z = 0, goesDown, 5m away (closest valid!)
                (new Vector2(11f, 10f), -1, true, true),   // 4: on z = -1, 1m away (closest valid from -1 -> 0!)
                (new Vector2(30f, 10f), -1, true, false)   // 5: on z = -1, goesUp, 20m away
            };
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(self, 0, 0, samplePortals),
                "Same floor (0 -> 0) must return -1");

            // 8. Ground to basement (0 -> -1) selects nearest z=0 portal with goesDown=true
            AssertEqual(3, ExpeditionSystems.FindBestFloorTransitionPortal(self, 0, -1, samplePortals),
                "0 -> -1 must skip z=-1 & up-only portals and pick closest down portal #3");

            // 9. Basement to ground (-1 -> 0) selects nearest z=-1 portal with goesUp=true
            AssertEqual(4, ExpeditionSystems.FindBestFloorTransitionPortal(self, -1, 0, samplePortals),
                "-1 -> 0 must skip z=0 portals and pick closest z=-1 up portal #4");

            // 10. Missing suitable portal on current floor returns -1
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(self, -2, -1, samplePortals),
                "No portal on z=-2 must return -1");
            var downOnlyOnBasement = new List<(Vector2, sbyte, bool, bool)> { (new Vector2(11f, 10f), -1, false, true) };
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(self, -1, 0, downOnlyOnBasement),
                "Down-only portal on z=-1 cannot ascend to z=0 and must return -1");

            // 11. NaN coordinate hardening in FindBestFloorTransitionPortal
            AssertEqual(-1, ExpeditionSystems.FindBestFloorTransitionPortal(
                new Vector2(float.NaN, 10f), 0, -1, samplePortals), "NaN selfPos must return -1");
            var nanPortals = new List<(Vector2, sbyte, bool, bool)>
            {
                (new Vector2(float.NaN, 10f), 0, true, true),
                (new Vector2(15f, 10f), 0, true, true)
            };
            AssertEqual(1, ExpeditionSystems.FindBestFloorTransitionPortal(self, 0, -1, nanPortals),
                "NaN portal entry must be skipped in favor of valid portal #1");

            // 12. Pet synergy summary localization & bounds clamping (RU / EN / ZH, zero emojis)
            string ruZero = ExpeditionSystems.FormatPetsSummary(0, 3, ModLanguage.Russian);
            string enActive = ExpeditionSystems.FormatPetsSummary(2, 5, ModLanguage.English);
            string zhActive = ExpeditionSystems.FormatPetsSummary(3, 1, ModLanguage.Chinese);
            AssertTrue(ruZero.Contains("нет активных", StringComparison.Ordinal), "RU empty pet summary");
            AssertTrue(enActive.Contains("2", StringComparison.Ordinal) && enActive.Contains("hungry: 2", StringComparison.Ordinal),
                "EN pet summary must clamp hungry count to active count");
            AssertTrue(zhActive.Contains("3", StringComparison.Ordinal) && zhActive.Contains("1", StringComparison.Ordinal),
                "ZH pet summary must report active and hungry counts");
            AssertTrue(!string.IsNullOrEmpty(ExpeditionSystems.GetActivePlayerPetsSummary(ModLanguage.Russian)),
                "Live GetActivePlayerPetsSummary must safely return non-empty summary in menu fixture");

            const string passMsg = "[UiSmoke] PASS v3.0.0 vehicle seat allocation, multi-floor portal routing & pet synergy rules";
            UiSmokePlugin.Logger?.LogInfo(passMsg);
            Console.WriteLine(passMsg);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[ExpeditionRegression] ASSERT TRUE FAILED: " + message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException("[ExpeditionRegression] ASSERT FALSE FAILED: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[ExpeditionRegression] ASSERT EQUAL FAILED ({message}): expected '{expected}', got '{actual}'");
            }
        }
    }
}
