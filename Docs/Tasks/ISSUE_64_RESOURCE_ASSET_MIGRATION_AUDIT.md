# Issue 64 Resource Asset Migration Audit

Date: 2026-05-15

Scope: migrate the temporary sample assets from `Assets/_TempImportedResources/`
to the formal sample roots defined by `Docs/RESOURCE_DIRECTORY_LAYOUT.md`, audit
import settings, and record validation evidence. No Resource Catalog JSON,
runtime API, runtime loading test, FMOD bank, StreamingAssets bank, FMOD plugin,
or `.gitignore` changes are part of this task.

## Migration Method

Assets were moved through the Unity Editor via MCP and `AssetDatabase.MoveAsset`.
This preserves the existing asset GUIDs and moves each asset together with its
`.meta` file. The VFX `Materials` and `Textures` directories were moved as
directories so their child assets and directory metadata stayed paired. After
all non-meta assets were migrated, the empty `Assets/_TempImportedResources`
staging root was removed through `AssetDatabase.DeleteAsset`.

The first `manage_asset` call for `Skeleton.fbx` reported a tool-level failure,
but disk and AssetDatabase state showed that Unity had already moved the asset
successfully. The remaining migration was run with explicit `AssetDatabase`
validation and returned the target GUIDs listed below.

## Moved Assets

| Source | Target | GUID |
| --- | --- | --- |
| `Assets/_TempImportedResources/Art/Models/Characters/Skeleton.fbx` | `Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx` | `984990905752598458c011763135060d` |
| `Assets/_TempImportedResources/Art/Models/Characters/Materials/Skeleton.mat` | `Assets/Art/MxFramework/Samples/Characters/Skeleton/Materials/Skeleton.mat` | `fbd308fd70b4293429b1f7e3b760c154` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Meshes/katana.fbx` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Meshes/katana.fbx` | `ba23a8af226666642bc5277b12efc919` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Materials/mat_sword_generic01.mat` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Materials/mat_sword_generic_01.mat` | `04f36426fa8d5c341a2f993f0c836d42` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Materials/phys_MetalSword.physicMaterial` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Materials/phys_metal_sword.physicMaterial` | `e26af8b40b8a1204f8102f9d30f24732` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_dif.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_albedo.png` | `d0439053683214c4a9dd202ecaec4bc0` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_msk.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_mask.png` | `e68a3ffc3c0e6894db46bc49e2cd6d27` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_nrm.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_normal.png` | `3779661a1e84c51419d18590b9e434d8` |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Prefabs/Katana_Generic01.prefab` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab` | `1ac258bebac56774ab51d86547617ba5` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/menu_button_normal.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png` | `9b7cb3348a06dee46ba153871343231b` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/menu_button_hover.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/button_hover.png` | `b47f751b47c32a6499fdd750475c2ac6` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/separator_diamond_line.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/separator_diamond_line.png` | `8bcde21fde17e924a8bed58d2be6ecfc` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_archive_book.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_archive_book.png` | `ed58aa5ff609f0a4b8be7ded49841aca` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_continue_hourglass.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_continue_hourglass.png` | `a01e088a870a4de478c7a96d70cdfe61` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_exit_door.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_exit_door.png` | `c1b86fe6990f84a46812ec8db5cb5760` |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_settings_cog.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_settings_cog.png` | `bf684e93841ff9e4d9b1656de3ae2850` |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Materials/**` | `Assets/VFX/MxFramework/Samples/StatusAuras/Materials/**` | Directory move, child GUIDs preserved |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Textures/**` | `Assets/VFX/MxFramework/Samples/StatusAuras/Textures/**` | Directory move, child GUIDs preserved |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Textures/3-Trail_ 1.tiff` | `Assets/VFX/MxFramework/Samples/StatusAuras/Textures/trail_01.png` | `f316856648304c04894a3e47462902be` |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Burn.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Burn.prefab` | `5357b7f40e1451744886942913efe75f` |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Lightning.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Lightning.prefab` | `88b14fae91f3b5345abdc3e7a5f29793` |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Smoke.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Smoke.prefab` | `778a281a2188fc24aab47c804de7dc83` |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Stun.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Stun.prefab` | `b6d3e2e4b8df2964eb3dfce561902f3e` |
| `Assets/_TempImportedResources/Audio/MagicEffects/ExplosionFire1.ogg` | `Assets/Audio/MxFramework/Samples/MagicEffects/explosion_fire_1.ogg` | `de63089b399c59949b3e74a3d762d57b` |
| `Assets/_TempImportedResources/Audio/MagicEffects/ExplosionLightning3.wav` | `Assets/Audio/MxFramework/Samples/MagicEffects/explosion_lightning_3.wav` | `08a66967347d9f644b6084fc55fd4acb` |
| `Assets/_TempImportedResources/Audio/MagicEffects/LoopFire2.ogg` | `Assets/Audio/MxFramework/Samples/MagicEffects/loop_fire_2.ogg` | `f5935514f32e73346976dae087365809` |
| `Assets/_TempImportedResources/Audio/MagicEffects/Wind.wav` | `Assets/Audio/MxFramework/Samples/MagicEffects/wind.wav` | `6a4eaec8a876e284ea980b00de0a386f` |

## Import Settings Audit

- StartScreen UI textures are formal UI Toolkit `Texture2D` assets: `textureType=Default`,
  `spriteImportMode=None`, `sRGBTexture=True`, `alphaSource=FromInput`,
  `alphaIsTransparency=True`, `mipmapEnabled=False`. No Sprite assets were
  generated.
- Katana `sword_generic_01_mask.png` was normalized to `sRGBTexture=False`,
  `textureType=Default`, and `spriteImportMode=None` because it is a mask map.
- Katana `sword_generic_01_normal.png` remains a normal map:
  `textureType=NormalMap`, `sRGBTexture=False`, and `spriteImportMode=None`.
- `3-Trail_ 1.tiff` was verified as PNG image data by the `file` tool and was
  renamed to `trail_01.png` while preserving GUID `f316856648304c04894a3e47462902be`.
  Its importer is `textureType=Default`, `spriteImportMode=None`,
  `sRGBTexture=True`, and `alphaSource=FromInput`.
- VFX support textures other than `trail_01.png` remain dependency-only default
  textures with no Sprite generation.
- MagicEffects remain Unity `AudioClip` samples per the #63 resource layout
  decision. Their importer settings were audited and left unchanged:
  `loadType=DecompressOnLoad`, `compression=Vorbis`, `preload=False`,
  `quality=1`.
- `Skeleton.fbx` remains a conditional/direct model-viewer sample candidate,
  not a gameplay runtime prefab. Its model importer still uses Humanoid
  settings (`animationType=3`, `avatarSetup=1`) and the binary still contains
  legacy local WGame source path strings such as
  `E:\UnityProjects\WGame\BlenderLearn\Skeleton.blend` and
  `E:\UnityProjects\WGame\Client\Assets\LOWPOLY_MEDIEVAL_WORLD\PT_Medieval_Armors_Texture_work.png`.
  These strings are recorded as import provenance and were not edited during
  the asset move.
- `Katana_Generic01.prefab` is a display/test prefab. It contains five
  `BoxCollider` components and one kinematic `Rigidbody`; it should not be
  treated as deterministic Combat source of truth.

## Reference Validation

Unity refresh and importer reimports were run through MCP. The following
Editor checks loaded each prefab with `PrefabUtility.LoadPrefabContents`,
counted missing scripts and missing serialized object references, and checked
dependencies for any `_TempImportedResources` paths:

| Prefab | Dependencies | Temp deps | Missing scripts | Missing object refs |
| --- | ---: | ---: | ---: | ---: |
| `Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab` | 10 | 0 | 0 | 0 |
| `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Burn.prefab` | 9 | 0 | 0 | 0 |
| `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Lightning.prefab` | 9 | 0 | 0 | 0 |
| `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Smoke.prefab` | 7 | 0 | 0 | 0 |
| `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Stun.prefab` | 13 | 0 | 0 | 0 |

Additional GUID spot checks confirm that the moved Katana prefab still resolves
the mesh, material, physic material, albedo, mask, and normal texture GUIDs, and
that `Stun/Trail.mat` still resolves `trail_01.png` by the original trail GUID.

## Residual `_TempImportedResources` References

The physical `Assets/_TempImportedResources` directory has been removed after
the migration. A search for `_TempImportedResources` now returns only
documentation and historical/task references:

- `Docs/RESOURCE_DIRECTORY_LAYOUT.md` records the #63 staging policy and mapping
  table that #64 executed.
- `Docs/Tasks/ISSUE_68_FMOD_BANK_RESOURCE_POLICY.md` is #68-owned context
  documenting that MagicEffects are Unity `AudioClip` samples and not FMOD
  source audio.

No Catalog, config, runtime source, prefab, material, or active asset path
references `_TempImportedResources`.

## Not In Scope

- No files under `Assets/Config/MxFramework/ResourceCatalogs/**` were created or
  modified.
- No runtime code, resource manager public API, FMOD project/cache/plugin files,
  or `Assets/StreamingAssets/Master*.bank` files were modified.
- No runtime loading tests were performed; validation was limited to Unity
  refresh/importer and prefab/material reference checks required by #64.
