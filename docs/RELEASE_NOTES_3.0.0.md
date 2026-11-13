# Smart Companions v3.0.0 — Expeditions milestone: vehicle crews, multi-floor (zLevel) navigation & pet synergy

## English

### Changes

- **Organized vehicle crews & overflow policy (`BasicVehicle`):**
  - When you take the wheel, active companions coordinate passenger seat allocation (`PlanVehicleSeatAllocation`): critically wounded squadmates (`< 50% HP`), `Medic`s and `Fighter`s receive priority seats, while overflow companions (e.g., an 8-person squad with a 4-seat car) hold cleanly at camp/standby instead of endlessly shoving at vehicle doors.
  - Player pets automatically board when supported (`GOAPAgent_Pet.TryBoardOwnerVehicle`), and `Supplier` / `Quartermaster` companions physically walk to parked vehicles (`GetClosestBodyPoint <= 2.45 m`) for bodywork upkeep.
- **Physical multi-floor & bunker transitions (`MultiFloorNavigator` — `TerrainObject_Door_UndergroundEntrance`, `DugShaft`, `zLevel`):**
  - Replaces passive `Different floor` stalls with two-step physical floor routing: when you descend into a basement/bunker or climb to another `zLevel`, companions locate the nearest matching hatch/stairway on their current `zLevel`, physically walk to the portal (`<= 2.15 m`, zero ceiling teleportation!), resolve `GetMigrateDestination(targetZ)`, and atomically update both `npcData.zLevel` and `characterData.zLevel` while refreshing spatial caches.
- **Pet companion synergy (`PetSystem`, `PetCharacterController`, `F8 → 01 SQUAD`):**
  - Displays active friendly player pets alongside your squad summary in **F8 → Squad**, keeps strict friendly-fire immunity across human teammates and pets, and supports physical pet feeding using `PetSystem.IsEdibleForPet` + `NotifyFoodDropped` from `Farming & Pets` supplies.
- Culminates the **2.4.0 → 3.0.0** expansion series (Camp Garrison & safe gear handover, Field Tactics 2.0 with cursor fan & `GrenadeSafetyGate`, Auto Bio-Masks & sentry sleep shifts, Industrial Power/Furnace logistics, and Expeditions).

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v3.0.0`. Full live in-engine acceptance & soak: native ranged combat, physical loot & chest carry (0 teleports), tactical orders, reserves, 80-chest / 960-item warehouse + 20-cycle soak, Camp Garrison, SafeDismiss physical handover, `GrenadeSafetyGate`, bio-masks & sentry sleep, industrial fuel whitelist, and live `v3.0.0` vehicle seat overflow + multi-floor `zLevel` portal routing + pet synergy checks. Full **300/300** graphical UI matrix across six resolutions and three languages + all 8 deterministic regression suites (`ExpeditionRegression`, `IndustryRegression`, `SurvivalRegression`, `FieldTacticsRegression`, `GarrisonRegression`, `ScaleAndPrivacyRegression`, `SpecializationRegression`, `TacticalRegression`, `ProtocolRegression`). All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.0.md).

**Limits:** a live two-machine / two-account host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Организованные экипажи транспорта и переполнение мест (`BasicVehicle`):**
  - При посадке игрока за руль мод распределяет свободные места (`PlanVehicleSeatAllocation`): приоритет получают тяжелораненые (`< 50% HP`), медики и бойцы, а не поместившиеся в салон напарники аккуратно остаются в гарнизоне/ожидании без бесконечной толкотни у дверей автомобиля.
  - Питомцы игрока автоматически подсаживаются через `TryBoardOwnerVehicle`, а специалисты «Снабженец» физически подходят к стоящей машине (`<= 2.45 м`) для обслуживания кузова.
- **Физическая межэтажная навигация и бункеры (`TerrainObject_Door_UndergroundEntrance`, `DugShaft`, `zLevel`):**
  - Вместо ожидания со статусом «другой этаж» напарник находит ближайший люк или лестницу на своём этаже, физически подходит к нему (`<= 2.15 м`, без телепортации сквозь перекрытия!), вычисляет точку выхода (`GetMigrateDestination`) и синхронно переключает `npcData.zLevel` и `characterData.zLevel`.
- **Связка с питомцами (`PetSystem`, `PetCharacterController`, `F8 → 01 ОТРЯД`):**
  - Сводка активных питомцев игрока в шапке вкладки **Отряд**, полная защита питомцев от дружественного огня и физическая подкормка голодных питомцев съедобным кормом (`IsEdibleForPet` + `NotifyFoodDropped`).
- Объединяет всю линейку **2.4.0 → 3.0.0**: гарнизон и безопасный отпуск, полевую тактику курсора и безопасные гранаты, авто-противогазы и вахту сна, генераторы и печи базы, транспорт, этажи и питомцев.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v3.0.0`. Полная живая приёмка и многоцикловый прогон в игре + **300/300** графических случаев на шести разрешениях и трёх языках + все 9 пакетов детерминированных проверок. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
