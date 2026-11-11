using System;
using System.Globalization;
using LiteNetLib;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    public static partial class MultiplayerSyncSystem
    {
        public const string PacketPrefix = "\u200B[SC14]|";

        public static string BuildPositionBoxKey(Vector2 worldPos, int zLayer = 0)
        {
            int rx = Mathf.RoundToInt(worldPos.x * 10f);
            int ry = Mathf.RoundToInt(worldPos.y * 10f);
            return $"pos_{rx}_{ry}_{zLayer}";
        }

        public static void WriteRuleToTerrainObject(TerrainObject tobj, ContainerRuleProfile rule)
        {
            if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData) || rule == null) return;
            try
            {
                if (!rule.IsMarkedByPlayer || (rule.Categories == StorageCategory.None && !rule.IsDropoffSource))
                {
                    tobj.objectData.SetProperty(ContainerRulePropertyIndex, "");
                    return;
                }
                string safeName = (rule.CustomName ?? "").Replace("|", "/").Replace(";", ",");
                string payload = $"SC142:{(int)rule.Categories}:{(rule.IsDropoffSource ? 1 : 0)}:{rule.Priority}:{safeName}:{(rule.AutoCompact ? 1 : 0)}";
                tobj.objectData.SetProperty(ContainerRulePropertyIndex, payload);
            }
            catch { }
        }

        public static bool TryReadRuleFromTerrainObject(TerrainObject tobj, ref ContainerRuleProfile rule)
        {
            if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) return false;
            try
            {
                string raw = tobj.objectData.GetProperty(ContainerRulePropertyIndex);
                if (string.IsNullOrEmpty(raw)) return false;
                if (raw.StartsWith("SC14:", StringComparison.Ordinal))
                {
                    // Clear legacy v1.4.0/v1.4.1 property so old Medicine-only auto-tags don't re-populate
                    tobj.objectData.SetProperty(ContainerRulePropertyIndex, "");
                    return false;
                }
                if (!raw.StartsWith("SC142:", StringComparison.Ordinal)) return false;
                var parts = raw.Split(':');
                if (parts.Length < 4) return false;
                if (!int.TryParse(parts[1], out int catInt)) return false;
                int.TryParse(parts[2], out int dropInt);
                int.TryParse(parts[3], out int prio);
                string name = parts.Length >= 5 ? parts[4] : "";

                if (rule == null) return false;
                rule.Categories = (StorageCategory)catInt;
                rule.IsDropoffSource = dropInt != 0;
                rule.IsMarkedByPlayer = (rule.Categories != StorageCategory.None || rule.IsDropoffSource);
                rule.Priority = Mathf.Clamp(prio, 1, 5);
                if (!string.IsNullOrEmpty(name)) rule.CustomName = name;
                if (parts.Length >= 6 && int.TryParse(parts[5], out int compactInt))
                    rule.AutoCompact = compactInt != 0;
                return true;
            }
            catch { return false; }
        }

        public static void SyncBoxRuleChange(ContainerRuleProfile rule, TerrainObject tobj = null)
        {
            if (rule == null) return;
            try
            {
                if (!Il2CppSafe.IsAliveUnity(tobj))
                    tobj = BaseSortingSystem.FindTerrainObjectForRule(rule);

                if (Il2CppSafe.IsAliveUnity(tobj))
                {
                    rule.LastKnownPos = tobj.transform.position;
                    WriteRuleToTerrainObject(tobj, rule);

                    // Also mirror under position key so Host & Client always match even if replica InventoryData.inventoryId differs
                    int z = Il2CppSafe.IsAlive(tobj.objectData) ? tobj.objectData.zLayer : 0;
                    string posKey = BuildPositionBoxKey(rule.LastKnownPos, z);
                    if (!string.Equals(posKey, rule.ContainerId, StringComparison.OrdinalIgnoreCase))
                    {
                        var mirror = CompanionConfigStore.GetOrCreateRule(posKey, rule.CustomName);
                        mirror.Categories = rule.Categories;
                        mirror.IsDropoffSource = rule.IsDropoffSource;
                        mirror.IsMarkedByPlayer = rule.IsMarkedByPlayer;
                        mirror.Priority = rule.Priority;
                        mirror.LastKnownPos = rule.LastKnownPos;
                    }
                }

                if (!IsMultiplayerOnline) return;

                string safeId = (rule.ContainerId ?? "").Replace("|", "/");
                string safeName = (rule.CustomName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                if (safeName.Length > 28) safeName = safeName.Substring(0, 28);
                string px = rule.LastKnownPos.x.ToString("F1", CultureInfo.InvariantCulture);
                string py = rule.LastKnownPos.y.ToString("F1", CultureInfo.InvariantCulture);
                string body = $"BOX|{safeId}|{(int)rule.Categories}|{(rule.IsDropoffSource ? 1 : 0)}|{rule.Priority}|{px}|{py}|{safeName}|{(rule.AutoCompact ? 1 : 0)}";

                if (IsMultiplayerHost) BroadcastPacketFromHost(body);
                else if (IsMultiplayerClient) SendPacketToHost(body);
            }
            catch { }
        }

        public static void SyncCompanionDismissed(string npcGuid)
        {
            if (string.IsNullOrEmpty(npcGuid) || !IsMultiplayerOnline) return;
            try
            {
                string safeGuid = npcGuid.Replace("|", "/");
                string body = $"NPC_DISMISS|{safeGuid}";
                if (IsMultiplayerHost) BroadcastPacketFromHost(body);
                else if (IsMultiplayerClient) SendPacketToHost(body);
            }
            catch { }
        }

        public static void SyncCompanionStateChange(CompanionRuntimeState st)
        {
            if (st == null || string.IsNullOrEmpty(st.NpcGuid) || !IsMultiplayerOnline) return;
            try
            {
                string ownerId = (st.OwnerCharacterId ?? "").Replace("|", "/");
                string ownerName = (st.OwnerPlayerName ?? "").Replace("|", "/");
                if (ownerName.Length > 20) ownerName = ownerName.Substring(0, 20);
                string body = $"NPC|{st.NpcGuid}|{(int)st.Doctrine}|{ownerId}|{ownerName}";
                if (IsMultiplayerHost) BroadcastPacketFromHost(body);
                else if (IsMultiplayerClient) SendPacketToHost(body);
            }
            catch { }
        }

        public static void SendCrossPlayerHeal(HumanCharacterController targetPlayer, float healHp, bool clearWounds, string healerName)
        {
            if (!Il2CppSafe.IsAliveUnity(targetPlayer)) return;
            try
            {
                string targetCharId = targetPlayer.characterData?.characterId ?? "";
                if (string.IsNullOrEmpty(targetCharId)) return;

                string safeHealer = (healerName ?? "NPC").Replace("|", "/");
                if (safeHealer.Length > 20) safeHealer = safeHealer.Substring(0, 20);
                string hpStr = healHp.ToString("F1", CultureInfo.InvariantCulture);
                string body = $"HEAL|{targetCharId}|{hpStr}|{(clearWounds ? 1 : 0)}|{safeHealer}";

                if (IsMultiplayerHost) BroadcastPacketFromHost(body);
                else if (IsMultiplayerClient) SendPacketToHost(body);
            }
            catch { }
        }

        public static void BroadcastAllRulesAndCompanionsFromHost()
        {
            if (!IsMultiplayerHost) return;
            try
            {
                foreach (var kv in CompanionConfigStore.ContainerRules)
                {
                    var r = kv.Value;
                    if (r.Categories == StorageCategory.None && !r.IsDropoffSource) continue;
                    SyncBoxRuleChange(r, null);
                }
                foreach (var kv in CompanionConfigStore.CompanionStates)
                {
                    SyncCompanionStateChange(kv.Value);
                }
            }
            catch { }
        }

        public static void SendPacketToHost(string payloadBody)
        {
            if (string.IsNullOrEmpty(payloadBody) || !IsMultiplayerClient) return;
            try
            {
                var net = NetworkManager.instance;
                if (!Il2CppSafe.IsAliveUnity(net)) return;
                var msg = new MsgChat
                {
                    senderId = net.localPlayerId,
                    senderName = net.localPlayerName ?? "Client",
                    text = PacketPrefix + payloadBody
                };
                net.SendToHost(msg, DeliveryMethod.ReliableOrdered, 0);
            }
            catch { }
        }

        public static void BroadcastPacketFromHost(string payloadBody, NetPeer exceptPeer = null)
        {
            if (string.IsNullOrEmpty(payloadBody) || !IsMultiplayerHost) return;
            try
            {
                var net = NetworkManager.instance;
                if (!Il2CppSafe.IsAliveUnity(net)) return;
                var msg = new MsgChat
                {
                    senderId = net.localPlayerId,
                    senderName = net.localPlayerName ?? "Host",
                    text = PacketPrefix + payloadBody
                };
                net.BroadcastToJoined(msg, exceptPeer, DeliveryMethod.ReliableOrdered, 0);
            }
            catch { }
        }

        public static bool TryHandleIncomingChatPacket(MsgChat msg, NetPeer senderPeer, bool receivedOnHost)
        {
            if (!Il2CppSafe.IsAlive(msg) || string.IsNullOrEmpty(msg.text)) return false;
            if (!msg.text.StartsWith(PacketPrefix, StringComparison.Ordinal)) return false;

            try
            {
                string body = msg.text.Substring(PacketPrefix.Length);
                var parts = body.Split('|');
                if (parts.Length == 0) return true;

                string cmd = parts[0];
                if (cmd == "REQ_SYNC" && receivedOnHost)
                {
                    BroadcastAllRulesAndCompanionsFromHost();
                    return true;
                }

                if (cmd == "BOX" && parts.Length >= 7)
                {
                    string id = parts[1];
                    int.TryParse(parts[2], out int catInt);
                    int.TryParse(parts[3], out int dropInt);
                    int.TryParse(parts[4], out int prio);
                    float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float px);
                    float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float py);
                    string name = parts.Length >= 8 ? parts[7] : "";
                    bool autoCompact = true;
                    if (parts.Length >= 9 && int.TryParse(parts[8], out int compactInt))
                        autoCompact = compactInt != 0;

                    ApplyReceivedBoxRule(id, (StorageCategory)catInt, dropInt != 0, prio, new Vector2(px, py), name, autoCompact);
                    if (receivedOnHost)
                        BroadcastPacketFromHost(body, senderPeer);
                    return true;
                }

                if (cmd == "NPC_DISMISS" && parts.Length >= 2)
                {
                    string guid = parts[1];
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var st = CompanionConfigStore.GetOrCreateCompanion(guid);
                        st.OwnerCharacterId = "";
                        st.OwnerPlayerName = "";
                        st.IsDismissedOrNotInTeam = true;
                        st.IsSortingActive = false;
                        st.SortingPhase = 0;
                        CompanionConfigStore.Save();
                    }
                    if (receivedOnHost)
                        BroadcastPacketFromHost(body, senderPeer);
                    return true;
                }

                if (cmd == "NPC" && parts.Length >= 4)
                {
                    string guid = parts[1];
                    int.TryParse(parts[2], out int docInt);
                    string ownerId = parts[3];
                    string ownerName = parts.Length >= 5 ? parts[4] : "";

                    var st = CompanionConfigStore.GetOrCreateCompanion(guid);
                    st.Doctrine = (TacticalDoctrine)Mathf.Clamp(docInt, 0, 5);
                    if (!string.IsNullOrEmpty(ownerId)) st.OwnerCharacterId = ownerId;
                    if (!string.IsNullOrEmpty(ownerName)) st.OwnerPlayerName = ownerName;

                    if (receivedOnHost && GOAPNPCManager.instance?.activeNPCs != null &&
                        GOAPNPCManager.instance.activeNPCs.TryGetValue(guid, out var ag) && Il2CppSafe.IsAliveUnity(ag))
                    {
                        if (!string.IsNullOrEmpty(ownerId) && Il2CppSafe.IsAlive(ag.npcData))
                            ag.npcData.leaderCharacterGUID = ownerId;
                        SmartCombatAI.ApplyDoctrineToNpcData(ag, st);
                    }
                    CompanionConfigStore.Save();
                    if (receivedOnHost)
                        BroadcastPacketFromHost(body, senderPeer);
                    return true;
                }

                if (cmd == "HEAL" && parts.Length >= 5)
                {
                    string targetCharId = parts[1];
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float hpGain);
                    int.TryParse(parts[3], out int clearWoundsInt);
                    string healerName = parts[4];

                    ApplyReceivedCrossPlayerHeal(targetCharId, hpGain, clearWoundsInt != 0, healerName);
                    if (receivedOnHost)
                        BroadcastPacketFromHost(body, senderPeer);
                    return true;
                }

                if (cmd == "SORT" && receivedOnHost)
                {
                    BaseSortingSystem.CommandSortBase(null, null, instant: false);
                    return true;
                }

                if (cmd == "LOOT" && receivedOnHost)
                {
                    AutoLootSystem.CommandSweepNearbyLoot(null, 20f);
                    return true;
                }
            }
            catch { }
            return true;
        }

        private static void ApplyReceivedBoxRule(string id, StorageCategory cats, bool isDropoff, int prio, Vector2 pos, string name, bool autoCompact = true)
        {
            bool isMarked = isDropoff || cats != StorageCategory.None;
            var rule = CompanionConfigStore.GetOrCreateRule(id, name);
            rule.Categories = cats;
            rule.IsDropoffSource = isDropoff;
            rule.IsMarkedByPlayer = isMarked;
            rule.Priority = Mathf.Clamp(prio, 1, 5);
            rule.AutoCompact = autoCompact;
            if (pos.sqrMagnitude > 0.01f) rule.LastKnownPos = pos;
            if (!string.IsNullOrEmpty(name)) rule.CustomName = name;

            if (pos.sqrMagnitude > 0.01f)
            {
                var tobj = BaseSortingSystem.FindTerrainObjectNearPosition(pos, 1.5f);
                if (Il2CppSafe.IsAliveUnity(tobj))
                {
                    WriteRuleToTerrainObject(tobj, rule);
                    string localId = BaseSortingSystem.GetContainerStableId(tobj.objectData?.inventoryData, tobj);
                    if (!string.IsNullOrEmpty(localId) && !string.Equals(localId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        var localRule = CompanionConfigStore.GetOrCreateRule(localId, name);
                        localRule.Categories = cats;
                        localRule.IsDropoffSource = isDropoff;
                        localRule.IsMarkedByPlayer = isMarked;
                        localRule.Priority = rule.Priority;
                        localRule.LastKnownPos = pos;
                        if (!string.IsNullOrEmpty(name)) localRule.CustomName = name;
                    }
                }
            }
            CompanionConfigStore.Save();
        }

        private static void ApplyReceivedCrossPlayerHeal(string targetCharId, float hpGain, bool clearWounds, string healerName)
        {
            string localId = GetLocalPlayerCharacterId();
            if (!string.Equals(localId, targetCharId, StringComparison.OrdinalIgnoreCase)) return;

            var player = GameController.instance?.playerCharacter;
            if (!Il2CppSafe.IsAliveUnity(player) || player.deadFlag) return;
            var status = player.characterData?.characterStatusData;
            if (!Il2CppSafe.IsAlive(status)) return;

            float maxHp = status.maxhp > 0f ? status.maxhp : 100f;
            status.hp = Mathf.Min(maxHp, status.hp + Mathf.Max(5f, hpGain));
            player.hp = status.hp;
            if (clearWounds)
            {
                try { player.HealAllBodyPartInjuries(); } catch { }
            }
            BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                $"💉 Напарник союзника ({healerName}) перевязал вас (+{Mathf.RoundToInt(hpGain)} HP)!",
                $"💉 Allied companion ({healerName}) patched you up (+{Mathf.RoundToInt(hpGain)} HP)!",
                $"💉 盟友的队友（{healerName}）为你进行了包扎急救（+{Mathf.RoundToInt(hpGain)} HP）！"));
        }
    }
}
