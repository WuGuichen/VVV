import pathlib
import sys
import math
import bpy
import bmesh
import numpy as np
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
n=16
vertices=[(0,0,0)]+[(2*math.cos(i*2*math.pi/n),math.sin(i*2*math.pi/n),0) for i in range(n)]
faces=[(0,i+1,(i+1)%n+1) for i in range(n)]
mesh=bpy.data.meshes.new('Ellipse');mesh.from_pydata(vertices,[],faces)
detail=mesh.uv_layers.new(name='DetailUV');mesh.uv_layers.new(name='PaletteUV');mesh.uv_layers.active_index=0
for face in mesh.polygons:
    for i in face.loop_indices:
        v=mesh.vertices[mesh.loops[i].vertex_index].co
        detail.data[i].uv=(.5+v.x*.2,.5+v.y*.3)
obj=bpy.data.objects.new('Ellipse',mesh);bpy.context.collection.objects.link(obj);obj.select_set(True);bpy.context.view_layer.objects.active=obj
group=obj.vertex_groups.new(name='TestWeight');group.add([1],.7,'REPLACE')
palette=[tuple(l.uv) for l in mesh.uv_layers['PaletteUV'].data]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.context.scene.tool_settings.use_uv_select_sync=True
assert bpy.ops.wgame_uv.round_uv()=={'FINISHED'}
bm=bmesh.from_edit_mesh(mesh);uv=bm.loops.layers.uv.active
assert all(np.allclose(tuple(v.co),vertices[v.index]) for v in bm.verts)
boundary=np.array([tuple(l[uv].uv) for f in bm.faces for l in f.loops if l.vert.index!=0])
assert np.std(np.linalg.norm(boundary-[.5,.5],axis=1))<1e-6
uv_before=[[tuple(l[uv].uv) for l in f.loops] for f in bm.faces]
assert bpy.ops.wgame_uv.round_mesh()=={'FINISHED'}
bm=bmesh.from_edit_mesh(mesh);uv=bm.loops.layers.uv.active
assert [[tuple(l[uv].uv) for l in f.loops] for f in bm.faces]==uv_before
boundary=np.array([tuple(v.co) for v in bm.verts if v.index!=0])
assert np.std(np.linalg.norm(boundary,axis=1))<1e-6
assert all(abs(v.co.z)<1e-8 for v in bm.verts)
bpy.ops.object.mode_set(mode='OBJECT')
assert len(mesh.vertices)==17 and len(mesh.polygons)==16
assert abs(group.weight(1)-.7)<1e-6
assert [tuple(l.uv) for l in mesh.uv_layers['PaletteUV'].data]==palette
print('PASS: UV circle with interior preserves geometry; model disk circle preserves UV/topology/weights; planar result')
from WGameUVTools.rounding import round_points
points=np.array([[1.2,0,0],[-1.2,0,0],[0,1,0],[0,-1,0],[0,0,.9],[0,0,-.9]])
rounded=round_points(points,'SPHERE',1.)
assert np.std(np.linalg.norm(rounded,axis=1))<1e-8
assert np.array_equal(round_points(points,'SPHERE',0.),points)
print('PASS: optional sphere fit and zero strength')
