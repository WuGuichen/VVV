# MxFramework Agent Game Feature Guide

> 版本 0.1.0 | 2026-05-11
>
> 本文是 agent 基于 MxFramework 开发游戏功能、小游戏、Playable Demo、Runtime Showcase 和场景验证时的强制执行规范。核心目标是：任何游戏功能都先映射到框架已有能力和接口，再决定是否补扩展点，避免绕过框架重新造系统。Playable 只是其中一种交付等级，不是本文的唯一目标。

---

## 1. 适用范围

当任务包含以下任一目标时，必须先读本文，再开始设计或编码：

- 开发任何游戏功能：输入、技能、属性、Buff、Modifier、AI、资源、配置、UI、场景流、存档、回放、诊断、战斗物理或运行时工具。
- 基于框架制作小游戏、玩法 Demo、Playable Demo、Runtime Showcase。
- 新建或修改用于验证框架能力的 Unity 场景。
- 给现有 Demo 增加输入、UI、场景流、资源、存档、回放、诊断或战斗物理能力。
- 让 AI/agent 自动生成游戏逻辑、关卡验证、手测入口或演示资源。

如果只是修复单个底层模块 bug，优先读对应 `Docs/Interfaces/*.md` 和任务文档；但只要改动会被游戏层调用，仍要遵守本文的框架优先原则。

---

## 2. 工作入口

开始前按顺序读取：

1. `AGENTS.md`
2. `Assets/AGENTS.md`
3. `Assets/Scripts/MxFramework/AGENTS.md`
4. `Docs/CAPABILITIES.md`
5. `Docs/USAGE.md`
6. 本文
7. 需求涉及的 `Docs/Interfaces/*.md`

已有标准样板：

| 样板 | 用途 |
| --- | --- |
| `Assets/Scripts/MxFramework/Demo/Tetris/` | 纯 C# 小游戏规则、`RuntimeHost`、`RuntimeCommandBuffer`、Replay hash、SaveState 的最小验证路径。 |
| `Assets/Scripts/MxFramework/Demo/Breakout/` | AppFlow / SceneFlow、UI Toolkit 可玩入口、连续运动、碰撞、关卡推进和多状态 Demo。 |
| `Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceRunner.cs` | Gameplay / Ability / Attribute / Buff / Modifier / RuntimeHost / SaveState 的组合根样板。 |
| `Assets/Scripts/MxFramework/Demo/Combat/` | Combat Motion / Combat Physics / debug report / 手测输入的样板。 |

新增小游戏优先复制这些样板的分层方式，不复制不必要的业务细节。

---

## 3. 框架优先开发合约

agent 开发游戏功能时，默认不是“写一个能跑的局部实现”，而是“把功能接到 MxFramework 的既有系统里”。开工前必须先回答：

1. 这个需求属于哪些框架能力域？
2. 对应公共 API、接口文档、示例和测试在哪里？
3. 本次是组合已有 API、补默认实现、补扩展点，还是确实需要新增模块？
4. 如果不使用某个已有模块，缺口是什么，为什么适配层比直接复用更合理？
5. 新代码会不会形成和框架平行的输入、实体、Buff、资源、UI、场景、存档或回放系统？

硬规则：

- 能用 `RuntimeHost` / Command / Replay / SaveState 表达的运行时流程，不自建生命周期、命令队列、回放或存档系统。
- 能用 `Gameplay`、`Attributes`、`Buffs`、`Modifiers`、`AI` 表达的玩法能力，不新建平行实体/技能/数值/状态系统。
- 能用 `Input` 表达的玩家意图，不在业务规则里直接读 Unity 输入。
- 能用 `Resources` / `Config` 表达的数据和资源引用，不散落魔法路径、Inspector-only 规则数据或临时 `Resources.Load`。
- 能用 `AppFlow` / `SceneFlow` 表达的状态和场景切换，不把状态机写进 UI 回调。
- 能用 UI Toolkit 和 `MxFramework.UI.Toolkit` 表达的运行时 UI，不新增正式 OnGUI 入口。
- 需要 Unity 专属能力时，写 adapter/view/bridge；不要让 Unity 组件成为框架 runtime 的隐藏依赖。

---

## 4. 开工前必须输出 API 复用计划

编码前必须先写一张计划表。没有这张表，不应创建脚本、场景或资源。

| 需求点 | 优先使用的框架 API / 模块 | 本次是否使用 | 不使用时的原因 |
| --- | --- | --- | --- |
| 游戏主循环 / 固定帧 | `RuntimeHost`、`IRuntimeModule`、`RuntimeClock`、`RuntimeFrame` | 必填 | 必填 |
| 玩家输入 / AI 指令 | `InputSnapshot`、`InputCommandQueue`、`RuntimeCommandBuffer` | 必填 | 必填 |
| 命令验证 / 回放 | `RuntimeCommand`、`IRuntimeCommandValidator`、`RuntimeReplayRecorder`、`IRuntimeHashContributor` | 必填 | 必填 |
| 存档 / 恢复 | `RuntimeSaveState`、`IRuntimeSaveStateProvider`、`IRuntimeSaveStateRestorer`、`RuntimeSaveStateJson` | 必填 | 必填 |
| 场景状态 / 场景切换 | `AppFlowController`、`SceneFlowController`、`UnitySceneFlowDriver` | 按需 | 必填 |
| 实体 / 技能 / 目标 | `GameplayWorld`、`RuntimeEntity`、`IAbility`、`GameplayTargetingService` | 按需 | 必填 |
| 属性 / Buff / Modifier | `AttributeStore`、`BuffPipeline`、`ModifierPipeline`、`CounterStore` | 按需 | 必填 |
| 资源加载 / 预热 | `ResourceManager`、`ResourceCatalog`、`ResourcePreloadService`、Provider | 按需 | 必填 |
| 移动 / 碰撞 / 物理查询 | `CombatKinematicMotor`、`CombatPhysicsWorld`、`ExplainQuery`、既有 Motion API | 触发即必填 | 必填 |
| 运行时 UI | UI Toolkit、`MxFramework.UI.Toolkit` 复用控件、UXML / USS | 必填 | 必填 |
| 诊断 / 调试快照 | `Diagnostics`、模块 snapshot、运行时 summary | 必填 | 必填 |
| 配置驱动 | `ConfigSchema`、`ConfigTable<T>`、`ConfigRegistry`、`Config.Runtime` 工厂 | 按需 | 必填 |

判断规则：

- “按需”不等于可忽略；只要需求触发该能力，就必须优先使用对应模块。
- 如果现有模块无法满足，先说明缺口，再实现最小适配层；不能直接实现一套平行系统。
- 适配层命名要表达桥接职责，例如 `XxxRuntimeModule`、`XxxUnityView`、`XxxConfigAdapter`。

---

## 5. 交付等级和可玩定义

每个任务最终必须声明交付等级，只能选一个：

| 等级 | 含义 |
| --- | --- |
| `Framework Feature` | 框架能力或扩展点已实现，有接口/测试/文档，但不承诺可玩场景。 |
| `Runtime Slice` | 纯 C# 或 runtime 组合闭环已完成，可测试，但没有完整 Unity 可玩入口。 |
| `Playable` | 有可打开场景或入口，按 Play 即可操作并完成玩法闭环。 |
| `Blocked` | 因缺 Unity、资产、外部依赖或设计决策无法完成。 |

只要任务明确使用“小游戏”、“Playable Demo”、“可玩”、“试玩入口”或“做一个 Demo”这类目标，默认交付等级必须是 `Playable`。代码/测试 ready 但没有 Unity 场景入口，只能标记为 `Runtime Slice` 或 `Framework Feature`，不能标记为 `Playable`、`Accepted` 或 `Done`。

可玩交付必须同时满足：

- `Assets/Scenes/` 下存在可打开的 `.unity` 场景，或已有场景中存在明确的可玩入口。
- 场景中有唯一清晰的 Composition Root，并已绑定必要的 `MonoBehaviour`、配置、UXML、USS、`PanelSettings` 和运行时资源。
- 打开场景按 Play 后，玩家无需手工再挂组件、拖引用或创建对象，就能看到 UI 和玩法对象。
- 至少有一个输入路径能操作游戏状态，例如键盘/手柄/UI Button -> command buffer -> runtime tick -> 可见状态变化。
- 有明确的胜利、失败、重置、暂停或等价闭环；不能只是对象出现在场景里。
- UI Toolkit HUD 可见，关键 Label / Button 文本非空，颜色 alpha 大于 0。
- Console 无新增 error；warning 必须解释并记录是否可接受。
- 场景、资源和文档描述一致，`Docs/USAGE.md` 必须写清打开方式和操作方式。
- 最终回复必须明确写出“Playable 验证结果”；如果未完成上述任一项，必须写“未达到 Playable，只完成 Runtime Slice”。

资产生成约束不是降低可玩标准的理由。不能手写 `.unity` / prefab / `.asset` YAML 时，agent 必须使用 Unity Editor、Unity MCP、现有 Editor 菜单或项目脚本生成并保存资产；如果当前环境无法操作 Unity，则任务应停在 `Runtime Slice`，并把“生成可玩场景”作为未完成阻塞项，而不是宣称完成 playable。

---

## 6. 标准分层

新增小游戏或 Runtime Showcase 默认采用以下分层：

```text
Pure Game Rules
  - 纯 C# 状态、规则、碰撞/结算/计分
  - 不继承 MonoBehaviour
  - 不读取 UnityEngine.Time / Input / SceneManager
  - 可被 EditMode 测试直接实例化

Runtime Module
  - 实现 IRuntimeModule 或接入 RuntimeHost
  - 消费 RuntimeCommandBuffer / InputCommandQueue 中的命令
  - 推进纯 C# 规则
  - 输出 Runtime hash、diagnostics、SaveState

Unity Composition Root
  - MonoBehaviour 只负责装配 RuntimeHost、输入采集、UI 绑定和表现同步
  - 不承载核心玩法规则
  - 不绕过 RuntimeCommandBuffer 直接改游戏状态

UI Toolkit View
  - UXML / USS 定义结构和视觉
  - C# 只绑定状态、按钮事件和可见性
  - 正式手测入口不用 OnGUI，OnGUI 只作为临时 fallback

Scene / Config / Assets
  - 场景通过 Unity Editor、Unity MCP 或现有 Editor 菜单创建
  - 配置用 ScriptableObject、JSON、ConfigTable 或 Catalog 表达
  - 不手写 .unity / prefab / .asset YAML
```

允许例外：

- 一次性最小 bug 复现可以只写纯 C# 测试。
- 调试探针可以临时用 `MonoBehaviour`，但最终 Demo 必须回到上述分层。
- 视觉表现层可以使用 Unity 组件，但权威状态仍来自 Runtime 模块或纯 C# 规则。

---

## 7. 能力映射硬规则

### 7.1 主循环和时间

必须：

- 用 `RuntimeHost` 驱动模块生命周期。
- 用显式 `RuntimeFrame` / `deltaTime` 推进规则。
- 固定帧小游戏优先参考 Tetris / Breakout 的 runner 结构。

禁止：

- 在多个 `MonoBehaviour.Update()` 里分散推进权威玩法状态。
- 在纯规则层直接读取 `Time.deltaTime`。
- 用 Unity 帧率作为回放或测试的唯一时间来源。

### 7.2 输入和命令

必须：

- Unity 输入只转换为意图或命令。
- 命令进入 `RuntimeCommandBuffer` 或 `InputCommandQueue` 后再被 Runtime 消费。
- 命令 ID、frame、payload 要能被测试和回放读取。

禁止：

- 在核心规则里直接调用 `Input.GetKey`、`PlayerInput` 或 UI Button 回调修改权威状态。
- 为每个 Demo 自己写不可回放的输入队列。

### 7.3 回放、Hash 和确定性验证

必须：

- 新小游戏至少提供一个 result hash 或诊断摘要，用于判断回放结果是否一致。
- 有固定帧或命令输入时，优先接入 `RuntimeReplayRecorder` / playback runner。
- 测试覆盖正常回放和 hash mismatch 或等价错误路径。

禁止：

- 只靠 Game 视图表现判断逻辑正确。
- 把随机数、时间或 Unity 物理结果隐藏在不可重放路径里。

### 7.4 存档

必须：

- 游戏状态可保存时，优先实现 `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer`。
- JSON roundtrip 使用 `RuntimeSaveStateJson` 或已有 SaveState 管线。
- SaveState DTO 包含 schema version 和可诊断错误。

禁止：

- 每个 Demo 自己随意定义一套不带版本和错误码的 JSON 存档。
- Restore 后跳过必要的 hash / snapshot 验证。

### 7.5 场景和 AppFlow

必须：

- 多场景、多状态 Demo 使用 `AppFlowController` / `SceneFlowController` 表达 Boot、Menu、Loading、Gameplay、GameOver 等状态。
- Unity 场景加载通过 `UnitySceneFlowDriver` 或明确的适配层接入。
- 可玩 Demo 必须提供可打开场景；只提交代码、UXML、USS 和测试不算可玩。

禁止：

- 在玩法逻辑里直接调用 `SceneManager.LoadScene`。
- 把场景名和状态切换散落在多个 UI 回调中。

### 7.6 Gameplay、属性、Buff 和技能

必须：

- 有实体、阵营、生命、属性、技能、目标选择时，先评估 `MxFramework.Gameplay`。
- 有数值属性时使用 `AttributeStore`。
- 有持续效果、层数、过期时使用 `BuffPipeline`。
- 有条件效果、计数器、词条式逻辑时使用 `ModifierPipeline` / `CounterStore`。

禁止：

- 新建平行的 HP / Buff / Modifier / Ability 管线，除非计划中说明现有管线缺口。
- 把 WGame 特有角色、元素、关卡规则写进框架公共类型。

### 7.7 资源和配置

必须：

- 稳定资源引用优先使用 `ResourceKey` / Catalog / Provider。
- 运行前预加载使用 `ResourcePreloadService` 或现有资源管理能力。
- 数据驱动 Demo 用 `ConfigSchema`、`ConfigTable<T>`、`ConfigRegistry` 或 `Config.Runtime` 工厂描述。

禁止：

- 在多个脚本里散落魔法路径和 `Resources.Load`。
- 只靠 Unity Inspector 字段表达应被测试或导出的规则数据。

### 7.8 UI

必须：

- 正式 Playable Demo 使用 UI Toolkit。
- UI 结构在 UXML，视觉在 USS，C# 只做状态绑定。
- 使用已有 `MxFramework.UI.Toolkit` 控件和主题 token；缺控件时优先扩展该模块。
- Play Mode 验证关键 Label / Button 文本非空、可见颜色 alpha 大于 0。
- `UIDocument.panelSettings`、`VisualTreeAsset` 和 `StyleSheet` 必须在最终场景或配置资产中显式绑定；运行时临时创建 `PanelSettings` 只能作为调试兜底，不能作为可玩验收依据。

禁止：

- 新正式 Demo 只用 OnGUI。
- 在 UI 控制器里写核心规则。
- 多个 HUD 重叠显示却没有统一入口。

### 7.9 Combat 和物理

必须：

- 任何基于框架制作的小游戏 / Playable Demo / Runtime Showcase，只要包含移动、碰撞、命中、拾取、触发区、墙体阻挡、出口判定或物理查询，玩法权威必须优先来自框架物理 / Motion 模块。
- Combat 规则、命中查询、战斗移动优先用 `MxFramework.Combat`。
- 权威命中、拾取、出口、范围检测来自 `CombatPhysicsWorld.Query` / `ExplainQuery` 或对应框架物理 API。
- 角色、棋子、球体、投射物、机关等移动优先用 `CombatKinematicMotor` 或既有 Motion API；若现有 Motion API 不足，先补最小框架适配层，再做 Demo。
- Unity 场景对象只能作为 view / authoring marker / adapter 输入来源；runtime state、胜负、拾取、碰撞结果不能以 Unity `Collider`、`OnTriggerEnter`、`OnCollisionEnter`、`Rigidbody` 或 `UnityEngine.Physics` 查询为权威。
- 如确实需要 Unity 物理做临时可视化或编辑器辅助，必须在 API 复用计划中标明“非权威 view-only”，并提供框架物理结果与 Unity 显示同步的测试。

禁止：

- 在正式小游戏 / Playable Demo / Runtime Showcase 中用 Unity 自带物理开发玩法逻辑。
- 用 `Rigidbody` 推进权威位置、用 Unity `Collider` 阻挡作为权威碰撞、用 trigger/collision 回调推进 checkpoint、拾取、伤害或通关。
- 用 `UnityEngine.Physics.Raycast/Overlap*` 作为权威命中、拾取或范围查询。
- Unity marker 直接成为 runtime state 的唯一真源。

---

## 8. MonoBehaviour 使用规范

新增 `MonoBehaviour` 前必须说明它属于哪一类：

| 类型 | 允许职责 | 不允许职责 |
| --- | --- | --- |
| Composition Root | 创建和连接 RuntimeHost、服务、配置、UI、输入适配器 | 写核心规则、绕过命令缓冲修改状态 |
| Input Adapter | 读取 Unity Input System，把输入转换为命令 | 直接结算游戏状态 |
| View Adapter | 同步 Runtime snapshot 到 Transform / UI | 持有权威规则状态 |
| Scene Bootstrap | 加载配置、创建入口对象、挂载 HUD | 分散管理多个玩法系统生命周期 |
| Debug Probe | 临时调试、可视化、Explain 数据 | 成为正式玩法依赖 |

每个正式 Demo 应尽量只有一个清晰的 Composition Root。确实需要多个 `MonoBehaviour` 时，必须说明生命周期和数据流。

---

## 9. Unity 资产生成规范

遵守 `Assets/AGENTS.md` 的文件生成约束：

- `.cs`、`.uxml`、`.uss`、`.json` 可以直接生成。
- `.unity`、`.prefab`、`.asset`、`.mat`、`.controller`、`.anim` 默认不手写。
- 需要新增场景时，优先用 Unity Editor / Unity MCP / 现有 Editor 菜单生成并保存。
- 新增资源必须带 `.meta`，但 `.meta` 优先让 Unity 生成。
- 场景只做舞台和入口，不预挂一堆相互抢生命周期的 Demo 组件。
- 如果任务目标是可玩 Demo，不能因为不手写 YAML 就省略场景；必须改用 Unity Editor / Unity MCP / Editor 菜单生成场景，或明确降级为 Runtime Slice。

---

## 10. 测试和验收

新增小游戏 / Demo 至少满足：

- 任何游戏功能任务必须满足本文“框架优先开发合约”。
- 可玩任务必须完成本文“交付等级和可玩定义”。
- 纯 C# 规则有 EditMode 测试。
- 命令输入路径有测试：输入 -> command buffer -> runtime tick -> state change。
- Replay 或 hash 路径有测试；如果暂不接入，必须说明原因和补齐任务。
- SaveState roundtrip 有测试；如果游戏没有可保存状态，必须说明。
- UI Toolkit Play Mode 或等价测试验证关键文本/按钮可见。
- 涉及场景流时覆盖 AppFlow / SceneFlow 的 busy、success、failure 或等价状态。
- 涉及资源时覆盖 Catalog / Provider / preload / missing asset 诊断。
- 涉及 Combat 时覆盖 hit / miss / movement collision / explain report。
- Playable Demo 必须至少进行一次 Unity Play Mode 或 Unity MCP 级别的 smoke 验证；无法运行时，不得宣称 playable 完成。

提交前检查：

```bash
svn status
Tools/GitNexus/gitnexus.sh detect-changes
```

能自动跑测试时，优先跑最小相关测试集；不能跑时，在最终回复中说明原因。

---

## 11. 文档同步

新增或升级小游戏 / Demo 时，按影响同步：

| 变更 | 必须同步 |
| --- | --- |
| 新增可用能力 | `Docs/CAPABILITIES.md` |
| 新增接入方式或手测入口 | `Docs/USAGE.md` |
| 新增或修改公共 API | `Docs/INTERFACES.md` 和对应 `Docs/Interfaces/*.md` |
| 新增任务切片 | `Docs/Tasks/*.md` |
| 改变验收标准 | `Docs/QUALITY_GATE.md` |
| 改变模块边界 | `Docs/ARCHITECTURE.md` / `Docs/DESIGN.md` |

已完成的 `Docs/Tasks/*.md` 可以作为历史记录；当前能力以 `Docs/CAPABILITIES.md` 和 `Docs/USAGE.md` 为准。

---

## 12. 最终回复必须报告

完成后，agent 的最终回复必须包含：

- 交付等级：`Framework Feature` / `Runtime Slice` / `Playable` / `Blocked`，只能选一个。
- 如果声称 `Playable`，列出场景路径、入口对象、操作方式和 Play Mode 验证结果。
- 使用了哪些框架模块。
- 哪些地方没有使用现有模块，原因是什么。
- 新增了哪些 `MonoBehaviour`，分别属于 Composition Root / Input Adapter / View Adapter / Scene Bootstrap / Debug Probe 哪一类。
- 新增或修改了哪些场景、UXML、USS、配置、测试和文档。
- 实际运行了哪些测试或验证命令。
- 未完成的风险和后续补齐项。

如果发现需求会迫使 agent 绕过本文规则，应先停下来说明冲突点，再给出符合框架方向的替代方案。
