# ZED ZONE: Smart Companions 3.2.2 — Built-in Event Logging for Bug Reports

## What's New

- **Opt-in Event Log Recording (`F8 → 05 Settings`, OFF by default):**
  - New toggle **`Record mod events to log file`** (`EnableEventLogging`, persisted in `SmartCompanions_Rules.txt` & `SmartCompanionsConfig.json`, zero per-frame overhead when disabled).
  - Writes structured, timestamped events to **`BepInEx/config/SmartCompanions_EventLog.log`** (UTF-8 without BOM, buffered flush, automatic `4 MiB` rotation to `.prev.log`, forced flush on exit):
    - session header & active settings, world transitions (`WORLD`), per-companion state/GOAP goal/action/weapon/vehicle deltas (`NPC_STATE`),
    - automatic navigation stall detector (`[STALL]` when a companion with a navigation target or active task moves `< 0.35 m` over `4 s`),
    - hotkeys (`INPUT`), radial/custom NPC commands (`NPC_CMD`), tactical/dismiss/banner events, and all `WARN`/`ERROR` messages.
  - **Privacy-safe:** never writes OS user paths (`C:\Users\...`), Steam IDs, or raw character GUIDs/names (uses anonymized per-session aliases `NPC#01(xxxx)`).
- **One-Click `[!] Mark Bug` & Log Controls (EN / RU / ZH):**
  - **`[!] Mark Bug`** inserts a prominent timestamped `[BUG_MARK #N]` marker + immediate full world & squad snapshot and flushes to disk (even if continuous logging was OFF, dumping the 128-entry in-memory ring buffer of recent context).
  - **`Open Log Folder`** opens `BepInEx/config` in Explorer with `SmartCompanions_EventLog.log` selected; **`Clear Log`** resets the file for clean bug repros; **`[REC]`** indicator appears in the `F8` window title while recording.
- **Includes All `3.2.1` & `3.2.0` Updates:** Fever/Cold companion freeze fix, autonomous Fever Reducer cure across all bags, automatic NPC equipment & inventory repair across all 18 equipped slots and nested bags, and full ZED ZONE Patch `1.0.20` support.

## Verification

- Verified on ZED ZONE **Patch `1.0.20`** (`buildid 26773715`) on frozen `SmartCompanions.dll` (`SHA-256: 56b1db0b954cbd050b7529760b24f304553b77c14c20feb86fc72b348c7a96f8`, `Assembly-CSharp AssemblyRef = 0.0.0.0`, `92` Harmony patches `failed=0`):
  - `run_live_acceptance.py` (`backups/live-20261123-141810`): `exit 0`, `55.7s`, all live suites PASS including new **`V312Live (g)`** live `EventLogRecorder` in `Gaming` world (`WORLD`/`SNAP_NPC`/`BUG_MARK`, `privacy=clean`), `8,386/8,386` `GameData` files unchanged.
  - `run_ui_smoke.py`: `300/300` actual repaint cases (`303` PNGs), `13/13` suites PASS including `V312Regression (j)` (`EventLogRecorder` toggle/write/mark/privacy/clear), `8,386/8,386` `GameData` files unchanged.

---

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.
