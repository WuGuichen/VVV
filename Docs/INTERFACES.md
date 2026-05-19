# MxFramework 接口索引

> 版本 0.3.7 | 2026-05-18
>
> 本文件只做接口导航、跨模块规则和依赖矩阵。具体模块接口不要继续堆在这里，必须拆到 `Docs/Interfaces/`。

## 阅读顺序

1. `Docs/USAGE.md`：先看怎么接入。
2. 本文件：确认模块边界和依赖方向。
3. `Docs/Interfaces/<Module>.md`：查看具体模块接口。
4. `Assets/Scripts/MxFramework/Tests/<Module>/`：查看可运行样例。

## 模块接口文档

| 模块 | 文档 | 主要代码 | 测试入口 |
|------|------|----------|----------|
| Core | `Docs/Interfaces/Core.md` | `Assets/Scripts/MxFramework/Core/` | `Assets/Scripts/MxFramework/Tests/Core/` |
| Events | `Docs/Interfaces/Events.md` | `Assets/Scripts/MxFramework/Events/` | `Assets/Scripts/MxFramework/Tests/Events/` |
| Attributes | `Docs/Interfaces/Attributes.md` | `Assets/Scripts/MxFramework/Attributes/` | `Assets/Scripts/MxFramework/Tests/Attributes/` |
| Buffs | `Docs/Interfaces/Buffs.md` | `Assets/Scripts/MxFramework/Buffs/` | `Assets/Scripts/MxFramework/Tests/Buffs/` |
| Modifiers | `Docs/Interfaces/Modifiers.md` | `Assets/Scripts/MxFramework/Modifiers/` | `Assets/Scripts/MxFramework/Tests/Modifiers/` |
| Config | `Docs/Interfaces/Config.md` | `Assets/Scripts/MxFramework/Config*/` | `Assets/Scripts/MxFramework/Tests/Config/` |
| Resources | `Docs/Interfaces/Resources.md` | `Assets/Scripts/MxFramework/Resources/` | `Assets/Scripts/MxFramework/Tests/Resources/` |
| Animation | `Docs/Interfaces/Animation.md` | `Assets/Scripts/MxFramework/Animation*/` | `Assets/Scripts/MxFramework/Tests/Animation/` |
| Audio | `Docs/Interfaces/Audio.md` | `Assets/Scripts/MxFramework/Audio*/` | `Assets/Scripts/MxFramework/Tests/Audio/` |
| AI | `Docs/Interfaces/AI.md` | `Assets/Scripts/MxFramework/AI/` | `Assets/Scripts/MxFramework/Tests/AI/` |
| Diagnostics | `Docs/Interfaces/Diagnostics.md` | `Assets/Scripts/MxFramework/Diagnostics/` | `Assets/Scripts/MxFramework/Tests/Diagnostics/` |
| Debug UI | `Docs/Interfaces/DebugUI.md` | `Assets/Scripts/MxFramework/DebugUI*/` | `Assets/Scripts/MxFramework/Tests/DebugUI/` |
| Logging | `Docs/Interfaces/Logging.md` | `Assets/Scripts/MxFramework/Logging*/` | `Assets/Scripts/MxFramework/Tests/Logging/` |
| Runtime | `Docs/Interfaces/Runtime.md` | `Assets/Scripts/MxFramework/Runtime/` | `Assets/Scripts/MxFramework/Tests/Runtime/` |
| App / Scene Flow | `Docs/Interfaces/AppFlow.md` | `Assets/Scripts/MxFramework/Runtime*/` | `Assets/Scripts/MxFramework/Tests/Runtime/` |
| Input | `Docs/Interfaces/Input.md` | `Assets/Scripts/MxFramework/Input/` | `Assets/Scripts/MxFramework/Tests/Input/` |
| UI Toolkit | `Docs/Interfaces/UI.Toolkit.md` | `Assets/Scripts/MxFramework/UI.Toolkit/` | `Assets/Scripts/MxFramework/Tests/UI.Toolkit/` |
| Gameplay | `Docs/Interfaces/Gameplay.md` | `Assets/Scripts/MxFramework/Gameplay/` | `Assets/Scripts/MxFramework/Tests/Ability/`, `Assets/Scripts/MxFramework/Tests/Gameplay/` |
| Combat | `Docs/Interfaces/Combat.md` | `Assets/Scripts/MxFramework/Combat/` | `Assets/Scripts/MxFramework/Tests/Combat/` |
| Character Application | `Docs/Interfaces/CharacterApplication.md` | `Assets/Scripts/MxFramework/Character.Application/` | `Assets/Scripts/MxFramework/Tests/CharacterApplication/` |
| Character Control | `Docs/Interfaces/CharacterControl.md` | `Assets/Scripts/MxFramework/CharacterControl*/` | `Assets/Scripts/MxFramework/Tests/CharacterControl/` |
| Editor | `Docs/Interfaces/Editor.md` | `Assets/Scripts/MxFramework/Editor/` | Unity Editor / MCP |

## 文档规则

- 每个模块页只写本模块公开接口、默认实现、使用约定和禁止事项。
- 模块页应给出测试入口，不复制整份源码。
- 公共 API 改动时，必须同步对应模块页和 `Docs/USAGE.md` 中的示例。
- 旧接口、计划中接口、未实现接口不得混进当前契约；计划内容放 `ROADMAP.md`。

## 跨模块依赖矩阵

`MxFramework.Input` 是 Unity 适配模块，依赖 Unity Input System；它不进入 noEngine 依赖矩阵，也不应被 Core / Runtime / Gameplay 等内层模块反向引用。游戏层、Demo 或本地多人组合根按需引用 `IInputProvider`。

```text
                    Core  Events  Attr  Modif  Buffs  AI    Config  Resources  Anim  Diag  Logging  Runtime  Gameplay
          Core      -     -       -     -      -      -     -       -          -     -     -        -        -
          Events    ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Attr      ✓     ✓       -     -      -      -     -       -          -     -     -        -        -
          Buffs     ✓     ✓       ✓     -      -      -     -       -          -     -     -        -        -
          Modif     ✓     ✓       ✓     -      ✓*     -     -       -          -     -     -        -        -
          AI        ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Config    ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Resources ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Anim      -     -       -     -      -      -     -       ✓          -     -     -        -        -
          Diag      ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Logging   ✓     -       -     -      -      -     -       -          -     -     -        -        -
          Runtime   -     -       -     -      -      -     -       -          -     -     -        -        -
          Gameplay  ✓     ✓       ✓     ✓      ✓      -     -       -          -     -     -        -        -
          Editor    ✓     ✓       ✓     ✓      ✓      ✓     ✓       ✓          ✓     ✓     ✓        ✓        ✓

          ✓* = Modifiers → Buffs 只允许通过 IBuffPipeline 等接口访问。
```

`MxFramework.Logging.Diagnostics` 是 Diagnostics adapter，依赖 `MxFramework.Logging` + `MxFramework.Diagnostics`，不改变 Runtime 依赖矩阵。`MxFramework.DebugUI` 只依赖 Diagnostics / Core；跨 Runtime、Resources、Gameplay、Combat、CharacterControl、Config Runtime 的接入放在 Debug UI adapter 层或组合根中，保持被观察模块不反向依赖 Debug UI。`MxFramework.DebugUI.Input` 是可选 Unity/Input 桥，不进入 noEngine 依赖矩阵。Performance counters、Simulation Harness、hot reload observation 与 command gate diagnostics 归入观察 / 报告 API，默认不改变 Runtime authority、Replay、SaveState 或 hash。

`MxFramework.Character.Application` 是应用层配置契约，当前只依赖 `MxFramework.Config`。它保存 Character 聚合所需的静态表、typed id 和 schema 元数据，不拥有 Runtime / Gameplay / Combat / Resources / Animation / CharacterControl 的权威状态；后续 resolver 和 Runtime Spawn 也必须保持下层模块不反向依赖 Character Application。

## AI Terminology

> 本项目中的 "AI" 一词指代四种不同的概念。为避免混淆，所有文档、任务、接口和目录命名必须遵循以下四域区分：

| 域 | 推荐名称 | 含义 | 不要与什么混淆 |
|----|---------|------|--------------|
| **Runtime AI Planner** | 运行时规划器 | `MxFramework.AI` 模块：`AiWorldState`、`IAiGoal`、`IAiAction`、`IAiPlanner`、`SequentialPlanner`。游戏内 NPC 的轻量行为决策引擎。 | LLM、Agent 工作流、配置数据 |
| **AIAction Config** | AIAction 配置迁移 | 旧 WGame 的 AI 行为配置数据：`AIActionIndex`/`AIActionGraph`、`AIConfig`/`AIConfigDefense`/`AIGoals`、`TbCharacterAI` 以及 `TalentTree→AIAction` 多态引用。所有属于 Config 体系的 AI 数据迁移。 | 运行时 Planner 本身、LLM |
| **Authoring AI Assist** | 创作 AI 辅助 | `AiStepContext`、LLM 辅助编辑功能。读取当前编辑步骤上下文，解释字段、推荐值、生成修复建议。 | 游戏内 NPC AI 决策 |
| **Development Agent** | 开发 Agent | Gitea Issue Context Pack、分支/PR/审计、`Docs/USAGE.md §22` 中的 AI Agent 文档导航。 | Runtime AI 或 Authoring AI |

### 命名约束

1. **文档标题**禁止仅用 "AI" 作为独立名词。必须叠限定词：Runtime / AIAction / Authoring / Agent。
2. **任务命名**禁止 `AI_*` 裸前缀。必须带域：`RUNTIME_AI_PLANNER_*`、`AI_ACTION_MIGRATION_*`、`AUTHORING_AI_ASSIST_*`、`AGENT_WORKFLOW_*`。
3. **每篇涉及 AI 的文档**开头应标明''本文的 AI 指哪一域''。
4. 旧有任务文件名（如 `AI_ACTION_MIGRATION_PILOT_*`、`AUTHORING_EDITOR_04_AI_ASSIST`）保留，域已隐含在名称中，无需重命名。

## 总原则

- Runtime 不引用 `UnityEditor`。
- Framework 不引用 WGame、Entitas、Luban、CrashKonijn 或项目私有插件。
- 下层模块不引用上层模块。
- 跨模块协作优先通过接口、工厂、快照或组合根完成。
- Editor 只读取 Runtime 暴露的接口和 Debug Snapshot，不读取模块私有字段。
