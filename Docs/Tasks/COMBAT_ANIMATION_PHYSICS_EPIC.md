# Combat Animation Physics Epic：确定性动画与物理战斗框架

> **状态**: Active
> **优先级**：P0
> 起点版本：r1213
> 设计文档：`Docs/COMBAT_ANIMATION_PHYSICS.md`
> 目标：把动作战斗需要的动画时间轴、确定性物理查询、武器轨迹、命中结算和回放验证拆成可并行开发的小任务。

## 目标

建立一套不依赖 Unity 物理作为权威结果的 Combat Runtime：

```text
Frame Input
  -> Combat Frame Clock
  -> Action Timeline
  -> Motion / Root Motion Resolver
  -> Combat Physics Query
  -> Weapon Trace
  -> Hit Resolve
  -> Ability / Buff / Modifier Events
  -> Replay / Hash / Diagnostics
```

首要目标不是做完整 3D 刚体模拟，而是让战斗动作在单机、回放和未来帧同步联机中使用同一套确定性逻辑。

## 开发原则

- 每个小任务必须可以独立验收、独立 SVN 提交。
- 先做纯 C# Runtime Core，再做 Unity 表现和 Authoring Bridge。
- 权威逻辑不依赖 `UnityEngine.Physics`、`Time.deltaTime`、`MonoBehaviour.Update` 的执行顺序。
- 第一版以测试和可观察性为主，不急于做炫酷 3D 表现。
- 子代理之间只通过文档中定义的数据契约协作，不互相改对方模块。
- 任何影响确定性结果的规则都要进入测试：固定帧、排序、随机、hash、query 结果。

## 非目标

- 不做通用刚体模拟。
- 不做完整角色动画系统导入工具。
- 不实现真实联网协议。
- 不接入 WGame 具体角色、技能、Buff 或业务配置。
- 不重构现有 Attributes / Buffs / Modifiers / Gameplay 模块。

## 总体拆分

| 顺序 | 任务 | 负责人建议 | 产物 |
|------|------|------------|------|
| 1 | M1 Deterministic Core | Core 子代理 | 固定帧、Id、排序 key、hash 骨架 |
| 2 | M2 Fixed Math Minimal | Math 子代理 | fixed-point 最小数学集和测试 |
| 3 | M3 Combat Physics Query | Physics 子代理 | Ray / Sphere / Capsule / AABB 查询 |
| 4 | M4 Action Timeline | Animation 子代理 | 动作帧、窗口、事件轨道 |
| 5 | M5 Weapon Trace | Physics + Animation 子代理 | 武器轨迹帧、sweep、命中去重 |
| 6 | M6 Hit Resolve Bridge | Gameplay 子代理 | 命中结果到 Ability/Buff/Modifier 事件 |
| 7 | M7 Diagnostics / Replay | Diagnostics 子代理 | frame hash、replay 输入、desync dump |
| 8 | M8 Unity Showcase | UI/Unity 子代理 | HUD 展示 query、trace、hit explain |
| 9 | M9A CombatPhysicsWorld Query v0 | Physics 子代理 | body/AABB collider 注册、Ray/AABB 查询、Showcase 最小接入 |
| 10 | M9B Shape Queries v0 | Physics 子代理 | Sphere / Capsule / Sector 查询、WeaponTrace Capsule 接入 |
| 11 | M9C Unity Scene Binding v0 | Unity/Demo 子代理 | Player / Enemy marker transform 同步到 CombatPhysicsWorld |
| 12 | M9D Manual Combat Showcase Feedback v0 | Unity/Demo 子代理 | 鼠标手测、镜头、trace / resolve 场景反馈 |
| 13 | M10 Combat Authoring / Gizmo Tool | Editor/Authoring 子代理 | Authoring asset、Scene View Gizmo、Timeline、Validation、Export |
| 14 | M11D.1 Physics Shape Contract and Unified Query API | Physics 子代理 | runtime shape descriptor、统一 query / batch 契约、debug report |
| 15 | M11D.2 Physics Broadphase v0 | Physics 子代理 | grid / spatial hash broadphase、候选稳定排序、规模测试 |
| 16 | Combat Physics：命中查询调试与可视化 | Physics + Demo + UI 子代理 | Play Mode 展示 query explain、candidate、filter reason、hit reason |
| 17 | Combat Physics：Runtime World Lifecycle | Physics 子代理 | body / collider 生命周期、revision / stats、snapshot / copy、query 回归和规模测试 |
| 18 | Combat Motion：Kinematic Character Movement v0 | Motion + Physics + Demo 子代理 | 基础移动、重力、跳跃、静态碰撞响应、world sync、可玩验证 |
| 19 | Combat Motion v1：Capsule Character Proxy / Narrow Phase | Motion + Physics + Demo 子代理 | deterministic capsule proxy、narrow phase、skin width、no penetration、query regression |

依赖方向：

```text
Combat.Core
  <- Combat.Physics
  <- Combat.Animation
  <- Combat.GameplayBridge
  <- Demo / UI / Unity Bridge
```

## 推荐目录

首版建议新增：

```text
Assets/Scripts/MxFramework/Combat/
  Core/
  Physics/
  Animation/
  GameplayBridge/
  Diagnostics/

Assets/Scripts/MxFramework/Tests/Combat/
  Core/
  Physics/
  Animation/
  Diagnostics/
```

如 asmdef 数量过多影响开发速度，第一批可以先建一个运行时 asmdef 和一个测试 asmdef：

```text
MxFramework.Combat
MxFramework.Combat.Tests
```

后续模块稳定后再拆分。

## M1：Deterministic Core

目标：

- 建立 Combat Runtime 的固定帧推进和稳定身份体系。
- 后续所有模块都以 frame 为唯一权威时间单位。

建议类型：

```text
CombatFrame
CombatFrameClock
CombatStepConfig
CombatEntityId
CombatBodyId
CombatColliderId
CombatHash
StableSortKey / CombatSortKey
```

验收：

- 同样 step count 输出同样 frame。
- frame 不能倒退，reset 后回到确定初始状态。
- 同一批乱序 id 输入，排序结果稳定。
- `CombatHash` 对相同状态输出一致 hash，对不同 frame 或 id 输出不同 hash。

禁止：

- 不接 Unity `Time.deltaTime`。
- 不引入 `UnityEngine`。
- 不提前写 physics query。

## M2：Fixed Math Minimal

目标：

- 提供 Combat 权威逻辑可用的最小 fixed-point 数值集。
- 第一版只覆盖 query、距离、排序所需能力。

建议类型：

```text
Fix64
FixVector2
FixVector3
FixBounds
```

最小能力：

- 加、减、乘、除。
- 比较、Abs、Min、Max、Clamp。
- Dot、LengthSquared。
- Normalize 可先用确定性近似，并明确误差。

验收：

- 边界值、零向量、负数、溢出策略都有测试。
- 不使用 `float` / `double` 作为权威结果存储。
- 文档或注释写明 scale、rounding、overflow 策略。

## M3：Combat Physics Query

目标：

- 用自定义几何查询替代 Unity Physics 作为战斗命中权威。
- 第一版只做足够支撑动作战斗的静态几何和 hurtbox/hitbox 判定。

建议类型：

```text
CombatPhysicsWorld
CombatBody
CombatCollider
CombatQuery
CombatQueryResult
RayQuery
SphereQuery
CapsuleQuery
AabbQuery
```

验收：

- Ray / Sphere / Capsule 至少覆盖 2D 平面移动和 3D 近战攻击常见情况。
- query candidate 和 result 都稳定排序。
- 同一 world、同一 query 多次执行结果一致。
- 测试不依赖 Unity Scene。

第一版可暂缓：

- Broadphase 优化。
- OBB / Sector。
- 复杂 mesh collision。

## M4：Action Timeline

目标：

- 用固定帧表达动作状态，而不是依赖 Animator 当前浮点时间。
- 让动作的命中窗口、取消窗口、移动窗口和事件轨道都可回放。

建议类型：

```text
CombatActionTimeline
CombatActionClip
CombatActionFrameEvent
CombatActionWindow
CombatActionInstance
```

验收：

- 指定 action id 和起始 frame，任意 frame 都能查询当前窗口状态。
- 同一 timeline 输出同一批 frame events。
- 可以表达 startup / active / recovery。
- 不依赖 Animator。

## M5：Weapon Trace

目标：

- 支持大量射线和武器扫掠，但运行时结果必须稳定可复现。
- 先用手写或测试数据模拟动画烘焙结果。

建议类型：

```text
WeaponTraceFrame
WeaponTraceSegment
WeaponTraceQueryBuilder
WeaponHitOnceKey
```

验收：

- frame N-1 到 N 可生成确定的 capsule sweep 或 segment query。
- 高速挥砍有确定性子采样规则。
- 同一动作实例对同一目标可去重。
- query 输出可进入 M3 Physics Query。

## M6：Hit Resolve Bridge

目标：

- 把物理命中候选转成战斗结算结果。
- 与现有 Ability / Buff / Modifier 通过事件或桥接层协作。

建议类型：

```text
HitCandidate
HitResolveContext
HitResolveResult
HitResolveSystem
CombatGameplayEventBridge
```

验收：

- 同一帧多个命中候选按稳定规则结算。
- 能表达命中、格挡、无敌过滤、优先级压制。
- 可以把命中结果转为现有 `AbilityEvent` 或属性变更。
- 不破坏当前 Ability demo。

## M7：Diagnostics / Replay

目标：

- 从第一版开始就能定位确定性问题。

建议类型：

```text
CombatReplayInput
CombatReplayRecorder
CombatDebugSnapshot
CombatQueryTrace
CombatHitExplain
```

验收：

- 每帧输出 hash。
- 记录输入后可重放并得到同样 hash。
- hash 不一致时能导出 frame、entity、query、hit result 摘要。
- 不刷 Console；通过 snapshot / event log / test assertion 输出。

## M8：Unity Showcase

目标：

- 让制作人能在 Play 模式下观察 Combat Runtime，而不是只看测试。

建议内容：

- 当前 combat frame。
- 当前 action state。
- 生成的 query 数量。
- 最近 hit explain。
- replay/hash 状态。

验收：

- HUD 可隐藏、可滚动，不遮挡 3D 场景。
- UI 只依赖 Runtime 公开 snapshot，不反向驱动 Core 私有状态。
- 不使用 Console 作为主要反馈。

## 第一轮派发

第一轮只派发 3 个子任务，避免并行范围过大：

```text
Task A: Deterministic Core Skeleton
Task B: Fixed Math Evaluation
Task C: Combat Physics Query Contract
```

### Task A：Deterministic Core Skeleton

交付：

- 新增 `CombatFrame`、`CombatFrameClock`、`CombatStepConfig`。
- 新增基础 id 类型和稳定排序 key。
- 新增最小 `CombatHash`。
- 新增 EditMode 测试。

文件范围：

```text
Assets/Scripts/MxFramework/Combat/Core/
Assets/Scripts/MxFramework/Tests/Combat/Core/
```

不允许：

- 不写 physics query。
- 不接 Unity UI。
- 不修改现有 Ability/Buff/Modifier 行为。

### Task B：Fixed Math Evaluation

交付：

- 调研当前 `Core/Math` 是否已有可复用基础。
- 给出 `Fix64` 自研或引入轻量实现的建议。
- 若实现，第一批只做 `Fix64` 和 `FixVector3` 最小集。
- 新增测试覆盖 rounding、division、overflow 策略。

文件范围：

```text
Assets/Scripts/MxFramework/Core/Math/
Assets/Scripts/MxFramework/Tests/Core/
```

不允许：

- 不引入大型第三方物理库。
- 不把 fixed math 写到 Unity 层。

### Task C：Combat Physics Query Contract

交付：

- 只定义 query 数据契约和结果排序规则。
- 可新增空 world 或 fake world 测试，不急于完整几何实现。
- 明确 Ray / Sphere / Capsule 的输入输出结构。

文件范围：

```text
Assets/Scripts/MxFramework/Combat/Physics/
Assets/Scripts/MxFramework/Tests/Combat/Physics/
```

不允许：

- 不接 `UnityEngine.Physics`。
- 不做 Broadphase。
- 不做 WeaponTrace。

## 子代理通用提示词

派发给任何子代理时，必须附带：

```text
你在 WGameFramework 工程根目录工作。
SVN 是主版本控制，不要使用 git 提交。
不要回退他人改动。
遵守 AGENTS.md。
本任务只允许修改任务文档声明的文件范围；如确需越界，先说明原因。
Runtime Core 不引用 UnityEditor；Combat Core 默认不引用 UnityEngine。
完成后运行相关测试或说明无法运行的原因。
提交前运行 git diff --stat && git diff --check，并确认 svn status 只包含本任务文件。
```

## 提交策略

- 每个子任务单独 SVN revision。
- 提交信息格式：

```text
Add combat deterministic core skeleton
Add fixed math minimal primitives
Add combat physics query contracts
```

- 不把文档派发、Core、Physics、UI 混在同一次提交。

## 当前下一步

已完成到 Combat Motion v0，并发现部分任务文档状态滞后于实现：

- `CombatPhysicsWorld` 支持 body 注册、AABB collider 注册、Ray 查询、AABB 查询和稳定结果排序。
- `CombatPhysicsWorld` 支持 Sphere / Capsule / Sector 查询。
- Runtime Showcase 的 WeaponTrace 已接入 Capsule 查询，Resolve 读取 CombatPhysicsWorld 查询结果。
- Runtime Showcase 可从 Combat 测试场景的 Player / Enemy marker transform 同步 body 位置。
- EditMode Combat 测试覆盖查询过滤、排序、ray hit point / normal、body 位置更新和基础 shape query。
- Combat Showcase Play 模式下动态挂载鼠标输入控制器，不要求场景预挂额外组件。
- 左键点击 Player / Enemy marker 可选中角色，左键拖动或右键点击地面可移动选中角色。
- 右键拖动旋转镜头，滚轮缩放镜头。
- 选中角色后可执行探测和攻击命令，结果进入 `CombatPhysicsWorld` 查询、`HitResolve` 和 HUD 事件记录。
- Trace / Resolve 已有场景表现：trace line、hit / miss marker、result label 和 HUD 摘要，手动测试不再只依赖 Console。
- M10 Combat Authoring / Gizmo 的 M10E-M10I 子任务已完成多项 no-typing、timeline、duplicate/delete、split window 和 validation quick action 能力。
- M11D.1 / M11D.2 已完成统一 query contract 与 broadphase v0。
- `Combat Physics：命中查询调试与可视化` 已出现 explain / debug report 数据链路实现证据，但任务文档仍需补 Play Mode closeout。
- `Combat Physics：Runtime World Lifecycle` 已出现 revision / stats / mutation / copy API 和测试证据，但任务文档仍需补 closeout。
- `Combat Motion：Kinematic Character Movement v0` 已完成并验收。

本轮已完成两个 closeout 校准任务：

1. `COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md`：已补 Play Mode 演示、HUD count、marker 同步、Console / 影响面验收记录，状态 `Accepted / Closed`。
2. `COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md`：已补 lifecycle 测试门槛、stats / revision、snapshot / copy 隔离、规模测试记录，状态 `Accepted / Closed`。

下一步已具体化为 `Combat Motion v1：Capsule Character Proxy / Narrow Phase`。任务文档：`Docs/Tasks/COMBAT_MOTION_V1_CAPSULE_CHARACTER_PROXY.md`。

理由：

- 当前 Physics closeout 已完成，后续可以在已关闭的 query / lifecycle / debug report 口径上推进 Motion v1。
- Motion v1 不应一次性吃下所有复杂角色控制；本批先把 v0 AABB character proxy 扩展为 deterministic capsule character proxy / narrow phase，服务角色移动碰撞，不改变攻击 query 语义。
- `坡面/台阶`、`手感参数`、`移动平台`、`Root Motion Resolver` 等仍保留为后续独立功能包候选。

## M11：Combat Runtime Foundation Completion

M10 已经把 Authoring 到 Play Mode Showcase 的最小闭环打通。若优先补齐物理基础框架，下一阶段从 `M11D.1` 开始：

- `M11D.1 Combat Physics Shape Contract and Unified Query API`：统一 runtime shape descriptor、query request、batch query 和 debug report，不改变现有 query 行为。
- `M11D.2 Combat Physics Broadphase v0`：在统一契约稳定后，再引入 grid / spatial hash broadphase、候选稳定排序和规模测试。任务文档：`Docs/Tasks/COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`。

当前物理任务文档：

- `Docs/Tasks/COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`
- `Docs/Tasks/COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
- `Docs/Tasks/COMBAT_PHYSICS_HIT_QUERY_DEBUG_VISUALIZATION.md`
- `Docs/Tasks/COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md`
- `Docs/Tasks/COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md`
- `Docs/Tasks/COMBAT_MOTION_V1_CAPSULE_CHARACTER_PROXY.md`

## 当前制作优先级

截至 2026-05-09，物理基础查询、broadphase v0、命中查询调试与可视化、`CombatPhysicsWorld` lifecycle、`Combat Motion：Kinematic Character Movement v0` 均已完成并验收：

- 运行时新增 `MxFramework.Combat.Motion`，覆盖固定帧基础移动、重力、跳跃、grounded、静态 AABB 阻挡、墙 / 天花板响应和 Motion -> `CombatPhysicsWorld` body position 同步。
- `Assets/Scenes/CombatAnimationPhysicsTest.unity` 已挂载 `RuntimeCombatShowcase`、Player / Enemy marker 和 Motion obstacle，可 Play 验证跑、跳、落地、撞墙、移动后 Probe / Attack。
- 验收：`dotnet build WGameFramework.sln --no-restore -v minimal` 0 warning / 0 error；Unity EditMode `MxFramework.Tests.Combat` 105/105；Play Mode smoke 中 Enemy HP `600 -> 490`，query `hit=1`；Console 0 error / 0 warning；影响面检查通过。任务文档：`Docs/Tasks/COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md`。

`Combat Motion v1：Capsule Character Proxy / Narrow Phase` 已完成并验收，任务文档：`Docs/Tasks/COMBAT_MOTION_V1_CAPSULE_CHARACTER_PROXY.md`。

本批结果：

- Motion contract 新增 deterministic capsule proxy，覆盖 radius / height / center / skin width / layer / collision mask。
- `CombatKinematicMotor` 使用 capsule proxy narrow-phase sweep，覆盖 capsule vs wall / ground / ceiling、skin width、no penetration、world sync、移动后 Probe / Attack。
- Combat regression 证明 Ray / Sphere / Capsule / AABB / Sector 攻击 query 既有结果不变。

后续 Motion v1+ 候选保留为独立功能包：

- 可玩手感：coyote time、jump buffer、air control、加速度 / 减速度曲线。
- 地形能力：坡面、台阶、边缘稳定、移动平台。
- 动画桥接：Root Motion Resolver 与 Motion state 的确定性合并。
