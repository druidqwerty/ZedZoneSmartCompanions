using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    // Disposable copied world only. No direct Fire calls: pass requires loaded ammo
    // consumption or the weapon's native fireFrameFlag advancing during normal AI.
    internal static partial class CombatLiveRegression
    {
        private static float _start = -1f, _phaseStart, _seenAt = -1f, _nextEquip;
        private static int _phase = -1, _initialAmmo, _initialFireFrame;
        private static GOAPAgent _agent;
        private static ZombieController _zombie;
        private static Vector2 _originalZombiePosition;
        private static bool _zombieEnabled, _zombieWasFrozen;
        private static float _originalHp;
        private static string _originalCombat, _originalWeapon, _originalBasic;
        private static CompanionRuntimeState _state;
        private static TacticalDoctrine _originalDoctrine;
        private static Vector2 _placement;
        internal static bool Succeeded;
        internal static bool Finished;

        internal static bool Tick()
        {
            if (Finished) return true;
            float now = Time.realtimeSinceStartup;
            if (_start < 0f) _start = now;
            try
            {
                if (_phase < 0)
                {
                    if (now - _start < 8f) return false;
                    bool prepare = now >= _nextEquip;
                    if (prepare) _nextEquip = now + 1f;
                    foreach (var candidate in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        var weapon = candidate.humanCharacter.rangedWeapon;
                        if (prepare)
                        {
                            bool equipped = Il2CppSafe.IsAliveUnity(weapon) || GOAPAction_RangeAttack.EquipBestRangedWeapon(candidate);
                            weapon = candidate.humanCharacter.rangedWeapon;
                            int loaded = Il2CppSafe.IsAliveUnity(weapon) && Il2CppSafe.IsAlive(weapon.weaponData)
                                ? BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData) : -1;
                            int reserve = loaded >= 0 ? BasicRangedWeapon.GetInventoryAmmoTotal(candidate.humanCharacter, weapon.weaponData, 0) : -1;
                            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Prep {candidate.npcData.characterData?.characterName}: equipped={equipped} loaded={loaded} reserve={reserve} range={(weapon != null ? weapon.fireRange : 0f)}.");
                            if (loaded >= 0 && loaded < 4 && reserve > 0 && !candidate.humanCharacter.reloadingMagazineFlag)
                                candidate.CheckAndReloadMagazine();
                        }
                        if (Il2CppSafe.IsAliveUnity(weapon) && Il2CppSafe.IsAlive(weapon.weaponData) &&
                            BasicRangedWeapon.GetLoadedAmmo(weapon.weaponData) >= 4 && weapon.fireRange >= 12f)
                        { _agent = candidate; break; }
                    }
                    var gc = GameController.instance;
                    if (_agent == null)
                    {
                        if (now - _start < 20f) return false;
                        return End(false, "BLOCKED: no active teammate with a loaded ranged weapon (>=4 rounds, range>=12m).");
                    }
                    float nearestZombie = float.MaxValue;
                    foreach (var character in gc.characterList)
                    {
                        if (!Il2CppSafe.IsAliveUnity(character) || character.deadFlag) continue;
                        var zombie = character.TryCast<ZombieController>();
                        if (Il2CppSafe.IsAliveUnity(zombie) && Il2CppSafe.IsAlive(zombie.characterData) &&
                            zombie.characterData.zLevel == _agent.npcData.zLevel)
                        {
                            float spawnDistance = Vector2.Distance(zombie.transform.position, gc.playerCharacter.transform.position);
                            if (spawnDistance < nearestZombie) { nearestZombie = spawnDistance; _zombie = zombie; }
                        }
                    }
                    if (_zombie == null) return End(false, "BLOCKED: no live native zombie in the loaded area.");
                    _originalCombat = _agent.npcData.combatCommand;
                    _originalWeapon = _agent.npcData.weaponPreference;
                    _originalBasic = _agent.npcData.basicCommand;
                    _state = CompanionConfigStore.GetOrCreateCompanion(_agent.npcData.npcGUID);
                    _originalDoctrine = _state.Doctrine;
                    _originalZombiePosition = _zombie.transform.position;
                    _zombieEnabled = _zombie.enabled;
                    _zombieWasFrozen = _zombie.isScriptedMoveFreezeActive;
                    _originalHp = _zombie.hp;
                    RememberActorPositions();
                    // A disabled character may be excluded by native perception. Freeze
                    // movement through the native API, keeping its component alive/active.
                    if (!_zombieWasFrozen) _zombie.BeginScriptedMoveFreeze();
                    _zombie.hp = 10000f;
                    _state.Doctrine = TacticalDoctrine.SniperKite;
                    // Returning-to-base deliberately suppresses native combat. Put the
                    // disposable test NPC on the normal escort order, not a forced goal.
                    _agent.npcData.ExecuteNPCCommand("Basic.FollowPlayer");
                    SmartCombatAI.ApplyDoctrineToNpcData(_agent, _state);
                    if (_agent.npcData.combatCommand != "FireAtWill" || _agent.npcData.weaponPreference != "RangedWeaponOnly")
                        return End(false, "FAIL: doctrine is not stored in native bare-leaf format.");
                    _phase = 0;
                    StartPhase(now);
                    return false;
                }

                if (_zombie.deadFlag || _agent.humanCharacter.deadFlag) return End(false, "FAIL: test participant died.");
                _zombie.TeleportTo(_placement, false);
                _agent.LookAtPositionSmoothCompat(_placement, 30f);
                DiagnosePhase(now);
                var gun = _agent.humanCharacter.rangedWeapon;
                if (!Il2CppSafe.IsAliveUnity(gun)) return End(false, "FAIL: loaded ranged weapon was lost during test.");
                float distance = Vector2.Distance(_agent.humanCharacter.transform.position, _zombie.transform.position);
                bool acquired = Il2CppSafe.IsAliveUnity(_agent.mostThreateningCharacter) &&
                    _agent.mostThreateningCharacter.Pointer == _zombie.Pointer;
                if (acquired && _seenAt < 0f)
                {
                    _seenAt = now;
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Acquired phase={_phase} after={now-_phaseStart:F2}s distance={distance:F2}m sight={_agent.sightRange:F2}.");
                }
                bool fired = BasicRangedWeapon.GetLoadedAmmo(gun.weaponData) < _initialAmmo || gun.fireFrameFlag > _initialFireFrame;
                if (_phase < 2 && fired)
                {
                    if (!acquired || distance < 6f) return End(false, "FAIL: discharge was not at the ranged test target/distance.");
                    LiveSmokePlugin.Logger.LogInfo($"[CombatLive] PASS native AI discharge phase={_phase} distance={distance:F2}m after={now-_phaseStart:F2}s.");
                    _phase++;
                    StartPhase(now);
                }
                else if (_phase == 2)
                {
                    if (fired) return End(false, "FAIL: native StopFire permitted a shot.");
                    if (now - _phaseStart > 3f)
                        return End(true, "PASS ranged acquisition/discharge at 8m and 12m; StopFire negative case.");
                }
                else if (now - _phaseStart > 12f)
                {
                    return End(false, $"FAIL: no native shot within12s phase={_phase}, distance={distance:F2}m acquired={acquired}, sight={_agent.sightRange:F2}, command={_agent.npcData.combatCommand}.");
                }
            }
            catch (Exception error) { return End(false, "FAIL: " + error); }
            return false;
        }

        private static void StartPhase(float now)
        {
            float distance = _phase == 1 ? 12f : 8f;
            bool found = FindOpenTestPlacement(distance);
            if (!found) throw new Exception("BLOCKED: no clear native fire line at " + distance + "m.");
            _zombie.TeleportTo(_placement, false);
            _agent.humanCharacter.LookAtPosition(_placement, true);
            if (_phase == 2)
            {
                _state.Doctrine = TacticalDoctrine.Passive;
                SmartCombatAI.ApplyDoctrineToNpcData(_agent, _state);
            }
            _phaseStart = now;
            _seenAt = -1f;
            var gun = _agent.humanCharacter.rangedWeapon;
            _initialAmmo = BasicRangedWeapon.GetLoadedAmmo(gun.weaponData);
            _initialFireFrame = gun.fireFrameFlag;
            LiveSmokePlugin.Logger.LogInfo($"[CombatLive] Begin phase={_phase} distance={distance}m rounds={_initialAmmo} command={_agent.npcData.combatCommand}.");
        }

        private static bool End(bool success, string message)
        {
            Succeeded = success;
            Finished = true;
            if (success) LiveSmokePlugin.Logger.LogInfo("[CombatLive] " + message);
            else LiveSmokePlugin.Logger.LogError("[CombatLive] " + message);
            try
            {
                if (_state != null) _state.Doctrine = _originalDoctrine;
                if (Il2CppSafe.IsAliveUnity(_agent) && _originalCombat != null)
                {
                    _agent.npcData.combatCommand = _originalCombat;
                    _agent.npcData.weaponPreference = _originalWeapon;
                    _agent.agentState.combatCommand = _originalCombat;
                    _agent.agentState.weaponPreference = _originalWeapon;
                    if (!string.IsNullOrEmpty(_originalBasic))
                        _agent.npcData.ExecuteNPCCommand("Basic." + _originalBasic);
                    _agent.executeSelectPriorityGoalFlag = true;
                }
                if (Il2CppSafe.IsAliveUnity(_zombie))
                {
                    _zombie.TeleportTo(_originalZombiePosition, false);
                    _zombie.hp = _originalHp;
                    _zombie.enabled = _zombieEnabled;
                    if (!_zombieWasFrozen) _zombie.EndScriptedMoveFreeze();
                }
                RestoreActorPositions();
            }
            catch (Exception error) { LiveSmokePlugin.Logger.LogWarning("[CombatLive] Lab cleanup: " + error.Message); }
            return true;
        }
    }
}
