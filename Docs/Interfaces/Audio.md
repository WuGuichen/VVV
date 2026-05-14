# Audio 接口

> 状态：M1 Implemented / FMOD Runtime Scaffold
> 来源：`Docs/AUDIO_SYSTEM_FMOD.md`
> 实现边界：`MxFramework.Audio` noEngine contract、`NullAudioBackend` 和基础测试已落地；`MxFramework.Audio.FMOD` 已引用官方 FMOD Unity Integration，并在 `MXFRAMEWORK_FMOD` 下编译真实后端路径；`AudioRuntimeModule` 和 FMOD Demo Runner 已接入 RuntimeHost。缺测试 bank / Demo bank 时仍未声明真实出声验收。

## 职责

Audio 提供纯 C# 的音频意图、播放请求、句柄、Bus 状态、后端抽象和诊断快照契约。`MxFramework.Audio` 必须保持 `noEngineReferences=true`，不依赖 `UnityEngine`、`UnityEditor`、`FMODUnity` 或 `FMOD.Studio`。

FMOD 接入放在独立的 `MxFramework.Audio.FMOD` 适配层中。Gameplay、Combat、Ability、Buff、UI 等模块只通过 `IAudioService` 或项目层 cue mapper 表达音频意图，不直接持有 FMOD `EventInstance`，也不直接调用 `RuntimeManager`。

## 模块边界

| 模块 | 状态 | 依赖 | 职责 |
|------|------|------|------|
| `MxFramework.Audio` | Implemented | BCL only | 稳定 ID、请求、结果、句柄、定义 provider、后端接口、Null 后端、诊断 DTO |
| `MxFramework.Audio.FMOD` | Runtime Scaffold | Audio、Runtime、FMODUnity；`MXFRAMEWORK_FMOD` 下使用 UnityEngine / FMODUnity | FMOD event / bus / VCA 适配，提供 `AudioRuntimeModule`；未定义 symbol 时返回 BackendUnavailable |
| `MxFramework.Audio.Editor` | Proposed | Audio、Audio.FMOD、Config、UnityEditor | FMOD event / bus / parameter / bank 校验和调试工具 |
| `MxFramework.Demo` | Scaffolded | Audio、Audio.FMOD、Runtime | `FmodAudioDemoRunner`，用于填入 FMOD event / bus 后验证 one-shot、loop、parameter、bus volume |

依赖方向：

```text
Core
  <- Audio
      <- Audio.FMOD
          <- Demo / Preview.Runtime / project composition root

Runtime
  <- project composition root
      -> Audio.FMOD RuntimeModule
```

`MxFramework.Audio` 不引用 `Runtime`。需要生命周期和 Tick 的后端模块放在 `Audio.FMOD`，由 Unity composition root 注册进 `RuntimeHost`。

## 公开接口

| 接口/类型 | 状态 | 用途 |
|-----------|------|------|
| `AudioEventDefinition` | Implemented | EventId 到 FMOD path / guid、kind、bus、3D、loop、参数和 labels 的定义 |
| `AudioBusDefinition` | Implemented | Bus / VCA 稳定 ID 到 FMOD bus path / VCA path 的映射 |
| `AudioParameterDefinition` | Implemented | 参数 ID、名称、默认值和值域定义 |
| `AudioPlayRequest` | Implemented | 播放请求，包含 event、位置、emitter、参数、priority、trace 和 play mode |
| `AudioTransform` | Implemented | noEngine 位置和朝向数值，不暴露 `Vector3` |
| `AudioHandle` | Implemented | loop 或可控事件句柄，不暴露 FMOD 原生对象 |
| `AudioResult` / `AudioPlayResult` | Implemented | 非异常成功/失败结果和错误码 |
| `AudioErrorCode` | Implemented | `InvalidEvent`、`NotInitialized`、`InvalidParameter` 等结构化错误 |
| `IAudioService` / `AudioService` | Implemented | 业务侧入口，负责基础校验和后端转发 |
| `IAudioBackend` | Implemented | 可替换后端契约，首个真实后端为 FMOD |
| `NullAudioBackend` | Implemented | noEngine 测试、服务器和未安装 FMOD 环境的空后端 |
| `IAudioDefinitionProvider` | Implemented | Event / Bus / Parameter 定义查询入口 |
| `AudioDebugSnapshot` | Implemented | active events、bus states、recent errors 和计数 |
| `AudioRuntimeModule` | Implemented | RuntimeHost PostSimulation 阶段统一 tick `IAudioService` |
| `AudioDebugSource` | Proposed | 将音频快照接入 Diagnostics 的只读 debug source |

建议首版服务契约：

```csharp
public interface IAudioService
{
    AudioPlayResult PlayOneShot(in AudioPlayRequest request);
    AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle);
    AudioResult Stop(AudioHandle handle, AudioStopMode stopMode);
    AudioResult SetParameter(AudioHandle handle, int parameterId, float value);
    AudioResult SetBusVolume(int busId, float volume);
    AudioResult SetBusMuted(int busId, bool muted);
}
```

建议首版后端契约：

```csharp
public interface IAudioBackend
{
    AudioResult Initialize(IAudioDefinitionProvider definitions);
    AudioPlayResult Play(in AudioPlayRequest request, out AudioHandle handle);
    AudioResult Stop(AudioHandle handle, AudioStopMode stopMode);
    AudioResult SetParameter(AudioHandle handle, int parameterId, float value);
    AudioResult SetBusVolume(int busId, float volume);
    AudioResult SetBusMuted(int busId, bool muted);
    AudioDebugSnapshot CaptureSnapshot();
    void Tick(float deltaTime);
    void Dispose();
}
```

定义查询契约：

```csharp
public interface IAudioDefinitionProvider
{
    bool TryGetEvent(int eventId, out AudioEventDefinition definition);
    bool TryGetBus(int busId, out AudioBusDefinition definition);
    bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition);
}
```

## 使用约定

- `AudioEventDefinition.Id` 使用稳定 int，建议 ID 空间为 `500000-599999`；`Name` 只用于调试。
- 音频配置保存 `AudioEventDefinition.Id`、FMOD event path / guid、bus path、参数名和必要的 Unity 资源 key，不保存场景对象引用。
- `FmodEventGuid` 优先用于运行时解析；`FmodEventPath` 用于 Editor 可读性和 fallback。
- FMOD event path / guid 不是 generic `ResourceKey`；Gameplay、Combat、Ability、Buff、UI 等模块通过 audio event id 或项目层 cue mapper 引用声音。
- Unity `AudioClip` 样例可使用普通 Resource Catalog 和 `ResourceTypeIds.AudioClip`；FMOD bank `.bank` 文件不作为普通 `ResourceCatalogEntry`，也不伪装成 `AudioClip`。
- `IsLoop=true` 的 event 必须通过 `StartEvent` 返回 handle，并通过 `Stop` 释放。
- `PlayOneShot` 默认不返回可持有 handle；需要后续控制时使用 `StartEvent`。
- `Stop(handle, stopMode)` 幂等；重复停止或释放应返回结构化结果并写入诊断，不抛异常。
- 3D event 必须提供 emitter 绑定或 `AudioTransform`；缺失时按配置返回失败或降级到 listener 位置。
- Unity 侧 `AudioEmitterBinding` 可绑定 `Transform`、`GameObject`、`Rigidbody` 或 `Rigidbody2D`，但这些类型不能进入 `MxFramework.Audio`。
- Bus volume 范围统一为 `0..1`；mute 与 volume 分开记录。
- 音频失败默认不打断 gameplay 帧；Editor / CI 校验中缺失 event、parameter、bus、bank 应失败。
- 音频不参与 Runtime result hash；Replay 可记录 command 摘要用于诊断，也可以忽略实际音频输出。
- `AudioDebugSnapshot` 是低频调试接口，不作为高频状态同步入口。
- Gameplay / Combat 源码不得直接引用 `FMODUnity` 或 `FMOD.Studio`。

## FMOD 后端约定

- `FmodAudioBackend` 是唯一直接调用 FMOD Unity Integration 的运行时适配层。
- One-shot 可在后端内部使用 `RuntimeManager.PlayOneShot`，但业务层不直接调用。
- Loop、蓄力、引导、aura、环境声等可控事件使用 `CreateInstance -> start -> handle table -> Stop -> release`。
- `AudioHandle.Id` 由后端分配，映射到内部 `EventInstance` 表。
- `AudioRuntimeModule` 默认运行在 `RuntimeTickStage.PostSimulation`，使同一帧 gameplay 先产生命令，再统一转为音频输出。
- Bank 首期使用 FMOD Settings 自动加载 + 显式 warmup。当前 demo 的 `FMOD/.../Build/Desktop/Master*.bank` 是版本化 authoring build output / release input，`Assets/StreamingAssets/Master*.bank` 是版本化 runtime mirror；Catalog 化 bank 后续再接入 Resources。
- `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` 当前是版本化的 FMOD Unity generated metadata，用于记录 Editor 可见 bank、event、guid 和路径；刷新后可提交，但不能把它当作手写配置源。
- `Audio.FMOD` 可以依赖 Runtime 以提供 `AudioRuntimeModule`，但 `Audio` contract 不依赖 Runtime。

## 诊断约定

`AudioDebugSnapshot` 建议包含：

```text
Initialized
TotalPlayRequests
TotalStopRequests
ActiveEventCount
BusStates[]
ActiveEvents[]
RecentErrors[]
```

Debug 面板和导出工具只能读取 snapshot，不读取 `FmodAudioBackend` 私有 handle table。

## 测试入口

状态：M1 tests implemented，M2 runtime scaffold verified。

建议测试入口：

```text
Assets/Scripts/MxFramework/Tests/Audio/
```

M1 noEngine 契约测试已覆盖：

- `NullAudioBackend` 可初始化、播放、停止并输出 snapshot。
- 未注册 parameter 返回 `AudioErrorCode.InvalidParameter`。
- loop event 必须返回有效 handle。
- one-shot loop event 被 `AudioService` 拒绝，要求走 `StartEvent`。
- bus volume clamp / validation 行为稳定。
- `AudioService` 不依赖 Unity 或 FMOD 类型。
- `FmodAudioBackend` availability 测试会按 `MXFRAMEWORK_FMOD` 编译 symbol 验证成功初始化或 `BackendUnavailable` 降级。
- `AudioRuntimeModule` 测试覆盖默认 PostSimulation stage、从 Runtime service registry 取 `IAudioService` 并转发 tick。

M2 FMOD 后端测试建议覆盖：

- FMOD package 缺失时不影响 `MxFramework.Audio` 编译。
- 已安装 FMOD 时可创建 `MxFramework.Audio.FMOD` asmdef 并编译。
- one-shot、loop start / stop、parameter、bus volume 可通过 fake 或最小 bank 验证。
- `FmodAudioDemoRunner` 已可通过 Inspector 填 event path/guid、bus path/VCA path、parameter name；真实播放仍要求 FMOD bank。
- listener / emitter binding 不把 Unity 类型泄漏到 noEngine contract。
- FMOD event / bus / parameter 缺失返回结构化错误并进入 snapshot。

## 索引接入状态

当前状态：已接入 `Docs/INTERFACES.md`。
