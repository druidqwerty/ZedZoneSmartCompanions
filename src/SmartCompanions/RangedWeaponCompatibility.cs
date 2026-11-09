using HarmonyLib;
namespace ZedZone.SmartCompanions
{
    // Native EquipRangedWeapon rejects these types, but FindBestRangedWeapon used
    // by RangeAttack includes them. One shared eligible list prevents endless
    // rifle -> liquid/energy tool -> rifle plan failures. Inventory is untouched.
    [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.GetRangedWeapons))]
    internal static class RangedWeaponCompatibility
    {
        internal static bool Supported(ItemAttr_RangedWeapon attr) => Il2CppSafe.IsAlive(attr) &&
            attr.weaponType != RangedWeaponType.LiquidWeapon &&
            attr.weaponType != RangedWeaponType.EnergyWeapon &&
            attr.weaponType != RangedWeaponType.EnergyChargeWeapon &&
            attr.feedingType != ItemAttr_RangedWeapon.FeedingType.Battery;

        private static void Postfix(GOAPAgent __instance, ref Il2CppSystem.Collections.Generic.List<ItemData> __result)
        {
            try
            {
                if (!CompanionConfigStore.EnableSmartCombat || !Il2CppSafe.IsAliveUnity(__instance) ||
                    !__instance.IsPlayerTeammate() || __result == null || ItemManager.instance == null) return;
                var eligible = new Il2CppSystem.Collections.Generic.List<ItemData>();
                for(int i=0;i<__result.Count;i++)
                {
                    var item=__result[i];
                    if(!Il2CppSafe.IsAlive(item))continue;
                    var attr=ItemManager.instance.GetItemAttrById(item.itemId)?.TryCast<ItemAttr_RangedWeapon>();
                    if(Supported(attr))eligible.Add(item);
                }
                __result=eligible;
            }
            catch { } // Preserve the original native result during scene teardown.
        }
    }
}
