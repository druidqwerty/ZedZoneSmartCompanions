using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    /// <summary>
    /// 2.3.0: Performance caches, telemetry counters, enhanced AVO Expanded compatibility checks,
    /// and a privacy-safe diagnostic report generator (zero PII, zero save/inventory contents).
    /// </summary>
    public static class PerformanceAndDiagnostics
    {
        public static long TeammateCacheHits { get; private set; }
        public static long TeammateCacheMisses { get; private set; }
        public static long ItemCategoryCacheHits { get; private set; }
        public static long ItemCategoryCacheMisses { get; private set; }
        public static long TerrainScanCacheHits { get; private set; }
        public static long TerrainScanCacheMisses { get; private set; }
        public static double LastContainerScanMs { get; internal set; }
        public static int LastPatchedMethodCount { get; internal set; }

        private static readonly Dictionary<int, StorageCategory> ItemCategoryMemo = new Dictionary<int, StorageCategory>(512);

        // Shared hostile-character snapshot so 15 companions do not each re-scan all 150+ world characters
        public struct HostileEntry
        {
            public BasicCharacterController Controller;
            public Vector2 Pos;
        }

        private static readonly List<HostileEntry> CachedHostiles = new List<HostileEntry>(64);
        private static float _cachedHostilesAt = -999f;
        private static int _cachedHostilesCamp = -999;

        public static int ItemCategoryCacheSize => ItemCategoryMemo.Count;

        public static void ClearCachesOnTransition()
        {
            SmartChatterSystem.InvalidateSquadCache();
            BaseSortingSystem.InvalidateTerrainScanCache();
            CachedHostiles.Clear();
            _cachedHostilesAt = -999f;
        }

        public static bool TryGetMemoizedCategory(int itemId, out StorageCategory cat)
        {
            if (itemId > 0 && ItemCategoryMemo.TryGetValue(itemId, out cat))
            {
                ItemCategoryCacheHits++;
                return true;
            }
            cat = StorageCategory.MiscAndValuables;
            return false;
        }

        public static void StoreMemoizedCategory(int itemId, StorageCategory cat)
        {
            if (itemId <= 0) return;
            if (ItemCategoryMemo.Count > 4096) ItemCategoryMemo.Clear();
            ItemCategoryMemo[itemId] = cat;
            ItemCategoryCacheMisses++;
        }

        public static void NoteTeammateCache(bool hit)
        {
            if (hit) TeammateCacheHits++;
            else TeammateCacheMisses++;
        }

        public static void NoteTerrainScanCache(bool hit)
        {
            if (hit) TerrainScanCacheHits++;
            else TerrainScanCacheMisses++;
        }

        public static IReadOnlyList<HostileEntry> GetActiveHostilesSnapshot(GOAPAgent observer, int friendlyCamp, float now)
        {
            if (now - _cachedHostilesAt < 0.20f && _cachedHostilesCamp == friendlyCamp)
                return CachedHostiles;

            _cachedHostilesAt = now;
            _cachedHostilesCamp = friendlyCamp;
            CachedHostiles.Clear();

            try
            {
                var gc = GameController.instance;
                if (!Il2CppSafe.IsAliveUnity(gc) || gc.characterList == null)
                    return CachedHostiles;

                int count = gc.characterList.Count;
                for (int i = 0; i < count; i++)
                {
                    var c = gc.characterList[i];
                    if (!Il2CppSafe.IsAliveUnity(c) || c.deadFlag || !c.enabled || !c.gameObject.activeInHierarchy)
                        continue;
                    if (!SmartCombatAI.IsActualHostileCharacter(observer, c, friendlyCamp))
                        continue;
                    CachedHostiles.Add(new HostileEntry
                    {
                        Controller = c,
                        Pos = c.transform.position
                    });
                }
            }
            catch { }
            return CachedHostiles;
        }

        public static bool DetectAvoExpanded(out string source)
        {
            source = "none";
            try
            {
                var plugins = IL2CPPChainloader.Instance?.Plugins;
                if (plugins != null)
                {
                    foreach (var kv in plugins)
                    {
                        string guid = kv.Key ?? "";
                        string name = kv.Value?.Metadata?.Name ?? "";
                        if (guid.IndexOf("avo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("AVO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Advanced Squad", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            source = "BepInExPlugin";
                            return true;
                        }
                    }
                }
            }
            catch { }

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = asm?.GetName()?.Name ?? "";
                    if (n.IndexOf("AVO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("AdvancedSquadControl", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        source = "LoadedAssembly";
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static string DiagnosticReportPath =>
            Path.Combine(Paths.ConfigPath, "SmartCompanions_Diagnostics.txt");

        /// <summary>
        /// Builds a plain-text bug-report summary containing ZERO file paths, OS usernames,
        /// Steam IDs, player/companion names, base coordinates, or save inventory contents.
        /// </summary>
        public static string BuildPrivacySafeReport()
        {
            var sb = new StringBuilder(1536);
            bool avo = CompanionConfigStore.IsAvoExpandedLoaded(out string avoSource);
            int nativeCap = 3;
            try { nativeCap = CharacterData.maxTeamNPCNumber; } catch { }

            sb.AppendLine("=== ZED ZONE: Smart Companions Diagnostic Report (Privacy-Safe) ===");
            sb.AppendLine($"ModVersion: {SmartCompanionsPlugin.PluginVersion}");
            sb.AppendLine($"UnityVersion: {Application.unityVersion}");
            sb.AppendLine($"Screen: {Screen.width}x{Screen.height} | UiScalePref: {CompanionConfigStore.UiScale:F2} | EffectiveUiScale: {SmartCompanionsUI.EffectiveUiScale:F3}");
            sb.AppendLine($"LanguageMode: {CompanionConfigStore.LanguageMode} (Effective: {CompanionConfigStore.EffectiveLanguage})");
            sb.AppendLine($"HarmonyPatchedMethods: {LastPatchedMethodCount}");
            sb.AppendLine($"MultiplayerMode: {(MultiplayerSyncSystem.IsMultiplayerHost ? "Host" : MultiplayerSyncSystem.IsMultiplayerClient ? "Client" : "Singleplayer")} | RejectedMpChanges: {MultiplayerSyncSystem.RejectedCompanionChanges}");
            sb.AppendLine($"AvoExpandedDetected: {avo} ({avoSource}) | ConfiguredSquadCap: {CompanionConfigStore.MaxTeamSize} | EffectiveNativeCap: {nativeCap} | RationHotkey: {(CompanionConfigStore.ShouldUseAltForRationHotkey() ? "Alt+F6" : "F6")}");
            sb.AppendLine($"Tactics: Formation={CompanionConfigStore.ActiveFormation} | FollowDist={CompanionConfigStore.FollowDistance} | DoorwayYield={CompanionConfigStore.EnableNarrowPassageYield} | RetreatHP={CompanionConfigStore.RetreatHpThresholdPercent}% | LowAmmoRetreat={CompanionConfigStore.LowAmmoRetreat}");
            sb.AppendLine($"BaseAndSupply: Paused={CompanionConfigStore.BaseWorkPaused} | Priority={CompanionConfigStore.BasePriorityMode} | LootRadius={CompanionConfigStore.LootSweepRadius:F0}m | ZonePinned={CompanionConfigStore.LootZonePinned} | RareFood={CompanionConfigStore.AllowSpendRareFood} | RareMeds={CompanionConfigStore.AllowSpendRareMeds} | RareAmmo={CompanionConfigStore.AllowSpendRareAmmo}");

            int markedChests = 0, dropoffChests = 0;
            foreach (var kv in CompanionConfigStore.ContainerRules)
            {
                var r = kv.Value;
                if (r == null || !r.IsMarkedByPlayer) continue;
                markedChests++;
                if (r.IsDropoffSource) dropoffChests++;
            }
            sb.AppendLine($"WarehouseRules: MarkedChests={markedChests} (Dropoff={dropoffChests}) | CachedNearbyContainers={BaseSortingSystem.CachedContainers.Count} | LastScanMs={LastContainerScanMs:F2}");
            sb.AppendLine($"Caches: TeammateHits={TeammateCacheHits}/{TeammateCacheHits + TeammateCacheMisses} | ItemCatMemo={ItemCategoryMemo.Count} (Hits={ItemCategoryCacheHits}) | TerrainScanHits={TerrainScanCacheHits}/{TerrainScanCacheHits + TerrainScanCacheMisses}");

            var activeSquad = SmartChatterSystem.GetActiveTeammateAgents();
            sb.AppendLine($"ActiveSquadCount: {activeSquad.Count} | StoredCompanionProfiles: {CompanionConfigStore.CompanionStates.Count}");

            int idx = 0;
            foreach (var kv in CompanionConfigStore.CompanionStates)
            {
                var s = kv.Value;
                if (s == null) continue;
                idx++;
                if (idx > 15) break;
                // Strictly anonymized: never emit NpcGuid, LastKnownName, OwnerPlayerName, OwnerCharacterId, or coordinates!
                sb.AppendLine(
                    $"  Companion#{idx:00}: Spec={s.Specialization} | Role={s.WorkRole} | Doc={s.Doctrine} | Loadout={s.Loadout} | " +
                    $"Order={s.ActiveOrder} | OrderBlock={s.OrderBlockReason} | WorkBlock={s.LastBlockReason} | " +
                    $"Reserves={s.ReserveAmmoMin}/{s.ReserveFoodMin}/{s.ReserveMedMin} | Busy={(s.HasAnyPhysicalTask ? 1 : 0)} | Carry={s.CarryBuffer.Count}");
            }

            sb.AppendLine("PrivacyNotice: No save files, world coordinates, Steam IDs, OS paths, or character names are included.");
            return sb.ToString();
        }

        public static bool ExportDiagnosticReport(out string reportText)
        {
            reportText = BuildPrivacySafeReport();
            try
            {
                GUIUtility.systemCopyBuffer = reportText;
            }
            catch { }

            try
            {
                Directory.CreateDirectory(Paths.ConfigPath);
                File.WriteAllText(DiagnosticReportPath, reportText, Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                SmartCompanionsPlugin.LogWarning("ExportDiagnosticReport write failed: " + e.Message);
                return false;
            }
        }
    }
}
