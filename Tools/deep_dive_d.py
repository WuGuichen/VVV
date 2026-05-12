#!/usr/bin/env python3
"""Deep dive: D field structure for ALL event types found in data"""
import json, os, glob
from collections import defaultdict

DEFAULT_WGAME_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "WGame"))
WGAME_DIR = os.environ.get("WGAME_DIR", DEFAULT_WGAME_DIR)
EVENT_TYPE_NAMES = {
    0: "PlayAnim", 1: "PlayEffectCharacter", 2: "PlayEffectDuration",
    3: "PlayAudio", 4: "NoticeMessage", 5: "AddMessageReceiver",
    6: "AddBuff", 7: "DoAction", 8: "LockTick", 9: "SetTimeScale",
    10: "FocusKeepDist", 11: "FocusDoForce", 12: "FocusDoApproach",
    13: "FocusDoFaceTo", 14: "FinishTargetHit", 15: "TriggerInputToMotion",
    16: "TriggerStateToMotion", 17: "TriggerInputToAbility",
    18: "TriggerStateToAbility", 19: "AddHitTriggerSphere",
    20: "AddHitTriggerBox", 21: "SetOwnerProperty", 22: "SetOwnerAttr",
    23: "SetState", 24: "OpenWeapon", 25: "ActionPoise", 26: "CostMP",
    27: "SetTimeArea", 28: "SetMoveParam", 29: "Interrupt",
    30: "MoveToPoint", 31: "MoveToSpecialPoint", 32: "MapAddCharacter",
    33: "MapTriggerInputToAction", 34: "MapTriggerSpherePortal",
    35: "MapTriggerDoAction", 36: "MapTriggerDoActionCondition",
    37: "MapTriggerDoMultiAction", 38: "MapTriggerDoMultiActionCondition",
    39: "MapTriggerEvent", 40: "MapTriggerNPC", 41: "MapTriggerDialog",
    42: "SetWeaponPosition", 43: "SetWeaponRotation",
    44: "EventAddRayHitTrigger", 45: "EventAddSectorHitTrigger",
    46: "EventCastSkill", 47: "MapTriggerDialogDecorate",
    48: "MapTriggerDurationAction", 49: "MapTriggerEventAction",
    50: "MapTriggerMotionToAction", 51: "MapTriggerItem",
    52: "MapTriggerMoveNPC", 53: "EventPlayEffectSkill",
    54: "EventSetFakeShadow", 55: "SetStatusBuff", 56: "EventOpenShield",
    57: "TargetStateToAbility", 58: "EventSummon", 59: "PlayAudioPreset",
    60: "SetHitEffectOpen", 61: "SetGroundHitEffect", 62: "EventGroundSkill",
    63: "FocusDoAttach", 64: "EventAddSpineRotation",
    65: "EventSetAttackShadow", 66: "TriggerToAbility",
}

# Collect D samples per event type
d_samples = defaultdict(list)
event_counts = defaultdict(int)
c_samples = defaultdict(list)

for dir_name in ["TestGroup", "SkillData", "MapTriggerData", "MapSettingData"]:
    full_path = os.path.join(WGAME_DIR, "Client/Assets/Res/SplitAbilityData", dir_name)
    for fp in glob.glob(os.path.join(full_path, "*.json")):
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
            if not isinstance(evt, dict):
                continue
            t = evt.get("T", None)
            if t is None or not isinstance(t, int):
                continue
            event_counts[t] += 1
            d = evt.get("D", {})
            if isinstance(d, dict):
                if len(d_samples[t]) < 3:
                    d_samples[t].append(d)
            c = evt.get("C", [])
            if isinstance(c, list) and len(c_samples[t]) < 2:
                c_samples[t].append(c)

# Print D structure for each event type found
for t in sorted(event_counts.keys()):
    name = EVENT_TYPE_NAMES.get(t, f"UNKNOWN[{t}]")
    count = event_counts[t]
    samples = d_samples.get(t, [])
    c_samps = c_samples.get(t, [])
    
    print(f"\nT={t:3d} ({name:25s}) [{count:5d}x]")
    
    # C sample
    if c_samps:
        c0 = c_samps[0]
        print(f"  C: [TrackName(str), TrackIndex(int), TriggerType(str), TriggerTime(int), IsEnabled(bool), Duration(int)]")
        print(f"     Sample: {c0}")
    
    for i, d in enumerate(samples):
        for dk, dv in d.items():
            if isinstance(dv, list):
                types = [type(x).__name__ for x in dv[:12]]
                vals = [str(x)[:40] for x in dv[:12]]
                print(f"  D.{dk}: array[{len(dv)}] [{', '.join(types)}]")
                if len(vals) <= 12:
                    print(f"         values: {vals}")
                else:
                    print(f"         first 12: {vals}")
            elif isinstance(dv, dict):
                inner_summary = {}
                for ik, iv in dv.items():
                    if isinstance(iv, list):
                        inner_summary[ik] = f"array[{len(iv)}]"
                    else:
                        inner_summary[ik] = type(iv).__name__
                print(f"  D.{dk}: object{{{', '.join(f'{k}:{v}' for k,v in inner_summary.items())}}}")
            else:
                print(f"  D.{dk}: {type(dv).__name__} = {str(dv)[:60]}")
