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
        private static readonly Dictionary<string, float> NextTickByGuid = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static float _nextSleepScanAt = -999f;
        private static MethodInfo _hostSendEquipMethod;
        private static bool _hostSendEquipResolved;

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
            if (!st.AllowBaseChores || st.HasAnyPhysicalTask) return false;

            Vector2 selfPos = chosen.humanCharacter.transform.position;
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
                });
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
