# LCC-M4-T2：CombatPhysics 确定性地形探针

> Status: Guide
>
> 工作包：LCC-M4-T2（生产实现）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
>
> 执行文档状态：`Verified`（主代理交接门禁闭合；focused 15/15；fallback Unity gate green；integrated首次失败原证据保留）
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
> 前置：T1 `Verified`。

## 1. 任务结果与非目标

CombatPhysics提供noEngine fixed-point static ground-probe API，从调用者给定的world origin/direction/shape选择稳定surface hit，并量化world point/normal、surface body/collider/leaf identity与material；只返回通用几何status供T3消费。

非目标：不判断Character motion/action/posture/Severed eligibility，不判断binding/source stale，不求IK、不保存Character状态、不读取Unity Terrain/Raycast/Collider、不触发脚步音频。

## 2. 写入所有权

- Owner：`Combat/Physics/CombatPhysicsWorld.Queries.cs`、Narrowphase/Sorting中通用static probe职责、通用result/comparer和Combat tests。
- Scene bridge只读；T0已冻结先以纯Combat测试证明static layer排除与material精确回查。确有缺口时只做最小通用Combat修补，由主代理门禁并留到T5一次终审。
- CharacterTest/RuntimeSpawn/UnityView/Network禁止写。

## 3. 实施清单

- [x] 输入使用Fix64 origin/direction/distance/radius/layer/slope；验证向下单位方向、T1一致100m边界与raw endpoint安全。
- [x] 只消费caller static mask；测试证明layer 0/1可命中且8/9/10/11排除。
- [x] single-best按完整stable comparer选择；结果携带surface body/collider/compound leaf/shape identity与精确material。
- [x] point/distance 1mm、normal signed 1/32767后重归一；覆盖相切、台阶边、同距、注册顺序与退化。
- [x] 只返回通用几何reason；失败`HasHit=false`，不返回Character eligibility或`Stale`。
- [x] single-best warmed hot path 4096次实测0 B；无diagnostic字符串。
- [x] 复用现有Combat broadphase与shape真源；非零radius为解析sphere cast，无Unity/浮点旁路。

## 4. 验证与停止

Combat tests覆盖平面、上下坡、台阶边、no hit、too steep、多surface同距、identity/material、顺序扰动、quantization、fixed-point重演、warmed 0 B和旧query回归；Unity脚本验证。主代理只做space、normal、comparer、退化、NoAlloc与focused evidence交接门禁，不另开独立评审轮次。若static geometry缺少法线合同，先做最小通用Combat修补，不借Unity Physics；独立P0/P1终审只在T5进行。回报comparer、量化、tests/perf和限制。

## 5. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main`共享dirty worktree；已派发，禁止stage/commit/push |
| 实际修改文件 | `CombatGroundProbe.cs`、`CombatPhysicsWorld.GroundProbes.cs`、`CombatGroundProbeTests.cs`及meta；`Docs/Interfaces/Combat.md`公开合同增量；`Docs/Evidence/LCC/LCC-M4-T2/COMBAT_PROBE_2026-08-25.md` |
| 测试与原始证据 | focused EditMode job `08b703c9b5ef478897ed5206a004db21`：15/15 passed、0 failed/skipped、0.4972615s；完整原证据见Evidence |
| 集成 / 回滚结果 | 未stage/commit/push；回滚仅删除T2生产/测试文件及meta并撤销Combat接口T2段落，不触碰共享dirty state |
| 状态历史 | `Audit-Frozen`→`Production-Frozen`→`In Progress`→`Implementation Complete / Handoff Ready`；主代理关闭轴向TOI、无来源边界与非零radius OBB证据缺口后晋升`Verified` |
| 执行者 / 审查者 | T2实现子代理完成并按原owner关闭主门禁缺口；主代理核对源码、15/15 job、0 B、3文件代码规范0问题、分步Unity gate与Console0后通过；T5负责唯一独立终审 |
| probe API / stable hit | single-best Ray/解析sphere cast；AABB/sphere/capsule/OBB；轴向首次接触distance、target surface point/normal；primitive/compound精确material lookup；非零radius斜坡OBB覆盖distance/point/normal/slope、镜像与重演稳定 |
| quantization / diagnostics | point/distance 1mm；normal signed 1/32767后重归一；typed failure且失败不伪造target；无hot-path字符串 |
| NoAlloc / Unity | warmed 4096次0 B；唯一integrated validator job `8cc4a8ead0f74269a473b95b40f62290`首轮CS0177失败后机械修正且未重跑；分步scripts refresh/compile、domain reload、Editor idle、15/15成立；按授权清除Test Runner临时保存结果条目后Console error=0，fallback Unity gate green |
| 限制 | caller负责static mask语义；不接Character eligibility/IK/stale/state；未跑全仓/PlayMode/Network/父里程碑测试 |
