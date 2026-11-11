# Smart Companions v1.8.0 — Adaptive interface

## English

### Changes

- Reorganizes F8 into Squad, Base, Chatter and Settings with a clearer title, grouped controls and larger text.
- Uses one logical coordinate system for the hub, HUD and chest category bar. Automatic scaling follows screen height; manual presets are 75%, 100%, 125%, 150%, 175%, 200%.
- Caps effective scale to available screen space, keeps windows within bounds, and repositions them after live resolution/aspect/scale changes. The settings screen shows the actual effective scale.
- Keeps physical-pixel world input capture aligned with scaled overlay rectangles. Existing modal/release input protection remains in place.
- Releases drag and scroll capture when the hub closes and hides the chest category overlay while the modal hub is open.
- Gives wrapped container names measured row heights and uses a compact four-column, three-row chest category grid.
- Persists the UI scale preference with backward-compatible config loading; invalid values fall back to Auto.
- Does not change combat, physical looting or item-transfer behavior from 1.7.0. No save reset or additional dependency.

### Verification and limitations

Build: 0 warnings/errors. Final DLL passed 270 unique graphical cases across six resolutions (1024×768 through 3840×2160, including 3440×1440), three languages and manual-scale profiles. Exact-window message probes passed clicks, scrollbar buttons, scaled dragging and Close at 75% and 200% requested. Background wheel injection remains unverified. All 8,386 original save files matched SHA-256 after the tests. [Full verification and boundaries](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.8.0.md).

The UI fixtures test rendering and input in the real engine; they are not a full gameplay or co-op soak test. Human NPC visibility in an actual two-player host + client session still awaits live confirmation.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- F8 разделён на «Отряд», «База», «Реплики» и «Настройки»: яснее заголовок, крупнее текст, параметры собраны по группам.
- Окно F8, мини-панель и категории ящика используют общую систему координат. Автомасштаб зависит от высоты экрана; ручные варианты: 75%, 100%, 125%, 150%, 175%, 200%.
- Фактический масштаб ограничивается доступным местом. Окна удерживаются в границах экрана и меняют положение при смене разрешения, пропорций или масштаба. В настройках показан реальный процент.
- Защита от кликов по миру учитывает масштаб панелей. Сохранена блокировка игрового ввода при открытом окне и до отпускания закрывающего клика.
- При закрытии окна сбрасывается захват перетаскивания и прокрутки; панель категорий ящика скрыта, пока открыто модальное окно F8.
- Высота строк ящиков учитывает перенос названий, а категории в компактной панели собраны в сетку из четырёх столбцов и трёх строк.
- Масштаб сохраняется в конфигурации; старые конфиги совместимы, некорректные значения заменяются режимом Авто.
- Боевые механики, физический лут и переноска вещей из 1.7.0 не менялись. Сброс сохранений и новые зависимости не нужны.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок. Финальный DLL прошёл 270 уникальных графических случаев на шести разрешениях (от 1024×768 до 3840×2160, включая 3440×1440), трёх языках и ручных масштабах. Проверены клики, кнопки прокрутки, перетаскивание и закрытие через оконные сообщения при выбранных 75% и 200%. Автоматическая подача колеса в фоне не подтверждена. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт и границы проверки](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.8.0.md).

Изолированные UI-тесты проверяют отрисовку и ввод в настоящем движке, но не заменяют длительный игровой или кооперативный прогон. Видимость людей-NPC в парной сессии хост + клиент ещё ждёт живой проверки вдвоём.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
