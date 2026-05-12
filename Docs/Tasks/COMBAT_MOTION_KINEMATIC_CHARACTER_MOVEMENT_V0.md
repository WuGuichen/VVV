# Combat Motion：Kinematic Character Movement v0

> **状态**：Completed / Accepted（2026-05-09）
> **优先级**：P0
> **功能包类型**：Runtime 基础能力 / 制作侧可玩验证
> **前置完成项**：
> - `COMBAT_PHYSICS_M11D_1_SHAPE_QUERY_CONTRACT.md`
> - `COMBAT_PHYSICS_M11D_2_BROADPHASE_V0.md`
> - `COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md` 的基础 lifecycle API（任务文档 closeout 待补）
> **设计依据**：
> - `Docs/COMBAT_ANIMATION_PHYSICS.md` 的 `移动与约束 / CombatMotor`
> - `Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md` 的 `Motion / Root Motion Resolver`

## 收口摘要

本任务已从“待派发”收口为“已完成 / 已验收”。v0 交付范围以确定性 Kinematic Character Movement 为准：固定帧输入移动、重力、跳跃、grounded、静态 AABB 阻挡、墙 / 地面 / 天花板碰撞分类、有限次数 collide-and-slide、Motion 状态写回 `CombatPhysicsWorld`，以及 Combat Showcase 中可操作的跑、跳、落地、撞墙、移动后 Probe / Attack 闭环。

校准说明：Motion v0 已验收不变；但 `COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md` 的独立任务文档仍需补 closeout 验收记录。Motion v0 依赖的是已存在的 world revision、body position sync 和基础 lifecycle API，不代表完整 Runtime World Lifecycle 功能包已经完成验收。

已实现范围：

- Runtime 新增 `MxFramework.Combat.Motion` 层，包含 `CombatMotionState`、`CombatMotionInput`、`CombatMotionConfig`、`CombatMotionStepResult`、`CombatMotionCollision`、`CombatMotionCollisionFlags`、`CombatKinematicMotor`。
- Motion 权威数据使用 `Fix64` / `FixVector3` 和 `CombatStepConfig` 固定步推进，不依赖 `Time.deltaTime`、`Rigidbody` 或 `CharacterController`。
- `CombatKinematicMotor` 支持水平移动、重力、起跳、禁止 v0 空中重复起跳、最大下落速度、skin width、ground / ceiling normal threshold、最大滑动迭代次数。
- 静态碰撞通过 `CombatPhysicsWorld` 查询候选，并输出 grounded、wall、ceiling、blocked axis 等 collision flags；贴地初始接触不会错误阻挡起跳。
- Motion step 完成后同步 Player body position 到 `CombatPhysicsWorld`，后续 Probe / Attack 使用移动后的 body position。
- Combat Showcase 已接入 Motion：`WASD` / 方向键移动，`Space` 跳跃，`P` Probe，`J` Attack，`Q` 切换 query shape，`T` 单步帧推进，`R` Reset。
- HUD / 调试摘要显示 Motion position、velocity、grounded、collision flags、body position、world revision 和最近交互结果。

## 已验收记录

验收日期：2026-05-09。

自动化验收：

- `dotnet build WGameFramework.sln --no-restore -v minimal`：通过，`0 warning / 0 error`。
- Unity EditMode `RuntimeCombatShowcaseRunnerTests`：`5/5 passed`。
- Unity EditMode `MxFramework.Tests.Combat`：`105/105 passed`。
- Unity Console：`0 error / 0 warning`。
- `Tools/GitNexus/gitnexus.sh detect-changes`：`low risk`。

固定输入 / 行为验收：

- Motion determinism：测试序列从 grounded 初始状态开始，运行 140 个固定 step；第 0-69 帧输入 `+X`，第 70-139 帧输入 `-X`，第 5 / 16 / 90 帧发送 `JumpPressed`。重复 replay 后 `Position / Velocity / Grounded / CollisionFlags / WorldPosition / WorldRevision` 完全一致。
- Gravity / landing：角色从空中下落到静态地面后进入 `Grounded`，`CollisionFlags` 包含 `Grounded`，垂直速度被清零，并且 world body position 与 Motion state position 一致。
- Grounded contact：角色已贴地且无移动输入时持续保持 `Grounded`，不继续累计下落速度，Y 位置保持稳定。
- Jump：grounded 起跳不会被初始贴地接触阻挡；起跳首帧离地并保留向上速度；空中重复 `JumpPressed` 不触发二段跳。
- Wall：向 `+X` 墙体移动 40 个固定 step 后无法穿墙，`CollisionFlags` 包含 `Wall` 和 `BlockedX`，X 速度清零。
- Ceiling：向上撞到天花板后 `CollisionFlags` 包含 `Ceiling` 和 `BlockedY`，上升速度清零，角色保持 airborne。
- World Sync：移动 30 个固定 step 后，旧位置短 Probe miss，新 `CombatPhysicsWorld` body position 发起的短 Probe hit=1。
- Showcase API：移动 60 个固定 step 后 HUD summary 包含 `pos / vel / grounded / flags / body`，移动后 Probe 命中目标。
- Play Mode smoke：移动 60 帧 + 跳跃 + Probe + Attack 后，Enemy HP `600 -> 490`，query `hit=1`。

当前 v0 验收结论：通过。该版本已经可以作为 Combat Runtime 的基础角色运动能力使用，并支撑 Showcase 中“跑、跳、落地、撞墙、移动后攻击判定”的制作侧验证。

## 功能目标

把 Combat Runtime 从“能做攻击命中查询”推进到“能用确定性逻辑驱动角色基础移动、跳跃、重力和静态碰撞响应”。

完成后，Combat Motion 应能稳定支撑以下运行时场景：

1. 角色从输入方向得到固定帧位移，不依赖 `Rigidbody` 或 `Time.deltaTime`。
2. 角色受重力下落，落到地面后进入 grounded 状态。
3. grounded 状态下可以起跳，空中继续受重力影响。
4. 角色水平移动撞到静态阻挡体时被阻挡，不能穿墙。
5. 角色跳跃撞到顶部阻挡体时停止上升，并继续下落。
6. 角色沿墙移动时可以做最小滑动或轴向截断，不出现卡死、穿透或位置抖动。
7. Motion 输出能同步回 `CombatPhysicsWorld` body position，后续攻击 query 读取移动后的新位置。
8. Play Mode Showcase 或等价验证场景能直接演示“跑、跳、落地、撞墙、攻击命中”的组合闭环。

这个功能包按“角色运动控制层”验收，不再拆成移动、跳跃、重力、碰撞四个微任务。子代理内部可以拆 motion state、sweep query、collision resolve、showcase、tests，但顶层交付必须是一组可玩的基础移动能力。

## 用户可见结果

- 制作人进入 Combat Motion Showcase 后，可以用键盘控制测试角色左右 / 前后移动。
- 按跳跃键后，角色离地、上升、受重力回落，并在地面稳定停住。
- 场景里有至少一个地面和一个墙体；角色不能穿过墙体或地面。
- HUD 或调试区显示：
  - position / velocity；
  - grounded；
  - collision flags；
  - 最近一次碰撞 normal / hit distance；
  - 当前 world revision / physics body position。
- 移动后再触发攻击或探测，命中查询使用角色新位置。

## 审查采纳补充

本节把任务审查中确认合理的实现提示固化为交付约束，避免子代理只交付“能动”的临时代码。

### Motion 合约最小字段

`CombatMotionState` 至少需要表达：

- `Frame` 或调用方可追踪的固定帧序号。
- `Position`：权威位置，使用 `FixVector3`。
- `Velocity`：权威速度，使用 `FixVector3`；垂直速度不得只藏在局部变量中。
- `Grounded`：上一帧 / 当前帧地面状态语义必须明确。
- `LastCollisionNormal`：最近一次有效碰撞法线；无碰撞时使用明确默认值。
- `CollisionFlags`：位域枚举，至少覆盖 `BlockedX`、`BlockedY`、`BlockedZ`、`Grounded`、`Ceiling`。

`CombatMotionInput` 至少需要表达：

- `MoveDirection`：水平输入方向，使用 fixed vector；输入归一化和 clamp 规则必须确定。
- `JumpPressed`：本帧跳跃请求；是否使用 press / held 语义必须在字段名或注释中明确。
- 可选 `MoveSpeedScale`：用于测试或后续 buff / slow 效果，但默认值必须稳定。

`CombatMotionStepResult` 至少需要表达：

- `State`：step 后的新状态。
- `DesiredDelta` 与 `AppliedDelta`：用于测试和 HUD 判断被阻挡了多少。
- `JumpStarted`：本帧是否真正起跳成功。
- `Collisions` 或最近一次 collision summary：包含 hit normal、hit fraction / distance、body / collider id。
- `CollisionFlags`：同一帧多次碰撞时允许组合位。

### 固定步长与积分

- Motion 权威逻辑必须以固定帧推进，使用 `CombatFrame` / `CombatStepConfig` 或等价固定 step 配置；不得把渲染帧耗时、`Time.deltaTime` 或 variable dt 传入权威积分。
- 位置、速度、重力、jump impulse 和每帧 delta 都使用 `Fix64` / `FixVector3` 或整数派生值；不得用 `float` / `double` 存储权威结果。
- 水平运动建议由输入方向、最大速度和 fixed step 得到每帧水平 delta；垂直运动由 `Velocity.Y += GravityPerStep`、jump impulse 和 velocity clamp 共同决定。
- 跳跃、重力、碰撞截断的执行顺序必须在实现中固定，并用测试覆盖；同一输入序列重复运行必须得到完全一致的 `Position / Velocity / Grounded / CollisionFlags`。

### 静态碰撞查询与响应

- 静态阻挡体查询必须复用 `CombatPhysicsWorld` 和 broadphase candidate 结果；允许保留 full-scan oracle 测试，但 runtime motion path 不应默认遍历全部 collider。
- v0 推荐用 AABB character proxy 起步；如实现 Capsule proxy，仍必须输出同样的 step result contract。角色形状选择不得影响攻击 query 既有语义。
- Sweep / constraint query 应返回最早 hit 的 fraction / distance、normal、target body / collider，并使用稳定 tie-breaker 处理同 fraction 多候选。
- 为避免贴面抖动或初始轻微重叠，必须定义 fixed `SkinWidth` / epsilon。移动到命中点时应保留安全间隙，不能依赖浮点误差“刚好不穿透”。
- 碰撞分类使用 normal：`normal.Y >= GroundMinNormalY` 视为地面，`normal.Y <= -CeilingMinNormalY` 视为天花板，其余视为墙。阈值进入配置或常量，并由测试锁定。
- 碰撞响应采用“sweep -> 移动到安全点 -> 投影剩余位移到接触面切线 -> 继续 sweep”的 collide-and-slide 思路；最大迭代次数固定，v0 建议 3 次，超过后截断剩余位移并记录 flag。
- 向上撞顶时清零向上速度；向下落地时设置 `Grounded` 并清零或 clamp 垂直速度。墙面阻挡不得改变 grounded 语义。

### World Sync 与调试

- Motion step 完成碰撞响应后，必须在同一固定帧内把 `CombatMotionState.Position` 写回对应 `CombatPhysicsWorld` body position。
- 攻击 query、Probe、debug report 和 HUD 读取的 body position 必须来自写回后的 `CombatPhysicsWorld`，避免 Motion 状态与 Physics World 跨帧不一致。
- HUD 中展示的 `bodyPosition`、world revision 和最近碰撞摘要应来自公开 state / result / world API，不读取私有容器。

### Showcase 与确定性测试

- v0 Showcase 场景只放简单平地、墙体和可选天花板，不加入动态刚体、移动平台或复杂坡面。
- 必须用 EditMode 测试记录一组固定输入序列，多次 replay 后比较最终 `Position / Velocity / Grounded / CollisionFlags`。
- 必须覆盖“移动后立即 Probe / Attack”的回归：query origin、hit / miss 和 debug report 使用移动后的 body position。

## 范围

允许修改：

- Combat Runtime 中新增 Motion / Motor 层公开类型。
- 基于 fixed frame 的 motion state、input、step result、motion config 和 collision result DTO。
- 使用 `CombatPhysicsWorld` / `CombatPhysicsQuery` 实现的静态阻挡查询、sweep 或保守移动约束。
- `CombatPhysicsWorld` 必要的最小查询扩展，但不得破坏既有 hit query 语义。
- Combat Demo / Runtime Showcase 的移动、跳跃、平台验证入口。
- Combat tests / Showcase tests / PlayMode smoke tests。
- 本任务文档、Epic 当前制作优先级和文档索引。

建议关注文件范围：

```text
Assets/Scripts/MxFramework/Combat/Core/
Assets/Scripts/MxFramework/Combat/Physics/
Assets/Scripts/MxFramework/Combat/Motion/
Assets/Scripts/MxFramework/Demo/Combat/
Assets/Scripts/MxFramework/Tests/Combat/
Assets/Scenes/
Docs/Tasks/COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md
Docs/Tasks/COMBAT_ANIMATION_PHYSICS_EPIC.md
```

如果当前 asmdef 还没有 `Combat/Motion/` 目录，可以先放在现有 `MxFramework.Combat` 程序集内，命名空间使用 `MxFramework.Combat.Motion`。

## 非目标

- 不做通用刚体模拟。
- 不做刚体旋转惯性、摩擦堆叠、关节、复杂多体接触求解。
- 不做 Rigidbody / CharacterController 作为权威运动结果。
- 不做复杂坡面、台阶、移动平台、斜坡限速的完整版本；v0 可只支持平地、墙、天花板。
- `CharacterSlopeLimit` 暂不作为 v0 可用能力；若实现中遇到斜坡 collider，必须按非目标记录后续包，而不是临时接入未验证坡面逻辑。
- 不做导航寻路、网络同步协议或预测回滚完整系统。
- 不接入 WGame 真实角色、技能、Buff 或关卡数据。
- 不把 Unity Scene 碰撞体作为权威；Unity 对象只作为 Demo 输入和表现。
- 不用 Console log 作为验收输出。

## 后续非 v0 项

以下内容不纳入本任务验收，后续应按独立功能包规划：

- Capsule / Cylinder 角色代理和更精细的 narrow-phase，而不是仅以 AABB proxy 覆盖 v0。
- 斜坡、坡度限制、台阶上落、边缘吸附、地面贴合、斜坡速度投影。
- 移动平台、旋转平台、动态阻挡体和角色被平台携带。
- 多角色互相阻挡、角色推挤、动态刚体碰撞响应。
- 网络预测 / 回滚 / 输入压缩 / 状态校验协议。
- Root Motion Resolver 与动画状态机的正式融合，包括动画位移优先级、动作锁定、受击位移和击退。
- Gameplay 层能力接入，包括真实技能、Buff、关卡配置和 WGame 业务角色。
- 更完整的 PlayMode 场景矩阵和可视化调试工具，例如轨迹回放、碰撞法线绘制、候选 collider 列表。

## 串行链路

本功能包内部必须保持以下先后顺序：

1. Motion contract：定义 `CombatMotionState`、`CombatMotionInput`、`CombatMotionStepResult`、collision flags、grounded 语义。
2. Deterministic integration：固定帧处理水平输入、重力、跳跃 impulse 和 velocity clamp。
3. Static collision query：基于当前 `CombatPhysicsWorld` 做 capsule / AABB 的保守 sweep 或分轴约束。
4. Collision resolve：处理地面、墙、天花板，输出实际位移、normal、grounded 和 blocked flags。
5. World sync：Motion step 后更新对应 `CombatPhysicsWorld` body position，query / debug report 读取新位置。
6. Showcase：把移动、跳跃、阻挡、攻击探测接到可玩的场景入口。
7. Regression：证明 Motion 新增能力不改变已有 Ray / Sphere / Capsule / AABB / Sector 攻击查询结果。

没有 motion contract 前，不先写 Showcase；没有 collision result DTO 前，不把 HUD 绑定到临时字段；没有 world sync 前，不宣称攻击判定支持移动后位置。

## 可并行工作

在 motion contract 确定后，可以并行派发：

- Motion Core 子代理：实现 state、input、integration、gravity、jump。
- Collision 子代理：实现静态阻挡 sweep / 分轴约束和 collision flags。
- Physics Bridge 子代理：处理 Motion body 与 `CombatPhysicsWorld` 的同步和 query 回归。
- Demo / UI 子代理：搭建小型移动验证场景和 HUD 状态显示。
- Test 子代理：补 EditMode contract tests、determinism tests、PlayMode / visual tree smoke。

这些子任务只通过 Motion contract、Physics query contract 和 StepResult 协作，不互相读取私有容器。

## 验收标准

功能验收：

- 固定输入序列重复运行，最终 position / velocity / grounded / collision flags 完全一致。
- 水平移动能改变 body position，并同步到 `CombatPhysicsWorld`。
- 重力使空中角色下落，落地后 vertical velocity 清零或进入明确的 grounded clamp。
- grounded 状态可起跳；非 grounded 状态不能无限连跳，除非输入中明确配置允许。
- 角色撞墙后不能穿过阻挡体。
- 角色撞顶后停止上升，并在后续帧下落。
- collide-and-slide 或等价响应最多使用固定迭代次数，迭代溢出时有确定性截断结果。
- SkinWidth / epsilon、GroundMinNormalY、CeilingMinNormalY 有固定值并被测试覆盖。
- 移动后触发攻击 / Probe，query origin 和 hit / miss 结果使用新 body position。
- Play Mode Showcase 中能直接演示跑、跳、落地、撞墙和攻击探测。

技术验收：

- Combat Motion Runtime 不引用 `UnityEditor`。
- Combat Motion 权威逻辑不调用 `UnityEngine.Physics`、`Rigidbody` 或 `CharacterController`。
- Motion Core 不依赖 Demo / UI / Gameplay / Authoring。
- 所有排序、候选选择、碰撞选择规则稳定，不依赖 GameObject 遍历顺序。
- 不改变已有 Combat Physics query hit 语义和排序。
- Runtime motion path 复用 broadphase 或等价候选裁剪，不把全量 collider 遍历固化为长期实现。
- Console 最终无 error。

文档验收：

- 本文档记录最终实现范围、测试结果和演示方式。
- Epic 的当前制作优先级指向本功能包。
- 如发现坡面、台阶、移动平台等必须后续实现，只记录为后续功能点，不塞进 v0。

## 测试门槛

最低测试：

```text
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.Combat.Motion.*
Unity EditMode: MxFramework.Tests.Combat.Physics.*
Unity EditMode: MxFramework.Tests.Combat
Unity Console: 0 error
Tools/GitNexus/gitnexus.sh detect-changes
```

必须补充或确认覆盖：

- Motion integration determinism tests。
- Fixed step tests：相同输入序列与相同 step config 多次运行一致。
- Gravity / landing tests。
- Jump / no infinite jump tests。
- Wall block tests。
- Ceiling hit tests。
- Collide-and-slide iteration / tie-breaker tests。
- World sync tests：motion 后 query 使用新 body position。
- Query regression tests：Ray / AABB / Sphere / Capsule / Sector。
- Showcase smoke：HUD 或 runner 可读到 grounded、velocity、collision flags。

验收输出不要只写“测试通过”，必须记录：

- 使用了哪组固定输入；
- 最终 position / velocity / grounded；
- SkinWidth、ground / ceiling normal threshold、最大迭代次数；
- 至少一个撞墙结果；
- 至少一个落地结果；
- 移动后攻击 query 的命中变化；
- Console 最终状态；
- GitNexus 风险等级。

## 完成后演示方式

推荐演示脚本：

1. 打开 Combat Motion Showcase 或当前 Combat 测试场景的 Motion 模式。
2. 控制 Player 在地面上水平移动，确认 HUD position / velocity 更新。
3. 按跳跃键，确认角色上升、离地、下落、落地。
4. 朝墙移动，确认角色被阻挡或沿墙滑动，不穿透。
5. 跳到天花板或低平台下方，确认撞顶后停止上升。
6. 移动到 Enemy 附近，触发 `Probe` 或 `Attack`，确认 query 使用移动后的 Player 位置。
7. 重置并重复同一输入序列，确认结果一致。

演示通过标准：制作人无需阅读测试代码，就能判断“这个角色现在能用框架物理跑、跳、落地、撞墙，并且攻击判定跟随移动位置”。

## 派发提示

派发给子代理时使用功能包口径：

```text
你负责 WGameFramework 功能包 `Combat Motion：Kinematic Character Movement v0` 的实现。
目标不是继续补攻击查询，而是交付确定性角色基础移动：固定帧输入位移、重力、跳跃、grounded、静态阻挡碰撞、world sync 和 Showcase 可玩验证。
先读 AGENTS.md、Docs/COMBAT_ANIMATION_PHYSICS.md、COMBAT_ANIMATION_PHYSICS_EPIC.md、COMBAT_PHYSICS_RUNTIME_WORLD_LIFECYCLE.md、COMBAT_MOTION_KINEMATIC_CHARACTER_MOVEMENT_V0.md。
不要回退他人改动。
只在文档允许范围内修改文件；如需越界，先说明原因。
完成后必须跑 build、Combat Motion / Physics / Combat 测试、Unity Console 检查和 GitNexus，并记录固定输入、落地、撞墙、移动后攻击 query 的验收结果。
```

## 当前记录

- 2026-05-09：创建功能包任务文档。创建时只完成任务定义和验收包，不包含代码实现。
- 2026-05-09：采纳文档审查中合理的实现约束，补充 motion contract 最小字段、固定步长积分、broadphase / sweep / skin width、collide-and-slide、world sync、Showcase 简化场景和 deterministic replay 测试要求。
- 2026-05-09：功能包实现与验收完成，状态收口为 `Completed / Accepted`。已记录 Runtime Motion 实现范围、Showcase 操作方式、固定输入 / 行为验收、Play Mode smoke、Console 和 GitNexus 结果；斜坡、移动平台、动态刚体、Root Motion 正式融合等内容转入后续非 v0 项。
