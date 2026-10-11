# UPM Package Surface 与目录边界优化方案

> Status: Design
>
> Revised: 2026-08-27
>
> 本文定义哪些源码、Editor 工具、测试和示例属于外部 MxFramework package。Embedded Package 最终开发拓扑由 `Decisions/ADR-015-upm-embedded-package-topology.md` 接受；活动数字与证据状态统一读取 `UPM_CURRENT_STATUS.md`。

## 1. 已核实的问题

P1-P5 已完成 surface 分类与 owner 迁移，P6 已把 canonical UPM root 切换到 `Packages/com.vincent.wgameframework/`。本节保留迁移前问题作为设计背景；活动文件数、asmdef、initializer、tree hash 和 evidence ledger 只在 `UPM_CURRENT_STATUS.md` 与机器报告中维护。

2026-08-01 的历史抽样中，排除 `Tests/` 后有 94 个 C# 文件包含 `Assets/Scenes`、`Assets/Config`、`Assets/VFX`、`Assets/Bundles`、`FGUIProject/` 或 `Tools/` 路径：

| Surface | 文件数 |
| --- | ---: |
| Game / CharacterTest | 17 |
| Demo | 21 |
| Editor | 56 |
| 基础 Runtime / bridge | 0 |

当时另有 35 个非测试自动 Editor 入口；当前 initializer 总数以 `UPM_CURRENT_STATUS.md` 和 E0 report 为准。审计不会把这些文件一律判为缺陷：

- 把输出写到 Consumer 自己的 `Assets/...` 可以合法；
- 注册菜单、tool provider 或 importer 可以合法；
- 缺少项目数据时静默表示 capability unavailable 可以合法；
- 在 import/initialize 时要求 WGameFramework 特有场景、配置、`FGUIProject` 或 `Tools`，或因此抛异常/`LogError`，不可作为基础包默认行为。

现有 base Consumer 曾因 VFX Editor bootstrap 把项目自有 manifest 缺失写成 `LogError` 而失败，证明路径/初始化可移植性必须成为显式 contract。

### 1.1 2026-08-01 历史边界事故

固定候选 `343b1d084747879df8c0ebfadf59e7cc79800726` 的 clean base Consumer 进一步发现：`Editor/PlayModeUiHierarchyLocator.cs` 位于基础 `MxFramework.Editor` 程序集，却静态引用 `UnityEngine.EventSystems`、`PointerEventData`、`RaycastResult` 和 `PanelRaycaster`。这些类型来自 `UnityEngine.UI` / `com.unity.ugui`，而 uGUI 不在主包 15 项 mandatory dependencies 中。

主工程没有暴露该问题，因为 embedded FairyGUI 和 TextMeshPro 间接解析了 uGUI。干净 base Consumer 不安装 FairyGUI，因而在包导入阶段直接编译失败。这是“主工程依赖环境掩盖 package boundary”的实证，不是理论风险。

本轮最小修复保留 locator 为 `portable-editor`，但把 uGUI/EventSystem 命中放入低频反射桥：

- package 未安装 uGUI 时，基础 Editor 程序集不再产生 `UnityEngine.UI` 静态依赖，locator 仍可尝试 FairyGUI 并输出 capability unavailable 诊断；
- package 安装 uGUI 时，桥接当前 `EventSystem`、pointer raycast、click handler 和 UI Toolkit `PanelRaycaster`，保持原命中顺序；
- 反射只在用户显式触发的 Editor 诊断路径执行，归类为 `LowFreqAlloc`，不进入 Runtime 或每帧 hot path；
- 当时主工程 Unity compile、locator focused tests `3/3`、Local base `1/1` 与三项 positive profile `2/2` 通过；由于工作树脏，这些只属于历史 E1。
- E0 policy v2 已把事故固化为 `optional-source-ugui`；最近静态结果读取 `UPM_CURRENT_STATUS.md`，且任何 E0 结果都不替代 matching Consumer matrix。

由此增加一条 surface 规则：`portable-editor` 可以在低频路径通过无静态类型引用的 capability bridge 探测 optional package；如果需要强类型或高频使用，则必须进入独立 optional assembly/package gate，不能把 optional 类型直接留在基础 Editor asmdef 中。

## 2. 官方约定与仓库修正

Unity 当前推荐的 package layout 包含 package manifest、README、CHANGELOG、LICENSE、Editor、Runtime、Tests、`Samples~` 和 `Documentation~`。本仓库采用以下精确解释：

- consumer-importable samples 放在 `Samples~/<sample>`，并在 `package.json.samples` 登记；
- package-owned tests 放在 `Tests/Editor` 与 `Tests/Runtime`；非 embedded Consumer 通过项目 manifest 的 `testables` 启用；
- repository-only tests 不应伪装成 package tests，应移到 UPM root 外；
- package documentation 放在 `Documentation~`，同时保留根 README/CHANGELOG；
- `Tests~` 不是本计划采用的 package test 目录。此前 Current 文档中的 `Tests~` 表述应更正。

官方布局是推荐约定，不要求为了视觉一致性立即把 100 多个模块 asmdef 全部塞进一个 `Runtime/`。本计划优先收窄消费 surface 和依赖闭包，避免无收益的 GUID/路径大迁移。

参考：

- [Unity package layout](https://docs.unity3d.com/Manual/cus-layout.html)
- [Adding tests to a package](https://docs.unity3d.com/Manual/cus-tests.html)
- [Creating package samples](https://docs.unity3d.com/Manual/cus-samples.html)

## 3. Surface 分类模型

每个 asmdef 必须且只能属于以下一种 owner：

| Owner | 能否随基础包安装 | 允许依赖 |
| --- | --- | --- |
| `base-runtime` | 是 | mandatory package dependencies、其他 base runtime |
| `portable-editor` | 是 | base runtime、UnityEditor、明确 mandatory dependencies；缺项目数据时可解释降级 |
| `optional-adapter` | 条件编译或后续独立包 | 对应第三方 package 与 base contract |
| `package-test` | 仅 testables/开发态 | package public/internal test contract，不依赖 WGameFramework 外围资产 |
| `package-sample` | 仅用户显式 Import | sample 自带资产，或在导入说明中声明依赖 |
| `repository-validation` | 否 | 主工程场景、Config、FGUIProject、Tools、FMOD bank、CharacterTest |

禁止出现 `unclassified`。建议新增机器可读清单：

```text
Assets/Scripts/MxFramework/upm-surface.json
```

示意：

```json
{
  "schemaVersion": 1,
  "assemblies": {
    "MxFramework.Runtime": "base-runtime",
    "MxFramework.Editor": "portable-editor",
    "MxFramework.Audio.FMOD": "optional-adapter",
    "MxFramework.Game.CharacterTest": "repository-validation"
  }
}
```

E0 static gate 必须验证：全部 package-root asmdef 都有 owner，package-root 外的 repository-validation asmdef 不被 package assembly 反向引用。

## 4. 目标物理拓扑

### 4.1 过渡 root

在 L1-L4 surface 收敛期间暂时保留：

```text
Assets/Scripts/MxFramework/
```

这一位置只用于降低早期迁移风险，保持现有 local/Git URL 和 Consumer runner 可用；它不是最终开发拓扑。`MxFrameworkPackageAssetLocator` 的 development fallback 也是过渡桥。

### 4.2 最终 Embedded Package root

L1-L4 完成后，以独立切片迁移为：

```text
Packages/com.vincent.wgameframework/
  package.json
  README.md
  CHANGELOG.md
  LICENSE.md
  Third Party Notices.md
  Documentation~/
  <base runtime module directories>/
  <portable editor directories>/
  <optional adapter directories>/
  Tests/
    Editor/
    Runtime/
  Samples~/
    <consumer-ready sample>/
```

开发工程和已安装 Consumer 都通过 `Packages/com.vincent.wgameframework/...` 访问 package asset。仓库继续保持 monorepo；Embedded Package 不要求拆分 Git repository。

package-owned tests 只保留外部边界契约，例如：

- `RuntimeHost` 最小 lifecycle；
- package public API compile sentinel；
- portable Editor 在缺少项目数据时不抛异常/不写 `LogError`；
- optional adapter 的小型 compile/behavior contract。

这不等于把当前 646 个测试 C# 文件整体重新归档到 `Tests/Editor`。

### 4.3 Project-owned surface

建议新建清晰的主工程 owner 根，具体名称在实施前冻结，例如：

```text
Assets/Scripts/WGameFrameworkProject/
  CharacterTest/
  Demos/
  Editor/
  Tests/
```

也可以使用已有更合适的 framework-validation 根；关键不是名字，而是：

- 它不在 package root 下；
- 只能依赖 package assembly，不得被 package assembly 反向依赖；
- 主工程 `Assets/Scenes`、`Assets/Config`、`Assets/VFX`、`Assets/Bundles`、`FGUIProject`、`Tools` 由此 surface 拥有；
- CharacterTest 继续是框架验证游戏，不进入通用 public package surface；
- Embedded cutover 后，旧 Git subpath `/Assets/Scripts/MxFramework` 改为 `/Packages/com.vincent.wgameframework`，必须提供 preview migration note。

## 5. 路径可移植性分类

对审计发现的 94 个文件逐个标注下列类型：

| 类型 | 例子 | 处理 |
| --- | --- | --- |
| `portable-no-input` | 只注册菜单、生命周期清理或纯内存 bridge | 可保留；不读取项目路径、不写项目内容 |
| `consumer-output` | 创建 `Assets/Scenes/MxFramework/...` | 可保留；路径可配置、写入前确认目录、操作必须由用户显式触发 |
| `optional-project-input` | 可选读取 Consumer 自己的 `Assets/Config/...` | 可保留；缺失返回 unavailable/empty，不在 import 时报错 |
| `sample-input` | 示例专用场景、FGUI bytes、VFX prefab | 移到 `Samples~`，或移入 repository validation |
| `repository-input` | `Tools/MxFramework.Authoring`、`FGUIProject` | 移到 package 外，或通过明确的 host capability/provider 注入 |
| `test-fixture` | 测试场景和固定 Config | 移到 package 外测试 owner，或打包为自包含 package test fixture |
| `generated-authority` | Composer/VFX/Resource 生成源 | 由对应 authoring owner 管理，不允许 portable Editor 默认假定存在 |

每个自动 Editor 入口还必须声明：

```text
Activation: import | domain reload | user command | play mode
Required inputs: none | optional | explicit configured
Missing-input behavior: silent unavailable | diagnostic info | blocking error
Writes: none | Temp | Consumer Assets | repository-only
```

`upm-surface.json` 使用 `editorInitializers` 以相对 package root 的源码路径登记这些事实。E0 L0 strict policy 要求该集合与静态检测到的非测试 initializer 集合精确一致；旧条目和漏登条目都会失败。每项至少包含 `classification`、`activation`、`requiredInputs`、`missingInputBehavior` 与 `writes`，其中 `classification` 使用本节上表的路径可移植性类型。

示意：

```json
{
  "editorInitializers": {
    "Editor/VfxPublishedBindingEditorBootstrap.cs": {
      "classification": "optional-project-input",
      "activation": "domain reload",
      "requiredInputs": "optional",
      "missingInputBehavior": "silent unavailable",
      "writes": "none"
    }
  }
}
```

默认规则：

- import/domain reload 不得因为可选项目数据缺失而 `LogError`；
- 缺少用户明确配置时不得自动写 Consumer `Assets`；
- `Tools/` 和 `FGUIProject/` 不得作为 portable Editor 的隐式当前目录；
- 需要外部 authoring host 时使用接口、settings/provider 或显式命令参数；
- 真实数据损坏可以 fail-closed，但必须先确认该数据已被用户启用，而不是仅仅不存在。

## 6. Tests 优化

### 6.1 当前问题

- package root 的 Tests 有 659 个非 `.meta` 文件；
- 聚合 `MxFramework.Tests` 同时引用 FMOD、FairyGUI、Transport、CharacterTest、Demo 和大量 Editor assembly；
- `MxFramework.Game.CharacterTest.Editor` 还直接引用 `UnityEditor.TestRunner`；
- 因 Tests 与 Game 仍在 package root，主包 `package.json` 把 `com.unity.test-framework` 作为 mandatory dependency；
- base smoke 自己的 NUnit/TestRunner 目前也通过主包的该 dependency 间接获得。

### 6.2 目标

1. 把 CharacterTest、PlayMode scene、性能、authoring、完整回归移到 repository-validation tests。
2. 把真正 package-owned contract tests 收敛到 `Tests/Editor` / `Tests/Runtime`。
3. Consumer smoke fixture 在自己的 `Packages/manifest.json` 显式声明 Test Framework，不借主包 dependency。
4. package root 清除非测试生产 assembly 对 `UnityEditor.TestRunner` / NUnit 的引用。
5. 完成后评估从 MxFramework mandatory dependencies 移除 `com.unity.test-framework`。

移除 Test Framework dependency 的必要条件：

- clean base consumer 仍编译；
- package test consumer 通过 `testables` 和显式 Test Framework dependency 能运行；
- Consumer Smoke fixture 显式拥有自己的测试依赖；
- 主工程 repository tests 仍由主工程 manifest 提供测试依赖；
- package public/runtime assembly 不泄漏 NUnit/TestRunner 类型。

## 7. Samples 优化

不是所有 Demo 都应该变成 package sample。候选必须满足：

- 能独立导入到干净 Consumer 的 `Assets/Samples/...`；
- 场景、Prefab、ScriptableObject、配置和必要资源随 sample 一起提供；
- 不依赖 WGameFramework 根目录的 `Assets/Config`、`Assets/VFX`、`Assets/Bundles` 或 `FGUIProject`；
- 不依赖 FMOD bank，除非 sample 明确属于 FMOD optional sample；
- 使用真实 MxFramework API，不复制 runtime；
- 有 README、入口场景、输入说明和预期结果；
- Import 后不会自动修改项目设置或覆盖 Consumer 文件；需要配置时使用显式 setup command，并可撤销。

第一批只选择 1 个最小 Runtime sample，避免把全部 Demo 一次性搬入 `Samples~`。建议以 RuntimeHost/Gameplay 最小 vertical slice 为候选，但必须在实施时重新检查其场景与资源依赖。

## 8. 迁移切片

### L0：建立清单，不移动文件

- 新增 `upm-surface.json`；
- 给 package root 内全部 asmdef 分类；
- 给 package root 内全部 Editor initializer 分类，并登记 project-path capability owner；
- E0 阻断新增 unclassified assembly/initializer；
- 记录 package-root 当前 GUID、asmdef identity 和 reverse dependency baseline。

验收：只产生文档/清单与静态检查，不改变编译结果。

### L1：迁出 CharacterTest

- 移动 `Game/CharacterTest*`、对应 Editor 与测试到 package root 外；
- 保留 `.meta`，通过 Unity Editor / Unity MCP 或受控资产移动完成；
- package asmdef 不得引用 CharacterTest；
- 主工程 CharacterTest 继续依赖 package API；
- base + optional Consumer matrix 与 CharacterTest focused tests 都通过。

这是收益最大的第一步：同时减少 package 文件面、主工程路径假设和 optional closure 粗耦合。

### L2：拆 repository tests 与 package tests

- 把聚合回归移出 package；
- 新建最小 `Tests/Editor`、`Tests/Runtime`；
- Consumer fixture 显式声明 Test Framework；
- 验证是否可移除主包 Test Framework dependency。

### L3：拆 Demo / Samples

- 选择一个 self-contained sample；
- 通过 Unity Editor 生成或移动序列化资产，不手写 YAML；
- 登记 `package.json.samples`；
- 其余主工程 Demo 迁到 repository-validation surface。

### L4：收敛 Editor portability

- portable Editor 只保留通用工作台与显式 host capability；
- 依赖 `Tools`、`FGUIProject` 或 WGameFramework 项目布局的实现迁出；
- 每个 auto-load 入口增加 absent-project-data smoke；
- base Consumer import 为 0 `LogError`、0 隐式资产写入。

### L5：Embedded Package cutover

- 把收敛后的 package root 移到 `Packages/com.vincent.wgameframework`；
- 保留 package 内部资产的 `.meta`、GUID 和 asmdef identity；`Assets/Scripts/MxFramework.meta` 作为源 root sibling folder meta 单独登记并退役，不在 `Packages/` 下创建对应 sibling `.meta`；
- 更新 runner 的 local root、Git subpath、Multi-project 示例和 CI；
- 用 preview migration note 明确 Consumer manifest 变更；
- 验证 canonical package asset path 后删除 `Assets/Scripts/MxFramework` fallback。

L5 只迁移已经完成 owner 分类的 package 内容，不与 CharacterTest、Tests 或 Demo 拆分混成一次大提交。是否进一步把模块目录嵌套到 `Runtime/`/`Editor/` 仍需独立收益证明，不能只为匹配示例树制造大规模无语义路径变化。

## 9. 每个切片的验证

目录/asmdef 移动属于 V4 package/public boundary 变更，最低要求：

- 移动前后 `.meta`/GUID 审计；
- `git diff --check`；
- E0 package boundary；
- 主工程 Unity refresh/compile + Console error；
- 受影响 EditMode tests；
- 有场景/Playable surface 时 focused PlayMode；
- clean Local base Consumer；
- matching optional profiles；
- 候选提交的 PinnedGit matrix；
- public API/asmdef identity 变化时 migration note 和 package minor/preview 版本判断。

不得在 dirty Unity scene 未保存、模态 reload 未处理或用户资产同时移动时执行大范围目录迁移。

## 10. 完成定义

- package root 内所有 asmdef 都有唯一 surface owner；
- 开发工程与 Consumer 使用同一 `Packages/com.vincent.wgameframework` package asset root；
- CharacterTest、仓库聚合 Tests 和主工程专用 Editor 不再随基础包安装；
- package-owned tests 位于 `Tests/Editor` / `Tests/Runtime`，repository-only tests 位于 package root 外；
- 至少一个 self-contained sample 通过 `Samples~` 和 manifest 导入；
- portable Runtime 不含 WGameFramework 主工程路径假设；
- portable Editor 对缺失可选项目数据不抛异常、不写 `LogError`、不自动写资产；
- `Tools/` / `FGUIProject` 依赖通过 repository owner 或显式 host capability 处理；
- Test Framework 是否保留为 mandatory dependency 有实测结论；
- local/Git Consumer 已迁移到新的 package root/subpath，旧开发 fallback 已删除；
- 主工程和外部 Consumer 的验证都通过，没有因“包变小”牺牲 CharacterTest 验证能力。
