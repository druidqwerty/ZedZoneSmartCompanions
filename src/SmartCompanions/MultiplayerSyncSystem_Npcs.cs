using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    public static partial class MultiplayerSyncSystem
    {
        public static void TickHostNpcsAndRemoteLeaders()
        {
            try
            {
                EnsureHumanMonsterAttrsConfigured();
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || gc.characterList == null) return;

                var monsterSync = NetMonsterSync.instance;
                bool isHostOnline = IsMultiplayerHost && Il2CppSafe.IsAliveUnity(monsterSync);

                var chars = gc.characterList;
                for (int i = 0; i < chars.Count; i++)
                {
                    var ch = chars[i];
                    if (!Il2CppSafe.IsAliveUnity(ch) || ch.deadFlag || ch.isPlayer) continue;
                    var human = ch.TryCast<HumanCharacterController>();
                    if (!Il2CppSafe.IsAliveUnity(human) || !Il2CppSafe.IsAlive(human.characterData)) continue;
                    if (NetSessionConfig.IsNetworkPlayer(human)) continue;

                    var agent = human.GetCachedGOAPAgent();
                    bool isLooter = Il2CppSafe.IsAliveUnity(agent) &&
                        (agent.TryCast<GOAPAgent_Looter>() != null ||
                         (Il2CppSafe.IsAlive(agent.npcData) && agent.npcData.isBandit));

                    int prevAttrId = human.characterData.characterAttrId;
                    NormalizeHumanCharacterData(human.characterData, isLooter);

                    if (isHostOnline)
                    {
                        // If characterAttrId was 0 when NetMonsterSync first cached m_spawnBytesScratch,
                        // clear the stale entry and resend spawn so Clients spawn the puppet instead of dropping it!
                        if (prevAttrId == 0 && human.characterData.characterAttrId != 0)
                        {
                            try
                            {
                                if (monsterSync.HostTryGetNetId(human, out int netId) && monsterSync.m_spawnBytesScratch != null)
                                    monsterSync.m_spawnBytesScratch.Remove(netId);
                                monsterSync.HostResendSpawn(human);
                            }
                            catch { }
                        }

                        // Sync NPC weapon/equipment visuals to Clients whenever weapon changes
                        int curWeaponId = human.characterData.characterEquipmentData?.currentWeaponItemData?.itemId ?? 0;
                        if (!LastSentWeaponIdByNpc.TryGetValue(human.Pointer, out int lastWep) || lastWep != curWeaponId)
                        {
                            LastSentWeaponIdByNpc[human.Pointer] = curWeaponId;
                            try { monsterSync.HostSendEquipment(human); } catch { }
                        }
                    }

                    // Bind companion ownership & remote leader position/zLevel
                    if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData))
                    {
                        SyncCompanionOwnerAndLeader(agent);
                    }
                }
                CompanionConfigStore.ApplyMaxTeamSizeIfConfigured();
            }
            catch { }
        }

        public static void OnCompanionDismissed(GOAPNPCData npcData)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return;
            try
            {
                var st = CompanionConfigStore.GetOrCreateCompanion(npcData.npcGUID, npcData.characterData?.characterName);
                BaseSortingSystem.FlushCarryBufferSafely(st);
                st.IsSortingActive = false;
                st.SortingPhase = 0;
                st.PendingTransferCount = 0;
                st.OwnerCharacterId = "";
                st.OwnerPlayerName = "";
                st.IsDismissedOrNotInTeam = true;

                npcData.leaderCharacterGUID = null;
                var ag = npcData.agentCache;
                if (Il2CppSafe.IsAliveUnity(ag))
                {
                    ag.leaderCharacter = null;
                    if (Il2CppSafe.IsAlive(ag.agentState))
                    {
                        ag.agentState.SetState("hasLeader", false);
                        ag.agentState.SetState("leaderCharacter", (Il2CppSystem.Object)null);
                    }
                }
                CompanionConfigStore.Save();
            }
            catch { }
        }

        public static void OnCompanionRecruited(CharacterData leaderData, GOAPNPCData npcData)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return;
            try
            {
                var st = CompanionConfigStore.GetOrCreateCompanion(npcData.npcGUID, npcData.characterData?.characterName);
                st.IsDismissedOrNotInTeam = false;
                string lid = Il2CppSafe.IsAlive(leaderData) ? leaderData.characterId : npcData.leaderCharacterGUID;
                if (!string.IsNullOrEmpty(lid))
                {
                    st.OwnerCharacterId = lid;
                    st.OwnerPlayerName = ResolvePlayerNameByCharacterId(lid);
                }
                CompanionConfigStore.Save();
            }
            catch { }
        }

        private static bool IsNpcInAnyActivePlayerTeam(GOAPNPCData npcData, string ownerCharId)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return false;
            try
            {
                var mgr = GOAPNPCManager.instance;
                if (Il2CppSafe.IsAlive(mgr) && mgr.teammateNPCs != null && mgr.teammateNPCs.Contains(npcData))
                    return true;

                if (!string.IsNullOrEmpty(ownerCharId))
                {
                    var leaderCtrl = ResolveLeaderController(ownerCharId, out _);
                    if (Il2CppSafe.IsAliveUnity(leaderCtrl) && Il2CppSafe.IsAlive(leaderCtrl.characterData) &&
                        leaderCtrl.characterData.teamNPCGuids != null &&
                        !string.IsNullOrEmpty(npcData.npcGUID) &&
                        leaderCtrl.characterData.teamNPCGuids.Contains(npcData.npcGUID))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SyncCompanionOwnerAndLeader(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return;
            try
            {
                var npcData = agent.npcData;
                string leaderId = npcData.leaderCharacterGUID;
                var st = CompanionConfigStore.GetOrCreateCompanion(npcData.npcGUID, npcData.characterData?.characterName);

                if (st.IsDismissedOrNotInTeam)
                {
                    if (!string.IsNullOrEmpty(leaderId) && IsNpcInAnyActivePlayerTeam(npcData, leaderId))
                    {
                        st.IsDismissedOrNotInTeam = false;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(npcData.leaderCharacterGUID))
                            npcData.leaderCharacterGUID = null;
                        if (Il2CppSafe.IsAliveUnity(agent.leaderCharacter))
                            agent.leaderCharacter = null;
                        return;
                    }
                }

                if (string.IsNullOrEmpty(leaderId))
                {
                    if (!string.IsNullOrEmpty(st.OwnerCharacterId))
                    {
                        // Never resurrect a dismissed companion! Only restore leaderCharacterGUID if the NPC is STILL in teammateNPCs / teamNPCGuids
                        if (IsMultiplayerClient || IsNpcInAnyActivePlayerTeam(npcData, st.OwnerCharacterId))
                        {
                            leaderId = st.OwnerCharacterId;
                            npcData.leaderCharacterGUID = leaderId;
                        }
                        else
                        {
                            OnCompanionDismissed(npcData);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    st.IsDismissedOrNotInTeam = false;
                    if (string.IsNullOrEmpty(st.OwnerCharacterId))
                    {
                        st.OwnerCharacterId = leaderId;
                        st.OwnerPlayerName = ResolvePlayerNameByCharacterId(leaderId);
                    }
                }

                if (string.IsNullOrEmpty(leaderId)) return;

                var leaderCtrl = ResolveLeaderController(leaderId, out string ownerName);
                if (!string.IsNullOrEmpty(ownerName))
                    st.OwnerPlayerName = ownerName;

                if (!Il2CppSafe.IsAliveUnity(leaderCtrl) || leaderCtrl.deadFlag) return;

                // Keep remote player's CharacterData.characterPosition synced to live transform so GOAPAction_FollowLeader works!
                if (Il2CppSafe.IsAlive(leaderCtrl.characterData))
                {
                    Vector2 leaderPos = leaderCtrl.transform.position;
                    leaderCtrl.characterData.characterPosition = leaderPos;

                    // Sync companion zLevel to its owner's zLevel in MP (vanilla OnTimeUpdate skips this when NetZLayers.enabled!)
                    sbyte leaderZ = leaderCtrl.characterData.zLevel;
                    if (npcData.zLevel != leaderZ && !GOAPNPCManager.IsStayingBehindOrder(npcData))
                    {
                        npcData.zLevel = leaderZ;
                        if (Il2CppSafe.IsAlive(npcData.characterData))
                            npcData.characterData.zLevel = leaderZ;
                        if (Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                        {
                            bool onHostLayer = Il2CppSafe.IsAliveUnity(MapController.instance) &&
                                leaderZ == MapController.instance.currentZLevel;
                            NetShadowLayer.SetShadowCharacter(agent.humanCharacter, !onHostLayer);
                        }
                    }
                }

                if (!Il2CppSafe.IsAliveUnity(agent.leaderCharacter) || agent.leaderCharacter.Pointer != leaderCtrl.Pointer)
                {
                    agent.leaderCharacter = leaderCtrl;
                    if (Il2CppSafe.IsAlive(agent.agentState))
                        agent.agentState.SetState("leaderCharacter", leaderCtrl);
                }
            }
            catch { }
        }

        public static HumanCharacterController ResolveLeaderController(string leaderCharacterId, out string playerName)
        {
            playerName = "";
            if (string.IsNullOrEmpty(leaderCharacterId)) return null;
            try
            {
                var localPlayer = GameController.instance?.playerCharacter;
                if (Il2CppSafe.IsAliveUnity(localPlayer) && Il2CppSafe.IsAlive(localPlayer.characterData) &&
                    string.Equals(localPlayer.characterData.characterId, leaderCharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    playerName = GetLocalPlayerDisplayName();
                    return localPlayer;
                }

                var playerSync = NetPlayerSync.instance;
                if (Il2CppSafe.IsAliveUnity(playerSync) && playerSync.m_remotes != null)
                {
                    foreach (var kv in playerSync.m_remotes)
                    {
                        var remote = kv.Value;
                        if (!Il2CppSafe.IsAlive(remote) || !Il2CppSafe.IsAliveUnity(remote.controller)) continue;
                        string rCharId = remote.characterData?.characterId ?? remote.controller.characterData?.characterId ?? "";
                        if (string.Equals(rCharId, leaderCharacterId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals("player:" + remote.playerId, leaderCharacterId, StringComparison.OrdinalIgnoreCase))
                        {
                            playerName = !string.IsNullOrEmpty(remote.playerName) ? remote.playerName : $"Player #{remote.playerId}";
                            return remote.controller;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static string ResolvePlayerNameByCharacterId(string characterId)
        {
            ResolveLeaderController(characterId, out string name);
            return name;
        }

        public static bool IsCompanionOwnedByLocalPlayer(GOAPNPCData npcData)
        {
            if (!Il2CppSafe.IsAlive(npcData)) return false;
            if (!IsMultiplayerOnline) return true;

            string localId = GetLocalPlayerCharacterId();
            if (string.IsNullOrEmpty(localId)) return true;

            if (!string.IsNullOrEmpty(npcData.leaderCharacterGUID))
                return string.Equals(npcData.leaderCharacterGUID, localId, StringComparison.OrdinalIgnoreCase);

            if (CompanionConfigStore.CompanionStates.TryGetValue(npcData.npcGUID ?? "", out var st) &&
                !string.IsNullOrEmpty(st.OwnerCharacterId))
            {
                return string.Equals(st.OwnerCharacterId, localId, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        public static List<HumanCharacterController> GetAllAlliedPlayers()
        {
            var list = new List<HumanCharacterController>();
            try
            {
                var local = GameController.instance?.playerCharacter;
                if (Il2CppSafe.IsAliveUnity(local) && !local.deadFlag)
                    list.Add(local);

                var playerSync = NetPlayerSync.instance;
                if (Il2CppSafe.IsAliveUnity(playerSync) && playerSync.m_remotes != null)
                {
                    foreach (var kv in playerSync.m_remotes)
                    {
                        var remote = kv.Value;
                        if (Il2CppSafe.IsAlive(remote) && Il2CppSafe.IsAliveUnity(remote.controller) && !remote.controller.deadFlag)
                            list.Add(remote.controller);
                    }
                }
            }
            catch { }
            return list;
        }

        public static void TickClientPuppets()
        {
            try
            {
                var monsterSync = NetMonsterSync.instance;
                if (!Il2CppSafe.IsAliveUnity(monsterSync) || monsterSync.m_puppets == null) return;

                sbyte localZ = 0;
                if (Il2CppSafe.IsAliveUnity(MapController.instance))
                    localZ = MapController.instance.currentZLevel;

                foreach (var kv in monsterSync.m_puppets)
                {
                    var puppet = kv.Value;
                    if (!Il2CppSafe.IsAlive(puppet) || !Il2CppSafe.IsAliveUnity(puppet.controller)) continue;
                    EnsureClientPuppetVisibleAndBound(puppet.controller, localZ);
                }
            }
            catch { }
        }

        public static void EnsureClientPuppetVisibleAndBound(BasicCharacterController ctrl, sbyte localZ)
        {
            if (!Il2CppSafe.IsAliveUnity(ctrl) || ctrl.deadFlag) return;
            try
            {
                var human = ctrl.TryCast<HumanCharacterController>();
                if (!Il2CppSafe.IsAliveUnity(human)) return;

                // Ensure GOAPAgent on Client has a valid GOAPNPCData reference so UI/ally checks never NRE
                var agent = human.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(agent) && !Il2CppSafe.IsAlive(agent.npcData) && Il2CppSafe.IsAlive(human.characterData))
                {
                    var npcData = new GOAPNPCData();
                    npcData.characterData = human.characterData;
                    npcData.agentCache = agent;
                    if (!string.IsNullOrEmpty(human.characterData.characterId))
                        npcData.npcGUID = human.characterData.characterId;
                    agent.npcData = npcData;
                }

                sbyte npcZ = Il2CppSafe.IsAlive(human.characterData) ? human.characterData.zLevel : localZ;
                if (npcZ == localZ)
                {
                    NetShadowLayer.SetShadowCharacter(human, false);
                    human.HideOutsideSightFlag = false;
                    // Fix BasicCharacterController.Start() setting inSightFlag=1 after Init()->OnExitSight()
                    if (Il2CppSafe.IsAliveUnity(human.spriteParent) &&
                        !human.spriteParent.gameObject.activeSelf)
                    {
                        human.OnEnterScreen();
                        human.OnEnterSight();
                        human.spriteParent.gameObject.SetActive(true);
                    }
                }
            }
            catch { }
        }
    }
}
