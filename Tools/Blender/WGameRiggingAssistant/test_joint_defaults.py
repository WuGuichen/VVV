"""Blender background regression for geometry-aware joint guide defaults."""
import importlib.util
import math
from pathlib import Path
from types import SimpleNamespace

import bpy
from mathutils import Matrix, Vector


def main():
    spec = importlib.util.spec_from_file_location("joint_fit_test", Path(__file__).with_name("__init__.py"))
    addon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(addon)
    vertices = [(0, 0, 0), (0, 0, 1.68)]
    for sign in (-1, 1):
        for i in range(70):
            x = .13 + i * .008
            for j in range(16):
                a = j * math.tau / 16
                vertices.append((sign*x, math.cos(a)*.025, 1.43-.32*x+math.sin(a)*.025))
        for z in (.065, .085, .095, .115, .45, .47, .48, .50, .85, .86, .88):
            for j in range(20):
                a = j * math.tau / 20
                vertices.append((sign*(.075+math.cos(a)*.03), math.sin(a)*.035, z))
    mesh = bpy.data.meshes.new("UprightBody")
    mesh.from_pydata(vertices, [], [])
    obj = bpy.data.objects.new("UprightBody", mesh)
    bpy.context.collection.objects.link(obj)
    fit = addon._default_joint_guide_positions(obj)
    legacy = addon._legacy_joint_guide_positions(obj)
    assert abs(fit['ankle.l'].x-.075) < .015
    assert abs(fit['knee.l'].x-.075) < .015
    assert abs(fit['ankle.l'].x-.075) < abs(legacy['ankle.l'].x-.075)
    assert fit['shoulder.l'].z > fit['elbow.l'].z > fit['wrist.l'].z
    assert abs(fit['wrist.l'].z-(1.43-.32*fit['wrist.l'].x)) < .02
    assert fit['wrist.l'].x < fit['hand_end.l'].x < .683
    # World-space transforms must not change the relative fit.
    offset = Vector((.3, -.4, .7))
    obj.matrix_world = Matrix.Translation(offset) @ Matrix.Scale(2, 4)
    transformed = addon._default_joint_guide_positions(obj)
    for key in fit:
        assert (transformed[key] - (fit[key]*2+offset)).length < 1e-4, key
    obj.matrix_world = Matrix.Identity(4)
    marker = bpy.data.objects.new("Guide", None)
    bpy.context.collection.objects.link(marker)
    marker.location = legacy['wrist.l']
    assert addon._joint_guide_default_is_untouched(marker, legacy['wrist.l'], 1e-5)
    marker['wgame_joint_default_position'] = list(fit['wrist.l'])
    marker.location = fit['wrist.l']
    assert addon._joint_guide_default_is_untouched(marker, legacy['wrist.l'], 1e-5)
    marker.location.z += .02
    assert not addon._joint_guide_default_is_untouched(marker, legacy['wrist.l'], 1e-5)
    addon.register()
    try:
        settings = bpy.context.scene.wgame_rigging_assistant
        settings.unrigged_pose_locked = True
        addon._current_model_preflight = lambda *_args: True
        addon._scope_vertex_ids_by_object = lambda *_args: {}
        assert bpy.ops.wgame_rigging.create_joint_guides() == {'FINISHED'}
        wrist = addon._joint_guide_object('wrist.l')
        wrist.location.z += .03
        manual_position = wrist.location.copy()
        state = addon._joint_guide_state(settings)
        state['knee.l']['accepted'] = True
        addon._save_joint_guide_state(settings, state)
        knee_position = addon._joint_guide_object('knee.l').location.copy()
        assert bpy.ops.wgame_rigging.create_joint_guides() == {'FINISHED'}
        assert (wrist.location-manual_position).length < 1e-6
        assert (addon._joint_guide_object('knee.l').location-knee_position).length < 1e-6
        assert addon._joint_guide_state(settings)['knee.l']['accepted']
    finally:
        addon.unregister()
    print('WG_JOINT_DEFAULTS_OK lowered_arms straight_legs world_transform manual_preservation')


if __name__ == '__main__':
    main()
