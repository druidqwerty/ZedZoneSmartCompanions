# Smart Companions v2.2.0 — Specializations, reserves & loot zones

## English

### Changes

- **Companion specializations (`F8 → Work`):** assign **Fighter**, **Medic**, **Supplier** (Quartermaster) or **Scavenger** presets per companion (or keep **Custom**). Each preset configures combat doctrine, base work role, preferred weapon loadout, support toggles and non-reducible supply reserves.
- **Squad-wide profile preview before applying:** open **Squad Profile** in **F8 → Work** to see an exact diff count across your squad (`Role`, `Gear`, `Doctrine`, `Reserve Ammo/Food/Med` changes) before clicking **Apply to Squad**.
- **Preferred loadout & non-reducible backpack reserves:** choose **Auto gear**, **Ranged** or **Melee** preference and set minimum backpack reserves (`0/0/0`, `24/2/1`, `45/3/2`, `60/4/4` for Ammo / Food / Medicine). Base sorting and auto-dropoff never strip a companion below their reserve or take equipped weapons, and routine field-medic healing preserves their medical reserve.
- **Separate permissions for spending rare supplies (`F8 → Settings → Squad support`):**
  - **Spend rare dishes** (off by default — protects steaks and gourmet dishes when sharing rations while ordinary food is available or at all when disabled).
  - **Spend rare serums** (off by default — protects serums, doses and blood bags during routine field healing; emergency downed rescue always saves your life).
  - **Share rare calibers** (on by default — toggle off to prevent emergency ammo supply from handing out small-stack heavy/sniper calibers).
- **Configurable loot zones & base work priority (`F8 → Work`):**
  - Cycle automatic base job priority (**Sort → Loot**, **Loot → Sort**, **Camp → Sort**).
  - Choose loot sweep radius (**14 m**, **24 m**, **36 m**, **48 m**) and **Pin** a scavenging zone at a chosen spot (marked base chests remain protected).
- **Co-op:** specializations, loadouts and reserve minimums sync through the host-authoritative system. Existing saves and configs remain compatible.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.2.0`. Live in-engine acceptance on a disposable profile copy (`backups/live-20261112-154729`): native ranged combat, physical ground loot + chest carry with 4 s mid-carry pause/resume (0 teleports), Hold Position & Fall Back Here, live `ItemData` backpack reserve protection (keeps exact 2-medkit reserve, releases 3rd surplus), and live `RationSharing` rare-steak guard (protects Spicy Meat Steak `248` when disabled, consumes ordinary canned food `210` first). Full **300/300** graphical UI matrix across six resolutions and three languages + 12/12 specialization/reserve checks + 16/16 tactical checks + 13/13 co-op checks. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.2.0.md).

**Limits:** a live two-player host + client session (human NPC visibility and live two-player sync) and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Специализации напарников (`F8 → Работа`):** назначайте готовые профили **Боец**, **Медик**, **Снабженец** или **Сборщик** (или оставьте **Свой**). Профиль задаёт боевую доктрину, рабочую роль, предпочтительное оружие, переключатели поддержки и неснижаемый резерв припасов.
- **Предпросмотр профиля перед применением ко всему отряду:** нажмите **Профиль отряда** во вкладке **F8 → Работа**, чтобы увидеть точное число изменений по отряду (роль, снаряжение, доктрина, резерв БК/еды/медицины) до нажатия **Применить отряду**.
- **Предпочтительное оружие и неснижаемый резерв в рюкзаке:** выбирайте **Авто-оруж.**, **Стрелок** или **Ближний** и задавайте неснижаемый запас (`0/0/0`, `24/2/1`, `45/3/2`, `60/4/4` — патроны / еда / медицина). Сортировка склада и авто-выгрузка никогда не забирают экипированное оружие и не опустошают рюкзак напарника ниже его резерва.
- **Раздельные разрешения на редкие припасы (`F8 → Настройки → Поддержка отряда`):**
  - **Тратить редкие блюда** (выкл. по умолчанию — бережёт стейки и редкую кулинарию при раздаче пайка).
  - **Тратить сыворотки/дозы** (выкл. по умолчанию — бережёт редкие сыворотки, дозы и пакеты крови при обычном лечении; экстренная реанимация при падении работает всегда).
  - **Делиться редким БК** (вкл. по умолчанию — можно отключить, чтобы не раздавать редкие крупнокалиберные патроны).
- **Зоны сбора лута и приоритеты работ (`F8 → Работа`):**
  - Переключайте очередь задач базы (**Склад → Лут**, **Лут → Склад**, **Лагерь → Склад**).
  - Выбирайте радиус поиска (**14 м**, **24 м**, **36 м**, **48 м**) и закрепляйте точку сбора на месте (размеченные ящики базы всегда защищены).
- **Кооператив:** специализации, предпочтения оружия и резервы синхронизируются с проверкой хостом. Старые сохранения и конфиги совместимы.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.2.0`. Живая приёмка в настоящей игре на одноразовой копии профиля (`backups/live-20261112-154729`): стрельба, физический лут и переноска между ящиками с паузой 4 с (0 телепортаций), тактические приказы, живая защита неснижаемого резерва на настоящих `ItemData` и защита редкого стейка (`248`) при раздаче пайка. Полная графическая матрица **300/300** на шести разрешениях и трёх языках + 12/12 проверок специализаций и резерва + 16/16 проверок тактики + 13/13 правил кооператива. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.2.0.md).

**Ограничения:** настоящая сессия вдвоём (хост + клиент), включая видимость людей-NPC и синхронизацию вдвоём, а также автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
