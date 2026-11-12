using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class SmartCombatAI
    {
        private static readonly Dictionary<int, string> RecentTargetEngageOwner = new Dictionary<int, string>();
        private static readonly Dictionary<IntPtr, float> ProvokedHumanTargets = new Dictionary<IntPtr, float>();
        private static float _lastEngageDictCleanupTime = 0f;

        public static void RecordProvokedHuman(BasicCharacterController actor)
        {
            if (!Il2CppSafe.IsAliveUnity(actor) || actor.deadFlag || actor.isPlayer) return;
            try
            {
                if (ZedZone.Net.NetSessionConfig.IsNetworkPlayer(actor)) return;
                var human = actor.TryCast<HumanCharacterController>();
                if (!Il2CppSafe.IsAliveUnity(human)) return;
                var ag = human.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(ag) && ag.IsPlayerTeammate()) return;

                ProvokedHumanTargets[human.Pointer] = Time.unscaledTime + 120f;
                if (Il2CppSafe.IsAlive(human.characterData))
                {
                    MultiplayerSyncSystem.NormalizeHumanCharacterData(human.characterData, isLooterHint: true);
                    if (!CharacterData.IsHostileCamp(human.characterData.camp))
                        human.characterData.camp = MultiplayerSyncSystem.RemappedLooterCamp;
                }
                if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAlive(ag.npcData))
                    ag.npcData.isBandit = true;
            }
            catch { }
        }

        public static bool IsHostileHumanTarget(GOAPAgent selfAgent, HumanCharacterController targetHuman)
        {
            if (!Il2CppSafe.IsAliveUnity(targetHuman) || targetHuman.deadFlag || targetHuman.isPlayer)
                return false;
            try
            {
                if (ZedZone.Net.NetSessionConfig.IsNetworkPlayer(targetHuman)) return false;
                var targetAgent = targetHuman.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(targetAgent) && targetAgent.IsPlayerTeammate())
                    return false;
                if (Il2CppSafe.IsAlive(targetHuman.characterData))
                {
                    string leaderId = targetAgent?.npcData?.leaderCharacterGUID;
                    if (!string.IsNullOrEmpty(leaderId) && MultiplayerSyncSystem.IsAnyAlliedPlayerCharacterId(leaderId))
                        return false;
                    if (CharacterData.IsHostileCamp(targetHuman.characterData.camp)) return true;
                }
                if (Il2CppSafe.IsAliveUnity(targetAgent))
                {
                    if (targetAgent.TryCast<GOAPAgent_Looter>() != null) return true;
                    if (Il2CppSafe.IsAlive(targetAgent.npcData) && targetAgent.npcData.isBandit) return true;
                    var theirTarget = targetAgent.mostThreateningCharacter;
                    if (Il2CppSafe.IsAliveUnity(theirTarget))
                    {
                        if (theirTarget.isPlayer || ZedZone.Net.NetSessionConfig.IsNetworkPlayer(theirTarget))
                            return true;
                        var theirTargetAg = theirTarget.GetCachedGOAPAgent();
                        if (Il2CppSafe.IsAliveUnity(theirTargetAg) && theirTargetAg.IsPlayerTeammate())
                            return true;
                    }
                }
                if (ProvokedHumanTargets.TryGetValue(targetHuman.Pointer, out float until) && Time.unscaledTime < until)
                    return true;
            }
            catch { }
            return false;
        }

        public static bool IsActualHostileCharacter(GOAPAgent observer, BasicCharacterController candidate, int friendlyCamp)
        {
            if (!Il2CppSafe.IsAliveUnity(candidate) || candidate.deadFlag || candidate.isPlayer)
                return false;
            try
            {
                if (ZedZone.Net.NetSessionConfig.IsNetworkPlayer(candidate)) return false;
                var candAgent = candidate.GetCachedGOAPAgent();
                if (Il2CppSafe.IsAliveUnity(candAgent))
                {
                    if (candAgent.IsPlayerTeammate()) return false;
                    if (Il2CppSafe.IsAlive(candAgent.npcData) && Il2CppSafe.IsAlive(candAgent.npcData.petData))
                        return false;
                }
                var candHuman = candidate.TryCast<HumanCharacterController>();
                if (Il2CppSafe.IsAliveUnity(candHuman))
                    return IsHostileHumanTarget(observer, candHuman);

                if (Il2CppSafe.IsAlive(candidate.characterData))
                {
                    int cCamp = candidate.characterData.camp;
                    if (cCamp == friendlyCamp) return false;
                    return CharacterData.IsHostileCamp(cCamp) || cCamp == 0;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyDoctrineToNpcData(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) || st == null) return;
            try
            {
                CombatResponse.NormalizeNativeCommands(agent);
                string combat = "FireAtWill";
                string weapon = "NoWeaponPreference";
                switch (st.Doctrine)
                {
                    case TacticalDoctrine.Assault:
                        break;
                    case TacticalDoctrine.Bodyguard:
                        combat = "ProtectPlayer";
                        break;
                    case TacticalDoctrine.SniperKite:
                        weapon = "RangedWeaponOnly";
                        break;
                    case TacticalDoctrine.StealthMelee:
                        combat = "ProtectPlayer";
                        weapon = "MeleeWeaponOnly";
                        break;
                    case TacticalDoctrine.Passive:
                        combat = "StopFire";
                        weapon = null; // Cease-fire does not silently change weapon preference.
                        break;
                    case TacticalDoctrine.Balanced:
                    default:
                        break;
                }
                // The dispatcher accepts Category.Leaf and performs native action/state
                // cancellation. It writes bare Leaf to npcData/agentState itself.
                agent.npcData.ExecuteNPCCommand("Combat." + combat);
                if (weapon != null) agent.npcData.ExecuteNPCCommand("WeaponPreference." + weapon);
                agent.executeSelectPriorityGoalFlag = true;
                if (st.Doctrine == TacticalDoctrine.Passive)
                {
                    agent.SelectPriorityGoalExecute();
                }
            }
            catch { }
        }

        public static void AdjustThreatScore(GOAPAgent agent, BasicCharacterController target, ref float score)
        {
            if (!CompanionConfigStore.EnableSmartCombat) return;
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(target) || target.deadFlag) return;
            if (!Il2CppSafe.IsAlive(agent.npcData) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter)) return;

            try
            {
                if (!agent.IsPlayerTeammate()) return;

                // Hard guard: never score friendly players, co-op allies, teammates, or neutral humans as threats in ANY doctrine
                var targetHuman = target.TryCast<HumanCharacterController>();
                if (Il2CppSafe.IsAliveUnity(targetHuman) && !IsHostileHumanTarget(agent, targetHuman))
                {
                    score = 0f;
                    return;
                }

                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID, agent.npcData.characterData?.characterName);
                if (st.Doctrine == TacticalDoctrine.Passive)
                {
                    score *= 0.05f;
                    return;
                }

                var gc = GameController.instance;
                var leader = Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
                    ? agent.leaderCharacter
                    : (Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null);
                Vector2 selfPos = agent.humanCharacter.transform.position;
                Vector2 targetPos = target.transform.position;
                float distToSelf = Vector2.Distance(selfPos, targetPos);

                // 2. Hostile human NPCs (Bandits / Looters / Raiders / Provoked Survivors)
                if (Il2CppSafe.IsAliveUnity(targetHuman))
                {
                    if (score <= 0.01f)
                    {
                        score = 35f / Mathf.Max(1f, distToSelf * 0.15f);
                    }
                    score *= 1.80f;
                    if (Il2CppSafe.IsAliveUnity(targetHuman.rangedWeapon))
                        score *= 1.35f;
                }

                // 1. Protect the companion's leader (Bodyguard & Balanced priority for enemies close to leader)
                if (Il2CppSafe.IsAliveUnity(leader) && !leader.deadFlag)
                {
                    float distToLeader = Vector2.Distance(leader.transform.position, targetPos);
                    if (distToLeader < 3.8f)
                    {
                        float mult = st.Doctrine == TacticalDoctrine.Bodyguard ? 2.75f : 1.90f;
                        score *= mult;
                    }
                    else if (distToLeader < 6.8f && st.Doctrine == TacticalDoctrine.Bodyguard)
                    {
                        score *= 1.50f;
                    }
                }

                // 3. Bosses / Giant Mutants / Armed Zombies
                if (target.FullHP >= 400f || target.TryCast<GiantZombieController>() != null ||
                    target.TryCast<CorpsewheelController>() != null || target.TryCast<CarrionBossController>() != null)
                {
                    score *= 1.70f;
                }
                else
                {
                    var zc = target.TryCast<ZombieController>();
                    if (Il2CppSafe.IsAliveUnity(zc) && Il2CppSafe.IsAliveUnity(zc.rangedWeapon))
                        score *= 1.45f;
                }

                // 4. Finisher bonus on wounded enemies in range
                if (target.FullHP > 1f && (target.hp / target.FullHP) < 0.35f && distToSelf < 9.5f)
                {
                    score *= 1.35f;
                }

                // 5. "Сытый и злой" (Fed & Fierce) & Battle Cry [F9] buffs + Archetype & Rank bonuses
                if (st.HasFedBuff)
                {
                    score *= 1.15f;
                }
                if (st.HasBattleCryBuff)
                {
                    score *= 1.25f;
                }
                if (st.EffectiveArchetype == CompanionArchetype.Veteran &&
                    (Il2CppSafe.IsAliveUnity(targetHuman) || target.FullHP >= 400f))
                {
                    score *= 1.18f;
                }
                else if (st.EffectiveArchetype == CompanionArchetype.StreetPunk && distToSelf < 5.5f)
                {
                    score *= 1.20f;
                }
                if (st.Rank >= CompanionRank.Fighter)
                {
                    score *= (1.0f + (int)st.Rank * 0.05f);
                }

                TacticalSquadSystem.AdjustSectorThreatScore(agent, st, selfPos, targetPos, ref score);
            }
            catch { }
        }

        public static int CommandSquadBattleCry(GOAPAgent specificAgent = null)
        {
            var targets = new List<GOAPAgent>();
            if (Il2CppSafe.IsAliveUnity(specificAgent))
                targets.Add(specificAgent);
            else
                targets.AddRange(SmartChatterSystem.GetActiveTeammateAgents());

            if (targets.Count == 0) return 0;

            float now = Time.unscaledTime;
            int count = 0;
            GOAPAgent speaker = null;

            foreach (var ag in targets)
            {
                if (!Il2CppSafe.IsAliveUnity(ag) || !Il2CppSafe.IsAlive(ag.npcData) || !Il2CppSafe.IsAliveUnity(ag.humanCharacter))
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(ag.npcData.npcGUID, ag.npcData.characterData?.characterName);
                st.BattleCryBuffUntil = now + 60f;
                st.AddExperience(10, ag);

                try
                {
                    var hc = ag.humanCharacter;
                    if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && hc.rangedWeapon.isMalfunction)
                        hc.ClearWeaponMalfunction(hc.rangedWeapon);

                    ag.CheckAndReloadMagazine();

                    var status = ag.npcData.characterData?.characterStatusData;
                    if (Il2CppSafe.IsAlive(status))
                    {
                        status.energy = status.maxEnergy > 0 ? status.maxEnergy : 100f;
                        status.fatigue = Mathf.Min(status.maxFatigue > 0 ? status.maxFatigue : 100f, status.fatigue + 20f);
                    }
                }
                catch { }

                if (speaker == null) speaker = ag;
                count++;
            }

            if (speaker != null)
            {
                bool hasEnemy = Il2CppSafe.IsAliveUnity(speaker.mostThreateningCharacter);
                SmartChatterSystem.Speak(speaker, hasEnemy ? ChatterTopic.CombatFocusTarget : ChatterTopic.SquadBattleCry, minCooldown: 0.2f, bubbleDuration: 6f, force: true);
            }

            BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                $"📣 Боевой клич отряда ({count} чел.)! Оружие проверено, выносливость 100%, штурмовой фокус на 60 сек!",
                $"📣 Squad Battle Cry ({count})! Weapons unjammed, 100% stamina & assault focus for 60s!",
                $"📣 小队战吼突击（{count} 人）！武器已排障装填、耐力拉满，获得 60 秒集火突击增益！"));

            return count;
        }

        public static void TickAllCompanionsCombat()
        {
            float now = Time.unscaledTime;
            if (now - _lastEngageDictCleanupTime > 60f)
            {
                _lastEngageDictCleanupTime = now;
                if (RecentTargetEngageOwner.Count > 250)
                    RecentTargetEngageOwner.Clear();
            }

            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            var gc = GameController.instance;
            var player = Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null;

            foreach (var agent in teammates)
            {
                if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(agent.humanCharacter) ||
                    agent.humanCharacter.deadFlag || !Il2CppSafe.IsAlive(agent.npcData))
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID, agent.npcData.characterData?.characterName);
                // Repair invalid legacy save commands even when smart combat is disabled.
                CombatResponse.NormalizeNativeCommands(agent);
                if (!CompanionConfigStore.EnableSmartCombat) continue;
                CombatResponse.Refresh(agent, st, now);
                if (st.IsSortingActive) continue;

                TickSingleCompanionCombat(agent, st, player, now);
            }
        }

        private static void TickSingleCompanionCombat(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;

            // Apply "Сытый и злой" (Fed & Fierce), Battle Cry, and Veteran Rank passive boosts
            if (st.HasFedBuff || st.HasBattleCryBuff || st.Rank >= CompanionRank.Fighter)
            {
                try
                {
                    var status = agent.npcData.characterData?.characterStatusData;
                    if (Il2CppSafe.IsAlive(status))
                    {
                        float energyBoost = (st.HasFedBuff ? 1.5f : 0f) + (st.HasBattleCryBuff ? 2.0f : 0f) + ((int)st.Rank * 0.35f);
                        if (energyBoost > 0f && status.energy < status.maxEnergy)
                            status.energy = Mathf.Min(status.maxEnergy, status.energy + energyBoost);

                        float rankHpBonus = CompanionConfigStore.GetRankMaxHpBonus(st.Rank);
                        if (rankHpBonus > 0f && status.maxhp < 100f + rankHpBonus)
                            status.maxhp = 100f + rankHpBonus;
                    }
                }
                catch { }
            }

            // Smart Flashlight control (Night / Rain / Underground vs Stealth Doctrine)
            if (CompanionConfigStore.EnableSmartFlashlight && now - st.LastFlashlightCheckTime > 4.0f)
            {
                st.LastFlashlightCheckTime = now;
                TickSmartFlashlight(agent, st, hc);
            }

            if (!MultiplayerSyncSystem.IsMultiplayerClient)
            {
                var alliedPlayers = MultiplayerSyncSystem.GetAllAlliedPlayers();
                for (int pIdx = 0; pIdx < alliedPlayers.Count; pIdx++)
                {
                    var allyPlayer = alliedPlayers[pIdx];
                    if (!Il2CppSafe.IsAliveUnity(allyPlayer) || allyPlayer.deadFlag) continue;
                    if (CompanionConfigStore.EnablePlayerRescue)
                    {
                        TryEmergencyPlayerRescue(agent, st, allyPlayer, now);
                    }
                    TickFieldSupport(agent, st, allyPlayer, now);
                }
            }

            var leader = Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
                ? agent.leaderCharacter
                : player;

            if (Il2CppSafe.IsAliveUnity(leader) && !leader.deadFlag)
            {
                // Anti-Friendly-Fire Corridor
                if (CompanionConfigStore.EnableAntiFriendlyFire && leader.isRangedWeaponAiming)
                {
                    TryStepOutOfPlayerFireLine(agent, st, leader, selfPos, now);
                }

                // Rear Flank Radar: warn local player if an actual hostile sneaks behind their back
                if (leader.isPlayer)
                {
                    CheckPlayerRearFlank(agent, st, leader, now);
                }
            }

            var target = agent.mostThreateningCharacter;
            if (!Il2CppSafe.IsAliveUnity(target) || target.deadFlag)
            {
                TickOutOfCombatPrep(agent, st, hc, now);
                if (string.IsNullOrEmpty(st.CurrentTaskText) || st.CurrentTaskText.StartsWith("Бой") || st.CurrentTaskText.StartsWith("Combat") || st.CurrentTaskText.StartsWith("交战"))
                {
                    st.CurrentTaskText = CompanionConfigStore.L(
                        st.HasFedBuff ? "Патруль (🔥 Сытый и злой)" : "Патруль / Сопровождение",
                        st.HasFedBuff ? "Escorting (🔥 Fed & Fierce)" : "Escorting Leader",
                        st.HasFedBuff ? "护卫巡逻（🔥 饱腹狂暴）" : "护卫巡逻 / 跟随队长");
                }
                return;
            }

            // Respect both mod doctrines and the native cease-fire command.
            if (st.Doctrine == TacticalDoctrine.Passive || agent.IsTeammateCombatSuppressed()) return;

            int targetId = target.GetInstanceID();
            RecentTargetEngageOwner[targetId] = st.NpcGuid;

            Vector2 targetPos = target.transform.position;
            float distToTarget = Vector2.Distance(selfPos, targetPos);
            var targetName = target.characterData?.characterName ?? CompanionConfigStore.L("Враг", "Hostile", "敌方目标");
            st.CurrentTaskText = CompanionConfigStore.L(
                $"Бой: {targetName} ({distToTarget:F1}м)",
                $"Combat: {targetName} ({distToTarget:F1}m)",
                $"交战：{targetName}（{distToTarget:F1}米）");

            // Check weapon malfunction in combat -> callout & auto-clear!
            if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && hc.rangedWeapon.isMalfunction)
            {
                SmartChatterSystem.Speak(agent, ChatterTopic.CombatWeaponJammed, minCooldown: 8f);
                try { hc.ClearWeaponMalfunction(hc.rangedWeapon); } catch { }
            }

            // Tactical Callouts
            if (now - st.LastCombatCalloutTime > 7.0f)
            {
                st.LastCombatCalloutTime = now;
                // Tactical Callouts
                int nearbyEnemies = CountNearbyHostiles(agent, selfPos, 13f, hc.characterData?.camp ?? 1);

                if (Il2CppSafe.IsAliveUnity(player) && Vector2.Distance(player.transform.position, targetPos) < 2.9f)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSavePlayer, minCooldown: 5.5f);
                }
                else if (hc.FullHP > 1f && (hc.hp / hc.FullHP) < 0.35f)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatWounded, minCooldown: 7.5f);
                }
                else if (target.FullHP >= 400f || target.TryCast<GiantZombieController>() != null || target.TryCast<CorpsewheelController>() != null)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotBoss, minCooldown: 9f);
                }
                else if (target.TryCast<HumanCharacterController>() != null)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotBandit, minCooldown: 7.5f);
                }
                else if (nearbyEnemies >= 5)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotHorde, minCooldown: 8.5f);
                }
                else
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotZombie, minCooldown: 9.5f);
                }
            }

            if (now - st.LastWeaponSwitchTime > 2.0f)
            {
                TrySmartWeaponSwitch(agent, st, distToTarget, now);
            }

            if (TacticalSquadSystem.TryApplyCombatRetreat(agent, st, leader, target, now))
                return;

            if (CompanionConfigStore.EnableKiting && st.Doctrine != TacticalDoctrine.Assault &&
                CombatResponse.AllowsRangedResponse(st, agent.npcData.weaponPreference))
            {
                float kiteThreshold = st.Doctrine == TacticalDoctrine.SniperKite ? 4.8f : 3.2f;
                bool usingRanged = Il2CppSafe.IsAliveUnity(hc.rangedWeapon) || hc.reloadingMagazineFlag;

                if (usingRanged && distToTarget < kiteThreshold && distToTarget > 0.1f && now - st.LastKiteStepTime > 1.0f)
                {
                    st.LastKiteStepTime = now;
                    Vector2 awayDir = (selfPos - targetPos).normalized;
                    if (Il2CppSafe.IsAliveUnity(player))
                    {
                        Vector2 toPlayer = ((Vector2)player.transform.position - selfPos);
                        if (toPlayer.magnitude > 9f)
                            awayDir = (awayDir + toPlayer.normalized * 0.45f).normalized;
                    }

                    Vector2 kiteDest = selfPos + awayDir * 2.5f;
                    try
                    {
                        agent.MoveTo_Direct(kiteDest, true, true, false);
                        agent.LookAtPositionSmoothCompat(targetPos, 14f);
                    }
                    catch { }

                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatKiting, minCooldown: 7.5f);
                }
                else if (target.TryCast<HumanCharacterController>() != null && distToTarget < 11.5f &&
                    !CombatResponse.HasNativeRangedAction(agent) && now - st.LastKiteStepTime > 1.5f)
                {
                    st.LastKiteStepTime = now;
                    Vector2 toEnemy = (targetPos - selfPos).normalized;
                    Vector2 perp = (Time.frameCount % 120 < 60) ? new Vector2(-toEnemy.y, toEnemy.x) : new Vector2(toEnemy.y, -toEnemy.x);
                    Vector2 strafeDest = selfPos + perp * 1.9f;
                    try
                    {
                        agent.MoveTo_Direct(strafeDest, true, true, false);
                        agent.LookAtPositionSmoothCompat(targetPos, 14f);
                    }
                    catch { }
                }
            }
        }

        private static void CheckPlayerRearFlank(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            if (now - st.LastFlankWarnTime < 14f) return;
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || gc.characterList == null) return;

                Vector2 playerPos = player.transform.position;
                Vector2 playerForward = Vector2.zero;
                if (Il2CppSafe.IsAliveUnity(player.rangedWeapon))
                    playerForward = player.rangedWeapon.transform.right;
                else if (player.movementInput.sqrMagnitude > 0.05f)
                    playerForward = player.movementInput.normalized;

                if (playerForward.sqrMagnitude < 0.01f) return;
                playerForward.Normalize();

                int friendlyCamp = player.characterData?.camp ?? 1;
                float maxRadarDist = st.EffectiveArchetype == CompanionArchetype.ParanoidPrepper ? 8.5f : 5.8f;
                var hostiles = PerformanceAndDiagnostics.GetActiveHostilesSnapshot(agent, friendlyCamp, now);
                for (int i = 0; i < hostiles.Count; i++)
                {
                    Vector2 toEnemy = hostiles[i].Pos - playerPos;
                    float dist = toEnemy.magnitude;
                    if (dist < 1.2f || dist > maxRadarDist) continue;

                    float dot = Vector2.Dot(playerForward, toEnemy / dist);
                    if (dot < -0.30f) // Behind the player's back!
                    {
                        st.LastFlankWarnTime = now;
                        SmartChatterSystem.Speak(agent, ChatterTopic.CombatFlankWarning, minCooldown: 6f);
                        break;
                    }
                }
            }
            catch { }
        }

        private static void TickOutOfCombatPrep(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController hc, float now)
        {
            if (now - st.LastOutOfCombatPrepTime < 4.5f) return;
            st.LastOutOfCombatPrepTime = now;

            try
            {
                if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon))
                {
                    if (hc.rangedWeapon.isMalfunction)
                    {
                        hc.ClearWeaponMalfunction(hc.rangedWeapon);
                        return;
                    }
                    if (!hc.reloadingMagazineFlag && Il2CppSafe.IsAlive(hc.rangedWeapon.weaponData))
                    {
                        int loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                        int magSize = hc.rangedWeapon.weaponAttr != null ? hc.rangedWeapon.weaponAttr.magazineSize : 0;
                        if (loaded == 0 || (magSize > 2 && loaded <= magSize / 3))
                        {
                            agent.CheckAndReloadMagazine();
                        }
                    }
                }

                // Self-heal minor injuries out of combat if companion is wounded
                if (hc.FullHP > 10f && hc.hp < hc.FullHP * 0.85f)
                {
                    hc.HealAllBodyPartInjuries();
                }
            }
            catch { }
        }

        private static bool TryStepOutOfPlayerFireLine(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, Vector2 selfPos, float now)
        {
            if (now - st.LastFireLineStepTime < 1.0f) return false;
            try
            {
                Vector2 playerPos = player.transform.position;
                Vector2 toCompanion = selfPos - playerPos;
                float dist = toCompanion.magnitude;
                if (dist < 0.8f || dist > 10.5f) return false;

                Vector2 aimDir = Vector2.zero;
                if (Il2CppSafe.IsAliveUnity(player.rangedWeapon))
                    aimDir = player.rangedWeapon.transform.right;
                if (aimDir.sqrMagnitude < 0.01f) return false;

                aimDir.Normalize();
                float proj = Vector2.Dot(toCompanion, aimDir);
                if (proj <= 0.3f || proj >= dist + 0.5f) return false;

                Vector2 closestOnRay = playerPos + aimDir * proj;
                float perpDist = Vector2.Distance(selfPos, closestOnRay);
                if (perpDist < 1.08f)
                {
                    st.LastFireLineStepTime = now;
                    Vector2 side = new Vector2(-aimDir.y, aimDir.x);
                    if (Vector2.Dot(selfPos - closestOnRay, side) < 0f)
                        side = -side;

                    Vector2 safePos = selfPos + side * 1.95f;
                    agent.MoveTo_Direct(safePos, true, true, false);
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatClearFireLine, minCooldown: 6.0f);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void TrySmartWeaponSwitch(GOAPAgent agent, CompanionRuntimeState st, float distToTarget, float now)
        {
            try
            {
                var hc = agent.humanCharacter;
                if (st.Doctrine == TacticalDoctrine.Passive || agent.IsTeammateCombatSuppressed()) return;
                bool allowRanged = CombatResponse.AllowsRangedResponse(st, agent.npcData.weaponPreference);
                if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && Il2CppSafe.IsAlive(hc.rangedWeapon.weaponData))
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                    bool jammed = hc.rangedWeapon.isMalfunction;
                    bool reloading = hc.reloadingMagazineFlag;
                    bool preferMelee = st.Doctrine == TacticalDoctrine.StealthMelee ||
                        (st.Loadout == PreferredLoadout.MeleeFirst && distToTarget < 6.2f) ||
                        (distToTarget < 1.95f && (loaded == 0 || jammed || reloading));

                    if (preferMelee)
                    {
                        var bestMelee = agent.FindBestMeleeWeapon(out float bestVal);
                        if (Il2CppSafe.IsAlive(bestMelee))
                        {
                            st.LastWeaponSwitchTime = now;
                            agent.DrawWeaponViaSlot(bestMelee);
                            SmartChatterSystem.Speak(agent, ChatterTopic.CombatSwitchMelee, minCooldown: 6.5f);
                        }
                    }
                    else if (loaded == 0 && !reloading)
                    {
                        int reserve = BasicRangedWeapon.GetInventoryAmmoTotal(hc, hc.rangedWeapon.weaponData, 0);
                        if (reserve == 0)
                            SmartChatterSystem.Speak(agent, ChatterTopic.CombatLowAmmo, minCooldown: 12f);
                    }
                }
                else if (allowRanged && distToTarget > 3.8f && !hc.reloadingMagazineFlag)
                {
                    st.LastWeaponSwitchTime = now; // Bound failed inventory/equip attempts too.
                    if (GOAPAction_RangeAttack.EquipBestRangedWeapon(agent))
                    {
                        st.LastWeaponSwitchTime = now;
                        SmartChatterSystem.Speak(agent, ChatterTopic.CombatSwitchRanged, minCooldown: 7.5f);
                    }
                }
            }
            catch { }
        }

        private static void TickSmartFlashlight(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController hc)
        {
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || !Il2CppSafe.IsAlive(gc.gameData)) return;

                if (st.Doctrine == TacticalDoctrine.StealthMelee || st.Doctrine == TacticalDoctrine.Passive)
                {
                    if (hc.isFlashLightOn)
                    {
                        hc.TurnOffLight();
                        SmartChatterSystem.Speak(agent, ChatterTopic.CombatStealthLightsOff, minCooldown: 18f);
                    }
                    return;
                }

                float hours = (gc.gameData.inGameTime / 60f) % 24f;
                bool isDark = (hours < 6.0f || hours >= 19.5f) || gc.gameData.isBloodRaining ||
                              (Il2CppSafe.IsAlive(agent.npcData) && agent.npcData.zLevel < 0);

                if (isDark && !hc.isFlashLightOn)
                {
                    var lights = agent.GetAvailableFlashLights();
                    if (lights != null && lights.Count > 0)
                        hc.TurnOnLight();
                }
                else if (!isDark && hc.isFlashLightOn && hours >= 7.5f && hours <= 18.0f &&
                         (agent.npcData == null || agent.npcData.zLevel >= 0))
                {
                    hc.TurnOffLight();
                }
            }
            catch { }
        }

        private static void TryEmergencyPlayerRescue(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            if (now - st.LastPlayerRescueTime < 35f) return;
            try
            {
                var status = player.characterData?.characterStatusData;
                if (!Il2CppSafe.IsAlive(status) || status.maxhp <= 5f) return;

                bool isDowned = player.weaponHolsteredByDowned || (status.hp / status.maxhp) < 0.22f;
                if (!isDowned) return;

                float dist = Vector2.Distance(agent.humanCharacter.transform.position, player.transform.position);
                if (dist > 11.5f) return;

                if (dist > 2.6f)
                {
                    agent.MoveTo_Direct(player.transform.position, false, true, false);
                }

                st.LastPlayerRescueTime = now;
                st.LastFieldMedicTime = now;

                if (player.weaponHolsteredByDowned)
                {
                    try { player.NetExitDowned(0.50f); } catch { }
                }

                float rescueHeal = Mathf.Max(35f, status.maxhp * 0.40f);
                status.hp = Mathf.Min(status.maxhp, status.hp + rescueHeal);
                status.energy = Mathf.Min(status.maxEnergy > 0 ? status.maxEnergy : 100f, status.energy + 45f);
                player.hp = status.hp;
                try { player.HealAllBodyPartInjuries(); } catch { }
                MultiplayerSyncSystem.SendCrossPlayerHeal(player, rescueHeal, true, st.LastKnownName);

                st.AddExperience(30, agent);
                SmartChatterSystem.Speak(agent, ChatterTopic.CombatPlayerDownedRescue, minCooldown: 0.5f, bubbleDuration: 6.5f, force: true);
                BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                    $"🚨 {st.LastKnownName} провёл экстренную реанимацию и вытащил вас с того света (+{rescueHeal:F0} HP)!",
                    $"🚨 {st.LastKnownName} performed an emergency adrenaline rescue (+{rescueHeal:F0} HP)!",
                    $"🚨 {st.LastKnownName} 实施了战地紧急强心抢救（+{rescueHeal:F0} 生命值）！"));
            }
            catch { }
        }

        private static void TickFieldSupport(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            float dist = Vector2.Distance(agent.humanCharacter.transform.position, player.transform.position);
            if (dist > 6.5f) return;

            bool isMedicArch = st.EffectiveArchetype == CompanionArchetype.CombatMedic;
            float medicCooldown = isMedicArch ? 20f : 30f;
            if (st.Rank >= CompanionRank.Elite) medicCooldown *= 0.75f;

            if (CompanionConfigStore.EnableFieldMedic && st.AllowFieldMedic && now - st.LastFieldMedicTime > medicCooldown)
            {
                var status = player.characterData?.characterStatusData;
                float hpThreshold = isMedicArch ? 0.60f : 0.48f;
                if (Il2CppSafe.IsAlive(status) && status.maxhp > 10f && (status.hp / status.maxhp) < hpThreshold)
                {
                    st.LastFieldMedicTime = now;
                    float healPct = isMedicArch ? 0.38f : 0.28f;
                    float healAmount = Mathf.Min(status.maxhp - status.hp, Mathf.Max(25f, status.maxhp * healPct));
                    status.hp = Mathf.Min(status.maxhp, status.hp + healAmount);
                    player.hp = status.hp;
                    try { player.HealAllBodyPartInjuries(); } catch { }
                    MultiplayerSyncSystem.SendCrossPlayerHeal(player, healAmount, true, st.LastKnownName);

                    try
                    {
                        var heals = agent.GetHealingItems();
                        if (heals != null && heals.Count > st.ReserveMedMin && agent.npcData?.characterData?.inventoryData != null)
                        {
                            ItemData chosenMed = null;
                            for (int hIdx = 0; hIdx < heals.Count; hIdx++)
                            {
                                var cand = heals[hIdx];
                                if (!Il2CppSafe.IsAlive(cand)) continue;
                                if (!CompanionProfileSystem.IsRareMedItem(cand.itemId))
                                {
                                    chosenMed = cand;
                                    break;
                                }
                                if (CompanionConfigStore.AllowSpendRareMeds && chosenMed == null)
                                    chosenMed = cand;
                            }
                            if (Il2CppSafe.IsAlive(chosenMed))
                                CharacterActionManager.ConsumeOneFood(agent.npcData.characterData, agent.npcData.characterData.inventoryData, chosenMed);
                        }
                    }
                    catch { }

                    st.AddExperience(25, agent);
                    SmartChatterSystem.Speak(agent, ChatterTopic.FieldMedicAid, minCooldown: 1.5f, bubbleDuration: 6f, force: true);
                }
            }

            if (CompanionConfigStore.EnableAmmoShare && st.AllowAmmoShare && player.isPlayer && now - st.LastAmmoShareTime > 42f)
            {
                if (Il2CppSafe.IsAliveUnity(player.rangedWeapon) && Il2CppSafe.IsAlive(player.rangedWeapon.weaponData) &&
                    Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter))
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(player.rangedWeapon.weaponData);
                    int reserve = BasicRangedWeapon.GetInventoryAmmoTotal(player, player.rangedWeapon.weaponData, 0);
                    if (loaded == 0 && reserve == 0)
                    {
                        var wAttr = player.rangedWeapon.weaponAttr;
                        if (Il2CppSafe.IsAliveUnity(wAttr) && wAttr.ammoId > 0 && Il2CppSafe.IsAlive(ItemManager.instance))
                        {
                            int ammoId = wAttr.ammoId;
                            var ammoAttr = ItemManager.instance.GetItemAttrById(ammoId);
                            if (Il2CppSafe.IsAliveUnity(ammoAttr) &&
                                (CompanionConfigStore.AllowSpendRareAmmo || !CompanionProfileSystem.IsRareAmmoStackSize(ammoAttr.stackNumber)))
                            {
                                var ammoPack = ItemManager.instance.GetRandomDataByAttr(ammoAttr, false, false);
                                if (Il2CppSafe.IsAlive(ammoPack))
                                {
                                    ammoPack.itemNumberFloat = Mathf.Clamp(ammoAttr.stackNumber / 2, 8, 30);
                                    if (GameController.instance.AddItemToPlayer(ammoPack, true))
                                    {
                                        st.LastAmmoShareTime = now;
                                        st.AddExperience(25, agent);
                                        SmartChatterSystem.Speak(agent, ChatterTopic.FieldAmmoShare, minCooldown: 1.5f, bubbleDuration: 6f, force: true);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void OnCharacterKilled(BasicCharacterController victim)
        {
            if (!Il2CppSafe.IsAliveUnity(victim)) return;
            try
            {
                int id = victim.GetInstanceID();
                if (RecentTargetEngageOwner.TryGetValue(id, out var ownerGuid))
                {
                    RecentTargetEngageOwner.Remove(id);
                    if (CompanionConfigStore.CompanionStates.TryGetValue(ownerGuid, out var st))
                    {
                        st.KillsThisSession++;
                    }
                    int killXp = 12;
                    try
                    {
                        if (victim.FullHP >= 400f || victim.TryCast<GiantZombieController>() != null ||
                            victim.TryCast<CorpsewheelController>() != null || victim.TryCast<CarrionBossController>() != null)
                            killXp = 75;
                        else if (victim.TryCast<HumanCharacterController>() != null)
                            killXp = 28;
                    }
                    catch { }

                    foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        if (Il2CppSafe.IsAlive(ag.npcData) && ag.npcData.npcGUID == ownerGuid)
                        {
                            if (st != null) st.AddExperience(killXp, ag);
                            SmartChatterSystem.Speak(ag, ChatterTopic.CombatKillEnemy, minCooldown: 4.8f);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private static int CountNearbyHostiles(GOAPAgent observer, Vector2 center, float radius, int friendlyCamp)
        {
            int count = 0;
            float rSqr = radius * radius;
            try
            {
                var hostiles = PerformanceAndDiagnostics.GetActiveHostilesSnapshot(observer, friendlyCamp, Time.unscaledTime);
                for (int i = 0; i < hostiles.Count; i++)
                {
                    if ((hostiles[i].Pos - center).sqrMagnitude <= rSqr)
                        count++;
                }
            }
            catch { }
            return count;
        }
    }
}
