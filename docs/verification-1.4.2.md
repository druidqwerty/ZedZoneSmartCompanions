# 1.4.2 verification — PASS

RELEASE_READY: true

## Final build

- Rebuilt against updated ZED ZONE interop assemblies (`2026-11-10 08:50`).
- Release build with warnings treated as errors (`--warnaserror`): 0 warnings, 0 errors.
- DLL SHA-256: `bd26c9f67bac995a0477003b5cab9c9ff1f918e8c3c252cc44972a32977a341a`.
- BepInEx startup: 43 native methods patched; `[SelfVerify] PASS v1.4.2`.
- Final live verification run (`docs/combat-acceptance-1.4.2.log`): zero error entries and zero exceptions.

## Root causes resolved in v1.4.2

1. **Fixed `ItemCategorizer.HasFeature` Classifying Every Item as `[Medicine]` (`0x13A9D30`):**
   - Native disassembly of `ItemAttr.GetItemFeatureByFeatureType(ItemFeatureType)` (`0x13A9D30`) showed that when `this.itemFeatureDataDic.ContainsKey(m_featureType)` is `false`, it jumps to `ItemManager.instance.GetItemFeatureByFeatureType(m_featureType)` — returning the global `ItemFeature` ScriptableObject (`!= null` for every feature in the game!).
   - Because `ItemCategorizer.ClassifyItem` called `HasFeature(attr, ItemFeatureType.Bandage)` first, every item with a non-null `ItemAttr` (guns, ammo, materials, tools, clothes, food) was misclassified as `StorageCategory.Medicine`!
   - Fixed by checking only `attr.itemFeatures.Contains(ft)` and `attr.itemFeatureDataDic.ContainsKey(ft)`. Verified in-engine with live `ItemAttr` instances (`gun=406 -> RangedWeapons`, `knife=352 -> MeleeWeapons`, `steak=248 -> FreshFood`).

2. **Sort Only Player-Marked Base Chests (`IsMarkedByPlayer`) & Smart 3-Mode F8 Filter:**
   - Previously, `AutoDetectCategoriesFromContents` tagged all 133 world containers within 48 m with `[Medicine]`, so companions carried items out of the player's Drop-off box into distant random world containers (making items appear to vanish!).
   - Added `ContainerRuleProfile.IsMarkedByPlayer` (`[*] Отмечен как ящик моей базы`) and migrated the save format (`v1.4.2` / `SC142:` property prefix) to automatically purge legacy `v1.4.1` `Medicine`-only auto-tags.
   - Sorting (`F7` / NPC sorting / Instant sort) now **only** moves items between chests explicitly marked by the player (`IsMarkedByPlayer == true`).
   - Added a 3-mode filter in **F8** Tab 2 (`ContainerListFilterMode`: default `Мои + у ног ≤2м` showing only marked base chests plus at most 3 unmarked chests right next to the player, cycling to `Рядом ≤7м` and `Все 18м`).

3. **Overflow-Free Compaction, Stack Merging & Full Drop-Off Unloading:**
   - Replaced `InventoryData.AutoSorting(false, false, AutoSortMode.Dense, false)` (which deletes overflow items when `executeDropFlag == false`) with `InventoryTransfers.SafeCompactInventory` (`inv.TryAutoSortingWithoutOverflow(false, out _)`), which captures a `LayoutSnapshot` and restores it if any item would overflow.
   - Added `TryDepositOrStackWhole` so stackable items (materials, ammo, meds) merge onto existing partial stacks, and tracked `remainingFreeCells` per destination chest in `PickupBatchIntoCarryBuffer` so companions route overflow items to the next marked chest of that category instead of over-picking for a nearly-full chest.
   - When items remain in a Drop-off box after sorting, the status banner reports the exact remaining item count and which categories need a marked chest or free space instead of falsely claiming all items are sorted.

Evidence: [combat-acceptance-1.4.2.log](combat-acceptance-1.4.2.log).

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
