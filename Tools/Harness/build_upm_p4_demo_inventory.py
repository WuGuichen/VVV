#!/usr/bin/env python3
"""Build the frozen UPM-P4 Demo owner and sample-candidate inventory."""

from __future__ import annotations

import hashlib
import json
import pathlib
import re


REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
DEMO_ROOT = REPO_ROOT / "Assets/Scripts/MxFramework/Demo"
TARGET_ROOT = pathlib.PurePosixPath("Assets/Scripts/WGameFrameworkProject/Demos")
TARGET_ROOT_FS = REPO_ROOT / pathlib.Path(*TARGET_ROOT.parts)
OUTPUT = REPO_ROOT / "Docs/UPM/Evidence/UPM-P4/demo-sample-inventory.json"
SURFACE = REPO_ROOT / "Assets/Scripts/MxFramework/upm-surface.json"

FORBIDDEN_PATTERNS = {
    "assets-config": re.compile(r"Assets/Config", re.IGNORECASE),
    "assets-vfx": re.compile(r"Assets/VFX", re.IGNORECASE),
    "assets-bundles": re.compile(r"Assets/Bundles", re.IGNORECASE),
    "fgui-project": re.compile(r"FGUIProject", re.IGNORECASE),
    "tools": re.compile(r"(?:^|[\\/])Tools[\\/]", re.IGNORECASE),
    "fmod": re.compile(r"FMOD|MXFRAMEWORK_FMOD"),
    "fairygui": re.compile(r"FairyGUI|MXFRAMEWORK_FAIRYGUI"),
    "character-test": re.compile(r"CharacterTest", re.IGNORECASE),
}

TEXT_SUFFIXES = {".asmdef", ".cs", ".json", ".md", ".txt", ".uss", ".uxml", ".unity", ".asset"}


def posix(path: pathlib.Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_guid(path: pathlib.Path) -> str:
    meta = pathlib.Path(str(path) + ".meta")
    if not meta.is_file():
        return ""
    match = re.search(r"^guid:\s*([0-9a-fA-F]+)\s*$", meta.read_text(encoding="utf-8"), re.MULTILINE)
    return match.group(1).lower() if match else ""


def classify(path: pathlib.Path) -> str:
    if path.suffix == ".asmdef":
        return "assembly"
    if path.suffix == ".cs":
        return "source"
    return "asset"


def forbidden_references(path: pathlib.Path) -> list[str]:
    if path.suffix.lower() not in TEXT_SUFFIXES:
        return []
    text = path.read_text(encoding="utf-8", errors="replace")
    return sorted(name for name, pattern in FORBIDDEN_PATTERNS.items() if pattern.search(text))


def main() -> int:
    surface = json.loads(SURFACE.read_text(encoding="utf-8"))["assemblies"]
    scan_root = DEMO_ROOT if DEMO_ROOT.is_dir() else TARGET_ROOT_FS
    if not scan_root.is_dir():
        raise FileNotFoundError("Neither the P4 source nor repository target Demo root exists.")
    files = []
    groups: dict[str, dict[str, int]] = {}
    assemblies = []

    for path in sorted(scan_root.rglob("*")):
        if not path.is_file() or path.suffix == ".meta":
            continue
        relative = path.relative_to(scan_root).as_posix()
        group = relative.split("/", 1)[0] if "/" in relative else "_root"
        stats = groups.setdefault(group, {"fileCount": 0, "byteCount": 0, "forbiddenReferenceFileCount": 0})
        findings = forbidden_references(path)
        stats["fileCount"] += 1
        stats["byteCount"] += path.stat().st_size
        if findings:
            stats["forbiddenReferenceFileCount"] += 1

        entry = {
            "path": (pathlib.PurePosixPath("Assets/Scripts/MxFramework/Demo") / relative).as_posix(),
            "relativePath": relative,
            "targetPath": (TARGET_ROOT / relative).as_posix(),
            "kind": classify(path),
            "sizeBytes": path.stat().st_size,
            "sha256": sha256(path),
            "guid": read_guid(path),
            "forbiddenReferences": findings,
            "owner": "repository-validation",
        }
        files.append(entry)

        if path.suffix == ".asmdef":
            data = json.loads(path.read_text(encoding="utf-8"))
            name = data["name"]
            assemblies.append(
                {
                    "name": name,
                    "path": posix(path),
                    "guid": entry["guid"],
                    "surface": surface.get(name, "unclassified"),
                    "references": data.get("references", []),
                    "defineConstraints": data.get("defineConstraints", []),
                    "versionDefines": data.get("versionDefines", []),
                }
            )

    inventory = {
        "schemaVersion": 1,
        "phase": "UPM-P4",
        "state": "inventory-frozen",
        "sourceRoot": posix(DEMO_ROOT),
        "repositoryTargetRoot": TARGET_ROOT.as_posix(),
        "summary": {
            "fileCountExcludingMeta": len(files),
            "sourceFileCount": sum(item["kind"] == "source" for item in files),
            "assemblyCount": len(assemblies),
            "byteCount": sum(item["sizeBytes"] for item in files),
            "missingGuidCount": sum(not item["guid"] for item in files),
            "forbiddenReferenceFileCount": sum(bool(item["forbiddenReferences"]) for item in files),
            "plannedDeletionCount": 0,
        },
        "groups": [{"name": name, **stats} for name, stats in sorted(groups.items())],
        "assemblies": assemblies,
        "sampleDecision": {
            "approved": "RuntimeHost Minimal Lifecycle",
            "directory": "Samples~/RuntimeHostMinimalLifecycle",
            "assembly": "MxFramework.Samples.RuntimeHostMinimalLifecycle",
            "baseReferences": ["MxFramework.Runtime"],
            "observableContract": "Initialize -> Start -> Tick observed -> Stop -> Dispose",
            "thirdPartyContent": "none",
            "rejectedPrimaryCandidate": "RuntimeVerticalSlice self-contained extraction",
            "rejectionReason": "The current authority is coupled to the repository Demo aggregate, FMOD, project Config, VFX, Bundles and showcase assets; extracting it would copy project-owned validation input into a base sample.",
            "doesNotReplace": "Assets/Scenes/RuntimeVerticalSlice.unity and its repository validation coverage",
        },
        "files": files,
    }

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT.relative_to(REPO_ROOT).as_posix()}: {len(files)} files, {len(assemblies)} assemblies.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
