# Story.Config 接口

> 状态：S2 Config Schema / Mapper / Validator 已实现（2026-05-24）。本文记录 `MxFramework.Story.Config` 当前可用的 noEngine 配置桥接 API。

## 职责

`MxFramework.Story.Config` 是 Story core 的 sibling bridge。它把项目层导入器产出的 graph / beat / step / branch / choice / fact 配置行映射为 `MxFramework.Story` DTO，并在映射前执行跨行校验。

本模块不解析 Excel、CSV、JSON、Luban、Yarn、Ink、Articy 或 Unity 序列化资产；这些导入器应在项目层或后续 authoring 工具层实现，最后产出本页定义的配置行。

外部工具层现在可以先产出 `.story.json` draft 作为 handoff 文件。`Tools/MxFrameworkStoryAuthoring/story_authoring.py import-markdown` 把 Markdown Story Outline v1 转成 `schema=mx.story.config.draft.v1` 的 JSON，字段名对齐下方配置行；`validate` 在工具层镜像本页的跨行约束并输出结构化 diagnostics。该 draft 不是 Runtime SaveState、Unity asset 或 Story core 输入，项目层仍需把 rows 映射进 `StoryConfigSet` / `StoryGraphConfigMapper`。

## 依赖边界

```text
MxFramework.Story.Config
  -> MxFramework.Story
  -> MxFramework.Config
```

禁止依赖 Runtime、Gameplay、UnityEngine、UnityEditor、Resources、Buffs、Attributes、Modifiers、Runtime AI Planner、Editor 或 WGame 私有内容。

## 配置行

| 类型 | `IConfigData.Id` 含义 | 关键字段 |
| --- | --- | --- |
| `StoryGraphConfig` | `GraphId` | `Version`, `EntryBeatId`, `SourcePath` |
| `StoryBeatConfig` | `BeatId` | `GraphId`, `SortOrder`, `ChoiceSetId`, `TriggerIds` |
| `StoryStepConfig` | `StepId` | `GraphId`, `BeatId`, `SortOrder`, `Kind`, `TextKey`, `WaitPolicy`, `FactNamespace`, `FactId`, `FactValueKind`, `FactValueRaw` |
| `StoryBranchConfig` | `BranchId` | `GraphId`, `BeatId`, `TargetBeatId`, `ConditionFactId`, `Priority`, `IsFallback` |
| `StoryChoiceConfig` | `ChoiceId` | `GraphId`, `BeatId`, `SortOrder`, `LabelTextKey`, `TargetBeatId`, `ConditionFactId`, `EffectIds` |
| `StoryFactConfig` | `FactId` | `Namespace`, `ValueKind` |

`StoryConfigSchemas.CreateAll()` 返回上述 schema。每个行类型也提供 `CreateSchema()`，便于注册到 `ConfigTable<T>`、Config Workbench 或项目层导入器。

## Mapper

`StoryConfigSet` 聚合六类配置行，可直接由数组构造，也可通过 `StoryConfigSet.FromProvider(IConfigProvider)` 从当前配置 provider 抽取。

```csharp
StoryConfigSet set = StoryConfigSet.FromProvider(configProvider);
StoryConfigReferenceIndex references = new StoryConfigReferenceIndex()
    .AddTextKey(9001)
    .AddTextKey(9002);

StoryGraphConfigMappingResult result =
    StoryGraphConfigMapper.Map(set, graphId: 1001, references);

StoryGraphDefinition graph = result.Definition;
```

`Map(...)` 会先调用 `StoryConfigValidator.Validate(...)`。如果存在诊断，`Definition == null`，调用方应展示或记录 `Diagnostics`，不要加载到 `StoryDirector`。

映射排序是确定性的：

- beats: `SortOrder`, `BeatId`
- steps: `SortOrder`, `StepId`
- choices: `SortOrder`, `ChoiceId`
- branches: `Priority`, `BranchId`
- `TriggerIds` / `EffectIds`: 升序

这些顺序会进入 `StoryGraphDefinition`，用于稳定 graph traversal、hash 和 SaveState 兼容。

## Validator

`StoryConfigValidator` 覆盖 generic `ConfigTableValidator` 无法表达的跨行 Story 合约：

| 诊断 | 触发条件 |
| --- | --- |
| `MissingEntryBeat` | `StoryGraphConfig.EntryBeatId` 没有对应 beat |
| `InvalidBranchTarget` / `InvalidChoiceTarget` | branch / choice 指向缺失 beat；`0` 表示 graph 完成 |
| `DuplicateStableId` | graph、beat、step、branch、choice 或 `StoryFactKey` 稳定 id 重复 |
| `UnsupportedStepKind` / `UnsupportedWaitPolicy` | enum 值不在 Story core 当前支持范围 |
| `InvalidTextReference` | line / choice text key 非正数，或不在 `StoryConfigReferenceIndex` 中 |
| `InvalidFactReference` / `InvalidFactValue` | set-fact / condition fact 缺失、类型不匹配，或 condition fact 不是 Bool |
| `InvalidTriggerId` / `InvalidEffectId` | trigger / choice effect id 非正数 |
| `InvalidBeatFlow` | 同一 beat 同时声明 choices 和 branches，或声明多个 fallback branches |

`ConditionFactId` 只能表达 Story core 当前 DTO 的 id-only condition。`0` 表示无条件；负数非法。正数运行时解析顺序与 `StoryDirector` 一致：优先 `(GraphId, ConditionFactId)`，再查 `(0, ConditionFactId)`，且被引用 fact 必须声明为 `Bool`。

`SetFact` 的 `FactValueRaw` 必须符合声明的 `StoryValueKind`：

- `Bool`: 只能为 `0` 或 `1`。
- `Int32`: 必须落在 32-bit signed integer 范围内。
- `Int64` / `Fix64`: 使用完整 `long` raw 值。
- `StringRef`: 必须是正数 32-bit text key；如果提供 `StoryConfigReferenceIndex`，还必须存在于 text key 索引中。

Beat transition 合约与 `StoryDirector` 当前推进顺序保持一致：choice beat 会先进入 choice wait，因此同一 beat 不允许再声明 branch rows；branch beat 最多只能有一个 `IsFallback=true` 的 fallback branch。

## 已知限制

- 不加载或校验真实本地化文本内容；只校验稳定 text key。
- 不执行 Story effect；`StoryChoiceConfig.EffectIds` 当前只做正数和确定性排序。
- 不创建 RuntimeCommand、Runtime hash、SaveState、Gameplay command、Resources preload、Unity trigger、UI Toolkit view 或 authoring import。
- `StoryFactConfig` 只声明可引用 fact 和 value kind；初始黑板值仍由 runtime / save / graph step 明确写入。
- 外部 `.story.json` draft 当前只覆盖 Markdown Story Outline v1；Yarn / Ink / Articy 和 Authoring AI Assist 仍是后续工具层能力，不进入本模块依赖。

## 测试入口

`Assets/Scripts/MxFramework/Tests/Story.Config/`
