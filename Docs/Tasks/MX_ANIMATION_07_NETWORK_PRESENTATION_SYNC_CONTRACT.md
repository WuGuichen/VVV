# MxAnimation 07：Network Presentation Sync Contract

> Issue: #106
> 状态：Design + noEngine Contract Draft
> 任务等级：S3
> 日期：2026-05-16

## 目标

定义 MxAnimation 在多人、late join、补包和预测纠偏场景下的表现同步契约。本切片不实现网络库、rollback 框架或服务端协议，只固定表现层可消费的 noEngine DTO、校验结果和事件去重键。

## 非目标

- 不同步 `PlayableGraph`、Animator state、Unity bone pose、Unity object reference 或 normalized time。
- 不让 MxAnimation 状态进入 Combat authority、Replay hash、命中、取消、无敌、伤害或权威移动。
- 不在 `MxFramework.Animation` 中引用 `UnityEngine`、`UnityEditor`、Playables 或 Unity asset path。
- 不实现 rollback / prediction 框架；只定义 MxAnimation 如何从权威动作状态恢复表现。

## v0 同步字段

`MxAnimationPresentationSyncState` 是远端或 late join 侧恢复表现所需的最小状态：

- actor / entity id，当前用 string 保存，不依赖 Combat assembly。
- animation set id / version / hash。
- resource catalog hash。
- clip registry version。
- action id 或 action key。
- action instance id。
- started-at Combat frame。
- current local frame。
- presentation sync status：started / running / canceled / finished / interrupted。
- layer sync states。
- quantized blend parameters。
- correlation id。

该状态只用于 presentation correction。客户端可以据此 seek、crossfade、停止 layer 或恢复 layer weight 过渡；它不能写回 Combat 权威状态。

## Layer Sync State

`MxAnimationLayerSyncState` 记录 late join 恢复 layer transition 所需的最小数据：

- layer id。
- current weight。
- target weight。
- transition start frame。
- transition duration frames。
- transition remaining frames。
- transition policy id。
- correlation id。

只同步静态 `layerId + weight` 不足以恢复 upper-body 从 0 到 1 的过渡。v0 使用 current / target / remaining frames 表达过渡状态，后续 #108 的 diagnostics 应复用同一字段集。

## Quantized Parameters

`MxAnimationQuantizedParameter` 用于同步 locomotion blend 等表现参数：

- parameter id。
- quantized value。
- scale。

它用于表现层插值，不代表 Combat 权威速度。若某个参数需要影响权威逻辑，必须在 Combat / Gameplay 侧作为显式、可复现输入建模。

## Event Dedupe Key

`MxAnimationPresentationEventDedupeKey` 是表现事件去重的稳定键：

- actor / entity id。
- action instance id。
- world frame。
- local frame。
- event id。
- source order。

该 key 支持 late packet、重复事件和 catch-up 过程中的去重。`action instance id` 可以为 0，以兼容旧桥接路径；此时仍用 actor、frame、event id 和 source order 去重。v0 不定义完整 dedupe window；#110 会在此 key 基础上定义按 actor + action instance 的 bounded window 和 catch-up policy。

## Version Validation

`MxAnimationPresentationSyncValidator` 校验同步状态与本地资源/配置版本是否兼容：

- actor id 必须存在。
- animation set id 必须存在。
- animation set id / version / hash 可以与 expected 值比较。
- resource catalog hash 可以与 expected 值比较。
- clip registry version 可以与 expected 值比较。

`MxAnimationPresentationSyncVersionExpectation.None` 只校验必需 identity，不做 version / hash 比较；需要强校验的调用方必须显式传入 expected version / hash。

hash / version mismatch 必须输出明确 diagnostics，不能静默 fallback 到空播。#109 会在 warmup 和 resource validation 中复用该契约并补充具体资源级错误。

## 与 #107 的关系

#106 先冻结 v0 sync id / hash 最小集合，#107 可以基于该集合并行做 data format / mapping provider 验证。如果 #107 发现字段缺口，应回到 #106 评审补充，但不能无边界扩大同步契约。

## 验收清单

- noEngine DTO 已落在 `MxFramework.Animation`。
- `MxFramework.Animation` asmdef 仍保持 `noEngineReferences=true`。
- focused tests 覆盖 layer transition state、quantized parameter、event dedupe key equality、version mismatch diagnostics 和 missing identity diagnostics。
- `Docs/Interfaces/Animation.md` 记录表现同步契约。
- `Docs/Interfaces/Combat.md` 继续明确 Combat frame / action event 是权威来源，MxAnimation 不反向驱动 Combat。
