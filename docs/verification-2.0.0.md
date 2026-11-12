# 2.0.0 verification — PASS (live gameplay + UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `255b1941592e037cd809ef70688819340fc4ff7ba2b1dae3528e9c5a20a07d44`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.0.0`.
- Live gameplay acceptance: `backups/live-20261112-035652`. Graphical matrix: `backups/ui200-20261112-035736` (one uninterrupted run, zero `[UiSmoke] FAIL`).

## Rejected candidates (development history, not approval evidence)

| DLL | Live | Matrix | Reason rejected |
|---|---|---|---|
| `68ff1827…` | PASS | stopped at 93 | Pause/Resume symbols (U+23F8, U+25B6) drawn as boxes: the interface font has no glyph. |
| `d902d1b1…` | PASS | stopped at 83 | A hover tooltip from the Squad tab stayed visible on the Work tab. |
| `b9594dd8…` | PASS | stopped at 85 | Resetting `GUI.tooltip` did not clear it in this IL2CPP build; tooltip still stale. |

Fixes: unsupported glyphs (including the 1.9.0 lock symbol and existing emoji in task texts/banners) are removed at draw time via `UiSafe` using `Font.HasCharacter`; only tooltips registered by controls drawn in the current hub draw may be shown.

## Scope of the change

- Work roles per companion (Any / Storekeeper / Scavenger / Camp keeper / No base work), persisted as field 15 of the `NPC|…` config line; old configs load as Any.
- Roles gate automatic auto-loot, chores, trunk unloading, auto-dropoff and whole-squad sort orders. A direct order to one companion ignores the role.
- Global pause: no new automatic work; companions with a physical job keep the job and `CarryBuffer`, keep stall timers fresh and follow the player natively; resume continues the job. Direct whole-squad sort/loot orders end the pause.
- Cancel one/all: `CancelAllPhysicalTasksOnCombat` path, carried items returned to the companion's own inventory (`ReturnCarryBufferWithoutTeleport`), reason "cancelled by player".
- Co-op: role is field 8 of the NPC packet (host-authoritative, 1.9.0 rules); pause is a `PAUSE|0/1` packet and part of the host's full resync.
- F8: new WORK tab (index 4, shown second). Tab indices of existing tabs are unchanged.

## Live gameplay acceptance — PASS (exact final DLL)

Real game, disposable copy of the profile, isolated live-lab plugin:

- Native ranged combat: zombie targets at 8 m and 12 m, StopFire negative case, hostile human at 8 m — PASS.
- Phase A, ground loot: companion walked 6–7 m and picked up at ≤2.45 m — PASS, 0 teleports.
- Phase B, chest-to-chest carry with **new pause check**: after the pickup entered `CarryBuffer`, base work was paused for 4 s; the job stayed active, `CarryBuffer` stayed 1 and nothing was deposited. After resume the item was carried and deposited at the destination (minDestDist ≈1.1 m) — PASS, 0 teleports.
- The same acceptance also passed on each rejected candidate (UI-only differences).

## Graphical regression — 300/300

Ten profiles (1280×720, 1920×1080, 2560×1440, 3840×2160, 3440×1440, 1024×768 Auto; 1920×1080 and 1280×720 at 75% and 200% fit-capped) × RU/EN/ZH × ten variants (the nine 1.8.x variants plus the new Work tab; the English Work case shows the paused header). The co-op decision table passed 13/13 in the same run. Work-tab screenshots inspected at 1280×720, 1920×1080, 75% and 200% in all three languages: five tabs fit, no boxes, no stale tooltip, no clipping.

## Lab isolation and restoration

Every live and matrix run: **8,386/8,386** real `GameData` files SHA-256 identical; Workshop install, game install, config, logs and screen preferences restored; temporary plugins and `steam_appid.txt` removed.

## Remaining limits

A real two-player host + client session (role/pause sync, human NPC visibility) and mouse-wheel injection remain unverified. Roles and pause were not soak-tested over long sessions; the Work tab with busy companions was exercised only in the live lab, not in screenshots.
