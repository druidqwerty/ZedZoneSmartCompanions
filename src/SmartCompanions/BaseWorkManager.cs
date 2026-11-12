using System;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    // 2.0.0: base work management - roles, global pause and per-companion cancel.
    // Nothing here moves items without a walk: cancelling keeps carried items in the
    // companion's own inventory (ReturnCarryBufferWithoutTeleport).
    public static class BaseWorkManager
    {
        public static bool Paused => CompanionConfigStore.BaseWorkPaused;

        public static bool AllowsSorting(CompanionRuntimeState st) =>
            st != null && (st.WorkRole == CompanionWorkRole.Any || st.WorkRole == CompanionWorkRole.Sorter);

        public static bool AllowsScavenging(CompanionRuntimeState st) =>
            st != null && (st.WorkRole == CompanionWorkRole.Any || st.WorkRole == CompanionWorkRole.Scavenger);

        public static bool AllowsChores(CompanionRuntimeState st) =>
            st != null && (st.WorkRole == CompanionWorkRole.Any || st.WorkRole == CompanionWorkRole.Camp);

        public static bool AllowsAutoDropoff(CompanionRuntimeState st) =>
            st != null && st.WorkRole != CompanionWorkRole.Off;

        /// <summary>Automatic (not player-ordered) work may start only when not paused and the role allows it.</summary>
        public static bool MayStartAutomaticWork(CompanionRuntimeState st, Func<CompanionRuntimeState, bool> roleCheck) =>
            !Paused && st != null && roleCheck(st);

        public static void CycleRole(CompanionRuntimeState st)
        {
            if (st == null) return;
            st.WorkRole = (CompanionWorkRole)(((int)st.WorkRole + 1) % 5);
            CompanionConfigStore.Save();
            MultiplayerSyncSystem.SyncCompanionStateChange(st);
        }

        public static void SetPaused(bool paused, bool fromNetwork = false)
        {
            if (CompanionConfigStore.BaseWorkPaused == paused) return;
            CompanionConfigStore.BaseWorkPaused = paused;
            if (!fromNetwork) MultiplayerSyncSystem.SyncBaseWorkPause(paused);
            BaseSortingSystem.SetBanner(paused
                ? CompanionConfigStore.L("Работа базы на паузе: напарники держат вещи при себе и ждут.",
                    "Base work paused: companions keep what they carry and wait.",
                    "基地工作已暂停：队友保留携带的物品并等待。")
                : CompanionConfigStore.L("Работа базы продолжается.", "Base work resumed.", "基地工作已恢复。"));
        }

        public static void CancelWork(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (st == null) return;
            bool had = st.HasAnyPhysicalTask || st.CarryBuffer.Count > 0;
            PhysicalMovementHelper.CancelAllPhysicalTasksOnCombat(agent, st);
            if (had) st.NoteBlocked(CompanionBlockReason.CancelledByPlayer);
            st.CurrentTaskText = "";
            try
            {
                if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                {
                    agent.humanCharacter.movementInput = Vector2.zero;
                    agent.hasNavigationTarget = false;
                }
            }
            catch { }
        }

        public static void CancelAll()
        {
            foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData)) continue;
                if (!MultiplayerSyncSystem.CanLocalPlayerCommand(ag.npcData)) continue;
                if (CompanionConfigStore.CompanionStates.TryGetValue(ag.npcData.npcGUID ?? "", out var st))
                    CancelWork(ag, st);
            }
        }

        /// <summary>Called each frame for a companion with a physical job while paused: hold timers so the job resumes.</summary>
        public static void HoldJobWhilePaused(GOAPAgent agent, CompanionRuntimeState st)
        {
            float now = Time.time;
            st.ArrivalInteractStarted = false;
            if (Il2CppSafe.IsAliveUnity(agent?.humanCharacter))
                st.LastMovedPos = agent.humanCharacter.transform.position;
            st.LastMovedTime = now;
            st.LastProgressTime = now;
            st.SortingStateStartTime = now;
        }

        public static string GetRoleLabel(CompanionWorkRole role, ModLanguage lang)
        {
            switch (role)
            {
                case CompanionWorkRole.Sorter: return CompanionConfigStore.L(lang, "Кладовщик", "Storekeeper", "仓管");
                case CompanionWorkRole.Scavenger: return CompanionConfigStore.L(lang, "Сборщик", "Scavenger", "拾荒者");
                case CompanionWorkRole.Camp: return CompanionConfigStore.L(lang, "Лагерь", "Camp keeper", "营地");
                case CompanionWorkRole.Off: return CompanionConfigStore.L(lang, "Без работы", "No base work", "不干活");
                default: return CompanionConfigStore.L(lang, "Любая работа", "Any work", "任意工作");
            }
        }

        public static string GetRoleTip(CompanionWorkRole role, ModLanguage lang)
        {
            switch (role)
            {
                case CompanionWorkRole.Sorter: return CompanionConfigStore.L(lang, "Только раскладывает вещи по ящикам базы", "Only sorts items into base chests", "只把物品整理进基地箱子");
                case CompanionWorkRole.Scavenger: return CompanionConfigStore.L(lang, "Только собирает лут рядом и приносит на базу", "Only gathers nearby loot and brings it to base", "只收集附近物资并带回基地");
                case CompanionWorkRole.Camp: return CompanionConfigStore.L(lang, "Только костёр, грядки и багажник", "Only fire, crops and trunk", "只负责篝火、作物和后备箱");
                case CompanionWorkRole.Off: return CompanionConfigStore.L(lang, "Не берёт работу сам; прямые приказы выполняет", "Takes no work on their own; still follows direct orders", "不会主动干活，但仍执行直接命令");
                default: return CompanionConfigStore.L(lang, "Берёт любую разрешённую работу базы", "Takes any allowed base work", "接受任何允许的基地工作");
            }
        }
    }
}
