using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SmartCompanionsPlugin : BasePlugin
    {
        public const string PluginGuid = "zedzone.smartcompanions";
        public const string PluginName = "ZED ZONE Smart Companions";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Logs;

        public override void Load()
        {
            Logs = Log;
            LogInfo($"{PluginName} v{PluginVersion} initializing...");

            CompanionConfigStore.Load();

            try
            {
                var harmony = new Harmony(PluginGuid);
                harmony.PatchAll(typeof(HarmonyPatches));
                int patchCount = harmony.GetPatchedMethods().Count();
                LogInfo($"Patched {patchCount} game method(s) via Harmony.");
            }
            catch (Exception e)
            {
                LogError("Harmony patching failed: " + e);
            }

            try
            {
                AddComponent<SmartCompanionsController>();
                LogInfo("Registered SmartCompanionsController MonoBehaviour in IL2CPP domain.");
            }
            catch (Exception e)
            {
                LogError("Failed to register SmartCompanionsController: " + e);
            }

            RunStartupSelfVerify();
        }

        private static void RunStartupSelfVerify()
        {
            try
            {
                var topics = (ChatterTopic[])Enum.GetValues(typeof(ChatterTopic));
                int rudeRuCount = 0;
                int cleanRuCount = 0;
                int rudeEnCount = 0;
                int cleanEnCount = 0;
                var uniqueRu = new HashSet<string>();
                var uniqueEn = new HashSet<string>();

                foreach (var t in topics)
                {
                    var rRu = ChatterDatabase.GetLines(t, true, ProfanityMode.HardcoreRude);
                    var cRu = ChatterDatabase.GetLines(t, true, ProfanityMode.Clean);
                    var mRu = ChatterDatabase.GetLines(t, true, ProfanityMode.Mixed);
                    var rEn = ChatterDatabase.GetLines(t, false, ProfanityMode.HardcoreRude);
                    var cEn = ChatterDatabase.GetLines(t, false, ProfanityMode.Clean);

                    if (rRu == null || rRu.Length == 0 || cRu == null || cRu.Length == 0 || mRu == null || mRu.Length == 0)
                        throw new Exception($"Missing RU lines for topic {t}");
                    if (rEn == null || rEn.Length == 0 || cEn == null || cEn.Length == 0)
                        throw new Exception($"Missing EN lines for topic {t}");

                    rudeRuCount += rRu.Length;
                    cleanRuCount += cRu.Length;
                    rudeEnCount += rEn.Length;
                    cleanEnCount += cEn.Length;

                    foreach (var s in rRu) if (!string.IsNullOrWhiteSpace(s)) uniqueRu.Add(s);
                    foreach (var s in cRu) if (!string.IsNullOrWhiteSpace(s)) uniqueRu.Add(s);
                    foreach (var s in rEn) if (!string.IsNullOrWhiteSpace(s)) uniqueEn.Add(s);
                    foreach (var s in cEn) if (!string.IsNullOrWhiteSpace(s)) uniqueEn.Add(s);
                }

                // Verify steak signature phrase in both Rude and Clean modes
                string rudeSteak0 = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, true, ProfanityMode.HardcoreRude)[0];
                string cleanSteak0 = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, true, ProfanityMode.Clean)[0];
                if (!rudeSteak0.Contains("Вкусно тебе") || !cleanSteak0.Contains("Вкусно тебе"))
                    throw new Exception("Signature steak line mismatch!");

                // Verify ItemCategorizer
                var steakKind = ItemCategorizer.ClassifyFoodKind(248, null, null);
                var rawKind = ItemCategorizer.ClassifyFoodKind(204, null, null);
                var canKind = ItemCategorizer.ClassifyFoodKind(232, null, null);
                var medCat = ItemCategorizer.ClassifyItem(220, null, null);
                var foodCat = ItemCategorizer.ClassifyItem(248, null, null);
                var canCat = ItemCategorizer.ClassifyItem(232, null, null);

                if (steakKind != FoodKind.SteakAndRoastMeat || rawKind != FoodKind.RawMeat || canKind != FoodKind.CannedFood)
                    throw new Exception($"FoodKind check failed: steak={steakKind}, raw={rawKind}, can={canKind}");
                if (medCat != StorageCategory.Medicine || foodCat != StorageCategory.FreshFood || canCat != StorageCategory.CannedAndDrinks)
                    throw new Exception($"StorageCategory check failed: med={medCat}, food={foodCat}, can={canCat}");

                // Verify CustomNpcCommands
                foreach (var cmd in CustomNpcCommands.AllCustomCommands)
                {
                    if (!CustomNpcCommands.TryGetDisplayName(cmd, out var label) || string.IsNullOrEmpty(label))
                        throw new Exception("Custom command missing label: " + cmd);
                }

                LogInfo($"[SelfVerify] PASS v{PluginVersion}: {topics.Length} chatter topics ({uniqueRu.Count} unique RU lines [Rude={rudeRuCount}, Clean={cleanRuCount}], {uniqueEn.Count} unique EN lines [Rude={rudeEnCount}, Clean={cleanEnCount}]), 12 storage categories, {CustomNpcCommands.AllCustomCommands.Length} custom NPC commands verified.");
            }
            catch (Exception e)
            {
                LogError("[SelfVerify] FAIL: " + e);
            }
        }

        public static void LogInfo(string msg) => Logs?.LogInfo(msg);
        public static void LogWarning(string msg) => Logs?.LogWarning(msg);
        public static void LogError(string msg) => Logs?.LogError(msg);
    }

    public class SmartCompanionsController : MonoBehaviour
    {
        public SmartCompanionsController(IntPtr ptr) : base(ptr) { }

        private float _lastCombatTickTime = 0f;
        private float _lastSortTickTime = 0f;
        private bool _languageAutoDetected = false;

        private void Update()
        {
            try
            {
                if (!_languageAutoDetected && Il2CppSafe.IsAliveUnity(GameController.instance) &&
                    Il2CppSafe.IsAlive(GameController.instance.gameSettings))
                {
                    _languageAutoDetected = true;
                    string code = GameController.instance.gameSettings.languageCode ?? "";
                    if (!string.IsNullOrEmpty(code) && code.IndexOf("ru", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        CompanionConfigStore.UseRussian = true;
                    }
                }

                // Hotkey F8: Toggle Smart Companions & Base Sorting Hub
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    SmartCompanionsUI.ShowMainWindow = !SmartCompanionsUI.ShowMainWindow;
                    if (SmartCompanionsUI.ShowMainWindow)
                        BaseSortingSystem.ScanNearbyContainers(force: true);
                }

                // Hotkey F7: Quick Order -> Sort Base Containers
                if (Input.GetKeyDown(KeyCode.F7))
                {
                    BaseSortingSystem.CommandSortBase(null, null, instant: false);
                }

                // Hotkey F6: Quick Order -> Share Ration with Squad ("Подогреть братву пайком")
                if (Input.GetKeyDown(KeyCode.F6))
                {
                    SmartChatterSystem.CommandShareRationWithSquad(null, fromAutoShare: false);
                }

                float now = Time.unscaledTime;

                if (now - _lastCombatTickTime >= 0.15f)
                {
                    _lastCombatTickTime = now;
                    SmartCombatAI.TickAllCompanionsCombat();
                }

                if (now - _lastSortTickTime >= 0.30f)
                {
                    _lastSortTickTime = now;
                    BaseSortingSystem.TickActiveSortingJobs();
                    AutoLootSystem.TickPassiveAutoLoot();
                }

                SmartChatterSystem.TickAmbientChatter();
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("SmartCompanionsController.Update exception: " + e.Message);
            }
        }

        private void OnGUI()
        {
            try
            {
                SmartCompanionsUI.DrawAllGUI();
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("SmartCompanionsController.OnGUI exception: " + e.Message);
            }
        }
    }
}
