# WGameFramework Project Validation Guide

本层约束 package root 外的 WGameFramework repository-validation 源码。它可以消费 MxFramework package assembly，但不得成为任何 package assembly 的依赖。

## Owner boundary

- `CharacterTest/` 是正式 repository validation game，不随 `com.vincent.wgameframework` 安装。
- `MxFramework.Game.CharacterTest`、`MxFramework.Game.CharacterTest.Editor`、`MxFramework.Tests` 和 `MxFramework.Tests.PlayMode.CharacterTest` 的 assembly name、namespace、GUID 和 friend identity 保持稳定。
- 依赖方向只允许 `WGameFrameworkProject -> MxFramework package`；任何 `MxFramework package -> WGameFrameworkProject` 引用都是阻断错误。
- CharacterTest 使用的 `Assets/Scenes/`、`Assets/Config/`、`Assets/StreamingAssets/`、`Assets/MxFrameworkGenerated/`、`FGUIProject/` 和 `Tools/` 仍由 repository owner 管理，不复制进 package。

## Layout

| Path | Responsibility |
| --- | --- |
| `CharacterTest/` | CharacterTest Runtime/composition/presentation and the `MxFramework.Game.CharacterTest` asmdef |
| `CharacterTest/Editor/` | CharacterTest authoring, validation and `MxFramework.Game.CharacterTest.Editor` |
| `CharacterTest/Tests/EditMode/` | Existing `MxFramework.Tests` aggregate repository-validation assembly until UPM-P3 splits package contracts |
| `CharacterTest/Tests/PlayMode/` | CharacterTest-specific PlayMode assembly and fixtures |
| `Demos/` | Repository showcase and playable Demo assemblies extracted during UPM-P4; existing assembly identities remain stable |
| `Editor/` | Project-only authoring, import processors, CharacterTest VFX builders and repository scene/sample tooling extracted during UPM-P5 |
| `Tests/Repository/` | Framework repository-validation tests extracted from the UPM package during UPM-P3; assembly names and GUIDs remain stable |

新增 repository-validation 测试必须进入 `Tests/Repository/` 或命中的 CharacterTest test owner；只有自包含的最小 package contract tests 可以进入 package `Tests/Editor` / `Tests/Runtime`，不得把项目回归重新放回 package。

## Change rules

- CharacterTest gameplay/combat/UI/AI/authoring 改动继续读取相关 `Docs/Interfaces/*.md`、`Docs/API_STANDARDS.md` 和 `Docs/QUALITY_GATE.md`。
- Unity 序列化资产由 Unity Editor、Unity MCP 或仓库生成器写入；资产与 `.meta` 成对提交。
- Runtime assembly 不引用 `UnityEditor`；Editor/test assembly 不把 Editor 类型泄漏到 Runtime API。
- `Editor/` implementation may consume package Editor contracts, but package assemblies must never reference the project-only assemblies added here.
- 路径迁移不授权改 public API、schema、deterministic semantics 或测试断言。
- `MxFramework.Tests` aggregate 仍是 repository-validation；UPM-P3 选择最小 package tests 前，不通过删除测试缩小范围。

## Validation

- 普通 C# 改动按根 `AGENTS.md` 执行 Unity refresh/compile、Editor ready 和 Console error 检查。
- CharacterTest 改动运行命中模块的 focused EditMode/PlayMode tests；正式视觉/场景声明必须有图形 Editor 或 Playable 证据。
- UPM surface 变化同时运行 strict E0、Local Consumer 和 package -> project reverse-dependency 检查。
