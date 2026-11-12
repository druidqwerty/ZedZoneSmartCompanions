# Smart Companions v2.1.0 — Tactical squad control

## English

### Changes

- **Squad formations & follow distance:** choose **Free**, **Wedge**, **Line**, **Column** or **Guard Ring** and **2.5 m**, **4 m**, **6.5 m** or **9 m** right from the tactical bar in **F8 → Squad** (or **F8 → Settings**). Formations keep your forward line of fire clear.
- **Direct positional orders** (squad-wide, per companion in F8, and in the native NPC radial menu):
  - **Hold Position:** hold the current spot and fire from there without chasing far off the anchor.
  - **Fall Back Here:** break off pursuit and run back to your current position (cancelling base jobs if needed), then hold that fallback point.
  - **Cover Direction:** hold position and prioritize targets in the direction you are aiming.
  - **Follow:** clear positional holds and resume following in formation.
- **Narrow-passage yield & ally spacing:** trailing companions yield single-file in tight doorways and keep spacing so teammates do not stack on one spot.
- **Configurable retreat thresholds:** set **Wounded Retreat** (`Off / <25% / <30% / <50% HP`) and **Low-Ammo Retreat** (`Off / 0 ammo / <=5 ammo`) in **F8 → Settings**. When triggered, the companion steps back behind the leader and shows why on their card.
- **Clear order & block status:** each companion card shows the active combat order (`Following`, `Holding position`, `Falling back`, `Covering sector`) and any active override or block (`no path to anchor`, `retreat: wounded`, `retreat: out of ammo`, `cease-fire active`, `on base task`, `different floor`).
- **Fix:** `CollectResource` follow-leash suppression stays active even when `StopFire` / `Passive` cease-fire is enabled while an enemy is in sight.
- **Co-op:** tactical orders, anchor coordinates, aim sectors, formations and follow distances sync through the 1.9.0 host-authoritative system. Existing saves and configs remain compatible.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.1.0` (15 custom NPC commands in RU/EN/ZH). Live in-engine acceptance on a disposable profile copy: native ranged combat (8 m & 12 m zombie, StopFire negative case, 8 m hostile human), physical ground loot + chest-to-chest carry with 4 s mid-carry pause/resume (0 teleports), **Hold Position** (0.00 m drift for 2.5 s while player moved 9 m away), **Fall Back Here** (7.17 m run to fallback point), and **Cover Sector** threat priority. Full **300/300** graphical UI matrix across six resolutions and three languages + 16/16 tactical math checks + 13/13 co-op authority checks. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.1.0.md).

**Limits:** a live two-player host + client session (including human NPC visibility and live co-op tactical sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Построения и дистанция следования:** выбирайте **Свободно**, **Клин**, **Шеренга**, **Колонна** или **Круг** и дистанцию **2.5 м**, **4 м**, **6.5 м** или **9 м** прямо на тактической панели во вкладке **F8 → Отряд** (или в **Настройках**). Построения оставляют вашу линию огня свободной.
- **Боевые приказы позиции** (всему отряду, отдельному напарнику в F8 и в нативном радиальном меню NPC):
  - **Держать позицию:** удерживать текущую точку и вести огонь с места, не убегая далеко в погоню.
  - **Отступить сюда («Ко мне!»):** разорвать бой и бегом отступить к вашей позиции, затем удерживать этот рубеж.
  - **Прикрывать сектор:** удерживать точку и в первую очередь встречать врагов в направлении вашего прицела.
  - **Следовать:** снять удержание и вернуться в строй.
- **Узкие проходы и интервал союзников:** идущие сзади напарники уступают узкие двери в колонну по одному и не сбиваются в одну точку.
- **Пороги отступления:** настраивайте **Отход при ранении** (`Выкл / <25% / <30% / <50% HP`) и **Отход без патронов** (`Выкл / 0 патр. / <=5 патр.`) в **F8 → Настройки**. При срабатывании напарник отходит за спину командира и показывает причину на карточке.
- **Наглядный статус приказа:** на карточке каждого напарника показан текущий боевой приказ и причина задержки или отхода (`нет пути к точке`, `отход: ранен`, `отход: нет патронов`, `приказ «Не стрелять»`, `занят работой базы`, `другой этаж`).
- **Исправление:** подавление поводка `FollowLeader` при сборе ресурсов (`CollectResource`) теперь сохраняется, даже если включён режим «Не стрелять» (`StopFire` / `Passive`) рядом с врагами.
- **Кооператив:** боевые приказы, координаты точек, сектора, построения и дистанция синхронизируются через систему с проверкой хостом из 1.9.0. Старые сохранения и конфиги полностью совместимы.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.1.0` (15 пользовательских команд NPC на RU/EN/ZH). Живая приёмка в настоящей игре на одноразовой копии профиля: стрельба (зомби на 8 и 12 м, проверка `StopFire`, враждебный человек на 8 м), физический лут и переноска между ящиками с паузой 4 с (0 телепортаций), **Держать позицию** (0.00 м смещения за 2.5 с при отходе игрока на 9 м), **Отступить сюда** (пробежка 7.17 м к точке отхода) и приоритет **Прикрытия сектора**. Полная графическая матрица **300/300** на шести разрешениях и трёх языках + 16/16 проверок тактики + 13/13 правил кооператива. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.1.0.md).

**Ограничения:** настоящая сессия вдвоём (хост + клиент), включая видимость людей-NPC и сетевую синхронизацию приказов вдвоём, а также автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
