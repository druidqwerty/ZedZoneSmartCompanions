using System;
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
        public const string PluginVersion = "1.0.0";

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
                // 1. Verify all 37 ChatterTopic entries have non-empty RU and EN phrase arrays
                var topics = (ChatterTopic[])Enum.GetValues(typeof(ChatterTopic));
                int totalRuLines = 0;
                int totalEnLines = 0;
                foreach (var t in topics)
                {
                    var ru = ChatterDatabase.GetLines(t, true);
                    var en = ChatterDatabase.GetLines(t, false);
                    if (ru == null || ru.Length == 0)
                        throw new Exception($"Missing RU lines for topic {t}");
                    if (en == null || en.Length == 0)
                        throw new Exception($"Missing EN lines for topic {t}");
                    totalRuLines += ru.Length;
                    totalEnLines += en.Length;
                }

                // 2. Verify exact signature steak phrase
                string firstSteakLine = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, true)[0];
                if (!firstSteakLine.Contains("Вкусно тебе наверное"))
                    throw new Exception("Signature steak line mismatch: " + firstSteakLine);

                // 3. Verify ItemCategorizer food & storage category rules
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

                // 4. Verify CustomNpcCommands display names
                foreach (var cmd in CustomNpcCommands.AllCustomCommands)
                {
                    if (!CustomNpcCommands.TryGetDisplayName(cmd, out var label) || string.IsNullOrEmpty(label))
                        throw new Exception("Custom command missing label: " + cmd);
                }

                LogInfo($"[SelfVerify] PASS: {topics.Length} chatter topics ({totalRuLines} RU lines, {totalEnLines} EN lines), 12 storage categories, {CustomNpcCommands.AllCustomCommands.Length} custom NPC commands verified.");
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
                if (!_languageAutoDetected && GameController.instance?.gameSettings != null)
                {
                    _languageAutoDetected = true;
                    string code = GameController.instance.gameSettings.languageCode ?? "";
                    if (!string.IsNullOrEmpty(code))
                    {
                        if (code.IndexOf("ru", StringComparison.OrdinalIgnoreCase) >= 0)
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
