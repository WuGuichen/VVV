#!/usr/bin/env python3
"""Extended analysis: P.V structure, _index.json details, specific event D structures"""

import json, os, glob
from collections import Counter, defaultdict

DEFAULT_WGAME_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "WGame"))
WGAME_DIR = os.environ.get("WGAME_DIR", DEFAULT_WGAME_DIR)

# 1. _index.json structure
print("=" * 60)
print("=== _INDEX.JSON STRUCTURE ===")
print("=" * 60)

for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    path = os.path.join(WGAME_DIR, "Client/Assets/Res/SplitAbilityData", dir_name, "_index.json")
    with open(path, "r") as f:
        idx = json.load(f)
    print(f"\n--- {dir_name} ---")
    print(f"  GroupName:    {idx.get('GroupName')}")
    print(f"  StorageMode:  {idx.get('StorageMode')}")
    print(f"  LastUpdate:   {idx.get('LastUpdate')}")
    print(f"  TotalFiles:   {idx.get('TotalFiles')}")
    files = idx.get("Files", [])
    print(f"  Files entries: {len(files)}")
    if files:
        if isinstance(files[0], dict):
            print(f"  File entry keys: {list(files[0].keys())}")

# 2. P.V structure analysis
print("\n" + "=" * 60)
print("=== P.V STRUCTURE ANALYSIS ===")
print("=" * 60)

pv_type_counter = Counter()
pv_inner_keys = Counter()
sample_props = []
all_p_keys = Counter()

for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    full_path = os.path.join(WGAME_DIR, "Client/Assets/Res/SplitAbilityData", dir_name)
    for fp in glob.glob(os.path.join(full_path, "*.json")):
        if "_index" in fp:
            continue
        try:
            with open(fp, "r") as f:
                data = json.load(f)
        except:
            continue
        p = data.get("P", [])
        if not isinstance(p, list):
            continue
        for prop in p:
            if not isinstance(prop, dict):
                continue
            k = prop.get("K", "NO-KEY")
            v = prop.get("V", "NO-VAL")
            all_p_keys[k] += 1
            pv_type_counter[type(v).__name__] += 1
            if isinstance(v, dict):
                for ik in v.keys():
                    pv_inner_keys[ik] += 1
            if len(sample_props) < 10:
                sample_props.append((k, v))

print(f"P.V type distribution: {dict(pv_type_counter)}")
print(f"\nP.K unique count: {len(all_p_keys)}")
print(f"P.K top keys:")
for k, cnt in all_p_keys.most_common(30):
    print(f"  {k:30s}: {cnt:4d}")

print(f"\nP.V inner keys (for dict type):")
for k, cnt in pv_inner_keys.most_common(20):
    print(f"  {k}: {cnt}")

print(f"\nSample properties (first 10):")
for k, v in sample_props:
    if isinstance(v, dict):
        v_short = "{" + ", ".join(f"{ik}:{type(iv).__name__}" for ik, iv in v.items()) + "}"
    else:
        v_short = f"{type(v).__name__}({str(v)[:50]})"
    print(f"  K={k:30s} V={v_short}")

# 3. D field sample for top event types
print("\n" + "=" * 60)
print("=== SAMPLE D FIELD STRUCTURES (TOP EVENT TYPES) ===")
print("=" * 60)

# Collect samples per event type
d_samples = defaultdict(list)
for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    full_path = os.path.join(WGAME_DIR, "Client/Assets/Res/SplitAbilityData", dir_name)
    for fp in glob.glob(os.path.join(full_path, "*.json")):
        if "_index" in fp:
            continue
        try:
            with open(fp, "r") as f:
                data = json.load(f)
        except:
            continue
        e = data.get("E", [])
        if not isinstance(e, list):
            continue
        for evt in e:
            if not isinstance(evt, dict):
                continue
            t = evt.get("T", None)
            d = evt.get("D", None)
            if t is not None and isinstance(t, int) and isinstance(d, dict):
                if len(d_samples[t]) < 3:
                    d_samples[t].append(d)

EVENT_TYPE_NAMES = {
    0: "PlayAnim", 1: "PlayEffectCharacter", 2: "PlayEffectDuration",
    3: "PlayAudio", 6: "AddBuff", 7: "DoAction", 10: "FocusKeepDist",
    12: "FocusDoApproach", 13: "FocusDoFaceTo", 16: "TriggerStateToMotion",
    19: "AddHitTriggerSphere", 23: "SetState", 24: "OpenWeapon",
    25: "ActionPoise", 26: "CostMP", 28: "SetMoveParam", 29: "Interrupt",
    32: "MapAddCharacter", 35: "MapTriggerDoAction", 53: "EventPlayEffectSkill",
    56: "EventOpenShield", 59: "PlayAudioPreset", 61: "SetGroundHitEffect",
    62: "EventGroundSkill", 64: "EventAddSpineRotation",
    41: "MapTriggerDialog", 30: "MoveToPoint",
}

top_types = [28, 29, 0, 32, 26, 13, 25, 3, 59, 24, 10, 23, 61, 19, 12, 7, 6, 16, 64, 1]
for t in top_types:
    name = EVENT_TYPE_NAMES.get(t, f"T={t}")
    samples = d_samples.get(t, [])
    if not samples:
        print(f"\nT={t:3d} ({name:25s}): [NO SAMPLES]")
        continue
    print(f"\nT={t:3d} ({name:25s}):")
    for i, d in enumerate(samples[:2]):
        # Pretty-print with depth limit
        def pprint(obj, indent=2):
            if isinstance(obj, dict):
                items = []
                for k, v in obj.items():
                    if isinstance(v, dict):
                        sub = "{" + ", ".join(f"{sk}:{type(sv).__name__}" for sk, sv in v.items()) + "}"
                        items.append(f"  {k}: {sub}")
                    elif isinstance(v, list):
                        types = [type(el).__name__ for el in v[:8]]
                        items.append(f"  {k}: [{', '.join(types)}] (len={len(v)})")
                    else:
                        items.append(f"  {k}: {type(v).__name__}")
                print(f"    Sample {i+1}: {{")
                for item in items:
                    print(f"      {item}")
                print(f"    }}")
            else:
                print(f"    Sample {i+1}: {type(d).__name__} = {str(d)[:80]}")
