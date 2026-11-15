# Smart Companions v3.0.2 — Input, vehicle, long-range garrison, co-op revival & stack-merging fixes

## English

### Bug fixes & improvements

- **Fixed startup WASD / Esc / UI input freeze on save load (`LootInventoryPanel._instance`):**
  - Reading ZED ZONE's lazy `LootInventoryPanel.instance` singleton property on frame 1 instantiated `LootInventoryPanel` (`Awake()`) before the player ever opened a container, locking `CharacterInput.instance.stopAllInput`. Replaced all reads across `SmartCompanionsUI*.cs` and `BaseSortingSystem_*.cs` with the raw backing field `LootInventoryPanel._instance`.
- **Fixed vehicle boarding, exit & seat-overflow restore (`ExpeditionSystems`):**
  - Replaced permanent `RosterMode = CampGarrison` mutation on seat overflow with a transient `IsTransientVehicleOverflow` flag that clears immediately on vehicle exit (never throttled), tracks `aboardCompanions` across `isDriving || drivingVehicle`, skips formation/yield/combat movement nudges while driving, and preserves native `0.10 m` `IsPathClear` clearance around vehicle hulls and narrow rooms.
- **Fixed long-range Hold Position & Camp Garrison teleporting (>100 m):**
  - Patched `GOAPNPCData.isStayAtPositionOrder`, `GOAPNPCManager.IsStayingBehindOrder`, and `GOAPAction_FollowLeader.TryCatchUpTeleport`, clamped `GOAPGoal_FollowLeader.GetPriority <= 0.35f` regardless of distance, synced `npcStayPosition`/`npcPosition` with `(0,0)` anchor guards, and updated squad-wide hotkeys (`IssueOrder(null, ...)`, `Alt+G`, `Alt+F`, `F6`, `F9`) to skip distant `CampGarrison` defenders.
- **Fixed multiplayer companions dying immediately instead of kneeling (`MultiplayerTeammateAidPatches`):**
  - Scoped a thread-local `NetworkManager.isOnline -> false` override strictly around non-player companion calls (`IsNonPlayerNpcForAid`) in `CharacterAidManager` and `PlayerAidInteract` on Host so zero-HP companions enter ZED ZONE's native kneeling revival state, synced `NPC_DOWNED|<guid>|1/0` to clients, synced remote leader positions every frame, and added a **`[+] Revive`** button (`REVIVE_NPC`, `<= 4.6 m`) in `F8 → Squad`.
- **Added `Alt+F` loot sweep hotkey, stack merging, hybrid Drop-off chests, Coconut & Reset All Settings:**
  - Added **`Alt+F`** world hotkey for `CommandSweepNearbyLoot`.
  - Added `InventoryTransfers.TryAddWholeWithStacking` so partial stacks (bandages, ammo, food) merge cleanly into existing stacks in chests and backpacks without wasting 1-item slots, with atomic rollback and `TryAutoSortingWithoutOverflow` compaction fallback.
  - Enabled hybrid `[Drop-off + Category]` chests as valid category destinations while keeping pure `[Drop-off]` chests (`14.0 m` auto-dropoff radius) for incoming raid loot and vehicle trunk unloading (`BaseChoreSystem`).
  - Classified Coconut (`230`) into `FreshFood` (`SweetAndSnack`).
  - Added a two-step **Reset All Settings to Default** button at the bottom of `F8 → Settings` (preserving marked base chests and companion XP).

### Verification and limitations

Build: 0 warnings/errors against ZED ZONE's `2026-11-15` regenerated `Assembly-CSharp.dll`, 81 Harmony patches, `[SelfVerify] PASS v3.0.2`. Live in-engine acceptance: ranged zombie & hostile human combat, supermarket indoor ground loot & chest-to-chest carry walk (0 teleports), Hold Position & Fall Back Here, reserves, 80-chest warehouse soak (with pure `[Drop-off]` + hybrid `[Drop-off + Category]` chests), Camp Garrison & SafeDismiss physical handover, and live `v3.0.2` checks (`LootInventoryPanel._instance == null` & `stopAllInput == false` at boot, 250 m long-range stay/garrison clamp, `CampGarrison` hotkey isolation, and `IsNonPlayerNpcForAid`). Full **300/300** graphical UI matrix across six resolutions and three languages + all 10 deterministic regression suites (`InventoryRegression` stack merging & `GarrisonRegression` reset/overflow/downed sync). All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.2.md).

**Limits:** a live two-machine / two-account host + client session and automated hardware mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Исправления и улучшения

- **Исправлена блокировка WASD, Esc и UI сразу после загрузки сохранения (`LootInventoryPanel._instance`):**
  - Обращение к ленивому геттеру `LootInventoryPanel.instance` на первом кадре вызывало `LootInventoryPanel.Awake()` до открытия любого контейнера и блокировало `CharacterInput.instance.stopAllInput`. Все обращения в `SmartCompanionsUI*.cs` и `BaseSortingSystem_*.cs` переведены на прямое поле `LootInventoryPanel._instance`.
- **Исправлена посадка, высадка и переполнение мест в машинах (`ExpeditionSystems`):**
  - Вместо постоянной смены `RosterMode = CampGarrison` при нехватке мест введён временный флаг `IsTransientVehicleOverflow`, который мгновенно сбрасывается при выходе игрока из авто; исправлен подсчёт пассажиров (`isDriving || drivingVehicle`), отключены смещения строя/боя в поездке и возвращён штатный зазор `0.10 м` в `IsPathClear` вокруг кузова машин и мебели.
- **Исправлен срыв «Держать позицию» и «Гарнизона базы» на дистанции свыше 100 м:**
  - Перехвачены `GOAPNPCData.isStayAtPositionOrder`, `GOAPNPCManager.IsStayingBehindOrder` и `GOAPAction_FollowLeader.TryCatchUpTeleport`, приоритет `GOAPGoal_FollowLeader` ограничен `<= 0.35f` на любом удалении, синхронизированы `npcStayPosition`/`npcPosition` с защитой от `(0,0)`, а общие горячие клавиши отряда (`Alt+G`, `Alt+F`, `F6`, `F9`) больше не срывают бойцов в гарнизоне лагеря.
- **Исправлена мгновенная смерть напарников в кооперативе (`MultiplayerTeammateAidPatches`):**
  - Добавлено потокобезопасное локальное переопределение `NetworkManager.isOnline -> false` строго вокруг проверок помощи для неигровых NPC (`IsNonPlayerNpcForAid`) на хосте: при нуле HP напарники встают на колено вместо мгновенной гибели, состояние `NPC_DOWNED|<guid>|1/0` синхронизируется с клиентами, а во вкладке `F8 → Отряд` рядом с раненым (`<= 4.6 м`) доступна кнопка реанимации **`[+]`** (`REVIVE_NPC`).
- **Клавиша лута `Alt+F`, слияние стаков, гибридные ящики, кокосы и сброс настроек:**
  - Добавлена горячая клавиша **`Alt+F`** для быстрого сбора лута вокруг отряда.
  - Реализовано объединение стаков (`InventoryTransfers.TryAddWholeWithStacking`) для бинтов, патронов, еды и материалов в ящиках и рюкзаках с атомарным откатом и уплотнением `TryAutoSortingWithoutOverflow`.
  - Поддержаны гибридные ящики `[Сброс + категория]` как целевые хранилища категорий, увеличена зона авто-сброса до `14.0 м`, добавлена разгрузка багажника транспорта в `[Сброс]`, кокосы (`230`) отнесены к категории свежей еды, а внизу `F8 → Настройки` добавлена двухшаговая кнопка **Сбросить все настройки** (с сохранением размеченных ящиков и опыта напарников).

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок под обновлённый `Assembly-CSharp.dll` от `2026-11-15`, 81 Harmony-патч, `[SelfVerify] PASS v3.0.2`. Живая приёмка в настоящей игре: стрелковый бой по зомби и враждебному NPC, физический лут и переноска между ящиками без телепортации (с паузой посреди маршрута), тактика, резервы, склад на 80 ящиков (чистый `[Сброс]` + гибридный `[Сброс + категория]`), гарнизон, безопасный отпуск и прямые живые проверки `v3.0.2` (`LootInventoryPanel._instance == null` и `stopAllInput == false` при старте мира, удержание на 250 м, изоляция гарнизона от хоткеев и `IsNonPlayerNpcForAid`). Полная матрица **300/300** графических случаев на 6 разрешениях и 3 языках + все 10 наборов детерминированных проверок. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.2.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и аппаратная подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
