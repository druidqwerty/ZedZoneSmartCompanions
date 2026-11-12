# Smart Companions v2.4.0 — Camp garrison, safe gear-handover dismissal & co-op sync health

## English

### Changes

- **Camp Garrison mode (`F8 → Squad` & radial NPC menu):** assign companions to **Camp Garrison** so they stay guarding the base perimeter and performing allowed storage/chore duties instead of chasing after you on distant raids. Base sorting and container discovery continue scanning around garrisoned companions even while you are away.
- **Safe two-step dismissal with zero-teleport gear handover:** clicking **Dismiss** on a companion card opens an inline confirmation strip (`[x] Hand over gear first` + **Confirm Leave** / **Cancel**). When enabled, the companion physically walks (`<= 2.45 m`, zero item teleportation) to a marked base chest or to you, transfers carried items, backpack contents, weapons and armor safely (with `HumanNpcEquipmentGuard` cleanup so unequipping never triggers hand-IK `NullReferenceException`s), and only then leaves the squad. If unreachable, dismissal cancels safely without losing items.
- **Co-op sync health panel & telemetry (`F8 → Settings` & Diagnostics):** live network status line showing `Host / Client / Singleplayer`, `SC14` packets sent/received, rejected unauthorized companion edits, and synced human NPC / puppet count — also added to the privacy-safe Diagnostic Report without names, paths or coordinates.
- **Co-op:** `RosterMode` and `HomeAnchor` sync through the host-authoritative `SC14` protocol (`CanLocalPlayerCommand` enforced). Existing saves and configs remain compatible.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.4.0` (17 custom NPC commands in RU/EN/ZH). Live in-engine acceptance (`backups/live-20261113-022051`): native ranged combat, physical ground loot & chest carry with 4 s mid-carry pause/resume (0 teleports), tactical Hold Position & Fall Back Here, backpack reserves & rare-steak protection, 80-chest / 960-item warehouse + 20-cycle soak, **Camp Garrison perimeter hold** while the player moved 12 m away, and **SafeDismiss physical walk + gear handover** (0 teleports). Full **300/300** graphical UI matrix across six resolutions and three languages + 8/8 garrison/sync/privacy checks + 14/14 scale/AVO/privacy + 12/12 specialization + 16/16 tactical + 13/13 co-op checks. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.4.0.md).

**Limits:** a live two-machine / two-account host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Режим «Гарнизон базы» (`F8 → Отряд` и радиальное меню NPC):** переводите выбранных напарников в **Гарнизон**, чтобы они охраняли периметр лагеря и продолжали работу на складе и огороде, не бегая за вами в дальние вылазки. Сортировка продолжает видеть ящики рядом с гарнизоном, даже когда вы ушли в рейд.
- **Безопасный двухшаговый отпуск со сдачей снаряжения (0 телепортаций):** кнопка **Отпустить** на карточке открывает встроенное подтверждение (`[x] Сдать вещи в ящик/игроку` + **Подтвердить уход** / **Отмена**). При включённой сдаче напарник физически подходит (`<= 2.45 м`) к ящику базы или к вам, аккуратно складывает содержимое рюкзака, оружие и броню (с очисткой состояния рук через `HumanNpcEquipmentGuard`) и только затем покидает отряд. Если путь перекрыт, увольнение безопасно отменяется без потери вещей.
- **Индикатор синхронизации кооператива (`F8 → Настройки` и Диагностика):** наглядная строка статуса сети (`Хост / Клиент / Одиночная игра`, отправленные/принятые пакеты `SC14`, отклонённые чужие правки и число синхронизированных NPC) без личных данных и координат.
- **Кооператив:** гарнизон и якорь базы синхронизируются через протокол с проверкой владельца на хосте. Старые сохранения и конфиги совместимы.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.4.0` (17 кастомных команд NPC на RU/EN/ZH). Живая приёмка в настоящей игре (`backups/live-20261113-022051`): стрельба, физический лут и переноска с паузой 4 с (0 телепортаций), тактика, резервы, склад на 80 ящиков / 960 предметов + 20 циклов прогона, **удержание гарнизона при уходе игрока на 12 м** и **физический подход при увольнении со сдачей вещей** (0 телепортаций). Полная графическая матрица **300/300** на шести разрешениях и трёх языках + 8/8 проверок гарнизона/кооператива/приватности. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.4.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
