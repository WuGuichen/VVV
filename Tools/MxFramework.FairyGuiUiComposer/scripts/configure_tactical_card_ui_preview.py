"""Upsert the controlled test-card UI preview recipes and their screen owner.

Only Composer source JSON is written. Apply/Publish belongs to the normal CLI.
Unrelated recipes and screen nodes retain their original serialized text.
"""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "ArtSource/UIKits/MxUiFoundation"
TOOL_BUTTON = "ui.component.button.card_ui_preview"
CARD = "ui.component.tactical.player_card_face"
DETAIL = "ui.component.tactical.card_ui_preview_detail"
DIALOG = "ui.component.runtime_ai.card_ui_preview"
OVERLAY = "ui.component.runtime_ai.card_ui_preview_overlay"
HINT = "ui.component.tactical.approach_action_hint"
HINT_DETAILS = "ui.component.tactical.approach_action_details"
ACTION_TOKEN = "ui.component.tactical.action_name_token"
MOVEMENT_TERM = "ui.component.tactical.basic_movement_term"
MOVEMENT_DETAILS = "ui.component.tactical.basic_movement_details"
RETREAT_TERM = "ui.component.tactical.retreat_term"
RETREAT_DETAILS = "ui.component.tactical.retreat_details"
ORBIT_DETAILS = "ui.component.tactical.orbit_details"
KEEP_DISTANCE_DETAILS = "ui.component.tactical.keep_distance_details"
FACING_STEP_DETAILS = "ui.component.tactical.facing_step_details"
DIALOG_WIDTH = 936
PARAMETER_WIDTH = 336
PARAMETER_PADDING = 16
PARAMETER_CELL = 144
PARAMETER_GAP = 16
PARAMETER_ROW = 36
PARAMETER_TOP = 48


def node(name, kind, x, y, w, h, **fields):
    return {"nodeId": name, "name": name, "type": kind, "order": 0, "bindingSlot": "",
            "parentContainerId": "", "visible": True,
            "layout": {"x": x, "y": y, "width": w, "height": h, "horizontalAnchor": "Left", "verticalAnchor": "Top"},
            "graph": None, "text": None, "image": None, "component": None, "list": None, **fields}


def graph(name, x, y, w, h, color, radius=0):
    return node(name, "Graph", x, y, w, h, graph={"fillColor": color, "cornerRadius": radius})


def text(name, value, x, y, w, h, size=16, color="#ffe1dfd3", bold=False, single=False, align="left"):
    return node(name, "Text", x, y, w, h, text={"value": value, "fontSize": size, "color": color,
                "font": "MxUI Serif SC" if name in ("name", "title") else "MxUI Sans SC" if bold or name == "cost" else "MxUI Sans SC",
                "align": align, "bold": False, "singleLine": single, "input": False, "restrict": "", "maxLength": 0})


def image(name, resource, x, y, w, h, visible=True):
    return node(name, "Image", x, y, w, h, visible=visible, image={"resourceId": resource, "tint": "#ffffffff"})


def generated_image(name, resource, x, y, w, h, preserve=False, visible=True):
    # Place the unchanged PNG by its opaque-content bounds; do not crop or repaint generated art.
    manifest = json.loads((LIBRARY/"source/card_foundation_ai_v1/manifest.json").read_text(encoding="utf-8"))
    asset = next(a for a in manifest["assets"] if a["resourceId"] == resource)
    bounds, pixels = asset["contentRect"], asset["pixelSize"]
    sx, sy = w/bounds["width"], h/bounds["height"]
    if preserve:
        sx = sy = min(sx, sy)
    left = x + (w-bounds["width"]*sx)/2 - bounds["x"]*sx
    top = y + (h-bounds["height"]*sy)/2 - bounds["y"]*sy
    return image(name, resource, round(left), round(top), round(pixels["width"]*sx), round(pixels["height"]*sy), visible)


def component(name, recipe, title, x, y, w, h, visible=True):
    return node(name, "ComponentInstance", x, y, w, h, visible=visible,
                component={"componentRecipeId": recipe, "title": title, "iconResourceId": ""})


def recipe(key, name, w, h, nodes):
    for index, item in enumerate(nodes):
        item["order"] = index
    return {"componentId": key, "displayName": name, "componentType": "Composite", "fairyGuiComponentName": name,
            "defaultSize": {"width": w, "height": h}, "resourceSlots": [], "instanceProperties": [],
            "statePresentation": None, "template": {"nodes": nodes}}


def parameter_details(key, name, title, fields):
    """One shared two-column grid for every parameter window; runtime may span long values."""
    height = PARAMETER_TOP + ((len(fields) + 1) // 2) * PARAMETER_ROW + PARAMETER_PADDING
    nodes = [generated_image("background", "ui.card.imagegen.tooltip.base", 0, 0, PARAMETER_WIDTH, height),
             text("title", title, PARAMETER_PADDING, 12, 304, 24, 16, single=True),
             graph("separator", PARAMETER_PADDING, 40, 304, 1, "#ff55625d")]
    color = "#ffe1dfd3"
    for index, field in enumerate(fields):
        x = PARAMETER_PADDING + index % 2 * (PARAMETER_CELL + PARAMETER_GAP)
        y = PARAMETER_TOP + index // 2 * PARAMETER_ROW
        if field == "movement":
            icons = [graph("footLeft", x+3, y+6, 5, 10, color, 2), graph("heelLeft", x+4, y+18, 4, 4, color, 2),
                     graph("footRight", x+12, y+13, 5, 10, color, 2), graph("heelRight", x+13, y+25, 4, 4, color, 2)]
        elif field == "rotation":
            icons = [graph("rotationArc", x, y+6, 24, 24, color), graph("rotationArrow", x, y+6, 24, 24, color)]
        elif field == "distance":
            icons = [graph("distanceLeft", x, y+9, 2, 18, color), graph("distanceRight", x+22, y+9, 2, 18, color),
                     graph("distanceLine", x, y+17, 24, 2, color)]
        elif field == "facing":
            icons = [graph("facingIcon", x+2, y+8, 20, 20, "#00ffffff")]
        elif field == "direction":
            icons = [text("directionIcon", "↔", x, y+4, 24, 28, 20, single=True)]
        elif field in ("duration", "preparationDuration"):
            prefix = "preparationClock" if field == "preparationDuration" else "clock"
            icons = [graph(prefix+"Outer", x, y+6, 24, 24, color, 12), graph(prefix+"Inner", x+2, y+8, 20, 20, "#ff202b2c", 10),
                     graph(prefix+"Minute", x+11, y+10, 2, 10, color), graph(prefix+"Hour", x+11, y+18, 8, 2, color)]
        else:
            raise ValueError(field)
        value = field + "Multiplier" if field in ("movement", "rotation") else field
        nodes += icons + [text(value, "—", x+32, y+4, PARAMETER_CELL-32, 28, 20, single=True),
                          graph(field+"Field", x, y, PARAMETER_CELL, PARAMETER_ROW, "#00ffffff")]
    return recipe(key, name, PARAMETER_WIDTH, height, nodes)


def definitions(existing):
    button = copy.deepcopy(next(r for r in existing if r["componentId"] == "ui.component.button.tool.neutral"))
    button.update(componentId=TOOL_BUTTON, displayName="卡面预览工具按钮", fairyGuiComponentName="CardUiPreviewButton",
                  defaultSize={"width": 160, "height": 44})
    button["resourceSlots"] = [{"slotId": "base", "resourceId": "ui.button.menu.standard.normal"}]
    button["statePresentation"].update(titleFont="MxUI Sans SC", titleBold=False, titleFontSize=15, titleColor="#e1dfd3", iconColor="#e1dfd3",
                                       overOverlayColor="#00000000", downOverlayColor="#00000000")
    token = recipe(ACTION_TOKEN, "TacticalActionNameToken", 266, 38, [
        generated_image("background", "ui.card.imagegen.action_token.base", 0, 0, 266, 38),
        text("name", "", 8, 5, 226, 28, 19, "#ff294842", single=True),
        image("edit", "ui.icon.minimal.edit", 242, 10, 18, 18)])
    movement_term = recipe(MOVEMENT_TERM, "TacticalBasicMovementTerm", 100, 34, [
        text("label", "常规移动", 0, 0, 100, 34, 23, "#ff294842", single=True)])
    retreat_term = recipe(RETREAT_TERM, "TacticalRetreatTerm", 58, 34, [
        text("label", "后撤", 0, 0, 58, 34, 23, "#ff294842", single=True)])
    hint = recipe(HINT, "TacticalApproachActionHint", 326, 102, [
        text("leadPrefix", "", 0, 0, 28, 34, 23, "#ff222c2a", single=True),
        component("movement", MOVEMENT_TERM, "", 25, 0, 100, 34),
        text("leadSuffix", "", 125, 0, 201, 34, 23, "#ff222c2a", single=True),
        text("verb", "", 0, 43, 56, 34, 23, "#ff222c2a", single=True),
        component("action", ACTION_TOKEN, "", 60, 40, 266, 38),
        component("retreat", RETREAT_TERM, "", 125, 0, 58, 34, False)])
    hint_details = parameter_details(HINT_DETAILS, "TacticalApproachActionDetails", "出招参数", ["distance", "facing", "duration"])
    movement_details = parameter_details(MOVEMENT_DETAILS, "TacticalBasicMovementDetails", "常规移动", ["movement", "rotation"])
    retreat_details = parameter_details(RETREAT_DETAILS, "TacticalRetreatDetails", "后撤参数", ["movement", "rotation", "duration"])
    orbit_details = parameter_details(ORBIT_DETAILS, "TacticalOrbitDetails", "环绕参数", ["movement", "rotation", "distance", "direction", "duration"])
    keep_distance_details = parameter_details(KEEP_DISTANCE_DETAILS, "TacticalKeepDistanceDetails", "守距参数", ["movement", "rotation", "distance", "duration"])
    facing_step_details = parameter_details(FACING_STEP_DETAILS, "TacticalFacingStepDetails", "垫步参数", ["rotation", "facing", "preparationDuration", "duration"])
    card = recipe(CARD, "TacticalPlayerCardFace", 378, 528, [
        graph("skinMask", 0, 0, 378, 528, "#ffffffff", 8),
        generated_image("background", "ui.card.imagegen.behavior.base", 0, 0, 378, 528),
        graph("costPlate", 15, 14, 48, 48, "#ff202b2c", 24),
        text("cost", "—", 18, 19, 42, 36, 24, single=True, align="center"),
        text("name", "", 86, 14, 267, 45, 28, bold=True, single=True),
        image("icon", "ui.icon.behavior.fallback", 133, 94, 112, 112),
        generated_image("swordIllustration", "ui.card.imagegen.sword.illustration", 26, 90, 326, 122, preserve=True, visible=False),
        image("actionEngraving", "ui.standard.card.engraving.action", 0, 0, 378, 528, False),
        image("utilityEngraving", "ui.standard.card.engraving.utility", 0, 0, 378, 528, False),
        graph("separatorLeft", 26, 230, 128, 1, "#ff92958a"),
        graph("separatorRight", 222, 230, 128, 1, "#ff92958a"),
        image("qualityDiamond", "ui.standard.card.type.action", 174, 215, 30, 30),
        graph("qualityCircle", 174, 215, 30, 30, "#ff858782", 15),
        text("description", "", 26, 267, 326, 150, 23, "#ff222c2a"),
        component("behaviorHint", HINT, "", 26, 267, 326, 102, False),
        node("behaviorHints", "List", 26, 267, 326, 150, visible=False, list={"itemComponentRecipeId": HINT,
             "layout": "Column", "lineGap": 2, "columnGap": 0, "selectionMode": "None", "overflow": "Scroll", "virtualized": False}),
        graph("focus", 0, 0, 378, 528, "#ffe1dfd3", 8),
    ])
    detail = recipe(DETAIL, "TacticalCardUiPreviewDetail", 400, 100, [text("body", "", 0, 0, 396, 96, 16)])
    nodes = [graph("background", 0, 0, DIALOG_WIDTH, 674, "#ff202b2c", 10),
             text("title", "卡面预览 · 动作装配", 24, 16, 600, 34, 24, bold=True),
             text("context", "", 24, 55, DIALOG_WIDTH - 100, 26, 14, "#ffacb6b1"),
             component("btnClose", TOOL_BUTTON, "关闭", DIALOG_WIDTH - 114, 18, 90, 44),
             component("card", CARD, "", 38, 106, 378, 528),
             text("bindingTitle", "预览装配对说明的影响", 458, 100, 420, 30, 18, bold=True),
             component("btnCurrent", TOOL_BUTTON, "当前装配", 458, 138, 140, 44),
             component("btnUnbound", TOOL_BUTTON, "全部未装配", 606, 138, 140, 44),
             component("btnReload", TOOL_BUTTON, "刷新内容", 754, 138, 140, 44),
             text("slotLabel", "", 458, 194, 326, 32, 16, single=True),
             component("btnPreviousSlot", TOOL_BUTTON, "‹", 794, 190, 46, 40),
             component("btnNextSlot", TOOL_BUTTON, "›", 848, 190, 46, 40),
             component("btnChooseAction", TOOL_BUTTON, "选择预览动作", 458, 238, 326, 44),
             component("btnClearAction", TOOL_BUTTON, "清空此槽", 794, 238, 100, 44),
             text("bindingState", "", 458, 294, 436, 46, 14, "#ffacb6b1"),
             text("detailsTitle", "装配前后说明对照", 458, 350, 270, 30, 18, bold=True),
             component("btnCopyDescription", TOOL_BUTTON, "复制说明", 754, 344, 140, 40)]
    nodes += [node("details", "List", 458, 394, 438, 196, list={"itemComponentRecipeId": DETAIL, "layout": "Column",
                  "lineGap": 4, "columnGap": 0, "selectionMode": "None", "overflow": "Scroll", "virtualized": False}),
              text("footer", "动作切换仅用于本次预览；关闭后丢弃。\n当前卡片和角色装配不会被修改。", 458, 600, 440, 48, 14, "#ffacb6b1"),
              image("normalBase", "ui.button.menu.standard.normal", 0, 0, 1, 1, False),
              image("hoverBase", "ui.button.menu.standard.hover", 0, 0, 1, 1, False),
              image("pressedBase", "ui.button.menu.standard.pressed", 0, 0, 1, 1, False)]
    for name, details in [("behaviorHintDetails", hint_details), ("movementHintDetails", movement_details),
                          ("retreatHintDetails", retreat_details), ("orbitHintDetails", orbit_details),
                          ("keepDistanceHintDetails", keep_distance_details), ("facingStepHintDetails", facing_step_details)]:
        nodes.append(component(name, details["componentId"], "", 430, 373,
                               details["defaultSize"]["width"], details["defaultSize"]["height"], False))
    overlay = recipe(OVERLAY, "RuntimeAiLabCardUiPreviewOverlay", 1920, 1080, [
        graph("scrim", 0, 0, 1920, 1080, "#a6000000"),
        component("panel", DIALOG, "", (1920-DIALOG_WIDTH)//2, 203, DIALOG_WIDTH, 674),
        node("parameterWindows", "List", 0, 0, PARAMETER_WIDTH + 10, 448, visible=False,
             list={"itemComponentRecipeId": RETREAT_DETAILS, "layout": "Column", "lineGap": 12,
                   "columnGap": 0, "selectionMode": "None", "overflow": "Scroll", "virtualized": False})])
    return [button, token, movement_term, movement_details, retreat_term, retreat_details, orbit_details, keep_distance_details, facing_step_details, hint, hint_details, card, detail,
            recipe(DIALOG, "RuntimeAiLabCardUiPreview", DIALOG_WIDTH, 674, nodes), overlay]


def upsert_array(path, array_key, identity_key, changes):
    raw = path.read_bytes()
    value = raw.decode("utf-8-sig")
    decoder = json.JSONDecoder()
    start = value.index("[", value.index('"'+array_key+'"'))+1
    cursor = start
    spans = {}
    while True:
        while value[cursor].isspace() or value[cursor] == ',':
            cursor += 1
        if value[cursor] == ']':
            end = cursor
            break
        item, stop = decoder.raw_decode(value, cursor)
        spans[item[identity_key]] = (cursor, stop)
        cursor = stop
    new = []
    replacements = []
    for item in changes:
        encoded = json.dumps(item, ensure_ascii=False, indent=2).replace("\n", "\n    ")
        if item[identity_key] in spans:
            a, b = spans[item[identity_key]]
            replacements.append((a, b, encoded))
        else:
            new.append(encoded)
    if new:
        replacements.append((end, end, (",\n    " if spans else "\n    ") + ",\n    ".join(new) + "\n  "))
    for a, b, encoded in sorted(replacements, reverse=True):
        value = value[:a]+encoded+value[b:]
    json.loads(value)
    if path.read_bytes() != raw:
        raise RuntimeError("Authoring file changed concurrently: "+str(path))
    path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+value.encode("utf-8"))


def main():
    catalog_path = LIBRARY/"ui-component-recipes.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8-sig"))
    changes = definitions(catalog["recipes"])
    face = copy.deepcopy(next(r for r in catalog["recipes"] if r["componentId"] == "ui.component.runtime_ai.configured_card_face"))
    face["template"]["nodes"] = [n for n in face["template"]["nodes"] if n["name"] != "btnPreviewUi"]
    button = component("btnPreviewUi", "ui.component.button.tool.neutral", "预览卡面", 16, 344, 258, 30)
    button["order"] = len(face["template"]["nodes"])
    face["template"]["nodes"].append(button)
    upsert_array(catalog_path, "recipes", "componentId", changes+[face])
    screen = LIBRARY/"screens/character_test_runtime_ai_lab.ui-screen.json"
    root = json.loads(screen.read_text(encoding="utf-8-sig"))
    item = component("tacticalCardUiPreview", OVERLAY, "", 0, 0, 1920, 1080, False)
    previous = next((n for n in root["nodes"] if n["nodeId"] == item["nodeId"]), None)
    item["order"] = previous["order"] if previous else max(n["order"] for n in root["nodes"])+1
    upsert_array(screen, "nodes", "nodeId", [item])
    print("Configured shared player card, preview dialog and library entry in Composer sources.")


if __name__ == "__main__":
    main()
