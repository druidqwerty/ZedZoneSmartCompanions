# 2.6.0 verification — PASS (live bio-masks, debuff care, sentry sleep & UI regression)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `08f0d48c42e5ca55420e697962225a1f36d3a1ab239de9ab8ba06b0ed78ed47e`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup: **57** Harmony-patched methods; `[SelfVerify] PASS v2.6.0` (20 custom NPC commands in RU/EN/ZH).
- Live gameplay acceptance: `backups/live-20261113-111220`. Graphical matrix: `backups/ui260-20261113-111528` (**300/300** uninterrupted, zero `[UiSmoke] FAIL`).

## Scope of the change

- **Automatic bio-protection masks (`CompanionSurvivalSystem`):** equips respirators/gas masks during blood rain (`gameData.isBloodRaining`) or elevated radiation (`> 3.5`), syncs visuals via `RefreshWornFaceMaskSprites` + `HostSendEquipment`, protects the displaced everyday mask in `SavedEverydayMaskByGuid` inside `BuildProtectedBackpackPointers` so base sorting never strips it, and restores it in clean air.
- **Debuff & environmental care (`TryTreatSurvivalDebuffs`):** invokes `GOAPAction_ClearDebuff_*` for hypothermia, burning, fever and spores while strictly gating rare serums behind `AllowSpendRareMeds && lifeThreatening`.
- **Sentry-guarded sleep rotation (`TryScheduleCampSleepShift`):** exhausted companions (`fatigue < 28%`) outside combat physically walk (`<= 2.45 m`, 0 teleport) to `TerrainObject_Furniture_Bed` (`StartSleepingNPC`), while always keeping at least one awake sentry on watch and waking everyone immediately (`ForceWakeupNPC`) if combat starts.

## Live gameplay & graphical acceptance — PASS

- Live game (`backups/live-20261113-111220`): ranged combat, physical loot & chest carry (0 teleports), Hold Position & Fall Back Here, reserves, 80-chest soak, Camp Garrison, SafeDismiss physical handover, `GrenadeSafetyGate`, and live `SavedEverydayMaskByGuid` backpack-protection check — PASS.
- Graphical & deterministic (`backups/ui260-20261113-111528`): **300/300** actual IMGUI repaint cases + `SurvivalRegression` + `FieldTacticsRegression` + `GarrisonRegression` + `ScaleAndPrivacyRegression` + `SpecializationRegression` + `TacticalRegression` + `ProtocolRegression`. Screenshots inspected across RU/EN/ZH: zero clipping, zero wrapping, zero missing glyphs.
- Lab isolation: **8,386/8,386** real `GameData` files SHA-256 identical; all installs, configs and screen prefs restored.

## Remaining limits

A live two-machine / two-account host + client session and automated mouse-wheel injection remain unverified.
