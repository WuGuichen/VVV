#!/usr/bin/env python3
"""Apply the reviewed UPM-P2 move manifest with paired-meta rollback safety."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
from typing import Any


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = (
    REPOSITORY_ROOT
    / "Docs/UPM/Evidence/UPM-P2/character-test-move-manifest.json"
)
ALLOWED_SOURCE_ROOTS = (
    pathlib.PurePosixPath("Assets/Scripts/MxFramework/Game/CharacterTest"),
    pathlib.PurePosixPath("Assets/Scripts/MxFramework/Game/CharacterTest.Editor"),
    pathlib.PurePosixPath("Assets/Scripts/MxFramework/Tests"),
)
TARGET_ROOT = pathlib.PurePosixPath(
    "Assets/Scripts/WGameFrameworkProject/CharacterTest"
)


def is_within(path: pathlib.PurePosixPath, root: pathlib.PurePosixPath) -> bool:
    return path == root or root in path.parents


def read_guid(path: pathlib.Path) -> str:
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("guid: "):
            return line[6:].strip().lower()
    raise RuntimeError(f"Unity meta has no GUID: {path}")


def content_hash(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_manifest(path: pathlib.Path) -> dict[str, Any]:
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    if raw.get("schemaVersion") != 1 or raw.get("phase") != "UPM-P2":
        raise RuntimeError("unsupported UPM-P2 move manifest")
    if raw.get("state") not in {"planned", "applied-working-tree"}:
        raise RuntimeError("UPM-P2 move manifest has an unsupported state")
    if raw.get("targetRoot") != TARGET_ROOT.as_posix():
        raise RuntimeError("UPM-P2 target root does not match the accepted decision")
    moves = raw.get("moves")
    if not isinstance(moves, list) or not moves:
        raise RuntimeError("UPM-P2 move manifest has no move entries")
    return raw


def validate_entry(entry: dict[str, Any]) -> None:
    source = pathlib.PurePosixPath(str(entry["source"]))
    target = pathlib.PurePosixPath(str(entry["target"]))
    meta_source = pathlib.PurePosixPath(str(entry["metaSource"]))
    meta_target = pathlib.PurePosixPath(str(entry["metaTarget"]))
    if not any(is_within(source, root) for root in ALLOWED_SOURCE_ROOTS):
        raise RuntimeError(f"move source is outside the approved roots: {source}")
    if not is_within(target, TARGET_ROOT):
        raise RuntimeError(f"move target is outside the approved root: {target}")
    if meta_source.as_posix() != source.as_posix() + ".meta":
        raise RuntimeError(f"invalid source meta pairing: {source}")
    if meta_target.as_posix() != target.as_posix() + ".meta":
        raise RuntimeError(f"invalid target meta pairing: {target}")
    source_path = REPOSITORY_ROOT / source
    meta_source_path = REPOSITORY_ROOT / meta_source
    target_path = REPOSITORY_ROOT / target
    meta_target_path = REPOSITORY_ROOT / meta_target
    if not source_path.exists() or not meta_source_path.is_file():
        raise RuntimeError(f"move source/meta is missing: {source}")
    if target_path.exists() or meta_target_path.exists():
        raise RuntimeError(f"move target/meta already exists: {target}")
    if read_guid(meta_source_path) != str(entry["guid"]).lower():
        raise RuntimeError(f"move source GUID drifted: {source}")
    expected_hash = entry.get("contentHash")
    if source_path.is_file() and content_hash(source_path) != expected_hash:
        raise RuntimeError(f"move source content drifted: {source}")
    if source_path.is_dir() and expected_hash is not None:
        raise RuntimeError(f"folder move unexpectedly has a content hash: {source}")


def validate_manifest(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    entries = list(manifest["moves"])
    validate_identity_sets(entries)
    for entry in entries:
        validate_entry(entry)
    return entries


def validate_identity_sets(entries: list[dict[str, Any]]) -> None:
    sources = [str(entry["source"]) for entry in entries]
    targets = [str(entry["target"]) for entry in entries]
    guids = [str(entry["guid"]).lower() for entry in entries]
    if len(sources) != len(set(sources)):
        raise RuntimeError("UPM-P2 move manifest has duplicate sources")
    if len(targets) != len(set(targets)):
        raise RuntimeError("UPM-P2 move manifest has duplicate targets")
    if len(guids) != len(set(guids)):
        raise RuntimeError("UPM-P2 move manifest has duplicate GUIDs")


def remove_empty_target_directories() -> None:
    target = REPOSITORY_ROOT / TARGET_ROOT
    if not target.exists():
        return
    directories = sorted(
        (item for item in target.rglob("*") if item.is_dir()),
        key=lambda item: len(item.parts),
        reverse=True,
    )
    for directory in directories:
        if not any(directory.iterdir()):
            directory.rmdir()
    if target.exists() and not any(target.iterdir()):
        target.rmdir()
    parent = target.parent
    if parent.exists() and not any(parent.iterdir()):
        parent.rmdir()


def apply_moves(entries: list[dict[str, Any]]) -> None:
    file_entries = [
        entry
        for entry in entries
        if (REPOSITORY_ROOT / entry["source"]).is_file()
    ]
    folder_entries = [
        entry
        for entry in entries
        if (REPOSITORY_ROOT / entry["source"]).is_dir()
    ]
    moved_pairs: list[tuple[pathlib.Path, pathlib.Path]] = []

    try:
        for entry in sorted(file_entries, key=lambda item: item["source"]):
            source = REPOSITORY_ROOT / entry["source"]
            target = REPOSITORY_ROOT / entry["target"]
            meta_source = REPOSITORY_ROOT / entry["metaSource"]
            meta_target = REPOSITORY_ROOT / entry["metaTarget"]
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(source), str(target))
            moved_pairs.append((target, source))
            shutil.move(str(meta_source), str(meta_target))
            moved_pairs.append((meta_target, meta_source))

        for entry in sorted(
            folder_entries,
            key=lambda item: len(pathlib.PurePosixPath(item["source"]).parts),
            reverse=True,
        ):
            source = REPOSITORY_ROOT / entry["source"]
            target = REPOSITORY_ROOT / entry["target"]
            meta_source = REPOSITORY_ROOT / entry["metaSource"]
            meta_target = REPOSITORY_ROOT / entry["metaTarget"]
            target.mkdir(parents=True, exist_ok=True)
            if any(source.iterdir()):
                remaining = sorted(item.name for item in source.iterdir())
                raise RuntimeError(
                    f"source folder is not empty after file moves: {source}; "
                    f"remaining={remaining}"
                )
            shutil.move(str(meta_source), str(meta_target))
            moved_pairs.append((meta_target, meta_source))
            source.rmdir()
    except Exception:
        for current, original in reversed(moved_pairs):
            if not current.exists():
                continue
            original.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(current), str(original))
        remove_empty_target_directories()
        raise


def verify_targets(
    entries: list[dict[str, Any]],
    *,
    require_original_content: bool,
) -> int:
    evolved_file_count = 0
    for entry in entries:
        source = REPOSITORY_ROOT / entry["source"]
        target = REPOSITORY_ROOT / entry["target"]
        meta_source = REPOSITORY_ROOT / entry["metaSource"]
        meta_target = REPOSITORY_ROOT / entry["metaTarget"]
        if source.exists() or meta_source.exists():
            raise RuntimeError(f"source remains after move: {entry['source']}")
        if not target.exists() or not meta_target.is_file():
            raise RuntimeError(f"target/meta missing after move: {entry['target']}")
        if read_guid(meta_target) != str(entry["guid"]).lower():
            raise RuntimeError(f"target GUID changed: {entry['target']}")
        expected_hash = entry.get("contentHash")
        if (target.is_file() and expected_hash
                and content_hash(target) != expected_hash):
            if require_original_content:
                raise RuntimeError(f"target content changed during move: {entry['target']}")
            evolved_file_count += 1
    return evolved_file_count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        type=pathlib.Path,
        default=DEFAULT_MANIFEST,
        help="Reviewed UPM-P2 move manifest",
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Apply the move after completing all preflight checks",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = load_manifest(args.manifest.resolve())
    if manifest["state"] == "applied-working-tree":
        if args.apply:
            raise RuntimeError("UPM-P2 move manifest has already been applied")
        entries = list(manifest["moves"])
        validate_identity_sets(entries)
        evolved_file_count = verify_targets(
            entries,
            require_original_content=False,
        )
        print(
            f"UPM-P2 applied move verification passed: {len(entries)} entries; "
            f"post-move content changes={evolved_file_count}"
        )
        return 0
    entries = validate_manifest(manifest)
    print(f"UPM-P2 move preflight passed: {len(entries)} entries")
    if not args.apply:
        return 0
    apply_moves(entries)
    verify_targets(entries, require_original_content=True)
    print(f"UPM-P2 move applied: {len(entries)} entries")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
