using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Multiplayer synchronization for ZED ZONE Smart Companions v1.6.0:
    /// - Verified native MonsterAttr IDs from ZEDZONE_Data/resources.assets + GOAPNPCManager:
    ///   200 = Looter ('强盗', prefab 'Looter'), 201 = Survivor ('幸存者', prefab 'Survivor'),
    ///   202 = SecurityGuard ('Augmented Security Officer'), 0 = Player ('Human').
    /// - Never mutates Player or RemotePlayer CharacterData (characterAttrId=0 is valid for players).
    /// - Rescues active host NPCs missing from GameController.characterList and resyncs on peer join.
    /// - Resolves per-player companion ownership &amp; cross-player healing, doctrines, and chest rules.
    /// </summary>
    public static partial class MultiplayerSyncSystem
    {
        public const int LooterMonsterAttrId = 200;
        public const int SurvivorMonsterAttrId = 201;
        public const int SecurityGuardMonsterAttrId = 202;
        public const int RemappedLooterCamp = 23; // 23 % 10 == 3 != 1 (avoids UpdateAgentSight camp%10==1 player collision), IsHostileCamp(23)==true
        public const int ContainerRulePropertyIndex = 7;

        private static float _nextHostNpcScanTime = -999f;
        private static float _nextClientPuppetScanTime = -999f;
        private static float _nextFullStateSyncTime = -999f;
        private static bool _wasOnline = false;
        private static int _lastRemotePlayerCount = 0;
        private static bool _pendingPeerJoinNpcRefresh = false;
        private static readonly Dictionary<IntPtr, int> LastSentWeaponIdByNpc = new Dictionary<IntPtr, int>();

        public static bool IsMultiplayerOnline
        {
            get
            {
                try { return NetworkManager.isOnline; }
                catch { return false; }
            }
        }

        public static bool IsMultiplayerClient
        {
            get
            {
                try { return NetworkManager.isClient; }
                catch { return false; }
            }
        }

        public static bool IsMultiplayerHost
        {
            get
            {
                try { return NetworkManager.isHost; }
                catch { return false; }
            }
        }

        public static string GetLocalPlayerCharacterId()
        {
            try
            {
                var cd = GameController.instance?.playerCharacter?.characterData;
                if (Il2CppSafe.IsAlive(cd) && !string.IsNullOrEmpty(cd.characterId))
                    return cd.characterId;
            }
            catch { }
            return "";
        }

        public static string GetLocalPlayerDisplayName()
        {
            try
            {
                if (Il2CppSafe.IsAliveUnity(NetworkManager.instance) && !string.IsNullOrEmpty(NetworkManager.instance.localPlayerName))
                    return NetworkManager.instance.localPlayerName;
                var cd = GameController.instance?.playerCharacter?.characterData;
                if (Il2CppSafe.IsAlive(cd) && !string.IsNullOrEmpty(cd.characterName))
                    return cd.characterName;
            }
            catch { }
            return "Player";
        }

        public static bool IsAnyPlayerCharacterData(CharacterData data)
        {
            if (!Il2CppSafe.IsAlive(data)) return false;
            try
            {
                if (GOAPNPCManager.IsPlayerCharacterData(data)) return true;

                var localCd = GameController.instance?.playerCharacter?.characterData;
                if (Il2CppSafe.IsAlive(localCd))
                {
                    if (localCd.Pointer == data.Pointer) return true;
                    if (!string.IsNullOrEmpty(localCd.characterId) &&
                        string.Equals(localCd.characterId, data.characterId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                var playerSync = NetPlayerSync.instance;
                if (Il2CppSafe.IsAliveUnity(playerSync) && playerSync.m_remotes != null)
                {
                    foreach (var kv in playerSync.m_remotes)
                    {
                        var remote = kv.Value;
                        if (!Il2CppSafe.IsAlive(remote)) continue;
                        var rcd = Il2CppSafe.IsAlive(remote.characterData)
                            ? remote.characterData
                            : (Il2CppSafe.IsAliveUnity(remote.controller) ? remote.controller.characterData : null);
                        if (!Il2CppSafe.IsAlive(rcd)) continue;
                        if (rcd.Pointer == data.Pointer) return true;
                        if (!string.IsNullOrEmpty(rcd.characterId) &&
                            string.Equals(rcd.characterId, data.characterId, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool IsAnyAlliedPlayerCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return false;
            return ResolveLeaderController(characterId, out _) != null;
        }

        public static void EnsureHumanMonsterAttrsConfigured()
        {
            try
            {
                var mm = MonsterManager.instance;
                if (!Il2CppSafe.IsAlive(mm)) return;
                // Only ensure hostile Looter (200) has valid threat range; never turn peaceful Survivor (201) into a global threat!
                var looterAttr = mm.GetMonsterAttrById(LooterMonsterAttrId);
                if (Il2CppSafe.IsAliveUnity(looterAttr))
                {
                    if (looterAttr.monsterThreatLevel <= 1f) looterAttr.monsterThreatLevel = 25f;
                    if (looterAttr.monsterThreatRange <= 5f) looterAttr.monsterThreatRange = 28f;
                }
            }
            catch { }
        }

        public static void NormalizeHumanCharacterData(CharacterData data, bool isLooterHint = false)
        {
            if (!Il2CppSafe.IsAlive(data)) return;
            try
            {
                // CRITICAL: Player MonsterAttr is 0 ('Player'). Never overwrite a local or remote player's CharacterData!
                if (IsAnyPlayerCharacterData(data)) return;

                EnsureHumanMonsterAttrsConfigured();
                bool hostileCamp = isLooterHint || CharacterData.IsHostileCamp(data.camp);

                int attrId = data.characterAttrId;
                bool missingAttr = attrId <= 0 ||
                    (Il2CppSafe.IsAlive(MonsterManager.instance) && MonsterManager.instance.GetMonsterAttrById(attrId) == null);

                // Repair legacy saves corrupted by earlier 202 (SecurityGuard) assignment on ordinary survivors/looters
                if (missingAttr || (attrId == SecurityGuardMonsterAttrId && !SecondIslandSecurityGuard.IsGuard(data)))
                {
                    data.characterAttrId = hostileCamp ? LooterMonsterAttrId : SurvivorMonsterAttrId;
                }

                // Fix native UpdateAgentSight (0x67D091) bug: (21 % 10) == (1 % 10) == 1, which caused
                // companions (camp=1) to treat Looters (camp=21) as same-camp allies and never shoot them.
                if (data.camp == 21)
                {
                    data.camp = RemappedLooterCamp;
                }
            }
            catch { }
        }

        public static void Tick()
        {
            float now = Time.unscaledTime;
            bool online = IsMultiplayerOnline;
            int remoteCount = 0;
            try { remoteCount = NetPlayerSync.remotePlayerCount; } catch { }

            if (online && (!_wasOnline || remoteCount != _lastRemotePlayerCount))
            {
                if (remoteCount > _lastRemotePlayerCount)
                    _pendingPeerJoinNpcRefresh = true;
                _wasOnline = true;
                _lastRemotePlayerCount = remoteCount;
                _nextFullStateSyncTime = now + 1.25f;
                _nextHostNpcScanTime = now;
            }
            else if (!online)
            {
                _wasOnline = false;
                _lastRemotePlayerCount = 0;
                _pendingPeerJoinNpcRefresh = false;
                LastSentWeaponIdByNpc.Clear();
            }

            if (online && now >= _nextFullStateSyncTime && _nextFullStateSyncTime > 0f)
            {
                _nextFullStateSyncTime = -1f;
                if (IsMultiplayerHost)
                    BroadcastAllRulesAndCompanionsFromHost();
                else if (IsMultiplayerClient)
                    SendPacketToHost("REQ_SYNC|1");
            }

            if (!IsMultiplayerClient && now >= _nextHostNpcScanTime)
            {
                _nextHostNpcScanTime = now + 0.65f;
                TickHostNpcsAndRemoteLeaders();
            }

            if (IsMultiplayerClient && now >= _nextClientPuppetScanTime)
            {
                _nextClientPuppetScanTime = now + 0.50f;
                TickClientPuppets();
            }
        }
    }
}
