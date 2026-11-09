using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Multiplayer synchronization for ZED ZONE Smart Companions v1.4.0:
    /// - Fixes NPC replication on Host &amp; Client (ensures valid characterAttrId 201/202, clears stale 0-id cache, syncs equipment)
    /// - Fixes Client puppet visibility (clears Start() inSightFlag=1 trap after Init()->OnExitSight(), binds client GOAPNPCData)
    /// - Resolves per-player companion ownership (leaderCharacterID) across Host and RemotePlayers
    /// - Enables cross-player companion healing (friend's companions can heal local player and vice versa)
    /// - Syncs base container categories &amp; companion doctrines across all connected MP peers
    /// </summary>
    public static partial class MultiplayerSyncSystem
    {
        public const int SurvivorMonsterAttrId = 201;
        public const int LooterMonsterAttrId = 202;
        public const int RemappedLooterCamp = 23; // 23 % 10 == 3 != 1 (avoids UpdateAgentSight camp%10==1 player collision), IsHostileCamp(23)==true
        public const int ContainerRulePropertyIndex = 7;

        private static float _nextHostNpcScanTime = -999f;
        private static float _nextClientPuppetScanTime = -999f;
        private static float _nextFullStateSyncTime = -999f;
        private static bool _wasOnline = false;
        private static int _lastRemotePlayerCount = 0;
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

        public static void EnsureHumanMonsterAttrsConfigured()
        {
            try
            {
                var mm = MonsterManager.instance;
                if (!Il2CppSafe.IsAlive(mm)) return;
                foreach (int id in new[] { SurvivorMonsterAttrId, LooterMonsterAttrId })
                {
                    var attr = mm.GetMonsterAttrById(id);
                    if (Il2CppSafe.IsAliveUnity(attr))
                    {
                        if (attr.monsterThreatLevel <= 1f) attr.monsterThreatLevel = 25f;
                        if (attr.monsterThreatRange <= 5f) attr.monsterThreatRange = 28f;
                    }
                }
            }
            catch { }
        }

        public static void NormalizeHumanCharacterData(CharacterData data, bool isLooterHint = false)
        {
            if (!Il2CppSafe.IsAlive(data)) return;
            try
            {
                EnsureHumanMonsterAttrsConfigured();
                bool hostileCamp = isLooterHint || CharacterData.IsHostileCamp(data.camp);
                if (data.characterAttrId == 0 ||
                    (Il2CppSafe.IsAlive(MonsterManager.instance) && MonsterManager.instance.GetMonsterAttrById(data.characterAttrId) == null))
                {
                    data.characterAttrId = hostileCamp ? LooterMonsterAttrId : SurvivorMonsterAttrId;
                }
                // Fix native UpdateAgentSight (0x679CC3) bug: (21 % 10) == (1 % 10) == 1, which caused
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
                _wasOnline = true;
                _lastRemotePlayerCount = remoteCount;
                _nextFullStateSyncTime = now + 1.5f;
            }
            else if (!online)
            {
                _wasOnline = false;
                _lastRemotePlayerCount = 0;
            }

            if (online && now >= _nextFullStateSyncTime && _nextFullStateSyncTime > 0f)
            {
                _nextFullStateSyncTime = -1f;
                if (IsMultiplayerHost)
                {
                    BroadcastAllRulesAndCompanionsFromHost();
                }
                else if (IsMultiplayerClient)
                {
                    SendPacketToHost("REQ_SYNC|1");
                }
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
