# LCC-M5-T3：Dragon14 动画附肢软接触验收

> Status: Guide
>
> 工作包：LCC-M5-T3（集成验收 / 唯一最终审查）
>
> 状态权威：[LCC-M5 第 3 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md)
>
> 执行文档状态：`Verified`（唯一终审P0=0/P1=0 open；两项历史P1经同一审查上下文关闭）
>
> 前置：T2 `Verified`。

## 1. 任务结果与非目标

用正式 Dragon14、真实 CharacterTestScene 和自动化证明：玩家接触头、尾中、尾尖时有稳定有限的滑开/推开和接触反馈，不会把附肢当空气，也不会形成动画硬墙、普通接触伤害或墙体穿透；M5 能力等级和限制按证据回写。

本任务不新增核心算法或 Authoring 字段，不扩到翼膜/全身/ragdoll，不补正式音频美术，不做全仓或公网网络声明。发现核心缺陷回派 T1/T2 原 owner，不在验收任务中扩张修复。

## 2. 前置与并行

T2 必须 `Verified`，T1/T2 生产 diff、focused 原始结果和未验证项已由主代理复核。T3 是唯一活动 M5 包；最终只读审查可在候选证据齐全后由不同上下文执行，但不得与仍在修复的生产 owner 并行判定。

## 3. 必读输入

- M5 Gate、T0 冻结表、T1/T2 实际记录；
- `Docs/QUALITY_GATE.md` 与 CharacterApplication/Combat/Network/Audio 接口；
- M1墙边/压力、M3 Severed、M4 pose/Hurtbox/性能基线；
- 正式 Dragon14 source/generated package 和 CharacterTestScene。

## 4. 写入所有权

- 新建 M5-only `CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests`，以及 M5 聚合 EditMode/network/rollback/replay/performance fixtures；现有回归 fixtures 只读。
- 允许在 `CharacterTestAuthorityPoseConsumerTests.cs` 仅迁移一条陈旧 Dragon14 `ContactPoint=0` 断言，使其验证当前 3 个 PresentationOnly config / 0 authority-pose attachment，并继续断言它们不成为 terrain authority 或硬阻挡；禁止改其它 M4 行为。
- `Docs/Evidence/LCC/LCC-M5-T3/AUTOMATION_2026-08-26.md`、`PLAYABLE_2026-08-26.md`、`PERFORMANCE_2026-08-26.md`、`FINAL_REVIEW_2026-08-26.md` 和正式 Game/Scene View 证据。
- M5 父文档实际记录、LCC 总台账状态候选、`Docs/CAPABILITIES.md` 和实际命中的 CharacterApplication/Combat/Network/Audio 接口章节。
- 仅为验收需要的 tests-only marker/driver；不得进入生产玩家路径。

生产源码、Authoring 和正式资产默认只读；修复由主代理回派原 owner。

## 5. 验收实施清单

1. 先跑 M5 focused、M1～M4 窄回归和 network/rollback，再进入 PlayMode。
2. 用正式 Dragon14/玩家复现 head、tail-mid、tail-tip 接触及墙边/高速/动作/Severed 场景。
3. 记录 8 Dragon 性能和 steady-state GC，不用孤立 query 代替全链。
4. 取得 Game View 与 Scene/debug 分层证据，明确 tests-only marker。
5. 回写实际接口/能力表和限制，再进行唯一最终审查。

## 6. 验收矩阵

| 类别 | 必须证明 |
| --- | --- |
| 玩家体验 | idle/移动/转身下分别接触 head、tail-mid、tail-tip；有界滑开/推开、可观察 contact phase，不能无反馈完整穿透 |
| 非硬墙 | 玩家可沿附肢切向滑开；高速摆动不产生无限推力、teleport或持续夹死 |
| 墙体安全 | 墙边、角落、窄通道中 static sweep 优先，允许有界软重叠但不穿墙 |
| 战斗隔离 | 普通接触 HP/HitResolve/Trace 不变；bite/tail sweep/spin 仍仅 authored window 伤害 |
| 拓扑 | Severed tail 后尾部软接触消失且不重放事件；head/核心 Presence 正常 |
| 兼容 | 缺失/v1 Body Presence、非 Dragon 角色、BlocksCharacters/CanBePushed/dead/disabled 语义保持 |
| 确定性 | full/owned/non-owned hash、rollback+1、replay/late join/source mismatch；若无新 schema，明确证明纯派生与既有 contact state 足够 |
| 性能 | 8 Dragon，3 animated sources/只，warm 300、measure 1800；记录 Hurtbox sync、query/build、pressure/apply、全链 p50/p95/max 与 GC，steady-state 0 B |
| Unity | 正式脚本门禁、Editor ready、Console 0；正式 Game View 和带分层标记的 Scene/debug 证据 |

冻结回归 filter：`CharacterTestLargeCharacterPresenceAuthorityTests`、`CharacterTestLargeCharacterPresenceContactTests`、`CharacterTestLargeCharacterPresenceMotionSafetyTests`、`CharacterTestHurtboxTopologyTests`、`CharacterTestAuthorityPoseConsumerTests`、`CharacterTestAuthorityPoseNetworkTests`、`CharacterTestReplayTests`；PlayMode 额外运行现有 `CharacterTestLargeCharacterPresencePlayModeTests` 与 `CharacterTestDragon14AuthorityTerrainPosePlayModeTests`。

## 7. 唯一最终审查

只在所有候选证据齐全后安排一次不同实现上下文的只读审查，范围限可复现 P0/P1：

- 是否误把 Hurtbox 加入硬阻挡/导航/伤害之外的新全局职责；
- 是否读取 Unity Transform/Physics 作为 authority；
- 是否有 rollback、Severed、墙边、无界速度、普通接触伤害或 source mismatch 缺陷；
- 是否把 tests-only marker、音频 ContactPoint 或源码设计冒充 Playable。

P0/P1 回派原 owner并复验；P2/P3只登记明确限制，不开循环评审。本任务不追加第二轮独立审查。

## 8. 回滚与完成回报

验收 fixtures、Evidence 和文档可按本任务边界恢复；生产缺陷必须回派 T1/T2，不在 T3 直接覆盖。报告实际修改文件、测试 filter/job/通过数、性能数值、网络/回滚结果、Game View 路径、Console 状态、唯一审查清单、能力等级和仍未支持的翼/全身范围。所有 Gate 满足前最多为候选完成，不得自行把父 M5 标为 `Verified`。

## 9. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 状态权威 | M5 工作包表 |
| 执行者 / 审查者 | `lcc_m5_t3_acceptance` / `lcc_m5_final_review` 唯一终审与P1关闭核对完成 |
| 实际修改文件 | 新建 M5-only PlayMode fixture；仅迁移 `CharacterTestAuthorityPoseConsumerTests` 一条获准的陈旧 ContactPoint 断言；新增 AUTOMATION / PLAYABLE 候选 Evidence；无生产源码、Authoring 或正式资产修改 |
| 自动化 / Unity / Game View | 历史 tail-mid 与 v7 consumer 失败均保留。P1 回修后 M5+M4 PlayMode job `49385ded7f134ef58881d523976b9ad9` 5/5 Passed：正式菜单关闭、真实 Dragon14/player views、head/tail-mid/tail-tip 下一 tick 位移 75/41/74 mm；moving/turning/high-speed/wall-corner/Severed 均有直接 M5 数值断言，HP/CombatHit trace 不变。Game View 已覆盖并用 `view_image(original)` 检查到 Dragon14、玩家和 tests-only markers；Scene 图仍只作上下文。冻结 M1/M3/M4 窄回归 job `4a23acc7da774d9096896eb7cb7a2255` 39/39 Passed；最终 Unity gate `3fbfe6ceae0d4f00a72f8d799cf0f18a` refresh/compile/domain reload/Editor ready/Console 0 全通过 |
| 性能 / 网络 | P1 final focused/network/performance job `58457a8e727b4a8d8acd069dc937108f` 13/13 Passed。正式全 RuntimeModule：8 Dragon14 + player、3 source/Dragon、warm 300 / measure 1800、0 B，whole Tick p50/p95/max 2125.7/3010.4/3964.0 us；Motion/AuthorityPose recorder Valid 但 EditMode LastValue=0，不伪造双Sync分段时长。synthetic 子阶段保留且0B，final aggregate p50/p95 602.6/899.6 us、max outlier 61.501 ms。正式 schema33 rollback+1 同 job 通过；6个非M5 EditMode陈旧断言和1个非M5 ActionId PlayMode断言仍为外部基线限制 |
| 独立审查 | 唯一终审与同上下文P1关闭核对完成：`FINAL_REVIEW_2026-08-26.md`，最终P0=0/P1=0 open、P2=1、P3=2；未开启第二轮宽审 |
| 结论 | `Verified`；两次生产阻断与终审两项证据缺口均由原owner/原始路径回修复验。保留6个非M5 EditMode陈旧断言、1个非M5 PlayMode ActionId陈旧断言、ProfilerRecorder分段值不可用、dense tests-only visual和production Audio/VFX/overlay限制 |

最终证据：[AUTOMATION_2026-08-26](../Evidence/LCC/LCC-M5-T3/AUTOMATION_2026-08-26.md)、[PLAYABLE_2026-08-26](../Evidence/LCC/LCC-M5-T3/PLAYABLE_2026-08-26.md)、[PERFORMANCE_2026-08-26](../Evidence/LCC/LCC-M5-T3/PERFORMANCE_2026-08-26.md)、[FINAL_REVIEW_2026-08-26](../Evidence/LCC/LCC-M5-T3/FINAL_REVIEW_2026-08-26.md)。两次生产失败和两项历史P1均完整保留并关闭；本任务不再追加审查轮次。
