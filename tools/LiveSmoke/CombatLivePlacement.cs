using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    internal static partial class CombatLiveRegression
    {
        private static Vector2 _originalAgentPosition, _originalPlayerPosition;
        private static bool _haveActorPositions;

        private static void RememberActorPositions()
        {
            _originalAgentPosition = _agent.humanCharacter.transform.position;
            _originalPlayerPosition = GameController.instance.playerCharacter.transform.position;
            _haveActorPositions = true;
        }

        private static bool FindOpenTestPlacement(float distance)
        {
            var gc = GameController.instance;
            var human = _agent.humanCharacter;
            var player = gc.playerCharacter;
            // Existing zombie positions are real loaded map locations, usually clear
            // of the owner's furnished base. Try several instead of a synthetic void.
            var centers = new List<Vector2>();
            if (Vector2.Distance(_originalZombiePosition, _originalPlayerPosition) <= 35f)
                centers.Add(_originalZombiePosition);
            foreach (var character in gc.characterList)
            {
                if (centers.Count >= 8) break;
                if (!Il2CppSafe.IsAliveUnity(character) || character.deadFlag ||
                    character.TryCast<ZombieController>() == null ||
                    !Il2CppSafe.IsAlive(character.characterData) ||
                    character.characterData.zLevel != _agent.npcData.zLevel) continue;
                Vector2 center = character.transform.position;
                if (Vector2.Distance(center, _originalPlayerPosition) > 35f) continue;
                bool distinct = true;
                foreach (var old in centers)
                    if (Vector2.Distance(old, center) < 6f) { distinct = false; break; }
                if (distinct) centers.Add(center);
            }
            // A cleared base can have no nearby zombies. Relocate the existing native
            // actor into the already loaded player neighborhood, never move the test
            // 150m away to its original chunk and invalidate world activation.
            foreach (float radius in new[] { 12f, 20f, 28f })
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    centers.Add(_originalPlayerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            int tries = 0;
            foreach (Vector2 center in centers)
            {
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12f;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 origin = center - axis * distance;
                    tries++;
                    if (distance >= 10f && Mathf.Abs(axis.y) > 0.65f) continue;
                    if (!_agent.IsPathClear(origin, center, 0.35f)) continue;
                    Vector2 perp = new Vector2(-axis.y, axis.x);
                    Vector2 leaderSpot = origin + perp * 5.0f;
                    if (!_agent.IsPathClear(origin, leaderSpot, 0.30f))
                        leaderSpot = origin - perp * 5.0f;
                    if (!_agent.IsPathClear(origin, leaderSpot, 0.30f))
                        leaderSpot = origin - axis * 4.5f;
                    human.TeleportTo(origin, false);
                    human.movementInput = Vector2.zero;
                    _agent.hasNavigationTarget = false;
                    // Keep the leader ~5m away and off the firing line; other squad
                    // members remain at the base instead of overlapping this actor.
                    player.TeleportTo(leaderSpot, false);
                    _zombie.TeleportTo(center, false);
                    human.LookAtPosition(center, true);
                    Physics2D.SyncTransforms();
                    _agent.fireLineCacheTimeFlag = -9999f;
                    Vector2 muzzle = human.GetProjectileAimOrigin();
                    Vector3 from = new Vector3(muzzle.x, muzzle.y, human.transform.position.z);
                    Vector3 to = _zombie.GetTorsePositionOrRoot();
                    if (!_agent.HasClearFireLine(from, to)) continue;
                    _placement = center;
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Native LOS+Path clear: tries={tries}, origin={origin}, target={center}, leader={leaderSpot}, muzzle={from}, aim={to}.");
                    return true;
                }
            }
            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] No native clear LOS after {tries} real-map origin trials; collision checks were not overridden.");
            return false;
        }

        private static void RestoreActorPositions()
        {
            if (!_haveActorPositions) return;
            if (Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                _agent.humanCharacter.TeleportTo(_originalAgentPosition, false);
            if (Il2CppSafe.IsAliveUnity(GameController.instance?.playerCharacter))
                GameController.instance.playerCharacter.TeleportTo(_originalPlayerPosition, false);
            _haveActorPositions = false;
        }
    }
}
