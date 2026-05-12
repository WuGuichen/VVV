# Runtime Foundation 04：v1 并行收口

> 状态：Completed / Verified 2026-05-10
> 日期：2026-05-10
> 优先级：P0
> 设计文档：`Docs/RUNTIME_FOUNDATION_SYSTEM.md`
> 前置：`RUNTIME_FOUNDATION_01_RUNTIME_HOST.md`、`RUNTIME_FOUNDATION_02_FRAME_COMMAND_REPLAY.md`、`RUNTIME_FOUNDATION_03_SAVE_STATE_SERIALIZATION.md`

## 目标

把 Runtime Foundation 从 v0.1 契约推进到 v1 可用闭环：同一组 command 可以回放验证，核心状态可以保存和恢复，Authoring Preview 可以走正式 RuntimeHost 路径，测试可以用 golden replay 稳定防回归。

本阶段不是增加玩法复杂度，而是先把运行时底座变成可复现、可诊断、可组合的生产级基础设施。

## 公共契约冻结

以下规则在 04A-04E 并行任务中必须保持一致：

- `MxFramework.Runtime` 继续保持 `noEngineReferences=true`，不得引用 `UnityEngine`、`UnityEditor`、Preview、Demo、Gameplay 或 Combat。
- Replay 只记录输入、结果 hash 和诊断摘要，不记录完整对象图；完整对象图属于 SaveState。
- SaveState 只负责当前状态恢复，不承担输入复现；不能把 Replay 数据塞进 SaveState DTO。
- Runtime hash 不包含对象地址、Dictionary 原始迭代顺序、本地化文本或 Unity 实例 ID。
- Preview 可以依赖 Runtime，Runtime 不反向依赖 Preview。
- 每个子任务只写自己的文件范围；遇到需要跨范围改动时，先在任务文档追加备注，不直接改别人范围。

## 并行任务

| 任务 | 状态 | 负责人范围 | 任务文档 |
|------|------|------------|----------|
| 04A Replay Playback | Completed | Replay playback / playback diagnostics | `RUNTIME_FOUNDATION_04A_REPLAY_PLAYBACK.md` |
| 04B Hash Contributor | Completed | Runtime hash contract / accumulator / deterministic tests | `RUNTIME_FOUNDATION_04B_HASH_CONTRIBUTOR.md` |
| 04C SaveState Orchestration | Completed | Provider/restorer registry / restore ordering / error aggregation | `RUNTIME_FOUNDATION_04C_SAVE_STATE_ORCHESTRATION.md` |
| 04D Preview Host Adapter | Completed | Preview -> RuntimeHost adapter path | `RUNTIME_FOUNDATION_04D_PREVIEW_HOST_ADAPTER.md` |
| 04E Golden Replay Harness | Completed | Golden replay fixtures / test harness / desync report expectations | `RUNTIME_FOUNDATION_04E_GOLDEN_REPLAY_HARNESS.md` |

## 集成顺序

并行实现可以同时开始，但合入时建议按以下顺序：

1. 04B Hash Contributor：先稳定 result hash 输入和 accumulator。
2. 04A Replay Playback：使用 04B hash 结果做 playback compare。
3. 04C SaveState Orchestration：独立合入，但需要和 playback 区分错误模型。
4. 04D Preview Host Adapter：接入 RuntimeHost，不阻塞 04A-04C。
5. 04E Golden Replay Harness：最后把 04A/04B 合成端到端测试。

## 非目标

- 不做网络同步、rollback、预测、输入压缩。
- 不重构 Ability Graph、Combat Motion 或 Authoring Editor UI。
- 不引入 WGame 业务字段、真实配置数据或私有运行时依赖。
- 不把 RuntimeHost 变成全局单例。
- 不新增第三方 DI、序列化或测试框架依赖。

## 验收

- 相同 replay 输入在同一运行环境下两次 playback 结果 hash 一致。
- Playback hash mismatch 能输出 frame、expected、actual、commands 和 diagnostics summary。
- SaveState coordinator 可以按顺序调用多个 provider/restorer，并聚合结构化错误。
- Preview apply/tick/reset 至少有一条路径通过 RuntimeHost 驱动。
- Golden replay harness 能作为后续 Ability / Combat / Gameplay 回归入口。

## 分发规则

子代理开始前必须读取本文件和对应子任务文档。所有子代理都不是独自在代码库中工作，不能回退或覆盖其他人的改动；如果遇到未提交改动，应保留并围绕它们实现。

## 2026-05-10 实现记录

- 04A 已完成 `RuntimeReplayPlaybackRunner`、`IRuntimeReplayFrameDriver`、frame/result/failure model 和 Runtime playback tests。JSON replay roundtrip 未纳入本轮，仍作为后续可选项。
- 04B 已完成 `RuntimeHashContext`、`IRuntimeHashContributor`、`RuntimeHashCombiner`、`RuntimeHashAccumulator` 和确定性 hash tests。
- 04C 已完成 `RuntimeSaveStateRegistry`、`RuntimeSaveStateCoordinator`、participant 稳定排序、capture/restore 聚合错误和 orchestration tests。
- 04D 已完成 `RuntimePreviewHostAdapter`，Preview Runtime asmdef 新增 `MxFramework.Runtime` 引用；Runtime 不反向引用 Preview。
- 04E 已完成 synthetic golden replay fixture / harness tests，未修改 Runtime 生产代码。
- 父级验证：`dotnet build MxFramework.Runtime.csproj --no-restore` 通过，0 warning / 0 error。
- 父级验证：`dotnet build MxFramework.Tests.csproj --no-restore` 通过，0 warning / 0 error。
