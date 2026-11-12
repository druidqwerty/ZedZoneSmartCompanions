using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using Il2CppInterop.Runtime.InteropTypes;
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

    public enum ProfanityMode
    {
        HardcoreRude = 0, // Постапокалипсис: Быдло и хамы (Мат 18+) — по умолчанию
        Mixed = 1,        // Смешанный (Мат + Обычные)
        Clean = 2         // Без мата (Цензура / Стример)
    }

    public enum ModLanguage
    {
        Auto = 0,    // Автоопределение по языку игры ZED ZONE / системы
        Russian = 1, // Русский (RU)
        English = 2, // English (EN)
        Chinese = 3  // 简体中文 (ZH)
    }

    public enum CompanionArchetype
    {
        Auto = 0,            // Автоопределение по GUID выжившего
        Veteran = 1,         // 🎖 Бывалый ветеран (Grizzled Veteran / 铁血老兵)
        StreetPunk = 2,      // 🔥 Дерзкий гопник (Street Punk / 街头混混)
        ParanoidPrepper = 3, // 👁 Параноик-выживальщик (Paranoid Prepper / 末日偏执狂)
        CombatMedic = 4,     // 💉 Полевой врач (Combat Medic / 战地医官)
        JollyScavenger = 5   // 🎒 Весельчак-мародёр (Jolly Scavenger / 乐天拾荒客)
    }

    public enum CompanionRank
    {
        Rookie = 0,     // Новобранец (0..99 XP)
        Fighter = 1,    // Боец (100..299 XP)
        Veteran = 2,    // Ветеран (300..699 XP)
        Elite = 3,      // Элита (700..1499 XP)
        ZoneLegend = 4  // Легенда Зоны (1500+ XP)
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
        PlayerShareFoodThanks,
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
        CombatFlankWarning,
        CombatWeaponJammed,
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
        LootOverweight,
        BaseAutoDropoff,
        AmbientRain,
        AmbientBloodRain,
        AmbientNight,
        AmbientMorning,
        AmbientHungry,
        AmbientTired,
        AmbientBaseIdle,
        BanterQuestion,
        BanterReply,
        WorldNpcEncounter,
        CampfireStory,
        ChoreGardening,
        ChoreWeaponMaint,
        ChoreVehicleCheck,
        CombatPlayerDownedRescue,
        CombatFocusTarget,
        CombatStealthLightsOff,
        LevelUpRank,
        PlayerDrivingRide,
        SquadBattleCry
    }

    /// <summary>
    /// Safe IL2CPP &amp; UnityEngine.Object validity helpers to prevent native pointer crashes across scene reloads.
    /// </summary>
    public static class Il2CppSafe
    {
        public static bool IsAlive(Il2CppObjectBase obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            try
            {
                return obj.Pointer != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAliveUnity(UnityEngine.Object uobj)
        {
            if (ReferenceEquals(uobj, null)) return false;
            try
            {
                return uobj.Pointer != IntPtr.Zero && uobj != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CarryItemEntry
    {
        public ItemData Item;
        public InventoryData FallbackSourceInv;
        public string TargetContainerId = "";
        public InventoryData TargetInv;
        public Transform TargetAnchor;
        public Vector2 TargetPos;
        public string TargetDisplayName = "";
        public StorageCategory Category = StorageCategory.None;
    }

    public enum PhysicalLootKind
    {
        GroundCluster = 0,
        Corpse = 1,
        WildContainer = 2
    }

    public enum CompanionBlockReason
    {
        None = 0,
        PathBlocked = 1,
        NoStorageSpace = 2,
        NoMarkedBoxes = 3,
        NothingToDo = 4,
        InterruptedByCombat = 5
    }

    public class PhysicalLootStop
    {
        public PhysicalLootKind Kind;
        public Vector2 WorldPos;
        public Transform TargetTransform;
        public BasicItem PrimaryGroundItem;
        public TerrainObject TargetTerrainObj;
        public InventoryData TargetInventory;
        public string DisplayName = "";
        public string TargetKey = "";
    }

    public class ContainerRuleProfile
    {
        public string ContainerId = "";
        public string CustomName = "";
        public StorageCategory Categories = StorageCategory.None;
        public bool IsDropoffSource = false;
        public bool IsMarkedByPlayer = false;
        public int Priority = 3;
        public bool AutoCompact = true;
        public Vector2 LastKnownPos;

        public bool IsConfiguredForSorting => IsMarkedByPlayer && (IsDropoffSource || Categories != StorageCategory.None);
    }

    public class CompanionRuntimeState
    {
        public string NpcGuid = "";
        public string LastKnownName = "";
        public string OwnerCharacterId = "";
        public string OwnerPlayerName = "";
        public TacticalDoctrine Doctrine = TacticalDoctrine.Balanced;
        public CompanionArchetype Archetype = CompanionArchetype.Auto;
        public int ExperiencePoints = 0;

        public bool AllowAutoSortBase = true;
        public bool AllowAutoLootNearby = false;
        public bool AllowFieldMedic = true;
        public bool AllowAmmoShare = true;
        public bool AllowAutoDropoffAtBase = true;
        public bool AllowBaseChores = true;

        public string CurrentTaskText = "";
        public int KillsThisSession = 0;
        public int SortedItemsThisSession = 0;
        public int LootedItemsThisSession = 0;
        public int ChoresDoneThisSession = 0;

        // Buff: "Сытый и злой" (Fed & Fierce) from sharing rations
        public float FedAndFierceUntil = -999f;
        public float LastRationTime = -999f;
        // Buff: "Боевой кураж / Штурм" (Battle Cry & Focus Assault) [F9]
        public float BattleCryBuffUntil = -999f;

        public float LastChatterTime = -999f;
        public float LastCombatCalloutTime = -999f;
        public float LastKiteStepTime = -999f;
        public float LastFireLineStepTime = -999f;
        public float LastWeaponSwitchTime = -999f;
        public float LastFieldMedicTime = -999f;
        public float LastAmmoShareTime = -999f;
        public float LastAutoSortTickTime = -999f;
        public float LastAutoLootTickTime = -999f;
        public float LastAutoDropoffTime = -999f;
        public float LastFlankWarnTime = -999f;
        public float LastOutOfCombatPrepTime = -999f;
        public float LastOverweightWarnTime = -999f;
        public float LastChoreTime = -999f;
        public float LastPlayerRescueTime = -999f;
        public float LastFlashlightCheckTime = -999f;

        // Active physical sorting state & invisible carry buffer
        public bool IsDismissedOrNotInTeam = false;
        public bool IsSortingActive = false;
        public int SortingPhase = 0; // 0=Idle, 1=WalkingToSource, 2=WalkingToDestination
        public string SourceContainerGuid = "";
        public string SourceContainerName = "";
        public InventoryData SourceContainerInv = null;
        public Transform SourceContainerAnchor = null;
        public bool SourceIsOwnBackpack = false;
        public StorageCategory SourceAllowedStayMask = StorageCategory.None;
        public string TargetContainerGuid = "";
        public string TargetContainerName = "";
        public Transform TargetContainerAnchor = null;
        public Vector2 TargetContainerPos;
        public float SortingStateStartTime = 0f;
        public int PendingTransferCount = 0;
        public readonly List<CarryItemEntry> CarryBuffer = new List<CarryItemEntry>();

        // Active physical scavenging / loot queue & physical camp chore walk
        public bool IsLootingActive = false;
        public bool IsSilentAutoLoot = false;
        public int LootPickedInCurrentSweep = 0;
        public readonly List<PhysicalLootStop> LootQueue = new List<PhysicalLootStop>();

        public bool IsChoreWalkActive = false;
        public Vector2 ChoreTargetPos;
        public Transform ChoreTargetAnchor = null;
        public string ChoreDisplayText = "";
        public int ChorePropItemId = 0;
        public Action PendingChoreArrivalAction = null;

        // Shared physical arrival / stuck-detection state (zero-teleport guarantee)
        public Vector2 WalkOriginPos;
        public Vector2 LastMovedPos;
        public float LastMovedTime = 0f;
        public float WalkBestDist = 9999f;
        public float LastProgressTime = 0f;
        public bool NavResetDone = false;
        public bool ArrivalInteractStarted = false;
        public float ArrivalInteractUntil = 0f;

        // Why the last physical job stopped; shown in the F8 squad card (1.8.1+).
        public CompanionBlockReason LastBlockReason = CompanionBlockReason.None;
        public string LastBlockTarget = "";
        public float LastBlockTime = -999f;

        public void NoteBlocked(CompanionBlockReason reason, string target = "")
        {
            LastBlockReason = reason;
            LastBlockTarget = target ?? "";
            LastBlockTime = Time.time;
        }

        public bool HasAnyPhysicalTask => IsSortingActive || IsLootingActive || IsChoreWalkActive;

        public bool HasFedBuff => Time.unscaledTime < FedAndFierceUntil;
        public float FedBuffRemainingSeconds => Mathf.Max(0f, FedAndFierceUntil - Time.unscaledTime);
        public bool HasBattleCryBuff => Time.unscaledTime < BattleCryBuffUntil;

        public CompanionArchetype EffectiveArchetype
        {
            get
            {
                if (Archetype != CompanionArchetype.Auto)
                    return Archetype;
                return CompanionConfigStore.ResolveArchetypeFromGuid(NpcGuid);
            }
        }

        public CompanionRank Rank => CompanionConfigStore.GetRankForXp(ExperiencePoints);

        public bool AddExperience(int deltaXp, GOAPAgent agent = null)
        {
            if (deltaXp <= 0) return false;
            var prevRank = Rank;
            ExperiencePoints = Mathf.Clamp(ExperiencePoints + deltaXp, 0, 999999);
            CompanionConfigStore.MarkProgressDirty();
            var newRank = Rank;
            if (newRank > prevRank)
            {
                try
                {
                    if (Il2CppSafe.IsAliveUnity(agent))
                    {
                        SmartChatterSystem.Speak(agent, ChatterTopic.LevelUpRank, minCooldown: 0.2f, bubbleDuration: 6.5f, force: true);
                        var status = agent.npcData?.characterData?.characterStatusData;
                        if (Il2CppSafe.IsAlive(status))
                        {
                            float bonusMaxHp = CompanionConfigStore.GetRankMaxHpBonus(newRank);
                            status.maxhp = Mathf.Max(status.maxhp, 100f + bonusMaxHp);
                            status.hp = status.maxhp;
                            status.energy = status.maxEnergy;
                            if (Il2CppSafe.IsAliveUnity(agent.humanCharacter))
                                agent.humanCharacter.hp = status.hp;
                        }
                    }
                    string rTitle = CompanionConfigStore.GetRankDisplayName(newRank, CompanionConfigStore.EffectiveLanguage);
                    BaseSortingSystem.SetBanner(CompanionConfigStore.L(
                        $"🎖 {LastKnownName} повысил ранг до «{rTitle}» ({ExperiencePoints} XP)! Здоровье и боевые навыки усилены!",
                        $"🎖 {LastKnownName} promoted to '{rTitle}' ({ExperiencePoints} XP)! Combat stats & stamina boosted!",
                        $"🎖 {LastKnownName} 晋升为「{rTitle}」（{ExperiencePoints} XP）！生命值与战斗素养大幅提升！"));
                    CompanionConfigStore.Save();
                }
                catch { }
                return true;
            }
            return false;
        }
    }

    public static class CompanionConfigStore
    {
        private static bool _progressDirty;
        private static float _nextProgressSaveAt;

        public static void MarkProgressDirty()
        {
            if (!_progressDirty) _nextProgressSaveAt = Time.unscaledTime + 5f;
            _progressDirty = true;
        }

        public static void FlushPendingProgress(bool force = false)
        {
            if (_progressDirty && (force || Time.unscaledTime >= _nextProgressSaveAt))
            {
                _nextProgressSaveAt = Time.unscaledTime + 5f;
                Save();
            }
        }

        public static readonly Dictionary<string, ContainerRuleProfile> ContainerRules =
            new Dictionary<string, ContainerRuleProfile>(StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, CompanionRuntimeState> CompanionStates =
            new Dictionary<string, CompanionRuntimeState>(StringComparer.OrdinalIgnoreCase);

        public static ModLanguage LanguageMode = ModLanguage.Auto;

        public static ModLanguage EffectiveLanguage
        {
            get
            {
                if (LanguageMode == ModLanguage.Russian || LanguageMode == ModLanguage.English || LanguageMode == ModLanguage.Chinese)
                    return LanguageMode;
                return DetectGameOrSystemLanguage();
            }
        }

        public static bool UseRussian
        {
            get => EffectiveLanguage == ModLanguage.Russian;
            set => LanguageMode = value ? ModLanguage.Russian : ModLanguage.English;
        }

        public static bool IsChinese => EffectiveLanguage == ModLanguage.Chinese;
        public static bool IsEnglish => EffectiveLanguage == ModLanguage.English;

        public static string L(string ru, string en, string zh)
        {
            return L(EffectiveLanguage, ru, en, zh);
        }

        public static string L(ModLanguage lang, string ru, string en, string zh)
        {
            if (lang == ModLanguage.Auto) lang = EffectiveLanguage;
            return lang switch
            {
                ModLanguage.Russian => ru,
                ModLanguage.Chinese => zh,
                _ => en
            };
        }

        public static ModLanguage DetectGameOrSystemLanguage()
        {
            string code = "";
            try
            {
                if (Il2CppSafe.IsAliveUnity(GameController.instance) && Il2CppSafe.IsAlive(GameController.instance.gameSettings))
                {
                    code = GameController.instance.gameSettings.languageCode ?? "";
                }
            }
            catch { }

            SystemLanguage sysLang = SystemLanguage.English;
            try
            {
                sysLang = Application.systemLanguage;
            }
            catch { }

            return ResolveLanguageMode(code, sysLang);
        }

        public static ModLanguage ResolveLanguageMode(string gameLanguageCode, SystemLanguage fallbackSystemLang)
        {
            if (!string.IsNullOrWhiteSpace(gameLanguageCode))
            {
                string c = gameLanguageCode.Trim().ToLowerInvariant();
                if (c == "zh" || c == "cn" || c.StartsWith("zh-") || c.StartsWith("zh_") ||
                    c.Contains("chinese") || c.Contains("schinese") || c.Contains("tchinese") || c.Contains("han"))
                {
                    return ModLanguage.Chinese;
                }
                if (c == "ru" || c.StartsWith("ru-") || c.StartsWith("ru_") || c.Contains("russian") || c.Contains("рус"))
                {
                    return ModLanguage.Russian;
                }
                return ModLanguage.English;
            }

            if (fallbackSystemLang == SystemLanguage.Chinese ||
                fallbackSystemLang == SystemLanguage.ChineseSimplified ||
                fallbackSystemLang == SystemLanguage.ChineseTraditional)
            {
                return ModLanguage.Chinese;
            }

            if (fallbackSystemLang == SystemLanguage.Russian ||
                fallbackSystemLang == SystemLanguage.Belarusian ||
                fallbackSystemLang == SystemLanguage.Ukrainian)
            {
                return ModLanguage.Russian;
            }

            return ModLanguage.English;
        }

        public static bool EnableChatter = true;
        public static ProfanityMode Profanity = ProfanityMode.HardcoreRude;
        public static float ChatterIntervalMultiplier = 1.0f;
        public static bool EnableSmartCombat = true;
        public static bool EnableAntiFriendlyFire = true;
        public static bool EnableKiting = true;
        public static bool EnableFieldMedic = true;
        public static bool EnableAmmoShare = true;
        public static bool AutoShareFoodOnEat = false;
        public static bool EnableBaseAutoDropoff = true;
        public static bool EnableWorldNpcChatter = true;
        public static bool EnableBaseChores = true;
        public static bool EnableSmartFlashlight = true;
        public static bool EnablePlayerRescue = true;
        public static bool ShowHudBadge = true;
        public static bool HudBadgeCollapsed = false;
        public static float BaseScanRadius = 48f;
        public static int MaxTeamSize = 0; // 0 = Auto/Vanilla/AVO Compatibility, or 4, 6, 8, 10, 12, 15
        public static bool RequireAltForRationHotkey = false;
        public static float UiViewportWidth = 0f, UiViewportHeight = 0f;
        public static float UiScale = 0f; // 0=Auto; otherwise 0.75..2, capped to screen fit.
        public static float MainWinPosX = -1f;
        public static float MainWinPosY = -1f;
        public static float ChestBarPosX = -1f;
        public static float ChestBarPosY = -1f;
        public static float HudBadgePosX = -1f;
        public static float HudBadgePosY = -1f;

        private static bool _avoCheckDone = false;
        private static bool _avoDetected = false;

        public static bool IsAvoExpandedLoaded()
        {
            if (_avoCheckDone) return _avoDetected;
            _avoCheckDone = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = asm?.GetName()?.Name ?? "";
                    if (n.IndexOf("AVO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("AdvancedSquadControl", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _avoDetected = true;
                        break;
                    }
                }
            }
            catch { }
            return _avoDetected;
        }

        public static bool ShouldUseAltForRationHotkey()
        {
            return RequireAltForRationHotkey || IsAvoExpandedLoaded();
        }

        public static void ApplyMaxTeamSizeIfConfigured()
        {
            try
            {
                if (MaxTeamSize > 0)
                    CharacterData.maxTeamNPCNumber = MaxTeamSize;
            }
            catch { }
        }

        public static void CycleMaxTeamSize()
        {
            int[] presets = { 0, 4, 6, 8, 10, 12, 15 };
            int next = 4;
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] == MaxTeamSize)
                {
                    next = presets[(i + 1) % presets.Length];
                    break;
                }
            }
            MaxTeamSize = next;
            if (MaxTeamSize > 0)
            {
                try { CharacterData.maxTeamNPCNumber = MaxTeamSize; } catch { }
            }
            else
            {
                try { if (!IsAvoExpandedLoaded()) CharacterData.maxTeamNPCNumber = 3; } catch { }
            }
            Save();
        }

        public static string FormatMaxTeamSizeLabel(ModLanguage lang)
        {
            if (MaxTeamSize <= 0)
                return L(lang, "Авто (3 / AVO)", "Auto (3 / AVO)", "自动（原版3 / 兼容AVO）");
            return L(lang, $"{MaxTeamSize} чел.", $"{MaxTeamSize} NPCs", $"{MaxTeamSize} 人");
        }

        public static void ResetUiPositions()
        {
            ShowHudBadge = true;
            HudBadgeCollapsed = false;
            MainWinPosX = -1f;
            MainWinPosY = -1f;
            ChestBarPosX = -1f;
            ChestBarPosY = -1f;
            HudBadgePosX = -1f;
            HudBadgePosY = -1f;
            SmartCompanionsUI.ResetWindowRectsToDefault();
            Save();
        }

        public static CompanionArchetype ResolveArchetypeFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return CompanionArchetype.Veteran;
            uint hash = 2166136261u;
            for (int i = 0; i < guid.Length; i++)
            {
                hash ^= guid[i];
                hash *= 16777619u;
            }
            int idx = (int)(hash % 5u) + 1;
            return (CompanionArchetype)idx;
        }

        public static CompanionRank GetRankForXp(int xp)
        {
            if (xp >= 1500) return CompanionRank.ZoneLegend;
            if (xp >= 700) return CompanionRank.Elite;
            if (xp >= 300) return CompanionRank.Veteran;
            if (xp >= 100) return CompanionRank.Fighter;
            return CompanionRank.Rookie;
        }

        public static int GetNextRankXpThreshold(CompanionRank rank)
        {
            return rank switch
            {
                CompanionRank.Rookie     => 100,
                CompanionRank.Fighter    => 300,
                CompanionRank.Veteran    => 700,
                CompanionRank.Elite      => 1500,
                _                        => 1500
            };
        }

        public static float GetRankMaxHpBonus(CompanionRank rank)
        {
            return rank switch
            {
                CompanionRank.Fighter    => 12f,
                CompanionRank.Veteran    => 28f,
                CompanionRank.Elite      => 48f,
                CompanionRank.ZoneLegend => 75f,
                _                        => 0f
            };
        }

        public static string GetArchetypeDisplayName(CompanionArchetype arch, ModLanguage lang)
        {
            return arch switch
            {
                CompanionArchetype.Veteran         => L(lang, "Бывалый ветеран", "Grizzled Veteran", "铁血老兵"),
                CompanionArchetype.StreetPunk      => L(lang, "Дерзкий гопник", "Street Punk", "街头混混"),
                CompanionArchetype.ParanoidPrepper => L(lang, "Параноик-выживальщик", "Paranoid Prepper", "末日偏执狂"),
                CompanionArchetype.CombatMedic     => L(lang, "Полевой врач", "Combat Medic", "战地医官"),
                CompanionArchetype.JollyScavenger  => L(lang, "Весельчак-мародёр", "Jolly Scavenger", "乐天拾荒客"),
                _                                  => L(lang, "Авто-характер", "Auto Personality", "自动性格")
            };
        }

        public static string GetRankDisplayName(CompanionRank rank, ModLanguage lang)
        {
            return rank switch
            {
                CompanionRank.Rookie     => L(lang, "Новобранец", "Rookie", "新兵"),
                CompanionRank.Fighter    => L(lang, "Боец", "Fighter", "战士"),
                CompanionRank.Veteran    => L(lang, "Ветеран", "Veteran", "老兵"),
                CompanionRank.Elite      => L(lang, "Элита", "Elite", "精锐"),
                CompanionRank.ZoneLegend => L(lang, "Легенда Зоны", "Zone Legend", "禁区传奇"),
                _                        => L(lang, "Боец", "Fighter", "战士")
            };
        }

        private static string SaveFilePath =>
            Path.Combine(Paths.ConfigPath, "SmartCompanions_Rules.txt");

        private static string JsonConfigFilePath =>
            Path.Combine(Paths.ConfigPath, "SmartCompanionsConfig.json");

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
                sb.AppendLine("# ZED ZONE Smart Companions Saved Rules v1.4.2");
                sb.AppendLine($"SET|LanguageMode|{(int)LanguageMode}");
                sb.AppendLine($"SET|UseRussian|{UseRussian}");
                sb.AppendLine($"SET|EnableChatter|{EnableChatter}");
                sb.AppendLine($"SET|Profanity|{(int)Profanity}");
                sb.AppendLine($"SET|ChatterIntervalMultiplier|{ChatterIntervalMultiplier.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|EnableSmartCombat|{EnableSmartCombat}");
                sb.AppendLine($"SET|EnableAntiFriendlyFire|{EnableAntiFriendlyFire}");
                sb.AppendLine($"SET|EnableKiting|{EnableKiting}");
                sb.AppendLine($"SET|EnableFieldMedic|{EnableFieldMedic}");
                sb.AppendLine($"SET|EnableAmmoShare|{EnableAmmoShare}");
                sb.AppendLine($"SET|AutoShareFoodOnEat|{AutoShareFoodOnEat}");
                sb.AppendLine($"SET|EnableBaseAutoDropoff|{EnableBaseAutoDropoff}");
                sb.AppendLine($"SET|EnableWorldNpcChatter|{EnableWorldNpcChatter}");
                sb.AppendLine($"SET|EnableBaseChores|{EnableBaseChores}");
                sb.AppendLine($"SET|EnableSmartFlashlight|{EnableSmartFlashlight}");
                sb.AppendLine($"SET|EnablePlayerRescue|{EnablePlayerRescue}");
                sb.AppendLine($"SET|ShowHudBadge|{ShowHudBadge}");
                sb.AppendLine($"SET|HudBadgeCollapsed|{HudBadgeCollapsed}");
                sb.AppendLine($"SET|BaseScanRadius|{BaseScanRadius.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|MaxTeamSize|{MaxTeamSize}");
                sb.AppendLine($"SET|RequireAltForRationHotkey|{RequireAltForRationHotkey}");
                sb.AppendLine($"SET|UiViewportWidth|{UiViewportWidth.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|UiViewportHeight|{UiViewportHeight.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|UiScale|{UiScale.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|MainWinPosX|{MainWinPosX.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|MainWinPosY|{MainWinPosY.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|ChestBarPosX|{ChestBarPosX.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|ChestBarPosY|{ChestBarPosY.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|HudBadgePosX|{HudBadgePosX.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"SET|HudBadgePosY|{HudBadgePosY.ToString("F1", CultureInfo.InvariantCulture)}");

                foreach (var kv in ContainerRules)
                {
                    var r = kv.Value;
                    if (string.IsNullOrEmpty(r.ContainerId) ||
                        string.Equals(r.ContainerId, "unknown_box", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.ContainerId, "unknown", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!r.IsMarkedByPlayer)
                        continue;
                    if (r.Categories == StorageCategory.None && !r.IsDropoffSource && string.IsNullOrEmpty(r.CustomName))
                        continue;
                    var safeName = (r.CustomName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    sb.AppendLine($"BOX|{r.ContainerId}|{(int)r.Categories}|{r.IsDropoffSource}|{r.Priority}|{r.AutoCompact}|{safeName}|{r.IsMarkedByPlayer}");
                }

                foreach (var kv in CompanionStates)
                {
                    var c = kv.Value;
                    var safeName = (c.LastKnownName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    var safeOwnerId = (c.OwnerCharacterId ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    var safeOwnerName = (c.OwnerPlayerName ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");
                    sb.AppendLine($"NPC|{c.NpcGuid}|{(int)c.Doctrine}|{c.AllowAutoSortBase}|{c.AllowAutoLootNearby}|{c.AllowFieldMedic}|{c.AllowAmmoShare}|{safeName}|{c.AllowAutoDropoffAtBase}|{(int)c.Archetype}|{c.ExperiencePoints}|{c.AllowBaseChores}|{safeOwnerId}|{safeOwnerName}");
                }

                File.WriteAllText(SaveFilePath, sb.ToString(), Encoding.UTF8);

                string json = "{\n" +
                    $"  \"Version\": \"{SmartCompanionsPlugin.PluginVersion}\",\n" +
                    $"  \"LanguageMode\": \"{LanguageMode}\",\n" +
                    $"  \"LanguageModeId\": {(int)LanguageMode},\n" +
                    $"  \"EffectiveLanguage\": \"{EffectiveLanguage}\",\n" +
                    $"  \"Profanity\": \"{Profanity}\",\n" +
                    $"  \"EnableChatter\": {EnableChatter.ToString().ToLowerInvariant()},\n" +
                    $"  \"EnableSmartCombat\": {EnableSmartCombat.ToString().ToLowerInvariant()},\n" +
                    $"  \"EnableBaseChores\": {EnableBaseChores.ToString().ToLowerInvariant()},\n" +
                    $"  \"EnableSmartFlashlight\": {EnableSmartFlashlight.ToString().ToLowerInvariant()},\n" +
                    $"  \"EnablePlayerRescue\": {EnablePlayerRescue.ToString().ToLowerInvariant()},\n" +
                    $"  \"AutoShareFoodOnEat\": {AutoShareFoodOnEat.ToString().ToLowerInvariant()}\n" +
                    "}\n";
                File.WriteAllText(JsonConfigFilePath, json, Encoding.UTF8);
                _progressDirty = false;
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
                bool isV142Format = false;
                foreach (var raw in File.ReadAllLines(SaveFilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#"))
                    {
                        if (line.Contains("v1.4.2")) isV142Format = true;
                        continue;
                    }
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;

                    if (parts[0] == "SET")
                    {
                        var key = parts[1];
                        var val = parts[2];
                        if (key == "LanguageMode" && int.TryParse(val, out var lMode)) LanguageMode = (ModLanguage)Mathf.Clamp(lMode, 0, 3);
                        else if (key == "LanguageMode" && Enum.TryParse<ModLanguage>(val, true, out var lEnum)) LanguageMode = lEnum;
                        else if (key == "UseRussian" && bool.TryParse(val, out var b0) && LanguageMode == ModLanguage.Auto) { /* Keep Auto unless explicitly set */ }
                        else if (key == "EnableChatter" && bool.TryParse(val, out var b1)) EnableChatter = b1;
                        else if (key == "Profanity" && int.TryParse(val, out var pMode)) Profanity = (ProfanityMode)Mathf.Clamp(pMode, 0, 2);
                        else if (key == "ChatterIntervalMultiplier" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f1)) ChatterIntervalMultiplier = Mathf.Clamp(f1, 0.25f, 4f);
                        else if (key == "EnableSmartCombat" && bool.TryParse(val, out var b2)) EnableSmartCombat = b2;
                        else if (key == "EnableAntiFriendlyFire" && bool.TryParse(val, out var b3)) EnableAntiFriendlyFire = b3;
                        else if (key == "EnableKiting" && bool.TryParse(val, out var b4)) EnableKiting = b4;
                        else if (key == "EnableFieldMedic" && bool.TryParse(val, out var b5)) EnableFieldMedic = b5;
                        else if (key == "EnableAmmoShare" && bool.TryParse(val, out var b6)) EnableAmmoShare = b6;
                        else if (key == "AutoShareFoodOnEat" && bool.TryParse(val, out var bShare)) AutoShareFoodOnEat = bShare;
                        else if (key == "EnableBaseAutoDropoff" && bool.TryParse(val, out var bDrop)) EnableBaseAutoDropoff = bDrop;
                        else if (key == "EnableWorldNpcChatter" && bool.TryParse(val, out var bWorld)) EnableWorldNpcChatter = bWorld;
                        else if (key == "EnableBaseChores" && bool.TryParse(val, out var bChores)) EnableBaseChores = bChores;
                        else if (key == "EnableSmartFlashlight" && bool.TryParse(val, out var bFlash)) EnableSmartFlashlight = bFlash;
                        else if (key == "EnablePlayerRescue" && bool.TryParse(val, out var bRescue)) EnablePlayerRescue = bRescue;
                        else if (key == "ShowHudBadge" && bool.TryParse(val, out var b7)) ShowHudBadge = b7;
                        else if (key == "HudBadgeCollapsed" && bool.TryParse(val, out var bCol)) HudBadgeCollapsed = bCol;
                        else if (key == "BaseScanRadius" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f2)) BaseScanRadius = Mathf.Clamp(f2, 15f, 120f);
                        else if (key == "MaxTeamSize" && int.TryParse(val, out var mts)) MaxTeamSize = Mathf.Clamp(mts, 0, 30);
                        else if (key == "RequireAltForRationHotkey" && bool.TryParse(val, out var bAlt)) RequireAltForRationHotkey = bAlt;
                        else if (key == "UiViewportWidth" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var uiW))
                            UiViewportWidth = float.IsFinite(uiW) && uiW > 0f ? uiW : 0f;
                        else if (key == "UiViewportHeight" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var uiH))
                            UiViewportHeight = float.IsFinite(uiH) && uiH > 0f ? uiH : 0f;
                        else if (key == "UiScale" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var uiScale))
                            UiScale = float.IsNaN(uiScale) || float.IsInfinity(uiScale) || uiScale <= 0f ? 0f : Mathf.Clamp(uiScale, 0.75f, 2f);
                        else if (key == "MainWinPosX" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var mx)) MainWinPosX = mx;
                        else if (key == "MainWinPosY" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var my)) MainWinPosY = my;
                        else if (key == "ChestBarPosX" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx)) ChestBarPosX = cx;
                        else if (key == "ChestBarPosY" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy)) ChestBarPosY = cy;
                        else if (key == "HudBadgePosX" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var hx)) HudBadgePosX = hx;
                        else if (key == "HudBadgePosY" && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var hy)) HudBadgePosY = hy;
                    }
                    else if (parts[0] == "BOX" && parts.Length >= 6)
                    {
                        var id = parts[1];
                        if (string.IsNullOrEmpty(id) ||
                            string.Equals(id, "unknown_box", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(id, "unknown", StringComparison.OrdinalIgnoreCase))
                            continue;
                        int.TryParse(parts[2], out var catInt);
                        bool.TryParse(parts[3], out var drop);
                        int.TryParse(parts[4], out var prio);
                        bool.TryParse(parts[5], out var compact);
                        var name = parts.Length >= 7 ? parts[6] : "";
                        bool isMarked = true;
                        if (parts.Length >= 8 && bool.TryParse(parts[7], out var parsedMarked))
                            isMarked = parsedMarked;

                        // Purge bogus Medicine-only rules generated by v1.4.1's AutoDetectCategoriesFromContents bug
                        if (!isV142Format && (StorageCategory)catInt == StorageCategory.Medicine && !drop)
                            continue;
                        if (!isMarked)
                            continue;

                        ContainerRules[id] = new ContainerRuleProfile
                        {
                            ContainerId = id,
                            Categories = (StorageCategory)catInt,
                            IsDropoffSource = drop,
                            IsMarkedByPlayer = true,
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
                        bool autoDrop = true;
                        if (parts.Length >= 9 && bool.TryParse(parts[8], out var ad)) autoDrop = ad;
                        var arch = CompanionArchetype.Auto;
                        if (parts.Length >= 10 && int.TryParse(parts[9], out var archInt)) arch = (CompanionArchetype)Mathf.Clamp(archInt, 0, 5);
                        int xp = 0;
                        if (parts.Length >= 11 && int.TryParse(parts[10], out var xpParsed)) xp = Mathf.Max(0, xpParsed);
                        bool chores = true;
                        if (parts.Length >= 12 && bool.TryParse(parts[11], out var chParsed)) chores = chParsed;
                        var ownerId = parts.Length >= 13 ? parts[12] : "";
                        var ownerName = parts.Length >= 14 ? parts[13] : "";

                        CompanionStates[guid] = new CompanionRuntimeState
                        {
                            NpcGuid = guid,
                            Doctrine = (TacticalDoctrine)Mathf.Clamp(docInt, 0, 5),
                            AllowAutoSortBase = sort,
                            AllowAutoLootNearby = loot,
                            AllowFieldMedic = med,
                            AllowAmmoShare = ammo,
                            AllowAutoDropoffAtBase = autoDrop,
                            Archetype = arch,
                            ExperiencePoints = xp,
                            AllowBaseChores = chores,
                            LastKnownName = name,
                            OwnerCharacterId = ownerId,
                            OwnerPlayerName = ownerName
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
