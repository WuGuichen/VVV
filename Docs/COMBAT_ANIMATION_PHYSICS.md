# Combat Animation Physics 开发设计

> 版本 0.1.1 | 2026-05-08
>
> 本文档定义 MxFramework 面向动作战斗的确定性动画与物理协作方案。目标不是实现通用 3D 刚体物理，而是实现可回放、可联网、可调试的战斗物理和动作时间轴。

## 1. 设计目标

Combat Animation Physics 的核心目标是让战斗逻辑由固定帧时间轴驱动，动画和物理只作为该时间轴的两个协作模块。

必须满足：

- 单机、联机、回放、服务器复用同一套战斗计算。
- 战斗判定不直接依赖 Unity `Animator` 当前状态。
- 伤害、硬直、击退、格挡、振刀、Buff 触发不直接依赖 Unity `Physics.Raycast`。
- 动作帧、命中帧、取消窗口、无敌帧、霸体帧、武器轨迹都可配置、可烘焙、可回放。
- 高频战斗查询默认无 GC 分配。
- 所有命中候选和结算结果具备稳定排序规则。

明确非目标：

- 不做完整 3D Rigidbody、摩擦、堆叠、弹性、关节或布娃娃模拟。
- 不替代 Unity Animator 的表现能力。
- 不把 WGame 具体角色、技能、元素体系或私有配置带入框架。
- 不要求所有视觉物理表现确定性，只有权威战斗逻辑必须确定。

## 2. 总体架构

```text
CombatWorld
  FixedFrameClock
  EntityStateStore
  ActionSystem
  MotionSystem
  CombatPhysicsWorld
  HitDetectionSystem
  HitResolveSystem
  Ability/Buff/Modifier bridge
  CombatEventStream

UnityPresentation
  AnimatorDriver
  TransformDriver
  VfxDriver
  AudioDriver
  CameraDriver
  DebugGizmos
```

依赖方向：

```text
Deterministic Math
  <- Combat Physics
  <- Combat Animation Timeline
  <- Combat Runtime
  <- Unity Presentation
```

约束：

- `Combat Runtime` 不读取 `Time.deltaTime`，只接收固定帧。
- `Combat Physics` 不引用 `UnityEngine.Physics`。
- `Unity Presentation` 可以引用 UnityEngine，但不得反向驱动权威战斗结果。
- `Gameplay Ability/Buff/Modifier` 可以消费 CombatEvent，也可以发起动作请求，但命中查询必须通过 Combat Physics。

## 3. 固定帧时钟

战斗世界只认固定逻辑帧：

```csharp
public readonly struct CombatFrame
{
    public readonly int Index;
    public readonly int DeltaTicks;
}
```

推荐默认：

```text
LogicFps = 30 或 60
DeltaTicks = 1
```

持续时间表达优先级：

1. 动作、取消、命中窗口使用 frame。
2. Buff/CD 等长期计时可用 frame 或 millisecond tick，但必须由 fixed clock 推进。
3. 表现层可以使用 seconds 插值，但不得影响权威结果。

## 4. 战斗动作时间轴

每个动作由 `CombatActionTimeline` 描述，而不是由 Animator Clip 直接作为权威。

```csharp
public sealed class CombatActionTimeline
{
    public int ActionId;
    public int TotalFrames;
    public FrameRange Startup;
    public FrameRange Active;
    public FrameRange Recovery;
    public CancelWindow[] CancelWindows;
    public HitboxTrack[] Hitboxes;
    public HurtboxTrack[] Hurtboxes;
    public WeaponTraceTrack[] WeaponTraces;
    public MotionTrack RootMotion;
    public StateWindow[] SuperArmorWindows;
    public StateWindow[] InvincibleWindows;
    public StateWindow[] ParryWindows;
    public CombatTimelineEvent[] Events;
}
```

动作帧语义：

| 阶段 | 说明 |
| --- | --- |
| Startup | 前摇，通常可被更高优先级事件打断 |
| Active | 产生命中查询或状态效果 |
| Recovery | 后摇，允许按取消窗口进入后续动作 |
| CancelWindow | 可取消到指定动作集合的帧段 |
| SuperArmorWindow | 霸体窗口，影响受击硬直/打断 |
| InvincibleWindow | 无敌窗口，影响命中有效性 |
| ParryWindow | 格挡/振刀/反制窗口 |

权威动作状态：

```csharp
public struct CombatActionState
{
    public int EntityId;
    public int ActionId;
    public int LocalFrame;
    public int StartedAtFrame;
    public CombatActionPhase Phase;
}
```

## 5. 动画数据来源

Unity AnimationClip 是表现和烘焙来源，不是运行时权威。

推荐管线：

```text
AnimationClip
  -> BakeCombatClip
  -> CombatActionTimeline asset
  -> Runtime CombatActionTimeline
```

烘焙内容：

- Root motion 每逻辑帧位移和朝向偏移。
- Weapon root/tip/socket 每逻辑帧位置。
- Hurtbox/Hitbox 的本地中心、尺寸、旋转。
- 音效、特效、镜头、震屏事件帧。
- 动作标签、取消窗口、霸体/无敌/振刀窗口。

运行时表现：

```text
ActionStarted -> Animator.CrossFade
ActionFrameEvent -> VFX/SFX/Camera
HitConfirmed -> HitStop/VFX/SFX
ActionCanceled -> Animator.CrossFade next
MotionUpdated -> Transform interpolation
```

严禁：

- 战斗命中时读取 Animator 当前骨骼作为权威 hitbox。
- 用 Animator transition 结果决定是否可取消、可闪避、可受击。
- 用 Mecanim normalizedTime 直接驱动服务器或回放结果。

## 6. Combat Physics 职责

Combat Physics 是 gameplay physics，不是 simulation physics。

第一期 Shape：

| Shape | 用途 |
| --- | --- |
| Ray | 瞬发射线、瞄准、穿刺线 |
| Segment | 武器上一帧到当前帧线段 |
| Sphere | 点范围、球形命中 |
| Capsule | 角色 hurtbox、刀剑扫掠 |
| AABB | 简单阻挡、粗筛 |
| OBB | 矩形攻击盒、武器盒 |
| Sector | 扇形攻击 |
| Cylinder | 地面范围、圆柱 hitbox |

核心接口草案：

```csharp
public sealed class CombatPhysicsWorld
{
    public int Frame { get; private set; }

    public void Step(int frame);
    public void AddBody(in CombatBody body);
    public void UpdateBody(in CombatBody body);
    public void RemoveBody(int bodyId);

    public int Raycast(in RayQuery query, Span<CombatHit> results);
    public int CapsuleCast(in CapsuleCastQuery query, Span<CombatHit> results);
    public int OverlapCapsule(in CapsuleShape shape, CombatLayerMask mask, Span<CombatHit> results);
    public int OverlapSector(in SectorShape shape, CombatLayerMask mask, Span<CombatHit> results);
    public int OverlapObb(in ObbShape shape, CombatLayerMask mask, Span<CombatHit> results);
}
```

基础数据：

```csharp
public struct CombatBody
{
    public int BodyId;
    public int EntityId;
    public FixVector3 Position;
    public FixRotation Rotation;
    public CombatLayer Layer;
    public bool IsStatic;
}

public struct CombatCollider
{
    public int ColliderId;
    public int BodyId;
    public int BoneId;
    public CombatColliderShape Shape;
    public FixVector3 LocalCenter;
    public FixVector3 Size;
    public Fix64 Radius;
    public Fix64 Height;
}

public struct CombatHit
{
    public int EntityId;
    public int BodyId;
    public int ColliderId;
    public int BoneId;
    public Fix64 Distance;
    public FixVector3 Point;
    public FixVector3 Normal;
}
```

结果排序必须稳定：

```text
Distance ASC
HitPriority DESC
EntityId ASC
BodyId ASC
ColliderId ASC
BoneId ASC
```

## 7. 武器轨迹与大量射线

现有大量射线检测应迁移到 `WeaponTrace`，由确定性轨迹数据生成查询。

推荐模型：

```text
上一逻辑帧 weaponRoot/weaponTip
当前逻辑帧 weaponRoot/weaponTip
  -> blade capsule sweep
  -> tip sphere/capsule sweep
  -> optional ray fan
```

数据结构：

```csharp
public struct WeaponTraceFrame
{
    public int TraceId;
    public FixVector3 RootPrev;
    public FixVector3 TipPrev;
    public FixVector3 RootNow;
    public FixVector3 TipNow;
    public Fix64 Radius;
    public CombatLayerMask TargetMask;
}
```

好处：

- 避免逐帧多条 Unity Raycast 的非确定性。
- 表达武器体积，而不是只有一条无厚度射线。
- 易于实现同一动作同一目标只命中一次。
- 易于 Gizmo 显示和回放复现。

## 8. 移动与约束

角色移动由 `CombatMotor` 负责，不使用 Rigidbody 作为权威。

职责：

- 输入方向到确定性位移。
- Root motion 位移叠加。
- 击退、牵引、吸附、冲刺。
- 地面吸附、高度采样、坡度限制。
- 静态阻挡体碰撞。
- 动态角色间可选推挤。

执行顺序：

```text
InputMotion
  + ActionRootMotion
  + ExternalForces(knockback/pull)
  -> DesiredDelta
  -> ConstraintQuery
  -> ResolvedPosition
```

第一期不做：

- 真实动量累计。
- 刚体旋转惯性。
- 多刚体堆叠。
- 复杂摩擦求解。

## 9. 命中检测与结算

命中分两层：检测和结算。

检测产出候选：

```csharp
public struct HitCandidate
{
    public int AttackerId;
    public int TargetId;
    public int ActionId;
    public int TraceId;
    public CombatHit PhysicsHit;
    public int Frame;
}
```

结算规则：

```text
HitCandidate
  -> same action target filter
  -> target alive/state filter
  -> invincible check
  -> parry/block/counter check
  -> armor/super armor check
  -> hit priority check
  -> damage/stagger/knockback/buff event
  -> CombatEvent
```

结算结果：

```csharp
public struct HitResolveResult
{
    public int AttackerId;
    public int TargetId;
    public int ActionId;
    public HitResolveKind Kind;
    public int Damage;
    public int StaggerFrames;
    public FixVector3 Knockback;
}
```

注意：物理命中不等于伤害命中。无敌、霸体、振刀、格挡、阵营、同目标去重都在结算层处理。

## 10. 单机、联机、回放一致性

核心计算必须完全一致：

```text
Single Player:
  LocalInput -> CombatWorld.Step

Frame Sync Multiplayer:
  NetworkFrameInput -> CombatWorld.Step

Server Authoritative:
  ServerInput -> Server CombatWorld.Step
  ClientInput -> Client predicted CombatWorld.Step

Replay:
  RecordedInput -> CombatWorld.Step
```

区别只在输入来源和权威归属，不在战斗计算代码。

所有模式都必须支持：

- 输入记录。
- CombatEvent 记录。
- Physics query 记录。
- 每 N 帧 CombatHash。
- Desync dump。

## 11. 与 Ability/Buff/Modifier 的关系

推荐边界：

```text
AbilitySystem
  发起动作或技能请求
  读取配置
  不直接做物理查询

ActionSystem
  推进 CombatActionTimeline
  产生 WeaponTrace/ShapeQuery

HitSystem
  使用 CombatPhysicsWorld 查询
  产生 HitCandidate/HitResolveResult

Buff/Modifier
  响应 CombatEvent
  修改属性、状态、窗口、伤害参数
```

例如：

```text
Cast Strike
  -> StartAction(actionId=Strike)
  -> Active frame produces WeaponTrace
  -> HitResolveResult Damage
  -> Attribute HP changed
  -> Buff/Modifier may react
```

## 12. Unity 表现适配

Unity 层只消费状态和事件。

组件建议：

| 组件 | 职责 |
| --- | --- |
| CombatAnimatorDriver | 根据 ActionStarted/ActionCanceled 播放 Animator |
| CombatTransformDriver | 对逻辑位置做插值显示 |
| CombatVfxDriver | 根据 CombatEvent 播 VFX |
| CombatAudioDriver | 根据 CombatEvent 播音效 |
| CombatGizmoView | 绘制 hitbox、hurtbox、trace、hit point |
| CombatPhysicsCompareView | 对比 Unity Physics 和 Combat Physics 查询结果 |

表现允许：

- 插值。
- IK。
- 相机震动。
- 命中停顿视觉处理。
- 布娃娃和碎片表现。

表现禁止：

- 改写权威 EntityState。
- 反向决定命中结果。
- 用本地 Animator 时间覆盖逻辑帧。

## 13. Authoring 与调试工具

必须配套工具，否则动作和 hitbox 难以维护。

第一批工具：

- Action Timeline Inspector。
- 帧级 Scrubber。
- Hitbox/Hurtbox 编辑与预览。
- WeaponTrace 轨迹可视化。
- 命中候选列表。
- HitResolve 规则解释。
- Unity Physics 对照模式。
- Replay 播放器。
- CombatHash 面板。

调试输出：

```text
Frame
EntityState
ActionState
GeneratedQueries
HitCandidates
HitResolveResults
CombatEvents
WorldHash
```

## 14. 性能与内存规则

高频路径要求：

- `CombatWorld.Step` 默认 0 GC alloc。
- 查询结果使用 `Span<T>`、数组池或预分配缓冲。
- Broadphase 候选列表可复用。
- 不在 Step 中创建 LINQ、闭包、字符串。
- 调试字符串只在 DebugSnapshot 阶段生成。

Broadphase 第一版：

```text
Static colliders:
  Spatial Hash / Uniform Grid

Dynamic colliders:
  每帧重建 AABB 列表或轻量 grid

Query:
  broadphase candidates
  deterministic sort
  narrowphase
  deterministic hit sort
```

## 15. 分阶段落地

### M1: 确定性基础

- Fixed math：`Fix64`、`FixVector3`、`FixRotation`。
- Fixed frame clock。
- Entity deterministic transform。
- CombatHash 基础。

验收：

- 同一输入回放 1000 帧 hash 一致。
- 不依赖 UnityEngine 的核心数学程序集可测试。

### M2: Combat Physics Query

- `CombatPhysicsWorld`。
- Body/Collider 注册和更新。
- Ray、Sphere、Capsule、OBB、Sector 查询。
- 稳定排序。
- 当前落地顺序：M11D.1 已统一 query contract 并预留 OBB；M11D.2 已完成 broadphase v0；下一阶段 `Docs/Tasks/COMBAT_PHYSICS_M11D_3_OBB_QUERY_V0.md` 只补 OBB query vs AABB collider，不扩展 OBB collider 或角色运动。

验收：

- 查询测试覆盖边界、相交、穿透、排序。
- 查询结果不依赖注册顺序。

### M3: Combat Action Timeline

- `CombatActionTimeline` 数据结构。
- `ActionSystem` 固定帧推进。
- Active/Recovery/Cancel/Armor/Invincible window。
- Root motion track。

验收：

- 动作帧推进可回放。
- 同一动作在不同帧率表现下权威帧结果一致。

### M4: WeaponTrace 与 Hit Resolve

- Weapon trace 生成。
- Capsule sweep / segment query。
- HitCandidate。
- HitResolve pipeline。
- 同动作同目标去重。

验收：

- 武器挥砍可产生稳定命中。
- 格挡、无敌、霸体至少各有一个测试。

### M5: Unity Authoring Bridge

- AnimationClip 烘焙到 CombatActionTimeline。
- Gizmo 显示 hitbox/hurtbox/trace。
- Timeline Scrubber。
- Unity Physics 对照调试。

验收：

- 能在 Editor 中逐帧查看武器轨迹。
- 能导出运行时不依赖 Animator 的 timeline 数据。

### M6: Runtime Integration

- Ability 发起 CombatAction。
- HitResolveResult 转属性/Buff/Modifier 事件。
- Runtime HUD 展示当前 action、query、hit、event。

验收：

- 示例 Strike 动作用确定性 WeaponTrace 造成伤害。
- 单机和回放路径使用同一 CombatWorld。

### M7: Multiplayer Readiness

- FrameInput buffer。
- Replay file。
- CombatHash 每 N 帧校验。
- Desync dump。

验收：

- 可用录制输入完整重放战斗。
- 修改任一输入帧能导致 hash 差异并定位 dump。

## 16. 开发红线

- 不允许在 Combat Runtime 中调用 `Physics.Raycast` 作为权威判定。
- 不允许在 Combat Runtime 中读取 Animator 当前骨骼作为权威 hitbox。
- 不允许无排序地遍历 Dictionary/HashSet 并影响结果。
- 不允许使用 Unity `Random` 影响权威逻辑。
- 不允许让单机和联机走不同战斗结算代码。
- 不允许在高频 Step 中生成调试字符串。

## 17. 第一版推荐目录

```text
Assets/Scripts/MxFramework/Deterministic/
  Fix64.cs
  FixVector3.cs
  FixRotation.cs
  DeterministicHash.cs

Assets/Scripts/MxFramework/Combat.Physics/
  CombatPhysicsWorld.cs
  CombatBody.cs
  CombatCollider.cs
  CombatQueries.cs
  CombatHit.cs
  CombatBroadphase.cs
  CombatNarrowphase.cs

Assets/Scripts/MxFramework/Combat.Animation/
  CombatActionTimeline.cs
  CombatActionState.cs
  CombatTimelineTracks.cs
  WeaponTraceFrame.cs

Assets/Scripts/MxFramework/Combat.Runtime/
  CombatWorld.cs
  ActionSystem.cs
  MotionSystem.cs
  HitDetectionSystem.cs
  HitResolveSystem.cs
  CombatEventStream.cs

Assets/Scripts/MxFramework/Combat.Unity/
  CombatAnimatorDriver.cs
  CombatTransformDriver.cs
  CombatGizmoView.cs
  CombatTimelineBaker.cs
```

目录名可在实现前根据 asmdef 规划调整，但模块边界必须保持：确定性核心不依赖 Unity 表现层。

## 18. 确定性最佳实践

本节补充实现 Combat Animation Physics 时必须提前固化的工程细节。这些规则不是新功能，但任何一条处理不严都会造成隐藏的回放漂移、平台差异或联机不同步。

### 18.1 固定数值与固定时间步

Unity 内置浮点数学和物理不承诺跨平台确定性。战斗权威逻辑应使用框架自有的 fixed-point 数值类型，例如 `Fix64`、`FixVector3`、`FixRotation`，并把数值精度、舍入、溢出、Clamp 规则写入测试。

要求：

- 权威逻辑中禁止使用 `float` / `double` 做位置、速度、距离、角度、时间累计、伤害系数等核心计算。
- fixed-point 的加减乘除、归一化、长度、点乘、叉乘、角度比较必须有单元测试。
- 除法、开方、三角函数如需支持，必须定义确定性近似策略和误差预算。
- 所有单位要明确：距离建议使用毫米或厘米级 fixed unit，角度建议使用固定角度单位或查表。

时间推进规则：

- `CombatWorld.Step` 每次只推进一个逻辑帧。
- 动作帧、命中窗口、取消窗口、无敌/霸体/振刀窗口只用 frame 表达。
- 不在权威逻辑中使用 `Time.deltaTime`、`Time.time`、`DateTime.Now`。
- 不用变量 `dt` 做权威积分；需要速度时使用 fixed frame 下的每帧位移或每秒 fixed 速度转每帧 delta。

### 18.2 随机数与可重复性

任何影响战斗结果的随机都必须由 `DeterministicRandom` 提供，并且随机状态必须进入 `CombatWorld` 快照和 hash。

要求：

- 禁止权威逻辑调用 `UnityEngine.Random`、`System.Random.Shared` 或系统时间种子。
- 随机算法固定版本，例如 xorshift、PCG 或 LCG；算法变更必须升级 replay 版本。
- 随机种子、调用次数、当前状态必须可序列化。
- 随机调用点要集中，避免 Buff、AI、命中结算在不可控顺序里各自取随机。

建议接口：

```csharp
public interface IDeterministicRandom
{
    uint State { get; }
    int Range(int minInclusive, int maxExclusive);
    Fix64 Range01();
}
```

### 18.3 稳定排序与数据结构

集合遍历顺序会直接影响最终判定结果。例如多个命中候选、多个碰撞体、多个 Buff 响应同一事件时，如果排序不稳定，客户端之间可能在同一帧得出不同结果。

必须稳定排序的场景：

- Broadphase 候选列表。
- Narrowphase 命中结果。
- 同帧 CombatEvent。
- 同优先级 HitResolveResult。
- 同一实体上的 Buff/Modifier 事件响应。
- 同一帧输入命令。

规则：

```text
PrimaryKey ASC/DESC
EntityId ASC
BodyId ASC
ColliderId ASC
TraceId ASC
ActionId ASC
SourceOrder ASC
```

禁止：

- 依赖 `Dictionary`、`HashSet`、`unordered_map` 的迭代顺序。
- 使用未定义 tie-breaker 的排序。
- 在逻辑中直接调用可能返回非稳定顺序的 Unity API，例如 `FindObjectsByType`，再让其顺序影响结果。

推荐：

- 高频集合用预分配数组或 `List<T>`，写入后显式排序。
- 近乎有序的小列表可用插入排序，稳定且低开销。
- 排序比较器必须单独测试，覆盖完全相等、距离相等、优先级相等等场景。

### 18.4 Broadphase 与空间索引

Combat Physics 可以先用简单遍历实现，但框架设计必须预留 Broadphase。战斗场景中几十到数百个 Hurtbox/Hitbox 每帧做精确测试会浪费 CPU，也会让后续联机和回放测试成本上升。

第一版 Broadphase：

```text
Static colliders:
  Uniform Grid / Spatial Hash

Dynamic colliders:
  每帧更新 AABB 列表
  或按格子增量更新

Query:
  query AABB
  -> collect candidate ids
  -> deterministic candidate sort
  -> narrowphase
  -> deterministic hit sort
```

确定性要求：

- 格子坐标由 fixed-point 位置换算，不能使用浮点 floor。
- Hash 只用于定位桶，不允许桶遍历顺序直接决定结果。
- 候选体进入 narrowphase 前必须按稳定 key 排序。
- 静态场景烘焙数据要有版本号和 hash。

性能建议：

- 动态候选列表利用时间连续性，相邻帧变化小，可用插入排序维护。
- 每种 query 提前计算 query AABB，先粗筛再精测。
- 高频 buffer 由 world 统一持有，避免每个系统自行分配临时数组。

### 18.5 武器轨迹采样与缓存

武器挥砍不能用“当前帧一条 Ray”表达。应在动作烘焙阶段缓存武器关键点轨迹，并在运行时按固定帧生成 `WeaponTraceFrame`。

烘焙数据：

- `weaponRoot`、`weaponTip`、可选 `bladeMid`。
- 每逻辑帧 local position / local rotation。
- 每段轨迹的半径、层级、命中优先级。
- 高速动作的子采样点。

运行时查询：

```text
frame N-1 root/tip
frame N root/tip
  -> blade capsule sweep
  -> tip capsule/sphere sweep
  -> optional ray fan / sector query
```

高速挥砍规则：

- 可按武器端点速度自适应增加子采样。
- 子采样数量必须由 fixed 速度阈值决定，且结果确定。
- 子采样结果仍按统一 HitSortKey 排序。

去重规则：

```text
HitOnceKey = ActionInstanceId + TraceId + TargetEntityId
```

同一动作实例对同一目标通常只结算一次；多段攻击或多 hit 技能必须通过不同 `TraceId` 或 hit group 显式声明。

### 18.6 多线程与执行顺序

默认策略：`CombatWorld` 单线程执行。可以并行化局部查询，但不能让并行调度影响结果顺序。

固定执行顺序：

```text
FrameInputBuffer
  -> ActionSystem
  -> MotionSystem
  -> CombatPhysicsWorld.SyncBodies
  -> HitDetectionSystem
  -> HitResolveSystem
  -> Ability/Buff/Modifier reactions
  -> CombatEventStream flush
  -> CombatHash
```

禁止：

- 在多个 MonoBehaviour 的 `Update` / `FixedUpdate` / `LateUpdate` 中分别推进战斗子系统。
- 在后台线程更新权威 EntityState。
- 并行写入共享命中列表或事件流后不做确定性合并。

如需并行：

- 每个 worker 写入自己的本地 buffer。
- 主线程按 worker id / query id / entity id 稳定合并。
- 合并后再统一排序和结算。

### 18.7 调试、回放与反同步

确定性框架必须内建验证工具，而不是等联机后再排查。

必备调试能力：

- Frame hash：每帧末尾计算 `CombatHash`。
- State dump：hash 不一致时导出前后若干帧状态。
- Replay：记录输入并完整重放。
- Query trace：记录本帧生成了哪些 Ray/Capsule/OBB/Sector 查询。
- Hit explain：解释某个候选为什么命中、被格挡、被无敌过滤或被优先级压制。

Hash 覆盖范围：

```text
Frame
RandomState
EntityState
ActionState
MotorState
Buff/Modifier state
GeneratedQueries
HitCandidates
HitResolveResults
CombatEvents
```

回放测试要求：

- 同一输入在不同渲染帧率下 hash 一致。
- 同一输入连续重放多次 hash 一致。
- 修改任一输入帧能导致 hash 差异并定位到 desync dump。
- 每次修改 Combat Physics、Action Timeline、HitResolve 都要跑 replay regression。

### 18.8 版本与兼容

Combat 数据需要版本化，否则旧 replay、旧配置、旧烘焙资产会在算法变更后产生不可解释的差异。

需要版本号的对象：

- Fixed math algorithm version。
- CombatActionTimeline bake version。
- CombatPhysicsWorld query version。
- HitResolve rule version。
- Replay file version。
- Static collider bake version。

规则：

- 任何影响权威结果的算法变更都必须升级对应版本。
- Replay 文件记录所有版本号。
- Editor 在加载旧版本烘焙资产时必须提示重烘焙。
- DebugSnapshot 输出版本号，方便定位跨版本差异。

## 19. 审查参考来源

本节基于 2026-05-08 对外部审查稿的吸收整理，重点补充 fixed timestep、确定性 lockstep、Unity 时间/物理边界、稳定排序、可重播随机、Broadphase、武器轨迹缓存、多线程合并和 replay/hash 验证。审查稿来自用户提供的 ChatGPT 分享链接：`https://chatgpt.com/s/69fcb70ec8e08191afc925c1394555de`。
