import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(r"C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions")
GAME_DIR = Path(r"G:\Steam\steamapps\common\ZED ZONE")
WORKSHOP_ROOT = Path(r"G:\Steam\steamapps\workshop\content\1211600\3836613312")
LOCAL_MOD = GAME_DIR / "Mods" / "SmartCompanions"
LOCAL_APPDATA_ZZ = Path(r"C:\Users\druid\AppData\LocalLow\LevenLiu\ZEDZONE")
REAL_GAMEDATA = LOCAL_APPDATA_ZZ / "GameData"

VERSION = re.search(r'PluginVersion = "([^"]+)"', (ROOT / "src/SmartCompanions/SmartCompanionsPlugin.cs").read_text(encoding="utf-8-sig")).group(1)
LIVE_DIR = ROOT / "backups" / ("live-" + time.strftime("%Y%m%d-%H%M%S"))
CANDIDATE = Path(next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--dll=")), ROOT / "src/SmartCompanions/bin/SmartCompanions.dll")).resolve()
PROFILE_DIR = LIVE_DIR / "profile"
ZLIVESMOKE_DST = GAME_DIR / "Mods" / "ZLiveSmoke"
STEAM_APPID = GAME_DIR / "steam_appid.txt"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


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
    chk = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ZEDZONE.exe"], capture_output=True, text=True)
    if "ZEDZONE.exe" in chk.stdout:
        print("ABORT: ZEDZONE.exe is already running!", flush=True)
        return 2

    LIVE_DIR.mkdir(parents=True, exist_ok=True)
    print("Computing pre-run SHA-256 of real GameData...", flush=True)
    pre_hashes = snapshot_hashes(REAL_GAMEDATA)
    print(f"Pre-run GameData files hashed: {len(pre_hashes)}", flush=True)

    if PROFILE_DIR.exists():
        shutil.rmtree(PROFILE_DIR)
    (PROFILE_DIR / "GameData").parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(REAL_GAMEDATA, PROFILE_DIR / "GameData")
    print("Copied disposable profile to " + str(PROFILE_DIR), flush=True)

    # Backup all candidate target paths (both Workshop copies and local Mods/SmartCompanions)
    targets = list(WORKSHOP_ROOT.rglob("SmartCompanions.dll")) + list(WORKSHOP_ROOT.rglob("mod.json"))
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
                meta = json.loads(p.read_text(encoding="utf-8-sig")) if p.exists() else {"name": "ZED ZONE Smart Companions", "author": "Modsi", "enabled": True}
                meta["version"] = VERSION
                p.write_text(json.dumps(meta, indent=2), encoding="utf-8")

        if ZLIVESMOKE_DST.exists():
            shutil.rmtree(ZLIVESMOKE_DST)
        ZLIVESMOKE_DST.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / "tools" / "LiveSmoke" / "bin" / "SmartCompanions.LiveSmoke.dll", ZLIVESMOKE_DST / "SmartCompanions.LiveSmoke.dll")
        shutil.copy2(ROOT / "backups" / "live160" / "ZLiveSmoke-verified" / "mod.json", ZLIVESMOKE_DST / "mod.json")

        if not STEAM_APPID.exists():
            STEAM_APPID.write_text("1211600\n", encoding="utf-8")
            created_appid = True

        env = dict(os.environ, SteamAppId="1211600", SteamGameId="1211600")
        cmd = [
            str(GAME_DIR / "ZEDZONE.exe"),
            f"--sc-live-profile={PROFILE_DIR}",
            "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720",
        ]
        print("Launching ZEDZONE.exe isolated live acceptance...", flush=True)
        t0 = time.time()
        proc = subprocess.Popen(cmd, cwd=str(GAME_DIR), env=env)
        try:
            rc = proc.wait(timeout=150)
        except subprocess.TimeoutExpired:
            print("TIMEOUT: killing ZEDZONE.exe after 150s", flush=True)
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

        player_log = LOCAL_APPDATA_ZZ / "Player.log"
        bepinex_log = GAME_DIR / "BepInEx" / "LogOutput.log"
        if player_log.exists():
            shutil.copy2(player_log, LIVE_DIR / "Player.log")
        if bepinex_log.exists():
            shutil.copy2(bepinex_log, LIVE_DIR / "LogOutput.log")

        print("Computing post-run SHA-256 of real GameData...", flush=True)
        post_hashes = snapshot_hashes(REAL_GAMEDATA)
        identical = pre_hashes == post_hashes
        log_text = (LIVE_DIR / "LogOutput.log").read_text(encoding="utf-8-sig", errors="replace") if (LIVE_DIR / "LogOutput.log").exists() else ""
        has_fail = (" FAIL" in log_text) or (f"[SelfVerify] PASS v{VERSION}" not in log_text)
        report = {
            "returncode": rc,
            "version_verified": VERSION,
            "has_fail_in_log": has_fail,
            "pre_file_count": len(pre_hashes),
            "post_file_count": len(post_hashes),
            "gamedata_identical": identical,
            "zlivesmoke_removed": not ZLIVESMOKE_DST.exists(),
            "installs_restored": all_restored,
            "steam_appid_removed": not STEAM_APPID.exists() if created_appid else "preexisting",
        }
        (LIVE_DIR / "verification_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("VERIFICATION REPORT:", json.dumps(report), flush=True)

    return 0 if (rc == 0 and identical and all_restored and not has_fail) else 1


if __name__ == "__main__":
    sys.exit(main())
