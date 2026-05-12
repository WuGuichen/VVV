# GAMEPLAY_ECS_STYLE_18_COMPONENT_ABILITY_RULES

## 目标

给 component ability 增加最小规则层：cooldown 和 cost gate。

本批次只回答一个问题：

```text
这次 CastComponentAbility 是否允许执行？
```

并保持现有 command-driven component runtime：

```text
RuntimeCommandBuffer
-> GameplayComponentAbilityCommandSystem
-> GameplayComponentAbilityRuleSet
-> GameplayComponentAbility
-> GameplayAttributeSetComponent / Cooldown state
-> RuntimeEventQueue / hash / SaveState
```

## 背景

第 16 / 17 批已经让 component ability 能：

- 使用 generation-safe caster。
- 使用 request store 选择 explicit target。
- 通过 component targeting 过滤 team / alive / tag / status。
- 修改 `GameplayAttributeSetComponent`。

但目前 ability 只要命令合法就能释放。真实 gameplay 至少需要：

- cooldown：技能释放后 N 帧内不能再次释放。
- cost：属性不足时不能释放，成功释放后扣除属性。

这些规则必须成为 component runtime 的权威状态，参与 hash / SaveState，并输出结构化 failure reason。

## 范围

建议新增：

- `GameplayAbilityCooldownComponent`
- `GameplayAbilityCost`
- `GameplayComponentAbilityRule`
- `GameplayComponentAbilityRuleSet`
- `GameplayComponentAbilityRuleResult`
- rule evaluation helpers
- rule state schema diagnostics / hash / SaveState adapters
- focused tests

建议修改：

- `GameplayComponentAbilityCommandSystem` 在调用 ability 前执行 rules。
- `GameplayComponentAbilityRegistry` 或 ability definition 暴露 rule set。

## 不做

本批次不要做：

- cast time
- channel / interrupt
- timeline phase
- projectile / delayed hit
- global cooldown 复杂策略
- buff / modifier gate
- AI decision
- UI cooldown display adapter
- rollback / netcode
- Unity Animator / VFX / Audio

## Cooldown state

推荐新增 component：

```csharp
public readonly struct GameplayAbilityCooldownComponent : IGameplayComponent
{
    public int Count { get; }

    public bool TryGetEndFrame(int abilityId, out long endFrame);
    public long GetRemainingFrames(int abilityId, RuntimeFrame frame);
    public GameplayAbilityCooldownComponent Start(
        int abilityId,
        RuntimeFrame frame,
        long durationFrames);
    public GameplayAbilityCooldownComponent RemoveExpired(RuntimeFrame frame);
    public GameplayAbilityCooldownEntry[] ToArray();
}
```

Entry：

```csharp
public readonly struct GameplayAbilityCooldownEntry
{
    public int AbilityId { get; }
    public long EndFrame { get; }
}
```

规则：

- `AbilityId > 0`。
- `EndFrame >= current frame`。
- entries 按 `AbilityId` 升序保存。
- `RemoveExpired(frame)` 显式清理过期项。
- component value immutable，更新后通过 `store.Set(entityId, updated)` 写回。
- 不使用 wall-clock time，不读取 Unity time。

## Cost model

推荐：

```csharp
public readonly struct GameplayAbilityCost
{
    public int AttributeId { get; }
    public int Amount { get; }
}
```

规则：

- `AttributeId > 0`。
- `Amount >= 0`。
- amount 为 0 表示 no-op cost。
- 多个 cost 按输入顺序或 attribute id 稳定顺序执行，文档必须固定一种。

推荐第一版按 `AttributeId` 升序保存和执行，避免注册顺序影响 hash / 行为。

## Rule definition

不要把 cooldown / cost 写死进 command system。

建议 ability 暴露 rule set：

```csharp
public interface IGameplayComponentAbility
{
    int AbilityId { get; }
    GameplayComponentAbilityRuleSet Rules { get; }
    GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context);
}
```

Rule set：

```csharp
public sealed class GameplayComponentAbilityRuleSet
{
    public long CooldownFrames { get; }
    public IReadOnlyList<GameplayAbilityCost> Costs { get; }
}
```

如果不想改接口，可以用 registry companion metadata：

```csharp
GameplayComponentAbilityRegistry.Register(ability, rules)
```

二选一即可。推荐改接口，因为 rule 是 ability definition 的一部分，不是 world state。

## Rule evaluation

建议新增：

```csharp
public static class GameplayComponentAbilityRules
{
    public static GameplayComponentAbilityRuleResult Evaluate(
        GameplayComponentWorld world,
        GameplayEntityId caster,
        int abilityId,
        GameplayComponentAbilityRuleSet rules,
        RuntimeFrame frame);

    public static GameplayComponentAbilityRuleResult Commit(
        GameplayComponentWorld world,
        GameplayEntityId caster,
        int abilityId,
        GameplayComponentAbilityRuleSet rules,
        RuntimeFrame frame);
}
```

`Evaluate` 只读，不修改 state。

`Commit` 表示完整提交 helper；command system v0 实际拆分为 cost commit 和 cooldown commit：

1. `Evaluate` 先做只读检查。
2. `CommitCosts` 在 ability effect 前扣 cost。
3. ability effect 执行。
4. effect 成功后 `CommitCooldown` 启动 cooldown。

当前 v0 不是完整事务 / rollback 模型。effect 失败不会启动 cooldown，但已经提交的 cost 不会自动 refund；如果项目需要退费，后续必须通过显式 refund policy 或 transaction adapter 引入，不隐式回滚 component stores。

## Command system integration

`GameplayComponentAbilityCommandSystem` 顺序：

1. 校验 command / request / caster / target。
2. 查 ability。
3. `Evaluate(rules)`。
4. 如果 rule rejected：输出 `AbilityCastFailed`，不调用 ability。
5. `CommitCosts(rules)`。
6. 调用 ability cast。
7. 如果 ability success：`CommitCooldown(rules)`。
8. 输出 final ability event。
9. MarkHandled。

注意：本批次采用“先扣 cost，再执行 effect，成功后启动 cooldown”。这避免 effect 已经生效后 cost commit 才失败的半成功状态，但它不是 refund 模型；effect 失败时 cost 是否退还由后续显式策略决定。

## Failure code / reason

建议扩展 `GameplayComponentAbilityFailureCode`：

```text
OnCooldown
InsufficientCost
InvalidAbilityRule
```

Reason 常量：

```csharp
public static class GameplayComponentAbilityEvents
{
    public const string AbilityOnCooldownReason = "ComponentAbilityOnCooldown";
    public const string InsufficientCostReason = "ComponentAbilityInsufficientCost";
    public const string InvalidRuleReason = "InvalidComponentAbilityRule";
}
```

失败 event：

- type = `AbilityCastFailed`
- reason = stable reason
- component entity id = caster
- ability id = ability id
- failure code = mapped runtime failure code if existing enum cannot extend cleanly；否则新增 component-specific code 并映射。

测试必须断言 reason。

## Event 边界

本批次不要求新增独立 cooldown/cost event。

成功 cast 可能产生：

```text
ComponentAttributeChanged        // ability effect
ComponentAttributeChanged        // cost commit
AbilityCastSucceeded
```

如果成本属性和 effect 属性相同，事件顺序必须稳定：

1. cost event
2. ability effect event
3. final ability event

Cooldown start 第一版可只通过 component diagnostics/hash/save 观察；后续 UI 需要时再加 `ComponentAbilityCooldownStarted` event。

## Schema / Hash / SaveState

`GameplayAbilityCooldownComponent` 必须接入 schema registry。

要求：

- diagnostics writer 输出 ability id、end frame、remaining frame 可由外层 context 计算，writer 只写 state。
- hash writer 按 ability id 升序写入 ability id / end frame。
- SaveState adapter 稳定 JSON 字段名。
- Restore 校验 duplicate ability id、invalid ability id、invalid end frame。

Cost definition 不是 world state，不进入 ComponentWorld SaveState。

## Cleanup

Cooldown 过期清理可以二选一：

### 方案 A：Evaluate 时顺手清理

当 caster 有 cooldown component 时，command system 在 evaluate 前调用 `RemoveExpired(frame)` 并写回。

优点：简单。
缺点：没有 cast 的 entity 不会自动清理过期 cooldown。

### 方案 B：新增 cleanup system

```csharp
public sealed class GameplayAbilityCooldownCleanupSystem : IGameplaySystem
```

运行在 `GameplaySystemPhase.Resolution`，遍历 cooldown store，清理过期项。

推荐本批次采用 **方案 A**，把系统数量压低。第 19 垂直切片如果需要长期诊断干净，再补 cleanup system。

因此 v0 中过期 cooldown 是惰性清理：当前 caster cast 前会清理，闲置 entity 的过期 cooldown 可能保留在 hash / SaveState 中，直到该 entity cast 或后续 cleanup system 处理。

## 测试要求

至少新增：

- `CooldownComponent_StartsAndReportsRemainingFrames`
- `CooldownComponent_RemoveExpiredClearsExpiredEntries`
- `AbilityRules_EvaluateRejectsOnCooldown`
- `AbilityRules_EvaluateRejectsInsufficientCost`
- `AbilityRules_CommitDeductsCostAndStartsCooldown`
- `CastComponentAbility_RuleRejectedDoesNotRunAbilityEffect`
- `CastComponentAbility_SuccessCommitsCostAndCooldown`
- `CastComponentAbility_CooldownBlocksSecondCastUntilEndFrame`
- `CastComponentAbility_CostFailureDoesNotStartCooldown`
- `CooldownSchema_HashChangesWhenCooldownChanges`
- `CooldownSchema_SaveStateRoundtripRestoresCooldown`

如果 targeting request 已完成，额外新增：

- `CastComponentAbilityRequest_RulesApplyToExplicitTargetCast`

## 默认 pipeline 接入

本批次仍不自动加入 default pipeline。Rules 是 ability command system 的内部能力，只要调用方注册 `GameplayComponentAbilityCommandSystem` 即可。

示例：

```csharp
abilityRegistry.Register(new GameplayComponentAttributeDeltaAbility(
    abilityId: 300001,
    attributeId: HpAttributeId,
    delta: -10,
    targetMode: GameplayComponentTargetMode.ExplicitSingle,
    rules: new GameplayComponentAbilityRuleSet(
        cooldownFrames: 30,
        costs: new[] { new GameplayAbilityCost(ManaAttributeId, 5) })));
```

具体构造函数形态按实现决定，但 rule set 必须明确绑定到 ability definition。

## 后续衔接

第 18 批完成后，当前阶段最后一步建议做：

```text
GAMEPLAY_ECS_STYLE_19_COMPONENT_RUNTIME_VERTICAL_SLICE
```

目标是把 spawn、attribute、targeting、ability、rules、death cleanup、event queue、hash、SaveState 串成一个最小可验收闭环。

## 验收

- Component ability 支持 cooldown gate。
- Component ability 支持 attribute cost gate。
- Rule rejected 不执行 ability effect。
- Ability success 后稳定扣 cost、启动 cooldown。
- Cooldown state 参与 diagnostics / hash / SaveState。
- Cost definition 不进入 world state。
- 不引入 cast time / interrupt / projectile / buff modifier。
- 旧 `CastAbility` 和旧 `RuntimeEntity` 路线不受影响。
- 文档和 `Docs/Interfaces/Gameplay.md` 同步新增 component ability rules 语义。
