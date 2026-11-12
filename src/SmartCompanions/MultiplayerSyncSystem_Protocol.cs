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

        public static void SyncCompanionStateChange(CompanionRuntimeState st, bool bumpRevision = true)
        {
            if (st == null || string.IsNullOrEmpty(st.NpcGuid) || !IsMultiplayerOnline) return;
            try
            {
                string ownerId = (st.OwnerCharacterId ?? "").Replace("|", "/");
                string ownerName = (st.OwnerPlayerName ?? "").Replace("|", "/");
                if (ownerName.Length > 20) ownerName = ownerName.Substring(0, 20);
                // Local changes bump the revision (host: authoritative; client: optimistic proposal).
                // Echoes, rejections and full re-syncs repeat the current revision unchanged.
                if (bumpRevision) st.StateRevision++;
                int rev = Math.Max(1, st.StateRevision);
                string sender = (GetLocalPlayerCharacterId() ?? "").Replace("|", "/");
                string ax = st.OrderAnchorPos.x.ToString("F1", CultureInfo.InvariantCulture);
                string ay = st.OrderAnchorPos.y.ToString("F1", CultureInfo.InvariantCulture);
                string dx = st.OrderDirection.x.ToString("F2", CultureInfo.InvariantCulture);
                string dy = st.OrderDirection.y.ToString("F2", CultureInfo.InvariantCulture);
                string body = $"NPC|{st.NpcGuid}|{(int)st.Doctrine}|{ownerId}|{ownerName}|{rev}|{sender}|{(int)st.WorkRole}|{(int)st.ActiveOrder}|{ax}|{ay}|{st.OrderAnchorZ}|{dx}|{dy}|{(int)st.Specialization}|{(int)st.Loadout}|{st.ReserveAmmoMin}|{st.ReserveFoodMin}|{st.ReserveMedMin}";
                if (IsMultiplayerHost) BroadcastPacketFromHost(body);
                else if (IsMultiplayerClient) SendPacketToHost(body);
            }
            catch { }
        }

        public static void SyncBaseWorkPause(bool paused)
        {
            if (!IsMultiplayerOnline) return;
            string body = "PAUSE|" + (paused ? "1" : "0");
            if (IsMultiplayerHost) BroadcastPacketFromHost(body);
            else if (IsMultiplayerClient) SendPacketToHost(body);
        }

        public static void SyncSquadTacticsConfig()
        {
            if (!IsMultiplayerOnline) return;
            string body = $"TACTICS|{(int)CompanionConfigStore.ActiveFormation}|{(int)CompanionConfigStore.FollowDistance}";
            if (IsMultiplayerHost) BroadcastPacketFromHost(body);
            else if (IsMultiplayerClient) SendPacketToHost(body);
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
                BroadcastPacketFromHost("PAUSE|" + (CompanionConfigStore.BaseWorkPaused ? "1" : "0"));
                BroadcastPacketFromHost($"TACTICS|{(int)CompanionConfigStore.ActiveFormation}|{(int)CompanionConfigStore.FollowDistance}");
                foreach (var kv in CompanionConfigStore.CompanionStates)
                {
                    SyncCompanionStateChange(kv.Value, bumpRevision: false);
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
                    int packetRev = 0;
                    if (parts.Length >= 6) int.TryParse(parts[5], out packetRev);
                    string senderCharId = parts.Length >= 7 ? parts[6] : "";
                    int roleInt = -1;
                    if (parts.Length >= 8 && !int.TryParse(parts[7], out roleInt)) roleInt = -1;
                    int orderInt = -1;
                    if (parts.Length >= 9 && !int.TryParse(parts[8], out orderInt)) orderInt = -1;

                    var st = CompanionConfigStore.GetOrCreateCompanion(guid);
                    if (receivedOnHost)
                    {
                        var decision = DecideCompanionChange(st.OwnerCharacterId, senderCharId, ownerId, st.StateRevision, packetRev);
                        if (decision != CompanionChangeDecision.Accept)
                        {
                            NoteRejectedChange(st, decision);
                            return true;
                        }
                        st.StateRevision = Math.Max(st.StateRevision + 1, packetRev);
                    }
                    else if (packetRev > 0)
                    {
                        // The host is authoritative; never step back below our own optimistic proposal.
                        st.StateRevision = Math.Max(st.StateRevision, packetRev);
                    }
                    st.Doctrine = (TacticalDoctrine)Mathf.Clamp(docInt, 0, 5);
                    if (roleInt >= 0) st.WorkRole = (CompanionWorkRole)Mathf.Clamp(roleInt, 0, 4);
                    if (orderInt >= 0)
                    {
                        var nextOrder = (TacticalOrderKind)Mathf.Clamp(orderInt, 0, 3);
                        if (st.ActiveOrder != nextOrder) st.FallBackArrived = false;
                        st.ActiveOrder = nextOrder;
                        st.OrderIssuedTime = Time.unscaledTime;
                        st.LastOrderMoveProgressTime = Time.unscaledTime;
                        if (parts.Length >= 11 &&
                            float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax) &&
                            float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay))
                        {
                            st.OrderAnchorPos = new Vector2(ax, ay);
                        }
                        if (parts.Length >= 12 && int.TryParse(parts[11], out int az))
                            st.OrderAnchorZ = az;
                        if (parts.Length >= 14 &&
                            float.TryParse(parts[12], NumberStyles.Float, CultureInfo.InvariantCulture, out float dx) &&
                            float.TryParse(parts[13], NumberStyles.Float, CultureInfo.InvariantCulture, out float dy))
                        {
                            Vector2 dir = new Vector2(dx, dy);
                            if (dir.sqrMagnitude > 0.001f) st.OrderDirection = dir.normalized;
                        }
                    }
                    if (parts.Length >= 15 && int.TryParse(parts[14], out int specInt))
                        st.Specialization = (CompanionSpecialization)Mathf.Clamp(specInt, 0, 4);
                    if (parts.Length >= 16 && int.TryParse(parts[15], out int loadInt))
                        st.Loadout = (PreferredLoadout)Mathf.Clamp(loadInt, 0, 2);
                    if (parts.Length >= 19 &&
                        int.TryParse(parts[16], out int rAmmo) &&
                        int.TryParse(parts[17], out int rFood) &&
                        int.TryParse(parts[18], out int rMed))
                    {
                        st.ReserveAmmoMin = Mathf.Clamp(rAmmo, 0, 200);
                        st.ReserveFoodMin = Mathf.Clamp(rFood, 0, 20);
                        st.ReserveMedMin = Mathf.Clamp(rMed, 0, 20);
                    }
                    if (!string.IsNullOrEmpty(ownerId)) st.OwnerCharacterId = ownerId;
                    if (!string.IsNullOrEmpty(ownerName)) st.OwnerPlayerName = ownerName;

                    if (receivedOnHost && GOAPNPCManager.instance?.activeNPCs != null &&
                        GOAPNPCManager.instance.activeNPCs.TryGetValue(guid, out var ag) && Il2CppSafe.IsAliveUnity(ag))
                    {
                        if (!string.IsNullOrEmpty(ownerId) && Il2CppSafe.IsAlive(ag.npcData))
                            ag.npcData.leaderCharacterGUID = ownerId;
                        CompanionProfileSystem.ApplyPreferredLoadoutAndDoctrine(ag, st);
                    }
                    CompanionConfigStore.Save();
                    if (receivedOnHost)
                        SyncCompanionStateChange(st, bumpRevision: false); // echo the host-stamped revision to everyone, including the sender
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

                if (cmd == "PAUSE" && parts.Length >= 2)
                {
                    BaseWorkManager.SetPaused(parts[1] == "1", fromNetwork: true);
                    if (receivedOnHost)
                        BroadcastPacketFromHost(body, senderPeer);
                    return true;
                }

                if (cmd == "TACTICS" && parts.Length >= 3)
                {
                    if (int.TryParse(parts[1], out int formInt))
                        CompanionConfigStore.ActiveFormation = (SquadFormation)Mathf.Clamp(formInt, 0, 4);
                    if (int.TryParse(parts[2], out int distInt))
                        CompanionConfigStore.FollowDistance = (FollowDistanceMode)Mathf.Clamp(distInt, 0, 3);
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
