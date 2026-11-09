using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class SmartCombatAI
    {
        private static readonly Dictionary<int, string> RecentTargetEngageOwner = new Dictionary<int, string>();

        public static void ApplyDoctrineToNpcData(GOAPAgent agent, CompanionRuntimeState st)
        {
            if (agent == null || agent.npcData == null || st == null) return;
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
            if (agent == null || target == null || target.deadFlag) return;
            if (agent.npcData == null || agent.humanCharacter == null) return;

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
                var player = gc != null ? gc.playerCharacter : null;
                Vector2 selfPos = agent.humanCharacter.transform.position;
                Vector2 targetPos = target.transform.position;
                float distToSelf = Vector2.Distance(selfPos, targetPos);

                // 1. Protect the player (Bodyguard & Balanced priority for enemies close to player)
                if (player != null && !player.deadFlag)
                {
                    float distToPlayer = Vector2.Distance(player.transform.position, targetPos);
                    if (distToPlayer < 3.6f)
                    {
                        float mult = st.Doctrine == TacticalDoctrine.Bodyguard ? 2.65f : 1.85f;
                        score *= mult;
                    }
                    else if (distToPlayer < 6.5f && st.Doctrine == TacticalDoctrine.Bodyguard)
                    {
                        score *= 1.45f;
                    }
                }

                // 2. Hostile human NPCs (Bandits / Looters / Raiders)
                var targetHuman = target.TryCast<HumanCharacterController>();
                if (targetHuman != null)
                {
                    score *= 1.75f;
                    if (targetHuman.rangedWeapon != null)
                        score *= 1.35f; // Prioritize enemy shooters!
                }

                // 3. Bosses / Giant Mutants / Armed Zombies
                if (target.FullHP >= 400f || target.TryCast<GiantZombieController>() != null ||
                    target.TryCast<CorpsewheelController>() != null || target.TryCast<CarrionBossController>() != null)
                {
                    score *= 1.65f;
                }
                else
                {
                    var zc = target.TryCast<ZombieController>();
                    if (zc != null && zc.rangedWeapon != null)
                        score *= 1.45f;
                }

                // 4. Finisher bonus on wounded enemies in range
                if (target.FullHP > 1f && (target.hp / target.FullHP) < 0.35f && distToSelf < 9f)
                {
                    score *= 1.35f;
                }
            }
            catch { }
        }

        public static void TickAllCompanionsCombat()
        {
            if (!CompanionConfigStore.EnableSmartCombat) return;
            var teammates = SmartChatterSystem.GetActiveTeammateAgents();
            if (teammates.Count == 0) return;

            var gc = GameController.instance;
            var player = gc != null ? gc.playerCharacter : null;

            foreach (var agent in teammates)
            {
                if (agent == null || agent.humanCharacter == null || agent.humanCharacter.deadFlag || agent.npcData == null)
                    continue;

                var st = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID, agent.npcData.characterData?.characterName);
                if (st.IsSortingActive) continue; // Don't override active base sorting unless attacked

                TickSingleCompanionCombat(agent, st, player);
            }
        }

        private static void TickSingleCompanionCombat(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player)
        {
            float now = Time.unscaledTime;
            var hc = agent.humanCharacter;
            Vector2 selfPos = hc.transform.position;

            // 1. Check Field Medic & Ammo Support for player
            if (player != null && !player.deadFlag)
            {
                TickFieldSupport(agent, st, player, now);

                // 2. Anti-Friendly-Fire Corridor: step aside if blocking player's line of fire
                if (CompanionConfigStore.EnableAntiFriendlyFire && (player.isRangedWeaponAiming || player.rangedWeapon != null))
                {
                    if (TryStepOutOfPlayerFireLine(agent, st, player, selfPos, now))
                        return;
                }
            }

            var target = agent.mostThreateningCharacter;
            if (target == null || target.deadFlag)
            {
                // Out of combat: proactive magazine reload if half-empty
                if (hc.rangedWeapon != null && !hc.reloadingMagazineFlag && hc.rangedWeapon.weaponData != null)
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                    if (loaded == 0)
                    {
                        try { agent.CheckAndReloadMagazine(); } catch { }
                    }
                }
                if (string.IsNullOrEmpty(st.CurrentTaskText) || st.CurrentTaskText.StartsWith("Бой") || st.CurrentTaskText.StartsWith("Combat"))
                {
                    st.CurrentTaskText = CompanionConfigStore.UseRussian ? "Патруль / Сопровождение" : "Escorting Leader";
                }
                return;
            }

            // Track target for kill credit & callouts
            int targetId = target.GetInstanceID();
            RecentTargetEngageOwner[targetId] = st.NpcGuid;

            Vector2 targetPos = target.transform.position;
            float distToTarget = Vector2.Distance(selfPos, targetPos);
            bool ru = CompanionConfigStore.UseRussian;
            var targetName = target.characterData?.characterName ?? "Враг";
            st.CurrentTaskText = ru ? $"Бой: {targetName} ({distToTarget:F1}м)" : $"Combat: {targetName} ({distToTarget:F1}m)";

            // 3. Tactical Callouts (Boss / Bandit / Horde / Saving Player / Wounded)
            if (now - st.LastCombatCalloutTime > 7.5f)
            {
                st.LastCombatCalloutTime = now;
                int nearbyEnemies = CountNearbyHostiles(selfPos, 13f, hc.characterData?.camp ?? 0);

                if (player != null && Vector2.Distance(player.transform.position, targetPos) < 2.8f)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSavePlayer, minCooldown: 6f);
                }
                else if (hc.FullHP > 1f && (hc.hp / hc.FullHP) < 0.35f)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatWounded, minCooldown: 8f);
                }
                else if (target.FullHP >= 400f || target.TryCast<GiantZombieController>() != null || target.TryCast<CorpsewheelController>() != null)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotBoss, minCooldown: 10f);
                }
                else if (target.TryCast<HumanCharacterController>() != null)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotBandit, minCooldown: 8f);
                }
                else if (nearbyEnemies >= 5)
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotHorde, minCooldown: 9f);
                }
                else
                {
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatSpotZombie, minCooldown: 10f);
                }
            }

            // 4. Smart Weapon Switching (Melee vs Ranged based on distance, ammo, and doctrine)
            if (now - st.LastWeaponSwitchTime > 2.2f)
            {
                TrySmartWeaponSwitch(agent, st, distToTarget, now);
            }

            // 5. Tactical Kiting & Evasive Positioning
            if (CompanionConfigStore.EnableKiting && st.Doctrine != TacticalDoctrine.Assault)
            {
                float kiteThreshold = st.Doctrine == TacticalDoctrine.SniperKite ? 4.8f : 3.2f;
                bool usingRanged = hc.rangedWeapon != null || hc.reloadingMagazineFlag;

                if (usingRanged && distToTarget < kiteThreshold && distToTarget > 0.1f && now - st.LastKiteStepTime > 0.35f)
                {
                    st.LastKiteStepTime = now;
                    Vector2 awayDir = (selfPos - targetPos).normalized;
                    if (player != null)
                    {
                        Vector2 toPlayer = ((Vector2)player.transform.position - selfPos);
                        if (toPlayer.magnitude > 9f)
                            awayDir = (awayDir + toPlayer.normalized * 0.45f).normalized;
                    }

                    Vector2 kiteDest = selfPos + awayDir * 2.4f;
                    try
                    {
                        agent.MoveTo_Direct(kiteDest, true, true, false);
                        agent.LookAtPositionSmoothCompat(targetPos, 12f);
                    }
                    catch { }

                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatKiting, minCooldown: 8f);
                }
                else if (target.TryCast<HumanCharacterController>() != null && distToTarget < 11f && now - st.LastKiteStepTime > 0.9f)
                {
                    // Lateral strafe against hostile shooters
                    st.LastKiteStepTime = now;
                    Vector2 toEnemy = (targetPos - selfPos).normalized;
                    Vector2 perp = (Time.frameCount % 120 < 60) ? new Vector2(-toEnemy.y, toEnemy.x) : new Vector2(toEnemy.y, -toEnemy.x);
                    Vector2 strafeDest = selfPos + perp * 1.8f;
                    try
                    {
                        agent.MoveTo_Direct(strafeDest, true, true, false);
                        agent.LookAtPositionSmoothCompat(targetPos, 12f);
                    }
                    catch { }
                }
            }
        }

        private static bool TryStepOutOfPlayerFireLine(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, Vector2 selfPos, float now)
        {
            if (now - st.LastFireLineStepTime < 0.4f) return false;
            try
            {
                Vector2 playerPos = player.transform.position;
                Vector2 toCompanion = selfPos - playerPos;
                float dist = toCompanion.magnitude;
                if (dist < 0.8f || dist > 10.5f) return false;

                // Determine player's aim direction from player's rangedWeapon or torso look direction
                Vector2 aimDir = Vector2.zero;
                if (player.rangedWeapon != null)
                    aimDir = player.rangedWeapon.transform.right;
                if (aimDir.sqrMagnitude < 0.01f)
                    return false;

                aimDir.Normalize();
                float proj = Vector2.Dot(toCompanion, aimDir);
                if (proj <= 0.3f || proj >= dist + 0.5f) return false;

                Vector2 closestOnRay = playerPos + aimDir * proj;
                float perpDist = Vector2.Distance(selfPos, closestOnRay);
                if (perpDist < 1.05f)
                {
                    st.LastFireLineStepTime = now;
                    Vector2 side = new Vector2(-aimDir.y, aimDir.x);
                    if (Vector2.Dot(selfPos - closestOnRay, side) < 0f)
                        side = -side;

                    Vector2 safePos = selfPos + side * 1.9f;
                    agent.MoveTo_Direct(safePos, true, true, false);
                    SmartChatterSystem.Speak(agent, ChatterTopic.CombatClearFireLine, minCooldown: 6.5f);
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
                if (hc.rangedWeapon != null && hc.rangedWeapon.weaponData != null)
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(hc.rangedWeapon.weaponData);
                    bool jammed = hc.rangedWeapon.isMalfunction;
                    bool reloading = hc.reloadingMagazineFlag;

                    // If enemy is right in our face and gun is empty/reloading/jammed (or StealthMelee doctrine), draw melee!
                    if (st.Doctrine == TacticalDoctrine.StealthMelee || (distToTarget < 1.95f && (loaded == 0 || jammed || reloading)))
                    {
                        var bestMelee = agent.FindBestMeleeWeapon(out float bestVal);
                        if (bestMelee != null)
                        {
                            st.LastWeaponSwitchTime = now;
                            agent.DrawWeaponViaSlot(bestMelee);
                            SmartChatterSystem.Speak(agent, ChatterTopic.CombatSwitchMelee, minCooldown: 7f);
                        }
                    }
                    else if (loaded == 0 && !reloading)
                    {
                        int reserve = BasicRangedWeapon.GetInventoryAmmoTotal(hc, hc.rangedWeapon.weaponData, 0);
                        if (reserve == 0)
                            SmartChatterSystem.Speak(agent, ChatterTopic.CombatLowAmmo, minCooldown: 14f);
                    }
                }
                else if (hc.meleeWeapon != null && st.Doctrine != TacticalDoctrine.StealthMelee && distToTarget > 3.8f)
                {
                    // Enemy is far away — switch back to best ranged weapon if we have ammo
                    if (GOAPAction_RangeAttack.EquipBestRangedWeapon(agent))
                    {
                        st.LastWeaponSwitchTime = now;
                        SmartChatterSystem.Speak(agent, ChatterTopic.CombatSwitchRanged, minCooldown: 8f);
                    }
                }
            }
            catch { }
        }

        private static void TickFieldSupport(GOAPAgent agent, CompanionRuntimeState st, HumanCharacterController player, float now)
        {
            float dist = Vector2.Distance(agent.humanCharacter.transform.position, player.transform.position);
            if (dist > 6.5f) return;

            // 1. Field Medic: emergency heal/bandage if player HP < 48%
            if (CompanionConfigStore.EnableFieldMedic && st.AllowFieldMedic && now - st.LastFieldMedicTime > 32f)
            {
                var status = player.characterData?.characterStatusData;
                if (status != null && status.maxhp > 10f && (status.hp / status.maxhp) < 0.48f)
                {
                    st.LastFieldMedicTime = now;
                    float healAmount = Mathf.Min(status.maxhp - status.hp, Mathf.Max(25f, status.maxhp * 0.28f));
                    status.hp = Mathf.Min(status.maxhp, status.hp + healAmount);
                    player.hp = status.hp;

                    // Consume a bandage/medkit from companion if they carry one, otherwise use companion's field kit
                    try
                    {
                        var heals = agent.GetHealingItems();
                        if (heals != null && heals.Count > 0 && agent.npcData?.characterData?.inventoryData != null)
                        {
                            var medItem = heals[0];
                            if (medItem != null)
                                CharacterActionManager.ConsumeOneFood(agent.npcData.characterData, agent.npcData.characterData.inventoryData, medItem);
                        }
                    }
                    catch { }

                    SmartChatterSystem.Speak(agent, ChatterTopic.FieldMedicAid, minCooldown: 2f, bubbleDuration: 6f, force: true);
                }
            }

            // 2. Field Ammo Share: if player's gun is empty in combat, give emergency rounds
            if (CompanionConfigStore.EnableAmmoShare && st.AllowAmmoShare && now - st.LastAmmoShareTime > 45f)
            {
                if (player.rangedWeapon != null && player.rangedWeapon.weaponData != null && agent.mostThreateningCharacter != null)
                {
                    int loaded = BasicRangedWeapon.GetLoadedAmmo(player.rangedWeapon.weaponData);
                    int reserve = BasicRangedWeapon.GetInventoryAmmoTotal(player, player.rangedWeapon.weaponData, 0);
                    if (loaded == 0 && reserve == 0)
                    {
                        var wAttr = player.rangedWeapon.weaponAttr;
                        if (wAttr != null && wAttr.ammoId > 0 && ItemManager.instance != null)
                        {
                            int ammoId = wAttr.ammoId;
                            var ammoAttr = ItemManager.instance.GetItemAttrById(ammoId);
                            if (ammoAttr != null)
                            {
                                var ammoPack = ItemManager.instance.GetRandomDataByAttr(ammoAttr, false, false);
                                if (ammoPack != null)
                                {
                                    ammoPack.itemNumberFloat = Mathf.Clamp(ammoAttr.stackNumber / 2, 8, 30);
                                    if (GameController.instance.AddItemToPlayer(ammoPack, true))
                                    {
                                        st.LastAmmoShareTime = now;
                                        SmartChatterSystem.Speak(agent, ChatterTopic.FieldAmmoShare, minCooldown: 2f, bubbleDuration: 6f, force: true);
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
            if (victim == null) return;
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
                        if (ag.npcData != null && ag.npcData.npcGUID == ownerGuid)
                        {
                            SmartChatterSystem.Speak(ag, ChatterTopic.CombatKillEnemy, minCooldown: 5.5f);
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
                if (gc == null || gc.characterList == null) return 0;
                for (int i = 0; i < gc.characterList.Count; i++)
                {
                    var c = gc.characterList[i];
                    if (c == null || c.deadFlag) continue;
                    if (c.characterData != null && c.characterData.camp == friendlyCamp) continue;
                    if (((Vector2)c.transform.position - center).sqrMagnitude <= rSqr)
                        count++;
                }
            }
            catch { }
            return count;
        }
    }
}
