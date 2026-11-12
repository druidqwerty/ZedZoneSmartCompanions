"""Package a verified release into staging + ZIP; never installs or publishes it.

Usage: python tools/package_release.py <version> [<dll path>]
"""
from pathlib import Path
import hashlib
import json
import shutil
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = sys.argv[1]
DLL = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else ROOT / "src/SmartCompanions/bin/SmartCompanions.dll"
REPORT = ROOT / f"docs/verification-{VERSION}.md"
NOTES = ROOT / f"docs/RELEASE_NOTES_{VERSION}.md"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    report = REPORT.read_text(encoding="utf-8-sig")
    dll_hash = digest(DLL)
    if "RELEASE_READY: true" not in report or dll_hash not in report:
        raise SystemExit("Verification must approve the exact DLL SHA-256 before packaging.")
    notes = NOTES.read_text(encoding="utf-8-sig")
    if "Candidate verification is pending" in notes or "Проверка кандидата не завершена" in notes:
        raise SystemExit("Replace pending release-note paragraphs with final verification evidence.")
    canonical = ROOT / "workshop/Contents/SmartCompanions/mod.json"
    manifest = json.loads(canonical.read_text(encoding="utf-8-sig"))
    if manifest["version"] != VERSION:
        raise SystemExit("Wrong staging manifest version.")
    source_project = ROOT / "src/SmartCompanions/SmartCompanions.csproj"
    source_plugin = ROOT / "src/SmartCompanions/SmartCompanionsPlugin.cs"
    if f"<Version>{VERSION}</Version>" not in source_project.read_text(encoding="utf-8-sig"):
        raise SystemExit("Wrong source project version.")
    if f'PluginVersion = "{VERSION}"' not in source_plugin.read_text(encoding="utf-8-sig"):
        raise SystemExit("Wrong plugin version.")

    # Only project staging is mutated. The active game/Workshop cache is left alone.
    for relative in ("workshop/Contents/SmartCompanions", "workshop/Contents/Mods/SmartCompanions"):
        target = ROOT / relative
        target.mkdir(parents=True, exist_ok=True)
        shutil.copy2(DLL, target / "SmartCompanions.dll")
        if (target / "mod.json").resolve() != canonical.resolve():
            shutil.copy2(canonical, target / "mod.json")
        assert digest(target / "SmartCompanions.dll") == dll_hash

    files = ["README.md", "README_RU.md", "README_CN.md", "LICENSE",
             str(REPORT.relative_to(ROOT)), str(NOTES.relative_to(ROOT)),
             "docs/preview.png"]
    for evidence in sorted((ROOT / "docs").glob(f"*-{VERSION}.json")):
        if not evidence.name.startswith(("publication-", "steam-verification-")):
            files.append(str(evidence.relative_to(ROOT)))
    for evidence in sorted((ROOT / "docs").glob(f"*{VERSION}*.log")):
        files.append(str(evidence.relative_to(ROOT)))
    for picture in sorted((ROOT / "docs/images").glob(f"ui-{VERSION}-*.png")):
        files.append(str(picture.relative_to(ROOT)))

    dist = ROOT / "dist"
    dist.mkdir(exist_ok=True)
    archive = dist / f"ZedZoneSmartCompanions-{VERSION}.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(DLL, "Mods/SmartCompanions/SmartCompanions.dll")
        z.write(canonical, "Mods/SmartCompanions/mod.json")
        for name in files:
            z.write(ROOT / name, name.replace("\\", "/"))
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert hashlib.sha256(z.read("Mods/SmartCompanions/SmartCompanions.dll")).hexdigest() == dll_hash
        assert json.loads(z.read("Mods/SmartCompanions/mod.json"))["version"] == VERSION
        assert not any("Smoke" in name or name.endswith(".pdb") for name in z.namelist())
        entries = z.namelist()
    sha = digest(archive)
    archive.with_suffix(".zip.sha256").write_text(f"{sha}  {archive.name}\n", encoding="ascii")
    result = {"zip": str(archive), "size": archive.stat().st_size,
              "sha256": sha, "dll_sha256": dll_hash, "entries": entries}
    (dist / f"package-{VERSION}.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
