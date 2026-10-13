# LCC-M1-T0：Runtime 接触链与预算审计

> Status: Guide
>
> 工作包：LCC-M1-T0（只读审计）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Production-Frozen`（审计结论已由主代理复核并回写生产任务）
>
> 工作包派发状态：`Verified`
>
> 派发、集成与回滚规则：[LCC 单任务执行文档模板](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_TASK_EXECUTION_TEMPLATE.md)
>
> 并行规则：除本文明确写明“可并行”外，不与同里程碑其它生产写入并行；跨里程碑只允许只读调查。
>
> 必读基线：总台账第 1～8 节、父里程碑冻结合同、本任务全部章节、路径上的 AGENTS.md、Docs/API_STANDARDS.md 和 Docs/QUALITY_GATE.md；再按本文输入/所有权加载专项接口与源码。
>
> 子代理派发合同：本文是唯一任务边界；只写 Owner 范围，保留工作区其它修改，不改父状态。完成回报逐项列出实际文件、合同/兼容/失败行为、测试原始结果、未执行验证、风险冲突、集成与回滚建议。实现者不得担任 schema、network、碰撞数学、拓扑或正式资产任务的最终独立审查者。
>
> 两级派发门禁：只有 T0 只读审计可在全路线 Audit-Frozen 后派发。T0 只写自身冻结表并结构化回报精确文件/partial owner、测试 filter、Docs/Evidence/LCC/<TASK-ID>/ 证据文件名和交接点；T1+ 必须等待主代理把这些结论回写到生产任务，并将本里程碑标记为 Production-Frozen。
>
> 前置：M0 `Verified`；不得与 M1 生产写入并行。

## 1. 任务结果与非目标

冻结 Presence sync、候选、约束、pressure、motor、网络派生缓存与诊断的真实 tick 顺序、稳定 identity、容量和性能预算，并给 T1～T5 无重叠 ownership。

非目标：不创建 Presence body、不改 contact/motion/snapshot、不调 Dragon14 参数、不宣称 M1 行为存在。

## 2. 审计输入

- [总台账第 1～8 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)、[M1 第 1～4 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)、M0 实际交付记录。
- `CharacterTestCharacterPhysicsRuntime.cs`、`CharacterTestCharacterMotionSystem.cs`、`CharacterTestCharacterContactResolver.cs`、component/entity world、CombatPhysics layer/body/compound、snapshot/hash/rollback/replay、debug overlay 和 motion tests。
- `Docs/Interfaces/Combat.md`、`Docs/Interfaces/Network.md`、CharacterTest game standard、API/quality gate。

## 3. 写入所有权

- 唯一写入：本文的顺序图、identity/capacity/budget 表、T1～T5 精确文件表和实际执行记录。
- 其余仓库只读；禁止修改生产源码、fixture、父状态或 M0 合同。

## 4. 必须冻结的结论

### 4.1 当前 authority tick 事实

源码入口只按本任务授权范围读取；下表是当前实现，不表示 Presence Runtime 已存在。

| 顺序 | 当前函数链与事实 |
| ---: | --- |
| 1 | `CharacterTestCharacterRuntimeModule.Tick` 依次完成 action、root motion、collision profile、mechanic motion，再调用 `CharacterTestCharacterMotionSystem.Tick`；motion 返回后才开始 perception、Combat trace、authority pose、animation、destruction 和 View sync。 |
| 2 | `CharacterTestCharacterMotionSystem.Tick` 清空当帧 contact scratch，执行 movable `BeginFrame/SyncAllCombatColliders`、`SyncAllCharacterCombatColliders`，再用 `StaticObstacleLayerMask + DynamicCharacterLayer + DynamicMovableObjectLayer` prepare motor candidate cache。 |
| 3 | 每个 `TickEntity` 在 motor 前读取当前 root XYZ 为 `previousX/Y/Z`；`BuildKinematicMotionInput` 把实体上一次完成 tick 写入的 `Physics.ContactVelocityX/YMillimetersPerSecond` 交给 motion solver。 |
| 4 | `CharacterKinematicPhysicsAdapter.Step` 通过当前 obstacle mask 做 static/dynamic sweep；结果经 `entity.SetTransform(...)` 提交，并立即由 `SyncCharacterCombatCollider` 更新 Hard Root body。实体按 world 列表逐个完成，因此后续实体可见前面实体本 tick 的 Hard Root 位置。 |
| 5 | 全部角色 motor 完成后执行 movable-object tick，再执行 `ResolveCharacterOverlaps`。它按根半径/高度做两轮兼容 overlap 解算，并在每轮发生位移后重同步全部 Hard Root。 |
| 6 | `ResolveCharacterContactPressure` 以 attempted velocity 做三轮 pressure 传播，随后 `ApplyCharacterContactState` 写入 contact velocity/displacement/count，最后 `CharacterTestCharacterHurtboxSystem.SyncAll` 同步本 tick 最终 Hurtbox。contact velocity 在**下一次** motor tick 被消费。 |
| 7 | formal snapshot/hash 不在上述 `Tick` 内调用；网络/replay 在完成 tick 的状态上调用 `CharacterTestNetworkSnapshotParticipant` capture/hash。诊断只能读取已完成状态，不得插入会改变 authority 顺序的 View/Unity 事实。 |

M1 生产顺序冻结为以下最小改动：

```text
motion tick 入口捕获本 tick previous root XYZ/Yaw/binding witness（预分配 transient scratch）
  -> 现有 Hard Root / movable sync + candidate-cache prepare
  -> 现有逐实体 kinematic motor/static sweep/transform commit/Hard Root sync
  -> 现有 movable tick
  -> 现有 Hard Root overlap 兼容解算（只允许根体积）
  -> 用最终 root pose 同步 Hard Root + Body Presence 派生几何
  -> 构建 root-contact + presence-contact 规范约束
  -> 按一个稳定 key 顺序执行现有 3 轮 pressure solve
  -> 写 CharacterTest contact state（供下一 tick motor 消费）
  -> 现有 Hurtbox SyncAll
  -> 后续 Combat trace / authority pose / snapshot-hash / diagnostics
```

Presence 不加入 motor candidate cache；因此不会在本 tick 越过静态 sweep 移动目标。它只产生有界 contact velocity，下一 tick 由现有 motor 再经过 static obstacle sweep。

### 4.2 当前直接位移路径及 M1 边界

| 路径 | 当前行为 | M1 冻结边界 |
| --- | --- | --- |
| `ResolveCharacterOverlaps` → `ApplyContactDisplacement` | 根半径相交时直接调用 `entity.SetTransform`；没有 static obstacle sweep；两轮后逐轮重同步 Hard Root。 | 为 Hard Root 核心重合的兼容行为保留，不在 M1 删除或改写。它的无 sweep 风险继续作为既有债务登记。 |
| `ResolveCharacterContactPressure` → `ApplyCharacterContactState` | 只累计速度并写 physics component，下一 tick motor 才移动。 | Presence 只接入此 pressure/motor 路径；禁止调用、复用或扩展 `ApplyContactDisplacement`。 |

Presence layer 不进入 `MotionObstacleLayerMask`，Presence volume 不进入根 overlap grid，代码审查和 focused test 必须证明两条绕过边界都成立。

### 4.3 Presence identity 与生命周期

审计入口已占用：Dynamic Character layer `8`、Dynamic Movable Object layer `9`、Character Hurtbox layer `10`；Hard Root body `20000 + entityId`、Hurtbox body `50000 + entityId`、movable body `900000 + numericId`；Hard Root collider `1`、movable collider `2`、Hurtbox collider 从 `1000` 开始。

| 项 | 生产候选冻结 |
| --- | --- |
| layer | `11`；只用于 Body Presence query。不得加入 `MotionObstacleLayerMask`、damage、navigation 或 projectile/environment blocker mask。 |
| entity identity | 与该角色 Hard Root 复用同一个 `CombatEntityId` 语义，即 `CharacterTestCharacterPhysicsRuntime.GetEntityId(entity)`；不得伪造第二个 gameplay entity。 |
| body identity | 候选 `PresenceBodyIdStart = 100000`，`bodyId = start + entityId`，独立于 Hard Root/Hurtbox/movable。T1 必须先以 checked arithmetic 和实际 entity/static-body 上界证明不碰撞；若不能证明，停止并请求主代理改冻结值。 |
| collider/leaf identity | `CombatPhysicsWorld` 以 `(bodyId, colliderId)` 为 collider key，因此不造全局 collider 号段。Presence body 内按 M0 binding 全部 volume 的 `volumeId` ordinal 稳定分配 `1..4`；disabled volume 仍占 ordinal；不得按启用集合顺序、字典顺序或 string hash 重新编号。compound leaf identity 使用同一值，并保留 M0 separation result 返回的 body/collider/leaf 全身份。T1 只需证明同一 Presence body 内唯一。 |
| lifecycle | Missing Presence 保持只有 Hard Root；invalid/future/stale binding fail closed。spawn/current binding 创建或 upsert；disable/despawn/destroy 移除 collider/body；root XYZ/Yaw、binding revision/hash、enabled state均未变时跳过 refit；restore 先清空 transient pose/candidate/constraint 与旧 Presence 映射，再从恢复后的 roster/root/binding 确定性重建。 |

当前审计文件没有声明各 body-id range 的完整结束值，这是 T1 必须先关闭的全局 identity 未知；collider id 只要求在同一 Presence body 内唯一，不得为它引入跨 body registry 或任意全局号段。

### 4.4 normal、pair ownership、去重与 pressure

- 查询方向固定为“目标角色 `ColliderPosture` capsule 作为 separation source query，Presence volume/compound leaf 作为 target shape”。按 M0 已交付合同，normal 是 `target shape -> source query`，因此这里恰为 `Presence source -> target character`；contact point 位于 Presence surface，penetration 非负。
- 只把 `Touching/Overlapping`（包含编译后 skin 的形状）变成约束；`Separated` 不产生约束；`UnsupportedShape/InvalidShape` 在任何 mutation 前拒绝整个 Presence 子步并记录 typed diagnostic。
- unordered character pair 由 `(minEntityId,maxEntityId)` 唯一拥有并只调度一次；其下的 directed constraint key 为 `(presenceEntityId,presenceBodyId,colliderId,leafId,targetEntityId)`。A 与 B 各自不同 Presence volume 是不同 directed constraint，不因 A↔B 规范化而丢失。
- broadphase/compound 重复返回相同 directed key 时只保留一个完全一致结果；同 key 几何/status 不一致视为 invariant failure，拒绝本 tick Presence 子步，不按集合先后选胜者。最终顺序先用 pair key，再用 directed key，最后用 M0 `CombatShapeSeparationResultComparer` 的完整字段决胜。
- 使用当前 `EffectiveMassGrams`、双方 `EffectivePushScaleMilli`、target `ColliderPosture.CanBePushed` 和 contact count 语义；不可推动目标仍产生 contact diagnostic/count，但不写目标速度。固定沿用当前 pressure solver 的 `3` 次迭代，不建立第二套 solver。
- root 平移和最短有向 Yaw 差只从同一 motion tick 的 previous/current authority pose 派生；rotation surface velocity 使用 contact offset，所有运算保持现有 fixed-point/量化单位。具体线速度、角速度、correction velocity clamp 尚无已落地常量，T3 必须从现有 movement/profile 上限取得或提交主代理决策，不得在实现中临时猜数。

### 4.5 容量与 overflow

M0 已冻结每角色最多 `4` 个 volume；M1 性能/验收包冻结 `N=8` 个 active character。由这两个现有上限直接推导，不另造预算数：

| 缓冲 | 上限/公式 | 8 角色冻结容量 |
| --- | ---: | ---: |
| active Presence volume / entity | `4` | `4` |
| active Presence leaf / world | `4N` | `32` |
| raw Presence candidate / target entity（排除自己的 4 个 leaf） | `4(N-1)` | `28` |
| unordered entity pair / frame | `N(N-1)/2` | `28` |
| directed volume constraint / frame | `4N(N-1)` | `224` |
| pressure iteration | 当前 `ContactPressureIterations` | `3` |

所有 buffer 在初始化/显式 warm-up 时按上述 envelope 预分配。超过 authoring volume 上限继续由 M0 typed validation fail closed；active character、candidate、pair 或 constraint 超限时不扩容、不稳定截断，也不保留部分 Presence 输出：在 mutation 前拒绝**整个 Presence 子步**，清零本 tick Presence contribution、记录 typed overflow diagnostic，仍允许现有 Hard Root motor/overlap/root pressure 完成。若 M1 要支持 `N>8`，必须先由主代理提升 envelope、容量和验收矩阵。

### 4.6 previous/current pose 与 formal state 结论

`TickEntity` 已在本 tick motor 前取得 previous XYZ；M1 只需把 XYZ/Yaw/binding witness 提前收集到预分配 scratch，使 post-motor Presence sync/constraint 能访问同 tick previous/current。该 scratch 在 tick 结束即失效，spawn/binding switch/restore 缺少合法 angular baseline 时 rotation contribution 为 `0`；平移继续来自本 tick motor 的确定性位移事实。

因此 T0 结论是：**不新增 formal authority/network state**。若实现需要 hysteresis、last normal、跨 tick previous pose 或任何影响未来命令的历史，立即停止；当前 schema 为 `29`，不得在未获主代理批准时私自占用下一 schema。

### 4.7 snapshot/hash/restore 事实

- 当前 `CharacterTestNetworkCharacterSnapshot.FromEntity` capture contact velocity、contact displacement 和 contact count；`CreatePhysicsComponent` 把三者恢复到 physics component。
- full、owned、non-owned hash 都经 `AddCharacterEntityHash`；它只 hash `Physics.ContactVelocityX/YMillimetersPerSecond`。源码明确把 displacement/count 视为逐帧派生观测值而不 hash，因为只有 contact velocity 会影响下一 motion tick。
- `ApplySnapshot/ApplyRollbackDocument` 最终进入 `ApplyDocument -> CharacterTestCharacterRuntimeModule.ApplyNetworkCharacters`；成功后当前 module 会重同步 authority colliders/view。schema `29` 没有 Presence 几何、constraint、diagnostic 或 previous/current pose 字段。
- M1 继续复用上述 contact velocity capture/hash/apply；Presence compound、候选、约束、same-tick pose scratch 和 diagnostics 保持纯派生。restore/late join/rollback 必须移除 stale Presence body，清空 transient scratch/buffer，并在下一次 query 前从恢复后的 root/binding 重建；rollback 后 `+1 tick` 的 pose、contact velocity、command 和 full/owned/non-owned hash 必须与未分叉基线一致。
- scoped snapshot participant 中没有可见的 Presence binding revision/content hash 字段；T4 必须证明现有 session/package source identity 已覆盖 binding v4。若没有覆盖，停止并补正式 source-identity 合同，不能把配置差异藏在派生 cache。

## 5. 审计、验证与证据

- [x] 以点名源码函数冻结当前调用链；没有把父里程碑计划顺序当作实现事实。
- [x] 登记 root-radius 直接位移，并冻结 Hard Root 保留 / Presence 绕过边界。
- [x] 冻结 Presence mask、identity 候选、dirty/refit、M0 comparer、去重与 overflow 政策；body range 上界保留为 T1 stop-check。
- [x] 登记 contact velocity capture/apply 和 full/owned/non-owned hash；派生 restore 规则已冻结，source identity 覆盖留给 T4 实证。
- [x] 冻结 T1～T5 文件/partial owner 与测试 filter 候选；父状态仍由主代理维护。
- [x] 主代理复核调用链、测试名、容量公式、identity stop-check，并确认本文本地链接与 `git diff --check`；本任务按边界不运行 Unity、测试或 profiler。

### 5.1 T1～T5 文件与 partial owner

同一文件可在 DAG 后续任务再次进入，但每项只拥有下列 partial；不得在同一批次并行写共享文件。

| 工作包 | 唯一 owner 文件 / partial | 明确非 owner / 交接 |
| --- | --- | --- |
| T1 Presence Runtime | `CharacterTestCharacterPhysicsRuntime.cs`：layer/mask/body checked identity、body-local collider ordinal 与 sync-state；新增 `CharacterTestCharacterBodyPresenceSystem.cs`：binding projection、compound upsert/remove、dirty/refit/restore rebuild；`CharacterTestCharacterMotionSystem.cs`：字段、`SetPhysicsRuntime/Clear` 生命周期和“final root 后 sync”调用点；新增 `CharacterTestLargeCharacterPresenceRuntimeTests.cs`。 | 不改 contact solver 数学、contact velocity、snapshot schema、direct displacement。先关闭全局 body range collision stop-check，并证明 body-local collider `1..4` 唯一，再交 T2。 |
| T2 Contact Constraints | `CharacterTestCharacterContactResolver.cs`：Presence candidate buffer、规范 key/去重、root+Presence stable constraint order、现有三轮 pressure solver 接入、overflow diagnostic；`CharacterTestCharacterMotionSystem.cs`：仅替换 post-motor contact 调用序列；新增 `CharacterTestLargeCharacterPresenceContactTests.cs`。 | 不改 identity 数值、surface-velocity pose capture/clamp、snapshot、overlay；`ApplyContactDisplacement` 只读并保持 root-only。 |
| T3 Surface Velocity / Safety | `CharacterTestCharacterMotionSystem.cs`：same-tick previous/current XYZ/Yaw scratch capture/clear；`CharacterTestCharacterContactResolver.cs`：surface velocity、mass/pushability、clamp 与 wall-limited 输出 partial；新增 `CharacterTestLargeCharacterPresenceMotionSafetyTests.cs`。 | 不改 Presence topology/identity 或 snapshot schema；若需要跨 tick pose/history，立即停机交主代理。 |
| T4 Authority / Diagnostics | `CharacterTestNetworkSnapshot.cs`：只审查/必要时修改 contact velocity `FromEntity/CreatePhysicsComponent/AddCharacterEntityHash/ApplyDocument` partial；`CharacterTestCharacterBodyPresenceSystem.cs` 与 resolver：只加 bounded numeric diagnostics snapshot；`CharacterTestNetworkLoopbackTests.cs`、`CharacterTestReplayTests.cs`、`CharacterTestProfilerGcRemediationTests.cs` 及新增 `CharacterTestLargeCharacterPresenceAuthorityTests.cs`。 | 默认 schema 不变；不得持久化派生 compound/constraints/diagnostics。现有 Game View overlay adapter 的精确文件不在本 T0 授权源码集，主代理必须在 T4 派发前补唯一 owner。 |
| T5 Acceptance | 新增 `CharacterTestLargeCharacterPresencePlayModeTests.cs`；只扩展 `CharacterTestDragon14NaturalAttackPlayModeTests.cs` 与 `CharacterTestLargeCharacterPresenceDebugTests.cs` 的 M1 回归；按实际实现回写 `Docs/Interfaces/Combat.md`、必要时 `Docs/Interfaces/Network.md` 和能力表。 | 不调 Dragon14 Presence 参数、不重写 T1～T4 生产源码；父 M1 状态、最终证据接受与回滚由主代理拥有。 |

### 5.2 focused filter 与证据文件名

T0 只用 `rg` 确认下列现有测试名；没有执行。生产任务使用 exact class/method filter，不用整仓失败替代定向证据：

- M0 geometry 回归：`CombatShapeSeparationTests.BulkQuery_IsStableAcrossInsertionAndDuplicateLeafOrder`、`BulkQuery_Warmed4096IterationsAllocateZeroBytes_AndLegacyQueryStillHits`，以及该类四种 capsule separation shape tests。
- motor/static safety：`CombatKinematicMotorTests.ShapeTargetInitialOverlap_MovingAwayDoesNotBlock`、`WorldSync_MovedBodyPositionIsUsedBySubsequentPhysicsQuery`、`Step_CandidateCacheUpdatesDynamicBodyAfterWorldSync`、`Step_CachedCandidatePathDoesNotAllocateAfterWarmup`。
- M0 Presence/动作非回归：`CharacterTestLargeCharacterPresenceDebugTests` 两项；`CharacterTestDragon14NaturalAttackPlayModeTests.BakedSocketCapsulesDamageTrainingTargetWithPerEventDeduplication`。
- snapshot/hash/replay：`CharacterTestNetworkLoopbackTests.LateJoinSnapshot_AppliesCharacterPhysicsProfileAndHash`、`NetworkSnapshotV1_WritesIntegerAuthorityFieldsWithoutLegacyFloats`、`SnapshotHash_OwnedStateIgnoresPeerLocalControlPresentation`、`SnapshotSchemaMismatch_ReturnsStructuredFailure`；`CharacterTestReplayTests.ReplayPlayback_ReplaysMoveThenStopWithMatchingHashes`、`ReplayBootstrap_FirstRecordedFrameSnapshotMatchesSource`、`LockstepSeed_DualHeadlessSlicesProduceMatchingIntegerSnapshotsAndHashes`。
- allocation 回归：`CharacterTestProfilerGcRemediationTests.CharacterMotion_PhysicsMotorCacheHandlesMultiplePosturesWithoutSteadyStateAllocation`、`...RootMotionMaskSwitch...`、`...PostureSwitch...`。
- T1～T5 新增 exact filters 使用上表类名，覆盖 8×4/18 Hurtbox、平移、原地 Yaw、相向、同中心、墙夹、死亡/不可推动、顺序扰动、overflow、spawn/restore 和 rollback+1 tick。

证据目录统一为 `Docs/Evidence/LCC/LCC-M1-T0/`；后续任务登记 `LCC-M1-T1-presence-sync-editmode.xml`、`LCC-M1-T2-contact-constraints-editmode.xml`、`LCC-M1-T3-surface-velocity-safety-editmode.xml`、`LCC-M1-T4-network-replay-hash-editmode.xml`、`LCC-M1-T4-presence-sync-contact-performance.json`、`LCC-M1-T5-000003-playmode.xml` 和 Game View 截图。T0 未创建这些证据。

### 5.3 性能与 0 B 测量合同

- fixture 固定为 8 active characters、每角色最多 4 Presence、Dragon14 18 Hurtbox 路径；完整测量必须包含 final-root Presence sync、compound dirty/refit、capsule separation candidate、dedup、三轮 solve、contact-state write 和现有 Hurtbox `SyncAll`，不能只测 M0 separation micro-kernel。
- setup 阶段预建 package/world/roster，预分配上表所有 buffer，并运行至 body/leaf 数、buffer capacity、world topology 和 unchanged-skip 计数稳定；debug string/JSON/截图在测量窗口外生成。
- 0 B 沿用 M0 现有 warmed `4096` 次惯例：在同一线程用 `GC.GetAllocatedBytesForCurrentThread` 包围完整 contact tick loop，记录 before/after/delta；要求 delta=`0 B`。另保留单 query 4096 次 M0 回归，但它不能替代完整路径。
- timing 用同一机器/Editor 状态的无日志 fixture 采集每 tick elapsed 和 Presence sync/contact scoped marker，原始样本计算 median/P95/max，并同时记录 active volume、raw candidate、dedup constraint、iteration、refit/skipped-unchanged、overflow count。T0 因禁止运行 profiler，**不填写数值 P95 budget**；T4 产出基线，主代理在 T5 派发前接受数值门槛，禁止用猜测数字写 Gate。

## 6. 停止条件与派发

现有 motor 已有 contact velocity 合法入口，M0 normal `target -> source query` 在冻结查询方向下可直接作为 `Presence -> character` 推力方向，不需要改变根距离合同。若出现以下任一情况则停止并提交主代理：全局 body-id range 无法证明不冲突，或同一 Presence body 的 collider ordinal 不唯一；Presence 必须进入 motion/damage/navigation/projectile mask；需要直接 transform displacement；需要跨 tick previous pose/hysteresis/history；binding source identity 未覆盖 v4；M0 bulk API 无法在冻结容量内保持 NoAlloc；或必须修改根距离、DamageZone、Engagement Anchor / Attack Opportunity 语义。

实现期仍须关闭的显式未知：body/static/entity 数值上界、surface/correction clamp 的权威来源、现有 package/session identity 对 binding v4 的实证，以及完整路径实测 P95 数值。T4 overlay owner 由主代理冻结为现有 `CharacterTestLargeCharacterPresenceDebug.cs`、`CharacterTestDebugPresentationController.cs` 与 `CharacterTestDeveloperDebugSnapshotBuilder.cs` 的 Presence 专属 partial。这些未知不得被描述为已实现能力；对应生产任务命中停止条件时必须回报。

## 7. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | 未执行 |
| 实际修改文件 | 仅本文；无生产源码、fixture、父状态或测试文件修改 |
| 测试与原始证据 | 按任务边界未执行；仅以 `rg` 登记现有 exact test 名，未生成 Evidence 文件 |
| 集成 / 回滚结果 | 未执行 |
| 状态历史 | 2026-08-24：M0 Verified 后 `Planned` → `In Progress` → `Verified`；只读审计与主代理复核完成，无生产行为 |
| 执行者 / 审查者 | 最小 Runtime 审计由干净上下文子代理完成；主代理复核并补严 body/collider range stop-check、overlay owner 与生产任务边界 |
| tick / identity / capacity | 已形成 T0 冻结候选：当前链、root-only direct displacement、layer 11、identity 候选、8×4 推导容量、整 Presence 子步拒绝；body range 仍有 T1 stop-check |
| ownership / 测试矩阵 | 已登记 T1～T5 文件/partial owner、exact focused filters、Evidence 文件名与完整性能/0 B 测量合同 |
| 限制 | 只读执行包；未运行测试/Unity/profiler，未证明 M1 Runtime 行为；这里只将审计任务标为 `Verified`，不代表任何生产能力 |
