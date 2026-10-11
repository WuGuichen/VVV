import json
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "ArtSource" / "UIKits" / "MxUiFoundation"
LEGACY = ROOT / "FGUIProject" / "assets" / "MxCharacterTestHud"


SCREENS = (
    {
        "source": "SettingsPanel.xml",
        "screenId": "ui.screen.character_test.settings",
        "displayName": "CharacterTest 设置",
        "packageId": "MxCharacterTestSettingsUi",
        "componentName": "SettingsPanel",
        "viewId": "ui.character_test.settings.panel",
        "resourceStem": "settings_ui",
    },
    {
        "source": "NetworkPanel.xml",
        "screenId": "ui.screen.character_test.network",
        "displayName": "CharacterTest 联机大厅",
        "packageId": "MxCharacterTestNetworkUi",
        "componentName": "NetworkPanel",
        "viewId": "ui.character_test.network.panel",
        "resourceStem": "network_ui",
    },
    {
        "source": "AbilityBindingPanel.xml",
        "screenId": "ui.screen.character_test.ability_binding",
        "displayName": "CharacterTest 能力绑定",
        "packageId": "MxCharacterTestAbilityUi",
        "componentName": "AbilityBindingPanel",
        "viewId": "ui.character_test.ability.bindings",
        "resourceStem": "ability_ui",
    },
)


IMAGE_RESOURCES = {
    "Images/panels/settings_panel_760x620.png": "ui.panel.modal.frame",
    "Images/decor/divider_240x32.png": "ui.divider.section.horizontal",
    "Images/decor/divider_480x32.png": "ui.divider.section.horizontal",
    "Images/icons/settings_amber_64.png": "ui.icon.menu.settings",
    "Images/icons/video_amber_64.png": "ui.icon.section.graphics",
    "Images/icons/audio_amber_64.png": "ui.icon.section.audio",
    "Images/icons/controls_amber_64.png": "ui.icon.section.controls",
    "Images/badges/badge_normal_128x32.png": "ui.badge.status.base",
    "Images/forms/input_normal_320x48.png": "ui.input.field.base",
}


LEGACY_COMPONENT_SIZES = {
    "Components/SettingsPanelActionButton.xml": (128, 36),
    "Components/SettingsPanelIconButton.xml": (40, 40),
    "Components/SettingsPanelKeybindButton.xml": (72, 28),
    "Components/SettingsPanelMiniButton.xml": (54, 28),
    "Components/SettingsPanelToggleButton.xml": (72, 36),
}


BIND_PATHS = {
    "SettingsPanel": {
        "title": "Title", "status": "StatusText", "dirtyMarker": "DirtyText",
        "qualityValue": "QualityText", "vSyncValue": "VSyncText",
        "renderScaleInput": "RenderScaleText", "resolutionValue": "ResolutionText",
        "windowModeValue": "WindowModeText", "languageValue": "LocaleText",
        "audioVolumeInput": "AudioVolumeText", "audioMutedValue": "AudioMutedText",
        **{f"input{i}Label": f"InputRows[{i}].Label" for i in range(9)},
        **{f"input{i}Value": f"InputRows[{i}].Value" for i in range(9)},
    },
    "NetworkPanel": {
        "title": "Title", "status": "StatusText", "endpoint": "EndpointText",
        "modeDescription": "ModeDescriptionText", "connectionHeader": "ConnectionHeaderText",
        "remoteAddressInput": "JoinOptions.RemoteAddress", "portInput": "JoinOptions.Port",
        "displayNameInput": "JoinOptions.DisplayName", "roomNameInput": "JoinOptions.RoomName",
        "connectionSummary": "ConnectionSummaryText", "roomHeader": "RoomHeaderText",
        "roomSummary": "RoomSummaryText", "memberSummary": "MemberSummaryText",
        "readinessSummary": "ReadinessSummaryText", "syncProbeSummary": "SyncProbeSummaryText",
        "replaySummary": "ReplaySummaryText", "detailsHeader": "DetailsHeaderText",
        "detailTransport": "DetailTransportText", "detailPeer": "DetailPeerText",
        "detailRtt": "DetailRttText", "detailFrames": "DetailFramesText",
        "detailSnapshot": "DetailSnapshotText", "detailDisconnect": "DetailDisconnectText",
        "message": "Message",
    },
    "AbilityBindingPanel": {
        "title": "Title", "status": "StatusText", "message": "Message",
        "selectedSlotLabel": "SelectedSlotText",
    },
}


COMMANDS = {
    "SettingsPanel": {
        "btnClose": "character_test.settings.close", "btnApply": "character_test.settings.apply",
        "btnSave": "character_test.settings.save", "btnReload": "character_test.settings.reload",
        "btnQualityPrev": "character_test.settings.quality.previous",
        "btnQualityNext": "character_test.settings.quality.next",
        "btnLanguagePrev": "character_test.settings.language.previous",
        "btnLanguageNext": "character_test.settings.language.next",
        "btnVSync": "character_test.settings.vsync.toggle",
        "btnMute": "character_test.settings.audio.mute.toggle",
        "btnSaveInput": "character_test.settings.input.save",
        "btnResetInput": "character_test.settings.input.reset",
        **{f"btnInput{i}": "character_test.settings.input.rebind" for i in range(9)},
    },
    "NetworkPanel": {
        "btnDirectLan": "character_test.network.mode.direct_lan", "btnRoom": "character_test.network.mode.room",
        "btnHost": "character_test.network.host", "btnJoin": "character_test.network.join",
        "btnDisconnect": "character_test.network.disconnect", "btnReady": "character_test.network.ready",
        "btnEnterGame": "character_test.network.enter_game",
        "btnAutoSyncProbe": "character_test.network.auto_sync_probe",
        "btnRunSyncProbe": "character_test.network.run_sync_probe", "btnClose": "character_test.network.close",
    },
    "AbilityBindingPanel": {
        **{f"btnSlot{i}": "character_test.abilities.select_slot" for i in range(1, 10)},
        **{f"btnAbility{i}": "character_test.abilities.select_ability" for i in range(15)},
        "btnClear": "character_test.abilities.clear_slot", "btnReset": "character_test.abilities.reset_defaults",
        "btnSave": "character_test.abilities.save", "btnCancel": "character_test.abilities.cancel",
    },
}


def parse_pair(value, default=(0, 0)):
    if not value:
        return default
    left, right = value.split(",", 1)
    return int(float(left)), int(float(right))


def layout(element, scale):
    x, y = parse_pair(element.get("xy"))
    default_size = LEGACY_COMPONENT_SIZES.get(element.get("fileName", ""), (0, 0))
    width, height = parse_pair(element.get("size"), default_size)
    relation = element.find("relation")
    sides = relation.get("sidePair", "") if relation is not None else ""
    horizontal = "Stretch" if "width-width" in sides else "Center" if "center-center" in sides else "Right" if "right-right" in sides else "Left"
    vertical = "Stretch" if "height-height" in sides else "Middle" if "middle-middle" in sides else "Bottom" if "bottom-bottom" in sides else "Top"
    return {
        "x": round(x * scale), "y": round(y * scale),
        "width": max(1, round(width * scale)), "height": max(1, round(height * scale)),
        "horizontalAnchor": horizontal, "verticalAnchor": vertical,
    }


def recipe_for(name):
    if name in {"btnApply", "btnSave", "btnHost", "btnJoin", "btnEnterGame"}:
        return "ui.component.button.compact.primary"
    if name in {"btnReset", "btnResetInput", "btnDisconnect", "btnClear"}:
        return "ui.component.button.compact.danger"
    return "ui.component.button.compact.neutral"


def convert_node(element, order, scale):
    name = element.get("name", f"node{order}")
    node = {
        "nodeId": name, "name": name, "type": "", "order": order,
        "bindingSlot": name, "layout": layout(element, scale),
    }
    if element.tag == "graph":
        if name == "panelFrame":
            node["type"] = "Image"
            node["image"] = {"resourceId": "ui.panel.modal.frame", "tint": "#ffffffff"}
        else:
            node["type"] = "Graph"
            corner = int(float(element.get("corner", "0").split(",", 1)[0]))
            node["graph"] = {"fillColor": element.get("fillColor", "#00000000"), "cornerRadius": round(corner * scale)}
    elif element.tag == "text":
        node["type"] = "Text"
        node["text"] = {
            "value": element.get("text", ""),
            "fontSize": max(8, round(int(element.get("fontSize", "16")) * (1.25 if scale > 1 else 1))),
            "color": element.get("color", "#ffffffff"), "align": element.get("align", "left"),
            "bold": element.get("bold") == "true", "singleLine": element.get("singleLine") == "true",
            "input": element.get("input") == "true", "restrict": element.get("restrict", ""),
            "maxLength": int(element.get("maxLength", "0")),
        }
    elif element.tag == "image":
        resource_id = IMAGE_RESOURCES.get(element.get("fileName", ""))
        if not resource_id:
            raise RuntimeError(f"No semantic resource mapping for {element.get('fileName')} ({name}).")
        node["type"] = "Image"
        node["image"] = {"resourceId": resource_id, "tint": element.get("color", "#ffffffff")}
    elif element.tag == "component":
        button = element.find("Button")
        node["type"] = "ComponentInstance"
        node["component"] = {
            "componentRecipeId": recipe_for(name),
            "title": button.get("title", "") if button is not None else "",
            "iconResourceId": "",
        }
    else:
        raise RuntimeError(f"Unsupported legacy node type: {element.tag}.")
    return node


def add_screen_icon(nodes, component_name):
    if component_name == "NetworkPanel":
        nodes.insert(3, {
            "nodeId": "networkIcon", "name": "networkIcon", "type": "Image", "order": 3,
            "bindingSlot": "networkIcon",
            "layout": {"x": 48, "y": 30, "width": 32, "height": 32, "horizontalAnchor": "Left", "verticalAnchor": "Top"},
            "image": {"resourceId": "ui.icon.menu.network", "tint": "#ffffffff"},
        })
    for index, node in enumerate(nodes):
        node["order"] = index


def build_screen(spec):
    root = ET.parse(LEGACY / spec["source"]).getroot()
    source_width, source_height = parse_pair(root.get("size"))
    if (source_width, source_height) not in {(1280, 720), (1920, 1080)}:
        raise RuntimeError(f"Unexpected source size for {spec['source']}: {source_width}x{source_height}.")
    scale = 1.5 if source_width == 1280 else 1.0
    nodes = [convert_node(element, index, scale) for index, element in enumerate(root.find("displayList"))]
    add_screen_icon(nodes, spec["componentName"])
    return {
        "schemaVersion": "mx.uiScreenBlueprint.v1", "screenId": spec["screenId"],
        "displayName": spec["displayName"], "libraryId": "MxUiFoundation",
        "designResolution": {"width": 1920, "height": 1080},
        "target": {
            "packageId": spec["packageId"], "componentName": spec["componentName"],
            "packageSourcePath": f"FGUIProject/assets/{spec['packageId']}",
        },
        "nodes": nodes,
    }


def build_contract(spec, blueprint):
    bindings = []
    binds = BIND_PATHS[spec["componentName"]]
    commands = COMMANDS[spec["componentName"]]
    for node in blueprint["nodes"]:
        bindings.append({
            "slotId": node["name"], "controlName": node["name"], "nodeType": node["type"],
            "required": True, "bindPath": binds.get(node["name"], ""),
            "commandId": commands.get(node["name"], ""),
        })
    stem = spec["resourceStem"]
    return {
        "schemaVersion": "mx.uiComposerBindingContract.v1", "screenId": spec["screenId"],
        "viewId": spec["viewId"],
        "target": {
            "packageId": spec["packageId"], "packageSourcePath": f"FGUIProject/assets/{spec['packageId']}",
            "componentName": spec["componentName"],
            "publishedPackagePath": f"Assets/Bundles/FGUI/{spec['packageId']}/{spec['packageId']}_fui.bytes",
            "publishedAtlasPath": f"Assets/Bundles/FGUI/{spec['packageId']}/{spec['packageId']}_atlas0.png",
            "packageBytesResourceId": f"ui.fairygui.charactertest.{stem}.fui",
            "atlasResourceId": f"ui.fairygui.charactertest.{stem}.atlas0",
        },
        "bindings": bindings,
    }


def resource(resource_id, path, category, name, description, size, policy="PreserveAspect", grid=None):
    usage = {"policy": policy, "designSize": {"width": size[0], "height": size[1]}, "preserveAspect": policy == "PreserveAspect"}
    if grid:
        usage["scale9Grid"] = {"x": grid[0], "y": grid[1], "width": grid[2], "height": grid[3]}
    return {
        "resourceId": resource_id, "relativePath": path, "category": category,
        "displayName": name, "description": description,
        "tags": ["player-ui", "foundation", category.lower()],
        "recommendedFairyGuiUsage": usage,
        "replacement": {
            "requireAlphaChannel": True, "requireNonOpaqueAlpha": True,
            "requireSamePixelSize": True, "allowStretch": policy in {"NineSlice", "HorizontalFill", "Stretch"},
            "minPixelWidth": size[0], "minPixelHeight": size[1], "replaceTogether": False,
        },
    }


def update_catalog():
    path = LIBRARY / "ui-art-resources.json"
    catalog = json.loads(path.read_text(encoding="utf-8"))
    additions = [
        resource("ui.input.field.base", "images/forms/input_field_base_320x48.png", "InputBackground", "通用输入框底图", "设置与联机表单使用的可拉伸输入区域。", (320, 48), "NineSlice", (20, 12, 280, 24)),
        resource("ui.panel.section.frame", "images/panels/panel_section_frame_512x256.png", "PanelFrame", "章节面板框", "设置分组、房间状态和详情区域使用的轻量章节框。", (512, 256), "NineSlice", (32, 24, 448, 208)),
        resource("ui.badge.status.base", "images/badges/status_badge_base_192x40.png", "Badge", "状态徽标底图", "保存状态、连接状态和选择状态使用的九宫格徽标。", (192, 40), "NineSlice", (24, 10, 144, 20)),
        resource("ui.icon.section.graphics", "images/icons/menu_graphics_32x32.png", "Icon", "画面设置图标", "设置界面画面章节图标。", (32, 32)),
        resource("ui.icon.section.audio", "images/icons/menu_audio_32x32.png", "Icon", "音频设置图标", "设置界面音频章节图标。", (32, 32)),
        resource("ui.icon.section.controls", "images/icons/menu_controls_32x32.png", "Icon", "操作设置图标", "设置与能力绑定界面的操作章节图标。", (32, 32)),
    ]
    existing = {item["resourceId"] for item in catalog["resources"]}
    catalog["resources"].extend(item for item in additions if item["resourceId"] not in existing)
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def update_recipes():
    path = LIBRARY / "ui-component-recipes.json"
    catalog = json.loads(path.read_text(encoding="utf-8"))
    colors = {
        "neutral": ("#183b8179", "#4d0b2221", "#e8efed"),
        "primary": ("#1f51c7b8", "#4d0d4d49", "#ecfffb"),
        "danger": ("#246f3034", "#5c43191d", "#ffd9d4"),
    }
    existing = {item["componentId"] for item in catalog["recipes"]}
    for role, (over, down, title) in colors.items():
        component_id = f"ui.component.button.compact.{role}"
        if component_id in existing:
            continue
        catalog["recipes"].append({
            "componentId": component_id, "displayName": {"neutral": "紧凑中性按钮", "primary": "紧凑主要按钮", "danger": "紧凑危险按钮"}[role],
            "componentType": "Button", "fairyGuiComponentName": f"Compact{role.title()}Button",
            "defaultSize": {"width": 220, "height": 48},
            "resourceSlots": [{"slotId": "base", "resourceId": "ui.button.command.base"}],
            "instanceProperties": ["title"],
            "statePresentation": {
                "controllerName": "button", "pages": ["up", "down", "over", "selectedOver"],
                "overOverlayColor": over, "downOverlayColor": down,
                "titleColor": title, "titleFontSize": 16, "iconColor": "#ffffffff",
            },
        })
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    update_catalog()
    update_recipes()
    for spec in SCREENS:
        blueprint = build_screen(spec)
        screen_path = LIBRARY / "screens" / (spec["screenId"].removeprefix("ui.screen.").replace(".", "_") + ".ui-screen.json")
        contract_path = LIBRARY / "contracts" / (spec["screenId"].removeprefix("ui.screen.").replace(".", "_") + ".ui-composer-binding-contract.json")
        screen_path.write_text(json.dumps(blueprint, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        contract_path.write_text(json.dumps(build_contract(spec, blueprint), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Generated {spec['screenId']}: {len(blueprint['nodes'])} nodes")


if __name__ == "__main__":
    main()
