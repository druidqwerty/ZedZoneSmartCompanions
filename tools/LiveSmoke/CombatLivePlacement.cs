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
                    human.TeleportTo(origin, false);
                    // Keep the leader nearby but off the firing line; other squad
                    // members remain at the base instead of overlapping this actor.
                    player.TeleportTo(origin + new Vector2(-axis.y, axis.x) * 5f, false);
                    _zombie.TeleportTo(center, false);
                    human.LookAtPosition(center, true);
                    Physics2D.SyncTransforms();
                    _agent.fireLineCacheTimeFlag = -9999f;
                    Vector2 muzzle = human.GetProjectileAimOrigin();
                    Vector3 from = new Vector3(muzzle.x, muzzle.y, human.transform.position.z);
                    Vector3 to = _zombie.GetTorsePositionOrRoot();
                    tries++;
                    if (!_agent.HasClearFireLine(from, to)) continue;
                    _placement = center;
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Native LOS clear: tries={tries}, origin={origin}, target={center}, muzzle={from}, aim={to}.");
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
