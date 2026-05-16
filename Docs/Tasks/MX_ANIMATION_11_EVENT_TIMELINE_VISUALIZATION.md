# MxAnimation 11 - Event Timeline Visualization

> 来源：Gitea Issue #110
> 状态：Implemented

## Scope

- 在 `MxFramework.Animation` noEngine 层补充 presentation event dispatch payload、sink、dedupe window、diagnostics 和 event timeline row builder。
- 在 `MxFramework.Editor.Animation` 的 clip registry Inspector 中增加 Event Timeline Preview，复用 exporter 输出的 noEngine `MxAnimationSetDefinition`。
- 在 `MxFramework.Combat.Animation.Unity` bridge 中使用 `MxAnimationPresentationEventDedupeKey` 防止同一 action instance / frame / event id / source order 重复 dispatch。

## Boundaries

- Timeline preview 只查看 / 编辑 `MxAnimationClipRegistryAsset` 中的表现事件，不是完整 Timeline/Scrubber。
- Runtime DTO 只保存 `ResourceKey`、id、frame correlation 和 policy，不保存 `AnimationClip`、Unity object、GUID 或 `Assets/...` path。
- Presentation event 只驱动 VFX / SFX / Camera / Footstep / UI feedback 这类表现后端，不反向驱动 Combat authority、hit resolve、cancel window、damage、movement 或 replay hash。
- Late join 默认不补播一次性事件；需要补播时，项目层必须显式检查 `MxAnimationPresentationEventReplayPolicy.CatchUpSafe` 并执行自己的网络策略。

## Validation

- `AnimationContractTests` 覆盖 noEngine dispatch sink 去重、不同 action instance 隔离和 timeline row correlation。
- `MxAnimationClipRegistryExporterTests` 覆盖 Editor registry 导出 event payload `ResourceKey`、replay policy 和 timeline rows。
- `CombatMxAnimationUnityBridgeTests` 覆盖 Combat bridge frame event dispatch dedupe。
