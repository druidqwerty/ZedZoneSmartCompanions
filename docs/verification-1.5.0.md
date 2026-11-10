# 1.5.0 verification — PASS

RELEASE_READY: true

## Final build

- Rebuilt against updated ZED ZONE interop assemblies (`2026-11-10`).
- Release build with warnings treated as errors (`--warnaserror`): 0 warnings, 0 errors.
- DLL SHA-256: `059393d525389a104734d0a78f2547666abbeb4352f725051859668f09facbc8`.
- BepInEx startup: 49 native methods patched; `[SelfVerify] PASS v1.5.0`.
- Final live verification run (`docs/combat-acceptance-1.5.0.log`): zero error entries and zero exceptions.

## Root causes & features verified in v1.5.0

1. **Block 1 — Companion Dismissal Fix (`QuitTeam` / `RemoveTeamNPC` / `UnFollowPlayer`):**
   - Previously, `MultiplayerSyncSystem.SyncCompanionOwnerAndLeader` checked `if (!string.IsNullOrEmpty(st.OwnerCharacterId) && string.IsNullOrEmpty(leaderId))` and unconditionally rewrote `npcData.leaderCharacterGUID = st.OwnerCharacterId` and `agent.leaderCharacter = leaderCtrl` on the next tick after the player dismissed a companion via `QuitTeam` or dialogue `UnFollowPlayer`.
   - Fixed by hooking `CharacterData.RemoveTeamNPC`, `CharacterData.AddTeamNPC`, `GOAPNPCData.ExecuteNPCCommand("QuitTeam" / "JoinPlayerTeam")`, and `GOAPAgent.SetLeader(null)`. `OnCompanionDismissed` flushes any carried items (`FlushCarryBufferSafely`), clears `OwnerCharacterId` / `OwnerPlayerName`, sets `IsDismissedOrNotInTeam = true`, clears `agent.leaderCharacter = null` and `agentState["hasLeader"] = false`, and `SyncCompanionOwnerAndLeader` never restores a blank `leaderCharacterGUID` unless the NPC is still in `GOAPNPCManager.instance.teammateNPCs` or `teamNPCGuids`.
   - Verified in-engine (`CombatLiveRegression`): dismissing a companion via `RemoveTeamNPC` + `ExecuteNPCCommand("QuitTeam")` followed by repeated `SyncCompanionOwnerAndLeader` ticks leaves `leaderCharacterGUID` empty and `leaderCharacter == null`; re-recruiting restores full team state.

2. **Block 2 — Draggable UI Windows (`F8`, `ChestCategoryBar`, `HudBadge`) & Safe Default Positions:**
   - Added mouse drag-and-drop (`HandleWindowDrag`) to all three UI surfaces (`MainHubWindow [F8]`, `ChestCategoryBar` expanded & collapsed, and `HudBadge` expanded & collapsed) with persistent coordinates in `CompanionConfigStore` and `ClampWindowPosition`.
   - Moved default `HudBadge` position out of the top-right minimap area to upper left-center (`x = Clamp(Screen.width * 0.28f, ...), y = 10f`) with `[-]` collapse (`F8` pill) and `[x]` hide buttons.
   - Moved default `ChestCategoryBar` position to bottom-center (`Screen.height - panelH - 92f`) so it never overlaps the top inventory header or backpack sort button.
   - Updated `IsPointerOverOverlay` to use the live dynamic `Rect`s (`_mainWinRect`, `_currentHudBadgeRect`, `_currentChestBarRect`, plus active drag state `_activeWindowDragId != 0`).

3. **Block 3 — Configurable Squad Limit (`MaxTeamSize` up to 15) & `AVO Expanded` Compatibility:**
   - Added `CompanionConfigStore.MaxTeamSize` (`0 = Auto / Vanilla 3 / AVO Compatibility`, `4`, `6`, `8`, `10`, `12`, `15`) with cycle buttons in **F8** Tab 1 and Tab 3, applying `CharacterData.maxTeamNPCNumber = MaxTeamSize` when `> 0`.
   - Added automatic `AVO Expanded` detection (`IsAvoExpandedLoaded()`) and a manual toggle (`RequireAltForRationHotkey`) switching the ration hotkey from `F6` to `Alt + F6` so pressing `F6` to open the `AVO Expanded` menu never consumes player food.

4. **Block 4 — `Basic.CollectResource` / `CollectSpecifiedItem` Roaming Fix & Base Chest Protection:**
   - Patched `GOAPGoal_FollowLeader.GetPriority` (`Postfix`) so companions ordered to `CollectResource` or `CollectSpecifiedItem` keep `FollowLeader` priority at `1.5f` up to `42 m` from the player (preventing vanilla `(dist - 8) * 10` from preempting `GOAPGoal_LootResource = 40.0` at 12 m).
   - Patched `GOAPAgentState.IsTeammateScavengeRestricted` (`Postfix`) to return `false` during `CollectResource` / `CollectSpecifiedItem` / `AllowAutoLootNearby`, and patched `GOAPAgentState.IsContainerSearchableByNPC` (`Postfix`) to return `false` for all player-marked/player-built base containers (`AutoLootSystem.IsProtectedPlayerBaseContainer`).
   - Extended `AutoLootSystem.CommandSweepNearbyLoot` to scavenge unmarked wild containers nearby while strictly excluding protected base chests.

5. **Block 5 — Obstacle-Aware Carry Pathing & Cache Pruning:**
   - When `agent.isStuck` or `agent.IsStuckRecovering` is true during physical chest carrying, movement switches from `MoveTo_Direct` to pathfinding `agent.MoveTo(st.TargetContainerPos, 1.4f, true)`.
   - Added `BaseSortingSystem.PruneStaleCacheEntries()` to clean up destroyed `TerrainObject` pointers across chunk/scene unloads.

Evidence: [combat-acceptance-1.5.0.log](combat-acceptance-1.5.0.log).

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
