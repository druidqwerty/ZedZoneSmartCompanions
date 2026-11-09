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

# 1. Build SmartCompanions.dll v1.3.0
subprocess.run(
    ["dotnet", "build", os.path.join(proj, "src", "SmartCompanions", "SmartCompanions.csproj"), "-c", "Release"],
    cwd=proj,
    check=True,
)

dll_src = os.path.join(proj, "src", "SmartCompanions", "bin", "SmartCompanions.dll")
for d in [mod_dir, ws_contents]:
    os.makedirs(d, exist_ok=True)
    shutil.copy2(dll_src, os.path.join(d, "SmartCompanions.dll"))
    if os.path.normcase(os.path.abspath(d)) != os.path.normcase(os.path.abspath(mod_dir)):
        shutil.copy2(os.path.join(mod_dir, "mod.json"), os.path.join(d, "mod.json"))

kit_dst = os.path.join(game_dir, "ZedModKit", "src", "SmartCompanions")
if os.path.exists(kit_dst):
    shutil.rmtree(kit_dst)
shutil.copytree(os.path.join(proj, "src", "SmartCompanions"), kit_dst)

zip_path = os.path.join(proj, "dist", "ZedZoneSmartCompanions-1.3.0.zip")
os.makedirs(os.path.dirname(zip_path), exist_ok=True)
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
    zf.write(dll_src, "Mods/SmartCompanions/SmartCompanions.dll")
    zf.write(os.path.join(mod_dir, "mod.json"), "Mods/SmartCompanions/mod.json")

zip_size = os.path.getsize(zip_path)
zip_sha = hashlib.sha256(open(zip_path, "rb").read()).hexdigest()
print(f"Built ZIP 1.3.0: size={zip_size}, sha256={zip_sha}", flush=True)

# 2. Update READMEs and Workshop descriptions
for rel in ["README.md", "README_RU.md", "README_CN.md"]:
    p = os.path.join(proj, rel)
    if os.path.exists(p):
        txt = open(p, encoding="utf-8").read()
        txt = txt.replace("v1.2.0", "v1.3.0").replace("1.2.0.zip", "1.3.0.zip").replace("1.1.0.zip", "1.3.0.zip")
        txt = txt.replace("50 контекстными темами живых реплик (987 уникальных фраз на 3 языках: 495 RU / 229 EN / 263 ZH)", "60 контекстными темами живых реплик (1 179 уникальных фраз на 3 языках: 565 RU / 290 EN / 324 ZH)")
        txt = txt.replace("50 context chatter topics (987 unique lines across 3 languages: 495 RU / 229 EN / 263 ZH)", "60 context chatter topics (1,179 unique lines across 3 languages: 565 RU / 290 EN / 324 ZH)")
        txt = txt.replace("50 个情景对话主题（987 条独特台词：495 俄语 / 229 英语 / 263 简体中文）", "60 个情景对话主题（1,179 条独特台词：565 俄语 / 290 英语 / 324 简体中文）")
        open(p, "w", encoding="utf-8").write(txt)

for rel in ["workshop/workshop.txt", "workshop/workshop_russian.txt", "workshop/workshop_schinese.txt"]:
    p = os.path.join(proj, rel)
    if os.path.exists(p):
        txt = open(p, encoding="utf-8").read()
        txt = txt.replace("1.2.0", "1.3.0")
        txt = txt.replace("987", "1,179").replace("50", "60")
        txt = txt.replace("495 RU / 229 EN / 263 ZH", "565 RU / 290 EN / 324 ZH")
        open(p, "w", encoding="utf-8").write(txt)

# 3. Git commit & push + GitHub Release v1.3.0
subprocess.run(["git", "add", "-A"], cwd=proj, check=True)
subprocess.run(["git", "commit", "-m", "feat: release v1.3.0 (5 Archetypes, 5 Veteran Ranks & XP, Camp Chores, Vehicle Trunk Unload, Player Rescue, 60 topics / 1179 lines)"], cwd=proj, check=True)
subprocess.run(["git", "push", "origin", "main"], cwd=proj, check=True)

notes_file = os.path.join(proj, "BOOSTY_1.3.0.md")
r = subprocess.run(
    ["gh", "release", "create", "v1.3.0", zip_path, "--title", "ZED ZONE: Smart Companions v1.3.0", "--notes-file", notes_file],
    cwd=proj,
    capture_output=True,
    text=True,
)
print("GitHub Release v1.3.0:", r.stdout.strip(), r.stderr.strip(), flush=True)

# 4. Upload to Steam Workshop (3836613312)
subprocess.run(
    [
        "python",
        "-I",
        os.path.join(proj, "tools", "zed_wsupload.py"),
        os.path.join(proj, "workshop"),
        "v1.3.0: Added 5 Companion Archetypes, 5 Veteran Ranks & XP progression, Autonomous Camp Chores (Campfire Keeper, Base Gardener, Field Armorer, Vehicle Trunk Unloader), Emergency Player Rescue, Smart Flashlight, Squad Battle Cry (F9), and 60 Chatter Topics (1,179 unique RU/EN/ZH lines)",
    ],
    cwd=proj,
    check=True,
)

# 5. Update Boosty Queue (replace older pending ZedZoneSmartCompanions posts with BOOSTY_1.3.0.md)
q_file = r"C:\Users\druid\.openclaw\workspace\tools\boosty\queue.txt"
if os.path.exists(q_file):
    lines = [ln for ln in open(q_file, encoding="utf-8").read().splitlines() if "zedzone-smart-companions" not in ln.lower()]
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

dl_bytes = urllib.request.urlopen("https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/download/v1.3.0/ZedZoneSmartCompanions-1.3.0.zip", timeout=20).read()
dl_sha = hashlib.sha256(dl_bytes).hexdigest()
print("GitHub Release ZIP download verified:", len(dl_bytes), "bytes, sha256 match=", dl_sha == zip_sha, flush=True)
