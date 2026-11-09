> UNRELEASED 1.3.2 candidate: native menu checks passed, ranged-combat acceptance FAILED. See docs/verification-1.3.2.md. Published/installed stable version remains 1.3.1.

# Smart Companions 1.3.2 — Native commands, threat response and inventory fixes

## English

### Native companion menus

- Fixed the separate blank-menu problem in the game's own NPC command menu, not just the F8 hub.
- Custom commands now use canonical paths such as `Basic.SortBaseContainers`. The `NPCCommand:` dialogue envelope is not a native menu category.
- Native `BuildPanelUI` requests a bare leaf name; labels now resolve that name as well as full paths and legacy envelope-form custom commands.
- Preserved vanilla categories and commands. Removed only the mod's obsolete duplicate paths and unsupported emoji labels.

### Threat response and combat

- Migrated malformed legacy combat/weapon state to the bare action values expected by the native AI.
- Added a 0.3-second native threat-perception refresh, bounded by the equipped weapon's range and native line-of-sight checks. Does not force shots or ignore Hold Fire.
- Threats interrupt base sorting. Anti-friendly-fire sidesteps require actual player aiming, and kiting causes fewer firing interruptions.

### Loot, rewards and rations

- Centered loot searches on the companion rather than the player.
- Transfers accept a whole item only when the destination can receive it, protecting full-inventory cases from partial-transfer errors.
- XP progress is saved between rank-ups. Writes are debounced to avoid excessive disk activity, with a final flush on exit; XP award amounts are unchanged.
- Manual ration sharing consumes one food item per served companion and has a separate 30-second cooldown for each companion. Automatic sharing uses food already consumed by the player.

No new chatter lines in this hotfix. Existing F8 rendering, fonts and input-capture fixes are retained. No save reset required; install with the game closed.

[Verification details and limitations](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/v1.3.2/docs/verification-1.3.2.md) · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Штатное меню приказов

- Исправлена отдельная проблема пустых пунктов в собственном меню NPC игры, а не только в окне F8.
- Команды используют обычные пути вроде `Basic.SortBaseContainers`. Префикс диалоговых инструкций `NPCCommand:` не является категорией штатного меню.
- `BuildPanelUI` запрашивает короткое имя команды; теперь локализация понимает его, полный путь и старую запись пользовательской команды с префиксом.
- Сохранены обычные категории и команды игры. Удаляются только устаревшие дубликаты путей мода и неподдерживаемые эмодзи в названиях.

### Реакция на угрозы и бой

- Неверные старые значения боевого приказа и предпочтения оружия приводятся к коротким названиям действий, которые понимает штатный ИИ.
- Проверка угроз обновляется каждые 0,3 секунды с учётом дальности текущего оружия и штатной проверки видимости. Принудительных выстрелов и отмены приказа «Не стрелять» нет.
- Угроза прерывает сортировку базы. Отход с линии огня включается при настоящем прицеливании игрока; кайтинг меньше прерывает стрельбу.

### Лут, опыт и пайки

- Поиск лута ведётся от позиции напарника, а не игрока.
- Предмет переносится целиком только при возможности принять его в целевой инвентарь; исправлены ошибки частичного переноса в полный рюкзак.
- Прогресс опыта сохраняется между повышениями ранга. Частые записи объединяются с задержкой, при выходе выполняется окончательное сохранение; размеры начисляемого опыта не изменены.
- Ручная раздача расходует по одному предмету еды на каждого накормленного напарника и имеет отдельную задержку 30 секунд для каждого напарника. Автоматическая раздача использует уже съеденную игроком еду.

Новых реплик в этом исправлении нет. Сохранены предыдущие исправления F8, шрифтов и перехвата кликов. Сброс сохранений не нужен; устанавливайте обновление при закрытой игре.

[Проверки и ограничения](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/v1.3.2/docs/verification-1.3.2.md) · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
