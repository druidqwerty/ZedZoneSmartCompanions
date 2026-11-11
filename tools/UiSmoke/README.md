# Graphical UI regression harness (not a release component)

This separate BepInEx plugin is disabled unless the game is launched with
`--sc-ui-smoke`. It never loads a player save. It suppresses Smart Companions
background gameplay updates and JSON config writes during the probe.

Build after the main mod has been built:

```powershell
dotnet build tools/UiSmoke/UiSmoke.csproj -c Release --warnaserror
```

Temporarily install `bin/SmartCompanions.UiSmoke.dll` in the game's
`Mods/ZUiSmoke/` folder, alongside the normal Smart Companions installation.
Launch the graphical game from its game directory with `SteamAppId=1211600`
and `SteamGameId=1211600`, adding `--sc-ui-smoke` and optionally
`--sc-ui-smoke-output=C:\absolute\output`. Use a fresh output directory for
each run. Do not use `-nographics`. Preserve/restore `steam_appid.txt`, game
resolution preferences, loader logs, and candidate install hashes around the run.

Add `--sc-ui-matrix` to resize the real Unity backbuffer through 1280×720,
1920×1080, 2560×1440, 3840×2160, 3440×1440 and 1024×768 in Auto mode, then
1920×1080 and 1280×720 at manual 75% and 200% (fit-capped) in one run. If the
desktop/driver clamps a requested resolution, the run explicitly fails after
eight seconds with both requested and actual dimensions; it never silently
claims the larger size passed. Without this flag, only the launch resolution
is tested (use `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`).

The probe draws the production DrawAllGUI wrapper in the main menu,
temporarily assigning a disabled fixture player only during each draw,
captures each case after three real IMGUI repaints, and exits automatically.
Cases cover all four tabs in Russian, English and Chinese, plus the no-companion
empty state, a six-entry scrolled squad, expanded and collapsed HUD, and the
bottom of the settings scroll in each language: 27 cases per resolution,
270 for the full ten-profile matrix. The populated squad fixture uses
three disabled native GOAPAgent objects with minimal CharacterData and status.
They are not registered in the game world and never run AI. A managed discovered
container supplies the populated base editor, and eight long multilingual,
multiline speech entries exercise log wrapping and scrolling. The HUD cases
also draw expanded/collapsed chest category overlays using a disabled native
`LootInventoryPanel` fixture with its inventory assigned. Only that fixture's
`InventoryPanel.Awake` is suppressed; its native current-inventory getter is
asserted. No world container is opened or modified.

`[UiSmoke] PASS 27/27` (or `270/270` for the matrix) and the corresponding
number of non-empty screenshots are required. Every draw verifies the main
window/HUD is inside the logical viewport and the viewport transforms exactly
to the actual backbuffer. Physical-pixel pointer tests cover the center and
both corners of the interactive window plus a negative outside point. Each
long-squad/settings-bottom case must retain a positive scroll offset. Each
profile also checks native `GUIUtility` coordinate roundtrips and scaled
distances. Direct mutation of native `Event` objects proved unreliable in this
IL2CPP build and is not used or counted as input verification. The screenshot
matrix alone does not claim actual tab clicking, dragging or wheel scrolling.
Review the screenshots and the full BepInEx log; drawing without throwing is
not sufficient evidence of readable glyphs. The probe tests public F8
input-block flags and the three actual native CharacterInput gating getters
on a disabled component. InputRegression also invokes the real native attack
and input detours with harmless final kill-switches, checking player isolation,
NPC independence, movement reset, close-frame quarantine and restored controls.
It does not perform damage tests against a loaded world. Those checks must be
reported separately. Fixture constructor failures are explicit test failures, never
silently downgraded to a passing empty-squad test. A successful matrix verifies
the container overlay using inert native menu fixtures, not a loaded-world
base or real OS mouse clicks. The harness tests production pointer conversion
and native input gates without gameplay side effects.

Remove the temporary plugin after verification. Do not include it in a release
ZIP or Workshop contents.

## Separate Windows-message input probe

Launch with `--sc-ui-smoke --sc-ui-os-probe --sc-ui-scale=0.75` and
`--sc-ui-smoke-output=<fresh directory>`, without `--sc-ui-matrix`. Use 1920×1080
and repeat with `--sc-ui-scale=2`. `OsProbeRunner` holds inert fixtures open and
writes `state.json`; it exits on `finish.txt` or after 120 seconds.

Run `python tools/run_ui_os_probe.py --pid <exact game PID> --output <directory>`.
The driver posts Win32 mouse messages to that PID's window without moving the
global cursor or changing focus. It requires observable settings-tab switching,
scrolling, correctly scaled title dragging/release and closing. It writes
`os-probe-result.json` and screenshots. Message delivery alone is not success;
unsupported background messages fail without switching to global input injection.
If background wheel messages are ignored but the retained scrollbar arrow works,
the report explicitly marks `wheel_verified=false`; that is not a wheel-input pass.
The probe logs native wheel coordinates before DrawAllGUI and inside the production
scroll viewport to distinguish platform delivery issues from scaling errors.
This does not test F8/Escape hotkeys: controller Update is suppressed in this lab.
Use the normal rollback runner for installs, preferences and save verification.

## Harness implementation notes

Partial C# file order is not a safe initialization dependency: use `const` flags
and self-contained `typeof(...)` lookups or expression-bodied Type helpers.
Cross-file `static readonly` dependencies caused startup/probe failures and must
not be reintroduced. Compilation alone does not prove runtime initialization.

For safe installation/rollback use `python tools/run_ui_smoke.py`. It refuses
an existing game process, snapshots candidate install paths/config/logs/screen
preferences, hashes GameData, and restores originals in finally. After an
interrupted matrix, `--start-case=N` resumes at the first unverified case in a
fresh output directory. Audit unique case IDs across the evidence segments;
never count a partial run as a completed 270-case pass. Minimized Unity windows
can stop repainting even with background mode, so retain the actual evidence
and resume after inspecting process/window state instead of restarting completed cases.
