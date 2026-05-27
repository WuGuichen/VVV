# Runtime Foundation System

> Status: Design
> Version: 0.1
> Date: 2026-05-10
> Planning state: Runtime foundation roadmap.
> 目标：把 MxFramework 从“模块可用”推进到“可承载真实游戏主循环、回放和存档”的运行时底座。

## 背景

当前框架已经具备 Attributes、Buffs、Modifiers、Config、Resources、Gameplay、Combat 和 Runtime Showcase。缺口不在单点功能，而在运行时编排层：

```text
Bootstrap / Host
  -> Module Lifecycle
  -> Frame Clock
  -> Command Dispatch
  -> Gameplay / Combat / Resources Tick
  -> Diagnostics / Replay
  -> Save State
```

如果没有这一层，项目接入时会在自己的 `MonoBehaviour`、场景脚本、测试 runner 和外部预览服务里重复装配模块。重复装配会带来四类风险：

- 生命周期顺序不一致。
- 同一帧输入、战斗、Buff Tick、事件派发的顺序不可追踪。
- Bug 复现只能依赖人工步骤，难以稳定 Replay。
- Runtime 状态无法被版本化存档、诊断和 Mod/配置变更共享。

本系统规划拆成三条 P0 任务：

| 顺序 | 任务 | 产物 |
|------|------|------|
| 1 | Runtime Host / Composition Root | 标准组合根、模块注册、生命周期和 Tick 顺序 |
| 2 | Frame Clock / Command / Replay | 统一帧、命令输入、回放记录和确定性诊断 |
| 3 | Save State / Runtime Serialization | 运行时状态快照、存档契约、版本迁移和恢复语义 |

## 总体目标

建立一套不绑定具体游戏业务、不持有全局单例、可在 Unity / 测试 / 预览服务中复用的运行时基础设施。

完成后，项目层应该能这样接入：

```text
Game code creates host
  -> Registers framework modules
  -> Loads config/resource catalogs
  -> Starts runtime session
  -> Sends commands per frame
  -> Ticks with explicit frame/time
  -> Captures replay/save/diagnostics
  -> Stops and disposes cleanly
```

## 设计原则

- Host 是组合根，不是全局单例。
- Runtime 权威时间由外部传入，不自行读取 `Time.deltaTime`。
- 命令是输入事实，不直接等同于业务行为。
- Replay 记录输入和关键结果摘要，不记录每个私有字段。
- SaveState 记录可恢复的玩家运行时状态，不记录临时调试对象。
- 框架提供通用契约，游戏层负责业务 ID、具体配置、实体语义和表现层。
- 第一版优先支撑单机、测试、预览和 Bug 复现；不实现真实联网协议。

## 非目标

- 不做完整网络同步协议。
- 不做项目级场景管理器。
- 不做背包、任务、成就、剧情等业务存档。
- 不迁移 WGame 真实存档数据。
- 不强行接入 Entitas、Luban、Addressables 或第三方 DI 容器。
- 不把 `RuntimeVerticalSliceRunner` 变成框架核心 API。

## 模块关系

建议新增或扩展的模块边界：

```text
Core
  <- Runtime.Foundation
      - Host
      - Lifecycle
      - Time / Frame
      - Command
      - Replay
      - SaveState contracts

Runtime.Foundation
  <- Gameplay
  <- Combat
  <- Resources
  <- Preview.Runtime
  <- Demo / Editor / Tests
```

第一版可先放在 `MxFramework.Gameplay` 或新增 `MxFramework.Runtime` 中验证 API。若 API 稳定且被 Combat / Preview / Demo 共同使用，再拆出独立 asmdef：

```text
Assets/Scripts/MxFramework/Runtime/
Assets/Scripts/MxFramework/Tests/Runtime/
MxFramework.Runtime
```

拆分判断：

- 只有 Gameplay 使用：先留在 Gameplay。
- Gameplay、Combat、Preview 均需要：拆到 Runtime。
- 需要 noEngine 运行：不得依赖 `UnityEngine`。

## 数据流

```text
External Input / Test Script / Preview RPC
  -> RuntimeCommandBuffer
  -> RuntimeFrameRunner.Tick(frame)
  -> Command Dispatch
  -> Gameplay / Combat module tick
  -> EventBus
  -> Diagnostics Snapshot
  -> Replay Frame Record
  -> Optional SaveState Capture
```

推荐帧内顺序：

1. BeginFrame。
2. Drain commands。
3. Apply config/resource changes queued before frame。
4. Tick simulation modules。
5. Dispatch end-of-frame events。
6. Capture diagnostics/replay hashes。
7. EndFrame。

具体模块可选择加入哪些阶段，但顺序必须由 Host 可观测。

## 任务入口

| 文档 | 说明 |
|------|------|
| `Tasks/RUNTIME_FOUNDATION_01_RUNTIME_HOST.md` | 组合根、模块生命周期、Tick 分组、错误处理和 Demo 接入 |
| `Tasks/RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md` | FrameClock、CommandBuffer、ReplayRecorder、hash 和调试输出 |
| `Tasks/RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md` | SaveState 契约、序列化、版本迁移、恢复策略和测试 |

## 推荐推进顺序

### M1：Host Skeleton

先建立最小 Host，不接复杂业务：

- 注册模块。
- `Initialize / Start / Tick / Stop / Dispose`。
- 固定 Tick 顺序。
- 结构化错误。
- 测试纯 C# 生命周期。

### M2：Frame + Command

在 Host 上加入显式 frame：

- `RuntimeFrame`。
- `RuntimeClock`。
- `RuntimeCommand`。
- 每帧 command drain。
- command validation。

### M3：Replay

让 Runtime Showcase 和 Combat 可记录输入：

- frame input log。
- result hash。
- replay playback。
- desync dump。

### M4：SaveState Contract

定义可保存内容：

- Entity runtime state。
- Attribute base/final 或恢复策略。
- Active Buff / Modifier state。
- Ability runtime state。
- Counters。
- Config/resource references。

### M5：Save / Load Vertical Slice

在 Runtime Showcase 中验证：

- 打技能。
- 挂 Buff。
- 改 HP。
- 保存。
- reset。
- 读取。
- 状态恢复并继续 Tick。

## 验收总线

三条任务全部完成后，必须能证明：

- 同样输入命令序列能跑出同样 replay hash。
- Runtime Host 的生命周期错误可诊断。
- Runtime Showcase 能从保存点恢复属性、Buff、Modifier、Ability 状态。
- Preview Server 可以复用 Host 而不是手写一套运行时装配。
- SaveState 版本不匹配时返回结构化错误，不静默吞掉字段。
