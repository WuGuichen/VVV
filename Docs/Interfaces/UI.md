# UI Core 接口

> Status: Current
>
> Version: v0.2 | 2026-08-02
>
> Assembly: `MxFramework.UI` | `noEngineReferences=true`

## 职责与边界

`MxFramework.UI` 提供不依赖 Unity、FairyGUI 或 Audio 的视图、命令和反馈语义。
本页描述已经落地的 noEngine Core、generated manifest policy、UI Composer
binding contract v2 和可选的 `MxFramework.UI.Audio` bridge；四个 Audio Cue、
Error Event、人工审听/发布仍以
`Docs/UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md` 为 Planned。CharacterTest
Settings 已作为首个 player-facing runtime composition 样板接入。

因此，当前可以在生成规格中声明反馈策略，并从 generated manifest 构建 registry；
Settings runtime composition 已注入 feedback decorator 并上报真实结果，但
四个 Cue 尚未发布，所以当前按缺 Cue 静音降级。其他 FairyGUI 界面仍需逐个在
项目组合根接入，不能仅因 contract 有 feedback 字段就宣称可听。UI Core 不引用
FMOD path、GUID、Event ID、Cue key 或 `IAudioService`。

## Player Guidance contract

`MxFramework.UI` 当前提供正式 player-facing Guidance 的 surface-neutral 合同：

- `MxUiGuidanceScopeId` 与 `MxUiGuidanceMessageId` 是 ordinal stable value id；
- `MxUiGuidanceTone` 固定为 `Neutral / Info / Success / Warning / Error`；
- `MxUiGuidanceGlyph` 是封闭语义集合，调用方不能传 FairyGUI URL 或文件路径；
- `MxUiGuidanceCardSnapshot` 固定为 title + Action / Focus / Status 三行；
- `MxUiGuidanceNotice` 表示一条有界短时消息；`Neutral` 不能用于 Notice；
- `IMxUiGuidanceSink` 只暴露 `PresentCard`、`TryPushNotice` 和 `ClearScope`；
- `MxUiGuidanceState` 实现一个 Card、两个 Notice slot、同 scope/message 合并、
  严重度替换、duration clamp、scope 清理和单调 Card revision。

Notice 默认时长为 Info / Success 2.8 秒、Warning / Error 4 秒；显式时长限制在
1 至 8 秒。两槽已满时，只有严格更高严重度的消息能替换较低严重度项；否则返回
`false` 并累计 `NoticeOverflowCount`。状态层不会从文字推断冲突或完成。

所有 DTO 与 `MxUiGuidanceState.Advance(...)` 为 NoAlloc；通过正式 Presenter 提交 Card
或 Notice 属于 LowFreqAlloc，`ClearScope` 和稳态 presentation Tick 在初始化后为
NoAlloc。调用方的 Tutorial、Objective、Interaction、Story、Trigger、Combat 或
Validation 仍拥有事实与流程；Guidance 只显示项目 Mapper 已确认的结果，不拥有
SaveState、Replay、network 或 deterministic hash。

## 命令与调用身份

| 类型 | 当前契约 | 分配 |
| --- | --- | --- |
| `MxUiCommand` | 保留三参数构造；`InvocationId=0` 表示未经过反馈路由；`WithInvocationId(...)` 返回带正数调用身份的副本 | 值类型；`WithInvocationId` 为 NoAlloc |
| `IMxUiCommandSink` | `Enqueue(MxUiCommand)` 签名保持不变 | 由实现决定 |
| `MxUiCommandOutcome` | 用 invocation 关联 `Success`、`Failure` 或 `Cancelled`；`ErrorCode` 只用于低频诊断 | 值类型；NoAlloc |
| `IMxUiCommandOutcomeSink` | 在 owner thread 上接收真实业务结果 | NoAlloc |
| `IMxUiCommandOutcomeSource` | 只允许一个 active outcome owner | `BindOutcomeSink` 为 LowFreqAlloc |
| `MxUiCommandOutcomeChannel` | 可选的单槽 source 实现；重复绑定和错线程调用 fail-fast；binding Dispose 幂等且使用 generation lease | 构造/binding 为 LowFreqAlloc；`TryComplete` 为 NoAlloc |

invocation 只服务 UI 表现关联，不进入 Gameplay authority、SaveState、Replay、
网络协议或确定性 hash。

## 反馈语义与策略

`MxUiFeedbackKind` 当前包含 `None`、`Click`、`Confirm`、`Back` 和 `Error`。
`MxUiFeedbackPolicy` 分别保存 Activation、Success、Failure 三个 phase 的反馈。
`MxUiFeedbackContext` 只携带 view、command、invocation 和 phase 诊断上下文。

`IMxUiFeedbackPlayer.TryPlay(...)` 是后端适配点。返回 `false` 或播放器异常都不能
改变业务命令是否执行。Current `MxFramework.UI.Audio` 提供
`MxAudioUiFeedbackPlayer`：构造时解析并缓存 `ui.click`、`ui.confirm`、
`ui.back`、`ui.error` 的完整 `AudioCueDefinition`，播放时保留默认参数并发出
priority 80 的 2D one-shot。Cue、服务或后端缺失时只记录值类型诊断并静音。

该 bridge 的构造为 LowFreqAlloc；`TryPlay` 与 `CaptureDebugSnapshot` 在初始化后
为 NoAlloc。当前 Audio Authoring Manifest 尚未发布四个 Cue，因此仅存在 bridge
不能让界面产生可听反馈。

`MxUiFeedbackPolicyRegistry` 在组合阶段接收
`MxUiFeedbackPolicyBinding` 列表，拒绝无效或重复的 view/command key，并按
`SourceViewId + CommandId` 做 ordinal 精确查找：

- registry 构造为 LowFreqAlloc；
- `TryGetPolicy(...)` 为 NoAlloc；
- 不根据命令后缀、控件名或界面 XML 推断策略；
- 缺策略时反馈路由静音、记录 `PolicyMissing`，业务命令仍转发。

## Generated manifest 与 binding contract

- `MxFairyGuiCommandBinding` 和 `MxUiCommandDescriptor` 当前都携带
  `MxUiFeedbackPolicy`。
- generation spec 中省略策略时默认 `Click / None / None`，generated C# 始终
  显式写出三个 phase。
- `MxFairyGuiManifestProjection.CreateFeedbackPolicyRegistry(...)` 在 UI 组合阶段
  为单个 generated manifest 建立 registry；该方法为 LowFreqAlloc。
- `mx.uiComposerBindingContract.v2` 要求每个 command slot 显式包含 feedback；
  缺字段或未知 kind 会由 Composer preflight 以 `UICOMP015` 阻断。
- v1 contract 只在 Authoring Core 内存中投影为 `None / None / None`，文件本身
  不会被工具自动升级。CharacterTest Settings 是当前首个 v2 样板。
- Composer Web 只读展示即时/成功/失败策略；Screen Blueprint、FairyGUI XML、
  helper Apply/Publish 和 `ui-composer-state.json` 不拥有也不写入这些语义。
- runtime composition 仍不得给 v1 面板注入 feedback registry；CharacterTest
  Settings 是首个 v2 runtime consumer，其他面板属于后续阶段。

## `MxUiFeedbackCommandSink`

`MxUiFeedbackCommandSink` 是 additive `IMxUiCommandSink` decorator：

1. 在构造时捕获 owner managed thread，并一次分配 64 个 pending slot。
2. 每次 `Enqueue` 清理超过 30 秒的 pending，分配非零 invocation。
3. 精确查策略，登记需要结果反馈的 invocation，播放 Activation，再转发业务命令。
4. `Complete` 按 invocation 播放 Success 或 Failure；Cancelled 只移除 pending。
5. Dispose 先释放 outcome binding，再清空 pending 并断开 player。

`Enqueue`、`Complete`、`CaptureDebugSnapshot` 和 Dispose 都是 main-thread-only，
在初始化后为 NoAlloc。异步业务完成必须先由项目组合根回送 owner thread；
UI Core 不创建后台队列，也不调用 Unity/FMOD API。

失败策略：

- 64 槽满时记录 `PendingOverflow`，但继续转发业务命令；
- 重复、未知或 invocation 0 的结果记录 `OutcomeUnknown` 并静音；
- 超过 30 秒的结果记录 `OutcomeExpired` 并静音；
- inner sink 抛错时移除对应 pending、记录 `HandlerException` 并保留原异常；
- 非 owner thread 调用记录 `WrongThread` 后 fail-fast；
- Dispose 后直接送达 decorator 的迟到结果记录 `OutcomeAfterDispose` 并静音。

`MxUiFeedbackDebugSnapshot` 只暴露 pending 数、诊断累计数和最后诊断码，不拼接
动态诊断字符串。

## 当前验证

- Unity EditMode focused：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/UI/MxUiFeedbackTests.cs`
- 独立 noEngine：`Tools/MxFramework.NoEngineTests/UI.Feedback.Tests/`
- Audio bridge noEngine：`Tools/MxFramework.NoEngineTests/UI.Audio.Tests/`
- Audio Workbench 四 Cue 契约：
  `Tools/MxFramework.NoEngineTests/UI.Audio.Authoring.Tests/`
- manifest/codegen：`Tools/MxFramework.NoEngineTests/FairyGUI.Manifest.Tests/`
- Player Guidance contracts/state：`Tools/MxFramework.NoEngineTests/UI.Guidance.Tests/`
- Authoring v1/v2 preflight：`Tools/MxFramework.NoEngineTests/UI.Composer.Feedback.Tests/`
  （同一回归也保留在 `MxFramework.Authoring.Tests` 聚合套件）

这些测试覆盖旧构造兼容、精确策略、Activation 顺序、成功/失败/取消、快速重复与
乱序完成、未知/重复/过期 outcome、容量溢出、inner 异常、错线程、单 owner
binding 和 teardown 后迟到结果。
