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
            }
            else
            {
                st.RosterMode = CompanionRosterMode.CampGarrison;
                ResolveAndPinHomeAnchor(agent, st);
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

        public static void ResolveAndPinHomeAnchor(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            Vector2 selfPos = Il2CppSafe.IsAliveUnity(agent?.humanCharacter)
                ? (Vector2)agent.humanCharacter.transform.position
                : st.HomeAnchorPos;
            int selfZ = Il2CppSafe.IsAlive(agent?.npcData) ? agent.npcData.zLevel : st.HomeAnchorZ;

            Vector2 bestAnchor = selfPos;
            float bestDist = 18.0f;
            var boxes = BaseSortingSystem.CachedContainers;
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
            st.HomeAnchorPos = bestAnchor;
            st.HomeAnchorZ = selfZ;
            st.HasHomeAnchor = true;
            st.GarrisonAnchorSettled = Vector2.Distance(selfPos, bestAnchor) <= 4.2f;
        }

        public static bool TickGarrisonFrame(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;
            if (!agent.IsPlayerTeammate()) return false;
            if (!CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var st) || st == null)
                return false;
            if (st.RosterMode != CompanionRosterMode.CampGarrison || st.HasAnyPhysicalTask)
                return false;
            if (!st.HasHomeAnchor)
                ResolveAndPinHomeAnchor(agent, st);
            if (agent.npcData.zLevel != st.HomeAnchorZ)
                return false;

            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;
            float dist = Vector2.Distance(selfPos, st.HomeAnchorPos);
            bool hasThreat = Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag;
            if (hasThreat && dist <= 7.5f)
                return false; // Let native ranged/melee combat engage enemies near camp!

            if (dist > 3.6f)
            {
                try { agent.MoveTo(st.HomeAnchorPos, 1.8f, dist > 7.5f); } catch { }
                return true;
            }
            hc.movementInput = Vector2.zero;
            agent.hasNavigationTarget = false;
            return !hasThreat;
        }
    }
}
