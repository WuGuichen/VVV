# CharacterTest Runtime AI Lab Draft Model And Persistence

> Status: Design
>
> Date: 2026-07-17
>
> Parent: `CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_WORKBENCH.md`
>
> Scope: Runtime AI Lab experiment workspace, behavior-group/card draft identity, local recovery, copy-on-write editing, and explicit collaborative export. This document does not define behavior-card step/condition payloads, runtime compilation, revision replay, or publishing.
>
> Published-card, player-deck and Runtime AI Lab relationship: [Runtime AI Planner behavior-card gameplay and Runtime AI Lab](CHARACTER_TEST_RUNTIME_AI_PLANNER_BEHAVIOR_CARD_GAMEPLAY.md).
>
> Single-behavior authoring authority: [单行为创作与组合预览设计](CHARACTER_TEST_RUNTIME_AI_LAB_SINGLE_BEHAVIOR_AUTHORING_PREVIEW_DESIGN.md).

> Implementation status (2026-08-18): workspace/group/card GUID identity, Arena snapshot import, group/card copy-on-write, stable hash references, versioned shared-draft persistence, split local recovery, 500ms autosave, focus/exit flush, `.previous` recovery, newer-schema read-only handling, save-failure dirty retention and up to 100 in-process Undo/Redo operations are implemented. The production Lab entry reuses the versioned workspace referenced by `latest-workspace.txt`; named shared workspaces are not automatically pruned. Explicitly supplied local/test roots retain the bounded five-workspace cleanup policy. Persisted-workspace browsing remains a separate follow-up workflow. Behavior-card logic v8 is implemented and documented in `CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_CARD_PAYLOAD.md`. Process locking/concurrent read-only handling, extension-data round-trip for supported schemas and explicit collaborative export remain follow-up work.

## Single-Behavior Authoring Override (2026-08-07)

本文记录的 `Experiment Workspace -> Behavior Group Draft -> Behavior Card Draft` 是既有广义 Lab 草稿实现和正式行为组兼容流程。它不再定义单行为编辑界面的产品层级。

单行为创作的目标持久化关系固定为：

```text
Behavior Card Draft
└─ Behavior Test Context
   ├─ Behavior Test Composition Draft
   └─ Behavior Test Setup Draft

Apply And Rerun
└─ immutable Behavior Test Run Snapshot
```

- `Behavior Card Draft` 是唯一可编辑和可发布的正式创作对象；
- `Behavior Test Composition Draft` 跟随当前行为草稿保存，包含唯一 owner 行为和固定版本陪测行为；
- `Behavior Test Setup Draft` 保存主测试槽位、敌我阵容、项目行为组固定版本、出生位置和帧上限；
- 二者均为本地测试上下文，不具有正式组 stable ID、项目 revision、项目路径或发布能力；
- `Behavior Test Run Snapshot` 与 RuntimeFrame 历史不写入草稿或项目目录；
- 现有单卡隔离 group draft 只能作为迁移输入，不能继续在 UI 或项目来源状态中冒充正式行为组。

本文件后续关于正式 `Behavior Group Draft`、group copy-on-write、group export 和多 Agent 共享组的规则继续服务既有兼容/正式组流程；单行为测试组合的字段、身份、恢复和失败规则以链接的新设计为准。

## Summary

Runtime AI Lab 使用三层草稿数据：`Experiment Workspace -> Behavior Group Draft -> Behavior Card Draft`。工作区负责恢复完整实验配置，行为组负责有序实例卡和 Runtime AI Planner 默认设置，行为卡负责版本化行为内容。

项目行为卡和行为组 JSON 是正式权威；这里描述的草稿只属于当前用户在 AI Lab 中的本地工作副本。打开项目版本不会修改项目目录，也不会自动替换当前实验。项目保存必须追加 revision，并通过 Editor-only 双目录事务；Player 只读取项目 JSON。项目目录缺失、hash 不一致或固定卡版本缺失时保持阻塞诊断，不使用 C# seed 静默兜底。

实验设置的行为组选择器同时展示正式项目行为组和本地行为组草稿。草稿行必须明确标记“本地草稿”，可以被当前实验槽位选择、编辑、持久化和恢复，但不得因此自动获得项目 stable ID，也不得写入 `behavior_group_catalog.json`。只有用户在基本信息区显式执行“确认为正式行为组”，并通过行为组及固定行为卡引用校验后，才创建新的短 stable ID 和 `r1` 项目版本；确认失败时本地草稿及当前实验选择保持不变。

本地自动保存用于个人恢复；显式导出用于版本控制和协作。两者都不是发布，也不得直接进入 gameplay authority。Runtime Tick 只读取后续 compiler 生成的不可变 evaluator，不读取本设计定义的 JSON。

玩家收藏和 Installed Deck 不属于本草稿目录。目标生命周期是 `Draft -> Validated -> Published -> Unlocked -> Installed Deck -> Runtime Instance`；只有经过独立 Publish 门禁的稳定卡 ID 才能被玩家存档引用。

## Data Layers

### Experiment Workspace

一个 workspace 表示可恢复的 Runtime AI Lab 实验配置。它使用持久化 GUID `workspaceId`，最少保存：

- schema、workspaceId、可选 stableId、displayName、createdUtc、updatedUtc 和 revision。
- 场景/测试区预设、simulation frame limit 和工作台选择上下文。
- Team 1 / Team 2 槽位：启用状态、稳定角色引用、行为组草稿引用、出生 X/Z。
- 当前 workspace 引用的 group/card 文件清单及内容 hash。

workspace 不内嵌完整 group/card 内容。槽位不得保存运行时 entity id、profile slot、compiled evaluator 或 timeline cursor。

### Behavior Group Draft

> 本节描述正式/兼容 Behavior Group Draft，不描述单行为编辑器的测试组合。测试组合不得继承本节的 published stableId、组级编辑或发布语义。

行为组草稿使用 GUID `groupDraftId`。现有 Arena `int DraftId` 仅是当前会话 adapter，不是持久化身份。

group 最少保存：

- schema、groupDraftId、可选 published stableId、displayName 和 revision。
- targeting、perception、movement 等 Runtime AI Planner 组级默认设置。
- 有序 `entries[]`。

每个 entry 使用独立 GUID `entryId`，并保存 `cardDraftId`、order、disabled 和 parameterOverrides。多个 entry 可以引用同一 card draft；entry identity 与 card identity 必须分离。

### Behavior Card Draft

行为卡草稿使用 GUID `cardDraftId`，与发布后的 stableId 分离。未发布草稿可以没有 stableId，不能用显示名代替身份。

card envelope 最少保存：

- schema、cardDraftId、可选 stableId、displayName、category 和 capability metadata。
- revision、payloadSchema、payloadVersion 和 payload。
- 引用摘要和最后一次校验/编译状态的非权威快照。

payload 的条件、步骤、判断窗口、分支和 End 结构由下一专项设计确定。本设计只要求 payload 有独立 schema/version，避免 envelope 随行为语义频繁迁移。

## Identity And Revision Rules

- workspace、group、card 和 entry 均使用标准 GUID 字符串；创建后不得因重命名或排序而改变。
- published stableId 是发布身份，不得复用 draft GUID，也不得在未发布阶段自动生成正式 catalog id。
- 玩家存档、卡组和联网请求只引用 published stableId；会话开始时由 authority 解析最新兼容 Published revision，并固定 revision/content hash/catalog revision 到该会话。
- `CurrentRevision` 每次成功 mutation 递增；revision 只在同一对象内比较，不作为跨对象全局序号。
- workspace manifest 保存引用文件的 revision 和 content hash，用于发现缺失文件、并发覆盖和不完整写入。
- JSON 数组按显式 order、稳定 id 排序；对象属性采用固定 writer 顺序，确保导出 diff 和 content hash 稳定。

## Shared Persistence And Local Recovery

可跨机器协作、由 Git 管理的草稿权威路径固定为：

```text
Authoring/RuntimeAiLab/Drafts/
  latest-workspace.txt
  <workspaceId>/
    workspace.json
    groups/<groupDraftId>.json
    cards/<cardDraftId>.json
```

本机事务恢复路径固定为：

```text
UserSettings/RuntimeAiLab/DraftRecovery/<workspaceId>/
  .staging/
    workspace.json
    groups/*.json
    cards/*.json
  .previous/
    workspace.json
    groups/*.json
    cards/*.json
```

`Authoring/RuntimeAiLab/Drafts` 是未发布但可协作的创作源，进入版本控制但不进入游戏构建；`UserSettings` 只保存事务暂存和 last-known-good，不进入版本控制或游戏构建。另一台机器拉取仓库后通过同一 `latest-workspace.txt` 和稳定 GUID 继续编辑同一工作区。若 Lab 未来运行于非 Editor 环境，本机恢复路径可映射到 `Application.persistentDataPath/RuntimeAiLab/DraftRecovery`，但共享文件合同不变。

项目行为卡工作副本即使暂时没有被任何行为组 entry 引用，也必须保留在共享工作区的 `cards/` 和 manifest `cards` 引用中。打开旧共享工作区时，持久化层以项目行为卡 catalog 补齐缺失的 standalone card draft；已有本地修改的同 stable ID 草稿优先，不得被 Published 内容覆盖。

### Autosave

- mutation 后等待 500ms；连续修改合并为待保存的最新 revision。
- 在主线程捕获不可变 DTO，不让后台线程读取可变 UI/runtime 对象。
- 使用单写入队列；同一 workspace 同时最多一个 writer。
- 先写临时目录并完成 JSON/reference/hash 校验，再原子替换当前版本；`workspace.json` 最后提交。
- 替换共享草稿前将最近一次完整成功版本保留到本机 `.previous`；只保留一份 last-known-good，不进入 Git。
- 导航离开编辑层、应用 revision、关闭 Lab 或失去应用焦点时请求立即 flush；退出阶段失败必须留下明确诊断。
- 保存失败不更新 `LastSavedRevision`，不清除 Dirty，也不破坏 `.previous`。

### Draft State

UI 必须分别显示：

| 状态 | 含义 |
| --- | --- |
| `CurrentRevision` | 当前内存草稿版本。 |
| `LastSavedRevision` | 最近成功写入本地恢复文件的版本。 |
| `LastCompiledRevision` | 最近完成 compiler 尝试的版本，无论成功或失败。 |
| `LastAppliedRevision` | 当前模拟实际使用的不可变 revision。 |

`Dirty = CurrentRevision != LastSavedRevision`。Saved 不代表 Compiled，Compiled 不代表 Applied，Applied 不代表 Validated 或 Published。

### Recovery And Compatibility

- 进入 Lab 设置始终从当前 Arena/Setup 创建工作区，只从 `latest-workspace.txt` 选择性恢复 Team/Slot 的 `Enabled`、`SpawnX` / `SpawnZ`；不得恢复或提示旧角色、行为组、行为卡和 loadout。完整草稿的浏览和恢复必须由独立入口显式触发。
- 当前文件完整且 hash/reference 校验通过时正常加载。
- 当前文件损坏时加载 `.previous`，显示 `Recovered from previous`，并保留损坏版本供诊断；不得静默覆盖。
- 缺失 group/card 时允许以诊断模式打开 workspace，但禁止 compile/apply/export-as-validated。
- 高于当前支持版本的 schema 只读打开；不得降级保存或丢弃未知字段。
- envelope 使用 extension data 保留未知可选字段；已知关键字段、enum 和引用仍执行严格校验。
- 第一版不自动迁移旧 Editor-only 07 schema。现有 Arena group 通过显式 `Import current Arena group` 创建全新的 GUID 草稿，源数据不变。

## Copy-On-Write Editing

Runtime AI Lab 首次选择现有 Arena/published 内容时，将其导入为 workspace draft snapshot，不原地修改来源。

首次对 Published Behavior Card 或其它共享发布内容进行语义编辑时同样必须自动创建 copy-on-write card draft。该操作不会改变稳定发布卡、玩家收藏、已安装卡组或正在运行的 session；只有后续独立 Publish 才能产生新的 Published revision。

- 多个 Agent 引用同一 group draft 时，第一次编辑 group 会克隆 group，并仅将当前 Agent 重新绑定到 clone。
- group entries 可以共享 card draft。第一次修改共享 card 时克隆 card，并只改写当前 entry 的 `cardDraftId`。
- 复制 entry 只创建新的 entryId；card 继续共享，直到其中一个实例实际编辑。
- 用户可显式执行“编辑共享源”，但第一版不提供此命令，避免跨 Agent 隐式联动。
- copy-on-write 作为一个原子 Undo 操作记录，撤销时恢复引用并删除无人引用的临时 clone。

## Undo And Concurrency

- Undo/Redo 为进程内 command history，默认最多 100 次 mutation；不写入 JSON。
- 自动保存不清空 Undo；重新加载 workspace 后 Undo 历史为空。
- Undo/Redo 后仍按普通 mutation 更新 revision、Dirty、precompile 和 autosave。

以下并发规则是尚未实现的目标：

- workspace lock 保存 process id、session token 和打开时间。
- 第二个进程默认只读，可执行“另存为副本”；第一版不提供多人实时协作或自动 merge。
- 检测到外部 revision/content hash 变化时停止写入，要求 Reload 或 Save As Copy，不采用 last-writer-wins。

## Explicit Export

本节定义尚未实现的协作导出目标，不代表当前已有可调用的导出入口。

可协作草稿包固定导出到：

```text
Tools/MxFramework.Authoring/samples/runtime-ai-lab/<workspace-stable-id>/
  workspace.json
  groups/*.json
  cards/*.json
  export.json
```

导出前先 flush 当前本地草稿，并在 staging directory 生成完整 package。全部文件和引用校验通过后再替换目标目录。

`export.json` 最少记录 source workspaceId、exportedUtc、workspace revision、各文件 content hash、compile status、validation status 和 tool/schema versions。

- 允许导出 Dirty 已保存但尚未通过模拟验证的草稿；package 必须明确标记 `Draft`、`CompileFailed` 或 `NotValidated`。
- 导出不生成 published behavior stableId，不更新 behavior definition catalog/Behavior Loadout，也不写入 `StreamingAssets`。
- 只有后续发布专项可以把 Validated Draft 转换为 Published Behavior。

## Excluded State

以下数据不得进入 workspace/group/card JSON：

- timeline records、external command history、authority checkpoint 和 state hash samples。
- compiled evaluator、compiled profile、profile slot 和运行时 entity id。
- Playables、动画、骨骼、IK、ragdoll、VFX、音频和 presentation checkpoint。
- Overlay/HUD view model、调试字符串、搜索/滚动状态和临时诊断对象。
- SaveState、network snapshot、正式 replay wire 或 gameplay authority 字段。

工作区可以保存轻量编辑上下文，例如最后选中的 Agent slot、group/card 和右栏层级；这些字段只改善恢复体验，不参与 compiler 或 runtime。

## Failure Handling

| 失败 | 行为 |
| --- | --- |
| JSON 解析或 hash 失败 | 打开 `.previous`，标记 Recovery Required。 |
| 引用文件缺失 | 诊断模式打开，禁止 compile/apply。 |
| autosave I/O 失败 | 保持 Dirty 和旧 last-known-good，允许重试/另存。 |
| workspace 被其他进程锁定 | 只读打开或另存副本。 |
| schema 版本过新 | 只读打开，不写回。 |
| export 目标发生外部变化 | 停止覆盖，要求重新载入或选择新 stable path。 |

任何失败都不得清空内存草稿、替换当前 evaluator 或修改正在运行的 Runtime AI Lab session。

## Implementation Acceptance

当前已通过实现和回归覆盖：

- workspace/group/card 三层 JSON roundtrip、稳定 writer 顺序和 content hash。
- 500ms autosave 合并、Dirty/Save/Compile/Apply 状态独立性。
- 原子写入与 `.previous` 恢复。
- group/card copy-on-write 对其他 Agent 和 entry 的隔离。
- Arena group 显式导入和 session-local int DraftId adapter。
- 缺失引用、较新 schema 只读打开和保存失败时保留 Dirty。
- Runtime Tick 不读取 draft JSON；保存/导出不影响 authority hash、timeline 或当前 evaluator。
- `.previous` 固定为每个工作区一份本机 last-known-good。生产共享草稿不自动删除其他版本化工作区；显式传入的本地/测试根仍最多保留当前工作区和四个最近恢复工作区。

仍待实现和验收：

- workspace process lock、第二进程只读和 Save As Copy。
- 外部 revision/content hash 冲突检测。
- supported schema 的 extension-data round-trip。
- export package 的 staging、确定性 diff、hash 和状态标记。

## Boundaries And Follow-Up

本文不定义以下已经由其它专项实现的行为语义和运行边界；对应文档与源码是当前事实源：

- 行为卡条件、步骤、分支和判断窗口 payload。
- behavior-group/card Runtime Compiler。
- 下一 RuntimeFrame 应用 revision 和确定性重演事件。

CharacterTest-local Published Behavior Card catalog、兼容 revision 解析、玩家解锁/Installed Deck、Behavior Loadout 推荐预设和 append-only 本地 Publish 也已由玩家卡组专项实现，但不属于本文的持久化 envelope。生产级 Validated Draft 审批、catalog 签名/分发/回滚、账号/云存档和联网权威仍是后续专项；它们必须继续遵守本文的身份、copy-on-write 和保存边界。
