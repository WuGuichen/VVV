# Tetris Runtime Validation 01

> 状态：Implemented / Verified
> 日期：2026-05-11
> 优先级：P0
> 前置：`RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`、`RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md`

## 目标

用一个纯 C# Tetris 规则切片验证 Runtime Foundation 的确定性链路：

- RuntimeHost 固定帧推进。
- RuntimeCommandBuffer 接收输入命令。
- RuntimeReplayRecorder 记录每帧 command 和 result hash。
- RuntimeReplayPlaybackRunner 能用同一 replay 得到相同 hash。
- RuntimeSaveStateJson 能 roundtrip Tetris 自定义 module state，并恢复出同一局面 hash。

本任务是 validation fixture，不是产品级小游戏。

## 范围

### 做

- 10x20 board。
- 7 种 tetromino：I/O/T/S/Z/J/L。
- 固定 piece 队列，避免随机源影响确定性。
- 输入命令：Left、Right、RotateClockwise、SoftDrop、HardDrop。
- Gravity 每 N 帧下落一格，N 由配置指定。
- Lock、行消除、score/lines/locked piece 计数。
- 稳定 snapshot / diagnostics / hash。
- Replay playback 成功和故意 hash mismatch 失败测试。
- SaveState roundtrip + restore 测试。
- 一个最小 Unity PlayMode 场景，用键盘输入驱动同一套 Runtime 验证 runner。

### 不做

- 不做 SRS wall kick。
- 不做 hold、ghost、next queue UI、音效、计分表。
- 不做 7-bag 随机；固定序列更适合作为 golden replay fixture。
- 不修改 RuntimeHost / Command / Replay / SaveState 公共语义。
- 不做产品级 UI；当前 PlayMode 入口只作为框架验证和手动试玩。

## 建议目录

```text
Assets/Scripts/MxFramework/Demo/Tetris/
  TetrisGame.cs
  TetrisRuntimeValidation.cs
  TetrisPlayableDemo.cs

Assets/Scripts/MxFramework/Tests/Demo/Tetris/
  TetrisRuntimeValidationTests.cs

Assets/Scenes/
  TetrisRuntimeValidation.unity
```

## 核心设计

### TetrisGame

`TetrisGame` 是纯 C# 规则核心：

- `ApplyCommand(TetrisCommand command)` 只处理输入意图。
- `TickGravity()` 按当前 active piece 下落或 lock。
- `HardDrop()` 立即落到底并 lock。
- `CaptureSnapshot()` 返回不可变 snapshot，包含 board、active piece、score、lines、gameOver。
- `ComputeStableHash(RuntimeFrame frame)` 用 RuntimeHashAccumulator 生成确定性 hash。

### TetrisRuntimeValidationRunner

封装 Runtime 机制：

- 内部持有 `RuntimeHost`、`RuntimeCommandBuffer`、`RuntimeReplayRecorder`、`TetrisRuntimeModule`。
- `EnqueueCommand(frame, command)` 将 Tetris 输入映射为 RuntimeCommand。
- `TickFrame(frame)` drain command、推进 Tetris、记录 replay frame。
- `CreateReplaySnapshot()` 输出 replay。
- `CaptureSaveState()` / `RestoreSaveState()` 使用 Runtime module custom state。

## 验收

- Tetris core 不引用 UnityEngine / UnityEditor。
- 给定固定 command 序列，两次运行最终 hash 一致。
- Replay playback 对原始 snapshot 成功。
- 修改任一 frame expected hash 后 playback 返回 hash mismatch。
- SaveState JSON roundtrip 后，新 runner 恢复出的 hash 与原 runner 相同。
- `dotnet build MxFramework.Tests.csproj --no-restore` 通过，或在 Unity csproj 未刷新时用临时编译覆盖新增源码。

## 当前实现记录

已落地文件：

- `Assets/Scripts/MxFramework/Demo/Tetris/TetrisGame.cs`
- `Assets/Scripts/MxFramework/Demo/Tetris/TetrisRuntimeValidation.cs`
- `Assets/Scripts/MxFramework/Demo/Tetris/TetrisPlayableDemo.cs`
- `Assets/UI/MxFramework/Tetris/TetrisPlayableDemo.uxml`
- `Assets/UI/MxFramework/Tetris/TetrisPlayableDemo.uss`
- `Assets/Scenes/TetrisRuntimeValidation.unity`
- `Assets/Scripts/MxFramework/Tests/Demo/Tetris/TetrisRuntimeValidationTests.cs`

实现边界：

- Tetris 规则核心保持纯 C#，不引用 UnityEngine / UnityEditor。
- `TetrisRuntimeModule` 作为 RuntimeHost module 负责 drain command、推进 Tetris、记录 replay hash、提供 SaveState custom state。
- `TetrisReplayFrameDriver` 驱动 `RuntimeReplayPlaybackRunner` 复放 replay snapshot。
- `TetrisPlayableDemo` 是 Unity UI Toolkit Demo 外壳，键盘输入先转成 RuntimeCommand，再由 RuntimeHost tick 推进 Tetris。
- Playable Demo 使用 `UIDocument` + UXML/USS + cached VisualElement cells，不再使用 `OnGUI`；同时设置 `Application.targetFrameRate = 60`。
- SaveState 使用 Runtime custom state 存放 Tetris JSON payload，经 `RuntimeSaveStateJson` 完成 JSON roundtrip。

验证记录：

- `dotnet build MxFramework.Tests.csproj --no-restore`：通过，0 error；当前输出仍包含既有 Demo serialized field warnings。
- `dotnet run --project /tmp/mx-tetris-source-smoke-*/TetrisSourceSmoke.csproj`：通过，覆盖 replay playback success、final hash matching、SaveState JSON payload、SaveState JSON restore matching。
- `rg -n "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Demo/Tetris Assets/Scripts/MxFramework/Runtime`：无命中。
- Unity MCP 创建并保存 `Assets/Scenes/TetrisRuntimeValidation.unity`，进入 Play Mode 后 Console error / warning 为 0；UI Toolkit visual tree 中 `tetris-root`、`tetris-board` 和 200 个 cell 已创建。

剩余风险：

- 这是 validation fixture，不包含 SRS wall kick、hold/ghost/next UI 或随机 7-bag。
