# CD-M2-T3：DamageZone 稳定选择、迟滞与诊断

> Status: Guide
>
> 工作包：CD-M2-T3
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T2 `Verified`
>
> 本文实现无持久副作用的稳定选择器及 Planner/Attack Opportunity 接入；Zone lock 的 snapshot/hash/rollback/replay 属于 T4。

## 1. 任务结果

在多 Hurtbox 大型目标上，以冻结快照和显式 previous lock 为输入，确定性选择 DamageZone/Hurtbox，并用失效规则、机会等级提升、2 tick 改善门槛和稳定决胜消除轻微动画造成的切换抖动。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)、[T0 冻结记录](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md) 和 [T2 实际交付](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T2_RUNTIME_AGGREGATION.md)。
- [CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) 的 opportunity 等级、earliest tick、Hurtbox/Zone witness 和 fail-closed 合同。
- [AI](../Interfaces/AI.md) 与 [Combat](../Interfaces/Combat.md) 当前 Planner/trace 章节。
- `Packages/com.vincent.wgameframework/AGENTS.md`、`Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md`。

开工前，T0 必须已冻结“机会等级更高”的完整顺序、2 tick 比较方向、相等结果和稳定决胜链。

## 3. 选择器合同

- 输入必须包含同一 evaluation snapshot 下的 target、resolved action、Zone aggregates、每 Zone Attack Opportunity 结果、previous lock witness 和全部相关 revisions。
- 输出至少包含 query status、selected Zone/Hurtbox、opportunity level、earliest tick、switch reason、previous/new lock witness 和确定性诊断。
- 选择器本身无副作用：不直接写 Planner state、network snapshot 或世界组件；T4 负责持久化输出。
- `Unavailable`、`Unsupported`、`StaleSnapshot` 不能参与肯定选择，也不能触发根距离或旧范围静默回退。
- 没有作者 Zone 的 legacy Hurtbox 兼容只能按 T0 冻结的临时查询身份工作，不得伪造成持久 Zone lock。

## 4. 实现顺序

- [ ] 建立固定容量 candidate scratch 和稳定排序键，避免 LINQ、临时集合和容器顺序依赖。
- [ ] 实现初次选择：合法性/机会等级 → earliest tick → 作者优先级 → Zone stable ID → Hurtbox stable ID。
- [ ] previous Zone 仍有效时默认保持。
- [ ] previous Zone 失效时立即重新选择，并输出明确 `Invalidated` 类 switch reason。
- [ ] 候选机会等级严格提升时允许切换，并记录旧/新等级。
- [ ] 同等级时仅当 earliest hit 至少改善 2 tick 才允许切换；改善 0 或 1 tick 保持旧 Zone。
- [ ] 通过门槛后的相等候选按冻结的作者优先级和稳定 ID 决胜。
- [ ] 将选择器接入 CD-M1 Attack Opportunity/PrimaryAttack guard 所需位置，不改变 `target.distance` 与 `target.inAttackRange`。
- [ ] 输出当前锁定 Zone、机会等级、earliest tick、切换原因和 revision witness，供 T6 Playable 展示。

## 5. 自动化测试

计划由单一 focused class `CharacterTestCharacterDistanceM2SelectionTests`（或 T0 冻结的等价类）拥有纯选择与 Planner 接入用例。测试必须调用生产选择器，不得在测试中复制决胜算法计算“期望值”。

- [ ] 无 previous lock 时按完整稳定决胜链初选。
- [ ] previous lock 有效且候选只改善 0/1 tick 时保持。
- [ ] 同等级改善恰好 2 tick 和大于 2 tick 时切换。
- [ ] 更高机会等级无需满足 tick 门槛即可切换。
- [ ] previous Zone 失效、部位禁用、revision stale 和 target 改变时行为符合冻结合同。
- [ ] 作者优先级相同/不同、Zone ID 和 Hurtbox ID 决胜稳定。
- [ ] 随机打乱 Zone、Hurtbox、broadphase 和 evaluator 返回顺序，结果与诊断不变。
- [ ] 轻微动画抖动的长序列不会在两个 Zone 间频繁切换。
- [ ] `Unavailable`、`Unsupported`、`StaleSnapshot` fail closed，PrimaryAttack 不提交。
- [ ] warmed 后 candidate 生成、归约和诊断投影的权威热路径为 0 B GC。

## 6. 非目标

- 不把 lock 写入 Planner authority、network snapshot、hash、rollback 或 replay。
- 不实现 DamageZone 编辑器。
- 不实现 Engagement Anchor、CandidatePose、路径成本、容量/预约或 `(target, action, zone)` 跨 target/action 联合搜索。
- 不改变特殊技能的 `CompatibilityLegacyRange`，除非 T0 已明确将某一动作纳入 M2 evaluator 支持矩阵。

## 7. 完成 Gate

- 所有边界值、顺序扰动、长序列抖动、stale/fail-closed 和 NoAlloc tests 通过。
- Planner/PrimaryAttack 只消费同一 snapshot 的选择结果，旧 fact 语义不变。
- 诊断足以解释“保持/切换/失效/无有效 Zone”，没有仅供测试的隐藏状态。
- Unity refresh/compile/domain reload、Editor ready、Console 0 error 通过。
- 实际交付记录完整后 T3 才能进入 `Verified`；权威回滚能力仍不得宣称完成。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交 |
| 实际修改文件 | `CharacterTestDamageZoneSelector.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`、selection focused tests |
| Selection focused tests | 初选、保持、立即提升、2 tick 门槛、失效、稳定决胜、跨帧迟滞均通过；包含在 M2 EditMode 22/22 |
| Planner integration | 正式 adapter 消费 selector；按 source 保存 previous lock；rollback reset 清除 cache；legacy 无 Zone 路径保持 M1 结果 |
| 顺序扰动 / 抖动 | 输入顺序扰动与同等级轻微改善长序列不抖动；revision witness 只对 identity/合同变化失效 |
| NoAlloc | warmed selector 查询 0 B GC 用例通过 |
| Unity 验证 | refresh/compile/domain reload/editor ready 通过，Console 0 error（最终 job `f51be42d052245e88ef17a8b8a80f081`） |
| 已知限制 / 阻断 | 无 T3 阻断；authority 持久化由 T4 实现 |
