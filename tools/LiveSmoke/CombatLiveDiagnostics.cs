using System;
using System.Text;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    internal static partial class CombatLiveRegression
    {
        private static float _nextDiagnosis;
        private static void DiagnosePhase(float now)
        {
            if (now < _nextDiagnosis) return;
            _nextDiagnosis = now + 2f;
            try
            {
                var human = _agent.humanCharacter;
                var gc = GameController.instance;
                var text = new StringBuilder();
                text.Append("[CombatLive] DIAG phase=").Append(_phase)
                    .Append(" paused=").Append(gc.isSoftPaused).Append(" timeScale=").Append(Time.timeScale)
                    .Append(" enabled(agent/human/target)=").Append(_agent.enabled).Append('/').Append(human.enabled).Append('/').Append(_zombie.enabled)
                    .Append(" active(agent/target)=").Append(_agent.gameObject.activeInHierarchy).Append('/').Append(_zombie.gameObject.activeInHierarchy)
                    .Append(" suppressed=").Append(_agent.IsTeammateCombatSuppressed())
                    .Append(" sight=").Append(_agent.sightRange).Append(" inSight=").Append(_agent.IsInSight(_zombie.transform.position))
                    .Append(" camp(self/target)=").Append(human.characterData.camp).Append('/').Append(_zombie.characterData.camp)
                    .Append(" hostile=").Append(BasicCharacterController.IsHostileCamp(_zombie.characterData))
                    .Append(" z(self/target)=").Append(_agent.npcData.zLevel).Append('/').Append(_zombie.characterData.zLevel)
                    .Append(" downed=").Append(_agent.downedStateFlag).Append(" sleeping=").Append(human.isSleeping)
                    .Append(" goal=").Append(_agent.currentGoal?.goalName ?? "null")
                    .Append(" basic=").Append(_agent.npcData.basicCommand)
                    .Append(" teammate=").Append(_agent.IsPlayerTeammate())
                    .Append(" stateCombat=").Append(_agent.agentState?.combatCommand ?? "null")
                    .Append(" threats=").Append(_agent.threatDataDic?.Count ?? -1).Append(" actions=");
                var actions = _agent.currentActions;
                if (actions != null)
                    for (int i = 0; i < Math.Min(actions.Count, 5); i++)
                        text.Append(actions[i]?.actionName ?? "null").Append(',');
                LiveSmokePlugin.Logger.LogInfo(text.ToString());
                PerceptionDiagnostics.Log(_agent, _zombie);
                WeaponActionDiagnostics.Log(_agent);
            }
            catch (Exception error) { LiveSmokePlugin.Logger.LogWarning("[CombatLive] Diagnostic read: " + error); }
        }
    }
}
