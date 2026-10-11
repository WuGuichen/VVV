"""A closed side band needs a longitudinal UV cut; a flat annulus does not."""
from pathlib import Path
import math,sys
import bpy,bmesh
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
editor=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D');editor.type='IMAGE_EDITOR';editor.ui_type='UV'
settings=bpy.context.scene.wgame_uv_tools;settings.face_unwrap_mode='KEEP'


def run_band(name,ring,levels,triangles=False,manual=False,flat=False):
    if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    n=len(ring)
    vertices=[(x*(z if flat else 1),y*(z if flat else 1),0 if flat else z) for z in levels for x,y in ring]
    faces=[]
    for row in range(len(levels)-1):
        for i in range(n):
            a=row*n+i;b=row*n+(i+1)%n;c=b+n;d=a+n
            faces.extend(((a,b,c),(a,c,d)) if triangles else ((a,b,c,d),))
    count=len(faces)
    if not flat:faces.extend((tuple(reversed(range(n))),tuple(range((len(levels)-1)*n,len(vertices)))))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    palette=mesh.uv_layers.new(name='PaletteUV');uv=mesh.uv_layers.new(name='DetailUV');mesh.uv_layers.active=uv
    for i,value in enumerate(palette.data):value.uv=(.2+i*.001,.7)
    for face in mesh.polygons:
        face.select=face.index<count
        for i in face.loop_indices:uv.data[i].uv=(-1,-1) if face.select else (2+i*.001,3)
    marked={tuple(sorted((row*n+1,(row+1)*n+1))) for row in range(len(levels)-1)} if manual else set()
    for e in mesh.edges:e.use_seam=tuple(sorted(e.vertices)) in marked
    before_palette=[tuple(v.uv) for v in palette.data]
    before_other=[tuple(uv.data[i].uv) for f in mesh.polygons if not f.select for i in f.loop_indices]
    before_seams=[e.use_seam for e in mesh.edges]
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.context.scene.tool_settings.mesh_select_mode=(False,False,True)
    bpy.ops.mesh.select_all(action='DESELECT')
    selection=bmesh.from_edit_mesh(mesh);selection.faces.ensure_lookup_table()
    for face in selection.faces:face.select_set(face.index<count)
    bmesh.update_edit_mesh(mesh)
    with bpy.context.temp_override(area=editor):
        assert bpy.ops.wgame_uv.face_authoring(action='CREATE')=={'FINISHED'},name
        assert not addon.islands.native_operation(bpy.context,obj,list(range(count)),'OVERLAP',settings),name+' overlaps'
    bm=bmesh.from_edit_mesh(mesh);bm.faces.ensure_lookup_table();bm.verts.index_update();layer=bm.loops.layers.uv.active
    cuts=set();selected={bm.faces[i] for i in range(count)}
    for e in bm.edges:
        adjacent=[f for f in e.link_faces if f in selected]
        if len(adjacent)!=2:continue
        a,b=({l.vert.index:l[layer].uv.copy() for l in f.loops if l.vert in e.verts} for f in adjacent)
        if any((a[v]-b[v]).length>1e-5 for v in a):cuts.add(tuple(sorted(v.index for v in e.verts)))
    if flat:assert not cuts, 'A planar annulus must retain its hole'
    else:
        assert cuts, name+' has no longitudinal UV opening'
        if manual:assert cuts==marked,(name,cuts,marked)
        # Developable straight bands should fill one rectangle, up to rotation.
        coords=[[l[layer].uv.copy() for l in f.loops] for f in selected]
        edges=[b-a for poly in coords for a,b in zip(poly,poly[1:]+poly[:1])]
        points=[p for poly in coords for p in poly]
        boxes=[]
        for edge in edges:
            if edge.length_squared<1e-12:continue
            axis=edge.normalized();across=Vector((-axis.y,axis.x))
            boxes.append((max(p.dot(axis) for p in points)-min(p.dot(axis) for p in points))*(max(p.dot(across) for p in points)-min(p.dot(across) for p in points)))
        box=min(boxes)
        fill=sum(addon.islands.area(poly) for poly in coords)/box
        assert fill>.99,(name,'twisted/nonrectangular band',fill)
    _,_,islands=addon.islands.collect(obj,True)
    assert sum(bool(set(ids)&set(range(count))) for ids,_ in islands)==1,name+' split into multiple islands'
    bpy.ops.object.mode_set(mode='OBJECT')
    assert before_palette==[tuple(v.uv) for v in mesh.uv_layers['PaletteUV'].data]
    assert before_other==[tuple(mesh.uv_layers['DetailUV'].data[i].uv) for f in mesh.polygons if f.index>=count for i in f.loop_indices]
    assert before_seams==[e.use_seam for e in mesh.edges]
    assert [f.select for f in mesh.polygons]==[i<count for i in range(len(mesh.polygons))]
    print('PASS_BAND',name,'cuts',len(cuts),flush=True)


rectangle=[(-.4,-.3),(.4,-.3),(.4,.3),(-.4,.3)]
circle=[(math.cos(i*math.tau/12),math.sin(i*math.tau/12)) for i in range(12)]
run_band('Rectangular post',rectangle,[0,4])
run_band('Triangulated post',rectangle,[0,1.3,4],True)
run_band('Cylindrical band',circle,[0,1,3])
run_band('Authored seam',rectangle,[0,1,4],True,True)
run_band('Flat ring',circle,[1,2],flat=True)
assert not any(s.name.startswith('WGame UV Temporary') for s in bpy.data.scenes)
print('PASS ring side unwrap, single chart without overlap, seam preservation and flat annulus regression')
