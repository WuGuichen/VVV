#!/usr/bin/env python3
"""Validate numeric character identity and optional DCC source conventions."""

from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath, PureWindowsPath


NUMERIC_ID = re.compile(r"^[0-9]{6}$")
SHA256 = re.compile(r"^sha256:[0-9a-f]{64}$")
MANAGED_ROOTS = {"AI_Raw", "references", "Blender_Work", "Final_Source"}
ARCHIVE_ROOT = ("Blender_Work", "publish_backups")
LEGACY_MIGRATION_FORMATS = {
    "mx.wgameLegacyPalettePreserveMigration.v1",
    "mx.wgameColorPaletteMigrationReport.v2",
    "mx.wgameSurfacePaletteMigrationReport.v1",
}

FORBIDDEN_BLEND = (
    re.compile(r".*\.blend[12]$", re.IGNORECASE),
    re.compile(r".*_backup_.*\.blend$", re.IGNORECASE),
    re.compile(r".*_autosave\.blend$", re.IGNORECASE),
)


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected a JSON object")
    return value


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def is_safe_relative(value: str) -> bool:
    candidate = PurePosixPath(value)
    windows = PureWindowsPath(value)
    return (bool(value) and value != "." and not candidate.is_absolute()
            and not windows.is_absolute() and not windows.drive
            and ".." not in candidate.parts and ":" not in value)


def is_archive(relative: str) -> bool:
    parts = PurePosixPath(relative).parts
    return len(parts) > 2 and parts[:2] == ARCHIVE_ROOT


def git_paths(repo: Path, *args: str) -> set[str]:
    result = subprocess.run(["git", *args], cwd=repo, capture_output=True, check=False)
    if result.returncode:
        raise ValueError("cannot determine Git delivery scope: "
                         + result.stderr.decode("utf-8", errors="replace").strip())
    return {path for path in result.stdout.decode("utf-8").split("\0") if path}


def delivery_scope(repo: Path) -> tuple[set[str], set[str], set[str]]:
    # The index includes force-added ignored files. Untracked candidates exclude
    # Git-ignored local state; neither branch traverses the user's backup folders.
    paths = git_paths(repo, "ls-files", "--cached", "--others", "--exclude-standard",
                      "-z", "--", "ArtSource/Characters")
    historical = git_paths(repo, "ls-tree", "-r", "--name-only", "-z", "HEAD",
                           "--", "ArtSource/Characters")
    modified = git_paths(repo, "diff", "HEAD", "--name-only", "-z", "--",
                         "ArtSource/Characters")
    return paths, historical, modified


def check_lfs(repo: Path, relative: str, errors: list[str]) -> None:
    result = subprocess.run(
        ["git", "check-attr", "filter", "--", relative],
        cwd=repo,
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode != 0 or not result.stdout.rstrip().endswith(": lfs"):
        errors.append(f"{relative}: managed binary/image source must use Git LFS")


def retained_migration_paths(root: Path, repo: Path, cid: str, manifest: dict,
                             historical: set[str], modified: set[str]) -> tuple[set[str], set[str]]:
    """Keep immutable existing migration records and their explicit backup links."""
    prefix = root.relative_to(repo).as_posix() + "/"
    adopted = {item.get("path") for item in manifest.get("items") or []}
    retained, backups = set(), set()

    def linked_path(value: str) -> str | None:
        # Old records store absolute Windows paths. Resolve only their explicit
        # character-root suffix locally; never read or follow the absolute path.
        normalized = str(value).replace("\\", "/")
        marker = "ArtSource/Characters/" + cid + "/"
        at = normalized.find(marker)
        if at < 0:
            return None
        relative = normalized[at + len(marker):]
        return relative if is_safe_relative(relative) else None

    for path in sorted(historical - modified):
        if not path.startswith(prefix) or not path.endswith(".json") or not (repo / path).is_file():
            continue
        try:
            document = load_json(repo / path)
        except (OSError, ValueError):
            continue
        if document.get("format") not in LEGACY_MIGRATION_FORMATS:
            continue
        if document.get("characterId") not in (None, cid):
            continue
        if not SHA256.fullmatch(str(document.get("catalogHash") or "")):
            continue
        backup = linked_path(document.get("backupBlend") or "")
        source = linked_path(document.get("sourceBlend") or "") if document.get("sourceBlend") else None
        if (not backup or PurePosixPath(backup).suffix != ".blend"
                or (document.get("sourceBlend") and source not in adopted)):
            continue
        retained.add(path[len(prefix):])
        backup_path = prefix + backup
        if backup_path in historical and (root / backup).is_file():
            # An edited historical backup is still a backup. It loses the
            # immutable-retention exemption, but cannot become an active item.
            backups.add(backup)
            if backup_path not in modified:
                retained.add(backup)
    return retained, backups


def saved_source_evidence(repo: Path, source: Path, expected: str) -> str:
    """Verify available bytes without fetching, restoring, or following old paths."""
    if source.is_file() and file_sha256(source) == expected:
        return "saved source bytes verified at the adopted path"
    relative = source.relative_to(repo).as_posix()
    head = subprocess.run(["git", "show", "HEAD:" + relative], cwd=repo,
                          capture_output=True, check=False)
    head_matches = False
    if head.returncode == 0:
        if "sha256:" + hashlib.sha256(head.stdout).hexdigest() == expected:
            return "frozen source bytes verified in HEAD"
        pointer = re.fullmatch(
            rb"version https://git-lfs.github.com/spec/v1\noid sha256:([0-9a-f]{64})\nsize [0-9]+\n?",
            head.stdout)
        head_matches = bool(pointer and "sha256:" + pointer[1].decode() == expected)
    common = subprocess.run(["git", "rev-parse", "--git-common-dir"], cwd=repo,
                            capture_output=True, text=True, check=False)
    if common.returncode == 0:
        directory = Path(common.stdout.strip())
        if not directory.is_absolute():
            directory = repo / directory
        digest = expected.removeprefix("sha256:")
        cached = directory / "lfs/objects" / digest[:2] / digest[2:4] / digest
        if cached.is_file() and file_sha256(cached) == expected:
            if head_matches:
                return "frozen source bytes verified through HEAD and local LFS"
            return "local LFS source bytes verified; versioned linkage unverified"
    return "frozen source bytes unverified; a recorded hash does not verify source content"


def published_bake_paths(repo: Path, root: Path, cid: str, manifest: dict,
                         errors: list[str], states: list[str]) -> set[str]:
    """Recognize the existing producer registration, not artistic/import acceptance."""
    binding_path = root / "Final_Source/character_baked_surface_binding.json"
    if not binding_path.is_file():
        return set()
    package = repo / "Authoring/CharacterPackages" / ("character-" + cid)
    try:
        final = load_json(binding_path)
        published = load_json(package / "config/character_baked_surface_binding.json")
        package_manifest = load_json(package / "manifest.json")
        catalog = load_json(package / "resource_catalog.json")
        if package_manifest.get("packageId") != cid or final.get("packageId") != cid:
            raise ValueError("package identity mismatch")
        file_fields = {"modelFile", "baseColorFile", "surfaceDataFile"}
        if ({key: value for key, value in final.items() if key not in file_fields}
                != {key: value for key, value in published.items() if key not in file_fields}):
            raise ValueError("Final/package binding identity mismatch")
        source_prefix = root.relative_to(repo).as_posix() + "/"
        source_path = str(final.get("authoringSourcePath") or "")
        source_hash = str(final.get("authoringSourceHash") or "")
        if not source_path.startswith(source_prefix) or not SHA256.fullmatch(source_hash):
            raise ValueError("saved authoring source identity missing")
        if not SHA256.fullmatch(str(final.get("sourceBlendHash") or "")):
            raise ValueError("frozen bake snapshot hash missing or invalid")
        source_relative = source_path[len(source_prefix):]
        if (not is_safe_relative(source_relative) or is_archive(source_relative)
                or not any(item.get("path") == source_relative
                           and item.get("kind") in {"BlenderWork", "BlenderAuthority"}
                           for item in manifest.get("items") or [])):
            raise ValueError("published provenance does not match an adopted source")
        authoring_source = root / source_relative
        if not authoring_source.resolve().is_relative_to(root.resolve()):
            raise ValueError("adopted authoring source escapes character root")
        # Current-source registration is checked independently below. Publishing
        # freezes its own provenance; a later registered save must not rewrite it.
        outputs = ((cid + "_LOD0.fbx", "model", "modelHash", "modelFile", "body"),
                   ("BaseColor.png", "texture", "baseColorHash", "baseColorFile", "baked_base_color"),
                   ("SurfaceData.png", "texture", "surfaceDataHash", "surfaceDataFile", "baked_surface_data"))
        registered = {"Final_Source/character_baked_surface_binding.json"}
        for filename, kind, hash_field, file_field, suffix in outputs:
            digest = str(final.get(hash_field) or "")
            if not SHA256.fullmatch(digest) or final.get(file_field) != filename:
                raise ValueError("invalid Final artifact identity: " + filename)
            source = root / "Final_Source" / filename
            if not source.is_file() or file_sha256(source) != digest:
                raise ValueError("Final artifact content mismatch: " + filename)
            key = "char." + cid + "." + kind + "." + suffix
            entries = [entry for entry in catalog.get("entries") or []
                       if entry.get("resourceKey") == key]
            if len(entries) != 1:
                raise ValueError("catalog identity missing/ambiguous: " + key)
            entry = entries[0]
            relative = str(entry.get("relativePath") or "")
            hashes = entry.get("hashes") or {}
            if (not is_safe_relative(relative) or published.get(file_field) != relative
                    or entry.get("hash") != digest
                    or (hashes.get("contentHash") and hashes["contentHash"] != digest)):
                raise ValueError("catalog/package artifact identity mismatch: " + key)
            payload = package / relative
            if not payload.resolve().is_relative_to(package.resolve()):
                raise ValueError("package payload path escapes package")
            if not payload.is_file() or file_sha256(payload) != digest:
                raise ValueError("package payload content mismatch: " + key)
            check_lfs(repo, source.relative_to(repo).as_posix(), errors)
            registered.add("Final_Source/" + filename)
        states.append(f"{cid}: published artifact identity and bytes verified; "
                      + saved_source_evidence(repo, authoring_source, source_hash))
        states.append(f"{cid}: frozen bake snapshot bytes unverified "
                      "(binding retains sourceBlendHash, not a managed snapshot path)")
        if authoring_source.is_file() and file_sha256(authoring_source) != source_hash:
            states.append(f"{cid}: unpublished authoring changes; "
                          "published artifact identity and bytes remain intact")
        return registered
    except (OSError, ValueError, TypeError, AttributeError) as exc:
        errors.append(f"{binding_path.relative_to(repo).as_posix()}: published baked registration invalid: {exc}")
        return set()


def validate_manifest(repo: Path, character_root: Path, character_id: str, errors: list[str],
                       delivery: set[str], historical: set[str], modified: set[str],
                       states: list[str]) -> None:
    path = character_root / "authoring_manifest.json"
    if not path.is_file():
        errors.append(f"{path.relative_to(repo).as_posix()}: numeric character is missing authoring_manifest.json")
        return
    try:
        manifest = load_json(path)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        errors.append(str(exc))
        return
    if manifest.get("schemaVersion") != "1.0":
        errors.append(f"{path.relative_to(repo).as_posix()}: schemaVersion must be 1.0")
    if manifest.get("characterId") != character_id or manifest.get("packageId") != character_id:
        errors.append(f"{path.relative_to(repo).as_posix()}: characterId/packageId must match the folder")
    if manifest.get("sourceRoot") != ".":
        errors.append(f"{path.relative_to(repo).as_posix()}: sourceRoot must be dot")
    seen_ids: set[str] = set()
    seen_paths: set[str] = set()
    for item in manifest.get("items") or []:
        item_id = str(item.get("id") or "")
        relative = str(item.get("path") or "").replace("\\", "/")
        if not item_id or item_id in seen_ids:
            errors.append(f"{path.relative_to(repo).as_posix()}: item IDs must be non-empty and unique")
        seen_ids.add(item_id)
        if not is_safe_relative(relative) or relative in seen_paths:
            errors.append(f"{path.relative_to(repo).as_posix()}: unsafe or duplicate item path {relative!r}")
            continue
        seen_paths.add(relative)
        if is_archive(relative):
            errors.append(f"{path.relative_to(repo).as_posix()}: historical publication archive cannot be an active source: {relative}")
        source = character_root / relative
        if not source.is_file():
            errors.append(f"{source.relative_to(repo).as_posix()}: declared optional source is missing")
            continue
        declared_hash = str(item.get("sha256") or "")
        if not SHA256.match(declared_hash) or declared_hash != file_sha256(source):
            errors.append(f"{source.relative_to(repo).as_posix()}: optional source SHA-256 is missing or stale")
            if item.get("kind") == "BlenderWork":
                states.append(f"{character_id}: Work registration pending; "
                              "current managed-source SHA mismatch remains a strict DCC failure")
        if source.suffix.lower() in {".blend", ".fbx", ".png", ".jpg", ".jpeg", ".psd"}:
            check_lfs(repo, source.relative_to(repo).as_posix(), errors)
    for field in ("blenderPath", "finalFbxPath"):
        relative = str((manifest.get("authority") or {}).get(field) or "").replace("\\", "/")
        if relative and (not is_safe_relative(relative) or is_archive(relative)
                         or relative not in seen_paths):
            errors.append(f"{path.relative_to(repo).as_posix()}: {field} must reference a declared active source")

    published_paths = published_bake_paths(repo, character_root, character_id, manifest, errors, states)
    migration_paths, migration_backups = retained_migration_paths(
        character_root, repo, character_id, manifest, historical, modified)
    for item in manifest.get("items") or []:
        if item.get("path") in migration_backups:
            errors.append(f"{path.relative_to(repo).as_posix()}: retained migration backup cannot be an active source")
    for field in ("blenderPath", "finalFbxPath"):
        if (manifest.get("authority") or {}).get(field) in migration_backups:
            errors.append(f"{path.relative_to(repo).as_posix()}: retained migration backup cannot be an active authority")

    # Declared items are always checked above, including missing/ignored files.
    # Only undeclared delivery candidates are selected through Git scope.
    prefix = character_root.relative_to(repo).as_posix() + "/"
    for repo_path in sorted(delivery | (historical & modified)):
        if not repo_path.startswith(prefix):
            continue
        relative = repo_path[len(prefix):]
        parts = PurePosixPath(relative).parts
        if not parts:
            continue
        if is_archive(relative):
            if repo_path not in historical:
                errors.append(f"{repo_path}: new publication backups are local state, not deliverable active sources")
            elif repo_path in modified:
                errors.append(f"{repo_path}: retained historical publication archive changed; review separately")
            continue
        if not (repo / repo_path).is_file():
            continue
        if any(pattern.match(parts[-1]) for pattern in FORBIDDEN_BLEND):
            errors.append(f"{repo_path}: Blender backup/recovery files are forbidden in delivery")
            continue  # One diagnosis per forbidden path; adoption cannot legitimize it.
        if parts[0] in MANAGED_ROOTS and relative not in seen_paths and relative not in published_paths and relative not in migration_paths:
            errors.append(f"{repo_path}: file in a managed source directory is not declared in authoring_manifest.json")


def main(repo: Path | None = None) -> int:
    repo = repo or Path(__file__).resolve().parents[2]
    catalog_path = repo / "Authoring" / "CharacterPackages" / "character_id_catalog.json"
    exemptions_path = repo / "ArtSource" / "Characters" / "authoring_legacy_exemptions.json"
    errors: list[str] = []
    states: list[str] = []
    try:
        catalog = load_json(catalog_path)
        exemptions = load_json(exemptions_path)
        delivery, historical, modified = delivery_scope(repo)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"character authoring source check failed: {exc}", file=sys.stderr)
        return 1

    if catalog.get("schemaVersion") != "1.0" or catalog.get("width") != 6:
        errors.append("character_id_catalog.json: expected schemaVersion 1.0 and width 6")
    entries = catalog.get("entries") or []
    retired = catalog.get("retiredIds") or []
    ids = [str(item.get("id") or "") for item in entries]
    if len(ids) != len(set(ids)) or any(not NUMERIC_ID.match(value) or value == "000000" for value in ids):
        errors.append("character_id_catalog.json: entries contain duplicate or invalid IDs")
    if len(retired) != len(set(retired)) or any(not NUMERIC_ID.match(str(value)) for value in retired):
        errors.append("character_id_catalog.json: retiredIds contain duplicate or invalid IDs")

    legacy_ids = exemptions.get("characterIds") or []
    if len(legacy_ids) != len(set(legacy_ids)) or any(NUMERIC_ID.match(str(value)) for value in legacy_ids):
        errors.append("authoring_legacy_exemptions.json: entries must be unique non-numeric legacy IDs")

    art_root = repo / "ArtSource" / "Characters"
    delivered_ids = {parts[2] for name in delivery
                     if len(parts := PurePosixPath(name).parts) > 2 and NUMERIC_ID.match(parts[2])}
    # Registered characters remain subject to manifest validation even with no
    # DCC item adopted. Entirely ignored, unregistered local folders are not delivery.
    character_ids = delivered_ids | {cid for cid in ids if (art_root / cid).is_dir()}
    for cid in sorted(character_ids):
        child = art_root / cid
        if cid not in ids:
            errors.append(f"{child.relative_to(repo).as_posix()}: numeric ArtSource root is missing from the ID catalog")
        validate_manifest(repo, child, cid, errors, delivery, historical, modified, states)

    for package_dir in sorted((repo / "Authoring" / "CharacterPackages").glob("character-[0-9][0-9][0-9][0-9][0-9][0-9]")):
        character_id = package_dir.name.removeprefix("character-")
        if character_id not in ids:
            errors.append(f"{package_dir.relative_to(repo).as_posix()}: numeric package is missing from the ID catalog")
        if not (art_root / character_id).is_dir():
            errors.append(f"{package_dir.relative_to(repo).as_posix()}: matching ArtSource root is missing")

    for state in states:
        print(f"state: {state}")
    if errors:
        for error in errors:
            print(f"error: {error}", file=sys.stderr)
        return 1
    print(f"Character authoring source check passed ({len(entries)} numeric IDs, {len(legacy_ids)} legacy exemptions).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
