using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class SmartCompanionsUI_Hub
    {
        // Partial helper methods bridged from SmartCompanionsUI
    }

    public static partial class SmartCompanionsUI
    {
        private static void SetLanguageMode(ModLanguage mode)
        {
            if (CompanionConfigStore.LanguageMode != mode)
            {
                CompanionConfigStore.LanguageMode = mode;
                InvalidateStyles();
                CompanionConfigStore.Save();
            }
        }

        private static void DrawMainHubWindow()
        {
            var lang = CompanionConfigStore.EffectiveLanguage;
            var r = _mainWinRect;
            // Tooltips are filled by the control under the mouse during this draw; never keep a stale one.
            BeginTooltipFrame();
            try { GUI.tooltip = string.Empty; } catch { }
            GUI.Box(r, GUIContent.none, _winStyle);

            float x = r.x + 14f;
            float y = r.y + 10f;
            float w = r.width - 28f;

            // Keep the title separate from controls; the whole free header is draggable.
            var titleDragRect = new Rect(x, y, w - 348f, 32f);
            string recTag = EventLogRecorder.IsEnabled ? "  [REC]" : "";
            GUI.Label(titleDragRect,
                CompanionConfigStore.L(lang, "УМНЫЕ НАПАРНИКИ", "SMART COMPANIONS", "智能队友") + recTag, _headerStyle);
            var winPos = new Vector2(_mainWinRect.x, _mainWinRect.y);
            HandleWindowDrag(1, titleDragRect, ref winPos, new Vector2(_mainWinRect.width, _mainWinRect.height), p =>
            {
                _mainWinRect.x = p.x;
                _mainWinRect.y = p.y;
                CompanionConfigStore.MainWinPosX = p.x;
                CompanionConfigStore.MainWinPosY = p.y;
            });

            if (GUI.Button(new Rect(r.xMax - 344f, y, 38f, 28f),
                UiTip("?", CompanionConfigStore.L(lang,
                    "Краткая памятка по моду",
                    "Quick Mod Guide",
                    "模组快速指南")),
                CompanionConfigStore.ShowQuickGuide ? _catOnStyle : _btnStyle))
            {
                CompanionConfigStore.ShowQuickGuide = !CompanionConfigStore.ShowQuickGuide;
            }

            float lx = r.xMax - 300f;
            var languages = new[] { ModLanguage.Auto, ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese };
            var languageLabels = new[] { "Auto", "RU", "EN", "中文" };
            for (int i = 0; i < languages.Length; i++)
                if (GUI.Button(new Rect(lx + i * 53f, y, 49f, 28f), languageLabels[i],
                    CompanionConfigStore.LanguageMode == languages[i] ? _catOnStyle : _btnStyle))
                    SetLanguageMode(languages[i]);
            if (GUI.Button(new Rect(r.xMax - 82f, y, 68f, 28f), "Esc / F8", _btnStyle))
            {
                ShowMainWindow = false;
                return;
            }

            y += 36f;
            // Tab indices stay stable (0 squad, 1 base, 2 chatter, 3 settings, 4 work); display order differs.
            var tabOrder = new[] { 0, 4, 1, 2, 3 };
            var tabs = new[]
            {
                CompanionConfigStore.L(lang, "01  ОТРЯД", "01  SQUAD", "01  队伍"),
                CompanionConfigStore.L(lang, "02  РАБОТА", "02  WORK", "02  工作"),
                CompanionConfigStore.L(lang, "03  БАЗА", "03  BASE", "03  基地"),
                CompanionConfigStore.L(lang, "04  РЕПЛИКИ", "04  CHATTER", "04  对话"),
                CompanionConfigStore.L(lang, "05  НАСТРОЙКИ", "05  SETTINGS", "05  设置")
            };
            float tabW = (w - 8f * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (GUI.Button(new Rect(x + i * (tabW + 8f), y, tabW, 34f), tabs[i],
                    !CompanionConfigStore.ShowQuickGuide && _activeTab == tabOrder[i] ? _catOnStyle : _btnStyle))
                {
                    CompanionConfigStore.ShowQuickGuide = false;
                    _activeTab = tabOrder[i];
                    if (_activeTab == 1) BaseSortingSystem.ScanNearbyContainers(force: true);
                }
            y += 44f;
            var contentRect = new Rect(x, y, w, r.height - (y - r.y) - 36f);
            GUI.Label(new Rect(x, r.yMax - 29f, w, 20f), CompanionConfigStore.L(lang,
                "F8 / Esc — закрыть   ·   Колесо мыши — прокрутка   ·   Перетащите заголовок, чтобы переместить окно",
                "F8 / Esc — close   ·   Mouse wheel — scroll   ·   Drag the title to move this window",
                "F8 / Esc 关闭   ·   鼠标滚轮滚动   ·   拖动标题可移动窗口"), _labelStyle);

            try
            {
                if (CompanionConfigStore.ShowQuickGuide) DrawQuickGuideCard(contentRect, lang);
                else if (_activeTab == 0) DrawTabSquad(contentRect, lang);
                else if (_activeTab == 1) DrawTabBaseBoxes(contentRect, lang);
                else if (_activeTab == 2) DrawTabChatter(contentRect, lang);
                else if (_activeTab == 4) DrawTabWork(contentRect, lang);
                else DrawTabSettings(contentRect, lang);
            }
            catch (Exception error)
            {
                ReportUiFailure("Tab " + _activeTab, error);
                DrawUiFailure(contentRect, lang);
            }

            DrawHoverTooltip(r);

            if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) &&
                r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }
        }

        private static void DrawQuickGuideCard(Rect area, ModLanguage lang)
        {
            GUI.Box(area, GUIContent.none, _boxStyle);
            float x = area.x + 16f;
            float y = area.y + 12f;
            float w = area.width - 32f;

            GUI.Label(new Rect(x, y, w, 30f), CompanionConfigStore.L(lang,
                "Краткая памятка по моду",
                "Quick Mod Guide",
                "模组快速指南"), _headerStyle);
            y += 38f;

            var steps = new[]
            {
                (
                    CompanionConfigStore.L(lang,
                        "1. Отряд и боевые группы [--] / [A] / [B]  ·  Alt+1 / Alt+2 / Alt+3",
                        "1. Squad & Fireteams [--] / [A] / [B]  ·  Alt+1 / Alt+2 / Alt+3",
                        "1. 队伍与火力组 [--] / [A] / [B]  ·  Alt+1 / Alt+2 / Alt+3"),
                    CompanionConfigStore.L(lang,
                        "На карточке напарника назначьте группу [A] (Альфа) или [B] (Браво). Клавиши Alt+1 (Все), Alt+2 (Альфа), Alt+3 (Браво) переключают, кто принимает команды.",
                        "Click [--]/[A]/[B] on a companion card to assign Alpha or Bravo. Press Alt+1 (All), Alt+2 (Alpha), or Alt+3 (Bravo) to target squad commands.",
                        "在队友卡片点击 [--]/[A]/[B] 分配 A 组或 B 组。按 Alt+1（全队）、Alt+2（A组）、Alt+3（B组）切换接收指令的队伍频道。")
                ),
                (
                    CompanionConfigStore.L(lang,
                        "2. Гарнизон лагеря, выживание и огород",
                        "2. Camp Garrison, Survival & Crops",
                        "2. 营地驻守、生存防护与农田作物"),
                    CompanionConfigStore.L(lang,
                        "Кнопка «Гарнизон» оставляет бойца охранять базу, спать по сменам, надевать противогаз в угрозах и вести хозяйство (в т.ч. автосбор урожая во вкладке «Работа»).",
                        "Toggle Garrison to keep a companion guarding base, taking sleep shifts, auto-equipping gas masks in hazards, and tending chores (including crop harvest in Work tab).",
                        "开启「营地驻守」可让队友留守基地巡逻、轮班睡眠、在毒区自动佩戴防毒面具并照料内务（可在「工作」页开关自动收获成熟作物）。")
                ),
                (
                    CompanionConfigStore.L(lang,
                        "3. Разметка ящиков базы, Приёмник и F7 / Alt+F",
                        "3. Marked Base Chests, [Drop-off] & F7 / Alt+F",
                        "3. 标记基地箱子、[中转箱] 与 F7 / Alt+F"),
                    CompanionConfigStore.L(lang,
                        "Отметьте категории на ящиках базы (или при открытии ящика). Пометьте один ящик как «Приёмник» — напарники разнесут вещи оттуда. F7 — сортировка, Alt+F — сбор лута.",
                        "Tag categories on base chests (in Base tab or while looting). Mark one chest as [Drop-off] so companions distribute loot from it. F7 sorts base; Alt+F sweeps nearby loot.",
                        "在基地页或开箱时勾选物品分类。将一个箱子设为「中转箱」后队友会自动将战利品分拣入库。按 F7 整理基地，按 Alt+F 搜刮附近物资。")
                ),
                (
                    CompanionConfigStore.L(lang,
                        "4. Транспорт и переходы между этажами",
                        "4. Vehicles & Multi-Floor Navigation",
                        "4. 载具乘车与多层建筑跨层"),
                    CompanionConfigStore.L(lang,
                        "Садитесь в авто — походный отряд автоматически займёт свободные места (лишние подождут на месте). Напарники сами проходят по лестницам и люкам между этажами.",
                        "Board a vehicle and active squadmates take available passenger seats (overflow holds safely). Companions also use stairs and hatches to follow between floors.",
                        "驾驶载具时现役队友会自动上车入座（超员队友在原地安全待命）。队友也会自主使用楼梯和地下入口跟随你切换楼层。")
                ),
                (
                    CompanionConfigStore.L(lang,
                        "5. Тактика и бой: Alt+G / F9 / Alt+H",
                        "5. Tactical Hotkeys: Alt+G / F9 / Alt+H",
                        "5. 战术快捷键：Alt+G / F9 / Alt+H"),
                    CompanionConfigStore.L(lang,
                        "Alt+G — отправить отряд веером в точку курсора; F9 — «Боевой клич / Штурм»; Alt+H — лечебный дым и перевязка. В меню [F8] доступны формации и резервы.",
                        "Alt+G fans the squad to your cursor; F9 triggers Battle Cry; Alt+H deploys healing smoke / aid. Open [F8] anytime for formations, doctrines, and reserves.",
                        "Alt+G 令小队扇形前往鼠标点；F9 发动战吼；Alt+H 投掷治疗烟雾与包扎。随时按 [F8] 调整编队阵型、学说与物资保底。")
                )
            };

            const float rowH = 76f;
            const float gap = 8f;
            for (int i = 0; i < steps.Length; i++)
            {
                var rowRect = new Rect(x, y + i * (rowH + gap), w, rowH);
                GUI.Box(rowRect, GUIContent.none, _winStyle);
                GUI.Label(new Rect(rowRect.x + 12f, rowRect.y + 4f, rowRect.width - 24f, 22f), steps[i].Item1, _subHeaderStyle);
                GUI.Label(new Rect(rowRect.x + 12f, rowRect.y + 26f, rowRect.width - 24f, 46f), steps[i].Item2, _labelStyle);
            }

            if (GUI.Button(new Rect(area.x + (area.width - 240f) * 0.5f, area.yMax - 46f, 240f, 34f),
                CompanionConfigStore.L(lang, "Закрыть памятку", "Close Guide", "关闭指南"), _catOnStyle))
            {
                CompanionConfigStore.ShowQuickGuide = false;
            }
        }

        private static GOAPAgent PickBestFirstAidCompanion(List<GOAPAgent> teammates)
        {
            if (teammates == null || teammates.Count == 0) return null;
            var player = GameController.instance?.playerCharacter;
            bool hasPlayer = Il2CppSafe.IsAliveUnity(player);
            Vector2 playerPos = hasPlayer ? (Vector2)player.transform.position : Vector2.zero;

            GOAPAgent preferred = null;
            float preferredDistSq = float.MaxValue;
            GOAPAgent nonGarrisonFallback = null;
            float nonGarrisonDistSq = float.MaxValue;
            GOAPAgent anyFallback = null;
            float anyDistSq = float.MaxValue;

            for (int i = 0; i < teammates.Count; i++)
            {
                var ag = teammates[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) ||
                    !Il2CppSafe.IsAliveUnity(ag.humanCharacter) || ag.humanCharacter.deadFlag)
                    continue;
                if (MultiplayerSyncSystem.IsMultiplayerOnline && !MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                    continue;

                float distSq = hasPlayer ? ((Vector2)ag.humanCharacter.transform.position - playerPos).sqrMagnitude : i;
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, CompanionConfigStore.GetCharacterDisplayName(ag.npcData.characterData));
                bool notGarrison = st == null || st.RosterMode != CompanionRosterMode.CampGarrison;

                if (CompanionConfigStore.MatchesActiveCommandChannel(st) && notGarrison)
                {
                    if (distSq < preferredDistSq)
                    {
                        preferredDistSq = distSq;
                        preferred = ag;
                    }
                }
                else if (notGarrison)
                {
                    if (distSq < nonGarrisonDistSq)
                    {
                        nonGarrisonDistSq = distSq;
                        nonGarrisonFallback = ag;
                    }
                }

                if (distSq < anyDistSq)
                {
                    anyDistSq = distSq;
                    anyFallback = ag;
                }
            }

            return preferred ?? nonGarrisonFallback ?? anyFallback;
        }

        private static void DrawTabSquad(Rect area, ModLanguage lang)
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();

            // One calm summary row, followed by evenly sized direct orders.
            GUI.Box(new Rect(area.x, area.y, area.width, 80f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 6f;
            string limitStr = CompanionConfigStore.MaxTeamSize <= 0
                ? CompanionConfigStore.L(lang, "Авто (3/AVO)", "Auto (3/AVO)", "自动 (3/AVO)")
                : CompanionConfigStore.MaxTeamSize.ToString();
            string petSummary = ExpeditionSystems.GetActivePlayerPetsSummary(lang);
            GUI.Label(new Rect(bx + 8f, by, area.width - 425f, 28f), CompanionConfigStore.L(lang,
                $"Напарников рядом: {teammates.Count}  ·  {petSummary}",
                $"Companions nearby: {teammates.Count}  ·  {petSummary}",
                $"附近队友：{teammates.Count}  ·  {petSummary}"), _subHeaderStyle);
            if (GUI.Button(new Rect(area.xMax - 404f, by, 166f, 28f), UiTip(
                CompanionConfigStore.GetCommandChannelLabel(CompanionConfigStore.ActiveCommandGroup, lang),
                CompanionConfigStore.L(lang,
                    "Активный канал команд: Все [Alt+1] / Альфа [Alt+2] / Браво [Alt+3]",
                    "Active command channel: All [Alt+1] / Alpha [Alt+2] / Bravo [Alt+3]",
                    "当前指挥频道：全队 [Alt+1] / A组 [Alt+2] / B组 [Alt+3]")),
                CompanionConfigStore.ActiveCommandGroup != SquadFireteam.None ? _catOnStyle : _btnStyle))
            {
                CompanionConfigStore.ActiveCommandGroup = (SquadFireteam)(((int)CompanionConfigStore.ActiveCommandGroup + 1) % 3);
            }
            if (GUI.Button(new Rect(area.xMax - 232f, by, 224f, 28f), CompanionConfigStore.L(lang,
                $"Лимит: {limitStr}", $"Squad cap: {limitStr}", $"队伍上限：{limitStr}"), _btnStyle))
                CompanionConfigStore.CycleMaxTeamSize();
            by += 34f;
            float actionW = (area.width - 16f - 5f * 6f) / 6f;
            if (GUI.Button(new Rect(bx, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Сортировать [F7]", "Sort Base [F7]", "整理基地 [F7]"), _catOnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            if (GUI.Button(new Rect(bx + (actionW + 6f), by, actionW, 30f),
                CompanionConfigStore.L(lang, "Багажник", "Unload Car", "卸后备箱"), _btnStyle))
                BaseChoreSystem.CommandUnloadVehiclesToBase(null, silentIfEmpty: false);
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 2f, by, actionW, 30f), UiTip(
                CompanionConfigStore.L(lang, "Лут [Alt+F]", "Loot [Alt+F]", "搜刮 [Alt+F]"),
                CompanionConfigStore.L(lang, "Собрать лут вокруг отрядом [Alt+F]", "Sweep nearby loot with squad [Alt+F]", "命令小队搜刮附近物资 [Alt+F]")), _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, 24f);
            string rationKey = CompanionConfigStore.ShouldUseAltForRationHotkey() ? "Alt+F6" : "F6";
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 3f, by, actionW, 30f),
                CompanionConfigStore.L(lang, $"Паёк [{rationKey}]", $"Ration [{rationKey}]", $"分口粮 [{rationKey}]"), _btnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(null, fromAutoShare: false);
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 4f, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Штурм [F9]", "Rally [F9]", "战吼 [F9]"), _btnStyle))
                SmartCombatAI.CommandSquadBattleCry(null);
            var firstAidAgent = PickBestFirstAidCompanion(teammates);
            bool prevAidEnabled = GUI.enabled;
            GUI.enabled = prevAidEnabled && Il2CppSafe.IsAliveUnity(firstAidAgent);
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 5f, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Первая помощь", "First Aid", "急救"), _btnStyle) &&
                Il2CppSafe.IsAliveUnity(firstAidAgent))
            {
                CustomNpcCommands.ExecuteCustomCommand(firstAidAgent.npcData, CustomNpcCommands.EmergencyAid);
            }
            GUI.enabled = prevAidEnabled;

            float listY = area.y + 88f;
            float listH = area.height - 88f;

            DrawSquadTacticalBar(new Rect(area.x, listY, area.width, 28f), lang);
            listY += 34f;
            listH -= 34f;

            if (teammates.Count == 0)
            {
                GUI.Box(new Rect(area.x, listY, area.width, 90f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(area.x + 16f, listY + 16f, area.width - 32f, 60f),
                    CompanionConfigStore.L(lang,
                        "У вас сейчас нет активных напарников рядом. Наймите выжившего в лагере или по рации — здесь появятся его тактические доктрины, управление умным боем, приказ на сортировку базы и живые реакции!",
                        "No active companions nearby. Recruit a survivor in camp or via radio to manage their tactical doctrines, base sorting orders, and live reactions here!",
                        "当前附近没有已招募的队友。在营地或通过电台招募幸存者后，即可在此管理其战术学说、智能战斗 AI、基地箱子整理指令与实时互动吐槽！"),
                    _labelStyle);
                return;
            }

            var visible = new System.Collections.Generic.List<GOAPAgent>();
            foreach (var candidate in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(candidate) || !Il2CppSafe.IsAlive(candidate.npcData)) continue;
                CompanionConfigStore.CompanionStates.TryGetValue(candidate.npcData.npcGUID ?? "", out var candidateState);
                if (PassesSquadFilter(candidate, candidateState)) visible.Add(candidate);
            }
            DrawSquadFilterRow(new Rect(area.x, listY, area.width, 28f), lang, teammates.Count, visible.Count);
            listY += 34f;
            listH -= 34f;
            if (visible.Count == 0)
            {
                GUI.Box(new Rect(area.x, listY, area.width, 60f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(area.x + 16f, listY + 16f, area.width - 32f, 30f), CompanionConfigStore.L(lang,
                    "Под этот фильтр сейчас никто не подходит.", "Nobody matches this filter right now.", "当前没有符合此筛选的队友。"), _labelStyle);
                return;
            }
            teammates = visible;

            float cardH = 194f;
            var viewRect = new Rect(0, 0, area.width - 20f, teammates.Count * (cardH + 8f));
            BeginScrollArea(new Rect(area.x, listY, area.width, listH), ref _scrollSquad, viewRect, 1);
            bool baseGuiEnabled = GUI.enabled;
            try
            {

            for (int i = 0; i < teammates.Count; i++)
            {
                GUI.enabled = baseGuiEnabled;
                try
                {
                var ag = teammates[i];
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var cd = ag.npcData.characterData;
                var status = cd?.characterStatusData;
                string name = CompanionConfigStore.GetCharacterDisplayName(cd, $"NPC #{i + 1}");
                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, name);

                var cRect = new Rect(0, i * (cardH + 8f), viewRect.width, cardH);
                GUI.Box(cRect, GUIContent.none, _boxStyle);

                float cx = cRect.x + 10f;
                float cy = cRect.y + 6f;

                float hp = status != null ? status.hp : ag.npcData.health;
                float maxHp = status != null && status.maxhp > 0 ? status.maxhp : Mathf.Max(100f, ag.npcData.maxHealth);
                float hunger = status != null ? status.hunger : ag.npcData.hunger;
                float thirst = status != null ? status.thirst : ag.npcData.thirst;
                string wepName = cd?.characterEquipmentData?.currentWeaponItemData?.GetItemName()
                    ?? CompanionConfigStore.L(lang, "Без оружия", "Unarmed", "徒手");

                string rankTitle = CompanionConfigStore.GetRankDisplayName(st.Rank, lang);
                int nextXp = CompanionConfigStore.GetNextRankXpThreshold(st.Rank);
                string archTitle = CompanionConfigStore.GetArchetypeDisplayName(st.EffectiveArchetype, lang);
                string ownerBadge = "";
                bool canCommand = true;
                string controllerName = "";
                if (MultiplayerSyncSystem.IsMultiplayerOnline)
                {
                    bool isMine = MultiplayerSyncSystem.IsCompanionOwnedByLocalPlayer(ag.npcData);
                    string ownerLabel = !string.IsNullOrEmpty(st.OwnerPlayerName)
                        ? st.OwnerPlayerName
                        : CompanionConfigStore.L(lang, "Союзник", "Ally", "盟友");
                    ownerBadge = isMine
                        ? CompanionConfigStore.L(lang, " [Ваш]", " [Yours]", " [你的]")
                        : $" [{ownerLabel}]";
                    canCommand = isMine;
                    controllerName = ownerLabel;
                }

                float innerW = viewRect.width - 20f;
                GUI.Label(new Rect(cx, cy, canCommand ? innerW - 248f : innerW - 424f, 24f),
                    $"{name}{ownerBadge}  ·  {rankTitle}  ·  {st.ExperiencePoints}/{nextXp} XP", _subHeaderStyle);
                if (!canCommand)
                {
                    GUI.Label(new Rect(cx + innerW - 420f, cy, 172f, 24f), UiTip(
                        CompanionConfigStore.L(lang, "только просмотр", "view only", "仅查看"),
                        CompanionConfigStore.L(lang, $"Приказы этому напарнику отдаёт {controllerName}",
                            $"Only {controllerName} can give orders to this companion", $"只有 {controllerName} 能指挥这名队友")), _labelStyle);
                    GUI.enabled = false;
                }

                var prevBg = GUI.backgroundColor;
                bool fireteamClicked = false;
                try
                {
                    if (st.Fireteam == SquadFireteam.Alpha)
                        GUI.backgroundColor = new Color(1f, 0.72f, 0.52f);
                    else if (st.Fireteam == SquadFireteam.Bravo)
                        GUI.backgroundColor = new Color(0.55f, 0.90f, 1f);
                    fireteamClicked = GUI.Button(new Rect(cx + innerW - 242f, cy, 56f, 24f), UiTip(
                        "[" + CompanionConfigStore.GetFireteamShortTag(st.Fireteam, lang) + "]",
                        CompanionConfigStore.L(lang,
                            "Боевая группа: [--] / [A] Альфа / [B] Браво (Alt+1/2/3)",
                            "Fireteam group: [--] / [A] Alpha / [B] Bravo (Alt+1/2/3)",
                            "火力分组：[--] / [A] A组 / [B] B组 (Alt+1/2/3)")),
                        st.Fireteam != SquadFireteam.None ? _catOnStyle : _btnStyle);
                }
                finally
                {
                    GUI.backgroundColor = prevBg;
                }
                if (fireteamClicked)
                {
                    st.Fireteam = (SquadFireteam)(((int)st.Fireteam + 1) % 3);
                    CompanionConfigStore.Save();
                    MultiplayerSyncSystem.SyncCompanionStateChange(st);
                }

                if (GUI.Button(new Rect(cx + innerW - 180f, cy, 180f, 24f), archTitle, _btnStyle))
                {
                    st.Archetype = (CompanionArchetype)(((int)st.Archetype + 1) % 6);
                    CompanionConfigStore.Save();
                }
                cy += 26f;
                GUI.Label(new Rect(cx, cy, innerW * 0.40f, 28f),
                    $"HP {hp:F0}/{maxHp:F0}  ·  {wepName}", _labelStyle);
                var statusKind = GetStatusKind(ag, st);
                string statusDetail = statusKind == CompanionStatusKind.Blocked ? DescribeBlockReason(st, lang)
                    : statusKind == CompanionStatusKind.Combat ? CompanionConfigStore.L(lang, "Работа на паузе до конца боя", "Work paused until the fight ends", "工作暂停至战斗结束")
                    : statusKind == CompanionStatusKind.Wounded ? CompanionConfigStore.L(lang, $"Голод {hunger:F0} · Жажда {thirst:F0}", $"Food {hunger:F0} · Water {thirst:F0}", $"饱食 {hunger:F0} · 水分 {thirst:F0}")
                    : st.CurrentTaskText;
                var prevColor = GUI.color;
                GUI.color = GetStatusColor(statusKind);
                var statusContent = UiTip("● " + GetStatusLabel(statusKind, lang),
                    CompanionConfigStore.L(lang, "Текущее состояние напарника", "Current companion state", "队友当前状态"));
                float statusW = Mathf.Clamp(_labelStyle.CalcSize(statusContent).x + 8f, 90f, innerW * 0.30f);
                bool wrapWas = _labelStyle.wordWrap;
                _labelStyle.wordWrap = false;
                GUI.Label(new Rect(cx + innerW * 0.41f, cy, statusW, 28f), statusContent, _labelStyle);
                _labelStyle.wordWrap = wrapWas;
                GUI.color = prevColor;
                GUI.Label(new Rect(cx + innerW * 0.41f + statusW + 4f, cy, innerW * 0.59f - statusW - 4f, 28f),
                    UiTip(UiSafe($"{statusDetail}  |  K:{st.KillsThisSession} S:{st.SortedItemsThisSession} C:{st.ChoresDoneThisSession}"),
                        CompanionConfigStore.L(lang, "K — убийства, S — разложено предметов, C — бытовые дела за сессию",
                            "K — kills, S — items sorted, C — camp chores this session", "K—击杀，S—整理物品，C—本局完成的内务")), _labelStyle);

                // Row 2: Doctrines
                cy += 26f;
                GUI.Label(new Rect(cx, cy, 90f, 24f), CompanionConfigStore.L(lang, "Доктрина:", "Doctrine:", "战术学说:"), _labelStyle);
                DrawDoctrineButtons(new Rect(cx + 85f, cy, viewRect.width - 95f, 24f), ag, st, lang);

                // Row 3: Per-companion feature toggles
                cy += 30f;
                float tw = (viewRect.width - 20f - 20f) / 5f;
                if (GUI.Button(new Rect(cx, cy, tw, 24f),
                    UiTip((st.AllowFieldMedic ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Медик", "Field Medic", "战地医疗"),
                        CompanionConfigStore.L(lang, "Лечит раненых союзников своими аптечками", "Heals wounded allies with own medkits", "用自己的医疗包治疗受伤队友")),
                    st.AllowFieldMedic ? _catOnStyle : _catOffStyle))
                {
                    st.AllowFieldMedic = !st.AllowFieldMedic;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 1f, cy, tw, 24f),
                    UiTip((st.AllowAmmoShare ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Патроны", "Ammo Share", "弹药补给"),
                        CompanionConfigStore.L(lang, "Делится лишними патронами с отрядом", "Shares spare ammo with the squad", "与队伍分享多余弹药")),
                    st.AllowAmmoShare ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAmmoShare = !st.AllowAmmoShare;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 2f, cy, tw, 24f),
                    UiTip((st.AllowAutoLootNearby ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Авто-лут", "Auto-Loot", "自动拾取"),
                        CompanionConfigStore.L(lang, "Сам подходит и подбирает ценные вещи рядом вне боя", "Walks over and picks up valuables nearby outside combat", "非战斗时自动走过去拾取附近物资")),
                    st.AllowAutoLootNearby ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoLootNearby = !st.AllowAutoLootNearby;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 3f, cy, tw, 24f),
                    UiTip((st.AllowAutoDropoffAtBase ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Выгрузка", "Auto-Dropoff", "回城卸货"),
                        CompanionConfigStore.L(lang, "На базе относит собранное в подходящие ящики", "At base, carries collected items into matching chests", "回到基地后把收集的物品搬进对应箱子")),
                    st.AllowAutoDropoffAtBase ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoDropoffAtBase = !st.AllowAutoDropoffAtBase;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 4f, cy, tw, 24f),
                    UiTip((st.AllowBaseChores ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Быт/Костёр", "Camp Chores", "营地内务"),
                        CompanionConfigStore.L(lang, "Разжигает костёр, ухаживает за грядками и разгружает багажник", "Lights the fire, tends crops and unloads the trunk", "点火、照料作物并卸下后备箱")),
                    st.AllowBaseChores ? _catOnStyle : _catOffStyle))
                {
                    st.AllowBaseChores = !st.AllowBaseChores;
                    CompanionConfigStore.Save();
                }

                // Row 4: Direct Companion Orders, Downed Revival, Garrison & Two-Step Safe Dismiss (2.4.0 / 3.0.2)
                cy += 30f;
                bool isDowned = st.IsDownedInMp;
                float distToLocal = 999f;
                try
                {
                    if (Il2CppSafe.IsAliveUnity(ag.humanCharacter))
                    {
                        if (CharacterAidManager.IsDowned(ag.humanCharacter) || ag.humanCharacter.weaponHolsteredByDowned)
                            isDowned = true;
                        var localPl = GameController.instance?.playerCharacter;
                        if (Il2CppSafe.IsAliveUnity(localPl))
                            distToLocal = Vector2.Distance(localPl.transform.position, ag.humanCharacter.transform.position);
                    }
                }
                catch { }

                if (isDowned && distToLocal <= 4.6f)
                {
                    bool prevEn = GUI.enabled;
                    GUI.enabled = baseGuiEnabled;
                    try
                    {
                        if (GUI.Button(new Rect(cx, cy, innerW, 28f), UiTip(
                            CompanionConfigStore.L(lang, "[+] Поднять раненого напарника (рядом)", "[+] Revive Downed Companion (Nearby)", "[+] 扶起身边濒死队友"),
                            CompanionConfigStore.L(lang, "Поднять напарника с колен (работает и для хоста, и для клиента в коопе)",
                                "Bring kneeling companion back to their feet (works for Host and Co-op Clients)",
                                "扶起跪地濒死的队友（单机与联机主客机通用）")), _catOnStyle))
                        {
                            MultiplayerSyncSystem.RequestReviveCompanion(ag, st);
                        }
                    }
                    finally { GUI.enabled = prevEn; }
                }
                else if (CampGarrisonSystem.PendingDismissConfirmGuid == st.NpcGuid)
                {
                    float toggleW = innerW * 0.48f;
                    float confirmW = (innerW - toggleW - 12f) * 0.54f;
                    float cancelW = innerW - toggleW - confirmW - 12f;
                    if (GUI.Button(new Rect(cx, cy, toggleW, 28f), UiTip(
                        (CampGarrisonSystem.PendingDismissReturnGear ? "[x] " : "[ ] ") +
                        CompanionConfigStore.L(lang, "Сдать вещи в ящик/игроку", "Hand over gear first", "离队前交出全部装备"),
                        CompanionConfigStore.L(lang, "Физически подойдёт к ящику базы или к вам и выложит оружие, броню и рюкзак без телепортации",
                            "Walks to a marked chest or to you and hands over weapons, armor and pack (0 teleport)",
                            "走过去把武器、护甲和背包交进基地箱子或交给玩家（零瞬移）")),
                        CampGarrisonSystem.PendingDismissReturnGear ? _catOnStyle : _catOffStyle))
                    {
                        CampGarrisonSystem.PendingDismissReturnGear = !CampGarrisonSystem.PendingDismissReturnGear;
                    }
                    if (GUI.Button(new Rect(cx + toggleW + 6f, cy, confirmW, 28f),
                        CompanionConfigStore.L(lang, "Подтвердить уход", "Confirm Leave", "确认离队"), _catOnStyle))
                    {
                        CampGarrisonSystem.StartSafeDismiss(ag, st, CampGarrisonSystem.PendingDismissReturnGear);
                    }
                    if (GUI.Button(new Rect(cx + toggleW + confirmW + 12f, cy, cancelW, 28f),
                        CompanionConfigStore.L(lang, "Отмена", "Cancel", "取消"), _btnStyle))
                    {
                        CampGarrisonSystem.PendingDismissConfirmGuid = "";
                    }
                }
                else
                {
                    float orderW = (innerW - 24f) / 5f;
                    if (GUI.Button(new Rect(cx, cy, orderW, 28f),
                        CompanionConfigStore.L(lang, "Сортировать", "Sort Base", "整理基地"), _btnStyle))
                        BaseSortingSystem.CommandSortBase(ag, null, instant: false);

                    if (GUI.Button(new Rect(cx + (orderW + 6f) * 1f, cy, orderW, 28f),
                        CompanionConfigStore.L(lang, "Багажник", "Unload Car", "卸后备箱"), _btnStyle))
                        BaseChoreSystem.CommandUnloadVehiclesToBase(ag, silentIfEmpty: false);

                    if (GUI.Button(new Rect(cx + (orderW + 6f) * 2f, cy, orderW, 28f),
                        CompanionConfigStore.L(lang, "Боевой клич", "Battle Cry", "战吼突击"), _btnStyle))
                        SmartCombatAI.CommandSquadBattleCry(ag);

                    bool inGarrison = st.RosterMode == CompanionRosterMode.CampGarrison;
                    if (GUI.Button(new Rect(cx + (orderW + 6f) * 3f, cy, orderW, 28f), UiTip(
                        inGarrison
                            ? CompanionConfigStore.L(lang, "[x] Гарнизон", "[x] Garrison", "[x] 营地驻守")
                            : CompanionConfigStore.L(lang, "Гарнизон", "Garrison", "营地驻守"),
                        CompanionConfigStore.L(lang, "Оставить напарника охранять базу и работать по хозяйству, не бегая за вами в рейд",
                            "Keep companion guarding the base and doing chores instead of following raids",
                            "留在基地巡逻并干活，不再跟随外出远征")),
                        inGarrison ? _catOnStyle : _btnStyle))
                    {
                        CampGarrisonSystem.ToggleCampGarrison(ag, st);
                    }

                    if (GUI.Button(new Rect(cx + (orderW + 6f) * 4f, cy, orderW, 28f), UiTip(
                        CompanionConfigStore.L(lang, "Отпустить", "Dismiss", "安全离队"),
                        CompanionConfigStore.L(lang, "Безопасно отпустить напарника из отряда с возможностью сдать выданное снаряжение",
                            "Safely dismiss companion with optional physical gear handover",
                            "安全解雇队友，可选择先走过去交出装备")), _btnStyle))
                    {
                        CampGarrisonSystem.PendingDismissConfirmGuid = st.NpcGuid;
                        CampGarrisonSystem.PendingDismissReturnGear = true;
                    }
                }

                // Row 5: Tactical Combat Order & Status (2.1.0)
                cy += 32f;
                DrawCompanionTacticalRow(new Rect(cx, cy, innerW, 24f), ag, st, lang);
                }
                catch (Exception error)
                {
                    ReportUiFailure("Companion card " + i, error);
                    GUI.Label(new Rect(10f, i * (cardH + 8f) + 8f, viewRect.width - 20f, 50f),
                        CompanionConfigStore.L(lang, "Данные напарника обновляются. Откройте меню снова.",
                            "Companion data is refreshing. Reopen this menu.", "队友数据正在刷新，请重新打开菜单。"), _labelStyle);
                }
            }

            }
            finally { GUI.enabled = baseGuiEnabled; EndScrollArea(); }
        }

        private static void DrawDoctrineButtons(Rect area, GOAPAgent agent, CompanionRuntimeState st, ModLanguage lang)
        {
            var docs = new[]
            {
                (TacticalDoctrine.Balanced,     CompanionConfigStore.L(lang, " Умный баланс", " Balanced", " 均衡战术")),
                (TacticalDoctrine.Assault,      CompanionConfigStore.L(lang, " Штурм", " Assault", " 突击强攻")),
                (TacticalDoctrine.Bodyguard,    CompanionConfigStore.L(lang, " Телохранитель", " Bodyguard", " 贴身护卫")),
                (TacticalDoctrine.SniperKite,   CompanionConfigStore.L(lang, " Снайпер/Кайт", " Sniper/Kite", " 狙击风筝")),
                (TacticalDoctrine.StealthMelee, CompanionConfigStore.L(lang, " Тихий клинок", " Stealth Melee", " 潜行近战")),
                (TacticalDoctrine.Passive,      CompanionConfigStore.L(lang, " Не стрелять", " Passive", " 停止开火"))
            };

            float bw = (area.width - (docs.Length - 1) * 5f) / docs.Length;
            for (int i = 0; i < docs.Length; i++)
            {
                var d = docs[i];
                bool active = st.Doctrine == d.Item1;
                if (GUI.Button(new Rect(area.x + i * (bw + 5f), area.y, bw, area.height), UiTip(d.Item2, GetDoctrineTip(d.Item1, lang)), active ? _catOnStyle : _catOffStyle))
                {
                    st.Doctrine = d.Item1;
                    SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                    MultiplayerSyncSystem.SyncCompanionStateChange(st);
                    CompanionConfigStore.Save();
                }
            }
        }

        private static string GetDoctrineTip(TacticalDoctrine doctrine, ModLanguage lang)
        {
            switch (doctrine)
            {
                case TacticalDoctrine.Assault: return CompanionConfigStore.L(lang, "Идёт на сближение и давит на ближайшую цель", "Closes in and pressures the nearest target", "主动接近并压制最近的目标");
                case TacticalDoctrine.Bodyguard: return CompanionConfigStore.L(lang, "Держится рядом и атакует тех, кто угрожает вам", "Stays close and attacks whoever threatens you", "紧跟在旁，攻击威胁你的敌人");
                case TacticalDoctrine.SniperKite: return CompanionConfigStore.L(lang, "Держит дистанцию и отступает, стреляя издалека", "Keeps distance and backs off while shooting", "保持距离，边退边射击");
                case TacticalDoctrine.StealthMelee: return CompanionConfigStore.L(lang, "Бережёт патроны и дерётся тихо в ближнем бою", "Saves ammo and fights quietly in melee", "节省弹药，安静地近战");
                case TacticalDoctrine.Passive: return CompanionConfigStore.L(lang, "Не открывает огонь сам — удобно для скрытности", "Never opens fire on its own — good for stealth", "不会主动开火——适合潜行");
                default: return CompanionConfigStore.L(lang, "Сам выбирает дистанцию и оружие по ситуации", "Picks range and weapon to suit the fight", "根据战况自动选择距离和武器");
            }
        }

        private static void DrawTabBaseBoxes(Rect area, ModLanguage lang)
        {
            var containers = BaseSortingSystem.GetFilteredContainersForUI(forceScan: false);

            // Top action bar
            GUI.Box(new Rect(area.x, area.y, area.width, 46f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 8f;
            float commandW = (area.width - 34f) / 4f;

            string filterLabel = BaseSortingSystem.ContainerListFilterMode switch
            {
                1 => CompanionConfigStore.L(lang, "Фильтр: Рядом (≤7м)", "Filter: Nearby (≤7m)", "筛选：附近（≤7米）"),
                2 => CompanionConfigStore.L(lang, "Фильтр: Все вокруг (18м)", "Filter: All (18m)", "筛选：全部（18米）"),
                _ => CompanionConfigStore.L(lang, "Мои + у ног (≤2м)", "Mine + Close (≤2m)", "筛选：已标记 + 脚边")
            };
            if (GUI.Button(new Rect(bx, by, commandW, 30f), filterLabel, BaseSortingSystem.ContainerListFilterMode == 0 ? _catOnStyle : _btnStyle))
            {
                BaseSortingSystem.ContainerListFilterMode = (BaseSortingSystem.ContainerListFilterMode + 1) % 3;
                containers = BaseSortingSystem.GetFilteredContainersForUI(forceScan: true);
            }

            if (GUI.Button(new Rect(bx + commandW + 6f, by, commandW, 30f),
                CompanionConfigStore.L(lang, "Авто-разметка рядом (≤8м)", "Auto-Detect Near (≤8m)", "自动识别身边箱子（≤8米）"), _btnStyle))
            {
                BaseSortingSystem.AutoDetectCategoriesFromContents();
                containers = BaseSortingSystem.GetFilteredContainersForUI(forceScan: true);
            }

            if (GUI.Button(new Rect(bx + (commandW + 6f) * 2f, by, commandW, 30f),
                CompanionConfigStore.L(lang, "Сортировать базу [F7]", "Sort Base [F7]", "命令队友：整理仓库 [F7]"), _catOnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            }

            if (GUI.Button(new Rect(bx + (commandW + 6f) * 3f, by, commandW, 30f),
                CompanionConfigStore.L(lang, "Собрать лут [Alt+F]", "Sweep Loot [Alt+F]", "搜刮周围物资 [Alt+F]"), _btnStyle))
            {
                AutoLootSystem.CommandSweepNearbyLoot(null, 24f);
            }

            float bodyY = area.y + 54f;
            float bodyH = area.height - 54f;

            // Left column: container list; Right column: selected container's category editor
            float leftW = area.width * 0.38f;
            float rightW = area.width - leftW - 10f;

            GUI.Box(new Rect(area.x, bodyY, leftW, bodyH), GUIContent.none, _boxStyle);
            // Reserve the scrollbar gutter before measuring text, so adding a scrollbar
            // cannot change wrapping and invalidate the row heights in this GUI pass.
            float rowWidth = leftW - 36f;
            var rowLabels = new string[containers.Count];
            var rowOffsets = new float[containers.Count];
            var rowHeights = new float[containers.Count];
            float totalRowHeight = 0f;
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c == null || c.Rule == null) continue;
                string markPrefix = c.Rule.IsMarkedByPlayer ? "[*] " : "";
                string summary = c.Rule.IsMarkedByPlayer
                    ? ItemCategorizer.FormatCategoriesSummary(c.Rule.Categories, c.Rule.IsDropoffSource, lang)
                    : CompanionConfigStore.L(lang, "Не отмечен (игнорируется)", "Not marked (ignored)", "未标记（不参与整理）");
                string unit = CompanionConfigStore.L(lang, "шт.", "items", "件");
                string mUnit = CompanionConfigStore.L(lang, "м", "m", "米");
                string coordTag = $"[{Mathf.RoundToInt(c.WorldPos.x)},{Mathf.RoundToInt(c.WorldPos.y)}]";
                string btnText = $"{markPrefix}{c.DisplayName} {coordTag} ({c.DistanceToPlayer:F1}{mUnit} | {c.ItemCount} {unit})\n{summary}";

                rowLabels[i] = btnText;
                rowOffsets[i] = totalRowHeight;
                rowHeights[i] = Mathf.Max(48f, _btnStyle.CalcHeight(new GUIContent(btnText), rowWidth) + 8f);
                totalRowHeight += rowHeights[i] + 6f;
            }
            var viewRect = new Rect(0f, 0f, leftW - 28f, Mathf.Max(bodyH - 10f, totalRowHeight));
            DiscoveredContainer selectedContainer = null;
            BeginScrollArea(new Rect(area.x + 4f, bodyY + 4f, leftW - 8f, bodyH - 8f), ref _scrollBoxes, viewRect, 2);
            try
            {

            if (containers.Count == 0)
            {
                GUI.Label(new Rect(8f, 8f, leftW - 28f, 80f),
                    CompanionConfigStore.L(lang,
                        "Рядом не найдено контейнеров. Подойдите к ящикам на базе или откройте любой ящик.",
                        "No containers found nearby. Walk near your base chests or open any container.",
                        "附近未发现储物箱。请靠近基地的箱子或打开任意储物容器。"),
                    _labelStyle);
            }

            if (containers.Count > 0)
            {
                bool foundSel = false;
                for (int i = 0; i < containers.Count; i++)
                {
                    if (containers[i] != null && containers[i].ContainerId == _selectedBoxIdInHub)
                    {
                        foundSel = true;
                        break;
                    }
                }
                if (!foundSel && containers[0] != null)
                    _selectedBoxIdInHub = containers[0].ContainerId;
            }

            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (c == null || c.Rule == null) continue;
                if (c.ContainerId == _selectedBoxIdInHub)
                    selectedContainer = c;

                bool isSel = c.ContainerId == _selectedBoxIdInHub;

                if (GUI.Button(new Rect(4f, rowOffsets[i], rowWidth, rowHeights[i]), rowLabels[i], isSel ? _catOnStyle : _btnStyle))
                {
                    _selectedBoxIdInHub = c.ContainerId;
                    selectedContainer = c;
                }
            }
            }
            finally { EndScrollArea(); }

            // Right panel: Editor for selected container
            var rightRect = new Rect(area.x + leftW + 10f, bodyY, rightW, bodyH);
            GUI.Box(rightRect, GUIContent.none, _boxStyle);

            if (selectedContainer != null && selectedContainer.Rule != null)
            {
                var rule = selectedContainer.Rule;
                float rx = rightRect.x + 12f;
                float ry = rightRect.y + 8f;
                float rw = rightRect.width - 24f;

                GUI.Label(new Rect(rx, ry, rw, 46f),
                    CompanionConfigStore.L(lang,
                        $"Ящик: {selectedContainer.DisplayName} (предметов: {selectedContainer.ItemCount} | {selectedContainer.DistanceToPlayer:F1}м)",
                        $"Chest: {selectedContainer.DisplayName} (items: {selectedContainer.ItemCount} | {selectedContainer.DistanceToPlayer:F1}m)",
                        $"储物箱：{selectedContainer.DisplayName}（物品：{selectedContainer.ItemCount} | {selectedContainer.DistanceToPlayer:F1}米）"),
                    _labelStyle);

                ry += 50f;
                string markText = (rule.IsMarkedByPlayer ? "[x] " : "[ ] ") +
                    CompanionConfigStore.L(lang,
                        "Ящик базы — участвует в сортировке",
                        "Marked as Base Chest (Included in sorting)",
                        "标记为我的基地储物箱（参与自动分拣）");
                if (GUI.Button(new Rect(rx, ry, rw, 30f), markText, rule.IsMarkedByPlayer ? _catOnStyle : _catOffStyle))
                {
                    rule.IsMarkedByPlayer = !rule.IsMarkedByPlayer;
                    if (!rule.IsMarkedByPlayer)
                    {
                        rule.IsDropoffSource = false;
                        rule.Categories = StorageCategory.None;
                    }
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }

                ry += 36f;
                string dropText = (rule.IsDropoffSource ? "[x] " : "[ ] ") +
                    CompanionConfigStore.L(lang,
                        "Приёмник — NPC разносят вещи ОТСЮДА",
                        "Drop-off source — NPCs carry items FROM here",
                        "收货中转箱（队友会从此处取出战利品并分拣到各个分类箱）");
                if (GUI.Button(new Rect(rx, ry, rw, 30f), dropText, rule.IsDropoffSource ? _catOnStyle : _catOffStyle))
                {
                    rule.IsDropoffSource = !rule.IsDropoffSource;
                    rule.IsMarkedByPlayer = rule.IsDropoffSource || rule.Categories != StorageCategory.None;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }

                ry += 36f;
                if (GUI.Button(new Rect(rx, ry, rw * 0.48f, 30f),
                    CompanionConfigStore.L(lang,
                        $"Приоритет: {rule.Priority} (сменить)",
                        $"Priority: {rule.Priority} (cycle)",
                        $"存放优先级：*{rule.Priority}（点击切换）"), _btnStyle))
                {
                    rule.Priority = (rule.Priority % 5) + 1;
                    rule.IsMarkedByPlayer = true;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + rw * 0.50f, ry, rw * 0.50f, 30f),
                    (rule.AutoCompact ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Авто-уплотнение сетки", "Auto-Compact Grid", "整理后自动紧凑排列"),
                    rule.AutoCompact ? _catOnStyle : _catOffStyle))
                {
                    rule.AutoCompact = !rule.AutoCompact;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }

                ry += 36f;
                GUI.Label(new Rect(rx, ry, rw, 20f),
                    CompanionConfigStore.L(lang,
                        "Категории хранения (отмечают ящик автоматически):",
                        "Storage categories (automatically mark this chest):",
                        "勾选存入此储物箱的物品分类（勾选后自动标记为基地箱子）："),
                    _subHeaderStyle);

                ry += 22f;
                var cats = ItemCategorizer.AllCategories;
                int cols = 2;
                float cellW = (rw - 8f) / cols;
                float cellH = 30f;
                for (int i = 0; i < cats.Length; i++)
                {
                    var cat = cats[i];
                    int rIdx = i / cols;
                    int cIdx = i % cols;
                    bool on = (rule.Categories & cat) != 0;
                    string label = (on ? "[x] " : "[ ] ") + ItemCategorizer.GetCategoryName(cat, lang);
                    if (GUI.Button(new Rect(rx + cIdx * (cellW + 8f), ry + rIdx * 34f, cellW, cellH), label, on ? _catOnStyle : _catOffStyle))
                    {
                        if (on) rule.Categories &= ~cat;
                        else rule.Categories |= cat;
                        rule.IsMarkedByPlayer = rule.IsDropoffSource || rule.Categories != StorageCategory.None;
                        MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                        CompanionConfigStore.Save();
                    }
                }

                ry += ((cats.Length + cols - 1) / cols) * 34f + 6f;
                float pw = (rw - 12f) / 3f;
                if (GUI.Button(new Rect(rx, ry, pw, 30f), CompanionConfigStore.L(lang, "Выбрать все", "Select All", "全选分类"), _btnStyle))
                {
                    rule.Categories = StorageCategory.All;
                    rule.IsMarkedByPlayer = true;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + pw + 6f, ry, pw, 30f), CompanionConfigStore.L(lang, "Убрать из базы", "Unmark / Clear", "取消标记/清空"), _btnStyle))
                {
                    rule.Categories = StorageCategory.None;
                    rule.IsDropoffSource = false;
                    rule.IsMarkedByPlayer = false;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + (pw + 6f) * 2f, ry, pw, 30f),
                    CompanionConfigStore.L(lang, "Сортировать", "Sort This Box", "整理此箱物品"), _catOnStyle))
                {
                    BaseSortingSystem.CommandSortBase(null, selectedContainer.Inventory, instant: false);
                }
            }
        }

        private static float _settingsContentHeight = 1350f;
        private static bool _confirmResetAllSettings = false;

        private static float DrawSettingsHeading(float x, float y, float width, string title)
        {
            GUI.Box(new Rect(x, y, width, 30f), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x + 8f, y, width - 16f, 30f), title, _subHeaderStyle);
            return y + 38f;
        }

        private static void DrawTabSettings(Rect area, ModLanguage lang)
        {
            BeginScrollArea(area, ref _scrollSettings, new Rect(0f, 0f, area.width - 22f, _settingsContentHeight), 4);
            try
            {
            float lx = 4f;
            float ly = 4f;
            float lw = area.width - 30f;
            ly = DrawDisplaySettings(lx, ly, lw, lang);
            ly = DrawSettingsHeading(lx, ly, lw, CompanionConfigStore.L(lang, "Бой и тактика", "Combat & tactics", "战斗与战术"));
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableSmartCombat ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Умный боевой ИИ, радар тыла и авто-клин", "Smart Combat AI, Flank Radar & Auto-Unjam", "智能战斗 AI、背后偷袭雷达与自动排障"),
                CompanionConfigStore.EnableSmartCombat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableSmartCombat = !CompanionConfigStore.EnableSmartCombat;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableKiting ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Тактический кайтинг (отход при стрельбе вблизи)", "Tactical Kiting (Backpedal while firing)", "战术风筝走位（近身边退边打）"),
                CompanionConfigStore.EnableKiting ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableKiting = !CompanionConfigStore.EnableKiting;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableAntiFriendlyFire ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Анти-Френдли-Фаер (выход с линии огня игрока)", "Anti-Friendly-Fire (Step out of player aim line)", "防误伤让枪线（自动闪开玩家射界）"),
                CompanionConfigStore.EnableAntiFriendlyFire ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableAntiFriendlyFire = !CompanionConfigStore.EnableAntiFriendlyFire;
                CompanionConfigStore.Save();
            }
            ly += 36f;
            ly = DrawTacticalSettings(lx, ly, lw, lang);

            ly = DrawSettingsHeading(lx, ly, lw, CompanionConfigStore.L(lang, "Поддержка отряда", "Squad support", "队伍支援"));
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableFieldMedic ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Полевой медик и снабженец патронами", "Field Medic & Emergency Ammo Supplier", "战地急救包扎与紧急弹药支援"),
                CompanionConfigStore.EnableFieldMedic ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableFieldMedic = !CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.EnableAmmoShare = CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.AutoShareFoodOnEat ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Авто-делёжка пайка при еде (Бафф «Сытый и злой»)", "Auto-Share Food When Eating ('Fed & Fierce' Buff)", "进食自动分口粮（触发「饱腹狂暴」增益）"),
                CompanionConfigStore.AutoShareFoodOnEat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AutoShareFoodOnEat = !CompanionConfigStore.AutoShareFoodOnEat;
                CompanionConfigStore.Save();
            }
            ly += 36f;
            ly = DrawRareSupplySettings(lx, ly, lw, lang);

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableAutoBioAndSleep ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang,
                    "Авто-противогаз в спорах/дожде, лечение дебаффов и вахта сна",
                    "Auto Bio-Masks in Hazards, Debuff Care & Sentry Sleep Shifts",
                    "血雨/辐射自动戴防毒面具、解异常与营地轮班睡眠"),
                CompanionConfigStore.EnableAutoBioAndSleep ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableAutoBioAndSleep = !CompanionConfigStore.EnableAutoBioAndSleep;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            ly = DrawSettingsHeading(lx, ly, lw, CompanionConfigStore.L(lang, "База и походный быт", "Base & camp life", "基地与营地生活"));
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableBaseAutoDropoff ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Авто-выгрузка хабара на базе и реплики мира", "Base Auto-Dropoff & World NPC Chatter", "回基地自动卸货与野生 NPC 路人台词"),
                CompanionConfigStore.EnableBaseAutoDropoff ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableBaseAutoDropoff = !CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.EnableWorldNpcChatter = CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableBaseChores ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Живой лагерь (костёр, огород, чистка оружия, багажник)", "Camp Chores (Campfire, Crops, Gear Repair & Trunk)", "营地内务（篝火取暖、种菜浇水、擦枪修甲与卸车）"),
                CompanionConfigStore.EnableBaseChores ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableBaseChores = !CompanionConfigStore.EnableBaseChores;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            string repairModeLbl = CompanionConfigStore.FormatAutoRepairModeLabel(CompanionConfigStore.AutoRepairMode, lang);
            if (GUI.Button(new Rect(lx, ly, lw, 30f), UiTip(
                CompanionConfigStore.L(lang,
                    $"Авторемонт снаряжения NPC (экипировка + инвентарь): {repairModeLbl}",
                    $"NPC Gear Auto-Repair (equipped + inventory): {repairModeLbl}",
                    $"队友装备自动维修（已穿戴 + 背包）：{repairModeLbl}"),
                CompanionConfigStore.L(lang,
                    "Автоматически чинит всё экипированное (огнестрел, обвесы, магазины, броню, щит, противогаз, экзоскелет, рюкзак) и снаряжение в инвентаре/сумках NPC. Режимы: Материалы + ТО / Только материалы / Бесплатное ТО / Выкл.",
                    "Automatically repairs all equipped gear (firearms, attachments, mags, armor, shield, mask, exo, backpack) and repairable items in companion inventories/pouches. Modes: Materials + Maint / Materials only / Free maint / Off.",
                    "自动维修队友所有已穿戴装备（枪械、配件、弹匣、护甲、盾牌、面具、外骨骼、背包）及背包/共享包内可修装备。模式：材料优先+维护 / 仅消耗材料 / 免费维护 / 关闭。")),
                CompanionConfigStore.AutoRepairMode != GearAutoRepairMode.Off ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.CycleAutoRepairMode();
            }
            ly += 36f;

            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableSmartFlashlight ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Умный фонарь (вкл. ночью / гашение в стелсе) и Реанимация", "Smart Flashlight (Night/Stealth) & Emergency Rescue", "智能手电筒（夜间开启/潜行熄灯）与濒死强心抢救"),
                CompanionConfigStore.EnableSmartFlashlight ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableSmartFlashlight = !CompanionConfigStore.EnableSmartFlashlight;
                CompanionConfigStore.EnablePlayerRescue = CompanionConfigStore.EnableSmartFlashlight;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            ly = DrawSettingsHeading(lx, ly, lw, CompanionConfigStore.L(lang, "Отряд и управление", "Squad & controls", "队伍与操作"));
            string capLabel = CompanionConfigStore.FormatMaxTeamSizeLabel(lang);
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                CompanionConfigStore.L(lang,
                    $"Лимит напарников в отряде: {capLabel} (нажмите для смены до 15)",
                    $"Max Squad Companions Limit: {capLabel} (click to cycle up to 15)",
                    $"队伍人数上限：{capLabel}（点击切换，最高 15 人）"),
                CompanionConfigStore.MaxTeamSize > 0 ? _catOnStyle : _btnStyle))
            {
                CompanionConfigStore.CycleMaxTeamSize();
            }
            ly += 36f;

            bool useAltF6 = CompanionConfigStore.ShouldUseAltForRationHotkey();
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (useAltF6 ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang,
                    "Хоткей пайка Alt+F6 (совместимость с AVO Expanded на F6)",
                    "Ration Hotkey Alt+F6 (AVO Expanded F6 compatibility)",
                    "分口粮快捷键改为 Alt+F6（兼容 AVO Expanded 的 F6 菜单）"),
                useAltF6 ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.RequireAltForRationHotkey = !CompanionConfigStore.RequireAltForRationHotkey;
                CompanionConfigStore.Save();
            }
            ly += 36f;

            float uiBtnW = (lw - 6f) * 0.5f;
            if (GUI.Button(new Rect(lx, ly, uiBtnW, 30f),
                (CompanionConfigStore.ShowHudBadge ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Мини-панель на экране", "Show Mini-HUD Bar", "显示顶部快捷栏"),
                CompanionConfigStore.ShowHudBadge ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.ShowHudBadge = !CompanionConfigStore.ShowHudBadge;
                if (CompanionConfigStore.ShowHudBadge) CompanionConfigStore.HudBadgeCollapsed = false;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + uiBtnW + 6f, ly, uiBtnW, 30f),
                CompanionConfigStore.L(lang, "Сбросить позиции UI", "Reset UI Positions", "重置所有窗口位置"), _btnStyle))
            {
                CompanionConfigStore.ResetUiPositions();
            }
            ly += 36f;

            if (!_confirmResetAllSettings)
            {
                if (GUI.Button(new Rect(lx, ly, lw, 30f), UiTip(
                    CompanionConfigStore.L(lang,
                        "Сбросить все настройки по умолчанию (разметка ящиков базы сохранится)",
                        "Reset All Settings to Default (preserves marked base chests)",
                        "恢复全部默认设置（保留已标记的基地储物箱）"),
                    CompanionConfigStore.L(lang,
                        "Возвращает все переключатели, формацию, доктрины, резервы и позиции окон к заводским настройкам мода",
                        "Restores all toggles, formation, doctrines, reserves and window positions to factory defaults",
                        "将所有开关、阵型、战术学说、物资保留线与窗口位置恢复为默认设置")), _btnStyle))
                {
                    _confirmResetAllSettings = true;
                }
            }
            else
            {
                if (GUI.Button(new Rect(lx, ly, uiBtnW, 30f),
                    CompanionConfigStore.L(lang, "Подтвердить сброс настроек", "Confirm Reset to Default", "确认恢复默认设置"), _catOnStyle))
                {
                    CompanionConfigStore.ResetAllSettingsToDefault(preserveContainerRules: true);
                    _confirmResetAllSettings = false;
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(lang,
                        "Все настройки мода и доктрины напарников сброшены по умолчанию (разметка ящиков сохранена).",
                        "All mod settings and companion doctrines reset to default (marked base chests preserved).",
                        "已将所有模组设置与队友学说恢复为默认值（基地储物箱标记已保留）。"), 5f);
                }
                if (GUI.Button(new Rect(lx + uiBtnW + 6f, ly, uiBtnW, 30f),
                    CompanionConfigStore.L(lang, "Отмена", "Cancel", "取消"), _btnStyle))
                {
                    _confirmResetAllSettings = false;
                }
            }
            ly += 36f;

            ly = DrawSettingsHeading(lx, ly, lw, CompanionConfigStore.L(lang,
                "Диагностика и запись лога (баг-репорты)",
                "Diagnostics & Event Logging (Bug Reports)",
                "诊断与事件日志录制（Bug 反馈）"));

            bool logOn = EventLogRecorder.IsEnabled;
            if (GUI.Button(new Rect(lx, ly, lw, 30f), UiTip(
                (logOn ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang,
                    "Записывать события мода в файл лога (для отправки автору при багах)",
                    "Record mod events to log file (to send to author when reporting bugs)",
                    "录制模组运行事件日志（遇到 Bug 时发送给作者排查）"),
                CompanionConfigStore.L(lang,
                    "Пишет приказы, переходы состояний ИИ, застревания, ошибки и снимки отряда в BepInEx/config/SmartCompanions_EventLog.log (ротация до 4 МБ, без имён и путей ПК)",
                    "Records orders, AI state transitions, stalls, errors and squad snapshots to BepInEx/config/SmartCompanions_EventLog.log (4 MB cap, privacy-safe)",
                    "记录指令、AI 状态切换、卡位、报错与队伍快照至 BepInEx/config/SmartCompanions_EventLog.log（上限 4 MB，不含隐私信息）")),
                logOn ? _catOnStyle : _catOffStyle))
            {
                EventLogRecorder.SetEnabled(!logOn, "ui_settings");
                BaseSortingSystem.SetBanner(EventLogRecorder.IsEnabled
                    ? CompanionConfigStore.L(lang,
                        "Запись лога ВКЛЮЧЕНА -> BepInEx/config/SmartCompanions_EventLog.log",
                        "Event logging ENABLED -> BepInEx/config/SmartCompanions_EventLog.log",
                        "事件日志录制已开启 -> BepInEx/config/SmartCompanions_EventLog.log")
                    : CompanionConfigStore.L(lang,
                        "Запись лога остановлена и сохранена на диск.",
                        "Event logging stopped and flushed to disk.",
                        "事件日志录制已停止并写入磁盘。"), 5f);
            }
            ly += 34f;

            string sizeStr = EventLogRecorder.FormatFileSize(EventLogRecorder.CachedFileSizeBytes);
            GUI.Box(new Rect(lx, ly, lw, 28f), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(lx + 10f, ly + 3f, lw - 20f, 22f), UiSafe(logOn
                ? CompanionConfigStore.L(lang,
                    $"[REC] Идёт запись  ·  Событий: {EventLogRecorder.SessionEventCount}  ·  Файл: {sizeStr}  ({EventLogRecorder.DisplayRelativePath})",
                    $"[REC] Recording  ·  Events: {EventLogRecorder.SessionEventCount}  ·  File: {sizeStr}  ({EventLogRecorder.DisplayRelativePath})",
                    $"[REC] 正在录制  ·  事件数：{EventLogRecorder.SessionEventCount}  ·  大小：{sizeStr}  ({EventLogRecorder.DisplayRelativePath})")
                : CompanionConfigStore.L(lang,
                    $"[OFF] Запись выкл.  ·  Файл: {sizeStr}  ({EventLogRecorder.DisplayRelativePath})",
                    $"[OFF] Recording off  ·  File: {sizeStr}  ({EventLogRecorder.DisplayRelativePath})",
                    $"[OFF] 未开启录制  ·  文件：{sizeStr}  ({EventLogRecorder.DisplayRelativePath})")), _labelStyle);
            ly += 34f;

            float logBtnW = (lw - 12f) / 3f;
            if (GUI.Button(new Rect(lx, ly, logBtnW, 28f), UiTip(
                CompanionConfigStore.L(lang, "[!] Метка бага", "[!] Mark Bug", "[!] 标记 Bug"),
                CompanionConfigStore.L(lang,
                    "Вставляет в лог яркую метку времени и полный снимок состояния отряда прямо сейчас (работает даже при выключенной записи)",
                    "Writes a timestamped bug marker and full squad snapshot to the log right now (works even if continuous logging was off)",
                    "立即在日志中写入 Bug 时间标记与完整队伍快照（即使未开启持续录制也可使用）")),
                logOn ? _catOnStyle : _btnStyle))
            {
                bool ok = EventLogRecorder.MarkBugNow("ui_button");
                BaseSortingSystem.SetBanner(ok
                    ? CompanionConfigStore.L(lang,
                        $"Метка бага #{EventLogRecorder.BugMarkCount} и снимок записаны в {EventLogRecorder.DisplayRelativePath}.",
                        $"Bug marker #{EventLogRecorder.BugMarkCount} and snapshot saved to {EventLogRecorder.DisplayRelativePath}.",
                        $"Bug 标记 #{EventLogRecorder.BugMarkCount} 与快照已保存至 {EventLogRecorder.DisplayRelativePath}。")
                    : CompanionConfigStore.L(lang,
                        "Не удалось записать метку бага на диск.",
                        "Failed to write bug marker to disk.",
                        "写入 Bug 标记失败。"), 5f);
            }
            if (GUI.Button(new Rect(lx + logBtnW + 6f, ly, logBtnW, 28f), UiTip(
                CompanionConfigStore.L(lang, "Открыть папку логов", "Open Log Folder", "打开日志文件夹"),
                CompanionConfigStore.L(lang,
                    "Открывает BepInEx/config в Проводнике, чтобы быстро прикрепить файл SmartCompanions_EventLog.log к баг-репорту",
                    "Opens BepInEx/config in Explorer so you can attach SmartCompanions_EventLog.log to a bug report",
                    "在资源管理器中打开 BepInEx/config，方便上传 SmartCompanions_EventLog.log")), _btnStyle))
            {
                bool opened = EventLogRecorder.OpenLogFolder();
                BaseSortingSystem.SetBanner(opened
                    ? CompanionConfigStore.L(lang,
                        "Открыта папка BepInEx/config с файлом SmartCompanions_EventLog.log.",
                        "Opened BepInEx/config folder with SmartCompanions_EventLog.log.",
                        "已打开 BepInEx/config 日志目录。")
                    : CompanionConfigStore.L(lang,
                        "Путь к файлу лога скопирован в буфер обмена.",
                        "Log file path copied to clipboard.",
                        "日志文件路径已复制至剪贴板。"), 4f);
            }
            if (GUI.Button(new Rect(lx + (logBtnW + 6f) * 2f, ly, logBtnW, 28f), UiTip(
                CompanionConfigStore.L(lang, "Очистить лог", "Clear Log", "清空日志"),
                CompanionConfigStore.L(lang,
                    "Удаляет старый файл SmartCompanions_EventLog.log, чтобы записать чистый лог воспроизведения бага",
                    "Clears SmartCompanions_EventLog.log so you can record a clean bug reproduction",
                    "清空旧的 SmartCompanions_EventLog.log，重新录制干净的复现日志")), _btnStyle))
            {
                EventLogRecorder.ClearLogFile();
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(lang,
                    "Файл лога событий очищен.",
                    "Event log file cleared.",
                    "事件日志文件已清空。"), 4f);
            }
            ly += 34f;

            bool avoPresent = CompanionConfigStore.IsAvoExpandedLoaded();
            string avoTag = avoPresent
                ? CompanionConfigStore.L(lang, "AVO: активен", "AVO: active", "AVO：已激活")
                : CompanionConfigStore.L(lang, "AVO: не найден", "AVO: standalone", "AVO：未安装");
            if (GUI.Button(new Rect(lx, ly, lw, 30f), UiTip(
                CompanionConfigStore.L(lang,
                    $"Скопировать диагностический отчёт ({avoTag}, без данных сохранения)",
                    $"Copy Privacy-Safe Diagnostic Report ({avoTag}, no save data)",
                    $"复制无隐私存档数据的诊断报告（{avoTag}）"),
                CompanionConfigStore.L(lang,
                    "Копирует отчёт в буфер обмена и сохраняет SmartCompanions_Diagnostics.txt без имён, путей и предметов",
                    "Copies the report to clipboard and writes SmartCompanions_Diagnostics.txt without names, paths or items",
                    "复制报告至剪贴板并保存诊断文件，不含姓名、路径与存档物品")), _btnStyle))
            {
                bool savedOk = PerformanceAndDiagnostics.ExportDiagnosticReport(out _);
                BaseSortingSystem.SetBanner(savedOk
                    ? CompanionConfigStore.L(lang,
                        "Диагностический отчёт скопирован в буфер обмена и сохранён в BepInEx/config (без данных сохранения).",
                        "Diagnostic report copied to clipboard and saved to BepInEx/config (no save data included).",
                        "诊断报告已复制至剪贴板并保存至 BepInEx/config（不含任何存档隐私数据）。")
                    : CompanionConfigStore.L(lang,
                        "Диагностический отчёт скопирован в буфер обмена.",
                        "Diagnostic report copied to clipboard.",
                        "诊断报告已复制至剪贴板。"), 6f);
            }
            ly += 36f;

            string netMode = MultiplayerSyncSystem.IsMultiplayerHost
                ? CompanionConfigStore.L(lang, "Хост", "Host", "主机")
                : MultiplayerSyncSystem.IsMultiplayerClient
                    ? CompanionConfigStore.L(lang, "Клиент", "Client", "客户端")
                    : CompanionConfigStore.L(lang, "Одиночная игра", "Singleplayer", "单人模式");
            int netNpcs = MultiplayerSyncSystem.IsMultiplayerHost
                ? MultiplayerSyncSystem.CountHostReplicatedHumanNpcs()
                : MultiplayerSyncSystem.CountClientBoundHumanPuppets();
            GUI.Box(new Rect(lx, ly, lw, 30f), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(lx + 10f, ly + 3f, lw - 20f, 24f), UiSafe(CompanionConfigStore.L(lang,
                $"Сеть: {netMode}  ·  Пакеты SC14 отпр./прин.: {MultiplayerSyncSystem.PacketsSent}/{MultiplayerSyncSystem.PacketsReceived}  ·  Отклонено: {MultiplayerSyncSystem.RejectedCompanionChanges}  ·  Сетевые NPC: {netNpcs}",
                $"Network: {netMode}  ·  SC14 sent/recv: {MultiplayerSyncSystem.PacketsSent}/{MultiplayerSyncSystem.PacketsReceived}  ·  Rejected: {MultiplayerSyncSystem.RejectedCompanionChanges}  ·  Synced NPCs: {netNpcs}",
                $"网络：{netMode}  ·  SC14 收发：{MultiplayerSyncSystem.PacketsSent}/{MultiplayerSyncSystem.PacketsReceived}  ·  拦截冲突：{MultiplayerSyncSystem.RejectedCompanionChanges}  ·  同步队友：{netNpcs}")), _labelStyle);
            ly += 36f;

            _settingsContentHeight = ly + 12f;
            }
            finally { EndScrollArea(); }
        }

        private static void DrawTabChatter(Rect area, ModLanguage lang)
        {
            GUI.Box(new Rect(area.x, area.y, area.width, 182f), GUIContent.none, _boxStyle);
            float lx = area.x + 12f;
            float ly = area.y + 8f;
            float lw = area.width - 24f;
            if (GUI.Button(new Rect(lx, ly, lw, 30f),
                (CompanionConfigStore.EnableChatter ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Живые комментарии и фразы напарников (60 тем)", "Companion Context Chatter & Reactions (60 Topics)", "开启队友实时情景吐槽与互动气泡（60大主题）"),
                CompanionConfigStore.EnableChatter ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableChatter = !CompanionConfigStore.EnableChatter;
                CompanionConfigStore.Save();
            }
            ly += 34f;

            GUI.Label(new Rect(lx, ly, lw, 20f),
                CompanionConfigStore.L(lang, " Режим лексики и характера NPC:", " NPC Profanity & Attitude Mode:", " 队友台词风格与脏话模式："), _subHeaderStyle);
            ly += 22f;

            float mw = (lw - 10f) / 3f;
            if (GUI.Button(new Rect(lx, ly, mw, 30f),
                CompanionConfigStore.L(lang, " Мат 18+ (Быдло)", " Hardcore 18+", " 末日粗口 18+"),
                CompanionConfigStore.Profanity == ProfanityMode.HardcoreRude ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.HardcoreRude;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + mw + 5f, ly, mw, 30f),
                CompanionConfigStore.L(lang, " Смешанный", " Mixed", " 混合模式"),
                CompanionConfigStore.Profanity == ProfanityMode.Mixed ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Mixed;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + (mw + 5f) * 2f, ly, mw, 30f),
                CompanionConfigStore.L(lang, " Без мата", " Clean", " 纯净无脏话"),
                CompanionConfigStore.Profanity == ProfanityMode.Clean ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Clean;
                CompanionConfigStore.Save();
            }
            ly += 34f;

            GUI.Label(new Rect(lx, ly, lw, 20f),
                CompanionConfigStore.L(lang, " Проверить реплику напарника прямо сейчас:", " Test Companion Speech Now:", " 立即测试队友气泡台词："), _subHeaderStyle);
            ly += 22f;

            float hw = (lw - 18f) / 4f;
            var player = GameController.instance?.playerCharacter;
            var nearest = Il2CppSafe.IsAliveUnity(player)
                ? SmartChatterSystem.PickNearestTeammate(player.transform.position, 50f)
                : null;
            if (GUI.Button(new Rect(lx, ly, hw, 30f),
                CompanionConfigStore.L(lang, " Стейк (Наезд/Зависть)", " Steak Envy", " 偷吃牛排吐槽"), _catOnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.PlayerEatSteakMeat, 0.1f, 6.5f, true);

            string rationKey = CompanionConfigStore.ShouldUseAltForRationHotkey() ? "Alt+F6" : "F6";
            if (GUI.Button(new Rect(lx + hw + 6f, ly, hw, 30f),
                CompanionConfigStore.L(lang, $"Паёк [{rationKey}]", $"Ration [{rationKey}]", $"分享口粮 [{rationKey}]"), _catOnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(nearest, fromAutoShare: false);

            if (GUI.Button(new Rect(lx + (hw + 6f) * 2f, ly, hw, 30f),
                CompanionConfigStore.L(lang, " Орда / Замес (Мат)", " Horde Callout", " 遭遇尸潮喊话"), _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.CombatSpotHorde, 0.1f, 5.5f, true);

            if (GUI.Button(new Rect(lx + (hw + 6f) * 3f, ly, hw, 30f),
                CompanionConfigStore.L(lang, " Перегруз (Плюшкин)", " Overweight Rant", " 背包超重吐槽"), _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.LootOverweight, 0.1f, 5.5f, true);


            var log = SmartChatterSystem.RecentSpeechLog;
            var logArea = new Rect(area.x, area.y + 192f, area.width, area.height - 192f);
            GUI.Box(logArea, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(logArea.x + 12f, logArea.y + 6f, logArea.width - 24f, 28f),
                CompanionConfigStore.L(lang, "ПОСЛЕДНИЕ РЕПЛИКИ", "RECENT SPEECH", "最近的对话"), _subHeaderStyle);
            if (log.Count == 0)
            {
                GUI.Label(new Rect(logArea.x + 12f, logArea.y + 42f, logArea.width - 24f, 64f),
                    CompanionConfigStore.L(lang, "Здесь появятся реплики напарников. Выберите тему выше, чтобы начать разговор.",
                        "Your companions' recent lines will appear here. Choose a topic above to start a conversation.",
                        "队友最近说的话会显示在这里。选择上面的主题即可开始对话。"), _labelStyle);
                return;
            }
            float textWidth = logArea.width - 54f;
            var rowHeights = new float[log.Count];
            float totalHeight = 0f;
            for (int i = 0; i < log.Count; i++)
            {
                rowHeights[i] = Mathf.Max(42f, _labelStyle.CalcHeight(new GUIContent(log[i]), textWidth) + 14f);
                totalHeight += rowHeights[i] + 4f;
            }
            BeginScrollArea(new Rect(logArea.x + 8f, logArea.y + 38f, logArea.width - 16f, logArea.height - 46f),
                ref _scrollChatter, new Rect(0f, 0f, logArea.width - 36f, totalHeight), 3);
            try
            {
                float rowY = 0f;
                for (int i = 0; i < log.Count; i++)
                {
                    GUI.Box(new Rect(0f, rowY, textWidth + 14f, rowHeights[i]), GUIContent.none, _winStyle);
                    GUI.Label(new Rect(7f, rowY + 5f, textWidth, rowHeights[i] - 10f), log[i], _labelStyle);
                    rowY += rowHeights[i] + 4f;
                }
            }
            finally { EndScrollArea(); }

        }
    }
}
