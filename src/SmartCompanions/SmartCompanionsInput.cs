using System;
using System.Collections.Generic;
using System.Reflection;
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

        public static bool CanProcessWorldHotkeys()
        {
            try
            {
                var player = GameController.instance?.playerCharacter;
                if (!Il2CppSafe.IsAliveUnity(player)) return false;
                if (ZedZone.Net.NetChatBar.isTypingChat) return false;
                if (GUIUtility.keyboardControl != 0) return false;
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
                    return CaptureUntilReleased(SmartCompanionsUI.ShowMainWindow,
                        ref _modalReleasePending, ref _modalReleasedFrame);
                }
                catch { return SmartCompanionsUI.ShowMainWindow; }
            }
        }

        public static bool BlockPlayerMouse
        {
            get
            {
                if (BlockPlayerControls) return true;
                try
                {
                    var cursor = Input.mousePosition;
                    bool overPanel = SmartCompanionsUI.IsPointerOverOverlay(
                        new Vector2(cursor.x, Screen.height - cursor.y));
                    return CaptureUntilReleased(overPanel,
                        ref _mouseReleasePending, ref _mouseReleasedFrame);
                }
                catch { return false; }
            }
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
                yield return AccessTools.PropertyGetter(typeof(CharacterInput), "stopAllInput");
                yield return AccessTools.PropertyGetter(typeof(CharacterInput), "stopAllInputExceptAreaSwitch");
            }

            private static bool Prefix(CharacterInput __instance, ref bool __result)
            {
                if (!LocalPlayerInput(__instance) || !BlockPlayerControls) return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch]
        public static class MouseGates
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.PropertyGetter(typeof(CharacterInput), "CanPressMouse");
                yield return AccessTools.PropertyGetter(typeof(CharacterInput), "CanControlMouseActions");
            }

            private static bool Prefix(CharacterInput __instance, ref bool __result)
            {
                if (!LocalPlayerInput(__instance) || !BlockPlayerMouse) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch]
        public static class ControlUpdates
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(CharacterInput), "UpdateCharacterInput");
                yield return AccessTools.Method(typeof(CharacterInput), "UpdateDrivingInput");
            }

            private static bool Prefix(CharacterInput __instance)
            {
                if (!LocalPlayerInput(__instance) || !BlockPlayerControls) return true;
                StopMovement(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(CharacterInput), "UpdateCameraZoom")]
        public static class CameraZoom
        {
            private static bool Prefix(CharacterInput __instance)
            {
                return !LocalPlayerInput(__instance) || !BlockPlayerMouse;
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
                yield return AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire");
                yield return AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire_Gun");
                yield return AccessTools.DeclaredMethod(typeof(HumanCharacterController), "MeleeAttack");
                yield return AccessTools.DeclaredMethod(typeof(HumanCharacterController), "OnMeleeWeaponFireHold");
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
