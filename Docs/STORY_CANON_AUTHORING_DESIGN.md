# WGame Story Canon Authoring Design

> Status: Current
>
> Scope: WGame project-level StorySource authoring, Story Input Packet approval, and ArtSource handoff.

## Summary

StorySource 是 WGame 项目正史事实源、故事大纲和跨部门输入包系统，不替代 `MxFramework.Story` runtime。Runtime story graph、beat、choice、blackboard、command、config mapping 和 Unity adapter 仍归 `MxFramework.Story`；StorySource 只负责维护可审批、可校验、可交接的项目故事设计输入。

第一阶段目标是建立 `Canon -> Outline -> Packet -> ArtSource` 生产链：先把世界基石和单元种子写成 canon facts，再用 canon facts 约束故事大纲，最后由已批准的大纲和正史事实派生 Story Input Packet，用于解除 ArtSource 的 Story gate。当前旧故事输入包已经移除；新的内容方向从 `StorySource/Projects/WGame/Canon/physical_world_foundation.md` 的自然物理基石重新开始，尚未派生新的 canon facts、outline 或 approved packet。

## Boundaries

StorySource 可以做：

- 维护世界前提、核心事件、地点功能、文明来源、玩家身份、敌人来源、力量规则、可感知语义、情绪标签和内容禁区。
- 维护结构化世界观记忆，避免把正史事实散落在 prompt、Art Bible 或普通 Markdown 概念段落里。
- 维护轻量故事大纲，包括 beat flow、冲突、开放问题和目标消费方。
- 写故事时同步维护 Story Input Packet 的美术参照字段，避免 ArtSource 在缺故事事实时自行补设定。
- 给 ArtSource、Runtime、UI 等消费方输出结构化 packet。
- 记录 canon、outline、packet 的版本、审批状态、变更记录和 review 结论。

StorySource 不可以做：

- 不写 Unity `.asset`、`.prefab`、`.unity`、`.meta` 或 StreamingAssets。
- 不写 `ArtSource/Projects/WGame/art_manifest.json`。
- 不写 Resource Manager Profile 或 Global Resource Build Profile。
- 不直接修改 runtime SaveState。
- 不把 WGame 正史写进 MxFramework 通用模块。

## Directory Contract

`StorySource/Projects/WGame/` 是项目内容事实源：

- `Canon/`：世界、地点、势力、角色类别、敌人来源、力量规则、可感知语义。
- `Outlines/`：故事大纲索引和 Markdown 大纲正文。
- `Packets/`：面向 Art、Runtime、UI 等消费方的批准输入包。
- `Reviews/`：审批记录、变更结论、退回原因。
- `Exports/`：给 ArtSource 或 Story.Config 的干净导出件。
- `story_packets.json`：packet 索引，供 Authoring provider 和 Resource Manager 读取。

## Canon Fact v1

Canon fact 是世界观记忆的最小事实单元。事实类型固定为：

- `world`
- `location`
- `faction`
- `character_archetype`
- `enemy_origin`
- `power_rule`
- `symbol`
- `tone`
- `forbidden_content`

每条 canon fact 包含：

- `canonId`
- `type`
- `title`
- `summary`
- `status`
- `tags`
- `relations`
- `usedByPacketIds`
- `usedByOutlineIds`

`status` 只能是：

- `draft`：草稿记忆，可以被大纲引用，但不能支撑 approved packet。
- `canon`：已确认正史，可以进入 approved packet。
- `deprecated`：被新事实替代。
- `rejected`：退回或否决。

只有 `canon` 状态的 fact 可以支撑 `approved-for-art` packet。ArtSource 不能直接读取 Canon，也不能补写 Canon。

## Story Outline v1

第一版故事大纲使用 Markdown 正文加 JSON 索引，兼容后续 `Tools/MxFrameworkStoryAuthoring/story_authoring.py` 的 beat/choice 导出方向。每个 outline 包含：

- `outlineId`
- `title`
- `stage`
- `outlinePath`
- `canonRefs`
- `beats`
- `conflicts`
- `openQuestions`
- `targetConsumers`

`stage` 只能是：

- `draft`
- `review`
- `approved-for-packet`
- `superseded`

大纲必须引用 canon facts；不允许把世界观事实只写在大纲正文里。批准为 `approved-for-packet` 前，引用的 canon facts 必须都是 `canon`，并且至少有一个 beat。

## Story Input Packet v1

JSON schema id 是 `wgame.story.input_packet.v1`。最小必需字段：

- `storySourceId`
- `version`
- `approvalStatus`
- `canonRefs`
- `worldPremise`
- `coreEvent`
- `locationFunction`
- `civilizationOrigin`
- `playerRole`
- `enemyOrigins`
- `powerRules`
- `symbolMeanings`
- `toneTags`
- `visualDoNotUse`

`outlineRefs` 是可选字段，但如果填写，引用的大纲必须是 `approved-for-packet`。

`approvalStatus` 只能是：

- `draft`：可编辑，不能解除 ArtSource 门禁。
- `approved-for-art`：已批准给美术，ArtSource 可以派生参考图提示词和评审标准。
- `superseded`：被新版本替代，不能作为新生产输入。
- `rejected`：退回修改，不能作为生产输入。

数组字段不能用空数组通过审批。批准给美术前，所有必需字符串字段必须非空，所有必需数组字段至少有一项，`canonRefs` 指向的 facts 必须都是 `canon`，`outlineRefs` 指向的大纲必须都是 `approved-for-packet`。

Story Input Packet 是做故事时同步维护的美术参照交接面，不是故事写完后的补录表。字段来源规则：

| Packet 字段 | 故事侧需要回答的问题 | 主要来源 |
| --- | --- | --- |
| `worldPremise` | 这是个什么世界，玩家应该相信什么。 | `world` / `tone` canon |
| `coreEvent` | 当前单元发生了什么异常，让场景值得被表现。 | outline 主事件 / 关键 beat |
| `locationFunction` | 地点原本做什么，现在承担什么玩法功能。 | `location` canon / outline |
| `civilizationOrigin` | 谁建造或维护了它，材料和结构逻辑从哪来。 | `world` / `location` canon |
| `playerRole` | 玩家在视觉生产里是什么角色。 | `character_archetype` canon |
| `enemyOrigins` | 敌人来自什么规则事故、群体、机关或错误执行。 | `enemy_origin` canon / outline 冲突 |
| `powerRules` | 超自然力量如何稳定、失控、危险和可交互。 | `power_rule` canon |
| `symbolMeanings` | 颜色、材质状态、运动痕迹、可见标记、VFX 分别代表什么。 | `symbol` / `power_rule` canon |
| `toneTags` | mood、构图和表现强度被哪些词约束。 | `tone` canon |
| `visualDoNotUse` | 哪些视觉方向会违背故事或世界观。 | `forbidden_content` canon |

## Resource Manager Story Editor

Resource Manager 的 `故事设定` 工作区对外呈现为“故事设定编辑器”。它不是资源状态表，而是围绕 `Canon -> Outline -> Packet -> ArtSource` 的创作工作台：顶部只显示设定进度、当前阻塞和下一步动作；服务端 diagnostics 是最终权威，前端只做提前提示和分组展示。

编辑器保留三栏结构，并且每栏有独立选中状态：

- 左侧：世界观记忆库，按 canon fact 类型和状态查看事实。
- 中间：故事大纲编辑，逐条维护 beat flow，展示冲突、开放问题和引用的 canon facts。
- 右侧：输入包派生与审批，按故事核心、世界 / 地点、视觉约束、交接信息和审批阻塞分组，显示哪些 canon / outline 支撑 packet，并执行“批准给美术”。

选择 packet 时，编辑器会高亮它引用的 canon 和 outline；选择 outline 时会高亮它引用的 canon。这个联动只影响可视化，不改变三栏各自的 selected 状态。

受控写入 API：

- 保存 canon 调用 `POST /api/authoring/resources/story-canon/update`。
- 保存 outline 调用 `POST /api/authoring/resources/story-outlines/update`。
- 批准 outline 给输入包调用 `POST /api/authoring/resources/story-outlines/approve-for-packet`。
- 保存草稿调用 `POST /api/authoring/resources/story-packets/update`。
- 批准给美术调用 `POST /api/authoring/resources/story-packets/approve`。

所有写入都只落在 `StorySource/Projects/WGame`。Story 工作区不审批 ArtSource，不导入 Unity，不保存 Profile，不写 runtime Story SaveState。

## Authoring Provider

Provider id：`storySource`。

Provider 读取 `StorySource/Projects/WGame/Canon/canon_facts.json`、`StorySource/Projects/WGame/Outlines/story_outlines.json`、`StorySource/Projects/WGame/story_packets.json` 和 packet 文件。Browse 里仍以 StorySource items 展示事实源，不声明 runtime 可加载。collection metadata 包含 canon、outline、packet snapshot，供 Story 工作区渲染。

Packet metadata 包含：

- `storySourceId`
- `version`
- `approvalStatus`
- `targetConsumers`
- `canonRefs`
- `outlineRefs`
- `reviewStatus`
- `packetPath`
- `exportPath`
- `artSourceTaskId`

诊断码前缀是 `STORY_SOURCE_*`：

- `STORY_SOURCE_PACKETS_MISSING`
- `STORY_SOURCE_JSON_INVALID`
- `STORY_SOURCE_REQUIRED_FIELD_MISSING`
- `STORY_SOURCE_NOT_APPROVED`
- `STORY_SOURCE_VERSION_CONFLICT`
- `STORY_SOURCE_SUPERSEDED`
- `STORY_SOURCE_ART_REFERENCE_MISSING`
- `STORY_SOURCE_CANON_REF_MISSING`
- `STORY_SOURCE_CANON_REF_NOT_APPROVED`
- `STORY_SOURCE_OUTLINE_REF_MISSING`
- `STORY_SOURCE_OUTLINE_BEAT_INVALID`
- `STORY_SOURCE_PACKET_DERIVATION_BLOCKED`

## ArtSource Handoff

ArtSource 只消费批准后的 Story Input Packet，不直接读取 Canon 或 Outline。派生规则：

- `story.input_packet.approval` 找到匹配 `approved-for-art` packet 后可以解除阻塞。
- 参考图任务显示消费的 `storySourceId` 和 `version`。
- 如果 packet 没有 approved 状态，或者 derivation 被 canon/outline 阻断，ArtSource 只能显示 Story gate blocked。
- ArtSource 不能批准 Story，也不能在 Art 工作区补写正史。

## Phase 2 Notes

Runtime Story.Config handoff 放到第二阶段。等世界观、地点输入和 ArtSource 参考图稳定后，再从 StorySource Outline 导出 runtime story config 或 graph draft；现有 `Tools/MxFrameworkStoryAuthoring/story_authoring.py` 是后续导出基础，不在 StorySource v2 重做剧情 runtime。
