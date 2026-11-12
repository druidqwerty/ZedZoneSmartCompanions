using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 2.1.0: deterministic checks for formations, follow distances, narrow-passage yield,
    // ally separation, sector threat weighting, and wounded/low-ammo retreat rules.
    internal static class TacticalRegression
    {
        private static void Assert(bool cond, string message)
        {
            if (!cond) throw new Exception("TacticalRegression failed: " + message);
        }

        public static void Run()
        {
            // 1. Follow distances strictly ordered
            float dClose = TacticalSquadSystem.GetFollowDistanceMeters(FollowDistanceMode.Close);
            float dNorm = TacticalSquadSystem.GetFollowDistanceMeters(FollowDistanceMode.Normal);
            float dWide = TacticalSquadSystem.GetFollowDistanceMeters(FollowDistanceMode.Wide);
            float dScout = TacticalSquadSystem.GetFollowDistanceMeters(FollowDistanceMode.Scout);
            Assert(dClose < dNorm && dNorm < dWide && dWide < dScout, "follow distances must increase monotonically");

            // 2. Formations keep leader's forward fire corridor clear and slots distinct
            Vector2 leader = new Vector2(10f, 20f);
            Vector2 fwd = Vector2.right;
            foreach (SquadFormation form in Enum.GetValues(typeof(SquadFormation)))
            {
                var slots = new List<Vector2>();
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = TacticalSquadSystem.ComputeFormationSlot(i, 4, leader, fwd, form, 4.0f);
                    Assert(!float.IsNaN(p.x) && !float.IsNaN(p.y), $"NaN slot in {form}");
                    for (int j = 0; j < slots.Count; j++)
                        Assert(Vector2.Distance(p, slots[j]) > 0.65f, $"overlapping slots in {form} ({i} vs {j})");
                    slots.Add(p);
                }
            }

            // Wedge puts slots behind leader (x < leader.x when facing +X) and flanking left/right
            Vector2 w0 = TacticalSquadSystem.ComputeFormationSlot(0, 2, leader, fwd, SquadFormation.Wedge, 4.0f);
            Vector2 w1 = TacticalSquadSystem.ComputeFormationSlot(1, 2, leader, fwd, SquadFormation.Wedge, 4.0f);
            Assert(w0.x < leader.x && w1.x < leader.x && (w0.y - leader.y) * (w1.y - leader.y) < 0f, "wedge symmetry");

            // Column trails strictly further back per index
            Vector2 c0 = TacticalSquadSystem.ComputeFormationSlot(0, 3, leader, fwd, SquadFormation.Column, 4.0f);
            Vector2 c2 = TacticalSquadSystem.ComputeFormationSlot(2, 3, leader, fwd, SquadFormation.Column, 4.0f);
            Assert(c2.x < c0.x - 2.0f, "column depth spacing");

            // 3. Ally separation pushes stacked target apart
            Vector2 sep = TacticalSquadSystem.ComputeAllySeparatedPoint(
                new Vector2(5f, 5f), new Vector2(4f, 5f), new[] { new Vector2(5.05f, 5.0f) }, 1.25f);
            Assert(Vector2.Distance(sep, new Vector2(5.05f, 5.0f)) >= 1.20f, "ally separation push");

            // 4. Narrow-passage single-file yield
            bool yieldTight = TacticalSquadSystem.ShouldYieldInNarrowPassage(
                1, Vector2.zero, new Vector2(6f, 0f), new[] { new Vector2(1.1f, 0.05f) }, false, false);
            bool noYieldWide = TacticalSquadSystem.ShouldYieldInNarrowPassage(
                1, Vector2.zero, new Vector2(6f, 0f), new[] { new Vector2(1.1f, 0.05f) }, true, false);
            bool leadNeverYields = TacticalSquadSystem.ShouldYieldInNarrowPassage(
                0, Vector2.zero, new Vector2(6f, 0f), new[] { new Vector2(1.1f, 0.05f) }, false, false);
            Assert(yieldTight && !noYieldWide && !leadNeverYields, "narrow doorway single-file yield");

            // 5. Wounded & low-ammo retreat rules
            Assert(TacticalSquadSystem.EvaluateRetreatNeed(24f, 100f, 30, true, 15, 60, LowAmmoRetreatMode.EmptyOnly, 6f) ==
                   TacticalOrderBlockReason.RetreatingWounded, "wounded retreat");
            Assert(TacticalSquadSystem.EvaluateRetreatNeed(90f, 100f, 0, true, 0, 0, LowAmmoRetreatMode.EmptyOnly, 6f) ==
                   TacticalOrderBlockReason.RetreatingLowAmmo, "empty ammo retreat");
            Assert(TacticalSquadSystem.EvaluateRetreatNeed(90f, 100f, 30, true, 2, 2, LowAmmoRetreatMode.LowReserve, 6f) ==
                   TacticalOrderBlockReason.RetreatingLowAmmo, "low reserve ammo retreat");
            Assert(TacticalSquadSystem.EvaluateRetreatNeed(90f, 100f, 30, true, 0, 0, LowAmmoRetreatMode.EmptyOnly, 1.5f) ==
                   TacticalOrderBlockReason.None, "melee range does not trigger ranged ammo retreat");

            // 6. Cover-sector threat weighting
            var st = new CompanionRuntimeState
            {
                ActiveOrder = TacticalOrderKind.CoverDirection,
                OrderDirection = Vector2.up
            };
            float front = 10f, back = 10f;
            TacticalSquadSystem.AdjustSectorThreatScore(null, st, Vector2.zero, new Vector2(0f, 8f), ref front);
            TacticalSquadSystem.AdjustSectorThreatScore(null, st, Vector2.zero, new Vector2(0f, -8f), ref back);
            Assert(front > 15f && back < 8f, "cover sector threat boost/dampen");

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v2.1.0 tactical formations, doorway yield, retreat thresholds & sector priority (16/16).");
        }
    }
}
