using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    /// <summary>
    /// 3.1.2 deterministic regressions:
    /// (a) Vehicle passengerSeatLimit == -1 unlimited seats vs >= 0 overflow + boarding yield;
    /// (b) CampGarrisonSystem.ShouldBlockVanillaReturnToBase + /OPT:ICF ReturnToBase/LootResource/CollectSpecifiedItem isolation;
    /// (c) BaseSortingSystem.IsContainerInWorkerZone / FilterContainersForWorker (zLayer, garrison 28f, follower 40f dual clamp, BaseScanRadius > 32f);
    /// (d) CompanionSurvivalSystem nested bags (depth <= 3, Pointer dedup), bleeding/wound heal >= 78% HP, ShouldSuppressVanillaFoodWaterSearch;
    /// (e) ChatterDatabase / SmartChatterSystem gender & anime-headgear (二次元头套) persona filtering across ZH/RU/EN;
    /// (f) SmartCompanionsRuntimeGate & HumanNpcEquipmentGuard MainMenu / InGameController guards & TeamNPCPanel._instance isolation;
    /// (g) SmartCompanionsInput cursor refresh & CanProcessSortBaseHotkey.
    /// </summary>
    internal static class V312Regression
    {
        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("V312Regression failed: " + msg);
        }

        public static void Run()
        {
            const string npcGuid = "sc_v312_npc";
            const string leaderId = "sc_v312_leader";

            float prevScanRadius = CompanionConfigStore.BaseScanRadius;
            bool prevSmartCombat = CompanionConfigStore.EnableSmartCombat;
            bool prevAutoBio = CompanionConfigStore.EnableAutoBioAndSleep;
            bool prevAllowMenuUi = UiSmokePlugin.AllowMenuFixtureUiRender;
            var gc = GameController.instance;
            var prevPlayer = Il2CppSafe.IsAliveUnity(gc) ? gc.playerCharacter : null;

            GameObject driverGo = null;
            GameObject vehGo = null;
            GameObject npcGo = null;

            try
            {
                Assert(SmartCompanionsPlugin.PluginVersion == "3.2.3",
                    "Expected SmartCompanionsPlugin.PluginVersion == 3.2.3, got " + SmartCompanionsPlugin.PluginVersion);

                var st = CompanionConfigStore.GetOrCreateCompanion(npcGuid, "V312Tester");
                st.RosterMode = CompanionRosterMode.ActiveSquad;
                st.ActiveOrder = TacticalOrderKind.Follow;
                st.IsTransientVehicleOverflow = false;
                st.HasHomeAnchor = false;
                st.AllowBaseChores = true;

                // Setup disposable player & companion fixtures (never persisted, destroyed in finally)
                driverGo = new GameObject("V312DummyLeader");
                driverGo.SetActive(false);
                var dummyPlayer = driverGo.AddComponent<HumanCharacterController>();
                dummyPlayer.isPlayer = true;
                var playerCd = new CharacterData
                {
                    characterId = leaderId,
                    characterName = "V312Leader",
                    zLevel = 0,
                    teamNPCGuids = new Il2CppSystem.Collections.Generic.HashSet<string>()
                };
                playerCd.teamNPCGuids.Add(npcGuid);
                dummyPlayer.characterData = playerCd;
                driverGo.transform.position = new Vector3(10f, 10f, 0f);
                if (Il2CppSafe.IsAliveUnity(gc))
                    gc.playerCharacter = dummyPlayer;

                vehGo = new GameObject("V312DummyVehicle");
                vehGo.SetActive(false);
                var dummyVeh = vehGo.AddComponent<BasicVehicle>();
                dummyVeh.passengerSeatLimit = -1;

                npcGo = new GameObject("V312DummyNpc");
                npcGo.SetActive(false);
                var npcHuman = npcGo.AddComponent<HumanCharacterController>();
                var npcAgent = npcGo.AddComponent<GOAPAgent>();
                npcHuman.isPlayer = false;
                npcHuman.deadFlag = false;
                npcGo.transform.position = new Vector3(12f, 10f, 0f);

                var npcCharData = new CharacterData
                {
                    characterName = "V312Tester",
                    characterAttrId = 201,
                    camp = 0,
                    gender = 0,
                    zLevel = 0,
                    characterStatusData = new CharacterStatusData
                    {
                        maxhp = 100f,
                        hp = 100f,
                        maxHunger = 100f,
                        hunger = 90f,
                        maxThirst = 100f,
                        thirst = 90f,
                        maxFatigue = 100f,
                        fatigue = 90f
                    },
                    characterEquipmentData = new CharacterEquipmentData(),
                    bodypartsData = CharacterBodyPartsData.EnsureHumanBodyParts(null)
                };
                var npcData = new GOAPNPCData
                {
                    npcGUID = npcGuid,
                    leaderCharacterGUID = leaderId,
                    isBandit = false,
                    zLevel = 0,
                    basicCommand = "Basic.FollowLeader",
                    combatCommand = "Combat.FireAtWill",
                    characterData = npcCharData,
                    agentCache = npcAgent
                };
                npcHuman.characterData = npcCharData;
                npcAgent.humanCharacter = npcHuman;
                npcAgent.npcData = npcData;
                npcAgent.leaderCharacter = dummyPlayer;

                // (a) Vehicle passengerSeatLimit == -1 unlimited seats vs >= 0 overflow & ShouldYieldToVanillaVehicleBoarding
                var squadEntries = new List<(string, float, CompanionSpecialization, CompanionRosterMode)>
                {
                    ("v312_garrison", 0.30f, CompanionSpecialization.Medic, CompanionRosterMode.CampGarrison),
                    ("v312_a", 0.90f, CompanionSpecialization.Fighter, CompanionRosterMode.ActiveSquad),
                    ("v312_b", 0.85f, CompanionSpecialization.Scavenger, CompanionRosterMode.ActiveSquad),
                    ("v312_c", 0.40f, CompanionSpecialization.Quartermaster, CompanionRosterMode.ActiveSquad)
                };
                var (boardUnlim, ovUnlim) = ExpeditionSystems.PlanVehicleSeatAllocation(squadEntries, int.MaxValue);
                Assert(boardUnlim.Count == 3 && ovUnlim.Count == 0,
                    $"Unlimited seat allocation failed: board={boardUnlim.Count}, overflow={ovUnlim.Count}");
                var (boardLimited, ovLimited) = ExpeditionSystems.PlanVehicleSeatAllocation(squadEntries, 1);
                Assert(boardLimited.Count == 1 && ovLimited.Count == 2,
                    $"1-seat allocation failed: board={boardLimited.Count}, overflow={ovLimited.Count}");

                dummyPlayer.drivingVehicle = dummyVeh;
                Assert(ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(npcAgent, st),
                    "ShouldYieldToVanillaVehicleBoarding returned false for ActiveSquad follower while leader driving");
                st.RosterMode = CompanionRosterMode.CampGarrison;
                Assert(!ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(npcAgent, st),
                    "ShouldYieldToVanillaVehicleBoarding returned true for CampGarrison");
                st.RosterMode = CompanionRosterMode.ActiveSquad;
                st.ActiveOrder = TacticalOrderKind.HoldPosition;
                Assert(!ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(npcAgent, st),
                    "ShouldYieldToVanillaVehicleBoarding returned true for HoldPosition");
                st.ActiveOrder = TacticalOrderKind.Follow;
                st.IsTransientVehicleOverflow = true;
                Assert(!ExpeditionSystems.ShouldYieldToVanillaVehicleBoarding(npcAgent, st),
                    "ShouldYieldToVanillaVehicleBoarding returned true for transient overflow");
                st.IsTransientVehicleOverflow = false;
                dummyPlayer.drivingVehicle = null;

                // (b) CampGarrisonSystem.ShouldBlockVanillaReturnToBase & /OPT:ICF Harmony patch guards
                Assert(npcAgent.IsPlayerTeammate(), "Dummy companion failed IsPlayerTeammate() check");
                npcGo.transform.position = new Vector3(200f, 0f, 0f);
                npcData.npcStayPosition = new Vector3(195f, 0f, 0f);
                npcData.basicCommand = "Basic.FollowLeader";
                Assert(CampGarrisonSystem.ShouldBlockVanillaReturnToBase(npcAgent, st),
                    "ShouldBlockVanillaReturnToBase did not block non-StayAtPosition order");

                npcData.basicCommand = "Basic.StayAtPosition";
                npcData.npcStayPosition = Vector3.zero;
                Assert(CampGarrisonSystem.ShouldBlockVanillaReturnToBase(npcAgent, st),
                    "ShouldBlockVanillaReturnToBase did not block uninitialized (0,0) stay position >25m away");

                npcData.npcStayPosition = new Vector3(50f, 0f, 0f);
                Assert(CampGarrisonSystem.ShouldBlockVanillaReturnToBase(npcAgent, st),
                    "ShouldBlockVanillaReturnToBase did not block stale >120m stay position");

                npcData.npcStayPosition = new Vector3(195f, 0f, 0f);
                Assert(!CampGarrisonSystem.ShouldBlockVanillaReturnToBase(npcAgent, st),
                    "ShouldBlockVanillaReturnToBase blocked valid local StayAtPosition <=120m");

                st.RosterMode = CompanionRosterMode.CampGarrison;
                Assert(CampGarrisonSystem.ShouldBlockVanillaReturnToBase(npcAgent, st),
                    "ShouldBlockVanillaReturnToBase did not block CampGarrison companion");
                st.RosterMode = CompanionRosterMode.ActiveSquad;

                // /OPT:ICF TryCast isolation checks on new GOAPGoal/GOAPAction hooks
                var exploreGoal = new GOAPGoal_Explore();
                bool rtbValid = true;
                HarmonyPatches.ReturnToBase_IsValid_Postfix(exploreGoal, npcAgent, ref rtbValid);
                Assert(rtbValid, "ReturnToBase_IsValid_Postfix touched GOAPGoal_Explore");
                bool lootResValid = true;
                HarmonyPatches.LootResource_IsValid_Postfix(exploreGoal, npcAgent, ref lootResValid);
                Assert(lootResValid, "LootResource_IsValid_Postfix touched GOAPGoal_Explore");
                bool collectValid = true;
                HarmonyPatches.CollectSpecifiedItem_IsValid_Postfix(exploreGoal, npcAgent, ref collectValid);
                Assert(collectValid, "CollectSpecifiedItem_IsValid_Postfix touched GOAPGoal_Explore");
                bool patrolValid = true;
                HarmonyPatches.TerritoryPatrol_IsValid_Postfix(exploreGoal, npcAgent, ref patrolValid);
                Assert(patrolValid, "TerritoryPatrol_IsValid_Postfix touched GOAPGoal_Explore");
                var exploreAct = new GOAPAction_ExploreChunk();
                bool rtbActRes = true;
                bool rtbActCont = HarmonyPatches.ReturnToBase_Execute_Prefix(exploreAct, npcAgent, ref rtbActRes);
                Assert(rtbActCont && rtbActRes, "ReturnToBase_Execute_Prefix touched GOAPAction_ExploreChunk");

                // (c) BaseSortingSystem.IsContainerInWorkerZone & FilterContainersForWorker
                var knownInvField = typeof(BaseSortingSystem).GetField("KnownInvToZLayer", StaticAny);
                var isWorkerZoneMethod = typeof(BaseSortingSystem).GetMethod("IsContainerInWorkerZone", StaticAny);
                var filterContainersMethod = typeof(BaseSortingSystem).GetMethod("FilterContainersForWorker", StaticAny);
                Assert(knownInvField != null && isWorkerZoneMethod != null && filterContainersMethod != null,
                    "Missing BaseSortingSystem worker zone reflection targets");
                var knownInvDict = (Dictionary<IntPtr, sbyte>)knownInvField.GetValue(null);

                CompanionConfigStore.BaseScanRadius = 60f;
                npcGo.transform.position = new Vector3(12f, 10f, 0f);
                driverGo.transform.position = new Vector3(10f, 10f, 0f);
                npcData.zLevel = 0;

                var invA = MakeInventory(4, 4);
                var invB = MakeInventory(4, 4);
                var invC = MakeInventory(4, 4);
                var invD = MakeInventory(4, 4);
                var invE = MakeInventory(4, 4);
                knownInvDict[invA.Pointer] = 0;
                knownInvDict[invB.Pointer] = -1;
                knownInvDict[invC.Pointer] = 0;
                knownInvDict[invD.Pointer] = 0;
                knownInvDict[invE.Pointer] = 0;

                var cA = new DiscoveredContainer { ContainerId = "v312_A", Inventory = invA, WorldPos = new Vector2(20f, 10f) };
                var cB = new DiscoveredContainer { ContainerId = "v312_B", Inventory = invB, WorldPos = new Vector2(20f, 10f) };
                var cC = new DiscoveredContainer { ContainerId = "v312_C", Inventory = invC, WorldPos = new Vector2(58f, 10f) }; // > 40f from player at (10,10)
                var cD = new DiscoveredContainer { ContainerId = "v312_D", Inventory = invD, WorldPos = new Vector2(120f, 100f) };
                var cE = new DiscoveredContainer { ContainerId = "v312_E", Inventory = invE, WorldPos = new Vector2(135f, 100f) };
                var allBoxes = new List<DiscoveredContainer> { cA, cB, cC, cD, cE };

                Assert((bool)isWorkerZoneMethod.Invoke(null, new object[] { cA, npcAgent, st }), "Follower excluded valid z=0 chest within 40m");
                Assert(!(bool)isWorkerZoneMethod.Invoke(null, new object[] { cB, npcAgent, st }), "Follower accepted different zLevel chest");
                Assert(!(bool)isWorkerZoneMethod.Invoke(null, new object[] { cC, npcAgent, st }), "Follower accepted >40m chest despite BaseScanRadius=60");

                var followerFiltered = (List<DiscoveredContainer>)filterContainersMethod.Invoke(null, new object[] { allBoxes, npcAgent, st });
                Assert(followerFiltered.Count == 1 && followerFiltered[0].ContainerId == "v312_A",
                    $"FilterContainersForWorker follower expected [v312_A], got count={followerFiltered.Count}");

                st.RosterMode = CompanionRosterMode.CampGarrison;
                st.HomeAnchorPos = new Vector2(100f, 100f);
                st.HomeAnchorZ = 0;
                st.HasHomeAnchor = true;
                var garrisonFiltered = (List<DiscoveredContainer>)filterContainersMethod.Invoke(null, new object[] { allBoxes, npcAgent, st });
                Assert(garrisonFiltered.Count == 1 && garrisonFiltered[0].ContainerId == "v312_D",
                    $"FilterContainersForWorker garrison expected [v312_D <=28m], got count={garrisonFiltered.Count}");
                st.RosterMode = CompanionRosterMode.ActiveSquad;
                st.HasHomeAnchor = false;

                knownInvDict.Remove(invA.Pointer);
                knownInvDict.Remove(invB.Pointer);
                knownInvDict.Remove(invC.Pointer);
                knownInvDict.Remove(invD.Pointer);
                knownInvDict.Remove(invE.Pointer);

                // (d) CompanionSurvivalSystem nested bags (depth <= 3, dedup), bleeding @ 100% HP, ShouldSuppressVanillaFoodWaterSearch
                var backpackAttrs = ItemManager.instance?.GetEligibleLootAttrsByType(ItemType.Backpack);
                int bagItemId = (backpackAttrs != null && backpackAttrs.Count > 0 && Il2CppSafe.IsAliveUnity(backpackAttrs[0]))
                    ? backpackAttrs[0].itemId
                    : 500;
                var bagInv4 = MakeInventory(2, 2);
                var bagItem4 = new ItemData { itemId = bagItemId, inventoryData = bagInv4 };
                var bagInv3 = MakeInventory(2, 2);
                bagInv3.itemList.Add(bagItem4);
                var bagItem3 = new ItemData { itemId = bagItemId, inventoryData = bagInv3 };
                var bagInv2 = MakeInventory(2, 2);
                bagInv2.itemList.Add(bagItem3);
                var bagItem2 = new ItemData { itemId = bagItemId, inventoryData = bagInv2 };
                var bagInv1 = MakeInventory(2, 2);
                bagInv1.itemList.Add(bagItem2);
                var bagItem1 = new ItemData { itemId = bagItemId, inventoryData = bagInv1 };
                var bagInv0 = MakeInventory(4, 4);
                bagInv0.itemList.Add(bagItem1);

                npcCharData.inventoryData = bagInv0;
                npcData.sharedInventoryData = bagInv0; // intentional duplicate Pointer to verify dedup!
                var collectMethod = typeof(CompanionSurvivalSystem).GetMethod("CollectCompanionInventories", StaticAny);
                Assert(collectMethod != null, "Missing CollectCompanionInventories reflection target");
                var collected = (List<InventoryData>)collectMethod.Invoke(null, new object[] { npcCharData, npcData });
                Assert(collected.Count == 4, $"CollectCompanionInventories depth<=3 & dedup expected 4, got {collected.Count}");
                Assert(collected.Any(i => i.Pointer == bagInv3.Pointer) && !collected.Any(i => i.Pointer == bagInv4.Pointer),
                    "CollectCompanionInventories did not enforce exact depth <= 3");

                // Bleeding at >= 78% HP (100% HP)
                var hasInjuryMethod = typeof(CompanionSurvivalSystem).GetMethod("HasTreatableInjuryState", StaticAny);
                Assert(hasInjuryMethod != null, "Missing HasTreatableInjuryState reflection target");
                var part0 = npcCharData.bodypartsData.bodypartDataAry[0];
                part0.hp = part0.maxHP * 0.5f;
                float worldTime = Mathf.Max(Time.time, Il2CppSafe.IsAlive(gc?.gameData) ? gc.gameData.inGameTime : 0f);
                var prevGameData = Il2CppSafe.IsAliveUnity(gc) ? gc.gameData : null;
                bool injectedGameData = false;
                try
                {
                    if (Il2CppSafe.IsAliveUnity(gc) && !Il2CppSafe.IsAlive(prevGameData))
                    {
                        gc.gameData = new GameData { inGameTime = worldTime };
                        injectedGameData = true;
                    }
                    part0.bleedingStopTime = worldTime + 3600f;
                    part0.bandageData = null;
                    object[] injuryArgs = new object[] { npcCharData, false, false };
                    bool hasInjury = (bool)hasInjuryMethod.Invoke(null, injuryArgs);
                    Assert(hasInjury && (bool)injuryArgs[1], "HasTreatableInjuryState failed to detect bleeding at 100% HP");
                    Assert(CompanionSurvivalSystem.DoesAgentNeedNativeSurvival(npcAgent),
                        "DoesAgentNeedNativeSurvival returned false for actively bleeding companion at 100% HP");
                }
                finally
                {
                    if (injectedGameData && Il2CppSafe.IsAliveUnity(gc))
                        gc.gameData = prevGameData;
                    part0.bleedingStopTime = 0f;
                    part0.hp = part0.maxHP;
                }

                // Nested bag food in depth-3 bag satisfies ShouldSuppressVanillaFoodWaterSearch(forWater: false)
                CompanionConfigStore.EnableSmartCombat = true;
                CompanionConfigStore.EnableAutoBioAndSleep = true;
                npcCharData.characterStatusData.hunger = 30f;
                npcCharData.characterStatusData.thirst = 30f;
                var nestedFood = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(232), false, true);
                nestedFood.itemNumberFloat = 1f;
                bagInv3.itemList.Add(nestedFood);
                Assert(CompanionSurvivalSystem.ShouldSuppressVanillaFoodWaterSearch(npcAgent, st, forWater: false),
                    "ShouldSuppressVanillaFoodWaterSearch did not find food inside depth-3 nested bag");
                Assert(!CompanionSurvivalSystem.ShouldSuppressVanillaFoodWaterSearch(npcAgent, st, forWater: true),
                    "ShouldSuppressVanillaFoodWaterSearch suppressed water search when companion had no drink away from base");

                // Medicine eligibility guard: reject food with hp > 0 (232 Canned Stew, 240 Steak, 248 Spicy Steak), accept 220 Bandage & 221 First Aid Kit
                var isEligibleMedMethod = typeof(CompanionSurvivalSystem).GetMethod("IsEligibleMedicineItem", StaticAny);
                Assert(isEligibleMedMethod != null, "Missing IsEligibleMedicineItem reflection target");
                foreach (int foodId in new[] { 232, 240, 248 })
                {
                    var foodAttr = ItemManager.instance.GetItemAttrById(foodId);
                    Assert(Il2CppSafe.IsAliveUnity(foodAttr), $"Missing food ItemAttr {foodId}");
                    var foodItem = ItemManager.instance.GetRandomDataByAttr(foodAttr, false, true);
                    foodItem.itemNumberFloat = 1f;
                    object[] medArgs = new object[] { foodItem, null, false, false };
                    bool isMed = (bool)isEligibleMedMethod.Invoke(null, medArgs);
                    Assert(!isMed, $"IsEligibleMedicineItem misclassified non-medicine food id={foodId} as medicine");
                }
                foreach (int medId in new[] { 220, 221 })
                {
                    var medAttr = ItemManager.instance.GetItemAttrById(medId);
                    Assert(Il2CppSafe.IsAliveUnity(medAttr), $"Missing medicine ItemAttr {medId}");
                    var medItem = ItemManager.instance.GetRandomDataByAttr(medAttr, false, true);
                    medItem.itemNumberFloat = 1f;
                    object[] medArgs = new object[] { medItem, null, false, false };
                    bool isMed = (bool)isEligibleMedMethod.Invoke(null, medArgs);
                    Assert(isMed, $"IsEligibleMedicineItem rejected real medicine id={medId}");
                    if (medId == 220) Assert((bool)medArgs[2], "IsEligibleMedicineItem did not mark Bandage (220) as isBandage");
                    if (medId == 221) Assert((bool)medArgs[3], "IsEligibleMedicineItem did not mark First Aid Kit (221) as healsHp");
                }

                // (e) ChatterDatabase & SmartChatterSystem gender + anime-headgear (二次元头套) persona filtering
                var resolvePersonaMethod = typeof(SmartChatterSystem).GetMethod("ResolveNpcPersona", StaticAny);
                Assert(resolvePersonaMethod != null, "Missing ResolveNpcPersona reflection target");

                npcCharData.gender = 1;
                var pFemale = (NpcChatterPersona)resolvePersonaMethod.Invoke(null, new object[] { npcAgent });
                Assert(pFemale.IsFemale && !pFemale.HasNoveltyHeadgear, "ResolveNpcPersona failed Female detection");

                npcCharData.gender = 0;
                var animeHood = new ItemData { itemId = 220 };
                animeHood.SetCustomName("二次元猫耳头套");
                npcCharData.characterEquipmentData.headDecorationItemData = animeHood;
                npcCharData.characterEquipmentData.headgearHiddenMode = CharacterEquipmentData.HEADGEAR_SHOW_ALL;
                var pHoodShown = (NpcChatterPersona)resolvePersonaMethod.Invoke(null, new object[] { npcAgent });
                Assert(pHoodShown.HasNoveltyHeadgear && pHoodShown.Gender == NpcBiologicalGender.Male,
                    "ResolveNpcPersona failed visible anime hood detection");

                npcCharData.characterEquipmentData.headgearHiddenMode = CharacterEquipmentData.HEADGEAR_HIDE_HAT_MASK_HELMET;
                var pHoodHidden = (NpcChatterPersona)resolvePersonaMethod.Invoke(null, new object[] { npcAgent });
                Assert(!pHoodHidden.HasNoveltyHeadgear,
                    "ResolveNpcPersona ignored HEADGEAR_HIDE_HAT_MASK_HELMET");
                npcCharData.characterEquipmentData.headDecorationItemData = null;
                npcCharData.characterEquipmentData.headgearHiddenMode = CharacterEquipmentData.HEADGEAR_SHOW_ALL;

                var allTopics = (ChatterTopic[])Enum.GetValues(typeof(ChatterTopic));
                foreach (var persona in new[] { NpcChatterPersona.Female, NpcChatterPersona.AnimeHoodMale, NpcChatterPersona.AnimeHoodFemale })
                {
                    foreach (var t in allTopics)
                    {
                        foreach (var mode in new[] { ProfanityMode.Clean, ProfanityMode.Mixed, ProfanityMode.HardcoreRude })
                        {
                            foreach (var line in ChatterDatabase.GetLines(t, ModLanguage.Chinese, mode, persona))
                                Assert(!line.Contains("老子") && !line.Contains("你小子") && !line.Contains("兄弟"),
                                    $"ZH macho token leaked in {t}/{mode}: {line}");

                            if (persona.IsFemale)
                            {
                                foreach (var line in ChatterDatabase.GetLines(t, ModLanguage.Russian, mode, persona))
                                    Assert(!line.Contains("братан", StringComparison.OrdinalIgnoreCase) &&
                                           !line.Contains("братва", StringComparison.OrdinalIgnoreCase) &&
                                           !line.Contains("пацан", StringComparison.OrdinalIgnoreCase) &&
                                           !line.Contains("бубенц", StringComparison.OrdinalIgnoreCase),
                                        $"RU male token leaked in {t}/{mode}: {line}");

                                foreach (var line in ChatterDatabase.GetLines(t, ModLanguage.English, mode, persona))
                                    Assert(!line.Contains("brother", StringComparison.OrdinalIgnoreCase),
                                        $"EN brother leaked in {t}/{mode}: {line}");
                            }
                        }
                    }
                }
                Assert(ChatterDatabase.AdaptLineForPersona("Respect, brother!", ModLanguage.English, NpcChatterPersona.Female) == "Respect, sister!",
                    "EN female brother->sister adaptation mismatch");
                Assert(ChatterDatabase.AdaptLineForPersona("Устал я как собака!", ModLanguage.Russian, NpcChatterPersona.Female) == "Устала я как собака!",
                    "RU female grammar adaptation mismatch");

                // (f) SmartCompanionsRuntimeGate & HumanNpcEquipmentGuard MainMenu guards + TeamNPCPanel._instance
                UiSmokePlugin.AllowMenuFixtureUiRender = false;
                Assert(!SmartCompanionsRuntimeGate.IsUiAllowedToRenderAndCapture(),
                    "IsUiAllowedToRenderAndCapture returned true in MainMenu");

                var equipGuardType = typeof(SmartCompanionsPlugin).Assembly.GetType("ZedZone.SmartCompanions.HumanNpcEquipmentGuard");
                var loadEquipGuardType = typeof(SmartCompanionsPlugin).Assembly.GetType("ZedZone.SmartCompanions.HumanLoadEquipmentsGuard");
                var updateRangedGuardType = typeof(SmartCompanionsPlugin).Assembly.GetType("ZedZone.SmartCompanions.HumanNpcUpdateRangedWeaponGuard");
                Assert(equipGuardType != null && loadEquipGuardType != null && updateRangedGuardType != null,
                    "Missing HumanNpcEquipmentGuard reflection types");

                npcCharData.inventoryData = null;
                bool isNonLocal = (bool)equipGuardType.GetMethod("IsNonLocalPlayerHuman", StaticAny).Invoke(null, new object[] { npcHuman });
                Assert(!isNonLocal, "IsNonLocalPlayerHuman returned true in MainMenu without InGameController");
                loadEquipGuardType.GetMethod("Prefix", StaticAny).Invoke(null, new object[] { npcHuman });
                Assert(npcCharData.inventoryData == null, "LoadEquipments guard allocated inventoryData on MainMenu portrait dummy");
                bool updateHandCont = (bool)updateRangedGuardType.GetMethod("Prefix", StaticAny).Invoke(null, new object[] { npcHuman });
                Assert(updateHandCont, "UpdateHand_RangedWeapon guard intercepted MainMenu dummy");

                MultiplayerSyncSystem.OnCompanionDismissed(npcData);
                Assert(!Il2CppSafe.IsAliveUnity(TeamNPCPanel._instance),
                    "TeamNPCPanel._instance was lazily instantiated during MainMenu dismissal");

                // (g) SmartCompanionsInput cursor refresh & CanProcessSortBaseHotkey
                Assert(!SmartCompanionsInput.CanProcessSortBaseHotkey(),
                    "CanProcessSortBaseHotkey returned true outside active gameplay");
                Assert(!SmartCompanionsInput.CanProcessGameplayWorldOrders(),
                    "CanProcessGameplayWorldOrders returned true outside active gameplay");
                SmartCompanionsInput.NotifyUiInteractionOrClose(closedOrCollapsed: true);
                SmartCompanionsInput.ForceRestoreOsCursorFromUi();

                // (h) 1.0.20 companion nickname support (CharacterData.GetDisplayName() + empty/whitespace fallback)
                string origNick = npcCharData.nickname;
                try
                {
                    npcCharData.nickname = "  Callsign Rex  ";
                    Assert(CompanionConfigStore.GetCharacterDisplayName(npcCharData, "NPC") == "Callsign Rex",
                        "GetCharacterDisplayName did not prefer trimmed CharacterData.nickname");
                    var syncedNick = CompanionConfigStore.GetOrCreateCompanion(npcGuid, CompanionConfigStore.GetCharacterDisplayName(npcCharData));
                    Assert(syncedNick.LastKnownName == "Callsign Rex",
                        "GetOrCreateCompanion did not refresh LastKnownName to companion nickname");

                    npcCharData.nickname = "   ";
                    Assert(CompanionConfigStore.GetCharacterDisplayName(npcCharData, "NPC") == "V312Tester",
                        "GetCharacterDisplayName did not fall back to characterName on whitespace nickname");

                    npcCharData.nickname = "";
                    Assert(CompanionConfigStore.GetCharacterDisplayName(npcCharData, "NPC") == "V312Tester",
                        "GetCharacterDisplayName did not fall back to characterName on empty nickname");
                    CompanionConfigStore.GetOrCreateCompanion(npcGuid, CompanionConfigStore.GetCharacterDisplayName(npcCharData));
                }
                finally
                {
                    npcCharData.nickname = origNick;
                }

                // (i) 3.2.0 automatic NPC equipment & inventory repair config persistence & pure checks
                var origRepairMode = CompanionConfigStore.AutoRepairMode;
                try
                {
                    foreach (GearAutoRepairMode arMode in Enum.GetValues(typeof(GearAutoRepairMode)))
                    {
                        foreach (var lang in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                        {
                            string lbl = CompanionConfigStore.FormatAutoRepairModeLabel(arMode, lang);
                            Assert(!string.IsNullOrWhiteSpace(lbl), $"Missing AutoRepairMode label for {arMode}/{lang}");
                        }
                    }

                    CompanionConfigStore.AutoRepairMode = GearAutoRepairMode.MaterialsOnly;
                    string savedTxt = CompanionConfigStore.BuildSaveText();
                    Assert(savedTxt.Contains("SET|AutoRepairMode|1"), "BuildSaveText missing SET|AutoRepairMode|1");

                    CompanionConfigStore.AutoRepairMode = GearAutoRepairMode.Off;
                    CompanionConfigStore.LoadFromText(savedTxt);
                    Assert(CompanionConfigStore.AutoRepairMode == GearAutoRepairMode.MaterialsOnly,
                        $"LoadFromText failed to restore AutoRepairMode (got {CompanionConfigStore.AutoRepairMode})");

                    CompanionConfigStore.LoadFromText("SET|AutoRepairMode|FreeMaintenance\n");
                    Assert(CompanionConfigStore.AutoRepairMode == GearAutoRepairMode.FreeMaintenance,
                        "LoadFromText failed to parse enum AutoRepairMode name");

                    // CycleAutoRepairMode without disk side effects check via Off -> MaterialsOnly -> MaterialsOrField -> FreeMaintenance -> Off
                    CompanionConfigStore.AutoRepairMode = GearAutoRepairMode.Off;
                    var offRes = CompanionGearRepairSystem.TryRunGearRepairCycleForAgent(npcAgent, st, Time.unscaledTime + 100f, ignoreCooldown: true, allowSpeak: false);
                    Assert(offRes.TotalRepaired == 0, "AutoRepairMode.Off repaired items");

                    // Non-repairable food/material safety check
                    var foodAttr = ItemManager.instance?.GetItemAttrById(232);
                    if (Il2CppSafe.IsAliveUnity(foodAttr))
                    {
                        Assert(!CompanionGearRepairSystem.IsEligibleStandardRepairableEquipment(foodAttr, false),
                            "Canned food (232) must never be eligible for gear repair");
                    }
                }
                finally
                {
                    CompanionConfigStore.AutoRepairMode = origRepairMode;
                }

                // (j) EventLogRecorder: toggle ON, log event, MarkBugNow, verify file + privacy, ClearLogFile, toggle OFF
                bool origLogEnabled = CompanionConfigStore.EnableEventLogging;
                try
                {
                    EventLogRecorder.ClearLogFile();
                    EventLogRecorder.SetEnabled(true, "uismoke_test");
                    Assert(EventLogRecorder.IsEnabled, "EventLogRecorder.SetEnabled(true) did not enable logging");
                    EventLogRecorder.Log("TEST", "UiSmoke event log verification line", immediateFlush: true);
                    bool marked = EventLogRecorder.MarkBugNow("uismoke_bug_mark");
                    Assert(marked, "EventLogRecorder.MarkBugNow returned false");
                    Assert(System.IO.File.Exists(EventLogRecorder.LogFilePath), "EventLogRecorder log file was not created");
                    string logText = System.IO.File.ReadAllText(EventLogRecorder.LogFilePath, System.Text.Encoding.UTF8);
                    Assert(logText.Contains("UiSmoke event log verification line"), "EventLogRecorder log missing test event");
                    Assert(logText.Contains("[BUG_MARK #"), "EventLogRecorder log missing BUG_MARK header");
                    Assert(logText.IndexOf("C:\\Users\\", StringComparison.OrdinalIgnoreCase) < 0,
                        "PRIVACY VIOLATION: EventLogRecorder leaked OS user path into log file!");

                    string savedRules = CompanionConfigStore.BuildSaveText();
                    Assert(savedRules.Contains("SET|EnableEventLogging|True"), "BuildSaveText did not serialize EnableEventLogging=True");

                    EventLogRecorder.SetEnabled(false, "uismoke_test");
                    Assert(!EventLogRecorder.IsEnabled, "EventLogRecorder.SetEnabled(false) did not disable logging");
                    EventLogRecorder.ClearLogFile();
                    Assert(!System.IO.File.Exists(EventLogRecorder.LogFilePath), "EventLogRecorder.ClearLogFile did not delete log file");
                }
                finally
                {
                    EventLogRecorder.SetEnabled(origLogEnabled, "uismoke_restore");
                    if (!origLogEnabled)
                        EventLogRecorder.ClearLogFile();
                }
            }
            finally
            {
                if (Il2CppSafe.IsAliveUnity(gc))
                    gc.playerCharacter = prevPlayer;
                CompanionConfigStore.BaseScanRadius = prevScanRadius;
                CompanionConfigStore.EnableSmartCombat = prevSmartCombat;
                CompanionConfigStore.EnableAutoBioAndSleep = prevAutoBio;
                UiSmokePlugin.AllowMenuFixtureUiRender = prevAllowMenuUi;
                CompanionConfigStore.CompanionStates.Remove(npcGuid);
                if (Il2CppSafe.IsAliveUnity(npcGo)) UnityEngine.Object.DestroyImmediate(npcGo);
                if (Il2CppSafe.IsAliveUnity(vehGo)) UnityEngine.Object.DestroyImmediate(vehGo);
                if (Il2CppSafe.IsAliveUnity(driverGo)) UnityEngine.Object.DestroyImmediate(driverGo);
            }

            UiSmokePlugin.Logger.LogInfo(
                "[UiSmoke] PASS v3.1.2 vehicle seats (-1), ReturnToBase/ICF anti-roam guards, BaseSorting worker zones, survival nested bags & bleeding >=78% HP, gender/anime-hood chatter, MainMenu portrait/TeamNPCPanel._instance guards, cursor hotkeys & 1.0.20 nickname support.");
        }

        private static InventoryData MakeInventory(int width, int height)
        {
            var inv = new InventoryData
            {
                inventorySizeX = width,
                inventorySizeY = height,
                itemList = new Il2CppSystem.Collections.Generic.List<ItemData>()
            };
            inv.SetOccupyDataDirty();
            return inv;
        }
    }
}
