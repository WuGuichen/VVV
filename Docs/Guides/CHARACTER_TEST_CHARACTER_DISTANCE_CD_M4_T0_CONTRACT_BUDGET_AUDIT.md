# CD-M4-T0：合同、预算与现状审计

> Status: Guide
>
> 状态权威：[CD-M4 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M4_JOINT_SELECTION.md)
>
> 前置：CD-M3 `Verified`

## 1. 可交付结果

只读核对 target acquisition、ready/resolvable action、Attack Opportunity/Zone/Anchor inputs、action utility、Planner budget/cache/diagnostics，冻结联合候选字段、完整排序、预算计数与校准场景。

## 2. 执行清单

- [x] 记录当前 target selection、action resolver/readiness/utility 和 M1～M3 evaluator/lock/reservation 的实际调用链。
- [x] 冻结 actor/action/zone candidate DTO、eligibility、compatibility source 和 stable IDs。
- [x] 冻结 aggregate broadphase union、explicit intersection、root-distance soft rank 与 Top-K 截断顺序。
- [x] 冻结 query budget 的计数单位：actor aggregate、action-zone broadphase、leaf PreviewTrace、CandidatePose/path query 分别如何计数。
- [x] 冻结完整排序方向、缺失值/failure 排名和 `CandidateSetTruncated` 语义。
- [x] 冻结 Trace Plan/result cache key、capacity、replacement、frame lifecycle、revision invalidation 和 allocation ownership。
- [x] 判断最终 selection 是否形成跨帧 authority；若是，冻结 snapshot/hash/rollback；若否，记录每帧重算依据。
- [x] 定义代表性校准场景、峰值统计、顺序扰动和 2 倍余量计算，无 `TBD`。

## 3. 已核对的现状调用链

### 3.1 当前 Planner 顺序及缺口

当前生产顺序是：

1. `CharacterTestRuntimeAiPlannerCommandModule.Tick()` 为本帧启动 `CharacterTestPerceptionFrameCache`；
2. `CharacterTestRuntimeAiPlannerEvaluator.TryEvaluate()` 的 `Sense()` 调用 `TrySelectTarget()`；
3. `TrySelectNearestOpposingTeamTarget()` 以根节点平方距离选择一个 opposing-team actor，等距时会继承 perception/container 的首次出现顺序；
4. 只对这个 target 写 facts/navigation，再由 `TrySelectAction()` 在 compiled Planner action 中按 `Score` 高、`Cost` 低、`ActionId` 小选 action；
5. `PrimaryAttack`/`MoveToTarget` 才通过 `_primaryAttackEligibility` 调用 `CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.EvaluatePlannerPrimaryAttack()`；
6. CommandModule 对最终 target 再调用同一个 adapter，决定 attack、TurnInPlace 或 Engagement world move。

因此当前链是“先最近 target、再 action/opportunity”，且 nearest tie 不是 canonical stable-ID tie；这正是 T1～T3 要替换的语义。

### 3.2 action、M1～M3 与跨帧状态

- PrimaryAttack 的实际 CharacterAction 不是 Planner compiled `ActionId`。adapter 通过 `LightAttackIntentId + AttackPrimary` 调用 `ResolveIntent()`，得到最终 `CharacterActionConfig.Id/StableId`，并以 character/equipment/action-set binding signature 缓存静态解析。
- 同一个 AttackPrimary command 会再次按相同 intent 解析。因此 CD-M4 只能把这个最终 resolved PrimaryAttack 作为 evaluator-backed basic action；不得把 action-set 中未被该 command 最终解析的低优先级 binding 伪装成可提交 action。
- `CastAbilitySlot` 当前只有 Planner readiness/conditions 与 legacy range command 语义，没有 M1 evaluator；它只能产生 `CompatibilityLegacyRange` 候选，`ZoneId/AnchorId/path/PreviewTrace` 必须为空且 UI 必须明确 compatibility。
- M1/M2/M3 调用链已集中在 `CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.TryEvaluatePrimaryAttack()`：采集上一 completed frame snapshot、构建 CurrentPose/TurnInPlace Trace Plan、按 DamageZone 选择、必要时遍历 Engagement Anchors、生成 CandidatePose、运行 PreviewTrace 与 PF-M4 path，并产生 opportunity/zone/anchor/path witness。
- 该方法目前在单目标评估过程中直接写 `_zoneLocksBySource`、`_engagementSelectionsBySource` 和 reservation authority。CD-M4 多候选预览必须拆成无副作用 `Preview` 与胜者一次性 `Commit`；循环调用现方法并在循环中写 lock/reservation 是禁止实现。
- Zone lock 与 Engagement reservation 已经是 formal snapshot/hash/rollback/replay authority；其 schema 保持不变。

## 4. 冻结的候选合同

### 4.1 Canonical DTO 与 identity

每个 evaluator-backed joint candidate 至少携带：

- `SourceEntityId`、`TargetEntityId`（target stable ID 是正整数 runtime entity ID）；
- `PlannerActionId`、`ResolvedActionId`、`ResolvedActionStableId`；
- `ZoneId`；有 Approach 时再带 `AnchorId`；
- source/target life 与 valid flags、relation、aggregate-intersection flag、root-distance-mm-squared；
- `OpportunityStatus/Level/Mode`、CurrentPose/TurnInPlace/Approach flags；
- 整数 `ActionUtilityMilli`、`PathCostMillimeters`、`EarliestHitTick`、`ZonePriority`；
- hurtbox/sample/witness、failure、compatibility、truncation 与 cache evidence。

Canonical candidate ID 是 ordinal tuple：

```text
(TargetEntityId,
 ResolvedActionStableId,
 ZoneId-or-empty,
 AnchorId-or-empty,
 PlannerActionId)
```

字符串均用 `StringComparison.Ordinal`，不得用本地化、display name、数组索引、哈希桶或遍历顺序决胜。`ActionUtilityMilli = round-away-from-zero(Score * 1000)` 在 compiled profile 建立时一次计算；比较路径不使用 float。path cost 统一毫米，缺失 path 为 `int.MaxValue`；缺失 earliest tick 为 `int.MaxValue`；缺失 Zone priority 为 `int.MinValue`。

### 4.2 eligibility 与 compatibility

Actor 在进入 Top-K 前必须：非 null、非 self、opposing team、`LifeState == Alive`、具有本帧 current entity/geometry sample。dead/dying/reviving、friendly、self、stale/missing snapshot 只计 rejection，不生成 selectable candidate。

Action 必须通过 active HFSM path/既有条件、冷却/资源/ability slot 可解析性以及 command 可提交性。支持矩阵冻结为：

| action | 候选来源 | 可填 Zone/Anchor/Preview/path | selectable 条件 |
| --- | --- | --- | --- |
| resolved PrimaryAttack | `EvaluatorBacked` | 是 | final resolve 成功、BasicAttack、M1 支持 |
| MoveToTarget | `EvaluatorBackedApproach` | 复用同一 PrimaryAttack 的 target/Zone/Anchor | 对同一 resolved PrimaryAttack 得到 `RequiresTranslation` 且 Planner 条件满足 |
| CastAbilitySlot | `CompatibilityLegacyRange` | 否 | ability slot 可解析且既有 Planner 条件满足 |
| Idle/Dodge/无目标动作 | 非 joint action | 否 | 继续由既有 Planner fallback 处理，不进入 target/action/zone 集合 |

Compatibility candidate 永远排在任一 `Status == Valid` 的 evaluator-backed candidate 之后；它不能声称 `CurrentPose`、`TurnInPlace`、`ApproachPose`、Zone、Anchor 或 path evidence。若只有 compatibility 候选，Planner 可保持原行为并在诊断中显式标记来源。

### 4.3 conservative generation 与 Top-K

1. 对每个 ready evaluator-backed action 的全部 Trace Plan leaf bounds 求 conservative world envelope；对 ready action envelopes 作 actor aggregate union。
2. actor broadphase 返回只是输入集合。对每个 actor 的 DamageZone aggregate bounds 做明确 AABB intersection；“至少一个 Zone 明确相交”的 actor rank 高于“不相交但仍在 soft search radius”的 actor。
3. actor Top-K 顺序冻结为：explicit-intersection 降序、root-distance-mm-squared 升序、`TargetEntityId` 升序。根距离只是软排序，不是攻击射程或命中证明。
4. 进入 Top-K 后，对每个 ready action 与每个合法 `Hittable` Zone 做 action-envelope/Zone AABB conservative test，再建立 canonical `(target, action, zone)`。Approach 必须额外要求 `Approachable` Zone。
5. 所有输入先 canonical insertion/sort；任何达到上限的阶段都停止写入、保留已按上述 canonical 顺序确定的前缀并设置 typed truncation。不得通过先到先得截断。

## 5. 冻结的完整 comparator

候选从优到劣逐字段比较；只有字段相同才进入下一项：

1. `CandidateStatusRank`：Evaluator `Valid` > `CompatibilityLegacyRange`；Unavailable/Unsupported/Stale/invalid 不 selectable；
2. `TargetLifeRank`：Alive；其他 life state 已在 generation 排除；字段仍保留用于诊断/防御性比较；
3. `CurrentPose` true 优先；
4. `TurnInPlace` true 优先；
5. 合法 `ApproachPose` true 优先；
6. `ActionUtilityMilli` 大者优先；
7. `PathCostMillimeters` 小者优先；
8. `EarliestHitTick` 小者优先；
9. `ZonePriority` 大者优先；
10. `TargetEntityId` 小者优先；
11. `ResolvedActionStableId` ordinal 小者优先；
12. `ZoneId` ordinal 小者优先；
13. `AnchorId` ordinal 小者优先；
14. `PlannerActionId` 小者优先。

`RequiresTranslation` 但没有已验证 CandidatePose/path 的候选不具有 `ApproachPose` rank；`Valid/NoOpportunity` 可保留诊断但不可胜过任何可提交 compatibility candidate。最终没有 selectable candidate 时返回 typed no-selection，而不是回退到最近目标并伪装成 joint result。

## 6. 预算与 typed truncation

T1～T3 先使用以下临时 profile defaults；T4 必须用代表性零截断峰值重新计算 production defaults。硬存储上限固定为临时值的 4 倍，配置超过硬上限必须验证失败，不得运行期扩容。

| budget | 一次计数 | 临时默认 | 硬上限 |
| --- | --- | ---: | ---: |
| actor scan | 检查一个非 self entity | 64 | 256 |
| actor Top-K | 保留一个 canonical actor | 16 | 64 |
| ready joint actions | 一个通过静态解析和 readiness 的 action | 16 | 64 |
| action-zone broadphase | 一个 `(target, action, zone)` AABB test | 256 | 1024 |
| joint candidate buffer | 写入一个 canonical candidate | 256 | 1024 |
| leaf PreviewTrace | evaluator 实际评估一个 Trace Plan leaf/sample | 512 | 2048 |
| CandidatePose preview | 实际评估一个 `(candidate, anchor)` pose/trace | 128 | 512 |
| path query | 实际调用一次 PF-M4 path | 128 | 512 |

`CandidateSetTruncated` 是总标志，同时携带首个耗尽的 `Phase`、`ObservedCount`、`AcceptedCount`、`Limit`；后续阶段不得把它清除。phase 枚举固定为 ActorScan、ActorTopK、ReadyAction、ActionZoneBroadphase、JointCandidate、LeafPreviewTrace、CandidatePose、PathQuery、TracePlanCache、ResultCache。截断表示“预算内前缀的最佳结果”，不表示预算外没有更优候选；只有 `CandidateSetTruncated == false` 时，no-selection 才能解释为完整有限集无可选项。

## 7. 两层 cache 合同

### 7.1 Trace Plan cache

- 临时/最低 production capacity：128 entries，硬上限 512。
- key/witness：source entity、resolved action ID/stable revision、pose kind、source position/yaw、completed simulation frame、physics world revision、source pose/facing revision、action-set/equipment/binding revision、animation/clip/root-motion/trace-policy revision。
- value：只读 plan view + build status/counts；owner 是 selector/adapter 预分配 plan buffers，调用方不得保存跨 reset 的 view。

### 7.2 result cache

- 临时/最低 production capacity：512 entries，硬上限 2048。
- key/witness：完整 Trace Plan key，加 target entity、target hurtbox/zone/binding revision、mode、ZoneId、AnchorId、candidate pose、lock witness、reservation revision/capacity witness、navigation graph revision/content hash、physics revision、world-target-classification frame/revision。
- value：完整 opportunity/pose/path/failure evidence；cache hit 必须逐字段等于 cold evaluation。

两层均为固定数组 + deterministic clock replacement：本帧 `BeginFrame` 将 cursor 置 0，但不分配；同 key 命中，否则从 cursor 顺序覆盖，cursor 到 capacity 后回绕。任何 witness 不同即 miss；rollback/replay、profile/hot update、navigation session rebuild、world revision 后不得命中旧 entry。overflow 不扩容，设置相应 cache phase truncation。diagnostic snapshot 只复制当前结果到预分配/值类型展示结构，不触发查询或 cache mutation。warmed candidate generation、reduction、cache hit/miss 和 diagnostics capture 均要求 0 B。

## 8. Authority 决策

最终 joint selection **不新增跨帧 authority**：它仅由上一 completed-frame formal snapshot、当前 Planner profile、ready state 和已存在的 Zone lock/Engagement reservation authority 每 tick 纯重算，并只影响本 tick 将提交的 command。selector 不保存 target stickiness、候选历史或未来 command。

胜者确定后才允许 adapter 一次性 commit 现有 Zone lock/reservation；未胜候选预览无副作用。既有 command/action/motion authority、DamageZone lock 与 Engagement reservation snapshot/hash/rollback/replay 保持权威。rollback 时清空两层派生 cache/diagnostics；重放下一 tick 必须得到相同 joint result、command 和 formal hash。snapshot 中的 `HurtboxRevision/WorldRevision/CombatPhysicsRevision` 是本地 freshness witness，不进入跨分支 formal hash；binding、zone、body-part、anchor、graph 等语义 witness 仍参与 hash。只要实现不引入跨帧选择记忆，就不增加 network schema；若后续产品要求 target stickiness，必须另开 schema/authority 变更，不能暗中加入。

## 9. T4 校准矩阵与公式

固定 seed `42004`，每个 fixture 预热 120 ticks、采样 600 ticks，并在每个 fixture 运行原序、reverse actor、reverse action、reverse Zone、deterministic shuffled broadphase 五种输入顺序：

| fixture | 规模与覆盖 |
| --- | --- |
| ordinary | 1 AI + 4 alive opponents；1 resolved basic + 1 compatibility ability；单 Zone/无阻挡 |
| boss-zones | 4 AI + 1 Dragon14；6 DamageZones、6 Anchors；Current/Turn/Approach 混合 |
| multi-ai | 16 AI + 16 opponents；每 actor 4 ready joint actions、4 Zones；共享 graph |
| weapon-mix | 短剑/长枪 source profile 各 1，4 targets；近目标短剑不可达、较远目标长枪可达 |
| blocked-unreachable | 8 targets；environment blocker、invalid leaf、不可达 graph、capacity-full reservation 混合 |
| compatibility | 4 targets；evaluator-backed basic 不可用、可解析 ranged ability compatibility，验证不伪造 Zone/path |

每项记录五种顺序中零截断运行的最大 observed count。production default 公式为 `min(hardLimit, max(1, 2 * peak))`；若 `2 * peak > hardLimit`，T4 必须失败并提高硬上限/存储，不能以截断场景冒充代表性成功。压力 fixture 使用 `default + 1` 和 `hardLimit + 1` 两个边界，证明 canonical typed truncation、0 B、无异常。机器耗时只作辅证，不参与预算公式。

## 10. 完成 Gate

- 字段、排序、预算、cache、authority、compatibility、diagnostics 和 test matrix 完整。
- T1～T5 可依据审计直接实施；审计不修改生产代码。
- 总控页记录后 T0 才可 `Verified`。

## 11. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 审计结论 | 当前是 nearest-target-first；PrimaryAttack evaluator 解析与 Planner action ID 分属不同 identity；多候选必须 Preview/Commit 分离 |
| 排序 / 预算 / cache | DTO、14 级整数 comparator、8 类工作量预算、两层 key/capacity/replacement/lifecycle 已冻结 |
| 校准矩阵 | seed 42004；6 fixtures；120 tick warmup + 600 tick sample；5 类顺序扰动；production default = 2 倍零截断峰值 |
| authority | joint result 每 tick 派生；不新增 schema；只在胜者后提交既有 Zone lock/reservation authority |
| 限制 | 本任务只读、未提供联合选择能力；临时 defaults 不得作为 T4 已校准 production 数字 |
