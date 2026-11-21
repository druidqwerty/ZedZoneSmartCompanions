using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// v2.6.0 Autonomous Survival: bio-mask auto-swap with everyday mask protection,
    /// rare-gated debuff remedies, and sentry-guarded camp sleep rotation.
    /// </summary>
    public static class CompanionSurvivalSystem
    {
        public const float BioRadiationThreshold = 3.5f;
        public const float SleepFatigueThreshold = 0.28f;
        public const float RestedWakeThreshold = 0.90f;
        public const float BedReachRadius = PhysicalMovementHelper.MaxReachableInteractRadius; // 2.45m

        public const int BuffIdBurning = 1;
        public const int BuffIdHypothermia = 6;
        public const int BuffIdHighFever = 7;
        public const int BuffIdLowFever = 14;
        public const int BuffIdSpore = 16;
        public const int BuffIdDrowning = 25;
        public const int ItemIdFeverReducer = 234;

        private static readonly HashSet<int> CommonRemedyIds = new HashSet<int> { 220, 222, 234, 276 };
        private static readonly HashSet<int> RareSerumIds = new HashSet<int> { 249, 250, 251, 252, 277 };

        public static readonly Dictionary<string, ItemData> SavedEverydayMaskByGuid =
            new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ActiveBioSwapGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> WalkingToBedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastRadiationByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastRemedyAtByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastSelfCareAtByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastCampResupplyAtByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> NextTickByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static float _nextSleepScanAt = -999f;
        private static MethodInfo _hostSendEquipMethod;
        private static bool _hostSendEquipResolved;

        public static bool IsAtCampOrPlayerBase(Vector2 pos, CompanionRuntimeState st) =>
            (st != null && st.RosterMode == CompanionRosterMode.CampGarrison &&
             (!st.HasHomeAnchor || Vector2.Distance(pos, st.HomeAnchorPos) <= 32f)) ||
            AutoLootSystem.IsPositionNearPlayerBase(pos, 28f);

        public static bool DoesAgentNeedNativeSurvival(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData))
                return false;
            try
            {
                if (agent.currentGoal != null &&
                    (agent.currentGoal.TryCast<GOAPGoal_SatisfyNeeds>() != null ||
                     agent.currentGoal.TryCast<GOAPGoal_StayAlive>() != null))
                    return true;

                var cd = agent.npcData.characterData;
                if (HasTreatableInjuryState(cd, out _, out _))
                    return true;

                var status = cd?.characterStatusData;
                if (Il2CppSafe.IsAlive(status))
                {
                    float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
                    float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
                    float maxHp = status.maxhp > 1f ? status.maxhp : 100f;
                    return (status.hunger / maxH) < 0.48f ||
                           (status.thirst / maxT) < 0.48f ||
                           (status.hp / maxHp) < 0.78f;
                }

                float npcMaxHp = agent.npcData.maxHealth > 1f ? agent.npcData.maxHealth : 100f;
                return agent.npcData.hunger < 48f ||
                       agent.npcData.thirst < 48f ||
                       agent.npcData.health < npcMaxHp * 0.78f;
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldSuppressVanillaFoodWaterSearch(GOAPAgent agent, CompanionRuntimeState st = null) =>
            ShouldSuppressVanillaFoodWaterSearchCore(agent, st, null);

        public static bool ShouldSuppressVanillaFoodWaterSearch(GOAPAgent agent, CompanionRuntimeState st, bool forWater) =>
            ShouldSuppressVanillaFoodWaterSearchCore(agent, st, forWater);

        private static bool ShouldSuppressVanillaFoodWaterSearchCore(GOAPAgent agent, CompanionRuntimeState st, bool? forWater)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient ||
                !CompanionConfigStore.EnableSmartCombat ||
                !CompanionConfigStore.EnableAutoBioAndSleep)
                return false;

            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;

            if (st == null && !string.IsNullOrEmpty(agent.npcData.npcGUID))
                CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID, out st);
            if (st == null || st.IsDismissedOrNotInTeam || st.IsSortingActive)
                return false;

            Vector2 pos = agent.humanCharacter.transform.position;
            bool canCoverCamp = !BaseWorkManager.Paused &&
                                (st.RosterMode == CompanionRosterMode.CampGarrison ||
                                 (st.ActiveOrder == TacticalOrderKind.Follow && st.AllowBaseChores)) &&
                                IsAtCampOrPlayerBase(pos, st);
            if (canCoverCamp)
                return true;

            var cd = agent.npcData.characterData;
            if (forWater.HasValue)
                return HasOwnSupplyForNeed(agent, cd, agent.npcData, forWater.Value);

            var status = cd?.characterStatusData;
            if (Il2CppSafe.IsAlive(status))
            {
                float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
                float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
                bool hungry = (status.hunger / maxH) < 0.50f;
                bool thirsty = (status.thirst / maxT) < 0.50f;
                if (hungry && !HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: false)) return false;
                if (thirsty && !HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: true)) return false;
            }
            return HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: false) &&
                   HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: true);
        }

        private static bool HasOwnSupplyForNeed(GOAPAgent agent, CharacterData cd, GOAPNPCData npcData, bool forThirst)
        {
            try
            {
                if (forThirst)
                {
                    var ownDrinks = TryGetAgentDrinkingItems(agent);
                    if (ownDrinks != null && ownDrinks.Count > 0)
                        return true;
                }
                var invs = CollectCompanionInventories(cd, npcData);
                return TryFindConsumableFoodInInventories(invs, forThirst, CompanionConfigStore.AllowSpendRareFood, out _, out _, out _);
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasTreatableInjuryState(CharacterData cd, out bool isBleeding, out bool hasUnbandagedWound)
        {
            isBleeding = false;
            hasUnbandagedWound = false;
            if (!Il2CppSafe.IsAlive(cd)) return false;
            try
            {
                return HasTreatableInjuryStateNoInline(cd, out isBleeding, out hasUnbandagedWound);
            }
            catch
            {
                isBleeding = false;
                hasUnbandagedWound = false;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasTreatableInjuryStateNoInline(CharacterData cd, out bool isBleeding, out bool hasUnbandagedWound)
        {
            isBleeding = false;
            hasUnbandagedWound = false;
            var parts = cd.bodypartsData?.bodypartDataAry;
            if (parts == null) return false;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!Il2CppSafe.IsAlive(part)) continue;
                if (part.isBleeding) isBleeding = true;
                if (part.isWounded && !Il2CppSafe.IsAlive(part.bandageData)) hasUnbandagedWound = true;
            }
            return isBleeding || hasUnbandagedWound;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsAnyCloseCombatDanger(GOAPAgent agent, float dangerRadius = 4.5f)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return false;
            try
            {
                if (CombatResponse.HasCloseLineOfSightThreat(agent, agent.mostThreateningCharacter, dangerRadius))
                    return true;
                if (agent.threatDataDic == null || agent.threatDataDic.Count == 0)
                    return false;
                foreach (var kv in agent.threatDataDic)
                {
                    if (CombatResponse.HasCloseLineOfSightThreat(agent, kv.Key, dangerRadius))
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static bool ShouldWearBioProtectionMask(bool isBloodRaining, float radiationDose, bool inHazardZone) =>
            isBloodRaining || (!float.IsNaN(radiationDose) && radiationDose > BioRadiationThreshold) || inHazardZone;

        public static int SelectSleepShiftCandidateIndex(
            IReadOnlyList<(float fatigueRatio, bool alreadySleeping, bool inCombat)> squad)
        {
            if (squad == null || squad.Count <= 1) return -1;

            int awakeCount = 0;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i].inCombat) return -1;
                if (!squad[i].alreadySleeping) awakeCount++;
            }
            if (awakeCount - 1 < 1) return -1;

            int bestIdx = -1;
            float lowest = SleepFatigueThreshold;
            for (int i = 0; i < squad.Count; i++)
            {
                var m = squad[i];
                if (m.alreadySleeping || float.IsNaN(m.fatigueRatio) || float.IsInfinity(m.fatigueRatio)) continue;
                if (m.fatigueRatio < lowest) { lowest = m.fatigueRatio; bestIdx = i; }
            }
            return bestIdx;
        }

        public static int ChooseDebuffRemedyItemId(
            IReadOnlyList<int> availableMedIds, bool allowRareSerums, bool lifeThreatening)
        {
            if (availableMedIds == null || availableMedIds.Count == 0) return 0;

            for (int i = 0; i < availableMedIds.Count; i++)
            {
                int id = availableMedIds[i];
                if (CommonRemedyIds.Contains(id) && !CompanionProfileSystem.IsRareMedItem(id))
                    return id;
            }
            if (allowRareSerums && lifeThreatening)
            {
                for (int i = 0; i < availableMedIds.Count; i++)
                {
                    int id = availableMedIds[i];
                    if (RareSerumIds.Contains(id) && CompanionProfileSystem.IsRareMedItem(id))
                        return id;
                }
            }
            return 0;
        }

        public static bool IsProtectedSavedMask(string npcGuid, IntPtr itemPtr) =>
            !string.IsNullOrEmpty(npcGuid) && itemPtr != IntPtr.Zero &&
            SavedEverydayMaskByGuid.TryGetValue(npcGuid, out ItemData saved) &&
            Il2CppSafe.IsAlive(saved) && saved.Pointer == itemPtr;

        public static void TickCompanionSurvival(GOAPAgent agent, CompanionRuntimeState st, float now)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient || st == null) return;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag) return;

            var gc = GameController.instance;
            var hc = agent.humanCharacter;
            string guid = agent.npcData.npcGUID ?? st.NpcGuid ?? "";
            if (!st.IsChoreWalkActive) WalkingToBedGuids.Remove(guid);

            bool inCombat = (Il2CppSafe.IsAliveUnity(gc) && gc.isPlayerInCombat) ||
                            (Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag);
            if (inCombat && (hc.isSleeping || WalkingToBedGuids.Contains(guid)))
                WakeCompanionImmediate(hc, st, guid);

            if (NextTickByGuid.TryGetValue(guid, out float nextAt) && now < nextAt) return;
            NextTickByGuid[guid] = now + 1.0f;

            var cd = agent.npcData.characterData;
            var status = cd?.characterStatusData;
            var eq = cd?.characterEquipmentData;
            if (!Il2CppSafe.IsAlive(cd) || !Il2CppSafe.IsAlive(status) || !Il2CppSafe.IsAlive(eq)) return;

            if (!inCombat && hc.isSleeping)
            {
                float maxF = status.maxFatigue > 0.01f ? status.maxFatigue : 100f;
                if ((status.fatigue / maxF) >= RestedWakeThreshold)
                    WakeCompanionImmediate(hc, st, guid);
            }

            UpdateBioProtectionMask(hc, cd, status, eq, guid, gc);
            TryTreatSurvivalDebuffs(agent, cd, status, eq, st, guid, now);
            TryAutonomousEatDrinkAndBandage(agent, hc, cd, status, st, guid, now, inCombat);
            if (!inCombat && !hc.isSleeping)
                CompanionGearRepairSystem.TickCompanionGearRepair(agent, st, now);

            if (!inCombat && now >= _nextSleepScanAt)
            {
                _nextSleepScanAt = now + 4.0f;
                TryScheduleCampSleepShift(now);
            }
        }

        public static bool TryScheduleCampSleepShift(float now)
        {
            var gc = GameController.instance;
            if (MultiplayerSyncSystem.IsMultiplayerClient || !Il2CppSafe.IsAliveUnity(gc)) return false;

            var rawSquad = SmartChatterSystem.GetActiveTeammateAgents();
            if (gc.isPlayerInCombat)
            {
                for (int i = 0; i < rawSquad.Count; i++)
                {
                    var a = rawSquad[i];
                    if (!Il2CppSafe.IsAliveUnity(a) || !Il2CppSafe.IsAliveUnity(a.humanCharacter)) continue;
                    string g = a.npcData?.npcGUID ?? "";
                    if (a.humanCharacter.isSleeping || WalkingToBedGuids.Contains(g))
                    {
                        CompanionConfigStore.CompanionStates.TryGetValue(g, out var s);
                        WakeCompanionImmediate(a.humanCharacter, s, g);
                    }
                }
                return false;
            }

            var validAgents = new List<GOAPAgent>(rawSquad.Count);
            var snapshot = new List<(float fatigueRatio, bool alreadySleeping, bool inCombat)>(rawSquad.Count);
            for (int i = 0; i < rawSquad.Count; i++)
            {
                var ag = rawSquad[i];
                var status = ag?.npcData?.characterData?.characterStatusData;
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) ||
                    ag.humanCharacter.deadFlag || !Il2CppSafe.IsAlive(status)) continue;

                string g = ag.npcData.npcGUID ?? "";
                CompanionConfigStore.CompanionStates.TryGetValue(g, out var mst);
                if (mst == null || !mst.IsChoreWalkActive) WalkingToBedGuids.Remove(g);
                if (mst == null || (mst.RosterMode != CompanionRosterMode.CampGarrison && mst.ActiveOrder != TacticalOrderKind.Follow) ||
                    !mst.AllowBaseChores || mst.HasAnyPhysicalTask ||
                    !IsAtCampOrPlayerBase(ag.humanCharacter.transform.position, mst)) continue;

                float maxF = status.maxFatigue > 0.01f ? status.maxFatigue : 100f;
                bool sleeping = ag.humanCharacter.isSleeping || WalkingToBedGuids.Contains(g);
                bool combat = Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag;
                validAgents.Add(ag);
                snapshot.Add((Mathf.Clamp01(status.fatigue / maxF), sleeping, combat));
            }

            int idx = SelectSleepShiftCandidateIndex(snapshot);
            if (idx < 0 || idx >= validAgents.Count) return false;

            var chosen = validAgents[idx];
            string chosenGuid = chosen.npcData.npcGUID ?? "";
            var st = CompanionConfigStore.GetOrCreateCompanion(chosenGuid, CompanionConfigStore.GetCharacterDisplayName(chosen.npcData.characterData));
            Vector2 selfPos = chosen.humanCharacter.transform.position;
            if ((st.RosterMode != CompanionRosterMode.CampGarrison && st.ActiveOrder != TacticalOrderKind.Follow) ||
                !st.AllowBaseChores || st.HasAnyPhysicalTask || !IsAtCampOrPlayerBase(selfPos, st)) return false;

            var bed = FindNearestReachableBed(selfPos, 22f);
            if (!Il2CppSafe.IsAliveUnity(bed)) return false;

            Vector2 bedPos = bed.transform.position;
            if (PhysicalMovementHelper.ComputeSurfaceDistance(selfPos, bedPos, bed.transform) <= BedReachRadius)
                return BeginBedSleep(chosen, st, chosenGuid);

            WalkingToBedGuids.Add(chosenGuid);
            BaseChoreSystem.StartPhysicalChoreWalk(
                chosen, st, bedPos, bed.transform,
                CompanionConfigStore.L("💤 Идёт на смену сна...", "💤 Walking to bed shift...", "💤 前往轮班休息..."),
                0,
                () =>
                {
                    WalkingToBedGuids.Remove(chosenGuid);
                    if (!Il2CppSafe.IsAliveUnity(chosen) || !Il2CppSafe.IsAliveUnity(bed)) return;
                    if (Il2CppSafe.IsAliveUnity(GameController.instance) && GameController.instance.isPlayerInCombat) return;
                    if (CountAwakeTeammatesExcluding(chosenGuid) < 1) return;
                    if (PhysicalMovementHelper.ComputeSurfaceDistance(chosen.humanCharacter.transform.position, bed.transform.position, bed.transform) <= BedReachRadius)
                        BeginBedSleep(chosen, st, chosenGuid);
                },
                $"bed_{bed.Pointer.ToInt64():X}");
            return true;
        }

        private static void UpdateBioProtectionMask(
            HumanCharacterController hc, CharacterData cd, CharacterStatusData status,
            CharacterEquipmentData eq, string guid, GameController gc)
        {
            bool bloodRain = Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAlive(gc.gameData) && gc.gameData.isBloodRaining;
            float rad = status.radiationDose;
            bool risingHazard = LastRadiationByGuid.TryGetValue(guid, out float prevRad) && (rad > prevRad + 0.02f);
            LastRadiationByGuid[guid] = rad;

            var inv = Il2CppSafe.IsAlive(cd.inventoryData) ? cd.inventoryData : eq.backpackItemData?.inventoryData;
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return;

            if (ShouldWearBioProtectionMask(bloodRain, rad, risingHazard))
            {
                if ((ActiveBioSwapGuids.Contains(guid) && Il2CppSafe.IsAlive(eq.faceMaskItemData)) ||
                    (Il2CppSafe.IsAlive(eq.faceMaskItemData) && IsGasMaskItem(eq.faceMaskItemData))) return;

                var bioMask = FindBestMaskInInventory(inv, guid);
                if (!Il2CppSafe.IsAlive(bioMask)) return;

                var prevMask = eq.faceMaskItemData;
                inv.RemoveItem(bioMask, false);
                if (Il2CppSafe.IsAlive(prevMask))
                {
                    if (!InventoryTransfers.Contains(inv, prevMask))
                    {
                        if (!inv.TryAddItem(prevMask, out bool partial, false, true) || partial)
                        {
                            if (InventoryTransfers.Contains(inv, prevMask)) inv.RemoveItem(prevMask, false);
                            inv.TryAddItem(bioMask, out _, false, true);
                            return;
                        }
                    }
                    SavedEverydayMaskByGuid[guid] = prevMask;
                }
                else SavedEverydayMaskByGuid.Remove(guid);

                ActiveBioSwapGuids.Add(guid);
                eq.faceMaskItemData = bioMask;
                try { hc.RefreshWornFaceMaskSprites(bioMask); } catch { }
                SyncNpcEquipment(hc);
            }
            else if (ActiveBioSwapGuids.Contains(guid))
            {
                var wornBio = eq.faceMaskItemData;
                SavedEverydayMaskByGuid.TryGetValue(guid, out var savedMask);
                SavedEverydayMaskByGuid.Remove(guid);
                ActiveBioSwapGuids.Remove(guid);

                if (Il2CppSafe.IsAlive(savedMask))
                {
                    if (InventoryTransfers.Contains(inv, savedMask)) inv.RemoveItem(savedMask, false);
                    if (Il2CppSafe.IsAlive(wornBio) && wornBio.Pointer != savedMask.Pointer && !InventoryTransfers.Contains(inv, wornBio))
                        inv.TryAddItem(wornBio, out _, false, true);
                    eq.faceMaskItemData = savedMask;
                }
                else
                {
                    if (Il2CppSafe.IsAlive(wornBio) && !InventoryTransfers.Contains(inv, wornBio))
                        inv.TryAddItem(wornBio, out _, false, true);
                    eq.faceMaskItemData = null;
                }
                try { hc.RefreshWornFaceMaskSprites(eq.faceMaskItemData); } catch { }
                SyncNpcEquipment(hc);
            }
        }

        public static float ComputeTeammateClearDebuffPriority(
            float rawPriority, bool hasFever, bool hasCriticalNonFever, bool inCombat, bool hasPlayerOrder)
        {
            if (rawPriority <= 0f || hasCriticalNonFever || !hasFever)
                return rawPriority;
            if (inCombat || hasPlayerOrder)
                return -100f;
            return rawPriority > 45f ? 45f : rawPriority;
        }

        public static bool ShouldAbortIdleFeverClearAction(
            bool inCombat, bool hasPlayerOrder, bool hasWarmSourceCovering,
            int gulpCount, int maxGulps, bool hasDrinkableWater, float elapsedSeconds)
        {
            if (inCombat || hasPlayerOrder)
                return true;
            if (hasWarmSourceCovering)
                return false;
            if (elapsedSeconds >= 4.0f)
                return true;
            int safeMax = maxGulps > 0 ? maxGulps : 3;
            return !hasDrinkableWater || gulpCount >= safeMax;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HasBuffSafe(CharacterData cd, int buffId)
        {
            if (!Il2CppSafe.IsAlive(cd)) return false;
            try
            {
                return HasBuffSafeNoInline(cd, buffId);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasBuffSafeNoInline(CharacterData cd, int buffId) =>
            Il2CppSafe.IsAlive(cd.GetBuffDataByBuffID(buffId));

        public static bool HasFeverDebuff(CharacterData cd) =>
            HasBuffSafe(cd, BuffIdHighFever) || HasBuffSafe(cd, BuffIdLowFever);

        public static bool HasCriticalNonFeverDebuff(CharacterData cd) =>
            HasBuffSafe(cd, BuffIdBurning) || HasBuffSafe(cd, BuffIdDrowning);

        public static bool IsInterruptibleClearDebuffForCombat(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData))
                return false;
            var cd = agent.npcData.characterData;
            return HasFeverDebuff(cd) && !HasCriticalNonFeverDebuff(cd);
        }

        public static bool HasActivePlayerOrderOrTask(GOAPAgent agent, CompanionRuntimeState st = null)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData))
                return false;
            try
            {
                var hc = agent.humanCharacter;
                if (Il2CppSafe.IsAliveUnity(hc) && (hc.isDriving || Il2CppSafe.IsAliveUnity(hc.drivingVehicle)))
                    return true;

                if (st == null && !string.IsNullOrEmpty(agent.npcData.npcGUID))
                    CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID, out st);
                if (st != null && (st.ActiveOrder != TacticalOrderKind.Follow || st.HasAnyPhysicalTask))
                    return true;

                if (CampGarrisonSystem.ShouldStayAnchoredBehind(agent.npcData))
                    return true;

                if (Il2CppSafe.IsAlive(agent.agentState) && !string.IsNullOrEmpty(agent.agentState.onetimeCommand))
                    return true;
            }
            catch { }
            return false;
        }

        public static bool HasActiveCombatThreat(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return false;
            try
            {
                var threat = agent.mostThreateningCharacter;
                if (Il2CppSafe.IsAliveUnity(threat) && !threat.deadFlag && threat.enabled && threat.gameObject.activeInHierarchy)
                    return true;
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SuppressFeverDebuffGoal(GOAPAgent agent, float durationSeconds)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.agentState))
                return;
            try
            {
                SuppressFeverDebuffGoalNoInline(agent.agentState, durationSeconds);
            }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SuppressFeverDebuffGoalNoInline(GOAPAgentState agentState, float durationSeconds)
        {
            string key = GOAPGoal_ClearDebuff.feverSuppressedUntilKey;
            if (string.IsNullOrEmpty(key)) key = "feverSuppressedUntil";
            float until = Time.time + Mathf.Max(5f, durationSeconds);
            IntPtr cls = Il2CppInterop.Runtime.Il2CppClassPointerStore<float>.NativeClassPtr;
            if (cls == IntPtr.Zero) return;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(float));
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(new[] { until }, 0, ptr, 1);
                IntPtr boxed = Il2CppInterop.Runtime.IL2CPP.il2cpp_value_box(cls, ptr);
                if (boxed != IntPtr.Zero)
                    agentState.SetState(key, new Il2CppSystem.Object(boxed));
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ReleaseCompanionFromFeverFreeze(GOAPAgent agent, float suppressSeconds = 25f)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData))
                return;
            try
            {
                if (!agent.IsPlayerTeammate()) return;
                if (!IsInterruptibleClearDebuffForCombat(agent)) return;
                SuppressFeverDebuffGoal(agent, suppressSeconds);
                if (agent.currentGoal != null && agent.currentGoal.TryCast<GOAPGoal_ClearDebuff>() != null)
                {
                    agent.goalReselectDeferred = true;
                    try { agent.SelectPriorityGoal(); } catch { }
                }
            }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyFoodBuffsSafe(HumanCharacterController hc, ItemData item)
        {
            if (!Il2CppSafe.IsAliveUnity(hc) || !Il2CppSafe.IsAlive(item)) return;
            try { ApplyFoodBuffsNoInline(hc, item); } catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyFoodBuffsNoInline(HumanCharacterController hc, ItemData item) =>
            hc.AddFoodBuffs(item);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RemoveBuffSafe(HumanCharacterController hc, int buffId)
        {
            if (!Il2CppSafe.IsAliveUnity(hc)) return;
            try { RemoveBuffNoInline(hc, buffId); } catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RemoveBuffNoInline(HumanCharacterController hc, int buffId) =>
            hc.RemoveBuff(buffId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryImmediateFeverMedicine(GOAPAgent agent, CompanionRuntimeState st = null)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient) return false;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;

            var hc = agent.humanCharacter;
            var cd = agent.npcData.characterData;
            var status = cd?.characterStatusData;
            if (!Il2CppSafe.IsAlive(cd) || !Il2CppSafe.IsAlive(status)) return false;

            float stdTemp = CharacterStatusData.standardBodyTemperature > 30f ? CharacterStatusData.standardBodyTemperature : 37f;
            bool hasHighFever = HasBuffSafe(cd, BuffIdHighFever);
            bool hasLowFever = HasBuffSafe(cd, BuffIdLowFever);
            bool feverish = hasHighFever || hasLowFever || status.bodyTemperature > stdTemp + 1.5f;
            if (!feverish) return false;

            if (st == null && !string.IsNullOrEmpty(agent.npcData.npcGUID))
                CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID, out st);

            var invs = CollectCompanionInventories(cd, agent.npcData);
            if (invs.Count == 0) return false;

            int medUnits = 0;
            InventoryData feverInv = null;
            ItemData feverItem = null;
            for (int invIdx = 0; invIdx < invs.Count; invIdx++)
            {
                var inv = invs[invIdx];
                if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                for (int i = 0; i < inv.itemList.Count; i++)
                {
                    var it = inv.itemList[i];
                    if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f) continue;
                    if (ItemCategorizer.ClassifyItem(it) == StorageCategory.Medicine)
                        medUnits += Mathf.Max(1, Mathf.FloorToInt(it.itemNumberFloat));
                    if (feverItem == null && it.itemId == ItemIdFeverReducer)
                    {
                        feverInv = inv;
                        feverItem = it;
                    }
                }
            }

            if (!Il2CppSafe.IsAlive(feverItem) || !Il2CppSafe.IsAlive(feverInv))
                return false;

            int reserveMin = st != null ? st.ReserveMedMin : 0;
            if (!hasHighFever && medUnits <= reserveMin)
                return false;

            ApplyFoodBuffsSafe(hc, feverItem);
            bool consumed = false;
            try { consumed = CharacterActionManager.ConsumeOneFood(cd, feverInv, feverItem); } catch { }
            if (!consumed) return false;

            if (status.bodyTemperature > stdTemp)
                status.bodyTemperature = stdTemp;
            RemoveBuffSafe(hc, BuffIdHighFever);
            RemoveBuffSafe(hc, BuffIdLowFever);
            SuppressFeverDebuffGoal(agent, 15f);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool HasDrinkableWaterWithVolume(GOAPAgent agent)
        {
            try
            {
                var drinks = TryGetAgentDrinkingItems(agent);
                if (drinks == null || drinks.Count == 0) return false;
                for (int i = 0; i < drinks.Count; i++)
                {
                    var d = drinks[i];
                    if (!Il2CppSafe.IsAlive(d) || d.properties == null || d.properties.Length < 2) continue;
                    if (d.properties[1] > 0.01f) return true;
                }
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsCoveredByWarmSourceSafe(Vector2 pos)
        {
            try
            {
                return IsCoveredByWarmSourceNoInline(pos);
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsCoveredByWarmSourceNoInline(Vector2 pos) =>
            Il2CppSafe.IsAliveUnity(GOAPAction_ClearDebuff_Fever.FindWarmSourceCovering(pos));

        private static void TryTreatSurvivalDebuffs(
            GOAPAgent agent, CharacterData cd, CharacterStatusData status,
            CharacterEquipmentData eq, CompanionRuntimeState st, string guid, float now)
        {
            if (LastRemedyAtByGuid.TryGetValue(guid, out float lastAt) && now - lastAt < 12f) return;

            float stdTemp = CharacterStatusData.standardBodyTemperature > 30f ? CharacterStatusData.standardBodyTemperature : 37f;
            bool hasHighFever = HasBuffSafe(cd, BuffIdHighFever);
            bool hasLowFever = HasBuffSafe(cd, BuffIdLowFever);
            bool hasHypothermia = HasBuffSafe(cd, BuffIdHypothermia);
            bool hypothermic = hasHypothermia || (status.bodyTemperature > 10f && status.bodyTemperature < stdTemp - 1.5f);
            bool feverish = hasHighFever || hasLowFever || status.bodyTemperature > stdTemp + 1.5f;
            bool irradiated = status.radiationDose > BioRadiationThreshold;
            bool lifeThreatening = (status.maxhp > 1f && (status.hp / status.maxhp) < 0.38f) ||
                                   status.radiationDose > 12f ||
                                   hasHighFever ||
                                   (status.bodyTemperature > 10f && status.bodyTemperature < stdTemp - 3.0f);
            if (!hypothermic && !feverish && !irradiated && !lifeThreatening) return;

            LastRemedyAtByGuid[guid] = now;
            if (HasBuffSafe(cd, BuffIdBurning))
            {
                try { new GOAPAction_ClearDebuff_Burning().Execute(agent); } catch { }
            }

            if (feverish && TryImmediateFeverMedicine(agent, st))
                return;

            if (!irradiated && !hypothermic && !lifeThreatening)
                return;

            var invs = CollectCompanionInventories(cd, agent.npcData);
            if (invs.Count == 0) return;

            var medIds = new List<int>();
            int medUnits = 0;
            for (int invIdx = 0; invIdx < invs.Count; invIdx++)
            {
                var inv = invs[invIdx];
                if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                for (int i = 0; i < inv.itemList.Count; i++)
                {
                    var it = inv.itemList[i];
                    if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f) continue;
                    medIds.Add(it.itemId);
                    if (ItemCategorizer.ClassifyItem(it) == StorageCategory.Medicine)
                        medUnits += Mathf.Max(1, Mathf.FloorToInt(it.itemNumberFloat));
                }
            }
            if (!lifeThreatening && medUnits <= st.ReserveMedMin) return;

            int chosenId = ChooseDebuffRemedyItemId(medIds, CompanionConfigStore.AllowSpendRareMeds, lifeThreatening);
            if (chosenId <= 0) return;
            // Do not waste antipyretics when not feverish, and do not waste unrelated pills on pure fever.
            if (feverish && !irradiated && !hypothermic && chosenId != ItemIdFeverReducer) return;

            for (int invIdx = 0; invIdx < invs.Count; invIdx++)
            {
                var inv = invs[invIdx];
                if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                for (int i = 0; i < inv.itemList.Count; i++)
                {
                    var it = inv.itemList[i];
                    if (!Il2CppSafe.IsAlive(it) || it.itemId != chosenId) continue;
                    ApplyFoodBuffsSafe(agent.humanCharacter, it);
                    try { CharacterActionManager.ConsumeOneFood(cd, inv, it); } catch { }
                    if (irradiated) status.radiationDose = Mathf.Max(0f, status.radiationDose - (chosenId == 277 ? 15f : 4f));
                    if (hypothermic || feverish) status.bodyTemperature = Mathf.Lerp(status.bodyTemperature, stdTemp, 0.5f);
                    return;
                }
            }
        }

        private static ItemData FindBestMaskInInventory(InventoryData inv, string guid)
        {
            ItemData fallback = null;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im)) return null;
            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var it = inv.itemList[i];
                if (!Il2CppSafe.IsAlive(it) || IsProtectedSavedMask(guid, it.Pointer)) continue;
                var attr = im.GetItemAttrById(it.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr) || attr.itemType != ItemType.FaceMask) continue;
                if (IsGasMaskAttr(attr)) return it;
                if (fallback == null) fallback = it;
            }
            return fallback;
        }

        private static bool IsGasMaskItem(ItemData item)
        {
            var attr = ItemManager.instance?.GetItemAttrById(item.itemId);
            return Il2CppSafe.IsAliveUnity(attr) && attr.itemType == ItemType.FaceMask && IsGasMaskAttr(attr);
        }

        private static bool IsGasMaskAttr(ItemAttr attr)
        {
            string n = ((attr.ItemName_EN ?? "") + " " + (attr.itemName ?? "")).ToLowerInvariant();
            return n.Contains("gas") || n.Contains("respirator") || n.Contains("bio") || n.Contains("filter") ||
                   n.Contains("противогаз") || n.Contains("респиратор") || n.Contains("防毒");
        }

        private static TerrainObject_Furniture_Bed FindNearestReachableBed(Vector2 origin, float radius)
        {
            TerrainObject_Furniture_Bed best = null;
            float bestSq = radius * radius;
            var objs = BaseSortingSystem.CollectWorldTerrainObjects(origin, radius);
            for (int i = 0; i < objs.Count; i++)
            {
                var bed = Il2CppSafe.IsAliveUnity(objs[i]) ? objs[i].TryCast<TerrainObject_Furniture_Bed>() : null;
                if (!Il2CppSafe.IsAliveUnity(bed) || !bed.gameObject.activeInHierarchy) continue;
                if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable($"bed_{bed.Pointer.ToInt64():X}")) continue;
                float sq = ((Vector2)bed.transform.position - origin).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = bed; }
            }
            return best;
        }

        private static bool BeginBedSleep(GOAPAgent ag, CompanionRuntimeState st, string guid)
        {
            WalkingToBedGuids.Remove(guid);
            ag.humanCharacter.movementInput = Vector2.zero;
            try { new GOAPAction_Sleep().Execute(ag); } catch { }
            try { ag.humanCharacter.StartSleepingNPC(4.0f); } catch { }
            st.LastChoreTime = Time.unscaledTime;
            st.CurrentTaskText = CompanionConfigStore.L("Спит на вахте", "Sleeping (shift)", "轮班睡眠中");
            return true;
        }

        private static void WakeCompanionImmediate(HumanCharacterController hc, CompanionRuntimeState st, string guid)
        {
            bool wasBedWalk = WalkingToBedGuids.Remove(guid);
            try { hc.ForceWakeupNPC("wake", true); } catch { }
            try { hc.Wakeup(); } catch { }
            if (st != null)
            {
                if (wasBedWalk && st.IsChoreWalkActive) { st.IsChoreWalkActive = false; st.PendingChoreArrivalAction = null; }
                st.CurrentTaskText = "";
            }
        }

        private static int CountAwakeTeammatesExcluding(string excludedGuid)
        {
            int awake = 0;
            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag) continue;
                string g = ag.npcData?.npcGUID ?? "";
                if (!string.Equals(g, excludedGuid, StringComparison.OrdinalIgnoreCase) &&
                    !ag.humanCharacter.isSleeping && !WalkingToBedGuids.Contains(g))
                    awake++;
            }
            return awake;
        }

        private static bool HasOwnMedicineInInventories(List<InventoryData> invs, bool allowRare)
        {
            if (invs == null) return false;
            try
            {
                for (int invIdx = 0; invIdx < invs.Count; invIdx++)
                {
                    var inv = invs[invIdx];
                    if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                    for (int i = 0; i < inv.itemList.Count; i++)
                    {
                        var cand = inv.itemList[i];
                        if (!IsEligibleMedicineItem(cand, out _, out _, out _))
                            continue;
                        if (!allowRare && CompanionProfileSystem.IsRareMedItem(cand.itemId))
                            continue;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private enum CampResupplyKind { Drink = 0, Heal = 1, Food = 2 }

        private static void TryAutonomousEatDrinkAndBandage(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd,
            CharacterStatusData status, CompanionRuntimeState st, string guid, float now, bool inCombat)
        {
            float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
            float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
            float maxHp = status.maxhp > 1f ? status.maxhp : 100f;
            float hpRatio = status.hp / maxHp;
            HasTreatableInjuryState(cd, out bool isBleeding, out bool hasUnbandagedWound);

            bool needsHealOrBandage = hpRatio < 0.78f || isBleeding || hasUnbandagedWound;
            bool allowCombatEmergencyHeal = inCombat &&
                                            (isBleeding || hasUnbandagedWound || hpRatio < 0.65f) &&
                                            !IsAnyCloseCombatDanger(agent, 4.5f);

            if (inCombat && !allowCombatEmergencyHeal)
                return;

            bool needsAnyDirect = inCombat
                ? allowCombatEmergencyHeal
                : ((status.thirst / maxT) < 0.45f || (status.hunger / maxH) < 0.45f || needsHealOrBandage);

            if (needsAnyDirect && (!LastSelfCareAtByGuid.TryGetValue(guid, out float lastSelfAt) || now - lastSelfAt >= 4.5f))
            {
                LastSelfCareAtByGuid[guid] = now;
                var invs = CollectCompanionInventories(cd, agent.npcData);

                if (!inCombat)
                {
                    // 1) Thirst: native drinkable containers first, then thirst-restoring consumables.
                    if ((status.thirst / maxT) < 0.45f)
                    {
                        TryDrinkFromOwnContainers(agent, hc, status);
                        if ((status.thirst / maxT) < 0.45f &&
                            TryFindConsumableFoodInInventories(invs, forThirst: true, CompanionConfigStore.AllowSpendRareFood,
                                out var drinkInv, out var drinkItem, out var drinkAttr))
                        {
                            ConsumeFoodItemSafe(agent, cd, status, drinkInv, drinkItem, drinkAttr);
                        }
                    }

                    // 2) Hunger: personal inventory first, then shared inventory / backpack.
                    if ((status.hunger / maxH) < 0.45f &&
                        TryFindConsumableFoodInInventories(invs, forThirst: false, CompanionConfigStore.AllowSpendRareFood,
                            out var foodInv, out var foodItem, out var foodAttr))
                    {
                        ConsumeFoodItemSafe(agent, cd, status, foodInv, foodItem, foodAttr);
                    }
                }

                // 3) Bandaging / Self-Heal (including bleeding/injuries >= 78% HP and safe combat cover healing)
                HasTreatableInjuryState(cd, out isBleeding, out hasUnbandagedWound);
                hpRatio = status.hp / maxHp;
                if (hpRatio < 0.78f || isBleeding || hasUnbandagedWound)
                {
                    TrySelfHealFromInventories(agent, hc, cd, status, invs, maxHp, isBleeding, hasUnbandagedWound);
                }
            }

            if (inCombat || BaseWorkManager.Paused || hc.isSleeping || st.HasAnyPhysicalTask ||
                !IsAtCampOrPlayerBase(hc.transform.position, st))
                return;

            bool isGarrison = st.RosterMode == CompanionRosterMode.CampGarrison;
            if (!isGarrison && (st.ActiveOrder != TacticalOrderKind.Follow || !st.AllowBaseChores))
                return;

            if (LastCampResupplyAtByGuid.TryGetValue(guid, out float lastResupply) && now - lastResupply < 12f)
                return;

            HasTreatableInjuryState(cd, out isBleeding, out hasUnbandagedWound);
            float thirstRatio = status.thirst / maxT;
            float hungerRatio = status.hunger / maxH;
            hpRatio = status.hp / maxHp;

            bool allowRoutineCampResupply = CompanionConfigStore.EnableBaseChores && st.AllowBaseChores;
            bool stillNeedsThirst = allowRoutineCampResupply ? (thirstRatio < 0.45f) : (thirstRatio < 0.25f);
            bool stillNeedsHunger = allowRoutineCampResupply ? (hungerRatio < 0.45f) : (hungerRatio < 0.25f);
            bool stillNeedsHeal = allowRoutineCampResupply
                ? (hpRatio < 0.72f || isBleeding || hasUnbandagedWound)
                : (hpRatio < 0.45f || isBleeding);

            if (!stillNeedsThirst && !stillNeedsHunger && !stillNeedsHeal)
                return;

            var ownInvs = CollectCompanionInventories(cd, agent.npcData);
            if (stillNeedsThirst && HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: true))
                stillNeedsThirst = false;
            if (stillNeedsHunger && HasOwnSupplyForNeed(agent, cd, agent.npcData, forThirst: false))
                stillNeedsHunger = false;
            if (stillNeedsHeal)
            {
                bool lowBlood = CharacterStatusData.maxBloodVolume > 1f && status.bloodVolume < CharacterStatusData.maxBloodVolume * 0.65f;
                bool allowRareMed = CompanionConfigStore.AllowSpendRareMeds && (hpRatio < 0.38f || (isBleeding && lowBlood));
                if (HasOwnMedicineInInventories(ownInvs, allowRareMed))
                    stillNeedsHeal = false;
            }

            if (!stillNeedsThirst && !stillNeedsHunger && !stillNeedsHeal)
                return;

            TryDispatchCampResupplyWalk(agent, hc, cd, status, st, guid, now,
                stillNeedsThirst, stillNeedsHunger, stillNeedsHeal, isBleeding || hasUnbandagedWound, maxT, maxHp);
        }

        internal static List<InventoryData> CollectCompanionInventories(CharacterData cd, GOAPNPCData npcData)
        {
            var invs = new List<InventoryData>(6);
            var seen = new HashSet<IntPtr>();
            AddInventoryRecursive(cd?.inventoryData, 0, 3, invs, seen);
            AddInventoryRecursive(npcData?.sharedInventoryData, 0, 3, invs, seen);
            AddInventoryRecursive(cd?.characterEquipmentData?.backpackItemData?.inventoryData, 0, 3, invs, seen);
            return invs;
        }

        private static void AddInventoryRecursive(
            InventoryData inv, int depth, int maxDepth,
            List<InventoryData> dest, HashSet<IntPtr> seen)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null || !seen.Add(inv.Pointer))
                return;
            dest.Add(inv);
            if (depth >= maxDepth) return;
            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var it = inv.itemList[i];
                if (!Il2CppSafe.IsAlive(it)) continue;
                var nested = it.inventoryData;
                if (Il2CppSafe.IsAlive(nested))
                    AddInventoryRecursive(nested, depth + 1, maxDepth, dest, seen);
            }
        }

        private static bool TryDrinkFromOwnContainers(GOAPAgent agent, HumanCharacterController hc, CharacterStatusData status)
        {
            try
            {
                var drinks = TryGetAgentDrinkingItems(agent);
                if (drinks == null || drinks.Count == 0) return false;
                float vol = ResolveDefaultDrinkVolume();
                for (int i = 0; i < drinks.Count; i++)
                {
                    var d = drinks[i];
                    if (!Il2CppSafe.IsAlive(d)) continue;
                    float beforeT = status.thirst;
                    TryDrinkLiquidFromContainer(hc, d, vol);
                    if (Il2CppSafe.IsAlive(agent.npcData))
                        agent.npcData.thirst = status.thirst;
                    if (status.thirst > beforeT + 0.01f)
                        return true;
                }
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Il2CppSystem.Collections.Generic.List<ItemData> TryGetAgentDrinkingItems(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return null;
            try
            {
                return GetAgentDrinkingItemsNoInline(agent);
            }
            catch
            {
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Il2CppSystem.Collections.Generic.List<ItemData> GetAgentDrinkingItemsNoInline(GOAPAgent agent)
        {
            return agent.GetDrinkingItems();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Il2CppSystem.Collections.Generic.List<ItemData> TryGetNpcDataDrinkingItems(GOAPNPCData npcData)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return null;
            try
            {
                return GetNpcDataDrinkingItemsNoInline(npcData);
            }
            catch
            {
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Il2CppSystem.Collections.Generic.List<ItemData> GetNpcDataDrinkingItemsNoInline(GOAPNPCData npcData)
        {
            return npcData.GetDrinkingItems();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float ResolveDefaultDrinkVolume()
        {
            try
            {
                float vol = GetDefaultDrinkVolumeNoInline();
                return vol > 10f ? vol : 250f;
            }
            catch
            {
                return 250f;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetDefaultDrinkVolumeNoInline()
        {
            return HumanCharacterController.defaultDrinkVolume;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryDrinkLiquidFromContainer(HumanCharacterController hc, ItemData containerItem, float volume)
        {
            if (!Il2CppSafe.IsAliveUnity(hc) || !Il2CppSafe.IsAlive(containerItem)) return false;
            try
            {
                DrinkLiquidFromContainerNoInline(hc, containerItem, volume);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DrinkLiquidFromContainerNoInline(HumanCharacterController hc, ItemData containerItem, float volume)
        {
            hc.DrinkLiquidFromContainer(containerItem, volume);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryAppendDrinkingContainers(
            Il2CppSystem.Collections.Generic.List<ItemData> candidates,
            Il2CppSystem.Collections.Generic.List<ItemData> validDrinks)
        {
            if (candidates == null || validDrinks == null) return false;
            try
            {
                AppendDrinkingContainersNoInline(candidates, validDrinks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AppendDrinkingContainersNoInline(
            Il2CppSystem.Collections.Generic.List<ItemData> candidates,
            Il2CppSystem.Collections.Generic.List<ItemData> validDrinks)
        {
            GOAPNPCData.AppendDrinkingContainers(candidates, validDrinks);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryOfflineUseItem(GOAPNPCData npcData, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(npcData) || !Il2CppSafe.IsAlive(item)) return false;
            try
            {
                OfflineUseItemNoInline(npcData, item);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OfflineUseItemNoInline(GOAPNPCData npcData, ItemData item)
        {
            var mgr = GOAPNPCManager.instance;
            if (Il2CppSafe.IsAlive(mgr))
                mgr.OfflineUseItem(npcData, item);
        }

        private static bool IsEligibleConsumableFood(
            ItemData item, bool forThirst, bool allowRare, float minStatGain, out ItemAttr_Food foodAttr, out bool isRare)
        {
            foodAttr = null;
            isRare = false;
            if (!Il2CppSafe.IsAlive(item) || item.itemNumberFloat < 1f) return false;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im)) return false;

            var attr = im.GetItemAttrById(item.itemId);
            if (!Il2CppSafe.IsAliveUnity(attr) || ItemData.IsFoodExpired(item, attr)) return false;
            foodAttr = attr.TryCast<ItemAttr_Food>();
            if (!Il2CppSafe.IsAliveUnity(foodAttr) || foodAttr.badStomach) return false;

            float gain = forThirst ? foodAttr.thirst : foodAttr.hunger;
            if (gain <= minStatGain) return false;

            var kind = ItemCategorizer.ClassifyFoodKind(item.itemId, attr, item);
            if (kind == FoodKind.RawMeat || kind == FoodKind.RottenFood ||
                kind == FoodKind.MedicineBandage || kind == FoodKind.MedicinePills)
                return false;

            isRare = CompanionProfileSystem.IsRareFoodItem(item.itemId, kind);
            return allowRare || !isRare;
        }

        private static bool TryFindConsumableFoodInInventories(
            List<InventoryData> invs, bool forThirst, bool allowRare,
            out InventoryData srcInv, out ItemData chosenItem, out ItemAttr_Food chosenAttr)
        {
            srcInv = null;
            chosenItem = null;
            chosenAttr = null;
            int passes = allowRare ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int invIdx = 0; invIdx < invs.Count; invIdx++)
                {
                    var inv = invs[invIdx];
                    if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                    for (int i = 0; i < inv.itemList.Count; i++)
                    {
                        var it = inv.itemList[i];
                        if (!IsEligibleConsumableFood(it, forThirst, allowRare, 5f, out var fa, out bool isRare))
                            continue;
                        if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                        srcInv = inv;
                        chosenItem = it;
                        chosenAttr = fa;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool ConsumeFoodItemSafe(
            GOAPAgent agent, CharacterData cd, CharacterStatusData status,
            InventoryData srcInv, ItemData item, ItemAttr_Food foodAttr)
        {
            if (!Il2CppSafe.IsAlive(cd) || !Il2CppSafe.IsAlive(status) ||
                !Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(item))
                return false;

            float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
            float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
            float beforeH = status.hunger;
            float beforeT = status.thirst;

            bool consumed = false;
            try { consumed = CharacterActionManager.ConsumeOneFood(cd, srcInv, item); } catch { }
            if (!consumed) return false;

            if (Il2CppSafe.IsAliveUnity(foodAttr) &&
                status.hunger <= beforeH + 0.01f && status.thirst <= beforeT + 0.01f)
            {
                if (foodAttr.hunger > 0f)
                    status.hunger = Mathf.Min(maxH, status.hunger + foodAttr.hunger);
                if (foodAttr.thirst > 0f)
                    status.thirst = Mathf.Min(maxT, status.thirst + foodAttr.thirst);
            }

            if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData))
            {
                agent.npcData.hunger = status.hunger;
                agent.npcData.thirst = status.thirst;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsBandageItem(int itemId)
        {
            if (itemId <= 0) return false;
            try { return IsBandageItemNoInline(itemId); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsBandageItemNoInline(int itemId) => AidTreatUtil.IsBandageItem(itemId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsMedicalConsumable(int itemId)
        {
            if (itemId <= 0) return false;
            try { return IsMedicalConsumableNoInline(itemId); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsMedicalConsumableNoInline(int itemId) => AidTreatUtil.IsMedicalConsumable(itemId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static CharacterBodyPartData TryFindBandageTargetPart(CharacterData cd)
        {
            if (!Il2CppSafe.IsAlive(cd)) return null;
            try { return FindBandageTargetPartNoInline(cd); }
            catch { return null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static CharacterBodyPartData FindBandageTargetPartNoInline(CharacterData cd) => AidTreatUtil.FindBandageTargetPart(cd);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryApplyBandage(HumanCharacterController hc, CharacterBodyPartData part, ItemData bandageItem)
        {
            if (!Il2CppSafe.IsAliveUnity(hc) || !Il2CppSafe.IsAlive(part) || !Il2CppSafe.IsAlive(bandageItem)) return;
            try { ApplyBandageNoInline(hc, part, bandageItem); }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ApplyBandageNoInline(HumanCharacterController hc, CharacterBodyPartData part, ItemData bandageItem) =>
            hc.UseBandage(part, bandageItem);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryStopAllBleeding(HumanCharacterController hc)
        {
            if (!Il2CppSafe.IsAliveUnity(hc)) return;
            try { StopAllBleedingNoInline(hc); }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StopAllBleedingNoInline(HumanCharacterController hc) => hc.StopAllBleeding();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryUpdateVitalStatesSafe(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent)) return;
            try { UpdateVitalStatesNoInline(agent); }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateVitalStatesNoInline(GOAPAgent agent) => agent.UpdateVitalStates();

        private static bool IsEligibleMedicineItem(
            ItemData item, out ItemAttr_Food medAttr, out bool isBandage, out bool healsHp)
        {
            medAttr = null;
            isBandage = false;
            healsHp = false;
            if (!Il2CppSafe.IsAlive(item) || item.itemNumberFloat < 1f) return false;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im)) return false;

            var attr = im.GetItemAttrById(item.itemId);
            if (!Il2CppSafe.IsAliveUnity(attr) || ItemData.IsFoodExpired(item, attr)) return false;
            medAttr = attr.TryCast<ItemAttr_Food>();
            if (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.badStomach) return false;

            var cat = ItemCategorizer.ClassifyItem(item);
            var foodKind = ItemCategorizer.ClassifyFoodKind(item.itemId, attr, item);
            bool bandageUtil = TryIsBandageItem(item.itemId);
            bool medUtil = TryIsMedicalConsumable(item.itemId);

            bool hasFoodOrDrinkStats = Il2CppSafe.IsAliveUnity(medAttr) && (medAttr.hunger > 1f || medAttr.thirst > 1f);
            bool isExplicitMedical = cat == StorageCategory.Medicine || bandageUtil || medUtil ||
                                     foodKind == FoodKind.MedicineBandage || foodKind == FoodKind.MedicinePills;
            bool hasAttrSpecialHeal = Il2CppSafe.IsAliveUnity(medAttr) &&
                                      (medAttr.stopBleeding || medAttr.healAllInjuries || medAttr.bloodVolumeRecover > 0f);
            if (hasFoodOrDrinkStats && !isExplicitMedical && !hasAttrSpecialHeal)
                return false;

            isBandage = bandageUtil || foodKind == FoodKind.MedicineBandage ||
                        (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.stopBleeding);
            healsHp = Il2CppSafe.IsAliveUnity(medAttr) && medAttr.hp > 0f;
            return isExplicitMedical || hasAttrSpecialHeal || healsHp;
        }

        private static void ApplyConsumedMedicineEffects(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd, CharacterStatusData status,
            ItemData medSnapshot, ItemAttr_Food medAttr, float maxHp, float beforeHp)
        {
            bool isBandage = Il2CppSafe.IsAlive(medSnapshot) &&
                             (TryIsBandageItem(medSnapshot.itemId) ||
                              ItemCategorizer.ClassifyFoodKind(medSnapshot) == FoodKind.MedicineBandage ||
                              (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.stopBleeding));

            if (isBandage && Il2CppSafe.IsAlive(medSnapshot))
            {
                var targetPart = TryFindBandageTargetPart(cd);
                if (Il2CppSafe.IsAlive(targetPart))
                    TryApplyBandage(hc, targetPart, medSnapshot);
            }

            if ((Il2CppSafe.IsAliveUnity(medAttr) && medAttr.stopBleeding) ||
                (isBandage && HasTreatableInjuryState(cd, out bool stillBleeding, out _) && stillBleeding))
            {
                TryStopAllBleeding(hc);
            }

            if (status.hp <= beforeHp + 0.1f)
            {
                float healAmt;
                if (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.hp > 0f)
                    healAmt = Mathf.Max(22f, medAttr.hp);
                else if (!isBandage)
                    healAmt = 25f;
                else
                    healAmt = beforeHp < maxHp * 0.85f ? 12f : 0f;

                if (healAmt > 0f)
                    status.hp = Mathf.Min(maxHp, status.hp + healAmt);
            }

            if (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.bloodVolumeRecover > 0f && CharacterStatusData.maxBloodVolume > 0f)
                status.bloodVolume = Mathf.Min(CharacterStatusData.maxBloodVolume, status.bloodVolume + medAttr.bloodVolumeRecover);

            if (!isBandage || (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.healAllInjuries) || (status.hp / maxHp) < 0.50f)
            {
                try { hc.HealAllBodyPartInjuries(); } catch { }
            }

            hc.hp = status.hp;
            if (Il2CppSafe.IsAlive(agent?.npcData))
                agent.npcData.health = status.hp;
            TryUpdateVitalStatesSafe(agent);
        }

        private static bool TrySelfHealFromInventories(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd,
            CharacterStatusData status, List<InventoryData> invs, float maxHp,
            bool isBleeding, bool hasUnbandagedWound)
        {
            float hpRatio = status.hp / maxHp;
            bool lowBlood = CharacterStatusData.maxBloodVolume > 1f && status.bloodVolume < CharacterStatusData.maxBloodVolume * 0.65f;
            bool allowRare = CompanionConfigStore.AllowSpendRareMeds && (hpRatio < 0.38f || (isBleeding && lowBlood));
            bool preferBandage = (isBleeding || hasUnbandagedWound) && hpRatio >= 0.60f;

            ItemData chosenMed = null;
            InventoryData medInv = null;
            ItemAttr_Food chosenAttr = null;

            int totalPasses = allowRare ? 4 : 2;
            for (int pass = 0; pass < totalPasses && !Il2CppSafe.IsAlive(chosenMed); pass++)
            {
                bool passRare = pass >= 2;
                bool passStrictPref = (pass % 2) == 0;
                for (int invIdx = 0; invIdx < invs.Count && !Il2CppSafe.IsAlive(chosenMed); invIdx++)
                {
                    var inv = invs[invIdx];
                    if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                    for (int i = 0; i < inv.itemList.Count; i++)
                    {
                        var cand = inv.itemList[i];
                        if (!IsEligibleMedicineItem(cand, out var candAttr, out bool isBandage, out bool healsHp))
                            continue;
                        bool isRare = CompanionProfileSystem.IsRareMedItem(cand.itemId);
                        if (isRare != passRare) continue;
                        if (passStrictPref)
                        {
                            if (preferBandage ? !isBandage : !healsHp) continue;
                        }
                        chosenMed = cand;
                        medInv = inv;
                        chosenAttr = candAttr;
                        break;
                    }
                }
            }

            if (!Il2CppSafe.IsAlive(chosenMed) || !Il2CppSafe.IsAlive(medInv)) return false;

            ItemData medSnapshot = null;
            try { medSnapshot = chosenMed.CopyItemData(null); } catch { }
            float beforeHp = status.hp;
            bool consumed = false;
            try { consumed = CharacterActionManager.ConsumeOneFood(cd, medInv, chosenMed); } catch { }
            if (!consumed) return false;

            ApplyConsumedMedicineEffects(agent, hc, cd, status, medSnapshot ?? chosenMed, chosenAttr, maxHp, beforeHp);
            return true;
        }

        private static bool TryFindChestResupplyItem(
            List<DiscoveredContainer> chests, Vector2 origin, sbyte zLevel, float maxRadius, bool isGarrison,
            CampResupplyKind kind, float hpRatio, bool preferBandage,
            out DiscoveredContainer matchedChest, out InventoryData matchedSourceInv, out ItemData matchedItem, out bool isDrinkContainer)
        {
            matchedChest = null;
            matchedSourceInv = null;
            matchedItem = null;
            isDrinkContainer = false;
            if (chests == null || chests.Count == 0) return false;

            bool allowRare = kind == CampResupplyKind.Heal
                ? (CompanionConfigStore.AllowSpendRareMeds && hpRatio < 0.38f)
                : CompanionConfigStore.AllowSpendRareFood;
            int passes = allowRare ? 2 : 1;
            float bestScore = float.MaxValue;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < chests.Count; i++)
                {
                    var c = chests[i];
                    if (c == null || !Il2CppSafe.IsAliveUnity(c.TerrainObj) || !Il2CppSafe.IsAlive(c.Inventory) ||
                        c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                        continue;
                    bool isMarked = c.Rule != null && c.Rule.IsMarkedByPlayer;
                    if (!isMarked && !(isGarrison && AutoLootSystem.IsProtectedPlayerBaseContainer(c.TerrainObj, c.Inventory)))
                        continue;
                    if (Il2CppSafe.IsAlive(c.TerrainObj.objectData) && c.TerrainObj.objectData.zLayer != zLevel)
                        continue;
                    if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                        continue;

                    float d = Vector2.Distance(origin, c.WorldPos);
                    if (d > maxRadius) continue;
                    float score = isMarked ? d : (d + 4.0f);
                    if (score >= bestScore) continue;

                    var chestInvs = new List<InventoryData>(4);
                    var chestSeen = new HashSet<IntPtr>();
                    AddInventoryRecursive(c.Inventory, 0, 2, chestInvs, chestSeen);

                    InventoryData foundSrc = null;
                    ItemData found = null;
                    bool foundContainer = false;

                    if (kind == CampResupplyKind.Drink && pass == 0)
                    {
                        for (int invIdx = 0; invIdx < chestInvs.Count && !Il2CppSafe.IsAlive(found); invIdx++)
                        {
                            var subInv = chestInvs[invIdx];
                            var contCand = new Il2CppSystem.Collections.Generic.List<ItemData>();
                            for (int j = 0; j < subInv.itemList.Count; j++)
                            {
                                var it = subInv.itemList[j];
                                if (!Il2CppSafe.IsAlive(it)) continue;
                                var attr = ItemManager.instance?.GetItemAttrById(it.itemId);
                                if (Il2CppSafe.IsAliveUnity(attr) && attr.TryCast<ItemAttr_Container>() != null)
                                    contCand.Add(it);
                            }
                            if (contCand.Count > 0)
                            {
                                var validDrinks = new Il2CppSystem.Collections.Generic.List<ItemData>();
                                if (TryAppendDrinkingContainers(contCand, validDrinks) &&
                                    validDrinks.Count > 0 && Il2CppSafe.IsAlive(validDrinks[0]))
                                {
                                    foundSrc = subInv;
                                    found = validDrinks[0];
                                    foundContainer = true;
                                }
                            }
                        }
                    }

                    if (!Il2CppSafe.IsAlive(found))
                    {
                        ItemData fallbackHealItem = null;
                        InventoryData fallbackHealSrc = null;
                        for (int invIdx = 0; invIdx < chestInvs.Count && !Il2CppSafe.IsAlive(found); invIdx++)
                        {
                            var subInv = chestInvs[invIdx];
                            for (int j = 0; j < subInv.itemList.Count; j++)
                            {
                                var it = subInv.itemList[j];
                                if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f) continue;

                                if (kind == CampResupplyKind.Heal)
                                {
                                    if (!IsEligibleMedicineItem(it, out _, out bool isBandage, out bool healsHp)) continue;
                                    bool isRare = CompanionProfileSystem.IsRareMedItem(it.itemId);
                                    if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                                    if (preferBandage ? isBandage : healsHp)
                                    {
                                        foundSrc = subInv;
                                        found = it;
                                        break;
                                    }
                                    if (fallbackHealItem == null)
                                    {
                                        fallbackHealSrc = subInv;
                                        fallbackHealItem = it;
                                    }
                                }
                                else
                                {
                                    if (!IsEligibleConsumableFood(it, forThirst: kind == CampResupplyKind.Drink, allowRare, 8f, out _, out bool isRare))
                                        continue;
                                    if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                                    foundSrc = subInv;
                                    found = it;
                                    break;
                                }
                            }
                        }
                        if (!Il2CppSafe.IsAlive(found) && Il2CppSafe.IsAlive(fallbackHealItem))
                        {
                            foundSrc = fallbackHealSrc;
                            found = fallbackHealItem;
                        }
                    }

                    if (Il2CppSafe.IsAlive(found) && Il2CppSafe.IsAlive(foundSrc))
                    {
                        bestScore = score;
                        matchedChest = c;
                        matchedSourceInv = foundSrc;
                        matchedItem = found;
                        isDrinkContainer = foundContainer;
                    }
                }
                if (matchedChest != null) return true;
            }
            return false;
        }

        private static TerrainObject_WaterWell FindNearestReachableWaterWell(Vector2 origin, sbyte zLevel, float radius)
        {
            TerrainObject_WaterWell best = null;
            float bestSq = radius * radius;
            BaseSortingSystem.InvalidateTerrainScanCache();
            var objs = BaseSortingSystem.CollectWorldTerrainObjects(origin, radius);
            for (int i = 0; i < objs.Count; i++)
            {
                var well = Il2CppSafe.IsAliveUnity(objs[i]) ? objs[i].TryCast<TerrainObject_WaterWell>() : null;
                if (!Il2CppSafe.IsAliveUnity(well) || !well.gameObject.activeInHierarchy) continue;
                if (Il2CppSafe.IsAlive(well.objectData) && well.objectData.zLayer != zLevel) continue;
                if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable($"well_{well.Pointer.ToInt64():X}")) continue;
                float sq = ((Vector2)well.transform.position - origin).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = well; }
            }
            return best;
        }

        private static bool TryTakeOneUnitFromChest(
            InventoryData chestInv, InventoryData destInv, ItemData sourceItem, bool wholeContainer, out ItemData destRef)
        {
            destRef = null;
            if (!Il2CppSafe.IsAlive(chestInv) || !Il2CppSafe.IsAlive(destInv) || !Il2CppSafe.IsAlive(sourceItem))
                return false;
            if (!InventoryTransfers.Contains(chestInv, sourceItem))
                return false;

            int itemId = sourceItem.itemId;
            if (wholeContainer || sourceItem.itemNumberFloat <= 1.001f)
            {
                if (!InventoryTransfers.TryMoveWhole(chestInv, destInv, sourceItem))
                    return false;
                destRef = InventoryTransfers.Contains(destInv, sourceItem)
                    ? sourceItem
                    : FindLastMatchingItem(destInv, itemId);
                return Il2CppSafe.IsAlive(destRef);
            }

            var single = sourceItem.CopyItemData(null);
            if (!Il2CppSafe.IsAlive(single)) return false;
            single.itemNumberFloat = 1f;
            if (!InventoryTransfers.TryDepositOrStackWhole(destInv, single))
                return false;

            sourceItem.itemNumberFloat -= 1f;
            if (sourceItem.itemNumberFloat <= 0.001f)
                chestInv.RemoveItem(sourceItem, true);
            else
                try { chestInv.NotifyDataChanged(true, sourceItem, false); } catch { }

            destRef = InventoryTransfers.Contains(destInv, single)
                ? single
                : FindLastMatchingItem(destInv, itemId);
            return Il2CppSafe.IsAlive(destRef);
        }

        private static ItemData FindLastMatchingItem(InventoryData inv, int itemId)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return null;
            for (int i = inv.itemList.Count - 1; i >= 0; i--)
            {
                var it = inv.itemList[i];
                if (Il2CppSafe.IsAlive(it) && it.itemId == itemId && it.itemNumberFloat >= 1f)
                    return it;
            }
            return null;
        }

        private static void TryDispatchCampResupplyWalk(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd, CharacterStatusData status,
            CompanionRuntimeState st, string guid, float now,
            bool needThirst, bool needHunger, bool needHeal, bool preferBandage, float maxT, float maxHp)
        {
            if (!Il2CppSafe.IsAlive(cd.inventoryData))
                try { cd.RefreshInventorySize(); } catch { }
            InventoryData destInv = Il2CppSafe.IsAlive(cd.inventoryData)
                ? cd.inventoryData
                : agent.npcData.GetOrCreateSharedInventory();
            if (!Il2CppSafe.IsAlive(destInv)) return;

            Vector2 selfPos = hc.transform.position;
            bool isGarrison = st.RosterMode == CompanionRosterMode.CampGarrison;
            Vector2 resupplyOrigin = (isGarrison && st.HasHomeAnchor) ? st.HomeAnchorPos : selfPos;
            float maxResupplyRadius = isGarrison ? 28f : 36f;
            sbyte zLevel = agent.npcData.zLevel;
            float hpRatio = status.hp / maxHp;
            var chests = BaseSortingSystem.FilterContainersForWorker(BaseSortingSystem.ScanNearbyContainers(force: false), agent, st);

            if (needThirst)
            {
                if (TryFindChestResupplyItem(chests, resupplyOrigin, zLevel, maxResupplyRadius, isGarrison,
                    CampResupplyKind.Drink, hpRatio, preferBandage,
                    out var drinkChest, out var drinkSrcInv, out var drinkItem, out bool isCont))
                {
                    StartChestResupplyWalk(agent, st, guid, now, drinkChest, drinkSrcInv, drinkItem, CampResupplyKind.Drink, isCont);
                    return;
                }

                var well = FindNearestReachableWaterWell(resupplyOrigin, zLevel, 28f);
                if (Il2CppSafe.IsAliveUnity(well))
                {
                    LastCampResupplyAtByGuid[guid] = now;
                    Vector2 wellPos = well.transform.position;
                    BaseChoreSystem.StartPhysicalChoreWalk(
                        agent, st, wellPos, well.transform,
                        CompanionConfigStore.L("💧 Идёт к колодцу попить...", "💧 Walking to water well...", "💧 前往水井喝水..."),
                        0,
                        () =>
                        {
                            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag ||
                                !Il2CppSafe.IsAliveUnity(well)) return;
                            var stData = agent.npcData.characterData?.characterStatusData;
                            if (!Il2CppSafe.IsAlive(stData)) return;
                            if (PhysicalMovementHelper.ComputeSurfaceDistance(
                                    agent.humanCharacter.transform.position, well.transform.position, well.transform) > BedReachRadius)
                                return;

                            float curMaxT = stData.maxThirst > 1f ? stData.maxThirst : 100f;
                            stData.thirst = Mathf.Min(curMaxT, stData.thirst + 60f);
                            agent.npcData.thirst = stData.thirst;
                            st.LastChoreTime = Time.unscaledTime;
                            st.CurrentTaskText = CompanionConfigStore.L(
                                "💧 Попил воды из колодца", "💧 Drank from water well", "💧 在水井喝了水");
                        },
                        $"well_{well.Pointer.ToInt64():X}");
                    return;
                }
            }

            if (needHeal && TryFindChestResupplyItem(chests, resupplyOrigin, zLevel, maxResupplyRadius, isGarrison,
                CampResupplyKind.Heal, hpRatio, preferBandage,
                out var healChest, out var healSrcInv, out var healItem, out _))
            {
                StartChestResupplyWalk(agent, st, guid, now, healChest, healSrcInv, healItem, CampResupplyKind.Heal, false);
                return;
            }

            if (needHunger && TryFindChestResupplyItem(chests, resupplyOrigin, zLevel, maxResupplyRadius, isGarrison,
                CampResupplyKind.Food, hpRatio, preferBandage,
                out var foodChest, out var foodSrcInv, out var foodItem, out _))
            {
                StartChestResupplyWalk(agent, st, guid, now, foodChest, foodSrcInv, foodItem, CampResupplyKind.Food, false);
            }
        }

        private static void StartChestResupplyWalk(
            GOAPAgent agent, CompanionRuntimeState st, string guid, float now,
            DiscoveredContainer chest, InventoryData sourceInv, ItemData item, CampResupplyKind kind, bool isDrinkContainer)
        {
            if (chest == null || !Il2CppSafe.IsAliveUnity(chest.TerrainObj) || !Il2CppSafe.IsAlive(sourceInv) || !Il2CppSafe.IsAlive(item))
                return;

            LastCampResupplyAtByGuid[guid] = now;
            string walkLabel = kind == CampResupplyKind.Drink
                ? CompanionConfigStore.L("💧 Идёт к ящику за водой...", "💧 Walking to chest for a drink...", "💧 前往储物箱取水...")
                : kind == CampResupplyKind.Heal
                    ? CompanionConfigStore.L("🩹 Идёт к ящику за аптечкой...", "🩹 Walking to chest for medicine...", "🩹 前往储物箱拿取医疗物资...")
                    : CompanionConfigStore.L("🍖 Идёт к ящику за едой...", "🍖 Walking to chest for food...", "🍖 前往储物箱拿取食物...");

            BaseChoreSystem.StartPhysicalChoreWalk(
                agent, st, chest.WorldPos, chest.TerrainObj.transform, walkLabel, 0,
                () =>
                {
                    if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                        !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag ||
                        !Il2CppSafe.IsAliveUnity(chest.TerrainObj) || !Il2CppSafe.IsAlive(sourceInv))
                        return;

                    var curHc = agent.humanCharacter;
                    var curCd = agent.npcData.characterData;
                    var curStatus = curCd?.characterStatusData;
                    if (!Il2CppSafe.IsAlive(curCd) || !Il2CppSafe.IsAlive(curStatus)) return;
                    if (PhysicalMovementHelper.ComputeSurfaceDistance(
                            curHc.transform.position, chest.TerrainObj.transform.position, chest.TerrainObj.transform) > BedReachRadius)
                        return;

                    if (!Il2CppSafe.IsAlive(curCd.inventoryData))
                        try { curCd.RefreshInventorySize(); } catch { }
                    InventoryData curDestInv = Il2CppSafe.IsAlive(curCd.inventoryData)
                        ? curCd.inventoryData
                        : agent.npcData.GetOrCreateSharedInventory();
                    if (!Il2CppSafe.IsAlive(curDestInv)) return;

                    if (!TryTakeOneUnitFromChest(sourceInv, curDestInv, item, isDrinkContainer, out var taken))
                        return;

                    if (isDrinkContainer)
                    {
                        float vol = ResolveDefaultDrinkVolume();
                        TryDrinkLiquidFromContainer(curHc, taken, vol);
                        agent.npcData.thirst = curStatus.thirst;
                    }
                    else if (kind == CampResupplyKind.Heal)
                    {
                        float curMaxHp = curStatus.maxhp > 1f ? curStatus.maxhp : 100f;
                        float beforeHp = curStatus.hp;
                        var medAttr = ItemManager.instance?.GetItemAttrById(taken.itemId)?.TryCast<ItemAttr_Food>();
                        ItemData medSnap = null;
                        try { medSnap = taken.CopyItemData(null); } catch { }
                        bool consumed = false;
                        try { consumed = CharacterActionManager.ConsumeOneFood(curCd, curDestInv, taken); } catch { }
                        if (consumed)
                        {
                            ApplyConsumedMedicineEffects(agent, curHc, curCd, curStatus, medSnap ?? taken, medAttr, curMaxHp, beforeHp);
                        }
                    }
                    else
                    {
                        var foodAttr = ItemManager.instance?.GetItemAttrById(taken.itemId)?.TryCast<ItemAttr_Food>();
                        ConsumeFoodItemSafe(agent, curCd, curStatus, curDestInv, taken, foodAttr);
                    }

                    st.LastChoreTime = Time.unscaledTime;
                    st.CurrentTaskText = kind == CampResupplyKind.Drink
                        ? CompanionConfigStore.L("💧 Попил из запасов базы", "💧 Drank from base supplies", "💧 饮用了基地储备饮水")
                        : kind == CampResupplyKind.Heal
                            ? CompanionConfigStore.L("🩹 Перевязался со склада базы", "🩹 Bandaged from base supplies", "🩹 使用基地医疗物资进行了包扎")
                            : CompanionConfigStore.L("🍖 Перекусил из запасов базы", "🍖 Ate from base supplies", "🍖 食用了基地储备食物");
                },
                chest.ContainerId);
        }

        private static void ConsumeOneOfflineSupply(GOAPNPCData npcData, InventoryData inv, ItemData item, ItemAttr_Food attr)
        {
            if (!Il2CppSafe.IsAlive(npcData) || !Il2CppSafe.IsAlive(inv) || !Il2CppSafe.IsAlive(item))
                return;

            float beforeQty = item.itemNumberFloat;
            float beforeH = npcData.hunger;
            float beforeT = npcData.thirst;
            TryOfflineUseItem(npcData, item);

            if (Il2CppSafe.IsAlive(item) && item.itemNumberFloat >= beforeQty - 0.01f &&
                InventoryTransfers.Contains(inv, item))
            {
                item.itemNumberFloat -= 1f;
                if (item.itemNumberFloat <= 0.001f)
                    inv.RemoveItem(item, false);
                else
                    try { inv.NotifyDataChanged(false, item, false); } catch { }
            }

            if (Il2CppSafe.IsAliveUnity(attr) &&
                npcData.hunger <= beforeH + 0.01f && npcData.thirst <= beforeT + 0.01f)
            {
                if (attr.hunger > 0f) npcData.hunger = Mathf.Min(100f, npcData.hunger + attr.hunger);
                if (attr.thirst > 0f) npcData.thirst = Mathf.Min(100f, npcData.thirst + attr.thirst);
            }
        }

        /// <summary>
        /// Called from GOAPNPCManager.UpdateOfflineNPC(GOAPNPCData) Postfix:
        /// keeps unloaded CampGarrison companions fed, hydrated, and healthy when carrying supplies or stationed at a stocked base.
        /// </summary>
        public static void SustainOfflineGarrisonCompanion(GOAPNPCData npcData)
        {
            if (!CompanionConfigStore.EnableAutoBioAndSleep ||
                MultiplayerSyncSystem.IsMultiplayerClient || !Il2CppSafe.IsAlive(npcData) || string.IsNullOrEmpty(npcData.npcGUID))
                return;
            if (!CompanionConfigStore.CompanionStates.TryGetValue(npcData.npcGUID, out var st) || st == null ||
                st.RosterMode != CompanionRosterMode.CampGarrison || st.IsDismissedOrNotInTeam)
                return;

            float maxHp = npcData.maxHealth > 1f ? npcData.maxHealth : 100f;
            if (npcData.thirst >= 55f && npcData.hunger >= 55f && npcData.health >= maxHp * 0.80f)
                return;

            var offlineDrinks = TryGetNpcDataDrinkingItems(npcData);
            bool hasDrinks = offlineDrinks != null && offlineDrinks.Count > 0;

            bool hasStockedBase = st.HasHomeAnchor && AutoLootSystem.IsPositionNearPlayerBase(st.HomeAnchorPos, 32f);

            var invs = CollectCompanionInventories(npcData.characterData, npcData);
            if (npcData.hunger < 55f &&
                TryFindConsumableFoodInInventories(invs, forThirst: false, CompanionConfigStore.AllowSpendRareFood,
                    out var foodInv, out var foodItem, out var foodAttr))
            {
                ConsumeOneOfflineSupply(npcData, foodInv, foodItem, foodAttr);
            }

            if (npcData.thirst < 55f &&
                TryFindConsumableFoodInInventories(invs, forThirst: true, CompanionConfigStore.AllowSpendRareFood,
                    out var drinkInv, out var drinkItem, out var drinkAttr))
            {
                ConsumeOneOfflineSupply(npcData, drinkInv, drinkItem, drinkAttr);
            }

            if (hasStockedBase || hasDrinks)
            {
                if (npcData.thirst < 65f)
                    npcData.thirst = Mathf.Max(npcData.thirst, 65f);
                if (hasStockedBase)
                {
                    if (npcData.hunger < 60f)
                        npcData.hunger = Mathf.Max(npcData.hunger, 60f);
                    if (npcData.health > 0.5f)
                        npcData.health = Mathf.Max(npcData.health, maxHp * 0.85f);
                }
            }

            var status = npcData.characterData?.characterStatusData;
            if (Il2CppSafe.IsAlive(status))
            {
                float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
                float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
                float statusMaxHp = status.maxhp > 1f ? status.maxhp : maxHp;
                status.hunger = Mathf.Clamp(npcData.hunger, 0f, maxH);
                status.thirst = Mathf.Clamp(npcData.thirst, 0f, maxT);
                if (npcData.health > 0.5f)
                    status.hp = Mathf.Clamp(npcData.health, 1f, statusMaxHp);
            }

            CompanionGearRepairSystem.TryRepairOfflineGarrisonGear(npcData, st);
        }

        internal static void SyncNpcEquipment(HumanCharacterController hc)
        {
            if (!MultiplayerSyncSystem.IsMultiplayerHost || !Il2CppSafe.IsAliveUnity(hc)) return;
            try
            {
                var sync = ZedZone.Net.NetMonsterSync.instance;
                if (!Il2CppSafe.IsAliveUnity(sync)) return;
                if (!_hostSendEquipResolved)
                {
                    _hostSendEquipResolved = true;
                    _hostSendEquipMethod = typeof(ZedZone.Net.NetMonsterSync).GetMethod(
                        "HostSendEquipment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                var ps = _hostSendEquipMethod?.GetParameters();
                if (ps != null && ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(HumanCharacterController)))
                    _hostSendEquipMethod.Invoke(sync, new object[] { hc });
                else if (ps != null && ps.Length == 1 && Il2CppSafe.IsAlive(hc.characterData) && ps[0].ParameterType.IsAssignableFrom(typeof(CharacterData)))
                    _hostSendEquipMethod.Invoke(sync, new object[] { hc.characterData });
            }
            catch { }
        }
    }
}
