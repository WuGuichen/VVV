"""Run in background Blender with factory startup."""
import importlib.util
from pathlib import Path
import tempfile
from types import SimpleNamespace

import bpy


def main():
    path = Path(__file__).resolve().parents[1] / '__init__.py'
    spec = importlib.util.spec_from_file_location('palette_repair_test', path)
    a = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(a)
    with tempfile.TemporaryDirectory() as directory:
        a._cache_root = lambda: Path(directory)
        a.register()
        try:
            a.load_catalog(bpy.context, prefer_server=False)
            sc = bpy.context.scene
            item = sc.wgame_character_color_profile_colors.add()
            item.category_index = 0
            item.surface_profile = '4'
            a._set_profile_item_color(item, 56986)
            mesh = bpy.data.meshes.new('RepairFixture')
            mesh.from_pydata([(0,0,0),(1,0,0),(0,1,0)], [], [(0,1,2)])
            obj = bpy.data.objects.new('RepairFixture', mesh)
            sc.collection.objects.link(obj)
            a._apply_color_profile_to_polygons(obj, [0], 56986, 4, 0)
            ctx = SimpleNamespace(scene=sc, selected_objects=[obj], object=obj)
            preserved = [tuple(v.color) for v in mesh.color_attributes['PaletteData'].data]
            for value in mesh.uv_layers['PaletteUV'].data:
                value.uv = (.3,.7)
            plan = a.palette_uv_repair_plan(ctx)
            assert len(plan)==1
            assert a.apply_palette_uv_repair(plan)==1
            assert not a._validate_mesh_object_data(ctx)[0]
            assert preserved==[tuple(v.color) for v in mesh.color_attributes['PaletteData'].data]
            assert not a.palette_uv_repair_plan(ctx)
            for value in mesh.uv_layers['PaletteUV'].data:
                value.uv=(.3,.7)
            mesh.attributes['ColorProfileSlot'].data[0].value=99
            assert not a.palette_uv_repair_plan(ctx), 'unconfirmed category must not be guessed'
            assert a._validate_mesh_object_data(ctx)[0], 'export validation still blocks unresolved data'
            print('WG_PALETTE_UV_REPAIR_OK safe_recovery idempotence preserved_channels ambiguity_blocks')
        finally:
            a.unregister()


if __name__=='__main__':
    main()
