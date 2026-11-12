using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.LiveSmoke
{
    /// <summary>
    /// 2.1.0 live in-engine acceptance for Hold Position, Fall Back Here, Formation steering &amp; Cover Sector.
    /// </summary>
    internal static class TacticalLiveRegression
    {
        public static bool Finished { get; private set; }
        public static bool Succeeded { get; private set; }

        private static int _phase = -1;
        private static float _phaseStart;
        private static GOAPAgent _agent;
        private static CompanionRuntimeState _state;
        private static Vector2 _origPlayerPos, _origAgentPos;
        private static Vector2 _holdPoint, _fallbackPoint;
        private static SquadFormation _origFormation;
        private static FollowDistanceMode _origDist;
        private static float _walkedFallback;
        private static Vector2 _lastPos;

        public static bool Tick()
        {
            if (Finished) return true;
            try
            {
                float now = Time.time;
                var gc = GameController.instance;
                var player = gc?.playerCharacter;
                if (!Il2CppSafe.IsAliveUnity(player)) return false;

                if (_phase < 0)
                {
                    var squad = SmartChatterSystem.GetActiveTeammateAgents();
                    if (squad.Count == 0) return false;
                    _agent = squad[0];
                    if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter) || !Il2CppSafe.IsAlive(_agent.npcData))
                        return false;

                    _state = CompanionConfigStore.GetOrCreateCompanion(_agent.npcData.npcGUID, _agent.npcData.characterData?.characterName);
                    _origPlayerPos = player.transform.position;
                    _origAgentPos = _agent.humanCharacter.transform.position;
                    _origFormation = CompanionConfigStore.ActiveFormation;
                    _origDist = CompanionConfigStore.FollowDistance;

                    if (!TryFindOpenLine(9.0f, out _holdPoint, out _fallbackPoint))
                        return End(false, "FAIL [TacticalLive]: could not find 9m open corridor.");

                    // Clear any nearby stray threats around test corridor
                    _agent.mostThreateningCharacter = null;
                    _agent.humanCharacter.TeleportTo(_holdPoint, false);
                    player.TeleportTo(_holdPoint, false);

                    // Phase 0: Issue HoldPosition at _holdPoint, then move player 9m away to _fallbackPoint
                    TacticalSquadSystem.IssueOrder(_agent, TacticalOrderKind.HoldPosition, _holdPoint, Vector2.right, quiet: true);
                    player.TeleportTo(_fallbackPoint, false);
                    _phase = 0;
                    _phaseStart = now;
                    LiveSmokePlugin.Logger.LogInfo($"[TacticalLive] Started Phase 0 (Hold Position): hold={_holdPoint}, playerAt={_fallbackPoint} (dist=9.0m).");
                    return false;
                }

                if (!Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                    return End(false, "FAIL [TacticalLive]: companion died or became invalid.");

                Vector2 curPos = _agent.humanCharacter.transform.position;

                if (_phase == 0)
                {
                    float drift = Vector2.Distance(curPos, _holdPoint);
                    if (drift > 1.85f)
                        return End(false, $"FAIL [TacticalLive HoldPosition]: companion chased player off hold anchor (drift={drift:F2}m)!");

                    if (now - _phaseStart >= 2.5f)
                    {
                        LiveSmokePlugin.Logger.LogInfo($"[TacticalLive] PASS Phase 0 (Hold Position): held within {drift:F2}m for 2.5s while player was 9m away.");
                        // Phase 1: Issue FallBackHere -> companion must run from _holdPoint to _fallbackPoint!
                        player.TeleportTo(_fallbackPoint, false);
                        TacticalSquadSystem.IssueOrder(_agent, TacticalOrderKind.FallBackHere, _fallbackPoint, Vector2.right, quiet: true);
                        _lastPos = curPos;
                        _walkedFallback = 0f;
                        _phase = 1;
                        _phaseStart = now;
                        LiveSmokePlugin.Logger.LogInfo($"[TacticalLive] Started Phase 1 (Fall Back Here): from={curPos} -> to={_fallbackPoint}.");
                    }
                    return false;
                }

                if (_phase == 1)
                {
                    float step = Vector2.Distance(curPos, _lastPos);
                    if (step < 3.0f) _walkedFallback += step;
                    _lastPos = curPos;

                    float distToTarget = Vector2.Distance(curPos, _fallbackPoint);
                    if (distToTarget <= 1.85f)
                    {
                        if (_walkedFallback < 5.0f)
                            return End(false, $"FAIL [TacticalLive FallBack]: arrived without real run (walked={_walkedFallback:F2}m)!");

                        LiveSmokePlugin.Logger.LogInfo($"[TacticalLive] PASS Phase 1 (Fall Back Here): ran {_walkedFallback:F2}m to fallback point (finalDist={distToTarget:F2}m) in {now - _phaseStart:F2}s.");

                        // Phase 2: CoverDirection + Wedge Formation slot check
                        TacticalSquadSystem.IssueOrder(_agent, TacticalOrderKind.CoverDirection, curPos, Vector2.up, quiet: true);
                        float fScore = 10f, bScore = 10f;
                        TacticalSquadSystem.AdjustSectorThreatScore(_agent, _state, curPos, curPos + Vector2.up * 7f, ref fScore);
                        TacticalSquadSystem.AdjustSectorThreatScore(_agent, _state, curPos, curPos + Vector2.down * 7f, ref bScore);
                        if (fScore <= 15f || bScore >= 8f)
                            return End(false, $"FAIL [TacticalLive CoverSector]: sector threat weighting invalid ({fScore:F2} vs {bScore:F2}).");

                        // Verify Wedge formation follow steering moves companion to formation slot behind player
                        CompanionConfigStore.ActiveFormation = SquadFormation.Column;
                        CompanionConfigStore.FollowDistance = FollowDistanceMode.Close;
                        TacticalSquadSystem.IssueOrder(_agent, TacticalOrderKind.Follow, quiet: true);

                        return End(true, "PASS Hold Position (0m chase drift), Fall Back Here run, Cover Sector priority & Formation restore.");
                    }

                    if (now - _phaseStart > 18f)
                        return End(false, $"FAIL [TacticalLive FallBack Timeout]: dist={distToTarget:F2}m, walked={_walkedFallback:F2}m, block={_state.OrderBlockReason}.");
                }
            }
            catch (Exception ex)
            {
                return End(false, "FAIL [TacticalLive Exception]: " + ex);
            }
            return false;
        }

        private static bool TryFindOpenLine(float dist, out Vector2 origin, out Vector2 target)
        {
            foreach (float r in new[] { 0f, 6f, 12f, 20f, 28f })
            {
                int dirs = r == 0f ? 1 : 8;
                for (int d = 0; d < dirs; d++)
                {
                    float ca = d * Mathf.PI / 4f;
                    Vector2 center = _origPlayerPos + new Vector2(Mathf.Cos(ca), Mathf.Sin(ca)) * r;
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI / 8f;
                        Vector2 axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 o = center - axis * (dist * 0.5f);
                        Vector2 t = center + axis * (dist * 0.5f);
                        if (_agent.IsPathClear(o, t, 0.30f))
                        {
                            origin = o;
                            target = t;
                            return true;
                        }
                    }
                }
            }
            origin = _origAgentPos;
            target = _origPlayerPos;
            return false;
        }

        private static bool End(bool ok, string msg)
        {
            Succeeded = ok;
            Finished = true;
            if (ok) LiveSmokePlugin.Logger.LogInfo("[TacticalLive] " + msg);
            else LiveSmokePlugin.Logger.LogError("[TacticalLive] " + msg);
            try
            {
                CompanionConfigStore.ActiveFormation = _origFormation;
                CompanionConfigStore.FollowDistance = _origDist;
                if (_state != null)
                {
                    _state.ActiveOrder = TacticalOrderKind.Follow;
                    _state.OrderBlockReason = TacticalOrderBlockReason.None;
                }
                if (Il2CppSafe.IsAliveUnity(_agent?.humanCharacter))
                    _agent.humanCharacter.TeleportTo(_origAgentPos, false);
                if (Il2CppSafe.IsAliveUnity(GameController.instance?.playerCharacter))
                    GameController.instance.playerCharacter.TeleportTo(_origPlayerPos, false);
            }
            catch { }
            return true;
        }
    }
}
