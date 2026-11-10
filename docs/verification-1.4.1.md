# 1.4.1 verification — PASS

RELEASE_READY: true

## Final build

- Release build with warnings treated as errors (`--warnaserror`): 0 warnings, 0 errors.
- DLL SHA-256: `cfb9a1ec0c1ec4fb5291b8def4a425b43d3e56aa35fa443da672ca9cac1f680c`.
- BepInEx startup: 43 native methods patched; `[SelfVerify] PASS v1.4.1`.
- Final live container scan, physical carry buffer & combat run (`docs/combat-acceptance-1.4.1.log`): zero error entries and zero exceptions.

## Root causes resolved in v1.4.1

1. **World Container Discovery Without Opening Chests (`CollectWorldTerrainObjects` & `ResolveOrInitTerrainObjectInventory`):**
   - Previously, `BaseSortingSystem.ScanNearbyContainers()` only scanned `GameController.instance.terrainObjectsParent`, which had `0` chunk/building objects! As a result, no containers appeared in **F8** until the player manually opened one, and `LootInventoryPanel.CurrentOpenInventoryData` kept pointing to the first opened container even after closing the loot panel (`0.0 m`).
   - Fixed by scanning all active `BasicChunk` instances in `MapController.instance.currentChunkList` (`chunkData.terrainObjectListTemp` + `chunkData.terrainObjectParent`), all `BasicBuilding.ActiveObjects` (`building.terrainObjectParent` + `building.buildingFloors[i].insideTerrainObjectList` + `floor.terrainObjectParent`), and deployed container items in `BasicItem.ActiveObjects`. For unopened `TerrainObject_Furniture_Inventory` and `TerrainObject_Production_Fridge` containers within range, `EnsureLootGenerated()` / `EnsureLootGeneratedNet()` is called so all nearby chests (`133` unique chests in the owner's base) appear immediately in **F8** without opening them first, and pressing **F8** automatically selects the closest container next to the player.

2. **Unique Per-Container Identification (Eliminating `"unknown_box"` Shared Rule Bug):**
   - Previously, `FindTerrainObjectByInventory(inv)` failed on chunk/building chests and `inv.inventoryId` was empty (`""`), causing `GetContainerStableId` to return `"unknown_box"` for every container—so toggling `"Ящик-приёмник"` (Drop-off Box) or categories on one chest applied to all chests!
   - Fixed by registering a 1-to-1 `InventoryData <-> TerrainObject` map (`KnownInvToTerrainObj`, `KnownInvToStableId`, `KnownStableIdToInv`), building guaranteed-unique per-box IDs (`box_{rx100}_{ry100}_{z}_{attrId}_{standVz}_{guid}`), and purging any legacy `"unknown_box"` entry.

3. **Physical Companion Item Carrying via Dedicated Invisible Carry Buffer (`CarryBuffer`):**
   - Replaced instant item teleportation during companion sorting (`F7` / `"Приказ NPC: Сортировать базу"`) with a two-phase physical carrying system using a dedicated invisible transfer inventory (`CompanionRuntimeState.CarryBuffer`) that never conflicts with the companion's main backpack/weapons:
     - **Phase 1 (`WalkToSource`):** Companion physically walks to the source/drop-off chest (`GOAPAction_FollowLeader.Execute` redirected + per-frame `TickActiveSortingMovementEveryFrame`), takes up to `10` item stacks out of the source chest into `st.CarryBuffer`, and refreshes `LootInventoryPanel` if open.
     - **Phase 2 (`WalkToDestination`):** Companion physically walks to each matching destination chest, deposits the carried items from `st.CarryBuffer` into the destination chest, compacts the grid (`AutoSortMode.Dense`), and repeats until all chests are sorted.
     - **Zero-Loss Safety (`FlushCarryBufferSafely`):** If combat interrupts sorting or the game exits while items are in `CarryBuffer`, all in-transit items are immediately restored to their source/destination chest.

## Real in-engine verification in a copied world

| Final acceptance case | Observed result |
|---|---|
| Container Scan without opening | Discovered **133 unique containers** in player base (`0` duplicates, `0` `"unknown_box"` IDs); toggling Drop-off on box 0 leaves box 1 untouched |
| Invisible CarryBuffer roundtrip | `TryTakeIntoCarryBuffer` + `CanAcceptWholeItem` + `FlushCarryBufferSafely` verified with 100% item conservation |
| Phase 0 — Zombie 8 m away | Acquired in 0.13 s; actual rifle shot (`weaponId=406`, `ammo 8->7`, `fireFrame -10->1625`) in 0.86 s |
| Phase 1 — Zombie 12 m away | Acquired in 0.01 s; actual rifle shot (`ammo 7->6`, `fireFrame 1625->1648`) in 0.26 s |
| Phase 2 — StopFire negative case | Immediate goal reselection (`FollowLeader`); 0 shots over full 3.0 s window |
| Phase 3 — Hostile Human NPC 8 m away | Acquired `Survivor(Clone)` in 0.06 s; actual rifle shot (`ammo 6->5`, `fireFrame -10->2240`) in 1.18 s |

Evidence: [combat-acceptance-1.4.1.log](combat-acceptance-1.4.1.log).

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
