using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 2.0.0: the "Work" tab - one board for who does what, roles, pause and cancel.
    public static partial class SmartCompanionsUI
    {
        private static Vector2 _scrollWork;

        private static void DrawTabWork(Rect area, ModLanguage lang)
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            int busy = 0, carrying = 0, stopped = 0;
            foreach (var ag in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                CompanionConfigStore.CompanionStates.TryGetValue(ag.npcData.npcGUID ?? "", out var s);
                var kind = GetStatusKind(ag, s);
                if (kind == CompanionStatusKind.Busy) busy++;
                if (kind == CompanionStatusKind.Blocked) stopped++;
                if (s != null) carrying += s.CarryBuffer.Count;
            }

            GUI.Box(new Rect(area.x, area.y, area.width, 80f), GUIContent.none, _boxStyle);
            float bx = area.x + 8f, by = area.y + 6f;
            bool paused = BaseWorkManager.Paused;
            GUI.Label(new Rect(bx + 8f, by, area.width - 24f, 28f), paused
                ? CompanionConfigStore.L(lang, $"\u041f\u0410\u0423\u0417\u0410  \u00b7  \u0437\u0430\u043d\u044f\u0442\u044b {busy}  \u00b7  \u043d\u0435\u0441\u0443\u0442 {carrying} \u043f\u0440\u0435\u0434\u043c.  \u00b7  \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u043b\u0438\u0441\u044c {stopped}",
                    $"PAUSED  \u00b7  busy {busy}  \u00b7  carrying {carrying} items  \u00b7  stopped {stopped}",
                    $"\u5df2\u6682\u505c  \u00b7  \u5fd9\u788c {busy}  \u00b7  \u643a\u5e26 {carrying} \u4ef6  \u00b7  \u5df2\u505c\u6b62 {stopped}")
                : CompanionConfigStore.L(lang, $"\u0420\u0430\u0431\u043e\u0442\u0430 \u0431\u0430\u0437\u044b  \u00b7  \u0437\u0430\u043d\u044f\u0442\u044b {busy}  \u00b7  \u043d\u0435\u0441\u0443\u0442 {carrying} \u043f\u0440\u0435\u0434\u043c.  \u00b7  \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u043b\u0438\u0441\u044c {stopped}",
                    $"Base work  \u00b7  busy {busy}  \u00b7  carrying {carrying} items  \u00b7  stopped {stopped}",
                    $"\u57fa\u5730\u5de5\u4f5c  \u00b7  \u5fd9\u788c {busy}  \u00b7  \u643a\u5e26 {carrying} \u4ef6  \u00b7  \u5df2\u505c\u6b62 {stopped}"), _subHeaderStyle);
            by += 34f;
            float w4 = (area.width - 16f - 18f) / 4f;
            if (GUI.Button(new Rect(bx, by, w4, 30f), UiTip(paused
                    ? CompanionConfigStore.L(lang, "\u041f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u0442\u044c", "Resume", "\u7ee7\u7eed")
                    : CompanionConfigStore.L(lang, "\u041f\u0430\u0443\u0437\u0430", "Pause", "\u6682\u505c"),
                    CompanionConfigStore.L(lang, "\u041d\u0430 \u043f\u0430\u0443\u0437\u0435 \u043d\u0430\u043f\u0430\u0440\u043d\u0438\u043a\u0438 \u043d\u0435 \u0431\u0435\u0440\u0443\u0442 \u043d\u043e\u0432\u0443\u044e \u0440\u0430\u0431\u043e\u0442\u0443, \u043d\u0435\u0441\u0443\u0442 \u0432\u0435\u0449\u0438 \u043f\u0440\u0438 \u0441\u0435\u0431\u0435 \u0438 \u0438\u0434\u0443\u0442 \u0437\u0430 \u0432\u0430\u043c\u0438",
                        "While paused, companions take no new work, keep carried items and follow you", "\u6682\u505c\u65f6\u961f\u53cb\u4e0d\u63a5\u65b0\u5de5\u4f5c\uff0c\u4fdd\u7559\u643a\u5e26\u7269\u54c1\u5e76\u8ddf\u968f\u4f60")),
                paused ? _catOnStyle : _btnStyle))
                BaseWorkManager.SetPaused(!paused);
            if (GUI.Button(new Rect(bx + (w4 + 6f), by, w4, 30f), UiTip(
                    CompanionConfigStore.L(lang, "\u041e\u0442\u043c\u0435\u043d\u0438\u0442\u044c \u0432\u0441\u0451", "Cancel all", "\u5168\u90e8\u53d6\u6d88"),
                    CompanionConfigStore.L(lang, "\u0412\u0441\u0435 \u0432\u0430\u0448\u0438 \u043d\u0430\u043f\u0430\u0440\u043d\u0438\u043a\u0438 \u0431\u0440\u043e\u0441\u0430\u044e\u0442 \u0442\u0435\u043a\u0443\u0449\u0443\u044e \u0440\u0430\u0431\u043e\u0442\u0443; \u0432\u0435\u0449\u0438 \u043e\u0441\u0442\u0430\u044e\u0442\u0441\u044f \u0443 \u043d\u0438\u0445 \u0432 \u0440\u044e\u043a\u0437\u0430\u043a\u0435",
                        "All your companions drop their current job; items stay in their backpacks", "\u4f60\u7684\u961f\u53cb\u653e\u5f03\u5f53\u524d\u5de5\u4f5c\uff0c\u7269\u54c1\u7559\u5728\u4ed6\u4eec\u80cc\u5305\u91cc")), _btnStyle))
                BaseWorkManager.CancelAll();
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 2f, by, w4, 30f),
                CompanionConfigStore.L(lang, "\u0421\u043e\u0440\u0442\u0438\u0440\u043e\u0432\u0430\u0442\u044c [F7]", "Sort Base [F7]", "\u6574\u7406\u57fa\u5730 [F7]"), _catOnStyle))
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            if (GUI.Button(new Rect(bx + (w4 + 6f) * 3f, by, w4, 30f),
                CompanionConfigStore.L(lang, "\u0421\u043e\u0431\u0440\u0430\u0442\u044c \u043b\u0443\u0442", "Sweep Loot", "\u641c\u522e\u7269\u8d44"), _btnStyle))
                AutoLootSystem.CommandSweepNearbyLoot(null, 24f);

            float listY = area.y + 90f, listH = area.height - 90f;
            if (teammates.Count == 0)
            {
                GUI.Box(new Rect(area.x, listY, area.width, 70f), GUIContent.none, _boxStyle);
                GUI.Label(new Rect(area.x + 16f, listY + 14f, area.width - 32f, 44f), CompanionConfigStore.L(lang,
                    "\u0420\u044f\u0434\u043e\u043c \u043d\u0435\u0442 \u043d\u0430\u043f\u0430\u0440\u043d\u0438\u043a\u043e\u0432. \u0417\u0434\u0435\u0441\u044c \u0431\u0443\u0434\u0435\u0442 \u0432\u0438\u0434\u043d\u043e, \u043a\u0442\u043e \u0447\u0442\u043e \u0434\u0435\u043b\u0430\u0435\u0442, \u043d\u0435\u0441\u0451\u0442 \u0438 \u043f\u043e\u0447\u0435\u043c\u0443 \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u043b\u0441\u044f.",
                    "No companions nearby. This board shows who does what, what they carry and why they stopped.",
                    "\u9644\u8fd1\u6ca1\u6709\u961f\u53cb\u3002\u8fd9\u91cc\u4f1a\u663e\u793a\u8c01\u5728\u505a\u4ec0\u4e48\u3001\u643a\u5e26\u4ec0\u4e48\u4ee5\u53ca\u4e3a\u4f55\u505c\u6b62\u3002"), _labelStyle);
                return;
            }

            const float rowH = 62f;
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

        private static void DrawWorkRow(Rect r, GOAPAgent ag, ModLanguage lang, bool baseEnabled)
        {
            string name = ag.npcData.characterData?.characterName ?? "NPC";
            var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, name);
            bool canCommand = !MultiplayerSyncSystem.IsMultiplayerOnline || MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData);
            GUI.Box(r, GUIContent.none, _boxStyle);
            float x = r.x + 10f, y = r.y + 5f, w = r.width - 20f;

            var kind = GetStatusKind(ag, st);
            GUI.Label(new Rect(x, y, w * 0.34f, 24f), name, _subHeaderStyle);
            var prev = GUI.color;
            GUI.color = GetStatusColor(kind);
            bool wrap = _labelStyle.wordWrap;
            _labelStyle.wordWrap = false;
            GUI.Label(new Rect(x + w * 0.34f, y, w * 0.24f, 24f), "\u25cf " + GetStatusLabel(kind, lang), _labelStyle);
            _labelStyle.wordWrap = wrap;
            GUI.color = prev;

            GUI.enabled = baseEnabled && canCommand;
            if (GUI.Button(new Rect(x + w * 0.58f, y, w * 0.24f, 24f),
                UiTip(BaseWorkManager.GetRoleLabel(st.WorkRole, lang), BaseWorkManager.GetRoleTip(st.WorkRole, lang) +
                    CompanionConfigStore.L(lang, " (\u043d\u0430\u0436\u043c\u0438\u0442\u0435, \u0447\u0442\u043e\u0431\u044b \u0441\u043c\u0435\u043d\u0438\u0442\u044c)", " (click to change)", "\uff08\u70b9\u51fb\u5207\u6362\uff09")), _btnStyle))
                BaseWorkManager.CycleRole(st);
            if (GUI.Button(new Rect(x + w * 0.83f, y, w * 0.17f, 24f), UiTip(
                    CompanionConfigStore.L(lang, "\u041e\u0442\u043c\u0435\u043d\u0438\u0442\u044c", "Cancel", "\u53d6\u6d88"),
                    CompanionConfigStore.L(lang, "\u0411\u0440\u043e\u0441\u0438\u0442\u044c \u0442\u0435\u043a\u0443\u0449\u0443\u044e \u0440\u0430\u0431\u043e\u0442\u0443; \u0432\u0435\u0449\u0438 \u043e\u0441\u0442\u0430\u043d\u0443\u0442\u0441\u044f \u0432 \u0440\u044e\u043a\u0437\u0430\u043a\u0435", "Drop the current job; items stay in the backpack", "\u653e\u5f03\u5f53\u524d\u5de5\u4f5c\uff0c\u7269\u54c1\u7559\u5728\u80cc\u5305\u91cc")),
                st.HasAnyPhysicalTask ? _catOnStyle : _btnStyle) && st.HasAnyPhysicalTask)
                BaseWorkManager.CancelWork(ag, st);
            GUI.enabled = baseEnabled;

            y += 28f;
            string detail;
            if (kind == CompanionStatusKind.Blocked) detail = DescribeBlockReason(st, lang);
            else if (st.HasAnyPhysicalTask && BaseWorkManager.Paused)
                detail = CompanionConfigStore.L(lang, "\u0416\u0434\u0451\u0442 \u043a\u043e\u043d\u0446\u0430 \u043f\u0430\u0443\u0437\u044b: ", "Waiting for resume: ", "\u7b49\u5f85\u7ee7\u7eed\uff1a") + st.CurrentTaskText;
            else if (st.HasAnyPhysicalTask) detail = st.CurrentTaskText;
            else detail = CompanionConfigStore.L(lang, "\u0421\u0432\u043e\u0431\u043e\u0434\u0435\u043d", "Free", "\u7a7a\u95f2");
            if (st.CarryBuffer.Count > 0)
                detail += CompanionConfigStore.L(lang, $"  \u00b7  \u043d\u0435\u0441\u0451\u0442 {st.CarryBuffer.Count} \u043f\u0440\u0435\u0434\u043c.", $"  \u00b7  carrying {st.CarryBuffer.Count}", $"  \u00b7  \u643a\u5e26 {st.CarryBuffer.Count} \u4ef6");
            if (st.IsLootingActive && st.LootQueue.Count > 0)
                detail += CompanionConfigStore.L(lang, $"  \u00b7  \u043e\u0441\u0442\u0430\u043b\u043e\u0441\u044c \u0442\u043e\u0447\u0435\u043a: {st.LootQueue.Count}", $"  \u00b7  stops left: {st.LootQueue.Count}", $"  \u00b7  \u5269\u4f59\u5730\u70b9\uff1a{st.LootQueue.Count}");
            GUI.Label(new Rect(x, y, w, 26f), UiSafe(detail), _labelStyle);
        }
    }
}
