# 项目打包系统逐项检查与优化计划

> Status: Current
> 本机产物保留：2026-09-29 按用户要求清理旧包，仅保留 `E:/WGFBuilds/IL2CPP-Release-20260929-Ready/`；文中其它构建路径属于历史证据记录，部分已删除，见 §14.5。
> v0.4 Player 玩法修复已验收；v0.5 Windows x64 IL2CPP 常规发布配置、Development 和单独 Warm Run 已完成专项验收，见 §13。
> v0.6 概览细查已实现并验证；未重新构建 Player，沿用 v0.5 的实测验收。
> Version: 0.6.1 | 2026-09-29；保留既有 Mono 基线，追加 IL2CPP 发布与开发预设。
> v0.2 评审基线：源码复核、隔离判定复算和本地产物核验已完成；当时未运行 Unity 构建与 Player。§1–§4、§8 保留评审时证据，§5 为验收合同，最终实施结果见 §10。
> 实施状态：A/B/C/D 已落地；完整打包菜单、单独 Warm Run 和真实 Unity BatchMode 脚本均有成功证据，见 §10。§9 保留此前失败尝试及修复过程。
> 检查基线：`6b534ea93` 加当前工作区；工作区存在其他未提交改动，不视为该提交的纯净验收。

## 1. 结论与范围

现有实现已经连接资源计划、AssetBundle 构建、CharacterTest Development Player 构建和运行报告。保留这些已有职责边界，优先修复最终验收结果丢失和目标平台不一致，再收敛构建输入准备及残留文件管理。

上一轮提出的三项问题逐项复核结果：

| 编号 | 问题 | 本次结论 | 优先级 / 证据 |
| --- | --- | --- | --- |
| PKG-01 | 二进制目录加载失败没有进入最终打包结果 | 已通过当前判定函数的隔离复算确认；三类应纳入二进制验收的负样本仍返回通过 | P1；函数级行为已复现 |
| PKG-02 | Bundle 与 Player 的目标平台来源不同 | 两套目标选择及缺少前置一致性检查已由源码确认；跨平台错配的实际构建结果尚未实跑 | P1；条件风险，静态证据明确 |
| PKG-03 | 旧 Bundle 未清理、未报告 | 当前清单之外确有一个文件；没有找到可证明该文件生成归属的历史回执，因此不能直接认定可删除 | P2；文件事实已确认，清理归属待证 |
| PKG-04 | 构建入口的输入准备与保存顺序不同 | 完整打包入口与 Warm Run 入口准备项不同；完整入口构建 Bundle 后，Player 构建函数才保存所有脏场景 | P2；本次追加的相关静态发现 |

v0.2 评审时的范围：游戏资源包、CharacterTest Windows64 Development Player、PowerShell 打包入口及其验收。UPM 包分发、正式 Release/IL2CPP、Android/WebGL、签名、商店分发和 CDN 更新不是本方案新增目标。现有文档也没有承诺这些能力，不能把它们缺席计为本轮缺陷。[当前使用范围][usage] 用户后续授权追加的 Windows IL2CPP Release/Development 配置与实测另见 §13。

本文不判断构建速度、增量构建收益、启动哈希开销或整体维护成本；没有本次可比测量支持这些结论。

## 2. 权威与已有链路

- 当前资源行为：[RESOURCE_SYSTEM_WORKFLOW.md][workflow]、[Interfaces/Resources.md][resources-interface]。
- 当前打包入口：[USAGE.md 的 CharacterTest Windows Package Test][usage]。
- 验收原则：[QUALITY_GATE.md][quality]；后续源码修改继续遵循所在目录 `AGENTS.md`。
- 本文描述检查快照和拟实施修改；不覆盖 Current 接口，不把拟议状态字段写成现有协议。

### 2.1 全局资源构建

```text
producer snapshots / Resource Manager 手工快照
  -> global_resource_mapping_profile.v2.json
  -> GlobalResourceBuildPlanCompiler
  -> Unity GUID / 路径 / 类型 / dependency hash 解析
  -> Temp 内构建与暂存
  -> 发布 catalog / mounts / preload / dependencies / bundles
  -> 最后发布 global_resource_artifact_set.json
  -> Offline bootstrap 校验身份与文件 SHA-256
  -> ResourceManager / SceneFlow 消费
```

### 2.2 完整打包与单独 Warm Run

```text
build_character_test_player.ps1 / Run Windows Package Test
  -> 选择宿主平台对应的 Player 目标
  -> 构建全局资源（读取 Unity activeBuildTarget；可跳过）
  -> 暂存默认角色包 JSON / bytes
  -> BuildPlayer
       -> Guidance / Audio 检查
       -> 准备输出目录，保存当前打开的脏场景
       -> 检查 Play Mode，刷新运行目录镜像
       -> 临时使用 Mono，构建 Development Player，恢复后端设置
  -> RunSynchronous
  -> EvaluatePlayerRunReports
  -> summary / Unity exit code / PowerShell exit code

Build And Run Player Warm Run
  -> 直接 BuildPlayer
  -> 运行与报告
```

两个入口共用 Player 构建函数，但单独 Warm Run 没有调用全局资源构建和默认角色包暂存。图中顺序反映检查时的源码，不是推荐的新顺序。[打包命令][package-command]、[Player 构建][player-build]

## 3. 逐项检查表

“源码已核对”不等于本次测试通过；“已有测试”只说明检查过对应测试代码。

| 检查项 | 已核对事实与反证 | 结论 / 处理 |
| --- | --- | --- |
| 01 构建权威 | Unity Builder 使用 v2 Profile 和共享 Compiler；没有为了 Player 再建目录模型 | 保留；不新增第二套资源管理器 |
| 02 Unity 源解析 | GUID、路径、主资源类型与 dependency hash 进入解析；非独立 `.anim` 的动画输入有拒绝分支 | 源码已核对；本轮没有在 Unity 中重算当前 source fingerprint |
| 03 Bundle 计划约束 | Compiler 检查具体目标、规则目标匹配及混合压缩；现有测试检查 Editor/Player 的 internal key 与 Bundle 成员对应 | 保留；这些约束只覆盖资源计划内部，不解决 PKG-02 |
| 04 整批产物完整性 | Builder 暂存后逐文件发布，marker 最后写入；Loader 校验必需种类、路径、哈希；有部分/混合产物测试 | 保留；这不是整目录回滚事务，不能宣传“中断后旧版本始终可运行” |
| 05 当前落地产物 | 24 个清单文件均存在且哈希匹配，report/marker 六项身份字段一致 | 本次实际核验通过，详见 §4；不证明源码最新性 |
| 06 外部目录 | 代码有 mount allowlist 与外部目录加载测试；当前构建报告 External=0 | 本轮未验证实际外部包构建，不扩展支持声明 |
| 07 角色包暂存 | 校验 packageId、源/目标分离和链接目录；socket bake 复制前后校验；按明确路径排除六项产物 | 保留；默认暂存根目前为角色 `000001`，不能宣传成所有角色的打包验证 |
| 08 运行目录镜像 | 必需 JSON/bytes 缺失会阻断；复制比较文件字节。镜像刷新本身不重编源目录，也不能代替二进制读取验收 | PKG-01；保留已有校验器和 Runtime fallback 行为 |
| 09 构建配置 | 显式 CharacterTestScene、Development、临时 Mono，并在 finally 恢复后端 | 保留开发包定位；不是 Release 或 IL2CPP 证据 |
| 10 目标平台 | Bundle 读 activeBuildTarget；Player 按宿主系统选择；脚本未指定目标 | PKG-02 |
| 11 最终验收 | 动画 ready、报告文件及运行 finished 会检查；目录加载判定和进程结果没有完整汇入最终 verdict | PKG-01；七组隔离输入见 §5.1 |
| 12 入口准备 / 场景保存 | 单独 Warm Run 缺两项准备；完整入口的场景保存发生在资源构建之后，且保存所有打开的有路径脏场景 | PKG-04；不能认定现有产物已经因此不一致 |
| 13 残留与清理 | Builder 只发布本次文件；当前有一个未列入 marker 的 Bundle；工作流已登记清理事项 | PKG-03；清单外文件先报告，归属确定后才能清理 |
| 14 自动化与诊断模式 | 有 PowerShell/BatchMode 入口；`NoRun` 可以跳过运行，`SkipResourceBuild` 可以复用旧资源 | 保留选项；结果必须带验收范围，不能等同完整构建运行证明 |

主要证据：[Unity 源解析][mapping-builder]、[共享 Compiler][compiler]、[资源 Builder][resource-builder]、[产物 Loader][artifact-loader]、[资源构建测试][builder-tests]、[产物加载测试][loader-tests]、[目录镜像][catalog-mirror]。

## 4. 本地产物快照

复核时间：2026-09-28 22:50（Asia/Shanghai）。marker 修改时间：2026-09-28 15:31:47。该时间是本机文件时间，不是发布版本号。

| 项目 | 实测值 |
| --- | --- |
| 目标 | `StandaloneWindows64` |
| 构建报告计数 | Internal 215 / External 0 / EditorOnly 10 / Excluded 1 |
| 运行目录条目 | 215，其中 Scene 条目 1 |
| marker 文件数 | 24，其中 Bundle 20、目录与辅助 JSON 4 |
| 清单文件总字节 | 50,790,549 B；不是完整 Player 包体 |
| 文件缺失 / SHA-256 不匹配 | 0 / 0 |
| report 与 marker 身份不匹配 | 0；比较 profileId、catalogId、buildTarget 和三个 fingerprint |
| Bundle 目录中的额外非 `.meta` 文件 | 1 个，251,915 B，约 246.01 KiB |

当前 fingerprint：

```text
mappingInputFingerprint = sha256:402814b7a46222aec32aa1fa3750216ae227d6193284e16316dc75a57181bf70
buildPlanFingerprint    = sha256:ceb9f08247119118baa5d213faf8e52ffe134763aca0faa7ac17f39a8417acf3
sourceInputFingerprint  = sha256:02a8385d17ad41e88e32b6bef59d54c10c90050bcbd5b96a63b3c456cae1f2a0
```

额外文件相对路径：

```text
Assets/StreamingAssets/MxFramework/Resources/Bundles/StandaloneWindows64/global.character.iron_vanguard.assetbundle
```

该文件被 Git 忽略；对当前 Assets 配置/StreamingAssets JSON、框架源码、Tools 和 Docs 的限定文本检索未发现该完整文件名的引用。这里只能证明它未被当前清单引用，不能证明没有外部工具使用、历史生成归属或可以安全删除。本轮没有删除它。

核验方式：读取 marker → 检查每个相对路径存在 → 实际读取文件计算 SHA-256 → 比较 report/marker 身份 → 枚举 Bundle 目录与清单做集合差。没有调用 Unity 构建器或刷新资源。

## 5. 问题与拟实施方案

### 5.1 PKG-01：统一最终验收判定

**事实与触发条件**

- `CharacterTestPerformancePlayerBuildRun.ResolvePlayerRunCompletionStatus` 能产生 `finished-binary-catalog-miss`，这是已有二进制目录验收信息。[运行监督器][player-run]（当前第 183 行）
- `CharacterTestPackageTestCommand.Run` 调用 `RunSynchronous` 后忽略返回对象，且不读取 Editor build-run 汇总的终止状态，而重新调用 `EvaluatePlayerRunReports` 决定 `Passed`。[打包命令][package-command]（第 155、197 行）
- 该 evaluator 检查 Player 报告 `finished`、timing/profiler 文件存在、animation `ready`，不读取目录加载诊断；PowerShell 继续消费这个 summary。[脚本][build-script]
- 现有 `CharacterTestRuntimeCatalogLoadReportParser.BuildSummary` 在报告提供计数、missing/nonBinary 列表和 `allExpectedBinary` 时优先采用自报值；即使逐种类记录缺失或为 JSON，也可能保留自报的 `AllExpectedBinary=true`。因此，仅把现有 summary 接入最终 verdict 仍不足以关闭缺口。[目录报告解析器][catalog-parser]（检查时第 400–449 行）
- 运行监督器记录退出码和强制结束状态，但最终 evaluator 不读取它们。进程在写完报告后异常退出、或者必须强制结束时，文件层通过不等于进程正常结束；本次仅源码确认这一结果传递缺口，没有故障进程实跑。

**隔离复算**

2026-09-28，从当前源文件逐字抽取 evaluator、`ReadKeyValueFile`、`ResolveReportPath`、verdict struct，以及实际路径解析函数和报告文件名常量，在 PowerShell 进程内编译。仅在操作系统临时目录创建文本 fixture；没有载入 Unity、运行 Player 或改动项目资产。

| 输入 | 当前函数结果 | 结论 |
| --- | --- | --- |
| finished + 报告齐全 + animation ready + allExpectedBinary=true | Passed | 正向对照 |
| 同上，但 allExpectedBinary=false、JSON fallback 1 项 | Passed | 二进制验收信息被忽略 |
| 同上，但 allExpectedBinary=false、missingExpectedKinds 非空 | Passed | 必需目录缺失信息被忽略 |
| finished + 报告齐全 + animation ready，完全不提供目录诊断 | Passed | 没有目录证据仍可放行 |
| Player status=failed | Failed | 现有运行状态检查有效 |
| animation status=failed | Failed | 现有动画检查有效 |
| 缺 timing 文件 | Failed | 现有报告存在性检查有效 |

被抽取文件 SHA-256：`9be702b7cd8c8551aeeb59f8eafd2095a70cd9832b9f445dab93a08723cdfd06`。本机复算记录：`%TEMP%/WGamePackagingAudit-1818b005c7b94e34bf0df68c7242224e/audit-result.json`；该记录不是版本化验收产物，关键结果已固定在上表。

反证与边界：现有 [PackageTestCommand_EvaluatePlayerRunReportsRequiresFinishedReports 测试][package-tests] 确实覆盖运行失败、缺文件和动画失败；不能说系统没有验收。现有 Runtime 允许的 JSON fallback 也不应因此删除。要修复的是严格打包验收对这些证据的消费方式。

**优化要求**

1. 在项目 Editor 层形成一个最终验收结果，菜单、BatchMode、summary、脚本都投影这个结果；沿用现有 manifest 和报告格式，在现有 parser 或其严格验收入口补齐逐条记录校验，不另建目录权威。不能只读取现有 parser 的 `AllExpectedBinary`。
2. 完整运行验收要求：构建成功、Player 正常退出、未超时/未被强制结束、run=finished、报告齐全、animation=ready、所有预期目录实际命中 Binary。
3. 预期种类集合 E 由本次构建所用 `CharacterTestRuntimeCatalogArtifactManifest` 中 `RequiresPlayerBinary=true` 的条目确定，不能由报告的 `expectedCount` 缩小。逐条读取 `runtimeCatalogLoad.<index>.kind/.format`：每个预期种类必须恰有一条完整记录，且 format 必须是 Binary。缺记录、缺字段、未知格式、重复预期种类或重复记录字段均拒绝；不能用 Dictionary 的后写覆盖掩盖矛盾。非预期种类不能充当预期种类或补足计数。
4. 从 E 与逐种类记录独立计算 expected/binary/json/embedded 计数、missing/nonBinary 集合及 allExpectedBinary，再与报告提供的汇总字段逐项交叉核对；列表按集合语义比较。汇总与记录矛盾、非法计数或缺少严格验收所需的记录均拒绝，不能回退到自报汇总。完整通过要求 E 全部命中 Binary；自报 true、空 missing 列表或 expectedCount=0 均不能豁免逐条证据。
5. `NoRun` 保留为构建模式，结果明确标识“未运行”；它可以按所选模式成功，但不能证明完整 Player 运行通过。`SkipResourceBuild` 记录资源复用及所用产物身份，进入 PKG-02/04 的复用校验。
6. 保留 Runtime fallback 的用途；严格打包验收发现 fallback 后输出具体目录、路径、原因。源 binary 失效时复用既有导出/验证路径，不在验收中悄悄造一份新源数据。

**验收标准**

- 上表三种目录负样本（回退、缺项、无诊断）必须从 Passed 变为失败或缺证据阻断；运行失败、动画失败和缺 timing 文件仍须拒绝。完整逐条正向样本按下述新合同补齐。
- v0.1 七组复算中的“正向对照”仅证明旧 evaluator 的分支行为，其 fixture 只自报 allExpectedBinary=true，没有逐种类记录；它不是新合同的正向样本。新正向样本必须包含 E 中每个种类的完整 Binary 记录及与记录一致的汇总。
- 报告写完后进程非零退出、超时和强杀分别有负样本，不能返回完整通过。
- 菜单、summary、脚本的同一次结果一致；按所选模式失败时退出码非零。
- 有一次实际 Windows64 完整构建运行正向证据，且二进制目录数量来自当前 manifest，不能硬编码本次数量。

以下是 v0.2 新增的正式回归要求，本轮仅完成源码复核和合同补充，未将这些样本记为已执行测试：

| 输入（run=finished，其他报告及动画检查均通过） | 严格验收预期 |
| --- | --- |
| E 的记录齐全且全为 Binary，计数与列表均与记录一致 | 通过 |
| 自报 allExpectedBinary=true、binaryCount=expectedCount、missing/nonBinary=none，但缺一个预期种类记录 | 拒绝；指出实际缺失种类和汇总矛盾 |
| 同样自报全部 Binary，但某预期种类的记录为 Json 或 Embedded | 拒绝；指出实际格式和汇总矛盾 |
| 自报 expectedCount=0、allExpectedBinary=true，实际 E 非空且无记录 | 拒绝；不能缩小 manifest 的预期集合 |
| E 的记录全为 Binary，但汇总 binaryCount 错误或 nonBinary 列表非空 | 拒绝；不能只验证单条记录而忽略矛盾汇总 |
| 同一预期种类同时出现 Binary/Json，或同一索引的 format 被重复写入 | 拒绝；不得以最后一条覆盖前一条 |
| 用额外非预期种类补足条数，但一个预期种类缺失 | 拒绝；比较种类集合，而非仅比较总数 |

### 5.2 PKG-02：让资源与 Player 共用明确目标

**事实与触发条件**

- Player 的 `ResolveDefaultBuildTarget` 在 Windows 宿主选择 `StandaloneWindows64`。[Player 构建][player-build]（第 109 行）
- 资源输入编译和 Bundle emission 使用 `EditorUserBuildSettings.activeBuildTarget`。[源解析][mapping-builder]（第 49 行）、[资源 Builder][resource-builder]（第 105 行）
- Windows 脚本未传入构建目标，也没有在进入资源构建前拒绝不一致的 active target。[脚本][build-script]（第 130 行）
- 资源 Builder 的既有检查比较“Plan 目标”和“active target”，不能比较它尚不知道的 Player 目标。

触发例：Windows 宿主的 Editor 当前目标是另一平台，执行默认打包命令。代码会先以该 active target 构建资源，再请求 Windows64 Player；可能因目标支持或后续加载失败而中止，具体失败点未实跑。本机当前 marker 为 Windows64，不应写成本机已发生此故障。

**优化要求**

1. Windows 专项入口确定唯一 expected target=StandaloneWindows64；在任何资源输出、暂存、输出目录清理和设置改动之前核对。
2. BatchMode 启动参数显式指定目标，启动后仍回读核对。Editor 菜单遇到不一致先阻断并显示当前/预期目标；首轮不在函数中隐式切换并跨 domain reload 继续旧调用栈。
3. Compiler、Builder、Player 构建和报告均使用并记录同一个目标。Builder 的既有防线保留。
4. `SkipResourceBuild` 至少验证 marker/report、每个清单文件哈希及 expected target；缺失、损坏或其他平台产物均阻断。
5. 创建 Offline 运行资源服务时明确传递或核对实际 Player 目标，避免目标缺省时只用 marker 自述值完成内部一致性检查。

**验收标准**

- active target 与预期不同时，资源构建、暂存、清目录、Player 构建调用次数均为 0。
- 缺模块或无法确定目标时给出清晰阻断，不静默降级。
- 正向构建的 plan、marker、report、Player 四者目标一致。
- 正常同目标复用通过；其他平台资源复用失败；不要求安装和构建 Android/WebGL 才能完成守卫的负样本测试。

### 5.3 PKG-03：先可见，再按归属清理残留

**事实与边界**

当前未引用文件见 §4。`StageBundles` 和发布循环只处理本次计划，没有做旧文件差集清理；[资源工作流][workflow] 已将 stale-file cleanup 登记为 follow-up。因此这不是新增热更新需求。

251,915 B 只是当前目录额外占用；本轮没有测量完整 Player 中的贡献、压缩后大小或下载收益。不能把它放大为严重包体问题，也不能把所有清单外文件当作本构建器所有。

**优化要求**

1. 只读扫描结果区分：本代引用、历史同 profile/target 生成但本代不再引用、其他 target、归属未知。报告路径、大小和归属证据，Workbench 展示同一结果。
2. 对后续构建，在覆盖 marker 前持久化可校验的历史归属和跨代待清理台账。台账以 profile/target/规范化相对路径为归属键，并保留文件哈希、生成代身份、首次退出引用的代、处理状态、最近失败原因及处置证据。新代计算的是“尚未完成处置的历史记录 ∪ 本次新增退役记录”，不能只用上一代 marker 的文件差集替换台账。
3. 待清理记录的删除资格必须绑定已经成功提交的退役代；仅写入台账或发布了一部分文件不构成清理许可。构建/发布中断与进程重启后，记录仍须可恢复，并核对已提交 marker 后再决定是否可清理。记录保留到成功清理，或有明确原因与证据的处置（例如文件重新成为当前代引用、人工转移所有权）；失败、重试次数耗尽或新代提交均不能静默丢弃归属。成功处置的结果保留在清理回执中。
4. “不在当前 marker”不是删除许可。当前这个遗留文件如无历史回执，维持未知归属并提示人工核对；本方案不安排自动删除。
5. 清理范围限制在该生成器拥有的 Bundle 子树，验证规范化路径与链接目录；保留其他 profile/target、ExternalBundles 和手工资源。执行前再次排除当前代引用，并核对待清理文件与归属记录的哈希；路径被重用或内容变化时停止该项清理，不能凭旧路径删除新文件。
6. Unity Assets 内的清理通过 Unity Editor/仓库生成器执行，处理相关 `.meta`。清理失败单独报告，不修改新一代 marker 来掩盖失败。台账属于生成器本机状态/构建证据，不进入 Runtime 资源权威；台账缺失或损坏时停止自动清理，不凭目录扫描重造所有权。

**验收标准**

- 构建 A 包含 X/Y，构建 B 仅包含 Y：B 提交前不得删除 A 的文件；B 成功后只把有归属证明的 X 列为可清理。
- A→B→C：A 包含 X/Y，B 不再引用 X 且清理 X 失败；即使 B 的 marker 已无 X，提交 C 并重启清理进程后仍能找到 X 的 A 代归属、B 代退役和失败记录。C 成功后可继续重试，成功后才记录为已处置。
- C 重新引用 X 时不得执行旧清理任务；X 的字节被其他流程替换时也不得凭旧归属删除。两种情况均留下可解释的处置/阻断记录。
- 构建或发布失败不执行旧代清理。
- 手工文件、未知归属文件、其他 target/profile 和外部包均保留。
- 重复扫描/清理幂等；报告中“发现”“可清理”“已清理”“失败”可区分。

### 5.4 PKG-04：收敛构建输入准备与保存顺序

**事实与影响**

完整命令先 `BuildGlobalPlayerResources`，后进入 `BuildPlayer`；后者调用 `SaveDirtyScenesIfNeeded` 遍历所有打开的有路径脏场景，再检查是否处于 Play Mode。[打包命令][package-command]（第 126 行）、[Player 构建][player-build]（第 186、377 行）

这意味着资源源文件在 Bundle 构建后仍可能被保存。当前目录包含 `scene.character_test`，所以资源包与随后 Player 的场景输入存在先后变化的路径。是否已经发生差异，本轮没有运行证据。另一个已确认事实是单独 Warm Run 不构建全局资源、不暂存默认角色包，依赖已有准备状态。

`GlobalResourceMappingBuilder.SourceInputFingerprint` 由资源映射中 internal 条目的 Unity 源解析结果计算；角色包 JSON/bytes 暂存和 Player 运行目录镜像是两条独立复制路径。因此这个 fingerprint 不能单独证明它们没有变化。[源解析][mapping-builder]（检查时第 49–62 行）、[角色暂存][package-command]（第 366 行）、[目录镜像][catalog-mirror]（`Refresh`）

**输入身份核对范围**

本节保证限定为下表三类由该打包编排准备或复用的输入，在规定检查点之间身份一致；不声明捕获了 Unity 全部隐式输入、脚本/插件、Packages 或所有 Project Settings，也不声称实现了完全隔离、可复现的整个 Player 构建环境。

| 输入域 | 需固定的源身份 | 需验证的 Player 输入身份 |
| --- | --- | --- |
| G：全局资源 | profile/catalog、expected target、mapping/plan fingerprint，以及 Unity 重算的 sourceInputFingerprint | 已提交 artifact-set 身份与完整清单；每个相对路径的实际 SHA-256；report/marker 一致 |
| C：本次暂存的角色包 | 从关卡启用角色 ID 与数字身份目录解析的 packageId 集合，加 `000001` 默认运行依赖；每包导入报告身份、按现有六项排除规则选出的完整相对路径集合及逐文件大小/SHA-256、socket bake 配对证据 | 对应 packageId 的实际暂存目录，按同一选择规则复算路径集合与逐文件哈希，并重新验证 socket bake 配对。增删文件同样属于输入变化 |
| R：Player 运行目录镜像 | 本次构建所用 ArtifactManifest 的 catalogKind/路径/RequiresPlayerBinary 选择集合，以及每个生成 JSON、必需 bytes 的大小/SHA-256；保留现有语义/二进制验证结果 | 按 manifest 指向的实际镜像 JSON/bytes 逐项核对。复制字节一致与源 binary 有效性分别记录，不能互相替代 |

三类身份写入同一次构建回执，以构建尝试身份和 expected target 关联。新增回执仅记录输入证据，不成为角色配置、资源目录或运行协议的第二份权威；包清单/目录清单以现有选择器和 manifest 为准，不能在回执代码里另写一套选择规则。

**优化要求**

1. 先检查 Editor 是否允许构建、目标和相关输入是否已保存，再生成资源计划。首轮对相关脏场景明确阻断，由现有 authoring 保存权威完成保存；不要在打包深层函数里保存无关打开场景。
2. 完整打包与单独 Warm Run 复用项目 Editor 层的输入检查。保留两种清晰模式：构建所需资源；复用已验证、匹配当前输入的资源。
3. 复用时在 Unity 中重新解析 G 域 source fingerprint，并核对 mapping/plan/target/清单哈希；C/R 域独立重算源文件集合和暂存/镜像字节身份。不能用 G 域 fingerprint、文件存在或修改时间替代 C/R 域检查，外部脚本也不能据此推断 Unity 源最新性。
4. 需要的角色包暂存、运行目录镜像检查集中在明确阶段；保留现有 packageId、安全路径、socket bake 配对校验和排除列表。
5. 在任何准备写入前记录三域源身份；准备完成后重新读取源身份，确认未漂移，再验证 G 的已提交产物及 C/R 的实际输出。紧邻调用 BuildPlayer 前再次核对三域源和输出；BuildPlayer 返回后、允许启动或给出构建成功验收前再核对一次。任一域发生变化都使本次证据失效，应阻断或重新准备并创建新的构建尝试，不得复用旧的通过标志。
6. 上述核对证明检查点之间未观察到身份差异，不排除并发写入后又恢复原字节的瞬时变化。严格构建期间需要禁止其他流程写入已固定的输入；不能保证写入隔离时，在回执注明该限制，不能升级为“整个 Player 输入快照绝对不变”的声明。

**验收标准**

- Play Mode 或相关脏场景情况下，在任何保存/资源构建/输出清理前阻断；无关场景保持原脏状态和磁盘内容。
- 单独 Warm Run 在缺资源或缺角色暂存时准确指出准备项；不得等 Player 启动后才以泛化缺资源错误失败。
- 复用时修改被引用资产或依赖，能识别 source fingerprint 变化；不通过清缓存掩盖问题。
- G/C/R 三域分别注入“准备过程中”“准备完成到 BuildPlayer 前”“BuildPlayer 执行中并在返回时仍存在”的变化；对应阶段阻断或拒绝本次构建验收，不变输入正常继续。注入不需要扩大本轮实际测试范围，这些是实现时的回归要求。
- G 域 fingerprint 保持不变，单独修改任一被选角色源包 JSON、bytes 或加入一个可暂存文件，C 域必须识别；修改实际暂存文件而源文件不变，也必须识别。`000001` 默认依赖的证据不能覆盖关卡选择的其他角色包。
- G 域 fingerprint 保持不变，单独修改运行目录源 JSON/bytes 或镜像文件，R 域必须识别；缺少必需文件、改变 RequiresPlayerBinary 选择均不能复用旧回执。

## 6. 分批实施与文件落点

下表保留 v0.2 的实施顺序，并记录 2026-09-29 最终状态；详细证据见 §10。

| 批次 | 对应问题 | 主要落点 | 当前状态与完成条件 |
| --- | --- | --- | --- |
| A：验收结果收敛 | PKG-01 | `CharacterTestPerformancePlayerBuildRun`、`CharacterTestPackageTestCommand`、目录报告 parser、PowerShell | 完整菜单、Warm Run、真实 BatchMode 均通过；manifest 要求的 10 个目录逐条 Binary，进程与动画验收通过 |
| B：目标守卫 | PKG-02 | PowerShell 参数、PackageTest/WarmRun 前置检查、Player 资源组合入口 | 同目标资源与 Player 构建通过，目标错配及清单负样本通过；BatchMode 参数和退出码已实跑 |
| C：准备阶段收敛 | PKG-04 | 项目 Editor 编排、角色暂存、镜像验证；复用 `GlobalResourceMappingBuilder` | 当前场景 3 个角色映射到 `000002/000003/000009`，加默认依赖 `000001`；四检查点回执、脏场景和 G/C/R 漂移验证通过 |
| D：残留可见与归属清理 | PKG-03 | Global Builder/Workbench 报告与本机跨代台账 | A→B→C、发布中断恢复、失败重试、重新引用、哈希/路径保护和 Unity `.meta` 清理通过；真实未知归属文件继续保留 |

实现继续保持依赖方向：项目 Editor 可以组合框架 Editor API；框架包不能引用 CharacterTest。外部脚本启动和消费 Unity 构建结果，不承接 AssetDatabase 权威。

报告字段若有变化，先确定兼容策略，再同步 PowerShell、测试、`USAGE.md`；资源输出或清理行为落地后再更新 `RESOURCE_SYSTEM_WORKFLOW.md`。本轮不提前修改 Current 文档来宣称新能力已存在。

## 7. 验证与交付边界

以下两个“本轮”清单保留 v0.2 评审阶段的历史状态，不代表 2026-09-29 的实施结果；最新结果见 §10。

### 本轮已完成

- [x] 对三项原评审发现复核源码调用链、保护分支和反证。
- [x] 追加核对入口准备与场景保存顺序。
- [x] 隔离执行当前 evaluator 的七组输入；没有修改函数逻辑。
- [x] 实际计算现有 24 个产物的 SHA-256 并比较身份字段。
- [x] 检查额外 Bundle、Git 忽略状态及限定范围的文本引用。
- [x] 检查现有构建、目录加载与打包判定测试源码。
- [x] 形成优化计划并加入项目任务路由。

### 本轮未执行 / 后续实现验收

- [ ] Unity Editor refresh/compile 和 Console 验收；本轮没有 C# 修改，因此未触发。
- [ ] Unity focused tests；已有测试不能凭源码阅读记为通过。
- [ ] 当前工作区的完整 Windows64 Player 构建、启动与退出。
- [ ] 跨目标守卫、发布中断、进程异常退出的实际故障注入。
- [ ] 当前所有 Unity 源资源与依赖的最新 fingerprint 重算。
- [ ] 残留文件生成归属确认、清理和包体收益测量。

后续每批实现先执行根 `AGENTS.md` 的 Unity 编译验证，再按本专项命中的失败路径运行 focused 验收。完整构建会生成本机资源和报告；应记录构建前后状态，不能把打包过程误称为只读测试。不要为本文的 Markdown 修改运行 Unity 或整仓测试。

本轮没有修改源码、场景、资产、Project Settings 或已有构建产物；没有执行清理。计划完成判据是以上问题对应的正反验收证据齐全，不能以“文档已写完”“Editor 可以播放”或一次仅构建模式成功代替。

## 8. v0.2 评审意见处理

2026-09-28 复核评审意见后，仅修改本 Design 文档；原七组函数复算和 §4 产物快照仍为此前证据，没有将新要求写成已通过测试。

| 意见 | 文档处理 | 尚待实现验收 |
| --- | --- | --- |
| P1：汇总声称全 Binary，但逐种类记录矛盾 | §5.1 以 manifest 预期集合和实际记录重算；新增缺记录、Json/Embedded、计数矛盾、重复与额外种类样本；限定旧正向 fixture 的证明范围 | 严格解析、最终结果接线及新正反样本 |
| P2：sourceInputFingerprint 不覆盖角色和运行目录 | §5.4 明确 G/C/R 三域源/输出身份和构建前后检查点；限制“输入未变化”声明的范围 | 独立域回执、单域变化注入、真实 Player 构建证据 |
| P2：只留上一代会丢失清理失败归属 | §5.3 改为跨代持久化待清理台账；补 A→B→C 中 B 失败、重启恢复、C 重新引用和文件替换保护 | 台账恢复/处置及失败路径回归 |

## 9. 2026-09-29 第一轮实施与失败尝试记录（历史）

本次从约 484 项已有未提交改动开始，涉及美术、UI 和场景的其他会话工作；未 stash、覆盖、提交或推送。方案文档在开始时为未跟踪文件，已直接在同一文件续写。正式场景的 Dust 序列化改动属于并行工作，本次没有自动保存或修改该场景。

| 批次 | 实际实现 | 已执行验证 | 仍缺证据或阻断 |
| --- | --- | --- | --- |
| A / PKG-01 | 新增严格目录报告入口：manifest 决定预期种类，逐条记录、汇总计数与集合交叉核对；重复、未知格式、缺项和回退均拒绝。进程异常、超时或强杀不能通过；PackageTest summary、Warm Run 菜单与运行监督器消费同一最终 verdict。 | Unity 编译/reload/ready/Console 0 error；`PackageCatalogGate_RequiresManifestRecordsAndConsistentSummary` 1/1；`PackageTestCommand_EvaluatePlayerRunReportsRequiresFinishedReports` 1/1（补充重复字段后重跑）。实际 Player 目录加载 10/10 Binary、进程 exit 0；复用已构建 Player 再跑运行监督器时，最终 `acceptanceStatus: Failed` 与 animation 失败一致；脚本假启动器的正常与矛盾报告分支已测。 | 实际完整验收被动画报告阻断；真正的 Unity BatchMode 脚本入口未独立实跑。 |
| B / PKG-02 | Windows 专项固定 `StandaloneWindows64`，脚本传 `-buildTarget Win64`；写入前拒绝 active target 错配和缺模块。复用使用现有 artifact-set Loader 校验每个文件哈希，再比较 report/marker 与当前 plan/source 六项身份；Offline Player 默认核对实际运行平台。 | Unity 编译/reload/ready/Console 0 error；目标守卫 1/1、产物缺项/混代 Loader 1/1；实际全量资源及 Player 同为 Windows64。随后 Editor 只读调用 `ValidatePublishedArtifacts` 对当前产物通过。 | 未在真实异平台 Editor 执行构建负样本；局部目标错配测试证明提前抛错，未用写入计数探针。 |
| C / PKG-04 | Play Mode/相关脏场景前置阻断；移除 BuildPlayer 深层保存打开场景与镜像刷新。完整命令显式准备 G、默认 `000001` 的 C、R；Warm Run 复用三域已验证输出。回执按同一 attempt 记录准备前、准备后、BuildPlayer 前后源及实际输出路径/大小/SHA-256，并在每点重算 G 的 Unity source fingerprint。 | Unity 编译/reload/ready/Console 0 error；角色暂存文件集/字节漂移 1/1，R 源漂移 1/1、镜像漂移 1/1，G 生产输入解析 1/1。实际完整构建回执四检查点均存在并一致；全量 Bundle 与 Player 构建成功。Editor 内另执行单独 Warm Run 的 G/C/R 复用准备及 BuildPlayer 前复核，均通过，未再次构建 Player。 | 构建期间 G/C/R 的定点并发漂移注入、相关脏场景负样本及单独 Warm Run 全流程实跑尚未执行。检查点不能证明两次检查间瞬时写入后恢复。 |
| D / PKG-03 | 新增本机校验和台账，按 profile/target/相对路径记录当前归属、生成代、退役代、哈希、失败与处置回执。仅在 marker 成功提交后清理，删除前重查当前引用、规范路径、链接目录和哈希；Workbench 显示同源残留报告。 | Unity 编译/reload/ready/Console 0 error；跨 A→B→C、清理失败/重启恢复 1/1；重新引用、文件替换、未知归属、其他 target/profile 保护 1/1；旧 marker 迁移到新受信代与当前 marker 哈希损坏保护 1/1。真实资源 Builder 发布后生成 `Library/MxFramework/ResourceBundles/residue-ledger.json` 和 `residue-report.txt`，报告 1 个 unknown Bundle（251,915 B），文件仍在；Workbench 中有同一未知文件的可见 Label。 | 现有 marker 早于台账，当前迁移代为 `untrusted`，自动清理禁用；真实删除和发布中断注入未执行，不能从目录扫描推断旧文件归属。 |

完整构建两次尝试的证据：

1. 第一次全量资源发布成功；准备阶段的 R 语义校验阻断 Player 构建。`Temp/CharacterTestRuntimeCatalogLoadBenchmarkReport.txt` 指出 `mxframework.vfx.definitions` 的 JSON 53 条、bytes 50 条。通过仓库既有 `Repair Runtime Catalog Artifacts` 导出路径修复，`Temp/CharacterTestRuntimeCatalogRepairReport.txt` 为 `status: ready`、0 issue。
2. 第二次全量资源和 `StandaloneWindows64` Development Player 构建成功，`Temp/CharacterTestPackageTest/CharacterTestPackageInputReceipt.txt` 有四个检查点；Player exit code 0，run `finished`，逐条目录 10/10 Binary，timing/profiler 文件存在。`CharacterTestPackageTestSummary.txt` 最终为 `Failed`：animation `no character views are active`。Player 日志显示场景缓存的角色 ID `651590215`、`634812596`、`534146882` 均 `character-not-found`，启动角色数 0；当前包暂存只覆盖 `000001`，其中前两个 ID 对应生成包 `000002/000003`，第三个在当前生成包中未找到。没有把这次“构建成功”记作“完整 Player 验收通过”。

运行监督器最终 verdict 接线后，复用上述 Player 单独实跑，`Temp/CharacterTestPostFixPlayerRun-74b0fe093a5b4ac7a8ca232787d26a44/editor.txt` 记录进程 exit 0、Binary gate true、`acceptanceStatus: Failed`，原因仍是无活动角色视图；没有重新宣称构建成功的当前性。

PowerShell 入口另以 `Temp/PackagingScriptMock.ps1` 隔离验证：`NoRun` BuildOnly 和完整正向夹具均 exit 0，Binary gate 矛盾及重复 summary 字段均 exit 1；捕获参数含 `-buildTarget win64`。此夹具只证明脚本分支，不是 Unity BatchMode 构建证据。`win64` 参数值另按 [Unity 6 官方命令行文档](https://docs.unity3d.com/cn/current/Manual/EditorCommandLineArguments.html) 核对。

本节结束时尚待核定的角色包集合、失效 ID、完整运行、Warm Run 与 BatchMode 均在 §10 继续处理；本节的 Failed 状态只对应当时的尝试。D 的未知 Bundle 始终保留。本轮没有提交或推送 Git。

## 10. 2026-09-29 续实施：角色包闭环与完整验收

### 10.1 角色身份与构建输入

- 关卡 Authoring 源与 `CharacterTestScene` 缓存原先都保存 `player.local.01 = 534146882`。该值是 `000009` 的旧包名散列；当前 `Tools/RuntimeHash/character_definition_ids.json` 的 413 域把 `000009` 分配为 `668367837`，`Assets/MxFrameworkGenerated/CharacterPackages/000009/config/character_config_patch.json` 持久化同一值。`000009` 的 Authoring 包、生成包、`import_report` 和 Unity Editor receipt 均存在且 ready，故原意可确定为 `000009`，无需另造角色或重导入未变化的包。
- `CharacterTestPlayableCharacterCatalog` 改为优先消费导入包持久化的 `RuntimeCharacterId`；旧散列只用于兼容诊断。通过现有 Level Editor 统一保存事务，仅将 `player.local.01` 的角色 ID 从 `534146882` 改为 `668367837`，并同步场景缓存 hash/时间。原先其他会话的 Dust 改动保留；`TacticalCardDemoScene` 未保存。
- 构建前从关卡启用角色和数字身份目录解析完整选择：`000002`（651590215）、`000003`（634812596）、`000009`（668367837），再加入 `000001` 默认运行依赖。源包缺失、导入报告不可生成角色、导入包无法加载、数字 ID 不一致、角色目录不可解析或场景缓存与 Authoring 源不同步，均在资源/Player 构建写入前阻断。C 域回执记录关卡源、数字目录、场景资产、逐角色映射及四包逐文件身份；暂存后拒绝额外未选择的包目录。
- PowerShell 入口改为等待 Unity 进程并读取真实退出码。默认报告目录改为系统临时目录中的独立运行目录，避免 Unity BatchMode 退出清理项目 `Temp/` 后丢失 summary；`-OutputRoot` 仍可显式指定。首个真实 BatchMode 尝试暴露了未设置 `$LASTEXITCODE` 与项目 `Temp/` 报告清理问题；修复后真实脚本通过。

### 10.2 局部测试与故障注入

| 验证层级 | 实际结果 |
| --- | --- |
| Unity 脚本验证 | 每次 C# 批次均经 Editor refresh、编译、domain reload、ready 与 Console 检查；最终源码验证 Console error 0。 |
| 角色身份与包集合 EditMode | `PlayableCatalog_UsesPersistedNumericIdentityForPackage000009`、`PackageTestCommand_SelectsSceneCharactersAndRejectsStaleOrMissingPackages`、`PackageTestCommand_StagesEverySceneSelectedCharacterPackage` 各 1/1；旧 `534146882` 能明确提示 `000009` 当前 ID，缺 `000002` 能在构建前指出包。 |
| G/C/R 漂移 | `ProductionBuildInput_DetectsReferencedUnitySourceDriftWithoutPlanChange` 1/1：Build Plan fingerprint 未变，Unity source fingerprint 随隔离源贴图变化。真实已生成的 `000002` C 镜像在 BuildPlayer 前改动时被拒绝；R `.bytes` 镜像在模拟 BuildPlayer 后检查点改动时被拒绝；两项均按原字节恢复、复用再验证通过。短暂移出 `000003` 暂存目录时，Warm Run 在 Player 构建前准确报缺包，Unity 资产移动后恢复。 |
| 场景与进程负样本 | 同时将相关 CharacterTest 场景和无关新场景标脏，打包在资源/Player 构建前失败，无 Player 输出；两张场景磁盘 SHA-256 不变，无关场景脏状态未被保存。临时把内存中的 Player 场景缓存 ID 改回旧值、保持源 hash 不变，前置检查仍报 `player.local.01` 行不一致；恢复后四包预检通过，场景磁盘 SHA-256 不变。A 的目录记录/汇总、进程非零/超时/强杀和重复关键字段测试保持通过；真实逐视图动画诊断允许重复的非合同键。 |
| D 清理与重启 | A→B marker 已提交、台账尚未更新时的重启恢复，B 清理失败后 C 跨代重试、重新引用与内容替换保护均通过。`BundleResidueLedger_UnityCleanupRemovesOwnedBundleAndMeta` 1/1：仅隔离在 `Assets/__BundleResidueCleanupFixture_*` 的已证明归属 Bundle 与 `.meta` 经 Unity `AssetDatabase` 一并移除；夹具已清理。真实未知 Bundle 未删除。 |

### 10.3 实际完整验收

| 入口 | 报告与结果 |
| --- | --- |
| 完整 Windows64 Development Player 菜单 | 同一尝试的 `CharacterTestPackageTestSummary.txt` 为 `Passed`，资源/Player 构建均 `Succeeded`，四检查点一致；Player exit 0、run `finished`、目录 10/10 Binary、animation `ready`、活动角色视图 3。该次项目 `Temp/` 报告随后被 BatchMode 清理，结论以当时读取结果和后续保留的 BatchMode 报告共同支撑。 |
| 单独 Build And Run Player Warm Run | G/C/R 已发布资源复用通过，四检查点一致；Editor run report 为 `acceptanceStatus: Passed`、process exit 0、Binary gate true，animation `ready`、活动视图 3。复验报告已复制到 `C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkWarmRun-20260929/`。 |
| 真实 `build_character_test_player.ps1` | 使用 Unity `6000.5.0f1`、`-buildTarget win64`，脚本实际退出码 0。`C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkPackageBatch-20260929/CharacterTestPackageTestSummary.txt` 为 `Passed`，四检查点、Player 退出码 0、10/10 Binary 与 animation `ready` 全部保留。此 run 显式传了项目外 `-OutputRoot`；新的默认项目外路径通过 PowerShell 语法与路径检查，未重复运行整个 BatchMode。 |

当前 `Library/MxFramework/ResourceBundles/residue-report.txt` 为 `trusted`，报告 1 个 `unknown` 的 `global.character.iron_vanguard.assetbundle`（251,915 B），`eligible/cleaned/failed` 均为 0；该文件仍在。v0.2 限定的 G/C/R 检查点只证明检查时身份一致，不排除两次检查之间发生写入后又恢复原字节。原有未提交工作保留，本次未提交或推送 Git。

## 11. 自动打包概览报告（v0.3）

### 11.1 展示与口径

完整 Windows Package Test 和单独 Build And Run Player Warm Run 结束时，各在对应报告目录生成可直接用浏览器打开的 `CharacterTestPackageOverview.html`，无需手工整理技术日志。首页显示最终状态、Player 构建结果和总量；整包图以 Player 目录全部文件长度为分母，资源图以 `StreamingAssets` 文件长度为分母。分类、文件数、原始字节数与 MiB 同时展示，分类字节数必须与相应分母严格相等。

当前 manifest 中的 Bundle 才列为正式资源 Bundle；清单外 Bundle 独立展示，并与现有残留报告的路径和大小对照归属。外部 Bundle、场景选择的角色暂存包、运行目录镜像及其他 `StreamingAssets` 各占一类。正式 Bundle 依现有资源规则 ID 显示角色、界面、特效、场景等类别；不把 Bundle 文件大小伪称为内部单个贴图、模型或音频的大小。报告还列最大资源文件和可展开的最大 Player 文件、输入回执及原始日志。残留证据复制到同一输出目录的 `CharacterTestBundleResidueReport.txt`，避免清理验证副本后网页链接失效；所有动态文字按 HTML 编码，报告不依赖网络资源。

没有本次成功构建的 Player 时，报告明确显示体积不可用，不读取旧 Player 目录。完整命令的 summary 新增 `overviewReportStatus`、`overviewReport`，脚本要求成功结果同时具备报告文件并打印路径；网页生成失败使入口失败，同时保留构建与 Player 运行的原始结果。未知归属 Bundle 只报告、保留，报告生成不执行清理。

### 11.2 实施与当前证据

| 层次 | 结果 |
| --- | --- |
| 源码实现 | `CharacterTestPackageOverviewReport` 负责 Player 文件清点、manifest 分类、残留证据匹配、总量校验和独立 HTML 生成；完整打包与 Warm Run 复用该实现，脚本核验成功报告路径。 |
| Unity 编译 | 初次实现的 `validate_unity_scripts` job `7bd822d3aaae403581f985dd36925dba`：refresh、compile start/finish、domain reload、Editor ready、Console error 0 均确认。最后补充残留快照后，一键工具在自动重载期间断连；按其明确失败后的分步路径，`Editor.log` 记录 `Tundra build success` 与 `Mono: successfully reloaded assembly`，Editor `isCompiling=False`、`isUpdating=False` 且可执行代码，Console error 0。最后一次未把一键超时误记为通过。 |
| 局部测试 | EditMode `CharacterTestPackageOverviewReportTests` job `9340e84127b549928c73835006ffdb35` **4/4**：全文件分区与残留归属、缺失/重复/越界 manifest、失败和 BuildOnly 的旧数据隔离、成功 BuildOnly 的体积、HTML 转义及残留证据快照。 |
| 完整 Editor 打包 | `Temp/CharacterTestPackageTest/CharacterTestPackageTestSummary.txt` 为 `Passed`，资源与 Player 均 `Succeeded`，运行 `finished`、进程 exit 0、Runtime Catalog 10/10 Binary；HTML 为 405.08 MiB，物理目录 771 文件、424,754,318 B。网页整包 8 类合计 424,754,318 B，资源 6 类合计 63,530,825 B；清单外 Bundle 仍单列。 |
| 单独 Warm Run | `Temp/CharacterTestPerformancePlayerBuildRunReport.txt` 的 `acceptanceStatus: Passed`，对应 `Temp/CharacterTestPerformancePlayerReports/CharacterTestPackageOverview.html` 已生成。 |
| 真实 BatchMode 脚本 | 原项目被图形 Editor 占用时，第二 Unity 进程在构建前退出码 1；首个独立副本路径过长（报错路径 264 字符），资源发布前失败且网页正确显示本次无 Player 体积。将同一副本移至短路径后，完整脚本通过；补上残留证据快照后再以相同入口完整重跑，脚本退出码 **0** 并打印 summary 与 HTML 路径。最终输出为 `C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkPackageOverviewFinal-20260929/`。 |

最终 BatchMode Player 目录为 **771 文件、424,722,542 B**；网页整包分类合计恰为 424,722,542 B，`StreamingAssets` 分类合计 63,530,825 B，7 个证据链接均可解析到实际文件，包含报告目录内的残留台账快照。Player 进程 exit 0、目录加载 10/10 Binary、动画 `ready`、活动角色视图 3，summary 的 `overviewReportStatus: Succeeded` 与脚本退出码 0 一致。上一次短路径成功构建曾多出一个 706,903 B 的 `WGameFramework_BurstDebugInformation_DoNotShip/.../lib_burst_generated.txt`，使 Unity `BuildReport.totalSize` 与目录实测不同；v0.3 始终按既定口径统计实际目录。真实归属未知 Bundle 仍是 251,915 B，源码与 BatchMode Player 副本均保留。

本节只陈述 v0.3 报告能力的证据，不改变 §10 对 v0.2 打包系统的完成结论。体积来自文件长度（MiB = 1,048,576 B），不是压缩下载包或磁盘分配空间。本轮继续保留其他会话未提交工作，未提交或推送 Git。验证副本的递归删除被自动审批审查以 `blocked by policy` 拒绝，`E:/WGF_BatchVerify_20260929/`（约 10.26 GiB）仍在；正式 Player 和报告位于独立的系统临时输出目录，不依赖该副本。

## 12. Player 普攻、跳跃与移动姿态修复（v0.4）

### 12.1 实际缺陷与根因

用户在 v0.3 Player 中发现普攻、跳跃无效，以及平地移动时角色短暂下沉。保留的 `Player.log` 明确记录 `CHARACTER_TEST_ACTION_EXTERNAL_JSON_MISSING`，三个角色均 `Missing entity ActionSet`：正式生成的 CharacterActions 没有镜像进 Player，默认发现路径又使用项目相对 `Assets/...`，打包后无法解析。另有 `char.000003.animation.idle1` 命中仅 Editor 可用的 Provider；其正式 Bundle 条目实际存在，但 `default` variant 与动画 schema 请求的空 variant 未衔接。

旧动画报告仅统计 renderer/controller/backend 是否存在，即使 `lastAnimationResult: not-success code=LoadFailed` 仍能写 `status: ready`；旧运行验收没有驱动攻击或跳跃。这是旧验收范围的实际漏洞，不能由旧的 10/10 Binary 或进程 exit 0 推断玩法正常。

真实 `000009` prefab/Avatar/动画采样还复现：locomotion 起停过渡在 `SyncAnimation` 同帧被无活动 CharacterAction 的恢复分支清除。起步采样中髋部从约 0.955 m 短暂降到 0.158 m；修正过渡持有后，同一 35 帧采样最低约 0.882 m，脚部约 0.071 m。扩展到四方向完整起步、循环、停止后，脚部最低约 0.064–0.071 m，测试同时检查过渡到期恢复，未通过改根位移或替换动画掩盖问题。

### 12.2 修复范围与当前证据

- 正式动作 JSON 通过现有 Authoring 产物镜像至 `StreamingAssets/MxFramework/CharacterActions`，Player 使用实际 `Application.streamingAssetsPath`；源与镜像的完整相对文件集、长度和 SHA-256 纳入 R 域四检查点。构建前核对每个选择角色的默认装备 ActionSet。
- 新检查发现 `000003` 旧生成动作集装备状态为空；Authoring 源明确为 `000003.unarmed`。使用既有 `character import-unity --check-files --check-hashes` 重新生成后，绑定恢复为 `equip_state.000003.unarmed`；未更换角色或编造动作。
- 动画默认 variant 兼容条目保留正式目录原 Provider、地址和哈希；Player 不再注册 Editor-only 动画 Provider。动画报告要求真实 clip 加载成功。
- 普通启动键鼠复测又发现公共跳跃/落地动作的 clip 字段为空，导致 `anim.basic_jump` 播放 idle 回退。依据既有 `JumpAnimationClipResourceKey` / landing 配置及项目正式动画资源，在 `CharacterActionSource/Libraries/Common` 补齐 `standing_jump` 和 `standing_jump_running_landing` 引用，并经 `character import-unity` 重新生成。新探针要求普攻、跳跃实际 clip 与对应 Authoring 引用一致，不能以 fallback 返回 Success 代替。
- 新增仅报告模式启用的玩法探针，通过现有 `FakeInputProvider/InputCommand -> RuntimeCommand -> CharacterAction Gateway` 驱动移动、停止、普攻、跳跃、落地；采样动作执行、动画播放和高度，并在性能采样前完成及恢复输入 Provider。逐帧证据为 `CharacterTestPlayerGameplayProbe.csv`。最终 evaluator 拒绝缺少玩法证据、失败动作、无实际位移/跳跃或明显姿态塌陷的报告。
- Unity 编译 job `501be3212278469184e115dfe44677b6` 的 refresh、compile、domain reload、ready、Console error 0 通过。focused EditMode job `442779843d02483da9663fbf1e58b18e` **12/12** 通过；包含四方向真实角色动画、动作目录镜像、逐角色装备动作集、已发布动画 variant、动画加载失败拒绝、旧 idle-only 报告拒绝及相邻恢复行为。
- 最终数据修复后的 Unity 编译 job `69d4e516c390449485bbf08bb8c841ba` 同样完整通过；focused job `833a0f79b1ef4a04a8f0ebe6eb001aa3` **10/10** 通过。真实 prefab 四方向动画测试覆盖起步、循环、停止、持有到期，未发现原来的姿态塌陷。

### 12.3 最终 Player 验证（2026-09-29）

| 入口 | 实际证据 |
| --- | --- |
| 原项目完整 Editor 构建 | `C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkGameplayEditorFinal-20260929/`：summary `Passed`，资源/Player `Succeeded`、Player exit 0、目录 10/10 Binary、动画 `ready`；`gameplayProbe` 全部通过，含 `attackAnimation` 与 `jumpAnimation`。 |
| 真实 BatchMode 脚本 | 在保留的短路径独立验证副本中同步当前源码及正式产物，执行 `build_character_test_player.ps1`；进程/脚本退出码 **0**。最终可交付目录：`C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkGameplayFinal-20260929/`，summary `Passed`、overview `Succeeded`。 |
| 单独 Warm Run | `Temp/CharacterTestPerformancePlayerBuildRunReport.txt` 为 `acceptanceStatus: Passed`、Player exit 0，玩法探针全部通过。原始报告已另存 `C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkGameplayWarmRun-20260929/`。 |
| 最终 Player 实际键鼠 | 普通启动模式（不启用玩法探针，仅跳过主菜单），向正确窗口发送 W、鼠标左键和 Space。trace 显示鼠标攻击 `#301` accepted 且对应攻击 clip Success；Space 跳跃 `#300` accepted、`impulseY=12`，实际播放 `art.character.skeleton.animation.standing_jump`，无 idle fallback。证据保留于 `C:/Users/WuGC/AppData/Local/Temp/WGameFrameworkPlayerGameplayBug-20260929/keyboard-final-*.log/jsonl` 与 `final-jump.png`。 |

最终 BatchMode 玩法 CSV 有 534 条逐帧采样。实际移动 1.519 m、跳跃最高升高 2.058 m、离地与落地均观察到；移动/停止采样区间根节点 Y 始终为 0，脚部最低 0.06769 m，髋部相对根节点基线 0.95292 m、最低 0.74057 m。31 个正式动作 JSON 已进入 Player，R 回执包含准备前源与后三检查点源/镜像的共 7 组动作文件身份记录。未知归属 Bundle 251,915 B 仍保留。本轮未提交或推送 Git，也未保存无关场景。

## 13. Windows x64 IL2CPP 构建配置（v0.5，已完成专项验收）

用户确认默认一键打包为发布版、离线调试符号独立存放；2026-09-29 15:08 进一步选择常规 **C++ Release** 优化，取消 Master。此次状态独立于 v0.2 打包流程、v0.3 概览和 v0.4 玩法修复，以下为最终证据。

### 13.1 最终配置与实现

| 配置项 | Release（默认） | Development / 单独 Warm Run |
| --- | --- | --- |
| 平台／架构／后端 | StandaloneWindows64 / x86_64 / IL2CPP | 相同 |
| C++ 编译／代码生成 | Release / OptimizeSpeed | 相同 |
| Managed stripping／Engine stripping | Low / 开启 | 相同 |
| 压缩 | LZ4HC | LZ4 |
| Development Build | 关闭 | 开启 |
| IL2CPP 堆栈信息 | MethodOnly | MethodFileLineNumber |
| Script debugging / Auto-connect profiler / Deep profiling / Wait debugger | 关闭 | 关闭 |
| 图形 API／增量 GC | Direct3D11 / 开启 | 相同 |
| API compatibility | 保留 NET_Unity_4_8 | 相同 |
| 离线符号与 DoNotShip 产物 | 移到 Player 外的 Symbols，记录大小与 SHA-256 | 相同 |

- `CharacterTestWindowsBuildConfiguration` 统一预设、工具链检查、实际 backend/架构验证和符号归档；项目默认值通过 Unity API 保存为 IL2CPP / C++ Release / 非 Development，原有 HDR 设置保留。构建入口不再临时强制 Mono。
- 菜单 `MxFramework/Build/Windows64` 提供 Apply IL2CPP Release Defaults、Build Release And Verify、Build Development And Verify；脚本 `-Configuration Release|Development` 默认 Release。运行报告由编译宏和 `Debug.isDebugBuild` 上报实际后端/开发标志，evaluator 与脚本拒绝矛盾结果。
- Windows 模块使用与本机 Unity 6000.5.0f1 / 88b47c5e7076 一致的官方 IL2CPP 安装器；安装前核对官方 MD5 与 Unity 数字签名，用户批准 UAC。前置检查遍历 VS 安装实例并核对真实 x64 cl/link 与 SDK，避免新版本安装未完成时仅取 latest 而误拒已有可用工具链。最终构建实际使用 MSVC 14.51.36231。
- FMOD 按目标 x64 / ARM64 选择二进制，保留原插件的 AllVariants 清理/恢复枚举；发布包使用 fmodstudio.dll，开发包使用 fmodstudioL.dll。
- `CharacterTestJsonPreservation` 从 Player 源码中的具体 JSON 反序列化入口、数据契约引用图与 JSON 转换器生成明确 `link.xml`：当前 427 类型、25 个程序集，无整程序集保留。新增入口后执行 Regenerate Runtime JSON Preservation；测试核对清单与当前生成结果一致。动态类型入口仍需专门声明，该规则不是任意反射/AOT 路径的完备证明。
- 默认完整输出为系统临时目录 `WGF-Pkg-<标识>/`，Warm Run 为 `WGF-Warm-<标识>/`，均位于项目 Temp 外。BuildPlayer 前拒绝达到 260 字符的 StreamingAssets 输出文件路径。Warm Run 网页配置快照会换算符号相对路径，避免报告和符号目录层级不同造成错误引用。
- HTML 显示实际 Release/Development、IL2CPP、优化/压缩配置与独立符号体积；`.configuration.json` 为实际构建配置和符号清单。报告不打开浏览器、不删除未知 Bundle。

### 13.2 实际验证与交付

| 层次／入口 | 最终结果与证据 |
| --- | --- |
| Unity 编译 | 最终 job `e39b8c3e916c4c8886bd20e70693cc78`：refresh completed、2 assemblies 编译开始/结束、domain reload、Editor ready、Console error 0 全部确认。 |
| 专项测试 | `f1ef5153165a4568b9d1527ed456b21e` **9/9**（JSON 保留图及清单一致性、路径长度拒绝、配置/符号归档、概览分类与 Warm Run 符号路径）；用户改用常规 C++ Release 后，配置组 `09185cf38c184bc18d745d15707c164d` **4/4**。此前严格结果一致性与概览组合 `c3699a2a0e9240bf9f557ded87e5e9e8` **7/7**。PowerShell 语法错误 0。 |
| 默认 Release 真实 BatchMode 脚本 | **Passed，脚本退出码 0**。交付：`E:/WGFBuilds/IL2CPP-Release-20260929-Ready/`；summary 的资源/Player 构建 Succeeded、overview Succeeded，Player exit 0、IL2CPP、developmentBuild=false，10/10 Binary、动画 ready、全部玩法探针通过。移动 1.519 m、跳跃升高 2.058 m，实际普攻/跳跃 clip、离地和落地均确认。脚本打印 Summary 和 Overview 路径。 |
| Development 真实 BatchMode 脚本 | **Passed，脚本退出码 0**。`E:/WGFBuilds/IL2CPP-Dev-Verified-20260929/`：IL2CPP / developmentBuild=true；Player exit 0、10/10 Binary、动画及全部玩法探针通过。 |
| 单独 Development Warm Run BatchMode | **Passed，Unity 退出码 0**。`C:/Users/WuGC/AppData/Local/Temp/WGF-Warm-3c972ee7/`：Player exit 0、10/10 Binary、全部玩法探针通过。退出后 HTML、回执、配置和运行报告仍在；配置快照的 `../Symbols/Development-af46b281` 实际可解析。启动日志 `E:/WGFBuilds/IL2CPP-WarmRun-20260929/unity_verified.log`。 |
| 物理文件复核 | 三个最终目录均有 `Il2CppOutputAudit.json`：GameAssembly/metadata 存在，无 MonoBleedingEdge、无 ARM64 DLL，native PE 标头均 x64，PDB/DoNotShip 文件已移出，逐个符号 SHA-256/大小一致，网页分母与 Player 实际文件总和一致。未知 Bundle 251,915 B 仍保留。 |
| 网页检查 | Release 与 Development 网页已通过无界面浏览器实际渲染检查，中文、状态、配置和占比正常；Release 截图在交付目录 `overview-preview.png`。未自动打开用户浏览器。 |

| 最终产物 | Player 实际大小 | 独立离线符号/调试源码 |
| --- | --- | --- |
| Release | **302,095,806 B / 288.10 MiB** | 2,586,374,700 B / 1,223 文件（2466.56 MiB） |
| Development | 510,093,910 B / 486.46 MiB | 2,876,691,807 B / 1,418 文件 |
| Warm Run | 510,138,048 B / 486.51 MiB | 2,876,719,082 B / 1,418 文件 |

Release 整包分类：Player 数据 **91.30 MiB / 31.69%**，Player 运行文件 **135.62 MiB / 47.07%**，StreamingAssets 合计 **64,151,686 B / 61.18 MiB**。最大文件为 GameAssembly.dll（95,845,376 B）、压缩后的 data.unity3d（64,177,682 B）和 UnityPlayer.dll（37,316,008 B）；不把这些容器误称为可精确分摊的贴图/模型。

原保留 Mono Development 目录 `WGameFrameworkGameplayFinal-20260929/Player` 实测 426,081,061 B / 406.34 MiB；当前发布预设减少 **118.24 MiB / 29.10%**。这是后端、非开发引擎、压缩及符号分离等配置共同变化后的结果，不是隔离变量的 Mono/IL2CPP 性能对比。交付时分发 Player 文件夹，保留 Symbols 供离线诊断。

### 13.3 中间失败、边界与剩余事项

- 首轮 IL2CPP Development 成功构建，但 JSON DTO 构造函数被裁剪，资源/音频目录无法加载，运行验收失败；证据 `E:/WGFBuilds/IL2CPP-Development-Batch-20260929/Player-first-attempt.log`。局部保留规则曾让现有门禁 Passed，但日志另有行为卡转换器错误与 Trigger CacheSourceHashMismatch；该中间目录不作为最终交付。最终数据契约图保留修复后，三个最终 Player 日志均不再出现这些问题。
- Warm Run 旧项目 Temp 输出曾在退出时被清空；迁移持久目录时的首次创建遗漏、过长路径尝试均已修复。268 字符的 Bundle 路径曾表现为文件存在但 IL2CPP 无法加载；现采用短默认目录与前置拒绝，未删减资源绕过问题。失败目录和日志保留。
- Master 曾在主 Editor 中同步链接超过 24 分钟，导致编辑器无法交互。用户指出后停止该构建，未终止 Unity 或保存场景，核实 building/compiling/updating=false、场景保持干净。随后发布构建移到独立副本 BatchMode；用户最终选择常规 C++ Release，Master 尝试均不计为完成证据。后续长等待按用户要求交由 GPT-6 Luna 监控，完成/异常后主代理接手。
- Editor 菜单仍是 Unity 的同步构建入口，会占用主编辑器；长时间构建应使用关闭对应 Editor 的项目或独立副本运行脚本。本次最终 Release/Development/Warm Run 均有独立 BatchMode 证据。
- Development 保留约 130.8 MiB 的 il2cpp.usym，供运行时源码行号堆栈使用；它计入 Player 数据，不应与可移出的离线 PDB 混同。Release 使用 MethodOnly。官方说明：[IL2CPP managed stack traces](https://docs.unity3d.com/cn/6000.0/Manual/il2cpp-managed-stack-traces.html)。
- Release 的 ProfilerRecorder 项标记 available=false，记录中的零值不能解释为性能耗时为零；本轮通过不等于完整性能基准验证。最终日志仍有 scene-characters.missing-ai-controlled 诊断；Development 退出还有原生分配提示，Release 本轮日志未命中该提示。本轮未宣称解决所有 AI 场景或原生内存问题。
- v0.5 无剩余打包验收阻塞。Unity Performance Testing 的 Cleanup 删除了既有 PerformanceTestRunInfo/PerformanceTestRunSettings 及 `.meta`；结束时已按测试前干净版本精确恢复并经 Unity 重新导入。独立验证副本、失败构建和符号归档留在本机；未知归属 Bundle 保留。其他会话工作未覆盖，本会话未提交或推送 Git、未保存无关场景。

## 14. 概览中的代码与内置资源细查（v0.6）

本次只扩展打包报告和分析工具，未改变 Player、场景或资源引用。已有 Release 的明细从其保存的 LastBuild.buildreport 和对应 DLL/PDB 回填；网页仍展示 v0.5 的实际构建/运行结果，不宣称本轮重新构建或重新运行 Player。

### 14.1 展示与自动采集

- Player 运行文件与 Player 数据分别列出所有实际文件、用途、MiB 和分类内占比；原先粗分类保持兼容，并增加页内导航。
- GameAssembly.dll 使用 Windows DIA 读取经过 GUID/age 校验的 PDB section contributions，映射到 PE 实际存储区间。虚拟/BSS 不算文件字节，重叠归属只计一次并列为共享；文件头、对齐与未归因区间单列，分类总和必须等于 DLL 实际长度。共享泛型不能全部分摊给某个业务程序集，不以源码行数或中间 DLL 大小冒充原生体积。
- 内置资源来自本次 BuildReport.packedAssets，按资源类型与角色包/字体/光照等来源汇总；最大资源展示实际资源容器和能定位的场景→Prefab→资源引用链。该表使用序列化资源记录、未按最终 LZ4HC 压缩的字节口径，不与整包占比相加，不表示可直接节省的压缩后字节。
- 正式 Bundle 目录声明从这份 Player 的 global_runtime_catalog.json 读取。标记仅证明同一路径同时进入内置资源记录和 Bundle 目录声明，不能代替对 Bundle 依赖或场景内部的完整分析；不自动删除资源。
- FinishBuild 在已有配置/符号回执之后自动捕获 `.details.json` 与 `.native.json`。最终网页使用 `CharacterTestPackageDetails.json` 快照，核对 DLL/data.unity3d 的大小与 SHA-256、资源总量及原生分类总量；拒绝旧包、改包、越界路径和矛盾汇总。失败构建不沿用旧明细。缺少原生符号工具时明确显示不可用，不虚构占比。
- 原生分析工具为 `Tools/Harness/analyze_package_native_size.ps1` 和 `PackageNativeSizeAnalysis.cs`；使用本机 Visual Studio DIA 与 .NET Framework SDK 的 TlbImp，Interop 仅在 Library 本地生成，不进入 Player。分析进程隐藏运行、超时 60 秒时报告不可用。

### 14.2 这份 Release 的事实

交付网页仍为 `E:/WGFBuilds/IL2CPP-Release-20260929-Ready/CharacterTestPackageOverview.html`。Player 文件总量仍为 302,095,806 B，DLL 与 data.unity3d SHA-256 未变。

- GameAssembly.dll 91.41 MiB：共享泛型 28.86 MiB、IL2CPP 注册与生成表 16.22 MiB、MxFramework.Game.CharacterTest 编译单元贡献 11.22 MiB；文件头/对齐/未归因 6.53 MiB，其余模块可展开查看。174 个归属分组的实际文件区间总和严格核对为 95,845,376 B。
- 10,503 条 BuildReport 资源记录合计 149,310,860 B，聚合到 4,471 个源路径。字体目录合计约 19.03 MiB。角色包的记录合计：000003 33.31 MiB、000009 12.93 MiB、000004 11.79 MiB、000008 11.42 MiB、000002 2.82 MiB、000001 1.49 MiB。均为压缩前统计。
- 当前工程引用链确认：CharacterTestScene → 000004/000008/000009_character_preview.prefab → 对应模型；这些模型也有 global.character.package.assets 声明。000003 的三张 5.33 MiB 贴图出现在 resources.assets / resources.assets.resS。共 30 个内置资源路径有正式 Bundle 目录声明。回填报告明确注明引用链来自事后读取的当前工程，大小来自保存的本次 BuildReport。

### 14.3 验证边界

Unity 编译 job `23b7ff79a8254558b2d660af1b653768` 的 refresh、compile、domain reload、ready、Console error 0 通过；报告测试 job `66d7b9e0d1b84f90bf34cd8299a6f6ca` **7/7**，新增匹配身份、同长度不同哈希、矛盾总量、越界身份、失败旧证据清除和 HTML 转义验证。实际匹配的 Release DLL/PDB 分析成功；将 Development PDB 配给 Release DLL 时退出码 1、没有生成结果。当前输出独立复核原生/资源总量、SHA-256 和 Player 大小不变，网页已实际渲染检查。

源码采集路径已接入未来打包入口，本轮实际执行的是保存 BuildReport 的明细采集与现有网页重生成，没有为报告改动再跑一次完整 Player 构建。最终补充失败处理后，编译 job `b911de3e2cad478a9a620c830c843706` 全阶段通过、Console error 0；测试 job `06a4f0e94a394e59adb46c2fbe9efd6f` **8/8**，含明细采集失败不改写原始构建/运行证据；在 Editor 中实际调用未命中缓存的原生分析入口也返回 Verified，634,471 个有效文件区间、1,449 个编译单元，总量与独立脚本一致。最终网页保留页内导航与可展开的剩余模块、资源类型和引用链。

### 14.4 首页评审意见（v0.6.1）

新增 `CharacterTestPackageReview`，从本次已核验的文件清单、配置回执、资源明细和既有验收结果生成首页意见，分为：需要修复、待确认、优化候选、正常/保留、证据不足。判断依据与建议同时展示，评审不改写验收结果，也不把正常构成等同于体积最优。

- **需要修复**：已有验收失败或状态矛盾；实际文件与 IL2CPP 回执冲突；Release 带开发/调试标志；离线 PDB/MDB/DoNotShip 产物进入 Player。缺失回执/字段按证据不足处理，不能默认成 false。
- **正常/保留**：已记录的 Passed 验收、用途明确且未见后端冲突的运行库与元数据、离线符号已分离。这些结论不表示所有玩法、性能、文件必要性已经验证。
- **优先复核**：内置资源与正式 Bundle 目录的双重入口；预览 Prefab 是否确为编辑器专用。名称 preview/Demo 本身不能证明无用，也未把压缩前记录当作可删除字节。
- **优化候选**：先按真实语言支持评估字体，再结合使用效果/视觉质量评估特效素材规格；代码生成或裁剪设置须做等功能和性能对照，不直接删 DLL。当前字体代码确实按 SC/TW/JP/KR 切换并通过 Resources 加载，默认 zh-CN 不是其余字体无用的证据。
- **待确认/证据不足**：清单外 Bundle 先查归属，未知继续保留；没有包体预算或等条件对照，不凭总 MiB、模块名字或共享泛型比例判大小异常。此页“需修复 0”仅表示当前规则未发现已确认问题，不是全面质量背书。

本轮只扩展报告评审，Player 与资源引用未变，未重新构建或重新运行。参考：[Unity Resources](https://docs.unity3d.com/ru/current/ScriptReference/Resources.html)、[IL2CPP 代码生成取舍](https://docs.unity3d.com/cn/6000.0/ScriptReference/PlayerSettings.SetIl2CppCodeGeneration.html)。测试首次发现评审复用 Passed 样式会干扰 BuildOnly 识别，已改用独立 review-* 标签；最终编译 job `6d0b037b429f44fe94e39549e5f46b57` 全阶段通过、Console error 0；报告测试 job `ffb505e2a7134e868c5e64b57c7c0e2f` **10/10**。当前网页已重生成并实际渲染：需修复 0、待确认 2、优化候选 3，另列正常/保留和证据不足项；Player 实际总量仍为 302,095,806 B。

### 14.5 旧打包产物清理（2026-09-29）

用户要求“清理之前打包的，留下最后的”。已删除本次工作生成的旧 Release/Development、Mono、Warm Run 输出及报告检查缓存，共 35 项、26,526,121,746 B（按文件长度合计约 24.70 GiB），无删除失败。`E:/WGFBuilds/` 现只剩 `IL2CPP-Release-20260929-Ready/`。

最终发布包的 Player、最新概览、运行验收记录、细查快照及 Symbols 全部保留。清理前后核对 7 个关键文件的 SHA-256 一致，并重新通过全部符号大小/哈希、x64/IL2CPP、网页总量与物理文件总量核验。未知归属 Bundle 的最后发布副本仍为 251,915 B；项目中的资源未清理。

源码、Unity Library、构建验证副本 `E:/WGF_BatchVerify_20260929` 及不属于打包输出的历史备份/玩法诊断未动。旧路径在此前章节中仅保留历史记录，不再承诺对应原始文件仍存在。清理清单、保留文件校验及清理后审计位于最终目录 `Validation/Cleanup-20260929.json`、`CleanupKeepEvidence.json` 和 `Audit-after-cleanup.txt`；原本在 WGFBuilds 根目录的审计脚本及编译回执已归并至该 Validation 目录。

[workflow]: RESOURCE_SYSTEM_WORKFLOW.md
[resources-interface]: Interfaces/Resources.md
[usage]: USAGE.md
[quality]: QUALITY_GATE.md
[package-command]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPackageTestCommand.cs
[player-build]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPerformancePlayerBuildReportMenu.cs
[player-run]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPerformancePlayerBuildRun.cs
[build-script]: ../Tools/Harness/build_character_test_player.ps1
[mapping-builder]: ../Packages/com.vincent.wgameframework/Editor/Resources/GlobalResourceMappingBuilder.cs
[compiler]: ../Packages/com.vincent.wgameframework/Editor/ResourceMapping/Core/GlobalResourceBuildPlanCompiler.cs
[resource-builder]: ../Packages/com.vincent.wgameframework/Editor/Resources/GlobalPlayerResourceBuildProfileBuilder.cs
[artifact-loader]: ../Packages/com.vincent.wgameframework/Resources.Unity/GeneratedResourceBuildArtifactLoaders.cs
[builder-tests]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/GlobalPlayerResourceBuildProfileBuilderTests.cs
[loader-tests]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Resources/GeneratedResourceBuildArtifactLoaderTests.cs
[catalog-mirror]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestRuntimeCatalogPlayerArtifacts.cs
[catalog-parser]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/CharacterTestRuntimeCatalogArtifacts.cs
[package-tests]: ../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestProfilerGcRemediationTests.cs
