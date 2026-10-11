# SPDX-License-Identifier: GPL-3.0-or-later
"""Floating image selections: originals stay untouched until an explicit commit."""
import hashlib
import math
import time
import bpy
import numpy as np
from bpy_extras.io_utils import ImportHelper
from . import decals

_session = None
_history = globals().get('_history', [])
_redo = globals().get('_redo', [])
_floating_redo = None
_keymaps = []
TAG = 'wgame_canvas_preview'


def resized(values, width, height):
    if values.shape[:2] == (height,width):return values
    x=np.minimum((np.arange(width)+.5)*values.shape[1]/width,values.shape[1]-1).astype(int)
    y=np.minimum((np.arange(height)+.5)*values.shape[0]/height,values.shape[0]-1).astype(int)
    return values[y[:,None],x[None,:]]


def proxy(values, limit):
    h,w=values.shape[:2];scale=min(1.,limit/max(w,h))
    return resized(values,max(1,round(w*scale)),max(1,round(h*scale)))


def prepare_patch(session):
    if session.get('patch_image'):
        bpy.data.images.remove(session['patch_image'])
    patch=proxy(session['patch'],2048)
    image=bpy.data.images.new('浮动素材 · GPU',width=patch.shape[1],height=patch.shape[0],alpha=True)
    image[TAG]=True;image.colorspace_settings.name=session['target'].colorspace_settings.name
    decals.write_pixels(image,patch)
    session['patch_image']=image
    session['patch_texture']=None
    session['patch_proxy']=proxy(session['patch'],1024)
    session['base_low']=proxy(session['base'],256)
    session['base_high']=proxy(session['base'],512)
    decals.write_pixels(session['display'],session['base'])


def uv_borders(obj):
    segments=[]
    if not obj or obj.type != 'MESH':return segments
    with decals.mesh_snapshot(obj) as bm:
        uv=bm.loops.layers.uv.get('DetailUV')
        if not uv:return segments
        valid={f for f in bm.faces if not f.hide and any(
            (l[uv].uv-l.link_loop_next[uv].uv).length_squared>1e-12 for l in f.loops)}
        for face in valid:
            for loop in face.loops:
                a,b=loop[uv].uv,loop.link_loop_next[uv].uv
                connected=False
                for other in loop.edge.link_faces:
                    if other==face or other not in valid:continue
                    coords={l.vert:l[uv].uv for l in other.loops if l.vert in loop.edge.verts}
                    if (a-coords[loop.vert]).length_squared<1e-12 and (b-coords[loop.link_loop_next.vert]).length_squared<1e-12:
                        connected=True;break
                if not connected:segments.extend((tuple(a),tuple(b)))
    return segments


def digest(values):
    return hashlib.sha256(values.tobytes()).hexdigest()


def image_nodes(tree, seen):
    if not tree or tree.as_pointer() in seen:
        return
    seen.add(tree.as_pointer())
    for node in tree.nodes:
        if node.type == 'TEX_IMAGE':
            yield node
        elif node.type == 'GROUP':
            yield from image_nodes(node.node_tree, seen)


def image_editor(context):
    if context.area and context.area.type == 'IMAGE_EDITOR':return context.area
    return next((a for a in context.screen.areas if a.type == 'IMAGE_EDITOR'), None) if context.screen else None


def target_image(context, area=None):
    area = area or image_editor(context)
    target = area.spaces.active.image if area else None
    if target is None:
        # A 2D operation edits the displayed canvas, regardless of selected faces
        # or the active color/emission material node. Infer only when none is shown.
        obj = context.active_object
        if not obj or obj.type != 'MESH':
            raise ValueError('请在图像编辑器选择目标画布图片，再导入素材')
        with decals.mesh_snapshot(obj) as bm:
            uv = bm.loops.layers.uv.get('DetailUV')
            faces = [f for f in bm.faces if not f.hide and uv and
                     any(tuple(loop[uv].uv) != (-1., -1.) for loop in f.loops)]
            if not faces:
                raise ValueError('请在图像编辑器选择目标画布图片，再导入素材')
            try:target = decals.target_for_faces(obj, faces)
            except ValueError as exc:
                raise ValueError('模型没有唯一的细节画布，请在图像编辑器选择目标图片') from exc
    decals.pixels(target)
    if max(target.size) > 4096:
        raise ValueError('本版画布最大为 4096 像素')
    return target


def begin(context, action, source=None):
    global _session
    cancel()
    decals.cancel()
    area = image_editor(context)
    if not area:
        raise ValueError('请先打开 Texture Paint，保留左侧图像编辑器')
    target = target_image(context, area)
    if context.active_object and context.active_object.type == 'MESH':
        context.active_object.data.use_paint_mask = False
        context.active_object.data.use_paint_mask_vertex = False
    base = decals.pixels(target)
    patch = None
    if action == 'IMPORT':
        if not source:
            raise ValueError('请先导入素材')
        if source == target:
            raise ValueError('导入素材不能是目标细节图；移动已有内容请使用框选')
        patch = decals.pixels(source)
        if max(source.size) > 4096:
            raise ValueError('素材最大为 4096 像素')
        if source.colorspace_settings.name != target.colorspace_settings.name:
            raise ValueError('素材和画布请使用相同色彩空间，颜色图通常为 sRGB')
    small=proxy(base,512)
    preview = bpy.data.images.new('细节图 · 模型预览', width=small.shape[1], height=small.shape[0],
                                  alpha=True, float_buffer=target.is_float)
    preview[TAG] = True
    preview.colorspace_settings.name = target.colorspace_settings.name
    preview.alpha_mode = target.alpha_mode
    decals.write_pixels(preview, small)
    display=bpy.data.images.new('细节图 · 画布预览',width=base.shape[1],height=base.shape[0],alpha=True,float_buffer=target.is_float)
    display[TAG]=True;display.colorspace_settings.name=target.colorspace_settings.name;display.alpha_mode=target.alpha_mode
    decals.write_pixels(display,base)
    nodes = []
    seen = set()
    for material in bpy.data.materials:
        for node in image_nodes(material.node_tree, seen):
            if node.image == target:
                nodes.append(node)
                node.image = preview
    spaces = []
    for screen in bpy.data.screens:
        for a in screen.areas:
            if a.type == 'IMAGE_EDITOR' and (a.spaces.active.image == target or a == area):
                space = a.spaces.active
                spaces.append((space, space.image, space.mode))
                space.image = display
                space.mode = 'VIEW'
    canvases = []
    for scene in bpy.data.scenes:
        if scene.tool_settings.image_paint.canvas == target:
            canvases.append(scene)
            scene.tool_settings.image_paint.canvas = display
    _session = dict(target=target, preview=preview, display=display, original=base, base=base.copy(),source=source,
        original_digest=digest(base), patch=patch, nodes=nodes, spaces=spaces, canvases=canvases,
        area=area, region=next(r for r in area.regions if r.type == 'WINDOW'),
        scene=context.scene, action=action, phase='FLOAT' if patch is not None else 'RECT',
        center=np.array([base.shape[1]/2, base.shape[0]/2]), scale=1., angle=0., stretch=np.ones(2), shear=0.,
        uv_segments=uv_borders(context.active_object),
        rect_start=None, rect_end=None, dirty=True, updating=False, timer=None, draw_handle=None,
        patch_image=None, patch_texture=None, last_change=time.monotonic(),last_preview=0.,quality='LOW',uv_batch=None,uv_view=None,
        transform_states=[],transform_redo=[],selection_bounds=None)
    context.scene.wgame_uv_tools.decal_target = target
    context.scene.wgame_uv_tools.ui_tab_uv = 'DECAL'
    if patch is not None:
        _session['scale'] = min(1., base.shape[1]*.5/patch.shape[1], base.shape[0]*.5/patch.shape[0])
        prepare_patch(_session)
        sync_fields()
        render()
        _session['transform_states']=[transform_state(_session)]
    context.scene.wgame_uv_tools.canvas_message = '拖动摆放；S 缩放，R 旋转，Enter 确认，Esc 取消' if patch is not None else ('在左侧画布点击目标颜色孤岛' if action == 'PICK' else '在左侧画布拖出矩形选区')
    return _session


def detach(session):
    preview = session['preview']
    for node in session['nodes']:
        try:
            if node.image == preview:
                node.image = session['target']
        except ReferenceError:
            pass
    for space, original, mode in session['spaces']:
        try:
            if space.image == session['display']:
                space.image = original
                space.mode = mode
        except ReferenceError:
            pass
    for scene in session['canvases']:
        if scene.tool_settings.image_paint.canvas == session['display']:
            scene.tool_settings.image_paint.canvas = session['target']
    if session['draw_handle']:
        bpy.types.SpaceImageEditor.draw_handler_remove(session['draw_handle'], 'WINDOW')
    if session['timer']:
        bpy.context.window_manager.event_timer_remove(session['timer'])
    try:
        session['area'].header_text_set(None)
        session['area'].tag_redraw()
    except ReferenceError:
        pass
    # Restore bindings before disposing of the temporary image.
    bpy.data.images.remove(preview)
    bpy.data.images.remove(session['display'])
    session['patch_texture']=None
    if session['patch_image']:bpy.data.images.remove(session['patch_image'])


def cancel():
    global _session, _floating_redo
    _floating_redo=None
    if _session:
        session = _session
        _session = None
        detach(session)


def clear_session():
    cancel()
    _history.clear()
    _redo.clear()


def transform_state(session):
    return (tuple(session['center']),session['scale'],session['angle'],tuple(session['stretch']),session['shear'])


def checkpoint():
    if not _session or _session['phase']!='FLOAT':return
    state=transform_state(_session)
    states=_session['transform_states']
    if not states or state!=states[-1]:
        states.append(state)
        if len(states)>64:states.pop(0)


def restore_transform(state):
    _session['center']=np.array(state[0]);_session['scale']=state[1];_session['angle']=state[2]
    _session['stretch']=np.array(state[3]);_session['shear']=state[4]
    sync_fields()


def trim_history():
    while len(_history)+len(_redo)>1 and (len(_history)+len(_redo)>8 or
        sum(x[1].nbytes for x in _history+_redo)>256*1024*1024):
        if _history:_history.pop(0)
        else:_redo.pop(0)


@bpy.app.handlers.persistent
def before_save(_):
    # Never serialize temporary image bindings into the user's Blend.
    cancel()


def sync_fields():
    session = _session
    s = session['scene'].wgame_uv_tools
    session['updating'] = True
    s.canvas_x, s.canvas_y = session['center']
    s.canvas_scale = session['scale']
    s.canvas_angle = session['angle']
    s.canvas_scale_x,s.canvas_scale_y=session['stretch']
    s.canvas_shear=session['shear']
    session['updating'] = False
    session['dirty'] = True
    session['last_change']=time.monotonic()
    session['area'].tag_redraw()


def field_update(self, context):
    if not _session or _session['updating'] or _session['phase'] != 'FLOAT':
        return
    _session['center'] = np.array([self.canvas_x, self.canvas_y])
    _session['scale'] = self.canvas_scale
    _session['angle'] = self.canvas_angle
    _session['stretch']=np.array([self.canvas_scale_x,self.canvas_scale_y])
    _session['shear']=self.canvas_shear
    _session['dirty'] = True
    _session['last_change']=time.monotonic()
    _session['area'].tag_redraw()


def corners(session):
    height, width = session['patch'].shape[:2]
    q = np.array([[-width,-height],[width,-height],[width,height],[-width,height]], dtype=float)*.5*session['scale']
    q *= session['stretch']
    q[:,0] += session['shear']*q[:,1]
    c, s = math.cos(session['angle']), math.sin(session['angle'])
    return q @ np.array([[c,s],[-s,c]]) + session['center']


def render(final=False):
    session = _session
    if not session or session['phase'] != 'FLOAT':
        return
    height, width = session['base'].shape[:2]
    dest = corners(session)/np.array([width,height])
    src = np.array([[0,0],[1,0],[1,1],[0,1]], dtype=float)
    indices = [[0,1,2],[0,2,3]]
    if final:
        session['result'], _ = decals.composite(session['base'], session['patch'], dest[indices], src[indices], 1.)
        return
    _session['transform_redo'].clear()
    moving=time.monotonic()-session['last_change']<.18
    base=session['base_low'] if moving else session['base_high']
    result, _ = decals.composite(base,session['patch_proxy'],dest[indices],src[indices],1.)
    w,h=session['preview'].size
    decals.write_pixels(session['preview'],resized(result,w,h))
    session['last_preview']=time.monotonic();session['quality']='LOW' if moving else 'HIGH'
    session['dirty'] = False
    session['area'].tag_redraw()


def select_rectangle(start, end):
    session = _session
    height, width = session['base'].shape[:2]
    low = np.maximum(np.floor(np.minimum(start,end)).astype(int),0)
    high = np.minimum(np.ceil(np.maximum(start,end)).astype(int),[width,height])
    if np.any(high <= low):
        raise ValueError('请在图片范围内框选至少一个像素')
    x0,y0 = low; x1,y1 = high
    session['patch'] = session['original'][y0:y1,x0:x1].copy()
    session['base'] = session['original'].copy()
    if session['action'] == 'MOVE':
        session['base'][y0:y1,x0:x1] = 0
    session['center'] = (low+high)/2
    session['scale'] = 1.; session['angle'] = 0.; session['phase'] = 'FLOAT'
    session['stretch']=np.ones(2);session['shear']=0.
    session['selection_bounds']=(low.copy(),high.copy())
    prepare_patch(session)
    sync_fields(); render()
    session['transform_states']=[transform_state(session)]
    session['scene'].wgame_uv_tools.canvas_message = '拖动选区；S 缩放，R 旋转，Enter 确认，Esc 取消'


def select_island(point):
    session = _session
    height, width = session['base'].shape[:2]
    px = int(np.clip(round(point[0]), 0, width - 1))
    py = int(np.clip(round(point[1]), 0, height - 1))
    from .feather import flood_fill_island
    mask = flood_fill_island(session['original'][:, :, 3], px, py)
    if not np.any(mask):
        raise ValueError('点击位置未检测到不透明颜色孤岛（Alpha > 0），请点击有色彩区域')
    y_idx, x_idx = np.where(mask)
    x0, x1 = int(np.min(x_idx)), int(np.max(x_idx)) + 1
    y0, y1 = int(np.min(y_idx)), int(np.max(y_idx)) + 1

    sub_patch = session['original'][y0:y1, x0:x1].copy()
    sub_mask = mask[y0:y1, x0:x1]
    sub_patch[~sub_mask] = 0.0

    session['patch'] = sub_patch
    session['base'] = session['original'].copy()
    if session['action'] in {'MOVE', 'PICK'}:
        session['base'][mask] = 0.0
    session['center'] = np.array([(x0 + x1) / 2.0, (y0 + y1) / 2.0])
    session['scale'] = 1.0; session['angle'] = 0.0; session['phase'] = 'FLOAT'
    session['stretch'] = np.ones(2); session['shear'] = 0.0
    session['selection_bounds'] = (np.array([x0, y0]), np.array([x1, y1]))
    session['island_mask'] = mask
    prepare_patch(session)
    sync_fields(); render()
    session['transform_states'] = [transform_state(session)]
    session['scene'].wgame_uv_tools.canvas_message = '已选中颜色孤岛；可移动/缩放/羽化边缘；Enter 确认，Esc 取消'


def commit():
    global _session, _floating_redo
    session = _session
    if not session or session['phase'] != 'FLOAT':
        raise ValueError('请先导入素材或框选像素')
    current = decals.pixels(session['target'])
    if digest(current) != session['original_digest']:
        raise ValueError('原图在预览期间被其他操作修改，请取消后重新操作')
    render(final=True)
    target = session['target']
    decals.write_pixels(target, session['result'])
    _history.append((target, session['original'], digest(decals.pixels(target))))
    _redo.clear();_floating_redo=None;trim_history()
    decals._undo = None
    settings = session['scene'].wgame_uv_tools
    settings.decal_target = target
    _session = None
    detach(session)
    settings.canvas_message = '已确认合并，尚未保存图片'


def undo():
    global _floating_redo
    if _session:
        if _session.get('patch_undo_stack'):
            _session.setdefault('patch_redo_stack', []).append(_session['patch'].copy())
            _session['patch'] = _session['patch_undo_stack'].pop()
            prepare_patch(_session)
            render()
            return
        checkpoint()
        states=_session['transform_states']
        if len(states)>1:
            _session['transform_redo'].append(states.pop())
            restore_transform(states[-1])
        else:
            seed={k:_session[k] for k in ('target','source','action','selection_bounds','original_digest')}
            seed['redo_depth']=len(_redo)
            seed['transform']=transform_state(_session) if _session['phase']=='FLOAT' else None
            cancel();_floating_redo=seed
        return
    if not _history:
        raise ValueError('当前会话没有可撤销的二维编辑')
    image, before, after = _history[-1]
    current=decals.pixels(image)
    if digest(current) != after:
        raise ValueError('图片此后已被其他工具修改，不能覆盖这些新改动')
    decals.write_pixels(image,before)
    _history.pop()
    _redo.append((image,current,digest(decals.pixels(image))))
    trim_history()


def resume_floating(context):
    seed=_floating_redo
    if not seed or target_image(context)!=seed['target'] or digest(decals.pixels(seed['target']))!=seed['original_digest']:
        raise ValueError('画布已变化，不能恢复上次浮动编辑')
    session=begin(context,seed['action'],seed['source'])
    if seed['selection_bounds'] is not None:select_rectangle(*seed['selection_bounds'])
    if seed['transform'] is not None:
        restore_transform(seed['transform']);session['transform_states']=[seed['transform']]
    return session


def redo(context=None):
    if _session:
        if _session.get('patch_redo_stack'):
            _session.setdefault('patch_undo_stack', []).append(_session['patch'].copy())
            _session['patch'] = _session['patch_redo_stack'].pop()
            prepare_patch(_session)
            render()
            return
        states=_session['transform_redo']
        if not states:raise ValueError('没有可重做的浮动变换')
        state=states.pop();_session['transform_states'].append(state);restore_transform(state)
        return
    if _floating_redo and len(_redo)<=_floating_redo['redo_depth']:
        if not context:raise ValueError('请在二维画布中重做浮动导入')
        result=bpy.ops.wgame_uv.canvas_transform('INVOKE_DEFAULT',action='RESUME')
        if 'RUNNING_MODAL' not in result:raise ValueError('无法恢复浮动编辑')
        return
    if _redo:
        image,after,expected=_redo[-1]
        current=decals.pixels(image)
        if digest(current)!=expected:raise ValueError('图片已被其他操作修改，不能覆盖新改动')
        decals.write_pixels(image,after);_redo.pop()
        _history.append((image,current,digest(decals.pixels(image))))
        trim_history()
        return
    if _floating_redo and context:
        result=bpy.ops.wgame_uv.canvas_transform('INVOKE_DEFAULT',action='RESUME')
        if 'RUNNING_MODAL' not in result:raise ValueError('无法恢复浮动编辑')
        return
    raise ValueError('没有可重做的二维编辑')


def event_point(session,event):
    region=session['region']
    x,y=event.mouse_x-region.x,event.mouse_y-region.y
    u,v=region.view2d.region_to_view(x,y)
    height,width=session['base'].shape[:2]
    inside = 0<=x<region.width and 0<=y<region.height
    # Blender's overlapping sidebar can sit over WINDOW's coordinate rectangle.
    # Let its buttons and numeric fields receive their own events.
    for other in session['area'].regions:
        if other.type != 'WINDOW' and other.width>1 and other.height>1 and (
            other.x<=event.mouse_x<other.x+other.width and other.y<=event.mouse_y<other.y+other.height):
            inside=False
            break
    return np.array([u*width,v*height]), inside


def screen_corners(session):
    height,width=session['base'].shape[:2]
    return [session['region'].view2d.view_to_region(p[0]/width,p[1]/height,clip=False) for p in corners(session)]


def clipped_quad(session):
    h,w=session['base'].shape[:2]
    vertices=[(p,np.array(t,float)) for p,t in zip(corners(session),[(0,0),(1,0),(1,1),(0,1)])]
    for axis,bound,sign in [(0,0,1),(0,w,-1),(1,0,1),(1,h,-1)]:
        output=[]
        if not vertices:break
        prev=vertices[-1];prev_in=(prev[0][axis]-bound)*sign>=0
        for current in vertices:
            inside=(current[0][axis]-bound)*sign>=0
            if inside!=prev_in:
                t=(bound-prev[0][axis])/(current[0][axis]-prev[0][axis])
                output.append((prev[0]+t*(current[0]-prev[0]),prev[1]+t*(current[1]-prev[1])))
            if inside:output.append(current)
            prev=current;prev_in=inside
        vertices=output
    return vertices


def draw_overlay():
    session=_session
    if not session or bpy.context.area!=session['area']:
        return
    import gpu
    from gpu_extras.batch import batch_for_shader
    shader=gpu.shader.from_builtin('UNIFORM_COLOR')
    gpu.state.blend_set('ALPHA')
    try:
        if session['phase']=='FLOAT':
            vertices=clipped_quad(session)
            if len(vertices)>=3:
                h,w=session['base'].shape[:2]
                coords=[session['region'].view2d.view_to_region(p[0]/w,p[1]/h,clip=False) for p,t in vertices]
                image_shader=gpu.shader.from_builtin('IMAGE')
                if session['patch_texture'] is None:
                    session['patch_texture']=gpu.texture.from_image(session['patch_image'])
                image_shader.bind();image_shader.uniform_sampler('image',session['patch_texture'])
                batch_for_shader(image_shader,'TRIS',{'pos':coords,'texCoord':[tuple(t) for p,t in vertices]},
                    indices=[(0,i,i+1) for i in range(1,len(vertices)-1)]).draw(image_shader)
        if session['scene'].wgame_uv_tools.canvas_show_uv and session['uv_segments']:
            region=session['region']
            view_key=(region.view2d.region_to_view(0,0),region.view2d.region_to_view(region.width,region.height),region.width,region.height)
            if session['uv_batch'] is None or session['uv_view']!=view_key:
                coords=[region.view2d.view_to_region(*p,clip=False) for p in session['uv_segments']]
                session['uv_batch']=batch_for_shader(shader,'LINES',{'pos':coords});session['uv_view']=view_key
            batch=session['uv_batch']
            shader.bind()
            gpu.state.line_width_set(3.);shader.uniform_float('color',(0.,0.,0.,.8));batch.draw(shader)
            gpu.state.line_width_set(1.3);shader.uniform_float('color',(.1,.85,1.,.95));batch.draw(shader)
        if session['phase']=='RECT':
            if session['rect_start'] is None or session['rect_end'] is None:return
            a,b=session['rect_start'],session['rect_end']
            points=np.array([[a[0],a[1]],[b[0],a[1]],[b[0],b[1]],[a[0],b[1]]])
            height,width=session['base'].shape[:2]
            coords=[session['region'].view2d.view_to_region(p[0]/width,p[1]/height,clip=False) for p in points]
        else:
            coords=screen_corners(session)
        batch=batch_for_shader(shader,'LINE_STRIP',{'pos':coords+[coords[0]]})
        gpu.state.line_width_set(2.)
        shader.bind();shader.uniform_float('color',(1.,.65,.05,1.));batch.draw(shader)
        if session['phase']=='FLOAT':
            gpu.state.point_size_set(8.)
            batch_for_shader(shader,'POINTS',{'pos':coords}).draw(shader)
    finally:
        gpu.state.point_size_set(1.);gpu.state.line_width_set(1.);gpu.state.blend_set('NONE')


class WGAMEUV_OT_canvas_transform(bpy.types.Operator):
    bl_idname='wgame_uv.canvas_transform'
    bl_label='二维画布编辑'
    action:bpy.props.EnumProperty(items=[('IMPORT','摆放素材',''),('PICK','点击选中孤岛',''),('MOVE','框选移动',''),('COPY','框选复制',''),('RESUME','恢复浮动编辑','')])

    def invoke(self,context,event):
        try:
            if self.action=='RESUME':
                session=resume_floating(context)
            else:
                session=begin(context,self.action,context.scene.wgame_uv_tools.decal_source)
            self.token=session
            self.drag=None
            session['draw_handle']=bpy.types.SpaceImageEditor.draw_handler_add(draw_overlay,(),'WINDOW','POST_PIXEL')
            session['timer']=context.window_manager.event_timer_add(.05,window=context.window)
            header='点击要选中的颜色孤岛 · S 缩放 · R 旋转 · Enter 确认 · Esc 取消' if self.action=='PICK' else '框选 / 拖动 · S 缩放 · R 旋转 · Enter 确认 · Esc 取消'
            session['area'].header_text_set(header)
            context.window_manager.modal_handler_add(self)
        except (ValueError,RuntimeError) as exc:
            cancel();self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'RUNNING_MODAL'}

    def modal(self,context,event):
        session=_session
        if session is not self.token:
            return {'CANCELLED'}
        if not any(a==session['area'] for a in context.screen.areas):
            cancel();return {'CANCELLED'}
        point,inside=event_point(session,event)
        try:
            if event.type=='TIMER':
                now=time.monotonic()
                if now-session['last_preview']>=.1 and (session['dirty'] or (session['quality']=='LOW' and now-session['last_change']>=.18)):
                    render()
                return {'PASS_THROUGH'}
            if event.type=='ESC' and event.value=='PRESS':
                cancel();return {'CANCELLED'}
            if inside and event.type=='Z' and event.value=='PRESS' and event.ctrl:
                self.drag=None
                redo(context) if event.shift else undo()
                return {'RUNNING_MODAL'} if _session is session else {'CANCELLED'}
            if inside and event.type in {'RET','NUMPAD_ENTER'} and event.value=='PRESS':
                commit();return {'FINISHED'}
            if inside and event.type in {'S','R'} and event.value=='PRESS' and session['phase']=='FLOAT':
                checkpoint();session['transform_redo'].clear()
                self.drag=(event.type,point.copy(),session['center'].copy(),session['scale'],session['angle'])
                return {'RUNNING_MODAL'}
            if event.type=='LEFTMOUSE' and event.value=='PRESS' and inside:
                if self.drag and self.drag[0] in {'S','R'}:
                    checkpoint()
                    self.drag=None
                elif session['phase']=='RECT':
                    if session['action']=='PICK':
                        select_island(point)
                        self.drag=None
                        return {'RUNNING_MODAL'}
                    session['rect_start']=point;session['rect_end']=point
                    self.drag=('RECT',point.copy())
                else:
                    checkpoint();session['transform_redo'].clear()
                    mouse=np.array([event.mouse_x-session['region'].x,event.mouse_y-session['region'].y])
                    hit=any(np.linalg.norm(mouse-np.array(p))<12 for p in screen_corners(session))
                    self.drag=('CORNER',) if hit else ('MOVE',point.copy(),session['center'].copy())
                return {'RUNNING_MODAL'}
            if event.type=='MOUSEMOVE' and self.drag:
                kind=self.drag[0]
                if kind=='RECT':
                    session['rect_end']=point;session['area'].tag_redraw()
                elif kind=='MOVE':
                    session['center']=self.drag[2]+point-self.drag[1];sync_fields()
                elif kind=='CORNER':
                    q=point-session['center'];c,s=math.cos(session['angle']),math.sin(session['angle'])
                    local=q@np.array([[c,-s],[s,c]])
                    local[0]-=session['shear']*local[1]
                    h,w=session['patch'].shape[:2]
                    session['stretch']=np.clip(np.abs(local)/np.array([w,h])*2/session['scale'],.01,100.)
                    sync_fields()
                elif kind=='S':
                    session['scale']=max(.01,min(100.,self.drag[3]*np.linalg.norm(point-self.drag[2])/max(1.,np.linalg.norm(self.drag[1]-self.drag[2]))));sync_fields()
                else:
                    a=self.drag[1]-self.drag[2];b=point-self.drag[2]
                    session['angle']=self.drag[4]+math.atan2(b[1],b[0])-math.atan2(a[1],a[0]);sync_fields()
                return {'RUNNING_MODAL'}
            if event.type=='LEFTMOUSE' and event.value=='RELEASE' and self.drag and self.drag[0] in {'RECT','MOVE','CORNER'}:
                if self.drag[0]=='RECT':select_rectangle(self.drag[1],point)
                self.drag=None
                checkpoint()
                if session['dirty']:render()
                return {'RUNNING_MODAL'}
            if inside and event.type in {'LEFTMOUSE','RIGHTMOUSE'}:
                return {'RUNNING_MODAL'}
        except (ValueError,RuntimeError,ReferenceError) as exc:
            self.report({'ERROR'},str(exc));cancel();return {'CANCELLED'}
        return {'PASS_THROUGH'}


class WGAMEUV_OT_canvas_import(bpy.types.Operator,ImportHelper):
    bl_idname='wgame_uv.canvas_import'
    bl_label='导入素材并摆放'
    filter_glob:bpy.props.StringProperty(default='*.png;*.tga;*.tif;*.tiff',options={'HIDDEN'})
    def invoke(self,context,event):
        return decals.invoke_image_selector(self,context)
    def execute(self,context):
        try:
            source=decals.load_source_image(self.filepath)
            context.scene.wgame_uv_tools.decal_source=source
            result=bpy.ops.wgame_uv.canvas_transform('INVOKE_DEFAULT',action='IMPORT')
            return {'FINISHED'} if 'RUNNING_MODAL' in result else {'CANCELLED'}
        except (ValueError,RuntimeError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}


class WGAMEUV_OT_canvas_finish(bpy.types.Operator):
    bl_idname='wgame_uv.canvas_finish'
    bl_label='二维编辑操作'
    action:bpy.props.EnumProperty(items=[('COMMIT','确认合并',''),('CANCEL','取消',''),('UNDO','撤销二维编辑',''),('REDO','重做二维编辑','')])
    def execute(self,context):
        try:
            if self.action=='COMMIT':commit()
            elif self.action=='CANCEL':cancel()
            elif self.action=='UNDO':undo()
            else:redo(context)
        except (ValueError,RuntimeError,ReferenceError) as exc:
            self.report({'ERROR'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


class WGAMEUV_OT_canvas_history(bpy.types.Operator):
    bl_idname='wgame_uv.canvas_history'
    bl_label='二维画布撤销 / 重做'
    redo_action:bpy.props.BoolProperty(default=False)

    @classmethod
    def poll(cls, context):
        if not context.area or context.area.type != 'IMAGE_EDITOR':
            return False
        # When in PAINT mode, user is painting brush strokes with Blender's native brush tool.
        # NEVER intercept Ctrl+Z in PAINT mode so Blender's native brush undo can work!
        space = getattr(context, 'space_data', None)
        if not space or space.mode == 'PAINT':
            return False
        settings = getattr(context.scene, 'wgame_uv_tools', None)
        if not settings or settings.decal_workflow != 'CANVAS' or settings.ui_tab_uv != 'DECAL':
            return False
        if _session:
            return True
        image = space.image
        if not image or image != settings.decal_target:
            return False
        # Only intercept Ctrl+Z if canvas editor actually has valid undo/redo for THIS image
        has_valid_undo = bool(_history and _history[-1][0] == image and digest(decals.pixels(image)) == _history[-1][2])
        has_valid_redo = bool(_redo and _redo[-1][0] == image and digest(decals.pixels(image)) == _redo[-1][2])
        has_valid_floating = bool(_floating_redo and _floating_redo.get('target') == image and digest(decals.pixels(image)) == _floating_redo.get('original_digest'))
        return has_valid_undo or has_valid_redo or has_valid_floating

    def execute(self,context):
        try:
            redo(context) if self.redo_action else undo()
        except (ValueError,RuntimeError,ReferenceError) as exc:
            self.report({'WARNING'},str(exc));return {'CANCELLED'}
        return {'FINISHED'}


def register_keymaps():
    config=bpy.context.window_manager.keyconfigs.addon
    if config:
        keymap=config.keymaps.new(name='Image Generic',space_type='IMAGE_EDITOR')
        for shift in (False,True):
            item=keymap.keymap_items.new('wgame_uv.canvas_history','Z','PRESS',ctrl=True,shift=shift)
            item.properties.redo_action=shift
            _keymaps.append((keymap,item))


def unregister_keymaps():
    for keymap,item in _keymaps:
        keymap.keymap_items.remove(item)
    _keymaps.clear()


def workflow_changed(self,context):
    cancel();decals.cancel()


def draw(layout,context):
    from . import decal_drop
    decal_drop.draw_hint(layout,context)
    s=context.scene.wgame_uv_tools
    box=layout.box();box.label(text='二维画布 · 不需要面遮罩')
    box.prop(s,'canvas_show_uv',text='显示 DetailUV 岛轮廓')
    row=box.row(align=True)
    row.operator('wgame_uv.canvas_finish',text='撤销 · Ctrl+Z').action='UNDO'
    row.operator('wgame_uv.canvas_finish',text='重做 · Ctrl+Shift+Z').action='REDO'
    if _session:
        box.label(text='画布：'+_session['target'].name)
        if _session['phase']=='RECT':
            box.label(text='在左侧图片上拖出矩形选区')
        else:
            box.label(text='拖动移动 · 拖角点拉伸 · R 旋转')
            for name in ('canvas_x','canvas_y','canvas_scale','canvas_scale_x','canvas_scale_y','canvas_shear','canvas_angle'):box.prop(s,name)
            fbox = box.box()
            fbox.label(text='当前浮动孤岛边缘羽化')
            frow = fbox.row(align=True)
            frow.prop(s, 'feather_radius', text='半径')
            frow.prop(s, 'feather_mode', text='')
            op = fbox.operator('wgame_uv.feather_islands', text='羽化当前浮动孤岛', icon='SMOOTHCURVE')
            op.radius = s.feather_radius
            op.mode = s.feather_mode
            op.falloff = s.feather_falloff
            box.operator('wgame_uv.canvas_finish',text='确认合并 · Enter',icon='CHECKMARK').action='COMMIT'
        box.operator('wgame_uv.canvas_finish',text='取消 · Esc').action='CANCEL'
    else:
        box.operator('wgame_uv.enter_paint',text='打开细节画布',icon='TPAINT_HLT')
        area=image_editor(context)
        image=area.spaces.active.image if area else None
        box.label(text='目标画布：'+image.name if image else '目标画布：请先在图像编辑器选择图片')
        box.operator('wgame_uv.canvas_import',icon='FILE_IMAGE')
        if s.decal_source:
            box.label(text='素材：'+s.decal_source.name)
            box.operator('wgame_uv.canvas_transform',text='摆放已加载素材').action='IMPORT'
        row=box.row(align=True)
        row.operator('wgame_uv.canvas_transform',text='点击选中孤岛',icon='RESTRICT_SELECT_OFF').action='PICK'
        row.operator('wgame_uv.canvas_transform',text='框选移动').action='MOVE'
        row.operator('wgame_uv.canvas_transform',text='框选复制').action='COPY'
        fbox = box.box()
        fbox.label(text='颜色孤岛选择与羽化')
        frow = fbox.row(align=True)
        frow.prop(s, 'feather_radius', text='半径')
        frow.prop(s, 'feather_mode', text='')
        from . import feather
        sel = feather.get_selection()
        if sel:
            info = fbox.box()
            info.label(text=f"已选中孤岛：{sel['count']} 像素 ({sel['width']}×{sel['height']})", icon='CHECKMARK')
            row_op = fbox.row(align=True)
            row_op.scale_y = 1.25
            op_f = row_op.operator('wgame_uv.feather_selected', text='羽化选中的孤岛', icon='SMOOTHCURVE')
            op_f.radius = s.feather_radius
            op_f.mode = s.feather_mode
            op_f.falloff = s.feather_falloff
            row_op.operator('wgame_uv.clear_selection', text='取消选中', icon='X')
        else:
            row_sel = fbox.row(align=True)
            row_sel.scale_y = 1.2
            row_sel.operator('wgame_uv.select_island', text='点击选中颜色孤岛', icon='RESTRICT_SELECT_OFF')
            fbox.label(text='点击上方按钮在图像上选中孤岛，再单独羽化', icon='INFO')
            op = fbox.operator('wgame_uv.feather_islands', text='羽化贴图全部孤岛', icon='SMOOTHCURVE')
            op.radius = s.feather_radius
            op.mode = s.feather_mode
            op.falloff = s.feather_falloff
        box.operator('wgame_uv.decal',text='保存细节图',icon='FILE_TICK').action='SAVE'
        box.label(text='点击选中孤岛：基于 Alpha 连通分量整岛拾取')
    if s.canvas_message:box.label(text=s.canvas_message)
    box.label(text='浮动预览未写入原图；确认后再保存')
