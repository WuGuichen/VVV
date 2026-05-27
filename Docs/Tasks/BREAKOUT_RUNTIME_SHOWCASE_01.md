# Breakout Runtime Showcase 01

> 状态：Runtime Validation Implemented / Verified；Playable 场景资产未提交
> 日期：2026-05-11
> 优先级：P0
> 前置：`APP_SCENE_FLOW_01_FOUNDATION.md`、`TETRIS_RUNTIME_VALIDATION_01.md`
>
> 现状校准（2026-05-14）：当前仓库没有 Breakout 场景资产。本任务文档保留 Breakout runtime、AppFlow、SceneFlow 和 UI Toolkit 试玩层代码的设计记录，但能力状态以 Runtime Validation 为准，不能标记为 Playable。

## 目标

用一个 Breakout / 打砖块小游戏补足 Tetris 未覆盖的框架验证面：

- AppFlow：Boot / Menu / Loading / Gameplay / GameOver 状态流。
- SceneFlow：从启动场景进入 Gameplay 场景，并提供 busy / progress / result 诊断。
- RuntimeHost：固定帧推进 Breakout 规则。
- RuntimeCommandBuffer：发球、左右移动、暂停、重开输入进入 runtime command。
- Replay / Hash：固定输入序列可回放并得到稳定 result hash。
- SaveState：中局保存并恢复球、球拍、砖块、分数、生命和道具状态。
- UI Toolkit：菜单、HUD、GameOver 面板。

## 范围

### 做

- 纯 C# Breakout 规则核心。
- 固定尺寸 playfield、paddle、ball、brick grid。
- 命令：MoveLeft、MoveRight、Launch、Pause、Restart。
- AABB 碰撞：墙、paddle、bricks。
- 分数、生命、胜负状态。
- 一个能力/道具验证点：Wide Paddle 或 Slow Ball，走简单状态计时。
- Runtime module 封装 command drain、simulation tick、replay recorder、save state custom payload。
- EditMode 测试覆盖 core、replay hash、save state roundtrip。
- Unity UI Toolkit 试玩层代码：主菜单 -> loading -> gameplay -> game over；提交 Playable 状态前仍需要补齐 Unity 场景资产。

### 不做

- 不用 Unity Physics 作为第一版规则核心。
- 不做复杂关卡编辑器。
- 不做粒子、动画、商业化美术。
- 不引入 Addressables；资源预热先用框架 Resource API 可替换接口或本地占位。
- 不修改 RuntimeHost / Command / Replay / SaveState 公共语义。

## 目录

```text
Assets/Scripts/MxFramework/Demo/Breakout/
  BreakoutGame.cs
  BreakoutRuntimeValidation.cs
  BreakoutAppFlowDemo.cs
  BreakoutPlayableDemo.cs

Assets/Scripts/MxFramework/Tests/Demo/Breakout/
  BreakoutRuntimeValidationTests.cs

Assets/UI/MxFramework/Breakout/
  BreakoutPlayableDemo.uxml
  BreakoutPlayableDemo.uss
```

## 拆分任务

### Worker A：Breakout 纯 C# Runtime 核心

负责文件：

- `Assets/Scripts/MxFramework/Demo/Breakout/BreakoutGame.cs`
- `Assets/Scripts/MxFramework/Demo/Breakout/BreakoutRuntimeValidation.cs`
- `Assets/Scripts/MxFramework/Tests/Demo/Breakout/BreakoutRuntimeValidationTests.cs`

验收：

- Core 文件不引用 `UnityEngine` / `UnityEditor`。
- 无输入时球按固定速度运动并反弹。
- paddle 命令影响 paddle x。
- 打砖块会移除 brick、增加 score。
- 掉出底部会扣 life 并 reset ball。
- Replay playback success 和 hash mismatch 测试。
- SaveState JSON roundtrip 恢复同一局面 hash。

### Worker B：Unity UI / AppFlow Showcase

负责文件：

- `Assets/Scripts/MxFramework/Demo/Breakout/BreakoutAppFlowDemo.cs`
- `Assets/Scripts/MxFramework/Demo/Breakout/BreakoutPlayableDemo.cs`
- `Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uxml`
- `Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uss`

验收：

- 使用 UI Toolkit，不使用 `OnGUI`。
- Boot/Menu/Loading/Gameplay/GameOver 状态由 AppFlow 表达。
- Gameplay 输入通过 Breakout runtime command 进入 simulation。
- UI 显示 score / lives / state / scene flow progress。
- Game View 下 Play Mode console 无 error。

## 总管验收

- `dotnet build MxFramework.Tests.csproj --no-restore` 通过。
- Unity 刷新后 Console 无 Breakout 编译错误。
- 当前仓库缺少 Breakout 场景资产；补齐场景前不以 Play Mode 入口作为当前验收条件。
- `git diff --stat && git diff --check` 风险符合 Demo 范围。
- 更新 `Docs/CAPABILITIES.md` 和 `Docs/README.md` 的 Demo 入口。

## 当前实现

- 纯 C# 规则核心：`Assets/Scripts/MxFramework/Demo/Breakout/BreakoutGame.cs`
- Runtime 验证夹具：`Assets/Scripts/MxFramework/Demo/Breakout/BreakoutRuntimeValidation.cs`
- EditMode 测试：`Assets/Scripts/MxFramework/Tests/Demo/Breakout/BreakoutRuntimeValidationTests.cs`
- Unity 试玩层：`Assets/Scripts/MxFramework/Demo/Breakout/BreakoutPlayableDemo.cs`
- App / Scene Flow 展示：`Assets/Scripts/MxFramework/Demo/Breakout/BreakoutAppFlowDemo.cs`
- UI Toolkit 资产：`Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uxml`、`Assets/UI/MxFramework/Breakout/BreakoutPlayableDemo.uss`
- 场景入口：当前仓库未提交 Breakout 场景资产，因此该切片当前只能标记为 Runtime Validation，不能标记为 Playable。

边界：

- `BreakoutGame.cs` / `BreakoutRuntimeValidation.cs` 不引用 `UnityEngine` / `UnityEditor`。
- 试玩层代码使用 UI Toolkit，不使用 `OnGUI`。
- v0.1 默认用 timed SceneFlow driver 展示 loading / progress / result 诊断；Unity SceneManager driver 保留为可切换实现，避免试玩入口对象在单场景加载时被卸载。
- v0.2：未发球时球会在球拍上左右滚动；发球时按球相对球拍中心的位置决定水平速度和发射方向。
- v0.3：新增 3 个内建关卡、Normal/Strong/Unbreakable/PowerUp 砖块类型、砖块 HP、Wide/Slow/Multi/ExtraLife/Laser 道具、多球动态实体、下一关流程和反馈事件；SaveState payload 升级到 v2，保存砖块类型/HP/道具、多球、关卡和事件计数。

## 验证记录

- `dotnet build MxFramework.Tests.csproj --no-restore` 通过。
- Unity refresh / compile 通过，无 Breakout 编译错误。
- 历史 Play Mode 记录引用过 Breakout 场景资产，但当前仓库缺少该场景文件；以 `Assets/Scenes/` 实际内容和 `CAPABILITIES.md` 当前能力状态为准。
- Play Mode runtime bridge 检查：`runtimeReady=True score=0 lives=3 bricks=32`。
- Play Mode AppFlow 手动推进检查：`state=Menu pending=False tick=4 last=None target=Menu`。
- Breakout 边界检查：核心 runtime 文件无 Unity 引用，试玩层无 `OnGUI` / `GUILayout` / `GUI.*`。
- v0.2 新增测试覆盖未发球滚动和按滚动位置决定发球方向。
- v0.3 新增测试覆盖关卡配置中的砖块类型/道具，以及 MultiBall 动态球快照。
