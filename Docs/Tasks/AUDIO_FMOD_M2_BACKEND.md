# Audio FMOD M2：FMOD Backend MVP

> 状态：Partial / Runtime Scaffold Verified
> 日期：2026-05-10
> 优先级：P0
> 前置任务：`AUDIO_FMOD_M1_CONTRACT.md`
> 前置文档：`Docs/AUDIO_SYSTEM_FMOD.md`
> 接口文档：`Docs/Interfaces/Audio.md`
> 实现边界：`MxFramework.Audio.FMOD` asmdef 已引用官方 FMOD Unity Integration，`MXFRAMEWORK_FMOD` 编译分支已在 Unity 内通过；`AudioRuntimeModule` 和可配置 FMOD Demo Runner 已实现。测试 bank 和真实出声验收尚未完成。

## 目标

在 M1 noEngine contract 之上新增 Unity + FMOD 后端 MVP：

```text
IAudioService
  -> FmodAudioBackend
  -> FMODUnity.RuntimeManager
  -> EventInstance / Bus / VCA
  -> AudioHandle table
  -> AudioDebugSnapshot
```

M2 验证真实运行时后端可以播放 2D one-shot、启动和停止 loop、设置参数、设置 bus volume / mute，并支持 listener / emitter binding。FMOD 类型只允许出现在 `MxFramework.Audio.FMOD`、Editor 或 Demo 外层，不允许泄漏到 `MxFramework.Audio`。

## 建议写入范围

- `Assets/Scripts/MxFramework/Audio.FMOD/`
- `Assets/Scripts/MxFramework/Audio.FMOD/MxFramework.Audio.FMOD.asmdef`
- `Assets/Scripts/MxFramework/Tests/Audio.FMOD/`
- 可选 Demo 场景或 runner 文件
- 必要 `.meta`
- `Docs/Tasks/AUDIO_FMOD_M2_BACKEND.md`
- 必要时更新 `Docs/Interfaces/Audio.md` 的 Implemented 状态

需要主代理或代码代理协调的写入：

- FMOD package / plugin import
- `Assets/Scripts/MxFramework/Tests/MxFramework.Tests.asmdef`
- `Docs/INTERFACES.md`

本文档代理不直接修改上述 package、索引或 asmdef。

## 已实现范围

- 新增 `MxFramework.Audio.FMOD` asmdef，引用 `MxFramework.Audio` 和官方 `FMODUnity` 程序集。
- 新增 `FmodAudioBackend`。
- 新增 `FmodAudioBackendOptions`，包含 missing bus 策略、preload bus ids、recent history 容量。
- 新增 event reference 解析逻辑：优先 guid，path 作为 fallback / debug。
- 新增 one-shot 播放路径。
- 新增 loop / controlled event 路径：`CreateInstance -> set params -> set 3D attributes -> start -> handle table`。
- 新增 `Stop(handle, AudioStopMode)` 到 FMOD stop mode 的映射。
- 新增 `SetParameter(handle, parameterId, value)`。
- 新增 `SetBusVolume` / `SetBusMuted`，初始化时缓存 bus / VCA。
- 新增 handle table 清理逻辑，释放已停止或失效 `EventInstance`。
- 新增 `AudioDebugSnapshot` 填充：active events、bus states、recent errors。
- 新增 `AudioRuntimeModule`，默认在 `RuntimeTickStage.PostSimulation` tick `IAudioService`。
- 新增 `FmodAudioDemoRunner`，可从 Inspector 填入 one-shot / loop event path 或 guid、bus / VCA、parameter 后进行手动 smoke。
- 未定义 `MXFRAMEWORK_FMOD` 时，后端返回 `BackendUnavailable`，保持普通工程可编译；当前工程已启用 `MXFRAMEWORK_FMOD` 并通过 Unity 编译。

## 未实现范围

- 尚未新增 listener / emitter Unity 绑定组件。
- 尚未新增测试 bank 或真实 FMOD integration test。
- 尚未把 Demo Runner 固定挂入 Demo scene。

## 行为契约

- `FmodAudioBackend` 是运行时唯一直接调用 `FMODUnity.RuntimeManager` 和 `FMOD.Studio` 的适配层。
- 运行时优先使用 `AudioEventDefinition.FmodEventGuid` 创建 event；guid 无效时才使用 path fallback。
- `IsLoop=true` 的 event 必须进入 handle table。
- `PlayOneShot` 可以内部使用 `RuntimeManager.PlayOneShot`，但仍要经过 `IAudioService` 的限流、校验和诊断。
- `Stop(handle, AllowFadeout)` 映射到 FMOD allow fadeout；`Immediate` 映射到 immediate。
- 释放 FMOD `EventInstance` 后，handle 状态必须进入 stopped / released 语义。
- emitter 绑定对象销毁后，相关 handle 必须停止或解绑，并写入 recent error / event。
- 3D event 没有 emitter 或位置时，默认返回结构化失败，除非 definition 明确允许 fallback。
- bus 和 VCA 初始化失败按 options 决定 fail fast 或降级；无论哪种都必须进入诊断。
- `Audio.FMOD` 可以依赖 UnityEngine / FMOD；`Audio` contract 不允许新增这些依赖。

## Bank / Warmup 边界

M2 只做最小后端验证，不建立完整 bank catalog。

允许：

- 使用 FMOD Settings 自动加载基础 bank。
- 使用小型测试 / Demo bank 验证 event、bus、parameter。
- 手动 warmup 少量关键 event。

不做：

- 不新增 `FmodBankProvider`。
- 不扩展 `ResourceTypeIds.FmodBank`。
- 不实现 Mod / DLC bank catalog。
- 不实现完整 scene audio warmup 配置。

## 测试计划

测试入口：

```text
Assets/Scripts/MxFramework/Tests/Audio/FmodAudioBackendAvailabilityTests.cs
Assets/Scripts/MxFramework/Tests/Audio/AudioRuntimeModuleTests.cs
```

测试可分两层：

1. 编译 / 边界测试，不要求真实 bank。
2. FMOD integration 测试，要求测试 bank 或 Demo bank 存在。

已覆盖：

- 未安装 FMOD 时 `MxFramework.Audio` 仍可编译，M1 测试不受影响。
- `FmodAudioBackend.Initialize` availability 测试会按当前编译 symbol 验证成功初始化或 `BackendUnavailable` 降级。
- 已导入官方 FMOD Unity Integration 后，`MxFramework.Audio.FMOD` 可在 Unity 内编译。
- `AudioRuntimeModule` 默认 stage、service registry 解析和 tick 转发。

真实 FMOD package 导入后仍需覆盖：

- event guid / path 缺失返回 `BackendFailed`。
- one-shot 成功路径、loop start / stop、parameter、bus volume / mute。
- emitter binding 和 listener 更新。
- snapshot 包含 active event、bus state 和 recent errors。

## Demo 验收建议

建议最小 Demo 覆盖：

```text
UI click 2D one-shot
combat cast one-shot
hit 3D SFX at position or emitter
loop aura start / stop
SFX bus volume slider or scripted adjustment
```

Demo 只展示框架用法，不承载框架核心逻辑。

当前可用 Demo 入口：

```text
Assets/Scripts/MxFramework/Demo/Audio/FmodAudioDemoRunner.cs
```

运行方式：

- 在场景中挂 `MxFramework/Audio/FMOD Audio Demo Runner`。
- 填入 FMOD Studio 已导出 bank 中存在的 one-shot / loop event path 或 guid。
- 如需 bus / VCA 控制，填入 `bus:/...` 或 `vca:/...`。
- 使用 Context Menu 或默认按键 `Alpha1` / `Alpha2` 触发 one-shot 和 loop。

## 验证建议

已执行：

```text
dotnet build <temp AudioCompile.csproj>
dotnet build <temp AudioTestsCompile.csproj>
Unity refresh + compile
Unity EditMode: MxFramework.Tests.Audio.FmodAudioBackendAvailabilityTests.Initialize_ReturnsExpectedAvailabilityForCompileSymbol
Unity EditMode: MxFramework.Tests.Audio
```

结果：

- dotnet 临时编译：0 warnings, 0 errors。
- Unity Console errors：0。
- Unity EditMode unavailable-path test：passed。
- 导入官方 FMOD Unity Integration 后，Unity Console errors：0。
- Unity EditMode `MxFramework.Tests.Audio`：10 total, 10 passed, 0 failed。

尚未执行真实 PlayMode smoke test；当前项目缺测试 bank / Demo bank。

当前 FMOD Settings 状态：

- `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset` 已存在。
- 未发现任何 `.bank`、`.strings.bank` 或 `.fspro`。
- `MasterBanks`、`Banks`、`BanksToLoad` 为空。
- `ImportType=StreamingAssets`，`BankLoadType=All`。

真实出声验收至少需要提供单平台 bank 目录，包含 `Master.bank`、`Master.strings.bank` 和一个包含 Demo event 的业务 bank。

## 不做范围

- 不修改 `MxFramework.Audio` 的 noEngine 边界。
- 不让 Gameplay / Combat 直接引用 FMOD。
- 不实现 Editor validator。
- 不实现 ConfigAudioDefinitionProvider。
- 不实现 bank catalog / Mod bank loading。
- 不把 audio command 写入 Runtime result hash。
- 不做完整 Debug 面板。

## 验收标准

- `MxFramework.Audio.FMOD` 能在安装 FMOD 的 Unity 工程中编译。
- `MxFramework.Audio` 在未安装 FMOD 时仍能独立编译和测试。
- Demo 或 integration 测试能播放 2D、3D、loop，并能停止 loop。
- bus volume / mute 可运行时调整。
- FMOD event / bus / parameter 缺失会返回结构化错误并进入 snapshot。
- `Gameplay`、`Combat`、`Ability` 源码中没有直接引用 `FMODUnity` 或 `FMOD.Studio`。

## 下一步

进入后续任务：

```text
Audio FMOD M3：Config + Editor Validation
Audio FMOD M4：Bank Warmup + Diagnostics
```

M3 固定 audio config schema 和 FMOD event / bus / parameter 校验。M4 再接 scene warmup、Diagnostics debug source 和调试面板。
