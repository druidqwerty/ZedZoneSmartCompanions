using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Unified A* physical navigation, collider-perimeter arrival check, hand-reach interaction,
    /// and GOAP UpdateGoal override for all companion item/loot/sorting/chore actions.
    /// Guarantees ZERO item teleportation: items are only picked up or placed when the NPC
    /// physically arrives at the target collider/spot; unreachable targets are skipped.
    /// </summary>
    public static class PhysicalMovementHelper
    {
        public const float DefaultArrivalRadius = 1.75f;
        public const float MaxReachableInteractRadius = 2.45f;
        public const float MotionlessStallSeconds = 5.5f;
        public const float DefaultStallSeconds = MotionlessStallSeconds;
        public const float MaxDetourLegSeconds = 32.0f;

        private static readonly Dictionary<string, float> UnreachableTargetCooldownUntil =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public static bool IsTargetTemporarilyUnreachable(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (UnreachableTargetCooldownUntil.TryGetValue(key, out float until))
            {
                if (Time.unscaledTime < until) return true;
                UnreachableTargetCooldownUntil.Remove(key);
            }
            return false;
        }

        public static void MarkTargetTemporarilyUnreachable(string key, float cooldownSeconds = 45f)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (UnreachableTargetCooldownUntil.Count > 512)
                UnreachableTargetCooldownUntil.Clear();
            UnreachableTargetCooldownUntil[key] = Time.unscaledTime + cooldownSeconds;
        }

        public static float ComputeSurfaceDistance(Vector2 selfPos, Vector2 targetCenter, Transform targetTransform)
        {
            float best = Vector2.Distance(selfPos, targetCenter);
            if (!Il2CppSafe.IsAliveUnity(targetTransform)) return best;
            try
            {
                var col = targetTransform.GetComponent<Collider2D>();
                if (!Il2CppSafe.IsAliveUnity(col))
                    col = targetTransform.GetComponentInChildren<Collider2D>();
                if (Il2CppSafe.IsAliveUnity(col) && col.enabled)
                {
                    Vector2 closest = col.ClosestPoint(selfPos);
                    float surfDist = Vector2.Distance(selfPos, closest);
                    if (surfDist < best) best = surfDist;
                }
            }
            catch { }
            return best;
        }

        public static void BeginLeg(CompanionRuntimeState st, Vector2 selfPos, Vector2 targetPos, Transform anchor = null)
        {
            if (st == null) return;
            float now = Time.time;
            st.WalkOriginPos = selfPos;
            st.LastMovedPos = selfPos;
            st.LastMovedTime = now;
            st.WalkBestDist = ComputeSurfaceDistance(selfPos, targetPos, anchor);
            st.LastProgressTime = now;
            st.SortingStateStartTime = now;
            st.NavResetDone = false;
            st.ArrivalInteractStarted = false;
            st.ArrivalInteractUntil = 0f;
        }

        public static void TriggerReachInteraction(HumanCharacterController hc, Vector2 targetPos, int propItemId = 0)
        {
            if (!Il2CppSafe.IsAliveUnity(hc) || hc.deadFlag) return;
            try
            {
                hc.movementInput = Vector2.zero;
                hc.PlayFarmAction(2, new Vector3(targetPos.x, targetPos.y, 0f), propItemId, propItemId > 0);
            }
            catch { }
        }

        public static void StepWalkAndInteract(
            GOAPAgent agent,
            CompanionRuntimeState st,
            Vector2 targetPos,
            Transform targetAnchor,
            int interactPropItemId,
            float interactSeconds,
            out bool readyToExecute,
            out bool unreachable,
            float arrivalRadius = DefaultArrivalRadius,
            float stallSeconds = MotionlessStallSeconds)
        {
            readyToExecute = false;
            unreachable = false;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || st == null)
            {
                unreachable = true;
                return;
            }

            var hc = agent.humanCharacter;
            float now = Time.time;
            Vector2 selfPos = hc.transform.position;
            float surfDist = ComputeSurfaceDistance(selfPos, targetPos, targetAnchor);

            if (surfDist <= arrivalRadius)
            {
                hc.movementInput = Vector2.zero;
                try { agent.LookAtPositionSmoothCompat(targetPos, 14f); } catch { }

                if (!st.ArrivalInteractStarted)
                {
                    st.ArrivalInteractStarted = true;
                    st.ArrivalInteractUntil = now + Mathf.Max(0.28f, interactSeconds);
                    TriggerReachInteraction(hc, targetPos, interactPropItemId);
                }

                if (now >= st.ArrivalInteractUntil)
                {
                    readyToExecute = true;
                }
                return;
            }

            st.ArrivalInteractStarted = false;

            if (surfDist < st.WalkBestDist - 0.25f)
            {
                st.WalkBestDist = surfDist;
                st.LastProgressTime = now;
            }

            if ((selfPos - st.LastMovedPos).sqrMagnitude >= 0.40f * 0.40f)
            {
                st.LastMovedPos = selfPos;
                st.LastMovedTime = now;
            }

            try
            {
                if (!st.NavResetDone)
                {
                    st.NavResetDone = true;
                    agent.hasNavigationTarget = false;
                }
                float stopDist = Mathf.Max(0.85f, arrivalRadius - 0.35f);
                agent.MoveTo(targetPos, stopDist, true);
                agent.LookAtPositionSmoothCompat(targetPos, 10f);
            }
            catch { }

            float motionlessSeconds = now - st.LastMovedTime;
            float totalLegElapsed = now - st.SortingStateStartTime;
            if ((agent.cantFindPath && totalLegElapsed > 2.5f) ||
                motionlessSeconds > stallSeconds ||
                totalLegElapsed > MaxDetourLegSeconds)
            {
                hc.movementInput = Vector2.zero;
                unreachable = true;
                st.NoteBlocked(CompanionBlockReason.PathBlocked,
                    !string.IsNullOrEmpty(st.TargetContainerName) && st.SortingPhase == 2 ? st.TargetContainerName :
                    !string.IsNullOrEmpty(st.SourceContainerName) && st.SortingPhase == 1 ? st.SourceContainerName :
                    st.ChoreDisplayText);
            }
        }

        public static void CancelAllPhysicalTasksOnCombat(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            if (st.HasAnyPhysicalTask)
                st.NoteBlocked(CompanionBlockReason.InterruptedByCombat);
            if (st.IsSortingActive || st.CarryBuffer.Count > 0)
            {
                BaseSortingSystem.ReturnCarryBufferWithoutTeleport(agent, st);
                st.IsSortingActive = false;
                st.SortingPhase = 0;
            }
            if (st.IsLootingActive)
            {
                st.LootQueue.Clear();
                st.IsLootingActive = false;
            }
            if (st.IsChoreWalkActive)
            {
                st.IsChoreWalkActive = false;
                st.PendingChoreArrivalAction = null;
            }
            st.ArrivalInteractStarted = false;
        }

        public static bool TickPhysicalFrameForAgent(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;

            if (!CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var st) ||
                st == null || !st.HasAnyPhysicalTask)
                return false;

            if (Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag)
            {
                // Pause physical walking while actively fighting a live threat; keep stall timers fresh so the NPC resumes seamlessly right after combat!
                float now = Time.time;
                st.ArrivalInteractStarted = false;
                st.LastMovedPos = agent.humanCharacter.transform.position;
                st.LastMovedTime = now;
                st.LastProgressTime = now;
                st.SortingStateStartTime = now;
                return false;
            }

            if (BaseWorkManager.Paused)
            {
                // Paused: keep the job and carried items, follow the player natively, resume later.
                BaseWorkManager.HoldJobWhilePaused(agent, st);
                return false;
            }

            if (st.IsSortingActive)
            {
                BaseSortingSystem.StepSortingJobForAgent(agent, st);
                return true;
            }

            if (st.IsLootingActive)
            {
                AutoLootSystem.StepLootingJobForAgent(agent, st);
                return true;
            }

            if (st.IsChoreWalkActive)
            {
                BaseChoreSystem.StepChoreWalkForAgent(agent, st);
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.UpdateGoal))]
    internal static class PhysicalTaskUpdateGoalOverride
    {
        [HarmonyPrefix]
        private static bool Prefix(GOAPAgent __instance)
        {
            try
            {
                if (PhysicalMovementHelper.TickPhysicalFrameForAgent(__instance))
                    return false;
                if (TacticalSquadSystem.TickTacticalGoalOverride(__instance))
                    return false;
            }
            catch { }
            return true;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, GOAPAgent __instance)
        {
            if (__exception == null) return null;
            if (Il2CppSafe.IsAliveUnity(__instance) && HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance.humanCharacter))
            {
                HumanNpcEquipmentGuard.CleanupBrokenRangedState(__instance.humanCharacter);
                return null;
            }
            return __exception;
        }
    }
}
