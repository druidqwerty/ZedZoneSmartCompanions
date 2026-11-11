using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// Protects HumanCharacterController equipment initialization and LateUpdate hand IK
    /// for local/host NPCs, client NPC puppets, and remote player puppets.
    ///
    /// Root causes verified in GameAssembly.dll x64 disassembly + Player-prev.log:
    /// 1. NetPlayerSync.ApplyEquipmentVisuals (0xC0E440) calls EquipRangedWeapon without
    ///    allocating CharacterData.inventoryData (unlike NetApplyNPCEquipment at 0xED9B8B),
    ///    throwing NullReferenceException at 0xEC5CCA when a remote player joins.
    /// 2. Any exception inside HumanCharacterController.Init -> LoadEquipments (0xED11B3)
    ///    aborts Init before hand1Elbow/hand2Elbow (+0xB30/+0xB38) are bound AND aborts
    ///    BasicCharacterController.Start (0xC5E80A) before GameController.AddCharacter (0xC5E960),
    ///    making the NPC invisible to NetMonsterSync.HostUpdateInterest and causing 60/sec NREs
    ///    in UpdateHand_RangedWeapon (0xF03870).
    /// </summary>
    internal static class HumanNpcEquipmentGuard
    {
        private static readonly HashSet<string> LoggedIssues = new HashSet<string>();

        internal static bool IsNonLocalPlayerHuman(HumanCharacterController human)
        {
            if (!Il2CppSafe.IsAliveUnity(human)) return false;
            try
            {
                return !human.isPlayer;
            }
            catch
            {
                return false;
            }
        }

        internal static void EnsureBackingInventory(HumanCharacterController human)
        {
            if (!IsNonLocalPlayerHuman(human)) return;
            try
            {
                var cd = human.characterData;
                if (Il2CppSafe.IsAlive(cd) && !Il2CppSafe.IsAlive(cd.inventoryData))
                {
                    var backpackInv = cd.characterEquipmentData?.backpackItemData?.inventoryData;
                    cd.inventoryData = Il2CppSafe.IsAlive(backpackInv) ? backpackInv : new InventoryData();
                }
            }
            catch { }
        }

        internal static bool IsValidWeaponItem(ItemData item, bool expectRanged, out string reason)
        {
            reason = "missing item data";
            if (!Il2CppSafe.IsAlive(item)) return false;

            try
            {
                var im = ItemManager.instance;
                if (!Il2CppSafe.IsAlive(im))
                {
                    reason = "ItemManager unavailable";
                    return false;
                }

                var attr = im.GetItemAttrById(item.itemId);
                if (!Il2CppSafe.IsAliveUnity(attr))
                {
                    reason = $"ItemAttr {item.itemId} unavailable";
                    return false;
                }
                if (attr.isMissingMod)
                {
                    reason = $"ItemAttr {item.itemId} belongs to a missing mod";
                    return false;
                }
                if (expectRanged && attr.TryCast<ItemAttr_RangedWeapon>() == null)
                {
                    reason = $"ItemAttr {item.itemId} is not ItemAttr_RangedWeapon";
                    return false;
                }
                if (!expectRanged && attr.TryCast<ItemAttr_MeleeWeapon>() == null)
                {
                    reason = $"ItemAttr {item.itemId} is not ItemAttr_MeleeWeapon";
                    return false;
                }

                reason = "";
                return true;
            }
            catch (Exception e)
            {
                reason = $"item {item.itemId} validation threw {e.GetType().Name}";
                return false;
            }
        }

        internal static void CleanupBrokenRangedState(HumanCharacterController human)
        {
            if (!Il2CppSafe.IsAliveUnity(human)) return;
            try
            {
                if (!Il2CppSafe.IsAliveUnity(human.rangedWeapon))
                {
                    if (Il2CppSafe.IsAliveUnity(human.weaponAttr) &&
                        human.weaponAttr.TryCast<ItemAttr_RangedWeapon>() != null)
                    {
                        human.weaponAttr = null;
                        human.weaponData = null;
                    }
                    var rParent = human.rangedWeaponParent;
                    if (Il2CppSafe.IsAliveUnity(rParent) && Il2CppSafe.IsAliveUnity(rParent.gameObject))
                        rParent.gameObject.SetActive(false);
                }
            }
            catch { }
        }

        internal static void LogOnce(HumanCharacterController human, ItemData item, string reason)
        {
            try
            {
                string name = Il2CppSafe.IsAlive(human.characterData) ? (human.characterData.characterName ?? "Human") : "Human";
                int itemId = Il2CppSafe.IsAlive(item) ? item.itemId : 0;
                string key = $"{human.Pointer}:{itemId}:{reason}";
                if (LoggedIssues.Count > 512) LoggedIssues.Clear();
                if (LoggedIssues.Add(key))
                    SmartCompanionsPlugin.LogWarning($"[EquipmentGuard] Skipped broken weapon on {name} (item={itemId}): {reason}");
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.LoadEquipments), new Type[] { typeof(bool) })]
    internal static class HumanLoadEquipmentsGuard
    {
        [HarmonyPrefix]
        private static void Prefix(HumanCharacterController __instance)
        {
            HumanNpcEquipmentGuard.EnsureBackingInventory(__instance);
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipRangedWeapon))]
    internal static class HumanNpcEquipRangedWeaponGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (!HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance)) return true;
            HumanNpcEquipmentGuard.EnsureBackingInventory(__instance);

            if (HumanNpcEquipmentGuard.IsValidWeaponItem(m_weaponData, expectRanged: true, out string reason))
                return true;

            HumanNpcEquipmentGuard.CleanupBrokenRangedState(__instance);
            HumanNpcEquipmentGuard.LogOnce(__instance, m_weaponData, reason);
            return false;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (__exception == null || !HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance))
                return __exception;

            HumanNpcEquipmentGuard.CleanupBrokenRangedState(__instance);
            HumanNpcEquipmentGuard.LogOnce(__instance, m_weaponData, "native EquipRangedWeapon threw " + __exception.GetType().Name);
            return null;
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipMeleeWeapon))]
    internal static class HumanNpcEquipMeleeWeaponGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (!HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance)) return true;
            HumanNpcEquipmentGuard.EnsureBackingInventory(__instance);

            if (HumanNpcEquipmentGuard.IsValidWeaponItem(m_weaponData, expectRanged: false, out string reason))
                return true;

            HumanNpcEquipmentGuard.LogOnce(__instance, m_weaponData, reason);
            return false;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (__exception == null || !HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance))
                return __exception;

            HumanNpcEquipmentGuard.LogOnce(__instance, m_weaponData, "native EquipMeleeWeapon threw " + __exception.GetType().Name);
            return null;
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.EquipWeaponSlot))]
    internal static class HumanNpcEquipWeaponSlotGuard
    {
        [HarmonyPrefix]
        private static void Prefix(HumanCharacterController __instance)
        {
            HumanNpcEquipmentGuard.EnsureBackingInventory(__instance);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, HumanCharacterController __instance, ItemData m_weaponData)
        {
            if (__exception == null || !HumanNpcEquipmentGuard.IsNonLocalPlayerHuman(__instance))
                return __exception;

            HumanNpcEquipmentGuard.CleanupBrokenRangedState(__instance);
            HumanNpcEquipmentGuard.LogOnce(__instance, m_weaponData, "native EquipWeaponSlot threw " + __exception.GetType().Name);
            return null;
        }
    }

    [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.UpdateHand_RangedWeapon))]
    internal static class HumanNpcUpdateRangedWeaponGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(HumanCharacterController __instance)
        {
            if (!Il2CppSafe.IsAliveUnity(__instance)) return false;
            try
            {
                var weapon = __instance.rangedWeapon;
                if (!Il2CppSafe.IsAliveUnity(weapon) || !Il2CppSafe.IsAliveUnity(weapon.weaponAttr))
                    return false;
                if (!Il2CppSafe.IsAlive(__instance.characterData))
                    return false;
                if (!Il2CppSafe.IsAliveUnity(__instance.hand1Elbow) || !Il2CppSafe.IsAliveUnity(__instance.hand2Elbow))
                    return false;
                if (!Il2CppSafe.IsAliveUnity(__instance.hand1Transform) || !Il2CppSafe.IsAliveUnity(__instance.hand2Transform))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
