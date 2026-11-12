# Smart Companions v1.9.0 — Safer co-op orders

## English

### Changes

- **Host-authoritative companion orders.** In co-op, the host now checks every doctrine or ownership change sent by a client. Only the player who owns a companion can change it; nobody can hand someone else's companion to themselves.
- **No more order tug-of-war.** Each change carries a revision number. When two players change the same companion at the same moment, the first change wins and the host immediately re-sends the real state, so every screen shows the same doctrine instead of drifting apart.
- **Fast clicks still land.** Several quick changes by the owner are applied in order instead of being lost.
- **View-only cards for allies' companions.** In F8 → Squad, companions owned by another player show "🔒 view only" with the owner's name; their buttons are disabled.
- **Mixed versions keep working.** 1.9.0 accepts packets from 1.8.x players (without revision checks), and 1.8.x ignores the new fields. Both players should still update to get the protection.
- Everything from 1.8.1 (squad status, stop reasons, filters, hints) is included. Single-player gameplay is unchanged. No save reset or new dependency.

### Verification and limitations

Build: 0 warnings/errors. In the real game the co-op decision table passed 13/13 cases (owner, case-insensitive IDs, other player, steal attempt, unowned claim, stale and older revisions, legacy packets, rapid clicks, simultaneous changes). The final DLL passed the full 270-case graphical matrix on six resolutions and three languages; all 8,386 save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.9.0.md).

**Honest limit:** the network path itself (two game instances, host + client) could not be run on one PC. The rules the host applies are proven; delivery between two real players and human NPC visibility in co-op still await a two-player test. Please report co-op issues in the Workshop discussion.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Приказы напарникам проверяет хост.** В кооперативе хост проверяет каждую смену доктрины или владельца от клиента. Менять напарника может только его владелец; чужого напарника нельзя «переписать» на себя.
- **Нет перетягивания приказов.** Каждое изменение получает номер версии. Если двое меняют одного напарника одновременно, побеждает первое изменение, а хост сразу рассылает настоящее состояние — у всех на экране одна и та же доктрина.
- **Быстрые клики не теряются.** Несколько быстрых изменений владельца применяются по порядку.
- **Чужие напарники — только просмотр.** Во вкладке F8 → «Отряд» напарники другого игрока помечены «🔒 только просмотр» с именем владельца; кнопки неактивны.
- **Смешанные версии работают.** 1.9.0 принимает пакеты от игроков на 1.8.x (без проверки версий), а 1.8.x игнорирует новые поля. Для защиты обновиться стоит обоим.
- Всё из 1.8.1 (статусы, причины остановки, фильтры, подсказки) сохранено. Одиночная игра не изменилась. Сброс сохранений и новые зависимости не нужны.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок. В настоящей игре таблица решений кооператива прошла 13/13 случаев (владелец, регистр ID, чужой игрок, попытка забрать, захват ничейного, устаревшие версии, старые пакеты, быстрые клики, одновременные изменения). Финальный DLL прошёл полную графическую матрицу из 270 случаев на шести разрешениях и трёх языках; все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-1.9.0.md).

**Честное ограничение:** сам сетевой путь (две копии игры, хост + клиент) на одном ПК запустить нельзя. Правила, которые применяет хост, проверены; доставка между двумя настоящими игроками и видимость людей-NPC в кооперативе ещё ждут проверки вдвоём. Сообщайте о проблемах в обсуждениях Workshop.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
