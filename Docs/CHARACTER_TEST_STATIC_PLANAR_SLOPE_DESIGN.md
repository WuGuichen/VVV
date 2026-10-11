# CharacterTest 静态平面斜坡总设计

> **状态**：Verified / Playable（2026-08-24）；CT-SLOPE-M1～M4全部Verified
> **总交付等级**：`Playable`
> **范围**：静态有限平面斜坡、角色运动、CharacterTestScene 可玩验收
> **当前实现边界**：静态有限平面OBB已贯通Combat Motion、坡面分类/snap、Level v2、CharacterTest Authoring/Scene、snapshot/replay/network clone、PlayMode/NoAlloc与Game View；不扩展AI坡高寻路、movable斜坡动力学、Terrain/mesh/curve/moving platform/IK或Unity Physics authority

## 1. 决策

CharacterTest 第一版斜坡能力采用以下口径：

- 正式运行时形状是 `CombatLevel` 中的静态 OBB；不使用 Unity `Collider`、`CharacterController`、`Rigidbody` 或 `UnityEngine.Physics` 作为权威。
- CharacterTest 世界对象定义继续把 Cube/Box 描述为本地盒体；轴对齐实例编译成 AABB，带有效旋转的静态实例编译成 OBB。
- 不给 `CharacterTestMovableNaturalObjectShapeKind` 增加 OBB，也不宣称可移动自然物体已经支持斜坡接触、滑动或滚动。
- `CombatKinematicMotor` 增加专门的 OBB 运动窄相位，不能让 OBB 落入现有 AABB 默认分支，也不能只使用 broadphase 外包 AABB作为最终接触。
- `CharacterMovementProfileConfig.SlopeLimitDegrees` 继续是坡度阈值权威；首版保持 CharacterTest 默认 `45°`，法线恰好等于阈值时按可行走处理。
- 通用 `CombatMotionConfig` 新增默认关闭的 `GroundSnapDistance`；CharacterTest 首版配置为 `0.10m`，并通过边缘回归证明不会把真正离地错误吸回地面。
- 输入位移、Root Motion、冲量、牵引和其它外力仍进入同一个 CharacterTest Motion 权威；不能建立“普通移动支持斜坡、动作位移绕过斜坡”的并行路径。
- 首版只保证角色运动能使用静态坡面。AI 寻路选择坡面、可移动物体坡面动力学、曲面、三角网格、Terrain、移动平台和脚底 IK 均不在本包内。

推荐交付顺序：

```text
CT-SLOPE-M1 OBB 运动接触基础
  T0 算法合同 -> T1 分离Cast -> T2 Overlap/Skin -> T3 Motor接入 -> T4 收口
        |
        +-------------------+
        v                   v
CT-SLOPE-M2 坡面运动语义   CT-SLOPE-M3 Level / Authoring
        |                   |
        +---------+---------+
                  v
CT-SLOPE-M4 CharacterTestScene Playable 收口
```

M1 是父级里程碑，不直接派发；T0～T4 串行完成并使 M1 进入 `Verified` 后，M2 与 M3 才允许并行。共享接口文档、总设计状态和最终场景由集成负责人串行更新。

## 2. 当前基线

### 2.1 已有能力

- `CombatMotionObbCast`与`CombatKinematicMotor`已交付静态OBB连续movement contact、touch/overlap/skin/fail-closed status、稳定candidate ordering/cache与warmed 0 B；AABB/Sphere/Capsule既有路径保持。
- `CombatMotionConfig`已包含`GroundSnapDistance`与`GroundMinNormalY`；generic Combat default snap仍为0，CharacterTest正式settings/runtime/clone固定为0.10m。`SteepSlope`、`GroundSnap`与typed step options已进入Current合同。
- `CharacterMovementProfileConfig.SlopeLimitDegrees`仍是唯一坡度阈值；CharacterTest默认45°，正式毫度cos阈值raw=`707128`。15°/30°分类为Walkable，50°分类为Steep。
- Combat Level Current schema为v2，正式保存AABB与OBB；strict v1迁移、validator、JSON known-field gate、shape-discriminated content hash和world builder均已闭合。CharacterTest静态Box compiler按完整祖先Transform输出AABB或OBB并fail closed处理invalid transform。
- 正式`CharacterTestScene`通过Level Editor/save coordinator登记三个visible static OBB：`character-test.slope.15deg/.30deg/.50deg`，body=`10080/10081/10082`、collider=`1`、Rough/layer1；Unity Cube只有view/authoring用途且无BoxCollider authority。
- local/AI typed movement、Dash 327 Root Motion、persistent/external motion、gravity与jump/landing最终进入同一`CharacterKinematicPhysicsAdapter -> CombatKinematicMotor`。same-frame typed diagnostic包含identity、normal、classification、threshold、snap、flags、requested/applied/resolved与material。
- snapshot/rollback/replay/hash、Core/Transport physics clone、navigation/movable隔离、完整CharacterTest motion hot path 0 B和正式Game View八段验收均已通过；正式labels与terraced AABB对照已同步。

### 2.2 当前限制

- Runtime AI Planner/navigation graph仍是单一plane/二维派生拓扑；AI若由typed command直接驱动到坡面会服从相同Motor，但不会规划坡高、上下层或坡面路线。
- Movable natural objects不增加OBB shape或斜坡摩擦/支撑/滚动；三个正式坡面只属于static character obstruction authority。
- 不支持dynamic/rotating/moving platforms、curve、Terrain、triangle mesh、任意MeshCollider、通用GJK/EPA、rigidbody或足底IK/坡面动画适配。
- Unity Collider/CharacterController/Rigidbody/Animator Transform不是authority；visible坡面Cube不带BoxCollider。
- generic Combat使用者若不显式配置snap仍为0；CharacterTest 0.10m不能被误写成框架全局默认。
- Combat Level content hash当前用于内容/回归事实，尚无typed network handshake；Core mirror不持有physics world，不能称为physics clone。
- same-frame motion diagnostic是transient projection，可由Replay重算但不进入snapshot/hash；T7真实设备键盘、鼠标、UI hit-test与所有角色包组合不在验收范围。

### 2.3 当前场景解释

`Assets/Scenes/CharacterTestScene.unity`中的平滑坡面区现在是正式Playable样例，不再是visual-only：

- `CharacterTest_Physics_SmoothRamp_15deg/SlopeSurface`与30°对应对象是Walkable静态OBB；50°对象在默认45°限制下是Steep blocker。
- 标签已正式写为`Static OBB slopes · slope limit 45°`、`15° / 30° walkable`、`50° blocked`。
- `CharacterTest_Physics_TerracedRamp`继续是AABB step-up对照，标签为`AABB terraced ramp: small 0.10m increments`；它不是平滑坡面的实现来源。
- 最终Scene SHA-256=`BB6CD1135CE62EEA37DF49D3E37513545A9BB1960EA9CE067514611A467EC026`，Level hash=`12242125253111897828`，authoring content hash=`1e2bd8d6e99ad7bb6610a882c0ad084869be213a53bd4d21b6546833d9f9a10e`。

## 3. 目标与非目标

### 3.1 目标

1. 静态旋转盒体能以确定性 OBB 进入正式 Combat Level 和 `CombatPhysicsWorld`。
2. 角色能在有限 OBB 顶面形成真实接触法线，并连续地上坡、下坡和横穿坡面。
3. 坡度限制由 `SlopeLimitDegrees` 决定；15°/30°可行走，50°在默认 45°限制下不可向上攀爬。
4. 可行走坡面空闲站立不因接触探针或重力产生被动下滑、抖动或 grounded 闪烁。
5. 下坡通过有界 ground snap 保持贴地；越过超过 snap 距离的真实边缘仍会离地。
6. 普通输入、Root Motion、外力、跳跃、落地、摩擦、弹性材料和 step-up 保持单一运动权威。
7. 相同Level内容、初始状态和输入序列产生相同position、velocity、grounded、flags、surface authority与result hash，并能在对应同帧确定性重算相同的contact identity/normal/classification/snap diagnostic；normal本身不是snapshot/hash字段。
8. CharacterTestScene 现有坡面样件升级为可直接操作的 Playable 验收区。

### 3.2 非目标

- 不实现通用刚体、旋转惯性、角速度、堆叠、关节或布娃娃。
- 不实现可移动自然物体在坡面上的支撑、切向重力、滑动或滚动。
- 不实现 Triangle Mesh、Terrain、Heightfield、任意凸体或曲面运动。
- 不实现移动/旋转平台或平台携带角色。
- 不重做 CharacterTest navigation graph；不承诺 Planner 会选择经过坡面的路线。
- 不新增斜坡速度曲线、体力消耗、动画坡度混合或脚底 IK。
- 不改变攻击 query 的命中排序、过滤或 damage 语义。
- 不用 AABB terraced ramp 冒充最终平滑坡面验收。

## 4. 术语

| 术语 | 定义 |
| --- | --- |
| Static OBB | 位于 `CombatPhysicsWorld` 的不可移动有向盒体，含稳定 body/collider identity、half extents、正交轴和 surface material。 |
| Walkable slope | 接触法线 `normal.Y >= GroundMinNormalY` 的向上表面；等于阈值时可行走。 |
| Steep slope | `0 < normal.Y < GroundMinNormalY` 的向上表面；不产生向上的自动攀爬。 |
| Ground snap | 仅在上一状态 grounded、未起跳且当前主位移没有找到支撑时执行的有界向下支撑查询。 |
| Slope projection | 将尚未应用的位移去除进入接触法线的分量，使剩余位移沿接触平面继续。 |
| Authority contact | 由 `CombatKinematicMotor` 和 `CombatPhysicsWorld` 固定帧求得的接触；Unity Transform/Collider 只提供 authoring/view。 |

## 5. 权威链路

正式链路固定为：

```text
Unity / Level Editor static Box transform
  -> CharacterTest Scene Cache / world-object instance
  -> CharacterTestCombatLevelSceneAdapter
  -> CombatLevelDefinition v2 (AABB + OBB)
  -> CombatLevelValidator / CombatLevelHash
  -> CombatLevelWorldBuilder
  -> CombatPhysicsWorld
  -> CharacterControl pre-physics + explicit motion requests
  -> CombatKinematicMotor OBB sweep / slope classification / ground snap
  -> CharacterTest runtime transform / velocity / grounded / flags / surface authority
      -> snapshot / hash / replay / physics-owning network clone
      -> Unity production view transform
  -> same-frame contact identity / normal / classification / snap / requested / applied / resolved
      -> transient diagnostic
      -> replay / restore 后由下一 Tick 确定性重算
```

禁止链路：

```text
Unity BoxCollider / MeshCollider / CharacterController
  -> OnCollision / Physics.Cast
  -> 直接修改 CharacterTest Transform 或 grounded
```

## 6. API 复用计划

| 需求点 | 优先复用 | 本包口径 |
| --- | --- | --- |
| 固定帧 | CharacterTest 现有 fixed-tick、`RuntimeFrame`、`CombatFrame` | 使用；不新增时钟。 |
| 输入 / AI 请求 | CharacterTest input、`CharacterTestCharacterMotionRequest` | 使用；不新增 slope command。 |
| 运动 | `CombatKinematicMotor`、`CombatMotionConfig`、`CombatMotionState` | 扩展 OBB sweep、坡面分类和 ground snap。 |
| 物理世界 | `CombatPhysicsWorld`、`CombatPhysicsObbCollider`、broadphase | 使用；不建立平行 slope world。 |
| Level | `CombatLevelDefinition`、validator、hash、JSON、world builder | schema v2 增加静态 OBB。 |
| Character 配置 | `CharacterMovementProfileConfig.SlopeLimitDegrees` | 继续作为坡度限制；不增加重复字段。 |
| 场景配置 | `CharacterTestScenePhysicsSettings` | 增加 `GroundSnapDistance`，首版 0.10m。 |
| Replay / Hash | 现有 CharacterTest replay、full/owned hash 和 physics clone | 做等价/恢复回归；静态 OBB 纳入 Level content hash。 |
| SaveState | 现有 CharacterTest snapshot | 默认不新增状态；若实现引入跨帧 support identity，必须同步 snapshot/hash/rollback。 |
| UI | 现有场景 TextMesh / debug diagnostics | 无新增 player-facing UI；只更新验证标签和诊断。 |
| Unity Physics | 无 | 不使用。编辑器可用 Transform 作为 authoring 输入，但不得输出权威碰撞结果。 |

## 7. Combat Level v2 静态 OBB 合同

### 7.1 数据结构

`CombatLevelDefinition` 升级到 schema v2，并增加只读 `ObbColliders`。建议新增：

```text
CombatLevelObbColliderDefinition
  StableId
  EntityId
  BodyId
  ColliderId
  Layer
  Position              // body world position
  LocalCenter           // collider center relative to body
  HalfExtents
  AxisX / AxisY / AxisZ // normalized world-space axes, stored as Fix64 raw values
  SurfaceMaterial
```

设计要求：

- body 仍只有平移；OBB 朝向由 collider 轴表达，不向 `CombatPhysicsBody` 增加旋转状态。
- `HalfExtents` 三轴必须严格大于零。
- 三个轴必须非零、归一、两两正交，并形成有效基；非法输入 fail closed。
- Level validator 必须跨 AABB/OBB 检查 stable ID、body/collider identity 冲突。
- content hash 必须按 shape kind + stable identity 稳定排序，并写入全部 raw fixed-point geometry、layer 和 material。
- `CombatLevelWorldBuilder` 只从 validated definition 建 world，不进行 Unity 相关转换。

### 7.2 schema 兼容

- `CurrentSchemaVersion` 升至 2。
- schema v1 JSON 缺少 `obbColliders` 时必须显式迁移为 v2 + empty OBB list；不能让 Newtonsoft 构造器偶然默认值承担兼容合同。
- 未知未来 schema 必须拒绝，不能按 v2 猜测加载。
- v1 -> v2 -> JSON roundtrip 后内容语义保持一致；v1 AABB content hash 的兼容策略必须由 M3 测试锁定并写入接口文档。
- 新增 OBB、改变轴、half extents、identity、layer 或 material 必须改变 v2 content hash；仅改变列表输入顺序不得改变 hash。

### 7.3 CharacterTest 编译规则

CharacterTest 静态 Box 的编译规则为：

1. 定义仍使用现有本地 Box/AABB half extents。
2. 实例 world rotation 与 identity 等价时输出 `CombatLevelAabbColliderDefinition`。
3. 实例存在有效旋转时输出 `CombatLevelObbColliderDefinition`。
4. OBB axis 从 world transform basis 量化为 Fix64 后，由既有 OBB math 归一/校验。
5. 非零负缩放、shear、退化 scale 或无法得到正交 basis 的 transform 首版 fail closed，并输出带 transform path 的结构化诊断。
6. Scene/cache 仍是 Unity 序列化资产权威；不得在 JSON 中复制另一组易漂移的 position/rotation/scale。
7. `CombatLevelHash` 必须反映实际编译后的 OBB geometry，因此场景旋转变化会改变 runtime Level content identity。

该规则不改变 movable object shape：静态 Box 可因实例旋转编译为 OBB，可移动 AABB 仍按当前可移动物体合同处理。

## 8. OBB 运动窄相位合同

### 8.1 必须新增专门分支

`CombatKinematicMotor.TryBuildCollisionFromCandidate` 必须显式处理 `CombatPhysicsShapeKind.Obb`，调用形如 `TrySweepCapsuleObb` 的确定性实现或行为等价实现。

输入：

- character capsule proxy / bounds；
- start position；
- desired delta；
- OBB center、half extents、axes；
- skin width。

输出：

- 是否命中；
- earliest fraction，范围 `[0,1]`；
- distance；
- world-space unit normal；
- 接触目标 identity 和 surface material；
- 初始重叠的稳定退出/阻挡口径。

### 8.2 算法约束

本文不强制某一种数学实现，但实现必须满足：

- 不以固定空间 substep 近似高速移动；不能因步长不同漏撞。
- 不调用 Unity Physics，不使用 platform-dependent float 作为权威结果。
- 如采用 conservative advancement、迭代求根或 GJK cast，迭代次数、停止阈值和 tie-break 必须固定且使用 Fix64。
- broadphase AABB 只用于候选裁剪；最终 fraction/normal 必须来自 OBB narrow phase。
- 同 fraction 多接触继续使用现有 stable identity / normal tie-break；容器顺序不能改变结果。
- 轴对齐 OBB 必须与等价 AABB oracle 在允许的 skin 容差内得到相同 applied delta 和分类。
- OBB 顶面、侧面、底面、边、角、初始轻微重叠和高速穿越必须有直接测试。
- warmed 运动路径维持 0 B GC allocation。

M1 实施前允许在任务文档中补充算法附录；若算法改变这里的可观察合同，必须先更新本总设计，不能由实现细节暗中决定行为。

## 9. 坡面分类与 flags

### 9.1 分类顺序

碰撞分类必须先判断 Y 语义，再决定是否标记水平墙面：

1. `normal.Y >= GroundMinNormalY`：walkable ground。
2. `0 < normal.Y < GroundMinNormalY`：steep slope。
3. `normal.Y <= -CeilingMinNormalY`：ceiling。
4. 其它接近竖直的表面：wall。

walkable slope：

- 设置 `Grounded | BlockedY`。
- 不能仅因 normal.X/Z 非零而设置 `Wall / BlockedX / BlockedZ`。
- 仍保留实际 normal，供投影、material 和 diagnostics 使用。

steep slope：

- 设置新 flag `SteepSlope`，并保留 `Wall` 兼容语义。
- 不设置 `Grounded`。
- 不允许水平输入投影出正向上坡位移。
- 可以保留已有向下速度并沿接触面解析，但首版不增加独立“斜坡滑落加速度”参数。

建议新增：

```text
CombatMotionCollisionFlags.SteepSlope = 1 << 8
CombatMotionCollisionFlags.GroundSnap = 1 << 9
```

如实现证明无需新 flag，也必须提供等价 typed diagnostics；不能用字符串推断 steep/snap。

### 9.2 阈值

- `normal.Y == GroundMinNormalY` 时可行走。
- CharacterTest 默认 45°，因此 45°边界样例必须有明确测试。
- 计算阈值继续使用现有毫度 -> Fix64 cosine 路径，不在热路径使用 `Mathf.Cos`。
- 非法 `SlopeLimitDegrees` 沿用现有 clamp；本包不改变 profile JSON 兼容格式。

## 10. 坡面运动语义

### 10.1 上坡

- 对 walkable slope，剩余位移使用现有 `ProjectOnPlane` 语义继续。
- 首版不对投影后的切向位移重新归一化，因此不额外注入速度；坡度速度曲线后续单独设计。
- projected delta 可以产生正 Y 位移，但只允许来自 walkable surface。
- step-up 仍只处理现有 AABB 台阶；不能把 steep OBB 当成可跨越台阶。

### 10.2 陡坡

- 输入撞向 steep slope 时不得因为平面投影产生正 Y 攀爬。
- steep slope 可以阻断法线方向并保留切向横移/向下分量。
- 空中角色落到 steep slope 时保持非 grounded；后续可以受现有重力向下离开或沿面解析。
- 首版不承诺角色在 steep slope 上以指定加速度滑落，只承诺不能稳定站立或向上攀爬。

### 10.3 空闲站立

- 上一帧 grounded 且没有水平/外力/Root Motion 意图时，向下支撑检查不得投影出横向滑动。
- walkable slope 上连续空闲帧的 X/Z 必须稳定在确定性容差内，grounded 不闪烁。
- surface friction 继续复用现有 material；本包不增加切向静摩擦/动摩擦模型。

### 10.4 Ground snap

`GroundSnapDistance` 放入 `CombatMotionConfig`：

- 非负 Fix64；generic default 为 `0`，保证旧 consumer 行为兼容。
- 纳入 config equality/hash 和 motor cache key。
- CharacterTest 通过 `CharacterTestScenePhysicsSettings` 设置为 `0.10m`。

snap 仅在以下条件全部满足时执行：

- 上一运动状态 grounded；
- 本 tick 未开始 jump；
- 没有明确向上 vertical/root-motion/external request；
- 主位移后尚未找到 walkable support；
- 向下范围内第一个合法接触为 walkable surface。

snap 结果：

- 应用带 skin 的安全位移；
- 设置 `Grounded | BlockedY | GroundSnap`；
- 写入真实 contact normal、target identity 和 material；
- 不把 snap 位移变为持久下落速度；
- 不跨越大于配置距离的边缘，不选择 steep slope，不穿过较近 ceiling/obstacle。

### 10.5 跳跃、Root Motion 与外力

- jump started 当 tick 禁止 ground snap。
- Root Motion 继续作为当帧 delta；经过同一 OBB sweep 和 slope limit，不持久化为速度。
- `StaticOnly` Root Motion obstacle policy 必须包含静态 OBB。
- 冲量、击退、牵引和其它外力不得使用 slope bypass；向上外力可以离地，向下落地按 surface classification 处理。
- BouncePad/Elastic 在坡面上的反弹使用实际 OBB normal；首版至少覆盖不会错误标记 grounded 的回归，复杂斜向弹跳手感不另增规则。

## 11. 确定性、snapshot、hash 与网络边界

### 11.1 静态配置

- OBB geometry 是静态 Level 配置，不是每帧 snapshot body state。
- `CombatLevelHash` 必须覆盖 OBB identity、geometry、layer 和 material。
- 任务必须审计当前 CharacterTest session/replay 配置身份如何携带静态 Level hash；若现有 AABB 只进入诊断字符串，不得在本包中虚构已经存在的网络握手保证。
- 本包只沿现有静态 Level identity 边界扩展；若发现正式 session compatibility 完全缺少 Level hash，记录为独立阻断或经设计批准后补最小强类型字段，不能解析 `SourceDescription` 字符串作为权威。

### 11.2 动态状态

- position、velocity、grounded、collision flags和surface material沿用现有CharacterTest状态/快照，并进入对应full/owned/non-owned hash边界。
- last contact identity/normal、surface classification、GroundSnap applied distance与requested/applied/resolved motion属于same-frame transient diagnostic；它们不进入snapshot/hash/network，restore/replay后由下一Tick确定性重算。
- 若 `SteepSlope` / `GroundSnap` 只表示本帧结果，可复用 flags；若新增 support body/collider identity、sticky timer 或其它会影响未来 Tick 的状态，必须进入 full/owned hash、snapshot、rollback 和 replay。
- physics world clone、server/client runner 和 rollback 重建必须保留静态 OBB；现有 clone 已复制 OBB，任务仍需增加实际坡面回归。

### 11.3 验证

- 相同输入重复运行结果完全一致。
- 中途 capture/restore 后继续运行与不中断基线一致。
- collider 插入顺序、AABB/OBB 列表顺序和 broadphase rebuild 不改变结果。
- 30/60/120 渲染更新节奏下，相同 fixed-tick command 序列得到相同 authority hash。

## 12. Authoring 与场景

### 12.1 正式写入路径

- `.unity` 和 Scene cache 必须通过 Unity Editor、Unity MCP 或现有 Level Editor/save coordinator 修改。
- 不手写场景 YAML、fileID 或 `.meta`。
- `character_test_scene.world_objects.json` 的更新必须通过现有 world-object authoring/save flow，并与 Scene transform refs 一致。
- 场景 Transform 是 authoring 输入；runtime 只消费编译后的 Fix64 Level definition。

### 12.2 CharacterTestScene 验收区

复用现有：

- `CharacterTest_Physics_SmoothRamp_15deg`
- `CharacterTest_Physics_SmoothRamp_30deg`
- `CharacterTest_Physics_SmoothRamp_50deg_Blocker`

M4 已完成以下落地：

- 三者已注册为稳定single-static collider instance：body/collider=`10080.1/10081.1/10082.1`。
- visible `SlopeSurface` Cube与Transform继续作为authoring/view，正式physics只消费编译后的Fix64 OBB。
- labels已通过Unity正式写入并在重新进入Play后读取确认；terraced ramp继续作为AABB step-up对照。
- 两条既有navigation route在默认与production-maximum clearance下node/edge序列不变；这只证明非退化，不表示Planner理解坡高。
- 正式8个movable与三个OBB保持2m conservative guard；movable static-obstacle cache继续只复制AABB，不暗示slope dynamics。

## 13. Diagnostics

至少提供以下 typed 或稳定结构化证据：

- target body/collider ID；
- target shape kind = OBB；
- contact normal；
- normal.Y 与 `GroundMinNormalY`；
- surface classification：walkable / steep / wall / ceiling；
- ground snap 是否执行及实际距离；
- grounded、collision flags、applied delta、resolved velocity；
- surface material；
- broadphase/narrowphase candidate counts 继续沿用现有诊断。

Debug 信息是只读投影，不得反向控制运动。

## 14. AI、Navigation 与可移动物体边界

### 14.1 AI

所有 CharacterTest 角色实体最终使用同一 Motion，因此 AI command 若把角色直接驱动到坡面，物理约束仍应正确。但是当前 navigation graph 使用单一 `PlaneYMillimeters`，本包不承诺：

- graph 节点沿坡面取得不同 Y；
- A* 选择上坡/下坡路径；
- path cost 计入坡度；
- Planner 理解坡顶可达性。

正式 AI 坡面寻路必须另起 Height-aware Navigation 设计。M4 只做现有 AI 路线不退化的回归。

### 14.2 可移动自然物体

当前可移动自然物体的静态接触模型以 AABB/Sphere/Capsule 支撑和水平 top/bottom plane 为主。本包不扩展：

- OBB 支撑高度；
- 切向重力；
- 斜坡摩擦；
- 旋转或滚动；
- 动态物体堆叠。

不得用“角色可走 OBB”推导“所有物体已支持斜坡”。

## 15. 跨里程碑验收矩阵

| 能力 | M1 | M2 | M3 | M4 |
| --- | --- | --- | --- | --- |
| OBB motor narrow phase | 实现并完成纯 Combat oracle | 回归 | Level 实例可进入 world | 场景实测 |
| Walkable/steep 分类 | 命中 normal 可用 | 实现 15/30/45/50 | 配置/轴正确 | Game View 可见 |
| Ground snap | 不做 | 实现、边缘回归 | 配置桥接预留 | CharacterTest 0.10m 实测 |
| Combat Level OBB | 不做 | 不做 | schema v2、validator/hash/json/builder | 正式场景内容 |
| CharacterTest motion requests | 不做 | adapter contract 回归 | 不做 | input/root motion/外力集成 |
| Snapshot/replay/hash | 纯 motor determinism | state/snap determinism | level content hash | full/owned/restore/playable |
| Performance | warmed OBB sweep 0 B | warmed slope/snap 0 B | build 为低频；runtime query 无新增每帧分配 | 完整 CharacterTest hot path 0 B |
| Unity | compile/Console | compile/Console | authoring/cache tests + compile | Scene、PlayMode、Game View |

## 16. 任务路由与唯一状态

| 里程碑 | 当前状态 | 交付等级 | 执行文档 | 前置 | 主要所有权 |
| --- | --- | --- | --- | --- | --- |
| CT-SLOPE-M1 | `Verified（2026-08-24）` | Framework Feature | [OBB 运动接触里程碑](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_OBB_MOTION_SWEEP.md) | 本设计 | T0～T4算法、primitive、鲁棒性、Motor接入和收口均已完成 |
| CT-SLOPE-M2 | `Verified（2026-08-24）` | Framework Feature | [坡面运动语义](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M2_MOTION_SEMANTICS.md) | M1 Verified | T0～T5与M4 fixed-plane/live-edge窄回流均已闭合 |
| CT-SLOPE-M3 | `Verified（2026-08-24）` | Framework Feature | [Level 与 Authoring](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M3_LEVEL_AUTHORING.md) | M1 Verified | T0～T5：Level v2、compiler、authoring/preview与closeout均已完成 |
| CT-SLOPE-M4 | `Verified / Playable（2026-08-24）` | Playable | [CharacterTestScene 验收收口](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M4_PLAYABLE_ACCEPTANCE.md) | M2 + M3 Verified | T0～T8：CharacterTest integration、scene/config、PlayMode/NoAlloc、Game View与Current文档全部完成 |

M1 子任务：

| 子任务 | 初始状态 | 执行文档 | 单一交付 |
| --- | --- | --- | --- |
| CT-SLOPE-M1-T0 | `Verified` | [算法与 Oracle 合同](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_T0_ALGORITHM_CONTRACT.md) | directed raw support gap/closing bounds、capacity envelope、P01/P02与F01～F16已冻结 |
| CT-SLOPE-M1-T1 | `Verified` | [初始分离连续 Cast](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_T1_SEPARATED_CAST_CORE.md) | 25/25、capacity/support/one-sided Oracle、多路径0 B、独立复验无P0/P1、根Unity验证通过 |
| CT-SLOPE-M1-T2 | `Verified` | [Overlap、Skin 与等价性](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_T2_OVERLAP_SKIN_ORACLE.md) | 61/61、完整六方向矩阵、raw tangent反例、threshold双侧、0 B、独立复验无P0/P1/P2、根Unity验证通过 |
| CT-SLOPE-M1-T3 | `Verified` | [Motor 候选接入](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_T3_MOTOR_INTEGRATION.md) | 141/141、穷尽route、双接触、dynamic结果型cache、skin单扣、独立复验P0/P1为0、根Unity验证通过 |
| CT-SLOPE-M1-T4 | `Verified` | [回归与收口](Tasks/CHARACTER_TEST_STATIC_PLANAR_SLOPE_M1_T4_REGRESSION_CLOSEOUT.md) | root最终149/149、functional 93/93、真实narrowphase miss/fallback、Motor 0 B、Unity Console 0；12项current-worktree外部例外已精确记录 |

状态流转：

```text
Ready for dispatch -> In Progress -> Implementation Complete -> Verified
```

只有实际实现、测试、Unity 和对应交付等级证据全部闭合时才可进入 `Verified`。文档计划完成不等于运行时能力完成。

## 17. 分布执行与文件所有权

### 17.1 允许并行的阶段

- M1-T0～T4 串行执行；父级M1不直接派发。
- M1 Verified 后，M2 与 M3 可并行处理不重叠源码/测试。
- M4 必须等待 M2/M3 合并并复验后执行。

### 17.2 共享文件

以下文件由集成负责人串行维护，分布任务执行者不得并发编辑：

- 本总设计；
- `Docs/PROJECT_INDEX.md`；
- `Docs/Interfaces/Combat.md`；
- `Docs/CAPABILITIES.md`；
- `Docs/USAGE.md`；
- `Assets/Scenes/CharacterTestScene.unity`；
- `Assets/Config/MxFramework/Levels/CharacterTest/character_test_scene.world_objects.json`。

M1/M2/M3 如果需要接口回写，应在各自任务文档的 closeout 区记录精确条目；由集成负责人按依赖顺序写入共享接口文档。M4 负责最终 Current/Capability/Usage 收口。

## 18. 风险与阻断门槛

| 风险 | 门槛 |
| --- | --- |
| OBB sweep 只能靠 substep 才工作 | M1 不得完成；必须提供连续/保守 shape cast 和高速 oracle。 |
| 轴对齐 OBB 与 AABB 行为明显不等价 | M1 不得完成；先修正 fraction/skin/normal。 |
| idle 在 walkable slope 上滑动或抖动 | M2 不得完成。 |
| 50°仍能通过投影产生上升 | M2 不得完成。 |
| snap 把角色跨越真实边缘吸回地面 | M2 不得完成。 |
| Level v1 无法显式迁移 | M3 不得完成。 |
| 旋转/scale 编译结果不进入 content hash | M3 不得完成。 |
| 场景修改绕过 Level Editor/save coordinator | M4 不得完成。 |
| OBB 改变现有 AI graph/路线且未解释 | M4 不得完成。 |
| 只通过测试、没有真实 CharacterTestScene 操作证据 | 总包只能停在 Runtime Slice，不能标记 Playable。 |

## 19. 最终文档回写

M4-T8已按实际实现完成以下同步：

- `Docs/Interfaces/Combat.md`：Level v2 OBB、Motion slope/snap、flags、限制和测试入口。
- `Docs/CAPABILITIES.md`：只有 Playable 验收后新增静态平面斜坡能力；不能在 M1-M3 提前标记。
- `Docs/USAGE.md`：CharacterTestScene 打开方式、坡面区域、操作和观察项。
- `Docs/Guides/CHARACTER_TEST_GAME_DEVELOPMENT_STANDARD.md`：仅当权威运动规则或未来任务标准需要永久补充时更新。
- 各子任务 closeout：实际文件、测试数量、Unity job、Game View 证据、性能、限制和提交。

## 20. 总完成定义

整个静态平面斜坡包只有同时满足以下条件才算完成：

- M1-M4 全部 `Verified`。
- CharacterTestScene 中 15°/30°可以稳定上下行、横穿和空闲站立。
- 默认 45°阈值下 50°不能向上攀爬。
- 上坡、下坡、坡底/坡顶接缝、真实边缘、跳跃、Root Motion 和至少一种外力经过同一权威路径。
- Level OBB JSON/validator/hash/world builder、旧 v1 兼容和场景编译闭合。
- 固定输入 determinism、snapshot restore、replay/hash、network physics clone 回归闭合。
- warmed 运动热路径 0 B GC；低频 Level build/JSON 允许按既有 `AllocByDesign` 口径记录。
- Unity refresh/compile/domain reload/Editor ready/Console 0 error。
- CharacterTestScene Play Mode smoke 和直接 Game View 操作证据存在。
- 当前不支持的 AI slope navigation、movable slope dynamics、curve/terrain/platform/IK 被明确保留为非目标。

当前工作区已满足以上定义；最终状态为`Verified / Playable`。交付尚未提交或推送，不能把dirty工作区状态描述为已发布commit。

## 21. 状态历史

| 日期 | 状态变化 | 证据 | 限制 |
| --- | --- | --- | --- |
| 2026-08-23 | 初始化 -> `Planned / Design Locked` | 完成当前场景、Combat Motion、OBB query、Level v1、CharacterTest authoring、navigation 和 movable contact 的源码/文档审计；创建 M1-M4 执行包 | 未修改运行时代码/场景；未运行坡面原型、Unity 测试或 Play Mode；平滑坡面仍是 visual-only |
| 2026-08-23 | M1 `Ready for dispatch` -> `Planned / Router`；M1-T0 -> `Ready for dispatch` | 复核M1同时包含未决算法、几何primitive、overlap/skin、Motor接入、回归与性能五类独立证明义务；拆成T0～T4串行子任务 | 尚未实施生产代码；M2/M3继续等待父级M1 Verified |
| 2026-08-23 | M1-T0 `Ready for dispatch` -> `Verified`；M1-T1 -> `Ready for dispatch` | `gpt-5.6-sol / medium` 子代理完成算法合同；根审查退回并修正全局速度near-miss假命中与fully-inside/AABB深度冲突后通过；冻结separating-plane CA、40轮、16 raw阈值及F01～F16 | documentation-only，未运行tests/Unity；T1必须继续证明Fix64舍入下不会over-advance |
| 2026-08-23 | M1-T0 `Verified` -> `In Progress / Reopened`；M1-T1 -> `Planned / Waiting for T0` | 独立`gpt-5.6-sol / medium` T1子代理在实现前构造逐raw反例：普通Fix64 Normalize/Dot会低估closing projection，冻结advance可晚于精确support-plane上限1～1004 raw fraction；按合同停止并撤销全部草稿 | T0必须冻结world-space support gap下界、closing上界、定向舍入和overflow范围；无生产代码保留 |
| 2026-08-23 | M1-T0 `In Progress / Reopened` -> `Verified`；M1-T1 -> `Ready for dispatch` | T0改为world-space未归一化witness raw²证明：sqrt floor/ceil、support gap lower、closing ceil、signed end-gap floor、advance floor；P01输出500000不超过500667，P02输出61615；冻结2000m world/500m local/1100m delta容量 | documentation-only，未运行tests/Unity；T1必须编码全部raw helpers、capacity preflight和P01/P02硬门槛 |
| 2026-08-23 | M1-T1 `Ready for dispatch` -> `Verified` -> `In Progress / Reopened` | 实现子代理交付19/19、direct 0 B、Unity validate/Console 0；独立只读审查随后复现capacity内segment-edge `a*e-b*b` long溢出，并证明F03～F06双向容差可接受晚于数学接触的结果 | 修复edge solver中间算术或重新证明envelope；为每个hit增加独立one-sided conservative Oracle后重新验收；T2未解锁 |
| 2026-08-23 | M1-T1 `In Progress / Reopened` -> `Verified`；M1-T2 -> `Ready for dispatch` | 修复edge raw²溢出、改用原始world segment/OBB support上下界、补one-sided解析Oracle及basis/大坐标压力；独立复验原P1修复且无P0/P1，P2断言加固；根复跑25/25 job `2c2ac65739b74ad3ae1b36e984f7c436`和Unity validate job `5a5ca6b6e8464a0fa0161b3334d2d0b6`通过 | rotated/edge/corner常以PrecisionLimit保守命中，T2/T3必须作为阻挡hit消费且T4持续观察提前量 |
| 2026-08-23 | M1-T2 `Ready for dispatch` -> `Implemented / Verification blocked` -> `In Progress / Reopened` | 实现子代理交付T1+T2 53/53和0 B，但独立审查发现六方向AABB等价矩阵与负侧16/17 raw证据不完整；根另构造`w=(100000,700000)`切向delta反例，证明使用归一化normal点积会把精确tangent误判为into | 改用原始witness raw dot判方向，补完整矩阵及Console 0后才能Verified；T3未解锁 |
| 2026-08-23 | M1-T2 `In Progress / Reopened` -> `Verified`；M1-T3 -> `Ready for dispatch` | 修复raw tangent方向，补±X/Y/Z touch/overlap/inside/三skin矩阵与threshold正负边界；T0同步F16 Y勘误；独立复验残余P0/P1/P2均0；根复跑61/61 job `6f0337b957cc46959eea3e650861cdb9`和Unity validate job `8f2923e8ed084245a569e90ad7f53cdc`通过 | 两种conservative limit继续作为阻挡true，T3只能扣skin一次且必须同时消费bool/status |
| 2026-08-24 | M1-T3 `Ready for dispatch` -> `Verified` -> `In Progress / Reopened` | 实现子代理交付T3 20/20、T1/T2 61/61、Motor 58/58及Unity Console 0；独立审查确认branch主干正确，但bool/status未穷尽fail-fast，fraction-0双接触测试不足以证明第二接触，dynamic/static cache Oracle不会改变结果 | 补状态route matrix、双identity/位置/flags/last-normal Oracle和结果型cache移动/重建；术语改为未应用skin的conservative primitive contact；T4未解锁 |
| 2026-08-24 | M1-T3 `In Progress / Reopened` -> `Verified`；M1-T4 -> `Ready for dispatch` | 补穷尽bool/status fail-fast、双fraction-0接触强Oracle、dynamic/static结果型cache与conservative contact术语；独立复验P0/P1为0，仅留static rebuild cached-vs-uncached完整相等P2给T4；根复跑141/141 job `6d9658cbbe034e4892ad068b548f75db`和Unity validate job `b2bda804409e47db8e6b6f6352b71f30`通过 | T4需补完整cache/rebuild equality、OBB hot-path NoAlloc、长序列determinism、接口回写和父级收口 |
| 2026-08-24 | M1-T4 `Ready for dispatch` -> `Verified`；父级M1 -> `Verified`；M2/M3 -> `Ready for dispatch` | 加固static rebuild完整结果相等、真实broadphase+narrowphase miss NoAlloc、真实primitive iteration-budget耗尽与normal Oracle；独立复验P0/P1/P2为0；root最终focused job `e62a761d83de4ce29a361903786cd1fe`为149/149，functional impact job `19716724596048ef99a8d01224911614`为93/93，Unity validate job `5ba8486c53d74dc2aa7cd9893bed52c5`通过，Motor热路径0 B，256 tick hash `3496946136420153923` | 当前仍没有walkable/steep、Ground Snap、Level OBB、CharacterTestScene或Playable；广域337项中的10项并行CharacterAction迁移失败与2项既有Physics query P95失败作为current-worktree外部例外保留，未声称337/337 |
| 2026-08-24 | M2/M3 `Ready for dispatch` -> `Planned / Router`；两条T0 -> `Verified`；两条T1 -> `Ready for dispatch` | 复核M2含config/flags、projection、Ground Snap、CharacterControl和closeout六类证明；M3含Level DTO、JSON/hash/world、Transform compiler、authoring/save/preview和closeout六类证明，分别拆为T0～T5。两轮`gpt-5.6-sol / medium`独立反例复核修正M2 raw残差/零阈值/typed intent/comparator与M3全祖先scale/JSON token/axis overflow/scale-source/save seam；修订后交叉复核M2 P0/P1/P2为0、M3 P0/P1为0，root补清M3两个诊断P2 | documentation-only，未运行tests/Unity；当前只派发M2-T1与M3-T1，M4继续等待两个父里程碑Verified |
| 2026-08-24 | M2-T1/M3-T1 -> `Verified`；两条T2 -> `Ready for dispatch` | M2交付GroundSnapDistance、bits与typed step options并保持旧constructor/Step API；M3交付Level schema v2、OBB DTO与checked-safe跨shape validator。独立实现复核均P0/P1为0；root补强constructor metadata、AABB/OBB priority和handedness overflow tests；最终combined job `62aaef02a2b44ec7ba213ade1af6b151`为22/22，Unity validate `b1b1238cbeb74d48bb650df73d9e9f94`通过 | T1尚未改变坡面运动、JSON/hash/world builder、CharacterTest或Scene；M2 options的eligible行为由T2/T3开始验证 |
| 2026-08-24 | M2-T2/M3-T2 -> `Verified`；两条T3 -> `Ready for dispatch` | M2交付Y-first分类、directed raw non-entering projection和support-only分流；首轮因缺真实rotated OBB Motor证据重开，补15°/30°连续上坡、横穿、50°、fraction-0与correction极值。M3交付strict v1/v2 migration、shape hash和OBB world；首轮因duplicate property覆盖重开，补parse-stage拒绝、逐component hash和invalid world no-partial。第二轮独立复核两边P0/P1/P2均为0；root最终job `c4708581fd4b4737b2162ca37ab94a7e`为203/203，Unity validate `c0ec7e47f49f44b08b982beec75397bb`通过 | M2尚未执行Ground Snap query，M3尚未编译CharacterTest Transform；M4仍等待父级M2/M3 Verified |
| 2026-08-24 | M2-T3/M3-T3 -> `Verified`；两条T4 -> `Ready for dispatch` | M2交付纯Y Ground Snap、一次skin、earliest-only、cache/material/bounce与warmed cached 0 B；下坡Oracle按skin修正为非增且最终下降，独立复核P0/P1/P2为0。M3交付identity AABB/rotated OBB compiler、完整祖先transform门禁与typed diagnostics；先修nonfinite伪fixture，再修finite extreme Fix64量化越界，并补quantized basis、rotated cache与OBB-only mask；复核P0/P1/P2为0。root adapter job `7aceeec9cea142c6a9eb4838869f0c5f`为26/26、combined job `0116902cd4ec423589ee567e233f02d0`为250/250、Unity validate `7b7542c5aa5148a59580634eb7f1acee`通过 | M2尚未迁移CharacterControl旧probe；M3尚未接入save preflight与真实OBB preview；正式Scene未修改 |
| 2026-08-24 | M2/M3全部子任务 -> `Verified`；M4-T0～T5 -> `Verified` | M2/M3最终combined `324/324`；M4完成snap runtime clone、三条正式OBB authoring/Scene、motion diagnostics、snapshot/replay/network、navigation/movable隔离；T5 formal `9/9`与相关`63/63`通过 | 仍未把PlayMode或Game View证据互相替代；M4-T6后续独立执行 |
| 2026-08-24 | M4-T6 -> `Verified`；M4-T7 -> `Verified` | live-edge窄回流后任务相关`301/301`，最终行为+NoAlloc`12/12` job `3d89461205df46e694a77a7a58822d05`；T7正式报告`8/8`、八张2560x1440 Game View、view误差0/0、无IterationLimit；Unity job `3f024fda6bf44f5095d8495534fa5a0d` Console 0；独立T7终审P0/P1/P2=0 | live aggregate dot按skin+最终separation验证，T6 canonical严格projection不变；真实设备输入与所有角色未穷举 |
| 2026-08-24 | M4-T8及父级M4 -> `Verified / Playable` | Scene/Level/authoring hash、正式labels、Combat interface、Capabilities、Usage、Project Index、父子状态与限制已串行同步 | 未创建commit或push；AI坡高寻路、movable slope dynamics、Terrain/mesh/curve/platform/IK继续不支持 |
| 2026-08-24 | Playable后贴墙移动回归 -> fixed | 普通Wall按skin-safe remaining time保留切向，active locomotion使用default0 typed floor并限制为`min(simulated,target)`；Root/Impulse/external与Steep/Walkable/Ceiling旧合同不变，AABB/OBB双非进入、真实corner和0.55m blocker继续阻挡。wall/gate `9/9`、Motor/CharacterControl `167/167`、slope/CharacterTest `46/46`、行为+NoAlloc `13/13`通过；独立终审P0/P1/P2=0 | 最终源码compile/domain ready，Console clean被任务外`vfx.magic.fireball_burst.charge` stale阻断；未修改VFX或Scene布局 |
| 2026-08-24 | 30°/50°斜坡夹缝冻结 -> fixed | 保留正式Scene原1m夹缝与原Scene/Level/navigation hash；修复两个相向零距离OBB墙约束交替导致的`IterationLimit`。仅当active输入存在足够共同切线时忽略该对重叠body重算本帧，其余body仍参与；横向挤压、正交corner和单墙不放宽。通用相向OBB夹缝切线/横向阻挡`2/2`、正式CombatGirl原夹缝双向退出`2/2`、Motor+CharacterControl+完整slope combined `205/205` job `6943d296ddc544d6a85aec638c304023`通过 | 未移动30°/50°坡体；Scene SHA-256仍为`BB6CD1135CE62EEA37DF49D3E37513545A9BB1960EA9CE067514611A467EC026` |
| 2026-08-24 | 夹缝修复后T7复验 -> `Verified` | 当前代码与原1m夹缝下重新执行正式active Scene八段Game View；`latest.json`为`8/8`、Level hash=`12242125253111897828`、view position/yaw误差=`0/0`、IterationLimit sample=`0`。新截图为`t7-gapfix-verified-01-15-up-stop-down-1.png`及`t7-narrow-gap-verified-02...08...png` | 自动流程未新增夹缝专用可视段；夹缝本身由正式CombatGirl双向90 tick回归覆盖 |
| 2026-08-25 | 用户现场夹缝姿态回流 -> fixed | 直接读取live Game View与authority：CombatGirl位于raw mm=`(-23482,2051,16661)`，idle时为`BlockedX/Y/Z + Wall + SteepSlope + IterationLimit`；碰撞序列含30° OBB法线`(0.358161,0.620352,0.697772)`非零fraction、50° OBB法线`(0,0,-1)`零fraction，证明前版“两个零距离墙”判定过窄。Motor现按不同body的相向水平约束识别复合夹缝；无出口切线时提前确定性停止，有出口切线时忽略该对body重算，其余碰撞仍参与。冻结live姿态idle `1/1`、双向出口`2/2`，加原地面姿态双向、通用墙和恢复路径NoAlloc共`15/15`；Motor/CharacterControl/完整slope `209/209` job `300c166534f14237b8846db67de4dddb`通过 | 最终Unity compile/domain ready/Console error 0 job `6efb54b8bdc645b8b382498f68777c91` |
