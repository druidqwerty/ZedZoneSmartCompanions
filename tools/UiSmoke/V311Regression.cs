using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 3.1.1: deterministic verification for native /OPT:ICF SearchForFood/Water vs SearchForMeleeWeapon/RangedWeapon
    // isolation, [EH > 0] try/catch protection across every Harmony patch entrypoint, SafeHarmonyPatcher &
    // SmartCompanionsRuntimeGate invariants, and Assembly-CSharp AssemblyRef 0.0.0.0 normalization.
    internal static class V311Regression
    {
        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("V311Regression failed: " + msg);
        }

        public static void Run()
        {
            // 1. Native /OPT:ICF SearchForFood / SearchForWater vs SearchForMeleeWeapon / SearchForRangedWeapon isolation
            var meleeAct = new GOAPAction_SearchForMeleeWeapon();
            var rangedAct = new GOAPAction_SearchForRangedWeapon();

            bool foodMeleeRes = true;
            bool foodMeleeCont = HarmonyPatches.SearchForFood_Execute_Prefix(meleeAct, null, ref foodMeleeRes);
            Assert(foodMeleeCont && foodMeleeRes,
                $"SearchForFood_Execute_Prefix intercepted GOAPAction_SearchForMeleeWeapon (cont={foodMeleeCont}, res={foodMeleeRes})");

            bool foodRangedRes = true;
            bool foodRangedCont = HarmonyPatches.SearchForFood_Execute_Prefix(rangedAct, null, ref foodRangedRes);
            Assert(foodRangedCont && foodRangedRes,
                $"SearchForFood_Execute_Prefix intercepted GOAPAction_SearchForRangedWeapon (cont={foodRangedCont}, res={foodRangedRes})");

            bool waterMeleeRes = true;
            bool waterMeleeCont = HarmonyPatches.SearchForWater_Execute_Prefix(meleeAct, null, ref waterMeleeRes);
            Assert(waterMeleeCont && waterMeleeRes,
                $"SearchForWater_Execute_Prefix intercepted GOAPAction_SearchForMeleeWeapon (cont={waterMeleeCont}, res={waterMeleeRes})");

            bool waterRangedRes = true;
            bool waterRangedCont = HarmonyPatches.SearchForWater_Execute_Prefix(rangedAct, null, ref waterRangedRes);
            Assert(waterRangedCont && waterRangedRes,
                $"SearchForWater_Execute_Prefix intercepted GOAPAction_SearchForRangedWeapon (cont={waterRangedCont}, res={waterRangedRes})");

            // 2. [EH > 0] Harmony Patch Entrypoint Audit across all [HarmonyPatch] types in SmartCompanions
            var modAsm = typeof(SmartCompanionsPlugin).Assembly;
            const BindingFlags AllDeclared = BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Static | BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly;
            int auditedCount = 0;
            var missingEh = new List<string>();
            foreach (var type in modAsm.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), inherit: false))
                    continue;

                foreach (var m in type.GetMethods(AllDeclared))
                {
                    bool isEntrypoint =
                        m.IsDefined(typeof(HarmonyPrefix), inherit: false) ||
                        m.IsDefined(typeof(HarmonyPostfix), inherit: false) ||
                        m.IsDefined(typeof(HarmonyFinalizer), inherit: false) ||
                        m.Name == "Prefix" ||
                        m.Name == "Postfix" ||
                        m.Name == "Finalizer";

                    if (!isEntrypoint)
                        continue;

                    auditedCount++;
                    var body = m.GetMethodBody();
                    if (body == null || body.ExceptionHandlingClauses == null || body.ExceptionHandlingClauses.Count == 0)
                        missingEh.Add($"{type.FullName}.{m.Name}");
                }
            }
            Assert(auditedCount >= 80, $"Unexpectedly low Harmony entrypoint count discovered: {auditedCount}");
            Assert(missingEh.Count == 0,
                $"Harmony patch entrypoints missing [EH > 0] try/catch ({missingEh.Count}): " + string.Join(", ", missingEh));

            // 3. SafeHarmonyPatcher & SmartCompanionsRuntimeGate invariants
            Assert(SafeHarmonyPatcher.FailedPatchCount == 0,
                $"SafeHarmonyPatcher.FailedPatchCount == {SafeHarmonyPatcher.FailedPatchCount} (expected 0)");
            Assert(SafeHarmonyPatcher.LastPatchedMethodCount >= 86,
                $"SafeHarmonyPatcher.LastPatchedMethodCount == {SafeHarmonyPatcher.LastPatchedMethodCount} (expected >= 86)");
            Assert(!Il2CppSafe.IsAliveUnity(InGameController.instance),
                "Expected InGameController.instance == null in UiSmoke MainMenu");
            Assert(!SmartCompanionsRuntimeGate.IsGameplayWorldReady(),
                "SmartCompanionsRuntimeGate.IsGameplayWorldReady() returned true in MainMenu!");
            Assert(!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture(),
                "SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture() returned true in MainMenu!");

            // 4. AssemblyRef version check (Assembly-CSharp -> 0.0.0.0)
            var acRef = modAsm.GetReferencedAssemblies().FirstOrDefault(a => a.Name == "Assembly-CSharp");
            Assert(acRef != null, "Assembly-CSharp AssemblyRef missing on SmartCompanions.dll");
            Assert(acRef.Version == new Version(0, 0, 0, 0),
                $"Assembly-CSharp AssemblyRef version was {acRef.Version}, expected 0.0.0.0");

            // 5. MainMenu portrait mannequin guard & player CharacterData self-heal (prevents attr 0 -> 201 .mpchar corruption)
            var portraitDummyGo = new UnityEngine.GameObject("V311PortraitPreviewDummy");
            portraitDummyGo.SetActive(false);
            try
            {
                var dummyHuman = portraitDummyGo.AddComponent<HumanCharacterController>();
                var previewCd = new CharacterData { characterName = "PreviewPlayer", characterAttrId = 0, camp = 2 };
                dummyHuman.isPlayer = false;
                dummyHuman.characterData = previewCd;
                HarmonyPatches.HumanCharacterController_Init_Prefix(dummyHuman);
                Assert(previewCd.characterAttrId == 0 && previewCd.camp == 2,
                    $"MainMenu portrait dummy mutated player CharacterData: attr={previewCd.characterAttrId}, camp={previewCd.camp}");

                var corruptedPlayerCd = new CharacterData { characterName = "CorruptedPlayer", characterAttrId = 201, camp = 2 };
                dummyHuman.isPlayer = true;
                dummyHuman.characterData = corruptedPlayerCd;
                HarmonyPatches.HumanCharacterController_Init_Prefix(dummyHuman);
                Assert(corruptedPlayerCd.characterAttrId == 0 && corruptedPlayerCd.camp == 2,
                    $"Player self-heal failed on Human.Init: attr={corruptedPlayerCd.characterAttrId}, camp={corruptedPlayerCd.camp}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(portraitDummyGo);
            }

            UiSmokePlugin.Logger.LogInfo(
                "[UiSmoke] PASS v3.1.1 RuntimeGate, SafeHarmonyPatcher (86 patches, 0 failed), [EH>0] on all patch entrypoints, ICF SearchForFood guard, Portrait Player Guard & AssemblyRef 0.0.0.0.");
        }
    }
}
