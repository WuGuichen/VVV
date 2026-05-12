# Config Demo

WGameFramework 内置 Demo 只用于验证框架能力，不代表任何真实游戏数据，也不作为项目数据迁移来源。

## 目标

- 验证 `IConfigEditorSource` 接入流程。
- 展示 `Table` 到 `Graph` 的跨源引用。
- 展示 enum、flags、bool、asset path 的只读控件映射。
- 给 AI agent 提供稳定、纯净的配置编辑器示例。

## 内置源

| Source | Type | Purpose |
| --- | --- | --- |
| `内置 Demo / BasicBuffConfig` | Demo Memory | 运行时配置、多语言、运行时类型引用 |
| `内置 Demo / BasicModifierConfig` | Demo Memory | 被 Buff 引用的 Modifier 示例 |
| `内置 Demo / ActionCatalog` | Demo TSV | enum、flags、asset path、Graph 引用 |
| `内置 Demo / ActionGraph` | Demo JSON Index | Graph 索引和跨源引用目标 |

## ActionCatalog

```tsv
Id	DisplayName	Category	Tags	GraphId	IconPath	Enabled
1001	Fire Burst	1	3	9001	Assets/Demo/Icons/fire_burst.png	true
1002	Guard Break	2	12	9002	Assets/Demo/Icons/guard_break.png	false
```

字段说明：

- UI 优先显示中文别名，英文列名仍然是真实字段 key。
- `DisplayName` 显示为 `显示名称`。
- `Category` 使用 `demo.ActionCategory`。
- `Tags` 使用 flags 枚举 `demo.ActionTags`。
- `GraphId` 引用 `Graph:DemoActionGraph.Id`。
- `IconPath` 只演示资源路径控件外观，不要求文件真实存在。

## ActionGraph

```tsv
Id	Name	EntryNode	NodeCount
9001	FireBurstGraph	Cast	4
9002	GuardBreakGraph	Approach	5
```

## Optional Broken Reference

`ConfigDemoSources.CreateBrokenReferenceDemoSource()` 不会默认注册。项目或测试可以临时注册它，用于观察健康报告如何发现预览行中的缺失引用。

```csharp
MxEditorUtils.RegisterConfigEditorSource(ConfigDemoSources.CreateBrokenReferenceDemoSource());
```

预期结果：`GraphId=9999` 会被报告为 `NotFound`。

## Boundary

- Demo 数据可以留在框架内。
- 真实项目数据不得提交到 WGameFramework 主干。
- 具体游戏只应在项目层实现 Adapter，并把解析后的 schema、keys、预览和 issue 交给框架。
