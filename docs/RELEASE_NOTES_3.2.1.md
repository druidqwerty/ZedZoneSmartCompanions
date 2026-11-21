# ZED ZONE: Smart Companions 3.2.1 — Fever / Cold Companion Freeze Hotfix

## What's Fixed

- **Companions No Longer Freeze When Sick With Fever/Cold (`High Fever` / `Low Fever`):**
  - Fixed the vanilla `GOAPGoal_ClearDebuff` (`priority = 150`) + `GOAPAction_ClearDebuff_Fever` (`giveUpSeconds = 120s`) freeze loop where sick mercenaries stood completely still for 2 minutes ignoring enemies, aiming/shooting, and player commands until manually given fever-reducing medication.
  - Fever debuff care now yields immediately to combat threats (`CombatResponse`, `GOAPGoal_ClearDebuff.GetPriority`) and any player command (`ExecuteNPCCommand`, tactical orders, base tasks, vehicles).
  - Out of combat, if a companion has no water left (or finishes quick gulps) and is not standing next to a heat source, the action exits immediately with native `feverSuppressedUntil` instead of stalling for 120 seconds.
- **Autonomous Fever Reducer Cure Across All Bags:**
  - `CompanionSurvivalSystem` now detects `High Fever (7)` and `Low Fever (14)` debuffs directly, searches personal, shared, backpack, and nested pouches (`depth <= 3`) for **Fever Reducer (`234`)**, applies its native `AddFoodBuffs` effect, consumes the item, normalizes body temperature, and clears the fever buffs automatically.
- **Includes All `3.2.0` Features:** automatic NPC equipment & inventory repair across all 18 equipped slots and nested bags (`4` live modes in `F8 → Settings`) plus full ZED ZONE Patch `1.0.20` support.

## Verification

- Verified on ZED ZONE **Patch `1.0.20`** (`buildid 26773715`): `run_live_acceptance.py` (`V312Live (f)` PASS, `0` exceptions in `Player.log`), `run_ui_smoke.py` (`13/13` suites, `303` screenshots, `SurvivalRegression #12` PASS), and `8,386 / 8,386` `GameData` files byte-identical.
- Production DLL SHA-256: `36b8136732292e3e0f1b81e4f89c7df65681dd2d76d071ca2b2eebcc60d1f1f1` (`Assembly-CSharp AssemblyRef = 0.0.0.0`).

---

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
