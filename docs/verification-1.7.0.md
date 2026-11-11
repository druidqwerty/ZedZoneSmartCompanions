# ZED ZONE Smart Companions v1.7.0 — Physical Looting & Item Transfer Verification

- **Version:** `1.7.0`
- **Build:** `dotnet build src\SmartCompanions\SmartCompanions.csproj -c Release -t:Rebuild --no-restore -warnaserror` -> `0 warnings, 0 errors`
- **Verified DLL SHA-256:** `e2fefde9349acec87fb5f20792eb96f7ee1bfd81f8cca93d12ea15ff63017224` (`371,200` bytes)
- **Harmony Patches Bound:** `57`
- **Startup `[SelfVerify]`:** `PASS v1.7.0`
- **Live Engine Acceptance (`docs/combat-acceptance-1.7.0.log`):** `PASS` (Phases 0–3 Combat + Phase A Physical Ground Loot Walk + Phase B Physical Chest-to-Chest Carry Sort)
- **Save Integrity (`GameData`):** `8,386 / 8,386` files byte-for-byte SHA-256 identical before and after live runs (`gamedata_identical: true`)
- **Item Teleports Observed:** `0` (`minDistObserved <= MaxReachableInteractRadius = 2.45m` for all ground pickups, source chest pickups, and destination chest deposits)
- **Weapon/Hand IK NREs in `Player.log`:** `0`

## What Changed & Was Verified in v1.7.0

1. **Zero-Teleport Physical Movement & Interaction (`PhysicalMovementHelper.cs`)**
   - Unified all companion physical jobs (Ground/Corpse/Wild-Container Loot Sweep, Base Chest Sorting, and Base Chores) under `GOAPAgent.UpdateGoal -> PhysicalMovementHelper.TickPhysicalFrameForAgent`.
   - Uses `ComputeSurfaceDistance(selfPos, targetPos, targetAnchor)` against the target `Collider2D.ClosestPoint(selfPos)` so large furniture/shelves are measured to their outer edge rather than their interior center.
   - Navigates via native A* `agent.MoveTo(approachPoint, 1.15f, true)` when `agent.IsPathClear` detects an obstacle or when `st.StuckFrames >= 10`, with a brief arrival interaction pause (`0.35s–0.45s`) facing the item/chest before any item is removed or deposited.
   - Removed all instant/remote item transfer paths (`instant` parameter in `CommandSortBase`, remote timeout fallback in `BaseChoreSystem`, and instant ground/corpse/container loops in `CommandSweepNearbyLoot`).

2. **Physical Ground, Corpse & Wild-Container Loot Queue (`AutoLootSystem_JobRunner.cs`, `AutoLootSystem_Transfer.cs`)**
   - `CommandSweepNearbyLoot` scans within the requested radius, builds a distance-ordered `st.LootQueue` of reachable targets (`GroundItem`, `Corpse`, `WildContainer`), and assigns each companion to walk from target to target.
   - `ExecutePhysicalPickupAtStop` verifies `ComputeSurfaceDistance <= MaxReachableInteractRadius (2.45m)` at the moment of arrival before transferring items into the companion's inventory.

3. **Physical Base Chest Carry & Safe Non-Teleporting Rollback (`BaseSortingSystem_Worker.cs`, `BaseSortingSystem_PhysicalCarry.cs`, `BaseSortingSystem_CarryOps.cs`)**
   - Companions walk to the source chest (`SortingPhase = 1`), pause to take items into `st.CarryBuffer` only after reaching `surfaceDist <= 2.45m`, then walk to each destination category chest (`SortingPhase = 2`) and deposit items only upon physical arrival.
   - If combat interrupts a carry job or a destination chest becomes unreachable, `ReturnCarryBufferWithoutTeleport` keeps the carried items in the companion's own inventory (`sharedInventoryData` / `inventoryData`) or drops them at the companion's feet (`DropItem`) — never teleporting items back across the map.

4. **6-Phase Live ZED ZONE Engine Acceptance (`docs/combat-acceptance-1.7.0.log`)**
   - **Phase 0 (`8m` Zombie):** acquired in `0.30s`, native rifle discharge in `1.33s` (`ammo 5->4`).
   - **Phase 1 (`12m` Zombie):** acquired in `0.06s`, native rifle discharge in `0.30s` (`ammo 4->3`).
   - **Phase 2 (`StopFire` Cease-Fire + v1.6.0/v1.7.0 In-Engine Checks):** `0` shots over `3.0s`, all player/looter/survivor/UI/categorizer checks `PASS`.
   - **Phase 3 (`8m` Hostile Human `Looter(200)`):** acquired in `0.09s`, native rifle discharge in `1.79s` (`ammo 3->2`).
   - **Phase A (Physical Ground Loot Walk):** companion walked `6.77m` to ground item (`arrivalDist = 1.40m <= 2.45m`) and picked it up after `1.67s` with `0` teleports.
   - **Phase B (Physical Chest-to-Chest Carry Walk):** companion took item from source `supermarket shelf` (`minSrcDist = 0.45m <= 2.45m`, `CarryBuffer = 1`), walked `1.64m` to destination `supermarket shelf` (`minDestDist = 1.56m <= 2.45m`), and deposited it after `1.42s` with `0` teleports.
