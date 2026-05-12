# Runtime Foundation 04C：SaveState Orchestration

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

在现有 SaveState DTO、provider/restorer contract 和 migration pipeline 之上，补齐通用保存/恢复编排器。编排器负责注册多个模块的 provider/restorer、按稳定顺序执行、聚合错误，并保持 SaveState 与 Replay 的职责边界。

## 建议写入范围

- `Assets/Scripts/MxFramework/Runtime/RuntimeSaveStateCoordinator*.cs`
- `Assets/Scripts/MxFramework/Runtime/RuntimeSaveStateRegistry*.cs`
- `Assets/Scripts/MxFramework/Tests/Runtime/RuntimeSaveStateOrchestrationTests.cs`
- 必要时新增对应 `.meta`

不要修改 Ability、Combat、Preview、Demo 或 UI 文件。

## 建议模型

```text
RuntimeSaveStateCoordinator
  -> providers ordered by order/moduleId
  -> capture each module/entity/global state
  -> merge into RuntimeSaveState
  -> run migration pipeline on load if needed
  -> restorers ordered by order/moduleId
  -> restore and aggregate errors
```

建议增加一个 registration 描述：

```csharp
public sealed class RuntimeSaveStateParticipant
{
    public string ParticipantId { get; }
    public int Order { get; }
    public IRuntimeSaveStateProvider Provider { get; }
    public IRuntimeSaveStateRestorer Restorer { get; }
}
```

具体 API 可调整，但必须表达 participant id、顺序、provider 和 restorer。

## 规则

- participant id 不能为空，重复注册必须返回结构化错误或抛可诊断异常。
- restore 顺序必须稳定：先 `Order`，再 `ParticipantId`。
- provider/restorer 抛异常时不能导致后续错误丢失；结果里要保留 participant id。
- migration pipeline 只处理 schema，不负责业务 ID 改名。
- 恢复失败不能静默成功。

## 测试

至少覆盖：

- 多 provider capture 顺序稳定。
- 多 restorer restore 顺序稳定。
- 重复 participant id 被拒绝。
- provider 异常被包装成结构化错误。
- restorer 返回失败时 coordinator 聚合错误。
- migration 缺失时返回现有 `MissingMigration` 风格错误。

## 验收

- 可用纯 C# 测试验证多模块保存和恢复编排。
- 不引入 Unity 依赖。
- 不改变现有 SaveState JSON 字段名。
- 文档和错误模型明确 SaveState 不是 Replay。

## 2026-05-10 实现记录

- 新增 `RuntimeSaveStateRegistry.cs`，包含 participant、registry、重复 id 结构化拒绝，以及 `Order` + `ParticipantId` 稳定排序。
- 新增 `RuntimeSaveStateCoordinator.cs`，包含 capture / restore coordinator、SaveState 合并、restore 前 migration、按 participant 聚合错误。
- 新增 `RuntimeSaveStateOrchestrationTests.cs` 覆盖稳定顺序、重复注册、provider 异常包装、restorer 失败聚合和缺失 migration。
- 验证：Runtime / Tests dotnet build 通过；Unity EditMode `RuntimeSaveStateOrchestrationTests` 6/6 passed；`RuntimeSaveStateTests` + orchestration tests 12/12 passed。
