using System;
using HarmonyLib;
namespace ZedZone.SmartCompanions
{
    // Native DrawWeaponViaSlot unconditionally redraws an existing slot, even when
    // its item is already held. Repeated GOAP equip requests reset the weapon/aim.
    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.DrawWeaponViaSlot))]
    internal static class CombatWeaponStability
    {
        private static bool Prefix(GOAPAgent __instance, ItemData m_weaponData, ref bool __result)
        {
            try
            {
                if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(__instance) ||
                    !Il2CppSafe.IsAlive(m_weaponData) || !__instance.IsPlayerTeammate() ||
                    !Il2CppSafe.IsAliveUnity(__instance.mostThreateningCharacter) ||
                    __instance.mostThreateningCharacter.deadFlag) return true;
                var human = __instance.humanCharacter;
                if (!Il2CppSafe.IsAliveUnity(human) || human.deadFlag || human.holsterWeapon || human.hosterRangedWeapon ||
                    !Il2CppSafe.IsAlive(human.characterData)) return true;
                var gun = human.rangedWeapon;
                var equipment = human.characterData.characterEquipmentData;
                if (!Il2CppSafe.IsAliveUnity(gun) || !gun.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(gun.weaponData) ||
                    !Il2CppSafe.IsAlive(equipment) || !Il2CppSafe.IsAlive(equipment.currentWeaponItemData)) return true;
                if (gun.weaponData.Pointer != m_weaponData.Pointer ||
                    equipment.currentWeaponItemData.Pointer != m_weaponData.Pointer) return true;
                __result = true; // This exact native item is already drawn; no action reset.
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
                    __instance.deadFlag || __instance.holsterWeapon || __instance.hosterRangedWeapon ||
                    !Il2CppSafe.IsAlive(__instance.characterData)) return true;
                var agent = __instance.GetComponent<GOAPAgent>();
                if (!Il2CppSafe.IsAliveUnity(agent) || !agent.IsPlayerTeammate() ||
                    !Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) || agent.mostThreateningCharacter.deadFlag) return true;
                var equipment = __instance.characterData.characterEquipmentData;
                var gun = __instance.rangedWeapon;
                if (!Il2CppSafe.IsAlive(equipment) || equipment.currentWeaponIndex != __0 ||
                    !Il2CppSafe.IsAliveUnity(gun) || !gun.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(gun.weaponData) ||
                    !Il2CppSafe.IsAlive(equipment.currentWeaponItemData)) return true;
                var requested = equipment.GetWeaponData(__0);
                if (!Il2CppSafe.IsAlive(requested)) return true;
                return requested.Pointer != gun.weaponData.Pointer ||
                    equipment.currentWeaponItemData.Pointer != requested.Pointer;
            }
            catch { return true; }
        }
    }
}
