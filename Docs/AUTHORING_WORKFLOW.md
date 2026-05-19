# Authoring Workflow 规范

## 目标

Authoring Workflow 是一套可移植的配置创作协议，不是 Unity Editor 专属界面。

同一份 Workflow 数据应能被以下前端消费：

- Unity Editor 内部工具。
- 外部 Mod Editor。
- AI Agent。
- 命令行校验和导出工具。
- 项目层自定义管理后台。

核心目标：

- **易用性**：让玩家、开发者、AI 都知道当前要做什么、下一步是什么。
- **容错率**：任何编辑先进入草稿或 Patch/Mod 层，不直接破坏 Base 数据。
- **性能**：编辑期可以严格和友好，运行时只消费已合并的只读快照。

## 基本原则

- Workflow 必须是数据描述，不能依赖 Unity API。
- Workflow 不要求加载完整项目源码。
- Workflow 不要求持有真实业务数据；真实数据由项目层 Adapter 或 Mod 包提供。
- QuickAction 是抽象动作，由不同前端解释执行。
- Player Mode 默认安全、简化、低风险。
- Developer Mode 暴露更多诊断、引用、Schema 和报告能力。
- AI 只处理当前 Step Context，不直接猜完整功能。
- 所有危险操作都必须可预览、可回滚、可报告。

## 角色模式

| 模式 | 目标用户 | 能做什么 | 不能做什么 |
| --- | --- | --- | --- |
| Player Mode | 玩家 / Mod 作者 | 创建 Mod 层配置、填写安全字段、运行校验、导出 Mod Patch | 修改 Base、访问源码、执行项目私有脚本 |
| Developer Mode | 开发者 / 技术策划 | 查看 Base/Patch/Mod 合并关系、诊断引用、导出报告、连接 Unity/AI | 仍不应直接绕过校验写入运行时 |
| AI Mode | AI agent | 读取当前步骤上下文、提出补全或修复建议、生成报告 | 不直接提交、不隐式修改 Base |
| Tool Mode | 自动工具 / CLI | 校验、合并、导出、生成 ChangeSet | 不做设计决策 |

## Workflow 数据结构

```json
{
  "workflowId": "create-buff.fire",
  "title": "创建 Buff：燃烧",
  "category": "Buff",
  "status": "InProgress",
  "mode": "Player",
  "target": {
    "source": "BasicBuffConfig",
    "rowId": 100001,
    "layer": "Mod"
  },
  "currentStepId": "basic-info",
  "steps": []
}
```

字段约定：

- `workflowId`：稳定 ID，允许外部工具引用。
- `title`：用户可见中文标题。
- `category`：Buff、Modifier、Ability、AI、ConfigFix 等。
- `status`：NotStarted、InProgress、Blocked、Ready、Completed。
- `mode`：Player、Developer、AI、Tool。
- `target`：目标源、行、字段、层。
- `steps`：流程节点列表。

## Step 数据结构

```json
{
  "stepId": "type-fields",
  "title": "填写类型专属字段",
  "status": "Blocked",
  "actor": "Human",
  "availableInPlayerMode": true,
  "requiresUnity": false,
  "requiresSourceCode": false,
  "requiresDeveloperMode": false,
  "description": "根据当前 BuffType 填写 NBuffData、CBuffData、StatusBuffData 等类型专属字段。",
  "inputs": ["BuffFactoryData.Type", "BuffData subtype payload"],
  "outputs": ["BuffData subtype payload"],
  "checks": ["BuffType is not None", "Buff data subtype matches BuffType"],
  "quickActions": [],
  "aiPromptHint": "只围绕当前 BuffType 的专属字段检查缺失、冲突和单位问题。"
}
```

字段约定：

- `actor`：Human、AI、Tool、Runtime。
- `availableInPlayerMode`：玩家模式是否可见。
- `requiresUnity`：是否必须 Unity Editor。
- `requiresSourceCode`：是否必须完整源码。
- `requiresDeveloperMode`：是否只允许开发者模式。
- `inputs` / `outputs`：必须使用可移植字段名或数据源 ID。
- `checks`：当前节点必须通过的校验。
- `quickActions`：抽象快捷动作。
- `aiPromptHint`：当前节点给 AI 的任务边界。

## QuickAction 数据结构

```json
{
  "kind": "OpenField",
  "label": "查看类型专属字段",
  "payload": {
    "source": "NBuffData",
    "field": "AttrID"
  }
}
```

允许的动作类型：

| ActionKind | 用途 |
| --- | --- |
| `OpenConfigSource` | 定位配置源 |
| `OpenField` | 定位字段 |
| `OpenReferenceTarget` | 打开引用目标 |
| `CopyAiContext` | 复制当前步骤 AI 上下文 |
| `RunValidation` | 运行当前范围校验 |
| `PreviewMergedResult` | 预览 Base/Patch/Mod 合并结果 |
| `ExportReport` | 导出报告 |
| `OpenDocument` | 打开规范或教程 |
| `ExportModPatch` | 导出 Mod Patch |

Unity Editor、外部 Mod Editor、CLI 可以分别解释这些动作。

## 易用性规范

- 默认从模板开始，不要求从空白创建。
- 玩家模式只显示安全字段和必要步骤。
- 字段显示优先用中文别名，同时保留原始字段 key。
- 每个 Step 必须写明当前目标、输入、输出和完成条件。
- 错误信息必须定位到源、行、字段和步骤。
- 快捷动作必须能直接跳到下一处需要处理的位置。
- AI 建议必须围绕当前步骤，不输出大段无关架构解释。

## 容错率规范

- 所有编辑先进入 Draft / Patch / Mod 层。
- 不允许玩家模式直接修改 Base。
- Remove 默认应表现为禁用或 Patch 删除，不做不可恢复物理删除。
- 保存前必须运行结构校验、引用校验、多语言校验和冲突校验。
- Mod 包失败时必须能跳过该 Mod，并保留错误报告。
- ChangeSet 必须清楚列出新增、替换、删除、Noop 和来源。
- 导出前必须有报告，报告应能被人和 AI 复查。

## 性能规范

- 运行时只消费 merged snapshot，不在游戏逻辑中反复合并。
- Patch/Mod 合并只在加载、热更或显式刷新时执行。
- ChangeSet 记录增量，不做全表深 diff 作为默认路径。
- 大表必须分页或虚拟列表显示。
- 外部 Mod Editor 只加载当前 Workflow 所需 Schema、Enum、Localization 摘要和目标数据。
- 校验分层：编辑期深校验，运行时快速校验。
- AI 上下文必须裁剪到当前 workflow / step / target，避免全项目扫描。

## Buff 创建流程模板

Buff 编辑器的用户操作入口必须贴近 WGame 的 `BuffType` 分类，同时底层仍保持 `AuthoringWorkflow` 的可移植协议。详细操作逻辑见 `Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md`。

```text
确定 Buff 设计目标
  -> 选择 Buff 类型
  -> 填写公共字段
  -> 配置目标、堆叠和持续
  -> 填写类型专属字段
  -> 配置表现资源
  -> 检查引用关系
  -> 填写多语言
  -> 选择 Patch / Mod 层
  -> 运行校验
  -> 预览 merged result
  -> 导出报告 / Mod Patch
```

推荐步骤：

| Step | Actor | Player | Unity | Source | 目标 |
| --- | --- | --- | --- | --- | --- |
| 设计目标 | Human / AI | 是 | 否 | 否 | 明确玩法目的并推荐 BuffType |
| Buff 类型 | Human | 是 | 否 | 否 | 选择 Numerical、Condition、Status 等类型 |
| 公共字段 | Human / AI | 是 | 否 | 否 | 填 ID、Name、Desc、图标和可移除 |
| 堆叠与持续 | Human / AI | 是 | 否 | 否 | 填 Target、AddType、AddNum、Duration、HitCooldown |
| 类型专属字段 | Human / AI | 是 | 否 | 否 | 填当前 BuffType 对应的数据类字段 |
| 表现资源 | Human | 是 | 否 | 否 | 填特效、部位、缩放、命中特效等 |
| 引用检查 | Tool | 是 | 否 | 否 | 检查属性、状态、条件、技能、Buff、资源引用 |
| 多语言 | Human / AI | 是 | 否 | 否 | 填中文、英文文本 |
| Patch / Mod 层 | Human | 是 | 否 | 否 | 选择保存到哪个 Mod 包 |
| 校验 | Tool | 是 | 否 | 否 | 校验字段、引用、多语言、冲突 |
| 合并预览 | Tool | 是 | 否 | 否 | 预览 Base + Mod 后结果 |
| 导出 | Tool | 是 | 否 | 否 | 生成 Mod Patch 和报告 |

开发者模式可以额外显示：

- Base/Patch/Mod 来源。
- Schema 原始字段。
- 引用图。
- ChangeSet。
- 性能统计。
- Unity 运行时预览。

## 角色资源包流程模板

Character Resource Package 是资源聚合型内容，不走表格 Patch 首屏。外部 3D 装配编辑器和 CLI 都必须围绕同一个目录包工作：

```text
打开 Character Resource Package
  -> 校验 manifest / resource_catalog
  -> 校验 package-local ResourceKey、stable id、relative path、source format
  -> 生成 dependency graph 和 content/import/dependency hash report
  -> 编辑 geometry、collider、socket、weapon attachment、trace
  -> 调用 CharacterAuthoringCompiler 生成 config patch、geometry binding、resource mapping、write plan、gate report、resolver verification plan
  -> 检查 ExportBlocked / ImportBlocked / SpawnBlocked / WarningOnly
  -> 保存 package
  -> 可选调用 Unity Importer Bridge 导入项目 ResourceCatalog
```

该流程不要求 Unity 先导出模型或骨骼工作包。Unity Importer Bridge 只消费同一份 package，并在导入后生成项目 ResourceCatalog 映射；外部编辑器不能直接写 Unity asset GUID、Prefab、`AnimationClip`、Material 或绝对路径。

角色工作流的预检入口是：

```bash
mx-authoring character compile --package <CharacterPackage> --out <dir> --check-files --check-hashes
```

外部编辑器可以保存 draft，但只有 compiler 未返回 `ExportBlocked` 时才能保存为可导入 / 可分发产物；只有未返回 `ImportBlocked` 时 Unity Importer Bridge 才能写项目；只有未返回 `SpawnBlocked` 时 Runtime Spawn 才能生成实例。

## 验收标准

一个 Workflow 功能只有同时满足以下条件，才算可以进入实现：

- 玩家模式能在不打开 Unity、不加载源码的情况下理解流程。
- 开发者模式能看到足够诊断信息。
- 每个步骤都有明确输入、输出、完成条件和失败恢复路径。
- 所有快捷动作都是抽象动作，不绑定某一个 UI。
- 运行时最终只读取合并后的只读配置。
- 失败不会破坏 Base，也不会阻止游戏在禁用坏 Mod 后继续启动。
- AI 上下文可以从当前 Step 独立生成。

## 后续实现顺序

1. 定义 Authoring Workflow runtime-agnostic 数据模型。✅ 已实现：`AuthoringWorkflow` / `AuthoringWorkflowStep` / `AuthoringQuickAction`。
2. 提供 Buff Workflow Demo 模板。✅ 已实现：`BuffAuthoringWorkflowTemplate.CreateCreateBuffWorkflow(...)`。
3. 外部 Authoring Editor 读取同一份 Workflow / Schema / Patch 数据并呈现流程。✅ 已转向外部编辑器。
4. 接入当前 Config Workbench 的源、字段、引用跳转。
5. 接入 Buff Patch / Mod merged preview。

## Unity Editor 边界

Unity Editor 内部不再提供 `编辑模式 > 创作流程` 页。创作流程 UI 统一放到外部 Authoring Editor 中，Unity 侧只保留导出、桥接、运行时连接状态和开发者诊断能力。

## 当前 Buff 模板范围

`BuffAuthoringWorkflowTemplate` 已提供创建 Buff 的基础流程模板：

- 支持多条流程同时存在，每条流程有独立 `workflowId`、目标 Buff Id、模式和层。
- 默认面向 Player Mode，步骤不依赖 Unity、不依赖完整源码。
- Buff 类型、公共字段、堆叠持续、类型专属字段、表现资源和引用检查都有独立步骤。
- 校验、合并预览和导出步骤只定义抽象动作，由 Unity Editor、外部 Mod Editor 或 CLI 分别解释。
- 每个步骤都能独立生成 AI 上下文，避免 AI 扫描完整项目。

后续接入真实编辑能力时，必须继续保持“工作流协议独立、前端解释动作、运行时只读快照”的边界。
