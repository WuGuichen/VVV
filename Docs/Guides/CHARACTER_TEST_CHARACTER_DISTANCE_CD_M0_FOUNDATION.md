# CD-M0：快照、伤害几何与 Trace Plan 基础

> Status: Guide
>
> 里程碑：CD-M0
>
> 状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 父设计范围：[角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.1 至 7.3 节
>
> 本文不维护独立状态；任务是否成立以实际交付记录和总台账状态为准。

## 1. 目标

建立后续 Attack Opportunity 查询共用的确定性快照、伤害几何视图和 direct-hit Trace Plan，使预演与实际执行读取同一组形状、sample 和稳定 ID。本阶段不改变 Planner 行为。

## 2. 最小上下文与前置依赖

实施或评审 CD-M0 时只需加载：

- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 的公共首版合同、状态与统一门禁。
- [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.1 至 7.3 节。
- [Combat 接口](../Interfaces/Combat.md) 与 [CharacterAction 接口](../Interfaces/CharacterAction.md) 的命中、Hurtbox 和动作解析相关章节。
- 涉及 RuntimeFrame 阶段时读取 [Frame Sync 边界整改](CHARACTER_TEST_FRAME_SYNC_BOUNDARY_REMEDIATION.md)。

开工前确认：

- RuntimeFrame 阶段边界与当前 CombatPhysics revision 来源。
- 四类首版命中窗口在实际 CharacterAction 执行链路中的权威采样入口。
- Hurtbox 同步写入 CombatPhysics 的形状、collider ID、HitZone 与 receiver 映射。

## 3. 实现内容

- [x] 建立逻辑不可变的 `CharacterTestAttackEvaluationSnapshot`。
- [x] 快照绑定 `RuntimeFrame`、CombatPhysics `Revision`、source pose/facing 及 revision、target Hurtbox revision 和 resolved action revision。
- [x] Hurtbox 同步时同时生成 actor aggregate bounds 与稳定 collider 元数据索引。
- [x] aggregate bounds 与元数据只来自写入 CombatPhysics 的同一组确定性形状。
- [x] 抽取实际执行与预演共用的 direct-hit trace sampler。
- [x] sampler 覆盖 `WeaponTrace`、`ManualCapsule`、`CasterBody`、`BakedSocketCapsule`、完整 Active tick 与 root motion。
- [x] 为每个 resolved action 生成保守 `SourceActionBroadphaseBounds`。
- [x] 根距离只参与排序，不作为可能漏掉大型目标近端 Hurtbox 的硬排除。

## 4. 兼容与非目标

- 不改变 `target.distance`、`target.inAttackRange` 或任何 Planner 决策。
- 不实现 PreviewTrace 归约、PrimaryAttack guard、DamageZone、Anchor 或联合选择。
- 不实现精确 `AttackEnvelopeSeparation`。
- 不为投射物、光束、范围或脚本技能补写半径近似 evaluator。

## 5. 自动化测试

- [x] 实际执行与 Trace Plan 的 shape 类型、参数、sample 顺序和稳定 ID 完全一致。WeaponTrace、CasterBody 与 BakedSocketCapsule 均已有正式执行链回归；四类 sampler/preview 等价测试已通过。
- [x] 覆盖全部 Active tick、socket 轨迹与 root motion，不只采首尾帧。
- [x] 跨 Tick、跨 `BeginFrame` 阶段或 revision 混用返回 stale。
- [x] 大型目标根远但尾部 Hurtbox 近时，不被根距离或错误 broadphase 排除。
- [x] 打乱 collider 枚举顺序后，稳定元数据索引与结果不变。
- [x] warmed 后快照、Trace Plan 与 broadphase 热路径为 0 B GC allocation。

## 6. Playable 证据

CD-M0 不强制单独保存 Game View 证据，以实际执行和 Trace Plan 的自动化等价证据为主。开发期可视化只能作为辅助，不能替代等价测试。

## 7. 完成 Gate

- 实际执行与 Trace Plan 的形状、sample 顺序和 ID 一致。
- 跨阶段、跨版本混用稳定返回 stale。
- 大型目标根远尾近场景通过保守粗筛。
- focused tests、Unity refresh/compile、Editor ready、Console 0 error 和 warmed NoAlloc 全部通过。
- Combat、CharacterAction 接口文档按实际实现回写。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 里程碑状态 | 见[总台账 CD-M0](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) |
| 实际分支 / 提交 | `main` 工作区实现；尚未提交 |
| 受影响文件 | 新增 `CharacterTestAttackEvaluationSnapshot.cs`、`CharacterTestDamageGeometrySnapshot.cs`、`CharacterTestDirectHitTracePlanTypes.cs`、`CharacterTestDirectHitTracePlanSampler.cs` 及 M0 focused tests；修改 Hurtbox、Motion、RuntimeModule、BakedWeaponTraceProvider 与 CombatTrace 执行链；同步 Combat、CharacterAction、CharacterApplication 接口文档 |
| focused tests | 当前合并工作区：`CharacterTestCharacterDistanceM0Tests` 20 项与 DashStrike CasterBody 正式 Ability/Action 执行链 5 项合计 25/25 通过（Unity EditMode job `eb59b26b380545688b11ce3be88bd2c5`）；Dragon14 BakedSocketCapsule 三段自然攻击的 PlayMode 正式执行链 1/1 通过（job `1ded6e49264f43438ec67ec2788b4d46`）。既有 WeaponTrace、root motion、HitOnce、friendly filter、trace debug 与 damage/death 回归证据继续保留（jobs `bbbc845270ac419392c75a9fd6a9a494`、`18254a42fbe84d839d9b339452231756`、`50aa88b164894d069c5e8e59eae70339`） |
| Unity 验证 | 当前合并工作区通过：refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count 0（job `8c903d0cb3cb49c7ba792cea6b729477`） |
| Playable 辅助证据 | 本阶段非强制；当前无 |
| NoAlloc 证据 | `WarmedSnapshotGeometryAndBroadphaseQueries_DoNotAllocate` 与 `DirectHitTracePreviewPlan_WarmedBuildLookupAndBoundsDoNotAllocate` 均通过，测得 warmed 路径 0 B GC allocation |
| 接口文档回写 | 已同步 Combat、CharacterAction 与 CharacterApplication；AI/Planner 合同未改变，无 Current 能力可回写 |
| 已知限制 / 阻断 | 无 CD-M0 验收阻断。本阶段按设计不改变 Planner，生产 Attack Opportunity/PreviewTrace 消费者属于 CD-M1。实际执行计划中的 `resolvedActionRevision` 当前以当前动作实例/命中窗身份隔离，而不是动作目录内容 hash；预演查询合同仍显式接收并严格校验调用方提供的 resolved action revision。旧 `character_test` 生成动作 fixture 已迁移到正式 `codex_flow_test` Ability/Action 路径；BakedSocketCapsule fixture 仅修正三维挂点对齐和测试内确定性支撑，未改变生产命中代码。 |
