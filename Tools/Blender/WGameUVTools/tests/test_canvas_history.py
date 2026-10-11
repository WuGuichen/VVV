import runpy
import pathlib
import bpy
import numpy as np
runpy.run_path(str(pathlib.Path(__file__).with_name('test_canvas_editor.py')))
import WGameUVTools as addon
c=addon.canvas_editor;d=addon.decals
c.clear_session()
obj=bpy.context.object
image=next(n.image for n in obj.active_material.node_tree.nodes if n.type=='TEX_IMAGE')
source=bpy.data.images['Sticker']
area=next(a for a in bpy.context.screen.areas if a.type=='IMAGE_EDITOR')
before=d.pixels(image)
with bpy.context.temp_override(area=area):
    session=c.begin(bpy.context,'IMPORT',source)
    initial=c.transform_state(session)
    session['center']+=np.array([10,12]);c.checkpoint()
    moved=c.transform_state(session)
    session['angle']=.7;c.checkpoint()
    c.undo();assert c.transform_state(session)==moved
    c.undo();assert c.transform_state(session)==initial
    c.redo();assert c.transform_state(session)==moved
    # A new gesture branches away from the undone rotation.
    session['transform_redo'].clear();session['angle']=.3;c.checkpoint()
    c.commit();after=d.pixels(image)
    c.undo();assert np.array_equal(d.pixels(image),before)
    c.redo();assert np.array_equal(d.pixels(image),after)
    c.undo()
    c.begin(bpy.context,'IMPORT',source);c._session['center']+=20;c.commit()
    assert not c._redo
    c.undo();assert np.array_equal(d.pixels(image),before)
    c.begin(bpy.context,'IMPORT',source)
    c.undo();assert c._session is None and c._floating_redo
    # Reopen the undone floating import through its real invoke path.
    c.resume_floating(bpy.context)
    assert c._session is not None
    assert np.array_equal(d.pixels(image),before)
    c.cancel()
print('PASS: floating move/rotate undo and redo; committed pixels undo/redo; new commit clears redo; import undo and resume')
