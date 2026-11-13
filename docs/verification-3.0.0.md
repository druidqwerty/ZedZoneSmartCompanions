# 3.0.0 verification — PASS (full live suite + vehicle/zLevel/pets + 300-case UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `5b9ed3d28848a1f5dfd0be117a625da11d6a95e20eb3ce22807847bf0b8002ab`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v3.0.0` (20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261113-135603`. Graphical matrix: `backups/ui300-20261113-142451` (**300/300** uninterrupted, zero `[UiSmoke] FAIL`).

## Scope of the change

- **Organized vehicle crews & overflow policy (`ExpeditionSystems.PlanVehicleSeatAllocation` / `TickVehicleExpedition`):** prioritizes critically wounded teammates (`< 50% HP`), `Medic`s and `Fighter`s for available passenger seats on `BasicVehicle`, holds overflow squadmates cleanly in standby/garrison instead of crowding vehicle doors, assists pet boarding (`GOAPAgent_Pet.TryBoardOwnerVehicle`), and dispatches `Supplier` specialists to physically inspect parked vehicle bodywork (`<= 2.45 m`).
- **Physical multi-floor & bunker transitions (`TryStepMultiFloorTransition`):** locates the nearest matching `TerrainObject_Door_UndergroundEntrance` or `TerrainObject_DugShaft` on the companion's current `zLevel`, walks physically to the portal (`<= 2.15 m`, zero ceiling teleportation), computes `GetMigrateDestination(targetZ)`, and atomically updates `npcData.zLevel` + `characterData.zLevel` while clearing spatial caches.
- **Pet companion synergy (`GetActivePlayerPetsSummary`, `TickPetCompanionSynergy`):** displays live friendly pet counts in **F8 → Squad**, coordinates pet escort, and dispatches physical pet feeding (`PetSystem.IsEdibleForPet` + `NotifyFoodDropped`) while preserving non-reducible food reserves and rare-dish protection.

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261113-135603`): native ranged combat (8 m & 12 m zombie, StopFire negative case, 8 m hostile human), physical loot & chest carry with 4 s mid-carry pause/resume (0 teleports), Hold Position & Fall Back Here, backpack reserves & rare-steak guard, 80-chest / 960-item warehouse (`0.11 ms`) + 20-cycle soak, Camp Garrison perimeter hold (`0.40 m`), SafeDismiss physical handover (`5.19 m`, 0 teleports), `GrenadeSafetyGate`, `SavedEverydayMaskByGuid`, `IsApprovedStationFuelItem`, and live `PlanVehicleSeatAllocation` + `PetSystem` verification — PASS.
- Graphical & deterministic (`backups/ui300-20261113-142451`): **300/300** actual IMGUI repaint cases across six resolutions, three languages and manual 75%/200% scales + all **9** deterministic regression suites (`ExpeditionRegression`, `IndustryRegression`, `SurvivalRegression`, `FieldTacticsRegression`, `GarrisonRegression`, `ScaleAndPrivacyRegression`, `SpecializationRegression`, `TacticalRegression`, `ProtocolRegression`). Screenshots inspected across RU/EN/ZH: zero clipping, zero wrapping, zero missing glyphs.
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical; all installs, configs and screen prefs restored.

## Remaining limits

A live two-machine / two-account host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.
