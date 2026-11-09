# ZED ZONE — Smart Companions (Умные напарники) — MODLOG

## Status: 1.3.2 VERIFIED; publication in progress (prior blockers resolved)

- **Game:** ZED ZONE (`1211600`, Unity `2023.1.18f1` IL2CPP x64)
- **Loader:** BepInEx `6.0.0-be.793` IL2CPP + `ZedModsLoader` `1.1.0` (Workshop `3834614258`)
- **Installed Mod Path:** `G:\Steam\steamapps\common\ZED ZONE\Mods\SmartCompanions\SmartCompanions.dll` (+ `mod.json`)
- **Source Path:** `C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions\src\SmartCompanions\` (и синхронизировано в `G:\Steam\steamapps\common\ZED ZONE\ZedModKit\src\SmartCompanions\`)
- **Save Backup:** `C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions\backups\ZEDZONE-saves-backup`

## Historical 1.0.0 Startup Evidence (`BepInEx\LogOutput.log` & `ZedModsLoader-report.txt`)
- `Patched 13 game method(s) via Harmony.`
- `Registered SmartCompanionsController MonoBehaviour in IL2CPP domain.`
- `[SelfVerify] PASS: 44 chatter topics (171 RU lines, 92 EN lines), 12 storage categories, 8 custom NPC commands verified.`
- `Code mods loaded: 1 — ZED ZONE Smart Companions 1.0.0 [BepInEx plugin]`

## Completed Files
- [x] `SmartCompanions.csproj`
- [x] `CompanionModels.cs`
- [x] `ItemCategorizer.cs`
- [x] `ChatterDatabase.cs`
- [x] `ChatterDatabaseCombat.cs`
- [x] `SmartChatterSystem.cs`
- [x] `SmartCombatAI.cs`
- [x] `BaseSortingSystem.cs`
- [x] `AutoLootSystem.cs`
- [x] `HarmonyPatches.cs`
- [x] `SmartCompanionsUI.cs`
- [x] `SmartCompanionsUI_Hub.cs`
- [x] `SmartCompanionsPlugin.cs`

## Cover 2026 — completed (2026-11-09)

- Goal: square series-style Steam Workshop cover for ZED ZONE: Smart Companions.
- [x] docs/cover-2026-master.png: original image_generate output, 1254 x 1254, byte-identical to source.
- [x] docs/preview.png: Pillow LANCZOS optimized PNG, 512 x 512, 532393 bytes (less than 1 MiB).
- [x] workshop/preview.png: byte-identical copy of docs/preview.png.
- [x] docs/cover-2026-prompt.md: exact prompt, generation details, dimensions, byte counts and SHA-256 hashes.
- [x] Master and final 512px preview visually inspected via view_image; all required text correctly spelled and readable.
- Gotcha: provider ignored aspectRatio override; actual generated output is square, so no crop required.
- Next: no remaining cover tasks. No upload or publication performed by this artwork task.

## 1.3.1 UI repair — 2026-11-09 (in progress)
- User reports: F8 has blank companion content on every language; Chinese labels are squares; LMB in F8 attacks and destroys base objects.
- Starting state: clean git HEAD 4046a60, released plugin 1.3.0 (csproj incorrectly still 1.2.0). No running ZEDZONE process.
- Existing log repeats `SmartCompanionsController.OnGUI exception: Method unstripping failed`; quick row renders before stripped scroll view aborts.
- Owners: UI font/scroll repair; gameplay input gate; public Steam feedback read; root integration/live regression/release.
- [x] Plugin/assembly version unified at 1.3.1; GUI failures retain full exception and throttle duplicates to 30s.
- Next: complete UI and game input fixes, live graphical verification (startup tests alone did not exercise GUI), package and synchronize GitHub/Steam/Boosty.
- [x] First graphical run (not release acceptance): native YaHei glyph checks passed, real populated cards rendered in RU/EN/ZH. Found settings tab unguarded player access in main menu; fixed and settings column made scrollable. Re-run must fail on any UI fallback (first probe had counted fallback as repaint).
- [x] Steam public feedback: all 7 comments + 2 pinned empty discussions, archived docs/feedback-1.3.1.md. Independent material-gathering trip is a separate feature suggestion, not part of this hotfix.
- [x] Backups before verification/install: backups/ui131-before (old DLL/manifest/config/logs plus full GameData). No save opened in menu-only UI fixture tests.
- [x] CompanionModels JSON summary now takes version from PluginVersion (removed hardcoded 1.3.0); both Workshop content layouts must receive identical final 1.3.1 DLL/manifest.
- [x] FINAL graphical run backups/ui131-final8: PID 66408 exited 0, `[UiSmoke] PASS 15/15`, font Latin/Cyrillic/Chinese coverage verified, native player Fire/Fire_Gun/MeleeAttack/held-melee blocked while modal, companions allowed, closing-frame quarantine and full release restoration PASS; 0 error entries, 0 mod warning entries.
- [x] Removed temporary probe from game Mods by moving exact verified directory to backups/ZUiSmoke-verified. Source harness remains tools/UiSmoke, not release content.
- [x] 8,355 GameData files matched backup SHA256s; player saves never loaded. Tests use inert native fixtures in graphical engine; no damage/physics test on loaded base is claimed.
- [x] Final DLL SHA256 1d1f58ed2d4375c216555a2c882d832f6a759cc0587d35116c56ac9b7c1ac932. ZIP126795 bytes SHA2564d861a2f9f30ba9f1758dd66ea1ba1c3becc452dbb96713d4d6a8fbb5a51886f. Both staging layouts and game/kit synchronized.
- Gotcha: patch concrete Human weapon overrides only. Additional Basic Fire_Gun/Melee merged/no-op stubs caused early native exit; removed. Fixture movement setter needs animation rig, so native CharacterInput own field is used. GUI color tests compare engine-normalized readback, not raw hardcoded color components.
- Next: GitHub commit/push/release, Steam upload EN/RU/ZH visibility0 and dependencies/topics verification, Boosty queue.

- [x] RELEASED 1.3.1: GitHub runtime/source commit 4d3e2f60804c3777c0e82892c325c4cd7d442efa, release https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.3.1.
- [x] Final ZIP expanded to include screenshot/log documentation: 1,262,503 bytes, SHA256 bc52644b75c1c4f3348cc0f435f94e0fc1c21082c607de4e081f3e2835e16ed2 (supersedes earlier packaging-only ZIP checksum above). Anonymous download byte/hash + manifest + DLL checks passed.
- [x] Steam upload content, preview, EN/RU/ZH pages acknowledged; dependency3834614258 already present EResult29. Anonymous API result1, visibility0, file_size576732, title/description version1.3.1.
- [x] Correctforum40676593 AJAX verified exactly2 pinned topics (570428419877202927,570428419877202938). Public EN/RU/ZH HTML returned429; post-upload localized HTML/sidebar rendering not claimed verified. Evidence docs/publication-1.3.1.json.
- [x] Boosty1.3.1 queued, replaced only pending1.3.0 entry; other projects untouched. Queue acceptance is not publication.
- Completed. No game process or temporary probe remains active. Full boundary/limitations in docs/verification-1.3.1.md.

## 1.3.2 native menu / combat audit (in progress)
- Owner reports native companion commands blank and late threat reaction. Different UI from repaired F8.
- Native runtime probe confirmed vanilla data commands are unprefixed Basic.FollowPlayer etc. Injected NPCCommand: prefix creates extra blank groups. Native label lookup receives bare leaf name, not full path; existing label hook missed it.
- Combat audit: invalid prefixed doctrine state, no proactive native perception refresh, sorting skips combat entirely, anti-friendly-fire moves even when merely carrying a gun. Fix owners separate.
- Broader audit: player-centered passive loot, XP only persisted on rankup, free unlimited manual ration buff, ignored inventory partial merges. Investigating/fixing with quantity-conservation probes.
- Lab profile copied to backups/live132/profile; temporary LiveSmoke redirects GameConfigs save/config getters there and suppresses cloud saves. Original profile must remain hash-identical.

- CIRCUIT BREAKER: three repeated native combat failures `no native shot within12s phase=0, distance=8,00m acquired=False, sight=28,00, command=FireAtWill`. Stopped repeated launches and informed owner. Final valid fixture: enabled NPC/zombie, LOS/inSight true, hostile samelevel, suppressedFalse, FollowPlayer, threats0. Investigate UpdatePreceptionSystem/native threat acquisition next; do not force threats/shots to pass.
- Menu actualnativepanels RU/EN/ZH + exactcanonicalcommands and inventoryconservation/food tests PASS. Build0warnings0errors. No1.3.2publication orBoostyqueue. See docs/verification-1.3.2.md with RELEASE_READY:false; package_v132 refuses publication package until true.
- Local game restored verified1.3.1 DLL+manifest. Testpluginsmoved to backups/live132 disabled; no gameprocess. Original8357GameDatafiles SHA256 unchanged. CandidateDLL/failedlog preservedbackup.

## Resume authorized: owner says «чини и в релиз»
- Continued from e4399ae; no completed menu/inventory work restarted. New diagnostic launch observed native GOAP Update/perception actively running, started=true, goapPause=false, target physics simulated=true. Missing UpdatePreceptionSystem and frozen physics ruled out.
- Exact GameAssembly native analysis: UpdateAgentSight returns when own/candidate isOnScreen false (both gates use agent.perceiveOffScreen; shared tiny false getter unsafe to detour), and only runs when (frameCount + frameOffset) % 7 == 1. RefreshThreatsNow only updates existing threats. Candidate's torso/sprite render children are inactive in current test.
- Implement scoped original-native sensing: temporarily expose bounded same-floor visible candidates to sight scan, align frame phase, run original native UpdateAgentSight, restore render flags/frameOffset in finally. No manual threats, targets or shots. Keep native hostility/targetability/visibility and cease-fire policies. Native dump retained backups/live132/profile/native-addresses.json, no game binary/source redistribution.
- Native camera/frame fix now admits the real 8m target in 0.06–0.19s; initial ammo25000→8 was a weapon SWAP, notshot. Shot test strengthened to require same native weapon/data identity and real ammo decrease/fireFrame advancement; no per-frame forcedaim. Copied-world arena isolates otherhostiles so native target choice is uncontaminated.
- Second native issue confirmed: DrawWeaponViaSlot and EquipRangedWeapon action unconditionally call Human.EquipWeapon_WeaponSlot even ifsame itemslot alreadydrawn, creating repeated weaponobjects andresettingaim. Added narrow teammate-activecombat idempotenceguards atbothrealcallpaths; exact slot+data+drawn weapon identity mustmatch, otherwise nativeoriginalruns. No firemethod override.
- Current pendingtest: lower-slot guard +native perception, actual8/12m shots +StopFire, preciseweapon/actionstate diagnostics. No release untilsuccess.
- Exact equip-loop root: FollowLeader priority is2 nearleader; FireAtWill adds0 to EliminateThreat native threatscore. FollowLeader.OnActionStarted calls GOAPAgent.PullOutWeapon, which selects FIRSTnonempty slot0 (knife), undoingnativegun selection. Trace confirmed slot2gun406 returnscorrectthenexplicit slot0 equip. currentWeaponItemData is indexedproperty, NOTcache; never mutate it to sync.
- Added RangedEngagementPolicy.cs: only FireAtWill companions within8m ofleader withnative-selected visible in-range threat +usable ownedgun raiseEliminate justabove nearbyFollow (2.5), leavingnativevalidity/emergencygoals/ceasefire/stealth/catch-up. GOAP PullOutWeapon preservesexactalreadyselectedgun insteadfirstslot. No manualFire/forcedgoal/inventory changes. Build0warn0error; awaitingacceptance.
- Further exact native mismatch: EquipRangedWeapon excludes LiquidWeapon8/Energy5/EnergyCharge6/Batteryfeeding2; FindBestRangedWeapon/RangeAttack includes them. Ownedliquidweapon469 (25000units) keptwinningRangeStart overrifle406 (8rounds,24m), whileEquipaction switchedback. Added RangedWeaponCompatibility filteredCOPY of AIeligibleGetRangedWeapons for playercompanions; inventory andnativeammo/scoring/locks untouched, bolts NOTexcluded (feedingType F4, gunActionType F0). Pendingfreshsameweaponshotacceptance.

- FINAL acceptance PASS on freshcopy: native8m acquisition0.75s, actualsamegunshot1.36s ammo8->7;12mshot0.36s ammo7->6; nativeStopFire3s nofire. Earlierpass .15/.49/.42s also retained; timingsobserved, notguaranteed. ProtectedtargetHP avoidsownerweaponcritdeath invalidatingstopfiretest. No forcedFire/SetThreatData. NativeMenuactualpanels3languages +7nativecommands +inventoryconservation +F8UI15/15 andnativeinputrestore PASS;0modwarnings/errors in finalruns.
- FinalDLL d4e8daf8b4f6d87e384eefa9f6a37b3b451f1e7c1c5b5a44b4cd31e676bb144b;29nativepatches. SourceKit/install/bothWorkshoplayouts synchronized1.3.2. Testplugins moved outofMods;8357originalsavefiles SHA256unchanged. ZIP1622475bytes SHA25699d9f598934929351a6b7ca477771f22d170819e92218e0b738d24e151d85813.
- Verification report RELEASE_READY:true; independent native-code review completed, no criticalinventory/globalhook/ceasefire defect. Currentremainingstep: publishGitHub/Steam/Boosty andverifypublicresults.
