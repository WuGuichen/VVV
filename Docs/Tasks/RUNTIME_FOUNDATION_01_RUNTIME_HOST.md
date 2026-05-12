# Runtime Foundation 01：Runtime Host / Composition Root

> 状态：Host Core v0.1 Implemented / Ability Showcase 接入已完成
> 日期：2026-05-10
> 优先级：P0
> 设计文档：`Docs/RUNTIME_FOUNDATION_SYSTEM.md`
> 前置：Phase 11 Runtime Gameplay Foundation、Resource Management M6、Combat Motion v1

## 目标

建立框架级 Runtime Host，让游戏项目、Demo、Preview Server 和测试可以用同一套组合根装配运行时模块。

Host 不负责业务逻辑，不持有全局单例；它只负责：

- 模块注册。
- 生命周期顺序。
- Tick 分组。
- 错误收集。
- Diagnostics 汇总入口。
- 与后续 Frame / Command / Replay / SaveState 的连接点。

## 为什么先做

当前各模块已经能独立工作，但装配点分散在 Demo、Preview、测试和项目层样例里。继续向 Ability、Combat、Mod 和 Save 扩展前，必须先让“运行时如何启动、每帧如何推进、如何停止”成为稳定契约。

## 范围

### 做

- 新增 Host / Module / Lifecycle 最小契约。
- 支持模块按 stage 注册和排序。
- 支持 `Initialize`、`Start`、`Tick`、`Stop`、`Dispose`。
- 支持生命周期错误诊断。
- 支持从 Host 捕获模块 debug snapshot。
- 让 Runtime Showcase 至少有一条路径通过 Host 装配 Gameplay slice。

### 不做

- 不做业务场景管理。
- 不做联网 session。
- 不做第三方 DI 容器集成。
- 不迁移所有 Demo 代码。
- 不改变 Attributes / Buffs / Modifiers 的公共语义。

## 建议目录

第一版建议：

```text
Assets/Scripts/MxFramework/Runtime/
  MxFramework.Runtime.asmdef
  RuntimeHost.cs
  RuntimeModule.cs
  RuntimeLifecycle.cs
  RuntimeTickStage.cs
  RuntimeHostDiagnostics.cs

Assets/Scripts/MxFramework/Tests/Runtime/
  RuntimeHostTests.cs
```

如果拆 asmdef 成本过高，可先放在 `Gameplay/RuntimeFoundation/`，但公共类型命名仍按 `MxFramework.Runtime` 设计，避免后续迁移破坏 API。

## 建议 API

```csharp
public enum RuntimeLifecycleState
{
    Created,
    Initialized,
    Started,
    Stopped,
    Disposed
}

public enum RuntimeTickStage
{
    PreSimulation,
    Simulation,
    PostSimulation,
    Diagnostics
}

public interface IRuntimeModule
{
    string ModuleId { get; }
    int Priority { get; }
    RuntimeTickStage TickStage { get; }

    void Initialize(RuntimeHostContext context);
    void Start(RuntimeHostContext context);
    void Tick(RuntimeTickContext context);
    void Stop(RuntimeHostContext context);
    void Dispose();
}
```

Host 上下文第一版只放通用依赖：

```text
IConfigRegistry
IResourceManager
IEventBus registry or event dispatcher
IDiagnosticsSink
IServiceRegistry minimal key-value service locator
```

注意：`IServiceRegistry` 只服务组合根，不能鼓励业务代码到处拉全局服务。

## Tick 顺序

建议首版固定顺序：

```text
PreSimulation
  -> drain queued changes
Simulation
  -> Gameplay / Combat / Buff / Modifier ticks
PostSimulation
  -> late events / cleanup
Diagnostics
  -> snapshots / reports
```

模块排序规则：

1. 先按 `RuntimeTickStage`。
2. 再按 `Priority` 升序。
3. 再按 `ModuleId` 字典序，保证稳定。

## 错误语义

| 场景 | 行为 |
|------|------|
| 重复注册同 ModuleId | 返回失败结果或抛可诊断异常 |
| 未 Initialize 就 Start | 抛 `InvalidOperationException`，包含当前 state |
| Tick 中模块抛异常 | Host 捕获到 `RuntimeHostError` 后按策略决定继续或停止 |
| Stop / Dispose 重复调用 | 幂等 |
| Dispose 后再次 Tick | 抛异常 |

错误策略首版可提供：

```text
FailFast
CollectAndStopFrame
CollectAndContinue
```

默认使用 `FailFast`，测试和 Preview 可按需覆盖。

## Milestones

### M1：Host Core

当前状态：✅ v0.1 已实现

- `RuntimeHost` 可注册模块。
- 生命周期状态机可测试。
- Tick 顺序稳定。
- 重复注册和非法生命周期有测试。

### M2：Diagnostics

当前状态：✅ v0.1 已实现

- Host 可输出模块状态、当前 lifecycle、最后错误。
- 模块可选实现 debug snapshot provider。
- Diagnostics 不进入高频分配路径。

### M3：Gameplay Showcase 接入

当前状态：✅ Ability Showcase 已实现

- Runtime Showcase 增加一条 Host 装配路径。
- 旧路径保留，便于对比。
- HUD 或日志能显示 Host state、模块列表和 tick count。当前通过 `RuntimeFoundationSummary` 暴露 frame / command / hash / replay frame。

### M4：Preview Server 接入准备

当前状态：📋 待实现

- Preview Runtime 可通过 HostContext 装配 Config / Gameplay。
- 不要求本任务完成 Preview UI 改造。

## 当前实现记录

2026-05-10 Host Core v0.1 已落地：

- 新增 `MxFramework.Runtime` noEngine 程序集。
- 新增 `RuntimeHost`、`IRuntimeModule`、`RuntimeModule`、`RuntimeLifecycleState`、`RuntimeTickStage`、`RuntimeTickContext`、`RuntimeHostContext`。
- 新增 `RuntimeHostErrorPolicy`、`RuntimeHostError`、`RuntimeHostException`、`RuntimeHostDiagnostics`。
- 新增 `RuntimeServiceRegistry` 作为最小组合根服务表。
- `RuntimeHostTests` 覆盖稳定排序、重复注册、非法生命周期、Stop/Dispose 幂等、Tick 异常策略、Initialize/Start 失败不推进状态和 diagnostics copy。

验证：

```text
dotnet build MxFramework.Tests.csproj --no-restore
rg "UnityEngine|UnityEditor" Assets/Scripts/MxFramework/Runtime
```

## 测试建议

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Runtime
Unity EditMode: MxFramework.Tests.Gameplay
```

测试重点：

- 注册顺序乱序，Tick 顺序仍稳定。
- 生命周期非法调用会失败。
- Stop / Dispose 幂等。
- 模块 Tick 抛异常时错误策略生效。
- Diagnostics snapshot 不直接暴露可变内部集合。

## 验收

- 有纯 C# Host 单元测试。
- Runtime Showcase 至少一个路径能通过 Host 启动 Gameplay slice。
- 文档同步 `Docs/INTERFACES.md` 或新增 `Docs/Interfaces/Runtime.md`。
- `Docs/CAPABILITIES.md` 仅在功能真正落地后更新，不在本规划任务中标为完成。
