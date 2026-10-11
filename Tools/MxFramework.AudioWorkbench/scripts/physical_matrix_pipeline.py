#!/usr/bin/env python3
"""Prepare and generate the project-wide physical audio material matrix.

The script talks to the local Authoring server instead of editing FMOD or
semantic manifests directly. Candidate generation is resumable and never
performs adoption, approval, or runtime publication.
"""

from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Any, Iterable


DEFAULT_SERVER = "http://127.0.0.1:4873"
DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-codex-flow-test"
PLAN_REVISION = "physical-matrix-natural-decay-v3"
NEGATIVE_PROMPT = (
    "music, melody, rhythm, speech, voice, ambience, environmental bed, background noise, "
    "broadband noise, static hiss, clipping, distortion, rhythmic repetition, synthetic test tone, "
    "baked large-room reverb, long cinematic reverb, room impulse response, hall reverb, chamber reverb, "
    "echo, slapback, diffuse tail, wind bed, sustained resonance bed, second impact, repeated hit, sound after the event, multiple unrelated impacts"
)


@dataclass(frozen=True)
class MaterialSpec:
    material_id: str
    token: str
    display_name: str
    prompt: str
    ui_token: str
    group: str


MATERIALS = (
    MaterialSpec("audio.material.generic_solid", "generic_solid", "通用固体", "neutral dense solid material", "generic_solid", "hard"),
    MaterialSpec("audio.material.wood", "wood", "木材", "dry solid wood with restrained fibrous resonance", "wood", "hard"),
    MaterialSpec("audio.material.metal", "metal", "金属", "solid metal with a controlled non-musical ring", "metal", "resonant"),
    MaterialSpec("audio.material.stone", "stone", "石材 / 混凝土", "dense stone or concrete with a dry mineral knock", "stone_concrete", "hard"),
    MaterialSpec("audio.material.glass", "glass", "玻璃 / 陶瓷", "hard brittle glass or ceramic without automatic shattering", "glass_ceramic", "brittle"),
    MaterialSpec("audio.material.hard_plastic", "hard_plastic", "硬塑料", "rigid hard plastic with a compact polymer clack", "hard_plastic", "hard"),
    MaterialSpec("audio.material.rubber", "rubber", "橡胶", "dense rubber with strong damping and restrained rebound", "rubber", "soft"),
    MaterialSpec("audio.material.fabric_leather", "fabric_leather", "布料 / 皮革", "layered fabric or leather with a muted dry slap", "cloth_leather", "soft"),
    MaterialSpec("audio.material.flesh", "flesh", "肉体", "non-gory human flesh and tissue with a compact body thump", "flesh", "soft"),
    MaterialSpec("audio.material.soil_gravel", "soil_gravel", "泥土 / 沙砾", "dry soil and small gravel with localized granular scatter", "soil_sand", "granular"),
    MaterialSpec("audio.material.ice_crystal", "ice_crystal", "冰晶", "hard ice crystal with a short brittle crystalline resonance", "ice", "brittle"),
    MaterialSpec("audio.material.magic_energy", "magic_energy", "魔法能量", "restrained non-musical magical energy with a compact spectral pulse", "magic_energy", "magic"),
)
AIR_ID = "audio.material.air"


CALIBRATION = (
    ("Impact", "metal", "metal"),
    ("Impact", "wood", "stone"),
    ("Impact", "glass", "stone"),
    ("Impact", "rubber", "stone"),
    ("Impact", "metal", "flesh"),
    ("Impact", "stone", "soil_gravel"),
    ("Impact", "generic_solid", "magic_energy"),
    ("Sweep", "metal", "air"),
)


FAMILY_BY_VARIANT = {
    "Impact": "audio.family.object_impact",
    "Step": "audio.family.footstep",
    "Landing": "audio.family.landing",
    "Hit": "audio.family.weapon_hit",
    "Block": "audio.family.weapon_block",
    "Sweep": "audio.family.air_displacement",
}


def material_by_token(token: str) -> MaterialSpec:
    for material in MATERIALS:
        if material.token == token:
            return material
    raise KeyError(token)


def compact_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def fnv1a_recipe_hash(recipe: dict[str, Any]) -> str:
    identity = {
        "bindingHash": recipe.get("bindingHash", ""),
        "revision": recipe.get("revision", 1),
        "layerTemplate": recipe.get("layerTemplate", "BodyOnly"),
        "collisionProfile": recipe.get("collisionProfile"),
        "targetDurationSeconds": recipe.get("targetDurationSeconds", 0.7),
        "durationMode": recipe.get("durationMode"),
        "durationCharacter": recipe.get("durationCharacter"),
        "decayProfile": recipe.get("decayProfile"),
        "maxDurationSeconds": recipe.get("maxDurationSeconds"),
        "positivePrompt": recipe.get("positivePrompt", ""),
        "negativePrompt": recipe.get("negativePrompt", ""),
        "layers": recipe.get("layers", []),
    }
    value = 2166136261
    encoded = compact_json(identity).encode("utf-16-le")
    for index in range(0, len(encoded), 2):
        code_unit = encoded[index] | (encoded[index + 1] << 8)
        value ^= code_unit
        value = (value * 16777619) & 0xFFFFFFFF
    return f"fnv1a-{value:08x}"


def stable_seed(basis_id: str, variant: str, recipe_revision: int = 1) -> int:
    source = f"{PLAN_REVISION}|{basis_id}|{variant}|{recipe_revision}".encode("utf-8")
    return int.from_bytes(hashlib.sha256(source).digest()[:4], "big") & 0x7FFFFFFF


def canonical_pair(material_a: dict[str, Any], material_b: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    if int(material_a["runtimeMaterialId"]) <= int(material_b["runtimeMaterialId"]):
        return material_a, material_b
    return material_b, material_a


def pair_id(material_a: str, material_b: str, material_map: dict[str, dict[str, Any]]) -> str:
    first, second = canonical_pair(material_map[material_a], material_map[material_b])
    first_token = first["id"].removeprefix("audio.material.")
    second_token = second["id"].removeprefix("audio.material.")
    return f"audio.pair.{first_token}--{second_token}"


def desired_pair_specs() -> list[tuple[str, str, str]]:
    values: list[tuple[str, str, str]] = []
    for first, second in itertools.combinations_with_replacement(MATERIALS, 2):
        values.append((first.material_id, second.material_id, "Impact"))
    for material in MATERIALS:
        values.append((material.material_id, AIR_ID, "Sweep"))
    return values


def impact_basis_id(first: str, second: str, material_map: dict[str, dict[str, Any]]) -> str:
    pair = pair_id(first, second, material_map).removeprefix("audio.pair.").replace("--", ".")
    return f"audio.basis.impact.{pair}"


def sweep_basis_id(material: str, material_map: dict[str, dict[str, Any]]) -> str:
    pair = pair_id(material, AIR_ID, material_map).removeprefix("audio.pair.").replace("--", ".")
    return f"audio.basis.sweep.{pair}"


def request_json(base_url: str, path: str, method: str = "GET", payload: Any | None = None) -> Any:
    data = None if payload is None else compact_json(payload).encode("utf-8")
    request = urllib.request.Request(
        base_url.rstrip("/") + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=45) as response:
            body = response.read().decode("utf-8")
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        try:
            detail = json.loads(body)
            message = detail.get("message") or detail.get("error") or body
        except json.JSONDecodeError:
            message = body
        raise RuntimeError(f"HTTP {error.code} {path}: {message}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"Authoring server unavailable at {base_url}: {error.reason}") from error
    return json.loads(body) if body else {}


def state_path(package: str) -> str:
    return "/api/authoring/audio/state?package=" + urllib.parse.quote(package, safe="")


def load_state(base_url: str, package: str) -> dict[str, Any]:
    return request_json(base_url, state_path(package))


def save_state(base_url: str, package: str, state: dict[str, Any]) -> dict[str, Any]:
    payload = {
        "package": package,
        "expectedHash": state["contentHash"],
        "manifest": state["manifest"],
        "expectedGenerationHash": state["audioGeneration"]["manifestHash"],
        "generationManifest": state["audioGeneration"]["manifest"],
        "expectedPhysicalHash": state["audioPhysical"]["manifestHash"],
        "physicalManifest": state["audioPhysical"]["manifest"],
    }
    result = request_json(base_url, "/api/authoring/audio/save", "POST", payload)
    if not result.get("success"):
        raise RuntimeError(result.get("message") or "Audio draft save failed")
    return load_state(base_url, package)


def ensure_pairs(state: dict[str, Any]) -> int:
    manifest = state["audioPhysical"]["manifest"]
    materials = {item["id"]: item for item in manifest["materialProfiles"]}
    pairs = manifest.setdefault("materialPairs", [])
    by_id = {item["id"]: item for item in pairs}
    allocation = manifest.setdefault("idAllocation", {})
    next_id = max(550001, int(allocation.get("nextMaterialPairId", 550001)))
    created = 0
    for first_id, second_id, variant in desired_pair_specs():
        wanted_id = pair_id(first_id, second_id, materials)
        if wanted_id in by_id:
            continue
        first, second = canonical_pair(materials[first_id], materials[second_id])
        pair = {
            "id": wanted_id,
            "runtimeMaterialPairId": next_id,
            "materialAId": first["id"],
            "materialBId": second["id"],
            "lifecycle": "Active",
            "metadata": {"productionPlan": PLAN_REVISION, "baseVariant": variant, "contentStatus": "Planned"},
        }
        pairs.append(pair)
        by_id[wanted_id] = pair
        next_id += 1
        created += 1
    allocation["nextMaterialPairId"] = next_id
    return created


def basis_for_target(state: dict[str, Any], variant: str, first_token: str, second_token: str) -> dict[str, Any] | None:
    physical = state["audioPhysical"]["manifest"]
    material_map = {item["id"]: item for item in physical["materialProfiles"]}
    first_id = AIR_ID if first_token == "air" else material_by_token(first_token).material_id
    second_id = AIR_ID if second_token == "air" else material_by_token(second_token).material_id
    wanted_pair = pair_id(first_id, second_id, material_map)
    return next((basis for basis in physical.get("basisSounds", []) if basis.get("materialPairId") == wanted_pair and basis.get("variantKind") == variant), None)


def target_basis_id(state: dict[str, Any], variant: str, first_token: str, second_token: str) -> str:
    physical = state["audioPhysical"]["manifest"]
    material_map = {item["id"]: item for item in physical["materialProfiles"]}
    first_id = AIR_ID if first_token == "air" else material_by_token(first_token).material_id
    second_id = AIR_ID if second_token == "air" else material_by_token(second_token).material_id
    return impact_basis_id(first_id, second_id, material_map) if variant == "Impact" else sweep_basis_id(first_id if second_id == AIR_ID else second_id, material_map)


def template_event_id(state: dict[str, Any], family_id: str) -> str:
    physical = state["audioPhysical"]["manifest"]
    family = next(item for item in physical["families"] if item["id"] == family_id)
    fallback = next(item for item in physical["basisSounds"] if item["id"] == family["fallbackBasisId"])
    cue = next(item for item in state["manifest"]["cues"] if item["audioCueId"] == fallback["audioCueId"])
    return cue["eventDefinitionId"]


def wait_managed_operation(base_url: str, operation_id: str, timeout: float = 240.0) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    last_stage = ""
    while time.monotonic() < deadline:
        status = request_json(base_url, f"/api/authoring/audio/physical/managed-event/{operation_id}")
        stage = status.get("stage", "")
        if stage != last_stage:
            print(f"  [{status.get('progress', 0):3}%] {stage}: {status.get('message', '')}", flush=True)
            last_stage = stage
        if status.get("terminal"):
            if stage != "Ready":
                raise RuntimeError(f"Managed Event failed ({status.get('code')}): {status.get('message')} {status.get('detail', '')}")
            return status
        time.sleep(1.0)
    raise TimeoutError(f"Managed Event operation timed out: {operation_id}")


def ensure_managed_basis(base_url: str, package: str, state: dict[str, Any], variant: str, first: str, second: str) -> dict[str, Any]:
    existing = basis_for_target(state, variant, first, second)
    cue_ids = {item["audioCueId"] for item in state["manifest"]["cues"]}
    if existing and existing.get("audioCueId") in cue_ids:
        return state
    physical = state["audioPhysical"]["manifest"]
    material_map = {item["id"]: item for item in physical["materialProfiles"]}
    first_id = AIR_ID if first == "air" else material_by_token(first).material_id
    second_id = AIR_ID if second == "air" else material_by_token(second).material_id
    wanted_pair = pair_id(first_id, second_id, material_map)
    if not any(item["id"] == wanted_pair for item in physical["materialPairs"]):
        raise RuntimeError(f"Pair is not prepared: {wanted_pair}")
    basis_id = existing["id"] if existing else target_basis_id(state, variant, first, second)
    first_name = "空气" if first == "air" else material_by_token(first).display_name
    second_name = "空气" if second == "air" else material_by_token(second).display_name
    family_id = FAMILY_BY_VARIANT[variant]
    print(f"物化 {variant} · {first_name} × {second_name} · {basis_id}", flush=True)
    status = request_json(base_url, "/api/authoring/audio/physical/managed-event", "POST", {
        "package": package, "expectedSemanticHash": state["contentHash"],
        "expectedPhysicalHash": state["audioPhysical"]["manifestHash"], "familyId": family_id,
        "materialPairId": wanted_pair, "variantKind": variant, "basisId": basis_id,
        "createBasis": existing is None, "templateEventDefinitionId": template_event_id(state, family_id),
        "displayName": f"{first_name} × {second_name} · {variant}",
    })
    if status.get("terminal") and status.get("stage") != "Ready":
        raise RuntimeError(f"Managed Event rejected ({status.get('code')}): {status.get('message')}")
    if not status.get("terminal"):
        wait_managed_operation(base_url, status["operationId"])
    return load_state(base_url, package)


def acoustic_behavior(first: MaterialSpec, second: MaterialSpec) -> str:
    groups = {first.group, second.group}
    if "magic" in groups:
        return "a short restrained energy response integrated with the physical material, non-musical and without a tonal drone"
    if "granular" in groups:
        return "a localized granular crunch and a few short particles, not a continuous noise wash"
    if "brittle" in groups:
        return "a precise brittle contact with a short decay, without full destruction or a shower of debris"
    if groups <= {"soft"}:
        return "a muted damped deformation thump with little high-frequency ringing"
    if "soft" in groups:
        return "a compact asymmetric impact where the soft side damps the harder material naturally"
    if "resonant" in groups:
        return "a controlled hard impact with a brief material ring that remains non-musical"
    return "a compact dry impact with a clear material-specific onset and controlled short resonance"


def target_duration(variant: str, first: MaterialSpec | None, second: MaterialSpec | None) -> float:
    """Returns the natural-decay maximum window, never a length to fill."""
    if variant == "Step": return 0.40
    if variant == "Landing": return 0.55
    if variant == "Hit": return 0.45
    if variant == "Block": return 0.65
    if variant == "Sweep": return 0.60 if first and first.group == "magic" else 0.45
    groups = {first.group, second.group} if first and second else set()
    if "magic" in groups: return 0.50
    if "resonant" in groups: return 0.45
    if "granular" in groups: return 0.42
    if "brittle" in groups: return 0.38
    if "soft" in groups: return 0.22
    return 0.30


def decay_profile(variant: str, first: MaterialSpec | None, second: MaterialSpec | None) -> str:
    if variant == "Sweep": return "Sweep"
    groups = {item.group for item in (first, second) if item is not None}
    if "magic" in groups: return "Magic"
    if "resonant" in groups: return "Resonant"
    if "granular" in groups: return "Granular"
    if "brittle" in groups: return "Brittle"
    if "soft" in groups: return "Short"
    return "Natural"


def recipe_layers(variant: str, base_prompt: str, default_max: float) -> tuple[str, list[dict[str, Any]]]:
    if variant in {"Impact", "Step", "Sweep"}:
        names, template = ("body",), "BodyOnly"
    elif variant in {"Landing", "Hit"}:
        names, template = ("transient", "body"), "TransientBody"
    else:
        names, template = ("transient", "body", "tail"), "TransientBodyTail"
    focus = {
        "transient": "Focus only on a fast precise onset; avoid a second impact and avoid a long tail.",
        "body": "Render the complete material body and weight with compact controlled decay.",
        "tail": "Render only the short natural material resonance; no room reverb, drone, melody, or repeated hit.",
    }
    layer_max = {
        "transient": 0.18 if variant in {"Landing", "Block"} else 0.15,
        "body": 0.55 if variant == "Landing" else 0.50 if variant == "Block" else 0.45 if variant == "Hit" else default_max,
        "tail": 0.65,
    }
    return template, [{"name": name, "enabled": True, "gainDb": 0, "delayMs": 0, "takes": 3, "adoptionTakes": 1, "maxDurationSeconds": layer_max[name], "prompt": f"{base_prompt} {focus[name]}"} for name in names]


def build_recipe(state: dict[str, Any], basis: dict[str, Any]) -> dict[str, Any]:
    physical = state["audioPhysical"]["manifest"]
    pair = next(item for item in physical["materialPairs"] if item["id"] == basis["materialPairId"])
    cue = next(item for item in state["manifest"]["cues"] if item["audioCueId"] == basis["audioCueId"])
    event = next(item for item in state["manifest"]["eventDefinitions"] if item["id"] == cue["eventDefinitionId"])
    cue_state = next(item for item in state["cueStates"] if item["audioCueId"] == cue["audioCueId"])
    variant = basis["variantKind"]
    first = next((item for item in MATERIALS if item.material_id == pair["materialAId"]), None)
    second = next((item for item in MATERIALS if item.material_id == pair["materialBId"]), None)
    if variant == "Sweep":
        moving = first or second
        base_prompt = f"One isolated close-mic air displacement whoosh made by {moving.prompt}, medium fast swing, physically plausible aerodynamic movement, compact game-ready motion cue with a clean pass-by shape, dry studio, no impact, no projectile launch, no wind ambience, realistic and clearly readable, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance"
        intent, collision_profile, category = f"{moving.display_name}快速运动形成的克制破空声。", None, "AirDisplacement"
    else:
        assert first is not None and second is not None
        mode = {"Step": "step", "Landing": "land", "Hit": "weapon_hit", "Block": "block"}.get(variant, "impact")
        action = {"Step": "one isolated footstep contact", "Landing": "one isolated character landing", "Hit": "one isolated weapon hit", "Block": "one isolated weapon block"}.get(variant, "one isolated medium-force collision")
        base_prompt = f"{action} between {first.prompt} and {second.prompt}, {acoustic_behavior(first, second)}, physically plausible contact, one complete compact game sound, realistic material identity, dry studio, close-mic, isolated, game-ready, no unrelated source, anechoic booth, direct sound only, zero room reflections, terminate after natural material resonance"
        intent = f"{first.display_name}与{second.display_name}发生一次写实、清晰、近录的{variant}音效。"
        collision_profile = {
            "kind": {"Step": "Footstep", "Landing": "Landing", "Hit": "WeaponHit", "Block": "WeaponHit"}.get(variant, "ObjectCollision"),
            "impactor": {"materialId": first.ui_token, "customMaterial": "", "massClass": "medium", "contactArea": "medium", "surface": "normal", "construction": "solid"},
            "receiver": {"materialId": second.ui_token, "customMaterial": "", "massClass": "heavy", "contactArea": "medium", "surface": "normal", "construction": "solid"},
            "interaction": {"intensity": "medium", "speed": "medium", "angle": "direct", "mode": mode},
        }
        category = {"Step": "Footstep", "Landing": "Landing", "Hit": "WeaponHit", "Block": "WeaponHit"}.get(variant, "ObjectCollision")
    maximum = target_duration(variant, first, second)
    base_prompt = (
        f"{base_prompt}, the single event begins immediately and is completely finished within "
        f"{round(maximum * 1000):d} milliseconds, followed by absolute silence, no sound before or after the event"
    )
    template, layers = recipe_layers(variant, base_prompt, maximum)
    return {
        "format": "mx.audioGenerationRecipe.v2", "audioCueId": cue["audioCueId"],
        "eventDefinitionId": event["id"], "bindingHash": cue_state["bindingHash"], "intentZh": intent,
        "category": category, "material": "" if variant == "Sweep" else f"{first.display_name} x {second.display_name}",
        "action": cue.get("displayName") or basis.get("displayName") or variant, "weight": "medium", "intensity": "medium",
        "perspective": "close", "environment": "anechoic booth", "targetDurationSeconds": maximum,
        "durationMode": "NaturalDecay", "durationCharacter": "Auto",
        "decayProfile": decay_profile(variant, first, second), "maxDurationSeconds": maximum,
        "variationCount": len(layers) * 3, "adoptionCountPerLayer": 1, "generationProfile": "Exploration",
        "layerTemplate": template, "soundDirection": "realistic, physically readable, dry and restrained",
        "collisionProfile": collision_profile, "positivePrompt": base_prompt, "negativePrompt": NEGATIVE_PROMPT,
        "layers": layers, "derivationProvider": "PhysicalMatrixRuleDerived", "derivationModel": PLAN_REVISION,
        "promptModified": False, "revision": 1, "updatedAtUtc": "", "history": [],
    }


def persist_recipes(base_url: str, package: str, state: dict[str, Any], bases: Iterable[dict[str, Any]]) -> tuple[dict[str, Any], dict[str, dict[str, Any]]]:
    generation = state["audioGeneration"]["manifest"]
    recipes = generation.setdefault("recipes", [])
    by_cue = {item["audioCueId"]: item for item in recipes}
    selected: dict[str, dict[str, Any]] = {}
    changed = False
    for basis in bases:
        recipe = build_recipe(state, basis)
        existing = by_cue.get(recipe["audioCueId"])
        if existing and existing.get("derivationProvider") not in {"PhysicalMatrixRuleDerived", "RuleDerived"}:
            print(f"保留人工 Recipe：{recipe['audioCueId']} ({existing.get('derivationProvider')})", flush=True)
            selected[recipe["audioCueId"]] = existing
            continue
        if existing:
            index = recipes.index(existing)
            existing_core = {key: value for key, value in existing.items() if key not in {"updatedAtUtc", "history"}}
            recipe_core = {key: value for key, value in recipe.items() if key not in {"updatedAtUtc", "history"}}
            if compact_json(existing_core) == compact_json(recipe_core):
                selected[recipe["audioCueId"]] = existing
                continue
            recipe["revision"] = max(1, int(existing.get("revision", 1))) + 1
            recipe["history"] = existing.get("history", [])
            recipes[index] = recipe
            changed = True
            selected[recipe["audioCueId"]] = recipe
        else:
            recipes.append(recipe)
            by_cue[recipe["audioCueId"]] = recipe
            selected[recipe["audioCueId"]] = recipe
            changed = True
    if not changed:
        return state, selected
    saved = save_state(base_url, package, state)
    persisted = {item["audioCueId"]: item for item in saved["audioGeneration"]["manifest"].get("recipes", [])}
    return saved, {cue_id: persisted.get(cue_id, recipe) for cue_id, recipe in selected.items()}


def wait_job(base_url: str, job_id: str, timeout: float = 1200.0) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    last_bucket = -1
    while time.monotonic() < deadline:
        job = request_json(base_url, f"/api/authoring/audio/generation/jobs/{job_id}")
        bucket = int(float(job.get("progress", 0)) * 10)
        if bucket != last_bucket:
            print(f"  [{float(job.get('progress', 0))*100:5.1f}%] {job.get('status')} · {len(job.get('candidates', []))} candidates", flush=True)
            last_bucket = bucket
        if job.get("status") in {"Succeeded", "Failed", "Cancelled"}:
            return job
        time.sleep(1.5)
    raise TimeoutError(f"Generation job timed out: {job_id}")


def matching_succeeded_job(base_url: str, cue_id: str, recipe_hash: str, wanted: int) -> dict[str, Any] | None:
    jobs = request_json(base_url, "/api/authoring/audio/generation/jobs?audioCueId=" + urllib.parse.quote(cue_id, safe=""))
    return next((job for job in jobs if job.get("status") == "Succeeded" and job.get("recipeHash") == recipe_hash and len(job.get("candidates", [])) >= wanted), None)


def generate_recipe(base_url: str, recipe: dict[str, Any], is_3d: bool = True, accept_failed: bool = False) -> dict[str, Any]:
    recipe_hash = fnv1a_recipe_hash(recipe)
    wanted = sum(int(item["takes"]) for item in recipe["layers"] if item.get("enabled"))
    existing = matching_succeeded_job(base_url, recipe["audioCueId"], recipe_hash, wanted)
    if not existing and accept_failed:
        jobs = request_json(base_url, "/api/authoring/audio/generation/jobs?audioCueId=" + urllib.parse.quote(recipe["audioCueId"], safe=""))
        existing = next((item for item in jobs if item.get("status") == "Failed" and item.get("recipeHash") == recipe_hash and len(item.get("candidates", [])) >= wanted), None)
    if existing:
        print(f"复用已完成 Job {existing['jobId']} · {recipe['audioCueId']}", flush=True)
        return existing
    seed = stable_seed(recipe["audioCueId"], recipe["category"], int(recipe.get("revision", 1)))
    print(f"生成 {wanted} 个候选 · {recipe['audioCueId']} · seed {seed}", flush=True)
    job = request_json(base_url, "/api/authoring/audio/generation/jobs", "POST", {"brief": recipe, "seed": seed, "is3D": is_3d, "recipeRevision": int(recipe.get("revision", 1)), "recipeHash": recipe_hash})
    result = wait_job(base_url, job["jobId"])
    ready = sum(1 for item in result.get("candidates", []) if item.get("status") == "Ready")
    print(f"完成 {result['jobId']} · Ready {ready}/{len(result.get('candidates', []))}", flush=True)
    if result.get("status") != "Succeeded":
        raise RuntimeError(f"Generation failed for {recipe['audioCueId']}: {result.get('failureReason')}")
    return result


def calibration_bases(state: dict[str, Any]) -> list[dict[str, Any]]:
    result = []
    for variant, first, second in CALIBRATION:
        basis = basis_for_target(state, variant, first, second)
        if not basis: raise RuntimeError(f"Calibration Basis missing: {variant} {first} {second}")
        result.append(basis)
    return result


def material_targets(scope: str) -> list[tuple[str, str, str]]:
    impact = [("Impact", first.token, second.token) for first, second in itertools.combinations_with_replacement(MATERIALS, 2)]
    sweep = [("Sweep", material.token, "air") for material in MATERIALS]
    if scope == "impact": return impact
    if scope == "sweep": return sweep
    if scope == "all": return impact + sweep
    return []


def specialized_bases(state: dict[str, Any]) -> list[dict[str, Any]]:
    wanted = {"Step", "Landing", "Hit", "Block"}
    return [basis for basis in state["audioPhysical"]["manifest"].get("basisSounds", []) if basis.get("variantKind") in wanted]


def generate_scope(base_url: str, package: str, state: dict[str, Any], scope: str) -> list[str]:
    bases: list[dict[str, Any]] = []
    if scope in {"impact", "sweep", "all"}:
        for variant, first, second in material_targets(scope):
            state = ensure_managed_basis(base_url, package, state, variant, first, second)
            basis = basis_for_target(state, variant, first, second)
            if not basis: raise RuntimeError(f"Basis missing after materialization: {variant} {first} {second}")
            bases.append(basis)
    if scope in {"specialized", "all"}:
        bases.extend(specialized_bases(state))
    unique = {basis["id"]: basis for basis in bases}
    state, recipes = persist_recipes(base_url, package, state, unique.values())
    failed: list[str] = []
    for basis in unique.values():
        try:
            result = generate_recipe(base_url, recipes[basis["audioCueId"]], accept_failed=True)
            if result.get("status") != "Succeeded": failed.append(basis["audioCueId"])
        except RuntimeError as error:
            failed.append(basis["audioCueId"])
            print(f"质量门禁失败，继续下一组：{error}", flush=True)
    return failed


def print_plan() -> None:
    print("公共材质矩阵制作计划")
    print("  78 Impact Pair × 1 layer × 3 candidates = 234")
    print("  12 Sweep Pair × 1 layer × 3 candidates = 36")
    print("  4 Step × 1 layer × 3 candidates = 12")
    print("  4 Landing × 2 layers × 3 candidates = 24")
    print("  4 Hit × 2 layers × 3 candidates = 24")
    print("  1 Block × 3 layers × 3 candidates = 9")
    print("  Total: 103 Basis, 339 candidates, 113 adopted layer sources")
    print("  Auto cells: fallback-only; Air × Air: silent")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("plan", "prepare", "calibrate", "generate", "status"))
    parser.add_argument("--server", default=DEFAULT_SERVER)
    parser.add_argument("--package", default=DEFAULT_PACKAGE)
    parser.add_argument("--scope", choices=("impact", "sweep", "specialized", "all"), default="all")
    parser.add_argument("--calibration-approved", action="store_true", help="Required before generating beyond the calibration batch.")
    args = parser.parse_args()
    if args.command == "plan": print_plan(); return 0
    state = load_state(args.server, args.package)
    health = request_json(args.server, "/api/authoring/audio/generation/health")
    if args.command in {"prepare", "calibrate", "generate"}:
        if health.get("status") != "ok" or health.get("device") != "cuda" or health.get("fake"):
            raise RuntimeError(f"Generation preflight failed: {compact_json(health)}")
        if not health.get("revision"): raise RuntimeError("Generation preflight failed: model revision is not pinned")
        if health.get("activeJobId"): raise RuntimeError(f"Generation preflight failed: conflicting job {health['activeJobId']} is active")
    if args.command == "generate" and not args.calibration_approved:
        raise RuntimeError("Full generation is gated: listen to the calibration batch, then pass --calibration-approved")
    if args.command == "status":
        physical = state["audioPhysical"]["manifest"]
        print(f"Pair {len(physical.get('materialPairs', []))}/90 · Basis {len(physical.get('basisSounds', []))}/103")
        print(f"Host {health.get('status')} · {health.get('device')} · {health.get('revision')}")
        for variant, first, second in CALIBRATION:
            basis = basis_for_target(state, variant, first, second)
            cue_id = basis.get("audioCueId") if basis else ""
            jobs = request_json(args.server, "/api/authoring/audio/generation/jobs?audioCueId=" + urllib.parse.quote(cue_id, safe="")) if cue_id else []
            latest = jobs[0] if jobs else None
            print(f"  {variant:6} {first:16} × {second:16} · {cue_id or 'not materialized'} · {latest.get('status') if latest else 'no job'}")
        return 0
    created = ensure_pairs(state)
    if created: state = save_state(args.server, args.package, state)
    print(f"Pair preparation complete · created {created} · total {len(state['audioPhysical']['manifest']['materialPairs'])}", flush=True)
    if args.command == "prepare": return 0
    if args.command == "generate":
        failed = generate_scope(args.server, args.package, state, args.scope)
        print(f"{args.scope} 批次候选已生成 · 质量失败 {len(failed)}；脚本没有采用、批准或发布任何候选。", flush=True)
        return 2 if failed else 0
    for variant, first, second in CALIBRATION:
        state = ensure_managed_basis(args.server, args.package, state, variant, first, second)
    bases = calibration_bases(state)
    state, recipes = persist_recipes(args.server, args.package, state, bases)
    failed: list[str] = []
    for basis in bases:
        try:
            result = generate_recipe(args.server, recipes[basis["audioCueId"]], accept_failed=True)
            if result.get("status") != "Succeeded": failed.append(basis["audioCueId"])
        except RuntimeError as error:
            failed.append(basis["audioCueId"])
            print(f"校准质量门禁失败，继续下一组：{error}", flush=True)
    print(f"校准批次已完成 · 质量失败 {len(failed)}/{len(bases)}。请在 Audio Workbench 逐个试听；脚本没有采用、批准或发布任何候选。", flush=True)
    return 2 if failed else 0


if __name__ == "__main__":
    try: raise SystemExit(main())
    except (RuntimeError, TimeoutError, KeyError) as error:
        print(f"ERROR: {error}", file=sys.stderr, flush=True)
        raise SystemExit(1)
