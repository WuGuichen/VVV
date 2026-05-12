# AppFlow / SceneFlow 接口

> 状态：App / Scene Flow v0.1 foundation。本文记录 `MxFramework.Runtime` 的 noEngine 流程契约，以及 `MxFramework.Runtime.Unity` 对 Unity SceneManager 的适配边界。

## 职责

AppFlow 提供游戏 App 状态流转骨架；SceneFlow 提供场景加载、卸载、进度、失败结果和诊断骨架。两者都属于 runtime contract，不写入具体项目枚举、场景路径、Loading UI、联网会话或资源 warmup 策略。

`MxFramework.Runtime` 必须保持 `noEngineReferences=true`，不得引用 `UnityEngine` 或 `UnityEditor`。Unity 场景加载只通过 `MxFramework.Runtime.Unity.UnitySceneFlowDriver` 进入，项目层负责把稳定 scene key 映射到 Unity 可加载的 scene name 或 path。

## 模块边界

| 模块 | 依赖 | 职责 |
|------|------|------|
| `MxFramework.Runtime` | BCL only | AppFlow / SceneFlow contract、controller、snapshot、runtime module |
| `MxFramework.Runtime.Unity` | Runtime、UnityEngine | `UnitySceneFlowDriver`，封装 `SceneManager.LoadSceneAsync` / `UnloadSceneAsync` |
| Project composition root | Runtime、Runtime.Unity、项目配置 | 注册状态、建立 scene key 映射、把 module 注册进 `RuntimeHost` |

依赖方向：

```text
MxFramework.Runtime
  <- MxFramework.Runtime.Unity
      <- Demo / Preview / project composition root
```

## AppFlow API

| 接口/类型 | 用途 |
|-----------|------|
| `IAppFlowState` | 项目层 App 状态节点，暴露 `StateId`、`Enter`、`Tick`、`Exit` |
| `AppFlowController` | 注册状态、启动初始状态、登记 pending transition、按 Tick 推进 |
| `AppFlowTransition` | 一次状态切换的 from / to / reason 记录 |
| `AppFlowTransitionResult` | 非异常切换结果，包含成功或结构化错误 |
| `AppFlowSnapshot` | 当前状态、pending transition、启动状态和最近错误的诊断快照 |
| `AppFlowRuntimeModule` | RuntimeHost 模块入口，在固定 Tick 阶段调用 controller |

约定：

- `RegisterState` 使用项目层稳定 `StateId`，重复 ID 返回结构化失败或抛出明确异常，不能静默覆盖。
- `Start(initialStateId, reason)` 立即进入初始状态；未注册状态必须可诊断。
- `RequestTransition(targetStateId, reason)` 只登记 pending transition；真正的 `Exit` / `Enter` 在下一次 `Tick` 开始时执行，避免 state 在自身 `Tick` 中被重入退出。
- 同一时间只允许一个 pending transition；重复请求返回结构化失败。
- AppFlow 不定义 `Boot`、`MainMenu`、`Gameplay` 等业务枚举，这些 ID 由项目层注册。

## SceneFlow API

| 接口/类型 | 用途 |
|-----------|------|
| `SceneFlowRequest` | 加载请求，包含稳定 `SceneKey`、load mode 和是否卸载上一场景 |
| `ISceneFlowDriver` | 场景后端抽象，提供 `LoadScene(request)` 与 `UnloadScene(sceneKey)` |
| `ISceneFlowOperation` | 后端异步操作抽象，暴露 `SceneKey`、完成状态、进度、`Success` 和 `Error` |
| `SceneFlowController` | 串行编排加载、busy 拒绝、成功后 active scene 更新和可选卸载旧 scene |
| `SceneFlowResult` | 请求或操作结果，包含成功状态或 `SceneFlowErrorCode` |
| `SceneFlowSnapshot` | 当前 active scene、busy 状态、进度、pending request 和 last result |
| `SceneFlowRuntimeModule` | RuntimeHost 模块入口，在固定 Tick 阶段推进 SceneFlow |

约定：

- `SceneFlowRequest.SceneKey` 是稳定 key，不要求等于 Unity scene path；driver 或项目 composition root 决定映射。
- 正在加载或卸载时拒绝新请求，返回 `SceneFlowErrorCode.Busy`。
- scene key 为空、空白或映射失败时返回 `SceneFlowErrorCode.InvalidRequest`。
- driver 调用失败、返回 null operation 或 Unity async operation 返回 null 时返回 `SceneFlowErrorCode.DriverFailure`。
- Load 成功后更新 `ActiveSceneKey`；`UnloadPreviousScene=true` 且存在上一个 active scene 时，再启动 unload 操作。
- Driver 失败时保留上一 active scene，并把失败记录到 `LastResult`。

## Unity Adapter 边界

`UnitySceneFlowDriver` 位于：

```text
Assets/Scripts/MxFramework/Runtime.Unity/UnitySceneFlowDriver.cs
```

它是唯一允许直接调用 Unity `SceneManager` 的 App / Scene Flow 运行时文件：

- `LoadScene` 校验 `SceneKey` 后调用 `SceneManager.LoadSceneAsync(sceneKey, loadMode)`。
- `UnloadScene` 校验 `sceneKey` 后调用 `SceneManager.UnloadSceneAsync(sceneKey)`。
- Unity 返回 null 或抛出异常时，转换为已完成失败 `ISceneFlowOperation`。
- Unity adapter 不实现 loading UI，不直接注册项目状态，不修改 RuntimeHost 生命周期语义。
- Addressables、AssetBundle scene、项目自定义 scene manifest 应作为后续独立 driver 或 composition root 映射，不放进 noEngine contract。

## noEngine 边界

以下类型不能出现在 `MxFramework.Runtime`：

```text
UnityEngine
UnityEditor
UnityEngine.SceneManagement.SceneManager
UnityEngine.AsyncOperation
```

Runtime 层只保存字符串 key、枚举、数值进度、结构化错误和 snapshot。任何 Unity scene name、build settings、path、AssetDatabase、Addressables handle 或 `AsyncOperation` 都必须留在 Unity adapter 或项目组合根。

## 测试入口

noEngine 核心测试：

```text
Assets/Scripts/MxFramework/Tests/Runtime/AppFlowTests.cs
Assets/Scripts/MxFramework/Tests/Runtime/SceneFlowTests.cs
```

建议验证命令：

```text
dotnet build MxFramework.Tests.csproj --no-restore
rg "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Runtime
rg "SceneManager" Assets/Scripts/MxFramework/Runtime Assets/Scripts/MxFramework/Runtime.Unity
```

Unity adapter 可通过最小 PlayMode 或 EditMode fixture 验证：传入无效 scene key 应立即得到失败 operation；传入 build settings 中存在的 scene key 时，operation 进度应随 Unity async operation 推进并最终成功。
