# 2.5.0 verification — PASS (live field tactics, GrenadeSafetyGate & UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `9527234181900a03a26ee0c4db28f5e8ea4262ccf1558891f0b0fa788f436c70`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.5.0` (20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261113-031142`. Graphical matrix: `backups/ui250-20261113-031249` (**300/300** uninterrupted, zero `[UiSmoke] FAIL`).

## Development history (not approval evidence)

Candidate `4ef74d86…` passed live acceptance, and screenshot review of `case-000` caught that the Russian tactical-bar button `Строй:Свободно` wrapped onto two lines inside a 1/8th button. Shortened to the concise formation name (`Свободно / Клин / Шеренга / Колонна / Круг` with hover tooltip); rebuilt and re-verified from scratch on `95272341…`.

## Scope of the change

- **Move-to-Cursor fan order (`FieldTacticsSystem.IssueMoveToCursorOrder`):** `Alt+G`, `F8 → Squad → To Spot` and `Basic.MoveToCursorPoint` send one fighter or the full squad in a non-overlapping fan (`>= 1.25 m` pairwise spacing) to the cursor spot via `GOAPAction_TeamOrder_MoveTo`.
- **Smart ranged cover (`TryTriggerCoverUnderRangedFire`):** `Balanced`, `Bodyguard` and `Sniper/Kite` companions trigger `GOAPAction_TakeCover` under enemy gunfire with a `6.5 s` cooldown.
- **Offensive grenades with `GrenadeSafetyGate` (`CommandSafeOffensiveGrenade`):** requires `>= 3` hostiles within `4.5 m` (or a boss) and strictly blocks throws within `6.5 m` of any player, ally or pet OR within `18.0 m` of any marked base chest.
- **Tactical healing smoke & Medic rescue (`CommandTacticalHealingSmoke`, `TryMedicRescueWoundedTeammate`):** `Alt+H` / `Smoke` deploys `GOAPAction_TeamOrder_ThrowHealingSmoke` (with field-bandage fallback), while Medics autonomously rescue critically wounded teammates (`< 32% HP`) via `GOAPAction_RescueAlly`.

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261113-031142`): ranged combat, physical loot & chest carry (0 teleports), Hold Position & Fall Back Here, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover, and live `GrenadeSafetyGate` friendly-fire block (`TooCloseToFriendly` verified at player position) — PASS.
- Graphical & deterministic (`backups/ui250-20261113-031249`): **300/300** actual IMGUI repaint cases + `FieldTacticsRegression` + `GarrisonRegression` (8/8) + `ScaleAndPrivacyRegression` (14/14) + `SpecializationRegression` (12/12) + `TacticalRegression` (16/16) + `ProtocolRegression` (13/13). Screenshots inspected across RU/EN/ZH: zero clipping, zero wrapping, zero missing glyphs.
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical; all installs, configs and screen prefs restored.

## Remaining limits

A live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.
