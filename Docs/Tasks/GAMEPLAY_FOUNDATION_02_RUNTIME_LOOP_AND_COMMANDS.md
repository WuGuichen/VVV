# Gameplay Foundation 02：Runtime Loop and Commands

> 状态：Implemented v0（2026-05-11）

## 目标

把 `MxFramework.Gameplay` 从“可手动调用的纯 C# Ability / World 库”推进到 Runtime 主线：`RuntimeHost -> RuntimeCommandBuffer -> GameplayRuntimeModule -> GameplayWorld -> RuntimeEventQueue`。

## 范围

- 新增 `GameplayRuntimeModule`，可注册为 `RuntimeModule`。
- 新增 Gameplay command id 和 factory，避免调用方手写 `RuntimeCommand` payload 位序。
- v0 支持 `CastAbility` 和 `DespawnEntity` command。
- Module 每帧 drain `RuntimeCommandBuffer`，按 buffer 稳定排序执行 command。
- `RuntimeCommandBuffer` 是单 drain owner 资源：Gameplay module 持有的 buffer 只允许该 module 调用 `DrainForFrame`；其他模块可以 enqueue command。
- Module 默认自动 tick `GameplayWorld`，也允许关闭自动 tick。
- 新增 `GameplayRuntimeEvent`，通过 `RuntimeEventQueue<GameplayRuntimeEvent>` 输出 ability result、despawn、rejected command 和 world tick。
- Ability cast result 只保留最近 N 条诊断结果，默认 `64`，避免长时间运行无限增长。

## 不做

- 不做完整 Gameplay SaveState restore。
- 不做 cooldown、cost、cast time、interrupt。
- 不做 Combat hit bridge、AI decision bridge、projectile、physics range 或公式 DSL。
- 不让 Gameplay 依赖 Unity、Demo、Config.Runtime、Combat 或 WGame 私有数据。

## Command Payload v0

| Command | CommandId | Payload |
| --- | --- | --- |
| `CastAbility` | `GameplayRuntimeCommandIds.CastAbility` | `payload0=casterEntityId`、`payload1=abilityId`、`payload2=optional single candidateEntityId` |
| `DespawnEntity` | `GameplayRuntimeCommandIds.DespawnEntity` | `payload0=entityId` |

后续如果需要多目标、复杂 payload 或 schema 展示，应接入 `RuntimeCommandRegistry`，不要继续扩张裸 int payload。

## Command Buffer Ownership

`RuntimeCommandBuffer.DrainForFrame(frame)` 会推进 buffer 的 `CurrentFrame`。因此一个 buffer 必须只有一个 drain owner。

`GameplayRuntimeModule` 是它持有的 command buffer 的 drain owner。Input adapter、AI、TimerScheduler、SceneFlow 或 Demo 代码可以向这个 buffer `Enqueue` command，但不应调用 `DrainForFrame`。如果其他系统需要独立消费 command，应使用独立 buffer 或在上层组合根明确转发。

## 事件

`GameplayRuntimeEvent` 包含：

- `Frame`
- `Type`
- `CommandId`
- `CasterEntityId`
- `AbilityId`
- `TargetEntityId`
- `FailureCode`
- `Reason`
- `TraceId`

事件队列是 UI / Audio / Diagnostics / Replay 的边界。即时 `EventBus` 仍可用于局部模块内同步通知，但不应作为跨模块、跨帧的唯一事件来源。

## 测试

- `GameplayRuntimeModuleTests.Tick_DrainsCastAbilityCommandAndEmitsStableEvents`
- `GameplayRuntimeModuleTests.Tick_CastAbilityFailureEmitsStructuredFailureEvent`
- `GameplayRuntimeModuleTests.Tick_DespawnEntityCommandRemovesEntity`
- `GameplayRuntimeModuleTests.RuntimeHost_TicksGameplayModuleAfterEarlierModules`
- `GameplayRuntimeModuleTests.AbilityResults_KeepRecentResultsAndCanBeCleared`

## 验收

- `MxFramework.Gameplay` 仍为 `noEngineReferences=true`。
- `dotnet build MxFramework.Gameplay.csproj --no-restore` 通过。
- 新增源码级 / EditMode 测试覆盖 command loop 和 event queue。
