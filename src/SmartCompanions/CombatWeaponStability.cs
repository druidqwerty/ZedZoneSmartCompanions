using System;
using HarmonyLib;
namespace ZedZone.SmartCompanions
{
    internal static class CombatWeaponStabilityHelper
    {
        internal static bool TryStabilizeEquippedRangedSlot(
            GOAPAgent agent, HumanCharacterController human, ItemData requested, int expectedSlot)
        {
            if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(agent) ||
                !Il2CppSafe.IsAlive(requested) || !agent.IsPlayerTeammate() ||
                !Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) ||
                agent.mostThreateningCharacter.deadFlag)
                return false;

            if (!Il2CppSafe.IsAliveUnity(human) || human.deadFlag ||
                human.weaponHolsteredByDowned || human.weaponHolsteredByAid ||
                human.weaponHolsteredByBackpack || !Il2CppSafe.IsAlive(human.characterData))
                return false;

            var gun = human.rangedWeapon;
            var equipment = human.characterData.characterEquipmentData;
            if (!Il2CppSafe.IsAliveUnity(gun) || gun.isMalfunction ||
                !Il2CppSafe.IsAlive(gun.weaponData) || !Il2CppSafe.IsAlive(equipment) ||
                !Il2CppSafe.IsAlive(equipment.currentWeaponItemData))
                return false;

            if (expectedSlot >= 0 && equipment.currentWeaponIndex != expectedSlot)
                return false;

            if (gun.weaponData.Pointer != requested.Pointer ||
                equipment.currentWeaponItemData.Pointer != requested.Pointer)
                return false;

            if (!RangedEngagementPolicy.IsInWeaponSlotSafe(agent, requested))
                return false;

            if (RangedEngagementPolicy.HasAnyWeaponSlotLockSafe(agent) &&
                !RangedEngagementPolicy.IsInLockedWeaponSlotSafe(agent, requested) &&
                RangedEngagementPolicy.HasAnyLockedEquippedWeapon(agent, human))
                return false;

            // If this exact ranged weapon is merely holstered (or hosterRangedWeapon flag is stale),
            // reactivate it in place instead of letting EquipRangedWeapon destroy and recreate the gun.
            if (human.holsterWeapon || !gun.gameObject.activeInHierarchy)
            {
                try { human.PullOutWeapon(); } catch { }
            }
            return true;
        }
    }

    // Native DrawWeaponViaSlot unconditionally redraws an existing slot, even when
    // its item is already held. Repeated GOAP equip requests reset the weapon/aim.
    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.DrawWeaponViaSlot))]
    internal static class CombatWeaponStability
    {
        private static bool Prefix(GOAPAgent __instance, ItemData m_weaponData, ref bool __result)
        {
            try
            {
                var human = Il2CppSafe.IsAliveUnity(__instance) ? __instance.humanCharacter : null;
                if (!CombatWeaponStabilityHelper.TryStabilizeEquippedRangedSlot(__instance, human, m_weaponData, -1))
                    return true;
                __result = true; // This exact native item is already selected; no action/aim reset.
                return false;
            }
            catch { return true; }
        }
    }

    // The native EquipRangedWeapon action calls this method directly, bypassing
    // GOAPAgent.DrawWeaponViaSlot. Keep the already-drawn slot stable there too.
    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipWeapon_WeaponSlot))]
    internal static class CombatWeaponSlotStability
    {
        private static bool Prefix(HumanCharacterController __instance, int __0)
        {
            try
            {
                if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(__instance) ||
                    !Il2CppSafe.IsAlive(__instance.characterData)) return true;
                var agent = __instance.GetCachedGOAPAgent() ?? __instance.GetComponent<GOAPAgent>();
                var requested = __instance.characterData.characterEquipmentData?.GetWeaponData(__0);
                return !CombatWeaponStabilityHelper.TryStabilizeEquippedRangedSlot(agent, __instance, requested, __0);
            }
            catch { return true; }
        }
    }
}
