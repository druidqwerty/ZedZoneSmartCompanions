# Smart Companions v2.5.0 — Field tactics 2.0: cursor fan move, cover, safe grenades & medic rescue

## English

### Changes

- **Move-to-Cursor fan order (`Alt+G`, `F8 → Squad → To Spot` & radial menu):** send a chosen fighter or the whole squad in a non-overlapping tactical fan (`>= 1.25 m` spacing) to the exact world spot under your cursor via `GOAPAction_TeamOrder_MoveTo`.
- **Smart ranged-fire cover (`GOAPAction_TakeCover`):** companions in `Balanced`, `Bodyguard` or `Sniper/Kite` doctrines automatically trigger native cover maneuvers under enemy gunfire (`Looter` / armed hostile humans) without breaking shooting stability.
- **Offensive grenades with hard `GrenadeSafetyGate` (`F8 → Squad → Grenade` & radial menu):** engages clusters of `>= 3` hostiles within `4.5 m` (or bosses), **strictly blocking** throws if any player, ally or pet is within `6.5 m` OR if any marked base chest is within `18.0 m` (protecting your base warehouse from blast damage).
- **Tactical healing smoke & autonomous Medic rescue (`Alt+H`, companion `Smoke` button & radial menu):** deploys native `GOAPAction_TeamOrder_ThrowHealingSmoke` (with field-bandage fallback when smoke is absent), while `Medic` specialists autonomously sprint to critically wounded teammates (`< 32% HP`) and perform `GOAPAction_RescueAlly` / stabilization.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.5.0` (20 custom NPC commands in RU/EN/ZH). Live in-engine acceptance (`backups/live-20261113-030636`): native ranged combat, physical loot & chest carry (0 teleports), Hold Position & Fall Back Here, reserves & 80-chest soak, Camp Garrison & SafeDismiss physical handover, and live `GrenadeSafetyGate` friendly-fire block verification. Full **300/300** graphical UI matrix across six resolutions and three languages + `FieldTacticsRegression` (`GrenadeSafetyGate` & 6-fighter fan math) + `GarrisonRegression` + `ScaleAndPrivacyRegression` + `SpecializationRegression` + `TacticalRegression` + `ProtocolRegression`. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.5.0.md).

**Limits:** a live two-machine / two-account host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Веерный приказ по курсору (`Alt+G`, `F8 → Отряд → К цели` и радиальное меню):** отправляйте бойца или весь отряд веером без сбивания в кучу (`>= 1.25 м`) в точную точку под курсором мыши через `GOAPAction_TeamOrder_MoveTo`.
- **Умные укрытия под обстрелом (`GOAPAction_TakeCover`):** напарники в доктринах `Умный баланс`, `Телохранитель` и `Снайпер/Кайт` автоматически используют укрытия в перестрелках с вооружёнными людьми-мародёрами.
- **Прицельные гранаты с предохранителем `GrenadeSafetyGate` (`F8 → Отряд → Граната` и радиальное меню):** бросок разрешён только по скоплению `>= 3` врагов (или боссу) и **жёстко блокируется**, если в радиусе `6.5 м` есть игрок, союзник или питомец, либо в радиусе `18.0 м` находится хотя бы один размеченный ящик базы.
- **Лечебный дым и эвакуация раненых (`Alt+H`, кнопка `Мед-дым` и радиальное меню):** применение `GOAPAction_TeamOrder_ThrowHealingSmoke` (с резервной перевязкой при отсутствии шашек) и автоматическое спасение тяжелораненых напарников (`< 32% HP`) медиками через `GOAPAction_RescueAlly`.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.5.0` (20 кастомных команд NPC на RU/EN/ZH). Живая приёмка в настоящей игре (`backups/live-20261113-030636`): стрельба, физический лут и переноска (0 телепортаций), тактика, резервы, склад на 80 ящиков, гарнизон, безопасный отпуск и живая проверка блокировки `GrenadeSafetyGate` рядом с игроком. Полная графическая матрица **300/300** на шести разрешениях и трёх языках + пакет детерминированных проверок `FieldTacticsRegression`. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.5.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
