using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    /// <summary>Calls real native detours, but a final inert prefix prevents weapon/gameplay bodies.</summary>
    internal static class InputRegression
    {
        private static readonly Harmony Probe = new Harmony("zedzone.smartcompanions.uismoke.input");
        private static readonly MethodInfo Fire = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire");
        private static readonly MethodInfo FireGun = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "Fire_Gun");
        private static readonly MethodInfo Melee = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "MeleeAttack");
        private static readonly MethodInfo MeleeHold = AccessTools.DeclaredMethod(typeof(HumanCharacterController), "OnMeleeWeaponFireHold");
        private static readonly MethodInfo UpdateInput = AccessTools.Method(typeof(CharacterInput), "UpdateCharacterInput");
        private static readonly MethodInfo PlayerFirePrefix = AccessTools.Method(typeof(SmartCompanionsInput.PlayerFire), "Prefix");
        private static GameObject _inputObject;
        private static CharacterInput _input;
        private static bool _observed, _allowed;
        private static int _closingFrame = -1;

        internal static void VerifyOpen()
        {
            var gc = GameController.instance;
            if (!Il2CppSafe.IsAliveUnity(gc) || SmokeFixtures.Agents.Count < 2)
                throw new Exception("Input regression requires main-menu GameController and two inert NPC fixtures.");
            foreach (var method in new[] { Fire, FireGun, Melee, MeleeHold, UpdateInput })
            {
                var observer = new HarmonyMethod(AccessTools.Method(typeof(InputRegression), nameof(Observe))) { priority = Priority.Last + 1 };
                var harmless = new HarmonyMethod(AccessTools.Method(typeof(InputRegression), nameof(PreventSideEffects))) { priority = Priority.Last };
                Probe.Patch(method, prefix: observer);
                Probe.Patch(method, prefix: harmless);
            }
            _inputObject = new GameObject("SmartCompanions_Input_Regression");
            _inputObject.SetActive(false);
            _input = _inputObject.AddComponent<CharacterInput>();
            var player = SmokeFixtures.Agents[0].humanCharacter;
            var companion = SmokeFixtures.Agents[1].humanCharacter;
            var originalPlayer = gc.playerCharacter;
            try
            {
                gc.playerCharacter = player;
                SmartCompanionsUI.ShowMainWindow = true;
                CheckNative("player attack blocked while F8 open", () => player.Fire(null), false);
                CheckNative("companion attack unaffected by F8", () => companion.Fire(null), true);
                CheckNative("player gun attack blocked while F8 open", () => player.Fire_Gun(1f), false);
                CheckNative("companion gun attack unaffected by F8", () => companion.Fire_Gun(1f), true);
                CheckNative("player melee attack blocked while F8 open", () => player.MeleeAttack(null), false);
                CheckNative("companion melee attack unaffected by F8", () => companion.MeleeAttack(null), true);
                CheckNative("player held melee input blocked while F8 open", () => player.OnMeleeWeaponFireHold(), false);
                CheckNative("companion held melee input unaffected by F8", () => companion.OnMeleeWeaponFireHold(), true);
                // Inert fixture has no animation rig; Human.movementInput's setter
                // queries that rig. Test CharacterInput's native field without running it.
                _input.characterController = null;
                _input.movementInput = Vector2.one;
                CheckNative("native player control handler blocked", () => _input.UpdateCharacterInput(), false);
                if (_input.movementInput != Vector2.zero)
                    throw new Exception("Open F8 retained movement from the previous frame.");
                if (!((bool)PlayerFirePrefix.Invoke(null, new object[] { null })))
                    throw new Exception("Null character wrapper was not safely ignored.");
                GameController.instance = null;
                try { CheckNative("scene transition does not suppress unidentified NPC", () => companion.Fire(null), true); }
                finally { GameController.instance = gc; }
                UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS native attack isolation, modal movement reset, null-pointer and scene-transition guards.");
            }
            finally { gc.playerCharacter = originalPlayer; }
        }

        // Call repeatedly from Update after setting ShowMainWindow=false. No OS input is injected.
        // False means the legitimate close-click release guard still owns this frame.
        internal static bool VerifyClosed()
        {
            if (SmartCompanionsUI.ShowMainWindow)
                throw new Exception("VerifyClosed must run after the visual cases have closed F8.");
            if (_closingFrame < 0)
            {
                _closingFrame = Time.frameCount;
                if (!SmartCompanionsInput.BlockPlayerControls)
                    throw new Exception("Closing F8 did not quarantine its final input frame.");
                var gc = GameController.instance;
                var originalPlayer = gc.playerCharacter;
                try
                {
                    gc.playerCharacter = SmokeFixtures.Agents[0].humanCharacter;
                    CheckNative("closing-frame attack remains blocked", () => gc.playerCharacter.Fire(null), false);
                }
                finally { gc.playerCharacter = originalPlayer; }
                return false;
            }
            if (Time.frameCount <= _closingFrame || SmartCompanionsInput.BlockPlayerControls) return false;
            // Cursor could be over HUD in a real world. This harness is on the main menu,
            // with no player assigned, so no mod overlay can legitimately retain capture.
            if (SmartCompanionsInput.BlockPlayerMouse) return false;
            var controller = GameController.instance;
            var savedPlayer = controller.playerCharacter;
            try
            {
                controller.playerCharacter = SmokeFixtures.Agents[0].humanCharacter;
                // Suppress HUD hit-testing here only by turning off its actual setting.
                bool originalBadge = CompanionConfigStore.ShowHudBadge;
                CompanionConfigStore.ShowHudBadge = false;
                try
                {
                    CheckNative("player attack restored after release", () => controller.playerCharacter.Fire(null), true);
                    CheckNative("player gun attack restored after release", () => controller.playerCharacter.Fire_Gun(1f), true);
                    CheckNative("player melee attack restored after release", () => controller.playerCharacter.MeleeAttack(null), true);
                    CheckNative("player held melee input restored after release", () => controller.playerCharacter.OnMeleeWeaponFireHold(), true);
                    CheckNative("companion attack remains allowed", () => SmokeFixtures.Agents[1].humanCharacter.Fire(null), true);
                    CheckNative("native player control handler restored", () => _input.UpdateCharacterInput(), true);
                }
                finally { CompanionConfigStore.ShowHudBadge = originalBadge; }
                UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS close-frame attack quarantine and native player controls/attack restored after release; NPC attacks unaffected.");
            }
            finally
            {
                controller.playerCharacter = savedPlayer;
                Probe.UnpatchSelf();
                if (_inputObject != null) UnityEngine.Object.Destroy(_inputObject);
            }
            return true;
        }

        private static void CheckNative(string name, Action call, bool expectedAllowed)
        {
            _observed = false;
            _allowed = false;
            call();
            if (!_observed || _allowed != expectedAllowed)
                throw new Exception($"Native input regression failed: {name}; observed={_observed}, allowed={_allowed}, expected={expectedAllowed}.");
            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS " + name + ".");
        }

        // A simple void prefix always executes, including after production blocks original.
        private static void Observe(bool __runOriginal)
        {
            _observed = true;
            _allowed = __runOriginal;
        }

        private static bool PreventSideEffects() => false;
    }
}
