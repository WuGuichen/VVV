# Resource Management Runtime Resource Migration 01

> 状态：Implemented
> 日期：2026-05-10
> 范围：替换 Demo / 测试场景中已具备资源系统接入条件的直接 `Resources.Load`

## 目标

资源系统进入可用状态后，Demo 和测试场景不应继续在运行时脚本里直接拼 Unity `Resources` 路径加载 UI、材质、prefab 等资源。

本任务先迁移最容易验证的外层运行时展示资源：

- Runtime HUD 默认 `PanelSettings`。
- Runtime HUD 默认 `GameplayShowcase.uxml`。
- Runtime HUD 默认 `GameplayShowcase.uss`。
- Combat Showcase 的 debug material。

## 实现

- `MxRuntimeHudController` 不再通过 `ResourcesProvider` 加载默认 UI 资源，改为由 Runner、场景 bootstrap 或测试显式注入 UI Toolkit 资产。
- `RuntimeCombatShowcaseRunner` 不再加载 `Resources` 中的 trace material，改为运行时生成 debug material。
- `UnityResourceTypeResolver` 增加 UI Toolkit 和常用 Unity 类型解析：`Material`、`PanelSettings`、`VisualTreeAsset`、`StyleSheet`、`Font`。
- `MxFramework.UI.Toolkit` 移除对 `MxFramework.Resources`、`MxFramework.Resources.Unity` 的依赖，避免 UI 模块隐式回退到 `Resources`。
- Combat HUD 测试改为从 `Assets/UI/MxFramework/Showcase` 加载测试资产并注入 HUD。

## 约束

- 这次只改 Demo / UI Toolkit 外层组合，不让 `Core`、`Gameplay`、`Config` 等内层模块依赖 Unity Provider。
- `ResourcesProvider` 内部继续使用 `UnityEngine.Resources.Load`，这是 Provider 的职责，不属于业务脚本直连。
- 仓库不再保留 `Assets/Resources` 目录；`ResourcesProvider` 测试需要的 fixture 会在测试运行时临时创建并清理。

## Resources 目录当前保留项

- 无。仓库内没有长期提交的 `Assets/Resources` 资源。
- `Assets/TestAssets/MxFramework/ResourcesDemo`：AssetBundle / RemoteBundle 测试源文件。
- `ResourcesProvider` 测试会在运行时创建临时 `Assets/Temp/.../Resources` fixture，测试结束删除。

本轮已从 `Resources` 移出：

- `CombatDebug_*` 材质到 `Assets/Art/MxFramework/Showcase/Materials`。
- Runtime HUD UXML / USS / PanelSettings 到 `Assets/UI/MxFramework/Showcase`。
- Runtime Vertical Slice config 到 `Assets/Config/MxFramework/Demo`。
- Preview target profile 到 `Assets/Config/MxFramework/Preview`。
- `.DS_Store` 垃圾文件。

## 剩余直连点

业务 / Demo / Preview 侧已无直接 `Resources.Load`。`ResourcesProvider` 内部仍保留 `UnityEngine.Resources.Load`，用于验证 provider 能力和兼容少量项目显式接入场景。

## 验收

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Combat.RuntimeCombatShowcaseRunnerTests.RuntimeHudButtons_DoNotKeepKeyboardFocusForSpaceJump, 1/1 passed
Unity EditMode: MxFramework.Tests.Combat.RuntimeCombatShowcaseRunnerTests, 8/8 passed
Unity EditMode: MxFramework.Tests.Ability, 27/27 passed
Unity EditMode: MxFramework.Tests.Preview.ScenePreviewWorldDynamicTargetTests, 12/12 passed
Unity EditMode: MxFramework.Tests.Resources, 40/40 passed
Unity scene validation: Assets/Scenes/RuntimeVerticalSlice.unity, no issues
GitNexus detect-changes: risk low
```
