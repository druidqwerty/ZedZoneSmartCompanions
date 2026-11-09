using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class SmartCombatAI
    {
        private static readonly Dictionary<int, string> RecentTargetEngageOwner = new Dictionary<int, string>();
        private static float _lastEngageDictCleanupTime = 0f;

        public static void ApplyDoctrineToNpcData(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) || st == null) return;
            try
            {
                switch (st.Doctrine)
                {
                    case TacticalDoctrine.Assault:
                        agent.npcData.combatCommand = "NPCCommand:Combat.FireAtWill";
                        agent.npcData.weaponPreference = "NPCCommand:WeaponPreference.NoWeaponPreference";
                        break;
                    case TacticalDoctrine.Bodyguard:
                        agent.npcData.combatCommand = "NPCCommand:Combat.ProtectPlayer";
                        agent.npcData.weaponPreference = "NPCCommand:WeaponPreference.NoWeaponPreference";
                        break;
                    case TacticalDoctrine.SniperKite:
                        agent.npcData.combatCommand = "NPCCommand:Combat.FireAtWill";
                        agent.npcData.weaponPreference = "NPCCommand:WeaponPreference.RangedWeaponOnly";
                        break;
                    case TacticalDoctrine.StealthMelee:
                        agent.npcData.combatCommand = "NPCCommand:Combat.ProtectPlayer";
                        agent.npcData.weaponPreference = "NPCCommand:WeaponPreference.MeleeWeaponOnly";
                        break;
                    case TacticalDoctrine.Passive:
                        agent.npcData.combatCommand = "NPCCommand:Combat.StopFire";
                        break;
                    case TacticalDoctrine.Balanced:
                    default:
                        if (string.IsNullOrEmpty(agent.npcData.combatCommand) || agent.npcData.combatCommand.Contains("StopFire"))
                            agent.npcData.combatCommand = "NPCCommand:Combat.FireAtWill";
                        break;
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
                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID, agent.npcData.characterData?.characterName);
                if (st.Doctrine == TacticalDoctrine.Passive)
                {
                    score *= 0.05f;
                    return;
                }

                var gc = GameController.instance;
                var player = Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null;
                Vector2 selfPos = agent.humanCharacter.transform.position;
                Vector2 targetPos = target.transform.position;
                float distToSelf = Vector2.Distance(selfPos, targetPos);

                // 1. Protect the player (Bodyguard & Balanced priority for enemies close to player)
                if (Il2CppSafe.IsAliveUnity(player) && !player.deadFlag)
                {
                    float distToPlayer = Vector2.Distance(player.transform.position, targetPos);
                    if (distToPlayer < 3.8f)
                    {
                        float mult = st.Doctrine == TacticalDoctrine.Bodyguard ? 2.75f : 1.90f;
                        score *= mult;
                    }
                    else if (distToPlayer < 6.8f && st.Doctrine == TacticalDoctrine.Bodyguard)
                    {
                        score *= 1.50f;
                    }
                }

                // 2. Hostile human NPCs (Bandits / Looters / Raiders)
                var targetHuman = target.TryCast<HumanCharacterController>();
                if (Il2CppSafe.IsAliveUnity(targetHuman))
                {
                    score *= 1.80f;
                    if (Il2CppSafe.IsAliveUnity(targetHuman.rangedWeapon))
                        score *= 1.35f;
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

                // 5. "Сытый и злой" (Fed & Fierce) buff increases target aggression slightly
                if (st.HasFedBuff)
                {
                    score *= 1.15f;
                }
            }
            catch { }
        }

        public static void TickAllCompanionsCombat()
        {
            if (!CompanionConfigStore.EnableSmartCombat) return;
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
                if (st.IsSortingActive) continue;

                TickSingleCompanionCombat(agent, st, player, now);
            }
        }

        private static void TickSingleCompanionCombat(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;

            // Apply "Сытый и злой" (Fed & Fierce) stamina & accuracy boost while active
            if (st.HasFedBuff)
            {
                try
                {
                    var status = agent.npcData.characterData?.characterStatusData;
                    if (Il2CppSafe.IsAlive(status) && status.energy < status.maxEnergy)
                        status.energy = Mathf.Min(status.maxEnergy, status.energy + 1.8f);
                }
                catch { }
            }

            if (Il2CppSafe.IsAliveUnity(player) && !player.deadFlag)
            {
                TickFieldSupport(agent, st, player, now);

                // Anti-Friendly-Fire Corridor
                if (CompanionConfigStore.EnableAntiFriendlyFire && (player.isRangedWeaponAiming || Il2CppSafe.IsAliveUnity(player.rangedWeapon)))
                {
                    if (TryStepOutOfPlayerFireLine(agent, st, player, selfPos, now))
                        return;
                }

                // Rear Flank Radar: warn player if an enemy sneaks behind player's back
                CheckPlayerRearFlank(agent, st, player, now);
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
                int nearbyEnemies = CountNearbyHostiles(selfPos, 13f, hc.characterData?.camp ?? 0);

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

            if (CompanionConfigStore.EnableKiting && st.Doctrine != TacticalDoctrine.Assault)
            {
                float kiteThreshold = st.Doctrine == TacticalDoctrine.SniperKite ? 4.8f : 3.2f;
                bool usingRanged = Il2CppSafe.IsAliveUnity(hc.rangedWeapon) || hc.reloadingMagazineFlag;

                if (usingRanged && distToTarget < kiteThreshold && distToTarget > 0.1f && now - st.LastKiteStepTime > 0.32f)
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
                else if (target.TryCast<HumanCharacterController>() != null && distToTarget < 11.5f && now - st.LastKiteStepTime > 0.85f)
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

                int friendlyCamp = player.characterData?.camp ?? 0;
                for (int i = 0; i < gc.characterList.Count; i++)
                {
                    var c = gc.characterList[i];
                    if (!Il2CppSafe.IsAliveUnity(c) || c.deadFlag) continue;
                    if (Il2CppSafe.IsAlive(c.characterData) && c.characterData.camp == friendlyCamp) continue;

                    Vector2 toEnemy = (Vector2)c.transform.position - playerPos;
                    float dist = toEnemy.magnitude;
                    if (dist < 1.2f || dist > 5.8f) continue;

                    float dot = Vector2.Dot(playerForward, toEnemy.normalized);
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
            if (now - st.LastFireLineStepTime < 0.38f) return false;
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
                if (Il2CppSafe.IsAliveUnity(hc.rangedWeapon) && Il2CppSafe.IsAlive(hc.rangedWeapon.weaponData))
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                    bool jammed = hc.rangedWeapon.isMalfunction;
                    bool reloading = hc.reloadingMagazineFlag;

                    if (st.Doctrine == TacticalDoctrine.StealthMelee || (distToTarget < 1.95f && (loaded == 0 || jammed || reloading)))
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
                else if (Il2CppSafe.IsAliveUnity(hc.meleeWeapon) && st.Doctrine != TacticalDoctrine.StealthMelee && distToTarget > 3.8f)
                {
                    if (GOAPAction_RangeAttack.EquipBestRangedWeapon(agent))
                    {
                        st.LastWeaponSwitchTime = now;
                        SmartChatterSystem.Speak(agent, ChatterTopic.CombatSwitchRanged, minCooldown: 7.5f);
                    }
                }
            }
            catch { }
        }

        private static void TickFieldSupport(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            float dist = Vector2.Distance(agent.humanCharacter.transform.position, player.transform.position);
            if (dist > 6.5f) return;

            if (CompanionConfigStore.EnableFieldMedic && st.AllowFieldMedic && now - st.LastFieldMedicTime > 30f)
            {
                var status = player.characterData?.characterStatusData;
                if (Il2CppSafe.IsAlive(status) && status.maxhp > 10f && (status.hp / status.maxhp) < 0.48f)
                {
                    st.LastFieldMedicTime = now;
                    float healAmount = Mathf.Min(status.maxhp - status.hp, Mathf.Max(25f, status.maxhp * 0.28f));
                    status.hp = Mathf.Min(status.maxhp, status.hp + healAmount);
                    player.hp = status.hp;
                    try { player.HealAllBodyPartInjuries(); } catch { }

                    try
                    {
                        var heals = agent.GetHealingItems();
                        if (heals != null && heals.Count > 0 && agent.npcData?.characterData?.inventoryData != null)
                        {
                            var medItem = heals[0];
                            if (Il2CppSafe.IsAlive(medItem))
                                CharacterActionManager.ConsumeOneFood(agent.npcData.characterData, agent.npcData.characterData.inventoryData, medItem);
                        }
                    }
                    catch { }

                    SmartChatterSystem.Speak(agent, ChatterTopic.FieldMedicAid, minCooldown: 1.5f, bubbleDuration: 6f, force: true);
                }
            }

            if (CompanionConfigStore.EnableAmmoShare && st.AllowAmmoShare && now - st.LastAmmoShareTime > 42f)
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
                            if (Il2CppSafe.IsAliveUnity(ammoAttr))
                            {
                                var ammoPack = ItemManager.instance.GetRandomDataByAttr(ammoAttr, false, false);
                                if (Il2CppSafe.IsAlive(ammoPack))
                                {
                                    ammoPack.itemNumberFloat = Mathf.Clamp(ammoAttr.stackNumber / 2, 8, 30);
                                    if (GameController.instance.AddItemToPlayer(ammoPack, true))
                                    {
                                        st.LastAmmoShareTime = now;
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
                    foreach (var ag in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        if (Il2CppSafe.IsAlive(ag.npcData) && ag.npcData.npcGUID == ownerGuid)
                        {
                            SmartChatterSystem.Speak(ag, ChatterTopic.CombatKillEnemy, minCooldown: 4.8f);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private static int CountNearbyHostiles(Vector2 center, float radius, int friendlyCamp)
        {
            int count = 0;
            float rSqr = radius * radius;
            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || gc.characterList == null) return 0;
                for (int i = 0; i < gc.characterList.Count; i++)
                {
                    var c = gc.characterList[i];
                    if (!Il2CppSafe.IsAliveUnity(c) || c.deadFlag) continue;
                    if (Il2CppSafe.IsAlive(c.characterData) && c.characterData.camp == friendlyCamp) continue;
                    if (((Vector2)c.transform.position - center).sqrMagnitude <= rSqr)
                        count++;
                }
            }
            catch { }
            return count;
        }
    }
}
