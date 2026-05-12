#!/usr/bin/env python3
"""
Analyze all WGame Ability JSON files to extract structure, statistics, and anomalies.
Output: statistical report for ABILITY_JSON_AUDIT_RESULT.md

This script does NOT extract actual game data values — only structural statistics.
"""

import json
import os
import glob
from collections import Counter, defaultdict


DEFAULT_WGAME_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "WGame"))
WGAME_DIR = os.environ.get("WGAME_DIR", DEFAULT_WGAME_DIR)
DATA_DIRS = {
    "TestGroup": "Client/Assets/Res/SplitAbilityData/TestGroup",
    "SkillData": "Client/Assets/Res/SplitAbilityData/SkillData",
    "MapTriggerData": "Client/Assets/Res/SplitAbilityData/MapTriggerData",
    "MapSettingData": "Client/Assets/Res/SplitAbilityData/MapSettingData",
    "WyvernData": "Client/Assets/Res/SplitAbilityData/WyvernData",
}


# EventDataType enum (from EventDataType.cs)
EVENT_TYPE_NAMES = {
    0: "PlayAnim",
    1: "PlayEffectCharacter",
    2: "PlayEffectDuration",
    3: "PlayAudio",
    4: "NoticeMessage",
    5: "AddMessageReceiver",
    6: "AddBuff",
    7: "DoAction",
    8: "LockTick",
    9: "SetTimeScale",
    10: "FocusKeepDist",
    11: "FocusDoForce",
    12: "FocusDoApproach",
    13: "FocusDoFaceTo",
    14: "FinishTargetHit",
    15: "TriggerInputToMotion",
    16: "TriggerStateToMotion",
    17: "TriggerInputToAbility",
    18: "TriggerStateToAbility",
    19: "AddHitTriggerSphere",
    20: "AddHitTriggerBox",
    21: "SetOwnerProperty",
    22: "SetOwnerAttr",
    23: "SetState",
    24: "OpenWeapon",
    25: "ActionPoise",
    26: "CostMP",
    27: "SetTimeArea",
    28: "SetMoveParam",
    29: "Interrupt",
    30: "MoveToPoint",
    31: "MoveToSpecialPoint",
    32: "MapAddCharacter",
    33: "MapTriggerInputToAction",
    34: "MapTriggerSpherePortal",
    35: "MapTriggerDoAction",
    36: "MapTriggerDoActionCondition",
    37: "MapTriggerDoMultiAction",
    38: "MapTriggerDoMultiActionCondition",
    39: "MapTriggerEvent",
    40: "MapTriggerNPC",
    41: "MapTriggerDialog",
    42: "SetWeaponPosition",
    43: "SetWeaponRotation",
    44: "EventAddRayHitTrigger",
    45: "EventAddSectorHitTrigger",
    46: "EventCastSkill",
    47: "MapTriggerDialogDecorate",
    48: "MapTriggerDurationAction",
    49: "MapTriggerEventAction",
    50: "MapTriggerMotionToAction",
    51: "MapTriggerItem",
    52: "MapTriggerMoveNPC",
    53: "EventPlayEffectSkill",
    54: "EventSetFakeShadow",
    55: "SetStatusBuff",
    56: "EventOpenShield",
    57: "TargetStateToAbility",
    58: "EventSummon",
    59: "PlayAudioPreset",
    60: "SetHitEffectOpen",
    61: "SetGroundHitEffect",
    62: "EventGroundSkill",
    63: "FocusDoAttach",
    64: "EventAddSpineRotation",
    65: "EventSetAttackShadow",
    66: "TriggerToAbility",
}


def load_json(path):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, UnicodeDecodeError) as e:
        return {"_parse_error": str(e)}


def analyze_d_structure(d_obj, t_value):
    """Analyze D field structure: return (structure_type, field_names_or_keys)"""
    if not isinstance(d_obj, dict):
        return "non-dict", [str(type(d_obj))]
    
    keys = list(d_obj.keys())
    result_parts = []
    for k in keys:
        v = d_obj[k]
        if isinstance(v, list):
            types_in_list = [type(el).__name__ for el in v[:10]]
            result_parts.append(f"{k}:array[{len(v)}]({','.join(types_in_list)})")
        elif isinstance(v, dict):
            inner_keys = list(v.keys())
            result_parts.append(f"{k}:object{{{','.join(inner_keys[:5])}}}")
        else:
            result_parts.append(f"{k}:{type(v).__name__}")
    return "; ".join(result_parts)


def main():
    # 1. File count and _index.json analysis
    print("=" * 60)
    print("=== 1. FILE COUNTS & _INDEX.JSON ===")
    print("=" * 60)
    
    dir_stats = {}
    for dir_name, rel_path in DATA_DIRS.items():
        full_path = os.path.join(WGAME_DIR, rel_path)
        if not os.path.isdir(full_path):
            print(f"[SKIP] {dir_name} not found at {full_path}")
            continue
        
        all_files = [f for f in os.listdir(full_path) 
                     if f.endswith(".json") and not f.startswith(".")]
        json_files = [f for f in all_files if f != "_index.json"]
        has_index = "_index.json" in all_files
        
        index_content = None
        if has_index:
            idx_path = os.path.join(full_path, "_index.json")
            index_content = load_json(idx_path)
        
        dir_stats[dir_name] = {
            "path": full_path,
            "total_json": len(json_files),
            "has_index": has_index,
            "index": index_content,
        }
        
        print(f"\n--- {dir_name} ({full_path}) ---")
        print(f"  JSON files (excl. _index): {len(json_files)}")
        
        if index_content:
            if isinstance(index_content, list):
                print(f"  _index.json: list[{len(index_content)}] entries")
                if len(index_content) > 0:
                    sample = index_content[0]
                    print(f"  First entry type: {type(sample).__name__}")
                    if isinstance(sample, dict):
                        print(f"  First entry keys: {list(sample.keys())[:8]}")
            elif isinstance(index_content, dict):
                print(f"  _index.json: dict[{len(index_content)}] keys")
                print(f"  Keys: {list(index_content.keys())[:10]}")
    
    # 2. Analyze all ability JSON files for B, E, P fields
    print("\n" + "=" * 60)
    print("=== 2. B (BASE) FIELD ANALYSIS ===")
    print("=" * 60)
    
    b_field_stats = Counter()
    b_inconsistencies = []
    b_lengths = Counter()
    b_type_counts = Counter()  # type per position
    
    e_field_stats = Counter()
    total_events = 0
    event_type_counter = Counter()
    event_c_structure = Counter()  # C array length
    event_d_structure_by_t = defaultdict(Counter)  # D key patterns per T
    
    p_field_stats = Counter()
    total_properties = 0
    p_key_types = Counter()  # type of K value
    p_value_types = Counter()  # type of V value
    
    unknown_event_types = Counter()
    
    abilities_with_empty_b = 0
    abilities_with_empty_p = 0
    abilities_with_empty_e = 0
    
    d_key_patterns = defaultdict(set)  # T -> set of D key sets (as frozenset)
    d_field_counts = Counter()  # (T, key_name) -> count
    
    triggers_have_event_data = defaultdict(set)
    
    for dir_name, rel_path in DATA_DIRS.items():
        full_path = os.path.join(WGAME_DIR, rel_path)
        if not os.path.isdir(full_path):
            continue
        
        json_files = glob.glob(os.path.join(full_path, "*.json"))
        json_files = [f for f in json_files if not f.endswith("_index.json")]
        
        for filepath in json_files:
            data = load_json(filepath)
            if "_parse_error" in data:
                continue
            
            basename = os.path.basename(filepath)
            
            # Check top-level keys
            top_keys = set(data.keys()) if isinstance(data, dict) else set()
            
            # B field
            b = data.get("B", None)
            if b is None or (isinstance(b, list) and len(b) == 0):
                abilities_with_empty_b += 1
                continue
            
            if isinstance(b, list):
                b_len = len(b)
                b_lengths[b_len] += 1
                
                # Check type at each position
                for idx, val in enumerate(b):
                    b_type_counts[(idx, type(val).__name__)] += 1
                    
                    # Check for base field patterns: [ID, Name, TotalTime]
                    if idx == 0 and isinstance(val, int):
                        pass  # ID
                    elif idx == 1 and isinstance(val, str):
                        pass  # Name  
                    elif idx == 2 and isinstance(val, int):
                        pass  # TotalTime
            
            # E field (events)
            e = data.get("E", None)
            if e is None or (isinstance(e, list) and len(e) == 0):
                abilities_with_empty_e += 1
                continue
            
            if isinstance(e, list):
                total_events += len(e)
                e_field_stats[len(e)] += 1
                
                for evt in e:
                    if not isinstance(evt, dict):
                        continue
                    
                    evt_keys = set(evt.keys())
                    
                    # C field
                    c = evt.get("C", None)
                    if isinstance(c, list):
                        event_c_structure[len(c)] += 1
                    
                    # T field (event type)
                    t_val = evt.get("T", None)
                    if t_val is not None and isinstance(t_val, int):
                        event_type_counter[t_val] += 1
                        if t_val not in EVENT_TYPE_NAMES:
                            unknown_event_types[t_val] += 1
                    
                    # D field
                    d = evt.get("D", None)
                    if d is not None and isinstance(d, dict):
                        d_keys = frozenset(d.keys())
                        d_key_patterns[t_val].add(d_keys)
                        for dk in d.keys():
                            d_field_counts[(t_val, dk)] += 1
            
            # P field (properties)
            p = data.get("P", None)
            if p is None or (isinstance(p, list) and len(p) == 0):
                abilities_with_empty_p += 1
                continue
            
            if isinstance(p, list):
                total_properties += len(p)
                p_field_stats[len(p)] += 1
                
                for prop in p:
                    if not isinstance(prop, dict):
                        continue
                    
                    # K/V pattern
                    k = prop.get("K", None)
                    v = prop.get("V", None)
                    
                    if k is not None:
                        p_key_types[type(k).__name__] += 1
                    
                    if v is not None:
                        p_value_types[type(v).__name__] += 1
    
    print(f"\nB field length distribution:")
    for length, count in sorted(b_lengths.items()):
        print(f"  len={length}: {count} abilities")
    
    print(f"\nB field type pattern (by index):")
    for (idx, typename), count in sorted(b_type_counts.items()):
        print(f"  idx[{idx}]: {typename} ({count}x)")
    
    print(f"\nAbilities with empty B: {abilities_with_empty_b}")
    print(f"Abilities with empty E: {abilities_with_empty_e}")
    print(f"Abilities with empty P: {abilities_with_empty_p}")
    
    # 3. Event type distribution
    print("\n" + "=" * 60)
    print("=== 3. EVENT TYPE (T) DISTRIBUTION ===")
    print("=" * 60)
    
    print(f"\nTotal events: {total_events}")
    print(f"\nEvent type distribution (top 30):")
    for t_val, count in event_type_counter.most_common(30):
        name = EVENT_TYPE_NAMES.get(t_val, f"UNKNOWN[{t_val}]")
        pct = count / total_events * 100
        print(f"  T={t_val:3d} ({name:35s}): {count:5d} ({pct:5.1f}%)")
    
    if unknown_event_types:
        print(f"\nUnknown event types found:")
        for t_val, count in unknown_event_types.most_common():
            print(f"  T={t_val}: {count}x")
    
    # 4. C field structure
    print("\n" + "=" * 60)
    print("=== 4. C (EVENT COMMON) FIELD STRUCTURE ===")
    print("=" * 60)
    
    print(f"\nC array length distribution:")
    for length, count in sorted(event_c_structure.items()):
        print(f"  C len={length}: {count} events")
    
    # 5. D field structure by T
    print("\n" + "=" * 60)
    print("=== 5. D FIELD STRUCTURE BY EVENT TYPE ===")
    print("=" * 60)
    
    print(f"\nD key patterns (top event types):")
    for t_val, count in event_type_counter.most_common(20):
        name = EVENT_TYPE_NAMES.get(t_val, f"UNKNOWN[{t_val}]")
        patterns = d_key_patterns.get(t_val, set())
        if patterns:
            # Show field counts
            field_counts = [(k, v) for (et, k), v in d_field_counts.items() if et == t_val]
            field_counts.sort(key=lambda x: -x[1])
            keys_str = ", ".join(f"{k}({v}x)" for k, v in field_counts[:10])
            print(f"\n  T={t_val:3d} ({name:30s}) [{len(patterns)} patterns]")
            print(f"    Fields: {keys_str}")
            # Show each unique pattern
            for i, pat in enumerate(sorted(patterns, key=lambda x: sorted(x))):
                print(f"    Pattern {i+1}: {sorted(pat)}")
    
    # 6. P field analysis
    print("\n" + "=" * 60)
    print("=== 6. P (PROPERTIES) FIELD ANALYSIS ===")
    print("=" * 60)
    
    print(f"\nTotal properties: {total_properties}")
    print(f"\nP array length distribution (top 20):")
    for length, count in sorted(p_field_stats.most_common(20)):
        print(f"  len={length:3d}: {count:5d} abilities")
    
    print(f"\nP.K type: {dict(p_key_types)}")
    print(f"P.V type: {dict(p_value_types)}")
    
    # 7. Anomalies
    print("\n" + "=" * 60)
    print("=== 7. ANOMALIES ===")
    print("=" * 60)
    
    # Check for C field with wrong length (should be 6)
    c_wrong_length = 0
    c_length_values = Counter()
    
    for dir_name, rel_path in DATA_DIRS.items():
        full_path = os.path.join(WGAME_DIR, rel_path)
        if not os.path.isdir(full_path):
            continue
        
        json_files = glob.glob(os.path.join(full_path, "*.json"))
        json_files = [f for f in json_files if not f.endswith("_index.json")]
        
        for filepath in json_files:
            data = load_json(filepath)
            if "_parse_error" in data:
                continue
            
            e = data.get("E", [])
            if not isinstance(e, list):
                continue
            
            for evt in e:
                if not isinstance(evt, dict):
                    continue
                
                c = evt.get("C", None)
                if isinstance(c, list):
                    c_length_values[len(c)] += 1
                elif c is None:
                    pass  # Handled separately
    
    print(f"\nC array length distribution (detailed):")
    for length, count in sorted(c_length_values.items()):
        expected = "expected=6"
        print(f"  len={length}: {count}x {expected}")
    
    # Check non-dict D fields
    non_dict_d = 0
    for dir_name, rel_path in DATA_DIRS.items():
        full_path = os.path.join(WGAME_DIR, rel_path)
        if not os.path.isdir(full_path):
            continue
        
        json_files = glob.glob(os.path.join(full_path, "*.json"))
        json_files = [f for f in json_files if not f.endswith("_index.json")]
        
        for filepath in json_files:
            data = load_json(filepath)
            if "_parse_error" in data:
                continue
            
            e = data.get("E", [])
            if not isinstance(e, list):
                continue
            
            for evt in e:
                if not isinstance(evt, dict):
                    continue
                d = evt.get("D", None)
                if d is not None and not isinstance(d, dict):
                    non_dict_d += 1
    
    print(f"\nNon-dict D fields: {non_dict_d}")
    
    # Missing fields
    print(f"\nNote: Some abilities have empty B/E/P arrays - this may be intentional")
    print(f"      (placeholder entries or zero-event triggers)")


if __name__ == "__main__":
    main()
