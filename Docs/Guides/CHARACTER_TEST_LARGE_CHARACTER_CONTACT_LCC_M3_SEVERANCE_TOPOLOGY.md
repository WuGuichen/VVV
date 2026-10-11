# LCC-M3：切断、几何拓扑与动作失效

> Status: Guide
>
> 里程碑：LCC-M3
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 5 节
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（M3-T0～T6 全部 Verified；里程碑 Gate 已闭合）

> `main` 集成后 schema 映射：schema 31 为 Body Part state，Severed 当前使用 schema 32。本文历史表中的 schema31 是集成前原始证据标签。

## 1. 目标与完成结果

M3 在 M2 的 Broken 状态之上增加显式 Severed 转换，并让切断结果确定性地影响受击几何、DamageZone/Engagement Anchor、Attack Opportunity、动作 Trace、AI 可用动作和 Unity 表现。

Dragon14 首个正式切断对象为尾部；翼或其它部位只有在正式拆分资产、动作依赖和关卡验证准备完整后才能加入同一通用合同。

M3 完成时：

- 尾部 Severed 是 formal authority state，不由 mesh、Animator 或 collider 是否可见推断；
- 已切断部位的 Hurtbox 叶子从后续权威帧起不再进入 compound/query；
- 相关 DamageZone lock、Engagement reservation、Attack Opportunity sample 和 AI action eligibility 明确失效；
- 依赖尾部的扫尾/尾旋 Trace fail closed，不会从已不存在的 socket 继续造成伤害；
- 视觉拆分、VFX、SFX、掉落或残肢对象只消费一次性 authority event；
- collider、part、zone、anchor 和 action identity 不因拓扑缩减重新编号。

## 2. Authoring 与状态扩展

### 2.1 切断定义

向后兼容扩展 M2 的同一份 `body_part_damage.json` 与现有 binding；不并列维护第二份部位真源或通用 severance schema。

每个可切断 part state 至少需要：

- `severable`；
- `severRequiredTag` 或等价 typed policy；只有 frame-start Broken + 新 typed sever fact 可触发，不能从伤害类型或自由字符串猜测；
- `presentationEventId`；

其余关系不重复作者化：禁用 BodyPart 直接复用现有 `memberPartIds`，Zone/Anchor 由现有稳定关系派生，T4 动作依赖直接引用 `partStateId`。当前没有独立尾部资源，因此不写 detached prefab/resource key；以后只有资源证据齐全时再由 Presentation 专项扩展。

非法/缺失引用、同一 BodyPart 被多个切断定义控制、根核心被标为可切断或 typed tag/event 非稳定 ID 均 fail closed。

### 2.2 状态转换

结构轴扩展为：

```text
Intact -> Broken -> Severed
```

- v1 不允许 Intact 直接 Severed。同一 accepted hit 同时把 Intact 打成 Broken 且携带 sever tag 时，本次 commit 只到 Broken；必须在后续 authority frame 以 frame-start Broken 状态接受新的合法 sever fact，才能在该帧末 commit Severed。
- Severed 单向且生命周期内不可恢复；重生/重新 spawn 由角色初始化重新建立状态。
- 触发切断的 accepted hit 完成当前帧既有 HitResolve/HP/部位累积；Severed 从下一 authority frame 的几何同步、动作生成和选择生效，不追溯撤销当前帧已经接受的 Trace。
- transition event 按 target + partStateId + state revision 去重，只发布一次。

## 3. 几何与选择失效

### 3.1 Hurtbox 拓扑

`CharacterTestCharacterHurtboxSystem` 在遍历稳定 binding 数组时查询只读 part-state filter：

- 已切断 BodyPart 的原 collider id 保留，但该叶子不 `Upsert`；其余 collider id 不压缩、不重排。
- `BeginCompoundColliderUpdate` / `EndCompoundColliderUpdate` 正确处理 leaf removal 和 BVH topology revision。
- restore/late join 直接从 Severed state 重建相同叶子集合；不依赖此前 compound 增量历史。
- explicit body colliders 全部被切断或失效时 fail closed，不回退到兼容整身 AABB。
- Body Presence v1 核心 volume 不随断尾删除；若未来 Presence volume 可依赖 part state，必须单独扩展 schema 和稳定失效合同。

### 3.2 DamageZone 与 Engagement Anchor

- DamageGeometrySnapshot/Zone aggregate 必须反映有效 Hurtbox 集；无有效 hit-zone geometry 的 Zone 进入明确不可用状态。
- 指向失效 Zone 的当前 lock 在同一状态 transition 的权威处理阶段被标记 stale/invalid，后续 selection 按现有 comparator 重选。
- 指向失效 Zone 的 Engagement reservation 被确定性释放；容量计数、owned/non-owned hash 和 Planner command 在 rollback 后一致。
- Anchor 配置本身不从 package 删除；它在当前 part state 下不可用，以便 restore/respawn 复用稳定身份。
- root distance 和其它 Zone 的 CandidatePose/path cost 不被重算成新的距离定义。

### 3.3 动作与 Trace gate

依赖可切断部位的攻击事件必须显式声明 `requiredPartStateId` 或通用 typed dependency，至少覆盖：

- `000003.attack6.tail_sweep`；
- `000003.attack30b.tail_spin`；
- T0 审计到的所有当前尾部 direct trace、projectile、ability、socket 和 interaction emitters。未来新增此类事件必须声明同一 typed dependency，否则 validation fail closed。

规则：

- action eligibility 在提交前检查依赖；不满足时 fail closed 并返回 typed reason。
- 动作已经开始但部位在后续帧被切断时，下一帧及之后的依赖 Trace 不生成；非依赖动画可按动作取消/反应策略结束。
- Trace resolver 不通过 socket 是否存在猜测部位状态；missing socket 和 Severed dependency 是不同诊断。
- AI Planner 的候选 action 在联合选择阶段看到同一 dependency state，不能先选择一个 Runtime 必定拒绝的尾部动作。

## 4. Presentation 与残肢边界

- Combat/CharacterTest authority 只发布 Severed event 和稳定资源语义，不实例化 Unity prefab、Rigidbody 或物理残肢。
- Presenter 可以隐藏/切换 skinned mesh part、生成 VFX/SFX、创建非权威残肢 view；它不能改变 Hurtbox filter 或 part state。
- 若残肢需要继续作为可交互 Gameplay object，必须通过已有 world-object/movable-object 正式 spawn contract 创建新的 authority entity；不得把 view-only Rigidbody 当成掉落物或障碍。
- 没有正式可拆 mesh、bone split 或 resource binding 时，M3 最多声明 Runtime Slice；只有实际画面、资源绑定和回收生命周期通过才能声明完整 Playable 表现。

## 5. 工作包状态与实施顺序

| 工作包 | 状态 | 交付物 | 前置 |
| --- | --- | --- | --- |
| [LCC-M3-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T0_SEVERANCE_AUDIT.md) | `Verified` | 冻结 Broken 起始帧 typed Severed、3 个稳定尾叶、事件级动作 gate、Runtime Slice、schema31 候选与 T1～T6 owner | M2 Verified |
| [LCC-M3-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T1_AUTHORING_STATE.md) | `Verified` | Severed Authoring/schema/compiler/runtime state、CharacterStudio 字段闭环、typed condition 和 formal event | T0 Verified |
| [LCC-M3-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T2_HURTBOX_TOPOLOGY.md) | `Verified` | Hurtbox stable filter、compound leaf removal/rebuild、DamageGeometry revision 和 fail-closed fallback | T1 Verified |
| [LCC-M3-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T3_TARGETING_INVALIDATION.md) | `Verified` | Zone lock/Anchor reservation/Attack Opportunity/Planner invalidation 与纯状态重建 witness；formal rollback parity 由 T6 收口 | T2 Verified |
| [LCC-M3-T4](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T4_ACTION_TRACE_DEPENDENCY.md) | `Verified` | action/trace typed dependency、Dragon14 tail sweep/spin eligibility、mid-action transition 和 diagnostics | T1、T3 Verified |
| [LCC-M3-T5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T5_PRESENTATION_RESOURCES.md) | `Verified` | Presenter/resource binding、可选 authority 残肢 spawn 边界、Game View 证据 | T4 Verified |
| [LCC-M3-T6](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M3_T6_ACCEPTANCE.md) | `Verified` | schema/hash/replay/late join、性能、全回归、接口/能力表和独立终审 | T5 Verified |

主代理是本表唯一写入者。全路线 `Audit-Frozen` 且 M2 `Verified` 后才派发 T0；T0 `Verified` 并提交结构化回报后，由主代理完成生产任务的文件级回写并把 M3 提升为 `Production-Frozen`。状态、Hurtbox 拓扑、Zone/Anchor、动作 Trace、Presenter 与 network 由不同 owner 串行交接，验收代理只读。

## 6. 预计影响面

- M2 body-part Authoring/schema/compiler/runtime state
- `CharacterTestCharacterHurtboxSystem`
- damage geometry snapshot / DamageZone aggregate / selection locks
- Engagement Anchor reservation/Planner joint selection
- CharacterAction hit-volume schema、trace plan/builder/resolver、AI action eligibility
- CharacterTest network snapshot/hash/replay
- Unity character view、resource binding、VFX/SFX/optional world object adapter
- Dragon14 action package、mesh/prefab 或明确的无拆分资产限制
- EditMode/PlayMode/Authoring/CharacterStudio/NoAlloc tests

## 7. 验证矩阵

| 类别 | 必须覆盖 |
| --- | --- |
| 状态 | Intact→Broken→Severed、非法直接切断、重复 trigger、同帧多 hit、事件一次性、respawn 初始化 |
| Hurtbox | 原 collider id 保持、指定叶子移除、其余不重排、全部显式叶子失效不回退 AABB、restore 重建一致 |
| Zone/Anchor | tail Zone 失效、lock stale、reservation release、其它 Zone 不受影响、rollback 下一命令一致 |
| 动作 | sever 前扫尾/尾旋正常；sever 后提交拒绝；动作中切断后续窗口取消；missing socket 与 sever reason 分离 |
| 网络 | 下一可用 schema、full/owned/non-owned hash、legacy、rollback/replay/late join、source mismatch |
| 表现 | event 去重、mesh 隐藏/拆分、VFX/SFX、pool/recovery；可选残肢 authority/view 边界 |
| 性能 | 动态 leaf topology 切换为低频；稳定状态下 Hurtbox SyncAll/selector/trace gate warmed 0 B |
| 回归 | M0～M2、CD-M0～M4、Dragon14 bite、非尾部动作、死亡/重生、pathfinding |

## 8. M3 完成 Gate

- T0～T6 全部 `Verified`。
- Severed 是正式单向 authority state；转换时序、当前帧命中和下一帧几何/动作生效边界有测试和文档。
- Hurtbox 叶子移除保持稳定 collider identity，restore/late join 重建一致且无 compatibility AABB 回退。
- Zone lock、Anchor reservation、Attack Opportunity 和 Planner action 同步失效，rollback 后下一命令/hash parity 通过。
- Dragon14 尾部动作依赖显式 part state；断尾后不再产生尾部伤害，非尾动作不回归。
- Presenter 不回写 authority；若声明完整视觉断尾，正式资产、Game View、生命周期和回收证据齐全。
- Unity Console 0、全链回归、稳定路径 0 B GC、独立审查 P0/P1 为 0。
- 接口/能力表明确 Runtime Slice 与完整 Playable 表现等级，以及尚未支持的翼/其它部位切断范围。

## 9. 实际交付记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 7/7 Verified；M3 Gate闭合 |
| 实际分支 / 提交 | `main`共享工作区；未提交，保留既有非LCC修改 |
| 实际文件 / 资产 | T1原位扩展body-part Authoring/binding/state/CharacterStudio并生成Dragon14 revision2；T2复用既有compound seam实现18→15稳定叶、typed unavailable与独立topology witness；T3完成Zone/lock/Anchor reservation/Planner精确失效；T4完成两个正式尾部事件的typed dependency、Planner/Runtime gate与active trace prune；T5完成一次性事件消费、生命周期与typed资源降级/诊断；T6完成schema31、restore后Hurtbox/Zone同步与既有lock/reservation精确清扫 |
| focused / network tests | T1 Authoring/CLI/Studio、RuntimeBinding5/5、BodyPartState9/9；T2 focused5/5、既有删除回归1/1；T3 focused5/5、既有CD回归26/26；T4 focused8/8；T5 EditMode4/4、T4回归2/2、PlayMode1/1；T6两条P1各1/1、network/replay聚合16/16、T1～T5核心62/62 |
| Unity / Playable | T5已取得真实CharacterTest before/after Game View并证明scene clean/Console0；after使用Editor-only drive，只声明Runtime Slice，不证明生产player path |
| Dragon14 mesh/resource | 只有单一 basemesh、Body/Eye material 与整身 Preview Prefab；无独立尾部资源，T5已实证typed fallback并固定为 Runtime Slice |
| 性能 / NoAlloc | T2断尾后15叶，64 warmup+512 measured SyncAll为0 B；T3 selector+joint projection与T4 eligibility+trace prune各warmed 4096次为0 B；T6八角色低频观测10.9627 ms/0 B，仅记录而不臆造阈值 |
| 接口 / 能力表 | CharacterApplication、Combat、Network与CAPABILITIES已按schema31和Runtime Slice事实回写 |
| 限制 / 阻断 | 无阻断；唯一终审原P1=2均已整改并由主代理关闭，当前未关闭P0/P1=0；接受P2：无生产sever caller/player path；接受P3：单basemesh Runtime Slice、无低频硬阈值；不实现BigInteger、逐字节HTTP取证或新通用抽象 |

## 10. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | T0～T6 | 初始化 | `Planned` | 草拟 Severed 状态、下一帧生效、稳定 leaf identity、Zone/Anchor/action 失效和表现边界 | 执行包仍待审查；等待 M2 Verified，无生产实现 |
| 2026-08-24 | T0～T6 | `Drafting` | `Audit-Frozen` | 7/7 执行文档经结构、DAG、写入边界和验证合同独立审查，P0/P1 为 0 | 等待 M2 `Verified` 后才可派 T0；无生产实现 |
| 2026-08-25 | T0 | `Planned` | `In Progress` | M2 Gate已解除；按Audit-Frozen边界派发Severed状态、拓扑、动作、network与Dragon14资源只读审计 | 只允许写T0冻结表；T1～T6生产写入继续锁定 |
| 2026-08-25 | T0 | `In Progress` | `Verified` | source/resource 审计冻结状态时序、3 个尾叶、Zone/Anchor、两个真实尾部事件、Runtime Slice、schema31候选与串行owner；主代理完成事实/链接/diff复核 | 仅设计与源码边界证据，不代表Severed能力已实现 |
| 2026-08-25 | T1 | `Planned` | `In Progress` | M3提升为`Production-Frozen`，只派发现有body-part Authoring/binding/state/CharacterStudio与Dragon14尾部定义扩展 | T2～T6继续等待各自前置；共享topology/selector/action/network文件禁止写 |
| 2026-08-25 | T1 | `In Progress` | `Verified` | 三字段可选扩展、frame-start Broken typed Severed、事件去重、稳定filter/dependency、Dragon14 revision2与Studio闭合；Authoring/CLI/Studio、5/5、9/9及Unity Console0通过 | 无生产sever caller；topology/action/network仍待T2～T6，未作任务级循环评审 |
| 2026-08-25 | T2 | `Planned` | `In Progress` | T1 Gate解除，顺序派发3个稳定尾叶的Hurtbox filter、compound leaf removal/refit与topology witness | T3～T6继续锁定；不得修改Zone/Planner/action/presentation/network |
| 2026-08-25 | T2 | `In Progress` | `Verified` | Dragon14三尾叶18→15、原ordinal ID、typed zero-leaf/InvalidBinding、restore重建与topology witness闭合；focused5/5、删除回归1/1、512 measured 0 B、Unity Console0，主代理复核job与diff通过 | 未跑PlayMode/全仓/独立评审；Zone/Anchor/action/presentation/network按T3～T6串行收口 |
| 2026-08-25 | T3 | `Planned` | `In Progress` | T2 Gate解除，只派发Zone空集、lock stale、Anchor reservation release、Attack Opportunity/Planner同源失效与纯状态重建 | T4～T6继续锁定；不改root XZ、动作Trace、Presenter或network |
| 2026-08-25 | T3 | `In Progress` | `Verified` | authored Zone identity与精确member witness失效、同帧reservation sweep、Attack Opportunity/Joint/Planner同源排除闭合；主代理读取focused5/5，既有回归26/26、4096 warmed 0 B、Unity Console0 | 未跑PlayMode/全仓/独立评审；formal action、presentation、network留T4～T6 |
| 2026-08-25 | T4 | `Planned` | `In Progress` | T3 Gate解除，只派发typed partStateId dependency和两个既有Dragon14尾部事件的事件级Trace gate | 不 gate整段动作或bite；不改T3/T5/T6 owner，不新增通用condition框架 |
| 2026-08-25 | T4 | `In Progress` | `Verified` | 两字段事件依赖、Runtime/Planner同源eligibility、下一帧active-trace prune与两个正式Dragon14事件闭合；主代理读取focused8/8、4096 warmed 0 B及分步compile/reload/Console0证据 | integrated validator在domain reload时transport断线且无job id，按规则保留回退证据；未跑PlayMode/全仓/独立评审 |
| 2026-08-25 | T5 | `Planned` | `In Progress` | T4 Gate解除，只派发Severed event消费、typed presentation fallback、生命周期/overlay与真实Game View的Runtime Slice收口 | 无独立尾部mesh/resource；禁止伪造detach/Rigidbody/world limb或让Presenter回写authority |
| 2026-08-25 | T5 | `In Progress` | `Verified` | 一次性事件消费、late attach/restore静默投影、Release/respawn生命周期、typed资源降级与真实before/after Game View闭合；主代理读取EditMode4/4、T4回归2/2、PlayMode1/1并目视核对画面 | after为Editor-only acceptance drive，不证明生产sever ingress/player path；单basemesh只声明Runtime Slice；无任务级独立评审 |
| 2026-08-25 | T6 | `Planned` | `In Progress` | T5 Gate解除，只派发schema31、hash/rollback/replay/late join、组合回归/性能、CharacterStudio/能力表与唯一终审前候选收口 | 不扩大翼/头切断或完整Playable；caller/resource限制必须保留，核心缺陷回派T1～T5 owner |
| 2026-08-25 | T6 | `In Progress` | `Verified` | schema31与三类hash、rollback/replay/late join、16/16聚合闭合；唯一终审发现非Severable篡改与restore拓扑顺序两项P1，均按原owner整改，主代理直接核对源码和1/1、1/1、16/16 job后关闭，当前未关闭P0/P1=0 | 不追加第二轮独立终审；保留无生产caller、单basemesh Runtime Slice与无低频硬阈值的P2/P3限制 |
| 2026-08-25 | M3 | `In Progress` | `Verified` | T0～T6为7/7 Verified；Severed authority、18→15稳定叶、Zone/Anchor/动作失效、Runtime Slice表现与schema31恢复一致性满足里程碑Gate | 能力等级仍为Runtime Slice，不声明完整可玩断尾或翼/头切断 |
