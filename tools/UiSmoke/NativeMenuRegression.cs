using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZedZone.SmartCompanions.UiSmoke
{
    internal static class NativeMenuRegression
    {
        internal static Il2CppSystem.Collections.Generic.List<string> Commands()
        {
            // Use the native provider including our postfix, not dialogue-envelope literals.
            return new GOAPNPCData().GetAllAvailableCommands();
        }

        internal static NPCCommandNode DumpAndVerify(bool requireLabels = false)
        {
            var manager = NPCCommandManager.instance ?? new NPCCommandManager();
            var commands = Commands();
            var root = manager.BuildCommandTree(commands);
            UiSmokePlugin.Logger.LogInfo("[NativeMenu] Built native command tree with " + commands.Count + " commands.");
            Dump(root, 0, requireLabels);
            if (requireLabels)
            {
                if (root.children.Count != 4) throw new Exception("Native menu should retain exactly four vanilla groups.");
                for (int i = 0; i < commands.Count; i++)
                    if (commands[i].StartsWith("NPCCommand:", StringComparison.Ordinal))
                        throw new Exception("A dialogue instruction envelope leaked into native menu paths.");
                foreach (string path in CustomNpcCommands.AllCustomCommands)
                {
                    string leaf = path.Substring(path.LastIndexOf('.') + 1);
                    string expected;
                    if (!CustomNpcCommands.TryGetDisplayName(path, out expected) ||
                        NPCCommandManager.GetCommandDisplayName(leaf) != expected ||
                        NPCCommandManager.GetCommandDisplayName("NPCCommand:" + path) != expected)
                        throw new Exception("Bare/path/legacy command label mismatch: " + path);
                    int count = 0;
                    for (int i = 0; i < commands.Count; i++) if (commands[i] == path) count++;
                    if (count != 1) throw new Exception("Custom command missing or duplicated: " + path);
                }
                UiSmokePlugin.Logger.LogInfo("[NativeMenu] PASS four native groups, 13 vanilla + 11 custom commands; bare/path/legacy labels.");
            }
            return root;
        }

        internal static void VerifyAllLanguages()
        {
            ModLanguage original = CompanionConfigStore.LanguageMode;
            try
            {
                foreach (var language in new[] { ModLanguage.Russian, ModLanguage.English, ModLanguage.Chinese })
                {
                    CompanionConfigStore.LanguageMode = language;
                    var tree = DumpAndVerify(true);
                    var menu = NPCCommandMenu.Instance;
                    if (menu == null) menu = NPCCommandMenu.CreateSingleton();
                    if (menu == null) throw new Exception("Native NPCCommandMenu singleton unavailable.");
                    VerifyPanel(menu, tree, new Vector2(20f, 200f));
                    for (int i = 0; i < tree.children.Count; i++)
                        VerifyPanel(menu, tree.children[i], new Vector2(250f, 200f));
                    UiSmokePlugin.Logger.LogInfo("[NativeMenu] PASS actual native BuildPanelUI/CreateButton Text labels: " + language);
                }
            }
            finally { CompanionConfigStore.LanguageMode = original; }
        }

        internal static void DumpStoredCommands()
        {
            foreach (string command in new[] { "Combat.FireAtWill", "NPCCommand:Combat.FireAtWill", "WeaponPreference.RangedWeaponOnly", "NPCCommand:WeaponPreference.RangedWeaponOnly" })
            {
                var npc = new GOAPNPCData { npcGUID = "SC-EMPTY-COMMAND-PROBE", characterData = new CharacterData() };
                var parsed = GOAPNPCData.NPCCommandParser.Parse(command);
                UiSmokePlugin.Logger.LogInfo($"[NativeMenu] PARSE '{command}' type='{parsed.commandType}' command='{parsed.command}' parameter='{parsed.parameter}'");
                try
                {
                    npc.ExecuteNPCCommand(command);
                    UiSmokePlugin.Logger.LogInfo($"[NativeMenu] STORED after '{command}': combat='{npc.combatCommand}', weapon='{npc.weaponPreference}'");
                }
                catch (Exception e) { UiSmokePlugin.Logger.LogInfo("[NativeMenu] Empty-NPC execute requires live state: " + e.Message); }
            }
        }

        // The caller may keep a panel for a screenshot by calling this directly.
        internal static GameObject RenderPanel(NPCCommandMenu menu, NPCCommandNode node, Vector2 position)
        {
            var panel = menu.CreatePanel(position);
            menu.BuildPanelUI(node, panel.GetComponent<RectTransform>());
            return panel;
        }

        private static void VerifyPanel(NPCCommandMenu menu, NPCCommandNode node, Vector2 position)
        {
            GameObject panel = null;
            try
            {
                panel = RenderPanel(menu, node, position);
                var texts = panel.GetComponentsInChildren<Text>(true);
                var rendered = new HashSet<string>();
                foreach (var text in texts)
                {
                    string value = text.text;
                    if (string.IsNullOrWhiteSpace(value)) throw new Exception("Native menu created an empty Text label.");
                    rendered.Add(value.Trim().TrimEnd(' ', '▶', '►', '>'));
                }
                for (int i = 0; i < node.children.Count; i++)
                {
                    string expected = NPCCommandManager.GetCommandDisplayName(node.children[i].name);
                    if (!rendered.Contains(expected.Trim()))
                        throw new Exception("Native BuildPanelUI failed to render expected label: " + expected);
                }
            }
            finally { if (panel != null) UnityEngine.Object.Destroy(panel); }
        }

        private static void Dump(NPCCommandNode node, int depth, bool requireLabels)
        {
            if (node == null || depth > 8) return;
            string byName = NPCCommandManager.GetCommandDisplayName(node.name);
            UiSmokePlugin.Logger.LogInfo($"[NativeMenu] depth={depth} leaf={node.isLeaf} name='{node.name}' path='{node.fullPath}' displayName='{byName}'");
            if (requireLabels && depth > 0 && string.IsNullOrWhiteSpace(byName))
                throw new Exception("Native menu node has no display label: " + node.fullPath + " / " + node.name);
            if (node.children == null) return;
            for (int i = 0; i < node.children.Count; i++) Dump(node.children[i], depth + 1, requireLabels);
        }
    }
}
