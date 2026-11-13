using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 2.3.0: deterministic verification for 15-companion squad formations/profiles,
    // AVO Expanded compatibility invariants, item-category memoization, and zero-PII diagnostic reports.
    internal static class ScaleAndPrivacyRegression
    {
        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("ScaleAndPrivacyRegression failed: " + msg);
        }

        public static void Run()
        {
            // 1. 15-companion squad scale across all 5 formations
            Vector2 leader = new Vector2(100f, 200f);
            Vector2 fwd = new Vector2(0.8f, 0.6f).normalized;
            foreach (SquadFormation form in Enum.GetValues(typeof(SquadFormation)))
            {
                var pts = new List<Vector2>(15);
                for (int i = 0; i < 15; i++)
                {
                    Vector2 slot = TacticalSquadSystem.ComputeFormationSlot(i, 15, leader, fwd, form, 4.0f);
                    Assert(!float.IsNaN(slot.x) && !float.IsNaN(slot.y), $"15-NPC NaN slot in {form}");
                    for (int j = 0; j < pts.Count; j++)
                        Assert(Vector2.Distance(slot, pts[j]) > 0.45f, $"15-NPC collision in {form} ({i} vs {j})");
                    pts.Add(slot);
                }
            }

            // 15-companion squad profile diff preview speed & non-mutation
            var states15 = new List<CompanionRuntimeState>(15);
            for (int i = 0; i < 15; i++)
            {
                states15.Add(new CompanionRuntimeState
                {
                    Doctrine = (TacticalDoctrine)(i % 6),
                    WorkRole = (CompanionWorkRole)(i % 5),
                    ReserveAmmoMin = 24
                });
            }
            var sw = Stopwatch.StartNew();
            var preview = CompanionProfileSystem.BuildSquadPreview(CompanionSpecialization.Quartermaster, states15, ModLanguage.Russian);
            sw.Stop();
            Assert(preview.TargetCount == 15 && sw.Elapsed.TotalMilliseconds < 25.0, "15-NPC preview scale");
            Assert(states15[1].Doctrine == TacticalDoctrine.Assault, "15-NPC preview must not mutate");

            // 2. AVO Expanded compatibility invariants: Auto cap (0) never overwrites an external AVO cap!
            int savedMax = CompanionConfigStore.MaxTeamSize;
            int savedNativeCap = CharacterData.maxTeamNPCNumber;
            bool savedAlt = CompanionConfigStore.RequireAltForRationHotkey;
            try
            {
                CharacterData.maxTeamNPCNumber = 9; // Simulate AVO Expanded setting squad cap to 9
                CompanionConfigStore.MaxTeamSize = 0; // Auto (Vanilla/AVO)
                CompanionConfigStore.ApplyMaxTeamSizeIfConfigured();
                Assert(CharacterData.maxTeamNPCNumber == 9, "Auto MaxTeamSize=0 must preserve AVO's maxTeamNPCNumber");

                CompanionConfigStore.MaxTeamSize = 15;
                CompanionConfigStore.ApplyMaxTeamSizeIfConfigured();
                Assert(CharacterData.maxTeamNPCNumber == 15, "Explicit MaxTeamSize=15 must apply");

                CompanionConfigStore.RequireAltForRationHotkey = true;
                Assert(CompanionConfigStore.ShouldUseAltForRationHotkey(), "Alt+F6 hotkey guard for AVO");
            }
            finally
            {
                CompanionConfigStore.MaxTeamSize = savedMax;
                CharacterData.maxTeamNPCNumber = savedNativeCap;
                CompanionConfigStore.RequireAltForRationHotkey = savedAlt;
            }

            // 3. Diagnostic report privacy audit: zero names, GUIDs, paths, or inventory dumps!
            const string secretGuid = "SECRET_GUID_DO_NOT_LEAK_991";
            const string secretNpcName = "SECRET_NPC_IVAN_PRIVATE";
            const string secretOwnerName = "SECRET_OWNER_STEAM_76561198000000000";
            const string secretBoxId = "SECRET_BOX_ID_882";
            const string secretBoxName = "SECRET_STASH_COORDS_999";

            CompanionConfigStore.CompanionStates[secretGuid] = new CompanionRuntimeState
            {
                NpcGuid = secretGuid,
                LastKnownName = secretNpcName,
                OwnerCharacterId = "SECRET_CHAR_ID_777",
                OwnerPlayerName = secretOwnerName,
                Specialization = CompanionSpecialization.Medic,
                WorkRole = CompanionWorkRole.Camp
            };
            CompanionConfigStore.ContainerRules[secretBoxId] = new ContainerRuleProfile
            {
                ContainerId = secretBoxId,
                CustomName = secretBoxName,
                IsMarkedByPlayer = true,
                Categories = StorageCategory.Medicine,
                LastKnownPos = new Vector2(-2275.4f, 844.7f)
            };

            string report;
            try
            {
                report = PerformanceAndDiagnostics.BuildPrivacySafeReport();
            }
            finally
            {
                CompanionConfigStore.CompanionStates.Remove(secretGuid);
                CompanionConfigStore.ContainerRules.Remove(secretBoxId);
            }

            Assert(report.Contains("ModVersion: 2.7.0"), "report missing ModVersion 2.7.0");
            Assert(!report.Contains(secretGuid) && !report.Contains(secretNpcName) &&
                   !report.Contains(secretOwnerName) && !report.Contains(secretBoxId) &&
                   !report.Contains(secretBoxName) && !report.Contains("-2275"),
                   "PRIVACY VIOLATION: diagnostic report leaked secret identifier/name/coordinate!");
            Assert(report.IndexOf(@":\Users\", StringComparison.OrdinalIgnoreCase) < 0 &&
                   report.IndexOf("/Users/", StringComparison.OrdinalIgnoreCase) < 0,
                   "PRIVACY VIOLATION: diagnostic report leaked OS user path!");

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v2.3.0 15-NPC scale, AVO compatibility & zero-PII diagnostic report audit (14/14).");
        }
    }
}
