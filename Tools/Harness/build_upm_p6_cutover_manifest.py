#!/usr/bin/env python3
"""Build deterministic identity evidence for the UPM-P6 package-root cutover."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess


ROOT = pathlib.Path(__file__).resolve().parents[2]
OLD_ROOT = pathlib.PurePosixPath("Assets/Scripts/MxFramework")
NEW_ROOT = pathlib.PurePosixPath("Packages/com.vincent.wgameframework")
OLD_ROOT_META = pathlib.PurePosixPath("Assets/Scripts/MxFramework.meta")
NEW_ROOT_META = pathlib.PurePosixPath("Packages/com.vincent.wgameframework.meta")
DEFAULT_OUTPUT = ROOT / "Docs/UPM/Evidence/UPM-P6/cutover-manifest.json"


def run_git(*args: str, text: bool = True) -> str | bytes:
    result = subprocess.run(
        ["git", *args],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=text,
    )
    return result.stdout


def repo_path(path: pathlib.PurePosixPath) -> pathlib.Path:
    return ROOT.joinpath(*path.parts)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def source_blobs(revision: str) -> dict[pathlib.PurePosixPath, bytes]:
    listing = run_git(
        "ls-tree",
        "-r",
        "--name-only",
        "-z",
        revision,
        OLD_ROOT.as_posix(),
    )
    paths = [
        pathlib.PurePosixPath(path)
        for path in listing.split("\0")
        if path
    ]
    paths.append(OLD_ROOT_META)
    requests = b"".join(
        f"{revision}:{path.as_posix()}\n".encode("utf-8") for path in paths
    )
    process = subprocess.Popen(
        ["git", "cat-file", "--batch"],
        cwd=ROOT,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    stdout, stderr = process.communicate(requests)
    if process.returncode != 0:
        raise RuntimeError(stderr.decode("utf-8", errors="replace"))
    files: dict[pathlib.PurePosixPath, bytes] = {}
    offset = 0
    for path in paths:
        header_end = stdout.index(b"\n", offset)
        header = stdout[offset:header_end].decode("ascii")
        fields = header.split()
        if len(fields) != 3 or fields[1] != "blob":
            raise RuntimeError(f"Unexpected git cat-file response: {header}")
        size = int(fields[2])
        content_start = header_end + 1
        content_end = content_start + size
        files[path] = stdout[content_start:content_end]
        if stdout[content_end:content_end + 1] != b"\n":
            raise RuntimeError(f"Missing cat-file record terminator for {path}")
        offset = content_end + 1
    return files


def meta_guid(data: bytes) -> str:
    match = re.search(rb"^guid:\s*([0-9a-fA-F]+)\s*$", data, re.MULTILINE)
    return match.group(1).decode("ascii").lower() if match else ""


def asmdef_name(data: bytes) -> str:
    try:
        value = json.loads(data.decode("utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return ""
    name = value.get("name")
    return name if isinstance(name, str) else ""


def build_entry(source_files: dict[pathlib.PurePosixPath, bytes], old_path: pathlib.PurePosixPath) -> dict[str, object]:
    relative = old_path.relative_to(OLD_ROOT)
    new_path = NEW_ROOT / relative
    source = source_files[old_path]
    target_path = repo_path(new_path)
    target_exists = target_path.is_file()
    target = target_path.read_bytes() if target_exists else b""
    source_hash = sha256(source)
    target_hash = sha256(target) if target_exists else ""
    entry: dict[str, object] = {
        "oldPath": old_path.as_posix(),
        "newPath": new_path.as_posix(),
        "sourceSha256": source_hash,
        "targetSha256": target_hash,
        "targetExists": target_exists,
        "identityPreserved": target_exists and source_hash == target_hash,
    }
    if old_path.suffix == ".meta":
        entry["metaGuid"] = meta_guid(source)
    if old_path.suffix == ".asmdef":
        entry["asmdefName"] = asmdef_name(source)
        entry["asmdefSha256"] = source_hash
    return entry


def tree_hash(entries: list[dict[str, object]], hash_key: str) -> str:
    digest = hashlib.sha256()
    for entry in entries:
        relative = pathlib.PurePosixPath(str(entry["oldPath"])).relative_to(OLD_ROOT)
        digest.update(relative.as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(str(entry[hash_key]).encode("ascii"))
        digest.update(b"\n")
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-revision", default="HEAD")
    parser.add_argument("--output", type=pathlib.Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--allow-target-missing",
        action="store_true",
        help="Freeze a pre-move baseline before the target root exists.",
    )
    parser.add_argument(
        "--allowed-content-change",
        action="append",
        default=[],
        help="Package-relative path allowed to change after the pure move.",
    )
    args = parser.parse_args()

    source_revision = str(run_git("rev-parse", args.source_revision)).strip()
    archived_files = source_blobs(source_revision)
    old_files = sorted(
        path for path in archived_files
        if path != OLD_ROOT_META and path.is_relative_to(OLD_ROOT)
    )
    entries = [build_entry(archived_files, path) for path in old_files]
    allowed_changes = {
        pathlib.PurePosixPath(path).as_posix()
        for path in args.allowed_content_change
    }
    observed_changes: set[str] = set()
    for entry in entries:
        relative = pathlib.PurePosixPath(str(entry["oldPath"])).relative_to(OLD_ROOT).as_posix()
        changed = bool(entry["targetExists"]) and not bool(entry["identityPreserved"])
        if changed:
            observed_changes.add(relative)
        entry["contentChangeAllowed"] = changed and relative in allowed_changes
    preserved = sum(bool(entry["identityPreserved"]) for entry in entries)
    target_missing = sum(not bool(entry["targetExists"]) for entry in entries)
    unexpected_changes = sorted(observed_changes - allowed_changes)
    missing_allowed_changes = sorted(allowed_changes - observed_changes)

    root_meta = archived_files[OLD_ROOT_META]
    root_meta_guid = meta_guid(root_meta)
    old_root_exists = repo_path(OLD_ROOT).exists()
    old_root_meta_exists = repo_path(OLD_ROOT_META).exists()
    new_root_meta_exists = repo_path(NEW_ROOT_META).exists()
    target_tree_hash = tree_hash(entries, "targetSha256") if target_missing == 0 else ""
    report = {
        "schemaVersion": 1,
        "phase": "UPM-P6",
        "sourceRevision": source_revision,
        "packageIdentity": "com.vincent.wgameframework",
        "oldRoot": OLD_ROOT.as_posix(),
        "newRoot": NEW_ROOT.as_posix(),
        "summary": {
            "trackedEntryCount": len(entries),
            "identityPreservedCount": preserved,
            "targetMissingCount": target_missing,
            "allowedContentChangeCount": len(observed_changes & allowed_changes),
            "unexpectedContentChangeCount": len(unexpected_changes),
            "missingAllowedContentChangeCount": len(missing_allowed_changes),
            "sourceTreeHash": tree_hash(entries, "sourceSha256"),
            "targetTreeHash": target_tree_hash,
            "oldRootExists": old_root_exists,
            "newRootExists": repo_path(NEW_ROOT).is_dir(),
        },
        "retiredRootFolderMeta": {
            "oldPath": OLD_ROOT_META.as_posix(),
            "guid": root_meta_guid,
            "sha256": sha256(root_meta),
            "existsInWorkingTree": old_root_meta_exists,
            "disposition": "retired-root-folder-meta",
        },
        "targetRootMeta": {
            "path": NEW_ROOT_META.as_posix(),
            "existsInWorkingTree": new_root_meta_exists,
            "expected": False,
        },
        "contentChangePolicy": {
            "allowedRelativePaths": sorted(allowed_changes),
            "observedRelativePaths": sorted(observed_changes),
            "unexpectedRelativePaths": unexpected_changes,
            "missingAllowedRelativePaths": missing_allowed_changes,
        },
        "entries": entries,
    }
    output = args.output if args.output.is_absolute() else ROOT / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(
        f"Wrote {output.relative_to(ROOT).as_posix()}: "
        f"{preserved}/{len(entries)} identities preserved, "
        f"{target_missing} targets missing."
    )
    if new_root_meta_exists:
        return 1
    if args.allow_target_missing:
        return 0
    all_changes_accounted = preserved + len(observed_changes & allowed_changes) == len(entries)
    return 0 if (
        target_missing == 0
        and all_changes_accounted
        and not unexpected_changes
        and not missing_allowed_changes
        and not old_root_exists
    ) else 1


if __name__ == "__main__":
    raise SystemExit(main())
