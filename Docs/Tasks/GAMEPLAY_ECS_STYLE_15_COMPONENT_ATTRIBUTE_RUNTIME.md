# GAMEPLAY_ECS_STYLE_15_COMPONENT_ATTRIBUTE_RUNTIME

## 目标

让 component runtime 拥有第一类、可 hash、可 SaveState、可诊断的属性状态，为后续 Ability / Combat / Buff / UI 迁移提供稳定 source of truth。

本批次重点不是把旧 `RuntimeEntity.AttributeStore` 原样搬进 component store，而是定义 component-native attribute state：

```text
SpawnComponentEntity
-> initial GameplayAttributeSetComponent
-> GameplayAttributeCommandSystem / attribute helpers
-> RuntimeEventQueue<GameplayRuntimeEvent>
-> ComponentWorld hash / SaveState
```

## 背景

当前 component runtime 已经具备：

- generation-safe entity id
- component world / registry / query
- schema registry
- diagnostics / hash / SaveState
- lifecycle cleanup
- spawn definition

但 component entity 还没有权威属性状态。旧 `RuntimeEntity` 内部持有 `AttributeStore`、`BuffPipeline`、`ModifierPipeline`，适合 v0 API，但不适合作为新 component runtime 的长期 source of truth。

如果直接在 component 里塞旧 `AttributeStore` 引用，会带来几个问题：

- 泛型 store snapshot 看不到内部字典状态。
- hash / SaveState 必须额外绕过引用对象。
- component value 不再是清晰数据。
- 后续 agent 容易把旧 `RuntimeEntity` 和新 component store 双写。

因此第 15 批先做 component-native attribute state。旧 attribute / buff / modifier pipeline 后续通过 adapter 或迁移系统接入，不在本批次直接混合。

## 范围

建议新增：

- `GameplayAttributeValue`
- `GameplayAttributeSetComponent`
- `GameplayAttributeSetBuilder` 或 `GameplayAttributeSet` helpers
- `GameplayAttributeCommandSystem`
- attribute command id / factory
- attribute event reason / payload conventions
- schema diagnostics / hash / SaveState adapters
- focused tests

建议复用：

- `GameplayComponentWorld`
- `GameplayComponentStore<T>`
- `GameplayComponentSchemaRegistry`
- `RuntimeCommandBuffer`
- `RuntimeEventQueue<GameplayRuntimeEvent>`
- `GameplayComponentWorldHashContributor`
- `GameplayComponentWorldSaveStateProvider`

## 不做

本批次不要做：

- 旧 `RuntimeEntity` 到 component entity 的完整桥接
- `AttributeStore` 引用型 component
- Buff / Modifier pipeline 迁移
- Ability cast command 迁移
- Combat damage formula
- 浮点属性
- Unity object / MonoBehaviour / ScriptableObject 依赖
- 自动从配置表加载属性定义

## 数据模型

### GameplayAttributeValue

推荐第一版：

```csharp
public readonly struct GameplayAttributeValue
{
    public int AttributeId { get; }
    public int BaseValue { get; }
    public int CurrentValue { get; }
}
```

规则：

- `AttributeId > 0`。
- `BaseValue` 表达未受临时变化影响的基础值。
- `CurrentValue` 表达当前权威值。
- 第一版不计算 modifier final value，避免和旧 `AttributeStore` 语义混淆。
- 后续如果接 modifier，可扩展为 base / current / max / final，或新增 modifier component。

### GameplayAttributeSetComponent

推荐第一版：

```csharp
public readonly struct GameplayAttributeSetComponent : IGameplayComponent
{
    public int Count { get; }

    public bool TryGet(int attributeId, out GameplayAttributeValue value);
    public int GetCurrentValueOrDefault(int attributeId);
    public GameplayAttributeSetComponent SetBaseValue(int attributeId, int baseValue);
    public GameplayAttributeSetComponent SetCurrentValue(int attributeId, int currentValue);
    public GameplayAttributeSetComponent AddCurrentValue(int attributeId, int delta);
    public GameplayAttributeValue[] ToArray();
}
```

规则：

- 内部 attribute array 必须按 `AttributeId` 升序保存。
- 构造时过滤或拒绝 duplicate attribute id，建议拒绝并抛结构化异常。
- `ToArray()` 返回副本。
- mutate API 返回新的 component value，再由 system 调用 `store.Set(entityId, updated)`。
- 不暴露内部数组引用。

第一版保持 int 属性，避免浮点量化、平台差异和 hash 不稳定。

## Command API

新增 command id：

```csharp
public const int SetComponentAttribute = 1001006;
public const int AddComponentAttribute = 1001007;
```

推荐 factory：

```csharp
public static RuntimeCommand SetComponentAttribute(
    RuntimeFrame frame,
    GameplayEntityId entityId,
    int attributeId,
    int value,
    int sourceId = 0,
    string traceId = "");

public static RuntimeCommand AddComponentAttribute(
    RuntimeFrame frame,
    GameplayEntityId entityId,
    int attributeId,
    int delta,
    int sourceId = 0,
    string traceId = "");
```

payload 建议：

```text
targetId = entity.index
payload0 = entity.generation
payload1 = attributeId
payload2 = value or delta
```

注意：这里没有 candidate target。属性命令只修改一个 component entity。后续 Ability / Combat command 如果需要多个实体，应使用专门 command schema，不要挤进这两个 command。

## Command System

建议新增：

```csharp
public sealed class GameplayAttributeCommandSystem : IGameplaySystem
{
    public const string DefaultSystemId = "mxframework.gameplay.command.attribute";

    public string SystemId { get; }
    public GameplaySystemPhase Phase { get; }
    public int Priority { get; }
    public bool IsEnabled { get; }

    public void Tick(GameplaySystemContext context);
}
```

推荐 phase：

```text
GameplaySystemPhase.Command
```

推荐 priority：

```text
after GameplayComponentSpawnCommandSystem
before GameplayUnsupportedCommandSystem
```

## 执行规则

`GameplayAttributeCommandSystem.Tick(context)`：

1. 遍历 `context.Commands`。
2. 只处理 `SetComponentAttribute` / `AddComponentAttribute`。
3. 校验 `context.ComponentWorld != null`。
4. 从 `targetId + payload0` 还原 `GameplayEntityId`。
5. 校验 entity alive。
6. 读取 `GameplayComponentStore<GameplayAttributeSetComponent>`。
7. 如果缺少 attribute set：
   - `SetComponentAttribute` 可以创建新 set。
   - `AddComponentAttribute` 建议拒绝，避免把未知属性从 0 隐式创建成 delta。
8. 更新 component value 并写回 store。
9. 输出 attribute changed runtime event 或结构化 `CommandRejected`。
10. `context.CommandState.MarkHandled(command)`。

## Event 边界

第一版可以二选一：

### 方案 A：扩展 GameplayRuntimeEvent

新增 event type：

```text
ComponentAttributeChanged
```

并给 `GameplayRuntimeEvent` 增加：

```text
AttributeId
OldValue
NewValue
Delta
```

优点：UI / Diagnostics 读取简单。
缺点：`GameplayRuntimeEvent` 字段继续膨胀。

### 方案 B：先用 reason + component entity id

复用 `GameplayRuntimeEventType.CommandRejected` 处理失败；成功事件暂不进入通用 event，依赖 diagnostics/hash/save 验证状态。

优点：不扩大 event DTO。
缺点：UI / Audio / Diagnostics 不容易监听属性变化。

推荐本批次采用 **方案 A**，因为属性变化是 Gameplay 基础事件，后续 UI 血条、伤害飘字、Combat log 都会用。

## Reason 常量

建议新增：

```csharp
public static class GameplayAttributeEvents
{
    public const string SetAttributeReason = "SetComponentAttribute";
    public const string AddAttributeReason = "AddComponentAttribute";
    public const string MissingComponentWorldReason = "MissingComponentWorld";
    public const string InvalidComponentEntityReason = "InvalidComponentEntity";
    public const string MissingComponentEntityReason = "MissingComponentEntity";
    public const string MissingAttributeSetReason = "MissingAttributeSet";
    public const string InvalidAttributeIdReason = "InvalidAttributeId";
}
```

Reason 字符串必须稳定，测试断言 reason，不依赖本地化 message。

## Schema / Hash / SaveState

`GameplayAttributeSetComponent` 必须接入 schema registry。

要求：

- diagnostics writer 输出 entity id、attribute count、每个 attribute id/base/current。
- hash writer 按 `AttributeId` 升序写入 id/base/current。
- SaveState adapter 输出稳定 JSON 字段名。
- Restore 校验 duplicate attribute id、invalid id 和 schema version。

本批次不允许直接序列化 `GameplayAttributeSetComponent` 内部数组对象图。

## Spawn Definition 关系

第 14 批的 spawn initializer 可以直接写：

```csharp
new GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>(
    "mxframework.gameplay.attributes",
    new GameplayAttributeSetComponent(...))
```

这样 gameplay actor 的初始 HP、攻击、防御等可以通过 spawn definition 进入 component runtime。

本批次不定义具体 attribute id 常量。测试可使用本地私有常量，例如：

```text
Hp = 1
Attack = 2
Defense = 3
```

框架层不引入 WGame 私有属性 id。

## 与旧 AttributeStore 的关系

本批次不移除旧 `AttributeStore`。

边界：

- 旧 `RuntimeEntity` 继续服务现有 Ability v0 和 Demo。
- 新 `GameplayAttributeSetComponent` 是 component runtime 的 source of truth。
- 不允许同一个玩法对象同时把 HP 写在旧 `RuntimeEntity.AttributeStore` 和新 `GameplayAttributeSetComponent` 里。
- 后续如果需要复用旧 `IAbilityEffect`，应通过明确 adapter 把 component attribute state 映射到执行上下文，并在执行后写回 component value。

## 测试要求

至少新增：

- `AttributeSetComponent_SortsAttributesById`
- `AttributeSetComponent_RejectsDuplicateAttributeIds`
- `AttributeSetComponent_SetBaseValueUpdatesStableValue`
- `AttributeSetComponent_AddCurrentValueReturnsNewComponent`
- `AttributeSchema_DiagnosticsWritesStableFields`
- `AttributeSchema_HashChangesWhenAttributeChanges`
- `AttributeSchema_SaveStateRoundtripRestoresAttributes`
- `SetAttributeCommand_UpdatesExistingAttributeAndEmitsEvent`
- `SetAttributeCommand_CreatesAttributeSetWhenMissing`
- `AddAttributeCommand_RejectsMissingAttributeSet`
- `AttributeCommand_RejectsStaleComponentEntity`
- `AttributeCommand_IsHandledBeforeUnsupportedSystem`

如果 spawn definition 已完成，额外新增：

- `SpawnDefinition_CanInitializeAttributeSetComponent`

## 默认 pipeline 接入

本批次可以先不自动加入 default pipeline，避免改变已有 runtime 行为。

推荐接入：

```csharp
new GameplayRuntimeModule(..., configureDefaultPipeline: pipeline =>
{
    pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
    pipeline.Add(new GameplayAttributeCommandSystem());
});
```

如果后续 Demo / Ability bridge 稳定依赖 attribute commands，再考虑加入 default pipeline。

## 后续衔接

第 15 批完成后，下一步建议做：

```text
GAMEPLAY_ECS_STYLE_16_COMPONENT_ABILITY_COMMAND_BRIDGE
```

目标是让 component entity 使用 `GameplayAttributeSetComponent` 和 ability registry 执行最小 ability command，但仍然不要一次性加入 cooldown、cost、cast time、interrupt。

## 验收

- Component entity 可以持有稳定 attribute set。
- Attribute set 可以通过 command 修改，并输出可诊断事件。
- Attribute set 参与 diagnostics / hash / SaveState。
- Spawn definition 可以初始化 attribute set。
- 不依赖旧 `RuntimeEntity.AttributeStore` 作为 component runtime source of truth。
- 不引入 WGame 私有属性 id。
- 不迁移 Buff / Modifier / Ability 业务。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 attribute runtime 语义。
