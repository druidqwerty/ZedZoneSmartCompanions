using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.SmartCompanions
{
    public class DiscoveredContainer
    {
        public string ContainerId;
        public string DisplayName;
        public InventoryData Inventory;
        public TerrainObject TerrainObj;
        public Vector2 WorldPos;
        public float DistanceToPlayer;
        public int ItemCount;
        public ContainerRuleProfile Rule;
    }

    public static partial class BaseSortingSystem
    {
        public static readonly List<DiscoveredContainer> CachedContainers = new List<DiscoveredContainer>();
        private static readonly Dictionary<IntPtr, TerrainObject> KnownInvToTerrainObj = new Dictionary<IntPtr, TerrainObject>();
        private static readonly Dictionary<IntPtr, Vector3> KnownInvToWorldPos = new Dictionary<IntPtr, Vector3>();
        private static readonly Dictionary<IntPtr, string> KnownInvToStableId = new Dictionary<IntPtr, string>();
        private static readonly Dictionary<string, IntPtr> KnownStableIdToInv = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        private static float _lastContainerScanTime = -999f;
        public static string LastSortStatusBanner = "";
        public static float LastSortStatusBannerUntil = -1f;

        private static string EnsureUniqueIdForInventory(string candidateId, InventoryData inv)
        {
            if (string.IsNullOrEmpty(candidateId))
                candidateId = Il2CppSafe.IsAlive(inv) ? $"inv_{inv.Pointer.ToInt64():X}" : "inv_unassigned";
            if (!Il2CppSafe.IsAlive(inv))
                return candidateId;

            if (KnownStableIdToInv.TryGetValue(candidateId, out var existingPtr) && existingPtr != inv.Pointer)
            {
                candidateId = $"{candidateId}_{inv.Pointer.ToInt64():X}";
            }
            KnownStableIdToInv[candidateId] = inv.Pointer;
            KnownInvToStableId[inv.Pointer] = candidateId;
            inv.inventoryId = candidateId;
            return candidateId;
        }

        public static void RegisterInventoryTerrainObject(InventoryData inv, TerrainObject tobj)
        {
            if (!Il2CppSafe.IsAlive(inv) || !Il2CppSafe.IsAliveUnity(tobj)) return;
            try
            {
                KnownInvToTerrainObj[inv.Pointer] = tobj;
                Vector3 pos = tobj.transform.position;
                KnownInvToWorldPos[inv.Pointer] = pos;
                inv.worldposition = pos;
                GetContainerStableId(inv, tobj);
            }
            catch { }
        }

        public static string GetContainerStableId(InventoryData inv, TerrainObject tobj = null)
        {
            try
            {
                CompanionConfigStore.ContainerRules.Remove("unknown_box");
                CompanionConfigStore.ContainerRules.Remove("unknown");

                if (Il2CppSafe.IsAlive(inv) && KnownInvToStableId.TryGetValue(inv.Pointer, out var memoId) && !string.IsNullOrEmpty(memoId))
                {
                    if (!Il2CppSafe.IsAliveUnity(tobj) && KnownInvToTerrainObj.TryGetValue(inv.Pointer, out var memoTobj) && Il2CppSafe.IsAliveUnity(memoTobj))
                        tobj = memoTobj;
                    return memoId;
                }

                if (!Il2CppSafe.IsAliveUnity(tobj) && Il2CppSafe.IsAlive(inv))
                {
                    if (KnownInvToTerrainObj.TryGetValue(inv.Pointer, out var cachedTobj) && Il2CppSafe.IsAliveUnity(cachedTobj))
                    {
                        tobj = cachedTobj;
                    }
                    else if (ZedZone.Net.NetContainerSync.ClientHasActiveContainer &&
                        Il2CppSafe.IsAlive(ZedZone.Net.NetContainerSync.ClientActiveReplica) &&
                        ZedZone.Net.NetContainerSync.ClientActiveReplica.Pointer == inv.Pointer &&
                        Il2CppSafe.IsAliveUnity(ZedZone.Net.NetContainerSync.ClientActiveLocalObject))
                    {
                        tobj = ZedZone.Net.NetContainerSync.ClientActiveLocalObject;
                    }
                    else if (Il2CppSafe.IsAliveUnity(ZedZone.Net.NetContainerSync.instance) &&
                        Il2CppSafe.IsAliveUnity(ZedZone.Net.NetContainerSync.instance.m_activeLocalObject))
                    {
                        var activeObj = ZedZone.Net.NetContainerSync.instance.m_activeLocalObject;
                        var activeInv = ResolveOrInitTerrainObjectInventory(activeObj, allowEnsureLoot: false);
                        if (Il2CppSafe.IsAlive(activeInv) && activeInv.Pointer == inv.Pointer)
                            tobj = activeObj;
                    }

                    if (!Il2CppSafe.IsAliveUnity(tobj))
                        tobj = FindTerrainObjectByInventory(inv);
                }

                if (Il2CppSafe.IsAliveUnity(tobj))
                {
                    int z = Il2CppSafe.IsAlive(tobj.objectData) ? tobj.objectData.zLayer : 0;
                    int attrId = Il2CppSafe.IsAlive(tobj.objectData) ? tobj.objectData.objectAttrId : 0;
                    int vz = Il2CppSafe.IsAlive(tobj.objectData) ? tobj.objectData.standVz : 0;
                    Vector2 pos = tobj.transform.position;
                    int rx100 = Mathf.RoundToInt(pos.x * 100f);
                    int ry100 = Mathf.RoundToInt(pos.y * 100f);
                    string legacyGuid = Il2CppSafe.IsAlive(tobj.objectData) ? tobj.objectData.terrainObejctGUID : null;
                    string basePosKey = MultiplayerSyncSystem.BuildPositionBoxKey(pos, z);
                    string candidate = !string.IsNullOrEmpty(legacyGuid)
                        ? $"box_{rx100}_{ry100}_{z}_{attrId}_{vz}_{legacyGuid}"
                        : $"box_{rx100}_{ry100}_{z}_{attrId}_{vz}";

                    string uniqueKey = EnsureUniqueIdForInventory(candidate, inv);

                    if (Il2CppSafe.IsAlive(inv))
                    {
                        KnownInvToTerrainObj[inv.Pointer] = tobj;
                        KnownInvToWorldPos[inv.Pointer] = new Vector3(pos.x, pos.y, z);
                        inv.worldposition = pos;
                    }

                    MigrateLegacyBoxRule(legacyGuid, uniqueKey);
                    MigrateLegacyBoxRule(basePosKey, uniqueKey);
                    return uniqueKey;
                }

                if (Il2CppSafe.IsAlive(inv))
                {
                    if (KnownInvToWorldPos.TryGetValue(inv.Pointer, out var kPos) && ((Vector2)kPos).sqrMagnitude > 0.001f)
                    {
                        int rx100 = Mathf.RoundToInt(kPos.x * 100f);
                        int ry100 = Mathf.RoundToInt(kPos.y * 100f);
                        string posKey = $"itembox_{rx100}_{ry100}_{Mathf.RoundToInt(kPos.z)}";
                        return EnsureUniqueIdForInventory(posKey, inv);
                    }
                    if (!string.IsNullOrEmpty(inv.inventoryId) &&
                        !string.Equals(inv.inventoryId, "unknown_box", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(inv.inventoryId, "unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        return EnsureUniqueIdForInventory(inv.inventoryId, inv);
                    }
                    if (inv.worldposition.sqrMagnitude > 0.001f)
                    {
                        int rx100 = Mathf.RoundToInt(inv.worldposition.x * 100f);
                        int ry100 = Mathf.RoundToInt(inv.worldposition.y * 100f);
                        string posKey = $"pos_{rx100}_{ry100}_0";
                        return EnsureUniqueIdForInventory(posKey, inv);
                    }
                    string ptrKey = $"inv_{inv.Pointer.ToInt64():X}";
                    return EnsureUniqueIdForInventory(ptrKey, inv);
                }
            }
            catch { }

            return "inv_unassigned";
        }

        private static void MigrateLegacyBoxRule(string oldKey, string newKey)
        {
            if (string.IsNullOrEmpty(oldKey) || string.IsNullOrEmpty(newKey) ||
                string.Equals(oldKey, "unknown_box", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(oldKey, "unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
                return;
            if (CompanionConfigStore.ContainerRules.TryGetValue(oldKey, out var oldRule) &&
                (oldRule.Categories != StorageCategory.None || oldRule.IsDropoffSource))
            {
                if (!CompanionConfigStore.ContainerRules.TryGetValue(newKey, out var newRule) ||
                    (newRule.Categories == StorageCategory.None && !newRule.IsDropoffSource))
                {
                    var migrated = CompanionConfigStore.GetOrCreateRule(newKey, oldRule.CustomName);
                    migrated.Categories = oldRule.Categories;
                    migrated.IsDropoffSource = oldRule.IsDropoffSource;
                    migrated.Priority = oldRule.Priority;
                    migrated.LastKnownPos = oldRule.LastKnownPos;
                }
            }
        }

        public static InventoryData ResolveOrInitTerrainObjectInventory(TerrainObject tobj, bool allowEnsureLoot)
        {
            if (!Il2CppSafe.IsAliveUnity(tobj) || !Il2CppSafe.IsAlive(tobj.objectData)) return null;
            if (tobj.TryCast<TerrainObject_Corpse>() != null || tobj.TryCast<TerrainObject_CorpsePile>() != null)
                return null;

            try
            {
                var furn = tobj.TryCast<TerrainObject_Furniture_Inventory>();
                if (Il2CppSafe.IsAliveUnity(furn))
                {
                    var inv = tobj.objectData.inventoryData;
                    if (allowEnsureLoot && (!Il2CppSafe.IsAlive(inv) || inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0))
                    {
                        try { furn.EnsureLootGenerated(); } catch { }
                        inv = tobj.objectData.inventoryData;
                    }
                    return Il2CppSafe.IsAlive(inv) && inv.inventorySizeX > 0 && inv.inventorySizeY > 0 ? inv : null;
                }

                var fridge = tobj.TryCast<TerrainObject_Production_Fridge>();
                if (Il2CppSafe.IsAliveUnity(fridge))
                {
                    var inv = tobj.objectData.inventoryData;
                    if (allowEnsureLoot && (!Il2CppSafe.IsAlive(inv) || inv.inventorySizeX <= 0 || inv.inventorySizeY <= 0))
                    {
                        try { fridge.EnsureLootGeneratedNet(); } catch { }
                        inv = tobj.objectData.inventoryData;
                    }
                    return Il2CppSafe.IsAlive(inv) && inv.inventorySizeX > 0 && inv.inventorySizeY > 0 ? inv : null;
                }

                var prodInv = tobj.TryCast<TerrainObject_Production_Inventory>();
                if (Il2CppSafe.IsAliveUnity(prodInv) && Il2CppSafe.IsAlive(tobj.objectData.productionData))
                {
                    var inv = tobj.objectData.productionData.inventoryData1;
                    if (Il2CppSafe.IsAlive(inv) && inv.inventorySizeX > 0 && inv.inventorySizeY > 0)
                        return inv;
                }

                var directInv = tobj.objectData.inventoryData;
                if (Il2CppSafe.IsAlive(directInv) && directInv.inventorySizeX > 0 && directInv.inventorySizeY > 0)
                    return directInv;
            }
            catch { }
            return null;
        }

        public static List<TerrainObject> CollectWorldTerrainObjects(Vector2 center, float maxRadius)
        {
            var result = new List<TerrainObject>();
            var seen = new HashSet<IntPtr>();
            float maxSqr = maxRadius * maxRadius;

            void TryAdd(TerrainObject tobj)
            {
                if (!Il2CppSafe.IsAliveUnity(tobj) || !tobj.gameObject.activeInHierarchy || !Il2CppSafe.IsAlive(tobj.objectData))
                    return;
                if (((Vector2)tobj.transform.position - center).sqrMagnitude > maxSqr)
                    return;
                if (seen.Add(tobj.Pointer))
                    result.Add(tobj);
            }

            void ScanTransformChildren(Transform parent)
            {
                if (!Il2CppSafe.IsAliveUnity(parent)) return;
                int count = parent.childCount;
                for (int i = 0; i < count; i++)
                {
                    var tr = parent.GetChild(i);
                    if (!Il2CppSafe.IsAliveUnity(tr) || !tr.gameObject.activeInHierarchy) continue;
                    if (((Vector2)tr.position - center).sqrMagnitude > maxSqr) continue;
                    var tobj = tr.GetComponent<TerrainObject>();
                    if (Il2CppSafe.IsAliveUnity(tobj))
                        TryAdd(tobj);
                }
            }

            try
            {
                // 1. Active chunks in MapController.instance.currentChunkList
                var map = MapController.instance;
                if (Il2CppSafe.IsAliveUnity(map) && map.currentChunkList != null)
                {
                    for (int i = 0; i < map.currentChunkList.Count; i++)
                    {
                        var chunk = map.currentChunkList[i];
                        var cd = chunk != null ? chunk.chunkData : null;
                        if (!Il2CppSafe.IsAlive(cd)) continue;

                        if (cd.terrainObjectListTemp != null)
                        {
                            for (int j = 0; j < cd.terrainObjectListTemp.Count; j++)
                                TryAdd(cd.terrainObjectListTemp[j]);
                        }
                        ScanTransformChildren(cd.terrainObjectParent);
                    }
                }

                // 2. Active buildings & building floors in BasicBuilding.ActiveObjects
                var buildings = BasicBuilding.ActiveObjects;
                if (buildings != null)
                {
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        var b = buildings[i];
                        if (!Il2CppSafe.IsAliveUnity(b) || !b.gameObject.activeInHierarchy) continue;
                        ScanTransformChildren(b.terrainObjectParent);
                        ScanTransformChildren(b.terrainObjectParent_EnterBuildingDisable);

                        if (b.buildingFloors != null)
                        {
                            for (int f = 0; f < b.buildingFloors.Count; f++)
                            {
                                var floor = b.buildingFloors[f];
                                if (!Il2CppSafe.IsAliveUnity(floor) || !floor.gameObject.activeInHierarchy) continue;
                                if (floor.insideTerrainObjectList != null)
                                {
                                    for (int k = 0; k < floor.insideTerrainObjectList.Count; k++)
                                        TryAdd(floor.insideTerrainObjectList[k]);
                                }
                                ScanTransformChildren(floor.terrainObjectParent);
                            }
                        }
                    }
                }

                // 3. GameController.instance.terrainObjectsParent fallback
                ScanTransformChildren(GameController.instance?.terrainObjectsParent);
            }
            catch { }

            return result;
        }

        public static TerrainObject FindTerrainObjectByInventory(InventoryData inv)
        {
            if (!Il2CppSafe.IsAlive(inv)) return null;
            if (KnownInvToTerrainObj.TryGetValue(inv.Pointer, out var known) && Il2CppSafe.IsAliveUnity(known))
                return known;

            try
            {
                Vector2 center = Il2CppSafe.IsAliveUnity(GameController.instance?.playerCharacter)
                    ? (Vector2)GameController.instance.playerCharacter.transform.position
                    : inv.worldposition;
                var candidates = CollectWorldTerrainObjects(center, Mathf.Max(80f, CompanionConfigStore.BaseScanRadius));
                for (int i = 0; i < candidates.Count; i++)
                {
                    var tobj = candidates[i];
                    var objInv = ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: false);
                    if (Il2CppSafe.IsAlive(objInv) && objInv.Pointer == inv.Pointer)
                    {
                        RegisterInventoryTerrainObject(inv, tobj);
                        return tobj;
                    }
                }
            }
            catch { }
            return null;
        }

        public static TerrainObject FindTerrainObjectNearPosition(Vector2 pos, float maxDist = 1.5f)
        {
            try
            {
                float bestSqr = maxDist * maxDist;
                TerrainObject best = null;
                var candidates = CollectWorldTerrainObjects(pos, maxDist + 2f);
                for (int i = 0; i < candidates.Count; i++)
                {
                    var tobj = candidates[i];
                    float sqr = ((Vector2)tobj.transform.position - pos).sqrMagnitude;
                    if (sqr > bestSqr) continue;
                    var inv = ResolveOrInitTerrainObjectInventory(tobj, allowEnsureLoot: true);
                    if (!Il2CppSafe.IsAlive(inv)) continue;
                    bestSqr = sqr;
                    best = tobj;
                }
                return best;
            }
            catch { return null; }
        }

        public static TerrainObject FindTerrainObjectForRule(ContainerRuleProfile rule)
        {
            if (rule == null) return null;
            for (int i = 0; i < CachedContainers.Count; i++)
            {
                var c = CachedContainers[i];
                if (c != null && string.Equals(c.ContainerId, rule.ContainerId, StringComparison.OrdinalIgnoreCase) &&
                    Il2CppSafe.IsAliveUnity(c.TerrainObj))
                    return c.TerrainObj;
            }
            if (rule.LastKnownPos.sqrMagnitude > 0.01f)
                return FindTerrainObjectNearPosition(rule.LastKnownPos, 1.5f);
            return null;
        }
    }
}
