using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 2.0.0–2.2.0: the "Work" tab - base work board, priorities, loot zones, squad profile preview & reserves.
    public static partial class SmartCompanionsUI
    {
        private static Vector2 _scrollWork;
        internal static CompanionSpecialization PreviewSpec = CompanionSpecialization.Fighter;
        internal static bool ShowSquadProfilePreview = false;

        private static void DrawTabWork(Rect area, ModLanguage lang)
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            int busy = 0, carrying = 0, stopped = 0;
            var previewStates = new List<CompanionRuntimeState>(teammates.Count);
            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                var s = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                var kind = GetStatusKind(ag, s);
                if (kind == CompanionStatusKind.Busy) busy++;
                if (kind == CompanionStatusKind.Blocked) stopped++;
                if (s != null)
                {
                    carrying += s.CarryBuffer.Count;
                    if (!MultiplayerSyncSystem.IsMultiplayerOnline || MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData))
                        previewStates.Add(s);
                }
            }

            GUI.Box(new Rect(area.x, area.y, area.width, 114f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f, by = area.y + 6f;
            bool paused = BaseWorkManager.Paused;
            GUI.Label(new Rect(bx + 8f, by, area.width - 24f, 26f), paused
                ? CompanionConfigStore.L(lang, $"ПАУЗА  ·  заняты {busy}  ·  несут {carrying} предм.  ·  остановились {stopped}",
                    $"PAUSED  ·  busy {busy}  ·  carrying {carrying} items  ·  stopped {stopped}",
                    $"已暂停  ·  忙碌 {busy}  ·  携带 {carrying} 件  ·  已停止 {stopped}")
                : CompanionConfigStore.L(lang, $"Работа и снабжение  ·  заняты {busy}  ·  несут {carrying} предм.  ·  остановились {stopped}",
                    $"Work & supply  ·  busy {busy}  ·  carrying {carrying} items  ·  stopped {stopped}",
                    $"基地与补给  ·  忙碌 {busy}  ·  携带 {carrying} 件  ·  已停止 {stopped}"), _subHeaderStyle);

            by += 30f;
            float w4 = (area.width - 16f - 18f) / 4f;
            if (GUI.Button(new Rect(bx, by, w4, 28f), UiTip(paused
                    ? CompanionConfigStore.L(lang, "Продолжить", "Resume", "继续")
                    : CompanionConfigStore.L(lang, "Пауза", "Pause", "暂停"),
                    CompanionConfigStore.L(lang, "На паузе напарники не берут новую работу, несут вещи при себе и идут за вами",
                        "While paused, companions take no new work, keep carried items and follow you", "暂停时队友不接新工作，保留携带物品并跟随你")),
                paused ? _catOnStyle : _btnStyle))
                BaseWorkManager.SetPaused(!paused);
            if (GUI.Button(new Rect(bx + (w4 + 6f), by, w4, 28f), UiTip(
                    CompanionConfigStore.L(lang, "Отменить всё", "Cancel all", "全部取消"),
                    CompanionConfigStore.L(lang, "Все ваши напарники бросают текущую работу; вещи остаются у них в рюкзаке",
                        "All your companions drop their current job; items stay in their backpacks", "你的队友放弃当前工作，物品留在他们背包里")), _btnStyle))
                BaseWorkManager.CancelAll();
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 2f, by, w4, 28f),
                CompanionConfigStore.L(lang, "Сортировать [F7]", "Sort Base [F7]", "整理基地 [F7]"), _catOnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 3f, by, w4, 28f),
                CompanionConfigStore.L(lang, $"Собрать лут ({CompanionConfigStore.LootSweepRadius:F0}м)",
                    $"Sweep Loot ({CompanionConfigStore.LootSweepRadius:F0}m)",
                    $"搜刮物资 ({CompanionConfigStore.LootSweepRadius:F0}米)"), _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, CompanionConfigStore.LootSweepRadius);

            by += 34f;
            string prioLbl = CompanionProfileSystem.GetBasePriorityLabel(CompanionConfigStore.BasePriorityMode, lang);
            string zoneLbl = CompanionConfigStore.LootZonePinned
                ? CompanionConfigStore.L(lang, "Зона: точка", "Zone: Pinned", "区域：已固定")
                : CompanionConfigStore.L(lang, "Зона: за вами", "Zone: Player", "区域：跟随你");
            if (GUI.Button(new Rect(bx, by, w4, 28f), UiTip(
                CompanionConfigStore.L(lang, $"Очередь: {prioLbl}", $"Order: {prioLbl}", $"优先：{prioLbl}"),
                CompanionConfigStore.L(lang, "Порядок автоматических задач базы (Склад / Лут / Лагерь)",
                    "Priority order for automatic base jobs (Sort / Loot / Camp)", "自动基地工作的优先顺序")),
                CompanionConfigStore.BasePriorityMode != BaseWorkPriority.SortFirst ? _catOnStyle : _btnStyle))
            {
                CompanionProfileSystem.CycleBaseWorkPriority();
            }
            if (GUI.Button(new Rect(bx + (w4 + 6f), by, w4, 28f), UiTip(
                CompanionConfigStore.L(lang, $"Радиус лута: {CompanionConfigStore.LootSweepRadius:F0}м",
                    $"Loot radius: {CompanionConfigStore.LootSweepRadius:F0}m",
                    $"搜刮半径：{CompanionConfigStore.LootSweepRadius:F0}米"),
                CompanionConfigStore.L(lang, "Дальность поиска трофеев (14м / 24м / 36м / 48м)",
                    "Loot search radius (14m / 24m / 36m / 48m)", "物资搜刮半径（14/24/36/48米）")), _btnStyle))
            {
                CompanionProfileSystem.CycleLootSweepRadius();
            }
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 2f, by, w4, 28f), UiTip(zoneLbl,
                CompanionConfigStore.L(lang, "Закрепить зону сбора в вашей текущей точке или вернуть привязку к игроку (ящики базы всегда защищены)",
                    "Pin the scavenging zone at your feet or follow the player (marked base chests stay protected)",
                    "在当前位置固定搜刮区或恢复跟随玩家（基地箱子始终受保护）")),
                CompanionConfigStore.LootZonePinned ? _catOnStyle : _btnStyle))
            {
                CompanionProfileSystem.TogglePinnedLootZone();
            }
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 3f, by, w4, 28f), UiTip(
                CompanionConfigStore.L(lang, "Профиль отряда", "Squad Profile", "全队专精预览"),
                CompanionConfigStore.L(lang, "Открыть предпросмотр специализации перед применением ко всему отряду",
                    "Preview specialization changes before applying to the whole squad", "在应用至全队前预览专精变更")),
                ShowSquadProfilePreview ? _catOnStyle : _btnStyle))
            {
                ShowSquadProfilePreview = !ShowSquadProfilePreview;
            }

            float listY = area.y + 122f;
            if (ShowSquadProfilePreview)
            {
                DrawSquadProfilePreviewBox(new Rect(area.x, listY, area.width, 64f), previewStates, lang);
                listY += 72f;
            }
            float listH = area.yMax - listY;

            if (teammates.Count == 0)
            {
                GUI.Box(new Rect(area.x, listY, area.width, 70f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(area.x + 16f, listY + 14f, area.width - 32f, 44f), CompanionConfigStore.L(lang,
                    "Рядом нет напарников. Здесь настраиваются их специализации, неснижаемый резерв и очередь работ базы.",
                    "No companions nearby. Manage specializations, supply reserves and base work here.",
                    "附近没有队友。可在此管理专精职责、保底物资储备与基地工作。"), _labelStyle);
                return;
            }

            const float rowH = 66f;
            var view = new Rect(0, 0, area.width - 20f, teammates.Count * (rowH + 6f));
            bool baseEnabled = GUI.enabled;
            BeginScrollArea(new Rect(area.x, listY, area.width, listH), ref _scrollWork, view, 5);
            try
            {
                for (int i = 0; i < teammates.Count; i++)
                {
                    GUI.enabled = baseEnabled;
                    var ag = teammates[i];
                    if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                    try { DrawWorkRow(new Rect(0, i * (rowH + 6f), view.width, rowH), ag, lang, baseEnabled); }
                    catch (Exception error) { ReportUiFailure("Work row " + i, error); }
                }
            }
            finally { GUI.enabled = baseEnabled; EndScrollArea(); }
        }

        private static void DrawSquadProfilePreviewBox(Rect box, List<CompanionRuntimeState> states, ModLanguage lang)
        {
            GUI.Box(box, GUIContent.none, _boxStyle);
            float x = box.x + 8f, y = box.y + 6f, w = box.width - 16f;
            var specs = new[]
            {
                CompanionSpecialization.Fighter,
                CompanionSpecialization.Medic,
                CompanionSpecialization.Quartermaster,
                CompanionSpecialization.Scavenger
            };
            float btnW = (w - 5f * 5f) / 6f;
            for (int i = 0; i < specs.Length; i++)
            {
                var sp = specs[i];
                if (GUI.Button(new Rect(x + i * (btnW + 5f), y, btnW, 24f),
                    CompanionProfileSystem.GetSpecializationLabel(sp, lang),
                    PreviewSpec == sp ? _catOnStyle : _btnStyle))
                {
                    PreviewSpec = sp;
                }
            }
            if (GUI.Button(new Rect(x + 4f * (btnW + 5f), y, btnW * 2f + 5f, 24f),
                CompanionConfigStore.L(lang, "Применить отряду", "Apply to Squad", "应用至全队"), _catOnStyle))
            {
                CompanionProfileSystem.ApplyPresetToSquad(PreviewSpec);
            }

            var preview = CompanionProfileSystem.BuildSquadPreview(PreviewSpec, states, lang);
            bool wrap = _labelStyle.wordWrap;
            _labelStyle.wordWrap = false;
            GUI.Label(new Rect(x + 2f, y + 30f, w - 4f, 24f), UiSafe(preview.SummaryText), _labelStyle);
            _labelStyle.wordWrap = wrap;
        }

        private static void DrawWorkRow(Rect r, GOAPAgent ag, ModLanguage lang, bool baseEnabled)
        {
            string name = ag.npcData.characterData?.characterName ?? "NPC";
            var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, name);
            bool canCommand = !MultiplayerSyncSystem.IsMultiplayerOnline || MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData);
            GUI.Box(r, GUIContent.none, _boxStyle);
            float x = r.x + 10f, y = r.y + 5f, w = r.width - 20f;

            var kind = GetStatusKind(ag, st);
            GUI.Label(new Rect(x, y, w * 0.22f, 24f), name, _subHeaderStyle);
            var prev = GUI.color;
            GUI.color = GetStatusColor(kind);
            bool wrap = _labelStyle.wordWrap;
            _labelStyle.wordWrap = false;
            GUI.Label(new Rect(x + w * 0.22f, y, w * 0.16f, 24f), "● " + GetStatusLabel(kind, lang), _labelStyle);
            _labelStyle.wordWrap = wrap;
            GUI.color = prev;

            GUI.enabled = baseEnabled && canCommand;
            float rx = x + w * 0.38f;
            float rw = w * 0.62f;
            float gap = 4f;
            float cw = (rw - gap * 4f) / 5f;

            if (GUI.Button(new Rect(rx, y, cw, 24f), UiTip(
                CompanionProfileSystem.GetSpecializationLabel(st.Specialization, lang),
                CompanionConfigStore.L(lang, "Специализация: Боец / Медик / Снабженец / Сборщик",
                    "Specialization: Fighter / Medic / Supplier / Scavenger", "专精：战斗员/医疗兵/军需官/拾荒者")),
                st.Specialization != CompanionSpecialization.Custom ? _catOnStyle : _btnStyle))
            {
                CompanionProfileSystem.CycleCompanionSpecialization(ag, st);
            }
            if (GUI.Button(new Rect(rx + (cw + gap) * 1f, y, cw, 24f),
                UiTip(BaseWorkManager.GetRoleLabel(st.WorkRole, lang), BaseWorkManager.GetRoleTip(st.WorkRole, lang)), _btnStyle))
            {
                st.Specialization = CompanionSpecialization.Custom;
                BaseWorkManager.CycleRole(st);
            }
            if (GUI.Button(new Rect(rx + (cw + gap) * 2f, y, cw, 24f), UiTip(
                CompanionProfileSystem.GetLoadoutLabel(st.Loadout, lang),
                CompanionConfigStore.L(lang, "Предпочтительное вооружение (Авто / Стрелок / Ближний бой)",
                    "Preferred weapon loadout (Auto / Ranged / Melee)", "偏好武器配置（自动/远程/近战）")),
                st.Loadout != PreferredLoadout.Auto ? _catOnStyle : _btnStyle))
            {
                CompanionProfileSystem.CycleCompanionLoadout(ag, st);
            }
            if (GUI.Button(new Rect(rx + (cw + gap) * 3f, y, cw, 24f), UiTip(
                $"{st.ReserveAmmoMin}/{st.ReserveFoodMin}/{st.ReserveMedMin}",
                CompanionConfigStore.L(lang, "Неснижаемый резерв в рюкзаке: Патроны / Еда / Медицина (не выгружается при сортировке)",
                    "Non-reducible backpack reserve: Ammo / Food / Medicine (kept during base sorting)",
                    "背包保底储备：弹药/食物/医疗（整理基地时不会卸出）")),
                st.ReserveAmmoMin > 0 ? _catOnStyle : _catOffStyle))
            {
                CompanionProfileSystem.CycleCompanionReserves(st);
            }
            if (GUI.Button(new Rect(rx + (cw + gap) * 4f, y, cw, 24f), UiTip(
                CompanionConfigStore.L(lang, "Отменить", "Cancel", "取消"),
                CompanionConfigStore.L(lang, "Бросить текущую работу; вещи останутся в рюкзаке",
                    "Drop the current job; items stay in the backpack", "放弃当前工作，物品留在背包里")),
                st.HasAnyPhysicalTask ? _catOnStyle : _btnStyle) && st.HasAnyPhysicalTask)
            {
                BaseWorkManager.CancelWork(ag, st);
            }
            GUI.enabled = baseEnabled;

            y += 30f;
            string detail;
            if (kind == CompanionStatusKind.Blocked) detail = DescribeBlockReason(st, lang);
            else if (st.HasAnyPhysicalTask && BaseWorkManager.Paused)
                detail = CompanionConfigStore.L(lang, "Ждёт конца паузы: ", "Waiting for resume: ", "等待继续：") + st.CurrentTaskText;
            else if (st.HasAnyPhysicalTask) detail = st.CurrentTaskText;
            else detail = CompanionConfigStore.L(lang,
                $"Свободен  ·  резерв БК {st.ReserveAmmoMin} · еда {st.ReserveFoodMin} · мед. {st.ReserveMedMin}",
                $"Free  ·  reserve ammo {st.ReserveAmmoMin} · food {st.ReserveFoodMin} · med {st.ReserveMedMin}",
                $"空闲  ·  保底弹药 {st.ReserveAmmoMin} · 食物 {st.ReserveFoodMin} · 医疗 {st.ReserveMedMin}");
            if (st.CarryBuffer.Count > 0)
                detail += CompanionConfigStore.L(lang, $"  ·  несёт {st.CarryBuffer.Count} предм.", $"  ·  carrying {st.CarryBuffer.Count}", $"  ·  携带 {st.CarryBuffer.Count} 件");
            if (st.IsLootingActive && st.LootQueue.Count > 0)
                detail += CompanionConfigStore.L(lang, $"  ·  осталось точек: {st.LootQueue.Count}", $"  ·  stops left: {st.LootQueue.Count}", $"  ·  剩余地点：{st.LootQueue.Count}");
            GUI.Label(new Rect(x, y, w, 26f), UiSafe(detail), _labelStyle);
        }

        private static float DrawRareSupplySettings(float x, float y, float width, ModLanguage lang)
        {
            float w3 = (width - 12f) / 3f;
            if (GUI.Button(new Rect(x, y, w3, 30f), UiTip(
                (CompanionConfigStore.AllowSpendRareFood ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Тратить редкие блюда", "Spend rare dishes", "允许消耗高级料理"),
                CompanionConfigStore.L(lang, "Разрешить тратить стейки и редкую кулинарию при раздаче пайка (если обычной еды нет)",
                    "Allow sharing gourmet steaks & dishes when ordinary rations run out", "普通口粮耗尽时允许分享牛排等高级料理")),
                CompanionConfigStore.AllowSpendRareFood ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AllowSpendRareFood = !CompanionConfigStore.AllowSpendRareFood;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(x + w3 + 6f, y, w3, 30f), UiTip(
                (CompanionConfigStore.AllowSpendRareMeds ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Тратить сыворотки/дозы", "Spend rare serums", "允许消耗稀有药剂"),
                CompanionConfigStore.L(lang, "Разрешить полевому медику тратить редкие сыворотки и пакеты крови на обычное лечение",
                    "Allow field medics to use rare serums & blood bags for routine healing", "允许医疗兵在常规治疗中使用血清与血袋")),
                CompanionConfigStore.AllowSpendRareMeds ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AllowSpendRareMeds = !CompanionConfigStore.AllowSpendRareMeds;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(x + (w3 + 6f) * 2f, y, w3, 30f), UiTip(
                (CompanionConfigStore.AllowSpendRareAmmo ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, "Делиться редким БК", "Share rare calibers", "允许分享稀有弹药"),
                CompanionConfigStore.L(lang, "Разрешить экстренную подачу редких крупнокалиберных патронов в бою",
                    "Allow emergency ammo supply for rare small-stack calibers", "允许在战斗中紧急支援小堆叠大口径弹药")),
                CompanionConfigStore.AllowSpendRareAmmo ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.AllowSpendRareAmmo = !CompanionConfigStore.AllowSpendRareAmmo;
                CompanionConfigStore.Save();
            }
            return y + 36f;
        }
    }
}
