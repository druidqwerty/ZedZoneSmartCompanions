# Smart Companions v2.3.0 — Performance, 15-NPC scale & privacy-safe diagnostics

## English

### Changes

- **Faster item, container & target queries:**
  - Frame-cached active squad queries eliminate repeated native `GOAPNPCManager` scans across combat, UI, sorting, scavenging and chores within the same frame.
  - Item-ID category memoization turns warehouse item classification into an `O(1)` lookup after the first resolution.
  - Spatial/time caching for world `TerrainObject` scans avoids redundant chunk/building/floor hierarchy walks across base sorting, scavenging and camp chores.
  - Shared hostile-character spatial snapshots (`0.20 s` window) avoid per-companion full `characterList` sweeps across large squads.
- **Large warehouses & 15-companion squads:** verified priority-sorted routing across 80+ marked chests and 960+ items, plus non-overlapping formation slots and squad-wide profile previews for up to 15 companions.
- **Stronger AVO Expanded compatibility:** detects AVO via BepInEx IL2CPP plugin metadata and loaded assemblies, preserves AVO's `maxTeamNPCNumber` untouched when `Squad cap` is set to `Auto`, automatically protects `F6` via `Alt+F6`, and displays live AVO status in Settings.
- **Privacy-safe diagnostic report (`F8 → Settings → Squad & controls`):** copies a structured bug-report summary to the clipboard and writes `BepInEx/config/SmartCompanions_Diagnostics.txt` with **zero** save data, OS paths, Steam IDs, player/companion names or base coordinates.
- **Lifecycle & transition safety:** area/scene transition carry-buffer flushing, stale pointer pruning and co-op reconnect packet sync (`TACTICS`, `PAUSE`, `NPC` with specializations & reserves) verified in multi-cycle live engine runs.
- Includes all improvements from **2.2.0** (specializations, reserves, rare-supply permissions, loot zones), **2.1.0** (formations & tactical orders), **2.0.0** (Work board) and **1.9.0** (host-checked co-op orders).

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.3.0`. Live in-engine acceptance & soak (`backups/live-20261112-161122`): native ranged combat, physical loot & chest carry with 4 s pause/resume (0 teleports), Hold Position & Fall Back Here, live backpack reserve & rare-steak guards, **80-chest / 960-item warehouse benchmark**, **20-cycle live engine soak**, transition carry-buffer safety flush, co-op reconnect state sync, and live zero-PII diagnostic audit. Full **300/300** graphical UI matrix across six resolutions and three languages + 14/14 scale/AVO/privacy checks + 12/12 specialization checks + 16/16 tactical checks + 13/13 co-op checks. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.3.0.md).

**Limits:** a live two-player host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Снижение нагрузки на поиск предметов, ящиков и целей:**
  - Кэш активного отряда на кадр убирает повторные обходы `GOAPNPCManager` между боем, интерфейсом, сортировкой, лутом и бытом лагеря.
  - Кэширование категорий по ID предмета ускоряет разбор крупных складов до мгновенной выборки `O(1)`.
  - Пространственно-временной кэш обхода `TerrainObject` избавляет от лишнего сканирования чанков, зданий и этажей.
  - Общий срез враждебных целей (`0.20 с`) избавляет от повторного обхода всего `characterList` каждым из 15 напарников.
- **Большие склады и отряды до 15 человек:** проверена маршрутизация по приоритетам на складах из 80+ ящиков и 960+ предметов, а также построения и предпросмотр профилей для 15 бойцов.
- **Расширенная совместимость с AVO Expanded:** определение AVO через реестр плагинов BepInEx IL2CPP и загруженные сборки, сохранение лимита AVO нетронутым в режиме `Авто`, авто-защита `F6` через `Alt+F6` и индикатор статуса AVO в Настройках.
- **Диагностический отчёт без данных сохранения (`F8 → Настройки → Отряд и управление`):** одной кнопкой копирует отчёт в буфер обмена и сохраняет `BepInEx/config/SmartCompanions_Diagnostics.txt` **без** имён, путей ОС, Steam ID, координат базы и предметов сохранения.
- **Устойчивость переходов и переподключений:** проверены безопасный возврат переносимых предметов при смене локации/выходе, очистка устаревших указателей и восстановление тактики, паузы, специализаций и резервов при переподключении в кооперативе.
- Включает все возможности **2.2.0** (специализации, резерв, редкие припасы, зоны лута), **2.1.0** (построения и боевые приказы), **2.0.0** (доска «Работа») и **1.9.0** (защита приказов в кооперативе).

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.3.0`. Живая приёмка и многоцикловый прогон в игре (`backups/live-20261112-161122`): стрельба, физический лут и переноска с паузой 4 с (0 телепортаций), удержание позиции и отход, защита резерва и редких стейков, **тест склада на 80 ящиков / 960 предметов**, **20-цикловый прогон систем**, проверка переходов, реконнекта кооператива и приватности отчёта. Полная графическая матрица **300/300** на шести разрешениях и трёх языках + 14/14 проверок масштаба/AVO/приватности + 12/12 специализаций + 16/16 тактики + 13/13 кооператива. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.3.0.md).

**Ограничения:** настоящая сессия вдвоём (хост + клиент), включая видимость людей-NPC и синхронизацию вдвоём, а также автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
