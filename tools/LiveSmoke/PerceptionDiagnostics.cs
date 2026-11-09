using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    // Observation only: neither native execution nor its result is changed.
    [HarmonyPatch]
    internal static class PerceptionDiagnostics
    {
        private static readonly string[] Names = {
            "Update", "UpdatePreceptionSystem", "UpdateAgentSight", "RefreshThreatsNow", "UpdateThreats"
        };
        private static readonly Dictionary<IntPtr, Dictionary<string, int>> Calls = new();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in Names)
                yield return AccessTools.DeclaredMethod(typeof(GOAPAgent), name);
        }

        private static void Prefix(GOAPAgent __instance, MethodBase __originalMethod)
        {
            try
            {
                if (ReferenceEquals(__instance, null) || __instance.Pointer == IntPtr.Zero) return;
                if (!Calls.TryGetValue(__instance.Pointer, out var counters))
                {
                    counters = new Dictionary<string, int>();
                    Calls.Add(__instance.Pointer, counters);
                }
                string name = __originalMethod.Name;
                counters.TryGetValue(name, out int count);
                counters[name] = count + 1;
            }
            catch { } // Diagnostics must never alter native AI on an expired wrapper.
        }

        internal static void Log(GOAPAgent agent, ZombieController zombie)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(zombie)) return;
            try
            {
                var text = new StringBuilder("[PerceptionDiag] ");
                text.Append("frame=").Append(Time.frameCount).Append(" time=").Append(Time.time)
                    .Append(" realtime=").Append(Time.realtimeSinceStartup).Append(" dt=").Append(Time.deltaTime)
                    .Append(" started=").Append(agent.agentStartedFlag).Append(" goapPause=").Append(agent.goapPauseFlag)
                    .Append(" frameSkip=").Append(GameController.instance.characterFrameSkipping)
                    .Append(" focus=").Append(Application.isFocused).Append(" calls=");
                Calls.TryGetValue(agent.Pointer, out var counters);
                foreach (string name in Names)
                {
                    int count = 0;
                    counters?.TryGetValue(name, out count);
                    text.Append(name).Append(':').Append(count).Append(' ');
                }
                text.Append(" selfTransform=").Append(agent.humanCharacter.transform.position)
                    .Append(" camera(self/target)=").Append(agent.humanCharacter.isOnScreen).Append('/').Append(zombie.isOnScreen)
                    .Append(" perceiveOffScreen=").Append(agent.perceiveOffScreen).Append(" frameOffset=").Append(agent.frameOffset)
                    .Append(" selfData=").Append(agent.humanCharacter.characterData.characterPosition)
                    .Append(" agentLast=").Append(agent.lastPosition)
                    .Append(" targetTransform=").Append(zombie.transform.position)
                    .Append(" targetData=").Append(zombie.characterData.characterPosition)
                    .Append(" targetTemp=").Append(zombie.positionTemp)
                    .Append(" targetWorldInit=").Append(zombie.initCharacterToWorldPosition)
                    .Append(" targetDataInit=").Append(zombie.characterData.initFlag)
                    .Append(" targetInitTime=").Append(zombie.initUnscaledTimeFlag)
                    .Append(" invincibleUntil=").Append(zombie.netRespawnInvincibleUntil)
                    .Append(" frozen=").Append(zombie.isScriptedMoveFreezeActive)
                    .Append(" rootLayer=").Append(zombie.gameObject.layer);
                var body = zombie.m_rigidbody;
                if (body != null)
                    text.Append(" body=").Append(body.bodyType).Append(" simulated=").Append(body.simulated)
                        .Append(" bodyPos=").Append(body.position);
                text.Append(" colliders=");
                foreach (var collider in zombie.GetComponentsInChildren<Collider2D>(true))
                {
                    if (collider == null) continue;
                    text.Append('[').Append(collider.name).Append(" enabled=").Append(collider.enabled)
                        .Append(" active=").Append(collider.gameObject.activeInHierarchy)
                        .Append(" layer=").Append(collider.gameObject.layer)
                        .Append(" trigger=").Append(collider.isTrigger).Append(']');
                }
                LiveSmokePlugin.Logger.LogInfo(text.ToString());
            }
            catch (Exception error) { LiveSmokePlugin.Logger.LogWarning("[PerceptionDiag] Read failed: " + error); }
        }
    }
}
