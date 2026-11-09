import os
import shutil
import json
import zipfile
import hashlib
import subprocess
import urllib.parse
import urllib.request

proj = r"C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions"
game_dir = r"G:\Steam\steamapps\common\ZED ZONE"
mod_dir = os.path.join(game_dir, "Mods", "SmartCompanions")
ws_contents = os.path.join(proj, "workshop", "Contents", "SmartCompanions")

# 1. Build SmartCompanions.dll v1.1.0
subprocess.run(
    ["dotnet", "build", os.path.join(proj, "src", "SmartCompanions", "SmartCompanions.csproj"), "-c", "Release"],
    cwd=proj,
    check=True,
)

dll_src = os.path.join(proj, "src", "SmartCompanions", "bin", "SmartCompanions.dll")
for d in [mod_dir, ws_contents]:
    shutil.copy2(dll_src, os.path.join(d, "SmartCompanions.dll"))

kit_dst = os.path.join(game_dir, "ZedModKit", "src", "SmartCompanions")
if os.path.exists(kit_dst):
    shutil.rmtree(kit_dst)
shutil.copytree(os.path.join(proj, "src", "SmartCompanions"), kit_dst)

zip_path = os.path.join(proj, "dist", "ZedZoneSmartCompanions-1.1.0.zip")
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    zf.write(dll_src, "Mods/SmartCompanions/SmartCompanions.dll")
    zf.write(os.path.join(mod_dir, "mod.json"), "Mods/SmartCompanions/mod.json")

zip_size = os.path.getsize(zip_path)
zip_sha = hashlib.sha256(open(zip_path, "rb").read()).hexdigest()
print(f"Built ZIP 1.1.0: size={zip_size}, sha256={zip_sha}", flush=True)

# 2. Update README.md and README_RU.md version references
for rel in ["README.md", "README_RU.md"]:
    p = os.path.join(proj, rel)
    txt = open(p, encoding="utf-8").read()
    txt = txt.replace("v1.0.0", "v1.1.0").replace("1.0.0.zip", "1.1.0.zip")
    open(p, "w", encoding="utf-8").write(txt)

# 3. Git commit & push + GitHub Release v1.1.0
subprocess.run(["git", "add", "-A"], cwd=proj, check=True)
subprocess.run(["git", "commit", "-m", "feat: release v1.1.0 (720+ lines, 18+ Rude Mode, Share Ration Buff, Rear Flank Radar, Base Auto-Dropoff)"], cwd=proj, check=True)
subprocess.run(["git", "push", "origin", "main"], cwd=proj, check=True)

notes_file = os.path.join(proj, "BOOSTY_1.1.0.md")
r = subprocess.run(
    ["gh", "release", "create", "v1.1.0", zip_path, "--title", "ZED ZONE: Smart Companions v1.1.0", "--notes-file", notes_file],
    cwd=proj,
    capture_output=True,
    text=True,
)
print("GitHub Release v1.1.0:", r.stdout.strip(), r.stderr.strip(), flush=True)

# 4. Upload to Steam Workshop (3836613312)
subprocess.run(
    [
        "python",
        "-I",
        os.path.join(proj, "tools", "zed_wsupload.py"),
        os.path.join(proj, "workshop"),
        "v1.1.0: Added 310+ RU & 160+ EN 18+ rude post-apocalyptic lines (723 total lines across 50 topics), Profanity Mode toggle, Share Ration & Fed & Fierce buff (F6), Rear Flank Radar, Smart Overweight Filter, and Base Auto-Dropoff",
    ],
    cwd=proj,
    check=True,
)

# 5. Update Boosty Queue (remove pending BOOSTY_1.0.0.md if present, add BOOSTY_1.1.0.md)
q_file = r"C:\Users\druid\.openclaw\workspace\tools\boosty\queue.txt"
if os.path.exists(q_file):
    lines = [ln for ln in open(q_file, encoding="utf-8").read().splitlines() if "BOOSTY_1.0.0.md" not in ln]
    open(q_file, "w", encoding="utf-8").write("\n".join(lines) + ("\n" if lines else ""))

subprocess.run(
    ["python", "-I", r"C:\Users\druid\.openclaw\workspace\tools\boosty\queue.py", "add", notes_file],
    cwd=proj,
    check=True,
)

# 6. Verify anonymous Steam API & GitHub release download hash
data = urllib.parse.urlencode({"itemcount": 1, "publishedfileids[0]": "3836613312"}).encode()
req = urllib.request.Request("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", data=data)
res = json.loads(urllib.request.urlopen(req, timeout=15).read().decode())
det = res["response"]["publishedfiledetails"][0]
print("Steam API:", "result=", det.get("result"), "visibility=", det.get("visibility"), "file_size=", det.get("file_size"), "title=", det.get("title"), flush=True)

dl_bytes = urllib.request.urlopen("https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/download/v1.1.0/ZedZoneSmartCompanions-1.1.0.zip", timeout=20).read()
dl_sha = hashlib.sha256(dl_bytes).hexdigest()
print("GitHub Release ZIP download verified:", len(dl_bytes), "bytes, sha256 match=", dl_sha == zip_sha, flush=True)
