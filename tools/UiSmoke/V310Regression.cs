using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    // 3.1.0: deterministic checks for Fireteam channels & MP sync, cross-inventory reserve subtraction,
    // hybrid [Drop-off + Category] fallback ordering, Fighter auto-dropoff, VehicleCombatCallout lines, and Quick Guide state.
    internal static class V310Regression
    {
        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new Exception("V310Regression failed: " + msg);
        }

        public static void Run()
        {
            const string guidA = "sc_v310_alpha";
            const string guidB = "sc_v310_bravo";
            const string guidG = "sc_v310_garrison";
            var prevChannel = CompanionConfigStore.ActiveCommandGroup;
            bool prevGuide = CompanionConfigStore.ShowQuickGuide;
            bool prevHarvest = CompanionConfigStore.AutoHarvestCrops;
            bool prevIndustry = CompanionConfigStore.EnableBaseIndustry;
            bool prevPowerGrid = BaseIndustrySystem.EnablePowerGridAutomation;
            bool prevFurnace = BaseIndustrySystem.EnableFurnaceAndKitchenAutomation;

            try
            {
                // 1. SquadFireteam & MatchesActiveCommandChannel routing
                var stA = CompanionConfigStore.GetOrCreateCompanion(guidA, "AlphaOne");
                var stB = CompanionConfigStore.GetOrCreateCompanion(guidB, "BravoTwo");
                var stG = CompanionConfigStore.GetOrCreateCompanion(guidG, "GarrisonThree");
                stA.RosterMode = CompanionRosterMode.ActiveSquad;
                stA.Fireteam = SquadFireteam.Alpha;
                stB.RosterMode = CompanionRosterMode.ActiveSquad;
                stB.Fireteam = SquadFireteam.Bravo;
                stG.RosterMode = CompanionRosterMode.CampGarrison;
                stG.Fireteam = SquadFireteam.Alpha;

                CompanionConfigStore.ActiveCommandGroup = SquadFireteam.None;
                Assert(CompanionConfigStore.MatchesActiveCommandChannel(stA), "All channel excluded Alpha");
                Assert(CompanionConfigStore.MatchesActiveCommandChannel(stB), "All channel excluded Bravo");
                Assert(!CompanionConfigStore.MatchesActiveCommandChannel(stG), "All channel failed to exclude CampGarrison");

                CompanionConfigStore.ActiveCommandGroup = SquadFireteam.Alpha;
                Assert(CompanionConfigStore.MatchesActiveCommandChannel(stA), "Alpha channel excluded Alpha");
                Assert(!CompanionConfigStore.MatchesActiveCommandChannel(stB), "Alpha channel included Bravo");
                Assert(!CompanionConfigStore.MatchesActiveCommandChannel(stG), "Alpha channel failed to exclude CampGarrison");

                CompanionConfigStore.ActiveCommandGroup = SquadFireteam.Bravo;
                Assert(!CompanionConfigStore.MatchesActiveCommandChannel(stA), "Bravo channel included Alpha");
                Assert(CompanionConfigStore.MatchesActiveCommandChannel(stB), "Bravo channel excluded Bravo");

                // 2. Persistence & MultiplayerSyncSystem packet roundtrip for Fireteam + AutoHarvestCrops
                stA.OwnerCharacterId = "owner_1";
                stA.OwnerPlayerName = "Leader";
                stA.StateRevision = 10;
                CompanionConfigStore.AutoHarvestCrops = false;
                string saved = CompanionConfigStore.BuildSaveText();
                CompanionConfigStore.AutoHarvestCrops = true;
                CompanionConfigStore.CompanionStates.Remove(guidA);
                CompanionConfigStore.LoadFromText(saved);
                Assert(!CompanionConfigStore.AutoHarvestCrops, "AutoHarvestCrops save/load roundtrip failed");
                Assert(CompanionConfigStore.CompanionStates.TryGetValue(guidA, out stA), "stA missing after LoadFromText");
                Assert(stA.Fireteam == SquadFireteam.Alpha, "Fireteam Save/Load roundtrip failed");

                var ftPacket = new ZedZone.Net.MsgChat
                {
                    senderId = 1,
                    senderName = "Leader",
                    text = MultiplayerSyncSystem.PacketPrefix +
                           $"NPC|{guidA}|0|owner_1|Leader|11|owner_1|0|0|0|0|0|1|0|0|0|24|2|1|0|0|0|0|2"
                };
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(ftPacket, null, receivedOnHost: false);
                Assert(stA.Fireteam == SquadFireteam.Bravo, "NPC packet Fireteam=Bravo sync failed");

                // Legacy 23-field NPC packet without Fireteam field must default to SquadFireteam.None
                var legacyPacket = new ZedZone.Net.MsgChat
                {
                    senderId = 1,
                    senderName = "Leader",
                    text = MultiplayerSyncSystem.PacketPrefix +
                           $"NPC|{guidA}|0|owner_1|Leader|12|owner_1|0|0|0|0|0|1|0|0|0|24|2|1|0|0|0|0"
                };
                MultiplayerSyncSystem.TryHandleIncomingChatPacket(legacyPacket, null, receivedOnHost: false);
                Assert(stA.Fireteam == SquadFireteam.None,
                    "Legacy 23-field NPC packet did not default Fireteam to SquadFireteam.None");

                // 3. Cross-inventory reserve subtraction & equipped gear protection in BuildProtectedBackpackPointers
                var go = new GameObject("SC_V310_BackpackTest");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.SetActive(false);
                try
                {
                    var ag = go.AddComponent<GOAPAgent>();
                    var hc = go.AddComponent<HumanCharacterController>();
                    var personalInv = MakeInventory(8, 8);
                    var sharedInv = MakeInventory(8, 8);
                    var cd = new CharacterData
                    {
                        inventoryData = personalInv,
                        characterEquipmentData = new CharacterEquipmentData()
                    };
                    var npcData = new GOAPNPCData
                    {
                        npcGUID = guidA,
                        characterData = cd,
                        sharedInventoryData = sharedInv
                    };
                    ag.npcData = npcData;
                    ag.humanCharacter = hc;

                    var personalFood1 = MakeItem(232, 1f); // 2 cans in personal inventory satisfy ReserveFoodMin=2 regardless of stack size
                    var personalFood2 = MakeItem(232, 1f);
                    var equippedArmor = MakeItem(220, 1f);
                    cd.characterEquipmentData.torseItemData = equippedArmor;
                    AddFixture(personalInv, personalFood1);
                    AddFixture(personalInv, personalFood2);
                    AddFixture(personalInv, equippedArmor);

                    var sharedFood1 = MakeItem(232, 1f);
                    var sharedFood2 = MakeItem(232, 1f);
                    AddFixture(sharedInv, sharedFood1);
                    AddFixture(sharedInv, sharedFood2);

                    stA.ReserveAmmoMin = 0;
                    stA.ReserveFoodMin = 2;
                    stA.ReserveMedMin = 0;

                    var sharedProt = CompanionProfileSystem.BuildProtectedBackpackPointers(ag, stA, sharedInv);
                    Assert(!sharedProt.Contains(sharedFood1.Pointer) && !sharedProt.Contains(sharedFood2.Pointer),
                        "sharedInventoryData food stayed blocked even though personalInv already satisfied ReserveFoodMin");

                    var personalProt = CompanionProfileSystem.BuildProtectedBackpackPointers(ag, stA, personalInv);
                    Assert(personalProt.Contains(equippedArmor.Pointer), "personalInv equipped armor was not protected");
                    Assert(personalProt.Contains(personalFood1.Pointer) && personalProt.Contains(personalFood2.Pointer),
                        "personalInv food reserve was not protected");
                }
                finally
                {
                    UnityEngine.Object.Destroy(go);
                }

                // 4. GetAllDropoffFallbackBoxes orders pure [Drop-off] first, then hybrid [Drop-off + Category]
                var hybridBox = new DiscoveredContainer
                {
                    ContainerId = "v310_hybrid",
                    Inventory = MakeInventory(4, 4),
                    DistanceToPlayer = 1.0f,
                    Rule = new ContainerRuleProfile
                    {
                        ContainerId = "v310_hybrid",
                        IsMarkedByPlayer = true,
                        IsDropoffSource = true,
                        Categories = StorageCategory.Medicine,
                        Priority = 5
                    }
                };
                var pureBox = new DiscoveredContainer
                {
                    ContainerId = "v310_pure",
                    Inventory = MakeInventory(4, 4),
                    DistanceToPlayer = 4.0f,
                    Rule = new ContainerRuleProfile
                    {
                        ContainerId = "v310_pure",
                        IsMarkedByPlayer = true,
                        IsDropoffSource = true,
                        Categories = StorageCategory.None,
                        Priority = 1
                    }
                };
                var catOnlyBox = new DiscoveredContainer
                {
                    ContainerId = "v310_cat",
                    Inventory = MakeInventory(4, 4),
                    DistanceToPlayer = 2.0f,
                    Rule = new ContainerRuleProfile
                    {
                        ContainerId = "v310_cat",
                        IsMarkedByPlayer = true,
                        IsDropoffSource = false,
                        Categories = StorageCategory.FreshFood,
                        Priority = 5
                    }
                };
                var dropList = BaseSortingSystem.GetAllDropoffFallbackBoxes(
                    new List<DiscoveredContainer> { hybridBox, catOnlyBox, pureBox });
                Assert(dropList.Count == 2 && dropList[0].ContainerId == "v310_pure" && dropList[1].ContainerId == "v310_hybrid",
                    "GetAllDropoffFallbackBoxes ordering/inclusion failed");

                // 5. BaseWorkManager.AllowsAutoDropoff allows Fighter preset (WorkRole.Off + AllowAutoDropoffAtBase=true)
                var fighterSt = new CompanionRuntimeState { NpcGuid = "v310_fighter" };
                CompanionProfileSystem.ApplyPresetToCompanion(null, fighterSt, CompanionSpecialization.Fighter, sync: false);
                Assert(fighterSt.WorkRole == CompanionWorkRole.Off && fighterSt.AllowAutoDropoffAtBase,
                    "Fighter preset unexpected WorkRole/AutoDropoff");
                Assert(BaseWorkManager.AllowsAutoDropoff(fighterSt),
                    "AllowsAutoDropoff blocked Fighter preset when AllowAutoDropoffAtBase=true");
                fighterSt.AllowAutoDropoffAtBase = false;
                Assert(!BaseWorkManager.AllowsAutoDropoff(fighterSt),
                    "AllowsAutoDropoff did not respect AllowAutoDropoffAtBase=false");

                // 6. VehicleCombatCallout chatter across all 3 languages & profanity modes
                foreach (ModLanguage lang in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                {
                    foreach (ProfanityMode mode in new[] { ProfanityMode.HardcoreRude, ProfanityMode.Clean, ProfanityMode.Mixed })
                    {
                        var lines = ChatterDatabase.GetLines(ChatterTopic.VehicleCombatCallout, lang, mode);
                        Assert(lines != null && lines.Length >= 4,
                            $"VehicleCombatCallout missing lines for {lang}/{mode}");
                    }
                }

                // 7. Quick Guide toggle, LoadFromText(EnableBaseIndustry=false) sync & reset defaults check
                CompanionConfigStore.ShowQuickGuide = true;
                Assert(CompanionConfigStore.ShowQuickGuide, "ShowQuickGuide toggle failed");
                CompanionConfigStore.EnableBaseIndustry = true;
                BaseIndustrySystem.EnablePowerGridAutomation = true;
                BaseIndustrySystem.EnableFurnaceAndKitchenAutomation = true;
                CompanionConfigStore.LoadFromText("SET|EnableBaseIndustry|false");
                Assert(!CompanionConfigStore.EnableBaseIndustry &&
                       !BaseIndustrySystem.EnablePowerGridAutomation &&
                       !BaseIndustrySystem.EnableFurnaceAndKitchenAutomation,
                    "LoadFromText(EnableBaseIndustry=false) did not sync BaseIndustrySystem flags");

                CompanionConfigStore.ResetAllSettingsToDefault(preserveContainerRules: true);
                Assert(CompanionConfigStore.ActiveCommandGroup == SquadFireteam.None &&
                       CompanionConfigStore.AutoHarvestCrops &&
                       !CompanionConfigStore.ShowQuickGuide &&
                       CompanionConfigStore.EnableBaseIndustry &&
                       BaseIndustrySystem.EnablePowerGridAutomation &&
                       BaseIndustrySystem.EnableFurnaceAndKitchenAutomation,
                    "ResetAllSettingsToDefault did not reset v3.1.0 fields and sync BaseIndustrySystem");

                // 8. Zero-cell chest partial-stack merge batch cap (1 spare unit in dest => only 1 of 3 source items picked)
                int stackItemId = 0;
                ItemAttr stackAttr = null;
                for (int cid = 1; cid <= 2000; cid++)
                {
                    var candAttr = ItemManager.instance.GetItemAttrById(cid);
                    if (candAttr == null || candAttr.stackNumber < 3) continue;
                    var p1 = MakeItem(cid, 1f);
                    var p2 = MakeItem(cid, 1f);
                    if (p1 == null || p2 == null) continue;
                    if (!InventoryData.CanMergeStack(p1, p2)) continue;
                    if (ItemCategorizer.ClassifyItem(p1) == StorageCategory.None) continue;
                    stackItemId = cid;
                    stackAttr = candAttr;
                    break;
                }
                Assert(stackAttr != null && stackItemId > 0, "Missing stackable fixture item with stackNumber >= 3");
                int sw = Math.Max(1, stackAttr.itemSize.x);
                int sh = Math.Max(1, stackAttr.itemSize.y);
                var zeroCellDestInv = MakeInventory(sw, sh);
                var destSeed = MakeItem(stackItemId, stackAttr.stackNumber - 1);
                AddFixture(zeroCellDestInv, destSeed);
                Assert(zeroCellDestInv.CountFreeCells() == 0, "Zero-cell destination chest unexpectedly had free cells");

                var batchSrcInv = MakeInventory(sw * 4, sh * 2);
                var srcItem1 = destSeed.CopyItemData(null); srcItem1.itemNumberFloat = 1f;
                var srcItem2 = destSeed.CopyItemData(null); srcItem2.itemNumberFloat = 1f;
                var srcItem3 = destSeed.CopyItemData(null); srcItem3.itemNumberFloat = 1f;
                AddFixture(batchSrcInv, srcItem1);
                AddFixture(batchSrcInv, srcItem2);
                AddFixture(batchSrcInv, srcItem3);
                Assert(batchSrcInv.itemList.Count == 3, "Expected 3 separate 1-unit stacks in source inventory");

                StorageCategory batchCat = ItemCategorizer.ClassifyItem(srcItem1);
                var zeroCellDest = new DiscoveredContainer
                {
                    ContainerId = "v310_zero_cell_dest",
                    DisplayName = "ZeroCellChest",
                    Inventory = zeroCellDestInv,
                    DistanceToPlayer = 1.5f,
                    Rule = new ContainerRuleProfile
                    {
                        ContainerId = "v310_zero_cell_dest",
                        IsMarkedByPlayer = true,
                        IsDropoffSource = false,
                        Categories = batchCat,
                        Priority = 5
                    }
                };
                var batchSt = new CompanionRuntimeState
                {
                    NpcGuid = "sc_v310_batch_cap",
                    SourceContainerInv = batchSrcInv,
                    SourceIsOwnBackpack = false,
                    SourceAllowDropoffFallback = false,
                    SourceAllowedStayMask = StorageCategory.None
                };
                var pickupMethod = typeof(BaseSortingSystem).GetMethod(
                    "PickupBatchIntoCarryBuffer",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                Assert(pickupMethod != null, "BaseSortingSystem.PickupBatchIntoCarryBuffer method not found");
                int pickedCount = (int)pickupMethod.Invoke(null, new object[]
                {
                    null,
                    batchSt,
                    new List<DiscoveredContainer> { zeroCellDest },
                    null
                });
                Assert(pickedCount == 1 && batchSt.CarryBuffer.Count == 1,
                    $"Zero-cell partial-stack batch over-picked: pickedCount={pickedCount}, carry={batchSt.CarryBuffer.Count}");
                Assert(batchSrcInv.itemList.Count == 2,
                    $"Expected 2 unpicked items to remain in source inventory, got {batchSrcInv.itemList.Count}");
            }
            finally
            {
                CompanionConfigStore.ActiveCommandGroup = prevChannel;
                CompanionConfigStore.ShowQuickGuide = prevGuide;
                CompanionConfigStore.AutoHarvestCrops = prevHarvest;
                CompanionConfigStore.EnableBaseIndustry = prevIndustry;
                BaseIndustrySystem.EnablePowerGridAutomation = prevPowerGrid;
                BaseIndustrySystem.EnableFurnaceAndKitchenAutomation = prevFurnace;
                CompanionConfigStore.CompanionStates.Remove(guidA);
                CompanionConfigStore.CompanionStates.Remove(guidB);
                CompanionConfigStore.CompanionStates.Remove(guidG);
            }

            UiSmokePlugin.Logger.LogInfo("[UiSmoke] PASS v3.1.0 Fireteams, legacy 23-field MP sync, zero-cell partial-stack batch cap, industry config sync, dual-inventory reserve subtraction, hybrid drop-off, Fighter auto-dropoff, VehicleCombatCallout & QuickGuide.");
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

        private static ItemData MakeItem(int id, float quantity)
        {
            var item = ItemManager.instance.GetRandomDataByAttr(ItemManager.instance.GetItemAttrById(id), false, true);
            item.itemNumberFloat = quantity;
            return item;
        }

        private static void AddFixture(InventoryData inv, ItemData item)
        {
            if (!inv.TryAddItem(item, out bool partial, false, false) || partial)
                throw new Exception("Could not seed V310Regression inventory fixture.");
        }
    }
}
