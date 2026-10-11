# Settings 接口

> Status: Current
>
> Version: v0.13 | 2026-10-01 | 依赖：Settings noEngine；Settings.Audio -> Audio；Settings.Input -> Input；Settings.Graphics -> Unity/Rendering；Settings.UI -> Settings/Input；CharacterTest FairyGUI -> UI.FairyGUI
> 代码：`Packages/com.vincent.wgameframework/Settings*/`

Settings 模块保存用户偏好 profile，并把 profile 应用到已有框架能力。它不承载 gameplay 权威状态，不进入 Runtime SaveState、Replay、Runtime hash 或网络同步。

## 1. 模块边界

- `MxFramework.Settings` 是 noEngine 核心模块，提供 profile、JSON store、结构化 issue/result 和 applier 扩展点。
- `MxFramework.Settings.Audio` 是 noEngine 音频适配层，只通过 `IAudioService` 应用 Bus volume/muted。
- `MxFramework.Settings.Input` 是 Unity/Input-facing 适配层，包装 `IInputRebindingService` 和 `InputBindingDisplayService`。
- `MxFramework.Settings.Graphics` 是 Unity/Rendering-facing 适配层，包装 `Screen`、`Application.targetFrameRate`、`QualitySettings`、URP asset 运行时属性和 `MxVolumetricFogPassProvider`。
- `MxFramework.Settings.UI` 是 UI-facing 数据和控制层，只输出可绑定 view model 与命令入口，不依赖 FairyGUI 包、场景或具体控件。
- 输入重绑定 override 仍由 `InputRebindingService` 持久化到 `PlayerPrefs`；settings JSON 不复制 InputAction override 数据。显式 Apply/Cancel 页面通过可选 `IInputRebindingEditSessionService` 把多次 rebind/reset 包在同一个草稿事务中。
- 本地化语言偏好保存在 settings JSON 的 `localization.localeId`；旧 JSON 缺该字段时默认 `zh-CN`。
- CharacterTest 默认为 `SettingsUi` 注入一组 player-facing input binding targets；项目可在构造 `CharacterTestSettingsRuntime` 时替换。
- 画质底层可配置项多于默认玩家可见项；正式设置 UI 应按 `MxGraphicsSettingsCatalog.PlayerVisibleDescriptors` 做白名单过滤。
- 通用 Settings 契约兼容旧 profile 的 `QualityPreset` / `UnityQualityLevel`；CharacterTest 保留唯一 Unity Quality level，不向玩家展示 preset，也不在 Apply 时切换 level。玩家逐项设置渲染比例、MSAA、阴影距离、主光阴影图集、级联数和局部灯阴影图集。
- `MxUnityGraphicsRuntime` 在 Play Mode 首次修改 URP 参数时使用运行时 URP asset 副本，退出时恢复原 pipeline 引用；用户偏好不得写回项目 URP asset。
- CharacterTest 接入第一版 FairyGUI `SettingsPanel`，通过 `CharacterTestFairyGuiSettingsPanelHost` 绑定 `SettingsUi` working profile，并调用 `GameManager.SettingsRuntime` 上的 `ApplyCurrentProfile()` / `SaveCurrentProfile()` / `ReloadProfile()`；它不修改场景/Prefab/URP YAML，也不默认常驻遮挡画面。CharacterTest 的产品策略只在 attach 或替换当前 Session `IAudioService` 时自动恢复当前已应用的 Audio profile，避免 Session rebuild 回到默认音量；Graphics/Input 仍不因加载或 attach 自动应用。

## 2. 公开契约

| 类型 | 用途 |
|------|------|
| `MxSettingsProfile` | 用户偏好 profile，格式号固定 `mx.settings.v1`，默认 `ProfileId = "default"` |
| `MxAudioSettings` / `MxAudioBusSetting` | settings JSON 中的音频 Bus 音量和静音配置；volume clamp 到 `0..1` |
| `MxGraphicsSettings` | settings JSON 中的画质偏好；默认全部保持当前运行时状态，只应用显式设置 |
| `MxLocalizationSettings` | settings JSON 中的本地语言偏好，字段为 `LocaleId`，默认 `zh-CN` |
| `MxGraphicsSettingsCatalog` / `MxGraphicsSettingDescriptor` | 画质设置目录和默认玩家可见白名单 |
| `MxSettingsIssue` / `MxSettingsIssueCode` | load/save/apply/input 操作的结构化诊断 |
| `MxSettingsLoadResult` / `MxSettingsSaveResult` / `MxSettingsApplyResult` | 可恢复失败的结构化结果 |
| `IMxSettingsStore` / `MxSettingsJsonFileStore` | settings profile 存取；路径由组合根注入 |
| `IMxSettingsApplier` / `MxSettingsService` | 当前 profile 管理和多 applier 应用 |
| `MxAudioSettingsApplier` | 将 profile 中的 Bus 设置应用到 `IAudioService` |
| `MxUnityGraphicsSettingsApplier` / `MxUnityGraphicsSettingsApplyOptions` / `IMxGraphicsRuntime` | 将 profile 中的画质设置应用到 Unity runtime，支持按项启停应用，并隔离静态 API 便于测试 |
| `MxVolumetricFogSettingsApplier` | 将 settings feature quality 映射到 `MxVolumetricFogPassProvider.SetSettings(...)` |
| `IInputRebindingService` / `IInputRebindingEditSessionService` | 输入重绑定基础能力，以及显式 Apply/Cancel 页面使用的可选草稿事务边界 |
| `MxInputRebindingSettingsController` | 面向设置 UI 的输入重绑定、显示文本、保存/加载/重置和草稿 begin/commit/rollback 控制器 |
| `MxInputBindingTarget` / `MxInputBindingInfo` | 设置 UI 可展示的 action binding 目标和显示文本 |
| `CharacterTestSettingsRuntime` | CharacterTest 的代码层 settings 宿主，暴露 profile 的加载、应用和保存入口 |
| `MxSettingsUiPresenter` | 维护 settings working profile、dirty state、Apply/Save/Reload/ResetDirty、语言切换和输入重绑定命令 |
| `MxSettingsUiViewModel` / `MxSettingsUiSectionViewModel` / `MxSettingsUiItemViewModel` | UI 可绑定的设置页展示模型，包含 localization/graphics/audio/input sections、value text、control kind 和 issues |
| `CharacterTestFairyGuiSettingsPanelHost` | CharacterTest 的 FairyGUI 设置面板宿主，按需打开 Composer-owned `MxCharacterTestSettingsUi/SettingsPanel` 并把按钮命令转给 `SettingsUi` / `CharacterTestSettingsRuntime` |

## 3. 使用约定

```csharp
var store = new MxSettingsJsonFileStore(settingsPath);
var settings = new MxSettingsService(store);
MxSettingsLoadResult load = settings.Load();

settings.RegisterApplier(new MxAudioSettingsApplier(audioService));
settings.RegisterApplier(new MxUnityGraphicsSettingsApplier(new MxUnityGraphicsRuntime()));
MxSettingsApplyResult apply = settings.Apply();
MxSettingsSaveResult save = settings.Save();
```

如果某个组合根已经有运行时参数所有权，可以通过 options 关闭对应项。例如 CharacterTest 的 frame cap 仍由 `CharacterTestSceneProfile.TargetFrameRate` 驱动：

```csharp
settings.RegisterApplier(new MxUnityGraphicsSettingsApplier(
    new MxUnityGraphicsRuntime(),
    new MxUnityGraphicsSettingsApplyOptions(applyTargetFrameRate: false)));
```

```csharp
var graphics = new MxGraphicsSettings(
    resolutionWidth: 1920,
    resolutionHeight: 1080,
    applyWindowMode: true,
    windowMode: MxWindowMode.FullscreenWindow,
    vSyncCount: 1,
    targetFrameRate: 120,
    renderScale: 1.0f);

settings.Save(settings.CurrentProfile.WithGraphics(graphics));
```

```csharp
settings.Save(settings.CurrentProfile.WithLocalization(new MxLocalizationSettings("en-US")));
```

```csharp
var inputSettings = new MxInputRebindingSettingsController(
    inputService.Rebinding,
    inputService.BindingDisplay);

MxInputBindingListResult bindings = inputSettings.ListBindings(new[]
{
    new MxInputBindingTarget("Gameplay/Jump", 0)
});

inputSettings.StartRebind("Gameplay/Jump", 0);
```

显式 Apply/Cancel 页面应在打开时开始草稿，在 Apply 时提交，在 Cancel/关闭时回滚：

```csharp
inputSettings.BeginEditSession();
inputSettings.StartRebind("Gameplay/Jump", 0);

// Apply
inputSettings.CommitEditSession();

// 或 Cancel / Close
inputSettings.RollbackEditSession();
```

生产 `InputRebindingService` 实现该可选事务：完成 rebind 或 Reset 只更新内存 override 并标记 pending；Commit 才写 `PlayerPrefs`，Rollback 恢复打开页面时的 override snapshot。仅实现旧 `IInputRebindingService` 的 adapter 继续保留兼容语义：Commit 回退到 `Save()`、Rollback 回退到 `Load()`；这种 adapter 不提供真正的多步撤销保证，不能冒充 CharacterTest 正式事务证据。

CharacterTest FairyGUI 设置面板默认隐藏，由 `MxCharacterTestUi/MainMenuPanel` 中的 Settings 按钮打开。打开时同时开始 profile working copy 与输入 override 草稿；Apply 应用并保存 profile、提交输入草稿，然后为仍打开的页面开始新草稿；Cancel、Close 或非监听状态的 `ui.cancel`/Esc 会回滚输入草稿、重新加载 profile 并关闭。监听输入时 `ui.cancel`/Esc 只取消当前 rebind，不关闭页面。Language 行会切换 `localization.localeId` 并立即刷新已接入的 CharacterTest FairyGUI player-facing 文本。`SettingsPanel` 使用 `MxUiFoundation` 语义资源、紧凑按钮配方和 `character_test_settings.ui-screen.json`，由 UI Composer/helper 物化到 `MxCharacterTestSettingsUi`；视觉资源不改变 profile、presenter 或命令语义。宿主会委托 `SettingsUi`，因此不会忽略 pending UI edits，也会持续更新 `LastApplyResult` / `LastSaveResult` / `LastLoadResult`：

```csharp
MxSettingsUiViewModel view = gameManager.SettingsRuntime.SettingsUi.BuildViewModel();
gameManager.SettingsRuntime.SettingsUi.SetShadowDistance(120f);

MxSettingsLoadResult reload = gameManager.SettingsRuntime.ReloadProfile();
MxSettingsApplyResult apply = gameManager.SettingsRuntime.ApplyCurrentProfile();
MxSettingsSaveResult save = gameManager.SettingsRuntime.SaveCurrentProfile();
```

`SettingsUi.Apply()` / `SettingsUi.Save()` 仍可用于非 CharacterTest 宿主；CharacterTest UI 默认应走 host 方法以保留宿主 bookkeeping。

CharacterTest 面板画面页提供 Language、VSync、RenderScale、MSAA、ShadowDistance、主光阴影图集分辨率、级联数和局部灯阴影图集分辨率的独立入口；Resolution/WindowMode 仍只读显示。另有 CharacterTest 默认 Audio Bus volume/mute，以及默认 10 个输入动作的 rebind/reset。输入区不是固定控件槽，而是 `GList + SettingsInputBindingRow` 动态渲染：默认显示 2 个分组标题和 10 个动作行，运行时 target catalog 不再受 UI 行数限制，也不会静默截断自定义 targets。

默认 catalog 共 21 个 binding targets：Jump、Primary Attack、Secondary Attack、Interact、Dodge、Sprint、Pause 各提供 keyboard/mouse index 0 与 gamepad index 1；`TargetLock` 提供 keyboard `L` index 0、mouse `Middle Button` index 1 与 gamepad index 2；`UI/Submit`、`UI/Cancel` 各提供 keyboard index 0 与 gamepad index 1。键盘与鼠标页投影主要/备用两列，当前只有 `TargetLock` 正式使用备用列；手柄页投影每个动作的 gamepad target。页面设备切换只改变投影，不改变 Runtime action 或 binding index 的权威身份。

`TargetFrameRate` 不在该面板中开放，因为 frame cap 仍由 `CharacterTestSceneProfile.TargetFrameRate` 驱动。CharacterTest 本地化覆盖 MainMenu、NetworkPanel、SettingsPanel、Combat HUD、StoryDialog 的玩家可见静态文本，以及 AI Lab 战术卡预览中已确认的 `approach-face.v1`、`retreat-to-distance-face.v1`、面向后前向垫步描述、`face-action.v1`（旧 `approach-face-action.v1` 仅兼容）、`retreat-face.v1`、`orbit-face.v1`、`keep-distance-face.v1` 描述模板、参数提示词和未装配占位。这些描述使用同一文本提供器并随语言变化刷新；后撤、环绕与守距时长，以及接近执行时限（≤）／出招预期时长（≈），按模拟时间步长换算为秒，动作名称暂沿用动作目录的 `displayName`。其它运行时生成名字、Debug UI、Editor 工具和 UI Toolkit diagnostics 不纳入该入口。

## 4. 边界和禁止事项

### HDR 显示偏好

`MxGraphicsSettings.HdrOutput` 是 `MxHdrOutputSettings`，通过构造函数末尾可选参数
`hdrOutput` 提供。`MxHdrOutputMode` 为 `Default=0`（不接管原有显示配置）、
`Disabled=1`、`Enabled=2`。`PaperWhiteNits` 支持 80–400，`PeakNits` 支持
400–5000；0 表示使用设备建议值，设备报告无效时分别回退到 200 / 1000。
NaN、Infinity 和非正校准输入回退为自动值。JSON 仍为 `mx.settings.v1`，新增
`graphics.hdrOutput.{mode,paperWhiteNits,peakNits}`；旧存档缺字段时为 Default。
其他画质编辑必须保留 HDR 子配置。`MxSettingsUiPresenter.SetHdrOutput(...)`
只更新 working profile，Apply / Save / Cancel 继续使用既有事务。

`MxHdrOutputSettingsApplier` 消费 `IMxHdrOutputRuntime.TryApply(...)` 和只读
`Status`。`MxHdrOutputStatus` 报告 `MxHdrOutputState`（Unavailable / Sdr /
Switching / Hdr / Failed）、Available、Active、实际校准亮度和原因；期望开启与实际开启
分别表达。当前输出按 `HDROutputSettings.main.active` 判定，异步请求、拒绝、
显示能力变化与五秒请求超时都不会把请求成功误报为 HDR 已激活。

`MxUnityHdrOutputRuntime` 只在 Play Mode 且 URP 可用时接管输出。它拥有临时
global Volume / VolumeProfile，仅覆盖 Tonemapping 和 HDR 校准，不写入场景
Volume、太阳、Look、曝光或调色资产。SDR 使用 ACES；实际 HDR 输出使用
Neutral + BT.2390，以支持任意有效显示峰值（当前 Unity ACES HDR 仅有
1000 / 2000 / 4000 nit 预设）。None 不作为 HDR 输出默认值。环境不支持 HDR
时保留用户偏好并显示 SDR 回退；默认设置不创建覆盖。Dispose 取消未完成的
切换意图、恢复原显示请求/自动映射状态，并释放临时 Volume 和组件。

该 owner 在 Tonemapping 参数实际变化后，于受影响相机下一次渲染前刷新
ViaScripting 的 Volume stack；已有 EveryFrame 相机仍由 URP 更新。它不改变
项目或相机更新策略，也不每帧刷新参数未变的手动 stack。相机 stack 重建和
排除 owner layer 的 mask 变化会重新同步；Default / Dispose 先注销临时 Volume，
再刷新可能保留该覆盖的手动 stack（包括禁用但保有缓存的相机）。

`CharacterTestSettingsRuntime.AttachHdrOutput(runtime, ownsRuntime)` 注册该
applier 并单独恢复已保存的显示偏好；其余 Graphics / Input 自动应用策略不变。
宿主释放 SettingsRuntime 时 Dispose 所拥有的 HDR adapter。画面页显示 HDR
开关、参考白、峰值和实际输出状态；不支持的开启入口禁用，已保存的开启偏好
仍允许关闭。校准项仅在 HDR 可用且 working profile 选择开启时可编辑。
这些控件来自正式 Composer 蓝图和 C# 生成契约。

项目允许 HDR Display Output，但启动时不强制使用 HDR；玩家明确保存的偏好
在运行时恢复。Direct3D11 Editor 不支持 HDR 输出，真实 HDR 亮度及 UI 合成
需要支持的 Player / 系统 / 显示器联合验证，不能用普通 PNG 或模拟状态代替。

新增验证入口：`HdrOutputSettingsTests`。覆盖旧 JSON、校准序列化与非法值、
其他画质编辑保留 HDR、working profile 取消、输出事实与开启意图区分、
Edit Mode 不接管输出。

2026-09-28 本地验证：SettingsFoundation / SettingsUiModel / HDR 初版合计
45/45 通过，最终 HDR 专项 7/7 通过；Unity 编译无错误，重载后 Editor ready，
Console error 为 0。Composer Apply/Publish 和 Player 资源构建完成。真实
设置面板显示不可用原因，原生 Cancel 按钮验证了草稿恢复与关闭；Play Mode
下不支持 HDR 的设备请求安全回退到 SDR/ACES，退出后临时输出 Volume 为 0。
本机 Direct3D11 Editor 的原生 HDR 输出不可用，未声称完成真实 HDR 屏亮度验收。

- 不把 settings JSON 当作玩法存档；玩法状态继续走 Runtime SaveState。
- 不把用户偏好写进 replay/hash；settings 只影响本地表现和输入偏好。
- 不把 `localization.localeId` 进入 Runtime SaveState、Replay hash 或网络同步。
- 不绕过 `IAudioService` 直接调用 FMOD 或 Unity audio API。
- 不绕过 `IInputRebindingService` 直接写 InputAction override 文件。
- 不把所有画质底层项默认暴露给玩家；UI 默认只展示 catalog 中的 player-visible 项。
- 不在 FairyGUI binder、UI Toolkit view 或场景脚本里拼装 applier；UI 应绑定 `MxSettingsUiPresenter` 输出的 view model，并通过宿主或 presenter 提供的统一命令应用。
- 不让 CharacterTest 设置面板直接调用 graphics/audio/input service；面板只同步 working profile 或发出 presenter 命令。
- 不通过 settings 写入 URP / Quality / Scene / Prefab YAML 资产；画质 applier 只做运行时代码应用。
- 不在 Settings core 引入 `UnityEngine`、`UnityEditor`、`FMODUnity` 或 `Unity.InputSystem`。

## 5. 测试入口

- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Settings/SettingsFoundationTests.cs`
- `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Settings/SettingsUiModelTests.cs`

## Directional shadow quality preference

`MxGraphicsSettings.DirectionalShadowQuality` uses `MxGraphicsFeatureQuality.Default / Low / Medium / High` (JSON values `0 / 2 / 3 / 4`). The optional `graphics.directionalShadowQuality` field keeps the `mx.settings.v1` format. Missing values retain scene authoring; invalid values, including `Disabled`, report a load issue and normalize to `Default`. The constructor also normalizes unsupported values. All presenter graphics copy operations preserve the new field.

`MxDirectionalShadowSettingsApplier` lives in `Settings.Graphics` and takes a scene-configuration getter. It maps Low/Medium/High to dynamic R32_UInt resolutions 256/512/1024 and Default to a zero, nonserialized override. It does not change authored resolution, enable flags, static data, center/peripheral bake inputs, bias, filtering, GI or caster eligibility. Applying to a new configuration restores the previous binding; disposing restores the bound configuration's previous override. An explicit quality with no configuration returns `AdapterUnavailable`.

`MxSettingsUiPresenter.SetDirectionalShadowQuality` edits the working profile. CharacterTest's existing Composer-owned settings Graphics page reuses the main-shadow-resolution control slots as the localized “Shadow Quality” row. Selection stays in the draft until Apply; Apply uses the existing apply/save flow, Cancel reloads the saved draft. The runtime registers the adapter against `GameManager.SceneRendering`, reapplies the saved shadow preference on creation and disposes its override with the settings session. Existing URP atlas settings remain in the generic model for other consumers.
