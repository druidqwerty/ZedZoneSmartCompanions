Title / Заголовок: ZED ZONE: Smart Companions 3.0.2 — Input, vehicle, garrison, co-op revival & sorting fixes / ZED ZONE: Smart Companions 3.0.2 — Исправления управления, транспорта, гарнизона, реанимации в коопе и стаков
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support development on Boosty.

## Smart Companions 3.0.2 — Community Bugfix & QoL Update

- **Fixed startup WASD / Esc / UI freeze:** loading into a save no longer triggers `LootInventoryPanel`'s lazy singleton creation on the first frame, keeping movement keys, Esc and UI panels responsive immediately.
- **Fixed vehicle boarding, exit & seat overflow:** companions no longer get stuck against vehicle hulls, teleport mid-drive, or stay permanently moved into Camp Garrison when a car lacks seats — overflow squadmates resume following as soon as you step out.
- **Fixed long-range Hold Position & Camp Garrison (>100 m):** anchored guards no longer teleport back to you across distant map chunks, and squad-wide hotkeys skip Camp Garrison defenders.
- **Fixed co-op companions dying immediately instead of kneeling:** downed companions enter ZED ZONE's native kneeling revival state in multiplayer, sync across host and clients, and can be revived via `[+] Revive` in `F8 → Squad` when standing nearby.
- **Smarter looting, stack merging & settings reset:** added **`Alt+F`** quick loot sweep hotkey, automatic stack merging in chests and backpacks, hybrid `[Drop-off + Category]` chest support, vehicle trunk unloading into `[Drop-off]`, Coconut food classification, and a two-step **Reset All Settings** button in `F8 → Settings` (preserving your marked chests and companion XP).

Existing saves are untouched. A real two-machine two-player network session still awaits live two-player confirmation.

Requires the third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), which is not a mod by druidqwerty.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: full notes and verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.0.2)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Smart Companions 3.0.2 — Исправления по отзывам игроков и улучшения логистики

- **Исправлена блокировка WASD, Esc и UI при загрузке сохранения:** убрано преждевременное обращение к ленивому геттеру `LootInventoryPanel.instance` при старте мира, блокировавшее ввод персонажа.
- **Исправлена посадка, высадка и переполнение машин:** напарники больше не застревают у борта авто, не телепортируются в поездке и не остаются навсегда в гарнизоне, если в салоне не хватило мест — после выхода из машины весь отряд снова в строю.
- **Исправлен срыв «Держать позицию» и «Гарнизона базы» дальше 100 м:** охрана лагеря и закреплённые точки больше не телепортируются к игроку на дальних вылазках, а общие горячие клавиши отряда не срывают гарнизон.
- **Исправлена мгновенная смерть напарников в кооперативе:** при падении HP до нуля бойцы встают на колено для реанимации, состояние синхронизируется с клиентами, а рядом с раненым в `F8 → Отряд` доступна кнопка реанимации `[+]`.
- **Горячая клавиша лута `Alt+F`, объединение стаков и сброс настроек:** добавлены сбор лута по **`Alt+F`**, авто-слияние неполных стаков (бинты, патроны, еда) в ящиках и рюкзаках, поддержка гибридных ящиков `[Сброс + категория]`, разгрузка багажника авто, сортировка кокосов в еду и двухшаговая кнопка **Сбросить все настройки** в `F8 → Настройки` (разметка ящиков и опыт бойцов сохраняются).

Сохранения не тронуты. Настоящая парная сессия на двух ПК ещё ждёт живой проверки вдвоём.

Нужен сторонний [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), который не является модом druidqwerty.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: полный список изменений и проверка](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.0.2)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
