using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public struct GearRepairCycleResult
    {
        public int TotalRepaired;
        public int EquippedRepaired;
        public int InventoryRepaired;
        public int MaterialsUsed;
        public int FieldMaintUsed;
    }

    /// <summary>
    /// Automatic NPC equipment &amp; inventory repair system (3.2.0):
    /// - Scans ALL equipped slots on CharacterEquipmentData (weapons, shield, armor layers, hat, mask,
    ///   cloak, backpack, exoskeleton, thruster, special equipment, lighting tool, handheld devices)
    ///   AND all companion inventories (personal, sharedInventoryData, equipped backpack, nested bags depth &lt;= 3).
    /// - Matches ZED ZONE 1.0.20 native durability storage from CharacterActionManager.OnItemRepairFinish
    ///   and BasicItemUI.BuildGunPartsRepairPlan:
    ///   * ItemType.RangedWeapon -> properties[0] (max = ItemAttr_RangedWeapon.hp) + clears MalfunctionFlag
    ///   * Attached GunParts     -> properties[9, 11, 13] (max = ItemAttr_GunPart.firingDurability)
    ///   * Attached Magazine     -> properties[2] (max = 100f, when feedingType == 0)
    ///   * ItemType.GunPart      -> properties[0] (max = ItemAttr_GunPart.firingDurability)
    ///   * All other Repairable  -> item.durability (max = 100f)
    /// - Zero item teleportation: material-based repair consumes only from the companion's own inventories
    ///   and player-marked base chests within 2.45m physical reach.
    /// </summary>
    public static class CompanionGearRepairSystem
    {
        public const float MaterialRepairThresholdRatio = 0.85f;
        public const float FieldMaintThresholdRatio = 0.92f;
        public const float BaseRepairCooldownSeconds = 14f;
        public const float FieldRepairCooldownSeconds = 22f;
        public const int MaxRepairsPerCycle = 2;
        public const int MaxRepairsPerCycleWithBrokenGear = 3;

        private const float MaxStandardDurability = 100f;
        private const float RepairCompleteEpsilon = 0.25f;

        private enum RepairTargetKind
        {
            StandardDurability = 0,
            RangedWeaponHp = 1,
            StandaloneGunPart = 2,
            AttachedGunPart = 3,
            AttachedMagazine = 4
        }

        private sealed class RepairTarget
        {
            public ItemData Item;
            public InventoryData OwnerInventory;
            public ItemAttr ItemAttr;
            public ItemAttr RepairAttr;
            public RepairTargetKind Kind;
            public int PropertyIndex;
            public float CurrentDurability;
            public float MaxDurability;
            public bool IsEquipped;
            public bool IsMalfunction;

            public float Ratio => MaxDurability > 0.01f
                ? Mathf.Clamp01(Mathf.Max(0f, CurrentDurability) / MaxDurability)
                : 1f;
        }

        private sealed class SolidConsumeStep
        {
            public InventoryData Inventory;
            public ItemData Item;
            public float Amount;
        }

        private sealed class LiquidConsumeStep
        {
            public ItemData Container;
            public int LiquidItemId;
            public float Volume;
        }

        private static readonly Dictionary<string, float> LastOfflineRepairAtByGuid =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public static void TickCompanionGearRepair(GOAPAgent ag, CompanionRuntimeState st, float now)
        {
            if (CompanionConfigStore.AutoRepairMode == GearAutoRepairMode.Off ||
                MultiplayerSyncSystem.IsMultiplayerClient || st == null || st.IsDismissedOrNotInTeam)
                return;

            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag ||
                ag.humanCharacter.isSleeping)
                return;

            var gc = GameController.instance;
            if (Il2CppSafe.IsAliveUnity(gc) && gc.isPlayerInCombat)
                return;
            if (IsCompanionInActiveCombat(ag))
                return;
            if (TryIsDowned(ag.humanCharacter))
                return;

            TryRunGearRepairCycleForAgent(ag, st, now, ignoreCooldown: false, allowSpeak: true);
        }

        public static GearRepairCycleResult TryRunGearRepairCycleForAgent(
            GOAPAgent ag,
            CompanionRuntimeState st,
            float now,
            bool ignoreCooldown = false,
            bool allowSpeak = true)
        {
            var result = default(GearRepairCycleResult);
            var mode = CompanionConfigStore.AutoRepairMode;
            if (mode == GearAutoRepairMode.Off || MultiplayerSyncSystem.IsMultiplayerClient || st == null)
                return result;

            if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                return result;

            var hc = ag.humanCharacter;
            var cd = ag.npcData.characterData;
            if (!Il2CppSafe.IsAlive(cd))
                return result;

            Vector2 selfPos = hc.transform.position;
            bool atBase = CompanionSurvivalSystem.IsAtCampOrPlayerBase(selfPos, st);
            float cooldown = atBase ? BaseRepairCooldownSeconds : FieldRepairCooldownSeconds;
            if (!ignoreCooldown && now - st.LastGearRepairTime < cooldown)
                return result;

            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im))
                return result;

            var equippedItems = CollectAllEquippedItems(hc, cd, st.NpcGuid);
            var ownInventories = CompanionSurvivalSystem.CollectCompanionInventories(cd, ag.npcData);
            var candidates = CollectRepairTargets(im, equippedItems, ownInventories, mode);
            if (candidates.Count == 0)
                return result;

            var protectedMaterialPtrs = BuildProtectedMaterialPointers(equippedItems, st.NpcGuid);
            List<InventoryData> materialInvs = null;
            if (mode == GearAutoRepairMode.MaterialsOnly || mode == GearAutoRepairMode.MaterialsOrField)
                materialInvs = CollectMaterialInventories(ownInventories, selfPos, ag.npcData.zLevel, atBase);

            float maintFraction = ComputeMaintenanceFraction(st);
            bool hasBroken = candidates[0].Ratio <= 0.01f || candidates[0].IsMalfunction;
            int budget = hasBroken ? MaxRepairsPerCycleWithBrokenGear : MaxRepairsPerCycle;
            var touchedInventories = new HashSet<IntPtr>();
            var touchedItems = new List<(InventoryData inv, ItemData item)>();

            for (int i = 0; i < candidates.Count && result.TotalRepaired < budget; i++)
            {
                var target = candidates[i];
                if (!Il2CppSafe.IsAlive(target.Item) || !Il2CppSafe.IsAliveUnity(target.RepairAttr))
                    continue;

                bool repaired = false;

                if ((mode == GearAutoRepairMode.MaterialsOnly || mode == GearAutoRepairMode.MaterialsOrField) &&
                    (target.Ratio < MaterialRepairThresholdRatio || target.IsMalfunction))
                {
                    if (TryRepairTargetWithMaterials(im, target, materialInvs, protectedMaterialPtrs))
                    {
                        repaired = true;
                        result.MaterialsUsed++;
                    }
                }

                if (!repaired &&
                    (mode == GearAutoRepairMode.MaterialsOrField || mode == GearAutoRepairMode.FreeMaintenance) &&
                    (target.Ratio < FieldMaintThresholdRatio || target.IsMalfunction))
                {
                    if (TryRepairTargetByMaintenance(target, maintFraction))
                    {
                        repaired = true;
                        result.FieldMaintUsed++;
                    }
                }

                if (!repaired)
                    continue;

                result.TotalRepaired++;
                if (target.IsEquipped)
                    result.EquippedRepaired++;
                else
                    result.InventoryRepaired++;

                if (Il2CppSafe.IsAlive(target.OwnerInventory) && touchedInventories.Add(target.Item.Pointer))
                    touchedItems.Add((target.OwnerInventory, target.Item));
                else if (target.IsEquipped && Il2CppSafe.IsAlive(cd.inventoryData) &&
                         InventoryTransfers.Contains(cd.inventoryData, target.Item) &&
                         touchedInventories.Add(target.Item.Pointer))
                    touchedItems.Add((cd.inventoryData, target.Item));
            }

            if (result.TotalRepaired <= 0)
                return result;

            st.LastGearRepairTime = now;
            st.ChoresDoneThisSession++;
            st.AddExperience(6, ag);

            if (result.EquippedRepaired > 0)
            {
                TryRefreshEquipmentGains(hc);
                CompanionSurvivalSystem.SyncNpcEquipment(hc);
            }

            for (int i = 0; i < touchedItems.Count; i++)
            {
                try { touchedItems[i].inv.NotifyDataChanged(false, touchedItems[i].item, false); } catch { }
            }

            if (!st.HasAnyPhysicalTask)
            {
                st.CurrentTaskText = result.InventoryRepaired > 0 && result.EquippedRepaired > 0
                    ? CompanionConfigStore.L(
                        "🔧 Отремонтировал экипировку и снаряжение в рюкзаке",
                        "🔧 Repaired equipped & backpack gear",
                        "🔧 已维护身上与背包中的装备")
                    : result.InventoryRepaired > 0
                        ? CompanionConfigStore.L(
                            "🔧 Отремонтировал снаряжение в инвентаре",
                            "🔧 Repaired gear in inventory",
                            "🔧 已维修背包中的装备")
                        : CompanionConfigStore.L(
                            "🔧 Чистка оружия и ремонт брони",
                            "🔧 Weapon & armor maintenance",
                            "🔧 武器擦拭与护具维护");
            }

            if (allowSpeak)
                SmartChatterSystem.Speak(ag, ChatterTopic.ChoreWeaponMaint, minCooldown: 30f, bubbleDuration: 5.5f);

            return result;
        }

        /// <summary>
        /// Offline CampGarrison maintenance hook (pure data only, no Unity Transform / scene calls).
        /// </summary>
        public static int TryRepairOfflineGarrisonGear(GOAPNPCData npcData, CompanionRuntimeState st)
        {
            var mode = CompanionConfigStore.AutoRepairMode;
            if (mode == GearAutoRepairMode.Off || MultiplayerSyncSystem.IsMultiplayerClient ||
                !Il2CppSafe.IsAlive(npcData) || !Il2CppSafe.IsAlive(npcData.characterData) || st == null)
                return 0;

            string guid = npcData.npcGUID ?? st.NpcGuid ?? "";
            float now = Time.unscaledTime;
            if (!string.IsNullOrEmpty(guid) &&
                LastOfflineRepairAtByGuid.TryGetValue(guid, out float nextAllowedAt) &&
                now < nextAllowedAt)
                return 0;

            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im))
                return 0;

            var cd = npcData.characterData;
            var equippedItems = CollectAllEquippedItems(null, cd, guid);
            var ownInventories = CompanionSurvivalSystem.CollectCompanionInventories(cd, npcData);
            var candidates = CollectRepairTargets(im, equippedItems, ownInventories, mode);
            if (candidates.Count == 0)
            {
                if (!string.IsNullOrEmpty(guid))
                    LastOfflineRepairAtByGuid[guid] = now + 20f;
                return 0;
            }

            var protectedMaterialPtrs = BuildProtectedMaterialPointers(equippedItems, guid);
            float maintFraction = ComputeMaintenanceFraction(st);
            int repairedCount = 0;

            for (int i = 0; i < candidates.Count && repairedCount < MaxRepairsPerCycle; i++)
            {
                var target = candidates[i];
                bool repaired = false;

                if ((mode == GearAutoRepairMode.MaterialsOnly || mode == GearAutoRepairMode.MaterialsOrField) &&
                    (target.Ratio < MaterialRepairThresholdRatio || target.IsMalfunction))
                {
                    repaired = TryRepairTargetWithMaterials(im, target, ownInventories, protectedMaterialPtrs);
                }

                if (!repaired &&
                    (mode == GearAutoRepairMode.MaterialsOrField || mode == GearAutoRepairMode.FreeMaintenance) &&
                    (target.Ratio < FieldMaintThresholdRatio || target.IsMalfunction))
                {
                    repaired = TryRepairTargetByMaintenance(target, maintFraction);
                }

                if (repaired)
                    repairedCount++;
            }

            if (!string.IsNullOrEmpty(guid))
                LastOfflineRepairAtByGuid[guid] = now + (repairedCount > 0 ? 30f : 15f);

            return repairedCount;
        }

        /// <summary>
        /// Maintenance-only repair for a nearby player's equipped gear during camp chores (never consumes player items).
        /// </summary>
        public static int RepairEquippedGearMaintenanceOnly(HumanCharacterController hc, CharacterData cd, float maintFraction)
        {
            if (!Il2CppSafe.IsAlive(cd)) return 0;
            var im = ItemManager.instance;
            if (!Il2CppSafe.IsAlive(im)) return 0;

            var equipped = CollectAllEquippedItems(hc, cd, null);
            var targets = new List<RepairTarget>(8);
            for (int i = 0; i < equipped.Count; i++)
                AppendItemRepairTargets(im, equipped[i], null, isEquipped: true, GearAutoRepairMode.FreeMaintenance, targets);

            if (targets.Count == 0) return 0;
            SortTargetsByPriority(targets);

            int repaired = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.Ratio >= FieldMaintThresholdRatio && !t.IsMalfunction)
                    continue;
                if (TryRepairTargetByMaintenance(t, maintFraction))
                    repaired++;
            }

            if (repaired > 0 && Il2CppSafe.IsAliveUnity(hc))
                TryRefreshEquipmentGains(hc);

            return repaired;
        }

        public static float ComputeMaintenanceFraction(CompanionRuntimeState st)
        {
            float fraction = 0.15f;
            if (st != null)
            {
                if (st.EffectiveArchetype == CompanionArchetype.Veteran) fraction += 0.05f;
                if (st.Rank >= CompanionRank.Veteran) fraction += 0.05f;
            }
            return Mathf.Clamp(fraction, 0.10f, 0.35f);
        }

        internal static List<ItemData> CollectAllEquippedItems(HumanCharacterController hc, CharacterData cd, string npcGuid)
        {
            var list = new List<ItemData>(16);
            var seen = new HashSet<IntPtr>();

            void Add(ItemData item)
            {
                if (Il2CppSafe.IsAlive(item) && seen.Add(item.Pointer))
                    list.Add(item);
            }

            try
            {
                if (Il2CppSafe.IsAliveUnity(hc) && Il2CppSafe.IsAliveUnity(hc.rangedWeapon))
                    Add(hc.rangedWeapon.weaponData);
            }
            catch { }

            var eq = cd?.characterEquipmentData;
            if (Il2CppSafe.IsAlive(eq))
            {
                try
                {
                    Add(eq.currentWeaponItemData);
                    if (eq.weaponItemDataAry != null)
                    {
                        for (int i = 0; i < eq.weaponItemDataAry.Length; i++)
                            Add(eq.weaponItemDataAry[i]);
                    }
                    Add(eq.shieldItemData);
                    Add(eq.hatItemData);
                    Add(eq.faceMaskItemData);
                    Add(eq.headDecorationItemData);
                    Add(eq.torseItemData);
                    if (eq.torseItemList != null)
                    {
                        for (int i = 0; i < eq.torseItemList.Count; i++)
                            Add(eq.torseItemList[i]);
                    }
                    Add(eq.legItemData);
                    if (eq.legItemList != null)
                    {
                        for (int i = 0; i < eq.legItemList.Count; i++)
                            Add(eq.legItemList[i]);
                    }
                    Add(eq.feetItemData);
                    Add(eq.cloakItemData);
                    Add(eq.backpackItemData);
                    Add(eq.exoskeletonItemData);
                    Add(eq.thrusterItemData);
                    Add(eq.specialEquipmentItemData);
                    Add(eq.lightingToolItemData);
                    if (eq.handheldDeviceItemDataAry != null)
                    {
                        for (int i = 0; i < eq.handheldDeviceItemDataAry.Length; i++)
                            Add(eq.handheldDeviceItemDataAry[i]);
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(npcGuid) &&
                CompanionSurvivalSystem.SavedEverydayMaskByGuid.TryGetValue(npcGuid, out var savedMask))
            {
                Add(savedMask);
            }

            return list;
        }

        private static HashSet<IntPtr> BuildProtectedMaterialPointers(List<ItemData> equippedItems, string npcGuid)
        {
            var set = new HashSet<IntPtr>();
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (Il2CppSafe.IsAlive(equippedItems[i]))
                    set.Add(equippedItems[i].Pointer);
            }
            if (!string.IsNullOrEmpty(npcGuid) &&
                CompanionSurvivalSystem.SavedEverydayMaskByGuid.TryGetValue(npcGuid, out var savedMask) &&
                Il2CppSafe.IsAlive(savedMask))
            {
                set.Add(savedMask.Pointer);
            }
            return set;
        }

        private static List<InventoryData> CollectMaterialInventories(
            List<InventoryData> ownInventories,
            Vector2 selfPos,
            sbyte zLevel,
            bool atBase)
        {
            var invs = new List<InventoryData>(ownInventories.Count + 4);
            var seen = new HashSet<IntPtr>();

            for (int i = 0; i < ownInventories.Count; i++)
            {
                var inv = ownInventories[i];
                if (Il2CppSafe.IsAlive(inv) && seen.Add(inv.Pointer))
                    invs.Add(inv);
            }

            // Zero-teleport guarantee: only draw base chest repair materials from player-marked chests
            // strictly within physical interaction reach (<= 2.45m) on the same floor.
            if (atBase)
            {
                try
                {
                    var chests = BaseSortingSystem.ScanNearbyContainers(force: false);
                    for (int i = 0; i < chests.Count; i++)
                    {
                        var c = chests[i];
                        if (c == null || c.Rule == null || !c.Rule.IsMarkedByPlayer || !Il2CppSafe.IsAlive(c.Inventory))
                            continue;
                        if (Il2CppSafe.IsAliveUnity(c.TerrainObj) && Il2CppSafe.IsAlive(c.TerrainObj.objectData) &&
                            c.TerrainObj.objectData.zLayer != zLevel)
                            continue;
                        if (Vector2.Distance(selfPos, c.WorldPos) > PhysicalMovementHelper.MaxReachableInteractRadius)
                            continue;
                        if (seen.Add(c.Inventory.Pointer))
                            invs.Add(c.Inventory);
                    }
                }
                catch { }
            }

            return invs;
        }

        private static List<RepairTarget> CollectRepairTargets(
            ItemManager im,
            List<ItemData> equippedItems,
            List<InventoryData> ownInventories,
            GearAutoRepairMode mode)
        {
            var targets = new List<RepairTarget>(16);
            var seenItems = new HashSet<IntPtr>();

            for (int i = 0; i < equippedItems.Count; i++)
            {
                var it = equippedItems[i];
                if (!Il2CppSafe.IsAlive(it) || !seenItems.Add(it.Pointer))
                    continue;
                AppendItemRepairTargets(im, it, null, isEquipped: true, mode, targets);
            }

            for (int invIdx = 0; invIdx < ownInventories.Count; invIdx++)
            {
                var inv = ownInventories[invIdx];
                if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null)
                    continue;

                for (int i = 0; i < inv.itemList.Count; i++)
                {
                    var it = inv.itemList[i];
                    if (!Il2CppSafe.IsAlive(it) || !seenItems.Add(it.Pointer))
                        continue;
                    AppendItemRepairTargets(im, it, inv, isEquipped: false, mode, targets);
                }
            }

            if (targets.Count > 1)
                SortTargetsByPriority(targets);

            return targets;
        }

        private static void SortTargetsByPriority(List<RepairTarget> targets)
        {
            targets.Sort((a, b) =>
            {
                // 1. Equipped gear before inventory gear
                if (a.IsEquipped != b.IsEquipped)
                    return a.IsEquipped ? -1 : 1;
                // 2. Malfunctioning / jammed firearms first
                if (a.IsMalfunction != b.IsMalfunction)
                    return a.IsMalfunction ? -1 : 1;
                // 3. Lowest durability ratio first (broken 0% at the very top)
                int cmp = a.Ratio.CompareTo(b.Ratio);
                if (cmp != 0) return cmp;
                // 4. Main weapon / gear before attached accessories
                return ((int)a.Kind).CompareTo((int)b.Kind);
            });
        }

        private static void AppendItemRepairTargets(
            ItemManager im,
            ItemData item,
            InventoryData ownerInv,
            bool isEquipped,
            GearAutoRepairMode mode,
            List<RepairTarget> dest)
        {
            if (!Il2CppSafe.IsAlive(item) || item.itemId <= 0)
                return;

            ItemAttr attr;
            try { attr = im.GetItemAttrById(item.itemId); }
            catch { return; }
            if (!Il2CppSafe.IsAliveUnity(attr))
                return;

            float maxThreshold = mode == GearAutoRepairMode.MaterialsOnly
                ? MaterialRepairThresholdRatio
                : FieldMaintThresholdRatio;

            // 1. Ranged Weapon (durability in properties[0] out of rw.hp, plus attached gun parts & magazine)
            if (attr.itemType == ItemType.RangedWeapon)
            {
                var rw = attr.TryCast<ItemAttr_RangedWeapon>();
                if (!Il2CppSafe.IsAliveUnity(rw))
                    return;

                var props = item.properties;
                if (props != null && props.Length > 0 && rw.hp > 0.01f)
                {
                    bool hasNativeRepairPath = TryGetRepairable(attr) || HasRangedSparePartAttr(im, attr.itemId);
                    if (mode != GearAutoRepairMode.MaterialsOnly || hasNativeRepairPath)
                    {
                        float cur = props[0];
                        bool malfunction = IsWeaponMalfunctioning(item);
                        float ratio = Mathf.Clamp01(Mathf.Max(0f, cur) / rw.hp);
                        if (malfunction || (cur < rw.hp - RepairCompleteEpsilon && ratio < maxThreshold))
                        {
                            dest.Add(new RepairTarget
                            {
                                Item = item,
                                OwnerInventory = ownerInv,
                                ItemAttr = attr,
                                RepairAttr = attr,
                                Kind = RepairTargetKind.RangedWeaponHp,
                                PropertyIndex = 0,
                                CurrentDurability = cur,
                                MaxDurability = rw.hp,
                                IsEquipped = isEquipped,
                                IsMalfunction = malfunction
                            });
                        }
                    }

                    // Attached GunParts: pairs (8,9), (10,11), (12,13) verified from BasicItemUI.BuildGunPartsRepairPlan
                    if (props.Length >= 14)
                    {
                        for (int slotIdx = 8; slotIdx <= 12; slotIdx += 2)
                        {
                            int partId = Mathf.RoundToInt(props[slotIdx]);
                            if (partId <= 0) continue;
                            ItemAttr partBase;
                            try { partBase = im.GetItemAttrById(partId); }
                            catch { continue; }
                            var partAttr = partBase?.TryCast<ItemAttr_GunPart>();
                            if (!Il2CppSafe.IsAliveUnity(partAttr) || !TryGetRepairable(partBase) || partAttr.firingDurability <= 0.01f)
                                continue;

                            float curPart = props[slotIdx + 1];
                            float partRatio = Mathf.Clamp01(Mathf.Max(0f, curPart) / partAttr.firingDurability);
                            if (curPart < partAttr.firingDurability - RepairCompleteEpsilon && partRatio < maxThreshold)
                            {
                                dest.Add(new RepairTarget
                                {
                                    Item = item,
                                    OwnerInventory = ownerInv,
                                    ItemAttr = attr,
                                    RepairAttr = partBase,
                                    Kind = RepairTargetKind.AttachedGunPart,
                                    PropertyIndex = slotIdx + 1,
                                    CurrentDurability = curPart,
                                    MaxDurability = partAttr.firingDurability,
                                    IsEquipped = isEquipped,
                                    IsMalfunction = false
                                });
                            }
                        }
                    }

                    // Attached Magazine: properties[1]=magItemId, properties[2]=durability/100f when feedingType == 0
                    if (props.Length >= 3 && (int)rw.feedingType == 0)
                    {
                        int magId = Mathf.RoundToInt(props[1]);
                        if (magId > 0)
                        {
                            ItemAttr magAttr = null;
                            try { magAttr = im.GetItemAttrById(magId); } catch { }
                            if (Il2CppSafe.IsAliveUnity(magAttr) && TryGetRepairable(magAttr))
                            {
                                float curMag = props[2];
                                float magRatio = Mathf.Clamp01(Mathf.Max(0f, curMag) / MaxStandardDurability);
                                if (curMag < MaxStandardDurability - RepairCompleteEpsilon && magRatio < maxThreshold)
                                {
                                    dest.Add(new RepairTarget
                                    {
                                        Item = item,
                                        OwnerInventory = ownerInv,
                                        ItemAttr = attr,
                                        RepairAttr = magAttr,
                                        Kind = RepairTargetKind.AttachedMagazine,
                                        PropertyIndex = 2,
                                        CurrentDurability = curMag,
                                        MaxDurability = MaxStandardDurability,
                                        IsEquipped = isEquipped,
                                        IsMalfunction = false
                                    });
                                }
                            }
                        }
                    }
                }
                return;
            }

            // 2. Standalone GunPart in inventory (durability in properties[0] out of firingDurability)
            if (attr.itemType == ItemType.GunPart)
            {
                var gp = attr.TryCast<ItemAttr_GunPart>();
                var props = item.properties;
                if (!Il2CppSafe.IsAliveUnity(gp) || !TryGetRepairable(attr) || gp.firingDurability <= 0.01f ||
                    props == null || props.Length == 0)
                    return;

                float cur = props[0];
                float ratio = Mathf.Clamp01(Mathf.Max(0f, cur) / gp.firingDurability);
                if (cur < gp.firingDurability - RepairCompleteEpsilon && ratio < maxThreshold)
                {
                    dest.Add(new RepairTarget
                    {
                        Item = item,
                        OwnerInventory = ownerInv,
                        ItemAttr = attr,
                        RepairAttr = attr,
                        Kind = RepairTargetKind.StandaloneGunPart,
                        PropertyIndex = 0,
                        CurrentDurability = cur,
                        MaxDurability = gp.firingDurability,
                        IsEquipped = isEquipped,
                        IsMalfunction = false
                    });
                }
                return;
            }

            // 3. Standard Repairable Equipment (Clothing, Armor, MeleeWeapon, FaceMask, Backpack, Magazine, Cloak,
            //    Shield, Exoskeleton, Thruster, LightingTool, HandheldDevice, repairable Tools)
            if (!IsEligibleStandardRepairableEquipment(attr, isEquipped))
                return;

            float stdCur = item.durability;
            float stdRatio = Mathf.Clamp01(Mathf.Max(0f, stdCur) / MaxStandardDurability);
            if (stdCur < MaxStandardDurability - RepairCompleteEpsilon && stdRatio < maxThreshold)
            {
                dest.Add(new RepairTarget
                {
                    Item = item,
                    OwnerInventory = ownerInv,
                    ItemAttr = attr,
                    RepairAttr = attr,
                    Kind = RepairTargetKind.StandardDurability,
                    PropertyIndex = -1,
                    CurrentDurability = stdCur,
                    MaxDurability = MaxStandardDurability,
                    IsEquipped = isEquipped,
                    IsMalfunction = false
                });
            }
        }

        public static bool IsEligibleStandardRepairableEquipment(ItemAttr attr, bool isEquipped)
        {
            if (!Il2CppSafe.IsAliveUnity(attr))
                return false;

            switch (attr.itemType)
            {
                case ItemType.Food:
                case ItemType.Ammo:
                case ItemType.Arrow:
                case ItemType.Throwable:
                case ItemType.Liqiud:
                case ItemType.Container:
                case ItemType.Deployable:
                    return false;
            }

            if (!TryGetRepairable(attr))
                return false;

            switch (attr.itemType)
            {
                case ItemType.MeleeWeapon:
                case ItemType.Clothing:
                case ItemType.FaceMask:
                case ItemType.Backpack:
                case ItemType.Magazine:
                case ItemType.Cloak:
                    return true;

                default:
                    return isEquipped || attr.toolType != ToolType.None || HasRepairableEquipmentFeature(attr);
            }
        }

        private static bool HasRepairableEquipmentFeature(ItemAttr attr)
        {
            try
            {
                if (attr.itemFeatures != null)
                {
                    for (int i = 0; i < attr.itemFeatures.Count; i++)
                    {
                        var f = attr.itemFeatures[i];
                        if (f == ItemFeatureType.LightingTool ||
                            f == ItemFeatureType.HandheldDevice ||
                            f == ItemFeatureType.SlaughterTool ||
                            f == ItemFeatureType.MeleeWeaponAttachment ||
                            f == ItemFeatureType.HeadDecoration)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryRepairTargetWithMaterials(
            ItemManager im,
            RepairTarget target,
            List<InventoryData> materialInvs,
            HashSet<IntPtr> protectedMaterialPtrs)
        {
            if (materialInvs == null || materialInvs.Count == 0 ||
                !Il2CppSafe.IsAlive(target.Item) || !Il2CppSafe.IsAliveUnity(target.RepairAttr))
                return false;

            // Path 1: Native ItemAttr.repairData recipe (RecipeData.recipeItems)
            if (TryGetRepairable(target.RepairAttr))
            {
                var recipe = target.RepairAttr.repairData;
                if (Il2CppSafe.IsAlive(recipe) && recipe.recipeItems != null && recipe.recipeItems.Count > 0)
                {
                    if (TryPlanAndConsumeRecipe(recipe, materialInvs, protectedMaterialPtrs, target.Item.Pointer))
                    {
                        float restore = ResolveNativeRestoreAmount(target);
                        return ApplyRepairDelta(target, restore);
                    }
                }
            }

            // Path 2: Native ranged-weapon spare parts repair (BasicItemUI.RepairRangedWeaponWithParts: itemId + 1000)
            if (target.Kind == RepairTargetKind.RangedWeaponHp && Il2CppSafe.IsAliveUnity(target.ItemAttr))
            {
                int partId = target.ItemAttr.itemId + 1000;
                if (HasItemAttr(im, partId))
                {
                    int partCost = ResolveRangedPartsCostPerRepair();
                    if (TryPlanAndConsumeSolidIds(new[] { partId }, partCost, materialInvs, protectedMaterialPtrs, target.Item.Pointer))
                    {
                        float hpPct = ResolveRangedHpPercentPerRepair();
                        float restore = Mathf.Max(MaxStandardDurability, target.MaxDurability * hpPct);
                        return ApplyRepairDelta(target, restore);
                    }
                }
            }

            return false;
        }

        private static bool TryPlanAndConsumeRecipe(
            RecipeData recipe,
            List<InventoryData> materialInvs,
            HashSet<IntPtr> protectedMaterialPtrs,
            IntPtr excludeTargetPtr)
        {
            var items = recipe.recipeItems;
            if (items == null || items.Count == 0)
                return false;

            var solidSteps = new List<SolidConsumeStep>(4);
            var liquidSteps = new List<LiquidConsumeStep>(2);
            var reservedSolid = new Dictionary<IntPtr, float>();
            var reservedLiquid = new Dictionary<IntPtr, float>();

            for (int r = 0; r < items.Count; r++)
            {
                var rid = items[r];
                if (!Il2CppSafe.IsAlive(rid) || rid.itemId <= 0 || rid.itemNumber <= 0.0001f)
                    continue;

                if (TryIsLiquidMaterial(rid.itemId))
                {
                    ItemData chosenContainer = null;
                    for (int invIdx = 0; invIdx < materialInvs.Count && chosenContainer == null; invIdx++)
                    {
                        var inv = materialInvs[invIdx];
                        if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;
                        for (int i = 0; i < inv.itemList.Count; i++)
                        {
                            var cand = inv.itemList[i];
                            if (!Il2CppSafe.IsAlive(cand) || cand.Pointer == excludeTargetPtr) continue;
                            if (protectedMaterialPtrs != null && protectedMaterialPtrs.Contains(cand.Pointer)) continue;
                            reservedLiquid.TryGetValue(cand.Pointer, out float alreadyReserved);
                            if (TryCanSupplyLiquid(cand, rid.itemId, rid.itemNumber + alreadyReserved))
                            {
                                chosenContainer = cand;
                                reservedLiquid[cand.Pointer] = alreadyReserved + rid.itemNumber;
                                break;
                            }
                        }
                    }

                    if (chosenContainer == null)
                        return false;

                    liquidSteps.Add(new LiquidConsumeStep
                    {
                        Container = chosenContainer,
                        LiquidItemId = rid.itemId,
                        Volume = rid.itemNumber
                    });
                }
                else
                {
                    var accepted = ResolveAcceptedItemIds(recipe, rid);
                    if (!PlanSolidConsumption(accepted, rid.itemNumber, materialInvs, protectedMaterialPtrs, excludeTargetPtr, reservedSolid, solidSteps))
                        return false;
                }
            }

            if (solidSteps.Count == 0 && liquidSteps.Count == 0)
                return false;

            // Commit liquid consumption first (returns bool if container actually had enough)
            for (int i = 0; i < liquidSteps.Count; i++)
            {
                var step = liquidSteps[i];
                if (!TryConsumeLiquid(step.Container, step.LiquidItemId, step.Volume))
                    return false;
            }

            ExecuteSolidConsumption(solidSteps);
            return true;
        }

        private static bool TryPlanAndConsumeSolidIds(
            int[] acceptedIds,
            float requiredCount,
            List<InventoryData> materialInvs,
            HashSet<IntPtr> protectedMaterialPtrs,
            IntPtr excludeTargetPtr)
        {
            if (acceptedIds == null || acceptedIds.Length == 0 || requiredCount <= 0.0001f)
                return false;

            var steps = new List<SolidConsumeStep>(4);
            var reserved = new Dictionary<IntPtr, float>();
            if (!PlanSolidConsumption(acceptedIds, requiredCount, materialInvs, protectedMaterialPtrs, excludeTargetPtr, reserved, steps))
                return false;

            ExecuteSolidConsumption(steps);
            return true;
        }

        private static bool PlanSolidConsumption(
            int[] acceptedIds,
            float needed,
            List<InventoryData> materialInvs,
            HashSet<IntPtr> protectedMaterialPtrs,
            IntPtr excludeTargetPtr,
            Dictionary<IntPtr, float> reservedByPtr,
            List<SolidConsumeStep> outSteps)
        {
            float remaining = needed;

            for (int invIdx = 0; invIdx < materialInvs.Count && remaining > 0.001f; invIdx++)
            {
                var inv = materialInvs[invIdx];
                if (!Il2CppSafe.IsAlive(inv) || inv.itemList == null) continue;

                for (int i = 0; i < inv.itemList.Count && remaining > 0.001f; i++)
                {
                    var cand = inv.itemList[i];
                    if (!Il2CppSafe.IsAlive(cand) || cand.Pointer == excludeTargetPtr)
                        continue;
                    if (protectedMaterialPtrs != null && protectedMaterialPtrs.Contains(cand.Pointer))
                        continue;
                    if (!MatchesAnyItemId(cand.itemId, acceptedIds))
                        continue;
                    // Never destroy a bag/container holding nested items inside it
                    if (Il2CppSafe.IsAlive(cand.inventoryData) && cand.inventoryData.itemList != null && cand.inventoryData.itemList.Count > 0)
                        continue;

                    float totalQty = cand.itemNumberFloat > 0.001f ? cand.itemNumberFloat : 1f;
                    reservedByPtr.TryGetValue(cand.Pointer, out float alreadyReserved);
                    float avail = totalQty - alreadyReserved;
                    if (avail <= 0.001f) continue;

                    float take = Mathf.Min(avail, remaining);
                    reservedByPtr[cand.Pointer] = alreadyReserved + take;
                    outSteps.Add(new SolidConsumeStep { Inventory = inv, Item = cand, Amount = take });
                    remaining -= take;
                }
            }

            return remaining <= 0.001f;
        }

        private static void ExecuteSolidConsumption(List<SolidConsumeStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (!Il2CppSafe.IsAlive(step.Inventory) || !Il2CppSafe.IsAlive(step.Item))
                    continue;

                float curQty = step.Item.itemNumberFloat > 0.001f ? step.Item.itemNumberFloat : 1f;
                float nextQty = curQty - step.Amount;
                if (nextQty <= 0.001f)
                {
                    try { step.Inventory.RemoveItem(step.Item, false); } catch { }
                }
                else
                {
                    step.Item.itemNumberFloat = nextQty;
                    try { step.Inventory.NotifyDataChanged(false, step.Item, false); } catch { }
                }
            }
        }

        private static bool TryRepairTargetByMaintenance(RepairTarget target, float maintFraction)
        {
            if (!Il2CppSafe.IsAlive(target.Item) || target.MaxDurability <= 0.01f)
                return false;

            float delta = Mathf.Max(10f, target.MaxDurability * maintFraction);
            return ApplyRepairDelta(target, delta);
        }

        private static bool ApplyRepairDelta(RepairTarget target, float delta)
        {
            if (!Il2CppSafe.IsAlive(target.Item) || delta <= 0f || target.MaxDurability <= 0.01f)
                return false;

            try
            {
                switch (target.Kind)
                {
                    case RepairTargetKind.RangedWeaponHp:
                    {
                        var props = target.Item.properties;
                        if (props == null || props.Length == 0) return false;
                        float before = props[0];
                        float clampedStart = Mathf.Max(0f, before);
                        float after = Mathf.Min(target.MaxDurability, clampedStart + delta);
                        bool clearedMalfunction = target.IsMalfunction;
                        if (after <= before + 0.001f && !clearedMalfunction)
                            return false;

                        props[0] = after;
                        TryClearRangedWeaponMalfunction(target.Item);
                        target.CurrentDurability = after;
                        target.IsMalfunction = false;
                        return true;
                    }

                    case RepairTargetKind.StandaloneGunPart:
                    case RepairTargetKind.AttachedGunPart:
                    case RepairTargetKind.AttachedMagazine:
                    {
                        var props = target.Item.properties;
                        int idx = target.PropertyIndex;
                        if (props == null || idx < 0 || idx >= props.Length) return false;
                        float before = props[idx];
                        float clampedStart = Mathf.Max(0f, before);
                        float after = Mathf.Min(target.MaxDurability, clampedStart + delta);
                        if (after <= before + 0.001f)
                            return false;

                        props[idx] = after;
                        target.CurrentDurability = after;
                        return true;
                    }

                    default:
                    {
                        float before = target.Item.durability;
                        float clampedStart = Mathf.Max(0f, before);
                        float after = Mathf.Min(MaxStandardDurability, clampedStart + delta);
                        if (after <= before + 0.001f)
                            return false;

                        target.Item.durability = after;
                        target.CurrentDurability = after;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static float ResolveNativeRestoreAmount(RepairTarget target)
        {
            if (target.Kind == RepairTargetKind.StandaloneGunPart ||
                target.Kind == RepairTargetKind.AttachedGunPart ||
                target.Kind == RepairTargetKind.AttachedMagazine)
                return target.MaxDurability;

            try
            {
                float nativeDelta = target.RepairAttr.GetRepairedDurabilityPerRepair();
                if (nativeDelta > 0.01f)
                    return nativeDelta;
            }
            catch { }

            return target.Kind == RepairTargetKind.RangedWeaponHp
                ? Mathf.Max(MaxStandardDurability, target.MaxDurability * 0.25f)
                : MaxStandardDurability;
        }

        private static bool MatchesAnyItemId(int itemId, int[] accepted)
        {
            if (accepted == null) return false;
            for (int i = 0; i < accepted.Length; i++)
                if (accepted[i] == itemId) return true;
            return false;
        }

        private static bool HasRangedSparePartAttr(ItemManager im, int weaponItemId) =>
            HasItemAttr(im, weaponItemId + 1000);

        private static bool HasItemAttr(ItemManager im, int itemId)
        {
            if (!Il2CppSafe.IsAlive(im) || itemId <= 0) return false;
            try { return Il2CppSafe.IsAliveUnity(im.GetItemAttrById(itemId)); }
            catch { return false; }
        }

        private static bool IsWeaponMalfunctioning(ItemData item)
        {
            if (!Il2CppSafe.IsAlive(item)) return false;
            try
            {
                string flag = item.GetProperty("MalfunctionFlag");
                return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsCompanionInActiveCombat(GOAPAgent ag)
        {
            try
            {
                if (CombatResponse.HasCloseLineOfSightThreat(ag, ag.mostThreateningCharacter, 16f))
                    return true;
                if (Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                {
                    float dist = Vector2.Distance(ag.humanCharacter.transform.position, ag.mostThreateningCharacter.transform.position);
                    if (dist <= 16f) return true;
                }
            }
            catch { }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetRepairable(ItemAttr attr)
        {
            if (!Il2CppSafe.IsAliveUnity(attr)) return false;
            try { return GetRepairableNoInline(attr); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetRepairableNoInline(ItemAttr attr) => attr.Repairable;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryClearRangedWeaponMalfunction(ItemData weaponData)
        {
            if (!Il2CppSafe.IsAlive(weaponData)) return;
            try { ClearRangedWeaponMalfunctionNoInline(weaponData); }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ClearRangedWeaponMalfunctionNoInline(ItemData weaponData)
        {
            BasicRangedWeapon.CoolDownOnMalfunctionCleared(weaponData);
            weaponData.SetProperty("MalfunctionFlag", "false");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryRefreshEquipmentGains(HumanCharacterController hc)
        {
            if (!Il2CppSafe.IsAliveUnity(hc)) return;
            try { RefreshEquipmentGainsNoInline(hc); }
            catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RefreshEquipmentGainsNoInline(HumanCharacterController hc) => hc.RefreshEquipmentGains();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsDowned(HumanCharacterController hc)
        {
            if (!Il2CppSafe.IsAliveUnity(hc)) return false;
            try { return IsDownedNoInline(hc); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsDownedNoInline(HumanCharacterController hc) =>
            CharacterAidManager.IsDowned(hc) || hc.weaponHolsteredByDowned;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int[] ResolveAcceptedItemIds(RecipeData recipe, RecipeItemData rid)
        {
            try
            {
                var arr = ResolveAcceptedItemIdsNoInline(recipe, rid);
                if (arr != null && arr.Length > 0)
                {
                    var res = new int[arr.Length];
                    for (int i = 0; i < arr.Length; i++) res[i] = arr[i];
                    return res;
                }
            }
            catch { }
            return new[] { rid.itemId };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> ResolveAcceptedItemIdsNoInline(
            RecipeData recipe, RecipeItemData rid) => recipe.GetAcceptedItemIds(rid);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryIsLiquidMaterial(int itemId)
        {
            try { return IsLiquidMaterialNoInline(itemId); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsLiquidMaterialNoInline(int itemId) => RepairLiquidMaterial.IsLiquidMaterial(itemId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryCanSupplyLiquid(ItemData container, int liquidItemId, float requiredVolume)
        {
            try { return CanSupplyLiquidNoInline(container, liquidItemId, requiredVolume); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CanSupplyLiquidNoInline(ItemData container, int liquidItemId, float requiredVolume) =>
            RepairLiquidMaterial.CanSupply(container, liquidItemId, requiredVolume);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryConsumeLiquid(ItemData container, int liquidItemId, float volume)
        {
            try { return ConsumeLiquidNoInline(container, liquidItemId, volume); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ConsumeLiquidNoInline(ItemData container, int liquidItemId, float volume) =>
            RepairLiquidMaterial.Consume(container, liquidItemId, volume);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ResolveRangedPartsCostPerRepair()
        {
            try
            {
                int cost = GetRangedPartsCostNoInline();
                return cost > 0 ? cost : 1;
            }
            catch { return 1; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetRangedPartsCostNoInline() => BasicItemUI.rangedWeaponPartsCostPerRepair;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ResolveRangedHpPercentPerRepair()
        {
            try
            {
                float pct = GetRangedHpPercentNoInline();
                return pct > 0.01f && pct <= 1f ? pct : 0.25f;
            }
            catch { return 0.25f; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetRangedHpPercentNoInline() => BasicItemUI.rangedWeaponHPPercentPerRepair;
    }
}
