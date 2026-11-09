using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public class DiscoveredContainer
    {
        public string ContainerId;
        public string DisplayName;
        public InventoryData Inventory;
        public TerrainObject TerrainObj;
        public Vector2 WorldPos;
        public float DistanceToPlayer;
        public int ItemCount;
        public ContainerRuleProfile Rule;
    }

    public static class BaseSortingSystem
    {
        public static readonly List<DiscoveredContainer> CachedContainers = new List<DiscoveredContainer>();
        private static float _lastContainerScanTime = -999f;
        public static string LastSortStatusBanner = "";
        public static float LastSortStatusBannerUntil = -1f;

        public static string GetContainerStableId(InventoryData inv, TerrainObject tobj = null)
        {
            if (inv != null && !string.IsNullOrEmpty(inv.inventoryId))
                return inv.inventoryId;
            if (tobj != null && tobj.objectData != null && !string.IsNullOrEmpty(tobj.objectData.terrainObejctGUID))
                return tobj.objectData.terrainObejctGUID;
            if (tobj != null)
                return $"pos_{Mathf.RoundToInt(tobj.transform.position.x * 10f)}_{Mathf.RoundToInt(tobj.transform.position.y * 10f)}";
            return "unknown_box";
        }

        public static List<DiscoveredContainer> ScanNearbyContainers(bool force = false)
        {
            float now = Time.unscaledTime;
            if (!force && now - _lastContainerScanTime < 2.0f && CachedContainers.Count > 0)
                return CachedContainers;

            _lastContainerScanTime = now;
            CachedContainers.Clear();

            try
            {
                var gc = GameController.instance;
                if (gc == null || gc.playerCharacter == null) return CachedContainers;

                Vector2 playerPos = gc.playerCharacter.transform.position;
                float maxRadius = CompanionConfigStore.BaseScanRadius;
                float maxSqr = maxRadius * maxRadius;
                var seenInvs = new HashSet<IntPtr>();

                // 1. Scan active TerrainObjects under terrainObjectsParent
                var parent = gc.terrainObjectsParent;
                if (parent != null)
                {
                    int childCount = parent.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        var tr = parent.GetChild(i);
                        if (tr == null || !tr.gameObject.activeInHierarchy) continue;

                        Vector2 pos = tr.position;
                        float sqr = (pos - playerPos).sqrMagnitude;
                        if (sqr > maxSqr) continue;

                        var tobj = tr.GetComponent<TerrainObject>();
                        if (tobj == null || tobj.objectData == null) continue;

                        // Skip corpses and loot piles unless player-built or furniture/production container
                        if (tobj.TryCast<TerrainObject_Corpse>() != null) continue;

                        var inv = tobj.objectData.inventoryData;
                        if (inv == null || inv.isLocked) continue;
                        if (inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0) continue;
                        if (!seenInvs.Add(inv.Pointer)) continue;

                        AddDiscoveredContainer(inv, tobj, pos, Mathf.Sqrt(sqr));
                    }
                }

                // 2. Also include currently open LootInventoryPanel container if not already in list
                var openPanel = LootInventoryPanel.instance;
                var openInv = LootInventoryPanel.CurrentOpenInventoryData;
                if (openInv != null && !openInv.isPlayerInventory && seenInvs.Add(openInv.Pointer))
                {
                    string openName = (openPanel != null && !string.IsNullOrEmpty(openPanel.lootObjectName))
                        ? openPanel.lootObjectName
                        : (!string.IsNullOrEmpty(openInv.inventoryTitleName) ? openInv.inventoryTitleName : CompanionConfigStore.L("Открытый ящик", "Open Container", "当前打开的箱子"));
                    AddDiscoveredContainer(openInv, null, playerPos, 0f, openName);
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
                else if (tobj != null && tobj.attr != null && !string.IsNullOrEmpty(tobj.attr.name))
                    baseName = tobj.attr.name;
                else
                    baseName = CompanionConfigStore.L("Контейнер", "Container", "储物箱");
            }

            var rule = CompanionConfigStore.GetOrCreateRule(id, baseName);
            rule.LastKnownPos = pos;

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
                if (c.Inventory == null || c.Inventory.itemList == null || c.Inventory.itemList.Count == 0)
                    continue;
                if (c.Rule.IsDropoffSource)
                    continue;

                StorageCategory mask = StorageCategory.None;
                for (int i = 0; i < c.Inventory.itemList.Count; i++)
                {
                    var item = c.Inventory.itemList[i];
                    if (item == null) continue;
                    mask |= ItemCategorizer.ClassifyItem(item);
                }

                if (mask != StorageCategory.None)
                {
                    c.Rule.Categories = mask;
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
            var containers = ScanNearbyContainers(force: true);
            bool ru = CompanionConfigStore.UseRussian;

            // Count how many categorized destination chests exist
            var destBoxes = new List<DiscoveredContainer>();
            foreach (var c in containers)
            {
                if (c.Rule != null && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
                    destBoxes.Add(c);
            }

            if (destBoxes.Count == 0)
            {
                // Auto-detect if player hasn't set any categories yet!
                AutoDetectCategoriesFromContents();
                foreach (var c in containers)
                {
                    if (c.Rule != null && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
                        destBoxes.Add(c);
                }
            }

            if (destBoxes.Count == 0)
            {
                SetBanner(CompanionConfigStore.L(
                    "Нет размеченных ящиков! Откройте ящик и отметьте категории (Оружие, Еда, Медицина и т.д.).",
                    "No categorized containers found! Open a chest and select its categories first.",
                    "未找到已分类的储物箱！请先打开任意箱子并勾选分类（武器、食物、医疗等）。"));
                return;
            }

            var workers = new List<GOAPAgent>();
            if (specificAgent != null)
            {
                workers.Add(specificAgent);
            }
            else
            {
                workers.AddRange(SmartChatterSystem.GetActiveTeammateAgents());
            }

            if (instant || workers.Count == 0)
            {
                int moved = ExecuteSortPass(workers, containers, destBoxes, extraSourceContainer, out var dominantCategory);
                var speaker = workers.Count > 0 ? workers[0] : null;
                if (speaker != null)
                {
                    if (moved > 0)
                    {
                        SpeakCategoryDeposit(speaker, dominantCategory);
                        SmartChatterSystem.Speak(speaker, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 6f, force: true);
                    }
                    else
                    {
                        SmartChatterSystem.Speak(speaker, ChatterTopic.SortFinished, minCooldown: 1f, bubbleDuration: 5f, force: true);
                    }
                }
                SetBanner(CompanionConfigStore.L(
                    $"Сортировка базы выполнена: перемещено и уложено предметов — {moved} шт.",
                    $"Base sorting complete: {moved} item stacks sorted into category chests.",
                    $"基地整理完成：已将 {moved} 组物品按类别归入储物箱。"));
                return;
            }

            // Start physical + smart sorting job for active companions!
            foreach (var ag in workers)
            {
                if (ag == null || ag.npcData == null) continue;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                st.IsSortingActive = true;
                st.SortingPhase = 1;
                st.SortingStateStartTime = Time.unscaledTime;
                st.CurrentTaskText = CompanionConfigStore.L("Сортировка ящиков базы...", "Sorting base containers...", "正在整理基地储物箱...");

                // Pick closest destination container to walk toward for visual immersion
                var targetBox = destBoxes[UnityEngine.Random.Range(0, destBoxes.Count)];
                st.TargetContainerGuid = targetBox.ContainerId;
                st.TargetContainerPos = targetBox.WorldPos;

                SmartChatterSystem.Speak(ag, ChatterTopic.SortStart, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
            }

            // Also perform first immediate pass if extraSourceContainer was explicitly requested
            if (extraSourceContainer != null)
            {
                int immediateMoved = ExecuteSortPass(workers, containers, destBoxes, extraSourceContainer, out _);
                if (immediateMoved > 0 && workers.Count > 0)
                {
                    var st0 = CompanionConfigStore.GetOrCreateCompanion(workers[0].npcData.npcGUID);
                    st0.SortedItemsThisSession += immediateMoved;
                }
            }
        }

        public static void TickActiveSortingJobs()
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            float now = Time.unscaledTime;
            bool ru = CompanionConfigStore.UseRussian;

            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);

                // Automatic Base Drop-off when returning to a base drop-off box out of combat!
                if (!st.IsSortingActive && CompanionConfigStore.EnableBaseAutoDropoff && st.AllowAutoDropoffAtBase &&
                    !Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && now - st.LastAutoDropoffTime > 25f)
                {
                    TryAutoDropoffAtBase(ag, st, now);
                }

                if (!st.IsSortingActive) continue;

                // Abort sorting if companion enters combat
                if (ag.mostThreateningCharacter != null && !ag.mostThreateningCharacter.deadFlag)
                {
                    st.IsSortingActive = false;
                    st.SortingPhase = 0;
                    continue;
                }

                float elapsed = now - st.SortingStateStartTime;
                Vector2 selfPos = ag.humanCharacter.transform.position;
                float distToBox = Vector2.Distance(selfPos, st.TargetContainerPos);

                if (st.SortingPhase == 1)
                {
                    // Walk toward target chest (or finish after 3.2s if path is blocked by furniture)
                    if (distToBox > 1.8f && elapsed < 3.2f)
                    {
                        try
                        {
                            ag.MoveTo_Direct(st.TargetContainerPos, true, true, false);
                            ag.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                        }
                        catch { }
                        continue;
                    }

                    // Reached container — execute sorting pass!
                    var containers = ScanNearbyContainers(force: true);
                    var destBoxes = new List<DiscoveredContainer>();
                    foreach (var c in containers)
                    {
                        if (c.Rule != null && c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
                            destBoxes.Add(c);
                    }

                    int moved = ExecuteSortPass(new List<GOAPAgent> { ag }, containers, destBoxes, null, out var domCat);
                    st.SortedItemsThisSession += moved;

                    if (moved > 0)
                    {
                        SpeakCategoryDeposit(ag, domCat);
                        // Move to one more chest to finish up
                        st.SortingPhase = 2;
                        st.SortingStateStartTime = now;
                        if (destBoxes.Count > 0)
                        {
                            var nextBox = destBoxes[UnityEngine.Random.Range(0, destBoxes.Count)];
                            st.TargetContainerPos = nextBox.WorldPos;
                        }
                    }
                    else
                    {
                        st.IsSortingActive = false;
                        st.SortingPhase = 0;
                        st.CurrentTaskText = CompanionConfigStore.L("Склад отсортирован", "Base storage sorted", "仓库已整理完毕");
                        SmartChatterSystem.Speak(ag, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
                        SetBanner(CompanionConfigStore.L(
                            $"{st.LastKnownName}: сортировка базы завершена (всего уложено за сессию: {st.SortedItemsThisSession} шт.)",
                            $"{st.LastKnownName}: base sorting finished (session total: {st.SortedItemsThisSession})",
                            $"{st.LastKnownName}：基地仓库整理完毕（本局累计整理：{st.SortedItemsThisSession} 件）"));
                    }
                }
                else if (st.SortingPhase == 2)
                {
                    if (distToBox > 1.8f && elapsed < 2.2f)
                    {
                        try
                        {
                            ag.MoveTo_Direct(st.TargetContainerPos, true, true, false);
                            ag.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                        }
                        catch { }
                        continue;
                    }

                    st.IsSortingActive = false;
                    st.SortingPhase = 0;
                    st.CurrentTaskText = CompanionConfigStore.L("Склад отсортирован", "Base storage sorted", "仓库已整理完毕");
                    SmartChatterSystem.Speak(ag, ChatterTopic.SortFinished, minCooldown: 0.5f, bubbleDuration: 6.0f, force: true);
                    SetBanner(CompanionConfigStore.L(
                        $"{st.LastKnownName}: сортировка базы завершена (всего уложено за сессию: {st.SortedItemsThisSession} шт.)",
                        $"{st.LastKnownName}: base sorting finished (session total: {st.SortedItemsThisSession})",
                        $"{st.LastKnownName}：基地仓库整理完毕（本局累计整理：{st.SortedItemsThisSession} 件）"));
                }
            }
        }

        private static int ExecuteSortPass(
            List<GOAPAgent> workers,
            List<DiscoveredContainer> allContainers,
            List<DiscoveredContainer> destContainers,
            InventoryData extraSource,
            out StorageCategory dominantCategory)
        {
            dominantCategory = StorageCategory.None;
            int totalMoved = 0;
            var touchedDestInventories = new HashSet<InventoryData>();
            var categoryCounts = new Dictionary<StorageCategory, int>();

            try
            {
                // Sort destination containers by Priority (highest first), then closest
                destContainers.Sort((a, b) =>
                {
                    int cmp = b.Rule.Priority.CompareTo(a.Rule.Priority);
                    return cmp != 0 ? cmp : a.DistanceToPlayer.CompareTo(b.DistanceToPlayer);
                });

                // 1. Collect source inventories:
                //    a) Explicit extraSource (e.g. currently open container when player clicked "Sort this chest")
                //    b) Any container marked as IsDropoffSource (Ящик-приёмник хабара)
                //    c) Companion sharedInventoryData + excess backpack items not needed for combat
                //    d) Misplaced items sitting inside categorized chests that don't match that chest's category!
                var sourceInventories = new List<(InventoryData inv, StorageCategory allowedStayMask, GOAPAgent ownerAgent)>();

                if (extraSource != null)
                {
                    sourceInventories.Add((extraSource, StorageCategory.None, null));
                }

                foreach (var c in allContainers)
                {
                    if (c.Inventory == null) continue;
                    if (extraSource != null && c.Inventory.Pointer == extraSource.Pointer) continue;

                    if (c.Rule != null && c.Rule.IsDropoffSource)
                    {
                        sourceInventories.Add((c.Inventory, StorageCategory.None, null));
                    }
                    else if (c.Rule != null && c.Rule.Categories != StorageCategory.None && c.Rule.Categories != StorageCategory.All)
                    {
                        // Move only items that DO NOT belong to this chest's allowed categories!
                        sourceInventories.Add((c.Inventory, c.Rule.Categories, null));
                    }
                }

                foreach (var ag in workers)
                {
                    if (ag?.npcData == null) continue;
                    var shared = ag.npcData.sharedInventoryData;
                    if (shared != null && shared.itemList != null && shared.itemList.Count > 0)
                        sourceInventories.Add((shared, StorageCategory.None, ag));
                }

                foreach (var srcEntry in sourceInventories)
                {
                    var srcInv = srcEntry.inv;
                    if (srcInv == null || srcInv.itemList == null || srcInv.itemList.Count == 0) continue;

                    // Snapshot items in source
                    var snapshot = new List<ItemData>();
                    for (int i = 0; i < srcInv.itemList.Count; i++)
                    {
                        var it = srcInv.itemList[i];
                        if (it != null) snapshot.Add(it);
                    }

                    foreach (var item in snapshot)
                    {
                        if (item == null) continue;
                        var cat = ItemCategorizer.ClassifyItem(item);

                        // If item already belongs in this categorized container, leave it where it is!
                        if (srcEntry.allowedStayMask != StorageCategory.None && (srcEntry.allowedStayMask & cat) != 0)
                            continue;

                        // Find best destination chest that accepts `cat`
                        DiscoveredContainer chosenDest = null;
                        foreach (var dest in destContainers)
                        {
                            if (dest.Inventory == null || dest.Inventory.Pointer == srcInv.Pointer) continue;
                            if ((dest.Rule.Categories & cat) == 0) continue;

                            chosenDest = dest;
                            if (TransferSingleItem(srcInv, dest.Inventory, item))
                            {
                                totalMoved++;
                                touchedDestInventories.Add(dest.Inventory);
                                categoryCounts.TryGetValue(cat, out int cur);
                                categoryCounts[cat] = cur + 1;
                                break;
                            }
                        }
                    }
                }

                // Auto-compact/sort all destination inventories that received items
                foreach (var dest in destContainers)
                {
                    if (dest.Inventory != null && dest.Rule.AutoCompact && touchedDestInventories.Contains(dest.Inventory))
                    {
                        try
                        {
                            dest.Inventory.AutoSorting(false, false, AutoSortMode.Dense, false);
                        }
                        catch { }
                    }
                }

                // Refresh open UI if player is viewing a container
                try
                {
                    if (LootInventoryPanel.instance != null && LootInventoryPanel.CurrentOpenInventoryData != null)
                        LootInventoryPanel.instance.Refresh();
                }
                catch { }

                int bestCount = 0;
                foreach (var kv in categoryCounts)
                {
                    if (kv.Value > bestCount)
                    {
                        bestCount = kv.Value;
                        dominantCategory = kv.Key;
                    }
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("ExecuteSortPass error: " + e.Message);
            }

            return totalMoved;
        }

        private static bool TransferSingleItem(InventoryData source, InventoryData dest, ItemData item)
        {
            if (source == null || dest == null || item == null) return false;
            try
            {
                // First check if destination has space or can merge stack
                var copy = item.CopyItemData(null);
                if (copy == null) return false;

                if (dest.TryAddItem(copy, out bool partiallyAdded, true, true))
                {
                    source.RemoveItem(item, true);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void SpeakCategoryDeposit(GOAPAgent agent, StorageCategory cat)
        {
            if (agent == null) return;
            ChatterTopic topic = ChatterTopic.SortDepositMaterials;
            if ((cat & (StorageCategory.MeleeWeapons | StorageCategory.RangedWeapons | StorageCategory.AmmoAndMags)) != 0)
                topic = ChatterTopic.SortDepositWeapons;
            else if ((cat & (StorageCategory.FreshFood | StorageCategory.CannedAndDrinks)) != 0)
                topic = ChatterTopic.SortDepositFood;
            else if ((cat & StorageCategory.Medicine) != 0)
                topic = ChatterTopic.SortDepositMeds;

            SmartChatterSystem.Speak(agent, topic, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
        }

        private static void TryAutoDropoffAtBase(GOAPAgent ag, CompanionRuntimeState st, float now)
        {
            try
            {
                var shared = ag.npcData?.sharedInventoryData;
                if (!Il2CppSafe.IsAlive(shared) || shared.itemList == null || shared.itemList.Count == 0)
                    return;

                var containers = ScanNearbyContainers(force: false);
                if (containers.Count == 0) return;

                bool nearDropoffOrCatBox = false;
                Vector2 selfPos = ag.humanCharacter.transform.position;
                var destBoxes = new List<DiscoveredContainer>();

                foreach (var c in containers)
                {
                    if (c.Rule == null || !Il2CppSafe.IsAlive(c.Inventory)) continue;
                    if (c.Rule.Categories != StorageCategory.None && !c.Rule.IsDropoffSource)
                        destBoxes.Add(c);
                    if ((c.Rule.IsDropoffSource || c.Rule.Categories != StorageCategory.None) &&
                        Vector2.Distance(selfPos, c.WorldPos) <= 7.5f)
                    {
                        nearDropoffOrCatBox = true;
                    }
                }

                if (!nearDropoffOrCatBox || destBoxes.Count == 0) return;

                st.LastAutoDropoffTime = now;
                int moved = ExecuteSortPass(new List<GOAPAgent> { ag }, containers, destBoxes, null, out _);
                if (moved > 0)
                {
                    st.SortedItemsThisSession += moved;
                    SmartChatterSystem.Speak(ag, ChatterTopic.BaseAutoDropoff, minCooldown: 2f, bubbleDuration: 6f, force: true);
                    SetBanner(CompanionConfigStore.L(
                        $"📥 {st.LastKnownName} автоматически выгрузил и разложил по ящикам базы {moved} шт. трофеев!",
                        $"📥 {st.LastKnownName} auto-unloaded and sorted {moved} raid items into base chests!",
                        $"📥 {st.LastKnownName} 已自动将 {moved} 件战利品卸入基地储物箱并按类别归位！"));
                }
            }
            catch { }
        }

        public static void SetBanner(string msg, float seconds = 6f)
        {
            LastSortStatusBanner = msg;
            LastSortStatusBannerUntil = Time.unscaledTime + seconds;
        }
    }
}
