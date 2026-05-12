# GAMEPLAY_ECS_STYLE_16_COMPONENT_ABILITY_COMMAND_BRIDGE

## 目标

让 component runtime 支持最小 Ability command bridge：component entity 可以基于 `GameplayAttributeSetComponent` 执行 Ability，并把结果写回 component world。

本批次不是重做完整 Ability System，而是把现有 v0 Ability 的“命令 -> 目标 -> 效果 -> 事件”链路接到 component entity source of truth：

```text
RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplaySystemPipeline
-> GameplayComponentAbilityCommandSystem
-> GameplayComponentWorld
-> GameplayAttributeSetComponent
-> RuntimeEventQueue / hash / SaveState
```

## 背景

旧 `GameplayAbilityCommandSystem` 处理 `CastAbility` command 时，仍然依赖：

```text
GameplayWorld
-> RuntimeEntity / IRuntimeEntity
-> AttributeStore / BuffPipeline / ModifierPipeline
-> GameplayAbilityRuntimeService
```

这条链路对 v0 Demo 仍然有效，但新 component runtime 已经有：

- generation-safe component entity
- spawn definition
- lifecycle cleanup
- component attribute state
- schema / diagnostics / hash / SaveState

下一步应验证 component entity 能执行最小 Ability，而不是继续让 Ability 只能操作旧 `RuntimeEntity`。

## 范围

建议新增：

- `GameplayComponentAbilityCommandSystem`
- component ability command id / factory
- `GameplayComponentAbilityRegistry` 或复用现有 `GameplayAbilityRegistry` 的 adapter
- component ability request / result DTO
- component target resolver
- component ability effect adapter
- focused tests

建议复用：

- `GameplayComponentWorld`
- `GameplayComponentStore<GameplayAttributeSetComponent>`
- `GameplayAttributeCommandSystem` 的属性更新语义
- `GameplayRuntimeEvent`
- `GameplayCommandExecutionState`
- `RuntimeCommandBuffer`

## 不做

本批次不要做：

- Ability cooldown
- mana / stamina cost
- cast time / channel / interrupt
- projectile / hit window / hurt window
- Buff / Modifier pipeline 迁移
- Combat formula 框架
- AI ability decision
- 多目标复杂 payload
- Unity Animator / VFX / Audio 触发
- 旧 `RuntimeEntity` 数据迁移

## Command 设计

不要复用旧 `CastAbility` command。

原因：旧 `CastAbility` payload 使用裸 `int entityId`，目标是 `RuntimeEntity`。component runtime 必须使用 `GameplayEntityId.Index + Generation`，避免 stale id。

新增 command id：

```csharp
public const int CastComponentAbility = 1001008;
```

推荐 factory：

```csharp
public static RuntimeCommand CastComponentAbility(
    RuntimeFrame frame,
    GameplayEntityId casterEntityId,
    int abilityId,
    GameplayEntityId candidateEntityId = default,
    int sourceId = 0,
    string traceId = "");
```

payload 建议：

```text
targetId = caster.index
payload0 = caster.generation
payload1 = abilityId
payload2 = candidate.index
```

`candidate.generation` 放不下时不要偷塞到 traceId。第一版建议只支持：

- self ability
- no explicit candidate
- 或通过 command registry / side request store 支持 candidate index+generation

第一版 command payload：

```text
targetId = caster.index
payload0 = caster.generation
payload1 = abilityId
payload2 = 0
```

v0 只支持 self target。`payload2` 必须为 `0`，非 `0` payload 必须被拒绝为 invalid command payload。候选目标需要完整 id 时，下一批再做 `RuntimeCommandRegistry` payload schema、side request store 或新的 command schema，不允许 ad-hoc 复用 `payload2`，也不允许把 generation 偷塞进 `traceId`。

## Ability Definition

本批次不要直接复用 `IAbility`。

`IAbility` 依赖 `AbilityContext` / `IRuntimeEntity`，会把 component runtime 又拉回旧 entity facade。可以保留 adapter 作为后续兼容层，但第 16 批建议先定义 component-native 最小 ability：

```csharp
public interface IGameplayComponentAbility
{
    int AbilityId { get; }
    GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context);
}
```

Context：

```csharp
public readonly struct GameplayComponentAbilityContext
{
    public RuntimeFrame Frame { get; }
    public GameplayComponentWorld World { get; }
    public GameplayEntityId CasterEntityId { get; }
    public IReadOnlyList<GameplayEntityId> TargetEntityIds { get; }
    public string TraceId { get; }
}
```

Result：

```csharp
public sealed class GameplayComponentAbilityResult
{
    public bool Success { get; }
    public int AbilityId { get; }
    public GameplayEntityId CasterEntityId { get; }
    public IReadOnlyList<GameplayEntityId> TargetEntityIds { get; }
    public GameplayComponentAbilityFailureCode FailureCode { get; }
    public string FailureReason { get; }
}
```

第一版 failure code 可包含：

```text
None
MissingCaster
MissingAbility
MissingAttributeSet
MissingTarget
EffectFailed
InvalidCommandPayload
```

## Ability Registry

建议新增：

```csharp
public sealed class GameplayComponentAbilityRegistry
{
    public void Register(IGameplayComponentAbility ability);
    public bool TryGet(int abilityId, out IGameplayComponentAbility ability);
    public IGameplayComponentAbility[] CreateSnapshot();
    public void Clear();
}
```

规则：

- duplicate `AbilityId` 抛错。
- `AbilityId > 0`。
- snapshot 按 `AbilityId` 稳定排序。
- registry 不持有 world。

## Effect 边界

第一版只做最小属性效果，建议新增一个测试用 / runtime 用 effect：

```csharp
public sealed class GameplayComponentAttributeDeltaAbility : IGameplayComponentAbility
{
    public GameplayComponentAttributeDeltaAbility(
        int abilityId,
        int attributeId,
        int delta,
        GameplayComponentTargetMode targetMode);
}
```

Target mode 第一版只支持：

```text
Self
ExplicitSingle
```

如果 `ExplicitSingle` 需要完整 generation id，而 command payload 放不下，则本批次可以只实现 `Self`，把 `ExplicitSingle` 写为后续。

Effect 规则：

- 读取目标 entity 的 `GameplayAttributeSetComponent`。
- 对指定 attribute 执行 `AddCurrentValue(delta)`。
- 写回 component store。
- 输出 attribute changed event。
- 失败时不部分写入多个目标；第一版只有单目标，因此无需事务。

## Command System

建议新增：

```csharp
public sealed class GameplayComponentAbilityCommandSystem : IGameplaySystem
{
    public const string DefaultSystemId = "mxframework.gameplay.command.component_ability";

    public GameplayComponentAbilityCommandSystem(
        GameplayComponentAbilityRegistry abilityRegistry,
        string systemId = DefaultSystemId,
        int priority = 40);
}
```

推荐 phase：

```text
GameplaySystemPhase.Command
```

推荐 priority：

```text
after GameplayAttributeCommandSystem
before GameplayUnsupportedCommandSystem
```

执行规则：

1. 遍历 `context.Commands`。
2. 只处理 `CastComponentAbility`。
3. 校验 `context.ComponentWorld != null`。
4. 从 `targetId + payload0` 还原 caster `GameplayEntityId`。
5. 校验 caster alive。
6. 查 ability registry。
7. 解析 target list。
8. 调用 component ability。
9. 输出 `AbilityCastSucceeded` / `AbilityCastFailed` event，事件必须写入 component entity id。
10. `context.CommandState.MarkHandled(command)`。

## Event 边界

可以复用：

```text
GameplayRuntimeEventType.AbilityCastSucceeded
GameplayRuntimeEventType.AbilityCastFailed
```

但事件必须区分 component entity：

- `TargetEntityId` 继续留给旧 `RuntimeEntity`。
- `ComponentEntityIndex` / `ComponentEntityGeneration` 写 caster 或 primary target。
- `AbilityId` 写 ability id。
- `Reason` 写 failure reason 或稳定 success reason。

如果同时需要 caster 和 target component id，当前 `GameplayRuntimeEvent` 字段不够。第一版建议：

- component entity id 先写 caster。
- target ids 通过 ability result / diagnostics 观察。
- 后续如有需要新增 event detail store，不要把所有字段塞进 `GameplayRuntimeEvent`。

成功 cast 的事件顺序固定为：

```text
ComponentAttributeChanged
AbilityCastSucceeded
```

原因是 component ability 的 effect event 在 `IGameplayComponentAbility.Cast()` 内先入队，`GameplayComponentAbilityCommandSystem` 在 ability 返回后再写 final cast event。UI / Audio / Diagnostics 如果需要表现具体数值变化，应优先监听 effect event；final ability event 用于表达 cast 成功 / 失败边界。

## Reason 常量

建议新增：

```csharp
public static class GameplayComponentAbilityEvents
{
    public const string CastComponentAbilityReason = "CastComponentAbility";
    public const string MissingComponentWorldReason = "MissingComponentWorld";
    public const string InvalidCasterReason = "InvalidComponentAbilityCaster";
    public const string MissingCasterReason = "MissingComponentAbilityCaster";
    public const string MissingAbilityReason = "MissingComponentAbility";
    public const string MissingAttributeSetReason = "MissingAttributeSet";
    public const string EffectFailedReason = "ComponentAbilityEffectFailed";
}
```

## 与旧 Ability 的关系

本批次不删除旧链路：

- `CastAbility` 继续由 `GameplayAbilityCommandSystem` 处理旧 `GameplayWorld / RuntimeEntity`。
- `CastComponentAbility` 由新 `GameplayComponentAbilityCommandSystem` 处理 component entity。
- 两条 command 不共用 payload。
- 不允许同一个玩法对象同时被旧 `RuntimeEntity` 和 component entity 作为权威状态驱动。

后续如果需要兼容旧 `IAbility`，应新增明确 adapter：

```text
ComponentEntityRuntimeEntityAdapter
```

但 adapter 必须有清晰写回策略，不能把修改留在临时 `AttributeStore` 中。

## Hash / SaveState 关系

Ability definition / registry 不是 world state。

本批次只要求：

- ability command 修改后的 component attribute state 参与 hash。
- SaveState 捕获 cast 后的 attribute state。
- Restore 后不需要 ability registry 即可恢复 cast 结果状态。
- Replay 仍依赖 command 输入和 hash 验证，不把 ability result history 存进 SaveState。

## 测试要求

至少新增：

- `ComponentAbilityRegistry_RejectsDuplicateAbilityId`
- `CastComponentAbility_SelfDeltaUpdatesAttribute`
- `CastComponentAbility_EmitsSuccessEventWithComponentEntityId`
- `CastComponentAbility_RejectsMissingComponentWorld`
- `CastComponentAbility_RejectsStaleCaster`
- `CastComponentAbility_RejectsMissingAbility`
- `CastComponentAbility_RejectsMissingAttributeSet`
- `CastComponentAbility_IsHandledBeforeUnsupportedSystem`
- `CastComponentAbility_ChangesComponentWorldHash`
- `CastComponentAbility_SaveStateRoundtripPreservesResultState`

如果第 15 批 attribute changed event 已实现，额外新增：

- `CastComponentAbility_EmitsAttributeChangedEvent`

如果实现 explicit single target，额外新增：

- `CastComponentAbility_ExplicitTargetUpdatesTargetOnly`
- `CastComponentAbility_RejectsStaleTarget`

## 默认 pipeline 接入

本批次建议不要自动加入 default pipeline，因为需要调用方提供 `GameplayComponentAbilityRegistry`。

推荐组合根：

```csharp
var abilityRegistry = new GameplayComponentAbilityRegistry();
abilityRegistry.Register(new GameplayComponentAttributeDeltaAbility(
    abilityId: 300001,
    attributeId: HpAttributeId,
    delta: -10,
    targetMode: GameplayComponentTargetMode.Self));

var module = new GameplayRuntimeModule(..., configureDefaultPipeline: pipeline =>
{
    pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
    pipeline.Add(new GameplayAttributeCommandSystem());
    pipeline.Add(new GameplayComponentAbilityCommandSystem(abilityRegistry));
});
```

## 后续衔接

第 16 批完成后，下一步建议做：

```text
GAMEPLAY_ECS_STYLE_17_COMPONENT_ABILITY_TARGETING
```

目标是解决 explicit target、team filter、alive filter、range/source query 等目标选择问题，再进入 cooldown / cost / cast time。

## 验收

- Component entity 可以通过 command 执行最小 Ability。
- Ability 修改 `GameplayAttributeSetComponent`，不写旧 `RuntimeEntity.AttributeStore`。
- 成功 / 失败都有结构化 runtime event。
- Command handled 机制不会误报 unsupported。
- Cast 后 hash / SaveState 只由 component world state 表达。
- 旧 `CastAbility` 行为不受影响。
- 不引入 cooldown / cost / cast time。
- 不引入 WGame 私有 Ability 配置或属性 id。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 component ability bridge 语义。
