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
            GUI.Box(r, GUIContent.none, _winStyle);

            float x = r.x + 14f;
            float y = r.y + 10f;
            float w = r.width - 28f;

            // Title bar
            GUI.Label(new Rect(x, y, w - 310f, 28f),
                CompanionConfigStore.L(lang,
                    " ZED ZONE — УМНЫЕ НАПАРНИКИ И ЛОГИСТИКА БАЗЫ [F8]",
                    " ZED ZONE — SMART COMPANIONS & BASE LOGISTICS [F8]",
                    " ZED ZONE — 智能队友与基地物流中心 [F8]"),
                _headerStyle);

            // 4-mode language selector in header bar: Auto | RU | EN | 中文
            float lx = r.xMax - 306f;
            if (GUI.Button(new Rect(lx, y, 62f, 26f), " Auto",
                CompanionConfigStore.LanguageMode == ModLanguage.Auto ? _catOnStyle : _btnStyle))
                SetLanguageMode(ModLanguage.Auto);
            if (GUI.Button(new Rect(lx + 65f, y, 50f, 26f), " RU",
                CompanionConfigStore.LanguageMode == ModLanguage.Russian ? _catOnStyle : _btnStyle))
                SetLanguageMode(ModLanguage.Russian);
            if (GUI.Button(new Rect(lx + 118f, y, 50f, 26f), " EN",
                CompanionConfigStore.LanguageMode == ModLanguage.English ? _catOnStyle : _btnStyle))
                SetLanguageMode(ModLanguage.English);
            if (GUI.Button(new Rect(lx + 171f, y, 54f, 26f), " 中文",
                CompanionConfigStore.LanguageMode == ModLanguage.Chinese ? _catOnStyle : _btnStyle))
                SetLanguageMode(ModLanguage.Chinese);

            if (GUI.Button(new Rect(r.xMax - 76f, y, 64f, 26f),
                CompanionConfigStore.L(lang, "Закрыть", "Close", "关闭"), _btnStyle))
            {
                ShowMainWindow = false;
                return;
            }

            y += 34f;
            // Tabs
            float tabW = (w - 16f) / 3f;
            if (GUI.Button(new Rect(x, y, tabW, 30f),
                CompanionConfigStore.L(lang,
                    " 1. Напарники и Боевой ИИ",
                    " 1. Companions & Combat AI",
                    " 1. 队友与智能战斗 AI"),
                _activeTab == 0 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 0;
            }
            if (GUI.Button(new Rect(x + tabW + 8f, y, tabW, 30f),
                CompanionConfigStore.L(lang,
                    " 2. Ящики базы и Сортировка",
                    " 2. Base Containers & Sorting",
                    " 2. 基地储物箱与分类整理"),
                _activeTab == 1 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 1;
                BaseSortingSystem.ScanNearbyContainers(force: true);
            }
            if (GUI.Button(new Rect(x + (tabW + 8f) * 2f, y, tabW, 30f),
                CompanionConfigStore.L(lang,
                    " 3. Фразы, Реакции и Настройки",
                    " 3. Chatter, Reactions & Settings",
                    " 3. 语录、反应与语言设置"),
                _activeTab == 2 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 2;
            }

            y += 38f;
            var contentRect = new Rect(x, y, w, r.height - (y - r.y) - 14f);

            try
            {
                if (_activeTab == 0) DrawTabSquad(contentRect, lang);
                else if (_activeTab == 1) DrawTabBaseBoxes(contentRect, lang);
                else DrawTabChatterAndSettings(contentRect, lang);
            }
            catch (Exception error)
            {
                ReportUiFailure("Tab " + _activeTab, error);
                DrawUiFailure(contentRect, lang);
            }

            if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) &&
                r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }
        }

        private static void DrawTabSquad(Rect area, ModLanguage lang)
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();

            // Squad-wide quick command row
            GUI.Box(new Rect(area.x, area.y, area.width, 42f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 8f;
            GUI.Label(new Rect(bx, by, 145f, 26f),
                CompanionConfigStore.L(lang,
                    $"Отряд: {teammates.Count} чел.",
                    $"Squad: {teammates.Count}",
                    $"在队：{teammates.Count} 人"), _subHeaderStyle);

            if (GUI.Button(new Rect(bx + 148f, by, 120f, 26f),
                CompanionConfigStore.L(lang, " Сорт. [F7]", " Sort [F7]", " 整理 [F7]"), _catOnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: false);

            if (GUI.Button(new Rect(bx + 272f, by, 118f, 26f),
                CompanionConfigStore.L(lang, " Багажник", " Unload Car", " 卸后备箱"), _btnStyle))
                BaseChoreSystem.CommandUnloadVehiclesToBase(null, silentIfEmpty: false);

            if (GUI.Button(new Rect(bx + 394f, by, 114f, 26f),
                CompanionConfigStore.L(lang, " Собрать лут", " Sweep Loot", " 搜刮物资"), _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, 20f);

            if (GUI.Button(new Rect(bx + 512f, by, 118f, 26f),
                CompanionConfigStore.L(lang, " Паёк [F6]", " Ration [F6]", " 分口粮 [F6]"), _catOnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(null, fromAutoShare: false);

            if (GUI.Button(new Rect(bx + 634f, by, 114f, 26f),
                CompanionConfigStore.L(lang, " Штурм [F9]", " Rally [F9]", " 战吼 [F9]"), _catOnStyle))
                SmartCombatAI.CommandSquadBattleCry(null);

            if (GUI.Button(new Rect(bx + 752f, by, 90f, 26f),
                CompanionConfigStore.L(lang, " Аптечка", " Aid", " 急救"), _btnStyle))
            {
                if (teammates.Count > 0)
                    CustomNpcCommands.ExecuteCustomCommand(teammates[0].npcData, CustomNpcCommands.EmergencyAid);
            }

            float listY = area.y + 50f;
            float listH = area.height - 50f;

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

            float cardH = 128f;
            var viewRect = new Rect(0, 0, area.width - 20f, teammates.Count * (cardH + 8f));
            BeginScrollArea(new Rect(area.x, listY, area.width, listH), ref _scrollSquad, viewRect, 1);
            try
            {

            for (int i = 0; i < teammates.Count; i++)
            {
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
                if (MultiplayerSyncSystem.IsMultiplayerOnline)
                {
                    bool isMine = MultiplayerSyncSystem.IsCompanionOwnedByLocalPlayer(ag.npcData);
                    string ownerLabel = !string.IsNullOrEmpty(st.OwnerPlayerName)
                        ? st.OwnerPlayerName
                        : CompanionConfigStore.L(lang, "Союзник", "Ally", "盟友");
                    ownerBadge = isMine
                        ? CompanionConfigStore.L(lang, " [Ваш]", " [Yours]", " [你的]")
                        : $" [{ownerLabel}]";
                }

                GUI.Label(new Rect(cx, cy, 415f, 22f),
                    $"* {name}{ownerBadge} [{rankTitle} · {st.ExperiencePoints}/{nextXp} XP]  |  HP: {hp:F0}/{maxHp:F0}  |   {wepName}",
                    _subHeaderStyle);

                if (GUI.Button(new Rect(cx + 420f, cy, 170f, 22f), archTitle, _btnStyle))
                {
                    st.Archetype = (CompanionArchetype)(((int)st.Archetype + 1) % 6);
                    CompanionConfigStore.Save();
                }

                GUI.Label(new Rect(cx + 596f, cy, viewRect.width - 604f, 22f),
                    CompanionConfigStore.L(lang,
                        $"{st.CurrentTaskText} | K:{st.KillsThisSession} S:{st.SortedItemsThisSession} C:{st.ChoresDoneThisSession}",
                        $"{st.CurrentTaskText} | K:{st.KillsThisSession} S:{st.SortedItemsThisSession} C:{st.ChoresDoneThisSession}",
                        $"{st.CurrentTaskText} | K:{st.KillsThisSession} S:{st.SortedItemsThisSession} C:{st.ChoresDoneThisSession}"),
                    _labelStyle);

                // Row 2: Doctrines
                cy += 26f;
                GUI.Label(new Rect(cx, cy, 90f, 24f), CompanionConfigStore.L(lang, "Доктрина:", "Doctrine:", "战术学说:"), _labelStyle);
                DrawDoctrineButtons(new Rect(cx + 85f, cy, viewRect.width - 95f, 24f), ag, st, lang);

                // Row 3: Per-companion feature toggles
                cy += 30f;
                float tw = (viewRect.width - 20f - 20f) / 5f;
                if (GUI.Button(new Rect(cx, cy, tw, 24f),
                    (st.AllowFieldMedic ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Медик", "Field Medic", "战地医疗"),
                    st.AllowFieldMedic ? _catOnStyle : _catOffStyle))
                {
                    st.AllowFieldMedic = !st.AllowFieldMedic;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 1f, cy, tw, 24f),
                    (st.AllowAmmoShare ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Патроны", "Ammo Share", "弹药补给"),
                    st.AllowAmmoShare ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAmmoShare = !st.AllowAmmoShare;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 2f, cy, tw, 24f),
                    (st.AllowAutoLootNearby ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Авто-лут", "Auto-Loot", "自动拾取"),
                    st.AllowAutoLootNearby ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoLootNearby = !st.AllowAutoLootNearby;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 3f, cy, tw, 24f),
                    (st.AllowAutoDropoffAtBase ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Выгрузка", "Auto-Dropoff", "回城卸货"),
                    st.AllowAutoDropoffAtBase ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoDropoffAtBase = !st.AllowAutoDropoffAtBase;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + (tw + 5f) * 4f, cy, tw, 24f),
                    (st.AllowBaseChores ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Быт/Костёр", "Camp Chores", "营地内务"),
                    st.AllowBaseChores ? _catOnStyle : _catOffStyle))
                {
                    st.AllowBaseChores = !st.AllowBaseChores;
                    CompanionConfigStore.Save();
                }

                // Row 4: Direct Companion Orders
                cy += 30f;
                if (GUI.Button(new Rect(cx, cy, 195f, 24f), CompanionConfigStore.L(lang, " Сортировать базу", " Sort Base Chests", " 整理基地储物箱"), _btnStyle))
                    BaseSortingSystem.CommandSortBase(ag, null, instant: false);

                if (GUI.Button(new Rect(cx + 202f, cy, 195f, 24f), CompanionConfigStore.L(lang, " Разгрузить багажник", " Unload Car Trunk", " 卸载汽车后备箱"), _btnStyle))
                    BaseChoreSystem.CommandUnloadVehiclesToBase(ag, silentIfEmpty: false);

                if (GUI.Button(new Rect(cx + 404f, cy, 195f, 24f), CompanionConfigStore.L(lang, " Байка у костра", " Campfire Story", " 篝火旁闲聊"), _btnStyle))
                    SmartChatterSystem.Speak(ag, ChatterTopic.CampfireStory, minCooldown: 0.1f, bubbleDuration: 6.5f, force: true);

                if (GUI.Button(new Rect(cx + 606f, cy, 195f, 24f), CompanionConfigStore.L(lang, " Боевой клич [F9]", " Battle Cry [F9]", " 战吼突击 [F9]"), _btnStyle))
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
            finally { EndScrollArea(); }
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
                if (GUI.Button(new Rect(area.x + i * (bw + 5f), area.y, bw, area.height), d.Item2, active ? _catOnStyle : _catOffStyle))
                {
                    st.Doctrine = d.Item1;
                    SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                    MultiplayerSyncSystem.SyncCompanionStateChange(st);
                    CompanionConfigStore.Save();
                }
            }
        }

        private static void DrawTabBaseBoxes(Rect area, ModLanguage lang)
        {
            var containers = BaseSortingSystem.ScanNearbyContainers(force: false);

            // Top action bar
            GUI.Box(new Rect(area.x, area.y, area.width, 42f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 8f;

            if (GUI.Button(new Rect(bx, by, 175f, 26f),
                CompanionConfigStore.L(lang, " Обновить ящики базы", " Rescan Base Boxes", " 重新扫描周围箱子"), _btnStyle))
                containers = BaseSortingSystem.ScanNearbyContainers(force: true);

            if (GUI.Button(new Rect(bx + 182f, by, 240f, 26f),
                CompanionConfigStore.L(lang, " Авто-разметка по содержимому", " Auto-Detect Categories", " 按箱内物品自动识别分类"), _btnStyle))
            {
                BaseSortingSystem.AutoDetectCategoriesFromContents();
            }

            if (GUI.Button(new Rect(bx + 428f, by, 215f, 26f),
                CompanionConfigStore.L(lang, " Приказ NPC: Сортировать базу", " Order NPCs: Sort Base", " 命令队友：整理基地仓库"), _catOnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            }

            if (GUI.Button(new Rect(bx + 649f, by, 190f, 26f),
                CompanionConfigStore.L(lang, " Мгновенно разложить всё", " Instant Sort All Now", " 立即一键整理全部"), _btnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, null, instant: true);
            }

            float bodyY = area.y + 48f;
            float bodyH = area.height - 48f;

            // Left column: container list; Right column: selected container's category editor
            float leftW = 360f;
            float rightW = area.width - leftW - 10f;

            GUI.Box(new Rect(area.x, bodyY, leftW, bodyH), GUIContent.none, _boxStyle);
            var viewRect = new Rect(0, 0, leftW - 20f, Mathf.Max(bodyH - 10f, containers.Count * 48f));
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
                string summary = ItemCategorizer.FormatCategoriesSummary(c.Rule.Categories, c.Rule.IsDropoffSource, lang);
                string unit = CompanionConfigStore.L(lang, "шт.", "items", "件");
                string mUnit = CompanionConfigStore.L(lang, "м", "m", "米");
                string coordTag = $"[{Mathf.RoundToInt(c.WorldPos.x)},{Mathf.RoundToInt(c.WorldPos.y)}]";
                string btnText = $"{c.DisplayName} {coordTag} ({c.DistanceToPlayer:F1}{mUnit} | {c.ItemCount} {unit})\n{summary}";

                if (GUI.Button(new Rect(4f, i * 48f, viewRect.width - 8f, 44f), btnText, isSel ? _catOnStyle : _btnStyle))
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
                float ry = rightRect.y + 10f;
                float rw = rightRect.width - 24f;

                GUI.Label(new Rect(rx, ry, rw, 24f),
                    CompanionConfigStore.L(lang,
                        $"Настройка ящика: {selectedContainer.DisplayName} (предметов внутри: {selectedContainer.ItemCount})",
                        $"Configure Chest: {selectedContainer.DisplayName} (items: {selectedContainer.ItemCount})",
                        $"配置储物箱：{selectedContainer.DisplayName}（当前箱内物品：{selectedContainer.ItemCount} 件）"),
                    _headerStyle);

                ry += 30f;
                string dropText = (rule.IsDropoffSource ? "[x] " : "[ ] ") +
                    CompanionConfigStore.L(lang,
                        " Ящик-приёмник (NPC забирают хабар ОТСЮДА и разносят по базе)",
                        " Drop-off Chest (NPCs take items FROM here to sort)",
                        " 收货中转箱（队友会从此处取出战利品并分拣到各个分类箱）");
                if (GUI.Button(new Rect(rx, ry, rw, 28f), dropText, rule.IsDropoffSource ? _catOnStyle : _catOffStyle))
                {
                    rule.IsDropoffSource = !rule.IsDropoffSource;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }

                ry += 34f;
                if (GUI.Button(new Rect(rx, ry, rw * 0.48f, 26f),
                    CompanionConfigStore.L(lang,
                        $"Приоритет укладки: *{rule.Priority} (сменить)",
                        $"Fill Priority: *{rule.Priority} (cycle)",
                        $"存放优先级：*{rule.Priority}（点击切换）"), _btnStyle))
                {
                    rule.Priority = (rule.Priority % 5) + 1;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + rw * 0.50f, ry, rw * 0.50f, 26f),
                    (rule.AutoCompact ? "[x] " : "[ ] ") + CompanionConfigStore.L(lang, "Авто-уплотнение сетки", "Auto-Compact Grid", "整理后自动紧凑排列"),
                    rule.AutoCompact ? _catOnStyle : _catOffStyle))
                {
                    rule.AutoCompact = !rule.AutoCompact;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }

                ry += 34f;
                GUI.Label(new Rect(rx, ry, rw, 22f),
                    CompanionConfigStore.L(lang,
                        "Отметьте категории предметов, которые NPC должны складывать в этот ящик:",
                        "Select item categories that NPCs should store in this chest:",
                        "勾选队友应当存入此储物箱的物品分类："),
                    _subHeaderStyle);

                ry += 26f;
                var cats = ItemCategorizer.AllCategories;
                int cols = 2;
                float cellW = (rw - 8f) / cols;
                float cellH = 28f;
                for (int i = 0; i < cats.Length; i++)
                {
                    var cat = cats[i];
                    int rIdx = i / cols;
                    int cIdx = i % cols;
                    bool on = (rule.Categories & cat) != 0;
                    string label = (on ? "[x] " : "[ ] ") + ItemCategorizer.GetCategoryName(cat, lang);
                    if (GUI.Button(new Rect(rx + cIdx * (cellW + 8f), ry + rIdx * 32f, cellW, cellH), label, on ? _catOnStyle : _catOffStyle))
                    {
                        if (on) rule.Categories &= ~cat;
                        else rule.Categories |= cat;
                        MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                        CompanionConfigStore.Save();
                    }
                }

                ry += 6 * 32f + 8f;
                float pw = (rw - 12f) / 3f;
                if (GUI.Button(new Rect(rx, ry, pw, 26f), CompanionConfigStore.L(lang, "Выбрать все", "Select All", "全选分类"), _btnStyle))
                {
                    rule.Categories = StorageCategory.All;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + pw + 6f, ry, pw, 26f), CompanionConfigStore.L(lang, "Очистить все", "Clear All", "清空全选"), _btnStyle))
                {
                    rule.Categories = StorageCategory.None;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule, selectedContainer.TerrainObj);
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + (pw + 6f) * 2f, ry, pw, 26f),
                    CompanionConfigStore.L(lang, "Разобрать этот ящик", "Sort From This Box", "整理此箱物品"), _catOnStyle))
                {
                    BaseSortingSystem.CommandSortBase(null, selectedContainer.Inventory, instant: false);
                }
            }
        }

        private static void DrawTabChatterAndSettings(Rect area, ModLanguage lang)
        {
            float leftW = 420f;
            float rightW = area.width - leftW - 10f;

            GUI.Box(new Rect(area.x, area.y, leftW, area.height), GUIContent.none, _boxStyle);
            BeginScrollArea(new Rect(area.x + 2f, area.y + 2f, leftW - 4f, area.height - 4f),
                ref _scrollSettings, new Rect(0f, 0f, leftW - 24f, 560f), 4);
            try
            {
            float lx = 8f;
            float ly = 8f;
            float lw = leftW - 40f;

            // Language selector row (4 modes: Auto, RU, EN, ZH)
            GUI.Label(new Rect(lx, ly, lw, 20f),
                CompanionConfigStore.L(lang,
                    " Язык мода (Автоопределение или выбор):",
                    " Mod Language (Auto-detect or manual):",
                    " 模组语言（自动识别或手动选择）："), _subHeaderStyle);
            ly += 22f;
            float langBtnW = (lw - 12f) / 4f;
            if (GUI.Button(new Rect(lx, ly, langBtnW, 24f),
                CompanionConfigStore.L(lang, " Авто", " Auto", " 自动"),
                CompanionConfigStore.LanguageMode == ModLanguage.Auto ? _catOnStyle : _catOffStyle))
                SetLanguageMode(ModLanguage.Auto);
            if (GUI.Button(new Rect(lx + langBtnW + 4f, ly, langBtnW, 24f), " Русский",
                CompanionConfigStore.LanguageMode == ModLanguage.Russian ? _catOnStyle : _catOffStyle))
                SetLanguageMode(ModLanguage.Russian);
            if (GUI.Button(new Rect(lx + (langBtnW + 4f) * 2f, ly, langBtnW, 24f), " English",
                CompanionConfigStore.LanguageMode == ModLanguage.English ? _catOnStyle : _catOffStyle))
                SetLanguageMode(ModLanguage.English);
            if (GUI.Button(new Rect(lx + (langBtnW + 4f) * 3f, ly, langBtnW, 24f), " 简体中文",
                CompanionConfigStore.LanguageMode == ModLanguage.Chinese ? _catOnStyle : _catOffStyle))
                SetLanguageMode(ModLanguage.Chinese);
            ly += 27f;

            GUI.Label(new Rect(lx, ly, lw, 20f),
                CompanionConfigStore.L(lang, " Режим лексики и характера NPC:", " NPC Profanity & Attitude Mode:", " 队友台词风格与脏话模式："), _subHeaderStyle);
            ly += 22f;

            float mw = (lw - 10f) / 3f;
            if (GUI.Button(new Rect(lx, ly, mw, 24f),
                CompanionConfigStore.L(lang, " Мат 18+ (Быдло)", " Hardcore 18+", " 末日粗口 18+"),
                CompanionConfigStore.Profanity == ProfanityMode.HardcoreRude ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.HardcoreRude;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + mw + 5f, ly, mw, 24f),
                CompanionConfigStore.L(lang, " Смешанный", " Mixed", " 混合模式"),
                CompanionConfigStore.Profanity == ProfanityMode.Mixed ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Mixed;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + (mw + 5f) * 2f, ly, mw, 24f),
                CompanionConfigStore.L(lang, " Без мата", " Clean", " 纯净无脏话"),
                CompanionConfigStore.Profanity == ProfanityMode.Clean ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Clean;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableSmartCombat ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Умный боевой ИИ, радар тыла и авто-клин", "Smart Combat AI, Flank Radar & Auto-Unjam", "智能战斗 AI、背后偷袭雷达与自动排障"),
                CompanionConfigStore.EnableSmartCombat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableSmartCombat = !CompanionConfigStore.EnableSmartCombat;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableKiting ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Тактический кайтинг (отход при стрельбе вблизи)", "Tactical Kiting (Backpedal while firing)", "战术风筝走位（近身边退边打）"),
                CompanionConfigStore.EnableKiting ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableKiting = !CompanionConfigStore.EnableKiting;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableAntiFriendlyFire ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Анти-Френдли-Фаер (выход с линии огня игрока)", "Anti-Friendly-Fire (Step out of player aim line)", "防误伤让枪线（自动闪开玩家射界）"),
                CompanionConfigStore.EnableAntiFriendlyFire ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableAntiFriendlyFire = !CompanionConfigStore.EnableAntiFriendlyFire;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableFieldMedic ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Полевой медик и снабженец патронами", "Field Medic & Emergency Ammo Supplier", "战地急救包扎与紧急弹药支援"),
                CompanionConfigStore.EnableFieldMedic ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableFieldMedic = !CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.EnableAmmoShare = CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.AutoShareFoodOnEat ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Авто-делёжка пайка при еде (Бафф «Сытый и злой»)", "Auto-Share Food When Eating ('Fed & Fierce' Buff)", "进食自动分口粮（触发「饱腹狂暴」增益）"),
                CompanionConfigStore.AutoShareFoodOnEat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AutoShareFoodOnEat = !CompanionConfigStore.AutoShareFoodOnEat;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableBaseAutoDropoff ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Авто-выгрузка хабара на базе и реплики мира", "Base Auto-Dropoff & World NPC Chatter", "回基地自动卸货与野生 NPC 路人台词"),
                CompanionConfigStore.EnableBaseAutoDropoff ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableBaseAutoDropoff = !CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.EnableWorldNpcChatter = CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.Save();
            }
            ly += 26f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableBaseChores ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Живой лагерь (костёр, огород, чистка оружия, багажник)", "Camp Chores (Campfire, Crops, Gear Repair & Trunk)", "营地内务（篝火取暖、种菜浇水、擦枪修甲与卸车）"),
                CompanionConfigStore.EnableBaseChores ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableBaseChores = !CompanionConfigStore.EnableBaseChores;
                CompanionConfigStore.Save();
            }
            ly += 25f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableSmartFlashlight ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Умный фонарь (вкл. ночью / гашение в стелсе) и Реанимация", "Smart Flashlight (Night/Stealth) & Emergency Rescue", "智能手电筒（夜间开启/潜行熄灯）与濒死强心抢救"),
                CompanionConfigStore.EnableSmartFlashlight ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableSmartFlashlight = !CompanionConfigStore.EnableSmartFlashlight;
                CompanionConfigStore.EnablePlayerRescue = CompanionConfigStore.EnableSmartFlashlight;
                CompanionConfigStore.Save();
            }
            ly += 25f;

            if (GUI.Button(new Rect(lx, ly, lw, 24f),
                (CompanionConfigStore.EnableChatter ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Живые комментарии и фразы напарников (60 тем)", "Companion Context Chatter & Reactions (60 Topics)", "开启队友实时情景吐槽与互动气泡（60大主题）"),
                CompanionConfigStore.EnableChatter ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableChatter = !CompanionConfigStore.EnableChatter;
                CompanionConfigStore.Save();
            }
            ly += 25f;

            GUI.Label(new Rect(lx, ly, lw, 20f),
                CompanionConfigStore.L(lang, " Проверить реплику напарника прямо сейчас:", " Test Companion Speech Now:", " 立即测试队友气泡台词："), _subHeaderStyle);
            ly += 22f;

            float hw = (lw - 6f) * 0.5f;
            var player = GameController.instance?.playerCharacter;
            var nearest = Il2CppSafe.IsAliveUnity(player)
                ? SmartChatterSystem.PickNearestTeammate(player.transform.position, 50f)
                : null;
            if (GUI.Button(new Rect(lx, ly, hw, 24f),
                CompanionConfigStore.L(lang, " Стейк (Наезд/Зависть)", " Steak Envy", " 偷吃牛排吐槽"), _catOnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.PlayerEatSteakMeat, 0.1f, 6.5f, true);

            if (GUI.Button(new Rect(lx + hw + 6f, ly, hw, 24f),
                CompanionConfigStore.L(lang, " Подогреть братву [F6]", " Share Ration [F6]", " 分享口粮 [F6]"), _catOnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(nearest, fromAutoShare: false);

            ly += 26f;
            if (GUI.Button(new Rect(lx, ly, hw, 24f),
                CompanionConfigStore.L(lang, " Орда / Замес (Мат)", " Horde Callout", " 遭遇尸潮喊话"), _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.CombatSpotHorde, 0.1f, 5.5f, true);

            if (GUI.Button(new Rect(lx + hw + 6f, ly, hw, 24f),
                CompanionConfigStore.L(lang, " Перегруз (Плюшкин)", " Overweight Rant", " 背包超重吐槽"), _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.LootOverweight, 0.1f, 5.5f, true);

            }
            finally { EndScrollArea(); }

            // Right column: Recent Speech Log
            var rightRect = new Rect(area.x + leftW + 10f, area.y, rightW, area.height);
            GUI.Box(rightRect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rightRect.x + 10f, rightRect.y + 10f, rightW - 20f, 24f),
                CompanionConfigStore.L(lang, " Журнал последних реплик напарников:", " Recent Companion Speech Log:", " 最近队友台词记录："), _headerStyle);

            var log = SmartChatterSystem.RecentSpeechLog;
            var logView = new Rect(0, 0, rightW - 28f, Mathf.Max(area.height - 50f, log.Count * 36f));
            BeginScrollArea(new Rect(rightRect.x + 8f, rightRect.y + 38f, rightW - 16f, area.height - 46f), ref _scrollChatter, logView, 3);
            try
            {
            for (int i = 0; i < log.Count; i++)
            {
                GUI.Label(new Rect(4f, i * 36f, logView.width - 8f, 34f), log[i], _labelStyle);
            }
            }
            finally { EndScrollArea(); }
        }
    }
}
