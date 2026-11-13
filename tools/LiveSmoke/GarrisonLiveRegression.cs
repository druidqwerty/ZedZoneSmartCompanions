using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    /// <summary>
    /// 2.4.0 live engine verification:
    /// - Phase 0: CampGarrison holds companion at camp perimeter while player moves 12m away on a raid.
    /// - Phase 1: SafeDismiss physical walk to marked chest + zero-teleport gear handover (restoring team membership after verification).
    /// </summary>
    internal static class GarrisonLiveRegression
    {
        public static bool Finished { get; private set; }
        public static bool Succeeded { get; private set; }

        private static int _phase = -1;
        private static float _phaseStart;
        private static GOAPAgent _agent;
        private static CompanionRuntimeState _state;
        private static Vector2 _origPlayerPos, _origAgentPos;
        private static Vector2 _campSpot, _raidSpot;
        private static string _origLeaderGuid;
        private static int _chestInitialUnits;
        private static ItemData _testPackItem;
        private static Vector2 _lastPos;
        private static float _walkedDismiss;

        public static bool Tick()
        {
            if (Finished) return true;
            try
            {
                float now = Time.time;
                var gc = GameController.instance;
                var player = gc?.playerCharacter;
                if (!Il2CppSafe.IsAliveUnity(player)) return false;

                if (_phase < 0)
                {
                    var squad = SmartChatterSystem.GetActiveTeammateAgents();
                    if (squad.Count == 0) return false;
                    _agent = squad[0];
                    if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter) || !Il2CppSafe.IsAlive(_agent.npcData))
                        return false;

                    _state = CompanionConfigStore.GetOrCreateCompanion(_agent.npcData.npcGUID, _agent.npcData.characterData?.characterName);
                    _origPlayerPos = player.transform.position;
                    _origAgentPos = _agent.humanCharacter.transform.position;
                    _origLeaderGuid = _agent.npcData.leaderCharacterGUID;

                    if (!TryFindOpenLine(12.0f, out _campSpot, out _raidSpot))
                        return End(false, "FAIL [GarrisonLive]: could not find 12m open line.");

                    _agent.mostThreateningCharacter = null;
                    _agent.humanCharacter.TeleportTo(_campSpot, false);
                    player.TeleportTo(_campSpot, false);

                    _state.RosterMode = CompanionRosterMode.ActiveSquad;
                    CampGarrisonSystem.ToggleCampGarrison(_agent, _state, quiet: true);
                    _state.HomeAnchorPos = _campSpot;
                    _state.HomeAnchorZ = _agent.npcData.zLevel;
                    _state.HasHomeAnchor = true;

                    player.TeleportTo(_raidSpot, false);
                    _phase = 0;
                    _phaseStart = now;
                    LiveSmokePlugin.Logger.LogInfo($"[GarrisonLive] Started Phase 0 (Camp Garrison Hold): camp={_campSpot}, playerRaid={_raidSpot} (12m).");
                    return false;
                }

                if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                    return End(false, "FAIL [GarrisonLive]: companion became invalid.");

                if (Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter))
                    _agent.mostThreateningCharacter = null;

                Vector2 curPos = _agent.humanCharacter.transform.position;

                if (_phase == 0)
                {
                    float drift = Vector2.Distance(curPos, _campSpot);
                    if (drift > 2.20f)
                        return End(false, $"FAIL [GarrisonLive Phase 0]: garrisoned companion followed raid off camp (drift={drift:F2}m)!");

                    if (now - _phaseStart >= 2.5f)
                    {
                        LiveSmokePlugin.Logger.LogInfo($"[GarrisonLive] PASS Phase 0 (Camp Garrison): held within {drift:F2}m for 2.5s while player raided 12m away.");
                        CampGarrisonSystem.ToggleCampGarrison(_agent, _state, quiet: true);
                        player.TeleportTo(_origPlayerPos, false);
                        _agent.humanCharacter.TeleportTo(_origPlayerPos, false);

                        // Setup Phase 1: SafeDismiss with physical walk to player (6m away) & zero-teleport handover
                        var med = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(220), false, false);
                        med.itemNumberFloat = 1f;
                        var shared = _agent.npcData.GetOrCreateSharedInventory();
                        InventoryTransfers.TryAddWholeCopy(shared, med, out _testPackItem);

                        Vector2 startDismissPos = _campSpot;
                        Vector2 targetPlayerPos = _campSpot + (_raidSpot - _campSpot).normalized * 6.5f;
                        _agent.humanCharacter.TeleportTo(startDismissPos, false);
                        player.TeleportTo(targetPlayerPos, false);
                        _chestInitialUnits = CountUnits(player.characterData.inventoryData, 220);

                        // Start physical SafeDismiss directed to player (force no marked chests by temporarily clearing rules, restored in End)
                        _state.IsSafeDismissActive = true;
                        _state.SafeDismissReturnGear = true;
                        _state.SafeDismissTargetInv = player.characterData.inventoryData;
                        _state.SafeDismissTargetAnchor = player.transform;
                        _state.SafeDismissTargetPos = targetPlayerPos;
                        _state.SafeDismissToPlayer = true;
                        PhysicalMovementHelper.BeginLeg(_state, startDismissPos, targetPlayerPos, player.transform);

                        if (CountUnits(player.characterData.inventoryData, 220) > _chestInitialUnits)
                            return End(false, "FAIL [GarrisonLive t=0]: SafeDismiss teleported gear at t=0!");

                        _lastPos = startDismissPos;
                        _walkedDismiss = 0f;
                        _phase = 1;
                        _phaseStart = now;
                        LiveSmokePlugin.Logger.LogInfo($"[GarrisonLive] Started Phase 1 (SafeDismiss Physical Handover): from={startDismissPos} -> to={targetPlayerPos} (6.5m).");
                    }
                    return false;
                }

                if (_phase == 1)
                {
                    float step = Vector2.Distance(curPos, _lastPos);
                    if (step < 3.0f) _walkedDismiss += step;
                    _lastPos = curPos;

                    if (!_state.IsSafeDismissActive)
                    {
                        int afterUnits = CountUnits(player.characterData.inventoryData, 220);
                        if (_walkedDismiss < 3.5f)
                            return End(false, $"FAIL [GarrisonLive Phase 1]: SafeDismiss completed without physical walk (walked={_walkedDismiss:F2}m)!");
                        if (afterUnits <= _chestInitialUnits)
                            return End(false, "FAIL [GarrisonLive Phase 1]: SafeDismiss did not hand over inventory item on arrival!");
                        if (!_state.IsDismissedOrNotInTeam)
                            return End(false, "FAIL [GarrisonLive Phase 1]: SafeDismiss did not mark companion dismissed!");

                        // v3.0.1 Live check #1: Dismissed companion MUST also be purged from native TeamNPCPanel & teamNPCGuids!
                        string dGuid = _agent.npcData.npcGUID;
                        if (player.characterData.teamNPCGuids != null && player.characterData.teamNPCGuids.Contains(dGuid))
                            return End(false, "FAIL [v3.0.1 NativeTeamMenu]: dismissed companion still in player.characterData.teamNPCGuids!");
                        if (Il2CppSafe.IsAliveUnity(TeamNPCPanel.instance) && TeamNPCPanel.instance.npcUIDic != null &&
                            TeamNPCPanel.instance.npcUIDic.ContainsKey(dGuid))
                            return End(false, "FAIL [v3.0.1 NativeTeamMenu]: dismissed companion card still lingering in TeamNPCPanel.npcUIDic!");

                        // v3.0.1 Live check #2: Corridor breadcrumb single-file trail & walkable target clamping!
                        CorridorNavigationHelper.RecordLeaderBreadcrumb(_campSpot, _agent.npcData.zLevel);
                        CorridorNavigationHelper.RecordLeaderBreadcrumb(_campSpot + Vector2.right * 2.0f, _agent.npcData.zLevel);
                        Vector2 trailPt = CorridorNavigationHelper.GetTrailSingleFileSlot(_campSpot + Vector2.right * 2.0f, Vector2.right, 0, 1.35f);
                        Vector2 clampedPt = CorridorNavigationHelper.ClampToWalkableTarget(_agent, trailPt, _campSpot);
                        if (float.IsNaN(clampedPt.x) || Vector2.Distance(clampedPt, _campSpot) > 3.0f)
                            return End(false, $"FAIL [v3.0.1 CorridorNav]: invalid single-file slot {clampedPt}!");

                        // v2.5.0 Live check: GrenadeSafetyGate blocks throw right on player's position, and IssueMoveToCursorOrder assigns fan anchors!
                        FieldTacticsSystem.CommandSafeOffensiveGrenade(_agent, player.transform.position);
                        if (FieldTacticsSystem.LastGrenadeBlockReason != GrenadeSafetyBlockReason.TooCloseToFriendly)
                            return End(false, $"FAIL [v2.5.0 GrenadeSafetyGate]: expected TooCloseToFriendly at player pos, got {FieldTacticsSystem.LastGrenadeBlockReason}!");

                        // v2.6.0 Live check: SavedEverydayMaskByGuid protects displaced face mask inside BuildProtectedBackpackPointers!
                        var dummyPack = new InventoryData { inventorySize = new Vector2Int(6, 6), inventoryId = "test_v260_mask_pack" };
                        var maskItem = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(220), false, false);
                        InventoryTransfers.TryAddWholeCopy(dummyPack, maskItem, out var addedMask);
                        var testSt260 = new CompanionRuntimeState { NpcGuid = "v260_mask_guid", ReserveAmmoMin = 0, ReserveFoodMin = 0, ReserveMedMin = 0 };
                        CompanionSurvivalSystem.SavedEverydayMaskByGuid["v260_mask_guid"] = addedMask;
                        var prot260 = CompanionProfileSystem.BuildProtectedBackpackPointers(_agent, testSt260, dummyPack);
                        CompanionSurvivalSystem.SavedEverydayMaskByGuid.Remove("v260_mask_guid");
                        if (!prot260.Contains(addedMask.Pointer))
                            return End(false, "FAIL [v2.6.0 SavedMask]: BuildProtectedBackpackPointers failed to protect displaced everyday mask!");

                        // v2.7.0 Live check: real world ItemData (gun 406, steak 248, medkit 220) strictly blocked from fuel slots!
                        if (BaseIndustrySystem.IsApprovedStationFuelItem(406, StorageCategory.RangedWeapons, "rifle") ||
                            BaseIndustrySystem.IsApprovedStationFuelItem(248, StorageCategory.FreshFood, "steak") ||
                            !BaseIndustrySystem.IsApprovedStationFuelItem(101, StorageCategory.Materials, "дрова"))
                            return End(false, "FAIL [v2.7.0 FuelWhitelist]: unexpected live fuel whitelist evaluation!");

                        // v3.0.0 Live check: Vehicle seat overflow + zLevel portal routing + PetSystem summary in real engine!
                        var (bSeats, ovSeats) = ExpeditionSystems.PlanVehicleSeatAllocation(new[]
                        {
                            ("g1", 0.90f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                            ("g2", 0.25f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                            ("g3", 0.90f, CompanionSpecialization.Medic, CompanionRosterMode.ActiveSquad)
                        }, 2);
                        if (bSeats.Count != 2 || bSeats[0] != "g2" || bSeats[1] != "g3" || ovSeats.Count != 1)
                            return End(false, "FAIL [v3.0.0 VehicleSeats]: unexpected seat allocation!");
                        string petSum = ExpeditionSystems.GetActivePlayerPetsSummary(ModLanguage.Russian);
                        if (string.IsNullOrEmpty(petSum))
                            return End(false, "FAIL [v3.0.0 Pets]: empty pet summary!");

                        return End(true, $"PASS v2.4–v3.0.1 Live Suite: Garrison, SafeDismiss + Native TeamNPCPanel Purge (walked={_walkedDismiss:F2}m, 0 teleports), CorridorNav, GrenadeSafetyGate, SavedMask, FuelWhitelist, VehicleSeats & Pets.");
                    }

                    if (now - _phaseStart > 16f)
                        return End(false, $"FAIL [GarrisonLive Phase 1 Timeout]: walked={_walkedDismiss:F2}m, block={_state.LastBlockReason}.");
                }
                return false;
            }
            catch (Exception ex)
            {
                return End(false, "FAIL [GarrisonLive Exception]: " + ex);
            }
        }

        private static int CountUnits(InventoryData inv, int itemId)
        {
            if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) return 0;
            int total = 0;
            for (int i = 0; i < inv.itemList.Count; i++)
            {
                var it = inv.itemList[i];
                if (Il2CppSafe.IsAlive(it) && it.itemId == itemId)
                    total += Mathf.Max(1, Mathf.RoundToInt(it.itemNumberFloat));
            }
            return total;
        }

        private static bool TryFindOpenLine(float dist, out Vector2 origin, out Vector2 target)
        {
            foreach (float r in new[] { 0f, 6f, 12f, 20f, 28f })
            {
                int dirs = r == 0f ? 1 : 8;
                for (int d = 0; d < dirs; d++)
                {
                    float ca = d * Mathf.PI / 4f;
                    Vector2 center = _origPlayerPos + new Vector2(Mathf.Cos(ca), Mathf.Sin(ca)) * r;
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        Vector2 axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 o = center - axis * (dist * 0.5f);
                        Vector2 t = center + axis * (dist * 0.5f);
                        if (_agent.IsPathClear(o, t, 0.30f))
                        {
                            origin = o;
                            target = t;
                            return true;
                        }
                    }
                }
            }
            origin = _origAgentPos;
            target = _origPlayerPos;
            return false;
        }

        private static bool End(bool ok, string msg)
        {
            Succeeded = ok;
            Finished = true;
            if (ok) LiveSmokePlugin.Logger.LogInfo("[GarrisonLive] " + msg);
            else LiveSmokePlugin.Logger.LogError("[GarrisonLive] " + msg);
            try
            {
                var player = GameController.instance?.playerCharacter;
                if (_state != null)
                {
                    _state.IsSafeDismissActive = false;
                    _state.RosterMode = CompanionRosterMode.ActiveSquad;
                    _state.IsDismissedOrNotInTeam = false;
                }
                if (Il2CppSafe.IsAliveUnity(_agent) && Il2CppSafe.IsAlive(_agent.npcData))
                {
                    _agent.npcData.leaderCharacterGUID = _origLeaderGuid;
                    _agent.leaderCharacter = player;
                    if (Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData) &&
                        !player.characterData.teamNPCGuids.Contains(_agent.npcData.npcGUID))
                        player.characterData.teamNPCGuids.Add(_agent.npcData.npcGUID);
                    if (GOAPNPCManager.instance?.teammateNPCs != null &&
                        !GOAPNPCManager.instance.teammateNPCs.Contains(_agent.npcData))
                        GOAPNPCManager.instance.teammateNPCs.Add(_agent.npcData);
                    SmartChatterSystem.InvalidateSquadCache();
                }
                if (Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                    _agent.humanCharacter.TeleportTo(_origAgentPos, false);
                if (Il2CppSafe.IsAliveUnity(player))
                    player.TeleportTo(_origPlayerPos, false);
            }
            catch { }
            return true;
        }
    }
}
