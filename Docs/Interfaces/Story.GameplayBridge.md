# Story.GameplayBridge 接口

> 状态：S3 最小切片已实现（2026-05-24）。本文记录 `MxFramework.Story.GameplayBridge` 当前可用的 noEngine bridge API 和仍然 deferred 的 Gameplay effect 范围。

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
  -> MxFramework.Story
  -> MxFramework.Story.Runtime
  -> MxFramework.Gameplay
  -> MxFramework.Attributes
  -> MxFramework.Buffs
  -> MxFramework.Modifiers
  -> MxFramework.Runtime
```

No UnityEngine or UnityEditor dependency.

Story core 和 Story.Runtime 不依赖 GameplayBridge；组合根显式创建 bridge，并把 Gameplay-owned `RuntimeCommandBuffer` 传入 bridge。

## Current API

| 类型 | 用途 |
| --- | --- |
| `StoryGameplayEntityRef` | Story 持有的稳定 Gameplay ref，支持 legacy runtime entity id、component `GameplayEntityId` 和 project handle 占位 |
| `StoryBeatGameplayLocator` | 将 graph/beat 或 trigger id 映射到 stable ref，并可用 `GameplayComponentWorld.IsAlive` 检查 stale component entity |
| `StoryGameplayEntityResolutionResult` | locator 结构化结果，区分 missing / stale / unsupported ref kind |
| `StoryEvaluationContext` | bridge condition 评价上下文，包含 Story ids、target ref、blackboard、frame 和 source |
| `StoryModifierContextResolver` | 从显式注册的 resolver data 创建临时 `ModifierContext` |
| `StoryModifierConditionAdapter` | 包装 `IModifierCondition`，创建临时 context 后评价；失败返回 false 并记录 diagnostic |
| `StoryGameplayEffectIntent` | Story gameplay effect intent DTO，不直接执行 mutation |
| `StoryGameplayEffectBridge` | 把支持的 intent 转成 Gameplay-owned `RuntimeCommand` 并 enqueue 到 Gameplay command buffer |
| `StoryGameplayBridgeDiagnostics` | 非权威诊断计数和最近失败记录 |

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
    public int Kind { get; }
    public int Id0 { get; }
    public int Id1 { get; }
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
    public StoryGameplayEffectIntentKind Kind { get; }
    public int CommandId { get; }
    public int SourceId { get; }
    public int TargetId { get; }
    public int Payload0 { get; }
    public int Payload1 { get; }
    public int Payload2 { get; }
    public int DelayFrames { get; }
    public StoryGameplayEntityRef TargetRef { get; }
    public string TraceId { get; }
}
```

Bridge behavior:

- validate the intent.
- map `TargetRef` to Gameplay command target/payload ids.
- enqueue a Gameplay `RuntimeCommand` into the Gameplay command buffer.
- return a structured result for diagnostics.

If there is no Gameplay command capable of representing an effect, the bridge must reject the intent and emit diagnostics. It must not fall back to direct mutation.

Current S3 supported Gameplay-owned commands:

| Intent factory | Gameplay command | Target ref kind | Payload semantics |
| --- | --- | --- | --- |
| `SetComponentAttribute(...)` | `GameplayRuntimeCommandIds.SetComponentAttribute` | `ComponentEntity` | `payload1=attributeId`, `payload2=value` |
| `AddComponentAttribute(...)` | `GameplayRuntimeCommandIds.AddComponentAttribute` | `ComponentEntity` | `payload1=attributeId`, `payload2=delta` |
| `CastComponentAbility(...)` | `GameplayRuntimeCommandIds.CastComponentAbility` | `ComponentEntity` | `payload1=abilityId` |
| `CastLegacyAbility(...)` | `GameplayRuntimeCommandIds.CastAbility` | `LegacyRuntimeEntity` | `payload1=abilityId`, `payload2=optional candidate entity id` |

This slice does not add or change Gameplay command ids, payload layouts, or Gameplay command systems.

## Deferred Buff Effects

Existing Gameplay commands cover component entity create/destroy, spawn, attribute set/add, ability cast, ability request, and lifecycle/despawn flows. There is currently no general `AddComponentBuff` / `RemoveComponentBuff` command.

S3 therefore explicitly defers direct buff grant/remove semantics. `StoryGameplayEffectIntent.BuffGrant(...)` and `BuffRemove(...)` return `StoryGameplayEffectResult.Success=false` with `StoryGameplayBridgeDiagnosticCode.UnsupportedBuffEffect`, leave the Gameplay command buffer unchanged, and do not call:

- `IBuffPipeline.AddBuff`
- Attributes mutation APIs
- Modifier mutation APIs
- component store mutation APIs

## Command Buffer Ownership

Story.GameplayBridge receives a Gameplay command buffer reference from the composition root:

```text
StoryRuntimeModule
  -> StoryGameplayEffectBridge.EnqueueGameplayEffect(...)
  -> GameplayCommandBuffer.Enqueue(...)
  -> GameplayRuntimeModule drains later
```

Rules:

- The bridge does not own frame clocks.
- The bridge does not call `DrainForFrame`.
- The bridge enqueues at `targetFrame = currentStoryFrame + max(0, DelayFrames)`.
- Same-frame effects require Story Runtime priority earlier than Gameplay Runtime priority.

`DelayFrames == 0` is same-frame enqueue. Composition roots that need same-frame Gameplay consumption must tick Story before Gameplay in `RuntimeTickStage.Simulation`.

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

S3 tests:

- `Assets/Scripts/MxFramework/Tests/Story.GameplayBridge/StoryBeatGameplayLocatorTests.cs`
- `Assets/Scripts/MxFramework/Tests/Story.GameplayBridge/StoryModifierConditionAdapterTests.cs`
- `Assets/Scripts/MxFramework/Tests/Story.GameplayBridge/StoryGameplayEffectBridgeTests.cs`

See `Docs/Tasks/STORY_S3_GAMEPLAY_RESOURCES_BRIDGE.md` for the S3 task contract.
