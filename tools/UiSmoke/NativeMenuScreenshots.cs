using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class NativeMenuScreenshots
    {
        private static readonly ModLanguage[] Languages = { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese };
        private static readonly List<GameObject> Panels = new List<GameObject>();
        private static ModLanguage _originalLanguage;
        private static NPCCommandMenu _menu;
        private static bool _oldEnabled, _oldActive, _oldCanvasActive, _oldOverride;
        private static int _oldOrder, _index = -1, _startFrame, _captureFrame = -1;
        private static string _directory;
        private static float _capturedAt;

        // Call once per Update until true. This creates the game's own menu panels,
        // rather than a reproduction in IMGUI or a screenshot of text-only assertions.
        internal static bool Tick()
        {
            if (_index < 0)
            {
                VerifyStoredCommands();
                _originalLanguage = CompanionConfigStore.LanguageMode;
                _directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "NativeMenu132");
                Directory.CreateDirectory(_directory);
                _menu = NPCCommandMenu.Instance ?? NPCCommandMenu.CreateSingleton();
                _oldEnabled = _menu.enabled;
                _oldActive = _menu.gameObject.activeSelf;
                _oldCanvasActive = _menu.rootCanvas.gameObject.activeSelf;
                _oldOrder = _menu.rootCanvas.sortingOrder;
                _oldOverride = _menu.rootCanvas.overrideSorting;
                _menu.gameObject.SetActive(true);
                _menu.rootCanvas.gameObject.SetActive(true);
                _menu.enabled = false; // Prevent the normal mouse-away timeout during capture.
                _menu.rootCanvas.overrideSorting = true;
                _menu.rootCanvas.sortingOrder = 32760;
                _index = 0;
                Build();
                return false;
            }
            if (_index >= Languages.Length) return true;
            if (Time.frameCount < _startFrame + 2) return false;
            if (_captureFrame < 0)
            {
                string path = Path.Combine(_directory, "native-menu-" + Languages[_index] + ".png");
                ScreenCapture.CaptureScreenshot(path);
                UiSmokePlugin.Logger.LogInfo("[NativeMenu] Captured actual root/Basic/Combat/OneTime panels: " + path);
                _captureFrame = Time.frameCount;
                _capturedAt = Time.realtimeSinceStartup;
                return false;
            }
            // The screenshot is written at the end of frame, so leave panels alive.
            if (Time.frameCount < _captureFrame + 2) return false;
            if (Time.realtimeSinceStartup - _capturedAt < 1f) return false;
            ClearPanels();
            _index++;
            if (_index < Languages.Length)
            {
                Build();
                return false;
            }
            foreach (var language in Languages)
            {
                var file = new FileInfo(Path.Combine(_directory, "native-menu-" + language + ".png"));
                if (!file.Exists || file.Length < 1000) throw new Exception("Native menu screenshot missing: " + language);
            }
            CompanionConfigStore.LanguageMode = _originalLanguage;
            _menu.enabled = _oldEnabled;
            _menu.rootCanvas.overrideSorting = _oldOverride;
            _menu.rootCanvas.sortingOrder = _oldOrder;
            _menu.rootCanvas.gameObject.SetActive(_oldCanvasActive);
            _menu.gameObject.SetActive(_oldActive);
            UiSmokePlugin.Logger.LogInfo("[NativeMenu] PASS three actual native-menu screenshots written.");
            return true;
        }

        private static void Build()
        {
            CompanionConfigStore.LanguageMode = Languages[_index];
            var tree = NativeMenuRegression.DumpAndVerify(true);
            Panels.Add(NativeMenuRegression.RenderPanel(_menu, tree, Vector2.zero));
            foreach (var group in tree.children)
                if (group.name == "Basic" || group.name == "Combat" || group.name == "OneTime")
                    Panels.Add(NativeMenuRegression.RenderPanel(_menu, group, Vector2.zero));
            Canvas.ForceUpdateCanvases();
            float scale = Mathf.Max(0.1f, _menu.rootCanvas.scaleFactor);
            float totalWidth = 0f;
            foreach (var panel in Panels) totalWidth += panel.GetComponent<RectTransform>().rect.width * scale + 24f;
            float x = Mathf.Max(20f, (Screen.width - totalWidth) * 0.5f);
            float top = Screen.height * 0.78f;
            foreach (var panel in Panels)
            {
                var rect = panel.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0f, 1f);
                rect.position = new Vector3(x, top, 0f);
                x += rect.rect.width * scale + 24f;
            }
            _startFrame = Time.frameCount;
            _captureFrame = -1;
        }

        private static void ClearPanels()
        {
            foreach (var panel in Panels) if (panel != null) UnityEngine.Object.Destroy(panel);
            Panels.Clear();
        }

        private static void VerifyStoredCommands()
        {
            var npc = new GOAPNPCData { npcGUID = "SC-NATIVE-COMMAND-ASSERT", characterData = new CharacterData() };
            foreach (string command in new[] { "FireAtWill", "ProtectPlayer", "StopFire" })
            {
                npc.ExecuteNPCCommand("Combat." + command);
                if (npc.combatCommand != command) throw new Exception("Unexpected native combat field: " + npc.combatCommand);
            }
            foreach (string command in new[] { "RangedWeaponOnly", "MeleeWeaponOnly", "NoWeaponPreference" })
            {
                npc.ExecuteNPCCommand("WeaponPreference." + command);
                if (npc.weaponPreference != command) throw new Exception("Unexpected native weapon field: " + npc.weaponPreference);
            }
            npc.ExecuteNPCCommand("Basic.FollowPlayer");
            if (npc.basicCommand != "FollowPlayer") throw new Exception("Unexpected native basic field: " + npc.basicCommand);
            UiSmokePlugin.Logger.LogInfo("[NativeMenu] PASS seven native command executions store bare action names.");
        }
    }
}
