# LCC-M5-T1：Animated Hurtbox Presence Authoring 与 Runtime Binding

> Status: Guide
>
> 工作包：LCC-M5-T1（生产实现 / Authoring）
>
> 状态权威：[LCC-M5 第 3 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M5_ANIMATED_APPENDAGE_SOFT_CONTACT.md)
>
> 执行文档状态：`Verified`（v7 Network authority identity 与 M1 Body Presence consumer兼容回修后，相关focused 30/30与Unity Console 0闭合）
>
> 前置：T0 `Verified`，M5 `Production-Frozen`。

## 1. 任务结果与非目标

Body Presence v2 能安全作者化固定容量的 Animated Hurtbox 引用，经校验、规范化保存、compiler、geometry binding v7、RuntimeSpawn 和 CharacterStudio 单链投影；Dragon14 正式配置引用 head/tail-mid/tail-tip，重复生成稳定。

本任务不建立运行时接触、压力、事件、网络状态或 Playable，不修改 Combat separation、motion/contact resolver、Hurtbox sync、攻击 Trace 或 M3 topology。

## 2. 前置与并行

只有 T0 `Verified`、父 M5 `Production-Frozen` 且主代理已回写精确 owner/filter 后才可派发。T1 生产写入时 T2/T3 保持等待；允许 T2 只读消费冻结合同，不允许并行修改 RuntimeSpawn/CharacterTest 共享 binding。

## 3. 必读输入

- M5 与 T0 最终冻结表；
- Body Presence v1 Authoring/save/compiler/RuntimeSpawn/CharacterStudio 当前链；
- geometry binding v1～v6 compatibility；
- Dragon14 body_presence/body_colliders/body-part topology 与正式 import workflow；
- CharacterApplication、Combat 接口和 `Docs/QUALITY_GATE.md`。

## 4. 写入所有权

- `CharacterAuthoringBodyPresence.cs` 的 v2 DTO/codec/canonical/hash；`CharacterPackageValidation.BodyPresence.cs`；`CharacterAuthoringCompiler.cs` 的 Body Presence additive hunk；`EditorServer.BodyPresence.cs`、`CharacterPackageCommands.BodyPresence.cs`。
- `CharacterRuntimeSpawnContracts.cs` 的 Body Presence binding、`CharacterImportedPackageJson.cs` 的 v7 dispatch，以及新建 `CharacterImportedPackageJson.BodyPresenceV2.cs` partial。
- `Tools/MxFramework.CharacterStudio/web/src/inspector.js` 的 Body Presence v2 editor 和 `scripts/smoke.mjs`。
- Dragon14 `geometry/body_presence.json` 和正式生成器产物；生成输出只能由 CLI/Unity import 写入。
- Authoring `character-body-presence`、新建 Unity `CharacterBodyPresenceV2RuntimeBindingTests` 与 Studio smoke。

共享文件的精确 partial/hunk、filter 和 Evidence 名必须由 T0 回写后才能派发。禁止写 CharacterTest motion/contact/hurtbox、Network、Combat core、父状态文档和 T2/T3 tests。

## 5. 冻结合同

- v1 `volumes[]` 与 ModelRoot 最大 4 不变；v2 仅追加 `animatedHurtboxSources[]`，候选最大 4，最终值由 T0 冻结。
- animated source 只保存 stable `sourceId/colliderId/skinMillimeters/pressureScaleMilli/enabled`，不复制 shape、part、bone、socket 或动画轨。
- colliderId 必须唯一、存在于 `body_colliders.json`、属于 CharacterHurtbox，并能解析稳定 PartId；未知、重复、unsupported/future、容量超限全部 typed fail closed。
- Missing/v1 不升级字节；Future/Invalid 原文只读；存在 Current v2 才输出 additive geometry binding v7。
- Dragon14 首期恰好引用 `hurt.head.obb.0`、`hurt.tail_mid.capsule.0`、`hurt.tail_tip.capsule.0`；不得把音频 ContactPoint 当作参与资格。
- v2 source identity/content hash 必须覆盖这些引用和策略；Runtime 不读取作者 JSON。

## 6. 实施与验证

1. 落 Authoring DTO、JSON converter、canonical sort/hash、revision-gated save 和 typed validation。
2. 落 compiler/source mapping、geometry v7 与 RuntimeSpawn v1～v7 compatibility。
3. CharacterStudio 显示/编辑 stable collider 引用、skin、pressure、enabled；刷新/Undo/Redo/unknown schema保护复用现有 Body Presence 工作流。
4. 写入 Dragon14 正式源，通过 validate、双 compile hash 和 Unity import；不手改 generated/prefab/meta。
5. focused 覆盖 Missing/v1/v2/future/invalid、duplicate/missing collider、顺序扰动、容量、source mismatch、round-trip 和 Dragon14 三引用。

本任务完成只证明可被 T2 消费的数据与 binding 存在，不得以 CharacterStudio marker 或生成 JSON 宣称角色已有接触反馈。

冻结命令/证据：Authoring CLI test selector `character-body-presence`；`node Tools/MxFramework.CharacterStudio/scripts/smoke.mjs`；Unity EditMode `MxFramework.Tests.CharacterBodyPresenceV2RuntimeBindingTests`；证据写入 `Docs/Evidence/LCC/LCC-M5-T1/AUTHORING_RUNTIME_BINDING_2026-08-26.md`。

## 7. 回滚边界

Authoring 源通过 revision-gated 前向修复或恢复本任务字段；生成输出只允许从保留的源重新 import。不得 checkout/reset 整个 Dragon14 包，不得删除现有 v1 Presence、M2/M3/M4数据、`.meta` 或其它 dirty 生成结果。v7 尚未通过 T1 Gate 前可在主代理确认无外部消费者后撤回；通过后只做兼容前向修复。

## 8. 派发与回报

执行者开始前确认 T0 已回写精确 owner/filter。完成后报告实际文件、schema/兼容、正式生成命令与结果、focused 原始结果、未运行项和风险。发现必须复制几何或修改 CharacterHurtboxLayer mask 时立即停止。

## 9. 实际执行记录

| 项目 | 当前记录 |
| --- | --- |
| 状态权威 | M5 工作包表 |
| 执行者 / 审查者 | `lcc_m5_t1_authoring` / 主代理接收完成 |
| 实际修改文件 | Body Presence DTO/validation/compiler/CLI dedicated save；geometry v7 RuntimeSpawn partial/contracts；CharacterStudio inspector/state/smoke；Dragon14 source/generated；focused Authoring/Unity tests |
| Authoring / Runtime binding | 已实现并通过 focused gate；v1 保持兼容，v2 最多 4 个 stable collider ref，Dragon14 为 3 个 |
| 自动化 / Unity import | Authoring `character-body-presence` 通过；Unity focused `3/3`；脚本门禁 0 error；双 compile hash 相同；formal import report `canSpawn=true/errors=0` |
| 证据 | `Docs/Evidence/LCC/LCC-M5-T1/AUTHORING_RUNTIME_BINDING_2026-08-26.md` |
| 限制 | CharacterStudio full smoke 被既有 ActionSet fixture TypeError 阻断；T2 未完成前无 Gameplay 行为 |

### v7 consumer compatibility repair

T3 回归发现 CharacterTest network authority identity 与 M1 Body Presence consumer 仍以 v6 为最新版本。T1 已 additive 接受 v7：network 继续要求 authored Presence、M2 与 M4 identity；Body Presence system 继续同步 v7 的既有 ModelRoot volumes。最终相关 focused `30/30`，脚本门禁 0 error；详见同一 T1 Evidence 的 repair 段。
| T3 回派 | 正式 v7 在 `CharacterTestNetworkSessionFactory` 被拒绝，且 `CharacterTestCharacterBodyPresenceSystem` 只接受 v4～v6；T1 重新打开，必须补 additive v7 consumer compatibility 与focused回归 |
| 回修结果 | NetworkSessionFactory 与 M1 BodyPresence consumer 均接受v7且保持v1～v6/future fail-closed；相关focused 30/30、Unity gate 0 error，主代理/T3复验v7/root Presence失败消失 |
