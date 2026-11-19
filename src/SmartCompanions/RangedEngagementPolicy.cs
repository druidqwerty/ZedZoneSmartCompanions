using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
namespace ZedZone.SmartCompanions
{
    internal static class RangedEngagementPolicy
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool HasAnyWeaponSlotLockSafe(GOAPAgent agent)
        {
            try { return Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData) && agent.HasAnyWeaponSlotLock(); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool IsWeaponSlotLockedSafe(GOAPAgent agent, int slot)
        {
            try { return Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData) && NPCGearOwnership.IsWeaponSlotLocked(agent.npcData, slot); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool IsInWeaponSlotSafe(GOAPAgent agent, ItemData item)
        {
            try { return Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(item) && agent.IsInWeaponSlot(item); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool IsInLockedWeaponSlotSafe(GOAPAgent agent, ItemData item)
        {
            try { return Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(item) && agent.IsInLockedWeaponSlot(item); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool CanBringWeaponIntoSlotSafe(GOAPAgent agent, ItemData item)
        {
            try { return Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(item) && agent.CanBringWeaponIntoSlot(item); }
            catch { return false; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GOAPAgent ResolveAgentSafe(HumanCharacterController self, GOAPAgent agent)
        {
            if (Il2CppSafe.IsAliveUnity(agent)) return agent;
            try { return Il2CppSafe.IsAliveUnity(self) ? self.GetCachedGOAPAgent() : null; }
            catch { return null; }
        }

        internal static bool HasAnyLockedEquippedWeapon(GOAPAgent agent, HumanCharacterController self)
        {
            agent = ResolveAgentSafe(self, agent);
            if (!HasAnyWeaponSlotLockSafe(agent) || !Il2CppSafe.IsAliveUnity(self)) return false;
            var eq = self.characterData?.characterEquipmentData;
            if (!Il2CppSafe.IsAlive(eq)) return false;
            for (int slot = 0; slot < 3; slot++)
            {
                if (!IsWeaponSlotLockedSafe(agent, slot)) continue;
                if (Il2CppSafe.IsAlive(eq.GetWeaponData(slot)))
                    return true;
            }
            return false;
        }

        internal static bool HasUsableLockedRangedSlot(GOAPAgent agent, HumanCharacterController self)
        {
            agent = ResolveAgentSafe(self, agent);
            if (!HasAnyWeaponSlotLockSafe(agent) || !Il2CppSafe.IsAliveUnity(self) || ItemManager.instance == null) return false;
            var eq = self.characterData?.characterEquipmentData;
            if (!Il2CppSafe.IsAlive(eq)) return false;
            for (int slot = 0; slot < 3; slot++)
            {
                if (!IsWeaponSlotLockedSafe(agent, slot)) continue;
                var item = eq.GetWeaponData(slot);
                if (!Il2CppSafe.IsAlive(item)) continue;
                var attr = ItemManager.instance.GetItemAttrById(item.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                if (!RangedWeaponCompatibility.Supported(attr) || attr.fireRange <= 0f || float.IsNaN(attr.fireRange)) continue;
                if (BasicRangedWeapon.GetLoadedAmmo(item) <= 0 && BasicRangedWeapon.GetInventoryAmmoTotal(self, item, 0) <= 0) continue;
                return true;
            }
            return false;
        }

        internal static bool HasEquippableRangedWeaponRespectingLocks(GOAPAgent agent, HumanCharacterController self)
        {
            agent = ResolveAgentSafe(self, agent);
            if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAliveUnity(self)) return false;
            if (ReadyRange(self, agent) > 0f) return true;
            try
            {
                var guns = agent.GetRangedWeapons(false);
                if (guns == null || guns.Count == 0) return false;
                bool hasLock = HasAnyWeaponSlotLockSafe(agent);
                for (int i = 0; i < guns.Count; i++)
                {
                    var item = guns[i];
                    if (!Il2CppSafe.IsAlive(item) || ItemManager.instance == null) continue;
                    var attr = ItemManager.instance.GetItemAttrById(item.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                    if (!RangedWeaponCompatibility.Supported(attr) || attr.fireRange <= 0f || float.IsNaN(attr.fireRange)) continue;
                    if (BasicRangedWeapon.GetLoadedAmmo(item) <= 0 && BasicRangedWeapon.GetInventoryAmmoTotal(self, item, 0) <= 0) continue;
                    if (!hasLock || CanBringWeaponIntoSlotSafe(agent, item))
                        return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool CanEngage(GOAPAgent agent, GOAPAgentState state) =>
            ShouldEngageThreat(agent, state, requireLineOfSight: true, requireMinRange: true, allowMeleeFallback: false);

        internal static bool ShouldSupersedeFollowForCombat(GOAPAgent agent, GOAPAgentState state) =>
            ShouldEngageThreat(agent, state, requireLineOfSight: false, requireMinRange: false, allowMeleeFallback: true);

        internal static bool ShouldEngageThreat(
            GOAPAgent agent, GOAPAgentState state,
            bool requireLineOfSight, bool requireMinRange, bool allowMeleeFallback)
        {
            if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(agent) ||
                !Il2CppSafe.IsAlive(agent.npcData) ||
                !agent.IsPlayerTeammate() || agent.IsTeammateCombatSuppressed()) return false;
            string combatCmd = CombatResponse.NormalizeCommand(
                (Il2CppSafe.IsAlive(state) ? state.combatCommand : null) ?? agent.npcData.combatCommand);
            if (combatCmd != "FireAtWill") return false;
            var profile = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
            if (profile.Doctrine == TacticalDoctrine.Passive ||
                ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(agent, profile))
                return false;

            var self = agent.humanCharacter;
            var enemy = agent.mostThreateningCharacter;
            var leader = Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
                ? agent.leaderCharacter
                : GameController.instance?.playerCharacter;
            if (!Il2CppSafe.IsAliveUnity(self) || !Il2CppSafe.IsAliveUnity(enemy) || enemy.deadFlag || self.deadFlag ||
                !Il2CppSafe.IsAliveUnity(leader) || !Il2CppSafe.IsAlive(enemy.characterData) ||
                !Il2CppSafe.IsAlive(self.characterData) || enemy.characterData.zLevel != self.characterData.zLevel) return false;
            // While actively disengaging on FallBackHere, do not stop to shoot unless cornered.
            if (profile.ActiveOrder == TacticalOrderKind.FallBackHere && !profile.FallBackArrived &&
                Vector2.Distance(self.transform.position, profile.OrderAnchorPos) > 1.85f &&
                Vector2.Distance(self.transform.position, enemy.transform.position) >= 1.85f) return false;
            // Beyond vanilla's near-leader threshold keep its catch-up priorities, unless holding a tactical anchor.
            bool nearLeader = Vector2.Distance(self.transform.position, leader.transform.position) <=
                Mathf.Max(10f, TacticalSquadSystem.GetFollowDistanceMeters(CompanionConfigStore.FollowDistance) + 3.0f);
            bool nearTacticalAnchor = profile.ActiveOrder != TacticalOrderKind.Follow &&
                Vector2.Distance(self.transform.position, profile.OrderAnchorPos) <= 8f;
            if (!nearLeader && !nearTacticalAnchor) return false;

            float distance = Vector2.Distance(self.transform.position, enemy.transform.position);
            bool allowsRanged = CombatResponse.AllowsRangedResponse(profile, agent.npcData.weaponPreference);
            float readyRange = allowsRanged ? ReadyRange(self, agent) : 0f;
            if (readyRange <= 0f)
                return allowMeleeFallback && distance <= 8f;

            if (requireMinRange && distance < 2.5f) return false;
            if (distance > readyRange) return false;

            Vector2 muzzle = self.GetProjectileAimOrigin();
            Vector3 aimTo = enemy.GetTorsePositionOrRoot();
            bool hasLos = agent.HasClearFireLine(new Vector3(muzzle.x, muzzle.y, self.transform.position.z), aimTo) ||
                          agent.HasClearFireLine(self.GetTorsePositionOrRoot(), aimTo);
            if (requireLineOfSight) return hasLos;
            // When momentarily blocked behind a corner/obstacle (e.g. during combat maneuvering), still keep
            // EliminateThreat higher than FollowLeader within 16m so RangeAttack can step to LoS.
            return hasLos || distance <= 16f;
        }

        internal static float ReadyRange(HumanCharacterController self) => ReadyRange(self, null);

        internal static float ReadyRange(HumanCharacterController self, GOAPAgent agent)
        {
            if (!Il2CppSafe.IsAliveUnity(self)) return 0f;
            var eq=self.characterData?.characterEquipmentData;
            if (!Il2CppSafe.IsAlive(eq) || ItemManager.instance==null) return 0f;
            agent = ResolveAgentSafe(self, agent);
            bool hasLock = HasAnyWeaponSlotLockSafe(agent);
            float lockedRange=0f;
            float unlockedRange=0f;
            for(int slot=0;slot<3;slot++)
            {
                var item=eq.GetWeaponData(slot);
                if(!Il2CppSafe.IsAlive(item))continue;
                var attr=ItemManager.instance.GetItemAttrById(item.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                if(!RangedWeaponCompatibility.Supported(attr) || attr.fireRange<=0f || float.IsNaN(attr.fireRange))continue;
                if(BasicRangedWeapon.GetLoadedAmmo(item)<=0 && BasicRangedWeapon.GetInventoryAmmoTotal(self,item,0)<=0)continue;
                float r=Mathf.Min(24f,attr.fireRange);
                if (hasLock && IsWeaponSlotLockedSafe(agent, slot))
                    lockedRange=Mathf.Max(lockedRange,r);
                else
                    unlockedRange=Mathf.Max(unlockedRange,r);
            }
            if (lockedRange > 0f) return lockedRange;
            return unlockedRange;
        }
    }

    [HarmonyPatch(typeof(GOAPGoal_EliminateThreat), nameof(GOAPGoal_EliminateThreat.GetPriority))]
    internal static class ReadyShotPriority
    {
        private static void Postfix(GOAPAgentState agentState, ref float __result)
        {
            try
            {
                var agent=agentState?.npcData?.agentCache;
                if (Il2CppSafe.IsAliveUnity(agent) && Il2CppSafe.IsAlive(agent.npcData) && agent.IsPlayerTeammate())
                {
                    string combatCmd = CombatResponse.NormalizeCommand(agentState.combatCommand ?? agent.npcData.combatCommand);
                    var profile = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                    if (combatCmd == "StopFire" || profile.Doctrine == TacticalDoctrine.Passive || agent.IsTeammateCombatSuppressed())
                    {
                        __result = -1f;
                        return;
                    }
                    if (ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(agent, profile))
                        return;
                }
                if (!RangedEngagementPolicy.ShouldSupersedeFollowForCombat(agent, agentState)) return;
                float follow=Il2CppSafe.IsAlive(agent.followLeaderGoal) ? agent.followLeaderGoal.GetPriority(agentState) : 2f;
                // Native FireAtWill adds no command priority; FollowLeader has a
                // floor of 2 even next to the leader. Do not turn this into a forced
                // goal: native validity and higher emergency goals still apply.
                if(!float.IsNaN(follow) && follow<=6.5f) __result=Mathf.Max(__result,follow+0.5f);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GOAPGoal_EliminateThreat), nameof(GOAPGoal_EliminateThreat.IsValid))]
    internal static class CeaseFireEliminateThreatGate
    {
        private static void Postfix(GOAPAgent agent, ref bool __result)
        {
            try
            {
                if (!__result || !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return;
                if (!agent.IsPlayerTeammate()) return;
                string combatCmd = CombatResponse.NormalizeCommand(agent.npcData.combatCommand);
                var profile = CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
                if (combatCmd == "StopFire" || profile.Doctrine == TacticalDoctrine.Passive || agent.IsTeammateCombatSuppressed())
                {
                    __result = false;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.PullOutWeapon))]
    internal static class KeepDrawnRangedWeapon
    {
        private static bool Prefix(GOAPAgent __instance)
        {
            try
            {
                if (!RangedEngagementPolicy.ShouldEngageThreat(__instance, __instance?.agentState,
                    requireLineOfSight: false, requireMinRange: false, allowMeleeFallback: false)) return true;
                var self = __instance.humanCharacter;
                var profile = CompanionConfigStore.GetOrCreateCompanion(__instance.npcData?.npcGUID);
                if (profile.Loadout == PreferredLoadout.MeleeFirst &&
                    Il2CppSafe.IsAliveUnity(__instance.mostThreateningCharacter) &&
                    Vector2.Distance(self.transform.position, __instance.mostThreateningCharacter.transform.position) < 6.2f)
                    return true;
                var gun = self.rangedWeapon;
                var current=self.characterData?.characterEquipmentData?.currentWeaponItemData;
                if(!Il2CppSafe.IsAliveUnity(gun) || gun.isMalfunction || !Il2CppSafe.IsAlive(gun.weaponData) ||
                    !Il2CppSafe.IsAlive(current) || current.Pointer!=gun.weaponData.Pointer)return true;
                if (!RangedEngagementPolicy.IsInWeaponSlotSafe(__instance, gun.weaponData)) return true;
                if (BasicRangedWeapon.GetLoadedAmmo(gun.weaponData) <= 0 &&
                    BasicRangedWeapon.GetInventoryAmmoTotal(self, gun.weaponData, 0) <= 0)
                    return true;
                // Respect Patch 1.0.18 NPCGearOwnership weapon slot locks: never pin an unlocked
                // ranged weapon when the player locked a different usable ranged slot.
                if (RangedEngagementPolicy.HasAnyWeaponSlotLockSafe(__instance) &&
                    !RangedEngagementPolicy.IsInLockedWeaponSlotSafe(__instance, gun.weaponData) &&
                    RangedEngagementPolicy.HasUsableLockedRangedSlot(__instance, self))
                    return true;
                // Native GOAP PullOutWeapon otherwise selects first nonempty slot,
                // which can be a knife, discarding the already selected loaded gun.
                self.PullOutWeapon();
                return false;
            }
            catch { return true; }
        }
    }
}
