# 运行时配置切片 01：Data Driven Attributes / Buffs / Modifiers

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

把已完成的 `RuntimeVerticalSlice` 从**硬编码运行脚本**推进到**配置驱动运行脚本**：一个 Unity Scene、一个 Demo Runner，从框架级示例配置创建 `Attributes`、`Modifiers`、`Buffs`，并在 Play Mode 中看到同样的运行闭环。

这一步的核心不是做真实配置导入器，而是证明框架已经具备“配置数据 -> Runtime 对象 -> 可运行结果”的最小链路。

## 背景

上一任务 `RUNTIME_VERTICAL_SLICE_01_PLAYABLE_ATTRIBUTES_BUFFS_MODIFIERS.md` 已完成：

- `Assets/Scenes/RuntimeVerticalSlice.unity`
- `Assets/Scripts/MxFramework/Demo/RuntimeVerticalSliceRunner.cs`
- 手写属性、Modifier、Buff，并在 Play Mode 中可见运行状态。

现有配置基础能力包括：

- `ConfigTable<T>`
- `ConfigRegistry`
- `BasicBuffConfig`
- `BasicModifierConfig`
- `ConfigBuffFactory<TConfig>`
- `ConfigModifierFactory<TConfig>`
- `RuntimeConfigPatchMerger`

当前缺口：

- 运行时 Demo 仍以硬编码方式创建 Buff / Modifier。
- `Config.Runtime` 尚未在 Playable Scene 中证明可用。
- `ConfiguredModifier` 当前只保存 `IModifierConfig`，默认没有 effects；`BasicModifierConfig.Parameters` 尚未被解释执行，不能直接表达 `Attack +50`。
- Authoring Editor / Mod / Runtime Preview 后续都需要一个稳定的运行时配置入口。

## 非目标

本任务不做：

- WGame 真实配置导入。
- Excel / TSV / JSON 文件解析器。
- `RuntimeConfig.bytes` 二进制产物。
- 字段级 Patch。
- 完整 Mod 包格式。
- 外部 Authoring Editor UI。
- Runtime Preview Server 场景目标接入。
- 复杂角色系统、技能系统、AI 或战斗表现。

如果本任务开始依赖真实 WGame 数据、外部编辑器或 Mod 包，说明范围失控。

## 最小设计

### 示例配置来源

首版配置可以在 Demo 代码内构造，但必须以 `ConfigTable<T>` / `ConfigRegistry` 形式进入 Runtime，不允许直接 new 具体 Buff 作为主路径。

建议新增一个专门的 Demo 配置构建器：

```text
Assets/Scripts/MxFramework/Demo/RuntimeConfigSliceDemoData.cs
```

职责：

- 创建 `ConfigTable<BasicBuffConfig>`。
- 创建 `ConfigTable<BasicModifierConfig>`。
- 注册到 `ConfigRegistry`。
- 返回一个只读的 Demo Runtime Context。

### 示例数据

属性初始值：

| 字段 | 值 |
| --- | --- |
| `Hp` | `1000` |
| `Attack` | `100` |
| `Defense` | `20` |

Modifier：

| 字段 | 值 |
| --- | --- |
| `Id` | `200001` |
| `NameKey` | `modifier.attack_up.name` |
| `DescriptionKey` | `modifier.attack_up.desc` |
| `ParamIndex` | `AttrAttack` |
| `Parameters` | `+50` |

Buff：

| 字段 | 值 |
| --- | --- |
| `Id` | `100001` |
| `NameKey` | `buff.burning.name` |
| `DescriptionKey` | `buff.burning.desc` |
| `Duration` | `5s` |
| `MaxLayers` | `3` |
| `ModifierId` | `200001` 或 `0`，按现有 Runtime Factory 能力决定 |

### Config.Runtime 必补能力

当前 `ConfiguredModifier` 不能表达 `Attack +50`，这是 `Config.Runtime` 的框架缺口，不允许用 Demo 专用 create 回调绕过去。

本任务必须在框架层补齐一个通用路径，使下面的配置语义成立：

```text
BasicModifierConfig
  Id = 200001
  ParamIndex = AttrAttack
  Parameters[0] = 50
```

验收时，`ConfigModifierFactory<BasicModifierConfig>` 创建出的运行时 modifier 必须能通过框架公共 API 让 `Attack: 100 -> 150`。具体实现可以是：

- 让 `ConfiguredModifier` 默认解释 `ParamIndex + Parameters[0]` 为属性增量效果。
- 或新增更明确的 Config.Runtime 公共类型，例如配置驱动的 attribute modifier / effect。

无论采用哪种实现，都必须满足：

- 能被 `ConfigModifierFactory<BasicModifierConfig>` 主路径创建。
- 不写入 Demo 专用效果类作为主路径。
- 不引入 WGame 业务语义。
- 同步更新相关接口文档或 `Docs/USAGE.md`。

### 运行脚本

建议改造现有：

```text
Assets/Scripts/MxFramework/Demo/RuntimeVerticalSliceRunner.cs
```

职责：

- 增加 `[SerializeField] private bool _useConfigDriven`，在同一个场景中切换硬编码切片和配置驱动切片。
- 调用 Demo 配置构建器。
- 用 `ConfigBuffFactory<BasicBuffConfig>` 创建 `BuffPipeline`。
- 用 `ConfigModifierFactory<BasicModifierConfig>` 创建 Modifier。
- 将配置创建出的 Buff / Modifier 作用到测试目标。
- 在 `OnGUI` 显示配置表、运行状态和事件日志。

可以复用 `RuntimeVerticalSliceRunner` 中的测试目标模型，但要把硬编码路径和配置驱动路径拆成清晰的私有方法，避免变成难读的巨大脚本。

## 可视输出

Game View 至少显示：

```text
Runtime Config Slice
Config:
  Buff 100001 Burning duration=5 maxLayers=3 modifier=200001
  Modifier 200001 Attack +50
Runtime:
  Hp: 1000 -> 965 -> 930
  Attack: 100 -> 150
  Buffs: 100001 stack=1 remaining=3.75s
Events:
  Config registry ready
  Modifier 200001 created from config
  Buff 100001 created from config
  Buff 100001 tick damage=35
```

必须让使用者能区分：

- 哪些内容来自配置。
- 哪些内容是运行时状态。
- 哪些内容是事件日志。

## 操作方式

复用现有场景：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

不新建 `RuntimeConfigSlice.unity`。垂直切片共享同一个验证场景，避免每推进一步都新增场景造成维护成本和心智负担线性增长。

推荐操作：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 在 `RuntimeVerticalSliceRunner` 上勾选 `_useConfigDriven`。
3. 点击 Play。
4. 观察 Game View。
5. 等待 5 秒，确认 Buff 到期。
6. 停止 Play。

## 代码边界

必须遵守：

- Demo 配置数据不进入 `Config.Runtime` 核心模块。
- `Config.Runtime` 不反向依赖 Demo。
- 不引入 WGame 业务类型或真实配置文件。
- 不用 Demo 专用 create 回调掩盖 Config.Runtime 缺口。
- 如果修改 `BasicBuffConfig` / `BasicModifierConfig` 语义，必须保持框架通用性，并同步更新接口文档。
- 不手写 `.unity` YAML；场景由 Unity Editor / Unity MCP 创建。
- `.meta` 由 Unity 生成后提交。

## 验收标准

- Unity 编译无项目 error。
- 场景可打开。
- 点击 Play 后可见 `Runtime Config Slice` 输出。
- `ConfigRegistry` 中能查到 Buff `100001` 和 Modifier `200001`。
- Buff 由 `ConfigBuffFactory<BasicBuffConfig>` 创建，而不是主路径手写 new。
- Modifier 由 `ConfigModifierFactory<BasicModifierConfig>` 主路径创建，不使用 Demo 专用 create 回调。
- Attack 从 `100` 变为 `150`。
- `AttributeChangedEvent` 从配置驱动的属性系统正确发布，至少覆盖 Attack `100 -> 150` 和 Burning tick HP 变化。
- Burning Buff 每秒 tick 并修改 HP。
- Buff 到期后从 pipeline 移除。
- 运行 10 秒无异常。
- 停止 Play 后无持久化脏数据写入。
- 文档说明如何从硬编码切片过渡到配置驱动切片。

## 测试建议

### 自动化优先

建议新增 EditMode 测试：

```text
RuntimeConfigSlice_CreatesRegistry
RuntimeConfigSlice_ConfigBuffFactoryCreatesBuff
RuntimeConfigSlice_ConfigModifierFactoryCreatesModifier
RuntimeConfigSlice_DataDrivenBuffTicksHp
RuntimeConfigSlice_PublishesAttributeChangedEvent
```

如果创建 PlayMode 测试成本较低，再补：

```text
RuntimeConfigSliceScene_RunsWithoutErrors
```

### 手动验证

- 使用 Unity MCP 创建或打开场景。
- Play 运行 10 秒。
- 读取 Console，确认没有 error。
- 记录 Game View 输出。

## 文档更新

完成实现后必须更新：

- `Docs/USAGE.md`
- `Docs/ROADMAP.md`
- 必要时更新 `Docs/Interfaces/Config.md`
- 必要时更新 `Docs/Interfaces/Buffs.md`
- 必要时更新 `Docs/Interfaces/Modifiers.md`

文档必须明确：

- 这是框架级示例配置，不是 WGame 真实数据。
- 真实项目接入时只需要把自己的导入器最终转成 `ConfigTable<T>` / `ConfigRegistry`。
- Runtime Config Slice 是 Authoring Editor、Mod、Runtime Preview 的基础层。

## 状态

`Implemented (r1149)`

## 优先级

当前优先级高于：

- Buff Authoring Editor 深化。
- Runtime Preview 场景目标。
- Mod 包格式。
- AI 辅助闭环。

原因：先证明运行时能被配置驱动，再把编辑器和预览体验接上去。

## 后续衔接

本任务完成后，再进入：

1. Buff Authoring 垂直切片强化：让外部编辑器产出的 Buff 草稿与 Runtime Config Slice 对齐。
2. Runtime Preview Scene Target：把配置驱动 Buff 应用到测试场景目标。
3. Runtime Config Patch / Mod 最小文件包：把内存构造配置替换为可加载的 Patch / Mod 文件。
