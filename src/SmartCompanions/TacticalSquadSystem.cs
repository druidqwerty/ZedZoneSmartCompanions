using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// 2.1.0: Tactical squad formations, follow distances, positional orders
    /// (Hold Position, Fall Back Here, Cover Sector), narrow-passage/ally spacing,
    /// and configurable wounded / low-ammo disengage rules.
    /// </summary>
    public static partial class TacticalSquadSystem
    {
        private static Vector2 _cachedLeaderForward = Vector2.right;

        public static float GetFollowDistanceMeters(FollowDistanceMode mode)
        {
            switch (mode)
            {
                case FollowDistanceMode.Close: return 2.5f;
                case FollowDistanceMode.Wide: return 6.5f;
                case FollowDistanceMode.Scout: return 9.0f;
                default: return 4.0f;
            }
        }

        public static void CycleFormation()
        {
            CompanionConfigStore.ActiveFormation = (SquadFormation)(((int)CompanionConfigStore.ActiveFormation + 1) % 5);
            CompanionConfigStore.Save();
            MultiplayerSyncSystem.SyncSquadTacticsConfig();
        }

        public static void CycleFollowDistance()
        {
            CompanionConfigStore.FollowDistance = (FollowDistanceMode)(((int)CompanionConfigStore.FollowDistance + 1) % 4);
            CompanionConfigStore.Save();
            MultiplayerSyncSystem.SyncSquadTacticsConfig();
        }

        public static void CycleRetreatHpThreshold()
        {
            int cur = CompanionConfigStore.RetreatHpThresholdPercent;
            CompanionConfigStore.RetreatHpThresholdPercent = cur <= 0 ? 25 : cur < 30 ? 30 : cur < 50 ? 50 : 0;
            CompanionConfigStore.Save();
        }

        public static void CycleLowAmmoRetreat()
        {
            CompanionConfigStore.LowAmmoRetreat = (LowAmmoRetreatMode)(((int)CompanionConfigStore.LowAmmoRetreat + 1) % 3);
            CompanionConfigStore.Save();
        }

        /// <summary>Pure formation slot calculator (deterministic, unit-tested in UiSmoke &amp; LiveSmoke).</summary>
        public static Vector2 ComputeFormationSlot(
            int slotIndex, int squadCount, Vector2 leaderPos, Vector2 leaderForward,
            SquadFormation formation, float followDist)
        {
            Vector2 fwd = leaderForward.sqrMagnitude > 0.001f ? leaderForward.normalized : Vector2.right;
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            int idx = Mathf.Max(0, slotIndex);
            int count = Mathf.Max(1, squadCount);
            float dist = Mathf.Clamp(followDist, 1.8f, 12f);

            switch (formation)
            {
                case SquadFormation.Wedge:
                {
                    int rank = (idx / 2) + 1;
                    float sideSign = (idx % 2 == 0) ? -1f : 1f;
                    float back = dist * 0.72f + (rank - 1) * 1.35f;
                    float lateral = sideSign * (1.25f + (rank - 1) * 1.05f);
                    return leaderPos - fwd * back + right * lateral;
                }
                case SquadFormation.Line:
                {
                    float centered = idx - (count - 1) * 0.5f;
                    if (Mathf.Abs(centered) < 0.2f) centered = 0.65f; // keep leader's aim line clear
                    return leaderPos - fwd * Mathf.Max(1.6f, dist * 0.55f) + right * (centered * 1.55f);
                }
                case SquadFormation.Column:
                {
                    float back = dist * 0.75f + idx * 1.45f;
                    float slightOffset = (idx % 2 == 0 ? -0.28f : 0.28f);
                    return leaderPos - fwd * back + right * slightOffset;
                }
                case SquadFormation.GuardRing:
                {
                    float angleRad = (idx * (Mathf.PI * 2f) / count) + Mathf.Atan2(fwd.y, fwd.x) + Mathf.PI * 0.5f;
                    return leaderPos + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * dist;
                }
                default:
                {
                    float spread = (idx - (count - 1) * 0.5f) * 0.9f;
                    return leaderPos - fwd * dist + right * spread;
                }
            }
        }

        /// <summary>Pure ally-separation helper: nudges target point away from stacked teammates.</summary>
        public static Vector2 ComputeAllySeparatedPoint(
            Vector2 desiredPos, Vector2 selfPos, IReadOnlyList<Vector2> otherAllyPositions, float minSpacing = 1.25f)
        {
            if (otherAllyPositions == null || otherAllyPositions.Count == 0) return desiredPos;
            Vector2 push = Vector2.zero;
            float minSq = minSpacing * minSpacing;
            for (int i = 0; i < otherAllyPositions.Count; i++)
            {
                Vector2 delta = desiredPos - otherAllyPositions[i];
                float sq = delta.sqrMagnitude;
                if (sq >= minSq) continue;
                Vector2 dir = sq > 0.0004f ? delta.normalized : ((selfPos - otherAllyPositions[i]).sqrMagnitude > 0.001f
                    ? (selfPos - otherAllyPositions[i]).normalized : new Vector2(0.707f, 0.707f));
                float deficit = minSpacing - Mathf.Sqrt(Mathf.Max(0f, sq));
                push += dir * (deficit + 0.15f);
            }
            if (push.sqrMagnitude > 4.0f) push = push.normalized * 2.0f;
            return desiredPos + push;
        }

        /// <summary>Pure narrow-passage yield check: trailing companion yields single-file in tight doorways.</summary>
        public static bool ShouldYieldInNarrowPassage(
            int myIndex, Vector2 selfPos, Vector2 targetPos,
            IReadOnlyList<Vector2> aheadAllyPositions, bool lateralClearLeft, bool lateralClearRight)
        {
            if (myIndex <= 0 || aheadAllyPositions == null || aheadAllyPositions.Count == 0) return false;
            if (lateralClearLeft || lateralClearRight) return false;
            Vector2 toTarget = targetPos - selfPos;
            if (toTarget.sqrMagnitude < 1.2f) return false;
            Vector2 fwd = toTarget.normalized;
            for (int i = 0; i < aheadAllyPositions.Count; i++)
            {
                Vector2 toAlly = aheadAllyPositions[i] - selfPos;
                float dist = toAlly.magnitude;
                if (dist > 0.15f && dist < 1.65f && Vector2.Dot(fwd, toAlly / dist) > 0.55f)
                    return true;
            }
            return false;
        }

        /// <summary>Pure retreat condition evaluator for wounded or low-ammo companions.</summary>
        public static TacticalOrderBlockReason EvaluateRetreatNeed(
            float hp, float maxHp, int retreatHpPct,
            bool usingRanged, int loadedAmmo, int reserveAmmo,
            LowAmmoRetreatMode ammoMode, float enemyDist)
        {
            if (retreatHpPct > 0 && maxHp > 5f && (hp / maxHp) * 100f < retreatHpPct)
                return TacticalOrderBlockReason.RetreatingWounded;

            if (ammoMode != LowAmmoRetreatMode.Off && usingRanged && enemyDist > 2.3f)
            {
                int total = Mathf.Max(0, loadedAmmo) + Mathf.Max(0, reserveAmmo);
                if (ammoMode == LowAmmoRetreatMode.EmptyOnly && total <= 0)
                    return TacticalOrderBlockReason.RetreatingLowAmmo;
                if (ammoMode == LowAmmoRetreatMode.LowReserve && total <= 5)
                    return TacticalOrderBlockReason.RetreatingLowAmmo;
            }
            return TacticalOrderBlockReason.None;
        }

        public static Vector2 ResolveLeaderForward(HumanCharacterController leader)
        {
            if (Il2CppSafe.IsAliveUnity(leader))
            {
                try
                {
                    if (Il2CppSafe.IsAliveUnity(leader.rangedWeapon))
                    {
                        Vector2 aim = leader.rangedWeapon.transform.right;
                        if (aim.sqrMagnitude > 0.01f)
                        {
                            _cachedLeaderForward = aim.normalized;
                            return _cachedLeaderForward;
                        }
                    }
                    if (leader.movementInput.sqrMagnitude > 0.04f)
                    {
                        _cachedLeaderForward = leader.movementInput.normalized;
                        return _cachedLeaderForward;
                    }
                }
                catch { }
            }
            return _cachedLeaderForward;
        }
    }
}
