using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class ContainerFixtures
    {
        internal static readonly List<DiscoveredContainer> Containers = new List<DiscoveredContainer>();
        private static readonly List<string> SavedSpeech = new List<string>();
        private static GameObject _object;
        private static LootInventoryPanel _panel, _savedPanel;
        private static InventoryData _inventory;
        internal const string ObjectName = "SmartCompanions_UI_SMOKE_Chest";
        internal static bool Active;

        internal static void Create()
        {
            SavedSpeech.AddRange(SmartChatterSystem.RecentSpeechLog);
            SmartChatterSystem.RecentSpeechLog.Clear();
            for (int i = 0; i < 8; i++)
                SmartChatterSystem.RecentSpeechLog.Add($"[12:{i:00}] Smoke companion: Проверка длинной реплики — напарник идёт к ящику, переносит припасы и возвращается на базу.\nLong message wrapping: companions carry supplies on foot and leave unreachable containers alone. 测试长句换行：队友会步行搬运补给并避开无法到达的箱子。");
            _inventory = new InventoryData { inventoryId = "SC-UI-SMOKE-CHEST", inventoryTitleName = "Склад / Storage / 仓库", isPlayerInventory = false };
            string id = BaseSortingSystem.GetContainerStableId(_inventory);
            var rule = CompanionConfigStore.GetOrCreateRule(id, _inventory.inventoryTitleName);
            rule.IsMarkedByPlayer = true;
            rule.Categories = StorageCategory.Medicine | StorageCategory.Materials;
            Containers.Add(new DiscoveredContainer { ContainerId = id, DisplayName = _inventory.inventoryTitleName,
                Inventory = _inventory, Rule = rule, WorldPos = new Vector2(120f, 85f), DistanceToPlayer = 2.5f, ItemCount = 12 });
            _savedPanel = LootInventoryPanel._instance;
            _object = new GameObject(ObjectName);
            UnityEngine.Object.DontDestroyOnLoad(_object);
            _object.SetActive(false);
            _panel = _object.AddComponent<LootInventoryPanel>();
            _panel.enabled = false;
            _panel.inventoryData = _inventory;
            _panel.lootObjectName = _inventory.inventoryTitleName;
            _panel.inventoryDataLinkedList = new Il2CppSystem.Collections.Generic.LinkedList<InventoryData>();
            _panel.inventoryDataLinkedList.AddLast(_inventory);
            LootInventoryPanel._instance = _panel;
            UiSmokePlugin.Logger.LogInfo("[UiSmoke] Inert chest editor, native overlay panel and multiline chatter fixtures ready.");
        }

        internal static void SetVisible(bool visible, bool collapsed)
        {
            Active = visible;
            SmartCompanionsUI.CollapseChestBar = collapsed;
            _object.SetActive(visible);
            if (visible && (!Il2CppSafe.IsAlive(LootInventoryPanel.CurrentOpenInventoryData) ||
                LootInventoryPanel.CurrentOpenInventoryData.Pointer != _inventory.Pointer))
                throw new Exception("Native open inventory getter did not return chest fixture.");
        }

        internal static void Cleanup()
        {
            Active = false;
            if (_panel != null && LootInventoryPanel._instance == _panel) LootInventoryPanel._instance = _savedPanel;
            if (_object != null) UnityEngine.Object.Destroy(_object);
            foreach (var container in Containers) CompanionConfigStore.ContainerRules.Remove(container.ContainerId);
            Containers.Clear();
            SmartChatterSystem.RecentSpeechLog.Clear();
            SmartChatterSystem.RecentSpeechLog.AddRange(SavedSpeech);
            SavedSpeech.Clear();
        }
    }

    [HarmonyPatch(typeof(InventoryPanel), nameof(InventoryPanel.Awake))]
    internal static class SuppressFixtureInventoryAwake
    {
        private static bool Prefix(InventoryPanel __instance) => __instance.gameObject.name != ContainerFixtures.ObjectName;
    }

    [HarmonyPatch(typeof(BaseSortingSystem), nameof(BaseSortingSystem.GetFilteredContainersForUI))]
    internal static class FixtureContainers
    {
        private static bool Prefix(ref List<DiscoveredContainer> __result)
        {
            __result = ContainerFixtures.Containers;
            return false;
        }
    }
}
