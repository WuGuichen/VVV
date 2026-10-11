# Implementation references

- UniV, Oxicid, GPL-3.0-or-later: https://github.com/Oxicid/UniV/blob/main/operators/checker.py
  Read on 2026-09-14. Reference for the viewport-only Geometry Nodes Set Material
  modifier approach, including edit-mode preview and `show_render = False`.
  This implementation is written for WGame; no UniV modules, icons, textures,
  shortcuts or proprietary Pro implementation are bundled.
- Blender RNA inspected directly in Blender 5.2.0 LTS:
  `SpaceUVEditor.show_stretch`, `display_stretch_type`, `stretch_opacity`.
  https://docs.blender.org/api/current/bpy.types.SpaceUVEditor.html
- Blender native shader nodes: UV Map, Checker Texture, Emission.
  Blender native geometry nodes: Group Input, Set Material, Group Output.

WGame UV Tools is distributed under GPL-3.0-or-later; see LICENSE.

1.1.0 island tools use Blender native `uv.select_overlap`, `uv.minimize_stretch`
and `uv.pack_islands` (RNA verified on Blender 5.2.0):
https://docs.blender.org/api/current/bpy.ops.uv.html
Blender 5.2 UV selection uses BMLoop.uv_select_vert/uv_select_edge rather than
legacy BMLoopUV.select fields. Island continuity uses shared mesh edges with
matching endpoint UV coordinates. No external packing or overlap algorithm is bundled.
