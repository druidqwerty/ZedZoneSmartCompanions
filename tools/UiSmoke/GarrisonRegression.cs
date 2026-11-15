using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 2.4.0: deterministic checks for Camp Garrison persistence, co-op NPC packet sync,
    // owner rejection of garrison/dismiss changes, and telemetry privacy.
    internal static class GarrisonRegression
    {
        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("GarrisonRegression failed: " + msg);
        }

        public static void Run()
        {
            const string testGuid = "sc_v240_garrison_test";
            try
            {
                // 1. SaveText / LoadFromText roundtrip preserves CampGarrison + HomeAnchorPos + HomeAnchorZ
                var s = CompanionConfigStore.GetOrCreateCompanion(testGuid, "GarrisonTester");
                s.OwnerCharacterId = "owner_alpha";
                s.OwnerPlayerName = "Alpha";
                s.StateRevision = 3;
                s.RosterMode = CompanionRosterMode.CampGarrison;
                s.HomeAnchorPos = new Vector2(124.5f, -88.25f);
                s.HomeAnchorZ = -1;
                s.HasHomeAnchor = true;

                string savedText = CompanionConfigStore.BuildSaveText();
                CompanionConfigStore.CompanionStates.Remove(testGuid);
                CompanionConfigStore.LoadFromText(savedText);

                Assert(CompanionConfigStore.CompanionStates.TryGetValue(testGuid, out var loaded), "garrison state missing after LoadFromText");
                Assert(loaded.RosterMode == CompanionRosterMode.CampGarrison, "RosterMode roundtrip failed");
                Assert(Mathf.Abs(loaded.HomeAnchorPos.x - 124.5f) < 0.05f &&
                       Mathf.Abs(loaded.HomeAnchorPos.y - (-88.25f)) < 0.05f &&
                       loaded.HomeAnchorZ == -1 && loaded.HasHomeAnchor, "HomeAnchor roundtrip failed");

                // 2. Host rejects non-owner attempt to flip RosterMode from CampGarrison to ActiveSquad
                loaded.StateRevision = 3;
                long prevRejected = MultiplayerSyncSystem.RejectedCompanionChanges;
                var spoofMsg = new ZedZone.Net.MsgChat
                {
                    senderId = 2,
                    senderName = "Intruder",
                    text = MultiplayerSyncSystem.PacketPrefix +
                           $"NPC|{testGuid}|0|owner_alpha|Alpha|4|intruder_id|0|0|0|0|0|1|0|0|0|24|2|1|0|0|0|0"
                };
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(spoofMsg, null, receivedOnHost: true);
                Assert(loaded.RosterMode == CompanionRosterMode.CampGarrison &&
                       MultiplayerSyncSystem.RejectedCompanionChanges > prevRejected,
                       "host failed to reject non-owner garrison change");

                // 3. Legitimate owner packet updates RosterMode & HomeAnchorPos on client & increments PacketsReceived
                long prevRecv = MultiplayerSyncSystem.PacketsReceived;
                var validMsg = new ZedZone.Net.MsgChat
                {
                    senderId = 1,
                    senderName = "Alpha",
                    text = MultiplayerSyncSystem.PacketPrefix +
                           $"NPC|{testGuid}|1|owner_alpha|Alpha|5|owner_alpha|1|0|0|0|0|1|0|1|1|45|2|2|1|55.5|-22.0|0"
                };
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(validMsg, null, receivedOnHost: false);
                Assert(MultiplayerSyncSystem.PacketsReceived > prevRecv &&
                       loaded.RosterMode == CompanionRosterMode.CampGarrison &&
                       Mathf.Abs(loaded.HomeAnchorPos.x - 55.5f) < 0.1f &&
                       loaded.HomeAnchorZ == 0, "valid garrison sync packet failed");

                // 4. Privacy check: HomeAnchorPos coordinates never leak into diagnostic report
                string rep = PerformanceAndDiagnostics.BuildPrivacySafeReport();
                Assert(rep.Contains("Roster=CampGarrison") && !rep.Contains("55.5") && !rep.Contains("124.5"),
                    "diagnostic report leaked garrison HomeAnchorPos coordinates");

                // 5. v3.0.2: ShouldStayAnchoredBehind covers CampGarrison, HoldPosition, CoverDirection & IsTransientVehicleOverflow (unless physical task active)
                var mockNpc = new GOAPNPCData { npcGUID = testGuid };
                loaded.RosterMode = CompanionRosterMode.CampGarrison;
                loaded.ActiveOrder = TacticalOrderKind.Follow;
                loaded.IsTransientVehicleOverflow = false;
                Assert(CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind false for CampGarrison");

                loaded.RosterMode = CompanionRosterMode.ActiveSquad;
                Assert(!CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind true for ActiveSquad + Follow");

                loaded.ActiveOrder = TacticalOrderKind.HoldPosition;
                Assert(CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind false for HoldPosition");

                loaded.ActiveOrder = TacticalOrderKind.CoverDirection;
                Assert(CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind false for CoverDirection");

                loaded.ActiveOrder = TacticalOrderKind.Follow;
                loaded.IsTransientVehicleOverflow = true;
                Assert(CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind false for IsTransientVehicleOverflow");

                loaded.IsSortingActive = true;
                Assert(!CampGarrisonSystem.ShouldStayAnchoredBehind(mockNpc), "ShouldStayAnchoredBehind failed to yield to active physical task");
                loaded.IsSortingActive = false;

                // 6. v3.0.2: ClearAllTransientVehicleOverflow clears transient flag without mutating RosterMode
                ExpeditionSystems.ClearAllTransientVehicleOverflow();
                Assert(!loaded.IsTransientVehicleOverflow && loaded.RosterMode == CompanionRosterMode.ActiveSquad,
                    "ClearAllTransientVehicleOverflow failed or mutated RosterMode");

                // 7. v3.0.2: Client NPC_DOWNED packet updates IsDownedInMp cleanly
                var downedMsg = new ZedZone.Net.MsgChat
                {
                    senderId = 1,
                    senderName = "Host",
                    text = MultiplayerSyncSystem.PacketPrefix + $"NPC_DOWNED|{testGuid}|1"
                };
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(downedMsg, null, receivedOnHost: false);
                Assert(loaded.IsDownedInMp, "NPC_DOWNED|1 did not set IsDownedInMp on client");
                downedMsg.text = MultiplayerSyncSystem.PacketPrefix + $"NPC_DOWNED|{testGuid}|0";
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(downedMsg, null, receivedOnHost: false);
                Assert(!loaded.IsDownedInMp, "NPC_DOWNED|0 did not clear IsDownedInMp on client");

                // 8. v3.0.2: ResetAllSettingsToDefault(preserveContainerRules: true) resets settings & doctrines while preserving marked chests and XP
                const string testBoxId = "sc_v302_reset_box";
                CompanionConfigStore.ContainerRules[testBoxId] = new ContainerRuleProfile
                {
                    ContainerId = testBoxId,
                    IsMarkedByPlayer = true,
                    Categories = StorageCategory.Medicine
                };
                loaded.ExperiencePoints = 180;
                loaded.Doctrine = TacticalDoctrine.SniperKite;
                loaded.RosterMode = CompanionRosterMode.CampGarrison;
                loaded.ActiveOrder = TacticalOrderKind.HoldPosition;
                CompanionConfigStore.ActiveFormation = SquadFormation.Line;
                CompanionConfigStore.FollowDistance = FollowDistanceMode.Scout;
                CompanionConfigStore.EnableSmartCombat = false;

                CompanionConfigStore.ResetAllSettingsToDefault(preserveContainerRules: true);
                bool boxPreserved = CompanionConfigStore.ContainerRules.ContainsKey(testBoxId);
                CompanionConfigStore.ContainerRules.Remove(testBoxId);

                Assert(boxPreserved, "ResetAllSettingsToDefault(true) erased marked container rules");
                Assert(loaded.ExperiencePoints == 180, "ResetAllSettingsToDefault altered companion XP");
                Assert(loaded.Doctrine == TacticalDoctrine.Balanced &&
                       loaded.RosterMode == CompanionRosterMode.ActiveSquad &&
                       loaded.ActiveOrder == TacticalOrderKind.Follow &&
                       CompanionConfigStore.ActiveFormation == SquadFormation.Free &&
                       CompanionConfigStore.FollowDistance == FollowDistanceMode.Normal &&
                       CompanionConfigStore.EnableSmartCombat,
                       "ResetAllSettingsToDefault did not restore expected defaults");
            }
            finally
            {
                CompanionConfigStore.CompanionStates.Remove(testGuid);
            }

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v2.4.0–v3.0.2 camp garrison, anchor stay-behind, transient overflow, NPC_DOWNED sync & settings reset.");
        }
    }
}
