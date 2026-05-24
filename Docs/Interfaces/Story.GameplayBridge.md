# Story.GameplayBridge 接口

> 状态：S0 Proposed Contract。本文固定 Story 与 Gameplay/Attributes/Buffs/Modifiers 的桥接边界；S3 实现关闭前，不代表仓库已有可用 API。

## 职责

`MxFramework.Story.GameplayBridge` connects Story Runtime to Gameplay-owned state without making Story core depend on Gameplay.

It may:

- evaluate Story conditions from Gameplay state through adapters.
- translate Story effect intents into Gameplay-owned `RuntimeCommand` values.
- locate Gameplay entities or component entities through stable ids/handles.
- enqueue Gameplay commands into the Gameplay command buffer owned by `GameplayRuntimeModule`.

It must not:

- drain the Gameplay command buffer.
- mutate `GameplayComponentWorld` stores directly from Story.
- call `IBuffPipeline.AddBuff`, `IAttributeOwner.SetAttribute`, `IAttributeOwner.AddAttribute`, `ModifierPipeline.ApplyAll`, or other Gameplay mutation APIs directly as a Story effect.
- store direct references to Gameplay component stores or Unity objects inside Story state.

## Dependencies

```text
MxFramework.Story.GameplayBridge
  -> MxFramework.Story.Runtime
  -> MxFramework.Gameplay
  -> MxFramework.Attributes
  -> MxFramework.Buffs
  -> MxFramework.Modifiers
```

No UnityEngine or UnityEditor dependency.

## Condition Categories

Pure Story condition:

```csharp
public interface IStoryCondition
{
    bool Evaluate(in StoryEvaluationContext context);
}
```

Pure conditions read Story metadata and blackboard only.

Gameplay bridge condition:

```csharp
public sealed class StoryModifierConditionAdapter : IStoryCondition
{
}
```

Adapter behavior:

- wraps an `IModifierCondition`.
- constructs a temporary `ModifierContext` from `StoryEvaluationContext` using an injected resolver.
- resolves `Target`, `Buffs`, `Counters`, parameters, compare ids, and source explicitly.
- returns the adapted condition result.
- releases pooled `ModifierContext` if used.
- does not execute `IModifierEffect`.
- does not mutate Story or Gameplay state.

If the required Gameplay target cannot be resolved, the adapter returns false and emits diagnostics through the bridge snapshot/event policy, not through Story core exceptions.

## Gameplay Entity References

Story should not store raw references to Gameplay objects. It uses stable refs:

```csharp
public readonly struct StoryGameplayEntityRef
{
    public readonly int Kind;
    public readonly int Id0;
    public readonly int Id1;
}
```

Suggested `Kind` values:

| Kind | Meaning |
| ---: | --- |
| `0` | None |
| `1` | Legacy `RuntimeEntity` integer id |
| `2` | `GameplayEntityId` packed as index/generation |
| `3` | Project-defined stable entity handle |

The bridge owns conversion between `StoryGameplayEntityRef` and live Gameplay runtime ids. Stale or invalid refs must be rejected without mutating state.

## Effect Intent

Story side effect declarations produce intent, not direct mutation:

```csharp
public readonly struct StoryGameplayEffectIntent
{
    public readonly int CommandId;
    public readonly int SourceId;
    public readonly int TargetId;
    public readonly int Payload0;
    public readonly int Payload1;
    public readonly int Payload2;
    public readonly int DelayFrames;
    public readonly StoryGameplayEntityRef TargetRef;
}
```

Bridge behavior:

- validate the intent.
- map `TargetRef` to Gameplay command target/payload ids.
- enqueue a Gameplay `RuntimeCommand` into the Gameplay command buffer.
- return a structured result for diagnostics.

If there is no Gameplay command capable of representing an effect, the bridge must reject the intent and emit diagnostics. It must not fall back to direct mutation.

## Current Gameplay Command Gap

Existing Gameplay commands cover component entity create/destroy, spawn, attribute set/add, ability cast, ability request, and lifecycle/despawn flows. There is currently no general `AddComponentBuff` command.

Therefore Story S3 must choose one of these implementation paths before claiming Story can grant buffs:

1. Add a Gameplay-owned `AddComponentBuff` / `RemoveComponentBuff` command system.
2. Represent buff application through a configured Gameplay ability and enqueue `CastComponentAbility` / request commands.
3. Limit Story S3 effects to commands already supported, such as component attribute changes, and defer buff application.

The default recommendation is option 1 if framework-level story rewards need direct buff semantics. The command and system belong to Gameplay, not Story.

## Command Buffer Ownership

Story.GameplayBridge receives a Gameplay command buffer reference from the composition root:

```text
StoryRuntimeModule
  -> StoryGameplayBridge.EnqueueGameplayEffects(...)
  -> GameplayCommandBuffer.Enqueue(...)
  -> GameplayRuntimeModule drains later
```

Rules:

- The bridge does not own frame clocks.
- The bridge does not call `DrainForFrame`.
- The bridge must document whether it enqueues for current frame or `currentFrame + DelayFrames`.
- Same-frame effects require Story Runtime priority earlier than Gameplay Runtime priority.

## Locator

`StoryBeatGameplayLocator` maps Story beat/trigger ids to Gameplay entities or component entities.

Rules:

- Locator stores stable ids/refs, not direct store references.
- Locator can be rebuilt from SaveState, config, or composition root inputs.
- Locator results are not part of Story core SaveState unless explicitly represented as stable ids.
- Locator diagnostics must distinguish missing config, stale entity, destroyed entity, and unsupported ref kind.

## Diagnostics

The bridge should expose a read-only debug snapshot with:

- resolved / unresolved entity ref counts.
- recent rejected effect intents.
- recent condition adapter failures.
- Gameplay command enqueue results.
- current same-frame / delayed effect policy.

Diagnostics are not authority state and do not enter Story hash unless S3 explicitly documents a stable diagnostic hash field.

## Test Entry

Planned S3 tests:

- `StoryModifierConditionAdapter` creates correct `ModifierContext` and releases pooled context.
- missing Gameplay target returns false and diagnostic issue.
- effect intent maps to existing Gameplay command without direct mutation.
- unsupported buff effect is rejected until Gameplay command exists.
- same-frame ordering test with separate Story and Gameplay command buffers.

See `Docs/Tasks/STORY_S1.md` for the S1 prerequisite runtime slice.
