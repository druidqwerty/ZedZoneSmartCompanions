using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// IMGUI Event.Use does not consume Unity's polled mouse input. Gate the game's
    /// local CharacterInput instead; IMGUI and companion AI must keep receiving updates.
    /// </summary>
    public static class SmartCompanionsInput
    {
        private static bool _modalReleasePending;
        private static bool _mouseReleasePending;
        private static int _modalReleasedFrame = -1;
        private static int _mouseReleasedFrame = -1;
        private static float _nextWarningTime;
        private static Vector3 _lastRawMousePos;
        private static bool _wasMainWindowOpen;

        public static void OnFocusRegained()
        {
            try
            {
                if (!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture()) return;

                InvalidateCursorCacheSafe();
                Vector3 mp = Input.mousePosition;
                bool overOverlay = SmartCompanionsUI.IsPointerOverOverlay(new Vector2(mp.x, Screen.height - mp.y));
                bool gameplayCursorAllowed = !IsLockPickingHoldingCursorSafe() &&
                                             !IsMainCanvasHiddenSafe() &&
                                             !IsUsingControllerSafe() &&
                                             IsLocalPlayerAliveSafe();
                if (SmartCompanionsUI.ShowMainWindow || overOverlay || gameplayCursorAllowed)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            catch { }
        }

        public static void TickCursorGuard()
        {
            try
            {
                if (Application.isBatchMode) return;
                if (!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture())
                {
                    _wasMainWindowOpen = false;
                    return;
                }
                if (!IsGameControllerAliveSafe()) return;

                Vector3 mp = Input.mousePosition;
                bool mouseMoved = (mp - _lastRawMousePos).sqrMagnitude > 4f;
                _lastRawMousePos = mp;
                bool mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
                bool uiActive = SmartCompanionsUI.ShowMainWindow ||
                                SmartCompanionsUI.IsPointerOverOverlay(new Vector2(mp.x, Screen.height - mp.y));
                bool wasUsingController = IsUsingControllerSafe();

                if (uiActive || mouseMoved || mouseClicked)
                {
                    SetLastKeyboardMouseActivityTimeSafe(Time.unscaledTime);
                    if (uiActive || (wasUsingController && (mouseMoved || mouseClicked)))
                    {
                        SetIsUsingControllerSafe(false);
                    }
                }

                if (uiActive)
                {
                    if (!Cursor.visible)
                    {
                        Cursor.visible = true;
                        InvalidateCursorCacheSafe();
                    }
                    if (Cursor.lockState == CursorLockMode.Locked)
                        Cursor.lockState = CursorLockMode.None;
                }
                else if (_wasMainWindowOpen)
                {
                    InvalidateCursorCacheSafe();
                    if (!IsLockPickingHoldingCursorSafe() && !IsMainCanvasHiddenSafe() && !IsUsingControllerSafe())
                        Cursor.visible = true;
                }
                else if (IsLocalPlayerAliveSafe() && wasUsingController && (mouseMoved || mouseClicked))
                {
                    if (!IsLockPickingHoldingCursorSafe() && !IsMainCanvasHiddenSafe())
                    {
                        Cursor.visible = true;
                        InvalidateCursorCacheSafe();
                    }
                }

                _wasMainWindowOpen = SmartCompanionsUI.ShowMainWindow;
            }
            catch { }
        }

        public static bool CanProcessWorldHotkeys()
        {
            try
            {
                if (!SmartCompanionsRuntimeGate.IsGameplayWorldReady()) return false;
                if (!IsLocalPlayerAliveSafe()) return false;
                if (IsTypingChatSafe()) return false;
                if (GUIUtility.keyboardControl != 0) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool CanProcessGameplayWorldOrders()
        {
            try
            {
                if (!CanProcessWorldHotkeys()) return false;
                if (SmartCompanionsUI.ShowMainWindow) return false;
                if (IsLockPickingHoldingCursorSafe()) return false;
                if (IsLootInventoryPanelActiveSafe()) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool BlockPlayerControls
        {
            get
            {
                try
                {
                    if (!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture())
                    {
                        _modalReleasePending = false;
                        _mouseReleasePending = false;
                        return false;
                    }
                    return CaptureUntilReleased(SmartCompanionsUI.ShowMainWindow,
                        ref _modalReleasePending, ref _modalReleasedFrame);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool BlockPlayerMouse
        {
            get
            {
                try
                {
                    if (!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture())
                    {
                        _modalReleasePending = false;
                        _mouseReleasePending = false;
                        return false;
                    }
                    if (BlockPlayerControls) return true;
                    var cursor = Input.mousePosition;
                    bool overPanel = SmartCompanionsUI.IsPointerOverOverlay(
                        new Vector2(cursor.x, Screen.height - cursor.y));
                    return CaptureUntilReleased(overPanel,
                        ref _mouseReleasePending, ref _mouseReleasedFrame);
                }
                catch
                {
                    return false;
                }
            }
        }

        // --- Safe wrappers & [NoInlining] interop accessors ---

        private static void InvalidateCursorCacheSafe()
        {
            try { CallInvalidateCursorCache(); }
            catch { }
        }

        private static bool IsLockPickingHoldingCursorSafe()
        {
            try { return ReadLockPickingIsHoldingCursor(); }
            catch { return false; }
        }

        private static bool IsMainCanvasHiddenSafe()
        {
            try { return ReadMainCanvasHidden(); }
            catch { return false; }
        }

        private static bool IsUsingControllerSafe()
        {
            try { return ReadIsUsingController(); }
            catch { return false; }
        }

        private static void SetIsUsingControllerSafe(bool value)
        {
            try { WriteIsUsingController(value); }
            catch { }
        }

        private static void SetLastKeyboardMouseActivityTimeSafe(float time)
        {
            try { WriteLastKeyboardMouseActivityTime(time); }
            catch { }
        }

        private static bool IsTypingChatSafe()
        {
            try { return ReadIsTypingChat(); }
            catch { return false; }
        }

        private static bool IsLootInventoryPanelActiveSafe()
        {
            try { return ReadIsLootInventoryPanelActive(); }
            catch { return false; }
        }

        private static bool IsGameControllerAliveSafe()
        {
            try { return ReadIsGameControllerAlive(); }
            catch { return false; }
        }

        private static bool IsLocalPlayerAliveSafe()
        {
            try { return ReadIsLocalPlayerAlive(); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CallInvalidateCursorCache() => MouseController.InvalidateCursorCache();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadLockPickingIsHoldingCursor() => LockPickingPanel.isHoldingCursor;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadMainCanvasHidden() => HudVisibility.mainCanvasHidden;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsUsingController() => GameInputManager.isUsingController;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteIsUsingController(bool value) => GameInputManager.isUsingController = value;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteLastKeyboardMouseActivityTime(float time) => GameInputManager.lastKeyboardMouseActivityTime = time;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsTypingChat() => ZedZone.Net.NetChatBar.isTypingChat;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsLootInventoryPanelActive()
        {
            var lootPanel = LootInventoryPanel._instance;
            return Il2CppSafe.IsAliveUnity(lootPanel)
                && Il2CppSafe.IsAliveUnity(lootPanel.gameObject)
                && lootPanel.gameObject.activeInHierarchy;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsGameControllerAlive() => Il2CppSafe.IsAliveUnity(GameController.instance);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadIsLocalPlayerAlive()
        {
            var gc = GameController.instance;
            return Il2CppSafe.IsAliveUnity(gc) && Il2CppSafe.IsAliveUnity(gc.playerCharacter);
        }

        private static bool CaptureUntilReleased(bool capture, ref bool pending, ref int releasedFrame)
        {
            if (capture)
            {
                pending = true;
                releasedFrame = -1;
                return true;
            }
            if (!pending) return false;
            // Keep capture when the cursor leaves a button while dragging, or the hub
            // closes on MouseUp. Never turn that UI gesture into a world attack.
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                releasedFrame = -1;
                return true;
            }
            if (releasedFrame < 0) releasedFrame = Time.frameCount;
            if (Time.frameCount <= releasedFrame) return true;
            pending = false;
            return false;
        }

        private static bool LocalPlayerInput(CharacterInput input)
        {
            // CharacterInput is the player's own controller, unlike NPC/GOAP controllers.
            return Il2CppSafe.IsAliveUnity(input);
        }

        private static void StopMovement(CharacterInput input)
        {
            try
            {
                input.movementInput = Vector2.zero;
                var character = input.characterController;
                if (Il2CppSafe.IsAliveUnity(character)) character.movementInput = Vector2.zero;
            }
            catch (Exception e)
            {
                if (Time.unscaledTime < _nextWarningTime) return;
                _nextWarningTime = Time.unscaledTime + 30f;
                SmartCompanionsPlugin.LogWarning("UI input capture: " + e.Message);
            }
        }

        [HarmonyPatch]
        public static class ModalGates
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var m1 = AccessTools.PropertyGetter(typeof(CharacterInput), "stopAllInput");
                if (m1 != null) yield return m1;
                var m2 = AccessTools.PropertyGetter(typeof(CharacterInput), "stopAllInputExceptAreaSwitch");
                if (m2 != null) yield return m2;
            }

            private static bool Prefix(CharacterInput __instance, ref bool __result)
            {
                try
                {
                    if (!LocalPlayerInput(__instance) || !BlockPlayerControls) return true;
                    __result = true;
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class MouseGates
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var m1 = AccessTools.PropertyGetter(typeof(CharacterInput), "CanPressMouse");
                if (m1 != null) yield return m1;
                var m2 = AccessTools.PropertyGetter(typeof(CharacterInput), "CanControlMouseActions");
                if (m2 != null) yield return m2;
            }

            private static bool Prefix(CharacterInput __instance, ref bool __result)
            {
                try
                {
                    if (!LocalPlayerInput(__instance) || !BlockPlayerMouse) return true;
                    __result = false;
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class ControlUpdates
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var m1 = AccessTools.Method(typeof(CharacterInput), "UpdateCharacterInput");
                if (m1 != null) yield return m1;
                var m2 = AccessTools.Method(typeof(CharacterInput), "UpdateDrivingInput");
                if (m2 != null) yield return m2;
            }

            private static bool Prefix(CharacterInput __instance)
            {
                try
                {
                    if (!LocalPlayerInput(__instance) || !BlockPlayerControls) return true;
                    StopMovement(__instance);
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(CharacterInput), "UpdateCameraZoom")]
        public static class CameraZoom
        {
            private static bool Prefix(CharacterInput __instance)
            {
                try
                {
                    return !LocalPlayerInput(__instance) || !BlockPlayerMouse;
                }
                catch
                {
                    return true;
                }
            }
        }

        // Defense in depth for a weapon action queued before the UI opened. This is
        // deliberately scoped by native player pointer; NPC attacks are unaffected.
        [HarmonyPatch]
        public static class PlayerFire
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                // The player uses Human overrides. Do not detour the unused base-class
                // no-op bodies: IL2CPP can merge those stubs with unrelated methods.
                var m1 = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire");
                if (m1 != null) yield return m1;
                var m2 = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire_Gun");
                if (m2 != null) yield return m2;
                var m3 = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "MeleeAttack");
                if (m3 != null) yield return m3;
                var m4 = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "OnMeleeWeaponFireHold");
                if (m4 != null) yield return m4;
            }

            private static bool Prefix(BasicCharacterController __instance)
            {
                try
                {
                    var player = GameController.instance?.playerCharacter;
                    if (!Il2CppSafe.IsAliveUnity(player) || !Il2CppSafe.IsAliveUnity(__instance)
                        || __instance.Pointer != player.Pointer) return true;
                    return !BlockPlayerMouse;
                }
                catch { return true; } // Never suppress an unidentified NPC's action.
            }
        }
    }
}
