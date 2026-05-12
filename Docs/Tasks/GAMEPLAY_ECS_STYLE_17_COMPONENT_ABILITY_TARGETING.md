# GAMEPLAY_ECS_STYLE_17_COMPONENT_ABILITY_TARGETING

## 目标

让 component ability 支持 generation-safe 目标选择：从 self-only cast 推进到 explicit single target、team filter、alive filter、tag/status filter 的最小闭环。

本批次仍然保持 command-driven component runtime：

```text
RuntimeCommandBuffer
-> GameplayComponentAbilityCommandSystem
-> GameplayComponentTargetingService
-> GameplayComponentAbility
-> GameplayAttributeSetComponent
-> RuntimeEventQueue / hash / SaveState
```

## 背景

第 16 批 `CastComponentAbility` v0 只支持 self target。这个选择是对的，因为 `RuntimeCommand` 的 `payload0/1/2` 放不下 caster + target 的完整 `Index + Generation`，不能把 target generation 偷塞到 `traceId`。

但后续 Ability / Combat / AI 必须支持：

- 选自己。
- 选单个 explicit target。
- 按队伍关系过滤。
- 按 `GameplayLifecycleComponent` 判断 alive / pending destroy。
- 按 tag / status 过滤。
- 输出稳定 rejected reason，便于 UI、Diagnostics、Authoring Preview 和 agent 修复。

当前已有旧 `GameplayTargetingService`，但它使用裸 `int EntityId` / `IRuntimeEntity` 候选，服务旧 `RuntimeEntity` 路线。第 17 批应为 component entity 建立 generation-safe target candidate 和 query，不要直接复用裸 int target 结果。

## 范围

建议新增：

- `GameplayComponentTargetCandidate`
- `GameplayComponentTargetQuery`
- `GameplayComponentTargetingResult`
- `GameplayComponentTargetRejectedTarget`
- `GameplayComponentTargetingService`
- `GameplayComponentAbilityRequestStore` 或 command request handle
- explicit target cast command / factory 支持
- focused tests

建议复用：

- `GameplayEntityId`
- `GameplayTeamComponent`
- `GameplayLifecycleComponent`
- `GameplayTagComponent`
- `GameplayStatusComponent`
- `GameplayComponentQuery`
- `GameplayComponentWorld`
- `GameplayComponentAbilityCommandSystem`

## 不做

本批次不要做：

- 空间距离 / range / physics / navmesh
- Combat hit shape / hurt box
- 多目标复杂排序策略
- threat / aggro / AI utility score
- cooldown / cost / cast time
- projectile / delayed impact
- Unity object / GameObject target
- 旧 `IRuntimeEntity` targeting 替换

## 数据模型

### Component target candidate

推荐：

```csharp
public readonly struct GameplayComponentTargetCandidate
{
    public GameplayEntityId EntityId { get; }
    public int TeamId { get; }
    public GameplayLifecycleState LifecycleState { get; }
    public IReadOnlyList<int> Tags { get; }
    public IReadOnlyList<int> Statuses { get; }
    public bool IsAlive { get; }
}
```

规则：

- `EntityId` 必须有效。
- `IsAlive` 建议等价于 `LifecycleState == GameplayLifecycleState.Alive`。
- tag/status 数组在构造时复制、排序、去重，或直接使用已有 component 的稳定数组副本。
- Candidate 是 snapshot，不持有 store 引用。

### Component target query

推荐：

```csharp
public sealed class GameplayComponentTargetQuery
{
    public GameplayEntityId CasterEntityId { get; }
    public int CasterTeamId { get; }
    public bool RequireAlive { get; }
    public GameplayTargetRelationFilter RelationFilter { get; }
    public IReadOnlyList<int> RequiredTags { get; }
    public IReadOnlyList<int> BlockedStatuses { get; }
    public int MaxTargets { get; }
}
```

可以复用旧 `GameplayTargetRelationFilter` 和 `GameplayTargetRejectReason`，但 result 必须保存 `GameplayEntityId`，不能降级为裸 int。

### Result

推荐：

```csharp
public readonly struct GameplayComponentTargetRejectedTarget
{
    public int CandidateIndex { get; }
    public GameplayEntityId EntityId { get; }
    public GameplayTargetRejectReason Reason { get; }
    public int DetailId { get; }
}

public sealed class GameplayComponentTargetingResult
{
    public IReadOnlyList<GameplayComponentTargetCandidate> SelectedTargets { get; }
    public IReadOnlyList<GameplayComponentTargetRejectedTarget> RejectedTargets { get; }
}
```

Result 必须是 immutable snapshot，不能暴露内部 list。

## Candidate 构建

建议提供：

```csharp
public static class GameplayComponentTargetCandidates
{
    public static void CopyFromWorld(
        GameplayComponentWorld world,
        IList<GameplayComponentTargetCandidate> output);
}
```

构建规则：

1. 以 `GameplayComponentWorld.Registry.CreateEntitySnapshot()` 或 lifecycle snapshot 为 entity 基准。
2. 按 `GameplayEntityId` 稳定顺序输出。
3. Team 缺失时使用 `0`。
4. Lifecycle 缺失时可视为 `None`，`RequireAlive` 会拒绝。
5. Tag / Status 缺失时视为空集合。
6. 不读取旧 `GameplayWorld` / `RuntimeEntity`。

如果当前 `GameplayComponentRegistry` 没有直接 entity snapshot API，则通过 `GameplayEntityLifecycle.CreateSnapshot()` 暴露受控入口，不要扫描任意 store 推断 entity 列表。

## Explicit target request

由于 `RuntimeCommand` payload 容量有限，本批次不要把 target generation 塞到 payload。

建议新增 frame-local / request-scoped store：

```csharp
public readonly struct GameplayComponentAbilityRequestHandle
{
    public int Index { get; }
    public int Generation { get; }
}

public sealed class GameplayComponentAbilityRequestStore
{
    public GameplayComponentAbilityRequestHandle Add(GameplayComponentAbilityRequest request);
    public bool TryGet(GameplayComponentAbilityRequestHandle handle, out GameplayComponentAbilityRequest request);
    public bool Remove(GameplayComponentAbilityRequestHandle handle);
    public void Clear();
}
```

Request：

```csharp
public sealed class GameplayComponentAbilityRequest
{
    public GameplayEntityId CasterEntityId { get; }
    public int AbilityId { get; }
    public IReadOnlyList<GameplayEntityId> CandidateEntityIds { get; }
    public GameplayComponentTargetQuery TargetQuery { get; }
}
```

Command payload：

```text
targetId = caster.index
payload0 = caster.generation
payload1 = abilityId
payload2 = requestHandle.index
```

`requestHandle.generation` 不放进 command 时，需要另外方案。第一版更稳的做法是：

- `CastComponentAbility` 仍 self-only。
- 新增 `CastComponentAbilityRequest` command，`payload0=requestHandle.index`、`payload1=requestHandle.generation`、`payload2=abilityId`。

推荐本批次使用第二种，避免半个 handle。

## Command API

新增 command id：

```csharp
public const int CastComponentAbilityRequest = 1001009;
```

Factory：

```csharp
public static RuntimeCommand CastComponentAbilityRequest(
    RuntimeFrame frame,
    GameplayComponentAbilityRequestHandle requestHandle,
    int abilityId,
    int sourceId = 0,
    string traceId = "");
```

payload：

```text
targetId = requestHandle.index
payload0 = requestHandle.index
payload1 = requestHandle.generation
payload2 = abilityId
```

`GameplayComponentAbilityCommandSystem` 可以继续处理 self-only `CastComponentAbility`，并新增处理 request command。

## Targeting Service

推荐：

```csharp
public sealed class GameplayComponentTargetingService
{
    public GameplayComponentTargetingResult Select(
        GameplayComponentTargetQuery query,
        IReadOnlyList<GameplayComponentTargetCandidate> candidates);
}
```

规则：

- 输入顺序即候选优先级。
- selected / rejected 输出顺序稳定。
- `RequireAlive` 使用 component lifecycle。
- `RelationFilter` 使用 caster team + candidate team。
- required tags / blocked statuses 使用 component tag/status。
- `MaxTargets == 0` 表示无限制。
- null candidates 返回空 result，不抛。
- query null 抛 `ArgumentNullException`。

## Ability command integration

`GameplayComponentAbilityCommandSystem` 处理 request command：

1. 通过 request handle 获取 request。
2. 校验 caster alive。
3. 从 request candidate ids 构建 candidate snapshots。
4. 如果 request 没有 candidate ids，可以从 world 构建全部 candidates。
5. 使用 `GameplayComponentTargetingService.Select()`。
6. 无 selected target 时 cast failed，reason 使用稳定 target reject reason 汇总。
7. 将 selected `GameplayEntityId` 传给 component ability context。
8. 成功 / 失败后移除 request handle，避免泄漏。
9. 无论成功或结构化拒绝，都 mark command handled。

Request store 的生命周期必须清楚：

- 由组合根持有。
- `GameplayComponentAbilityCommandSystem` 引用它。
- 成功/失败处理后 remove。
- `Clear()` 用于 world/session reset。

## Event 边界

失败事件：

- `AbilityCastFailed`
- component entity id 写 caster。
- `Reason` 使用：
  - `MissingComponentAbilityRequest`
  - `InvalidComponentAbilityRequest`
  - `MissingComponentAbilityTarget`
  - `NoValidComponentAbilityTarget`

如果需要展示每个 rejected target，第一版可以通过 diagnostics snapshot 或 request result accessor 暴露；不要把多个 rejected target 塞进 `GameplayRuntimeEvent`。

## Reason 常量

建议扩展：

```csharp
public static class GameplayComponentAbilityEvents
{
    public const string MissingRequestReason = "MissingComponentAbilityRequest";
    public const string InvalidRequestReason = "InvalidComponentAbilityRequest";
    public const string MissingTargetReason = "MissingComponentAbilityTarget";
    public const string NoValidTargetReason = "NoValidComponentAbilityTarget";
}
```

Target rejected reason 继续使用 `GameplayTargetRejectReason`，不要新造一套字符串枚举。

## Hash / SaveState 关系

Targeting request 是 transient input，不是 world state。

规则：

- Request store 不参与 `GameplayComponentWorld` hash。
- Request store 不参与 ComponentWorld SaveState。
- Ability 成功后的 component state 参与 hash / SaveState。
- 如果保存发生在 request 入队后但 command 尚未执行，当前 v0 不要求 SaveState 捕获 pending request；后续如需要，应把 pending command/request 纳入 Runtime command/save orchestration，而不是 ComponentWorld SaveState。

## 测试要求

至少新增：

- `ComponentTargeting_SelectsSelfByGenerationEntity`
- `ComponentTargeting_FiltersDeadLifecycle`
- `ComponentTargeting_FiltersSameTeamAndEnemy`
- `ComponentTargeting_FiltersRequiredTagsAndBlockedStatuses`
- `ComponentTargeting_RespectsMaxTargets`
- `ComponentTargetCandidates_CopyFromWorldUsesStableEntityOrder`
- `AbilityRequestStore_RejectsStaleHandle`
- `CastComponentAbilityRequest_ExplicitTargetUpdatesTargetAttribute`
- `CastComponentAbilityRequest_RejectsStaleTarget`
- `CastComponentAbilityRequest_RejectsNoValidTarget`
- `CastComponentAbilityRequest_RemovesRequestAfterHandled`
- `CastComponentAbilityRequest_IsHandledBeforeUnsupportedSystem`

如果 hash/save 已完成，额外验证：

- `CastComponentAbilityRequest_ChangesComponentWorldHash`
- `CastComponentAbilityRequest_SaveStateRoundtripPreservesResultState`

## 默认 pipeline 接入

本批次不要自动加入 default pipeline，因为需要 request store 和 ability registry。

推荐组合根：

```csharp
var requestStore = new GameplayComponentAbilityRequestStore();
var abilityRegistry = new GameplayComponentAbilityRegistry();

var module = new GameplayRuntimeModule(..., configureDefaultPipeline: pipeline =>
{
    pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
    pipeline.Add(new GameplayAttributeCommandSystem());
    pipeline.Add(new GameplayComponentAbilityCommandSystem(
        abilityRegistry,
        requestStore,
        new GameplayComponentTargetingService()));
});
```

## 后续衔接

第 17 批完成后，下一步建议做：

```text
GAMEPLAY_ECS_STYLE_18_COMPONENT_ABILITY_COOLDOWN_COST
```

先做 cooldown / cost 的 component-native gating，不做 cast time / interrupt。理由是 cooldown/cost 只需要 command-time gate，风险低；cast time / interrupt 需要 timeline 和 pending operation。

## 验收

- Component ability 可以通过 generation-safe request 指向 explicit target。
- Targeting 使用 component world 的 team/lifecycle/tag/status state。
- Targeting result 和 rejected reason 稳定、可测试、可诊断。
- 不降级为裸 int entity id。
- 不把 target generation 塞进 traceId 或其他非结构化字段。
- Request store 是 transient input，不污染 ComponentWorld SaveState。
- 旧 `GameplayTargetingService` 和旧 `CastAbility` 行为不受影响。
- 不引入 cooldown / cost / cast time。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 component targeting 语义。
