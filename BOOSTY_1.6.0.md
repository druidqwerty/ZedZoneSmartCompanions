Title / Заголовок: ZED ZONE: Smart Companions 1.6.0 — Clean Rebuild, Puppet/Weapon Guard & Tactical Fixes / ZED ZONE: Smart Companions 1.6.0 — Чистая пересборка, защита экипировки/кукол и боевой ИИ
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support its development on Boosty.

## Smart Companions 1.6.0 — Full Audit & Clean Rebuild

Version **1.6.0** overhauls internal stability after a full line-by-line audit against ZED ZONE's native engine:

- **Accurate survivor & bandit IDs + automatic save repair:** restores native human IDs (`200` Looter, `201` Survivor) while protecting player character data (`0`) and repairing NPCs misassigned in older builds.
- **Zero-crash weapon & puppet loading:** protects ranged and melee weapon initialization and hand animations across companions, world NPCs, and network puppets—eliminating `LateUpdate` error spam and keeping active NPCs registered in the world.
- **Sharper combat & controls:** enforces strict cease-fire in *Passive / StopFire*, clears culled ghost threats, stops false ambush callouts on peaceful camp residents, syncs companion dismissal in co-op, and blocks `F6–F9` hotkeys while typing in chat (`Esc` closes `F8`).

**Co-op note:** 1.6.0 passed live ZED ZONE engine combat, sorting, and equipment/puppet verification with all save files untouched; real two-player host+client human NPC visibility still awaits live two-player co-op confirmation—let us know how it runs with your co-op partner!

Requires third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258). Safe for existing saves.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub Release v1.6.0 & Verification Notes](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.6.0)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Умные напарники 1.6.0 — полная ревизия и чистая пересборка

В версии **1.6.0** мод полностью перебран и вычищен по нативному коду движка ZED ZONE:

- **Точные ID выживших и мародёров + автопочинка сохранений:** возвращены родные ID людей (`200` — мародёр, `201` — выживший), защищены данные игрока (`0`), а выжившие со сбитым ID из прошлых сборок чинятся автоматически.
- **Надёжная загрузка оружия и сетевых кукол:** защищена инициализация огнестрельного и холодного оружия и анимация рук у напарников, диких NPC и сетевых кукол — без спама ошибок в `LateUpdate` и без потери регистрации NPC в мире.
- **Точный бой и удобное управление:** строго работает приказ «Не стрелять» (*Passive / StopFire*), очищаются выгруженные фантомные цели, убраны ложные крики о засаде на мирных жителей лагеря, синхронизируется роспуск напарников в коопе, а хоткеи `F6–F9` не срабатывают при наборе текста в чате (`Esc` закрывает `F8`).

**О кооперативе:** сборка 1.6.0 прошла живую приёмку боя, сортировки и защиты экипировки/кукол в движке ZED ZONE без единого изменения сейвов; видимость людей-NPC в реальной сессии двух игроков (хост + клиент) ещё ждёт живой парной проверки — напишите, как работает у вас с другом!

Требуется сторонний загрузчик [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258). Сброс сохранений не нужен.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Релиз GitHub v1.6.0 и отчёт о проверке](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.6.0)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
