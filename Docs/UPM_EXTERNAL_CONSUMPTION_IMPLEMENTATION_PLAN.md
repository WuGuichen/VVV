# UPM 外部消费验证与版本权威实施计划

> Status: Archive
>
> Record: Accepted Phase A-C
>
> 日期：2026-07-27
>
> 本文保存 MxFramework Phase A-C 建立最小 UPM 外部消费边界的历史实施顺序与证据。Phase A-C 的 base gate 已于 2026-07-20 落地并通过本地独立消费者验证；commit-pinned Git gate 已于 2026-07-26 对远端提交 `d0167b3c9b2f2028f6261548b2a030faf5796377` 完成 clean 复验。这仍不等同于稳定发布。
>
> 当前状态统一读取 `UPM_CURRENT_STATUS.md`；Phase D 后续优化由 `UPM_OPTIMIZATION_ROADMAP.md` 协调。本文中的“当前”只表示相应历史执行时点，不得用于判断活动 `main`、当前 Consumer 测试数量或发布准备度。

## 实施记录（2026-07-20）

- Framework Release Version 已切换为 `0.8.0-preview.1`，权威位置仍为 `Assets/Scripts/MxFramework/package.json`。
- package manifest 已补齐主工程曾隐式提供的 Newtonsoft Json 与源码直接使用的 Unity built-in modules，不再借用 URP、Input System 或 Test Framework 的传递依赖表达包边界。
- FMOD、FairyGUI、Unity Transport 与 VFX Graph 均有明确 asmdef compile gate；base consumer 不安装这些集成。FMOD 当前使用逐 Build Profile 配置的手工 `MXFRAMEWORK_FMOD` 过渡 gate。
- `Tools/PackageConsumerSmoke/UnityProject` 通过本地 `file:` UPM 引用导入包，默认清理并重建独立 `Library`，同时断言可选 package 未解析、asmdef 反向引用闭包完整受控。
- `RuntimeHostConsumerSmokeTests` 已通过 1/1 focused EditMode 测试；runner 输出稳定 JSON/Markdown 报告。
- 主工程 Unity 6000.5.0f1 BatchMode 刷新与编译通过，证明完整依赖组合仍可编译。
- 尚未纳入本轮：FMOD positive profile、tag smoke、Player build、N-1 升级矩阵与稳定 `1.0.0` 发布门禁。

当前 Demo、CharacterTest 和聚合 Tests 的 asmdef 仍把多种可选集成合并在同一程序集边界。base profile 因此会按依赖闭包整体禁用这些外层程序集；这是本轮不做 `Samples~`、package-owned `Tests/Editor` / `Tests/Runtime` 与 repository-only tests 分流前的有意 fail-closed 取舍，不代表只有单个 adapter 被精细禁用。

## 回归复核（2026-07-26）

- `MxFramework.Tests.PlayMode.CharacterTest` 新增后曾遗漏 FMOD、FairyGUI 和 Unity Transport 的完整闭包 gate，导致 base Consumer Smoke 在进入 Unity 前 fail-closed；现已补齐三个 `defineConstraints` 以及 FairyGUI、Unity Transport 的 `versionDefines`。
- 最新完整反向引用闭包为 FMOD 13、FairyGUI 8、Unity Transport 5、VFX Graph 2 个程序集，四类 gate 均为 0 违规。
- Unity `6000.5.0f1` clean base consumer 已重新解析 15 项必需依赖并通过编译与 `1/1` EditMode 测试；FMOD、FairyGUI、Unity Transport 和 VFX Graph 均未出现在 resolved lock。
- 本次复核只恢复 Phase A-C 的 base package boundary，不包含 positive adapter profile、pinned commit/tag、Player build 或稳定版发布声明。

## Git-pinned gate 实施记录（2026-07-26）

- runner 新增 `-PackageSource PinnedGit -Revision <full-sha>`，从 `package.json.repository.url` 生成 `git+...git?path=/Assets/Scripts/MxFramework#<full-sha>`；拒绝短 SHA、活动分支、带凭据 URL 以及调用方自行拼接的 query/fragment。
- Pinned 模式在 `Temp/PackageConsumerSmoke/PinnedGit/UnityProject` 生成独立、UTF-8 无 BOM 的最小 Consumer，不复制 fixture 的 `packages-lock.json`，默认从空 `Library` 解析。
- 验证事实来自 Consumer：要求 lock `source=git`、requested/resolved SHA 完全一致，从 `Library/PackageCache` 读取实际 `package.json`，再复核 15 项直接依赖、可选 package 缺席、完整 asmdef closure、Unity 退出码、C# 编译错误、`Debug.LogError` 和非零测试结果。
- 对远端 `bceb33241af707b3a127e386d3885992db15a417` 的 clean 解析已证明 Gitea/Unity URL 语法和完整 SHA lock 可用，但同时暴露 `VfxPublishedBindingEditorBootstrap` 错把 consumer 缺少项目自有 VFX manifest 当成 `LogError`；新门禁会正确拒绝该提交，不能把它登记为通过。
- 修正提交 `d0167b3c9b2f2028f6261548b2a030faf5796377` 已推送并通过 clean Gitea Consumer：lock `source=git`，requested/resolved SHA 完全一致，Consumer PackageCache observed version 为 `0.8.0-preview.1`，15 项直接依赖和可选闭包 `13/8/5/2` 一致，编译错误、package resolution failure 与 `Debug.LogError` 均为 0，EditMode 测试 `1/1` 通过。该提交可作为当前已审核的 immutable preview baseline；它不是稳定 tag，也不使活动 `main` 成为 release channel。

## Adapter profile 实施记录（2026-07-27）

- runner 新增 `-Profile base|with-fairygui|with-unity-transport|with-vfx-graph`；每个 positive profile 使用独立 Consumer 与空 `Library`，只安装一个可选集成。
- 三个 positive profile 都要求 optional package 出现在 resolved lock、对应 MxFramework adapter DLL 实际生成，并通过 base RuntimeHost 与一项 adapter 最小行为测试，共 `2/2`。
- FairyGUI 本地模式显式引用 `Packages/com.fairygui.gui`；PinnedGit 模式把 MxFramework 与 FairyGUI 两个 package 子路径固定到同一个完整 SHA。Unity Transport 与 VFX Graph 使用精确 Unity package baseline。
- Unity `6000.5.0f1` 已对当前远端 `ff984c808e3b35f41420d1f52e16490f4d5a6c4b` 完成三个 profile 的 local 与 PinnedGit clean 验证；六份报告均为 0 编译错误、0 package resolution failure、0 `Debug.LogError`、测试 `2/2`。
- FMOD 仍作为项目自有 `Assets` 集成使用；`Assets/Plugins/FMOD/package.json` 已提供本地 package-shaped metadata，但其依赖声明、许可、LFS、外部获取和 bank 边界尚未通过 clean Consumer 审计，因此还没有批准的 immutable external source，不伪造 positive external profile。

## 仓库外 Consumer 加固记录（2026-07-27）

- `Tools/PackageConsumerSmoke/UnityProject` 只保留为受版本控制的最小 source fixture；base 与全部 positive profile 都复制到 `C:\WGameFrameworkConsumerSmokeRuns` 下的新 Unity project，不再把 fixture 自身或仓库内 `Temp` 项目作为最终证据。
- 默认布局为 `C:\WGameFrameworkConsumerSmokeRuns\<Local|PinnedGit>\<profile>\UnityProject`；每个项目拥有独立 `Assets`、`Packages`、`ProjectSettings`、`Library`、日志和报告。`C:\WGameFrameworkConsumerSmoke` 与 `C:\WGameFrameworkConsumerRemote` 分别保留为本地源码和远程固定提交的长期开发 Consumer，都不作为 runner 的生成或清理目标。
- runner 新增 `-ConsumerRoot`，但 fail-closed 拒绝磁盘根目录、框架仓库、仓库子路径，以及包含框架仓库的父目录；清理仍只允许作用于精确 profile 输出目录下的 `UnityProject`。
- Local 模式只允许显式 `file:` 包引用；PinnedGit 模式只使用完整 SHA Git 引用，FairyGUI 的两个 package 子路径继续固定到同一 SHA。

## R0 跨平台刷新记录（2026-08-01）

- 初版跨平台 E0 policy v1 对远端候选 `343b1d084747879df8c0ebfadf59e7cc79800726` 通过静态边界检查：15 项直接依赖、130 个 asmdef、optional closure `13/8/5/2` 全部 0 违规；tree hash 为 `c12ce4470ab84a21a9eeea73dfc2049cb1fbdfcd124f99d4754c5f57a39e5de0`。它尚未检查源码层的 uGUI 引用。
- 本机使用 portable PowerShell 7 和 Unity `6000.5.0f1` 运行 PinnedGit `base`。Git clone、完整 SHA lock 与 PackageCache 解析成功，但候选因 `PlayModeUiHierarchyLocator` 静态依赖未声明的 `com.unity.ugui` 而编译失败，因此 `343b1d...` 被拒绝，不能替换 `ff984c...` baseline。
- E0 已升级为 policy v2：直接 `UnityEngine.EventSystems` / `UnityEngine.UI` using 必须由 mandatory dependency、FairyGUI reverse closure 或显式 `MXFRAMEWORK_UGUI` assembly gate 覆盖。回放旧候选会以 `optional-source-ugui`、1 blocking failure 在启动 Unity 前拒绝；当前修复树为 2 个受 FairyGUI 闭包覆盖的引用、0 违规。
- 当前脏工作树已把 locator 的 uGUI/EventSystem 访问改为低频运行时 capability bridge；主工程 Unity compile 为 0 新增编译错误，locator focused EditMode 测试 `3/3`。
- 修复后的 Local clean Consumer 四档均按 E0 policy v2 重跑并通过：`base=1/1`，三个 positive profile 各 `2/2`，共同 package tree hash 为 `4e062130adb2c8e64d0e6f864c3d3af6af7f59219173b7715142e5aeda4a6775`。所有 report 均为 `repositoryDirty=true`，只属于 E1，不构成 immutable evidence。
- macOS 首次执行发现 PATH 中多个 `python3` 时 PowerShell 会把多条 `Get-Command` 结果拼接为一个命令；runner 已改为确定性选择首个 Application，并由四档 Local Consumer 实跑覆盖。
- 当前 package manifest 的 repository URL 仍绑定旧 LAN 地址；本轮通过不含凭据的 `-GitUrl` override 使用当前可达 Gitea。正式 metadata 仍需选择长期 canonical URL，不能用一次机器覆盖值代替。
- 下一步必须先让精确修复进入新的远端完整 SHA，再从 E0、PinnedGit base 和三个 positive profile 重新运行；旧候选 E0 与脏 Local 结果不得拼成同一 receipt。

## 1. 目标

本计划只解决三个当前需要提前落地、但不应打断框架功能开发的问题：

1. 明确当前框架仍处于预发布开发阶段，`1.0.0` 不能作为稳定性、兼容性或发布完成证据。
2. 建立一个独立于主 Unity 工程的最小 Consumer Smoke，证明另一个干净 Unity 6 项目只通过 UPM 包边界即可完成依赖解析、脚本编译和最小 Runtime 启动。
3. 隔离 FMOD、FairyGUI、Unity Transport 和 VFX Graph 等可选依赖，使未安装某个可选集成时只禁用对应 adapter，不导致基础包编译失败。

这三项是包边界保护，不是完整发布工程。正式 Samples、全平台 Player Build、N-1 升级矩阵、签名制品和稳定 `1.0.0` 留在框架核心边界收敛之后实施。

## 2. 当前事实与问题

当前 UPM package root 是：

```text
Assets/Scripts/MxFramework/
```

当前包名是：

```text
com.vincent.wgameframework
```

当前已知事实：

- `package.json` 历史上写入过 `1.0.0`，但没有与其对应的稳定 release tag、升级证据或统一 release report；现已纠正为 `0.8.0-preview.1` 并通过 base Consumer Smoke。
- `Docs/README.md` 的 `0.x` 版本是文档事实集修订记录，不是 UPM package release version。
- 各模块的 `v0.1.x`、Phase 编号以及 SaveState/catalog/wire schema 版本属于各自兼容域，不是 UPM package version。
- 主工程安装了 Input System、URP、Unity Transport、VFX Graph、本地 FairyGUI package 和项目内 FMOD 集成；当前 `package.json` 没有完整表达所有 adapter 的可选性或获取方式。
- package root 内同时包含基础模块、Unity adapter、Editor、Demo 和 Tests；部分 Demo/Editor 路径引用 package root 之外的 `Assets/Scenes`、`Assets/Bundles`、`Assets/VFX`、`Assets/Config`、`FGUIProject` 或 `Tools` 内容。
- 当前仓库自身编译成功不能证明干净 consumer 能编译，因为主工程已经提供了这些依赖和外围资产。

因此，在本计划完成前只能声明“存在本地 UPM 路径接入方式”，不能声明“已经形成稳定、可发布、可升级的独立 UPM 包”。

## 3. 范围与非目标

### 3.1 本轮范围

- 冻结 package release version、文档修订、模块阶段和 schema version 的权威关系。
- 将当前 `1.0.0` 明确降格为未验证 metadata，不作为 release claim。
- 设计并实现一个最小独立 Unity consumer fixture 和本地 smoke runner。
- 对可选 adapter 建立缺依赖时的 fail-closed compile policy。
- 输出稳定、可审查的 JSON/Markdown smoke report。
- 将 smoke 作为 package 边界变化时的本地验证项，不要求默认接入重型 Gitea Unity CI。

### 3.2 本轮非目标

- 不立即发布稳定 `1.0.0`。
- 不立即把全部代码重排为 `Runtime` / `Editor` / `Samples~` / package-owned `Tests/Editor`、`Tests/Runtime` 物理目录，也不把 repository-only tests 留在 package root 内伪装成消费者测试。
- 不在本轮复制 CharacterTest、全部 Demo、FairyGUI 源工程、FMOD 工程或美术资源到 consumer fixture。
- 不要求每个 PR 构建完整 Player。
- 不建立 CDN、UPM registry、签名包、增量热更或公网发布。
- 不冻结所有公共 API；预发布阶段仍允许按现有 API 标准进行受审查的 breaking change。
- 不让 package consumer 反向依赖本仓库的 `Tools/`、`FGUIProject/` 或项目专用生成产物。

## 4. 版本权威

### 4.1 四类版本必须分离

| 版本类型 | 用途 | 权威位置 | 兼容含义 |
| --- | --- | --- | --- |
| Framework Release Version | UPM 安装、tag、release report、消费者升级 | `Assets/Scripts/MxFramework/package.json` | 唯一对外包版本 |
| Documentation Revision | Current 文档事实集的更新记录 | `Docs/README.md` | 不承诺二进制或 API 兼容 |
| Module/Phase Status | 模块成熟度和实施阶段 | `Docs/CAPABILITIES.md`、`Docs/ROADMAP.md` | 不可作为 package version |
| Schema/Wire Version | SaveState、network、catalog、JSON 等数据兼容 | 对应代码常量与 `Docs/Interfaces/*.md` | 只约束对应 schema/wire |

正式 release 必须满足：

```text
package.json version
  == Git tag version（去掉 v 前缀）
  == release report version
  == consumer smoke observed version
```

任何一项不一致都必须阻断 release。

### 4.2 当前 `1.0.0` 的处理

在完成最小 Consumer Smoke 和可选依赖隔离前：

- 历史 `package.json` 中的 `1.0.0` 只视为 metadata 占位值；当前权威值为 `0.8.0-preview.1`。
- 不创建 `v1.0.0` tag。
- 不在 Current 文档、PR 或对外说明中称其为稳定 1.0。
- consumer 只允许使用本地 `file:` 引用，或显式 pin 到审核过的 commit；不得把活动 `main` 描述为稳定 release channel。

首个实现 PR 应把 package version 切换到 `0.x.y-preview.n`。具体 `x.y` 取实现时已批准的下一预发布 release train，不从 `Docs/README.md` 的文档修订号自动推导。

推荐示例：

```text
0.8.0-preview.1
```

如果实施时项目 owner 已冻结另一条预发布 release train，以 owner 批准值为准，但仍必须满足 SemVer 和 `-preview.n` 形式。

### 4.3 预发布阶段 SemVer 规则

- 修复既有行为且不改变公共契约：增加 patch 或 preview 序号。
- 新增兼容能力：增加 minor 或 preview 序号。
- 改变公共 API、asmdef identity、package dependency 或 consumer-required setup：在 `0.x` 阶段至少增加 minor，并写 migration note。
- schema/wire version 的提升独立处理；不能只提升 package version 而省略 schema migration。
- 只有稳定 1.0 发布后，breaking change 才必须增加 major。

### 4.4 正式 `1.0.0` 最低门禁

正式 `1.0.0` 至少要求：

- 本文 Phase A-C 全部完成。
- 基础包在无可选依赖 consumer 中编译通过。
- 支持的可选 adapter profile 分别通过消费验证。
- package README、CHANGELOG、LICENSE 和依赖说明完整。
- 至少一个真实外部 Unity 项目通过 pin/tag 消费，而不是复制源码。
- 公共 API 兼容和弃用规则已经接受。
- N-1 到候选版本的 package、配置和关键 SaveState 升级验证通过。
- release tag、package version、report version 完全一致。

## 5. 最小 Consumer Smoke

### 5.1 Fixture 位置

仓库内只提交薄 source fixture：

```text
Tools/PackageConsumerSmoke/UnityProject/
  Assets/
  Packages/
  ProjectSettings/
```

执行时把 fixture 的 `Assets` 与 `ProjectSettings` 复制到仓库外的新项目；fixture 自身的 `Packages`、`Library` 或导入状态不得作为门禁证据。

只提交形成最小 Unity 项目所需文件；禁止提交：

```text
Library/
Temp/
Logs/
UserSettings/
obj/
Build/
```

consumer project 虽位于同一 Git 仓库，但拥有独立 `Assets`、`Packages`、`ProjectSettings` 和 `Library`，不得把 `C:/WGameFramework` 主项目路径传给 Unity BatchMode。

### 5.2 两种解析模式

#### Local 模式

用于日常 package 边界检查：

```json
"com.vincent.wgameframework": "file:../../../../Assets/Scripts/MxFramework"
```

Local 模式不证明 tag 可发布，但能快速发现：

- package dependency 缺失；
- asmdef 引用未隔离；
- package 源码依赖主项目 `Assets` 内容；
- Editor 初始化假定主项目目录存在；
- 基础 Runtime 无法在干净组合根实例化。

#### Pinned 模式

用于审核已经推送且不可变的候选提交，通过 Gitea Git URL 的 package subpath 加完整 commit SHA 解析：

```text
git+http://192.168.1.210:3002/vincent/WGameFramework.git?path=/Assets/Scripts/MxFramework#<full-40-character-commit-sha>
```

`path` query 必须位于 `#revision` 之前。commit-pinned 模式已经用当前 Unity `6000.5.0f1` 实测解析和 lock 语法；tag 语义仍须在实际 release candidate 阶段单独验证。

Pinned 模式必须证明：

- clean clone 可复现；
- consumer 没有读取开发工作区未提交文件；
- requested SHA 与 resolved lock hash 完全一致；
- observed package metadata 来自 Consumer `Library/PackageCache`，不是开发工作区；
- observed package version 与目标 commit report 一致；tag 发布时还必须与目标 tag/report 一致；
- Unity Editor log 没有编译错误、package resolution failure 或 `Debug.LogError`。

### 5.3 最小 smoke 内容

新增 runner：

```text
Tools/Harness/run_upm_consumer_smoke.ps1
```

默认仓库外输出：

```text
C:\WGameFrameworkConsumerSmokeRuns\
  Local\
    base\
      UnityProject\
      unity.log
      editmode-results.xml
      report.json
      report.md
  PinnedGit/
    base\
      UnityProject\
      unity.log
      editmode-results.xml
      report.json
      report.md
```

positive profiles 使用同级的 `with-fairygui`、`with-unity-transport` 与 `with-vfx-graph` 目录。

基础 profile 必须执行：

1. 读取 consumer `ProjectVersion.txt`，定位完全匹配的 Unity Editor。
2. 使用 consumer 独立 Library 解析 package。
3. 请求脚本刷新/编译并等待完成。
4. 检查 Unity process exit code 和完整 Editor log。
5. 确认无 missing assembly、missing package dependency 和 compile error。
6. 运行 package-owned 最小 EditMode fixture：
   - 创建 `RuntimeHost`；
   - 注册一个最小 module；
   - 完成 Initialize/Tick/Dispose；
   - 验证无主项目 scene/config/resource 依赖。
7. 生成不含绝对路径和时间戳漂移字段的稳定 JSON/Markdown report。

本轮最低门不要求 Player Build、CharacterTest、FMOD bank、FairyGUI package、VFX asset 或网络会话。

### 5.4 安全与并发

- smoke 只能操作 consumer project 自己的 `Library`。
- 主项目 Unity Editor 打开时仍可运行 consumer BatchMode，因为两者不是同一个 Unity project/Library；runner 必须验证两个绝对 project path 不相等。
- 同一个 source/profile consumer 同时只允许一个 Unity job；需要并行时使用不同的仓库外 `ConsumerRoot`。
- 失败不得删除主项目、consumer source 或用户工作区；只清理精确 `ConsumerRoot/<Local|PinnedGit>/<profile>/UnityProject`。
- Unity license、Gitea credentials 和私有 package credentials不得写入 report 或仓库。

### 5.5 触发条件

Consumer Smoke 不要求每次本地代码修改都运行。出现以下变化时必须运行：

- `package.json`；
- 新增/删除/重命名 asmdef；
- 修改 package dependency；
- 新增第三方程序集引用；
- 移动公共程序集或 namespace；
- 修改 package root、UPM 路径或 optional adapter gate；
- 准备 preview/stable tag；
- 外部项目首次接入或升级。

## 6. 可选依赖隔离

### 6.1 基本原则

基础 consumer 未安装某个可选依赖时：

- 对应 adapter 不编译或不注册；
- 基础 package 继续成功解析和编译；
- 不提供伪造第三方类型的 stub；
- 不通过反射在高频路径探测插件；
- 不把第三方程序集泄漏到 noEngine public API；
- 缺依赖是明确的 capability unavailable，不是运行时随机异常。

### 6.2 当前依赖分类

| 集成 | 当前状态 | 目标策略 | 基础 smoke 是否安装 |
| --- | --- | --- | --- |
| Input System | package 声明依赖，Input adapter 直接使用 | 作为当前主包 mandatory dependency；验证最低支持版本 | 是 |
| URP | package 声明依赖，Rendering/Camera/Settings 使用 | 作为当前主包 mandatory dependency；验证最低支持版本 | 是 |
| Unity Test Framework | 当前 package 声明依赖且 Tests 在 package root | Tests 隔离后评估移出 runtime dependency；本轮可暂留 | 是 |
| VFX Graph | 已有 `versionDefines` / define constraint | 保持可选并由 consumer smoke 证明缺失时基础包可编译 | 否 |
| Unity Transport | adapter 直接引用但 package 未声明 | 增加受控 optional gate，或后续拆为 adapter package | 否 |
| FairyGUI | adapter 直接引用本地 package assembly | 增加受控 optional gate；消费方单独安装 FairyGUI package | 否 |
| FMOD | adapter 直接引用项目内 FMOD assembly；FMOD 目录已有未验证的本地 package manifest | 先审计 FMOD package dependency、许可、LFS 和 clean Consumer，再决定 version define 或独立 adapter package | 否 |

### 6.3 Gate 选择规则

实施代理不得假定给 asmdef 增加 `defineConstraints` 就一定能消除 missing reference。每种方案都必须由干净 consumer 实测。

优先顺序：

1. 第三方是标准 UPM package：使用 `versionDefines` + `defineConstraints`，并验证未安装和已安装两种状态。
2. 第三方不是可解析的标准 package，或程序集发现不稳定：把 adapter 移入独立 optional package。
3. 只有短期过渡且已证明 Unity 不会解析缺失 reference 时，才允许手工 scripting define；必须记录启用/禁用入口和诊断。

不允许：

- 在基础包中加入假的 `FMODUnity`、`FairyGUI` 或 Transport 类型；
- 捕获编译错误后仍把 smoke 标为成功；
- 要求 consumer 复制本仓库的 `Assets/Plugins/FMOD` 或 `Packages/com.fairygui.gui`，却不在接入文档声明；
- 让 Demo/Test 对可选程序集的硬引用重新破坏基础包 gate。

### 6.4 Adapter profiles

基础 profile 完成后，后续按需增加：

```text
base
with-unity-transport
with-vfx-graph
with-fairygui
with-fmod
```

`with-fairygui`、`with-unity-transport` 与 `with-vfx-graph` 已实现并完成 local/PinnedGit clean 验证。每个 profile 只证明对应 adapter 的安装、编译和一个最小行为，不运行完整 CharacterTest。`with-fmod` 仍待现有 `Assets/Plugins/FMOD` package manifest 的依赖完整性、许可证、LFS 获取和 bank 资产边界明确后实现。

## 7. 实施阶段

### Phase A：版本与当前声明收口

改动：

- 将 `package.json` 从未验证 `1.0.0` 切换到批准的 `0.x.y-preview.n`。
- 新增 package README/CHANGELOG 的最小骨架，明确 preview、依赖和非目标。
- 更新 `MULTI_PROJECT_WORKFLOW.md`，要求 consumer 使用 local path 或 pinned commit，不把 `main` 当稳定 channel。
- 新增版本一致性检查脚本的设计或最小实现。

验收：

- 仓库不存在“当前已发布稳定 1.0”的 Current claim。
- package、文档、模块和 schema version 的权威关系无歧义。
- 不创建 stable tag。

### Phase B：可选依赖隔离

改动：

- 审计全部 asmdef 外部 references。
- 隔离 Unity Transport、FairyGUI、FMOD；复核 VFX Graph 现有 gate。
- 防止 Demo/Tests 在基础 profile 中重新硬拉可选 adapter。
- 记录 mandatory dependency 的最低支持版本与主工程验证版本。

验收：

- base consumer 未安装四类可选依赖时无 missing assembly/compile error。
- 每个被保留在主包中的 optional adapter 都有 disabled/enabled focused test。
- noEngine public API 不出现第三方类型。

### Phase C：最小 Consumer Smoke

改动：

- 新增薄 consumer fixture。
- 新增 PowerShell smoke runner 和稳定 report。
- 新增最小 RuntimeHost package fixture。
- 在本地开发工作流中只对 package boundary change 触发。

验收：

- clean consumer package resolve 成功。
- Unity compile 0 error。
- 最小 EditMode fixture 通过。
- report 明确记录 Unity version、package observed version、profile、commit、dependency resolution 和测试结果。
- consumer 不读取主项目 scene/config/resource/FGUI/FMOD 内容。

### Phase D：后续正式发布准备

- `Samples~` / `Documentation~`、package-owned `Tests/Editor` / `Tests/Runtime` 与 repository-only tests 物理整理；
- package LICENSE、完整 README、CHANGELOG；
- commit-pinned smoke runner 与 `d0167b3c9b2f2028f6261548b2a030faf5796377` clean Gitea baseline 已完成；后续每个候选提交仍须重新运行；
- tag smoke 与 tag/package/report 三方一致性；
- Windows Player build；
- N-1 升级与 SaveState/schema migration；
- 正式 release report 和 `v1.0.0` gate。

Phase D 的当前审计、切片顺序与完成定义见：

- `UPM_OPTIMIZATION_ROADMAP.md`；
- `UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`；
- `UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md`；
- `UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md`；
- `UPM_RELEASE_UPGRADE_READINESS_PLAN.md`。

## 8. 文件影响预案

预计后续实现会涉及：

```text
Assets/Scripts/MxFramework/package.json
Assets/Scripts/MxFramework/*.md
Assets/Scripts/MxFramework/**/**.asmdef
Tools/PackageConsumerSmoke/UnityProject/**
Tools/Harness/run_upm_consumer_smoke.ps1
Docs/MULTI_PROJECT_WORKFLOW.md
Docs/UPM_EXTERNAL_CONSUMPTION_IMPLEMENTATION_PLAN.md
Docs/README.md
Docs/PROJECT_INDEX.md
```

不应在最小阶段修改：

```text
Assets/Scenes/**
FGUIProject/**
FMOD/**
Library/**
用户项目目录
```

如果 optional isolation 无法在不移动大量代码的情况下完成，实施代理必须停止并提交包拆分方案，不得把大范围目录迁移混入 smoke PR。

## 9. 验证分级

| 改动 | 最低验证 |
| --- | --- |
| 本实施文档 | V0：diff review + `git diff --check` |
| package version/README | V1：JSON parse + version rule check + diff-check |
| asmdef optional gate | V3：主工程 Unity compile + base consumer compile + focused adapter tests |
| consumer fixture/runner | V3：独立 consumer Unity compile + focused EditMode + report verification |
| preview/stable tag | V4：local + pinned consumer、相关 harness、release evidence |

Consumer Smoke 是 package boundary gate，不替代主工程的 Unity MCP、场景、PlayMode、资源或表现验证；主工程验证也不能替代 Consumer Smoke。

## 10. 完成定义

本计划的最小阶段只有同时满足以下条件才能关闭：

- Current 文档不再把现有 `1.0.0` 描述为稳定发布。
- `package.json` 使用批准的 preview version。
- package version authority、tag rule、文档修订和 schema version 分离规则已写入维护入口。
- base consumer 在不安装 FMOD、FairyGUI、Unity Transport、VFX Graph 时成功解析和编译。
- 最小 RuntimeHost fixture 通过。
- smoke report 可重复生成且不依赖主项目外围资产。
- 可选 adapter 的 enabled/disabled 行为可解释、可测试。
- 未把完整发布工程、Samples 搬迁或全部 Demo 修复伪装成本轮已完成。
