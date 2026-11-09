# ZED ZONE: Smart Companions — Tactical AI, Base Sorting & Live Chatter

<p align="center"><a href="https://boosty.to/lrgpp"><img src="https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png" alt="LrgPP mods on Boosty" width="640"></a></p>
<p align="center"><em>Enjoy the mod? Support its development on <a href="https://boosty.to/lrgpp">Boosty</a>.</em></p>

<p align="center"><img src="docs/preview.png" alt="ZED ZONE: Smart Companions cover" width="420"></p>

<p align="center">
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312"><strong>Steam Workshop (3836613312)</strong></a> ·
  <a href="https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.0.0"><strong>Release v1.0.0 ZIP</strong></a> ·
  <a href="README_RU.md"><strong>Русская документация (README_RU.md)</strong></a>
</p>

---

**ZED ZONE: Smart Companions** is a BepInEx 6 (IL2CPP) code mod for **ZED ZONE** (`Unity 2023.1.18f1 x64`) that overhauls recruited survivor companions with tactical combat intelligence against both zombies and hostile human NPCs, an automated base chest sorting system with a 12-category container UI, 44 contextual speech topics (**171 Russian / 92 English lines**), 6 real-time combat doctrines, and emergency field support.

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

1. Subscribe to **[ZED ZONE Code Mod Loader (BepInEx IL2CPP)](https://steamcommunity.com/sharedfiles/filedetails/?id=3836614258)** ([Workshop ID `3834614258`](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)) and run its `install.cmd` once.
2. Subscribe to **[ZED ZONE: Smart Companions](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)** on Steam Workshop (or extract `ZedZoneSmartCompanions-1.0.0.zip` into `<ZED ZONE>\Mods\SmartCompanions\`).
3. Launch **ZED ZONE** through Steam. `ZedModsLoader` automatically loads `SmartCompanions.dll`.
4. Press **`F8`** in-game to open the **Smart Companions & Base Logistics Hub**, or **`F7`** to issue a quick Base Sort order.

## Building from Source

```powershell
dotnet build src/SmartCompanions/SmartCompanions.csproj -c Release
```

## My mods

- [SmoothRide MP](https://steamcommunity.com/sharedfiles/filedetails/?id=3827367565)
- [ZombieSync MP](https://steamcommunity.com/sharedfiles/filedetails/?id=3827946115)
- [Tripping Zombies MP Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3829034899)
- [Wandering Hordes Performance Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3829140377)
- [Download Unstuck](https://steamcommunity.com/sharedfiles/filedetails/?id=3829163351)
- [A-Life: Reactive Survivors](https://steamcommunity.com/sharedfiles/filedetails/?id=3829332775)
- [A-Life: Caravans](https://steamcommunity.com/sharedfiles/filedetails/?id=3829571361)
- [ZED ZONE: Smart Companions](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)

© 2026 druidqwerty. Released under the MIT License. Free and open source; AI-assisted code, direction and testing by druid.
