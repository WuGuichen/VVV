# CD-M3-T0：合同与现状审计

> Status: Guide
>
> 状态权威：[CD-M3 总控页第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M3_ENGAGEMENT_ANCHOR.md)
>
> 前置：CD-M2、PF-M4 `Verified`

## 1. 可交付结果

只读审计 Anchor 作者链、geometry binding、Zone runtime/lock、CandidatePose evaluator seam、CombatPhysics standing query、PF-M4 path sample、Planner authority/network 和 CharacterStudio transaction，冻结字段级合同及影响矩阵。

## 2. 执行清单

- [x] 核对 DamageZone v2 Authoring/Core/CLI/server/Compiler/Runtime/Studio 的实际文件与可复用事务。
- [x] 冻结 `engagement_anchors.json` schema version、Anchor/Zone ID、ModelRoot-local position mm、yaw millidegrees、capacity、priority、enabled/phase witness 字段。
- [x] 明确缺失文件、unknown schema、非法 Zone、重复 ID、非法容量/坐标/朝向的 typed 处理和写保护。
- [x] 核对 ModelRoot/world projection、source standing capsule、occupancy/reservation 检查与 CombatPhysics revision。
- [x] 核对 M1 evaluator 的 CandidatePose 输入 seam，确保只替换 source pose/facing，不复制 trace/damage 语义。
- [x] 冻结 Anchor/Zone/phase revision 与 graph/physics witness 的失效矩阵。
- [x] 冻结 reservation owner/key/capacity/lifetime、snapshot schema、canonical hash order、rollback/replay next-tick parity。
- [x] 给出 T1～T6 精确影响文件、测试入口、PlayMode/Studio fixture 和 NoAlloc 计量窗口，无 `TBD`。

## 3. 完成 Gate

- 字段、owner、生命周期、failure、migration、revision、authority 与兼容表完整。
- 不把计划名称当作已存在类型；每个复用点都有当前源码证据。
- 审计本身不修改生产代码；总控页记录后 T0 才可 `Verified`。

## 4. 实际记录

| 项目 | 当前记录 |
| --- | --- |
| 状态 | 见总控页；当前 `Verified` |
| 审计结论 | DamageZone v2 已存在 lossless read/write、canonical compiler、immutable Runtime binding、Studio transaction；M1 `CandidatePose` 是显式 typed unsupported seam；PF-M4 提供唯一 graph/session/A* owner，CD-M3 不需也不得复制寻路实现 |
| 字段 / 失效矩阵 | 已冻结，见第 5～7 节 |
| 影响文件 / 测试矩阵 | 已冻结，见第 8 节 |
| 限制 | T0 只修改任务文档；尚未提供 Anchor 生产能力。现有运行期没有独立 Boss phase controller，v1 只消费显式 phase witness，正式默认值为 `phase.default`，该边界必须保留在 T6 记录中 |

## 5. 冻结的 Authoring 与 binding 合同

唯一作者文件为 `geometry/engagement_anchors.json`：

- `format = mx.characterEngagementAnchors.v1`，`schemaVersion = 1.0`，`revision >= 1`。
- `anchors[]` 按 `anchorId` ordinal canonical sort；`anchorId` 在包内唯一，`zoneId` 必须引用当前 DamageZone 文档中 `approachable=true` 的 Zone。
- `referenceFrame` 只接受 `ModelRoot`；World、SkeletonRoot、Bone、Locator、BodyPart、Socket、leaf-Hurtbox-following 全部 typed reject。
- `localPositionMillimeters = {x,y,z}` 为整数且每轴位于 `[-10000000,10000000]`；`yawMilliDegrees` 使用 canonical `[0,359999]`。
- `capacity` 位于 `[1,64]`；`authoredPriority` 位于 `[-100000,100000]`；`enabled` 显式保存。
- `phaseId` 为空表示不限 phase；非空时是稳定 ID。正式现状由调用方提供 `phase.default + phaseRevision` witness，不从动画骨骼或 Hurtbox 推导。
- 缺失文件保持缺失，Compiler/Runtime 明确投影 `CompatibilityRootApproach`，不标脏、不自动补文件；首次真实 Studio 编辑才创建。
- unknown format/schema 与 malformed JSON 均保留 raw payload、阻断全部保存/编译覆盖入口；不提供隐式 migration。
- geometry binding 由 v2 升为 v3 并继续兼容读取 v1/v2；v3 是 Anchor 的唯一 Runtime artifact，包含 document revision、canonical Anchor 列表和独立 content hash witness。

## 6. 冻结的 CandidatePose、路径与失效合同

CandidatePose 绑定 `source/target/action/Zone/Anchor`，并携带 world position、world yaw、completed frame、source pose/facing、target pose、Zone、Anchor、phase、CombatPhysics、graph revision/hash witness。

- world pose 只按 sealed target ModelRoot translation/yaw 投影 Anchor local mm/millidegree；Hurtbox/骨骼动画不参与长期点位。
- standing 使用 source capsule 与 CombatPhysics；source 自身和目标实体 collider 可忽略，静态障碍及其它角色/可移动物体视为占用。地面/clearance 由同一 PF-M4 graph node 证明，不调用 Unity Physics。
- M1 query 增加 candidate plan/pose 输入，仍由同一 `CharacterTestAttackOpportunityService` 执行 trace、mask、blocker、damage qualification；CurrentPose/TurnInPlace 不变。
- path query 使用 PF-M4 session 的 graph/pathfinder/path buffer 和 PF-M3 penalty-inclusive total cost；`BudgetExceeded`、`Unreachable`、`StaleGraph`、endpoint/clearance/capacity failure 分别保留。
- 排序固定为：locked Zone、较高 authored priority、较低 path cost、较早 hit tick、ZoneId、HurtboxId、AnchorId；root distance仅诊断。

旧 CandidatePose 在下列任一 witness 变化后 stale 并清空旧 success：completed frame、source pose/facing、target ModelRoot pose、Zone/binding/Anchor document revision/hash、phase id/revision、CombatPhysics revision、graph revision/hash、action revision。

## 7. 冻结的 reservation authority 合同

- key：`(targetCombatEntityId, zoneId, anchorId)`；owner：`sourceEntityId`；值还包含 action revision、capacity slot、acquired/updated frame 及全部 Zone/Anchor/phase/graph witness。
- Planner authority 按 source entity ID 升序处理同 tick proposal；同 key 的 slot 也按 source ID canonical，禁止依赖 Dictionary/模块注册顺序。
- target inactive/dead、owner/action 失效、Zone/Anchor/phase/config revision 改变、capacity 缩小、runtime reset 均以 typed release reason 释放；同 source 重选为 update，切换 key 先释放旧值再申请。
- reservation 进入 Planner snapshot schema 12 与 CharacterTest network schema 29，并进入 full、owned、non-owned partition hash；capture/apply 为 `LowFreqAlloc`。
- CandidatePose/path sample 仍为当前帧派生数据，不序列化；rollback 先清空派生 cache，再恢复 reservation/lock，并以真实下一 tick命令 parity 验收。

## 8. 精确影响与验证矩阵

| 工作包 | 生产文件 | 测试 / 证据 |
| --- | --- | --- |
| T1 | `CharacterAuthoringGeometry.cs`、`CharacterPackageValidation.cs`、`CharacterPackageCommands.cs`、`EditorServer.cs`、`CharacterAuthoringCompiler.cs`、`CharacterRuntimeSpawnContracts.cs`、`CharacterImportedPackageJson.cs` | Authoring DamageZone 邻接 tests 扩展；RuntimeSpawn geometry v1/v2/v3 compatibility tests；Unity compile |
| T2～T3 | 新增 `CharacterTestEngagementPlanner.cs`；扩展 `CharacterTestAttackOpportunityService.cs`、Planner adapter/evaluator、Hurtbox/Runtime module | 新增 `CharacterTestCharacterDistanceM3Tests.cs`；M1/M2/PF-M4 focused regression；warmed candidate/path selection 计量 |
| T4 | Planner adapter/command module/state snapshot、`CharacterTestNetworkSnapshot.cs` | 新增 M3 authority/network tests；rollback next-tick command parity；owned/non-owned hash isolation |
| T5 | `CharacterStudio/web/src/inspector.js`、`styles.css`、`scripts/smoke.mjs` 与 editor-server 保存事务 | `npm run smoke`；标准/窄视口、dirty refresh、unknown read-only、save/reopen 直接浏览器证据 |
| T6 | 000003 作者 fixture、生成 geometry binding、Runtime AI Lab diagnostics、接口/台账 | EditMode/PlayMode/Game View、多 AI capacity、NoAlloc、文档健康、P0/P1 终审 |
