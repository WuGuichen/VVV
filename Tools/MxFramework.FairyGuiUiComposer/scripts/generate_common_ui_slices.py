"""Minimal UI art: seven slices, existing assets, and native Graph compositions.

This generates art/design sources only; it does not implement or publish UI.
"""
import hashlib
import json
import zipfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "ArtSource/UIKits/MxUiFoundation"
OUTPUT = LIBRARY / "images/standard_common"
SOURCE = LIBRARY / "source/standard_common"
SCALE = 8
INK, FACE, DARK = "#222C2A", "#BDB9AC", "#202B2C"
ENTRIES = []


class Vector:
    def __init__(self, width, height):
        self.size = (width, height)
        self.image = Image.new("RGBA", (width*SCALE, height*SCALE))
        self.draw = ImageDraw.Draw(self.image)
        self.elements = []

    def scaled(self, p):
        return tuple(round(v*SCALE) for v in p)

    def line(self, points, color, weight=1):
        self.draw.line([self.scaled(p) for p in points], fill=color, width=max(1, round(weight*SCALE)), joint="curve")
        vertices = " ".join(f"{x},{y}" for x, y in points)
        self.elements.append(f'<polyline points="{vertices}" fill="none" stroke="{color}" stroke-width="{weight}"/>')

    def polygon(self, points, color):
        self.draw.polygon([self.scaled(p) for p in points], fill=color)
        vertices = " ".join(f"{x},{y}" for x, y in points)
        self.elements.append(f'<polygon points="{vertices}" fill="{color}"/>')

    def curve(self, points, color):
        samples = []
        for step in range(65):
            t = step/64
            u = 1-t
            samples.append(tuple(u**3*points[0][a] + 3*u*u*t*points[1][a] + 3*u*t*t*points[2][a] + t**3*points[3][a] for a in (0, 1)))
        self.draw.line([self.scaled(p) for p in samples], fill=color, width=SCALE)
        p0, p1, p2, p3 = points
        self.elements.append(f'<path d="M{p0[0]} {p0[1]} C{p1[0]} {p1[1]} {p2[0]} {p2[1]} {p3[0]} {p3[1]}" fill="none" stroke="{color}"/>')

    def png(self):
        return self.image.resize(self.size, Image.Resampling.LANCZOS)

    def svg(self):
        w, h = self.size
        return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">\n' + "\n".join(self.elements) + '\n</svg>\n'


def add(key, label, v, category, group, policy="PreserveAspect", note="", tint=None):
    name = key.replace(".", "_") + f"_{v.size[0]}x{v.size[1]}"
    path = OUTPUT / (name+".png")
    v.png().save(path, optimize=True)
    (SOURCE/"vectors"/(name+".svg")).write_text(v.svg(), encoding="utf-8")
    ENTRIES.append({"resourceId": "ui.standard."+key, "relativePath": path.relative_to(LIBRARY).as_posix(),
                    "displayName": label, "category": category, "group": group, "note": note,
                    "usage": {"policy": policy, "designSize": {"width": v.size[0], "height": v.size[1]}, "scale9Grid": None,
                              "preserveAspect": policy == "PreserveAspect"},
                    "recommendedTint": tint, "replaceTogether": group == "engraving", "requiresTransparentPixel": True,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})


def engravings():
    for kind, label in (("action", "行为"), ("utility", "辅助")):
        v = Vector(252, 352)
        for offset in (0, 5):
            if kind == "action":
                for points in [[(226-offset, 60), (240, 74+offset)], [(12, 320-offset), (31+offset, 339)]]:
                    v.line([(x+0.6, y+0.6) for x, y in points], "#D9DACD")
                    v.line(points, "#8A9285")
            else:
                for points in [[(224-offset, 60), (224-offset, 70), (234, 76+offset), (241, 76+offset)],
                               [(12, 318-offset), (24+offset, 318-offset), (31+offset, 329), (31+offset, 339)]]:
                    v.curve([(x+0.6, y+0.6) for x, y in points], "#D9DACD")
                    v.curve(points, "#8A9285")
        add("card.engraving."+kind, label+"类型浅刻纹叠层", v, "Decoration", "engraving",
            note="仅透明刻纹；同一原生卡底组合使用。与252×352卡片同原点同比例缩放，不按品质换纹。")


def masks_and_icons():
    v = Vector(8, 8)
    for x in range(-8, 17, 4):
        v.line([(x, 8), (x+8, 0)], "#FFFFFF")
    add("bar.reserved.tile", "动势预留斜纹遮罩", v, "Decoration", "pattern", "Tile", tint="#626B64",
        note="仅在预留区裁切平铺；普通填充用Graph，不另出图。")
    v = Vector(32, 32)
    v.polygon([(16, 2), (30, 16), (16, 30), (2, 16)], "#FFFFFF")
    add("card.type.action", "菱形类型着色遮罩", v, "Icon", "shape",
        note="行为类型；品质只改变tint。圆形用原生Graph圆角，不另出图。")
    v = Vector(16, 12)
    v.polygon([(1, 1), (15, 1), (8, 11)], "#FFFFFF")
    add("target.marker", "当前目标三角标记", v, "Icon", "shape", tint="#B87462")
    v = Vector(32, 32)
    v.line([(8, 16), (14, 22), (25, 9)], "#FFFFFF", 2.5)
    add("icon.confirm", "确认／勾选单色图标", v, "Icon", "icon", note="复用于应用、确认和勾选；底槽和文字独立。")
    v = Vector(32, 32)
    v.line([(14, 5), (27, 5), (27, 27), (14, 27)], "#FFFFFF", 2.5)
    v.line([(4, 16), (21, 16)], "#FFFFFF", 2.5)
    v.line([(15, 10), (21, 16), (15, 22)], "#FFFFFF", 2.5)
    add("icon.exit", "退出应用单色图标", v, "Icon", "icon", note="命令语义不同于关闭菜单。")


REUSE_IDS = ["ui.button.menu.standard.normal", "ui.button.menu.standard.hover", "ui.button.menu.standard.pressed",
             "ui.icon.menu.resume", "ui.icon.menu.settings", "ui.icon.menu.close", "ui.icon.section.graphics",
             "ui.icon.section.audio", "ui.icon.section.controls", "ui.icon.minimal.previous", "ui.icon.minimal.next",
             "ui.icon.minimal.continue", "ui.icon.minimal.cost", "ui.icon.runtime_ai.pause"]


def replacements():
    rows = []
    def use(keys, kind, detail):
        rows.extend({"oldResourceId": "ui.standard."+k, "replacementKind": kind, "replacement": detail,
                     "runtimeMigrated": False} for k in keys)
    use(["panel.light", "panel.dark", "panel.section", "panel.tooltip"], "Graph composition",
        "共用圆角背景，填充色/尺寸/标题条为参数；边线用外层Graph加内层Graph。")
    use(["button.compact."+s for s in ("normal", "hover", "pressed")], "Existing image",
        "ui.button.menu.standard.*三态九宫格复用到192×48；图标文字槽随规格设置。")
    use(["button.icon."+s for s in ("normal", "hover", "pressed")], "Graph + existing icon",
        "原生圆角底槽切状态颜色，图标独立；48×48不强塞菜单图64px固定边距。")
    use(["tab."+s for s in ("normal", "hover", "selected")], "Graph composition",
        "共用底槽切填充/内容色；当前页标记为细Graph，图标和文字独立。")
    use(["input."+s for s in ("normal", "focused", "disabled")], "Graph composition",
        "共用底槽，聚焦切边色或加细线，禁用切颜色，不另出底图。")
    use(["checkbox."+s for s in ("unchecked", "hover", "checked", "disabled")], "Graph + image",
        "原生方槽＋ui.standard.icon.confirm；checked控制图标显隐，其他状态改变颜色。")
    use(["slider.track", "slider.fill", "slider.thumb.normal", "slider.thumb.hover", "slider.thumb.pressed",
         "scrollbar.track", "scrollbar.thumb.normal", "scrollbar.thumb.hover"], "Graph composition",
        "轨道/填充/滑块共用矩形或圆角矩形，长度/方向/颜色/状态为参数。")
    use(["divider", "bar.track.regular", "bar.fill.regular", "bar.track.thin", "bar.fill.thin",
         "health.tick.minor", "health.tick.major"], "Graph composition",
        "线/底槽/填充/刻度使用原生矩形；生命刻度对应固定血量单位，聚势不加刻度。")
    use(["card.action", "card.utility"], "Graph card + engraving image",
        "同一63:88原生卡底＋深标题，叠加ui.standard.card.engraving.action/utility，不复制整卡。")
    use(["card.focus"], "Graph composition", "背景后方中性外层描边或短焦点线，不修改品质颜色。")
    use(["card.cost", "buff.slot"], "Graph composition", "共用圆角底槽，尺寸/圆角参数化；文字图标独立。")
    use(["card.type.utility"], "Graph composition", "正方形Graph的cornerRadius=边长/2形成圆形，颜色表示品质。")
    return rows


def font(size):
    for p in ("C:/Windows/Fonts/msyh.ttc", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if Path(p).exists():
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def preview():
    board = Image.new("RGBA", (1120, 670), "#252E30")
    d = ImageDraw.Draw(board)
    d.text((24, 20), "精简版 · 7 张必要切片，其余由基础图形与已有资源组合", font=font(23), fill="#E1DFD3")
    by_id = {r["resourceId"]: r for r in ENTRIES}
    def asset(key):
        return Image.open(LIBRARY/by_id["ui.standard."+key]["relativePath"]).convert("RGBA")
    for i, kind in enumerate(("action", "utility")):
        x, y = 24+i*276, 80
        d.rounded_rectangle((x, y, x+252, y+352), radius=6, fill=FACE, outline="#838E83")
        d.rounded_rectangle((x+1, y+1, x+251, y+49), radius=5, fill=DARK)
        d.rectangle((x+1, y+26, x+251, y+49), fill=DARK)
        d.rounded_rectangle((x+10, y+8, x+58, y+40), radius=14, fill=DARK, outline="#7D8980")
        d.text((x+20, y+10), "10" if i == 0 else "0", font=font(21), fill="#E1DFD3")
        d.text((x+78, y+8), "猛击" if i == 0 else "调息", font=font(24), fill="#E1DFD3")
        d.rectangle((x+12, y+60, x+240, y+128), outline="#8A9285")
        d.text((x+72, y+82), "独立图标槽", font=font(16), fill=INK)
        if kind == "action":
            mark = asset("card.type.action")
            colored = Image.new("RGBA", mark.size, "#527B9D")
            colored.putalpha(mark.getchannel("A"))
            board.alpha_composite(colored.resize((24, 24), Image.Resampling.LANCZOS), (x+114, y+139))
        else:
            d.rounded_rectangle((x+114, y+139, x+138, y+163), radius=12, fill="#A17C37")
        board.alpha_composite(asset("card.engraving."+kind), (x, y))
        lines = ["接近目标，", "调整至最佳距离，", "尝试发动", "装配动作。"] if i == 0 else ["调整呼吸，", "恢复动势。"]
        for j, value in enumerate(lines):
            d.text((x+16, y+181+j*29), value, font=font(20), fill=INK)
    d.text((600, 80), "需要图片的细节", font=font(20), fill="#E1DFD3")
    for i, key in enumerate(("card.type.action", "target.marker", "bar.reserved.tile", "icon.confirm", "icon.exit")):
        y = 126+i*48
        board.alpha_composite(asset(key), (608, y))
        d.text((660, y+3), by_id["ui.standard."+key]["displayName"], font=font(16), fill="#BAC2BC")
    d.text((600, 393), "原生图形：血量、聚势、Buff 槽", font=font(17), fill="#E1DFD3")
    d.rectangle((610, 434, 850, 446), fill=DARK)
    d.rectangle((610, 434, 790, 446), fill="#739A82")
    for x in range(634, 850, 24):
        d.rectangle((x, 434, x+1, 446), fill=INK)
    d.rectangle((610, 452, 850, 455), fill=DARK)
    d.rectangle((610, 452, 752, 455), fill="#819CA9")
    for x in (610, 633):
        d.rounded_rectangle((x, 463, x+18, 481), radius=3, fill=DARK, outline="#59675E")
    d.text((24, 476), "同一原生卡底，只更换两种透明刻纹。", font=font(19), fill="#E1DFD3")
    d.text((24, 519), "面板、输入框、线条、条形与小底槽不再各出一张图。", font=font(18), fill="#BAC2BC")
    d.text((24, 557), "菜单三态与 11 个现有图标继续复用；这是组合计划，非运行时截图。", font=font(18), fill="#BAC2BC")
    d.text((24, 601), "槽位边线和文字用于排布检查，不包含在切图内。", font=font(16), fill="#BAC2BC")
    board.convert("RGB").save(SOURCE/"preview-minimal.png", optimize=True)


def package(manifest):
    target = LIBRARY/"exports/standard-common-ui-v2.zip"
    target.parent.mkdir(parents=True, exist_ok=True)
    paths = {LIBRARY/r["relativePath"] for r in manifest["resources"]+manifest["reusedResources"]}
    paths.update(SOURCE/name for name in ("README.md", "manifest.json", "replacements.json", "preview-minimal.png"))
    paths.update(SOURCE/"vectors"/(Path(r["relativePath"]).stem+".svg") for r in manifest["resources"])
    paths.update(p for p in (LIBRARY/"source/standard_menu_button").rglob("*") if p.is_file())
    with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for p in sorted(paths):
            info = zipfile.ZipInfo(p.relative_to(LIBRARY).as_posix(), date_time=(2026, 9, 26, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(info, p.read_bytes())


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    (SOURCE/"vectors").mkdir(parents=True, exist_ok=True)
    engravings()
    masks_and_icons()
    catalog = json.loads((LIBRARY/"ui-art-resources.json").read_text(encoding="utf-8-sig"))
    by_id = {r["resourceId"]: r for r in catalog["resources"]}
    reused = [{"resourceId": rid, "relativePath": by_id[rid]["relativePath"], "usage": by_id[rid]["recommendedFairyGuiUsage"],
               "sha256": hashlib.sha256((LIBRARY/by_id[rid]["relativePath"]).read_bytes()).hexdigest()} for rid in REUSE_IDS]
    manifest = {"name": "StandardCommonUiSlices", "version": 2,
                "status": "Minimal art source and Graph composition plan; runtime integration pending",
                "scope": ["start-menu", "settings", "tactical-combat"], "resources": ENTRIES, "reusedResources": reused,
                "nativePrimitives": {"supportedComposerNode": "Graph", "properties": ["fillColor", "cornerRadius"],
                                     "borders": "outer Graph plus inset Graph", "circle": "square Graph with radius=width/2",
                                     "dynamicBars": "Graph dimensions/color from actual data; ticks are pooled Graphs"},
                "cardLayout": {"size": [252, 352], "titleHeight": 48, "iconRect": [12, 60, 228, 68],
                               "typeQualityMarker": [114, 139, 24, 24], "descriptionRect": [16, 181, 220, 136],
                               "descriptionMaxLines": 4, "hasFooterStatus": False},
                "headpadLayout": {"healthTrack": [0, 0, 128, 12], "healthFill": [1, 2, 126, 8],
                                  "cohesionTrack": [0, 15, 128, 4], "cohesionFill": [1, 16, 126, 2],
                                  "buffOrigin": [0, 23], "buffDisplaySize": [16, 16], "buffGap": 3,
                                  "textVisible": False, "cohesionUsesTicks": False},
                "palette": {"dark": DARK, "face": FACE, "ink": INK, "quality": ["#858782", "#527B9D", "#A17C37"],
                            "allyHealth": "#739A82", "enemyHealth": "#B87462", "cohesion": "#819CA9"}}
    (SOURCE/"manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    (SOURCE/"replacements.json").write_text(json.dumps({"version": 2, "kind": "Design map, not runtime aliases",
                                                     "replacements": replacements()}, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    preview()
    package(manifest)
    print(json.dumps({"newBatchPngs": len(ENTRIES), "reused": len(reused), "replacedOldIds": len(replacements())}))


if __name__ == "__main__":
    main()
