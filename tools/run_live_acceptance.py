import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(r"C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions")
GAME_DIR = Path(r"G:\Steam\steamapps\common\ZED ZONE")
WORKSHOP_MOD = Path(r"G:\Steam\steamapps\workshop\content\1211600\3836613312\mods\SmartCompanions")
LOCAL_APPDATA_ZZ = Path(r"C:\Users\druid\AppData\LocalLow\LevenLiu\ZEDZONE")
REAL_GAMEDATA = LOCAL_APPDATA_ZZ / "GameData"

LIVE_DIR = ROOT / "backups" / ("live-" + time.strftime("%Y%m%d-%H%M%S"))
CANDIDATE = Path(next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--dll=")), ROOT / "src/SmartCompanions/bin/SmartCompanions.dll")).resolve()
PROFILE_DIR = LIVE_DIR / "profile"
ZLIVESMOKE_DST = GAME_DIR / "Mods" / "ZLiveSmoke"
STEAM_APPID = GAME_DIR / "steam_appid.txt"


def snapshot_hashes(folder: Path) -> dict[str, str]:
    out: dict[str, str] = {}
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


def main() -> int:
    # Ensure ZEDZONE is not already running
    chk = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ZEDZONE.exe"], capture_output=True, text=True)
    if "ZEDZONE.exe" in chk.stdout:
        print("ABORT: ZEDZONE.exe is already running!", flush=True)
        return 2

    LIVE_DIR.mkdir(parents=True, exist_ok=True)
    print("Computing pre-run SHA-256 of real GameData...", flush=True)
    pre_hashes = snapshot_hashes(REAL_GAMEDATA)
    print(f"Pre-run GameData files hashed: {len(pre_hashes)}", flush=True)

    # Refresh disposable profile copy
    if PROFILE_DIR.exists():
        shutil.rmtree(PROFILE_DIR)
    (PROFILE_DIR / "GameData").parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(REAL_GAMEDATA, PROFILE_DIR / "GameData")
    print("Copied disposable profile to " + str(PROFILE_DIR), flush=True)

    created_appid = False
    ws_backup = LIVE_DIR / "ws_backup"
    if ws_backup.exists():
        shutil.rmtree(ws_backup)
    shutil.copytree(WORKSHOP_MOD, ws_backup)

    rc = -1
    try:
        # Stage v1.7.0 DLL + mod.json into Workshop folder
        shutil.copy2(CANDIDATE, WORKSHOP_MOD / "SmartCompanions.dll")
        print("candidate_dll_sha256=" + hashlib.sha256(CANDIDATE.read_bytes()).hexdigest(), flush=True)
        shutil.copy2(ROOT / "workshop" / "Contents" / "mods" / "SmartCompanions" / "mod.json", WORKSHOP_MOD / "mod.json")

        # Stage ZLiveSmoke into Mods\ZLiveSmoke
        if ZLIVESMOKE_DST.exists():
            shutil.rmtree(ZLIVESMOKE_DST)
        ZLIVESMOKE_DST.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / "tools" / "LiveSmoke" / "bin" / "SmartCompanions.LiveSmoke.dll", ZLIVESMOKE_DST / "SmartCompanions.LiveSmoke.dll")
        shutil.copy2(ROOT / "backups" / "live160" / "ZLiveSmoke-verified" / "mod.json", ZLIVESMOKE_DST / "mod.json")

        if not STEAM_APPID.exists():
            STEAM_APPID.write_text("1211600\n", encoding="utf-8")
            created_appid = True

        cmd = [
            str(GAME_DIR / "ZEDZONE.exe"),
            f"--sc-live-profile={PROFILE_DIR}",
        ]
        print("Launching ZEDZONE.exe isolated live acceptance...", flush=True)
        t0 = time.time()
        proc = subprocess.Popen(cmd, cwd=str(GAME_DIR))
        try:
            rc = proc.wait(timeout=135)
        except subprocess.TimeoutExpired:
            print("TIMEOUT: killing ZEDZONE.exe after 135s", flush=True)
            proc.kill()
            rc = 124
        elapsed = time.time() - t0
        print(f"ZEDZONE.exe exited with code={rc} after {elapsed:.1f}s", flush=True)
    finally:
        if ZLIVESMOKE_DST.exists():
            shutil.rmtree(ZLIVESMOKE_DST, ignore_errors=True)
        # Restore the subscribed Workshop install exactly as it was.
        for name in ("SmartCompanions.dll", "mod.json"):
            if (ws_backup / name).exists():
                shutil.copy2(ws_backup / name, WORKSHOP_MOD / name)
        ws_restored = all(snapshot_hashes(ws_backup).get(k) == v for k, v in snapshot_hashes(WORKSHOP_MOD).items())
        if created_appid and STEAM_APPID.exists():
            try:
                STEAM_APPID.unlink()
            except OSError:
                pass

        player_log = LOCAL_APPDATA_ZZ / "Player.log"
        bepinex_log = GAME_DIR / "BepInEx" / "LogOutput.log"
        if player_log.exists():
            shutil.copy2(player_log, LIVE_DIR / "Player.log")
        if bepinex_log.exists():
            shutil.copy2(bepinex_log, LIVE_DIR / "LogOutput.log")

        print("Computing post-run SHA-256 of real GameData...", flush=True)
        post_hashes = snapshot_hashes(REAL_GAMEDATA)
        identical = pre_hashes == post_hashes
        report = {
            "returncode": rc,
            "pre_file_count": len(pre_hashes),
            "post_file_count": len(post_hashes),
            "gamedata_identical": identical,
            "zlivesmoke_removed": not ZLIVESMOKE_DST.exists(),
            "workshop_restored": ws_restored,
            "steam_appid_removed": not STEAM_APPID.exists() if created_appid else "preexisting",
        }
        (LIVE_DIR / "verification_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("VERIFICATION REPORT:", json.dumps(report), flush=True)

    return 0 if (rc == 0 and identical) else 1


if __name__ == "__main__":
    sys.exit(main())
