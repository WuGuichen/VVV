# LCC-M4-T3：Fixed-point IK Authority、状态与网络

> Status: Guide
>
> 工作包：LCC-M4-T3（生产实现与formal network）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
>
> 执行文档状态：`Verified`（主代理交接门禁直接复核 terrain 6/6、network 6/6、replay 3/3、最小回归 6/6、Console 0、状态/schema/hash 与禁止项；T4 已串行派发）
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
> 前置：T2 `Verified`。

## 1. 任务结果与非目标

CharacterTest按冻结顺序执行四肢probe与fixed-point two-bone IK overlay，唯一生产fixed-capacity resolved authority pose frame，处理冲突、Character eligibility、binding/source stale、失效/恢复/迟滞；所有影响未来pose的状态进入下一可用schema与完整网络链。

非目标：不接Hurtbox/socket/contact/View（T4），不持久化可重算mid/tip，不读取Animator/Transform/Unity Physics。

## 2. 写入所有权

- Owner：`Game/CharacterTest/Combat/CharacterTestAuthorityPose.cs`及专属terrain IK component/system/state、resolved authority pose frame类型/buffer/revision/producer、component world/runtime module、formal network snapshot/hash/replay字段与focused tests。T4只读消费该frame，不得重新定义。
- 可复用`Character.RuntimeSpawn/CharacterAuthorityPose.cs`纯solver；算法缺陷以通用修复单独审查。
- T4消费者文件、T1 Authoring、T2 Combat API只读。

## 3. 实施清单

- [x] 从baked ModelRoot-local base channel计算tip-channel-local probe origin/axis，再以authority root position/Yaw恰好一次转world调用T2；将world target/normal以root Yaw逆变换恰好一次转回ModelRoot求解。
- [x] eligibility由确定性motion/action/posture/flying/dead/Severed state决定；不看View接触。
- [x] two-bone solve/reach clamp/ModelRoot-local pole退化、左右镜像、quantized target与deterministic orientation/basis全部fixed-point。
- [x] channel conflict按T0 validation/runtime fail closed；terrain/secondary组合顺序显式。
- [x] T3唯一判断motion/action/posture/flying/dead/Severed eligibility与binding/source stale；no-hit/too-steep/out-of-reach/disabled/stale逐步回到base，迟滞有界且不无限持旧target。
- [x] 生产resolved frame：frame/revision、definition/source revision，按channelId稳定排序的fixed-capacity samples，每项含ModelRoot-local position、orientation/basis、validity与source mask；root transform不重复烘入。
- [x] 现场取下一schema；definition/source revision、last-valid target/normal、miss history、current weight与surface witness等future-affecting字段全部capture/apply/clone/legacy migration，并进入full/owned/non-owned hash；当帧validity/failure保持derived。
- [x] spawn/restore从base开始、清派生cache；rollback+1 probe/resolved frame/pose/state/hash、replay/late join/future schema/source mismatch一致。
- [x] fixed capacity、diagnostics投影与4-limb solve warmed 0 B。

## 4. 验证与停止

tests覆盖space/root-Yaw单次变换、position+orientation、reachable/unreachable、degenerate pole、mirror、eligibility/stale、loss/recovery/hysteresis、Severed、spawn/restore、legacy/future/source revision、full/owned/non-owned hash、rollback/replay/late join、order/NoAlloc；Unity脚本验证。主代理只做状态完备性、迁移、hash ownership、parity与focused evidence交接门禁，不另开独立评审轮次。若未来pose依赖未持久化历史，必须正式入schema，不称派生cache；独立P0/P1终审只在T5进行。回报schema、state表、solver结果和parity。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享dirty worktree；已派发，禁止stage/commit/push |
| 实际修改文件 | 新建`CharacterTestAuthorityPose.TerrainIk.cs`、`CharacterTestTerrainIkStateComponent.cs`、`CharacterTestResolvedAuthorityPoseFrame.cs`、`CharacterTestAuthorityPoseTerrainSystem.cs`、`CharacterTestNetworkAuthorityPoseSnapshot.cs`及meta；partial修改`CharacterTestComponentWorld.cs`、`CharacterTestCharacterEntity.cs`、`CharacterTestCharacterRuntimeModule.cs`、`CharacterTestNetworkSnapshot.cs`；经主代理批准窄修`CharacterTestNetworkSessionFactory.cs`的geometry v6资源身份；新增terrain/network tests并增量修改replay `AuthorityPoseSchema32_*` |
| 测试与原始证据 | terrain job `38a0d82a1f7742089d686e269bcf1afc` 6/6；network最终job `3dca1b0c96f4465f9cc85141cef486c5` 6/6；replay job `85b99cf28bfd4eeb87360e9bff03247e` 3/3；最小既有AuthorityPose/network回归job `95fe8d93fffa45c69484fa637ad13457` 6/6；完整原证据见`Docs/Evidence/LCC/LCC-M4-T3/AUTHORITY_STATE_SCHEMA32_2026-08-25.md` |
| 集成 / 回滚结果 | 未stage/commit/push/clean；回滚只撤销本文列出的T3新增文件/meta及partial hunk，不触碰共享dirty state、T1/T2或T4 consumer |
| 状态历史 | `Audit-Frozen`→`Production-Frozen`→`In Progress`→`Implementation Complete / Handoff Ready`→`Verified`；主代理完成单次交接门禁后串行派发T4 |
| 执行者 / 审查者 | T3实现子代理完成owner、focused验证和Evidence；主代理直接查询四个最终job、Console与scoped diff后关闭交接门禁；T5负责唯一独立终审 |
| IK orchestration / state | 单一顺序为base→T2 probe→fixed-point two-bone terrain→non-conflicting secondary→resolved frame；最多4 limbs/32 samples；root/model world↔local各一次；root/mid按child+bend plane，tip按ground normal与冻结forward fallback；motion/action/posture/flying/launch/dead/Severed均读现有确定性authority source |
| 实际 schema / network | 现场最高31后占用32；保存last published revision、definition/base-source witness、stable limb identity、last-valid frame/target/normal、饱和miss count、surface witness/material与current weight；不保存derived current validity/failure或mid/tip/frame samples；capture/apply/clone、legacy 0～31（含补回遗漏的schema27支持）、future/envelope/source mismatch、full/owned/non-owned hash、rollback+1/replay/late join闭合 |
| determinism / NoAlloc | 四肢固定容量稳定channel顺序；warmed tick `GC.GetAllocatedBytesForCurrentThread()`断言0 B；13个changed C#路径standards report-only为0 finding，scoped `git diff --check`无输出 |
| Unity / 限制 | 唯一 integrated `validate_unity_scripts`在domain reload等待期间因session disconnect返回失败并保留原证据；按根AGENTS失败回退后scripts refresh、domain reload、Editor重连可执行测试且Console清理已记录的Test Runner临时条目后error=0。未跑全仓、PlayMode、`.csproj`、独立评审；Hurtbox/socket/contact/View consumers等待T4 |
