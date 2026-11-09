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
            return TryGetDisplayName(cmd, CompanionConfigStore.EffectiveLanguage, out label);
        }

        public static bool TryGetDisplayName(string cmd, ModLanguage lang, out string label)
        {
            switch (cmd)
            {
                case "Basic.SortBaseContainers":
                case SortBase:
                    label = CompanionConfigStore.L(lang, "📦 Сортировать ящики базы", "📦 Sort Base Containers", "📦 整理基地储物箱");
                    return true;
                case "Basic.SweepNearbyLoot":
                case SweepLoot:
                    label = CompanionConfigStore.L(lang, "🎒 Собрать трофеи вокруг", "🎒 Sweep Nearby Loot", "🎒 搜刮附近战利品");
                    return true;
                case "Combat.DoctrineAssault":
                case DocAssault:
                    label = CompanionConfigStore.L(lang, "⚔ Доктрина: Штурм", "⚔ Doctrine: Assault", "⚔ 战术：突击强攻");
                    return true;
                case "Combat.DoctrineBodyguard":
                case DocBodyguard:
                    label = CompanionConfigStore.L(lang, "🛡 Доктрина: Телохранитель", "🛡 Doctrine: Bodyguard", "🛡 战术：贴身护卫");
                    return true;
                case "Combat.DoctrineSniper":
                case DocSniper:
                    label = CompanionConfigStore.L(lang, "🎯 Доктрина: Снайпер (кайтинг)", "🎯 Doctrine: Sniper / Kite", "🎯 战术：狙击风筝");
                    return true;
                case "Combat.DoctrineStealth":
                case DocStealth:
                    label = CompanionConfigStore.L(lang, "🗡 Доктрина: Тихий режим", "🗡 Doctrine: Stealth Melee", "🗡 战术：潜行近战");
                    return true;
                case "OneTime.OpenSmartHub":
                case OpenHub:
                    label = CompanionConfigStore.L(lang, "⚙ Панель напарников и базы [F8]", "⚙ Smart Companions & Base UI [F8]", "⚙ 智能队友与基地面板 [F8]");
                    return true;
                case "OneTime.EmergencyAid":
                case EmergencyAid:
                    label = CompanionConfigStore.L(lang, "💉 Полевая помощь / Патроны", "💉 Field Aid & Ammo Resupply", "💉 战地急救与弹药补给");
                    return true;
                case "OneTime.ShareRationWithSquad":
                case ShareRation:
                    label = CompanionConfigStore.L(lang, "🍖 Подогреть братву пайком (Бафф)", "🍖 Share Ration (Fed & Fierce Buff)", "🍖 分享口粮（饱腹狂暴增益）");
                    return true;
                default:
                    label = null;
                    return false;
            }
        }

        public static bool ExecuteCustomCommand(GOAPNPCData npcData, string commandStr)
        {
            if (string.IsNullOrEmpty(commandStr)) return false;

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
                    SmartChatterSystem.SpeakCustom(agent, CompanionConfigStore.L(
                        "Перехожу в режим штурма! Подавляю врагов огнём!",
                        "Assault doctrine active! Engaging all targets!",
                        "切换至突击模式！火力全开压制目标！"), 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineBodyguard", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.Bodyguard;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, CompanionConfigStore.L(
                        "Держусь рядом и охраняю твою спину, командир!",
                        "Bodyguard mode active! Staying close to protect you!",
                        "切换至护卫模式！紧跟队长，保护你的后背！"), 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineSniper", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.SniperKite;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, CompanionConfigStore.L(
                        "Держу дистанцию и отстреливаю врагов на отходе!",
                        "Sniper kiting mode active! Keeping firing distance!",
                        "切换至狙击风筝模式！保持距离边退边打！"), 5f, true);
                return true;
            }

            if (commandStr.EndsWith("DoctrineStealth", StringComparison.OrdinalIgnoreCase) && st != null)
            {
                st.Doctrine = TacticalDoctrine.StealthMelee;
                SmartCombatAI.ApplyDoctrineToNpcData(agent, st);
                CompanionConfigStore.Save();
                if (agent != null)
                    SmartChatterSystem.SpeakCustom(agent, CompanionConfigStore.L(
                        "Убираю огнестрел — работаю тихо клинком!",
                        "Going silent — melee weapons only!",
                        "收起枪械——切换潜行近战模式！"), 5f, true);
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
