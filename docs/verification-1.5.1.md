# ZED ZONE Smart Companions v1.5.1 — experimental release verification

RELEASE_READY: false
PUBLICATION_STATUS: owner-authorized experimental release; co-op visibility acceptance remains incomplete

## Reported issue and evidence

Owner reports that a friend cannot see human NPCs in co-op. Inspection of the local host's `Player.log` from a 2026-11-10 multiplayer session confirms a peer joined; human looters then repeatedly threw `NullReferenceException` in `HumanCharacterController.Init -> LoadEquipments -> EquipRangedWeapon` while the world started, before the ordinary 0.65-second host NPC scan.

Current-build native disassembly of `HumanCharacterController.LoadEquipments` confirms it calls `EquipRangedWeapon` on the saved current ranged weapon. That native method looks up `ItemAttr`, creates the ranged-weapon visual prefab, and initializes it. The existing serialization normalization cannot repair a missing weapon attr/prefab at this point in the flow, and it happened after NPC initialization started.

This is strong evidence of a human-NPC initialization failure that can prevent replication. It is not, on its own, proof that this explains every missing NPC on the friend's client.

## Experimental change

The owner explicitly authorized publication of version **1.5.1** as an experimental update despite the open co-op acceptance test. It must not be described as a confirmed fix for client NPC visibility.

- Add a `HumanCharacterController.Init` prefix that normalizes non-player human `characterAttrId` to the existing Survivor/Looter attrs (201/202) and applies the existing hostile-Looter camp remap before the native init sequence.
- Add `HumanNpcEquipmentGuard`: for local human NPCs only, skip ranged equipment data with a missing/non-ranged `ItemAttr`, catch a native `EquipRangedWeapon` failure during visual construction so a bad saved/mod weapon cannot abort the remaining NPC initialization, and skip `UpdateHand_RangedWeapon` when there is no valid ranged weapon. The intent is to leave a malformed-weapon NPC unarmed instead of preventing it from existing/replicating.
- The guard applies only while multiplayer is online, and excludes the local player and network-player puppets. The `EquipRangedWeapon` finalizer is confined to that method and human-NPC path; `UpdateHand_RangedWeapon` has only a narrow prefix that skips an absent/invalid ranged weapon, with no finalizer swallowing its animation exceptions. The vanilla behavior remains on single-player and for player avatars.
- Existing `NetMonsterSync` serialization/deserialization normalization and client puppet visibility repair are unchanged.

## Verification performed

- Release build with `--warnaserror`: **0 warnings, 0 errors** after the final guard changes.
- Final narrowed candidate DLL SHA-256: `3252934c6414f4b8d3a69d6f35146ed8192aa109817038a989e30fc1a3f5b53b`.
- Isolated BepInEx startup of this exact DLL loaded `ZED ZONE Smart Companions v1.5.1`, patched **52** methods, passed `[SelfVerify]`, and reached `Chainloader startup complete`; no Harmony/plugin errors were recorded. The run used `-batchmode -nographics`; it did not load a game world. The full local log is retained in project backups but is not redistributed.
- All 8,367 save `GameData` file hashes matched before/after the isolated startup. Local hash manifests are retained with project backups and are not redistributed; `steam_appid.txt` was removed after the run.
- Current-game interop metadata confirms the targeted native method signatures. The Harmony startup patch count increased from 49 to 52, confirming the three added patch points resolved.
- The release ZIP contains the same DLL hash above; ZIP integrity, manifest, and embedded binary were checked before publication.

## Not yet verified — release gate

The isolated batch-mode startup does **not** enter the co-op world, trigger the bad NPC loadout, verify an NPC remains present/visible, or spawn a client puppet. The owner and friend have not yet run this version together. Therefore the friend's missing-NPC issue is **not confirmed fixed**. The owner authorized an experimental public release before that test; the co-op fix remains unverified and this is not a verified resolution.

Real acceptance still requires the same 1.5.1 DLL loaded on both host and friend. In co-op, check whether the friend sees a nearby human Survivor and Looter; then inspect both players' `Player.log` and BepInEx logs for `[MP NPC]`, `HumanCharacterController.Init`, `EquipRangedWeapon`, `UpdateHand_RangedWeapon`, and `SpawnPuppet` errors. If the visual equipment is invalid, the NPC is intended to spawn unarmed and the guard should log why. Verify Code Mod Loader actually loads v1.5.1 on both PCs; if Workshop delivery fails on either machine, install the DLL locally on that machine and confirm the startup version in its BepInEx log.

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
