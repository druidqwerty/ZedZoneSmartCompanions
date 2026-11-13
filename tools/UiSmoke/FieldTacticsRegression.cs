using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class FieldTacticsRegression
    {
        public static void Run()
        {
            Vector2 target = new Vector2(50f, 50f);
            var safeFriendlies = new List<Vector2>
            {
                new Vector2(40f, 50f), // 10m away (>= 6.5m)
                new Vector2(50f, 41f)  // 9m away (>= 6.5m)
            };
            var safeChests = new List<Vector2>
            {
                new Vector2(25f, 50f), // 25m away (>= 18m)
                new Vector2(50f, 72f)  // 22m away (>= 18m)
            };
            var validCluster = new List<Vector2>
            {
                new Vector2(50.5f, 50.0f), // 0.5m
                new Vector2(52.0f, 51.5f), // 2.5m
                new Vector2(46.2f, 50.0f)  // 3.8m (<= 4.5m)
            };

            // 1. Block when < 3 enemies (0, 1, and 2 enemies) without boss
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, Array.Empty<Vector2>(), safeFriendlies, safeChests, isBossTarget: false),
                "GrenadeSafetyGate must block when 0 hostiles are near target");
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, new[] { new Vector2(51f, 50f), new Vector2(49f, 51f) }, safeFriendlies, safeChests, isBossTarget: false),
                "GrenadeSafetyGate must block when only 2 hostiles (<3) are near target without boss");

            // 2. Block when hostile count is >= 3 globally, but only 2 are inside the 4.5m blast cluster
            var scatteredHostiles = new List<Vector2>
            {
                new Vector2(50.2f, 50.0f), // 0.2m (inside)
                new Vector2(53.0f, 50.0f), // 3.0m (inside)
                new Vector2(54.8f, 50.0f), // 4.8m (outside 4.5m!)
                new Vector2(60.0f, 50.0f)  // 10.0m (outside 4.5m!)
            };
            AssertEqual(
                GrenadeSafetyBlockReason.InsufficientHostileCluster,
                FieldTacticsSystem.EvaluateGrenadeSafetyGate(
                    target, scatteredHostiles, safeFriendlies, safeChests, isBossTarget: false, out int scatteredCount),
                "Expected InsufficientHostileCluster when 3rd hostile is at 4.8m");
            AssertEqual(2, scatteredCount, "Expected exactly 2 hostiles counted inside 4.5m radius");

            // 3. Block when an ally/player/pet is at 5m (< 6.5m), even with 3 clustered enemies
            var closeFriendly5m = new List<Vector2> { new Vector2(55f, 50f) }; // 5.0m
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, validCluster, closeFriendly5m, safeChests, isBossTarget: false),
                "GrenadeSafetyGate must block when friendly/pet is 5m (<6.5m) from target");
            AssertEqual(
                GrenadeSafetyBlockReason.TooCloseToFriendly,
                FieldTacticsSystem.EvaluateGrenadeSafetyGate(
                    target, validCluster, closeFriendly5m, safeChests, isBossTarget: true, out _),
                "GrenadeSafetyGate must still block boss grenade when friendly is at 5m");

            // 4. Block when a marked base chest is at 15m (< 18.0m), even with 3 clustered enemies or boss
            var closeBaseChest15m = new List<Vector2> { new Vector2(65f, 50f) }; // 15.0m
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, validCluster, safeFriendlies, closeBaseChest15m, isBossTarget: false),
                "GrenadeSafetyGate must block when marked base chest is at 15m (<18m)");
            AssertEqual(
                GrenadeSafetyBlockReason.TooCloseToBaseChest,
                FieldTacticsSystem.EvaluateGrenadeSafetyGate(
                    target, validCluster, safeFriendlies, closeBaseChest15m, isBossTarget: true, out _),
                "GrenadeSafetyGate must block boss grenade when marked base chest is at 15m");

            // 5. Allow when >= 3 enemies are in 4.5m radius and friendlies (>=6.5m) & base chests (>=18m) are clear
            AssertTrue(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, validCluster, safeFriendlies, safeChests, isBossTarget: false),
                "GrenadeSafetyGate must allow throw with >=3 clustered enemies away from friendlies and base chests");

            // 6. Allow against a single boss target when friendlies & base chests are clear
            AssertTrue(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, new[] { target }, safeFriendlies, safeChests, isBossTarget: true),
                "GrenadeSafetyGate must allow throw against boss target when friendlies and base chests are safe");

            // 7. Boundary precision checks around 6.5m friendly & 18.0m chest gates, plus NaN guard
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, validCluster, new[] { new Vector2(56.45f, 50f) }, safeChests, isBossTarget: false),
                "Friendly at 6.45m must block (<6.5m)");
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    target, validCluster, safeFriendlies, new[] { new Vector2(67.9f, 50f) }, isBossTarget: false),
                "Marked chest at 17.9m must block (<18.0m)");
            AssertFalse(
                FieldTacticsSystem.CanThrowOffensiveGrenadeSafely(
                    new Vector2(float.NaN, 50f), validCluster, null, null, isBossTarget: true),
                "NaN target position must never pass GrenadeSafetyGate");

            // 8. ComputeCursorFanPositions for 6 fighters: finite coordinates and pairwise separation >= 1.15f
            Vector2 cursorPos = new Vector2(30f, 15f);
            Vector2 leaderPos = new Vector2(20f, 15f);
            List<Vector2> fan6 = FieldTacticsSystem.ComputeCursorFanPositions(cursorPos, leaderPos, 6, 1.25f);
            AssertEqual(6, fan6.Count, "ComputeCursorFanPositions(6) must return 6 slots");
            VerifyFanFiniteAndSeparated(fan6, minAllowedPairwise: 1.15f, label: "6-fighter standard fan");

            // 9. ComputeCursorFanPositions degenerate/edge cases (count=0, count=1, cursor==leader for 6 fighters)
            AssertEqual(0, FieldTacticsSystem.ComputeCursorFanPositions(cursorPos, leaderPos, 0).Count, "count=0 must return empty list");
            List<Vector2> fan1 = FieldTacticsSystem.ComputeCursorFanPositions(cursorPos, leaderPos, 1, 1.25f);
            AssertEqual(1, fan1.Count, "count=1 must return 1 slot");
            AssertTrue(Vector2.Distance(fan1[0], cursorPos) < 0.001f, "Single fighter slot must sit directly on cursorWorldPos");

            List<Vector2> coincidentFan6 = FieldTacticsSystem.ComputeCursorFanPositions(cursorPos, cursorPos, 6, 1.25f);
            AssertEqual(6, coincidentFan6.Count, "Coincident cursor==leader fan must still return 6 slots");
            VerifyFanFiniteAndSeparated(coincidentFan6, minAllowedPairwise: 1.15f, label: "6-fighter coincident cursor==leader fan");

            // 10. Cover doctrine eligibility & 6.5s cooldown rules
            AssertTrue(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.Balanced), "Balanced must take cover under ranged fire");
            AssertTrue(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.Bodyguard), "Bodyguard must take cover under ranged fire");
            AssertTrue(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.SniperKite), "SniperKite must take cover under ranged fire");
            AssertFalse(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.Assault), "Assault must not auto-trigger cover rule");
            AssertFalse(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.StealthMelee), "StealthMelee must not auto-trigger cover rule");
            AssertFalse(FieldTacticsSystem.IsDoctrineEligibleForCover(TacticalDoctrine.Passive), "Passive must not auto-trigger cover rule");
            AssertFalse(FieldTacticsSystem.IsCooldownReady(10.0f, 16.4f, FieldTacticsSystem.TakeCoverCooldownSeconds), "Cover 6.5s cooldown must block at +6.4s");
            AssertTrue(FieldTacticsSystem.IsCooldownReady(10.0f, 16.5f, FieldTacticsSystem.TakeCoverCooldownSeconds), "Cover 6.5s cooldown must allow at +6.5s");

            // 11. Medic <32% HP rescue gate & 22s cooldown rules
            AssertTrue(
                FieldTacticsSystem.ShouldMedicRescueTarget(CompanionSpecialization.Medic, 31f, 100f, isDowned: false, lastRescueAt: -999f, now: 50f),
                "Medic must rescue teammate at 31% HP (<32%)");
            AssertFalse(
                FieldTacticsSystem.ShouldMedicRescueTarget(CompanionSpecialization.Medic, 33f, 100f, isDowned: false, lastRescueAt: -999f, now: 50f),
                "Medic must not trigger critical rescue at 33% HP (>=32%) when standing");
            AssertFalse(
                FieldTacticsSystem.ShouldMedicRescueTarget(CompanionSpecialization.Medic, 20f, 100f, isDowned: true, lastRescueAt: 40f, now: 61.5f),
                "Medic 22s rescue cooldown must block at +21.5s");
            AssertTrue(
                FieldTacticsSystem.ShouldMedicRescueTarget(CompanionSpecialization.Medic, 20f, 100f, isDowned: true, lastRescueAt: 40f, now: 62.0f),
                "Medic 22s rescue cooldown must allow at +22.0s");

            Console.WriteLine("[UiSmoke] PASS v2.5.0 cursor fan, GrenadeSafetyGate & tactical cover/rescue rules");
        }

        private static void VerifyFanFiniteAndSeparated(List<Vector2> points, float minAllowedPairwise, string label)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y))
                    throw new InvalidOperationException($"[FieldTacticsRegression] {label}: slot #{i} has non-finite coordinates ({p.x}, {p.y})");

                for (int j = i + 1; j < points.Count; j++)
                {
                    float dist = Vector2.Distance(p, points[j]);
                    if (dist < minAllowedPairwise)
                    {
                        throw new InvalidOperationException(
                            $"[FieldTacticsRegression] {label}: slots #{i} and #{j} too close ({dist:F3}m < {minAllowedPairwise:F2}m)");
                    }
                }
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[FieldTacticsRegression] ASSERT TRUE FAILED: " + message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException("[FieldTacticsRegression] ASSERT FALSE FAILED: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[FieldTacticsRegression] ASSERT EQUAL FAILED ({message}): expected '{expected}', got '{actual}'");
            }
        }
    }
}
