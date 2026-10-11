# CharacterTest Game Development Standard

> Status: Guide
>
> Date: 2026-05-27
>
> Scope: New game feature work, playable demos, runtime showcases, scene validation, and future project-specific game development built on MxFramework.

## Decision

`CharacterTest` is the current game-development reference implementation for MxFramework.

Future game features should use CharacterTest as the primary standard for runtime structure, feature composition, resource loading, Story-driven flow, UI integration, diagnostics, and Unity view binding.

Older demos are historical references. They may still contain useful code, but they are not the default architectural standard for new game work. Do not copy an older demo pattern into new game code unless it has been checked against this document and the current CharacterTest implementation.

CharacterTest source code lives under the game-level namespace and folder:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest
MxFramework.Game.CharacterTest
```

This location is intentional. CharacterTest is not an isolated demo namespace. It is the current game-development standard slice for framework consumers.

## Why this standard exists

The previous demos were built across earlier framework phases. They prove individual systems, but many of them predate the current direction:

- Runtime ECS Style for game state.
- Resource-first character and equipment loading.
- Story-driven flow orchestration.
- FairyGUI for player-facing game UI.
- Debug UI as developer-only diagnostics.
- Clear separation between runtime data and Unity view objects.

CharacterTest is where these current rules are assembled into one vertical slice. It should become the living example for how future games consume MxFramework.

## Required architecture for new game features

New game feature code should follow this layering:

```text
Story / game trigger
  -> Runtime command or request
  -> Runtime ECS Style world/module
  -> Runtime entity + component data
  -> Runtime systems
  -> Unity view bridge
  -> ResourceManager loads prefabs/assets
  -> FairyGUI or Debug UI presentation
```

Avoid this pattern for new game work:

```text
Button / Story step / MonoBehaviour
  -> Object.Instantiate(prefab)
  -> direct state mutation on GameObject
  -> hidden input or equipment behavior inside prefab
```

Unity GameObjects are views. They are not the authoritative game state.

## CharacterTest responsibilities

CharacterTest should demonstrate framework-level reusable game capability:

- character selection and spawn requests;
- team assignment;
- control ownership;
- default equipment loading;
- character prefab view binding;
- weapon prefab loading and socket attachment;
- locomotion/input binding;
- Story-driven runtime flow;
- FairyGUI game UI when player-facing UI is needed;
- Debug UI for developer-only inspection;
- resource catalog/provider usage;
- lifecycle cleanup and diagnostics.

CharacterTest may contain temporary data while the framework API is still forming, but temporary data must live at the edge. The core flow should still match the intended production architecture.

Temporary bootstrap data is allowed only at the entry/config boundary, for example a current test character id or the current generated package path. Temporary data must not be embedded inside reusable runtime parsing, animation, movement, resource, Story, or UI logic.

## Runtime ECS Style requirements

CharacterTest character loading should create runtime data first.

Minimum runtime concepts:

```text
CharacterEntity
CharacterIdentity
Team
Transform / SpawnPose
EquipmentState
ControlOwner
UnityViewBinding
```

Minimum systems:

```text
CharacterSpawnSystem
CharacterEquipmentSystem
CharacterInputControlSystem
CharacterMotionSystem
CharacterAnimationSystem
CharacterUnityViewBridge
```

The exact implementation can be lightweight during the vertical slice, but the direction must be ECS Style:

- store character state in runtime data;
- process changes through explicit requests/systems;
- bind Unity views after runtime state exists;
- keep GameObject references out of authoritative gameplay state;
- support more than one character without all characters consuming the same input.

Current formalized runtime storage:

- `CharacterTestCharacterRuntimeWorld` owns a CharacterTest typed component world keyed by
  `CharacterTestCharacterEntityId`.
- `CharacterTestCharacterEntity` is a debug/read facade, not the formal system API and not an
  authority container. Runtime systems that mutate state must use typed component stores,
  `CharacterTestCharacterEntityId`, or facade helper methods that immediately write through to
  those stores.
- `CharacterTestCharacterActionRuntime` is bound to the runtime world and starts, ticks, freezes,
  clears, and inspects actions by `CharacterTestCharacterEntityId`; core action, death, revive,
  cohesion-break, and Broken-hit reaction flows must not require passing a
  `CharacterTestCharacterEntity` object as authority.
- Authoritative Transform, Control, Equipment, Physics, Input, Action, Animation, Combat mirror,
  Lifecycle, EntityPresence, AuthorityPose, movement state, and request buffers are stored in
  typed component stores.
- HP, lifecycle, attributes, buffs, and scatter pressure continue to use `GameplayComponentWorld`
  as the cross-module Gameplay authority. CharacterTest stores keep only CharacterTest slice state
  and mirrors needed by UI/debug/view sync.
- `CharacterTestBattleStateComponent` is the internal combat decision cache. It is recomputed from
  Lifecycle, Action, StatusControl, ScatterPressure, Control, and Movement stores before combat
  decisions and after lifecycle/reaction transitions. Combat, input, movement, and Runtime AI
  Planner code should consume its gates (`CanReceiveHpDamage`, `CanReceiveScatter`,
  `CanStartNormalAction`, `CanMoveByInput`, `CanRunRuntimeAiPlanner`) instead of hand-writing
  scattered `IsAlive` checks. BattleState is not a new authority source and is not public schema.
- CharacterTest combat lifecycle stays orthogonal: `LifeState` owns Alive/Dying/Dead/Reviving,
  `PressureBand.Broken` owns scatter-break recovery, CharacterAction owns reaction/death/revive
  actions, and control/stasis gates own movement or planner suppression. Ordinary non-Broken hits
  remain non-interrupting; death, cohesion break, and Broken-hit are explicit reaction/lifecycle
  entries.
- Special hit reactions are formal CharacterTest state, not ordinary hit-stun timers. The first
  v1 state is `Launched`: it is stored in a typed special-hit component, entered only from Alive +
  Broken targets with a valid `launchPower` input and target launch susceptibility, and reflected
  into BattleState gates. `Launched` disables normal action start, input movement, and Runtime AI
  Planner while still allowing HP damage; it holds Broken recovery through `Airborne` and the
  default `12f` landing recovery. ReactionProfile selects the launch presentation action, but the
  state authority remains in CharacterTest stores.
- Death is not entity destruction. `LifeState=Dead` means the corpse/terminal presentation remains
  queryable and can still be revived explicitly. Entity existence is owned by
  `CharacterTestEntityPresenceComponent`: `Active` participates in runtime systems,
  `PendingDestroy` closes all BattleState gates while waiting for cleanup, and `Destroyed` is only a
  short-lived trace state before `CharacterTestCharacterRuntimeWorld.Remove` deletes the entity from
  CharacterTest stores, `GameplayComponentWorld`, `CombatGameplayMap`, action runners, and Unity
  views. `GameplayLifecycleState.Destroyed` remains a non-alive Gameplay mirror; it is not the
  CharacterTest removal authority.
- Authoritative character collision shape is the current `CharacterTestColliderPostureComponent`,
  not the base `CharacterTestCharacterPhysicsComponent` and not a Unity Collider. CharacterAction
  Motion timeline events `SetCollisionProfile` / `ResetCollisionProfile` are the author-facing way
  to line up standing, crouch, low, corpse, and revive-low collision profiles with animation frames.
  Lifecycle and presence may override the action profile (`Dead` / `PendingDestroy` -> corpse);
  `CombatPhysicsWorld` sync reads the resolved posture, and Unity colliders remain view/debug only.
- New CharacterTest runtime code should use component-world access and `CharacterTestCharacterEntityId`
  for mutations. Facade helper methods are reserved for existing view/debug/read-model adapters and
  small boundary glue where the helper immediately writes through stores. It must not add new
  authoritative mutable fields to `CharacterTestCharacterEntity`.
- This is a pure C# ECS-style runtime, not Unity DOTS. Unity GameObjects, Animators, FairyGUI, and
  CharacterStudio remain view/authoring surfaces and do not own runtime authority.

## Current CharacterTest character loading and movement flow

The current CharacterTest role slice uses a request-driven runtime module:

```text
Story step or debug request
  -> CharacterTestCharacterSpawnFlow.RequestSpawn
  -> CharacterTestCharacterRuntimeModule.EnqueueSpawn
  -> CharacterTestCharacterSpawnSystem
  -> CharacterTestCharacterRuntimeWorld.Spawn
  -> CharacterTestCharacterUnityViewBridge.TryCreateView
  -> CharacterTestRuntimeCharacterLoader
  -> ResourceManager loads character prefab
  -> CharacterDefaultEquipmentRuntimeBinder loads default weapons
  -> CharacterTestCharacterInputSystem assigns input only to LocalPlayer entities
  -> CharacterTestRuntimeAiPlannerControlSystem assigns planner output to AiControlled entities
  -> CharacterTestCharacterMotionSystem applies gravity, jump, move, and facing
  -> CharacterTestCharacterAnimationSystem derives locomotion animation state from runtime motion
  -> CharacterTestCharacterViewSyncSystem pushes transform to the Unity view
  -> CharacterTestCharacterAnimationSyncSystem pushes runtime animation state to the Unity view
```

Runtime state is authoritative in `CharacterTestCharacterRuntimeWorld`. The generated prefab is a view/resource. Prefab-local `CharacterRuntimeInputMotionController` must not move spawned CharacterTest entities directly; CharacterTest disables it and feeds locomotion through the runtime animation sync path.

This makes the current CharacterTest character loading/movement flow complete for the vertical slice:

- multiple entities can spawn from the same character definition;
- bootstrap Story flow currently spawns one `LocalPlayer` and one `AiControlled` opponent;
- only `LocalPlayer` consumes local WASD/jump input;
- `AiControlled` entities can consume Runtime AI Planner output through the CharacterTest adapter layer;
- the CharacterTest Runtime AI Planner adapter uses `AiBlackboard` + `AiSensorManager` as the sensing boundary, then maps selected planner actions to runtime input without touching LocalPlayer input;
- non-player entities still receive runtime gravity;
- movement, facing, grounded state, and locomotion blend are derived from runtime entity data;
- locomotion animation state is represented by `CharacterTestCharacterAnimationComponent` before it reaches the Unity view;
- default weapons load through the runtime equipment binder after the body prefab is instantiated.

Remaining promotion work is to extract `CharacterTestCharacterMotionSystem` rules into a framework-level reusable character movement API after the vertical slice stabilizes.

## Authoritative motion request rules

CharacterTest movement is centralized in `CharacterTestCharacterMotionSystem`.

Rules:

- Runtime entity transform and velocity are authoritative. Unity Animator, Playables, prefab scripts, and view transforms must not directly move CharacterTest entities.
- Input movement, Action motion tracks, action-authored root motion, buffs, collision reactions, friction, and gravity must enter the runtime as explicit motion requests or movement state read by `CharacterTestCharacterMotionSystem`.
- `ApplyImpulse` from an Action motion track becomes a `CharacterTestCharacterMotionRequest` first. `JumpImpulseY` may remain as a short-lived debug/compatibility field, but it is not the long-term authoritative motion API.
- Root motion must be authored in `motionTrack.rootMotion.frames` and enter as a `RootMotionDelta` request in the action frame before motion simulation. Live Unity root motion must not be read back from the view as authoritative gameplay movement.
- `RootMotionDelta` is a per-frame displacement request. It may move the entity this frame, but it must not become persistent horizontal or vertical velocity unless another request explicitly changes velocity.
- CharacterTest root motion production lives in `CharacterTestCharacterRootMotionSystem`. It runs after `CharacterTestCharacterActionSystem` and before `CharacterTestCharacterMotionSystem`, reads the current action's `MotionTrackConfig.RootMotion`, checks the configured animation event/clip source when present, and enqueues `CharacterTestCharacterMotionRequest.RootMotionDelta`.
- Action JSON enables this path with `motionTrack.usesRootMotion=true` and owns the actual movement data in `motionTrack.rootMotion.frames`. Animation clips and `MxAnimationBake` may be used by the authoring tool to generate or preview samples, but the runtime action config is the source of truth.
- `SetControlScale` from Action motion tracks enters CharacterTest as motion requests/action state, then is resolved only by `CharacterTestCharacterMotionSystem`.
- Control scale affects only active-control contribution: movement input target velocity and active yaw steering, including target-lock steering. It does not scale root motion, impulses, external forces, collision response, friction, or gravity.
- Formal hit reactions use the same rule: every accepted ordinary hit resolves a Reaction action, whose first-frame `SetControlScale(0, 0)` blocks autonomous movement and turning while Combat/Kinematic knockback continues to move the character. The reaction action's authored release frame restores `SetControlScale(1, 1)`.
- `HitStunFramesRemaining` is Combat state and diagnostics only. It must not be read by MotionSystem as movement permission, and MotionSystem must not infer a hit lock from velocity direction, a fixed frame window, or the last-hit frame.
- Reaction completion never restores an earlier transform. Player or Runtime AI Planner movement resumes from the authoritative post-knockback position.
- To restrict an attack, place `SetControlScale` on the attack motion track. `movementControlScale` and `rotationControlScale` are independent; omit an axis when that axis should keep its current scale.
- `SetControlScale` must not change `movementMode`; use `SetMovementMode`, `ApplyImpulse`, or another explicit motion event when the action actually needs a movement-state transition.
- CharacterTest resets action control scales to `1/1` when the action finishes, is cancelled, or is interrupted. `ResetControlScale` is legacy-compatible, but normal authoring does not require a reset event.
- Collision and ground resolution stay inside the motion system. When framework physics is enabled, root motion displacement must go through the same collision path as regular movement.
- Character-to-character blocking is authoritative in `CharacterTestCharacterMotionSystem` and `CombatPhysicsWorld`. Dynamic character bodies use `CharacterTestCharacterPhysicsComponent` radius/height/mass values and are included in the motion obstacle mask; Unity `Collider` / `Rigidbody` objects remain view/debug-only and must not decide gameplay collision.
- Continuous character pushing is contact pressure: while a moving character keeps pressing into another blocking character, the attempted horizontal motion must keep transferring through effective mass and `pushScale` until input stops, the target cannot be pushed, or another authoritative obstacle blocks the chain.
- Runtime physical profile data comes from the character package `geometry.bodyProfile` when available. Runtime modifiers may change `massScale`, `radiusScale`, `pushScale`, `blocksCharacters`, and `canBePushed`, but they must stay on the CharacterTest physics component unless a future Buff/equipment pipeline explicitly owns those float geometry modifiers.
- Missing or stale root motion data for an action that declares root motion is a runtime resource/config error. It should be logged as an Error with action id, stable event id, animation key/resource key, entity id, and action source path where available, and should also be visible in the CharacterTest trace/debug output.

This keeps internal and external speed changes composable: authored jump impulses, root motion deltas, future buff forces, knockback, elastic collision responses, friction, and gravity all meet at one authority before the Unity view is synced.

## Combat hit trace authoring rules

CharacterTest 的属性、伤害、散势压力 / 散势债务、动作聚限支撑、HeadPad / DebugUI、Telemetry、未来 Buff 来源和装备属性边界统一定义在 `Docs/Guides/CHARACTER_TEST_ATTRIBUTE_DESIGN.md`。修改 HP、Attack、Defense、`ScatterDebt`、`CohesionLimit`、Action 内联伤害字段或战斗 UI / Debug 输出前，先以该文档作为 CharacterTest 专用事实源。

- Action timelines open weapon hit detection by `equipmentSlotId`, for example `mainHand`. They do not author weapon-specific trace ids for new data.
- Weapon trace configuration belongs to weapon/package data: `WeaponConfig.TraceBindings` defines trace length/radius/proximal range/slot binding, and geometry `weaponTraces` defines blade root/tip locator paths, authored locator poses, and optional `proximalExtension` + `proximalRadius`.
- CharacterTest runtime resolves `equipmentSlotId -> current/default equipped weapon -> weapon trace binding -> geometry trace -> baked weapon trace frame` before building `WeaponTraceFrame`.
- Legacy `traceProfileId` hit trace events may still be loaded for compatibility, but they should not be the primary Authoring UI workflow.
- Hit windows that deal damage must carry `hitEvent.damage`: damage type, base damage, attack attribute scale, `impactPower`, `launchPower`, tags and analysis category. `damageProfileId` and old top-level damage fields are invalid for new CharacterAction / CharacterTest data.
- `ApplyGameplayEffect` can produce non-weapon area damage in CharacterTest v1 when it carries `hitEvent.damage` plus an `area_#m` `targetPolicyId`. It is resolved as an AreaPulse damage source, queries `CombatPhysicsWorld`, and still goes through `HitResolveSystem` and Gameplay HP commands.
- CharacterTest resolves the common `hitEvent.damage` payload before submitting `HitCandidate` to `HitResolveSystem`; there is no CharacterTest damage profile catalog fallback. `HitResolveSystem` remains responsible for self/friendly/duplicate/blocking/invincible/super-armor decisions.
- CharacterTest writes structured `DamageTelemetryRows` with frame, source kind, action, stable event, common damage identity, weapon, target, hit zone, body part, incoming/final damage and HP before/after. Debug logs stay concise and are not the analytics source.
- CharacterTest HP authority is `GameplayComponentWorld` + `GameplayAttributeSetComponent`. Weapon hits produce resolved damage results, then `CombatDamageApplicationSystem` emits `GameplayRuntimeCommandFactory.AddComponentAttribute(..., -finalDamage)` for HP. `CombatState` mirrors HP plus hit-stun / last-hit data for Debug UI and HeadPad.
- CharacterTest scatter pressure authority is `GameplayScatterPressureComponent`, not a regular display attribute. Accepted resolved damage with `ImpactPower > 0` must enqueue `GameplayScatterPressureRequest`; friendly/self/duplicate/blocked results must not add scatter pressure.
- CharacterTest reaction profiles select ordinary/strong high, mid, and low hit actions for accepted hits. CohesionBreak, Launch, Death, guard break, and armor break retain their higher-priority reaction semantics and are not overwritten by an ordinary hit reaction in the same frame.
- Buff is not implemented in this CharacterTest damage pass. Future Buff / DOT integration must emit `DamageRequest(sourceKind=StatusTick)` into the same damage application chain; Buff tick code must not directly mutate HP.

Current source layout:

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition
  Unity scene bootstrap and C# composition root wiring.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Character
  CharacterTest edge configuration, spawn flow, movement profile, and animation resource bridges.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat
  CharacterTest action, ability, guard, authority-pose, and procedural combat feedback wiring.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Scene/CombatPhysics
  Scene-authored combat physics markers, adapters, and preview bridge code.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Presentation
  Audio and VFX presentation adapters.

Assets/Scripts/WGameFrameworkProject/CharacterTest/UI
  FairyGUI player-facing UI hosts plus CharacterTest UI runtime state.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Diagnostics/DebugUi
  Developer-only Debug UI bootstrap, snapshot, view, and source code.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Network
  CharacterTest network session, snapshot, replay, transport, automation, and diagnostics code.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Story
  CharacterTest Story content, fixture, text, debug source, and flow bridge code.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World
  Runtime entity ids, components, spawn requests, and world storage.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems
  RuntimeHost module and per-frame systems split by Character, Combat, Projectiles,
  RuntimeAiPlanner, Harness, and GeneratedContent responsibilities.
  Harness/Genesis groups Genesis surface, force-origin, boundary-form, combat-potency,
  movable-natural-object, and potency-mechanics validation probes and result DTOs.

Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/View
  Unity resource loading, prefab instantiation, equipment binder setup, and GameObject sync.
```

## Resource and equipment rules

Character and equipment loading must be resource-driven.

Rules:

- Character config/request stores ids and resource keys, not Unity object references.
- Character runtime prefab contains body, sockets, colliders, runtime binders, and view components.
- Character runtime prefab must not contain concrete weapon model instances.
- Each weapon is its own generated weapon prefab.
- Default equipment comes from character/equipment data.
- Runtime equipment flow loads weapon prefabs and attaches them to sockets.
- Resource loading uses `ResourceManager`, `ResourceCatalog`, `ResourceKey`, and providers.

Temporary CharacterTest catalogs are allowed only as a bridge until formal generated catalogs are consumed directly.

## Character animation resource rules

Character animation follows the same resource ownership rule as character and equipment assets:

- A character does not own `AnimationClip` assets.
- A character prefab must not serialize locomotion `AnimationClip[]` as its animation source.
- A character export/runtime entry folder must not receive copied `.anim` files just because that character references them.
- Character data references animation configuration through an animation runtime binding/profile.
- Locomotion configuration defines blend ids, blend points, clip ids, playback speed, looping, and `runtimeResourceKey`.
- The animation clip registry maps `runtimeResourceKey` to the shared project animation asset.
- Runtime playback loads clips through `ResourceManager` and an appropriate provider, then passes the resolved handles into the Unity Playables backend.
- Missing animation config, missing clip registry entries, missing provider entries, and failed clip loads must log enough context to identify the character, animation binding, blend id, clip id, resource key, provider, and address.

The current CharacterTest path is:

```text
CharacterTestCharacterDefinition.AnimationBinding
  -> CharacterTestCharacterIdentityComponent.AnimationBinding
  -> CharacterTestRuntimeCharacterLoader
  -> CharacterTestCharacterAnimationResources.TryConfigureLocomotion
  -> animation_set_definition.json
  -> animation_clip_registry.json
  -> ResourceManager.Load<AnimationClip>
  -> CharacterRuntimeLocomotionBlendController
  -> UnityPlayablesAnimationBackend
```

`CharacterTestCharacterAnimationResources` must remain data-driven. It may parse paths and ids supplied by `CharacterRuntimeAnimationBinding`, but it must not hard-code a concrete character id, concrete package name, concrete clip list, or concrete asset path.

Editor Play Mode may use `CharacterTestEditorAnimationAssetProvider` to bridge shared project `.anim` assets through `AssetDatabase`. This is an editor bootstrap provider, not a production build provider. Production runtime should replace it with the formal catalog provider, AssetBundle provider, Addressables provider, or another approved project resource provider.

## Input ownership rules

Input is not owned by the prefab.

Rules:

- A spawned character has explicit control ownership.
- Only one local-player character should consume WASD/local input unless multi-player ownership is explicitly modeled.
- Non-player or scripted characters must not read local input.
- `CharacterRuntimeInputMotionController` is a Unity view/control adapter, not proof that the character is the player.
- Spawn flow must decide whether input motion is enabled for a view based on runtime `ControlOwner`.

Expected control modes:

```text
None
LocalPlayer
Scripted
AiControlled
```

## UI rules

Use FairyGUI for player-facing game UI in CharacterTest and future game work.

Debug UI remains developer-only and may use the existing DebugUI/UI Toolkit infrastructure.

Do not use older demo UI patterns as the default for new player-facing UI.

## Story flow rules

Story may trigger game flow, but it must not directly become the game state owner.

Allowed:

```text
Story step -> enqueue spawn request
Story step -> request UI open/close
Story step -> request camera or presentation action
```

Current CharacterTest runtime command boundary:

```text
Story step / Unity input adapter / Debug request
  -> CharacterTestRuntimeCommandFactory
  -> CharacterTestRuntimeCommandModule.CommandBuffer
  -> RuntimeHost tick drains commands
  -> CharacterTest runtime systems mutate Runtime ECS-style state
  -> CharacterTestRuntimeEvent recent log / event queue feeds Debug UI
```

`CharacterTestRuntimeCommandModule` owns CharacterTest-level commands such as character spawn, camera mode changes, and target lock changes. Story and Unity view adapters enqueue commands; they do not directly mutate character spawn queues, camera lock state, or runtime world state. This is the current minimum standard before adding replay/hash export, SaveState orchestration, or wider framework-level promotion.

Avoid:

```text
Story step -> directly instantiate gameplay prefab
Story step -> directly mutate authoritative character state
```

## Debug and diagnostics rules

CharacterTest should expose enough diagnostics for development:

- current character entities;
- spawn requests/results;
- resource keys and load status;
- equipment state and loaded weapon instances;
- control owner;
- Unity view binding status;
- Story step that triggered the change.

Debug UI is allowed to be developer-specific. It does not need to match player UI style.

The current CharacterTest Debug UI uses a task-centric developer-only panel (`CharacterTestDeveloperDebugPanel`).
The panel is split into snapshot data (`CharacterTestDeveloperDebugSnapshot`), snapshot construction (`CharacterTestDeveloperDebugSnapshotBuilder`),
UI Toolkit rendering (`CharacterTestDeveloperDebugView` / `CharacterTestDeveloperDebugViewStyles`), and MonoBehaviour lifecycle wiring (`CharacterTestDeveloperDebugPanel`).
The generic DebugUI overlay and `CharacterTestSliceDebugSource` remain available as framework observability infrastructure,
but CharacterTest's in-game development surface is organized around the questions developers actually ask while running the slice:
runtime health, characters, camera/input, commands/events, Story state, and resources.
It must expose runtime inspection data instead of only console logs:

- Runtime Inspector: frame/world summary, entity ownership counts, local input, camera mode, target lock, and RuntimeHost module order.
- Runtime Commands: registered command count, pending command count, last drained count, command errors, and queue state.
- Runtime Event Log: recent `CharacterTestRuntimeEvent` entries for spawn, camera mode, and target lock changes.
- Characters: authoritative runtime entity transform, input, grounded state, velocity, and locomotion animation state.

## How to use older demos

Older demos can still be useful for narrow module examples:

- RuntimeHost and command buffer patterns;
- ResourceManager basics;
- simple UI binding examples;
- diagnostics/debug source examples;
- isolated tests.

But they are not authoritative for new game architecture.

When an older demo conflicts with CharacterTest, prefer CharacterTest unless the relevant framework interface document says otherwise.

## Agent requirements

For any new game feature, playable demo, runtime showcase, or CharacterTest extension:

1. Read `Docs/AGENT_GAME_CREATION_GUIDE.md`.
2. Read this document.
3. State how the work maps to the CharacterTest standard.
4. Use Runtime ECS Style for game state unless explicitly documenting a gap.
5. Use Resources/Config/Story/UI/Debug systems instead of creating parallel one-off systems.
6. If a temporary bridge is needed, keep it named and isolated so it can be replaced by the formal framework API.

## Acceptance criteria for future CharacterTest work

A CharacterTest feature is aligned with the standard only if:

- authoritative state is runtime data, not Unity GameObject state;
- resource loading uses framework resource APIs;
- character animation is resolved through animation binding/config/ResourceManager, not prefab-held clips or copied character-local `.anim` files;
- player-facing UI uses FairyGUI;
- debug UI is clearly developer-only;
- Story triggers requests instead of owning gameplay state;
- input ownership is explicit;
- multiple characters can exist without accidental shared local input;
- generated prefabs are treated as views/resources, not as game-state containers;
- the implementation can be promoted into reusable framework guidance.

## CharacterTest third-person camera

CharacterTest 的第三人称相机是框架级 Camera 接入示例：`GameManager` 只负责组合根绑定，`CharacterTestThirdPersonCameraRigBridge` 从 Runtime 角色实体生成 `MxCameraTargetSnapshot`，再交给 `MxCameraUnityRig` 求值并应用到 Unity Camera。相机状态是表现层，不回写角色移动、Runtime hash、Replay 或 SaveState。

当前相机由“视角模式”和“目标锁定状态”组合成第三人称/自动观战状态；Arena 只通过 HUD 和 `V` 快捷键切换这些已有 CharacterTest 镜头模式：

- `ThirdPersonControlled` + 无锁定：只跟随 `CharacterTestCharacterControlOwner.LocalPlayer`，鼠标锁定并隐藏时控制镜头 yaw/pitch。
- `ThirdPersonControlled` + 锁定：不把锁定目标加入相机 target group，而是使用 `Follow` profile 精确控制镜头。相机站在玩家身后，yaw 朝向锁定目标，pitch 由“目标胸口落在屏幕约 2/3 高度”反推；随后用角色 bounds extents 推导身高和半径，并用二分搜索求“玩家脚部和身体中心仍在安全屏幕区域内”的最小后退距离，避免通过大量锁定专用参数硬调镜头。锁定/解锁会平滑插值 yaw、pitch、distance 和 focus offset。该状态不使用 `GroupFollowPerspective` 的 bounds-distance 自动压缩，避免锁定镜头被单目标 bounds 拉得过近；锁定目标同时影响角色朝向和移动/动画解释。
- `MultiTargetAuto` + 无锁定：收集当前所有角色生成 target group，交给 Camera core 自动取景；该模式保持鼠标可见，适合多人观察和调试。
- `MultiTargetAuto` + 锁定：只把 LocalPlayer 和锁定目标加入 target group，形成“玩家 + 目标”的锁定多角色取景。
输入通过框架 `IInputProvider.Snapshot` 读取 `Look`、`Pause/Cancel`、`RightClick` 和 `CameraModeNext`，不直接读取 Unity legacy input。`Pause/Cancel` 负责 CharacterTest 菜单开关；Unity Editor Play 下 CharacterTest 会把现有 `Gameplay/Pause` 和 `UI/Cancel` 的键盘默认临时切到 `P`，让 `Esc` 留给 Game 视图释放鼠标，正式 Player 包仍使用 `Esc`。第三人称鼠标未捕获时右键先捕获鼠标，鼠标已捕获时右键切换锁定；多角色自动镜头中鼠标保持可见，右键直接切换锁定。Arena 运行中按 `V` 只提交现有 CharacterTest camera mode command，不直接改 Unity Camera。

第三人称角色移动使用 camera-facing basis：镜头水平前方就是角色输入的前方，镜头右方就是角色输入的右方。未锁定目标时，LocalPlayer 移动会自动面向世界移动方向；锁定目标时，LocalPlayer 保持面向锁定目标，同时移动仍按相机前方/右方解释。第三人称控制相机的 yaw 默认独立于角色 yaw，避免“相机跟随角色朝向、角色又跟随相机前方”的反馈环。

角色移动、朝向和输入仍以 CharacterTest Runtime ECS-style world 为权威。后续如果需要相机相对移动，应通过输入/移动系统消费 `MxCameraFacingBasisResolver` 的结果，而不是让相机直接改角色状态。

### CharacterTest camera source split

相机代码按职责拆分：`CharacterTestThirdPersonCameraRigBridge` 只做 Unity 生命周期、输入、目标选择、target snapshot 和 profile 装配；`CharacterTestLockedThirdPersonCameraComposer` 负责锁定第三人称构图数学；`CharacterTestCameraProfileSmoother` 负责 profile 插值；`CharacterTestCameraMode` 独立承载相机模式枚举。
