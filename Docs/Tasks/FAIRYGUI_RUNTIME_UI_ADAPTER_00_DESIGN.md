# FairyGUI Runtime UI Adapter 00: Design Contract

> **状态**: Spec Draft
> **优先级**: P1
> **范围**: 从 WGame 的 FairyGUI 接入经验提炼可复用 UI 抽象和 FairyGUI 运行时适配方案；本任务只交付设计，不实现代码、不导入 FairyGUI 插件、不创建 Unity 序列化资产。

## Goal

为 WGameFramework 建立一条可选的 FairyGUI 运行时 UI 接入路线，同时保留框架核心的可抽离边界。

本设计的目标不是把现有 UI Toolkit 全量替换掉，而是把运行时正式 UI 的抽象层先固定下来：

```text
Runtime / Gameplay / Story / Debug facts
  -> UI-neutral ViewModel + Command DTO
  -> UI-neutral Navigator / Registry / Lifecycle
  -> FairyGUI adapter or UI Toolkit adapter
  -> Concrete runtime view
```

完成后，WGameFramework 应能回答：

- 同一份 ViewModel 是否能被不同 UI 技术消费。
- Runtime Core 是否仍然不引用 FairyGUI、UI Toolkit 或项目私有插件。
- FairyGUI package、atlas、audio、font 等资源是否能通过框架资源系统描述和验证。
- FairyGUI 的代码生成、绑定和窗口生命周期能否作为 adapter 能力存在，而不是侵入框架核心。

## Background

当前 WGameFramework 已有两条 UI 相关能力：

- `MxFramework.UI.Toolkit`：运行时 Showcase HUD 和基础控件层，已经沉淀 `MxStatusBadge`、`MxCommandButton`、`MxStatBar`、`MxStressBar`、`MxEventLog`、`MxPanelTabs` 等控件。
- `MxFramework.DebugUI` / `MxFramework.DebugUI.Toolkit`：只读开发调试 overlay，依赖 Diagnostics snapshot 和 UI Toolkit 绑定器。

这些能力足够支撑 Showcase、Debug UI 和编辑器过渡，但 UI Toolkit 在正式游戏运行时 UI 上有明显摩擦：复杂面板、动效、嵌套组件、列表、弹窗、资源包协作和美术工作流都不如 FairyGUI 顺手。

WGame 项目已经接入 FairyGUI，并包含以下可参考经验：

- `FairyGUI.Dynamic.UIAssetManager`：通过 `IUIAssetLoader` 和 `IUIPackageHelper` 接管 FairyGUI package、texture、audio 的加载与释放。
- `YooassetUIAssetLoader`：把 FairyGUI 资源加载桥接到 YooAsset。
- `FGUITools`：生成 package mapping、View DB、panel skeleton 和强类型 `FUI_*` 绑定。
- `BaseView`：封装 FairyGUI `Window` 生命周期、显示/隐藏动画、语言刷新、事件清理、Timer 清理、子界面管理。
- `UIManager.OpenView(VDB.Xxx)`：使用 view id / registry 打开界面，开发体验直接。

同时，WGame 的实现也暴露了本次必须规避的问题：

- `UIManager` 同时处理资源、输入、音频、Steam、设置、剧情、环境音、窗口栈和全局状态，职责过大。
- View 直接读取 `ChessModel.Inst`、`StoryManager.Inst`、`DialogModel.Inst`、`EventCenter` 等游戏单例，表现层和业务层耦合过深。
- `OpenView(string, TAny[])` 类型不安全，参数协议依赖调用约定。
- `BaseView : FairyGUI.Window` 把通用窗口生命周期绑定到了 FairyGUI。
- `VDB` 只提供 string 到 Type 的映射，缺少层级、modal、资源包、输入 scope、缓存策略、预加载策略等声明式元数据。

本设计取其资源桥、代码生成、生命周期和 registry 思想，不迁移 WGame 的全局单例做法。

## Non-Goals

- 不把 FairyGUI 作为 WGameFramework core 的必选依赖。
- 不让 `MxFramework.Core`、`Runtime`、`Gameplay`、`Combat`、`Story`、`Resources` 等内层模块引用 FairyGUI。
- 不在本任务导入 `Assets/Plugins/FairyGUI*`。
- 不重写现有 UI Toolkit Showcase 或 Debug UI。
- 不设计完整项目 UI、背包、装备、关卡、设置、剧情 UI。
- 不把 WGame 现有 `UIManager`、`BaseView`、`VDB` 原样迁入。
- 不引入 WGame、Entitas、Luban、YooAsset 或 Wwise 依赖。

## Module Plan

### UI Core

新增可选 noEngine 模块：

```text
Assets/Scripts/MxFramework/UI/
  MxFramework.UI.asmdef
  MxUiViewId.cs
  MxUiViewDescriptor.cs
  MxUiLayer.cs
  IMxUiView.cs
  IMxUiViewHost.cs
  IMxUiViewRegistry.cs
  IMxUiNavigator.cs
  IMxUiCommandSink.cs
  MxUiLifecycle.cs
  MxUiViewContract.cs
  MxUiCommandDescriptor.cs
  MxUiOpenResult.cs
  MxUiOpenOperation.cs
```

职责：

- 保存 UI 技术无关的 view id、descriptor / contract、layer、lifecycle、navigator、command sink。
- 承载 ViewModel / Command DTO 的推荐模式。
- 提供可测试的窗口打开/关闭语义和错误结果。

禁止：

- 不引用 UnityEngine。
- 不引用 FairyGUI、UI Toolkit、UGUI。
- 不引用 Gameplay、Combat、Story、DebugUI 等上层模块。
- 不读取静态全局状态。

### FairyGUI Adapter

新增可选 Unity-facing adapter：

```text
Assets/Scripts/MxFramework/UI.FairyGUI/
  MxFramework.UI.FairyGUI.asmdef
  FairyGuiViewHost.cs
  FairyGuiViewRegistry.cs
  FairyGuiPackageLoader.cs
  FairyGuiResourceBridge.cs
  FairyGuiViewBinder.cs
  FairyGuiGeneratedViewDescriptor.cs
```

职责：

- 使用 FairyGUI 创建、显示、隐藏和释放实际 view。
- 将 `MxUiViewDescriptor` 映射到 FairyGUI package / component。
- 把 FairyGUI button、list、controller、transition 事件转成 `MxUiCommand`。
- 通过 `MxFramework.Resources` 加载 package bytes、atlas texture、audio clip、font 等资源。
- 可选承接生成代码，但生成物必须留在 adapter 或项目层，不进入 UI core。

禁止：

- 不引用 WGame 命名空间。
- 不直接读取 Gameplay / Combat / Story 私有状态。
- 不直接访问 `Resources.Load`、Addressables 或项目私有资源系统；必须通过资源桥或组合根注入。

### Existing UI Toolkit Adapter

现有 `MxFramework.UI.Toolkit` 暂时保留。后续可逐步调整为 UI core 的一个 adapter，但不要求本阶段迁移。

`MxFramework.DebugUI.Toolkit` 继续服务 Debug UI。Debug UI 是开发观察层，不急于迁移到 FairyGUI。

## Core Contracts

建议首批接口形状：

```csharp
public readonly struct MxUiViewId
{
    public MxUiViewId(string value);
    public string Value { get; }
}

public enum MxUiLayer
{
    Background = 0,
    Hud = 100,
    Panel = 200,
    Popup = 300,
    Modal = 400,
    Toast = 500,
    Debug = 900
}

public sealed class MxUiViewDescriptor
{
    public MxUiViewDescriptor(
        MxUiViewId id,
        string packageKey,
        string componentName,
        MxUiLayer layer)
    {
        Id = id;
        PackageKey = packageKey ?? string.Empty;
        ComponentName = componentName ?? string.Empty;
        Layer = layer;
    }

    public MxUiViewId Id { get; }
    public string PackageKey { get; }
    public string ComponentName { get; }
    public MxUiLayer Layer { get; }
    public bool Modal { get; set; }
    public bool KeepAlive { get; set; }
    public bool CloseOnSceneChange { get; set; }
    public string InputScope { get; set; }
}

public sealed class MxUiViewContract
{
    public MxUiViewContract(MxUiViewDescriptor descriptor)
    {
        Descriptor = descriptor;
        RequiredResources = Array.Empty<string>();
        Commands = Array.Empty<MxUiCommandDescriptor>();
        DiagnosticsTags = Array.Empty<string>();
    }

    public MxUiViewDescriptor Descriptor { get; }
    public string ViewModelType { get; set; }
    public IReadOnlyList<string> RequiredResources { get; set; }
    public IReadOnlyList<MxUiCommandDescriptor> Commands { get; set; }
    public IReadOnlyList<string> DiagnosticsTags { get; set; }
}

public sealed class MxUiCommandDescriptor
{
    public string CommandId { get; set; }
    public string PayloadType { get; set; }
    public string RiskLevel { get; set; }
    public bool RequiresConfirmation { get; set; }
    public bool IsReadOnly { get; set; }
    public string Owner { get; set; }
}

public interface IMxUiView
{
    MxUiViewId Id { get; }
    MxUiLifecycle Lifecycle { get; }
    void Show();
    void Hide();
    void Dispose();
}

public interface IMxUiView<in TViewModel> : IMxUiView
{
    void Bind(TViewModel model);
}

public interface IMxUiNavigator
{
    MxUiOpenResult Open<TArgs>(MxUiViewId id, TArgs args);
    MxUiOpenOperation OpenAsync<TArgs>(MxUiViewId id, TArgs args);
    bool Close(MxUiViewId id);
    bool IsOpen(MxUiViewId id);
}
```

`MxUiViewDescriptor` is the minimal instantiation descriptor. `MxUiViewContract` is the machine-readable contract owned from M1 so M2/M4 do not need to revise the core shape immediately. Generated descriptors, JSON mirrors and validation reports should all derive from this contract.

Open semantics:

- `Open<TArgs>` is allowed only when all resources required by `MxUiViewContract.RequiredResources` are already available or the adapter can load them synchronously.
- `OpenAsync<TArgs>` returns `MxUiOpenOperation`, a handle with stable status such as `Pending`, `Succeeded`, `Failed` and `Cancelled`.
- `MxUiOpenResult` must carry a stable error code and diagnostic message for synchronous failures.
- `MxUiOpenOperation` must expose the same result when completed, plus cancellation where the adapter can safely cancel pending loads.
- M2 may implement `OpenAsync` with an immediately completed operation if the first prototype only supports preloaded synchronous resources, but the API shape must exist from M1.

Typed arguments should be preferred over string arrays or object arrays:

```csharp
public readonly struct ConfirmDialogArgs
{
    public ConfirmDialogArgs(string title, string body, string confirmLabel, string cancelLabel);
    public string Title { get; }
    public string Body { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }
}
```

Commands should flow outward as typed DTOs:

```csharp
public readonly struct MxUiCommand
{
    public MxUiViewId SourceViewId { get; }
    public string CommandId { get; }
    public string PayloadJson { get; }
}

public interface IMxUiCommandSink
{
    void Enqueue(MxUiCommand command);
}
```

For high-value surfaces, use explicit command types instead of generic payload strings:

```csharp
public enum RuntimeHudCommand
{
    Strike,
    Ignite,
    ApplyBuff,
    Tick,
    Reset
}
```

## Lifecycle

UI core lifecycle should mirror existing framework lifecycle rules:

```text
Create
  -> Initialize
  -> Attach
  -> Show
  -> Bind / Refresh
  -> Hide
  -> Detach
  -> Dispose
```

Rules:

- `Dispose` must be idempotent.
- Event subscriptions, timers and command callbacks must be removed during `Detach` or `Dispose`.
- `Show` / `Hide` may be animated by adapter, but core lifecycle state must be deterministic.
- UI open / close state is presentation state and must not enter Runtime hash, Replay hash or SaveState.
- Adapter may keep view instances alive when `KeepAlive == true`, but must still expose deterministic `Hide` and `Dispose` behavior.

## FairyGUI Resource Bridge

WGame's `YooassetUIAssetLoader` shows the right direction: FairyGUI should not own the project resource system. In WGameFramework, the equivalent should bridge through `MxFramework.Resources`.

Recommended mapping:

| FairyGUI need | Resource key shape | Notes |
| --- | --- | --- |
| package bytes | `ui.fairygui.<package>.fui` | maps to exported `*_fui.bytes` |
| atlas texture | `ui.fairygui.<package>.<atlas>` | maps to png / texture resource |
| audio clip | `ui.fairygui.<package>.<clip>` | optional |
| font | `ui.font.<fontId>` | optional |
| package mapping | generated config or ScriptableObject adapter | adapter-only |

The adapter should support both synchronous and asynchronous package creation only if the underlying resource provider supports it. If a provider is pending, synchronous `Open` must fail with a structured result such as `ResourcesPending`, while `OpenAsync` returns an `MxUiOpenOperation` that completes after package bytes, atlas textures and other required resources are ready. A broken or partially created panel is not an acceptable pending state.

FairyGUI packages must be visible to Resource Manager / Global Resource Build Profile:

- package bytes and atlas textures appear as runtime resources;
- generated bundle plan can group all resources for one package;
- preload groups can warm up HUD or boot UI packages;
- validation can detect missing atlas, stale package mapping and type mismatch.

## Code Generation

WGame's `FGUITools` should be used as inspiration, not copied directly.

Recommended generated outputs:

```text
Generated/FairyGUI/
  FuiPackageIds.cs
  FuiViewIds.cs
  FuiViewDescriptors.cs
  FuiBindings/
    FuiRuntimeHudBinding.g.cs
    FuiConfirmDialogBinding.g.cs

Assets/Scripts/MxFramework/UI.FairyGUI/
  RuntimeHudFairyGuiView.cs
  ConfirmDialogFairyGuiView.cs
```

Generator responsibilities:

- read FairyGUI exported package metadata;
- emit strongly typed component and child names;
- emit view descriptors with package key, component name, layer and modal defaults;
- emit `*.g.cs` binding files that may be overwritten;
- never generate business logic;
- never generate code into UI core.

Stable generated/manual split:

- generated `*.g.cs` files contain only child, controller, transition, resource and command-id binding metadata;
- handwritten view / wrapper files own lifecycle, ViewModel mapping, command forwarding and error handling;
- generated files may be deleted and regenerated at any time;
- handwritten files may be edited by agents and humans;
- if partial classes are used, generated partials and manual partials must be clearly separated by filename.

Generated bindings should expose narrow methods:

```csharp
public sealed class FuiRuntimeHudBinding
{
    public void Bind(RuntimeHudViewModel model);
    public void SetCommandSink(IMxUiCommandSink sink);
    public void Dispose();
}
```

Avoid generated classes that directly call gameplay singletons, scene managers, story managers or global UI managers.

## ViewModel Rules

ViewModel should remain UI technology neutral:

- Plain C# data: string, int, bool, enums, immutable arrays or copied read-only lists.
- No `GComponent`, `GObject`, `VisualElement`, `Texture`, `GameObject`, `Transform`.
- Domain-to-tone mapping belongs in presenter / adapter layer, not inside low-level controls.
- ViewModel may include stable ids and command availability, but not direct service references.

Good:

```csharp
public sealed class RuntimeHudViewModel
{
    public RuntimeHudViewModel(
        string title,
        IReadOnlyList<RuntimeHudActionViewModel> actions,
        IReadOnlyList<string> eventLog)
    {
        Title = title ?? string.Empty;
        Actions = actions ?? Array.Empty<RuntimeHudActionViewModel>();
        EventLog = eventLog ?? Array.Empty<string>();
    }

    public string Title { get; }
    public IReadOnlyList<RuntimeHudActionViewModel> Actions { get; }
    public IReadOnlyList<string> EventLog { get; }
}
```

Avoid:

```csharp
public sealed class RuntimeHudViewModel
{
    public FairyGUI.GComponent Root;
    public RuntimeAbilitySliceRunner Runner;
}
```

## Navigation And Layers

Replace WGame-style static `UIManager` with an injected navigator:

```text
Composition Root
  -> create IMxUiViewRegistry
  -> create FairyGuiViewHost
  -> create IMxUiNavigator
  -> inject navigator / command sink into Demo or game layer
```

The navigator should own only view state:

- opened views;
- hidden views;
- layer ordering;
- modal stack;
- keep-alive cache;
- scene-change close policy.

It must not own:

- audio playback policy;
- input device polling;
- story execution;
- save data;
- Steam / platform services;
- gameplay authority.

Those systems can observe UI commands or inject services through composition root, but they must not be hidden inside the UI manager.

## Input Boundary

FairyGUI can receive pointer and keyboard/gamepad focus, but WGameFramework should keep `MxFramework.Input` as the source of input intent.

Recommended flow:

```text
Unity Input System
  -> MxFramework.Input intent
  -> UI input adapter
  -> IMxUiNavigator / focused view
  -> IMxUiCommandSink
```

FairyGUI-specific focus or navigation helpers may exist in `MxFramework.UI.FairyGUI`, but they should consume input intent and not poll devices directly in core.

## Migration Strategy

### M0: Design Contract

This document only.

### M1: UI Core noEngine Skeleton

- Add `MxFramework.UI`.
- Add ids, descriptors, lifecycle, registry and navigator tests.
- No Unity / FairyGUI references.

### M2: FairyGUI Adapter Prototype

- Add `MxFramework.UI.FairyGUI`.
- Support one package / one component / one ViewModel binding.
- Bridge resource loading through `MxFramework.Resources`.
- No generated code required yet.
- Current implementation status:
  - `MxFramework.UI` noEngine core is implemented.
  - `MxFramework.UI.FairyGUI` adapter prototype is implemented and isolated from core.
  - FairyGUI Unity runtime is embedded under `Packages/com.fairygui.gui`.
  - `FGUIProject/` exists as the framework-owned FairyGUI source project.
  - `MxFguiSmoke` exists as the first minimal source package and publish output under `Assets/Bundles/FGUI/MxFguiSmoke`.
  - `FGUIProject/plugins/wgameframework-agent-helper` provides project-local FairyGUI Editor commands to create/repair and publish the smoke package.
  - #510 still needs the real navigator smoke test using the published package.

### M3: Runtime HUD Vertical Slice

- Reuse an existing Runtime HUD ViewModel or extract a UI-neutral one from `MxFramework.UI.Toolkit`.
- Render the same runtime state in FairyGUI.
- UI Toolkit HUD remains available as fallback.

### M4: Code Generation And Resource Validation

- Add generator for view ids, descriptors, generated binding files and binding manifests.
- Register FairyGUI package resources in Resource Manager / Global Resource Build Profile.
- Add validation for missing package bytes / atlas / stale package mapping.

### M5: Decision Gate

Decide whether runtime formal UI should move to FairyGUI as the preferred adapter.

Do not deprecate UI Toolkit until:

- at least one playable runtime UI is validated through FairyGUI;
- resource build profile handles FairyGUI packages;
- input and command routing are stable;
- lifecycle cleanup is covered by tests.

## First Vertical Slice Candidate

Recommended first slice: Runtime Showcase HUD or Story dialog.

Runtime Showcase HUD is better for verifying:

- status cards;
- action buttons;
- event log;
- command emission;
- repeated refresh;
- package preload.

Story dialog is better for verifying:

- modal stack;
- choices;
- localized text;
- close / confirm / cancel semantics.

Do not start with a large inventory or character sheet. Those surfaces would hide framework boundary problems under too much UI-specific work.

## Authoring AI Assist And Development Agent Friendliness

This UI route must be designed for agents and authoring assistants from the start. FairyGUI is a strong runtime UI tool, but its exported packages and generated code can become opaque if the framework does not provide a machine-readable contract around them.

### Machine-Readable View Descriptors

Every framework-owned view should have a descriptor that an agent can read without opening FairyGUI Editor:

```text
viewId
packageKey
componentName
layer
modal / keepAlive / closeOnSceneChange
inputScope
requiredResources
viewModelType
commands
diagnosticsTags
```

The descriptor may be generated as C# and optionally mirrored as JSON for external tools. The important requirement is that an agent can answer:

- which package owns this view;
- which component is instantiated;
- which resources must exist;
- which commands the view may emit;
- which ViewModel shape it expects;
- whether the view is modal, persistent or scene-bound.

### Command Schema

UI commands should be schema-backed. For each command, define:

- command id;
- optional typed payload;
- source view id;
- risk level;
- confirmation requirement;
- whether the command is read-only, gameplay-affecting or destructive;
- expected owner that will handle the command.

This mirrors Debug UI command gate rules: the UI layer may expose commands, but the authority remains in the composition root or runtime module that consumes them.

Avoid command paths where a FairyGUI binder directly calls Gameplay, Story, Resources or SaveState services.

### Serializable ViewModel Snapshots

ViewModels used by framework UI should be serializable to stable JSON or a similarly structured snapshot.

Use cases:

- agent-generated fixture data;
- screenshot / visual smoke setup;
- UI diff review;
- regression tests for labels, buttons, empty states and disabled states;
- external authoring tools previewing a runtime panel contract.

ViewModel snapshots should not include Unity objects, FairyGUI objects, delegates, service references or live runtime handles.

### Generated Binding Manifest

FairyGUI generation should produce a small manifest next to generated ids / descriptors:

```text
package: RuntimeHud
component: RuntimeHudView
children:
  - name: strikeButton
    type: GButton
    command: runtimeHud.strike
  - name: eventLog
    type: GList
    binds: eventLog
resources:
  - ui.fairygui.runtimehud.fui
  - ui.fairygui.runtimehud.atlas0
```

This manifest is not for runtime authority. It is for tooling, review and automated validation. Agents should be able to inspect it before modifying a binder or diagnosing a missing UI element.

### Structured Diagnostics

FairyGUI adapter validation must return structured diagnostics instead of only logging to Unity Console.

Diagnostics should include:

- missing package bytes;
- missing atlas or audio clip;
- stale package id / package name mapping;
- missing child binding;
- command declared but not bound;
- ViewModel field not consumed by any binding;
- generated descriptor mismatch;
- duplicate view id;
- invalid layer or modal configuration.

Each diagnostic should include a stable code, severity, target view id, source field and suggested fix when possible.

### Agent-Safe Editing Boundaries

Agents may edit:

- UI core abstractions;
- handwritten adapter binders;
- descriptor source definitions;
- tests;
- documentation;
- generator code.

Agents should not hand-edit:

- FairyGUI exported package bytes;
- atlas textures;
- generated `FUI_*` files;
- generated descriptor files unless the generator is unavailable and the task explicitly allows it.

Agents must not bypass ResourceManager by hardcoding asset paths in binders.

### Testability Without FairyGUI Editor

Implementation should support useful validation without launching FairyGUI Editor:

- descriptor parsing tests;
- duplicate id tests;
- ViewModel snapshot tests;
- command binding tests with fake FairyGUI elements or adapter test doubles;
- resource key validation tests;
- lifecycle open / close / dispose tests.

Full visual authoring remains in FairyGUI Editor, but framework correctness should not depend on manually opening the editor.

### Documentation Requirements

Each FairyGUI-backed framework view should have a short doc or generated report that lists:

- ViewModel type;
- command list;
- required resources;
- validation entry point;
- known visual smoke scene or demo entry.

This makes future Development Agent work issue-first and bounded: the agent can inspect the descriptor and diagnostics before touching implementation.

## Acceptance Criteria

For the first implementation issue after this design:

- `MxFramework.UI` compiles without UnityEngine, UnityEditor, UI Toolkit or FairyGUI references.
- `MxFramework.UI.FairyGUI` is optional and isolated.
- Runtime / Gameplay / Combat / Story core modules do not reference FairyGUI.
- A typed `MxUiViewContract` can validate and open one FairyGUI-backed view through its `MxUiViewDescriptor`.
- The view can bind a UI-neutral ViewModel and emit a typed command.
- Resource loading goes through an injected resource bridge, not direct `Resources.Load`.
- Open / close / dispose removes callbacks and releases package references predictably.
- Tests cover registry, navigation failure, duplicate view id, lifecycle idempotency and command emission.
- View descriptors, command schemas and generated binding manifests are machine-readable.
- ViewModel fixtures can be serialized for agent-driven smoke tests.
- Adapter validation emits structured diagnostics with stable codes.

Suggested verification:

```text
rg -n "FairyGUI" Assets/Scripts/MxFramework/Core Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Gameplay Assets/Scripts/MxFramework/Combat Assets/Scripts/MxFramework/Story -g '*.cs'
dotnet build WGameFramework.sln --no-restore -v minimal
Unity EditMode: MxFramework.Tests.UI.*
Unity Console: 0 compile error
```

## Risks

1. FairyGUI plugin import may introduce project-wide assembly or shader side effects. Keep it behind an adapter and verify compile boundaries immediately.
2. Generated code can become a dumping ground for business logic. Generators must only create ids, descriptors and narrow binding surfaces.
3. Resource ownership can split between FairyGUI and ResourceManager. The resource bridge and build profile validation are mandatory before broad migration.
4. UI Toolkit and FairyGUI can drift into two separate UI models. UI-neutral ViewModel and command DTOs are the shared contract.
5. Debug UI should not be migrated prematurely. It is a developer observation surface and can remain UI Toolkit until runtime formal UI proves the FairyGUI path.

## Open Questions

1. Should `MxFramework.UI` become a long-term top-level module in `Docs/INTERFACES.md`, or stay task-local until M1 lands?
2. Should FairyGUI generated descriptors live under `Assets/Scripts/MxFramework/UI.FairyGUI.Generated/` or under project-level generated code?
3. Should package mapping be a generated C# table, a ScriptableObject adapter, or a Resource catalog entry?
4. Should modal stack and input focus be part of UI core, or stay adapter-specific until the second real view?
5. Should UI command payload use typed command structs only, or allow JSON payload for authoring / external tools?

## Design Conclusion

FairyGUI is a good candidate for formal runtime UI in WGameFramework, but only as an optional adapter. The reusable part from WGame is not its global `UIManager`; it is the package resource bridge, generated binding workflow, view registry ergonomics and lifecycle cleanup discipline.

The first implementation should therefore create the UI-neutral layer before importing or wiring FairyGUI. That keeps the framework reusable and gives FairyGUI a clean place to plug in.
