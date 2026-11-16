using System;
using System.Collections.Generic;
using System.Reflection;
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
            (st != null && st.RosterMode == CompanionRosterMode.CampGarrison) ||
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

                var status = agent.npcData.characterData?.characterStatusData;
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
            if (!inCombat)
                TryAutonomousEatDrinkAndBandage(agent, hc, cd, status, st, guid, now);

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
                if (mst == null || mst.ActiveOrder != TacticalOrderKind.Follow || !mst.AllowBaseChores || mst.HasAnyPhysicalTask ||
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
            var st = CompanionConfigStore.GetOrCreateCompanion(chosenGuid, chosen.npcData.characterData?.characterName);
            Vector2 selfPos = chosen.humanCharacter.transform.position;
            if (st.ActiveOrder != TacticalOrderKind.Follow || !st.AllowBaseChores || st.HasAnyPhysicalTask || !IsAtCampOrPlayerBase(selfPos, st)) return false;

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

        private static void TryTreatSurvivalDebuffs(
            GOAPAgent agent, CharacterData cd, CharacterStatusData status,
            CharacterEquipmentData eq, CompanionRuntimeState st, string guid, float now)
        {
            if (LastRemedyAtByGuid.TryGetValue(guid, out float lastAt) && now - lastAt < 12f) return;

            float stdTemp = CharacterStatusData.standardBodyTemperature > 30f ? CharacterStatusData.standardBodyTemperature : 37f;
            bool hypothermic = status.bodyTemperature > 10f && status.bodyTemperature < stdTemp - 1.5f;
            bool feverish = status.bodyTemperature > stdTemp + 1.5f;
            bool irradiated = status.radiationDose > BioRadiationThreshold;
            bool lifeThreatening = (status.maxhp > 1f && (status.hp / status.maxhp) < 0.38f) ||
                                   status.radiationDose > 12f ||
                                   (status.bodyTemperature > 10f && status.bodyTemperature < stdTemp - 3.0f);
            if (!hypothermic && !feverish && !irradiated && !lifeThreatening) return;

            LastRemedyAtByGuid[guid] = now;
            if (hypothermic) { try { new GOAPAction_ClearDebuff_Hypothermia().Execute(agent); } catch { } }
            try { new GOAPAction_ClearDebuff_Burning().Execute(agent); } catch { }
            if (CompanionConfigStore.AllowSpendRareMeds && lifeThreatening)
            {
                try { new GOAPAction_ClearDebuff_Spore().Execute(agent); } catch { }
                try { new GOAPAction_ClearDebuff_Fever().Execute(agent); } catch { }
            }

            var inv = Il2CppSafe.IsAlive(cd.inventoryData) ? cd.inventoryData : eq.backpackItemData?.inventoryData;
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return;

            var medIds = new List<int>();
            int medUnits = 0;
            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var it = inv.itemList[i];
                if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f) continue;
                medIds.Add(it.itemId);
                if (ItemCategorizer.ClassifyItem(it) == StorageCategory.Medicine)
                    medUnits += Mathf.Max(1, Mathf.FloorToInt(it.itemNumberFloat));
            }
            if (!lifeThreatening && medUnits <= st.ReserveMedMin) return;

            int chosenId = ChooseDebuffRemedyItemId(medIds, CompanionConfigStore.AllowSpendRareMeds, lifeThreatening);
            if (chosenId <= 0) return;

            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var it = inv.itemList[i];
                if (!Il2CppSafe.IsAlive(it) || it.itemId != chosenId) continue;
                try { CharacterActionManager.ConsumeOneFood(cd, inv, it); } catch { }
                if (irradiated) status.radiationDose = Mathf.Max(0f, status.radiationDose - (chosenId == 277 ? 15f : 4f));
                if (hypothermic || feverish) status.bodyTemperature = Mathf.Lerp(status.bodyTemperature, stdTemp, 0.5f);
                break;
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

        private enum CampResupplyKind { Drink = 0, Heal = 1, Food = 2 }

        private static void TryAutonomousEatDrinkAndBandage(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd,
            CharacterStatusData status, CompanionRuntimeState st, string guid, float now)
        {
            float maxH = status.maxHunger > 1f ? status.maxHunger : 100f;
            float maxT = status.maxThirst > 1f ? status.maxThirst : 100f;
            float maxHp = status.maxhp > 1f ? status.maxhp : 100f;

            bool needsAnyDirect = (status.thirst / maxT) < 0.45f ||
                                  (status.hunger / maxH) < 0.45f ||
                                  (status.hp / maxHp) < 0.78f;

            if (needsAnyDirect && (!LastSelfCareAtByGuid.TryGetValue(guid, out float lastSelfAt) || now - lastSelfAt >= 4.5f))
            {
                LastSelfCareAtByGuid[guid] = now;
                var invs = CollectCompanionInventories(cd, agent.npcData);

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

                // 3) Bandaging / Self-Heal
                if ((status.hp / maxHp) < 0.78f)
                {
                    TrySelfHealFromInventories(agent, hc, cd, status, invs, maxHp);
                }
            }

            if (hc.isSleeping || st.HasAnyPhysicalTask || st.ActiveOrder != TacticalOrderKind.Follow ||
                (st.RosterMode != CompanionRosterMode.CampGarrison && !st.AllowBaseChores) ||
                !IsAtCampOrPlayerBase(hc.transform.position, st))
                return;
            if (LastCampResupplyAtByGuid.TryGetValue(guid, out float lastResupply) && now - lastResupply < 12f)
                return;

            bool stillNeedsThirst = (status.thirst / maxT) < 0.48f;
            bool stillNeedsHunger = (status.hunger / maxH) < 0.48f;
            bool stillNeedsHeal = (status.hp / maxHp) < 0.72f;
            if (!stillNeedsThirst && !stillNeedsHunger && !stillNeedsHeal)
                return;

            TryDispatchCampResupplyWalk(agent, hc, cd, status, st, guid, now,
                stillNeedsThirst, stillNeedsHunger, stillNeedsHeal, maxT, maxHp);
        }

        private static List<InventoryData> CollectCompanionInventories(CharacterData cd, GOAPNPCData npcData)
        {
            var invs = new List<InventoryData>(3);
            var seen = new HashSet<IntPtr>();
            void AddIfAlive(InventoryData inv)
            {
                if (Il2CppSafe.IsAlive(inv) && inv.itemList != null && seen.Add(inv.Pointer))
                    invs.Add(inv);
            }
            AddIfAlive(cd?.inventoryData);
            AddIfAlive(npcData?.sharedInventoryData);
            AddIfAlive(cd?.characterEquipmentData?.backpackItemData?.inventoryData);
            return invs;
        }

        private static InventoryData ResolveItemOwnerInventory(List<InventoryData> invs, ItemData item)
        {
            if (!Il2CppSafe.IsAlive(item)) return null;
            for (int i = 0; i < invs.Count; i++)
            {
                if (InventoryTransfers.Contains(invs[i], item))
                    return invs[i];
            }
            return null;
        }

        private static bool TryDrinkFromOwnContainers(GOAPAgent agent, HumanCharacterController hc, CharacterStatusData status)
        {
            try
            {
                var drinks = agent.GetDrinkingItems();
                if (drinks == null || drinks.Count == 0) return false;
                for (int i = 0; i < drinks.Count; i++)
                {
                    var d = drinks[i];
                    if (!Il2CppSafe.IsAlive(d)) continue;
                    float beforeT = status.thirst;
                    float vol = HumanCharacterController.defaultDrinkVolume > 10f ? HumanCharacterController.defaultDrinkVolume : 250f;
                    try { hc.DrinkLiquidFromContainer(d, vol); } catch { }
                    if (Il2CppSafe.IsAlive(agent.npcData))
                        agent.npcData.thirst = status.thirst;
                    if (status.thirst > beforeT + 0.01f)
                        return true;
                }
            }
            catch { }
            return false;
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

        private static bool TrySelfHealFromInventories(
            GOAPAgent agent, HumanCharacterController hc, CharacterData cd,
            CharacterStatusData status, List<InventoryData> invs, float maxHp)
        {
            float hpRatio = status.hp / maxHp;
            bool allowRare = CompanionConfigStore.AllowSpendRareMeds && hpRatio < 0.38f;
            ItemData chosenMed = null;
            InventoryData medInv = null;

            try
            {
                var heals = agent.GetHealingItems();
                if (heals != null)
                {
                    ItemData rareCand = null;
                    InventoryData rareInv = null;
                    for (int i = 0; i < heals.Count; i++)
                    {
                        var cand = heals[i];
                        if (!Il2CppSafe.IsAlive(cand) || cand.itemNumberFloat < 1f) continue;
                        var owner = ResolveItemOwnerInventory(invs, cand);
                        if (!Il2CppSafe.IsAlive(owner)) continue;
                        if (!CompanionProfileSystem.IsRareMedItem(cand.itemId))
                        {
                            chosenMed = cand;
                            medInv = owner;
                            break;
                        }
                        if (allowRare && rareCand == null)
                        {
                            rareCand = cand;
                            rareInv = owner;
                        }
                    }
                    if (chosenMed == null && rareCand != null)
                    {
                        chosenMed = rareCand;
                        medInv = rareInv;
                    }
                }
            }
            catch { }

            if (!Il2CppSafe.IsAlive(chosenMed) || !Il2CppSafe.IsAlive(medInv))
            {
                int passes = allowRare ? 2 : 1;
                for (int pass = 0; pass < passes && !Il2CppSafe.IsAlive(chosenMed); pass++)
                {
                    for (int invIdx = 0; invIdx < invs.Count && !Il2CppSafe.IsAlive(chosenMed); invIdx++)
                    {
                        var inv = invs[invIdx];
                        if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                        for (int i = 0; i < inv.itemList.Count; i++)
                        {
                            var cand = inv.itemList[i];
                            if (!Il2CppSafe.IsAlive(cand) || cand.itemNumberFloat < 1f) continue;
                            if (ItemCategorizer.ClassifyItem(cand) != StorageCategory.Medicine) continue;
                            bool isRare = CompanionProfileSystem.IsRareMedItem(cand.itemId);
                            if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                            chosenMed = cand;
                            medInv = inv;
                            break;
                        }
                    }
                }
            }

            if (!Il2CppSafe.IsAlive(chosenMed) || !Il2CppSafe.IsAlive(medInv)) return false;

            var medAttr = ItemManager.instance?.GetItemAttrById(chosenMed.itemId)?.TryCast<ItemAttr_Food>();
            float beforeHp = status.hp;
            bool consumed = false;
            try { consumed = CharacterActionManager.ConsumeOneFood(cd, medInv, chosenMed); } catch { }
            if (!consumed) return false;

            if (status.hp <= beforeHp + 0.1f)
            {
                float healAmt = Mathf.Max(22f, (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.hp > 0f) ? medAttr.hp : 25f);
                status.hp = Mathf.Min(maxHp, status.hp + healAmt);
            }
            hc.hp = status.hp;
            if (Il2CppSafe.IsAlive(agent.npcData))
                agent.npcData.health = status.hp;
            try { hc.HealAllBodyPartInjuries(); } catch { }
            return true;
        }

        private static bool TryFindChestResupplyItem(
            List<DiscoveredContainer> chests, Vector2 selfPos, sbyte zLevel, CampResupplyKind kind, float hpRatio,
            out DiscoveredContainer matchedChest, out ItemData matchedItem, out bool isDrinkContainer)
        {
            matchedChest = null;
            matchedItem = null;
            isDrinkContainer = false;
            if (chests == null || chests.Count == 0) return false;

            bool allowRare = kind == CampResupplyKind.Heal
                ? (CompanionConfigStore.AllowSpendRareMeds && hpRatio < 0.38f)
                : CompanionConfigStore.AllowSpendRareFood;
            int passes = allowRare ? 2 : 1;
            float bestDist = 36f;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < chests.Count; i++)
                {
                    var c = chests[i];
                    if (c?.Rule == null || !c.Rule.IsMarkedByPlayer ||
                        !Il2CppSafe.IsAliveUnity(c.TerrainObj) || !Il2CppSafe.IsAlive(c.Inventory) ||
                        c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                        continue;
                    if (Il2CppSafe.IsAlive(c.TerrainObj.objectData) && c.TerrainObj.objectData.zLayer != zLevel)
                        continue;
                    if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                        continue;

                    float d = Vector2.Distance(selfPos, c.WorldPos);
                    if (d >= bestDist) continue;

                    ItemData found = null;
                    bool foundContainer = false;

                    if (kind == CampResupplyKind.Drink && pass == 0)
                    {
                        var contCand = new Il2CppSystem.Collections.Generic.List<ItemData>();
                        for (int j = 0; j < c.Inventory.itemList.Count; j++)
                        {
                            var it = c.Inventory.itemList[j];
                            if (!Il2CppSafe.IsAlive(it)) continue;
                            var attr = ItemManager.instance?.GetItemAttrById(it.itemId);
                            if (Il2CppSafe.IsAliveUnity(attr) && attr.TryCast<ItemAttr_Container>() != null)
                                contCand.Add(it);
                        }
                        if (contCand.Count > 0)
                        {
                            var validDrinks = new Il2CppSystem.Collections.Generic.List<ItemData>();
                            try { GOAPNPCData.AppendDrinkingContainers(contCand, validDrinks); } catch { }
                            if (validDrinks.Count > 0 && Il2CppSafe.IsAlive(validDrinks[0]))
                            {
                                found = validDrinks[0];
                                foundContainer = true;
                            }
                        }
                    }

                    if (!Il2CppSafe.IsAlive(found))
                    {
                        for (int j = 0; j < c.Inventory.itemList.Count; j++)
                        {
                            var it = c.Inventory.itemList[j];
                            if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f) continue;

                            if (kind == CampResupplyKind.Heal)
                            {
                                if (ItemCategorizer.ClassifyItem(it) != StorageCategory.Medicine) continue;
                                bool isRare = CompanionProfileSystem.IsRareMedItem(it.itemId);
                                if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                                found = it;
                                break;
                            }
                            else
                            {
                                if (!IsEligibleConsumableFood(it, forThirst: kind == CampResupplyKind.Drink, allowRare, 8f, out _, out bool isRare))
                                    continue;
                                if ((pass == 0 && isRare) || (pass == 1 && !isRare)) continue;
                                found = it;
                                break;
                            }
                        }
                    }

                    if (Il2CppSafe.IsAlive(found))
                    {
                        bestDist = d;
                        matchedChest = c;
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
            bool needThirst, bool needHunger, bool needHeal, float maxT, float maxHp)
        {
            if (!Il2CppSafe.IsAlive(cd.inventoryData))
                try { cd.RefreshInventorySize(); } catch { }
            InventoryData destInv = Il2CppSafe.IsAlive(cd.inventoryData)
                ? cd.inventoryData
                : agent.npcData.GetOrCreateSharedInventory();
            if (!Il2CppSafe.IsAlive(destInv)) return;

            Vector2 selfPos = hc.transform.position;
            sbyte zLevel = agent.npcData.zLevel;
            float hpRatio = status.hp / maxHp;
            var chests = BaseSortingSystem.ScanNearbyContainers(force: false);

            if (needThirst)
            {
                if (TryFindChestResupplyItem(chests, selfPos, zLevel, CampResupplyKind.Drink, hpRatio,
                    out var drinkChest, out var drinkItem, out bool isCont))
                {
                    StartChestResupplyWalk(agent, st, guid, now, drinkChest, drinkItem, CampResupplyKind.Drink, isCont);
                    return;
                }

                var well = FindNearestReachableWaterWell(selfPos, zLevel, 28f);
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

            if (needHeal && TryFindChestResupplyItem(chests, selfPos, zLevel, CampResupplyKind.Heal, hpRatio,
                out var healChest, out var healItem, out _))
            {
                StartChestResupplyWalk(agent, st, guid, now, healChest, healItem, CampResupplyKind.Heal, false);
                return;
            }

            if (needHunger && TryFindChestResupplyItem(chests, selfPos, zLevel, CampResupplyKind.Food, hpRatio,
                out var foodChest, out var foodItem, out _))
            {
                StartChestResupplyWalk(agent, st, guid, now, foodChest, foodItem, CampResupplyKind.Food, false);
            }
        }

        private static void StartChestResupplyWalk(
            GOAPAgent agent, CompanionRuntimeState st, string guid, float now,
            DiscoveredContainer chest, ItemData item, CampResupplyKind kind, bool isDrinkContainer)
        {
            if (chest == null || !Il2CppSafe.IsAliveUnity(chest.TerrainObj) || !Il2CppSafe.IsAlive(chest.Inventory) || !Il2CppSafe.IsAlive(item))
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
                        !Il2CppSafe.IsAliveUnity(chest.TerrainObj) || !Il2CppSafe.IsAlive(chest.Inventory))
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

                    if (!TryTakeOneUnitFromChest(chest.Inventory, curDestInv, item, isDrinkContainer, out var taken))
                        return;

                    if (isDrinkContainer)
                    {
                        float vol = HumanCharacterController.defaultDrinkVolume > 10f ? HumanCharacterController.defaultDrinkVolume : 250f;
                        try { curHc.DrinkLiquidFromContainer(taken, vol); } catch { }
                        agent.npcData.thirst = curStatus.thirst;
                    }
                    else if (kind == CampResupplyKind.Heal)
                    {
                        float curMaxHp = curStatus.maxhp > 1f ? curStatus.maxhp : 100f;
                        float beforeHp = curStatus.hp;
                        var medAttr = ItemManager.instance?.GetItemAttrById(taken.itemId)?.TryCast<ItemAttr_Food>();
                        bool consumed = false;
                        try { consumed = CharacterActionManager.ConsumeOneFood(curCd, curDestInv, taken); } catch { }
                        if (consumed)
                        {
                            if (curStatus.hp <= beforeHp + 0.1f)
                            {
                                float healAmt = Mathf.Max(22f, (Il2CppSafe.IsAliveUnity(medAttr) && medAttr.hp > 0f) ? medAttr.hp : 25f);
                                curStatus.hp = Mathf.Min(curMaxHp, curStatus.hp + healAmt);
                            }
                            curHc.hp = curStatus.hp;
                            agent.npcData.health = curStatus.hp;
                            try { curHc.HealAllBodyPartInjuries(); } catch { }
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
            try { GOAPNPCManager.instance?.OfflineUseItem(npcData, item); } catch { }

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

            bool hasDrinks = false;
            try
            {
                var drinks = npcData.GetDrinkingItems();
                hasDrinks = drinks != null && drinks.Count > 0;
            }
            catch { }

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
        }

        private static void SyncNpcEquipment(HumanCharacterController hc)
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
