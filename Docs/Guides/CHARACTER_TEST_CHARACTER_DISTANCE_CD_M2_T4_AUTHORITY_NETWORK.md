# CD-M2-T4：Zone Lock 权威状态、网络、回滚与重演

> Status: Guide
>
> 工作包：CD-M2-T4
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T3 `Verified`
>
> 本文只把已经验证的选择结果纳入 CharacterTest authority；不扩展选择策略、不实现 CharacterStudio 或 CD-M3 预约状态。

## 1. 任务结果

使所有会影响未来攻击命令的 Zone lock 状态进入确定性 Planner authority、formal network snapshot、full/owned/non-owned hash、rollback 和 replay，并证明分支回滚后选择与命令完全一致。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)、[T0 冻结记录](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md) 和 [T3 实际交付](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T3_ZONE_SELECTION.md)。
- [CD-M1](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M1_ATTACK_OPPORTUNITY.md) schema 24～27、source revision、active HitTrace window 和 CasterBody previous-center 的实际交付模式。
- [AI](../Interfaces/AI.md)、[Network](../Interfaces/Network.md)、[Combat](../Interfaces/Combat.md) 的 authority/snapshot/hash/rollback/replay 章节。
- 当前 `CharacterTestRuntimeAiPlannerStateSnapshot`、`CharacterTestNetworkSnapshot`、ownership state、rollback checkpoint 和 replay 路径。
- `Packages/com.vincent.wgameframework/AGENTS.md`、`Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md`。

开工前确认 T0 已冻结 lock identity、revision witness、清除条件、旧 schema 归一化和 ownership/hash 语义。

## 3. 实现范围

### 3.1 Planner Authority

- [ ] 增加固定容量/无分配 Zone lock authority state，保存冻结合同要求的 actor/action/Zone/Hurtbox identity、选择证据和 revisions。
- [ ] 只接受 T3 在同一 evaluation snapshot 上产生的有效结果。
- [ ] target/action 改变、Zone 失效、revision mismatch、实体销毁和显式 Planner reset 按合同清除 lock。
- [ ] 普通保持不推进无关 revision；真正影响未来命令的变化必须推进并进入 hash。

### 3.2 Snapshot、Hash 与版本兼容

- [ ] 将 Zone lock 纳入 Planner state snapshot 和 formal network snapshot，按现有规则更新 schema。
- [ ] capture/apply/deep clone 覆盖全部字段，无共享可变引用。
- [ ] full、owned、non-owned hash 按 T0 ownership 决策覆盖 lock；不能只覆盖 full hash。
- [ ] 旧 schema 显式归一化为“无 lock”或冻结的兼容状态，不读取未定义字段，不伪造合法 lock。
- [ ] serialization/message codec/loopback 如受影响，必须同步版本和测试。

### 3.3 Rollback 与 Replay

- [ ] rollback checkpoint 捕获并恢复 lock、revisions 和选择证据。
- [ ] replay capture/apply 保持同样语义；stale lock 不得在重演时复活。
- [ ] 分支回滚后下一 tick 的 selected Zone/Hurtbox、switch reason、Attack Opportunity、PrimaryAttack command 和 hash 与原分支一致。
- [ ] 重复 apply、空 lock、合法稀疏状态和损坏 payload 都有明确行为。

## 4. 自动化测试

计划由 `CharacterTestCharacterDistanceM2AuthorityStateTests` 和仅在 codec/loopback 实际受影响时的 network focused 类承载本任务。执行时分别记录 authority、network、rollback/replay filter，避免用一个聚合通过数掩盖某条链未运行。

- [ ] authority state 的默认、设置、保持、替换、清除和 revision 推进。
- [ ] snapshot capture/apply/deep clone 的字段完整性和引用隔离。
- [ ] full/owned/non-owned hash 对每个权威字段的敏感性和无关字段的不敏感性。
- [ ] 当前 schema round-trip 与所有受支持旧 schema 的显式归一化。
- [ ] rollback/replay 前后 Zone、Hurtbox、earliest tick、switch reason、query result 和下一 tick command parity。
- [ ] target/part invalidation、binding revision 和 Zone aggregate revision 变化不会恢复过期 lock。
- [ ] 网络 loopback/codec 如受影响则覆盖当前与旧 schema；未知 schema fail closed。
- [ ] warmed 逐帧 selection continuation/authority 消费不引入 GC allocation；formal snapshot capture/apply 沿用现有 `LowFreqAlloc` 合同并在交付记录中明确，不纳入逐帧计量窗口。

## 5. 非目标

- 不保存 CD-M3 Engagement Anchor 预约、路径或容量状态。
- 不改变 T3 的选择阈值或决胜规则；发现策略缺陷应返回 T3，而不是在 snapshot 层修正规则。
- 不实现 UI 或 Playable 最终展示。
- 不把仅诊断展示字段误纳入权威 hash，除非它会影响未来命令且 T0 已冻结。

## 6. 完成 Gate

- Zone lock 的 Planner/network/snapshot/hash/rollback/replay 全链测试通过。
- 当前和旧 schema 都有真实 round-trip/归一化证据，不能只断言字段存在。
- 真实分支回滚覆盖下一 tick 选择和攻击命令 parity。
- warmed 逐帧 authority 消费热路径 0 B GC；formal snapshot capture/apply 按 `LowFreqAlloc` 记录；Unity 完整脚本验证 Console 0 error。
- 实际交付记录、schema 变化和兼容限制完整后，T4 才能进入 `Verified`。

## 7. 交接给最终验收

T6 必须使用本任务的正式 authority/snapshot，而不是测试专用 lock 注入；Playable overlay 只读正式诊断投影，不成为第二份权威状态。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交 |
| 实际修改文件 | Planner state/command module、network snapshot/ownership state、`CharacterTestCharacterDistanceM2AuthorityStateTests.cs`、Playable rollback fixture |
| Authority tests | deep clone、每字段 hash sensitivity、canonical restore、invalid/duplicate fail closed、ownership exact 通过；包含在 M2 EditMode 22/22 |
| Snapshot / schema / hash | Planner schema 11；CharacterTest network schema 28；旧 `<28` 显式归一为空 lock；full/owned/non-owned 按 source entity 精确过滤 |
| Rollback / replay | PlayMode 正式 capture/apply 恢复 Zone/Hurtbox/reason；双方继续下一 tick 后真实 drained command 逐条一致，下一状态 lock 与 state hash 一致；M2 PlayMode 3/3（job `1354fdbb6e074c29a172d7eba712e941`） |
| Network loopback / codec | formal CharacterTest snapshot document 与现有 codec/hash 路径已接入；未单独运行多进程 transport loopback |
| NoAlloc | selector/aggregate 热路径 0 B；authority snapshot capture/apply 属现有 `LowFreqAlloc` 路径，未宣称 0 B |
| Unity 验证 | refresh/compile/domain reload/editor ready 通过，Console 0 error（最终 job `f51be42d052245e88ef17a8b8a80f081`） |
| 已知限制 / 阻断 | 无 T4 验收阻断；未另做多进程 transport loopback，formal snapshot document、schema 28、rollback continuation 与真实下一 tick command parity 已闭合 |
