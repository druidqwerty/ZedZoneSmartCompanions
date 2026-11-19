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
            if (SmartCompanionsRuntimeGate.IsAnyLoadingScreenOrTransitionActive()) return CachedContainers;

            float now = Time.unscaledTime;
            if (!force && now - _lastContainerScanTime < 1.5f && CachedContainers.Count > 0)
                return CachedContainers;

            if (force) InvalidateTerrainScanCache();
            var sw = System.Diagnostics.Stopwatch.StartNew();
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
                float maxRadius = Mathf.Clamp(CompanionConfigStore.BaseScanRadius, 16f, 80f);
                float maxSqr = maxRadius * maxRadius;
                var seenInvs = new HashSet<IntPtr>();

                var activeZLayers = new HashSet<sbyte> { playerZ };
                var garrisonAnchors = new List<(Vector2 pos, sbyte z)>();
                try
                {
                    var activeMates = SmartChatterSystem.GetActiveTeammateAgents();
                    for (int i = 0; i < activeMates.Count; i++)
                    {
                        var mate = activeMates[i];
                        if (Il2CppSafe.IsAliveUnity(mate) && Il2CppSafe.IsAlive(mate.npcData) &&
                            Il2CppSafe.IsAliveUnity(mate.humanCharacter) && !mate.humanCharacter.deadFlag)
                        {
                            activeZLayers.Add(mate.npcData.zLevel);
                        }
                    }
                    foreach (var kv in CompanionConfigStore.CompanionStates)
                    {
                        var gst = kv.Value;
                        if (gst != null && gst.RosterMode == CompanionRosterMode.CampGarrison && gst.HasHomeAnchor)
                        {
                            sbyte gz = (sbyte)gst.HomeAnchorZ;
                            activeZLayers.Add(gz);
                            garrisonAnchors.Add((gst.HomeAnchorPos, gz));
                        }
                    }
                }
                catch { }

                // 1. Scan active world TerrainObjects across current chunks, buildings, and all active teammate floors
                var worldObjs = CollectWorldTerrainObjects(playerPos, maxRadius);
                for (int i = 0; i < worldObjs.Count; i++)
                {
                    var tobj = worldObjs[i];
                    if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) continue;
                    if (!activeZLayers.Contains(tobj.objectData.zLayer)) continue;

                    var inv = ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: true);
                    if (!Il2CppSafe.IsAlive(inv) || inv.isLocked) continue;
                    if (inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0) continue;
                    if (!seenInvs.Add(inv.Pointer)) continue;

                    RegisterInventoryTerrainObject(inv, tobj);
                    Vector2 pos = tobj.transform.position;
                    AddDiscoveredContainer(inv, tobj, pos, Vector2.Distance(playerPos, pos), explicitZ: tobj.objectData.zLayer);
                }

                // 1b. Also include chests around any garrisoned companion's HomeAnchorPos when player is away on a raid
                var scannedGarrisons = new List<(Vector2 pos, sbyte z)>();
                for (int gIdx = 0; gIdx < garrisonAnchors.Count; gIdx++)
                {
                    var gar = garrisonAnchors[gIdx];
                    if (Vector2.Distance(gar.pos, playerPos) + 28f <= maxRadius)
                        continue;

                    bool alreadyScanned = false;
                    for (int sIdx = 0; sIdx < scannedGarrisons.Count; sIdx++)
                    {
                        if (scannedGarrisons[sIdx].z == gar.z && (gar.pos - scannedGarrisons[sIdx].pos).sqrMagnitude <= 64f)
                        {
                            alreadyScanned = true;
                            break;
                        }
                    }
                    if (alreadyScanned) continue;

                    scannedGarrisons.Add(gar);
                    InvalidateTerrainScanCache();
                    var garObjs = CollectWorldTerrainObjects(gar.pos, 28f);
                    for (int i = 0; i < garObjs.Count; i++)
                    {
                        var tobj = garObjs[i];
                        if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) continue;
                        if (tobj.objectData.zLayer != gar.z) continue;

                        var inv = ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: true);
                        if (!Il2CppSafe.IsAlive(inv) || inv.isLocked) continue;
                        if (inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0) continue;
                        if (!seenInvs.Add(inv.Pointer)) continue;

                        RegisterInventoryTerrainObject(inv, tobj);
                        Vector2 pos = tobj.transform.position;
                        AddDiscoveredContainer(inv, tobj, pos, Vector2.Distance(playerPos, pos), explicitZ: tobj.objectData.zLayer);
                    }
                }

                // 2. Scan deployed BasicItem containers on the ground across active floors and garrison zones
                var activeItems = BasicItem.ActiveObjects;
                if (activeItems != null)
                {
                    for (int i = 0; i < activeItems.Count; i++)
                    {
                        var bi = activeItems[i];
                        if (!Il2CppSafe.IsAliveUnity(bi) || !bi.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(bi.itemData))
                            continue;
                        if (!bi.itemData.deployedFlag) continue;
                        if (!activeZLayers.Contains(bi.itemData.zLevel)) continue;

                        Vector2 pos = bi.transform.position;
                        bool inRange = (pos - playerPos).sqrMagnitude <= maxSqr;
                        if (!inRange)
                        {
                            for (int gIdx = 0; gIdx < garrisonAnchors.Count; gIdx++)
                            {
                                if (garrisonAnchors[gIdx].z == bi.itemData.zLevel &&
                                    (pos - garrisonAnchors[gIdx].pos).sqrMagnitude <= 28f * 28f)
                                {
                                    inRange = true;
                                    break;
                                }
                            }
                        }
                        if (!inRange) continue;

                        var inv = bi.itemData.inventoryData;
                        if (!Il2CppSafe.IsAlive(inv) || inv.isLocked || inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0)
                            continue;
                        if (!seenInvs.Add(inv.Pointer)) continue;

                        KnownInvToWorldPos[inv.Pointer] = new Vector3(pos.x, pos.y, bi.itemData.zLevel);
                        inv.worldposition = pos;
                        inv.inventoryId = MultiplayerSyncSystem.BuildPositionBoxKey(pos, bi.itemData.zLevel);
                        string itemName = bi.itemData.GetItemName();
                        RegisterExternalInventoryAnchor(inv, bi.transform, itemName);
                        AddDiscoveredContainer(inv, null, pos, Vector2.Distance(playerPos, pos), itemName, bi.itemData.zLevel);
                    }
                }

                // 3. Only include LootInventoryPanel.CurrentOpenInventoryData if LootInventoryPanel is ACTUALLY open!
                // Never read LootInventoryPanel.instance (which instantiates the prefab when null); always read _instance!
                var openPanel = LootInventoryPanel._instance;
                var openInv = LootInventoryPanel.CurrentOpenInventoryData;
                if (Il2CppSafe.IsAliveUnity(openPanel) && openPanel.gameObject.activeInHierarchy &&
                    Il2CppSafe.IsAlive(openInv) && !openInv.isPlayerInventory && seenInvs.Add(openInv.Pointer))
                {
                    var openTobj = FindTerrainObjectByInventory(openInv);
                    Vector2 openPos = Il2CppSafe.IsAliveUnity(openTobj)
                        ? (Vector2)openTobj.transform.position
                        : (openInv.worldposition.sqrMagnitude > 0.001f ? openInv.worldposition : playerPos);
                    float openDist = Vector2.Distance(playerPos, openPos);
                    sbyte openZ = (Il2CppSafe.IsAliveUnity(openTobj) && Il2CppSafe.IsAlive(openTobj.objectData))
                        ? openTobj.objectData.zLayer
                        : playerZ;
                    string openName = !string.IsNullOrEmpty(openPanel.lootObjectName)
                        ? openPanel.lootObjectName
                        : (!string.IsNullOrEmpty(openInv.inventoryTitleName)
                            ? openInv.inventoryTitleName
                            : CompanionConfigStore.L("Открытый ящик", "Open Container", "当前打开的箱子"));
                    AddDiscoveredContainer(openInv, openTobj, openPos, openDist, openName, openZ);
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
            finally
            {
                sw.Stop();
                PerformanceAndDiagnostics.LastContainerScanMs = sw.Elapsed.TotalMilliseconds;
            }

            return CachedContainers;
        }

        private static readonly Dictionary<IntPtr, sbyte> KnownInvToZLayer = new Dictionary<IntPtr, sbyte>();

        internal static sbyte ResolveContainerZLayer(DiscoveredContainer c, sbyte fallbackZ = 0)
        {
            if (c == null) return fallbackZ;
            try
            {
                if (Il2CppSafe.IsAliveUnity(c.TerrainObj) && Il2CppSafe.IsAlive(c.TerrainObj.objectData))
                    return c.TerrainObj.objectData.zLayer;
                if (Il2CppSafe.IsAlive(c.Inventory) && KnownInvToZLayer.TryGetValue(c.Inventory.Pointer, out sbyte knownZ))
                    return knownZ;
            }
            catch { }
            return fallbackZ;
        }

        internal static bool IsContainerInWorkerZone(DiscoveredContainer c, GOAPAgent ag, CompanionRuntimeState st)
        {
            if (c == null || !Il2CppSafe.IsAlive(c.Inventory)) return false;
            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter))
                return true;

            sbyte workerZ = ag.npcData.zLevel;
            if (ResolveContainerZLayer(c, workerZ) != workerZ)
                return false;

            if (st != null && st.RosterMode == CompanionRosterMode.CampGarrison && st.HasHomeAnchor)
                return Vector2.Distance(c.WorldPos, st.HomeAnchorPos) <= 28.0f;

            float maxFollowRadius = Mathf.Clamp(Mathf.Min(CompanionConfigStore.BaseScanRadius, 40.0f), 16.0f, 40.0f);
            Vector2 selfPos = ag.humanCharacter.transform.position;
            if (Vector2.Distance(c.WorldPos, selfPos) > maxFollowRadius)
                return false;

            var player = GameController.instance?.playerCharacter;
            if (Il2CppSafe.IsAliveUnity(player))
            {
                Vector2 playerPos = player.transform.position;
                if (Vector2.Distance(c.WorldPos, playerPos) > maxFollowRadius)
                    return false;
            }
            return true;
        }

        internal static List<DiscoveredContainer> FilterContainersForWorker(
            List<DiscoveredContainer> all,
            GOAPAgent ag,
            CompanionRuntimeState st)
        {
            if (all == null || all.Count == 0) return all ?? new List<DiscoveredContainer>();
            var filtered = new List<DiscoveredContainer>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (IsContainerInWorkerZone(all[i], ag, st))
                    filtered.Add(all[i]);
            }
            return filtered;
        }

        private static void AddDiscoveredContainer(InventoryData inv, TerrainObject tobj, Vector2 pos, float dist, string overrideName = null, sbyte? explicitZ = null)
        {
            if (explicitZ.HasValue && Il2CppSafe.IsAlive(inv))
                KnownInvToZLayer[inv.Pointer] = explicitZ.Value;
            else if (Il2CppSafe.IsAliveUnity(tobj) && Il2CppSafe.IsAlive(tobj.objectData) && Il2CppSafe.IsAlive(inv))
                KnownInvToZLayer[inv.Pointer] = tobj.objectData.zLayer;
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

            string ruleKey = string.IsNullOrEmpty(id) ? "unknown" : id;
            bool hadConfigEntry = CompanionConfigStore.ContainerRules.ContainsKey(ruleKey);
            var rule = CompanionConfigStore.GetOrCreateRule(ruleKey, baseName);
            rule.LastKnownPos = pos;
            if (rule.IsDropoffSource || rule.Categories != StorageCategory.None)
                rule.IsMarkedByPlayer = true;

            if (Il2CppSafe.IsAliveUnity(tobj))
            {
                if (!rule.IsMarkedByPlayer && (MultiplayerSyncSystem.IsMultiplayerOnline || !hadConfigEntry))
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
