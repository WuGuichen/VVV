# Gameplay ECS-style 02：System Pipeline

> 状态：Implemented v0（2026-05-12）

## 目标

在 component store 之后补 Gameplay 运行时系统管线：

```text
IGameplaySystem
GameplaySystemPhase
GameplaySystemContext
GameplaySystemPipeline
RuntimeCommandBuffer still drained only by GameplayRuntimeModule
```

本批次不迁移 Ability / Despawn 业务逻辑。`GameplayRuntimeModule` 仍保留 v0 command handling；pipeline 作为后续 command systems、simulation systems 和 resolution systems 的调度底座先接入。

## Public API

```csharp
public enum GameplaySystemPhase
{
    PreCommand,
    Command,
    Simulation,
    Resolution,
    Diagnostics
}

public interface IGameplaySystem
{
    string SystemId { get; }
    GameplaySystemPhase Phase { get; }
    int Priority { get; }
    bool IsEnabled { get; }
    void Tick(GameplaySystemContext context);
}

public readonly struct GameplaySystemContext
{
    public RuntimeFrame Frame { get; }
    public double DeltaTime { get; }
    public double ElapsedTime { get; }
    public GameplayWorld World { get; }
    public IReadOnlyList<RuntimeCommand> Commands { get; }
    public RuntimeEventQueue<GameplayRuntimeEvent> Events { get; }
}

public sealed class GameplaySystemPipeline
{
    public int Count { get; }
    public void Add(IGameplaySystem system);
    public bool Remove(string systemId);
    public bool Contains(string systemId);
    public void Tick(GameplaySystemContext context);
    public GameplaySystemPipelineSnapshot CreateSnapshot();
    public void Clear();
}
```

## 执行顺序

Pipeline 按以下顺序稳定执行：

```text
GameplaySystemPhase asc
Priority asc
Registration sequence asc
```

`IsEnabled == false` 的 system 会被跳过，但仍保留在 pipeline 和 snapshot 中。同 phase / priority 下，registration order 具有语义；组合根或配置注册顺序变化会影响执行顺序。后续如果需要弱化注册顺序影响，可以再引入 explicit order 或 `SystemId` ordinal tie-breaker。

`GAMEPLAY_ABILITY_03_COMMAND_SYSTEM` 已把 built-in command handlers 迁入 pipeline。当前 default module 中，`PreCommand` 已位于 command systems 之前：

```text
Drain RuntimeCommandBuffer
-> Pipeline PreCommand
-> Pipeline Command
-> Pipeline Simulation
-> Pipeline Resolution
-> Pipeline Diagnostics
-> Optional GameplayWorld.Tick
```

## Context Lifetime

`GameplaySystemContext.Commands` 是帧内临时只读 view。System 可以在 `Tick()` 内读取；如果需要在 Tick 之后保留 command，必须复制 command 值，不能持有 `Commands` 列表引用。

## CommandBuffer Ownership

`RuntimeCommandBuffer` 仍只能由 `GameplayRuntimeModule` drain。System 不拿 command buffer，也不调用 `DrainForFrame`。

`GameplayRuntimeModule` 每帧：

```text
Drain RuntimeCommandBuffer
Execute current v0 built-in command handlers
Run GameplaySystemPipeline with drained commands
Optional GameplayWorld.Tick
```

`GAMEPLAY_ABILITY_03_COMMAND_SYSTEM` 已把 Ability / Despawn command handling 从 module 迁到 system。本批次文档保留 system pipeline 的底座说明；command system 细节见 `GAMEPLAY_ABILITY_03_COMMAND_SYSTEM.md`。

## 异常策略

System 抛异常时，pipeline 用 `GameplaySystemPipelineException` 包装 system id 和 phase 后重新抛出。第一版不吞异常，不做自动降级，避免隐藏权威 runtime 错误。

## 测试

新增 `GameplaySystemPipelineTests` 覆盖：

- phase / priority / registration order 稳定执行。
- disabled system 跳过，snapshot 统计 enabled count。
- add null / empty id / duplicate id 校验。
- system exception 包装 system id 和 phase。
- `GameplayRuntimeModule` 可选接入 pipeline，并把模块已 drain 的 commands 传给 systems。
