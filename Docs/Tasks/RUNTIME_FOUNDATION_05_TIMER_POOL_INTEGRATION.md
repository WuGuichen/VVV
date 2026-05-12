# Runtime Foundation 05：Timer / Pool Integration Pass

> 状态：Planned
> 日期：2026-05-11
> 优先级：P2
> 前置：`RUNTIME_FOUNDATION_04_TIMER_SCHEDULER.md`、`CORE_POOLING_01_OBJECT_REFERENCE_POOL.md`

## 背景

Timer 和 Pool 不能只停留在工具类。完成 P0 Scheduler 与 P1 Pooling 后，需要立刻找 2-3 个真实落点验证它们是否适合现有 Runtime 主线、Demo、Diagnostics 和 SaveState。

本任务是工具层落地验证，不新增大型 Gameplay Ability System、Rollback、网络同步或 Addressables Provider。

## 目标

用现有模块或 Demo 验证：

- 运行时延迟事件统一通过 `RuntimeTimerScheduler`，并优先走 `RuntimeCommandBuffer`。
- 临时上下文 / 集合复用统一走 Core Pooling。
- Replay、SaveState、diagnostics 不因为工具接入而退化。

## 候选落点

优先选择 2-3 个，不要求全部完成：

| 候选 | 接入方式 | 验证点 |
| --- | --- | --- |
| Ability cooldown / auto sequence | scheduler 到期写入 `RuntimeCommandBuffer` | command 序列、Replay hash、SaveState |
| Breakout power-up duration | scheduler 或保留内置计数并加对比说明 | 玩法 duration 与诊断 |
| UI status toast / event log fade | callback scheduler | 非权威 UI 延迟消失 |
| Combat hit window / hurt window / invincible frames | scheduler command 模式 | hit window 开关顺序稳定 |
| query debug marker 过期 | callback scheduler | 诊断可视化生命周期 |
| SceneFlow loading timeout | scheduler command 模式 | timeout 错误路径 |
| Resource preload timeout | scheduler command 模式 | preload failure 结构化 |
| `ModifierContext` | `ReferencePool<ModifierContext>` | Clear / 复用 / 无私有池重复逻辑 |

## 范围

### 做

- 为每个选中的落点记录接入前后的执行链路。
- 优先使用 `ScheduleCommand` 处理会影响 gameplay state 的延迟事件。
- UI / Editor / debug-only 生命周期可以使用 callback 模式。
- 对至少一个落点验证 SaveState pending timers。
- 对至少一个落点验证 Replay hash 或固定输出序列。
- 对 `ModifierContext` 或同类临时上下文验证 `ReferencePool<T>`。

### 不做

- 不重写 Ability / Combat / SceneFlow 架构。
- 不把所有已有 frame counter 一次性迁移。
- 不引入全局 scheduler 或全局 pool。
- 不把资源引用计数替换成 ReferencePool。

## 建议实施顺序

### M1：ModifierContext Pooling

- `ModifierContext` 实现 `IReference`。
- 私有静态 `Stack<ModifierContext>` 替换为 `ReferencePool<ModifierContext>`，或由调用方显式持有 pool。
- 保持 `Get()` / `Push()` 兼容入口时，文档标注这是过渡 facade。

### M2：Runtime Scheduler Gameplay Path

- 选一个已有 Ability / Demo 自动事件，用 `ScheduleCommand` 替代局部计时。
- 证明到期事件仍通过 `RuntimeCommandBuffer.DrainForFrame()` 进入现有处理。
- 保存 pending timer 后恢复，继续到期触发同一 command。

### M3：Runtime Scheduler UI / Diagnostics Path

- 选一个 UI toast、event log fade 或 debug marker，用 callback scheduler 管理非权威生命周期。
- 诊断 snapshot 可看到 pending timer traceId。

## 测试

测试覆盖跟随选中落点，至少包括：

- Pool 接入后重复 Get / Release 不泄漏状态。
- Scheduler 接入后事件触发帧不变。
- Save / Restore 后 pending scheduled command 继续触发。
- Replay hash 或 command trace 序列稳定。
- callback timer 不进入 gameplay 权威路径。

## 验收

- 至少 2 个真实落点使用新 Timer 或 Pool。
- 至少 1 个 gameplay 落点使用 `ScheduleCommand` 而不是直接 callback 修改状态。
- 至少 1 个上下文或集合复用使用 Core Pooling。
- 相关任务文档更新实现记录，说明保留局部计数器的原因或迁移结果。
- Unity / dotnet 测试有对应验证记录。

