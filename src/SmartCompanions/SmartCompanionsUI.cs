using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static partial class SmartCompanionsUI
    {
        private static bool _showMainWindow;
        public static bool ShowMainWindow
        {
            get => _showMainWindow;
            set
            {
                if (_showMainWindow && !value) ReleaseUiGestures();
                _showMainWindow = value;
            }
        }
        public static bool CollapseChestBar = false;
        private static int _activeTab = 0; // 0=Squad, 1=BaseBoxes, 2=Chatter, 3=Settings
        private static string _selectedBoxIdInHub = "";

        public static void SelectContainerInHub(string boxId)
        {
            if (!string.IsNullOrEmpty(boxId))
                _selectedBoxIdInHub = boxId;
        }

        public static void OnMainWindowOpened()
        {
            var containers = BaseSortingSystem.ScanNearbyContainers(force: true);
            var openPanel = LootInventoryPanel.instance;
            var openInv = LootInventoryPanel.CurrentOpenInventoryData;
            if (Il2CppSafe.IsAliveUnity(openPanel) && openPanel.gameObject.activeInHierarchy &&
                Il2CppSafe.IsAlive(openInv) && !openInv.isPlayerInventory)
            {
                _selectedBoxIdInHub = BaseSortingSystem.GetContainerStableId(openInv);
            }
            else if (containers.Count > 0 && containers[0] != null)
            {
                // Automatically select the closest container next to the player
                _selectedBoxIdInHub = containers[0].ContainerId;
            }
        }
        private static Vector2 _scrollSquad;
        private static Vector2 _scrollBoxes;
        private static Vector2 _scrollChatter;
        private static Vector2 _scrollSettings;

        private static bool _stylesBuilt = false;
        private static GUIStyle _winStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _btnStyle;
        private static GUIStyle _catOnStyle;
        private static GUIStyle _catOffStyle;
        private static GUIStyle _bannerStyle;

        private static Texture2D _texWinBg;
        private static Texture2D _texCardBg;
        private static Texture2D _texBtnNormal;
        private static Texture2D _texBtnHover;
        private static Texture2D _texCatOn;
        private static Texture2D _texCatOff;
        private static Texture2D _texBannerBg;

        private static Rect _mainWinRect = new Rect(0, 0, 960f, 660f);
        private static bool _mainWinCentered = false;

        private static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(4);
            for (int i = 0; i < 4; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static void InvalidateStyles()
        {
            _stylesBuilt = false;
        }

        private static void EnsureStyles()
        {
            if (_stylesBuilt && _texWinBg != null) return;
            DestroyStyleTextures();


            _texWinBg    = MakeTex(new Color(0.08f, 0.10f, 0.12f, 0.96f));
            _texCardBg   = MakeTex(new Color(0.13f, 0.16f, 0.20f, 0.94f));
            _texBtnNormal= MakeTex(new Color(0.20f, 0.25f, 0.32f, 0.95f));
            _texBtnHover = MakeTex(new Color(0.28f, 0.38f, 0.48f, 0.98f));
            _texCatOn    = MakeTex(new Color(0.12f, 0.36f, 0.33f, 0.98f));
            _texCatOff   = MakeTex(new Color(0.18f, 0.20f, 0.24f, 0.90f));
            _texBannerBg = MakeTex(new Color(0.10f, 0.28f, 0.18f, 0.95f));

            Font dynFont = GetInterfaceFont();

            _winStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };
            _winStyle.normal.background = _texWinBg;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6)
            };
            _boxStyle.normal.background = _texCardBg;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = false,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = new Color(0.45f, 0.90f, 0.82f);
            if (dynFont != null) _headerStyle.font = dynFont;

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = false,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _subHeaderStyle.normal.textColor = new Color(0.65f, 0.90f, 1.0f);
            if (dynFont != null) _subHeaderStyle.font = dynFont;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = new Color(0.90f, 0.92f, 0.95f);
            if (dynFont != null) _labelStyle.font = dynFont;

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                wordWrap = true,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4)
            };
            _btnStyle.normal.background = _texBtnNormal;
            _btnStyle.hover.background = _texBtnHover;
            _btnStyle.active.background = _texCatOn;
            _btnStyle.normal.textColor = Color.white;
            _btnStyle.hover.textColor = new Color(1f, 0.95f, 0.7f);
            if (dynFont != null) _btnStyle.font = dynFont;

            _catOnStyle = new GUIStyle(_btnStyle);
            _catOnStyle.normal.background = _texCatOn;
            _catOnStyle.normal.textColor = new Color(0.85f, 1f, 0.85f);

            _catOffStyle = new GUIStyle(_btnStyle);
            _catOffStyle.normal.background = _texCatOff;
            _catOffStyle.normal.textColor = new Color(0.72f, 0.75f, 0.80f);

            _bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(10, 10, 6, 6)
            };
            _bannerStyle.normal.background = _texBannerBg;
            _bannerStyle.normal.textColor = new Color(0.85f, 1f, 0.88f);
            if (dynFont != null) _bannerStyle.font = dynFont;
            _stylesBuilt = true;
        }

        public static void DrawAllGUI()
        {
            // Other IMGUI behaviours can leave tint, scale or disabled state behind.
            // Draw in logical units and restore the caller's GUI state afterward.
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try
            {
                UpdateDisplayMetrics();
                GUI.matrix = Matrix4x4.Scale(new Vector3(EffectiveUiScale, EffectiveUiScale, 1f));
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                DrawAllGuiContent();
            }
            finally
            {
                // A tab/panel may disappear before its own drag handler sees MouseUp.
                if (Event.current != null && Event.current.rawType == EventType.MouseUp)
                    ReleaseUiGestures();
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
                GUI.enabled = oldEnabled;
            }
        }

        private static void DrawAllGuiContent()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAliveUnity(gc.playerCharacter)) return;

            EnsureStyles();

            if (!_mainWinCentered)
            {
                _mainWinCentered = true;
                if (CompanionConfigStore.MainWinPosX >= 0f && CompanionConfigStore.MainWinPosY >= 0f)
                {
                    _mainWinRect.x = CompanionConfigStore.MainWinPosX;
                    _mainWinRect.y = CompanionConfigStore.MainWinPosY;
                }
                else
                {
                    _mainWinRect.x = Mathf.Max(20f, (ViewportWidth - _mainWinRect.width) * 0.5f);
                    _mainWinRect.y = Mathf.Max(20f, (ViewportHeight - _mainWinRect.height) * 0.5f);
                }
            }
            var clampedMain = ClampWindowPosition(new Vector2(_mainWinRect.x, _mainWinRect.y), new Vector2(_mainWinRect.width, _mainWinRect.height));
            _mainWinRect.x = clampedMain.x;
            _mainWinRect.y = clampedMain.y;

            // 1. Status banner at top-center if active
            if (!string.IsNullOrEmpty(BaseSortingSystem.LastSortStatusBanner) && Time.unscaledTime < BaseSortingSystem.LastSortStatusBannerUntil)
            {
                float bw = Mathf.Min(720f, ViewportWidth - 40f);
                var bRect = new Rect((ViewportWidth - bw) * 0.5f, 10f, bw, 32f);
                GUI.Box(bRect, UiSafe(BaseSortingSystem.LastSortStatusBanner), _bannerStyle);
            }

            // 2. Mini HUD bar at top-right (unless main window is open)
            if (CompanionConfigStore.ShowHudBadge && !ShowMainWindow)
            {
                DrawHudBadge();
            }

            // 3. Docked Container Category Bar whenever a non-player container is open in LootInventoryPanel!
            var openInv = LootInventoryPanel.CurrentOpenInventoryData;
            if (!ShowMainWindow && openInv != null && !openInv.isPlayerInventory && LootInventoryPanel.instance != null && LootInventoryPanel.instance.gameObject.activeInHierarchy)
            {
                DrawOpenContainerCategoryPanel(openInv);
            }

            // 4. Main Smart Companions & Base Logistics Hub Window (F8)
            if (ShowMainWindow)
            {
                DrawMainHubWindow();
            }
        }

        private static void DrawHudBadge()
        {
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            bool collapsed = CompanionConfigStore.HudBadgeCollapsed;
            float w = collapsed ? 92f : 368f;
            float h = 32f;

            float defX = Mathf.Clamp(ViewportWidth * 0.28f, 250f, Mathf.Max(250f, ViewportWidth - w - 340f));
            float defY = 10f;
            Vector2 pos = (CompanionConfigStore.HudBadgePosX >= 0f && CompanionConfigStore.HudBadgePosY >= 0f)
                ? new Vector2(CompanionConfigStore.HudBadgePosX, CompanionConfigStore.HudBadgePosY)
                : new Vector2(defX, defY);
            pos = ClampWindowPosition(pos, new Vector2(w, h));
            _currentHudBadgeRect = new Rect(pos.x, pos.y, w, h);

            GUI.Box(_currentHudBadgeRect, GUIContent.none, _boxStyle);
            var dragRect = new Rect(pos.x + 2f, pos.y + 3f, 24f, 26f);
            GUI.Box(dragRect, "::", _btnStyle);
            HandleWindowDrag(3, dragRect, ref pos, new Vector2(w, h), p =>
            {
                CompanionConfigStore.HudBadgePosX = p.x;
                CompanionConfigStore.HudBadgePosY = p.y;
                _currentHudBadgeRect.x = p.x;
                _currentHudBadgeRect.y = p.y;
            });

            if (collapsed)
            {
                if (GUI.Button(new Rect(pos.x + 28f, pos.y + 3f, 36f, 26f), "F8", _catOnStyle))
                {
                    ShowMainWindow = !ShowMainWindow;
                    if (ShowMainWindow) OnMainWindowOpened();
                }
                if (GUI.Button(new Rect(pos.x + 66f, pos.y + 3f, 22f, 26f), "+", _btnStyle))
                {
                    CompanionConfigStore.HudBadgeCollapsed = false;
                    CompanionConfigStore.Save();
                }
                return;
            }

            string label = CompanionConfigStore.L(
                $" Отряд: {teammates.Count} [F8]",
                $" Squad ({teammates.Count}) [F8]",
                $" 队友 ({teammates.Count}) [F8]");
            if (GUI.Button(new Rect(pos.x + 28f, pos.y + 3f, 146f, 26f), label, _btnStyle))
            {
                ShowMainWindow = !ShowMainWindow;
                if (ShowMainWindow) OnMainWindowOpened();
            }
            if (GUI.Button(new Rect(pos.x + 177f, pos.y + 3f, 68f, 26f), CompanionConfigStore.L(" Сорт.", " Sort", " 整理"), _btnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, null, instant: false);
            }
            if (GUI.Button(new Rect(pos.x + 248f, pos.y + 3f, 66f, 26f), CompanionConfigStore.L(" Лут", " Loot", " 搜刮"), _btnStyle))
            {
                AutoLootSystem.CommandSweepNearbyLoot(null, 20f);
            }
            if (GUI.Button(new Rect(pos.x + 317f, pos.y + 3f, 22f, 26f), "-", _btnStyle))
            {
                CompanionConfigStore.HudBadgeCollapsed = true;
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(pos.x + 341f, pos.y + 3f, 22f, 26f), "x", _btnStyle))
            {
                CompanionConfigStore.ShowHudBadge = false;
                CompanionConfigStore.Save();
            }
        }

        private static void DrawOpenContainerCategoryPanel(InventoryData openInv)
        {
            var lang = CompanionConfigStore.EffectiveLanguage;
            string boxId = BaseSortingSystem.GetContainerStableId(openInv);
            string boxTitle = LootInventoryPanel.instance != null && !string.IsNullOrEmpty(LootInventoryPanel.instance.lootObjectName)
                ? LootInventoryPanel.instance.lootObjectName
                : (!string.IsNullOrEmpty(openInv.inventoryTitleName)
                    ? openInv.inventoryTitleName
                    : CompanionConfigStore.L(lang, "Открытый ящик", "Open Container", "当前储物箱"));

            var rule = CompanionConfigStore.GetOrCreateRule(boxId, boxTitle);

            float panelW = Mathf.Min(780f, ViewportWidth - 40f);
            float panelH = 202f;
            float defX = (ViewportWidth - panelW) * 0.5f;
            float defY = Mathf.Max(60f, ViewportHeight - panelH - 92f);

            Vector2 size = CollapseChestBar ? new Vector2(240f, 34f) : new Vector2(panelW, panelH);
            Vector2 pos = (CompanionConfigStore.ChestBarPosX >= 0f && CompanionConfigStore.ChestBarPosY >= 0f)
                ? new Vector2(CompanionConfigStore.ChestBarPosX, CompanionConfigStore.ChestBarPosY)
                : new Vector2(CollapseChestBar ? (ViewportWidth - 240f) * 0.5f : defX, CollapseChestBar ? ViewportHeight - 42f : defY);
            pos = ClampWindowPosition(pos, size);
            _currentChestBarRect = new Rect(pos.x, pos.y, size.x, size.y);

            if (CollapseChestBar)
            {
                GUI.Box(_currentChestBarRect, GUIContent.none, _winStyle);
                var miniDragRect = new Rect(pos.x + 4f, pos.y + 2f, 28f, 28f);
                GUI.Box(miniDragRect, "::", _btnStyle);
                HandleWindowDrag(2, miniDragRect, ref pos, size, p =>
                {
                    CompanionConfigStore.ChestBarPosX = p.x;
                    CompanionConfigStore.ChestBarPosY = p.y;
                    _currentChestBarRect.x = p.x;
                    _currentChestBarRect.y = p.y;
                });
                if (GUI.Button(new Rect(pos.x + 36f, pos.y + 2f, 200f, 28f),
                    CompanionConfigStore.L(lang,
                        "Категории ящика [v]",
                        "Chest categories [v]",
                        "箱子分类 [v]"), _btnStyle))
                {
                    CollapseChestBar = false;
                }
                return;
            }

            var rect = _currentChestBarRect;
            GUI.Box(rect, GUIContent.none, _winStyle);

            // Row 1: Drag Handle + Header + Drop-off toggle + Priority + Collapse
            float rx = rect.x + 10f;
            float ry = rect.y + 8f;

            var headerDragRect = new Rect(rx, ry, 248f, 28f);
            GUI.Label(headerDragRect,
                CompanionConfigStore.L(lang,
                    $":: {boxTitle}",
                    $":: {boxTitle}",
                    $":: {boxTitle}"), _subHeaderStyle);
            HandleWindowDrag(2, headerDragRect, ref pos, size, p =>
            {
                CompanionConfigStore.ChestBarPosX = p.x;
                CompanionConfigStore.ChestBarPosY = p.y;
                _currentChestBarRect.x = p.x;
                _currentChestBarRect.y = p.y;
            });

            var dropStyle = rule.IsDropoffSource ? _catOnStyle : _catOffStyle;
            string dropLabel = (rule.IsDropoffSource ? "[x] " : "[ ] ") +
                CompanionConfigStore.L(lang, " Приёмник", " Drop-off", " 中转箱");
            if (GUI.Button(new Rect(rx + 255f, ry, 185f, 28f), dropLabel, dropStyle))
            {
                rule.IsDropoffSource = !rule.IsDropoffSource;
                rule.IsMarkedByPlayer = rule.IsDropoffSource || rule.Categories != StorageCategory.None;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }

            string prioLabel = CompanionConfigStore.L(lang, $"Приор.: {rule.Priority}", $"Priority: {rule.Priority}", $"优先级: *{rule.Priority}");
            if (GUI.Button(new Rect(rx + 446f, ry, 110f, 28f), prioLabel, _btnStyle))
            {
                rule.Priority = (rule.Priority % 5) + 1;
                rule.IsMarkedByPlayer = true;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }

            if (GUI.Button(new Rect(rx + 562f, ry, 118f, 28f), CompanionConfigStore.L(lang, "Ящики [F8]", "Boxes [F8]", "箱子 [F8]"), _btnStyle))
            {
                ShowMainWindow = true;
                _activeTab = 1;
                _selectedBoxIdInHub = boxId;
            }

            if (GUI.Button(new Rect(rect.xMax - 76f, ry, 66f, 28f), CompanionConfigStore.L(lang, "^", "^", "^"), _btnStyle))
            {
                CollapseChestBar = true;
            }

            // Row 2 & 3: 12 Category Toggle Buttons (6 per row)
            ry += 34f;
            DrawCategoryGrid(new Rect(rx, ry, panelW - 20f, 98f), rule, lang);

            // Row 4: Quick Presets & Companion Sort Actions
            ry += 106f;
            float bw = (panelW - 20f - 36f) / 7f;
            if (GUI.Button(new Rect(rx, ry, bw, 28f), CompanionConfigStore.L(lang, " Арсенал", " Arsenal", " 军火库"), _btnStyle))
            {
                rule.Categories = StorageCategory.MeleeWeapons | StorageCategory.RangedWeapons | StorageCategory.AmmoAndMags;
                rule.IsDropoffSource = false;
                rule.IsMarkedByPlayer = true;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(rx + (bw + 6f) * 1, ry, bw, 28f), CompanionConfigStore.L(lang, " Провизия", " Food/Drink", " 食品饮水"), _btnStyle))
            {
                rule.Categories = StorageCategory.FreshFood | StorageCategory.CannedAndDrinks;
                rule.IsDropoffSource = false;
                rule.IsMarkedByPlayer = true;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(rx + (bw + 6f) * 2, ry, bw, 28f), CompanionConfigStore.L(lang, " Медицина", " Medical", " 医疗品"), _btnStyle))
            {
                rule.Categories = StorageCategory.Medicine;
                rule.IsDropoffSource = false;
                rule.IsMarkedByPlayer = true;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(rx + (bw + 6f) * 3, ry, bw, 28f), CompanionConfigStore.L(lang, " Мастерская", " Workshop", " 工坊材料"), _btnStyle))
            {
                rule.Categories = StorageCategory.Materials | StorageCategory.ToolsAndRepair | StorageCategory.VehicleAndPower;
                rule.IsDropoffSource = false;
                rule.IsMarkedByPlayer = true;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(rx + (bw + 6f) * 4, ry, bw, 28f), CompanionConfigStore.L(lang, " Сбросить", " Clear", " 清空"), _btnStyle))
            {
                rule.Categories = StorageCategory.None;
                rule.IsDropoffSource = false;
                rule.IsMarkedByPlayer = false;
                MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                CompanionConfigStore.Save();
            }
            if (GUI.Button(new Rect(rx + (bw + 6f) * 5, ry, bw * 2f + 6f, 28f),
                CompanionConfigStore.L(lang, "Сортировать [F7]", "Sort base [F7]", "整理基地 [F7]"), _catOnStyle))
            {
                BaseSortingSystem.CommandSortBase(null, openInv, instant: false);
            }

            // Consume mouse clicks inside this panel so clicking category buttons doesn't click world underneath
            if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) &&
                rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
            }
        }

        private static void DrawCategoryGrid(Rect area, ContainerRuleProfile rule, ModLanguage lang)
        {
            var cats = ItemCategorizer.AllCategories;
            int cols = 4;
            float gapX = 5f;
            float gapY = 4f;
            float cellW = (area.width - gapX * (cols - 1)) / cols;
            float cellH = 30f;

            for (int i = 0; i < cats.Length; i++)
            {
                var cat = cats[i];
                int r = i / cols;
                int c = i % cols;
                float cx = area.x + c * (cellW + gapX);
                float cy = area.y + r * (cellH + gapY);

                bool isOn = (rule.Categories & cat) != 0;
                string prefix = isOn ? "[x] " : "[ ] ";
                string text = prefix + ItemCategorizer.GetCategoryShortTag(cat, lang).Trim('[', ']');

                if (GUI.Button(new Rect(cx, cy, cellW, cellH), text, isOn ? _catOnStyle : _catOffStyle))
                {
                    if (isOn) rule.Categories &= ~cat;
                    else rule.Categories |= cat;
                    rule.IsMarkedByPlayer = rule.IsDropoffSource || rule.Categories != StorageCategory.None;
                    MultiplayerSyncSystem.SyncBoxRuleChange(rule);
                    CompanionConfigStore.Save();
                }
            }
        }
    }
}
