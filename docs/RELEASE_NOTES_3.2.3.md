# ZED ZONE: Smart Companions 3.2.3 — Battery Charger & Charcoal Fuel Fix

## What's Fixed

- **Fixed Battery Charger premature unload & full Vehicle Battery loop (`BaseIndustrySystem`, `BaseSortingSystem`):**
  - Companions now read real battery charge via ZED ZONE's native `ItemFeature_Battery.GetBatteryRemainingPower` / `batteryCapacityFloat` (`"RemainingBattery"` Wh) instead of casing `durability`, and use exact native `BatteryCharger` cell states (`1 = full`, `2 = charging`) instead of treating charging batteries as full.
  - Partially charged batteries stay in the charger until `>= 99.9%` charged; fully charged `Vehicle Battery` (`id=85`, `1200 Wh`) and `High-Power Battery Module` (`id=86`, `5000 Wh`) are never reloaded into a charger; companions only load depleted batteries (`< 85%`) into powered, switched-on chargers (`isPowerSwitchedOn && powerInputSufficientFloat > 0.001f`).
  - Excluded `BatteryCharger` from regular storage chest scanning (`BaseSortingSystem`) and source/destination loops, and added arrival re-validation so stale carry jobs never move an already-full or already-removed battery.
- **Fixed non-combustible Charcoal being loaded into furnaces and generators (`BaseIndustrySystem`):**
  - Enforced native `ItemFeatureType.Combustible` + `PassesFeatureLimit` validation before feeding `StirlingGenerator`, `Heater`, or `Furnace` stations — companions no longer load non-combustible `Charcoal` (`id=6`, `Ash` only in vanilla `1.0.20`) or `Wood Dagger` (`id=71`) as fuel.
  - Replaced slot guessing with explicit native station slots (`StirlingGenerator` fuel = `inventoryData1`, `BatteryCharger` = `inventoryData1`, `Furnace` material/output/fuel = `inventoryData1/2/3`, `ElectricFurnace` output = `inventoryData2`), respected manual power switches (`isPowerSwitchedOn`) and `craftFinishFlag`, verified destination space before unloading, and added automatic clearing of accumulated Charcoal/ash from fuel slots when free space runs low.
- **Includes all `3.2.2`–`3.2.0` features:** opt-in Event Log Recording & `[!] Mark Bug` (`F8 → Settings`), Fever/Cold companion freeze fix, automatic NPC gear repair across all 18 equipped slots and nested bags, and full ZED ZONE Patch `1.0.20` support.

## Verification

- Verified on ZED ZONE **Patch `1.0.20`** (`buildid 26773715`) on frozen `SmartCompanions.dll` (`711,168` B, `SHA-256: 0c2dae8f21d0a47e527fb53fa8b0fc67b3a8aa9636d33d09fcf3be6b420c58e7`, `Assembly-CSharp AssemblyRef = 0.0.0.0`, `92` Harmony patches `failed=0`):
  - `run_ui_smoke.py` (`backups/ui323-20261125-110150`): `exit 0`, `300/300` repaint cases (`303` PNGs), `13/13` suites PASS including live `ItemManager` `IndustryRegression` (`combustibles=17, approvedFuels=2, nonCombustibleCharcoal=1, batteries=7, vehicleBatteries=2`), `Player.log` `0` exceptions, `8,386/8,386` `GameData` files unchanged.
  - `run_live_acceptance.py` (`backups/live-20261125-110709`): `exit 0`, `53.7s`, all live suites PASS including new **`V312Live (h)`** native `ChargeBattery`, `FindLastCombustibleIndex`, and `PassesFeatureLimit` checks, `Player.log` `0` exceptions, `8,386/8,386` `GameData` files unchanged.
  - `run_real_menu_load.py` (`backups/menuload-20261125-110806`): `exit 0`, `25.5s`, native `MainMenu → Save → Gaming` load + live `F8` toggle PASS, `Player.log` `0` exceptions, `8,386/8,386` `GameData` files unchanged.

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

# ZED ZONE: Smart Companions 3.2.3 — Фикс зарядной станции и древесного угля

## Что исправлено

- **Исправлен цикл зарядной станции (`Battery Charger`) с недозаряженными батареями и полными автоаккумуляторами:**
  - Напарники теперь проверяют реальный заряд батареи через нативный `ItemFeature_Battery.GetBatteryRemainingPower` / `batteryCapacityFloat` (`RemainingBattery`, Вт·ч) вместо прочности корпуса `durability`, а также корректно читают статус ячеек (`1 = заряжена`, `2 = заряжается`).
  - Недозаряженные батареи остаются в зарядке до `>= 99.9%`; полностью заряженные автомобильные аккумуляторы (`Vehicle Battery`, `1200 Вт·ч`) и энергомодули (`5000 Вт·ч`) больше не закладываются обратно в зарядку; разряженные батареи (`< 85%`) загружаются только во включённые зарядные станции с достаточным питанием.
  - `BatteryCharger` исключён из обычного сканирования складских ящиков (`BaseSortingSystem`) и повторно проверяется по приходу напарника к станции.
- **Исправлена загрузка негорючего древесного угля (`Charcoal`) в печи и генераторы:**
  - Перед подачей топлива в генераторы Стирлинга, обогреватели и каменные печи проверяется нативный признак `ItemFeatureType.Combustible` и `PassesFeatureLimit` — негорючий в ванильной `1.0.20` древесный уголь (`id=6`, только `Ash`) и деревянный кинжал (`id=71`) больше не используются как топливо.
  - Убрано угадывание слотов станков: используются точные нативные слоты входа/выхода/топлива, учитываются ручное выключение питания (`isPowerSwitchedOn`), флаг готовности (`craftFinishFlag`) и наличие места в целевом ящике; скопившийся уголь/зола выгружаются из топливного слота при нехватке свободных ячеек.

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
