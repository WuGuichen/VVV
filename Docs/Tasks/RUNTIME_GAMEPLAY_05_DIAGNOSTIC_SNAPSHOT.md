# Runtime Gameplay 05：Diagnostic Snapshot

> **状态**: ✅ 已完成（r1194）
> **优先级**：P0
> 所属 Goal：`PHASE11_RUNTIME_GAMEPLAY_GOAL.md`
> 前置任务：`RUNTIME_VERTICAL_SLICE_04_CONFIG_DRIVEN_ABILITY.md`

## 目标

为 `MxFramework.Gameplay` 增加纯 C# 运行时诊断快照，让 Entity / Ability / Buff / Modifier / Events 的状态可以被测试、Demo、AI 上下文和后续编辑器读取，而不是只能看 Unity Console 或 OnGUI 字符串。

目标链路：

```text
RuntimeEntity[]
  + attribute ids
  + last AbilityCastResult
  + AbilityEvent log
  + AttributeChangedEvent log
  + ability source
  -> GameplayDiagnosticSnapshot
```

## 范围

### 必须完成

1. 在 `MxFramework.Gameplay` 中新增诊断快照模型。

建议类型：

```text
GameplayDiagnosticSnapshot
GameplayEntitySnapshot
GameplayAttributeSnapshot
GameplayBuffSnapshot
GameplayModifierSnapshot
GameplayAbilityCastSnapshot
GameplayAbilityEventSnapshot
GameplayAttributeEventSnapshot
```

要求：

- 全部位于 `Assets/Scripts/MxFramework/Gameplay/`。
- 不引用 UnityEngine / UnityEditor。
- 数据结构只读或外部不可变。
- 字段名直接、AI 可读。

2. 新增 Snapshot Builder。

建议类型：

```text
GameplayDiagnosticSnapshotBuilder
```

建议 API：

```csharp
GameplayDiagnosticSnapshot Build(
    string sourceName,
    string abilitySource,
    IReadOnlyList<RuntimeEntity> entities,
    IReadOnlyList<int> attributeIds,
    AbilityCastResult lastCastResult,
    IReadOnlyList<AbilityEvent> abilityEvents,
    IReadOnlyList<AttributeChangedEvent> attributeEvents)
```

如果实现需要微调签名，必须保持：

- 可在纯 C# EditMode 测试中调用。
- 不要求 Unity 场景或 MonoBehaviour。
- 不读取模块私有字段。

3. Entity 快照至少包含：

- `EntityId`
- `TeamId`
- `IsAlive`
- `Attributes`
- `Buffs`
- `Modifiers`

属性快照通过传入的 `attributeIds` 读取：

- `AttributeId`
- `FinalValue`

不要求本任务修改 `AttributeStore` 内部结构。

4. Ability 快照至少包含：

- `AbilitySource`
- `LastCastSuccess`
- `LastFailureReason`
- `LastTargetEntityIds`
- Ability event list
- Attribute event list

5. RuntimeAbilitySliceRunner 集成。

Demo 可继续用 OnGUI，但必须能构建快照，并显示简短摘要：

```text
Snapshot: entities=2, abilityEvents=N, attributeEvents=N, source=...
```

6. 测试。

至少覆盖：

- Entity attributes / buffs / modifiers 被写入快照。
- Last cast target ids 被写入快照。
- Ability events 顺序被写入快照。
- AttributeChanged events 被写入快照。
- Config Driven ability source 能写入快照。
- 空输入不抛异常，返回空集合。

7. 文档。

至少更新：

- `Docs/Interfaces/Gameplay.md`
- `Docs/USAGE.md`
- `Docs/CAPABILITIES.md`
- 本任务状态

### 不做

- 不做 JSON 序列化。
- 不做编辑器 UI。
- 不做 Runtime Preview 协议接入。
- 不做 Mod Package 诊断合并。
- 不新增 Unity 场景。
- 不引入 WGame 业务类型。

## 验收标准

1. `MxFramework.Gameplay` 新增诊断快照模型和 Builder。
2. Builder 不引用 UnityEngine / UnityEditor。
3. Builder 可在纯 C# 测试中构建快照。
4. 快照包含 Entity / Attribute / Buff / Modifier / Ability / Event 基本信息。
5. `RuntimeAbilitySliceRunner` 可构建并展示快照摘要。
6. Unity Console 无编译 Error。
7. Gameplay / Ability / Config 相关 EditMode 测试通过。
8. 文档说明 Snapshot 的用途和当前边界。
9. 不新增 `.unity` 场景。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- 自动化测试通过。
- GitNexus 检查 low risk 或影响面合理。
- SVN 提交信息建议：

```text
Add gameplay diagnostic snapshot
```
