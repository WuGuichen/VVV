#!/usr/bin/env python3
"""Build and verify the UPM-P5 project-only Editor move manifest."""

from __future__ import annotations

import hashlib
import json
import pathlib
import re
import subprocess


ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Docs/UPM/Evidence/UPM-P5/editor-owner-move-manifest.json"

PREFIX_MOVES = {
    "Assets/Scripts/MxFramework/Combat.Editor/":
        "Assets/Scripts/WGameFrameworkProject/Editor/Combat/",
    "Assets/Scripts/MxFramework/Vfx.Unity.VfxGraph.Editor/":
        "Assets/Scripts/WGameFrameworkProject/Demos/Editor/Optional/VfxGraph/",
}

FILE_MOVES = {
    **{
        f"Assets/Scripts/MxFramework/Editor/{name}":
            f"Assets/Scripts/WGameFrameworkProject/Editor/CharacterTestVfx/{name}"
        for name in (
            "CharacterTestArcMineVfxPrefabBuilder.cs",
            "CharacterTestAreaConfinementVfxPrefabBuilder.cs",
            "CharacterTestChainLightningVfxPrefabBuilder.cs",
            "CharacterTestChannelBeamVfxPrefabBuilder.cs",
            "CharacterTestLaserLanceVfxPrefabBuilder.cs",
            "CharacterTestPhysicalShieldVfxPrefabBuilder.cs",
            "CharacterTestPointToPointLaserLanceVfxPrefabBuilder.cs",
            "CharacterTestPoisonBottleVfxPrefabBuilder.cs",
            "CharacterTestRuneTurretVfxPrefabBuilder.cs",
            "CharacterTestTornadoVfxPrefabBuilder.cs",
        )
    },
    "Assets/Scripts/MxFramework/Editor/MxFairyGuiArtSliceCatalogWindow.cs":
        "Assets/Scripts/WGameFrameworkProject/Editor/FairyGUI/MxFairyGuiArtSliceCatalogWindow.cs",
    "Assets/Scripts/MxFramework/Editor/MxFairyGuiAtlasImportProcessor.cs":
        "Assets/Scripts/WGameFrameworkProject/Editor/FairyGUI/MxFairyGuiAtlasImportProcessor.cs",
}


def git_bytes(path: str) -> bytes:
    result = subprocess.run(
        ["git", "show", f"HEAD:{path}"],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    )
    if result.returncode != 0:
        history = subprocess.run(
            ["git", "log", "-1", "--format=%H", "--", path],
            cwd=ROOT,
            check=True,
            stdout=subprocess.PIPE,
            text=True,
        ).stdout.strip()
        if not history:
            raise FileNotFoundError(f"No Git history found for {path}")
        result = subprocess.run(
            ["git", "show", f"{history}^:{path}"],
            cwd=ROOT,
            check=True,
            stdout=subprocess.PIPE,
        )
    return result.stdout


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def meta_guid(data: bytes) -> str:
    match = re.search(rb"^guid:\s*([0-9a-fA-F]+)\s*$", data, re.MULTILINE)
    return match.group(1).decode("ascii").lower() if match else ""


def discover_moves() -> dict[str, str]:
    moves = dict(FILE_MOVES)
    for old_prefix, target_prefix in PREFIX_MOVES.items():
        target_root = ROOT / pathlib.Path(*pathlib.PurePosixPath(target_prefix).parts)
        for path in target_root.rglob("*"):
            if path.is_file() and path.suffix != ".meta":
                relative = path.relative_to(target_root).as_posix()
                moves[old_prefix + relative] = target_prefix + relative
    return moves


def main() -> int:
    entries = []
    failures = []
    for source, target in sorted(discover_moves().items()):
        source_bytes = git_bytes(source)
        source_meta = git_bytes(source + ".meta")
        target_path = ROOT / pathlib.Path(*pathlib.PurePosixPath(target).parts)
        target_meta_path = pathlib.Path(str(target_path) + ".meta")
        target_bytes = target_path.read_bytes()
        target_meta = target_meta_path.read_bytes()
        source_hash = sha(source_bytes)
        target_hash = sha(target_bytes)
        source_guid = meta_guid(source_meta)
        target_guid = meta_guid(target_meta)
        match = source_hash == target_hash and source_guid == target_guid and bool(source_guid)
        if not match:
            failures.append(source)
        entries.append({
            "source": source,
            "target": target,
            "sourceSha256": source_hash,
            "targetSha256": target_hash,
            "guid": target_guid,
            "identityPreserved": match,
        })

    report = {
        "schemaVersion": 1,
        "phase": "UPM-P5",
        "state": "moved-working-tree",
        "summary": {
            "entryCount": len(entries),
            "identityPreservedCount": len(entries) - len(failures),
            "failureCount": len(failures),
            "deletionCount": 0,
        },
        "entries": entries,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        f"Wrote {OUTPUT.relative_to(ROOT).as_posix()}: "
        f"{len(entries) - len(failures)}/{len(entries)} identities preserved."
    )
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
