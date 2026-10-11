# CharacterTest Frame Sync Boundary Remediation

> Status: Design
>
> Date: 2026-07-11
>
> Implementation: Active Remediation Baseline

## Runtime AI Lab 手动状态工具基线

Runtime AI Lab 的默认历史定位采用固定初始基线加纯帧重演：

```text
immutable initial warmed-session baseline
  -> restore in the current GameSlice
  -> inject retained external authority commands by RuntimeFrame
  -> budgeted authority Tick + local animation-state advance to target frame
  -> commit presentation once at the target frame
  -> compare target authority hash
```

- Start 在首个有效 Agent 帧捕获唯一初始 authority/presentation 基线；该基线是固定重演起点，不是中途状态同步。
- `-1 / -10 / Jump / A-B Loop` 在当前预热 session 内恢复该初始基线，不销毁 `GameSlice`、角色 View、Playables、相机或 HUD，也不自动应用中途 authority checkpoint。
- Timeline 可额外保留逐帧的小型 animation input 作为 Presentation Capture，驱动常驻 Playables 精确回看；它不属于 authority、不参与 Frame Sync PASS/FAIL，也不进入正式同步合同。
- 完整 session 销毁重建只由显式 `冷启动校验` 触发；Warm Replay 失败时不得静默降级为冷重建。
- timeline 常驻数据只包含 `RuntimeFrame`、外部命令、authority hash 和最小调试摘要。
- `状态采集` 默认关闭；显式开启后，完整 Authority rollback document 与 Presentation checkpoint 才保存到独立的 session-only 测试存储。
- `校验状态` 以 `RuntimeFrame + authority hash` 决定 `Frame Sync: PASS/FAIL`；动画、骨骼、IK、ragdoll、VFX 和 Overlay 只产生独立的 `Presentation` 报告。
- `同步记录状态` 仅在暂停/Review 且目标帧已采集时可用。它不会修改 timeline 或创建分支，也不属于正常帧同步路径。
- 音频一次性播放不恢复；重演和手动恢复期间只做静音与 Presentation Diagnostics。
- 中间重演帧推进 authority simulation，并同步推进本地 Playables/CrossFade/locomotion/procedural-bone 状态机；不推进 Camera、Gameplay UI、Overlay、音频或其它场景表现。抵达目标帧后才强制同步并零时间刷新一次完整表现。
- 动画状态推进是本地重建，不进入 authority hash、network snapshot 或帧同步命令。完全跳过它只能定位单 clip，无法恢复有历史依赖的 CrossFade 和层权重。
- 超过即时阈值的重演按 Unity Update 分批执行，单次预算约 `4 ms`、最多 `512` 帧；重演期间锁定 Step、Jump、Loop 和 Edit Gate，`Reset` 可取消。
- HUD 状态显示 `processed / total`，完成后报告纯数据重演总 CPU 时间和平均 `us/RuntimeFrame`。这些指标只属于 Lab 低频诊断。

该边界保留状态恢复能力用于测试与定位，同时保证默认验收实际验证“同初始状态 + 同命令 + 同固定 Tick”的确定性。

>
> 本文是 CharacterTest 帧同步边界整改的设计与实施事实源。2026-07-11 已完成第一轮 authority 边界收敛；仍未逐字段证明的状态继续按本文审计，禁止通过增加表现字段解决 Lab 视觉差异。

## 当前实施基线

第一轮整改已经落地：

- CharacterTest authority snapshot 升级为 schema `20`，不再保存或 hash `VfxRuntime`、相机模式、Unity 动画输入和 Runtime AI Planner `LastCommand` 诊断字段。
- CharacterTest 本地 rollback 使用独立 `CharacterTestAuthorityRollbackCheckpoint` 载体；它复用 authority document，但不再通过 network `RuntimeCustomState` / JSON wire 保存 Lab checkpoint。
- Runtime AI Lab timeline 默认只记录 `RuntimeFrame`、外部命令、authority hash 和最小 Runtime AI Planner debug frame；中途 authority/presentation checkpoint 只进入显式开启的独立测试采集存储。
- Runtime AI Lab Seek 的中间帧使用 authority simulation + hidden animation-state step；长距离 Seek 使用时间预算分批重演，目标帧只提交一次完整 presentation。
- 正式回放验收使用 `CharacterTestRuntimeAiLabAuthorityProbe`，只比较 `RuntimeFrame + authority hash`。Runtime 的显式状态采集使用最小 `CharacterTestRuntimeAiLabPresentationDiagnosticProbe`；旧综合 Probe 已迁入 `MxFramework.Tests`，只做 test-only 字段级诊断，不再提供 authority checkpoint 通过模式。
- A-B Loop 已重写为暂停状态下的只读 authority replay：A-B 之间按历史命令重演，不写 timeline、不进入 Live、不截断未来记录；Unity 表现只做 canonical rebuild 和独立诊断。

仍需继续逐字段审计 gameplay subsystem snapshot，但后续审计必须证明“缺少该字段会让 checkpoint 后的下一帧 authority 发散”，不能以 HUD、动画、音频或 VFX 不一致作为保留理由。

## 结论

CharacterTest 正式玩家联机继续采用当前 `BootstrapOnly` 策略：

- 正常 gameplay 同步带 `RuntimeFrame` 的权威命令，并在固定 Tick、稳定系统顺序和确定性配置下独立计算 gameplay authority state。
- 正常 gameplay 不周期性发送 full snapshot 或 digest snapshot 作为状态同步手段。
- full authority snapshot 只用于初始接入、角色 assignment、late join、显式 snapshot request / resync，以及本地 rollback checkpoint 的恢复基础。
- state hash 用于检查 gameplay authority 是否一致，不用于同步 Unity 表现状态。

Runtime AI Lab 的 seek、review、A-B Loop 和视觉回看是本地开发工具，不是网络帧同步协议。动画、骨骼、IK、音频、VFX、Overlay 和 HUD 的记录属于 Presentation Diagnostics 或可选 Visual Capture，不能继续被描述为正式帧同步数据。

## 整改原因

当前实现已经把多个不同目标混入同一条“帧同步”链路：

1. 确定性固定帧模拟。
2. 网络 bootstrap / resync snapshot。
3. 本地 rollback checkpoint。
4. Runtime AI Lab 历史回放。
5. Unity 表现层精确恢复。
6. FrameSyncProbe 的全系统诊断采样。

整改前的历史基线为：

- `CharacterTestNetworkSnapshotParticipant` 已演进到 schema `19`。
- `CharacterTestRuntimeAiLabFrameSyncProbe` 当时已约 7000 行，并采样 gameplay authority、derived state、音频、VFX、Overlay、runtime events 和多种技能诊断；R5 已将其迁入测试程序集并按领域拆分。
- Runtime AI Lab retained timeline 会保存逐帧动画表现检查点。
- 当时未提交的 A-B Loop patch 依赖每帧精确动画表现检查点；第一轮整改已删除该依赖并改为 authority-only 只读回放。

这些实现不应直接批量回滚。整改必须先判断数据职责，再决定保留、拆分、重建或删除，避免误删真正影响后续确定性计算的 authority state。

## 术语和边界

### Frame Sync / 帧同步

帧同步只负责让参与者在同一个 `RuntimeFrame` 使用相同的确定性输入：

- 外部权威命令及其目标帧。
- 同帧命令稳定顺序。
- 固定 simulation delta 和 Tick 顺序。
- 确定性配置、稳定 ID 和必要随机状态。

角色位置、血量或动作结果是上述输入执行后的 authority 结果，不是正常帧同步中每帧传输的数据。

### Authority Snapshot / 权威快照

Authority Snapshot 是低频状态恢复工具，不是正常 gameplay 的持续同步主体。它只保存从目标帧继续确定性模拟所需的最小 authority state，用于：

- bootstrap / initial assignment。
- late join。
- 显式 resync。
- 无法通过命令纠正收敛时的 full snapshot correction。

`BootstrapOnly` 下不得因为 Runtime AI Lab 或表现回放需求向正式 network snapshot 增加字段。

### Rollback Checkpoint / 回滚检查点

Rollback Checkpoint 是本地稀疏恢复点。它可以复用 authority serializer，但必须有独立语义和调用边界：

```text
sparse authority checkpoint
  + retained external commands
  -> fixed-tick replay
  -> target RuntimeFrame authority state
```

Rollback Checkpoint 不要求保存每帧结果，也不保存可以从 authority state 重建的表现或派生缓存。

### Runtime AI Lab Replay

> Current rule: default Lab review rebuilds the immutable initial session and replays commands from the beginning. Sparse intermediate checkpoints are not used automatically. Full Authority and Presentation checkpoints exist only behind the explicit state-capture toggle and manual sync command.

Runtime AI Lab Replay 是开发场景内的回放与诊断能力：

- seek/review 通过重建初始 Lab session + 历史命令正向重演定位历史帧；不自动使用中途 checkpoint。
- authority hash 一致是帧重演成功的主要标准。
- selected frame、Overlay 和 HUD 只读取重演结果，不参与 authority 计算。
- 普通 review 不截断未来；只有明确创建调试分支时才允许截断。

### Presentation Diagnostics

Presentation Diagnostics 检查 Unity 表现是否跟随目标 authority frame。Runtime AI Lab 的显式状态采集当前只保存角色 View、动画、骨骼、IK/secondary bone、view-only ragdoll、VFX runtime 和 Overlay；Audio 仍仅由 test-only 复合探针诊断，HUD 不进入当前最小样本。

这些数据：

- 不进入正式 authority hash。
- 不进入 `BootstrapOnly` network snapshot。
- 不作为“帧同步是否正确”的首要判据。
- 需要录像级精确回看时，只能进入独立、可选、默认关闭的 Visual Capture。

## 数据分类和去留矩阵

所有待审计字段必须标记为以下五类之一：

| 分类 | 定义 | 正常网络命令 | Authority Snapshot | Authority Hash | Lab 默认记录 |
| --- | --- | --- | --- | --- | --- |
| `Command` | 某帧外部输入或权威命令 | 是 | 仅 pending command | 命令流单独诊断 | 是 |
| `Authority` | 会影响未来 gameplay 确定性结果的最小状态 | 否 | 是 | 是 | 稀疏 checkpoint |
| `Derived` | 可从 authority 和配置重新计算 | 否 | 否 | 否 | 按需重建 |
| `Presentation` | Unity 视图和感官表现 | 否 | 否 | 否 | 默认不记录 |
| `Diagnostic` | 报告、summary、计数和 UI 数据 | 否 | 否 | 否 | 低频按需采样 |

### 默认保留为 Authority

以下内容在影响后续模拟时可以进入最小 authority checkpoint：

- 角色确定性 transform、motion、input 和 collision posture。
- CharacterAction Runner 的 active instance、local frame、phase、plan / instance identity 和必要事件进度。
- health、death、hit stun、BattleState 和其他影响规则分支的 combat state。
- Ability cooldown 的稳定结束帧及必要 cast state。
- projectile 的确定性 lifecycle、position、direction、target identity 和 accepted impact history。
- gameplay-owned movable object、field、shield、mine、turret 等仍会影响后续规则的活动状态。
- Runtime AI Planner 会影响未来决策连续性的最小 evaluator/agent state；Editor debug snapshot 和展示字符串不属于 authority。
- 确定性 RNG、stable ID allocator、pending authority command 等无法由目标帧其它字段推导的连续状态。

“默认保留”不代表现有结构整体保留。每个字段仍需证明它会影响 checkpoint 后的下一帧结果；无法证明则降为 `Derived` 或 `Diagnostic`。

### 默认重建为 Derived

- Runtime AI Planner 感知帧缓存。
- 距离、角度、normalized direction、LOS/path 摘要和候选移动等派生查询结果。
- UI view model、中文标签、颜色、chip 和 report text。
- 可由 definition/config + authority state 重建的剩余时间、显示名称和格式化摘要。
- Unity view lookup、角色 marker、HUD agent row 和 Overlay 几何缓存。

### 必须移出正式 Snapshot / Hash

- Unity GameObject / Transform / Renderer / Animator 或 Playables 对象状态。
- clip time、CrossFade、layer weight、locomotion blend 等 Unity 动画播放状态。
- 骨骼 local pose、IK、secondary bone 和 view-only ragdoll body pose。
- audio backend active handle、bus 状态、累计播放计数和实际听觉结果。
- 一次性 VFX、interaction visual summary、floating text、trail 和镜头表现。
- Overlay、HUD、selected frame、selected Agent、tooltip 和 debug string。
- recent diagnostics、Console message、测试计数及不影响后续玩法的 last-frame summary。

### 必须逐字段审计的现有边界

| 当前区域 | 整改要求 |
| --- | --- |
| `VfxRuntime` snapshot/hash | 默认视为 Presentation。若内部混入 gameplay lifecycle，先拆出 gameplay authority，不能整体保留。 |
| Runtime AI Planner state | 仅保留影响下一帧决策连续性的内部状态；debug snapshot、facts 展示和 Editor 数据移出。 |
| command module state | pending authority command 可保留；last drained、recent errors 和 report summary 只做诊断。 |
| runtime event / interaction summary | 事件源的 authority 结果按所属系统保存；已 drain 的当帧 summary 不进入 snapshot/hash。 |
| animation presentation checkpoint | 从默认 timeline 移出；仅允许独立 Visual Capture 使用。 |
| audio / VFX probe sample | 从 authority probe 移出，进入独立 presentation diagnostic test。 |

## 目标数据流

### 正常 BootstrapOnly Gameplay

```text
authoritative RuntimeCommand(frame)
  -> stable command order
  -> fixed Runtime Tick
  -> gameplay authority state
  -> authority hash verification
```

正常流程不通过周期性 full/digest snapshot 推动角色状态。

### Bootstrap / Late Join / Resync

```text
minimal authority snapshot at frame F
  -> restore authority state
  -> enqueue retained authoritative commands after F
  -> fixed-tick replay to current frame
  -> compare authority hash
```

### Runtime AI Lab Replay

```text
immutable initial Lab bootstrap
  -> rebuild clean session
  -> replay retained external commands
  -> compare target authority hash
  -> rebuild presentation from target authority state
  -> optional presentation diagnostics
```

Lab 不得把逐帧完整状态恢复伪装成命令重演成功。

## 整改阶段

### R0：冻结扩张

- 暂停新增 network snapshot schema 字段。
- 暂停向 `CharacterTestRuntimeAiLabFrameSyncProbe` 增加 subsystem sample。
- 禁止恢复旧的逐帧表现检查点 Loop 方案。
- 新问题先归类到 Command、Authority、Derived、Presentation 或 Diagnostic，不再默认“补 snapshot”。

### R1：现状清单

- 枚举 schema `19` 的所有字段、state hash 输入、rollback document 和 network wire 字段。
- 枚举 FrameSyncProbe、timeline record 和 animation presentation checkpoint 的数据来源。
- 以字段为单位记录分类、写入者、读取者、是否影响下一帧、是否可重建和删除风险。
- 最近帧同步相关提交只作为审计范围，不做整批 revert。

### R2：拆分恢复合同

- 将 network bootstrap/resync snapshot 与 local rollback checkpoint 的职责分开。
- 允许共享纯 authority serializer，但调用 API、数据载体和测试必须区分。
- state hash 只消费 Authority 分类。
- Derived 数据在 restore 后由明确 rebuild 阶段重建。

### R3：收缩 Runtime AI Lab

- timeline 默认只保留 RuntimeFrame、外部命令、authority hash 和最小决策 marker。
- 删除默认逐帧完整动画/骨骼表现记录。
- authority 验收收缩到独立 `CharacterTestRuntimeAiLabAuthorityProbe` 的 `RuntimeFrame + authority hash`。
- Runtime 显式状态采集使用最小表现探针；完整 FrameSyncProbe 迁入测试程序集并按角色、Planner、Projectile/Ability、Interactions、Deployables、Fields/Defense、Audio 等领域拆分，不阻塞基础帧同步验收。

### R4：重新定义 A-B Loop

- 基础 Loop 只验证 authority replay，使用初始 bootstrap + external commands 到达 A-B。
- Loop 不产生新 timeline、不进入 Live、不修改 LatestFrame、不截断历史。
- 同一历史帧以 authority hash 和最小 authority sample 一致为验收。
- 视觉精确回放如果仍有需求，使用独立 `Visual Capture` 模式；缺少 capture 时只做 canonical presentation rebuild，不能宣称录像级一致。

### R5：删除和迁移

- 删除已确认的 Presentation/Diagnostic network snapshot 字段和 hash 输入。
- 为旧 schema 保留必要读取兼容，但不继续扩展旧字段语义。
- 删除无消费者的 sample、summary 和恢复代码。
- 拆分过大的 snapshot/probe 文件，按 authority subsystem 和 presentation diagnostic 分责。

### 阶段状态

| 阶段 | 当前状态 | 证据 |
| --- | --- | --- |
| R0 | 已执行 | schema/hash/Probe 不再为 Lab 视觉差异扩张。 |
| R1 | 第一轮完成 | 已识别并移除 VFX、camera、animation input、planner diagnostic；其余 subsystem 继续逐字段证明。 |
| R2 | 基础完成 | network snapshot 与 local rollback checkpoint 已有独立载体和调用入口。 |
| R3 | 基础完成 | timeline 不再保存逐帧完整动画表现，authority probe 与 presentation diagnostics 已分责。 |
| R4 | 已重写 | A-B Loop 使用只读 authority replay，不要求 Visual Capture。 |
| R5 | 进行中 | Runtime 复合 FrameSyncProbe 已迁入 `MxFramework.Tests` 并按领域拆分，单文件不超过 1800 行；显式“状态采集”改存最小表现样本，authority 仍只由 `RuntimeFrame + authority hash` 判定。剩余 snapshot/probe 债务继续逐项审计。 |

## 验收标准

### 正式帧同步

- `BootstrapOnly` gameplay 中，常规运行不会增长 periodic full/digest snapshot 或 snapshot request 计数。
- 相同 bootstrap authority state、相同外部命令和相同配置重演到目标帧时，authority hash 一致。
- 移动、动作、combat、Ability 和 projectile 的 focused tests 通过命令重演验证，不依赖 Unity view 或逐帧完整状态复制。
- authority hash 中不存在 Presentation、Derived 或 Diagnostic 字段。

### Snapshot / Rollback

- bootstrap/resync snapshot 能恢复继续模拟所需的最小 authority state。
- local rollback checkpoint 可以从稀疏恢复点重演到当前帧。
- snapshot 字段必须有“影响下一帧结果”的测试证据；仅用于显示或诊断的字段不得进入 wire。

### Runtime AI Lab

- Review/Seek/A-B Loop 不修改历史命令、record count、LatestFrame 或原始 authority hash。
- 默认 timeline 不保存每帧完整动画、骨骼、音频或 VFX 状态。
- authority replay 失败必须报告 frame、command 或 authority field；表现差异单独报告为 Presentation Diagnostics。
- 缺少 Visual Capture 不能被报告为 frame sync failure。

## 自动化测试重组

后续测试拆成三组，禁止再用一个“大而全一致”断言覆盖全部层：

1. **Deterministic Frame Tests**：输入命令、固定 Tick、authority state 和 hash。
2. **Snapshot / Rollback Tests**：bootstrap/resync roundtrip、稀疏 checkpoint 和正向 replay。
3. **Presentation Diagnostic Tests**：动画、音频、VFX、Overlay 的跟随与重建；失败不等同于 authority frame sync 失败。

至少保留以下关键场景：

- 移动命令从 F 到 F+N 重演后 transform/motion authority hash 一致。
- CharacterAction 在动作中间 checkpoint 后重演，不重复历史 hit event。
- combat hit、Ability cooldown 和 projectile 在相同命令流下重演一致。
- `BootstrapOnly` 正常 gameplay 不进行 routine state sync。
- 显式 resync 能恢复最小 authority snapshot 并继续收敛。
- Runtime AI Lab A-B authority replay 不修改 timeline。
- 人为修改 Unity clip time 或 Overlay 时，只触发 Presentation Diagnostic，不触发 authority hash mismatch。

## 审计与交付规则

- 每个整改切片只处理一个边界：snapshot、hash、Lab timeline、Probe 或 presentation diagnostic，不能再次混合交付。
- 删除字段前先补“该字段不影响下一帧 authority”的证明测试；保留字段则补“缺少它会导致重演发散”的证明测试。
- schema 迁移、network wire 改动和大规模删除必须独立提交。
- A-B Loop 只能按当前 authority-only 语义交付；Visual Capture 仍未实现，不能写成精确视觉回放能力。
- `Assets/_Recovery*` 和其它无关改动不纳入整改。
