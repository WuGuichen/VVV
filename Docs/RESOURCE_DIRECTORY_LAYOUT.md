# Resource Directory Layout

> Status: Draft for Issue #63
> Scope: directory, naming, catalog, and follow-up boundaries for sample resources.

This document turns the current temporary imported resources into an executable directory and naming policy. It is documentation only: Issue #63 does not move assets, rewrite import settings, generate Catalog entries, or change runtime code.

## Goals

- Define stable roots for framework sample resources.
- Keep import staging separate from runtime/catalog provider roots.
- Decide which assets receive direct `ResourceKey` entries and which remain dependency-only.
- Provide a migration table that Issues #64-#67 can execute without rediscovering paths or names.
- Record the current FMOD bank split enough for #64-#67 to avoid mixing Unity `AudioClip` samples with FMOD bank runtime data. Issue #68 now owns the detailed FMOD bank policy in `Docs/Tasks/ISSUE_68_FMOD_BANK_RESOURCE_POLICY.md`.

## Resource Pipeline

```text
External package / vendor drop
  -> Assets/_TempImportedResources/              staging and audit input only
  -> Assets/<Domain>/MxFramework/Samples/...     formal Unity project source assets
  -> Assets/Config/MxFramework/ResourceCatalogs/ generated or reviewed catalog JSON
  -> Generated runtime outputs                   bundles, catalogs, hashes, manifests
  -> Assets/StreamingAssets/                     runtime file mirrors, including FMOD banks
  -> Runtime composition root                    ResourceManager + providers + preload
```

Rules:

- `Assets/_TempImportedResources/` is a staging area and audit sample. It is not a runtime root, not a Catalog provider root, and not a valid config reference target.
- Long-lived runtime resources must not be loaded directly from `Assets/Resources`. `ResourcesProvider` stays available for explicit tiny demos and tests, but formal samples should be cataloged for AssetBundle / Streaming / RemoteBundle style providers.
- Gameplay, Config, Buffs, Combat, Ability, UI flow, and other noEngine/runtime logic keep only `ResourceKey` or audio event IDs. They must not bind Unity paths, GUIDs, `UnityEngine.Object`, or FMOD native objects.
- Editor-generated Catalog JSON, runtime config bytes, bundle files, FMOD bank mirrors, and editor cache/generated metadata are different artifact classes and must not be collapsed into one directory rule.

## Formal Roots

Use `MxFramework` as the package directory for built-in framework samples. Use `Samples` for stable sample content; do not use `TempSamples` as a formal destination.

Recommended roots:

```text
Assets/
  Art/MxFramework/Samples/
    Characters/
    Weapons/
  UI/MxFramework/Samples/
    StartScreen/
  VFX/MxFramework/Samples/
    StatusAuras/
  Audio/MxFramework/Samples/
    MagicEffects/
  Config/MxFramework/ResourceCatalogs/
  Config/MxFramework/ResourceProfiles/

FMOD/
  MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/
Assets/
  StreamingAssets/
  Plugins/FMOD/Cache/Editor/
```

Directory roles:

| Root | Role | Catalog status |
| --- | --- | --- |
| `Assets/Art/MxFramework/Samples/` | Formal authored model, material, texture, and prefab sample assets. | Direct top-level prefabs/models can be cataloged; support assets usually dependency-only. |
| `Assets/UI/MxFramework/Samples/` | Formal UI Toolkit textures, UXML, USS, themes, and UI sample support assets. | UI textures may be direct `Texture2D` entries when config/runtime UI references them. |
| `Assets/VFX/MxFramework/Samples/` | Formal authored VFX prefabs and support materials/textures. | VFX prefabs are direct `GameObject` entries; support assets dependency-only. |
| `Assets/Audio/MxFramework/Samples/` | Formal Unity `AudioClip` samples. | Direct `AudioClip` entries for generic resource samples. |
| `Assets/Config/MxFramework/ResourceCatalogs/` | Catalog JSON source/generated output reviewed by Editor validation. | Catalog files, not resources loaded as ordinary assets. |
| `Assets/Config/MxFramework/ResourceProfiles/` | Variant fallback, preload group, retain policy, and provider profile config. These files reference Catalog entries by `ResourceKey`, labels, variants, and package IDs instead of duplicating Catalog address data. | Config references `ResourceKey`, labels, and variants. |
| `FMOD/.../Build/Desktop/` | FMOD Studio authoring build output for desktop banks. | Audio/FMOD pipeline input/output, not generic `ResourceCatalogEntry` samples. |
| `Assets/StreamingAssets/Master*.bank` | Unity runtime mirror for FMOD banks. | Loaded by FMOD runtime/settings, not by generic Resource Catalog in #63-#67. |
| `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` | FMOD Unity editor cache/generated metadata. | Versioned generated metadata; refresh through FMOD tooling, do not hand-author. |

## Naming

Use stable lowercase identifiers for Resource Catalog fields and keep file names readable for Unity authors.

### PackageId

- Built-in framework samples: `mxframework.samples`.
- Future focused demo packages may use `mxframework.demo.<demo_id>`, for example `mxframework.demo.runtime_vertical_slice`.
- Mod/DLC packages keep their own package IDs and must not reuse `mxframework.samples`.

### ResourceKey.Id

Format:

```text
<domain>.<category>.<asset_name>[.<variant>]
```

Rules:

- Lowercase only; allowed characters are `a-z`, `0-9`, `.`, `_`, and `-`.
- `domain` is one of the broad roots such as `art`, `ui`, `vfx`, or `audio`.
- `category` describes the stable use group, such as `weapon`, `character`, `start_screen`, `status_aura`, or `magic_effect`.
- `asset_name` is semantic and does not need to preserve source prefixes like `tex_` or `menu_` if they are only authoring noise.
- `asset_name` may include stable version or serial suffixes such as `generic_01`. These suffixes remain part of `ResourceKey.Id`.
- Use the Catalog `variant` field only for runtime-resolved platform, quality, locale, or comparable fallback variants. Do not split stable asset names such as `generic_01` into the `variant` field.
- Semantic state names such as `normal`, `hover`, `burn`, `lightning`, or `loop` may appear in `ResourceKey.Id` when they identify distinct authored assets. Use the Catalog `variant` field only when the resource manager should select among interchangeable variants of the same asset.
- Type suffix is expressed by `ResourceKey.TypeId` / Catalog `type`, not by duplicating `.prefab`, `.texture`, or `.clip` into the key unless two resources would otherwise collide.

Examples:

| Asset | ResourceKey.Id | Type |
| --- | --- | --- |
| Katana prefab | `art.weapon.katana.generic_01` | `GameObject` |
| Start button normal texture | `ui.start_screen.button.normal` | `Texture2D` |
| Start button hover texture | `ui.start_screen.button.hover` | `Texture2D` |
| Aura Burn prefab | `vfx.status_aura.burn` | `GameObject` |
| ExplosionFire1 clip | `audio.magic_effect.explosion_fire_1` | `AudioClip` |

### Labels and Bundle Names

Labels are logical warmup/query groups, not replacement keys.

Recommended labels:

- `package.mxframework.samples`
- `domain.art`, `domain.ui`, `domain.vfx`, `domain.audio`
- `sample.characters`, `sample.katana`, `sample.start_screen`, `sample.status_auras`, `sample.magic_effects`
- `warmup.demo.start_screen` or `warmup.demo.combat` only when a runtime slice actually preloads that group.

Bundle names should be lowercase, package-scoped, and domain-scoped:

```text
mxframework.samples.art.characters
mxframework.samples.art.weapons.katana
mxframework.samples.ui.start_screen
mxframework.samples.vfx.status_auras
mxframework.samples.audio.magic_effects
```

`providerData` should be provider-specific. For Editor Play Mode / serialized-reference demos, `providerData.assetPath` may point to a Unity asset path for validation. For `assetBundle`, use `bundleName` and optionally `assetName` if the provider address does not already encode it. For `remoteBundle`, use `url`, `bundleName`, and `cacheKey`, with `hash` carrying the content checksum. For `streamingFile`, use a provider-owned relative path field such as `relativePath`; never store an absolute local machine path.

## Direct Keys vs Dependencies

Only assets that runtime/config/gameplay code should request by identity receive direct keys. Supporting assets stay as dependencies and are validated through Unity import, prefab references, bundle build reports, and Catalog dependency metadata.

| Family | Direct ResourceKey | Dependency-only by default |
| --- | --- | --- |
| Katana | `Katana_Generic01.prefab` as `GameObject`. | `katana.fbx`, `mat_sword_generic01.mat`, `phys_MetalSword.physicMaterial`, and `tex_sword_generic01_*` unless an editor/demo explicitly needs them standalone. |
| StatusAuras | Each top-level VFX prefab as `GameObject`. | Materials and textures under `Materials/` and `Textures/`. |
| StartScreen UI | Button, separator, and icon textures as `Texture2D` when UI Toolkit references them through config/catalog. | Sprite variants are deferred until a SpriteRenderer/uGUI use case exists. |
| Skeleton | Prefer a future character sample prefab as the direct `GameObject`. The raw `Skeleton.fbx` can be direct only for a model-viewer/import-validation sample. | `Skeleton.mat` and embedded/imported model dependencies. |
| MagicEffects | Current decision: Unity `AudioClip` sample entries. | If later imported into FMOD Studio, the source audio moves to the FMOD authoring pipeline and stops being a generic AudioClip sample. |
| FMOD banks | No generic `ResourceKey` in #63-#67. | Banks are FMOD runtime data loaded by FMOD settings/backend; future bank-specific keys require a separate S2 issue. |

## Import Type Policy

- UI Toolkit background and icon images are formal `Texture2D` resources. Import settings in #64 should optimize for UI Toolkit usage and preserve alpha where needed.
- Do not create Sprite variants for StartScreen images in #63-#65 unless a SpriteRenderer/uGUI consumer is added. If that happens later, use separate Sprite-specific entries with `ResourceTypeIds.Sprite` and clear labels.
- VFX textures remain texture dependencies of VFX materials/prefabs. Catalog entries should not expose each VFX texture as business-facing resources by default.
- `3-Trail_ 1.tiff` currently has an odd extension/name. #64 should audit the imported image data and either re-export/rename to a truthful extension or document why Unity import accepts it. #63 only records the issue.
- Katana and Skeleton models may need import setting review in #64. The Skeleton FBX has legacy local WGame texture path traces; #64 should decide whether to preserve them as audit evidence, clean them during reimport, or exclude stale references from formal samples.

### StartScreen Name Reconciliation

The current repository tree uses the concrete file names listed in the mapping table below. Earlier umbrella notes used names such as `buttonNormal.png`, `buttonHover.png`, `separator.png`, `characterIcon.png`, `exclamationIcon.png`, `exitIcon.png`, `optionsIcon.png`, `shieldIcon.png`, and `swordIcon.png`; #64 should not migrate nonexistent files by those names. If those aliases reappear in a later import drop, map them to the same `ui.start_screen.*` key family and require a current tree audit before moving assets.

## Audio and FMOD Decision

For Issue #63, `Assets/_TempImportedResources/Audio/MagicEffects/*` are classified as Unity `AudioClip` samples. They may enter the generic Resource Catalog with `ResourceTypeIds.AudioClip` after #65 and may be referenced by config examples after #66.

FMOD banks are separate:

- `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.bank`
- `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.strings.bank`

These are FMOD Studio authoring build outputs for the demo project. They are not ordinary Catalog `AudioClip` assets.

- `Assets/StreamingAssets/Master.bank`
- `Assets/StreamingAssets/Master.strings.bank`

These are the Unity runtime bank mirror currently available to FMOD Unity Integration and `MxFramework.Audio.FMOD`. Generic Resource Catalog loading does not own them in #63-#67.

- `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`

This is FMOD Unity editor cache/generated metadata. Issue #68 classifies it as versioned generated metadata: refresh it through FMOD/Refresh Banks when bank/event metadata changes, commit intentional changes, and do not treat it as hand-authored audio config.

## Temporary Asset Mapping

`Action` describes #64 behavior. `move` keeps the filename, `move+rename` changes path and filename, and `audit+rename` requires checking file content/import state before deciding the final filename.

| Current path | Recommended formal path | ResourceKey / label | Type | Direct? | Action | Next issue |
| --- | --- | --- | --- | --- | --- | --- |
| `Assets/_TempImportedResources/Art/Models/Characters/Skeleton.fbx` | `Assets/Art/MxFramework/Samples/Characters/Skeleton/Models/Skeleton.fbx` | `art.character.skeleton` if used as direct model-viewer sample; labels `package.mxframework.samples`, `domain.art`, `sample.characters` | `GameObject` / model asset | Conditional | move | #64 |
| `Assets/_TempImportedResources/Art/Models/Characters/Materials/Skeleton.mat` | `Assets/Art/MxFramework/Samples/Characters/Skeleton/Materials/Skeleton.mat` | Dependency of `art.character.skeleton` or future prefab | `Material` | No by default | move | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Meshes/katana.fbx` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Meshes/katana.fbx` | Dependency of `art.weapon.katana.generic_01` | model asset | No by default | move | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Materials/mat_sword_generic01.mat` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Materials/mat_sword_generic_01.mat` | Dependency of `art.weapon.katana.generic_01` | `Material` | No by default | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Materials/phys_MetalSword.physicMaterial` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Materials/phys_metal_sword.physicMaterial` | Dependency of `art.weapon.katana.generic_01` | `PhysicMaterial` | No by default | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_dif.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_albedo.png` | Dependency of `art.weapon.katana.generic_01` | `Texture2D` | No by default | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_msk.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_mask.png` | Dependency of `art.weapon.katana.generic_01` | `Texture2D` | No by default | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Textures/tex_sword_generic01_nrm.png` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Textures/sword_generic_01_normal.png` | Dependency of `art.weapon.katana.generic_01` | `Texture2D` | No by default | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Models/Weapons/Katana/Prefabs/Katana_Generic01.prefab` | `Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab` | `art.weapon.katana.generic_01`; labels `sample.katana`, `domain.art` | `GameObject` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/menu_button_normal.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png` | `ui.start_screen.button.normal`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/menu_button_hover.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/button_hover.png` | `ui.start_screen.button.hover`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/separator_diamond_line.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/separator_diamond_line.png` | `ui.start_screen.separator.diamond_line`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_archive_book.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_archive_book.png` | `ui.start_screen.icon.archive_book`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_continue_hourglass.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_continue_hourglass.png` | `ui.start_screen.icon.continue_hourglass`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_exit_door.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_exit_door.png` | `ui.start_screen.icon.exit_door`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/Textures/UI/StartScreen/icon_settings_cog.png` | `Assets/UI/MxFramework/Samples/StartScreen/Textures/icon_settings_cog.png` | `ui.start_screen.icon.settings_cog`; labels `sample.start_screen`, `domain.ui` | `Texture2D` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Burn.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Burn.prefab` | `vfx.status_aura.burn`; labels `sample.status_auras`, `domain.vfx` | `GameObject` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Lightning.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Lightning.prefab` | `vfx.status_aura.lightning`; labels `sample.status_auras`, `domain.vfx` | `GameObject` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Aura Smoke.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Aura_Smoke.prefab` | `vfx.status_aura.smoke`; labels `sample.status_auras`, `domain.vfx` | `GameObject` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Prefabs/Stun.prefab` | `Assets/VFX/MxFramework/Samples/StatusAuras/Prefabs/Stun.prefab` | `vfx.status_aura.stun`; labels `sample.status_auras`, `domain.vfx` | `GameObject` | Yes | move | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Materials/**` | `Assets/VFX/MxFramework/Samples/StatusAuras/Materials/**` | Dependencies of `vfx.status_aura.*` | `Material` | No by default | move | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Textures/**` | `Assets/VFX/MxFramework/Samples/StatusAuras/Textures/**` excluding `3-Trail_ 1.tiff` | Dependencies of `vfx.status_aura.*` | `Texture2D` | No by default | move | #64 |
| `Assets/_TempImportedResources/Art/VFX/StatusAuras/Textures/3-Trail_ 1.tiff` | `Assets/VFX/MxFramework/Samples/StatusAuras/Textures/trail_01.<verified-extension>` | Dependency of `vfx.status_aura.*`; audit extension/content mismatch before final path | `Texture2D` | No by default | audit+rename | #64 |
| `Assets/_TempImportedResources/Audio/MagicEffects/ExplosionFire1.ogg` | `Assets/Audio/MxFramework/Samples/MagicEffects/explosion_fire_1.ogg` | `audio.magic_effect.explosion_fire_1`; labels `sample.magic_effects`, `domain.audio` | `AudioClip` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Audio/MagicEffects/ExplosionLightning3.wav` | `Assets/Audio/MxFramework/Samples/MagicEffects/explosion_lightning_3.wav` | `audio.magic_effect.explosion_lightning_3`; labels `sample.magic_effects`, `domain.audio` | `AudioClip` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Audio/MagicEffects/LoopFire2.ogg` | `Assets/Audio/MxFramework/Samples/MagicEffects/loop_fire_2.ogg` | `audio.magic_effect.loop_fire_2`; labels `sample.magic_effects`, `domain.audio` | `AudioClip` | Yes | move+rename | #64 |
| `Assets/_TempImportedResources/Audio/MagicEffects/Wind.wav` | `Assets/Audio/MxFramework/Samples/MagicEffects/wind.wav` | `audio.magic_effect.wind`; labels `sample.magic_effects`, `domain.audio` | `AudioClip` | Yes | move+rename | #64 |
| `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.bank` | Same path; versioned FMOD Studio demo build output / release input | FMOD bank, no generic ResourceKey in #63-#67 | FMOD bank | No | keep/audit | #68 |
| `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.strings.bank` | Same path; versioned FMOD Studio demo build output / release input | FMOD strings bank, no generic ResourceKey in #63-#67 | FMOD bank | No | keep/audit | #68 |
| `Assets/StreamingAssets/Master.bank` | Same path; versioned Unity runtime mirror | Runtime FMOD bank mirror, loaded by FMOD integration/backend | FMOD bank | No | keep/audit | #68 |
| `Assets/StreamingAssets/Master.strings.bank` | Same path; versioned Unity runtime mirror | Runtime FMOD strings bank mirror, loaded by FMOD integration/backend | FMOD bank | No | keep/audit | #68 |
| `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` | Same path; versioned generated FMOD Unity metadata | FMOD editor cache/generated metadata | Editor metadata | No | keep/audit | #68 |

## Catalog Entry Shape

Issue #65 should generate or hand-author Catalog entries only after #64 has moved/audited import settings.

Address conventions:

- `assetBundle` and `remoteBundle` entries use `bundleName|assetPath` unless a provider implementation explicitly documents a different address parser.
- `memory` entries may use any stable in-memory address, but Editor Play Mode sample entries should also include `providerData.assetPath` for `AssetDatabase` validation.
- `streamingFile` entries should use a provider-owned relative path under `Application.streamingAssetsPath`, not an absolute path.
- `hash` and `size` are generated/build-report fields. Hand-authored draft entries may leave `hash` empty and `size` as `0`; bundle/remote build tools should fill them before release validation.
- `allowOverride=false` means this entry cannot silently replace a lower-layer entry with the same `id + type + variant`. Set it to `true` only for explicit package override scenarios, and keep the type stable.

Example shape:

```json
{
  "schemaVersion": 1,
  "catalogId": "mxframework.samples",
  "packageId": "mxframework.samples",
  "entries": [
    {
      "id": "art.weapon.katana.generic_01",
      "type": "GameObject",
      "variant": "",
      "packageId": "mxframework.samples",
      "provider": "assetBundle",
      "address": "mxframework.samples.art.weapons.katana|Assets/Art/MxFramework/Samples/Weapons/Katana/Prefabs/Katana_Generic01.prefab",
      "labels": ["package.mxframework.samples", "domain.art", "sample.katana"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {
        "bundleName": "mxframework.samples.art.weapons.katana"
      }
    },
    {
      "id": "ui.start_screen.button.normal",
      "type": "Texture2D",
      "variant": "",
      "packageId": "mxframework.samples",
      "provider": "assetBundle",
      "address": "mxframework.samples.ui.start_screen|Assets/UI/MxFramework/Samples/StartScreen/Textures/button_normal.png",
      "labels": ["package.mxframework.samples", "domain.ui", "sample.start_screen"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {
        "bundleName": "mxframework.samples.ui.start_screen"
      }
    },
    {
      "id": "audio.magic_effect.explosion_fire_1",
      "type": "AudioClip",
      "variant": "",
      "packageId": "mxframework.samples",
      "provider": "assetBundle",
      "address": "mxframework.samples.audio.magic_effects|Assets/Audio/MxFramework/Samples/MagicEffects/explosion_fire_1.ogg",
      "labels": ["package.mxframework.samples", "domain.audio", "sample.magic_effects"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {
        "bundleName": "mxframework.samples.audio.magic_effects"
      }
    },
    {
      "id": "debug.material.katana.sword_generic_01",
      "type": "Material",
      "variant": "",
      "packageId": "mxframework.samples",
      "provider": "memory",
      "address": "debug/material/katana/sword_generic_01",
      "labels": ["package.mxframework.samples", "domain.art", "debug.authoring"],
      "dependencies": [],
      "hash": "",
      "size": 0,
      "allowOverride": false,
      "providerData": {
        "assetPath": "Assets/Art/MxFramework/Samples/Weapons/Katana/Materials/mat_sword_generic_01.mat"
      }
    }
  ]
}
```

The `Material` entry above is intentionally marked as a debug/authoring example. Katana runtime gameplay should normally request the prefab and let materials/textures load as dependencies.

Editor Play Mode demos may use `memory` provider plus `providerData.assetPath` when they bind serialized Unity objects. Player-facing samples should move toward AssetBundle / Streaming / RemoteBundle providers instead of relying on `memory` or `resources`.

## Follow-up Boundaries

| Issue | Boundary |
| --- | --- |
| #64 | Move/rename/archive temporary assets, audit Unity import settings, verify file extensions and stale references, and decide exact submitted asset paths. No Catalog generation ownership beyond producing source assets ready for validation. |
| #65 | Generate or author Resource Catalog directories and entries for the formal sample assets. Validate provider, type, address, labels, dependencies, and direct/dependency-only split. |
| #66 | Add config examples that reference these resources by `ResourceKey`, plus validation for missing/wrong-type keys. Config examples must not use Unity paths. |
| #67 | Runtime loading validation: preload labels, load/release direct keys, verify UI/VFX/prefab/audio sample behavior through `IResourceManager` and provider setup. |
| #68 | Resolved by `Docs/Tasks/ISSUE_68_FMOD_BANK_RESOURCE_POLICY.md`: FMOD Build/Desktop bank output and StreamingAssets runtime mirror remain versioned, FMODStudioCache is versioned generated metadata, and FMOD bank/event data stays under Audio.FMOD rather than the ordinary Resource Catalog until a later S2 bank-provider issue. |

## Validation Checklist

- Every non-meta family under `Assets/_TempImportedResources/` is represented above or grouped with an explicit dependency-only rule.
- `_TempImportedResources` does not appear as a formal `providerData.assetPath`, Catalog address, or config target.
- The formal naming uses `Samples`, not `TempSamples`.
- MagicEffects are Unity `AudioClip` samples for #63-#67.
- FMOD build output, StreamingAssets mirror, and FMOD editor cache are listed separately and follow the #68 policy. Ordinary Catalog examples here must not add `.bank` files or FMOD event paths as `AudioClip` / `Texture2D` / `GameObject` entries.
