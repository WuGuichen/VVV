# LCC-M3-T0：切断状态、拓扑与资源 seam 审计

> Status: Guide
>
> 工作包：LCC-M3-T0（只读审计）
>
> 状态权威：[LCC-M3 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)
>
> 执行文档状态：`Verified`（只读审计与生产任务回写输入已冻结；不代表 Severed 已实现）
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
> 前置：M2 `Verified`。

## 1. 任务结果与非目标

冻结Broken→Severed条件与生效帧、stable collider/zone/anchor/action identity、compound leaf removal、lock/reservation失效、Trace gate、网络状态和Dragon14 mesh/resource可交付等级，并为T1～T6分配唯一owner。

非目标：不实现切断、不修改mesh/prefab/action/schema、不承诺正式可拆视觉资源存在。

## 2. 只读输入

- [总台账第1～8节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)、[M3第1～4节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_SEVERANCE_TOPOLOGY.md)、M2实际交付。
- M2 Authoring/state/event、`CharacterTestCharacterHurtboxSystem.cs`、Combat compound、DamageGeometry/Zone、Engagement reservation/joint planner、CharacterAction config/runtime/trace、network、RuntimeCharacterView、Dragon14 source mesh/prefab/resource mappings。
- 本节列出的生产链全部只读。

## 3. 写入所有权

唯一写入本文冻结表、resource audit、ownership、test matrix 和实际执行记录；其它生产源码、资产、任务文档和父状态只读。

## 4. 必须冻结的结论

### 4.1 状态与提交时序

- 结构轴固定为 `Intact -> Broken -> Severed`，伤口轴继续独立；`Severed` 只接受带 source/target/part/correlation 的显式 typed sever fact，且该帧起始结构必须已经是 `Broken`。
- 同一 accepted hit 即使同时把部位打成 `Broken` 并携带 sever tag，本帧也只能提交到 `Broken`；最早由下一 authority frame 的新 sever fact 转为 `Severed`。状态在 frame end 提交，Hurtbox、Zone、lock/reservation、Planner 与 action trace 的结果从下一 authority frame 起变化。
- 重复 fact 去重，Severed 事件只投影一次；死亡不重置，respawn/reinitialize 重置，snapshot restore 直接恢复权威状态且不重放 transient 事件。
- 不新增通用状态机、数学、仓储或序列化抽象；沿用 M2 fixed-capacity state、typed reducer 和事件桥。

### 4.2 Dragon14 稳定身份与拓扑

| 项目 | 冻结结果 |
| --- | --- |
| 部位组 | `partstate.000003.tail`；成员 `tail_base`、`tail_mid`、`tail_tip` |
| 18 个正式 Hurtbox 中的尾部叶子 | `hurt.tail_base.capsule.1`、`hurt.tail_mid.capsule.0`、`hurt.tail_tip.capsule.0` |
| Zone | `zone.tail` 由 `tail_mid`/`tail_tip` 支撑；Severed 后为空并不可用。`tail_base` 同时属于 `zone.core`，但胸部仍支撑 core，因此 core 只移除该叶而不失效 |
| Anchor | `anchor.tail.rear` 依赖 `zone.tail`，Zone 失效后 lock stale、reservation release，Planner/Attack Opportunity 从重建后的只读视图排除它 |
| 身份规则 | BodyPart/collider/compound leaf/Zone/Anchor ID 与数组顺序不改变；只维护 active mask/filter 与低频 topology revision，不压缩重编号 |
| fallback | 显式叶子全部 inactive 时不得回退整身 compatibility AABB；缺失/畸形绑定继续 fail closed |

T2 只在既有 compound leaf/refit seam 上做稳定叶子过滤与 revision；T3 消费 T2 的只读几何/Zone 结果。root-XZ distance、`GeometryInReach`、合法 Zone 与 pose-path cost 合同保持不变。

### 4.3 动作与 emitter 依赖

| 动作 / 事件 | 正式事实与门禁 |
| --- | --- |
| `940002` / `000003.attack6a` | 同一动作同时包含 bite 与 `000003.attack6.tail_sweep`；只 gate 尾扫 hit event/trace，不能禁用整项动作或 bite |
| `000003.attack6.tail_sweep` | frame `112..126`，`BakedSocketCapsule`，`tailSweepStart -> tailSweepEnd`，radius `0.55` |
| `940004` / `000003.attack30b` | `000003.attack30b.tail_spin`，frame `108..134`，同一 socket capsule 与 radius；gate 该 event/trace |
| Socket | `tailSweepStart`：abstract `tail_base` -> `tail_03`；`tailSweepEnd`：abstract `tail_tip` -> `tail_05`；正式 FBX 与 bake track 存在 |
| 其它 emitter | 当前正式包未发现尾部 projectile、ability 或 interaction emitter；记录为“无”，不修改通用 emitter。未来新增尾部 emitter 时必须声明同一 typed part dependency |

依赖判定放在 combat hit event/trace emit 层；提交前已 Severed 的依赖事件不可 emit，动作中 Severed 则从下一 authority frame 停止后续依赖 trace。missing socket 与 `Severed` 必须输出不同 typed reason。

### 4.4 资源与网络边界

- 正式资源只有单一 `000003_basemesh2.fbx`、Body/Eye material 与整身 Preview Prefab；没有直接证据证明尾部是独立 renderer/mesh/prefab/resource key。M3 基线冻结为 **Runtime Slice**：允许 view projection 隐藏/裁切可证明的尾部表现和播放 VFX/SFX，但不伪造可拾取残肢、Rigidbody 或第二 authority limb。T5 只有取得独立资源、生命周期和 Game View 直接证据后才可升级声明。
- 当前网络最高 schema 为 `30`，M3 候选为 `31`，T6 开始时必须再次确认“下一可用”。schema 只增加最小 Severed 权威状态/revision/event witness；active leaf mask、Zone 有效性、priority 等派生结果不序列化。
- `<=30` legacy 输入恢复为非 Severed；full/owned/non-owned hash、rollback+1、replay、late join 与 source mismatch 必须覆盖。拓扑切换是低频路径；稳定帧 warmed 路径继续 NoAlloc。

### 4.5 T1～T6 文件级 owner 与交接

| 任务 | 唯一生产 owner / focused 验证 / 交接 |
| --- | --- |
| T1 | Authoring body-part damage 的 Core/validation/compiler partial/CLI、RuntimeSpawn contracts/import JSON、`CharacterTestBodyPartStateComponent.cs`、`CharacterTestBodyPartDamageAuthorityBridge.cs`、正式 `body_part_damage.json` 和 CharacterStudio body-part 文件；只扩展 Severed 字段、typed fact 与状态。运行 Authoring `character-body-part-damage`、RuntimeBinding、BodyPartState、Studio smoke；证据 `Docs/Evidence/LCC/LCC-M3-T1/ACCEPTANCE_2026-08-25.md`。完成后把稳定 binding/state 交给 T2/T4 |
| T2 | `CharacterTestCharacterHurtboxSystem.cs`、`CharacterTestDamageGeometrySnapshot.cs` 的 topology/active-leaf partial，以及既有 Combat compound leaf removal/refit seam；不建通用 topology manager。focused topology + Hurtbox regression；证据 `LCC-M3-T2`。完成后串行移交 `DamageGeometrySnapshot` 给 T3 |
| T3 | 接手 `CharacterTestDamageGeometrySnapshot.cs` 的 Zone 只读视图，修改 `CharacterTestDamageZoneSelector.cs`、Engagement reservation/planner 与 Runtime AI Planner Attack Opportunity adapter/joint 的 topology-only partial；不改 root distance。focused Zone/lock/reservation/Attack Opportunity 与既有 CD 回归；证据 `LCC-M3-T3` |
| T4 | CharacterAction config/import/validation 中必要 dependency 字段、CharacterTest Action config source/runtime/trace/system 与 planner candidate adapter 的 topology-only partial、正式 `character_actions.json`；按事件 gate 并保留 bite。focused direct event、mid-action、natural attack；证据 `LCC-M3-T4` |
| T5 | `CharacterTestRuntimeCharacterView.cs` 或窄 `.Severance.cs` partial、debug/presentation projection 与 VFX/SFX；正式资源默认只读，基线 Runtime Slice。仅有真实资源和画面时才升级；证据 `LCC-M3-T5` |
| T6 | `CharacterTestNetworkSnapshot.cs`、`CharacterTestBodyPartStateNetworkSnapshot.cs`、`CharacterTestReplayValidation.cs` 的 schema31、集成/性能/Playable/文档与里程碑唯一终审；核心缺陷回派原 owner。证据 `LCC-M3-T6` |

串行 DAG 为 `T1 -> T2 -> T3`，随后使用 T1/T3 结果执行 `T4 -> T5 -> T6`。`CharacterTestDamageGeometrySnapshot.cs` 只允许 T2 完成交接后由 T3 写；不并行写共享热点。

## 5. 审计清单与证据

- [x] 逐函数追踪 state transition -> Hurtbox SyncAll -> Zone/Anchor/Planner -> action/trace -> presenter/network。
- [x] 用正式 binding/source 验证 18 Hurtbox 中的 BodyPart/collider identity，不按数组缩减猜测。
- [x] 检查 Dragon14 mesh/bones/material/prefab/resource mapping，冻结为 Runtime Slice。
- [x] 为 T1～T6 分配文件/partial owner、focused filter、证据文件和串行交接点。
- [x] 无 TBD；主代理完成事实复核，文档健康、链接与 diff check 作为本任务关闭检查。

## 6. 停止条件与派发

未发现阻止 T1 开始的外部 blocker。视觉资源不支持“完整可拆残肢”声明，因此已缩减为 Runtime Slice；若后续不能保持稳定叶子 identity、事件级 gate 或 schema 兼容，停止对应生产任务并回派 owner，不以整身动作禁用、AABB fallback 或假资源绕过。

## 7. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享工作区；不提交/暂存/推送/清理 |
| 实际修改文件 | 仅本文；生产源码、资产、其它任务文档与父状态在审计期间只读 |
| 测试与原始证据 | 子代理完成 source/resource 只读调查，主代理复核正式 JSON、动作/Socket、资源清单与 network schema；未运行生产测试，不把审计声明为功能验证 |
| 集成 / 回滚结果 | 无生产集成，无需回滚；输出已形成 T1～T6 的文件级写入合同 |
| 状态历史 | 2026-08-25 `Planned -> In Progress`；同日只读结论冻结并 `In Progress -> Verified` |
| 执行者 / 审查者 | 子代理执行只读调查；主代理完成事实复核与冻结集成；里程碑最终独立审查仍仅保留 T6 一轮 |
| transition / identity / schema | Broken 起始帧 + 新 typed fact 才可 Severed；稳定叶子不重编号；当前 schema30，T6 候选 schema31 |
| Dragon14 resource seam | 单 mesh/整身资源，未证明独立尾部资源；M3 基线仅 Runtime Slice |
| 限制 | 本任务只证明生产边界可实施，不证明 Severed、拓扑失效、动作 gate、网络或画面已落地 |
