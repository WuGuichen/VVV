# LCC-M5-T1 Authoring / Runtime Binding Evidence - 2026-08-26

## Result

T1 production implementation is complete within the frozen owner boundary. Body Presence v1 remains readable and canonical without an additive field; v2 adds a maximum of four stable `animatedHurtboxSources` references. A valid Current v2 document compiles to geometry binding v7 and RuntimeSpawn resolves the source policy without copying collider shape, PartId, bone, socket, or bake tracks.

This evidence proves authoring and RuntimeSpawn binding only. It does not prove gameplay contact, pressure, events, network behavior, or Playable acceptance.

## Implemented contract

- v1: `mx.characterBodyPresence.v1 / 1.0`, unchanged `volumes[]`, no emitted `animatedHurtboxSources`.
- v2: `mx.characterBodyPresence.v2 / 2.0`, additive `animatedHurtboxSources[]`, capacity 4.
- source fields: `sourceId`, `colliderId`, `skinMillimeters`, `pressureScaleMilli`, `enabled` only.
- validation: source/collider uniqueness; stable source id; collider exists; `CharacterHurtbox`; stable PartId; Sphere/Capsule/Box; skin `0..10000`; pressure `0..100000`.
- canonical order and source identity: ordinal `sourceId`; v2 content hash covers the complete v2 canonical document.
- geometry compatibility: v1-v6 dispatch remains accepted; v7 is emitted only for valid Current Body Presence v2 and adds canonical animated source bindings.
- RuntimeSpawn: v1-v6 receive zero animated sources; v7 rejects missing/duplicate/unsorted/unsupported sources and content hash drift.

## Focused validation

### Authoring

Command:

```text
dotnet Temp/Codex/LccM5T1/tests/MxFramework.Authoring.Tests.dll character-body-presence
```

Raw result:

```text
Character Body Presence authoring tests passed.
exit=0
```

The isolated Authoring test build completed with `0 warnings / 0 errors`.

### CharacterStudio smoke

Command used the bundled Node runtime:

```text
node Tools/MxFramework.CharacterStudio/scripts/smoke.mjs
```

Result: blocked by an existing unrelated fixture error before the Body Presence assertions execute:

```text
TypeError: binding.actionId.startsWith is not a function
at scripts/smoke.mjs:1084
exit=1
```

The new script syntax loads successfully. T1 added smoke assertions for the v2 schema, the three Dragon14 collider references, the fixed-capacity editor, and the no-shape-copy help contract. The unrelated ActionSet fixture was not modified because it is outside the T1 owner boundary.

### Unity script gate

`validate_unity_scripts` final result:

```text
outcome=passed
refresh requested/completed=true/true
compilation requested/started/finished=true/true/true
domain_reload_observed=true
editor_ready=true
console.error_count=0
```

The reported warnings are pre-existing obsolete Runtime AI Planner property usages; no T1 compile warning or error was reported.

### Unity EditMode focused filter

Filter:

```text
MxFramework.Tests.CharacterBodyPresenceV2RuntimeBindingTests
```

Final raw summary:

```text
job=cd55ab112cd34afe8cc1e9845fd0285e
status=succeeded
total=3 passed=3 failed=0 skipped=0
durationSeconds=0.4634216
```

Passed cases cover v1 zero-source compatibility, v7 duplicate/missing/hash fail-closed behavior, and the formal Dragon14 v7 three-reference binding.

## Dragon14 formal generation

The revision-gated Body Presence CLI save accepted revision 2 with exactly:

```text
presence.animated.head     -> hurt.head.obb.0
presence.animated.tail_mid -> hurt.tail_mid.capsule.0
presence.animated.tail_tip -> hurt.tail_tip.capsule.0
skin=50mm pressureScale=1000 enabled=true
```

Focused validate result:

```text
status=ready
warning=CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED (pre-existing WarningOnly)
```

Two independent compiler outputs produced:

```text
format=mx.characterGeometryBinding.v7
animatedHurtboxSources=3
geometryBindingHash A=sha256:f53f8f91562f53fa3480c129fb7a59bdd3dd63563449cf93863c21e91ec15b50
geometryBindingHash B=sha256:f53f8f91562f53fa3480c129fb7a59bdd3dd63563449cf93863c21e91ec15b50
equal=true
```

Formal `character import-unity` wrote the generated v7 binding through the importer:

```text
report status=WarningOnly
canWrite=true canSpawn=true
added=0 updated=17 skipped=32 conflicts=0 errors=0
```

The composite CLI process returned exit 1 because its separate global resource-mapping sync reported `RESOURCE_MAPPING_BUILD_TARGET_REQUIRED`. The character importer still completed and wrote the formal generated outputs; the missing global build-target report is unrelated to Body Presence and was not repaired in T1.

## Remaining limits

- CharacterStudio full smoke remains blocked by the unrelated ActionSet fixture at line 1084.
- T2 is still required before these references cause any runtime contact or pressure.
- T3 remains responsible for gameplay, rollback/network, performance, and Playable acceptance.

## Additive v7 consumer compatibility repair

T3 formal network/replay validation exposed two CharacterTest consumers that still treated v6 as the newest geometry contract. The repair remained inside the explicitly returned T1 owner:

- `CharacterTestNetworkSessionFactory` now accepts v7 and requires authored Body Presence revision/hash for v7. Its v7 branch continues to validate the existing Body-Part Damage and Authority Pose Rig identities. v1-v6 behavior is unchanged; in particular, strict v5/v6 compatibility Presence remains accepted.
- `CharacterTestCharacterBodyPresenceSystem` now accepts v7 authored Body Presence, so Dragon14 retains its three existing ModelRoot Presence volumes. v7 does not enter the v5/v6 compatibility branch.
- Runtime v7 source shape/PartId/capacity/hash validation remains owned by `CharacterImportedPackageJson.BodyPresenceV2`; neither consumer duplicates it.

The production scan found no other `CharacterTest` runtime v6-only consumer. Remaining v6 matches are compatibility branches or synthetic compatibility fixtures.

Final repair script gate:

```text
job=a31bf58975c04eb5a1b850bd177e665f
outcome=passed
refresh/compile/domain reload/editor ready=true
console.error_count=0
```

Final focused aggregate:

```text
job=6fdd1fdd54794b24bef7bec58f5d29bc
total=30 passed=30 failed=0 skipped=0
durationSeconds=10.3546808
```

The aggregate covered the T1 RuntimeBinding fixture, M1 Presence runtime/authority regression, the two v7 Authority Pose network identity cases, T2 soft-contact core, and T2 network rollback/hash tests.

An earlier broad 61-test diagnostic run also surfaced unrelated pre-existing schema/replay assertions: two `CharacterTestAuthorityPoseNetworkTests` schema 32/33 expectations and four `CharacterTestReplayTests` projection/pinned-card expectations. They were left for T3 as instructed. Its one T1-related failure was the old test treating geometry v7 as a future version; that fixture now uses v8 and passes.
