# Runtime Foundation 03：Save State / Runtime Serialization

> 状态：SaveState Contract v0.1 Implemented；Ability Showcase SaveLoad 接入已完成；通用 Gameplay restore 待办
> 日期：2026-05-10
> 优先级：P0
> 设计文档：`Docs/RUNTIME_FOUNDATION_SYSTEM.md`
> 前置：`RUNTIME_FOUNDATION_01_RUNTIME_HOST.md`、`RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`

## 目标

定义框架级运行时状态保存和恢复契约，让 Entity、Attributes、Buffs、Modifiers、Ability、Counters 和关键资源引用可以被稳定序列化、版本化、恢复和诊断。

本任务只做框架通用运行时状态，不做 WGame 业务存档。

## SaveState 与 Replay 的区别

| 类型 | 用途 | 内容 |
|------|------|------|
| Replay | 复现一段输入如何产生结果 | 命令序列、frame hash、诊断摘要 |
| SaveState | 从某个时刻恢复继续玩 | 当前运行时对象状态、版本、配置引用 |
| DebugSnapshot | 排查问题 | 只读诊断，不保证可恢复 |

三者可以互相引用，但不能混成一个格式。

## 范围

### 做

- 定义 SaveState schema。
- 定义 `ISaveStateProvider` / `ISaveStateRestorer`。
- 定义版本、迁移和兼容策略。
- 支持 JSON 序列化首版。
- 支持 Gameplay slice 保存和恢复。
- 支持 Buff / Modifier / Counter 恢复语义。
- 支持失败时返回结构化错误。

### 不做

- 不做具体游戏背包、任务、成就、剧情。
- 不迁移 WGame 旧 SaveData。
- 不做加密、防作弊、云存档。
- 不保存 Unity 场景对象实例。
- 不承诺 DebugSnapshot 可直接当 SaveState。

## 建议 Schema

```text
RuntimeSaveState
  - schemaVersion
  - createdAtUtc
  - frameworkVersion
  - configVersion
  - resourceCatalogVersion
  - frame
  - entities[]
  - globalCounters[]
  - moduleStates[]
  - metadata

EntitySaveState
  - entityId
  - definitionId
  - teamId
  - isAlive
  - attributes[]
  - buffs[]
  - modifiers[]
  - abilities[]
  - counters[]

AttributeSaveState
  - attributeId
  - baseValue
  - finalValuePolicy

BuffSaveState
  - buffId
  - instanceId
  - layer
  - remainingTime
  - duration
  - sourceId
  - configVersion
  - customState

ModifierSaveState
  - modifierId
  - instanceId
  - sourceId
  - paramIndex
  - counters
  - customState

AbilitySaveState
  - abilityId
  - cooldownRemaining
  - charges
  - lastCastFrame
  - sourceConfigId
```

首版可以只实现 Gameplay slice 需要的字段，其他字段先进入 schema 预留和文档说明。

## 恢复策略

恢复时不直接反序列化私有运行时对象，而是走 factory / registry：

```text
Read JSON
  -> Validate schemaVersion
  -> Migrate if needed
  -> Resolve config/resource references
  -> Create runtime entity
  -> Restore attributes
  -> Recreate buffs/modifiers through factories
  -> Apply saved runtime fields
  -> Rebuild derived caches
  -> Capture diagnostics
```

关键规则：

- `finalValue` 默认不作为权威保存值，恢复后由 base + modifiers 重算。
- 已挂 Buff / Modifier 的 runtime state 可以保存剩余时间、层数、计数器。
- 如果配置不存在，恢复必须失败或进入明确的 degraded state，不允许静默创建默认配置。
- Resource 引用保存 `ResourceKey`，不保存 Unity 对象实例。
- CustomState 必须带 `typeId` 和 `schemaVersion`。

## 版本迁移

建议接口：

```csharp
public interface ISaveStateMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    SaveStateMigrationResult Migrate(SaveStateDocument document);
}
```

迁移规则：

- 只允许单步迁移，跨多个版本由 pipeline 串联。
- 迁移失败必须保留原始错误和路径。
- 删除字段必须有默认策略或明确不兼容。
- 配置 ID 改名属于项目层迁移，框架只提供 hook。

## Milestones

### M1：Save Contract

- 定义 SaveState DTO。✅
- 定义 provider/restorer 接口。✅
- 定义 error code。✅
- JSON roundtrip 测试。✅

### M2：Gameplay Slice Save

- 保存 RuntimeEntity。
- 保存 Attribute base values。
- 保存 active Buff layer / remaining。
- 保存 Modifier counters。
- 保存 Ability cooldown / last cast summary。

### M3：Restore

- 从 SaveState 重建 RuntimeEntity。
- 重建 Buff / Modifier。
- 重建后 Diagnostics snapshot 与保存前关键字段一致。
- 恢复后可继续 Tick。

### M4：Version Migration

- schema v1 -> v2 示例迁移。
- 缺少迁移器时返回结构化错误。
- 字段路径错误可定位。

### M5：Runtime Showcase Save / Load

- HUD 或测试入口触发 save。Ability Slice runner API 已完成。
- Reset runtime。
- Load save。
- 验证 HP / Buff / Ability 状态恢复。当前覆盖 HP / Buff / Modifier；Ability cooldown/charges 等待 Ability 状态模型补齐。

## 错误码建议

| Code | 场景 |
|------|------|
| 1001 | Unsupported schema version |
| 1002 | Missing migration |
| 1003 | Invalid document |
| 1101 | Missing config reference |
| 1102 | Missing resource reference |
| 1201 | Unknown entity definition |
| 1202 | Unknown buff id |
| 1203 | Unknown modifier id |
| 1301 | Custom state type mismatch |
| 1302 | Custom state migration failed |

## 测试建议

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Runtime
Unity EditMode: MxFramework.Tests.Gameplay
Unity EditMode: MxFramework.Tests.Config
```

测试重点：

- SaveState JSON roundtrip 保持稳定字段名。
- 恢复后属性 final value 由 modifier 重算。
- Buff 剩余时间和层数恢复后继续 Tick。
- 缺失配置不静默成功。
- schema version 不支持时返回错误。
- migration pipeline 按顺序执行。

## 验收

- 有可读 SaveState JSON 示例。
- Runtime Showcase 能完成 save -> reset -> load -> continue。
- SaveState 和 DebugSnapshot 文档明确区分。
- 恢复失败有结构化错误和字段路径。
- 不保存 Unity 对象实例，不引入 WGame 业务字段。

## 2026-05-10 M1 实现记录

- Runtime noEngine 程序集新增 SaveState v0 契约：`RuntimeSaveState`、Entity / Attribute / Buff / Modifier / Ability / Counter / CustomState / Module DTO。
- 新增 `IRuntimeSaveStateProvider`、`IRuntimeSaveStateRestorer`，本轮只定义框架契约，不实现 Gameplay 真实恢复。
- 新增结构化错误模型：`RuntimeSaveStateErrorCode`、`RuntimeSaveStateError`、`RuntimeSaveStateResult<T>`。
- 新增 `IRuntimeSaveStateMigration` 与 `RuntimeSaveStateMigrationPipeline`，按单 schema step 串联迁移；缺失迁移返回 `MissingMigration`，迁移失败保留原始错误。
- 新增 `RuntimeSaveStateJson.SaveToJson()` / `LoadFromJson()`，使用 Newtonsoft.Json，保持 Runtime noEngine 且不引用 UnityEngine / UnityEditor。
- 新增 NUnit 覆盖：DTO 拷贝与 JSON roundtrip、迁移成功链、缺失迁移、迁移失败、invalid / unsupported document、custom state type/version/payload 字段保留。
