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
            AddComponent<UiSmokeRunner>();
            Log.LogInfo("[UiSmoke] Enabled. Main-menu fixtures only; no save is loaded or written.");
        }
    }

    public sealed class UiSmokeRunner : MonoBehaviour
    {
        public UiSmokeRunner(IntPtr ptr) : base(ptr) { }
        private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type Ui = typeof(SmartCompanionsUI);
        private static readonly FieldInfo Tab = Ui.GetField("_activeTab", Flags);
        private static readonly FieldInfo Window = Ui.GetField("_mainWinRect", Flags);
        private static readonly FieldInfo Centered = Ui.GetField("_mainWinCentered", Flags);
        private static readonly FieldInfo SquadScroll = Ui.GetField("_scrollSquad", Flags);
        private readonly ModLanguage[] _languages = { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese };
        private ModLanguage _originalLanguage;
        private string _output;
        private int _case = -1, _repaints, _passed;
        private float _nextAt;
        private bool _initialized, _failed;
        private float _closingAt = -1f;
        private const int Cases = 15;

        private void Update()
        {
            try
            {
                if (!_initialized)
                {
                    if (Time.realtimeSinceStartup < 5f) return;
                    _initialized = true;
                    _originalLanguage = CompanionConfigStore.LanguageMode;
                    _output = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--sc-ui-smoke-output="));
                    _output = _output == null ? Path.Combine(BepInEx.Paths.BepInExRootPath, "UiSmoke") : _output.Substring(21);
                    Directory.CreateDirectory(_output);
                    SmokeFixtures.Create();
                    SmartCompanionsUI.ShowMainWindow = true;
                    if (!SmartCompanionsInput.BlockPlayerControls || !SmartCompanionsInput.BlockPlayerMouse)
                        throw new Exception("Open F8 does not block player input.");
                    UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS open F8 blocks player controls and mouse.");
                    SmokeFixtures.VerifyNativeInputGates();
                    InputRegression.VerifyOpen();
                    _nextAt = Time.realtimeSinceStartup;
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
                    if (images != Cases) throw new Exception($"Expected {Cases} rendered screenshots, found {images}.");
                    if (SmokeFixtures.UiFailures != 0) throw new Exception($"UI used {SmokeFixtures.UiFailures} error fallbacks; see earlier full-stack reports.");
                    UiSmokePlugin.Logger.LogInfo($"[UiSmoke] PASS {_passed}/{Cases} actual repaint cases; screenshots={images}; fixture NPCs={SmokeFixtures.Agents.Count}.");
                    Finish();
                    return;
                }
                if (_case >= 0 && _repaints < 3) return;
                _case++;
                _repaints = 0;
                int language = _case / 5;
                int variant = _case % 5;
                CompanionConfigStore.LanguageMode = _languages[language];
                SmartCompanionsUI.InvalidateStyles();
                Tab.SetValue(null, variant >= 3 ? 0 : variant);
                SquadScroll.SetValue(null, new Vector2(0f, variant == 4 ? 150f : 0f));
                Window.SetValue(null, new Rect(10, 10, 880, 600));
                Centered.SetValue(null, true);
                SmokeFixtures.ShowAgents = variant != 3;
                SmokeFixtures.LongSquad = variant == 4;
                _nextAt = Time.realtimeSinceStartup + 1.2f;
            }
            catch (Exception e) { Fail(e); }
        }

        private void OnGUI()
        {
            if (!_initialized || _failed || _case < 0 || _case >= Cases || _closingAt >= 0f) return;
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
                SmartCompanionsUI.DrawAllGUI();
                if (GUI.enabled || Mathf.Abs(GUI.matrix.m00 - expectedScale) > 0.0001f || GUI.color != expectedColor ||
                    GUI.contentColor != expectedContent || GUI.backgroundColor != expectedBackground)
                    throw new Exception($"Production DrawAllGUI did not restore GUI state: enabled={GUI.enabled}, scale={GUI.matrix.m00}, colorB={GUI.color.b}, contentG={GUI.contentColor.g}, backgroundR={GUI.backgroundColor.r}.");
                if (Event.current.type != EventType.Repaint) return;
                _repaints++;
                if (_repaints != 3) return;
                string name = $"case-{_case:00}-{_languages[_case / 5]}-{_case % 5}";
                ScreenCapture.CaptureScreenshot(Path.Combine(_output, name + ".png"));
                _passed++;
                UiSmokePlugin.Logger.LogInfo($"[UiSmoke] PASS actual repaint {name}, NPCs={SmokeFixtures.VisibleAgentCount}.");
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
            SmartCompanionsUI.ShowMainWindow = false;
            SmokeFixtures.Cleanup();
            enabled = false;
            Application.Quit();
        }
    }
}
