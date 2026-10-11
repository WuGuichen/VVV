#!/usr/bin/env python3
"""Migrate the CharacterTest ability sample to the numeric runtime-id contract.

The definition-id catalog is read-only input.  This script never allocates or
rewrites catalog entries; it only applies the already-persisted mapping to the
sample and retains the old v1 spelling in the corresponding ``*Label`` field.
"""

from __future__ import annotations

import argparse
import json
import os
import tempfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[3]
CATALOG = ROOT / "Tools" / "RuntimeHash" / "character_definition_ids.json"
SAMPLE = ROOT / "Tools" / "MxFramework.Authoring" / "samples" / "ability-character-test"
GENERATED_SPAWNS = ROOT / "Assets" / "MxFrameworkGenerated" / "Combat" / "Projectiles" / "projectile_spawn_profiles.json"


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def domain_maps() -> tuple[dict[int, dict[str, int]], dict[int, dict[int, str]]]:
    document = load(CATALOG)
    by_domain: dict[int, dict[str, int]] = {}
    reverse: dict[int, dict[int, str]] = {}
    for domain in document.get("domains", []):
        domain_id = int(domain["domainId"])
        values = {str(entry["sourceKey"]): int(entry["id"]) for entry in domain.get("entries", [])}
        by_domain[domain_id] = values
        reverse[domain_id] = {value: key for key, value in values.items()}
    return by_domain, reverse


def bind(obj: dict[str, Any], key: str, label_key: str, mapping: dict[str, int], reverse: dict[int, str], required: bool = False) -> None:
    value = obj.get(key)
    if isinstance(value, str):
        label = value
        obj[label_key] = label
        if label:
            if label not in mapping:
                raise ValueError(f"No numeric mapping for {key}={label!r}")
            obj[key] = mapping[label]
        else:
            obj[key] = 0
    elif isinstance(value, int):
        if value > 0 and not obj.get(label_key):
            obj[label_key] = reverse.get(value, "")
    elif value is None:
        obj[key] = 0
        obj.setdefault(label_key, "")
    else:
        raise ValueError(f"Expected numeric or legacy string for {key}, got {type(value).__name__}")
    if required and int(obj.get(key, 0)) <= 0:
        raise ValueError(f"Required numeric field is empty: {key}")


def migrate_damage(damage: Any, maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]]) -> None:
    if not isinstance(damage, dict):
        return
    bind(damage, "damageTypeId", "damageTypeLabel", maps[409], reverse[409])
    tags = damage.get("tags", [])
    labels = damage.get("tagLabels", [])
    if not isinstance(tags, list):
        raise ValueError("damage.tags must be an array")
    converted: list[int] = []
    converted_labels: list[str] = []
    for index, value in enumerate(tags):
        if isinstance(value, str):
            if value not in maps[410]:
                raise ValueError(f"No numeric mapping for damage.tags[{index}]={value!r}")
            converted.append(maps[410][value])
            converted_labels.append(value)
        elif isinstance(value, int):
            converted.append(value)
            converted_labels.append(reverse[410].get(value, ""))
        else:
            raise ValueError("damage.tags entries must be numeric or legacy strings")
    if isinstance(labels, list):
        for index in range(min(len(labels), len(converted_labels))):
            if isinstance(labels[index], str) and labels[index]:
                converted_labels[index] = labels[index]
    damage["tags"] = converted
    damage["tagLabels"] = converted_labels
    bind(damage, "analysisCategory", "analysisCategoryLabel", maps[411], reverse[411])


def migrate_hit_event(event: Any, maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]]) -> None:
    if not isinstance(event, dict):
        return
    bind(event, "stableEventId", "stableEventLabel", maps[401], reverse[401])
    migrate_damage(event.get("damage"), maps, reverse)


def migrate_impact(impact: Any, maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]]) -> None:
    if not isinstance(impact, dict):
        return
    bind(impact, "presentationProfileId", "presentationProfileLabel", maps[407], reverse[407])
    bind(impact, "stableEventId", "stableEventLabel", maps[401], reverse[401])
    bind(impact, "effectTimelineId", "effectTimelineLabel", maps[401], reverse[401])
    migrate_hit_event(impact.get("hitEvent"), maps, reverse)
    migrate_hit_event(impact.get("areaHitEvent"), maps, reverse)


def migrate_projectile_definition(definition: Any, maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]]) -> None:
    if isinstance(definition, dict):
        migrate_impact(definition.get("impact"), maps, reverse)


def migrate_spawn(profile: dict[str, Any], maps: dict[int, dict[str, int]], reverse: dict[int, dict[str, int]], spawn_ids: dict[str, int]) -> None:
    stable = str(profile.get("stableId", ""))
    if not isinstance(profile.get("profileId"), int) or profile.get("profileId", 0) <= 0:
        if stable in spawn_ids:
            profile["profileId"] = spawn_ids[stable]
        else:
            raise ValueError(f"No persisted projectile spawn profile id for {stable!r}")
    bind(profile.setdefault("origin", {}), "socketId", "socketLabel", maps[400], reverse[400], required=True)
    if isinstance(profile.get("projectileStableId"), str):
        profile.setdefault("projectileDefinitionId", 0)


def migrate_timeline(timeline: dict[str, Any], maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]]) -> None:
    bind(timeline, "timelineId", "timelineLabel", maps[401], reverse[401], required=True)
    for event in timeline.get("events", []):
        if not isinstance(event, dict):
            continue
        bind(event, "eventId", "eventLabel", maps[401], reverse[401], required=True)
        bind(event, "targetTimelineId", "targetTimelineLabel", maps[401], reverse[401])
        bind(event, "requestId", "requestLabel", maps[401], reverse[401])
        bind(event, "presentationProfileId", "presentationProfileLabel", maps[407], reverse[407])


def migrate_ability_document(document: dict[str, Any], maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]], spawn_ids: dict[str, int]) -> None:
    for ability in document.get("abilities", []):
        if not isinstance(ability, dict):
            continue
        input_value = ability.setdefault("input", {})
        bind(input_value, "inputIntentId", "inputIntentLabel", maps[404], reverse[404])
        bind(input_value, "slotId", "slotLabel", maps[405], reverse[405])
        bind(ability.setdefault("cooldown", {}), "groupId", "groupLabel", maps[408], reverse[408])
        bind(ability, "targetPolicyId", "targetPolicyLabel", maps[406], reverse[406])
        action = ability.setdefault("actionBinding", {})
        bind(action, "actionIntentId", "actionIntentLabel", maps[404], reverse[404])
        bind(ability, "presentationProfileId", "presentationProfileLabel", maps[407], reverse[407])
        bind(ability, "effectTimelineId", "effectTimelineLabel", maps[401], reverse[401])
        migrate_damage(ability.get("damage"), maps, reverse)
    for profile in document.get("vfxPresentationProfiles", []):
        if isinstance(profile, dict):
            bind(profile, "presentationProfileId", "presentationProfileLabel", maps[407], reverse[407], required=True)
    for mapping in document.get("vfxTriggerMaps", []):
        if isinstance(mapping, dict):
            bind(mapping, "presentationProfileId", "presentationProfileLabel", maps[407], reverse[407], required=True)
    for definition in document.get("projectileDefinitions", []):
        migrate_projectile_definition(definition, maps, reverse)
    for profile in document.get("projectileSpawnProfiles", []):
        if isinstance(profile, dict):
            migrate_spawn(profile, maps, reverse, spawn_ids)
    for timeline in document.get("effectTimelines", []):
        if isinstance(timeline, dict):
            migrate_timeline(timeline, maps, reverse)
    resources = document.setdefault("resourceRefs", [])
    known = {
        str(item.get("resourceKey", {}).get("id", ""))
        for item in resources
        if isinstance(item, dict) and isinstance(item.get("resourceKey"), dict)
    }
    for resource_id in ("vfx.authored.2c98b752-503c-41af-933e-264fe69cceb8", "vfx.authored.sample.fireball_impact"):
        if resource_id not in known:
            resources.append({
                "resourceKey": {"id": resource_id, "typeId": "GameObject", "variant": "", "packageId": "mxframework.samples"},
                "address": "",
                "catalogId": "character_test.vfx.fireball",
            })


def migrate_projectile_file(document: dict[str, Any], maps: dict[int, dict[str, int]], reverse: dict[int, dict[int, str]], spawn_ids: dict[str, int], definition_ids: dict[str, int]) -> None:
    definition = document.get("definition")
    migrate_projectile_definition(definition, maps, reverse)
    spawn = document.get("spawnProfile")
    if isinstance(spawn, dict):
        migrate_spawn(spawn, maps, reverse, spawn_ids)
        if isinstance(definition, dict) and int(definition.get("definitionId", 0)) > 0:
            spawn["projectileDefinitionId"] = int(definition["definitionId"])
        elif str(spawn.get("projectileStableId", "")) in definition_ids:
            spawn["projectileDefinitionId"] = definition_ids[str(spawn["projectileStableId"])]
    binding = document.get("presentationBinding")
    if isinstance(binding, dict):
        for slot in (binding.get("slots") or {}).values():
            if isinstance(slot, dict) and slot.get("vfxDefinitionId") == 20000:
                slot["vfxDefinitionId"] = 12002
            if isinstance(slot, dict) and slot.get("resource", {}).get("id") == "vfx.authored.sample.fireball_impact":
                slot["vfxDefinitionId"] = 12002


def migrate_presentation_bindings(path: Path) -> tuple[Path, bytes] | None:
    document = load(path)
    changed = False
    for binding in document.get("bindings", []):
        if not isinstance(binding, dict):
            continue
        for slot in (binding.get("slots") or {}).values():
            if isinstance(slot, dict) and slot.get("vfxDefinitionId") == 20000:
                slot["vfxDefinitionId"] = 12002
                changed = True
    if changed:
        return path, (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return None


def write_transaction(writes: list[tuple[Path, bytes]]) -> None:
    token = next(tempfile._get_candidate_names())
    staged: list[tuple[Path, Path]] = []
    originals = {path: path.read_bytes() for path, _ in writes}
    try:
        for path, contents in writes:
            temporary = path.with_name(path.name + ".ability-numeric-staging-" + token)
            temporary.write_bytes(contents)
            staged.append((path, temporary))
        for path, temporary in staged:
            os.replace(temporary, path)
    except Exception:
        for path, contents in originals.items():
            rollback = path.with_name(path.name + ".ability-numeric-rollback-" + token)
            rollback.write_bytes(contents)
            os.replace(rollback, path)
        raise
    finally:
        for _, temporary in staged:
            if temporary.exists():
                temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    maps, reverse = domain_maps()
    generated = load(GENERATED_SPAWNS) if GENERATED_SPAWNS.exists() else {"profiles": []}
    spawn_ids = {str(value.get("stableId", "")): int(value["profileId"]) for value in generated.get("profiles", []) if isinstance(value, dict) and int(value.get("profileId", 0)) > 0}
    targets = [SAMPLE / "config" / "ability_authoring.json"] + sorted((SAMPLE / "config" / "projectiles").glob("*.json"))
    definition_ids: dict[str, int] = {}
    for path in targets[1:]:
        source = load(path)
        definition = source.get("definition") if isinstance(source, dict) else None
        if isinstance(definition, dict) and isinstance(definition.get("stableId"), str) and int(definition.get("definitionId", 0)) > 0:
            definition_ids[definition["stableId"]] = int(definition["definitionId"])
    writes: list[tuple[Path, bytes]] = []
    presentation_write = migrate_presentation_bindings(SAMPLE / "generated" / "projectile_presentation_bindings.json")
    if presentation_write is not None:
        writes.append(presentation_write)
    for path in targets:
        document = load(path)
        if path.name == "ability_authoring.json":
            migrate_ability_document(document, maps, reverse, spawn_ids)
        else:
            migrate_projectile_file(document, maps, reverse, spawn_ids, definition_ids)
        contents = (json.dumps(document, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        if contents != path.read_bytes():
            writes.append((path, contents))
    print(f"targets={len(targets)} changed={len(writes)}")
    if args.apply:
        write_transaction(writes)
        print("applied=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
