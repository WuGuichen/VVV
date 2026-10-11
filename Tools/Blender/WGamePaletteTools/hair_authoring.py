"""Hair authoring controls embedded in the existing WGame palette add-on."""
import bpy
import json
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import time
from mathutils import Vector
from . import hair_flow as hf

_draw_handle=None
_arrow_scene=None
_guide_draw_handle=None
_root_draw_handle=None
_lines=[]
_preview={}
DEFAULTS={'primaryWidth':0.32,'primaryStrength':0.45,'secondaryWidth':0.5,'secondaryStrength':0.16,
          'primaryShift':0.08,'secondaryShift':-0.16,'rootDarkening':0.18,'rimStrength':0.12,'aoStrength':0.5}


def root_vertices(obj, evaluated=False):
    """Read live Edit Mesh data so selection, edits and Undo need no overlay cache."""
    if not obj or obj.type!='MESH':return []
    if obj.mode=='EDIT':
        import bmesh
        bm=bmesh.from_edit_mesh(obj.data)
        bm.verts.index_update()
        layer=bm.verts.layers.bool.get(hf.ROOT)
        if layer is None:return []
        return [(v.index,v.co.copy(),v.select,v.hide) for v in bm.verts if v[layer]]
    mesh=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).data if evaluated else obj.data
    roots=mesh.attributes.get(hf.ROOT)
    if not roots or roots.domain!='POINT' or roots.data_type!='BOOLEAN':return []
    return [(v.index,v.co.copy(),v.select,v.hide) for v in mesh.vertices if roots.data[v.index].value]


def redraw_root_marks(context):
    for window in context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                if window.scene==context.scene:area.spaces.active.overlay.show_overlays=True
                area.tag_redraw()


def draw_root_marks():
    """Screen-sized green dots identify stored roots even when vertices are unselected."""
    import gpu
    from bpy_extras.view3d_utils import location_3d_to_region_2d
    from gpu_extras.batch import batch_for_shader
    context=bpy.context;space=context.space_data
    if not space or space.type!='VIEW_3D' or not space.overlay.show_overlays:return
    if context.scene.get('wgame_baked_preview',False) or not getattr(context.scene,'wgame_hair_show_roots',True):return
    obj=guide_target(context)
    if not obj or not obj.visible_get(view_layer=context.view_layer,viewport=space):return
    points=[];region=context.region;scale=context.preferences.system.ui_scale
    for _,co,_,hidden in root_vertices(obj,evaluated=True):
        if hidden:continue
        point=location_3d_to_region_2d(region,space.region_3d,obj.matrix_world@co)
        if point is not None and 0<=point.x<region.width and 0<=point.y<region.height:points.append(point)
    if not points:return
    shader=gpu.shader.from_builtin('POINT_UNIFORM_COLOR')
    old_blend=gpu.state.blend_get();old_depth=gpu.state.depth_test_get()
    try:
        gpu.state.blend_set('ALPHA');gpu.state.depth_test_set('NONE')
        # A dark border keeps the dots legible over light hair and orange selection.
        batch=batch_for_shader(shader,'POINTS',{'pos':points})
        for size,color in ((12*scale,(.015,.025,.02,1.)),(8*scale,(.2,1.,.45,1.))):
            shader.bind();shader.uniform_float('color',color);shader.uniform_float('size',size)
            batch.draw(shader)
    finally:
        gpu.state.depth_test_set(old_depth);gpu.state.blend_set(old_blend)


def guide_paths(obj):
    if not obj or obj.type!='CURVE' or not obj.get(hf.GUIDE):return []
    paths=[]
    for spline in obj.data.splines:
        points=hf.guide_points(spline)
        if len(points)>=2:paths.append([obj.matrix_world@p for p in points])
    return paths


def draw_guide_labels():
    """Readable root/tip labels using the same ordered points as flow generation."""
    import math
    import blf
    import gpu
    from bpy_extras.view3d_utils import location_3d_to_region_2d
    from gpu_extras.batch import batch_for_shader
    context=bpy.context;space=context.space_data
    if not space or space.type!='VIEW_3D' or not space.overlay.show_overlays or context.scene.get('wgame_baked_preview',False):return
    guides=[o for o in context.selected_objects if o.type=='CURVE' and o.get(hf.GUIDE) and o.visible_get(view_layer=context.view_layer,viewport=space)]
    if not guides:return
    region=context.region;view=space.region_3d;scale=context.preferences.system.ui_scale
    shader=gpu.shader.from_builtin('UNIFORM_COLOR')
    old_blend=gpu.state.blend_get();old_depth=gpu.state.depth_test_get();old_width=gpu.state.line_width_get()
    def draw(kind,coords,color):
        if not coords:return
        shader.bind();shader.uniform_float('color',color)
        batch_for_shader(shader,kind,{'pos':coords}).draw(shader)
    def visible(p):return p is not None and 0<=p.x<region.width and 0<=p.y<region.height
    def endpoint(point,text,color,above):
        if not visible(point):return
        radius=7*scale
        ring=[point+Vector((math.cos(i*math.tau/32),math.sin(i*math.tau/32)))*radius for i in range(33)]
        draw('LINE_STRIP',ring,color)
        blf.size(0,15*scale);width,height=blf.dimensions(0,text)
        x=max(4,min(region.width-width-10,point.x+12*scale))
        y=max(6,min(region.height-height-8,point.y+(10 if above else -24)*scale))
        pad=4*scale
        corners=[(x-pad,y-pad),(x+width+pad,y-pad),(x+width+pad,y+height+pad),(x-pad,y+height+pad)]
        draw('TRIS',[corners[i] for i in (0,1,2,0,2,3)],(.02,.025,.035,.86))
        blf.color(0,*color);blf.position(0,x,y,0);blf.draw(0,text)
    try:
        gpu.state.blend_set('ALPHA');gpu.state.depth_test_set('NONE');gpu.state.line_width_set(2*scale)
        for obj in guides:
            paths=guide_paths(obj)
            for index,path in enumerate(paths):
                points=[location_3d_to_region_2d(region,view,p) for p in path]
                arrows=[]
                for a,b in zip(points,points[1:]):
                    if a is None or b is None:continue
                    delta=b-a;length=delta.length
                    if length<18*scale:continue
                    direction=delta/length;side=Vector((-direction.y,direction.x))
                    tip=a+delta*.62
                    if not visible(tip):continue
                    size=min(11*scale,length*.18)
                    arrows.extend([tip-direction*size*1.8,tip,tip,tip-direction*size+side*size*.5,tip,tip-direction*size-side*size*.5])
                draw('LINES',arrows,(.12,.8,1.,1.))
                suffix=' '+str(index+1) if len(paths)>1 else ''
                endpoint(points[0],'发根 · 起点'+suffix,(.2,1.,.45,1.),True)
                endpoint(points[-1],'发梢 · 终点'+suffix,(1.,.55,.16,1.),False)
    finally:
        gpu.state.line_width_set(old_width);gpu.state.depth_test_set(old_depth);gpu.state.blend_set(old_blend)


class WG_OT_HairGuideReverse(bpy.types.Operator):
    bl_idname='wgame.hair_reverse_guide'
    bl_label='反转根梢方向'
    bl_description='保持引导线形状，交换发根与发梢；修改后需重新生成发流'
    bl_options={'REGISTER','UNDO'}
    @classmethod
    def poll(cls,context):
        return not context.scene.get('wgame_baked_preview',False) and context.object is not None and context.object.type=='CURVE' and bool(context.object.get(hf.GUIDE))
    def execute(self,context):
        if not self.poll(context):return {'CANCELLED'}
        obj=context.object;mode=obj.mode;selected=list(context.selected_objects)
        if mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);context.view_layer.objects.active=obj
        try:
            bpy.ops.object.mode_set(mode='EDIT');bpy.ops.curve.select_all(action='SELECT');bpy.ops.curve.switch_direction()
        finally:
            bpy.ops.object.mode_set(mode='OBJECT')
            for other in selected:other.select_set(True)
            context.view_layer.objects.active=obj
            if mode=='EDIT':bpy.ops.object.mode_set(mode='EDIT')
        for area in context.screen.areas:area.tag_redraw()
        self.report({'INFO'},'已交换根梢；绿色为发根，橙色为发梢。选回角色重新生成发流')
        return {'FINISHED'}


def draw_directions():
    import gpu
    from gpu_extras.batch import batch_for_shader
    context=bpy.context
    if not _lines or context.scene!=_arrow_scene or context.scene.get('wgame_baked_preview',False):return
    if not context.space_data or context.space_data.type!='VIEW_3D' or not context.space_data.overlay.show_overlays:return
    shader=gpu.shader.from_builtin('UNIFORM_COLOR')
    batch=batch_for_shader(shader,'LINES',{'pos':_lines})
    depth=gpu.state.depth_test_get();width=gpu.state.line_width_get()
    try:
        gpu.state.depth_test_set('NONE');gpu.state.line_width_set(2*context.preferences.system.ui_scale)
        shader.bind();shader.uniform_float('color',(0.15,0.8,1.0,1.0));batch.draw(shader)
    finally:gpu.state.depth_test_set(depth);gpu.state.line_width_set(width)


def update_arrows(obj):
    global _draw_handle,_lines,_arrow_scene
    _arrow_scene=bpy.context.scene
    m=obj.data;flow=m.attributes.get(hf.FLOW)
    _lines=[]
    if flow:
        length=max(obj.dimensions)*0.018
        ids=[v.index for v in m.vertices if flow.data[v.index].vector.length>0.5]
        for i in ids[::max(1,len(ids)//300)]:
            a=obj.matrix_world@m.vertices[i].co
            direction=(obj.matrix_world.to_3x3()@flow.data[i].vector).normalized()
            b=a+direction*length
            side=direction.cross(Vector((0,0,1)))
            if side.length<0.01:side=direction.cross(Vector((1,0,0)))
            side.normalize()
            _lines.extend([a,b,b,b-direction*length*.3+side*length*.15,b,b-direction*length*.3-side*length*.15])
    if not bpy.app.background and not _draw_handle:_draw_handle=bpy.types.SpaceView3D.draw_handler_add(draw_directions,(),'WINDOW','POST_VIEW')


class WG_OT_HairRegion(bpy.types.Operator):
    bl_idname='wgame.hair_region'
    bl_label='发根标记'
    bl_options={'REGISTER','UNDO'}
    action:bpy.props.EnumProperty(items=[('ADD','旧头发标记',''),('REMOVE','旧头发取消',''),('ROOT','标记根部',''),('CLEAR_ROOT','取消根部',''),('INIT','旧范围初始化','')],default='ROOT',options={'HIDDEN'})

    @classmethod
    def poll(cls,context):
        return not context.scene.get('wgame_baked_preview',False) and context.object is not None and context.object.type=='MESH'

    def execute(self,context):
        if self.action not in {'ROOT','CLEAR_ROOT'}:
            self.report({'WARNING'},'头发范围现在由材质类型自动识别，请在角色画板中设置头发材质')
            return {'CANCELLED'}
        if not self.poll(context):return {'CANCELLED'}
        obj=context.object
        if obj.mode!='EDIT':
            self.report({'WARNING'},'请进入编辑模式，选中发根顶点后再操作')
            return {'CANCELLED'}
        try:
            import bmesh
            bm=bmesh.from_edit_mesh(obj.data)
            bm.verts.index_update()
            selected=[v for v in bm.verts if v.select and not v.hide]
            if not selected:
                self.report({'WARNING'},'没有选中顶点，发根标记未改变')
                return {'CANCELLED'}
            root=bm.verts.layers.bool.get(hf.ROOT)
            if self.action=='ROOT':
                flags=hf.region(obj.data)
                hair={v for face,enabled in zip(bm.faces,flags) if enabled for v in face.verts}
                selected=[v for v in selected if v in hair]
                if not selected:
                    self.report({'WARNING'},'选区不包含头发顶点，请先设置头发材质类型')
                    return {'CANCELLED'}
                if root is None:
                    # Adding custom data can invalidate existing BMVert wrappers.
                    indices=[v.index for v in selected]
                    root=bm.verts.layers.bool.new(hf.ROOT)
                    bm.verts.ensure_lookup_table()
                    selected=[bm.verts[index] for index in indices]
            changed=0
            if root is not None:
                for vertex in selected:
                    value=self.action=='ROOT'
                    if bool(vertex[root])!=value:changed+=1
                    vertex[root]=value
                bmesh.update_edit_mesh(obj.data,loop_triangles=False,destructive=False)
            total=len(root_vertices(obj))
            verb='标记' if self.action=='ROOT' else '清除'
            if hasattr(context.scene,'wgame_hair_show_roots'):context.scene.wgame_hair_show_roots=True
            if changed:
                global _lines
                _lines=[]
                context.scene['wgame_hair_flow_status']='发根已修改，请重新生成发流'
            redraw_root_marks(context)
            self.report({'INFO'},f'已{verb} {changed} 个发根顶点；当前共 {total} 个，绿色点表示已标记')
        except (ValueError,RuntimeError) as e:
            self.report({'ERROR'},str(e));return {'CANCELLED'}
        return {'FINISHED'}


class WG_OT_SelectHairRoots(bpy.types.Operator):
    bl_idname='wgame.hair_select_roots'
    bl_label='选中已标记发根'
    bl_description='选中当前角色已标记且未隐藏的发根顶点，不修改标记'
    bl_options={'REGISTER','UNDO'}
    @classmethod
    def poll(cls,context):return WG_OT_HairRegion.poll(context)
    def execute(self,context):
        if not self.poll(context):return {'CANCELLED'}
        obj=context.object
        marked={i for i,_,_,hidden in root_vertices(obj) if not hidden}
        if not marked:
            self.report({'WARNING'},'当前没有可见的手动发根标记')
            return {'CANCELLED'}
        if obj.mode!='EDIT':bpy.ops.object.mode_set(mode='EDIT')
        import bmesh
        bm=bmesh.from_edit_mesh(obj.data);bm.verts.index_update()
        for face in bm.faces:face.select_set(False)
        for edge in bm.edges:edge.select_set(False)
        for vertex in bm.verts:vertex.select_set(vertex.index in marked)
        context.scene.tool_settings.mesh_select_mode=(True,False,False)
        bmesh.update_edit_mesh(obj.data,loop_triangles=False,destructive=False)
        redraw_root_marks(context)
        return {'FINISHED'}


class WG_OT_HairFlow(bpy.types.Operator):
    bl_idname='wgame.hair_generate_flow'
    bl_label='生成发流并显示箭头'
    bl_options={'REGISTER','UNDO'}
    @classmethod
    def poll(cls,context):return not context.scene.get('wgame_baked_preview',False)

    def execute(self,context):
        global _lines
        if not self.poll(context):return {'CANCELLED'}
        _lines=[]
        context.scene['wgame_hair_flow_conflicts']='[]'
        original=context.object;mode=original.mode if original else 'OBJECT'
        restore=True;obj=None
        try:
            obj=flow_target(context)
            if mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
            rig=obj.find_armature()
            result=hf.generate(obj,rig,context.scene.objects)
            update_arrows(obj)
            for area in context.screen.areas:
                if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True;area.tag_redraw()
            message=f"发流：{result['components']} 个区域，{result['vertices']} 个顶点"
            if not result['vertices']:message='未找到头发区域，请先设置头发材质类型'
            context.scene['wgame_hair_flow_status']=message
            self.report({'INFO'} if result['vertices'] else {'WARNING'},message)
        except (ValueError,RuntimeError) as e:
            if obj and getattr(e,'vertices',None):
                if context.object and context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
                bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);context.view_layer.objects.active=obj
                bad=set(e.vertices)
                for edge in obj.data.edges:edge.select=False
                for face in obj.data.polygons:face.select=False
                for vertex in obj.data.vertices:vertex.select=vertex.index in bad
                context.scene.tool_settings.mesh_select_mode=(True,False,False)
                bpy.ops.object.mode_set(mode='EDIT');restore=False
            context.scene['wgame_hair_flow_status']='生成失败：'+str(e)
            context.scene['wgame_hair_flow_conflicts']=json.dumps(list(getattr(e,'guides',())),ensure_ascii=False)
            self.report({'ERROR'},str(e));return {'CANCELLED'}
        finally:
            if restore and original and original.mode!=mode:bpy.ops.object.mode_set(mode=mode)
        return {'FINISHED'}


def guide_target(context):
    obj=context.object
    if obj and obj.type=='MESH':return obj
    if obj and obj.get(hf.GUIDE):
        target=context.scene.objects.get(obj.get('wgame_hair_target',''))
        if target and target.type=='MESH':return target
    return None


class WG_OT_SelectHairRegion(bpy.types.Operator):
    bl_idname='wgame.hair_select_region'
    bl_label='选中头发区域'
    bl_description='检查由哑光头发／柔光头发材质自动识别的面，不改变材质或范围'
    @classmethod
    def poll(cls,context):return not context.scene.get('wgame_baked_preview',False)
    def execute(self,context):
        if not self.poll(context):return {'CANCELLED'}
        try:obj=flow_target(context)
        except ValueError as error:self.report({'ERROR'},str(error));return {'CANCELLED'}
        if context.object and context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.select_set(True);context.view_layer.objects.active=obj
        flags=hf.region(obj.data)
        for vertex in obj.data.vertices:vertex.select=False
        for edge in obj.data.edges:edge.select=False
        for face,flag in zip(obj.data.polygons,flags):face.select=flag
        context.scene.tool_settings.mesh_select_mode=(False,False,True)
        bpy.ops.object.mode_set(mode='EDIT')
        self.report({'INFO'},f'按材质识别到 {sum(flags)} 个头发面')
        return {'FINISHED'}


def flow_target(context):
    target=guide_target(context)
    if target:return target
    targets={o.get('wgame_hair_target','') for o in context.scene.objects if o.type=='CURVE' and o.get(hf.GUIDE)}
    candidates=[context.scene.objects[name] for name in sorted(targets) if name in context.scene.objects and context.scene.objects[name].type=='MESH']
    if len(candidates)==1:return candidates[0]
    if len(candidates)>1:raise ValueError('场景有多个角色的引导线，请选择一个角色网格或它的引导线')
    candidates=[]
    for obj in context.scene.objects:
        if obj.type!='MESH':continue
        try:
            if any(hf.region(obj.data)):candidates.append(obj)
        except ValueError:continue
    if len(candidates)==1:return candidates[0]
    raise ValueError('请先选择要生成发流的角色网格或引导线')


def guide_list(context):
    target=guide_target(context)
    return sorted((o for o in context.scene.objects if o.type=='CURVE' and o.get(hf.GUIDE)
                   and (target is None or o.get('wgame_hair_target')==target.name)),key=lambda o:o.name.casefold())


class WG_OT_HairGuideSelect(bpy.types.Operator):
    bl_idname='wgame.hair_select_guide'
    bl_label='切换并编辑引导线'
    bl_description='保留当前修改，切换到这条引导线并聚焦控制点'
    guide_name:bpy.props.StringProperty(options={'HIDDEN'})
    @classmethod
    def poll(cls,context):return not context.scene.get('wgame_baked_preview',False)
    def execute(self,context):
        if not self.poll(context):return {'CANCELLED'}
        obj=context.scene.objects.get(self.guide_name)
        if not obj or obj.type!='CURVE' or not obj.get(hf.GUIDE):
            self.report({'ERROR'},'引导线已删除或改名，请重新选择');return {'CANCELLED'}
        target=context.scene.objects.get(obj.get('wgame_hair_target',''))
        if not target or target.type!='MESH':
            self.report({'ERROR'},'这条引导线的目标网格不存在');return {'CANCELLED'}
        if obj.name not in context.view_layer.objects:
            self.report({'ERROR'},'引导线所在集合已排除，请先启用该集合');return {'CANCELLED'}
        focus_guide(context,obj,target)
        return {'FINISHED'}


class WG_OT_HairGuideReturn(bpy.types.Operator):
    bl_idname='wgame.hair_guide_return_mesh'
    bl_label='返回角色网格'
    bl_description='保留引导线修改，选回它所属的角色网格以生成发流'
    @classmethod
    def poll(cls,context):
        return not context.scene.get('wgame_baked_preview',False) and context.object is not None and bool(context.object.get(hf.GUIDE)) and guide_target(context) is not None
    def execute(self,context):
        if not self.poll(context):return {'CANCELLED'}
        target=guide_target(context)
        if target.name not in context.view_layer.objects:
            self.report({'ERROR'},'目标网格所在集合已排除，请先启用该集合');return {'CANCELLED'}
        if context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.select_all(action='DESELECT');target.hide_set(False);target.select_set(True);context.view_layer.objects.active=target
        for area in context.screen.areas:
            if area.type=='VIEW_3D' and area.spaces.active.local_view:target.local_view_set(area.spaces.active,True)
        return {'FINISHED'}


def guide_placement(context, source):
    editing=source.mode=='EDIT'
    if editing:bpy.ops.object.mode_set(mode='OBJECT')
    try:return _guide_placement(context,source,editing)
    finally:
        if editing:bpy.ops.object.mode_set(mode='EDIT')


def _guide_placement(context, source, editing):
    """Honor an explicit hair selection or a nearby cursor, otherwise use the head."""
    mesh=source.data
    hair={v for face,flag in zip(mesh.polygons,hf.region(mesh)) if flag for v in face.vertices}
    if not hair:raise ValueError('请先将区域设为头发材质，再添加引导线')
    height=max(v.co.z for v in mesh.vertices)-min(v.co.z for v in mesh.vertices)
    height=max(height,.001)
    selected=[v for v in hair if mesh.vertices[v].select] if editing else []
    cursor=context.scene.cursor.location
    nearest=min(hair,key=lambda v:(source.matrix_world@mesh.vertices[v].co-cursor).length_squared)
    if selected:
        root=mesh.vertices[max(selected,key=lambda v:(mesh.vertices[v].co.z,-v))].co.copy()
    elif (source.matrix_world@mesh.vertices[nearest].co-cursor).length<=max(source.dimensions)*.08:
        root=source.matrix_world.inverted()@cursor
    else:
        center=hf.head_position(source,source.find_armature())
        if center is None:center=sum((mesh.vertices[v].co for v in hair),Vector())/len(hair)
        center.z=max(mesh.vertices[v].co.z for v in hair)-height*.025
        aim=source.matrix_world@center
        area=next((a for a in context.screen.areas if a.type=='VIEW_3D'),None) if context.screen else None
        if area:aim+=area.spaces.active.region_3d.view_rotation@Vector((0,0,max(source.dimensions)*.05))
        root=mesh.vertices[min(hair,key=lambda v:(source.matrix_world@mesh.vertices[v].co-aim).length_squared)].co.copy()
    matrix=source.matrix_world.copy();matrix.translation=source.matrix_world@root
    return matrix,height*.15


def focus_guide(context, obj, source):
    if context.object and context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);context.view_layer.objects.active=obj
    obj.hide_set(False);obj.hide_viewport=False;obj.show_in_front=True;obj.display_type='WIRE'
    areas=[a for a in context.screen.areas if a.type=='VIEW_3D'] if context.screen else []
    for area in areas:
        space=area.spaces.active;space.overlay.show_overlays=True;space.overlay.show_extras=True
        if space.local_view:obj.local_view_set(space,True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.curve.select_all(action='SELECT')
    for area in areas:
        region=next((r for r in area.regions if r.type=='WINDOW'),None)
        if region:
            with context.temp_override(area=area,region=region):bpy.ops.view3d.view_selected(use_all_regions=False)
            area.spaces.active.region_3d.view_distance=max(area.spaces.active.region_3d.view_distance,max(source.dimensions)*.3)
            area.tag_redraw()


class WG_OT_HairGuide(bpy.types.Operator):
    bl_idname='wgame.hair_add_guide'
    bl_label='添加根部到发梢引导线'
    bl_options={'REGISTER','UNDO'}
    @classmethod
    def poll(cls,context):
        return not context.scene.get('wgame_baked_preview',False) and guide_target(context) is not None
    def execute(self,context):
        if context.scene.get('wgame_baked_preview',False):return {'CANCELLED'}
        source=guide_target(context)
        if source is None:self.report({'ERROR'},'请先选择角色网格');return {'CANCELLED'}
        try:matrix,length=guide_placement(context,source)
        except ValueError as error:self.report({'ERROR'},str(error));return {'CANCELLED'}
        if context.object and context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        curve=bpy.data.curves.new('WGameHairGuide','CURVE');curve.dimensions='3D'
        spline=curve.splines.new('POLY');spline.points.add(2)
        for i,p in enumerate(spline.points):p.co=(0,0,-length*i/2,1)
        obj=bpy.data.objects.new('HairGuide_RootToTip',curve)
        context.collection.objects.link(obj);obj.matrix_world=matrix
        obj[hf.GUIDE]=True;obj.hide_render=True;obj.show_in_front=True
        obj['wgame_hair_target']=source.name
        focus_guide(context,obj,source)
        self.report({'INFO'},'已在头发附近创建并聚焦；第一个点是发根，调整后选回角色生成发流')
        return {'FINISHED'}


class WG_OT_HairPreview(bpy.types.Operator):
    bl_idname='wgame.hair_preview'
    bl_label='更新头发预览／恢复源材质'
    def execute(self,context):
        obj=context.object
        if not obj or obj.type!='MESH':return {'CANCELLED'}
        mode=obj.mode
        if mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
        try:
            if obj.name in _preview:
                for slot,original in zip(obj.material_slots,_preview.pop(obj.name)):
                    temp=slot.material;slot.material=original
                    if temp!=original and temp.users==0:bpy.data.materials.remove(temp)
                return {'FINISHED'}
            hf.generate(obj,obj.find_armature(),context.scene.objects)
            mask=hf.attribute(obj.data,'WGameHairPreviewMask','FLOAT','FACE')
            for item,flag in zip(mask.data,hf.region(obj.data)):item.value=float(flag)
            originals=[s.material for s in obj.material_slots];_preview[obj.name]=originals
            for slot in obj.material_slots:
                original_name=slot.material.name
                mat=slot.material.copy();slot.material=mat;tree=mat.node_tree
                mat['wgame_hair_preview_source']=original_name
                output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
                source=output.inputs['Surface'].links[0].from_node
                if source.type!='BSDF_PRINCIPLED':raise ValueError('预览要求 Principled 材质')
                base=source.inputs['Base Color']
                flow=tree.nodes.new('ShaderNodeAttribute');flow.attribute_name=hf.FLOW
                # Blender preview uses the same widths/shifts and source flow;
                # Cycles' hair BSDF is an approximation of the Unity light lobes.
                lobes=[]
                for prefix in ('primary','secondary'):
                    hair=tree.nodes.new('ShaderNodeBsdfHair')
                    hair.component='Reflection'
                    if base.is_linked:tree.links.new(base.links[0].from_socket,hair.inputs['Color'])
                    else:hair.inputs['Color'].default_value=base.default_value
                    hair.inputs['RoughnessU'].default_value=getattr(context.scene,'wgame_hair_'+prefix+'Width')
                    hair.inputs['RoughnessV'].default_value=getattr(context.scene,'wgame_hair_'+prefix+'Width')
                    hair.inputs['Offset'].default_value=getattr(context.scene,'wgame_hair_'+prefix+'Shift')
                    tree.links.new(flow.outputs['Vector'],hair.inputs['Tangent']);lobes.append(hair)
                mixed=tree.nodes.new('ShaderNodeMixShader')
                primary=context.scene.wgame_hair_primaryStrength;secondary=context.scene.wgame_hair_secondaryStrength
                mixed.inputs[0].default_value=secondary/max(1e-6,primary+secondary)
                tree.links.new(lobes[0].outputs[0],mixed.inputs[1]);tree.links.new(lobes[1].outputs[0],mixed.inputs[2])
                mask_node=tree.nodes.new('ShaderNodeAttribute');mask_node.attribute_name=mask.name
                final=tree.nodes.new('ShaderNodeMixShader')
                tree.links.new(mask_node.outputs['Fac'],final.inputs[0]);tree.links.new(source.outputs[0],final.inputs[1]);tree.links.new(mixed.outputs[0],final.inputs[2]);tree.links.new(final.outputs[0],output.inputs['Surface'])
            self.report({'INFO'},'头发预览已开启；再次点击恢复源材质后导出')
        except Exception as e:
            for slot,original in zip(obj.material_slots,_preview.pop(obj.name,[])):slot.material=original
            self.report({'ERROR'},str(e));return {'CANCELLED'}
        finally:
            if mode=='EDIT':bpy.ops.object.mode_set(mode='EDIT')
        return {'FINISHED'}


class WG_PT_Hair(bpy.types.Panel):
    bl_label='头发与烘焙设置'
    bl_idname='WG_PT_hair_baked_surface'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='WGame'
    def draw(self,context):
        layout=self.layout
        from . import baked_preview
        baked_preview.draw_buttons(layout,context)
        if baked_preview.active(context.window):
            return
        layout.label(text='头发范围按材质类型自动识别')
        layout.label(text='02 哑光头发 / 03 柔光头发')
        layout.operator('wgame.hair_select_region')
        box=layout.box()
        obj=guide_target(context);roots=root_vertices(obj)
        box.label(text=f'手动发根：{len(roots)} 个 · 选中 {sum(selected for _,_,selected,_ in roots)} 个')
        box.prop(context.scene,'wgame_hair_show_roots',text='显示发根标记')
        box.label(text='绿色点：已标记发根（可透视）')
        box.label(text='只标发束起点附近，不要选整片头发')
        if not roots:box.label(text='未手动标记时，生成发流会尝试自动找根')
        if not obj or obj.mode!='EDIT':box.label(text='进入编辑模式，选中发根顶点后标记')
        row=box.row(align=True)
        row.operator('wgame.hair_region',text='标记根部').action='ROOT'
        row.operator('wgame.hair_region',text='清除选中根部').action='CLEAR_ROOT'
        box.operator('wgame.hair_select_roots')
        layout.operator('wgame.hair_add_guide')
        guides=guide_list(context)
        if guides:
            box=layout.box();box.label(text=f'引导线（{len(guides)}）· 点击切换编辑')
            conflicts=json.loads(context.scene.get('wgame_hair_flow_conflicts','[]'))
            for guide in guides:
                active=guide==context.object
                row=box.row(align=True)
                row.operator('wgame.hair_select_guide',text=guide.name,icon='ERROR' if guide.name in conflicts else 'EDITMODE_HLT' if active else 'CURVE_DATA',depress=active).guide_name=guide.name
            if context.object and context.object.get(hf.GUIDE):
                box.prop(context.object,'name',text='名称')
                box.operator('wgame.hair_guide_return_mesh',icon='LOOP_BACK')
        if context.object and context.object.get(hf.GUIDE):
            layout.label(text='绿色：发根 → 橙色：发梢')
            layout.operator('wgame.hair_reverse_guide')
        layout.operator('wgame.hair_generate_flow')
        if context.scene.get('wgame_hair_flow_status'):
            status=context.scene['wgame_hair_flow_status']
            layout.label(text=status,icon='ERROR' if status.startswith('生成失败') else 'INFO')
        layout.operator('wgame.hair_preview')
        layout.label(text='新角色默认烘焙；已有色板角色可主动转换')
        layout.prop(context.scene,'wgame_baked_surface_enabled')
        layout.prop(context.scene,'wgame_bake_texture_size')
        from . import baked_workflow
        baked_workflow.draw_controls(layout,context)
        header,advanced=layout.panel('wgame_hair_advanced',default_closed=True)
        header.label(text='高级设置')
        if advanced:
            advanced.label(text='通常使用默认光照参数即可')
            for name in DEFAULTS:advanced.prop(context.scene,'wgame_hair_'+name)


from .baked_workflow import WG_OT_BakePreview, WG_OT_ViewBakeCandidate, WG_OT_BakedPublish


CLASSES=(WG_OT_HairRegion,WG_OT_SelectHairRoots,WG_OT_SelectHairRegion,WG_OT_HairFlow,WG_OT_HairGuide,WG_OT_HairGuideSelect,WG_OT_HairGuideReturn,WG_OT_HairGuideReverse,WG_OT_HairPreview,WG_OT_BakePreview,WG_OT_ViewBakeCandidate,WG_OT_BakedPublish,WG_PT_Hair)

def register():
    from . import baked_preview
    baked_preview.register()
    bpy.types.Scene.wgame_hair_show_roots=bpy.props.BoolProperty(name='显示发根标记',default=True,description='用绿色点显示手动发根；支持透视，隐藏顶点不显示',update=lambda self,context:redraw_root_marks(context))
    labels=['主亮带宽度','主亮带强度','副亮带宽度','副亮带强度','主亮带偏移','副亮带偏移','根部暗化','逆光柔光','局部遮蔽强度']
    for (key,value),label in zip(DEFAULTS.items(),labels):
        minimum=-1 if key.endswith('Shift') else .02 if key.endswith('Width') else 0
        maximum=2 if key.endswith('Strength') and key!='aoStrength' else 1
        setattr(bpy.types.Scene,'wgame_hair_'+key,bpy.props.FloatProperty(name=label,default=value,min=minimum,max=maximum))
    bpy.types.Scene.wgame_baked_surface_enabled=bpy.props.BoolProperty(name='将历史色板角色转为烘焙发布',description='新角色和已有烘焙角色始终烘焙；此选项只用于主动转换历史色板角色',default=False)
    bpy.types.Scene.wgame_bake_texture_size=bpy.props.EnumProperty(name='输出贴图尺寸',items=[('AUTO','自动保留细节','最多 4096'),('512','512 × 512','明确允许缩小细节'),('1024','1024 × 1024',''),('2048','2048 × 2048',''),('4096','4096 × 4096','')],default='AUTO')
    for cls in CLASSES:bpy.utils.register_class(cls)
    global _guide_draw_handle,_root_draw_handle
    if not bpy.app.background and _guide_draw_handle is None:
        _guide_draw_handle=bpy.types.SpaceView3D.draw_handler_add(draw_guide_labels,(),'WINDOW','POST_PIXEL')
    if not bpy.app.background and _root_draw_handle is None:
        _root_draw_handle=bpy.types.SpaceView3D.draw_handler_add(draw_root_marks,(),'WINDOW','POST_PIXEL')


def unregister():
    from . import baked_preview
    baked_preview.unregister()
    global _draw_handle,_guide_draw_handle,_root_draw_handle
    if _draw_handle:bpy.types.SpaceView3D.draw_handler_remove(_draw_handle,'WINDOW');_draw_handle=None
    if _guide_draw_handle:bpy.types.SpaceView3D.draw_handler_remove(_guide_draw_handle,'WINDOW');_guide_draw_handle=None
    if _root_draw_handle:bpy.types.SpaceView3D.draw_handler_remove(_root_draw_handle,'WINDOW');_root_draw_handle=None
    for obj_name,originals in list(_preview.items()):
        obj=bpy.data.objects.get(obj_name)
        if obj:
            for slot,original in zip(obj.material_slots,originals):slot.material=original
    _preview.clear()
    for cls in reversed(CLASSES):
        try:bpy.utils.unregister_class(cls)
        except RuntimeError:pass
    for name in ['wgame_hair_show_roots','wgame_baked_surface_enabled','wgame_bake_texture_size']+['wgame_hair_'+k for k in DEFAULTS]:
        if hasattr(bpy.types.Scene,name):delattr(bpy.types.Scene,name)
