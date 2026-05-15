# MxAnimation 01：运行时动画表现层契约设计

> Issue: #90
> 状态：Design Draft
> 任务等级：S2
> 交付类型：纯文档契约
> 日期：2026-05-15

## 目标

在实现 MxAnimation 之前，先固定第一阶段运行时动画表现层契约。本切片不新增运行时代码，不创建 Unity 序列化资产，不修改场景、Prefab 或 YAML。Issue #90 的实际写入范围仅为本文档。

MxAnimation 是表现层。它把框架侧动作意图和表现事件转换为 Unity 动画播放、VFX、SFX、镜头反馈和诊断信息。它不能成为 Combat 命中帧、取消窗口、无敌窗口、伤害窗口、Replay hash 或任何确定性状态的权威来源。

## 当前依据

现有系统边界如下：

- `MxFramework.Resources` 已提供 noEngine 的 `ResourceKey`、Catalog、Provider、`IResourceManager`、handle、引用计数和诊断契约。
- `MxFramework.Resources.Unity` 已通过 `UnityResourceTypeResolver` 把常用 `ResourceTypeIds` 映射到 Unity 类型。
- `ResourceCatalogValidator` 是 noEngine 结构校验器，只检查 key、provider、address、重复项、dependency 和依赖环。
- `ResourceCatalogEditorValidator` 在 Editor 侧通过 `AssetDatabase` 和 `UnityResourceTypeResolver.Resolve(typeId)` 校验 Unity 资产存在性和主资源类型。
- `MxFramework.Combat` 已拥有确定性的 fixed-frame `CombatActionTimeline`、动作状态、动作事件、武器轨迹、命中结算、hash、replay 和诊断。
- 现有 `CombatAnimatorMapping`、`CombatAnimatorDriver`、`CombatAnimationUnityModule` 只是旧 Animator / clip 直连表现桥的迁移参考，不能复制为新的运行时契约。

## 非目标

- Issue #90 不修改运行时代码。
- Issue #90 不创建 Unity 序列化资产。
- noEngine 运行时 DTO 不持有 `AnimationClip`、`ScriptableObject`、`GameObject`、`Animator`、`PlayableGraph`、Unity asset path 或 Unity object reference。
- 不把 Unity bridge 代码放进现有 noEngine `MxFramework.Combat.Animation` 程序集。
- 暂不引入 `MxFramework.Animation.Resources`。Animation 第一阶段直接使用现有 Resources 契约；只有后续实现证明 `ResourceKey`、Catalog label、preload group 和 provider policy 无法承载真实复杂度时，才考虑独立资源适配程序集。
- 不让 animation time、normalized time、Animator state 或 Playable state 反向驱动 Combat 命中、取消、无敌、伤害、replay 或 hash 权威。
- 不引入 WGame 特化角色、技能、元素或真实 Buff 数据。

## 模块契约

建议的首版模块如下：

| 模块 | 类型 | 引用权限 | 职责 |
| --- | --- | --- | --- |
| `MxFramework.Animation` | Runtime, noEngine | `MxFramework.Core`、`MxFramework.Resources`、可选 `MxFramework.Diagnostics` | DTO 契约、播放请求、表现事件、animation set 定义、诊断 snapshot、稳定 ID 和枚举。 |
| `MxFramework.Animation.Unity` | Runtime, Unity | `MxFramework.Animation`、`MxFramework.Resources`、`MxFramework.Resources.Unity`、UnityEngine、Playables | Playables 后端、通过 `IResourceManager` 加载 `AnimationClip`、resource handle 生命周期、Unity 播放诊断和 fallback clip 策略。 |
| `MxFramework.Animation.Editor` | Editor only | `MxFramework.Animation`、`MxFramework.Animation.Unity`、`MxFramework.Resources`、`MxFramework.Resources.Unity`、UnityEditor | Unity authoring asset、预览、校验、从 Unity 引用导出 noEngine DTO、FBX / `.anim` 工具入口。 |
| `MxFramework.Combat.Animation.Unity` | Runtime, Unity bridge | `MxFramework.Combat`、`MxFramework.Animation`、`MxFramework.Animation.Unity`、必要 UnityEngine | 监听 Combat action events 和 fixed-frame 表现事件，转成 MxAnimation 播放 / 停止 / crossfade 请求。 |

依赖方向：

```text
MxFramework.Resources
  <- MxFramework.Animation
      <- MxFramework.Animation.Unity
          <- MxFramework.Combat.Animation.Unity

MxFramework.Combat
  <- MxFramework.Combat.Animation.Unity
```

`MxFramework.Combat` noEngine 不得引用 `MxFramework.Animation.Unity`、Unity Playables、Animator、`AnimationClip` 或任何 Unity 后端。如果后续需要 noEngine Combat-to-animation DTO adapter，它可以放在 `MxFramework.Animation`，或放在仅引用 noEngine 契约的桥接程序集；Unity 播放后端仍必须留在 Combat 之外。

未来路径和 asmdef 建议：

| 未来路径 | 未来 asmdef | 说明 |
| --- | --- | --- |
| `Assets/Scripts/MxFramework/Animation/` | `MxFramework.Animation.asmdef` | noEngine 运行时契约；只引用 Resources 和可选 Diagnostics。 |
| `Assets/Scripts/MxFramework/Animation.Unity/` | `MxFramework.Animation.Unity.asmdef` | Unity Playables 后端；引用 Animation、Resources、Resources.Unity 和 UnityEngine。 |
| `Assets/Scripts/MxFramework/Editor/Animation/` | 复用现有 Editor asmdef 或新增 Editor asmdef | Editor authoring、预览、校验和导出；允许引用 UnityEditor 和 Animation.Unity。 |
| `Assets/Scripts/MxFramework/Combat.Animation.Unity/` | `MxFramework.Combat.Animation.Unity.asmdef` | Combat action events 到 Animation.Unity 请求的 Unity bridge；不得放入现有 noEngine Combat animation assembly。 |

## noEngine Runtime DTO 契约

实现时具体 API 名称仍可调整，但后续代码应保持下面的契约形状和边界。

### `MxAnimationSetDefinition`

用途：描述一个 actor、archetype、weapon set、skin 或 demo entity 可用的全部表现绑定。

建议字段：

- `string SetId`
- `int Version`
- `ResourceKey DefaultClip`
- `ResourceKey FallbackClip`
- `IReadOnlyList<MxAnimationActionBinding> Actions`
- `IReadOnlyList<MxAnimationPresentationEvent> Events`
- 可选 diagnostics / warmup labels 或 tags

规则：

- Clip、VFX、SFX、camera shake 等表现资源一律使用 `ResourceKey`。
- `DefaultClip` / `FallbackClip` 在 MxAnimation 02 后使用 `ResourceTypeIds.AnimationClip`。
- 不保存 `AnimationClip`、`ScriptableObject`、`GameObject`、`Animator`、`PlayableGraph`、Unity GUID 或 `Assets/...` 路径。

### `MxAnimationActionBinding`

用途：把逻辑 action 或 state key 绑定到表现播放数据。

建议字段：

- `string BindingId`
- `int ActionId` 或 `string ActionKey`
- `ResourceKey Clip`
- `MxAnimationLayerId Layer`
- `MxAnimationCrossFadeRequest DefaultCrossFade`
- `MxAnimationAlignmentPolicy AlignmentPolicy`
- `float PlaybackSpeed`
- `bool Loop`
- `IReadOnlyList<MxAnimationPresentationEvent> PresentationEvents`

规则：

- action identity 可以镜像 Combat action id、Gameplay ability id 或项目层 key，但 binding 仍只是表现数据。
- binding 不能定义命中帧、取消窗口、无敌窗口、伤害窗口或权威 root motion。
- Unity authoring 可以用 clip 做预览；导出的运行时 DTO 只保存 `ResourceKey`。

### 播放请求

`MxAnimationPlayRequest`：

- target presentation actor id
- action / binding key
- 可选 clip `ResourceKey` override
- layer id
- playback speed
- 起播 offset，使用 seconds 或 normalized presentation time
- alignment policy
- diagnostics correlation / source id

`MxAnimationStopRequest`：

- target presentation actor id
- layer id 或 binding key
- fade out duration
- stop reason
- correlation / source id

`MxAnimationCrossFadeRequest`：

- target presentation actor id
- from layer 或 current state
- target binding key 或 clip key
- fade duration
- fade curve / policy id
- target start offset
- alignment policy
- outgoing release policy

这些请求是 command-like 的表现 DTO。除非上层模块显式把它们包装成 view control 命令，否则它们不是权威 `RuntimeCommand`。它们不进入 Combat replay hash。

### 状态与 ID

`MxAnimationLayerId`：

- 使用 string / int ID 包装成稳定 value object 或 readonly struct。
- 示例：`Base`、`UpperBody`、`Additive`、`Override`。
- noEngine 契约不依赖 Unity Animator layer index。

`MxAnimationFadeState`：

- current clip key
- next clip key
- layer id
- fade elapsed seconds
- fade duration seconds
- blend weight
- 状态枚举，例如 `None`、`FadingIn`、`Playing`、`FadingOut`、`CrossFading`、`Stopped`、`Failed`
- 只记录诊断用 handle/key 状态，不保存直接 resource handle

`MxAnimationAlignmentPolicy`：

- `None`
- `StartAtZero`
- `PreserveNormalizedTime`
- `MatchPresentationTime`
- `UseCombatFrameAnchor`

`UseCombatFrameAnchor` 表示 bridge 可根据 Combat fixed-frame 来源数据计算动画起播 offset。请求发出后，Unity 播放仍然不具备权威性。

### 表现事件

`MxAnimationPresentationEvent`：

- `string EventId`
- `MxAnimationEventTimeDomain TimeDomain`
- 按时间域保存 frame、seconds 或 normalized time
- event kind：VFX、SFX、Camera、Footstep、UI feedback
- 对 VFX / SFX / camera-shake profile 使用 `ResourceKey` payload
- 可选 socket / bone / tag string，用于表现层定位
- 可选 diagnostics severity / category

`MxAnimationEventTimeDomain`：

- `Seconds`
- `NormalizedTime`
- `CombatFrame`
- `PresentationFrame`

规则：

- 表现事件只用于 VFX、SFX、camera feedback、footstep feedback 和 UI feedback。
- normalized-time 事件是非权威事件，不得影响 Combat hash、replay、命中判定、取消窗口、无敌窗口或伤害窗口。
- 需要确定性的事件必须来源于 `CombatActionTimeline` fixed-frame event，再向外桥接为表现请求或表现事件。
- 如果表现事件来源于 Combat，它可以携带 Combat action / frame correlation 供诊断使用，但 Unity 播放不能反向驱动权威逻辑。

### 诊断

`MxAnimationDiagnosticSnapshot`：

- actor count
- layer states
- current / fallback clip keys
- loading / failure summaries
- active fades
- pending presentation events
- recent requests
- recent resource errors
- Unity adapter 提供的 backend name 和 graph status

Snapshot 是 debug / read-only 数据，允许分配。它不能作为 SaveState、replay hash 或 Combat 决策来源。

## Unity Authoring Asset 边界

Unity authoring asset 可以为了作者体验、预览、校验和导出而引用 `AnimationClip`。这个边界只属于 Editor / authoring。

允许：

- 在 `MxFramework.Animation.Editor` 范围内创建带直接 `AnimationClip` 字段的 ScriptableObject authoring asset。
- Editor 预览窗口临时实例化 Playables / Animator 状态进行校验。
- exporter 把 authoring clip 引用转换为 `ResourceKey` DTO。
- Editor validator 检查每个 clip 是否有匹配的 Catalog entry 和 `ResourceTypeIds.AnimationClip`。

不允许：

- Runtime noEngine DTO 持有 `AnimationClip`、`ScriptableObject`、`GameObject`、`Animator`、`PlayableGraph`、Unity GUID 或 `Assets/...` 路径。
- Runtime 业务配置通过 Unity `Assets/...` 路径查找 clip。
- 把 `CombatAnimatorMapping` 复制成新运行时模型。它直接持有 `AnimationClip` 的方式只作为旧迁移参考。

## MxAnimation 02 的 ResourceKey 与 AnimationClip 前置要求

后续 MxAnimation 02 必须补齐一等 `AnimationClip` 类型支持，但不得把 noEngine validator 改成 Unity 类型检查器。

必需变更：

- 在 `MxFramework.Resources` 中新增 `ResourceTypeIds.AnimationClip = "AnimationClip"`。
- 在 `UnityResourceTypeResolver` 中把 `ResourceTypeIds.AnimationClip` 映射到 `typeof(AnimationClip)`。
- `ResourceCatalogValidator` 保持 noEngine 和结构校验职责，只继续校验字符串、key、provider、安全相对地址、重复项、dependency 和依赖环；它不得引用 Unity，也不得理解 `AnimationClip`。
- `ResourceCatalogEditorValidator` 通过 `UnityResourceTypeResolver.Resolve(typeId)` 覆盖 `AnimationClip` 类型检查，和现有 Unity 资源类型校验路径保持一致。
- Unity 后端运行时加载动画时走 `IResourceManager.Load<AnimationClip>` 或等价 async API，不硬编码 Unity 项目路径。

Catalog / runtime 规则：

- Clip 的 `ResourceKey.TypeId` 使用 `ResourceTypeIds.AnimationClip`。
- Catalog address 归 provider 所有。`assetBundle` 继续使用已文档化的 bundle / address 格式；Editor / demo `memory` provider 可以用 `providerData.assetPath` 做校验元数据，但这不是业务运行时配置。
- noEngine animation DTO 中不出现 `Assets/...` 路径。

## Playables 后端资源 handle 生命周期

Unity Playables 后端持有它加载的 clip handle，持有时间以 clip 是否仍被播放图引用为准。

规则：

- incoming handle owner：调用 `IResourceManager` 的后端拥有返回 handle，并必须通过 `IResourceManager.Release` 释放。
- 调用方只有在显式声明 ownership transfer 时，才可以把预加载 handle 交给后端。否则后端必须通过 `Load<T>` / `LoadAsync<T>` 获取并持有自己的 handle，结束后通过 `Release<T>` 释放。
- 当前 clip handle 在 clip 播放期间保持持有。
- crossfade outgoing clip handle 保持持有，直到 outgoing playable 有效权重为 0，且 graph 不再引用该 clip。
- 重复播放 / crossfade 同一 clip 时，后端应尽量复用已有 loaded record，并依赖 ResourceManager 引用计数语义，避免无意义复制 unmanaged playback resource。
- default / fallback clip 应随 actor backend 生命周期或 scene warmup group 生命周期常驻，取决于 composition root 策略。只要它仍是配置的失败 fallback，就不能释放。
- `PlayableGraph.Destroy` 是后端 shutdown 边界：停止播放、断开 / 销毁 playables，并释放该后端拥有的全部 handles。
- 加载失败必须输出诊断，至少包含 actor、layer、requested key、fallback key、provider / resource error 和 source request correlation。
- 加载失败 fallback 顺序：先尝试 requested clip；失败后尝试 actor fallback clip；fallback 也失败时停止该 layer 并报告 failed state。不得静默替换为任意 Unity object。
- 后端层 release 应具备幂等性；重复 release 不应崩溃，但如果暴露 ownership bug，应能通过 diagnostics 观察到。

## 表现事件时间域与确定性

表现事件时间域只用于视觉、音频和 UI 调度：

- VFX spawn / despawn
- SFX trigger
- camera shake / profile trigger
- footstep 或 surface feedback
- UI feedback

表现事件时间域不用于：

- Combat hash
- replay authority
- hit determination
- cancel decision
- invincible、super armor、parry 或 damage window
- authoritative movement / physics

Combat 确定性事件必须来源于 `CombatActionTimeline` fixed-frame event。Unity normalized time、Animator transition、Playables time 和 clip length 可能受帧率、导入设置、平台或 blend policy 影响，因此永远不能作为权威。

## Combat 桥接方向

正确方向：

```text
CombatActionRunner / CombatActionTimeline fixed-frame events
  -> MxFramework.Combat.Animation.Unity bridge
      -> MxAnimation play / stop / crossfade requests and presentation events
          -> MxFramework.Animation.Unity Playables backend
```

禁止方向：

```text
Animator / AnimationClip normalizedTime / Playable state
  -> Combat hit / cancel / invincible / damage decisions
```

桥接规则：

- Combat action started / canceled / completed 等事件可以驱动动画表现。
- Animation time、normalized time、Animator state 和 Playable state 不得驱动命中、取消、无敌、parry、super armor、伤害、replay hash 或 fixed-frame Combat state。
- `MxFramework.Combat` noEngine 层不得引用动画 Unity 后端。
- `MxFramework.Combat.Animation.Unity` 可以订阅 Combat events，并调用 MxAnimation 表现后端。
- 诊断 correlation 应尽量保留 Combat entity、action instance 和 frame id。

## 后续 Issue 草案

### MxAnimation 02：ResourceTypeIds AnimationClip

目标：

新增一等 `AnimationClip` 资源类型支持，让 animation DTO 可以通过 `ResourceKey` 引用 clip，并让 Unity Editor validation 能校验 Catalog entry 的主资源类型。

主要文件范围：

- `Assets/Scripts/MxFramework/Resources/ResourceTypeIds.cs`
- `Assets/Scripts/MxFramework/Resources.Unity/UnityResourceTypeResolver.cs`
- `Assets/Scripts/MxFramework/Editor/ResourceCatalogEditorValidator.cs`，仅当测试证明当前 resolver 路径不足时修改
- 必要的 Resources tests 和 Editor validation tests
- 如公共 API 文档需要同步，则更新接口 / 使用文档

验收标准：

- 存在 `ResourceTypeIds.AnimationClip`，字符串值精确为 `AnimationClip`。
- `UnityResourceTypeResolver.Resolve(ResourceTypeIds.AnimationClip)` 返回 `typeof(AnimationClip)`。
- noEngine `ResourceCatalogValidator` 保持 Unity-free，只做结构和字符串校验。
- Editor validation 通过 `UnityResourceTypeResolver.Resolve(typeId)` 校验 Catalog clip entry。
- Runtime 加载示例使用 `IResourceManager`，不硬编码 Unity `Assets/...` 路径。

### MxAnimation 03：Clip Directory + FBX Extraction Tool

目标：

定义并实现 Editor 侧工作流：定位样例 clips、必要时从 FBX / model import 中导出 clips，并生成 Catalog-ready `ResourceKey` entry，且不让 runtime DTO 依赖 Unity asset path。

Issue #93 implementation status:

- Editor-only extractor lives under `Assets/Scripts/MxFramework/Editor/Animation/` and reuses the existing `MxFramework.Editor` assembly boundary.
- Default input source is `Assets/_TempImportedResources/Art/Animations/`.
- Default output root is `Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/`.
- Extracted files use lowercase snake_case `<clip_name>.anim`; resource keys use `art.character.skeleton.animation.<clip_name>` with `ResourceTypeIds.AnimationClip`.
- Default collision policy is `Skip`; existing `.anim` files are reported as `Skipped` and are not overwritten by the default menu or batch method.
- The extractor copies float curves, object reference curves, AnimationEvents as presentation-only metadata, and available clip settings through Unity Editor APIs. It reports Catalog-ready key, labels, bundle name, target path, status, and reason/error, but does not generate or modify Resource Catalog JSON.

主要文件范围：

- `Assets/Scripts/MxFramework/Editor/Animation/` 或等价 Editor-only 目录
- 如果该 Issue 明确移动资产，则涉及既有正式 sample roots 下的 sample clip 目录规范
- Catalog generation / validation helpers
- 可行时补充 tests 或 Editor validation scripts
- 更新对应任务文档，记录已实现的 asset workflow

验收标准：

- 工具只运行在 Editor 代码中，不向 noEngine 模块新增 Unity 引用。
- 提取或发现的 clips 可分配稳定 `ResourceKey` id，并使用 `ResourceTypeIds.AnimationClip`。
- 工具输出不把 Unity `Assets/...` 路径写入业务运行时配置；任何 `assetPath` 只作为 provider / Editor validation 元数据。
- FBX extraction 行为明确、可重复，且不会在未确认时覆盖已创作资产。
- 不引入 WGame 特化动画内容。

### MxAnimation 04：Playables Backend MVP

目标：

实现最小 Unity Playables 后端：可对 MxAnimation DTO 中通过 `ResourceKey` 引用、并由 `IResourceManager` 加载的 clips 执行 play、stop、crossfade、fallback 和 diagnostics。

主要文件范围：

- `Assets/Scripts/MxFramework/Animation/`，如果 noEngine contracts 尚未创建
- `Assets/Scripts/MxFramework/Animation.Unity/`，用于 Playables 后端
- `MxFramework.Animation` 和 `MxFramework.Animation.Unity` asmdef
- 可行时补充 request / state / resource handle 行为测试

验收标准：

- 后端接受使用 `ResourceKey` 的 play、stop、crossfade 请求。
- 后端通过 `IResourceManager` 加载 `AnimationClip`，不硬编码 runtime Unity path。
- outgoing crossfade handle 只在 outgoing playable 权重归零且 graph 不再引用 clip 后释放。
- default / fallback clip 常驻策略明确，并体现在 diagnostics 中。
- `PlayableGraph.Destroy` 释放后端拥有的全部 handles。
- 同 clip 复用 / 引用计数行为由测试或文档化验证路径覆盖。
- 加载失败输出 diagnostics，并按配置的 fallback sequence 执行。

### MxAnimation 05：Combat Presentation Bridge

目标：

实现 Unity 侧 bridge：把 Combat action events 和 fixed-frame presentation events 转成 MxAnimation 表现请求，同时保持 Combat noEngine 权威。

主要文件范围：

- `Assets/Scripts/MxFramework/Combat.Animation.Unity/` 或等价 Unity bridge 目录
- bridge asmdef，引用 `MxFramework.Combat`、`MxFramework.Animation` 和 `MxFramework.Animation.Unity`
- 可行时补充 event-to-request mapping 测试
- 只有该 Issue 明确包含时，才加入小型 demo / composition-root 集成

验收标准：

- Combat action start / cancel / finish events 可触发 MxAnimation play / stop / crossfade 请求。
- Combat fixed-frame presentation events 可触发 VFX / SFX / camera / footstep / UI feedback 事件。
- 不存在让 Animator、normalized time、Playable state 或 clip state 驱动 Combat 命中、取消、无敌或伤害窗口的代码路径。
- `MxFramework.Combat` noEngine 不引用 Unity animation backend assemblies。
- diagnostics 保留 Combat entity / action / frame correlation。
- 旧 `CombatAnimatorMapping` 要么保持迁移兼容，要么被明确替换；但不得把 direct clip reference 复制进新 runtime contract。

## ADR 判断

Issue #90 不需要新增 ADR。本文档已经足以承载本切片的实现契约和后续拆分，且本 PR 不改变已接受的架构方向、版本控制流程或跨项目治理规则。

只有后续实现改变核心依赖方向、引入 `MxFramework.Animation.Resources` 这类新资源子系统、改变 Combat 权威语义，或形成超出 MxAnimation 契约的长期项目级决策时，才应升级为 ADR。

## Issue #90 验收清单

- 新增文档：`Docs/Tasks/MX_ANIMATION_01_DESIGN_CONTRACT.md`。
- 不修改运行时代码。
- 不创建或修改 Unity 序列化资产。
- 不修改 `AGENTS.md`、README 或其他文档。
- 契约覆盖 `MxFramework.Animation`、`MxFramework.Animation.Unity`、`MxFramework.Animation.Editor` 和 `MxFramework.Combat.Animation.Unity`。
- DTO 使用 `ResourceKey` 引用 clip / VFX / SFX / camera-shake 资源，并排除 Unity object references。
- Unity authoring 只可为了 Editor 配置、预览、校验和 DTO 导出引用 `AnimationClip`。
- MxAnimation 02 的 `ResourceTypeIds.AnimationClip` 资源前置要求已经定义。
- Playables handle ownership、crossfade release、fallback residency、graph destroy、failure fallback 和 same-clip reuse / ref-count 原则已经定义。
- 表现事件时间域明确为非权威；需要确定性的事件必须来源于 Combat fixed-frame events。
- Combat bridge 方向保持 Combat 为权威。
- 后续 Issue 草案仅包含 MxAnimation 02、03、04、05。
