# CD-M2-T6：DamageZone 集成、Playable、性能与里程碑收口

> Status: Guide
>
> 工作包：CD-M2-T6
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T4、CD-M2-T5 均为 `Verified`
>
> 本文只做真实全链集成、证据和文档收口；发现缺陷应回到拥有该合同的 T1～T5 修复，不在本任务暗增新能力。

## 1. 任务结果

以正式作者文件、Compiler、Runtime binding、Zone aggregate、稳定选择、authority snapshot 和 CharacterStudio 保存链构建一个可直接观察的多 Hurtbox 大型目标场景，并关闭 CD-M2 的 focused、Unity、Playable、Authoring、rollback/replay、NoAlloc 和接口文档门禁。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) 及 T0～T5 的实际交付记录；只加载发现问题所对应的任务页，不一次重读所有实现细节。
- [实施总台账](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md) 第 3、5、6 节。
- [AI](../Interfaces/AI.md)、[Combat](../Interfaces/Combat.md)、[CharacterApplication](../Interfaces/CharacterApplication.md)、[Network](../Interfaces/Network.md) 当前章节。
- `Docs/AGENT_GAME_CREATION_GUIDE.md`、`Docs/QUALITY_GATE.md`。
- 当前 Unity CharacterTest 场景、Runtime AI Lab/诊断展示入口和可复用测试 fixture。

开工前确认 T1～T5 的实际记录中没有未关闭 P0/P1、未验证 schema 或被跳过的 NoAlloc/rollback 门禁。

## 3. 集成 Fixture 与观察面

- [ ] 选择或建立确定性多 Hurtbox 大型目标 fixture，至少包含两个 DamageZone、每 Zone 多个 Hurtbox、不同作者优先级和可禁用部位。
- [ ] fixture 必须经正式 `damage_zones.json` → Compiler → `geometry_binding.json` → Runtime 路径，禁止测试直接注入私有 Zone 数据绕过 Authoring。
- [ ] 若修改正式角色包或场景，记录原因和资产影响；优先使用隔离测试 fixture，避免污染现有美术/恢复资产。
- [ ] 在 Game View/Play Mode 直接显示 target、当前锁定 Zone/Hurtbox、opportunity level、earliest tick、switch reason 和关键 revisions。
- [ ] 展示层只读取正式诊断，不缓存或反向控制 authority。

## 4. 必须演示的场景

- [ ] 初次选择按机会与稳定决胜落到预期 Zone。
- [ ] 目标轻微动画抖动时 lock 保持，不在两个 Zone 间频繁跳变。
- [ ] 同等级候选只改善 1 tick 时保持，改善 2 tick 时切换。
- [ ] 当前 Zone 部位失效后 revision 推进、旧 lock 失效并稳定选择新 Zone。
- [ ] 更高机会等级出现时按合同切换。
- [ ] rollback/replay 后下一 tick 的 Zone、switch reason 和 PrimaryAttack command 与原分支一致。
- [ ] 缺失可选作者文件的旧包继续旧 Hurtbox 命中/瞄准路径，且没有长期 Approachable Zone。
- [ ] 非法/未知 schema 在 Authoring/CharacterStudio 阻断，不能进入 Runtime。

## 5. 验证矩阵

### 5.1 Focused 与回归

T6 使用 T1～T5 实际记录中的精确入口和 filter，不重新发明一套聚合测试。计划的最小命名入口包括 `character-damage-zones`、`CharacterTestCharacterDistanceM2RuntimeTests`、`CharacterTestCharacterDistanceM2SelectionTests`、`CharacterTestCharacterDistanceM2AuthorityStateTests` 和最终 `CharacterTestCharacterDistanceM2PlayableTests`；若实现采用等价名称，必须在 T0 测试矩阵和各任务实际记录中统一更新。

- [ ] T1 Authoring Core/CLI/editor-server/Compiler focused suite。
- [ ] T2 Runtime binding/aggregation/revision/NoAlloc suite。
- [ ] T3 selection/hysteresis/order-perturbation/Planner suite。
- [ ] T4 authority/network/hash/rollback/replay suite。
- [ ] T5 CharacterStudio automated/smoke 与实际状态检查。
- [ ] CD-M0、CD-M1 受影响的 focused/Playable 回归，证明旧四类 evaluator、PrimaryAttack 和兼容动作没有回归。

### 5.2 Unity 与性能

- [ ] 执行完整 Unity refresh/compile/domain reload、等待 Editor ready、Console error count 0。
- [ ] 在 warmed 条件验证 CD-M2 新增的逐帧 Zone aggregate/selection 热路径为 0 B GC allocation；formal network/rollback snapshot capture/apply 沿用现有 `LowFreqAlloc` 合同并与热路径计量分开。
- [ ] 性能 fixture、预热次数、采样次数和测量区间写入实际记录；“代码看起来无分配”不算证据。

### 5.3 Playable 与 Authoring 证据

- [ ] 保存直接 Game View/Play Mode framebuffer，覆盖初选、保持、部位失效和稳定切换。
- [ ] 保存 CharacterStudio 实际编辑、Undo/Redo、保存、普通刷新保护和重开证据。
- [ ] headless PlayMode 可以补充行为证据，但没有 framebuffer 时不能关闭本阶段直接可观察证据 Gate。

## 6. 文档与交付收口

- [ ] 按实际实现回写 AI、Combat、CharacterApplication、Network；未实现能力不得写成 Current。
- [ ] 若 CharacterAction 支持矩阵或 damage eligibility 实际变化，同步 CharacterAction；无变化则记录“不需修改”及依据。
- [ ] 更新 CD-M2 总控的任务状态、实际提交、测试数字、证据、限制和状态历史。
- [ ] 只有全部 Gate 闭合后，才在实施总台账把 CD-M2 从 `Implementation Complete` 更新为 `Verified` 并追加状态历史。
- [ ] 记录实际分支/提交；若仍为未提交工作区，必须明确区分“验证完成”和“Git 未交付”。
- [ ] 重新审计 CD-M3 的两个前置：CD-M2 已 Verified 只解除一个条件，Pathfinding Foundation M2～M4 未 Verified 时 CD-M3 继续 `Blocked`。

## 7. 停止与回退规则

- 任一 T1～T5 合同失败时，把缺陷和证据回写对应任务，T6 保持 `In Progress`，不得用测试专用 fallback 绕过。
- 没有实际 CharacterStudio 或 Game View 证据时最多到 `Implementation Complete`。
- 逐帧 NoAlloc、rollback/replay、未知 schema 写保护或旧包兼容任一失败，都阻止 M2 `Verified`；已登记的低频 snapshot `LowFreqAlloc` 不冒充逐帧门禁。
- 不因 M2 收口顺手实现 Engagement Anchor、CandidatePose、路径成本或联合 target/action 选择。

## 8. 完成 Gate

- T0～T5 全部 `Verified`，本节验证矩阵全部通过。
- 直接 Playable 和 CharacterStudio 证据齐全，逐帧 aggregate/selector warmed 0 B GC、低频 snapshot `LowFreqAlloc`、rollback/replay 和接口文档同步闭合。
- 独立终审 P0/P1 为 0，限制和未覆盖范围明确。
- T6 实际交付记录完整，并按状态规则更新 CD-M2 和总台账。

## 9. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交 |
| 集成 fixture / 资产 | Dragon14 正式 `geometry/damage_zones.json`：5 Zone、12 HitZone、18 Hurtbox；三处 generated binding 同步为 v2 |
| 合并 focused / 回归 | M2 EditMode + overlay 29/29（job `f69e4cdde8a94e81a7de9532decafabe`）；M2 PlayMode 3/3（job `1354fdbb6e074c29a172d7eba712e941`）；M0/M1 EditMode 107/107（job `96b89edbd01e4a419701321578a86e0c`）；M1 PlayMode 6/6（job `6c6cd7a1140243adad89157cce5c5bd2`） |
| Unity 完整验证 | 两个目标程序集 build 0 error；refresh/compile/domain reload/editor ready 通过，Console 0 error（job `ca0f12353f394a0d891420d1742fae84`） |
| Playable framebuffer | Developer Debug Panel 的 Commands 页接入正式只读 DamageZone 诊断；1200×675 Game View 已保存无 lock/legacy 画面 `Docs/Evidence/CD-M2/CD-M2-DamageZone-Diagnostics-GameView-Running.png`，以及正式 Dragon14→Compiler→Runtime fixture 的 `zone.wings`、HitZone、Hurtbox、`CurrentPose / earliest tick 24`、`InitialSelection` 和 revisions 画面 `Docs/Evidence/CD-M2/CD-M2-DamageZone-Diagnostics-GameView-FormalLock-Fields.png` |
| CharacterStudio 证据 | 隔离 Dragon14 副本完成实际 edit/save/reopen、reload recovery、Undo、unknown `9.9` 只读、显式 discard；1280×720 无水平溢出；`CharacterStudio smoke ok` |
| Snapshot / rollback / replay | schema 28 capture/apply 后双方继续下一 tick，真实 drained command 逐条一致，下一 lock 与 state hash 一致；未另做多进程 transport loopback |
| NoAlloc | warmed Zone aggregate 与 selector 0 B；低频 authority snapshot capture/apply 按冻结后的 `LowFreqAlloc` 合同，不纳入逐帧计量窗口 |
| 接口文档回写 | `AI.md`、`Combat.md`、`CharacterApplication.md`、`Network.md` 已同步 schema 28 与 CD-M2 合同；`CharacterAction.md` 无接口变化 |
| 收口终审 | 按源码、测试、Game View、CharacterStudio、构建和文档状态做独立证据复核，P0/P1 为 0；未把设计项或 headless 测试冒充直接 UI 证据 |
| 已知限制 / 阻断 | 无 CD-M2 验收阻断；保持/2 tick 切换/失效语义由 focused 与 PlayMode 覆盖，直接 Game View 保存无 lock 和正式初选 authority 两种状态；工作区尚未提交 |
