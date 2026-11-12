using System;
using HarmonyLib;
using UnityEngine;
namespace ZedZone.SmartCompanions
{
    internal static class RangedEngagementPolicy
    {
        internal static bool CanEngage(GOAPAgent agent, GOAPAgentState state)
        {
            if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(agent) ||
                !Il2CppSafe.IsAlive(state) || !Il2CppSafe.IsAlive(agent.npcData) ||
                CombatResponse.NormalizeCommand(state.combatCommand) != "FireAtWill" ||
                !agent.IsPlayerTeammate() || agent.IsTeammateCombatSuppressed()) return false;
            var profile=CompanionConfigStore.GetOrCreateCompanion(agent.npcData.npcGUID);
            if (!CombatResponse.AllowsRangedResponse(profile, agent.npcData.weaponPreference)) return false;
            var self=agent.humanCharacter;var enemy=agent.mostThreateningCharacter;
            var leader=Il2CppSafe.IsAliveUnity(agent.leaderCharacter) && !agent.leaderCharacter.deadFlag
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
                Mathf.Max(8f, TacticalSquadSystem.GetFollowDistanceMeters(CompanionConfigStore.FollowDistance) + 1.5f);
            bool nearTacticalAnchor = profile.ActiveOrder != TacticalOrderKind.Follow &&
                Vector2.Distance(self.transform.position, profile.OrderAnchorPos) <= 8f;
            if (!nearLeader && !nearTacticalAnchor) return false;
            float distance=Vector2.Distance(self.transform.position,enemy.transform.position);
            if (distance<2.5f || distance>ReadyRange(self)) return false;
            Vector2 muzzle=self.GetProjectileAimOrigin();
            return agent.HasClearFireLine(new Vector3(muzzle.x,muzzle.y,self.transform.position.z),enemy.GetTorsePositionOrRoot());
        }

        internal static float ReadyRange(HumanCharacterController self)
        {
            var eq=self.characterData?.characterEquipmentData;
            if (!Il2CppSafe.IsAlive(eq) || ItemManager.instance==null) return 0f;
            float range=0f;
            for(int slot=0;slot<3;slot++)
            {
                var item=eq.GetWeaponData(slot);
                if(!Il2CppSafe.IsAlive(item))continue;
                var attr=ItemManager.instance.GetItemAttrById(item.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                if(!RangedWeaponCompatibility.Supported(attr) || attr.fireRange<=0f || float.IsNaN(attr.fireRange))continue;
                if(BasicRangedWeapon.GetLoadedAmmo(item)<=0 && BasicRangedWeapon.GetInventoryAmmoTotal(self,item,0)<=0)continue;
                range=Mathf.Max(range,Mathf.Min(24f,attr.fireRange));
            }
            return range;
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
                }
                if(!RangedEngagementPolicy.CanEngage(agent,agentState))return;
                float follow=Il2CppSafe.IsAlive(agent.followLeaderGoal) ? agent.followLeaderGoal.GetPriority(agentState) : 2f;
                // Native FireAtWill adds no command priority; FollowLeader has a
                // floor of 2 even next to the leader. Do not turn this into a forced
                // goal: native validity and higher emergency goals still apply.
                if(!float.IsNaN(follow) && follow<=2.01f) __result=Mathf.Max(__result,follow+0.5f);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GOAPGoal_EliminateThreat), nameof(GOAPGoal_EliminateThreat.IsValid))]
    internal static class CeaseFireEliminateThreatGate
    {
        private static void Postfix(GOAPAgent agent, ref bool __result)
        {
            if (!__result || !Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData)) return;
            try
            {
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
                if(!RangedEngagementPolicy.CanEngage(__instance,__instance?.agentState))return true;
                var self=__instance.humanCharacter;var gun=self.rangedWeapon;
                var current=self.characterData?.characterEquipmentData?.currentWeaponItemData;
                if(!Il2CppSafe.IsAliveUnity(gun) || !Il2CppSafe.IsAlive(gun.weaponData) ||
                    !Il2CppSafe.IsAlive(current) || current.Pointer!=gun.weaponData.Pointer)return true;
                // Native GOAP PullOutWeapon otherwise selects first nonempty slot,
                // which can be a knife, discarding the already selected loaded gun.
                self.PullOutWeapon();
                return false;
            }
            catch { return true; }
        }
    }
}
