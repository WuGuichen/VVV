import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
TARGET = ROOT / "ArtSource" / "UIKits" / "MxUiFoundation" / "screens" / "character_test_combat_hud.ui-screen.json"


def layout(x, y, width, height, horizontal="Left", vertical="Top"):
    return {
        "x": x,
        "y": y,
        "width": width,
        "height": height,
        "horizontalAnchor": horizontal,
        "verticalAnchor": vertical,
    }


def image_node(nodes, name, resource_id, bounds, tint="#ffffffff"):
    nodes.append({
        "nodeId": name,
        "name": name,
        "type": "Image",
        "order": len(nodes),
        "bindingSlot": name,
        "layout": bounds,
        "image": {"resourceId": resource_id, "tint": tint},
    })


def graph_node(nodes, name, bounds, fill, radius=0):
    nodes.append({
        "nodeId": name,
        "name": name,
        "type": "Graph",
        "order": len(nodes),
        "bindingSlot": name,
        "layout": bounds,
        "graph": {"fillColor": fill, "cornerRadius": radius},
    })


def text_node(nodes, name, bounds, value, font_size, color, align="left", bold=False):
    nodes.append({
        "nodeId": name,
        "name": name,
        "type": "Text",
        "order": len(nodes),
        "bindingSlot": name,
        "layout": bounds,
        "text": {
            "value": value,
            "fontSize": font_size,
            "color": color,
            "align": align,
            "bold": bold,
            "singleLine": True,
        },
    })


def add_buff_slots(nodes, prefix, start_x, start_y, horizontal, vertical):
    for slot in range(1, 9):
        x = start_x + (slot - 1) * 42
        base = f"{prefix}BuffSlot{slot}"
        graph_node(nodes, base + "Bg", layout(x, start_y, 36, 36, horizontal, vertical), "#e60a1217", 4)
        graph_node(nodes, base + "Border", layout(x, start_y, 36, 36, horizontal, vertical), "#00000000", 4)
        text_node(nodes, base + "Label", layout(x + 2, start_y + 3, 32, 16, horizontal, vertical), "DEF+" if slot == 1 else "", 11, "#f1f6f4", "center", True)
        graph_node(nodes, base + "CooldownFill", layout(x, start_y, 36, 36, horizontal, vertical), "#a6050a0e", 4)
        text_node(nodes, base + "Stack", layout(x + 23, start_y + 20, 11, 12, horizontal, vertical), "", 10, "#ffffff", "right", True)
        text_node(nodes, base + "Time", layout(x + 2, start_y + 24, 32, 10, horizontal, vertical), "", 9, "#e6b969", "center")


def add_ability_slots(nodes):
    start_x = 532
    y = 956
    for slot in range(1, 10):
        x = start_x + (slot - 1) * 96
        base = f"abilitySlot{slot}"
        image_node(nodes, base + "Bg", "ui.hud.ability.slot.frame", layout(x, y, 88, 88, "Center", "Bottom"))
        image_node(nodes, base + "Selected", "ui.hud.ability.slot.selected", layout(x, y, 88, 88, "Center", "Bottom"))
        text_node(nodes, base + "Key", layout(x + 10, y + 8, 22, 22, "Center", "Bottom"), str(slot), 17, "#f2c66f", "left", True)
        text_node(nodes, base + "Name", layout(x + 8, y + 36, 72, 22, "Center", "Bottom"), "Fireball" if slot == 1 else "Empty", 13, "#dce8e5", "center", True)
        graph_node(nodes, base + "CooldownFill", layout(x + 8, y + 72, 72, 10, "Center", "Bottom"), "#c7080d12", 3)
        text_node(nodes, base + "CooldownText", layout(x + 4, y + 58, 80, 20, "Center", "Bottom"), "", 12, "#f3d28d", "center", True)


def build_blueprint():
    nodes = []

    image_node(nodes, "playerPlate", "ui.hud.plate.player.frame", layout(36, 32, 520, 164))
    text_node(nodes, "playerName", layout(96, 44, 344, 30), "Wanderer", 24, "#eaf5f2", "left", True)
    image_node(nodes, "playerHealthIcon", "ui.hud.icon.health", layout(60, 82, 30, 30))
    image_node(nodes, "playerHpTrack", "ui.hud.bar.track", layout(96, 84, 360, 22))
    image_node(nodes, "playerHpFill", "ui.hud.bar.fill.health.player", layout(98, 86, 356, 18))
    text_node(nodes, "playerHpText", layout(464, 82, 66, 24), "100/100", 14, "#dbe7e3", "right", True)
    image_node(nodes, "playerPostureIcon", "ui.hud.icon.pressure", layout(60, 118, 30, 30))
    image_node(nodes, "playerPostureTrack", "ui.hud.bar.track", layout(96, 120, 360, 22))
    image_node(nodes, "playerPostureFill", "ui.hud.bar.fill.pressure", layout(100, 122, 352, 18))
    image_node(nodes, "playerPostureBreakMarker", "ui.hud.bar.break.marker", layout(446, 115, 12, 32))
    text_node(nodes, "playerPostureText", layout(464, 118, 66, 24), "0/100", 14, "#e6c47b", "right", True)
    text_node(nodes, "playerActionText", layout(96, 150, 360, 22), "Action: Ready", 13, "#8fa5a2")
    add_buff_slots(nodes, "player", 96, 206, "Left", "Top")

    image_node(nodes, "targetPlate", "ui.hud.plate.target.frame", layout(1364, 32, 520, 164, "Right", "Top"))
    text_node(nodes, "targetName", layout(1390, 44, 344, 30, "Right", "Top"), "Target", 24, "#f1dfd8", "left", True)
    text_node(nodes, "targetStatusText", layout(1740, 46, 112, 26, "Right", "Top"), "LOCK", 14, "#e9a36b", "right", True)
    image_node(nodes, "targetHpTrack", "ui.hud.bar.track", layout(1390, 84, 360, 22, "Right", "Top"))
    image_node(nodes, "targetHpFill", "ui.hud.bar.fill.health.target", layout(1392, 86, 356, 18, "Right", "Top"))
    text_node(nodes, "targetHpText", layout(1760, 82, 92, 24, "Right", "Top"), "100/100", 14, "#ebdeda", "right", True)
    image_node(nodes, "targetPostureTrack", "ui.hud.bar.track", layout(1390, 120, 360, 22, "Right", "Top"))
    image_node(nodes, "targetPostureFill", "ui.hud.bar.fill.pressure", layout(1394, 122, 352, 18, "Right", "Top"))
    image_node(nodes, "targetPostureBreakMarker", "ui.hud.bar.break.marker", layout(1740, 115, 12, 32, "Right", "Top"))
    text_node(nodes, "targetPostureText", layout(1760, 118, 92, 24, "Right", "Top"), "0/100", 14, "#e6c47b", "right", True)
    add_buff_slots(nodes, "target", 1516, 206, "Right", "Top")

    image_node(nodes, "lockCrosshair", "ui.hud.crosshair.lock", layout(928, 508, 64, 64, "Center", "Middle"))
    image_node(nodes, "networkLatencyBadgeBg", "ui.hud.badge.network", layout(816, 30, 288, 36, "Center", "Top"))
    text_node(nodes, "networkLatencyText", layout(844, 35, 232, 26, "Center", "Top"), "LOCAL · 0 ms", 14, "#71d8b5", "center", True)
    graph_node(nodes, "behaviorCardPanel", layout(36, 896, 480, 76, "Left", "Bottom"), "#e6091116", 8)
    graph_node(nodes, "behaviorCardIcon", layout(52, 908, 52, 52, "Left", "Bottom"), "#e6228f91", 6)
    text_node(nodes, "behaviorCardIconText", layout(52, 917, 52, 34, "Left", "Bottom"), "⚔", 24, "#f4d487", "center", True)
    text_node(nodes, "behaviorCardName", layout(120, 906, 372, 26, "Left", "Bottom"), "基础攻击", 18, "#edf6f3", "left", True)
    text_node(nodes, "behaviorCardReason", layout(120, 935, 372, 22, "Left", "Bottom"), "目标进入攻击范围", 14, "#78d8cf", "left")
    image_node(nodes, "feedbackPanel", "ui.hud.feedback.frame", layout(36, 988, 460, 56, "Left", "Bottom"))
    graph_node(nodes, "feedbackTrim", layout(54, 994, 400, 2, "Left", "Bottom"), "#d8a447")
    text_node(nodes, "feedbackText", layout(66, 1002, 398, 32, "Left", "Bottom"), "", 16, "#d6e2df")
    add_ability_slots(nodes)

    if len(nodes) != 183:
        raise RuntimeError(f"Expected 183 HUD nodes, generated {len(nodes)}.")

    return {
        "schemaVersion": "mx.uiScreenBlueprint.v1",
        "screenId": "ui.screen.character_test.combat_hud",
        "displayName": "CharacterTest 战斗 HUD",
        "libraryId": "MxUiFoundation",
        "designResolution": {"width": 1920, "height": 1080},
        "target": {
            "packageId": "MxCharacterTestCombatHud",
            "componentName": "CombatHudPanel",
            "packageSourcePath": "FGUIProject/assets/MxCharacterTestCombatHud",
        },
        "nodes": nodes,
    }


def main():
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(json.dumps(build_blueprint(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
