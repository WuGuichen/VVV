"""Register selected ImageGen PNGs without changing their pixels.

Opaque-content bounds only guide Composer image placement. FairyGUI helper owns
the package copy, atlas and published bytes.
"""
import hashlib
import json
from pathlib import Path

from PIL import Image
from configure_tactical_card_ui_preview import upsert_array

ROOT = Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "ArtSource/UIKits/MxUiFoundation"
DIRECTORY = "images/card_foundation_ai_v1"
SOURCE = LIBRARY / "source/card_foundation_ai_v1"
ASSETS = [
    ("behavior_card_base.png", "ui.card.imagegen.behavior.base", "行为卡实底", "PanelFrame", False, "exec-fe815321-aa92-41fa-8631-579ac0cafe04"),
    ("sword_attack_illustration.png", "ui.card.imagegen.sword.illustration", "剑击动作插图", "Icon", True, "exec-d08cf1f7-232f-4dd4-9575-08d4a35bde02"),
    ("tooltip_plate.png", "ui.card.imagegen.tooltip.base", "轻量详情窗底板", "PanelFrame", True, "exec-e797381c-19a5-40be-b5aa-3006146b68f2"),
    ("action_token_plate.png", "ui.card.imagegen.action_token.base", "动作选择文字块底板", "ButtonBase", True, "exec-9b5f7df1-3341-412b-91b3-79b39debdaa7"),
]


def main():
    entries, resources = [], []
    for filename, resource_id, title, category, transparent, generation in ASSETS:
        relative = f"{DIRECTORY}/{filename}"
        path = LIBRARY / relative
        with Image.open(path) as raw:
            width, height = raw.size
            if max(width, height) > 2048:
                raise ValueError(f"Generated asset exceeds the current FairyGUI atlas limit: {filename}")
            alpha = raw.convert("RGBA").getchannel("A")
            bounds = alpha.point(lambda value: 255 if value >= 200 else 0).getbbox()
            extrema = alpha.getextrema()
            if bounds is None:
                raise ValueError(f"Empty generated asset: {filename}")
            if not transparent and extrema != (255, 255):
                raise ValueError("The card skin must have a completely opaque interior.")
            has_alpha = "A" in raw.getbands()
        left, top, right, bottom = bounds
        entries.append({"resourceId": resource_id, "relativePath": relative, "generationId": generation,
                        "pixelSize": {"width": width, "height": height},
                        "contentRect": {"x": left, "y": top, "width": right-left, "height": bottom-top},
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        resources.append({"resourceId": resource_id, "relativePath": relative, "category": category,
                          "displayName": title, "description": "依据半即时动作卡牌 UI 基石，由内置图片生成功能生成；文字与数据独立绑定。",
                          "tags": ["card", "imagegen", "ui-foundation", "v1"],
                          "recommendedFairyGuiUsage": {"policy": "Stretch", "designSize": {"width": width, "height": height},
                                                       "scale9Grid": None, "preserveAspect": False},
                          "replacement": {"requireAlphaChannel": has_alpha and transparent, "requireNonOpaqueAlpha": transparent,
                                          "requireSamePixelSize": True, "allowStretch": True, "minPixelWidth": width,
                                          "minPixelHeight": height, "replaceTogether": False}})
    SOURCE.mkdir(parents=True, exist_ok=True)
    (SOURCE / "manifest.json").write_text(json.dumps({
        "schema": "mx.card-ui-imagegen-skins.v1", "generator": "built-in image_gen",
        "reference": "StorySource/玩法基石/UI目标效果/战斗HUD_当前目标.png",
        "pixelProcessing": "None. Files copied verbatim; alpha bounds are read-only placement metadata.",
        "assets": entries}, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    upsert_array(LIBRARY / "ui-art-resources.json", "resources", "resourceId", resources)
    print(f"Registered {len(resources)} original generated PNG assets.")


if __name__ == "__main__":
    main()
