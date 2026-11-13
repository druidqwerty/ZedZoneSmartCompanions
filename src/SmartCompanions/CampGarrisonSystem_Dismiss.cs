using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class CampGarrisonSystem
    {
        public static bool StartSafeDismiss(GOAPAgent ag, CompanionRuntimeState st, bool handOverGear, bool quiet = false)
        {
            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || st == null)
                return false;
            if (MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                return false;

            PendingDismissConfirmGuid = "";
            if (st.IsSortingActive || st.IsLootingActive || st.IsChoreWalkActive)
                BaseWorkManager.CancelWork(ag, st);

            if (!handOverGear)
            {
                BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);
                CompleteDismissal(ag, st, 0, quiet);
                return true;
            }

            Vector2 selfPos = Il2CppSafe.IsAliveUnity(ag.humanCharacter)
                ? (Vector2)ag.humanCharacter.transform.position
                : Vector2.zero;

            // Find nearest marked drop-off or storage chest within 28m on the same zLevel; fallback to local player
            InventoryData targetInv = null;
            Transform targetAnchor = null;
            Vector2 targetPos = selfPos;
            bool toPlayer = false;
            float bestDist = 28.0f;

            var containers = BaseSortingSystem.ScanNearbyContainers(force: true);
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c?.Rule == null || !c.Rule.IsMarkedByPlayer || !Il2CppSafe.IsAlive(c.Inventory))
                    continue;
                if (PhysicalMovementHelper.IsTargetTemporarilyUnreachable(c.ContainerId))
                    continue;
                float d = Vector2.Distance(selfPos, c.WorldPos);
                if (c.Rule.IsDropoffSource) d -= 3.5f;
                if (d < bestDist)
                {
                    bestDist = d;
                    targetInv = c.Inventory;
                    targetAnchor = Il2CppSafe.IsAliveUnity(c.TerrainObj)
                        ? c.TerrainObj.transform
                        : BaseSortingSystem.ResolveAnchorTransformForInventory(c.Inventory);
                    targetPos = c.WorldPos;
                }
            }

            if (!Il2CppSafe.IsAlive(targetInv))
            {
                var player = GameController.instance?.playerCharacter;
                if (Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData?.inventoryData))
                {
                    targetInv = player.characterData.inventoryData;
                    targetAnchor = player.transform;
                    targetPos = player.transform.position;
                    toPlayer = true;
                }
            }

            if (!Il2CppSafe.IsAlive(targetInv))
            {
                st.NoteBlocked(CompanionBlockReason.NoMarkedBoxes);
                return false;
            }

            st.IsSafeDismissActive = true;
            st.SafeDismissReturnGear = true;
            st.SafeDismissTargetInv = targetInv;
            st.SafeDismissTargetAnchor = targetAnchor;
            st.SafeDismissTargetPos = targetPos;
            st.SafeDismissToPlayer = toPlayer;
            PhysicalMovementHelper.BeginLeg(st, selfPos, targetPos, targetAnchor);
            st.CurrentTaskText = CompanionConfigStore.L(
                "Идёт сдать снаряжение перед уходом...",
                "Walking to hand over gear before leaving...",
                "正前往交出装备后离队...");
            return true;
        }

        public static void StepSafeDismissForAgent(GOAPAgent ag, CompanionRuntimeState st)
        {
            if (st == null || !st.IsSafeDismissActive) return;
            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || !Il2CppSafe.IsAlive(ag.npcData))
            {
                st.IsSafeDismissActive = false;
                return;
            }

            if (Il2CppSafe.IsAliveUnity(st.SafeDismissTargetAnchor))
                st.SafeDismissTargetPos = st.SafeDismissTargetAnchor.position;

            PhysicalMovementHelper.StepWalkAndInteract(
                ag, st, st.SafeDismissTargetPos, st.SafeDismissTargetAnchor, 0, 0.40f,
                out bool ready, out bool unreachable);

            if (unreachable)
            {
                st.IsSafeDismissActive = false;
                st.NoteBlocked(CompanionBlockReason.PathBlocked);
                st.CurrentTaskText = "";
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                    $"{st.LastKnownName}: нет пути для сдачи снаряжения — отпуск отменён, вещи сохранены.",
                    $"{st.LastKnownName}: cannot reach chest/player to hand over gear — dismissal cancelled.",
                    $"{st.LastKnownName}：无法到达交接点，已取消离队并保留装备。"), 5.5f);
                return;
            }

            if (!ready) return;

            int handedOver = HandOverAllGearAndInventoryAtTarget(ag, st, st.SafeDismissTargetInv);
            st.IsSafeDismissActive = false;
            CompleteDismissal(ag, st, handedOver, quiet: false);
        }

        public static int HandOverAllGearAndInventoryAtTarget(GOAPAgent ag, CompanionRuntimeState st, InventoryData destInv)
        {
            if (!Il2CppSafe.IsAlive(destInv) || !Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData))
                return 0;

            int moved = 0;
            try
            {
                // 1. CarryBuffer items already in companion's hands
                for (int i = st.CarryBuffer.Count - 1; i >= 0; i--)
                {
                    var ce = st.CarryBuffer[i];
                    if (ce != null && Il2CppSafe.IsAlive(ce.Item) && InventoryTransfers.TryDepositOrStackWhole(destInv, ce.Item))
                    {
                        st.CarryBuffer.RemoveAt(i);
                        moved++;
                    }
                }
                BaseSortingSystem.ReturnCarryBufferWithoutTeleport(ag, st);

                // 2. Shared & personal inventories (atomic: remove from source only after deposit succeeds)
                moved += TransferAllPossibleItems(ag.npcData.sharedInventoryData, destInv);
                moved += TransferAllPossibleItems(ag.npcData.characterData?.inventoryData, destInv);

                // 3. Unequip weapons & armor safely without leaving dangling weaponAttr/IK references
                var hc = ag.humanCharacter;
                var eq = ag.npcData.characterData?.characterEquipmentData;
                if (Il2CppSafe.IsAlive(eq))
                {
                    for (int s = 0; s < 3; s++)
                    {
                        var wd = eq.GetWeaponData(s);
                        if (Il2CppSafe.IsAlive(wd) && InventoryTransfers.TryDepositOrStackWhole(destInv, wd.CopyItemData(null)))
                        {
                            eq.SetWeaponData(null, s);
                            moved++;
                        }
                    }
                    eq.currentWeaponItemData = null;
                    if (TryMoveSlot(eq.hatItemData, destInv)) { eq.hatItemData = null; moved++; }
                    if (TryMoveSlot(eq.faceMaskItemData, destInv)) { eq.faceMaskItemData = null; moved++; }
                    if (TryMoveSlot(eq.torseItemData, destInv)) { eq.torseItemData = null; moved++; }
                    if (TryMoveSlot(eq.legItemData, destInv)) { eq.legItemData = null; moved++; }
                    if (TryMoveSlot(eq.feetItemData, destInv)) { eq.feetItemData = null; moved++; }
                    if (TryMoveSlot(eq.cloakItemData, destInv)) { eq.cloakItemData = null; moved++; }
                    if (Il2CppSafe.IsAliveUnity(hc))
                    {
                        HumanNpcEquipmentGuard.CleanupBrokenRangedState(hc);
                        try { hc.RefreshAllEquippedItemVisuals(); } catch { }
                        if (MultiplayerSyncSystem.IsMultiplayerHost && Il2CppSafe.IsAliveUnity(ZedZone.Net.NetMonsterSync.instance))
                            try { ZedZone.Net.NetMonsterSync.instance.HostSendEquipment(hc); } catch { }
                    }
                }
                BaseSortingSystem.RefreshOpenLootPanelIfNeeded();
            }
            catch { }
            return moved;
        }

        private static bool TryMoveSlot(ItemData slotItem, InventoryData destInv)
        {
            if (!Il2CppSafe.IsAlive(slotItem) || !Il2CppSafe.IsAlive(destInv)) return false;
            return InventoryTransfers.TryDepositOrStackWhole(destInv, slotItem.CopyItemData(null));
        }

        private static int TransferAllPossibleItems(InventoryData srcInv, InventoryData destInv)
        {
            if (!Il2CppSafe.IsAlive(srcInv) || !Il2CppSafe.IsAlive(destInv) || srcInv.itemList == null || srcInv.itemList.Count == 0)
                return 0;

            int count = 0;
            var snap = new List<ItemData>(srcInv.itemList.Count);
            for (int i = 0; i < srcInv.itemList.Count; i++)
            {
                var it = srcInv.itemList[i];
                if (Il2CppSafe.IsAlive(it)) snap.Add(it);
            }
            for (int i = 0; i < snap.Count; i++)
            {
                var it = snap[i];
                if (!Il2CppSafe.IsAlive(it)) continue;
                var copy = it.CopyItemData(null);
                if (InventoryTransfers.TryDepositOrStackWhole(destInv, copy))
                {
                    srcInv.RemoveItem(it, true);
                    count++;
                }
            }
            return count;
        }

        private static void CompleteDismissal(GOAPAgent ag, CompanionRuntimeState st, int handedOverCount, bool quiet)
        {
            try
            {
                var npcData = ag?.npcData;
                st.RosterMode = CompanionRosterMode.ActiveSquad;
                st.HasHomeAnchor = false;
                st.CurrentTaskText = "";

                if (Il2CppSafe.IsAlive(npcData))
                {
                    // 1. Run native OneTime.QuitTeam FIRST while leaderCharacterGUID is still bound to the player!
                    try { npcData.ExecuteNPCCommand("OneTime.QuitTeam"); } catch { }

                    var player = GameController.instance?.playerCharacter;
                    if (Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData))
                    {
                        try { player.characterData.RemoveTeamNPC(npcData); } catch { }
                        try { player.characterData.teamNPCGuids?.Remove(npcData.npcGUID); } catch { }
                        try { player.characterData.RefreshTeamData(); } catch { }
                        try { player.characterData.SyncTeamUI(); } catch { }
                    }

                    if (Il2CppSafe.IsAliveUnity(ag))
                    {
                        try { ag.SetLeader(null); } catch { }
                        try { ag.DisableWithLeaderGoals(); } catch { }
                        try { ag.SetFollowLeaderColliderSuppressed(false); } catch { }
                    }

                    try { GOAPNPCManager.instance?.teammateNPCs?.Remove(npcData); } catch { }
                    MultiplayerSyncSystem.OnCompanionDismissed(npcData);
                }

                if (!quiet)
                {
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        handedOverCount > 0
                            ? $"{st.LastKnownName} сдал {handedOverCount} предм. и покинул отряд."
                            : $"{st.LastKnownName} покинул отряд.",
                        handedOverCount > 0
                            ? $"{st.LastKnownName} handed over {handedOverCount} item(s) and left the squad."
                            : $"{st.LastKnownName} left the squad.",
                        handedOverCount > 0
                            ? $"{st.LastKnownName} 已交出 {handedOverCount} 件装备并离开队伍。"
                            : $"{st.LastKnownName} 已离开队伍。"), 5.5f);
                }
            }
            catch { }
        }
    }
}
