# Runtime Foundation 04A：Replay Playback

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

在现有 `RuntimeReplayRecorder` / `RuntimeReplaySnapshot` 基础上补齐 replay playback。Playback 负责按记录帧顺序把 command 交给外部 driver 执行，并比较 expected result hash 与 actual result hash。

Replay playback 不直接知道 Ability、Combat 或 Preview 的业务规则；它只编排输入、帧和结果校验。

## 建议写入范围

- `Assets/Scripts/MxFramework/Runtime/RuntimeReplayPlayback*.cs`
- `Assets/Scripts/MxFramework/Runtime/RuntimeReplayJson*.cs`（如实现 JSON roundtrip）
- `Assets/Scripts/MxFramework/Tests/Runtime/RuntimeReplayPlaybackTests.cs`
- 必要时新增对应 `.meta`

不要修改 Preview、Demo、Gameplay、Combat 或 Authoring 文件。

## 建议 API

```csharp
public interface IRuntimeReplayFrameDriver
{
    void Reset(RuntimeReplayHeader header);
    RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record);
}

public sealed class RuntimeReplayPlaybackRunner
{
    public RuntimeReplayPlaybackResult Play(RuntimeReplaySnapshot snapshot, IRuntimeReplayFrameDriver driver);
}
```

`RuntimeReplayPlaybackFrameResult` 至少包含：

- `RuntimeFrame Frame`
- `long ActualResultHash`
- `string DiagnosticsSummary`
- `IReadOnlyList<RuntimeCommandError> CommandErrors` 或等价结构化错误

`RuntimeReplayPlaybackResult` 至少包含：

- `bool Success`
- `int FramesPlayed`
- 首个 mismatch 的 frame、expected hash、actual hash
- mismatch frame 的 commands 和 diagnostics summary

## 规则

- Playback 必须按 `snapshot.Records` 顺序执行，不重新排序 record。
- Record 内 commands 的顺序使用已经记录的顺序，不重新分配 sequence。
- 空 snapshot 返回成功，但 diagnostics 要清楚说明没有 frame。
- Driver 抛异常时，playback 返回失败结果，不吞异常信息。
- Hash mismatch 后默认停止；后续可以增加 continue-on-mismatch 选项。
- JSON 序列化如果实现，应保持字段名稳定，不能保存对象图。

## 测试

至少覆盖：

- 空 replay 成功。
- 单帧 replay 成功。
- 多帧 replay 按顺序执行。
- expected / actual hash 不一致时返回 mismatch。
- driver 抛异常时返回结构化失败。
- snapshot copy 后 playback 不受 recorder 后续追加影响。

## 验收

- `RuntimeReplayPlaybackRunner` 可用纯 C# 测试验证。
- 不引入 Unity 依赖。
- 不改变现有 `RuntimeReplayRecorder` 行为。
- Playback 失败信息足够定位到具体 frame 和 command 列表。

## 2026-05-10 实现记录

- 新增 `RuntimeReplayPlayback.cs`，包含 `IRuntimeReplayFrameDriver`、`RuntimeReplayPlaybackRunner`、playback frame/result/failure model。
- Playback 按 snapshot record 原顺序执行，不重排 commands；hash mismatch 后停止。
- 空 snapshot 返回成功并带 `no frame records` diagnostics。
- Driver reset / run frame 异常返回结构化失败并保留异常对象。
- 新增 `RuntimeReplayPlaybackTests.cs` 覆盖空 replay、单帧、多帧、hash mismatch、driver exception、snapshot copy。
- 验证：Runtime / Tests dotnet build 通过；Unity EditMode `RuntimeReplayPlaybackTests` 6/6 passed。
- 未实现 JSON replay roundtrip；该项保留为后续可选任务。
