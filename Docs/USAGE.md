# MxFramework 使用手册

> 版本 0.3.2 | 2026-05-14
>
> 本文面向业务开发和 AI 辅助开发。目标是“先看这里就能接入”，不要靠通读源码理解基础模块。

## 1. 使用原则

- 框架只提供通用机制；具体属性 ID、Buff ID、AI 事实 key、技能和怪物逻辑都由游戏层定义。
- 游戏层负责组合根，把 Config、Buff、Modifier、AI 的默认实现装配起来。
- Runtime 不依赖 UnityEditor，不依赖 WGame、Entitas、Luban 或 AI 插件。
- 配置表先通过 `ConfigSchema` 描述，再由项目自己的 Excel/CSV/Json/Luban/ScriptableObject 适配器转成 `ConfigTable<T>`。

## 2. Events 事件总线

`EventBus<T>` 是同步、类型安全的事件总线。事件 payload 推荐使用 `readonly struct`。

```csharp
public readonly struct DamageEvent
{
    public DamageEvent(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

var events = new EventBus<DamageEvent>();
IDisposable subscription = events.Subscribe(e =>
{
    // 处理伤害事件。
    int damage = e.Value;
});

events.Publish(new DamageEvent(100));
subscription.Dispose();
```

约定：

- `Subscribe` 会分配订阅句柄；高频路径不要反复订阅/取消。
- `Publish` 是同步调用，handler 抛异常会直接向外传播。
- 发布过程中新增的 handler 从下一次发布开始生效。

## 3. Attributes 属性存储

`AttributeStore` 同时实现 `IAttributeOwner` 和 `IAttributeModifierOwner`。游戏层用 int 定义属性 ID。

```csharp
const int AttrHp = 1001;
const int AttrAtk = 1002;

var attributes = new AttributeStore();
attributes.RegisterAttribute(AttrHp, 1000);
attributes.RegisterAttribute(AttrAtk, 50);

IDisposable changed = attributes.OnAttributeChanged.Subscribe(e =>
{
    int attrId = e.AttributeId;
    int oldValue = e.OldValue;
    int newValue = e.NewValue;
});

attributes.AddAttribute(AttrHp, -100);
int hp = attributes.GetAttribute(AttrHp);

changed.Dispose();
```

添加属性修改器：

```csharp
public sealed class FlatAttributeModifier : IAttributeModifier
{
    public FlatAttributeModifier(int id, int attributeId, int value)
    {
        Id = id;
        AttributeId = attributeId;
        Value = value;
    }

    public int Id { get; }
    public int AttributeId { get; }
    public int Value { get; }
    public AttributeModifierPhase Phase => AttributeModifierPhase.Add;
    public int Priority => 0;

    public int Modify(int currentValue, IAttributeOwner owner)
    {
        return currentValue + Value;
    }
}

attributes.AddModifier(new FlatAttributeModifier(1, AttrAtk, 20));
int finalAtk = attributes.GetAttribute(AttrAtk);
attributes.RemoveModifier(1);
```

约定：

- `GetAttribute` 对不存在属性返回 `0`；需要区分缺失时用 `TryGetAttribute`。
- Modifier 按 `Phase`、`Priority`、`Id` 排序。
- Modifier 应保持通用，不直接读取游戏实体。

## 4. BuffPipeline 基础用法

Buff 的生命周期由 `BuffPipeline` 管理。游戏层需要提供 `IBuffTarget`。

```csharp
public sealed class BurnBuff : BuffBase
{
    public BurnBuff() : base(id: 100001, duration: 5f, maxLayers: 3)
    {
    }

    public override void OnAttach(IBuffTarget target)
    {
        // 挂载属性修改器、注册事件等。
    }

    public override void OnDetach(IBuffTarget target)
    {
        // 清理属性修改器、解除订阅等。
    }
}

var pipeline = new BuffPipeline();
pipeline.AddBuff(new BurnBuff(), target);
pipeline.TickAll(1f);

bool exists = pipeline.HasBuff(100001);
int layer = pipeline.GetBuffLayer(100001);
BuffSnapshot[] snapshot = pipeline.CreateSnapshot();

pipeline.RemoveBuff(100001);
```

约定：

- 同 ID Buff 再次添加时，由 `IBuffStackingPolicy` 处理层数和刷新。
- `TickAll` 由外部传入时间，不自行读取 Unity `Time.deltaTime`。
- `OnDetach` 必须清理 Buff 创建的属性修改器和事件订阅。
- 配置驱动创建请看后面的 `ConfigBuffFactory<TConfig>`。

## 5. ModifierPipeline 和 Counter

Modifier 用于把条件和效果组合成可应用的运行时逻辑。

```csharp
public sealed class AlwaysCondition : IModifierCondition
{
    public bool Evaluate(ModifierContext context)
    {
        return true;
    }
}

public sealed class AddCounterEffect : IModifierEffect
{
    private readonly int _counterId;
    private readonly int _value;

    public AddCounterEffect(int counterId, int value)
    {
        _counterId = counterId;
        _value = value;
    }

    public void Execute(ModifierContext context)
    {
        context.Counters.AddCounter(_counterId, _value, this);
    }
}

var modifier = new ModifierBase(
    id: 200001,
    conditions: new IModifierCondition[] { new AlwaysCondition() },
    effects: new IModifierEffect[] { new AddCounterEffect(1, 1) });

var pipeline = new ModifierPipeline(attributeOwner);
pipeline.AddModifier(modifier);
pipeline.ApplyAll(new ModifierContext(attributeOwner, pipeline.Buffs, pipeline.Counters));
```

Counter 单独使用：

```csharp
var counters = new CounterStore();
counters.SetCounter(1, 0);
counters.AddCounter(1, 3);
int value = counters.GetCounter(1);
```

约定：

- `ModifierContext` 是显式上下文，不要在 Modifier 里读取全局单例。
- 缺失 Counter 默认返回 `0`；需要区分缺失时用 `TryGetCounter`。
- 配置驱动创建请看后面的 `ConfigModifierFactory<TConfig>`。

## 6. Entity / Ability / Target / Effect 最小闭环

Ability Slice 是当前游戏行为层的最小样板：实体释放技能、选择目标、执行效果，并回到 Attributes、Buffs 和 Events。

代码位置：

- 核心 API：`Assets/Scripts/MxFramework/Gameplay/`
- Demo 装配：`Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceRunner.cs`
- 测试：`Assets/Scripts/MxFramework/Tests/Ability/AbilitySliceTests.cs`

Demo 运行：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 `RuntimeVerticalSliceRunner` 上勾选 `_useAbilitySlice`。
3. Play 后 Runner 会自动挂载 `RuntimeAbilitySliceRunner`。
4. OnGUI 会显示 Player / Enemy 属性、Buff 列表、Ability 事件和 AttributeChanged 事件。

最小调用链：

```csharp
using MxFramework.Gameplay;

var player = new RuntimeEntity(entityId: 1, teamId: 1, hpAttributeId: AttrHp);
var enemy = new RuntimeEntity(entityId: 2, teamId: 2, hpAttributeId: AttrHp);

player.AttributeStore.RegisterAttribute(AttrHp, 1000);
player.AttributeStore.RegisterAttribute(AttrAttack, 120);
player.AttributeStore.RegisterAttribute(AttrDefense, 20);

enemy.AttributeStore.RegisterAttribute(AttrHp, 600);
enemy.AttributeStore.RegisterAttribute(AttrAttack, 80);
enemy.AttributeStore.RegisterAttribute(AttrDefense, 10);

var ability = new SimpleAbility(
    abilityId: 1,
    targetSelector: new SingleEnemyTargetSelector(),
    effects: new IAbilityEffect[]
    {
        new DamageEffect(AttrAttack, AttrDefense, AttrHp)
    });

var context = new AbilityContext(player, new IRuntimeEntity[] { player, enemy });
AbilityCastResult result = ability.Cast(context);
```

当前边界：

- 核心类型位于 `MxFramework.Gameplay`，Demo 只负责装配示例数据和 Unity OnGUI 展示。
- 这是运行时垂直切片，不是 WGame Ability JSON 迁移。
- v0 不包含冷却、资源、吟唱、打断、动画、输入、范围检测。
- 详细接口见 `Docs/Interfaces/Gameplay.md`。
- Phase 11 Runtime Gameplay Foundation 已在 `Docs/Tasks/PHASE11_RUNTIME_GAMEPLAY_CLOSEOUT.md` 中验收关闭；后续新增玩法能力应进入独立任务。

### 6.1 Component Runtime Showcase

Component Runtime Showcase 展示新的 component gameplay runtime v0：

```text
RuntimeHost
-> RuntimeCommandBuffer
-> GameplayRuntimeModule
-> GameplayComponentWorld
-> spawn / explicit target / ability rules / cleanup
-> event queue / hash / SaveState
```

代码位置：

- Runtime slice：`Assets/Scripts/MxFramework/Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcase.cs`
- Unity runner：`Assets/Scripts/MxFramework/Demo/GameplayComponentRuntime/GameplayComponentRuntimeShowcaseRunner.cs`
- UI：`Assets/UI/MxFramework/GameplayComponentRuntime/GameplayComponentRuntimeShowcase.uxml`
- 场景生成菜单：`MxFramework/Gameplay Component Runtime/Create Showcase Scene`
- 任务记录：`Docs/Tasks/GAMEPLAY_COMPONENT_RUNTIME_SHOWCASE_01.md`

当前交付等级是 `Runtime Slice`。本次提交不手写 `.unity` 场景；需要可打开场景时，在 Unity 中运行菜单：

```text
MxFramework/Gameplay Component Runtime/Create Showcase Scene
```

生成后打开：

```text
Assets/Scenes/GameplayComponentRuntimeShowcase.unity
```

操作：

1. `Spawn` 创建 hero / enemy。
2. `Cast Strike` 通过 request store 选择 explicit enemy target，扣 mana、伤害 enemy、启动 cooldown。
3. cooldown 中再次 `Cast Strike` 会输出 rejected event，不改变 hp / mana。
4. cooldown 到期后再次 `Cast Strike`，enemy hp 到 0。
5. `Cleanup` 标记 `PendingDestroy`，由 `GameplayLifecycleCleanupSystem` 清理 entity 和 registered stores。
6. `Save` / `Restore` 展示 ComponentWorld SaveState roundtrip。
7. `Run Full Flow` 自动执行完整展示链。

### 6.2 Config Driven Ability

`MxFramework.Config.Runtime` 可以把 `BasicAbilityConfig` 转成 `MxFramework.Gameplay.SimpleAbility`：

```csharp
using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;

var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
table.Add(new BasicAbilityConfig(
    id: 300001,
    nameText: new LocalizedTextKey("ability.strike.name"),
    descriptionText: new LocalizedTextKey("ability.strike.desc"),
    targetSelectorKind: AbilityTargetSelectorKind.SingleEnemy,
    effects: new[]
    {
        AbilityEffectConfig.DamageByAttackDefense(
            attackAttributeId: AttrAttack,
            defenseAttributeId: AttrDefense,
            hpAttributeId: AttrHp)
    }));

var factory = new ConfigAbilityFactory(table);
if (factory.TryCreate(300001, out IAbility ability, out string error))
{
    ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}
```

当前支持：

- `AbilityTargetSelectorKind.Self`：选择 caster 自身。
- `AbilityTargetSelectorKind.SingleEnemy`：选择第一个不同队伍且存活的候选实体。
- `AbilityEffectConfig.DamageByAttackDefense(...)`：命名传入 `attackAttributeId`、`defenseAttributeId`、`hpAttributeId`。
- `AbilityEffectConfig.ApplyBuff(buffId)`：需要给 `ConfigAbilityFactory` 传入 `IBuffFactory`。
- 旧的 `AbilityEffectConfig(kind, int[] parameters)` 仍保留兼容；新代码和编辑器接入应优先使用命名参数或静态工厂，避免依赖数组位序。

### 6.2.1 Ability Authoring Contract

AI、外部 JSON 和后续编辑器不要直接生成 `BasicAbilityConfig`。先生成工具层 `AbilityAuthoringContract`，经过结构化校验后再映射到运行时配置：

```csharp
using MxFramework.Config;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;

var contract = new AbilityAuthoringContract
{
    AbilityId = 300001,
    DisplayName = "Strike",
    Description = "Deal attack minus defense damage.",
    TargetSelectorKind = AbilityAuthoringTargetSelectorKind.SingleEnemy,
    Effects = new[]
    {
        AbilityAuthoringEffectContract.DamageByAttackDefense(
            attackAttributeId: AttrAttack,
            defenseAttributeId: AttrDefense,
            hpAttributeId: AttrHp)
    }
};

if (!AbilityAuthoringContractMapper.TryMap(
        contract,
        out BasicAbilityConfig config,
        out AbilityAuthoringValidationReport report))
{
    foreach (AbilityAuthoringValidationIssue issue in report.Issues)
    {
        // issue.Code is stable; issue.Message may be localized.
    }
}

var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
table.Add(config);

var factory = new ConfigAbilityFactory(table);
if (factory.TryCreate(300001, out IAbility ability, out string error))
{
    ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}
```

工具上下文可通过 `AbilityAuthoringSchema.CreateSummary()` 获取字段中文名、类型、说明、允许值和稳定错误码。当前错误码包括 `MissingAbilityId`、`InvalidAbilityId`、`MissingDisplayName`、`UnknownTargetSelector`、`MissingEffect`、`UnknownEffectKind`、`MissingEffectParameter`、`InvalidAttributeId`、`InvalidBuffId`、`UnsupportedContractVersion`。

Demo 运行：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 `RuntimeVerticalSliceRunner` 上勾选 `_useAbilitySlice`。
3. 勾选 `_useConfigDrivenAbility` 时，Ability Runner 使用 `BasicAbilityConfig -> RuntimeAbilityConfigResolver -> ConfigAbilityFactory`；不勾选时保留硬编码 `SimpleAbility` 示例。

边界：这不是 WGame Ability JSON 迁移，也不包含冷却、资源、吟唱、打断、动画、输入、范围、碰撞、寻路或弹道。

### 6.3 Runtime Config Change Handling

Runtime 配置变更采用重建语义，不做 Ability 热替换，也不回溯修改已挂载 Buff / Modifier：

```csharp
var resolver = new RuntimeAbilityConfigResolver(
    configs: registry,
    buffFactory: buffFactory,
    sourceName: "RuntimeAbilitySliceDemoData",
    changeSet: changeSet);

if (resolver.TryCreate(300001, out IAbility rebuiltAbility, out string error))
{
    rebuiltAbility.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));
}

string summary = resolver.CreateSummary();
```

约定：

- `TryCreate` 每次从当前 `IConfigProvider` 创建新的 Ability；调用方负责替换自己持有的 Ability 引用。
- 旧 Ability 不会在原对象上替换 selector / effects / parameters。
- 已挂载 Buff / Modifier 保持挂载时捕获的运行时状态；配置变更只影响后续新创建实例。
- `RuntimeConfigChangeSummary` 可展示 source、policy、changed ids、rebuilt ids、failed ids 和错误摘要。

### 6.3 Gameplay Diagnostic Snapshot

`GameplayDiagnosticSnapshotBuilder` 把运行中的 Entity / Ability / Buff / Modifier / Event 状态整理成纯 C# 只读快照，适合 EditMode 测试、Demo 摘要、AI 上下文和后续编辑器读取。

最小用法：

```csharp
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Gameplay;

var abilityEvents = new List<AbilityEvent>();
var attributeEvents = new List<AttributeChangedEvent>();

player.AbilityEvents.Subscribe(e => abilityEvents.Add(e));
player.Store.OnAttributeChanged.Subscribe(e => attributeEvents.Add(e));
enemy.Store.OnAttributeChanged.Subscribe(e => attributeEvents.Add(e));

AbilityCastResult result = ability.Cast(new AbilityContext(player, new IRuntimeEntity[] { player, enemy }));

var builder = new GameplayDiagnosticSnapshotBuilder();
GameplayDiagnosticSnapshot snapshot = builder.Build(
    sourceName: "runtime-ability-slice",
    abilitySource: "BasicAbilityConfig -> ConfigAbilityFactory",
    entities: new[] { player, enemy },
    attributeIds: new[] { AttrHp, AttrAttack, AttrDefense },
    lastCastResult: result,
    abilityEvents: abilityEvents,
    attributeEvents: attributeEvents);

int entityCount = snapshot.Entities.Count;
int targetId = snapshot.LastTargetEntityIds.Count > 0
    ? snapshot.LastTargetEntityIds[0]
    : 0;
```

约定：

- Builder 不订阅事件；调用方负责维护 `abilityEvents` / `attributeEvents` 日志。
- `attributeIds` 是白名单，决定每个 Entity 输出哪些属性。
- 空实体、空属性、空事件日志应返回空集合，不要求 Unity 场景或 MonoBehaviour。

### 6.4 UI Toolkit Runtime Showcase

Phase 12 开始，Runtime Gameplay 的手测入口会逐步从 OnGUI 迁移到 UI Toolkit Showcase。

当前 M1 Shell 会在 Ability Slice 启动时自动挂载：

```text
RuntimeAbilitySliceRunner
  -> RuntimeAbilitySliceShowcaseUi
  -> MxRuntimeHudController
  -> GameplayShowcase.uxml / GameplayShowcase.uss
```

实际场景装配规则：

```text
RuntimeVerticalSliceSceneConfig asset
  -> RuntimeVerticalSliceBootstrap
  -> dynamically creates RuntimeVerticalSliceRunner
  -> dynamically mounts RuntimeAbilitySliceRunner
  -> dynamically mounts MxRuntimeHudController
  -> dynamically mounts RuntimeAbilitySliceShowcaseUi
```

`RuntimeVerticalSlice.unity` 只作为舞台，不需要预挂 Showcase / Preview 运行时组件。需要展示什么能力，应打开 `MxFramework / Runtime Showcase / Scene Config`，在统一配置窗口里修改配置资产。

手测方式：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 打开 `MxFramework / Runtime Showcase / Scene Config`。
3. 确认「自动启动」「启用 Ability Showcase」「启用 UI Toolkit HUD」已开启。
4. Play 后会看到 UI Toolkit HUD，展示 Player / Enemy、Mini Game Feedback、Buff 摘要、Ability source、Config summary、Snapshot summary、Diagnostic View 和 Event Log。
5. Config / Patch / Rebuild 面板可手动执行 `Load Patch`、`Load Mod Package`、`Rebuild Ability`、`Compare Old/New`。对比结果会显示旧 Ability 对象保持旧配置效果，新重建 Ability 使用当前配置效果。
6. Diagnostic View 可在 `Summary` / `Technical` 间切换，分区显示 Entity、Ability Events、AttributeChanged Events、Config Source、last cast failure 和 config errors；无错误时显示 `No runtime errors`。
7. Mini Game Feedback 会显示 Player / Enemy HP 状态徽章、Buff 层数和剩余时间、技能按钮可用反馈，以及最近动作摘要。

### 6.5 Combat Physics Playground

Combat Physics 的当前手测入口复用 `CombatAnimationPhysicsTest` 场景，用于验证自研 `CombatPhysicsWorld`、Combat Motion、统一 query、hit resolve 和场景反馈是否能形成可玩闭环。

手测方式：

1. 打开 `Assets/Scenes/CombatAnimationPhysicsTest.unity`。
2. Play 后 HUD 顶部模式显示 `Physics Game`，场景中应有 `Combat_Player_Marker` 和 `Combat_Enemy_Marker`。
3. 用 `WASD` 或方向键驱动 Player 的 Combat Motion；`Space` 起跳；角色会受重力下落、落地、撞墙或撞顶。
4. 左键拖动 Player / Enemy marker 可直接改位置；右键拖动镜头。
5. 按 `Q` 或点击 `Shape` 在 `Capsule / Ray / Sphere / Aabb / Sector` 间切换。
6. 按 `P` 或点击 `Probe` 执行一次只探测不扣血的查询。
7. 按 `J` 或点击 `Attack` 执行一次攻击；命中后扣血、加分、连击递增，击杀 Enemy 后进入下一回合并重生。
8. 按 `T` 或点击 `Step` 只推进确定性 combat frame；按 `R` 重置 Showcase。
9. HUD 的 `Mini Game Feedback` 会显示回合、分数、连击、当前 shape、Motion position / velocity / grounded / collision flags、world revision 和最近一次 `CombatPhysicsQueryDebugReport` 摘要。

Combat Physics Playground 约定：

- 物理命中权威来自 `CombatPhysicsWorld.Query` / `ExplainQuery`，不使用 `UnityEngine.Physics` 作为结果来源。
- Player 移动权威来自 `MxFramework.Combat.Motion.CombatKinematicMotor`，并在同一固定帧写回 `CombatPhysicsWorld` body position；移动后 Probe / Attack 必须读取写回后的新位置。
- 场景 marker 只负责给 Demo 提供输入和表现；Runtime Combat Physics / Motion 仍保持纯运行时模块边界。
- 该入口是制作人手测和验收入口，不承载 WGame 具体角色、关卡或业务配置。

### 6.6 Combat Animation Playable Demo

Combat Animation 的可玩验证入口用于串联输入、`RuntimeHost` 三模块管线、WeaponTrace、HitResolve、Combat -> Gameplay Bridge HP、Unity 表现驱动和 UI Toolkit HUD。

代码入口：

- Demo runtime：`Assets/Scripts/MxFramework/Demo/CombatAnimation/CombatAnimationDemoBootstrap.cs`
- 输入桥接：`Assets/Scripts/MxFramework/Demo/CombatAnimation/DemoInputToActionAdapter.cs`
- 位姿适配：`Assets/Scripts/MxFramework/Demo/CombatAnimation/CombatDemoPoseSource.cs`
- HUD：`Assets/Scripts/MxFramework/Demo/CombatAnimation/CombatAnimationHudController.cs`
- 场景生成：`Assets/Scripts/MxFramework/Demo/Editor/CreateCombatAnimationDemoScene.cs`
- 可玩场景：`Assets/Scenes/CombatAnimationDemo.unity`
- UI Toolkit：`Assets/UI/MxFramework/CombatAnimationHud.uxml` / `.uss`

手测方式：

1. 如需重新生成场景，执行 `MxFramework / Combat / Generate Animation Demo Scene`。
2. 打开 `Assets/Scenes/CombatAnimationDemo.unity`，直接 Play。
3. 用 `WASD` 移动 Player；位姿由 `CombatDemoPoseSource` 提供给 `CombatTransformDriver`。
4. 按 `J` 启动 LightAttack，按 `K` 启动 HeavyAttack，按 `Space` 启动 DodgeRoll。
5. HUD 显示 Player 当前动作、阶段、localFrame、Player/Dummy HP、WeaponTrace candidate 数、最近 Bridge / Attribute 事件，以及只读 Runtime Diagnostic 面板。
6. 当前 Demo 不生成 AnimatorController；`DemoCombatAnimatorDriver` 收到 action lifecycle 事件后输出 `[CombatAnimatorDriver] Entity ... ActionStarted ...` 日志。未来绑定 AnimatorController 后可直接按 mapping state name 走 CrossFade。

Combat Animation Playable 约定：

- 权威动作推进来自 `CombatActionRuntimeModule`，WeaponTrace 来自 `CombatWeaponTraceRuntimeModule`，诊断来自 `CombatAnimationDiagnosticsModule`。
- 当前没有通用 RuntimeCommand 到 CombatActionRunner 的正式桥接；Demo 使用 `DemoInputToActionAdapter` 从 `InputCommandQueue` 消费输入并调用 `CombatActionRunner`。
- `J` / `Space` 来自默认输入配置；`K` 是本 Demo 在 `CombatAnimationDemoBootstrap.EnqueueDemoKeyboardCommands` 中补充写入 `InputCommandQueue` 的临时绑定。
- HP 权威状态来自 `GameplayAttributeSetComponent`；命中后由 `HitResolveSystem` 写入 `_hitResults`，再经 `CombatHitApplicationSystem` 输出 `AddComponentAttribute`，最后由 `GameplayAttributeCommandSystem` 修改 HP 并驱动 HUD。
- Runtime Diagnostic 面板只读展示 `CombatActionStateComponent`、hit application output commands、`GameplayAttributeSetComponent`、`CombatEntityGameplayMap`、Demo diagnostic hash 和 `RuntimeEventQueue<GameplayRuntimeEvent>.CreateSnapshot()` 摘要；它不 drain event queue，也不修改 Combat / Gameplay 状态。
- Demo 的动作伤害仍是 `CombatAnimationDemoBootstrap` 内的手测动作配置；按钮或键盘输入只触发 Combat 动作，不直接修改 HP。

### 6.7 Marble Maze Runtime Showcase

Marble Maze 用于验证框架物理与 MxFramework Runtime 的边界：球体移动、墙体阻挡、GEM 拾取和 EXIT 判定必须由框架 runtime / physics 模块负责，`MarbleMazeRuntimeModule` 负责命令、计时、checkpoint、diagnostics、Replay hash 和 SaveState。Unity 场景对象只作为显示和输入适配，不使用 `Rigidbody` / `Collider` / trigger 作为玩法权威。

代码入口：

- Runtime：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeRuntime.cs`
- Unity view / input 适配：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazePhysicsDemo.cs`
- Framework physics adapter：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeFrameworkPhysicsWorld.cs`
- AppFlow 适配：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeAppFlowDemo.cs`
- 场景生成：`Assets/Scripts/MxFramework/Demo/Editor/CreateMarbleMazeScenes.cs`
- 可玩场景：`Assets/Scenes/MarbleMazeBoot.unity` / `Assets/Scenes/MarbleMazeGameplay.unity`
- UI Toolkit：`Assets/UI/MxFramework/MarbleMaze/MarbleMazePlayableDemo.uxml` / `.uss`
- 测试：`Assets/Scripts/MxFramework/Tests/Demo/MarbleMaze/MarbleMazeRuntimeTests.cs`

手测装配方式：

1. 打开 `Assets/Scenes/MarbleMazeBoot.unity` 或 `Assets/Scenes/MarbleMazeGameplay.unity`，直接 Play。
2. 如需重建场景，运行 Unity 菜单 `MxFramework / Marble Maze / Create Playable Scenes`；不要手写 `.unity` YAML。
3. 生成器会创建场景根对象、`DefaultInputService`、`MarbleMazePhysicsDemo`、`MarbleMazeAppFlowDemo`、`UIDocument`、棋盘、球、墙、GEM 和 EXIT view；不得依赖 Unity trigger 完成玩法。
4. WASD / 方向键控制倾斜；HUD 按钮提供 Pause / Reset / Save / Load。

约定：

- Unity 输入通过 `DefaultInputService` 采集为 `InputSnapshot`，再入队 `RuntimeCommandBuffer` 命令；UI Button 也只入队命令或触发 SaveState 操作，不直接改 Runtime 权威状态。
- 框架物理的 position / query 结果通过 `PhysicsSample` / `Checkpoint` / `Finish` 命令同步给 Runtime。
- Runtime SaveState 使用 `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer`，手测 HUD 的 Save / Load 按钮执行 JSON roundtrip。
- 该 Demo 必须使用框架物理；旧 Unity Physics 方向不再符合 `AGENT_GAME_CREATION_GUIDE.md`。

Runtime Showcase 通用约定：

- UXML / USS 是 UI 结构和视觉真源，C# 只负责绑定状态。
- `MxFramework.UI.Toolkit` 可以引用 UnityEngine / UI Toolkit。
- `MxFramework.Gameplay`、`Config.Runtime`、`Buffs`、`Modifiers` 等纯运行时模块不得反向依赖 UI。
- OnGUI 暂时保留为后备显示；正式手测入口优先使用 UI Toolkit HUD 的手动控制和 Mini Game Feedback。
- 配置项统一放在 `RuntimeVerticalSliceConfigWindow`；子 Runner 不应要求制作人手动挂载或单独配置。
- Preview Target 使用 `MxPreviewSceneTargetProfile` 资产配置，运行前场景中不常驻 `SceneTargetConfig` 或 `MxPreviewSceneTarget`。
- 默认 HUD 是单一紧凑面板；Preview Target legacy overlay 默认关闭，避免多个 HUD 在 Game 视图中堆叠。
- Snapshot v0 不包含 JSON 序列化、编辑器 UI、Runtime Preview 协议或 WGame 业务类型。

## 7. Config 表和校验

基础流程：

1. 定义配置类型，实现 `IConfigData`。
2. 定义 `ConfigSchema<T>`，写清字段、ID 范围、引用规则和多语言要求。
3. 把导入后的数据注册到 `ConfigTable<T>`。
4. 用 `Validate` 或 `ConfigAuthoring.ValidateTable` 做提交前校验。

示例：

```csharp
var buffs = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
buffs.Add(new BasicBuffConfig(
    100001,
    new LocalizedTextKey("buff.burn.name"),
    new LocalizedTextKey("buff.burn.desc"),
    duration: 5f,
    maxLayers: 3,
    modifierId: 200001));

var modifiers = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
modifiers.Add(new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.burn.name"),
    new LocalizedTextKey("mod.burn.desc")));

var registry = new ConfigRegistry();
registry.RegisterProvider<BasicBuffConfig>(buffs);
registry.RegisterProvider<BasicModifierConfig>(modifiers);

ConfigTableValidationReport report = buffs.Validate(registry, localizationProvider);
if (report.HasErrors)
{
    // 提交前阻断，展示 report.Issues。
}
```

### 7.1 Mod Package 运行时边界（单包 / LoadPlan / 多包 Merge）

- 单包加载：`RuntimeModPackageLoader.LoadFromDirectory(path)` 只负责读取一个包并返回 `RuntimeConfigPatchBundle`，不处理跨包顺序。
- 包发现与排序：`RuntimeModPackageDiscovery.Discover(containers)` + `RuntimeModPackageLoadPlanBuilder.Build(catalog)` 只产出 `OrderedItems/SkippedItems`，不修改配置表。
- 启用状态持久化：`RuntimeModPackageLoadoutJson.LoadFromFile(path)` 读取 `mod_loadout.json`，再用 `RuntimeModPackageLoadPlanBuilder.Build(catalog, loadout)` 解析启用组合。
- 多包合并：`RuntimeModPackagePatchMerger.Merge(loadPlan, baseRegistry)` 才会按顺序执行 patch，并生成最终 `ConfigRegistry` 与 merge report。

Loadout v0 约定：

- `format` 必须是 `mx.modLoadout.v1`。
- `enabledPackageKeys` 使用 `packageId|containerRelativePath`，例如 `demo.runtime.patch.mod|runtime-patch-mod`。
- `loadout == null` 时保持兼容：启用全部 valid 包。
- `enabledPackageKeys == []` 时启用 0 个包。
- loadout 中缺失 key 不中断流程，写入 `LoadPlan.Warnings`。

### 7.2 Mod Diagnostic CLI（命令行诊断）

`mod diagnose` 命令在 CLI 中执行完整的包发现→加载计划→合并诊断管道，输出 JSON 快照。

```bash
# 基本用法：诊断一个容器目录
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods

# 指定 loadout 文件
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --loadout /path/to/loadout.json

# 美化 JSON 输出
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --pretty

# 输出到文件
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --output snapshot.json

# 遇到 Warning 也返回非零退出码
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods --fail-on-warning
```

退出码：

| 退出码 | 含义 |
|--------|------|
| 0 | 成功（无 Error/Warning） |
| 2 | 校验被阻断（存在 Error） |
| 5 | 存在 Warning 但无 Error（仅在 `--fail-on-warning` 时） |
| 1 | 工具内部错误 |

多个容器可重复 `-c`：

```bash
dotnet run --project src/MxFramework.Authoring.Cli -- mod diagnose -c /path/to/Mods1 -c /path/to/Mods2
```

快照 JSON 格式与 `ModDiagnosticSnapshotDto` 一致，包括：

- `summary`：discovered/valid/invalid/enabled/ordered/skipped/overrides/errors/warnings
- `packages`：每包有效性、启用状态、key、相对路径、错误/警告
- `issues`：所有 Error 和 Warning，带 code/source/message
- `success`：整体是否成功（有 Error 则 false）
- `format`：固定为 `mx.modDiagnosticSnapshot.v1`

### 7.3 Mod Diagnostic Snapshot（运行时诊断统一入口）

- 目的：把 `catalog + loadout + loadPlan + mergeResult` 汇总成单一快照，不改变运行时行为。
- 构建 API：
  - `RuntimeModDiagnosticSnapshotBuilder.Build(catalog, loadout, loadPlan, mergeResult)`
  - `RuntimeModDiagnosticSnapshotJson.SaveToJson(snapshot)`
- 快照格式固定为 `mx.modDiagnosticSnapshot.v1`，包含：
  - `summary`（discovered/valid/invalid/enabled/ordered/skipped/overrides/errors/warnings）
  - `packages`（每包有效性、启用状态、key、相对路径、错误/警告）
  - `loadPlan`（Ordered/Skipped + skipReason + orderIndex）
  - `overrides`（配置覆盖链与赢家）
  - `errors` / `warnings`（带 code/source/message）
- 判定规则：
  - `Error` -> `success=false`
  - `Warning` -> `success` 可保持 true
- `RuntimeVerticalSliceRunner` 在 loadout+merge 模式会生成 snapshot 摘要，并可写入 `Application.persistentDataPath/MxFramework/Diagnostics/mod_diagnostic_snapshot.json`。

当前 v0 合并策略：

- deterministic last-write-wins：后加载包覆盖先加载包。
- `Remove` 删除不存在行记为 `Noop`，不会中断。
- skipped（invalid/disabled）包不参与合并，但会记录进报告。
- ordered 包在 merge 阶段加载失败会直接返回 `Success=false`，并包含 `packageId/path/error`。

### 7.4 EditorServer 诊断 API（Authoring Editor）

`editor serve` 提供 Mod 诊断 API：

- `GET /api/mod/diagnose`
- `POST /api/mod/diagnose`

参数：

- `containers`（数组或 CSV）
- `loadout`（可选）
- `includeAbsolutePaths`（可选，默认 false）

返回：`mx.modDiagnosticSnapshot.v1` JSON。
实现上，CLI `mod diagnose` 与 EditorServer API 复用同一 `ModDiagnosticService.BuildSnapshot(...)`，避免双份诊断逻辑漂移。

## 8. 配置编辑和 AI 辅助

`ConfigAuthoring` 是后续 Unity ConfigEditor 和 AI 生成配置表时的统一入口。

Unity 内部查看配置源：

1. 打开菜单 `MxFramework > Framework Manager`。
2. 选择 `编辑模式`。
3. 切换到 `配置工作台`。
4. 在左侧 `配置源` 导航中选择 `内置 Demo / BasicBuffConfig`、`内置 Demo / BasicModifierConfig`、`内置 Demo / ActionCatalog` 或 `内置 Demo / ActionGraph`。
5. 在主工作区页签查看 `概览 / 字段 / 行视图 / 引用`。
6. 在右侧检查器查看源预览、当前源校验和报告动作。
7. 在 `字段` 页点击 `详情`，右侧检查器会显示字段元数据、enum/flags 候选项和引用目标。
8. 对引用字段点击 `查看目标源`，可跳转到目标配置源。
9. 在底部问题抽屉查看全局问题列表。
10. 点击 `复制模板` 可把模板复制到剪贴板。
11. 页面会自动检测所有配置源健康状态，显示源数量、总行数、问题源、Error、Warning、缺失引用、多语言缺失、ID 问题、Schema 问题、索引源和索引 Key。
12. 在 `问题筛选` 中选择 `全部 / Error / Warning` 查看问题明细。
13. 点击 `AI 上下文` 可复制当前源信息、当前选中字段、健康报告、Schema、TSV 预览和当前源问题列表。
14. 页面会基于上一次健康检测保存轻量基线，显示配置源、行数、Error、Warning 和错误类型统计是否变化。
15. 点击 `变动报告` 可复制配置变动统计。
16. 点击 `重置基线` 可把当前健康状态设为新的比较基线。
17. 点击 `导出报告` 可把当前配置报告包写入 `Temp/MxFrameworkReports/Config/`。
18. 点击 `提交前检查` 会刷新配置源、重新检测、导出报告包并生成 `config_precommit.txt`。
19. 点击 `校验` 可验证当前配置源的引用和多语言规则。
20. 点击顶部 `复制报告` 可复制当前配置源报告。
21. 如果项目层运行时注册了新配置源，点击 `刷新` 重新读取列表并自动检测。

当前 Config Workbench v0 只查看配置源、复制模板和校验当前源，不编辑真实配置资产，也不绑定 Excel/CSV/Json/Luban/ScriptableObject。框架内置 Demo 见 `Docs/Demo/CONFIG_DEMO.md`，它只验证框架能力，不是任何真实项目的数据来源。

Buff 创作流程不再通过 Unity `Framework Manager` 的内置页查看。当前统一使用外部 Authoring Editor；使用方式见 `Docs/AUTHORING_EDITOR_USAGE.md`。

Buff 流程按 WGame 逻辑组织：先选 `BuffType`，再填写 `BuffData` 公共字段、堆叠持续字段、类型专属字段、表现资源和引用检查。完整操作规范见 `Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md`。

代码侧创建 Buff 工作流：

```csharp
AuthoringWorkflow workflow =
    BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(
        "buff.create.fire",
        "创建 Buff：燃烧",
        buffId: 100001,
        mode: AuthoringWorkflowMode.Player,
        layer: "Mod",
        templateKind: BuffAuthoringTemplateKind.DamageByAttr);

string aiContext = workflow.CreateStepAiContext("type-fields");
```

字段显示规则：

- `ConfigField.Name` 是真实字段 key，导入、导出、校验、引用和 AI 修复都使用它。
- `ConfigField.DisplayName` 是中文显示别名，配置工作台优先显示它，并同时保留原字段名。
- 问题报告会输出 `field=` 和可选 `fieldDisplay=`，便于人和 AI 同时理解。

项目层接入真实配置时，优先用 `ExternalConfigEditorSource` 包装已经解析好的 TSV/JSON/Luban 结果：

```csharp
var schema = new ConfigSchema(
        "GameActionCatalog",
        null,
        displayName: "项目动作索引",
        structureKind: ConfigStructureKind.Table)
    .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
    .AddField(new ConfigField(
        "GraphId",
        ConfigFieldType.ConfigReference,
        displayName: "动作图",
        referenceRule: new ConfigReferenceRule("GraphId", "GameActionGraph", ConfigStructureKind.Graph)));

MxEditorUtils.RegisterConfigEditorSource(new ExternalConfigEditorSource(
    name: "Game / ActionCatalog",
    sourceType: "TSV",
    schema: schema,
    rowCount: 120,
    keys: new object[] { 1001, 1002 },
    previewText: "Id\tGraphId\n1001\t9001\n",
    sourcePath: "ProjectConfig/Tables/ActionCatalog.tsv",
    contentHash: "sha256:..."));
```

带枚举/flags 候选项：

```csharp
var enums = new ConfigEnumRegistry();
enums.Register(new ConfigEnumDomain("game.ActionTags", isFlags: true)
    .AddValue(new ConfigEnumValue(1, "Area", "范围"))
    .AddValue(new ConfigEnumValue(2, "Projectile", "飞行物")));

MxEditorUtils.RegisterConfigEditorSource(new ExternalConfigEditorSource(
    name: "Game / ActionCatalog",
    sourceType: "TSV",
    schema: schema,
    rowCount: 120,
    keys: new object[] { 1001, 1002 },
    previewText: "Id\tTags\tGraphId\n1001\t3\t9001\n",
    enumRegistry: enums));
```

需要完全自定义展示时，实现 `IConfigEditorSource`：

```csharp
public sealed class GameConfigEditorSource : IConfigEditorSource
{
    private readonly IConfigTable<GameBuffConfig> _table;
    private readonly IConfigProvider _registry;
    private readonly ILocalizationProvider _localization;

    public string Name => "Game / BuffConfig";
    public string SourceType => "Luban";
    public ConfigSchema Schema => _table.Schema;
    public int RowCount => _table.Rows.Count;

    public ConfigAuthoringTemplate CreateTemplate()
    {
        return ConfigAuthoring.CreateTemplate(Schema);
    }

    public ConfigAuthoringReport Validate()
    {
        return ConfigAuthoring.ValidateTable(_table, _registry, _localization);
    }

    public string CreateTsvPreview(int maxRows)
    {
        // 项目层按自己的数据结构导出预览文本。
        return string.Empty;
    }

    public string CreateReport()
    {
        return ConfigAuthoring.ExportReportText(Validate());
    }
}
```

注册到 Framework Manager：

```csharp
MxEditorUtils.RegisterConfigEditorSource(new GameConfigEditorSource());
```

如果该源还需要参与 `ConfigSourceIndex` 和跨源引用校验，同时实现 `IConfigEditorSourceIndexProvider`。

如果该源还需要显示行视图和未来控件映射，同时实现 `IConfigEditorTablePreviewProvider`。`MemoryConfigEditorSource<T>` 和 `ExternalConfigEditorSource` 已经默认支持。当前控件全部只读，保存功能后续开启。

如果该源还需要显示枚举或 flags 候选项，同时实现 `IConfigEditorEnumProvider`，或在创建 `ExternalConfigEditorSource` 时传入 `ConfigEnumRegistry`。

提交前或 AI 辅助修表时，可以直接生成健康报告：

```csharp
IReadOnlyList<IConfigEditorSource> sources = MxEditorUtils.GetConfigEditorSources();
ConfigHealthReport health = MxEditorUtils.AnalyzeConfigHealth(sources);
string report = MxEditorUtils.CreateConfigHealthReport(health);
```

生成 AI 修复上下文：

```csharp
IReadOnlyList<ConfigIssueView> issues = MxEditorUtils.CollectConfigIssues(sources);
string context = MxEditorUtils.CreateConfigAiFixContext(source, health, issues, previewRows: 5);
```

带当前字段摘要：

```csharp
string context = MxEditorUtils.CreateConfigAiFixContext(
    source,
    health,
    issues,
    previewRows: 5,
    selectedFieldName: "GraphId");
```

生成配置变动报告：

```csharp
string baseline = MxEditorUtils.LoadConfigHealthBaseline();
ConfigChangeReport changes = MxEditorUtils.DetectConfigChanges(health, baseline);
string changeReport = MxEditorUtils.CreateConfigChangeReportText(changes);
MxEditorUtils.SaveConfigHealthBaseline(health);
```

导出配置报告包：

```csharp
ConfigReportExportResult result =
    MxEditorUtils.ExportConfigReportBundle(source, health, issues, changes, previewRows: 5);
```

默认导出目录是 `Temp/MxFrameworkReports/Config/`，包含：

- `config_health.txt`：整体健康报告。
- `config_issues.txt`：结构化问题列表。
- `config_changes.txt`：配置统计变动报告。
- `config_ai_context.txt`：当前配置源的 AI 修复上下文。
- `config_precommit.txt`：提交前检查结果。
- `config_report_index.txt`：报告包索引。

`config_precommit.txt` 的规则：

- `result: ready`：没有 Error 或 Warning，可以提交。
- `result: warning`：没有 Error，但存在 Warning，可以提交但必须人工确认。
- `result: blocked`：存在 Error，不应提交。

AI Agent 辅助修配置时，优先读取 `config_precommit.txt` 和 `config_report_index.txt`，再按任务读取具体报告；不要依赖 Unity Console 截图或临时剪贴板内容。

## 9. Buff Runtime Patch / Mod v0

运行时动态配置不直接修改编辑源，而是把 Base 表、热更 Patch、Mod 和 Debug 覆盖层合并成新的只读运行时表。

当前先以 Buff / Modifier 做垂直切片：

```csharp
BasicBuffConfig baseBuff = new BasicBuffConfig(
    100001,
    new LocalizedTextKey("buff.fire.name"),
    new LocalizedTextKey("buff.fire.desc"),
    duration: 5f,
    maxLayers: 1,
    modifierId: 200001);

BasicBuffConfig patchedBuff = new BasicBuffConfig(
    100001,
    new LocalizedTextKey("buff.fire.name"),
    new LocalizedTextKey("buff.fire.desc"),
    duration: 8f,
    maxLayers: 3,
    modifierId: 200001);

ConfigPatchMergeResult<BasicBuffConfig> result =
    RuntimeConfigPatchMerger.Merge(
        BasicBuffConfig.CreateSchema(),
        new[] { baseBuff },
        new[]
        {
            ConfigPatchEntry<BasicBuffConfig>.Upsert(
                patchedBuff,
                ConfigLayerKind.Patch,
                sourceId: "hotfix")
        });

BasicBuffConfig runtimeBuff = result.Table.GetConfig<BasicBuffConfig>(100001);
string changeReport = result.ChangeSet.ToReportText();
```

Mod 新增 Buff / Modifier 的推荐流程：

```csharp
ConfigPatchMergeResult<BasicModifierConfig> modifiers =
    RuntimeConfigPatchMerger.Merge(
        BasicModifierConfig.CreateSchema(),
        baseModifiers,
        modModifierPatches);

ConfigPatchMergeResult<BasicBuffConfig> buffs =
    RuntimeConfigPatchMerger.Merge(
        BasicBuffConfig.CreateSchema(),
        baseBuffs,
        modBuffPatches);

var registry = new ConfigRegistry();
registry.RegisterProvider<BasicBuffConfig>(buffs.Table);
registry.RegisterProvider<BasicModifierConfig>(modifiers.Table);

ConfigTableValidationReport report = buffs.Table.Validate(registry, localizationProvider);
```

当前 v0 支持：

- `Upsert`：新增或覆盖同 ID 行。
- `Remove`：从运行时 merged view 删除指定 ID。
- `ConfigLayerKind.Base / Patch / Mod / Debug`：记录变更来源层。
- `ConfigChangeSet`：输出新增、替换、删除、无变化及 sourceId。

当前 v0 不做：

- 不解析 Mod 文件包。
- 不保存回 TSV / JSON。
- 不做字段级 Patch。
- 不直接通知 BuffPipeline 或运行时对象；后续由 `ConfigReloadService` 和变更事件接入。

生成表模板：

```csharp
ConfigAuthoringTemplate template =
    ConfigAuthoring.CreateTemplate(BasicBuffConfig.CreateSchema());

string header = template.HeaderLine;
string sample = template.SampleLine;
string aiReadableText = template.Text;
```

生成结构化校验报告：

```csharp
ConfigAuthoringReport report =
    ConfigAuthoring.ValidateTable(buffs, registry, localizationProvider);

string reportText = ConfigAuthoring.ExportReportText(report);
```

校验表格到 Graph / Localization 的跨源引用：

```csharp
var graphSchema = new ConfigSchema(
    "GameActionGraph",
    null,
    structureKind: ConfigStructureKind.Graph);

var sourceIndex = new ConfigSourceIndex();
sourceIndex.Register(
    new ConfigSourceEntry(graphSchema)
        .AddKey(9001));

ConfigTableValidationReport crossSourceReport =
    sourceIndex.ValidateCrossSourceReferences(aiActionIndexTable);
```

约定：

- `HeaderLine` 和 `SampleLine` 使用 TSV，方便复制到表格工具。
- 业务表里的多语言字段只保存 `LocalizedTextKey`。
- 具体语言文本由 `ILocalizationProvider` 提供。
- 表格引用 Graph、Localization 或运行时产物时，先把目标源登记到 `ConfigSourceIndex`。
- 具体导入器不进入框架核心，只要最后产出 `ConfigTable<T>`。

## 10. 配置驱动 Buff

`BuffPipeline` 不知道 Config。配置到 Buff 的桥接放在 `MxFramework.Config.Runtime`。

```csharp
var buffs = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
buffs.Add(new BasicBuffConfig(
    100001,
    new LocalizedTextKey("buff.burn.name"),
    new LocalizedTextKey("buff.burn.desc"),
    duration: 5f,
    maxLayers: 3));

IBuffFactory factory = new ConfigBuffFactory<BasicBuffConfig>(buffs);
var pipeline = new BuffPipeline(factory);

if (pipeline.TryAddBuff(100001, target, out IBuff buff))
{
    // Buff 已进入 pipeline，后续由 Tick 推进生命周期。
}
```

如果游戏需要自定义 Buff 类型，给 `ConfigBuffFactory<TConfig>` 传入创建函数：

```csharp
IBuffFactory factory = new ConfigBuffFactory<BasicBuffConfig>(
    buffs,
    config => new MyGameBuff(config));
```

## 11. 配置驱动 Modifier

`ModifierPipeline` 同样通过 factory 接入配置。

```csharp
var modifiers = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
modifiers.Add(new BasicModifierConfig(
    200001,
    new LocalizedTextKey("mod.power.name"),
    new LocalizedTextKey("mod.power.desc"),
    paramIndex: 1,
    parameters: new[] { 10, 20 }));

IModifierFactory factory = new ConfigModifierFactory<BasicModifierConfig>(modifiers);
var pipeline = new ModifierPipeline(attributeOwner, factory);

pipeline.TryAddModifier(200001, out IModifier modifier);
```

如果游戏需要条件、效果或属性公式，仍然在游戏层创建具体 `IModifierCondition` / `IModifierEffect`，不要把业务逻辑写进框架配置类。

## 12. AI 轻量 Planner

AI v1 是内置轻量基础设施，不需要第三方插件。

核心概念：

- `AiFactKey`：事实 key，例如 `enemy.visible`、`has.weapon`。
- `IAiWorldState`：事实集合。
- `IAiGoal`：目标，判断当前世界是否满足。
- `IAiAction`：动作，包含前置条件和效果。
- `SequentialPlanner`：按目标优先级选目标，再搜索可执行动作链。

示例：

```csharp
var hasWeapon = new AiFactKey("has.weapon");
var enemyVisible = new AiFactKey("enemy.visible");
var enemyDefeated = new AiFactKey("enemy.defeated");

var world = new AiWorldState();
world.SetValue(hasWeapon, false);
world.SetValue(enemyVisible, false);
world.SetValue(enemyDefeated, false);

var goals = new IAiGoal[]
{
    new AiFactGoal<bool>(1, priority: 10f, enemyDefeated, true)
};

var actions = new IAiAction[]
{
    new AiAction(
        id: 10,
        cost: 1f,
        effects: new[] { new AiSetFactEffect<bool>(hasWeapon, true) }),

    new AiAction(
        id: 20,
        cost: 1f,
        preconditions: new[] { new AiFactCondition<bool>(hasWeapon, true) },
        effects: new[] { new AiSetFactEffect<bool>(enemyVisible, true) }),

    new AiAction(
        id: 30,
        cost: 1f,
        preconditions: new IAiCondition[]
        {
            new AiFactCondition<bool>(hasWeapon, true),
            new AiFactCondition<bool>(enemyVisible, true)
        },
        effects: new[] { new AiSetFactEffect<bool>(enemyDefeated, true) })
};

var planner = new SequentialPlanner(maxDepth: 4);
if (planner.TryPlan(world, goals, actions, out AiPlan plan))
{
    for (int i = 0; i < plan.Actions.Count; i++)
    {
        IAiAction action = plan.Actions[i];
        // 游戏层决定如何真正执行动作。
    }
}
```

注意：

- Planner 规划时 clone 世界状态，不修改传入的 `world`。
- 框架动作的 `Apply` 只修改模拟状态；真实移动、攻击、放技能由游戏层执行。
- WGame 的 GOAP 经验可以映射到这套接口，但不要把 WGame 的具体枚举、实体、技能逻辑放进框架。

## 13. Resources 资源管理

资源系统用 `ResourceKey` 描述稳定引用，用 `ResourceManager` 统一加载、释放、诊断和 Catalog 合并。

```csharp
var provider = new MemoryResourceProvider()
    .Register("demo/title", "Hello");

var resources = new ResourceManager();
resources.RegisterProvider(provider);
resources.AddCatalog(new ResourceCatalog(
    "demo.catalog",
    "demo.package",
    new[]
    {
        new ResourceCatalogEntry(
            "demo.text.title",
            ResourceTypeIds.String,
            "memory",
            "demo/title")
    }));

ResourceLoadResult<ResourceHandle<string>> result =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));

if (result.Success)
{
    string value = result.Value.Value;
    resources.Release(result.Value);
}
```

变体解析通过显式 profile 控制，Provider 不参与 fallback：

```csharp
resources.SetVariantProfile(new ResourceVariantProfile(
    "pc.high",
    new[] { "pc", "high", "default", string.Empty }));

ResourceLoadResult<ResourceHandle<string>> variantResult =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String));
```

RetainPolicy 可以减少短时间重复加载 / 卸载造成的资源抖动：

```csharp
resources.SetRetainPolicy(ResourceRetainPolicy.Timed(durationSeconds: 3f));

ResourceHandle<string> handle =
    resources.Load<string>(new ResourceKey("demo.text.title", ResourceTypeIds.String)).Value;
resources.Release(handle);

// noEngine 层不依赖 Unity 时间，由组合根显式推进。
resources.AdvanceRetainTime(3f);
```

Remote Bundle Provider 使用 Catalog 的 `providerData` 指定 source 和 cache key：

```csharp
var remoteProvider = new RemoteBundleProvider(cacheRootPath);
resources.RegisterProvider(remoteProvider);
resources.AddCatalog(new ResourceCatalog(
    "remote.demo",
    "mod.demo",
    new[]
    {
        new ResourceCatalogEntry(
            "demo.text.remote",
            ResourceTypeIds.TextAsset,
            RemoteBundleProvider.Id,
            "demo-bundle|Assets/TestAssets/MxFramework/ResourcesDemo/resource_demo_text.txt",
            hash: "sha256:...",
            providerData: new Dictionary<string, string>
            {
                { "url", "file:///path/to/demo-bundle" },
                { "bundleName", "demo-bundle" },
                { "cacheKey", "demo.bundle.v1" }
            })
    }));
```

Provider 选择建议：

| 使用场景 | 推荐 |
| --- | --- |
| noEngine 测试 | `MemoryResourceProvider` |
| Demo、小样例、少量固定资源 | `ResourcesProvider` |
| 正式本地运行时资源 | `AssetBundleProvider` |
| Mod / DLC / 外部包下载 | `RemoteBundleProvider` |
| 项目已使用 Addressables Groups / Remote Catalog | 独立可选 Addressables Adapter |

Addressables 不是默认依赖。只有项目已经安装并决定使用 Addressables 时，才应新增独立 `MxFramework.Resources.Addressables` 适配层；业务代码仍然只接触 `ResourceKey`、`ResourceHandle<T>` 和 `IResourceManager.Release`。

Demo / 测试场景中的 UI、材质、图标、prefab 等运行时资源不应再放入 `Assets/Resources`。Runtime HUD 默认 `PanelSettings`、`GameplayShowcase.uxml`、`GameplayShowcase.uss` 已迁到 `Assets/UI/MxFramework/Showcase`，由 Runner、场景 bootstrap 或测试显式注入；纯调试材质优先运行时生成，不放入 `Resources`。

UI Toolkit 可玩 Demo 必须显式绑定正式 `PanelSettings`、Runtime Theme、UXML 和 USS。验收时不能只检查 visual tree 节点存在，还要在 Play Mode 读取关键 `Label` / `Button` 的 `text` 与 `resolvedStyle.color`，确认文本非空且 alpha > 0。若 Unity Runtime Theme / USS 在当前版本下不稳定，Demo controller 必须在初始化后对关键 `Label` / `Button` 设置 font、font size、color 和 alignment 作为兜底。

预加载 group / scene warmup 可以作为独立策略服务使用，不需要修改 `IResourceManager`：

```csharp
var preload = new ResourcePreloadService(resources);
IResourceOperation<ResourcePreloadResult> preloadOp =
    preload.PreloadAsync(new ResourcePreloadPlan(
        "combat",
        labels: new[] { "warmup.combat" },
        explicitKeys: new[] { new ResourceKey("demo.text.title", ResourceTypeIds.String) }));

if (preloadOp.Result.Success)
{
    ResourcePreloadResult preloadResult = preloadOp.Result.Value;
    if (preloadResult.Success)
    {
        // Activate scene or open UI after warmup succeeds.
    }

    preload.ReleaseGroup(preloadResult.Handle);
}
```

Mod Package 可在 `mod.json` 中声明可选资源目录：

```json
{
  "resourceCatalog": "resources/catalog.json"
}
```

加载包后由组合根决定是否挂载：

```csharp
RuntimeModPackageLoadResult package = RuntimeModPackageLoader.LoadFromDirectory(packageRoot);
if (package.HasResourceCatalog)
{
    ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromFile(package.ResourceCatalogFilePath);
    resources.AddCatalog(catalog);
}
```

运行时诊断：

```csharp
var debugSource = new ResourceDebugSource(resources);
string report = FrameworkDebugReportExporter.ExportText(debugSource.CreateSnapshot());
```

Editor 校验：

```csharp
ResourceCatalogValidationReport validation =
    ResourceCatalogEditorValidator.ValidateCatalog(catalog, new[] { "resources", "assetBundle" });
```

约定：

- 业务配置只保存 `ResourceKey`，不保存 Unity 路径或 `UnityEngine.Object`。
- `ResourcesProvider` 只用于 Demo、小样例和少量常驻资源。
- Catalog JSON 覆盖字段使用 `allowOverride`。
- `PackageId` 非空时精确路由；为空时走全局覆盖结果。
- Variant fallback 必须显式配置；框架不会自动把 `pc.high` 拆成 `pc` / `high`。
- RetainPolicy 不改变外部 handle 语义，`Release(handle)` 后 handle 仍为 released。
- RemoteBundle 第一版只提供 file/local HTTP 最小闭环和 SHA-256 校验；复杂重试、断点续传、签名和 CDN 发布不在框架当前范围内。
- Addressables Adapter 后置为可选项，不进入 `MxFramework.Resources.Unity` 默认依赖。
- Catalog labels 可用于 warmup 分组，例如 `warmup.combat`、`scene.demo`、`ui`。
- Preload group 只持有并释放 asset handles，不负责场景激活、进度 UI 或实例销毁。
- Runtime Showcase 可通过 `MxFramework / Runtime Showcase / Generate Resource Catalog` 生成 `Assets/Config/MxFramework/Demo/runtime_vertical_slice_resource_catalog.json`；`RuntimeVerticalSliceRunner` 使用 `warmup.runtime_vertical_slice` label 预热已序列化的 Demo 资源并在销毁时释放 group。
- `GameObject` 实例由调用方销毁，资源系统只管理 prefab asset handle。

## 14. Input 输入系统

Input 模块把 Unity Input System 的 Action/Binding 封装成业务意图。角色、相机、UI 和自动化测试依赖 `IInputProvider`，不直接读设备或 `InputAction`。

```csharp
public sealed class CharacterControllerAdapter
{
    private readonly IInputProvider _input;

    public CharacterControllerAdapter(IInputProvider input)
    {
        _input = input;
    }

    public void Tick(float deltaTime)
    {
        Vector2 move = _input.Snapshot.Move;
        bool jump = _input.Snapshot.JumpPressed;
    }
}
```

Unity 场景接入：

1. 确认项目安装 `com.unity.inputsystem`。
2. 创建或复用 `InputActionAsset`，Action Map 默认使用 `Gameplay` / `UI`。
3. 场景中挂 `InputService`，把 asset 指到 `Assets/Input/MxFramework/Config/MxFrameworkInputActions.inputactions` 或项目自己的主输入资产。
4. 打开菜单、背包、弹窗时通过 `PushContext(InputContext.UI)` 临时切换；关闭时释放返回的 `IDisposable`。

```csharp
IDisposable uiScope = input.PushContext(InputContext.UI);
uiScope.Dispose();
```

测试和 AI 接管可以直接替换输入源：

```csharp
var fake = new FakeInputProvider();
fake.SetContext(InputContext.Gameplay);
fake.SetSnapshot(new InputSnapshot(
    move: new Vector2(1f, 0f),
    look: Vector2.zero,
    navigate: Vector2.zero,
    point: Vector2.zero,
    scroll: Vector2.zero,
    throttle: 0f,
    jumpPressed: true,
    jumpHeld: true,
    jumpReleased: false,
    attackPrimaryPressed: false,
    attackPrimaryHeld: false,
    attackSecondaryPressed: false,
    interactPressed: false,
    dodgePressed: false,
    sprintHeld: false,
    submitPressed: false,
    cancelPressed: false,
    pausePressed: false,
    debugTogglePressed: false));
```

约定：

- 业务层只消费 `InputSnapshot` 和 `InputCommandQueue`。
- 瞬时输入可由 `InputCommandQueue` 转成游戏自己的 `RuntimeCommand`。
- 本地多人使用 `LocalUserInputAdapter` 读取 `PlayerInput.actions` 的私有副本。
- 重绑定通过 `IInputRebindingService.StartRebind("Gameplay/Jump", bindingIndex)` 触发，结果保存到 `PlayerPrefs`。

## 15. Audio 音频系统

Audio 模块的业务入口是 `IAudioService` / `AudioService`。普通测试、服务器和未安装 FMOD 的环境使用 `NullAudioBackend`，不会依赖 Unity 或 FMOD。

```csharp
using System.Collections.Generic;
using MxFramework.Audio;

const int BusSfx = 10;
const int EventClick = 1001;
const int EventAura = 2001;
const int ParamIntensity = 1;

var definitions = new MemoryAudioDefinitions();
definitions.AddBus(new AudioBusDefinition(BusSfx, "SFX", "bus:/SFX"));
definitions.AddEvent(new AudioEventDefinition(
    EventClick,
    "ui.click",
    "event:/ui/click",
    string.Empty,
    AudioEventKind.Event,
    BusSfx,
    is3D: false,
    isLoop: false,
    maxDistance: 0f));
definitions.AddEvent(new AudioEventDefinition(
    EventAura,
    "combat.aura",
    "event:/combat/aura",
    string.Empty,
    AudioEventKind.Event,
    BusSfx,
    is3D: true,
    isLoop: true,
    maxDistance: 30f,
    parameters: new[]
    {
        new AudioParameterDefinition(ParamIntensity, "Intensity")
    }));

using (var audio = new AudioService(definitions, new NullAudioBackend()))
{
    AudioPlayResult oneShot = audio.PlayOneShot(AudioPlayRequest.Create2D(EventClick));

    AudioPlayResult started = audio.StartEvent(
        AudioPlayRequest.Create2D(EventAura),
        out AudioHandle auraHandle);
    if (started.Success)
    {
        audio.SetParameter(auraHandle, ParamIntensity, 0.75f);
        audio.SetBusVolume(BusSfx, 0.8f);
        audio.Tick(1f / 60f);
        audio.Stop(auraHandle, AudioStopMode.AllowFadeout);
    }

    AudioDebugSnapshot snapshot = audio.CaptureSnapshot();
}

public sealed class MemoryAudioDefinitions : IAudioDefinitionProvider
{
    private readonly Dictionary<int, AudioEventDefinition> _events =
        new Dictionary<int, AudioEventDefinition>();

    private readonly Dictionary<int, AudioBusDefinition> _buses =
        new Dictionary<int, AudioBusDefinition>();

    public void AddEvent(AudioEventDefinition definition)
    {
        _events[definition.Id] = definition;
    }

    public void AddBus(AudioBusDefinition definition)
    {
        _buses[definition.Id] = definition;
    }

    public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
    {
        return _events.TryGetValue(eventId, out definition);
    }

    public bool TryGetBus(int busId, out AudioBusDefinition definition)
    {
        return _buses.TryGetValue(busId, out definition);
    }

    public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
    {
        definition = default;
        if (!_events.TryGetValue(eventId, out AudioEventDefinition audioEvent))
            return false;

        AudioParameterDefinition[] parameters =
            audioEvent.Parameters ?? AudioEventDefinition.EmptyParameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Id == parameterId)
            {
                definition = parameters[i];
                return true;
            }
        }

        return false;
    }
}
```

约定：

- One-shot 使用 `PlayOneShot`，循环或需要后续控制的声音使用 `StartEvent` 并保存 `AudioHandle`。
- `SetBusVolume` 使用稳定 bus id，范围由后端 clamp 到 `0..1`。
- `NullAudioBackend` 只验证意图、句柄和诊断，不声明真实出声。

## 16. Combat 战斗模块

Combat 当前落地的是确定性战斗物理查询和 kinematic motion。不要假定存在未实现的 `CombatWorld` 或完整动作系统；组合根应直接装配 `CombatPhysicsWorld`、查询对象和 `CombatKinematicMotor`。

Physics Query 最小示例：

```csharp
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

var world = new CombatPhysicsWorld();
world.UpsertBody(new CombatPhysicsBody(
    new CombatEntityId(1),
    new CombatBodyId(1),
    FixVector3.Zero));
world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
    new CombatBodyId(1),
    new CombatColliderId(1),
    layer: 1,
    localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
    localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));

var header = new CombatQueryHeader(
    queryId: 1,
    kind: CombatQueryKind.Aabb,
    sourceEntityId: CombatEntityId.None,
    traceId: 0,
    actionId: 0,
    sourceOrder: 0,
    layerMask: CombatPhysicsLayerMask.FromLayer(1));
var query = new CombatAabbQuery(
    header,
    new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
    new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)));

var hits = new List<CombatQueryResult>();
int hitCount = world.Query(CombatPhysicsQuery.From(query), hits);
```

Motion 最小示例：

```csharp
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Core.Math;

var motor = new CombatKinematicMotor(CombatMotionConfig.Default);
var state = new CombatMotionState(
    CombatFrame.Zero,
    FixVector3.Zero,
    FixVector3.Zero,
    grounded: true,
    lastCollisionNormal: FixVector3.Zero,
    collisionFlags: CombatMotionCollisionFlags.None);
var input = new CombatMotionInput(
    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    jumpPressed: false);

CombatMotionStepResult step = motor.Step(state, input);
CombatMotionState nextState = step.State;
```

约定：

- `CombatPhysicsWorld` 只注册已落地的 body 和 AABB collider；查询结果按确定性规则排序。
- `CombatKinematicMotor.Step(world, bodyId, state, input)` 可把移动结果同步回已注册 body。
- Combat 使用 `Fix64` / `FixVector3`，不要在核心战斗逻辑中直接读 Unity Physics。

## 17. App / Scene Flow

AppFlow 表达 App 状态流转；SceneFlow 串行编排场景加载。Runtime 层只使用稳定字符串 key，Unity 场景名或 path 由 Unity adapter / 项目组合根映射。

AppFlow 状态注册和切换：

```csharp
using MxFramework.Runtime;

var appFlow = new AppFlowController();
appFlow.RegisterState(new SimpleAppState("Boot", tick =>
{
    tick.RequestTransition("Gameplay", "boot-complete");
}));
appFlow.RegisterState(new SimpleAppState("Gameplay"));

AppFlowTransitionResult start = appFlow.Start("Boot", "launch");
appFlow.Tick(frameIndex: 1, deltaTime: 1d / 60d, elapsedTime: 1d / 60d);
appFlow.Tick(frameIndex: 2, deltaTime: 1d / 60d, elapsedTime: 2d / 60d);

AppFlowSnapshot appSnapshot = appFlow.CaptureSnapshot();

public sealed class SimpleAppState : IAppFlowState
{
    private readonly System.Action<AppFlowTickContext> _onTick;

    public SimpleAppState(string stateId, System.Action<AppFlowTickContext> onTick = null)
    {
        StateId = stateId;
        _onTick = onTick;
    }

    public string StateId { get; }
    public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
    public void Tick(AppFlowTickContext context) { _onTick?.Invoke(context); }
    public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
}
```

SceneFlow 加载示例：

```csharp
using MxFramework.Runtime;

var sceneFlow = new SceneFlowController(new ImmediateSceneFlowDriver(), "Boot");
SceneFlowResult accepted = sceneFlow.RequestLoad(new SceneFlowRequest(
    "Gameplay",
    SceneFlowLoadMode.Single,
    unloadPreviousScene: true));
sceneFlow.Tick();
SceneFlowSnapshot sceneSnapshot = sceneFlow.CaptureSnapshot();

public sealed class ImmediateSceneFlowDriver : ISceneFlowDriver
{
    public ISceneFlowOperation LoadScene(SceneFlowRequest request)
    {
        return new DoneSceneFlowOperation(request.SceneKey);
    }

    public ISceneFlowOperation UnloadScene(string sceneKey)
    {
        return new DoneSceneFlowOperation(sceneKey);
    }
}

public sealed class DoneSceneFlowOperation : ISceneFlowOperation
{
    public DoneSceneFlowOperation(string sceneKey)
    {
        SceneKey = sceneKey;
    }

    public string SceneKey { get; }
    public bool IsDone => true;
    public float Progress => 1f;
    public bool Success => true;
    public SceneFlowError Error => SceneFlowError.None;
}
```

约定：

- 状态在自身 `Tick` 中只请求 pending transition，真正 `Exit` / `Enter` 在下一次 `Tick` 开始执行。
- SceneFlow busy 时会拒绝新的 load request；调用方读取 `SceneFlowResult.Error` 做 UI 或日志。
- Unity 项目中使用 `UnitySceneFlowDriver`，但 `MxFramework.Runtime` 本身不引用 `SceneManager`。

## 18. Diagnostics 诊断

Diagnostics 模块用 `IFrameworkDebugSource` 暴露只读快照。Editor、工具和运行时 HUD 读取 snapshot，不直接读模块私有字段。

```csharp
using System.Collections.Generic;
using MxFramework.Diagnostics;

IFrameworkDebugSource source = new RuntimeCounterDebugSource("runtime-counter", 42);
if (source.IsAvailable)
{
    FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
    string report = FrameworkDebugReportExporter.ExportText(snapshot);
}

public sealed class RuntimeCounterDebugSource : IFrameworkDebugSource
{
    private readonly int _value;

    public RuntimeCounterDebugSource(string name, int value)
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }
    public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
    public bool IsAvailable => true;

    public FrameworkDebugSnapshot CreateSnapshot()
    {
        return new FrameworkDebugSnapshot(
            Name,
            Mode,
            new List<FrameworkDebugSection>
            {
                new FrameworkDebugSection("Counters", "Value=" + _value)
            });
    }
}
```

约定：

- Snapshot 是只读调试报告，不是 SaveState、网络协议或可写命令入口。
- 可写调试操作要另设 command API，不要塞进 `FrameworkDebugSection.Body`。
- 游戏层在组合根中持有 debug source 列表，框架不提供全局 registry。

## 19. Gameplay Component Runtime

Gameplay Component Runtime 使用 generation-safe `GameplayEntityId`、`GameplayComponentWorld`、component store 和 system pipeline。下面示例用 spawn definition 创建实体，并注册一个自定义 system 读取同一个 `ComponentWorld`。

```csharp
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

const int ActorDefinitionId = 1001;

var world = new GameplayComponentWorld();
GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);

var spawnRegistry = new GameplayComponentSpawnRegistry();
spawnRegistry.Register(new GameplayComponentSpawnDefinition(
    ActorDefinitionId,
    "example.actor",
    schemaVersion: 1,
    initializers: new IGameplayComponentSpawnInitializer[]
    {
        new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
            GameplayCoreComponentSchemaDescriptors.IdentityStableId,
            new GameplayIdentityComponent(ActorDefinitionId)),
        new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
            GameplayCoreComponentSchemaDescriptors.TeamStableId,
            new GameplayTeamComponent(1)),
        new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
            GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
            GameplayLifecycleComponent.Alive)
    }));

var commandBuffer = new RuntimeCommandBuffer();
var module = new GameplayRuntimeModule(
    new GameplayWorld(),
    new GameplayAbilityRegistry(),
    commandBuffer,
    tickWorldAutomatically: false,
    configureDefaultPipeline: pipeline =>
    {
        pipeline.Add(new GameplayComponentSpawnCommandSystem(spawnRegistry));
        pipeline.Add(new CountAliveSystem());
    },
    componentWorld: world);

commandBuffer.Enqueue(GameplayRuntimeCommandFactory.SpawnComponentEntity(
    RuntimeFrame.Zero,
    ActorDefinitionId,
    traceId: "spawn-actor"));
module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

GameplayEntityId entity = world.CreateEntitySnapshot()[0];
GameplayComponentStore<GameplayTeamComponent> teams =
    world.GetOrCreateStore<GameplayTeamComponent>();
bool hasTeam = teams.TryGet(entity, out GameplayTeamComponent team);

public sealed class CountAliveSystem : IGameplaySystem
{
    public string SystemId => "example.count-alive";
    public GameplaySystemPhase Phase => GameplaySystemPhase.Diagnostics;
    public int Priority => 0;
    public bool IsEnabled => true;

    public void Tick(GameplaySystemContext context)
    {
        int alive = context.ComponentWorld.CountAlive;
    }
}
```

约定：

- Component store 只接受 `GameplayEntityId`，不要用裸 int entity id 当 key。
- Spawn definition 是组合根输入，不是 world state；SaveState 保存 spawn 后的实体和组件结果。
- 自定义 system 通过 `GameplaySystemContext.ComponentWorld` 访问 component runtime，不直接 drain `RuntimeCommandBuffer`。

## 20. 推荐组合根

游戏层可以集中装配框架模块：

```csharp
public sealed class GameFrameworkBootstrap
{
    public ConfigRegistry Configs { get; }
    public IBuffFactory BuffFactory { get; }
    public IModifierFactory ModifierFactory { get; }
    public IAiPlanner AiPlanner { get; }

    public GameFrameworkBootstrap(
        ConfigRegistry configs,
        ILocalizationProvider localizationProvider)
    {
        Configs = configs;
        BuffFactory = new ConfigBuffFactory<BasicBuffConfig>(configs);
        ModifierFactory = new ConfigModifierFactory<BasicModifierConfig>(configs);
        AiPlanner = new SequentialPlanner(maxDepth: 6);
    }
}
```

组合根可以在 Unity `MonoBehaviour`、服务器入口或测试里创建。框架本身不提供全局单例。

## 21. AI Agent 如何找文档

AI agent 进入项目后应按这个顺序读取：

1. `AGENTS.md`：确认项目边界、红线和文档入口。
2. `Docs/README.md`：确认文档职责。
3. `Docs/USAGE.md`：直接查接入流程和最小示例。
4. `Docs/INTERFACES.md`：确认模块接口文档入口和依赖矩阵。
5. `Docs/Interfaces/<Module>.md`：查看具体模块 API 和约定。
6. 具体测试文件：需要可运行样例时看 `Assets/Scripts/MxFramework/Tests/`。

不要让 AI 先通读 `Assets/Scripts/MxFramework/**`。只有当 `USAGE.md` 和测试示例不够时，才定位读取具体源码。

可视化编辑器约定：

- Unity Editor 工具的用户可见文案默认使用中文。
- 类名、程序集名、菜单路径、配置 key 和日志中的技术标识保留英文。
- 正式编辑器使用 UI Toolkit；IMGUI 只用于临时调试工具。
- 编辑器分为 `编辑模式` 和 `运行模式`。
- 编辑模式不依赖 Play Mode，用于配置、Schema、依赖和静态校验。
- 运行模式默认只读，通过 `IFrameworkDebugSource` 提供快照。
- 不把运行时对象反向写回配置表。
- 所有验证和沙盒结果都应能复制或导出报告。

## 22. 提交前检查

项目通用提交前流程见 `Docs/WORKFLOW.md`。每次改框架基础能力后至少确认：

1. Unity 编译无错误。
2. 相关 EditMode / PlayMode 测试通过，或说明无法运行原因。
3. `git status` 只包含本次相关文件。
4. 按 `Docs/GITNEXUS.md` 运行 GitNexus 影响面检查。
5. 如果本次涉及配置表，打开 `MxFramework > Framework Manager > 编辑模式 > 配置工作台`，点击 `提交前检查`，优先查看 `Temp/MxFrameworkReports/Config/config_precommit.txt`。

当前已覆盖的关键测试：

- Config 表读取、ID 范围、引用和多语言校验。
- Config authoring 模板和报告。
- Config-backed Buff/Modifier factory。
- Buff/Modifier pipeline 基础行为。
- AI WorldState、目标选择、动作前置条件和规划链。
- Input 上下文栈、命令队列、Fake/Recorded 输入源。
