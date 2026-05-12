# 外部主创编辑器规划

## 顶层目标

WGameFramework 的内容编辑体系最终要同时满足：

- **编辑友好**：主入口是外部桌面编辑器，中文 UI、向导式流程、字段说明、默认值、实时校验和一键导出。
- **AI 辅助友好**：AI 读取当前 Workflow Step、Schema、草稿、校验报告和预览日志，不要求扫描完整 Unity 项目源码。
- **实时预览**：编辑器生成临时 Patch，游戏运行时预览器热加载并回传 Buff 状态、伤害、属性变化、错误和性能数据。
- **Mod 模式和开发模式并存**：玩家安全创作，开发者能看到 Base/Patch/Mod、Schema、原始字段、资源索引和运行时诊断。

这不是单个 Unity Editor 面板能解决的问题。最终形态应是外部主创工具、Authoring Core、游戏运行时预览器和 Unity 桥接器协作。

## 产品形态

```text
External Authoring Editor
  面向玩家、策划、AI 协作的主创工具
  中文向导、Buff 编辑、实时校验、预览、导出 Mod

Authoring Core
  纯框架能力
  Schema、Workflow、Patch、Validator、Merger、Report、AI Context

Game Runtime Preview
  真实游戏运行时预览
  热加载 Patch、战斗沙盒、Buff 应用、日志和性能回传

Unity Bridge
  开发者桥接工具
  导出 Schema、Enum、资源索引、多语言索引，接 Play Mode 验证
```

Unity Editor 不是主创工具。Unity Editor 只保留桥接、导出、调试和开发者验证能力。

## 推荐实现路线

优先实现顺序：

1. **规划和协议稳定**：明确模式、流程、数据包、接口和验收标准。
2. **Authoring Core 可脱离 Unity**：把 Workflow、Schema、Patch、校验、合并和报告从 Unity UI 中抽出来。
3. **Buff 外部编辑器 MVP**：先做 WGame Buff 垂直切片，用向导式流程跑通创建、校验和导出。
4. **运行时预览闭环**：游戏端加载临时 Patch，编辑器接收预览日志。
5. **AI 协作闭环**：AI 只围绕当前步骤给建议、修复草稿、解释校验问题。
6. **Mod / 开发双模式完善**：同一套编辑器根据模式显示不同能力。

## 技术选型建议

### 外部主创编辑器

推荐：`Tauri + React/TypeScript`。

理由：

- 适合复杂向导、表格、节点、搜索、过滤、多语言和实时状态展示。
- 打包比 Electron 轻。
- AI 辅助生成 UI 和迭代效率高。
- 可通过 CLI / 本地服务调用 C# Authoring Core。

备选：`Avalonia + C#`。

适合全 C# 技术栈，但复杂 UI、节点视图和前端生态弱一些。

### Authoring Core

推荐：独立纯 C# `.NET Standard 2.1` 类库，源码位于 `Tools/MxFramework.Authoring/`。

职责：

- `AuthoringWorkflow` 和步骤上下文。
- Config Schema / Field / Enum / Reference。
- Patch / Mod / Debug 层。
- Validator。
- Runtime merge preview。
- Report bundle。
- AI context export。

Authoring Core 不引用 `UnityEngine`、`UnityEditor`、WGame 具体业务代码或真实项目数据。
外部编辑器和 CLI 直接引用构建产物 dll；Unity Bridge 通过 asmdef / asmref 或编译后 dll 引用同一份 Core，不复制逻辑。

### CLI / Local Service

推荐先做 CLI，后续可包成本地服务：

- CLI 适合 CI、AI agent、提交前检查。
- 外部编辑器可调用 CLI 完成校验、合并和导出。
- 后续如果实时交互频繁，再升级为本地服务或长连接进程。

### 游戏运行时预览

游戏端提供本机 WebSocket + JSON-RPC 2.0 Preview Server：

- 加载临时 Patch。
- 选择测试场景、测试角色、目标实体。
- 应用 Buff。
- 回传属性变化、Buff 状态、伤害 tick、错误、日志和性能。

运行时预览必须使用真实游戏逻辑，不在外部编辑器里手写一套战斗模拟。
默认只监听 `127.0.0.1`，端口由游戏预览器自动选择并写入连接描述文件，外部编辑器不要求玩家手动配置端口。

## 模式设计

用户能力拆成两个维度：

| 维度 | 取值 |
| --- | --- |
| 用户权限模式 | Mod Mode / Developer Mode |
| 接入面 | UI / CLI / AI / Unity Bridge |

AI 和 Tool 不是权限模式；它们必须运行在 Mod Mode 或 Developer Mode 上下文中。

| 权限模式 | 用户 | 能力 | 限制 |
| --- | --- | --- | --- |
| Mod Mode | 玩家 / Mod 作者 | 创建 Mod 层内容、使用安全模板、校验、预览、导出 Mod 包 | 不改 Base、不看源码、不执行项目私有脚本 |
| Developer Mode | 开发者 / 技术策划 | 查看 Base/Patch/Mod 合并、原始字段、Schema、资源索引、运行时日志、Unity 桥接 | 修改仍需校验和提交流程 |

## Buff MVP 范围

首个垂直切片只做 Buff，因为 Buff 同时覆盖配置、引用、表现、运行时状态、实时预览和 Mod 导出。

MVP 必须支持：

- WGame 风格 `BuffType` 选择。
- `BuffData` 公共字段。
- 堆叠和持续字段。
- 类型专属字段。
- 表现资源字段。
- 引用检查。
- 多语言。
- Patch / Mod 层。
- 校验报告。
- 运行时合并预览。
- 导出临时 Patch 和 Mod Patch。
- 通过游戏运行时预览应用 Buff。

MVP 不做：

- 完整 Ability 编辑器。
- 全量资源打包。
- 玩家上传平台。
- 多人协同编辑。
- 通用图编辑器。

## 数据交换

外部编辑器只依赖导出的项目工作包，不直接读取 Unity 项目源码。

项目工作包建议包含：

```text
ProjectAuthoringManifest/
  project.json
  schemas/
    buff.schema.json
    localization.schema.json
  enums/
    buff_type.json
    status_type.json
    attribute_type.json
  references/
    buff_index.json
    skill_index.json
    asset_index.json
  workflows/
    create_buff.workflow.json
  localization/
    zh_cn.json
    en_us.json
```

编辑输出建议包含：

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

Patch / Mod 包首版以 JSON 为唯一导出格式。大表编辑源可以是 TSV，但导出的 Mod Patch 必须归一为 JSON Patch 包。临时预览 Patch 和正式 Mod Patch 使用同一目录结构，区别只在 `mod.json.kind = "Preview" | "Mod"`。

Mod 包默认只包含数据描述，不包含可执行代码。任何条件 DSL 或数值公式都必须由 Authoring Core 白名单解释执行。

## 流程管理

每个开发需求必须按同一流程推进：

```text
需求定义
  -> 协议设计
  -> Core 实现
  -> CLI 验证
  -> UI 接入
  -> 运行时预览接入
  -> AI 上下文接入
  -> 文档和示例
  -> 验收
```

阶段门槛：

- 没有协议文档，不进入实现。
- 没有 Core API，不先做 UI。
- 没有 CLI 命令和自动化测试同时验证，不接复杂 UI。
- 没有运行时回传，不宣称实时预览完成。
- 没有报告包，不进入提交或 Mod 导出流程。

## 文档入口

- 大需求：`Docs/Tasks/AUTHORING_EDITOR_EPIC.md`
- 子需求 1：`Docs/Tasks/AUTHORING_EDITOR_01_CORE.md`
- 子需求 2：`Docs/Tasks/AUTHORING_EDITOR_02_BUFF_MVP.md`
- 子需求 3：`Docs/Tasks/AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md`
- 子需求 4：`Docs/Tasks/AUTHORING_EDITOR_04_AI_ASSIST.md`
- 子需求 5：`Docs/Tasks/AUTHORING_EDITOR_05_MOD_DEV_MODES.md`
- 子需求 6：`Docs/Tasks/AUTHORING_EDITOR_06_UNITY_BRIDGE.md`
