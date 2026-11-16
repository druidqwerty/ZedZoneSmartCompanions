# Smart Companions v3.1.0 — Camp survival, Crop harvesting, Fireteams, Anti-Wander & Dual-Inventory Polish

## English

### New features & improvements

- **Fireteam Groups (`[--]` / `[A]` / `[B]`) & `Alt+1 / Alt+2 / Alt+3` channels:**
  - Assign companions to **Alpha (`[A]`)** or **Bravo (`[B]`)** on their Squad cards, and press **`Alt+1` (All)**, **`Alt+2` (Alpha)**, or **`Alt+3` (Bravo)** (or click the header channel button) to direct tactical orders, sorting, auto-loot, battle cries, field aid, and ration sharing to a specific fireteam. Backwards-compatible with 23-field co-op packets.
- **Autonomous camp survival, hydration & offline garrison sustain (`CompanionSurvivalSystem`):**
  - Companions autonomously eat, drink from canteens, and bandage from personal and shared inventories (while staying within backpack reserves), walk to camp food/medicine chests or nearby water wells (`TerrainObject_WaterWell`) when idle at base/garrison, keep offline garrison needs sustained (`UpdateOfflineNPC` Postfix, gated by `EnableAutoBioAndSleep`), yield settled garrison ticks cleanly so native `StayAlive`/`SatisfyNeeds` never starve, and wake immediately if sleeping during recall/combat.
- **Base crop auto-harvesting & 32 m farming radius (`BaseChoreSystem`):**
  - Added an **`[x] Auto-harvest crops`** toggle in `F8 → Work` alongside 32 m planting-pot watering, fertilizing, and ripe crop harvesting, with base/`isPlayerBuilt`/`zLayer` checks and 45-second unreachable-target cooldowns.
- **Anti-wander & tight tactical anchor hold (`HarmonyPatches`, `TacticalSquadSystem`):**
  - Disabled native `Explore` wandering on teammates, suppressed `TerritoryPatrol` and native `SearchForFood`/`SearchForWater` scavenging when Smart Companions manages survival, clamped `FollowLeader` priority `<= 0` for anchored/Hold/Overflow defenders, enforced per-frame `1.15 m` (`1.65 m` in combat) `HoldPosition` anchors and `1.85 m` Camp Garrison leashes, and added range leashes for loot/chores/pet care.
- **Dual-inventory sorting, Fighter auto-dropoff & stack-cap fixes (`BaseSortingSystem`, `CompanionProfileSystem`):**
  - Enabled default `AllowAutoDropoffAtBase` on the `Fighter` specialization preset, added unloading of surplus items across both `sharedInventoryData` and personal `characterData.inventoryData` (with cross-inventory reserve subtraction and first flashlight/canteen protection in shared inventory), fixed partial-stack batch merge over-allocation (`claimedMergeUnits`), added `[Drop-off]` fallback chest rerouting when a category chest fills mid-run, and synced `EnableBaseIndustry` across config load/reset.
- **Vehicles, tactics, cursor guard, Quick Guide `[?]` & Vehicle Combat Chatter:**
  - Excluded `HoldPosition`/`CoverDirection` guards from vehicle boarding, zeroed movement input before `GetInVehicle`, fixed `FieldTacticsSystem` max-HP resolution (`characterStatusData.maxhp`), added controller-transition cursor restoration & modal hotkey gating, guarded `EmergencyAid`/`First Aid` by floor, distance, and cooldown, preserved ranged combat LOS before ducking/weaving, added the built-in **`[?]` Quick Mod Guide** (RU/EN/ZH), and added **Vehicle Combat Callouts** (now **61 chatter topics** and **1,215 unique lines**: 577 RU / 302 EN / 336 ZH).

### Verification and limitations

Build: 0 warnings/errors against ZED ZONE's `2026-11-15` `Assembly-CSharp.dll`, **86** Harmony patches, `[SelfVerify] PASS v3.1.0`. Reproducible live gameplay acceptance passed **twice consecutively** (`backups/live-20261117-012137` and `backups/live-20261117-012242`) on the frozen DLL (`8a4a9423…`): 8 m & 12 m zombie combat, `StopFire` negative case, 8 m hostile human NPC discharge, supermarket ground loot & chest-to-chest carry walk with mid-carry pause/resume (0 teleports), `0.00 m` Hold Position drift, Fall Back Here run, 80-chest warehouse (`0.14 ms`), Camp Garrison hold, SafeDismiss handover & `TeamNPCPanel` purge. Graphical **300/300** UI matrix + 3 `[?]` Quick Guide renders + all **11** deterministic regression suites (`V310Regression` included) PASS (`backups/ui310-20261117-012352`), plus Win32 OS input probes at `0.75x` and `2.0x` scale PASS; all **8,386/8,386** real `GameData` save files matched SHA-256 across all 5 runs. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.0.md).

**Limits:** a live two-machine / two-account host + client session and automated hardware mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Новые возможности и исправления

- **Боевые группы (`[--]` / `[A]` / `[B]`) и каналы команд `Alt+1 / Alt+2 / Alt+3`:**
  - Назначайте бойцам группу **Альфа (`[A]`)** или **Браво (`[B]`)** на карточке в `F8 → Отряд` и переключайте активный канал клавишами **`Alt+1` (Все)**, **`Alt+2` (Альфа)**, **`Alt+3` (Браво)** или кнопкой в шапке для тактических приказов, сортировки, лута, боевого клича, первой помощи и раздачи пайков. Обратная совместимость с 23-польными пакетами кооператива сохранена.
- **Автономное выживание в лагере и оффлайн-поддержка гарнизона (`CompanionSurvivalSystem`):**
  - Напарники сами пьют из фляг, едят и перевязываются из личного и общего инвентарей с соблюдением резервов, в мирное время на базе/в гарнизоне подходят за припасами к отмеченным ящикам и колодцам (`TerrainObject_WaterWell`), не погибают от голода/жажды в оффлайн-гарнизоне (`UpdateOfflineNPC`), не срывают `HoldPosition`/`CoverDirection` на походы к ящикам, держат поводок гарнизона `1,85 м` и уступают нативным `StayAlive`/`SatisfyNeeds` на месте без застреваний.
- **Автосбор урожая на базе и радиус огорода 32 м (`BaseChoreSystem`):**
  - Добавлен переключатель **`[x] Автосбор урожая`** во вкладке `F8 → Работа` вместе с поливом, удобрением и сбором созревших грядок в радиусе `32 м`, проверкой `isPlayerBuilt`/`zLayer` и 45-секундным откатом недостижимых целей.
- **Защита от самовольного бродяжничества и жёсткое удержание позиции (`HarmonyPatches`, `TacticalSquadSystem`):**
  - Запрещено блуждание `Explore` и патрулирование у закреплённых бойцов, отключены нативные `SearchForFood`/`SearchForWater` при активном авто-выживании мода, приоритет `FollowLeader` зажат в `<= 0` для гарнизона/позиций/переполнения авто, добавлен покадровый контроль удержания позиции (`<= 1,15 м` вне боя / `<= 1,65 м` в бою) и ограничители радиуса для лута, быта и питомцев.
- **Двойной инвентарь, авто-сдача для «Бойца» и корректные стаки (`BaseSortingSystem`, `CompanionProfileSystem`):**
  - Включена `AllowAutoDropoffAtBase` по умолчанию у специализации «Боец», добавлена разгрузка лишних вещей сразу из `sharedInventoryData` и личного `characterData.inventoryData` с защитой резервов, первого фонаря и фляги, исправлен пересчёт лимита при частичном слиянии стаков (`claimedMergeUnits`) и перемаршрутизация при заполнении ящика, синхронизирован флаг `EnableBaseIndustry`.
- **Транспорт, полевая тактика, защита курсора, памятка `[?]` и реплики в бою из авто:**
  - Исключены защитники на `HoldPosition`/`CoverDirection` из посадки в авто, сброшен `movementInput` до `GetInVehicle`, исправлено чтение `maxhp` в `FieldTacticsSystem`, улучшен возврат курсора после диалогов/панелей (`OnFocusRegained`, `CanProcessGameplayWorldOrders`), добавлены проверки этажа/дистанции/отката для «Первой помощи» и защита прицела до ухода в укрытие, добавлена встроенная **краткая памятка `[?]`** (RU/EN/ZH) и реплики боя вокруг транспорта (**61 тема**, **1 215 уникальных реплик**: 577 RU / 302 EN / 336 ZH).

### Проверка и ограничения

Сборка: 0 предупреждений и 0 ошибок под `Assembly-CSharp.dll` от `2026-11-15`, **86** Harmony-патчей, `[SelfVerify] PASS v3.1.0`. Два подряд чистых живых прогона в игре (`backups/live-20261117-012137` и `backups/live-20261117-012242`) на замороженной DLL (`8a4a9423…`): стрелковый бой 8 м и 12 м по зомби и по враждебному человеку `Survivor(Clone)`, `StopFire`, физический сбор лута и переноска между ящиками с паузой (0 телепортаций), удержание `Hold Position` (`0,00 м` дрейфа), «Ко мне!», склад на 80 ящиков (`0,14 мс`), гарнизон базы, безопасный отпуск и очистка `TeamNPCPanel`. Матрица **300/300** графических случаев + 3 экрана памятки `[?]` + все **11** наборов регрессий (`V310Regression` включён) PASS (`backups/ui310-20261117-012352`), а также пройдены OS-пробы ввода Win32 на масштабах `0.75x` и `2.0x`; все **8 386/8 386** файлов реального `GameData` совпали по SHA-256 во всех 5 прогонах. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.1.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и аппаратная подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
