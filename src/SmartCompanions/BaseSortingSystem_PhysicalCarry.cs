using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class BaseSortingSystem
    {
        public const int MaxCarryBatchPerTrip = 10;

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
                float maxRadius = CompanionConfigStore.BaseScanRadius;
                float maxSqr = maxRadius * maxRadius;
                var seenInvs = new HashSet<IntPtr>();

                // 1. Scan all active world TerrainObjects across current chunks, buildings, and floors
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

                // 2. Scan deployed BasicItem containers on the ground (e.g., deployed storage boxes / backpacks)
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
                        AddDiscoveredContainer(inv, null, pos, Vector2.Distance(playerPos, pos), itemName);
                    }
                }

                // 3. Only include LootInventoryPanel.CurrentOpenInventoryData if the LootInventoryPanel is ACTUALLY open!
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

                CachedContainers.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
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
            if (Il2CppSafe.IsAliveUnity(tobj))
            {
                if (rule.Categories == StorageCategory.None && !rule.IsDropoffSource)
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
                if (!Il2CppSafe.IsAlive(c.Inventory) || c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                    continue;
                if (c.Rule.IsDropoffSource)
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
                    MultiplayerSyncSystem.SyncBoxRuleChange(c.Rule, c.TerrainObj);
                    updated++;
                }
            }

            CompanionConfigStore.Save();
            SetBanner(CompanionConfigStore.L(
                $"Авто-разметка завершена: категории назначены для {updated} ящиков по их содержимому!",
                $"Auto-detected categories for {updated} containers based on contents!",
                $"自动识别完成：已根据箱内物品为 {updated} 个储物箱分配分类！"));
            return updated;
        }

        public static void CommandSortBase(GOAPAgent specificAgent = null, InventoryData extraSourceContainer = null, bool instant = false)
        {
            if (MultiplayerSyncSystem.IsMultiplayerClient)
            {
                MultiplayerSyncSystem.SendPacketToHost("SORT|1");
            }
            var containers = ScanNearbyContainers(force: true);

            var destBoxes = GetCategorizedDestinationBoxes(containers);
            if (destBoxes.Count == 0)
            {
                AutoDetectCategoriesFromContents();
                destBoxes = GetCategorizedDestinationBoxes(containers);
            }

            if (destBoxes.Count == 0)
            {
                SetBanner(CompanionConfigStore.L(
                    "Нет размеченных ящиков! Откройте ящик или нажмите F8 и отметьте категории.",
                    "No categorized containers found! Open a chest or press F8 and select categories first.",
                    "未找到已分类的储物箱！请先打开箱子或按 F8 勾选分类。"));
                return;
            }

            var workers = new List<GOAPAgent>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                workers.Add(specificAgent);
            else
                workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

            if (instant || workers.Count == 0)
            {
                int moved = ExecuteInstantSortPass(workers, containers, destBoxes, extraSourceContainer, out var dominantCategory);
                var speaker = workers.Count > 0 ? workers[0] : null;
                if (Il2CppSafe.IsAliveUnity(speaker))
                {
                    if (moved > 0)
                    {
                        var stSpk = CompanionConfigStore.GetOrCreateCompanion(speaker.npcData?.npcGUID ?? "npc");
                        stSpk.SortedItemsThisSession += moved;
                        stSpk.AddExperience(Mathf.Clamp(moved * 2, 2, 40), speaker);
                        SpeakCategoryDeposit(speaker, dominantCategory);
                    }
                    SmartChatterSystem.Speak(speaker, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 6f, force: true);
                }
                SetBanner(CompanionConfigStore.L(
                    $"Мгновенная сортировка выполнена: перемещено предметов — {moved} шт.",
                    $"Instant sorting complete: {moved} item stacks sorted into category chests.",
                    $"一键整理完成：已将 {moved} 组物品按类别归入储物箱。"));
                return;
            }

            int assignedWorkers = 0;
            var claimedSourcePointers = new HashSet<IntPtr>();
            foreach (var ag in workers)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                FlushCarryBufferSafely(st);

                if (TryAssignNextCarrySource(ag, st, containers, destBoxes, extraSourceContainer, claimedSourcePointers))
                {
                    assignedWorkers++;
                    SmartChatterSystem.Speak(ag, ChatterTopic.SortStart, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
                }
            }

            if (assignedWorkers > 0)
            {
                SetBanner(CompanionConfigStore.L(
                    $"📦 Напарники ({assignedWorkers} чел.) пошли переносить предметы по ящикам базы!",
                    $"📦 Companions ({assignedWorkers}) started carrying items between base chests!",
                    $"📦 队友（{assignedWorkers} 人）已开始在基地储物箱之间搬运分拣物品！"));
            }
            else
            {
                SetBanner(CompanionConfigStore.L(
                    "Все подходящие предметы в ящиках уже разложены по своим категориям!",
                    "All matching items in base containers are already sorted into their categories!",
                    "基地储物箱中的所有匹配物品均已按分类归位！"));
            }
        }

        private static List<DiscoveredContainer> GetCategorizedDestinationBoxes(List<DiscoveredContainer> containers)
        {
            var destBoxes = new List<DiscoveredContainer>();
            foreach (var c in containers)
            {
                if (c?.Rule != null && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource && Il2CppSafe.IsAlive(c.Inventory))
                    destBoxes.Add(c);
            }
            destBoxes.Sort((a, b) =>
            {
                int cmp = b.Rule.Priority.CompareTo(a.Rule.Priority);
                return cmp != 0 ? cmp : a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
            });
            return destBoxes;
        }
    }
}
