using System;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class CustomNpcCommands
    {
        public const string SortBase      = "NPCCommand:Basic.SortBaseContainers";
        public const string SweepLoot     = "NPCCommand:Basic.SweepNearbyLoot";
        public const string DocAssault    = "NPCCommand:Combat.DoctrineAssault";
        public const string DocBodyguard  = "NPCCommand:Combat.DoctrineBodyguard";
        public const string DocSniper     = "NPCCommand:Combat.DoctrineSniper";
        public const string DocStealth    = "NPCCommand:Combat.DoctrineStealth";
        public const string OpenHub       = "NPCCommand:OneTime.OpenSmartHub";
        public const string EmergencyAid  = "NPCCommand:OneTime.EmergencyAid";
        public const string ShareRation   = "NPCCommand:OneTime.ShareRationWithSquad";

        public static readonly string[] AllCustomCommands = new[]
        {
            SortBase,
            SweepLoot,
            DocAssault,
            DocBodyguard,
            DocSniper,
            DocStealth,
            OpenHub,
            EmergencyAid,
            ShareRation
        };

        public static bool TryGetDisplayName(string cmd, out string label)
        {
            bool ru = CompanionConfigStore.UseRussian;
            switch (cmd)
            {
                case "Basic.SortBaseContainers":
                case SortBase:
                    label = ru ? "📦 Сортировать ящики базы" : "📦 Sort Base Containers";
                    return true;
                case "Basic.SweepNearbyLoot":
                case SweepLoot:
                    label = ru ? "🎒 Собрать трофеи вокруг" : "🎒 Sweep Nearby Loot";
                    return true;
                case "Combat.DoctrineAssault":
                case DocAssault:
                    label = ru ? "⚔ Доктрина: Штурм" : "⚔ Doctrine: Assault";
                    return true;
                case "Combat.DoctrineBodyguard":
                case DocBodyguard:
                    label = ru ? "🛡 Доктрина: Телохранитель" : "🛡 Doctrine: Bodyguard";
                    return true;
                case "Combat.DoctrineSniper":
                case DocSniper:
                    label = ru ? "🎯 Доктрина: Снайпер (кайтинг)" : "🎯 Doctrine: Sniper / Kite";
                    return true;
                case "Combat.DoctrineStealth":
                case DocStealth:
                    label = ru ? "🗡 Доктрина: Тихий режим" : "🗡 Doctrine: Stealth Melee";
                    return true;
                case "OneTime.OpenSmartHub":
                case OpenHub:
                    label = ru ? "⚙ Панель напарников и базы [F8]" : "⚙ Smart Companions & Base UI [F8]";
                    return true;
                case "OneTime.EmergencyAid":
                case EmergencyAid:
                    label = ru ? "💉 Полевая помощь / Патроны" : "💉 Field Aid & Ammo Resupply";
                    return true;
                case "OneTime.ShareRationWithSquad":
                case ShareRation:
                    label = ru ? "🍖 Подогреть братву пайком (Бафф)" : "🍖 Share Ration (Fed & Fierce Buff)";
                    return true;
                default:
                    label = null;
                    return false;
            }
        }

        public static bool ExecuteCustomCommand(GOAPNPCData npcData, string commandStr)
        {
            if (string.IsNullOrEmpty(commandStr)) return false;
            bool ru = CompanionConfigStore.UseRussian;

            GOAPAgent agent = npcData?.agentCache;
            if (agent == null && npcData != null && GOAPNPCManager.instance?.activeNPCs != null)
            {
                GOAPNPCManager.instance.activeNPCs.TryGetValue(npcData.npcGUID, out agent);
            }

            var st = npcData != null
                ? CompanionConfigStore.GetOrCreateCompanion(npcData.npcGUID, npcData.characterData?.characterName)
                : null;

            if (commandStr.EndsWith("SortBaseContainers", StringComparison.OrdinalIgnoreCase))
            {
                BaseSortingSystem.CommandSortBase(agent, null, instant: false);
                return true;
            }

            if (commandStr.EndsWith("SweepNearbyLoot", StringComparison.OrdinalIgnoreCase))
            {
                AutoLootSystem.CommandSweepNearbyLoot(agent, 20f);
                return true;
            }

            if (commandStr.EndsWith("DoctrineAssault", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.Assault;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, ru ? "Перехожу в режим штурма! Подавляю врагов огнём!" : "Assault doctrine active! Engaging all targets!", 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineBodyguard", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.Bodyguard;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, ru ? "Держусь рядом и охраняю твою спину, командир!" : "Bodyguard mode active! Staying close to protect you!", 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineSniper", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.SniperKite;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, ru ? "Держу дистанцию и отстреливаю врагов на отходе!" : "Sniper kiting mode active! Keeping firing distance!", 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineStealth", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.StealthMelee;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, ru ? "Убираю огнестрел — работаю тихо клинком!" : "Going silent — melee weapons only!", 5f, true);
                return true;
            }

            if (commandStr.EndsWith("OpenSmartHub", StringComparison.OrdinalIgnoreCase))
            {
                SmartCompanionsUI.ShowMainWindow = true;
                return true;
            }

            if (commandStr.EndsWith("EmergencyAid", StringComparison.OrdinalIgnoreCase))
            {
                if (st != null)
                {
                    st.LastFieldMedicTime = -999f;
                    st.LastAmmoShareTime = -999f;
                }
                var player = GameController.instance?.playerCharacter;
                if (Il2CppSafe.IsAliveUnity(player) && Il2CppSafe.IsAlive(player.characterData?.characterStatusData))
                {
                    var status = player.characterData.characterStatusData;
                    status.hp = Mathf.Min(status.maxhp, status.hp + Mathf.Max(25f, status.maxhp * 0.30f));
                    player.hp = status.hp;
                }
                if (Il2CppSafe.IsAliveUnity(agent))
                    SmartChatterSystem.Speak(agent, ChatterTopic.FieldMedicAid, minCooldown: 0.5f, bubbleDuration: 5.5f, force: true);
                return true;
            }

            if (commandStr.EndsWith("ShareRationWithSquad", StringComparison.OrdinalIgnoreCase))
            {
                SmartChatterSystem.CommandShareRationWithSquad(agent, fromAutoShare: false);
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // 1. Player starts eating/drinking/using food
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.UseFoodAction))]
        [HarmonyPostfix]
        public static void UseFoodAction_Postfix(HumanCharacterController character, ItemData foodItemData)
        {
            try
            {
                SmartChatterSystem.OnPlayerEatOrUseFood(character, foodItemData);
            }
            catch { }
        }

        // 2. Player finishes eating/applying food nutrition (covers instant food use as well)
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.ApplyFoodNutrition))]
        [HarmonyPostfix]
        public static void ApplyFoodNutrition_Postfix(HumanCharacterController character, ItemData foodItemData)
        {
            try
            {
                SmartChatterSystem.OnPlayerEatOrUseFood(character, foodItemData);
            }
            catch { }
        }

        // 3. Player reloads magazine
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.ReloadMagazineAction))]
        [HarmonyPostfix]
        public static void ReloadMagazineAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerReloadAmmo);
            }
            catch { }
        }

        // 4. Player repairs item or vehicle
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.ItemRepairAction))]
        [HarmonyPostfix]
        public static void ItemRepairAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerRepairOrCraft);
            }
            catch { }
        }

        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.VehicleRepairAction))]
        [HarmonyPostfix]
        public static void VehicleRepairAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerRepairOrCraft);
            }
            catch { }
        }

        // 5. Player cooks on BBQ / Fireplace
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.BarbecueAction))]
        [HarmonyPostfix]
        public static void BarbecueAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerCookingBBQ);
            }
            catch { }
        }

        // 6. Player picks lock or breaks door
        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.PickLockAction))]
        [HarmonyPostfix]
        public static void PickLockAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerLockpickBreakDoor);
            }
            catch { }
        }

        [HarmonyPatch(typeof(CharacterActionManager), nameof(CharacterActionManager.BreakDoorAction))]
        [HarmonyPostfix]
        public static void BreakDoorAction_Postfix(HumanCharacterController character)
        {
            try
            {
                SmartChatterSystem.OnPlayerSpecialAction(character, ChatterTopic.PlayerLockpickBreakDoor);
            }
            catch { }
        }

        // 7. Smart Combat AI threat scoring
        [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.GetThreatLevelByCharacter))]
        [HarmonyPostfix]
        public static void GetThreatLevelByCharacter_Postfix(GOAPAgent __instance, BasicCharacterController character, ref float __result)
        {
            try
            {
                SmartCombatAI.AdjustThreatScore(__instance, character, ref __result);
            }
            catch { }
        }

        // 8. Enemy death tracking for companion kill callouts
        [HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.CharacterDead))]
        [HarmonyPostfix]
        public static void CharacterDead_Postfix(BasicCharacterController __instance)
        {
            try
            {
                SmartCombatAI.OnCharacterKilled(__instance);
            }
            catch { }
        }

        // 9. Inject custom commands into GOAPNPCData.GetAllAvailableCommands
        [HarmonyPatch(typeof(GOAPNPCData), nameof(GOAPNPCData.GetAllAvailableCommands))]
        [HarmonyPostfix]
        public static void GetAllAvailableCommands_Postfix(Il2CppSystem.Collections.Generic.List<string> __result)
        {
            try
            {
                if (__result == null) return;
                foreach (var cmd in CustomNpcCommands.AllCustomCommands)
                {
                    if (!__result.Contains(cmd))
                        __result.Add(cmd);
                }
            }
            catch { }
        }

        // 10. Localize custom commands in NPCCommandMenu
        [HarmonyPatch(typeof(NPCCommandManager), nameof(NPCCommandManager.GetCommandDisplayName))]
        [HarmonyPrefix]
        public static bool GetCommandDisplayName_Prefix(string commandStr, ref string __result)
        {
            try
            {
                if (CustomNpcCommands.TryGetDisplayName(commandStr, out var label))
                {
                    __result = label;
                    return false;
                }
            }
            catch { }
            return true;
        }

        // 11. Handle custom commands when clicked in NPCCommandMenu
        [HarmonyPatch(typeof(GOAPNPCData), nameof(GOAPNPCData.ExecuteNPCCommand))]
        [HarmonyPrefix]
        public static bool ExecuteNPCCommand_Prefix(GOAPNPCData __instance, string commandStr)
        {
            try
            {
                if (CustomNpcCommands.ExecuteCustomCommand(__instance, commandStr))
                    return false;
            }
            catch { }
            return true;
        }
    }
}
