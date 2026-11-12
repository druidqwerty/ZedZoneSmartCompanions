# 2.3.0 verification — PASS (live soak, 80-chest/15-NPC scale, AVO & UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `1274472a79bf91cfc37cb57c5922569f088b5f7b3b1de5cad201aef7c6bd925d`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.3.0` (15 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance & soak: `backups/live-20261112-161122`. Graphical matrix: `backups/ui230-20261112-161229` (300/300 uninterrupted, zero `[UiSmoke] FAIL`).

## Scope of the change

- **Performance caches:** frame-cached active teammate queries (`GetActiveTeammateAgents`), `O(1)` item-ID category memoization (`ItemCategorizer.ClassifyItem`), spatial/time world `TerrainObject` scan cache (`CollectWorldTerrainObjects`, invalidated on `force: true`), and shared `0.20 s` hostile-character snapshots (`GetActiveHostilesSnapshot`).
- **Large warehouse & 15-NPC scalability:** priority-ordered destination routing verified across 80+ marked chests and 960+ item stacks (`0.11 ms` in the live IL2CPP engine), plus non-overlapping formation slots and squad-wide profile previews for up to 15 companions.
- **AVO Expanded compatibility:** detects AVO via BepInEx IL2CPP plugin metadata and loaded assemblies, preserves AVO's `CharacterData.maxTeamNPCNumber` untouched when `MaxTeamSize == 0 (Auto)`, auto-gates Ration Sharing to `Alt+F6`, and surfaces AVO status in Settings and diagnostics.
- **Privacy-safe Diagnostic Report (`PerformanceAndDiagnostics`):** exports to clipboard and `BepInEx/config/SmartCompanions_Diagnostics.txt` with anonymized `#01..#15` companion state/role/order/reserve summaries and **zero** save data, player/companion names, Steam IDs, OS user paths or base coordinates.

## Live gameplay, scale & soak acceptance — PASS (exact final DLL)

Real game on a disposable profile copy (`backups/live-20261112-161122`):

- Native ranged combat (8 m & 12 m zombie, StopFire negative case, 8 m hostile human) — PASS.
- Physical ground-loot walk (6.10 m) & chest-to-chest carry with 4 s mid-carry pause/resume (0 teleports) — PASS.
- Tactical Hold Position (`0.19 m` within anchor) & Fall Back Here (`7.46 m` run) + live `ItemData` backpack reserve & rare-steak protection — PASS.
- **80-chest / 960-item warehouse benchmark:** priority sort + 960 real `ItemData` classifications completed in **`0.11 ms`** (`2,897` total memo hits across soak) — PASS.
- **20-cycle live engine soak + transition & co-op reconnect checks:** 20 multi-frame cycles of prioritized base work + combat + frame-cached squad queries; simulated area-transition `FlushAllCarryBuffers` (zero item loss) + `ClearCachesOnTransition` + `PruneStaleCacheEntries`; simulated co-op reconnect (`TACTICS`, `NPC` with specialization/reserves/order) — PASS.
- **Live zero-PII diagnostic audit:** generated report in the live loaded world and verified zero leakage of the real player's character name or Windows user paths — PASS.

## Graphical & deterministic regression — 300/300 + 14/14 + 12/12 + 16/16 + 13/13

- **14/14** 15-NPC formation/preview scale, AVO `MaxTeamSize = 0` cap preservation, and sentinel privacy audit checks (`ScaleAndPrivacyRegression`).
- **12/12** specialization, non-mutating squad preview, backpack reserve & rare-supply checks (`SpecializationRegression`).
- **16/16** tactical formation, doorway yield, retreat & sector checks (`TacticalRegression`).
- **13/13** co-op authority decision checks (`ProtocolRegression`).
- **300/300** actual IMGUI repaint cases across six resolutions, three languages and manual 75%/200% scales. Screenshots inspected across RU/EN/ZH, 75% and 200%: zero clipping, zero wrapping, zero missing glyphs.

## Lab isolation and restoration

Every live and matrix run confirmed **8,386/8,386** real `GameData` files SHA-256 identical; Workshop install, game install, config, logs and screen preferences restored; temporary plugins and `steam_appid.txt` removed.

## Remaining limits

A live two-player host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.
