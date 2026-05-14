# Issue 66 ResourceKey Config Sample And Validation

Date: 2026-05-15

Scope: add a minimal framework demo config profile that proves config fields
store `ResourceKey` values, not Unity asset locations or direct object
references. This task does not load runtime resources, move assets, edit the
#65 catalog, or touch FMOD bank/settings/cache files.

## Sample Config

- Source: `Assets/Config/MxFramework/ResourceProfiles/mxframework_demo_resource_profile.json`
- Schema: `Assets/Config/MxFramework/ResourceProfiles/mxframework_demo_resource_profile.schema.json`
- Runtime/demo DTO: `ResourceKeyConfigProfile`
- Validator: `ResourceKeyConfigProfileValidator`

The sample covers the direct #65 catalog entries required by #66:

- Start screen UI textures as `Texture2D`: normal button, hover button,
  diamond separator, archive icon, continue icon, exit icon, and settings icon
- Status aura VFX prefabs as `GameObject`: burn, lightning, smoke, and stun
- Katana weapon prefab: `art.weapon.katana.generic_01` as `GameObject`
- MagicEffects clips as Unity `AudioClip` sample keys:
  `audio.magic_effect.explosion_fire_1`,
  `audio.magic_effect.explosion_lightning_3`,
  `audio.magic_effect.loop_fire_2`, and `audio.magic_effect.wind`

Every sample key uses package `mxframework.samples` and an empty variant,
matching the #65 catalog policy.

## Validation Rules

`ResourceKeyConfigProfileValidator` validates the config source against a
provided `ResourceCatalog` only. It does not call `IResourceManager.Load`.

Validation fails when:

- The referenced key is missing from the catalog.
- The field expected type does not match the config key or catalog entry type.
- The package is not `mxframework.samples`.
- The variant is not empty.
- A key field contains an `Assets/` path, `_TempImportedResources`, GUID-like
  value, bundle file name, FMOD bank/event value, or direct object type marker.

Issue messages include source, field, expected type, actual key, package,
variant, and catalog context so authoring tools can point at the exact field.

## Audio Boundary

MagicEffects in this sample are ordinary Unity `AudioClip` resources from #65.
FMOD event config remains outside the generic Resource Catalog route and should
use `AudioEventDefinition`, `FmodEventGuid`, `FmodEventPath`, and the Audio
module validation policy documented in #68.

## Tests

EditMode tests live in
`Assets/Scripts/MxFramework/Tests/Config/ResourceKeyConfigProfileTests.cs`.
They cover the valid sample, schema shape, package/variant policy, type
matching, missing keys, direct-reference bans, and static checks for the JSON
sample files.
