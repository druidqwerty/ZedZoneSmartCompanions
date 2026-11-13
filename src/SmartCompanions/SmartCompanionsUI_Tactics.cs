using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 2.1.0–2.5.0: tactical bar, cursor move, safe grenade, healing smoke, and retreat/formation settings.
    public static partial class SmartCompanionsUI
    {
        private static void DrawSquadTacticalBar(Rect row, ModLanguage lang)
        {
            float gap = 5f;
            float bw = (row.width - gap * 7f) / 8f;
            string formName = TacticalSquadSystem.GetFormationLabel(CompanionConfigStore.ActiveFormation, lang);
            string distName = TacticalSquadSystem.GetFollowDistanceLabel(CompanionConfigStore.FollowDistance, lang);

            if (GUI.Button(new Rect(row.x, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, formName, $"Form:{formName}", $"阵型:{formName}"),
                CompanionConfigStore.L(lang, "Построение отряда при следовании", "Squad follow formation", "跟随时的队伍阵型")),
                CompanionConfigStore.ActiveFormation != SquadFormation.Free ? _catOnStyle : _btnStyle))
                TacticalSquadSystem.CycleFormation();

            if (GUI.Button(new Rect(row.x + (bw + gap) * 1f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, $"Дист:{distName}", $"Dist:{distName}", $"距离:{distName}"),
                CompanionConfigStore.L(lang, "Дистанция следования (2.5–9м)", "Follow distance (2.5–9m)", "跟随距离（2.5–9米）")),
                CompanionConfigStore.FollowDistance != FollowDistanceMode.Normal ? _catOnStyle : _btnStyle))
                TacticalSquadSystem.CycleFollowDistance();

            if (GUI.Button(new Rect(row.x + (bw + gap) * 2f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "В строй", "Follow", "跟随"),
                CompanionConfigStore.L(lang, "Вернуть отряд в строй", "Resume following in formation", "恢复编队跟随")), _btnStyle))
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.Follow);

            if (GUI.Button(new Rect(row.x + (bw + gap) * 3f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Держать", "Hold", "坚守"),
                CompanionConfigStore.L(lang, "Удерживать текущие позиции", "Hold current spots", "原地驻守射击")), _btnStyle))
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.HoldPosition);

            if (GUI.Button(new Rect(row.x + (bw + gap) * 4f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Ко мне!", "Retreat", "撤回"),
                CompanionConfigStore.L(lang, "Бегом отступить к вашей позиции", "Run back to your position", "跑步撤回玩家位置")), _btnStyle))
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.FallBackHere);

            if (GUI.Button(new Rect(row.x + (bw + gap) * 5f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Сектор", "Sector", "掩护"),
                CompanionConfigStore.L(lang, "Прикрывать направление прицела", "Cover aimed direction", "掩护瞄准方向")), _btnStyle))
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.CoverDirection);

            if (GUI.Button(new Rect(row.x + (bw + gap) * 6f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "К цели", "To Spot", "定点"),
                CompanionConfigStore.L(lang, "Отправить отряд веером в точку прицела/курсора [Alt+G]",
                    "Send squad in a fan to the cursor spot [Alt+G]", "扇形前往准星位置 [Alt+G]")), _btnStyle))
                FieldTacticsSystem.IssueMoveToCursorOrder(null);

            if (GUI.Button(new Rect(row.x + (bw + gap) * 7f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Граната", "Grenade", "安全雷"),
                CompanionConfigStore.L(lang, "Прицельная граната (строго с защитой своих и ящиков базы)",
                    "Safe offensive grenade (blocks friendly fire & base chest damage)", "防误伤安全投雷（保护友军与基地箱）")), _btnStyle))
                FieldTacticsSystem.CommandSafeOffensiveGrenade(null);
        }

        private static void DrawCompanionTacticalRow(Rect row, GOAPAgent ag, CompanionRuntimeState st, ModLanguage lang)
        {
            float statusW = row.width * 0.36f;
            string orderText = TacticalSquadSystem.GetOrderLabel(st.ActiveOrder, lang);
            string blockText = TacticalSquadSystem.DescribeTacticalBlock(st, lang);

            var prevColor = GUI.color;
            if (!string.IsNullOrEmpty(blockText))
                GUI.color = new Color(1f, 0.76f, 0.32f);
            else if (st.ActiveOrder != TacticalOrderKind.Follow)
                GUI.color = new Color(0.52f, 0.88f, 1f);

            string summary = string.IsNullOrEmpty(blockText)
                ? CompanionConfigStore.L(lang, $"Приказ: {orderText}", $"Order: {orderText}", $"指令：{orderText}")
                : $"{orderText} · {blockText}";

            bool wrapWas = _labelStyle.wordWrap;
            _labelStyle.wordWrap = false;
            GUI.Label(new Rect(row.x, row.y, statusW - 6f, row.height), UiTip(
                UiSafe(summary),
                CompanionConfigStore.L(lang, "Текущий боевой приказ напарника и причина задержки/отступления",
                    "Current combat order and why it is delayed or overridden", "当前战斗指令及延迟或撤退原因")), _labelStyle);
            _labelStyle.wordWrap = wrapWas;
            GUI.color = prevColor;

            float bx = row.x + statusW;
            float remW = row.width - statusW;
            float gap = 5f;
            float bw = (remW - gap * 4f) / 5f;

            var items = new[]
            {
                (TacticalOrderKind.Follow,         CompanionConfigStore.L(lang, "В строй", "Follow", "跟随"),
                    CompanionConfigStore.L(lang, "Идти за командиром в строю", "Follow leader in formation", "按编队跟随队长")),
                (TacticalOrderKind.HoldPosition,   CompanionConfigStore.L(lang, "Держать", "Hold", "坚守"),
                    CompanionConfigStore.L(lang, "Удерживать текущую точку", "Hold current spot", "坚守当前位置")),
                (TacticalOrderKind.FallBackHere,   CompanionConfigStore.L(lang, "Отход", "Retreat", "撤回"),
                    CompanionConfigStore.L(lang, "Отступить к позиции игрока", "Fall back to player position", "撤回到玩家位置")),
                (TacticalOrderKind.CoverDirection, CompanionConfigStore.L(lang, "Сектор", "Cover", "掩护"),
                    CompanionConfigStore.L(lang, "Прикрывать направление прицела", "Cover aimed direction", "掩护当前瞄准方向"))
            };

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                bool active = st.ActiveOrder == it.Item1;
                if (GUI.Button(new Rect(bx + i * (bw + gap), row.y, bw, row.height),
                    UiTip(it.Item2, it.Item3), active ? _catOnStyle : _catOffStyle))
                {
                    TacticalSquadSystem.IssueOrder(ag, it.Item1);
                }
            }

            if (GUI.Button(new Rect(bx + 4f * (bw + gap), row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Мед-дым", "Smoke", "治疗烟"),
                CompanionConfigStore.L(lang, "Бросить лечебный дым или перевязать рядом [Alt+H]",
                    "Throw healing smoke or field-bandage nearby [Alt+H]", "投掷治疗烟雾或近身包扎 [Alt+H]")), _btnStyle))
            {
                FieldTacticsSystem.CommandTacticalHealingSmoke(ag);
            }
        }

        private static float DrawTacticalSettings(float x, float y, float width, ModLanguage lang)
        {
            float halfW = (width - 8f) * 0.5f;
            string formName = TacticalSquadSystem.GetFormationLabel(CompanionConfigStore.ActiveFormation, lang);
            string distName = TacticalSquadSystem.GetFollowDistanceLabel(CompanionConfigStore.FollowDistance, lang);

            if (GUI.Button(new Rect(x, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Построение отряда: {formName}", $"Squad Formation: {formName}", $"队伍编队阵型：{formName}"),
                CompanionConfigStore.ActiveFormation != SquadFormation.Free ? _catOnStyle : _btnStyle))
                TacticalSquadSystem.CycleFormation();
            if (GUI.Button(new Rect(x + halfW + 8f, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Дистанция следования: {distName}", $"Follow Distance: {distName}", $"跟随距离：{distName}"),
                CompanionConfigStore.FollowDistance != FollowDistanceMode.Normal ? _catOnStyle : _btnStyle))
                TacticalSquadSystem.CycleFollowDistance();
            y += 36f;

            string hpLabel = TacticalSquadSystem.GetRetreatHpLabel(CompanionConfigStore.RetreatHpThresholdPercent, lang);
            string ammoLabel = TacticalSquadSystem.GetLowAmmoRetreatLabel(CompanionConfigStore.LowAmmoRetreat, lang);
            if (GUI.Button(new Rect(x, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Отход при ранении: {hpLabel}", $"Wounded Retreat: {hpLabel}", $"受伤撤退阈值：{hpLabel}"),
                CompanionConfigStore.RetreatHpThresholdPercent > 0 ? _catOnStyle : _catOffStyle))
                TacticalSquadSystem.CycleRetreatHpThreshold();
            if (GUI.Button(new Rect(x + halfW + 8f, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Отход без патронов: {ammoLabel}", $"Low-Ammo Retreat: {ammoLabel}", $"缺弹撤退条件：{ammoLabel}"),
                CompanionConfigStore.LowAmmoRetreat != LowAmmoRetreatMode.Off ? _catOnStyle : _catOffStyle))
                TacticalSquadSystem.CycleLowAmmoRetreat();
            y += 36f;

            if (GUI.Button(new Rect(x, y, width, 30f),
                (CompanionConfigStore.EnableNarrowPassageYield ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang,
                    "Аккуратный проход узких дверей и интервал между союзниками",
                    "Single-file doorway yield & anti-clumping ally spacing",
                    "窄门单列礼让通行与队友防拥挤间距"),
                CompanionConfigStore.EnableNarrowPassageYield ? _catOnStyle : _catOffStyle))
            {
                CompanionConfigStore.EnableNarrowPassageYield = !CompanionConfigStore.EnableNarrowPassageYield;
                CompanionConfigStore.Save();
            }
            return y + 36f;
        }
    }
}
