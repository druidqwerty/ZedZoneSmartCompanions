# Smart Companions v2.0.0 — Base work board

## English

### Changes

- **New WORK tab in F8** (second tab). One board shows every companion's state, current job, how many items they carry, how many loot stops are left and why work stopped.
- **Work roles per companion:** Any work, Storekeeper (sorting only), Scavenger (loot only), Camp keeper (fire, crops, trunk) or No base work. Roles limit only automatic and whole-squad work; a direct order to one companion always applies.
- **Pause / Resume base work.** While paused, companions take no new automatic work, keep their current job and carried items, and follow you. Resume and they continue where they stopped. A direct "Sort base" or "Sweep loot" order ends the pause.
- **Cancel** one companion's job or all jobs at once. Carried items stay in the companion's backpack; nothing is dropped or teleported.
- **Co-op:** roles and the pause state sync through the host-authoritative order system from 1.9.0; companions owned by another player are view-only.
- **Fixes:** symbols the game font cannot draw (pause/play, lock, emoji in task texts and banners) no longer appear as boxes; hover hints no longer linger after switching tabs.
- Includes 1.8.1 (squad states, stop reasons, filters, hints) and 1.9.0 (safer co-op orders). Roles are saved with your companions; old configs load as "Any work". No save reset or new dependency.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.0.0`. Live gameplay acceptance in the real game on a disposable profile copy: native ranged combat (zombies at 8 m and 12 m, hostile human), physical ground-loot walk, and chest-to-chest carry with a **4-second pause in the middle of carrying** (job and carried item kept, nothing deposited) followed by resume and completion — 0 teleports. Full graphical matrix with the new tab: **300/300** cases on six resolutions and three languages, plus the 13-case co-op rule table — see the [verification report](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.0.0.md). All 8,386 real save files matched SHA-256.

**Limits:** a real two-player host + client session (including role/pause sync and human NPC visibility) and mouse-wheel injection remain unverified. Roles were not soak-tested over long play sessions.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Новая вкладка «РАБОТА» в F8** (вторая). На одной доске видно состояние каждого напарника, его текущее дело, сколько вещей он несёт, сколько точек лута осталось и почему работа остановилась.
- **Роли напарников:** Любая работа, Кладовщик (только сортировка), Сборщик (только лут), Лагерь (костёр, грядки, багажник) или Без работы. Роль ограничивает только автоматическую работу и приказы всему отряду; прямой приказ конкретному напарнику выполняется всегда.
- **Пауза / продолжить работу базы.** На паузе напарники не берут новую автоматическую работу, сохраняют текущее дело и вещи в руках и идут за вами. После продолжения доделывают начатое. Прямой приказ «Сортировать» или «Собрать лут» снимает паузу.
- **Отмена** дела одного напарника или всех сразу. Переносимые вещи остаются у напарника в рюкзаке — ничего не выбрасывается и не телепортируется.
- **Кооператив:** роли и пауза синхронизируются через систему приказов с проверкой хостом из 1.9.0; чужие напарники — только просмотр.
- **Исправления:** символы, которых нет в шрифте игры (пауза/плей, замок, эмодзи в задачах и баннерах), больше не показываются квадратиками; подсказки не остаются после смены вкладки.
- Включает 1.8.1 (статусы, причины остановки, фильтры, подсказки) и 1.9.0 (надёжные приказы в кооперативе). Роли сохраняются вместе с напарниками; старые конфиги загружаются как «Любая работа». Сброс сохранений и новые зависимости не нужны.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.0.0`. Живая игровая приёмка в настоящей игре на одноразовой копии профиля: нативная стрельба (зомби на 8 и 12 м, враждебный человек), физический сбор лута с земли и переноска между ящиками с **паузой на 4 секунды посреди переноски** (дело и вещь сохранены, ничего не положено), затем продолжение и завершение — 0 телепортаций. Полная графическая матрица с новой вкладкой: **300/300** случаев на шести разрешениях и трёх языках плюс таблица правил кооператива из 13 случаев — в [отчёте о проверке](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.0.0.md). Все 8 386 настоящих файлов сохранений совпали по SHA-256.

**Ограничения:** настоящая сессия вдвоём (хост + клиент), включая синхронизацию ролей и паузы и видимость людей-NPC, а также подача колеса мыши пока не проверены. Длительный игровой прогон ролей не проводился.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
