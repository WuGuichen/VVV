# MxFramework 使用手册

> 版本 0.3.5 | 2026-05-18
>
> 本文面向业务开发和 AI 辅助开发。目标是“先看这里就能接入”，不要靠通读源码理解基础模块。

## 1. 使用原则

- 框架只提供通用机制；具体属性 ID、Buff ID、AI 事实 key、技能和怪物逻辑都由游戏层定义。
- 游戏层负责组合根，把 Config、Buff、Modifier、AI 的默认实现装配起来。
- Runtime 不依赖 UnityEditor，不依赖 WGame、Entitas、Luban 或 AI 插件。
- 配置表先通过 `ConfigSchema` 描述，再由项目自己的 Excel/CSV/Json/Luban/ScriptableObject 适配器转成 `ConfigTable<T>`。

角色资源包 Runtime Spawn 第一切片当前是 `Runtime Slice`：使用 `CharacterImportedPackageJson.LoadFromDirectory("Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard")` 读取 #222 导入产物，再通过 `CharacterRuntimeSpawnResolver.Resolve(...)` 或 `CharacterRuntimeSpawnModule` 得到 `CharacterRuntimeBinding`。该切片用于验证 resolver、gate、resource mapping、geometry binding 和 runtime id plan。

导入完成后可以在 Unity 中生成可摆放到实际场景的预览 Prefab：执行菜单 `MxFramework/Character/Create Preview Prefab For Iron Vanguard`，产物写入 `Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard/prefabs/iron_vanguard_character_preview.prefab`。也可以执行 `MxFramework/Character/Create Preview Scene For Iron Vanguard` 生成 `Assets/Scenes/MxFramework/CharacterImportedPreview.unity`。需要运行时手测 locomotion 动画时，执行 `MxFramework/Character/Create Locomotion Calibration Scene For Iron Vanguard` 生成 `Assets/Scenes/MxFramework/CharacterLocomotionCalibration.unity`；该场景只放置 `CharacterRuntimeResourceBootstrap` / `CharacterLocomotionCalibrationRunner` 组合根，Play Mode 中通过 `ResourceManager`、`ResourcesProvider`、`animation_set_definition.json` 和 `animation_clip_registry.json` 加载 Iron Vanguard、默认武器和动画资源，不把最终角色 prefab 作为常驻场景对象。校准 HUD 会显示 blend domain、当前 sample、BlendTree 点、不可达诊断、当前 clip weights 和 dominant clip；同一数据也会进入 `CharacterRuntimeAnimationDebugSource` 的 `Locomotion Blend Probe` 区块。该 Prefab 会读取导入后的 `config/*.json`，复用 `CharacterRuntimeSpawnResolver` 做 gate / binding 校验，然后把角色主体模型、武器模型、挂点和 authoring 碰撞体装配成 Unity GameObject 层级；如果项目缺少 GLB importer，会使用占位体并在 Console 输出提示。

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
3. 用 `WASD` 或方向键驱动 Player；输入先进入 `InputCharacterCommandSource`，再经 `CharacterControlStateMachine` / `CharacterMotionResolver` 驱动 Combat Motion；`Space` 起跳，角色会受重力下落、落地、撞墙或撞顶。
4. 左键拖动 Player / Enemy marker 可直接改位置；右键拖动镜头。核心 follow / orbit 已通过 `MxCameraUnityRig` 求值并应用，`Camera.main` 只保留给 pointer ray 这类 Unity 查询。
5. 按 `Q` 在 `Capsule / Ray / Sphere / Aabb / Sector` 间切换。
6. 按 `P` 或点击 `Probe` 执行一次只探测不扣血的查询。
7. 按 `J` 执行 Character Control combat action；命令会经 `CharacterActionController` / `CombatActionRunner`，再复用现有 HitResolve 可视化伤害链路。点击 `Attack` 仍执行当前选中 marker 的手动攻击。
8. 按 `T` 或点击 `AI Cmd` 运行一次 **Runtime AI Planner** command source；点击 `Break` 触发 Gameplay pressure -> Character Control reaction -> MxAnimation presentation -> Debug UI snapshot。
9. 按 `R` 重置 Showcase；HUD 的 `Mini Game Feedback` 会显示回合、分数、连击、当前 shape、Motion position / velocity / grounded / collision flags、world revision、最近一次 `CombatPhysicsQueryDebugReport` 和 Character Control 诊断摘要。

Combat Physics Playground 约定：

- 物理命中权威来自 `CombatPhysicsWorld.Query` / `ExplainQuery`，不使用 `UnityEngine.Physics` 作为结果来源。
- Player 移动权威来自 `MxFramework.Combat.Motion.CombatKinematicMotor`，并在同一固定帧写回 `CombatPhysicsWorld` body position；移动后 Probe / Attack 必须读取写回后的新位置。
- Character Control 可玩切片权威来自 `RuntimeCombatCharacterControlSlice` 组合根：Local Input、Runtime AI Planner、state machine、motion resolver、action bridge、pressure reaction、MxAnimation presentation 和 `CharacterControlDebugSource` 在同一场景内形成回归闭环。
- Combat Playground 的相机表现来自 `MxFramework.Camera`：`RuntimeCombatShowcaseInputController` 采样 Player / Enemy marker 为 `MxCameraTargetSnapshot`，由 `MxCameraUnityRig` 在 LateUpdate 路径应用；Character Control facing basis 通过 `MxCameraFacingBasisResolver` 从相机 state 派生，Character Control core 不依赖 Camera。
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

### 6.7 MxAnimation Play Mode Smoke

MxAnimation 的可视化 smoke 场景用于肉眼验证真实 Skeleton 模型通过正式 sample resource catalog 加载 `.anim` / `AvatarMask`，并由 `UnityPlayablesAnimationBackend` 播放 1D locomotion blend 和 upper body layer。

代码入口：

- Demo runtime：`Assets/Scripts/MxFramework/Demo/MxAnimationSmoke/MxAnimationSmokeDemoBootstrap.cs`
- 场景生成：`Assets/Scripts/MxFramework/Demo/Editor/CreateMxAnimationSmokeScene.cs`
- 可视化场景：`Assets/Scenes/MxAnimationPlayModeSmoke.unity`
- HUD：`Assets/UI/MxFramework/MxAnimationSmoke/MxAnimationSmokeHud.uxml` / `.uss`

手测方式：

1. 如需重新生成场景，执行 `MxFramework / MxAnimation / Generate Play Mode Smoke Scene`。
2. 打开 `Assets/Scenes/MxAnimationPlayModeSmoke.unity`，直接 Play。
3. 按 `I` / `O` / `P` 把 locomotion speed 设置为 idle / walk / run，`Space` 播放 upper body attack。
4. 观察 Skeleton 模型下半身持续响应 locomotion blend，上半身 layer 可叠加播放攻击；HUD 会显示 speed parameter、blend weights、layer weight、warmup、backend graph、resource loaded/ref count 和 Combat bridge 状态。

资源加载约定：

- Skeleton model、`.anim` clip 和 upper body `AvatarMask` 都先写入 `mxframework.samples` catalog，运行时只使用 `ResourceKey`。
- Editor Play Mode smoke 使用 catalog-backed serialized reference provider 注册 Unity asset，再由 `ResourceManager.Load<GameObject>` / `LoadAsync<AnimationClip>` / `LoadAsync<AvatarMask>` 加载。
- `UnityPlayablesAnimationBackend` 只从 `IResourceManager` 获取 `AnimationClip` handle；demo 不直接把 `AnimationClip` 塞给 backend。
- 1D blend definition、warmup group 和 upper body layer 都来自 `MxAnimationSetDefinition`，不从场景脚本绕过正式 mapping / resource loading 流程。
- Combat action 通过 `CombatMxAnimationUnityBridge` 转成 `CrossFade` 请求，动画时间不反向写入 Combat 权威状态。

### 6.8 MxAnimation System Showcase

MxAnimation System Showcase 是比 smoke 更完整的可视化验收场景。它在同一场景中放置四个 Skeleton actor，分别展示 1D locomotion blend、2D directional blend、upper-body layer + AvatarMask + Combat bridge、Mod override / fallback diagnostics，并在 UI Toolkit HUD 中展示 warmup、package validation、compatibility validation、bake artifact hash、Playable cache 和 resource handle 状态。详细手测流程和验收清单见 `Docs/Demo/MX_ANIMATION_SYSTEM_SHOWCASE.md`。

代码入口：

- Demo runtime：`Assets/Scripts/MxFramework/Demo/MxAnimationShowcase/MxAnimationShowcaseDemoBootstrap.cs`
- 场景生成：`Assets/Scripts/MxFramework/Demo/Editor/CreateMxAnimationShowcaseScene.cs`
- 可视化场景：`Assets/Scenes/MxAnimationSystemShowcase.unity`
- HUD：`Assets/UI/MxFramework/MxAnimationShowcase/MxAnimationShowcaseHud.uxml` / `.uss`

### 6.9 Marble Maze Runtime Showcase

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

### 6.10 UI Camera 3D Validation

UI Camera 3D Validation 用于验证 UI 3D Overlay Camera 路径：主相机作为 URP Base Camera，UI 3D 相机作为 Overlay Camera 加入 camera stack；普通 UI 仍由 UI Toolkit HUD 显示。该入口只验证表现层 camera / layer / HUD 绑定，不承诺玩法闭环，因此交付等级是 `Runtime Slice`。

代码入口：
- Runtime / Composition Root：`Assets/Scripts/MxFramework/Demo/CameraUi/UiCamera3DValidationDemo.cs`
- 场景生成：`Assets/Scripts/MxFramework/Demo/Editor/CreateUiCamera3DValidationScene.cs`
- UI Toolkit：`Assets/UI/MxFramework/CameraUi3DValidation/UiCamera3DValidation.uxml` / `.uss`
- 场景：`Assets/Scenes/UiCamera3DValidation.unity`
- 测试：`Assets/Scripts/MxFramework/Tests/Demo/CameraUi/UiCamera3DValidationDemoTests.cs`

生成或重建场景：
```text
MxFramework/Camera UI/Create 3D Validation Scene
```

手动验证：
1. 打开 `Assets/Scenes/UiCamera3DValidation.unity` 并进入 Play Mode。
2. Game 视图应同时显示世界参考 cube、右侧 UI-layer 3D 物体和左上角 UI Toolkit HUD。
3. HUD 的 `URP Overlay Stack Bound`、`Base excludes UI layer`、`Overlay only UI layer`、`3D UI object on UI layer` 均应显示成功状态。
4. 点击 `Rebind` 应保持 stack 绑定成功；点击 `Pause Spin` / `Resume Spin` 应切换 UI 3D 物体旋转状态。
5. Console 不应出现新增 error。

约束：
- 该 Demo 依赖 `MxFramework.Camera.URP`，URP 依赖保持在 Unity-facing Demo / adapter 层，不进入 noEngine camera core。
- UI 3D 物体使用项目已有 `UI` layer 作为最小验证层；主相机排除该 layer，Overlay Camera 只渲染该 layer。
- 不手写 `.unity`、Material 或 PanelSettings YAML；这些序列化资产由 Unity 菜单创建并保存。

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

### 7.1 Character Application 配置契约

`MxFramework.Character.Application` 提供角色应用层第一批固定配置表和纯解析器。它只描述静态数据、引用关系和解析结果，不生成运行时角色实例。

```csharp
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Config;

var characters = new ConfigTable<CharacterConfig>(CharacterConfig.CreateSchema());
characters.Add(new CharacterConfig(
    new CharacterConfigId(710001),
    "mx.character.iron_vanguard",
    new LocalizedTextKey("character.iron_vanguard.name"),
    new LocalizedTextKey("character.iron_vanguard.desc"),
    new CharacterAttributeProfileId(720001),
    new CharacterBodyProfileId(730001),
    new EquipmentSchemaId(750001),
    new EquipmentLoadoutId(760001),
    new AbilityLoadoutId(790001),
    new CharacterPresentationProfileId(810001),
    CharacterControllerKind.HumanInput,
    "controller.human.default",
    new[] { "sample", "vanguard" }));

ConfigSchema characterSchema = CharacterConfig.CreateSchema();
IReadOnlyList<ConfigSchema> allCharacterSchemas = CharacterApplicationConfigSchemas.CreateAll();
```

配置包可以先用纯解析器在 Workstation、测试或生成前检查中得到 resolved profile：

```csharp
CharacterPackageResolveResult result = CharacterPackageResolver.Resolve(
    new CharacterPackageResolveRequest(
        character,
        attributeProfile,
        bodyProfile,
        bodyParts,
        equipmentSchema,
        selectedLoadout,
        equipmentStates,
        weapons,
        abilityLoadouts,
        combatActionSets,
        presentationProfile));

if (result.ValidationReport.HasErrors)
{
    // 展示 result.ValidationReport.Issues；例如 CHAR_EQUIPMENT_STATE_TIE。
    return;
}

CharacterResolvedProfile profile = result.ResolvedProfile;
EquipmentStateId activeState = profile.ActiveEquipmentStateId;
CharacterAbilityId[] abilities = profile.EffectiveAbilityIds;
CharacterResourceKeyEntry[] resources = profile.RequiredResources;
```

约定：

- `IConfigData.Id` 保持 `int`，跨表字段使用 `CharacterConfigId`、`EquipmentLoadoutId` 等 typed id。
- `StableId` 用于 SaveState、Mod、调试报告和跨版本迁移。
- `CharacterAttributeProfileConfig` 使用 `BaseValue` / `InitialValue`，运行时当前值由 runtime state 保存。
- `CombatActionSetConfig` 只做动作绑定，不复制 Combat action timeline 权威字段。
- 空手、单武器、多槽位武器都通过 `EquipmentLoadoutConfig` 和 `EquipmentStateResolver` 解释。
- Resolver 不读取 Unity 场景对象、不写运行时 world、不加载资源；失败通过 `CharacterDiagnostic.StableCode` 结构化输出。
- SaveState 恢复时应重新解析装备得到 active state，保存的 active state id 只作为 mismatch 诊断线索。

### 7.2 Mod Package 运行时边界（单包 / LoadPlan / 多包 Merge）

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

### 7.3 Mod Diagnostic CLI（命令行诊断）

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

### 7.4 Mod Diagnostic Snapshot（运行时诊断统一入口）

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

### 7.5 EditorServer 诊断 API（Authoring Editor）

`editor serve` 提供 Mod 诊断 API：

- `GET /api/mod/diagnose`
- `POST /api/mod/diagnose`

参数：

- `containers`（数组或 CSV）
- `loadout`（可选）
- `includeAbsolutePaths`（可选，默认 false）

返回：`mx.modDiagnosticSnapshot.v1` JSON。
实现上，CLI `mod diagnose` 与 EditorServer API 复用同一 `ModDiagnosticService.BuildSnapshot(...)`，避免双份诊断逻辑漂移。

## 8. 配置编辑和 Authoring AI Assist（AI 辅助）

> **本文的 AI 指 Authoring AI Assist**，即 LLM 辅助编辑配置数据的功能。
> 不包含 Runtime AI Planner（游戏内 NPC 决策引擎）或 Development Agent（Gitea 自动化工作流）。

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

## 12. Runtime AI Planner（AI 轻量 Planner）

> **本文的 AI 指 Runtime AI Planner**。不包含 AIAction 配置数据、Authoring AI Assist 或 Development Agent 工作流。

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

Retain policy 的选择边界：

- `ResourceRetainPolicy.None`：默认策略，handle 引用计数归零后立即释放底层资源。
- `ResourceRetainPolicy.Timed(...)`：适合短时间来回切换 UI、动画或小型场景 warmup，组合根必须显式推进时间或帧数。
- `ResourceRetainPolicy.KeepAlive`：适合明确需要 pinned 的常驻资源，只通过 `EvictRetainedResources()` 显式清理。
- `ResourceRetainPolicy.Budgeted(maxRetainedBytes)`：适合有 catalog size signal 的 Demo / Player 路径；预算只使用 `ResourceCatalogEntry.Size`，可选框架保留 metadata `providerData["retainPriority"]` 决定驱逐优先级，整数越大越晚驱逐。

Budgeted policy 不启动后台卸载线程，也不访问平台内存 API。引用计数归零后，record 会先进入 retained 状态，再按低 priority、较早 retained、`ResourceKey` 字符串顺序驱逐，直到 `ResourceDebugSnapshot.RetainedBytes <= RetainBudgetBytes` 或只剩 pinned `KeepAlive` 记录。`KeepAlive` 记录不会被预算策略自动驱逐；手动调用 `EvictRetainedResources()` 表示显式清理。

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
        explicitKeys: new[] { new ResourceKey("demo.text.title", ResourceTypeIds.String) },
        maxConcurrentLoads: 2));

while (!preloadOp.IsDone)
{
    float progress = preloadOp.Progress;
    // Update loading UI from the main loop.
}

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

`ResourcePreloadPlan.MaxConcurrentLoads` 控制同时启动的 load 数量，完成一个再补一个；默认 collect-all 模式会保留已加载 handle 并收集所有错误，`failFast: true` 会在首个失败后取消仍在途的 load。调用 `preloadOp.Cancel()` 或取消传入的 `CancellationToken` 会释放已经成功加载的 handles，并返回 `ResourceErrorCode.Cancelled`。Unity Provider 的 completion / progress 应在 Unity 主线程驱动，不能从 worker thread 访问 `UnityEngine.Object`、`AssetBundle` 或 UI Toolkit 对象。

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

框架样例 Catalog 由 Editor 生成器维护，菜单路径：

```text
MxFramework/Samples/Generate Resource Catalog
```

生成器会输出 `Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json`，并校验 `memory` provider 的 `providerData.assetPath` 是否指向正式样例资源。该 catalog 仍服务 Editor Play Mode / Demo 组合根；Player 资源路径应由后续 AssetBundle / Streaming catalog 接管。

Player smoke 资源路径由独立生成器维护，菜单路径：

```text
MxFramework/Samples/Build Player Resource Catalog
```

生成器会构建 `Assets/StreamingAssets/MxFramework/Samples/Bundles/mxframework.samples.start_screen.assetbundle`，并输出 `Assets/StreamingAssets/MxFramework/Samples/mxframework_samples_player_catalog.json`。该 catalog 使用 `assetBundle` provider，可通过 `StreamingResourceCatalogLoader.LoadFromStreamingAssets("MxFramework/Samples/mxframework_samples_player_catalog.json")` 挂载到 `ResourceManager`，bundle root 为 `Path.Combine(Application.streamingAssetsPath, "MxFramework/Samples/Bundles")`。

当前 smoke 只验证本地文件系统 / 桌面 Player 兼容路径。Android 和 WebGL 的 StreamingAssets 读取不能依赖同步 `System.IO.File` 或 `AssetBundle.LoadFromFile`，需要后续 provider / loader 使用 `UnityWebRequest` 或平台专用读取策略。

`RuntimeVerticalSliceRunner` 默认会在启动资源 warmup 时执行两条路径：

- Editor Play Mode / Demo serialized reference path：`mxframework.samples` memory catalog + `MemoryResourceProvider`。
- Player smoke path：`StreamingResourceCatalogLoader` + `AssetBundleProvider` + `ResourcePreloadService`，使用上方 Player catalog 和 bundle root。

运行结果会写入 `ResourceWarmupSummary` 和 `ResourceBindingLogLines`，其中包含 warmup、direct load、release 后 `ResourceDebugSnapshot` loaded/ref-count，以及 Player bundle 计数。Runtime Preview 当前不隐式创建或持有 ResourceManager；如 Preview 需要资源，应由外层组合根显式注入 catalog、provider 和 UI Toolkit 资产。

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
- Runtime Showcase 的资源测试直接使用 `mxframework.samples`：`RuntimeVerticalSliceRunner` 会在 Play Mode 预热 package / StartScreen / Combat / StatusEffects / MagicEffects labels，按 `ResourceKeyConfigProfile.CreateSample()` 直接加载 Katana、StatusAura Prefab、StartScreen 贴图和 MagicEffects AudioClip，并输出加载 / 引用计数 / 释放后的诊断结果。
- `GameObject` 实例由调用方销毁，资源系统只管理 prefab asset handle。

## 14. Animation 表现层

Animation 模块把业务侧 presentation 意图转换为动画播放请求。noEngine 层只保存 `ResourceKey`、layer id、request DTO 和 diagnostics；Unity 播放由 `MxFramework.Animation.Unity` 的 Playables backend 负责。

Locomotion clip 可以在 `animation_authoring.json` 的 clip `calibration` 中声明 `nativeVelocityX` / `nativeVelocityY`、`playbackSpeed`、`cycleDurationSeconds` 以及左右脚 `footContactWindows`。Authoring Compiler 会把这些数据写入 `animation_set_definition.json`，运行时通过 `MxAnimationSetDefinition.LocomotionClipCalibrations` 或 `TryFindLocomotionClipCalibration` 读取；缺失 locomotion 校准数据会产生 `LOCO_CAL_CLIP_METADATA_MISSING` warning，并且校准数据参与 `DefinitionHash`。

```csharp
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Resources;
using UnityEngine;

var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
var walk = new ResourceKey("demo.animation.walk", ResourceTypeIds.AnimationClip);
var run = new ResourceKey("demo.animation.run", ResourceTypeIds.AnimationClip);
var left = new ResourceKey("demo.animation.left", ResourceTypeIds.AnimationClip);
var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
var forward = new ResourceKey("demo.animation.forward", ResourceTypeIds.AnimationClip);
var backward = new ResourceKey("demo.animation.backward", ResourceTypeIds.AnimationClip);
var fallback = new ResourceKey("demo.animation.fallback", ResourceTypeIds.AnimationClip);
var upperBodyMask = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);
var set = new MxAnimationSetDefinition(
    "demo.actor",
    version: 1,
    defaultClip: idle,
    fallbackClip: fallback,
    layers: new[]
    {
        new MxAnimationLayerDefinition(MxAnimationLayerId.Base),
        new MxAnimationLayerDefinition(
            new MxAnimationLayerId("upper_body"),
            profileId: "combat.upper_body",
            defaultWeight: 0f,
            blendMode: MxAnimationLayerBlendMode.Override,
            avatarMaskKey: upperBodyMask)
    },
    warmup: new MxAnimationWarmupDefinition(
        groupId: "combat.demo.actor",
        labels: new[] { "warmup.combat" }),
    blend1DDefinitions: new[]
    {
        new MxAnimationBlend1DDefinition(
            blendId: "locomotion",
            parameterId: "locomotion.speed",
            layerId: MxAnimationLayerId.Base,
            points: new[]
            {
                new MxAnimationBlend1DPoint(0, idle),
                new MxAnimationBlend1DPoint(500, walk),
                new MxAnimationBlend1DPoint(1000, run)
            },
            parameterScale: 1000)
    },
    blend2DDefinitions: new[]
    {
        new MxAnimationBlend2DDefinition(
            blendId: "locomotion2d",
            parameterXId: "locomotion.x",
            parameterYId: "locomotion.y",
            layerId: MxAnimationLayerId.Base,
            points: new[]
            {
                new MxAnimationBlend2DPoint(-1000, 0, left),
                new MxAnimationBlend2DPoint(1000, 0, right),
                new MxAnimationBlend2DPoint(0, 1000, forward),
                new MxAnimationBlend2DPoint(0, -1000, backward)
            },
            parameterXScale: 1000,
            parameterYScale: 1000)
    },
    compatibilityExpectation: new MxAnimationCompatibilityExpectation(
        skeletonProfileId: "humanoid",
        skeletonProfileHash: "sha256:skeleton-profile",
        requiredBonePaths: new[] { "Hips/Spine" },
        requiredSocketPaths: new[] { "Hips/Spine/WeaponSocket" },
        clipExpectations: new[]
        {
            new MxAnimationClipCompatibilityExpectation(idle, new[] { "Hips/Spine" })
        },
        avatarMaskExpectations: new[]
        {
            new MxAnimationAvatarMaskCompatibilityExpectation(upperBodyMask, new[] { "Hips/Spine" })
        }));

// resources 由组合根注册 provider + catalog；clip / AvatarMask 必须通过 ResourceKey 解析。
var backend = new UnityPlayablesAnimationBackend(animator, resources, set, "actor.demo");
backend.Play(new MxAnimationPlayRequest { ClipKey = idle });
backend.SetBlend1D(new MxAnimationBlend1DRequest
{
    BlendId = "locomotion",
    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 750)
});
backend.SetBlend2D(new MxAnimationBlend2DRequest
{
    BlendId = "locomotion2d",
    ParameterX = new MxAnimationQuantizedParameter("locomotion.x", 250),
    ParameterY = new MxAnimationQuantizedParameter("locomotion.y", 500)
});
backend.SetLayerWeight(new MxAnimationLayerWeightRequest
{
    LayerId = new MxAnimationLayerId("upper_body"),
    Weight = 1f,
    FadeDurationSeconds = 0.12f
});
backend.Tick(Time.deltaTime);

MxAnimationDiagnosticSnapshot snapshot = backend.CreateSnapshot();
backend.Release();
```

从 catalog 构建 clip registry，并用 mapping provider 交给组合根：

```csharp
ResourceCatalog catalog = LoadCatalog();
MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(
    catalog,
    version: 1,
    catalogHash: "catalog-hash");

var provider = new MxAnimationStaticMappingProvider(new[] { set });
if (provider.TryFindDefinition("demo.actor", out MxAnimationSetDefinition mappedSet))
{
    string mappingHash = mappedSet.DefinitionHash;
}
```

事件时间轴和 dispatch sink 使用同一份 noEngine DTO，不直接保存 Unity object：

```csharp
IReadOnlyList<MxAnimationEventTimelineRow> rows =
    MxAnimationEventTimelineBuilder.BuildRows(mappedSet);

var eventSink = new MxAnimationPresentationEventDispatchSink(mySink, maxDedupeEntries: 128);
var dispatch = new MxAnimationPresentationEventDispatch(
    actorId: "entity:7",
    actionKey: "action:1001",
    bindingId: "attack",
    actionInstanceId: 3,
    worldFrame: 120,
    localFrame: 6,
    sourceOrder: 0,
    presentationEvent: rows[0].Event);

eventSink.TryDispatch(dispatch, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic diagnostic);
```

进入战斗、生成角色或 late join 前可以先做 warmup / version validation：

```csharp
var preloadService = new ResourcePreloadService(resources);
var warmupService = new MxAnimationWarmupService(preloadService);
MxAnimationCompatibilityProfile compatibilityProfile = LoadOrExtractCompatibilityProfile();
MxAnimationWarmupResult warmup = warmupService.Warmup(new MxAnimationWarmupRequest(
    mappedSet,
    registry,
    catalog,
    syncState,
    compatibilityProfile: compatibilityProfile));

if (!warmup.Success)
{
    foreach (MxAnimationWarmupIssue issue in warmup.Issues)
        Log(issue.Code, issue.Key, issue.Field, issue.Expected, issue.Actual, issue.Message);

    // 不要把失败当作空播成功；由组合根选择 fallback、重试或阻断进入表现层。
}

// 战斗结束或 actor 销毁时释放 warmup group。
warmupService.Release(warmup);
```

如果项目层资源 provider 会跨帧完成加载，改用 `warmupService.WarmupAsync(request)`，轮询返回的 `IResourceOperation<MxAnimationWarmupResult>.IsDone` 后再读取 `Result`。同步 `Warmup` 只覆盖立即完成路径；遇到 pending preload 会返回 `PreloadOperationPending` issue，并要求调用方切到异步路径。

如果同一份 mapping 要在 sample memory provider、本地 AssetBundle、remote Bundle 或项目层可选 Addressables adapter 间切换，Animation 层只额外声明 package expectation；provider 差异仍留在 `ResourceCatalogEntry`：

```csharp
var packageExpectation = new MxAnimationPackageExpectation(
    "mx.anim.demo",
    version: 2,
    catalogId: "mx.anim.demo.catalog",
    catalogHash: "sha256:catalog",
    acceptedProviderIds: new[] { "memory", "assetBundle", "remoteBundle", "addressables" },
    resources: new[]
    {
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip),
            catalogEntryHash: "sha256:clip"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask),
            catalogEntryHash: "sha256:mask"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.bake.attack", MxAnimationResourceTypeIds.BakeArtifact),
            catalogEntryHash: "sha256:bake"),
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.profile.humanoid", MxAnimationResourceTypeIds.CompatibilityProfile),
            catalogEntryHash: "sha256:profile")
    });

MxAnimationWarmupResult packageWarmup = warmupService.Warmup(new MxAnimationWarmupRequest(
    mappedSet,
    registry,
    catalog,
    syncState,
    null,
    true,
    compatibilityProfile,
    packageExpectation,
    new MxAnimationPackageCatalog(catalog, version: 2, catalogHash: "sha256:catalog")));
```

`MxAnimationPackageCatalogValidator` 会报告 package id/version/catalog hash mismatch、provider 不匹配、entry hash mismatch、missing clip/mask/bake/profile；`RequiredForWarmup=true` 的 package resources 会进入同一个 preload group。Addressables 仍是可选 adapter：项目可把 provider id 写入 `AcceptedProviderIds`，但 MxFramework 默认程序集不引用 Addressables。

Mod animation package 通过 noEngine override DTO 覆盖表现 mapping。合并前必须校验 base hash/version、catalog/package、compatibility；成功后再 warmup：

```csharp
var manifest = new MxAnimationModPackageManifest(
    "mod.anim.demo",
    version: 2,
    displayName: "Demo Animation Mod",
    catalogId: "mod.anim.demo.catalog",
    catalogHash: "sha256:catalog",
    loadOrder: 10);

var modOverride = new MxAnimationModOverrideDefinition(
    targetSetId: mappedSet.SetId,
    manifest: manifest,
    overrideVersion: 1,
    expectedBaseVersion: mappedSet.Version,
    expectedBaseHash: mappedSet.DefinitionHash,
    actionOverrides: new[]
    {
        new MxAnimationActionBindingOverride(new MxAnimationActionBinding(
            "attack",
            "action:attack",
            new ResourceKey("demo.animation.attack.mod", ResourceTypeIds.AnimationClip, packageId: "mod.anim.demo"),
            new MxAnimationLayerId("upper_body")))
    },
    layerOverrides: new[]
    {
        new MxAnimationLayerDefinitionOverride(new MxAnimationLayerDefinition(
            new MxAnimationLayerId("upper_body"),
            avatarMaskKey: new ResourceKey("demo.animation.mask.mod", ResourceTypeIds.AvatarMask, packageId: "mod.anim.demo")))
    },
    packageResources: new[]
    {
        new MxAnimationPackageResourceExpectation(
            new ResourceKey("demo.animation.bake.attack.mod", MxAnimationResourceTypeIds.BakeArtifact, packageId: "mod.anim.demo"),
            catalogEntryHash: "sha256:bake")
    },
    compatibilityExpectation: LoadModCompatibilityExpectation(),
    acceptedProviderIds: new[] { "assetBundle", "remoteBundle" });

MxAnimationModOverrideMergeResult merge = MxAnimationModOverrideMerger.Merge(
    new MxAnimationModOverrideMergeRequest(
        mappedSet,
        modOverride,
        modCatalog,
        compatibilityProfile,
        new MxAnimationPackageCatalog(modCatalog, version: 2, catalogHash: "sha256:catalog"),
        packageExpectation));

if (!merge.Success)
{
    foreach (MxAnimationModOverrideIssue issue in merge.Issues)
        Log(issue.Code, issue.Field, issue.Expected, issue.Actual, issue.Message);

    // 不要强制播放 rejected override；继续使用 base mapping 或阻断加载。
}
```

`merge.MergedDefinition.DefinitionHash` 是合并后稳定 mapping hash；`merge.BaseDefinitionHash` / `merge.OverrideHash` 保留审计所需的 base 与 mod 输入。资源生命周期仍通过 `MxAnimationWarmupService`：把 `merge.MergedDefinition` 和 `merge.MergedPackageExpectation` 传给 warmup，释放时调用 `warmupService.Release(result)`。

Unity Editor 内可以创建 `MxFramework/Animation/Clip Registry` asset，用 Inspector 填写 clip/action/binding 映射，再用 `Validate Mapping Structure` 做无 catalog 的结构校验。Inspector 的 `Event Timeline Preview` 会显示 Seconds / NormalizedTime / CombatFrame / PresentationFrame 事件，并对 CombatFrame / PresentationFrame 输出 deterministic correlation 摘要。正式导出或 CI 校验应调用 exporter 并传入 `ResourceCatalog`，检查 catalog entry、typeId、variant 和 package。该 asset 可以引用 `AnimationClip`，但运行时仍只使用导出的 `ResourceKey` mapping。

只读逐帧检查可以打开 `MxFramework / MxAnimation / Timeline Scrubber Preview MVP`，或在 Clip Registry Inspector 点击 `Open Timeline Scrubber Preview`。窗口选择 action binding 和 frame 后，会显示同帧 presentation event、CombatActionTimeline phase/window/frame event、baked root/socket/weapon trace sample，以及 missing clip、missing bake、hash/source mismatch、event out of range、timeline frame mismatch diagnostics。该工具只做 preview，不编辑 registry、Combat authoring 或 runtime DTO。

Bake MVP 入口用于把选中的 `AnimationClip` 转成 fixed-frame、量化的派生参考数据：

```text
MxFramework / MxAnimation / Bake Selected Animation Clip MVP
```

生成的 `.mxbake.txt` artifact 报告包含 source clip hash、bake profile hash、skeleton/avatar profile hash、artifact hash、root motion reference、weapon trace reference 和 event markers。它只用于 authoring / preview / Combat 参考数据输入；运行时 Combat 不读取 Animator、PlayableGraph 或 Unity bone pose 当前状态。

Skeleton / Avatar / Clip compatibility 可以由 Editor 提取器生成 noEngine profile，再交给 warmup、CI 或 bake 校验：

```csharp
MxAnimationSkeletonCompatibilityProfile skeletonProfile =
    MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
        characterRoot,
        "humanoid",
        socketPaths: new[] { "Hips/Spine/WeaponSocket" });

MxAnimationCompatibilityProfile compatibilityProfile =
    MxAnimationCompatibilityEditorExtractor.CreateProfile(
        skeletonProfile,
        new[] { MxAnimationCompatibilityEditorExtractor.CreateClipProfile(idleClip, idle, skeletonProfile) },
        new[] { MxAnimationCompatibilityEditorExtractor.CreateAvatarMaskProfile(upperBodyAvatarMask, upperBodyMask, skeletonProfile) });
```

约定：

- `MxFramework.Animation` 不引用 Unity，不保存 `AnimationClip`、GUID 或 `Assets/...` path。
- `MxFramework.Animation.Unity` 通过 `IResourceManager.LoadAsync<AnimationClip>` 获取 backend 自己拥有的 handle。
- AvatarMask 与 clip 一样使用 `ResourceKey` 和 `ResourceManager` 加载；runtime definition 不直接保存 `AvatarMask` 引用。
- layer weight 只影响 Unity Playables layer mixer 的表现权重，可用于上半身攻击、下半身移动这类视觉混合；它不改变 Combat authority。
- 1D locomotion blend 使用 `MxAnimationBlend1DDefinition` 和 `MxAnimationBlend1DRequest`；2D locomotion blend 使用 `MxAnimationBlend2DDefinition` 和 `MxAnimationBlend2DRequest`。point clip key 进入 definition hash、mapping validation 和 warmup，实际 clip 仍由 backend 通过 `ResourceManager` 加载。
- `MxAnimationBlend1DRequest` / `MxAnimationBlend2DRequest` 会转换成共享 `MxAnimationBlendRequest`，backend 再走同一条 clip weight -> Playables slot mixer 路径。
- 2D blend 权重计算在 noEngine 层使用量化整数参数完成，Unity backend 只消费 clip weight 列表；它不能把 Unity 当前动画姿态或 Playable time 写回权威逻辑。
- compatibility profile 只保存 skeleton id/hash、bone/socket path、clip binding path、AvatarMask active path 和 `ResourceKey`；不得把 Unity object、GUID 或 asset path 写入 noEngine runtime contract。
- bake artifact 必须作为派生缓存处理。source/profile/skeleton/artifact hash mismatch 必须输出 diagnostics，不能静默使用过期数据。
- bake artifact 可和 `MxAnimationCompatibilityExpectation` 一起校验 skeleton profile id/hash，避免用错误骨架生成的 trace/root motion reference 混入运行时。
- `MxAnimationSetDefinition.DefinitionHash` 是稳定 mapping hash；加载侧可用它和 catalog hash / registry version 检测过期数据。
- warmup 复用 `ResourcePreloadService` 和 catalog labels；hash/version mismatch、missing clip、wrong type、compatibility mismatch 或 partial failure 都会产生结构化 `MxAnimationWarmupIssue`。
- package expectation 复用同一套 `ResourceKey` mapping，允许 memory / AssetBundle / remote Bundle / 可选 Addressables provider 切换；错误包、错误版本、错误 hash、missing bake/profile 不能静默播放。
- Mod animation override 只改表现 mapping 和 package resource expectation；合并结果必须先通过 catalog/package/compatibility validation，不能修改 Combat authority。
- warmup group release 只释放预热持有的 handles，不会误释放其它 consumer 正在持有的同一 clip。
- Editor clip registry 只是 authoring 入口，不允许作为运行时资源加载捷径。
- presentation event 是表现层事件，不驱动 Combat 命中、取消、伤害、无敌、移动或 replay hash。默认 late join 不补播一次性 VFX/SFX；需要补播时必须把事件标记为 `CatchUpSafe` 并由项目网络层显式执行策略。
- runtime presentation event sink 使用 actor/action instance/frame/event/source-order dedupe key；重复 dispatch 会被丢弃并输出 diagnostics。
- default / fallback clip 按 backend 生命周期常驻，并在 diagnostics 中显示 resident 状态。
- crossfade outgoing clip 在权重归零且 playable 从 graph 断开后释放。
- 加载失败会记录 `ResourceError`，先尝试 actor fallback；fallback 也失败时 layer 进入 failed state。
- `Tick(deltaTime)` 由外部传入 presentation delta。这个时间源只用于表现层，不进入 Combat authority、Replay hash 或命中/取消判定。
- `MxFramework.Combat.Animation.Unity` 提供 Combat 到 MxAnimation 的首版 Unity 表现桥；旧 `CombatAnimatorDriver` 仍保留为 opt-in 迁移路径，未被替换。

详细接口见 `Docs/Interfaces/Animation.md`，测试入口为 `Assets/Scripts/MxFramework/Tests/Animation/`。

## 15. Input 输入系统

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

## 15.5 Character Control 角色控制编排

Character Control 是 Input、Runtime AI Planner、Replay/Test source 与 Combat / Gameplay 之间的 noEngine 编排层。组合根负责把设备输入或规划结果转成 `CharacterCommand`；Character Control 只消费固定点命令和稳定 entity id。

```csharp
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Core;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

var entity = CharacterControlEntityRef.FromGameplayAndCombat(
    gameplayEntityId: new GameplayEntityId(10, 1),
    combatEntityId: new CombatEntityId(20),
    combatBodyId: new CombatBodyId(30),
    stableId: 1);

var frame = new RuntimeFrame(12);
var command = new CharacterCommand(
    frame: frame,
    sourceId: 0,
    entity: entity,
    moveDirection: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    facingBasis: CharacterFacingBasis.Identity,
    jumpPressed: false,
    sprintHeld: true,
    actionButtons: CharacterActionButtons.None,
    actionRequest: default);

var control = new CharacterControlStateMachine(entity);
CharacterControlTransitionResult transition = control.BeginAction(
    frame,
    CharacterControlTransitionReason.ActionStarted);
```

运动桥接必须走 Combat Motion：

```csharp
var resolver = new CharacterMotionResolver(
    combatKinematicMotor,
    CharacterMotionSettings.Default,
    motionModifierProviders);

CharacterMotionResult motion = resolver.Step(
    command: command,
    controlState: control,
    motionState: currentMotionState,
    physicsWorld: combatPhysicsWorld);
```

动作桥接只启动 / 取消 Combat action 或 enqueue Gameplay command，不直接写 Gameplay component store：

```csharp
var actionController = new CharacterActionController(
    stateMachine: control,
    combatActionRunner: combatActionRunner,
    gameplayCommandBuffer: runtimeCommandBuffer,
    abilityRequestStore: gameplayAbilityRequestStore);

CharacterActionResult action = actionController.Submit(
    CharacterActionRequest.CombatAction(
        frame,
        entity,
        CharacterActionKind.Attack,
        combatActionId: 1001,
        sourceId: 0,
        queueIfBusy: true));
```

Gameplay pressure typed events 通过 reaction bridge 接入控制状态，不直接写 Gameplay source of truth：

```csharp
var pressureReaction = new CharacterPressureReactionController(
    control,
    actionController,
    new CharacterPressureReactionPolicy
    {
        GuardBreakReactionFrames = 8,
        GuardBreakLockMask = CharacterControlLockMask.Action
    });

CharacterPressureReactionResult pressureResult = pressureReaction.Apply(
    new PostureBreakEvent(
        frame,
        entity.GameplayEntityId,
        PressureBand.Critical,
        previousValue: 80,
        currentPressure: 100,
        maxPressure: 100,
        delta: 20,
        traceId: "hit-001"));

if (pressureResult.ReactionStarted)
{
    pressureReaction.TryFinishExpiredReaction(
        pressureResult.ReactionEndFrame,
        out CharacterPressureReactionResult finished);
}
```

如果启用 `BrokenBandChangeStartsReaction`，band-change 只有在 pressure 上升且 `NewBand > PreviousBand` 时才会启动 reaction；recovery、negative delta 和 band 回落不会刷新已有 reaction window。组合根停用 pressure owner 时可调用 `FinishActiveReaction(...)` 主动释放控制锁。

MxAnimation 表现适配放在可选程序集 `MxFramework.CharacterControl.Animation`。它只消费 Character Control 结果 / 事件，把移动、反应转成 animation backend 请求；Combat action lifecycle 仍由 `CombatMxAnimationUnityBridge` 负责：

```csharp
using MxFramework.CharacterControl.Animation;
using MxFramework.Resources;

var animationPresentation = new CharacterAnimationPresentationController(
    mxAnimationBackend,
    new CharacterAnimationPresentationOptions
    {
        TargetActorId = "character:1",
        LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D,
        LocomotionBlend2DId = "locomotion2d",
        BlendLocomotionWhenAirborne = false,
        ReactionBindings =
        {
            new CharacterAnimationReactionBinding
            {
                Reason = CharacterControlTransitionReason.GuardBreak,
                BindingId = "reaction.guard_break",
                ClipKey = new ResourceKey("demo.animation.guard_break", ResourceTypeIds.AnimationClip),
                RequestKind = CharacterAnimationReactionRequestKind.CrossFade,
                FadeDurationSeconds = 0.12f
            }
        }
    });

CharacterAnimationPresentationResult locomotionAnimation =
    animationPresentation.ApplyLocomotion(motion);

CharacterAnimationPresentationResult reactionAnimation =
    animationPresentation.ApplyStateChanged(stateChangedEvent);

CharacterAnimationPresentationDiagnosticSnapshot animationDiagnostics =
    animationPresentation.CreateSnapshot();
```

Character Control 可直接注册到 Phase 13 Debug UI registry。Source 会订阅状态、动作和 pressure reaction 的公开事件；command / motion 由组合根在采样和 motion step 后显式记录：

```csharp
using MxFramework.DebugUI;
using MxFramework.DebugUI.Adapters;

var characterDebugSource = new CharacterControlDebugSource(
    control,
    actionController: actionController,
    pressureReactionController: pressureReaction);

characterDebugSource.RecordCommand(command);
characterDebugSource.RecordMotionResult(motion);

var debugRegistry = new FrameworkDebugSourceRegistry();
debugRegistry.Register(characterDebugSource);

// 组合根销毁、场景卸载或角色换绑时：
characterDebugSource.Dispose();
```

本地输入适配放在可选程序集 `MxFramework.CharacterControl.Input`：

```csharp
using MxFramework.CharacterControl.Input;
using MxInput = MxFramework.Input;

var inputSource = new InputCharacterCommandSource(inputProvider, new InputCharacterCommandSourceOptions
{
    SourceId = 0,
    UseLookAsFacing = true,
    ActionBindings = new[]
    {
        CharacterInputActionBinding.CombatAction(
            MxInput.InputIntent.AttackPrimary,
            CharacterActionKind.Attack,
            combatActionId: 1001,
            queueIfBusy: true),
        CharacterInputActionBinding.GameplayAbility(
            MxInput.InputIntent.AttackSecondary,
            gameplayAbilityId: 300001)
    }
});

if (inputSource.TryGetCommand(frame, entity, out CharacterCommand inputCommand))
{
    control.RecordCommandFrame(inputCommand.Frame);
}
```

Runtime AI Planner 适配放在 `MxFramework.CharacterControl.RuntimeAiPlannerBridge`，由项目组合根显式配置 planner action 到角色命令的 profile：

```csharp
using MxFramework.CharacterControl.RuntimeAiPlannerBridge;

var profiles = new RuntimeAiCharacterCommandProfileRegistry();
profiles.Register(new RuntimeAiCharacterCommandProfile(
    actionId: 10,
    moveDirection: FixVector3.Zero,
    facingBasis: CharacterFacingBasis.Identity,
    actionKind: CharacterActionKind.Attack,
    combatActionId: 1001,
    moveSpeedScale: Fix64.Zero,
    traceTag: "stand-cast"));

var plannerSource = new RuntimeAiPlannerCharacterCommandSource(
    aiWorldState,
    planner,
    goals,
    actions,
    profiles,
    new RuntimeAiPlannerCharacterCommandSourceOptions
    {
        SourceId = 10,
        RequireTargetFacts = true,
        ReactionDelayFrames = 2,
        MinDecisionIntervalFrames = 3,
        CommandSmoothingFrames = 2
    });
```

`combatActionId` / `gameplayAbilityId` 这类 action request 只在 action selection edge 或 reaction-delay 生效帧输出一次；后续最小决策间隔或 smoothing 复用同一 profile 时只输出 movement command。`moveSpeedScale` 不传时默认 `1`，显式传 `Fix64.Zero` 可以表达站定施法、原地防御或停步等待。

移动 modifier / traction 通过 provider 注入，第一版只影响 `CombatMotionInput.MoveSpeedScale`：

```csharp
public sealed class SlowMotionProvider : ICharacterMotionModifierProvider
{
    public void CollectModifiers(
        CharacterMotionModifierContext context,
        IList<CharacterMotionModifier> destination)
    {
        destination.Add(new CharacterMotionModifier(
            source: "buff.slow",
            moveSpeedScale: Fix64.Half,
            reason: "slow",
            priority: 10));
    }
}
```

约定：

- Input、Runtime AI Planner、UI Toolkit、Replay 和测试只实现 `ICharacterCommandSource` 或提交 `CharacterActionRequest`。
- `CharacterMotionResolver` 通过 `CombatKinematicMotor` 得到权威移动；Unity `CharacterController`、`Rigidbody`、`UnityEngine.Physics` 和表现层 root motion 不能作为权威。
- `CharacterActionController` 通过 `CombatActionRunner` 和 `GameplayRuntimeCommandFactory` 桥接动作，不改 Combat timeline、hit window、damage 或 Gameplay HP/Buff/Ability 状态。
- `CharacterPressureReactionController` 只消费 Gameplay pressure typed events，先校验 `GameplayEntityId` 映射，再按策略进入 `Reaction`、输出事件或 rejected result。
- posture / guard break 默认会清理 queued action request 并取消当前 Combat action；armor break 默认只记录反馈，不改变控制状态。
- band-change reaction 只响应 pressure 上升导致的 band 升级；生命周期提前结束时用 `FinishActiveReaction(...)` 释放 active reaction。
- cooldown、资源、状态、目标合法性等项目规则通过 `ICharacterActionConstraint` 注入。
- slow、traction、fatigue 等移动影响通过 `ICharacterMotionModifierProvider` 输出 scale，不直接写 Gameplay / Combat 状态。
- Input adapter 在 Gameplay context 关闭时会 drain 并丢弃当前 frame 及以前的 queued commands；Runtime AI Planner profile 的 `ActionRequest` 只在首次选择或 reaction delay 生效帧发出一次。
- MxAnimation presentation adapter 只输出 `MxAnimationBlend1DRequest` / `MxAnimationBlend2DRequest` / `Play` / `CrossFade` 请求和 diagnostics；1D speed 使用水平输入幅度乘移动倍率，默认 airborne 时 locomotion 参数归零，缺失 reaction binding、fallback binding 或 backend reject 会记录 diagnostics 但不影响 Character Control authority。
- Combat action started / finished / canceled 的动画表现仍由 `CombatMxAnimationUnityBridge` 负责；accepted、queued、rejected 和 gameplay-only action events 在 Character Control animation adapter 中只记录 skipped diagnostics。
- Debug UI source 只读公开事件和组合根传入的 last command / motion result；不暴露可写命令，不进入 SaveState、Replay 或 Runtime hash。`FrameworkDebugSourceRegistry` 不拥有 source 生命周期，组合根必须释放 event-backed source。

详细接口见 `Docs/Interfaces/CharacterControl.md`，测试入口为 `Assets/Scripts/MxFramework/Tests/CharacterControl/`。

## 16. Audio 音频系统

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

## 17. Combat 战斗模块

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

Baked weapon trace 参考数据转 Combat query 的最小示例：

```csharp
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

var reference = new CombatBakedWeaponTraceReferenceFrame(
    traceId: 7,
    localFrame: 3,
    socketPrev: FixVector3.Zero,
    socketNow: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
    tipDirectionPrev: new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
    tipDirectionNow: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));

var profile = new CombatBakedWeaponRuntimeProfile(
    characterScale: Fix64.One,
    weaponLength: Fix64.FromInt(3),
    weaponRadius: Fix64.Half,
    socketOffset: FixVector3.Zero,
    targetMask: CombatPhysicsLayerMask.FromLayer(1));

CombatCapsuleQuery bakedQuery = CombatBakedWeaponTraceAdapter.BuildCurrentBladeCapsule(
    reference,
    profile,
    new CombatEntityId(1),
    actionId: 1001,
    queryId: 42,
    sourceOrder: 0);
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
- 动画驱动命中应走 bake reference + runtime profile -> `WeaponTraceFrame` / `CombatCapsuleQuery`，不能在运行时读 Unity Animator、PlayableGraph 或 bone pose 当前状态。

## 18. App / Scene Flow

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

## 19. Diagnostics 诊断

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

## 20. Gameplay Component Runtime

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

### 20.1 Posture Pressure

姿态压力是 component-native 状态：系统只读写 `GameplayComponentWorld`，Combat / UI / Runtime AI Planner 接入由后续 bridge 负责。

```csharp
var world = new GameplayComponentWorld();
GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);

GameplayEntityId entity = world.CreateEntity();
world.GetOrCreateStore<GameplayPosturePressureComponent>().Set(
    entity,
    new GameplayPosturePressureComponent(
        maxPressure: 100,
        recoveryRate: 5,
        recoveryDelayFrames: 6));

var pressure = new GameplayPosturePressureSystem();
pressure.BandChangedEvents.Subscribe(evt =>
{
    PressureBand band = evt.NewBand;
});
pressure.PostureBreakEvents.Subscribe(evt =>
{
    GameplayEntityId brokenEntity = evt.EntityId;
});

pressure.Enqueue(new GameplayPosturePressureRequest(entity, delta: 35, traceId: "hit-001"));
pressure.Tick(new GameplaySystemContext(
    new RuntimeFrame(10),
    0d,
    0d,
    new GameplayWorld(),
    new RuntimeCommand[0],
    world.Events,
    componentWorld: world));
```

约定：

- `GameplayPosturePressureSystem` 默认运行在 Resolution phase、priority 70；它先消费 request，再执行自然恢复。
- 状态段事件和破韧事件是 typed event bus，不会写入 `GameplayRuntimeEvent`。
- Schema / Hash / SaveState 由 `GameplayCoreComponentSchemaDescriptors` 统一注册。

## 21. 推荐组合根

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

## 22. Development Agent：AI Agent 如何找文档

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

## 23. 提交前检查

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
