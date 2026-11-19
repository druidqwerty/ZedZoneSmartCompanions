Title / Заголовок: ZED ZONE: Smart Companions 3.1.2 — Patch 1.0.20, companion nicknames, anti-wander & base logistics / ZED ZONE: Smart Companions 3.1.2 — Патч 1.0.20, никнеймы, анти-блуждание и логистика базы
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support development on Boosty.

## Smart Companions 3.1.2 — Patch 1.0.20, Companion Nicknames, Logistics & Survival

- **ZED ZONE Patch `1.0.20` & Companion Nicknames (`CharacterData.GetDisplayName`):** verified against Patch `1.0.20` (`buildid 26773715`) with full support for custom companion nicknames across **F8 → Squad / Work**, tactical callouts, and dialogue logs (with seamless fallback on empty nicknames or `1.0.19`).
- **Vehicles, Anti-Wandering & Zero-Teleport Unloading:** fixed unlimited-seat (`passengerSeatLimit < 0`) vehicle boarding, eliminated stale-anchor `ReturnToBase` / 1000 m teleportation jumps on `CampGarrison` exit, stopped distant (`> 12 m`) or unseen enemies from interrupting active base sorting (`CarryBuffer` preserved), added equipped backpack unloading and up to `80 m` base chest discovery, fixed single-player chest unmarking (`SC142:`), and kept passive scavenging out of base corpses (`<= 18 m`).
- **Nested-Bag Healing, Combat Responsiveness & Dialogue Polish:** companions now bandage bleeding at `>= 78%` HP using bandages/medkits tucked inside nested pouches (`depth <= 3`, without misclassifying steaks or stews as medicine), switch reliably back to ranged weapons, restore the mouse cursor cleanly without breaking gamepad looting, and match chatter to companion gender and novelty headgear.

### Quick Start (How to Use)

- Press **`F8`** to open the hub (click **`?`** for the built-in Quick Guide) and pick **Squad** doctrines, formations, **`[--]/[A]/[B]`** fireteams, or **Garrison**.
- Open any chest at your base (or press **`F8 → Base`**) to tick categories and mark **one** **`[Drop-off]`** chest — then press **`F7`** so companions physically carry items into matching chests.
- Hotkeys: **`Alt+1/2/3`** All/Alpha/Bravo · **`Alt+F`** sweep nearby loot · **`F7`** physical sort · **`F6`** (`Alt+F6` with AVO) share rations · **`Alt+G`** fan to cursor · **`Alt+H`** healing smoke · **`F9`** battle cry.

Existing saves stay safe. A live two-machine host + client co-op session, manual in-match `NPCNickname` UI flow, and automated hardware mouse-wheel scroll remain open verification boundaries.

Requires the third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), which is not a mod by druidqwerty. After a ZED ZONE game update (such as Patch `1.0.20`), if mods do not load or startup stalls, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so BepInEx regenerates fresh IL2CPP interop files.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: full notes and verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.1.2)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Smart Companions 3.1.2 — Патч 1.0.20, никнеймы, анти-блуждание и логистика базы

- **Совместимость с патчем ZED ZONE `1.0.20` и никнеймы напарников (`CharacterData.GetDisplayName`):** сборка проверена на патче `1.0.20` (`buildid 26773715`) и отображает назначенные игроком никнеймы на карточках **F8 → Отряд / Работа**, в тактических сообщениях и ленте реплик (с безопасным откатом к базовому имени при пустом никнейме или на `1.0.19`).
- **Транспорт, анти-блуждание и разгрузка базы без телепортаций:** исправлена посадка в машины с безлимитными местами (`passengerSeatLimit < 0`), убраны рывки и телепорты `ReturnToBase` после выхода из «Гарнизона», далёкие (`> 12 м`) или невидимые враги больше не срывают текущую переноску лута на базе, добавлена выгрузка надетых рюкзаков и радиус склада до `80 м`, починено снятие разметки ящиков в одиночной игре (`SC142:`), а пассивный лут не трогает трупы у базы (`<= 18 м`).
- **Лечение из вложенных сумок, отзывчивость в бою и диалоги:** бойцы сами перевязывают кровотечение даже при `>= 78%` HP бинтами и аптечками из вложенных подсумков (`depth <= 3`, без путаницы еды с лекарствами), чётко возвращаются на огнестрел, аккуратно возвращают курсор без поломки геймпада и подбирают реплики с учётом пола и необычных головных уборов.

### Быстрый старт (как пользоваться)

- Нажмите **`F8`** для открытия центра управления (кнопка **`?`** — встроенная памятка): выбирайте доктрину, строй, группы **`[--]/[A]/[B]`** и **Гарнизон**.
- Откройте любой ящик базы (или вкладку **`F8 → База`**), отметьте нужные категории и пометьте **один** ящик как **`[Приёмник]`** — затем нажмите **`F7`**, чтобы напарники ногами разнесли добычу.
- Горячие клавиши: **`Alt+1/2/3`** Все/Альфа/Браво · **`Alt+F`** лут рядом · **`F7`** сортировка базы · **`F6`** (`Alt+F6` с AVO) паёк · **`Alt+G`** веер к курсору · **`Alt+H`** мед-дым · **`F9`** боевой клич.

Сохранения не тронуты. Живая сессия двух игроков на двух ПК, ручное переименование в окне `NPCNickname` и аппаратное колесо мыши пока остаются открытыми границами проверки.

Нужен сторонний [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), который не является модом druidqwerty. После обновления самой игры ZED ZONE (например, патча `1.0.20`), если моды не подгружаются или запуск зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или повторно запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: полный список изменений и проверка](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.1.2)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
