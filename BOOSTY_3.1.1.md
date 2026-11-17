Title / Заголовок: ZED ZONE: Smart Companions 3.1.1 — Patch 1.0.19 loading hotfix, fireteams & camp survival / ZED ZONE: Smart Companions 3.1.1 — Хотфикс загрузки на патче 1.0.19, группы Альфа/Браво и выживание на базе
---

[![Support development](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Enjoy the mod? Support development on Boosty.

## Smart Companions 3.1.1 — Hotfix for Patch 1.0.19, Save Loading & Profile Self-Repair

- **Fixed save & world loading freezes (`SmartCompanionsRuntimeGate`):** companions, chunk container scans (`EnsureLootGenerated`), survival walks, and IMGUI/input capture now remain strictly gated off in `MainMenu` and throughout ZED ZONE's `"Gaming"` scene / `GameInitPanel` / chunk streaming transition (`timeScale = 0.01`) until gameplay is ready.
- **Patch `1.0.19` compatibility & fault-isolated hooks (`SafeHarmonyPatcher`):** resolved the native `/OPT:ICF` `GOAPAction_SearchForFood` vs `SearchForMeleeWeapon`/`SearchForRangedWeapon` hook collision, isolated all 86 Harmony patch entrypoints per method (`[EH>0]` + `[NoInlining]` IL2CPP wrappers), bound `Assembly-CSharp` across `2.0.0.x` hotfixes via `0.0.0.0`, and unified single-folder Workshop staging.
- **Player profile guard & automatic self-repair:** stopped `MainMenu` / `CharacterSelectOverlay` portrait preview mannequins from mutating player `CharacterData.characterAttrId` (`0 -> 201`), and added automatic self-repair back to `0` on load for any previously affected save or profile.

### Also in 3.1.0 — Fireteams, Camp Survival, Crop Harvest & Base Logistics

- **Fireteam Groups (`[--]` / `[A]` / `[B]`) & `Alt+1/2/3` channels:** split your squad into **Alpha** and **Bravo** on `F8 → Squad` cards and command each group separately.
- **Autonomous camp survival & offline garrison sustain:** companions drink from canteens or camp water wells, eat and bandage from personal and shared inventories, stay sustained offline in Camp Garrison, and keep `HoldPosition` defenders anchored (`0.00 m` drift).
- **Auto-harvest crops, anti-wander & dual-inventory sorting:** **`[x] Auto-harvest crops`** in `F8 → Work` (32 m farming), no random `Explore` roaming, `Auto-Dropoff` enabled by default for `Fighter` presets across personal and shared bags, **`[?]` Quick Guide** (EN/RU/ZH), and Vehicle Combat chatter (**61 topics**, **1,215 unique lines**).

Existing saves are untouched. A live two-machine two-player network session still awaits real two-player confirmation.

Requires the third-party [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), which is not a mod by druidqwerty. After a ZED ZONE game update (such as Patch `1.0.19`), if mods do not load or the game stalls at startup, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so BepInEx regenerates fresh IL2CPP interop files.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: full notes and verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.1.1)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

[![Поддержать разработку](https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png)](https://boosty.to/lrgpp)

Нравится мод? Поддержать разработку можно на Boosty.

## Smart Companions 3.1.1 — Хотфикс загрузки на патче 1.0.19 и автопочинка профиля

- **Устранены зависания загрузки мира (`SmartCompanionsRuntimeGate`):** логика напарников, обход контейнеров чанков (`EnsureLootGenerated`), походы выживания и перехват IMGUI/ввода строго отключены в главном меню и на всём протяжении загрузки сцены `"Gaming"`, `GameInitPanel` и подгрузки чанков (`timeScale = 0.01`) до полной готовности мира.
- **Совместимость с патчем `1.0.19` и изолированные Harmony-хуки (`SafeHarmonyPatcher`):** устранена нативная `/OPT:ICF`-коллизия хука `GOAPAction_SearchForFood` с `SearchForMeleeWeapon`/`SearchForRangedWeapon`, все 86 точек входа Harmony изолированы пометодно (`[EH>0]` + `[NoInlining]` обёртки IL2CPP), привязка к `Assembly-CSharp` переведена на `0.0.0.0` для хотфиксов `2.0.0.x`, а пакет Workshop приведён к однопапочной раскладке.
- **Защита профиля игрока и автоматическое восстановление:** портретные манекены в `MainMenu` / `CharacterSelectOverlay` больше не меняют `CharacterData.characterAttrId` игрока (`0 -> 201`), а при загрузке любого ранее затронутого сохранения/профиля значение автоматически восстанавливается в `0`.

### Также в 3.1.0 — Группы Альфа/Браво, выживание на базе, автосбор урожая и логистика

- **Боевые группы (`[--]` / `[A]` / `[B]`) и каналы `Alt+1/2/3`:** распределяйте бойцов по группам **Альфа** и **Браво** на карточках `F8 → Отряд` и отдавайте раздельные приказы.
- **Автономное выживание и оффлайн-поддержка гарнизона:** бойцы сами пьют из фляг или колодца, едят и перевязываются из личных и общих сумок, не гибнут от голода/жажды в оффлайн-гарнизоне и не покидают посты `HoldPosition` (`0,00 м` дрейфа).
- **Автосбор урожая, анти-блуждание и двойной инвентарь:** переключатель **`[x] Автосбор урожая`** в `F8 → Работа` (огород 32 м), отключение блуждания `Explore`, авто-сдача по умолчанию для «Бойца» из обоих рюкзаков, встроенная памятка **`[?]`** (RU/EN/ZH) и боевые реплики у авто (**61 тема**, **1 215 уникальных реплик**).

Сохранения не тронуты. Настоящая парная сессия на двух ПК ещё ждёт живой проверки вдвоём.

Нужен сторонний [ZED ZONE Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258), который не является модом druidqwerty. После обновления самой игры ZED ZONE (например, патча `1.0.19`), если моды не подгружаются или запуск зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или повторно запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [GitHub: полный список изменений и проверка](https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v3.1.1)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
