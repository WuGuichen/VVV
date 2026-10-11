# LCC-M2：伤口与部位破坏状态

> Status: Guide
>
> 里程碑：LCC-M2
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 5 节
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（T0～T6 全部 Verified；M2 Gate 已闭合）

> `main` 集成后 schema 映射：schema 30 属于数值 `CharacterActionId`；本里程碑 Body Part state 当前使用 schema 31。本文历史表中的 schema30 是集成前原始证据标签。

## 1. 目标与阶段边界

M2 在现有 Hurtbox → HitZone → BodyPart → damage application 链上增加可作者化、可回滚的伤口与部位结构状态，使 Dragon14 的头、左右翼和尾部能够积累局部结果，同时不改变 Hurtbox 拓扑。

本阶段完成：

- 多个现有 `BodyPartId` 聚合为一个稳定 `partStateId`；
- 伤口 Closed/Open/Consumed 状态；
- 结构 Intact/Broken 状态；
- 命中前状态参与后续伤害倍率、Focus/诊断和 DamageZone 动态优先级；
- accepted damage 后的部位累积、一次性 Open/Broken 事件；
- Authoring/Compiler/Runtime/CharacterStudio、formal snapshot/hash/rollback/replay 全链。

本阶段不删除 Hurtbox、不改变 compound 叶子、不禁用动作 Trace、不拆视觉网格，也不引入 Severed；这些属于 M3。

## 2. Authoring 合同

新增独立可选文件：

`geometry/body_part_damage.json`

独立文件避免把持续战斗规则混入基础 `body_parts.json` 身份表。计划格式为 `mx.characterBodyPartDamage.v1` / schema `1.0`，包含 revision 和稳定排序的 `partStates[]`。

每个定义候选字段：

| 字段 | 合同 |
| --- | --- |
| `partStateId` | 包内唯一稳定状态身份 |
| `memberPartIds[]` | 一个或多个现有 BodyPart；全文件内不得重复归属 |
| `damageZoneId` | 可选现有 Zone 投影；不得自动创建 Zone |
| `woundCapacity` | 开放伤口所需累积，有界正整数；0 表示不支持伤口 |
| `openDamageScaleMilli` | Open 时后续命中的有界伤害比例 |
| `focusConsumeTag` | 可选显式 action/ability/interaction tag；空表示 v1 不支持消费。accepted damage tag 本身不触发 consume |
| `maxIntegrity` | 结构耐久，有界正整数；0 表示不可破坏 |
| `integrityDamageScaleMilli` | accepted final damage 到结构损伤的确定性比例 |
| `breakReactionId` | 可选稳定反应/表现语义引用；Runtime 不从字符串推断行为 |
| `authoredPriorityDeltaOpen` | Open 时 Zone 选择的有界动态加分 |
| `authoredPriorityDeltaBroken` | Broken 时 Zone 选择的有界动态加分或减分 |

约束：

- 每个 BodyPart 最多属于一个 part-state definition；重复、缺失引用、非法 Zone 或数值越界 fail closed。
- 缺失文件兼容现有伤害行为，不生成默认状态、不标脏。
- future schema 原文保持并只读阻断；不得覆盖。
- Dragon14 v1 建议至少配置 `part.head`、`part.left_wing`、`part.right_wing`、`part.tail`；确切阈值必须通过正式战斗基线校准，不在计划页预填。
- `part.tail` 可以聚合 tail_base/mid/tip，但 M2 的 Broken 不删除任何一个叶子。

## 3. Runtime 状态与事件

### 3.1 双轴状态

每个运行时 part state 至少包含：

- `partStateId` / stable definition index；
- `woundValue`、`WoundState = Closed | Open | Consumed`；
- `currentIntegrity`、`StructureState = Intact | Broken`；
- `lastAcceptedHitFrame`；
- `revision`；
- 需要去重的一次性 Open/Broken correlation 状态。

固定容量由编译后的定义数量决定，spawn 时一次初始化；tick/命中路径不得字典动态扩容、LINQ 或字符串查表。

### 3.2 伤害接入顺序

```text
frame-start immutable BodyPartState read snapshot
  -> physics hit / collider id
  -> existing HitZone + BodyPart resolution
  -> every hit in this frame reads the frame-start state for damage modifiers
  -> existing HitResolve / DamageProfile / HP application
  -> stage accepted positive-damage facts with explicit BodyPart evidence
  -> stage accepted typed Focus-consume facts
  -> after the damage phase, stable sort + dedupe + reduce all staged facts
  -> atomically commit state revisions
  -> publish Open/Broken/Consumed transition once
  -> next authority frame consumes the new state for damage and selection
```

冻结规则：

- 同一 authority frame 的全部命中都读取 frame-start immutable state；hit A 在本帧打开伤口或打破部位时，hit A 与同帧 hit B 都使用旧倍率。新状态从下一 authority frame 的命中、选择和命令生效。
- 只有 `IsAcceptedDamage` 且 final applied HP damage 大于 0、BodyPart 来自显式 Hurtbox/合法映射的事实才累积。
- self/friendly/dead/duplicate/blocked/rejected、默认 body fallback、纯 poison/status tick、无部位范围伤害和零伤控制命中默认不累积；未来支持必须由显式 source policy 加入。
- 同一 accepted damage event 只能累积一次；Damage telemetry、ResolvedEvent 和回放投影不能分别重复消费。
- Focus consume 只响应 authority 已接受、具有明确 target/partStateId、匹配 `focusConsumeTag` 的 typed action/ability/interaction fact，并要求 frame-start 状态已经是 Open；accepted damage 即使携带同名tag也只参与伤口/结构累积，不直接consume。同帧刚打开的伤口不能被同帧consume。reducer先应用稳定排序的accepted-damage facts，再应用consume facts。
- Consumed 是当前 entity 生命周期内伤口轴终态：不再累积 wound、不自动回到 Closed/Open；结构 integrity 仍可继续累积。只有正式 respawn/reinitialize 重建默认 Closed，M2 不提供治疗/时间恢复。

### 3.3 DamageZone / Attack Opportunity 投影

- Open/Broken 只提供动态 priority delta 和可见状态；不改变 Zone 的 `Hittable`、`Aimable`、`Approachable` 基础布尔值。
- 翼 Zone 当前不可接近时，即使 Open 也不得生成长期 Engagement Anchor 或 CandidatePose。
- 动态 priority 必须进入现有联合选择 comparator 的显式字段和诊断，不通过修改 root distance、Hurtbox priority 或集合顺序注入。
- 当前 Zone lock 只因现有失效/切换规则或显式更优机会改变；状态变化不绕过已有迟滞和稳定决胜。

### 3.4 Reaction 与 Presentation

- Open/Broken/Consumed authority event 包含 frame、source/target、partStateId、BodyPart/Zone 证据、before/after，以及对应 damage 或 consume correlation。
- CharacterAction reaction、VFX、SFX、UI/Focus highlight 只消费事件或只读状态，不决定状态转换。
- M2 不要求专用 mesh 破损资产；没有正式资源时允许调试 overlay/文本证明 authority，但不能宣称美术完成。

## 4. Schema 与网络

T0 已确认 CharacterTest formal network schema 当前为 29，并冻结 M2 使用 30；只有 T5 开始前该版本已被其它已合入变更占用时，才改用下一可用版本并先同步总台账。

必须纳入：

- entity part-state fixed array/entries；
- wound/structure values and enums；
- last frame/revision 及真正影响未来命令的去重状态；
- full/owned/non-owned hash；
- capture/apply/clone/legacy migration；
- rollback 后下一 tick damage/selection/command parity；
- replay、late join 和 source definition revision/hash mismatch。

Presentation highlight、调试文本和派生 Zone summary 不进入 formal snapshot。

## 5. 工作包状态与实施顺序

| 工作包 | 状态 | 交付物 | 前置 |
| --- | --- | --- | --- |
| [LCC-M2-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T0_DAMAGE_STATE_AUDIT.md) | `Verified` | 重审 damage resolver/applier、telemetry、BodyPart/Zone、selector、network schema 和 CharacterStudio seam；冻结字段/来源政策 | M1 Verified |
| [LCC-M2-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T1_AUTHORING_COMPILER.md) | `Verified` | `body_part_damage.json` Authoring DTO/validation/hash/service/Compiler/binding 与 Dragon14 definitions | T0 Verified |
| [LCC-M2-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T2_RUNTIME_STATE.md) | `Verified` | fixed-capacity BodyPartState component、definition lookup、frame-staged Open/Broken/Consumed reducer 和事件 | T1 Verified |
| [LCC-M2-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T3_DAMAGE_INTEGRATION.md) | `Verified` | frame-start pre-hit modifier、accepted-damage/typed consume fact adapters、source policy、去重、reaction/presentation projection | T2 Verified |
| [LCC-M2-T4](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T4_SELECTION_INTEGRATION.md) | `Verified` | DamageZone/joint selector 动态 priority、lock/diagnostics、Attack Opportunity 回归 | T3 Verified |
| [LCC-M2-T5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T5_AUTHORITY_NETWORK.md) | `Verified` | formal schema、full/owned/non-owned hash、rollback/replay/late join/source mismatch | T2、T4 Verified |
| [LCC-M2-T6](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_T6_ACCEPTANCE.md) | `Verified` | CharacterStudio 编辑闭环、Dragon14 Playable、阈值校准、NoAlloc、接口/能力表和独立终审 | T5 Verified |

主代理是本表唯一写入者。全路线 `Audit-Frozen` 且 M1 `Verified` 后才派发 T0；T0 `Verified` 并提交结构化回报后，由主代理完成生产任务的文件级回写并把 M2 提升为 `Production-Frozen`。schema、Authoring/compiler、Runtime state、damage、selector、network 和 UI 的共享热点再按唯一 owner 串行交接。

## 6. 预计影响面

候选包括：

- Character Authoring geometry DTO/load/save/compiler/source mapping
- `CharacterRuntimeSpawnContracts` / imported binding
- CharacterTest component world/entity/spawn/definition source
- `CharacterTestCombatDamageResolver` / `CharacterTestCombatDamageApplier`
- DamageZone snapshot/selector、joint selection diagnostics
- CharacterTest network snapshot/hash/replay
- CharacterStudio geometry/body-part workspace
- Dragon14 package data、debug overlay、UI/VFX/SFX project adapters
- focused EditMode/PlayMode/NoEngine/Authoring tests

不得直接把现有单体 `GameplayArmorIntegrityComponent` 当成多部位容器；若复用其纯数值 reducer，必须保持稳定 ID、固定容量和多 entry 状态由 CharacterTest 明确拥有。

## 7. 验证矩阵

| 类别 | 必须覆盖 |
| --- | --- |
| Authoring | round-trip、重复 member、缺失 BodyPart/Zone、非法阈值、future schema、稳定 hash、Dragon14 四组 fixture |
| Reducer | Closed→Open、Open consume、Intact→Broken、同 hit 同时跨阈值、一次性事件、已 Open/Broken 再命中 |
| Damage | pre-hit multiplier、触发命中不自放大、拒绝/重复/零伤/default body/poison 不累积、显式 source policy |
| Selection | Open/Broken priority、不可接近翼不变、lock/迟滞、稳定 comparator、顺序扰动 |
| Network | 预期 schema 30 或下一版本、full/owned/non-owned hash、legacy、rollback 后下一 hit/command、late join |
| CharacterStudio | 新建/编辑/复制/排序、Undo/Redo、save/reopen、普通刷新保草稿、unknown schema 只读、窄视口 |
| Playable | 头/左右翼/尾分别累积；命中错误部位不串组；Open/Broken 诊断/表现；现有 HP/死亡/尾攻击不回归 |
| 性能 | 多角色全部 part state lookup/reducer/selector warmed 0 B；低频 snapshot 继续单独登记 |

## 8. M2 完成 Gate

- T0～T6 全部 `Verified`。
- Authoring→Compiler→Runtime→damage→selection→network→CharacterStudio 只有一条权威链。
- 伤口与结构状态正交，accepted damage 单次累积，触发 hit 不自放大，所有排除来源有测试。
- 动态 priority 不改变根距离或 Zone 可接近性，CD-M0～M4 回归通过。
- formal schema/hash/rollback/replay/late join 完整；下一 tick damage、selection command 和 state hash parity 有直接证据。
- Dragon14 四个 part-state group 的 Playable 和 CharacterStudio 直接证据齐全；无 mesh 资产时限制明确。
- warmed Runtime 0 B GC、Unity 完整验证、Console 0、独立审查 P0/P1 为 0。
- 按实际实现回写 Combat/Gameplay/Network/AI/Authoring 接口与 `Docs/CAPABILITIES.md`；明确 M2 尚不支持 Severed 或 Hurtbox 拓扑变化。

## 9. 实际交付记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 7/7 Verified；M2 Gate 已闭合并解除 M3-T0 前置。 |
| 实际分支 / 提交 | `main@8a9dd405b` 工作区；未提交。 |
| 实际文件 | T0 审计冻结表；T1 Authoring/compiler/binding与Dragon14定义；T2 fixed-capacity BodyPartState；T3 damage authority bridge与typed projection；T4 dynamic Zone state、selector/ordinary/joint Planner接线；T5 schema 30 BodyPartState/lock snapshot、三类hash、restore清理与network tests；T6 CharacterStudio、正式阈值/生成物、Playable/NoAlloc/证据；v5会话identity与M1 Presence runtime兼容seam均已窄补。 |
| Authoring / Runtime tests | 主代理最终复跑：M2 59/59、RuntimeSpawn 4/4、Presence authority/replay 13/13、Presence v5 runtime 2/2、NoAlloc 1/1、Dragon14 2/2、既有Presence/尾攻击3/3、自然攻击1/1；Authoring/Studio/Node/import均exit 0；Unity validator `f4b4ff25b5a24395ba4fe7e98a66d562` Console 0。 |
| selector / network | T4 selector/joint动态priority与lock revision已Verified；T5 formal schema 30、full/owned/non-owned hash、legacy、rollback/replay/late join/source mismatch与v5会话身份已Verified。 |
| Unity / CharacterStudio | CharacterStudio CRUD/映射/Undo/Redo/草稿/规范化保存与窄宽浏览器证据闭合；Unity正式Game View证明Dragon14入场，数值状态由正式PlayMode证明。 |
| Playable | Dragon14 Head真实连段`66→26→26`证明Open/Alive→Broken/Alive→HP0/唯一死亡；头/左右翼膜/尾尖四组分别走resolver→applier→accepted state且不串组；既有尾攻击回归通过。 |
| 性能 / NoAlloc | 八个不同entity共享physics world，32 warmup + 4096次state/pre-hit/accepted commit/selector测得0 B；低频snapshot继续单列。 |
| 接口 / 能力表 | CharacterApplication/Combat/Network与CAPABILITIES已按M2 Verified事实回写；Focus仅有typed ingress且正式tag为空，不宣称生产adapter已接入。 |
| 限制 / 阻断 | 无M2阻断。唯一终审P0=0、P1=0；保留非阻塞P2（Focus真实生产adapter门禁）与P3（动态/Mod catalog前做全局root唯一性）。不声明Severed、Hurtbox拓扑变化或M3/M4。 |

## 10. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | T0～T6 | 初始化 | `Planned` | 草拟独立 body-part-damage 文档、双轴状态、伤害接入顺序、动态 Zone 投影和网络门禁 | 执行包仍待审查；等待 M1 Verified，无生产实现 |
| 2026-08-24 | T0～T6 | `Drafting` | `Audit-Frozen` | 7/7 执行文档经结构、DAG、写入边界和验证合同独立审查，P0/P1 为 0 | 等待 M1 `Verified` 后才可派 T0；无生产实现 |
| 2026-08-24 | T0 | `Planned` | `In Progress` | M1 Gate 已解除；按 Audit-Frozen 边界派发 damage/BodyPart/selector/network/Authoring/Studio 只读审计 | 只允许写 T0 冻结表；T1～T6 生产任务继续锁定 |
| 2026-08-24 | T0 | `In Progress` | `Verified` | 复核现有 damage/final HP、BodyPart fallback、selector 与 schema 29；冻结 schema 30、max 8、typed provenance、source policy、owner 和验证入口 | 只读 source-level 证据；无 M2 生产代码或运行声明 |
| 2026-08-24 | T1 | `Planned` | `In Progress` | M2 提升为 Production-Frozen 后只派发 Authoring/compiler/runtime binding 与 Dragon14 四组定义 | T2～T6 继续等待；不写 damage/selector/network/UI |
| 2026-08-24 | T1 | `In Progress` | `Verified` | 四态Authoring/专属保存、typed validation、binding v5与v1～v4兼容、Dragon14四组/10 lookup闭合；重复compile稳定，Authoring/CLI/Unity/RuntimeSpawn focused通过 | 阈值待T6校准；全局ResourceMapping缺build target为既有后置项；未增加任务级评审 |
| 2026-08-24 | T2 | `Planned` | `In Progress` | T1 Gate解除后只派发fixed-capacity state、frame-start read view、bounded staging/reducer与typed transition | damage/selector/network/UI继续锁定；不得新增通用ECS或动态容器 |
| 2026-08-24 | T2 | `In Progress` | `Verified` | max 8 state、frame-start read view、damage-first reducer、逐轴revision、typed transition与生命周期接线闭合；主代理修正空Authored分类后EditMode 7/7、4096 warmed 0 B、Console 0 | 未接damage/selector/network/UI；无任务级独立评审 |
| 2026-08-24 | T3 | `Planned` | `In Progress` | T2 Gate解除后只派发pre-hit modifier、accepted-damage/typed Focus adapters、source policy与transition projection | selector/network/UI继续锁定；不得旁路现有HP authority或新增通用事件框架 |
| 2026-08-24 | T3 | `In Progress` | `Verified` | HP前读取frame-start状态、HP后accepted fact/reducer、typed source/Focus/telemetry/reaction投影闭合；主代理复跑EditMode 34/34、4096 warmed 0 B、Unity Console 0 | 未跑全仓测试或PlayMode；M2唯一最终只读审查仍留T6 |
| 2026-08-24 | T4 | `Planned` | `In Progress` | T3 Gate解除后只派发动态DamageZone priority、lock/revision诊断与joint selector/Attack Opportunity回归 | T2/T3/network只读；不得改变Approachable、root distance或pose-path语义 |
| 2026-08-24 | T4 | `In Progress` | `Verified` | 同Zone有界priority、Focus可见、eligibility透传、lock revision与ordinary/joint同源闭合；主代理复跑9/9、核验141/141、4096 warmed 0 B、Unity Console 0 | formal network仍留T5；无任务级独立评审 |
| 2026-08-24 | T5 | `Planned` | `In Progress` | T2/T4 Gate解除且当前最高schema仍为29，按冻结30派发BodyPartState/lock witness正式snapshot、hash与rollback/replay | T2/T4只读；不得持久化派生priority、Focus文本或telemetry |
| 2026-08-25 | T5 | `In Progress` | `Verified` | schema30 BodyPartState/lock witness、canonical bytes、三类hash、legacy、rollback+1、replay/late join/source mismatch与restore transient清理闭合；geometry v5 catalog identity按M1-T4 owner窄修后主代理组合20/20、最终Unity Console 0 | 未增加任务级独立评审；Playable、Studio、完整M2性能与唯一终审留T6 |
| 2026-08-25 | T6 | `Planned` | `In Progress` | T5 Gate解除，按冻结owner派发CharacterStudio、Dragon14校准/Playable、完整NoAlloc、事实文档与一次P0/P1终审 | 不实现Severed/拓扑/动作失效；核心缺陷回派T1～T5 owner，不在验收任务复制真源 |
| 2026-08-25 | T6 / M2 | `In Progress` | `Verified` | CharacterStudio、正式Dragon14四组/连段、schema30网络、八角色0 B、v5 Presence回归、主代理最终矩阵与唯一终审闭合；P0=0、P1=0 | 接受Focus生产adapter P2与catalog全局root唯一性P3；登记后续门禁，不增加评审轮次 |
