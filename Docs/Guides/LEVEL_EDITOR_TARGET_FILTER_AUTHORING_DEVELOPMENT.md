# Mx 关卡编辑器 P3.2：目标筛选创作开发指南

> Status: Current
>
> Implementation state: Implemented
>
> Last verified: 2026-07-28
>
> Parent design: [目标筛选创作设计](../LEVEL_EDITOR_TARGET_FILTER_AUTHORING_DESIGN.md)
>
> Scope: CharacterTest Combat 区域 Subject Filter、关卡 Trigger Inspector 与 Runtime Sensor

## 1. API 复用计划

| 需求 | 复用能力 | P3.2 做法 |
|---|---|---|
| 身份和能力 | `GameplayTargetClassificationComponent` | 直接读取 Component World，不建立关卡专用身份枚举 |
| 存活 | `GameplayLifecycleComponent` | 固定要求 Alive |
| 阵营 | `GameplayTeamComponent`、`GameplayTargetRelationFilter`、`GameplayTeamRelations` | 以场景角色 `TeamId`作为显式参照 |
| 空间候选 | `CombatPhysicsWorld`、`CombatEntityGameplayMap` | 继续由正式 Combat Sensor 查询和映射，不使用 Unity Physics |
| Trigger 输入 | complete occupancy、`GameplayTriggerRuntime.TrySubmitOccupancy` | 在提交前筛选，保持 Entered/Stayed/Exited 语义 |
| Editor 候选 | `MxAuthoringReferenceField` | 关系和参照队伍沿用 P3.0 的目录引用规则 |
| 保存与撤销 | 关卡文档事务、Undo session、保存基线 | 每次关系、队伍或能力修改为一个 Undo step |

没有新增 Team Registry、Runtime 目标来源模型、Collider Trigger 或并行 Trigger 协议。

## 2. 高频路径与分配

`GameplayTriggerCombatSensorSystem.Tick` 是高频路径。P3.2 在初始化时缓存 classification、team 和 lifecycle component store，并预分配映射去重与诊断空间；每个 candidate 只执行常量时间的 enum/flags/TeamId 判定。

- `GameplayComponentTargetFilter`和单 candidate evaluator：`NoAlloc`。
- Sensor Tick：初始化后 `NoAlloc`，不使用 LINQ、临时集合、字符串拼接、反射或 Unity 对象查找。
- component query/result 快照：延续 `AllocByDesign`。
- Inspector、目录候选、JSON 迁移和 scene cache 编译：Editor/启动期 `AllocByDesign`。

## 3. 公共 Runtime 契约

`GameplayComponentTargetFilter`是不可变值，包含：

- `AcceptedIdentityTypes`
- `RequiredCapabilities`
- `RequireAlive`
- `RelationFilter`
- `ReferenceEntityId`
- `ReferenceTeamId`

`Self`必须提供有效 generation-safe 实体；`SameTeam / Enemy / Neutral`必须提供正整数参照 TeamId；`Any`不得携带参照队伍。

`GameplayComponentTargetFilterEvaluator.TryAccept(...)`返回稳定的 `GameplayTargetRejectReason`和数值 detail。`GameplayComponentTargetingService`与 Combat Sensor 复用同一个 evaluator，避免低频目标选择和区域 Sensor 形成两套判定。

## 4. CharacterTest Authoring

筛选保存在 `CharacterTestLevelGameplayTriggerRegionBinding.SubjectFilter`，不修改 `GameplayTriggerAuthoringJson v1`：

| 模板 | 默认身份 | 默认能力 | 存活 | 默认关系 |
|---|---|---|---|---|
| 进入时施加 Buff | Character | Damageable | 是 | Any |
| 区域内维持 Buff | Character | Damageable | 是 | Any |
| 进入时触发剧情 | Character | None | 是 | Any |

Buff 的 Damageable 和全部模板的 Character/Alive 是最低要求，不能在 Editor 取消。`Movable`是第一项可选附加能力，放在`高级规则`中的`必须可移动`开关。关系只开放 `Any / SameTeam / Enemy / Neutral`；静态区域没有实体 owner，因此不开放 Self。

参照队伍候选直接读取当前场景角色文档中的正整数 `TeamId`，去重并升序排列。未知历史关系或队伍值保留并诊断，不自动改为 Any。

## 5. Runtime 数据流

```text
CombatPhysicsWorld 查询
  -> CombatEntityGameplayMap 映射
  -> GameplayComponentTargetFilterEvaluator
  -> accepted Subject 排序
  -> complete occupancy
  -> Trigger Entered / Stayed / Exited
  -> Buff 或 Story Action
```

筛选发生在 occupancy 提交前。对象由匹配变为不匹配时，下一次完整采样会正常产生 `Exited`，因此区域维持 Buff 继续复用既有 Remove 路由。

运行时检查显示命中、映射、通过、过滤数量，以及首个 typed rejection。filter 的全部有效字段进入 Sensor catalog hash 和 CharacterTest combined hash。

## 6. 版本与迁移

| 数据 | 版本 |
|---|---|
| CharacterTest gameplay trigger module | v2 |
| CharacterTest gameplay trigger payload | v2 |
| CharacterTest gameplay trigger scene cache | v2 |
| Combat Sensor catalog hash schema | v2 |
| Gameplay Trigger core authoring JSON | 保持 v1 |

读取 module v1 时，根据每条 Entry 的模板补齐默认 Subject Filter，再在内存中升级为 v2。仅打开编辑器不会因此变脏；第一次显式保存会写 v2，并在同一事务中重建旧 Scene cache。Runtime 不猜测旧 cache 的含义。

## 7. 手工测试

1. 打开 `CharacterTestScene` 和 Mx 关卡编辑器，选择一个带 Gameplay Trigger 的触发区域。
2. 对 Buff Trigger 在`作用对象`中选择`仅敌对`，参照选择`队伍 1`，然后点击`保存`。
3. 进入 Play Mode。Team 2 角色进入区域应触发，Team 1 不触发；`运行时检查`应显示被过滤数量。
4. 改成`仅同队`并重复，结果应反转；改成`阵营不限`后两队均可进入 occupancy。
5. 展开`高级规则`并启用`必须可移动`；当前正式角色均声明 Movable，行为应保持。用不带 Movable classification 的测试 candidate 验证 typed rejection。
6. 修改关系后执行一次 Undo/Redo，确认关系、参照队伍、未保存状态和滚动位置正确恢复。
7. 保存、关闭并重开编辑器，确认筛选值保持；旧 v1 文件首次打开不应无故显示未保存。

## 8. 扩展边界

新增第二种身份、能力或 source provider 前，必须先证明它有正式 Runtime consumer、映射和可持久化候选。P3.2 不提供 Tag/Status 手动 ID、布尔表达式、动作级二次选目标、WorldObject Subject、活动会话热重载或网络 schema 迁移。
