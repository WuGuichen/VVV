# Audio System FMOD Design

> Status: Design
> Implementation state: M1 Implemented / M2 Runtime Scaffold Verified
> Date: 2026-05-10
> Goal: 为 MxFramework 设计一套可测试、可替换、可诊断的音频系统，并以 FMOD 作为 Unity 运行时首选后端。

## 1. 结论先行

音频系统采用“三层拆分”：

```text
MxFramework.Audio          noEngine contract, command, config key, bus state, diagnostics
MxFramework.Audio.FMOD     Unity + FMOD adapter, bank/event/bus/VCA bridge, listener/emitter binding
MxFramework.Audio.Editor   FMOD event validation, bank manifest import, config authoring checks
```

核心原则：

- 框架核心不直接依赖 `FMODUnity`、`FMOD.Studio`、`UnityEngine` 或 `UnityEditor`。
- Gameplay / Combat / Ability 只发音频意图，不持有 FMOD `EventInstance`。
- 音频配置只保存稳定 audio event id、FMOD event path / guid、bus path、参数名和必要的 Unity 资源 key，不保存场景对象引用。
- FMOD 相关生命周期集中在 `MxFramework.Audio.FMOD`，由 Unity composition root 注入到 `RuntimeHost` 的 service registry。
- 高频播放路径以 struct command 和 handle id 为主，避免每帧分配。
- 音频不参与确定性战斗结果 hash；Replay 只记录可复现的 audio command 摘要或完全忽略音频输出。
- 所有 bank、event、bus、VCA、参数缺失都必须可诊断，不允许静默失败。

## 2. 要解决的问题

WGameFramework 已经有 Runtime Host、Events、Config、Resources、Diagnostics 和 Gameplay 垂直切片。音频若直接在业务层调用 FMOD，会很快出现这些问题：

- 技能、Buff、UI、战斗表现都散落 `RuntimeManager.PlayOneShot`。
- FMOD event path 改名后，运行时才发现声音丢失。
- 长循环音效、角色挂点音效和 UI 音效没有统一句柄和释放规则。
- Bank 加载、样本预载、场景切换卸载、Mod/DLC 覆盖缺少统一策略。
- 音量、静音、快照、低血量/暂停等混音状态没有中心化状态。
- Debug 面板看不到当前活跃事件、bus 音量、加载 bank、最近错误。

本设计把“音频意图”和“音频后端”分离：框架层只定义可测试契约，FMOD 层负责把契约映射到 FMOD Unity Integration。

## 3. 非目标

第一阶段不做：

- 不实现自研 DSP、音频导入管线或 FMOD Studio 工程管理。
- 不让 noEngine 模块依赖 FMOD。
- 不把 Unity `AudioSource` 作为并列后端；如果需要，可后续做 `Audio.Unity` 适配。
- 不做运行时在线下载 bank。
- 不做完整 Wwise / CriWare 抽象层；接口保留可替换空间即可。
- 不让 Mod 包直接执行任意 FMOD 命令；Mod 只能通过 catalog / config 声明资源和事件引用。

## 4. 模块边界

| 模块 | 类型 | 依赖 | 职责 |
| --- | --- | --- | --- |
| `MxFramework.Audio` | Runtime, noEngine | Core, Events, Config 可选, Diagnostics | 音频 ID、请求、句柄、通道、bus 状态、后端接口、诊断快照 |
| `MxFramework.Audio.FMOD` | Runtime | Audio, Resources 可选, Runtime 可选, UnityEngine, FMODUnity | FMOD bank/event/bus/VCA 后端、listener、emitter 绑定、RuntimeModule |
| `MxFramework.Audio.Editor` | Editor only | Audio, Audio.FMOD, Config, UnityEditor | FMOD event 引用校验、bank manifest 导入、配置检查、调试面板 |
| `MxFramework.Demo` | Runtime | Audio.FMOD 可选 | 展示最小播放、3D 跟随、bus 音量和 bank warmup |

依赖方向：

```text
Core
  <- Audio
      <- Audio.FMOD
          <- Demo / Preview.Runtime / project composition root

Resources
  <- Audio.FMOD  (可选，用于 bank manifest / catalog 接入)

Runtime
  <- project composition root
      -> Audio.FMOD RuntimeModule
```

`MxFramework.Audio` 不引用 `Runtime`，避免 Runtime 反向依赖外层模块。需要 Tick 的 FMOD 后端用 `AudioRuntimeModule : RuntimeModule` 放在 `Audio.FMOD` 中。

## 5. 核心概念

### 5.1 AudioEventKey

`AudioEventKey` 是业务配置和运行时命令使用的稳定 ID，不等同于 FMOD path。

建议字段：

```text
AudioEventKey
  int Id
  string Name
```

约定：

- `0` 保留为 invalid。
- ID 空间建议 `AudioEvent: 500000-599999`。
- `Name` 只用于调试，例如 `combat.hit.blade_light`。
- 游戏层配置可以用 int ID；Editor 展示时映射到名称和 FMOD event。

### 5.2 AudioEventDefinition

音频事件定义由 Config 或项目层 Provider 提供。

```text
AudioEventDefinition
  int Id
  string Name
  string FmodEventPath
  string FmodEventGuid
  AudioEventKind Kind
  AudioBusId BusId
  bool Is3D
  bool IsLoop
  float MaxDistance
  AudioParameterDefinition[] Parameters
  string[] Labels
```

规则：

- `FmodEventGuid` 优先用于运行时解析，`FmodEventPath` 用于 Editor 可读性和 fallback。
- `IsLoop=true` 的事件必须通过 handle 停止，不能用 fire-and-forget。
- 3D 事件必须提供位置或绑定对象；缺失时按配置决定失败或降级为 listener 位置。
- 参数名由配置声明，运行时设置未知参数应返回失败并记录诊断。

### 5.3 AudioCommand

播放请求统一走 command，避免业务层直接调用后端。

```text
AudioCommand
  long Frame
  int EventId
  AudioCommandKind Kind
  AudioEmitterId EmitterId
  AudioTransform Transform
  AudioParameterValue[] Parameters
  int Priority
  string TraceId
```

`AudioCommandKind` 首版：

- `PlayOneShot`
- `StartLoop`
- `Stop`
- `SetParameter`
- `SetBusVolume`
- `SetBusMuted`
- `SetSnapshot`

`AudioTransform` 在 noEngine 层只保存数值：

```text
AudioTransform
  float X
  float Y
  float Z
  float ForwardX
  float ForwardY
  float ForwardZ
```

Unity 侧再把它映射到 `Vector3` 和 FMOD 3D attributes。

### 5.4 AudioHandle

循环、可控一次性事件和需要后续设置参数的事件返回 `AudioHandle`。

```text
AudioHandle
  int Id
  int EventId
  AudioHandleState State
  AudioEmitterId EmitterId
```

规则：

- `PlayOneShot` 默认不返回可持有 handle；需要控制时使用 `StartEvent`。
- `Stop(handle, allowFadeout)` 幂等。
- 后端释放 FMOD `EventInstance` 后，handle 状态变为 `Released`。
- handle id 由 `IAudioBackend` 分配，不暴露 FMOD 原生对象。

### 5.5 Bus / VCA / Snapshot

框架层使用稳定 ID，FMOD 层映射到 path：

```text
AudioBusDefinition
  int Id
  string Name
  string FmodBusPath   // bus:/SFX, bus:/Music
  string FmodVcaPath   // vca:/Master, 可选
  float DefaultVolume
  bool DefaultMuted
```

推荐 bus 层级：

```text
Master
  Music
  SFX
    Combat
    Ability
    UI
    Ambience
  Voice
```

Snapshot 用 event 形式管理：`snapshot:/combat/low_hp`、`snapshot:/ui/pause`。框架只提供 start/stop 和权重参数，不解释具体混音曲线。

## 6. 公开接口草案

`MxFramework.Audio` 建议首版公共 API：

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

```csharp
public interface IAudioDefinitionProvider
{
    bool TryGetEvent(int eventId, out AudioEventDefinition definition);
    bool TryGetBus(int busId, out AudioBusDefinition definition);
    bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition);
}
```

实现建议：

- `AudioService` 放在 `MxFramework.Audio`，只做校验、优先级、限流和转发。
- `NullAudioBackend` 放在 `MxFramework.Audio`，用于服务器、EditMode 测试和未接入 FMOD 的 Demo。
- `FmodAudioBackend` 放在 `MxFramework.Audio.FMOD`，封装 FMOD Unity API。
- `AudioRuntimeModule` 放在 `MxFramework.Audio.FMOD`，在 `RuntimeTickStage.PostSimulation` 处理队列和后端 Tick。

## 7. FMOD 适配策略

FMOD Unity Integration 提供 `FMODUnity.EventReference`、`RuntimeManager.CreateInstance`、`RuntimeManager.PlayOneShot`、`RuntimeManager.AttachInstanceToGameObject`、`RuntimeManager.LoadBank`、`RuntimeManager.StudioSystem.getBus/getVCA` 等入口。适配层只在一个地方直接使用这些 API。

### 7.1 EventReference

Editor 配置期保留 path，运行时优先使用 guid：

```text
AudioEventDefinition.FmodEventGuid -> FMOD.GUID -> RuntimeManager.CreateInstance(guid)
AudioEventDefinition.FmodEventPath -> RuntimeManager.CreateInstance(path) 仅作 fallback / debug
```

理由：

- path 可读，但重命名敏感。
- guid 更稳定，适合构建后的 runtime。
- Editor validator 必须检查 path 和 guid 是否仍对应同一个 FMOD event。

### 7.2 一次性音效

短 SFX：

```text
AudioService.PlayOneShot
  -> FmodAudioBackend
  -> RuntimeManager.PlayOneShot(eventReference, position)
```

但为了统一限流、优先级和诊断，首版不建议业务直接调用 `RuntimeManager.PlayOneShot`。后端内部可以用它，或者统一走 `CreateInstance -> set3DAttributes -> start -> release`。

### 7.3 循环和可控事件

循环、蓄力、引导技能、角色 aura、环境声：

```text
CreateInstance
  -> set parameter / 3D attributes
  -> start
  -> keep EventInstance in handle table
  -> Stop(handle)
  -> release
```

规则：

- `IsLoop=true` 必须进入 handle table。
- `Stop` 使用 `AudioStopMode.AllowFadeout` / `Immediate` 映射到 FMOD stop mode。
- 后端维护 `handleId -> EventInstance`，业务层永远不持有 `EventInstance`。

### 7.4 3D 跟随

适配层提供 Unity-only 绑定：

```text
AudioEmitterBinding
  AudioEmitterId -> Transform / GameObject / Rigidbody / Rigidbody2D
```

播放时：

- 有绑定对象：使用 `RuntimeManager.AttachInstanceToGameObject`。
- 无绑定对象但有位置：设置一次 3D attributes。
- 对象销毁：`AudioEmitterBinding` 自动停止或解绑相关 handle。

`AudioEmitterId` 由项目层实体/表现层分配，不能把 `GameObject.GetInstanceID()` 写入 noEngine 配置。

### 7.5 Bank 加载

首版支持两种策略：

1. FMOD Settings 自动加载基础 bank，框架只校验事件可用。
2. `AudioBankManifest` 显式声明场景/模块需要的 bank，由 `FmodBankService` 调用 `RuntimeManager.LoadBank(bankName, loadSamples)`。

建议第一阶段使用自动加载 + 显式 warmup：

```text
SceneAudioWarmup
  labels: ["combat", "ui"]
  banks: ["Master", "Master.strings", "Combat", "UI"]
  loadSamples: true for combat critical SFX
```

后续再把 bank manifest 接入 `MxFramework.Resources`：

```text
ResourceTypeIds.FmodBank = "FmodBank"
provider = "fmodBank"
address = "Combat"
providerData.loadSamples = true
```

Issue #68 当前策略：

- `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.bank` 和 `Master.strings.bank` 是 FMOD Studio demo project 的 desktop bank build output。它们在框架 demo 中作为可审计的 authoring build output / release input 保留版本化，方便没有 FMOD Studio 的检出环境仍能检查现有 demo bank。
- `Assets/StreamingAssets/Master.bank` 和 `Master.strings.bank` 是 Unity runtime bank mirror，由 FMOD Unity Integration 和 `MxFramework.Audio.FMOD` 在运行时使用。它们也保留版本化，作为当前 player/runtime 输入，而不是由普通 Resource Catalog 复制或加载。
- Build/Desktop 与 StreamingAssets mirror 必须表达同一组 demo bank 内容。重新构建或刷新 bank 时，提交者应一起检查 FMOD build output、StreamingAssets mirror、FMOD settings/cache 和 validator 结果；不要只更新其中一侧。
- `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` 当前按 versioned generated metadata 处理：它可由 FMOD/Refresh Banks 刷新，但被提交用于记录 Unity Editor 可见的 bank、event、guid 和路径元数据。它不是普通本地机器 cache，不加入 `.gitignore`；也不是权威音频配置，不能手写修复 event 语义。
- 当前 `.gitignore` 策略保持不变：允许 `FMOD/.../Build/**` 进入版本控制，忽略 FMOD Studio project 的 `.cache/buildrecords/` 和 `.cache/fsbcache/` 机器缓存，不忽略 `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`。
- `FmodAudioSetupValidator` 的职责是校验 runtime bank mirror 和 FMOD Settings 的基础一致性，包括 bank root、`.bank` 文件、`Master.bank`、`Master.strings.bank` 和 `BankLoadType`。Resource Catalog validator 不校验 FMOD bank 内容。

### 7.6 Bus / VCA

`FmodAudioBackend` 初始化时根据 `AudioBusDefinition` 缓存 bus / VCA：

```text
FmodBusPath -> RuntimeManager.StudioSystem.getBus(path)
FmodVcaPath -> RuntimeManager.StudioSystem.getVCA(path)
```

规则：

- 音量范围统一为 `0..1`。
- Mute 与 Volume 分开记录，避免恢复音量时丢失用户设置。
- 设置失败返回 `AudioResult`，并进入 `AudioDebugSnapshot.RecentErrors`。

## 8. 与 Runtime Host 集成

Unity composition root 示例：

```csharp
var audioDefinitions = new ConfigAudioDefinitionProvider(configRegistry);
var audioBackend = new FmodAudioBackend(fmodOptions);
var audioService = new AudioService(audioBackend, audioDefinitions);

services.Register<IAudioService>(audioService);
host.RegisterModule(new AudioRuntimeModule(audioService));
```

模块调度：

| 阶段 | 说明 |
| --- | --- |
| `Initialize` | 加载 definition、校验 bus/VCA、准备 bank manifest |
| `Start` | 执行场景 warmup、设置默认 bus 音量 |
| `Tick(PostSimulation)` | drain audio command queue、更新 3D listener/emitter、清理完成事件 |
| `Stop` | 停止非持久事件、释放 transient handles |
| `Dispose` | 释放所有 FMOD EventInstance、清空缓存 |

音频命令推荐在 Gameplay / Combat 执行结束后播放，因此 `AudioRuntimeModule` 使用 `RuntimeTickStage.PostSimulation`。这样同一帧中，Ability 先产生事件，再统一转成音频输出。

## 9. 与 Gameplay / Combat 集成

Gameplay 不应该直接播放声音，而是发布音频意图：

```text
AbilityExecutedEvent
  -> project layer AudioCueMapper
  -> IAudioService.PlayOneShot(AudioEventId.AbilityCast)
```

或在 Ability 配置中声明表现 cue：

```text
AbilityPresentationConfig
  int AbilityId
  int CastAudioEventId
  int HitAudioEventId
  int LoopAudioEventId
```

推荐职责划分：

- `MxFramework.Gameplay` 只发布 gameplay event。
- 项目层或 Demo 层做 `AudioCueMapper`，把 gameplay event 映射到 audio event。
- `MxFramework.Audio` 不知道 Ability、Buff、Hitbox 等业务概念。

## 10. 配置和校验

最小配置表：

```text
AudioEventConfig
  Id
  NameText
  FmodEventPath
  FmodEventGuid
  Kind
  BusId
  Is3D
  IsLoop
  MaxDistance
  Labels

AudioParameterConfig
  Id
  EventId
  Name
  DefaultValue
  MinValue
  MaxValue

AudioBusConfig
  Id
  Name
  FmodBusPath
  FmodVcaPath
  DefaultVolume
  DefaultMuted

AudioSceneWarmupConfig
  Id
  SceneKey
  Labels
  BankNames
  LoadSamples
```

Editor 校验：

- FMOD event path 存在。
- event guid 与 path 匹配。
- event 2D/3D 与 `Is3D` 一致。
- `IsLoop=true` 的 event 不允许只配置成 one-shot cue。
- 参数名存在，类型和值域合理。
- Bus / VCA path 存在。
- warmup 声明的 bank 存在。
- 配置引用的 `AudioEventId` / `AudioBusId` 不缺失。

## 11. 资源和 Mod Package

FMOD bank 本质上更接近平台构建产物，不应由普通 `AudioClip` 路径处理。

建议分两期：

### Phase A：项目内 FMOD

- FMOD Unity Integration 管理 bank 路径和自动加载。
- MxFramework 只保存 event/bus/parameter 配置。
- Demo 使用少量本地 bank 验证。
- FMOD event 通过 `AudioEventDefinition.Id`、`FmodEventGuid` 和 `FmodEventPath` 暴露给配置和运行时，不暴露成 generic `ResourceKey`。
- `Master*.bank` 不写成普通 `ResourceCatalogEntry`，也不使用 `ResourceTypeIds.AudioClip`。
- `Assets/Audio/MxFramework/Samples/MagicEffects/**` 是 Unity `AudioClip` 样例，可由普通 Resource Catalog 覆盖；若未来把这些源音频导入 FMOD Studio，它们应转入 FMOD authoring pipeline 并停止作为普通 `AudioClip` Catalog 样例。

### Phase B：Catalog 化

- 扩展 `ResourceTypeIds.FmodBank`、`ResourceTypeIds.FmodEventManifest`。
- Mod/DLC 包可声明额外 bank catalog。
- `FmodBankProvider` 负责加载 bank 文件和 strings bank。
- Editor 构建报告输出 bank hash、size、platform、event 列表。
- 只有后续单独 S2 设计批准后，才新增 `FmodBank`、`AudioEvent` 或 `FmodEventManifest` 这类 ResourceTypeId。未批准前，普通 `ResourceCatalogEntry` 只覆盖 Unity `AudioClip`、`Texture2D`、`GameObject` 等常规资源，不代表 FMOD event 或 bank。

Mod 约束：

- Mod 可新增 event config，但必须绑定自己的 package id。
- 覆盖内置 event 必须显式 `allowOverride`，且事件 kind / 2D3D / loop 语义兼容。
- 禁止 Mod 覆盖全局 Master bus 默认音量。

## 12. 诊断

`AudioDebugSnapshot`：

```text
AudioDebugSnapshot
  BackendName
  Initialized
  LoadedBankCount
  ActiveEventCount
  VirtualEventCount
  ListenerCount
  BusStates[]
  ActiveEvents[]
  RecentCommands[]
  RecentErrors[]
```

`ActiveEventDebugInfo`：

```text
HandleId
EventId
EventName
EmitterId
State
AgeSeconds
Volume
IsVirtual
TraceId
```

接入 Diagnostics：

```text
AudioDebugSource : IFrameworkDebugSource
  -> FrameworkDebugSnapshot("Audio", mode, sections)
```

Debug 面板只读取 snapshot，不读取 `FmodAudioBackend` 私有表。

## 13. 性能预算

| 场景 | 目标 |
| --- | --- |
| 每帧 Tick 无命令 | 0 GC alloc |
| 播放 one-shot | 允许后端少量分配，但业务层请求使用 struct |
| 循环音效参数更新 | NoAlloc after initialization |
| 3D emitter 更新 | 批量遍历已绑定 emitter，不按事件查找 Transform |
| Debug snapshot | LowFreqAlloc，只在面板/导出时调用 |

运行时策略：

- 同一 event 每帧限流，例如 `maxInstancesPerFrame`。
- 同一 emitter 的同类 one-shot 可合并或丢弃低优先级请求。
- 超出全局 active event 上限时按 `Priority` 和距离裁剪。
- 预热阶段加载关键 bank samples，避免首击卡顿。

## 14. 错误处理

| 场景 | 行为 |
| --- | --- |
| 未注册 EventId | 返回 `AudioErrorCode.EventNotFound` |
| FMOD event path/guid 不存在 | 返回 `AudioErrorCode.BackendEventMissing` |
| 缺少 bus/VCA | 初始化失败或降级，取决于 `AudioBackendOptions.FailOnMissingBus` |
| 3D event 缺少位置 | 返回失败，除非 definition 允许 fallback |
| 参数不存在 | 返回 `AudioErrorCode.ParameterNotFound` |
| 重复释放 handle | 不抛异常，记录诊断 |
| 后端未初始化 | 返回 `AudioErrorCode.BackendNotReady` |

音频失败默认不打断 gameplay 帧；但在 Editor/CI 校验中应当失败。

## 15. 落地里程碑

### M1：noEngine Contract + Null Backend

- 新增 `MxFramework.Audio` asmdef，保持 `noEngineReferences=true`。
- 实现 `AudioEventKey`、`AudioPlayRequest`、`AudioHandle`、`AudioResult`。
- 实现 `IAudioService`、`IAudioBackend`、`IAudioDefinitionProvider`。
- 实现 `NullAudioBackend` 和 `AudioDebugSnapshot`。
- EditMode 测试覆盖播放、停止、参数、错误码、重复释放。

### M2：FMOD Backend MVP

- 接入 FMOD Unity package。
- 新增 `MxFramework.Audio.FMOD` asmdef，引用 FMOD runtime asmdef / dll。
- 实现 `FmodAudioBackend`：one-shot、loop handle、stop、parameter、bus volume。
- 实现 listener 和 emitter binding。
- Demo 场景播放 UI click、技能 cast、命中 3D SFX、循环 aura。

### M3：Config + Editor Validation

- 新增 audio config schema。
- 实现 `ConfigAudioDefinitionProvider`。
- Editor 校验 FMOD event/bus/parameter/bank。
- `Docs/Interfaces/Audio.md` 在接口真正落地后再加入 `Docs/INTERFACES.md`。

### M4：Bank Warmup + Diagnostics

- 实现 scene warmup。
- 接入 `ResourcePreloadService` 或独立 `AudioWarmupService`。
- `AudioDebugSource` 接入 Diagnostics。
- Debug 面板展示活跃事件、bus 音量、最近错误。

### M5：Mod / DLC Bank Catalog

- `ResourceTypeIds.FmodBank`。
- `FmodBankProvider`。
- Mod package audio catalog 合并、覆盖和校验。

## 16. 关键取舍

| 选择 | 结论 | 原因 |
| --- | --- | --- |
| 框架直接依赖 FMOD | 不采用 | 会破坏 noEngine 模块和测试能力 |
| 业务层直接调用 `RuntimeManager` | 不采用 | 无法统一限流、诊断、配置和替换后端 |
| 所有声音都返回 handle | 不采用 | one-shot 高频路径会增加管理成本 |
| FMOD path 作为唯一运行时引用 | 不采用 | 重命名脆弱，guid 更适合构建后 runtime |
| Audio 参与 Runtime hash | 不采用 | 音频是表现层，不应影响确定性战斗结果 |
| Bank 立即接入 ResourceManager | 暂缓 | FMOD 自带 bank 管理，先跑通 event/bus/handle，再做 catalog 化 |

## 17. 验收标准

首个可用闭环必须满足：

- 不安装 FMOD 时，`MxFramework.Audio` 可独立编译和测试。
- 安装 FMOD 后，Demo 能播放 2D UI 音效、3D 命中音效、循环音效，并能停止循环。
- Bus 音量和 mute 可运行时调整。
- 配置缺失 event / parameter / bus 能在 Editor 校验中报错。
- Runtime Debug Snapshot 能看到活跃事件和最近错误。
- Gameplay / Combat 源码中没有直接引用 `FMODUnity` 或 `FMOD.Studio`。

## 18. 参考资料

- FMOD Unity Integration 文档：`https://www.fmod.com/docs/2.03/unity/welcome.html`
- FMOD for Unity 2.03 `RuntimeManager`：`https://github.com/fmod/fmod-for-unity/blob/2.03/Assets/Plugins/FMOD/src/RuntimeManager.cs`
- FMOD for Unity 2.03 `StudioEventEmitter`：`https://github.com/fmod/fmod-for-unity/blob/2.03/Assets/Plugins/FMOD/src/StudioEventEmitter.cs`
- FMOD for Unity 2.03 `EventReference`：`https://github.com/fmod/fmod-for-unity/blob/2.03/Assets/Plugins/FMOD/src/EventReference.cs`
