from pathlib import Path

from PIL import Image, ImageDraw


SCALE = 4
ROOT = Path(__file__).resolve().parents[3] / "ArtSource" / "UIKits" / "MxUiFoundation" / "images"


def canvas(width: int, height: int):
    image = Image.new("RGBA", (width * SCALE, height * SCALE), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image)


def save(image: Image.Image, relative_path: str, size: tuple[int, int]):
    target = ROOT / relative_path
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize(size, Image.Resampling.LANCZOS).save(target, "PNG", optimize=True)


def rect(draw, box, fill=None, outline=None, width=1, radius=0):
    scaled = tuple(value * SCALE for value in box)
    if radius:
        draw.rounded_rectangle(scaled, radius=radius * SCALE, fill=fill, outline=outline, width=width * SCALE)
    else:
        draw.rectangle(scaled, fill=fill, outline=outline, width=width * SCALE)


def line(draw, points, fill, width=2, joint="curve"):
    draw.line([(x * SCALE, y * SCALE) for x, y in points], fill=fill, width=width * SCALE, joint=joint)


def polygon(draw, points, fill=None, outline=None):
    draw.polygon([(x * SCALE, y * SCALE) for x, y in points], fill=fill, outline=outline)


def panel_frame():
    image, draw = canvas(512, 512)
    rect(draw, (0, 0, 511, 511), fill=(12, 18, 23, 244), outline=(63, 79, 88, 255), width=2, radius=18)
    rect(draw, (8, 8, 503, 503), outline=(21, 175, 159, 176), width=2, radius=13)
    rect(draw, (19, 19, 492, 492), fill=(20, 28, 34, 236), outline=(44, 57, 64, 255), width=1, radius=9)
    for offset in (0, 470):
        polygon(draw, ((21 + offset, 9), (42 + offset, 9), (53 + offset, 20), (42 + offset, 31), (21 + offset, 31), (10 + offset, 20)), fill=(20, 94, 91, 255), outline=(116, 219, 202, 255))
    line(draw, ((54, 20), (458, 20)), (73, 195, 180, 125), 1)
    line(draw, ((54, 492), (458, 492)), (207, 157, 66, 120), 1)
    polygon(draw, ((10, 451), (22, 463), (22, 493), (10, 503)), fill=(109, 79, 35, 220), outline=(220, 170, 78, 220))
    polygon(draw, ((502, 451), (490, 463), (490, 493), (502, 503)), fill=(109, 79, 35, 220), outline=(220, 170, 78, 220))
    save(image, "panels/panel_modal_frame_512x512.png", (512, 512))


def command_button():
    image, draw = canvas(320, 72)
    rect(draw, (0, 0, 319, 71), fill=(20, 29, 35, 248), outline=(55, 70, 79, 255), width=2, radius=7)
    rect(draw, (5, 5, 314, 66), fill=(24, 36, 42, 245), outline=(35, 151, 143, 190), width=1, radius=5)
    polygon(draw, ((0, 12), (12, 0), (36, 0), (27, 9), (14, 9), (9, 14), (9, 58), (14, 63), (27, 63), (36, 72), (12, 72), (0, 60)), fill=(18, 93, 89, 235), outline=(82, 210, 192, 230))
    polygon(draw, ((320, 12), (308, 0), (290, 0), (299, 9), (306, 9), (311, 14), (311, 58), (306, 63), (299, 63), (290, 72), (308, 72), (320, 60)), fill=(91, 65, 29, 225), outline=(221, 168, 70, 225))
    line(draw, ((46, 12), (280, 12)), (87, 211, 196, 110), 1)
    line(draw, ((46, 60), (280, 60)), (207, 157, 66, 90), 1)
    save(image, "buttons/button_command_base_320x72.png", (320, 72))


def divider():
    image, draw = canvas(512, 16)
    line(draw, ((8, 8), (504, 8)), (44, 65, 70, 210), 1)
    line(draw, ((60, 7), (452, 7)), (62, 190, 176, 170), 1)
    polygon(draw, ((0, 8), (8, 1), (16, 8), (8, 15)), fill=(22, 112, 105, 255), outline=(101, 220, 203, 255))
    polygon(draw, ((496, 8), (504, 1), (512, 8), (504, 15)), fill=(104, 74, 31, 255), outline=(222, 168, 70, 255))
    save(image, "dividers/divider_section_horizontal_512x16.png", (512, 16))


def accent_diamond():
    image, draw = canvas(24, 24)
    polygon(draw, ((12, 0), (24, 12), (12, 24), (0, 12)), fill=(22, 98, 94, 255), outline=(122, 226, 208, 255))
    polygon(draw, ((12, 5), (19, 12), (12, 19), (5, 12)), fill=(201, 148, 59, 255), outline=(255, 207, 107, 255))
    save(image, "decor/accent_diamond_24x24.png", (24, 24))


def menu_icon(name: str, painter):
    image, draw = canvas(32, 32)
    painter(draw)
    save(image, f"icons/menu_{name}_32x32.png", (32, 32))


ICON = (151, 229, 217, 255)
ICON_DIM = (63, 139, 134, 220)


def icons():
    def resume(draw):
        polygon(draw, ((9, 5), (27, 16), (9, 27)), fill=ICON, outline=(220, 255, 249, 255))
        line(draw, ((5, 5), (5, 27)), ICON_DIM, 2)

    def network(draw):
        for x, y in ((16, 6), (7, 24), (25, 24)):
            rect(draw, (x - 3, y - 3, x + 3, y + 3), fill=(24, 43, 47, 255), outline=ICON, width=2, radius=3)
        line(draw, ((14, 9), (9, 21)), ICON, 2)
        line(draw, ((18, 9), (23, 21)), ICON, 2)
        line(draw, ((10, 24), (22, 24)), ICON, 2)

    def abilities(draw):
        polygon(draw, ((17, 2), (20, 12), (29, 16), (20, 19), (16, 30), (13, 20), (3, 16), (12, 12)), fill=(28, 81, 80, 255), outline=ICON)
        polygon(draw, ((16, 8), (19, 16), (16, 24), (13, 16)), fill=(225, 174, 77, 255), outline=(255, 218, 139, 255))

    def settings(draw):
        draw.ellipse((7 * SCALE, 7 * SCALE, 25 * SCALE, 25 * SCALE), fill=(24, 43, 47, 255), outline=ICON, width=3 * SCALE)
        draw.ellipse((13 * SCALE, 13 * SCALE, 19 * SCALE, 19 * SCALE), fill=ICON)
        for points in (((14, 2), (18, 2), (18, 8), (14, 8)), ((14, 24), (18, 24), (18, 30), (14, 30)), ((2, 14), (8, 14), (8, 18), (2, 18)), ((24, 14), (30, 14), (30, 18), (24, 18))):
            polygon(draw, points, fill=ICON)

    def reset(draw):
        line(draw, ((8, 8), (4, 14), (11, 14)), ICON, 3)
        draw.arc((5 * SCALE, 5 * SCALE, 27 * SCALE, 27 * SCALE), start=205, end=520, fill=ICON, width=3 * SCALE)
        line(draw, ((20, 18), (20, 10)), (225, 174, 77, 255), 2)
        line(draw, ((20, 18), (14, 18)), (225, 174, 77, 255), 2)

    def close(draw):
        line(draw, ((6, 6), (26, 26)), ICON, 4)
        line(draw, ((26, 6), (6, 26)), ICON, 4)
        draw.ellipse((3 * SCALE, 3 * SCALE, 29 * SCALE, 29 * SCALE), outline=ICON_DIM, width=SCALE)

    for name, painter in (("resume", resume), ("network", network), ("abilities", abilities), ("settings", settings), ("reset", reset), ("close", close)):
        menu_icon(name, painter)


def hud_plate(name: str, mirrored: bool):
    image, draw = canvas(512, 160)
    if mirrored:
        outer = ((0, 20), (20, 0), (496, 0), (512, 16), (512, 144), (496, 160), (20, 160), (0, 140))
        inner = ((8, 24), (26, 8), (491, 8), (504, 21), (504, 139), (491, 152), (26, 152), (8, 136))
        accent = ((480, 14), (498, 30), (498, 130), (480, 146), (470, 136), (470, 24))
    else:
        outer = ((0, 16), (16, 0), (492, 0), (512, 20), (512, 140), (492, 160), (16, 160), (0, 144))
        inner = ((8, 21), (21, 8), (486, 8), (504, 24), (504, 136), (486, 152), (21, 152), (8, 139))
        accent = ((14, 30), (32, 14), (42, 24), (42, 136), (32, 146), (14, 130))
    polygon(draw, outer, fill=(7, 13, 18, 246), outline=(48, 65, 73, 255))
    polygon(draw, inner, fill=(15, 25, 31, 240), outline=(35, 145, 138, 210))
    polygon(draw, accent, fill=(14, 83, 80, 230), outline=(93, 214, 198, 230))
    line(draw, ((58, 16), (454, 16)), (74, 198, 184, 112), 1)
    line(draw, ((58, 144), (454, 144)), (214, 161, 65, 105), 1)
    polygon(draw, ((206, 18), (250, 18), (238, 30), (195, 30)), fill=(35, 76, 78, 72))
    polygon(draw, ((326, 130), (374, 130), (362, 142), (314, 142)), fill=(103, 72, 30, 70))
    save(image, f"hud/plates/{name}_512x160.png", (512, 160))


def hud_bar_track():
    image, draw = canvas(384, 24)
    rect(draw, (0, 0, 383, 23), fill=(4, 9, 13, 244), outline=(54, 70, 77, 255), width=1, radius=6)
    rect(draw, (4, 4, 379, 19), fill=(13, 23, 29, 255), outline=(25, 39, 46, 255), width=1, radius=4)
    line(draw, ((14, 6), (370, 6)), (69, 109, 108, 92), 1)
    save(image, "hud/bars/bar_track_384x24.png", (384, 24))


def hud_bar_fill(name: str, size: tuple[int, int], top, bottom):
    width, height = size
    image, draw = canvas(width, height)
    rect(draw, (0, 0, width - 1, height - 1), fill=bottom, outline=top, width=1, radius=max(2, height // 4))
    rect(draw, (3, 3, width - 4, max(4, height // 2)), fill=top, radius=max(1, height // 5))
    for x in range(24, width, 48):
        polygon(draw, ((x, 2), (x + 16, 2), (x + 8, height - 2), (x - 8, height - 2)), fill=(255, 255, 255, 18))
    save(image, f"hud/bars/{name}_{width}x{height}.png", (width, height))


def hud_break_marker():
    image, draw = canvas(12, 32)
    polygon(draw, ((6, 0), (12, 8), (9, 16), (12, 24), (6, 32), (0, 24), (3, 16), (0, 8)), fill=(229, 175, 76, 255), outline=(255, 221, 142, 255))
    line(draw, ((6, 6), (6, 26)), (89, 55, 21, 255), 2)
    save(image, "hud/bars/bar_break_marker_12x32.png", (12, 32))


def hud_icon(name: str, painter):
    image, draw = canvas(32, 32)
    painter(draw)
    save(image, f"hud/icons/{name}_32x32.png", (32, 32))


def hud_icons():
    def health(draw):
        polygon(draw, ((16, 29), (5, 18), (3, 11), (6, 5), (12, 4), (16, 9), (20, 4), (26, 5), (29, 11), (27, 18)), fill=(61, 182, 145, 255), outline=(168, 247, 219, 255))
        line(draw, ((8, 16), (13, 16), (15, 11), (18, 21), (20, 16), (25, 16)), (231, 255, 247, 255), 2)

    def pressure(draw):
        polygon(draw, ((16, 2), (27, 7), (25, 22), (16, 30), (7, 22), (5, 7)), fill=(151, 103, 31, 255), outline=(244, 190, 83, 255))
        polygon(draw, ((16, 7), (22, 10), (21, 19), (16, 24), (11, 19), (10, 10)), fill=(28, 45, 48, 255), outline=(255, 218, 132, 255))
        line(draw, ((16, 9), (16, 20)), (239, 177, 68, 255), 2)

    hud_icon("health", health)
    hud_icon("pressure", pressure)


def hud_ability_slot():
    image, draw = canvas(88, 88)
    polygon(draw, ((0, 12), (12, 0), (76, 0), (88, 12), (88, 76), (76, 88), (12, 88), (0, 76)), fill=(7, 13, 18, 246), outline=(53, 70, 78, 255))
    polygon(draw, ((6, 15), (15, 6), (73, 6), (82, 15), (82, 73), (73, 82), (15, 82), (6, 73)), fill=(17, 27, 33, 242), outline=(40, 132, 128, 210))
    line(draw, ((20, 10), (68, 10)), (80, 205, 190, 116), 1)
    line(draw, ((20, 78), (68, 78)), (211, 157, 63, 108), 1)
    save(image, "hud/slots/ability_slot_frame_88x88.png", (88, 88))

    selected, selected_draw = canvas(88, 88)
    polygon(selected_draw, ((0, 12), (12, 0), (76, 0), (88, 12), (88, 76), (76, 88), (12, 88), (0, 76)), fill=(34, 159, 149, 28), outline=(112, 237, 218, 255))
    polygon(selected_draw, ((6, 15), (15, 6), (73, 6), (82, 15), (82, 73), (73, 82), (15, 82), (6, 73)), outline=(222, 174, 77, 238))
    save(selected, "hud/slots/ability_slot_selected_88x88.png", (88, 88))


def hud_crosshair():
    image, draw = canvas(64, 64)
    draw.ellipse((10 * SCALE, 10 * SCALE, 54 * SCALE, 54 * SCALE), outline=(116, 229, 211, 235), width=2 * SCALE)
    draw.ellipse((23 * SCALE, 23 * SCALE, 41 * SCALE, 41 * SCALE), outline=(227, 173, 74, 245), width=2 * SCALE)
    for points in (((32, 0), (37, 13), (32, 19), (27, 13)), ((64, 32), (51, 37), (45, 32), (51, 27)), ((32, 64), (27, 51), (32, 45), (37, 51)), ((0, 32), (13, 27), (19, 32), (13, 37))):
        polygon(draw, points, fill=(24, 105, 100, 245), outline=(135, 239, 222, 255))
    save(image, "hud/crosshair/lock_crosshair_64x64.png", (64, 64))


def hud_feedback_frame():
    image, draw = canvas(512, 56)
    polygon(draw, ((0, 10), (10, 0), (486, 0), (512, 26), (512, 56), (0, 56)), fill=(8, 15, 20, 238), outline=(47, 65, 73, 255))
    line(draw, ((16, 6), (470, 6)), (58, 190, 177, 205), 2)
    polygon(draw, ((0, 10), (10, 0), (28, 0), (18, 10), (18, 46), (8, 56), (0, 56)), fill=(19, 91, 87, 225), outline=(102, 221, 204, 220))
    save(image, "hud/feedback/feedback_frame_512x56.png", (512, 56))


def hud_network_badge():
    image, draw = canvas(288, 36)
    polygon(draw, ((0, 18), (18, 0), (270, 0), (288, 18), (270, 36), (18, 36)), fill=(8, 16, 21, 232), outline=(46, 67, 75, 255))
    line(draw, ((32, 5), (256, 5)), (57, 172, 161, 150), 1)
    line(draw, ((32, 31), (256, 31)), (205, 153, 62, 125), 1)
    polygon(draw, ((12, 18), (18, 12), (24, 18), (18, 24)), fill=(76, 207, 190, 255))
    save(image, "hud/badges/network_badge_288x36.png", (288, 36))


def hud_assets():
    hud_plate("player_plate", mirrored=False)
    hud_plate("target_plate", mirrored=True)
    hud_bar_track()
    hud_bar_fill("bar_fill_health_player", (384, 20), (99, 230, 189, 255), (24, 130, 104, 255))
    hud_bar_fill("bar_fill_health_target", (384, 20), (235, 113, 108, 255), (145, 48, 52, 255))
    hud_bar_fill("bar_fill_pressure", (384, 18), (247, 192, 82, 255), (157, 103, 31, 255))
    hud_break_marker()
    hud_icons()
    hud_ability_slot()
    hud_crosshair()
    hud_feedback_frame()
    hud_network_badge()


def form_input_field():
    image, draw = canvas(320, 48)
    rect(draw, (0, 0, 319, 47), fill=(8, 15, 20, 244), outline=(47, 66, 73, 255), width=2, radius=6)
    rect(draw, (5, 5, 314, 42), fill=(15, 27, 33, 248), outline=(40, 133, 128, 205), width=1, radius=4)
    line(draw, ((18, 8), (302, 8)), (77, 199, 184, 80), 1)
    polygon(draw, ((0, 12), (12, 0), (28, 0), (18, 10), (18, 38), (8, 48), (0, 48)), fill=(17, 83, 80, 205), outline=(90, 206, 192, 205))
    save(image, "forms/input_field_base_320x48.png", (320, 48))


def section_panel():
    image, draw = canvas(512, 256)
    rect(draw, (0, 0, 511, 255), fill=(9, 17, 22, 226), outline=(45, 61, 68, 255), width=2, radius=10)
    rect(draw, (7, 7, 504, 248), fill=(15, 25, 31, 218), outline=(31, 114, 109, 165), width=1, radius=7)
    line(draw, ((28, 12), (484, 12)), (67, 187, 174, 105), 1)
    line(draw, ((28, 243), (484, 243)), (196, 145, 57, 82), 1)
    save(image, "panels/panel_section_frame_512x256.png", (512, 256))


def status_badge():
    image, draw = canvas(192, 40)
    polygon(draw, ((0, 20), (20, 0), (172, 0), (192, 20), (172, 40), (20, 40)), fill=(8, 18, 24, 238), outline=(48, 70, 78, 255))
    line(draw, ((32, 5), (160, 5)), (62, 184, 172, 135), 1)
    line(draw, ((32, 35), (160, 35)), (204, 154, 63, 105), 1)
    save(image, "badges/status_badge_base_192x40.png", (192, 40))


def section_icons():
    def graphics(draw):
        rect(draw, (3, 6, 29, 24), fill=(20, 39, 44, 255), outline=ICON, width=2, radius=3)
        polygon(draw, ((9, 20), (15, 13), (20, 18), (24, 12), (28, 20)), fill=(31, 117, 109, 255), outline=(112, 229, 211, 255))
        line(draw, ((11, 28), (21, 28)), (223, 172, 74, 255), 2)

    def audio(draw):
        polygon(draw, ((4, 12), (10, 12), (17, 6), (17, 26), (10, 20), (4, 20)), fill=(28, 94, 89, 255), outline=ICON)
        draw.arc((13 * SCALE, 8 * SCALE, 27 * SCALE, 24 * SCALE), start=290, end=70, fill=ICON, width=2 * SCALE)
        draw.arc((13 * SCALE, 4 * SCALE, 31 * SCALE, 28 * SCALE), start=290, end=70, fill=(224, 173, 75, 255), width=2 * SCALE)

    def controls(draw):
        rect(draw, (3, 9, 29, 25), fill=(20, 39, 44, 255), outline=ICON, width=2, radius=7)
        line(draw, ((9, 14), (9, 20)), ICON, 2)
        line(draw, ((6, 17), (12, 17)), ICON, 2)
        draw.ellipse((20 * SCALE, 14 * SCALE, 23 * SCALE, 17 * SCALE), fill=(224, 173, 75, 255))
        draw.ellipse((24 * SCALE, 18 * SCALE, 27 * SCALE, 21 * SCALE), fill=(224, 173, 75, 255))

    for name, painter in (("graphics", graphics), ("audio", audio), ("controls", controls)):
        menu_icon(name, painter)


def authoring_form_assets():
    form_input_field()
    section_panel()
    status_badge()
    section_icons()


if __name__ == "__main__":
    panel_frame()
    command_button()
    divider()
    accent_diamond()
    icons()
    hud_assets()
    authoring_form_assets()
