# Mod Package 05：Loadout / 启用状态持久化 v0

> **状态**: ✅ 已完成（r1177）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_04_MULTI_PACKAGE_PATCH_MERGE.md`
> 目标版本：Phase 10.9

## 目标

在多包 Runtime Patch 已能按 LoadPlan 合并的基础上，补齐“哪些包启用、哪些包禁用、这个选择如何保存和复现”的最小能力。

目标链路：

```text
Discover packages
  -> Build catalog
  -> Read loadout
  -> Resolve enabled package keys
  -> Build load plan
  -> Merge runtime patches
  -> Save / report active loadout
```

这一步完成后，框架不再只能“发现到的包全部参与合并”，而是可以根据一个可读 JSON loadout 文件稳定复现当前启用组合。它是后续 Mod 管理 UI、开发者 Profile、AI Agent 调试和玩家 Mod 配置的基础。

## 背景

当前已经完成：

- 单包 Runtime Patch 加载。
- 多包发现和确定性排序。
- 多包 Runtime Patch 合并和 override report。

但当前多包流程缺少持久化启用状态：

- Demo / Runtime 很难复现“只启用某几个包”的状态。
- `BuildLoadPlan(enabledPackageIds)` 只适合临时代码调用，不适合存档或工具协作。
- `packageId` 可能重复，仅靠 `packageId` 表示启用项不够稳定。
- AI Agent 或外部编辑器无法直接读取“当前启用组合”。

因此本任务先定义 v0 loadout 文件和解析规则，不做复杂 UI。

## 范围

### 必须完成

1. 定义 Loadout 文件格式。

建议文件名：

```text
mod_loadout.json
```

建议格式：

```json
{
  "format": "mx.modLoadout.v1",
  "profileId": "demo",
  "displayName": "Demo Loadout",
  "enabledPackageKeys": [
    "sample.buff.preview|runtime-patch-mod",
    "sample.buff.override|runtime-patch-mod-override"
  ],
  "updatedUtc": "2026-05-07T00:00:00Z"
}
```

字段规则：

| 字段 | 规则 |
| --- | --- |
| `format` | 必须为 `mx.modLoadout.v1`。 |
| `profileId` | 必填，稳定 profile id。 |
| `displayName` | 可选，用于 UI 展示。 |
| `enabledPackageKeys` | 必填数组，按 package key 启用包。 |
| `updatedUtc` | 可选，保存时写入 UTC 时间。 |

2. 定义 package key。

不要只用 `packageId` 做启用状态键。v0 必须使用稳定 package key：

```text
packageKey = packageId + "|" + normalizedContainerRelativePackagePath
```

例：

```text
sample.buff.preview|runtime-patch-mod
sample.buff.override|runtime-patch-mod-override
```

要求：

- `packageId` 来自 `mod.json`。
- relative path 是相对发现它的 container root 的路径。
- 路径统一使用 `/`。
- 不包含绝对路径，避免不同机器不可复现。
- 如果同一个 package root 无法计算 container-relative path，应进入 warning，并用 normalized root path fallback，但报告必须标记不可移植。

3. 扩展 catalog item。

`RuntimeModPackageCatalogItem` 需要能提供：

- `PackageKey`
- `ContainerPath`
- `PackageRelativePath`

如果已有类型无法承载，应以兼容方式扩展构造函数或增加重载，不破坏现有测试。

4. 新增 Runtime 侧 Loadout 类型和读写。

建议放在 `MxFramework.Config.Runtime`：

```text
RuntimeModPackageLoadout.cs
RuntimeModPackageLoadoutJson.cs
```

推荐 API：

```csharp
public static RuntimeModPackageLoadout LoadFromJson(string json)
public static string SaveToJson(RuntimeModPackageLoadout loadout)
public static RuntimeModPackageLoadout LoadFromFile(string path)
public static void SaveToFile(string path, RuntimeModPackageLoadout loadout)
```

要求：

- 不引用 `UnityEditor`。
- 可以引用 `Newtonsoft.Json`。
- format 不匹配必须明确报错。
- 文件不存在时不应伪装成空 loadout；由调用方决定使用默认策略。

5. LoadPlan 支持 loadout。

新增或扩展 API：

```csharp
public static RuntimeModPackageLoadPlan Build(
    RuntimeModPackageCatalog catalog,
    RuntimeModPackageLoadout loadout)
```

规则：

- `loadout == null`：保持现有默认策略，启用所有 valid 包。
- `enabledPackageKeys` 为空数组：不启用任何包。
- key 命中的 valid 包进入 ordered。
- key 未命中的条目进入 warning：`loadout references missing package key`。
- invalid 包即使命中 key，也进入 skipped，并保留原错误。
- disabled 包进入 skipped，但不是 error。

旧的 `enabledPackageIds` API 如已存在，应保留，避免破坏调用方；但文档推荐新 loadout API。

6. RuntimeVerticalSlice 集成。

复用 `RuntimeVerticalSlice.unity`，不新增场景。

建议字段：

```csharp
[SerializeField] private bool _useModPackageLoadout;
[SerializeField] private string _loadoutFilePath = "MxFramework/Demo/mod_loadout.json";
```

行为：

- 从 `Application.streamingAssetsPath` 或明确配置路径读取 demo loadout。
- 根据 loadout 构建 load plan。
- 再走 04 的多包 merge。
- OnGUI / 日志显示：
  - profileId
  - enabled count
  - missing key warnings
  - ordered package keys
  - skipped package keys

7. 增加 Demo loadout。

建议路径：

```text
Assets/StreamingAssets/MxFramework/Demo/mod_loadout.json
```

内容启用当前 demo 的两个包：

```text
runtime-patch-mod
runtime-patch-mod-override
```

注意：`.meta` 由 Unity 生成。

8. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md`

说明：

- catalog 发现“有哪些包”。
- loadout 决定“启用哪些包”。
- load plan 决定“按什么顺序加载”。
- merge 决定“最终配置结果是什么”。

### 不做

- 不做玩家 Mod 管理 UI。
- 不做外部编辑器 loadout 页面。
- 不做依赖关系。
- 不做 load priority 字段。
- 不做云同步、Steam Workshop、平台发布。
- 不做签名和权限授权。
- 不引入真实 WGame 数据。
- 不新增 Unity 场景。

## 建议实现

### 1. PackageKey 生成

Discovery 阶段最适合生成 `PackageKey`，因为它知道 container root 和 package root。

伪代码：

```text
relative = GetRelativePath(containerRoot, packageRoot)
relative = NormalizeSlash(relative)
packageKey = manifest.packageId + "|" + relative
```

不要在 LoadPlan 阶段临时猜 path。

### 2. 默认策略

保持兼容：

```text
loadout == null -> all valid packages enabled
loadout.enabledPackageKeys == [] -> no packages enabled
```

这样旧 Demo 不传 loadout 时行为不变，新 Demo 可以明确保存启用状态。

### 3. 错误语义

- loadout JSON 格式错误：读取失败，调用方显示 error。
- loadout 引用不存在 package key：warning，不阻断其他包。
- loadout 引用 invalid package：skipped + 原错误，不额外伪装成 missing。

### 4. 可移植性

Loadout 不应保存绝对路径。绝对路径只能出现在诊断报告中，不能进入 `mod_loadout.json` 的启用列表。

## 验收标准

1. `RuntimeModPackageLoadoutJson.LoadFromJson` 能读取合法 `mx.modLoadout.v1`。
2. format 不匹配时明确报错。
3. 保存后再读取，`profileId` 和 `enabledPackageKeys` 保持一致。
4. Catalog item 能提供稳定 `PackageKey`。
5. 重复 `packageId` 的包能通过不同 `PackageKey` 区分。
6. `Build(catalog, null loadout)` 保持启用所有 valid 包。
7. `Build(catalog, loadout)` 只启用 `enabledPackageKeys` 命中的 valid 包。
8. 空 `enabledPackageKeys` 会生成空 ordered plan。
9. loadout 引用不存在 key 时产生 warning，不阻断其他包。
10. invalid 包即使命中 loadout，也不会进入 ordered。
11. `RuntimeVerticalSlice.unity` 可以显示 loadout 摘要并执行多包 merge。
12. Unity EditMode 测试覆盖：读取、保存、format 错误、重复 packageId、missing key、空启用列表。
13. 实现不引用 Authoring Core / CLI，不引用 `UnityEditor`。

## 推荐测试

- `RuntimeModPackageLoadoutJson_ValidJson_Loads`
- `RuntimeModPackageLoadoutJson_InvalidFormat_Fails`
- `RuntimeModPackageLoadoutJson_SaveThenLoad_RoundTrips`
- `RuntimeModPackageDiscovery_PackageKey_UsesContainerRelativePath`
- `RuntimeModPackageLoadPlan_LoadoutNull_EnablesAllValid`
- `RuntimeModPackageLoadPlan_LoadoutEmpty_EnablesNone`
- `RuntimeModPackageLoadPlan_LoadoutFiltersByPackageKey`
- `RuntimeModPackageLoadPlan_LoadoutMissingKey_Warns`
- `RuntimeModPackageLoadPlan_DuplicatePackageId_DifferentKeys`

测试数据优先使用临时目录，不依赖用户机器绝对路径。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- Loadout API 有 EditMode 测试。
- `RuntimeVerticalSlice.unity` 不新增场景即可查看 loadout + merge 摘要。
- `Docs/CAPABILITIES.md` 更新运行时能力。
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md` 说明 catalog / loadout / load plan / merge 的边界。
- SVN 提交信息建议：

```text
Add runtime mod package loadout state
```
