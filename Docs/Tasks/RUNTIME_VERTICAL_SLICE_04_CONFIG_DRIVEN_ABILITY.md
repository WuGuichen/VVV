# 运行时垂直切片 04：Config Driven Ability

> **状态**: ✅ 已完成（r1192）
> **优先级**：P0
> 前置任务：`RUNTIME_VERTICAL_SLICE_03_GAMEPLAY_RUNTIME_CORE.md`
> 目标版本：Phase 11.2

## 目标

把已经抽成正式模块的 `MxFramework.Gameplay` 接到配置系统，让 Ability 不再只能由 Demo 代码硬编码创建，而是能由最小配置定义生成并运行。

目标链路：

```text
BasicAbilityConfig
  -> ConfigAbilityFactory
  -> ITargetSelector
  -> IAbilityEffect[]
  -> SimpleAbility
  -> RuntimeEntity
  -> Attributes / Buffs / Events
```

完成后，框架具备“用配置描述一个技能并驱动运行时执行”的基础能力，为后续 Ability 编辑器、AI 辅助生成、Mod 包和 WGame Ability 数据迁移打底。

本任务只做运行时配置垂直切片，不做编辑器。

## 背景

当前已经具备：

- `MxFramework.Gameplay`：`RuntimeEntity`、`SimpleAbility`、`ITargetSelector`、`IAbilityEffect`。
- `MxFramework.Config`：配置表、Schema、ID 范围、引用校验。
- `MxFramework.Config.Runtime`：配置驱动 Buff / Modifier、Runtime Patch、Mod Package。
- `RuntimeVerticalSlice.unity`：可以展示 Attributes / Buffs / Modifiers / Ability Slice。

缺口是：

- Ability 仍由 `RuntimeAbilitySliceRunner` 直接 new 出来。
- 配置系统还不能表达“技能选择目标并执行效果”。
- 后续编辑器如果现在继续做，只能编辑无法稳定驱动 Gameplay Runtime 的字段。

因此本阶段优先补齐 Ability 配置到运行时对象的最小桥接。

## 范围

### 必须完成

1. 新增 Ability 配置模型。

建议放在：

```text
Assets/Scripts/MxFramework/Config.Runtime/
```

或如果需要更清晰的职责，可新增：

```text
Assets/Scripts/MxFramework/Gameplay.Config/
```

首选保守方案：先放 `Config.Runtime`，因为当前 Buff / Modifier 的配置到运行时桥接已在该模块。

建议类型：

```text
BasicAbilityConfig
AbilityTargetSelectorKind
AbilityEffectConfig
AbilityEffectKind
```

最小字段：

```text
BasicAbilityConfig
  int Id
  LocalizedTextKey NameText
  LocalizedTextKey DescText
  AbilityTargetSelectorKind TargetSelectorKind
  AbilityEffectConfig[] Effects

AbilityEffectConfig
  AbilityEffectKind Kind
  int[] Parameters
```

首版 `AbilityEffectKind` 只支持：

- `DamageByAttackDefense`
- `ApplyBuff`

首版 `AbilityTargetSelectorKind` 只支持：

- `Self`
- `SingleEnemy`

2. 为 Ability 配置定义 Schema。

`BasicAbilityConfig.CreateSchema()` 至少包含：

- ID 范围。
- `NameText` / `DescText` 多语言字段。
- `TargetSelectorKind` 枚举字段。
- `Effects` 字段说明。

推荐 ID 范围：

```text
Ability: 300000-399999
```

如果现有 ID 规范已有不同约定，以现有文档为准；如果没有，则把本范围写入文档。

3. 实现 `ConfigAbilityFactory`。

建议类型：

```text
ConfigAbilityFactory
```

职责：

- 从 `IConfigProvider` 或 `IConfigTable<BasicAbilityConfig>` 读取 Ability 配置。
- 创建 `SimpleAbility`。
- 根据 `TargetSelectorKind` 创建 `ITargetSelector`。
- 根据 `Effects` 创建 `IAbilityEffect[]`。
- 对无法识别的 selector / effect 返回明确失败，不静默跳过。

建议 API：

```csharp
public sealed class ConfigAbilityFactory
{
    public bool TryCreate(int abilityId, out IAbility ability, out string error);
}
```

如果项目现有工厂风格不同，可以对齐已有 `ConfigBuffFactory<TConfig>` / `ConfigModifierFactory<TConfig>`。

4. 明确 `AbilityEffectConfig.Parameters` 映射。

首版参数规则必须写进代码注释和文档：

```text
DamageByAttackDefense:
  parameters[0] = attackAttributeId
  parameters[1] = defenseAttributeId
  parameters[2] = hpAttributeId

ApplyBuff:
  parameters[0] = buffId
```

`ApplyBuff` 应通过 `IBuffFactory` 或 `IBuffFactory` 适配器创建 Buff，不允许在 `ConfigAbilityFactory` 中硬编码 Demo Buff 类型。

如果没有传入 BuffFactory，创建 ApplyBuff 效果必须失败并返回明确错误。

5. 新增 Demo 配置数据。

在 Demo 层提供最小 Ability 配置：

```text
Strike
  id = 300001
  target = SingleEnemy
  effects = DamageByAttackDefense(AttrAttack, AttrDefense, AttrHp)

Ignite
  id = 300002
  target = SingleEnemy
  effects = ApplyBuff(BuffBurning)
```

可以新增：

```text
RuntimeAbilitySliceDemoData
```

或合并到现有 Demo 数据类，但必须保持 Demo 数据与框架 Runtime 解耦。

6. RuntimeVerticalSlice 接入配置驱动 Ability 模式。

复用场景：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

不新增 `.unity` 场景。

建议在 `RuntimeVerticalSliceRunner` 增加模式：

```csharp
[SerializeField] private bool _useConfigDrivenAbility;
```

或在 `RuntimeAbilitySliceRunner` 中增加：

```csharp
[SerializeField] private bool _useConfigDriven;
```

推荐后者：Ability Slice 的展示逻辑集中在 `RuntimeAbilitySliceRunner`，`RuntimeVerticalSliceRunner` 只负责自动挂载。

运行结果应与 02 的硬编码 Ability 行为一致：

```text
Player casts Strike on Enemy
Enemy HP 600 -> 490
Player casts Ignite on Enemy
Enemy receives Burning buff
Burning ticks and decreases HP
```

7. 保持硬编码 Ability 模式可用。

不要删除 02 的硬编码 Ability 示例。配置驱动是新增模式，用来对比和验证配置链路。

8. 自动化测试。

新增或扩展 EditMode 测试，至少覆盖：

- `ConfigAbilityFactory` 能从 `BasicAbilityConfig` 创建 `SimpleAbility`。
- `SingleEnemy` selector 配置能选中敌人。
- `DamageByAttackDefense` 能按配置参数扣 HP。
- `ApplyBuff` 能通过传入 BuffFactory 添加 Buff。
- 未知 effect kind 返回失败。
- `ApplyBuff` 缺少 BuffFactory 时返回失败。
- 参数数量不足时返回失败。
- 配置驱动 Ability cast 事件顺序稳定。

9. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md`
- `Docs/Interfaces/Gameplay.md` 或新增 `Docs/Interfaces/GameplayConfig.md`
- 本任务状态

说明必须包含：

- Ability 配置字段。
- TargetSelectorKind 支持项。
- AbilityEffectKind 支持项。
- Parameters 映射规则。
- 当前不是 WGame Ability JSON 迁移。

### 不做

- 不导入 WGame Ability JSON。
- 不做 Ability 编辑器。
- 不做外部 Authoring Editor 接入。
- 不做 Mod Package 接入。
- 不做复杂公式 DSL。
- 不做冷却、资源、吟唱、打断、动画、输入。
- 不做范围、碰撞、寻路、弹道。
- 不做多语言文本内容编辑，只保留 `LocalizedTextKey`。
- 不新增 Unity 场景。

## 设计约束

### 1. Config.Runtime 只做桥接，不做业务技能系统

`ConfigAbilityFactory` 只能把配置映射到 `MxFramework.Gameplay` 的运行时接口。

不要在本任务中加入：

- 项目专属技能类型。
- WGame Ability 字段。
- 角色、怪物、元素、职业等业务概念。
- 复杂公式或脚本执行。

### 2. ApplyBuff 不允许硬编码 Demo Buff

错误做法：

```text
if buffId == 100001 -> new AbilityBurningBuff()
```

正确做法：

```text
ConfigAbilityFactory receives IBuffFactory
ApplyBuffEffect calls factory-created buff
```

Demo 可以提供一个 Demo BuffFactory，把 `BuffBurning` 映射到 `AbilityBurningBuff`。

### 3. 失败必须显式

以下情况必须返回明确错误：

- Ability ID 不存在。
- TargetSelectorKind 不支持。
- EffectKind 不支持。
- Parameters 数量不足。
- ApplyBuff 缺少 BuffFactory。
- BuffFactory 无法创建指定 Buff。

不要静默返回空 Ability，也不要跳过坏 effect 后继续创建。

### 4. 配置结构先易读，不追求一次覆盖 WGame

本任务的配置结构服务于框架最小闭环，不服务于一次性兼容旧项目所有 Ability 数据。

后续 WGame Ability 迁移应基于这个稳定 API，再通过审计结果设计映射层。

## 验收标准

1. 新增 Ability 配置类型和 Schema。
2. 新增 `ConfigAbilityFactory` 或等价工厂。
3. `ConfigAbilityFactory` 能创建 `SimpleAbility`。
4. 配置支持 `Self` 和 `SingleEnemy` target selector。
5. 配置支持 `DamageByAttackDefense` 和 `ApplyBuff` effect。
6. `DamageByAttackDefense` 参数映射明确并有测试。
7. `ApplyBuff` 通过传入 BuffFactory 创建 Buff，不硬编码 Demo Buff。
8. 配置错误返回明确失败信息。
9. `RuntimeAbilitySliceRunner` 可切换硬编码 / 配置驱动 Ability。
10. 不新增 `.unity` 场景。
11. Unity Console 无编译 Error。
12. Ability / Config 相关 EditMode 测试通过。
13. 文档更新并说明当前不是 WGame Ability JSON 迁移。
14. 不引用 WGame 业务类型。

## 推荐测试

```text
ConfigAbilityFactory_CreatesDamageAbility
ConfigAbilityFactory_CreatesApplyBuffAbility
ConfigAbilityFactory_UnknownAbility_ReturnsError
ConfigAbilityFactory_UnknownEffect_ReturnsError
ConfigAbilityFactory_MissingParameters_ReturnsError
ConfigAbilityFactory_ApplyBuffWithoutBuffFactory_ReturnsError
ConfigDrivenAbility_CastPublishesEventsInOrder
RuntimeAbilitySlice_ConfigDriven_MatchesHardcodedDamage
```

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- 配置驱动 Ability 能在纯测试中跑通。
- Demo Ability Slice 仍保留硬编码模式。
- Demo Ability Slice 可切到配置驱动模式。
- 自动化测试通过。
- 文档已更新。
- 提交前运行：

```bash
Tools/GitNexus/gitnexus.sh detect-changes
```

- SVN 提交信息建议：

```text
Add config driven ability runtime slice
```
