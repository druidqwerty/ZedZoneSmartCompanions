using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public enum GrenadeSafetyBlockReason
    {
        None = 0,
        InvalidTarget = 1,
        TooCloseToFriendly = 2,
        TooCloseToBaseChest = 3,
        InsufficientHostileCluster = 4
    }

    /// <summary>
    /// v2.5.0 Field Tactics 2.0:
    /// Cursor-directed fan move orders, automatic ranged-fire cover maneuvers,
    /// GrenadeSafetyGate offensive grenade validation, tactical healing smoke with
    /// soft field-bandage fallback, and autonomous Medic rescue of critically wounded allies.
    /// </summary>
    public static class FieldTacticsSystem
    {
        public const float GrenadeClusterRadius = 4.5f;
        public const int MinHostilesForGrenade = 3;
        public const float FriendlySafeRadius = 6.5f;
        public const float BaseChestSafeRadius = 18.0f;

        public const float DefaultCursorFanSpacing = 1.25f;
        public const float TakeCoverCooldownSeconds = 6.5f;
        public const float MedicRescueCooldownSeconds = 22.0f;
        public const float MedicRescueReachDistance = 2.45f;
        public const float CriticalWoundedHpRatio = 0.32f;

        private const float MaxGrenadeOrderRange = 24.0f;
        private const float MaxSmokeOrderRange = 26.0f;
        private const float SmokeThrowDirectRange = 18.5f;
        private const float MedicRescueMaxSearchRadius = 28.0f;
        private const float MedicRescueApproachTimeout = 8.0f;

        public static Vector2 LastCursorOrderWorldPos { get; private set; }
        public static float LastCursorOrderTime { get; private set; }
        public static int LastCursorOrderCount { get; private set; }
        public static GrenadeSafetyBlockReason LastGrenadeBlockReason { get; private set; }

        private struct MedicRescueSession
        {
            public IntPtr ActivePatientPtr;
            public float ApproachStartedAt;
            public float LastCompletedRescueAt;
        }

        private static readonly List<Vector2> ScratchHostilePositions = new List<Vector2>(64);
        private static readonly List<Vector2> ScratchFriendlyPositions = new List<Vector2>(32);
        private static readonly List<Vector2> ScratchBaseChestPositions = new List<Vector2>(64);
        private static readonly Dictionary<string, float> LastCoverTriggerByCompanion = new Dictionary<string, float>(32);
        private static readonly Dictionary<string, MedicRescueSession> MedicSessions = new Dictionary<string, MedicRescueSession>(16);

        private static readonly Dictionary<Type, MemberInfo> CachedHpMembers = new Dictionary<Type, MemberInfo>(8);
        private static readonly Dictionary<Type, MemberInfo> CachedMaxHpMembers = new Dictionary<Type, MemberInfo>(8);
        private static readonly Dictionary<Type, List<MemberInfo>> CachedDownedMembers = new Dictionary<Type, List<MemberInfo>>(8);
        private static readonly Dictionary<Type, MemberInfo> CachedPosMembers = new Dictionary<Type, MemberInfo>(8);

        // =====================================================================
        // 1. Pure Deterministic Rules (unit-tested in UiSmoke)
        // =====================================================================

        public static bool CanThrowOffensiveGrenadeSafely(
            Vector2 targetPos,
            IReadOnlyList<Vector2> hostilePositions,
            IReadOnlyList<Vector2> friendlyPositions,
            IReadOnlyList<Vector2> baseChestPositions,
            bool isBossTarget = false)
        {
            return EvaluateGrenadeSafetyGate(
                targetPos,
                hostilePositions,
                friendlyPositions,
                baseChestPositions,
                isBossTarget,
                out _) == GrenadeSafetyBlockReason.None;
        }

        public static GrenadeSafetyBlockReason EvaluateGrenadeSafetyGate(
            Vector2 targetPos,
            IReadOnlyList<Vector2> hostilePositions,
            IReadOnlyList<Vector2> friendlyPositions,
            IReadOnlyList<Vector2> baseChestPositions,
            bool isBossTarget,
            out int hostilesInCluster)
        {
            hostilesInCluster = 0;
            if (!IsFinite(targetPos))
                return GrenadeSafetyBlockReason.InvalidTarget;

            float friendlySq = FriendlySafeRadius * FriendlySafeRadius;
            if (friendlyPositions != null)
            {
                for (int i = 0; i < friendlyPositions.Count; i++)
                {
                    Vector2 fp = friendlyPositions[i];
                    if (!IsFinite(fp) || (fp - targetPos).sqrMagnitude < friendlySq)
                        return GrenadeSafetyBlockReason.TooCloseToFriendly;
                }
            }

            float chestSq = BaseChestSafeRadius * BaseChestSafeRadius;
            if (baseChestPositions != null)
            {
                for (int i = 0; i < baseChestPositions.Count; i++)
                {
                    Vector2 cp = baseChestPositions[i];
                    if (!IsFinite(cp) || (cp - targetPos).sqrMagnitude < chestSq)
                        return GrenadeSafetyBlockReason.TooCloseToBaseChest;
                }
            }

            float clusterSq = GrenadeClusterRadius * GrenadeClusterRadius;
            if (hostilePositions != null)
            {
                for (int i = 0; i < hostilePositions.Count; i++)
                {
                    Vector2 hp = hostilePositions[i];
                    if (IsFinite(hp) && (hp - targetPos).sqrMagnitude <= clusterSq)
                        hostilesInCluster++;
                }
            }

            if (hostilesInCluster < MinHostilesForGrenade && !isBossTarget)
                return GrenadeSafetyBlockReason.InsufficientHostileCluster;

            return GrenadeSafetyBlockReason.None;
        }

        public static List<Vector2> ComputeCursorFanPositions(
            Vector2 cursorWorldPos,
            Vector2 leaderPos,
            int count,
            float minSpacing = DefaultCursorFanSpacing)
        {
            if (count <= 0)
                return new List<Vector2>(0);

            Vector2 anchor = IsFinite(cursorWorldPos)
                ? cursorWorldPos
                : (IsFinite(leaderPos) ? leaderPos : Vector2.zero);
            Vector2 origin = IsFinite(leaderPos) ? leaderPos : (anchor - Vector2.right);
            float spacing = IsFinite(minSpacing) ? Mathf.Clamp(minSpacing, 0.5f, 6.0f) : DefaultCursorFanSpacing;

            var result = new List<Vector2>(count);
            if (count == 1)
            {
                result.Add(anchor);
                return result;
            }

            Vector2 delta = anchor - origin;
            Vector2 fwd = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
            Vector2 right = new Vector2(fwd.y, -fwd.x);

            const int maxPerRank = 4;
            float stepAngleRad = 32f * (Mathf.PI / 180f);
            float halfSin = Mathf.Sin(stepAngleRad * 0.5f);
            float arcRadius = Mathf.Max(spacing * 1.15f, (spacing * 1.12f) / (2f * halfSin));
            float rankStep = spacing * 1.18f;

            for (int placed = 0; placed < count; placed++)
            {
                int rank = placed / maxPerRank;
                int inThisRank = Mathf.Min(maxPerRank, count - rank * maxPerRank);
                int idxInRank = placed - rank * maxPerRank;
                float centered = idxInRank - (inThisRank - 1) * 0.5f;
                float angle = centered * stepAngleRad;

                float fwdOffset = -rank * rankStep - arcRadius * (1f - Mathf.Cos(angle));
                float latOffset = arcRadius * Mathf.Sin(angle);
                result.Add(anchor + fwd * fwdOffset + right * latOffset);
            }

            float targetDist = spacing * 1.02f;
            float targetSq = targetDist * targetDist;
            for (int iter = 0; iter < 8; iter++)
            {
                bool anyOverlap = false;
                for (int i = 0; i < count; i++)
                {
                    for (int j = i + 1; j < count; j++)
                    {
                        Vector2 diff = result[j] - result[i];
                        float sq = diff.sqrMagnitude;
                        if (sq >= targetSq)
                            continue;

                        anyOverlap = true;
                        float dist = Mathf.Sqrt(Mathf.Max(0f, sq));
                        Vector2 pushDir;
                        if (dist > 0.0001f)
                        {
                            pushDir = diff / dist;
                        }
                        else
                        {
                            float fallbackAngle = (j - i) * 1.1f;
                            pushDir = new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle));
                        }

                        float halfPush = (targetDist - dist) * 0.52f;
                        result[i] -= pushDir * halfPush;
                        result[j] += pushDir * halfPush;
                    }
                }

                if (!anyOverlap)
                    break;
            }

            return result;
        }

        public static bool IsDoctrineEligibleForCover(TacticalDoctrine doctrine)
        {
            return doctrine == TacticalDoctrine.Balanced ||
                   doctrine == TacticalDoctrine.Bodyguard ||
                   doctrine == TacticalDoctrine.SniperKite;
        }

        public static bool IsCooldownReady(float lastTriggeredAt, float now, float cooldownSeconds)
        {
            if (!IsFinite(lastTriggeredAt) || lastTriggeredAt < -900f)
                return true;
            if (!IsFinite(now))
                return false;
            if (now < lastTriggeredAt)
                return true; // clock reset across session reload
            return (now - lastTriggeredAt) >= cooldownSeconds;
        }

        public static bool ShouldMedicRescueTarget(
            CompanionSpecialization specialization,
            float targetHp,
            float targetMaxHp,
            bool isDowned,
            float lastRescueAt,
            float now)
        {
            if (specialization != CompanionSpecialization.Medic)
                return false;
            if (!IsCooldownReady(lastRescueAt, now, MedicRescueCooldownSeconds))
                return false;
            if (isDowned)
                return true;
            if (!IsFinite(targetHp) || !IsFinite(targetMaxHp) || targetMaxHp <= 0.01f)
                return false;
            return (targetHp / targetMaxHp) < CriticalWoundedHpRatio;
        }

        // =====================================================================
        // 2. Live Game Tactical Execution
        // =====================================================================

        public static bool TryGetCursorWorldPosition(out Vector2 worldPos)
        {
            worldPos = Vector2.zero;
            try
            {
                var cam = Camera.main;
                if (!Il2CppSafe.IsAliveUnity(cam))
                    return false;

                Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
                if (!IsFinite(wp.x) || !IsFinite(wp.y))
                    return false;

                worldPos = new Vector2(wp.x, wp.y);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int IssueMoveToCursorOrder(
            GOAPAgent specificAgent = null,
            Vector2? worldOverride = null,
            bool quiet = false)
        {
            Vector2 cursorPos;
            if (worldOverride.HasValue)
            {
                if (!IsFinite(worldOverride.Value))
                    return 0;
                cursorPos = worldOverride.Value;
            }
            else if (!TryGetCursorWorldPosition(out cursorPos))
            {
                return 0;
            }

            var targets = CollectCommandableSquadAgents(specificAgent);
            if (targets.Count == 0)
                return 0;

            var player = GameController.instance?.playerCharacter;
            Vector2 leaderPos = Il2CppSafe.IsAliveUnity(player)
                ? (Vector2)player.transform.position
                : (Vector2)targets[0].humanCharacter.transform.position;

            Vector2 toCursor = cursorPos - leaderPos;
            Vector2 orderDir = toCursor.sqrMagnitude > 0.001f
                ? toCursor.normalized
                : TacticalSquadSystem.ResolveLeaderForward(player);

            var slots = ComputeCursorFanPositions(cursorPos, leaderPos, targets.Count, DefaultCursorFanSpacing);
            int applied = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                var ag = targets[i];
                Vector2 slotPos = slots[i];
                var st = CompanionConfigStore.GetOrCreateCompanion(
                    ag.npcData.npcGUID,
                    ag.npcData.characterData?.characterName);

                if (st != null && st.HasAnyPhysicalTask)
                    BaseWorkManager.CancelWork(ag, st);

                int count = TacticalSquadSystem.IssueOrder(
                    ag,
                    TacticalOrderKind.HoldPosition,
                    slotPos,
                    orderDir,
                    fromNetwork: false,
                    quiet: true);

                if (count > 0)
                {
                    TryStartTeamMoveAction(ag, slotPos);
                    applied += count;
                }
            }

            if (applied > 0)
            {
                LastCursorOrderWorldPos = cursorPos;
                LastCursorOrderTime = Time.unscaledTime;
                LastCursorOrderCount = applied;

                if (!quiet)
                {
                    string banner = IsRussianLanguage()
                        ? $"Полевая тактика: позиция у курсора ({applied} бойц.) [{cursorPos.x:F0}, {cursorPos.y:F0}]"
                        : $"Field tactics: hold cursor point ({applied} squad) [{cursorPos.x:F0}, {cursorPos.y:F0}]";
                    BaseSortingSystem.SetBanner(banner, 4.5f);
                }
            }

            return applied;
        }

        public static bool CommandTacticalHealingSmoke(
            GOAPAgent specificAgent = null,
            Vector2? targetOverride = null)
        {
            Vector2 targetPos;
            if (targetOverride.HasValue)
            {
                if (!IsFinite(targetOverride.Value))
                    return false;
                targetPos = targetOverride.Value;
            }
            else if (!TryGetCursorWorldPosition(out targetPos))
            {
                var fallbackPlayer = GameController.instance?.playerCharacter;
                if (!Il2CppSafe.IsAliveUnity(fallbackPlayer))
                    return false;
                targetPos = fallbackPlayer.transform.position;
            }

            var candidates = CollectCommandableSquadAgents(specificAgent);
            if (candidates.Count == 0)
                return false;

            var player = GameController.instance?.playerCharacter;
            int refZ = Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData)
                ? player.characterData.zLevel
                : candidates[0].npcData.zLevel;

            GOAPAgent bestAgent = null;
            CompanionRuntimeState bestState = null;
            float bestScore = float.NegativeInfinity;
            float bestDist = float.MaxValue;
            bool bestHasSmoke = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                var ag = candidates[i];
                if (ag.npcData.zLevel != refZ)
                    continue;

                float dist = Vector2.Distance(ag.humanCharacter.transform.position, targetPos);
                if (dist > MaxSmokeOrderRange)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(
                    ag.npcData.npcGUID,
                    ag.npcData.characterData?.characterName);
                bool isMedic = st != null && st.Specialization == CompanionSpecialization.Medic;
                bool hasSmoke = HasHealingSmokeInInventory(ag);

                float score = (isMedic ? 1000f : 0f) + (hasSmoke ? 250f : 0f) - dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAgent = ag;
                    bestState = st;
                    bestDist = dist;
                    bestHasSmoke = hasSmoke;
                }
            }

            if (!Il2CppSafe.IsAliveUnity(bestAgent))
            {
                BaseSortingSystem.SetBanner(
                    IsRussianLanguage()
                        ? $"Мед-дым отменён: нет напарника на этаже в радиусе {MaxSmokeOrderRange:F0} м"
                        : $"Healing smoke cancelled: no companion in {MaxSmokeOrderRange:F0}m range",
                    4.0f);
                return false;
            }

            if (bestState != null && bestState.HasAnyPhysicalTask)
                BaseWorkManager.CancelWork(bestAgent, bestState);

            string actorName = GetDisplayName(bestAgent);

            if (bestHasSmoke && bestDist <= SmokeThrowDirectRange)
            {
                try
                {
                    var smokeAction = new GOAPAction_TeamOrder_ThrowHealingSmoke(targetPos);
                    smokeAction.targetPosition = targetPos;
                    smokeAction.OnActionStarted(bestAgent);
                    smokeAction.Execute(bestAgent);

                    BaseSortingSystem.SetBanner(
                        IsRussianLanguage()
                            ? $"Мед-дым: {actorName} бросает лечебную дымовую шашку"
                            : $"Healing smoke: {actorName} throwing medical smoke",
                        4.0f);
                    return true;
                }
                catch (Exception e)
                {
                    SmartCompanionsPlugin.LogWarning("CommandTacticalHealingSmoke native throw failed: " + e.Message);
                }
            }

            // Soft field-bandage fallback near target point when no healing smoke grenade is in inventory (or approaching point)
            TacticalSquadSystem.IssueOrder(
                bestAgent,
                TacticalOrderKind.HoldPosition,
                targetPos,
                explicitDir: null,
                fromNetwork: false,
                quiet: true);
            TryStartTeamMoveAction(bestAgent, targetPos);

            var patient = FindNearestWoundedAllyNearPoint(targetPos, 5.5f, refZ);
            if (Il2CppSafe.IsAliveUnity(patient) &&
                Vector2.Distance(bestAgent.humanCharacter.transform.position, patient.transform.position) <= 2.75f)
            {
                TryApplyFieldHeal(bestAgent, patient, healFraction: 0.18f, minFlatHeal: 8f);
            }

            BaseSortingSystem.SetBanner(
                IsRussianLanguage()
                    ? $"Дымовой шашки нет: {actorName} выполняет полевую перевязку у точки"
                    : $"No smoke grenade: {actorName} moving for soft field bandage at point",
                4.5f);
            return true;
        }

        public static bool CommandSafeOffensiveGrenade(
            GOAPAgent specificAgent = null,
            Vector2? targetOverride = null)
        {
            Vector2 targetPos;
            if (targetOverride.HasValue)
            {
                targetPos = targetOverride.Value;
            }
            else if (!TryGetCursorWorldPosition(out targetPos))
            {
                LastGrenadeBlockReason = GrenadeSafetyBlockReason.InvalidTarget;
                BaseSortingSystem.SetBanner(FormatGrenadeGateBanner(GrenadeSafetyBlockReason.InvalidTarget, 0), 4.0f);
                return false;
            }

            var candidates = CollectCommandableSquadAgents(specificAgent);
            if (candidates.Count == 0)
                return false;

            var player = GameController.instance?.playerCharacter;
            int friendlyCamp = Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData)
                ? player.characterData.camp
                : (Il2CppSafe.IsAlive(candidates[0].humanCharacter.characterData)
                    ? candidates[0].humanCharacter.characterData.camp
                    : 1);

            GOAPAgent thrower = SelectBestGrenadeThrower(candidates, targetPos, player);
            if (!Il2CppSafe.IsAliveUnity(thrower))
            {
                BaseSortingSystem.SetBanner(
                    IsRussianLanguage()
                        ? $"[GrenadeSafetyGate] Нет доступного бойца в радиусе {MaxGrenadeOrderRange:F0} м"
                        : $"[GrenadeSafetyGate] No available companion within {MaxGrenadeOrderRange:F0}m",
                    4.0f);
                return false;
            }

            float now = Time.unscaledTime;
            CollectHostilesAndBossFlag(thrower, friendlyCamp, now, targetPos, ScratchHostilePositions, out bool isBossTarget);
            CollectFriendlyPositions(thrower, friendlyCamp, ScratchFriendlyPositions);
            CollectMarkedBaseChestPositions(ScratchBaseChestPositions);

            var blockReason = EvaluateGrenadeSafetyGate(
                targetPos,
                ScratchHostilePositions,
                ScratchFriendlyPositions,
                ScratchBaseChestPositions,
                isBossTarget,
                out int clusterCount);
            bool safe = CanThrowOffensiveGrenadeSafely(
                targetPos,
                ScratchHostilePositions,
                ScratchFriendlyPositions,
                ScratchBaseChestPositions,
                isBossTarget);

            LastGrenadeBlockReason = blockReason;
            if (!safe || blockReason != GrenadeSafetyBlockReason.None)
            {
                BaseSortingSystem.SetBanner(FormatGrenadeGateBanner(blockReason, clusterCount), 4.5f);
                return false;
            }

            var st = CompanionConfigStore.GetOrCreateCompanion(
                thrower.npcData.npcGUID,
                thrower.npcData.characterData?.characterName);
            if (st != null && st.HasAnyPhysicalTask)
                BaseWorkManager.CancelWork(thrower, st);

            try
            {
                var grenadeAction = new GOAPAction_TeamOrder_ThrowGrenade(targetPos);
                grenadeAction.targetPosition = targetPos;
                grenadeAction.OnActionStarted(thrower);
                grenadeAction.Execute(thrower);

                string actorName = GetDisplayName(thrower);
                BaseSortingSystem.SetBanner(
                    IsRussianLanguage()
                        ? $"Граната разрешена: {actorName} атакует скопление ({clusterCount} враг.)"
                        : $"Grenade cleared: {actorName} engaging cluster ({clusterCount} hostiles)",
                    4.0f);
                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("CommandSafeOffensiveGrenade failed: " + e.Message);
                return false;
            }
        }

        public static bool TryTriggerCoverUnderRangedFire(
            GOAPAgent agent,
            CompanionRuntimeState st,
            BasicCharacterController threat,
            float now)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;
            if (st == null || st.HasAnyPhysicalTask || !IsDoctrineEligibleForCover(st.Doctrine))
                return false;
            if (!agent.IsPlayerTeammate() || agent.IsTeammateCombatSuppressed())
                return false;

            if (!Il2CppSafe.IsAliveUnity(threat) || threat.deadFlag)
                return false;

            var shooter = threat.TryCast<HumanCharacterController>();
            if (!Il2CppSafe.IsAliveUnity(shooter) || !Il2CppSafe.IsAliveUnity(shooter.rangedWeapon))
                return false;
            if (!SmartCombatAI.IsHostileHumanTarget(agent, shooter))
                return false;

            float distSq = ((Vector2)shooter.transform.position - (Vector2)agent.humanCharacter.transform.position).sqrMagnitude;
            if (distSq < 2.1f * 2.1f || distSq > 34.0f * 34.0f)
                return false;

            string key = GetAgentKey(agent);
            if (LastCoverTriggerByCompanion.TryGetValue(key, out float lastCoverAt) &&
                !IsCooldownReady(lastCoverAt, now, TakeCoverCooldownSeconds))
            {
                return false;
            }

            try
            {
                if (LastCoverTriggerByCompanion.Count > 128)
                    LastCoverTriggerByCompanion.Clear();
                LastCoverTriggerByCompanion[key] = now;

                var coverAction = new GOAPAction_TakeCover();
                coverAction.OnActionStarted(agent);
                coverAction.Execute(agent);
                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("TryTriggerCoverUnderRangedFire failed: " + e.Message);
                return false;
            }
        }

        public static bool TryMedicRescueWoundedTeammate(
            GOAPAgent medicAgent,
            CompanionRuntimeState medicState,
            float now)
        {
            if (!Il2CppSafe.IsAliveUnity(medicAgent) || !Il2CppSafe.IsAlive(medicAgent.npcData) ||
                !Il2CppSafe.IsAliveUnity(medicAgent.humanCharacter) || medicAgent.humanCharacter.deadFlag)
                return false;
            if (medicState == null || medicState.Specialization != CompanionSpecialization.Medic || medicState.HasAnyPhysicalTask)
                return false;
            if (!medicAgent.IsPlayerTeammate() || IsCharacterDowned(medicAgent.humanCharacter))
                return false;

            string medicKey = GetAgentKey(medicAgent);
            if (!MedicSessions.TryGetValue(medicKey, out var session))
            {
                session = new MedicRescueSession
                {
                    ActivePatientPtr = IntPtr.Zero,
                    ApproachStartedAt = -999f,
                    LastCompletedRescueAt = -999f
                };
            }

            if (session.ActivePatientPtr != IntPtr.Zero &&
                now > session.ApproachStartedAt &&
                (now - session.ApproachStartedAt) > MedicRescueApproachTimeout)
            {
                session.ActivePatientPtr = IntPtr.Zero;
                session.LastCompletedRescueAt = now;
                MedicSessions[medicKey] = session;
                return false;
            }

            bool hasActiveApproach = session.ActivePatientPtr != IntPtr.Zero;
            if (!hasActiveApproach &&
                !IsCooldownReady(session.LastCompletedRescueAt, now, MedicRescueCooldownSeconds))
            {
                return false;
            }

            var patient = FindCriticallyWoundedTeammate(medicAgent, session.ActivePatientPtr, session.LastCompletedRescueAt, now, out bool patientDowned);
            if (!Il2CppSafe.IsAliveUnity(patient))
            {
                if (hasActiveApproach)
                {
                    session.ActivePatientPtr = IntPtr.Zero;
                    MedicSessions[medicKey] = session;
                }
                return false;
            }

            if (session.ActivePatientPtr != patient.Pointer)
            {
                session.ActivePatientPtr = patient.Pointer;
                session.ApproachStartedAt = now;
            }

            Vector2 medicPos = medicAgent.humanCharacter.transform.position;
            Vector2 patientPos = patient.transform.position;
            float dist = Vector2.Distance(medicPos, patientPos);

            if (dist > MedicRescueReachDistance)
            {
                MedicSessions[medicKey] = session;
                try
                {
                    medicAgent.MoveTo(patientPos, 1.6f, true);
                    medicAgent.LookAtPositionSmoothCompat(patientPos, 12f);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // Within <= 2.45m reach: perform rescue (if downed) + emergency field heal, then enter 22s cooldown
            session.ActivePatientPtr = IntPtr.Zero;
            session.ApproachStartedAt = -999f;
            session.LastCompletedRescueAt = now;
            if (MedicSessions.Count > 64)
                MedicSessions.Clear();
            MedicSessions[medicKey] = session;

            try
            {
                medicAgent.humanCharacter.movementInput = Vector2.zero;
                medicAgent.hasNavigationTarget = false;
            }
            catch { }

            if (patientDowned)
            {
                try
                {
                    var rescueAction = new GOAPAction_RescueAlly();
                    rescueAction.m_target = patient;
                    rescueAction.OnActionStarted(medicAgent);
                    rescueAction.Execute(medicAgent);
                }
                catch (Exception e)
                {
                    SmartCompanionsPlugin.LogWarning("TryMedicRescueWoundedTeammate RescueAlly failed: " + e.Message);
                }
            }

            TryApplyFieldHeal(medicAgent, patient, healFraction: 0.28f, minFlatHeal: 14f);
            return true;
        }

        // =====================================================================
        // 3. Internal Helpers & Safe Reflection Bridges
        // =====================================================================

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);

        private static bool IsRussianLanguage()
        {
            try
            {
                string lang = CompanionConfigStore.EffectiveLanguage.ToString();
                return lang.StartsWith("Ru", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetAgentKey(GOAPAgent agent)
        {
            string guid = agent?.npcData?.npcGUID;
            if (!string.IsNullOrEmpty(guid))
                return guid;
            return agent != null ? agent.Pointer.ToString() : "";
        }

        private static string GetDisplayName(GOAPAgent agent)
        {
            try
            {
                string n = agent?.npcData?.characterData?.characterName;
                if (!string.IsNullOrWhiteSpace(n))
                    return n;
            }
            catch { }
            return IsRussianLanguage() ? "Напарник" : "Companion";
        }

        private static List<GOAPAgent> CollectCommandableSquadAgents(GOAPAgent specificAgent)
        {
            var result = new List<GOAPAgent>(8);
            if (Il2CppSafe.IsAliveUnity(specificAgent))
            {
                if (Il2CppSafe.IsAlive(specificAgent.npcData) &&
                    Il2CppSafe.IsAliveUnity(specificAgent.humanCharacter) &&
                    !specificAgent.humanCharacter.deadFlag &&
                    (!MultiplayerSyncSystem.IsMultiplayerOnline || MultiplayerSyncSystem.CanLocalPlayerCommand(specificAgent.npcData)))
                {
                    result.Add(specificAgent);
                }
                return result;
            }

            var active = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < active.Count; i++)
            {
                var ag = active[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;
                if (MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                    continue;
                result.Add(ag);
            }
            return result;
        }

        private static void TryStartTeamMoveAction(GOAPAgent agent, Vector2 targetPos)
        {
            try
            {
                var moveAction = new GOAPAction_TeamOrder_MoveTo(targetPos);
                moveAction.targetPosition = targetPos;
                moveAction.OnActionStarted(agent);
                moveAction.Execute(agent);
            }
            catch
            {
                try
                {
                    float dist = Vector2.Distance(agent.humanCharacter.transform.position, targetPos);
                    agent.MoveTo(targetPos, 1.15f, dist > 4.5f);
                }
                catch { }
            }
        }

        private static GOAPAgent SelectBestGrenadeThrower(
            List<GOAPAgent> candidates,
            Vector2 targetPos,
            HumanCharacterController player)
        {
            int refZ = Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData)
                ? player.characterData.zLevel
                : candidates[0].npcData.zLevel;

            GOAPAgent best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                var ag = candidates[i];
                if (ag.npcData.zLevel != refZ)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(
                    ag.npcData.npcGUID,
                    ag.npcData.characterData?.characterName);
                if (st != null && st.Doctrine == TacticalDoctrine.Passive)
                    continue;

                float dist = Vector2.Distance(ag.humanCharacter.transform.position, targetPos);
                if (dist > MaxGrenadeOrderRange)
                    continue;

                float score = -dist;
                if (st != null && st.Doctrine == TacticalDoctrine.Assault)
                    score += 6f;
                if (dist >= FriendlySafeRadius)
                    score += 12f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = ag;
                }
            }

            return best;
        }

        private static void CollectHostilesAndBossFlag(
            GOAPAgent observer,
            int friendlyCamp,
            float now,
            Vector2 targetPos,
            List<Vector2> output,
            out bool isBossTarget)
        {
            output.Clear();
            isBossTarget = false;
            float clusterSq = GrenadeClusterRadius * GrenadeClusterRadius;

            var snapshot = PerformanceAndDiagnostics.GetActiveHostilesSnapshot(observer, friendlyCamp, now);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                output.Add(entry.Pos);
                if (!isBossTarget &&
                    (entry.Pos - targetPos).sqrMagnitude <= clusterSq &&
                    IsBossCharacter(entry.Controller))
                {
                    isBossTarget = true;
                }
            }
        }

        private static void CollectFriendlyPositions(
            GOAPAgent observer,
            int friendlyCamp,
            List<Vector2> output)
        {
            output.Clear();

            var gc = GameController.instance;
            var player = gc?.playerCharacter;
            if (Il2CppSafe.IsAliveUnity(player) && !player.deadFlag)
                AddUniquePoint(output, player.transform.position);

            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < teammates.Count; i++)
            {
                var tm = teammates[i];
                if (Il2CppSafe.IsAliveUnity(tm) && Il2CppSafe.IsAliveUnity(tm.humanCharacter) && !tm.humanCharacter.deadFlag)
                    AddUniquePoint(output, tm.humanCharacter.transform.position);
            }

            try
            {
                if (!Il2CppSafe.IsAliveUnity(gc) || gc.characterList == null)
                    return;

                int count = gc.characterList.Count;
                for (int i = 0; i < count; i++)
                {
                    var c = gc.characterList[i];
                    if (!Il2CppSafe.IsAliveUnity(c) || c.deadFlag || !c.enabled || !c.gameObject.activeInHierarchy)
                        continue;

                    bool isFriendly = c.isPlayer || ZedZone.Net.NetSessionConfig.IsNetworkPlayer(c);
                    if (!isFriendly)
                    {
                        var ag = c.GetCachedGOAPAgent();
                        if (Il2CppSafe.IsAliveUnity(ag))
                        {
                            if (ag.IsPlayerTeammate())
                                isFriendly = true;
                            else if (Il2CppSafe.IsAlive(ag.npcData) && Il2CppSafe.IsAlive(ag.npcData.petData))
                                isFriendly = true;
                            else if (Il2CppSafe.IsAlive(ag.npcData) &&
                                     !string.IsNullOrEmpty(ag.npcData.leaderCharacterGUID) &&
                                     MultiplayerSyncSystem.IsAnyAlliedPlayerCharacterId(ag.npcData.leaderCharacterGUID))
                                isFriendly = true;
                        }
                    }

                    if (!isFriendly &&
                        Il2CppSafe.IsAlive(c.characterData) &&
                        c.characterData.camp == friendlyCamp &&
                        !SmartCombatAI.IsActualHostileCharacter(observer, c, friendlyCamp))
                    {
                        isFriendly = true;
                    }

                    if (isFriendly)
                        AddUniquePoint(output, c.transform.position);
                }
            }
            catch { }
        }

        private static void CollectMarkedBaseChestPositions(List<Vector2> output)
        {
            output.Clear();
            try
            {
                foreach (var kv in CompanionConfigStore.ContainerRules)
                {
                    var rule = kv.Value;
                    if (rule == null || !rule.IsMarkedByPlayer)
                        continue;
                    if (TryExtractObjectWorldPosition(rule, out Vector2 rulePos))
                        AddUniquePoint(output, rulePos);
                }
            }
            catch { }

            try
            {
                var cached = BaseSortingSystem.CachedContainers as System.Collections.IEnumerable;
                if (cached == null)
                    return;

                foreach (object rawItem in cached)
                {
                    if (rawItem == null)
                        continue;

                    object entry = UnwrapEnumerableEntry(rawItem);
                    if (entry == null)
                        continue;

                    if (IsMarkedCachedContainerEntry(entry) && TryExtractObjectWorldPosition(entry, out Vector2 chestPos))
                        AddUniquePoint(output, chestPos);
                }
            }
            catch { }
        }

        private static object UnwrapEnumerableEntry(object rawItem)
        {
            Type t = rawItem.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var valProp = t.GetProperty("Value");
                return valProp != null ? valProp.GetValue(rawItem) : null;
            }
            return rawItem;
        }

        private static bool IsMarkedCachedContainerEntry(object entry)
        {
            Type t = entry.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            MemberInfo directMark = (MemberInfo)t.GetProperty("IsMarkedByPlayer", flags) ?? t.GetField("IsMarkedByPlayer", flags);
            if (directMark != null && GetMemberValue(directMark, entry) is bool dm)
                return dm;

            MemberInfo ruleMember = (MemberInfo)t.GetProperty("Rule", flags) ?? t.GetField("Rule", flags);
            if (ruleMember != null)
            {
                object ruleObj = GetMemberValue(ruleMember, entry);
                if (ruleObj != null)
                {
                    Type rt = ruleObj.GetType();
                    MemberInfo rm = (MemberInfo)rt.GetProperty("IsMarkedByPlayer", flags) ?? rt.GetField("IsMarkedByPlayer", flags);
                    if (rm != null && GetMemberValue(rm, ruleObj) is bool rb)
                        return rb;
                }
            }

            string[] keyNames = { "ContainerKey", "RuleKey", "Key", "Guid", "ContainerGuid", "Id" };
            for (int i = 0; i < keyNames.Length; i++)
            {
                var p = t.GetProperty(keyNames[i], flags);
                if (p != null && p.GetValue(entry) is string sProp && !string.IsNullOrEmpty(sProp) &&
                    CompanionConfigStore.ContainerRules.TryGetValue(sProp, out var kr1) && kr1 != null)
                {
                    return kr1.IsMarkedByPlayer;
                }

                var f = t.GetField(keyNames[i], flags);
                if (f != null && f.GetValue(entry) is string sField && !string.IsNullOrEmpty(sField) &&
                    CompanionConfigStore.ContainerRules.TryGetValue(sField, out var kr2) && kr2 != null)
                {
                    return kr2.IsMarkedByPlayer;
                }
            }

            return false;
        }

        private static bool TryExtractObjectWorldPosition(object obj, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (obj == null)
                return false;

            if (obj is Component comp && Il2CppSafe.IsAliveUnity(comp))
            {
                pos = comp.transform.position;
                return IsFinite(pos);
            }

            Type t = obj.GetType();
            if (!CachedPosMembers.TryGetValue(t, out MemberInfo member))
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                string[] posNames = { "Pos", "WorldPos", "Position", "WorldPosition", "LastKnownPos", "AnchorPos", "Container", "Transform" };
                for (int i = 0; i < posNames.Length && member == null; i++)
                {
                    member = (MemberInfo)t.GetProperty(posNames[i], flags) ?? t.GetField(posNames[i], flags);
                }
                CachedPosMembers[t] = member;
            }

            if (member != null)
            {
                object val = GetMemberValue(member, obj);
                if (val is Vector2 v2 && IsFinite(v2))
                {
                    pos = v2;
                    return true;
                }
                if (val is Vector3 v3 && IsFinite(v3.x) && IsFinite(v3.y))
                {
                    pos = new Vector2(v3.x, v3.y);
                    return true;
                }
                if (val is Component innerComp && Il2CppSafe.IsAliveUnity(innerComp))
                {
                    pos = innerComp.transform.position;
                    return IsFinite(pos);
                }
            }

            const BindingFlags xyFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MemberInfo mx = (MemberInfo)t.GetProperty("WorldX", xyFlags) ?? t.GetField("WorldX", xyFlags)
                         ?? (MemberInfo)t.GetProperty("PosX", xyFlags) ?? t.GetField("PosX", xyFlags);
            MemberInfo my = (MemberInfo)t.GetProperty("WorldY", xyFlags) ?? t.GetField("WorldY", xyFlags)
                         ?? (MemberInfo)t.GetProperty("PosY", xyFlags) ?? t.GetField("PosY", xyFlags);
            if (mx != null && my != null &&
                TryConvertFloat(GetMemberValue(mx, obj), out float fx) &&
                TryConvertFloat(GetMemberValue(my, obj), out float fy) &&
                (Mathf.Abs(fx) > 0.001f || Mathf.Abs(fy) > 0.001f))
            {
                pos = new Vector2(fx, fy);
                return IsFinite(pos);
            }

            return false;
        }

        private static void AddUniquePoint(List<Vector2> list, Vector2 pt)
        {
            if (!IsFinite(pt))
                return;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i] - pt).sqrMagnitude < 0.01f)
                    return;
            }
            list.Add(pt);
        }

        private static string FormatGrenadeGateBanner(GrenadeSafetyBlockReason reason, int clusterCount)
        {
            bool ru = IsRussianLanguage();
            switch (reason)
            {
                case GrenadeSafetyBlockReason.TooCloseToFriendly:
                    return ru
                        ? "[GrenadeSafetyGate] Бросок заблокирован: союзник/игрок/питомец ближе 6.5 м!"
                        : "[GrenadeSafetyGate] Blocked: friendly/player/pet within 6.5m!";
                case GrenadeSafetyBlockReason.TooCloseToBaseChest:
                    return ru
                        ? "[GrenadeSafetyGate] Бросок заблокирован: размеченный ящик базы ближе 18 м!"
                        : "[GrenadeSafetyGate] Blocked: marked base chest within 18m!";
                case GrenadeSafetyBlockReason.InsufficientHostileCluster:
                    return ru
                        ? $"[GrenadeSafetyGate] Бросок заблокирован: мало врагов ({clusterCount}/3 в 4.5 м, не босс)"
                        : $"[GrenadeSafetyGate] Blocked: only {clusterCount}/3 hostiles in 4.5m (non-boss)";
                default:
                    return ru
                        ? "[GrenadeSafetyGate] Бросок заблокирован: некорректная точка прицеливания"
                        : "[GrenadeSafetyGate] Blocked: invalid target position";
            }
        }

        private static HumanCharacterController FindCriticallyWoundedTeammate(
            GOAPAgent medicAgent,
            IntPtr preferredPtr,
            float lastRescueAt,
            float now,
            out bool isDowned)
        {
            isDowned = false;
            int medicZ = medicAgent.npcData.zLevel;
            Vector2 medicPos = medicAgent.humanCharacter.transform.position;

            HumanCharacterController best = null;
            bool bestDowned = false;
            float bestScore = float.NegativeInfinity;

            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || ag.Pointer == medicAgent.Pointer)
                    continue;
                if (!Il2CppSafe.IsAlive(ag.npcData) || ag.npcData.zLevel != medicZ)
                    continue;

                var hc = ag.humanCharacter;
                if (!Il2CppSafe.IsAliveUnity(hc) || hc.deadFlag)
                    continue;

                float dist = Vector2.Distance(medicPos, hc.transform.position);
                if (dist > MedicRescueMaxSearchRadius)
                    continue;

                bool downed = IsCharacterDowned(hc);
                if (!TryGetCharacterHealth(hc, out float hp, out float maxHp) && !downed)
                    continue;

                float checkLastAt = (preferredPtr != IntPtr.Zero && hc.Pointer == preferredPtr) ? -999f : lastRescueAt;
                if (!ShouldMedicRescueTarget(CompanionSpecialization.Medic, hp, maxHp, downed, checkLastAt, now))
                    continue;

                float hpRatio = maxHp > 0.01f ? Mathf.Clamp01(hp / maxHp) : 0f;
                float score = (downed ? 500f : 0f) + (1f - hpRatio) * 100f - dist;
                if (preferredPtr != IntPtr.Zero && hc.Pointer == preferredPtr)
                    score += 200f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = hc;
                    bestDowned = downed;
                }
            }

            isDowned = bestDowned;
            return best;
        }

        private static HumanCharacterController FindNearestWoundedAllyNearPoint(Vector2 point, float radius, int zLevel)
        {
            HumanCharacterController best = null;
            float bestDistSq = radius * radius;

            var player = GameController.instance?.playerCharacter;
            if (Il2CppSafe.IsAliveUnity(player) && !player.deadFlag &&
                (!Il2CppSafe.IsAlive(player.characterData) || player.characterData.zLevel == zLevel) &&
                TryGetCharacterHealth(player, out float php, out float pmax) && php < pmax * 0.92f)
            {
                float sq = ((Vector2)player.transform.position - point).sqrMagnitude;
                if (sq <= bestDistSq)
                {
                    bestDistSq = sq;
                    best = player;
                }
            }

            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || ag.npcData.zLevel != zLevel)
                    continue;
                var hc = ag.humanCharacter;
                if (!Il2CppSafe.IsAliveUnity(hc) || hc.deadFlag)
                    continue;
                if (!TryGetCharacterHealth(hc, out float hp, out float maxHp) || hp >= maxHp * 0.92f)
                    continue;

                float sq = ((Vector2)hc.transform.position - point).sqrMagnitude;
                if (sq < bestDistSq)
                {
                    bestDistSq = sq;
                    best = hc;
                }
            }

            return best;
        }

        private static bool TryGetCharacterHealth(BasicCharacterController ch, out float hp, out float maxHp)
        {
            hp = 0f;
            maxHp = 0f;
            if (!Il2CppSafe.IsAliveUnity(ch))
                return false;

            try
            {
                if (Il2CppSafe.IsAlive(ch.characterData) &&
                    TryReadHpPair(ch.characterData, out hp, out maxHp))
                {
                    return true;
                }
                return TryReadHpPair(ch, out hp, out maxHp);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadHpPair(object target, out float hp, out float maxHp)
        {
            hp = 0f;
            maxHp = 0f;
            if (target == null)
                return false;

            Type t = target.GetType();
            if (!CachedHpMembers.TryGetValue(t, out MemberInfo hpMember))
            {
                hpMember = FindNamedNumericMember(t, new[]
                {
                    "hp", "health", "currentHp", "currentHealth", "curHp", "curHealth", "hitPoints", "m_hp", "m_health"
                });
                CachedHpMembers[t] = hpMember;
            }

            if (!CachedMaxHpMembers.TryGetValue(t, out MemberInfo maxHpMember))
            {
                maxHpMember = FindNamedNumericMember(t, new[]
                {
                    "maxHp", "maxHP", "maxHealth", "hpMax", "healthMax", "m_maxHp", "m_maxHealth"
                });
                CachedMaxHpMembers[t] = maxHpMember;
            }

            if (hpMember == null || maxHpMember == null)
                return false;

            return TryConvertFloat(GetMemberValue(hpMember, target), out hp) &&
                   TryConvertFloat(GetMemberValue(maxHpMember, target), out maxHp) &&
                   maxHp > 0.01f;
        }

        private static bool TryApplyFieldHeal(
            GOAPAgent healer,
            HumanCharacterController patient,
            float healFraction,
            float minFlatHeal)
        {
            if (!Il2CppSafe.IsAliveUnity(patient) || patient.deadFlag)
                return false;

            try
            {
                object holder = Il2CppSafe.IsAlive(patient.characterData) ? (object)patient.characterData : patient;
                if (!TryReadHpPair(holder, out float hp, out float maxHp) || hp >= maxHp)
                    return false;

                float delta = Mathf.Max(minFlatHeal, maxHp * healFraction);
                float nextHp = Mathf.Min(maxHp, hp + delta);

                if (CachedHpMembers.TryGetValue(holder.GetType(), out MemberInfo hpMember) && hpMember != null)
                {
                    if (hpMember is PropertyInfo pi && pi.CanWrite)
                    {
                        pi.SetValue(holder, ConvertToMemberNumericType(nextHp, pi.PropertyType));
                        return true;
                    }
                    if (hpMember is FieldInfo fi && !fi.IsInitOnly)
                    {
                        fi.SetValue(holder, ConvertToMemberNumericType(nextHp, fi.FieldType));
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool IsCharacterDowned(HumanCharacterController hc)
        {
            if (!Il2CppSafe.IsAliveUnity(hc) || hc.deadFlag)
                return false;

            try
            {
                if (CheckDownedFlagsOnObject(hc))
                    return true;
                if (Il2CppSafe.IsAlive(hc.characterData) && CheckDownedFlagsOnObject(hc.characterData))
                    return true;
                if (TryGetCharacterHealth(hc, out float hp, out float maxHp) && maxHp > 1f && hp <= 0.5f)
                    return true;
            }
            catch { }

            return false;
        }

        private static bool CheckDownedFlagsOnObject(object obj)
        {
            if (obj == null)
                return false;

            Type t = obj.GetType();
            if (!CachedDownedMembers.TryGetValue(t, out List<MemberInfo> members))
            {
                members = new List<MemberInfo>(4);
                string[] names =
                {
                    "downFlag", "isDown", "isDowned", "dyingFlag", "isDying",
                    "knockDownFlag", "isKnockedDown", "unconsciousFlag", "isUnconscious", "needRescue"
                };
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
                for (int i = 0; i < names.Length; i++)
                {
                    var p = t.GetProperty(names[i], flags);
                    if (p != null && p.PropertyType == typeof(bool))
                        members.Add(p);
                    var f = t.GetField(names[i], flags);
                    if (f != null && f.FieldType == typeof(bool))
                        members.Add(f);
                }
                CachedDownedMembers[t] = members;
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (GetMemberValue(members[i], obj) is bool b && b)
                    return true;
            }
            return false;
        }

        private static bool IsBossCharacter(BasicCharacterController ch)
        {
            if (!Il2CppSafe.IsAliveUnity(ch))
                return false;

            try
            {
                string goName = ch.gameObject?.name ?? "";
                if (goName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (Il2CppSafe.IsAlive(ch.characterData))
                {
                    Type dt = ch.characterData.GetType();
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
                    var p = dt.GetProperty("isBoss", flags) ?? dt.GetProperty("bossFlag", flags);
                    if (p != null && p.PropertyType == typeof(bool) && (bool)p.GetValue(ch.characterData))
                        return true;
                }

                if (ch.TryCast<HumanCharacterController>() == null &&
                    TryGetCharacterHealth(ch, out _, out float maxHp) &&
                    maxHp >= 1200f)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool HasHealingSmokeInInventory(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent))
                return false;

            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var actionType = typeof(GOAPAction_TeamOrder_ThrowHealingSmoke);
                string[] precondNames =
                {
                    "CheckProceduralPrecondition", "CheckCondition", "CanExecute", "CanPerform", "IsValid"
                };
                for (int i = 0; i < precondNames.Length; i++)
                {
                    var precond = actionType.GetMethod(precondNames[i], flags, null, new[] { typeof(GOAPAgent) }, null);
                    if (precond != null && precond.ReturnType == typeof(bool))
                    {
                        Vector2 probePos = agent.humanCharacter != null
                            ? (Vector2)agent.humanCharacter.transform.position
                            : Vector2.zero;
                        var probe = new GOAPAction_TeamOrder_ThrowHealingSmoke(probePos);
                        probe.targetPosition = probePos;
                        return (bool)precond.Invoke(probe, new object[] { agent });
                    }
                }

                object cd = agent.humanCharacter?.characterData;
                if (cd != null && ScanCharacterObjectForHealingSmoke(cd, flags))
                    return true;
                if (Il2CppSafe.IsAlive(agent.npcData) && ScanCharacterObjectForHealingSmoke(agent.npcData, flags))
                    return true;
            }
            catch { }

            return false;
        }

        private static bool ScanCharacterObjectForHealingSmoke(object owner, BindingFlags flags)
        {
            Type t = owner.GetType();
            var props = t.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                if (p.GetIndexParameters().Length > 0)
                    continue;
                string pn = p.Name ?? "";
                if (pn.IndexOf("item", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pn.IndexOf("inv", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pn.IndexOf("bag", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pn.IndexOf("equip", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pn.IndexOf("grenade", StringComparison.OrdinalIgnoreCase) < 0 &&
                    pn.IndexOf("throw", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                object val = null;
                try { val = p.GetValue(owner); } catch { }
                if (val == null)
                    continue;

                if (LooksLikeHealingSmokeItem(val))
                    return true;

                if (val is System.Collections.IEnumerable enumerable && !(val is string))
                {
                    int inspected = 0;
                    foreach (object elem in enumerable)
                    {
                        if (++inspected > 128)
                            break;
                        if (elem != null && LooksLikeHealingSmokeItem(elem))
                            return true;
                    }
                }
            }
            return false;
        }

        private static bool LooksLikeHealingSmokeItem(object itemObj)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            Type it = itemObj.GetType();
            string[] nameProps = { "itemName", "internalName", "prefabName", "idName", "name" };
            for (int i = 0; i < nameProps.Length; i++)
            {
                var mp = it.GetProperty(nameProps[i], flags);
                if (mp != null && mp.PropertyType == typeof(string))
                {
                    string s = null;
                    try { s = mp.GetValue(itemObj) as string; } catch { }
                    if (!string.IsNullOrEmpty(s) &&
                        s.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (s.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         s.IndexOf("med", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static MemberInfo FindNamedNumericMember(Type t, string[] candidates)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            for (int i = 0; i < candidates.Length; i++)
            {
                var p = t.GetProperty(candidates[i], flags);
                if (p != null && IsNumericType(p.PropertyType))
                    return p;
                var f = t.GetField(candidates[i], flags);
                if (f != null && IsNumericType(f.FieldType))
                    return f;
            }
            return null;
        }

        private static bool IsNumericType(Type t)
        {
            return t == typeof(float) || t == typeof(int) || t == typeof(double) || t == typeof(short);
        }

        private static object GetMemberValue(MemberInfo member, object target)
        {
            try
            {
                if (member is PropertyInfo pi) return pi.GetValue(target);
                if (member is FieldInfo fi) return fi.GetValue(target);
            }
            catch { }
            return null;
        }

        private static bool TryConvertFloat(object val, out float result)
        {
            result = 0f;
            if (val == null) return false;
            try
            {
                result = Convert.ToSingle(val);
                return IsFinite(result);
            }
            catch
            {
                return false;
            }
        }

        private static object ConvertToMemberNumericType(float value, Type targetType)
        {
            if (targetType == typeof(int)) return Mathf.RoundToInt(value);
            if (targetType == typeof(double)) return (double)value;
            if (targetType == typeof(short)) return (short)Mathf.Clamp(Mathf.RoundToInt(value), short.MinValue, short.MaxValue);
            return value;
        }
    }
}
