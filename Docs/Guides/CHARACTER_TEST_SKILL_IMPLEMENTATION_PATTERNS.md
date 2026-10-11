# CharacterTest Skill Implementation Patterns

> Status: Guide
>
> Date: 2026-06-25
>
> Audience: Development Agents, designers, and programmers working on CharacterTest combat skills.

本文是 CharacterTest 技能实现类型的 Agent 操作手册。目标不是逐技能百科，而是让 Agent 在改技能前快速判断实现类型、可编辑数据面、runtime authority、表现接入位置和最小验证入口。

公共 API 事实仍以 `Docs/Interfaces/*.md`、源码、generated catalog 和测试为准。本文只记录 CharacterTest 当前怎样把这些模块拼成技能。

## Agent Quick Route

| 任务意图 | 先看模式 | 默认 authority | 不要直接做 |
| --- | --- | --- | --- |
| 新增或修改会飞行、会碰撞、会过期的实体 | Projectile Runtime Pattern | `Combat.Projectiles` | 不用 Unity Rigidbody / prefab 碰撞做权威 |
| 调整 projectile 轨迹 | Projectile Motion Variants | projectile motion sampler | 不混用 `ArcToTarget` 和 `BallisticArcToTarget` 语义 |
| 做激光、光束、同帧长距离命中 | One-Frame Sweep / Beam Pattern | projectile sweep + HitResolve | 不用 VFX 长度决定命中 |
| 一个技能发多次 projectile 或延迟落点 | Multi-Shot / Delayed Projectile Pattern | CharacterAction timeline | 不把多发循环塞进单个 projectile definition |
| 飞行体落地后生成持续区域 | Projectile To Mechanic Source Pattern | projectile event + CharacterTest source runtime | 不让 projectile 自己持续刷 Buff |
| 抛物线投掷后命中/落地，二段下落后部署并延迟爆炸的地雷 | Projectile Carrier -> Drop Projectile -> Fuse Mine Pattern | `Combat.Projectiles` carrier/drop + CharacterTest trap runtime | 不让 VFX、Unity Trigger 或 prefab collider 决定落点、触发、引信或伤害 |
| 抛物线投放后创建持续炮台，炮台自动索敌并发射标准 projectile | Deployable Persistent Turret / Auto Projectile Source Pattern | `Combat.Projectiles` deploy carrier + CharacterTest turret runtime + shot projectile | 不让炮台 prefab、Unity Collider 或 VFX 决定索敌、开火、命中或伤害 |
| projectile 命中后拉动或位移单个目标 | Projectile Hit -> Target Displacement / Tether Pull Pattern | `Combat.Projectiles` + CharacterTest motion request | 不用 `physicalEventsEnabled` 表达单目标 tether |
| 飞行中吸引、推物体、爆炸冲量 | Active Field And Physical Interaction Pattern | projectile physical event + CharacterTest consumer | 不把吸附/推力解释逻辑写进 projectile definition |
| 突进、冲锋、技能驱动位移 | Mobility / Dash / Modified Caster Body Hit Window Pattern | CharacterAction rootMotion + combatTrack HitTrace | 不创建 projectile、Rigidbody 或新 gameplay request 只为了移动角色 |
| 远程同帧结算、没有飞行体 | Instant Non-Projectile Ranged Pattern | CharacterTest target resolver | 不创建空壳 projectile 只为播放表现 |
| 光环、护盾、引导、区域控制 | Aura, Shield, Zone And Channel Patterns | Buff/Aura/Shield/source runtime | 不绕过对应 runtime 直接改状态 |

## Skill To Type Index

这个表只用于按技能名反查实现类型。不要在这里写逐技能百科。

| Skill | Ability | Action | 主实现类型 | 关键 producer |
| --- | --- | --- | --- | --- |
| Fireball | `910001` | `fireball_impact` | projectile direct | `Combat.Projectiles` |
| Fireball Burst | `910002` | `fireball_burst` | projectile area pulse | `Combat.Projectiles` |
| Flame Lance | `910003` | `flame_lance` | piercing projectile | `Combat.Projectiles` |
| Fireball Lob | `910004` | `fireball_lob` | designed arc projectile | `ArcToTarget` |
| Fireball Surge | `910005` | `fireball_surge` | speed-profile projectile | `EaseInQuadratic` |
| Fireball Return | `910006` | `fireball_return` | returning projectile | `BoomerangReturn` |
| Fireball Sway | `910007` | `fireball_sway` | sway projectile | `SwayForward` |
| Guard Aura | `910008` | `guard_aura` | aura Buff | `CombatGameplayAuraBuffSystem` |
| Fireball Homing | `910009` | `fireball_homing` | tracking projectile | `HomingToTarget` |
| Fireball Bezier | `910010` | `fireball_bezier` | curve projectile | `BezierToTarget` |
| Fireball Explosion | `910011` | `fireball_explosion` | projectile area + delayed pulse | projectile event + effect timeline |
| Tornado | `910012` | `tornado` | active field projectile | projectile physical event -> interaction adapter |
| Area Confinement | `910013` | `area_confinement` | channel + zone source | Area Confinement runtime |
| Beam Lance | `910014` | `beam_lance` | one-frame piercing beam | projectile sweep |
| Channel Beam | `910015` | `channel_beam` | repeated one-frame beam | timeline tick requests |
| Laser Lance | `910016` | `laser_lance` | one-frame single beam | projectile sweep |
| Chain Lightning | `910017` | `chain_lightning` | instant chain resolver | CharacterTest resolver |
| Poison Bottle | `910018` | `poison_bottle` | ballistic projectile -> pool source | projectile event -> source runtime |
| Physical Shield | `910019` | `physical_shield` | shield runtime | `CombatShieldRuntime` |
| Stun Bolt | `910020` | `stun_bolt` | control projectile | projectile on-hit Buff |
| Meteor Barrage | `910021` | `meteor_barrage` | multi-shot area projectile | timeline requests + area pulse |
| Sword Wave | `910022` | `sword_wave` | wide projectile + interaction | custom sweep + projectile physical event |
| Dash Strike | `910023` | `dash_strike` | mobility + modified caster body hit window | CharacterAction rootMotion + combatTrack HitTrace |
| Hook Chain | `910024` | `hook_chain` | projectile hit -> tether pull | projectile impact + CharacterTest motion request |
| Arc Mine | `910025` | `arc_mine` | projectile carrier -> drop projectile -> fuse mine | `Combat.Projectiles` carrier/drop + CharacterTest trap runtime |
| Rune Turret | `910026` | `rune_turret` | deployable persistent turret -> auto projectile source | `Combat.Projectiles` deploy carrier + CharacterTest turret runtime |

## Edit Surface / Source Of Truth

| 数据面 | 首选编辑入口 | Generated / mirror | 禁止事项 |
| --- | --- | --- | --- |
| Projectile definition / spawn profile / presentation binding | Character Action Authoring 的 `投射物` 页签；时间轴内联 inspector 只作为上下文入口 | `Assets/MxFrameworkGenerated/Combat/Projectiles/*.json` and StreamingAssets runtime catalog mirrors | 不只改 StreamingAssets mirror；不只改 generated JSON 后忘记 source |
| Ability id / action binding / request id | `CharacterTestAbilityCatalogSource` and `CharacterTestCharacterActionConfigSource` | generated ability/action artifacts | 不改表现层来改变释放规则 |
| CharacterAction timeline events | CharacterAction authoring library / editor | embedded or generated action JSON | 不在 runtime producer 里硬编码替代 timeline |
| Buff / Aura / status effect | Buff/Aura authoring and generated Buff/Aura catalogs | runtime Buff/Aura catalogs | 不直接改 Buff store；通过 request/buffer 进入系统 |
| VFX / presentation | VFX ids, resource catalog, projectile presentation bindings | presentation bindings and resource artifacts | 不让 VFX、prefab、audio 决定命中、伤害或范围 |
| CharacterTest special producer / interaction adapter | CharacterTest runtime systems and tuning classes | may consume generic projectile physical event facts | 不把特例 producer 误写成 framework 通用能力 |
| CharacterTest mechanic tuning | `ability_authoring.json` mechanic tunings / generated `character_test_mechanic_tunings.json` | generated JSON and StreamingAssets mirror; currently JSON-only Player catalog | 不只改 runtime constants；不把 trap/source 半径、延迟、持续时间藏进代码 |
| Runtime catalog player artifact | generated catalog refresh pipeline | StreamingAssets mirror | 不在保存动作库时隐式刷新 Player artifact，除非任务明确要求 |

Generated files and StreamingAssets mirrors are artifacts. If the two copies disagree, treat it as stale artifact/mirror drift and refresh through the intended pipeline instead of picking one as the only truth.

Agent note: projectile authoring 的主入口是 Character Action Authoring 窗口里的 `投射物` 页签。那里集中显示 source/generated/pending draft 状态、常用数值和直接引用；时间轴里的 projectile inspector 只用于编辑当前事件上下文。第一版引用计数只覆盖 action gameplay request 与 ability effect timeline 中明确的 projectile request，不代表 VFX、prefab、resource 或文档依赖全集。

Agent note: projectile physical interaction has one generic edit switch, `definition.physicalEventsEnabled`. It does not add radius, force profile, falloff, mass scale, material response, or Unity Rigidbody authority to projectile data. When enabled, `Combat.Projectiles` emits `CombatProjectilePhysicalEvent` facts from existing projectile data: `collision.radius` for active/direct events, `impact.areaRadius` for area pulse/expiry events, and `impactPower` from `impact.hitEvent.damage` or `impact.areaHitEvent.damage`. CharacterTest then maps only known projectile stable ids and trigger kinds to interaction behavior; unmapped projectile facts are ignored.

## Common Cast Chain

大多数 CharacterTest 技能先经过同一条公共链路：

```text
Input / hotbar
  -> AbilityRuntime cooldown and action binding
  -> CharacterAction timeline
  -> track event
  -> gameplay producer
  -> Combat / Gameplay / VFX runtime
```

常用 track event：

| Event | 用法 |
| --- | --- |
| `SendGameplayRequest` | 发 projectile、启动 aura / shield / channel / special runtime。 |
| `StartHitTrace` | 武器或近战 trace，走 Combat hit config 和 HitResolve。 |
| `ApplyGameplayEffect` | 非武器范围效果，按配置查询目标并结算。 |
| `SpawnVisualCue` | 施法前摇、持续光环、蓄力、提示等表现，不直接改规则状态。 |

关键边界：

- AbilityRuntime 负责冷却、输入意图到 action 的绑定，不负责命中和表现。
- CharacterAction 负责时间线事件，不直接做 projectile 运动、Buff 应用或物理交互。
- Combat / Gameplay runtime 是规则权威；Unity VFX / prefab 只读快照或事件。
- CharacterTest 特例 producer 可以存在，但要明确它不是框架通用配置字段。

## Projectile Runtime Pattern

Use when: 技能需要飞行生命周期、位置采样、穿透/阻挡、命中历史、过期事件、SaveState/hash/network snapshot。

Do not use when: 技能是瞬间链式解析、纯 aura / zone / shield，或只是表现上像光束但规则同帧完成。

Authority: `Combat.Projectiles` 负责飞行、采样、命中、过期和 snapshot；`HitResolveSystem` 负责命中接受/拒绝；Unity presenter 只读 active view。

Data to edit: projectile definition、spawn profile、presentation binding、CharacterAction request id。不要只改 generated mirror。

Runtime path:

```text
SendGameplayRequest(requestId)
  -> spawn profile stableId
  -> projectile definition stableId
  -> CombatProjectileSpawnRequestBuffer
  -> CombatProjectileRuntime.Tick
  -> CombatPhysicsWorld QueryCapsule / QuerySphere
  -> HitResolveSystem
  -> CombatProjectileFrameEvents
  -> CharacterTest damage / Buff / interaction / VFX bridges
```

Validation: projectile runtime/importer tests；涉及 CharacterAction authoring 时加 CharacterAction editor/timeline focused tests；涉及 CharacterTest 技能行为时加 focused integration。

Common pitfalls: 不绕过 `HitResolveSystem` 直接改 HP；不让 `CombatProjectileUnityPresenter` 决定命中；不在 hot path 引入 LINQ、临时集合、字符串查找或 Unity 对象访问。

## Projectile Motion Variants

Use when: 已确认技能属于 projectile runtime，只需要改变轨迹采样或目标解析方式。

Do not use when: 需要新增 Buff、source、interaction producer 或非 projectile 同帧 resolver；这些是更外层的实现类型。

Authority: 同一个 `Combat.Projectiles` runtime；motion kind 只是采样策略，不是不同 runtime。

Data to edit: `motion.kind` 和对应 motion 参数。目标点、tracking target、effective lifetime、launch speed 等属于本次发射状态。

Runtime path: spawn profile 解析目标/方向后创建 active projectile，motion sampler 在 tick 中用固定点数学采样位置，再由 sweep/query 和 HitResolve 结算。

Validation: motion sampler runtime tests；有 editor preview 时加 CharacterAction projectile editor tests。

Common pitfalls: `ArcToTarget` 是 `arcHeight` 设计弧线，不读取 gravity；`BallisticArcToTarget` 是 gameplay gravity 弹道，不使用 `aim.maxRange`；不要把 `speedPerFrame * lifetimeFrames` 当 ballistic 射程。

| Motion | 实现语义 | 适用意图 |
| --- | --- | --- |
| `Linear` | 沿发射方向推进，速度曲线可改变生命周期内进度分配。 | 普通弹、穿透弹、beam sweep。 |
| `ArcToTarget` | 使用 `arcHeight` 的设计曲线，不读取 gravity。 | 策划手调视觉弧线。 |
| `BoomerangReturn` | 先到目标点，再沿同一路径返回 origin。 | 折返飞行。 |
| `SwayForward` | 主路径叠加确定性侧向摆动。 | 摆动飞行、Tornado 视觉路径。 |
| `HomingToTarget` | 保存 tracking target id，每帧通过 deterministic provider 重新解析目标中心并转向。 | 追踪弹。 |
| `BezierToTarget` | 用局部控制点偏移采样三次贝塞尔曲线。 | 曲线弹道预览 / 表现型路径。 |
| `BallisticArcToTarget` | 最大初速度 + gameplay gravity + 目标点 + 角度限制解本次初速度和方向。 | 物理语义投掷物，例如 Poison Bottle。 |

## One-Frame Sweep / Beam Pattern

Use when: 视觉像 beam / laser，但规则上需要同帧长胶囊 sweep、命中端点和 HitResolve 结果。

Do not use when: beam 需要持续碰撞体权威、可被中途阻挡后继续存在，或需要 projectile 多帧飞行生命周期。

Authority: `Combat.Projectiles` 的 `Linear` projectile，通常 `lifetimeFrames=1`；HitResolve 决定命中接受。

Data to edit: beam 的 projectile definition / spawn profile、CharacterAction tick request、VFX endpoint binding。

Runtime path: action 在指定 frame 发送 request，projectile 本帧 sweep 到 `speedPerFrame` 长度，runtime result 提供 hit point 或 endpoint 给表现。

Validation: one-frame projectile sweep tests；Channel Beam 这种重复 tick 还要验证 CharacterAction 多 request 时序。

Common pitfalls: 不让 beam VFX 自己决定长度；Channel Beam 的持续性来自 timeline 多次 tick request，不是一个持续存在的 projectile。

Examples: Beam Lance 是 piercing sweep；Laser Lance 是 single-target sweep；Channel Beam 是 action active 期间每 6 帧发送一次同类 tick projectile。

## Multi-Shot / Delayed Projectile Pattern

Use when: 一个技能释放多个相互独立 projectile，或按时间线延迟落点/多次请求。

Do not use when: 只是单个 projectile 内的穿透、弹跳、回旋或命中多个目标；这些应留在 projectile runtime/motion 语义里。

Authority: CharacterAction timeline 决定 request 次数和 frame；每个 projectile 仍由 `Combat.Projectiles` 独立管理。

Data to edit: action timeline events、request id、spawn profile；需要不同落点/偏移时使用多个 profile 或 CharacterTest cached context。

Runtime path: 每个 timeline event 独立发送 gameplay request，独立解析 spawn profile、创建 projectile、记录命中和过期。

Validation: CharacterAction timeline tests + projectile runtime focused tests；Meteor/Sword Wave 类技能加 CharacterTest integration。

Common pitfalls: 不把“多发”写成单个 projectile definition 内部循环；表现可以共享 telegraph/charge，但规则仍按 request 分解。

Examples: Meteor Barrage 在多个 frame 发送 `projectile.0..5`；Sword Wave 在不同 frame 发出三道 projectile。

## Projectile To Mechanic Source Pattern

Use when: 飞行体只是投放容器，真正持续效果由地面 source / zone 负责。

Do not use when: 只需要一次性 direct hit 或 area pulse 伤害；那应继续使用普通 projectile impact。

Authority: projectile runtime 负责飞行、命中、过期；CharacterTest source runtime 负责落地后的半径、持续时间、周期刷新 Buff 和 source VFX。

Data to edit: projectile definition / spawn profile、source tuning/definition、Buff definition、presentation binding。

Runtime path: projectile impact 或 expiration event 被 CharacterTest bridge 识别，计算 source center，创建 mechanic source，后续由 source runtime 周期 query 和 enqueue Buff request。

Validation: Poison Bottle / mechanic source focused integration；source create、Buff refresh、expiry 都要覆盖。

Common pitfalls: 不让 projectile 自己持续刷 Buff；direct shatter 不绕过 Buff / damage / VFX 既有链路直接改 HP；落点投影要来自命中点、目标 body 或过期位置的规则结果。

Example: Poison Bottle 是 ballistic projectile，命中 hostile 或过期后碎裂并创建 poison pool。

## Projectile Carrier -> Drop Projectile -> Fuse Mine Pattern

Use when: 技能需要先把一个 carrier 以 projectile 方式抛向目标点或前方落点；carrier 命中 hostile 时只产生碰撞事实并生成二段 drop projectile，carrier/drop 碰到地面、墙或静态障碍后才部署短时存在的地雷；地雷被 hostile 触发后启动引信，延迟结束再做一次范围结算并消失。

Do not use when: 只是瞬时在脚下生成区域，或飞行体本身需要命中 hostile 并立刻造成伤害；前者走纯 mechanic source，后者走普通 Projectile Runtime Pattern。也不要用 Unity `Collider` / Trigger / prefab 决定落点、触发范围、触发目标、引信或伤害。

Authority: `Combat.Projectiles` 负责 carrier/drop 的弹道、阻挡、过期、snapshot/hash/save 和 flight presenter。CharacterTest bridge 只把 carrier hostile hit 转换为 drop projectile，把 carrier/drop 的静态 impact 或 expiration 转换为 active mine。CharacterTest trap runtime 保存 created/expire/fuse/detonate frame、center/forward/source order，并用 `CombatPhysicsWorld.QuerySphere` 做触发和爆炸范围查询；blast accepted hit 仍通过 `HitResolveSystem`，不直接改 HP。

Data to edit: Ability id/cooldown/action binding、CharacterAction `SendGameplayRequest` frame、Arc Mine carrier/drop 的 projectile definition / spawn profile / presentation binding、`character_test_mechanic_tunings.json` 里的 deploy lock range、fallback forward distance、fuse delay、duration、trigger radius、blast radius、max targets、flight/deploy/fuse/trigger VFX ids，以及 trap blast damage profile / presentation profile。不要把投掷飞行参数复制到 track event，也不要只改 runtime constants 或 StreamingAssets mirror。

Runtime path:

```text
CharacterAction SendGameplayRequest(character_test.arc_mine.deploy)
  -> CharacterTest deterministic target resolver
  -> Combat.Projectiles ballistic carrier character_test.arc_mine.projectile
  -> hostile impact creates Combat.Projectiles drop character_test.arc_mine.drop_projectile
  -> carrier/drop static impact or expiration
  -> active ArcMine state from landing frame
  -> CombatPhysicsWorld.QuerySphere trigger check starts fuse
  -> fuse delay
  -> CombatPhysicsWorld.QuerySphere blast query
  -> HitResolveSystem
  -> damage / telemetry / VFX events
```

VFX path: carrier/drop 的飞行表现由 projectile presentation binding 播放 `vfx.magic.arc_mine.flight`，只跟随 projectile snapshot。mine 落地部署时播放 `vfx.magic.arc_mine.deploy`，hostile 触发引信时播放 `vfx.magic.arc_mine.fuse`，引信结束爆炸时播放 `vfx.magic.arc_mine.trigger`。这些 prefab / ParticleSystem 都是 view-only，不决定落点、触发、伤害或 collision。

Validation: CharacterTest action/ability/catalog focused tests + projectile spawn/ballistic/static impact focused coverage + trap runtime integration + snapshot/hash/save restore coverage；改 projectile 或 tuning catalog 时要验证 generated/mirror consistency。涉及 UI tooltip 时加 CharacterAction editor/timeline focused tests。

Common pitfalls: carrier 命中 hostile 不能直接部署地雷或造成伤害，只能生成 drop projectile；drop projectile 只撞世界/地面/墙/静态物；carrier/drop 过期是安全兜底，要在当前位置投影到地面部署并记录诊断，不能静默丢失技能；地雷入圈只启动引信，59f 内不伤害，60f 后才 blast；self/friendly/dead 目标不触发也不受伤；VFX 只读 projectile flight、部署、引信和爆炸事件；不要把落地后的 mine 写成 projectile、aura 或通用 framework mechanic API，除非已经明确抽象边界。

Example: Arc Mine / 投掷地雷在 frame 12 发送 `character_test.arc_mine.deploy`，锁定 8m 内 hostile 时把 `character_test.arc_mine.projectile` 抛向目标脚下，否则抛向 caster 前方 5m；carrier 命中 hostile 后生成 `character_test.arc_mine.drop_projectile` 继续下落，carrier/drop 落地、撞墙或过期后才创建 mine。mine 最多存在 180 frame，hostile 进入 1.25m trigger sphere 后启动 60 frame 引信，引信结束时用 2.25m blast sphere 对最多 8 个 hostile 走 `damage.arc_mine_blast`。表现资源使用 Arc Mine 专属 `vfx.magic.arc_mine.flight`、`vfx.magic.arc_mine.deploy`、`vfx.magic.arc_mine.fuse`、`vfx.magic.arc_mine.trigger`，不复用 Poison Bottle flight prefab。

## Deployable Persistent Turret / Auto Projectile Source Pattern

Use when: 技能先用 projectile 把一个部署物投放到规则落点，落地、撞墙或过期后创建持续存在的 CharacterTest runtime state；该 state 按固定频率确定性索敌，并只通过标准 projectile / HitResolve 输出伤害。
Do not use when: 只需要一次性区域爆炸、瞬时地面 source、可被攻击摧毁的单位、或 Unity prefab 自己用 Collider/Trigger 决定目标。v1 不把炮台抽成通用 framework deployable API。
Authority: deploy carrier 和 shot 都由 `Combat.Projectiles` 管理飞行、碰撞、过期、snapshot/hash/save 和 presentation；炮台本体由 CharacterTest turret runtime 保存 center、forward、activate/expire frame、nextThinkFrame、shotsFired 和 source order；索敌用 `CombatPhysicsWorld.QuerySphere`，命中仍走 `HitResolveSystem`。
Data to edit: ability id/cooldown/action binding、CharacterAction frame 12 `SendGameplayRequest`、deploy projectile / shot projectile definition、spawn profile、presentation binding、`character_test_mechanic_tunings.json` 里的 deploy range、fallback forward、warmup、duration、scan radius、scan interval、fire interval、max shots、muzzle height 和 deploy/idle/fire/expire VFX ids。不要把炮台规则塞进 track event，也不要只改 generated 或 StreamingAssets mirror。
Runtime path:

```text
CharacterAction SendGameplayRequest(character_test.rune_turret.deploy)
  -> CharacterTest deterministic deploy target resolver
  -> Combat.Projectiles ballistic deploy carrier
  -> carrier static impact or expiration
  -> active RuneTurret state
  -> interval CombatPhysicsWorld.QuerySphere nearest-hostile selection
  -> enqueue character_test.rune_turret.shot_projectile
  -> Combat.Projectiles Linear DirectHit shot
  -> HitResolveSystem
  -> damage / telemetry / VFX events
```

VFX path: deploy carrier 和 shot flight 通过 projectile presentation 播放 `vfx.magic.rune_turret.flight`；炮台部署、待机、开火、消失由 turret runtime 分别发 `vfx.magic.rune_turret.deploy/idle/fire/expire`。所有 VFX 都只读 runtime 事件，不决定索敌、开火、碰撞、命中或伤害。
Validation: CharacterTest ability/catalog/action focused tests + projectile deploy/shot focused tests + turret runtime tick/snapshot/hash/save restore tests + CharacterAction mechanic tuning editor tests；改 catalog 时同步 generated 与 StreamingAssets mirror。
Common pitfalls: 不让炮台 prefab 成为权威对象；不要直接改 HP；不要在 Tick 中做 LINQ、字符串查找、反射、Unity 对象访问或每帧集合分配；无目标时只按 scan interval 重试，不每帧全量索敌。
Example: Rune Turret / 符文炮台在 frame 12 发送 `character_test.rune_turret.deploy`，抛物线投放 `character_test.rune_turret.deploy_projectile` 到锁定 8m 内 hostile 脚下或前方 5m fallback；落地后等待 18f 激活，最多存在 360f，每 12f 搜索一次 10m 内最近 hostile，按 45f fire interval 发射最多 8 发 `character_test.rune_turret.shot_projectile`，每发命中首个 accepted hostile 后通过 `damage.rune_turret_shot` 造成 4 点 light 伤害。
## Projectile Hit -> Target Displacement / Tether Pull Pattern

Use when: projectile 必须先飞行、可被墙体截获，并且只在 accepted hostile hit 后对该目标施加短时位移控制。

Do not use when: 效果是飞行中吸附、范围冲量、推可动物体或环境交互；这些走 `physicalEventsEnabled` + CharacterTest interaction consumer。也不要为纯近身位移技能创建 projectile。

Authority: `Combat.Projectiles` 负责飞行、sweep、首个 accepted hit 和标准 `HitResolveSystem` 结果；CharacterTest 特例 runtime 只在 projectile accepted hit 后注册 active tether pull，并通过目标角色的 deterministic motion request 执行位移。

Data to edit: ability/action binding、CharacterAction `SendGameplayRequest` frame、projectile definition / spawn profile / presentation binding、damage profile、CharacterTest tether pull tuning。不要把 tether duration、stop distance、acceleration 或 terminal speed 写进 projectile definition，也不要用 VFX/prefab collider 决定拉拽。

Runtime path: action timeline 发送 projectile request；projectile direct hit 产生 accepted hostile `CombatProjectileImpactEvent`；CharacterTest bridge 识别 projectile stable id 并创建 active pull；每帧根据 caster 当前 body center + forward anchor 计算 target -> anchor 方向，向 target 提交 `CharacterTestCharacterMotionRequest.ExternalForceRaw`；目标自己的 kinematic/motion settlement 继续处理墙体阻挡和地面摩擦。

Validation: projectile spawn/hit focused tests + CharacterTest integration pull movement tests + snapshot/hash/save/restore tests；涉及热栏或 Showcase 时加 ability catalog / binding UI focused tests。

Common pitfalls: 不绕过 HitResolve 直接拉 friendly/self/dead/blocked 目标；不要把 `impactPower` 当成 tether 参数；不要用 Unity Rigidbody、prefab collider 或 VFX endpoint 做权威；不要把单目标 tether 误归类成 projectile physical event，因为后者是环境/区域 interaction fact。

Example: Hook Chain / 牵引钩链在 frame 12 发射 `character_test.hook_chain.projectile`，12m Linear DirectHit，首个 accepted hostile hit 后停止 projectile，并创建约 24 frame 的目标拉拽，把目标拉向 caster 身前 anchor。

## Active Field And Physical Interaction Pattern

Use when: projectile 飞行期间或 area pulse 时，需要对角色/可动物体施加确定性外力。

Do not use when: 只是普通伤害、Buff 或 VFX shake；也不要把当前 CharacterTest 的吸附、推力、冲量解释逻辑当成 projectile definition 内建通用字段。

Authority: `Combat.Projectiles` 只输出 `CombatProjectilePhysicalEvent` 事实；CharacterTest adapter 把已映射 projectile stable id + trigger kind 转成 `CharacterTestInteractionEvent`；`CharacterTestInteractionPhysicsSystem` 统一消费并执行 query、质量/材质/推力缩放、角色 motion request 和 movable object velocity。

Data to edit: projectile 侧只编辑 `physicalEventsEnabled`、既有 `collision.radius` / `impact.areaRadius` / `impactPower`；CharacterTest 侧编辑 interaction profile 和 projectile physical event adapter；不要新增 projectile 侧 force profile、mass scale、falloff curve 或 Rigidbody 参数。

Runtime path: projectile runtime 输出 active / impact / area pulse / expiration physical event；CharacterTest adapter 按 stable id 映射 attraction、directional push 或 radial impulse；consumer 在确定性物理交互阶段应用结果。

Validation: projectile physical event runtime tests + interaction physics focused tests + movable object tests；若修改映射 stable id，还要覆盖对应 CharacterTest integration。

Common pitfalls: 不用 Unity Rigidbody impulse 做权威；不要在 VFX/audio consumer 里执行物理；不要重新手写每个 projectile 的 active view / area pulse 采样 producer；不要把 unmapped projectile 因为开启了物理事件就自动推环境。

Agent note: do not reintroduce per-skill projectile sampling for Tornado, Sword Wave, Fireball Explosion, or Meteor. The projectile runtime emits `CombatProjectilePhysicalEvent`; CharacterTest adapter code decides whether that fact means attraction, directional push, or radial impulse. Do not move that interpretation into `CombatProjectileDefinition`.

| Producer | 触发 | 交互事件 | Consumer |
| --- | --- | --- | --- |
| Tornado attraction | `CombatProjectilePhysicalEvent.ActiveFrame` | `PhysicalField / AttractionAcceleration / Capsule` | `CharacterTestInteractionPhysicsSystem` |
| Sword Wave push | `CombatProjectilePhysicalEvent.ActiveFrame` | `PhysicalField / DirectionalImpulse / OrientedCapsule` | `CharacterTestInteractionPhysicsSystem` |
| Fireball Explosion impulse | `AreaPulse` / `Expiration` physical event, plus delayed timeline area pulse | `PhysicalField / RadialImpulse / Sphere` | `CharacterTestInteractionPhysicsSystem` |
| Meteor impulse | `AreaPulse` / `Expiration` physical event | `PhysicalField / RadialImpulse / Sphere` | `CharacterTestInteractionPhysicsSystem` |

## Mobility / Dash / Modified Caster Body Hit Window Pattern

Use when: 技能主要效果是驱动施法者或目标在确定性角色运动系统中位移，并且位移期间可能需要 sweep 命中或被静态场景阻挡。

Do not use when: 需要独立飞行体生命周期、projectile presenter、飞行体过期事件或 projectile physical event；也不要为了“看起来在冲”而用 Unity Rigidbody / prefab physics 做权威移动。

Authority: CharacterAction `motionTrack.rootMotion` 决定位移 delta；`CharacterTestCharacterRootMotionSystem` 把它转成 `RootMotionDelta` request，再由 `CharacterTestCharacterMotionSystem` / kinematic blocking 决定实际位置。`rootMotion.obstaclePolicy=StaticOnly` 表示 root motion 位移只把静态场景当位移障碍，目标角色是否受击仍由 `combatTrack` 判定框负责。`combatTrack` 的 `StartHitTrace` / `StopHitTrace` 决定判定窗口；普通武器攻击可走 `CombatWeaponTraceEvaluator`，动作自带碰撞框可用 `hitVolume.sourceKind=ManualCapsule`，冲锋斩这类身体撞击应使用 `hitVolume.sourceKind=CasterBody` 从施法者 runtime physics body 派生范围，并可用 action-local `bodyOffset`、`bodyRadiusScale/Add`、`bodyHeightScale/Add` 做命中框修饰，最终都进入 `HitResolveSystem`，不直接改 HP。

Data to edit: Ability id/cooldown/action binding、CharacterAction `motionTrack.rootMotion`（包含 `obstaclePolicy`）、CharacterAction `combatTrack` hit trace window / `hitVolume.sourceKind`、CasterBody body modifiers、damage profile、presentation profile。Dash Strike 不编辑手动 A/B/radius；基础范围来自角色 `CharacterTestCharacterPhysicsComponent` effective radius / height / centerY，修饰公式是 `radius = bodyRadius * bodyRadiusScale + bodyRadiusAdd`、`height = bodyHeight * bodyHeightScale + bodyHeightAdd`，`bodyOffset` 是角色本地坐标并随 yaw 旋转。不要新增 Dash gameplay request、active dash state、snapshot/hash/network DTO 或 projectile definition 字段。

Runtime path:

```text
CharacterAction motionTrack.rootMotion
  -> CharacterTestCharacterRootMotionSystem
  -> CharacterTestCharacterMotionSystem / kinematic blocking
CharacterAction combatTrack StartHitTrace..StopHitTrace
  -> CombatWeaponTraceEvaluator or StartHitTrace.hitVolume ManualCapsule / modified CasterBody query
  -> HitResolveSystem
  -> hit presentation / telemetry
```

Validation: action import / editor timeline visibility、root motion focused integration、hit-once、wall/static early stop、snapshot/hash/rollback via existing action/motion/trace state。涉及热栏或 Showcase 时追加 ability catalog / binding UI focused tests。

Common pitfalls: 敌人命中不阻挡 dash；静态世界阻挡来自 kinematic motor，不来自 VFX；CasterBody 的手调项是相对 runtime body 的修饰，不是替代 runtime body，也不改变真实移动碰撞体；不要把预览 capsule 的 fallback 数值当运行时权威；不要绕过 HitResolve；不要把 Tornado/Sword Wave 的环境 interaction producer 套用到普通 dash，除非明确新增 dash interaction adapter。

Example: Dash Strike / 冲锋斩在 frame 6..19 通过 rootMotion 每帧本地前向 0.3m，`rootMotion.obstaclePolicy=StaticOnly`，frame 6 `StartHitTrace`、frame 20 `StopHitTrace`，不依赖装备槽，`StartHitTrace.hitVolume.sourceKind=CasterBody`，命中范围由施法者身体 capsule 加 action-local 修饰派生并按上一帧到当前帧做 body sweep，命中策略为 `OncePerEvent`，伤害为 `damage.dash_strike_hit`。

## Instant Non-Projectile Ranged Pattern

Use when: 看起来是远程技能，但不需要飞行体、不需要中途碰撞，只需要同帧确定性目标解析。

Do not use when: 技能需要飞行时间、中途阻挡、墙体截获、命中历史或 projectile presenter。

Authority: CharacterTest deterministic target resolver；命中后仍走 damage / telemetry / hit presentation / VFX 事件。

Data to edit: gameplay request、resolver tuning、VFX/presentation event；不编辑 projectile definition。

Runtime path: `SendGameplayRequest` 进入 CharacterTest 专用 resolver，使用 deterministic target state、Combat query 和稳定排序解析目标链，同帧产生命中/表现事件。

Validation: resolver focused tests + CharacterTest integration；不需要 projectile runtime tests，除非迁移到 projectile 模式。

Common pitfalls: 不创建空壳 projectile 只为播放远程表现；如果未来要做会飞行、会碰撞、可被墙挡住的链球/跳弹，应改用 projectile 模式。

Example: Chain Lightning 在 frame 30 同帧解析首目标和后续跳跃目标，不创建 projectile runtime。

## Aura, Shield, Zone And Channel Patterns

这些技能不是 projectile，但仍通过 AbilityRuntime + CharacterAction 统一释放。

### Aura Buff

Use when: caster-following 或 world-following 范围持续给目标刷新 Buff。

Do not use when: 需要实体飞行、一次性 impact 或 shield collider。

Authority: `CombatGameplayAuraBuffSystem` query 目标并 enqueue `GameplayBuffRequestBuffer`；Buff definition 决定 duration、stack policy 和属性效果。

Data to edit: aura catalog、Buff definition、action request、presentation snapshot binding。

Runtime path: action request 创建 aura source，aura system 每帧用 `CombatPhysicsWorld.QuerySphere` 找目标并提交 Buff request。

Validation: aura/Buff focused tests + HUD/head-pad presentation checks when UI changes.

Common pitfalls: 不直接改 Buff store；HUD/head-pad 只读 Buff presentation snapshot。

Example: Guard Aura。

### Physical Shield

Use when: 技能需要一个跟随 owner 的 shield body / collider，并进入 Combat shield block/hash/SaveState。

Do not use when: 只是加防御 Buff 或播放护盾 VFX。

Authority: `CharacterTestPhysicalShieldRuntime` 负责 cast/owner 同步；`CombatShieldRuntime` 负责 shield potency、block event、hash 和 SaveState。

Data to edit: gameplay request、shield runtime constants/tuning、presentation binding。

Runtime path: action request 进入 `CharacterTestPhysicalShieldRuntime.EnqueueCast`，创建或刷新 shield body，并每帧同步到 owner 前方。

Validation: shield focused tests + CharacterTest integration if behavior changes.

Common pitfalls: VFX 是表现层，不是 shield 权威；不要用 projectile 或 Buff 临时代替 shield runtime。

Example: Physical Shield。

### Channel + Zone Source

Use when: 技能需要引导/telegraph 成功后创建持续 zone/source。

Do not use when: 技能在释放 frame 立即生效，或只需要 projectile impact 创建 source。

Authority: channel runtime 决定 pending/取消/激活；mechanic source runtime 决定持续区查询和状态刷新。

Data to edit: action timeline、channel tuning、mechanic source definition、Buff/status definition、telegraph/source VFX。

Runtime path: action request 启动 channel 并锁定落点；channel 可被受击、动作退出、死亡或实体失效取消；成功到 activation frame 后创建 source，source 用 `CombatPhysicsWorld.QuerySphere` 刷新 hostile 目标状态。

Validation: channel cancellation/activation focused tests + mechanic source integration.

Common pitfalls: mechanic motion policy 只锁主动移动，不锁击退、Tornado 吸附、root motion 或碰撞外力；不要把 pending channel 简化成立即创建 source。

Example: Area Confinement。

## Presentation Pattern

Use when: 任务涉及 charge、flight、impact、beam endpoint、source create、status HUD 或 interaction VFX/audio。

Do not use when: 你想通过表现资源改变规则命中、伤害、范围或物理效果。

Authority: 表现层只读 action event、runtime snapshot、HitResolve/projectile result、source create result、Buff/status snapshot 或 interaction event。

Data to edit: VFX ids、resource catalog、presentation bindings、UI binding。规则参数仍在对应 runtime/source catalog。

Runtime path: rule runtime 先产出事件或快照，presentation bridge 再播放 VFX/audio/UI。

Validation: presentation binding/editor checks；如果只是表现 id 改动，不需要 projectile physics tests。

Common pitfalls: 不烘焙固定 beam 距离替代 runtime endpoint；不让 prefab collider 参与命中；不在 VFX/audio consumer 中执行物理。

| 表现类型 | 来源 | 规则关系 |
| --- | --- | --- |
| charge / cast cue | CharacterAction `SpawnVisualCue` | 只表现时间线，不创建命中。 |
| projectile flight | `CombatProjectileActiveView` -> `CombatProjectileUnityPresenter` | view-only 跟随。 |
| impact / hit VFX | HitResolve / projectile impact / CharacterTest bridge | 使用真实 hit point 或 resolved endpoint。 |
| beam / laser path | projectile result endpoint | 运行时长度参数，不烘焙固定距离。 |
| source create VFX | mechanic source create result | source 成功创建后播放。 |
| status HUD / head pad | Buff presentation snapshot / Gameplay status | 只读状态展示，不改规则。 |
| interaction VFX / audio | `CharacterTestInteractionEvent` consumer | 消费物理 interaction，不执行物理。 |

## Do Not Boundaries

- 不使用 Unity Rigidbody / PhysX / prefab collider 作为 CharacterTest 技能规则权威。
- 不让 VFX、Audio、Prefab、Presenter 决定命中、伤害、范围、Buff 或物理交互。
- 不绕过 `HitResolveSystem` 直接改 HP 或直接认定命中。
- 不把 Dash / Displacement 误建成 projectile，也不要为普通冲锋新增隐藏 gameplay request；dash 位移优先属于 CharacterAction root motion，命中窗口属于 combatTrack + HitResolve。
- 不把 Projectile Carrier -> Drop Projectile -> Fuse Mine 误建成瞬移部署、aura 或 Unity Trigger；飞行、hostile 碰撞转 drop、二段下落和落地必须由 `Combat.Projectiles` 决定，落地后的触发、引信和爆发范围必须由 CharacterTest deterministic trap runtime 和 Combat query 决定。
- 不把 Hook Chain 这类 accepted hit 后的单目标 tether 当成 `physicalEventsEnabled` 通用环境事件；tether pull 是 CharacterTest 目标位移特例。
- 不把 Tornado、Sword Wave、Fireball Explosion、Meteor 的 interaction 解释逻辑当成 projectile definition 通用字段；projectile 只负责 `physicalEventsEnabled` 和现有半径/impactPower fact 输出。
- 不把 generated JSON 或 StreamingAssets mirror 单独当作唯一事实源；它们必须和 authoring/source pipeline 保持一致。
- 不混用 `ArcToTarget` 与 `BallisticArcToTarget`：前者是设计弧线，后者是游戏重力弹道。
- `BallisticArcToTarget` 不使用 `aim.maxRange`；可达性由最大初速度、重力、高度差和角度限制决定。
- 不在 projectile tick / interaction hot path 中加入 LINQ、per-frame collection、字符串查找、反射或 Unity 对象访问。

## Task-Based Validation Matrix

| 改动类型 | 默认验证 |
| --- | --- |
| docs-only | `git diff --check` + docs health |
| projectile config/importer/schema | projectile importer/runtime focused tests + generated/mirror consistency check |
| projectile motion sampling | projectile runtime motion tests + snapshot/hash/save restore coverage |
| CharacterAction projectile editor/preview | CharacterAction editor/timeline focused tests |
| Poison Bottle / projectile -> source | CharacterTest integration + mechanic source/Buff refresh focused tests |
| projectile carrier -> drop projectile -> fuse mine | projectile spawn/ballistic/static/hostile impact tests + CharacterTest trap fuse runtime integration + snapshot/hash/save restore |
| projectile hit -> target displacement / tether pull | projectile hit tests + CharacterTest motion integration + snapshot/hash/save restore coverage |
| one-frame beam / sweep | projectile sweep tests + endpoint presentation check when VFX changes |
| multi-shot/delayed request | CharacterAction timeline tests + CharacterTest integration |
| active field / impulse interaction | interaction physics tests + movable object/character motion focused tests |
| mobility dash / combat hitbox window | CharacterAction import/editor visibility + root motion/combat integration + hit-once/wall-stop + existing snapshot/hash focused tests |
| aura / Buff | GameplayBridge/Buff tests + presentation snapshot checks when UI changes |
| shield | Combat shield tests + CharacterTest physical shield focused tests |
| channel + zone source | channel cancellation/activation tests + mechanic source integration |

Useful entry points:

| 类型 | 入口 |
| --- | --- |
| projectile runtime / importer | `Tests/Combat/Projectiles/*` |
| CharacterAction editor / timeline | `Tests/CharacterAction/*` |
| CharacterTest integration | `Tests/Game/CharacterTest/CharacterTestIntegrationContractTests.cs` |
| aura / Buff | `Tests/Combat/GameplayBridge/*`、`Tests/Ability/*Buff*` |
| shield | `Tests/Combat/Shields/*` |
| Skill Showcase smoke | `Tests/Game/CharacterTest/CharacterTestSkillShowcaseTests.cs` |

## Maintenance Rules

新增技能或改技能实现前，先更新或确认 `Skill To Type Index` 的主实现类型，再决定是否扩展正文模式。

- 如果只是某类 projectile 的新数值或新 VFX，不新增模式。
- 如果需要新的 runtime authority、source state、interaction producer 或 target resolver，要新增实现类型说明。
- 如果某个 CharacterTest 特例被提升为通用 framework capability，要同步更新 `Docs/Interfaces/*.md`，并在本文移除“CharacterTest 特例”措辞。
- `CAPABILITIES.md` / `USAGE.md` 只保留摘要和链接，不写逐技能长段落。
- 每个实现类型章节保持 `Use when` / `Do not use when` / `Authority` / `Data to edit` / `Runtime path` / `Validation` / `Common pitfalls` 模板，方便 Agent 直接按章节执行。
