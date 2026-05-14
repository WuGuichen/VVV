# Issue 65 Temp Resource Catalog

Date: 2026-05-15

Scope: create a deterministic Resource Catalog for the formal sample roots
produced by #64, with an Editor/demo memory-provider bootstrap for later runtime
loading validation. This task does not move assets, add config examples, add
runtime loading tests, or add FMOD bank entries.

## Catalog Output

- Catalog path: `Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json`
- Catalog/package id: `mxframework.samples`
- Provider: `memory`
- Bootstrap data: each direct entry includes `providerData.assetPath`.

Direct gameplay-facing entries are limited to:

- Katana top-level prefab as `GameObject`.
- StatusAuras top-level prefabs as `GameObject`.
- StartScreen UI Toolkit textures as `Texture2D`.
- MagicEffects Unity sample clips as `AudioClip`.

Katana meshes/materials/textures and StatusAuras support materials/textures are
dependency-only through Unity asset references. They are not direct gameplay
ResourceKey entries in this catalog.

## Provider Strategy

`MemoryResourceProvider` does not load Unity assets from `providerData.assetPath`.
`TempImportedResourceCatalogEditorBootstrap.CreateMemoryProvider` is the
Editor/demo bootstrap for this slice: it loads each catalog `assetPath` with
`AssetDatabase.LoadAssetAtPath` and registers the result by catalog address.
The `AssetDatabase` dependency is confined to `Demo/Editor` and tests; it is not
part of the noEngine resource assemblies.

FMOD banks, FMOD event paths, and FMOD GUIDs remain outside the ordinary Resource
Catalog per #68.
