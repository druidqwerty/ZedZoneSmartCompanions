using System;

namespace ZedZone.SmartCompanions
{
    // 1.9.0: host-authoritative companion orders. Only the owning player may change a
    // companion; stale or conflicting changes are rejected and the host re-broadcasts
    // its authoritative state so the sender's UI reverts instead of drifting.
    public enum CompanionChangeDecision
    {
        Accept = 0,
        RejectNotOwner = 1,
        RejectStale = 2
    }

    public static partial class MultiplayerSyncSystem
    {
        public static int RejectedCompanionChanges { get; private set; }

        /// <summary>Pure decision used by the host for every client NPC packet (unit-tested in UiSmoke).</summary>
        public static CompanionChangeDecision DecideCompanionChange(
            string currentOwnerId, string senderCharId, string requestedOwnerId,
            int currentRevision, int packetRevision)
        {
            bool owned = !string.IsNullOrEmpty(currentOwnerId);
            bool senderKnown = !string.IsNullOrEmpty(senderCharId);

            if (owned && senderKnown &&
                !string.Equals(currentOwnerId, senderCharId, StringComparison.OrdinalIgnoreCase))
                return CompanionChangeDecision.RejectNotOwner;

            // Nobody may hand an owned companion to someone else except its owner (checked above);
            // an unowned companion may only be claimed by the sender themselves.
            if (!owned && senderKnown && !string.IsNullOrEmpty(requestedOwnerId) &&
                !string.Equals(requestedOwnerId, senderCharId, StringComparison.OrdinalIgnoreCase))
                return CompanionChangeDecision.RejectNotOwner;

            if (packetRevision > 0 && packetRevision <= currentRevision)
                return CompanionChangeDecision.RejectStale;

            return CompanionChangeDecision.Accept;
        }

        internal static void NoteRejectedChange(CompanionRuntimeState st, CompanionChangeDecision decision)
        {
            RejectedCompanionChanges++;
            try
            {
                SmartCompanionsPlugin.Logs?.LogInfo(
                    $"[MP] Rejected companion change {st?.NpcGuid} ({decision}); re-broadcasting host state.");
            }
            catch { }
            if (st != null) SyncCompanionStateChange(st, bumpRevision: false);
        }

        /// <summary>True when the local player may issue orders to this companion.</summary>
        public static bool CanLocalPlayerCommand(GOAPNPCData npcData) => IsCompanionOwnedByLocalPlayer(npcData);
    }
}
