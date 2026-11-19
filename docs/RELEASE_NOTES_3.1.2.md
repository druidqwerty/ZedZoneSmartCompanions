# Smart Companions v3.1.2 — Patch 1.0.20 Compatibility, Companion Nicknames, Logistics, Survival & Anti-Wander Overhaul

## English

### What's new & fixed in v3.1.2

- **ZED ZONE Patch `1.0.20` (`buildid 26773715`) & Companion Nickname Support:**
  - Verified full compatibility against Patch `1.0.20` (`Assembly-CSharp.dll` `79e55c01...`, `0` missing TypeRefs/MemberRefs, `0` Harmony signature diffs across `90` patched methods, `0` new `/OPT:ICF` RVA collisions; `AssemblyRef` `0.0.0.0`).
  - Added `CompanionConfigStore.GetCharacterDisplayName` backed by a `[MethodImpl(MethodImplOptions.NoInlining)]` call to `1.0.20`'s `CharacterData.GetDisplayName()` (gracefully falling back to trimmed `characterName` on empty/whitespace nicknames or `1.0.19` builds) and wired it across **F8 → Squad**, **F8 → Work**, field tactics banners, combat targets, chatter bubbles/logs, and live `LastKnownName` refreshes without touching internal `NpcGuid` save keys.
- **Vehicle Boarding & Passenger Seat Fix (`ExpeditionSystems`, `HarmonyPatches`):**
  - Treated `BasicVehicle.passengerSeatLimit < 0` as unlimited passenger seats instead of overflowing companions out of buses/RV-style or modded unlimited vehicles; preserved Layer-18 / `FollowLeader` vehicle boarding yield and exited-vehicle overflow state refresh.
- **Anti-Wandering & 1000 m Teleportation Fix (`CampGarrisonSystem`, `HarmonyPatches`, `TacticalSquadSystem`):**
  - Synchronized `npcStayPosition` and `agentState["basePosition"]` on `CampGarrison` exit and anchor updates, guarded `GOAPGoal_ReturnToBase` / `GOAPAction_ReturnToBase` against stale `(0,0)` anchors and >120 m off-base leaps, enforced `CampGarrison` perimeter anchors and `AllowBaseChores`, bounded worker jobs by `zLayer` and radius, suppressed `TerritoryPatrol` / `LootResource`, and restored vanilla-faithful `Free + Normal` following behavior.
- **Physical Base Unloading & Zero-Teleportation Logistics (`BaseSortingSystem`, `CombatResponse`, `AutoLootSystem`):**
  - Prevented `CombatResponse.Refresh` from flushing `CarryBuffer` or aborting active sorting over distant (`>12 m`) or no-LoS threats (`ClearNonCloseThreatDuringSorting`).
  - Added support for unloading equipped backpacks (`characterEquipmentData.backpackItemData`), raised `BaseScanRadius` up to `80 m`, fixed `F7` hotkey/UI sorting when `LootInventoryPanel` is open or `CampGarrison` workers are nearby, rerouted carry jobs cleanly when a chest becomes unreachable (zero remote deposit or fallback teleportation), fixed single-player `SC142:` chest unmark resurrection (`MultiplayerSyncSystem.WriteRuleToTerrainObject` + `BaseSortingSystem_PhysicalCarry`), and made passive `AutoLoot` skip base corpses within `<= 18 m`.
- **Shared-Bag Healing, Nested Pouches & Survival (`CompanionSurvivalSystem`):**
  - Added recursive inventory collection across nested pouches/backpacks (`depth <= 3`, `Pointer`-deduplicated) for both personal and shared bags, pure bandage support (`hp <= 0` via `UseBandage` / `StopAllBleeding`), active bleeding/wound treatment even at `>= 78%` HP, safe in-combat bandaging when threats are `> 4.5 m` or lack LoS, strict rejection of ordinary foods/stews/steaks (`hp > 0`) as medicine, and automatic `CampGarrison` camp resupply.
- **`MainMenu`, Load Safety, Combat Stability & Gender/Headgear Chatter (`HumanNpcEquipmentGuard`, `CombatWeaponStability`, `SmartCompanionsInput`, `ChatterDatabase`):**
  - Scoped `HumanNpcEquipmentGuard` cleanly to `InGameController` (`!isReturningToMainMenu`), switched `TeamNPCPanel` cleanup to raw `_instance` + `"Gaming"` scene check, hardened `NPCGearOwnership` slot-lock wrappers, fixed inactive `rangedWeapon` switch-back + `PullOutWeapon` + 67-frame `goalReselectDeferred` delays, added `SecondIslandEndingDirector.Busy` gating, fixed cursor restore without breaking gamepad looting, and made EN/RU/ZH dialogue persona-aware for companion `gender` and novelty headgear (`IsHeadDecoration` / `CoverHair` respecting `headgearHiddenMode`).

### Verification & open boundaries

Rebuilt `Release` (`-warnaserror`, `0 warnings, 0 errors`) against ZED ZONE **Patch `1.0.20` (`buildid 26773715`)**, `AssemblyRef 0.0.0.0`, **90** Harmony patches (`failed=0`, **100%** `[EH > 0]`), `[SelfVerify] PASS v3.1.2` (`61` topics, `1,215` lines: `577 RU / 302 EN / 336 ZH`), `0` lazy `.instance` reads, on frozen DLL `2737f80bb1e3a329a142f07f1296318d0d4dfb8c51db33ae395f02e2166d93a6` (`657,408` B). All **7/7** Gate 2 live engine runs passed on `1.0.20`: `run_real_menu_load.py` x2 (`090723` in `26.1s`, `090806` in `25.9s`), `run_live_acceptance.py` x2 (`090907` in `53.8s`, `091019` in `64.8s`), `run_ui_smoke.py` (`091139`, `13/13` suites incl. `V312Regression (a)-(h)`, `300/300` UI matrix + `3` QuickGuide PNGs, visually inspected), and Win32 OS probes at `0.75x` (`091727`) and `2.0x` (`091802`), with `8,386/8,386` `GameData` files SHA-256 identical across all runs. [Full verification report](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.2.md).

**Post-update note:** After a ZED ZONE game update (such as Patch `1.0.20`), if BepInEx / `Code Mod Loader` fails to load or stalls at startup, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so IL2CPP interop assemblies regenerate cleanly.

**Open boundaries:** automated hardware mouse-wheel scroll (`wheel_verified=false`), a live two-machine/two-account Steam `Host + Client` co-op session (`UiSmoke` covers co-op logic `13/13` in-fixture only), and manual in-match `NPCNickname` UI renaming (nickname support verified via `UiSmoke V312Regression (h)`).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Что нового и исправлено в v3.1.2

- **Совместимость с патчем ZED ZONE `1.0.20` (`buildid 26773715`) и поддержка никнеймов напарников:**
  - Полная проверка под патч `1.0.20` (`Assembly-CSharp.dll` `79e55c01...`: `0` пропавших `TypeRef`/`MemberRef`, `0` расхождений сигнатур у `90` Harmony-патчей, `0` новых `/OPT:ICF`-коллизий; `AssemblyRef` `0.0.0.0`).
  - Добавлен общий `[MethodImpl(MethodImplOptions.NoInlining)]` хелпер `CompanionConfigStore.GetCharacterDisplayName`, вызывающий `CharacterData.GetDisplayName()` из `1.0.20` (с безопасным откатом к `characterName` при пустом/пробельном никнейме или на `1.0.19`) — никнеймы теперь отображаются во вкладках **F8 → Отряд** и **F8 → Работа**, в тактических баннерах, репликах и `LastKnownName` без изменения ключей сохранений (`NpcGuid`).
- **Посадка в транспорт и безлимитные места (`ExpeditionSystems`, `HarmonyPatches`):**
  - Отрицательный `BasicVehicle.passengerSeatLimit < 0` теперь корректно считается неограниченным числом мест (напарники не отсеиваются как «переполнение»), сохранена уступчивость при посадке на `18`-м слое и мгновенный сброс переполнения при выходе.
- **Устранение бродяжничества и телепортов на 1000 м (`CampGarrisonSystem`, `HarmonyPatches`, `TacticalSquadSystem`):**
  - Синхронизация `npcStayPosition` и `agentState["basePosition"]` при выходе из «Гарнизона» и смене якоря, блокировка ложного срабатывания `GOAPGoal_ReturnToBase` / `GOAPAction_ReturnToBase` на `(0,0)` и прыжков `> 120 м`, жёсткое удержание периметра гарнизона и `AllowBaseChores`, ограничение радиусов и этажа `zLayer`, подавление `TerritoryPatrol` / `LootResource`, а также возврат естественного следования в режиме `Свободно + Обычная дистанция`.
- **Физическая разгрузка базы и нулевая телепортация предметов (`BaseSortingSystem`, `CombatResponse`, `AutoLootSystem`):**
  - `CombatResponse.Refresh` больше не сбрасывает `CarryBuffer` и не прерывает сортировку из-за далёких угроз (`> 12 м`) или врагов без прямой видимости (`ClearNonCloseThreatDuringSorting`).
  - Добавлена выгрузка надетых рюкзаков (`characterEquipmentData.backpackItemData`), расширен `BaseScanRadius` до `80 м`, починена работа `F7` при открытой `LootInventoryPanel` и с гарнизоном рядом, исправлено перенаправление при недостижимых ящиках без телепортации в инвентарь/склад, исправлен «возврат» снятой в одиночной игре разметки ящиков `SC142:` (`WriteRuleToTerrainObject` + `BaseSortingSystem_PhysicalCarry`), а пассивный `AutoLoot` не трогает трупы внутри базы (`<= 18 м`).
- **Лечение из общих и вложенных сумок (`CompanionSurvivalSystem`):**
  - Рекурсивный поиск медицины, еды и воды во вложенных подсумках/рюкзаках (`depth <= 3` с дедупликацией по `Pointer`) в личном и общем инвентаре, поддержка чистых бинтов (`hp <= 0` через `UseBandage` / `StopAllBleeding`), обработка кровотечений даже при `>= 78%` HP, безопасная перевязка в бою при дистанции угрозы `> 4.5 м` или без LoS, запрет считать обычную еду/рагу/стейки (`hp > 0`) лекарством, а также автопополнение припасов в гарнизоне.
- **Защита `MainMenu` / сохранений, стабильность боя, курсор и персонифицированные реплики:**
  - `HumanNpcEquipmentGuard` ограничен наличием `InGameController` (`!isReturningToMainMenu`), очистка `TeamNPCPanel` переведена на сырой `_instance` и проверку сцены `"Gaming"`, усилены обёртки `NPCGearOwnership`, исправлен возврат неактивного `rangedWeapon` + `PullOutWeapon` и задержка выбора целей (`goalReselectDeferred`), учтён `SecondIslandEndingDirector.Busy`, починен возврат курсора без поломки геймпад-лута, а реплики EN/RU/ZH учитывают пол (`gender`) и заметные головные уборы (`IsHeadDecoration` / `CoverHair` с учётом `headgearHiddenMode`).

### Проверка и ограничения

Сборка `Release` (`-warnaserror`, `0` предупреждений и `0` ошибок) под ZED ZONE **Patch `1.0.20` (`buildid 26773715`)**, `AssemblyRef` `0.0.0.0`, **90** Harmony-патчей (`failed=0`, **100%** `[EH > 0]`), `[SelfVerify] PASS v3.1.2` (`61` тема, `1 215` реплик: `577 RU / 302 EN / 336 ZH`), `0` обращений к ленивым геттерам `.instance`, замороженная DLL `2737f80bb1e3a329a142f07f1296318d0d4dfb8c51db33ae395f02e2166d93a6` (`657 408` Б). Все **7/7** живых прогонов на `1.0.20` пройдены: `run_real_menu_load.py` ×2 (`090723` за `26.1 с`, `090806` за `25.9 с`), `run_live_acceptance.py` ×2 (`090907` за `53.8 с`, `091019` за `64.8 с`), `run_ui_smoke.py` (`091139`, `13/13` наборов с `V312Regression (a)-(h)`, `300/300` графических кадров + `3` QuickGuide PNG, просмотрены глазами), Win32 OS-пробы на `0.75x` (`091727`) и `2.0x` (`091802`), все **8 386/8 386** файлов реального `GameData` побайтово совпали во всех прогонах. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.2.md).

**Памятка после обновлений игры:** после выхода патча самой игры ZED ZONE (например, `1.0.20`), если моды не подгружаются или запуск зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или повторно запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

**Ограничения:** автоматическая аппаратная прокрутка колесом мыши (`wheel_verified=false`), настоящая сетевая сессия двух аккаунтов/ПК `Host + Client` (`UiSmoke` проверяет логику кооператива `13/13` на фикстурах) и ручное внутриигровое переименование через окно `NPCNickname` (`1.0.20` поддержка никнеймов проверена тестом `UiSmoke V312Regression (h)`).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
