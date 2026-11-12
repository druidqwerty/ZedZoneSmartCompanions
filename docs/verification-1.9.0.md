# 1.9.0 verification — PASS (scoped: co-op rules + UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `dfc491cea076134c34e435a32e76099c1f32672c77c7f955df928ee58a6286b4`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v1.9.0`.
- Evidence: `backups/ui190-20261112-034004` (one uninterrupted run), zero `[UiSmoke] FAIL` lines.

## Scope of the change

- NPC packets now carry `revision` and `sender character id` (`NPC|guid|doctrine|ownerId|ownerName|rev|sender`).
- The host runs `MultiplayerSyncSystem.DecideCompanionChange` on every client NPC packet: a non-owner may not change an owned companion, nobody may assign an unowned companion to a third player, and revisions at or below the host's are rejected as stale.
- On rejection the host re-broadcasts its authoritative state (no revision bump) so the sender's UI reverts. On acceptance the host echoes the stamped revision to everyone, including the sender.
- Clients adopt `max(own, host)` revision, so quick consecutive changes by the owner are applied in order.
- Legacy 1.8.x packets (no revision/sender) are accepted with the previous behavior; 1.8.x ignores the extra fields.
- F8 → Squad: companions owned by another player show "🔒 view only" with the owner's name and disabled controls (multiplayer only).
- Single-player paths are unchanged.

## Co-op decision table — 13/13 in the real game

Run inside the real IL2CPP game process by the UI harness against the production DLL:

| Case | Expected |
|---|---|
| Owner changes own companion | Accept |
| Owner ID differs only by case | Accept |
| Other player changes owned companion | Reject (not owner) |
| Other player tries to take ownership | Reject (not owner) |
| Unowned companion claimed by sender | Accept |
| Unowned companion assigned to a third player | Reject (not owner) |
| Same revision as host | Reject (stale) |
| Older revision | Reject (stale) |
| Legacy packet without revision | Accept |
| Legacy packet without sender | Accept |
| Rapid owner proposal rev 6 | Accept |
| Rapid owner proposal rev 7 | Accept |
| Simultaneous proposal on the same base | Reject (stale; first wins) |

## Graphical regression — 270/270

Ten profiles (1280×720, 1920×1080, 2560×1440, 3840×2160, 3440×1440, 1024×768 in Auto; 1920×1080 and 1280×720 at 75% and 200% fit-capped) × RU/EN/ZH × nine variants. Input gating, attack isolation, modal movement reset and coordinate roundtrips passed. Screenshots inspected at 200% and 1024×768 in English: labels on one line, hover hints inside the window, no clipping.

## Lab isolation and restoration

Disabled native fixtures in the main menu; no save loaded, no fixture AI. **8,386/8,386** `GameData` files SHA-256 identical; installation, configuration, logs and screen preferences restored; temporary test plugin removed.

## Remaining limits

The network transport between two real game instances (host + client) was **not** exercised: it cannot run on one PC with one Steam account. The view-only card state appears only online and is not covered by the menu fixtures. Human NPC visibility in real co-op and mouse-wheel injection remain unverified.
