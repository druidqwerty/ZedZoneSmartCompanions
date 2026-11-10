# ZED ZONE: Smart Companions — Tactical AI, Base Sorting & Live Chatter

<p align="center"><a href="https://boosty.to/lrgpp"><img src="https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png" alt="LrgPP mods on Boosty" width="640"></a></p>
<p align="center"><em>Enjoy the mod? Support its development on <a href="https://boosty.to/lrgpp">Boosty</a>.</em></p>

<p align="center"><img src="docs/preview.png" alt="ZED ZONE: Smart Companions cover" width="420"></p>

<p align="center">
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312"><strong>Steam Workshop (3836613312)</strong></a> ·
  <a href="https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.4.2"><strong>Release v1.4.2 ZIP</strong></a> ·
  <a href="README_RU.md"><strong>Русская документация (README_RU.md)</strong></a> ·
  <a href="README_CN.md"><strong>简体中文文档 (README_CN.md)</strong></a>
</p>

---

## New in v1.4.2 — Fixed Item Classification, Explicit Base Box Selection & Zero-Overflow Sorting

- **Fixed `ItemCategorizer.HasFeature` Classifying Every Item as `[Medicine]` (`0x13A9D30`):**
  - Native disassembly of `ItemAttr.GetItemFeatureByFeatureType(ItemFeatureType)` (`0x13A9D30`) showed that when an item lacks a feature in `itemFeatureDataDic`, it falls back to `ItemManager.instance.GetItemFeatureByFeatureType` (non-null for all features), which caused `HasFeature(attr, ItemFeatureType.Bandage)` to classify every item in the game as `StorageCategory.Medicine`! Fixed to check only `attr.itemFeatures.Contains(ft)` and `attr.itemFeatureDataDic.ContainsKey(ft)` so weapons, ammo, materials, tools, clothes, food, and medicine are classified accurately.
- **Sort Only Player-Marked Base Chests (`IsMarkedByPlayer`) & Smart 3-Mode F8 Filter:**
  - Companions now sort **only** between chests that you explicitly mark (`[*] Marked as Base Chest` — automatically enabled when you toggle any category or **Drop-off Box** on a chest, and removable in 1 click). Unmarked world containers are never touched.
  - Added a 3-mode filter in **F8** Tab 2 (default `Filter: Mine + Close ≤2m` showing only your marked base chests plus at most 3 unmarked chests right at your feet, cycling to `Nearby ≤7m` and `All 18m`), eliminating the clutter of distant world shelves/containers.
  - Automatically cleans up legacy `Medicine`-only auto-tags from `v1.4.1`.
- **Overflow-Free Compaction, Stack Merging & Complete Drop-Off Unloading:**
  - Replaced `InventoryData.AutoSorting(false, ...)` (which discarded overflow items when `executeDropFlag` was `false`) with `InventoryTransfers.SafeCompactInventory` (`TryAutoSortingWithoutOverflow`), which restores a layout snapshot if any item would overflow—guaranteeing zero item loss.
  - Stackable items (materials, ammo, meds) now merge onto existing partial stacks (`TryDepositOrStackWhole`), and `PickupBatchIntoCarryBuffer` tracks remaining free cells per destination chest so companions route overflow items to the next marked chest of that category. If items remain in a Drop-off box after sorting, the banner reports the exact count and which categories need a marked chest or free space. [Verification report](docs/verification-1.4.2.md).

## New in v1.4.1 — World Container Discovery, Per-Box Isolation & Physical Item Carrying

- **Full World Container Discovery Without Opening Chests (`CollectWorldTerrainObjects`):**
  - Scans all active chunks (`MapController.instance.currentChunkList`), buildings and building floors (`BasicBuilding.ActiveObjects`), and deployed ground container items (`BasicItem.ActiveObjects`), automatically initializing unopened furniture/fridge inventories (`EnsureLootGenerated`) so all nearby base chests appear immediately in **F8** without having to open each box first.
  - Pressing **F8** automatically selects the currently open chest or the closest chest right next to the player, and only reads `LootInventoryPanel.CurrentOpenInventoryData` when `LootInventoryPanel` is actively open.
- **Unique Per-Chest Identification (Fixed Shared Drop-Off / Category Bug):**
  - Replaced the fallback `"unknown_box"` key with a 1-to-1 `InventoryData <-> TerrainObject` registry (`box_{rx100}_{ry100}_{z}_{attrId}_{standVz}_{guid}`) and coordinate tags `[x,y]` in the **F8** list so setting **Drop-off Box** or categories on one chest never affects other chests.
- **Physical Companion Item Carrying with Invisible Carry Buffer (`CarryBuffer`):**
  - Companions no longer teleport items between chests: when ordered to sort (`F7` / **Order NPCs: Sort Base**), each companion physically walks to the source/drop-off chest (`GOAPAction_FollowLeader.Execute` redirected to the target chest), pulls a batch of up to 10 item stacks into a dedicated **invisible transfer buffer (`CarryBuffer`)** (separate from their main backpack/weapons), and physically walks to each matching destination chest to deposit and compact the items. If interrupted by combat or on game exit, `FlushCarryBufferSafely` returns all in-transit items so zero items can ever be lost. [Verification report](docs/verification-1.4.1.md).

## New in v1.4.0 — Co-op Multiplayer Synchronization & Hostile Human Combat Fix

- **Full Co-op Multiplayer Synchronization (`MultiplayerSyncSystem`):**
  - **Shared Base Container Categories:** when any player assigns or changes categories, drop-off mode, or priority on a chest, it syncs across Host and Clients via `TerrainObjectData` property slot `7` (`SC14:...`) and silent `MsgChat` sync packets (`\u200B[SC14]|BOX|...`) so both players see the exact same box categories in real time.
  - **Per-Player Companion Ownership & Cross-Player Field Aid:** each player commands their own companions (`leaderCharacterGUID` / owner badge in `F8`), companions follow their owner across Z-layers in co-op, and any companion can perform emergency field healing and rescue on injured allied players (`HEAL` packet sync).
- **Fixed Bug 1 (Co-op Fire-Lighting Menu):** opening a chest on a Client no longer triggers the fire-lighting (`ToolSelectManager.SelectTool_Player`) menu on the Host's screen (`BaseChoreSystem` now ignites fueled campfires directly via `TerrainObject_FirePlace.OnIgnite()` and guards `IgnitionFire(true)` while `LootInventoryPanel` is open).
- **Fixed Bug 2 (Co-op Invisible NPCs):** fixed `ScavengerCamp` human NPCs having `characterAttrId = 0`, which caused `NetMonsterSync.SpawnPuppet` (`0xBD0A37`) to abort on Clients, and fixed `BasicCharacterController.Start()` leaving client puppet `spriteParent` disabled (`OnEnterScreen()` / `OnEnterSight()`).
- **Fixed Bug 3 (Companions Not Shooting Hostile Human NPCs):** fixed native `GOAPAgent.UpdateAgentSight` (`0x679CC3`) treating Looters (`camp = 21`) as same-camp allies of companions (`camp = 1`) due to `(21 % 10) == (1 % 10)`, suppressed `GOAPGoal_WarningHumanEnemy` (`priority 100`) for player teammates, and configured human `MonsterAttr` (`201`/`202`) threat values so companions immediately aim and shoot hostile human NPCs just like zombies. [Verification report](docs/verification-1.4.0.md).

## Fixed in v1.3.2

- **Native NPC command menus:** restored the missing category/command labels in the game's own companion menu, separately from the F8 window fixed in 1.3.1. Existing vanilla commands remain available.
- **Threat response:** repaired stored combat/weapon orders and refresh threat perception every 0.3 seconds, bounded by weapon range and native visibility checks. The mod does not force shots through walls or override Hold Fire.
- **Less combat interference:** threats interrupt sorting; line-of-fire sidesteps require the player to aim; kiting interferes less with firing.
- **Loot and inventory fixes:** searches are centered on the companion, transfers move whole items only when the destination can accept them, and XP progress is saved between rank-ups with debounced writes and a final save on exit.
- **Rations:** manually sharing consumes one food item per served companion and has a separate 30-second cooldown for each companion; automatic sharing uses the meal the player already ate.
- No save reset or new chatter lines. Close the game once to install the update; in-game settings remain changeable without restarting. [Verification and limitations](docs/verification-1.3.2.md).


## Fixed in v1.3.1

- F8 companion cards, base containers and settings no longer go blank at the scrolling area.
- Russian and Simplified Chinese labels use a glyph-checked font instead of missing-character squares; unsupported emoji buttons use readable text.
- Clicking F8 or the mod's chest/HUD controls is isolated from world input, including the closing click.
- GUI errors retain their full stack trace without flooding the log.
- No save migration or reset required. See [verification and limitations](docs/verification-1.3.1.md).


**ZED ZONE: Smart Companions** is a BepInEx 6 (IL2CPP) code mod for **ZED ZONE** (`Unity 2023.1.18f1 x64`) that overhauls recruited survivor companions with tactical combat intelligence against both zombies and hostile human NPCs, an automated base chest sorting system with a 12-category container UI, 60 contextual speech topics (**1,179 unique lines across Russian, English, and Simplified Chinese: 565 RU / 290 EN / 324 ZH**), automatic + manual in-game language switching (`Auto / RU / EN / 中文`), 6 real-time combat doctrines, and emergency field support.

## New in v1.3.0

- **Full Simplified Chinese (`简体中文`) Localization + Auto-Detection & Manual Language Selector (`ModLanguage`):**
  - Automatically detects the active language from `GameController.instance.gameSettings.languageCode` (with fallback to `Application.systemLanguage`), or lets you switch manually in-game without restarting via the 4-mode language selector (`🌐 Auto`, `🇷🇺 Русский`, `🇬🇧 English`, `🇨🇳 简体中文`) in the **F8** window header and settings tab.
  - Translates all 12 storage categories, all **F8** UI tabs and buttons, the docked chest category bar, all 9 custom radial NPC commands, and all status banners into Simplified Chinese.
  - Adds **263 unique Chinese speech lines (`ChatterDatabaseZh.cs` + `ChatterDatabaseZhCombat.cs`)** across all 50 `ChatterTopic` values: **159 gritty 18+ post-apocalyptic Chinese lines (`HardcoreRude`)** and **104 clean Chinese lines (`Clean`)**, plus `Mixed` mode.

## Key Features

### 1. Smarter Combat AI vs. Zombies & Hostile NPCs (`SmartCombatAI`)
- **Dynamic Threat Prioritization (`GOAPAgent.GetThreatLevelByCharacter`):**
  - **Player Bodyguard Priority:** enemies closing within `3.6 m` of the player receive a `1.85×` threat boost (`2.65×` in *Bodyguard* doctrine) so companions immediately peel attackers off your back.
  - **Hostile Human Raiders & Shooters:** recognizes hostile `HumanCharacterController` enemies (`1.75×`–`2.35×` priority when armed with firearms), calls out shooter threats, and uses lateral strafing instead of standing still in gunfire.
  - **Boss & Giant Mutant Awareness:** identifies `GiantZombieController`, `CorpsewheelController`, `CarrionBossController`, and high-HP mutants (`FullHP >= 400`), keeping extra firing distance.
  - **Wounded Target Finisher Bonus:** prioritizes finishing off enemies below `35% HP` in weapon range.
- **Tactical Positioning & Kiting:**
  - **Anti-Friendly-Fire Corridor:** when the player aims a ranged weapon, companions inside a `1.05 m` corridor along your line of fire automatically sidestep perpendicular to the aim ray and call out that your firing line is clear.
  - **Backpedal Kiting:** when armed with a firearm or reloading and enemies close within `3.2 m` (`4.8 m` in *Sniper/Kite* doctrine), companions backpedal along a safe vector while keeping their aim on the threat.
- **Smart Weapon Switching & Magazine Management:**
  - Automatically swaps to the best melee weapon (`FindBestMeleeWeapon`) if an enemy reaches point-blank range (`< 1.95 m`) while the companion's firearm is empty, reloading, or jammed, then switches back to ranged once distance is restored.
  - Proactively reloads half-empty or empty magazines after combat ends.

### 2. Base Container Sorting & 12-Category Chest UI (`BaseSortingSystem` + `SmartCompanionsUI`)
- **Docked Container Category Bar:** opening any non-player chest, cabinet, safe, or fridge in `LootInventoryPanel` displays an interactive category bar at the top of the screen:
  1. `Melee Weapons`
  2. `Firearms & Parts`
  3. `Ammo & Magazines`
  4. `Medicine & First Aid`
  5. `Fresh Food & Meals`
  6. `Canned Food & Drinks`
  7. `Crafting Materials`
  8. `Tools & Repair`
  9. `Clothing & Backpacks`
  10. `Vehicle & Power`
  11. `Farming, Bait & Pets`
  12. `Explosives & Misc`
- **Drop-off Box Mode (`📥 Drop-off Box`):** mark an entrance chest as a drop-off source—dump your raid haul into it, order companions to sort (`F7`), and they will distribute every item into matching categorized chests across your base and run `InventoryData.AutoSorting` (`AutoSortMode.Dense`) to compact each grid.
- **Auto-Detect Categories from Contents:** 1-click scan that inspects existing items inside all chests within `48 m` and automatically assigns matching category masks.

### 3. 44 Context Chatter Topics — 171 RU & 92 EN Lines (`ChatterDatabase` + `SmartChatterSystem`)
- **Player Action Reactions (`CharacterActionManager` hooks):**
  - Eating steak or roasted meat (`ID 248` Spicy Meat Steak, `289` Grilled Salmon Steak, `291` Seared Tuna Steak, `241` Fried Meat, cooked `204` Meat, etc.) triggers signature lines including:
    > *«Вкусно тебе наверное? Да? Я вот стейк никогда не пробовал...»* / *"Must taste pretty good, huh? Yeah? I've actually never tried a real steak..."*
  - Distinct reactions to eating **raw/rotten meat**, **canned stew/SPUM/fruit**, **fish dishes**, **snacks/fruit**, **soda/water**, and **alcohol (lager)**.
  - Reactions when the player **bandages/heals**, **packs magazines**, **repairs weapons or vehicles**, **cooks on a campfire/BBQ**, or **picks locks / breaks doors**.
- **Combat, Base Sorting & Ambient Banter:**
  - Callouts for lone zombies, hordes (`5+` enemies), armed bandits, bosses, kiting, clearing your line of fire, weapon switching, kills, saving the player, low ammo, and wounds.
  - Live commentary when picking up and sorting weapons, food, medicine, and materials into base chests.
  - Weather and time-of-day reactions (rain, blood rain, nightfall, dawn, hunger, fatigue) plus **two-companion back-and-forth banter** when travelling with 2+ teammates.

### 4. 6 Tactical Doctrines, Field Medic, Ammo Supplier & Area Loot (`F8` Hub + Native Radial Menu)
- **6 Doctrines:** `Balanced`, `Assault`, `Bodyguard`, `Sniper/Kite`, `Stealth Melee`, and `Passive`.
- **Field Medic:** automatically bandages/heals the player when player HP drops below `48%` within `6.5 m` (32s cooldown).
- **Field Ammo Supplier:** hands over spare matching ammo when your firearm clicks empty in combat with `0` reserve rounds (45s cooldown).
- **Sweep Nearby Loot:** orders companions to collect loose ground items and loot from nearby zombie corpses into their shared inventory.
- **Native `NPCCommandMenu` Integration:** all custom orders and doctrines are also available directly inside the game's native right-side teammate command menu.

## Requirements & Installation

1. Subscribe to the required third-party dependency **[ZED ZONE Code Mod Loader (BepInEx IL2CPP)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)** ([Workshop ID `3834614258`](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)), which is not a mod by druidqwerty, and run its `install.cmd` once.
2. Subscribe to **[ZED ZONE: Smart Companions](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)** on Steam Workshop (or extract `ZedZoneSmartCompanions-1.4.2.zip` into the `<ZED ZONE>` game folder).
3. Launch **ZED ZONE** through Steam. `ZedModsLoader` automatically loads `SmartCompanions.dll`.
4. Press **`F8`** in-game to open the **Smart Companions & Base Logistics Hub**, or **`F7`** to issue a quick Base Sort order.

## Building from Source

```powershell
dotnet build src/SmartCompanions/SmartCompanions.csproj -c Release
```

© 2026 druidqwerty. Released under the MIT License. Free and open source; AI-assisted code, direction and testing by druid.
