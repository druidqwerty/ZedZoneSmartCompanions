using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class SmartCompanionsUI
    {
        // All UI rectangles are logical units; only the input gate accepts screen pixels.
        public static float EffectiveUiScale { get; private set; } = 1f;
        public static float ViewportWidth => Screen.width / EffectiveUiScale;
        public static float ViewportHeight => Screen.height / EffectiveUiScale;
        private static int _displayWidth, _displayHeight;
        private static float _displayPreference = -1f;

        public static float CalculateUiScale(int width, int height, float preference)
        {
            float fit = Mathf.Min(Mathf.Max(1, width) / 968f, Mathf.Max(1, height) / 668f);
            float wanted = preference > 0f && !float.IsInfinity(preference)
                ? Mathf.Clamp(preference, 0.75f, 2f)
                : Mathf.Clamp(height / 1080f, 1f, 2f);
            return Mathf.Max(0.001f, Mathf.Min(wanted, fit));
        }

        private static void UpdateDisplayMetrics()
        {
            float preference = CompanionConfigStore.UiScale;
            if (_displayWidth == Screen.width && _displayHeight == Screen.height &&
                _displayPreference == preference) return;
            float oldWidth = _displayWidth > 0 ? _displayWidth / EffectiveUiScale : CompanionConfigStore.UiViewportWidth;
            float oldHeight = _displayHeight > 0 ? _displayHeight / EffectiveUiScale : CompanionConfigStore.UiViewportHeight;
            _displayWidth = Screen.width;
            _displayHeight = Screen.height;
            _displayPreference = preference;
            EffectiveUiScale = CalculateUiScale(Screen.width, Screen.height, preference);
            // Legacy releases saved raw pixel positions without viewport metadata.
            if (oldWidth <= 0f || oldHeight <= 0f) { oldWidth = Screen.width; oldHeight = Screen.height; }
            // Retain the relative placement across live resolution/aspect/scale changes.
            if (oldWidth > 0f && oldHeight > 0f)
            {
                RemapPosition(ref CompanionConfigStore.MainWinPosX, ref CompanionConfigStore.MainWinPosY,
                    new Vector2(960f, 660f), oldWidth, oldHeight);
                RemapPosition(ref CompanionConfigStore.HudBadgePosX, ref CompanionConfigStore.HudBadgePosY,
                    new Vector2(CompanionConfigStore.HudBadgeCollapsed ? 92f : 368f, 32f), oldWidth, oldHeight);
                RemapPosition(ref CompanionConfigStore.ChestBarPosX, ref CompanionConfigStore.ChestBarPosY,
                    CollapseChestBar ? new Vector2(240f, 34f) : new Vector2(780f, 202f), oldWidth, oldHeight);
            }
            CompanionConfigStore.UiViewportWidth = ViewportWidth;
            CompanionConfigStore.UiViewportHeight = ViewportHeight;
            ResetWindowRectsToDefault();
        }

        private static void RemapPosition(ref float x, ref float y, Vector2 size, float oldWidth, float oldHeight)
        {
            if (x < 0f || y < 0f) return;
            x = 4f + Mathf.Clamp01((x - 4f) / Mathf.Max(1f, oldWidth - size.x - 8f)) *
                Mathf.Max(0f, ViewportWidth - size.x - 8f);
            y = 4f + Mathf.Clamp01((y - 4f) / Mathf.Max(1f, oldHeight - size.y - 8f)) *
                Mathf.Max(0f, ViewportHeight - size.y - 8f);
        }

        private static float DrawDisplaySettings(float x, float y, float width, ModLanguage lang)
        {
            GUI.Label(new Rect(x, y, width, 26f), CompanionConfigStore.L(lang,
                "Интерфейс и масштаб", "Interface & scale", "界面与缩放"), _subHeaderStyle);
            y += 32f;
            float[] scales = { 0f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
            float buttonWidth = (width - 36f) / scales.Length;
            for (int i = 0; i < scales.Length; i++)
            {
                string label = i == 0 ? CompanionConfigStore.L(lang, "Авто", "Auto", "自动") : $"{scales[i] * 100f:0}%";
                if (GUI.Button(new Rect(x + i * (buttonWidth + 6f), y, buttonWidth, 30f), label,
                    Mathf.Approximately(CompanionConfigStore.UiScale, scales[i]) ? _catOnStyle : _btnStyle))
                {
                    CompanionConfigStore.UiScale = scales[i];
                    CompanionConfigStore.Save();
                    // Apply on the next GUI pass, never change the matrix midway through a group.
                }
            }
            y += 36f;
            GUI.Label(new Rect(x, y, width, 42f), CompanionConfigStore.L(lang,
                $"Экран: {Screen.width} × {Screen.height}  •  Масштаб: {EffectiveUiScale * 100f:0}%\nМасштаб ограничивается размером экрана, чтобы все кнопки оставались доступны.",
                $"Screen: {Screen.width} × {Screen.height}  •  Scale: {EffectiveUiScale * 100f:0}%\nScale is capped to fit your screen and keep every control accessible.",
                $"屏幕：{Screen.width} × {Screen.height}  •  缩放：{EffectiveUiScale * 100f:0}%\n缩放自动限制为屏幕可容纳的大小，确保所有按钮可用。"), _labelStyle);
            return y + 50f;
        }
    }
}
