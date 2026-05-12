# Marble Maze Framework Physics Showcase 01

> 状态：Playable，已重构为框架物理权威，Unity scene asset 已由 Editor 生成
> 日期：2026-05-11

## 目标

用 Marble Maze 验证 MxFramework Demo 在“框架物理作为玩法权威”的场景下遵守 Runtime Foundation：

- `RuntimeHost` / `IRuntimeModule` / `RuntimeFrame` 负责 runtime tick。
- Unity 输入、UI Button 只转换为 `RuntimeCommandBuffer` 命令。
- 球体移动、墙体阻挡、宝石拾取和出口判定必须由框架 runtime / physics 模块推进。
- Unity `Transform`、Mesh、Material、TextMesh、UI Toolkit 只做显示；不得用 `Rigidbody`、`Collider`、trigger 或 `UnityEngine.Physics` 作为玩法权威。
- Runtime 负责 tilt 命令、计时、checkpoint 目标状态、diagnostics summary、stable hash、Replay playback 和 SaveState JSON roundtrip。
- 正式 HUD 使用 UI Toolkit UXML / USS，不新增 OnGUI 入口。
- 多状态入口使用 `AppFlowController` 的单场景简化适配。

## API 复用计划

| 需求点 | 使用模块 | 状态 | 说明 |
| --- | --- | --- | --- |
| 主循环 / 固定帧 | `RuntimeHost`、`IRuntimeModule`、`RuntimeFrame` | 使用 | `MarbleMazeRuntimeModule` 是唯一 runtime 规则推进入口。 |
| 输入 / UI 指令 | `RuntimeCommandBuffer`、`RuntimeCommand`、`InputSnapshot` | 使用 | Unity 输入和 UI 回调只入队 Tilt / Reset / Pause / Save / Load 相关命令或 SaveState 操作。 |
| 回放 / Hash | `RuntimeReplayRecorder`、`RuntimeReplayPlaybackRunner`、`IRuntimeHashContributor` | 使用 | EditMode 测试覆盖 replay 成功和 hash mismatch。 |
| 存档 / 恢复 | `IRuntimeSaveStateProvider`、`IRuntimeSaveStateRestorer`、`RuntimeSaveStateJson` | 使用 | 测试覆盖 JSON roundtrip；HUD 提供 Save / Load 手测按钮。 |
| App / Scene Flow | `AppFlowController`、`AppFlowRuntimeModule` | 使用简化适配 | 单场景 Boot -> Menu -> Gameplay -> Finished，不手写 scene YAML。 |
| UI | UI Toolkit UXML / USS | 使用 | `Assets/UI/MxFramework/MarbleMaze/`。 |
| Framework Physics | `CombatPhysicsWorld.Query` / `ExplainQuery`、既有 Motion API | 必须使用 | 球体与墙、GEM、EXIT 的权威碰撞 / 查询必须走框架模块。 |
| Gameplay / Buff / Modifier / Config / Resources | 对应模块 | 不使用 | Marble Maze 无实体技能、数值 Buff、配置表或资源加载需求。 |

## 新增入口

- Runtime：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeRuntime.cs`
- Unity adapter/view：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazePhysicsDemo.cs`
- Framework physics adapter：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeFrameworkPhysicsWorld.cs`
- AppFlow adapter：`Assets/Scripts/MxFramework/Demo/MarbleMaze/MarbleMazeAppFlowDemo.cs`
- Scene generator：`Assets/Scripts/MxFramework/Demo/Editor/CreateMarbleMazeScenes.cs`
- Playable scenes：`Assets/Scenes/MarbleMazeBoot.unity` / `Assets/Scenes/MarbleMazeGameplay.unity`
- UI Toolkit：`Assets/UI/MxFramework/MarbleMaze/MarbleMazePlayableDemo.uxml` / `.uss`
- PanelSettings：`Assets/UI/MxFramework/MarbleMaze/MarbleMazePanelSettings.asset`
- Tests：`Assets/Scripts/MxFramework/Tests/Demo/MarbleMaze/MarbleMazeRuntimeTests.cs`

## 验收

- Playable scene 由 `MxFramework/Marble Maze/Create Playable Scenes` 生成，不手写 `.unity` YAML。
- `Assets/Scenes/MarbleMazeBoot.unity` 和 `Assets/Scenes/MarbleMazeGameplay.unity` 不得依赖 Unity `Rigidbody` / `Collider` / trigger 完成玩法；场景对象只作为 view。
- Unity Play Mode 冒烟：棋盘可见，右侧 HUD 可见，Console 清空后未再出现 missing script / theme warning。
- 输入链路使用框架 `DefaultInputService` / `InputSnapshot`；WASD / 方向键经 `RuntimeCommandBuffer` 转换为 tilt 命令。
- `RuntimeCommandBuffer_TiltAndPhysicsSampleUpdateRuntimeSnapshot`
- `CheckpointsMustArriveInOrderBeforeFinish`
- `RuntimeReplayPlayback_ReplaysMarbleMazeCommandsWithMatchingHashes`
- `RuntimeReplayPlayback_HashMismatchReportsMarbleMazeFrame`
- `RuntimeSaveStateJson_RoundtripRestoresMarbleMazeHashAndCheckpoint`
- `RuntimeCommandBuffer_RejectsUnknownMarbleMazeCommandIds`
- `FrameworkPhysicsWorld_UsesCombatPhysicsForCheckpointAndExitQueries`
- `FrameworkPhysicsWorld_ClampsBallAgainstCombatPhysicsWalls`

## 剩余风险

- `CombatPhysicsWorld` 当前提供确定性 query / AABB collider，不是完整刚体求解器；Marble Maze 的滚动积分和撞墙反弹由 Demo 适配层实现，再用框架 query 做权威碰撞 / 拾取 / 出口判定。
- 该 Demo 不再使用 Unity `Rigidbody` / `Collider` / trigger 作为玩法权威。
