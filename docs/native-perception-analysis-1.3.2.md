# Native perception inspection for 1.3.2

Read-only analysis of the installed ZED ZONE GameAssembly, using live IL2CPP MethodInfo function pointers converted to module-relative addresses. Raw native bytes and disassembly remain under ignored `backups/live132/`, not in distributable source.

## Confirmed call graph

- `GOAPAgent.UpdatePreceptionSystem` at RVA `0x681170` calls `UpdateAgentSight` and then the virtual hearing method. Calling it instead of `UpdateAgentSight` does not remove the sight gates below.
- `RefreshThreatsNow` at `0x674B00` is a short jump to `UpdateThreats` at `0x6811A0`. It does not independently discover unseen characters.
- `UpdateAgentSight` at `0x6799D0` iterates characters, applies targetability/faction/height/sight conditions, adds or refreshes native threat data, then calls `UpdateThreats`.

## Rendering and frame gates

1. At `0x679AE5–0x679B13`, the method checks the agent's `character.isOnScreen`. If false, it calls the agent's virtual `get_perceiveOffScreen`; false returns before scanning.
2. At `0x679B1B–0x679B46`, scanning runs only when `(Time.frameCount + agent.frameOffset) % 7 == 1`.
3. At `0x679D13–0x679D33`, each candidate character is also rejected when its `isOnScreen` is false and that same **agent** `get_perceiveOffScreen` returns false.
4. Remaining native candidate filters include targetability, differing camp modulo 10, same height level and `IsInSight` (or the game's very-close exception). Threat processing subsequently checks firing obstruction.

Verified native field offsets for this build: `GOAPAgent.character=0x28`, `frameOffset=0x70`, `threatDataDic=0x198`, `BasicCharacterController.isOnScreen=0x469`.

These offsets are analysis evidence, not a recommendation to hardcode native offsets in production.

## Patch safety

Both off-screen checks dispatch through the agent's virtual slot. The base getter returns false at RVA `0x401A70`, a tiny shared stub also used by unrelated game getters. **Do not directly detour that shared native address.** A global hook there could change unrelated behavior or reinterpret an invalid instance pointer.

A scoped main-thread refresh may decouple only the rendering optimization from perception while retaining the native targetability/faction/range/height/LOS processing. Any temporary culling flags or frame-phase changes must be restored in `finally`, without yielding or rendering between mutation and restoration.

## Freeze fixture ruled out

`BasicCharacterController.BeginScriptedMoveFreeze` (`0xC1ECC0`) stores Rigidbody state, zeros velocity, changes its body type to Kinematic and disables interpolation. `EndScriptedMoveFreeze` (`0xC27090`) restores the state. The live diagnostic confirmed `simulated=True`, active root/body colliders and an active target. This is not equivalent to disabling the target's physics object.

The failed live trace also counted hundreds of real native `UpdatePreceptionSystem`/`UpdateAgentSight` calls. Therefore the diagnosis is not merely an uncalled perception method. Render-culling state must be measured directly in the next verification run.

## Follow-up: native attack and weapon selection

- `UpdateThreats` and `GOAPAction_RangeAttack.Execute` do not directly inspect the `isOnScreen` field. Restoring camera flags after the sight scan is therefore not itself a direct camera-based threat-removal gate.
- `GOAPAgent.GetMostThreatCharacter` (`0x668FF0`) merely returns the stored `mostThreateningCharacter` field; it does not recompute the choice.
- `UpdateThreats` clears that field and chooses the greatest valid distance-adjusted threat score. Expired memories, inactive/invalid targets and native relationship checks can remove entries. Native firing obstruction lowers the score.
- `GOAPAction_RangeAttack.FindBestRangedWeapon` (`0x55EFC0`) enumerates native usable weapons, checks locks/ammunition and compares a damage/interval-style score. It does not read the target pointer, target position or target distance.
- `RangeAttack.OnActionStarted` (`0x55F8C0`) calls that selector and `DrawWeaponViaSlot`, so starting a native ranged action can replace an already equipped weapon.
- Compare ammunition only while the weapon-data identity is unchanged. A switch from a large magazine to an eight-round weapon is not proof of firing. Log effective fire range after native selection before diagnosing a failed long-distance shot.
- The subsequent runtime trace showed the eight-round weapon had **24 m** range, so a short-range swap was ruled out for that test. Repeated recreation of that same weapon, not its range, became the demonstrated failure.
- The existing `GetThreatLevelByCharacter` hook targets a reader (`0x669AC0`) which returns `threatDataDic[target].threatLevel`. Native `UpdateThreats` reads that field directly. Scaling only the reader's returned value is not evidence that native target priorities changed.

## Equip-loop inspection

- `DrawWeaponViaSlot` (`0x663770`) finds an existing item in a slot by pointer, but then unconditionally calls `HumanCharacterController.EquipWeapon_WeaponSlot`, even if that slot is already selected.
- `GOAPAction_EquipRangedWeapon.Execute` (`0x5517E0`) independently calls the same Human method directly, bypassing `DrawWeaponViaSlot`. Guarding only the latter does not stop all redundant redraws.
- Native `PlanExecute` (`0x9D4860`) treats `Execute=true` as continuing, `Execute=false` as failed/abort, and checks `IsCompleted` against action effects before execution. Equip waits 0.25 seconds, performs the equip operation and returns false; the next plan depends on refreshed native weapon state satisfying the equip effects.
- `HumanCharacterController.EquipWeapon_WeaponSlot` (`0xEA7490`) writes `CharacterEquipmentData.currentWeaponIndex` at offset `0x80` before calling the native melee/ranged equip method.
- `CharacterEquipmentData.currentWeaponItemData` is **not a cached field**. Getter `0xFA7750` reads `weaponItemDataAry` at `0x78` indexed by `currentWeaponIndex` at `0x80`. Setter `0xFA7790` calls `SetWeaponData` and therefore changes actual slot contents. Never overwrite this property as a supposed cache refresh; that can replace an inventory item.

Any consistency check must distinguish the selected equipment slot from a retained, inactive ranged-weapon visual object. Native field/method dumps confirm no `currentWeaponItemData` storage field exists.

## Follow/combat priority and slot-zero reset

- `GOAPGoal_EliminateThreat.GetPriority` (`0x8AEAB0`) returns native `currentThreatLevel`. Its command bonus (`0x8AE820`) is zero for FireAtWill, -20 for StayAtPosition, -500 for StopFire, and for FireWithPlayer is +20 only when the player fired within ten seconds (otherwise -500).
- `GOAPGoal_FollowLeader.GetPriority` (`0x8AF030`) starts at 2. Above 8 m leader distance it adds `10 * (distance - 8)`. For a threatened NPC whose non-hidden player leader is within 24 m, this priority is capped at 20. A hidden/in-vehicle leader receives an additional 500. The inherited command-priority bonus is zero.
- Native goal selection (`0x675290`) adds each valid goal's normal and command priorities and selects the largest. Thus FireAtWill alone does not guarantee priority above FollowLeader at ranged distances.
- **Exact slot-zero writer chain:** `FollowLeader.OnActionStarted` (`0x554780`) tests `isThreatened`; when true it calls `GOAPAgent.PullOutWeapon` (`0x673DF0`). That method enumerates slots 0, 1, 2, chooses the **first non-null slot**, and calls `Human.EquipWeapon_WeaponSlot` with it. It does not preserve the current or preferred ranged slot.
- This explains the runtime trace where a correct equip of slot 2 is followed by an explicit native equip of slot 0, without any slot-array mutation or stale getter cache.

Use native goal priority readbacks rather than boxed-object diagnostic strings to quantify the current fixture. A companion-specific correction must preserve cease-fire, stealth, suppressed/noncombat orders, physical visibility, weapon range and emergency priorities.

## Inconsistent native ranged-weapon eligibility

- `GOAPAction_EquipRangedWeapon.Execute` explicitly skips `weaponType` values 8 (LiquidWeapon), 5 (EnergyWeapon), 6 (EnergyChargeWeapon), and `feedingType` value 2 (Battery).
- `GOAPAction_RangeAttack.FindBestRangedWeapon` does not apply those exclusions. In fact its feeding-type-2 branch counts battery reserves. Both selectors otherwise use damage/interval-style ranking and ammunition checks.
- Runtime diagnostics subsequently demonstrated the active weapon alternating between liquid weapon item 469 and ordinary gun item 406. The ordinary gun had 24 m range; the liquid weapon had 16 m range. This disproved an insufficient-range explanation and matched the two selectors choosing different “best” weapons.
- Native layout around the enum fields is `weaponType=0xE8`, `weaponSlotSize=0xEC`, `gunActionType=0xF0`, `feedingType=0xF4`. The filter at `+0xF4 == 2` concerns **Battery**, not `ActionType.BoltAction`; ordinary bolt-action rifles must not be excluded by confusing these fields.

A non-destructive AI fix can align both selectors' eligible item list for player companions, preserving native scoring, ammunition and slot-lock logic. Return a filtered copy of the AI enumeration; do not remove, move or overwrite the actual inventory items.

## Confirmed final implementation and acceptance

- `CombatSensing` performs a bounded native scan with temporary camera flags and the accepted native frame phase; all original flags are restored in `finally` on the same main-thread call. Targetability, factions, floor, sight and firing obstruction remain native checks.
- `RangedWeaponCompatibility` returns a new eligible-list copy for player companions, matching native Equip's supported weapon/feeding types. Stored weapons and inventory arrays are not changed.
- `RangedEngagementPolicy` resolves the near-leader FireAtWill priority conflict only within 8 m of the leader and with a native-selected, physically reachable ranged target. It raises combat just above nearby FollowLeader, not above arbitrary emergency priorities; StopFire and suppressed orders retain their native handling.
- Draw/slot idempotence checks prevent repeated recreation of the exact already-held native item. They do not assign current slot contents or synthesize completed GOAP effects.

The final copied-world run is recorded in [combat-acceptance-1.3.2.log](combat-acceptance-1.3.2.log):

| Case | Native acquisition | Actual discharge | Same weapon/ammunition evidence |
|---|---|---|---|
| Target at 8 m | 0.15 s | 0.49 s | item 406, 8 → 7 rounds, fire frame advanced |
| Target at 12 m | 0.01 s | 0.42 s | same item 406, 7 → 6 rounds, fire frame advanced |
| StopFire | target remains known | no shot during the 3-second negative case | rounds unchanged |

This verifies real native sensing/planning/firing in a controlled one-hostile copied-world arena. The fixture temporarily parked 135 other enemies and held the test zombie in place. It is not a horde, long-session or co-op acceptance test. No test-only threat insertion or manual firing was used to obtain the pass.

© 2026 druidqwerty. MIT License. Analysis of user-owned local game installation; no game binary or decompiled game source redistributed.
