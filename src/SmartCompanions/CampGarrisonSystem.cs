using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// 2.4.0: Camp Garrison mode (stay at base perimeter without following raids)
    /// and two-step physical Safe Dismissal with zero-teleport gear handover.
    /// </summary>
    public static partial class CampGarrisonSystem
    {
        public static string PendingDismissConfirmGuid = "";
        public static bool PendingDismissReturnGear = true;

        public static void ToggleCampGarrison(GOAPAgent agent, CompanionRuntimeState st, bool fromNetwork = false, bool quiet = false)
        {
            if (st == null) return;
            if (!fromNetwork && Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData) &&
                MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(agent.npcData))
                return;

            if (st.RosterMode == CompanionRosterMode.CampGarrison)
            {
                st.RosterMode = CompanionRosterMode.ActiveSquad;
                st.GarrisonAnchorSettled = false;
                if (st.IsChoreWalkActive)
                    BaseWorkManager.CancelWork(agent, st);
            }
            else
            {
                st.RosterMode = CompanionRosterMode.CampGarrison;
                ResolveAndPinHomeAnchor(agent, st);
                if (Il2CppSafe.IsAliveUnity(agent))
                {
                    if (Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                        agent.humanCharacter.movementInput = Vector2.zero;
                    agent.hasNavigationTarget = false;
                    agent.executeSelectPriorityGoalFlag = true;
                }
            }

            CompanionConfigStore.Save();
            if (!fromNetwork)
            {
                MultiplayerSyncSystem.SyncCompanionStateChange(st);
                if (!quiet)
                {
                    var lang = CompanionConfigStore.EffectiveLanguage;
                    string modeLbl = st.RosterMode == CompanionRosterMode.CampGarrison
                        ? CompanionConfigStore.L(lang, "переведён в гарнизон базы", "assigned to camp garrison", "已设为营地驻守")
                        : CompanionConfigStore.L(lang, "возвращён в походный отряд", "returned to active squad", "已恢复随队出征");
                    BaseSortingSystem.SetBanner($"{st.LastKnownName}: {modeLbl}.", 4.5f);
                }
            }
        }

        public static bool ShouldStayAnchoredBehind(GOAPNPCData npcData)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return false;
            string guid = npcData.npcGUID ?? "";
            if (string.IsNullOrEmpty(guid)) return false;
            if (!CompanionConfigStore.CompanionStates.TryGetValue(guid, out var st) || st == null)
                return false;
            if (st.HasAnyPhysicalTask)
                return false;
            return st.RosterMode == CompanionRosterMode.CampGarrison ||
                   st.IsTransientVehicleOverflow ||
                   st.ActiveOrder == TacticalOrderKind.HoldPosition ||
                   st.ActiveOrder == TacticalOrderKind.CoverDirection;
        }

        public static void SyncAnchoredNpcDataPosition(GOAPAgent agent, Vector2 anchorPos, int anchorZ)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return;
                agent.npcData.npcStayPosition = new Vector3(anchorPos.x, anchorPos.y, anchorZ);
                if (Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                {
                    Vector2 cur = agent.humanCharacter.transform.position;
                    if (Vector2.Distance(cur, anchorPos) <= 4.2f)
                    {
                        agent.npcData.npcPosition = cur;
                        if (Il2CppSafe.IsAlive(agent.npcData.characterData))
                            agent.npcData.characterData.characterPosition = cur;
                    }
                }
            }
            catch { }
        }

        public static void ResolveAndPinHomeAnchor(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            bool hasLiveBody = Il2CppSafe.IsAliveUnity(agent?.humanCharacter);
            Vector2 selfPos = hasLiveBody
                ? (Vector2)agent.humanCharacter.transform.position
                : (Il2CppSafe.IsAlive(agent?.npcData) && agent.npcData.npcPosition.sqrMagnitude > 0.001f
                    ? agent.npcData.npcPosition
                    : st.HomeAnchorPos);
            if (!hasLiveBody && selfPos.sqrMagnitude < 0.001f)
                return;

            int selfZ = Il2CppSafe.IsAlive(agent?.npcData) ? agent.npcData.zLevel : st.HomeAnchorZ;

            Vector2 bestAnchor = selfPos;
            float bestDist = 18.0f;
            var boxes = BaseSortingSystem.CachedContainers;
            if (boxes != null && boxes.Count > 0)
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    if (b?.Rule == null || !b.Rule.IsMarkedByPlayer) continue;
                    float d = Vector2.Distance(selfPos, b.WorldPos);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestAnchor = b.WorldPos;
                    }
                }
            }
            else
            {
                // Early boot before first container scan: inspect saved marked player chest positions within 20m.
                bestDist = 20.0f;
                foreach (var kv in CompanionConfigStore.ContainerRules)
                {
                    var r = kv.Value;
                    if (r == null || !r.IsMarkedByPlayer) continue;
                    Vector2 chestPos = r.LastKnownPos;
                    if (chestPos.sqrMagnitude <= 0.01f && !TryParseContainerRulePos(r.ContainerId, selfZ, out chestPos))
                        continue;
                    float d = Vector2.Distance(selfPos, chestPos);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestAnchor = chestPos;
                    }
                }
            }
            st.HomeAnchorPos = bestAnchor;
            st.HomeAnchorZ = selfZ;
            st.HasHomeAnchor = true;
            st.GarrisonAnchorSettled = Vector2.Distance(selfPos, bestAnchor) <= 4.2f;
            SyncAnchoredNpcDataPosition(agent, bestAnchor, selfZ);
        }

        private static bool TryParseContainerRulePos(string containerId, int expectedZ, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (string.IsNullOrEmpty(containerId)) return false;
            try
            {
                if (containerId.StartsWith("box_", StringComparison.OrdinalIgnoreCase) ||
                    containerId.StartsWith("itembox_", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = containerId.Split('_');
                    if (parts.Length >= 4 &&
                        int.TryParse(parts[1], out int rx100) &&
                        int.TryParse(parts[2], out int ry100) &&
                        int.TryParse(parts[3], out int z) &&
                        z == expectedZ)
                    {
                        Vector2 cand = new Vector2(rx100 / 100f, ry100 / 100f);
                        if (cand.sqrMagnitude > 0.01f)
                        {
                            pos = cand;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool TickTransientVehicleOverflowFrame(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null || !st.IsTransientVehicleOverflow || st.HasAnyPhysicalTask)
                return false;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;

            var hc = agent.humanCharacter;
            var leader = Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
                ? agent.leaderCharacter
                : GameController.instance?.playerCharacter;

            // Self-heal immediately if leader exited the vehicle or companion boarded
            if (hc.isDriving || Il2CppSafe.IsAliveUnity(hc.drivingVehicle) ||
                !Il2CppSafe.IsAliveUnity(leader) || (!leader.isDriving && !Il2CppSafe.IsAliveUnity(leader.drivingVehicle)))
            {
                st.IsTransientVehicleOverflow = false;
                return false;
            }

            if (st.TransientVehicleHoldPos.sqrMagnitude < 0.001f)
            {
                st.TransientVehicleHoldPos = hc.transform.position;
                st.TransientVehicleHoldZ = agent.npcData.zLevel;
            }
            if (agent.npcData.zLevel != st.TransientVehicleHoldZ)
                return false;

            Vector2 selfPos = hc.transform.position;
            float dist = Vector2.Distance(selfPos, st.TransientVehicleHoldPos);
            SyncAnchoredNpcDataPosition(agent, st.TransientVehicleHoldPos, st.TransientVehicleHoldZ);

            bool hasThreat = Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag;
            if (hasThreat && dist <= 7.5f)
                return false;

            if (dist > 3.6f)
            {
                try { CorridorNavigationHelper.SmartMoveTo(agent, st.TransientVehicleHoldPos, 1.8f, dist > 7.5f); } catch { }
                return true;
            }
            hc.movementInput = Vector2.zero;
            agent.hasNavigationTarget = false;
            return !hasThreat;
        }

        private static bool TryPrepareGarrisonTick(
            GOAPAgent agent, out CompanionRuntimeState st, out HumanCharacterController hc, out float dist, out bool hasThreat)
        {
            st = null;
            hc = null;
            dist = 0f;
            hasThreat = false;

            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag ||
                agent.humanCharacter.isSleeping)
                return false;
            if (!agent.IsPlayerTeammate()) return false;
            if (!CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out st) || st == null)
                return false;
            if (st.RosterMode != CompanionRosterMode.CampGarrison || st.HasAnyPhysicalTask)
                return false;
            if (!st.HasHomeAnchor)
                ResolveAndPinHomeAnchor(agent, st);
            if (!st.HasHomeAnchor || agent.npcData.zLevel != st.HomeAnchorZ)
                return false;

            hc = agent.humanCharacter;
            SyncAnchoredNpcDataPosition(agent, st.HomeAnchorPos, st.HomeAnchorZ);
            hasThreat = Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag;
            dist = Vector2.Distance(hc.transform.position, st.HomeAnchorPos);
            return true;
        }

        /// <summary>
        /// Called from PhysicalTaskUpdateGoalOverride.Prefix in GOAPAgent.UpdateGoal:
        /// steers a drifted guard back to camp (&gt; 1.85m), while returning false when settled so
        /// native StayAlive (bandaging/healing) and SatisfyNeeds (eating/drinking) still run.
        /// </summary>
        public static bool TickGarrisonGoalOverride(GOAPAgent agent)
        {
            if (!TryPrepareGarrisonTick(agent, out var st, out var hc, out float dist, out bool hasThreat))
                return false;
            if (hasThreat && dist <= 7.5f)
                return false; // Let native ranged/melee combat engage enemies near camp!

            if (dist > 1.85f)
            {
                try { CorridorNavigationHelper.SmartMoveTo(agent, st.HomeAnchorPos, 1.4f, dist > 7.5f); } catch { }
                return true;
            }
            if (!CompanionSurvivalSystem.DoesAgentNeedNativeSurvival(agent))
            {
                hc.movementInput = Vector2.zero;
                agent.hasNavigationTarget = false;
            }
            return false;
        }

        public static bool TickGarrisonFrame(GOAPAgent agent)
        {
            if (!TryPrepareGarrisonTick(agent, out var st, out var hc, out float dist, out bool hasThreat))
                return false;
            if (hasThreat && dist <= 7.5f)
                return false; // Let native ranged/melee combat engage enemies near camp!

            if (dist > 1.85f)
            {
                try { CorridorNavigationHelper.SmartMoveTo(agent, st.HomeAnchorPos, 1.4f, dist > 7.5f); } catch { }
                return true;
            }
            hc.movementInput = Vector2.zero;
            agent.hasNavigationTarget = false;
            return !hasThreat;
        }
    }
}
