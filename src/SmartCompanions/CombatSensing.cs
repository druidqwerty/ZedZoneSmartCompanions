using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Runs the native sight scan for nearby physically visible characters even when
    /// the camera is looking elsewhere. Native method inspection recorded in MODLOG
    /// identified camera culling and a seven-frame scan phase, not a weapon range bug.
    /// </summary>
    public static class CombatSensing
    {
        private readonly struct CameraFlag
        {
            public readonly BasicCharacterController Character;
            public readonly bool Original;
            public CameraFlag(BasicCharacterController character, bool original)
            { Character = character; Original = original; }
        }

        public static void Scan(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter)) return;
            var self = agent.humanCharacter;
            var controller = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(controller) || controller.characterList == null ||
                !Il2CppSafe.IsAlive(self.characterData) || self.deadFlag ||
                !agent.enabled || !self.enabled || !self.gameObject.activeInHierarchy ||
                !agent.IsPlayerTeammate() || agent.IsTeammateCombatSuppressed()) return;

            float nativeRange = agent.sightRange;
            if (!PositiveFinite(nativeRange)) return;
            float range = Mathf.Min(24f, nativeRange);
            var weapon = self.rangedWeapon;
            if (Il2CppSafe.IsAliveUnity(weapon))
            {
                float weaponRange = weapon.fireRange;
                if (!PositiveFinite(weaponRange)) return;
                range = Mathf.Min(range, weaponRange);
            }
            else range = Mathf.Min(range, 8f);

            Vector2 selfPosition = self.transform.position;
            Vector2 muzzle = self.GetProjectileAimOrigin();
            Vector3 from = new Vector3(muzzle.x, muzzle.y, self.transform.position.z);
            float squaredRange = range * range;
            sbyte floor = self.characterData.zLevel;
            var changed = new List<CameraFlag>();
            bool oldSelfOnScreen = self.isOnScreen;
            int oldFrameOffset = agent.frameOffset;
            try
            {
                // No yields, global hooks, renderer activation or physics changes.
                // Allies may pass this geometric filter; the original scan still
                // decides hostility, awareness and whether a threat may be added.
                var characters = controller.characterList;
                for (int i = 0; i < characters.Count; i++)
                {
                    var candidate = characters[i];
                    try
                    {
                        if (!Il2CppSafe.IsAliveUnity(candidate) || candidate.Pointer == self.Pointer ||
                            candidate.deadFlag || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                            !Il2CppSafe.IsAlive(candidate.characterData) ||
                            candidate.characterData.zLevel != floor) continue;

                        var candidateHuman = candidate.TryCast<HumanCharacterController>();
                        bool isHostileHuman = Il2CppSafe.IsAliveUnity(candidateHuman) &&
                            SmartCombatAI.IsHostileHumanTarget(agent, candidateHuman);
                        if (Il2CppSafe.IsAliveUnity(candidateHuman))
                        {
                            MultiplayerSyncSystem.NormalizeHumanCharacterData(candidate.characterData, isHostileHuman);
                        }

                        Vector3 targetPosition = candidate.transform.position;
                        if (((Vector2)targetPosition - selfPosition).sqrMagnitude > squaredRange ||
                            !agent.HasClearFireLine(from, candidate.GetTorsePositionOrRoot())) continue;

                        if (isHostileHuman)
                        {
                            try { agent.SetThreatData(candidate); } catch { }
                        }

                        if (candidate.isOnScreen || (!isHostileHuman && !agent.IsInSight(targetPosition))) continue;
                        // Record before mutating so even a native setter failure cannot
                        // leave an earlier character's temporary flag behind.
                        changed.Add(new CameraFlag(candidate, candidate.isOnScreen));
                        candidate.isOnScreen = true;
                    }
                    catch
                    {
                        // Scene teardown can invalidate one actor while the list survives.
                        // Other valid actors and all recorded restoration still proceed.
                    }
                }

                self.isOnScreen = true;
                // UpdateAgentSight accepts phase (frame + frameOffset) % 7 == 1.
                // The caller already applies a 0.30s budget; do not wait for a lucky
                // phase, and do not permanently alter vanilla scheduling.
                agent.frameOffset = (1 - Time.frameCount % 7 + 7) % 7;
                agent.UpdateAgentSight();
            }
            finally
            {
                try { if (Il2CppSafe.IsAliveUnity(agent)) agent.frameOffset = oldFrameOffset; }
                catch { }
                try { if (Il2CppSafe.IsAliveUnity(self)) self.isOnScreen = oldSelfOnScreen; }
                catch { }
                foreach (var flag in changed)
                {
                    try
                    {
                        if (Il2CppSafe.IsAliveUnity(flag.Character))
                            flag.Character.isOnScreen = flag.Original;
                    }
                    catch { }
                }
            }
        }

        private static bool PositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
