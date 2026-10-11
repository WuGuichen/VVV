#!/usr/bin/env python3
"""Debug: check specific file JSON structure"""

import json
import os

WGAME_DIR = os.environ.get(
    "WGAME_DIR",
    os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "WGame")),
)
path = os.path.join(
    WGAME_DIR,
    "Client/Assets/Res/SplitAbilityData/TestGroup/0002_Base_Attack1.json",
)
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

events = data.get("E", [])
print(f"Total events: {len(events)}")
for i, evt in enumerate(events[:3]):
    t = evt.get("T")
    c = evt.get("C")
    d = evt.get("D")
    print(f"\nEvent {i}:")
    print(f"  C: {c}")
    print(f"  T: {t} (type={type(t).__name__})")
    if isinstance(d, dict):
        print(f"  D keys: {list(d.keys())}")
        for dk, dv in d.items():
            if isinstance(dv, list):
                print(f"    D.{dk}: list(len={len(dv)})")
                print(f"      values: {dv[:10]}")
            elif isinstance(dv, dict):
                print(f"    D.{dk}: dict(keys={list(dv.keys())[:5]})")
            else:
                print(f"    D.{dk}: {type(dv).__name__} = {dv}")
    else:
        print(f"  D: {type(d).__name__} = {str(d)[:80]}")
