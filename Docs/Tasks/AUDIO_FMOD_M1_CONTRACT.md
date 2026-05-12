# Audio FMOD M1：noEngine Contract + Null Backend

> 状态：已实现（2026-05-10）
> 日期：2026-05-10
> 优先级：P0
> 前置文档：`Docs/AUDIO_SYSTEM_FMOD.md`
> 接口文档：`Docs/Interfaces/Audio.md`
> 实现边界：`MxFramework.Audio` noEngine contract、`NullAudioBackend` 和基础测试已实现；FMOD 真实播放不属于 M1。

## 目标

建立音频系统的 noEngine 最小契约闭环：

```text
AudioEventId
  -> AudioEventDefinition / AudioBusDefinition / AudioParameterDefinition
  -> IAudioDefinitionProvider
  -> IAudioService / AudioService
  -> IAudioBackend
  -> NullAudioBackend
  -> AudioHandle / AudioResult / AudioDebugSnapshot
```

M1 不接入 UnityEngine、不接入 FMOD、不加载真实 bank，不处理 `Transform`、`GameObject` 或 `EventInstance`。它只固定公共契约、错误语义、句柄生命周期、Bus 状态和诊断快照。

## 已实现范围

- 新增 `MxFramework.Audio` asmdef，并保持 `noEngineReferences=true`。
- 新增定义 DTO：`AudioEventDefinition`、`AudioBusDefinition`、`AudioParameterDefinition`。
- 新增播放请求 DTO：`AudioPlayRequest`、`AudioPlayMode`、`AudioTransform`。
- 新增句柄与状态：`AudioHandle`、`AudioHandleState`、`AudioStopMode`。
- 新增结果模型：`AudioResult`、`AudioPlayResult`、`AudioErrorCode`。
- 新增 `IAudioDefinitionProvider`。
- 新增 `IAudioService` 和 `AudioService`，负责定义查询、基础校验、后端初始化状态检查和转发。
- 新增 `IAudioBackend`。
- 新增 `NullAudioBackend`，用于服务器、EditMode 测试和未安装 FMOD 的 Demo。
- 新增 `AudioDebugSnapshot`、`AudioDebugBusState`、`AudioDebugActiveEvent`、`AudioDebugError`。
- 新增 `Assets/Scripts/MxFramework/Tests/Audio/AudioServiceTests.cs`。
- 新增 `Assets/Scripts/MxFramework/Tests/Audio/FmodAudioBackendAvailabilityTests.cs`，覆盖 FMOD symbol availability 路径。
- `MxFramework.Tests.asmdef` 已追加 `MxFramework.Audio` 和 `MxFramework.Audio.FMOD` 引用。

## 行为契约

- `AudioEventDefinition.Id == 0` 保留为 invalid。
- 未注册 event 返回 `AudioErrorCode.InvalidEvent`。
- 未注册 bus 返回 `AudioErrorCode.InvalidBus`。
- 未注册 parameter 返回 `AudioErrorCode.InvalidParameter`。
- 后端未初始化返回 `AudioErrorCode.NotInitialized`。
- `PlayOneShot` 默认不暴露可控 handle；返回结果可包含 invalid handle。
- `StartEvent` 成功时必须返回有效 `AudioHandle`。
- `IsLoop=true` 的 event 不允许通过不可控 one-shot 语义启动。
- `Stop(handle, stopMode)` 幂等；重复 stop 不抛异常，应记录 recent error。
- `SetParameter` 只接受 definition 中声明的参数。
- `SetBusVolume` 接受 `0..1`；越界策略需明确为 clamp 或 validation failure，测试必须固定。
- `SetBusMuted` 不改变保存的 volume。
- `AudioDebugSnapshot` 只读、低频分配，不进入每帧热路径。
- `AudioService`、`NullAudioBackend` 和 DTO 不引用 Unity / FMOD / Editor 类型。

## Replay / Hash 边界

- M1 不让音频参与 `IRuntimeHashContributor`。
- 如后续需要 replay 诊断，只记录音频请求摘要，不记录 FMOD 播放结果。
- 音频失败默认不改变 gameplay command 执行结果。

## 测试计划

测试入口：

```text
Assets/Scripts/MxFramework/Tests/Audio/AudioServiceTests.cs
Assets/Scripts/MxFramework/Tests/Audio/FmodAudioBackendAvailabilityTests.cs
```

已覆盖：

- `AudioService` 可用 memory definition provider 播放 one-shot，且不向调用方暴露 handle。
- `StartEvent` 对 loop event 返回有效 handle，并出现在 snapshot active events。
- loop event 不能通过 one-shot 语义启动。
- 缺失 parameter 返回 `InvalidParameter`。
- bus volume 边界值行为稳定。
- `NullAudioBackend.CaptureSnapshot()` 包含 initialized、active event、bus state 和 errors。
- 编译验证 `MxFramework.Audio` 不依赖 UnityEngine / FMOD。
- 未定义 `MXFRAMEWORK_FMOD` 时 `FmodAudioBackend.Initialize` 返回 `BackendUnavailable`。

## 验证建议

已执行：

```text
dotnet build <temp AudioCompile.csproj>
dotnet build <temp AudioTestsCompile.csproj>
Unity refresh + compile
Unity EditMode: MxFramework.Tests.Audio exact test set
```

结果：

- dotnet 临时编译：0 warnings, 0 errors。
- Unity Console errors：0。
- Unity EditMode Audio tests：7 total, 7 passed, 0 failed。

当前工程未安装 FMOD 包，`MxFramework.Audio.FMOD` 通过未定义 `MXFRAMEWORK_FMOD` 的 stub 路径编译。

## 不做范围

- 不安装或引用 FMOD Unity package。
- 不新增 `MxFramework.Audio.FMOD`。
- 不加载 bank 或 samples。
- 不绑定 listener / emitter 到 Unity 对象。
- 不新增 Editor validator。
- 不接入 `RuntimeHost`。
- 不修改 Gameplay / Combat / Ability 代码。
- 不把 Audio 写入 Runtime hash。

## 验收标准

- 不安装 FMOD 时，`MxFramework.Audio` 可独立编译和测试。
- noEngine contract 中没有 Unity / FMOD / Editor 类型。
- `IAudioService`、`IAudioBackend`、`IAudioDefinitionProvider` 的错误语义可测试。
- `NullAudioBackend` 能支持服务器、测试和未接入 FMOD 的环境。
- `Docs/Interfaces/Audio.md` 明确 Proposed / Implemented 边界。

## 下一步

进入 M2：

```text
Audio FMOD M2：FMOD Backend MVP
```

目标是在 M1 contract 上新增 `MxFramework.Audio.FMOD`，验证 FMOD one-shot、loop handle、stop、parameter、bus volume 和 listener / emitter binding。
