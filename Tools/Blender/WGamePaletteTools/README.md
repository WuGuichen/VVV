# WGame Color Palette + Surface Profiles (Blender 5.2)

Repository-authoritative Blender add-on for the final RGB565 Color Palette v3 contract.
The Blender panel is Simplified-Chinese-first; stable schema names, Profile IDs,
RGB565, Hex and mesh attribute identifiers remain in English for data inspection.
Install the deployment copy with:

```powershell
Tools/Blender/WGamePaletteTools/install.ps1 -BlenderVersion 5.2
```

“烘焙并预览”运行时显示实际日志对应的阶段、图集排布尝试次数、已用时间和距上次日志更新的时间，并提供“打开烘焙日志”。阶段序号不代表时间百分比；没有新日志不等于任务卡死。只有后台成功退出且候选校验通过才显示完成。进度读取不会修改制作源或发布候选。

The normal paint flow first selects a color from the current character's curated
Color Profile, then selects one of the 16 frozen Surface Profiles. The Character
Color Profile is an authoring-only categorized shortlist of global `colorIndex`
values. Each entry has a stable `categoryIndex`, and mesh faces carry the same
value in `ColorProfileSlot`, so changing one Profile entry updates every face in
that authoring category. It does not restore the old runtime Profile-row or
runtime recolor behavior. Its versioned
JSON contract is `mx.characterColorProfile.v1`, records the global Catalog hash,
and can be embedded in the Blend plus loaded/saved from a character package.
The shared JSON Schema lives at
`ArtSource/Common/Palettes/WGameSurfacePalette/character-color-profile.schema.json`.

The Profile configuration foldout maps an arbitrary Blender color or exact Hex
to the nearest unique RGB565 address, then adds or updates a named Profile
category. Updating a category explicitly remaps its classified faces to the new
color and Surface Profile; removing a category is blocked while faces still
reference it. Only the current
64-color page is represented by Blender UI items; the compact RGB table stays in
memory. Every address is bidirectionally encoded as
`(R5<<11)|(G6<<5)|B5`. The raw 65K address browser is technical storage and is
collapsed by default. The Catalog remains generator-owned and read-only.

The Character Color Profile belongs to the current Blender Scene. Category
updates and removal reference checks cover every mesh in that Scene carrying
`ColorProfileSlot`, including unselected, hidden, and collection-excluded meshes.
Meshes without that attribute and meshes in other Scenes are outside this scope.
A classified backup in the same Scene still references that Scene's Profile;
use a separate Scene with independent mesh data for independent character or
backup palettes. Selection-based painting retains its selected-object scope.

Mesh contract:

Before publishing, WGame Rigging Assistant can recover stale `PaletteUV` centers
only when `PaletteToken`, `SurfaceProfile`, `ColorProfileSlot` and `PaletteData.A`
agree with the current scene Profile and Catalog. It writes a Blend backup first,
changes UV coordinates only, then runs the normal export validation. Missing or
conflicting color authority remains blocking; the repair does not guess colors.

- `PaletteToken`: `FACE / INT`, `colorIndex * 16 + surfaceProfile`.
- `PaletteUV`: `CORNER / FLOAT2`, exact center of the selected 256x256 color cell.
- `SurfaceProfile`: `FACE / INT`, `0..15`, used for DCC validation.
- `ColorProfileSlot`: `FACE / INT`, authoring-only stable Profile category.
- `PaletteData`: `CORNER / FLOAT_COLOR`; RGB remains AO/Rim/Emission and Alpha is
  the profile center byte `(8 + profile * 16) / 255`.

The preview material samples only the RGB Color Palette through `PaletteUV` and
uses `PaletteData.A` to drive generated roughness, metallic, and specular ramps.

Migration creates a timestamped Blend backup and report, removes legacy Profile
row and MaterialClass data, writes `PaletteToken=-1`, `SurfaceProfile=-1`,
`ColorProfileSlot=-1`, `PaletteUV=(-1,-1)`, and `PaletteData.A=0`, and preserves geometry, rigging,
weights, UV0, FaceDetailUV, and `PaletteData.RGB`. It never guesses old colors.
