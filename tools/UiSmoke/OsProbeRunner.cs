using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // Interactive fixture mode for a separate exact-PID Win32 message driver.
    public sealed class OsProbeRunner : MonoBehaviour
    {
        public OsProbeRunner(IntPtr ptr) : base(ptr) { }
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo Tab = typeof(SmartCompanionsUI).GetField("_activeTab", Flags);
        private static readonly FieldInfo Window = typeof(SmartCompanionsUI).GetField("_mainWinRect", Flags);
        private static readonly FieldInfo Scroll = typeof(SmartCompanionsUI).GetField("_scrollSettings", Flags);
        private string _output;
        private float _menuAt = -1f, _readyAt, _lastState;
        private bool _ready, _finished, _savedHud;
        private float _savedScale;
        private ModLanguage _savedLanguage;

        private void Update()
        {
            try
            {
                if (_finished) return;
                if (!_ready)
                {
                    if (!GameController.initFinishFlag || Time.realtimeSinceStartup < 9f) return;
                    if (_menuAt < 0f)
                    {
                        _menuAt = Time.realtimeSinceStartup;
                        Application.runInBackground = true;
                        GameController.instance.LoadScene("MainMenu");
                        return;
                    }
                    if (Time.realtimeSinceStartup - _menuAt < 4f) return;
                    _output = Argument("--sc-ui-smoke-output=") ?? Path.Combine(Paths.BepInExRootPath, "UiOsProbe");
                    Directory.CreateDirectory(_output);
                    _savedHud = CompanionConfigStore.ShowHudBadge;
                    _savedScale = CompanionConfigStore.UiScale;
                    _savedLanguage = CompanionConfigStore.LanguageMode;
                    if (!float.TryParse(Argument("--sc-ui-scale=") ?? "0.75", NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                        throw new Exception("Invalid --sc-ui-scale value.");
                    CompanionConfigStore.UiScale = scale;
                    CompanionConfigStore.ShowHudBadge = false;
                    CompanionConfigStore.LanguageMode = ModLanguage.English;
                    SmokeFixtures.Create();
                    ContainerFixtures.Create();
                    ContainerFixtures.SetVisible(false, false);
                    SmartCompanionsUI.ShowMainWindow = true;
                    CompanionConfigStore.MainWinPosX = CompanionConfigStore.MainWinPosY = -1f;
                    SmartCompanionsUI.ResetWindowRectsToDefault();
                    Tab.SetValue(null, 0);
                    Scroll.SetValue(null, Vector2.zero);
                    _readyAt = Time.realtimeSinceStartup;
                    // Native menu startup can override the CLI dimensions from saved preferences.
                    Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                    _ready = true;
                    UiSmokePlugin.Logger.LogInfo("[OsProbe] Ready for external exact-PID PostMessage driver; no saves loaded.");
                }
                if (SmokeFixtures.UiFailures != 0) throw new Exception("UI fallback occurred in OS probe.");
                if ((Screen.width != 1920 || Screen.height != 1080) && Time.realtimeSinceStartup - _readyAt > 10f)
                    throw new Exception($"OS probe requested1920x1080, actual={Screen.width}x{Screen.height}.");
                if (File.Exists(Path.Combine(_output, "finish.txt"))) { Finish(); return; }
                if (Time.realtimeSinceStartup - _readyAt > 120f) throw new Exception("OS probe timed out after 120 seconds.");
            }
            catch (Exception error)
            {
                UiSmokePlugin.Logger.LogError("[OsProbe] FAIL " + error);
                Finish();
            }
        }

        private void OnGUI()
        {
            if (!_ready || _finished || Screen.width != 1920 || Screen.height != 1080) return;
            var gc = GameController.instance;
            var originalPlayer = gc.playerCharacter;
            try
            {
                gc.playerCharacter = SmokeFixtures.Agents[0].humanCharacter;
                var incoming = Event.current;
                if (incoming.rawType == EventType.MouseDown || incoming.rawType == EventType.MouseUp ||
                    incoming.rawType == EventType.MouseDrag || incoming.rawType == EventType.ScrollWheel)
                    UiSmokePlugin.Logger.LogInfo($"[OsProbe] Native event raw={incoming.rawType} type={incoming.type} position={incoming.mousePosition} delta={incoming.delta} matrix={GUI.matrix.m00:0.000}.");
                SmartCompanionsUI.DrawAllGUI();
                if (Event.current.type != EventType.Repaint) return;
                if (Time.realtimeSinceStartup - _lastState < 0.1f) return;
                _lastState = Time.realtimeSinceStartup;
                var rect = (Rect)Window.GetValue(null);
                float scale = SmartCompanionsUI.EffectiveUiScale;
                var scroll = (Vector2)Scroll.GetValue(null);
                string state = FormattableString.Invariant($"{{\"frame\":{Time.frameCount},\"width\":{Screen.width},\"height\":{Screen.height},\"scale\":{scale},\"x\":{rect.x},\"y\":{rect.y},\"w\":{rect.width},\"h\":{rect.height},\"tab\":{Tab.GetValue(null)},\"scroll\":{scroll.y},\"open\":{SmartCompanionsUI.ShowMainWindow.ToString().ToLowerInvariant()},\"dragging\":{SmartCompanionsUI.IsDraggingAnyWindow.ToString().ToLowerInvariant()}}}");
                string temporary = Path.Combine(_output, "state.tmp");
                File.WriteAllText(temporary, state);
                File.Move(temporary, Path.Combine(_output, "state.json"), true);
                string capture = Path.Combine(_output, "capture.txt");
                if (File.Exists(capture))
                {
                    string name = File.ReadAllText(capture).Trim();
                    if (name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c == '-'))
                        ScreenCapture.CaptureScreenshot(Path.Combine(_output, name + ".png"));
                    File.Delete(capture);
                }
            }
            catch (Exception error) { UiSmokePlugin.Logger.LogError("[OsProbe] FAIL " + error); Finish(); }
            finally { gc.playerCharacter = originalPlayer; }
        }

        [HideFromIl2Cpp]
        private static string Argument(string prefix) => Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith(prefix))?.Substring(prefix.Length);

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            SmartCompanionsUI.ShowMainWindow = false;
            CompanionConfigStore.ShowHudBadge = _savedHud;
            CompanionConfigStore.UiScale = _savedScale;
            CompanionConfigStore.LanguageMode = _savedLanguage;
            ContainerFixtures.Cleanup();
            SmokeFixtures.Cleanup();
            enabled = false;
            Application.Quit();
        }
    }

    [HarmonyPatch]
    internal static class OsProbeScrollTrace
    {
        private static readonly bool Enabled = Environment.GetCommandLineArgs().Contains("--sc-ui-os-probe");
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(SmartCompanionsUI), "BeginScrollArea");
        private static void Prefix(Rect viewport)
        {
            if (!Enabled) return;
            var ev = Event.current;
            if (ev != null && ev.rawType == EventType.ScrollWheel)
                UiSmokePlugin.Logger.LogInfo($"[OsProbe] Inside BeginScrollArea raw={ev.rawType} type={ev.type} position={ev.mousePosition} delta={ev.delta} viewport={viewport} contains={viewport.Contains(ev.mousePosition)} matrix={GUI.matrix.m00:0.000} effective={SmartCompanionsUI.EffectiveUiScale:0.000}.");
        }
    }
}
