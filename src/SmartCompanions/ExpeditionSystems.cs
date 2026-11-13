using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// v3.0.0 Expeditions: BasicVehicle crew seat allocation &amp; parked bodywork maintenance,
    /// physical multi-floor zLevel portal navigation, and PetCharacterController escort/feeding synergy.
    /// </summary>
    public static class ExpeditionSystems
    {
        public const float PortalReachRadius = 2.15f;
        public const float VehicleMaintainRadius = 2.45f;
        public const float PetFeedReachRadius = 2.45f;

        private static readonly HashSet<string> PlannedBoardGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> OverflowGarrisonHoldGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastFloorTransitionAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> PetEscortCompanionByPet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> LastPetFedAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<(TerrainObject obj, TerrainObject_Door_UndergroundEntrance door, Vector2 pos, sbyte z, bool up, bool down)> CachedPortals =
            new List<(TerrainObject, TerrainObject_Door_UndergroundEntrance, Vector2, sbyte, bool, bool)>(32);

        private static readonly PropertyInfo HungerProp =
            typeof(CharacterStatusData).GetProperty("hunger") ?? typeof(CharacterStatusData).GetProperty("satiety");
        private static readonly PropertyInfo MaxHungerProp =
            typeof(CharacterStatusData).GetProperty("maxHunger") ?? typeof(CharacterStatusData).GetProperty("maxhunger");

        private static bool _wasPlayerDriving;
        private static IntPtr _activeDrivenVehiclePtr;
        private static float _lastVehicleTickAt = -999f, _lastVehicleMaintainAt = -999f, _lastPetTickAt = -999f, _lastPortalScanAt = -999f;
        private static Vector2 _lastPortalScanCenter = new Vector2(-99999f, -99999f);

        public static (List<string> boardGuids, List<string> overflowGarrisonGuids) PlanVehicleSeatAllocation(
            IReadOnlyList<(string guid, float hpRatio, CompanionSpecialization spec, CompanionRosterMode roster)> squad,
            int freeSeats)
        {
            var boardGuids = new List<string>();
            var overflowGarrisonGuids = new List<string>();
            if (squad == null || squad.Count == 0) return (boardGuids, overflowGarrisonGuids);

            int seats = Math.Max(0, freeSeats);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(string guid, int tier, int specRank, float hp, int idx)>(squad.Count);

            for (int i = 0; i < squad.Count; i++)
            {
                var m = squad[i];
                if (string.IsNullOrWhiteSpace(m.guid) || !seen.Add(m.guid) || m.roster == CompanionRosterMode.CampGarrison) continue;
                float hp = (float.IsNaN(m.hpRatio) || float.IsInfinity(m.hpRatio)) ? 1f : Mathf.Clamp01(m.hpRatio);
                int specRank = m.spec == CompanionSpecialization.Medic ? 0 : m.spec == CompanionSpecialization.Fighter ? 1 :
                               m.spec == CompanionSpecialization.Quartermaster ? 2 : 3;
                candidates.Add((m.guid, hp < 0.5f ? 0 : 1, specRank, hp, i));
            }

            candidates.Sort((a, b) =>
            {
                int c = a.tier.CompareTo(b.tier);
                if (c != 0) return c;
                if (a.tier == 0 && (c = a.hp.CompareTo(b.hp)) != 0) return c;
                if ((c = a.specRank.CompareTo(b.specRank)) != 0) return c;
                return (c = a.hp.CompareTo(b.hp)) != 0 ? c : a.idx.CompareTo(b.idx);
            });

            for (int i = 0; i < candidates.Count; i++)
                if (i < seats) boardGuids.Add(candidates[i].guid); else overflowGarrisonGuids.Add(candidates[i].guid);
            return (boardGuids, overflowGarrisonGuids);
        }

        public static int FindBestFloorTransitionPortal(
            Vector2 selfPos, sbyte currentZ, sbyte targetZ,
            IReadOnlyList<(Vector2 pos, sbyte zLayer, bool goesUp, bool goesDown)> portals)
        {
            if (currentZ == targetZ || portals == null || portals.Count == 0) return -1;
            if (float.IsNaN(selfPos.x) || float.IsNaN(selfPos.y) || float.IsInfinity(selfPos.x) || float.IsInfinity(selfPos.y)) return -1;

            bool needUp = targetZ > currentZ, needDown = targetZ < currentZ;
            int bestIdx = -1; float bestSqr = float.MaxValue;
            for (int i = 0; i < portals.Count; i++)
            {
                var p = portals[i];
                if (p.zLayer != currentZ || (needUp && !p.goesUp) || (needDown && !p.goesDown)) continue;
                if (float.IsNaN(p.pos.x) || float.IsNaN(p.pos.y) || float.IsInfinity(p.pos.x) || float.IsInfinity(p.pos.y)) continue;
                float sqr = (p.pos - selfPos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; bestIdx = i; }
            }
            return bestIdx;
        }

        public static bool TryStepMultiFloorTransition(GOAPAgent agent, CompanionRuntimeState st, sbyte targetZ)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient || !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag || agent.humanCharacter.isSleeping) return false;

            sbyte currentZ = (sbyte)Mathf.Clamp(agent.npcData.zLevel, sbyte.MinValue, sbyte.MaxValue);
            if (currentZ == targetZ) return false;

            float now = Time.unscaledTime;
            string guid = agent.npcData.npcGUID ?? st?.NpcGuid ?? "";
            if (!string.IsNullOrEmpty(guid) && LastFloorTransitionAt.TryGetValue(guid, out float lastAt) && now - lastAt < 1.25f) return false;

            Vector2 selfPos = agent.humanCharacter.transform.position;
            RefreshPortalSnapshot(selfPos, now);
            if (CachedPortals.Count == 0) return false;

            var tuples = new List<(Vector2 pos, sbyte zLayer, bool goesUp, bool goesDown)>(CachedPortals.Count);
            for (int i = 0; i < CachedPortals.Count; i++)
                tuples.Add((CachedPortals[i].pos, CachedPortals[i].z, CachedPortals[i].up, CachedPortals[i].down));

            int idx = FindBestFloorTransitionPortal(selfPos, currentZ, targetZ, tuples);
            if (idx < 0 || !Il2CppSafe.IsAliveUnity(CachedPortals[idx].obj)) return false;

            var chosen = CachedPortals[idx];
            float dist = Vector2.Distance(selfPos, chosen.pos);
            if (dist > PortalReachRadius)
            {
                try { agent.MoveTo(chosen.pos, 1.45f, dist > 5.5f); } catch { }
                if (st != null) st.CurrentTaskText = CompanionConfigStore.L("Идёт к переходу между этажами...", "Moving to floor stairs/hatch...", "正前往跨层楼梯/入口...");
                return true;
            }

            Vector3 exitPos = new Vector3(chosen.pos.x, chosen.pos.y, 0f);
            if (Il2CppSafe.IsAliveUnity(chosen.door))
            {
                try
                {
                    Vector3 m = chosen.door.GetMigrateDestination(targetZ);
                    if (!float.IsNaN(m.x) && !float.IsNaN(m.y) && ((Vector2)m).sqrMagnitude > 0.001f) exitPos = m;
                }
                catch { }
            }

            agent.npcData.zLevel = targetZ;
            if (Il2CppSafe.IsAlive(agent.humanCharacter.characterData)) agent.humanCharacter.characterData.zLevel = targetZ;
            agent.humanCharacter.movementInput = Vector2.zero;
            agent.hasNavigationTarget = false;
            agent.humanCharacter.TeleportTo(exitPos, false);
            PerformanceAndDiagnostics.ClearCachesOnTransition();
            if (!string.IsNullOrEmpty(guid)) LastFloorTransitionAt[guid] = now;
            if (st != null) st.CurrentTaskText = "";
            return true;
        }

        public static void TickVehicleExpedition(float now)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient || now - _lastVehicleTickAt < 1.0f) return;
            _lastVehicleTickAt = now;
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return;

            var player = gc.playerCharacter;
            BasicVehicle driven = (player.isDriving && Il2CppSafe.IsAliveUnity(player.drivingVehicle)) ? player.drivingVehicle : null;
            var squad = SmartChatterSystem.GetActiveTeammateAgents();

            if (Il2CppSafe.IsAliveUnity(driven))
            {
                if (!_wasPlayerDriving || _activeDrivenVehiclePtr != driven.Pointer)
                {
                    RestoreOverflowGarrison(player.transform.position, forceAll: true);
                    _wasPlayerDriving = true; _activeDrivenVehiclePtr = driven.Pointer;
                    int freeSeats = driven.HasFreePassengerSeat() ? Mathf.Max(0, driven.passengerSeatLimit) : 0;
                    var entries = new List<(string, float, CompanionSpecialization, CompanionRosterMode)>(squad.Count);
                    for (int i = 0; i < squad.Count; i++)
                    {
                        var ag = squad[i];
                        if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag) continue;
                        var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                        var sd = ag.npcData.characterData?.characterStatusData;
                        float hp = (Il2CppSafe.IsAlive(sd) && sd.maxhp > 0.01f) ? (sd.hp / sd.maxhp) : 1f;
                        entries.Add((ag.npcData.npcGUID ?? "", hp, InferSpecialization(st), st.RosterMode));
                    }
                    var (board, overflow) = PlanVehicleSeatAllocation(entries, freeSeats);
                    PlannedBoardGuids.Clear();
                    for (int i = 0; i < board.Count; i++) PlannedBoardGuids.Add(board[i]);
                    for (int i = 0; i < overflow.Count; i++)
                    {
                        if (!CompanionConfigStore.CompanionStates.TryGetValue(overflow[i], out var ovSt) || ovSt == null) continue;
                        ovSt.RosterMode = CompanionRosterMode.CampGarrison;
                        CampGarrisonSystem.ResolveAndPinHomeAnchor(FindAgent(squad, overflow[i]), ovSt);
                        OverflowGarrisonHoldGuids.Add(overflow[i]);
                    }
                }
                GuideCrewAndPetsToBoard(driven, squad);
                return;
            }

            if (_wasPlayerDriving) { _wasPlayerDriving = false; _activeDrivenVehiclePtr = IntPtr.Zero; PlannedBoardGuids.Clear(); }
            RestoreOverflowGarrison(player.transform.position, forceAll: false);
            if (!gc.isPlayerInCombat && CompanionConfigStore.EnableBaseChores && !BaseWorkManager.Paused && now - _lastVehicleMaintainAt >= 6f)
            {
                _lastVehicleMaintainAt = now;
                TryMaintainParkedVehicleBodywork(player, squad, now);
            }
        }

        public static string FormatPetsSummary(int activeFriendlyPets, int hungryPets, ModLanguage lang)
        {
            int active = Math.Max(0, activeFriendlyPets), hungry = Math.Clamp(hungryPets, 0, active);
            return active == 0
                ? CompanionConfigStore.L(lang, "Питомцы: нет активных", "Pets: none active", "宠物：无活跃随行")
                : CompanionConfigStore.L(lang, $"Питомцы в строю: {active} (голодных: {hungry})", $"Active pets: {active} (hungry: {hungry})", $"随行宠物：{active}（饥饿：{hungry}）");
        }

        public static string GetActivePlayerPetsSummary(ModLanguage lang)
        {
            int friendly = 0, hungry = 0;
            try
            {
                var petDatas = PetSystem.GetActivePlayerPetDatas();
                var playerCtrl = GameController.instance?.playerCharacter;
                if (petDatas != null)
                    foreach (var pd in petDatas)
                    {
                        var ctrl = Il2CppSafe.IsAlive(pd) ? PetSystem.GetPetInstance(pd) : null;
                        if (!Il2CppSafe.IsAliveUnity(ctrl) || ctrl.deadFlag ||
                            (Il2CppSafe.IsAliveUnity(playerCtrl) && ctrl.IsHostileToOwner(playerCtrl))) continue;
                        friendly++; if (IsPetHungry(pd)) hungry++;
                    }
            }
            catch { }
            return FormatPetsSummary(friendly, hungry, lang);
        }

        public static void TickPetCompanionSynergy(float now)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient || now - _lastPetTickAt < 3.0f) return;
            _lastPetTickAt = now;
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter) || gc.isPlayerInCombat) return;
            try
            {
                var petDatas = PetSystem.GetActivePlayerPetDatas();
                if (petDatas == null) return;
                var squad = SmartChatterSystem.GetActiveTeammateAgents();
                foreach (var pd in petDatas)
                {
                    var ctrl = Il2CppSafe.IsAlive(pd) ? PetSystem.GetPetInstance(pd) : null;
                    var petAgent = Il2CppSafe.IsAliveUnity(ctrl) && !ctrl.deadFlag && !ctrl.IsHostileToOwner(gc.playerCharacter) ? ctrl.m_petAgent : null;
                    if (!Il2CppSafe.IsAliveUnity(petAgent)) continue;
                    if (gc.playerCharacter.isDriving && petAgent.CanBoardOwnerVehicle()) { petAgent.TryBoardOwnerVehicle(); continue; }

                    string petGuid = pd.npcGUID ?? $"pet_{ctrl.Pointer.ToInt64():X}";
                    PinAndStepPetEscort(squad, ctrl, petGuid, pd.zLevel);
                    if (IsPetHungry(pd) && (!LastPetFedAt.TryGetValue(petGuid, out float fedAt) || now - fedAt >= 35f))
                        TryDispatchCompanionPetFeed(squad, ctrl, petAgent, petGuid, pd.zLevel, now);
                }
            }
            catch { }
        }

        private static void RefreshPortalSnapshot(Vector2 center, float now)
        {
            if (now - _lastPortalScanAt < 1.5f && (center - _lastPortalScanCenter).sqrMagnitude < 64f) return;
            _lastPortalScanAt = now; _lastPortalScanCenter = center; CachedPortals.Clear();
            var seen = new HashSet<IntPtr>();
            var objs = BaseSortingSystem.CollectWorldTerrainObjects(center, 48f);
            for (int i = 0; i < objs.Count; i++) AddPortalIfValid(objs[i], seen);
            if (CachedPortals.Count > 0) return;
            var doors = UnityEngine.Object.FindObjectsOfType<TerrainObject_Door_UndergroundEntrance>();
            if (doors != null) for (int i = 0; i < doors.Length; i++) AddPortalIfValid(doors[i], seen);
            var shafts = UnityEngine.Object.FindObjectsOfType<TerrainObject_DugShaft>();
            if (shafts != null) for (int i = 0; i < shafts.Length; i++) AddPortalIfValid(shafts[i], seen);
        }

        private static void AddPortalIfValid(TerrainObject tobj, HashSet<IntPtr> seen)
        {
            if (!Il2CppSafe.IsAliveUnity(tobj) || !tobj.gameObject.activeInHierarchy || !seen.Add(tobj.Pointer)) return;
            sbyte z = Il2CppSafe.IsAlive(tobj.objectData) ? (sbyte)Mathf.Clamp(tobj.objectData.zLayer, -128, 127) : (sbyte)0;
            var door = tobj.TryCast<TerrainObject_Door_UndergroundEntrance>();
            if (Il2CppSafe.IsAliveUnity(door))
                CachedPortals.Add((tobj, door, door.transform.position, z, Convert.ToInt32(door.GetUpStairState()) != 0, Convert.ToInt32(door.GetDownStairState()) != 0));
            else if (Il2CppSafe.IsAliveUnity(tobj.TryCast<TerrainObject_DugShaft>()))
                CachedPortals.Add((tobj, null, tobj.transform.position, z, true, true));
        }

        private static CompanionSpecialization InferSpecialization(CompanionRuntimeState st) =>
            st == null ? CompanionSpecialization.Scavenger :
            (st.AllowFieldMedic && (st.ReserveMedMin >= 3 || st.Doctrine == TacticalDoctrine.Bodyguard)) ? CompanionSpecialization.Medic :
            st.WorkRole == CompanionWorkRole.Sorter ? CompanionSpecialization.Quartermaster :
            (st.Doctrine == TacticalDoctrine.Assault || st.WorkRole == CompanionWorkRole.Off) ? CompanionSpecialization.Fighter : CompanionSpecialization.Scavenger;

        private static GOAPAgent FindAgent(IReadOnlyList<GOAPAgent> squad, string guid)
        {
            for (int i = 0; i < squad.Count; i++)
                if (Il2CppSafe.IsAliveUnity(squad[i]) && Il2CppSafe.IsAlive(squad[i].npcData) && string.Equals(squad[i].npcData.npcGUID, guid, StringComparison.OrdinalIgnoreCase))
                    return squad[i];
            return null;
        }

        private static void RestoreOverflowGarrison(Vector2 playerPos, bool forceAll)
        {
            if (OverflowGarrisonHoldGuids.Count == 0) return;
            var restored = new List<string>();
            foreach (var guid in OverflowGarrisonHoldGuids)
            {
                if (!CompanionConfigStore.CompanionStates.TryGetValue(guid, out var st) || st == null) { restored.Add(guid); continue; }
                if (forceAll || Vector2.Distance(playerPos, st.HomeAnchorPos) <= 45f)
                { st.RosterMode = CompanionRosterMode.ActiveSquad; st.GarrisonAnchorSettled = false; restored.Add(guid); }
            }
            for (int i = 0; i < restored.Count; i++) OverflowGarrisonHoldGuids.Remove(restored[i]);
        }

        private static void GuideCrewAndPetsToBoard(BasicVehicle vehicle, IReadOnlyList<GOAPAgent> squad)
        {
            int vZ = Il2CppSafe.IsAlive(vehicle.vehicleData) ? vehicle.vehicleData.zLevel : 0;
            if (vehicle.HasFreePassengerSeat())
                for (int i = 0; i < squad.Count; i++)
                {
                    var ag = squad[i];
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter)) continue;
                    if (ag.humanCharacter.deadFlag || ag.humanCharacter.isDriving || ag.npcData.zLevel != vZ || !PlannedBoardGuids.Contains(ag.npcData.npcGUID ?? "")) continue;
                    Vector2 hullPt = vehicle.GetClosestBodyPoint(ag.humanCharacter.transform.position);
                    float d = Vector2.Distance(ag.humanCharacter.transform.position, hullPt);
                    if (d > 2.2f && d <= 26f) { try { ag.MoveTo(hullPt, 1.6f, d > 6f); } catch { } }
                }
            try
            {
                var petDatas = PetSystem.GetActivePlayerPetDatas();
                var ownerCtrl = GameController.instance?.playerCharacter;
                if (petDatas != null)
                    foreach (var pd in petDatas)
                    {
                        var ctrl = Il2CppSafe.IsAlive(pd) ? PetSystem.GetPetInstance(pd) : null;
                        if (Il2CppSafe.IsAliveUnity(ctrl) && (!Il2CppSafe.IsAliveUnity(ownerCtrl) || !ctrl.IsHostileToOwner(ownerCtrl)) &&
                            Il2CppSafe.IsAliveUnity(ctrl.m_petAgent) && ctrl.m_petAgent.CanBoardOwnerVehicle())
                            ctrl.m_petAgent.TryBoardOwnerVehicle();
                    }
            }
            catch { }
        }

        private static void TryMaintainParkedVehicleBodywork(HumanCharacterController player, IReadOnlyList<GOAPAgent> squad, float now)
        {
            var vehicles = BasicVehicle.s_liveVehicles;
            if (vehicles == null || vehicles.Count == 0) return;
            int pZ = Il2CppSafe.IsAlive(player.characterData) ? player.characterData.zLevel : 0;
            BasicVehicle targetVehicle = null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (!Il2CppSafe.IsAliveUnity(v) || !Il2CppSafe.IsAlive(v.vehicleData) || Il2CppSafe.IsAliveUnity(v.driverCharacterController)) continue;
                if (v.vehicleData.zLevel == pZ && Vector2.Distance(v.transform.position, player.transform.position) <= 28f && v.vehicleBodyWorkHP > 1f && v.vehicleBodyWorkHP < 95f)
                { targetVehicle = v; break; }
            }
            if (!Il2CppSafe.IsAliveUnity(targetVehicle)) return;

            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag || ag.npcData.zLevel != pZ) continue;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (!st.AllowBaseChores || st.HasAnyPhysicalTask || now - st.LastChoreTime < 20f || InferSpecialization(st) != CompanionSpecialization.Quartermaster) continue;
                if (!BaseWorkManager.MayStartAutomaticWork(st, BaseWorkManager.AllowsChores)) continue;

                st.LastChoreTime = now;
                BaseChoreSystem.StartPhysicalChoreWalk(ag, st, targetVehicle.GetClosestBodyPoint(ag.humanCharacter.transform.position), null,
                    CompanionConfigStore.L("Идёт осматривать кузов авто...", "Walking to inspect vehicle bodywork...", "正前往检查载具车身..."), 0, () =>
                    {
                        if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(targetVehicle) || Il2CppSafe.IsAliveUnity(targetVehicle.driverCharacterController)) return;
                        if (Vector2.Distance(ag.humanCharacter.transform.position, targetVehicle.GetClosestBodyPoint(ag.humanCharacter.transform.position)) > VehicleMaintainRadius) return;
                        if (targetVehicle.vehicleBodyWorkHP > 1f && targetVehicle.vehicleBodyWorkHP < 95f)
                            targetVehicle.vehicleBodyWorkHP = Mathf.Min(100f, targetVehicle.vehicleBodyWorkHP + 10f);
                        st.ChoresDoneThisSession++; st.AddExperience(8, ag);
                        st.CurrentTaskText = CompanionConfigStore.L("Подтянул кузов автомобиля", "Tightened vehicle bodywork", "已检修加固载具车身");
                    });
                break;
            }
        }

        private static bool IsPetHungry(GOAPNPCData pd)
        {
            var status = pd?.characterData?.characterStatusData;
            if (!Il2CppSafe.IsAlive(status) || HungerProp == null) return false;
            try
            {
                float h = Convert.ToSingle(HungerProp.GetValue(status)), mh = MaxHungerProp != null ? Convert.ToSingle(MaxHungerProp.GetValue(status)) : 100f;
                return mh > 0.01f && (h / mh) < 0.45f;
            }
            catch { return false; }
        }

        private static void PinAndStepPetEscort(IReadOnlyList<GOAPAgent> squad, PetCharacterController petCtrl, string petGuid, int petZ)
        {
            GOAPAgent best = null; float bestDist = 30f;
            Vector2 petPos = petCtrl.transform.position;
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag || ag.npcData.zLevel != petZ) continue;
                float d = Vector2.Distance(ag.humanCharacter.transform.position, petPos);
                if (d < bestDist) { bestDist = d; best = ag; }
            }
            if (!Il2CppSafe.IsAliveUnity(best)) return;
            string guid = best.npcData.npcGUID ?? "";
            PetEscortCompanionByPet[petGuid] = guid;
            if (bestDist > 10f && bestDist <= 24f && CompanionConfigStore.CompanionStates.TryGetValue(guid, out var st) && st != null &&
                st.RosterMode == CompanionRosterMode.ActiveSquad && !st.HasAnyPhysicalTask)
                try { best.MoveTo(petPos, 3.2f, false); } catch { }
        }

        private static void TryDispatchCompanionPetFeed(IReadOnlyList<GOAPAgent> squad, PetCharacterController petCtrl, GOAPAgent_Pet petAgent, string petGuid, int petZ, float now)
        {
            for (int i = 0; i < squad.Count; i++)
            {
                var ag = squad[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag || ag.npcData.zLevel != petZ) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                if (st.HasAnyPhysicalTask || Vector2.Distance(ag.humanCharacter.transform.position, petCtrl.transform.position) > 22f) continue;
                if (!TryFindEdiblePetFood(ag.npcData.characterData, st.ReserveFoodMin, out var srcInv, out var foodItem)) continue;

                LastPetFedAt[petGuid] = now;
                BaseChoreSystem.StartPhysicalChoreWalk(ag, st, petCtrl.transform.position, petCtrl.transform,
                    CompanionConfigStore.L("Идёт покормить питомца...", "Walking to feed pet...", "正前往投喂宠物..."), foodItem.itemId, () =>
                    {
                        if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(petCtrl) || !Il2CppSafe.IsAliveUnity(petAgent) ||
                            !Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(foodItem) || foodItem.itemNumberFloat < 1f) return;
                        if (Vector2.Distance(ag.humanCharacter.transform.position, petCtrl.transform.position) > PetFeedReachRadius) return;
                        var portion = foodItem.CopyItemData(null);
                        portion.itemNumberFloat = 1f;
                        if (foodItem.itemNumberFloat <= 1f) { if (!srcInv.RemoveItem(foodItem, true)) return; }
                        else { foodItem.itemNumberFloat -= 1f; srcInv.NotifyDataChanged(true, foodItem, false); }
                        petAgent.NotifyFoodDropped(portion, petCtrl.transform.position);
                        st.AddExperience(6, ag);
                    });
                break;
            }
        }

        private static bool TryFindEdiblePetFood(CharacterData cd, int reserveMin, out InventoryData foundInv, out ItemData foundItem)
        {
            foundInv = null; foundItem = null;
            if (!Il2CppSafe.IsAlive(cd)) return false;
            var seen = new HashSet<IntPtr>();
            var invs = new List<InventoryData>(4);
            if (Il2CppSafe.IsAlive(cd.inventoryData) && seen.Add(cd.inventoryData.Pointer)) invs.Add(cd.inventoryData);
            var carried = cd.GetCarriedInventoryList();
            if (carried != null) foreach (var inv in carried) if (Il2CppSafe.IsAlive(inv) && seen.Add(inv.Pointer)) invs.Add(inv);

            int totalFood = 0;
            for (int i = 0; i < invs.Count; i++)
            {
                var list = invs[i].itemList;
                if (list == null) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    var it = list[j];
                    if (!Il2CppSafe.IsAlive(it) || it.itemNumberFloat < 1f || !PetSystem.IsEdibleForPet(it)) continue;
                    if (!CompanionConfigStore.AllowSpendRareFood && CompanionProfileSystem.IsRareFoodItem(it.itemId, FoodKind.None)) continue;
                    totalFood += Mathf.Max(1, Mathf.FloorToInt(it.itemNumberFloat));
                    if (foundItem == null) { foundInv = invs[i]; foundItem = it; }
                }
            }
            return foundItem != null && totalFood > Math.Max(0, reserveMin);
        }
    }
}
