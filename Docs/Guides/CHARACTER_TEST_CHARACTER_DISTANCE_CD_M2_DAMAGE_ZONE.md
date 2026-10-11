# CD-M2：DamageZone、稳定选择与 CharacterStudio 闭环

> Status: Guide
>
> 里程碑：CD-M2
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 3 节
>
> 父设计范围：[角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.6 节
>
> 本文是 CD-M2 总控页，不是一次性执行清单。里程碑状态只在总台账维护；T0～T6 的状态只在本文维护。任务页中的计划、类型名和勾选项不证明实现存在。

## 1. 目标与阶段边界

建立可作者化、可编译、可回放的 DamageZone 合同，并通过 Zone lock、迟滞和稳定决胜消除多 Hurtbox 大型目标上的选择抖动。

CD-M2 完成时应形成一条单一权威链：

```text
geometry/damage_zones.json
  -> Authoring Core / validation / hash
  -> existing geometry_binding.json
  -> Runtime binding / sealed Zone aggregate
  -> deterministic selection / Zone lock
  -> Planner authority / snapshot / hash / rollback / replay
  -> CharacterStudio + Playable evidence
```

本阶段不实现 Engagement Anchor、CandidatePose、路径成本、容量/预约或跨 target/action 的联合选择。

## 2. 公共合同与兼容边界

- `geometry/damage_zones.json` 是可选作者文件；Runtime 只消费编译进现有 `geometry_binding.json` 的结果，不建立第二份 Runtime 几何真源。
- 一个 HitZone 只能归入一个有效 DamageZone；具体字段、映射方向和诊断由 T0 冻结后实施。
- `Hittable`、`Aimable`、`Approachable` 保持正交，不能互相推出。
- 缺少作者文件时，现有 Hurtbox 仍可直接命中和瞄准，但不伪造长期可接近 Zone。
- 缺失文件不标脏、不自动写入默认文件；未知 schema 保留原 payload 并进入只读阻断，禁止覆盖保存。
- 已知 schema 中的重复归属、非法引用、禁用部位和其它合同错误 fail closed；T0 必须明确阻断粒度和 typed diagnostics。
- Runtime 按 Zone 聚合 sealed Hurtbox authority，并以显式 revision 使旧 query/lock 失效。
- 锁定 Zone 有效时保持；仅在当前 Zone 失效、机会等级提升，或同等级 earliest hit 至少改善 2 tick 时切换。
- 通过切换门槛后按冻结的作者优先级与稳定 ID 链决胜；容器、broadphase 或 collider 返回顺序不得参与权威选择。
- 所有影响未来命令的 lock 状态进入 Planner authority、formal snapshot、full/owned/non-owned hash、rollback 和 replay。
- warmed Zone 聚合、选择和逐帧 authority 消费路径必须有 0 B GC allocation 证据；formal network/rollback snapshot capture/apply 沿用现有 `LowFreqAlloc` 合同。

## 3. 工作包状态、依赖与路由

工作包使用与总台账相同的 `Planned -> In Progress -> Implementation Complete -> Verified` 状态；`Blocked` 只记录真实外部阻断及解除条件。工作包状态不是里程碑状态。

```text
T0 -> T1 -> T2 -> T3 -> T4 --\
             \-> T5 ------------+-> T6
```

| 工作包 | 唯一状态 | 执行文档 | 可交付边界 | 前置 |
| --- | --- | --- | --- | --- |
| CD-M2-T0 | `Verified` | [合同与现状审计](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md) | 只读链路审计、字段级合同、影响文件和测试矩阵 | CD-M1 Verified |
| CD-M2-T1 | `Verified` | [Authoring Core、服务与 Compiler](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T1_AUTHORING_COMPILER.md) | schema、校验、保存、hash、CLI/server 与唯一 binding 投影 | T0 Verified |
| CD-M2-T2 | `Verified` | [Runtime Binding、聚合与 Revision](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T2_RUNTIME_AGGREGATION.md) | Runtime binding、Zone aggregate、正交属性、revision/stale | T1 Verified |
| CD-M2-T3 | `Verified` | [稳定选择、迟滞与诊断](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T3_ZONE_SELECTION.md) | 无副作用选择器、2 tick 门槛、稳定决胜、Planner 接入 | T2 Verified |
| CD-M2-T4 | `Verified` | [权威状态、网络、回滚与重演](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T4_AUTHORITY_NETWORK.md) | lock authority、snapshot/hash、rollback/replay | T3 Verified |
| CD-M2-T5 | `Verified` | [CharacterStudio 编辑闭环](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T5_CHARACTER_STUDIO.md) | UI、Undo/Redo、保存重开、未知 schema、草稿/界面保护 | T1 Verified |
| CD-M2-T6 | `Verified` | [集成、Playable、性能与收口](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T6_ACCEPTANCE.md) | 正式全链 fixture、Playable、NoAlloc、接口回写和终审 | T4、T5 Verified |

状态更新规则：

- 第一个工作包进入 `In Progress` 时，同时把总台账 CD-M2 从 `Planned` 更新为 `In Progress` 并追加状态历史。
- 每个工作包的状态变化只更新本表，并在对应任务页填写实际分支、提交、文件、测试、环境、证据和限制。
- T0 的字段级合同未冻结前，T1 不得开工；T1 `Verified` 后，T5 可与 T2～T4 的顺序链独立推进。
- T0～T5 全部 `Verified` 且计划内实现已完成，但 T6 的直接证据/性能/文档门禁未闭合时，CD-M2 最多为 `Implementation Complete`。
- 只有 T6 `Verified` 后，才允许总台账 CD-M2 进入 `Verified`。
- 若某任务发现需要改变总台账第 2 节公共合同，停止实现，先同步父设计、总台账和 T0 冻结记录。

## 4. 最小上下文加载规则

开始 CD-M2 任一工作包时只加载：

1. [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 的公共合同、状态和统一门禁；
2. 本总控页；
3. 当前一个工作包文档；
4. 当前任务页明确点名的前置实际交付和接口章节。

默认不一次加载全部 T0～T6。跨任务合同审查才读取多个任务页。CD-M1 只读取完成 Gate、实际交付和当前任务点名的合同，不重载完整历史。

前置总门禁：

- CD-M1 已达到 `Verified`，Attack Opportunity 能返回稳定 Hurtbox/Zone 证据。
- T0 必须实际核对 CharacterStudio package state、Undo/Redo、保存、普通刷新/显式丢弃、schema、Compiler、`hitZoneId`、BodyPart、Hurtbox revision 和 `geometry_binding.json` 投影入口；本页不把“要求核对”写成“已经核对”。

## 5. 跨工作包验收矩阵

| 工作包 | 自动化重点 | Unity / 工具 | 独立证据 |
| --- | --- | --- | --- |
| T0 | 无生产测试；审计完整性复核 | 只读源码/文档 | 无 `TBD` 的冻结表、文件清单和测试矩阵 |
| T1 | round-trip、校验、未知 schema、hash、稳定编译 | Authoring Core/CLI/editor-server | 缺失文件不补写、非法/未知不覆盖 |
| T2 | binding、聚合、顺序扰动、revision/stale、NoAlloc | Unity focused + 完整脚本验证 | 正式 Compiler 输出被 Runtime 消费 |
| T3 | 初选、保持、2 tick 边界、失效、稳定决胜、NoAlloc | Unity focused + Planner integration | 诊断解释每次保持/切换 |
| T4 | authority、schema、full/owned/non-owned hash、rollback/replay | Unity/network focused | 分支回滚后下一 tick 命令 parity |
| T5 | UI 事务、Undo/Redo、保存、刷新、恢复、未知 schema | CharacterStudio automated/smoke | 标准/窄窗口实际界面证据 |
| T6 | 合并 focused 与受影响回归 | Unity 完整验证 + warmed NoAlloc | 直接 Game View/Play Mode 与 CharacterStudio 证据 |

## 6. CD-M2 完成 Gate

- T0～T6 全部 `Verified`，实际交付记录完整。
- Authoring Core/CLI、schema round-trip、真实迁移或明确的无迁移边界、CharacterStudio 实际编辑状态与 Runtime focused tests 全部通过。
- 缺失文件兼容、非法/未知 schema fail closed、Zone lock authority、snapshot/hash/rollback/replay 证据齐全。
- Playable 可观察当前锁定 Zone、机会等级、earliest tick 和切换原因；随机顺序与动画抖动不破坏稳定性。
- 保存直接 Game View/Play Mode framebuffer 和 CharacterStudio 编辑/刷新/重开证据；headless 行为测试不能替代这两项直接证据。
- Unity refresh/compile/domain reload、Editor ready、Console 0 error，以及逐帧 aggregate/selector warmed 0 B GC 全部通过；低频 snapshot capture/apply 单独按 `LowFreqAlloc` 登记。
- AI、Combat、CharacterApplication、Network 按实际实现回写；CharacterAction 若无实际变化，记录无需修改的依据。
- 独立终审 P0/P1 为 0；限制仍明确排除 Anchor、CandidatePose、路径和联合选择。

## 7. 里程碑实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 里程碑状态 | 见[总台账 CD-M2](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)；当前 `Verified` |
| 工作包进度 | 7/7 Verified |
| 实际分支 / 提交 | `main` 脏工作区；未提交 |
| 实际受影响文件 | 未记录 |
| Authoring / focused tests | 未执行 |
| Unity 验证 | 未执行 |
| Playable / CharacterStudio 证据 | Game View Commands 页的 DamageZone Selection 卡片已取得 framebuffer；CharacterStudio 隔离副本完成编辑、Undo、保存重开、刷新恢复、unknown schema 只读、显式丢弃与 1280×720 窄视口检查 |
| Snapshot / replay 证据 | 未执行 |
| NoAlloc 证据 | warmed aggregate 与 selector 0 B；formal snapshot capture/apply 按冻结后的 `LowFreqAlloc` 合同 |
| 接口文档回写 | 未执行 |
| 已知限制 / 阻断 | 无 CD-M2 验收阻断；Game View 同时保存无活动 lock 与正式 Dragon14 fixture 的 `InitialSelection` authority 字段画面；未执行多进程 transport loopback，不影响本地 formal snapshot/rollback/replay 合同 |

## 8. 工作包状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-18 | T0～T6 | 初始化 | `Planned` | 将原单页里程碑拆成七个可独立实施和验收的工作包；尚未执行代码、测试或工具验证 | 无已登记阻断 |
| 2026-08-18 | T0 | `Planned` | `Verified` | 当前 Authoring→Compiler→Runtime→Planner/Network→CharacterStudio 源码审计完成；冻结 schema、v1/v2 兼容、revision、选择、lock 和 diagnostics | 只读审计，不声明生产实现 |
| 2026-08-18 | T1 | `Planned` | `In Progress` | 开始实现 DamageZone Authoring Core、统一保存事务和唯一 geometry binding 投影 | T2～T6 尚未开工 |
| 2026-08-18 | T1 | `In Progress` | `Verified` | Core/CLI/editor-server/compiler 完成；focused Authoring、Dragon14 validate/compile 与未知 schema/缺失文件写保护通过 | 无 T1 阻断 |
| 2026-08-18 | T2 | `Planned` | `Verified` | binding v2、Runtime aggregate、revision/stale、legacy/fail-closed 与 warmed 0 B 用例完成；M2 EditMode 22/22 | 无 T2 阻断 |
| 2026-08-18 | T3 | `Planned` | `Verified` | 稳定决胜、2 tick 迟滞、跨帧 lock 与正式 Planner adapter 接入完成；M2 EditMode 22/22 | authority 持久化交给 T4 |
| 2026-08-18 | T4 | `Planned` | `Implementation Complete` | Planner schema 11、network schema 28、full/owned/non-owned hash、capture/apply 与 rollback hash parity 已实现并测试 | 缺下一 tick 真实 command replay/transport loopback 直接证据；低频 snapshot 不宣称 0 B |
| 2026-08-18 | T5 | `Planned` | `Implementation Complete` | CharacterStudio 编辑器、unknown schema 只读、事务/smoke 与实际刷新保草稿/Undo 验证完成 | 缺窄窗口、save/reopen、unknown/discard 的直接 UI 证据 |
| 2026-08-18 | T6 | `Planned` | `In Progress` | Dragon14 5 Zone/18 Hurtbox fixture、M2 focused/PlayMode、M0/M1 回归、Unity Console 0 与接口文档回写完成 | Game View overlay/framebuffer、独立终审及上述 T4/T5 门禁未闭合 |
| 2026-08-18 | T4 | `Implementation Complete` | `Verified` | schema 28 rollback 后双方继续一 tick，真实 drained command 逐条一致，Zone/Hurtbox/reason 与最终 state hash 一致；M2 PlayMode 3/3 | formal snapshot capture/apply 保持 `LowFreqAlloc`；未另做多进程 transport loopback |
| 2026-08-18 | T5 | `Implementation Complete` | `Verified` | 隔离 Dragon14 副本完成实际 save/reopen、browser reload recovery、Undo、unknown schema 只读和显式 discard；1280×720 无水平溢出且操作可达；smoke 通过 | 内置浏览器后端不能设置低于 1280 px 的实际宽度，1280×720 作为本次窄视口下界 |
| 2026-08-18 | T6 | `In Progress` | `Verified` | Game View overlay/framebuffer、M2 29/29 EditMode（含 overlay）、3/3 PlayMode、M0/M1 107/107 EditMode 与 6/6 PlayMode、双程序集构建、CharacterStudio smoke、Unity Console 0、性能合同和文档回写全部闭合 | 收口审查 P0/P1 为 0；工作区仍未提交，CD-M3 仍受 Pathfinding Foundation 前置阻断 |
