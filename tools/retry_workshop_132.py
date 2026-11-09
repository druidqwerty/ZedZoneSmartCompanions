"""One bounded retry of the verified 1.3.2 upload; never rebuild or downgrade."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_DLL = "d4e8daf8b4f6d87e384eefa9f6a37b3b451f1e7c1c5b5a44b4cd31e676bb144b"

def details():
    body=urllib.parse.urlencode({"itemcount":1,"publishedfileids[0]":"3836613312"}).encode()
    request=urllib.request.Request("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",data=body)
    return json.load(urllib.request.urlopen(request,timeout=30))["response"]["publishedfiledetails"][0]

def main():
    current=details()
    assert current["result"]==1 and current["creator"]=="76561198068633311", "Unexpected Workshop owner/state"
    title=current.get("title","")
    version=re.search(r"\b(\d+)\.(\d+)\.(\d+)\b",title)
    if not version:
        raise RuntimeError("No recognizable current version; refusing to overwrite")
    release=tuple(map(int,version.groups()))
    if release >= (1,3,2):
        print("Workshop is already at 1.3.2 or newer; no upload repeated.")
        return
    if release != (1,3,1):
        raise RuntimeError("Unexpected older Workshop state; refusing automatic overwrite")
    for folder in ("SmartCompanions","Mods/SmartCompanions"):
        path=ROOT/"workshop/Contents"/folder
        assert hashlib.sha256((path/"SmartCompanions.dll").read_bytes()).hexdigest()==EXPECTED_DLL, "Staging binary changed"
        assert json.loads((path/"mod.json").read_text())["version"]=="1.3.2", "Staging version changed"
    assert "RELEASE_READY: true" in (ROOT/"docs/verification-1.3.2.md").read_text(encoding="utf-8")
    for name in ("workshop.txt","workshop_russian.txt","workshop_schinese.txt"):
        text=(ROOT/"workshop"/name).read_text(encoding="utf-8")
        assert "1.3.2" in text and "visibility=public" in text, "Staging description changed"
    result=subprocess.run([sys.executable,"-I",str(ROOT/"tools/zed_wsupload.py"),str(ROOT/"workshop"),
        "1.3.2: native NPC menus and verified ranged combat fixes."],cwd=ROOT,capture_output=True,text=True,encoding="utf-8",errors="replace")
    if result.returncode:
        lines=[line for line in result.stdout.splitlines() if "FAILED:" in line]
        raise RuntimeError("Steam update still rejected: "+("; ".join(lines) or result.stderr[-500:]))
    final=details()
    assert final["visibility"]==0 and "1.3.2" in final["title"], "Upload acknowledged but public verification incomplete"
    print("Smart Companions 1.3.2 is now public on Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312")

if __name__=="__main__":
    main()
