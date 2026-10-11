# Voice Input 接口

> Status: Current

> 依赖：`MxFramework.Runtime` | Unity 适配：`MxFramework.VoiceInput.Unity` | 代码：`Packages/com.vincent.wgameframework/VoiceInput*/`

Voice Input 提供运行时语音输入事实采集契约。首版只负责麦克风/识别 provider 的生命周期、状态、音频活动和转写事件承载，不解析语义，不映射 `InputIntent`，不直接生成 gameplay `RuntimeCommand`。

## 1. 模块边界

- `MxFramework.VoiceInput` 是 noEngine 程序集，只依赖 `MxFramework.Runtime`，可在测试、服务器或非 Unity provider 中使用。
- `MxFramework.VoiceInput.Unity` 是 Unity-facing adapter，使用 Unity `Microphone` / authorization API 做本地麦克风采集；Unity 类型不进入 noEngine 公共契约。
- `MxFramework.Audio` 仍只负责播放请求、后端和音频诊断，不承担麦克风输入。
- `MxFramework.Input` 的 `InputIntent` 不因语音输入扩展；后续若要把转写结果映射到输入意图，应在项目组合根或可选 bridge 中完成。
- Voice Input 不进入 Replay、SaveState、Network authority 或 Runtime hash。若后续语音解析成 command，应记录解析后的 command，而不是麦克风音频事实。

## 2. 公开契约

| 类型 | 用途 |
|------|------|
| `IVoiceInputProvider` | provider 采集入口，负责 start / stop / tick 并向 sink 发送语音输入事实 |
| `IVoiceInputService` | runtime-facing 服务，持有 provider、事件队列和最新 snapshot |
| `IVoiceInputEventSink` | provider 向 service 写入事件的低层接口 |
| `VoiceInputOptions` | noEngine 服务选项，包含默认 source id 和事件队列容量 |
| `VoiceInputSnapshot` | 最新状态、音频活动、音量、最近错误和最近转写摘要 |
| `VoiceInputEvent` | 按 `RuntimeFrame` 标记的语音输入事实 |
| `VoiceInputEventKind` | session、audio activity、audio level、partial/final transcript、error |
| `VoiceInputStatus` | unavailable、idle、permission required/denied、starting、listening、error |
| `VoiceInputErrorCode` | provider unavailable、permission denied、no device、start/capture failed 等 |
| `VoiceInputRuntimeModule` | `RuntimeHost` 模块，默认 `PreSimulation` tick `IVoiceInputService` |
| `FakeVoiceInputProvider` | 测试和手动注入用 provider |
| `ScriptedVoiceInputProvider` | 按 frame 播放预设语音输入事件的测试/回放 provider |
| `UnityMicrophoneVoiceInputProvider` | Unity 麦克风 provider，只输出状态和 activity，不声称完成语音识别 |

## 3. 事件语义

`VoiceInputEvent` 是低层事实，不是 gameplay 意图：

```csharp
public readonly struct VoiceInputEvent
{
    public RuntimeFrame Frame { get; }
    public int SourceId { get; }
    public VoiceInputEventKind Kind { get; }
    public VoiceInputStatus Status { get; }
    public VoiceInputErrorCode ErrorCode { get; }
    public float Level { get; }
    public string Text { get; }
    public float Confidence { get; }
    public string TraceId { get; }
}
```

首版 Unity 麦克风 provider 只生成：

- `SessionStarted`
- `SessionStopped`
- `AudioActivityStarted`
- `AudioActivityStopped`
- `Error`

`PartialTranscript` / `FinalTranscript` 为后续 ASR provider 预留。框架层不定义自然语言语义、命令词、热词或 NLU 规则。

## 4. Runtime 接入

组合根注册服务和模块：

```csharp
var provider = new UnityMicrophoneVoiceInputProvider();
var service = new VoiceInputService(provider, disposeProvider: true);

var services = new RuntimeServiceRegistry();
services.Register<IVoiceInputService>(service);

var host = new RuntimeHost(new RuntimeHostOptions { Services = services });
host.RegisterModule(new VoiceInputRuntimeModule(startOnHostStart: true));
```

读取语音输入事实：

```csharp
var events = new List<VoiceInputEvent>(8);
voiceInput.DrainEvents(frame, events);
for (int i = 0; i < events.Count; i++)
{
    VoiceInputEvent evt = events[i];
    // 项目层可在这里把 FinalTranscript 映射成 InputCommand / RuntimeCommand。
}
```

`VoiceInputRuntimeModule` 默认在 `RuntimeTickStage.PreSimulation` 运行，使语音输入事实先于模拟阶段进入服务队列。模块只 tick service，不拥有语义解析权威。

## 5. 分配约定

- `VoiceInputService.Tick(...)` 在 provider 不产生文本/错误事件且事件队列容量足够时是 `NoAlloc after initialization`。
- `VoiceInputService.DrainEvents(frame, destination)` 使用 caller-owned `List<VoiceInputEvent>`；目标列表容量足够时不分配。
- `FakeVoiceInputProvider` / `ScriptedVoiceInputProvider` 的构造和事件脚本准备是低频分配；tick 稳态不分配。
- `UnityMicrophoneVoiceInputProvider` 在 start 时分配采样 buffer；录音中只复用该 buffer。权限、错误文本、Unity `AudioClip` 和后端 SDK 边界属于低频/平台边界分配。
- `VoiceInputSnapshot` 和 `VoiceInputEvent` 可包含 string，用于错误和 ASR 转写事实；不应在无新文本/错误的高频 tick 中创建新字符串。

## 6. Unity 麦克风适配

`UnityMicrophoneVoiceInputProvider` 支持：

- 默认设备或显式 `DeviceName`。
- 无麦克风设备时返回 `VoiceInputErrorCode.NoDevice`。
- 麦克风权限等待和拒绝状态。
- `Microphone.Start` 失败和录音中断错误。
- 基于采样 RMS level 的 activity start/stop 事件。

它不输出 `PartialTranscript` / `FinalTranscript`；真实语音识别应由独立 ASR provider 实现同一 `IVoiceInputProvider` 契约。

## 7. 测试入口

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/VoiceInput/VoiceInputServiceTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/VoiceInput/VoiceInputRuntimeModuleTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/VoiceInput/UnityMicrophoneVoiceInputProviderTests.cs`
