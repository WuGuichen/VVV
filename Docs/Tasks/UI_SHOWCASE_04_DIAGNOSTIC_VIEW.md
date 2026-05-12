# UI Showcase 04：Diagnostic View

> **状态**: 已完成（2026-05-09）
> **优先级**：P1
> 所属 Goal：`PHASE12_UI_TOOLKIT_SHOWCASE_GOAL.md`
> 目标版本：Phase 12 M4
> 前置任务：Phase 12 M3 `Config / Patch / Rebuild Panel`

## 目标

为 Runtime Showcase 增加 Diagnostic View，让制作人和开发者在 Play 模式下不用打开测试代码或 Console，也能判断当前 Gameplay 运行时状态是否正确。

M4 在 M3 的 Config / Patch / Rebuild Panel 之后实现，Diagnostic View 作为 Runtime HUD 内的独立区域追加，不替代 M3 面板。

最终 UI 应提供：

- Snapshot 面板的 `summary` / `technical` 双视图。
- Ability Events、AttributeChanged Events、Config Source、错误信息分区显示。
- `GameplayDiagnosticSnapshot` 与 `RuntimeConfigChangeSummary` 的字段映射。
- 清晰标注现有 Runtime DTO 缺口；本任务不改 Runtime DTO。

## 已有运行时能力

### GameplayDiagnosticSnapshot

已由 `GameplayDiagnosticSnapshotBuilder.Build(...)` 提供，来源任务为 `RUNTIME_GAMEPLAY_05_DIAGNOSTIC_SNAPSHOT.md`。

现有快照字段：

| UI 数据 | Runtime 字段 |
| --- | --- |
| Snapshot source | `GameplayDiagnosticSnapshot.SourceName` |
| Ability source | `GameplayDiagnosticSnapshot.AbilitySource` / `LastCast.AbilitySource` |
| Entity 列表 | `GameplayDiagnosticSnapshot.Entities` |
| Entity id / team / alive | `GameplayEntitySnapshot.EntityId` / `TeamId` / `IsAlive` |
| Attribute 值 | `GameplayEntitySnapshot.Attributes[*].AttributeId` / `FinalValue` |
| Buff 状态 | `GameplayEntitySnapshot.Buffs[*].BuffId` / `Duration` / `RemainingTime` / `CurrentLayers` / `MaxLayers` / `IsPermanent` / `IsExpired` |
| Modifier 状态 | `GameplayEntitySnapshot.Modifiers[*].ModifierId` / `ParamIndex` |
| Last cast 结果 | `LastCastSuccess` / `LastFailureReason` / `LastTargetEntityIds` |
| Ability Events | `AbilityEvents[*].EventType` / `AbilityId` / `CasterEntityId` / `TargetEntityId` / `FailureReason` |
| AttributeChanged Events | `AttributeEvents[*].AttributeId` / `BaseValue` / `OldValue` / `NewValue` / `Delta` / `SourceName` |

### RuntimeConfigChangeSummary

已由 `RuntimeAbilityConfigResolver.ChangeSummary` 提供，来源任务为 `RUNTIME_GAMEPLAY_06_CONFIG_CHANGE_APPLY.md`。

现有摘要字段：

| UI 数据 | Runtime 字段 |
| --- | --- |
| 当前配置源 | `SourceName` |
| 上一个配置源 | `PreviousSourceName` |
| 应用策略 | `ApplyPolicy`，当前为 `RebuildOnResolve` |
| 变更 Ability | `ChangedAbilityIds` / `ChangedAbilityCount` |
| 变更 Buff | `ChangedBuffIds` / `ChangedBuffCount` |
| 变更 Modifier | `ChangedModifierIds` / `ChangedModifierCount` |
| 已重建 Ability | `RebuiltAbilityIds` / `RebuiltAbilityCount` |
| 重建失败 Ability | `FailedAbilityIds` / `FailedAbilityCount` |
| 错误摘要 | `Errors` / `ToSummaryText()` |

## UI 分区设计

Diagnostic View 应作为 Runtime HUD 内的独立诊断区域，不替代 M3 的 Config / Patch / Rebuild Panel。

建议分区：

1. `Snapshot Header`
   - 显示 source、ability source、entity count、ability event count、attribute event count。
   - 显示 last cast 成功/失败状态和最后目标列表。
   - 当 `LastFailureReason` 非空时给出错误状态，但不刷 Console。

2. `Snapshot Mode Switch`
   - 两个视图：`Summary`、`Technical`。
   - 默认使用 `Summary`，面向手测判断。
   - `Technical` 保留 Runtime DTO 的完整字段，可用于开发和 AI 截图/上下文读取。

3. `Summary View`
   - Entity 概览：每个 Entity 一行，显示 `EntityId`、`TeamId`、`Alive`、核心 Attribute、Buff 数、Modifier 数。
   - Ability 概览：显示 `AbilitySource`、last cast 状态、targets、Ability Events 最新 N 条。
   - Config 概览：显示 `SourceName`、`PreviousSourceName`、`ApplyPolicy`、changed/rebuilt/failed 数量。
   - 错误概览：聚合 `LastFailureReason` 和 `RuntimeConfigChangeSummary.Errors`。

4. `Technical View`
   - Entity 明细表：展开 Attributes / Buffs / Modifiers。
   - Ability Events 明细表：保留顺序，显示 event type、ability id、caster、target、failure reason。
   - AttributeChanged Events 明细表：保留顺序，显示 attribute id、base、old、new、delta、source。
   - Config Source 明细：展示 changed/rebuilt/failed id 列表和 policy。
   - Errors 明细：逐条显示 config summary errors 和 last cast failure。

5. `Event Sections`
   - Ability Events 与 AttributeChanged Events 必须分区显示，不能混成单一日志。
   - 每区应有 count、empty state 和最近事件优先或原始顺序策略说明。
   - 如果做截断，UI 必须显示 `showing N / total M`。

6. `Errors`
   - Last cast failure 与 config apply errors 分开展示。
   - 无错误时显示稳定的空状态，例如 `No runtime errors`。
   - 错误文本直接来自 DTO，不在 UI 层重新解释 Runtime 语义。

## 字段映射建议

### Summary View

| 区域 | 显示字段 | 规则 |
| --- | --- | --- |
| Header | `SourceName`、`AbilitySource` | 空字符串显示为 `(unknown)` |
| Header | entity / ability event / attribute event count | 从集合 `Count` 计算 |
| Last Cast | `LastCastSuccess`、`LastFailureReason`、`LastTargetEntityIds` | 失败优先显示 reason；无 target 显示 `none` |
| Entity Row | `EntityId`、`TeamId`、`IsAlive` | Alive 用状态徽章或短文本 |
| Entity Row | Attributes | 优先展示 Demo 已知核心属性；未知属性用 `id:value` |
| Entity Row | Buffs / Modifiers | Summary 只显示数量和关键 id，明细放 Technical |
| Config | `SourceName`、`PreviousSourceName`、`ApplyPolicy` | 来自 `RuntimeConfigChangeSummary` |
| Config | changed / rebuilt / failed counts | 使用 count 字段，避免 UI 重复计算 |
| Error Summary | `LastFailureReason`、`Errors` 最新一条 | 多条错误进入 Technical |

### Technical View

| 区域 | 显示字段 | 规则 |
| --- | --- | --- |
| Entity Attributes | `AttributeId`、`FinalValue` | 全量显示 |
| Entity Buffs | `BuffId`、`Duration`、`RemainingTime`、`CurrentLayers`、`MaxLayers`、`IsPermanent`、`IsExpired` | 全量显示 |
| Entity Modifiers | `ModifierId`、`ParamIndex` | 全量显示 |
| Ability Events | `EventType`、`AbilityId`、`CasterEntityId`、`TargetEntityId`、`FailureReason` | 保留 Runtime 输入顺序 |
| AttributeChanged Events | `AttributeId`、`BaseValue`、`OldValue`、`NewValue`、`Delta`、`SourceName` | 保留 Runtime 输入顺序 |
| Config Source | `ChangedAbilityIds`、`ChangedBuffIds`、`ChangedModifierIds`、`RebuiltAbilityIds`、`FailedAbilityIds` | id 列表可换行 |
| Errors | `RuntimeConfigChangeSummary.Errors`、`LastFailureReason` | 分来源显示 |

## 缺口与风险

当前 Runtime Snapshot DTO 足够支持 M4 首版，但存在这些 UI 诊断缺口。后续实现时只在文档或后续任务中记录，除非另开 Runtime 任务，不在 M4 UI 实现中直接补 Runtime 代码。

- `GameplayAbilityEventSnapshot` 没有 timestamp / frame / sequence id；UI 只能按集合顺序展示，不能按真实时间排序。
- `GameplayAttributeEventSnapshot` 没有 entity id；UI 无法直接标明属性变化发生在 Player 还是 Enemy，只能通过 source 或事件上下文辅助判断。
- `GameplayAttributeSnapshot` 只有 numeric id，没有 display name；UI 需要本地映射 Demo 核心属性名，未知 id 保持 `id:value`。
- `GameplayBuffSnapshot` 和 `GameplayModifierSnapshot` 没有 config source / display name；UI 只能展示 id 和运行时状态。
- `RuntimeConfigChangeSummary.Errors` 是字符串列表，没有 severity、code 或 source field；UI 只能做 error 列表，不能做结构化过滤。
- Snapshot 与 Config Summary 当前是两套对象；UI 适配层需要定义清楚刷新时机，避免一个是旧快照、一个是新 summary。

## 非目标

- 不实现主 UI。
- 不修改 `GameplayDiagnosticSnapshot`、`RuntimeConfigChangeSummary` 或其他 Runtime DTO。
- 不做 JSON 序列化、Runtime Preview 协议、Editor Window 或 Mod Diagnostic 合并。
- 不引入 WGame 业务配置、真实关卡或业务类型。

## 文件边界

本轮实现已在 M3 完成记录基础上读取并追加 HUD 文件状态，触碰范围：

- `Assets/Scripts/MxFramework/UI.Toolkit/` 下 HUD 控制器、ViewModel 或诊断子组件。
- `Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceDiagnosticViewModelBuilder.cs`
- `Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceShowcaseUi.cs` 作为 Demo 到 HUD 的适配层。
- `Assets/Scripts/MxFramework/Demo/Ability/RuntimeAbilitySliceRunner.cs` 仅暴露 Demo 侧 latest `RuntimeConfigChangeSummary`。
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uxml`
- `Assets/UI/MxFramework/Showcase/GameplayShowcase.uss`
- `Assets/Scripts/MxFramework/Tests/Ability/RuntimeAbilitySliceDiagnosticViewModelBuilderTests.cs`
- `Assets/Scripts/MxFramework/Tests/MxFramework.Tests.asmdef`

## 实现顺序建议

1. 等 M3 合入或确认完成，读取最新 HUD UXML / USS / Controller / ViewModel / Demo adapter。
2. 在 ViewModel 增加 Diagnostic View 所需的纯显示数据，避免 UI 层直接遍历 Runtime DTO。
3. 在 Demo adapter 中把 `RuntimeAbilitySliceRunner.LastSnapshot` 和 latest `RuntimeConfigChangeSummary` 映射为 ViewModel。
4. 在 UXML 增加 Diagnostic 区域和 Summary / Technical 切换控件。
5. 在 USS 中补诊断表格、错误状态、空状态样式。
6. 补 PlayMode 或 EditMode 可测的 ViewModel 映射测试；必要时补轻量 UXML 元素存在性测试。
7. Play 模式手测：Config / Patch / Rebuild 后刷新诊断，确认 Snapshot 与 Config Summary 同步。

## 验收标准

1. Diagnostic View 在 M3 面板之后实现，不与 M3 并行抢改同一批 UI 文件。
2. Snapshot 面板提供 `Summary` 和 `Technical` 两种视图。
3. Summary View 不依赖测试代码即可判断 entity、ability、config、last cast 是否正常。
4. Technical View 能看到 Entity / Attribute / Buff / Modifier / Ability Events / AttributeChanged Events 的 DTO 明细。
5. Ability Events 与 AttributeChanged Events 分区显示，并能看出事件数量和顺序。
6. Config Source 区域显示 source、previous source、policy、changed/rebuilt/failed counts 和 id 明细。
7. 错误信息分区显示 last cast failure 与 config errors；无错误时有明确空状态。
8. Runtime 纯 C# 模块不新增 UnityEngine / UI Toolkit 依赖。
9. 不新增 WGame 业务类型，不修改真实业务配置。
10. Unity Console 无编译 Error。

## 测试要求

后续实现至少覆盖：

- ViewModel 映射测试：空 snapshot / 空 summary 不抛异常，并显示稳定 empty state。
- ViewModel 映射测试：Ability Events 和 AttributeChanged Events 分别计数、分别输出。
- ViewModel 映射测试：`RuntimeConfigChangeSummary` 的 source、policy、changed/rebuilt/failed counts 和 errors 正确进入诊断模型。
- UXML 元素存在性测试：Summary / Technical 切换、Ability Events 区、AttributeChanged Events 区、Config Source 区、Errors 区存在。
- Play 手测：`RuntimeVerticalSlice.unity` 中执行 Strike / Ignite / Buff / Tick / Config Mode / Rebuild 后，Diagnostic View 可读且 Console 无 error。
- 如果使用 UI Toolkit 选择器或元素名，测试应固定关键 name/class，避免后续改样式时破坏数据绑定。

## 完成定义

- Diagnostic View 已追加在 M3 Config / Patch / Rebuild 面板之后。
- Summary / Technical 两种视图已接入 HUD 内切换。
- Ability Events 与 AttributeChanged Events 分区显示，Summary 显示 `showing N / total M`，Technical 保留 DTO 顺序。
- Config Source 显示 source、previous、policy、changed/rebuilt/failed counts 和 id 明细。
- Last cast failure 与 config errors 分来源展示；无错误显示 `No runtime errors`。
- `MxFramework.UI.Toolkit` 只新增通用文本 view model 与 UI 控制，不引用 Demo / Gameplay / Config.Runtime 类型。
- 未修改 `GameplayDiagnosticSnapshot`、`RuntimeConfigChangeSummary` 或 Combat。

## 完成记录

- 新增 `MxRuntimeDiagnosticViewModel`，由 Demo adapter 填充通用诊断字段。
- 新增 `RuntimeAbilitySliceDiagnosticViewModelBuilder`，映射 `GameplayDiagnosticSnapshot` 和 `RuntimeConfigChangeSummary` 到 Summary / Technical 文本模型。
- 扩展 `GameplayShowcase.uxml` / `.uss`，在 M3 面板之后加入 Diagnostic View、Summary / Technical 切换、事件、Config Source 和 Errors 分区。
- 扩展 `MxRuntimeHudController` 绑定诊断 header、summary/technical 列表和明确空状态。
- 增加 `RuntimeAbilitySliceDiagnosticViewModelBuilderTests` 覆盖空输入、事件/Config/Error 映射和 UXML 关键元素。
