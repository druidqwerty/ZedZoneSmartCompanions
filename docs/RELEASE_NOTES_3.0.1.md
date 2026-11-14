# Smart Companions v3.0.1 — Hotfix: native team HUD cleanup on dismissal & corridor/doorway navigation

## English

### Bug fixes

- **Fixed dismissed companions remaining in the native top-left team UI (`TeamNPCPanel`):**
  - Removed the premature `OnCompanionDismissed` call in `GOAPNPCData.ExecuteNPCCommand_Prefix` that cleared `leaderCharacterGUID` before native `OneTime.QuitTeam` ran (which prevented ZED ZONE's native `QuitTeam` handler from removing the companion's card in `TeamNPCPanel`).
  - `SafeDismiss` and `OnCompanionDismissed` now execute native `OneTime.QuitTeam` while `leaderCharacterGUID` is still bound, clear `GOAPAgent.SetLeader(null)` / `DisableWithLeaderGoals()` / `GOAPTeamManager.RemoveTeamMember`, explicitly purge `TeamNPCPanel.instance.RemoveNPC` + `npcUIDic` + `RefreshHeaderCount`, unbind `TeamNPCOverheadUI`, and call `player.characterData.RefreshTeamData()` + `SyncTeamUI()`.
- **Fixed companions repeatedly bumping into walls in narrow corridors and doorways (`CorridorNavigationHelper`):**
  - **Root cause #1 (`GOAPAgent.MoveTo` `0.10 m` thin ray vs `0.36 m` body collider):** native `GOAPAgent.MoveTo` checked `IsPathClear(..., 0.10f)` (`10 cm`), causing doorway frames and corridor corners to pass the ray check and skip A* `MoveTo_PathFinding` in favor of straight-line `MoveTo_Direct` into the wall. Widened `IsPathClear`'s internal `0.10f` check to `0.36 m` shoulder clearance on player teammates so `MoveTo` automatically follows A* waypoints around door jambs and bends.
  - **Root cause #2 (lateral formation & ally-separation slots inside corridor walls):** in narrow passages (`IsNarrowCorridorAt`), wide formations (`Wedge`, `Line`, `Ring`) and ally separation now collapse cleanly into a single-file breadcrumb trail along the leader's walked path (`GetTrailSingleFileSlot` + `ClampToWalkableTarget`) instead of pushing slots sideways into walls.
  - **Root cause #3 (blind `MoveTo_Direct` combat kiting/strafing/fire-line steps):** kiting, strafing, wounded retreat and friendly-fire step-aside now probe 7 open angles (`SmartStepInClearDirection`) and hold ground to shoot cleanly when boxed in by hallway walls, plus active corner-unwedging after continuous wall stalls.

### Verification and limitations

Build: 0 warnings/errors, 58 Harmony patches, `[SelfVerify] PASS v3.0.1`. Live in-engine acceptance (`backups/live-20261114-030555`): ranged combat, supermarket indoor physical loot & chest-to-chest carry (0 teleports), Hold Position & Fall Back Here, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover + live verification that `TeamNPCPanel.npcUIDic` and `player.characterData.teamNPCGuids` contain **zero** stale entries after dismissal, and live `CorridorNavigationHelper` single-file trail checks. Full **300/300** graphical UI matrix across six resolutions and three languages + all 9 deterministic regression suites. All 8,386 real save files matched SHA-256. [Full verification](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.1.md).

**Limits:** a live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Required third-party Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

### Исправления

- **Исправлено сохранение уволенного напарника в нативном игровом меню/HUD отряда (`TeamNPCPanel`):**
  - Убран преждевременный сброс `leaderCharacterGUID` в префиксе `ExecuteNPCCommand_Prefix`, из-за которого нативный обработчик `OneTime.QuitTeam` не успевал удалить карточку бойца из левой верхней панели отряда.
  - При увольнении через кнопку мода **Отпустить** и через нативное меню теперь корректно вызывается `OneTime.QuitTeam` до очистки привязки, сбрасываются цели лидера (`SetLeader(null)`, `DisableWithLeaderGoals`, `GOAPTeamManager`), принудительно очищаются `TeamNPCPanel` (`RemoveNPC`, `npcUIDic`, `RefreshHeaderCount`) и `TeamNPCOverheadUI`, а также вызываются `RefreshTeamData()` и `SyncTeamUI()`.
- **Исправлено многократное утыкание напарников в стены в узких коридорах и дверных проёмах (`CorridorNavigationHelper`):**
  - **Первопричина №1 (тонкий луч `0.10 м` в нативном `GOAPAgent.MoveTo`):** игра проверяла прямую видимость лучом шириной `10 см`, из-за чего углы стен и дверные косяки считались «свободными», обход A* пропускался и боец шёл напролом в стену (`MoveTo_Direct`). Для напарников проверка расширена до реального габарита плеч `0.36 м` — теперь углы и двери всегда обходятся по сетке A*.
  - **Первопричина №2 (боковые точки строя внутри стен коридора):** в узких проходах широкие построения (`Клин`, `Шеренга`, `Круг`) и расталкивание союзников автоматически перестраиваются в колонну по одному по реальным пройденным точкам игрока (`LeaderTrail`), не ставя целевые точки внутрь стен.
  - **Первопричина №3 (слепой отскок `MoveTo_Direct` в бою и с линии огня):** при кайте, стрейфе, отходе и освобождении линии прицела в коридоре мод проверяет 7 открытых направлений и не толкает бойцов спиной или боком в глухую стену, а при зацепе за угол включает мягкое соскальзывание вдоль препятствия.

### Проверка и ограничения

Сборка: 0 предупреждений и ошибок, 58 Harmony-патчей, `[SelfVerify] PASS v3.0.1`. Живая приёмка в настоящей игре внутри здания супермаркета (`backups/live-20261114-030555`): стрельба, физический лут и переноска между стеллажами (0 телепортаций), гарнизон, безопасный отпуск с прямой проверкой отсутствия карточки в `TeamNPCPanel.npcUIDic` и навигация в коридорах + **300/300** графических случаев. Все 8 386 файлов сохранений совпали по SHA-256. [Полный отчёт](https://github.com/druidqwerty/ZedZoneSmartCompanions/blob/main/docs/verification-3.0.1.md).

**Ограничения:** настоящая сетевая сессия двух игроков на двух аккаунтах/ПК и автоматическая подача колеса мыши пока не проверены.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312) · [Обязательный сторонний Code Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
