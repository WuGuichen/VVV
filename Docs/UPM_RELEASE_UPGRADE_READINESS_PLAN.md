# UPM Release、Player 与升级门禁方案

> Status: Policy
>
> Revised: 2026-08-23
>
> 本文定义 preview/stable 发布所需的许可、metadata、tag、Player build、N-1 升级和 release receipt。活动状态统一读取 `UPM_CURRENT_STATUS.md`；Consumer 证据规则见 `UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`。

## 1. Release gate 范围

活动 package metadata、Consumer matrix、E0 readiness gaps 和 evidence ledger 只从 `UPM_CURRENT_STATUS.md` 与机器报告读取。本文不复制这些易变化的数字。

Release gate 分为两层：

- 静态可发现性：manifest metadata、Documentation、LICENSE、Third Party Notices 和 surface manifest；
- 可晋升证据：不可变 candidate/tag、matching Consumer matrix、Player build/run、optional Player、N-1 upgrade、migration fixture、public API compatibility policy 和受版本控制的 receipt。

Build-only 不能解释为 Player run；主工程验证、移动的 `main`、dirty Local report 或分属不同 candidate 的结果也不能替代完整 release matrix。正式 `1.0.0` 必须满足本文后续全部门禁。

## 2. Release train

采用四级 train：

| Train | 版本示例 | 允许用途 | 必需证据 |
| --- | --- | --- | --- |
| Development snapshot | 无新版本 | 同仓开发 | E0；按风险 Local smoke |
| Reviewed preview baseline | `0.8.0-preview.2` | 明确 SHA 的外部开发 Consumer | pinned matrix + baseline receipt |
| Release candidate | `1.0.0-rc.1` | 稳定候选验证 | preview gates + Player + N-1 + tag candidate |
| Stable | `1.0.0` | 已批准的稳定消费 | tag/version/receipt 一致 + compatibility policy + 全部 release gates |

移动的 `main` 永远不作为 train。可消费 identity 必须是：

```text
reviewed full SHA
or
reviewed release tag resolving to an exact SHA
```

## 3. Package legal 与 metadata gate

### 3.1 许可文件

在创建下一个对外 preview tag 前必须由 owner 决定 MxFramework 自身许可。不得由 Development Agent 猜测开源许可证。

目标文件位于 package root；过渡 root 与 Embedded cutover 后分别为：

```text
Packages/com.vincent.wgameframework/LICENSE.md
Packages/com.vincent.wgameframework/Third Party Notices.md

Packages/com.vincent.wgameframework/LICENSE.md
Packages/com.vincent.wgameframework/Third Party Notices.md
```

Third-party notice 至少审计：

- Newtonsoft Json；
- FairyGUI adapter 与仓库内 FairyGUI package 的关系；
- Unity packages/built-in modules；
- FMOD adapter 代码与 FMOD integration binaries 的分发边界；
- 任何 package sample 中的美术、音频、字体、shader 或其他来源资产。

不要把第三方完整许可证盲目复制进 notice；按其许可要求放置准确文本或链接，并记录来源版本。

### 3.2 Manifest metadata

license 决策完成后，补齐适用字段：

```json
{
  "license": "<SPDX-or-reference>",
  "documentationUrl": "<approved-url-or-package-docs>",
  "changelogUrl": "<approved-url>",
  "keywords": ["unity", "game-framework", "runtime"],
  "samples": []
}
```

如果文档只随 package 分发，可以使用 `Documentation~`；如果使用 URL，必须对私有 NAS/离线 Consumer 的可访问性有明确说明。`samples` 只登记已经自包含并通过 Import smoke 的 `Samples~` 子目录。

### 3.3 Metadata gate

发布检查必须验证：

- package name/version/unity 合法；
- README/CHANGELOG/LICENSE/Third Party Notices 存在；
- manifest URL 可选但一旦填写必须为批准地址；
- changelog 含目标版本，不允许目标版本只留在 `[Unreleased]`；
- dependency/support matrix 与实际 Consumer lock 一致；
- package 不含机器缓存、个人设置、绝对路径或未授权制品。

## 4. 版本与 tag 权威

正式候选保持已有等式，并增加 resolved commit：

```text
package.json version
  == Git tag without "v"
  == release receipt version
  == Consumer observed package version

tag target commit
  == Consumer resolved commit
  == release receipt candidate commit
```

### 4.1 Tag 流程

1. 在干净 candidate commit 冻结 package version 与 changelog。
2. 对完整 SHA 运行 pinned release matrix。
3. 生成 candidate receipt，但尚不标记 tag verified。
4. 创建 `v<package-version>` tag；默认使用 annotated tag，并记录 tag object/target commit。
5. Runner 以 tag URL 解析，不允许调用方先把 tag 手工换算成另一个 SHA 后冒充 tag smoke。
6. 从 Consumer lock 读取 resolved commit，要求等于 tag target commit。
7. 从 PackageCache 读取 observed version，要求等于 tag。
8. 生成 tag receipt；全部通过后才允许宣布发布。

当前 runner 拒绝非 40 位 revision，这是 pinned-SHA gate 的正确行为。应新增独立 source mode，而不是放宽现有模式：

```text
-PackageSource TaggedGit
-Tag v0.8.0-preview.2
```

TaggedGit 必须：

- 拒绝 branch name；
- 读取并记录 tag object 和 peeled commit；
- 验证 tag 没有在运行期间移动；
- 继续检查 lock full hash；
- 阻止 tag/version 不一致。

禁止删除并重建已发布 tag。错误 tag 通过新版本修正。

## 5. Consumer Player gate

当前只完成 build-only 第一层：fixture 中的最小场景和 Runtime Probe 由 Unity 生成/导入，Local base 已有 Windows Standalone 构建证据。它证明 runtime Consumer 脚本和 package runtime assemblies 能进入 Player 构建，并检查 Editor-only 引用没有直接破坏该目标；它不等同于下面定义的 build + run 完整门禁。

EditMode compile 无法证明：

- runtime assembly 进入 Player；
- Editor-only reference 没有泄漏；
- IL2CPP/Mono stripping、native plugin、graphics backend 或 platform define 正确；
- package runtime 能在非 Editor 生命周期启动。

### 5.1 最小 Player fixture

Consumer fixture 增加由 Unity Editor/MCP 或受控生成器创建的最小场景，不能手写 `.unity` YAML：

```text
PackageConsumerPlayerSmoke
  -> RuntimeHost bootstrap
  -> register probe module
  -> Initialize / Start / fixed Tick / Stop / Dispose
  -> write deterministic completion receipt
  -> exit 0
```

场景不得引用 WGameFramework 主工程 Config、scene、resource、FGUI 或 FMOD bank。

### 5.2 Player matrix

Preview baseline 最低：

- Windows Standalone base build + run；
- 与声明 Player support 的 optional adapter matching build；
- build log 0 error；
- runtime log 0 exception/`LogError`；
- receipt 的 package version/candidate SHA 与 compile matrix 相同。

Stable 前按实际支持声明增加 macOS/Linux/Android 等目标。没有证据的平台标记为 `not-yet-validated`，不能由“Unity 理论支持”自动推导。

FMOD、VFX Graph 和 Transport Player gate 的具体深度由 optional support tier 决定：O2 adapter 不要求进入 stable Player support list；一旦声明 O3，就必须有对应 Player 行。

## 6. N-1 升级矩阵

### 6.1 需要区分的兼容域

Package upgrade 不等于所有 schema 自动兼容。矩阵分别记录：

| 域 | Authority | Upgrade assertion |
| --- | --- | --- |
| UPM package | package version/tag | 能解析、编译、API sentinel 通过 |
| Public C# API | API standards/interfaces | 兼容或有 migration note/deprecation |
| Config/catalog JSON | 对应 schema version | old fixture 可迁移或明确拒绝 |
| SaveState | SaveState header/schema | N-1 fixture 可加载/迁移或 fail-closed |
| Network/wire | 对应 protocol version | 独立兼容政策，不由 package version 替代 |
| Unity serialized assets | GUID/type/serialization | sample/fixture 引用不丢失 |

### 6.2 Upgrade Consumer 流程

建立两个控制组和一个真实升级组：

```text
Fresh N-1
Fresh N
Upgrade N-1 -> N using the same consumer project and fixtures
```

步骤：

1. 从已发布的 N-1 tag/SHA 创建 clean Consumer。
2. 编译并运行 API sentinel；生成 N-1 config/SaveState/serialized sample fixtures。
3. 保存 lock、package version、fixture hash 和行为结果。
4. 只把 MxFramework reference 升到候选 N；optional package 版本按 support policy 固定或显式升级。
5. 保留项目 `Assets` 和规定的升级状态；删除哪些缓存由测试协议固定，不能每次随意选择。
6. 解析 N，验证 lock 没有意外 dependency 漂移。
7. 编译并运行相同 API sentinel。
8. 加载/迁移 N-1 fixtures，检查 exact success 或预期结构化拒绝。
9. 运行一次 fresh N 控制组，区分升级残留和 N 本身问题。
10. 生成 upgrade receipt。

### 6.3 N-1 选择

- Preview train：至少选择上一条“已晋升 preview baseline”，而不是任意开发 commit；
- RC/stable：选择上一公开 release train；首个 `1.0.0` 至少从最后一个批准 preview/RC 升级；
- 如果跨过多个 schema 版本，按支持政策增加 oldest-supported -> N；
- 不能用当前源码复制一份并改 version 字符串伪造 N-1。

### 6.4 API sentinel

Consumer-owned 编译 sentinel 只使用承诺的 public API，放在 Consumer fixture 而不是 package 测试里。它至少覆盖：

- RuntimeHost 组合根；
- Core/Events/Config 等稳定基础入口；
- 已声明支持的 Unity adapter 入口；
- optional adapter 由 matching profile 单独覆盖。

发生预发布 breaking change 时，提升相应 minor/preview train并提供 migration note；stable 后遵守 SemVer major/deprecation policy。

## 7. Release receipt

建议 schema：

```json
{
  "schemaVersion": 1,
  "package": {
    "name": "com.vincent.wgameframework",
    "version": "1.0.0",
    "candidateCommit": "<sha>",
    "tag": "v1.0.0",
    "tagTargetCommit": "<sha>",
    "packageTreeHash": "<hash>"
  },
  "unity": {
    "editorVersion": "6000.5.0f1",
    "validatedPlatforms": ["WindowsStandalone64"]
  },
  "gates": {
    "consumerMatrix": "<receipt-hash>",
    "playerMatrix": "<receipt-hash>",
    "upgradeMatrix": "<receipt-hash>",
    "licenseAudit": "<receipt-hash>"
  },
  "result": "passed"
}
```

Receipt 必须：

- 稳定排序和 UTF-8 无 BOM；
- 无绝对路径、用户名、token、license key、Unity serial、时间戳漂移；
- 引用原始 artifact hash；
- 能由脚本重新校验 tag/package/report 一致性；
- 失败候选保留外部日志，但不能生成 `result=passed` receipt。

## 8. Preview gate

新的 reviewed preview baseline 至少要求：

- E0 static boundary；
- 当前 required Local/PinnedGit Consumer matrix；
- package README/CHANGELOG 与目标 preview version 一致；
- optional support matrix 与实际 profile 一致；
- baseline receipt 受版本控制；
- 至少 Windows base Player build/run；
- 从上一 approved preview 的 API compile upgrade；
- schema 有变化时增加对应 config/SaveState migration fixture；
- 不要求所有可选 adapter 达到 O3，但支持等级必须如实标记。

## 9. Stable `1.0.0` gate

除 preview gate 外，至少要求：

- package surface 收窄完成，CharacterTest/仓库聚合 Tests 不再随包安装；
- LICENSE、Third Party Notices、Documentation、完整 changelog；
- public API compatibility/deprecation policy 已接受；
- 所有“支持”的 optional adapter 达到声明 tier；
- supported platform Player matrix；
- 最后 preview/RC 到 `1.0.0` 的 package/config/SaveState upgrade matrix；
- `v1.0.0` TaggedGit smoke；
- tag、commit、package version、PackageCache observed version、release receipt 全一致；
- 至少一个真实外部项目通过 tag/SHA 消费，而不是复制源码；
- 没有未解释的 warnings、compile errors、resolution failures、`LogError` 或 skipped required tests。

## 10. 失败与回滚

- 候选失败：修复后创建新 commit，旧 SHA receipt 标记 failed，不重写历史。
- Preview tag 失败：不移动 tag；提升 preview 序号并创建新 tag。
- Stable tag 发布后发现问题：发布 patch，不重建 `v1.0.0`。
- Upgrade 失败：先判断 API、config、SaveState、serialized asset 或 dependency resolution 域；不得只清 Library 抹掉真实升级问题。
- Player 失败但 EditMode 通过：保持 candidate 未发布，不能把 Player 标为“环境问题”后跳过；只有明确不支持该平台时才能从支持矩阵移除，并记录 owner 决策。
- License/third-party 边界不清：阻断对外发布，但不阻断本地 private development；文档必须明确 private-only。

## 11. 完成定义

- package legal/metadata 文件完整且 owner 已批准；
- runner 支持独立 TaggedGit 模式；
- Consumer Player fixture 能构建并运行；
- N-1 -> N upgrade runner 覆盖 API、Config、SaveState 和 serialized fixture 中适用的域；
- preview/stable receipt schema 可机器校验；
- 每个发布版本都有 immutable candidate、tag target、observed package version 和 receipt 一致证据；
- 支持平台和 optional integration tier 与实际 Player/behavior 证据一致；
- `1.0.0` 只在全部 stable gate 通过后创建和宣布。
