# LCC-M1-T1：Presence Runtime body 与同步

> Status: Guide
>
> 工作包：LCC-M1-T1（生产实现）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Production-Frozen`（M1-T0 已 Verified，本文 owner/stop-check 已回写）
>
> 工作包派发状态：`Verified`
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
> 前置：T0 `Verified`。

## 1. 任务结果与非目标

CharacterTest 从 M0 Runtime binding 建立独立 Presence body/compound，按 root pose 确定性同步，具有稳定 collider identity、dirty gate 和 spawn/despawn/restore 重建。

非目标：不解算角色压力、不改 motion/motor、不持久化派生世界 shape、不接伤害/Hurtbox/navigation。

## 2. 写入所有权

- Owner：`CharacterTestCharacterPhysicsRuntime.cs` 的 layer/mask/body checked identity、body-local collider ordinal 与 sync-state；新增 `CharacterTestCharacterBodyPresenceSystem.cs` 的 binding projection、compound upsert/remove、dirty/refit/restore rebuild；`CharacterTestCharacterMotionSystem.cs` 仅拥有字段、`SetPhysicsRuntime/Clear` 生命周期和 final-root 后 sync 调用点；新增 `CharacterTestLargeCharacterPresenceRuntimeTests.cs`。
- 可调用 CombatPhysics compound API；不得修改 M0 separation 实现。
- 只读：`CharacterTestCharacterContactResolver.cs` 的 contact solver 数学/velocity/direct displacement、Network/Debug（T4）。共享 component registry 只由本任务增加 Presence entry。
- 不修改父状态和 Dragon14 作者/生成资产。

## 3. 实施清单

- [x] 使用 layer `11`；在任何 world mutation 前以 checked arithmetic/真实上界证明 `100000 + entityId` body 不碰撞 Hard Root、Hurtbox、movable、static 范围，不能证明即停止。collider key 是 `(bodyId, colliderId)`，同一 Presence body 内只用稳定 `1..4` ordinal，禁止另造全局 collider registry。
- [x] spawn/import 时验证 binding/revision/capacity，建立 fixed-capacity runtime view；每角色最多 4 leaf、8 角色 envelope 最多 32 active leaf；Missing 为零 body。
- [x] world shape 只由 root position/Yaw + ModelRoot local pose 派生；scale/Animator/bone Transform 不参与。
- [x] root pose或binding revision未变时跳过无效 upsert/refit；变化时按 `volumeId` ordinal 更新 compound。
- [x] disabled/zero-pressure volume 的身份与是否入 compound 按 M0 合同；溢出/future/stale fail closed。
- [x] despawn 清理 body；restore 清派生 cache并从 snapshot root+binding重建，不依赖旧 compound 历史。
- [x] layer masks 断言 Presence 不进入 MotionObstacle、damage、projectile、Hurtbox 或 navigation query。

## 4. 验证与证据

- focused tests 覆盖所有 shape、root 平移/Yaw、dirty/no-change、stable id、spawn/despawn/restore、缺失/非法 binding 和 layer 隔离。
- 顺序扰动生成相同 compound identity；steady unchanged sync warmed 0 B。
- Unity 脚本验证证据完整；不以 debug shape 存在证明 contact 已生效。
- 独立复核重点：第二真源、restore cache、mask 泄漏、每 tick 动态扩容。

## 5. 停止条件与派发

若需要持久化 world shapes、读取 Unity Transform/Animator 或把 Presence 加入硬障碍 mask，停止。回报 lifecycle、identity、dirty gate、tests/Unity 和风险。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` 工作树，未提交 |
| 实际修改文件 | `CharacterTestCharacterPhysicsRuntime.cs`、新增 `CharacterTestCharacterBodyPresenceSystem.cs` 及 `.meta`、`CharacterTestCharacterMotionSystem.cs`、新增 `CharacterTestLargeCharacterPresenceRuntimeTests.cs` 及 `.meta`、本文执行记录 |
| 测试与原始证据 | 实现者 job `6864b730dfd94a85b39b3dcd58f43b3c` 与主代理集成复跑 job `d722f99239b64e3e8d047ae2f8f726b5` 均执行 Unity EditMode filter `MxFramework.Tests.Game.CharacterTest.CharacterTestLargeCharacterPresenceRuntimeTests`；最终 6 passed / 0 failed / 0 skipped，0.2714972 s |
| 集成 / 回滚结果 | 未提交；Presence 保持派生层且未改 contact/network/direct displacement。回滚仅需撤销上述 3 个 production 文件、删除新增 system/test 的 `.meta`，并撤销本文记录 |
| 状态历史 | 2026-08-24：`In Progress` → `Verified`；主代理核对 owner/diff、运行时 fail-closed 边界与最终磁盘源码后复跑验证 |
| 执行者 / 审查者 | 子代理实现；主代理完成集成核对。按“避免过度评审”约束，本任务不另开独立审查，M1 仅保留 T5 一次 P0/P1 终审 |
| Presence body / sync | layer 11；checked body `100000 + entityId`，支持 active roster entity `1..29999`；collider 为同 body 内稳定 ordinal `1..4`；root XYZ/Yaw + binding revision/hash/leaf-count witness 未变时跳过 world mutation；8×4/32 固定 envelope |
| lifecycle / layer tests | 覆盖 all shapes、disabled ordinal/zero-pressure leaf、spawn/despawn/missing、restore rebuild、顺序扰动 identity、mask 隔离，以及 invalid/future/stale/capacity/第 9 角色/active entity 30000/foreign body occupancy 整子步 fail closed |
| Unity / NoAlloc | 实现者验证 job `13e1f277a9ba4fb4becd46c3fc70df3e`；主代理最终磁盘源码验证 job `86699338def347709b5b97d771a86979`：refresh/compile/domain reload/Editor ready 完整通过，Console 0 error；focused test 内 4096 次 warmed unchanged sync 断言 0 B，通过 |
| 限制 | 按任务边界未跑全仓回归、PlayMode 或完整路径 P95；T1 不接 pressure/contact，不声明软接触存在。上述项目分别留给 T4/T5 |
