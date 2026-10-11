# SPDX-License-Identifier: GPL-3.0-or-later
"""Edge feathering and anti-halo color dilation for texture color islands."""

import bpy
import numpy as np
from bpy.props import EnumProperty, IntProperty
from . import decals

_selection = None
_draw_handle = None


def set_selection(target, mask, seed=(0, 0)):
    global _selection
    y_idx, x_idx = np.where(mask)
    if len(y_idx) == 0:
        _selection = None
        return None
    x0, x1 = int(np.min(x_idx)), int(np.max(x_idx)) + 1
    y0, y1 = int(np.min(y_idx)), int(np.max(y_idx)) + 1
    _selection = {
        'target': target,
        'mask': mask,
        'bounds': (x0, y0, x1, y1),
        'count': int(np.sum(mask)),
        'seed': seed,
        'width': x1 - x0,
        'height': y1 - y0
    }
    return _selection


def clear_selection():
    global _selection
    _selection = None


def get_selection(target=None):
    if _selection is None:
        return None
    if target is not None and _selection.get('target') != target:
        return None
    return _selection


def has_selection(target=None):
    return get_selection(target) is not None


def draw_selection_overlay():
    if _selection is None:
        return
    context = bpy.context
    area = getattr(context, 'area', None)
    if not area or area.type != 'IMAGE_EDITOR':
        return
    target = _selection.get('target')
    if not target or area.spaces.active.image != target:
        return

    import gpu
    from gpu_extras.batch import batch_for_shader

    region = next((r for r in area.regions if r.type == 'WINDOW'), None)
    if not region:
        return

    x0, y0, x1, y1 = _selection['bounds']
    points = np.array([[x0, y0], [x1, y0], [x1, y1], [x0, y1]])
    h, w = target.size
    coords = [region.view2d.view_to_region(p[0] / w, p[1] / h, clip=False) for p in points]

    shader = gpu.shader.from_builtin('UNIFORM_COLOR')
    gpu.state.blend_set('ALPHA')
    try:
        # Bounding box
        batch = batch_for_shader(shader, 'LINE_STRIP', {'pos': coords + [coords[0]]})
        gpu.state.line_width_set(2.0)
        shader.bind()
        shader.uniform_float('color', (0.1, 0.85, 1.0, 0.9))
        batch.draw(shader)

        # Corner points
        gpu.state.point_size_set(7.0)
        batch_for_shader(shader, 'POINTS', {'pos': coords}).draw(shader)
    finally:
        gpu.state.point_size_set(1.0)
        gpu.state.line_width_set(1.0)
        gpu.state.blend_set('NONE')


def register_overlay():
    global _draw_handle
    if _draw_handle is None:
        _draw_handle = bpy.types.SpaceImageEditor.draw_handler_add(draw_selection_overlay, (), 'WINDOW', 'POST_PIXEL')


def unregister_overlay():
    global _draw_handle
    if _draw_handle is not None:
        bpy.types.SpaceImageEditor.draw_handler_remove(_draw_handle, 'WINDOW')
        _draw_handle = None


def gaussian_kernel_1d(radius, sigma=None):
    if radius <= 0:
        return np.array([1.0], dtype=np.float32)
    if sigma is None:
        sigma = max(radius / 2.0, 0.5)
    size = int(2 * radius + 1)
    x = np.arange(size, dtype=np.float32) - radius
    k = np.exp(-0.5 * (x / sigma) ** 2)
    return (k / k.sum()).astype(np.float32)


def separable_blur(arr, radius):
    if radius <= 0:
        return arr.copy()
    k = gaussian_kernel_1d(radius)
    h, w = arr.shape[:2]
    has_channels = arr.ndim == 3

    pad_h = ((0, 0), (radius, radius)) + (((0, 0),) if has_channels else ())
    padded_x = np.pad(arr, pad_h, mode='edge')
    out_x = np.zeros_like(arr)
    for i, weight in enumerate(k):
        if has_channels:
            out_x += weight * padded_x[:, i:i + w, :]
        else:
            out_x += weight * padded_x[:, i:i + w]

    pad_v = ((radius, radius), (0, 0)) + (((0, 0),) if has_channels else ())
    padded_y = np.pad(out_x, pad_v, mode='edge')
    out_y = np.zeros_like(arr)
    for i, weight in enumerate(k):
        if has_channels:
            out_y += weight * padded_y[i:i + h, ...]
        else:
            out_y += weight * padded_y[i:i + h]

    return out_y


def feather_alpha(sub_alpha, sub_mask, radius=4, mode='CENTER', falloff='SMOOTH'):
    blurred = separable_blur(sub_alpha, radius)
    blurred = np.clip(blurred, 0.0, 1.0)

    if mode == 'CENTER':
        res = blurred
    elif mode == 'INSIDE':
        res = np.clip((blurred - 0.45) / 0.55, 0.0, 1.0) * sub_mask
    elif mode == 'OUTSIDE':
        res = np.maximum(sub_alpha, np.clip(blurred / 0.5, 0.0, 1.0))
    else:
        res = blurred

    if falloff == 'SMOOTH':
        res = res * res * (3.0 - 2.0 * res)

    return np.clip(res, 0.0, 1.0)


def bleed_rgb(sub_rgb, sub_alpha, radius=4):
    w_rgb = sub_rgb * sub_alpha[..., None]
    w_alpha = sub_alpha.copy()

    bleed_radius = radius + 2
    blurred_w_rgb = separable_blur(w_rgb, bleed_radius)
    blurred_w_alpha = separable_blur(w_alpha, bleed_radius)

    safe_alpha = np.maximum(blurred_w_alpha[..., None], 1e-6)
    bled_rgb = blurred_w_rgb / safe_alpha

    solid_mask = sub_alpha[..., None] > 0.85
    return np.where(solid_mask, sub_rgb, bled_rgb)


def feather_rgba(rgba, radius=4, mode='CENTER', falloff='SMOOTH', alpha_threshold=0.02):
    if radius <= 0:
        return rgba.copy()

    h, w = rgba.shape[:2]
    alpha = rgba[:, :, 3]
    rgb = rgba[:, :, :3]

    mask = alpha > alpha_threshold
    if not np.any(mask):
        return rgba.copy()

    y_indices, x_indices = np.where(mask)
    margin = radius * 2 + 8
    y0 = max(0, int(np.min(y_indices)) - margin)
    y1 = min(h, int(np.max(y_indices)) + margin + 1)
    x0 = max(0, int(np.min(x_indices)) - margin)
    x1 = min(w, int(np.max(x_indices)) + margin + 1)

    sub_alpha = alpha[y0:y1, x0:x1].copy()
    sub_rgb = rgb[y0:y1, x0:x1].copy()
    sub_mask = mask[y0:y1, x0:x1].copy()

    new_sub_alpha = feather_alpha(sub_alpha, sub_mask, radius=radius, mode=mode, falloff=falloff)
    new_sub_rgb = bleed_rgb(sub_rgb, sub_alpha, radius=radius)

    out_rgba = rgba.copy()
    out_rgba[y0:y1, x0:x1, :3] = new_sub_rgb
    out_rgba[y0:y1, x0:x1, 3] = new_sub_alpha
    return out_rgba


def flood_fill_island(alpha, seed_x, seed_y, threshold=0.02):
    h, w = alpha.shape
    seed_x = int(np.clip(round(seed_x), 0, w - 1))
    seed_y = int(np.clip(round(seed_y), 0, h - 1))

    if alpha[seed_y, seed_x] <= threshold:
        for r in range(1, 9):
            y0, y1 = max(0, seed_y - r), min(h, seed_y + r + 1)
            x0, x1 = max(0, seed_x - r), min(w, seed_x + r + 1)
            sub = alpha[y0:y1, x0:x1]
            if np.max(sub) > threshold:
                ly, lx = np.unravel_index(np.argmax(sub), sub.shape)
                seed_y, seed_x = y0 + ly, x0 + lx
                break
        else:
            return np.zeros((h, w), dtype=bool)

    solid = alpha > threshold
    visited = np.zeros((h, w), dtype=bool)
    stack = [(seed_y, seed_x)]
    visited[seed_y, seed_x] = True

    while stack:
        cy, cx = stack.pop()
        y_min, y_max = max(0, cy - 1), min(h, cy + 2)
        x_min, x_max = max(0, cx - 1), min(w, cx + 2)
        for ny in range(y_min, y_max):
            for nx in range(x_min, x_max):
                if solid[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    stack.append((ny, nx))

    return visited


def feather_single_island(rgba, island_mask, radius=4, mode='CENTER', falloff='SMOOTH'):
    if radius <= 0 or not np.any(island_mask):
        return rgba.copy(), island_mask.copy()

    sub_rgba = rgba.copy()
    sub_rgba[~island_mask] = 0.0
    feathered_sub = feather_rgba(sub_rgba, radius=radius, mode=mode, falloff=falloff)
    new_island_mask = feathered_sub[:, :, 3] > 0.02

    base = rgba.copy()
    base[island_mask] = 0.0

    sub_alpha = feathered_sub[:, :, 3:4]
    base_alpha = base[:, :, 3:4]

    out_a = sub_alpha + base_alpha * (1.0 - sub_alpha)
    safe_a = np.maximum(out_a, 1e-6)
    out_rgb = (feathered_sub[:, :, :3] * sub_alpha + base[:, :, :3] * base_alpha * (1.0 - sub_alpha)) / safe_a

    result = np.zeros_like(rgba)
    result[:, :, :3] = np.where(out_a > 1e-6, out_rgb, 0.0)
    result[:, :, 3] = out_a[:, :, 0]
    return result, new_island_mask


class WGAMEUV_OT_select_island(bpy.types.Operator):
    bl_idname = 'wgame_uv.select_island'
    bl_label = '点击选中颜色孤岛'
    bl_description = '在图像编辑器中鼠标左键点击目标颜色孤岛，按 Alpha 连通性选中整座孤岛'

    def invoke(self, context, event):
        area = context.area if context.area and context.area.type == 'IMAGE_EDITOR' else next(
            (a for a in context.screen.areas if a.type == 'IMAGE_EDITOR'), None)
        if not area or not area.spaces.active.image:
            settings = getattr(context.scene, 'wgame_uv_tools', None)
            target = settings.decal_target if settings else None
            if not target:
                self.report({'WARNING'}, "请先在图像编辑器中打开需要处理的贴图")
                return {'CANCELLED'}
        else:
            target = area.spaces.active.image

        self.target = target
        self.area = area
        self.region = next(r for r in area.regions if r.type == 'WINDOW')
        area.header_text_set("点击要选中的颜色孤岛 · 左键选中 · 右键 / Esc 取消")
        context.window_manager.modal_handler_add(self)
        return {'RUNNING_MODAL'}

    def modal(self, context, event):
        if event.type in {'RIGHTMOUSE', 'ESC'} and event.value == 'PRESS':
            self.area.header_text_set(None)
            self.area.tag_redraw()
            return {'CANCELLED'}

        if event.type == 'LEFTMOUSE' and event.value == 'PRESS':
            r = self.region
            x, y = event.mouse_x - r.x, event.mouse_y - r.y
            inside = 0 <= x < r.width and 0 <= y < r.height
            for other in self.area.regions:
                if other.type != 'WINDOW' and other.width > 1 and other.height > 1 and (
                    other.x <= event.mouse_x < other.x + other.width and other.y <= event.mouse_y < other.y + other.height):
                    inside = False
                    break
            if not inside:
                return {'PASS_THROUGH'}

            u, v = r.view2d.region_to_view(x, y)
            w, h = self.target.size
            px = int(np.clip(round(u * w), 0, w - 1))
            py = int(np.clip(round(v * h), 0, h - 1))

            original = decals.pixels(self.target)
            mask = flood_fill_island(original[:, :, 3], px, py)
            if not np.any(mask):
                self.report({'WARNING'}, "点击位置未检测到不透明颜色孤岛（Alpha > 0），请点击有色彩的区域")
                return {'RUNNING_MODAL'}

            sel = set_selection(self.target, mask, (px, py))
            self.area.header_text_set(None)
            self.area.tag_redraw()
            self.report({'INFO'}, f"已选中颜色孤岛（共 {sel['count']} 像素，尺寸 {sel['width']}×{sel['height']}）")
            return {'FINISHED'}

        return {'PASS_THROUGH'}


class WGAMEUV_OT_feather_selected(bpy.types.Operator):
    bl_idname = 'wgame_uv.feather_selected'
    bl_label = '羽化选中的孤岛'
    bl_description = '对当前选中的颜色孤岛边缘进行自然羽化，其余区域保持不变'
    bl_options = {'REGISTER', 'UNDO'}

    radius: IntProperty(name="羽化半径", default=4, min=1, max=64)
    mode: EnumProperty(
        name="羽化方向",
        items=[
            ('CENTER', "居中羽化", "以原边缘为中心内外平滑过渡，最自然柔和"),
            ('INSIDE', "向内收缩", "严格不超出原颜色孤岛边界，向内部渐变（适合紧贴 UV 边界）"),
            ('OUTSIDE', "向外扩张", "保持内部完全不透明，向透明区域外扩渐隐")
        ],
        default='CENTER'
    )
    falloff: EnumProperty(
        name="过渡曲线",
        items=[
            ('SMOOTH', "平滑曲线", "S 型 Hermite 平滑过渡，柔和自然"),
            ('LINEAR', "线性过渡", "线性均匀衰减")
        ],
        default='SMOOTH'
    )

    @classmethod
    def poll(cls, context):
        return has_selection()

    def execute(self, context):
        sel = get_selection()
        if not sel:
            self.report({'WARNING'}, "请先点击【点击选中颜色孤岛】选中要羽化的孤岛")
            return {'CANCELLED'}

        target = sel['target']
        if not target.is_editable:
            self.report({'ERROR'}, f"贴图 {target.name} 不可编辑")
            return {'CANCELLED'}

        original = decals.pixels(target)
        mask = sel['mask']

        feathered, new_mask = feather_single_island(original, mask, radius=self.radius, mode=self.mode, falloff=self.falloff)
        decals.write_pixels(target, feathered)

        from . import canvas_editor
        canvas_editor._history.append((target, original, canvas_editor.digest(decals.pixels(target))))
        canvas_editor._redo.clear()
        canvas_editor.trim_history()

        # Update selection mask exclusively for this island
        if np.any(new_mask):
            set_selection(target, new_mask, sel.get('seed', (0, 0)))

        if context.area:
            context.area.tag_redraw()
        self.report({'INFO'}, f"已完成所选颜色孤岛羽化（半径 {self.radius}px，{self.mode}）")
        return {'FINISHED'}


class WGAMEUV_OT_clear_selection(bpy.types.Operator):
    bl_idname = 'wgame_uv.clear_selection'
    bl_label = '取消选中孤岛'
    bl_description = '取消当前选中的颜色孤岛'

    @classmethod
    def poll(cls, context):
        return has_selection()

    def execute(self, context):
        clear_selection()
        if context.area:
            context.area.tag_redraw()
        self.report({'INFO'}, "已取消孤岛选中")
        return {'FINISHED'}


class WGAMEUV_OT_feather_islands(bpy.types.Operator):
    bl_idname = 'wgame_uv.feather_islands'
    bl_label = '羽化颜色孤岛'
    bl_description = '对贴图或浮动素材上的颜色孤岛边缘进行自然羽化过渡，并自动扩边填充颜色防止黑边'
    bl_options = {'REGISTER', 'UNDO'}

    radius: IntProperty(name="羽化半径", default=4, min=1, max=64)
    mode: EnumProperty(
        name="羽化方向",
        items=[
            ('CENTER', "居中羽化", "以原边缘为中心内外平滑过渡，最自然柔和"),
            ('INSIDE', "向内收缩", "严格不超出原颜色孤岛边界，向内部渐变"),
            ('OUTSIDE', "向外扩张", "保持内部完全不透明，向透明区域外扩渐隐")
        ],
        default='CENTER'
    )
    falloff: EnumProperty(
        name="过渡曲线",
        items=[('SMOOTH', "平滑曲线", ""), ('LINEAR', "线性过渡", "")],
        default='SMOOTH'
    )

    def execute(self, context):
        from . import canvas_editor

        session = canvas_editor._session
        # Target 1: Active floating patch in Canvas Editor
        if session and session.get('phase') == 'FLOAT' and session.get('patch') is not None:
            session.setdefault('patch_undo_stack', []).append(session['patch'].copy())
            feathered = feather_rgba(session['patch'], radius=self.radius, mode=self.mode, falloff=self.falloff)
            session['patch'] = feathered
            canvas_editor.prepare_patch(session)
            canvas_editor.render()
            self.report({'INFO'}, f"浮动素材已羽化（半径 {self.radius}px，{self.mode}）")
            return {'FINISHED'}

        # Target 2: If an island is currently selected, feather that selected island!
        sel = get_selection()
        if sel:
            target = sel['target']
            original = decals.pixels(target)
            mask = sel['mask']
            feathered, new_mask = feather_single_island(original, mask, radius=self.radius, mode=self.mode, falloff=self.falloff)
            decals.write_pixels(target, feathered)
            canvas_editor._history.append((target, original, canvas_editor.digest(decals.pixels(target))))
            canvas_editor._redo.clear()
            canvas_editor.trim_history()
            if np.any(new_mask):
                set_selection(target, new_mask, sel.get('seed', (0, 0)))
            if context.area:
                context.area.tag_redraw()
            self.report({'INFO'}, f"已羽化选中的颜色孤岛（半径 {self.radius}px，{self.mode}）")
            return {'FINISHED'}

        # Target 3: Entire image (all islands)
        target = None
        if context.area and context.area.type == 'IMAGE_EDITOR' and context.space_data.image:
            img = context.space_data.image
            if not img.get(canvas_editor.TAG):
                target = img

        settings = getattr(context.scene, 'wgame_uv_tools', None)
        if not target and settings:
            target = settings.decal_target or settings.decal_source

        if not target:
            self.report({'WARNING'}, "请先在图像编辑器中打开贴图，或点击【点击选中颜色孤岛】")
            return {'CANCELLED'}

        if not target.is_editable:
            self.report({'ERROR'}, f"贴图 {target.name} 不可编辑")
            return {'CANCELLED'}

        original = decals.pixels(target)
        feathered = feather_rgba(original, radius=self.radius, mode=self.mode, falloff=self.falloff)
        decals.write_pixels(target, feathered)
        canvas_editor._history.append((target, original, canvas_editor.digest(decals.pixels(target))))
        canvas_editor._redo.clear()
        canvas_editor.trim_history()
        self.report({'INFO'}, f"贴图【{target.name}】全部孤岛已羽化（半径 {self.radius}px，{self.mode}）")
        return {'FINISHED'}
