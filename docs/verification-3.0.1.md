# 3.0.1 verification — PASS (native TeamNPCPanel dismissal purge & corridor navigation hotfix)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `849a81cde93e07559d455f9e80342ece00292c5dbef30e7fe04b9295fa913e9c`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **58** Harmony-patched methods; `[SelfVerify] PASS v3.0.1` (20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261114-030555`. Graphical matrix: `backups/ui301-20261114-030840` (**300/300** uninterrupted, zero `[UiSmoke] FAIL`).

## Development history (not approval evidence)

- Candidate `46663ec7…` added a Harmony patch directly on `GOAPAgent.MoveTo` (`Il2CppSystem.ValueTuple<bool, bool>` return type), which stalls IL2CPP struct-return ABI; replaced with a `bool`-returning `GOAPAgent.IsPathClear` prefix targeting only `MoveTo`'s internal `0.10f` literal (`0x3dcccccd`).
- Candidate `48442590…` caught an idle-gap `dt` accumulation in `CorridorNavigationHelper` and a `3.71 m` dual-arm's-reach shelf pair in `PhysicalCarryLiveRegression` inside the supermarket; fixed (`dt > 0.45f` reset + `sep >= 5.2 m` test chest separation) and re-verified on `849a81cd…`.

## Scope of the change

- **Native team HUD cleanup (`HarmonyPatches`, `CampGarrisonSystem_Dismiss`, `MultiplayerSyncSystem_Npcs`):** removed premature `OnCompanionDismissed` inside `ExecuteNPCCommand_Prefix` so native `OneTime.QuitTeam` sees `leaderCharacterGUID`; explicitly clears `TeamNPCPanel.RemoveNPC` + `npcUIDic` + `RefreshHeaderCount`, `TeamNPCOverheadUI`, `GOAPTeamManager`, `SetLeader(null)`, and `player.characterData.SyncTeamUI()`.
- **Corridor & doorway navigation (`CorridorNavigationHelper`):** widens `MoveTo`'s internal `0.10 m` ray check to `0.36 m` human shoulder width so doorways and corners route through A* waypoints; collapses wide formations into single-file `LeaderTrail` slots in narrow corridors; probes 7 open angles before kiting/strafing/retreating/fire-line steps instead of backing into walls; adds continuous wall-stall tangential unwedging.

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261114-030555`): ranged combat, indoor supermarket physical loot & chest-to-chest carry (`5.77 m` walked, 0 teleports), Hold Position & Fall Back Here, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover (`5.52 m` walked, 0 teleports) + live check that `TeamNPCPanel.npcUIDic` and `player.characterData.teamNPCGuids` are 100% purged, plus live `CorridorNavigationHelper` trail checks — PASS.
- Graphical & deterministic (`backups/ui301-20261114-030840`): **300/300** actual IMGUI repaint cases + all **9** deterministic regression suites.
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical; all installs, configs and screen prefs restored.

## Remaining limits

A live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.
