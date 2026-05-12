# Runtime Foundation 04D：Preview Server Host Adapter

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 父任务：`RUNTIME_FOUNDATION_04_V1_PARALLEL_CLOSEOUT.md`

## 目标

让 Preview apply/tick/reset 至少有一条正式路径通过 `RuntimeHost` 驱动，而不是只由 `IPreviewWorld` 私有 tick 路径完成。首轮目标是建立 adapter 和测试夹具，不要求一次性重构所有 ScenePreviewWorld 行为。

## 建议写入范围

- `Assets/Scripts/MxFramework/Preview/Runtime/RuntimePreviewHostAdapter*.cs`
- `Assets/Scripts/MxFramework/Preview/Runtime/MxFramework.Preview.Runtime.asmdef`（需要引用 `MxFramework.Runtime` 时）
- `Assets/Scripts/MxFramework/Tests/Preview/RuntimePreviewHostAdapterTests.cs`
- 必要时新增对应 `.meta`

不要修改 Runtime noEngine 层，不要修改外部 Authoring Web UI。

## 建议模型

```text
Preview request
  -> RuntimePreviewHostAdapter
  -> RuntimeCommandBuffer.Enqueue(command)
  -> RuntimeHost.Tick(frame)
  -> RuntimeReplayRecorder.RecordFrame(...)
  -> RuntimePreviewAdapterResult / RuntimePreviewSnapshot
```

首轮可以使用测试模块或轻量 preview module 验证 Host lifecycle 和 frame tick，不必把所有 Buff 逻辑强行迁移。

## 规则

- Preview 依赖 Runtime，Runtime 不依赖 Preview。
- Adapter 必须显式 initialize/start/stop/dispose Host，不能依赖全局单例。
- `reset` 后 command buffer、clock、replay recorder 和 preview state 必须清理。
- `tick(frames)` 必须用显式 frame 推进。
- 出错时保留 Preview 现有 `PreviewError` 风格，不把异常直接刷成 500。

## 测试

至少覆盖：

- adapter 初始化后 Host 进入 Started。
- apply 或 tick 会推进 Host TickCount。
- reset 后 frame / command / recorder 状态被清理。
- Host module 抛异常时 Preview 返回结构化失败。
- asmdef 引用方向正确：Preview Runtime 引用 Runtime，Runtime 不引用 Preview。

## 验收

- Preview Host Adapter 有独立测试，不依赖真实场景。
- 不破坏现有 `RuntimePreviewAdapter` / `IPreviewWorld` 路径。
- 后续可以逐步把 ScenePreviewWorld 接入 Host，而不是本轮大改。

## 2026-05-10 实现记录

- 新增 `RuntimePreviewHostAdapter.cs`，显式管理 `RuntimeHost` initialize / start / stop / dispose。
- 新 adapter 通过 `RuntimeCommandBuffer -> RuntimeHost.Tick -> RuntimeReplayRecorder` 驱动 apply / tick / reset 的最小路径。
- `MxFramework.Preview.Runtime.asmdef` 新增 `MxFramework.Runtime` 引用；Runtime asmdef 未反向引用 Preview。
- 新增 `RuntimePreviewHostAdapterTests.cs` 覆盖 lifecycle、apply、tick、reset、host module exception 和 asmdef 方向。
- 验证：Tests dotnet build 通过；Unity EditMode `RuntimePreviewHostAdapterTests` 6/6 passed；Unity Console error 0。
- 未改动 `ScenePreviewWorld`、Runtime noEngine 层或外部 Authoring Web UI。
