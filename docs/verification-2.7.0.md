# 2.7.0 verification — PASS (live industrial fuel whitelist, station routing & UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `2c15a6ecfcbb3d12370412e075b802424e6d72ed090984b8f1f537c0ed2eeb84`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.7.0` (20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261113-132706`. Graphical matrix: `backups/ui270-20261113-132922` (**300/300** uninterrupted, zero `[UiSmoke] FAIL`).

## Scope of the change

- **Zero-teleport power grid & heater maintenance (`BaseIndustrySystem`):** workers (`Any`, `Camp` or `Sorter`) physically walk (`<= 2.45 m`) between base chests and `StirlingGenerator`, `Heater`, `BatteryCharger` and `EnergyStorage` stations to supply approved fuel and rotate charged batteries via `CarryBuffer`.
- **Furnace & camp kitchen unloading:** collects finished ingots, charcoal and cooked meals from `Furnace`, `ElectricFurnace`, `Stove` and `CookingHob` output inventories into categorized base chests.
- **Strict fuel whitelist (`IsApprovedStationFuelItem`):** blocks weapons, armor, clothing, medicine, food, books, cash, planks, batteries and rare vehicle parts from ever entering fuel slots.
- **Work tab toggle (`F8 → 02 WORK`):** `[x] Power & Kilns` (`EnableBaseIndustry`) persisted in `CompanionConfigStore`.

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261113-132706`): ranged combat, physical loot & chest carry (0 teleports), Hold Position & Fall Back Here, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover, `GrenadeSafetyGate`, SavedMask protection, and live `IsApprovedStationFuelItem` checks — PASS.
- Graphical & deterministic (`backups/ui270-20261113-132922`): **300/300** actual IMGUI repaint cases + `IndustryRegression` + `SurvivalRegression` + `FieldTacticsRegression` + `GarrisonRegression` + `ScaleAndPrivacyRegression` + `SpecializationRegression` + `TacticalRegression` + `ProtocolRegression`. Screenshots inspected across RU/EN/ZH: zero clipping, zero wrapping, zero missing glyphs.
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical; all installs, configs and screen prefs restored.

## Remaining limits

A live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.
