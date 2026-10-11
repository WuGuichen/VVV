#!/usr/bin/env python3
"""Allocate a new six-digit numeric weapon ID and scaffold its ArtSource directory structure."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import shutil
import sys


def find_repo_root(start_dir: pathlib.Path) -> pathlib.Path:
    current = start_dir.resolve()
    while current != current.parent:
        if (current / "Authoring").is_dir() and (current / "ArtSource").is_dir():
            return current
        current = current.parent
    return start_dir.resolve()


def allocate_weapon(
    repo_root: pathlib.Path,
    display_name: str,
    category: str = "OneHandMelee",
    initial_blend: pathlib.Path | None = None,
) -> tuple[str, pathlib.Path]:
    catalog_path = repo_root / "Authoring" / "WeaponPackages" / "weapon_id_catalog.json"
    catalog_path.parent.mkdir(parents=True, exist_ok=True)

    if not catalog_path.is_file():
        catalog = {
            "schemaVersion": "1.0",
            "width": 6,
            "minimumId": 1,
            "maximumId": 999999,
            "nextCandidate": 1,
            "revision": 1,
            "entries": [],
            "retiredIds": [],
        }
    else:
        with open(catalog_path, "r", encoding="utf-8") as f:
            catalog = json.load(f)

    width = catalog.get("width", 6)
    retired = set(catalog.get("retiredIds", []))
    existing_ids = {e["id"] for e in catalog.get("entries", [])}
    candidate = catalog.get("nextCandidate", 1)

    while True:
        candidate_str = f"{candidate:0{width}d}"
        if candidate_str not in retired and candidate_str not in existing_ids:
            allocated_id = candidate_str
            break
        candidate += 1

    catalog["nextCandidate"] = candidate + 1
    catalog["revision"] = catalog.get("revision", 0) + 1

    art_source_rel = f"ArtSource/Common/Weapons/{allocated_id}"
    entry = {
        "id": allocated_id,
        "status": "active",
        "displayName": display_name,
        "category": category,
        "artSourcePath": art_source_rel,
    }
    catalog["entries"].append(entry)

    with open(catalog_path, "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=2, ensure_ascii=False)
        f.write("\n")

    # Scaffold ArtSource directory
    weapon_root = repo_root / art_source_rel
    blender_work = weapon_root / "Blender_Work"
    final_source = weapon_root / "Final_Source"
    model_dir = weapon_root / "model"
    textures_dir = weapon_root / "textures"

    blender_work.mkdir(parents=True, exist_ok=True)
    final_source.mkdir(parents=True, exist_ok=True)
    model_dir.mkdir(parents=True, exist_ok=True)
    textures_dir.mkdir(parents=True, exist_ok=True)

    # Initial README
    readme_path = weapon_root / "README.md"
    readme_content = f"""# Weapon {allocated_id} · {display_name}

## Metadata
- Weapon ID: `{allocated_id}`
- Display Name: `{display_name}`
- Category: `{category}`
- ArtSource: `{art_source_rel}`
- Authority Blend: `Final_Source/{allocated_id}_authoring.blend`
- Exported FBX: `model/{allocated_id}.fbx`
"""
    readme_path.write_text(readme_content, encoding="utf-8")

    # Initial weapon_sources.json
    sources_path = weapon_root / "weapon_sources.json"
    sources_content = {
        "schemaVersion": "1.0",
        "resourceId": "",
        "characterId": "",
        "weaponId": allocated_id,
        "displayName": display_name,
        "sourceRoot": "ArtSource/Common",
        "items": [
            {
                "id": "weapon-model",
                "label": f"{display_name} model",
                "presetId": "weaponModel",
                "bindTarget": "",
                "required": True,
                "source": {
                    "kind": "file",
                    "scope": "public",
                    "path": f"Weapons/{allocated_id}/model/{allocated_id}.fbx",
                    "selection": None,
                },
                "target": {
                    "resourceKey": f"weapon.{allocated_id}.model",
                    "resourceKind": "Model",
                    "resourceUsage": "weaponModel",
                    "bindTarget": "",
                    "slot": "",
                    "importHints": {
                        "targetPathPolicy": "",
                        "targetRelativePath": "",
                        "scale": 1,
                        "modelWrapperPose": {
                            "parentKind": "ModelRoot",
                            "parentPath": "",
                            "position": {"x": 0, "y": 0, "z": 0},
                            "rotation": {"x": 0, "y": 0, "z": 0, "w": 1},
                            "scale": {"x": 1, "y": 1, "z": 1},
                            "eulerHint": {"x": 0, "y": 0, "z": 0},
                        },
                        "materialPolicy": "wgame.prop_stylized_lit",
                        "animationPolicy": "",
                        "providerId": "",
                        "upAxis": "Y+",
                        "forwardAxis": "Z+",
                        "collisionPolicy": "authoringTraceOnly",
                        "physicsDataPolicy": "",
                        "labels": [],
                        "metadata": {},
                    },
                },
            }
        ],
    }
    sources_path.write_text(json.dumps(sources_content, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    # If initial blend provided, copy to Blender_Work
    if initial_blend and initial_blend.is_file():
        dest_blend = blender_work / f"{allocated_id}_work.blend"
        shutil.copy2(initial_blend, dest_blend)
        print(f"Copied source blend from {initial_blend} to {dest_blend}")

    return allocated_id, weapon_root


def main() -> None:
    parser = argparse.ArgumentParser(description="Allocate a numeric weapon ID and scaffold its project directory.")
    parser.add_argument("--name", required=True, help="Display name of the weapon (e.g. 'Thunder Sword')")
    parser.add_argument("--category", default="OneHandMelee", help="Weapon category (default: OneHandMelee)")
    parser.add_argument("--blend", type=pathlib.Path, default=None, help="Optional initial .blend file to import")
    parser.add_argument("--root", type=pathlib.Path, default=None, help="Repository root path")

    args = parser.parse_args()
    repo_root = find_repo_root(args.root or pathlib.Path.cwd())

    allocated_id, path = allocate_weapon(
        repo_root=repo_root,
        display_name=args.name,
        category=args.category,
        initial_blend=args.blend,
    )

    print(f"Successfully allocated weapon ID: {allocated_id}")
    print(f"Scaffolded ArtSource directory: {path}")


if __name__ == "__main__":
    main()
