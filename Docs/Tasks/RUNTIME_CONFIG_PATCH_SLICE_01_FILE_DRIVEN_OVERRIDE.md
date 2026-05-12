# 运行时配置 Patch 切片 01：File Driven Override / Mod

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

把已完成的 Runtime Config Slice 从**内存构造配置**推进到**文件驱动配置覆盖**：从一个最小 JSON patch 文件读取 Buff / Modifier 覆盖层，通过 `RuntimeConfigPatchMerger` 合并 Base + Patch / Mod 行，再驱动同一个 `RuntimeVerticalSlice` 场景运行。

这一步的目的不是做完整 Mod 平台，而是证明框架具备最小链路：

```text
Base Config
  + Patch / Mod File
  -> RuntimeConfigPatchMerger
  -> ConfigRegistry
  -> ConfigBuffFactory / ConfigModifierFactory
  -> RuntimeVerticalSlice Play Mode
```

完成后，外部编辑器、Runtime Preview 和 Mod 工作流才有一个共同的文件级运行时入口。

## 背景

前置任务已完成：

- `RUNTIME_VERTICAL_SLICE_01_PLAYABLE_ATTRIBUTES_BUFFS_MODIFIERS.md`
- `RUNTIME_CONFIG_SLICE_01_DATA_DRIVEN_BUFF.md`

现有能力：

- `RuntimeConfigSliceDemoData` 可构造 Demo Base 配置。
- `ConfiguredModifier` 已能表达配置驱动的属性增量。
- `RuntimeVerticalSliceRunner` 可通过 `_useConfigDriven` 切换配置驱动运行。
- `RuntimeConfigPatchMerger` 可合并 Base + Patch / Mod 行。
- `ConfigChangeSet` 可输出行级变更报告。

当前缺口：

- Patch / Mod 覆盖层仍停留在代码内构造。
- 没有框架级最小文件格式。
- 没有文件加载后的运行时可视化验证。
- `ConfigChangeSet` 尚未和 Playable 场景结果一起展示。

## 非目标

本任务不做：

- 完整 Mod 包格式。
- zip / manifest / 签名 / 权限模型。
- 字段级 Patch。
- 真实 WGame 配置导入。
- `RuntimeConfig.bytes` 二进制产物。
- 热更新下载。
- 外部 Authoring Editor 保存流程。
- Runtime Preview Server 场景目标接入。

如果本任务开始处理 Mod 发布、玩家目录扫描或编辑器 UI，说明范围失控。

## 最小文件格式

新增一个框架级 Demo patch 文件，建议路径：

```text
Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json
```

如果项目当前没有 `StreamingAssets`，可由 Unity 创建目录和 `.meta`。文件本身是自定义 JSON，可以由 Agent 直接生成。

首版格式只服务 Demo Buff / Modifier：

```json
{
  "format": "mx.runtimeConfigPatch.v1",
  "sourceId": "demo_patch",
  "layer": "Patch",
  "modifiers": [
    {
      "operation": "Upsert",
      "id": 200001,
      "nameText": "modifier.attack_up.name",
      "descriptionText": "modifier.attack_up.desc",
      "paramIndex": 2,
      "parameters": [80]
    }
  ],
  "buffs": [
    {
      "operation": "Upsert",
      "id": 100001,
      "nameText": "buff.burning.name",
      "descriptionText": "buff.burning.desc",
      "duration": 8.0,
      "maxLayers": 3,
      "modifierId": 200001
    }
  ]
}
```

约束：

- `format` 必填，首版固定 `mx.runtimeConfigPatch.v1`。
- `sourceId` 必填，用于 `ConfigChangeSet.SourceId`。
- `layer` 必填，首版支持 `Patch` / `Mod`。
- `operation` 首版支持 `Upsert` / `Remove`。
- Buff / Modifier 字段名使用框架配置语义，不使用 WGame 字段名。
- 多语言字段仍保存 `LocalizedTextKey` 字符串，不写具体语言文本。

## Runtime 加载器

建议新增：

```text
Assets/Scripts/MxFramework/Config.Runtime/RuntimeConfigPatchJsonLoader.cs
```

职责：

- 从 JSON 文本解析出最小 patch DTO。
- 转成 `ConfigPatchEntry<BasicBuffConfig>`。
- 转成 `ConfigPatchEntry<BasicModifierConfig>`。
- 返回一个 typed patch bundle。

要求：

- 不依赖 UnityEngine。
- 不依赖 UnityEditor。
- 不依赖 WGame。
- 使用 `Newtonsoft.Json` 解析 JSON。项目已安装 `com.unity.nuget.newtonsoft-json: 3.0.2`，它是纯 .NET 库，可用于 `MxFramework.Config.Runtime` 的 noEngine asmdef。
- C# DTO 使用 PascalCase 属性，并通过 `[JsonProperty("camelCaseName")]` 显式映射 JSON 字段名，例如 `paramIndex`、`nameText`、`maxLayers`。
- `format` 必须严格校验为 `mx.runtimeConfigPatch.v1`。
- 解析失败要返回可读错误，不允许静默 fallback 到 Base。
- `format` 缺失或版本不匹配时要返回明确错误，不得继续解析为当前格式。
- 首版可以只支持 `BasicBuffConfig` / `BasicModifierConfig`。

不要使用 `JsonUtility`，因为它依赖 UnityEngine，会破坏 `Config.Runtime` 的 noEngine 边界。不要手写 ad hoc JSON 解析器。

## 场景接入

继续复用：

```text
Assets/Scenes/RuntimeVerticalSlice.unity
```

改造现有 `RuntimeVerticalSliceRunner`：

- 保留 `_useConfigDriven`。
- 新增 `[SerializeField] private bool _usePatchFile`。
- 新增 `[SerializeField] private string _patchFilePath`，默认指向 Demo patch 文件。
- 当 `_useConfigDriven && _usePatchFile` 时：
  - 构造 Base 配置。
  - 读取 patch 文件。
  - 使用 `RuntimeConfigPatchMerger` 合并 Buff / Modifier。
  - 用合并后的表注册 `ConfigRegistry`。
  - 通过 `ConfigBuffFactory` / `ConfigModifierFactory` 驱动运行。

不要新建场景。

## 可视输出

Game View 至少显示：

```text
Runtime Config Patch Slice
Source: Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json
Layer: Patch

ChangeSet:
  BasicModifierConfig id=200001 Replaced source=demo_patch
  BasicBuffConfig id=100001 Replaced source=demo_patch

Config:
  Buff 100001 duration=8 maxLayers=3 modifier=200001
  Modifier 200001 Attack +80

Runtime:
  Attack: 100 -> 180
  Hp: 1000 -> ...
```

必须让使用者能区分：

- Base 值。
- Patch / Mod 覆盖值。
- Merge 变更报告。
- 最终运行时状态。

## 验收标准

- Unity 编译无项目 error。
- 不新建场景，仍使用 `Assets/Scenes/RuntimeVerticalSlice.unity`。
- Demo patch 文件可被加载。
- JSON 格式错误时输出可读错误，并阻止继续应用 patch。
- `format` 缺失或不是 `mx.runtimeConfigPatch.v1` 时，加载器返回明确错误，并阻止继续应用 patch。
- `RuntimeConfigPatchMerger` 合并 Modifier patch 后，Attack 从 `100 -> 180`。
- `RuntimeConfigPatchMerger` 合并 Buff patch 后，Burning duration 变为 `8s`。
- Game View 显示 `ConfigChangeSet`，且至少包含 Buff 和 Modifier 两条 `Replaced` 记录。
- `ConfigChangeSet.SourceId` 显示为 `demo_patch`。
- Buff / Modifier 仍由 `ConfigBuffFactory` / `ConfigModifierFactory` 主路径创建。
- `AttributeChangedEvent` 仍正确发布。
- 运行 10 秒无异常。
- 停止 Play 后无持久化脏数据写入。

## 测试建议

### 自动化优先

建议新增 EditMode 测试：

```text
RuntimeConfigPatchJsonLoader_LoadsModifierPatch
RuntimeConfigPatchJsonLoader_LoadsBuffPatch
RuntimeConfigPatchJsonLoader_InvalidFormatReportsError
RuntimeConfigPatchSlice_MergesPatchAndReportsChangeSet
RuntimeConfigPatchSlice_PatchedModifierAffectsAttack
```

已有 `RuntimeConfigPatchMergerTests` 不应删除；本任务是在其上增加文件加载和场景接入验证。

### 手动验证

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 勾选 `_useConfigDriven`。
3. 勾选 `_usePatchFile`。
4. 点击 Play。
5. 观察 Game View 中的 `ChangeSet` 和最终 Attack / Buff duration。
6. 修改 patch 文件为非法 JSON，确认错误可见且不会静默使用 Base。

## 文档更新

完成实现后必须更新：

- `Docs/USAGE.md`
- `Docs/ROADMAP.md`
- `Docs/Interfaces/Config.md`
- 必要时更新 `Docs/CONFIG_FORMAT_STRATEGY.md`

文档必须明确：

- 这是最小运行时 patch 文件，不是完整 Mod 包格式。
- 真实项目可以把自己的 TSV / JSON / 外部编辑器输出转换成同等 typed patch entries。
- 完整 Mod 包、manifest、签名、权限和资源白名单是后续任务。

## 状态

`Implemented (r1153)`

## 优先级

当前优先级高于：

- Buff Authoring Editor 深化。
- Runtime Preview Scene Target。
- 完整 Mod 包格式。
- AI 辅助闭环。

原因：文件级覆盖层是外部编辑器、Mod 和运行时预览共同依赖的底座。

## 后续衔接

本任务完成后，再进入：

1. Buff Authoring 垂直切片强化：让外部编辑器输出同等 patch 文件。
2. Runtime Preview Scene Target：加载 patch 后应用到测试目标。
3. Mod Package v0：manifest、目录结构、权限和资源白名单。
