using System;
using System.Collections.Generic;
using HarmonyLib;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Keeps malformed human-NPC weapon records from aborting native NPC initialization.
    /// The game can load a saved/modded weapon whose ItemAttr or ranged prefab is unavailable;
    /// EquipRangedWeapon then throws while building the visual weapon, and the NPC never reaches
    /// the network monster replication path. Such an NPC is left unarmed rather than discarded.
    /// </summary>
    internal static class HumanNpcEquipmentGuard
    {
        private static readonly HashSet<string> LoggedInvalidWeapons = new HashSet<string>();

        internal static bool IsLocalHumanNpc(HumanCharacterController human)
        {
            try
            {
                return MultiplayerSyncSystem.IsMultiplayerOnline &&
                    Il2CppSafe.IsAliveUnity(human) && !human.isPlayer &&
                    !NetSessionConfig.IsNetworkPlayer(human);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsValidRangedItem(ItemData item, out string reason)
        {
            reason = "missing item data";
            if (!Il2CppSafe.IsAlive(item)) return false;

            try
            {
                if (ItemManager.instance == null)
                {
                    reason = "ItemManager unavailable";
                    return false;
                }

                var attr = ItemManager.instance.GetItemAttrById(item.itemId);
                if (!Il2CppSafe.IsAlive(attr))
                {
                    reason = $"ItemAttr {item.itemId} unavailable";
                    return false;
                }
                if (attr.isMissingMod)
                {
                    reason = $"ItemAttr {item.itemId} belongs to a missing mod";
                    return false;
                }
                if (attr.TryCast<ItemAttr_RangedWeapon>() == null)
                {
                    reason = $"ItemAttr {item.itemId} is not ranged";
                    return false;
                }

                reason = "";
                return true;
            }
            catch (Exception e)
            {
                reason = $"item {item.itemId} validation threw: {e.Message}";
                return false;
            }
        }

        internal static void LogInvalid(HumanCharacterController human, ItemData item, string reason)
        {
            try
            {
                string name = Il2CppSafe.IsAlive(human.characterData) ? human.characterData.characterName : "NPC";
                int itemId = Il2CppSafe.IsAlive(item) ? item.itemId : 0;
                string key = $"{human.Pointer}:{itemId}:{reason}";
                if (LoggedInvalidWeapons.Add(key))
                    SmartCompanionsPlugin.LogWarning($"[MP NPC] Skipping invalid ranged equipment for {name}: {reason}.");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipRangedWeapon))]
    internal static class HumanNpcEquipRangedWeaponGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (!HumanNpcEquipmentGuard.IsLocalHumanNpc(__instance)) return true;
            if (HumanNpcEquipmentGuard.IsValidRangedItem(m_weaponData, out string reason)) return true;

            HumanNpcEquipmentGuard.LogInvalid(__instance, m_weaponData, reason);
            return false;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (__exception == null || !HumanNpcEquipmentGuard.IsLocalHumanNpc(__instance))
                return __exception;

            // A valid-looking saved item can still reference a missing ranged prefab or
            // incomplete visual data. Do not let that native visual-only failure abort
            // HumanCharacterController.Init and prevent NetMonsterSync from seeing the NPC.
            HumanNpcEquipmentGuard.LogInvalid(__instance, m_weaponData,
                "native EquipRangedWeapon failed: " + __exception.GetType().Name);
            return null;
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.UpdateHand_RangedWeapon))]
    internal static class HumanNpcUpdateRangedWeaponGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanCharacterController __instance)
        {
            if (!HumanNpcEquipmentGuard.IsLocalHumanNpc(__instance)) return true;

            try
            {
                var weapon = __instance.rangedWeapon;
                if (!Il2CppSafe.IsAliveUnity(weapon)) return false;
                if (HumanNpcEquipmentGuard.IsValidRangedItem(weapon.weaponData, out _)) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

    }
}
