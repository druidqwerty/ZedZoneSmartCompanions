# Smart Companions v1.8.1 — Clearer squad status

## English

### Changes

- Every companion card in F8 → Squad now shows a coloured state: **Awaiting orders**, **Busy**, **In combat**, **Stopped** or **Needs help**.
- When work stops, the card explains why: no path to a named chest, no space or chest for listed categories, no chests with categories, or work interrupted by combat. The reason stays visible for 90 seconds.
- New squad filters: All, Busy, Idle and Need help (HP below 50%, hungry or thirsty), with a "Shown X/Y" counter.
- Hover hints for doctrines, per-companion toggles, filters and the K/S/C counters.
- Long state labels stay on one line in every language.
- Combat, physical looting and item carrying are unchanged from 1.7.0/1.8.0. No save reset or new dependency.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v1.8.1`. The final DLL passed 270/270 graphical cases in the real game across six resolutions (1024×768 to 3840×2160, including 3440×1440), three languages and manual 75%/200% scales. All 8,386 save files matched SHA-256 before and after. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.8.1.md).

The UI fixtures prove rendering in the real engine; they are not a gameplay soak test. Mouse-wheel injection and a live two-player host + client session remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- На каждой карточке во вкладке F8 → «Отряд» виден цветной статус: **Ждёт приказа**, **Занят**, **В бою**, **Остановлен** или **Нужна помощь**.
- Если работа остановилась, карточка объясняет причину: нет пути к названному ящику, нет места или ящика для перечисленных категорий, нет ящиков с категориями, работу прервал бой. Причина видна 90 секунд.
- Фильтры отряда: «Все», «Заняты», «Свободны» и «Нужна помощь» (HP ниже 50%, голод или жажда) со счётчиком «Показано X/Y».
- Всплывающие подсказки для доктрин, переключателей напарника, фильтров и счётчиков K/S/C.
- Длинные статусы не переносятся на вторую строку ни в одном языке.
- Бой, физический лут и переноска вещей не менялись с 1.7.0/1.8.0. Сброс сохранений и новые зависимости не нужны.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v1.8.1`. Финальный DLL прошёл 270/270 графических случаев в настоящей игре на шести разрешениях (от 1024×768 до 3840×2160, включая 3440×1440), трёх языках и ручных масштабах 75%/200%. Все 8 386 файлов сохранений совпали по SHA-256 до и после. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.8.1.md).

UI-тесты проверяют отрисовку в настоящем движке, но не заменяют длительный игровой прогон. Подача колеса мыши и живая сессия вдвоём (хост + клиент) пока не подтверждены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
