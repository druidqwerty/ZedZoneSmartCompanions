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
        private static void DrawMainHubWindow()
        {
            bool ru = CompanionConfigStore.UseRussian;
            var r = _mainWinRect;
            GUI.Box(r, GUIContent.none, _winStyle);

            float x = r.x + 14f;
            float y = r.y + 10f;
            float w = r.width - 28f;

            // Title bar
            GUI.Label(new Rect(x, y, w - 160f, 28f),
                ru ? "🔧 ZED ZONE — УМНЫЕ НАПАРНИКИ И ЛОГИСТИКА БАЗЫ [F8]" : "🔧 ZED ZONE — SMART COMPANIONS & BASE LOGISTICS [F8]",
                _headerStyle);

            if (GUI.Button(new Rect(r.xMax - 140f, y, 54f, 26f), ru ? "RU/EN" : "EN/RU", _btnStyle))
            {
                CompanionConfigStore.UseRussian = !CompanionConfigStore.UseRussian;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(r.xMax - 80f, y, 66f, 26f), ru ? "Закрыть" : "Close", _btnStyle))
            {
                ShowMainWindow = false;
                return;
            }

            y += 34f;
            // Tabs
            float tabW = (w - 16f) / 3f;
            if (GUI.Button(new Rect(x, y, tabW, 30f),
                ru ? "👥 1. Напарники и Боевой ИИ" : "👥 1. Companions & Combat AI",
                _activeTab == 0 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 0;
            }
            if (GUI.Button(new Rect(x + tabW + 8f, y, tabW, 30f),
                ru ? "📦 2. Ящики базы и Сортировка" : "📦 2. Base Containers & Sorting",
                _activeTab == 1 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 1;
                BaseSortingSystem.ScanNearbyContainers(force: true);
            }
            if (GUI.Button(new Rect(x + (tabW + 8f) * 2f, y, tabW, 30f),
                ru ? "💬 3. Фразы, Реакции и Настройки" : "💬 3. Chatter, Reactions & Settings",
                _activeTab == 2 ? _catOnStyle : _btnStyle))
            {
                _activeTab = 2;
            }

            y += 38f;
            var contentRect = new Rect(x, y, w, r.height - (y - r.y) - 14f);

            if (_activeTab == 0)
                DrawTabSquad(contentRect, ru);
            else if (_activeTab == 1)
                DrawTabBaseBoxes(contentRect, ru);
            else
                DrawTabChatterAndSettings(contentRect, ru);

            if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) &&
                r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }
        }

        private static void DrawTabSquad(Rect area, bool ru)
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();

            // Squad-wide quick command row
            GUI.Box(new Rect(area.x, area.y, area.width, 42f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 8f;
            GUI.Label(new Rect(bx, by, 175f, 26f),
                ru ? $"Отряд активен: {teammates.Count} чел." : $"Active Squad: {teammates.Count}", _subHeaderStyle);

            if (GUI.Button(new Rect(bx + 180f, by, 140f, 26f), ru ? "📦 Сортировать [F7]" : "📦 Sort Base [F7]", _catOnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: false);

            if (GUI.Button(new Rect(bx + 325f, by, 130f, 26f), ru ? "⚡ Мгновенно" : "⚡ Instant Sort", _btnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: true);

            if (GUI.Button(new Rect(bx + 460f, by, 125f, 26f), ru ? "🎒 Собрать лут" : "🎒 Sweep Loot", _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, 20f);

            if (GUI.Button(new Rect(bx + 590f, by, 130f, 26f), ru ? "🍖 Подогреть [F6]" : "🍖 Share Food [F6]", _catOnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(null, fromAutoShare: false);

            if (GUI.Button(new Rect(bx + 725f, by, 115f, 26f), ru ? "💉 Медпомощь" : "💉 Field Aid", _btnStyle))
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
                    ru
                        ? "У вас сейчас нет активных напарников рядом. Наймите выжившего в лагере или по рации — здесь появятся его тактические доктрины, управление умным боем, приказ на сортировку базы и живые реакции!"
                        : "No active companions nearby. Recruit a survivor in camp or via radio to manage their tactical doctrines, base sorting orders, and live reactions here!",
                    _labelStyle);
                return;
            }

            float cardH = 128f;
            var viewRect = new Rect(0, 0, area.width - 20f, teammates.Count * (cardH + 8f));
            _scrollSquad = GUI.BeginScrollView(new Rect(area.x, listY, area.width, listH), _scrollSquad, viewRect);

            for (int i = 0; i < teammates.Count; i++)
            {
                var ag = teammates[i];
                if (ag == null || ag.npcData == null) continue;
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
                string wepName = cd?.characterEquipmentData?.currentWeaponItemData?.GetItemName() ?? (ru ? "Без оружия" : "Unarmed");

                GUI.Label(new Rect(cx, cy, 420f, 22f),
                    $"★ {name}  |  HP: {hp:F0}/{maxHp:F0}  |  {(ru ? "Сытость" : "Food")}: {hunger:F0}%  |  {(ru ? "Вода" : "Water")}: {thirst:F0}%  |  🔫 {wepName}",
                    _subHeaderStyle);

                GUI.Label(new Rect(cx + 430f, cy, viewRect.width - 440f, 22f),
                    ru
                        ? $"Задача: {st.CurrentTaskText} | Убито: {st.KillsThisSession} | Сорт.: {st.SortedItemsThisSession}"
                        : $"Task: {st.CurrentTaskText} | Kills: {st.KillsThisSession} | Sorted: {st.SortedItemsThisSession}",
                    _labelStyle);

                // Row 2: Doctrines
                cy += 26f;
                GUI.Label(new Rect(cx, cy, 90f, 24f), ru ? "Доктрина:" : "Doctrine:", _labelStyle);
                DrawDoctrineButtons(new Rect(cx + 85f, cy, viewRect.width - 95f, 24f), ag, st, ru);

                // Row 3: Per-companion feature toggles
                cy += 30f;
                if (GUI.Button(new Rect(cx, cy, 170f, 24f),
                    (st.AllowFieldMedic ? "☑ " : "☐ ") + (ru ? "Полевой медик" : "Field Medic"),
                    st.AllowFieldMedic ? _catOnStyle : _catOffStyle))
                {
                    st.AllowFieldMedic = !st.AllowFieldMedic;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + 176f, cy, 175f, 24f),
                    (st.AllowAmmoShare ? "☑ " : "☐ ") + (ru ? "Подача патронов" : "Ammo Supplier"),
                    st.AllowAmmoShare ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAmmoShare = !st.AllowAmmoShare;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + 357f, cy, 180f, 24f),
                    (st.AllowAutoLootNearby ? "☑ " : "☐ ") + (ru ? "Авто-сбор трофеев" : "Auto-Loot Nearby"),
                    st.AllowAutoLootNearby ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoLootNearby = !st.AllowAutoLootNearby;
                    CompanionConfigStore.Save();
                }

                if (GUI.Button(new Rect(cx + 543f, cy, 210f, 24f),
                    (st.AllowAutoDropoffAtBase ? "☑ " : "☐ ") + (ru ? "Авто-выгрузка на базе" : "Base Auto-Dropoff"),
                    st.AllowAutoDropoffAtBase ? _catOnStyle : _catOffStyle))
                {
                    st.AllowAutoDropoffAtBase = !st.AllowAutoDropoffAtBase;
                    CompanionConfigStore.Save();
                }

                // Row 4: Direct Companion Orders
                cy += 30f;
                if (GUI.Button(new Rect(cx, cy, 195f, 24f), ru ? "📦 Сортировать базу" : "📦 Sort Base Chests", _btnStyle))
                    BaseSortingSystem.CommandSortBase(ag, null, instant: false);

                if (GUI.Button(new Rect(cx + 202f, cy, 195f, 24f), ru ? "🎒 Собрать трофеи" : "🎒 Sweep Area Loot", _btnStyle))
                    AutoLootSystem.CommandSweepNearbyLoot(ag, 20f);

                if (GUI.Button(new Rect(cx + 404f, cy, 195f, 24f), ru ? "🍖 Тест реакции на стейк" : "🍖 Test Steak Reaction", _btnStyle))
                    SmartChatterSystem.Speak(ag, ChatterTopic.PlayerEatSteakMeat, minCooldown: 0.1f, bubbleDuration: 6.5f, force: true);

                if (GUI.Button(new Rect(cx + 606f, cy, 195f, 24f), ru ? "💬 Доложить обстановку" : "💬 Status Callout", _btnStyle))
                    SmartChatterSystem.Speak(ag, ChatterTopic.AmbientBaseIdle, minCooldown: 0.1f, bubbleDuration: 5.5f, force: true);
            }

            GUI.EndScrollView();
        }

        private static void DrawDoctrineButtons(Rect area, GOAPAgent agent, CompanionRuntimeState st, bool ru)
        {
            var docs = new[]
            {
                (TacticalDoctrine.Balanced,     ru ? "⚖ Умный баланс" : "⚖ Balanced"),
                (TacticalDoctrine.Assault,      ru ? "⚔ Штурм" : "⚔ Assault"),
                (TacticalDoctrine.Bodyguard,    ru ? "🛡 Телохранитель" : "🛡 Bodyguard"),
                (TacticalDoctrine.SniperKite,   ru ? "🎯 Снайпер/Кайт" : "🎯 Sniper/Kite"),
                (TacticalDoctrine.StealthMelee, ru ? "🗡 Тихий клинок" : "🗡 Stealth Melee"),
                (TacticalDoctrine.Passive,      ru ? "🏳 Не стрелять" : "🏳 Passive")
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
                    CompanionConfigStore.Save();
                }
            }
        }

        private static void DrawTabBaseBoxes(Rect area, bool ru)
        {
            var containers = BaseSortingSystem.ScanNearbyContainers(force: false);

            // Top action bar
            GUI.Box(new Rect(area.x, area.y, area.width, 42f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f;
            float by = area.y + 8f;

            if (GUI.Button(new Rect(bx, by, 175f, 26f), ru ? "🔍 Обновить ящики базы" : "🔍 Rescan Base Boxes", _btnStyle))
                containers = BaseSortingSystem.ScanNearbyContainers(force: true);

            if (GUI.Button(new Rect(bx + 182f, by, 240f, 26f),
                ru ? "✨ Авто-разметка по содержимому" : "✨ Auto-Detect Categories", _btnStyle))
            {
                BaseSortingSystem.AutoDetectCategoriesFromContents();
            }

            if (GUI.Button(new Rect(bx + 428f, by, 215f, 26f),
                ru ? "🤖 Приказ NPC: Сортировать базу" : "🤖 Order NPCs: Sort Base", _catOnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            }

            if (GUI.Button(new Rect(bx + 649f, by, 190f, 26f),
                ru ? "⚡ Мгновенно разложить всё" : "⚡ Instant Sort All Now", _btnStyle))
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
            _scrollBoxes = GUI.BeginScrollView(new Rect(area.x + 4f, bodyY + 4f, leftW - 8f, bodyH - 8f), _scrollBoxes, viewRect);

            if (containers.Count == 0)
            {
                GUI.Label(new Rect(8f, 8f, leftW - 28f, 80f),
                    ru ? "Рядом не найдено контейнеров. Подойдите к ящикам на базе или откройте любой ящик." : "No containers found nearby. Walk near your base chests or open any container.",
                    _labelStyle);
            }

            DiscoveredContainer selectedContainer = null;
            for (int i = 0; i < containers.Count; i++)
            {
                var c = containers[i];
                if (string.IsNullOrEmpty(_selectedBoxIdInHub) && i == 0)
                    _selectedBoxIdInHub = c.ContainerId;
                if (c.ContainerId == _selectedBoxIdInHub)
                    selectedContainer = c;

                bool isSel = c.ContainerId == _selectedBoxIdInHub;
                string summary = ItemCategorizer.FormatCategoriesSummary(c.Rule.Categories, c.Rule.IsDropoffSource, ru);
                string btnText = $"{c.DisplayName} ({c.DistanceToPlayer:F1}м | {c.ItemCount} шт.)\n{summary}";

                if (GUI.Button(new Rect(4f, i * 48f, viewRect.width - 8f, 44f), btnText, isSel ? _catOnStyle : _btnStyle))
                {
                    _selectedBoxIdInHub = c.ContainerId;
                    selectedContainer = c;
                }
            }
            GUI.EndScrollView();

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
                    ru
                        ? $"Настройка ящика: {selectedContainer.DisplayName} (предметов внутри: {selectedContainer.ItemCount})"
                        : $"Configure Chest: {selectedContainer.DisplayName} (items: {selectedContainer.ItemCount})",
                    _headerStyle);

                ry += 30f;
                string dropText = (rule.IsDropoffSource ? "☑ " : "☐ ") +
                    (ru ? "📥 Ящик-приёмник (NPC забирают хабар ОТСЮДА и разносят по базе)" : "📥 Drop-off Chest (NPCs take items FROM here to sort)");
                if (GUI.Button(new Rect(rx, ry, rw, 28f), dropText, rule.IsDropoffSource ? _catOnStyle : _catOffStyle))
                {
                    rule.IsDropoffSource = !rule.IsDropoffSource;
                    CompanionConfigStore.Save();
                }

                ry += 34f;
                if (GUI.Button(new Rect(rx, ry, rw * 0.48f, 26f),
                    ru ? $"Приоритет укладки: ★{rule.Priority} (сменить)" : $"Fill Priority: ★{rule.Priority} (cycle)", _btnStyle))
                {
                    rule.Priority = (rule.Priority % 5) + 1;
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + rw * 0.50f, ry, rw * 0.50f, 26f),
                    (rule.AutoCompact ? "☑ " : "☐ ") + (ru ? "Авто-уплотнение сетки" : "Auto-Compact Grid"),
                    rule.AutoCompact ? _catOnStyle : _catOffStyle))
                {
                    rule.AutoCompact = !rule.AutoCompact;
                    CompanionConfigStore.Save();
                }

                ry += 34f;
                GUI.Label(new Rect(rx, ry, rw, 22f),
                    ru ? "Отметьте категории предметов, которые NPC должны складывать в этот ящик:" : "Select item categories that NPCs should store in this chest:",
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
                    string label = (on ? "☑ " : "☐ ") + ItemCategorizer.GetCategoryName(cat, ru);
                    if (GUI.Button(new Rect(rx + cIdx * (cellW + 8f), ry + rIdx * 32f, cellW, cellH), label, on ? _catOnStyle : _catOffStyle))
                    {
                        if (on) rule.Categories &= ~cat;
                        else rule.Categories |= cat;
                        CompanionConfigStore.Save();
                    }
                }

                ry += 6 * 32f + 8f;
                float pw = (rw - 12f) / 3f;
                if (GUI.Button(new Rect(rx, ry, pw, 26f), ru ? "Выбрать все" : "Select All", _btnStyle))
                {
                    rule.Categories = StorageCategory.All;
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + pw + 6f, ry, pw, 26f), ru ? "Очистить все" : "Clear All", _btnStyle))
                {
                    rule.Categories = StorageCategory.None;
                    CompanionConfigStore.Save();
                }
                if (GUI.Button(new Rect(rx + (pw + 6f) * 2f, ry, pw, 26f), ru ? "Разобрать этот ящик" : "Sort From This Box", _catOnStyle))
                {
                    BaseSortingSystem.CommandSortBase(null, selectedContainer.Inventory, instant: false);
                }
            }
        }

        private static void DrawTabChatterAndSettings(Rect area, bool ru)
        {
            float leftW = 410f;
            float rightW = area.width - leftW - 10f;

            GUI.Box(new Rect(area.x, area.y, leftW, area.height), GUIContent.none, _boxStyle);
            float lx = area.x + 10f;
            float ly = area.y + 10f;
            float lw = leftW - 20f;

            GUI.Label(new Rect(lx, ly, lw, 22f), ru ? "⚙ Режим лексики и характера NPC:" : "⚙ NPC Profanity & Attitude Mode:", _headerStyle);
            ly += 24f;

            float mw = (lw - 10f) / 3f;
            if (GUI.Button(new Rect(lx, ly, mw, 26f), ru ? "🔥 Мат 18+ (Быдло)" : "🔥 Hardcore 18+",
                CompanionConfigStore.Profanity == ProfanityMode.HardcoreRude ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.HardcoreRude;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + mw + 5f, ly, mw, 26f), ru ? "⚖ Смешанный" : "⚖ Mixed",
                CompanionConfigStore.Profanity == ProfanityMode.Mixed ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Mixed;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(lx + (mw + 5f) * 2f, ly, mw, 26f), ru ? "😇 Без мата" : "😇 Clean",
                CompanionConfigStore.Profanity == ProfanityMode.Clean ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.Profanity = ProfanityMode.Clean;
                CompanionConfigStore.Save();
            }
            ly += 30f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableSmartCombat ? "☑ " : "☐ ") + (ru ? "Умный боевой ИИ, радар тыла и авто-клин" : "Smart Combat AI, Flank Radar & Auto-Unjam"),
                CompanionConfigStore.EnableSmartCombat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableSmartCombat = !CompanionConfigStore.EnableSmartCombat;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableKiting ? "☑ " : "☐ ") + (ru ? "Тактический кайтинг (отход при стрельбе вблизи)" : "Tactical Kiting (Backpedal while firing)"),
                CompanionConfigStore.EnableKiting ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableKiting = !CompanionConfigStore.EnableKiting;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableAntiFriendlyFire ? "☑ " : "☐ ") + (ru ? "Анти-Френдли-Фаер (выход с линии огня игрока)" : "Anti-Friendly-Fire (Step out of player aim line)"),
                CompanionConfigStore.EnableAntiFriendlyFire ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableAntiFriendlyFire = !CompanionConfigStore.EnableAntiFriendlyFire;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableFieldMedic ? "☑ " : "☐ ") + (ru ? "Полевой медик и снабженец патронами" : "Field Medic & Emergency Ammo Supplier"),
                CompanionConfigStore.EnableFieldMedic ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableFieldMedic = !CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.EnableAmmoShare = CompanionConfigStore.EnableFieldMedic;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.AutoShareFoodOnEat ? "☑ " : "☐ ") + (ru ? "Авто-делёжка пайка при еде (Бафф «Сытый и злой»)" : "Auto-Share Food When Eating ('Fed & Fierce' Buff)"),
                CompanionConfigStore.AutoShareFoodOnEat ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AutoShareFoodOnEat = !CompanionConfigStore.AutoShareFoodOnEat;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableBaseAutoDropoff ? "☑ " : "☐ ") + (ru ? "Авто-выгрузка хабара на базе и реплики мира" : "Base Auto-Dropoff & World NPC Chatter"),
                CompanionConfigStore.EnableBaseAutoDropoff ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableBaseAutoDropoff = !CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.EnableWorldNpcChatter = CompanionConfigStore.EnableBaseAutoDropoff;
                CompanionConfigStore.Save();
            }
            ly += 27f;

            if (GUI.Button(new Rect(lx, ly, lw, 25f),
                (CompanionConfigStore.EnableChatter ? "☑ " : "☐ ") + (ru ? "Живые комментарии и фразы напарников" : "Companion Context Chatter & Reactions"),
                CompanionConfigStore.EnableChatter ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableChatter = !CompanionConfigStore.EnableChatter;
                CompanionConfigStore.Save();
            }
            ly += 30f;

            GUI.Label(new Rect(lx, ly, lw, 22f), ru ? "🎙 Проверить реплику напарника прямо сейчас:" : "🎙 Test Companion Speech Now:", _subHeaderStyle);
            ly += 24f;

            float hw = (lw - 6f) * 0.5f;
            var nearest = SmartChatterSystem.PickNearestTeammate(GameController.instance.playerCharacter.transform.position, 50f);
            if (GUI.Button(new Rect(lx, ly, hw, 25f), ru ? "🥩 Стейк (Наезд/Зависть)" : "🥩 Steak Envy", _catOnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.PlayerEatSteakMeat, 0.1f, 6.5f, true);

            if (GUI.Button(new Rect(lx + hw + 6f, ly, hw, 25f), ru ? "🍖 Подогреть братву [F6]" : "🍖 Share Ration [F6]", _catOnStyle))
                SmartChatterSystem.CommandShareRationWithSquad(nearest, fromAutoShare: false);

            ly += 28f;
            if (GUI.Button(new Rect(lx, ly, hw, 25f), ru ? "🧟 Орда / Замес (Мат)" : "🧟 Horde Callout", _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.CombatSpotHorde, 0.1f, 5.5f, true);

            if (GUI.Button(new Rect(lx + hw + 6f, ly, hw, 25f), ru ? "🎒 Перегруз (Плюшкин)" : "🎒 Overweight Rant", _btnStyle))
                if (nearest != null) SmartChatterSystem.Speak(nearest, ChatterTopic.LootOverweight, 0.1f, 5.5f, true);

            // Right column: Recent Speech Log
            var rightRect = new Rect(area.x + leftW + 10f, area.y, rightW, area.height);
            GUI.Box(rightRect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rightRect.x + 10f, rightRect.y + 10f, rightW - 20f, 24f),
                ru ? "📜 Журнал последних реплик напарников:" : "📜 Recent Companion Speech Log:", _headerStyle);

            var log = SmartChatterSystem.RecentSpeechLog;
            var logView = new Rect(0, 0, rightW - 28f, Mathf.Max(area.height - 50f, log.Count * 36f));
            _scrollChatter = GUI.BeginScrollView(new Rect(rightRect.x + 8f, rightRect.y + 38f, rightW - 16f, area.height - 46f), _scrollChatter, logView);
            for (int i = 0; i < log.Count; i++)
            {
                GUI.Label(new Rect(4f, i * 36f, logView.width - 8f, 34f), log[i], _labelStyle);
            }
            GUI.EndScrollView();
        }
    }
}
