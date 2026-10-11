import pathlib
import sys
import bpy
import numpy as np
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]))
import WGameUVTools as addon
addon.register()
c=addon.canvas_editor;d=addon.decals
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_plane_add()
obj=bpy.context.object;obj.data.uv_layers.active.name='DetailUV'
target=bpy.data.images.new('Canvas',width=8,height=8,alpha=True)
source=bpy.data.images.new('Sticker',width=2,height=2,alpha=True)
base=np.zeros((8,8,4),np.float32);base[1:3,1:3]=[1,0,0,1];base[7,7]=[0,0,1,1]
d.write_pixels(target,base);d.write_pixels(source,np.full((2,2,4),[0,1,0,1],np.float32))
mat=bpy.data.materials.new('CanvasMaterial');mat.use_nodes=True
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=target
uv=mat.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map='DetailUV'
mat.node_tree.links.new(uv.outputs['UV'],tex.inputs['Vector']);obj.data.materials.append(mat)
area=next(a for a in bpy.context.screen.areas if a.type=='VIEW_3D');area.type='IMAGE_EDITOR';area.spaces.active.mode='PAINT';area.spaces.active.image=target
original_uv=[tuple(l.uv) for l in obj.data.uv_layers.active.data]
with bpy.context.temp_override(area=area):
    session=c.begin(bpy.context,'MOVE')
    c.select_rectangle(np.array([1,1]),np.array([3,3]))
    session['center']=np.array([5.,5.]);c.render()
    assert np.array_equal(d.pixels(target),base)
    assert tex.image==session['preview'] and area.spaces.active.image==session['display']
    preview=d.pixels(session['preview'])
    assert np.all(preview[1:3,1:3]==0) and np.allclose(preview[4:6,4:6],[1,0,0,1])
    c.cancel()
    assert tex.image==target and area.spaces.active.image==target and area.spaces.active.mode=='PAINT'
    assert np.array_equal(d.pixels(target),base)
    session=c.begin(bpy.context,'MOVE');c.select_rectangle(np.array([1,1]),np.array([3,3]));session['center']=np.array([5.,5.]);c.commit()
    moved=d.pixels(target)
    assert np.all(moved[1:3,1:3]==0) and np.allclose(moved[4:6,4:6],[1,0,0,1])
    c.undo();assert np.array_equal(d.pixels(target),base)
    session=c.begin(bpy.context,'COPY');c.select_rectangle(np.array([1,1]),np.array([3,3]));session['center']=np.array([5.,5.]);c.commit()
    copied=d.pixels(target)
    assert np.allclose(copied[1:3,1:3],[1,0,0,1]) and np.allclose(copied[4:6,4:6],[1,0,0,1])
    session=c.begin(bpy.context,'IMPORT',source);session['center']=np.array([4.,2.]);session['scale']=2.;session['angle']=np.pi/2;c.commit()
    assert np.any(d.pixels(target)[:,:,1]>0)
    c.undo();assert np.array_equal(d.pixels(target),copied)
    c.undo();assert np.array_equal(d.pixels(target),base)
    # A concurrent edit to the ORIGINAL cannot be overwritten by a stale floating layer.
    session=c.begin(bpy.context,'IMPORT',source)
    changed=base.copy();changed[0,0]=[1,1,1,1];d.write_pixels(target,changed)
    try:c.commit();raise AssertionError('stale write must fail')
    except ValueError:pass
    c.cancel();assert np.array_equal(d.pixels(target),changed)
    session=c.begin(bpy.context,'IMPORT',source)
    assert len(session['uv_segments'])==8
    session['stretch']=np.array([2.,3.]);session['shear']=.5
    bounds=c.corners(session)
    assert np.allclose(np.ptp(bounds,axis=0),[7.,6.])
    c.render();assert np.array_equal(d.pixels(target),changed)
    addon.hot_reload.reload_now()
    assert addon.canvas_editor._session is None and tex.image==target
    assert np.array_equal(d.pixels(target),changed)
assert original_uv==[tuple(l.uv) for l in obj.data.uv_layers.active.data]
assert not any(i.get(c.TAG) for i in bpy.data.images)
large=bpy.data.images.new('LargeCanvas',width=2048,height=2048,alpha=True)
d.write_pixels(large,np.zeros((2048,2048,4),np.float32));tex.image=large
area.spaces.active.image=large
with bpy.context.temp_override(area=area):
    session=c.begin(bpy.context,'IMPORT',source)
    assert tuple(session['preview'].size)==(512,512)
    assert tuple(session['display'].size)==(2048,2048)
    c.commit()
    full=d.pixels(large)
    assert full.shape==(2048,2048,4)
    assert np.count_nonzero(full[:,:,1])==4
print('PASS: 2K final commit uses original 2-pixel source, independent of reduced model preview')
print('PASS: floating preview preserves original; move clears source; copy preserves source; scale/rotate import; multi-step undo; concurrent edit protected; bindings restored; UV unchanged')
