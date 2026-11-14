Title / Заголовок: ZED ZONE: Smart Companions 3.0.1 — Hotfix: clean team HUD dismissal & smooth corridor navigation / ZED ZONE: Smart Companions 3.0.1 — Хотфикс: чистое увольнение из HUD игры и движение в узких коридорах
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support development on Boosty.

## Smart Companions 3.0.1 — Dismissal HUD & Corridor Navigation Hotfix

- **Clean dismissal in both Mod & Game UI:** fixed a bug where dismissing a companion removed them from the `F8` mod hub while leaving a ghost card in ZED ZONE's top-left `TeamNPCPanel` (and fixed native `QuitTeam` menu dismissal).
- **Smooth narrow-corridor & doorway movement:** widened `MoveTo`'s internal `10 cm` corner check to `36 cm` human shoulder width so companions route cleanly around doorframes via A* instead of bumping walls, collapsed wide formations into single-file leader trails inside hallways, and added 7-angle wall checks before combat kiting/strafing.

Includes the full **3.0.0 Expeditions** overhaul (vehicle crew & seat-overflow allocation, physical multi-floor `zLevel` bunker transitions, pet synergy, base generators/furnaces, auto bio-masks & sentry sleep, Field Tactics 2.0, and Camp Garrison). Zero item teleportation, existing saves untouched. Real two-machine two-player co-op visibility still awaits live two-player confirmation.

Requires the third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), which is not a mod by druidqwerty.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: full notes and verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.0.1)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Smart Companions 3.0.1 — Хотфикс: чистое удаление из игрового меню и движение в коридорах

- **Чистое увольнение из отряда в обоих меню:** исправлена ошибка, из-за которой отпущенный напарник пропадал из меню мода `F8`, но оставался в нативной панели отряда слева сверху (`TeamNPCPanel`), а также починен нативный пункт «Покинуть отряд».
- **Плавный проход узких коридоров и дверей без утыкания в стены:** внутренний луч `MoveTo` расширен с `10 см` до реальной ширины плеч `36 см` (теперь дверные проёмы и углы обходятся по сетке A*, а не напролом в стену), широкие построения в коридорах перестраиваются в колонну по следам игрока, а в бою перед отскоком проверяются 7 свободных углов.

Включает весь мажорный рубеж **3.0.0 «Экспедиции»** (экипажи транспорта, бункеры/этажи `zLevel`, питомцы, генераторы и печи, авто-противогазы и вахта сна, тактика курсора и гарнизон). Никакой телепортации предметов, сохранения не тронуты. Настоящая парная сессия на двух ПК ещё ждёт живой проверки вдвоём.

Нужен сторонний [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), который не является модом druidqwerty.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: полный список изменений и проверка](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.0.1)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
