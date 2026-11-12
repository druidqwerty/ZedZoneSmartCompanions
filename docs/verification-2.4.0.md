# 2.4.0 verification — PASS (live garrison, zero-teleport safe dismiss & UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `3259a25c463adf8893e9af393314ae82d8b67f1a160735706be6ed3109e697b5`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.4.0` (17 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261113-022051`. Graphical matrix: `backups/ui240-20261113-022229` (`0..275`) + `backups/ui240-20261113-023134` (`276..299`) = **300/300** unique cases, zero `[UiSmoke] FAIL`.

## Scope of the change

- **Camp Garrison (`CampGarrisonSystem`):** `CompanionRosterMode` (`ActiveSquad`, `CampGarrison`) + `HomeAnchorPos` / `HomeAnchorZ` persisted in config and synced host-authoritatively in `SC14`. Garrisoned companions hold the camp perimeter and continue base sorting/chores without chasing the player on distant raids; `ScanNearbyContainers` continues discovering base chests near garrisoned companions while the player is away.
- **Safe two-step dismissal (`StartSafeDismiss` / `StepSafeDismissForAgent`):** inline confirmation strip on companion cards (`[x] Hand over gear first` + **Confirm Leave** / **Cancel**). When enabled, the companion physically walks (`<= 2.45 m`, zero teleportation) to a marked base chest or the player, atomically transfers carried items, backpack contents, weapons and armor (calling `HumanNpcEquipmentGuard.CleanupBrokenRangedState` + `RefreshAllEquippedItemVisuals`), and only then leaves the squad.
- **Co-op sync health telemetry:** `PacketsSent`, `PacketsReceived`, `RejectedCompanionChanges`, `CountHostReplicatedHumanNpcs`, and `CountClientBoundHumanPuppets` surfaced in **F8 → Settings** and `BuildPrivacySafeReport` without names, paths or coordinates.

## Live gameplay acceptance — PASS (exact final DLL)

Real game on a disposable profile copy (`backups/live-20261113-022051`):

- Native ranged combat (8 m & 12 m zombie, StopFire negative case, 8 m hostile human) — PASS.
- Physical ground-loot walk (6.99 m) & chest-to-chest carry with 4 s mid-carry pause/resume (0 teleports) — PASS.
- Tactical Hold Position (`0.00 m` drift) & Fall Back Here (`7.17 m` run) + backpack reserves & rare-steak protection — PASS.
- 80-chest / 960-item warehouse (`0.10 ms`) + 20-cycle live soak + transition & co-op reconnect checks — PASS.
- **Camp Garrison Phase 0:** companion held within `0.40 m` of camp anchor for `2.5 s` while player moved `12.0 m` away on a raid — PASS.
- **SafeDismiss Phase 1:** companion walked `4.77 m` to the handover target (`0` item teleportations at `t = 0`), transferred inventory item on arrival, and completed clean dismissal — PASS.

## Graphical & deterministic regression — 300/300 + 8/8 + 14/14 + 12/12 + 16/16 + 13/13

- **8/8** garrison roundtrip persistence, host non-owner spoof rejection, sync packet reception & coordinate-free telemetry privacy checks (`GarrisonRegression`).
- **14/14** 15-NPC scale, AVO compatibility & zero-PII diagnostic checks (`ScaleAndPrivacyRegression`).
- **12/12** specialization & reserve checks (`SpecializationRegression`).
- **16/16** tactical formation, doorway yield, retreat & sector checks (`TacticalRegression`).
- **13/13** co-op authority decision checks (`ProtocolRegression`).
- **300/300** actual IMGUI repaint cases across six resolutions, three languages and manual 75%/200% scales (`276 + 24` segments). Screenshots inspected across RU/EN/ZH and 200% fit-cap: zero clipping, zero wrapping, zero missing glyphs.

## Lab isolation and restoration

Every live and matrix run confirmed **8,386/8,386** real `GameData` files SHA-256 identical; Workshop install, game install, config, logs and screen preferences restored; temporary plugins and `steam_appid.txt` removed.

## Remaining limits

A live two-machine / two-account host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.
