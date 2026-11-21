using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // Refresh the game's own perception/planner, never manufacture enemies or fire
    // directly. Native hostility, sight angle, lighting, z-level and cover checks remain.
    public static class CombatResponse
    {
        private sealed class Reaction
        {
            public float NextScan;
            public float LastSeen;
            public IntPtr Target;
        }
        private static readonly Dictionary<int, Reaction> Reactions = new Dictionary<int, Reaction>();
        private static float _nextPrune;
        private static float _nextWarning;
        public const float PerceptionInterval = 0.30f;

        public static bool AllowsRangedResponse(CompanionRuntimeState state, string weaponPreference)
        {
            return state != null && state.Doctrine != TacticalDoctrine.Passive &&
                state.Doctrine != TacticalDoctrine.StealthMelee &&
                !string.Equals(NormalizeCommand(weaponPreference), "MeleeWeaponOnly", StringComparison.Ordinal);
        }

        public static string NormalizeCommand(string command)
        {
            const string localizationPrefix = "NPCCommand:";
            if (command == null) return null;
            if (command.StartsWith(localizationPrefix, StringComparison.Ordinal))
                command = command.Substring(localizationPrefix.Length);
            // ExecuteNPCCommand accepts Category.Leaf, but the native data/state
            // properties store Leaf only. Neither category nor localization ID belongs here.
            if (command.StartsWith("Combat.", StringComparison.Ordinal))
                return command.Substring("Combat.".Length);
            if (command.StartsWith("WeaponPreference.", StringComparison.Ordinal))
                return command.Substring("WeaponPreference.".Length);
            return command;
        }

        public static void SelfVerifyCommandStorage()
        {
            foreach (string leaf in new[] { "FireAtWill", "ProtectPlayer", "StopFire" })
            {
                if (NormalizeCommand(leaf) != leaf || NormalizeCommand("Combat." + leaf) != leaf ||
                    NormalizeCommand("NPCCommand:Combat." + leaf) != leaf)
                    throw new InvalidOperationException("Native combat storage must be a bare leaf: " + leaf);
            }
            foreach (string leaf in new[] { "NoWeaponPreference", "RangedWeaponOnly", "MeleeWeaponOnly" })
            {
                if (NormalizeCommand(leaf) != leaf || NormalizeCommand("WeaponPreference." + leaf) != leaf ||
                    NormalizeCommand("NPCCommand:WeaponPreference." + leaf) != leaf)
                    throw new InvalidOperationException("Native weapon storage must be a bare leaf: " + leaf);
            }
            var state = new CompanionRuntimeState { Doctrine = TacticalDoctrine.Balanced };
            if (AllowsRangedResponse(state, "MeleeWeaponOnly") ||
                AllowsRangedResponse(state, "NPCCommand:WeaponPreference.MeleeWeaponOnly") ||
                !AllowsRangedResponse(state, "RangedWeaponOnly"))
                throw new InvalidOperationException("Native weapon preference gate mismatch.");
            state.Doctrine = TacticalDoctrine.Passive;
            if (AllowsRangedResponse(state, "RangedWeaponOnly"))
                throw new InvalidOperationException("Passive doctrine must never force ranged response.");
            state.Doctrine = TacticalDoctrine.StealthMelee;
            if (AllowsRangedResponse(state, "NoWeaponPreference"))
                throw new InvalidOperationException("Stealth doctrine must never force ranged response.");
        }

        public static void NormalizeNativeCommands(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return;
            try
            {
                var data = agent.npcData;
                string combat = NormalizeCommand(data.combatCommand);
                string weapon = NormalizeCommand(data.weaponPreference);
                if (combat == data.combatCommand && weapon == data.weaponPreference) return;
                data.combatCommand = combat;
                data.weaponPreference = weapon;
                if (Il2CppSafe.IsAlive(agent.agentState))
                {
                    agent.agentState.combatCommand = combat;
                    agent.agentState.weaponPreference = weapon;
                }
                agent.executeSelectPriorityGoalFlag = true;
            }
            catch (Exception error) { ReportFailure(Time.unscaledTime, error); }
        }

        public static bool HasNativeRangedAction(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return false;
            try
            {
                if (Il2CppSafe.IsAlive(agent.forcedAction) && agent.forcedAction.TryCast<GOAPAction_RangeAttack>() != null)
                    return true;
                var actions = agent.currentActions;
                return actions != null && actions.Count > 0 &&
                    Il2CppSafe.IsAlive(actions[0]) && actions[0].TryCast<GOAPAction_RangeAttack>() != null;
            }
            catch { return false; }
        }

        private static void ClearNonCloseThreatDuringSorting(GOAPAgent agent, Reaction reaction)
        {
            var currentThreat = agent.GetMostThreatCharacter();
            if (!Il2CppSafe.IsAliveUnity(currentThreat) ||
                currentThreat.deadFlag || !currentThreat.enabled || !currentThreat.gameObject.activeInHierarchy ||
                !HasCloseLineOfSightThreat(agent, currentThreat, 12f))
            {
                agent.mostThreateningCharacter = null;
                if (reaction != null)
                    reaction.Target = IntPtr.Zero;
            }
        }

        public static void Refresh(GOAPAgent agent, CompanionRuntimeState state, float now)
        {
            if (state == null || state.Doctrine == TacticalDoctrine.Passive ||
                !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag) return;
            try
            {
                if (agent.downedStateFlag || agent.humanCharacter.isSleeping) return;
                int id = agent.GetInstanceID();
                if (!Reactions.TryGetValue(id, out var reaction))
                {
                    reaction = new Reaction();
                    Reactions.Add(id, reaction);
                }
                if (agent.IsTeammateCombatSuppressed())
                {
                    if (state.IsSortingActive)
                        ClearNonCloseThreatDuringSorting(agent, reaction);
                    return;
                }
                reaction.LastSeen = now;
                if (now < reaction.NextScan)
                {
                    if (state.IsSortingActive)
                        ClearNonCloseThreatDuringSorting(agent, reaction);
                    return;
                }
                reaction.NextScan = now + PerceptionInterval;

                float originalRange = agent.defaultSightRange;
                try
                {
                    var weapon = agent.humanCharacter.rangedWeapon;
                    if (AllowsRangedResponse(state, agent.npcData.weaponPreference) &&
                        Il2CppSafe.IsAliveUnity(weapon) && Il2CppSafe.IsAliveUnity(weapon.weaponAttr))
                    {
                        float range = weapon.weaponAttr.fireRange;
                        if (range > 0f && !float.IsNaN(range) && !float.IsInfinity(range))
                            agent.defaultSightRange = Mathf.Max(originalRange, Mathf.Min(24f, range));
                    }
                    CombatSensing.Scan(agent);
                }
                finally { agent.defaultSightRange = originalRange; }

                if (agent.threatDataDic != null && agent.threatDataDic.Count > 0)
                {
                    List<BasicCharacterController> staleThreats = null;
                    foreach (var kv in agent.threatDataDic)
                    {
                        var ch = kv.Key;
                        if (!Il2CppSafe.IsAliveUnity(ch) || ch.deadFlag || !ch.enabled || !ch.gameObject.activeInHierarchy)
                        {
                            staleThreats ??= new List<BasicCharacterController>();
                            staleThreats.Add(ch);
                        }
                    }
                    if (staleThreats != null)
                    {
                        for (int i = 0; i < staleThreats.Count; i++)
                            agent.threatDataDic.Remove(staleThreats[i]);
                    }
                }

                agent.RefreshThreatsNow();
                var target = agent.GetMostThreatCharacter();
                IntPtr targetPointer = Il2CppSafe.IsAliveUnity(target) && !target.deadFlag &&
                    target.enabled && target.gameObject.activeInHierarchy
                    ? target.Pointer : IntPtr.Zero;

                if (state.IsSortingActive)
                {
                    // Distant or wall-blocked zombies (> 12m or no clear LoS) must never hijack or abort base unloading/sorting.
                    if (targetPointer == IntPtr.Zero || !HasCloseLineOfSightThreat(agent, target, 12f))
                    {
                        agent.mostThreateningCharacter = null;
                        reaction.Target = IntPtr.Zero;
                        return;
                    }
                    // Genuine close LoS threat: pause sorting smoothly without dumping CarryBuffer back to bags so CarryBuffer resumes after combat.
                    PauseSortingForCombat(agent, state);
                }

                agent.mostThreateningCharacter = targetPointer != IntPtr.Zero ? target : null;
                if (targetPointer != IntPtr.Zero)
                {
                    if (Il2CppSafe.IsAlive(agent.agentState))
                    {
                        agent.agentState.SetState("isThreatened", true);
                        agent.agentState.SetState("isSafe", false);
                        if (target.TryCast<HumanCharacterController>() != null)
                        {
                            agent.agentState.SetState("hasTargetEnemy", true);
                            agent.agentState.SetState("targetEnemyGameObject", target.gameObject);
                            agent.agentState.SetState("targetEnemyInSight", true);
                        }
                    }
                    bool feverDebuffSuperseded = agent.currentGoal != null &&
                        agent.currentGoal.TryCast<GOAPGoal_ClearDebuff>() != null &&
                        CompanionSurvivalSystem.IsInterruptibleClearDebuffForCombat(agent);
                    if (feverDebuffSuperseded)
                        CompanionSurvivalSystem.SuppressFeverDebuffGoal(agent, 20f);

                    bool idleOrFollowSuperseded = agent.currentGoal == null ||
                        feverDebuffSuperseded ||
                        (agent.currentGoal.TryCast<GOAPGoal_FollowLeader>() != null &&
                         RangedEngagementPolicy.CanEngage(agent, agent.agentState));
                    if (reaction.Target != targetPointer || idleOrFollowSuperseded)
                    {
                        // Replan only at acquisition/target change; restarting a current
                        // firing action every tick would reset its native aiming timer.
                        agent.UpdateAgentState();
                        if (Il2CppSafe.IsAlive(agent.agentState))
                        {
                            agent.agentState.SetState("isThreatened", true);
                            agent.agentState.SetState("isSafe", false);
                            if (target.TryCast<HumanCharacterController>() != null)
                            {
                                agent.agentState.SetState("hasTargetEnemy", true);
                                agent.agentState.SetState("targetEnemyGameObject", target.gameObject);
                                agent.agentState.SetState("targetEnemyInSight", true);
                            }
                        }
                        agent.SelectPriorityGoal();
                        agent.goalReselectDeferred = true;
                    }
                }
                reaction.Target = targetPointer;

                if (now >= _nextPrune)
                {
                    _nextPrune = now + 30f;
                    var expired = new List<int>();
                    foreach (var pair in Reactions)
                        if (now - pair.Value.LastSeen > 30f) expired.Add(pair.Key);
                    foreach (var key in expired) Reactions.Remove(key);
                }
            }
            catch (Exception error)
            {
                ReportFailure(now, error);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool HasCloseLineOfSightThreat(GOAPAgent agent, BasicCharacterController target, float maxDistance)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter)) return false;
            if (!Il2CppSafe.IsAliveUnity(target) || target.deadFlag || !target.enabled || !target.gameObject.activeInHierarchy)
                return false;
            try
            {
                Vector3 selfPos = agent.humanCharacter.transform.position;
                Vector3 tgtPos = target.transform.position;
                float dist = Vector2.Distance(selfPos, tgtPos);
                if (dist > maxDistance) return false;
                if (dist <= 4.5f) return true;
                return agent.HasClearFireLine(selfPos, tgtPos);
            }
            catch
            {
                return true;
            }
        }

        private static void PauseSortingForCombat(GOAPAgent agent, CompanionRuntimeState state)
        {
            if (state == null) return;
            float t = Time.time;
            state.ArrivalInteractStarted = false;
            if (Il2CppSafe.IsAliveUnity(agent?.humanCharacter))
                state.LastMovedPos = agent.humanCharacter.transform.position;
            state.LastMovedTime = t;
            state.LastProgressTime = t;
            state.SortingStateStartTime = t;
        }

        private static void ReportFailure(float now, Exception error)
        {
            if (now < _nextWarning) return;
            _nextWarning = now + 30f;
            SmartCompanionsPlugin.LogWarning("Combat perception refresh: " + error);
        }
    }
}
