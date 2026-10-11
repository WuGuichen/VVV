#!/usr/bin/env python3
"""One-time CharacterAction stable-string to global numeric-id migration.

Dry-run is the default. Use --apply with the source hash printed by dry-run.
The runtime importer intentionally does not use this module and rejects legacy schemas.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import uuid
from pathlib import Path
from typing import Any


ACTION_LIBRARY_SCHEMA = "0.3.0"
PROJECTION_SCHEMA = "0.4.0"
LOGIC_SCHEMA = "mx.character-test.runtime-ai-lab.behavior-card-logic.v9"
DRAFT_CARD_SCHEMA = "mx.character-test.runtime-ai-lab.behavior-card.v3"
CATALOG_SCHEMA = "mx.character-action.id-catalog.v1"

REFERENCE_KEYS = {
    "actionId",
    "defaultActionId",
    "lightLandingActionId",
    "heavyLandingActionId",
    "characterActionId",
    "requestedActionId",
    "sourceActionId",
    "generatedActionId",
    "actionStableId",
}


def repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def action_library_paths(root: Path) -> list[Path]:
    patterns = (
        "CharacterActionSource/**/character_actions.json",
        "Authoring/CharacterPackages/**/character_actions.json",
        "Tools/MxFramework.Authoring/samples/**/character_actions.json",
        "Assets/MxFrameworkGenerated/CharacterActions/**/character_actions.json",
    )
    return sorted({path for pattern in patterns for path in root.glob(pattern)})


def migration_json_paths(root: Path) -> list[Path]:
    roots = (
        root / "CharacterActionSource",
        root / "Authoring" / "CharacterPackages",
        root / "Tools" / "MxFramework.Authoring" / "samples",
        root / "Assets" / "MxFrameworkGenerated" / "CharacterActions",
        root / "Assets" / "MxFrameworkGenerated" / "Abilities",
        root / "WeaponSource",
        root / "Authoring" / "RuntimeAiLab" / "Drafts",
    )
    return sorted({path for base in roots if base.exists() for path in base.rglob("*.json")})


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def source_hash(paths: list[Path], root: Path) -> str:
    digest = hashlib.sha256()
    for path in paths:
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return "sha256:" + digest.hexdigest()


def build_legacy_map(paths: list[Path]) -> tuple[dict[str, int], list[dict[str, Any]]]:
    mapping: dict[str, int] = {}
    allocations: list[dict[str, Any]] = []
    used: dict[int, str] = {}
    for path in paths:
        document = load_json(path)
        for action in document.get("actions", []):
            if not isinstance(action, dict):
                continue
            action_id = int(action.get("actionId", action.get("id", 0)) or 0)
            legacy = str(action.get("stableId", "") or "")
            if action_id <= 0:
                raise ValueError(f"{path}: action id must be positive")
            if action_id in used and used[action_id] != legacy:
                raise ValueError(f"numeric id collision {action_id}: {used[action_id]} vs {legacy}")
            used[action_id] = legacy
            if legacy:
                if legacy in mapping and mapping[legacy] != action_id:
                    raise ValueError(f"legacy id collision {legacy}: {mapping[legacy]} vs {action_id}")
                mapping[legacy] = action_id
            allocations.append(
                {
                    "actionId": action_id,
                    "libraryId": library_id(path),
                    "sourcePath": path.as_posix(),
                    "status": "active",
                }
            )
    unique = {item["actionId"]: item for item in allocations}
    return mapping, [unique[key] for key in sorted(unique)]


def library_id(path: Path) -> str:
    if path.parent.name == "Common":
        return "Common"
    if path.parent.name == "config":
        return path.parent.parent.name.removeprefix("character-").replace("-", "_")
    return path.parent.name


def convert_action_object(action: dict[str, Any], mapping: dict[str, int]) -> dict[str, Any]:
    legacy = str(action.get("stableId", "") or "")
    action_id = int(action.get("actionId", action.get("id", mapping.get(legacy, 0))) or 0)
    if action_id <= 0:
        raise ValueError(f"action definition has no numeric identity: {legacy or '<unnamed>'}")
    converted = {"actionId": action_id}
    for key, value in action.items():
        if key not in {"id", "stableId", "actionId"}:
            converted[key] = convert_node(value, mapping, key)
    return converted


def convert_node(value: Any, mapping: dict[str, int], parent_key: str = "") -> Any:
    if isinstance(value, dict):
        converted: dict[str, Any] = {}
        for key, child in value.items():
            if key == "animationConfigurations":
                converted[key] = repair_animation_action_ids(child, mapping)
                continue
            if key == "actions" and isinstance(child, list):
                converted[key] = [
                    convert_action_object(item, mapping)
                    if isinstance(item, dict)
                    and ("timelineAuthority" in item or "category" in item)
                    and ("id" in item or "stableId" in item or isinstance(item.get("actionId"), int))
                    else convert_node(item, mapping, key)
                    for item in child
                ]
                continue
            if key in REFERENCE_KEYS and isinstance(child, str):
                if not child:
                    converted[key] = 0
                elif child in mapping:
                    converted[key] = mapping[child]
                elif key == "characterActionId" or key == "actionStableId":
                    raise ValueError(f"unknown CharacterAction reference {child!r} at {key}")
                else:
                    converted[key] = child
                continue
            if key == "actionIds" and isinstance(child, list):
                result: list[int] = []
                for item in child:
                    if isinstance(item, int):
                        result.append(item)
                    elif isinstance(item, str) and item in mapping:
                        result.append(mapping[item])
                    else:
                        raise ValueError(f"unknown CharacterAction sequence reference {item!r}")
                converted[key] = result
                continue
            converted[key] = convert_node(child, mapping, key)
        return converted
    if isinstance(value, list):
        return [convert_node(item, mapping, parent_key) for item in value]
    return value


def repair_animation_action_ids(value: Any, mapping: dict[str, int]) -> Any:
    """Animation action keys are a separate string domain and must not be migrated."""
    inverse = {action_id: legacy for legacy, action_id in mapping.items()}
    if isinstance(value, dict):
        return {
            key: (
                inverse.get(child, child)
                if key == "actionId" and isinstance(child, int)
                else repair_animation_action_ids(child, mapping)
            )
            for key, child in value.items()
        }
    if isinstance(value, list):
        return [repair_animation_action_ids(item, mapping) for item in value]
    return value


def update_schema(path: Path, document: Any) -> None:
    if not isinstance(document, dict):
        return
    if path.name == "character_actions.json":
        document["schemaVersion"] = ACTION_LIBRARY_SCHEMA
    elif path.name in {
        "character_action_sets.json",
        "character_action_profile.json",
        "character_reaction_profiles.json",
        "character_landing_profiles.json",
    }:
        document["schemaVersion"] = PROJECTION_SCHEMA
    if document.get("schema") == "mx.character-test.runtime-ai-lab.behavior-card.v2":
        document["schema"] = DRAFT_CARD_SCHEMA
    if document.get("logicSchema") == "mx.character-test.runtime-ai-lab.behavior-card-logic.v8":
        document["logicSchema"] = LOGIC_SCHEMA
    if document.get("payloadSchema") == "mx.character-test.runtime-ai-lab.behavior-card-logic.v8":
        document["payloadSchema"] = LOGIC_SCHEMA
    if document.get("schema") == "mx.character-test.runtime-ai-lab.behavior-card-logic.v8":
        document["schema"] = LOGIC_SCHEMA
    update_behavior_recipe_version(document)


def update_behavior_recipe_version(value: Any) -> None:
    if isinstance(value, dict):
        if value.get("behaviorRecipeVersion") == 8:
            value["behaviorRecipeVersion"] = 9
        for child in value.values():
            update_behavior_recipe_version(child)
    elif isinstance(value, list):
        for child in value:
            update_behavior_recipe_version(child)


def build_catalog(root: Path, allocations: list[dict[str, Any]]) -> dict[str, Any]:
    by_library: dict[str, list[int]] = {}
    for allocation in allocations:
        allocation["sourcePath"] = Path(allocation["sourcePath"]).resolve().relative_to(root).as_posix()
        by_library.setdefault(allocation["libraryId"], []).append(allocation["actionId"])
    libraries = []
    for name in sorted(by_library):
        ids = sorted(set(by_library[name]))
        blocks: dict[int, list[int]] = {}
        for action_id in ids:
            start = (action_id // 10000) * 10000 if action_id >= 10000 else 1
            end = start + 9999 if action_id >= 10000 else 9999
            blocks.setdefault(start, [start, end, start])
            blocks[start][2] = max(blocks[start][2], action_id + 1)
        libraries.append(
            {
                "libraryId": name,
                "ranges": [
                    {"start": values[0], "end": values[1], "nextActionId": values[2]}
                    for _, values in sorted(blocks.items())
                ],
            }
        )
    catalog = {
        "schema": CATALOG_SCHEMA,
        "revision": 1,
        "catalogHash": "",
        "libraries": libraries,
        "allocations": allocations,
    }
    catalog["catalogHash"] = "sha256:" + hashlib.sha256(canonical_bytes(catalog)).hexdigest()
    return catalog


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def json_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def apply_transaction(writes: list[tuple[Path, bytes]]) -> None:
    token = uuid.uuid4().hex
    originals: dict[Path, bytes | None] = {
        path: path.read_bytes() if path.exists() else None for path, _ in writes
    }
    staging: list[tuple[Path, Path]] = []
    try:
        for path, contents in writes:
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_name(path.name + ".numeric-id-staging-" + token)
            temporary.write_bytes(contents)
            staging.append((path, temporary))
        for path, temporary in staging:
            os.replace(temporary, path)
    except Exception:
        for path, previous in originals.items():
            if previous is None:
                if path.exists():
                    path.unlink()
            else:
                rollback = path.with_name(path.name + ".numeric-id-rollback-" + token)
                rollback.write_bytes(previous)
                os.replace(rollback, path)
        raise
    finally:
        for _, temporary in staging:
            if temporary.exists():
                temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true", help="Report the migration plan without writing files (default).")
    mode.add_argument("--apply", action="store_true", help="Apply the migration transaction after hash verification.")
    parser.add_argument("--expected-source-hash", default="")
    args = parser.parse_args()
    root = repo_root()
    libraries = action_library_paths(root)
    paths = migration_json_paths(root)
    current_hash = source_hash(paths, root)
    mapping, allocations = build_legacy_map(libraries)
    receipt_path = root / "CharacterActionSource" / "Migrations" / "numeric_action_ids_v1.receipt.json"
    catalog_path = root / "CharacterActionSource" / "action_id_catalog.json"
    if receipt_path.exists():
        prior_receipt = load_json(receipt_path)
        for legacy, action_id in prior_receipt.get("legacyMapping", {}).items():
            mapping.setdefault(str(legacy), int(action_id))
    if catalog_path.exists():
        prior_catalog = load_json(catalog_path)
        allocations = list(prior_catalog.get("allocations", allocations))
    changed: list[tuple[Path, Any]] = []
    for path in paths:
        document = load_json(path)
        converted = (
            repair_animation_action_ids(document, mapping)
            if path.name == "animation_authoring.json"
            else convert_node(document, mapping)
        )
        update_schema(path, converted)
        if canonical_bytes(document) != canonical_bytes(converted):
            changed.append((path, converted))
    print(f"sourceHash={current_hash}")
    print(f"legacyMapCount={len(mapping)} allocationCount={len(allocations)} changedFiles={len(changed)}")
    for path, _ in changed:
        print(path.relative_to(root).as_posix())
    if not args.apply:
        return 0
    if args.expected_source_hash != current_hash:
        raise SystemExit("--expected-source-hash does not match the dry-run source hash")
    if catalog_path.exists():
        catalog = load_json(catalog_path)
    else:
        catalog = build_catalog(root, allocations)
    receipt = {
        "schema": "mx.character-action.numeric-id-migration-receipt.v1",
        "sourceHash": current_hash,
        "catalogHash": catalog["catalogHash"],
        "changedFiles": [path.relative_to(root).as_posix() for path, _ in changed],
        "legacyMapping": dict(sorted(mapping.items())),
    }
    writes = [(path, json_bytes(converted)) for path, converted in changed]
    if not catalog_path.exists():
        writes.append((catalog_path, json_bytes(catalog)))
    writes.append((receipt_path, json_bytes(receipt)))
    apply_transaction(writes)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
