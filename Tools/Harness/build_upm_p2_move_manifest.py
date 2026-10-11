#!/usr/bin/env python3
"""Build the deterministic UPM-P2 CharacterTest move manifest without moving assets."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
from collections import OrderedDict
from types import SimpleNamespace
from typing import Any

import check_upm_package_boundary as boundary


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
PACKAGE_ROOT = REPOSITORY_ROOT / "Assets/Scripts/MxFramework"
TARGET_ROOT = pathlib.PurePosixPath(
    "Assets/Scripts/WGameFrameworkProject/CharacterTest"
)
DEFAULT_OUTPUT = (
    REPOSITORY_ROOT
    / "Docs/UPM/Evidence/UPM-P2/character-test-move-manifest.json"
)
GUID_RE = re.compile(r"\b[0-9a-fA-F]{32}\b")
PROJECT_INPUT_RE = re.compile(
    r"(?:Assets|Tools|FGUIProject)[\\/][^\"'\r\n]+"
)
TEXT_REFERENCE_SUFFIXES = {
    ".anim",
    ".asmdef",
    ".asset",
    ".controller",
    ".cs",
    ".json",
    ".mat",
    ".meta",
    ".prefab",
    ".shader",
    ".unity",
    ".uss",
    ".uxml",
    ".yaml",
}


def git_output(*arguments: str) -> bytes:
    result = subprocess.run(
        ["git", "-C", str(REPOSITORY_ROOT), *arguments],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(
            result.stderr.decode("utf-8", errors="replace").strip()
            or f"git {' '.join(arguments)} failed"
        )
    return result.stdout


def git_paths(*pathspecs: str) -> list[pathlib.PurePosixPath]:
    payload = git_output("ls-files", "-z", "--", *pathspecs)
    return sorted(
        (
            pathlib.PurePosixPath(item.decode("utf-8", errors="surrogateescape"))
            for item in payload.split(b"\0")
            if item
        ),
        key=str,
    )


def repository_relative(path: pathlib.Path) -> pathlib.PurePosixPath:
    return pathlib.PurePosixPath(path.relative_to(REPOSITORY_ROOT).as_posix())


def read_guid(meta_path: pathlib.Path) -> str:
    if not meta_path.is_file():
        raise RuntimeError(f"Unity asset is missing meta file: {meta_path}")
    for line in meta_path.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("guid: "):
            guid = line[6:].strip().lower()
            if re.fullmatch(r"[0-9a-f]{32}", guid):
                return guid
    raise RuntimeError(f"Unity meta has no valid GUID: {meta_path}")


def content_hash(path: pathlib.Path) -> str | None:
    if path.is_dir():
        return None
    return hashlib.sha256(path.read_bytes()).hexdigest()


def asset_kind(path: pathlib.Path) -> str:
    if path.is_dir():
        return "folder"
    return path.suffix.lower().lstrip(".") or "file"


class MoveManifestBuilder:
    def __init__(self) -> None:
        self.entries: dict[str, dict[str, Any]] = {}

    def add_asset(
        self,
        source: pathlib.PurePosixPath,
        target: pathlib.PurePosixPath,
        rollback_group: str,
        assembly: str,
    ) -> None:
        source_text = source.as_posix()
        if source_text in self.entries:
            existing = self.entries[source_text]
            if existing["target"] != target.as_posix():
                raise RuntimeError(
                    f"move source has conflicting targets: {source_text}"
                )
            return
        source_path = REPOSITORY_ROOT / source
        if not source_path.exists():
            raise RuntimeError(f"move source does not exist: {source_text}")
        meta_source = pathlib.PurePosixPath(source_text + ".meta")
        meta_path = REPOSITORY_ROOT / meta_source
        self.entries[source_text] = OrderedDict(
            source=source_text,
            target=target.as_posix(),
            metaSource=meta_source.as_posix(),
            metaTarget=target.as_posix() + ".meta",
            guid=read_guid(meta_path),
            assetKind=asset_kind(source_path),
            assembly=assembly,
            rollbackGroup=rollback_group,
            contentHash=content_hash(source_path),
            serializedReferencePaths=[],
            status="planned",
        )

    def add_tree(
        self,
        source_root: pathlib.PurePosixPath,
        target_root: pathlib.PurePosixPath,
        rollback_group: str,
        assembly: str,
    ) -> None:
        source_path = REPOSITORY_ROOT / source_root
        tracked = git_paths(source_root.as_posix(), source_root.as_posix() + ".meta")
        assets: set[pathlib.PurePosixPath] = set()
        for tracked_path in tracked:
            if tracked_path.suffix == ".meta":
                candidate = pathlib.PurePosixPath(str(tracked_path)[:-5])
                if (REPOSITORY_ROOT / candidate).is_dir():
                    assets.add(candidate)
            else:
                assets.add(tracked_path)
        if source_path.is_dir():
            assets.add(source_root)
        for source in sorted(assets, key=lambda item: (len(item.parts), str(item))):
            relative = source.relative_to(source_root)
            target = target_root / relative
            self.add_asset(source, target, rollback_group, assembly)


def add_aggregate_test_closure(builder: MoveManifestBuilder) -> None:
    tests_root = pathlib.PurePosixPath("Assets/Scripts/MxFramework/Tests")
    target_root = TARGET_ROOT / "Tests/EditMode"
    tests_path = REPOSITORY_ROOT / tests_root
    mixed_roots = {
        "Application.Unity.Editor",
        "Editor",
        "Persistence",
        "PlayMode",
        "UI",
    }

    for child in sorted(tests_path.iterdir(), key=lambda item: item.name):
        if child.is_dir() and child.name not in mixed_roots:
            builder.add_tree(
                repository_relative(child),
                target_root / child.name,
                "aggregate-editmode",
                "MxFramework.Tests",
            )
        elif child.is_file() and child.suffix != ".meta":
            builder.add_asset(
                repository_relative(child),
                target_root / child.name,
                "aggregate-editmode",
                "MxFramework.Tests",
            )

    editor_root = tests_path / "Editor"
    for child in sorted(editor_root.iterdir(), key=lambda item: item.name):
        if child.name in {"McpForUnity", "McpForUnity.meta"}:
            continue
        if child.is_dir():
            builder.add_tree(
                repository_relative(child),
                target_root / "Editor" / child.name,
                "aggregate-editmode",
                "MxFramework.Tests",
            )
        elif child.is_file() and child.suffix != ".meta":
            builder.add_asset(
                repository_relative(child),
                target_root / "Editor" / child.name,
                "aggregate-editmode",
                "MxFramework.Tests",
            )

    for partial_root_name, kept_child in (
        ("Persistence", "RuntimeBridge"),
        ("UI", "FairyGUI"),
    ):
        partial_root = tests_path / partial_root_name
        for child in sorted(partial_root.iterdir(), key=lambda item: item.name):
            if child.name in {kept_child, kept_child + ".meta"}:
                continue
            if child.is_dir():
                builder.add_tree(
                    repository_relative(child),
                    target_root / partial_root_name / child.name,
                    "aggregate-editmode",
                    "MxFramework.Tests",
                )
            elif child.is_file() and child.suffix != ".meta":
                builder.add_asset(
                    repository_relative(child),
                    target_root / partial_root_name / child.name,
                    "aggregate-editmode",
                    "MxFramework.Tests",
                )


def assembly_closure() -> list[dict[str, Any]]:
    asmdefs: dict[str, dict[str, Any]] = {}
    for path in PACKAGE_ROOT.rglob("*.asmdef"):
        raw = json.loads(path.read_text(encoding="utf-8-sig"))
        name = str(raw["name"])
        asmdefs[name] = {
            "path": path.relative_to(PACKAGE_ROOT).as_posix(),
            "references": list(raw.get("references") or []),
        }
    moved = {
        "MxFramework.Game.CharacterTest",
        "MxFramework.Game.CharacterTest.Editor",
        "MxFramework.Tests",
        "MxFramework.Tests.PlayMode.CharacterTest",
    }
    result: list[dict[str, Any]] = []
    for name in sorted(moved):
        reverse = sorted(
            source
            for source, definition in asmdefs.items()
            if name in definition["references"]
        )
        result.append(
            OrderedDict(
                assemblyName=name,
                asmdefPath=asmdefs[name]["path"],
                surfaceOwner="repository-validation",
                references=asmdefs[name]["references"],
                reverseDependencies=reverse,
            )
        )
    return result


def scan_guid_references(entries: list[dict[str, Any]]) -> None:
    by_guid = {entry["guid"]: entry for entry in entries}
    for path in git_paths("."):
        if path.suffix.lower() not in TEXT_REFERENCE_SUFFIXES:
            continue
        physical = REPOSITORY_ROOT / path
        try:
            text = physical.read_text(encoding="utf-8-sig", errors="ignore")
        except OSError:
            continue
        found = {match.group(0).lower() for match in GUID_RE.finditer(text)}
        for guid in found.intersection(by_guid):
            entry = by_guid[guid]
            if path.as_posix() != entry["metaSource"]:
                entry["serializedReferencePaths"].append(path.as_posix())
    for entry in entries:
        entry["serializedReferencePaths"].sort()


def scan_project_inputs(entries: list[dict[str, Any]]) -> list[dict[str, str]]:
    inputs: set[tuple[str, str]] = set()
    for entry in entries:
        source = pathlib.PurePosixPath(entry["source"])
        if source.suffix.lower() != ".cs":
            continue
        path = REPOSITORY_ROOT / source
        text = path.read_text(encoding="utf-8-sig", errors="ignore")
        for match in PROJECT_INPUT_RE.finditer(text):
            value = match.group(0).replace("\\", "/").strip()
            inputs.add((source.as_posix(), value))
    return [
        OrderedDict(sourcePath=source, projectInput=value)
        for source, value in sorted(inputs)
    ]


def build_manifest() -> dict[str, Any]:
    builder = MoveManifestBuilder()
    builder.add_tree(
        pathlib.PurePosixPath("Assets/Scripts/MxFramework/Game/CharacterTest"),
        TARGET_ROOT,
        "runtime",
        "MxFramework.Game.CharacterTest",
    )
    builder.add_tree(
        pathlib.PurePosixPath("Assets/Scripts/MxFramework/Game/CharacterTest.Editor"),
        TARGET_ROOT / "Editor",
        "editor",
        "MxFramework.Game.CharacterTest.Editor",
    )
    builder.add_tree(
        pathlib.PurePosixPath(
            "Assets/Scripts/MxFramework/Tests/PlayMode/CharacterTest"
        ),
        TARGET_ROOT / "Tests/PlayMode",
        "character-test-playmode",
        "MxFramework.Tests.PlayMode.CharacterTest",
    )
    add_aggregate_test_closure(builder)
    entries = [builder.entries[key] for key in sorted(builder.entries)]
    scan_guid_references(entries)

    boundary_report, boundary_exit = boundary.build_report(
        SimpleNamespace(
            package_root=PACKAGE_ROOT,
            expected_package_name=boundary.DEFAULT_PACKAGE_NAME,
            candidate_commit=None,
            require_surface_manifest=True,
            require_release_metadata=False,
        )
    )
    if boundary_exit != 0:
        raise RuntimeError("strict E0 must pass before building the P2 manifest")

    counts: dict[str, int] = {}
    for entry in entries:
        counts[entry["rollbackGroup"]] = counts.get(entry["rollbackGroup"], 0) + 1

    return OrderedDict(
        schemaVersion=1,
        phase="UPM-P2",
        state="planned",
        repositoryCommit=git_output("rev-parse", "HEAD").decode().strip(),
        repositoryDirty=bool(git_output("status", "--porcelain").strip()),
        sourcePackageTreeHash=boundary_report["package"]["treeHash"],
        targetRoot=TARGET_ROOT.as_posix(),
        assemblyClosure=assembly_closure(),
        rollbackGroupCounts=OrderedDict(sorted(counts.items())),
        moveCount=len(entries),
        moves=entries,
        externalProjectInputs=scan_project_inputs(entries),
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=pathlib.Path,
        default=DEFAULT_OUTPUT,
        help="Output manifest path",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    manifest = build_manifest()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(
        "UPM-P2 move manifest written: "
        f"{output} ({manifest['moveCount']} move entries)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
