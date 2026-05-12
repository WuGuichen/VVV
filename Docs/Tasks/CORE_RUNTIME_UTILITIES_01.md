# Core Runtime Utilities 01：Foundation Batch

> 状态：Planned
> 日期：2026-05-11
> 优先级：P0
> 子任务：`CORE_HANDLES_01_STABLE_HANDLE_TABLE.md`、`CORE_POOLING_01_OBJECT_REFERENCE_POOL.md`、`CORE_COLLECTIONS_01_RING_BUFFER.md`、`RUNTIME_FOUNDATION_04_TIMER_SCHEDULER.md`、`RUNTIME_RANDOM_01_DETERMINISTIC_RANDOM.md`

## 目标

补齐框架高频运行时小工具底座，服务 Ability、Combat、Audio、Resources、UI、Replay 和 SaveState。

本功能包不做大型玩法系统，只做 noEngine、可测试、可复用的基础工具：

1. `StableHandleTable<T>`
2. `ObjectPool<T>`
3. `ReferencePool<T>` / `ListPool<T>`
4. `RingBuffer<T>`
5. `RuntimeTimerScheduler`
6. `DeterministicRandom`

## 背景

当前 Core 公开工具主要是 `Heap<T>`、`UnsortList<T>`、`BitUtils`、`zstring`、`RandomTable`、`VectorExtensions`。Runtime 已有 Host、Clock、Command、Replay、Hash、SaveState 等大底座，但缺少 timer、stable handle、pool、deterministic random、recent-log buffer 这类细颗粒工具。

如果这些工具各模块自行实现，后续会出现：

- handle stale / 取消错对象。
- 高频路径临时分配散落。
- Replay 中随机来源不可控。
- Diagnostics 最近 N 条记录重复造轮子。
- Timer / Operation / UI item / Audio loop 等生命周期模型不一致。

## 范围

### 做

- Core handles：`StableHandle`、`StableHandleTable<T>`。
- Core pooling：`ObjectPool<T>`、`IReference`、`ReferencePool<T>`、可选 `ListPool<T>`。
- Core collections：`RingBuffer<T>`。
- Runtime scheduling：`RuntimeTimerScheduler`，Timer handle 基于 stable generation handle。
- Runtime random：`IDeterministicRandom`、`RuntimeRandomState`。
- 更新 `Docs/Interfaces/Core.md`、`Docs/Interfaces/Runtime.md` 或任务实现记录。
- 添加 Core / Runtime EditMode 测试。

### 不做

- 不做全局静态 pool / random / scheduler。
- 不依赖 UnityEngine 权威随机或 Unity time。
- 不把资源引用计数替换成通用 ReferencePool。
- 不引入线程调度、网络同步、Rollback 或大型 ECS。

## 模块边界

| 工具 | 目标程序集 | 原则 |
| --- | --- | --- |
| `StableHandleTable<T>` | `MxFramework.Core` | 通用 generation handle，不知道 Timer / Audio / Resource |
| `ObjectPool<T>` / `ReferencePool<T>` / `ListPool<T>` | `MxFramework.Core` | noEngine，实例池优先 |
| `RingBuffer<T>` | `MxFramework.Core` | 最近 N 条记录，不承担 diagnostics 格式 |
| `RuntimeTimerScheduler` | `MxFramework.Runtime` | 基于 `RuntimeFrame` / explicit delta，可接 `RuntimeCommandBuffer` |
| `DeterministicRandom` | `MxFramework.Runtime` | Replay / SaveState 友好，不使用 Unity random |

## 验收

- 六个基础工具均有独立任务文档和测试计划。
- Timer 使用 stable handle 或在实现记录中说明兼容迁移方案。
- `ObjectPool / ReferencePool / ListPool` 优先级提升为 P0。
- DeterministicRandom 支持 state capture / restore。
- RingBuffer 可支撑 recent errors / recent commands / recent events 这类诊断数据。
- 所有工具保持 noEngine；Unity 表现层只能作为 adapter 使用。

## 2026-05-11 实现记录

- 已实现 `StableHandle`、`StableHandleTable<T>` 和 `StableHandleTableSnapshot`。
- 已实现 `ObjectPool<T>`、`IReference`、`ReferencePool<T>`、`ListPool<T>`、`PooledList<T>`，并将 `ModifierContext` 的私有池改为 `ReferencePool<ModifierContext>`。
- 已实现 `RingBuffer<T>`。
- 已实现 `IDeterministicRandom`、`DeterministicRandom`、`RuntimeRandomState` 和 `RuntimeRandomStateJson`。
- 已实现 `RuntimeEventQueue<T>` 和 `RuntimeEventQueueSnapshot`。
- 已实现 `RuntimeTimerScheduler`，timer handle 基于 `StableHandleTable<T>`，并支持 callback、seconds、repeating、command、snapshot 和 save-state-friendly state。
- 新增 60 个源码级 NUnit 测试覆盖 Batch A1/A2。
- 验证：临时源码级 `dotnet test` 通过，`0` 失败、`60` 通过。Unity EditMode / 生成的 `.csproj` 需要 Unity 刷新新文件后再跑。
