# 运行时垂直切片 03：Gameplay Runtime Core 抽取

> **状态**: ✅ 已完成（r1189）
> **优先级**：P0
> 前置任务：`RUNTIME_VERTICAL_SLICE_02_ENTITY_ABILITY_EFFECT.md`
> 目标版本：Phase 11.1

## 目标

把 02 中已经跑通的 Demo 级 Entity / Ability / Target / Effect 抽成框架级运行时基础模块，让后续 Ability 配置、战斗逻辑、AI、预览和 Mod 数据都能引用同一套最小 Gameplay Runtime API。

目标不是继续堆功能，而是把已经验证过的最小闭环从：

```text
MxFramework.Demo.Ability
```

提升为：

```text
MxFramework.Gameplay
```

完成后，Demo 仍然能运行，但 Demo 只负责装配示例数据，不再拥有核心接口和通用运行时类型。

## 背景

02 已完成：

- `RuntimeEntity`
- `IAbility`
- `AbilityContext`
- `AbilityCastResult`
- `ITargetSelector`
- `IAbilityEffect`
- `SelfTargetSelector`
- `SingleEnemyTargetSelector`
- `DamageEffect`
- `ApplyBuffEffect`
- `AbilityEvent`
- `RuntimeAbilitySliceRunner`
- `AbilitySliceTests`

这些类型已经证明可以把：

```text
Entity -> Ability -> Target -> Effect -> Attributes -> Buff -> Events
```

串起来。但它们仍位于 Demo 目录，存在三个问题：

1. 后续真实 Ability Runtime 会引用 Demo 类型，边界会错。
2. 测试覆盖的是 Demo 命名空间，无法证明框架 API 稳定。
3. 文档说“后续决定哪些抽到核心模块”，现在必须做这个决定。

本任务就是 02 的结构收口。

## 范围

### 必须完成

1. 新增 `MxFramework.Gameplay` 运行时模块。

建议路径：

```text
Assets/Scripts/MxFramework/Gameplay/
Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef
```

要求：

- `rootNamespace = MxFramework.Gameplay`
- `noEngineReferences = true`
- 不引用 `UnityEngine`
- 不引用 `UnityEditor`
- 不引用 `MxFramework.Demo`
- 只引用必要基础模块：
  - `MxFramework.Core`
  - `MxFramework.Events`
  - `MxFramework.Attributes`
  - `MxFramework.Buffs`
  - `MxFramework.Modifiers`

2. 抽取 Gameplay 核心接口和基础类型。

从 Demo 提升到 Gameplay：

```text
IRuntimeEntity
RuntimeEntity
IAbility
AbilityContext
AbilityCastResult
ITargetSelector
IAbilityEffect
AbilityEvent
AbilityEventType
SimpleAbility
SelfTargetSelector
SingleEnemyTargetSelector
DamageEffect
ApplyBuffEffect
```

命名空间统一为：

```csharp
namespace MxFramework.Gameplay
```

允许按框架 API 质量做小幅改名，但不能改变 02 的外部行为。

3. 明确 `RuntimeEntity` 的定位。

`RuntimeEntity` v0 是框架提供的默认组合实现，必须包含：

- `EntityId`
- `TeamId`
- `IsAlive`
- `AttributeStore`
- `BuffPipeline`
- `ModifierPipeline`
- `AbilityEvents`
- `IBuffTarget` 适配

约束：

- `RuntimeEntity` 不继承 `MonoBehaviour`。
- `RuntimeEntity` 不读取 Unity `Time`。
- `RuntimeEntity` 不绑定 GameObject。
- `RuntimeEntity` 可以被 Unity、CLI 测试、外部工具共同使用。

4. 修正 Demo 引用。

`Assets/Scripts/MxFramework/Demo/Ability/` 中只保留 Demo 专属内容，例如：

- `RuntimeAbilitySliceRunner`
- `AbilityBurningBuff`
- `AbilityConst`

Demo 应引用 `MxFramework.Gameplay`，而不是继续定义自己的核心接口。

`MxFramework.Demo.asmdef` 增加对 `MxFramework.Gameplay` 的引用。

5. 修正测试引用。

把 `AbilitySliceTests` 从 Demo 类型迁移到 Gameplay 类型。

`MxFramework.Tests.asmdef` 应引用 `MxFramework.Gameplay`。

如果测试不再需要 Demo 类型，应移除 `MxFramework.Demo` 测试引用；如果仍测试 Runner 或 Demo Buff，可以保留，但核心逻辑测试必须优先覆盖 Gameplay 模块。

6. 增加模块级 API 文档。

新增或更新：

```text
Docs/Interfaces/Gameplay.md
```

内容至少包含：

- 模块定位。
- 为什么 Gameplay 不依赖 Unity。
- Entity / Ability / Target / Effect 的最小关系。
- 当前 v0 支持什么。
- 当前 v0 不支持什么。
- 后续 Ability 配置如何接入。

7. 更新使用文档和能力清单。

至少更新：

- `Docs/USAGE.md`
- `Docs/CAPABILITIES.md`
- 本任务状态

`USAGE.md` 中 02 新增的示例应改为引用 `MxFramework.Gameplay`。

8. 保持现有垂直切片可运行。

复用：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

不新增场景。

`RuntimeVerticalSliceRunner._useAbilitySlice = true` 时仍能自动挂载 `RuntimeAbilitySliceRunner` 并运行。

9. 自动化测试。

至少保留或新增测试：

- `Gameplay_TargetSelector_SelectsFirstEnemy`
- `Gameplay_DamageEffect_ReducesTargetHp`
- `Gameplay_ApplyBuffEffect_AddsBuff`
- `Gameplay_BurningBuff_TicksHp`
- `Gameplay_AbilityCast_PublishesEventsInOrder`
- `Gameplay_AbilityCast_NoEnemy_ReturnsFailure`
- `Gameplay_RuntimeEntity_IsAliveReflectsHp`

测试重点是 Gameplay 模块本身，不是 Demo UI。

### 不做

- 不做 WGame Ability JSON 导入。
- 不做 Ability 配置表。
- 不做 Ability 编辑器。
- 不做冷却、资源、吟唱、打断、动画、输入。
- 不做范围碰撞、寻路、物理查询。
- 不做 Mod Package 接入。
- 不新增 Unity 场景。
- 不把所有战斗系统一次抽象完。

## 设计约束

### 1. Gameplay 是运行时基础，不是业务战斗系统

Gameplay 模块只提供最小组合能力：

```text
实体
技能
目标选择
效果执行
事件
```

具体项目仍负责：

- 属性 ID 定义。
- Buff 类型定义。
- 技能配置来源。
- 战斗规则扩展。
- 公式系统。
- 表现和输入。

### 2. 抽取时不能制造“大而全”接口

02 里没有验证过的字段不要提前加。

不要在本任务中加入：

- Cooldown
- Mana / Cost
- CastTime
- Interrupt
- Range
- Projectile
- AnimationKey
- LocalizationKey
- AssetKey

这些都应该等配置驱动 Ability 或 Demo 需要时再进入 API。

### 3. Demo 只负责演示

Demo 可以有：

```text
AbilityConst
AbilityBurningBuff
RuntimeAbilitySliceRunner
```

Demo 不应该再拥有：

```text
IAbility
IRuntimeEntity
IAbilityEffect
ITargetSelector
AbilityEvent
SimpleAbility
DamageEffect
ApplyBuffEffect
```

### 4. API 必须 AI 友好

类型名、字段名和文档必须让 AI Agent 不读实现也能判断用途。

最低要求：

- 文件名和类型名一一对应。
- 每个 public 类型有一句 summary。
- `Docs/Interfaces/Gameplay.md` 给出最小完整示例。
- `USAGE.md` 能直接复制出核心调用链。

## 验收标准

1. 新增 `MxFramework.Gameplay.asmdef`。
2. `MxFramework.Gameplay` 不引用 UnityEngine / UnityEditor。
3. Demo 中核心 Ability 类型已迁移到 Gameplay 模块。
4. `RuntimeAbilitySliceRunner` 继续可运行。
5. `RuntimeVerticalSliceRunner._useAbilitySlice = true` 不需要手动挂组件。
6. Gameplay 核心测试全部通过。
7. Unity Console 无编译 Error。
8. `Docs/Interfaces/Gameplay.md` 存在并能解释核心 API。
9. `Docs/USAGE.md` 示例引用 `MxFramework.Gameplay`。
10. `Docs/CAPABILITIES.md` 把 Gameplay Runtime 标为可用能力。
11. 不引入 WGame 业务类型。
12. 不新增 `.unity` 场景。

## 推荐验证

```text
Unity EditMode:
MxFramework.Tests.Ability.AbilitySliceTests
```

如测试类改名，应使用新的 Gameplay 测试类。

还需用 Unity MCP 检查：

- Console error = 0
- Ability 相关测试 passed

提交前运行：

```bash
Tools/GitNexus/gitnexus.sh detect-changes
```

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- 新增 Gameplay Runtime 模块。
- Demo Ability Slice 仍能运行。
- 自动化测试通过。
- 文档已更新。
- SVN 提交信息建议：

```text
Promote gameplay runtime core from ability slice
```
