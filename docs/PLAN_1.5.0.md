# ZED ZONE: Smart Companions — План обновления v1.5.0 и аудит комментариев Steam (2026-11-10)

## 1. Полный аудит комментариев и обсуждений Steam Workshop (`3836613312`)

Проверено анонимно 2026-11-10 (`https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312`):
- **Комментарии к предмету:** 8 шт. (`total_count=8`, страница 1 из 1).
- **Обсуждения (форум `40676593`):** 2 закреплённые темы (`Error Logs & Bug Reports` — 0 ответов; `Ideas & Suggestions` — **1 новый ответ от 2026-11-10**).

### Новые репорты и запросы игроков (за 2026-11-10):

1. **[КРИТИЧЕСКИЙ БАГ] Напарников невозможно уволить / исключить из отряда (`QuitTeam` / `UnFollowPlayer`)**
   - **Автор:** `橘子海` (`https://steamcommunity.com/profiles/76561199141172049`) — `10 Nov 2026 @ 5:32:12 am PST`
   - **Ссылка:** `https://steamcommunity.com/sharedfiles/filedetails/comments/3836613312#c571554312244867411`
   - **Текст:** `貌似会导致队友解雇不掉吗？我试了下左上不显示队友了，但还是没有离队`
   - **Перевод:** «Кажется, из-за мода напарников нельзя уволить? Я проверил: слева вверху напарник больше не отображается в списке отряда, но из команды он так и не ушёл (продолжает ходить за игроком)».
   - **Точная первопричина (доказано дизассемблированием `GameAssembly.dll` + кодом `MultiplayerSyncSystem_Npcs.cs`):**
     - В нативном `GOAPNPCData.ExecuteNPCCommand` (`RVA 0xFC85E0`, ветка `QuitTeam` на `0xFC8DC3..0xFC9121`) и `DialogueController.ExecuteDialogueResult` (`RVA 0x125DA20`, ветка `UnFollowPlayer` на `0x125DC37 -> 0xFB73D0` / `CharacterData.RemoveTeamNPC` `0xFB76E0`) игра очищает `npcData.leaderCharacterGUID = null` (`[rbx+0x60] = 0`), ставит `characterData.camp = 22` (`0x16`), удаляет NPC из `player.characterData.teamNPCGuids` / `TeamNPCPanel` (поэтому иконка слева вверху пропадает) и сбрасывает команды.
     - **Но в нашем моде (`MultiplayerSyncSystem_Npcs.cs:69-118`) метод `SyncCompanionOwnerAndLeader(GOAPAgent)` вызывается каждый тик из `TickHostNpcsAndRemoteLeaders()` и из Harmony-постфикса `GOAPAgent.UpdateLeaderState`:**
       ```csharp
       if (!string.IsNullOrEmpty(st.OwnerCharacterId) && string.IsNullOrEmpty(leaderId))
       {
           leaderId = st.OwnerCharacterId;
           npcData.leaderCharacterGUID = leaderId;
       }
       ...
       agent.leaderCharacter = leaderCtrl;
       agent.agentState.SetState("leaderCharacter", leaderCtrl);
       ```
     - Поскольку `st.OwnerCharacterId` остаётся заполненным после увольнения, на следующем же кадре `SyncCompanionOwnerAndLeader` видит пустой `npcData.leaderCharacterGUID` и **принудительно записывает туда старый `st.OwnerCharacterId` и возвращает `agent.leaderCharacter = player`!** В итоге из UI отряда NPC удалён, но физически остаётся привязан к игроку и не может уйти.

2. **[UX / UI БАГ] Панели мода перекрывают кнопку сортировки рюкзака и мини-карту + нельзя перетаскивать мышью**
   - **Автор:** `乄十年，一个轮回-/` — `10 Nov 2026 @ 6:08:37 am PST`
   - **Ссылка:** `https://steamcommunity.com/workshop/filedetails/discussion/3836613312/570428419877202938/#c570428419877327423` (тема `Ideas & Suggestions`)
   - **Текст:** `作者，mod的界面能不能增加个可拖拽，收缩起来后的窗口有部分挡住呢背包的整理按键，和部分小地图`
   - **Перевод:** «Автор, можно ли сделать интерфейс мода перетаскиваемым мышью? Даже в свёрнутом виде окно частично перекрывает кнопку сортировки рюкзака и часть мини-карты».
   - **Точная первопричина (`SmartCompanionsUI.cs` & `SmartCompanionsUI_Runtime.cs`):**
     - Мини-панель `DrawHudBadge()` жёстко прибита к правому верхнему углу (`x = Screen.width - 326f, y = 12f, w = 310f, h = 34f`) — прямо поверх нативной мини-карты (`MinimapPanel`)!
     - Панель категорий открытого ящика `DrawOpenContainerCategoryPanel()` жёстко прибита по центру сверху (`panelX = (Screen.width - panelW) * 0.5f, panelY = 46f`), а в свёрнутом виде (`CollapseChestBar = true`) рисует широкую плашку `380x32` в той же точке `(panelX, 46f)`, которая перекрывает верхнюю шапку инвентаря игрока и кнопку сортировки рюкзака.
     - Ни одно из 3 окон (`MainHubWindow [F8]`, `ChestCategoryBar`, `HudBadge`) не поддерживает перетаскивание мышью (drag-and-drop), а настройка `CompanionConfigStore.ShowHudBadge` даже не выведена кнопкой во вкладку настроек `F8`.

3. **[ЗАПРОС ФУНКЦИИ + СОВМЕСТИМОСТЬ] Увеличение лимита напарников и совместимость с `AVO Expanded — Advanced Squad Control` (`3836458339`)**
   - **Автор:** `天哪，那是上市的` (`https://steamcommunity.com/id/52152152113703350`) — `10 Nov 2026 @ 5:22:16 am` и `5:22:56 am PST`
   - **Ссылки:**
     - `https://steamcommunity.com/sharedfiles/filedetails/comments/3836613312#c571554312244866750` (`可以提升队友上限吗` — «Можно ли повысить лимит напарников?»)
     - `https://steamcommunity.com/sharedfiles/filedetails/comments/3836613312#c571554312244866789` (`哦对了，这个和AVO Expanded — Advanced Squad Control兼容吗` — «О, кстати, совместим ли мод с AVO Expanded — Advanced Squad Control?»)
   - **Инженерный анализ (`GameAssembly.dll` + Workshop `3836458339`):**
     - В `CharacterData.AddTeamNPC` (`RVA 0xFA83E0`, адрес `0xFA851C..0xFA8547`) игра проверяет статическое свойство `CharacterData.maxTeamNPCNumber` (`cmp ebx, dword ptr [rax]`).
     - Мод `AVO Expanded — Advanced Squad Control` (`3836458339`) использует клавишу **`F6`** для открытия своего меню настроек, **`F1–F4`** для быстрых команд и **`MMB` (среднюю кнопку мыши)** для отправки отряда в точку (`GOAPAction_TeamOrder_MoveTo`).
     - **Конфликт:** в `SmartCompanions` клавиша **`F6`** сейчас вызывает `CommandShareRationWithSquad` (съедает еду из инвентаря игрока при каждом открытии меню AVO!).

4. **[ЗАПРОС ФУНКЦИИ / ИСПРАВЛЕНИЕ ВАНИЛЬНОГО ИИ] Самостоятельный выход напарников на сбор припасов (`CollectResource` / Автономный поиск лута)**
   - **Автор:** `乄十年，一个轮回-/` (`comment_571554312244791186`)
   - **Текст:** `能指令队友独自外出收集物资吗？游戏本身的队友指令感觉没效果，我想让队友去采集物质但还是呆呆的跟着我` («Можно ли приказать напарникам самостоятельно выходить за припасами? Ванильная команда сбора ресурсов не даёт эффекта — хочу отправить их собирать ресурсы, а они тупо ходят за мной»).
   - **Точная первопричина в нативном коде (`GameAssembly.dll`):**
     1. В `GOAPGoal_FollowLeader.IsValid` (`RVA 0x8B30B0`, `0x8B3251`) цель `FollowLeader` остаётся активной (`return true`) даже когда `basicCommand == "CollectResource"` или `"CollectSpecifiedItem"`.
     2. В `GOAPGoal_FollowLeader.GetPriority` (`RVA 0x8B2D90`, `0x8B2F12`) как только напарник отходит от игрока дальше **`8.0 м`**, приоритет `FollowLeader` растёт по формуле `2.0 + (dist - 8.0) * 10.0`. Уже на дистанции **`12.0 м`** приоритет `FollowLeader` становится `42.0`, перебивая приоритет `GOAPGoal_LootResource` (`40.0` на `0x8B361C`)! Из-за этого напарник отходит на 10 метров к ящику, `FollowLeader` перебивает цель сбора ресурсов и тянет его обратно к игроку!
     3. Кроме того, в `GOAPAction_SearchForResources.IsExecutable` (`RVA 0x89FC50`, `0x89FDD9`) вызывается `GOAPAgentState.IsTeammateScavengeRestricted()`, который блокирует обыск не открытых игроком контейнеров (`TeammateLootPolicy.PlayerSearchedOnly`).

---

## 2. Детальный план разработки v1.5.0

### Блок 1. Полное исправление увольнения напарников (`QuitTeam` / `RemoveTeamNPC` / `UnFollowPlayer`)
1. В `CompanionRuntimeState` добавить флаг `public bool IsDismissedOrNotInTeam = false;`.
2. В `HarmonyPatches.cs` добавить перехваты:
   - `Postfix` на `CharacterData.RemoveTeamNPC(GOAPNPCData npcData)`:
     - Если `npcData != null`, вызвать `MultiplayerSyncSystem.OnCompanionDismissed(npcData)`.
   - `Postfix` на `CharacterData.AddTeamNPC(GOAPNPCData npcData)`:
     - Если `npcData != null`, вызвать `MultiplayerSyncSystem.OnCompanionRecruited( __instance, npcData)`.
   - В `GOAPNPCData.ExecuteNPCCommand`: если `commandStr` заканчивается на `"QuitTeam"`, перед/после нативного вызова гарантированно сбрасывать активную сортировку (`BaseSortingSystem.FlushCarryBufferSafely(st)`), очищать `st.IsSortingActive = false`, `st.SortingPhase = 0`, `st.OwnerCharacterId = ""`, `st.OwnerPlayerName = ""`, `st.IsDismissedOrNotInTeam = true`, а также на самом `agent` сбрасывать `agent.leaderCharacter = null` и `agent.agentState?.SetState("hasLeader", false)` / `agent.agentState?.SetState("leaderCharacter", null)`.
3. В `MultiplayerSyncSystem.SyncCompanionOwnerAndLeader(GOAPAgent agent)`:
   - **Никогда** не восстанавливать пустой `npcData.leaderCharacterGUID` из `st.OwnerCharacterId`, если:
     - `st.IsDismissedOrNotInTeam == true`, ИЛИ
     - в одиночной игре / на Хосте `npcData.npcGUID` отсутствует в `GOAPNPCManager.instance.teammateNPCs` и отсутствует в `leaderCtrl.characterData.teamNPCGuids`!
   - Если `string.IsNullOrEmpty(npcData.leaderCharacterGUID)` на Хосте/в соло, наоборот очищать `st.OwnerCharacterId = ""` и `st.OwnerPlayerName = ""`, чтобы уволенный напарник мгновенно и навсегда покидал отряд (до повторного найма).

### Блок 2. Перетаскиваемый UI (Drag-and-Drop), защита мини-карты и кнопки сортировки рюкзака
1. **Поддержка перетаскивания мышью (`DragWindowHeader`) для всех 3 окон мода:**
   - **Главное окно `F8` (`_mainWinRect`)**: перетаскивается за верхнюю строку заголовка.
   - **Панель категорий открытого контейнера (`_chestBarPos`, развёрнутая и свёрнутая)**:
     - Добавить слева в шапке и на свёрнутой плашке ручку перетаскивания `::` / `[Перетащить | Drag | 拖拽]`.
     - Изменить дефолтную позицию по умолчанию так, чтобы она находилась **внизу по центру (`y = Screen.height - panelH - 92f`)** (или в безопасной зоне, не перекрывающей верхнюю шапку рюкзака и кнопку «Сортировать» в `LootInventoryPanel`).
     - Сделать свёрнутый режим компактной плашкой (`240x28 px` вместо `380x32 px`), также свободно перетаскиваемой мышью.
   - **Мини-панель отряда (`DrawHudBadge`)**:
     - Сместить позицию по умолчанию из правого верхнего угла (где находится `MinimapPanel`) в **верхнюю левую-центральную зону (`x = Mathf.Max(260f, Screen.width * 0.28f), y = 10f`)**, чтобы она не перекрывала ни карточки отряда слева, ни мини-карту справа.
     - Сделать её перетаскиваемой мышью, добавить кнопку `[-]` для сворачивания в мини-кнопку `[F8]` (`54x26 px`) и кнопку `[x]` для скрытия.
2. **Сохранение позиций и сброс в `F8`:**
   - Сохранять `MainWinX/Y`, `ChestBarX/Y`, `HudBadgeX/Y`, `HudBadgeCollapsed`, `ShowHudBadge` в `CompanionConfigStore` (`config.json`) с автоматическим `Mathf.Clamp` к текущему разрешению экрана.
   - Во вкладке 3 (`Настройки`) добавить кнопку **«Отображать мини-панель на экране + Сбросить позиции окон UI»** (`Show Mini-HUD & Reset UI Positions` / `显示顶部快捷栏 / 重置窗口位置`).
3. **Точный `IsPointerOverOverlay` в `SmartCompanionsUI_Runtime.cs`:**
   - Проверять актуальные динамические прямоугольники `_mainWinRect`, `_currentHudBadgeRect`, `_currentChestBarRect` (плюс удерживать блокировку во время перетаскивания окна `_activeWindowDragId != 0`), чтобы клики и перетаскивание любого окна в любом месте экрана никогда не вызывали выстрел/удар в мире.

### Блок 3. Настраиваемый лимит отряда (`MaxTeamSize` до 15) и полная совместимость с `AVO Expanded`
1. **Настройка лимита напарников (`CompanionConfigStore.MaxTeamSize`):**
   - Добавить параметр `MaxTeamSize` (`0 = По умолчанию / Совместимость (3)`, либо `4`, `6`, `8`, `10`, `12`, `15`).
   - Добавить переключатель лимита отряда в шапку вкладки 1 (`Напарники`: кнопка `Лимит отряда: Ваниль / 5 / 8 / 12 / 15`) и во вкладку 3 (`Настройки`).
   - Когда `MaxTeamSize > 0`, применять `CharacterData.maxTeamNPCNumber = MaxTeamSize` при загрузке, смене настройки и в `TickHostNpcsAndRemoteLeaders()`. Когда `MaxTeamSize == 0`, не трогать `CharacterData.maxTeamNPCNumber`, позволяя `AVO Expanded` или ванили управлять лимитом.
2. **Авто-совместимость с `AVO Expanded — Advanced Squad Control` (`3836458339`):**
   - Определять наличие `AVO Expanded` (по загруженным плагинам/сборкам `AVO*` / `AdvancedSquadControl*`) или через настройку хоткеев:
   - Убрать безусловный перехват одиночного нажатия `F6` (которое в `AVO Expanded` открывает меню мода): сделать хоткей раздачи пайка `Alt + F6` (или `F6` только если `AVO Expanded` не загружен и не зажат модификатор, плюс добавить выбор в настройках), чтобы открытие меню `AVO Expanded` по `F6` никогда не тратило еду игрока.
   - Убедиться, что во время выполнения нативного `GOAPAction_TeamOrder_MoveTo` (отправка отряда по СКМ / MMB из `AVO Expanded`) наш `SmartCombatAI` и `BaseSortingSystem` не сбивают приказ на движение к точке.

### Блок 4. Автономный сбор припасов и починка команды «Поиск ресурсов» (`CollectResource` / `Autonomous Scavenge`)
1. **Починка нативного `Basic.CollectResource` и `Basic.CollectSpecifiedItem`:**
   - В `HarmonyPatches.cs` добавить патч на `GOAPGoal_FollowLeader.GetPriority` (`Postfix`):
     - Если у напарника `npcData.basicCommand == "CollectResource"` или `"CollectSpecifiedItem"` (или активен режим автономного сбора лута `st.IsScavengingActive`), и игрок не находится в бою (`! agent.IsThreatened` / нет врагов ближе 15 м), удерживать приоритет `FollowLeader` низким (`1.5f`) вплоть до дистанции **`42 м`** от игрока (вместо ванильных `8 м`, где `(dist - 8) * 10` перебивало `LootResource = 40.0`)!
   - В `HarmonyPatches.cs` добавить патч на `GOAPAgentState.IsTeammateScavengeRestricted` (`Postfix`):
     - Если `basicCommand == "CollectResource"` или `"CollectSpecifiedItem"` или у напарника включён `AllowAutoLootNearby`, возвращать `__result = false`, чтобы напарники могли самостоятельно обыскивать не открытые игроком ящики/полки в рейдах.
   - В `HarmonyPatches.cs` добавить защиту в `GOAPAgentState.IsContainerSearchableByNPC` (`Postfix`):
     - Если контейнер отмечен игроком как ящик базы (`rule.IsMarkedByPlayer`), возвращать `__result = false`, чтобы напарники в режиме `CollectResource` **никогда не разграбляли собственные ящики базы игрока**!
2. **Физический рейд по сбору лута вокруг (`CommandSweepNearbyLoot` + Авто-обыск неразграбленных полок/трупов):**
   - Расширить `AutoLootSystem`: помимо предметов на земле и трупов зомби, при приказе «Сбор трофеев / Вылазка за лутом» (и в режиме `CollectResource`) напарники могут собирать предметы из **неотмеченных** диких контейнеров поблизости (в радиусе до `25 м`, только если контейнер НЕ отмечен `IsMarkedByPlayer` и находится вне радиуса базовых ящиков игрока), физически подходя к ним и складывая добычу в свой рюкзак/общий инвентарь.

### Блок 5. Стабильность при переноске предметов по базе (обход препятствий и очистка кэша указателей)
1. В `BaseSortingSystem.cs` добавить регулярную очистку мёртвых `IntPtr` из `KnownInvToTerrainObj`, `KnownInvToWorldPos`, `KnownInvToStableId`, `KnownStableIdToInv` при смене чанков/перезагрузке сцены.
2. В `BaseSortingSystem_Worker.cs` (`TickActiveSortingMovementEveryFrame` и `FollowLeader_Execute_Prefix`): если напарник при переноске предметов между ящиками базы упирается в стену/мебель (`ag.isStuck` или луч блокируется стеной), переключаться с `MoveTo_Direct` на полноценный `ag.MoveTo(st.TargetContainerPos, 1.4f, true)` (с обходом препятствий по сетке навигации).

---

## 3. Требования к верификации и релизу v1.5.0
1. Сборка `dotnet build src/SmartCompanions/SmartCompanions.csproj -c Release --warnaserror` — `0` предупреждений, `0` ошибок.
2. Обновить `PluginVersion = "1.5.0"`, `SmartCompanions.csproj` (`1.5.0`), `mod.json` (`1.5.0`).
3. Живая проверка в движке игры (`ZED ZONE` с бекапом сохранений):
   - Автотест 1: Наём напарника -> увольнение (`QuitTeam` и `RemoveTeamNPC`) -> проверка через 2+ тика `MultiplayerSyncSystem.TickHostNpcsAndRemoteLeaders` и `UpdateLeaderState`, что `npcData.leaderCharacterGUID` остаётся пустым, `agent.leaderCharacter == null`, и напарник действительно покинул отряд!
   - Автотест 2: Перетаскивание всех 3 окон UI (`F8`, `ChestCategoryBar`, `HudBadge`), проверка дефолтных позиций (не перекрывают мини-карту и кнопку сортировки рюкзака), проверка `IsPointerOverOverlay` в новых координатах.
   - Автотест 3: Проверка `MaxTeamSize` (`CharacterData.maxTeamNPCNumber` меняется на `8`/`15` и сбрасывается в `0` = совместимость с AVO).
   - Автотест 4: Проверка `CollectResource` (приоритет `FollowLeader` на дистанции `20м` не перебивает `LootResource`, `IsTeammateScavengeRestricted == false`, но ящики базы `IsMarkedByPlayer` защищены от разграбления).
   - Автотест 5: Полный регрессионный прогон боя (Phases 0–3: зомби 8м/12м, StopFire, бандит 8м) и сортировки базы (`ItemCategorizer` + `CarryBuffer`).
4. Упаковка `dist/ZedZoneSmartCompanions-1.5.0.zip`, синхронизация в локальную папку игры `G:\Steam\steamapps\common\ZED ZONE\Mods\SmartCompanions\`, публикация релиза `v1.5.0` на GitHub, обновление описания и билда в Steam Workshop (`3836613312`, EN/RU/ZH), публикация поста `BOOSTY_1.5.0.md` на Boosty (`https://boosty.to/lrgpp`) с проверкой ссылки.
