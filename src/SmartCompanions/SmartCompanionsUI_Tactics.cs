using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 2.1.0: tactical bar, per-companion order row, and retreat/formation settings.
    public static partial class SmartCompanionsUI
    {
        private static void DrawSquadTacticalBar(Rect row, ModLanguage lang)
        {
            float gap = 6f;
            float bw = (row.width - gap * 5f) / 6f;
            string formName = TacticalSquadSystem.GetFormationLabel(CompanionConfigStore.ActiveFormation, lang);
            string distName = TacticalSquadSystem.GetFollowDistanceLabel(CompanionConfigStore.FollowDistance, lang);

            if (GUI.Button(new Rect(row.x, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, $"Строй: {formName}", $"Form: {formName}", $"队形：{formName}"),
                CompanionConfigStore.L(lang, "Построение отряда при следовании (Свободно / Клин / Шеренга / Колонна / Круг)",
                    "Squad follow formation (Free / Wedge / Line / Column / Ring)", "跟随时的队伍阵型（自由/楔形/横队/纵队/环形）")),
                CompanionConfigStore.ActiveFormation != SquadFormation.Free ? _catOnStyle : _btnStyle))
            {
                TacticalSquadSystem.CycleFormation();
            }

            if (GUI.Button(new Rect(row.x + (bw + gap) * 1f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, $"Дист.: {distName}", $"Dist: {distName}", $"距离：{distName}"),
                CompanionConfigStore.L(lang, "Дистанция следования за командиром (2.5м / 4м / 6.5м / 9м)",
                    "Follow distance behind leader (2.5m / 4m / 6.5m / 9m)", "与队长的跟随距离（2.5米/4米/6.5米/9米）")),
                CompanionConfigStore.FollowDistance != FollowDistanceMode.Normal ? _catOnStyle : _btnStyle))
            {
                TacticalSquadSystem.CycleFollowDistance();
            }

            if (GUI.Button(new Rect(row.x + (bw + gap) * 2f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Следовать", "Follow", "跟随编队"),
                CompanionConfigStore.L(lang, "Снять удержание позиции — весь отряд снова идёт за вами в выбранном строю",
                    "Clear hold orders — the squad follows you in formation again", "解除驻守指令——全队恢复编队跟随")), _btnStyle))
            {
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.Follow);
            }

            if (GUI.Button(new Rect(row.x + (bw + gap) * 3f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Держать", "Hold Here", "原地坚守"),
                CompanionConfigStore.L(lang, "Приказать отряду удерживать свои текущие точки и вести огонь с места",
                    "Order the squad to hold their current positions and fire from there", "命令全队在当前位置驻守并原地射击")), _btnStyle))
            {
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.HoldPosition);
            }

            if (GUI.Button(new Rect(row.x + (bw + gap) * 4f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Ко мне!", "Fall Back", "撤回到我"),
                CompanionConfigStore.L(lang, "Немедленно разорвать дистанцию и бегом отступить к вашей позиции",
                    "Disengage and run back to your current position", "立即脱离追击并跑步撤回到你的位置")), _btnStyle))
            {
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.FallBackHere);
            }

            if (GUI.Button(new Rect(row.x + (bw + gap) * 5f, row.y, bw, row.height), UiTip(
                CompanionConfigStore.L(lang, "Сектор", "Cover Dir", "掩护方向"),
                CompanionConfigStore.L(lang, "Удерживать позицию и в первую очередь встречать цели в направлении вашего прицела",
                    "Hold position and prioritize targets in your current aim direction", "原地驻守并优先迎击你当前瞄准方向的敌人")), _btnStyle))
            {
                TacticalSquadSystem.IssueOrder(null, TacticalOrderKind.CoverDirection);
            }
        }

        private static void DrawCompanionTacticalRow(Rect row, GOAPAgent ag, CompanionRuntimeState st, ModLanguage lang)
        {
            float statusW = row.width * 0.44f;
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
            float bw = (remW - gap * 3f) / 4f;

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
        }

        private static float DrawTacticalSettings(float x, float y, float width, ModLanguage lang)
        {
            float halfW = (width - 8f) * 0.5f;
            string formName = TacticalSquadSystem.GetFormationLabel(CompanionConfigStore.ActiveFormation, lang);
            string distName = TacticalSquadSystem.GetFollowDistanceLabel(CompanionConfigStore.FollowDistance, lang);

            if (GUI.Button(new Rect(x, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Построение отряда: {formName}", $"Squad Formation: {formName}", $"队伍编队阵型：{formName}"),
                CompanionConfigStore.ActiveFormation != SquadFormation.Free ? _catOnStyle : _btnStyle))
            {
                TacticalSquadSystem.CycleFormation();
            }
            if (GUI.Button(new Rect(x + halfW + 8f, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Дистанция следования: {distName}", $"Follow Distance: {distName}", $"跟随距离：{distName}"),
                CompanionConfigStore.FollowDistance != FollowDistanceMode.Normal ? _catOnStyle : _btnStyle))
            {
                TacticalSquadSystem.CycleFollowDistance();
            }
            y += 36f;

            string hpLabel = TacticalSquadSystem.GetRetreatHpLabel(CompanionConfigStore.RetreatHpThresholdPercent, lang);
            string ammoLabel = TacticalSquadSystem.GetLowAmmoRetreatLabel(CompanionConfigStore.LowAmmoRetreat, lang);
            if (GUI.Button(new Rect(x, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Отход при ранении: {hpLabel}", $"Wounded Retreat: {hpLabel}", $"受伤撤退阈值：{hpLabel}"),
                CompanionConfigStore.RetreatHpThresholdPercent > 0 ? _catOnStyle : _catOffStyle))
            {
                TacticalSquadSystem.CycleRetreatHpThreshold();
            }
            if (GUI.Button(new Rect(x + halfW + 8f, y, halfW, 30f),
                CompanionConfigStore.L(lang, $"Отход без патронов: {ammoLabel}", $"Low-Ammo Retreat: {ammoLabel}", $"缺弹撤退条件：{ammoLabel}"),
                CompanionConfigStore.LowAmmoRetreat != LowAmmoRetreatMode.Off ? _catOnStyle : _catOffStyle))
            {
                TacticalSquadSystem.CycleLowAmmoRetreat();
            }
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
