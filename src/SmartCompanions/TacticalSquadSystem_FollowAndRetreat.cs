using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class TacticalSquadSystem
    {
        public static bool TryExecuteFollowFormationAndSpacing(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                return false;
            if (!agent.IsPlayerTeammate()) return false;

            if (CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var st) && st != null)
            {
                if (st.HasAnyPhysicalTask) return false;
                if (st.ActiveOrder != TacticalOrderKind.Follow)
                    return TickTacticalGoalOverride(agent);
            }

            var leader = Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
                ? agent.leaderCharacter
                : GameController.instance?.playerCharacter;
            if (!Il2CppSafe.IsAliveUnity(leader) || leader.deadFlag) return false;

            if (Il2CppSafe.IsAlive(leader.characterData) && leader.characterData.zLevel != agent.npcData.zLevel &&
                (st == null || st.RosterMode != CompanionRosterMode.CampGarrison))
            {
                if (ExpeditionSystems.TryStepMultiFloorTransition(agent, st, leader.characterData.zLevel))
                    return true;
            }

            bool customFormation = CompanionConfigStore.ActiveFormation != SquadFormation.Free;
            bool customDist = CompanionConfigStore.FollowDistance != FollowDistanceMode.Normal;
            if (!customFormation && !customDist && !CompanionConfigStore.EnableNarrowPassageYield)
                return false;

            var squad = SmartChatterSystem.GetActiveTeammateAgents();
            int count = squad.Count;
            if (count == 0) return false;

            int myIdx = 0;
            ScratchAllyPos.Clear();
            ScratchAheadPos.Clear();
            for (int i = 0; i < count; i++)
            {
                var mate = squad[i];
                if (!Il2CppSafe.IsAliveUnity(mate) || !Il2CppSafe.IsAliveUnity(mate.humanCharacter)) continue;
                if (mate.Pointer == agent.Pointer)
                {
                    myIdx = i;
                    continue;
                }
                Vector2 mPos = mate.humanCharacter.transform.position;
                ScratchAllyPos.Add(mPos);
                if (i < myIdx) ScratchAheadPos.Add(mPos);
            }

            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;
            Vector2 leaderPos = leader.transform.position;
            CorridorNavigationHelper.RecordLeaderBreadcrumb(leaderPos, leader.characterData?.zLevel ?? 0);
            float followDist = GetFollowDistanceMeters(CompanionConfigStore.FollowDistance);
            Vector2 fwd = ResolveLeaderForward(leader);

            bool inCorridor = CorridorNavigationHelper.IsNarrowCorridorAt(agent, leaderPos, fwd, 1.30f) ||
                              CorridorNavigationHelper.IsNarrowCorridorAt(agent, selfPos, leaderPos - selfPos, 1.20f);

            Vector2 desired = inCorridor
                ? CorridorNavigationHelper.GetTrailSingleFileSlot(leaderPos, fwd, myIdx, 1.35f)
                : ComputeFormationSlot(myIdx, count, leaderPos, fwd, CompanionConfigStore.ActiveFormation, followDist);

            if (CompanionConfigStore.EnableNarrowPassageYield)
            {
                if (!inCorridor)
                    desired = ComputeAllySeparatedPoint(desired, selfPos, ScratchAllyPos, 1.25f);

                if (ScratchAheadPos.Count > 0)
                {
                    Vector2 toTarget = desired - selfPos;
                    if (toTarget.sqrMagnitude > 1.2f)
                    {
                        Vector2 dir = toTarget.normalized;
                        Vector2 left = new Vector2(-dir.y, dir.x);
                        bool clearL = true, clearR = true;
                        try
                        {
                            clearL = agent.IsPathClear(selfPos, selfPos + left * 1.15f, 0.15f);
                            clearR = agent.IsPathClear(selfPos, selfPos - left * 1.15f, 0.15f);
                        }
                        catch { }

                        if (ShouldYieldInNarrowPassage(myIdx, selfPos, desired, ScratchAheadPos, clearL, clearR))
                        {
                            hc.movementInput = Vector2.zero;
                            return true;
                        }
                    }
                }
            }

            // In Free + Normal distance mode: never push companions laterally into corridor walls; let native A* FollowLeader run!
            if (!customFormation && !customDist)
            {
                if (inCorridor) return false;
                bool stacked = false;
                for (int i = 0; i < ScratchAllyPos.Count; i++)
                {
                    if ((selfPos - ScratchAllyPos[i]).sqrMagnitude < 0.95f * 0.95f)
                    {
                        stacked = true;
                        break;
                    }
                }
                if (!stacked) return false;
            }

            desired = CorridorNavigationHelper.ClampToWalkableTarget(agent, desired, leaderPos);
            float distToSlot = Vector2.Distance(selfPos, desired);
            if (distToSlot <= 1.15f)
            {
                hc.movementInput = Vector2.zero;
                agent.hasNavigationTarget = false;
                return true;
            }

            try
            {
                CorridorNavigationHelper.SmartMoveTo(agent, desired, 1.05f, distToSlot > followDist + 2.6f);
                agent.LookAtPositionSmoothCompat(leaderPos + fwd * 4f, 9f);
            }
            catch { }
            return true;
        }

        public static bool TryApplyCombatRetreat(
            GOAPAgent agent, CompanionRuntimeState st,
            HumanCharacterController leader, BasicCharacterController target, float now)
        {
            if (st == null || !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) ||
                !Il2CppSafe.IsAliveUnity(target))
                return false;

            var hc = agent.humanCharacter;
            var status = agent.npcData?.characterData?.characterStatusData;
            float hp = Il2CppSafe.IsAlive(status) ? status.hp : hc.hp;
            float maxHp = Il2CppSafe.IsAlive(status) && status.maxhp > 0f ? status.maxhp : Mathf.Max(100f, hc.FullHP);

            bool usingRanged = CombatResponse.AllowsRangedResponse(st, agent.npcData?.weaponPreference) &&
                               Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && Il2CppSafe.IsAlive(hc.rangedWeapon.weaponData);
            int loaded = 0, reserve = 0;
            if (usingRanged)
            {
                try
                {
                    loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                    reserve = BasicRangedWeapon.GetInventoryAmmoTotal(hc, hc.rangedWeapon.weaponData, 0);
                }
                catch { }
            }

            Vector2 selfPos = hc.transform.position;
            Vector2 targetPos = target.transform.position;
            float enemyDist = Vector2.Distance(selfPos, targetPos);

            var reason = EvaluateRetreatNeed(
                hp, maxHp, CompanionConfigStore.RetreatHpThresholdPercent,
                usingRanged, loaded, reserve, CompanionConfigStore.LowAmmoRetreat, enemyDist);

            if (reason == TacticalOrderBlockReason.None)
            {
                if (st.OrderBlockReason == TacticalOrderBlockReason.RetreatingWounded ||
                    st.OrderBlockReason == TacticalOrderBlockReason.RetreatingLowAmmo)
                {
                    st.OrderBlockReason = TacticalOrderBlockReason.None;
                }
                return false;
            }

            st.NoteTacticalBlock(reason);
            if (now - st.LastTacticalRetreatTime < 1.15f) return true;
            st.LastTacticalRetreatTime = now;

            Vector2 awayFromEnemy = enemyDist > 0.1f ? (selfPos - targetPos).normalized : Vector2.left;
            Vector2 retreatPoint = selfPos + awayFromEnemy * 3.2f;
            if (Il2CppSafe.IsAliveUnity(leader) && !leader.deadFlag)
            {
                Vector2 leaderPos = leader.transform.position;
                Vector2 behindLeader = leaderPos + (leaderPos - targetPos).normalized * 2.1f;
                if (Vector2.Distance(selfPos, leaderPos) > 2.4f)
                    retreatPoint = behindLeader;
            }

            try
            {
                CorridorNavigationHelper.SmartStepInClearDirection(
                    agent, selfPos, retreatPoint - selfPos,
                    Mathf.Clamp(Vector2.Distance(selfPos, retreatPoint), 1.6f, 3.2f), true);
                agent.LookAtPositionSmoothCompat(targetPos, 12f);
            }
            catch { }

            SmartChatterSystem.Speak(agent,
                reason == TacticalOrderBlockReason.RetreatingWounded ? ChatterTopic.CombatWounded : ChatterTopic.CombatLowAmmo,
                minCooldown: 8.5f);
            return true;
        }

        public static string GetFormationLabel(SquadFormation f, ModLanguage lang)
        {
            switch (f)
            {
                case SquadFormation.Wedge: return CompanionConfigStore.L(lang, "Клин", "Wedge", "楔形");
                case SquadFormation.Line: return CompanionConfigStore.L(lang, "Шеренга", "Line", "横队");
                case SquadFormation.Column: return CompanionConfigStore.L(lang, "Колонна", "Column", "纵队");
                case SquadFormation.GuardRing: return CompanionConfigStore.L(lang, "Круг", "Ring", "环形");
                default: return CompanionConfigStore.L(lang, "Свободно", "Free", "自由");
            }
        }

        public static string GetFollowDistanceLabel(FollowDistanceMode d, ModLanguage lang)
        {
            switch (d)
            {
                case FollowDistanceMode.Close: return CompanionConfigStore.L(lang, "2.5м", "2.5m", "2.5米");
                case FollowDistanceMode.Wide: return CompanionConfigStore.L(lang, "6.5м", "6.5m", "6.5米");
                case FollowDistanceMode.Scout: return CompanionConfigStore.L(lang, "9м", "9m", "9米");
                default: return CompanionConfigStore.L(lang, "4м", "4m", "4米");
            }
        }

        public static string GetOrderLabel(TacticalOrderKind order, ModLanguage lang)
        {
            switch (order)
            {
                case TacticalOrderKind.HoldPosition: return CompanionConfigStore.L(lang, "Держит позицию", "Holding position", "坚守位置");
                case TacticalOrderKind.FallBackHere: return CompanionConfigStore.L(lang, "Отход на точку", "Falling back", "撤回指定点");
                case TacticalOrderKind.CoverDirection: return CompanionConfigStore.L(lang, "Прикрывает сектор", "Covering sector", "掩护方向");
                default: return CompanionConfigStore.L(lang, "Следует в строю", "Following", "跟随编队");
            }
        }

        public static string GetRetreatHpLabel(int pct, ModLanguage lang) =>
            pct <= 0
                ? CompanionConfigStore.L(lang, "Выкл", "Off", "关闭")
                : $"<{pct}% HP";

        public static string GetLowAmmoRetreatLabel(LowAmmoRetreatMode m, ModLanguage lang)
        {
            switch (m)
            {
                case LowAmmoRetreatMode.EmptyOnly: return CompanionConfigStore.L(lang, "0 патр.", "0 ammo", "弹尽时");
                case LowAmmoRetreatMode.LowReserve: return CompanionConfigStore.L(lang, "<=5 патр.", "<=5 ammo", "<=5发时");
                default: return CompanionConfigStore.L(lang, "Выкл", "Off", "关闭");
            }
        }

        public static string DescribeTacticalBlock(CompanionRuntimeState st, ModLanguage lang)
        {
            if (st == null || st.OrderBlockReason == TacticalOrderBlockReason.None) return "";
            if (Time.unscaledTime - st.OrderBlockTime > 30f &&
                (st.OrderBlockReason == TacticalOrderBlockReason.RetreatingWounded ||
                 st.OrderBlockReason == TacticalOrderBlockReason.RetreatingLowAmmo ||
                 st.OrderBlockReason == TacticalOrderBlockReason.PathBlockedToAnchor))
                return "";

            switch (st.OrderBlockReason)
            {
                case TacticalOrderBlockReason.PathBlockedToAnchor:
                    return CompanionConfigStore.L(lang, "нет пути к точке", "no path to anchor", "无法到达位置");
                case TacticalOrderBlockReason.RetreatingWounded:
                    return CompanionConfigStore.L(lang, $"отход: ранен (<{CompanionConfigStore.RetreatHpThresholdPercent}% HP)",
                        $"retreat: wounded (<{CompanionConfigStore.RetreatHpThresholdPercent}% HP)",
                        $"撤退：受伤(<{CompanionConfigStore.RetreatHpThresholdPercent}%HP)");
                case TacticalOrderBlockReason.RetreatingLowAmmo:
                    return CompanionConfigStore.L(lang, "отход: нет патронов", "retreat: out of ammo", "撤退：弹药不足");
                case TacticalOrderBlockReason.SuppressedPassive:
                    return CompanionConfigStore.L(lang, "приказ «Не стрелять»", "cease-fire active", "已停火");
                case TacticalOrderBlockReason.PausedForBaseWork:
                    return CompanionConfigStore.L(lang, "занят работой базы", "on base task", "正执行基地任务");
                case TacticalOrderBlockReason.DifferentZLevel:
                    return CompanionConfigStore.L(lang, "другой этаж", "different floor", "不在同一楼层");
                default:
                    return "";
            }
        }

        public static string GetOrderBannerText(TacticalOrderKind order, int count, ModLanguage lang)
        {
            switch (order)
            {
                case TacticalOrderKind.HoldPosition:
                    return CompanionConfigStore.L(lang,
                        $"Приказ ({count} чел.): держать текущую позицию!",
                        $"Order ({count}): hold current position!",
                        $"战术指令（{count} 人）：原地坚守位置！");
                case TacticalOrderKind.FallBackHere:
                    return CompanionConfigStore.L(lang,
                        $"Приказ ({count} чел.): отступить к вашей позиции!",
                        $"Order ({count}): fall back to your position!",
                        $"战术指令（{count} 人）：撤回到你的位置！");
                case TacticalOrderKind.CoverDirection:
                    return CompanionConfigStore.L(lang,
                        $"Приказ ({count} чел.): прикрывать указанное направление!",
                        $"Order ({count}): cover aimed direction!",
                        $"战术指令（{count} 人）：掩护当前瞄准方向！");
                default:
                    return CompanionConfigStore.L(lang,
                        $"Приказ ({count} чел.): следовать в строю ({GetFormationLabel(CompanionConfigStore.ActiveFormation, lang)})!",
                        $"Order ({count}): follow in formation ({GetFormationLabel(CompanionConfigStore.ActiveFormation, lang)})!",
                        $"战术指令（{count} 人）：恢复跟随编队（{GetFormationLabel(CompanionConfigStore.ActiveFormation, lang)}）！");
            }
        }
    }
}
