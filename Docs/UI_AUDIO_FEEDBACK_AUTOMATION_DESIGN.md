# UI 音效自动反馈设计

> Status: Design
>
> Date: 2026-07-24
>
> Implementation Status: Phase 3 Runtime Slice In Progress
>
> 当前实现状态：Phase 1A noEngine command identity、outcome、单 owner binding、policy registry 和 feedback decorator，以及 Phase 1B binding contract v2、v1 静音兼容、生成投影与 Composer 只读展示已落地；Phase 2 的 `MxFramework.UI.Audio` bridge、四 Cue 约束校验器、Error 源音和定向 FMOD authoring 脚本已落地；Phase 3 已把 CharacterTest Settings 组合到 generated policy、modal gate、feedback decorator 和真实业务 outcome 链，并移除其硬编码提前 Event 播放。四个 Cue、Error Event、人工审听、发布和 Unity 可听验收仍为 Planned。Current API 见 `Docs/Interfaces/UI.md`；本文不得描述为自动可听反馈已经完成。

## 1. 目的

本文定义正式 player-facing FairyGUI 界面的自动音效反馈边界，使后续新建或迁移界面时：

- 普通命令默认获得 Click 反馈，不要求每个 Binder 或 Command Sink 手写播放代码。
- 返回、关闭和取消统一获得 Back 反馈。
- Apply、Save、购买、确认等结果型命令在真实成功后播放 Confirm，在真实失败后播放 Error。
- UI 只表达反馈语义，不引用 FMOD path、GUID、EventInstance 或产品层硬编码 event id。
- 音频不可用时 UI 命令仍正常执行，失败只进入表现诊断。

设计属于框架通用能力，CharacterTest Settings 是第一个运行时验证样板。正式玩家界面使用 FairyGUI；Debug、Showcase、Editor 和 Validation surface 继续默认使用 UI Toolkit，首版不自动接入玩家 UI 音效。

## 2. 状态口径

本文使用以下状态：

| 状态 | 含义 |
| --- | --- |
| Current | 当前源码、配置或发布产物中已经存在并可核验。 |
| Planned | 本文接受的目标设计，尚不能作为运行时能力使用。 |
| Out of Scope | 首版明确不实施，不得借本文描述为已支持。 |

类型、schema、运行时组合和内容发布必须分别按可核验事实标注 Current；
只有源码、生成物、测试、发布 Cue 和真实可听验收全部落地后，才可把整条能力
描述为“自动可听反馈已经完成”。

## 3. 当前基线

截至 2026-07-25，仓库存在以下 Current 事实：

- `MxFramework.Audio` 提供 noEngine `IAudioService`、`AudioPlayRequest.Create2D(...)`、Cue/Event Catalog 和结构化播放结果。
- `AudioMixBusIds.Ui` 已存在，玩家设置能够调整 UI Bus 音量与静音。
- Audio Authoring Manifest 已有 `UI Click`、`UI Confirm`、`UI Back` 三个 2D one-shot Event Definition。
- 上述三个 Event 目前没有对应的 `AudioCueDefinition`；`UI Error` 的 Event/Cue 也尚未建立。
- CharacterTest Settings 已使用 `mx.uiComposerBindingContract.v2`，generated manifest 显式携带 activation/success/failure 策略；其他 v1 contract 仍静音兼容。
- `MxUiCommand` 已 additive 增加 invocation identity，`MxUiFeedbackCommandSink` 可通过 outcome 契约关联同步或异步成功/失败。
- CharacterTest Settings Host 已在 modal gate 之后、业务 Sink 之前组合 feedback decorator；硬件 `ui.cancel` 也进入同一路由。
- Settings 业务 Sink 已移除 `AudioProductEventIds` 和提前播放逻辑，只为 Apply、Save、Reload、Start/Save/Reset Input 上报真实同步 outcome。
- generated Binder 只检查 Enabled、构造 payload 并向 `IMxUiCommandSink` 入队，不拥有通用音效逻辑。

因此，当前可以称为“Settings 自动反馈运行时切片”，但四个 Cue 尚未发布，
仍不能称为“新 UI 自动带可听音效”。

## 4. 目标与非目标

### 4.1 首版目标

- 正式 FairyGUI command binding 具有显式、可生成、可校验的反馈策略。
- 新命令在生成规格中默认采用 `Click / None / None`。
- 返回/取消命令采用 `Back / None / None`。
- 结果型命令采用 `None / Confirm / Error`，避免一次操作先 Click、后 Confirm 的重复反馈。
- 同步和异步结果都通过 invocation identity 关联。
- 旧 v1 contract 升级工具后保持静音，只有显式迁移到 v2 才改变声音行为。
- Disabled、被 modal gate 拒绝或没有真正派发的命令不播放声音。
- 缺少服务、Cue、Event、Bank 或后端时静音并记录诊断，不阻断 UI。

### 4.2 首版非目标

- Hover、焦点移动、选中变化、滚动、拖放、弹窗 Open/Close 音效。
- 依据控件名称、命令后缀或按钮颜色自动猜测 Confirm、Back 或 Error。
- 让 Button Recipe、Screen Blueprint 或 FairyGUI XML 成为音效策略权威。
- 把 UI 音效写入 SaveState、Replay、Runtime result hash 或网络同步。
- 让 UI 直接调用 FMOD Unity API。
- 为 UI Toolkit 的 Debug、Showcase、Editor 或 Validation surface 自动挂接玩家音效。

## 5. 权威与依赖

### 5.1 权威链

```text
reviewed C# generation spec
  -> generated FairyGUI manifest command binding
  -> read-only mx.uiComposerBindingContract.v2
  -> generated MxUiCommandDescriptor / feedback policy registry
  -> modal command gate
  -> MxUiFeedbackCommandSink
  -> project command sink
  -> optional command outcome
  -> IMxUiFeedbackPlayer
  -> IAudioService
  -> FMOD adapter
```

| 关注点 | 权威 |
| --- | --- |
| command id、control name、payload、反馈策略 | reviewed C# generation spec |
| Composer 可见的只读绑定投影 | `mx.uiComposerBindingContract.v2` |
| 资源、配方、布局 | 现有 UI Composer / FairyGUI 权威链 |
| 反馈语义到 Cue key 的映射 | `MxFramework.UI.Audio` bridge |
| Cue、Event、Bus、FMOD binding、素材与审听 | Audio Workbench / Audio Authoring Manifest |
| 业务成功、失败、取消 | 实际 command handler |
| 最终播放 | `IAudioService` 和 FMOD adapter |

约束：

- Screen Blueprint 仍只能把节点分配给生成的 binding slot，不增加 `feedback` 字段。
- Button Recipe 只拥有视觉状态。相同配方可以服务普通、确认、返回或静音命令，不能据此推断声音。
- Composer Inspector 只读展示 binding 的即时/成功/失败反馈，不允许覆盖 C# 生成规格。
- FairyGUI helper 不把 feedback 写入 package/component XML，Apply/Publish 也不产生第二份反馈权威。
- Runtime 不读取 Screen Blueprint 或 Composer catalog。

### 5.2 模块方向

目标模块保持以下依赖；`MxFramework.UI`、manifest 和
`MxFramework.UI.Audio` bridge 方向均为 Current；CharacterTest Settings 项目组合根
已接入，其他 player-facing 组合根仍为 Planned：

```text
MxFramework.UI
  <- MxFramework.UI.FairyGUI.Manifest
  <- project generated manifest/binder

MxFramework.UI + MxFramework.Audio
  <- MxFramework.UI.Audio
  <- project composition root
```

`MxFramework.UI` 与 `MxFramework.Audio` 继续相互独立且 `noEngineReferences=true`。可选 `MxFramework.UI.Audio` bridge 同样保持 noEngine，只通过 `IAudioService` 播放，不引用 Unity 或 FMOD 类型。

## 6. 反馈语义

### 6.1 Current 公共类型

```csharp
namespace MxFramework.UI
{
    public enum MxUiFeedbackKind
    {
        None = 0,
        Click = 1,
        Confirm = 2,
        Back = 3,
        Error = 4
    }

    public readonly struct MxUiFeedbackPolicy
    {
        public MxUiFeedbackPolicy(
            MxUiFeedbackKind activation,
            MxUiFeedbackKind success,
            MxUiFeedbackKind failure);

        public MxUiFeedbackKind Activation { get; }
        public MxUiFeedbackKind Success { get; }
        public MxUiFeedbackKind Failure { get; }
    }
}
```

`MxUiFeedbackPolicy` 是 noEngine、不可变、NoAlloc 值类型。策略只描述何时播放哪类反馈，不保存 Cue、Event 或 FMOD 信息。

### 6.2 标准策略

| 用途 | Activation | Success | Failure |
| --- | --- | --- | --- |
| 普通切换、分页、选择、打开子界面 | Click | None | None |
| 返回、关闭、取消 | Back | None | None |
| Apply、Save、购买、危险操作确认 | None | Confirm | Error |
| 明确静音 | None | None | None |

规则：

- `Activation` 在命令通过 modal gate、确定会转发给业务 Sink 时播放。
- `Success` / `Failure` 只能由真实 outcome 触发，不能在按钮按下时预判。
- `Cancelled` 只移除 pending invocation，首版默认不播放声音。
- 一个 phase 最多播放一个反馈。
- 不按 `*.close`、`*.save` 等字符串后缀推断策略。所有非默认策略必须来自生成规格。
- 保留命令 `ui.cancel` 由框架注册全局 `Back / None / None` 策略，但仍须经过现有 modal gate。

### 6.3 binding contract v2

Current `mx.uiComposerBindingContract.v2` 为每个 command binding 显式物化策略：

```json
{
  "slotId": "qualityNext",
  "controlName": "btnQualityNext",
  "nodeType": "ComponentInstance",
  "required": true,
  "bindPath": "",
  "commandId": "character_test.settings.quality.next",
  "feedback": {
    "activation": "Click",
    "success": "None",
    "failure": "None"
  }
}
```

结果型命令：

```json
{
  "slotId": "save",
  "controlName": "btnSave",
  "nodeType": "ComponentInstance",
  "required": true,
  "bindPath": "",
  "commandId": "character_test.settings.save",
  "feedback": {
    "activation": "None",
    "success": "Confirm",
    "failure": "Error"
  }
}
```

返回命令：

```json
{
  "slotId": "close",
  "controlName": "btnClose",
  "nodeType": "ComponentInstance",
  "required": true,
  "bindPath": "",
  "commandId": "character_test.settings.close",
  "feedback": {
    "activation": "Back",
    "success": "None",
    "failure": "None"
  }
}
```

兼容规则：

- C# generation spec 新增 command 时，省略策略表示默认 `Click / None / None`；生成器必须把三个值显式写入 v2 contract 和 generated manifest。
- v2 contract 中有 `commandId` 但缺少 `feedback`、字段为空或包含未知枚举值时，生成和 Composer preflight 必须失败。
- 读取 v1 contract 时，内存兼容投影统一为 `None / None / None`，不得因工具升级让旧界面自动发声。
- v1 文件只有在 generation spec 明确补齐策略并重新生成后才写为 v2。
- `feedback` 进入 contract hash。策略变化会阻断持有旧 hash 的 Save/Apply/Sync/Publish。
- feedback 不写入 Screen Blueprint、package XML 或 `ui-composer-state.json`。
- Phase 1B 只把 CharacterTest Settings 作为 v2 样板；Main Menu、Combat HUD 等现有 v1 contract 仍是静音兼容样板。
- 后续运行时组合只能为已迁移到 v2 的面板构建并注入 policy registry；不得把所有旧 generated manifest 一次性接入反馈路由。

## 7. 命令派发与结果关联

### 7.1 Additive command identity

Current `MxUiCommand` 已增加只读 `long InvocationId`：

- 保留现有三参数构造函数，生成 `InvocationId = 0`，避免破坏已有调用方。
- 增加带 invocation 的构造或 `WithInvocationId(...)` 值复制入口。
- `0` 表示尚未经过反馈路由；有效 invocation 从 `1` 单调递增，溢出时跳过 `0`。
- invocation 只用于 UI 表现关联，不进入 gameplay authority、SaveState、Replay 或网络协议。

### 7.2 Current outcome 契约

```csharp
public enum MxUiCommandOutcomeStatus
{
    Success = 0,
    Failure = 1,
    Cancelled = 2
}

public readonly struct MxUiCommandOutcome
{
    public long InvocationId { get; }
    public MxUiCommandOutcomeStatus Status { get; }
    public string ErrorCode { get; }
}

public interface IMxUiCommandOutcomeSink
{
    void Complete(in MxUiCommandOutcome outcome);
}

public interface IMxUiCommandOutcomeBinding : IDisposable
{
}

public interface IMxUiCommandOutcomeSource
{
    IMxUiCommandOutcomeBinding BindOutcomeSink(
        IMxUiCommandOutcomeSink sink);
}
```

- outcome 必须引用路由分配的有效 invocation。
- 结果型 handler 实现 `IMxUiCommandOutcomeSource`；`MxUiFeedbackCommandSink` 包装 inner sink 时建立并持有唯一 outcome binding。
- 一个 outcome source 同时只允许一个 active binding。空 sink 或第二次绑定必须 fail-fast，不能覆盖旧 owner。
- binding 是 LowFreqAlloc 的作用域令牌。只有创建它的 owner 可以 Dispose；重复 Dispose 幂等，旧 generation 的令牌不能解除后来建立的 binding。
- `MxUiFeedbackCommandSink.Dispose()` 必须先释放 outcome binding，再清空 pending invocation，最后断开 player；Dispose 后不接受新 command。
- 已 Dispose decorator 收到迟到 outcome 时只记录 `UI_AUDIO_OUTCOME_AFTER_DISPOSE` 并忽略，不能访问 Audio Service。
- 同步 handler 可在 `Enqueue` 返回前完成；异步 handler 可稍后完成，但必须先回到该 UI route 的 owner thread。
- 非结果型命令不要求报告 outcome。
- `ErrorCode` 只用于低频诊断，不选择具体 Cue。

线程契约：

- `MxUiFeedbackCommandSink` 在构造时捕获 UI owner thread；`Enqueue`、`Complete`、binding Dispose 和 decorator Dispose 都是 main-thread-only。
- 后台任务不得直接调用 `Complete`，也不得间接触发 `IMxUiFeedbackPlayer` / `IAudioService`。异步 handler 负责通过项目组合根已有的主线程调度入口把 outcome 回送到 owner thread。
- 首版不在 UI 模块内新增后台 outcome 队列。错误线程调用属于开发错误，必须 fail-fast，并由 focused test 覆盖。
- 线程归属与 `MxFramework.UI` 的 noEngine 边界兼容：核心只比较构造时捕获的 managed thread id，不引用 Unity 类型。

### 7.3 Current feedback command sink

`MxUiFeedbackCommandSink` 是 `IMxUiCommandSink` 的 additive decorator，不修改原接口：

1. 接收 Binder 或 Cancel bridge 发出的 `MxUiCommand`。
2. 按 `SourceViewId + CommandId` 从生成的 policy registry 精确查找策略。
3. 清理已超过 30 秒的 pending outcome。
4. 分配非零 invocation，并在需要结果反馈时先登记 pending policy。
5. 播放 Activation。
6. 把带 invocation 的 command 转发给 inner sink。
7. 在 owner thread 收到 outcome 后播放 Success 或 Failure，并移除 pending；Cancelled 只移除。

运行顺序必须为：

```text
FairyGUI Binder
  -> MxFairyGuiModalCommandGate
  -> MxUiFeedbackCommandSink
  -> project command sink
```

因此，被 modal gate 丢弃的后台界面命令不会产生声音。

pending 行为：

- 固定容量 64，初始化时一次分配；`Enqueue` / `Complete` 在容量内 NoAlloc。
- 容量满时仍转发业务命令，但不登记该 invocation 的结果反馈，并记录 overflow 诊断。
- 每次 `Enqueue` / `Complete` 使用可注入的单调时钟清理超过 30 秒的记录，不增加 Tick/Update。
- 重复、未知、已过期或 `InvocationId = 0` 的 outcome 不播放，记录结构化诊断。
- 快速重复同一命令会得到不同 invocation，结果可乱序完成。
- source view 关闭不会自动取消 pending；30 秒内到达的真实结果仍播放。业务取消应显式报告 Cancelled。
- inner sink 抛出的异常不转换成业务 Failure；路由移除该 invocation、记录诊断，并保持现有异常策略。
- route/session teardown 会清空全部 pending；之后到达的异步 outcome 按 disposed 规则静音，不得落到重建后的 Audio Session。

### 7.4 播放接口

```csharp
public interface IMxUiFeedbackPlayer
{
    bool TryPlay(
        MxUiFeedbackKind kind,
        in MxUiFeedbackContext context);
}
```

`MxUiFeedbackContext` 提供 view id、command id、invocation id 和 Activation/Success/Failure phase，仅用于诊断。`TryPlay` 返回 `false` 表示静音或播放被拒绝；返回值不能改变命令执行结果。

## 8. Audio bridge 与内容

### 8.1 稳定 Cue

首版使用以下稳定 Cue key：

| Feedback kind | Cue key | 当前状态 |
| --- | --- | --- |
| Click | `ui.click` | Planned；已有可绑定的 UI Click Event |
| Confirm | `ui.confirm` | Planned；已有可绑定的 UI Confirm Event |
| Back | `ui.back` | Planned；已有可绑定的 UI Back Event |
| Error | `ui.error` | Planned；Event 和 Cue 均缺失 |

实施时必须通过 Audio Workbench：

1. 为 Click、Confirm、Back 建立 Active Cue 并绑定现有 Event Definition。
2. 为 Error 建立新的 2D one-shot Event Definition 和 Cue。
3. 完成真实 Unity/FMOD Preview、人工审听和发布门禁。
4. 发布并验证 Generated/StreamingAssets Runtime Catalog mirror。

UI 代码不得直接分配 numeric Cue/Event ID；ID 由 Audio Workbench 的单调分配器管理。

### 8.2 `MxFramework.UI.Audio`

Current `MxAudioUiFeedbackPlayer`：

- 初始化时通过 `IAudioCueKeyDefinitionProvider` 解析并缓存完整、不可变的 `AudioCueDefinition`，不得只缓存 event id。
- 播放时使用 Cue 的 `EventId` 和 `DefaultParameters` 构造 `AudioPlayRequest.Create2D(...)`，保留 Cue 的 authoring 语义。
- 所有请求 priority 为 `80`。
- 使用静态 trace id：`audio.ui.click`、`audio.ui.confirm`、`audio.ui.back`、`audio.ui.error`，不在点击路径拼接字符串。
- 不存在 Cue、Event、Audio Service 或后端时返回 `false` 并记录诊断。
- 不因音频失败抛异常或阻断 command sink。
- 四个 UI Cue 必须为 `AudioCuePlayMode.OneShot`、`MissingPolicy=Mute`；默认参数允许存在并必须完整转发。
- 所有 Event 必须为 2D、OneShot 并路由到 `AudioMixBusIds.Ui`；Catalog/Workbench validator 负责阻断 Cue play mode、MissingPolicy、空间模式、循环属性或 Bus 漂移。

Current authoring contract 通过 manifest metadata
`"uiFeedbackCueSet": "v1"` 显式启用；启用后 Audio Workbench validator 要求
`ui.click`、`ui.confirm`、`ui.back`、`ui.error` 四个 Cue 完整存在，并验证上述
Cue/Event/Bus 契约。未启用该 metadata 的通用 manifest 不受这组产品内容约束。

截至 2026-07-25，Error 源音
`Assets/Audio/MxFramework/Product/UI/error.wav` 与只触碰
`event:/MxFramework/Product/UI/Error` 的定向 FMOD authoring 脚本已经准备。
FMOD Studio 2.03.14 在本机命令行 authoring 时发生应用级意外退出，项目未留下
半成品 Event；因此 manifest 尚未启用 `uiFeedbackCueSet`，四个 Cue 也未保存为
Draft。后续必须先在能正常打开该项目的 FMOD Studio 会话中执行定向脚本，再由
Audio Workbench 保存 Draft、预览、人工审听、批准和发布；禁止为绕过工具故障
直接手改 FMOD Metadata XML 或 Runtime Catalog。

## 9. Composer 与生成链变化

Current 生成链能力：

- `MxFairyGuiCommandBinding` 和 `MxUiCommandDescriptor` 增加 `MxUiFeedbackPolicy`。
- manifest generator 对新命令使用默认 Click 策略，并在生成 C#、binding contract 和 runtime policy registry 时保持一致。
- UI Composer Authoring Core 支持只读解析/验证 v2 feedback，纳入 contract hash、workspace preflight 和 drift diagnostics。
- Web Inspector 在绑定区显示“即时 / 成功 / 失败”，不提供编辑控件。
- generated-source check 比较 policy，任何 manifest/contract/binder 漂移都失败。
- FairyGUI helper、component XML writer、package source和发布产物不增加 feedback 内容。

CharacterTest Settings 已把 generated policy registry 接入 player-facing runtime
composition，并由业务结果驱动 Confirm/Error。其他界面尚未接入；在四个 Cue
发布前，Settings 会按缺 Cue 降级为静音，业务命令仍正常执行。

新 UI 的日常创作流程变为：

```text
generation spec 声明 commands
  -> 普通命令自动默认 Click
  -> 特殊命令显式选择 Back / result / silent policy
  -> 生成只读 binding contract v2
  -> Composer 分配 binding slot
  -> Apply / Publish
  -> runtime composition 注入 feedback decorator
```

## 10. CharacterTest 首个垂直切片

### 10.1 Settings

Current 首个迁移切片是 `MxCharacterTestSettingsUi/SettingsPanel`：

- Quality、Language、VSync、Audio Category 和 Mute 使用 `Click / None / None`，这些命令不报告 outcome。
- Close 和 `ui.cancel` 使用 Back。
- Apply、Save、Reload、Save Input 和 Reset Input 使用 `None / Confirm / Error`。
- Start Input Rebind 使用 `None / Click / Error`：成功开始监听只播放 Click，无法开始时播放 Error。
- handler 只为上述结果型命令报告 outcome：Apply/Save 使用现有 apply/save result；Reload 只有 `Success && !HasIssues` 才报告 Success；Start/Save/Reset Input 使用对应 `MxSettingsUiOperationResult`。普通 Click 命令不得报告 outcome。
- Command Sink 中对 `AudioProductEventIds` 的硬编码提前播放逻辑已删除。
- UI Bus 音量或 mute 命令本身仍按命令执行时的当前有效音量播放；修改 working profile 但尚未 Apply 时不暗中改变 Audio Service。

Settings Host 的 Current 路由顺序为 generated binder → modal gate →
`MxUiFeedbackCommandSink` → Settings 业务 Sink。组合根保留 Audio Cue Catalog，
并向 `MxAudioUiFeedbackPlayer` 注入 `IAudioService` 与完整 Cue provider；配置失败
或 teardown 时先释放 outcome binding，再释放自有 layer host。当前 outcome 都在
Unity/UI 主线程同步完成；未来异步 handler 必须先调度回该 owner thread。

### 10.2 后续迁移

Settings 验证通过后，按以下顺序迁移：

1. Main Menu。
2. Network。
3. Ability Binding。
4. 其他 Composer-owned player-facing package。

旧 v1 contract、legacy `MxCharacterTestHud` 和 UI Toolkit surface 不因首个切片自动启用声音。

## 11. 诊断与失败策略

建议诊断至少区分：

| Code | 含义 |
| --- | --- |
| `UI_AUDIO_POLICY_MISSING` | v2 runtime command 没有可用策略。 |
| `UI_AUDIO_CUE_MISSING` | 反馈 kind 没有有效 Cue/Event。 |
| `UI_AUDIO_PLAY_REJECTED` | `IAudioService` 拒绝播放。 |
| `UI_AUDIO_PENDING_OVERFLOW` | 64 个 pending 槽已满。 |
| `UI_AUDIO_OUTCOME_UNKNOWN` | outcome invocation 不存在或已经完成。 |
| `UI_AUDIO_OUTCOME_EXPIRED` | outcome 超过 30 秒。 |
| `UI_AUDIO_HANDLER_EXCEPTION` | inner sink 异常导致 pending 被清理。 |
| `UI_AUDIO_OUTCOME_WRONG_THREAD` | command/outcome/binding 生命周期从非 owner thread 调用。 |
| `UI_AUDIO_OUTCOME_BINDING_CONFLICT` | outcome source 已有 active owner，拒绝覆盖绑定。 |
| `UI_AUDIO_OUTCOME_AFTER_DISPOSE` | route/session teardown 后收到迟到 outcome。 |

诊断只进入低频 snapshot/Console/测试证据，不在正常点击路径拼接动态消息。缺音频是表现降级，不改变命令结果。

## 12. 测试与验收矩阵

### 12.1 noEngine / 生成测试

| 场景 | 预期 |
| --- | --- |
| 新 generation command 未指定 policy | 显式生成 `Click / None / None`。 |
| v2 command 缺 feedback | generation/preflight 失败。 |
| v2 包含未知 feedback kind | 解析失败并定位字段。 |
| v1 contract 读取 | 投影为静音，不改写源文件。 |
| policy 变化 | contract hash 变化并阻断 stale Apply。 |
| manifest/contract/generated binder policy 不一致 | generated-source check 失败。 |
| 普通 command | modal gate 接受后播放一次 Click 并转发。 |
| Disabled button | 不入队、不播放。 |
| modal gate 拒绝 | 不转发、不播放。 |
| Back / `ui.cancel` | 播放一次 Back。 |
| 结果 Success / Failure / Cancelled | 分别播放 Confirm / Error / 静音。 |
| Reload 或输入操作失败 | 结果策略登记 pending 并播放 Error，不产生 unknown outcome。 |
| 快速重复同一命令、结果乱序 | 每个 invocation 只完成一次。 |
| duplicate / unknown outcome | 不播放，写诊断。 |
| outcome 超过 30 秒 | 不播放，写 expired 诊断。 |
| 后台线程直接 Complete | fail-fast，不访问 player/backend。 |
| outcome source 重复绑定 | fail-fast，原 owner 保持有效。 |
| binding/decorator Dispose 后迟到 outcome | 静音并写 after-dispose 诊断。 |
| pending 容量满 | 命令仍转发，结果反馈降级并写诊断。 |
| 缺 Audio Service/Cue | 命令仍执行。 |
| UI Cue 带默认参数 | 完整转发参数，不把 Cue 降级为裸 Event。 |
| UI Cue play mode/MissingPolicy/Bus 非法 | Workbench/Catalog validation 阻断。 |
| Audio backend 拒绝 | 命令结果不变，写播放诊断。 |

### 12.2 Unity/FairyGUI focused 验收

CharacterTest Settings 必须验证：

- Composer-owned 真包加载成功，关键按钮仍为 `GButton`。
- 普通选项只播放 Click。
- Close 按钮和 Esc/Cancel 只播放 Back。
- Apply/Save 成功只播放 Confirm，不先播放 Click。
- Apply/Save 失败只播放 Error。
- Reload、Start/Save/Reset Input 的成功与失败符合各自结果策略，不产生 unknown outcome。
- Disabled 控件无声音。
- 连续快速点击不会串错结果。
- 后台异步完成先回到主线程；route/session teardown 后迟到结果不播放。
- UI Bus 音量和 mute 能控制全部四类反馈。
- Console 无 error，Audio diagnostics 无未知 Event/Bus/Cue。
- FMOD/Unity 真实可听审验覆盖 Click、Confirm、Back、Error，不以 Null backend 测试代替。

## 13. 分阶段实施

### Phase 1：noEngine contract 与生成链

- Current（Phase 1A）：feedback 类型、invocation/outcome 契约、单所有者 binding、main-thread-only decorator、固定容量 pending 和 policy registry。
- Current（Phase 1B）：contract v2、v1 静音兼容、Authoring Core/Web 只读展示、generated policy 投影和 focused tests；CharacterTest Settings 是首个 v2 contract 样板，但尚未接入运行时反馈路由。

### Phase 2：Audio bridge 与内容

- Current：独立可选的 `MxFramework.UI.Audio` bridge、完整 Cue 语义缓存/播放、
  focused noEngine/EditMode 测试，以及 opt-in 四 Cue/Event/Bus validator。
- Current：Error 源音与定向 FMOD authoring 脚本已经准备。
- Planned：在可正常工作的 FMOD Studio 会话建立 Error Event，再由 Audio
  Workbench 保存四个 Draft Cue、完成真实审听、批准和发布。

### Phase 3：CharacterTest Settings

- 迁移 Settings manifest、contract、composition 和 command outcome。
- 删除局部硬编码 event 播放。
- 完成 noEngine、Unity focused 和可听验收。

### Phase 4：其他正式玩家 UI

- 逐包迁移 Composer-owned commands。
- 根据真实产品需要另行设计 Hover、Focus、Open/Close；不得在首版 decorator 中提前塞入未定义行为。

## 14. 完成定义

只有同时满足以下条件，才能把自动 UI 音效描述为 Implemented：

- contract v2、生成器、Composer 只读展示和 hash/preflight 已落地。
- noEngine feedback/outcome/router API 已落地且保持兼容。
- 四个 UI Cue 均存在、Ready、已审听并发布到 Runtime Catalog。
- CharacterTest Settings 不再直接引用 `AudioProductEventIds`。
- focused tests 覆盖默认、Back、成功、失败、静音、缺失、重复、超时和异步结果。
- Unity 真包与 FMOD 真实可听验收通过。
- `Docs/Interfaces/Audio.md`、UI Composer 文档和 Current 能力文档已按实现事实同步。
