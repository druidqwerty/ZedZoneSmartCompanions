# 2.2.0 verification — PASS (live supply/reserve/tactical/combat + UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `e1e33e40094c84b4a9b56c994124faa670a20b784e229b5e3f5ed140b57a549c`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.2.0`.
- Live gameplay acceptance: `backups/live-20261112-154729`. Graphical matrix: `backups/ui220-20261112-155349` (300/300 uninterrupted, zero `[UiSmoke] FAIL`).

## Development history (not approval evidence)

The first candidate (`5db1e54c…`) passed live acceptance, and screenshot review of `case-009` caught that the Russian work-role button label (`Любая работа`) wrapped onto two lines inside a 24 px button. That candidate was rejected; concise Work-row labels (`Любая / Склад / Сборщик / Лагерь / Выкл`) and 26 px row buttons were applied, and both live acceptance and the full 300-case matrix were repeated on the final DLL above.

## Scope of the change

- Specialization presets (`Custom`, `Fighter`, `Medic`, `Quartermaster`, `Scavenger`) per companion, persisted in config and synced host-authoritatively in co-op.
- Non-mutating **Squad Profile diff preview** (`BuildSquadPreview`) showing exact squad-wide change counts before applying.
- Preferred weapon loadout (`Auto`, `Ranged`, `Melee`) and non-reducible backpack reserves (`ReserveAmmoMin`, `ReserveFoodMin`, `ReserveMedMin`) enforced in `BaseSortingSystem` and `SmartCombatAI.TickFieldSupport`.
- Independent permissions in Settings (`AllowSpendRareFood`, `AllowSpendRareMeds`, `AllowSpendRareAmmo`) protecting gourmet dishes, rare serums/blood bags and small-stack heavy calibers.
- Configurable automatic base work priority (`Sort -> Loot`, `Loot -> Sort`, `Camp -> Sort`), scavenging sweep radius (`14 / 24 / 36 / 48 m`) and pinned loot zone (`LootZonePinned` + `LootZoneCenter`), while base chests stay protected.

## Live gameplay acceptance — PASS (exact final DLL)

Real game on a disposable profile copy (`backups/live-20261112-154729`):

- Native ranged combat (8 m & 12 m zombie, StopFire negative case, 8 m hostile human) — PASS.
- Physical ground-loot walk & chest-to-chest carry with 4 s mid-carry pause/resume (0 teleports) — PASS.
- Tactical Hold Position (0.00 m drift) & Fall Back Here run — PASS.
- **Live `ItemData` backpack reserve protection:** `BuildProtectedBackpackPointers` kept exact 2-medkit reserve while releasing the 3rd surplus stack — PASS.
- **Live rare-dish ration guard:** `RationSharing.TryConsumePortion` preserved Spicy Meat Steak (`248`) when `AllowSpendRareFood = false` and consumed ordinary canned food (`210`) first — PASS.

## Graphical & deterministic regression — 300/300 + 12/12 + 16/16 + 13/13

- **12/12** specialization, non-mutating squad preview, backpack reserve & rare-supply checks (`SpecializationRegression`).
- **16/16** tactical formation, doorway yield, retreat & sector checks (`TacticalRegression`).
- **13/13** co-op authority decision checks (`ProtocolRegression`).
- **300/300** actual IMGUI repaint cases across six resolutions, three languages and manual 75%/200% scales. Screenshots inspected across RU/EN/ZH, 75% and 200%: zero clipping, zero wrapping, zero missing glyphs.

## Lab isolation and restoration

Every live and matrix run confirmed **8,386/8,386** real `GameData` files SHA-256 identical; Workshop install, game install, config, logs and screen preferences restored; temporary plugins and `steam_appid.txt` removed.

## Remaining limits

A live two-player host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.
