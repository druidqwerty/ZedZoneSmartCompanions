"""Graphical menu-fixture acceptance; rollback candidate installs and user settings."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import re
import winreg

ROOT = Path(__file__).resolve().parents[1]
GAME = Path(r"G:\Steam\steamapps\common\ZED ZONE")
WORKSHOP = Path(r"G:\Steam\steamapps\workshop\content\1211600\3836613312")
SAVES = Path.home() / "AppData/LocalLow/LevenLiu/ZEDZONE"
STAMP = time.strftime("%Y%m%d-%H%M%S")
VERSION = re.search(r'PluginVersion = "([^"]+)"', (ROOT / "src/SmartCompanions/SmartCompanionsPlugin.cs").read_text(encoding="utf-8-sig")).group(1)
BACKUP = ROOT / "backups" / ("ui" + VERSION.replace(".", "") + "-" + STAMP)
OUTPUT = BACKUP / "screenshots"
REG_PATH = r"Software\LevenLiu\ZEDZONE"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def saves_manifest():
    return {str(p.relative_to(SAVES)): digest(p) for p in (SAVES / "GameData").rglob("*") if p.is_file()}


def screen_prefs():
    result = {}
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_PATH) as key:
            for index in range(winreg.QueryInfoKey(key)[1]):
                name, value, kind = winreg.EnumValue(key, index)
                if name.lower().startswith("screenmanager"):
                    result[name] = (value, kind)
    except FileNotFoundError:
        pass
    return result


def main():
    global CANDIDATE_DLL
    CANDIDATE_DLL = Path(next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--dll=")), ROOT / "src/SmartCompanions/bin/SmartCompanions.dll"))
    print("candidate_dll_sha256=" + digest(CANDIDATE_DLL), flush=True)
    probe_scale = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--os-probe=")), None)
    running = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ZEDZONE.exe", "/FO", "CSV"], capture_output=True, text=True)
    if '"ZEDZONE.exe"' in running.stdout:
        raise SystemExit("Game already running; no changes made.")
    harness_dir = GAME / "Mods/ZUiSmoke"
    if harness_dir.exists():
        raise SystemExit("Existing ZUiSmoke directory; inspect before running.")
    OUTPUT.mkdir(parents=True)
    paths = list(WORKSHOP.rglob("SmartCompanions.dll")) + list(WORKSHOP.rglob("mod.json"))
    paths += [GAME / "Mods/SmartCompanions/SmartCompanions.dll", GAME / "Mods/SmartCompanions/mod.json",
              GAME / "steam_appid.txt", GAME / "BepInEx/LogOutput.log", GAME / "BepInEx/ZedModsLoader-report.txt",
              GAME / "BepInEx/config/SmartCompanionsConfig.json", GAME / "BepInEx/config/SmartCompanions_Rules.txt",
              SAVES / "Player.log", SAVES / "Player-prev.log"]
    original = {}
    for index, path in enumerate(dict.fromkeys(paths)):
        copy = BACKUP / "originals" / str(index)
        copy.parent.mkdir(exist_ok=True)
        original[str(path)] = str(copy) if path.exists() else None
        if path.exists():
            shutil.copy2(path, copy)
    prefs = screen_prefs()
    before = saves_manifest()
    (BACKUP / "rollback.json").write_text(json.dumps({"files": original, "registry": prefs, "saves": before}, indent=2))
    process = None
    try:
        for path in map(Path, original):
            if path.name == "SmartCompanions.dll":
                shutil.copy2(CANDIDATE_DLL, path)
            elif path.name == "mod.json":
                meta = json.loads(path.read_text(encoding="utf-8-sig"))
                meta["version"] = VERSION
                path.write_text(json.dumps(meta), encoding="utf-8")
        harness_dir.mkdir()
        shutil.copy2(ROOT / "tools/UiSmoke/bin/SmartCompanions.UiSmoke.dll", harness_dir)
        (harness_dir / "mod.json").write_text(json.dumps({"name": "UI acceptance", "version": "1.0.0", "enabled": True}))
        (GAME / "steam_appid.txt").write_text("1211600")
        env = dict(os.environ, SteamAppId="1211600", SteamGameId="1211600")
        mode = ["--sc-ui-os-probe", "--sc-ui-scale=" + probe_scale] if probe_scale else ["--sc-ui-matrix"]
        mode += [a.replace("--start-case=", "--sc-ui-start-case=") for a in sys.argv if a.startswith("--start-case=")]
        process = subprocess.Popen([str(GAME / "ZEDZONE.exe"), "--sc-ui-smoke"] + mode + [
            "--sc-ui-smoke-output=" + str(OUTPUT), "-screen-fullscreen", "0", "-screen-width", "1920" if probe_scale else "1280",
            "-screen-height", "1080" if probe_scale else "720"], cwd=GAME, env=env)
        print(f"pid={process.pid}; evidence={BACKUP}", flush=True)
        if probe_scale:
            subprocess.run([sys.executable, str(ROOT / "tools/run_ui_os_probe.py"), "--pid", str(process.pid), "--output", str(OUTPUT)], timeout=100)
        process.wait(timeout=900)
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            process.wait(timeout=20)
        for path in [GAME / "BepInEx/LogOutput.log", SAVES / "Player.log"]:
            if path.exists():
                shutil.copy2(path, BACKUP / path.name)
        # Delete only files made by this runner, never a pre-existing directory.
        if harness_dir.exists():
            for name in ("SmartCompanions.UiSmoke.dll", "mod.json"):
                (harness_dir / name).unlink(missing_ok=True)
            harness_dir.rmdir()
        for path, copy in original.items():
            if copy is None:
                Path(path).unlink(missing_ok=True)
            else:
                shutil.copy2(copy, path)
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, REG_PATH) as key:
            for name in screen_prefs():
                if name not in prefs:
                    winreg.DeleteValue(key, name)
            for name, (value, kind) in prefs.items():
                winreg.SetValueEx(key, name, 0, kind, value)
        after = saves_manifest()
        restored = all(digest(Path(p)) == digest(Path(c)) if c else not Path(p).exists() for p, c in original.items())
        report = {"save_files": len(before), "gamedata_identical": before == after, "original_files_restored": restored,
                  "screen_preferences_restored": screen_prefs() == prefs, "screenshots": len(list(OUTPUT.glob("*.png")))}
        (BACKUP / "restoration.json").write_text(json.dumps(report, indent=2))
        print(json.dumps(report), flush=True)
    log = (BACKUP / "LogOutput.log").read_text(encoding="utf-8-sig")
    passed = json.loads((OUTPUT / "os-probe-result.json").read_text())["passed"] if probe_scale else "actual repaint cases; screenshots=" in log
    if not passed or "[UiSmoke] FAIL" in log or not restored or before != after:
        raise SystemExit("Acceptance failed; inspect captured log. Originals restored.")


if __name__ == "__main__":
    main()
