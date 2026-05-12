#!/usr/bin/env python3
"""Analyze MapSettingData and trigger type distribution"""
import json
import glob
import os
from collections import Counter

DEFAULT_WGAME_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "WGame"))
WGAME_DIR = os.environ.get("WGAME_DIR", DEFAULT_WGAME_DIR)
BASE = "Client/Assets/Res/SplitAbilityData"

# Check MapSettingData structure
print("=== MAPSETTINGDATA SAMPLE ===")
path = f"{WGAME_DIR}/{BASE}/MapSettingData/0006_Environment_1_1.json"
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)
print(f"Top keys: {list(data.keys())}")
print(f"B: {data.get('B')}")
print(f"P count: {len(data.get('P', []))}")
print(f"E count: {len(data.get('E', []))}")
if data.get("E"):
    for i, evt in enumerate(data["E"][:5]):
        c = evt.get("C", [])
        print(f"  Event {i}: T={evt.get('T')}, C=[{c[0] if len(c)>0 else '?'}, {c[1] if len(c)>1 else '?'}, {c[2] if len(c)>2 else '?'}, ...]")

# Trigger type distribution across all files
print("\n=== TRIGGER TYPE (C[2]) DISTRIBUTION ===")
triggers = Counter()
for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    full_path = f"{WGAME_DIR}/{BASE}/{dir_name}"
    for fp in glob.glob(f"{full_path}/*.json"):
        if "_index" in fp:
            continue
        try:
            with open(fp, "r", encoding="utf-8") as f:
                data = json.load(f)
        except:
            continue
        e = data.get("E", [])
        if not isinstance(e, list):
            continue
        for evt in e:
            c = evt.get("C", [])
            if isinstance(c, list) and len(c) > 2:
                triggers[c[2]] += 1
for t, cnt in triggers.most_common():
    print(f"  {t}: {cnt}")

# P.V type structure
print("\n=== P.V (TYPE SYSTEM) SAMPLES ===")
pv_samples = Counter()
for dir_name in ["TestGroup"]:
    full_path = f"{WGAME_DIR}/{BASE}/{dir_name}"
    for fp in glob.glob(f"{full_path}/*.json"):
        if "_index" in fp:
            continue
        try:
            with open(fp, "r", encoding="utf-8") as f:
                data = json.load(f)
        except:
            continue
        p = data.get("P", [])
        if not isinstance(p, list):
            continue
        for prop in p:
            v = prop.get("V", {})
            if isinstance(v, dict):
                t = v.get("T", "NoT")
                vv_type = type(v.get("V", None)).__name__
                pv_samples[(t, vv_type)] += 1

for (t, vt), cnt in sorted(pv_samples.most_common(20)):
    print(f"  T={t:15s} V_type={vt:8s}: {cnt:4d}x")

# No-E abilities (empty event lists)
print("\n=== ABILITIES WITH EMPTY E ===")
empty_e_files = []
for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    full_path = f"{WGAME_DIR}/{BASE}/{dir_name}"
    for fp in glob.glob(f"{full_path}/*.json"):
        if "_index" in fp:
            continue
        try:
            with open(fp, "r", encoding="utf-8") as f:
                data = json.load(f)
        except:
            continue
        e = data.get("E", [])
        if not isinstance(e, list) or len(e) == 0:
            empty_e_files.append((dir_name, fp.split("/")[-1], data.get("B", [None])[1] if isinstance(data.get("B"), list) and len(data.get("B")) > 1 else "?"))
print(f"Total abilities with empty E: {len(empty_e_files)}")
for dir_name, fname, abname in empty_e_files[:10]:
    print(f"  {dir_name}/{fname:40s} B.Name={abname}")
