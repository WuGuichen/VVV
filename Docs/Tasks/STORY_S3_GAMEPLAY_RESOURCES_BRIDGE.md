# STORY_S3_GAMEPLAY_RESOURCES_BRIDGE

## Goal

Implement the Story S3 bridge slice for Issue #439:

- `MxFramework.Story.GameplayBridge`
- `MxFramework.Story.ResourcesBridge`

This task connects Story to Gameplay and Resources through explicit bridge modules. Story core and Story.Runtime must remain independent from Gameplay, Resources, Unity, Editor, Buffs, Attributes, and Modifiers.

## Workflow Level

- Issue: `#439`
- Task level: `S2`
- Suggested branch: `feature/439-story-bridges`
- Required PR: yes
- Merge policy: implementation agent opens PR and does not merge it.

## Required Reading

1. `AGENTS.md`
2. `Docs/PROJECT_INDEX.md`
3. `Docs/README.md`
4. `Docs/WORKFLOW.md`
5. `Docs/QUALITY_GATE.md`
6. `Docs/Decisions/ADR-004-story-module-scope.md`
7. `Docs/Decisions/ADR-005-story-runtime-command-boundary.md`
8. `Docs/Interfaces/Story.md`
9. `Docs/Interfaces/Story.Runtime.md`
10. `Docs/Interfaces/Story.Config.md`
11. `Docs/Interfaces/Story.GameplayBridge.md`
12. `Docs/Interfaces/Gameplay.md`
13. `Docs/Interfaces/Attributes.md`
14. `Docs/Interfaces/Buffs.md`
15. `Docs/Interfaces/Modifiers.md`
16. `Docs/Interfaces/Resources.md`
17. This task document

## API Reuse Plan

| Need | Existing API / Module | Use in S3 | Notes |
| --- | --- | --- | --- |
| Story authority state | `StoryDirector`, `StoryEvaluationContext`, Story DTOs | Yes | Bridge reads Story ids and metadata; it does not add Gameplay references to Story state. |
| Story command loop | `StoryRuntimeModule`, `RuntimeCommandBuffer` | Read boundary only | Bridge may receive Story timing/context, but it does not drain Story commands. |
| Gameplay command ownership | `RuntimeCommand`, `RuntimeCommandBuffer`, `GameplayRuntimeCommandFactory`, Gameplay command systems | Yes | Bridge enqueues Gameplay-owned commands into a Gameplay-owned buffer. |
| Gameplay entity identity | `GameplayEntityId`, component runtime ids, existing runtime entity ids where needed | Yes | Story stores stable refs only, never live store/object references. |
| Modifier condition evaluation | `IModifierCondition`, `ModifierContext` | Yes | Use a temporary context built from explicit resolver inputs. |
| Attributes / Buffs / Modifiers mutation | Gameplay-owned command systems | Limited | No direct Story mutation. Buff grant is explicitly deferred unless an existing Gameplay command already expresses it. |
| Resource preload | `ResourceKey`, `ResourcePreloadPlan`, labels | Yes | ResourcesBridge produces plans only; it does not load or release resources. |
| Unity scene / prefab / trigger | Unity and Editor modules | No | S4 owns Unity adapters. |
| Runtime AI Planner | Runtime AI Planner bridge | No | S5 owns planner projection and playable demo. |

## Implementation Strategy

### Gameplay Effect Strategy

S3 chooses **explicit defer for direct buff grant/remove semantics**.

The bridge may translate Story effect intents only when the intent can be represented by an existing Gameplay-owned command contract. Examples include component ability cast or component attribute commands if the current Gameplay API already exposes a stable command factory for them.

The bridge must reject unsupported buff-style effects with a structured result and diagnostics. It must not:

- call `IBuffPipeline.AddBuff` directly.
- call Attributes / Buffs / Modifiers mutation APIs directly.
- add a new Gameplay buff command unless the implementation discovers that a very small Gameplay-owned command already fits existing command-system patterns and documents the impact.

If buff grant is still required after S3, create a follow-up issue for a Gameplay-owned buff command/system.

### Same-Frame Policy

Default enqueue policy:

```text
targetFrame = currentStoryFrame + max(0, intent.DelayFrames)
```

`DelayFrames == 0` means same-frame enqueue is allowed, but the composition root must tick Story before Gameplay for the Gameplay module to consume the command in the same frame. The bridge must document this ordering and tests must cover current-frame and delayed command enqueue behavior without sharing drain ownership.

### Bridge Ownership

`Story.GameplayBridge` owns:

- stable Story-to-Gameplay refs.
- conversion from Story context to temporary Modifier context.
- effect intent validation and command enqueue.
- locator diagnostics for missing/stale entities.

It does not own:

- Gameplay command buffer drain.
- Gameplay system pipeline execution.
- StoryDirector graph advancement.
- SaveState authority for live Gameplay stores.

`Story.ResourcesBridge` owns:

- Story resource metadata DTOs.
- deterministic conversion into `ResourcePreloadPlan`.
- validation and diagnostics for invalid resource keys / labels.

It does not own:

- `IResourceManager.Load`.
- `ResourcePreloadService` execution.
- Unity provider selection.
- Catalog mounting.

## Allowed Changes

- `Assets/Scripts/MxFramework/Story.GameplayBridge/**`
- `Assets/Scripts/MxFramework/Story.ResourcesBridge/**`
- `Assets/Scripts/MxFramework/Tests/Story.GameplayBridge/**`
- `Assets/Scripts/MxFramework/Tests/Story.ResourcesBridge/**`
- Matching asmdefs and metas created by Unity / normal file creation.
- `Docs/Interfaces/Story.GameplayBridge.md`
- `Docs/Interfaces/Story.ResourcesBridge.md`
- `Docs/Interfaces/Gameplay.md` only if a new Gameplay-owned command is added.
- `Docs/INTERFACES.md`
- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- This task document.

## Out Of Scope

- Story core direct dependencies on Gameplay, Attributes, Buffs, Modifiers, Resources, UnityEngine, or UnityEditor.
- Story.Runtime dependencies on Gameplay or Resources.
- Unity trigger-zone components, Timeline, Cinemachine, UI Toolkit views, or Editor debug windows.
- Playable demo scenes.
- Runtime AI Planner projection.
- Yarn / Ink / Articy importers.
- WGame-specific story, buff, attribute, entity, or localization data.

## Suggested Slice Order

1. Create noEngine asmdefs for `Story.GameplayBridge` and `Story.ResourcesBridge`.
2. Implement stable DTO/result types and diagnostics before adapters.
3. Implement `StoryBeatGameplayLocator` using stable refs only.
4. Implement `StoryModifierConditionAdapter` with temporary context creation and missing-target failure behavior.
5. Implement effect intent validation and Gameplay command enqueue for existing supported commands.
6. Implement unsupported buff/effect rejection path and document the chosen defer strategy.
7. Implement ResourcesBridge metadata-to-`ResourcePreloadPlan` mapping.
8. Add focused tests for each behavior and command-buffer ownership.
9. Update interface docs, capabilities, and usage.
10. Run Unity compile and targeted EditMode tests.

## Acceptance Criteria

- `Story.GameplayBridge` and `Story.ResourcesBridge` compile as noEngine assemblies.
- Story core and Story.Runtime asmdefs remain free of Gameplay / Resources / Unity dependencies.
- `StoryModifierConditionAdapter` builds temporary Modifier context from explicit resolver data and does not retain mutable Gameplay refs.
- Missing or stale gameplay entity refs fail safely and produce diagnostics.
- Effect intents enqueue Gameplay-owned `RuntimeCommand` values into the provided Gameplay buffer.
- The bridge does not drain any Gameplay command buffer.
- Unsupported buff grant/remove effects are rejected and documented as deferred.
- Same-frame vs delayed enqueue policy is documented and covered by tests.
- `StoryBeatGameplayLocator` uses stable refs, not direct object/store references.
- ResourcesBridge produces deterministic `ResourcePreloadPlan` values from Story metadata.
- ResourcesBridge does not load resources or depend on Unity providers.
- Docs include `Docs/Interfaces/Story.ResourcesBridge.md` and update index/capability/usage entries.

## Suggested Tests

- `StoryBeatGameplayLocatorTests.ResolvesStableComponentEntityRef`
- `StoryBeatGameplayLocatorTests.MissingEntityReturnsDiagnostic`
- `StoryModifierConditionAdapterTests.MissingTargetReturnsFalse`
- `StoryModifierConditionAdapterTests.BuildsTemporaryModifierContext`
- `StoryGameplayEffectBridgeTests.EnqueuesExistingGameplayCommand`
- `StoryGameplayEffectBridgeTests.RejectsUnsupportedBuffEffectWithoutMutation`
- `StoryGameplayEffectBridgeTests.DelayFramesControlsTargetCommandFrame`
- `StoryGameplayEffectBridgeTests.DoesNotDrainGameplayCommandBuffer`
- `StoryResourcesBridgeTests.BuildsPreloadPlanFromExplicitKeys`
- `StoryResourcesBridgeTests.BuildsPreloadPlanFromLabelsDeterministically`
- `StoryResourcesBridgeTests.InvalidMetadataReportsDiagnostics`

## Validation

Required:

```text
git diff --check
Unity MCP refresh/import with Console error count 0
Targeted EditMode tests for Story.GameplayBridge and Story.ResourcesBridge
```

If Gameplay command contracts change, also run the nearest Gameplay command-system tests and document the public API impact in the PR.

## Handoff Notes

The implementation agent must report:

- files read.
- files changed.
- module impact.
- public API impact.
- Docs / ADR status.
- exact validation run.
- whether any buff/effect support remains deferred to a follow-up issue.
