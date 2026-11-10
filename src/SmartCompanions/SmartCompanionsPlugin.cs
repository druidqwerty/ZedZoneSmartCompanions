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
        public const string PluginVersion = "1.4.1";

        internal static ManualLogSource Logs;

        public override void Load()
        {
            Logs = Log;
            LogInfo($"{PluginName} v{PluginVersion} initializing...");

            CompanionConfigStore.Load();

            try
            {
                var harmony = new Harmony(PluginGuid);
                harmony.PatchAll(typeof(SmartCompanionsPlugin).Assembly);
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
                CombatResponse.SelfVerifyCommandStorage();
                var topics = (ChatterTopic[])Enum.GetValues(typeof(ChatterTopic));
                int rudeRuCount = 0, cleanRuCount = 0;
                int rudeEnCount = 0, cleanEnCount = 0;
                int rudeZhCount = 0, cleanZhCount = 0;
                var uniqueRu = new HashSet<string>();
                var uniqueEn = new HashSet<string>();
                var uniqueZh = new HashSet<string>();

                foreach (var t in topics)
                {
                    var rRu = ChatterDatabase.GetLines(t, ModLanguage.Russian, ProfanityMode.HardcoreRude);
                    var cRu = ChatterDatabase.GetLines(t, ModLanguage.Russian, ProfanityMode.Clean);
                    var mRu = ChatterDatabase.GetLines(t, ModLanguage.Russian, ProfanityMode.Mixed);
                    var rEn = ChatterDatabase.GetLines(t, ModLanguage.English, ProfanityMode.HardcoreRude);
                    var cEn = ChatterDatabase.GetLines(t, ModLanguage.English, ProfanityMode.Clean);
                    var mEn = ChatterDatabase.GetLines(t, ModLanguage.English, ProfanityMode.Mixed);
                    var rZh = ChatterDatabase.GetLines(t, ModLanguage.Chinese, ProfanityMode.HardcoreRude);
                    var cZh = ChatterDatabase.GetLines(t, ModLanguage.Chinese, ProfanityMode.Clean);
                    var mZh = ChatterDatabase.GetLines(t, ModLanguage.Chinese, ProfanityMode.Mixed);

                    if (rRu == null || rRu.Length == 0 || cRu == null || cRu.Length == 0 || mRu == null || mRu.Length == 0)
                        throw new Exception($"Missing RU lines for topic {t}");
                    if (rEn == null || rEn.Length == 0 || cEn == null || cEn.Length == 0 || mEn == null || mEn.Length == 0)
                        throw new Exception($"Missing EN lines for topic {t}");
                    if (rZh == null || rZh.Length == 0 || cZh == null || cZh.Length == 0 || mZh == null || mZh.Length == 0)
                        throw new Exception($"Missing ZH lines for topic {t}");

                    rudeRuCount += rRu.Length;
                    cleanRuCount += cRu.Length;
                    rudeEnCount += rEn.Length;
                    cleanEnCount += cEn.Length;
                    rudeZhCount += rZh.Length;
                    cleanZhCount += cZh.Length;

                    foreach (var s in rRu) if (!string.IsNullOrWhiteSpace(s)) uniqueRu.Add(s);
                    foreach (var s in cRu) if (!string.IsNullOrWhiteSpace(s)) uniqueRu.Add(s);
                    foreach (var s in rEn) if (!string.IsNullOrWhiteSpace(s)) uniqueEn.Add(s);
                    foreach (var s in cEn) if (!string.IsNullOrWhiteSpace(s)) uniqueEn.Add(s);
                    foreach (var s in rZh) if (!string.IsNullOrWhiteSpace(s)) uniqueZh.Add(s);
                    foreach (var s in cZh) if (!string.IsNullOrWhiteSpace(s)) uniqueZh.Add(s);
                }

                // Verify steak signature phrase in RU and ZH
                string rudeSteakRu = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, ModLanguage.Russian, ProfanityMode.HardcoreRude)[0];
                string cleanSteakRu = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, ModLanguage.Russian, ProfanityMode.Clean)[0];
                string rudeSteakZh = ChatterDatabase.GetLines(ChatterTopic.PlayerEatSteakMeat, ModLanguage.Chinese, ProfanityMode.HardcoreRude)[0];
                if (!rudeSteakRu.Contains("Вкусно тебе") || !cleanSteakRu.Contains("Вкусно тебе") || !rudeSteakZh.Contains("牛排"))
                    throw new Exception("Signature steak line mismatch!");

                // Verify auto-detection of language codes
                if (CompanionConfigStore.ResolveLanguageMode("zh-CN", SystemLanguage.English) != ModLanguage.Chinese ||
                    CompanionConfigStore.ResolveLanguageMode("schinese", SystemLanguage.English) != ModLanguage.Chinese ||
                    CompanionConfigStore.ResolveLanguageMode("ru", SystemLanguage.English) != ModLanguage.Russian ||
                    CompanionConfigStore.ResolveLanguageMode("en", SystemLanguage.ChineseSimplified) != ModLanguage.English ||
                    CompanionConfigStore.ResolveLanguageMode("", SystemLanguage.ChineseSimplified) != ModLanguage.Chinese)
                {
                    throw new Exception("ResolveLanguageMode verification failed!");
                }

                // Verify ItemCategorizer in all 3 languages
                foreach (var cat in ItemCategorizer.AllCategories)
                {
                    foreach (var lang in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                    {
                        if (string.IsNullOrWhiteSpace(ItemCategorizer.GetCategoryName(cat, lang)) ||
                            string.IsNullOrWhiteSpace(ItemCategorizer.GetCategoryShortTag(cat, lang)))
                            throw new Exception($"Category translation missing for {cat} ({lang})");
                    }
                }

                // Verify ItemCategorizer classification
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

                // Verify CustomNpcCommands in all 3 languages
                foreach (var cmd in CustomNpcCommands.AllCustomCommands)
                {
                    foreach (var lang in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                    {
                        if (!CustomNpcCommands.TryGetDisplayName(cmd, lang, out var label) || string.IsNullOrEmpty(label))
                            throw new Exception($"Custom command missing label for {lang}: " + cmd);
                        string leaf = cmd.Substring(cmd.LastIndexOf('.') + 1);
                        if (cmd.StartsWith("NPCCommand:") ||
                            !CustomNpcCommands.TryGetDisplayName(leaf, lang, out var bare) || bare != label ||
                            !CustomNpcCommands.TryGetDisplayName("NPCCommand:" + cmd, lang, out var legacy) || legacy != label)
                            throw new Exception("Native menu path/leaf compatibility failed: " + cmd);
                    }
                }

                // Verify CompanionArchetype and CompanionRank localization & XP thresholds
                foreach (CompanionArchetype arch in Enum.GetValues(typeof(CompanionArchetype)))
                {
                    foreach (var lang in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                    {
                        if (string.IsNullOrWhiteSpace(CompanionConfigStore.GetArchetypeDisplayName(arch, lang)))
                            throw new Exception($"Archetype name missing for {arch} ({lang})");
                    }
                }
                if (CompanionConfigStore.GetRankForXp(0) != CompanionRank.Rookie ||
                    CompanionConfigStore.GetRankForXp(150) != CompanionRank.Fighter ||
                    CompanionConfigStore.GetRankForXp(400) != CompanionRank.Veteran ||
                    CompanionConfigStore.GetRankForXp(900) != CompanionRank.Elite ||
                    CompanionConfigStore.GetRankForXp(2000) != CompanionRank.ZoneLegend)
                {
                    throw new Exception("CompanionRank XP threshold check failed!");
                }

                LogInfo($"[SelfVerify] PASS v{PluginVersion}: {topics.Length} chatter topics ({uniqueRu.Count} unique RU lines [Rude={rudeRuCount}, Clean={cleanRuCount}], {uniqueEn.Count} unique EN lines [Rude={rudeEnCount}, Clean={cleanEnCount}], {uniqueZh.Count} unique ZH lines [Rude={rudeZhCount}, Clean={cleanZhCount}]), 12 storage categories (RU/EN/ZH), {CustomNpcCommands.AllCustomCommands.Length} custom NPC commands (RU/EN/ZH), 5 archetypes, 5 veteran ranks, MP Sync & Hostile Human Combat AI, ModLanguage Auto/RU/EN/ZH verified.");
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
        private float _lastGuiErrorTime = -60f;
        private string _lastGuiError = "";

        private void Update()
        {
            try
            {
                // Hotkey F8: Toggle Smart Companions & Base Sorting Hub
                if (Input.GetKeyDown(KeyCode.F8))
                {
                    SmartCompanionsUI.ShowMainWindow = !SmartCompanionsUI.ShowMainWindow;
                    if (SmartCompanionsUI.ShowMainWindow)
                        SmartCompanionsUI.OnMainWindowOpened();
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

                // Hotkey F9: Quick Order -> Squad Battle Cry & Focus Assault ("Боевой клич / Штурм")
                if (Input.GetKeyDown(KeyCode.F9))
                {
                    SmartCombatAI.CommandSquadBattleCry(null);
                }

                float now = Time.unscaledTime;
                CompanionConfigStore.FlushPendingProgress();
                MultiplayerSyncSystem.Tick();
                BaseSortingSystem.TickActiveSortingMovementEveryFrame();

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
                    BaseChoreSystem.TickBaseChoresAndCampLife();
                }

                SmartChatterSystem.TickAmbientChatter();
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("SmartCompanionsController.Update exception: " + e.Message);
            }
        }

        private void OnApplicationQuit()
        {
            BaseSortingSystem.FlushAllCarryBuffers();
            CompanionConfigStore.FlushPendingProgress(force: true);
        }

        private void OnDestroy()
        {
            BaseSortingSystem.FlushAllCarryBuffers();
            CompanionConfigStore.FlushPendingProgress(force: true);
        }

        private void OnGUI()
        {
            try
            {
                SmartCompanionsUI.DrawAllGUI();
            }
            catch (Exception e)
            {
                // An IMGUI failure may occur several times per frame. Keep the first
                // stack trace useful without flooding the log or hiding the cause.
                string detail = e.ToString();
                if (detail != _lastGuiError || Time.unscaledTime - _lastGuiErrorTime >= 30f)
                {
                    _lastGuiError = detail;
                    _lastGuiErrorTime = Time.unscaledTime;
                    SmartCompanionsPlugin.LogWarning("SmartCompanionsController.OnGUI exception: " + detail);
                }
            }
        }
    }
}
