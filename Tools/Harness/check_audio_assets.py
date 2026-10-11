#!/usr/bin/env python3
"""Release-oriented audit for Audio manifests, catalogs, FMOD snapshot, and banks."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import sys


ROOT = pathlib.Path(__file__).resolve().parents[2]
SEMANTIC = pathlib.Path("Assets/Config/MxFramework/Audio/audio_semantic_manifest.json")
GENERATION = pathlib.Path("Assets/Config/MxFramework/Audio/audio_generation_manifest.json")
PHYSICAL = pathlib.Path("Assets/Config/MxFramework/Audio/audio_physical_manifest.json")
GLOBAL_GENERATED = pathlib.Path("Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json")
GLOBAL_PLAYER = pathlib.Path("Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json")
PHYSICAL_GENERATED = pathlib.Path("Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json")
PHYSICAL_PLAYER = pathlib.Path("Assets/StreamingAssets/MxFramework/Audio/audio_physical_runtime_catalog.json")
BANK_MANIFEST_GENERATED = pathlib.Path("Assets/MxFrameworkGenerated/Audio/audio_bank_manifest.json")
BANK_MANIFEST_PLAYER = pathlib.Path("Assets/StreamingAssets/MxFramework/Audio/audio_bank_manifest.json")
SNAPSHOT = pathlib.Path("Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json")
FMOD_PROJECT = pathlib.Path("FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/MxFrameworkAudioDemo.fspro")
FMOD_BANK_DIR = pathlib.Path("FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop")
PLAYER_BANK_DIR = pathlib.Path("Assets/StreamingAssets")


def load_json(relative: pathlib.Path, errors: list[str]) -> dict:
    path = ROOT / relative
    if not path.is_file():
        errors.append(f"missing file: {relative}")
        return {}
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"invalid JSON: {relative}: {exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"JSON root must be an object: {relative}")
        return {}
    return value


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_lfs_pointer(path: pathlib.Path) -> bool:
    if not path.is_file() or path.stat().st_size > 1024:
        return False
    return path.read_bytes().startswith(b"version https://git-lfs.github.com/spec/v1")


def check_mirror(first: pathlib.Path, second: pathlib.Path, label: str, errors: list[str]) -> None:
    first_path = ROOT / first
    second_path = ROOT / second
    if not first_path.is_file() or not second_path.is_file():
        errors.append(f"{label} mirror pair is incomplete: {first} <-> {second}")
        return
    if sha256(first_path) != sha256(second_path):
        errors.append(f"{label} mirror hash mismatch: {first} <-> {second}")


def check_bank_pair(name: str, errors: list[str]) -> None:
    built = ROOT / FMOD_BANK_DIR / name
    player = ROOT / PLAYER_BANK_DIR / name
    if not built.is_file() or not player.is_file():
        errors.append(f"bank mirror pair is incomplete: {name}")
        return
    for path in (built, player):
        if is_lfs_pointer(path):
            errors.append(f"bank is an unresolved Git LFS pointer: {path.relative_to(ROOT)}")
        elif path.stat().st_size < 64:
            errors.append(f"bank artifact is unexpectedly small: {path.relative_to(ROOT)}")
    if sha256(built) != sha256(player):
        errors.append(f"bank mirror hash mismatch: {name}")


def audit_release(errors: list[str], warnings: list[str]) -> dict[str, int]:
    semantic = load_json(SEMANTIC, errors)
    generation = load_json(GENERATION, errors)
    physical = load_json(PHYSICAL, errors)
    snapshot = load_json(SNAPSHOT, errors)
    global_catalog = load_json(GLOBAL_GENERATED, errors)
    physical_catalog = load_json(PHYSICAL_GENERATED, errors)
    bank_manifest = load_json(BANK_MANIFEST_GENERATED, errors)

    check_mirror(GLOBAL_GENERATED, GLOBAL_PLAYER, "global audio catalog", errors)
    check_mirror(PHYSICAL_GENERATED, PHYSICAL_PLAYER, "physical audio catalog", errors)
    check_mirror(BANK_MANIFEST_GENERATED, BANK_MANIFEST_PLAYER, "audio Bank manifest", errors)
    check_bank_pair("Master.bank", errors)
    check_bank_pair("Master.strings.bank", errors)

    if bank_manifest:
        if bank_manifest.get("format") != "mx.audioBankManifest.v1" or bank_manifest.get("schemaVersion") != 1:
            errors.append("audio Bank manifest format/schema is invalid")
        global_path = ROOT / GLOBAL_GENERATED
        if global_path.is_file() and bank_manifest.get("audioCatalogSha256") != sha256(global_path):
            errors.append("audio Bank manifest global Catalog hash is stale")
        project_path = ROOT / FMOD_PROJECT
        if project_path.is_file() and bank_manifest.get("fmodProjectSha256") != sha256(project_path):
            errors.append("audio Bank manifest FMOD project hash is stale")
        for bank in bank_manifest.get("banks") or []:
            file_name = bank.get("fileName") or ""
            player_bank = ROOT / PLAYER_BANK_DIR / file_name
            if not file_name or not player_bank.is_file():
                errors.append(f"audio Bank manifest entry is missing in Player: {file_name or '<empty>'}")
                continue
            if bank.get("sha256") != sha256(player_bank) or bank.get("sizeBytes") != player_bank.stat().st_size:
                errors.append(f"audio Bank manifest entry hash/size is stale: {file_name}")

    if snapshot:
        if not snapshot.get("cacheValid", False):
            errors.append("FMOD snapshot cacheValid is false")
        if snapshot.get("cacheStale", True):
            errors.append("FMOD snapshot cacheStale is true")
        for diagnostic in snapshot.get("diagnostics") or []:
            errors.append(f"FMOD snapshot diagnostic: {diagnostic}")

    events = {item.get("id"): item for item in semantic.get("eventDefinitions") or []}
    snapshot_events = {item.get("path"): item for item in snapshot.get("events") or []}
    active_cues = [item for item in semantic.get("cues") or [] if item.get("lifecycle") == "Active"]
    for cue in active_cues:
        cue_id = cue.get("audioCueId") or "<unknown>"
        evidence = cue.get("auditionEvidence") or {}
        if evidence.get("status") != "Approved":
            errors.append(f"Active Cue is not audition-approved: {cue_id}")
        event = events.get(cue.get("eventDefinitionId"))
        if event is None:
            errors.append(f"Active Cue references a missing Event: {cue_id}")
            continue
        snapshot_event = snapshot_events.get(event.get("fmodEventPath"))
        if snapshot_event is None:
            errors.append(f"Active Cue Event is missing from FMOD snapshot: {cue_id}")
            continue
        length_ms = int(snapshot_event.get("lengthMs") or 0)
        if length_ms <= 0:
            errors.append(f"Active Cue Event has zero timeline length: {cue_id}")
        if not snapshot_event.get("isLoop", False) and length_ms >= 30000:
            warnings.append(f"non-loop Event has a suspicious timeline length ({length_ms} ms): {cue_id}")

    adoption_sets = {item.get("setId"): item for item in generation.get("adoptionSets") or []}
    active_bases = [item for item in physical.get("basisSounds") or [] if item.get("lifecycle") == "Active"]
    cues = {item.get("audioCueId"): item for item in semantic.get("cues") or []}
    for basis in active_bases:
        basis_id = basis.get("id") or "<unknown>"
        adoption = adoption_sets.get(basis.get("adoptionSetId"))
        if adoption is None or adoption.get("status") != "Applied":
            errors.append(f"Active Physical Basis has no Applied adoption set: {basis_id}")
        cue = cues.get(basis.get("audioCueId"))
        evidence = (cue or {}).get("auditionEvidence") or {}
        if (cue is None or evidence.get("status") != "Approved"
                or evidence.get("audioBasisId") != basis.get("id")
                or evidence.get("appliedSetId") != basis.get("adoptionSetId")):
            errors.append(f"Active Physical Basis audition evidence is missing or stale: {basis_id}")

    if global_catalog and physical_catalog:
        expected = physical_catalog.get("audioCatalogHash")
        actual = global_catalog.get("manifestHash")
        if not expected or expected != actual:
            errors.append("physical/global audioCatalogHash link is missing or mismatched")

    return {
        "semanticCues": len(semantic.get("cues") or []),
        "activeCues": len(active_cues),
        "physicalBases": len(physical.get("basisSounds") or []),
        "activeBases": len(active_bases),
        "snapshotEvents": len(snapshot.get("events") or []),
        "manifestBanks": len(bank_manifest.get("banks") or []),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--report-only",
        action="store_true",
        help="print release blockers but return success (for baseline collection only)",
    )
    args = parser.parse_args()
    errors: list[str] = []
    warnings: list[str] = []
    counts = audit_release(errors, warnings)

    print("Audio asset audit summary:")
    for key, value in counts.items():
        print(f"  {key}: {value}")
    for warning in warnings:
        print(f"warning: {warning}", file=sys.stderr)
    for error in errors:
        print(f"error: {error}", file=sys.stderr)
    print(f"Audio asset audit found {len(errors)} release blocker(s) and {len(warnings)} warning(s).")
    return 0 if not errors or args.report_only else 1


if __name__ == "__main__":
    sys.exit(main())
