using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    [BepInPlugin("zedzone.smartcompanions.uismoke", "Smart Companions UI Smoke", "1.0.0")]
    [BepInDependency("zedzone.smartcompanions")]
    public sealed class UiSmokePlugin : BasePlugin
    {
        internal static bool Enabled;
        internal static BepInEx.Logging.ManualLogSource Logger;
        public override void Load()
        {
            Enabled = Environment.GetCommandLineArgs().Contains("--sc-ui-smoke");
            if (!Enabled) return;
            Logger = Log;
            new Harmony("zedzone.smartcompanions.uismoke").PatchAll(typeof(UiSmokePlugin).Assembly);
            if (Environment.GetCommandLineArgs().Contains("--sc-ui-os-probe")) AddComponent<OsProbeRunner>();
            else AddComponent<UiSmokeRunner>();
            Log.LogInfo("[UiSmoke] Enabled. Main-menu fixtures only; no save is loaded or written.");
        }
    }

    public sealed partial class UiSmokeRunner : MonoBehaviour
    {
        public UiSmokeRunner(IntPtr ptr) : base(ptr) { }
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static Type Ui => typeof(SmartCompanionsUI);
        private static readonly FieldInfo Tab = Ui.GetField("_activeTab", Flags);
        private static readonly FieldInfo Window = Ui.GetField("_mainWinRect", Flags);
        private static readonly FieldInfo Centered = Ui.GetField("_mainWinCentered", Flags);
        private static readonly FieldInfo SquadScroll = Ui.GetField("_scrollSquad", Flags);
        private static readonly FieldInfo SettingsScroll = Ui.GetField("_scrollSettings", Flags);
        private static readonly FieldInfo ProfilePreviewOpen = Ui.GetField("ShowSquadProfilePreview", Flags);
        private readonly ModLanguage[] _languages = { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese };
        private ModLanguage _originalLanguage;
        private string _output;
        private int _case = -1, _repaints, _passed, _startCase;
        private float _nextAt;
        private bool _initialized, _failed;
        private bool _nativeOnly;
        private float _menuSceneAt = -1f;
        private float _closingAt = -1f;
        private const int Variants = 10; // 2.0.0: variant 9 = Work tab
        private int Cases => _profiles.Length * _languages.Length * Variants;

        private void Update()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            try
            {
                if (!_initialized)
                {
                    if (Time.realtimeSinceStartup < 5f) return;
                    // Keep every fixture run in the settled main-menu scene. Starting
                    // in GameDataInit can destroy its inert actors during scene load.
                    {
                        if (!GameController.initFinishFlag || Time.realtimeSinceStartup < 9f) return;
                        if (_menuSceneAt < 0f)
                        {
                            _menuSceneAt=Time.realtimeSinceStartup;
                            Application.runInBackground=true;
                            GameController.instance.LoadScene("MainMenu");
                            return;
                        }
                        if(Time.realtimeSinceStartup-_menuSceneAt < 4f) return;
                    }
                    _initialized = true;
                    _originalLanguage = CompanionConfigStore.LanguageMode;
                    _output = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--sc-ui-smoke-output="));
                    _output = _output == null ? Path.Combine(BepInEx.Paths.BepInExRootPath, "UiSmoke") : _output.Substring(21);
                    Directory.CreateDirectory(_output);
                    InitializeProfiles();
                    var startArg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--sc-ui-start-case="));
                    if (startArg != null && (!int.TryParse(startArg.Split('=')[1], out _startCase) || _startCase < 0 || _startCase >= Cases))
                        throw new Exception("Invalid matrix start case.");
                    _case = _startCase - 1;
                    ProtocolRegression.Run();
                    TacticalRegression.Run();
                    SpecializationRegression.Run();
                    ScaleAndPrivacyRegression.Run();
                    GarrisonRegression.Run();
                    FieldTacticsRegression.Run();
                    SurvivalRegression.Run();
                    IndustryRegression.Run();
                    if (Environment.GetCommandLineArgs().Contains("--sc-native-menu-only"))
                    {
                        NativeMenuRegression.VerifyAllLanguages();
                        NativeMenuRegression.DumpStoredCommands();
                        InventoryRegression.Run();
                        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;i++)
                            UiSmokePlugin.Logger.LogInfo("[Scenes] "+UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                        _nativeOnly=true;
                        return;
                    }
                    SmokeFixtures.Create();
                    ContainerFixtures.Create();
                    SmartCompanionsUI.ShowMainWindow = true;
                    if (!SmartCompanionsInput.BlockPlayerControls || !SmartCompanionsInput.BlockPlayerMouse)
                        throw new Exception("Open F8 does not block player input.");
                    UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS open F8 blocks player controls and mouse.");
                    SmokeFixtures.VerifyNativeInputGates();
                    InputRegression.VerifyOpen();
                    _nextAt = Time.realtimeSinceStartup;
                }
                if(_nativeOnly)
                {
                    if(NativeMenuScreenshots.Tick()) { enabled=false;Application.Quit(); }
                    return;
                }
                if (_failed || Time.realtimeSinceStartup < _nextAt) return;
                if (_case >= Cases - 1)
                {
                    if (_repaints < 3) return;
                    if (_closingAt < 0f)
                    {
                        SmartCompanionsUI.ShowMainWindow = false;
                        _closingAt = Time.realtimeSinceStartup;
                        InputRegression.VerifyClosed();
                        return;
                    }
                    if (!InputRegression.VerifyClosed())
                    {
                        if (Time.realtimeSinceStartup - _closingAt > 3f)
                            throw new Exception("Input capture did not release within three seconds after closing F8.");
                        return;
                    }
                    int images = Directory.GetFiles(_output, "case-*.png").Count(p => new FileInfo(p).Length > 1000);
                    if (images != Cases - _startCase) throw new Exception($"Expected {Cases - _startCase} rendered screenshots, found {images}.");
                    if (SmokeFixtures.UiFailures != 0) throw new Exception($"UI used {SmokeFixtures.UiFailures} error fallbacks; see earlier full-stack reports.");
                    UiSmokePlugin.Logger.LogInfo($"[UiSmoke] PASS {_passed}/{Cases - _startCase} actual repaint cases; screenshots={images}; fixture NPCs={SmokeFixtures.Agents.Count}.");
                    Finish();
                    return;
                }
                if (_case >= _startCase && _repaints < 3) return;
                _case++;
                _repaints = 0;
                int language = (_case / Variants) % _languages.Length;
                int variant = _case % Variants;
                PrepareResolution(_case / (Variants * _languages.Length));
                CompanionConfigStore.LanguageMode = _languages[language];
                SmartCompanionsUI.InvalidateStyles();
                Tab.SetValue(null, variant == 9 ? 4 : variant == 8 ? 3 : variant >= 4 ? 0 : variant);
                CompanionConfigStore.BaseWorkPaused = variant == 9 && language == 1; // English work case shows the paused header
                ProfilePreviewOpen?.SetValue(null, variant == 9 && language == 0); // Russian work case shows the squad profile diff preview
                CampGarrisonSystem.PendingDismissConfirmGuid = (variant == 0 && SmokeFixtures.Agents.Count > 1)
                    ? SmokeFixtures.Agents[1].npcData.npcGUID : "";
                SquadScroll.SetValue(null, new Vector2(0f, variant == 5 ? 100000f : 0f));
                SettingsScroll.SetValue(null, new Vector2(0f, variant == 8 ? 100000f : 0f));
                Centered.SetValue(null, false);
                SmokeFixtures.ShowAgents = variant != 4;
                SmokeFixtures.LongSquad = variant == 5;
                SmartCompanionsUI.ShowMainWindow = variant != 6 && variant != 7;
                CompanionConfigStore.ShowHudBadge = true;
                CompanionConfigStore.HudBadgeCollapsed = variant == 7;
                ContainerFixtures.SetVisible(variant == 6 || variant == 7, variant == 7);
                _nextAt = Time.realtimeSinceStartup + 1.0f;
            }
            catch (Exception e) { Fail(e); }
        }

        private void OnGUI()
        {
            if (!_initialized || _failed || _case < _startCase || _case >= Cases || _closingAt >= 0f || !ResolutionReady()) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            int oldDepth = GUI.depth;
            var gc = GameController.instance;
            var oldPlayer = gc.playerCharacter;
            try
            {
                // Deliberately dirty the ambient state. The production wrapper must
                // normalize it for drawing and restore it when returning.
                var dirtyMatrix = Matrix4x4.identity;
                dirtyMatrix.m00 = 0.7f;
                dirtyMatrix.m11 = 0.7f;
                dirtyMatrix.m03 = 13f;
                dirtyMatrix.m13 = 17f;
                GUI.matrix = dirtyMatrix;
                GUI.color = Color.yellow;
                GUI.contentColor = Color.red;
                GUI.backgroundColor = Color.green;
                GUI.enabled = false;
                GUI.depth = -1000;
                // Read the engine-normalized colors back (HDR/linear conversion can
                // quantize a supplied Color), then compare restoration to that state.
                var expectedColor = GUI.color;
                var expectedContent = GUI.contentColor;
                var expectedBackground = GUI.backgroundColor;
                float expectedScale = GUI.matrix.m00;
                gc.playerCharacter = SmokeFixtures.Agents[0].humanCharacter;
                if(!Il2CppSafe.IsAliveUnity(gc.playerCharacter))throw new Exception("UI fixture was destroyed during a scene transition.");
                SmartCompanionsUI.DrawAllGUI();
                VerifyLayoutAndPointer();
                if (GUI.enabled || Mathf.Abs(GUI.matrix.m00 - expectedScale) > 0.0001f || GUI.matrix.m03 != 13f ||
                    GUI.matrix.m13 != 17f || GUI.depth != -1000 || GUI.color != expectedColor ||
                    GUI.contentColor != expectedContent || GUI.backgroundColor != expectedBackground)
                    throw new Exception($"Production DrawAllGUI did not restore GUI state: enabled={GUI.enabled}, scale={GUI.matrix.m00}, colorB={GUI.color.b}, contentG={GUI.contentColor.g}, backgroundR={GUI.backgroundColor.r}.");
                if (Event.current.type != EventType.Repaint) return;
                _repaints++;
                bool firstInProfile = _case % (Variants * _languages.Length) == 0;
                if (_repaints != 3 && !(firstInProfile && _repaints == 4)) return;
                string name = $"case-{_case:000}-{Screen.width}x{Screen.height}-pref{_preferences[_profile]:0.00}-{_languages[(_case / Variants) % _languages.Length]}-{_case % Variants}";
                if (_repaints == 3) ScreenCapture.CaptureScreenshot(Path.Combine(_output, name + ".png"));
                if (firstInProfile && _repaints == 3) return;
                if (firstInProfile) VerifyScaledInteraction();
                _passed++;
                UiSmokePlugin.Logger.LogInfo($"[UiSmoke] PASS actual repaint {name}, NPCs={SmokeFixtures.VisibleAgentCount}, uiScale={ReadUiFloat("EffectiveUiScale"):0.000}.");
            }
            catch (Exception e) { Fail(e); }
            finally
            {
                gc.playerCharacter = oldPlayer;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
                GUI.enabled = oldEnabled;
                GUI.depth = oldDepth;
            }
        }

        [HideFromIl2Cpp]
        private void Fail(Exception e)
        {
            _failed = true;
            UiSmokePlugin.Logger.LogError("[UiSmoke] FAIL " + (e.InnerException ?? e));
            Finish();
        }

        private void Finish()
        {
            CompanionConfigStore.LanguageMode = _originalLanguage;
            RestoreDisplayOptions();
            ContainerFixtures.Cleanup();
            SmartCompanionsUI.ShowMainWindow = false;
            SmokeFixtures.Cleanup();
            enabled = false;
            Application.Quit();
        }
    }
}
