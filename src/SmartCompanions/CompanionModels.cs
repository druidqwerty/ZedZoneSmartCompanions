using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public enum TacticalDoctrine
    {
        Balanced = 0,
        Assault = 1,
        Bodyguard = 2,
        SniperKite = 3,
        StealthMelee = 4,
        Passive = 5
    }

    [Flags]
    public enum StorageCategory
    {
        None             = 0,
        MeleeWeapons     = 1 << 0,
        RangedWeapons    = 1 << 1,
        AmmoAndMags      = 1 << 2,
        Medicine         = 1 << 3,
        FreshFood        = 1 << 4,
        CannedAndDrinks  = 1 << 5,
        Materials        = 1 << 6,
        ToolsAndRepair   = 1 << 7,
        ClothingAndArmor = 1 << 8,
        VehicleAndPower  = 1 << 9,
        FarmingAndPets   = 1 << 10,
        MiscAndValuables = 1 << 11,
        All              = (1 << 12) - 1
    }

    public enum FoodKind
    {
        None = 0,
        SteakAndRoastMeat = 1,
        RawMeat = 2,
        RottenFood = 3,
        CannedFood = 4,
        FishDish = 5,
        CookedMeal = 6,
        SweetAndSnack = 7,
        DrinkAndSoda = 8,
        Alcohol = 9,
        MedicineBandage = 10,
        MedicinePills = 11,
        FruitAndVeg = 12,
        OtherFood = 13
    }

    public enum ChatterTopic
    {
        PlayerEatSteakMeat,
        PlayerEatRawOrBad,
        PlayerEatCanned,
        PlayerEatFish,
        PlayerEatCookedMeal,
        PlayerEatSnackOrFruit,
        PlayerDrinkWaterSoda,
        PlayerDrinkAlcohol,
        PlayerHealBandage,
        PlayerReloadAmmo,
        PlayerRepairOrCraft,
        PlayerCookingBBQ,
        PlayerLockpickBreakDoor,
        CombatSpotZombie,
        CombatSpotHorde,
        CombatSpotBandit,
        CombatSpotBoss,
        CombatKiting,
        CombatClearFireLine,
        CombatSwitchMelee,
        CombatSwitchRanged,
        CombatKillEnemy,
        CombatSavePlayer,
        CombatLowAmmo,
        CombatWounded,
        FieldMedicAid,
        FieldAmmoShare,
        SortStart,
        SortPickUp,
        SortDepositWeapons,
        SortDepositFood,
        SortDepositMeds,
        SortDepositMaterials,
        SortFinished,
        LootFoundGood,
        AmbientRain,
        AmbientBloodRain,
        AmbientNight,
        AmbientMorning,
        AmbientHungry,
        AmbientTired,
        AmbientBaseIdle,
        BanterQuestion,
        BanterReply
    }

    public class ContainerRuleProfile
    {
        public string ContainerId = "";
        public string CustomName = "";
        public StorageCategory Categories = StorageCategory.None;
        public bool IsDropoffSource = false;
        public int Priority = 3;
        public bool AutoCompact = true;
        public Vector2 LastKnownPos;
    }

    public class CompanionRuntimeState
    {
        public string NpcGuid = "";
        public string LastKnownName = "";
        public TacticalDoctrine Doctrine = TacticalDoctrine.Balanced;
        public bool AllowAutoSortBase = true;
        public bool AllowAutoLootNearby = false;
        public bool AllowFieldMedic = true;
        public bool AllowAmmoShare = true;

        public string CurrentTaskText = "";
        public int KillsThisSession = 0;
        public int SortedItemsThisSession = 0;
        public int LootedItemsThisSession = 0;

        public float LastChatterTime = -999f;
        public float LastCombatCalloutTime = -999f;
        public float LastKiteStepTime = -999f;
        public float LastFireLineStepTime = -999f;
        public float LastWeaponSwitchTime = -999f;
        public float LastFieldMedicTime = -999f;
        public float LastAmmoShareTime = -999f;
        public float LastAutoSortTickTime = -999f;
        public float LastAutoLootTickTime = -999f;

        // Active physical sorting state
        public bool IsSortingActive = false;
        public int SortingPhase = 0; // 0=Idle, 1=MovingToSource, 2=MovingToTarget
        public string TargetContainerGuid = "";
        public Vector2 TargetContainerPos;
        public float SortingStateStartTime = 0f;
        public int PendingTransferCount = 0;
    }

    public static class CompanionConfigStore
    {
        public static readonly Dictionary<string, ContainerRuleProfile> ContainerRules =
            new Dictionary<string, ContainerRuleProfile>(StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, CompanionRuntimeState> CompanionStates =
            new Dictionary<string, CompanionRuntimeState>(StringComparer.OrdinalIgnoreCase);

        public static bool UseRussian = true;
        public static bool EnableChatter = true;
        public static float ChatterIntervalMultiplier = 1.0f;
        public static bool EnableSmartCombat = true;
        public static bool EnableAntiFriendlyFire = true;
        public static bool EnableKiting = true;
        public static bool EnableFieldMedic = true;
        public static bool EnableAmmoShare = true;
        public static bool ShowHudBadge = true;
        public static float BaseScanRadius = 48f;

        private static string SaveFilePath =>
            Path.Combine(Paths.ConfigPath, "SmartCompanions_Rules.txt");

        public static ContainerRuleProfile GetOrCreateRule(string id, string defaultName = "")
        {
            if (string.IsNullOrEmpty(id)) id = "unknown";
            if (!ContainerRules.TryGetValue(id, out var rule))
            {
                rule = new ContainerRuleProfile
                {
                    ContainerId = id,
                    CustomName = defaultName ?? ""
                };
                ContainerRules[id] = rule;
            }
            else if (string.IsNullOrEmpty(rule.CustomName) && !string.IsNullOrEmpty(defaultName))
            {
                rule.CustomName = defaultName;
            }
            return rule;
        }

        public static CompanionRuntimeState GetOrCreateCompanion(string guid, string name = "")
        {
            if (string.IsNullOrEmpty(guid)) guid = "default";
            if (!CompanionStates.TryGetValue(guid, out var st))
            {
                st = new CompanionRuntimeState
                {
                    NpcGuid = guid,
                    LastKnownName = name ?? ""
                };
                CompanionStates[guid] = st;
            }
            else if (!string.IsNullOrEmpty(name))
            {
                st.LastKnownName = name;
            }
            return st;
        }

        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ZED ZONE Smart Companions Saved Rules v1.0");
                sb.AppendLine($"SET|UseRussian|{UseRussian}");
                sb.AppendLine($"SET|EnableChatter|{EnableChatter}");
                sb.AppendLine($"SET|ChatterIntervalMultiplier|{ChatterIntervalMultiplier:F2}");
                sb.AppendLine($"SET|EnableSmartCombat|{EnableSmartCombat}");
                sb.AppendLine($"SET|EnableAntiFriendlyFire|{EnableAntiFriendlyFire}");
                sb.AppendLine($"SET|EnableKiting|{EnableKiting}");
                sb.AppendLine($"SET|EnableFieldMedic|{EnableFieldMedic}");
                sb.AppendLine($"SET|EnableAmmoShare|{EnableAmmoShare}");
                sb.AppendLine($"SET|ShowHudBadge|{ShowHudBadge}");
                sb.AppendLine($"SET|BaseScanRadius|{BaseScanRadius:F1}");

                foreach (var kv in ContainerRules)
                {
                    var r = kv.Value;
                    if (r.Categories == StorageCategory.None && !r.IsDropoffSource && string.IsNullOrEmpty(r.CustomName))
                        continue;
                    var safeName = (r.CustomName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    sb.AppendLine($"BOX|{r.ContainerId}|{(int)r.Categories}|{r.IsDropoffSource}|{r.Priority}|{r.AutoCompact}|{safeName}");
                }

                foreach (var kv in CompanionStates)
                {
                    var c = kv.Value;
                    var safeName = (c.LastKnownName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    sb.AppendLine($"NPC|{c.NpcGuid}|{(int)c.Doctrine}|{c.AllowAutoSortBase}|{c.AllowAutoLootNearby}|{c.AllowFieldMedic}|{c.AllowAmmoShare}|{safeName}");
                }

                File.WriteAllText(SaveFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("Save rules failed: " + e.Message);
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SaveFilePath)) return;
                foreach (var raw in File.ReadAllLines(SaveFilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;

                    if (parts[0] == "SET")
                    {
                        var key = parts[1];
                        var val = parts[2];
                        if (key == "UseRussian" && bool.TryParse(val, out var b0)) UseRussian = b0;
                        else if (key == "EnableChatter" && bool.TryParse(val, out var b1)) EnableChatter = b1;
                        else if (key == "ChatterIntervalMultiplier" && float.TryParse(val, out var f1)) ChatterIntervalMultiplier = Mathf.Clamp(f1, 0.25f, 4f);
                        else if (key == "EnableSmartCombat" && bool.TryParse(val, out var b2)) EnableSmartCombat = b2;
                        else if (key == "EnableAntiFriendlyFire" && bool.TryParse(val, out var b3)) EnableAntiFriendlyFire = b3;
                        else if (key == "EnableKiting" && bool.TryParse(val, out var b4)) EnableKiting = b4;
                        else if (key == "EnableFieldMedic" && bool.TryParse(val, out var b5)) EnableFieldMedic = b5;
                        else if (key == "EnableAmmoShare" && bool.TryParse(val, out var b6)) EnableAmmoShare = b6;
                        else if (key == "ShowHudBadge" && bool.TryParse(val, out var b7)) ShowHudBadge = b7;
                        else if (key == "BaseScanRadius" && float.TryParse(val, out var f2)) BaseScanRadius = Mathf.Clamp(f2, 15f, 120f);
                    }
                    else if (parts[0] == "BOX" && parts.Length >= 6)
                    {
                        var id = parts[1];
                        int.TryParse(parts[2], out var catInt);
                        bool.TryParse(parts[3], out var drop);
                        int.TryParse(parts[4], out var prio);
                        bool.TryParse(parts[5], out var compact);
                        var name = parts.Length >= 7 ? parts[6] : "";
                        ContainerRules[id] = new ContainerRuleProfile
                        {
                            ContainerId = id,
                            Categories = (StorageCategory)catInt,
                            IsDropoffSource = drop,
                            Priority = Mathf.Clamp(prio, 1, 5),
                            AutoCompact = compact,
                            CustomName = name
                        };
                    }
                    else if (parts[0] == "NPC" && parts.Length >= 7)
                    {
                        var guid = parts[1];
                        int.TryParse(parts[2], out var docInt);
                        bool.TryParse(parts[3], out var sort);
                        bool.TryParse(parts[4], out var loot);
                        bool.TryParse(parts[5], out var med);
                        bool.TryParse(parts[6], out var ammo);
                        var name = parts.Length >= 8 ? parts[7] : "";
                        CompanionStates[guid] = new CompanionRuntimeState
                        {
                            NpcGuid = guid,
                            Doctrine = (TacticalDoctrine)Mathf.Clamp(docInt, 0, 5),
                            AllowAutoSortBase = sort,
                            AllowAutoLootNearby = loot,
                            AllowFieldMedic = med,
                            AllowAmmoShare = ammo,
                            LastKnownName = name
                        };
                    }
                }
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("Load rules failed: " + e.Message);
            }
        }
    }
}
