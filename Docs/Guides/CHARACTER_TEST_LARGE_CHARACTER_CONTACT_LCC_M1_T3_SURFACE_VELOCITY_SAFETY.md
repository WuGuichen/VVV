# LCC-M1-T3：表面速度、推力与 Motor 安全

> Status: Guide
>
> 工作包：LCC-M1-T3（生产实现）
>
> 状态权威：[LCC-M1 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M1_BODY_PRESENCE.md)
>
> 执行文档状态：`Production-Frozen`（M1-T0～T2 已 Verified；owner 与参数来源边界已解除）
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
> 前置：T2 `Verified`。

## 1. 任务结果与非目标

Presence pressure 同时考虑 root 平移与 Yaw 旋转产生的表面速度、质量/可推动性和有界 clamp；本 tick 只提交 contact velocity，由下一次既有 kinematic motor 经 static obstacle sweep 消费。墙夹时允许有限软重叠但不穿墙。

非目标：不把软体积变硬阻挡、不用无障碍直接 displacement、不新增 Unity Rigidbody/Physics、不改网络 schema（T4）。

## 2. 写入所有权

- Owner：`CharacterTestCharacterMotionSystem.cs` 的 same-tick previous/current XYZ/Yaw/binding witness 预分配 scratch capture/clear；`CharacterTestCharacterContactResolver.cs` 仅拥有 surface velocity、mass/pushability、clamp 与 wall-limited output partial；新增 `CharacterTestLargeCharacterPresenceMotionSafetyTests.cs`。
- T2 已冻结 candidate/key/overflow/三轮 solver partial；缺陷回派原 owner。
- Network snapshot/hash 文件由 T4 写；本任务只定义需持久化字段清单。

## 3. 实施清单

- [x] 在同一 motion tick 捕获 previous/current root position与Yaw；scratch 不跨 tick 持久化，首帧、teleport、spawn/restore或 binding switch 缺少合法角速度基线时 rotation contribution 为 0。
- [x] 在 contact point 计算线速度 + `omega × radius` 的 fixed-point水平表面速度；不读取输入意图代替实际 root motion。
- [x] 应用 mass、pushability、dead/immovable、pressureScale和成对分配；参数均有界且确定性。
- [x] clamp 单帧速度/加速度/penetration bias；退化/超限返回 typed diagnostic。
- [x] clamp 必须取自现有 movement/profile 上限或先提交主代理决策；禁止临时猜测新常量。若需要跨 tick pose/hysteresis/history，停止，不得私占 schema 30。
- [x] 把结果组合进本 tick 的 motor contact velocity；由下一次 motion tick 的既有 static obstacle sweep/step/slope 消费。Presence-originated path 禁止调用 `ApplyContactDisplacement`/`SetTransform`，既有 Hard Root compatibility path 保留。
- [x] 墙夹、角落、窄通道中以静态障碍优先，无法完全分离时保留有界软重叠；自动化已覆盖直墙，角落/窄通道 Game View 留给 T5。
- [x] 关闭 Presence 时旧 root-radius/contact 行为的明确回归政策与测试。

## 4. 验证与证据

- tests：大型角色平移/原地 90°/180° yaw、双方相向、不同质量、不可推动、同中心、墙/角/窄通道、teleport、restore 首帧。
- 断言至少跨下一次 motion tick 后静态 collider 无穿透、速度不爆炸、固定输入重演一致；只对 Presence-originated path 断言无直接 transform displacement。
- warmed full motion/contact path 0 B；Unity PlayMode 初步观察平滑退出和墙边安全。
- M1-T5 的唯一一次 P0/P1 终审统一覆盖 fixed-point 量纲、Yaw wrap、clamp 顺序和 static sweep 优先级；T3 不另开逐任务评审。

## 5. 停止条件与派发

若现有 motor无法合并外部 contact velocity且需要架构变更，停止；若修复会改 root distance/Hurtbox/Attack Opportunity合同，停止。回报参数、场景结果、tests/Unity和网络待接字段。

## 6. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 实际分支 / 提交 | `main` 共享脏工作树；未提交、未推送 |
| 实际修改文件 | `CharacterTestCharacterMotionSystem.cs`、`CharacterTestCharacterContactResolver.cs`、新增 `CharacterTestLargeCharacterPresenceMotionSafetyTests.cs` + `.meta`；仅回填本记录 |
| 测试与原始证据 | EditMode focused job `640ebfb8db1b45f99f846450cc3a5540`：6/6 passed、0 failed、0 skipped；覆盖 actual root translation、相反 attempted intent、90°/±向、+180° tie、Yaw wrap、pressureScale、质量/不可推动、同中心、zero-limit、无效 rotation witness、Presence off、顺序重演与 next-tick wall sweep |
| 集成 / 回滚结果 | 候选已接入现有三轮 pressure solver 与下一 Tick kinematic/static sweep；未提交，按上述 4 个 owner 文件回滚 |
| 状态历史 | 2026-08-24：`Planned` → `In Progress` → `Verified`；主代理回查 focused job、最终脚本验证、NoAlloc 与 owner 边界后接受 |
| 执行者 / 审查者 | 子代理完成候选；未做独立评审，主代理负责参数边界与集成接受，M1 只在 T5 保留一次 P0/P1 终审 |
| surface velocity / clamp | `v_surface = Δroot_XZ * tickRate + (omega*rZ, -omega*rX)`；`omega = ShortestAngleDelta * tickRate * π / 180000`，π 以 Fix64 scale 私有定点常数表示；速度上限 `RunSpeed + ceil(Acceleration/tickRate)`，Yaw 限 `TurnSpeed`，penetration bias 限 `ceil(Acceleration/tickRate)`，并乘当前 volume ordinal 的 `PressureScaleMilli` |
| wall safety / determinism | 固定输入/roster 与 sample 顺序扰动结果一致；Presence 不直接位移；next-tick wall case 经现有 static sweep 未穿墙。角落、窄通道 PlayMode 观察留 T5 |
| Unity / NoAlloc | 最终 `validate_unity_scripts` job `6efca43f3a9441be8f755e18e5e8f56f` passed：refresh/compile/domain reload/editor ready，Console 0 errors；warmed 8 角色 × 4 Presence × 224 constraints × 128 frames 为 0 B |
| 限制 | 未做独立评审、未跑全仓/PlayMode；完整 generated-package Motion capture 的单独 GC 数值、角落/窄通道 Game View 与网络 parity 等待 T5/T4 |
