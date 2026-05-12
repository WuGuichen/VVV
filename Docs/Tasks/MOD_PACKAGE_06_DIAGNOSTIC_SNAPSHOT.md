# Mod Package 06：Diagnostic Snapshot / 自动检测报告 v0

> **状态**: ✅ 已完成（r1179）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_05_LOADOUT_ENABLE_STATE.md`
> 目标版本：Phase 10.10

## 目标

把当前 Mod Package 运行时链路的状态统一导出为一个可读、可机器处理的诊断快照，让人、外部编辑器、CLI、AI Agent 都能不用查代码就判断当前 Mod 组合是否健康。

目标链路：

```text
Package containers
  -> Catalog
  -> Loadout
  -> LoadPlan
  -> Multi-package merge
  -> Diagnostic Snapshot JSON
  -> Human / Editor / CLI / AI Agent
```

这一步不改变合并行为，只把“发现了什么、启用了什么、跳过了什么、覆盖了什么、最终是否成功”形成统一报告。后续 Mod 管理 UI、自动修复、AI 辅助和玩家报错都应优先读取这个快照。

## 背景

当前已经完成：

- `RuntimeModPackageDiscovery`：发现包并生成 Catalog。
- `RuntimeModPackageLoadoutJson`：读取启用状态。
- `RuntimeModPackageLoadPlanBuilder`：生成确定性 LoadPlan。
- `RuntimeModPackagePatchMerger`：合并多个 Runtime Patch 并输出 merge report。

但这些结果目前分散在多个对象和 Demo 日志里，不利于：

- 自动化测试快速判断风险。
- 外部编辑器展示问题。
- AI Agent 定位缺失包、非法包、覆盖链。
- 玩家或开发者提交诊断信息。

因此需要一个稳定的 `Diagnostic Snapshot v0`。

## 范围

### 必须完成

1. 定义诊断快照格式。

建议格式：

```json
{
  "format": "mx.modDiagnosticSnapshot.v1",
  "generatedUtc": "2026-05-07T00:00:00Z",
  "success": true,
  "summary": {
    "discovered": 2,
    "valid": 2,
    "invalid": 0,
    "enabled": 2,
    "ordered": 2,
    "skipped": 0,
    "overrides": 1,
    "errors": 0,
    "warnings": 0
  },
  "loadout": {
    "profileId": "demo",
    "displayName": "Demo Loadout",
    "enabledPackageKeys": []
  },
  "packages": [],
  "loadPlan": [],
  "overrides": [],
  "errors": [],
  "warnings": []
}
```

字段规则：

| 字段 | 规则 |
| --- | --- |
| `format` | 必须为 `mx.modDiagnosticSnapshot.v1`。 |
| `generatedUtc` | 生成时间，UTC。 |
| `success` | 没有 error 且 merge 成功时为 true。 |
| `summary` | 固定统计字段，便于 UI 和 AI 快速判断。 |
| `loadout` | 当前使用的 loadout 摘要；没有 loadout 时标记 default-all。 |
| `packages` | Catalog item 摘要。 |
| `loadPlan` | ordered/skipped 的确定顺序。 |
| `overrides` | 多包覆盖链。 |
| `errors` | 阻断性问题。 |
| `warnings` | 非阻断性问题。 |

2. 新增 Runtime 侧 Snapshot 类型和生成器。

建议放在 `MxFramework.Config.Runtime`：

```text
RuntimeModDiagnosticSnapshot.cs
RuntimeModDiagnosticSnapshotBuilder.cs
RuntimeModDiagnosticSnapshotJson.cs
```

推荐 API：

```csharp
public static RuntimeModDiagnosticSnapshot Build(
    RuntimeModPackageCatalog catalog,
    RuntimeModPackageLoadout loadout,
    RuntimeModPackageLoadPlan loadPlan,
    RuntimeModPackageMergeResult mergeResult)

public static string SaveToJson(RuntimeModDiagnosticSnapshot snapshot)
```

要求：

- 不引用 `UnityEditor`。
- 可以引用 `Newtonsoft.Json`。
- 输出 JSON 字段使用 camelCase。
- 列表顺序必须稳定，不能依赖 Dictionary 枚举顺序。

3. 包摘要内容。

每个 package 至少包含：

- `packageKey`
- `packageId`
- `displayName`
- `version`
- `kind`
- `containerPath` 或可选脱敏路径
- `packageRelativePath`
- `isValid`
- `isEnabled`
- `errors`
- `warnings`

注意：

- Snapshot 可包含绝对路径用于本机诊断。
- 但如果后续要给玩家导出，需要支持脱敏；v0 只需预留 `includeAbsolutePaths` 参数。

4. LoadPlan 摘要内容。

每个 load plan item 至少包含：

- `packageKey`
- `packageId`
- `state`: `Ordered` / `Skipped`
- `skipReason`
- `orderIndex`

排序：

- ordered items 按加载顺序输出。
- skipped items 按 PackageKey 升序输出，保证稳定。

5. Override 摘要内容。

每个 override 至少包含：

- `configType`
- `id`
- `packageChain`
- `winnerPackageKey`
- `winnerPackageId`

如果当前 merge report 只有 packageId，应补 packageKey 映射；如果无法补齐 packageKey，必须在 warning 中说明。

6. 错误分级。

Snapshot 需要明确：

```text
Error   -> success=false
Warning -> success 可以仍为 true
Info    -> 不影响 success
```

v0 至少实现 `errors` 和 `warnings` 两级。

典型 error：

- loadout JSON 格式错误。
- ordered package merge 阶段加载失败。
- Runtime Patch format 错误。

典型 warning：

- loadout 引用不存在 package key。
- 重复 packageId。
- package key 使用了不可移植 fallback。
- package 被禁用。

7. RuntimeVerticalSlice 集成。

复用 `RuntimeVerticalSlice.unity`，不新增场景。

行为：

- 在 loadout + merge 模式下生成 snapshot。
- OnGUI 显示 summary。
- 日志输出 snapshot 的关键摘要。
- 可选：把 JSON 写到临时路径或 `Application.persistentDataPath/MxFramework/Diagnostics/mod_diagnostic_snapshot.json`。

8. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md`

说明：

- Snapshot 是调试和 AI 辅助的统一入口。
- Snapshot 不改变 Runtime 行为，只报告状态。
- 如何从 summary 快速判断问题。

### 不做

- 不做玩家导出 UI。
- 不做自动修复。
- 不做冲突解决 UI。
- 不做上传日志。
- 不做压缩包收集。
- 不做真实 WGame 数据接入。
- 不新增 Unity 场景。

## 建议实现

### 1. Builder 不重新执行逻辑

SnapshotBuilder 不应重新扫描、重新排序或重新合并。它只消费已经产生的对象：

```text
catalog + loadout + loadPlan + mergeResult -> snapshot
```

这样可以避免“报告里的结果”和“实际运行结果”不一致。

### 2. 稳定输出

所有数组都要有明确排序：

- packages: packageKey 升序。
- ordered loadPlan: orderIndex 升序。
- skipped loadPlan: packageKey 升序。
- overrides: configType + id 升序。
- errors/warnings: source + message 升序。

### 3. AI 友好字段

字段名要直接，不要依赖 UI 文本：

```json
{
  "severity": "Warning",
  "code": "LoadoutMissingPackageKey",
  "source": "mod_loadout.json",
  "message": "Loadout references missing package key: sample.missing|missing"
}
```

后续 AI Agent 可以按 `code` 做自动建议。

## 验收标准

1. 能从 catalog + null loadout + loadPlan + mergeResult 生成 snapshot。
2. 能从 catalog + loadout + loadPlan + mergeResult 生成 snapshot。
3. Snapshot JSON `format` 为 `mx.modDiagnosticSnapshot.v1`。
4. summary 统计与输入对象一致。
5. packages 列表包含 valid / invalid / enabled / disabled 状态。
6. loadPlan 列表包含 ordered / skipped 状态和稳定顺序。
7. overrides 列表包含 override chain 和 winner。
8. merge error 会导致 `success=false`。
9. warning 不会导致 `success=false`。
10. JSON 输出字段为 camelCase。
11. 多次生成同一输入，除 `generatedUtc` 外内容稳定。
12. `RuntimeVerticalSlice.unity` 能显示 snapshot summary。
13. Unity EditMode 测试覆盖：成功快照、warning、error、稳定排序、JSON roundtrip。
14. 实现不引用 Authoring Core / CLI，不引用 `UnityEditor`。

## 推荐测试

- `RuntimeModDiagnosticSnapshotBuilder_SuccessInput_ProducesSuccessSnapshot`
- `RuntimeModDiagnosticSnapshotBuilder_LoadoutMissingKey_ProducesWarning`
- `RuntimeModDiagnosticSnapshotBuilder_MergeError_ProducesErrorAndFailure`
- `RuntimeModDiagnosticSnapshotBuilder_OrdersPackagesByPackageKey`
- `RuntimeModDiagnosticSnapshotJson_Save_UsesCamelCase`
- `RuntimeModDiagnosticSnapshotJson_SaveThenLoad_RoundTrips`

测试数据优先使用临时目录，不依赖用户机器绝对路径。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- Snapshot API 有 EditMode 测试。
- `RuntimeVerticalSlice.unity` 不新增场景即可显示 snapshot summary。
- `Docs/CAPABILITIES.md` 更新运行时能力。
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md` 说明 snapshot 的使用边界。
- SVN 提交信息建议：

```text
Add runtime mod diagnostic snapshot
```
