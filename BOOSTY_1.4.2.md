Title / Заголовок: ZED ZONE: Smart Companions 1.4.2 — Marked Base Chests Only, Accurate Auto-Categories & Safe Sorting / ZED ZONE: Smart Companions 1.4.2 — Сортировка только ваших ящиков, точные категории и защита предметов
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support its development on Boosty.

## Smart Companions 1.4.2

Version 1.4.2 updates the mod for the latest ZED ZONE patch, fixes item classification for auto-categories, restricts sorting strictly to chests you mark yourself, and prevents item loss during grid compaction.

- **Fixed Auto-Detect Categories ("Only Medicine" Bug):** fixed `ItemCategorizer.HasFeature` so weapons, ammo, materials, tools, clothes, food, and medicine are classified accurately.
- **Sort Only Your Marked Base Chests + Smart F8 Filter:** companions now carry items **only** between chests you explicitly mark as your base chests (`[*]`). The **F8** list defaults to showing only your marked chests plus the chest right at your feet (`≤ 2 m`), hiding random street shelves and containers.
- **Zero-Overflow Compaction & Full Drop-Off Unloading:** replaced `AutoSorting(false, ...)` with snapshot-safe `TryAutoSortingWithoutOverflow` so items can never vanish during grid compaction, added stack merging onto existing stacks, and added clear notices when items remain in a Drop-off box due to missing categories or full chests.

No save reset required. Requires [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub release v1.4.2 and detailed checks](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.4.2)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Умные напарники 1.4.2

Версия 1.4.2 адаптирована под свежее обновление ZED ZONE, исправляет авто-определение категорий, ограничивает сортировку строго отмеченными вами ящиками и защищает предметы от пропажи при уплотнении сетки.

- **Исправлена авто-разметка категорий (баг «везде только Медицина»):** теперь оружие, патроны, материалы, инструменты, одежда, еда и медикаменты определяются точно.
- **Сортировка только по отмеченным вами ящикам + умный фильтр в F8:** напарники разносят вещи **только** между теми ящиками, которые вы сами отметили (`[*]`). В списке **F8** по умолчанию показываются только ваши отмеченные ящики и ближайший ящик прямо у ваших ног (`≤ 2 м`), без десятков чужих полок вокруг.
- **Защита от пропажи предметов и полная разгрузка приёмника:** уплотнение сетки переведено на безопасный `TryAutoSortingWithoutOverflow` со снимком раскладки, добавлено слияние неполных пачек и точная подсказка, каких категорий или места не хватает, если в ящике-приёмнике остались вещи.

Сброс сохранений не нужен. Требуется [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Релиз GitHub v1.4.2 и подробные проверки](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.4.2)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
