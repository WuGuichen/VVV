# Gameplay World 01E：Diagnostics / Hash

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`GAMEPLAY_WORLD_01_V0_FOUNDATION.md`

## 目标

把 Gameplay entity/world 状态接入 Runtime Foundation 04 的 hash contract，并扩展诊断入口。首轮目标是稳定 hash contributor，不要求完整 JSON 导出或 Editor 面板。

## 建议写入范围

- `Assets/Scripts/MxFramework/Gameplay/GameplayHash*.cs`
- `Assets/Scripts/MxFramework/Gameplay/GameplayWorldDiagnostics*.cs`
- `Assets/Scripts/MxFramework/Gameplay/MxFramework.Gameplay.asmdef`（本任务唯一允许修改，必要时新增 `MxFramework.Runtime` 引用）
- `Assets/Scripts/MxFramework/Tests/Ability/GameplayDiagnosticsHashTests.cs`
- 对应 `.meta`

不要修改 Runtime 生产代码、Demo、Preview 或 UI。

## 建议模型

```text
GameplayHashContributor
  - ContributorId = stable id
  - input: entities + attribute ids
  - writes entity id / team / alive / selected attributes / buff count / modifier count

GameplayWorldDiagnostics
  - wraps existing GameplayDiagnosticSnapshotBuilder where possible
  - adds world/entity count summary if 01A is available
```

如果 01A `GameplayWorld` 尚未存在，先让 contributor 接收 `IReadOnlyList<IRuntimeEntity>`；后续可增加 world overload。

## 规则

- Hash 输入必须按 entity id、attribute id、buff id、modifier id 等稳定顺序写入。
- 不使用对象地址、Dictionary 原始顺序、本地化文本或 Unity instance id。
- 只读取公开 API 和 snapshot，不访问私有字段。
- Gameplay 可以引用 `MxFramework.Runtime` 以实现 `IRuntimeHashContributor`，但 Runtime 不得反向引用 Gameplay。

## 测试

至少覆盖：

- 同一实体集合不同输入顺序，hash 一致。
- 属性变化，hash 变化。
- Buff / Modifier 数量或关键状态变化，hash 变化。
- Contributor id 稳定。
- `MxFramework.Gameplay.asmdef` 仍然 `noEngineReferences=true`。

## 验收

- Gameplay hash contributor 可与 04B `RuntimeHashCombiner` 一起使用。
- 诊断扩展不破坏现有 `GameplayDiagnosticSnapshotBuilder`。
- `dotnet build MxFramework.Tests.csproj --no-restore` 通过。

## 2026-05-10 实现记录

- 新增 `GameplayHashContributor : IRuntimeHashContributor`，支持 entity list 和 `GameplayWorld` overload。
- Hash 输入包含稳定 contributor id、world tick、entity id/team/alive、指定属性、Buff snapshot 和 Modifier snapshot。
- Entity、attribute、buff、modifier 均按稳定顺序写入 hash。
- 新增 `GameplayWorldDiagnostics` 和 `GameplayWorldDiagnosticsSummary`，包装现有 `GameplayDiagnosticSnapshotBuilder` 并提供实体/存活/attribute/buff/modifier 计数。
- `MxFramework.Gameplay.asmdef` 新增 `MxFramework.Runtime` 引用，仍保持 `noEngineReferences=true`。
- 新增 `GameplayDiagnosticsHashTests` 覆盖稳定 hash、属性/Buff/Modifier/world tick 变化、contributor id 和 asmdef noEngine。
