# 运行时垂直切片 02：Entity / Ability / Target / Effect

> **状态**: ✅ 已完成（r1186）
> **优先级**：P0
> 前置任务：`RUNTIME_VERTICAL_SLICE_01_PLAYABLE_ATTRIBUTES_BUFFS_MODIFIERS.md`
> 目标版本：Phase 11.0

## 目标

暂停 Mod 编辑器产品化，把重心拉回框架运行时基础：做一个更接近游戏本体的最小可运行闭环。

目标链路：

```text
Entity
  -> Ability Cast
  -> Target Selection
  -> Effect Apply
  -> Attributes
  -> Buff / Modifier
  -> Events
  -> Snapshot
```

完成后，框架不再只是“属性、Buff、Modifier 能单独跑”，而是能表达一个最小游戏行为：实体释放 Ability，选择目标，执行效果，修改属性或施加 Buff，并发布事件与快照。

## 背景

当前已经具备：

- `Attributes`：属性存取、modifier、变化事件。
- `Buffs`：生命周期、堆叠、快照。
- `Modifiers`：条件 + 效果管线。
- `Config.Runtime`：配置驱动 Buff/Modifier、Patch、Mod Package。
- `RuntimeVerticalSlice.unity`：能展示 Attributes + Buffs + Modifiers。

但仍缺一个更基础的游戏行为层：

- Entity 的最小抽象是什么。
- Ability 如何被触发。
- 目标选择如何表达。
- Effect 如何作用到目标。
- Ability、Buff、Modifier、Events 如何串成一个玩法闭环。

本任务只做最小运行时模型，不引入 WGame 真实 Ability 数据，也不做编辑器。

## 范围

### 必须完成

1. 新增最小 Entity 抽象。

建议先在 Demo 中跑通，再决定是否提升到核心模块。

最小 Entity 必须包含：

- `EntityId`
- `TeamId`
- `AttributeStore`
- `BuffPipeline`
- `ModifierPipeline`
- 事件发布入口
- 快照入口

如果先放 Demo，任务完成时必须明确哪些类型值得提升到核心模块。

2. 新增 Ability 最小模型。

建议类型：

```text
IAbility
AbilityContext
AbilityCastResult
```

最小字段：

- abilityId
- caster
- selected targets
- result success/failure
- failure reason

Ability v0 不做冷却、蓝耗、吟唱、打断、动画、输入。

3. 新增 Target Selection 最小接口。

建议接口：

```csharp
public interface ITargetSelector
{
    IReadOnlyList<IRuntimeEntity> SelectTargets(AbilityContext context);
}
```

至少实现：

- `SelfTargetSelector`
- `SingleEnemyTargetSelector`

目标选择规则必须与 TeamId 相关：

- self：返回 caster。
- single enemy：从候选列表中选择第一个不同 TeamId 的存活目标。

4. 新增 Effect 最小接口。

建议接口：

```csharp
public interface IAbilityEffect
{
    void Apply(AbilityContext context, IRuntimeEntity target);
}
```

至少实现：

- `DamageEffect`：读取 caster Attack，扣目标 HP。
- `ApplyBuffEffect`：给目标添加一个 Buff。

Damage 公式 v0：

```text
damage = max(1, caster.Attack - target.Defense)
```

如果当前属性都是 int，保持 int，不引入浮点复杂度。

5. 新增 Ability 事件。

至少发布：

```text
AbilityCastStarted
AbilityTargetSelected
AbilityEffectApplied
AbilityCastFinished
```

事件可以先使用现有 `EventBus<T>` 或 Demo 内简单事件记录。必须能在 OnGUI 或日志看到事件序列。

6. 复用现有场景，不新增 Unity 场景。

继续使用：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

可以在现有 `RuntimeVerticalSliceRunner` 上新增模式：

```csharp
[SerializeField] private bool _useAbilitySlice;
```

也可以新增独立 runner 组件，但不新建 `.unity` 场景。

7. Demo 必须展示两个实体。

最小实体：

```text
Player
  TeamId = 1
  Hp = 1000
  Attack = 120
  Defense = 20

Enemy
  TeamId = 2
  Hp = 600
  Attack = 80
  Defense = 10
```

运行后执行：

```text
Player casts Strike on Enemy
Enemy HP decreases
Player casts Ignite on Enemy
Enemy receives Burning buff
Burning ticks and further decreases HP
```

8. 可视输出。

OnGUI 或日志必须显示：

- Player / Enemy HP、Attack、Defense。
- 当前 Ability cast 结果。
- target selection 结果。
- damage 结果。
- active buffs。
- ability event log。
- attribute changed event log。

9. 测试。

至少新增 EditMode 或 PlayMode 测试覆盖核心纯逻辑：

- Ability 选择敌方目标。
- DamageEffect 扣 HP。
- ApplyBuffEffect 添加 Buff。
- Ability cast 发布事件顺序。
- Buff tick 后继续影响 HP。

如果首版逻辑放 Demo，仍应把可测试部分抽成纯 C# 类，避免只能手动 Play。

10. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- 必要时新增或更新 `Docs/Interfaces/Gameplay.md`

说明：

- Entity / Ability / Target / Effect 的最小使用方式。
- 这不是 WGame Ability 迁移。
- 后续真实 Ability 配置会基于这个运行时闭环扩展。

### 不做

- 不做 WGame 真实 Ability JSON 导入。
- 不做 Ability 编辑器。
- 不做技能冷却、资源消耗、吟唱、打断、动画、输入。
- 不做复杂范围选择、碰撞检测、寻路。
- 不做多语言文本。
- 不做 Mod 包接入。
- 不新增 Unity 场景。

## 建议实现

### 1. 先 Demo 验证，再决定提升模块

不要一开始设计过大的 Gameplay 模块。建议先在 Demo 中跑通闭环：

```text
RuntimeAbilitySliceRunner
  RuntimeEntity
  SimpleAbility
  TargetSelector
  Effect
```

当测试证明接口稳定，再把通用接口移动到 `Gameplay.Runtime` 或合适模块。

### 2. Ability 不直接操作 UI

Ability 只产生结果和事件，不直接写 OnGUI。Runner 负责展示。

### 3. Effect 只做一件事

不要让一个 Effect 既选择目标又计算伤害又处理 Buff。拆开：

```text
Selector selects targets
Ability loops targets
Effect applies behavior
```

### 4. 事件顺序要稳定

事件顺序应固定，便于测试和 AI Agent 分析：

```text
CastStarted
TargetSelected
EffectApplied
CastFinished
```

失败时：

```text
CastStarted
CastFailed
```

如果实现 `CastFailed`，需写进文档和测试。

## 验收标准

1. Unity 编译无 error。
2. 不新增 `.unity` 场景。
3. `RuntimeVerticalSlice.unity` 可切到 Ability Slice 模式。
4. Demo 中存在 Player 和 Enemy 两个运行时实体。
5. `SingleEnemyTargetSelector` 能选中 Enemy。
6. `DamageEffect` 能按 `Attack - Defense` 扣 HP。
7. `ApplyBuffEffect` 能给 Enemy 添加 Burning Buff。
8. Burning Buff tick 后继续扣 Enemy HP。
9. AttributeChangedEvent 能记录 Ability 和 Buff 导致的 HP 变化。
10. Ability 事件顺序稳定且可测试。
11. OnGUI 或日志能看到 cast、target、effect、buff、attribute 结果。
12. 运行 10 秒无异常。
13. 自动化测试覆盖 target selection、damage、apply buff、event order。
14. 不引用 WGame 业务类型。

## 推荐测试

- `AbilitySlice_TargetSelector_SelectsFirstEnemy`
- `AbilitySlice_DamageEffect_ReducesTargetHp`
- `AbilitySlice_ApplyBuffEffect_AddsBuff`
- `AbilitySlice_BurningBuff_TicksHp`
- `AbilitySlice_Cast_PublishesEventsInOrder`
- `AbilitySlice_NoEnemy_ReturnsFailure`

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- RuntimeVerticalSlice 可运行 Ability Slice。
- 自动化测试通过。
- `Docs/CAPABILITIES.md` 更新运行时核心能力。
- `Docs/USAGE.md` 增加 Entity / Ability / Effect 最小示例。
- SVN 提交信息建议：

```text
Add runtime entity ability effect slice
```
