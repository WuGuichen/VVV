# 角色综合测试场景设计

> 状态：设计草案
> 交付等级：Design Contract
> 范围：集成 Gameplay、CharacterControl、Combat、Story、Camera、Rendering、UI Toolkit、DebugUI 八个模块的角色可玩测试场景

---

## 1. 目标

搭建一个 Unity 可运行场景，串联框架现有可直接使用的核心模块，验证角色移动 → 战斗 → 属性变化 → 受击反应 → 剧情驱动 → 表现层的完整闭环。

同时暴露未集成模块（CharacterAction 新层、CharacterGameplayRuntimeFoundation）的接入缺口，为后续桥接 PR 提供验收标准。

---

## 2. 模块集成全景图

```
InputCharacterCommandSource
         │
         ▼
CharacterControl v0（每帧）
┌──────────────────────────────────┐
│ CharacterCommand → StateMachine  │
│  → MotionResolver → CombatMotor  │──▶ CombatActionRunner
│  → ActionController              │──▶ GameplayCommandBuffer
│  → PressureReactionController    │
│  → AnimationPresentationCtrl     │──▶ MxAnimation Backend
│  → DebugSource                   │──▶ DebugUI
└──────────────────────────────────┘
         │
         ▼
RuntimeHost.Tick (RuntimeTickStage.Simulation)
┌──────────────────────────────────────────┐
│ StoryRuntimeModule (priority -100)       │
│  → Drain StoryCommandBuffer              │
│  → StoryDirector.Tick                    │
│  → StoryGameplayEffectBridge             │
│    → enqueue GameplayCommandBuffer       │
├──────────────────────────────────────────┤
│ GameplayRuntimeModule (priority 100)     │
│  → Drain GameplayCommandBuffer           │
│  → GameplaySystemPipeline                │
│    → attribute / ability / buff / spawn  │
│    → pressure systems                    │
└──────────────────────────────────────────┘
         │
         ▼
CharacterPressureReactionController（消费 Gameplay events）
Camera → MxCameraUnityRig → LateUpdate
UI → UI Toolkit HUD (MxStatBar / MxCommandButton / Story dialog)
Rendering → MxRenderingPipelineFeature (URP)
```

---

## 3. 场景内容

### 3.1 实体

| 实体 | 数量 | 说明 |
|------|------|------|
| 玩家角色 | 1 | 可操控，有完整属性/Buff/冷却/压力 |
| 敌人木桩 | 1~3 | 可被攻击，有属性/压力，有简单 AI 定时反击 |
| 地面 | 1 | 碰撞平面 |
| 相机 | 1 | 第三人称跟随 |

### 3.2 玩家能力

| 能力 | 输入 | 系统 |
|------|------|------|
| 移动（WASD） | 方向键 | CharacterMotionResolver + CombatKinematicMotor |
| 奔跑（Shift） | 按住 | CharacterMotionSettings |
| 跳跃（Space） | 按下 | MotionResolver → Jump impulse |
| 轻攻击（J） | 按下 | CombatActionRunner（有 Startup/Active/Recovery） |
| 格挡（L） | 按住 | GuardPressure 累积 |
| 技能1（Q） | 按下 | GameplayComponentAbilityCommandSystem |

### 3.3 敌人行为

| 行为 | 方式 |
|------|------|
| 空闲 | 站定，可被攻击 |
| 受击反应 | PressureSystem → CharacterPressureReactionController |
| 被击败判定 | HP ≤ 0 → Gameplay 事件 |
| 定时反击 | 简单 CombatAction 自动释放 |

### 3.4 Story 驱动（Phase 1 实际闭环）

Story 的定位：**记录战斗结果、驱动 UI 反馈、管理敌人波次配置**。Phase 1 不做 GameplayBridge 生成实体（当前不支持 spawn command），改为：

| 触发条件 | Story 效果 | 是否当前可行 |
|----------|-----------|-------------|
| 进入场景 | Story 载入 → Blackboard 写入初始事实 → UI 显示任务目标 | ✅ `StoryDirector` + `StorySetFact` |
| 玩家击败一个敌人 | Test code 或简单 adapter 调用 `StoryRuntimeCommandFactory.RaiseTrigger` → Director 推进 beat → Blackboard 更新"敌人击败数" | ✅ `StoryTriggerZoneAdapter` 模式（由组合根调用，不走 trigger collider） |
| 击败所有敌人 | Blackboard 事实变化 → Director 进入完成 beat → UI 显示胜利 | ✅ `StoryDirectorSnapshot` 供 UI 读取 |

**Phase 1 不做**：Story 通过 GameplayBridge 生成新实体、Gameplay 事件反向 enqueue Story trigger（这两个都需要后续独立桥接）。

### 3.5 HUD 显示

| 元素 | 数据源 |
|------|--------|
| HP 条 | GameplayAttributeSetComponent |
| 技能冷却 | GameplayAbilityCooldownComponent |
| 姿态压力条 | GameplayPosturePressureComponent |
| 状态标签 | CharacterControlState (Locomotion/Action/Reaction) |
| 事件日志 | RecentEvents |
| 任务/对白 | StoryDirectorSnapshot |
| 帧数/Hash | RuntimeFrame + RuntimeHash |

### 3.6 DebugUI

| 面板 | 数据源 |
|------|--------|
| CharacterControl 诊断 | CharacterControlDebugSource |
| Gameplay Component World | GameplayComponentWorldDebugSource |
| Event Timeline | GameplayRuntimeEventTimelineDebugSource |
| Entity Watch | GameplayComponentWorldEntityWatchDebugSource |
| Combat 诊断 | CombatDebugSnapshotDebugSource |
| Story 快照 | StoryRuntimeModule snapshot |
| 性能计数器 | FrameworkPerformanceCounterDebugSource |

---

## 4. 运行循环（每帧）

```
1. 采集输入（InputService → InputSnapshot）
2. CharacterControl 层
   a. InputCharacterCommandSource → CharacterCommand
   b. CharacterMotionResolver.Step → 移动
   c. CharacterActionController.Submit → 动作/技能请求
3. RuntimeHost.Tick
   a. StoryRuntimeModule (priority -100)
      - Drain StoryCommandBuffer
      - StoryDirector.Tick
      - StoryGameplayEffectBridge → GameplayCommandBuffer
   b. GameplayRuntimeModule (priority 100)
      - Drain GameplayCommandBuffer
      - GameplaySystemPipeline
   c. PostSimulation / Diagnostics stage
4. CharacterControl 反应处理
   a. CharacterPressureReactionController（消费 Gameplay events）
5. 动画表现
   a. CharacterAnimationPresentationController
6. Camera（MxCameraUnityRig.LateUpdate）
7. UI 刷新（HUD + DebugUI）
8. Replay recorder / Hash contributor
```

---

## 5. 项目文件结构

```
Assets/
├── Scenes/
│   └── CharacterTestScene.unity
├── Scripts/
│   └── MxFramework/
│       └── Demo/
│           ├── CharacterTest/
│           │   ├── CharacterTestDemo.cs          (MonoBehaviour)
│           │   ├── CharacterTestSlice.cs          (noEngine 组合根)
│           │   ├── CharacterTestStoryFixture.cs   (Story graph 定义)
│           │   └── CharacterTestHud.cs            (UI Toolkit 绑定)
│           └── Editor/
│               └── CreateCharacterTestScene.cs    (场景生成菜单，在 MxFramework.Demo.Editor 内)
├── UI/
│   └── MxFramework/
│       └── CharacterTest/
│           ├── CharacterTestHud.uxml
│           ├── CharacterTestHud.uss
│           └── CharacterTestPanelSettings.asset
└── Config/
    └── MxFramework/
        └── CharacterTest/
            └── character_test_actions.story.json     (Story graph)
```

说明：`CreateCharacterTestScene.cs` 放 `Demo/Editor/`（现有 `MxFramework.Demo.Editor.asmdef`），不放在 `Demo/CharacterTest/Editor/`（避免新建 asmdef 或误入 runtime 编译）。

---

## 6. 实施阶段

### Phase 1 — 核心闭环（最小可玩）

**目标**：玩家可以移动、攻击敌人、敌人受击反应、击败后通过 Story 驱动 UI 反馈。

**实际可用的 Story→Gameplay 路径**：
- Story → `StoryGameplayEffectBridge` 支持：`SetComponentAttribute`、`AddComponentAttribute`、`CastComponentAbility`
- **不支持**：spawn entity、add buff/remove buff
- Gameplay → Story：需要组合根显式调用 `StoryRuntimeCommandFactory.RaiseTrigger`，当前无通用 adapter

因此 Phase 1 的闭环是：

```
玩家攻击 → 敌人 HP/压力变化 → HP ≤ 0
  → 组合根检测到敌人死亡（Gameplay event 或定期检查）
  → 调用 StoryRuntimeCommandFactory.RaiseTrigger
  → StoryDirector 推进 beat → Blackboard 更新
  → UI 显示任务进度/胜利
```

**任务**：

| 任务 | 依赖 | 涉及模块 |
|------|------|---------|
| 创建场景 `CharacterTestScene.unity` | — | Unity Editor |
| 创建 `CharacterTestSlice` 组合根 | — | Runtime, Gameplay, CharacterControl, Combat |
| 创建 `CharacterTestDemo` MonoBehaviour | Slice | Demo, Input, UI Toolkit |
| 集成玩家移动 + 跳跃 + 攻击 | Slice | CharacterControl v0, Combat |
| 集成敌人 Gameplay entity + 属性 + 压力 | Slice | GameplayComponentWorld |
| 集成受击反应（压力 → 受击动作） | 敌人 entity | CharacterPressureReactionController |
| 集成 Story 基础驱动（触发 + beat + UI） | Slice | Story.Runtime |
| 集成第三人称相机 | Slice | MxCamera |
| 创建 `CreateCharacterTestScene.cs` Editor 菜单 | — | MxFramework.Demo.Editor |

**验收标准**：

- WASD 移动、跳跃、攻击可操作
- 攻击敌人 → 压力累积 → 破韧 → 受击反应
- 击败敌人 → Story trigger → UI 显示进度/胜利
- HUD 显示 HP、压力、状态
- Console 无 error

### Phase 2 — 表现完善 + Spawn 闭环

**目标**：动画、音效、完整 HUD、DebugUI；补上 Gameplay→Story adapter 和 Story→spawn 能力后完善波次循环。

| 任务 | 涉及模块 |
|------|---------|
| 集成 MxAnimation 移动 blend + 攻击/受击动画 | Animation, CharacterControl.Animation |
| 集成 Audio 攻击音效/受击音效 | Audio |
| 完善 HUD（技能冷却、Buff 图标、事件日志） | UI Toolkit |
| 集成 DebugUI overlay | DebugUI |
| 集成 Rendering Feature 验证 | Rendering |
| 集成 Gameplay→Story trigger adapter（Gameplay event 自动 enqueue Story trigger） | Story, Gameplay |
| 集成 Story→spawn（补齐 spawn command 后使能波次生成） | Story.GameplayBridge, Gameplay |

### Phase 3 — 新层桥接（可选）

**目标**：验证 CharacterAction 新层的集成。

| 任务 | 涉及模块 |
|------|---------|
| CharacterControl → CharacterAction 桥接 PR | CharacterAction, CharacterControl |
| CharacterGameplayRuntimeFoundation 接入 | Character.RuntimeSpawn |
| CharacterAction Workstation → DebugUI | CharacterAction, DebugUI |

---

## 7. 当前已有可复用资产

| 资产 | 路径 |
|------|------|
| CharacterControl Playable 场景 | `Assets/Scenes/CharacterControlPlayable.unity` |
| CharacterControl Playable 逻辑 | `Assets/Scripts/MxFramework/Demo/CharacterControl/CharacterControlPlayableSlice.cs` |
| CharacterControl Playable Demo | `Assets/Scripts/MxFramework/Demo/CharacterControl/CharacterControlPlayableDemo.cs` |
| CharacterControl UI | `Assets/UI/MxFramework/CharacterControl/CharacterControlPlayableDemo.uxml` |
| Combat CharacterControl Slice | `Assets/Scripts/MxFramework/Demo/Combat/RuntimeCombatCharacterControlSlice.cs` |
| Gameplay Component Showcase | `Assets/Scenes/GameplayComponentRuntimeShowcase.unity` |
| Story Runtime 场景 | `Assets/Scenes/StoryRuntimeVerticalSlice.unity` |
| Story Runtime Demo | `Assets/Scripts/MxFramework/Demo/Story/StoryRuntimeVerticalSliceDemo.cs` |
| Story Runtime 切片 | `Assets/Scripts/MxFramework/Demo/Story/StoryRuntimeVerticalSliceRunner.cs` |
| StoryGameplayEffectBridge | `Assets/Scripts/MxFramework/Story.GameplayBridge/StoryGameplayEffectBridge.cs` |
| MxAnimation Showcase | `Assets/Scenes/MxAnimationSystemShowcase.unity` |
| 可复用 UI 控件 | MxStatBar, MxCommandButton, MxStatusBadge, MxEventLog |

---

## 8. 不包含的内容（第一版 Scope）

- ❌ CharacterAction 新层（Resolver/Runner/TrackAdapter）
- ❌ CharacterGameplayRuntimeFoundation
- ❌ Story → spawn 实体（待后续显式 spawn command bridge）
- ❌ Gameplay → Story 自动 adapter（Phase 2 补齐）
- ❌ WGame 真实角色数据或业务逻辑
- ❌ 网络、多人、回滚
- ❌ Timeline / Cinemachine package-specific binding
- ❌ SaveState / Replay（框架已有，但不作为测试场景主力验证目标）
