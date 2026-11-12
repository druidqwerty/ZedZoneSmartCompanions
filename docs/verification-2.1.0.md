# 2.1.0 verification — PASS (live tactical/combat/carry + UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `0e337104c3bb14b2c30fa390dde67a9e2490b62b832f13a6d0be3ebffd640fc9`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.1.0` (15 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261112-152155`. Graphical matrix: `backups/ui210-20261112-152316` (300/300 uninterrupted, zero `[UiSmoke] FAIL`).

## Development history (not approval evidence)

The first candidate (`36baca13…`) revealed that in `FollowLeader_GetPriority_Postfix`, when `StopFire` cease-fire was active while an enemy remained in sight (`mostThreateningCharacter != null`), `CollectResource` follow-leash suppression returned early. Fixed by treating `mostThreateningCharacter` as an active combat engagement only when combat is not suppressed (`StopFire` / `Passive` / `IsTeammateCombatSuppressed`). Rebuilt and re-verified from scratch on `0e337104…`.

## Scope of the change

- Formations (`Free`, `Wedge`, `Line`, `Column`, `GuardRing`) and follow distances (`2.5 m`, `4 m`, `6.5 m`, `9 m`), persisted in config and synced in co-op (`TACTICS|form|dist`).
- Positional combat orders (`Follow`, `HoldPosition`, `FallBackHere`, `CoverDirection`) squad-wide, per companion, and via 4 new native radial NPC commands, synced host-authoritatively in co-op.
- Narrow-passage single-file yield (`ShouldYieldInNarrowPassage`) and anti-clumping ally spacing (`ComputeAllySeparatedPoint`).
- Configurable wounded (`Off / <25% / <30% / <50% HP`) and low-ammo (`Off / 0 ammo / <=5 ammo`) disengage thresholds, stepping back behind the leader and displaying the active reason on the companion card.
- Tactical status & block reasons (`no path to anchor`, `retreat: wounded`, `retreat: out of ammo`, `cease-fire active`, `on base task`, `different floor`) on every companion card.

## Live gameplay acceptance — PASS (exact final DLL)

Real game on a disposable profile copy (`backups/live-20261112-152155`):

- Native ranged combat: 8 m & 12 m zombie discharge, StopFire negative case, `CollectResource` suppression, and 8 m hostile human discharge — PASS.
- Physical ground-loot walk (6.38 m) and chest-to-chest carry with 4 s mid-carry pause/resume — PASS, 0 teleports.
- **Tactical Phase 0 (Hold Position):** companion anchored at hold point while player moved 9.0 m away; **0.00 m chase drift** over 2.5 s — PASS.
- **Tactical Phase 1 (Fall Back Here):** companion ran **7.17 m** to the fallback point (`finalDist = 1.83 m`) in 1.36 s — PASS.
- **Tactical Phase 2 (Cover Direction & Formation restore):** sector threat weighting verified (`+65%` forward, dampened rear) and formation restored — PASS.

## Graphical & deterministic regression — 300/300 + 16/16 + 13/13

- **16/16** tactical math checks (`TacticalRegression`: monotonic follow distances, non-overlapping slots across all 5 formations, wedge symmetry, column depth, ally separation, narrow doorway yield, wounded & low-ammo retreat rules, sector weighting) inside the real game process.
- **13/13** co-op authority decision checks (`ProtocolRegression`).
- **300/300** actual IMGUI repaint cases across six resolutions (1024×768 to 3840×2160, including 3440×1440), three languages (RU/EN/ZH) and manual 75%/200% scales. Screenshots inspected across RU/EN/ZH, 75% and 200%: zero clipping, zero wrapping, zero missing glyphs.

## Lab isolation and restoration

Every live and matrix run confirmed **8,386/8,386** real `GameData` files SHA-256 identical; Workshop install, game install, config, logs and screen preferences restored; temporary plugins and `steam_appid.txt` removed.

## Remaining limits

A live two-player host + client session (human NPC visibility and live two-player tactical sync) and automated mouse-wheel injection remain unverified.
