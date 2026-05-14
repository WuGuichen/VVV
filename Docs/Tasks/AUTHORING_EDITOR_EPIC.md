# 大开发需求：外部主创编辑器体系

> **本文涉及两个 AI 域**：
> - **Authoring AI Assist**（所有"AI 辅助"、"AI 建议"、"AI 上下文"相关描述）—— LLM 辅助编辑配置数据的功能
> - **Development Agent**（"AI agent"、"CI 和 AI agent"相关描述）—— Gitea Issue / 分支 / PR 工作流中的自动化代理
> 
> 不包含 Runtime AI Planner（游戏内 NPC 决策引擎）或 AIAction Config（WGame 配置数据迁移）。
> 四域定义见 `Docs/INTERFACES.md` 的 AI Terminology 章节。

> **状态**: 🟢 持续推进中（子需求清单见下方）

## 背景

WGameFramework 后续不应把主创作能力绑定在 Unity Editor 中。目标是建立一套可被玩家、策划、开发者和 AI 共同使用的外部主创编辑器体系。

这个体系必须同时支持：

- 编辑友好。
- AI 辅助友好。
- 真实运行时实时预览。
- Mod Mode 和 Developer Mode 并存。

## 目标

建立一套从内容编辑到运行时预览再到 Mod 导出的完整闭环：

```text
外部编辑器编辑草稿
  -> Authoring Core 校验
  -> 生成临时 Patch
  -> 游戏运行时预览器热加载
  -> 回传运行结果
  -> AI / 人类修正
  -> 导出 Mod Patch 或开发提交材料
```

## 非目标

- 不让 Unity Editor 成为主创工具。
- 不让玩家或 AI 依赖完整 Unity 工程源码。
- 不在外部编辑器里重写战斗模拟。
- 不把 WGame 真实业务数据提交进 WGameFramework 主干。
- 不一次性做完所有配置类型，先做 Buff 垂直切片。

## 规模假设

首版设计按以下规模优化：

| 项 | 目标规模 |
| --- | --- |
| 配置总行数 | <= 10,000 |
| Buff 数量 | <= 1,000 |
| Schema 数量 | <= 100 |
| 单个 Mod Patch 行数 | <= 500 |
| 单个 Project Authoring Manifest | <= 100 MB |

超出规模时允许 UI 降级为分页、懒加载或只显示摘要，但不应崩溃，也不应阻塞校验报告导出。

## 总体架构

| 模块 | 职责 |
| --- | --- |
| External Authoring Editor | 外部桌面主创工具，负责向导、表单、预览面板、AI 入口和导出 |
| Authoring Core | 纯 C# 逻辑层，负责 Workflow、Schema、Patch、Validate、Merge、Report |
| Authoring CLI | 自动化入口，给编辑器、CI 和 AI agent 调用 |
| Game Runtime Preview | 真实游戏预览，负责热加载 Patch、沙盒测试和运行时结果回传 |
| Unity Bridge | 开发者桥接，负责导出 Schema、Enum、资源索引和 Play Mode 验证 |

### Authoring Core 物理形态

Authoring Core 以独立纯 C# `.NET Standard 2.1` 类库存在，源码建议放在 `Tools/MxFramework.Authoring/`：

- 外部编辑器和 CLI 直接引用构建产物 dll。
- Unity Bridge 通过 asmdef / asmref 或编译后 dll 引用同一份 Core，不复制逻辑。
- Core 项目必须同时提供 `.csproj` 和 Unity 可消费的程序集描述。
- Core 的 `.csproj` 不允许引用 `UnityEngine`、`UnityEditor`、WGame 项目程序集或项目真实业务数据。
- Unity 侧 asmdef 必须把 Authoring Core 放在非 Editor-only Runtime 可读层，Editor 桥接层只能向上引用，不允许 Core 反向引用 Editor。
- CI / 提交前检查必须包含一次 `dotnet build` 或等效编译验证，确保 Core 可以脱离 Unity 编译。

如果后续需要分发给外部工具，优先以本地 dll / NuGet 包形式发布；源码同步只作为开发期便利，不作为长期分发契约。

### Patch / Mod 包格式

包格式先定为目录优先、zip 只是同结构压缩包：

```text
ModPackage/
  mod.json
  patches/
    buff.patch.json
    localization.patch.json
  reports/
    validation_report.json
    validation_report.txt
    merge_preview.json
  preview/
    last_runtime_preview.json
```

硬性要求：

- `mod.json` 必须包含 `packageId`、`displayName`、`author`、`version`、`schemaVersion`、`gameVersionRange`。
- Patch 文件使用 JSON，首版不混用 TSV 作为 Patch 存储格式。
- 大表编辑源可以是 TSV，但导出的 Mod Patch 必须归一为 JSON Patch 包。
- 临时预览 Patch 和正式 Mod Patch 使用同一结构，区别只在 `mod.json.kind = "Preview" | "Mod"`。
- Report bundle 必须随包生成，便于人和 AI 复查。
- Demo 样例的 `reports/`、`preview/` 不入版本控制；真实 Mod 包发布时由作者自行决定是否随包。

### 预览通信协议

运行时预览首版协议定为本机 WebSocket + JSON-RPC 2.0：

- 默认只监听 `127.0.0.1`。
- 端口由游戏预览器自动选择并写入**连接描述文件**，外部编辑器不要求玩家手填端口。
- 消息体使用 JSON-RPC 2.0，payload 复用 Authoring Core 的 Patch、Report 和 Preview Result 数据结构。
- CLI 可以通过同一连接描述文件发送 `loadPatch`、`applyBuff`、`reset`、`getSnapshot`、`getLogs`。
- 客户端连接后必须先发送 `preview.handshake`，未握手前其它方法返回 `1001`；token 不匹配返回 `1002`。
- 预览连接失败时，编辑器必须保留离线校验和合并预览能力。

权威 Spec、连接描述文件路径、JSON-RPC 方法 / 错误码 / `RuntimePreviewResult` 结构均见 `AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md`。

### 安全策略

- Mod 包默认只包含数据描述，不包含可执行代码。
- Mod Mode 不允许执行项目私有脚本。
- 如后续允许条件 DSL 或数值公式，必须由 Authoring Core 白名单解释执行，禁止反射、文件系统访问、网络访问和任意代码执行。
- Mod 可以引用 Base 中公开的 Buff、技能、资源和多语言 key，但不能引用未导出到 Manifest 的内部对象。
- 资源路径必须通过 Manifest 白名单校验；不允许任意绝对路径。
- 预览器加载失败的 Mod 必须可被跳过，不能阻止游戏启动。

## 用户模式

用户能力拆成两个维度，不做四套权限系统。

| 维度 | 取值 | 说明 |
| --- | --- | --- |
| 用户权限模式 | Mod Mode / Developer Mode | 决定可见字段、可写层、危险操作和报告详细程度 |
| 接入面 | UI / CLI / AI / Unity Bridge | 决定操作入口，但必须服从当前权限模式 |

权限模式：

| 模式 | 用户 | 默认能力 | 限制 |
| --- | --- | --- | --- |
| Mod Mode | 玩家 / Mod 作者 | 安全模板、Mod 层写入、实时校验、运行时预览、导出 Mod 包 | 不改 Base、不看源码、不执行项目私有脚本 |
| Developer Mode | 开发者 / 技术策划 | 原始字段、Schema、Base/Patch/Mod 合并、资源索引、Unity 桥接、提交材料 | 修改仍需校验和提交流程 |

接入面：

| 接入面 | 用途 |
| --- | --- |
| UI | 外部主创编辑器的人类操作入口 |
| CLI | CI、批处理、提交前检查和自动化入口 |
| AI | 读取当前步骤上下文、草稿、报告和预览日志，输出建议 |
| Unity Bridge | 导出项目 Manifest、资源索引和开发者验证 |

AI 和 Tool 不是权限模式；它们必须运行在 Mod Mode 或 Developer Mode 上下文中。

## 分阶段需求

1. `AUTHORING_EDITOR_01_CORE.md`：Authoring Core / CLI 基础。
2. `AUTHORING_EDITOR_02_BUFF_MVP.md`：Buff 外部编辑器 MVP。
3. `AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md`：游戏运行时实时预览。
4. `AUTHORING_EDITOR_04_AI_ASSIST.md`：AI 辅助闭环。
5. `AUTHORING_EDITOR_05_MOD_DEV_MODES.md`：Mod / Developer 双模式。
6. `AUTHORING_EDITOR_06_UNITY_BRIDGE.md`：Unity 桥接器。
7. `AUTHORING_EDITOR_07_SCENE_PREVIEW.md`：测试场景与测试角色预览。

## 依赖关系

```text
01 Core/CLI ──┬─> 02 Buff MVP ──┬─> 03 Runtime Preview ──> 07 Scene Preview
              │                 └─> 04 AI Assist
              ├─> 05 Mod/Dev Modes
              └─> 06 Unity Bridge ──> 03 Runtime Preview
```

## 里程碑

| 里程碑 | 目标 | 关键产物 |
| --- | --- | --- |
| M1 | Core / CLI Spec Ready + Patch 格式定稿 | Core 项目形态、Patch 包结构、CLI 命令规格 |
| M2 | Core Tool Verified | CLI 跑通 validate / merge-preview，自动化测试覆盖合并和校验 |
| M3 | Buff MVP UI Skeleton | 外部编辑器本地草稿和向导骨架，不联游戏 |
| M4 | Runtime Preview 协议 Spec Ready | WebSocket + JSON-RPC 2.0 方法、连接描述文件和 Preview Result |
| M5 | Buff MVP + Runtime Preview 闭环 | Buff 草稿生成临时 Patch，游戏预览器热加载并回传结果 |
| M6 | Scene Target Preview | 测试场景、测试角色、场景对象 Buff 应用和可视反馈 |
| M7 | AI Assist + Mod / Developer Modes | 步骤级 AI 建议、权限模式、字段显示和导出限制 |
| M8 | Unity Bridge 完整导出 | Project Authoring Manifest、Schema、Enum、资源、多语言和引用索引 |

## 流程管理

每个子需求使用同一状态流：

```text
Draft
  -> Spec Ready
  -> Core Ready
  -> Tool Verified
  -> UI Integrated
  -> Preview Verified
  -> Docs Ready
  -> Done
```

状态含义：

| 状态 | 含义 |
| --- | --- |
| Draft | 需求草案，允许大幅调整 |
| Spec Ready | 输入输出、接口、数据格式和验收标准已明确 |
| Core Ready | 纯逻辑层已实现，不依赖 UI |
| Tool Verified | CLI 命令路径打通，且自动化测试覆盖核心合并/校验能力 |
| UI Integrated | 外部编辑器或 Unity 桥接已接入 |
| Preview Verified | 如涉及运行时，游戏端预览闭环已可用 |
| Docs Ready | 使用文档、AI 上下文和报告样例齐全 |
| Done | 验收通过，可进入下一阶段 |

## 兼容性

- 每个 Project Authoring Manifest、Mod Package 和 Patch 文件都必须带 `schemaVersion`。
- 字段重命名必须通过 migration map 或 alias 保持旧 Patch 可读。
- 字段废弃不得立即删除，至少保留一个兼容窗口，并在报告中标记 deprecated。
- Validator 必须能识别旧 Patch 的 schemaVersion，并输出升级建议。
- 大需求验收必须包含一个旧版本 Mod Patch 加载用例。

## 多语言所有权

- 外部编辑器可以在 Mod 包中写入多语言 Patch。
- Mod Mode 只能新增或覆盖自己 Mod 命名空间下的 localization key。
- Developer Mode 可以生成项目提交材料，但仍不能直接绕过校验写 Base。
- `LocalizedTextKey` 是运行时引用身份，具体语言文本由 localization patch 或项目 localization manifest 提供。

## 验收标准

大需求完成必须满足：

- 普通用户能在外部编辑器中创建一个 Buff，不打开 Unity。指标：干净 Windows 环境安装后，10 分钟新手任务可产出一个能被预览器加载的 Mod 包；安装包目标 <= 200 MB。
- 编辑器能实时提示字段、引用、多语言和冲突问题。指标：单字段编辑到错误高亮 <= 200 ms；全量 Buff 校验 <= 2 s。
- 游戏运行时能加载临时 Patch 并回传预览结果。指标：本机预览连接免手动端口配置；加载临时 Patch 到收到首个结构化结果 <= 3 s。
- AI 能基于当前步骤上下文提出可执行建议。指标：AI 上下文包不超过当前 workflow / step / target 所需范围，建议必须能转成可审查草稿变更。
- Mod Mode 不能修改 Base 或访问危险字段。指标：CLI、UI、AI 三个接入面都无法生成 Base 写入 Patch。
- Developer Mode 能查看原始字段、Schema、合并结果和提交材料。指标：能导出包含 Schema 版本、ChangeSet、校验报告和提交前摘要的 report bundle。
- 同一套 Workflow / Schema 可被外部编辑器、CLI、Unity Bridge 和 AI 复用。指标：四个接入面引用同一个 Authoring Core 版本号，不存在 fork 版 Schema。
- 旧 Mod 包能向前兼容加载。指标：至少一个旧 `schemaVersion` 的 Buff Patch 可被 Validator 识别并给出升级或兼容报告。

## 当前状态

状态：`Spec Ready (pending sub-tasks)`

已完成基础：

- `AuthoringWorkflow` 数据模型。
- WGame Buff 风格创作流水线规范。
- Unity Framework Manager 只读流程验证入口。
- Authoring Core / CLI v0 骨架，位于 `Tools/MxFramework.Authoring/`。
- Project Authoring Manifest / Enum / Reference / Localization 样例。
- Report bundle 写文件能力。
- Buff 外部编辑器 UI Skeleton，位于 `Tools/MxFramework.Authoring.Editor/`。
- Buff 外部编辑器本地编辑闭环：编辑示例 Patch、保存 Patch、重新生成 report bundle、刷新校验和合并预览。
- Buff Schema 已支持字段分组、单位和按 BuffType 可见的类型专属字段。
- Buff UI 已能按当前 BuffType 动态显示字段组，并对当前类型的必填字段做即时提示。
- CLI `workflow context` / `workflow ai-context` 按 `--workflow` 路由内置 workflow 集合。
- `PatchEntry.Fields` 升级为 `Dictionary<string, FieldValue>`，支持 Scalar / List / Map 嵌套并向后兼容旧字符串字段。
- 新增 `ManifestAwareValidator`，覆盖 Reference / Enum / LocalizedText / AssetPath 校验。
- 新增 `LayeredMerger`，支持 Base→Patch→Mod 三层合并并标注 `OriginLayer` / `FieldOrigins`。
- CLI 退出码契约：0/1/2/3。
- `AuthoringSchemaVersions` 已知集合 + Validator 对未知/前向版本输出 Warning + `RequiresUpgrade`。
- `Workflow.CreateAiStepContext` + CLI `workflow ai-context` + EditorServer `/api/ai-context` 输出 `schemaSlice / draftSlice / validationIssues`。
- EditorServer 静态文件比较改为 `OrdinalIgnoreCase`，所有响应附带 `X-Mx-Authoring: 1` header（仅 127.0.0.1）。
- 新增 `samples/buff-mod/` Mod 样例，与 `samples/buff-preview/` 平行。
- `AuthoringValidate.Run` 统一校验入口；`PackageValidator` 解耦 BuiltIn Buff schema，必填字段改由 manifest 推导。
- `EditorServer` 包路径参数化 + `/api/packages` 列表 + UI 顶部包下拉切换；所有 API 走 `?package=` 并校验路径不跳出 root。
- `EditorServer` `/api/validate-draft` 实时校验入口 + UI debounce 300ms 草稿校验。
- AI Step Context 增加 `enumSlice` / `referenceSummary` / `allowedActions`；Mod / Developer 模式动作集分流。
- CLI 新增 `precommit` 命令，写 `precommit.txt` 并复用 0/2/3 退出码。
- `SchemaField.IsList` + Reference 多值规范化（List 与逗号串均可，单值不再误拆）。
- 新增 `Tools/MxFramework.Authoring/.gitignore`，`samples/*/reports/`、`samples/*/preview/` 不再入仓。
- Unity 侧 PreviewRpcServer + DummyPreviewWorld + 连接描述写入 + Editor 启动菜单（`Assets/Scripts/MxFramework/Preview/`，asmdef `MxFramework.Preview.Runtime` / `MxFramework.Preview.Editor`）。
- Authoring 端 RPC 客户端：`MxFramework.Authoring.Preview`（netstandard2.1 DTO + `PreviewConnectionLocator`）和 `MxFramework.Authoring.Preview.NetClient`（net8.0 WebSocket Client + 6 个 JSON-RPC 方法 + 错误码异常分类）。
- 外部编辑器 Runtime Preview UI/API：`/api/preview/status`、`/api/preview/run` 和运行时预览结果面板。
- 测试端 Mock Preview Server（`MockPreviewServer.cs`）+ 6 项预览测试（locator round-trip、stale process、handshake gating、loadPatch、token mismatch、full flow）。
- Authoring CLI 新增 `preview ping/load/apply/reset/snapshot/logs` 子命令；退出码扩展 `4 = preview unavailable`。
- Buff 外部编辑器 UI 收尾：创建 Buff 向导（BuffType / Id 去重 / 自动建议 Name key / 默认必填字段）、Mod / Developer 模式切换（Mod 隐藏 `DamageBaseTypeID` 并强制 Layer=Mod；Developer 显示 Layer 下拉，禁 Base）、多语言可写编辑区（项目内置只读 + 包内追加 Patch，写入 `samples/<package>/patches/localization.patch.json`，Mod 模式强制 `mod.{packageId}.` 前缀）。
- EditorServer 新增 `GET/POST /api/localization` 端点 + `/api/ai-context?mode=` 透传，AI 上下文文本顶部追加 `uiMode=`。

下一步：

1. 将 `Values`、条件列表和弹道参数继续拆成结构化子编辑器（利用新 FieldValue Map / List）。
2. 实施 03.5 闭环验收：按 `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md` 接入真实 `IBuffFactory` / Runtime config resolver / `GameplayDiagnosticSnapshot`，让 `applyBuff` 回传真实 Buff 状态、属性变化、日志和错误。
3. 完成 AI Assist 建议回写闭环（步骤上下文已就绪）。
4. 实现 Mod / Developer 双模式权限 UI（含创建 Buff 向导）。
5. Unity Bridge 导出真实 Schema / Reference / Localization 索引；WGame demo 命名空间整改。

## CLI 退出码契约

外部编辑器、CI 和 AI agent 必须按以下退出码处理 `authoring` CLI 结果：

| Exit | 含义 |
| --- | --- |
| 0 | ready：命令成功，校验通过 |
| 1 | 工具错误：参数缺失、IO 异常、未知 workflow id 等 |
| 2 | 校验阻断：Validator `HasErrors == true` |
| 3 | schemaVersion 不兼容：`RequiresUpgrade == true`，需要先升级再继续 |
| 4 | preview unavailable：未握手 / 连接描述缺失 / token 不匹配 |

所有 `validate / report / workflow ai-context` 等命令都遵循此契约（参见 `AuthoringExitCodes`）。

`precommit` 命令同样遵循上表。

## EditorServer 安全约束

- 仅监听 `127.0.0.1`，禁止改成 `0.0.0.0` 或公网地址。
- 不得在公网或共享机器上裸跑；用作本机外部主创工具与 UI 之间的桥接。
- 所有响应附带 `X-Mx-Authoring: 1` header，UI 可据此判断远端是否真的是本工具。
