# Smart Companions v2.6.0 — Autonomous survival: bio-masks, debuff remedies & sentry sleep shifts

## English

### Changes

- **Automatic bio-protection masks (`GOAPAction_EquipFaceMask` + everyday mask preservation):** during blood rain, toxic/spore fog or elevated radiation (`radiationDose > 3.5`), companions automatically equip a protective respirator/gas mask from their pack and sync visuals via `RefreshWornFaceMaskSprites` + `HostSendEquipment`. Their previous everyday bandana/mask is registered in `SavedEverydayMaskByGuid` so base sorting **never** strips it into storage, and it is re-equipped once the air clears.
- **Debuff & environmental remedies (`GOAPAction_ClearDebuff_*`):** companions treat burning, fever, hypothermia and spore buildup while strictly respecting your `v2.2.0` **Spend rare serums** toggle (using ordinary bandages, painkillers, fever reducers and iodine before ever touching rare serums, and only when life-threatening + permitted).
- **Sentry-guarded camp sleep rotation (`GOAPAction_Sleep`):** outside combat, exhausted companions (`fatigue < 28%`) physically walk (`<= 2.45 m`, zero teleportation) to an unoccupied camp bed (`TerrainObject_Furniture_Bed`), while the sentry coordinator always keeps **at least one awake watchman** on guard and wakes sleepers immediately if combat starts.
- **Settings toggle (`F8 → Settings → Squad support`):** enable or disable autonomous bio-masks, debuff care & camp sleep shifts at any time.

### Verification and limitations

Build: 0 warnings/errors, 57 Harmony patches, `[SelfVerify] PASS v2.6.0`. Live in-engine acceptance: native ranged combat, physical loot & chest carry (0 teleports), tactical orders, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover, `GrenadeSafetyGate`, and live `CompanionSurvivalSystem` bio-mask + sentry-sleep verification. Full **300/300** graphical UI matrix across six resolutions and three languages + `SurvivalRegression` + `FieldTacticsRegression` + `GarrisonRegression` + `ScaleAndPrivacyRegression` + `SpecializationRegression` + `TacticalRegression` + `ProtocolRegression`. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.6.0.md).

**Limits:** a live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Изменения

- **Авто-биозащита и сохранение обычной маски (`GOAPAction_EquipFaceMask`):** при кровавом дожде, споровом тумане или росте радиации напарники сами надевают противогаз/респиратор из рюкзака с синхронизацией визуала. Снятая повседневная маска фиксируется в защищённом списке, чтобы сортировка базы не выложила её в ящик, и возвращается на чистое небо.
- **Купирование опасных состояний (`GOAPAction_ClearDebuff_*`):** автоматическая помощь при горении, лихорадке, переохлаждении и спорах со строгим соблюдением запрета на редкие сыворотки из `v2.2.0` (сначала обычные таблетки и перевязка).
- **Посменный сон в лагере с часовым (`GOAPAction_Sleep`):** уставшие бойцы (`бодрость < 28%`) вне боя физически подходят (`<= 2.45 м`) к свободной кровати (`TerrainObject_Furniture_Bed`), но координатор вахты всегда оставляет минимум одного бодрствующего часового и мгновенно поднимает спящих при тревоге.
- **Переключатель в `F8 → Настройки → Поддержка отряда`:** позволяет в любой момент включать и отключать автономную биозащиту и посменный сон.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 57 Harmony-патчей, `[SelfVerify] PASS v2.6.0`. Живая приёмка в настоящей игре: стрельба, физический лут и переноска (0 телепортаций), тактика, резервы, склад на 80 ящиков, гарнизон, безопасный отпуск, `GrenadeSafetyGate`, биозащита и вахта сна. Полная графическая матрица **300/300** на шести разрешениях и трёх языках + пакет детерминированных проверок `SurvivalRegression`. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-2.6.0.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
