"""Render the approved menu-button vector geometry as reproducible RGBA slices.

Only this button's new assets are written; the existing Foundation generator and
published FairyGUI packages are not invoked. Requires Pillow, like the existing
generate_mx_ui_foundation_assets.py workflow.
"""

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "ArtSource/UIKits/MxUiFoundation"
OUTPUT = LIBRARY / "images/buttons/standard_menu"
SOURCE = LIBRARY / "source/standard_menu_button"
WIDTH, HEIGHT, SCALE = 320, 72, 8
GRID = {"x": 20, "y": 14, "width": 256, "height": 32}
STATES = {
    "normal": {
        "label": "未选中", "page": "up", "fill": "#49504E",
        "border": "#737C75", "groove": "#344039", "light": "#7D8980",
        "content": "#E1DFD3", "rail": None,
    },
    "hover": {
        "label": "悬浮", "page": "over", "fill": "#BDB9AC",
        "border": "#D7D5C9", "groove": "#67726A", "light": "#D9DACD",
        "content": "#222C2A", "rail": "#DC796B",
    },
    "pressed": {
        "label": "按下", "page": "down", "fill": "#A5A597",
        "border": "#848F84", "groove": "#4A5950", "light": "#BBC0B0",
        "content": "#222C2A", "rail": "#B86158",
    },
}


def coordinates(values):
    return tuple(round(value * SCALE) for value in values)


def bezier(points):
    for step in range(97):
        t = step / 96
        u = 1 - t
        yield tuple(
            (u ** 3 * points[0][axis]
             + 3 * u * u * t * points[1][axis]
             + 3 * u * t * t * points[2][axis]
             + t ** 3 * points[3][axis]) * SCALE
            for axis in (0, 1)
        )


def draw_button(state, width=WIDTH):
    """Generate from geometric primitives; no screenshot cropping or repainting."""
    palette = STATES[state]
    image = Image.new("RGBA", (width * SCALE, HEIGHT * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle(
        coordinates((9, 2, width - 2, HEIGHT - 2)), radius=6 * SCALE,
        fill=palette["fill"], outline=palette["border"], width=SCALE,
    )
    if palette["rail"]:
        draw.rounded_rectangle(
            coordinates((1, 9, 4, HEIGHT - 9)), radius=1.5 * SCALE,
            fill=palette["rail"],
        )
    if state == "pressed":
        draw.line(
            [coordinates((16, 4)), coordinates((width - 9, 4))],
            fill="#7E897E", width=SCALE,
        )
    points = [(width - 32, 60), (width - 21, 60), (width - 12, 54), (width - 9, 49)]
    draw.line(list(bezier([(x, y + 0.8) for x, y in points])),
              fill=palette["light"], width=SCALE)
    draw.line(list(bezier(points)), fill=palette["groove"], width=SCALE)
    return image.resize((width, HEIGHT), Image.Resampling.LANCZOS)


def svg_source(state):
    p = STATES[state]
    rail = (f'<rect x="1" y="9" width="3" height="54" rx="1.5" fill="{p["rail"]}"/>'
            if p["rail"] else "")
    inset = ('<path d="M16 4 H311" stroke="#7E897E" fill="none"/>'
             if state == "pressed" else "")
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="320" height="72" viewBox="0 0 320 72">
  <title>Standard menu button: {state}</title>
  <rect x="9" y="2" width="309" height="68" rx="6" fill="{p['fill']}" stroke="{p['border']}"/>
  {rail}
  {inset}
  <path d="M288 60.8 C299 60.8 308 54.8 311 49.8" fill="none" stroke="{p['light']}"/>
  <path d="M288 60 C299 60 308 54 311 49" fill="none" stroke="{p['groove']}"/>
</svg>
'''


def stretch_nine_slice(source, target_size):
    """Render the actual declared scale9Grid for the review board."""
    target = Image.new("RGBA", target_size)
    xs = (0, GRID["x"], GRID["x"] + GRID["width"], WIDTH)
    ys = (0, GRID["y"], GRID["y"] + GRID["height"], HEIGHT)
    tx = (0, xs[1], target_size[0] - (WIDTH - xs[2]), target_size[0])
    ty = (0, ys[1], target_size[1] - (HEIGHT - ys[2]), target_size[1])
    for row in range(3):
        for col in range(3):
            patch = source.crop((xs[col], ys[row], xs[col + 1], ys[row + 1]))
            size = (tx[col + 1] - tx[col], ty[row + 1] - ty[row])
            if patch.size != size:
                patch = patch.resize(size, Image.Resampling.LANCZOS)
            target.paste(patch, (tx[col], ty[row]))
    return target


def preview_font(size):
    for filename in ("C:/Windows/Fonts/msyh.ttc", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if Path(filename).exists():
            return ImageFont.truetype(filename, size)
    return ImageFont.load_default()


def make_preview(images):
    board = Image.new("RGB", (1040, 430), "#20292C")
    draw = ImageDraw.Draw(board)
    font = preview_font(18)
    draw.text((30, 18), "标准菜单按钮 · 三态底图", font=preview_font(24), fill="#E1DFD3")
    draw.text((152, 62), "320 × 72", font=font, fill="#ACB6B1")
    draw.text((512, 62), "480 × 72 · 九宫格拉伸预览", font=font, fill="#ACB6B1")
    for index, (state, image) in enumerate(images.items()):
        y = 102 + index * 96
        draw.text((30, y + 23), STATES[state]["label"], font=font, fill="#E1DFD3")
        board.paste(image, (152, y), image)
        wide = stretch_nine_slice(image, (480, 72))
        board.paste(wide, (512, y), wide)
    draw.text((152, 397), "底图不含文字与图标；图标槽与标题由共享组件绑定。", font=font, fill="#ACB6B1")
    board.save(SOURCE / "states-preview.png", optimize=True)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    images, entries = {}, []
    for state, palette in STATES.items():
        image = draw_button(state)
        path = OUTPUT / f"button_menu_{state}_320x72.png"
        image.save(path, optimize=True)
        (SOURCE / f"button_menu_{state}.svg").write_text(svg_source(state), encoding="utf-8")
        images[state] = image
        entries.append({
            "state": state, "displayName": palette["label"],
            "resourceId": f"ui.button.menu.standard.{state}",
            "relativePath": path.relative_to(LIBRARY).as_posix(),
            "fairyGuiPage": palette["page"], "contentColor": palette["content"],
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        })
    manifest = {
        "name": "StandardMenuButton", "version": 1,
        "status": "Approved art source; FairyGUI integration pending",
        "designSize": {"width": WIDTH, "height": HEIGHT}, "scale9Grid": GRID,
        "stateGroup": "ui.button.menu.standard", "states": entries,
        "controllerPages": {"up": "normal", "over": "hover", "down": "pressed", "selectedOver": "hover"},
        "icon": {"x": 24, "y": 20, "width": 32, "height": 32},
        "title": {"x": 80, "y": 0, "rightInset": 48, "height": 72, "fontSize": 22},
        "backgroundContainsIconOrText": False,
        "approvedReference": "exec-ee891eea-56a0-4f44-823d-82a040a13fb4.png",
    }
    (SOURCE / "standard-menu-button.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8",
    )
    make_preview(images)
    print(json.dumps({"generated": [entry["relativePath"] for entry in entries]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
