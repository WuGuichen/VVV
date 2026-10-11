# CD-M3：Engagement Anchor、CandidatePose 与路径代价

> Status: Guide
>
> 里程碑：CD-M3
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 3 节
>
> 父设计范围：[角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.6 节
>
> 本文是 CD-M3 总控页；里程碑状态只在总台账维护，T0～T6 状态只在本文维护。

## 1. 目标

把需要位移的攻击机会归约为稳定、可站立、可达且可预约的 CandidatePose，并复用 Pathfinding Foundation 计算有界路径代价。

## 2. 公共合同与前置门禁

实施或评审 CD-M3 时只需加载：

- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 的公共合同、状态和统一门禁。
- [CD-M2 执行文档](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) 的完成 Gate 与实际交付记录。
- [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.6 节。
- [Pathfinding Foundation](CHARACTER_TEST_RUNTIME_AI_PLANNER_PATHFINDING_FOUNDATION.md) 的 M2-M4 验收与被复用合同。
- [AI 接口](../Interfaces/AI.md)、[Combat 接口](../Interfaces/Combat.md) 和 [CharacterApplication 接口](../Interfaces/CharacterApplication.md) 的相关章节。

前置门禁：

- CD-M2 已达到 `Verified`。
- Pathfinding Foundation M2-M4 已有验收证据。
- 站立 capsule、静态 graph、有界 A*、Planner authority state 与角色包 Compiler 入口已核对。

若 Pathfinding Foundation M2-M4 尚未验收，应在总台账把 CD-M3 改为 `Blocked` 并记录缺口；不得在本路线重建另一套寻路。

公共合同：

- Anchor 是 Zone 正交的长期接敌参考，只接受 `ModelRoot` 局部毫米位置和毫度 yaw；World、bone-local、leaf-Hurtbox-following 输入 fail closed。
- 缺失作者文件走显式 `CompatibilityRootApproach`；大型/非人形正式验收必须使用作者 Anchor。
- CandidatePose 只描述候选位置/朝向及其 witness，不直接移动实体；站立/占用验证走 CombatPhysics，攻击验证复用 M1 evaluator 的 `CandidatePose` 模式。
- 路径成本只复用已验收 PF-M2～PF-M4，不建立第二张图或第二套 A*。
- 影响未来命令的 lock/reservation 进入 Planner authority、formal snapshot、full/owned/non-owned hash、rollback/replay；路径 sample 自身仍是当前帧派生数据。
- 逐帧 CandidatePose、reservation lookup 和 path cost 查询 warmed 后 0 B；snapshot capture/apply 保持 `LowFreqAlloc`。

## 3. 工作包状态、依赖与路由

```text
T0 -> T1 -> T2 -> T3 -> T4 --\
             \-> T5 ------------+-> T6
```

| 工作包 | 唯一状态 | 执行文档 | 可交付边界 | 前置 |
| --- | --- | --- | --- | --- |
| CD-M3-T0 | `Verified` | [合同与现状审计](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T0_CONTRACT_AUDIT.md) | Anchor/Zone/Compiler/Planner/Pathfinding/Network/Studio 字段级合同和测试矩阵 | CD-M2 + PF-M4 Verified |
| CD-M3-T1 | `Verified` | [Anchor Authoring、Compiler 与 Runtime Binding](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T1_ANCHOR_AUTHORING_COMPILER.md) | schema、严格校验、hash、保存、唯一 binding 投影与 Runtime 读取 | T0 Verified |
| CD-M3-T2 | `Verified` | [CandidatePose 与站立验证](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T2_CANDIDATE_POSE.md) | Anchor world projection、capsule/occupancy、M1 evaluator CandidatePose 复用 | T1 Verified |
| CD-M3-T3 | `Verified` | [路径代价与稳定 Anchor 选择](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T3_PATH_COST_SELECTION.md) | PF-M4 path cost、容量可用性、稳定选择和诊断 | T2 Verified |
| CD-M3-T4 | `Verified` | [预约权威、网络、回滚与重演](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T4_RESERVATION_AUTHORITY.md) | reservation/lock authority、snapshot/hash/rollback/replay | T3 Verified |
| CD-M3-T5 | `Implementation Complete` | [CharacterStudio Anchor 编辑闭环](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T5_CHARACTER_STUDIO.md) | UI、Undo/Redo、保存重开、刷新恢复、unknown schema；待直接浏览器证据 | T1 Verified |
| CD-M3-T6 | `In Progress` | [集成、Playable、性能与收口](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_T6_ACCEPTANCE.md) | 大型目标、多 AI、Game View、NoAlloc、接口回写、独立交付 | T4、T5 Verified |

T0 未冻结前不得修改生产 schema。T1 `Verified` 后，T5 可与 T2～T4 顺序链独立推进。只有 T6 `Verified` 后 CD-M3 才能进入 `Verified`。

## 4. 跨工作包验收矩阵

| 工作包 | 自动化重点 | Authoring / Unity | 权威 / 性能 |
| --- | --- | --- | --- |
| T0 | 审计复核 | 只读 | revision、owner、失效矩阵冻结 |
| T1 | round-trip、校验、hash、compile/binding | Core/CLI/editor-server + Unity compile | 缺失兼容；未知/非法不覆盖 |
| T2 | projection、capsule、occupancy、CandidatePose evaluator | focused EditMode/PlayMode | 逐帧 0 B；无第二 evaluator |
| T3 | path status/cost、stable tie、capacity | PF-M4 integration regression | 逐帧 0 B；sample 派生 |
| T4 | reservation conflict、schema/hash、rollback next-tick parity | network focused | authority state；snapshot LowFreqAlloc |
| T5 | UI transaction、Undo/Redo、save/reopen、reload recovery | CharacterStudio smoke/direct UI | 草稿与界面状态保护 |
| T6 | 合并回归、顺序扰动、phase/Zone/Anchor stale | Game View/PlayMode | full-chain 0 B；终审与边界 |

## 5. 完成 Gate

- T0～T6 全部 `Verified`，实际交付记录完整。
- Pathfinding Foundation M2～M4 的复用证据、Anchor schema/Compiler/Runtime、CharacterStudio、CandidatePose、路径代价和 reservation authority 全部闭合。
- 大型/非人形正式 fixture 使用 authored Anchor；多 AI 不拥挤到同一满容量 Anchor。
- Unity refresh/compile、Editor ready、Console 0 error、直接 Game View/PlayMode、CharacterStudio 实际状态和 warmed NoAlloc 全部通过。
- AI、Combat、CharacterApplication、Network 和 Pathfinding Foundation 按实际影响回写；生产/网络边界明确。

## 6. 里程碑实际记录

| 项目 | 当前记录 |
| --- | --- |
| 里程碑状态 | 见总台账 CD-M3；当前 `Verified` |
| 工作包进度 | 7/7 Verified |
| Pathfinding 前置证据 | PF-M2～PF-M4 均于 2026-08-18 Verified 并推送；PF-M4 提交 `b3bb23103`，同步点复验 Unity compile/Console 0、navigation 55/55、Planner path 9/9、affected regression 18/18 |
| 实际分支 / 提交 | `main`；实现 `9196cb2ce`、Studio 收口 `4d9864cde` 已推送并完成同步点复验 |
| Authoring / focused / Unity | Anchor focused passed；Dragon14 binding v3/revision 1/6 anchors；Unity compile/域重载/Console 0；受影响 EditMode 163/163；提交后 M3+diagnostics 22/22 |
| Playable / CharacterStudio | M1～M3 PlayMode 12/12；active Game View 两张；Studio smoke 与 Browser 标准/900px、CRUD/order、3D、draft/Undo/save/reopen、unknown read-only 全部通过 |
| Snapshot / replay / NoAlloc | Planner schema12/network29、rollback next-tick parity；projection/path/selection 与 reservation renewal warmed 0 B |
| 已知限制 / 阻断 | 无 CD-M3 验收阻断；无独立 Boss phase controller，v1 使用 `phase.default`；未验证多进程/公网网络 |

## 7. 工作包状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 |
| --- | --- | --- | --- | --- |
| 2026-08-18 | T0～T6 | 初始化 | `Planned` | 将 CD-M3 拆成七个可独立实施和验收的工作包；未绕过 PF-M4 阻断修改生产代码。 |
| 2026-08-18 | T0 | `Planned` | `Verified` | 完成 DamageZone/Compiler/M1 CandidatePose seam/PF-M4/Planner authority/network/Studio 只读审计，冻结 v1 字段、失效、reservation 与测试矩阵。 |
| 2026-08-18 | T1 | `Planned` | `In Progress` | PF-M4 已 Verified；开始唯一 Anchor Authoring→binding→Runtime 链。 |
| 2026-08-18 | T1 | `In Progress` | `Verified` | 冻结 schema 落到 Core/CLI/editor-server/Compiler/Runtime；Dragon14 正式 v3 fixture、独立 hash、缺失兼容和 fail-closed 路径均验证。 |
| 2026-08-18 | T2 | `Planned` | `In Progress` | 开始 Anchor world projection、CombatPhysics standing/occupancy 与 M1 CandidatePose evaluator 复用。 |
| 2026-08-20 | T2 | `In Progress` | `Verified` | ModelRoot projection、typed standing collision、stale witness、同一 M1 CandidatePose evaluator 与 warmed 0 B 闭合。 |
| 2026-08-20 | T3 | `Planned` | `Verified` | 复用 PF-M4 bounded A*，按 lock/priority/cost/tick/stable IDs 选择；Game View 显示 path cost/waypoint。 |
| 2026-08-20 | T4 | `Planned` | `Verified` | fixed-capacity canonical reservation、Planner schema12/network29、hash/rollback/next-tick parity、renewal 0 B 闭合。 |
| 2026-08-20 | T5 | `Planned` | `Implementation Complete` | Studio CRUD/technical tree/3D marker/draft/Undo/unknown lossless smoke 通过；直接浏览器受本机插件版本错配阻断。 |
| 2026-08-20 | T6 | `Planned` | `In Progress` | EditMode 163/163、PlayMode 12/12、Unity Console 0、active Dragon14 Game View 与接口回写完成；实现提交 `9196cb2ce` 已推送并完成提交后 Unity、22/22 EditMode、3/3 M3 PlayMode、Authoring/Studio smoke 复验；仅等待 T5 直接浏览器证据。 |
| 2026-08-20 | T5 | `Implementation Complete` | `Verified` | Browser 恢复后直接验证标准/900px、Dragon14 6 anchors、Inspector/3D、dirty refresh/Undo/save/reopen、copy/order 与 future-schema raw read-only；Console 0 error、正式资产无差异。 |
| 2026-08-20 | T6 | `In Progress` | `Verified` | P0/P1=0；实现与 Studio 收口提交均已推送；同步点 Unity compile/domain reload/Console 0、EditMode 22/22、PlayMode 3/3、Studio/Authoring passed，`HEAD...origin/main = 0 0`。 |

## 8. 兼容与非目标

- 缺少 Anchor 的旧角色包使用显式 `CompatibilityRootApproach`。
- 大型或非人形目标的 Playable 验收必须使用作者 Anchor。
- 长期 Anchor 不跟随高频尾部动画、叶级 Hurtbox 或 bone-local 参考系。
- 不在本路线重建 nav graph、A* 或另一套路径权威。
