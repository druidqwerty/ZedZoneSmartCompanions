using System;
using System.Collections.Generic;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class SurvivalRegression
    {
        public static void Run()
        {
            // 1. Bio-protection mask gate: clean air & exact 3.5f radiation boundary must be false
            AssertFalse(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: false, radiationDose: 0f, inHazardZone: false),
                "Clean air (no blood rain, 0 rad, no hazard) must not require bio mask");
            AssertFalse(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: false, radiationDose: 3.5f, inHazardZone: false),
                "Radiation at exact 3.5f boundary must not trigger (> 3.5f required)");
            AssertFalse(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: false, radiationDose: float.NaN, inHazardZone: false),
                "NaN radiationDose must not trigger bio mask in clean air");

            // 2. Bio-protection mask gate: triggers on Blood Rain, radiation > 3.5f, or hazard zone
            AssertTrue(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: true, radiationDose: 0f, inHazardZone: false),
                "Blood Rain must trigger bio-protection mask");
            AssertTrue(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: false, radiationDose: 3.51f, inHazardZone: false),
                "Radiation > 3.5f (3.51f) must trigger bio-protection mask");
            AssertTrue(
                CompanionSurvivalSystem.ShouldWearBioProtectionMask(isBloodRaining: false, radiationDose: 0f, inHazardZone: true),
                "Active spore/hazard zone must trigger bio-protection mask");

            // 3. Sleep shift rotation: null, empty, or solo companion must NEVER sleep without a sentry
            AssertEqual(-1, CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(null), "Null squad must return -1");
            AssertEqual(
                -1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(Array.Empty<(float, bool, bool)>()),
                "Empty squad must return -1");
            AssertEqual(
                -1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(new[] { (0.08f, false, false) }),
                "Solo exhausted companion must NEVER fall asleep without a sentry");

            // 4. Sleep shift rotation: out of two exhausted companions, the most tired sleeps and the second holds watch
            var twoTiredAwake = new (float fatigueRatio, bool alreadySleeping, bool inCombat)[]
            {
                (0.24f, false, false),
                (0.11f, false, false)
            };
            AssertEqual(
                1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(twoTiredAwake),
                "Most tired awake companion (index 1, 0.11f) must be selected first");

            // 5. Sleep shift rotation: once one of two companions is sleeping, the remaining sentry MUST stay awake
            var oneAlreadyAsleepOfTwo = new (float fatigueRatio, bool alreadySleeping, bool inCombat)[]
            {
                (0.15f, false, false),
                (0.11f, true, false)
            };
            AssertEqual(
                -1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(oneAlreadyAsleepOfTwo),
                "Last awake sentry in a 2-companion squad must never be put to sleep");

            // 6. Sleep shift rotation: 3-companion squad with 1 asleep and 2 tired awake allows 1 more sleeper (1 sentry remains)
            var threeSquadOneAsleep = new (float fatigueRatio, bool alreadySleeping, bool inCombat)[]
            {
                (0.05f, true, false),
                (0.25f, false, false),
                (0.14f, false, false)
            };
            AssertEqual(
                2,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(threeSquadOneAsleep),
                "3-companion squad with 1 asleep must pick index 2 (0.14f) while index 1 remains sentry");

            // 7. Sleep shift rotation: exact 0.28f fatigue threshold & rested squad return -1
            var restedOrBoundary = new (float fatigueRatio, bool alreadySleeping, bool inCombat)[]
            {
                (0.28f, false, false),
                (0.65f, false, false)
            };
            AssertEqual(
                -1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(restedOrBoundary),
                "Fatigue at exact 0.28f boundary must not trigger sleep (< 0.28f required)");

            // 8. Sleep shift rotation: combat immediately blocks any sleep candidate
            var squadInCombat = new (float fatigueRatio, bool alreadySleeping, bool inCombat)[]
            {
                (0.10f, false, false),
                (0.20f, false, true)
            };
            AssertEqual(
                -1,
                CompanionSurvivalSystem.SelectSleepShiftCandidateIndex(squadInCombat),
                "Any squad member in combat must block sleep shift selection (-1)");

            // 9. Debuff remedy selector: common remedies (220, 222, 234, 276) are always preferred over rare serums
            AssertEqual(
                222,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(new[] { 249, 222, 277 }, allowRareSerums: true, lifeThreatening: true),
                "Common remedy (222) must be chosen before rare serums (249, 277) even when rare serums are allowed");
            AssertEqual(
                276,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(new[] { 276 }, allowRareSerums: false, lifeThreatening: false),
                "Common remedy (276) must be allowed even when rare serums are disabled and non-lethal");

            // 10. Debuff remedy selector: rare serums (249, 250, 251, 252, 277) strictly require allowRareSerums && lifeThreatening
            int[] rareOnly = { 249, 250, 251, 252, 277 };
            AssertEqual(
                0,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(rareOnly, allowRareSerums: false, lifeThreatening: true),
                "Rare serums must be blocked when allowRareSerums is false");
            AssertEqual(
                0,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(rareOnly, allowRareSerums: true, lifeThreatening: false),
                "Rare serums must be blocked when condition is not lifeThreatening");
            AssertEqual(
                249,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(rareOnly, allowRareSerums: true, lifeThreatening: true),
                "Rare serum (249) must be selected when allowRareSerums && lifeThreatening and no common meds exist");
            AssertEqual(
                0,
                CompanionSurvivalSystem.ChooseDebuffRemedyItemId(Array.Empty<int>(), allowRareSerums: true, lifeThreatening: true),
                "Empty med list must return 0");

            // 11. Saved everyday mask protection gate (prevents BaseSorting from depositing swapped-off bandana/mask)
            const string testGuid = "sc_v260_saved_mask_test";
            AssertFalse(CompanionSurvivalSystem.IsProtectedSavedMask(null, new IntPtr(0x1234)), "Null guid must return false");
            AssertFalse(CompanionSurvivalSystem.IsProtectedSavedMask("", new IntPtr(0x1234)), "Empty guid must return false");
            AssertFalse(CompanionSurvivalSystem.IsProtectedSavedMask(testGuid, IntPtr.Zero), "Zero pointer must return false");

            var im = ItemManager.instance;
            var fixtureAttr = im != null ? im.GetItemAttrById(220) : null;
            if (fixtureAttr != null)
            {
                var savedMaskFixture = im.GetRandomDataByAttr(fixtureAttr, false, true);
                var otherItemFixture = im.GetRandomDataByAttr(fixtureAttr, false, true);
                try
                {
                    CompanionSurvivalSystem.SavedEverydayMaskByGuid[testGuid] = savedMaskFixture;
                    AssertTrue(
                        CompanionSurvivalSystem.IsProtectedSavedMask(testGuid, savedMaskFixture.Pointer),
                        "Saved everyday mask pointer must be protected for its owning companion");
                    AssertFalse(
                        CompanionSurvivalSystem.IsProtectedSavedMask("sc_v260_other_npc", savedMaskFixture.Pointer),
                        "Saved everyday mask must not match a different companion GUID");
                    AssertFalse(
                        CompanionSurvivalSystem.IsProtectedSavedMask(testGuid, otherItemFixture.Pointer),
                        "Unrelated item pointer must not be marked as protected mask");
                }
                finally
                {
                    CompanionSurvivalSystem.SavedEverydayMaskByGuid.Remove(testGuid);
                }
                AssertFalse(
                    CompanionSurvivalSystem.IsProtectedSavedMask(testGuid, savedMaskFixture.Pointer),
                    "Removed everyday mask entry must no longer be marked protected");
            }

            const string passMsg = "[UiSmoke] PASS v2.6.0 bio-mask protection, sentry sleep rotation & rare-remedy gates";
            UiSmokePlugin.Logger?.LogInfo(passMsg);
            Console.WriteLine(passMsg);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[SurvivalRegression] ASSERT TRUE FAILED: " + message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException("[SurvivalRegression] ASSERT FALSE FAILED: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[SurvivalRegression] ASSERT EQUAL FAILED ({message}): expected '{expected}', got '{actual}'");
            }
        }
    }
}
