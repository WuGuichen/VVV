# 子需求 08：Buff Authoring 导出 Runtime Config Patch

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

把 Authoring Core / CLI 的输出接到已完成的运行时 Patch 底座：用户通过 CLI 把 Buff Authoring 草稿导出为 `mx.runtimeConfigPatch.v1` JSON 文件，并被 `RuntimeVerticalSlice` 的 `_usePatchFile` 路径直接加载运行。

这一步只做纯函数导出层和 CLI 命令，不改外部 Editor UI。UI 按钮单独放到 `AUTHORING_EDITOR_09_EXPORT_RUNTIME_PATCH_BUTTON.md`。

## 背景

前置运行时链路已完成：

- `RUNTIME_VERTICAL_SLICE_01_PLAYABLE_ATTRIBUTES_BUFFS_MODIFIERS.md`
- `RUNTIME_CONFIG_SLICE_01_DATA_DRIVEN_BUFF.md`
- `RUNTIME_CONFIG_PATCH_SLICE_01_FILE_DRIVEN_OVERRIDE.md`

当前已有：

- 外部 Buff Authoring Editor 可编辑 `DamageByAttr` 草稿。
- Authoring Core 可校验旧 `PatchDocument` / `buff.patch.json`。
- Runtime 可加载 `mx.runtimeConfigPatch.v1` JSON。
- `RuntimeVerticalSliceRunner` 可通过 `_useConfigDriven + _usePatchFile` 运行 patch 文件。

当前缺口：

- Authoring Core / CLI 输出格式和 Runtime Patch v1 尚未对齐。
- 用户编辑后的 Buff 草稿不能直接喂给 `RuntimeConfigPatchJsonLoader`。
- 需要先建立脱离 Web UI 和 Unity 的可测试导出命令。

## 非目标

本任务不做：

- 完整 Mod 包格式。
- manifest / 签名 / 权限 / 资源白名单。
- Runtime Preview Scene Target。
- WGame 真实配置导入。
- 所有 Buff 类型完整运行时语义。
- Editor UI 按钮和 Web API。
- AI 自动补全。

如果本任务开始处理完整 Mod 发布或场景角色预览，说明范围失控。

## 目标格式

导出文件必须兼容：

```text
Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json
```

格式固定为：

```json
{
  "format": "mx.runtimeConfigPatch.v1",
  "sourceId": "authoring_export",
  "layer": "Patch",
  "modifiers": [],
  "buffs": []
}
```

字段约束：

- `format` 必须是 `mx.runtimeConfigPatch.v1`。
- `sourceId` 默认 `authoring_export`，允许由包 ID 或导出文件名派生。
- `layer` 在 Mod Mode 下为 `Mod`，Developer Mode 下可为 `Patch`。
- JSON 字段使用 camelCase。
- 导出后必须能被 `RuntimeConfigPatchJsonLoader` 读取。

## Authoring 到 Runtime 映射

首版只要求支持一个框架级最小映射，不要求完整 WGame 语义。

### 转换边界

`AuthoringRuntimePatchExporter` 是纯字符串 / DTO 转换层，只需要理解 Authoring Patch JSON Schema 和 Runtime Patch v1 JSON Schema 的字段映射。

必须遵守：

- 不引用 `MxFramework.Config.Runtime` 程序集。
- 不引用 Unity 程序集。
- 不直接 new `BasicBuffConfig` / `BasicModifierConfig`。
- 输出的 JSON 由 Runtime 侧 `RuntimeConfigPatchJsonLoader` 再解析成 typed patch entries。

这样可以保证 Authoring Core / CLI 独立于 Unity 和 Runtime asmdef，只通过 JSON 契约对接。

### DamageByAttr 草稿

Authoring 侧 `DamageByAttr` 字段映射到 runtime patch：

| Authoring 字段 | Runtime Patch 字段 | 规则 |
| --- | --- | --- |
| `Id` | `buffs[].id` | 必须为正整数 |
| `Name` | `buffs[].nameText` | 多语言 key；没有则生成 `buff.{id}.name` |
| `Desc` | `buffs[].descriptionText` | 多语言 key；没有则生成 `buff.{id}.desc` |
| `Duration` | `buffs[].duration` | Authoring 单位 ms，Runtime 单位秒，导出时除以 1000 |
| `AddNum` | `buffs[].maxLayers` | 为空时默认 1 |
| `Values` | `modifiers[].parameters[0]` | 首版只支持纯数字字符串，例如 `"80"` |
| `Id` | `buffs[].modifierId` | 生成或引用一个 modifier ID |

Modifier 导出规则：

- 稳定派生 ID 固定为 `modifierId = buffId + 100000`。
- 派生结果必须落在 `BasicModifierConfig` 的 ID 范围 `200000-299999` 内。
- 如果派生结果越界，导出失败并给出字段级错误。
- `paramIndex` 固定映射到 Demo 的 Attack 属性 ID 或文档中明确的目标属性 ID。
- `parameters[0]` 是导出的属性增量值。

`DamageByAttr.Values` 是当前最容易失控的字段。首版只支持纯数字值，例如：

```text
Values = "80"
```

暂不支持：

```text
Values = "caster.Attack * 0.35"
```

如果 `Values` 不是纯数字，导出必须失败并给出字段级错误，不允许生成不可运行 patch。公式解析和基于上下文的计算后续单独立项。

## CLI / Core 能力

在 Authoring Core 增加纯函数转换层：

```text
AuthoringRuntimePatchExporter
```

职责：

- 输入：`ProjectAuthoringManifest`、`ModPackageManifest`、旧 `PatchDocument`。
- 输出：Runtime Patch v1 JSON 文本或结构化错误。
- 执行字段映射、单位转换、默认值填充和错误报告。
- 只处理 JSON DTO，不引用 Runtime 配置程序集。

CLI 增加命令：

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  runtime-patch export \
  --package Tools/MxFramework.Authoring/samples/buff-preview \
  --out Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json
```

要求：

- 不依赖 Unity。
- 不写入 WGame 字段名。
- 输出 JSON 稳定排序，方便 SVN diff。
- 导出失败时返回非 0 exit code，并打印字段级错误。
- 不要求启动 Editor Server。
- 不要求打开浏览器。
- 导出失败时不覆盖上一次可用输出文件。

## 运行验证

导出后验证路径：

1. 打开 `Assets/Scenes/RuntimeVerticalSlice.unity`。
2. 勾选 `_useConfigDriven`。
3. 勾选 `_usePatchFile`。
4. 确认 `_patchFilePath` 指向导出的 Runtime Patch v1。
5. 点击 Play。
6. Game View 显示 ChangeSet 和最终属性变化。

## 验收标准

- Authoring Core 能把 sample `buff.patch.json` 导出为 `mx.runtimeConfigPatch.v1`。
- CLI `runtime-patch export` 能把 sample package 导出到指定 `--out`。
- 导出 JSON 能被 `RuntimeConfigPatchJsonLoader` 加载。
- `format`、`sourceId`、`layer`、`buffs`、`modifiers` 字段齐全。
- `Duration` 从毫秒正确转换为秒。
- `AddNum` 正确映射为 `maxLayers`。
- `Values` 成功映射为 modifier `parameters[0]`，无法映射时导出失败。
- 导出后运行 `RuntimeVerticalSlice` patch 模式能看到 ChangeSet。
- Buff / Modifier 仍由 Runtime 的 `ConfigBuffFactory` / `ConfigModifierFactory` 主路径创建。
- 导出失败时不覆盖上一次有效 Runtime Patch 文件。
- 文档说明 Authoring 草稿格式和 Runtime Patch v1 的区别。

## 测试建议

### Authoring Core / CLI

建议新增测试：

```text
RuntimePatchExporter_ExportsFormatV1
RuntimePatchExporter_ConvertsDurationMsToSeconds
RuntimePatchExporter_MapsAddNumToMaxLayers
RuntimePatchExporter_MapsValuesToModifierParameter
RuntimePatchExporter_RejectsFormulaValues
RuntimePatchExporter_RejectsUnsupportedValuesFormula
RuntimePatchExporter_ValidatesDerivedModifierIdRange
RuntimePatchExporter_DoesNotOverwriteOnFailure
```

### Runtime 兼容

建议复用 runtime loader 测试，额外加：

```text
RuntimePatchExporter_OutputLoadsInRuntimeConfigPatchJsonLoader
```

### 手动验证

- 使用 CLI 导出 sample Buff。
- 打开 Unity 场景运行 patch 模式。
- 确认 Game View 的 ChangeSet 与导出内容一致。

## 文档更新

完成实现后必须更新：

- `Docs/AUTHORING_EDITOR_USAGE.md`
- `Docs/USAGE.md`
- `Docs/ROADMAP.md`
- 必要时更新 `Docs/Interfaces/Config.md`

文档必须明确：

- Authoring 草稿格式用于编辑体验。
- Runtime Patch v1 用于运行时加载和 Mod / Preview 底座。
- 当前映射只覆盖框架级 Buff / Modifier 最小切片，不代表 WGame 完整 Buff 语义。
- Editor UI 按钮尚未接入，后续由 `AUTHORING_EDITOR_09_EXPORT_RUNTIME_PATCH_BUTTON.md` 完成。

## 状态

`Implemented (r1158)`

## 优先级

当前优先级高于：

- Editor UI 导出按钮。
- Runtime Preview Scene Target。
- 完整 Mod Package v0。
- AI 辅助闭环。

原因：只有 Authoring 输出和 Runtime Patch v1 对齐后，编辑器、Mod 和运行时预览才共用同一条数据链路。

## 后续衔接

本任务完成后，再进入：

1. `AUTHORING_EDITOR_09_EXPORT_RUNTIME_PATCH_BUTTON.md`：外部 Editor UI 按钮调用 08 的 CLI / API。
2. Runtime Preview Scene Target：加载导出的 Runtime Patch v1 后应用到测试目标。
3. Mod Package v0：把 Runtime Patch v1 放入带 manifest 的包结构。
