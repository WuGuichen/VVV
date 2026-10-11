# UPM 可选集成与 Adapter 优化方案

> Status: Design
>
> Revised: 2026-08-23
>
> 本文负责 FMOD、Magica Cloth、FairyGUI、Unity Transport 和 VFX Graph 的包来源、编译 gate、positive profile 与支持等级。活动 evidence ledger 统一读取 `UPM_CURRENT_STATUS.md`；基础包布局见 `UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md`，正式发布矩阵见 `UPM_RELEASE_UPGRADE_READINESS_PLAN.md`。

## 1. Gate 基础与历史证据

Base Consumer 不安装四类 optional integration。反向引用闭包使用以下根程序集和 gate；活动 closure 数量与最近结果只从 `UPM_CURRENT_STATUS.md` 和 E0 report 读取：

| Integration | Root external assembly | Gate |
| --- | --- | --- |
| FMOD | `FMODUnity`, `FMODUnityEditor` | `MXFRAMEWORK_FMOD` |
| Magica Cloth | `MagicaClothV2` | `MAGICACLOTH2` |
| FairyGUI | `FairyGUI` | package version define |
| Unity Transport | `Unity.Networking.Transport` | package version define |
| VFX Graph | `Unity.VisualEffectGraph.Runtime/Editor` | package version define |

历史 reviewed immutable baseline 已验证 positive profile：

- FairyGUI `5.2.0`；
- Unity Transport `6.5.0`；
- VFX Graph resolved baseline `17.5.0`，adapter gate minimum `17.0.0`。

每个 profile 证明对应 baseline 上 optional package 进入 lock、adapter DLL 生成、基础 RuntimeHost 测试通过和一个最小 adapter behavior 通过。它们不证明当前移动 `main`、完整 CharacterTest、网络会话、FairyGUI package publish、VFX asset 或 Player 表现。

## 2. 优化原则

1. Base package 未安装 optional integration 时必须正常解析和编译。
2. Optional package 已安装但版本过低时，对应 adapter 明确不可用；不能产生半编译程序集。
3. 第三方类型不得进入 base noEngine public API。
4. 缺依赖通过 compile-time capability 表达，不在高频路径用反射轮询。
5. Positive profile 必须验证 resolved source/version、adapter DLL 和行为，不以“包在 lock 中”代替编译。
6. Compile support、Editor support、Player support、content/bank support 分层声明，不能用一个 `2/2` smoke 覆盖全部。
7. 在单包 gate 足够稳定前，不急于把每个 adapter 拆成独立 MxFramework package。

## 3. 支持等级

每个 integration 使用统一等级：

| Tier | 含义 | 最低证据 |
| --- | --- | --- |
| O0 Excluded | Base 不安装，相关闭包完全禁用 | clean base compile + closure gate |
| O1 Compile | 安装可选包后 adapter assembly 编译 | positive lock/source/version + DLL |
| O2 Behavior | 一个无项目内容的最小 adapter 行为通过 | focused EditMode behavior |
| O3 Player | 最小 Player 构建并运行 adapter bootstrap | Player build/run receipt |
| O4 Content | 使用真实 UI package、network session、VFX asset 或 FMOD bank 验证 | surface-specific acceptance |

Reviewed preview baseline 可声明：

| Integration | Reviewed baseline tier | 活动 evidence 规则 |
| --- | --- | --- |
| FairyGUI | O2 | 以 Current ledger 为准；新 candidate 必须重跑 matching E2 |
| Unity Transport | O2 | 以 Current ledger 为准；新 candidate 必须重跑 matching E2 |
| VFX Graph | O2 | 以 Current ledger 为准；新 candidate 必须重跑 matching E2 |
| FMOD | O0；主工程内存在更高层验证，但没有外部 Consumer profile | 在 `with-fmod` 达到 O2 前保持 O0 |
| Magica Cloth | O0；当前仅有 `Assets/MagicaCloth2` project plugin 与 gated adapter | 来源、分发许可和 clean Consumer identity 批准前不增加 positive profile |

主工程内功能通过不能自动提升外部 Consumer tier。

## 4. FMOD 核实结果

### 4.1 已存在 package-shaped source

仓库已有：

```text
Assets/Plugins/FMOD/
  package.json
  FMODUnity.asmdef
  src/Editor/FMODUnityEditor.asmdef
  platforms/**
```

manifest 当前为：

```text
name    = com.fmod.fmodstudio
version = 2.3.19
unity   = 6000.0
```

目录约 296 MB、450 个 tracked files，平台库由 Git LFS 管理。FMOD 官方当前文档明确支持把 FMOD 根目录和根 `.meta` 移到 UPM package 位置，并要求 package version 与 integration version 一致。因此“FMOD 完全没有 UPM 形态”不是准确问题定义。

参考：[FMOD Unity Integration installation location](https://www.fmod.com/docs/2.03/unity/user-guide.html#installation-location)。

### 4.2 尚不能直接声明为 external package

现有 FMOD manifest 的 `dependencies` 为空，但 asmdef 包含以下外部 assembly references：

- Runtime：Timeline、Addressables、ResourceManager、URP；
- Editor：Input System、Visual Scripting、Scriptable Build Pipeline 等；
- `FMODUnity.asmdef` 的 URP version define 使用 `com.unity.urp`，而主工程实际 package id 是 `com.unity.render-pipelines.universal`。

这些引用是否在 clean Consumer 中全部可解析尚未实测。不能因为主工程已编译就把 manifest 视为完整依赖声明。

此外：

- 当前 MxFramework FMOD adapter 依赖手工 `MXFRAMEWORK_FMOD`；
- `ProjectSettings.asset` 只为 Standalone 配置该 symbol；
- `with-fmod` profile 不存在；
- pinned Git 获取会涉及 LFS、Gitea 凭据和大量平台二进制；
- Unity 官方提醒：Git LFS 客户端缺失时，UPM 可能只检出 pointer file 而没有明显错误，因此必须额外验证 payload；
- FMOD integration 可免费使用，但发布游戏仍需相应 FMOD license；本仓库是否允许把二进制作为可供所有 Consumer 获取的 package source，需要 owner 明确决定；
- FMOD bank、Studio project、settings 和 adapter package 不是同一权威，不能一起模糊打包。

因此当前准确状态是：

```text
Local package manifest exists.
Approved immutable distribution and clean external consumption are not verified.
```

## 5. FMOD 优化路线

### F0：冻结分发与身份决策

由 owner 明确：

1. FMOD integration 只允许项目本地/私有 NAS 消费，还是允许更广的仓库用户消费；
2. 是否允许 Gitea Git subpath 直接提供含 LFS 二进制的 package；
3. 选择 package id：保留现有 `com.fmod.fmodstudio`，或迁移到 FMOD 当前文档示例的 `com.firelight.fmod-for-unity`；
4. 支持哪些平台二进制，是否拆除不支持/受 NDA 约束的平台；
5. bank、FMOD Studio source、settings 和 integration binaries 分别由谁拥有。

在这五项没有批准前，只维持 O0，不创建对外 `with-fmod` release claim。

### F1：使 FMOD package 可独立解析

在隔离 Consumer 中对 `Assets/Plugins/FMOD` 做 local `file:` 解析，先不改主工程目录：

- 检查 manifest name/version；
- 审计所有 asmdef external references；
- 为真正 mandatory package 补 dependency；
- 修正或验证 URP package id/version define；
- 对可选 FMOD feature 使用正确 version define / source conditional，而不是依赖主工程传递包；
- 验证所有目标平台所需 native payload 存在且不是 LFS pointer；
- 记录 package size、解析时间和 PackageCache 实际 manifest。

F1 的目标是证明 FMOD package 自己成立，不涉及 MxFramework adapter。

### F2：增加 `with-fmod` O1/O2 profile

Runner 增加：

```text
-Profile with-fmod
-FmodPackageSource <approved-local-or-immutable-source>
```

最低断言：

- FMOD package source/name/version 精确；
- `FMODUnity` 与 `FMODUnityEditor` 编译；
- `MxFramework.Audio.FMOD` 与 Editor adapter 编译；
- reverse closure 与 chosen package version gate 一致；
- native library 文件通过非 pointer/非空/预期 hash 检查；
- 不需要 bank 的 adapter availability/initialization 行为通过；
- 退出时无 native handle 泄漏、`LogError` 或初始化残留。

如果无 bank 初始化不是 FMOD 合法使用方式，则 O2 测试应只验证明确的 `Unavailable/NotConfigured` 结果，不能伪造成功出声。

### F3：替换手工 define

只有 FMOD 通过 UPM identity 安装后，才把 MxFramework FMOD asmdef 从手工 Build Profile symbol 迁移为 package `versionDefines`：

```text
chosen FMOD package id/version
  -> MXFRAMEWORK_FMOD
```

迁移期可以短暂双轨，但最终只能保留一个 authority。完成后：

- 主工程不同 BuildTarget 不再靠人工复制 symbol；
- package 缺席时 adapter 自动排除；
- package 版本低于支持线时 adapter 自动排除；
- consumer report 记录实际 resolved FMOD version。

### F4：FMOD Player 与真实 bank

分为两个 gate：

1. **Adapter Player gate**：构建并启动无业务 bank 的最小 Player，验证 native library 装载和明确的未配置行为。
2. **Project audio acceptance**：由 WGameFramework/CharacterTest 自己的 bank、cue、bus、VCA 和 audible acceptance 负责，不作为通用 package 必备 sample。

FMOD O4 不应把项目 bank 复制到通用 MxFramework package。

## 6. FairyGUI 优化

当前 `Packages/com.fairygui.gui` 已有完整 manifest、dependencies、README 和 LICENSE，并完成同 SHA pinned subpath smoke。

后续重点：

- package/source SHA 与 MxFramework candidate 是否必须相同要明确记录；当前私有同仓方案要求相同 SHA；
- positive profile 增加 generated manifest/resource bridge 的无外围 FGUIProject 行为，确保 `FGUIProject` 不成为运行时必需；
- package publish、atlas、`*_fui.bytes` 属于 sample/consumer content，不进入基础 O2；
- 完成 package surface 拆分后，FairyGUI closure 不应继续拖住 CharacterTest、Demo 和聚合 Tests；
- 如果未来使用外部官方 FairyGUI source，新增 source provenance/hash，而不是默默把本地 embedded package 换成不同内容的同版本。

## 7. Unity Transport 优化

当前 O2 只构造 `UnityTransportNetworkTransport`、检查 identity 和未启动 restart 行为。后续分层：

- O2 保持无 socket、确定性、快速；
- O3 Player 增加 loopback 初始化/关闭，验证目标平台 native/collections 组合；
- O4 网络会话、late join、rollback/resync 继续由 Gameplay/CharacterTest integration tests 负责；
- adapter minimum `6.5.0` 与 resolved baseline 同步写入 support matrix；
- 如果主包不再 mandatory 引入相关依赖，positive Consumer 必须显式拥有 Collections/Transport，不借主工程传递依赖。

## 8. VFX Graph 优化

当前 O2 验证空 controller 的 unavailable/cleanup 行为。后续：

- O2 保持不要求 VFX asset；
- O3 Player 使用最小生成或 package-owned sample graph，验证目标图形 API；
- O4 的真实 graph、Prefab、fallback 和表现验收属于 sample/project content；
- minimum gate `17.0.0`、当前 resolved `17.5.0` 与 URP compatibility 必须在 report 分开记录；
- VFX Editor initializer 缺少 project-owned manifest 时继续静默 unavailable，不得恢复为 import-time `LogError`。

## 9. 是否拆成独立 MxFramework adapter packages

候选拓扑：

```text
com.vincent.wgameframework
com.vincent.wgameframework.fairygui
com.vincent.wgameframework.unity-transport
com.vincent.wgameframework.vfx-graph
com.vincent.wgameframework.fmod
```

但只有满足以下任一条件才执行拆包：

- 单包 asmdef gate 仍导致大量无关 surface 被禁用；
- 外部 Consumer 需要独立 adapter version/upgrade cadence；
- adapter 的依赖或许可不能出现在 base package；
- package registry/release tooling 需要独立制品；
- Player build 明确要求更小的安装/导入面。

拆包前先完成 CharacterTest/Tests/Demo surface 收窄。否则只是把粗耦合搬进多个 manifest。

Git package 限制：Unity 项目的 `manifest.json` 可以引用 Git URL，但 package 自己的 `package.json` 不能声明另一个 Git URL 依赖。因此同仓多 package 的 Git consumer 可能需要在项目 manifest 中显式列出 base、adapter 和第三方 package；文档和 smoke fixture 必须反映真实安装步骤。

## 10. Support matrix 权威

建议新增机器可读文件：

```text
Packages/com.vincent.wgameframework/optional-integrations.json
```

字段至少包含：

```json
{
  "id": "unity-transport",
  "packageName": "com.unity.transport",
  "minimumVersion": "6.5.0",
  "validatedVersion": "6.5.0",
  "define": "MXFRAMEWORK_UNITY_TRANSPORT",
  "adapterAssembly": "MxFramework.Network.UnityTransport",
  "supportTier": "O2"
}
```

Runner、package README 和 release receipt 从同一事实源生成/校验，禁止三处手工维护不同版本。

## 11. 完成定义

- 四类 integration 的 package identity、minimum、validated version、define、adapter assembly 和 support tier 有单一机器可读权威；
- base profile 的 reverse closure 继续 0 违规；
- FairyGUI、Transport、VFX Graph matching positive profile 在同一拟晋升 candidate 上通过；
- FMOD 分发/许可/LFS/bank/平台责任有 owner 决策；
- FMOD package 自身在 clean Consumer 中解析和编译；
- `with-fmod` 至少达到 O2，或文档明确保持 O0 且不对外宣称支持；
- FMOD adapter 最终由 package version define 驱动，不依赖逐 Build Profile 手工 symbol；
- support tier 与实际证据一致，不用 EditMode compile 冒充 Player/内容验收；
- 是否拆 adapter packages 基于消费需求和证据决定，而不是为了目录形式提前拆分。
