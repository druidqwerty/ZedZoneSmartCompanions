using System;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public static class CustomNpcCommands
    {
        public const string SortBase      = "Basic.SortBaseContainers";
        public const string SweepLoot     = "Basic.SweepNearbyLoot";
        public const string DocAssault    = "Combat.DoctrineAssault";
        public const string DocBodyguard  = "Combat.DoctrineBodyguard";
        public const string DocSniper     = "Combat.DoctrineSniper";
        public const string DocStealth    = "Combat.DoctrineStealth";
        public const string OpenHub       = "OneTime.OpenSmartHub";
        public const string EmergencyAid  = "OneTime.EmergencyAid";
        public const string ShareRation   = "OneTime.ShareRationWithSquad";
        public const string UnloadVehicle = "OneTime.UnloadVehicleToBase";
        public const string BattleCry     = "OneTime.SquadBattleCry";

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
            ShareRation,
            UnloadVehicle,
            BattleCry
        };

        public static bool TryGetDisplayName(string cmd, out string label)
        {
            return TryGetDisplayName(cmd, CompanionConfigStore.EffectiveLanguage, out label);
        }

        public static bool TryGetDisplayName(string cmd, ModLanguage lang, out string label)
        {
            if (cmd != null && cmd.StartsWith("NPCCommand:", StringComparison.Ordinal))
                cmd = cmd.Substring("NPCCommand:".Length);
            // BuildPanelUI asks for a bare node.name; callers may also pass a full path.
            switch (cmd)
            {
                case "SortBaseContainers":
                case SortBase:
                    label = CompanionConfigStore.L(lang, "Сортировка базы", "Sort Base", "基地整理");
                    return true;
                case "SweepNearbyLoot":
                case SweepLoot:
                    label = CompanionConfigStore.L(lang, "Сбор трофеев", "Collect Loot", "搜刮战利品");
                    return true;
                case "DoctrineAssault":
                case DocAssault:
                    label = CompanionConfigStore.L(lang, "Штурм", "Assault", "突击");
                    return true;
                case "DoctrineBodyguard":
                case DocBodyguard:
                    label = CompanionConfigStore.L(lang, "Телохранитель", "Bodyguard", "护卫");
                    return true;
                case "DoctrineSniper":
                case DocSniper:
                    label = CompanionConfigStore.L(lang, "Снайпер", "Sniper", "狙击");
                    return true;
                case "DoctrineStealth":
                case DocStealth:
                    label = CompanionConfigStore.L(lang, "Тихий режим", "Stealth", "潜行");
                    return true;
                case "OpenSmartHub":
                case OpenHub:
                    label = CompanionConfigStore.L(lang, "Панель [F8]", "Squad Panel [F8]", "队友面板 [F8]");
                    return true;
                case "EmergencyAid":
                case EmergencyAid:
                    label = CompanionConfigStore.L(lang, "Помощь и патроны", "Aid & Ammo", "急救与弹药");
                    return true;
                case "ShareRationWithSquad":
                case ShareRation:
                    label = CompanionConfigStore.L(lang, "Поделиться пайком", "Share Ration", "分享口粮");
                    return true;
                case "UnloadVehicleToBase":
                case UnloadVehicle:
                    label = CompanionConfigStore.L(lang, "Разгрузить машину", "Unload Vehicle", "卸载车辆");
                    return true;
                case "SquadBattleCry":
                case BattleCry:
                    label = CompanionConfigStore.L(lang, "Боевой клич [F9]", "Battle Cry [F9]", "战吼 [F9]");
                    return true;
                default:
                    label = null;
                    return false;
            }
        }

        public static bool ExecuteCustomCommand(GOAPNPCData npcData, string commandStr)
        {
            if (string.IsNullOrEmpty(commandStr) || !TryGetDisplayName(commandStr, out _)) return false;

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
                MultiplayerSyncSystem.SyncCompanionStateChange(st);
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
                MultiplayerSyncSystem.SyncCompanionStateChange(st);
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
                MultiplayerSyncSystem.SyncCompanionStateChange(st);
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
                MultiplayerSyncSystem.SyncCompanionStateChange(st);
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

            if (commandStr.EndsWith("UnloadVehicleToBase", StringComparison.OrdinalIgnoreCase))
            {
                BaseChoreSystem.CommandUnloadVehiclesToBase(agent, silentIfEmpty: false);
                return true;
            }

            if (commandStr.EndsWith("SquadBattleCry", StringComparison.OrdinalIgnoreCase))
            {
                SmartCombatAI.CommandSquadBattleCry(agent);
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
                // Remove only our old envelope-form paths if a cached list still has them.
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    string old = __result[i];
                    if (old != null && old.StartsWith("NPCCommand:", StringComparison.Ordinal) &&
                        CustomNpcCommands.TryGetDisplayName(old, out _)) __result.RemoveAt(i);
                }
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

        // 11. Handle custom commands when clicked in NPCCommandMenu & track QuitTeam / JoinPlayerTeam
        [HarmonyPatch(typeof(GOAPNPCData), nameof(GOAPNPCData.ExecuteNPCCommand))]
        [HarmonyPrefix]
        public static bool ExecuteNPCCommand_Prefix(GOAPNPCData __instance, string commandStr)
        {
            try
            {
                if (!string.IsNullOrEmpty(commandStr) && commandStr.EndsWith("QuitTeam", StringComparison.OrdinalIgnoreCase))
                {
                    MultiplayerSyncSystem.OnCompanionDismissed(__instance);
                }
                if (CustomNpcCommands.ExecuteCustomCommand(__instance, commandStr))
                    return false;
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(GOAPNPCData), nameof(GOAPNPCData.ExecuteNPCCommand))]
        [HarmonyPostfix]
        public static void ExecuteNPCCommand_Postfix(GOAPNPCData __instance, string commandStr)
        {
            try
            {
                if (string.IsNullOrEmpty(commandStr) || !Il2CppSafe.IsAlive(__instance)) return;
                if (commandStr.EndsWith("QuitTeam", StringComparison.OrdinalIgnoreCase))
                {
                    MultiplayerSyncSystem.OnCompanionDismissed(__instance);
                }
                else if (commandStr.EndsWith("JoinPlayerTeam", StringComparison.OrdinalIgnoreCase))
                {
                    MultiplayerSyncSystem.OnCompanionRecruited(GameController.instance?.playerCharacter?.characterData, __instance);
                }
            }
            catch { }
        }

        // 12. Prevent GOAPGoal_WarningHumanEnemy (priority 100) from blocking companions from shooting hostile human NPCs
        [HarmonyPatch(typeof(GOAPGoal_WarningHumanEnemy), nameof(GOAPGoal_WarningHumanEnemy.IsValid))]
        [HarmonyPostfix]
        public static void WarningHumanEnemy_IsValid_Postfix(GOAPAgent agent, ref bool __result)
        {
            try
            {
                if (__result && Il2CppSafe.IsAliveUnity(agent) && agent.IsPlayerTeammate())
                {
                    __result = false;
                }
            }
            catch { }
        }

        // 13. Ensure hostile human NPCs have valid MonsterAttr ID & non-colliding camp before SetThreatData,
        // and guard against duplicate dictionary key insertion in GOAPAgent.threats.
        [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.SetThreatData))]
        [HarmonyPrefix]
        public static bool SetThreatData_Prefix(GOAPAgent __instance, BasicCharacterController character, ref GOAPAgent.ThreatData __result)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(character)) return true;
                var human = character.TryCast<HumanCharacterController>();
                if (Il2CppSafe.IsAliveUnity(human) && Il2CppSafe.IsAlive(human.characterData))
                {
                    MultiplayerSyncSystem.NormalizeHumanCharacterData(human.characterData, isLooterHint: true);
                }
                if (Il2CppSafe.IsAliveUnity(__instance) && __instance.threatDataDic != null &&
                    __instance.threatDataDic.TryGetValue(character, out var existing) && Il2CppSafe.IsAlive(existing))
                {
                    existing.position = character.transform.position;
                    if (Il2CppSafe.IsAlive(character.characterData))
                        existing.zLevel = character.characterData.zLevel;
                    existing.memoryExpirationTimeFlag = Time.time + 15f;
                    __result = existing;
                    return false;
                }
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.SetThreatData))]
        [HarmonyPostfix]
        public static void SetThreatData_Postfix(BasicCharacterController character, ref GOAPAgent.ThreatData __result)
        {
            try
            {
                if (!Il2CppSafe.IsAlive(__result) || !Il2CppSafe.IsAliveUnity(character)) return;
                if (character.TryCast<HumanCharacterController>() != null)
                {
                    if (__result.threatLevel < 35f) __result.threatLevel = 35f;
                    if (__result.threatAffectRange < 30f) __result.threatAffectRange = 30f;
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(GOAPAgent_Survivor), nameof(GOAPAgent_Survivor.SetThreatData))]
        [HarmonyPostfix]
        public static void Survivor_SetThreatData_Postfix(BasicCharacterController character, ref GOAPAgent.ThreatData __result)
        {
            try
            {
                if (!Il2CppSafe.IsAlive(__result) || !Il2CppSafe.IsAliveUnity(character)) return;
                if (character.TryCast<HumanCharacterController>() != null)
                {
                    if (__result.threatLevel < 35f) __result.threatLevel = 35f;
                    if (__result.threatAffectRange < 30f) __result.threatAffectRange = 30f;
                }
            }
            catch { }
        }

        // 14. Track human-vs-player/companion combat hits so companions immediately engage hostile humans
        [HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.OnHitByCharacter))]
        [HarmonyPostfix]
        public static void OnHitByCharacter_Postfix(HumanCharacterController __instance, BasicCharacterController offenseCharacter)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(__instance) || !Il2CppSafe.IsAliveUnity(offenseCharacter)) return;
                bool victimIsPlayerOrAlly = __instance.isPlayer ||
                    ZedZone.Net.NetSessionConfig.IsNetworkPlayer(__instance) ||
                    (__instance.GetCachedGOAPAgent() != null && __instance.GetCachedGOAPAgent().IsPlayerTeammate());
                bool attackerIsPlayerOrAlly = offenseCharacter.isPlayer ||
                    ZedZone.Net.NetSessionConfig.IsNetworkPlayer(offenseCharacter) ||
                    (offenseCharacter.GetCachedGOAPAgent() != null && offenseCharacter.GetCachedGOAPAgent().IsPlayerTeammate());

                BasicCharacterController hostileActor = null;
                if (victimIsPlayerOrAlly && !attackerIsPlayerOrAlly)
                    hostileActor = offenseCharacter;
                else if (attackerIsPlayerOrAlly && !victimIsPlayerOrAlly)
                    hostileActor = __instance;

                if (Il2CppSafe.IsAliveUnity(hostileActor))
                {
                    SmartCombatAI.RecordProvokedHuman(hostileActor);
                    foreach (var mate in SmartChatterSystem.GetActiveTeammateAgents())
                    {
                        if (Il2CppSafe.IsAliveUnity(mate))
                        {
                            try
                            {
                                if (mate.threatDataDic == null || !mate.threatDataDic.ContainsKey(hostileActor))
                                    mate.SetThreatData(hostileActor);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        // Human NPCs can be serialized, replicated, and initialized by Unity before the periodic MP scan runs.
        // Normalize before the native Human.Init -> LoadEquipments path so client/host NPCs don't start with
        // characterAttrId 0 (or another missing MonsterAttr) and fail during their very first initialization.
        [HarmonyPatch(typeof(HumanCharacterController), "Init")]
        [HarmonyPrefix]
        public static void HumanCharacterController_Init_Prefix(HumanCharacterController __instance)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(__instance) || __instance.isPlayer ||
                    ZedZone.Net.NetSessionConfig.IsNetworkPlayer(__instance) ||
                    !Il2CppSafe.IsAlive(__instance.characterData)) return;

                var data = __instance.characterData;
                int previousAttrId = data.characterAttrId;
                int previousCamp = data.camp;

                var agent = __instance.GetCachedGOAPAgent();
                bool isLooter = Il2CppSafe.IsAliveUnity(agent) &&
                    (agent.TryCast<GOAPAgent_Looter>() != null ||
                     (Il2CppSafe.IsAlive(agent.npcData) && agent.npcData.isBandit));

                MultiplayerSyncSystem.NormalizeHumanCharacterData(data, isLooter);
                if (previousAttrId != data.characterAttrId || previousCamp != data.camp)
                {
                    SmartCompanionsPlugin.LogInfo(
                        $"[MP NPC] Normalized before Human.Init: {data.characterName ?? "NPC"}, " +
                        $"attr {previousAttrId}->{data.characterAttrId}, camp {previousCamp}->{data.camp}.");
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("[MP NPC] Pre-Init normalization failed: " + e.Message);
            }
        }

        // 15. Multiplayer NPC replication: ensure CharacterData always has valid characterAttrId (201/202) before Serialize & after Deserialize
        [HarmonyPatch(typeof(ZedZone.Net.NetMonsterSync), "SerializeCharacterData")]
        [HarmonyPrefix]
        public static void NetMonsterSync_SerializeCharacterData_Prefix(CharacterData data)
        {
            try
            {
                MultiplayerSyncSystem.NormalizeHumanCharacterData(data);
            }
            catch { }
        }

        [HarmonyPatch(typeof(ZedZone.Net.NetMonsterSync), "DeserializeCharacterData")]
        [HarmonyPostfix]
        public static void NetMonsterSync_DeserializeCharacterData_Postfix(ref CharacterData __result)
        {
            try
            {
                MultiplayerSyncSystem.NormalizeHumanCharacterData(__result);
            }
            catch { }
        }

        [HarmonyPatch(typeof(ZedZone.Net.NetMonsterSync), "SpawnPuppet")]
        [HarmonyPostfix]
        public static void NetMonsterSync_SpawnPuppet_Postfix(ZedZone.Net.NetMonsterSync __instance, ZedZone.Net.MsgMonsterSpawn msg)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(__instance) || !Il2CppSafe.IsAlive(msg) || __instance.m_puppets == null) return;
                if (__instance.m_puppets.TryGetValue(msg.netId, out var puppet) &&
                    Il2CppSafe.IsAlive(puppet) && Il2CppSafe.IsAliveUnity(puppet.controller))
                {
                    sbyte localZ = Il2CppSafe.IsAliveUnity(MapController.instance) ? MapController.instance.currentZLevel : (sbyte)0;
                    MultiplayerSyncSystem.EnsureClientPuppetVisibleAndBound(puppet.controller, localZ);
                }
            }
            catch { }
        }

        // 16. Multiplayer Companion Ownership & Remote Player Leader Resolution
        [HarmonyPatch(typeof(GOAPAgent), "UpdateLeaderState")]
        [HarmonyPostfix]
        public static void GOAPAgent_UpdateLeaderState_Postfix(GOAPAgent __instance)
        {
            try
            {
                MultiplayerSyncSystem.SyncCompanionOwnerAndLeader(__instance);
            }
            catch { }
        }

        [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.IsPlayerTeammate))]
        [HarmonyPostfix]
        public static void GOAPAgent_IsPlayerTeammate_Postfix(GOAPAgent __instance, ref bool __result)
        {
            try
            {
                if (!__result && Il2CppSafe.IsAliveUnity(__instance) && Il2CppSafe.IsAlive(__instance.npcData))
                {
                    if (CompanionConfigStore.CompanionStates.TryGetValue(__instance.npcData.npcGUID ?? "", out var st) &&
                        st != null && st.IsDismissedOrNotInTeam)
                    {
                        return;
                    }
                    if (!__instance.npcData.isBandit && !string.IsNullOrEmpty(__instance.npcData.leaderCharacterGUID))
                    {
                        __result = true;
                    }
                }
            }
            catch { }
        }

        [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.RemoveTeamNPC))]
        [HarmonyPostfix]
        public static void CharacterData_RemoveTeamNPC_Postfix(GOAPNPCData npcData)
        {
            try
            {
                MultiplayerSyncSystem.OnCompanionDismissed(npcData);
            }
            catch { }
        }

        [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.AddTeamNPC))]
        [HarmonyPrefix]
        public static void CharacterData_AddTeamNPC_Prefix()
        {
            try
            {
                CompanionConfigStore.ApplyMaxTeamSizeIfConfigured();
            }
            catch { }
        }

        [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.AddTeamNPC))]
        [HarmonyPostfix]
        public static void CharacterData_AddTeamNPC_Postfix(CharacterData __instance, GOAPNPCData npcData, bool __result)
        {
            try
            {
                if (__result)
                    MultiplayerSyncSystem.OnCompanionRecruited(__instance, npcData);
            }
            catch { }
        }

        [HarmonyPatch(typeof(GOAPAgent), nameof(GOAPAgent.SetLeader), new Type[] { typeof(HumanCharacterController) })]
        [HarmonyPostfix]
        public static void GOAPAgent_SetLeader_Postfix(GOAPAgent __instance, HumanCharacterController m_leaderCharacter)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(m_leaderCharacter) &&
                    Il2CppSafe.IsAliveUnity(__instance) && Il2CppSafe.IsAlive(__instance.npcData) &&
                    string.IsNullOrEmpty(__instance.npcData.leaderCharacterGUID))
                {
                    MultiplayerSyncSystem.OnCompanionDismissed(__instance.npcData);
                }
            }
            catch { }
        }

        // 17. Intercept internal SmartCompanions MP sync packets on NetChatSync before Append
        [HarmonyPatch(typeof(ZedZone.Net.NetChatSync), "OnHostReceive")]
        [HarmonyPrefix]
        public static bool NetChatSync_OnHostReceive_Prefix(ZedZone.Net.MsgChat msg, LiteNetLib.NetPeer peer)
        {
            try
            {
                if (MultiplayerSyncSystem.TryHandleIncomingChatPacket(msg, peer, receivedOnHost: true))
                    return false;
            }
            catch { }
            return true;
        }

        [HarmonyPatch(typeof(ZedZone.Net.NetChatSync), "OnClientReceive")]
        [HarmonyPrefix]
        public static bool NetChatSync_OnClientReceive_Prefix(ZedZone.Net.MsgChat msg, LiteNetLib.NetPeer peer)
        {
            try
            {
                if (MultiplayerSyncSystem.TryHandleIncomingChatPacket(msg, peer, receivedOnHost: false))
                    return false;
            }
            catch { }
            return true;
        }

        // 18. Guard TerrainObject_FirePlace.IgnitionFire(true) from opening fire-lighting UI while container loot panel is open
        [HarmonyPatch(typeof(TerrainObject_FirePlace), "IgnitionFire")]
        [HarmonyPrefix]
        public static bool FirePlace_IgnitionFire_Prefix(TerrainObject_FirePlace __instance, bool __0)
        {
            try
            {
                if (!__0) return true;
                var lootPanel = LootInventoryPanel.instance;
                if (Il2CppSafe.IsAliveUnity(lootPanel) && lootPanel.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }
            catch { }
            return true;
        }

        // 19. Register opened furniture chest's InventoryData <-> TerrainObject mapping and select it in F8
        [HarmonyPatch(typeof(TerrainObject_Furniture_Inventory), nameof(TerrainObject_Furniture_Inventory.LootInventory))]
        [HarmonyPostfix]
        public static void FurnitureInventory_LootInventory_Postfix(TerrainObject_Furniture_Inventory __instance)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(__instance) || !Il2CppSafe.IsAlive(__instance.objectData)) return;
                var inv = __instance.objectData.inventoryData;
                if (Il2CppSafe.IsAlive(inv))
                {
                    BaseSortingSystem.RegisterInventoryTerrainObject(inv, __instance);
                    string id = BaseSortingSystem.GetContainerStableId(inv, __instance);
                    SmartCompanionsUI.SelectContainerInHub(id);
                }
            }
            catch { }
        }

        // 20. Redirect GOAPAction_FollowLeader movement to TargetContainerPos while companion is physically carrying items between chests
        [HarmonyPatch(typeof(GOAPAction_FollowLeader), nameof(GOAPAction_FollowLeader.Execute))]
        [HarmonyPrefix]
        public static bool FollowLeader_Execute_Prefix(GOAPAgent agent, ref bool __result)
        {
            try
            {
                if (!Il2CppSafe.IsAliveUnity(agent) || !Il2CppSafe.IsAlive(agent.npcData) ||
                    !Il2CppSafe.IsAliveUnity(agent.humanCharacter) || agent.humanCharacter.deadFlag)
                    return true;

                if (Il2CppSafe.IsAliveUnity(agent.mostThreateningCharacter) && !agent.mostThreateningCharacter.deadFlag)
                    return true;

                if (CompanionConfigStore.CompanionStates.TryGetValue(agent.npcData.npcGUID ?? "", out var st) &&
                    st != null && st.IsSortingActive && (st.SortingPhase == 1 || st.SortingPhase == 2))
                {
                    float dist = Vector2.Distance(agent.humanCharacter.transform.position, st.TargetContainerPos);
                    if (dist > 1.60f)
                    {
                        if (agent.isStuck || agent.IsStuckRecovering)
                            agent.MoveTo(st.TargetContainerPos, 1.4f, true);
                        else
                            agent.MoveTo_Direct(st.TargetContainerPos, true, true, false);
                        agent.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                    }
                    else
                    {
                        agent.LookAtPositionSmoothCompat(st.TargetContainerPos, 10f);
                    }
                    __result = true;
                    return false;
                }
            }
            catch { }
            return true;
        }

        // 21. Allow companions ordered to CollectResource / CollectSpecifiedItem to roam up to 42m without FollowLeader pulling them back at 12m
        [HarmonyPatch(typeof(GOAPGoal_FollowLeader), nameof(GOAPGoal_FollowLeader.GetPriority))]
        [HarmonyPostfix]
        public static void FollowLeader_GetPriority_Postfix(GOAPAgentState agentState, ref float __result)
        {
            try
            {
                if (!Il2CppSafe.IsAlive(agentState) || !Il2CppSafe.IsAlive(agentState.npcData)) return;
                string cmd = agentState.basicCommand ?? agentState.npcData.basicCommand ?? "";
                if (!cmd.EndsWith("CollectResource", StringComparison.OrdinalIgnoreCase) &&
                    !cmd.EndsWith("CollectSpecifiedItem", StringComparison.OrdinalIgnoreCase))
                    return;

                var ag = agentState.npcData.agentCache;
                if (Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAliveUnity(ag.mostThreateningCharacter) && !ag.mostThreateningCharacter.deadFlag)
                    return;

                var leader = Il2CppSafe.IsAliveUnity(ag) && Il2CppSafe.IsAliveUnity(ag.leaderCharacter)
                    ? ag.leaderCharacter
                    : GameController.instance?.playerCharacter;
                if (Il2CppSafe.IsAliveUnity(leader))
                {
                    float dist = Vector2.Distance(agentState.agentPosition, leader.transform.position);
                    if (dist <= 42f && __result > 1.5f)
                        __result = 1.5f;
                }
            }
            catch { }
        }

        // 22. Lift TeammateScavengeRestricted when companion is ordered to CollectResource / CollectSpecifiedItem or AllowAutoLootNearby is enabled
        [HarmonyPatch(typeof(GOAPAgentState), nameof(GOAPAgentState.IsTeammateScavengeRestricted))]
        [HarmonyPostfix]
        public static void IsTeammateScavengeRestricted_Postfix(GOAPAgentState __instance, ref bool __result)
        {
            try
            {
                if (!__result || !Il2CppSafe.IsAlive(__instance) || !Il2CppSafe.IsAlive(__instance.npcData)) return;
                string cmd = __instance.basicCommand ?? __instance.npcData.basicCommand ?? "";
                if (cmd.EndsWith("CollectResource", StringComparison.OrdinalIgnoreCase) ||
                    cmd.EndsWith("CollectSpecifiedItem", StringComparison.OrdinalIgnoreCase))
                {
                    __result = false;
                    return;
                }
                if (CompanionConfigStore.CompanionStates.TryGetValue(__instance.npcData.npcGUID ?? "", out var st) &&
                    st != null && st.AllowAutoLootNearby)
                {
                    __result = false;
                }
            }
            catch { }
        }

        // 23. Protect player-marked base chests from being scavenged by companions during CollectResource
        [HarmonyPatch(typeof(GOAPAgentState), nameof(GOAPAgentState.IsContainerSearchableByNPC))]
        [HarmonyPostfix]
        public static void IsContainerSearchableByNPC_Postfix(TerrainObject terrainObject, ref bool __result)
        {
            try
            {
                if (!__result || !Il2CppSafe.IsAliveUnity(terrainObject)) return;
                if (AutoLootSystem.IsProtectedPlayerBaseContainer(terrainObject))
                {
                    __result = false;
                }
            }
            catch { }
        }
    }
}
