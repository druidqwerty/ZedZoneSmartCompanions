using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class TacticalSquadSystem
    {
        private static readonly List<Vector2> ScratchAllyPos = new List<Vector2>(16);
        private static readonly List<Vector2> ScratchAheadPos = new List<Vector2>(16);

        public static int IssueOrder(
            GOAPAgent specificAgent,
            TacticalOrderKind order,
            Vector2? explicitAnchor = null,
            Vector2? explicitDir = null,
            bool fromNetwork = false,
            bool quiet = false)
        {
            var player = GameController.instance?.playerCharacter;
            Vector2 playerPos = Il2CppSafe.IsAliveUnity(player) ? (Vector2)player.transform.position : Vector2.zero;
            int playerZ = Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData) ? player.characterData.zLevel : 0;
            Vector2 aimDir = explicitDir ?? ResolveLeaderForward(player);
            if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector2.right;
            aimDir.Normalize();

            var targets = new List<GOAPAgent>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                targets.Add(specificAgent);
            else
                targets.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

            if (targets.Count == 0) return 0;

            float now = Time.unscaledTime;
            float followDist = GetFollowDistanceMeters(CompanionConfigStore.FollowDistance);
            var placedAnchors = new List<Vector2>(targets.Count);
            int applied = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                var ag = targets[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter))
                    continue;
                if (!fromNetwork && MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                Vector2 selfPos = ag.humanCharacter.transform.position;
                int selfZ = Il2CppSafe.IsAlive(ag.npcData) ? ag.npcData.zLevel : playerZ;

                if (order == TacticalOrderKind.FallBackHere && st.HasAnyPhysicalTask)
                    BaseWorkManager.CancelWork(ag, st);

                st.ActiveOrder = order;
                st.OrderIssuedTime = now;
                st.LastOrderMoveProgressTime = now;
                st.LastOrderMovePos = selfPos;
                st.OrderDirection = aimDir;
                st.OrderBlockReason = TacticalOrderBlockReason.None;
                st.FallBackArrived = false;

                if (order == TacticalOrderKind.Follow)
                {
                    st.OrderAnchorPos = Vector2.zero;
                    st.OrderAnchorZ = selfZ;
                }
                else if (order == TacticalOrderKind.FallBackHere)
                {
                    Vector2 baseAnchor = explicitAnchor ?? playerPos;
                    Vector2 slot = targets.Count > 1
                        ? ComputeFormationSlot(applied, targets.Count, baseAnchor, aimDir,
                            CompanionConfigStore.ActiveFormation == SquadFormation.Free ? SquadFormation.Wedge : CompanionConfigStore.ActiveFormation,
                            Mathf.Min(2.4f, followDist))
                        : baseAnchor;
                    slot = ComputeAllySeparatedPoint(slot, selfPos, placedAnchors, 1.20f);
                    st.OrderAnchorPos = slot;
                    st.OrderAnchorZ = explicitAnchor.HasValue ? selfZ : playerZ;
                    placedAnchors.Add(slot);
                }
                else
                {
                    // HoldPosition or CoverDirection: hold current spot (separated if stacked) or explicit anchor
                    Vector2 rawAnchor = explicitAnchor ?? selfPos;
                    Vector2 slot = ComputeAllySeparatedPoint(rawAnchor, selfPos, placedAnchors, 1.15f);
                    st.OrderAnchorPos = slot;
                    st.OrderAnchorZ = selfZ;
                    placedAnchors.Add(slot);
                }

                if (!fromNetwork)
                    MultiplayerSyncSystem.SyncCompanionStateChange(st);

                applied++;
            }

            if (applied > 0 && !quiet && !fromNetwork)
            {
                BaseSortingSystem.SetBanner(GetOrderBannerText(order, applied, CompanionConfigStore.EffectiveLanguage), 5f);
            }
            return applied;
        }

        public static void AdjustSectorThreatScore(
            GOAPAgent agent, CompanionRuntimeState st, Vector2 selfPos, Vector2 targetPos, ref float score)
        {
            if (st == null || score <= 0f) return;
            if (st.ActiveOrder == TacticalOrderKind.CoverDirection && st.OrderDirection.sqrMagnitude > 0.01f)
            {
                Vector2 toEnemy = targetPos - selfPos;
                if (toEnemy.sqrMagnitude > 0.04f)
                {
                    float dot = Vector2.Dot(st.OrderDirection.normalized, toEnemy.normalized);
                    if (dot >= 0.35f) score *= 1.65f;
                    else if (dot < -0.25f && toEnemy.magnitude > 4.2f) score *= 0.65f;
                }
            }
        }

        public static bool TickTacticalGoalOverride(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;
            if (!agent.IsPlayerTeammate()) return false;

            if (!CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var st) || st == null)
                return false;

            if (st.HasAnyPhysicalTask)
            {
                if (st.ActiveOrder != TacticalOrderKind.Follow)
                    st.NoteTacticalBlock(TacticalOrderBlockReason.PausedForBaseWork);
                return false;
            }

            if (st.ActiveOrder == TacticalOrderKind.Follow)
                return false;

            var hc = agent.humanCharacter;
            int myZ = agent.npcData.zLevel;
            if (myZ != st.OrderAnchorZ)
            {
                st.NoteTacticalBlock(TacticalOrderBlockReason.DifferentZLevel);
                return false;
            }

            Vector2 selfPos = hc.transform.position;
            float distToAnchor = Vector2.Distance(selfPos, st.OrderAnchorPos);
            var threat = agent.mostThreateningCharacter;
            bool hasThreat = Il2CppSafe.IsAliveUnity(threat) && !threat.deadFlag;

            if (st.Doctrine == TacticalDoctrine.Passive || agent.IsTeammateCombatSuppressed())
                st.NoteTacticalBlock(TacticalOrderBlockReason.SuppressedPassive);
            else if (st.OrderBlockReason == TacticalOrderBlockReason.SuppressedPassive ||
                     st.OrderBlockReason == TacticalOrderBlockReason.PausedForBaseWork ||
                     st.OrderBlockReason == TacticalOrderBlockReason.DifferentZLevel)
                st.OrderBlockReason = TacticalOrderBlockReason.None;

            // 1. FallBackHere before arrival: actively break off and run to anchor unless cornered in melee (<1.85m)
            if (st.ActiveOrder == TacticalOrderKind.FallBackHere && !st.FallBackArrived)
            {
                if (distToAnchor <= 1.85f)
                {
                    st.FallBackArrived = true;
                    hc.movementInput = Vector2.zero;
                    agent.hasNavigationTarget = false;
                    if (st.OrderBlockReason == TacticalOrderBlockReason.PathBlockedToAnchor)
                        st.OrderBlockReason = TacticalOrderBlockReason.None;
                    return !hasThreat;
                }

                bool meleeCornered = hasThreat && Vector2.Distance(selfPos, threat.transform.position) < 1.85f;
                if (!meleeCornered)
                {
                    SteerTowardAnchor(agent, st, selfPos, distToAnchor, shouldRun: true);
                    return true;
                }
                return false;
            }

            // 2. HoldPosition / CoverDirection / arrived FallBackHere:
            // If fighting, let native combat shoot/fight unless companion chased >4.2m off their anchor!
            if (hasThreat)
            {
                if (distToAnchor > 4.2f)
                {
                    SteerTowardAnchor(agent, st, selfPos, distToAnchor, shouldRun: true);
                    return true;
                }
                return false;
            }

            // Out of combat: stay on anchor & watch sector if CoverDirection
            if (distToAnchor > 1.45f)
            {
                SteerTowardAnchor(agent, st, selfPos, distToAnchor, shouldRun: distToAnchor > 5.5f);
                return true;
            }

            hc.movementInput = Vector2.zero;
            agent.hasNavigationTarget = false;
            if (st.OrderBlockReason == TacticalOrderBlockReason.PathBlockedToAnchor)
                st.OrderBlockReason = TacticalOrderBlockReason.None;

            if (st.ActiveOrder == TacticalOrderKind.CoverDirection && st.OrderDirection.sqrMagnitude > 0.01f)
            {
                try { agent.LookAtPositionSmoothCompat(selfPos + st.OrderDirection * 6f, 10f); } catch { }
            }
            return true;
        }

        private static void SteerTowardAnchor(GOAPAgent agent, CompanionRuntimeState st, Vector2 selfPos, float dist, bool shouldRun)
        {
            float now = Time.unscaledTime;
            if ((selfPos - st.LastOrderMovePos).sqrMagnitude >= 0.35f * 0.35f)
            {
                st.LastOrderMovePos = selfPos;
                st.LastOrderMoveProgressTime = now;
                if (st.OrderBlockReason == TacticalOrderBlockReason.PathBlockedToAnchor)
                    st.OrderBlockReason = TacticalOrderBlockReason.None;
            }
            else if (now - st.LastOrderMoveProgressTime > 4.2f || (agent.cantFindPath && now - st.OrderIssuedTime > 2.0f))
            {
                st.NoteTacticalBlock(TacticalOrderBlockReason.PathBlockedToAnchor);
            }

            try
            {
                agent.MoveTo(st.OrderAnchorPos, 1.15f, shouldRun);
                Vector2 lookPos = st.ActiveOrder == TacticalOrderKind.CoverDirection
                    ? selfPos + st.OrderDirection * 6f
                    : st.OrderAnchorPos;
                agent.LookAtPositionSmoothCompat(lookPos, 11f);
            }
            catch { }
        }
    }
}
