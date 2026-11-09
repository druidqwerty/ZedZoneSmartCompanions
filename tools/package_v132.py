"""Package the already verified build. Does not rebuild or publish."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
game = Path(r"G:\Steam\steamapps\common\ZED ZONE")
dll = root / "src/SmartCompanions/bin/SmartCompanions.dll"
verification = root / "docs/verification-1.3.2.md"
if not verification.exists() or "RELEASE_READY: true" not in verification.read_text(encoding="utf-8"):
    raise SystemExit("Missing completed verification report.")
meta = root / "workshop/Contents/SmartCompanions/mod.json"
assert json.loads(meta.read_text())["version"] == "1.3.2"
for target in (root / "workshop/Contents/SmartCompanions", root / "workshop/Contents/Mods/SmartCompanions", game / "Mods/SmartCompanions"):
    target.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, target / dll.name)
    if meta.resolve() != (target / "mod.json").resolve():
        shutil.copy2(meta, target / "mod.json")
kit = game / "ZedModKit/src/SmartCompanions"
kit.mkdir(parents=True, exist_ok=True)
for source in (root / "src/SmartCompanions").iterdir():
    if source.suffix in (".cs", ".csproj"):
        shutil.copy2(source, kit / source.name)
dist = root / "dist"
dist.mkdir(exist_ok=True)
archive = dist / "ZedZoneSmartCompanions-1.3.2.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(dll, "Mods/SmartCompanions/SmartCompanions.dll")
    z.write(meta, "Mods/SmartCompanions/mod.json")
    for filename in ("README.md", "README_RU.md", "README_CN.md", "LICENSE", "docs/verification-1.3.2.md", "docs/feedback-1.3.2.md", "docs/native-menu-1.3.2.log", "docs/ui-smoke-1.3.2.log", "docs/combat-acceptance-1.3.2.log", "docs/native-perception-analysis-1.3.2.md", "docs/preview.png"):
        z.write(root / filename, filename)
    for picture in sorted((root / "docs/images").glob("ui-1.3.2-*.png")):
        z.write(picture, str(picture.relative_to(root)))
sha = hashlib.sha256(archive.read_bytes()).hexdigest()
(dist / "ZedZoneSmartCompanions-1.3.2.zip.sha256").write_text(f"{sha}  {archive.name}\n", encoding="ascii")
print(json.dumps({"zip": str(archive), "size": archive.stat().st_size, "sha256": sha, "dll_sha256": hashlib.sha256(dll.read_bytes()).hexdigest()}, indent=2))
