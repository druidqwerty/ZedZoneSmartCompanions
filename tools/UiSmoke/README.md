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
`--sc-ui-smoke-output=C:\absolute\output`. Do not use `-nographics`.

The probe draws the production DrawAllGUI wrapper in the main menu,
temporarily assigning a disabled fixture player only during each draw,
captures each case after three real IMGUI repaints, and exits automatically.
Cases cover every tab in Russian, English and Chinese, plus the no-companion
empty state and a six-entry scrolled squad in each language: 15 cases total. The populated squad fixture uses
three disabled native GOAPAgent objects with minimal CharacterData and status.
They are not registered in the game world and never run AI.

`[UiSmoke] PASS 15/15` and fifteen non-empty screenshots are required.
Review the screenshots and the full BepInEx log; drawing without throwing is
not sufficient evidence of readable glyphs. The probe tests public F8
input-block flags and the three actual native CharacterInput gating getters
on a disabled component. InputRegression also invokes the real native attack
and input detours with harmless final kill-switches, checking player isolation,
NPC independence, movement reset, close-frame quarantine and restored controls.
It does not perform damage tests against a loaded world. Those checks must be
reported separately. Fixture constructor failures are explicit test failures, never
silently downgraded to a passing empty-squad test.

Remove the temporary plugin after verification. Do not include it in a release
ZIP or Workshop contents.
