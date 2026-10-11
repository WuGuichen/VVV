# MxFramework Repository Rules

> 根 `AGENTS.md` 只记录无法从通用开发常识推断的项目不变量与文档路由；具体做法由对应权威文档维护。

本文件作用于整个仓库；路径上的子目录 `AGENTS.md` 逐层追加更具体的规则。

## 项目不变量

- Runtime 程序集边界不包含 `UnityEditor`；noEngine 模块边界不包含 `UnityEngine`。模块依赖以路径上的 `AGENTS.md` 和 `Docs/Interfaces/*.md` 为准。
- 外部 Authoring 工具、CLI 和 Authoring AI Assist 保持独立于 Unity Editor。
- Unity 序列化资产的写入权威是 Unity Editor、Unity MCP 或仓库生成器；正式资产与 `.meta` 共同构成版本化内容。
- 版本化内容限于任务源码、文档和正式资产；`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`.agent/`、工具缓存及个人状态属于本地工作状态。
- “AI”领域名称使用 **Runtime AI Planner**、**AIAction Config**、**Authoring AI Assist** 或 **Development Agent**；定义见 `Docs/INTERFACES.md`。

- 功能变更由同一owner将代码与相关权威说明同批交付；设计预期与已实现事实分开，文档证据及检查边界见[QUALITY_GATE](Docs/QUALITY_GATE.md#31-代码与权威说明同批交付)。

## 默认验证

- 普通 Unity C# 改动仅执行 Unity Editor `refresh/compile`、等待 Editor ready 并检查 Console error；除非用户明确要求或进入提交等专项流程，不追加 `.csproj` 构建、测试或其它检查。
- 上述验证优先单次调用 Unity MCP `execute_custom_tool(tool_name="validate_unity_scripts", parameters={})`；仅当返回证据同时确认 refresh completed、compilation started/finished、domain reload observed、Editor ready 且 Console error count 为 0 时才算通过。不得为此枚举 MCP resources；只有该工具不可用或明确失败时才回退到分步调用，并按实际证据报告结果。

## 任务路由

| 任务                                           | 权威入口                                                                                                                                |
| -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| C# 源码                                        | 路径上的 `AGENTS.md`、`Docs/API_STANDARDS.md` 与相关 `Docs/Interfaces/*.md` 的命中章节                                                           |
| 游戏功能、小游戏、Playable Demo、Runtime Showcase、场景验证 | `Docs/AGENT_GAME_CREATION_GUIDE.md` 与相关模块接口                                                                                         |
| 正式 player-facing UI、FairyGUI、UI Composer     | `Docs/Guides/FAIRYGUI_UI_COMPOSER_USER_GUIDE.md`、`FGUIProject/AGENTS.md`；Composer 权威或实现变更再进入 `Docs/FAIRYGUI_UI_COMPOSER.md` 与对应 ADR |
| Unity 场景、资源、Prefab、ScriptableObject、asmdef   | `Assets/AGENTS.md` 与相关模块接口                                                                                                          |
| 公共 API、schema、验收或发布                          | `Docs/QUALITY_GATE.md` 的命中章节                                                                                                        |
| 其它专项任务                                       | `Docs/PROJECT_INDEX.md` 中最直接命中的任务行                                                                                                  |

规则细节只在上表对应的权威入口维护，根文件不复制专项流程。
