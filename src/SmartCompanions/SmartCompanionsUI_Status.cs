using System;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 1.8.1: readable companion states, squad filters and hover hints for the F8 hub.
    public static partial class SmartCompanionsUI
    {
        internal enum SquadFilter { All = 0, Busy = 1, Idle = 2, NeedsHelp = 3 }
        internal enum CompanionStatusKind { Waiting, Busy, Combat, Blocked, Wounded }

        private static SquadFilter _squadFilter = SquadFilter.All;
        private const float BlockReasonVisibleSeconds = 90f;

        internal static bool IsAgentInCombat(GOAPAgent ag)
        {
            try
            {
                return Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) &&
                       !ag.mostThreateningCharacter.deadFlag;
            }
            catch { return false; }
        }

        internal static bool NeedsHelp(GOAPAgent ag)
        {
            try
            {
                var status = ag.npcData?.characterData?.characterStatusData;
                float hp = status != null ? status.hp : ag.npcData.health;
                float maxHp = status != null && status.maxhp > 0 ? status.maxhp : Mathf.Max(100f, ag.npcData.maxHealth);
                float hunger = status != null ? status.hunger : ag.npcData.hunger;
                float thirst = status != null ? status.thirst : ag.npcData.thirst;
                return maxHp > 0f && hp / maxHp < 0.5f || hunger < 25f || thirst < 25f;
            }
            catch { return false; }
        }

        internal static CompanionStatusKind GetStatusKind(GOAPAgent ag, CompanionRuntimeState st)
        {
            if (IsAgentInCombat(ag)) return CompanionStatusKind.Combat;
            if (st != null && st.HasAnyPhysicalTask) return CompanionStatusKind.Busy;
            if (NeedsHelp(ag)) return CompanionStatusKind.Wounded;
            if (st != null && st.LastBlockReason != CompanionBlockReason.None &&
                st.LastBlockReason != CompanionBlockReason.NothingToDo &&
                Time.time - st.LastBlockTime < BlockReasonVisibleSeconds)
                return CompanionStatusKind.Blocked;
            return CompanionStatusKind.Waiting;
        }

        internal static bool PassesSquadFilter(GOAPAgent ag, CompanionRuntimeState st)
        {
            if (_squadFilter == SquadFilter.All) return true;
            var kind = GetStatusKind(ag, st);
            switch (_squadFilter)
            {
                case SquadFilter.Busy: return kind == CompanionStatusKind.Busy || kind == CompanionStatusKind.Combat;
                case SquadFilter.Idle: return kind == CompanionStatusKind.Waiting || kind == CompanionStatusKind.Blocked;
                case SquadFilter.NeedsHelp: return NeedsHelp(ag);
                default: return true;
            }
        }

        internal static string GetStatusLabel(CompanionStatusKind kind, ModLanguage lang)
        {
            switch (kind)
            {
                case CompanionStatusKind.Combat: return CompanionConfigStore.L(lang, "В БОЮ", "IN COMBAT", "战斗中");
                case CompanionStatusKind.Busy: return CompanionConfigStore.L(lang, "ЗАНЯТ", "BUSY", "忙碌");
                case CompanionStatusKind.Blocked: return CompanionConfigStore.L(lang, "ОСТАНОВЛЕН", "STOPPED", "已停止");
                case CompanionStatusKind.Wounded: return CompanionConfigStore.L(lang, "НУЖНА ПОМОЩЬ", "NEEDS HELP", "需要帮助");
                default: return CompanionConfigStore.L(lang, "ЖДЁТ ПРИКАЗА", "AWAITING ORDERS", "待命");
            }
        }

        internal static Color GetStatusColor(CompanionStatusKind kind)
        {
            switch (kind)
            {
                case CompanionStatusKind.Combat: return new Color(1f, 0.42f, 0.36f);
                case CompanionStatusKind.Busy: return new Color(0.45f, 0.82f, 1f);
                case CompanionStatusKind.Blocked: return new Color(1f, 0.74f, 0.3f);
                case CompanionStatusKind.Wounded: return new Color(1f, 0.55f, 0.75f);
                default: return new Color(0.72f, 0.92f, 0.62f);
            }
        }

        internal static string DescribeBlockReason(CompanionRuntimeState st, ModLanguage lang)
        {
            if (st == null) return "";
            string t = string.IsNullOrEmpty(st.LastBlockTarget) ? "" : st.LastBlockTarget;
            switch (st.LastBlockReason)
            {
                case CompanionBlockReason.PathBlocked:
                    return t.Length > 0
                        ? CompanionConfigStore.L(lang, $"Не нашёл путь к «{t}»", $"No path to \"{t}\"", $"无法到达“{t}”")
                        : CompanionConfigStore.L(lang, "Путь перекрыт", "Path is blocked", "路径受阻");
                case CompanionBlockReason.NoStorageSpace:
                    return CompanionConfigStore.L(lang, $"Нет места или ящика для: {t}", $"No space or chest for: {t}", $"缺少空位或分类箱：{t}");
                case CompanionBlockReason.NoMarkedBoxes:
                    return CompanionConfigStore.L(lang, "Нет ящиков с категориями", "No chests with categories", "没有已分类的箱子");
                case CompanionBlockReason.InterruptedByCombat:
                    return CompanionConfigStore.L(lang, "Работа прервана боем", "Work interrupted by combat", "工作因战斗中断");
                case CompanionBlockReason.NothingToDo:
                    return CompanionConfigStore.L(lang, "Работы нет", "Nothing to do", "无事可做");
                default: return "";
            }
        }

        private static void DrawSquadFilterRow(Rect row, ModLanguage lang, int total, int shown)
        {
            var labels = new[]
            {
                CompanionConfigStore.L(lang, "Все", "All", "全部"),
                CompanionConfigStore.L(lang, "Заняты", "Busy", "忙碌"),
                CompanionConfigStore.L(lang, "Свободны", "Idle", "空闲"),
                CompanionConfigStore.L(lang, "Нужна помощь", "Need help", "需要帮助")
            };
            var tips = new[]
            {
                CompanionConfigStore.L(lang, "Показать весь отряд", "Show the whole squad", "显示全部队友"),
                CompanionConfigStore.L(lang, "Работают или сражаются", "Working or fighting", "正在工作或战斗"),
                CompanionConfigStore.L(lang, "Ждут приказа или остановились", "Waiting or stopped", "待命或已停止"),
                CompanionConfigStore.L(lang, "Ранены (HP < 50%), голодны или хотят пить", "Wounded (HP < 50%), hungry or thirsty", "受伤（HP<50%）、饥饿或口渴")
            };
            float countW = 150f;
            float bw = (row.width - countW - 6f * labels.Length) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
                if (GUI.Button(new Rect(row.x + i * (bw + 6f), row.y, bw, row.height), UiTip(labels[i], tips[i]),
                    (int)_squadFilter == i ? _catOnStyle : _btnStyle))
                {
                    _squadFilter = (SquadFilter)i;
                    _scrollSquad = Vector2.zero;
                }
            GUI.Label(new Rect(row.xMax - countW, row.y, countW, row.height),
                CompanionConfigStore.L(lang, $"Показано {shown}/{total}", $"Shown {shown}/{total}", $"显示 {shown}/{total}"), _labelStyle);
        }

        private static readonly System.Collections.Generic.Dictionary<char, bool> GlyphCache = new System.Collections.Generic.Dictionary<char, bool>();

        /// <summary>Removes glyphs the interface font cannot draw (emoji, pause/play symbols) so they never show as boxes.</summary>
        internal static string UiSafe(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Font font = null;
            try { font = GetInterfaceFont(); } catch { }
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsSurrogate(c) || c == '\uFE0F' || c == '\u200D') continue;
                if (c >= '\u2000' && font != null)
                {
                    if (!GlyphCache.TryGetValue(c, out bool ok))
                    {
                        try { ok = font.HasCharacter(c); } catch { ok = true; }
                        GlyphCache[c] = ok;
                    }
                    if (!ok) continue;
                }
                sb.Append(c);
            }
            return sb.ToString().Replace("  ", " ").Trim();
        }

        // IL2CPP interop strips GUIContent(string, string); use the three-argument overload.
        internal static GUIContent UiTip(string text, string tip) => new GUIContent(text, (Texture)null, tip ?? "");

        private static void DrawHoverTooltip(Rect window)
        {
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip) || Event.current == null || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            var content = new GUIContent(tip);
            float maxW = Mathf.Min(420f, window.width - 24f);
            float h = _labelStyle.CalcHeight(content, maxW - 16f) + 10f;
            float w = Mathf.Min(maxW, _labelStyle.CalcSize(content).x + 18f);
            if (w < 80f) w = 80f;
            if (_labelStyle.CalcSize(content).x + 18f > maxW) h = _labelStyle.CalcHeight(content, w - 16f) + 10f;
            Vector2 m = Event.current.mousePosition;
            float x = Mathf.Clamp(m.x + 16f, window.x + 4f, window.xMax - w - 4f);
            float y = m.y + 22f;
            if (y + h > window.yMax - 4f) y = m.y - h - 8f;
            var rect = new Rect(x, y, w, h);
            GUI.Box(rect, GUIContent.none, _winStyle);
            GUI.Box(rect, GUIContent.none, _boxStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, rect.height - 10f), content, _labelStyle);
        }
    }
}
