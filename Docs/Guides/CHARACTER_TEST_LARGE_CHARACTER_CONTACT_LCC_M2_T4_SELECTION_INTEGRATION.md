# LCC-M2-T4：动态 DamageZone 与联合选择接入

> Status: Guide
>
> 工作包：LCC-M2-T4（生产实现）
>
> 状态权威：[LCC-M2 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M2_WOUND_BREAK.md)
>
> 执行文档状态：`Verified`（生产实现、主代理集成确认与 focused gate 已完成）
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
> 前置：T3 `Verified`。

## 1. 任务结果与非目标

DamageZone selector与Planner联合选择消费BodyPartState派生的动态优先级/Focus可见性，使用稳定迟滞与lock诊断；GeometryInReach、Approachable、root distance和pose-path cost语义不变。

非目标：不把不可接近Zone变可接近、不修改伤害累计、不实现Severed invalidation或网络序列化。

## 2. 写入所有权

- 冻结 Owner：`CharacterTestDamageZoneSelector.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`及专属dynamic-state adapter/diagnostics/tests。
- 实施中经主代理批准的最小扩界：
  - `CharacterTestRuntimeAiJointRuntime.cs`：仅在`CollectZones`调用专属投影并把effective priority/原有eligibility送入既有joint seed。发现依据是原实现直接把`zone.AuthoredPriority`写入`CharacterTestRuntimeAiJointZoneInput`，仅改`.Joint.cs`无法让joint selector看到相同候选。
  - `CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`：仅在既有`CreateEngagementZoneLock`写入`target.BodyPartState?.Revision ?? 0L`。该helper同时服务ordinary/joint engagement；若不在此处补revision，engagement路径会覆盖selector lock并丢失revision witness。
- 上述扩界不包含generator/reducer/comparer、root distance、GeometryInReach、Approachable、pose-path或通用Attack Opportunity语义。
- `CharacterTestAttackOpportunityService.cs`原则上只读；若需暴露已有事实，由本任务增加最小通用只读接口且不得改变root distance。
- Network lock snapshot/hash由T5写；T2/T3只读。

## 3. 实施清单

- [x] 从target snapshot读取稳定partState revision，不从View/telemetry字符串猜状态。
- [x] 计算authored priority + bounded dynamic modifier；只在现有AuthoredPriority比较位注入effective priority，完整comparer、迟滞和tie breaker不依赖容器顺序。
- [x] Open/Broken的Focus/hittable/aimable政策按定义；`Approachable=false`永不被动态状态提升。
- [x] 现有lock在state revision变化时重新验证；合法则按冻结迟滞保持/切换并输出typed reason。
- [x] joint action/Zone/Anchor选择看到同一动态候选，不创建UI/Planner私有副本。
- [x] Attack Opportunity仍区分GeometryInReach、legal Zone和pose-path cost；root XZ distance回归锁定。
- [x] 无definitions/Closed+Intact角色保持旧选择结果。

## 4. 验证与停止

tests覆盖priority边界、Focus显示、不可接近保持、lock保持/切换、state revision stale、候选顺序扰动、rollback输入预备、CD-M0～M4/root distance回归、NoAlloc。若只能通过改root distance或复制Zone真源实现，停止。回报选择链、diagnostics、tests/Unity和T5所需状态字段。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`；未commit、未stage、未clean |
| 实际修改文件 | 见5.1；未修改`CharacterTestAttackOpportunityService.cs`、T2/T3、network、UI、damage或Severed文件 |
| 测试与原始证据 | 实现代理 T4 focused `85b8ec8e2e4b4643b484842fe42175fe`：9/9 Passed；主代理复跑 `23c463c9f0074a7281c85bd9d597fb3a`：9/9 Passed。CD-M0～M4 focused `296564f5cd344f9ca997c3ab69263025`：141/141 Passed，主代理直接核验该 job summary。 |
| 集成 / 回滚结果 | 主代理检查最终diff、专属adapter、ordinary/joint接线与扩界理由后确认集成；回滚仅需撤回5.1列出的T4文件修改/新增，不涉及资产或持久化迁移。 |
| 状态历史 | T0 Production-Frozen；T3 Verified 后派发；实现代理完成生产与focused gate；2026-08-24主代理复跑focused/Unity并确认扩界最小后晋升 `Verified`。 |
| 执行者 / 审查者 | 执行：`/root/lcc_m2_t4_selection`；主代理完成集成确认；未增加独立评审轮次。 |
| dynamic selection / lock | target authority state投影、effective priority、typed revision reason、ordinary/joint lock revision witness已接线 |
| distance/opportunity regression | 既有root-XZ、GeometryInReach、legal Zone、pose-path与完整tie-breaker未改；141/141 focused回归通过 |
| NoAlloc / Unity | warmed 4096 selector + joint projection + joint generator：0 B；实现代理 Unity validator `d1df68edadeb48fba2a199a540bd1365`、主代理复跑 `71cee65f73ed4b869ea782d2125a553d` 均 Passed，refresh/compile/domain reload/editor ready/Console 0证据完整。 |
| 限制 | formal network snapshot/hash/rollback parity等待T5；CharacterStudio、Dragon标定和Playable验收等待T6 |

### 5.1 实际文件

- 修改：
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestDamageZoneSelector.cs`
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.Joint.cs`
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiJointRuntime.cs`
- 新增：
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestBodyPartDamageZoneSelectionState.cs`及`.meta`
  - `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestBodyPartDamageZoneSelectionTests.cs`及`.meta`
  - 本执行文档

### 5.2 落地合同

- 专属adapter只从`target.BodyPartState`的typed definition/current-entry read API读取，并记录state/source revision；读取期间revision变化、owner不匹配、无效状态或无效Zone均fail closed。
- 同一Zone可映射多个definition；按加法聚合Open/Broken authored delta，先用`long`累加，再稳定clamp到`[-1000, 1000]`，最后对`authored + delta`做`int`饱和，不受definition顺序影响。
- effective priority只替换既有comparer中的`AuthoredPriority`比较位；opportunity level、earliest tick、ZoneId、HurtboxId及迟滞阈值不变。
- `Hittable`、`Aimable`、`Approachable`严格透传authored Zone；动态状态不提升任一eligibility，尤其`Approachable=false`永不提升。
- `FocusVisible`仅当current state为Open且对应definition的`FocusConsumeTag`非空；Closed、Consumed、仅Broken及空tag均为false。Dragon14当前空tag不会虚构player-facing Focus。
- lock记录BodyPartState revision。revision变化时仍重评全部候选；合法旧lock继续沿用既有opportunity-level/earliest-tick迟滞，typed reason区分保持、切换和失效。
- independent selector与JointRuntime均复用同一投影；joint保留原AABB转换、generator/reducer/comparer和pose-path语义。
- 无definitions以及Closed+Intact的动态delta为0，保持旧结果；无效authority state则fail closed。

### 5.3 验证证据

- T4 focused EditMode：job `85b8ec8e2e4b4643b484842fe42175fe`，9/9 Passed。覆盖多entry顺序稳定、边界/clamp/溢出、Focus、eligibility不提升、legacy/invalid、revision保持/切换、selector/joint同源及warmed 4096 NoAlloc。
- 主代理复跑同一T4 focused类：job `23c463c9f0074a7281c85bd9d597fb3a`，9/9 Passed。
- CD-M0～M4相关selector/joint/root-distance focused回归：job `296564f5cd344f9ca997c3ab69263025`，141/141 Passed。
- Unity默认脚本门禁：job `d1df68edadeb48fba2a199a540bd1365`，Passed；refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count 0。仅见既有obsolete warnings。
- 主代理最终Unity门禁：首次调用在domain reload等待中断线，重连复跑job `71cee65f73ed4b869ea782d2125a553d` Passed；refresh/compile/domain reload/ready与Console 0均完整。
- API标准扫描：3444 first-party files，153个report-only既有finding；T4文件0 finding。
- `git diff --check`：通过。
- 早期编译job `2ab5dd39d32446bebe02193b845c9fc0`曾因测试named argument拼写错误失败；修正后由上述focused jobs和最终Unity门禁覆盖。

### 5.4 未执行项、风险与T5交接

- 按任务边界未执行全仓测试、PlayMode、逐任务独立评审、commit、stage或clean。
- T5必须把`BodyPartState`及`CharacterTestDamageZoneLock.BodyPartStateRevision`纳入formal snapshot、serialization/hash、rollback/replay parity；T4只提供typed字段和derived cache witness，不声称network verified。
- T6仍需决定/author Dragon的`FocusConsumeTag`、动态优先级标定，并完成CharacterStudio/Playable可见验收；空tag保持Focus不可见是当前明确合同。
- 共享工作区存在大量非T4修改；本任务未清理、覆盖或归属这些修改。
