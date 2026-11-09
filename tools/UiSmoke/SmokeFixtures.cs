using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class SmokeFixtures
    {
        internal static readonly List<GOAPAgent> Agents = new List<GOAPAgent>();
        internal static readonly List<GOAPAgent> LongAgents = new List<GOAPAgent>();
        private static readonly List<GameObject> Objects = new List<GameObject>();
        internal static bool ShowAgents = true;
        internal static bool LongSquad;
        internal static int VisibleAgentCount => !ShowAgents ? 0 : LongSquad ? LongAgents.Count : Agents.Count;
        internal static int UiFailures;

        internal static void Create()
        {
            // Disabled objects never run NPC AI. They are not registered in any world or save.
            string[] names = { "Тестовый напарник", "Smoke companion", "测试队友" };
            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject("SmartCompanions_UI_SMOKE_" + i);
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.SetActive(false);
                Objects.Add(go);
                var agent = go.AddComponent<GOAPAgent>();
                var human = go.AddComponent<HumanCharacterController>();
                var status = new CharacterStatusData { hp = 90, maxhp = 100, hunger = 70, thirst = 80 };
                var character = new CharacterData { characterName = names[i], characterStatusData = status };
                var npc = new GOAPNPCData { npcGUID = "SC-UI-SMOKE-" + i, characterData = character, health = 90, hunger = 70, thirst = 80 };
                agent.npcData = npc;
                agent.humanCharacter = human;
                Agents.Add(agent);
            }
            LongAgents.AddRange(Agents);
            LongAgents.AddRange(Agents);
            UiSmokePlugin.Logger.LogInfo("[UiSmoke] Created three disabled native GOAPAgent fixtures.");
        }

        internal static void VerifyNativeInputGates()
        {
            var go = new GameObject("SmartCompanions_UI_SMOKE_Input");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            Objects.Add(go);
            var input = go.AddComponent<CharacterInput>();
            if (!input.stopAllInput || input.CanPressMouse || input.CanControlMouseActions)
                throw new Exception("Native CharacterInput getters did not respect the open F8 modal gate.");
            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS native CharacterInput stopAllInput=true, CanPressMouse=false, CanControlMouseActions=false.");
        }

        internal static void Cleanup()
        {
            Agents.Clear();
            LongAgents.Clear();
            foreach (var go in Objects) if (go != null) UnityEngine.Object.Destroy(go);
            Objects.Clear();
        }
    }

    [HarmonyPatch(typeof(SmartChatterSystem), nameof(SmartChatterSystem.GetActiveTeammateAgents))]
    internal static class FixtureTeammates
    {
        private static bool Prefix(ref List<GOAPAgent> __result)
        {
            __result = !SmokeFixtures.ShowAgents ? new List<GOAPAgent>() :
                SmokeFixtures.LongSquad ? SmokeFixtures.LongAgents : SmokeFixtures.Agents;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompanionConfigStore), nameof(CompanionConfigStore.Save))]
    internal static class SuppressConfigWrites
    {
        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    internal static class SuppressBackgroundGameplay
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(SmartCompanionsController), "Update");
        private static bool Prefix() => false;
    }

    [HarmonyPatch]
    internal static class CatchUiFallbacks
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(SmartCompanionsUI), "ReportUiFailure");
        private static void Prefix(string context, Exception error)
        {
            SmokeFixtures.UiFailures++;
            UiSmokePlugin.Logger.LogError("[UiSmoke] UI fallback is a test failure: " + context + ": " + error);
        }
    }
}
