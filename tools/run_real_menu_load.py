"""End-to-end real-player MainMenu -> Sandbox Save -> CharacterSelect -> Gaming load acceptance.

Hashes real GameData before and after, redirects all save/settings writes to a disposable profile
copy, stages candidate SmartCompanions.dll + ZLiveSmoke, runs --sc-real-menu-load without ever
overriding Time.timeScale or isSoftPaused, restores all originals, and verifies [RealMenuLoad] PASS
+ 0 FAIL + real-menu-load.png (> 10 KB).
"""
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import winreg
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GAME_DIR = Path(r"G:\Steam\steamapps\common\ZED ZONE")
WORKSHOP_ROOT = Path(r"G:\Steam\steamapps\workshop\content\1211600\3836613312")
LOCAL_MOD = GAME_DIR / "Mods" / "SmartCompanions"
LOCAL_APPDATA_ZZ = Path(r"C:\Users\druid\AppData\LocalLow\LevenLiu\ZEDZONE")
REAL_GAMEDATA = LOCAL_APPDATA_ZZ / "GameData"
REAL_GAMESETTINGS = LOCAL_APPDATA_ZZ / "GameSettings"
REG_PATH = r"Software\LevenLiu\ZEDZONE"

VERSION = re.search(
    r'PluginVersion = "([^"]+)"',
    (ROOT / "src/SmartCompanions/SmartCompanionsPlugin.cs").read_text(encoding="utf-8-sig"),
).group(1)
LIVE_DIR = ROOT / "backups" / ("menuload-" + time.strftime("%Y%m%d-%H%M%S"))
CANDIDATE = Path(
    next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--dll=")), ROOT / "src/SmartCompanions/bin/SmartCompanions.dll")
).resolve()
PROFILE_DIR = LIVE_DIR / "profile"
ZLIVESMOKE_DST = GAME_DIR / "Mods" / "ZLiveSmoke"
STEAM_APPID = GAME_DIR / "steam_appid.txt"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def snapshot_hashes(folder: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not folder.exists():
        return out
    for dirpath, _, filenames in os.walk(folder):
        for fn in filenames:
            full = Path(dirpath) / fn
            rel = str(full.relative_to(folder))
            h = hashlib.sha256()
            with open(full, "rb") as f:
                while chunk := f.read(65536):
                    h.update(chunk)
            out[rel] = h.hexdigest()
    return out


def snapshot_registry() -> dict[str, tuple[object, int]]:
    result: dict[str, tuple[object, int]] = {}
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_PATH) as key:
            for index in range(winreg.QueryInfoKey(key)[1]):
                name, value, kind = winreg.EnumValue(key, index)
                result[name] = (value, kind)
    except FileNotFoundError:
        pass
    return result


def restore_registry(saved: dict[str, tuple[object, int]]) -> None:
    try:
        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, REG_PATH) as key:
            current = snapshot_registry()
            for name in current:
                if name not in saved:
                    try:
                        winreg.DeleteValue(key, name)
                    except FileNotFoundError:
                        pass
            for name, (value, kind) in saved.items():
                winreg.SetValueEx(key, name, 0, kind, value)
    except OSError:
        pass


def main() -> int:
    chk = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ZEDZONE.exe"], capture_output=True, text=True)
    if "ZEDZONE.exe" in chk.stdout:
        print("ABORT: ZEDZONE.exe is already running!", flush=True)
        return 2

    LIVE_DIR.mkdir(parents=True, exist_ok=True)
    print("Computing pre-run SHA-256 of real GameData...", flush=True)
    pre_hashes = snapshot_hashes(REAL_GAMEDATA)
    pre_settings_hashes = snapshot_hashes(REAL_GAMESETTINGS)
    reg_before = snapshot_registry()
    print(f"Pre-run GameData files hashed: {len(pre_hashes)} (GameSettings files: {len(pre_settings_hashes)})", flush=True)

    if PROFILE_DIR.exists():
        shutil.rmtree(PROFILE_DIR)
    PROFILE_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copytree(REAL_GAMEDATA, PROFILE_DIR / "GameData")
    if REAL_GAMESETTINGS.exists():
        if REAL_GAMESETTINGS.is_dir():
            shutil.copytree(REAL_GAMESETTINGS, PROFILE_DIR / "GameSettings")
        else:
            shutil.copy2(REAL_GAMESETTINGS, PROFILE_DIR / "GameSettings")
    print("Copied disposable profile (GameData + GameSettings) to " + str(PROFILE_DIR), flush=True)

    # Backup all candidate target paths (1 or 2 Workshop copies + local Mods/SmartCompanions if present)
    targets: list[Path] = []
    if WORKSHOP_ROOT.exists():
        targets += list(WORKSHOP_ROOT.rglob("SmartCompanions.dll")) + list(WORKSHOP_ROOT.rglob("mod.json"))
    if LOCAL_MOD.exists():
        targets += [LOCAL_MOD / "SmartCompanions.dll", LOCAL_MOD / "mod.json"]
    originals: dict[str, str | None] = {}
    for idx, p in enumerate(dict.fromkeys(targets)):
        bak = LIVE_DIR / "originals" / str(idx)
        bak.parent.mkdir(parents=True, exist_ok=True)
        if p.exists():
            shutil.copy2(p, bak)
            originals[str(p)] = str(bak)
        else:
            originals[str(p)] = None

    created_appid = False
    rc = -1
    try:
        cand_sha = digest(CANDIDATE)
        print(f"candidate_dll_sha256={cand_sha} version={VERSION}", flush=True)
        for p_str in originals:
            p = Path(p_str)
            p.parent.mkdir(parents=True, exist_ok=True)
            if p.name == "SmartCompanions.dll":
                shutil.copy2(CANDIDATE, p)
            elif p.name == "mod.json":
                meta = (
                    json.loads(p.read_text(encoding="utf-8-sig"))
                    if p.exists()
                    else {"name": "ZED ZONE Smart Companions", "author": "Modsi", "enabled": True}
                )
                meta["version"] = VERSION
                p.write_text(json.dumps(meta, indent=2), encoding="utf-8")

        if ZLIVESMOKE_DST.exists():
            shutil.rmtree(ZLIVESMOKE_DST)
        ZLIVESMOKE_DST.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / "tools" / "LiveSmoke" / "bin" / "SmartCompanions.LiveSmoke.dll", ZLIVESMOKE_DST / "SmartCompanions.LiveSmoke.dll")
        (ZLIVESMOKE_DST / "mod.json").write_text(
            json.dumps({"name": "ZLiveSmoke", "author": "Lab", "version": "1.0.0", "enabled": True}),
            encoding="utf-8",
        )

        if not STEAM_APPID.exists():
            STEAM_APPID.write_text("1211600\n", encoding="utf-8")
            created_appid = True

        env = dict(os.environ, SteamAppId="1211600", SteamGameId="1211600")
        cmd = [
            str(GAME_DIR / "ZEDZONE.exe"),
            f"--sc-live-profile={PROFILE_DIR}",
            "--sc-real-menu-load",
            "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720",
        ]
        print("Launching ZEDZONE.exe real MainMenu -> Save Load acceptance...", flush=True)
        t0 = time.time()
        proc = subprocess.Popen(cmd, cwd=str(GAME_DIR), env=env)
        try:
            rc = proc.wait(timeout=160)
        except subprocess.TimeoutExpired:
            print("TIMEOUT: killing ZEDZONE.exe after 160s", flush=True)
            proc.kill()
            proc.wait(timeout=15)
            rc = 124
        elapsed = time.time() - t0
        print(f"ZEDZONE.exe exited with code={rc} after {elapsed:.1f}s", flush=True)
    finally:
        if ZLIVESMOKE_DST.exists():
            shutil.rmtree(ZLIVESMOKE_DST, ignore_errors=True)
        for p_str, bak_str in originals.items():
            p = Path(p_str)
            if bak_str is None:
                p.unlink(missing_ok=True)
            else:
                shutil.copy2(bak_str, p)
        all_restored = all(
            (digest(Path(p)) == digest(Path(b)) if b else not Path(p).exists())
            for p, b in originals.items()
        )
        if created_appid and STEAM_APPID.exists():
            try:
                STEAM_APPID.unlink()
            except OSError:
                pass
        restore_registry(reg_before)

        player_log = LOCAL_APPDATA_ZZ / "Player.log"
        bepinex_log = GAME_DIR / "BepInEx" / "LogOutput.log"
        if player_log.exists():
            shutil.copy2(player_log, LIVE_DIR / "Player.log")
        if bepinex_log.exists():
            shutil.copy2(bepinex_log, LIVE_DIR / "LogOutput.log")

        shot_profile = PROFILE_DIR / "real-menu-load.png"
        shot_copy = LIVE_DIR / "real-menu-load.png"
        if shot_profile.exists():
            shutil.copy2(shot_profile, shot_copy)
        shot_bytes = shot_profile.stat().st_size if shot_profile.exists() else 0
        shot_ok = shot_bytes > 10240

        print("Computing post-run SHA-256 of real GameData...", flush=True)
        post_hashes = snapshot_hashes(REAL_GAMEDATA)
        post_settings_hashes = snapshot_hashes(REAL_GAMESETTINGS)
        identical = pre_hashes == post_hashes
        settings_identical = pre_settings_hashes == post_settings_hashes

        log_text = (
            (LIVE_DIR / "LogOutput.log").read_text(encoding="utf-8-sig", errors="replace")
            if (LIVE_DIR / "LogOutput.log").exists()
            else ""
        )
        fail_matches = re.findall(r" FAIL\b", log_text)
        pass_found = "[RealMenuLoad] PASS" in log_text
        self_verify_found = f"[SelfVerify] PASS v{VERSION}" in log_text
        has_fail = (len(fail_matches) > 0) or (not pass_found) or (not self_verify_found)

        report = {
            "returncode": rc,
            "version_verified": VERSION,
            "real_menu_load_pass": pass_found,
            "self_verify_pass": self_verify_found,
            "fail_count": len(fail_matches),
            "has_fail_in_log": has_fail,
            "screenshot_path": str(shot_copy if shot_copy.exists() else shot_profile),
            "screenshot_bytes": shot_bytes,
            "screenshot_valid": shot_ok,
            "pre_file_count": len(pre_hashes),
            "post_file_count": len(post_hashes),
            "gamedata_identical": identical,
            "gamesettings_identical": settings_identical,
            "zlivesmoke_removed": not ZLIVESMOKE_DST.exists(),
            "installs_restored": all_restored,
            "steam_appid_removed": not STEAM_APPID.exists() if created_appid else "preexisting",
        }
        (LIVE_DIR / "verification_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("VERIFICATION REPORT:", json.dumps(report), flush=True)

    return 0 if (rc == 0 and identical and all_restored and shot_ok and not has_fail) else 1


if __name__ == "__main__":
    sys.exit(main())
