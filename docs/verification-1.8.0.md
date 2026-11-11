# 1.8.0 verification — PASS (scoped UI acceptance)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `0f1aa6c6663f53ac86116cf67b90befa33cd34377e76211a95213bd0f64b9462`.
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup observed for this candidate: **57** Harmony-patched methods; `[SelfVerify] PASS v1.8.0`.
- The preceding graphical matrix passed **270/270**, but used an earlier DLL. It is development evidence only and does **not** approve this final candidate.
- Final candidate: **270/270 unique cases**, completed as cases 0–243 in `backups/ui180-20261111-200813` and cases 244–269 in `backups/ui180-20261111-201617`. A minimized test window interrupted the first segment; the remaining cases were resumed, not counted twice. Combined log and JSON have no missing or duplicate IDs. Both segments have zero UI failures; 8 final RU/ZH screenshots were inspected, with corrected labels and no clipping found.

## Scope of the change

- Four F8 tabs: Squad, Base, Chatter and Settings; teal/neutral theme, larger text and grouped controls.
- Logical 960×660 hub and shared scaled coordinates for hub/HUD/chest controls.
- Auto scaling follows screen height (100–200% before fit cap); manual presets are 75%, 100%, 125%, 150%, 175%, 200%. The effective size is limited to available screen space.
- Live resolution/aspect/scale changes preserve relative window placement and keep windows in bounds. Viewport metadata supports saved-position migration.
- Scaled physical-pixel hit testing; main-window closure releases drag/scroll capture. The chest category overlay is hidden while the modal hub is open.
- Measured container rows accommodate wrapped text; the compact chest editor uses a four-column, three-row category grid with taller controls.
- No changes to combat, physical looting or item-transfer mechanisms from 1.7.0.

## Final graphical test matrix — 270/270

The final matrix uses the real graphical Unity backbuffer, not a browser mock-up or a headless rendering substitute. Each profile requests the actual resolution and fails if the engine cannot set it. Each profile contains **27** cases: three languages (RU/EN/ZH) × nine variants (four tabs, empty squad, long scrolled squad, expanded HUD/chest overlay, collapsed HUD/chest overlay, settings scrolled to the bottom).

| Profile | Requested scale | Expected effective scale | Cases | Final result |
|---|---|---|---:|---|
| 1280×720 | Auto | 100% | 27 | PASS |
| 1920×1080 | Auto | 100% | 27 | PASS |
| 2560×1440 | Auto | 133.33% | 27 | PASS |
| 3840×2160 | Auto | 200% | 27 | PASS |
| 3440×1440 | Auto | 133.33% | 27 | PASS |
| 1024×768 | Auto | 100% | 27 | PASS |
| 1920×1080 | 75% | 75% | 27 | PASS |
| 1920×1080 | 200% | 161.68% (fit cap) | 27 | PASS |
| 1280×720 | 75% | 75% | 27 | PASS |
| 1280×720 | 200% | 107.78% (fit cap) | 27 | PASS |

Assertions include logical viewport/backbuffer correspondence, window and overlay bounds, physical-pixel pointer hits and negative outside hits, native `GUIUtility` coordinate roundtrips, retained positive scroll offsets, and restoration of the caller's GUI matrix, enabled state and color state. Caught production UI failures count as failures. Screenshots must also be inspected for glyphs, wrapping and clipping; successful repaints alone are not readability proof.

## Input verification boundary

The inert native fixtures exercise the production modal and mouse-capture flags, actual native `CharacterInput` gating getters and native attack/input detours with harmless test-only kill switches. They verify player isolation, NPC independence, movement reset, close-frame quarantine and restored controls without dealing damage.

Separate exact-PID Windows `PostMessage` probes passed at **75%** and **200% requested / 161.68% effective**, both at actual 1920×1080: Settings-tab click, scrollbar-arrow scrolling, correctly scaled title dragging, release, and Close button. Evidence: `ui-input-1.8.0.json` and `ui-input-1.8.0.log`.

**Boundary:** background WM_MOUSEWHEEL injection did not scroll: the observed native event location was outside the scroll viewport (desktop/client coordinate mismatch). Wheel delivery is therefore **not verified** by this automation; no wheel PASS is claimed. Native mouse handling was not changed to compensate for a test-injection limitation. Direct mutation of native Event objects was also unreliable and is excluded from evidence. This is window-message input, not physical-device or loaded-world testing.

## Lab isolation and restoration — verified

- All graphical cases use disabled native player/companion/container fixtures in the main menu. No player save is loaded and no fixture AI runs.
- Test harness suppresses production background updates and configuration writes, and is excluded from release archives and Workshop contents.
- Both final matrix segments and both successful OS probes confirmed **8,386/8,386** original `GameData` files SHA-256 identical and restored original installations, configuration, loader logs and screen preferences.
- Temporary test plugins removed. Optional subsequent probe did not reach its ready state and was stopped with the same successful rollback; it adds no passing evidence.

## Remaining limits

This is a focused interface regression test, not a new loaded-world gameplay acceptance, damage test, long gameplay soak or co-op test. The six-phase physical-looting/combat acceptance remains historical 1.7.0 evidence, not a test repeated for 1.8.0. Real two-player host + client human NPC visibility remains unverified.

Release approved for this UI-only scope and exact DLL above. The stated wheel-injection and real co-op boundaries remain explicit. Packaging validates this approved hash.
