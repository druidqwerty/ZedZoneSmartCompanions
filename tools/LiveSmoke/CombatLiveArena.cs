using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
namespace ZedZone.SmartCompanions.LiveSmoke
{
    [HarmonyPatch(typeof(GameController), nameof(GameController.AddCharacter))]
    internal static class ArenaSpawnBlocker
    {
        static void Postfix(BasicCharacterController character) => CombatLiveRegression.ParkIfUnexpectedHostile(character);
    }

    [HarmonyPatch(typeof(GOAPAgent), "UpdateAgentSight")]
    internal static class ArenaSightPreSweep
    {
        static void Prefix(GOAPAgent __instance) => CombatLiveRegression.SweepBeforeAgentSight(__instance);
    }

    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.SetThreatData))]
    internal static class ArenaThreatFilter
    {
        static bool Prefix(GOAPAgent __instance, BasicCharacterController character, ref GOAPAgent.ThreatData __result)
        {
            return CombatLiveRegression.AllowArenaThreat(__instance, character, ref __result);
        }
    }

    internal static partial class CombatLiveRegression
    {
        private static readonly List<GameObject> ParkedEnemies = new();
        private static bool _arenaActive;

        internal static bool AllowArenaThreat(GOAPAgent instance, BasicCharacterController character, ref GOAPAgent.ThreatData result)
        {
            if (PhysicalCarryLiveRegression.ArenaActive && !PhysicalCarryLiveRegression.Finished &&
                Il2CppSafe.IsAliveUnity(PhysicalCarryLiveRegression.ActiveAgent) &&
                instance.Pointer == PhysicalCarryLiveRegression.ActiveAgent.Pointer)
            {
                if (Il2CppSafe.IsAliveUnity(character) && !character.isPlayer && character.gameObject.activeSelf)
                {
                    ParkedEnemies.Add(character.gameObject);
                    character.gameObject.SetActive(false);
                }
                result = null;
                return false;
            }
            if (!_arenaActive || Finished || !Il2CppSafe.IsAliveUnity(_agent) || instance.Pointer != _agent.Pointer)
                return true;
            if (!Il2CppSafe.IsAliveUnity(character)) return true;
            var expected = _phase == 3 && Il2CppSafe.IsAliveUnity(_hostileHuman)
                ? (BasicCharacterController)_hostileHuman
                : (BasicCharacterController)_zombie;
            if (Il2CppSafe.IsAliveUnity(expected) && character.Pointer == expected.Pointer)
                return true;
            if (!character.isPlayer && character.gameObject.activeSelf)
            {
                ParkedEnemies.Add(character.gameObject);
                character.gameObject.SetActive(false);
            }
            result = null;
            return false;
        }

        internal static void SweepBeforeAgentSight(GOAPAgent instance)
        {
            if (!_arenaActive || Finished || !Il2CppSafe.IsAliveUnity(_agent) || instance.Pointer != _agent.Pointer) return;
            IsolateArena(log: false);
        }

        internal static void ParkIfUnexpectedHostile(BasicCharacterController character)
        {
            if (!_arenaActive || Finished) return;
            if (!Il2CppSafe.IsAliveUnity(character) || character.isPlayer ||
                (Il2CppSafe.IsAliveUnity(_agent) && character.Pointer == _agent.humanCharacter.Pointer) ||
                (Il2CppSafe.IsAliveUnity(_zombie) && character.Pointer == _zombie.Pointer) ||
                (Il2CppSafe.IsAliveUnity(_hostileHuman) && character.Pointer == _hostileHuman.Pointer) ||
                character.deadFlag || !character.gameObject.activeSelf)
                return;
            var ag = character.GetCachedGOAPAgent();
            if (Il2CppSafe.IsAliveUnity(ag) && ag.IsPlayerTeammate()) return;
            bool isHostile = character.TryCast<ZombieController>() != null ||
                character.TryCast<HordeZombieController>() != null ||
                (Il2CppSafe.IsAlive(character.characterData) && BasicCharacterController.IsHostileCamp(character.characterData));
            if (!isHostile) return;
            ParkedEnemies.Add(character.gameObject);
            character.gameObject.SetActive(false);
        }

        private static void IsolateArena(bool log = true)
        {
            _arenaActive = true;
            var snapshot = new List<BasicCharacterController>();
            foreach(var character in GameController.instance.characterList) snapshot.Add(character);
            foreach(var character in snapshot)
            {
                if(!Il2CppSafe.IsAliveUnity(character) || character.isPlayer ||
                    (Il2CppSafe.IsAliveUnity(_agent) && character.Pointer == _agent.humanCharacter.Pointer) ||
                    character.Pointer==_zombie.Pointer ||
                    (Il2CppSafe.IsAliveUnity(_hostileHuman) && character.Pointer==_hostileHuman.Pointer) ||
                    character.deadFlag || !character.gameObject.activeSelf) continue;
                var ag = character.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(ag) && ag.IsPlayerTeammate()) continue;
                bool isHostile = character.TryCast<ZombieController>() != null ||
                    character.TryCast<HordeZombieController>() != null ||
                    (Il2CppSafe.IsAlive(character.characterData) && BasicCharacterController.IsHostileCamp(character.characterData));
                if (!isHostile) continue;
                ParkedEnemies.Add(character.gameObject);
                if (Il2CppSafe.IsAliveUnity(_agent))
                {
                    if (_agent.threatDataDic != null && _agent.threatDataDic.ContainsKey(character))
                        _agent.threatDataDic.Remove(character);
                    if (Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter) && _agent.mostThreateningCharacter.Pointer == character.Pointer)
                        _agent.mostThreateningCharacter = null;
                }
                character.gameObject.SetActive(false);
            }
            if (log)
                LiveSmokePlugin.Logger.LogInfo("[CombatLive] Isolated one-hostile copied-world arena; temporarily parked "+ParkedEnemies.Count+" other enemies.");
        }
        internal static void SuppressAllNearbyHostilesForLogistics(GOAPAgent logisticsAgent)
        {
            var snapshot = new List<BasicCharacterController>();
            foreach (var character in GameController.instance.characterList) snapshot.Add(character);
            foreach (var character in snapshot)
            {
                if (!Il2CppSafe.IsAliveUnity(character) || character.isPlayer || character.deadFlag || !character.gameObject.activeSelf) continue;
                var ag = character.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(ag) && ag.IsPlayerTeammate()) continue;
                bool isHostile = character.TryCast<ZombieController>() != null ||
                    character.TryCast<HordeZombieController>() != null ||
                    (Il2CppSafe.IsAlive(character.characterData) && BasicCharacterController.IsHostileCamp(character.characterData));
                if (!isHostile) continue;
                ParkedEnemies.Add(character.gameObject);
                character.gameObject.SetActive(false);
            }
            if (Il2CppSafe.IsAliveUnity(logisticsAgent))
            {
                logisticsAgent.mostThreateningCharacter = null;
                logisticsAgent.threatDataDic?.Clear();
            }
        }

        private static void RestoreArena()
        {
            _arenaActive = false;
            foreach(var obj in ParkedEnemies) if(obj!=null)obj.SetActive(true);
            ParkedEnemies.Clear();
        }
    }
}
