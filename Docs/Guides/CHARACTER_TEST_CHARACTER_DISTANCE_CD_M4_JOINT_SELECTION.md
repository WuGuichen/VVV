# CD-M4：有限 `(target, action, zone)` 联合选择

> Status: Guide
>
> 里程碑：CD-M4
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 3 节
>
> 父设计范围：[角色距离计算方案](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.7 节
>
> 本文是 CD-M4 总控页；里程碑状态只在总台账维护，T0～T5 状态只在本文维护。

## 1. 目标

将目标、动作和 DamageZone 作为有限联合候选进行确定性归约，解决先选最近目标再检查当前技能造成的循环依赖，同时用显式预算约束成本。

## 2. 公共合同与前置门禁

实施或评审 CD-M4 时只需加载：

- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 的公共合同、状态和统一门禁。
- [CD-M3 执行文档](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md) 的完成 Gate 与实际交付记录。
- [父设计](CHARACTER_TEST_CHARACTER_DISTANCE_CALCULATION.md) 第 7.7 节。
- [AI 接口](../Interfaces/AI.md)、[Combat 接口](../Interfaces/Combat.md)、[CharacterAction 接口](../Interfaces/CharacterAction.md) 和 [CharacterApplication 接口](../Interfaces/CharacterApplication.md) 的相关章节。

前置门禁：

- CD-M3 已达到 `Verified`。
- ready/resolvable action 集合、现有 action utility、Planner budget 和诊断入口已确认。
- 代表性校准场景和可复现的 broadphase 顺序扰动工具已准备。

公共合同：

- 选择单位是有限 `(target, resolved action, DamageZone)`；只考虑 ready/resolvable 且已有 M1 evaluator 的 action。
- 特殊技能保留显式 Compatibility 来源，不得伪装为 evaluator-backed 候选。
- actor aggregate broadphase 并集只作保守候选生成；明确相交候选优先，根距离仅软排序。
- Top-K 和 query budget 是工作量限制，不是预算外候选不可能命中的证明；截断必须输出 `CandidateSetTruncated`。
- 最终排序冻结为状态/生存 -> 当前可攻击 -> 只需转向 -> 合法 ApproachPose -> action utility -> path cost -> earliest tick -> Zone priority -> target/action/zone stable ID。
- 两层 cache 必须绑定全部 source/action/pose/snapshot/Zone/mode witness；缓存命中不能改变选择语义。
- warmed 候选生成、归约、cache 和诊断 0 B；影响未来命令的稳定选择 state 若存在，必须进入 authority/network。

## 3. 工作包状态、依赖与路由

```text
T0 -> T1 -> T2 -> T3 -> T4 -> T5
```

| 工作包 | 唯一状态 | 执行文档 | 可交付边界 | 前置 |
| --- | --- | --- | --- | --- |
| CD-M4-T0 | `Verified` | [合同、预算与现状审计](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T0_CONTRACT_BUDGET_AUDIT.md) | action/target/Zone 输入、排序、cache witness、预算与校准矩阵冻结 | CD-M3 Verified |
| CD-M4-T1 | `Verified` | [有限候选生成与保守粗筛](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T1_CANDIDATE_GENERATION.md) | ready action、aggregate broadphase、Top-K、typed truncation | T0 Verified |
| CD-M4-T2 | `Verified` | [联合归约、两层缓存与稳定选择](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T2_SELECTION_CACHE.md) | evaluator-backed joint selection、cache、稳定 tie-break | T1 Verified |
| CD-M4-T3 | `Verified` | [Planner 权威与可解释诊断](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T3_AUTHORITY_DIAGNOSTICS.md) | Planner 命令接入、每 tick 派生 selection、snapshot/rollback 边界、只读 overlay | T2 Verified |
| CD-M4-T4 | `Verified` | [预算校准与默认值冻结](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T4_BUDGET_CALIBRATION.md) | 代表场景峰值、零截断基线、2 倍余量和压力诊断 | T3 Verified |
| CD-M4-T5 | `Verified` | [Playable、性能与最终收口](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_T5_ACCEPTANCE.md) | 联合场景、Game View、NoAlloc、回归、接口回写与独立交付 | T4 Verified |

T0 未冻结排序和预算语义前不得写生产 selector。T4 是生产默认值进入正式 profile/config 的唯一门禁。只有 T5 `Verified` 后 CD-M4 才可进入 `Verified`。

## 4. 跨工作包验收矩阵

| 工作包 | 自动化重点 | Unity / 可玩 | 性能 / 权威 |
| --- | --- | --- | --- |
| T0 | 审计复核 | 只读 | cache/authority/预算边界冻结 |
| T1 | action/actor/zone 粗筛、Top-K、扰动 | focused EditMode | warmed candidate generation 0 B |
| T2 | full ordering、cache hit/miss/stale | focused + M1-M3 回归 | warmed reduction/cache 0 B |
| T3 | Planner command、diagnostics、snapshot if needed | Runtime AI Lab overlay | selection state authority 明确 |
| T4 | representative peak/truncation/load sweep | PlayMode calibration fixture | 2 倍余量、压力边界、数字可复现 |
| T5 | joint scenarios、全回归 | direct Game View/PlayMode | full-chain 0 B、终审与边界 |

## 5. 完成 Gate

- T0～T5 全部 `Verified`，实际记录完整。
- 短剑/长枪/远程兼容、顺序扰动、截断、cache invalidation 和预算校准证据齐全。
- 默认 Top-K/query budget 至少为代表性零截断峰值的 2 倍，并记录适用场景边界。
- 直接 Game View/PlayMode 可解释 target/action/zone、机会等级、path cost、earliest tick、截断和选择原因。
- Unity 完整验证、受影响回归、warmed NoAlloc、接口回写、独立终审和提交推送闭合。
- 明确 local-offline、formal snapshot 与未验证 production online authority 的边界。

## 6. 里程碑实际记录

| 项目 | 当前记录 |
| --- | --- |
| 里程碑状态 | 见总台账 CD-M4；当前 `Verified` |
| 工作包进度 | 6/6 Verified |
| 实际分支 / 提交 | `main`；T0 `7ea6fbf58`、T1 `6ac350dcb`、T2 `f7972c064`、T3 `d9761dd05`、T4 `535e8584d` 已推送；T5 收口见 Git 历史 |
| focused / 校准 / Unity | 最终精确矩阵 EditMode 199/199、PlayMode 14/14；T4 6 fixture 校准保持零截断；Unity compile/domain reload/Editor ready/Console 0 |
| Playable / Game View | CharacterTest 直接 Game View 已显示真实 AI 的 Commands 联合诊断卡；短剑/长枪、多目标 Zone/Anchor、兼容受阻、Turn、Approach 和 truncation 由 PlayMode 矩阵闭合 |
| authority / NoAlloc | joint result/cache/diagnostics 每 tick 派生且 rollback 清空，不新增 schema；只有最终胜者提交既有 Zone lock/reservation/command authority；候选、leaf、pose/path pressure typed truncation，完整 Planner submit/apply/action guard 与诊断 warmed 0 B |
| 接口回写 / 终审 | AI/Combat/CharacterAction/CharacterApplication/Network、CAPABILITIES 与总台账已回写；第二遍本地独立审查 P0/P1=0 |
| 已知限制 / 阻断 | 无 CD-M4 验收阻断；只声明 local-offline 单进程代表规模与 formal snapshot/loopback-oriented 合同，不声明 dedicated server、公网、多进程或大型 raid 容量 |

## 7. 工作包状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 |
| --- | --- | --- | --- | --- |
| 2026-08-18 | T0～T5 | 初始化 | `Planned` | 将 CD-M4 拆成六个可独立实施和验收的工作包；尚未修改生产 selector。 |
| 2026-08-20 | T0 | `Planned` | `Verified` | 冻结 canonical DTO/identity、14 级整数 comparator、8 类预算、两层 cache witness/lifecycle、Preview/Commit 边界、每 tick 派生 authority 与 6 fixture 校准矩阵；未改生产代码。 |
| 2026-08-20 | T1 | `Planned` | `Verified` | 交付预分配 coarse generator、actor/action/Zone canonicalization、explicit-over-root Top-K、compatibility seed 与 typed truncation；focused 5/5、Unity validate/Console 0、warmed 0 B。 |
| 2026-08-20 | T2 | `Planned` | `Verified` | 交付 14 级整数 reducer、两层有界 frame cache、完整 witness keys、typed overflow/rollback clear、M1～M3 Preview/Commit 与只读 reservation 预测；focused 47/47、受影响回归 132/132、Unity Console 0、warmed 0 B。宽泛 Formalization 扫描的 18 个既有 behavior/UI/profile 失败保留到最终审计。 |
| 2026-08-20 | T3 | `Planned` | `Verified` | Planner 每 tick 联合选择并提交 Current/Turn/Approach/fail-closed 命令；最终胜者才写既有 lock/reservation；Commands overlay 只读显示联合诊断；snapshot apply 清派生 cache/diagnostics。focused 39/39、Unity Console 0、双程序集 0 error、热链/诊断读取 0 B。 |
| 2026-08-20 | T4 | `Planned` | `Verified` | seed 42004 的 6 fixture、5 顺序扰动、120+600 tick 校准冻结峰值 32/16/4/256/256/256/256/256，production default 与 cache 逐项 2 倍；default/hard 压力 typed truncation、真实 Dragon14 CandidatePose budget、warmed 0 B、Unity Console 0 通过。 |
| 2026-08-20 | T5 | `Planned` | `Verified` | 修复无 joint leaf 时误封普通 Planner 目标感知的回归，同时保持 PrimaryAttack evaluator fail-closed；最终 EditMode 199/199、PlayMode 14/14、Game View 联合诊断、Unity Console 0、全链 NoAlloc、接口/能力表与 P0/P1=0 收口。全程序集 7183 项审计只见缺失 Piloto 索引和既有 Runtime AI Lab 卡数据失败。 |

## 8. 兼容与非目标

- 没有专用 evaluator 的特殊技能继续显式兼容。
- Top-K 是工作量预算，不是预算外候选不可命中的几何证明。
- 根距离不会重新成为硬射程或唯一 target 过滤条件。
- broadphase、容器或缓存填充顺序不得成为隐式决胜规则。
