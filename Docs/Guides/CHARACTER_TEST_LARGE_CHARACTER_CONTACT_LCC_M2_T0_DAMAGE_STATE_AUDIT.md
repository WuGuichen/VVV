# LCC-M2-T0：伤害链、部位状态与网络边界审计

> Status: Guide
>
> 工作包：LCC-M2-T0（只读审计与生产冻结）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`
>
> 审计基线：`main@8a9dd405b` 的当前工作区，2026-08-24
>
> 本任务只冻结合同、owner 与验证入口；未修改 M2 生产源码、正式资产或 Unity 场景。

## 1. 任务结论

LCC-M2 可以沿现有 CharacterTest 命中链增量实现，不需要替换基础 HP 真源、不需要自研大整数，也不需要新增跨模块通用抽象。生产实现冻结为六个串行任务 T1～T6；每个任务只拥有自己的专属 seam，上一任务 `Verified` 后才交接下一任务。

本次审计确认的关键边界：

- 当前 CharacterTest formal snapshot 最高 schema 是 `29`：`DamageZoneLock=28`、`EngagementReservation=29`，因此 M2 预留 `30`。
- `CharacterTestCombatDamageResolver` 生成 `HitResolveResult`、`DamageProfileResolveResult` 与 `DamageProfileUsage`；`CharacterTestCombatDamageApplier.Apply` 调用 `CombatDamageApplicationSystem`，后者计算 `FinalDamage`、产生 `DamageResolvedEvent`，随后 `GameplayAttributeCommandSystem` 才正式提交 HP 变化。
- 部位累计只消费一次 `DamageResolvedEvent`，且必须同时满足 `HitResult.IsAcceptedDamage`、`FinalDamage > 0`、目标/来源有效、显式 BodyPart 证据有效；telemetry、presentation、reaction 均不是第二个 authority 来源。
- 当前 `CharacterTestHitZoneResolution.Default` 与作者明确配置的 `BodyPartId="body"` 在值上相同。T3 必须增加一个仅限 CharacterTest 的 typed provenance/reason，以区分 `ExplicitBinding` 与 `DefaultFallback`；不得从字符串、damage tag 或 telemetry 反推。
- 当前没有同时携带 accepted、target、partStateId、tag 的统一 Focus consume 事实。M2 只接受显式 action/ability/interaction adapter 提交的 typed fact；不得把 accepted damage 的 tag 当成 consume。
- M2 只增加 Closed/Open/Consumed 与 Intact/Broken 两条正交状态轴；不删除 Hurtbox、不修改 compound 拓扑、不禁用 Trace、不拆网格、不实现 Severed。

## 2. 已审计的现有权威链

### 2.1 命中、伤害与 HP

| 阶段 | 当前权威文件 / seam | 审计结论 |
| --- | --- | --- |
| Hurtbox → Zone/Part | `Runtime/Systems/Combat/CharacterTestCombatDamageResolver.cs` 的 `ResolveHitZone` | collider 通过现有 package 的 BodyPart/HitZone resolver 得到 `CharacterTestHitZoneResolution`；失败回到 `Default`。这里是 typed provenance 的产生点。 |
| 伤害 payload | 同文件的 direct trace/projectile/ability 解析路径 | 已把 `HitZoneId`、`BodyPartId` 写入 `DamageProfileUsage`；Open 倍率应在同一 CharacterTest resolve 路径读取 frame-start state，不改公共 Combat 公式体系。 |
| accepted/final | `Combat/GameplayBridge/CombatDamageApplicationSystem.cs` 的 `Apply` / `PublishEvents` | `IsAcceptedDamage` 后计算 armor-adjusted 与 HP clamp 后的 `finalDamage`，再发布 `DamageResolvedEvent`。这是“是否实际造成正 HP 伤害”的唯一事实。 |
| CharacterTest 编排 | `Runtime/Systems/Combat/CharacterTestCombatDamageApplier.cs` 的 `Apply` | resolution 先产生 damage commands/events，随后 attribute command 提交 HP，再处理死亡、pressure 与 reaction。M2 reducer commit 放在 HP command 提交后、death/reaction 投影前的 CharacterTest 专属尾部。 |
| 下游投影 | `CharacterTestCharacterCombatTraceSystem.Diagnostics.cs`、audio/reaction/presentation consumers | 可消费 committed transition 或只读 state，不得再次累计部位值。 |

冻结后的最小调用顺序：

```text
frame start: capture immutable BodyPartState read view
  -> stable resolved-hit order
  -> ResolveHitZone emits value + typed provenance
  -> CharacterTest damage resolve reads frame-start state and applies Open scale
  -> CombatDamageApplicationSystem calculates FinalDamage and publishes one DamageResolvedEvent
  -> GameplayAttributeCommandSystem commits HP
  -> CharacterTest adapter stages accepted positive-damage facts
  -> explicit accepted action/ability/interaction adapters stage Focus-consume facts
  -> stable sort, bounded dedupe, damage-first reduction, one atomic state commit
  -> publish typed Open/Broken/Consumed transitions once
  -> death/reaction/presentation consume committed results
  -> next authority frame exposes new damage/selection state
```

同一 authority frame 的全部命中必须读取同一 frame-start view。hit A 本帧打开伤口或打破结构时，hit A 与同帧 hit B 均使用旧倍率；新状态只从下一 authority frame 的 damage、selection 与 command 生效。

### 2.2 BodyPart provenance 缺口

现有默认值是 `HitZoneId="body"`、`BodyPartId="body"`。合法作者数据也可能显式使用同名 ID，因此只比较字符串会把 fallback 误当成部位证据。

T3 冻结为一个 CharacterTest-internal enum/field，至少包含：

- `ExplicitColliderBinding`：合法 collider/BodyPart/HitZone 映射解析成功；允许继续匹配 part definition。
- `DefaultFallback`：collider index、package、binding 或 resolver 失败而使用默认 body；禁止累计。
- `NoPartEvidence`：合法来源但没有可映射 BodyPart；禁止累计。
- `InvalidEvidence`：身份不一致或损坏；fail closed 并输出 typed diagnostic。

这个字段只服务 CharacterTest 的 M2 authority 与诊断；不加入公共 `DamageResolvedEvent`、不创建公共“命中证据框架”，也不通过 `"body"`、空串或错误文本猜来源。

## 3. Authoring 与编译合同冻结

### 3.1 唯一作者真源

新增可选文件 `geometry/body_part_damage.json`：

- format：`mx.characterBodyPartDamage.v1`
- schema：`1.0`
- revision：正整数
- `partStates[]`：按 `partStateId` ordinal canonical sort
- 每角色最大 definition 数：`8`

状态定义保持父里程碑列出的最小字段：`partStateId`、`memberPartIds[]`、可选 `damageZoneId`、`woundCapacity`、`openDamageScaleMilli`、可选 `focusConsumeTag`、`maxIntegrity`、`integrityDamageScaleMilli`、可选 `breakReactionId`、`authoredPriorityDeltaOpen`、`authoredPriorityDeltaBroken`。不增加血条 UI、mesh、Severed、恢复、时间衰减或任意脚本回调字段。

数值边界冻结为：

| 字段 | 边界 / 语义 |
| --- | --- |
| `woundCapacity` / `maxIntegrity` | `0..1_000_000`；0 关闭对应轴；非零时为正阈值。 |
| `openDamageScaleMilli` | `0..4_000`；启用 wound 轴时必须大于 0；`1000` 表示不变。 |
| `integrityDamageScaleMilli` | `0..4_000`；结构损伤使用 `FinalDamage × scale / 1000` 的有界整数换算。 |
| dynamic priority delta | `-1000..1000`；只参与显式 comparator 字段，不改变可命中/可瞄准/可接近布尔值。 |
| 累加与乘法 | 输入先过上述边界，乘法使用 `long` 中间值并饱和到轴容量；不使用 BigInteger。 |

`memberPartIds[]` 必须非空、全文件不重复归属，并且全部引用现有 `body_parts` 身份；`damageZoneId` 只允许引用现有 Zone，不能隐式创建。Dragon14 v1 固定四组：head、left wing、right wing、tail；tail 可以聚合 base/mid/tip，阈值由 T6 实战校准。

### 3.2 文档状态机

Authoring loader 必须区分：

| 状态 | 行为 |
| --- | --- |
| `Missing` | 兼容旧 package；编译为空 definitions，旧 damage/selection 行为不变，不标脏。 |
| `Current` | 规范化、校验、canonical save/compile/hash。 |
| `Future` | 保留原始文本，read-only 阻断写入与编译覆盖。 |
| `Invalid` | 保留原始文本与 typed diagnostics；write-ineligible，事务不得部分写。 |

Runtime binding 使用稳定 definition index 与编译后的 BodyPart→definition index lookup；tick 不解析 JSON、不用 LINQ、不动态扩容、不依赖字典枚举顺序。source identity 至少包含 format/schema/revision/content hash，并进入 package identity 与 T5 mismatch gate。

## 4. Runtime state 与 reducer 冻结

每实体最多 8 个 entry，spawn/reinitialize 一次性建立固定容量、按 `partStateId` ordinal 排序。无 definitions 的实体保持零 entry。

每个 entry 的最小 authority state：

- stable definition index；`partStateId` 仅用于边界/诊断，不在 hot path 查字符串；
- `woundValue` 与 `Closed | Open | Consumed`；
- `currentIntegrity` 与 `Intact | Broken`；
- `lastAcceptedHitFrame`；
- 单调 `revision`。

不新增通用 ECS 容器；由 CharacterTest entity/world 明确拥有小型 fixed-capacity component。stage buffer 也是固定容量：每帧最多按现有 accepted-hit 上限乘以实体部位上限预分配；超限 fail closed、记录 typed overflow，不扩容。去重键复用稳定命中身份（source/target/action instance/policy event/trace/local frame）加 part definition index；Focus consume 使用 source/target/adapter kind/correlation/part index。frame-local key 不进入 snapshot；真正影响未来的 state 与 revision 进入 snapshot。

Reducer 规则：

1. stable sort accepted-damage facts；去重后按 `FinalDamage > 0` 累加 wound 与 integrity。
2. wound/integrity 分别饱和到容量；同一 fact 可同时触发 Open 与 Broken。
3. 每条轴只在实际状态转换时递增 entry revision 并发布一次 typed transition。
4. 再 stable sort Focus-consume facts；只有 frame-start 已 Open、tag 精确匹配、authority accepted 的事实可转 Consumed。
5. 同帧刚 Open 不可同帧 Consumed；Consumed 不再累积 wound，但 structure 仍可继续累积。
6. Closed/Open/Consumed 与 Intact/Broken 均为当前 entity 生命周期内单向状态；M2 只有正式 respawn/reinitialize 重置，不提供治疗或修复。

一次性事件通过“仅在 before != after 时发布”与 entry revision 构造稳定 event identity；不增加无限 correlation 历史表。

## 5. Source policy 与 Focus consume 冻结

### 5.1 Accepted damage

| 来源 | M2 v1 结论 | 必要证据 |
| --- | --- | --- |
| direct weapon/action trace | 接受 | accepted、`FinalDamage > 0`、显式 collider BodyPart provenance、稳定 hit key。 |
| direct projectile impact | 接受 | 同上；projectile 的 area pulse 若没有显式 part 则拒绝。 |
| ability shape/direct hit | 接受 | 同上；必须落到单一显式 Hurtbox/BodyPart。 |
| self / friendly / dead target | 拒绝 | 保持现有 hit policy，不能 stage。 |
| blocked / parried / invincible / rejected | 拒绝 | `IsAcceptedDamage=false` 或 `FinalDamage=0`。 |
| duplicate hit/event | 拒绝 | 稳定 key 只消费一次。 |
| default body fallback / no part evidence | 拒绝 | 即使字符串为 `body` 也不能累计。 |
| zero-damage control hit | 拒绝 | `FinalDamage <= 0`。 |
| poison/status periodic tick | 拒绝 | 没有显式 Hurtbox part provenance。 |
| no-part AOE / environment | 拒绝 | M2 不从位置重新 query Hurtbox。 |

累计值使用实际应用到 HP 的 `FinalDamage`，不是 profile base damage、telemetry incoming damage、impact power 或 presentation number。若将来要让特定 status/AOE 累积，必须新增显式 source-policy adapter 与测试，不能放宽默认规则。

### 5.2 Focus consume

M2 v1 只定义 CharacterTest-internal typed `FocusConsumeFact` ingress。生产 adapter 必须明确提供：authority frame、adapter kind（Action/Ability/Interaction）、source、target、part definition index、stable tag、stable correlation、accepted=true。

- `CharacterTestCharacterActionSystem` 的 outcome trace 只表示动作接受，不天然携带 target/part，不可直接当 consume 事实。
- ability timeline、interaction event 也只有在其正式处理器已确定 target 与 partStateId 后才可提交。
- damage payload/tag、reaction id、UI focus、telemetry 字符串均不得触发 consume。
- v1 没有满足全部证据的正式 adapter 时，`focusConsumeTag` 保持空并 fail closed；T3/T6 的 focused test 可通过专属 typed ingress 验证 reducer，不伪造公共事件。

## 6. Selection seam 冻结

当前 `CharacterTestDamageZoneSelector.Compare` 的稳定顺序是 opportunity level、earliest hit tick、`AuthoredPriority`、ZoneId、HurtboxId，且已有 previous-lock 迟滞。

T4 只在 authored priority 比较项引入显式 `DynamicPriorityDelta`，形成 bounded effective priority，并把 authored/dynamic/effective 与 state revision 输出到 typed diagnostics。以下语义不变：

- `GeometryInReach`、root XZ distance、legal Zone、pose-path cost 仍彼此分离；
- dynamic state 不把 `Approachable=false`、`Hittable=false` 或 `Aimable=false` 改成 true；
- previous lock 仍按现有 witness、opportunity level 与 earliest-hit threshold 保持/切换；state revision 变化只触发重新验证，不绕过迟滞；
- Closed+Intact 或无 definitions 时 dynamic delta 为 0，选择结果与旧版一致。

## 7. Formal schema / rollback 冻结

`CharacterTestNetworkSnapshotParticipant.SchemaVersionValue` 当前等于 `EngagementReservationSchemaVersionValue=29`。M2-T5 固定新增 `BodyPartStateSchemaVersionValue=30`；只有在 T5 开始前该数字被其它已合入变更占用时，才重新取下一可用版本并先回写台账。

schema 30 必须覆盖：

- 每实体稳定排序的 definition index/state entries；
- wound/structure enum 与累计值、last accepted frame、entry revision；
- definition source revision/content hash；
- 会影响下一次 selection 的 Zone lock/state witness；
- capture、apply、clone、formal serialization；
- full、owned、non-owned hash 的对应 owner 投影；
- rollback+1 frame 的 damage、transition、selection command 与 state hash parity；
- replay、late join、source mismatch。

legacy `29` 及更早快照没有部位状态时，按当前 binding 初始化 Closed+Intact/零 wound/满 integrity；其动态 priority 为 0、伤害倍率为 1，因而保持旧行为。future schema 或 definition source identity mismatch 必须 fail closed，不部分 apply。restore 后丢弃 frame-stage、selection 派生 cache 与诊断 cache，从恢复后的 authority state 重建；presentation/highlight/telemetry/派生 Zone summary 不入 formal snapshot。

## 8. 串行 owner 与交接表

| 任务 | 唯一生产 owner | 冻结交接 |
| --- | --- | --- |
| T1 Authoring/compiler | `Tools/MxFramework.Authoring/.../CharacterPackages/` 下新的 body-part-damage DTO/service/validation/compiler partial；CLI service partial；`Character.RuntimeSpawn` imported binding；Dragon14 `geometry/body_part_damage.json` 与正式生成输出；对应 tests | 交付 Current/Missing/Future/Invalid、max 8、canonical hash/source identity、四组 Dragon definitions。 |
| T2 Runtime state | 新 `CharacterTestBodyPartState*` component/world/reducer/event/diagnostic 文件；entity spawn/reset 最小接线；focused runtime tests | 交付 fixed-capacity read view、stage/commit API、typed reason、NoAlloc 证据；不得写 damage/selector/network。 |
| T3 Damage integration | `CharacterTestCombatDamageResolver.cs`、`CharacterTestCombatDamageApplier.cs`、必要的 CharacterTest 专属 partial/adapter 与 focused damage tests | 交付 typed provenance、frame-start modifier、accepted positive-damage stage、typed Focus consume ingress/source matrix；不得改公共 Combat event 形状。 |
| T4 Selection | `CharacterTestDamageZoneSelector.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`、专属 state adapter/diagnostics/tests | 交付 dynamic/effective priority 与 lock witness；root distance、Approachable 和 Attack Opportunity 合同不变。 |
| T5 Network | `Network/Snapshot/CharacterTestNetworkSnapshot.cs` 及专属 snapshot/hash/replay/checkpoint partial、network tests | 交付 schema 30、clone/capture/apply/hash、legacy/rollback/replay/late join/source mismatch。 |
| T6 Studio/acceptance | `Tools/MxFramework.CharacterStudio/web/**` 的 body-part 专属 UI/API/state、`scripts/smoke.mjs`、M2 PlayMode/Playable/NoAlloc 证据与事实文档回写 | 只调用 T1 同一 service；阈值只从作者真源调整；最终只做一轮 P0/P1 只读审查。 |

共享热点不得并行写。若后续任务发现上游合同缺陷，回派对应 owner，不在当前任务复制或兼容两套真源。

## 9. 验证入口与证据命名

| 任务 | focused filter / 直接证据 |
| --- | --- |
| T1 | Authoring `CharacterPackageTests` 的 body-part-damage fixture；RuntimeSpawn imported binding tests；重复 compile byte/hash diff；`Docs/Evidence/LCC/LCC-M2-T1/`。 |
| T2 | `CharacterTestBodyPartState*Tests`：阈值、双轴、同帧、dedupe、overflow、respawn、warmed 0 B；`Docs/Evidence/LCC/LCC-M2-T2/`。 |
| T3 | `CharacterTestBodyPartDamage*Tests`：pre/post damage 与 source-policy 每行；旧角色/damage 回归；`Docs/Evidence/LCC/LCC-M2-T3/`。 |
| T4 | `CharacterTestBodyPartDamageZone*Tests`：priority、Approachable、lock、顺序扰动、root-distance/CD 回归、NoAlloc；`Docs/Evidence/LCC/LCC-M2-T4/`。 |
| T5 | `CharacterTestBodyPartStateNetwork*Tests`：schema/hash/legacy/rollback+1/replay/late join/mismatch；`Docs/Evidence/LCC/LCC-M2-T5/`。 |
| T6 | CharacterStudio smoke + browser direct evidence；Dragon14 formal Game View/PlayMode；8 角色 warmed 0 B；最终 P0/P1 review；`Docs/Evidence/LCC/LCC-M2-T6/`。 |

普通 Unity C# 改动按仓库默认只做一次 `validate_unity_scripts`，除非任务自身 Gate 明确要求 focused tests/Playable/NoAlloc。T0 为只读源码审计，不运行 Unity、测试或性能验证。

## 10. T0 验收清单

- [x] 复核 resolver → application → HP → reaction 的实际调用链。
- [x] 冻结 `DamageResolvedEvent.FinalDamage > 0` 为唯一 accepted accumulation seam。
- [x] 识别显式 `body` 与默认 fallback 同值问题，并冻结内部 typed provenance。
- [x] 冻结 source-policy 与 Focus consume typed ingress；damage tag 不 consume。
- [x] 冻结 Missing/Current/Future/Invalid、max 8、数值边界与无 BigInteger 政策。
- [x] 冻结 frame-start read、damage-first reducer、同帧不自放大与一次性 transition。
- [x] 复核 DamageZone comparator/lock，并冻结只加动态 priority 的边界。
- [x] 复核 schema 29 占用并冻结 M2 schema 30、legacy/hash/rollback/replay/late join。
- [x] 精确到任务/文件族登记 T1～T6 owner、focused filter 与证据目录。
- [x] 未修改生产源码、正式资产、Unity 场景或父里程碑状态。

## 11. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main@8a9dd405b` 当前工作区；未提交。 |
| 实际修改文件 | 仅本文（恢复并完成审计冻结表）。 |
| 源码证据 | `CharacterTestCombatDamageResolver.ResolveHitZone`；`CharacterTestCombatDamageApplier.Apply`；`CombatDamageApplicationSystem.Apply/PublishEvents`；`CharacterTestDamageZoneSelector.Select/Compare`；`CharacterTestNetworkSnapshotParticipant` schema constants。 |
| 测试 / Unity / 性能 | 未执行；T0 是只读源码与合同审计，不将其表述为运行验证。 |
| 审计结果 | source chain、时序、source policy、typed provenance、schema 30、owner/DAG 均已冻结；无生产阻断。 |
| 过度设计约束 | 不实现 BigInteger、通用部位框架、公共命中证据抽象、HTTP/取证工具、Severed 或 Hurtbox 拓扑变化。 |
| 下一步 | 主代理回写 T1～T6 为 `Production-Frozen` 后，仅派发 T1；T1 `Verified` 前不派发 T2。 |

## 12. 状态历史

| 日期 | 原状态 | 新状态 | 证据摘要 | 限制 |
| --- | --- | --- | --- | --- |
| 2026-08-24 | `Planned` | `In Progress` | M1 Gate 解除，只读审计已派发 | T1～T6 生产写入锁定 |
| 2026-08-24 | `In Progress` | `Verified` | 复核 damage/BodyPart/selector/network seam；冻结 schema 30、source policy、typed provenance、owner 与验证入口 | 仅 source-level 审计；无 M2 生产代码、测试或 Unity 证据 |
