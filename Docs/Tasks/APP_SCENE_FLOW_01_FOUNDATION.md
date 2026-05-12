# App / Scene Flow 01: Runtime Flow Foundation

> 状态：Implemented / Verified
> 日期：2026-05-11
> 优先级：P0
> 前置：`RUNTIME_FOUNDATION_01_RUNTIME_HOST.md`

## 目标

建立第一版 App / Scene Flow 框架，让项目层可以用稳定契约表达：

- 游戏 App 状态流转：Boot、MainMenu、Loading、Gameplay、Paused、Shutdown 等由项目层注册，不写死业务枚举。
- Scene 切换请求：目标 scene key、加载模式、是否卸载前一个 scene、加载进度和失败原因。
- RuntimeHost 接入：通过 RuntimeModule 在固定 Tick 阶段推进 AppFlow 和 SceneFlow。
- Unity SceneManager 适配：Unity 侧只作为 driver，不进入 noEngine runtime contract。

本任务只做框架底座，不迁移现有 Demo 场景，也不实现产品级 loading UI。

## 为什么先做

RuntimeHost 已解决“模块如何启动和每帧推进”，但没有解决“游戏如何从启动进入菜单、从菜单进入关卡、关卡切换时如何暂停/恢复运行时模块”。继续推进 Input Command、Save/Replay、资源 warmup、Presentation 前，需要一个可测试的 App/Scene Flow 契约作为上层流程骨架。

## 架构边界

### 做

- 在 `MxFramework.Runtime` 中新增 noEngine AppFlow / SceneFlow contract。
- AppFlow 支持注册状态、启动初始状态、请求转移、Tick、诊断快照。
- SceneFlow 支持请求加载、忙碌拒绝、进度、成功/失败结果、可选卸载上一场景。
- 提供 `AppFlowRuntimeModule` 和 `SceneFlowRuntimeModule`。
- 新增 `MxFramework.Runtime.Unity` 适配层，封装 `UnityEngine.SceneManagement.SceneManager`。
- 增加纯 C# 测试覆盖 AppFlow 和 SceneFlow 核心。
- 同步 `CAPABILITIES.md`、`INTERFACES.md` 和新增 `Interfaces/AppFlow.md`。

### 不做

- 不做 UI Loading Screen。
- 不做 Addressables 场景加载；后续可新增独立 driver。
- 不做联网 Session / Matchmaking。
- 不把 WGame 场景、关卡、存档或剧情规则写入框架。
- 不让 `MxFramework.Runtime` 引用 `UnityEngine` 或 `UnityEditor`。
- 不修改现有 RuntimeHost 生命周期语义。

## 建议目录

```text
Assets/Scripts/MxFramework/Runtime/
  AppFlow.cs
  SceneFlow.cs

Assets/Scripts/MxFramework/Runtime.Unity/
  MxFramework.Runtime.Unity.asmdef
  UnitySceneFlowDriver.cs

Assets/Scripts/MxFramework/Tests/Runtime/
  AppFlowTests.cs
  SceneFlowTests.cs

Docs/Interfaces/
  AppFlow.md
```

## 核心 API 草案

### AppFlow

```csharp
public interface IAppFlowState
{
    string StateId { get; }
    void Enter(AppFlowStateContext context, AppFlowTransition transition);
    void Tick(AppFlowTickContext context);
    void Exit(AppFlowStateContext context, AppFlowTransition transition);
}

public sealed class AppFlowController
{
    void RegisterState(IAppFlowState state);
    AppFlowTransitionResult Start(string initialStateId, string reason = null);
    AppFlowTransitionResult RequestTransition(string targetStateId, string reason = null);
    void Tick(long frameIndex, double deltaTime, double elapsedTime);
    AppFlowSnapshot CaptureSnapshot();
}
```

语义：

- `Start` 立即进入初始状态。
- `RequestTransition` 只登记 pending transition；下一次 `Tick` 开始时执行，避免 state 在自身 Tick 中被重入退出。
- 同一时间只允许一个 pending transition；重复请求返回结构化失败。
- 未注册状态、重复状态 ID、未启动 Tick 都必须可诊断。

### SceneFlow

```csharp
public interface ISceneFlowDriver
{
    ISceneFlowOperation LoadScene(SceneFlowRequest request);
    ISceneFlowOperation UnloadScene(string sceneKey);
}

public sealed class SceneFlowController
{
    SceneFlowResult RequestLoad(SceneFlowRequest request);
    void Tick();
    SceneFlowSnapshot CaptureSnapshot();
}
```

语义：

- `SceneFlowRequest.SceneKey` 是稳定场景 key，不要求等于 Unity scene path；具体映射由 driver 或项目层负责。
- 正在加载/卸载时拒绝新请求，返回 `SceneFlowErrorCode.Busy`。
- Load 成功后更新 `ActiveSceneKey`。
- `UnloadPreviousScene=true` 且存在上一个 active scene 时，Load 成功后启动 unload 操作。
- Driver 失败时保留上一 active scene，并记录 `LastResult`。

## 子代理分工

### Worker A：noEngine AppFlow Core

所有权：

- `Assets/Scripts/MxFramework/Runtime/AppFlow.cs`
- `Assets/Scripts/MxFramework/Tests/Runtime/AppFlowTests.cs`

任务：

- 实现 AppFlow contract、controller、snapshot、runtime module。
- 测试覆盖注册、启动、pending transition、重入请求、未注册状态、RuntimeModule Tick。

### Worker B：noEngine SceneFlow Core

所有权：

- `Assets/Scripts/MxFramework/Runtime/SceneFlow.cs`
- `Assets/Scripts/MxFramework/Tests/Runtime/SceneFlowTests.cs`

任务：

- 实现 SceneFlow request/result/error、driver/operation contract、controller、snapshot、runtime module。
- 用 fake driver 测试加载成功、加载失败、busy 拒绝、卸载上一场景、RuntimeModule Tick。

### Worker C：Unity Adapter + Docs Index

所有权：

- `Assets/Scripts/MxFramework/Runtime.Unity/`
- `Docs/Interfaces/AppFlow.md`

任务：

- 新增 `MxFramework.Runtime.Unity` asmdef。
- 实现 `UnitySceneFlowDriver`，仅在该程序集引用 UnityEngine SceneManagement。
- 写接口文档，说明 noEngine 和 Unity adapter 的边界。

## 验收

- `MxFramework.Runtime.asmdef` 保持 `noEngineReferences=true`。
- `rg "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Runtime` 没有命中。
- 纯 C# AppFlow / SceneFlow 测试覆盖核心状态机。
- Unity SceneManager 调用只存在于 `MxFramework.Runtime.Unity`。
- `CAPABILITIES.md` 能查到 App / Scene Flow v0.1。

## 验证命令

```text
dotnet build MxFramework.Tests.csproj --no-restore
rg "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Runtime
rg "SceneManager" Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Runtime.Unity
```

## 当前实现记录

2026-05-11 App / Scene Flow v0.1 已落地：

- 新增 `MxFramework.Runtime` noEngine AppFlow contract：`IAppFlowState`、`AppFlowController`、`AppFlowTransition`、`AppFlowTransitionResult`、`AppFlowSnapshot`、`AppFlowRuntimeModule`。
- 新增 `MxFramework.Runtime` noEngine SceneFlow contract：`SceneFlowRequest`、`ISceneFlowDriver`、`ISceneFlowOperation`、`SceneFlowController`、`SceneFlowResult`、`SceneFlowSnapshot`、`SceneFlowRuntimeModule`。
- 新增 `MxFramework.Runtime.Unity` 程序集和 `UnitySceneFlowDriver`，Unity `SceneManager` 调用只存在于 Unity adapter。
- 新增接口文档 `Docs/Interfaces/AppFlow.md`，并接入 `CAPABILITIES.md`、`INTERFACES.md`、`README.md`。
- 新增 `AppFlowTests` 和 `SceneFlowTests`，覆盖状态注册/切换、Tick 内 pending transition、busy 拒绝、加载失败保留旧 active scene、卸载上一场景和 RuntimeHost module tick。

验证：

```text
dotnet restore MxFramework.Tests.csproj
dotnet build MxFramework.Tests.csproj --no-restore
rg "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Runtime
rg "SceneManager" Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Runtime.Unity
```

剩余风险：

- 尚未用 Unity Editor / PlayMode 打开真实 build settings scene 验证 `UnitySceneFlowDriver` 的异步加载。
- 生成式 `.csproj` 在 Unity 重新生成前可能不包含新增源码；实际 Unity asmdef 会按目录编译。
- Scene key 到 Unity scene name/path 的映射仍由项目层 composition root 或后续 scene manifest 负责。
