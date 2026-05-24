# Debug UI 接口

## 职责

Debug UI 提供开发观察层：把多个 `IFrameworkDebugSource` 注册到普通 registry 中，刷新为只读 dashboard view model，并由可选 UI Toolkit overlay 展示。它不改变运行时权威状态，不执行 gameplay command，不把 UI 表现状态写入 Replay、SaveState 或 Runtime hash。

## 程序集

| 程序集 | 依赖 | noEngine | 职责 |
| --- | --- | --- | --- |
| `MxFramework.DebugUI` | `MxFramework.Diagnostics` | 是 | Source registry、snapshot aggregation、dashboard view model、timeline / entity watch view model、visibility state |
| `MxFramework.DebugUI.Toolkit` | `MxFramework.DebugUI`, `MxFramework.UI.Toolkit`, Unity UI Toolkit | 否 | Runtime overlay shell、Hidden / Collapsed / Expanded 绑定 |
| `MxFramework.DebugUI.Adapters` | `MxFramework.DebugUI` + 被观察 noEngine 模块 | 是 | RuntimeHost、Gameplay、Combat、CharacterControl 等 source adapter |
| `MxFramework.DebugUI.Input` | `MxFramework.DebugUI`, `MxFramework.DebugUI.Toolkit`, `MxFramework.Input` | 否 | 可选 Debug UI input bridge，消费 `IInputProvider` 和 Debug intents |

## 公开接口

| 类型 | 用途 |
|------|------|
| `FrameworkDebugSourceRegistry` | 普通对象 registry，按 ordinal source name 保证唯一 |
| `DebugUiSnapshotAggregator` | 从 registry 刷新 dashboard view model，隔离 source 异常 |
| `DebugUiDashboardViewModel` | Dashboard 根展示模型，包含 source、section、error 和 refresh sequence |
| `DebugUiVisibility` | Hidden / Collapsed / Expanded 表现状态 |
| `DebugUiTimelineEntryViewModel` | 事件时间线展示项，包含 frame、source、category、entityId、traceId 和 summary |
| `DebugUiTimelineFilter` | 事件时间线 source / entity / category 过滤条件 |
| `DebugUiTimelineViewModel` | 事件时间线集合，按 frame 和来源稳定排序并可限制最近条数 |
| `DebugUiEntityWatchEntryViewModel` | 实体观察展示项，包含 id、active、key attributes、pressure、guard、armor 和 summary |
| `DebugUiEntityWatchViewModel` | 实体观察集合，支持按 entity id 过滤 |
| `DebugUiObservabilityFormatter` | 将 timeline / entity watch view model 格式化为 snapshot section 文本 |
| `DebugUiOverlayController` | UI Toolkit runtime MonoBehaviour overlay shell |
| `DebugUiOverlayViewModelBinder` | 可测试的 UI Toolkit tree 构造和绑定器 |
| `RuntimeHostDebugSource` | 将 `RuntimeHost.CaptureDiagnostics()` 映射为 `FrameworkDebugSnapshot` |
| `GameplayDiagnosticSnapshotDebugSource` | 将 `GameplayDiagnosticSnapshot` 映射为 Debug UI source |
| `GameplayComponentWorldDebugSource` | 复用 `GameplayComponentWorldDiagnostics` 映射 component world 状态 |
| `CombatDebugSnapshotDebugSource` | 将 `CombatDebugSnapshot` 映射为 Debug UI source |
| `CharacterControlDebugSource` | 将 Character Control 状态、last command、motion、action、pressure 和 recent events 映射为 Debug UI source |
| `GameplayRuntimeEventTimelineDebugSource` | 将 `GameplayRuntimeEvent` 映射为 Timeline sections |
| `CombatTimelineDebugSource` | 将 `CombatDebugSnapshot` query / hit trace 映射为 Timeline sections |
| `GameplayComponentWorldEntityWatchDebugSource` | 将 component world entity、pressure、guard、armor 状态映射为 Entity Watch sections |
| `RuntimeHostPerformanceCounterSource` | 从 `RuntimeHostDiagnostics` 生成 RuntimeHost performance counters |
| `GameplayDiagnosticPerformanceCounterSource` | 从 `GameplayDiagnosticSnapshot` 生成 Gameplay performance counters |
| `CombatDebugPerformanceCounterSource` | 从 `CombatDebugSnapshot` 生成 Combat performance counters |
| `RuntimeConfigHotReloadDebugSource` | 将最近一次 `RuntimeConfigHotReloadResult` 映射为 Debug UI sections |
| `StoryRuntimeDebugSource` | Story.Editor 中的只读 source，将 Story runtime graph / beat / blackboard / event queue 快照映射为 Debug UI sections |
| `DebugUiCommandGate` | 显式 Debug UI command gate，默认 disabled，统一做风险、确认和执行日志检查 |
| `DebugUiCommandDescriptor` / `DebugUiCommandRequest` / `DebugUiCommandResult` | Command provider 协议、参数 schema、确认标记和结构化执行结果 |
| `IDebugUiCommandProvider` | 项目层可选命令提供者接口；默认实现为空 provider |
| `DebugUiCommandGateDebugSource` | 将 command 列表和最近执行日志映射为只读 Debug UI source |
| `DebugUiInputAdapter` | 可选 input adapter，消费 `IInputProvider` commands 并切换 overlay visibility / refresh |
| `IDebugUiInputTarget` | input adapter 面向 overlay 或测试替身的目标接口 |
| `DebugUiOverlayInputBridge` | Unity MonoBehaviour bridge，将 `InputService` 连接到 `DebugUiOverlayController` |

## 使用约定

- `FrameworkDebugSourceRegistry` 不是单例；Demo、游戏层或工具组合根负责创建和持有。
- Source name 使用 `StringComparer.Ordinal` 唯一性；`Logging` 和 `logging` 是不同 source。
- `IsAvailable == false` 的 source 仍出现在 dashboard 中，显示为 unavailable。
- `CreateSnapshot()` 抛异常时，aggregator 记录 `DebugUiErrorViewModel`，其他 source 继续刷新。
- 每次 refresh 都生成新的展示模型；UI 展开状态不写回 source。
- Toolkit overlay 首版只读，Commands tab 默认不交付。
- Toolkit overlay 当前包含 Overview、Snapshots、Timeline、Entities、Logs 五个只读 tab；Timeline / Entities tab 只渲染对应标题的 sections。
- Timeline 只观察既有 Gameplay / Combat 诊断事实，不新增权威事件流或回放语义。
- Entity Watch 只读取 component world 当前快照，不提供选中实体后的写操作。
- Character Control source 只订阅公开 state / control / action / pressure events；组合根可调用 `RecordCommand(...)` / `RecordMotionResult(...)` 补充 last command 和 motion，不读取 controller 私有容器。
- Story runtime source 位于 `MxFramework.Story.Editor`，只读取 `StoryDirector.CreateSnapshot()`、`RuntimeEventQueue<StoryRuntimeEvent>.CreateSnapshot()` 和 `StoryRuntimeModule` 公开 last command/error 列表；不读取 Director 私有字段，也不提供默认写操作。
- `FrameworkDebugSourceRegistry` 不负责释放 source；注册 `CharacterControlDebugSource` 这类 event-backed source 的组合根必须在销毁或换绑角色时调用 `Dispose()` 解除订阅。
- Command gate 不属于 snapshot 写入口；它是组合根显式持有的可选对象，`Options.Enabled` 默认 false。
- 中高风险或破坏性 command 必须在 `DebugUiCommandDescriptor` 中声明 risk / confirmation / 参数 schema，破坏性 command 还需要显式打开 `AllowDestructiveCommands`。
- Debug UI input adapter 只通过 `PeekForFrame()` 非破坏性观察 `IInputProvider.Commands` 中的 `ToggleHud`、`ToggleConsole`、`DebugCycle`、`DebugStep` 等 intent，并通过 `InputContext.Debug` 的 overlay scope 接入。
- `MxFramework.DebugUI.Input` 可以引用 Unity/Input；`MxFramework.DebugUI` core 仍保持 noEngine 且不引用 Input。

## 接入示例

```csharp
var registry = new FrameworkDebugSourceRegistry();
registry.Register(new LogDebugSource(logBuffer));
registry.Register(new RuntimeHostDebugSource(runtimeHost));
registry.Register(new ResourceDebugSource(resourceManager));
registry.Register(new GameplayRuntimeEventTimelineDebugSource(() => gameplayEvents));
registry.Register(new CombatTimelineDebugSource(() => combatDebugSnapshot));
registry.Register(new CharacterControlDebugSource(
    characterControlStateMachine,
    actionController: characterActionController,
    pressureReactionController: pressureReactionController));
registry.Register(new GameplayComponentWorldEntityWatchDebugSource(componentWorld));
RuntimeConfigHotReloadResult lastHotReloadResult = null;
registry.Register(new RuntimeConfigHotReloadDebugSource(() => lastHotReloadResult));

var aggregator = new DebugUiSnapshotAggregator();
DebugUiDashboardViewModel dashboard = aggregator.Refresh(registry);
```

Demo 层可以在组合根中把 registry 交给 `DebugUiOverlayController.Configure(registry)`；现有 `RuntimeAbilitySliceRunner.CreateDebugSourceRegistry()` 提供了 RuntimeHost + Gameplay snapshot 的最小示例。

## 禁止事项

- 不在 Runtime / Gameplay / Combat / Resources 核心模块中引用 DebugUI。
- 不让 Debug UI 表现状态进入 Replay / SaveState / Runtime hash。
- 不在 `MxFramework.DebugUI` core 中引用 `UnityEngine`、`UnityEditor`、`UnityEngine.UIElements` 或 Input System。
- 不把可写命令塞进 `FrameworkDebugSnapshot`。

## 测试入口

`Assets/Scripts/MxFramework/Tests/DebugUI/`
