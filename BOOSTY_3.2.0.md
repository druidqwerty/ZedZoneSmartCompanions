Title / Заголовок: ZED ZONE: Smart Companions 3.2.0 — Automatic NPC gear & inventory repair / ZED ZONE: Smart Companions 3.2.0 — Автоматический ремонт снаряжения и инвентаря NPC
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support development on Boosty.

## Smart Companions 3.2.0 — Automatic NPC Equipment & Inventory Repair (Patch 1.0.20)

- **Automatic Equipment & Inventory Maintenance:** companions now maintain **all 18 equipped slots** (active & holstered firearms, melee weapons, shields, helmets, gas masks & saved everyday masks, layered torso/leg armor, boots, cloaks, backpacks, exoskeletons, thrusters, special gear, lighting & handheld tools) as well as repairable items inside their **personal inventory, shared bag, equipped backpack, and nested pouches (`depth <= 3`)** — including completely broken (`0%`) items.
- **Exact Native Firearm & Attachment Care:** restores ranged weapon HP, clears weapon jams (`MalfunctionFlag`), and maintains attached gun parts, magazines, and standalone gun parts using ZED ZONE `1.0.20`'s exact durability model while strictly leaving food, ammo, liquids, and non-tool consumables untouched.
- **4 Live Modes in `F8 → Settings` (No Restart Required):** switch anytime between **Materials + Maint** *(default — consumes real repair recipes & spare parts first, with gradual out-of-combat field maintenance fallback)*, **Materials only** *(strict zero-teleport repair from own inventories or marked base chests within `2.45 m`)*, **Free maint**, or **Off**. Offline Camp Garrison defenders stay maintained too.
- **Patch `1.0.20` Ready:** verified on ZED ZONE Patch `1.0.20` (`buildid 26773715`) together with `3.1.2`'s companion nicknames, unlimited-seat vehicle boarding, anti-wander fixes, zero-teleport unloading, and nested-pouch medical care.

### Quick Start (How to Use)

- Press **`F8`** to open the hub (click **`?`** for the built-in Quick Guide) and pick **Squad** doctrines, formations, **`[--]/[A]/[B]`** fireteams, or **Garrison**.
- Open **`F8 → Settings`** to choose your **NPC Gear Auto-Repair** mode (`Materials + Maint` / `Materials only` / `Free maint` / `Off`) live in-game.
- Open any chest at your base (or press **`F8 → Base`**) to tick categories and mark **one** **`[Drop-off]`** chest — then press **`F7`** so companions physically carry items into matching chests.

Existing saves stay safe. A live two-machine host + client co-op session, manual in-match `NPCNickname` UI flow, and automated hardware mouse-wheel scroll remain open verification boundaries.

Requires the third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), which is not a mod by druidqwerty. After a ZED ZONE game update (such as Patch `1.0.20`), if mods do not load or startup stalls, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so BepInEx regenerates fresh IL2CPP interop files.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: full notes and verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.2.0)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Smart Companions 3.2.0 — Автоматический ремонт снаряжения и инвентаря NPC (Патч 1.0.20)

- **Автоматический ремонт всей экипировки и сумок напарников:** бойцы сами обслуживают **все 18 слотов снаряжения** (активный и запасной огнестрел, ближний бой, щиты, шлемы, противогазы и сохранённые обычные маски, многослойную броню/одежду торса и ног, обувь, плащи, рюкзаки, экзоскелеты, ускорители, фонари и приборы), а также всё чинимое снаряжение в **личном, общем инвентаре, рюкзаке и вложенных подсумках (`depth <= 3`)** — включая полностью сломанные (`0%`) вещи.
- **Точный уход за огнестрелом и обвесами:** восстанавливают прочность оружия и снимают клины (`MalfunctionFlag`), чинят пристёгнутые обвесы, магазины и отдельные оружейные модули по родным формулам ZED ZONE `1.0.20`, строго не трогая еду, патроны, жидкости и расходники.
- **4 режима в `F8 → Настройки` (применяются сразу, без перезапуска):** переключайтесь на лету между **Материалы + ТО** *(по умолчанию — сначала расходует реальные рецепты и запчасти, а при их нехватке выполняет постепенное полевое обслуживание вне боя)*, **Только материалы** *(строгий режим без телепортаций — берёт материалы из своих сумок или отмеченных ящиков рядом `<= 2.45 м`)*, **Бесплатное ТО** и **Выкл**. Снаряжение выгруженного Гарнизона базы также поддерживается в порядке.
- **Проверено на патче ZED ZONE `1.0.20` (`buildid 26773715`):** полностью сохраняет никнеймы напарников, посадку в безлимитный транспорт, защиту от бродяжничества, разгрузку базы без телепортаций и лечение из вложенных сумок.

### Быстрый старт (как пользоваться)

- Нажмите **`F8`** для открытия центра управления (кнопка **`?`** — встроенная памятка): выбирайте доктрину, строй, группы **`[--]/[A]/[B]`** и **Гарнизон**.
- В **`F8 → Настройки`** выберите удобный режим **Авторемонта снаряжения NPC** (`Материалы + ТО` / `Только материалы` / `Бесплатное ТО` / `Выкл`) прямо во время игры.
- Откройте любой ящик базы (или вкладку **`F8 → База`**), отметьте нужные категории и пометьте **один** ящик как **`[Приёмник]`** — затем нажмите **`F7`**, чтобы напарники ногами разнесли добычу.

Сохранения не тронуты. Живая сессия двух игроков на двух ПК, ручное переименование в окне `NPCNickname` и аппаратное колесо мыши пока остаются открытыми границами проверки.

Нужен сторонний [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), который не является модом druidqwerty. После обновления самой игры ZED ZONE (например, патча `1.0.20`), если моды не подгружаются или запуск зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или повторно запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: полный список изменений и проверка](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.2.0)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
