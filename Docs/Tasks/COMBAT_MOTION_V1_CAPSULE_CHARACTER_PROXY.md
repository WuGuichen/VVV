# Combat Motion v1：Capsule Character Proxy / Narrow Phase

> **状态**：Accepted / Closed（2026-05-09）
> **优先级**：P0
> **功能包类型**：Runtime Motion / Physics narrow phase
> **前置已完成项**：
> - `M11D.1 Physics Shape Contract and Unified Query API`
> - `M11D.2 Physics Broadphase v0`
> - `Hit Query Debug Visualization`
> - `Runtime World Lifecycle`
> - `Combat Motion v0`
> **设计依据**：
> - `Docs/COMBAT_ANIMATION_PHYSICS.md` 的 `MotionSystem` / `Combat Physics` 边界
> - `Docs/Tasks/COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md` 的 Motion contract、world sync 和后续非 v0 项
> - `Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md` 的当前制作优先级

## 功能目标

把 `Combat Motion v0` 的 AABB character proxy 扩展为 deterministic capsule character proxy / narrow phase，服务角色移动碰撞，不改变攻击 query 语义。

完成后，Motion 的权威角色代理应能用 capsule 表达站立角色的半径、高度、center、skin width，并在固定帧内通过稳定 narrow phase 判断地面、墙、天花板接触。该能力只服务 kinematic character movement，不把攻击 Ray / Sphere / Capsule / AABB / Sector query 的既有结果、排序、过滤和 debug report 语义改写成新规则。

本任务是 `Combat Motion v1` 的第一个窄范围功能包，不一次性扩展到复杂地形、平台、动态刚体或动画位移融合。

## 做 / 不做边界

做：

- 新增或扩展 Motion 层的 capsule character proxy contract，包含 deterministic radius / height / center / skin width / layer / collision mask 等字段。
- 为角色移动实现 capsule vs static collider 的 narrow phase / sweep 或等价保守约束。
- 保留 v0 Motion contract 的固定帧输入、state、step result、collision flags、grounded 语义和 world sync 口径。
- 输出可测试的 collision hit summary：hit body / collider、normal、distance 或 fraction、applied delta、blocked flags。
- 让 Showcase 能切换或演示 capsule proxy，并显示 capsule 参数、skin width、最近 narrow phase hit 和 world body position。
- 用 regression 证明攻击 query 既有 Ray / Sphere / Capsule / AABB / Sector 结果不变。

不做：

- 不做坡面、坡度限制、台阶、边缘吸附、复杂地面贴合。
- 不做移动平台、旋转平台、动态刚体、角色推挤或通用刚体响应。
- 不做 Root Motion Resolver、动画位移优先级、动作锁定、击退或受击位移融合。
- 不做网络预测、回滚、输入压缩或状态校验协议。
- 不接入 WGame 真实角色、技能、Buff、关卡或配置数据。
- 不引入 Unity Physics、`Rigidbody`、`CharacterController` 或 `UnityEngine.Physics` 作为权威结果。

## 串行链路

本功能包内部按以下链路推进：

1. Contract：定义 capsule character proxy、config、step result 扩展和兼容策略。
2. Capsule sweep / narrow phase：实现 capsule vs wall / ground / ceiling 的稳定命中计算和候选 tie-breaker。
3. Collision response：基于 capsule hit 执行 no penetration、skin width、blocked axis、grounded / ceiling / wall 分类和剩余位移投影。
4. World sync：Motion step 完成后同步新位置到 `CombatPhysicsWorld` body position。
5. Showcase：提供可操作演示、HUD 参数和最近 hit 摘要。
6. Regression：锁定 determinism、capsule collision 行为和攻击 query 结果不变。

没有 contract 前，不先写 Showcase；没有 regression 前，不宣称 query 语义保持不变；没有 world sync 前，不宣称 Probe / Attack 使用新位置。

## 可并行工作拆分

在 contract 稳定后，可以并行派发：

- Motion contract：定义 capsule proxy、config、StepResult 扩展、AABB v0 兼容策略和公开 API 注释。
- Narrow phase：实现 capsule vs 静态 AABB / 既有 shape 的 sweep 或保守移动约束、稳定候选排序和 tie-breaker。
- Collision resolve：处理 skin width、no penetration、collide-and-slide、blocked flags、grounded / wall / ceiling 分类。
- Showcase / HUD：把 capsule proxy 参数、最近 hit、world revision、body position 和 Probe / Attack 结果暴露给制作侧。
- Tests：补 determinism、ground / wall / ceiling、skin width、no penetration、world sync、query regression 和 Showcase smoke。

这些子任务只通过 Motion contract、Physics query contract 和 StepResult 协作，不读取对方私有容器。

## 验收标准

功能验收：

- 固定输入 determinism：同一初始 world、capsule proxy、step config 和输入序列重复运行，`Position / Velocity / Grounded / CollisionFlags / WorldPosition / WorldRevision` 完全一致。
- Capsule vs wall：角色向墙移动时不能穿透，最终位置保留 skin width，`CollisionFlags` 包含 wall / blocked axis。
- Capsule vs ground：角色从空中下落到地面后进入 grounded，垂直速度被清零或进入明确 grounded clamp。
- Capsule vs ceiling：角色上升撞顶后停止继续上升，`CollisionFlags` 包含 ceiling / blocked Y，后续帧继续受重力下落。
- Skin width：接触点保留固定安全间隙，不依赖浮点误差；skin width 变更会以确定方式影响最终位置。
- No penetration：固定测试场景中 step 后 capsule 不穿透 wall / ground / ceiling；初始轻微重叠处理规则明确并有测试。
- World sync：Motion step 后新位置写回 `CombatPhysicsWorld` body position，world revision / stats 变化符合 lifecycle 约定。
- Probe / Attack 使用新位置：移动后立即触发 Probe / Attack，query origin 和 hit / miss 使用 Motion 写回后的 body position。
- 不改变攻击 query 既有结果：Ray / Sphere / Capsule / AABB / Sector 攻击 query 在既有 regression 数据中 hit count、排序、hit point / normal / distance、filter reason 和 debug report 语义保持不变。

技术验收：

- Combat Motion 权威逻辑使用 `Fix64` / `FixVector3` 或既有 deterministic math，不用 `float` / `double` 存储权威位置、速度或碰撞结果。
- Runtime Motion / Physics 不引用 `UnityEditor`。
- 不调用 Unity Physics、`Rigidbody` 或 `CharacterController` 作为权威。
- Narrow phase 的候选选择、同距离排序、初始重叠处理和迭代截断规则稳定。
- Runtime path 复用 broadphase 或等价候选裁剪，不把全量 collider 遍历固化为长期实现。

## 测试门槛

最低测试：

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Combat
Unity EditMode: MxFramework.Tests.Combat.Motion.*
Unity EditMode: MxFramework.Tests.Combat.Physics.*
Unity Console: 0 error
Tools/GitNexus/gitnexus.sh detect-changes
```

必须补充或确认覆盖：

- Capsule motion determinism tests。
- Capsule vs wall / ground / ceiling tests。
- Skin width / no penetration tests。
- 初始轻微 overlap 的确定性处理测试。
- World sync tests：motion 后 query 使用新 body position。
- Probe / Attack moved-position tests。
- Query regression tests：Ray / Sphere / Capsule / AABB / Sector 攻击 query 既有结果不变。
- Showcase smoke：HUD 或 runner 可读到 capsule 参数、grounded、velocity、collision flags、body position 和最近 narrow phase hit。

验收输出不要只写“测试通过”，必须记录：

- 使用了哪组固定输入；
- capsule radius / height / center / skin width；
- 最终 position / velocity / grounded / collision flags；
- 至少一个撞墙、落地、撞顶结果；
- no penetration 的最小间隙或断言口径；
- 移动后 Probe / Attack 的命中变化；
- 攻击 query regression 的覆盖范围；
- Console 最终状态；
- GitNexus 风险等级。

## 完成后演示方式

推荐演示脚本：

1. 打开 Combat Motion Showcase 或当前 Combat 测试场景的 Motion 模式。
2. 选择 capsule character proxy，确认 HUD 显示 radius / height / skin width。
3. 控制 Player 在地面水平移动，确认 position / velocity / body position 更新。
4. 朝墙移动，确认 capsule 被阻挡且不穿透，HUD 显示 wall / blocked axis 和最近 hit normal。
5. 从空中下落到地面，确认 grounded 和 skin width 行为稳定。
6. 跳到低天花板下方，确认撞顶后停止上升并继续下落。
7. 移动到 Enemy 附近后触发 `Probe` 和 `Attack`，确认 query 使用移动后的 Player 位置。
8. 切回或运行攻击 query regression，确认 Ray / Sphere / Capsule / AABB / Sector 既有攻击查询结果不变。

演示通过标准：制作人无需阅读测试代码，就能判断“角色移动碰撞已从 AABB proxy 升级到 capsule narrow phase，移动、落地、撞墙、撞顶、攻击判定跟随位置都稳定，并且既有攻击查询没有被改坏”。

## 派发提示

派发给子代理时使用功能包口径：

```text
你负责 WGameFramework 功能包 `Combat Motion v1：Capsule Character Proxy / Narrow Phase` 的实现。
目标是把 v0 AABB character proxy 扩展为 deterministic capsule character proxy / narrow phase，服务角色移动碰撞，不改变攻击 query 语义。
先读 AGENTS.md、Docs/COMBAT_ANIMATION_PHYSICS.md、Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md、Docs/Tasks/COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md、Docs/Tasks/COMBAT_MOTION_V1_CAPSULE_CHARACTER_PROXY.md。
不要回退他人改动。
只在任务文档允许范围内修改文件；如确需越界，先说明原因。
不要做坡面/台阶/移动平台/动态刚体/Root Motion/网络预测/WGame 数据接入。
不要引入 Unity Physics、Rigidbody 或 CharacterController 作为权威。
完成后必须跑 build、Combat / Motion / Physics EditMode 测试、Unity Console 检查和 GitNexus，并记录 capsule 参数、固定输入、墙/地/顶碰撞、skin width、no penetration、world sync、移动后 Probe / Attack、攻击 query regression 的验收结果。
```

## 当前记录

- 2026-05-09：创建功能包任务文档，状态 `Ready for dispatch`。本次只定义任务、边界、串行链路、并行拆分、验收标准、测试门槛和演示方式，不包含运行时代码实现。
- 2026-05-09：实现并验收 `CombatMotionCapsuleProxy`、capsule narrow-phase sweep、skin width / no penetration、Showcase capsule summary 和 Motion / Combat regression。状态收口为 `Accepted / Closed`。

## Closeout 记录（2026-05-09）

本次交付：

- Motion contract 新增 `CombatMotionCapsuleProxy`，稳定表达 radius / height / center / skin width / layer / collision mask。
- `CombatKinematicMotor` 从 v0 AABB proxy 切到 capsule proxy bounds 与 capsule segment sweep；保留固定帧输入、state、collision flags、grounded、world sync 语义。
- Capsule vs static AABB 支持地面、墙、天花板阻挡；wall no-penetration 断言保留 `skinWidth=0.01` 的安全间隙。
- Showcase `MotionSummary` 显示 capsule radius / height / skin、position / velocity / grounded / flags / body 和最近 collision summary。
- 攻击 query 路径未改动；Combat 回归覆盖 Ray / Sphere / Capsule / AABB / Sector 既有 query contract 与 Showcase moved-position probe / attack。

验收输入：

- Capsule 参数：`radius=0.50`，`height=2.00`，`center=(0,0,0)`，`skinWidth=0.01`，collision mask 为 obstacle layer。
- 固定输入：determinism 序列 140 fixed steps，前 70 帧 `+X`，后 70 帧 `-X`，第 5 / 16 / 90 帧发送 jump。
- 撞墙：朝 `+X` wall 移动后 `CollisionFlags` 包含 `Wall / BlockedX`，最终 `Position.X = wallMinX - radius - skinWidth = 1.49`。
- 落地：空中下落后进入 `Grounded`，vertical velocity clamp 到 `0`，body position 写回 `CombatPhysicsWorld`。
- 撞顶：上升撞 ceiling 后 `CollisionFlags` 包含 `Ceiling / BlockedY`，vertical velocity 清零，角色保持 airborne 并继续受后续重力影响。
- No penetration：wall gap 断言为 `skinWidth=0.01`。
- World sync：Motion step 后 `CombatPhysicsWorld` player body position 等于 Motion state position，移动后短距离 Probe 使用新 body position 命中目标。

验证命令：

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Combat.Motion => 9/9 passed
Unity EditMode: MxFramework.Tests.Combat => 110/110 passed
Unity Console errors/warnings: no project error; only MCP websocket tool warning and LogAssert test control logs
Tools/GitNexus/gitnexus.sh detect-changes => low risk, affected processes 0
```

剩余风险：

- 本批 narrow phase 只覆盖当前静态 AABB obstacle 场景，不包含坡面、台阶、移动平台、动态刚体或复杂地形贴合。
- Capsule corner contact 采用稳定保守约束，后续若要复杂地形手感，需要独立功能包细化 corner / edge 行为。
- Showcase 已显示 capsule 参数与最近 hit summary，但未新增独立 capsule mesh gizmo；当前仍复用现有 obstacle / marker 表现。
