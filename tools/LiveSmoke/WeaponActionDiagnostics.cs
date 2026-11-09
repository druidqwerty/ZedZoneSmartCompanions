using System;
using System.Text;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    // Read-only snapshots, no action execution, forced completion or weapon changes.
    internal static class WeaponActionDiagnostics
    {
        internal static void Log(GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(agent?.humanCharacter)) return;
            try
            {
                var human = agent.humanCharacter;
                var text = new StringBuilder("[WeaponActionDiag] ");
                text.Append("goal=").Append(agent.currentGoal?.goalName ?? "null")
                    .Append(" replan=").Append(agent.executeSelectPriorityGoalFlag)
                    .Append(" planFrame=").Append(agent.lastPlannedGoalFrame)
                    .Append(" goalTime=").Append(agent.lastGoalTimeFlag)
                    .Append(" holster=").Append(human.holsterWeapon)
                    .Append(" holsterRanged=").Append(human.hosterRangedWeapon)
                    .Append(" running/backpack/aid/downed=").Append(human.weaponHolsteredByRunning).Append('/')
                    .Append(human.weaponHolsteredByBackpack).Append('/').Append(human.weaponHolsteredByAid).Append('/')
                    .Append(human.weaponHolsteredByDowned)
                    .Append(" canEquip=").Append(human.CanStartEquipmentAction)
                    .Append(" pullFrame=").Append(human.pullOutWeaponFrameFlag)
                    .Append(" holsterFrame=").Append(human.holsterWeaponFrameFlag)
                    .Append(" restoring=").Append(human.isRestoringEquipmentState)
                    .Append(" animationScale=").Append(human.animationTimeScale)
                    .Append(" animSuspended=").Append(human.isRuntimeAnimationSuspend)
                    .Append(" aiming=").Append(human.isRangedWeaponAiming)
                    .Append(" movement=").Append(human.movementInput);
                var gun = human.rangedWeapon;
                var equipped = human.characterData?.characterEquipmentData?.currentWeaponItemData;
                var eq = human.characterData?.characterEquipmentData;
                if (eq != null)
                {
                    text.Append(" selectedSlot=").Append(eq.currentWeaponIndex).Append(" slots=");
                    for(int slot=0;slot<3;slot++)
                    { var item=eq.GetWeaponData(slot);text.Append(slot).Append(':').Append(item?.itemId??-1).Append(' '); }
                }
                text.Append(" equippedData=").Append(equipped == null ? "null" : $"{equipped.itemId}@{equipped.Pointer}")
                    .Append(" rangedData=").Append(gun?.weaponData == null ? "null" : $"{gun.weaponData.itemId}@{gun.weaponData.Pointer}");
                text.Append(" actions=");
                var actions = agent.currentActions;
                if (actions != null)
                    for (int i = 0; i < Math.Min(actions.Count, 5); i++)
                    {
                        var action = actions[i];
                        if (!Il2CppSafe.IsAlive(action)) continue;
                        text.Append('[').Append(action.actionName).Append(" started=").Append(action.actionStarted)
                            .Append(" start=").Append(action.actionStartTimeFlag)
                            .Append(" complete=").Append(action.IsCompleted(agent)).Append(']');
                    }
                text.Append(" states=");
                var states = agent.agentState?.states;
                if (states != null)
                {
                    int written = 0;
                    foreach (var state in states)
                    {
                        string key = state.Key?.ToLowerInvariant() ?? "";
                        if (!key.Contains("weapon") && !key.Contains("threat") && !key.Contains("combat") &&
                            !key.Contains("ammo") && !key.Contains("equip") && !key.Contains("aim")) continue;
                        text.Append('[').Append(state.Key).Append('=').Append(state.Value?.ToString() ?? "null").Append(']');
                        if (++written >= 16) break;
                    }
                }
                LiveSmokePlugin.Logger.LogInfo(text.ToString());
            }
            catch (Exception error) { LiveSmokePlugin.Logger.LogWarning("[WeaponActionDiag] Read failed: " + error); }
        }
    }
}
