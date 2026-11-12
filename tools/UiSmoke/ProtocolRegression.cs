using System;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 1.9.0: deterministic checks of the host-authoritative co-op decision table.
    // This does not replace a real two-player session; it proves the rules the host applies.
    internal static class ProtocolRegression
    {
        private static void Expect(string name, CompanionChangeDecision actual, CompanionChangeDecision expected)
        {
            if (actual != expected)
                throw new Exception($"Protocol case '{name}' returned {actual}, expected {expected}.");
        }

        public static void Run()
        {
            const string A = "char-A", B = "char-B";
            Expect("owner changes own companion", MultiplayerSyncSystem.DecideCompanionChange(A, A, A, 3, 4), CompanionChangeDecision.Accept);
            Expect("owner id compare is case-insensitive", MultiplayerSyncSystem.DecideCompanionChange(A, "CHAR-a", A, 3, 4), CompanionChangeDecision.Accept);
            Expect("other player cannot command", MultiplayerSyncSystem.DecideCompanionChange(A, B, A, 3, 4), CompanionChangeDecision.RejectNotOwner);
            Expect("other player cannot steal", MultiplayerSyncSystem.DecideCompanionChange(A, B, B, 3, 9), CompanionChangeDecision.RejectNotOwner);
            Expect("unowned companion claimed by sender", MultiplayerSyncSystem.DecideCompanionChange("", B, B, 0, 1), CompanionChangeDecision.Accept);
            Expect("unowned companion cannot be assigned to someone else", MultiplayerSyncSystem.DecideCompanionChange("", B, A, 0, 1), CompanionChangeDecision.RejectNotOwner);
            Expect("stale revision rejected", MultiplayerSyncSystem.DecideCompanionChange(A, A, A, 5, 5), CompanionChangeDecision.RejectStale);
            Expect("older revision rejected", MultiplayerSyncSystem.DecideCompanionChange(A, A, A, 5, 2), CompanionChangeDecision.RejectStale);
            Expect("legacy packet without revision", MultiplayerSyncSystem.DecideCompanionChange(A, A, A, 5, 0), CompanionChangeDecision.Accept);
            Expect("legacy packet without sender", MultiplayerSyncSystem.DecideCompanionChange(A, "", A, 5, 0), CompanionChangeDecision.Accept);

            // Two rapid optimistic proposals from the owner (rev 6, then 7) must both land.
            int host = 5;
            foreach (int proposal in new[] { 6, 7 })
            {
                Expect("rapid proposal " + proposal, MultiplayerSyncSystem.DecideCompanionChange(A, A, A, host, proposal), CompanionChangeDecision.Accept);
                host = Math.Max(host + 1, proposal);
            }
            // A simultaneous proposal built on the same base as an accepted one loses (first wins).
            Expect("concurrent same-base proposal", MultiplayerSyncSystem.DecideCompanionChange(A, A, A, host, 7), CompanionChangeDecision.RejectStale);

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS co-op authority decision table 13/13 (logic only; live two-player session not exercised).");
        }
    }
}
