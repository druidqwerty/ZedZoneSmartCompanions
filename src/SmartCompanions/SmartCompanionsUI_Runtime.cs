using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class SmartCompanionsUI
    {
        private static Font _interfaceFont;
        private static int _scrollDragId;
        private static float _scrollDragStartY;
        private static float _scrollDragStartValue;
        private static string _lastUiFailure;
        private static float _lastUiFailureTime;

        public static bool IsPointerOverOverlay(Vector2 position)
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return false;
            if (ShowMainWindow && _mainWinRect.Contains(position)) return true;
            if (!ShowMainWindow && CompanionConfigStore.ShowHudBadge &&
                new Rect(Screen.width - 326f, 12f, 310f, 34f).Contains(position)) return true;
            var panel = LootInventoryPanel.instance;
            var inventory = LootInventoryPanel.CurrentOpenInventoryData;
            if (!Il2CppSafe.IsAliveUnity(panel) || !Il2CppSafe.IsAlive(inventory) || inventory.isPlayerInventory ||
                !panel.gameObject.activeInHierarchy) return false;
            float width = Mathf.Min(780f, Screen.width - 40f);
            return new Rect((Screen.width - width) * 0.5f, 46f, CollapseChestBar ? 380f : width,
                CollapseChestBar ? 32f : 158f).Contains(position);
        }

        private static void DestroyStyleTextures()
        {
            foreach (var texture in new[] { _texWinBg, _texCardBg, _texBtnNormal, _texBtnHover, _texCatOn, _texCatOff, _texBannerBg })
                if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        private static Font GetInterfaceFont()
        {
            if (_interfaceFont != null) return _interfaceFont;
            // Unity stripped both public font factories/constructors in this IL2CPP build.
            // Allocate the managed native object, then use the retained engine icall directly.
            string[] candidates = { "Microsoft YaHei", "Microsoft YaHei UI", "SimSun", "Noto Sans CJK SC", "WenQuanYi Micro Hei", "Arial Unicode MS", "Segoe UI", "Arial" };
            Font latinFallback = null;
            foreach (string name in candidates)
            {
                Font candidate = null;
                try
                {
                    var names = new Il2CppStringArray(new[] { name });
                    candidate = new Font(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr));
                    Font.Internal_CreateDynamicFont(candidate, names, 14);
                    candidate.hideFlags = HideFlags.HideAndDontSave;
                    if (!candidate.HasCharacter('A') || !candidate.HasCharacter('Я'))
                    {
                        UnityEngine.Object.Destroy(candidate);
                        continue;
                    }
                    if (candidate.HasCharacter('队') && candidate.HasCharacter('储') && candidate.HasCharacter('语'))
                    {
                        if (latinFallback != null) UnityEngine.Object.Destroy(latinFallback);
                        _interfaceFont = candidate;
                        SmartCompanionsPlugin.LogInfo("[UI] Font ready: " + name + " (Latin/Cyrillic/Chinese glyphs verified).");
                        return candidate;
                    }
                    if (latinFallback == null) latinFallback = candidate;
                    else UnityEngine.Object.Destroy(candidate);
                }
                catch (Exception error)
                {
                    if (candidate != null) UnityEngine.Object.Destroy(candidate);
                    ReportUiFailure("Font " + name, error);
                }
            }
            _interfaceFont = latinFallback != null ? latinFallback : GUI.skin.font;
            SmartCompanionsPlugin.LogWarning("[UI] No CJK-capable OS font found. Install Microsoft YaHei or Noto Sans CJK for Chinese text.");
            return _interfaceFont;
        }

        private static void ReportUiFailure(string context, Exception error)
        {
            string failure = context + ": " + error;
            if (failure == _lastUiFailure && Time.unscaledTime - _lastUiFailureTime < 30f) return;
            _lastUiFailure = failure;
            _lastUiFailureTime = Time.unscaledTime;
            SmartCompanionsPlugin.LogWarning("[UI] " + failure);
        }

        private static void DrawUiFailure(Rect area, ModLanguage lang)
        {
            GUI.Box(new Rect(area.x + 4f, area.y + 52f, area.width - 8f, 86f), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(area.x + 14f, area.y + 60f, area.width - 28f, 70f), CompanionConfigStore.L(lang,
                "Не удалось прочитать часть данных. Закройте F8 и откройте снова. Подробности записаны в BepInEx/LogOutput.log.",
                "Some data could not be read. Close and reopen F8. Details are recorded in BepInEx/LogOutput.log.",
                "无法读取部分数据。请关闭后重新打开 F8。详情已记录在 BepInEx/LogOutput.log。"), _labelStyle);
        }

        // GUI.BeginScrollView/EndScrollView are unstripped throwing stubs in ZED ZONE.
        // Native clipping groups plus local scroll controls avoid those unavailable methods.
        private static void BeginScrollArea(Rect viewport, ref Vector2 scroll, Rect content, int id)
        {
            float maxScroll = Mathf.Max(0f, content.height - viewport.height);
            scroll.x = 0f;
            scroll.y = Mathf.Clamp(scroll.y, 0f, maxScroll);
            var ev = Event.current;
            if (ev != null && ev.type == EventType.ScrollWheel && viewport.Contains(ev.mousePosition))
            {
                scroll.y = Mathf.Clamp(scroll.y + ev.delta.y * 30f, 0f, maxScroll);
                ev.Use();
            }
            if (maxScroll > 0f)
            {
                const float barWidth = 16f;
                var bar = new Rect(viewport.xMax - barWidth, viewport.y, barWidth, viewport.height);
                if (GUI.Button(new Rect(bar.x, bar.y, bar.width, 20f), "^", _btnStyle))
                    scroll.y = Mathf.Max(0f, scroll.y - 60f);
                if (GUI.Button(new Rect(bar.x, bar.yMax - 20f, bar.width, 20f), "v", _btnStyle))
                    scroll.y = Mathf.Min(maxScroll, scroll.y + 60f);
                var track = new Rect(bar.x, bar.y + 22f, bar.width, Mathf.Max(1f, bar.height - 44f));
                float thumbHeight = Mathf.Min(track.height, Mathf.Max(22f, track.height * viewport.height / content.height));
                float travel = Mathf.Max(1f, track.height - thumbHeight);
                var thumb = new Rect(track.x, track.y + scroll.y / maxScroll * travel, track.width, thumbHeight);
                GUI.Box(track, GUIContent.none, _boxStyle);
                GUI.Box(thumb, GUIContent.none, _catOnStyle);
                if (ev != null)
                {
                    if (ev.type == EventType.MouseDown && ev.button == 0 && track.Contains(ev.mousePosition))
                    {
                        if (!thumb.Contains(ev.mousePosition))
                            scroll.y = Mathf.Clamp((ev.mousePosition.y - track.y - thumbHeight * 0.5f) / travel * maxScroll, 0f, maxScroll);
                        _scrollDragId = id;
                        _scrollDragStartY = ev.mousePosition.y;
                        _scrollDragStartValue = scroll.y;
                        ev.Use();
                    }
                    else if (_scrollDragId == id && ev.type == EventType.MouseDrag)
                    {
                        scroll.y = Mathf.Clamp(_scrollDragStartValue + (ev.mousePosition.y - _scrollDragStartY) / travel * maxScroll, 0f, maxScroll);
                        ev.Use();
                    }
                    else if (_scrollDragId == id && ev.rawType == EventType.MouseUp)
                    {
                        _scrollDragId = 0;
                    }
                }
                viewport.width -= barWidth + 2f;
            }
            GUI.BeginGroup(viewport, GUIContent.none, GUIStyle.none);
            try
            {
                GUI.BeginGroup(new Rect(0f, -scroll.y, content.width, Mathf.Max(content.height, viewport.height)), GUIContent.none, GUIStyle.none);
            }
            catch
            {
                GUI.EndGroup();
                throw;
            }
        }

        private static void EndScrollArea()
        {
            try { GUI.EndGroup(); }
            finally { GUI.EndGroup(); }
        }
    }
}
