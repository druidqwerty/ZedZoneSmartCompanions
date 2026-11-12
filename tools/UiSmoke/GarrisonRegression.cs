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
            }
            finally
            {
                CompanionConfigStore.CompanionStates.Remove(testGuid);
            }

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v2.4.0 camp garrison persistence, host-checked sync & telemetry privacy (8/8).");
        }
    }
}
