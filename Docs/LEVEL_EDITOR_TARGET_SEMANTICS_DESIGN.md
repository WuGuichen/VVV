# Mx 关卡编辑器 P3.1：目标语义基础设计

> Status: Current
>
> Implementation state: Implemented; P3.1 hardening complete
>
> Last verified: 2026-07-28
>
> Phase: Level Editor P3.1
>
> Scope: Gameplay 目标基础身份、能力、关系、标签、状态分层，以及关卡 Trigger 首次投影
>
> Parent: [P3.0 通用创作引用设计](LEVEL_EDITOR_COMMON_AUTHORING_REFERENCES_DESIGN.md)

## 1. 设计结论

目标不能由一个万能枚举表达。P3.1 将目标语义稳定拆成六层：

| 层 | 回答的问题 | 现有或新增权威 |
|---|---|---|
| 目标来源 | 从哪里取得目标 | `GameplayTriggerTargetSelector`、显式绑定 |
| 实例身份 | 具体是哪一个运行时实体 | `GameplayEntityId` |
| 定义身份 | 这个实例由哪份定义产生 | `GameplayIdentityComponent` |
| 基础身份与能力 | 它是角色、世界对象、点或区域；是否可伤害、可移动或静态 | 新增 Gameplay target classification |
| 目标关系 | 相对发起者是自身、同队、敌对或中立 | `GameplayTargetRelationFilter`、`GameplayTeamRelation` |
| 标签与状态 | 玩家控制、Boss、召唤物等标签；存活、中毒、眩晕等状态 | `GameplayTagComponent`、`GameplayStatusComponent`、`GameplayLifecycleComponent` |

`Owner / Subject / Instigator / ExplicitBindingTarget` 只表示来源，不再被称为身份类型。`Player / Enemy / NPC / Boss / Summon` 不进入框架基础身份枚举：Player/NPC 属于控制或业务标签，Enemy 属于相对关系，Boss/Summon 属于标签或定义能力。

## 2. 基础身份与能力

### 2.1 基础身份

`GameplayTargetIdentityTypes` 是可组合 flags，但单个实体的 classification 首版只允许一个基础身份：

| 值 | 稳定数值 | 含义 |
|---|---:|---|
| `None` | 0 | 未声明；用于旧实体兼容，不等于任意目标 |
| `Character` | 1 | 使用统一 Character 契约的角色或 Actor |
| `WorldObject` | 2 | 具有玩法身份的世界对象 |
| `SpatialPoint` | 4 | 出生点、检查点、标记点等空间点 |
| `SpatialRegion` | 8 | 触发区、测试区等空间区域 |

角色的玩家、敌人、NPC、Boss、召唤物、奇幻生物或简单几何表现不改变 `Character` 基础身份。它们继续由 controller、team、definition、tags、ability 和 presentation 区分。

### 2.2 能力

`GameplayTargetCapabilities` 首版只声明已有系统能证明的通用能力：

| 值 | 稳定数值 | 含义 |
|---|---:|---|
| `None` | 0 | 不声明通用能力 |
| `Damageable` | 1 | 可以进入正式 Gameplay/Combat 伤害或效果链 |
| `Movable` | 2 | 具有可变的权威空间状态 |
| `Static` | 4 | 作为静态世界目标存在 |

能力可以组合。可移动且可伤害的世界物体仍是 `WorldObject`，而不是在多个相互冲突的基础身份之间选择。

不在首版增加 `Interactable`、`Collectible`、`Destructible`、`Boss` 等尚无统一 Runtime 权威的值。出现正式模块和查询需求后，再以兼容新增位扩展。

### 2.3 关系

关系不是身份，稳定数值继续由既有 `GameplayTargetRelationFilter` 拥有：

| 值 | 稳定数值 | 含义 |
|---|---:|---|
| `Any` | 0 | 不限制关系 |
| `Self` | 1 | 仅发起者自身 |
| `SameTeam` | 2 | 仅同队 |
| `Enemy` | 3 | 仅敌对 |
| `Neutral` | 4 | 仅中立 |

Query 构造时必须拒绝未定义的关系值、身份位或能力位，不能让未知枚举落入 `switch` 默认分支后被静默解释成`不限`。

## 3. 与现有模块的映射

### 3.1 Gameplay

- `GameplayEntityId` 继续解决 generation-safe 实例身份。
- `GameplayIdentityComponent` 继续保存 definition/variant，不承担类型枚举。
- 新增 `GameplayTargetClassificationComponent`，只保存基础身份和通用能力。
- `GameplayComponentTargetCandidate` 从 Component World 投影 classification、team、lifecycle、tags 和 statuses。
- `GameplayComponentTargetQuery` 可选限制基础身份和必需能力；未配置限制时保持现有行为。
- `GameplayTargetRelationFilter` 补齐 `Neutral`，与已经存在的 `GameplayTeamRelation.Neutral` 对齐。
- `GameplayTargetQuery` 与 `GameplayComponentTargetQuery` 在构造阶段验证关系值；component query 同时验证身份与能力 flags。

目标筛选顺序固定为：

```text
基础身份
  -> 必需能力
  -> 存活状态
  -> 关系
  -> 必需标签
  -> 阻止状态
  -> 数量上限
```

未知 classification 只在查询没有身份或能力限制时兼容通过；一旦作者要求具体身份或能力，未知实体必须 fail-visible 地被拒绝。

### 3.2 Character

Character runtime 创建 Gameplay 实体时声明：

```text
Identity: Character
Capabilities: Damageable | Movable
```

这不区分玩家、敌人、NPC、Boss 或召唤物。相同 Character 契约继续通过队伍、控制来源、定义和 Tag 表达差异。

### 3.3 Combat

`CombatHitTargetTypes` 保持现有协议，不迁移：

| Combat 表达 | P3.1 语义拆分 |
|---|---|
| `Self` | `Character` + Self relation |
| `FriendlyCharacter` | `Character` + SameTeam relation |
| `HostileCharacter` | `Character` + Enemy relation |
| `ThirdPartyCharacter` | `Character` + Neutral/third-party project relation |
| `DamageableObject` | `WorldObject` + `Damageable` |
| `WorldWall / WorldGround / WorldStatic` | `WorldObject` + `Static`，具体表面类型仍由 Combat 拥有 |

P3.1 不让 Gameplay 反向依赖 Combat，也不把 Combat 的墙面/地面判定复制到 Gameplay。

### 3.4 Level Authoring

关卡对象继续由 `LevelAuthoringDefinitionUsage` 拥有正式分类：

| Level usage | P3.1 基础身份 |
|---|---|
| `Movable` | `WorldObject` + `Movable` |
| `StaticCollider` | `WorldObject` + `Static` |
| `Point` | `SpatialPoint` |
| `Region` | `SpatialRegion` |

该映射属于业务 Editor/组合层，不让 `MxFramework.Gameplay` 依赖 `Config.Levels`。

## 4. 关卡 Trigger 首次接入

P3.1 不修改 Trigger JSON 或 Runtime action payload。Inspector 只投影当前正式组合已经保证的语义：

| 当前模板 | 来源 | 基础身份 | 关系 / Tag / Status |
|---|---|---|---|
| 区域 Buff | 进入区域的角色 (`Subject`) | `Character`，`Damageable + Movable` | 当前无限定 |
| Story | 当前区域 (`ExplicitBindingTarget`) | `SpatialRegion` | 不适用 |

界面保持紧凑：

```text
作用对象
  进入区域的角色
  可伤害 · 阵营不限
```

Trigger Source / Subject、`Movable`、无 Tag/Status 限制等完整技术语义放在 Tooltip。只有存在可持久化的正式筛选字段时，身份、关系、Tag 或 Status 才升级为选择控件。本阶段不制造无效下拉框。

## 5. 数据、兼容与性能

- 新 classification 是可选 component；旧实体不自动猜测身份。
- 新 component 使用独立 schema，不修改 `GameplayIdentityComponent` schema。
- classification 进入 Gameplay diagnostics、Runtime hash 和 SaveState，与其他 core component 一致。
- public enum 只做兼容追加，不重排已有数值。
- 未声明 classification 的旧 candidate 在未配置身份/能力筛选时继续兼容通过；一旦配置任一相关筛选则给出 typed rejection。
- Targeting candidate/query 构建属于 `AllocByDesign`；候选过滤循环只做 struct/enum/bit 运算，不新增 LINQ、临时集合、字符串、反射或 Unity 对象查询。
- Editor 投影为低频 `AllocByDesign`。

## 6. 非目标

P3.1 不做：

- 全局 Player/NPC/Boss/Summon 枚举；
- 第二套 Team、Tag、Status、Combat target type 或 Level object kind；
- Gameplay 对 Combat、Config.Levels、Character 或 Unity 的反向依赖；
- Trigger JSON schema、SaveState 外层协议、其他既有 component schema、网络协议或 action payload 迁移；新增 classification 只使用自己的 v1 component schema；
- Tag/Status 正式目录编辑器、复杂布尔筛选表达式或搜索窗口；
- Unity Physics、Collider 或 GameObject identity 作为玩法权威。

## 7. 验收门禁

- classification 拒绝非法基础身份位和未知能力位。
- query 拒绝未知关系值、身份位和能力位；稳定 enum 数值有直接回归测试。
- component diagnostics、hash 和 SaveState roundtrip 保留身份及能力。
- Component target query 能按身份、能力、中立关系、Tag、Status 稳定筛选并给出 typed rejection。
- 无身份限制时，旧 candidate 行为不变。
- Character spawn 和 CharacterTest runtime entity 均声明 `Character + Damageable + Movable`。
- 关卡 Trigger UI 以人性化`作用对象`摘要合并显示来源结果与必要要求；完整来源和身份规则仍保留在 Tooltip，Buff 和 Story 映射与现有 route/binding/runtime 事实一致。
- 不修改 Trigger authoring JSON、runtime action payload 或 Combat hit config。
- 执行 V4：Unity 编译、Console、focused EditMode、轻量检查、代码规范报告和实际窗口视觉检查。

下一阶段的可持久化筛选、阵营参照、Combat Sensor 执行点与迁移边界见
[P3.2 目标筛选创作设计](LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md)。
