# Runtime Foundation 04E：Golden Replay Harness

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

建立 golden replay 测试入口，让后续 Ability、Combat、Gameplay 的固定 command 序列可以作为稳定回归样本。04E 可以先用测试内 fake driver / fake module 搭建 harness，等 04A/04B 合入后再接真实 playback 和 hash contributor。

## 建议写入范围

- `Assets/Scripts/MxFramework/Tests/Runtime/RuntimeGoldenReplayHarnessTests.cs`
- `Assets/Scripts/MxFramework/Tests/Runtime/RuntimeGoldenReplayFixtures*.cs`
- 必要时新增对应 `.meta`

不要修改 Runtime 生产代码；如果发现必须增加生产 API，先在本任务文档追加备注，等待 04A/04B 合入。

## Harness 形态

```text
GoldenReplayFixture
  - name
  - header
  - command frames
  - expected frame hashes
  - expected final hash
  - expected diagnostics keywords
```

测试重点不是 UI，而是把固定输入、固定 hash、失败诊断组织成可复用结构。

## 规则

- fixture 数据必须是 synthetic，不使用 WGame 真实业务数据。
- 不依赖 Unity 场景。
- 不读取当前时间或机器路径。
- 失败信息必须能定位 fixture name 和 frame。
- 如果 04A/04B API 尚未合入，先用测试内 fake driver 保持测试可编译。

## 测试

至少覆盖：

- fixture 可构建 replay snapshot。
- 成功 replay 输出 expected final hash。
- 单帧 hash mismatch 能报告 fixture name、frame、expected、actual。
- command 序列顺序稳定。
- diagnostics summary 被带入失败报告。

## 验收

- Golden replay harness 可以在 Runtime tests 中单独运行。
- 04A/04B 合入后只需替换 fake driver，即可变成端到端 playback 测试。
- 后续 Ability / Combat fixture 可以复用同一结构。

## 2026-05-10 实现记录

- 新增 `RuntimeGoldenReplayFixtures.cs`，包含 synthetic golden fixture、snapshot 构建、fake replay driver、harness/result model。
- 新增 `RuntimeGoldenReplayHarnessTests.cs` 覆盖 snapshot 构建、成功 final hash、单帧 mismatch 报告、命令顺序稳定和 diagnostics 报告。
- 未修改 Runtime 生产代码。
- 验证：Tests dotnet build 通过。Unity TestRunner 首轮受并行新增文件 cleanup 检查拦截；后续父级 dotnet 编译验证通过。
