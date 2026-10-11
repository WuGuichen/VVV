"""Prepare native VFX and socket references in existing CharacterAction source JSON.

This is an authoring migration. It never allocates or derives an ID: every
reference must already exist in the corresponding persisted content directory.
Imported runtime documents then consume only the numbers. Repeating this script
preserves numbers. Call migrate_document from the owning revision/hash workflow
for managed source documents; the CLI supports plain generated action JSON.
"""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path


def load_references(vfx_path: Path, identities_path: Path):
    vfx = json.loads(vfx_path.read_text(encoding="utf-8-sig"))
    identities = json.loads(identities_path.read_text(encoding="utf-8-sig"))
    definitions = {}
    seen = set()
    for definition in vfx["definitions"]:
        number = definition["definitionId"]
        if type(number) is not int or number <= 0 or number in seen:
            raise ValueError("Invalid or duplicate VFX definition identity")
        seen.add(number)
        resource = definition["prefabResource"]
        key = resource["id"] + ":" + resource["typeId"]
        if resource.get("variant"): key += "#" + resource["variant"]
        if resource.get("packageId"): key += "@" + resource["packageId"]
        for label in (definition["stableId"], key):
            previous = definitions.get(label)
            if previous is not None and previous != number:
                raise ValueError("Ambiguous VFX definition reference: " + label)
            definitions[label] = number
    sockets = {entry["sourceKey"]: entry["id"]
               for domain in identities["domains"] if domain["domainId"] == 400
               for entry in domain["entries"] if entry["status"] == "active"}
    return definitions, sockets


def migrate_document(value, definitions, sockets):
    changed = 0
    def visit(node):
        nonlocal changed
        if isinstance(node, list):
            for child in node: visit(child)
        elif isinstance(node, dict):
            if node.get("kind") == "SpawnVisualCue":
                label = node.get("resourceKey", "")
                if label not in definitions:
                    raise ValueError("Unregistered VFX reference: " + label)
                attachment = node.get("vfxAttachment") or {}
                socket = 0
                if attachment.get("mode") in ("FollowSocket", 1):
                    socket_label = attachment.get("socketId", "")
                    if socket_label not in sockets:
                        raise ValueError("Unregistered socket reference: " + str(socket_label))
                    socket = sockets[socket_label]
                for field, number in (("vfxDefinitionId", definitions[label]), ("numericSocketId", socket)):
                    existing = node.get(field, 0)
                    if type(existing) is not int or existing not in (0, number):
                        raise ValueError("Conflicting persisted numeric reference: " + field)
                    if field not in node or existing != number:
                        node[field] = number
                        changed += 1
            for child in node.values(): visit(child)
    visit(value)
    return changed


def include_frozen_projection(path: Path, definitions, sockets):
    """Reuse explicit IDs from a reviewed earlier projection during integration.

    This only restores already persisted mappings; it never assigns an ID to a
    new resource. Conflicting mappings or unknown socket identities fail closed.
    """
    def visit(node):
        if isinstance(node, list):
            for child in node: visit(child)
        elif isinstance(node, dict):
            if node.get("kind") == "SpawnVisualCue":
                label = node.get("resourceKey")
                number = node.get("vfxDefinitionId")
                if not isinstance(label, str) or not label or type(number) is not int or number <= 0:
                    raise ValueError("Frozen VFX projection requires explicit resource and numeric identities")
                previous = definitions.get(label)
                if previous is not None and previous != number:
                    raise ValueError("Frozen VFX projection conflicts with the current directory")
                attachment = node.get("vfxAttachment") or {}
                if attachment.get("mode") in ("FollowSocket", 1):
                    if sockets.get(attachment.get("socketId")) != node.get("numericSocketId"):
                        raise ValueError("Frozen VFX projection socket identity is missing or conflicting")
                definitions[label] = number
            for child in node.values(): visit(child)
    visit(json.loads(path.read_text(encoding="utf-8-sig")))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+", type=Path)
    parser.add_argument("--vfx", type=Path, default=Path("Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json"))
    parser.add_argument("--identities", type=Path, default=Path("Tools/RuntimeHash/character_definition_ids.json"))
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--frozen-projection", type=Path,
                        help="Reviewed prior generated projection with existing numeric mappings; never a source of new IDs")
    args = parser.parse_args()
    definitions, sockets = load_references(args.vfx, args.identities)
    if args.frozen_projection:
        include_frozen_projection(args.frozen_projection, definitions, sockets)
    outputs = []
    for path in args.paths:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        count = migrate_document(data, definitions, sockets)
        if count:
            if any(field in data for field in ("revision", "contentHash", "manifestHash")):
                raise ValueError("Use this migration through the owning revision/hash workflow: " + str(path))
            outputs.append((path, json.dumps(data, ensure_ascii=False, indent=2) + "\n", count))
    if not args.check:
        for path, text, _ in outputs:
            temporary = path.with_name(path.name + ".vfx-identities.tmp")
            temporary.write_text(text, encoding="utf-8")
            os.replace(temporary, path)
    print(json.dumps({"filesChanged": len(outputs), "fieldsChanged": sum(row[2] for row in outputs), "check": args.check}))
    if args.check and outputs: raise SystemExit(1)


if __name__ == "__main__": main()
