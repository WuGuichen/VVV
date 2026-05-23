# ADR-003: Character Gameplay Runtime Bootstrap

Date: 2026-05-23

Status: Proposed

## Context

Character runtime currently has two useful but separate paths:

- `CharacterRuntimeResourceBootstrap` loads imported character resources, warms up animation artifacts, instantiates the prefab view, binds default equipment, and initializes local controller binding.
- `GameplayComponentRuntimeShowcase` validates the command-driven `RuntimeHost -> RuntimeCommandBuffer -> GameplayRuntimeModule -> GameplayComponentWorld` path for spawn definitions, ability rules, targeting, runtime hash, and SaveState.
- `CharacterRuntimeSpawnResolver` already resolves package data into `CharacterRuntimeBinding`, `CharacterGameplayRegistrationPlan`, `CharacterCombatBodyBindingPlan`, `CharacterWeaponAttachmentBindingPlan`, and `CharacterResourcePreloadBindingPlan`.
- `CharacterGameplayRegistrationPlan` is still intentionally deferred: it carries a planned `GameplayEntityId`, but `willCreateEntity=false`, so no live `GameplayComponentWorld` character entity is created by the character runtime path yet.
- `CharacterControl`, Combat motion/action, Runtime AI Planner adapters, MxAnimation presentation, and Debug UI snapshots exist as framework capabilities, but they do not yet consume one live character runtime registry.

The gap is architectural rather than a missing MonoBehaviour hook. The framework needs one official character runtime composition root that turns a compiled/imported character package into a live component entity, then attaches view, Combat proxy, Character Control, Animation, SaveState restore, and diagnostics around that entity without duplicating source of truth.

## Decision

Adopt a separate `CharacterGameplayRuntimeBootstrap` path instead of expanding `CharacterRuntimeResourceBootstrap` into the full runtime composition root.

`CharacterRuntimeResourceBootstrap` remains a resource, prefab preview, importer validation, animation warmup, and regression entry. It must not become the owner of authoritative Gameplay state, Combat body lifecycle, Character Control state machine orchestration, SaveState restore, Debug UI aggregation, and Unity view construction at the same time.

The official character gameplay runtime path should be built around these source-of-truth rules:

| Concern | Owner | Notes |
| --- | --- | --- |
| Gameplay identity, team, lifecycle, attributes, buffs, modifiers, cooldowns, ability state | `GameplayComponentWorld` | One authoritative component runtime state. No dual write with prefab fields, `RuntimeEntity`, or Combat. |
| Entity creation and destruction | `RuntimeCommandBuffer` + `GameplayRuntimeModule` systems | Character runtime enqueues commands and observes resulting `GameplayEntityId`; it does not mutate component stores behind command systems except inside registered initializers/systems. |
| Stable character package identity | Character runtime plan / registry | Stable package ids map to live runtime ids, but do not replace generation-safe `GameplayEntityId`. |
| Unity prefab / GameObject | View and presentation only | Instantiated after or alongside live entity registration; it reads binding data and displays state, but does not own HP, Buff, Cooldown, or Lifecycle. |
| Combat proxy ids, bodies, colliders, and deterministic spatial queries | Combat bridge and shared `CombatPhysicsWorld` | Combat owns deterministic query/motion data only. Gameplay remains the owner of character gameplay state. |
| Character Control state machine and command sources | `CharacterControl` bridge | It translates input, Runtime AI Planner, action, and reaction into commands/events against mapped live ids; it does not own Gameplay state. |
| MxAnimation / Animator / Playables | Presentation adapter/backend | Animation consumes control/combat/view snapshots. Root motion or Playables time must not drive authoritative Gameplay or Combat state backward. |
| SaveState restore | Gameplay SaveState provider plus character restore orchestrator | SaveState restores component world state first, then rebuilds runtime registry, Combat proxies, views, and controller bindings from live ids. |
| Debug UI | Read-only adapters | Debug UI consumes snapshots from the registry, Gameplay diagnostics, Combat bridge, Character Control, Animation, and resource/runtime plans. |

## API Reuse Plan

The implementation slices must reuse existing modules before adding new extension points:

| Existing capability | Required reuse |
| --- | --- |
| Runtime | Use `RuntimeHost`, `RuntimeCommandBuffer`, runtime frame ownership, runtime events, runtime hash, and SaveState contracts. |
| Gameplay | Use `GameplayComponentWorld`, generation-safe `GameplayEntityId`, `GameplayComponentSpawnDefinition`, `GameplayComponentSpawnRegistry`, `GameplayComponentAbilityRegistry`, command systems, schemas, diagnostics, hash, and SaveState providers. |
| Resources | Use `CharacterResourcePreloadBindingPlan`, `ResourceKey`, catalog/provider mappings, preload/warmup services, and existing Unity resource adapters. |
| Character authoring/import | Use compiled/imported package data, `CharacterRuntimeSpawnResolver`, `CharacterRuntimeBinding`, resolved profile, resource mapping hashes, geometry binding, and package diagnostics. |
| Combat | Use existing Combat ids, body/collider plans, `CombatPhysicsWorld`, motion/query/action contracts, and Gameplay bridge patterns. |
| Character Control | Use `CharacterControlEntityRef`, command sources, state machine, motion/action/reaction bridges, Runtime AI Planner command source, and animation adapter contracts. |
| Animation | Use MxAnimation compiled artifacts, warmup service, presentation adapter, and Unity backend injection from the composition root. |
| Debug UI | Use existing read-only adapter direction; do not make Runtime, Gameplay, Combat, Resources, or Character Control depend on Debug UI. |

Bypass decisions:

- Do not create a second per-character `GameplayComponentWorld`; it breaks shared targeting, SaveState, Replay, Debug UI, Combat bridge, and runtime scheduling.
- Do not make Unity prefab state authoritative; prefab fields are view/config inputs only.
- Do not store Gameplay HP, Buff, Cooldown, or Lifecycle in Combat; Combat exposes spatial/motion/action facts and deterministic query results.
- Do not use `CharacterRuntimeResourceBootstrap` as a long-term all-in-one composition root; it remains useful for preview/importer regression but has the wrong responsibility shape for the formal runtime.

## Runtime Assembly Model

The character runtime implementation should introduce the following minimal infrastructure:

| Type | Module | Responsibility |
| --- | --- | --- |
| `CharacterRuntimePlanBundle` | `Character.RuntimeSpawn` | Immutable aggregate of binding, spawn recipe, ability recipe, combat recipe, presentation recipe, resource preload plan, and diagnostics. |
| `CharacterGameplaySpawnDefinitionBuilder` | `Character.RuntimeSpawn` or Gameplay bridge namespace | Converts `CharacterRuntimeBinding` / `CharacterResolvedProfile` into `GameplayComponentSpawnDefinition` initializers and validation diagnostics. |
| `CharacterGameplayAbilityRegistryBuilder` | `Character.RuntimeSpawn` or Gameplay bridge namespace | Maps character loadout / granted ability data into `GameplayComponentAbilityRegistry` definitions or explicit deferred diagnostics when data is not expressible yet. |
| `CharacterGameplayRuntimeBootstrap` | `Character.RuntimeSpawn` or `Character.RuntimeSpawn.Unity` composition layer | Owns `RuntimeHost`, `RuntimeCommandBuffer`, `GameplayRuntimeModule`, shared `GameplayComponentWorld`, spawn registry, ability registry, and tick lifecycle. |
| `CharacterRuntimeEntityRegistry` | noEngine runtime layer | Maps `stableCharacterId <-> GameplayEntityId <-> CombatEntityId/CombatBodyId <-> view handle <-> CharacterControlEntityRef`. |
| `CharacterRuntimeViewFactory` | Unity adapter layer | Loads resources, instantiates prefab views, binds default weapons, configures animation backend, and writes view-only bindings from registry ids. |
| `CharacterGameplayCombatProxyBridge` | Combat bridge layer | Registers, updates, and removes Combat bodies/colliders from `CharacterCombatBodyBindingPlan` and live registry mappings. |
| `CharacterGameplayControlBridge` | Character Control bridge layer | Connects input, Runtime AI Planner, action, reaction, and Gameplay command/event flow through `CharacterControlEntityRef`. |
| `CharacterRuntimeRestoreOrchestrator` | runtime composition layer | Rebuilds registry, Combat proxies, views, and control bindings after `GameplayComponentWorldSaveStateProvider` restore. |

The first implementation task should keep this list minimal and avoid speculative editor/UI types. New public APIs should be added only when a slice needs them and must update `Docs/Interfaces/Gameplay.md`, `Docs/Interfaces/CharacterControl.md`, or the relevant module docs in the same PR.

## Live Id Lifecycle

The official flow is:

1. Resolve imported package data with `CharacterRuntimeSpawnResolver`.
2. Build a `CharacterRuntimePlanBundle` with deterministic spawn, ability, resource, Combat, and presentation recipes.
3. Register or update `GameplayComponentSpawnDefinition` and ability definitions in explicit registries.
4. Enqueue a component spawn command through `RuntimeCommandBuffer`.
5. Tick `GameplayRuntimeModule` through `RuntimeHost`.
6. Read the resulting live `GameplayEntityId` from runtime events or an explicit spawn result adapter.
7. Register `stableCharacterId`, `GameplayEntityId`, Combat ids, view handle, and `CharacterControlEntityRef` in `CharacterRuntimeEntityRegistry`.
8. Instantiate/bind Unity view and animation backend as presentation for the live registry entry.
9. Register Combat bodies/colliders from the combat binding plan.
10. Bind Character Control command sources and bridges through the registry entry.

Destroy must follow the reverse ownership order:

1. Stop command sources and view bindings.
2. Remove Character Control bridge references.
3. Remove Combat bodies/colliders.
4. Enqueue or run Gameplay lifecycle destruction and let `GameplayComponentWorld.DestroyEntity` clean registered component stores.
5. Release resource handles and Unity view instances.
6. Remove the registry entry.

SaveState restore must not deserialize Unity view objects or Combat bodies as authoritative state. It restores `GameplayComponentWorld` first, then reconstructs registry mappings and presentation/proxy layers from stable character ids and recipes.

## Consequences

Benefits:

- Gameplay authoritative state has one owner: `GameplayComponentWorld`.
- Character package import, runtime entity creation, Unity view instantiation, Combat proxy registration, Character Control, Animation, SaveState, and Debug UI get explicit seams.
- `CharacterRuntimeResourceBootstrap` remains useful and smaller instead of becoming a god-object MonoBehaviour.
- The next implementation issue can start with a focused foundation: spawn definitions, bootstrap, and registry.
- Future Playable / SaveState / Debug UI work can attach to the same live registry rather than inventing per-feature lookups.

Costs:

- The first implementation slice adds several public or semi-public runtime types.
- Existing tests that assert deferred `CharacterGameplayRegistrationPlan` behavior must be updated only when live spawn is implemented.
- A registry introduces lifecycle responsibility: stale ids, restore ordering, and cleanup must be tested explicitly.
- Unity-facing slices must still use Unity Editor / Unity MCP / existing Editor menu paths for serialized assets; no hand-written scene/prefab YAML.

Follow-up tasks are already split in the milestone:

- #408: implement character Gameplay runtime foundation: spawn definitions, bootstrap, and live entity registry.
- #410: implement live view binding, Combat proxy, and Character Control bridge.
- #412: close out SaveState restore and diagnostics.

## Alternatives Considered

- Option: Keep expanding `CharacterRuntimeResourceBootstrap`.
- Reason not chosen: it centralizes unrelated responsibilities in one MonoBehaviour and makes authoritative state boundaries hard to audit.

- Option: Create one local `GameplayComponentWorld` per character prefab.
- Reason not chosen: it breaks shared runtime scheduling, multi-character targeting, SaveState, Replay, Debug UI, Combat bridge, and the existing `GameplayRuntimeModule` single-drain model.

- Option: Let Combat own the runtime character identity and mirror to Gameplay.
- Reason not chosen: Combat is the deterministic motion/query/action layer, not the owner of HP, Buff, Cooldown, Lifecycle, ability rules, or Gameplay SaveState.

## References

- Issue: #407
- Follow-up issues: #408, #410, #412
- Related docs:
  - `Docs/Interfaces/Gameplay.md`
  - `Docs/Interfaces/CharacterControl.md`
  - `Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01.md`
  - `Docs/Tasks/GAMEPLAY_COMPONENT_PLAYABLE_COMBAT_BRIDGE_PLAN_01.md`
  - `Docs/CHARACTER_RESOURCE_PACKAGE_AUTHORING.md`
