# Smart Companions v3.1.1 — Hotfix: Patch 1.0.19 Compatibility, Native Save-Loading Gate & Player Profile Self-Repair

## English

### Bug fixes & engine hardening

- **Fixed save & world loading freezes (`SmartCompanionsRuntimeGate`):**
  - Added a strict native runtime gate (`IsAnyLoadingScreenOrTransitionActive`, `IsGameplayWorldReady`, `IsGameplaySimulationActive`, `IsUiAllowedToRenderAndCapture`) backed by raw singleton fields (`LoadingInfoPanel._instance`, `GameInitPanel._instance`, `GenericConfirmPanel._instance`, `LootInventoryPanel._instance`) and `[MethodImpl(MethodImplOptions.NoInlining)]` IL2CPP interop helpers.
  - Companion `HoldPosition` pathfinding, chunk container loot discovery (`EnsureLootGenerated`), autonomous survival walks, multiplayer NPC scans, and IMGUI/input capture now remain strictly gated off in `MainMenu` and throughout ZED ZONE's `"Gaming"` scene / `GameInitPanel` / `MapController` chunk streaming transition (`Time.timeScale == 0.01f`) until gameplay is ready.
- **Fixed ZED ZONE Patch `1.0.19` (`buildid 26711990`) compatibility, `/OPT:ICF` hook collision & fault isolation (`SafeHarmonyPatcher`):**
  - Resolved the native `/OPT:ICF` identical-codedat folding collision in `GameAssembly.dll` (`RVA 0x0567A10`) where `GOAPAction_SearchForFood::Execute` shared its native function body with `GOAPAction_SearchForMeleeWeapon::Execute` and `GOAPAction_SearchForRangedWeapon::Execute` by checking `__instance.TryCast<GOAPAction_SearchForFood>()` / `TryCast<GOAPAction_SearchForWater>()` before suppressing native scavenging.
  - Replaced monolithic `Harmony.PatchAll` with per-target fault-isolated `SafeHarmonyPatcher.PatchAssemblySafely`, wrapped **100%** of Harmony patch entrypoints (`86` target methods) in explicit `try/catch` (`[EH > 0]`), isolated `1.0.19` interop symbols behind `[NoInlining]` helpers, normalized `Assembly-CSharp` `AssemblyRef` to `0.0.0.0` so minor `2.0.0.x` interop regenerations bind without `FileLoadException`, and unified Workshop staging to a single `SmartCompanions/` folder.
- **Fixed `MainMenu` / `CharacterSelectOverlay` portrait preview player corruption & added automatic save self-repair (`MultiplayerSyncSystem`):**
  - Prevented `CharacterData.GetHumanCharacterPortrait` UI preview mannequins in `MainMenu` and character selection (`InGameController.instance == null` / `GetCachedGOAPAgent() == null`) from mutating player `CharacterData.characterAttrId` (`0 -> 201`), and added `SelfHealPlayerCharacterData` so any previously affected `.mpchar` or `.game` save automatically repairs `characterAttrId` back to `0` (`Player`) on load.
- **End-to-end real-menu save-load regression harness (`RealMenuLoadRegression`):**
  - Added automated `MainMenu -> SinglePlayer -> Sandbox -> Slot -> CharacterSelectOverlay -> Gaming` native load verification (`tools/run_real_menu_load.py`) without overriding `Time.timeScale` or `isSoftPaused`, redirecting both `GameConfigs.*Path` and `NetCharacterSave.saveDirectory` / `ProfilePath` to disposable lab copies.

### Verification and limitations

Build: 0 warnings/errors against ZED ZONE Patch `1.0.19` (`buildid 26711990`, `Assembly-CSharp.dll` `v2.0.0.668`, normalized `AssemblyRef` `0.0.0.0`), **86** Harmony patches (`failed=0`, **100%** `[EH>0]`), `[SelfVerify] PASS v3.1.1` on frozen DLL `0e997ed19b4d014f9117c730560454cb23c9a571e267f68f5803e4ca1f4422ed`. Real-player native menu save-load (`MainMenu -> Sandbox -> Slot 5 -> CharacterSelectOverlay -> Gaming`, `timeScale 0.010 -> 1.000`) passed **twice consecutively** (`backups/menuload-20261117-101552` in `26.2s` and `backups/menuload-20261117-101713` in `26.3s`, player `characterAttrId 201->0` self-healed, live `F8` toggle & soak PASS). Full live gameplay acceptance (`CombatLive`, `PhysicalLive`, `TacticalLive`, `Soak230`, `GarrisonLive`) passed **twice consecutively** (`backups/live-20261117-102345` in `57.7s` and `backups/live-20261117-102507` in `59.8s`, `0` teleports, `0.00 m`/`0.05 m` Hold drift). Graphical **300/300** UI matrix + 3 `[?]` Quick Guide renders + all **12** deterministic regression suites (`V311Regression` included) PASS (`backups/ui311-20261117-102849`), plus Win32 OS input probes at `0.75x` (`backups/ui311-20261117-103750`) and `2.0x` (`backups/ui311-20261117-103840`) PASS; all **8,386/8,386** real `GameData` save files matched SHA-256 across all **7** live runs. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.1.md).

**Post-update note:** After a ZED ZONE game update (such as Patch `1.0.19`), if BepInEx / `Code Mod Loader` fails to start, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so IL2CPP interop assemblies regenerate cleanly.

**Limits:** a live two-machine / two-account host + client session and automated hardware mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Исправления и защита загрузки

- **Устранены зависания загрузки сохранений и мира (`SmartCompanionsRuntimeGate`):**
  - Добавлен строгий затвор готовности мира (`IsAnyLoadingScreenOrTransitionActive`, `IsGameplayWorldReady`, `IsGameplaySimulationActive`, `IsUiAllowedToRenderAndCapture`) через чтение сырых полей синглтонов (`LoadingInfoPanel._instance`, `GameInitPanel._instance`, `GenericConfirmPanel._instance`, `LootInventoryPanel._instance`) и `[NoInlining]` обёртки IL2CPP.
  - Поиск пути `HoldPosition`, обход контейнеров чанков (`EnsureLootGenerated`), походы автономного выживания, сетевое сканирование NPC и перехват IMGUI/ввода полностью отключены в `MainMenu` и на всём протяжении загрузки сцены `"Gaming"`, `GameInitPanel` и стриминга чанков карты (`Time.timeScale == 0.01f`) до полной готовности мира.
- **Совместимость с патчем ZED ZONE `1.0.19` (`buildid 26711990`), устранение `/OPT:ICF`-коллизии и изоляция хуков (`SafeHarmonyPatcher`):**
  - Устранена нативная `/OPT:ICF`-коллизия в `GameAssembly.dll` (`RVA 0x0567A10`), где `GOAPAction_SearchForFood::Execute` делил машинный код с `GOAPAction_SearchForMeleeWeapon::Execute` и `GOAPAction_SearchForRangedWeapon::Execute` (добавлена явная проверка `__instance.TryCast<GOAPAction_SearchForFood>()` / `TryCast<GOAPAction_SearchForWater>()`).
  - Монолитный `Harmony.PatchAll` заменён пометодным `SafeHarmonyPatcher.PatchAssemblySafely`, **100%** точек входа Harmony (`86` целевых методов) защищены `try/catch` (`[EH > 0]`), символы `1.0.19` изолированы через `[NoInlining]`, ссылка на `Assembly-CSharp` нормализована в `0.0.0.0` для совместимости между пересборками `2.0.0.x`, а структура пакета Workshop приведена к одной папке `SmartCompanions/`.
- **Защита профиля игрока от манекенов меню и автопочинка сохранений (`MultiplayerSyncSystem`):**
  - Портретные манекены `CharacterData.GetHumanCharacterPortrait` в `MainMenu` и `CharacterSelectOverlay` больше не меняют `CharacterData.characterAttrId` игрока (`0 -> 201`), а `SelfHealPlayerCharacterData` автоматически восстанавливает `characterAttrId = 0` при загрузке любого ранее затронутого `.mpchar` или сохранения мира.
- **Сквозной автотест реальной загрузки из главного меню (`RealMenuLoadRegression`):**
  - Добавлен живой прогон `MainMenu -> SinglePlayer -> Sandbox -> Slot -> CharacterSelectOverlay -> Gaming` (`tools/run_real_menu_load.py`) без подмены `Time.timeScale` и `isSoftPaused`, с полной переадресацией `GameConfigs.*Path` и `NetCharacterSave.saveDirectory` / `ProfilePath` во временную копию профиля.

### Проверка и ограничения

Сборка: 0 предупреждений и 0 ошибок под патч ZED ZONE `1.0.19` (`buildid 26711990`, `Assembly-CSharp.dll` `v2.0.0.668`, `AssemblyRef` `0.0.0.0`), **86** Harmony-патчей (`failed=0`, **100%** `[EH>0]`), `[SelfVerify] PASS v3.1.1` на замороженной DLL `0e997ed19b4d014f9117c730560454cb23c9a571e267f68f5803e4ca1f4422ed`. Реальная нативная загрузка из меню (`MainMenu -> Sandbox -> Slot 5 -> CharacterSelectOverlay -> Gaming`, `timeScale 0.010 -> 1.000`) пройдена **дважды подряд** (`backups/menuload-20261117-101552` за `26.2 с` и `backups/menuload-20261117-101713` за `26.3 с`, автопочинка `characterAttrId 201->0`, проверка `F8` и живого мира PASS). Полный живой прогон геймплея (`CombatLive`, `PhysicalLive`, `TacticalLive`, `Soak230`, `GarrisonLive`) пройден **дважды подряд** (`backups/live-20261117-102345` за `57.7 с` и `backups/live-20261117-102507` за `59.8 с`, `0` телепортаций, `0.00 м`/`0.05 м` дрейф `HoldPosition`). Графическая матрица **300/300** + 3 экрана памятки `[?]` + все **12** наборов регрессий (`V311Regression` включён) PASS (`backups/ui311-20261117-102849`), плюс Win32 OS-пробы ввода на масштабах `0.75x` (`backups/ui311-20261117-103750`) и `2.0x` (`backups/ui311-20261117-103840`) PASS; все **8 386/8 386** файлов реального `GameData` совпали по SHA-256 во всех **7** прогонах. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.1.md).

**Памятка после обновлений игры:** после выхода патча самой игры ZED ZONE (например, `1.0.19`), если моды не подгружаются или игра зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или заново запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и аппаратная подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
