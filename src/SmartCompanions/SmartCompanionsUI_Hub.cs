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
            try { GUI.tooltip = string.Empty; } catch { }
            GUI.Box(r, GUIContent.none, _winStyle);

            float x = r.x + 14f;
            float y = r.y + 10f;
            float w = r.width - 28f;

            // Keep the title separate from controls; the whole free header is draggable.
            var titleDragRect = new Rect(x, y, w - 300f, 32f);
            GUI.Label(titleDragRect,
                CompanionConfigStore.L(lang, "УМНЫЕ НАПАРНИКИ", "SMART COMPANIONS", "智能队友"), _headerStyle);
            var winPos = new Vector2(_mainWinRect.x, _mainWinRect.y);
            HandleWindowDrag(1, titleDragRect, ref winPos, new Vector2(_mainWinRect.width, _mainWinRect.height), p =>
            {
                _mainWinRect.x = p.x;
                _mainWinRect.y = p.y;
                CompanionConfigStore.MainWinPosX = p.x;
                CompanionConfigStore.MainWinPosY = p.y;
            });

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
                    _activeTab == tabOrder[i] ? _catOnStyle : _btnStyle))
                {
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
                if (_activeTab == 0) DrawTabSquad(contentRect, lang);
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
            GUI.Label(new Rect(bx + 8f, by, area.width - 265f, 28f), CompanionConfigStore.L(lang,
                $"Напарников рядом: {teammates.Count}", $"Companions nearby: {teammates.Count}", $"附近队友：{teammates.Count}"), _subHeaderStyle);
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
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 2f, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Собрать лут", "Sweep Loot", "搜刮物资"), _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, 24f);
            string rationKey = CompanionConfigStore.ShouldUseAltForRationHotkey() ? "Alt+F6" : "F6";
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 3f, by, actionW, 30f),
                CompanionConfigStore.L(lang, $"Паёк [{rationKey}]", $"Ration [{rationKey}]", $"分口粮 [{rationKey}]"), _btnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(null, fromAutoShare: false);
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 4f, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Штурм [F9]", "Rally [F9]", "战吼 [F9]"), _btnStyle))
                SmartCombatAI.CommandSquadBattleCry(null);
            if (GUI.Button(new Rect(bx + (actionW + 6f) * 5f, by, actionW, 30f),
                CompanionConfigStore.L(lang, "Первая помощь", "First Aid", "急救"), _btnStyle) && teammates.Count > 0)
                CustomNpcCommands.ExecuteCustomCommand(teammates[0].npcData, CustomNpcCommands.EmergencyAid);

            float listY = area.y + 90f;
            float listH = area.height - 90f;

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

            float cardH = 164f;
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
                string name = cd?.characterName ?? $"NPC #{i + 1}";
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
                GUI.Label(new Rect(cx, cy, innerW - 190f, 24f),
                    $"{name}{ownerBadge}  ·  {rankTitle}  ·  {st.ExperiencePoints}/{nextXp} XP", _subHeaderStyle);
                if (!canCommand)
                {
                    GUI.Label(new Rect(cx + innerW - 360f, cy, 175f, 24f), UiTip(
                        CompanionConfigStore.L(lang, "только просмотр", "view only", "仅查看"),
                        CompanionConfigStore.L(lang, $"Приказы этому напарнику отдаёт {controllerName}",
                            $"Only {controllerName} can give orders to this companion", $"只有 {controllerName} 能指挥这名队友")), _labelStyle);
                    GUI.enabled = false;
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

                // Row 4: Direct Companion Orders
                cy += 30f;
                float orderW = (innerW - 18f) / 4f;
                if (GUI.Button(new Rect(cx, cy, orderW, 28f), CompanionConfigStore.L(lang, " Сортировать базу", " Sort Base Chests", " 整理基地储物箱"), _btnStyle))
                    BaseSortingSystem.CommandSortBase(ag, null, instant: false);

                if (GUI.Button(new Rect(cx + orderW + 6f, cy, orderW, 28f), CompanionConfigStore.L(lang, " Разгрузить багажник", " Unload Car Trunk", " 卸载汽车后备箱"), _btnStyle))
                    BaseChoreSystem.CommandUnloadVehiclesToBase(ag, silentIfEmpty: false);

                if (GUI.Button(new Rect(cx + (orderW + 6f) * 2f, cy, orderW, 28f), CompanionConfigStore.L(lang, " Байка у костра", " Campfire Story", " 篝火旁闲聊"), _btnStyle))
                    SmartChatterSystem.Speak(ag, ChatterTopic.CampfireStory, minCooldown: 0.1f, bubbleDuration: 6.5f, force: true);

                if (GUI.Button(new Rect(cx + (orderW + 6f) * 3f, cy, orderW, 28f), CompanionConfigStore.L(lang, " Боевой клич [F9]", " Battle Cry [F9]", " 战吼突击 [F9]"), _btnStyle))
                    SmartCombatAI.CommandSquadBattleCry(ag);
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
                CompanionConfigStore.L(lang, "Собрать лут вокруг", "Sweep Nearby Loot", "搜刮周围物资"), _btnStyle))
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

        private static float _settingsContentHeight = 1100f;

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
