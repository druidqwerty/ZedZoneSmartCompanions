# 1.8.1 verification — PASS (scoped UI acceptance)

RELEASE_READY: true

## Candidate identity and build

- Production DLL SHA-256: `e312809b91618ba14b824835361b5c64969de36128ba39cb1e96ac73237916af`.
- Source: commit `08b6634` (`5d2b2c8` feature + label-width fix).
- Release build with warnings treated as errors: **0 warnings, 0 errors**.
- Real engine startup for this candidate: **57** Harmony-patched methods; `[SelfVerify] PASS v1.8.1`.
- Final matrix: **270/270** unique cases in one uninterrupted run, `backups/ui181-20261112-033113`, zero `[UiSmoke] FAIL` lines.

## Development history (not approval evidence)

The first candidate (`efd98e99…`) passed 270/270 in two segments (`ui181-20261112-031938` cases 0–174, then `ui181-20261112-032826` cases 175–269 after the test window stalled at case 175 and was stopped with a clean rollback). Screenshot review found the English **AWAITING ORDERS** label wrapped onto two lines. That candidate was rejected; the status label is now measured and drawn without wrapping. The full matrix was repeated on the final DLL above.

## Scope of the change

- Squad cards show a coloured state (Awaiting orders / Busy / In combat / Stopped / Needs help) and the reason work stopped.
- Squad filters All / Busy / Idle / Need help with a shown/total counter.
- Hover hints (IL2CPP-safe three-argument `GUIContent`) for doctrines, per-companion toggles, filters and counters, drawn inside the hub window bounds.
- Gameplay code only records why a job stopped (`NoteBlocked` on path failure, combat interruption, missing chests or space). It does not change any decision, movement or item transfer.

## Final graphical test matrix — 270/270

Real graphical Unity backbuffer; each profile requests the actual resolution and fails if the engine cannot set it. 27 cases per profile: RU/EN/ZH × nine variants (four tabs, empty squad, long scrolled squad, expanded/collapsed HUD with chest overlay, settings bottom).

| Profile | Requested scale | Cases | Result |
|---|---|---:|---|
| 1280×720 | Auto | 27 | PASS |
| 1920×1080 | Auto | 27 | PASS |
| 2560×1440 | Auto | 27 | PASS |
| 3840×2160 | Auto | 27 | PASS |
| 3440×1440 | Auto | 27 | PASS |
| 1024×768 | Auto | 27 | PASS |
| 1920×1080 | 75% | 27 | PASS |
| 1920×1080 | 200% (fit cap) | 27 | PASS |
| 1280×720 | 75% | 27 | PASS |
| 1280×720 | 200% (fit cap) | 27 | PASS |

Native input gating, attack isolation, modal movement reset and coordinate roundtrips passed in the same run. Screenshots of the filter row and state labels were inspected in Russian, English and Chinese: no wrapping, clipping or overlap.

## Lab isolation and restoration

All cases use disabled native fixtures in the main menu; no save is loaded and no fixture AI runs. Every run (including the rejected candidate's two segments) confirmed **8,386/8,386** `GameData` files SHA-256 identical, original installation, configuration, logs and screen preferences restored, and the temporary test plugin removed.

## Remaining limits

Focused interface regression, not a loaded-world gameplay soak. The block-reason recording is passive and was not exercised in a loaded world. Mouse-wheel injection and a real two-player host + client session remain unverified.
