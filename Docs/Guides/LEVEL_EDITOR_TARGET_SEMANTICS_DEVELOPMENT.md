# Mx 关卡编辑器 P3.1：目标语义基础开发指南

> Status: Current
>
> Implementation state: Implemented; P3.1 hardening complete
>
> Last verified: 2026-07-28
>
> Parent design: [目标语义基础设计](../LEVEL_EDITOR_TARGET_SEMANTICS_DESIGN.md)
>
> Scope: Gameplay classification、component targeting 与 CharacterTest Level Editor 投影

## 1. API 复用计划

| 需求 | 复用能力 | P3.1 做法 |
|---|---|---|
| 实例身份 | `GameplayEntityId` | 保持 generation-safe identity |
| 定义身份 | `GameplayIdentityComponent` | 保持 definition/variant，不增加职责 |
| 基础身份 | Gameplay core component schema | 新增独立 classification component |
| 关系 | `GameplayTeamRelation`、`GameplayTargetRelationFilter` | 复用并补齐 Neutral filter |
| 标签 / 状态 | `GameplayTagComponent`、`GameplayStatusComponent`、`GameplayLifecycleComponent` | 直接进入现有 candidate/query |
| 目标筛选 | `GameplayComponentTargetCandidate/Query/TargetingService` | 增加可选 identity/capability filter |
| Character 分类 | Character spawn builder、CharacterTest runtime world | 创建实体时显式写 classification |
| Level 分类 | `LevelAuthoringDefinitionUsage` | 只在 Editor adapter 中映射 |
| Trigger 来源 | `GameplayTriggerTargetSelector`、route/binding/compiler | 保持 P3.0 解析，不改 schema |
| Editor 控件 | `MxAuthoringReferenceField`、紧凑只读摘要 | 面向创作者显示作用对象，技术来源与身份规则进入 Tooltip |
| Combat | `CombatHitTargetTypes` | 保持权威，不复制或迁移 |

未使用 Unity Physics、独立目标注册中心、反射 Provider 或第二份 Runtime 目录。

## 2. 高频路径与分配

| 路径 | 分类 | 约束 |
|---|---|---|
| classification struct 属性与 bit 检查 | `NoAlloc` | 不使用字符串、集合或反射 |
| `GameplayComponentTargetingService` candidate loop | 既有筛选热路径 | 只增加常量时间 enum/flags 判断 |
| Character entity 初始化 | 低频初始化 | 写一个 component store value |
| Candidate/query 构建 | `AllocByDesign` | 延续现有数组复制行为 |
| Level Editor Inspector | Editor-only `AllocByDesign` | 只在构建/刷新时生成摘要 |

## 3. 公共契约

### 3.1 `GameplayTargetIdentityTypes`

基础身份 flags。Candidate classification 首版只接受 `None` 或单一 bit；Query 可以组合多个允许身份。

### 3.2 `GameplayTargetCapabilities`

通用能力 flags。Candidate 可组合；Query 用 `RequiredCapabilities` 表示必须全部具备。

### 3.3 `GameplayTargetClassificationComponent`

与 `GameplayIdentityComponent` 并列：

- `GameplayIdentityComponent`：具体 definition/variant；
- `GameplayTargetClassificationComponent`：跨定义筛选使用的基础身份/能力。

新增 component 必须同时注册 diagnostics、hash 与 SaveState adapter。schema stable id 固定为 `mxframework.gameplay.target_classification`，version 从 1 开始。

### 3.4 Component Targeting

`GameplayComponentTargetCandidate` 增加 classification 快照；`GameplayComponentTargetCandidates` 从 world 的 classification store 读取，缺失时使用 default。

`GameplayComponentTargetQuery` 增加：

- `AcceptedIdentityTypes`：`None` 表示不限制；
- `RequiredCapabilities`：`None` 表示不限制。

筛选失败分别返回：

- `IdentityTypeMismatch`
- `MissingRequiredCapability`

`DetailId` 保存实际或缺失 flags 的数值，便于诊断。

### 3.5 关系值与非法输入

`GameplayTargetRelationFilter` 的稳定值为 `Any=0 / Self=1 / SameTeam=2 / Enemy=3 / Neutral=4`。`GameplayTargetQuery` 与 `GameplayComponentTargetQuery` 都在构造阶段使用无反射、常量时间的校验拒绝未知值；component query 同时拒绝未定义 identity/capability flags。

未声明 classification 的旧 candidate 只在 query 没有身份或能力限制时兼容通过。作者一旦要求具体身份或能力，结果分别使用 `IdentityTypeMismatch` 或 `MissingRequiredCapability` 显式拒绝。

## 4. Character 接入

正式 Character spawn definition 与 CharacterTest runtime world 都写：

```csharp
new GameplayTargetClassificationComponent(
    GameplayTargetIdentityTypes.Character,
    GameplayTargetCapabilities.Damageable
        | GameplayTargetCapabilities.Movable)
```

不得根据队伍把角色写成 Player/Enemy，也不得根据控制器写成 NPC。关系和控制角色属于其他轴。

## 5. Level Editor 投影

CharacterTest 适配器根据已验证的 Trigger action family、target selector、source binding 和当前项目组合约束构造只读摘要：

- Buff Subject：`Character + Damageable + Movable`、关系 Any、无额外 Tag/Status。
- Story explicit source binding：`SpatialRegion`、关系不适用。

摘要只描述已有 Runtime/Authoring 事实，不写回文档。未来新增持久化筛选字段时，继续通过 `MxAuthoringReferenceField` 的 0/1/多项规则升级，不能让只读摘要变成第二份配置。

## 6. 测试范围

最低 focused 集合：

- `GameplayCoreComponentTests`
- `GameplayComponentSchemaRegistryTests`
- `GameplayComponentWorldHashContributorTests`
- `GameplayComponentWorldSaveStateTests`
- `GameplayComponentAbilityTargetingTests`
- `CharacterRuntimeSpawnFirstSliceTests`
- `CharacterTestLevelGameplayTriggerAuthoringTests`

验证重点：

1. classification 构造、相等性和非法 flags。
2. identity、capability 和 relation enum 稳定数值，以及 query 对非法值的 fail-fast。
3. diagnostics/hash/SaveState 稳定保留字段。
4. identity/capability/Neutral relation/Tag/Status 过滤顺序和拒绝原因。
5. default query 对未声明 classification 的旧 candidate 兼容；配置筛选后显式拒绝。
6. Inspector 可见摘要与 Tooltip 同时来自同一解析结果。
7. Character 初始化写入正确 classification。
8. Inspector 来源与身份不混合，且不产生文档 dirty。

## 7. 手动检查

1. 打开 `MxFramework > Level Editor`，进入世界对象。
2. 选择带 Buff Trigger 的区域。
3. 确认`作用对象`显示“进入区域的角色”。
4. 确认下一行显示“可伤害 · 阵营不限”，界面不显示 Trigger Source / Subject；Tooltip 仍包含 `Character + Damageable + Movable + Any relation` 及当前无 Tag/Status 限制。
5. 切换 Story Trigger，确认作用对象只显示“当前区域”，Tooltip 仍说明 `ExplicitBindingTarget + SpatialRegion`。
6. 展开/折叠、切换条目后确认 Inspector 不跳顶、不产生未保存内容。

## 8. 状态台账

| 项目 | 状态 |
|---|---|
| 设计边界 | Implemented |
| Gameplay classification | Current |
| Component targeting | Current |
| Character integration | Current |
| Level Editor projection | Current |
| Trigger JSON / Runtime payload | Unchanged |
| 非法输入与兼容护栏 | Current |
| Unity verification | Passed：Unity 6000.5.0f1 编译、Console 0 error、95/95 focused EditMode、实际窗口投影检查 |
