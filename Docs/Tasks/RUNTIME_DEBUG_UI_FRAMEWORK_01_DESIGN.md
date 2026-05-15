# Runtime Debug UI Framework 01：通用运行时调试界面框架设计

> Issue: #85「设计：通用运行时调试界面框架」
> Status: Design Draft
> Task level: S2
> Delivery level: Design / Implementation Plan
> 日期：2026-05-15

## 目标

设计一套 WGameFramework 通用运行时调试界面框架，让任意 Demo、Playable、Runtime Showcase 或项目接入层都能在 Play Mode / Development Build 中调出、隐藏、折叠和刷新调试信息，同时保持纯 Runtime / Gameplay / Combat / Resources 核心不依赖 Unity UI。

本任务只交付设计文档，不实现代码，不创建 Unity 资产，不手写 YAML。后续实现必须从新的 Gitea implementation Issue 开始。

## 当前基础和缺口

仓库已有以下可复用基础：

- Diagnostics：`IFrameworkDebugSource`、`FrameworkDebugSnapshot`、`FrameworkDebugSection`、`FrameworkDebugReportExporter`。
- Logging Diagnostics：`LogDebugSource` 可把 `LogBuffer` 最近日志导出为通用 `FrameworkDebugSnapshot`。
- Resources Diagnostics：`ResourceDebugSource` 可把 `IResourceManager.CreateDebugSnapshot()` 输出接入同一诊断链路。
- Runtime：`RuntimeHost.CaptureDiagnostics()` 已能输出 lifecycle、tick count、module list 和 errors。
- Input：已有 `InputContext.Debug`、`InputIntent.ToggleHud`、`InputIntent.ToggleConsole`、`DebugCycle`、`DebugStep` 等调试输入意图；默认 Debug action map 已绑定 backquote、H、Q/X、T 等键。
- UI Toolkit：已有 `MxUiThemeTokens`、`MxStatusBadge`、`MxCommandButton`、`MxEventLog`、`MxPanelTabs` 等通用控件和主题 token。

缺口是这些能力没有统一的运行时调试 overlay。现有 `MxRuntimeHudController` 已经承载 Ability / Combat Showcase 的具体字段、按钮和布局，不适合作为通用调试界面基类。

## API 复用计划

| 能力 | 复用方式 | 说明 |
| --- | --- | --- |
| Diagnostics | 继续使用 `IFrameworkDebugSource`、`FrameworkDebugSnapshot`、`FrameworkDebugSection`、`FrameworkDebugReportExporter` | Debug UI 只做注册、聚合和展示，不读取模块私有字段。 |
| Logging | 通过 `LogDebugSource` 接入最近日志 | 日志仍由游戏层或组合根持有 `LogBuffer` / `ILogger`；Debug UI 不提供全局 logger。 |
| Resources | 通过 `ResourceDebugSource` 接入资源快照 | Resources 自己生成 summary、catalog、entry origins、recent errors；Debug UI 不依赖资源管理内部结构。 |
| RuntimeHost diagnostics | 通过 adapter 包装 `RuntimeHost.CaptureDiagnostics()` | Runtime Core 不引用 Debug UI；adapter 放在组合根或 Debug UI 扩展层，输出 Host state、tick、modules、errors。 |
| Input Debug map | 复用 `InputContext.Debug`、`InputIntent.ToggleHud`、`ToggleConsole`、`DebugCycle`、`DebugStep` | Debug UI 不直接读取 `Keyboard.current`；输入由项目层或可选 adapter 转换。 |
| UI Toolkit 控件 | 复用 `MxUiThemeTokens`、`MxStatusBadge`、`MxCommandButton`、`MxEventLog`、`MxPanelTabs` | 只复用通用控件和 token；不复用 Showcase 专用 ViewModel 作为通用调试模型。 |

## 通用 Debug UI 与 Showcase HUD 边界

通用 Debug UI 是开发者调试外壳，面向跨模块可观测性：

- 展示多个 `IFrameworkDebugSource` 的只读快照。
- 显示日志、RuntimeHost、Resources、Gameplay、Combat 等逐步接入的 source。
- 管理 Hidden / Collapsed / Expanded、刷新、搜索、tab 和可选命令区。
- 不知道某个 Demo 的玩法按钮、角色字段、技能名或战斗流程。

Showcase HUD 是具体 Demo / Playable 的用户界面，面向可玩流程：

- 可以显示当前玩法的 HP、技能按钮、重置按钮、演示步骤、配置切换等具体字段。
- 可以通过 `RuntimeCommandBuffer` 或 Demo runner 发起业务命令。
- 可以继续复用 `MxFramework.UI.Toolkit` 控件。
- 不应成为所有运行时调试 overlay 的基类或全局入口。

因此后续实现应新增独立 Debug UI 层，而不是继续扩展 `MxRuntimeHudController`。现有 Showcase HUD 可与 Debug UI 同屏共存：HUD 服务玩家/验证流程，Debug UI 服务开发者诊断。

## 非目标

- 不做 WGame 业务调试面板。
- 不做远程控制台、网络同步调试、线上遥测、文件上传或 crash reporting。
- 不把 Debug UI 状态写入 Replay / Runtime hash / SaveState 权威状态。
- 不在纯 Runtime / Gameplay / Combat / Resources 核心中引用 `UnityEngine`、`UnityEngine.UIElements`、Input System 或 `UnityEditor`。
- 不默认提供全局单例。游戏层或 Demo 组合根负责创建、注册和生命周期。
- 不在本设计任务中创建场景、Prefab、UXML、USS、PanelSettings 等 Unity 序列化资产。
- 不把可写调试命令塞进 `FrameworkDebugSnapshot`。

## 架构结构

推荐结构：

```text
Game / Demo Composition Root
  -> create source registry
  -> register Diagnostics / Logging / Resources / RuntimeHost adapters
  -> optionally register writable command providers
  -> optionally connect Input Debug map adapter
  -> mount Debug UI overlay controller

MxFramework.Diagnostics
  -> IFrameworkDebugSource
  -> FrameworkDebugSnapshot
  -> FrameworkDebugSection

MxFramework.DebugUI
  -> source registry
  -> snapshot aggregation
  -> debug dashboard view model
  -> UI visibility state model
  -> optional writable command provider contracts

MxFramework.DebugUI.Toolkit
  -> UI Toolkit overlay shell
  -> tabs / lists / source cards / search / refresh
  -> visibility and focus behavior

MxFramework.DebugUI.Input
  -> InputIntent adapter
  -> ToggleHud / ToggleConsole / DebugCycle / DebugStep routing
```

依赖方向：

```text
Diagnostics <- DebugUI <- DebugUI.Toolkit
                       <- DebugUI.Input

UI.Toolkit <- DebugUI.Toolkit
Input      <- DebugUI.Input
```

Runtime、Gameplay、Combat、Resources 不能反向依赖 DebugUI。需要展示这些模块状态时，由组合根或 adapter 把它们转换成 `IFrameworkDebugSource`。

## 程序集拆分建议

| 程序集 | 依赖 | noEngine | 职责 |
| --- | --- | --- | --- |
| `MxFramework.DebugUI` | `MxFramework.Diagnostics`、必要时 `MxFramework.Core` | 是 | Source registry、snapshot aggregator、view model、visibility state、command provider DTO。 |
| `MxFramework.DebugUI.Toolkit` | `MxFramework.DebugUI`、`MxFramework.UI.Toolkit`、Unity UI Toolkit | 否 | 运行时 overlay shell、tab/list/source card、绑定器、主题扩展。 |
| `MxFramework.DebugUI.Input` | `MxFramework.DebugUI`、`MxFramework.Input` | 否 | 可选输入适配器，把 `InputIntent` 路由到 Debug UI state。 |

如果首批实现需要控制文件量，可以先落地 `MxFramework.DebugUI` + `MxFramework.DebugUI.Toolkit`，把 Input adapter 放到第二个实施切片。但不建议让 `MxFramework.UI.Toolkit` 直接反向依赖 Input，也不建议让 DebugUI core 依赖 UI Toolkit。

## 核心契约草案

### Source Registry

`FrameworkDebugSourceRegistry` 是普通对象，不是静态全局表。组合根创建后传给 Debug UI。

```csharp
public sealed class FrameworkDebugSourceRegistry
{
    public bool Register(IFrameworkDebugSource source);
    public bool Unregister(string name);
    public IReadOnlyList<IFrameworkDebugSource> Sources { get; }
}
```

规则：

- `Name` 使用 ordinal 唯一性；重复注册默认失败，覆盖策略必须显式配置。
- `IsAvailable == false` 的 source 仍可显示为不可用，不强制隐藏。
- `Register` 不调用 `CreateSnapshot()`，避免注册阶段触发高成本或异常。
- registry 不持有模块私有对象，只持有 `IFrameworkDebugSource`。

### Snapshot Aggregation

`DebugUiSnapshotAggregator` 把多个 source 转成一个展示模型：

```csharp
public sealed class DebugUiDashboardViewModel
{
    public IReadOnlyList<DebugUiSourceViewModel> Sources { get; }
    public IReadOnlyList<DebugUiErrorViewModel> Errors { get; }
    public long RefreshSequence { get; }
}
```

聚合规则：

- 默认按 `FrameworkDebugMode`、`SourceName` 稳定排序。
- 每个 `FrameworkDebugSection` 映射为可折叠 section。
- 空 section 显示明确空状态。
- `CreateSnapshot()` 异常必须被捕获为 `DebugUiErrorViewModel`，不能让整个 overlay 崩溃。
- 每次刷新生成新的展示模型，不把 UI 展开状态写回 source。
- 默认刷新节流，例如 4 Hz；用户可以手动刷新，也可以暂停自动刷新。

### UI Visibility State

Debug UI 的可见性是表现状态，不属于游戏权威状态：

```csharp
public enum DebugUiVisibility
{
    Hidden,
    Collapsed,
    Expanded
}
```

建议状态字段：

- `Visibility`
- `ActiveTabId`
- `SelectedSourceName`
- `SearchText`
- `RefreshPaused`
- `LastRefreshSequence`
- `LastRefreshUtcTicks`

这些状态只允许保存在 UI controller 或本地偏好中，不进入 Replay / SaveState / Runtime hash。

### Writable Command Provider

只读 snapshot 和可写命令必须分开：

```csharp
public interface IFrameworkDebugCommandProvider
{
    IReadOnlyList<FrameworkDebugCommandDefinition> CreateDefinitions();
    FrameworkDebugCommandResult Execute(FrameworkDebugCommandRequest request);
}
```

建议 DTO：

```csharp
public readonly struct FrameworkDebugCommandDefinition
{
    public string ProviderName { get; }
    public string CommandId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public FrameworkDebugCommandRisk Risk { get; }
    public bool RequiresConfirmation { get; }
}
```

命令规则：

- 默认关闭命令执行，只展示只读 source。
- 命令需要声明 `CommandId`、显示名、描述、参数 schema、风险等级和是否需要确认。
- 破坏性命令在 Development Build 中也必须二次确认。
- Release Player 默认禁用 command provider；是否允许由项目层显式配置。
- 命令结果写入 Debug UI 事件流，也可以由调用方写入 `ILogger`。
- 命令 provider 不替代 `RuntimeCommandBuffer`。如果命令会改变 runtime 权威状态，应由 provider 显式转换为已有 runtime command 或调用组合根允许的服务。

## Runtime UX

首版采用非侵入式 overlay：

| 状态 | 行为 |
| --- | --- |
| `Hidden` | 完全隐藏，不拦截输入，不 push UI context。 |
| `Collapsed` | 只显示小型 handle / 状态条，展示 source 数、错误数、暂停状态和当前 tab 简名；不阻塞 Gameplay。 |
| `Expanded` | 显示右侧或底部调试面板，包含 tabs、搜索、刷新、暂停、关闭和可选命令区。 |

默认 tabs：

| Tab | 内容 |
| --- | --- |
| Overview | Source 状态、错误数、RuntimeHost 摘要、当前 frame / tick 信息。 |
| Snapshots | 按 source 展示 `FrameworkDebugSnapshot` sections。 |
| Logs | 由 `LogDebugSource` 或日志 source 提供的最近日志。 |
| Commands | 显式注册的调试命令；默认隐藏或禁用。 |

推荐输入映射：

| 输入意图 | 行为 |
| --- | --- |
| `ToggleHud` | `Hidden -> Expanded -> Hidden`。 |
| `ToggleConsole` | `Hidden -> Collapsed -> Expanded -> Hidden`，用于控制台式调试入口。 |
| `DebugCycle` | Expanded 时切换 tab 或 source；Collapsed 时切换摘要 source。 |
| `DebugStep` | Expanded 且当前 panel 支持 step 时执行 panel-local step；否则记录 ignored event。 |
| `Cancel` | Expanded 时回到 Collapsed 或 Hidden，由 options 决定。 |

Focus 行为：

- Hidden 时不 push UI context。
- Collapsed 时只保留 Debug overlay 热键，不阻塞 Gameplay。
- Expanded 且鼠标 / 键盘焦点进入面板时，项目层可 push `InputContext.UI`，策略由组合根决定。
- Expanded 时按钮、搜索框等 UI Toolkit 元素可以获得 focus；离开面板后应释放 UI context 或回到 Debug overlay 热键模式。
- Debug hotkeys 推荐通过 `InputContext.Debug` overlay 常驻提供，不直接绑定具体键盘 API。

## 实施切片

### M1：Core Registry and Aggregator

- 新增 `MxFramework.DebugUI` noEngine 程序集。
- 提供 source registry、dashboard view model、refresh aggregator。
- 捕获 source 异常并生成错误模型。
- 单元测试覆盖注册排序、不可用 source、异常 source 和 section 映射。

验收：

- 不新增 UnityEngine / UI Toolkit / Input System 依赖。
- 不修改现有 `IFrameworkDebugSource` 契约。
- Diagnostics、Logging、Resources 现有测试不回退。

### M2：UI Toolkit Overlay Shell

- 新增 `MxFramework.DebugUI.Toolkit`。
- 建立可隐藏 / 折叠 / 展开的 UI Toolkit overlay controller。
- 使用 `MxUiThemeTokens`、`MxPanelTabs`、`MxEventLog`、`MxStatusBadge`、`MxCommandButton` 等通用控件。
- 提供默认 UXML / USS 生成菜单或代码构建兜底。

验收：

- Play Mode 可显示空状态和至少一个 fake source。
- Hidden 不拦截输入，Collapsed / Expanded 状态可切换。
- 不反向依赖 Demo / Gameplay / Combat / Resources 内部类型。

### M3：Framework Source Adapters

- 提供 `RuntimeHostDebugSource` 或示例 adapter，把 `RuntimeHost.CaptureDiagnostics()` 映射为 `FrameworkDebugSnapshot`。
- 给 Logging / Resources 示例接入路径补文档。
- 在一个现有 Runtime Showcase 或 Combat Showcase 中接入 Debug UI source，不替代原 Showcase HUD。

验收：

- 一个现有 Demo 能在同一 overlay 中看到 RuntimeHost、Logs、Resources 或 Gameplay / Combat snapshot。
- Showcase HUD 继续存在，Debug UI 是开发者调试层，不抢占制作人 HUD。

### M4：Optional Command Providers

- 新增可写命令 provider 协议和 UI。
- 支持无参数命令、简单数值 / string 参数、confirm gate。
- Demo 接入 Reset、Pause Refresh 或 Capture Snapshot 这类低风险命令。

验收：

- 默认只读。
- command provider 需要显式启用。
- 命令失败返回结构化错误并进入事件流。

### M5：Adoption and Quality Gate

- 至少两个场景 / Demo 接入同一 Debug UI。
- 补齐 Editor 菜单生成入口，避免手写 Unity YAML。
- 加入边界静态检查和 Play Mode smoke test。

验收：

- `MxFramework.DebugUI` 仍为 noEngine。
- Runtime Core、Gameplay、Combat、Resources 不依赖 Debug UI。
- Development Build / Release gating 行为有测试或手测记录。

## 文件计划

首批建议文件：

```text
Assets/Scripts/MxFramework/DebugUI/
  MxFramework.DebugUI.asmdef
  FrameworkDebugSourceRegistry.cs
  DebugUiSnapshotAggregator.cs
  DebugUiDashboardViewModel.cs
  DebugUiVisibility.cs
  FrameworkDebugCommandContracts.cs

Assets/Scripts/MxFramework/DebugUI.Toolkit/
  MxFramework.DebugUI.Toolkit.asmdef
  DebugUiOverlayController.cs
  DebugUiOverlayViewModelBinder.cs
  DebugUiToolkitThemeTokens.cs

Assets/Scripts/MxFramework/DebugUI.Input/
  MxFramework.DebugUI.Input.asmdef
  DebugUiInputAdapter.cs

Assets/Scripts/MxFramework/Tests/DebugUI/
  DebugUiSourceRegistryTests.cs
  DebugUiSnapshotAggregatorTests.cs
  DebugUiCommandContractTests.cs
```

Unity 资产文件必须通过 Editor 菜单、Unity Editor 或 Unity MCP 生成，不手写 YAML：

```text
Assets/UI/MxFramework/DebugUI/
  RuntimeDebugOverlay.uxml
  RuntimeDebugOverlay.uss
  RuntimeDebugOverlayPanelSettings.asset
```

设计文档入口：

```text
Docs/Tasks/RUNTIME_DEBUG_UI_FRAMEWORK_01_DESIGN.md
Docs/README.md
Docs/Interfaces/Diagnostics.md
Docs/Interfaces/Input.md
```

## 边界检查

实施时必须保留以下边界：

- `MxFramework.DebugUI` 不引用 `UnityEngine`、`UnityEditor`、`UnityEngine.UIElements`、Input System。
- `MxFramework.DebugUI.Toolkit` 不引用 Demo / WGame 业务类型。
- `MxFramework.DebugUI.Input` 是可选层，不能让 Debug UI core 强依赖 Input。
- `MxFramework.Diagnostics` 仍只定义 snapshot 协议，不承担 UI 状态。
- `IFrameworkDebugSource.CreateSnapshot()` 仍默认只读。
- 调试命令不进入 Replay / SaveState / hash。
- Runtime、Gameplay、Combat、Resources 不依赖 DebugUI；接入通过组合根注册 source / adapter。

建议静态检查：

```bash
rg -n "UnityEngine|UnityEditor|UIElements|InputSystem" Assets/Scripts/MxFramework/DebugUI -g '*.cs'
rg -n "MxFramework\\.Demo|WGame|MxFramework\\.Gameplay|MxFramework\\.Combat" Assets/Scripts/MxFramework/DebugUI.Toolkit Assets/Scripts/MxFramework/DebugUI.Input -g '*.cs'
```

## 验证计划

设计阶段：

```bash
git diff --check -- Docs/Tasks/RUNTIME_DEBUG_UI_FRAMEWORK_01_DESIGN.md Docs/README.md Docs/Interfaces/Diagnostics.md Docs/Interfaces/Input.md
rg -n "RUNTIME_DEBUG_UI_FRAMEWORK_01_DESIGN|Runtime Debug UI|通用运行时调试" Docs/README.md Docs/Interfaces/Diagnostics.md Docs/Interfaces/Input.md Docs/Tasks/RUNTIME_DEBUG_UI_FRAMEWORK_01_DESIGN.md
```

M1 后：

- `dotnet build WGameFramework.sln --no-restore -v minimal`
- `MxFramework.Tests.DebugUI.*`
- `MxFramework.Tests.Diagnostics.*`
- `MxFramework.Tests.Logging.*`
- `MxFramework.Tests.Resources.*`

M2 后：

- UI Toolkit 构造测试。
- Play Mode smoke：打开 overlay、折叠、隐藏、刷新 fake source。
- Unity Console 0 compile error。

M3+ 后：

- 接入 Demo 的手测记录。
- GitNexus detect-changes 或同等影响面说明。
- 边界静态检查。

## 开放决策

1. `ToggleHud` 与 `ToggleConsole` 是否都控制同一 overlay，还是 `ToggleConsole` 预留给未来命令行式 console。
2. 默认 `Cancel` 行为是 `Expanded -> Collapsed` 还是 `Expanded -> Hidden`。
3. RuntimeHost adapter 放在 `MxFramework.DebugUI`、单独 `MxFramework.DebugUI.Runtime`，还是只作为组合根示例。
4. Debug UI 自动刷新默认频率是否固定 4 Hz，或由 overlay options 配置。
5. Release Player 中是否完全禁用 overlay，还是允许项目显式启用只读 source。
6. 可写命令参数 schema 是否复用 Runtime command registry 的 payload schema，还是保持 Debug UI 独立 DTO。

## ADR 判断

本 Issue 的交付是 S2 设计文档，当前不需要立即新增 ADR。理由：

- 本文已经作为任务级长期设计入口，足以指导 M1-M5 implementation Issue。
- 当前不修改公共 API、不新增正式程序集、不改变现有依赖矩阵。
- 后续如果 owner 接受 `MxFramework.DebugUI` 作为长期顶层模块，并要求更新 `ARCHITECTURE.md` / `INTERFACES.md` 依赖矩阵，可在 M1 或单独决策 Issue 中补 ADR。

## 验收标准

- 明确通用 Debug UI 与现有 Showcase HUD 的边界。
- 明确新增独立 Debug UI 层，而不是扩展 `MxRuntimeHudController`。
- 明确 noEngine core、UI Toolkit shell、可选 Input adapter 的程序集边界。
- 明确 `Hidden` / `Collapsed` / `Expanded` 可见状态和输入意图映射。
- 明确 `IFrameworkDebugSource` snapshots 如何注册、刷新、排序和渲染。
- 明确只读 Snapshot 与可写调试命令的分离。
- 明确文件计划、边界检查、验证计划和开放决策。
- 明确当前不实现代码、不创建 Unity 资产、不手写 YAML。
