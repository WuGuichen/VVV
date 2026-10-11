# LCC-M3-T1 implementation evidence — 2026-08-25

> Scope: optional Severed authoring/binding fields, Dragon14 tail fixture, CharacterTest authority reducer/event seams, CharacterStudio body-part editing, and the frozen focused gates.
>
> State: `Verified`; main-agent integration review confirmed scope, generated identity, focused job results, Unity validation, and the T2/T4/T6 handoff boundaries.

## Implemented contract

- The existing `mx.characterBodyPartDamage.v1` / `1.0` document is extended in place with only `severable`, `severRequiredTag`, and `presentationEventId`. Missing extension fields retain the M2 non-severable behavior; no second severance schema, detached resource key, policy graph, serializer, repository, or generic state-machine abstraction was added.
- Compiler output emits all three fields for every definition. RuntimeSpawn accepts either all three fields or none for each definition, verifies the matching new or legacy canonical hash, and rejects partial, invalid, duplicate-event, malformed, and hash-mismatched bindings.
- `Intact -> Broken -> Severed` is one-way. A sever fact is a typed, bounded authority input; it is effective only when the frame-start snapshot was already `Broken` and its explicit tag exactly matches. Damage that creates `Broken` and a sever fact in the same frame commits only `Broken`; a new fact in the next authority frame may commit `Severed`.
- Exact duplicate sever facts reduce to one transition. Further facts after `Severed` produce no transition. The transient presentation projection exposes target entity, `partStateId`, committed state revision, and `presentationEventId`; authority restore clears projections rather than replaying them.
- Stable read-only lookups expose member BodyPart filter status and `partStateId` dependency status. Disabled members reuse existing `memberPartIds`; later Zone/Anchor topology derives from existing relationships, and T4 action/trace dependencies reference `partStateId` directly.
- Dragon14 `body_part_damage.json` was saved through the existing expected-revision transaction at document revision 2. Head and both wings are explicitly non-severable; only `partstate.000003.tail` is severable with tag `action.000003.tail.sever` and event `event.000003.tail.severed`.
- CharacterStudio reuses the existing dedicated body-part API, draft, Undo/Redo, refresh, discard, and save/reopen paths. The inspector normalizes and edits the three optional fields and describes the fixed frame-start-Broken plus typed-fact policy; no UI-private schema was introduced.

## Formal authoring and generated identity

- Revision-gated save: exit 0; reopened state `Current`, write-eligible, revision 2.
- Authoring source SHA-256: `b772a70d9a3aa64fac39a70de6c39a42c540e8862823fbb641ce2790bc115492`.
- Generated body-part content identity: `sha256:38f8f91475d263480b06bdf86da8a6f3f0527e387101137335861e1b8a582adb`.
- `config/geometry_binding.json` SHA-256: `f3b0caee9e1702c5b0ca82d3091819edc671571ece0b10b2bb2e5ac4be91b549`.
- `generated/character_geometry_binding.json` SHA-256: `ea49bf230496bd68465cb991ae72aa3a5b40bf2c83ed5f6b2057760826cdda53`.
- Both generated bindings report `bodyPartDamageRevision=2`, four definitions, and all three extension properties on every definition; only the tail has non-empty sever data.

The required generator command used only the repository generator and reported `status=WarningOnly`, `canWrite=True`, `canSpawn=True`, 49 artifacts, 5 updated, 44 skipped, 0 conflicts and 0 errors. Its process exit was 1 because the current Unity global resource build report has no concrete build target (`RESOURCE_MAPPING_BUILD_TARGET_REQUIRED`). The five reported updates were the two geometry bindings, `package_cache/compile_result.json`, `package_cache/unity_import_write_plan.json`, and `config/unity_resource_catalog.json`. This existing post-import ResourceMapping condition is not represented as a successful full import, but it did not prevent the generator-owned body-part outputs from being written and verified.

## Focused results

| Gate | Raw result |
| --- | --- |
| Authoring `character-body-part-damage` | exit 0; `Character body-part-damage authoring tests passed.` |
| CLI `character body-part-damage validate` | exit 0; `status=ready`; only existing `CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED` warning |
| CharacterStudio syntax + smoke | bundled Node exit 0; `CharacterStudio smoke ok` |
| Unity script validator | job `448afa097b3e4ed2b6133e5968b364ff`; refresh completed, compilation started/finished, domain reload observed, editor ready, Console error count 0 |
| RuntimeSpawn focused class | EditMode job `47341ea8aba04ae1bfe4eeaf34541a75`; 5/5 passed, 0 failed/skipped, 0.260 s |
| BodyPartState focused class | EditMode job `aa149fe612b84ebd963e07c8c7d4e139`; 9/9 passed, 0 failed/skipped, 0.302 s |
| Scoped diff check | exit 0; only Git line-ending conversion warnings for generated JSON/Studio smoke |

Main-agent integration retrieved the two final Unity jobs directly: `47341ea8aba04ae1bfe4eeaf34541a75` is `succeeded` with 5/5, and `aa149fe612b84ebd963e07c8c7d4e139` is `succeeded` with 9/9. Hashes and generated extension rows were independently re-read from disk; `git diff --check` remained exit 0. No extra task-level review round or broad test suite was added.

The Unity validator call immediately before the successful job disconnected during domain reload and is not counted as evidence. The repeated call above is the complete result.

Focused coverage includes legacy v5 definitions without optional fields, all-or-none extension parsing, canonical/hash identity, invalid and duplicate presentation events, Dragon14 tail-only data, frame-start gating, same-frame Broken denial, next-frame Severed, exact-fact dedupe, one presentation projection, restore clearing, stable filter/dependency lookup, respawn/reinitialize reset, death/revive retention, and the existing warmed 4096-iteration reducer allocation assertion.

## Files and handoff

- Authoring/compiler: `CharacterAuthoringBodyPartDamage.cs`, `CharacterPackageValidation.BodyPartDamage.cs`, the body-part region of `CharacterAuthoringCompiler.cs`, and `CharacterPackageTests.BodyPartDamage.cs`.
- Runtime binding/state: `CharacterRuntimeSpawnContracts.cs`, the body-part parser/hash region of `CharacterImportedPackageJson.cs`, `CharacterTestBodyPartStateComponent.cs`, `CharacterTestBodyPartDamageAuthorityBridge.cs`, and their two frozen focused test classes.
- Formal/UI: Dragon14 `geometry/body_part_damage.json`, generator-owned outputs listed above, `CharacterStudio/web/src/inspector.js`, and `CharacterStudio/scripts/smoke.mjs`.
- Documentation: this evidence and the LCC-M3-T1 task record only. Parent milestone status and T2-T6 task documents were not changed.

T2 may consume `GetBodyPartFilterStatus(bodyPartId)` after the authority-frame commit to filter stable Hurtbox leaves. T4 may consume `GetPartStateDependencyStatus(partStateId)` and the sever transition projection; it must not infer dependencies from free-form damage tags.

## Deliberate limits

- No T2 Hurtbox/compound topology, T3 Zone/Anchor/target invalidation, T4 production action/trace caller, T5 mesh slicing/presentation resource, T6 schema/network/replay serialization, or detached limb resource was implemented.
- No full-repository test suite, PlayMode fixture, direct browser capture, or independent final review was run for T1. The frozen focused filters and default Unity script gate are the only acceptance claims here.
- Formal network remains on schema 30 until T6. The authority reducer has the runtime state and event witness seams, but production network persistence and late-join replay are not claimed.
- Because the existing schema-30 serializer stores the structure enum as an integer, T6 must explicitly ensure schema `<=30` cannot accept or emit `Severed`; schema31 is the first formal Severed network contract. Until T4 wires a production sever caller and T6 closes this gate, T1 is a bounded authority seam rather than a release-ready network feature.
