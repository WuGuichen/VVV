"""Real-window scope Undo/Redo regression (not a --background test).

blender --factory-startup --enable-event-simulate --python this_file -- ADDON LABEL
Default ADDON is the adjacent __init__.py. LABEL may contain multi and right.
The scene is synthetic; no production Blend is loaded, saved or changed.
Outputs use WGAME_SCOPE_TEST_OUTPUT, or the system temporary directory.
"""
import bpy, bmesh, importlib.util, json, os, sys, tempfile, traceback
from pathlib import Path
from mathutils import Quaternion, Vector

SCRIPT_DIR = Path(__file__).parent
ROOT = Path(os.environ.get('WGAME_SCOPE_TEST_OUTPUT', tempfile.gettempdir()))
ROOT.mkdir(parents=True, exist_ok=True)
ARGS = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
SOURCE = Path(ARGS[0]) if ARGS else SCRIPT_DIR/'__init__.py'
LABEL = ARGS[1] if len(ARGS)>1 else 'scope-ui-edit'
LOG = ROOT/(LABEL+'.json')
spec = importlib.util.spec_from_file_location('wgame_rigging_scope_test', SOURCE)
addon = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = addon
spec.loader.exec_module(addon)
addon.register()
bpy.context.preferences.view.show_splash = False
if hasattr(bpy.context.preferences.filepaths, 'use_save_on_exit'):
    bpy.context.preferences.filepaths.use_save_on_exit = False
RESULT = {'label':LABEL, 'states':[]}

def context():
    w = bpy.context.window_manager.windows[0]
    a = next(a for a in w.screen.areas if a.type == 'VIEW_3D')
    r = next(r for r in a.regions if r.type == 'WINDOW')
    return w, a, r

def snap(tag):
    s = bpy.context.scene.wgame_rigging_assistant
    state = {'tag': tag, 'mode': bpy.context.mode,
        'active': bpy.context.active_object.name if bpy.context.active_object else None,
        'index': s.unrigged_component_index, 'json': s.unrigged_components_json,
        'objects': {}}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        if o.mode == 'EDIT':
            bm = bmesh.from_edit_mesh(o.data)
            vs = list(bm.verts)
        else: vs = o.data.vertices
        state['objects'][o.name] = {'mask': sorted(addon._mesh_vertex_group_ids(o, addon.CUSTOM_BIND_MASK_GROUP)),
            'selected': [v.index for v in vs if v.select], 'hidden': [v.index for v in vs if v.hide],
            'authority': bool(o.get(addon.CUSTOM_BIND_MASK_AUTHORITY_KEY)),
            'groups': [g.name for g in o.vertex_groups], 'mode': o.mode}
    RESULT['states'].append(state)
    LOG.write_text(json.dumps(RESULT, ensure_ascii=False, indent=2), encoding='utf-8')

def setup():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for name, offset in [('ProbeA', -2), ('ProbeB', 2)]:
        me = bpy.data.meshes.new(name)
        me.from_pydata([(offset-1,-1,0),(offset+1,-1,0),(offset,1,0),
            (offset-1,3,0),(offset+1,3,0),(offset,5,0)], [], [(0,1,2),(3,4,5)])
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
    o = bpy.data.objects['ProbeA']
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    s = bpy.context.scene.wgame_rigging_assistant
    s.unrigged_components_json = json.dumps(addon._scan_unrigged_components(bpy.context), ensure_ascii=False, separators=(',', ':'))
    s.unrigged_component_scan_fingerprint = addon._unrigged_topology_fingerprint(bpy.context)
    s.unrigged_component_index = 0
    addon._ensure_custom_mask_groups(bpy.context, s)
    if any(name in LABEL for name in ['reclassify', 'finalize', 'reopen', 'hide', 'show']):
        components = addon._unrigged_components(s)
        addon._set_custom_mask_vertices(o, components[0]['vertex_indices'], True)
        addon._sync_component_scopes_from_mask(bpy.context, s, finalized='reopen' in LABEL)
    if 'show' in LABEL:
        addon._set_marked_custom_hidden(bpy.context, s, True)
    if 'nomask' in LABEL:
        for obj in bpy.context.scene.objects:
            group=obj.vertex_groups.get(addon.CUSTOM_BIND_MASK_GROUP)
            if group: obj.vertex_groups.remove(group)
            if addon.CUSTOM_BIND_MASK_AUTHORITY_KEY in obj: del obj[addon.CUSTOM_BIND_MASK_AUTHORITY_KEY]
    bpy.ops.ed.undo_push(message='Probe global baseline')
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bm = bmesh.from_edit_mesh(o.data)
    bm.verts.ensure_lookup_table()
    bm.verts[1].select = True
    bmesh.update_edit_mesh(o.data)
    w,a,r = context()
    a.spaces.active.region_3d.view_rotation = Quaternion((1,0,0,0))
    a.spaces.active.region_3d.view_location = Vector((0,2,0))
    a.spaces.active.region_3d.view_distance = 12
    with bpy.context.temp_override(window=w, area=a, region=r):
        bpy.ops.ed.undo_push(message='Probe baseline')
    if 'object' in LABEL:
        bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.ed.undo_push(message='Object baseline')
    snap('before')

from bpy_extras import view3d_utils

EXPECTED = {}

def remember(key):
    EXPECTED[key] = addon._capture_component_pick_state(bpy.context)
    snap(key)

def check(key):
    actual = addon._capture_component_pick_state(bpy.context)
    if actual != EXPECTED[key]:
        snap('FAILED_' + key)
        differences = [k for k in actual if actual[k] != EXPECTED[key][k]]
        raise AssertionError((key, differences, actual, EXPECTED[key]))
    RESULT.setdefault('passed', []).append(key)
    snap('match_' + key)

def invoke():
    w,a,r = context()
    with bpy.context.temp_override(window=w, area=a, region=r):
        result = bpy.ops.wgame_rigging.pick_component_in_view('INVOKE_DEFAULT', True)
        assert result == {'RUNNING_MODAL'}, result

def key(kind, ctrl=False, shift=False):
    w,a,r = context()
    x,y = r.x + r.width//2, r.y + r.height//2
    w.event_simulate(type=kind, value='PRESS', x=x, y=y, ctrl=ctrl, shift=shift)
    w.event_simulate(type=kind, value='RELEASE', x=x, y=y, ctrl=ctrl, shift=shift)

def click(name, ids=(0,1,2), shift=False):
    w,a,r = context()
    o = bpy.data.objects[name]
    center = sum((o.data.vertices[i].co for i in ids), Vector()) / len(ids)
    p = view3d_utils.location_3d_to_region_2d(r, a.spaces.active.region_3d, o.matrix_world @ center)
    assert p is not None
    x,y = int(r.x+p.x), int(r.y+p.y)
    w.event_simulate(type='MOUSEMOVE', value='NOTHING', x=x, y=y)
    w.event_simulate(type='LEFTMOUSE', value='PRESS', x=x, y=y, shift=shift)
    w.event_simulate(type='LEFTMOUSE', value='RELEASE', x=x, y=y, shift=shift)

def multi_setup():
    setup()
    if 'multi' in LABEL:
        bpy.ops.object.mode_set(mode='OBJECT')
        bpy.data.objects['ProbeB'].select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
    s=bpy.context.scene.wgame_rigging_assistant
    # Nontrivial workflow state catches incomplete Scene restoration.
    s.wgame_base_skin_report = 'Before picking'
    s.wgame_custom_binding_report = 'Keep this report'
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.ed.undo_push(message='UI global baseline')
    bpy.ops.object.mode_set(mode='EDIT')
    with bpy.context.temp_override(window=context()[0], area=context()[1], region=context()[2]):
        bpy.ops.ed.undo_push(message='UI edit baseline')
    remember('initial')

def picked(key_name, name):
    ids = addon._mesh_vertex_group_ids(bpy.data.objects[name], addon.CUSTOM_BIND_MASK_GROUP)
    assert {0,1,2} <= ids, (key_name,name,ids)
    remember(key_name)

def clean_check():
    assert not any(t.name.startswith('.wgame_component_scope_undo') for t in bpy.data.texts)
    assert not bpy.context.window.modal_operators
    RESULT['completed'] = True

STEPS = [lambda: key('ESC'), multi_setup, invoke,
    lambda: click('ProbeA'), lambda: picked('first', 'ProbeA'),
    lambda: click('ProbeB'), lambda: picked('second', 'ProbeB')]
for _ in range(3):
    STEPS += [lambda: key('Z',ctrl=True), lambda: check('first'),
              lambda: key('Z',ctrl=True,shift=True), lambda: check('second')]
STEPS += [lambda: click('ProbeA',shift=True), lambda: remember('toggle'),
          lambda: key('Z',ctrl=True), lambda: check('second'),
          lambda: key('Z',ctrl=True,shift=True), lambda: check('toggle'),
          lambda: key('H'), lambda: remember('hidden'),
          lambda: key('Z',ctrl=True), lambda: check('toggle'),
          lambda: key('Z',ctrl=True,shift=True), lambda: check('hidden'),
          lambda: key('RIGHTMOUSE' if 'right' in LABEL else 'ESC'),
          lambda: check('hidden'),
          lambda: bpy.ops.screen.screenshot(filepath=str(ROOT/(LABEL+'.png'))),
          lambda: key('Z',ctrl=True), lambda: check('initial'),
          lambda: key('Z',ctrl=True,shift=True), lambda: check('hidden')]
for _ in range(2):
    STEPS += [lambda: key('Z',ctrl=True), lambda: check('initial'),
              lambda: key('Z',ctrl=True,shift=True), lambda: check('hidden')]
# Exiting an unchanged second picker session must not add an Undo step.
STEPS += [invoke, lambda: key('ESC'), lambda: key('Z',ctrl=True),
          lambda: check('initial'), lambda: key('Z',ctrl=True,shift=True),
          lambda: check('hidden'), clean_check]

def tick():
    try:
        if STEPS:
            STEPS.pop(0)()
            return .25
    except Exception:
        RESULT['error'] = traceback.format_exc()
    LOG.write_text(json.dumps(RESULT,ensure_ascii=False,indent=2),encoding='utf-8')
    bpy.ops.wm.quit_blender()
    return None
bpy.app.timers.register(tick, first_interval=2)
