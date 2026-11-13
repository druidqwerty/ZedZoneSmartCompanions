# Smart Companions v2.7.0 — Industrial base & power grid (zero teleportation)

## English

### Changes

- **Zero-teleport power grid & heater maintenance (`StirlingGenerator`, `BatteryCharger`, `EnergyStorage`, `Heater`):** when a generator or heater runs low on fuel, base workers (`Any`, `Camp` or `Sorter` roles) physically walk (`<= 2.45 m`) to a marked `Vehicle & Power` / `Materials` chest, pick up approved fuel into `CarryBuffer`, walk to the station, load its `productionData` fuel slot and start the unit. Depleted and `100%` charged batteries are rotated physically between `BatteryCharger` and base chests.
- **Furnace, electric furnace & camp kitchen logistics (`Furnace`, `ElectricFurnace`, `Stove`, `CookingHob`):** companions physically collect finished ingots, charcoal and cooked meals from station output inventories into categorized base chests and refuel furnaces/stoves.
- **Strict fuel whitelist (`IsApprovedStationFuelItem`):** companions are strictly forbidden from burning weapons, armor, clothing, medicine, food, books, cash or valuable crafting parts — only explicit fuel items (firewood, charcoal, coal, fuel canisters) may enter fuel slots.
- **Work tab controls (`F8 → 02 WORK`):** toggle **Power & Furnaces** automation right on the Work board; mid-carry combat or global Pause keeps carried canisters/batteries safely in `CarryBuffer` without teleportation or loss.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.7.0`. Live in-engine acceptance: ranged combat, physical loot & chest carry, tactical orders, reserves, 80-chest soak, Camp Garrison, SafeDismiss, `GrenadeSafetyGate`, bio-masks & sleep shifts, and live `BaseIndustrySystem` fuel-whitelist & station routing checks. Full **300/300** graphical UI matrix across six resolutions and three languages + `IndustryRegression` + `SurvivalRegression` + `FieldTacticsRegression` + `GarrisonRegression` + `ScaleAndPrivacyRegression` + `SpecializationRegression` + `TacticalRegression` + `ProtocolRegression`. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.7.0.md).

**Limits:** a live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Энергосеть и обогрев без телепортации (`StirlingGenerator`, `BatteryCharger`, `EnergyStorage`, `Heater`):** при нехватке топлива рабочий физически идёт (`<= 2.45 м`) к ящику базы, берёт горючее в `CarryBuffer`, подходит к генератору или обогревателю, загружает топливный слот и запускает станцию, а также меняет разряженные и заряженные аккумуляторы в `BatteryCharger`.
- **Печи и полевая кухня (`Furnace`, `ElectricFurnace`, `Stove`, `CookingHob`):** физическая выгрузка готовых слитков, угля и приготовленных блюд из выходных слотов станков по размеченным ящикам склада и поднос топлива.
- **Жёсткий белый список топлива (`IsApprovedStationFuelItem`):** полный запрет на сжигание оружия, брони, одежды, медицины, еды, книг, денег и ценных деталей — в топку идут только дрова, уголь и разрешённое топливо.
- **Управление на вкладке `F8 → 02 РАБОТА`:** переключатель обслуживания генераторов и печей; при бое или паузе канистры и батареи сохраняются в `CarryBuffer` без потерь и без телепортации.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.7.0`. Живая приёмка в настоящей игре + полная графическая матрица **300/300** на шести разрешениях и трёх языках + пакет проверок `IndustryRegression`. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.7.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
