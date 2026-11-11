using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    public sealed partial class UiSmokeRunner
    {
        private Vector2Int[] _profiles = { new Vector2Int(0, 0) };
        private float[] _preferences = { 0f };
        private float _originalUiScale;
        private static readonly FieldInfo UiScalePreference = typeof(CompanionConfigStore).GetField("UiScale", BindingFlags.Static | BindingFlags.Public);
        private int _profile = -1;
        private float _resolutionRequestedAt;
        private bool _originalHudVisible, _originalHudCollapsed;
        private static readonly FieldInfo HudRect = Ui.GetField("_currentHudBadgeRect", Flags);

        private void InitializeProfiles()
        {
            _originalHudVisible = CompanionConfigStore.ShowHudBadge;
            _originalHudCollapsed = CompanionConfigStore.HudBadgeCollapsed;
            _originalUiScale = (float)UiScalePreference.GetValue(null);
            if (Environment.GetCommandLineArgs().Contains("--sc-ui-matrix"))
            {
                _profiles = new[] {
                    new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
                    new Vector2Int(3440, 1440), new Vector2Int(1024, 768),
                    new Vector2Int(1920, 1080), new Vector2Int(1920, 1080),
                    new Vector2Int(1280, 720), new Vector2Int(1280, 720)
                };
                _preferences = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0.75f, 2f, 0.75f, 2f };
            }
            else _profiles = new[] { new Vector2Int(Screen.width, Screen.height) };
            UiSmokePlugin.Logger.LogInfo($"[UiSmoke] Planned {Cases} cases across {_profiles.Length} real render resolutions.");
        }

        private void PrepareResolution(int profile)
        {
            if (_profile == profile) return;
            _profile = profile;
            UiScalePreference.SetValue(null, _preferences[profile]);
            _resolutionRequestedAt = Time.realtimeSinceStartup;
            var requested = _profiles[profile];
            // Resizing the actual Unity backbuffer also verifies changes while F8 is open.
            // Never label a clamped desktop/window size as a successful requested size.
            if (Screen.width != requested.x || Screen.height != requested.y)
                Screen.SetResolution(requested.x, requested.y, FullScreenMode.Windowed);
            UiSmokePlugin.Logger.LogInfo($"[UiSmoke] Requested actual backbuffer {requested.x}x{requested.y}.");
        }

        private bool ResolutionReady()
        {
            if (_profile < 0) return false;
            var requested = _profiles[_profile];
            if (Screen.width == requested.x && Screen.height == requested.y) return true;
            if (Time.realtimeSinceStartup - _resolutionRequestedAt > 8f)
                Fail(new Exception($"Requested resolution {requested.x}x{requested.y} unavailable; actual {Screen.width}x{Screen.height}. Not counted as passed."));
            return false;
        }

        private static float ReadUiFloat(string name)
        {
            var property = Ui.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (property == null) throw new MissingMemberException(Ui.FullName, name);
            return (float)property.GetValue(null);
        }

        private void VerifyLayoutAndPointer()
        {
            float scale = ReadUiFloat("EffectiveUiScale");
            float width = ReadUiFloat("ViewportWidth"), height = ReadUiFloat("ViewportHeight");
            if (!(scale > 0f) || float.IsInfinity(scale) || float.IsNaN(scale))
                throw new Exception("UI scale is non-finite or non-positive.");
            if (Mathf.Abs(width * scale - Screen.width) > 1f || Mathf.Abs(height * scale - Screen.height) > 1f)
                throw new Exception("Logical viewport and physical backbuffer disagree.");
            var rect = (Rect)(SmartCompanionsUI.ShowMainWindow ? Window : HudRect).GetValue(null);
            if (rect.width <= 0f || rect.height <= 0f || rect.xMin < -0.5f || rect.yMin < -0.5f ||
                rect.xMax > width + 0.5f || rect.yMax > height + 0.5f)
                throw new Exception($"Interactive window offscreen: rect={rect}, viewport={width}x{height}, scale={scale}.");

            // Input polled by native CharacterInput is in physical pixels, not GUI units.
            var center = new Vector2(rect.center.x * scale, rect.center.y * scale);
            if (!SmartCompanionsUI.IsPointerOverOverlay(center))
                throw new Exception($"Scaled pointer missed center: point={center}, rect={rect}, scale={scale}.");
            // Validate both close edges, not only a center that might overlap at wrong scales.
            foreach (var logical in new[] { new Vector2(rect.xMin + 1f, rect.yMin + 1f),
                new Vector2(rect.xMax - 1f, rect.yMax - 1f) })
                if (!SmartCompanionsUI.IsPointerOverOverlay(logical * scale))
                    throw new Exception($"Scaled pointer missed window edge {logical} at scale={scale}.");
            var outside = new Vector2(-10f, -10f);
            if (SmartCompanionsUI.IsPointerOverOverlay(outside))
                throw new Exception("Overlay captured a pointer outside every window.");
            if (ContainerFixtures.Active)
            {
                var chest = (Rect)Ui.GetField("_currentChestBarRect", Flags).GetValue(null);
                if (chest.width <= 0f || chest.xMin < 0f || chest.yMin < 0f || chest.xMax > width || chest.yMax > height)
                    throw new Exception($"Chest overlay is offscreen or absent: {chest}.");
                if (!SmartCompanionsUI.IsPointerOverOverlay(chest.center * scale))
                    throw new Exception("Scaled pointer missed native chest overlay.");
            }
            if (_case % Variants == 5 && (Vector2)SquadScroll.GetValue(null) == Vector2.zero)
                throw new Exception("Long-squad scroll did not retain a positive offset.");
            if (_case % Variants == 8 && (Vector2)SettingsScroll.GetValue(null) == Vector2.zero)
                throw new Exception("Settings scroll did not reach below the initial viewport.");
        }

        private void RestoreDisplayOptions()
        {
            CompanionConfigStore.ShowHudBadge = _originalHudVisible;
            CompanionConfigStore.HudBadgeCollapsed = _originalHudCollapsed;
            UiScalePreference.SetValue(null, _originalUiScale);
        }
    }
}
