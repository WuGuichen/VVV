# MxFramework 接口索引

> 版本 0.3.1 | 2026-05-14
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
| Audio | `Docs/Interfaces/Audio.md` | `Assets/Scripts/MxFramework/Audio*/` | `Assets/Scripts/MxFramework/Tests/Audio/` |
| AI | `Docs/Interfaces/AI.md` | `Assets/Scripts/MxFramework/AI/` | `Assets/Scripts/MxFramework/Tests/AI/` |
| Diagnostics | `Docs/Interfaces/Diagnostics.md` | `Assets/Scripts/MxFramework/Diagnostics/` | `Assets/Scripts/MxFramework/Tests/Diagnostics/` |
| Runtime | `Docs/Interfaces/Runtime.md` | `Assets/Scripts/MxFramework/Runtime/` | `Assets/Scripts/MxFramework/Tests/Runtime/` |
| App / Scene Flow | `Docs/Interfaces/AppFlow.md` | `Assets/Scripts/MxFramework/Runtime*/` | `Assets/Scripts/MxFramework/Tests/Runtime/` |
| Input | `Docs/Interfaces/Input.md` | `Assets/Scripts/MxFramework/Input/` | `Assets/Scripts/MxFramework/Tests/Input/` |
| Gameplay | `Docs/Interfaces/Gameplay.md` | `Assets/Scripts/MxFramework/Gameplay/` | `Assets/Scripts/MxFramework/Tests/Ability/` |
| Combat | `Docs/Interfaces/Combat.md` | `Assets/Scripts/MxFramework/Combat/` | `Assets/Scripts/MxFramework/Tests/Combat/` |
| Editor | `Docs/Interfaces/Editor.md` | `Assets/Scripts/MxFramework/Editor/` | Unity Editor / MCP |

## 文档规则

- 每个模块页只写本模块公开接口、默认实现、使用约定和禁止事项。
- 模块页应给出测试入口，不复制整份源码。
- 公共 API 改动时，必须同步对应模块页和 `Docs/USAGE.md` 中的示例。
- 旧接口、计划中接口、未实现接口不得混进当前契约；计划内容放 `ROADMAP.md`。

## 跨模块依赖矩阵

`MxFramework.Input` 是 Unity 适配模块，依赖 Unity Input System；它不进入 noEngine 依赖矩阵，也不应被 Core / Runtime / Gameplay 等内层模块反向引用。游戏层、Demo 或本地多人组合根按需引用 `IInputProvider`。

```text
                    Core  Events  Attr  Modif  Buffs  AI    Config  Resources  Diag  Runtime  Gameplay
          Core      -     -       -     -      -      -     -       -          -     -        -
          Events    ✓     -       -     -      -      -     -       -          -     -        -
          Attr      ✓     ✓       -     -      -      -     -       -          -     -        -
          Buffs     ✓     ✓       ✓     -      -      -     -       -          -     -        -
          Modif     ✓     ✓       ✓     -      ✓*     -     -       -          -     -        -
          AI        ✓     -       -     -      -      -     -       -          -     -        -
          Config    ✓     -       -     -      -      -     -       -          -     -        -
          Resources ✓     -       -     -      -      -     -       -          -     -        -
          Diag      ✓     -       -     -      -      -     -       -          -     -        -
          Runtime   -     -       -     -      -      -     -       -          -     -        -
          Gameplay  ✓     ✓       ✓     ✓      ✓      -     -       -          -     -        -
          Editor    ✓     ✓       ✓     ✓      ✓      ✓     ✓       ✓          ✓     ✓        ✓

          ✓* = Modifiers → Buffs 只允许通过 IBuffPipeline 等接口访问。
```

## 总原则

- Runtime 不引用 `UnityEditor`。
- Framework 不引用 WGame、Entitas、Luban、CrashKonijn 或项目私有插件。
- 下层模块不引用上层模块。
- 跨模块协作优先通过接口、工厂、快照或组合根完成。
- Editor 只读取 Runtime 暴露的接口和 Debug Snapshot，不读取模块私有字段。
