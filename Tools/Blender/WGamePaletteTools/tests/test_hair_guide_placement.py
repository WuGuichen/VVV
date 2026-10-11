"""Guide placement/visibility regression checks in a disposable Blender scene."""
import importlib.util
import importlib
from pathlib import Path
import sys
import unittest
import bpy
import bmesh
from mathutils import Vector

folder=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('guide_test_palette',folder/'__init__.py',submodule_search_locations=[str(folder)])
package=importlib.util.module_from_spec(spec);sys.modules[spec.name]=package;spec.loader.exec_module(package)
hair=importlib.import_module('guide_test_palette.hair_authoring')
bpy.utils.register_class(hair.WG_OT_HairGuide)
bpy.utils.register_class(hair.WG_OT_HairGuideReverse)
bpy.utils.register_class(hair.WG_OT_HairGuideSelect)
bpy.utils.register_class(hair.WG_OT_HairGuideReturn)
bpy.utils.register_class(hair.WG_OT_HairFlow)
bpy.utils.register_class(hair.WG_OT_HairRegion)
bpy.utils.register_class(hair.WG_OT_SelectHairRegion)
bpy.utils.register_class(hair.WG_OT_SelectHairRoots)


class GuideTests(unittest.TestCase):
    def select_vertices(self,indices):
        if self.obj.mode!='EDIT':bpy.ops.object.mode_set(mode='EDIT')
        bm=bmesh.from_edit_mesh(self.obj.data);bm.verts.ensure_lookup_table()
        for face in bm.faces:face.select_set(False)
        for edge in bm.edges:edge.select_set(False)
        for vertex in bm.verts:vertex.select_set(vertex.index in indices)
        bmesh.update_edit_mesh(self.obj.data)

    def test_root_marks_are_readable_in_edit_and_object_mode_without_changing_selection(self):
        self.select_vertices({3,4})
        self.assertEqual(bpy.ops.wgame.hair_region(action='ROOT'),{'FINISHED'})
        self.assertEqual({i for i,_,_,_ in hair.root_vertices(self.obj)},{3,4})
        self.assertEqual(bpy.context.mode,'EDIT_MESH')
        self.select_vertices(set())
        self.assertTrue(all(not selected for _,_,selected,_ in hair.root_vertices(self.obj)))
        bpy.ops.object.mode_set(mode='OBJECT')
        self.assertEqual({i for i,_,_,_ in hair.root_vertices(self.obj)},{3,4})
        self.assertEqual(self.obj.data.attributes[hair.hf.ROOT].data_type,'BOOLEAN')

    def test_clear_only_removes_selected_roots_and_invalidates_old_arrows(self):
        self.select_vertices({3,4});bpy.ops.wgame.hair_region(action='ROOT')
        hair._lines=[Vector((0,0,0)),Vector((0,0,1))]
        self.select_vertices({3});bpy.ops.wgame.hair_region(action='CLEAR_ROOT')
        self.assertEqual({i for i,_,_,_ in hair.root_vertices(self.obj)},{4})
        self.assertEqual(hair._lines,[])
        self.assertIn('重新生成',bpy.context.scene['wgame_hair_flow_status'])

    def test_empty_or_nonhair_selection_does_not_create_roots(self):
        self.select_vertices(set())
        self.assertEqual(bpy.ops.wgame.hair_region(action='ROOT'),{'CANCELLED'})
        self.select_vertices({0,1})
        self.assertEqual(bpy.ops.wgame.hair_region(action='ROOT'),{'CANCELLED'})
        self.assertEqual(hair.root_vertices(self.obj),[])

    def test_mark_skips_skin_but_preserves_selected_hair(self):
        self.select_vertices({0,3})
        self.assertEqual(bpy.ops.wgame.hair_region(action='ROOT'),{'FINISHED'})
        self.assertEqual({i for i,_,_,_ in hair.root_vertices(self.obj)},{3})

    def test_select_existing_roots_does_not_change_markers(self):
        self.select_vertices({3,4});bpy.ops.wgame.hair_region(action='ROOT')
        self.select_vertices({0});bpy.ops.wgame.hair_select_roots()
        bm=bmesh.from_edit_mesh(self.obj.data)
        self.assertEqual({v.index for v in bm.verts if v.select},{3,4})
        self.assertEqual({i for i,_,_,_ in hair.root_vertices(self.obj)},{3,4})

    def test_object_mode_cannot_silently_mark_stale_vertex_selection(self):
        self.assertEqual(bpy.ops.wgame.hair_region(action='ROOT'),{'CANCELLED'})
        self.assertEqual(hair.root_vertices(self.obj),[])

    def test_preview_cannot_modify_root_data(self):
        self.select_vertices({3})
        bpy.context.scene['wgame_baked_preview']=True
        self.assertFalse(bpy.ops.wgame.hair_region.poll())
        self.assertFalse(bpy.ops.wgame.hair_select_roots.poll())

    def test_painted_material_is_authority_even_with_a_stale_manual_mask(self):
        import json
        catalog=folder.parents[2]/'ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json'
        package.STATE.catalog=json.loads(catalog.read_text(encoding='utf-8-sig'))
        # Identical palette color, different material type: only the type counts.
        package._apply_color_profile_to_polygons(self.obj,[0],65535,0)
        package._apply_color_profile_to_polygons(self.obj,[1],65535,2)
        self.assertEqual(hair.hf.region(self.obj.data),[False,True])
        bpy.ops.object.mode_set(mode='EDIT')
        package._apply_color_profile_to_polygons(self.obj,[1],65535,4)
        self.assertEqual(hair.hf.region(self.obj.data),[False,False])
        package._apply_color_profile_to_polygons(self.obj,[1],65535,3)
        self.assertEqual(hair.hf.region(self.obj.data),[False,True])

    def test_shared_vertices_do_not_promote_adjacent_skin_faces_to_hair(self):
        mesh=self.obj.data;mesh.clear_geometry();mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2),(0,2,3)]);mesh.update()
        profile=mesh.attributes.get('SurfaceProfile') or mesh.attributes.new(name='SurfaceProfile',type='INT',domain='FACE')
        profile.data[0].value=0;profile.data[1].value=2
        self.assertEqual(bpy.ops.wgame.hair_select_region(),{'FINISHED'})
        bm=bmesh.from_edit_mesh(mesh)
        self.assertEqual([f.select for f in bm.faces],[False,True])
        self.assertEqual(hair.hf.region(mesh),[False,True])

    def test_legacy_manual_mark_command_cannot_override_material_or_mark_roots(self):
        before=hair.hf.region(self.obj.data)
        self.assertEqual(bpy.ops.wgame.hair_region(action='ADD'),{'CANCELLED'})
        self.assertEqual(hair.hf.region(self.obj.data),before)
        self.assertIsNone(self.obj.data.attributes.get(hair.hf.ROOT))

    def setUp(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mesh=bpy.data.meshes.new('GuideTest')
        mesh.from_pydata([(-.1,0,0),(.1,0,0),(0,.1,0),(-.05,-.1,1.5),(.05,-.1,1.5),(0,0,1.7)],[],[(0,1,2),(3,4,5)]);mesh.update()
        self.obj=bpy.data.objects.new('GuideTest',mesh);bpy.context.collection.objects.link(self.obj)
        self.obj.select_set(True);bpy.context.view_layer.objects.active=self.obj
        profile=mesh.attributes.new(name='SurfaceProfile',type='INT',domain='FACE');profile.data[1].value=2
        mask=mesh.attributes.new(name=hair.hf.MASK,type='BOOLEAN',domain='FACE');mask.data[0].value=True
        bpy.context.view_layer.update()

    def test_origin_cursor_places_guide_at_hair_and_enters_edit_mode(self):
        self.assertEqual(bpy.ops.wgame.hair_add_guide(),{'FINISHED'})
        guide=bpy.context.object
        self.assertEqual(bpy.context.mode,'EDIT_CURVE')
        self.assertGreater(guide.matrix_world.translation.z,1.4)
        self.assertEqual(guide['wgame_hair_target'],self.obj.name)
        self.assertTrue(guide.show_in_front);self.assertTrue(guide.hide_render)
        self.assertFalse(guide.hide_get())
        self.assertEqual(guide.display_type,'WIRE')
        for area in bpy.context.screen.areas:
            if area.type=='VIEW_3D':self.assertTrue(area.spaces.active.overlay.show_overlays)

    def test_nearby_cursor_is_honored(self):
        bpy.context.scene.cursor.location=(.02,-.08,1.57)
        matrix,_=hair.guide_placement(bpy.context,self.obj)
        self.assertLess((matrix.translation-bpy.context.scene.cursor.location).length,1e-6)

    def test_explicit_hair_selection_takes_precedence(self):
        bpy.ops.object.mode_set(mode='EDIT');bm=bmesh.from_edit_mesh(self.obj.data);bm.verts.ensure_lookup_table()
        for v in bm.verts:v.select=False
        bm.verts[3].select=True;bmesh.update_edit_mesh(self.obj.data)
        matrix,_=hair.guide_placement(bpy.context,self.obj)
        self.assertLess((matrix.translation-Vector((-.05,-.1,1.5))).length,1e-6)

    def test_transformed_character_and_repeated_add_keep_the_target(self):
        self.obj.location=(2,3,0);self.obj.scale=(2,2,2);bpy.context.view_layer.update()
        bpy.ops.wgame.hair_add_guide()
        first=bpy.context.object
        self.assertGreater(first.matrix_world.translation.z,2.8)
        bpy.ops.wgame.hair_add_guide()
        self.assertEqual(bpy.context.object['wgame_hair_target'],self.obj.name)
        self.assertNotEqual(first,bpy.context.object)

    def test_preview_and_nonhair_mesh_are_rejected(self):
        bpy.context.scene['wgame_baked_preview']=True
        self.assertFalse(bpy.ops.wgame.hair_add_guide.poll())
        del bpy.context.scene['wgame_baked_preview']
        for item in self.obj.data.attributes['SurfaceProfile'].data:item.value=0
        with self.assertRaisesRegex(ValueError,'头发材质'):hair.guide_placement(bpy.context,self.obj)

    def test_endpoint_order_matches_flow_and_follows_edits(self):
        bpy.ops.wgame.hair_add_guide();guide=bpy.context.object
        before=hair.guide_paths(guide)[0]
        bpy.ops.curve.select_all(action='DESELECT');guide.data.splines[0].points[-1].select=True
        bpy.ops.transform.translate(value=(.03,.02,0))
        after=hair.guide_paths(guide)[0]
        self.assertLess((after[0]-before[0]).length,1e-6)
        self.assertGreater((after[-1]-before[-1]).length,.02)
        segments=hair.hf.guide_segments(self.obj,[guide])
        self.assertLess((self.obj.matrix_world@segments[0][0]-after[0]).length,1e-6)
        self.assertLess((self.obj.matrix_world@segments[-1][1]-after[-1]).length,1e-6)

    def test_reverse_swaps_endpoints_without_changing_path_shape_or_other_guides(self):
        bpy.ops.wgame.hair_add_guide();first=bpy.context.object
        first_path=[p[:] for p in hair.guide_paths(first)[0]]
        bpy.ops.wgame.hair_add_guide();second=bpy.context.object
        old_path=[p[:] for p in hair.guide_paths(second)[0]]
        bpy.ops.object.mode_set(mode='OBJECT');first.select_set(True);second.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
        self.assertEqual(bpy.ops.wgame.hair_reverse_guide(),{'FINISHED'})
        self.assertEqual(bpy.context.mode,'EDIT_CURVE')
        self.assertEqual([p[:] for p in hair.guide_paths(first)[0]],first_path)
        self.assertEqual([p[:] for p in hair.guide_paths(second)[0]],list(reversed(old_path)))
        bpy.ops.wgame.hair_reverse_guide()
        self.assertEqual([p[:] for p in hair.guide_paths(second)[0]],old_path)

    def test_bezier_control_endpoints_use_the_same_direction_contract(self):
        bpy.ops.wgame.hair_add_guide();guide=bpy.context.object
        bpy.ops.curve.select_all(action='SELECT');bpy.ops.curve.spline_type_set(type='BEZIER')
        before=[p[:] for p in hair.guide_paths(guide)[0]]
        bpy.ops.wgame.hair_reverse_guide()
        self.assertEqual([p[:] for p in hair.guide_paths(guide)[0]],list(reversed(before)))

    def test_quick_switch_preserves_edits_and_returns_to_correct_mesh(self):
        bpy.ops.wgame.hair_add_guide();first=bpy.context.object
        bpy.ops.transform.translate(value=(.02,.01,0))
        edited=[p[:] for p in hair.guide_paths(first)[0]]
        bpy.ops.wgame.hair_add_guide();second=bpy.context.object
        count=len(bpy.context.scene.objects)
        self.assertEqual(bpy.ops.wgame.hair_select_guide(guide_name=first.name),{'FINISHED'})
        self.assertEqual(bpy.context.object,first);self.assertEqual(bpy.context.mode,'EDIT_CURVE')
        self.assertEqual([p[:] for p in hair.guide_paths(first)[0]],edited)
        first.name='刘海引导'
        self.assertIn(first,hair.guide_list(bpy.context))
        bpy.ops.wgame.hair_select_guide(guide_name=second.name)
        self.assertEqual(bpy.context.object,second)
        bpy.ops.wgame.hair_guide_return_mesh()
        self.assertEqual(bpy.context.object,self.obj);self.assertEqual(bpy.context.mode,'OBJECT')
        self.assertEqual(len(bpy.context.scene.objects),count)

    def test_list_is_scoped_to_target_and_preview_switch_is_blocked(self):
        bpy.ops.wgame.hair_add_guide();first=bpy.context.object
        bpy.ops.wgame.hair_add_guide();other=bpy.context.object
        other['wgame_hair_target']='OtherCharacter'
        bpy.ops.wgame.hair_select_guide(guide_name=first.name)
        self.assertEqual(hair.guide_list(bpy.context),[first])
        bpy.context.scene['wgame_baked_preview']=True
        self.assertFalse(bpy.ops.wgame.hair_select_guide.poll())
        self.assertFalse(bpy.ops.wgame.hair_guide_return_mesh.poll())

    def test_generate_from_guide_edit_mode_and_after_deselecting_everything(self):
        bpy.ops.wgame.hair_add_guide();guide=bpy.context.object
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertEqual(bpy.context.object,guide);self.assertEqual(bpy.context.mode,'EDIT_CURVE')
        self.assertGreater(len(hair._lines),0)
        bpy.ops.object.mode_set(mode='OBJECT');bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=None
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertIsNone(bpy.context.object)
        self.assertIn('3 个顶点',bpy.context.scene['wgame_hair_flow_status'])

    def test_no_selection_without_guides_resolves_unique_marked_mesh(self):
        roots=hair.hf.attribute(self.obj.data,hair.hf.ROOT,'BOOLEAN','POINT');roots.data[5].value=True
        bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=None
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertGreater(len(hair._lines),0)

    def test_removing_all_hair_clears_previous_vectors_and_arrows(self):
        bpy.ops.wgame.hair_add_guide();bpy.ops.wgame.hair_generate_flow();bpy.ops.wgame.hair_guide_return_mesh()
        for item in self.obj.data.attributes['SurfaceProfile'].data:item.value=0
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertEqual(hair._lines,[])
        self.assertTrue(all(v.vector.length==0 for v in self.obj.data.attributes[hair.hf.FLOW].data))
        self.assertIn('先设置头发材质',bpy.context.scene['wgame_hair_flow_status'])

    def test_multiple_targets_report_error_instead_of_guessing(self):
        bpy.ops.wgame.hair_add_guide();guide=bpy.context.object;bpy.ops.wgame.hair_generate_flow()
        bpy.ops.object.mode_set(mode='OBJECT')
        other=self.obj.copy();other.data=self.obj.data.copy();bpy.context.collection.objects.link(other)
        second=guide.copy();second.data=guide.data.copy();second['wgame_hair_target']=other.name;bpy.context.collection.objects.link(second)
        bpy.ops.object.select_all(action='DESELECT');bpy.context.view_layer.objects.active=None
        with self.assertRaisesRegex(RuntimeError,'多个角色'):bpy.ops.wgame.hair_generate_flow()
        self.assertEqual(hair._lines,[])
        self.assertIn('生成失败',bpy.context.scene['wgame_hair_flow_status'])

    def test_direction_conflict_identifies_guides_and_clears_after_correction(self):
        import json
        bpy.ops.wgame.hair_add_guide();first=bpy.context.object
        bpy.ops.wgame.hair_add_guide();second=bpy.context.object
        bpy.ops.wgame.hair_reverse_guide()
        with self.assertRaisesRegex(RuntimeError,'引导线方向冲突'):bpy.ops.wgame.hair_generate_flow()
        self.assertEqual(set(json.loads(bpy.context.scene['wgame_hair_flow_conflicts'])),{first.name,second.name})
        self.assertEqual(bpy.context.object,self.obj);self.assertEqual(bpy.context.mode,'EDIT_MESH')
        bpy.ops.wgame.hair_select_guide(guide_name=second.name);bpy.ops.wgame.hair_reverse_guide()
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertEqual(json.loads(bpy.context.scene['wgame_hair_flow_conflicts']),[])
        self.assertGreater(len(hair._lines),0)

    def test_separate_opposing_guides_do_not_false_positive_at_nearest_boundary(self):
        bpy.ops.wgame.hair_add_guide();first=bpy.context.object
        bpy.ops.wgame.hair_add_guide();second=bpy.context.object
        bpy.ops.object.mode_set(mode='OBJECT')
        first.location.y=-.01;second.location.y=.01
        for point in second.data.splines[0].points:point.co.z=-point.co.z
        bpy.context.view_layer.update()
        before=[[p[:] for p in hair.guide_paths(o)[0]] for o in (first,second)]
        self.assertEqual(bpy.ops.wgame.hair_generate_flow(),{'FINISHED'})
        self.assertEqual([[p[:] for p in hair.guide_paths(o)[0]] for o in (first,second)],before)
        self.assertGreater(len(hair._lines),0)


try:
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(GuideTests))
    if not result.wasSuccessful():raise RuntimeError('Hair guide tests failed')
finally:
    bpy.utils.unregister_class(hair.WG_OT_SelectHairRoots)
    bpy.utils.unregister_class(hair.WG_OT_SelectHairRegion)
    bpy.utils.unregister_class(hair.WG_OT_HairRegion)
    bpy.utils.unregister_class(hair.WG_OT_HairFlow)
    bpy.utils.unregister_class(hair.WG_OT_HairGuideReturn)
    bpy.utils.unregister_class(hair.WG_OT_HairGuideSelect)
    bpy.utils.unregister_class(hair.WG_OT_HairGuideReverse)
    bpy.utils.unregister_class(hair.WG_OT_HairGuide)
