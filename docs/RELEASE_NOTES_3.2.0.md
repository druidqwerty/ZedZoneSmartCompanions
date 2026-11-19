# Smart Companions v3.2.0 — Automatic NPC Equipment & Inventory Repair (Patch 1.0.20)

## English

### What's new in v3.2.0

- **Automatic NPC Equipment & Inventory Repair (`CompanionGearRepairSystem`):**
  - **All 18 equipped slots covered:** automatically inspects and repairs companions' active & holstered weapons (`currentWeaponItemData`, `weaponItemDataAry`, `rangedWeapon.weaponData`), shield, helmet/hat, face mask (plus `SavedEverydayMaskByGuid` backup mask), head decoration, torso/vest + layered torso items, legs + layered leg items, boots, cloak, backpack, exoskeleton, thruster, special equipment, lighting tool, and handheld devices.
  - **Full companion inventory & nested pouch coverage:** scans `characterData.inventoryData`, `npcData.sharedInventoryData`, equipped `backpackItemData.inventoryData`, and nested bags/pouches (`depth <= 3`, `Pointer`-deduplicated), including completely broken (`0%`) items.
  - **Exact ZED ZONE `1.0.20` native durability & weapon maintenance:** restores `ItemType.RangedWeapon` HP via `properties[0]` up to `ItemAttr_RangedWeapon.hp`, clears weapon jams (`MalfunctionFlag` + `BasicRangedWeapon.CoolDownOnMalfunctionCleared`), maintains attached gun parts (`properties[9, 11, 13]` out of `firingDurability`), attached magazines (`properties[2]` out of `100f`), standalone `ItemType.GunPart` (`properties[0]`), and all other `ItemAttr.Repairable` gear (`item.durability` out of `100f`). Strictly excludes food, ammo, arrows, throwables, liquids, containers, deployables, and non-tool materials.
  - **4 live modes in `F8 → Settings` (RU / EN / ZH, no restart needed):**
    - **Materials + Maint** *(default, `MaterialsOrField`)*: uses exact native repair recipes (`ItemAttr.repairData` incl. `RepairLiquidMaterial`) and ranged spare parts (`itemId + 1000`) when available, falling back to gradual out-of-combat field maintenance when out of parts.
    - **Materials only** (`MaterialsOnly`): strict realism — repairs only when real materials/parts are available in the companion's own inventories or a nearby player-marked chest (`<= 2.45 m`, zero teleportation).
    - **Free maint** (`FreeMaintenance`): automatic out-of-combat upkeep without consuming repair materials.
    - **Off** (`Off`): disables automatic NPC gear repair completely.
  - **Zero-teleportation, atomic consumption & offline garrison upkeep:** checks full material availability before consuming anything (never wastes partial materials), protects equipped items and non-empty containers from being consumed as scrap, replaces the legacy 5-slot `BaseChoreSystem` repair stub, and runs both out-of-combat (`CompanionSurvivalSystem.TickCompanionSurvival`) and for unloaded camp defenders (`SustainOfflineGarrisonCompanion`).

### Verification & open boundaries

Rebuilt `Release` (`-warnaserror`, `0 warnings, 0 errors`) against ZED ZONE **Patch `1.0.20` (`buildid 26773715`)**, `AssemblyRef 0.0.0.0`, **90** Harmony patches (`failed=0`, **100%** `[EH > 0]`), `[SelfVerify] PASS v3.2.0`, `0` lazy `.instance` reads, on frozen DLL `cad63c2bb8e1ae5dbcbf543aea2373f6073dd3cbada2e843e6c9eca672ef1533` (`674,304` B). All **5/5** Gate 2 live engine runs passed on `1.0.20`: `run_live_acceptance.py` (`202926` in `56.7s`, incl. new `V312Live (e)` testing `MaterialsOnly` broken armor `0->100` with recipe `3->0` and `MaterialsOrField` equipped rifle `120->400/400` unjam + attached gun part + personal/shared/nested-pouch items + food untouched), `run_real_menu_load.py` (`203049` in `25.7s`), `run_ui_smoke.py` (`203339`, `13/13` suites incl. `V312Regression (i)`, `300/300` UI matrix + `3` QuickGuide PNGs, visually verified in RU/EN/ZH), and Win32 OS probes at `0.75x` (`230711`) and `2.0x` (`230849`), with `8,386/8,386` real `GameData` files SHA-256 identical and `0` exceptions in `Player.log` across all runs. [Full verification report](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.2.0.md).

**Post-update note:** After a ZED ZONE game update (such as Patch `1.0.20`), if BepInEx / `Code Mod Loader` fails to load or stalls at startup, delete `<ZED ZONE>/BepInEx/interop` (or re-run `Code Mod Loader`'s `install.cmd`) once so IL2CPP interop assemblies regenerate cleanly.

**Open boundaries:** automated hardware mouse-wheel scroll (`wheel_verified=false`), a live two-machine/two-account Steam `Host + Client` co-op session (`UiSmoke` covers co-op logic `13/13` in-fixture only), and manual in-match `NPCNickname` UI renaming (`1.0.20` nickname support verified via `UiSmoke V312Regression (h)`).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Что нового в v3.2.0

- **Автоматический ремонт снаряжения и инвентаря NPC (`CompanionGearRepairSystem`):**
  - **Все 18 слотов экипировки:** напарники автоматически обслуживают и чинят активное и запасное оружие (`currentWeaponItemData`, `weaponItemDataAry`, `rangedWeapon.weaponData`), щит, шлем/шапку, противогаз/маску (включая сохранённую обычную маску `SavedEverydayMaskByGuid`), головные аксессуары, броню/одежду торса и ног (включая многослойную), обувь, плащ, рюкзак, экзоскелет, ускоритель, спецснаряжение, фонарь и ручные приборы.
  - **Весь инвентарь и вложенные сумки напарников:** проверяются личный инвентарь (`characterData.inventoryData`), общий инвентарь (`npcData.sharedInventoryData`), надетый рюкзак (`backpackItemData.inventoryData`) и вложенные подсумки (`depth <= 3`, дедупликация по `Pointer`), включая полностью сломанные вещи (`0%`).
  - **Точная работа с нативной прочностью ZED ZONE `1.0.20`:** огнестрел (`ItemType.RangedWeapon`) чинится через `properties[0]` до `ItemAttr_RangedWeapon.hp` со снятием клина (`MalfunctionFlag` + `BasicRangedWeapon.CoolDownOnMalfunctionCleared`), обвесы на оружии — через `properties[9, 11, 13]` до `firingDurability`, пристёгнутые магазины — через `properties[2]` до `100f`, отдельные оружейные модули (`ItemType.GunPart`) — через `properties[0]`, а всё остальное чинимое снаряжение (`ItemAttr.Repairable`) — через `item.durability` до `100f`. Еда, патроны, стрелы, гранаты, жидкости, канистры, стройблоки и обычные материалы строго не затрагиваются.
  - **4 режима в `F8 → Настройки` (RU / EN / ZH, применяются сразу без перезапуска):**
    - **Материалы + ТО** *(по умолчанию, `MaterialsOrField`)*: сначала чинит по точным рецептам игры (`ItemAttr.repairData`, включая жидкие реагенты `RepairLiquidMaterial`) и запчастям для огнестрела (`itemId + 1000`), а при их отсутствии выполняет постепенное полевое обслуживание вне боя.
    - **Только материалы** (`MaterialsOnly`): строгий режим — ремонт выполняется только при наличии реальных материалов/запчастей у самого напарника или в отмеченном ящике рядом (`<= 2.45 м`, без телепортации).
    - **Бесплатное ТО** (`FreeMaintenance`): автоматическое обслуживание снаряжения вне боя без расхода материалов.
    - **Выкл** (`Off`): полностью отключает авторемонт снаряжения NPC.
  - **Без телепортаций, атомарное списание и оффлайн-гарнизон:** перед списанием проверяется наличие всех компонентов рецепта (материалы не сгорают впустую), экипированные вещи и непустые контейнеры защищены от разбора на материалы, старая 5-слотовая заглушка `BaseChoreSystem` заменена на единую систему, которая работает как вне боя (`CompanionSurvivalSystem`), так и у выгруженного гарнизона базы (`SustainOfflineGarrisonCompanion`).

### Проверка и ограничения

Сборка `Release` (`-warnaserror`, `0` предупреждений и `0` ошибок) под ZED ZONE **Patch `1.0.20` (`buildid 26773715`)**, `AssemblyRef` `0.0.0.0`, **90** Harmony-патчей (`failed=0`, **100%** `[EH > 0]`), `[SelfVerify] PASS v3.2.0`, `0` обращений к ленивым геттерам `.instance`, замороженная DLL `cad63c2bb8e1ae5dbcbf543aea2373f6073dd3cbada2e843e6c9eca672ef1533` (`674 304` Б). Все **5/5** живых прогонов на `1.0.20` пройдены: `run_live_acceptance.py` (`202926` за `56.7 с`, вкл. новый тест `V312Live (e)`: `MaterialsOnly` сломанная броня `0->100` с рецептом `3->0` и `MaterialsOrField` экипированная винтовка `120->400/400` + снятие клина + обвес + личная/общая/вложенная сумки + еда не тронута), `run_real_menu_load.py` (`203049` за `25.7 с`), `run_ui_smoke.py` (`203339`, `13/13` наборов с `V312Regression (i)`, `300/300` графических кадров + `3` QuickGuide PNG, визуально проверено на RU/EN/ZH), Win32 OS-пробы на `0.75x` (`230711`) и `2.0x` (`230849`); все **8 386/8 386** файлов реального `GameData` побайтово совпали, `0` исключений в `Player.log` во всех прогонах. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.2.0.md).

**Памятка после обновлений игры:** после выхода патча самой игры ZED ZONE (например, `1.0.20`), если моды не подгружаются или запуск зависает на старте, один раз удалите папку `<ZED ZONE>/BepInEx/interop` (или повторно запустите `install.cmd` из `Code Mod Loader`), чтобы BepInEx пересобрал свежие файлы IL2CPP interop.

**Ограничения:** автоматическая аппаратная прокрутка колесом мыши (`wheel_verified=false`), настоящая сетевая сессия двух аккаунтов/ПК `Host + Client` (`UiSmoke` проверяет логику кооператива `13/13` на фикстурах) и ручное внутриигровое переименование через окно `NPCNickname` (`1.0.20` поддержка никнеймов проверена тестом `UiSmoke V312Regression (h)`).

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
