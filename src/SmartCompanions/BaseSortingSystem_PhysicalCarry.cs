using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
        public const int MaxCarryBatchPerTrip = 10;
        public static int ContainerListFilterMode = 0; // 0 = Marked + At Feet (<=2.3m, max 3 unmarked), 1 = Nearby (<=7m), 2 = All (<=18m)
        public static bool ShowAllNearbyContainersInUI
        {
            get => ContainerListFilterMode > 0;
            set => ContainerListFilterMode = value ? 2 : 0;
        }

        public static List<DiscoveredContainer> GetFilteredContainersForUI(bool forceScan = false)
        {
            var all = ScanNearbyContainers(force: forceScan);
            var result = new List<DiscoveredContainer>();
            float maxUnmarkedDist = ContainerListFilterMode == 0 ? 2.3f : (ContainerListFilterMode == 1 ? 7.0f : 18.0f);
            int maxUnmarkedCount = ContainerListFilterMode == 0 ? 3 : 999;
            int unmarkedAdded = 0;

            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c?.Rule == null) continue;
                bool isMarked = c.Rule.IsMarkedByPlayer || c.Rule.IsDropoffSource || c.Rule.Categories != StorageCategory.None;
                if (isMarked)
                {
                    result.Add(c);
                }
                else if ((c.DistanceToPlayer <= maxUnmarkedDist || i == 0) && unmarkedAdded < maxUnmarkedCount)
                {
                    result.Add(c);
                    unmarkedAdded++;
                }
            }
            return result;
        }

        public static int ClearAllContainerRules()
        {
            int cleared = 0;
            try
            {
                foreach (var c in CachedContainers)
                {
                    if (c?.Rule == null) continue;
                    if (c.Rule.IsMarkedByPlayer || c.Rule.IsDropoffSource || c.Rule.Categories != StorageCategory.None)
                    {
                        c.Rule.IsMarkedByPlayer = false;
                        c.Rule.IsDropoffSource = false;
                        c.Rule.Categories = StorageCategory.None;
                        MultiplayerSyncSystem.WriteRuleToTerrainObject(c.TerrainObj, c.Rule);
                        cleared++;
                    }
                }
                CompanionConfigStore.ContainerRules.Clear();
                CompanionConfigStore.Save();
            }
            catch { }
            return cleared;
        }

        public static List<DiscoveredContainer> ScanNearbyContainers(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && now - _lastContainerScanTime < 1.5f && CachedContainers.Count > 0)
                return CachedContainers;

            _lastContainerScanTime = now;
            CachedContainers.Clear();

            try
            {
                CompanionConfigStore.ContainerRules.Remove("unknown_box");
                CompanionConfigStore.ContainerRules.Remove("unknown");

                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter))
                    return CachedContainers;

                Vector2 playerPos = gc.playerCharacter.transform.position;
                sbyte playerZ = gc.playerCharacter.characterData != null ? gc.playerCharacter.characterData.zLevel : (sbyte)0;
                float maxRadius = Mathf.Min(32f, CompanionConfigStore.BaseScanRadius);
                float maxSqr = maxRadius * maxRadius;
                var seenInvs = new HashSet<IntPtr>();

                // 1. Scan active world TerrainObjects across current chunks, buildings, and floors
                var worldObjs = CollectWorldTerrainObjects(playerPos, maxRadius);
                for (int i = 0; i < worldObjs.Count; i++)
                {
                    var tobj = worldObjs[i];
                    if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) continue;
                    if (tobj.objectData.zLayer != playerZ) continue;

                    var inv = ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: true);
                    if (!Il2CppSafe.IsAlive(inv) || inv.isLocked) continue;
                    if (inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0) continue;
                    if (!seenInvs.Add(inv.Pointer)) continue;

                    RegisterInventoryTerrainObject(inv, tobj);
                    Vector2 pos = tobj.transform.position;
                    AddDiscoveredContainer(inv, tobj, pos, Vector2.Distance(playerPos, pos));
                }

                // 2. Scan deployed BasicItem containers on the ground
                var activeItems = BasicItem.ActiveObjects;
                if (activeItems != null)
                {
                    for (int i = 0; i < activeItems.Count; i++)
                    {
                        var bi = activeItems[i];
                        if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                            continue;
                        if (!bi.itemData.deployedFlag) continue;
                        if (bi.itemData.zLevel != playerZ) continue;

                        Vector2 pos = bi.transform.position;
                        if ((pos - playerPos).sqrMagnitude > maxSqr) continue;

                        var inv = bi.itemData.inventoryData;
                        if (!Il2CppSafe.IsAlive(inv) || inv.isLocked || inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0)
                            continue;
                        if (!seenInvs.Add(inv.Pointer)) continue;

                        KnownInvToWorldPos[inv.Pointer] = new Vector3(pos.x, pos.y, bi.itemData.zLevel);
                        inv.worldposition = pos;
                        inv.inventoryId = MultiplayerSyncSystem.BuildPositionBoxKey(pos, bi.itemData.zLevel);
                        string itemName = bi.itemData.GetItemName();
                        RegisterExternalInventoryAnchor(inv, bi.transform, itemName);
                        AddDiscoveredContainer(inv, null, pos, Vector2.Distance(playerPos, pos), itemName);
                    }
                }

                // 3. Only include LootInventoryPanel.CurrentOpenInventoryData if LootInventoryPanel is ACTUALLY open!
                var openPanel = LootInventoryPanel.instance;
                var openInv = LootInventoryPanel.CurrentOpenInventoryData;
                if (Il2CppSafe.IsAliveUnity(openPanel) && openPanel.gameObject.activeInHierarchy &&
                    Il2CppSafe.IsAlive(openInv) && !openInv.isPlayerInventory && seenInvs.Add(openInv.Pointer))
                {
                    var openTobj = FindTerrainObjectByInventory(openInv);
                    Vector2 openPos = Il2CppSafe.IsAliveUnity(openTobj)
                        ? (Vector2)openTobj.transform.position
                        : (openInv.worldposition.sqrMagnitude > 0.001f ? openInv.worldposition : playerPos);
                    float openDist = Vector2.Distance(playerPos, openPos);
                    string openName = !string.IsNullOrEmpty(openPanel.lootObjectName)
                        ? openPanel.lootObjectName
                        : (!string.IsNullOrEmpty(openInv.inventoryTitleName)
                            ? openInv.inventoryTitleName
                            : CompanionConfigStore.L("Открытый ящик", "Open Container", "当前打开的箱子"));
                    AddDiscoveredContainer(openInv, openTobj, openPos, openDist, openName);
                }

                // Sort: closest container (< 2.2m) first so standing next to a box puts it at index 0,
                // then player-marked base containers, then by distance!
                CachedContainers.Sort((a, b) =>
                {
                    bool aVeryClose = a.DistanceToPlayer <= 2.2f;
                    bool bVeryClose = b.DistanceToPlayer <= 2.2f;
                    if (aVeryClose != bVeryClose)
                        return aVeryClose ? -1 : 1;
                    if (aVeryClose && bVeryClose)
                        return a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);

                    bool aMarked = a.Rule != null && a.Rule.IsMarkedByPlayer;
                    bool bMarked = b.Rule != null && b.Rule.IsMarkedByPlayer;
                    if (aMarked != bMarked)
                        return aMarked ? -1 : 1;

                    return a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
                });
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("ScanNearbyContainers failed: " + e.Message);
            }

            return CachedContainers;
        }

        private static void AddDiscoveredContainer(InventoryData inv, TerrainObject tobj, Vector2 pos, float dist, string overrideName = null)
        {
            string id = GetContainerStableId(inv, tobj);
            string baseName = overrideName;
            if (string.IsNullOrEmpty(baseName))
            {
                if (!string.IsNullOrEmpty(inv.inventoryTitleName))
                    baseName = inv.inventoryTitleName;
                else if (Il2CppSafe.IsAliveUnity(tobj) && Il2CppSafe.IsAliveUnity(tobj.attr) && !string.IsNullOrEmpty(tobj.attr.name))
                    baseName = tobj.attr.name;
                else
                    baseName = CompanionConfigStore.L("Контейнер", "Container", "储物箱");
            }

            var rule = CompanionConfigStore.GetOrCreateRule(id, baseName);
            rule.LastKnownPos = pos;
            if (rule.IsDropoffSource || rule.Categories != StorageCategory.None)
                rule.IsMarkedByPlayer = true;

            if (Il2CppSafe.IsAliveUnity(tobj))
            {
                if (!rule.IsMarkedByPlayer)
                    MultiplayerSyncSystem.TryReadRuleFromTerrainObject(tobj, ref rule);
                else
                    MultiplayerSyncSystem.WriteRuleToTerrainObject(tobj, rule);
            }

            int itemCount = inv.itemList != null ? inv.itemList.Count : 0;
            CachedContainers.Add(new DiscoveredContainer
            {
                ContainerId = id,
                DisplayName = !string.IsNullOrEmpty(rule.CustomName) ? rule.CustomName : baseName,
                Inventory = inv,
                TerrainObj = tobj,
                WorldPos = pos,
                DistanceToPlayer = dist,
                ItemCount = itemCount,
                Rule = rule
            });
        }

        public static int AutoDetectCategoriesFromContents()
        {
            var containers = ScanNearbyContainers(force: true);
            int updated = 0;

            foreach (var c in containers)
            {
                if (c?.Rule == null || !Il2CppSafe.IsAlive(c.Inventory) || c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                    continue;
                if (c.Rule.IsDropoffSource)
                    continue;
                // Only auto-detect on chests the player marked OR chests right near the player (<= 8.0m)
                if (!c.Rule.IsMarkedByPlayer && c.DistanceToPlayer > 8.0f)
                    continue;

                StorageCategory mask = StorageCategory.None;
                for (int i = 0; i < c.Inventory.itemList.Count; i++)
                {
                    var item = c.Inventory.itemList[i];
                    if (!Il2CppSafe.IsAlive(item)) continue;
                    mask |= ItemCategorizer.ClassifyItem(item);
                }

                if (mask != StorageCategory.None)
                {
                    c.Rule.Categories = mask;
                    c.Rule.IsMarkedByPlayer = true;
                    MultiplayerSyncSystem.SyncBoxRuleChange(c.Rule, c.TerrainObj);
                    updated++;
                }
            }

            CompanionConfigStore.Save();
            SetBanner(CompanionConfigStore.L(
                $"Авто-разметка завершена: категории назначены для {updated} ближайших/отмеченных ящиков (≤8м)!",
                $"Auto-detected categories for {updated} nearby/marked chests (≤8m) based on contents!",
                $"自动识别完成：已根据箱内物品为 {updated} 个附近/已标记储物箱（≤8米）分配分类！"));
            return updated;
        }
    }
}
