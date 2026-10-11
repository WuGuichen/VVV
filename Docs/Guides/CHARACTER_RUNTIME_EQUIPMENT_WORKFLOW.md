# Character Runtime Equipment Workflow

> Status: Guide
>
> Date: 2026-06-30
>
> Scope: Character Resource Package import, slim generated character prefabs, generated/package-local weapon prefabs, runtime default equipment composition, CharacterTest consumption, tests, and manual validation.

## Decision

Runtime character prefabs are slim exported view assets. They must not persist concrete weapon model instances or runtime behavior components.

A generated character prefab represents the actor body and its exported binding points. Runtime equipment, controller binding, locomotion, and bone sync are installed only after the prefab is instantiated by `CharacterRuntimePrefabComposer`.

This means:

- `*_character_preview.prefab` may contain the model hierarchy, Unity native `Animator` / renderers, `Sockets`, non-physics `AuthoringColliders` marker transforms, `Diagnostics`, and exactly one `CharacterRuntimePrefabDescriptor`.
- `*_character_preview.prefab` must not persist `CharacterDefaultEquipmentRuntimeBinder`, `CharacterRuntimeControllerBinding`, `CharacterRuntimeInputMotionController`, `CharacterRuntimeLocomotionBlendController`, or `CharacterBoneRuntimeSync`.
- `PreviewWeapons` is not part of the runtime character prefab. Preview or bake flows instantiate weapons transiently after composition.
- Package-local weapon visuals may still produce generated weapon prefabs such as `codex_flow_test_mainHand_weapon_iron_sword.prefab`; public/global weapons use their public resource keys and do not require package-local copies.
- Runtime equipment attaches weapon prefab instances to sockets such as `Socket_mainHand` and `Socket_offHand` after the character instance is composed.

## API Reuse Plan

| Need | Framework module / API | Required usage |
| --- | --- | --- |
| Stable asset references | `MxFramework.Resources.ResourceKey`, `ResourceCatalog`, `IResourceManager` | Character and weapon prefabs are loaded through catalog keys, not hard-coded Unity object references in gameplay code. |
| Unity asset loading | `MxFramework.Resources.Unity.ResourcesProvider` or later player providers | Demo/runtime bootstrap may use `ResourcesProvider`; the workflow stays provider-driven. |
| Character config and equipment semantics | `MxFramework.Character.Application` configs and resolvers | Default loadout/equipment decides which weapon is equipped; prefab contents do not decide equipment state. |
| Imported package to runtime binding | `MxFramework.Character.RuntimeSpawn` resolver output | Imported package artifacts produce runtime binding, preload/resource plans, sockets, and weapon attachment plans. |
| Unity composition | `CharacterRuntimePrefabComposer`, `CharacterRuntimeResourceBootstrap`, `CharacterDefaultEquipmentRuntimeBinder` | Unity view instances are composed after instantiation, then default equipment is loaded through the active resource manager. |
| CharacterTest demo | `CharacterTestRuntimeCharacterLoader` and generated package source | CharacterTest consumes generated character resources through the shared composer and does not install standalone input motion by default. |

## Artifact Contracts

### Character Runtime Prefab

Allowed content:

- `ModelRoot` with body model and animator-compatible hierarchy.
- `Sockets` with socket transforms and markers.
- `AuthoringColliders` marker transforms generated from geometry binding. They do not carry `UnityEngine.Collider`, trigger, collision, or `Rigidbody` components; authoritative body collision comes from package geometry and `CombatPhysicsWorld`.
- `Diagnostics` objects that do not carry gameplay assets.
- One `CharacterRuntimePrefabDescriptor` containing `schemaVersion`, `packageId`, `characterResourceId`, and `resourceVariant`.
- Unity native transform, renderer, animator, material, and prefab metadata.

Forbidden content:

- Any concrete weapon model instance.
- Any child named `PreviewWeapons` or whose name starts with `mainHand_weapon.`, `offHand_weapon.`, `DefaultWeapon_`, or `Model_weapon_`.
- `CharacterDefaultEquipmentRuntimeBinder`.
- `CharacterRuntimeControllerBinding`.
- `CharacterRuntimeInputMotionController`.
- `CharacterRuntimeLocomotionBlendController`.
- `CharacterBoneRuntimeSync`.
- Any direct dependency on a specific spawned weapon GameObject instance.

Compatibility note:

- Old fat prefabs are disposable generated artifacts. Rebuild/import the package instead of keeping compatibility paths.
- Runtime and editor preview code must call `CharacterRuntimePrefabComposer` after instantiation when it needs binder, controller binding, locomotion, or bone sync.

### Weapon Prefab

Each generated package-local weapon prefab represents one equipable weapon visual resource.

Allowed content:

- Weapon model wrapper and imported weapon model.
- Weapon-local locators needed by traces, VFX, or muzzle points.
- Optional metadata components that describe weapon id, slot compatibility, trace id, or authoring diagnostics.

Forbidden content:

- Character body model, skeleton, or character socket hierarchy.
- Character-specific runtime state.
- Gameplay ownership state such as team, owner entity, cooldown, or current durability.

### Runtime Catalog

The catalog must include the generated character prefab entry and any generated package-local weapon prefab entries.

Expected entry groups:

- Character prefab: `char.<packageId>.prefab.character_preview` or its future non-preview replacement.
- Package-local weapon prefabs: `char.<packageId>.prefab.weapon.<slot>.<weaponId>`.
- Public/global weapons: their public runtime resource keys, for example `weapon.common_weapon_1.model` with `packageId=common`.
- Source model resources may remain in the catalog, but runtime equipment should load the resolved weapon visual resource, not a serialized weapon child from the character prefab.

## Import and Rebuild Workflow

### Source Package Import

Use this when the external package changed:

```text
MxFramework > Character Package > Import Package...
MxFramework > Character Package > Reimport Last Package
```

`Import Package...` must select the external Character Resource Package root, the directory that contains `manifest.json`.

Do not select:

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>
```

That directory is the Unity-generated import target, not the source package.

### Runtime Entry Point Rebuild

Use this when Unity-side generation logic changed or generated entry points need repair:

```text
MxFramework > Character Package > Rebuild Runtime Entry Points
```

This command should:

- synchronously import generated Unity assets before prefab generation;
- rebuild generated package-local weapon prefabs when a weapon attachment needs one;
- rebuild the slim character runtime prefab;
- mirror runtime-loadable prefabs into the package `runtime_resources/Resources` tree;
- synchronize runtime preview/calibration scenes;
- validate descriptor presence and runtime composition smoke.

It should not:

- reinterpret external authoring data;
- save preview weapon instances into the runtime character prefab;
- persist runtime behavior components into the runtime character prefab;
- require hand-editing generated prefab YAML.

## Runtime Spawn and Equip Flow

Expected runtime flow:

```text
CharacterSpawnRequest
  -> CharacterRuntimeSpawnResolver / selected character definition
  -> preload required character resources
  -> load character runtime prefab
  -> instantiate character body at spawn position
  -> CharacterRuntimePrefabComposer.Compose(...)
  -> install controller binding / locomotion / bone sync / default equipment binder as requested
  -> resolve default loadout / equipment state
  -> load weapon visual resources
  -> instantiate weapon prefabs under resolved sockets
  -> record handles for release
```

Important rules:

- Spawning a character does not imply that the character prefab already contains weapons or runtime behavior components.
- Default equipment may be equipped immediately after spawn, but this must be a runtime action.
- CharacterTest composes with external tick and does not install standalone input motion by default.
- Calibration or standalone scenes may opt into `CharacterRuntimeInputMotionController` through composition options.
- Switching equipment later must unload or detach previous weapon instances and attach newly resolved weapon prefab instances.
- Weapon handles must be released through `IResourceManager.Release`.
- Save/load should store character/equipment ids, not spawned weapon GameObject references.

## Current Implementation

Current implementation points:

- `CharacterImportedPackagePrefabBuilder` writes only the slim descriptor and exported hierarchy to the character prefab.
- `CharacterRuntimePrefabComposer` installs default equipment binder, controller binding, bone sync, locomotion, and optional input motion on an instantiated prefab.
- `CharacterRuntimeResourceBootstrap.LoadCharacter()` composes after loading the character prefab and can opt into standalone input motion for bootstrap/calibration flows.
- `CharacterTestRuntimeCharacterLoader` composes after instantiation, enables external tick, installs default weapons, and keeps standalone input motion disabled.
- `CharacterActionPreviewRuntimeSession` and weapon trace bake compose transient preview instances before installing preview loadouts, default weapons, bone sync, and animation sampling dependencies.

## Test Requirements

### EditMode Tests: Generated Prefab Structure

Location:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterImport/
```

Required assertions:

- Generated character prefab exists after builder runs.
- Generated character prefab contains exactly one `CharacterRuntimePrefabDescriptor`.
- Generated character prefab contains `ModelRoot`, `Sockets`, `AuthoringColliders`, and `Diagnostics`.
- Generated character prefab keeps `AuthoringColliders` as non-physics marker transforms and contains zero `UnityEngine.Collider` components under that subtree.
- Generated character prefab does not contain descendants named `PreviewWeapons`, `mainHand_weapon.*`, `offHand_weapon.*`, `DefaultWeapon_*`, `Model_weapon_*`, `katana`, or `kite_shield`.
- Generated character prefab does not contain `CharacterDefaultEquipmentRuntimeBinder`, `CharacterRuntimeControllerBinding`, `CharacterRuntimeInputMotionController`, `CharacterRuntimeLocomotionBlendController`, or `CharacterBoneRuntimeSync`.
- Runtime composition smoke installs default equipment binder, controller binding, locomotion, and bone sync on an instantiated prefab.

### EditMode Tests: Catalog and Resource Mapping

Location:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterImport/
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/
```

Required assertions:

- Character prefab catalog entry resolves to a Unity-loadable GameObject.
- Package-local weapon prefab catalog entries resolve to Unity-loadable GameObjects when generated.
- Public weapon attachments resolve to their public resource ids and package ids, not generated package-local weapon ids.
- Runtime Resources mirror addresses match the catalog addresses used by CharacterTest.
- Stale `PendingUnityImport` metadata does not force placeholder generation when `AssetDatabase` can load the asset.

### Runtime/PlayMode Tests: Default Equipment Attach

Location:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterRuntimeSpawn/
```

Required assertions:

- `CharacterRuntimeResourceBootstrap.LoadCharacter()` instantiates the slim character prefab.
- Composition creates the runtime binder/controller/bone sync/locomotion components after instantiation.
- Configuring the binder with `instantiateDefaultWeapons: true` creates weapon instances under the expected sockets.
- Destroying the character or clearing equipment destroys spawned weapon instances.
- Weapon resource handles are released exactly once.
- Missing weapon prefab logs a diagnostic and does not block body spawn unless the equipment/resource plan marks it spawn-critical.

### CharacterTest Integration Tests

Location:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/
```

Required assertions:

- CharacterTest spawn config selects character id, team id, and spawn position only.
- CharacterTest does not directly pick a weapon prefab path.
- Default equipment comes from character/equipment data.
- Character instance appears at the requested position.
- Weapon instances appear only after the runtime equipment step.
- CharacterTest does not install `CharacterRuntimeInputMotionController` by default.

## Manual Validation Checklist

After changing the builder:

1. Run `MxFramework > Character Package > Rebuild Runtime Entry Points`.
2. Open the generated character prefab.
3. Confirm `ModelRoot`, `Sockets`, `AuthoringColliders`, `Diagnostics`, and `CharacterRuntimePrefabDescriptor` exist.
4. Confirm `AuthoringColliders` contains only marker transforms and no `UnityEngine.Collider`, trigger, collision, or `Rigidbody` components.
5. Confirm `PreviewWeapons` is absent.
6. Confirm the five runtime components are absent from the prefab asset.
7. Confirm package-local generated weapon prefabs exist only when the attachment is package-local.
8. Enter the CharacterTest scene.
9. Spawn the character.
10. Confirm the body appears at the requested position.
11. Confirm default weapons appear on the correct sockets only after runtime loading.
12. Confirm Console has no new errors and no placeholder-generation warnings for ready assets.

## Acceptance Summary

The workflow is healthy when:

- The generated runtime character prefab is slim and contains no concrete weapon instances or persisted runtime behavior components.
- The descriptor carries only package/resource identity.
- Weapon visuals are still loadable through the resource system.
- Default equipment is attached at runtime through composer-installed binder/bootstrap or the formal character spawn flow.
- CharacterTest does not own hard-coded weapon prefab selection or standalone input motion.
- EditMode tests cover prefab cleanliness, runtime composition, public weapon resources, and weapon trace bake sampling.
