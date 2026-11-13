using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// v3.0.1 Corridor &amp; Indoor Navigation Helper:
    /// Fixes repeated wall-bumping in narrow hallways/doorways by:
    /// 1) Recording a breadcrumb trail of walkable leader positions for single-file corridor following;
    /// 2) Checking full body-collider clearance (0.44m instead of native MoveTo's 0.10m thin ray) so
    ///    corners and door jambs always route through MoveTo_PathFinding (A*) instead of MoveTo_Direct;
    /// 3) Detecting wall-wedging (<0.09m movement over 0.38s) and applying tangential obstacle slide + A*;
    /// 4) Probing 7 open angles before combat kiting/strafing/retreating instead of backing into walls.
    /// </summary>
    public static class CorridorNavigationHelper
    {
        public const float BodyClearanceWidth = 0.44f;
        public const float BodyOverlapRadius = 0.36f;

        private struct AgentNavTracker
        {
            public Vector2 LastPos;
            public float LastSampleAt;
            public float StuckSeconds;
            public Vector2 StabilizedGoal;
            public float LastPathCallAt;
            public float UnwedgeUntil;
            public Vector2 UnwedgeDir;
        }

        private static readonly Dictionary<IntPtr, AgentNavTracker> Trackers = new Dictionary<IntPtr, AgentNavTracker>(32);
        private static readonly List<Vector2> LeaderTrail = new List<Vector2>(24);
        private static int _leaderTrailZ = -999;

        public static void RecordLeaderBreadcrumb(Vector2 leaderPos, int zLevel)
        {
            if (!IsFinite(leaderPos)) return;
            if (zLevel != _leaderTrailZ)
            {
                _leaderTrailZ = zLevel;
                LeaderTrail.Clear();
            }
            if (LeaderTrail.Count == 0 || (LeaderTrail[0] - leaderPos).sqrMagnitude >= 0.85f * 0.85f)
            {
                LeaderTrail.Insert(0, leaderPos);
                if (LeaderTrail.Count > 20)
                    LeaderTrail.RemoveAt(LeaderTrail.Count - 1);
            }
        }

        public static Vector2 GetTrailSingleFileSlot(Vector2 leaderPos, Vector2 leaderFwd, int slotIndex, float spacing = 1.35f)
        {
            float targetAlong = Mathf.Max(1.35f, (slotIndex + 1) * spacing);
            float accum = 0f;
            Vector2 prev = leaderPos;
            for (int i = 0; i < LeaderTrail.Count; i++)
            {
                Vector2 pt = LeaderTrail[i];
                float seg = Vector2.Distance(prev, pt);
                if (accum + seg >= targetAlong && seg > 0.01f)
                {
                    float t = (targetAlong - accum) / seg;
                    return Vector2.Lerp(prev, pt, Mathf.Clamp01(t));
                }
                accum += seg;
                prev = pt;
            }
            return leaderPos - (leaderFwd.sqrMagnitude > 0.001f ? leaderFwd.normalized : Vector2.right) * targetAlong;
        }

        public static bool IsNarrowCorridorAt(GOAPAgent agent, Vector2 pos, Vector2 forwardDir, float halfCheck = 1.25f)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return false;
            Vector2 fwd = forwardDir.sqrMagnitude > 0.001f ? forwardDir.normalized : Vector2.right;
            Vector2 left = new Vector2(-fwd.y, fwd.x);
            try
            {
                bool clearL = agent.IsPathClear(pos, pos + left * halfCheck, 0.22f);
                bool clearR = agent.IsPathClear(pos, pos - left * halfCheck, 0.22f);
                return !clearL || !clearR;
            }
            catch { return false; }
        }

        public static Vector2 ClampToWalkableTarget(GOAPAgent agent, Vector2 desired, Vector2 fallbackWalkable)
        {
            if (!IsFinite(desired)) return fallbackWalkable;
            if (!Il2CppSafe.IsAliveUnity(agent)) return desired;
            try
            {
                var mask = GOAPAgent.moveObstacleLayermask;
                if (!Physics2D.OverlapCircle(desired, BodyOverlapRadius, mask))
                    return desired;

                // Probe pull-back steps toward fallbackWalkable (e.g. leader centerline in a hallway)
                for (int s = 1; s <= 4; s++)
                {
                    Vector2 cand = Vector2.Lerp(desired, fallbackWalkable, s * 0.25f);
                    if (!Physics2D.OverlapCircle(cand, BodyOverlapRadius, mask))
                        return cand;
                }
            }
            catch { }
            return fallbackWalkable;
        }

        public static void SmartMoveTo(GOAPAgent agent, Vector2 targetPos, float stopDist, bool runOrAvoid)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || !IsFinite(targetPos))
                return;

            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;
            float dist = Vector2.Distance(selfPos, targetPos);
            if (dist <= stopDist)
            {
                hc.movementInput = Vector2.zero;
                return;
            }

            float now = Time.time;
            IntPtr key = agent.Pointer;
            Trackers.TryGetValue(key, out var tr);

            if (tr.LastSampleAt > 0f)
            {
                float dt = now - tr.LastSampleAt;
                if (dt > 0.45f)
                {
                    // Reset tracker after any idle/combat gap so first move frame never falsely triggers unwedge!
                    tr.StuckSeconds = 0f;
                    tr.LastPos = selfPos;
                    tr.LastSampleAt = now;
                }
                else if (dt >= 0.12f)
                {
                    float moved = Vector2.Distance(selfPos, tr.LastPos);
                    if (moved < 0.05f) tr.StuckSeconds += dt;
                    else tr.StuckSeconds = 0f;
                    tr.LastPos = selfPos;
                    tr.LastSampleAt = now;
                }
            }
            else { tr.LastPos = selfPos; tr.LastSampleAt = now; }

            // Active corner-unwedge slide if bumping a doorframe/wall (>0.36s motionless)
            if (tr.StuckSeconds >= 0.36f && now >= tr.UnwedgeUntil)
            {
                tr.StuckSeconds = 0f;
                Vector2 toGoal = (targetPos - selfPos).normalized;
                Vector2 perpA = new Vector2(-toGoal.y, toGoal.x);
                Vector2 perpB = -perpA;
                bool aClear = false, bClear = false;
                try
                {
                    aClear = agent.IsPathClear(selfPos, selfPos + perpA * 0.95f, 0.28f);
                    bClear = agent.IsPathClear(selfPos, selfPos + perpB * 0.95f, 0.28f);
                }
                catch { }
                tr.UnwedgeDir = aClear && !bClear ? (perpA * 0.85f + toGoal * 0.35f).normalized
                              : bClear && !aClear ? (perpB * 0.85f + toGoal * 0.35f).normalized
                              : ((Time.frameCount & 1) == 0 ? perpA : perpB);
                tr.UnwedgeUntil = now + 0.28f;
                agent.pathfindingBackoffUntil = 0f;
                agent.hasNavigationTarget = false;
            }

            if (now < tr.UnwedgeUntil)
            {
                hc.movementInput = tr.UnwedgeDir;
                Trackers[key] = tr;
                return;
            }

            if ((tr.StabilizedGoal - targetPos).sqrMagnitude > 0.45f * 0.45f || tr.LastPathCallAt <= 0f)
                tr.StabilizedGoal = targetPos;

            try
            {
                tr.LastPathCallAt = now;
                agent.MoveTo(tr.StabilizedGoal, stopDist, runOrAvoid);
            }
            catch { }
            Trackers[key] = tr;
        }

        private static readonly float[] ProbeAnglesDeg = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        /// <summary>
        /// Probes 7 clear directions before short combat kiting/strafing/retreating steps so companions
        /// never back blindly into room/corridor walls via MoveTo_Direct.
        /// </summary>
        public static bool SmartStepInClearDirection(GOAPAgent agent, Vector2 selfPos, Vector2 preferredDir, float stepDist, bool run)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter)) return false;
            Vector2 baseDir = preferredDir.sqrMagnitude > 0.001f ? preferredDir.normalized : Vector2.left;
            var mask = GOAPAgent.moveObstacleLayermask;

            for (int i = 0; i < ProbeAnglesDeg.Length; i++)
            {
                Vector2 dir = RotateVec2(baseDir, ProbeAnglesDeg[i]);
                Vector2 cand = selfPos + dir * stepDist;
                try
                {
                    if (!Physics2D.OverlapCircle(cand, BodyOverlapRadius, mask) &&
                        agent.IsPathClear(selfPos, cand, 0.34f))
                    {
                        agent.MoveTo_Direct(cand, run, true, false);
                        return true;
                    }
                }
                catch { }
            }

            // Boxed in on all immediate angles in a narrow corridor -> hold ground and shoot cleanly instead of ramming walls!
            agent.humanCharacter.movementInput = Vector2.zero;
            return false;
        }

        public static Vector2 RotateVec2(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static bool IsFinite(Vector2 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
    }

    /// <summary>
    /// Native GOAPAgent.MoveTo checks IsPathClear(self, target, 0.10f) — an exact 0.10f literal (0x3dcccccd)!
    /// Do NOT patch GOAPAgent.MoveTo itself (it returns Il2CppSystem.ValueTuple`2 which breaks IL2CPP Harmony ABI).
    /// Patching ONLY bool IsPathClear when distanceOffset == 0.10f on player teammates widens MoveTo's wall-corner
    /// check to 0.36m human shoulder width so native MoveTo automatically routes via A* waypoints around doorframes!
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.IsPathClear))]
    internal static class CorridorBodyWidthIsPathClearPatch
    {
        [HarmonyLib.HarmonyPrefix]
        private static void Prefix(GOAPAgent __instance, ref float distanceOffset)
        {
            if (Mathf.Abs(distanceOffset - 0.10f) < 0.005f &&
                Il2CppSafe.IsAliveUnity(__instance) && __instance.IsPlayerTeammate())
            {
                distanceOffset = 0.36f;
            }
        }
    }
}
