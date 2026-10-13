# 角色包导出 JSON 精简实施方案

> Status: Design
> Implementation: Batch A + Batch B implemented; formal 000008 migration and Unity acceptance completed; see §12.10
> Updated: 2026-09-19
> Baseline: `3cd823848` 的源码及当日工作区；资产样本为 `000008`。
> Scope: 两份完整编译快照退出默认导入产物、轻量摘要、定向迁移清理、三组配置镜像收敛、CharacterTest Player 暂存过滤。
> 第 1～11 节保留实施合同，第 12 节记录实际交付；最新收尾状态以 §12.10 为准。真实 Player 构建/完整战斗验收未在本次执行，不以 Editor 和暂存验证代替。

## 1. 交付目标与执行顺序

第一批 A：正式角色包不再默认保存两份完整编译快照，改为一份有界摘要；完整诊断结果由显式诊断导出获得。同步实现旧包清理（含**未被当前导入声明的孤立资源文件**）、Player 暂存过滤与暂存体积门槛。

第二批 B：将配置补丁、几何绑定、资源映射收敛到 `config/`，取消 `generated/` 下的三份重复镜像。B 在 A 验收后实施，可独立提交和回退。

本次目标是减少导出目录、版本管理和重复写入的数据量。没有启动时间、内存或帧耗时对照数据，不以运行性能提升作为已知收益。

**口径与量级（2026-09-19 评审补充）**：JSON 仅占角色包 5.27%，本方案 A+B 的收益约 1.04 MB（占 000008 包体 2.9%）。同批包内存在**占单包 47.3% 的孤立文件**（见 §2.1.1），其判定与清理完全复用本文已建立的清理框架，故纳入 A；相对收益最大的口径是 **Player 暂存**（A 后 000001 减少 28.6%，见 §9.4）。本方案**不涉及**占包体 89% 的三个模型/预览文件（`character_00.fbx` 16.9 MB、`character_00.preview.glb` 11.6 MB、`000008_lod0.fbx` 3.4 MB），不得据此宣称整包体积优化已完成。

本次不裁剪动画/资源 DTO 字段，不改变模型、材质、动画曲线、运行时数值 ID 或 Gameplay 合同，不修改烘焙二进制格式，不删除整个 `package_cache/`，不重构整个导入事务框架。第三阶段的“运行数据与创作来源字段分离”另行立项。

### 1.1 开工阅读顺序

1. 根与修改路径上的 `AGENTS.md`。
2. [SDK 与导入边界](CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md)、[Character Application 接口](Interfaces/CharacterApplication.md)。
3. [数据归属](Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md)、[动画烘焙存储合同](Interfaces/Animation.md#bake-artifact-contract)。
4. [API 规范](API_STANDARDS.md)、[质量门禁](QUALITY_GATE.md)中与接口、角色导入、验证相关的章节。
5. 本文列出的实际源码。架构白皮书只能作为线索，不能据其“可剥离”列直接删文件。

仓库根：`E:\Unity\WxFramework\WGameFramework`。下文工程路径均相对此根；行号是调查时的定位提示，实施时以符号为准。

## 2. 已核实基线

### 2.1 000008 的体积

统计递归包含的 `.json` 原始字节，不含 `.meta`，不使用磁盘占用大小、压缩大小或 Git pack 大小。

| 类别 | 数量 | 字节数 |
| --- | ---: | ---: |
| `config/` JSON | 15 | 770,283 |
| `generated/` JSON | 6 | 89,137 |
| `package_cache/` JSON | 12 | 1,022,680 |
| JSON 合计 | 33 | 1,882,100 |
| 两份完整编译快照 | 2 | 956,032 |
| 三份重复配置镜像 | 3 | 87,207 |

原包全部文件合计 35,754,538 字节（含 `.meta` 65,990 字节；上表 JSON 统计不含 `.meta`），JSON 占约 5.3%。本方案的约 50% 节省指 JSON，不是整个角色包。

### 2.1.1 未声明资源遗留（2026-09-19 补充实测）

以 `import_report.json` 中 `kind = resourceFile` 的写入集合为"本次导入声明"的权威清单，与磁盘实际文件比对：

| 包 | 磁盘 `resources/`+`previews/` 文件 | 本次导入声明 | 孤立文件 | 字节 |
| --- | ---: | ---: | :--- | ---: |
| `000008` | 5 | 4 | `resources/models/character_00.fbx` | 16,900,732 |
| `000004` | 5 | 4 | `resources/models/character_00.fbx` | 16,900,732 |

`000008` 声明的 4 项为 `previews/thumbnail.png`、`resources/models/000008_lod0.fbx`、`previews/character_00.preview.glb`、`resources/textures/body_albedo.png`。`character_00.fbx` **不在任何一次导入的写入集合中**，也不在当前源 `resource_catalog.json` 的 4 条 entry 中（该文件仅在预览 GLB 条目的 `provenance.sourceFile` 里作为字符串出现）。源包 `Authoring/CharacterPackages/character-000008/resources/models/character_00.fbx` 同样存在（2026-06-27，16.9 MB）。

**量级对比**：单个孤立文件 16,900,732 B = `000008` 包体的 **47.3%**；本方案 A+B 全部收益 −1,035,047 B = **2.9%**。两包合计 33.8 MB。

**处置**：纳入 A 的清理白名单（§3.1、§6.1），判定依据为现成机器可读的 `import_report.resourceFile` 集合，**不做目录递归、不做文件名通配**。这不属于"裁剪资源"：被删文件不被任何配置引用，资源数量、DTO 字段与运行时行为均不变。

已确认的重复关系：

- 总编译结果的 `animationCompileResult` 与独立动画编译结果解析后相同。
- 动画编译结果中的 Clip Registry、Resource Plan、Expectation、Validation Report 与各自独立文件解析后相同。
- 三组 `config/` 与 `generated/` 配置镜像解析后相同；资源映射文件仅有文本长度差异。
- 样本中的独立 `animation_set_definition.json` 已是 v2，并增加数值 binding 与 prepared bindings；它不等于快照内的旧阶段对象。禁止从旧快照覆盖正式独立动画文件。

### 2.2 当前调用链与必要纠正

| 已核实事实 | 源码入口与影响 |
| --- | --- |
| 默认导入写两份完整快照 | `CharacterPackageCommands.CreateUnityImportAdditionalWrites`，约 526、533、578、583 行 |
| Readiness 使用本次编译对象 | `EditorServer.Readiness.AddCompileBackedStages` / `AddUnityImportStage`，并非从这两份缓存反序列化 |
| 已找到的完整快照磁盘读取主要在测试 | `CharacterPackageTests.UnityImportBridge.cs`，约 88、108、160 行；实施前须补查其它脚本、动态拼接路径和构建入口 |
| Compiler 计划写入 `generated/`，CLI 又写 `config/` | `CharacterAuthoringCompiler.Compile` 约 700–726 行，与 `CreateUnityImportAdditionalWrites` 约 587–589 行组成两套路径 |
| 导入桥目前逐项写入 | `CharacterUnityImportBridge.Execute`；现有路径不是整包原子交换，不能把成功写入第一项等同整包完成 |
| 写完后还有验证和公共映射同步 | `CharacterPackageCommands.ImportUnity` 中 `PerformWriteAfterVerify`、`ResourceMappingCharacterCoordinator.ApplyFromCompileResult` |
| Runtime 仍读取部分 cache 与 catalog | `CharacterImportedPackageJson.LoadFromDirectory` 必须读取 `package_cache/import_report.json` 和 `config/unity_resource_catalog.json` |
| JSON 烘焙索引属于必要加载输入 | `CharacterWeaponTraceBakeStorage.Load` 先读索引再校验两个 payload；Hurtbox 系统同样从索引解析二进制 |
| Player 暂存会复制所有 JSON | `CharacterTestPackageTestCommand.StageGeneratedPlayerContent` 约 323–348 行，递归复制 `.json` 与 `.bytes`；当前默认角色为 `000001`，不能把其构建验证写成 `000008` 的 Player 验证 |
| 已有 DeleteFile 执行器不是当前导入入口 | `SourceContract/CharacterWritePlanExecutor` 有删除、哈希前置条件和幂等处理；不能假设给它加规则就会影响 `ImportUnity` |

**补充实测（2026-09-19 评审）**：

- 非测试代码中 `package_cache/compile_result.json` 与 `package_cache/animation_compile_result.json` **不存在任何读取点**，全部命中都是写入点（`CharacterPackageCommands.cs:430`、`:578`、`:583`）或同名兄弟产物。A1 取消默认写入不会破坏任何非测试消费者。
- `prefabs/<packageId>_character_preview.prefab` 与 `runtime_resources/.../<packageId>_character_preview.prefab` 大小同为 158,831 B，但 SHA-256 不同（`6d4427ea…` / `fc0f8c9a…`）：二者是两次独立序列化，**禁止按大小或路径假设去重、禁止硬链接**。
- `package_cache/manifest.json`、`package_cache/resource_catalog.json`、`package_cache/character_application.json` 在代码中**读取点为 0**（合计 16,820 B），且 `resource_catalog.json` 的 mtime 为 2026-09-08 而源为 2026-09-12，属陈旧副本。保留它们需要给出消费者证据（见 §3.3）。

## 3. 文件处置合同

### 3.1 第一批 A

| 当前文件，均相对生成角色包根 | 默认导入的新行为 | 诊断保留方式 |
| --- | --- | --- |
| `package_cache/compile_result.json` | 停止写入；成功迁移后删除旧文件及其 `.meta` | 显式诊断导出完整结果 |
| `package_cache/animation_compile_result.json` | 停止写入；成功迁移后删除旧文件及其 `.meta` | 显式诊断导出完整结果 |
| `package_cache/compile_summary.json`（新增） | 保存一个有界、确定性的轻量摘要 | 见第 4 节 |
| `resources/**`、`previews/**` 中的**未声明孤立文件** | 不写入（本就不在写入集合中）；成功迁移后删除旧文件及其 `.meta`。判定依据：不在当前导入 `import_report.resourceFile` 集合内的 `resources/`、`previews/` 文件 | 迁移备份保留原始字节（§6.2） |
| 其余 31 个现有 JSON | A 保持各自生成与读取行为 | 不按“缓存”目录批量删除 |

**孤立文件白名单的边界**：只覆盖 `resources/` 与 `previews/` 两个目录，且只删除"未被本次导入声明"的文件；不得扩展到 `prefabs/`、`materials/`、`preview_materials/`、`runtime_resources/`（这些目录含 Unity 侧生成资产，归属 Unity Editor bridge，见 §3.3）。判定必须用规范化后的完整相对路径，不用通配符。

### 3.2 第二批 B

| 保留的唯一正式路径 | 停止生成并迁移清理的路径 |
| --- | --- |
| `config/character_config_patch.json` | `generated/character_application_config_patch.json` |
| `config/geometry_binding.json` | `generated/character_geometry_binding.json` |
| `config/resource_catalog_mapping.json` | `generated/character_resource_mapping.json` |

不以删除 `CreateGeneratedArtifacts` 中的三个内存产物来去重；它们仍是导入桥按 `SourcePath` 取载荷的输入。B 应修改最终目标路径并删除第二个 writer。

### 3.3 必须保留的边界

- 所有正式 `config/` 文件；本次不从中删除字段。
- `package_cache/import_report.json`（Runtime `LoadFromDirectory` 的必需输入）、Unity 成功回执及其它校验/计划文件。
- **待裁决项**：`package_cache/manifest.json`、`package_cache/resource_catalog.json`、`package_cache/character_application.json`（合计 16,820 B）在代码中读取点为 0，且 `resource_catalog.json` 为陈旧副本（2026-09-08，源为 2026-09-12）。实施前必须二选一：(a) 给出具名消费者证据并写入 §10 产物表，保留；(b) 无消费者则纳入 B 的清理白名单。不得在无证据的情况下继续以"增量比对基准"为由默认保留。

- `generated/weapon_trace_bake_catalog.json` 和两个 weapon/contact `.bytes`。
- Hurtbox JSON 索引和 `.bytes`，以及 Socket Bake v3 小 JSON 索引和唯一 `.bytes` 载荷；Socket 的完整 JSON 已退役，详见 `CHARACTER_SOCKET_BAKE_BINARY_EXPORT_TASK.md`。
- Prefab、材质、模型、贴图与保留资产的 `.meta` GUID。
- 由本次导入正常更新的 CharacterAction、Resource Mapping 等包外正式产物；迁移清理器不得删除这些路径。

不新增 Runtime 回退读取两份旧快照的逻辑。旧包在未重导入时仍按现有独立配置加载；新包不再有快照，运行时也不需要它们。

## 4. 轻量摘要和诊断输出

### 4.1 摘要格式

新增内部 DTO `CharacterPackageCompileSummary` 和内部 builder；建议独立文件，放在 Authoring CLI 层，不把新的文件布局策略塞进 Runtime DTO。

固定格式：`mx.characterPackageCompileSummary.v1`；`schemaVersion = "1.0"`。A 的 `exportLayoutVersion = 1`，B 完成后为 2。布局版本不等于角色源 schema，不改变源包版本。

| 字段 | 类型与来源 | 约束 |
| --- | --- | --- |
| `format`、`schemaVersion`、`exportLayoutVersion` | 固定格式与布局版本 | 只表示摘要及导出布局 |
| `packageId`、`packageStableId` | 原编译结果 | 必须匹配目标包 |
| `sourcePackageVersion` | 源 manifest | 保留原版本 |
| `compileStatus` | 原编译状态 | 不能解释成 Unity Editor Ready；不新增 `ready=true` |
| `compilerHashes` | 原结果的 algorithm / sourcePackageHash / generatedConfigHash / geometryBindingHash / resourceMappingHash / writePlanHash | 是 compiler canonical hash，不冒充最终落盘文件字节哈希 |
| `diagnosticCounts` | 原 GateReport 的 error / warning 数量 | 不内嵌完整 issues、stack trace 或 sourceMappings |
| `counts` | package resource、runtime resource、animation clip、animation set、config entry 的数量 | 每类一个整数，不列逐项数据 |
| `artifactRefs` | 第 4.2 节固定清单 | 包根相对路径，禁止绝对本机路径和内容载荷 |

要求：UTF-8，无 BOM，LF，固定属性与数组顺序，单个末尾换行；不含时间戳、GUID、运行序号或机器路径。相同输入和相同布局生成相同字节。内容相同时不重写摘要。`000008` 摘要上限 **8 KiB**；超过时修正重复结构，不截断诊断来强行通过。

摘要不是新的加载清单、发布门禁或第二份身份数据库。Runtime、Readiness 不得因摘要缺失或未知版本而丢弃原有可加载包。未知版本只影响工具展示和本次迁移覆盖行为：保留原文并报告不支持。

**消费者（必须明确，否则本文件不应进正式包）**：摘要的唯一消费者是 **CharacterStudio 的布局/计数展示**（读取 `exportLayoutVersion`、`compileStatus`、`diagnosticCounts`、`counts`、`artifactRefs` 用于技术信息面板）。约束：消费者读取失败或摘要缺失时必须降级为"未提供摘要"，不得阻塞导入、不得改变 readiness 判定。若实施时无法落地该消费者，则摘要改为仅随 `--diagnostics-out` 产出、不进正式包——不允许出现无消费者的新增受版本管理产物。

### 4.2 artifactRefs 固定清单

只引用下列已存在的导入产物，按路径排序，每项仅 `kind` 与 `path`：

- `config/character_config_patch.json`
- `config/geometry_binding.json`
- `config/resource_catalog_mapping.json`
- `config/unity_resource_catalog.json`
- `config/runtime_resource_catalog.json`
- `config/character_resource_plan.json`
- `config/animation_set_definition.json`
- `config/animation_clip_registry.json`
- `config/animation_resource_plan.json`
- `config/resolver_verification_plan.json`
- `config/resource_validation_report.json`
- `package_cache/import_report.json`
- `package_cache/animation_package_expectation.json`
- `package_cache/animation_validation_report.json`

不在摘要复制每个 Clip/资源条目；不引用尚未由 Unity Editor 生成的 bake/receipt，也不做目录递归索引。CLI 导入完成后摘要引用须存在；后续 Unity 阶段的就绪仍由原 receipt 判定。

### 4.3 完整诊断输出

保留现有 `character compile --out <dir>` 和 `animation compile --out <dir>` 的完整诊断输出合同。不要更改公开 `CharacterAuthoringCompileResult` 的字段或移除其 `AnimationCompileResult`。

新增 `character import-unity --diagnostics-out <dir>`，用于保存**同一次导入**的编译对象和动画对象，避免另跑 compile 得到另一个阶段的数据：

1. 默认不启用，不在正式包中隐式创建诊断目录。
2. 显式启用时输出两份完整快照、导入报告，以及说明阶段为 `compiler` / `preparedAnimation` 的小型诊断 manifest；记录 packageId、compilerHashes，内容来自实际导入对象。
3. 若要求转换后的证据，引用实际导入报告的 operation/artifact 信息；不得宣称原对象快照等于经过身份转换的最终 JSON。
4. 推荐目录 `Temp/CharacterPackageDiagnostics/<packageId>/<run>/`，也可使用调用者指定的外部诊断目录；拒绝写入 `Assets/`、`Packages/`、`Authoring/`、`ArtSource/`、`WeaponSource/`、`CharacterActionSource/` 及其重解析后的子目录。诊断路径预检须在导入写盘前完成。
5. 不复用或改写现有 `--report-out` 的语义，不把完整快照塞进 import_report。
6. 显式诊断输出失败时返回可识别的工具失败并说明主导入是否已完成；禁止伪装全部成功。诊断输出成功与否不能放宽编译/导入门禁。
7. 新参数先提供 CLI 入口；CharacterStudio 默认流程继续复用 CLI，不增加普通创作界面的开关。

## 5. 第一批 A：实施步骤

### A0：固定对照与消费者清单

- 记录 HEAD、目标源包/global library/identity catalog 的 hash，以及目标包下列文件集合的路径/长度/SHA-256：全部 `.json`、全部 `.meta`、本次将被写入或删除的文件、`resources/`+`previews/` 下的全部文件。**不要求对全部资产做全量哈希**（9 个包全量约 320 MB I/O，对判定"差异是否由本次优化导致"无贡献）。证据放本地 `Temp/CharacterPackageExportOptimization/`。
- 复核两份快照的读取、写入和打包引用，包含测试、PowerShell/Python/JS、动态 Path.Combine 和仓库外 CI 的显式配置。未发现仓库外消费者不等于证明其不存在，交付记录中保留此边界。
- 使用相同源输入、相同依赖和 identity catalog 建立“当前生成器重新导出的基线”；不能只用可能陈旧的已提交生成目录与新结果比较。
- 先对隔离测试项目执行导入；正式 `000008` 仅在代码回归通过后重导入。源配置不得为了压体积而删除动画或资源引用。

### A1：改变默认写入

修改 `CharacterPackageCommands.CreateUnityImportAdditionalWrites`：

1. 移除两份快照的默认 AddContentWrite。
2. 完整对象序列化仅在显式诊断导出时发生；默认路径不要先拼出大字符串再丢弃。
3. 保留独立动画产物、资源计划、预期与校验报告的生成。`PopulatePreparedClipBindings` 的执行顺序和身份转换合同不变。
4. 新摘要候选在内存构造，但不在导入桥刚开始写文件时就发布“本次已完成”的状态。

### A2：完成顺序与失败语义

在 `ImportUnity` 收口本次完成条件，使用已有状态、诊断和退出码，不另建一套 Ready 判定：

1. 编译与资源预检通过；预检诊断目标和可迁移旧文件。
2. 执行现有桥接写入，保持生成资产转换。
3. 执行 `PerformWriteAfterVerify`，检查新增 issue 的 severity/gate，不能只沿用此前的 `ErrorCount == 0`。当前此方法会追加 issue 和修改 CanSpawn，清理代码必须尊重这些变化。
4. 公共资源映射同步成功。依赖映射同步结果的最终报告，在同步之后重算状态；不得保留提前写出的假成功报告。
5. 检查第 4.2 节实际文件，完成第 6 节旧文件迁移；写最终导入报告。
6. 以同目录临时文件 + 替换方式发布摘要；相同字节跳过。摘要不把自身路径/hash 纳入自身计算，不引用自己。**摘要的 `.meta` 首次由 Unity refresh 生成，此后仅替换文件内容，`.meta` 保持不变**；禁止用"删除 + 新建"实现替换，否则 Unity 会重新分配 GUID 造成引用漂移。
7. 后续 Unity Editor 正常生成或验证 Prefab、Avatar、Registry、Bake 和正式 receipt。CLI 摘要不能代替这一阶段。

非 dry-run 且前四步全部成功才允许开始删除旧快照。摘要/报告写入失败、清理失败必须返回非成功并保留可重试证据。干跑不改项目内摘要、快照、`.meta` 或恢复文件；显式 `--report-out` / `--diagnostics-out` 只允许产生外部请求的预检证据。

这只保证本次新增“摘要/废弃文件”步骤可恢复，不宣称现有模型/配置/公共映射写入具备整包 ACID 回滚。不得顺带引入新的全项目发布框架。

### A3：Player 暂存过滤

在 `CharacterTestPackageTestCommand.StageGeneratedPlayerContent` 提取一个包相对路径判定，精确排除：

- `package_cache/compile_result.json`
- `package_cache/animation_compile_result.json`
- `package_cache/compile_summary.json`

只按规范化后的完整相对路径匹配，不按文件名通配或整目录过滤。保留所有其它现有复制规则和必要 `.bytes`；尤其保留 import_report、Unity catalog、weapon/hurtbox 索引。

该过滤同时覆盖未迁移旧包。不要为了让测试通过先清空所有包的快照。新增参数化的内部文件选择 helper 供 `000008` fixture 验证；保留当前无参入口默认 `000001` 的行为，不硬编码切换整个项目的默认角色。

## 6. 旧文件迁移、.meta 与恢复

新增内部 helper（建议名 `CharacterPackageExportCleanup`），由实际 `ImportUnity` 成功路径调用。它只拥有本文列明的废弃路径，不递归清理目录。

### 6.1 候选和所有权

- A 的删除白名单分两类，各自仅附带同名 `.meta`：
  1. **固定废弃文件**：`package_cache/compile_result.json`、`package_cache/animation_compile_result.json`（两项）；
  2. **未声明孤立资源**：`resources/`、`previews/` 下**不在本次导入 `import_report.resourceFile` 集合内**的文件。判定必须使用规范化后的完整相对路径逐项比较，不使用通配符、不递归目录。该集合在每次导入时重新计算，不持久化、不缓存。
- B 完成后固定废弃文件扩展三项镜像（`generated/character_application_config_patch.json`、`generated/character_geometry_binding.json`、`generated/character_resource_mapping.json`）。
- 孤立资源的删除必须额外满足：文件扩展名属于该包资源目录的既有类型（`.fbx`/`.glb`/`.png`/`.tga`/`.jpg`/`.wav`/`.ogg`/`.anim`），且同目录下不存在同名 `.meta` 指向其它资产。不满足即保留并报告。
- 目标根取已验证的 write plan target root；每个完整路径必须规范化并验证在该包根内，拒绝 `..`、绝对子路径和穿越根的 symlink/junction。保留自定义 `--unity-root` 的合法既有语义。
- 删除前检查 JSON 的已知格式/结构及 packageId。动画快照的 `packageId` 应为对应 `animation.<packageId>`，不能拿它直接与六位 ID 作等值判断。
- 不认识的文件、未来版本、包身份不匹配、非正常文件或外部修改冲突：保留并报告明确诊断，不覆盖、不默默跳过后声称迁移完成。
- 无目标文件是正常幂等情况。仅剩 `.meta` 时先记录其字节/哈希并作为该白名单的孤立元数据处理，不扩展到其它 `.meta`。

### 6.2 执行与恢复

1. 预检所有候选及其 `.meta`；在开始删除前，将需删除的原始字节和相对路径/哈希清单备份到本地 `Temp/CharacterPackageExportMigration/<packageId>/<run>/`。备份不提交。**因 `Temp/` 属可被清理的本地工作状态，必须同时提供 `--migration-backup <dir>` 允许指定外部备份目录**，并把备份位置与清单摘要写入导入报告，使"备份是否存在"可被后续核查；未指定时使用默认 `Temp/` 路径并在报告中显式标注该风险。
2. 真正删除前再次比较哈希，避免删除预检后变化的文件；变化即冲突，旧文件保留。
3. 使用仓库生成器的定向文件操作；不得串联 Shell 递归删除。Unity 序列化资产不手写，保留文件的 `.meta` 不重建、不随机换 GUID。
4. 删除 JSON 与对应 `.meta` 为同一逻辑迁移操作；部分失败或后续摘要发布失败时，用备份恢复本次已删除内容。若恢复本身失败，返回错误及可用备份位置，不声称已恢复。
5. 成功记录删除/缺失跳过/恢复/失败的路径与结果。复用既有 import report issue/operation 机制；若增加 report 字段，验证旧 Runtime 读取兼容，不更改 Runtime readiness 解释。
6. 完成后 Unity refresh 生成新增摘要所需 `.meta`，核对被删除文件无孤立 `.meta`、其它资产 GUID 不变。新摘要及其 `.meta` 随正式生成产物版本化。
7. 失败重试只处理白名单残余。内容相同的摘要第二次导入不应被重写，废弃文件不得复活。

不执行全仓自动清理。首个正式迁移目标为 `000008`；其余包逐包重导入自然升级。旧 CLI 再次导入可能写回快照，因此迁移上线需同步更新制作环境的 CLI 版本；新 CLI 重导入应再次完成清理。

## 7. 第二批 B：三组镜像收敛

### B1：单一 writer

1. 将 `CharacterAuthoringCompiler.Compile` 中三项 write entry 的 TargetPath 改为第 3.2 节 `config/` 路径，保留 SourcePath、Kind、Owner 和内容语义。
2. 删除 CLI AdditionalWrites 对这三份相同目标的重复注册。导入计划与 AdditionalWrites 合并后不得有重复 TargetPath。
3. 保留 `CreateGeneratedArtifacts` 的三个 payload 及导入桥转换路径，确保 `CharacterImportedPackageJson` 继续从原有 config 路径读取。
4. 查找旧路径的测试、Builder、导出/清理脚本并定向迁移；测试断言改为“正式目标可被加载且唯一写入”。
5. 迁移白名单扩展为三个旧镜像；Player 相对路径排除清单同步增加三项。保持 `generated/` 中的烘焙文件。

### B2：写入计划哈希顺序

当前 Compiler 在插入三项生成写入之前调用 `BuildHashes`，而 `BuildWritePlanCanonicalText` 会遍历 Writes。B 必须显式处理此顺序，不能假设换目标路径一定会进入当前持久化的 WritePlanHash。

目标顺序：先计算 source/config/geometry/resource mapping hashes，形成包括三项正式配置目标的完整 compiler write plan，再对最终计划计算 WritePlanHash。不存在自身引用：摘要不进入 compiler plan，三项 payload 的 ContentHash 不引用 WritePlanHash。

该调整只收口已有 compiler plan 的哈希，不把所有 CLI AdditionalWrites 塞入 plan，不改变其它 canonical hash 的算法。所有使用同一 Compiler 的 Readiness、导入报告、摘要、Unity receipt 比对必须采用同一新值；重新执行 Unity 验收，禁止手改旧 receipt 冒充新值。

验收必须包含“最终计划重新计算的 canonical hash 等于持久化 WritePlanHash”，以及改变正式目标路径会改变该哈希。保留 runtime 数值身份和 config 内容不变的证据。

**发布前置条件：全仓 receipt 集体失效。** `EditorServer.Readiness.EvaluateUnityImportReceipt`（`EditorServer.Readiness.cs:1005` 起）接收 `currentHashes` 并返回 `Failed` / `Rejected` / `Deferred`；同一文件 `:763-770` 对 import report 也执行 `HashEquals(reportSourceHash, currentHashes.SourcePackageHash)`。B2 改变 `WritePlanHash` 语义后，**所有已导入包的既有 receipt 与新编译值不再相等**，未重导入的包会立即出现哈希不一致，这不是"自然升级"而是"自然降级直到有人重导入"。因此：

- 升级 CLI 前必须先规划一次全仓重导入窗口，并产出"受影响的已导入包清单"；
- B 的交付物必须包含该清单与逐个重导入后的 receipt 证据；
- 不得通过手改旧 receipt 或复用旧 hash 来消除不一致。

## 8. 代码落点与任务拆分

| 工作项 | 修改/新增位置 | 交付边界 |
| --- | --- | --- |
| A0 基线、消费调查 | 测量 helper 可放 `Tools/MxFramework.Authoring/scripts/`；结果放 Temp | 字节清单、消费者清单、未覆盖项 |
| A1 摘要与默认输出 | `src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs`；新增摘要 DTO/builder 文件 | 取消两份默认序列化与写入，保持独立产物 |
| A2 诊断导出 | 同 CLI；参数说明更新 `Tools/MxFramework.Authoring/README.md` | 显式诊断路径、同次导入快照、失败退出码 |
| A2/A4 成功收口与迁移 | `CharacterPackageCommands.ImportUnity`；新增 cleanup helper | 正确晚于校验/映射同步、白名单与恢复 |
| A4 孤立资源回收 | 同上 cleanup helper；判定输入为 `CharacterUnityImportReport.Operations` 的 `resourceFile` 集合 | 未声明 `resources/`+`previews/` 文件及 `.meta` 被回收，边界目录不受影响 |
| A3 Player 暂存 | `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPackageTestCommand.cs` | 精确路径过滤，不改默认角色 |
| A/B noEngine 回归 | `tests/MxFramework.Authoring.Tests/CharacterPackageTests.UnityImportBridge.cs`；新增 `CharacterPackageTests.ExportLayout.cs`；`Program.cs` | 新增聚焦分组 `character-export-layout`，保留旧必要行为覆盖 |
| A/B Unity 回归 | `CharacterTestProfilerGcRemediationTests.PackageTestCommand_StagesCodexFlowTestGeneratedConfigForPlayer` 及聚焦新增用例 | 暂存过滤、加载路径、索引/payload 保留 |
| B producer 收敛 | `src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringCompiler.cs` + CLI | 三份正式 writer 唯一，最终计划哈希正确 |
| B compiler 回归 | `CharacterPackageTests.Compiler.cs` 与 ExportLayout 用例 | 确定性、哈希、旧目标消失、内容等价 |
| 文档与资产收尾 | 本文、数据归属、接口/白皮书命中章节、000008 生成包 | 如实标状态，记录验证结果，保存成对 `.meta` |

上表 `src/`、`tests/` 的前缀均为 `Tools/MxFramework.Authoring/`。新增名是建议内部命名，不是当前已存在 API。

建议交付为两个可回退变更：A 完整包含默认输出、诊断、迁移、Player 过滤、测试、000008 证据；B 再包含镜像收敛与哈希更新。不能把“删旧文件”留给使用者手工执行。

## 9. 验证矩阵与通过标准

### 9.1 必需的自动化用例

| 场景 | 必须证明 |
| --- | --- |
| 新目标首次导入 | 两份快照不生成；摘要存在且 ≤ 8 KiB；独立配置可读 |
| 现有旧包升级 | 已识别快照及 `.meta` 删除；其它文件和 GUID 未被清理器改动 |
| 孤立资源清理（A 新增） | `resources/`、`previews/` 下未被本次导入声明的文件及其 `.meta` 被删除；被声明的 4 项资源、`prefabs/`、`materials/`、`preview_materials/`、`runtime_resources/` 完全未被触及；含同名 `.meta` 指向其它资产或扩展名不在白名单时保留并报告 |
| 孤立资源误删防护 | 用"当前导入声明集合"逐项比较而非通配符；构造一个未声明但与已声明文件同名的不同目录文件，验证不被删除 |
| 二次相同导入 | 废弃文件不复活，摘要字节与修改时间稳定，清理缺失项正常跳过 |
| dry-run | 目标目录、摘要、旧快照、`.meta` 的前后字节与存在性完全相同 |
| 编译/桥接/写后校验/映射同步失败 | 不执行迁移，不发布新成功摘要；退出结果与实际失败一致 |
| 删除或摘要发布失败 | 无假成功；恢复已删文件或明确报告恢复失败/位置，可重试 |
| 未知版本/错误 packageId/变化哈希 | 保留冲突文件并明确报告；不误删 |
| 路径越界、junction 或错误诊断目标 | 在写入/删除前拒绝，不触及包外内容 |
| 显式诊断导出 | 同次导入的完整对象只写到指定诊断目录；摘要不嵌套 payload |
| 现有 compile/animation --out | 完整导出仍可用，原 CLI 命令含义保持 |
| 摘要确定性与有界性 | 相同输入字节一致；增加 Clip 不产生逐 Clip 摘要；无时间戳/绝对路径 |
| Readiness 回归 | 新旧布局都通过原报告/receipt 链判断；不依赖完整快照和摘要 |
| Player 暂存旧布局 | 即使输入仍有两份旧快照也不复制；import_report 和索引/payload 仍在 |
| Player 暂存新布局 | 不复制摘要；保留现有全部必需文件；不误伤同名不同路径文件 |
| B 单一 writer | 合并写入集合 TargetPath 唯一，三个旧镜像不再生成 |
| B hash 顺序 | 持久化 WritePlanHash 与最终计划重算一致，相关 receipt 重新生成并验真 |
| B 数据等价 | config、geometry、mapping 解析结果与同输入基线相同，数值身份不漂移 |

有意义的测试应执行导入、删除、加载与失败路径；不能只断言源码含某个字符串。复用现有 suites 的 fixture 与临时项目；有必要时为文件 I/O/映射同步失败注入窄接口，不扩展成通用事务框架。

### 9.2 验证入口

Authoring 是独立工具，此处验证其 .NET 工程；它不代表 Unity 脚本编译通过。按该工具 README 构建最新代码后，执行：

```powershell
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-export-layout
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-unity-import-bridge
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-unity-import-receipt
```

第一条分组需本任务新增；后两条已存在。B 还需注册/运行 compiler 确定性与最终计划哈希的聚焦用例。只有复用了 SourceContract 执行器时才追加 `character-source-contract`。

改到 Unity C# 后，优先单次 `execute_custom_tool(tool_name="validate_unity_scripts", parameters={})`。必须同时获得 refresh completed、编译开始/结束、domain reload observed、Editor ready、Console error count = 0 的证据。仅工具不可用或明确失败才分步回退；不枚举 MCP resources、不以辅助 csproj 构建代替 Unity 编译。

Player 暂存过滤属于实际改动，必须运行上述聚焦 EditMode 用例。无需为本任务运行全仓测试；若声称真实 Player 运行通过，必须另外提供对应构建与运行证据，不能用暂存测试代替。

### 9.3 正式 000008 验收

1. 记录同输入旧生成器基线；保护已有未提交内容。
2. 通过正常 CLI/CharacterStudio 导入 `Authoring/CharacterPackages/character-000008`，再通过既有 Unity Import Bridge 完成 Editor 阶段；不要手改导出 JSON。
3. 核对源包/公共库/身份目录未被本次精简改写；保留配置、资源键、动画集合及关键 JSON 与基线等价。若当前源输入本身变化，先排除后才能宣称由本次优化导致的差异。
4. 同输入下 111 个 Clip、116 个 runtime catalog entries、2 个 animation sets 保持；Prefab/Avatar/Material 与 bake 校验仍通过。允许摘要和报告记录实际布局/操作变化，不允许改资源数量来达到体积指标。
5. 从精简包执行正式目录加载与角色生成，验证待机/移动及一个现有攻击动作的动画、武器/受击数据解析。复用已存在的角色验证入口；若包在基线即有阻断，记录原阻断并使用可用 fixture 覆盖实现回归，不伪造 Ready。
6. 默认 000001 的 Player 暂存回归与 000008 的参数化文件选择/目录加载回归分别记录，不混为同一实机结果。
7. 重复导入一次，复测旧文件消失、摘要不变、保留 GUID 不变。

### 9.4 体积门槛

| 阶段 | 000008 理论字节上限，摘要按 8 KiB 计 | 目标数量 | 通过口径 |
| --- | ---: | ---: | --- |
| A（JSON 目录） | 1,882,100 − 956,032 + 8,192 = **934,260** | 32 个 JSON | ≤ 0.90 MiB，约减少 50% |
| A+B（JSON 目录） | 934,260 − 87,207 = **847,053** | 29 个 JSON | ≤ 0.81 MiB，约减少 55% |
| A（含孤立资源回收） | 934,260 − 16,900,732 = 目录净减 16,866,472 | 非 JSON 减少 1 | `resources/models/character_00.fbx` 消失且 `.meta` 无孤立 |
| A（Player 暂存，默认角色 000001） | 3,085,106 − 890,426 + 8,192 = **2,202,872** | — | ≤ 2.21 MB，**约减少 28.6%** |
| A（Player 暂存，样本 000008） | 2,602,359 − 956,032 + 8,192 = **1,654,519** | — | ≤ 1.66 MB，约减少 36.4% |

暂存口径说明：Player 暂存只复制全部 `.json` 与 `.bytes`（`CharacterTestPackageTestCommand.cs:323-348`），因此这是本方案**相对收益最大**的口径。两个口径必须分别记录，不得把"JSON 目录 −50%"表述为整包收益。

上述是固定样本、其它 JSON 不变时的预算。必要报告字段或当前生成器产生合理小量变化时，证据必须逐项给出字节差异和调整后的实测值，不通过删除报告来硬凑数量。另统计 `.meta` 和 Player 暂存的前后值；未实测的压缩包、Git 历史、内存与启动耗时不计收益。

**明确不计入本方案收益的部分**（占 000008 包体 89%，本次不处理，仅登记以免误判"体积优化已完成"）：`resources/models/character_00.fbx` 为孤立文件（已纳入 A，见 §2.1.1）；`previews/character_00.preview.glb` 11,618,384 B 登记为 `usage=previewMesh`、位于 `character_resource_plan.uiDeferred` 且 `required=false`，因 Player 暂存只复制 `.json`/`.bytes` 而**不进 Player**，属编辑器预览资源；`resources/models/000008_lod0.fbx` 3,395,612 B 是正式 `characterModel`。

## 10. 文档同步、交付物与回退

文档必须写清：每个产物的真实用途、唯一生成者、磁盘消费者、是否进入已验证的 Player 暂存流程、重建方式、旧路径迁移方式。不要从“位于 Assets”“叫 cache”“理论应剥离”直接推导打包结果。

更新范围：

- `Tools/MxFramework.Authoring/README.md`：默认精简行为、显式诊断参数与示例。
- `Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md`：源、正式派生配置、轻量摘要、外部诊断的边界。
- `Docs/Interfaces/CharacterApplication.md`：仅补充实际消费/导入合同；必要时记录 report 的兼容附加字段。
- `Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md` §4.2：纠正实际布局、三组镜像和烘焙索引的发行归属；文档已有他人未提交修改时只做定向合并，不整篇覆盖。
- 本文：保留 Design 意图，同时追加每批 commit、测试入口/结果、前后清单和未验证项；不得在实施前把状态改为完成。

交付清单：

- [x] A 的代码、聚焦测试、文档、000008 正式再生成及配对 `.meta`，见 §12.10。
- [x] 迁移前/后逐文件字节与哈希清单；本轮沿用当前生成器，投影变化及非同工具链比较限制见 §12.10。
- [x] CLI 导入结果、Unity 编译/Console 证据、Editor receipt、角色加载/生成计划及 Preview Scene 实例化结果。
- [x] Player 暂存过滤的旧布局和新布局证据，包含实际复制与加载。
- [x] dry-run、失败不清理与恢复的 Authoring 回归见 §12.2；正式二次导入见 §12.10。
- [x] B 的单一 writer、最终 WritePlanHash 与 receipt 更新证据。
- [x] 本轮保留其它任务变更；必要生成资产变化与附带修复已列明。Temp/Library 仅保留本地证据，未提交。

回退 A：回退生成策略、过滤规则和对应已生成资产变更；旧生成器正常重导入会恢复快照。临时迁移失败只按备份清单恢复本次拥有的文件，禁止整个工作区 reset。

回退 B：恢复原 producer 目标路径、重复写入和哈希规则后重导入，并重新进行 Unity receipt 验证；不能保留 B 的 receipt 再宣称旧布局已经通过。不要手工复用不匹配的 hash。

## 11. 可直接交给实施人的任务说明

按照本文实施角色包导出精简，先完成 A，再完成 B。先核对当前源码和局部规则，保存同输入基线；不要直接批量删除 JSON。保持运行配置、烘焙索引、二进制数据和原有就绪门禁，完整诊断改为显式外部导出。旧产物清理必须接到实际导入成功路径并有失败恢复；同时过滤旧包进入 Player 暂存的废弃快照。完成聚焦测试、Unity 验证和 000008 再生成，交付逐文件体积对照与未覆盖项。每批独立提交；是否推送按交接人的明确要求执行。

---

## 12. 实施记录（2026-09-19）

### 12.1 已实现

| 批次 | 落点 | 实现内容 |
| :--- | :--- | :--- |
| A1 | `Cli/CharacterPackageCommands.cs` | 移除两份快照的默认 `AddContentWrite`；完整对象仅在显式诊断时序列化（`includeFullSnapshots` + `CharacterPackageImportPayloads`） |
| A1 | `Cli/CharacterPackageCompileSummary.cs`（新增） | `CharacterPackageCompileSummary` DTO + builder：固定格式 `mx.characterPackageCompileSummary.v1`、`exportLayoutVersion`、`compilerHashes`、`diagnosticCounts`、`counts`、14 项 `artifactRefs`；确定性序列化与 8 KiB 有界校验 |
| A2 | `Cli/CharacterPackageCommands.ExportLayout.cs`（新增） | `--diagnostics-out` / `--migration-backup` 参数；诊断目标预检（拒绝 `Assets/`、`Packages/`、`Authoring/`、`ArtSource/`、`WeaponSource/`、`CharacterActionSource/`）；摘要以临时文件 + 覆盖替换发布（保留 `.meta`） |
| A2/A4 | `Cli/CharacterPackageExportCleanup.cs`（新增） | 定向清理器：固定废弃路径 + 未声明孤立资源；备份 + 哈希前置 + 冲突保留 + 幂等跳过 + 路径越界与 reparse point 拒绝；结果写入 import report 的 operation/issue |
| A3 | `Assets/.../CharacterTestPackageTestCommand.cs` | 提取 `IsExcludedPlayerGeneratedPackageFile` / `SelectPlayerGeneratedPackageFiles`，按规范化完整相对路径排除 3 项；新增参数化入口 `StageGeneratedPlayerContent(string)` |
| B1 | `Core/CharacterPackages/CharacterAuthoringCompiler.cs` + `Cli/CharacterPackageCommands.cs` | 三项 write entry 目标改为 `config/*`；删除 CLI 对同目标的重复注册，形成单一 writer |
| B2 | `Core/CharacterPackages/CharacterAuthoringCompiler.cs` | 在插入三项正式目标之后重算 `WritePlanHash`，使持久化哈希覆盖最终计划 |

### 12.2 验证证据

| 项目 | 结果 |
| :--- | :--- |
| Authoring 构建 | 0 warning / 0 error |
| 全量 Authoring 测试（含新增 `character-export-layout`） | `MxFramework.Authoring.Tests passed.`（7m44s） |
| 聚焦套件 | `character-export-layout`、`character-unity-import-bridge`、`character-unity-import-receipt`、`character-source-contract`、`character-actions`、`character-readiness-dedupe` 全部通过 |
| Unity 编译 | `validate_unity_scripts`：refresh completed、compilation finished（9 assemblies）、domain reload observed、Editor ready、Console error count = 0 |
| 隔离副本端到端迁移（000008 副本） | `status=Ready`；`exportCleanup=deleted:6 missing:0 conflict:0 failed:0`；已声明 4 项资源全部保留 |
| 体积（000008 副本） | 非 meta 文件 47 → 42；JSON 1,882,100 → **849,660 B**（预算 ≤ 934,260）；摘要 2,578 B ≤ 8 KiB |
| 幂等性 | 二次导入 `deleted:0 missing:5`、`compileSummary=Skipped`、`status=Ready`；废弃文件未复活 |
| B2 哈希顺序 | 对持久化写入计划重算 canonical 哈希 = `sha256:85175c63…`，与 import report、摘要三处**完全一致**；三项正式目标在计划内、旧镜像目标已消失 |
| dry-run | 孤立文件、旧快照、摘要均未被修改 |
| 失败不清理 | 负路径用例 `status=ImportBlocked` 时输出 `exportCleanup=Skipped (import did not complete successfully)` |

### 12.3 首轮未完成 / 未验证（历史快照，收尾结果见 §12.10）

1. **正式 `000008` 未迁移**：真实包保持基线（47 个文件、JSON 1,882,100 B），迁移仅在隔离副本上验证。正式迁移会同时触发公共资源映射同步，建议在确认 CLI 版本后与 Unity 阶段一并执行。
2. **Unity Editor 阶段未执行**：摘要 `.meta` 需 Unity refresh 生成；Prefab / Avatar / Registry / Bake / receipt 的重新生成未执行。
3. **Unity EditMode 暂存用例未跑通**：`CharacterTestProfilerGcRemediationTests.PackageTestCommand_StagesCodexFlowTestGeneratedConfigForPlayer` 已补充"旧布局快照不得暂存"断言，但本会话 Test Runner 对该过滤条件三次均返回 `total=0`（含按程序集 `MxFramework.Tests` 过滤），未能取得执行证据。该项不得记为已验证。
4. **Player 暂存字节未实测**：`000001` 暂存集合 3,085,106 → 2,202,872 B（−28.6%）为按当前文件集合的推算，需在 Player 构建后实测确认。
5. **§3.3 待裁决项未裁决**：`package_cache/{manifest,resource_catalog,character_application}.json` 仍按原样保留，消费者证据未补。
6. **无独立测量脚本**：§8 提到的 `Tools/MxFramework.Authoring/scripts/` 测量 helper 未实现，基线以一次性脚本完成。

### 12.4 附带修复（与本方案无关）

`Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.Palette.cs` 存在一处未提交改动引入的 `CS0136`（`material` 在嵌套作用域重复声明），导致 Unity 工程无法编译并阻断本方案的 Unity 侧验证。已做最小修复：将 `CollectPaletteBodyMaterialBindingStatus` 内层局部变量重命名为 `bakedMaterial`。该文件其余未提交改动未被触碰。

### 12.5 真实工程侧旁证（`000009`，非本会话执行）

本会话期间，另一个并行会话在作者完成本批次代码构建之后（`authoring.dll` 17:50:12）对正式包 `000009` 执行了一次导入（`package_cache/import_report.json` 17:51:24）。该次运行使用的是本批次代码，结果可作真实工程侧证据：

| 项 | 实测 |
| :--- | :--- |
| 导入状态 | `status=Ready`、`errorCount=0`、`conflictCount=0`、`canSpawnAfterImport=True` |
| `exportCleanup` | 5 项，全部 `Deleted`：2 份废弃快照 + 3 份废弃镜像 |
| 已声明资源保护 | 7 项 `resourceFile`（含新增的 `resources/textures/baked/BaseColor.png`、`SurfaceData.png`）全部保留，**无一被误删**；含子目录资源 |
| 摘要 | 生成 `package_cache/compile_summary.json`，`exportLayoutVersion=2`、2,578 B |

该次运行未出现任何孤立资源误删，说明"按 `import_report.resourceFile` 集合判定已声明"的保护在含子目录资源的真实包上成立。

注意：该次导入同时包含与本方案无关的在途改动（baked surface / hair 相关），本方案不主张这些改动；此处仅引用其清理与保护结果。

### 12.6 后续修复（同批次内完成）

测量脚本（`Tools/MxFramework.Authoring/scripts/measure_character_package_export.py`）在对照基线时暴露出两个真实缺陷，均已修复并复验：

| 缺陷 | 证据 | 修复 |
| :--- | :--- | :--- |
| **重复写入目标**：`package_cache/character_application.json` 同时被 `CharacterAuthoringCompiler.BuildWritePlan`（`kind=resourceFile`）与 CLI `CreateUnityImportAdditionalWrites`（`kind=packageCache`）注册，违反 §7 B1「合并后不得有重复 TargetPath」 | 导入报告中该目标出现 **2 次** | 删除 CLI 侧注册，编译器计划成为唯一 writer（与 B1 对三项镜像的处理一致） |
| **正式 config 丢失结尾换行**：`CreateGeneratedArtifacts` 不带 `EnsureTrailingNewline`，B1 将正式 `config/*` 的载荷来源切到它之后，`character_config_patch.json` / `geometry_binding.json` / `resource_catalog_mapping.json` 失去结尾换行，违反 B1「内容语义不变」 | 迁移后三个文件尾部无 `\n`，基线均有 | 在 `CreateGeneratedArtifacts` 三处补 `EnsureTrailingNewline` |

复验（全新隔离副本）：三个正式文件均以单个 `\n` 结尾且无双换行；导入报告无重复 TargetPath；`resource_catalog_mapping.json` 与基线**字节一致**（5,680 = 5,680）。新套件同时加入"无重复 TargetPath"与"正式 config 结尾换行"两条断言。

另两处 content 差异（`character_config_patch.json` +1,652 B、`geometry_binding.json` +1,439 B）**不是本次改动造成**：差异集中在 `generatedReferences[].sourceObjectPath`（`geometry/bodyParts/0` → `torso`）、`bodyPartContactPoints[].bodyPartId`（`701` → `51`）、`weaponAttachments[].weaponRef.*`（`weapon.common_weapon_1.model` → `weapon.000001.model`）等，属并行的数字武器身份与 baked surface 在途工作。按 §9.3，须待这些改动落地后以同源同工具链重建基线才能断言差异归属。

### 12.7 体积实测（`measure_character_package_export.py`）

| 口径 | 000008 基线 | 000008 迁移后（隔离副本） | 变化 |
| :--- | ---: | ---: | ---: |
| 非 meta 文件数 | 47 | 42 | −5 |
| JSON 字节 | 1,882,100 | 848,279 | **−54.9%** |
| `config/` JSON | 770,283 | 775,903 | 见 12.6 说明 |
| `generated/` JSON | 89,137 | 1,930 | −97.8% |
| `package_cache/` JSON | 1,022,680 | 70,446 | −93.1% |
| 非 meta 总字节 | 35,688,548 | 17,752,684 | **−50.3%** |
| 孤立资源 | 1 个 / 16,900,732 B | 0 | 已回收 |
| Player 暂存（过滤前 → 过滤后） | 2,602,359 → 1,646,327 | 1,568,538 → 1,565,960 | 相对基线过滤前 **−39.8%** |

Player 暂存口径说明：基线的"过滤后"是按新过滤规则对旧布局重算（排除 2 份快照），用于体现过滤本身的效果；真正的端到端变化是 `2,602,359 → 1,565,960`。以上数值均由 `measure_character_package_export.py` 实测输出。

### 12.8 18:30 剩余步骤与阻塞原因（历史快照，收尾结果见 §12.10）

> 2026-09-19 18:30 更新：第 2、3 项已由 §12.9 的真实工程只读证据部分关闭，见下。

1. **正式 `000008` 未迁移**——**主动暂缓**，非能力限制。执行时点检查（18:06 / 18:28 / 18:31）显示：共享文件 `Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json` 于 18:05:18 被另一并行会话修改，`000009` 与 `CharacterImportedPackagePrefabBuilder.*` 持续写入中（baked surface 在途开发，最近一次写入 18:27:09）。该导入会写同一个全局 profile 并触发 Unity refresh，与在途工作争抢共享文件，故留待协调窗口。
2. **Unity Editor 阶段**——`000009` 的真实迁移已佐证 refresh 会为摘要生成 `.meta` 且后续内容替换保持 GUID 不变（§12.9）；`000008` 的 Prefab / Avatar / Registry / Bake / receipt 重新生成仍未执行。
3. **Unity EditMode 暂存用例**——当时测试运行器对过滤条件返回 `total=0`；§12.9 只证明生产方法的路径过滤和文件选择结果，尚未证明实际复制、同名不同路径保留和不同包暂存隔离，不能关闭完整暂存验收。后续实际测试执行见 §12.10。
4. **§3.3 待裁决项已裁决**：`package_cache/{manifest,resource_catalog,character_application}.json` 经全仓代码检索（`.cs`/`.js`/`.mjs`/`.ts`/`.html`）确认**无生产消费者**，仅 Unity EditMode 测试夹具引用其路径。决定：**保留**（合计 16.8 KB，删除收益不足以承担未知外部工具依赖的风险），并在本文记录该结论与证据；后续如接入增量比对消费者，需同步更新 §3.3。

### 12.9 真实工程侧验证（只读，2026-09-19 18:30）

**A3 Player 暂存过滤——在运行中的 Unity Editor 内直接调用生产方法验证**（`execute_code` + 反射调用 `CharacterTestPackageTestCommand`）：

```
isExcluded(package_cache/compile_result.json)          = True   源包内存在该旧快照
isExcluded(package_cache/animation_compile_result.json)= True   源包内存在该旧快照
isExcluded(package_cache/compile_summary.json)         = True
isExcluded(package_cache/import_report.json)           = False  必需文件不被排除
isExcluded(package_cache/compile_result.json.bak)      = False  精确路径匹配，不做通配
selectedCount                                          = 40
selectedContainsExcluded                               = False  实际选择集合不含任何排除项
selectedHasImportReport                                = True
selectedHasBytes                                       = True
```

该次调用证明：输入仍含旧快照时，**选择集合不包含快照**，且集合包含 import_report 与至少一个 `.bytes`。它没有执行完整暂存复制，也没有覆盖同名不同路径；`.bak` 是不同文件名，不能代替后者。不对直接调用与测试运行器笼统排序。`character-export-layout` 是已通过的独立 Authoring 测试；当时的 `total=0` 仅指 Unity EditMode 过滤调用，不应混写。

**摘要 `.meta` 的 GUID 稳定性——由 `000009` 的真实迁移佐证**：

| 文件 | 时间 |
| :--- | :--- |
| `000009/package_cache/compile_summary.json` | 18:19:46（被后续导入重写） |
| `000009/package_cache/compile_summary.json.meta` | **17:53:35（未随内容重写而更新，GUID `ca5e1f5cab140ce48b3de514fedbe304`）** |

这些时间戳和当时读出的 GUID 是 `000009` 摘要元数据保留的旁证，不等于受控的导入前后字节/GUID 对照，也不覆盖其它资产。`000009` 被删除的 5 个废弃文件（2 快照 + 3 镜像）对应 `.meta` 当时均不存在。正式 `000008` 的重复导入对照另见 §12.10。

该次检查说明真实工程中已有摘要 `.meta`；不能据此验收 `000008` 的 Prefab / Avatar / Registry / Bake / receipt。该包后续结果见 §12.10。

### 12.10 正式收尾（2026-09-19）

**结果：正式 `000008` 已迁移，Unity 成功回执已更新；暂存串包和 Prefab 镜像 GUID 漂移已修复。** 本节替代 §12.3、§12.8 的当前状态判断，保留前述内容作为历史证据。

#### 修复内容

1. `CharacterTestPackageTestCommand` 的参数化入口不再固定写入 `000001`。目标由导入报告中的 `packageId` 与源目录一致性校验决定，为 `CharacterPackages/<packageId>`；拒绝非法身份、源/目标重叠、Assets 外暂存根和目标链上的 reparse point。删除目标前先完成预检。
2. 增加显式暂存根供测试隔离。旧真实包暂存回归也改为专用临时目录，清理只作用于该测试拥有的目录，不再删除正式 `Assets/StreamingAssets/MxFramework/Generated`。
3. Player 过滤覆盖六项：两份完整快照、摘要、三份废弃配置镜像。新增实际复制检查，证明同名不同路径、import_report 和全部三组烘焙索引/payload 被保留；测量脚本同步该清单。
4. 全量 GUID 对照发现既有 `MirrorPrefabToRuntimeResources` 采用 DeleteAsset + CopyAsset，重建会更换 Runtime Prefab GUID。已改为既有目标只更新生成载荷并 ImportAsset，保留 `.meta`；首次目标仍使用 CopyAsset。增加“修改源 Prefab 内容后镜像内容更新但 GUID/meta 不变”的回归。
5. 首次正式重建已触发上述旧行为，因此从本次迁移前备份恢复 `000008` Runtime Prefab 的原始 `.meta`，再次运行修复后的镜像方法并重新验证 receipt。恢复的是原始字节，不是新造 GUID；最终原 GUID 为 `e8d56c5b69eb1e04692639aa0c5ef5b1`。
6. 根据交接人的明确授权，修复并行战术卡牌改动遗留的 CS1729：`CreateDefaultLoadout` 改用现有三参数 `TacticalCardLoadout("loadout.default", 50, cards)`，保持显示文案属于角色包的既定边界。

#### 验证证据

| 项目 | 本轮结果 |
| --- | --- |
| 既有暂存测试重新发现 | 使用完整测试名，真实 Test Runner 1/1 通过；原 `total=0` 本轮未复现，未修改 runner 或 asmdef。作业 `96158e2c4635495395a1dd0bdbebc5be` |
| 暂存与 GUID 最终回归 | **6/6 通过，0 失败、0 跳过**，2026-09-19 11:31:25Z～11:31:53Z；保留正式 TestResults.xml。覆盖两包隔离、保留原包 meta、精确过滤后的实际复制、错误身份、路径重叠/越界、Prefab 内容更新后的 GUID 稳定性 |
| Unity 最终脚本验证 | `validate_unity_scripts` 作业 `e1d0b60b3ef74d47a4d22fb42cd07edf`：refresh completed、compilation started/finished、domain reload observed、Editor ready，**Console 0 error**；该轮 2 assemblies，0 编译 warning |
| Authoring CLI | 从当前源码构建到独立 Temp 输出，0 warning / 0 error；先 dry-run 再正式导入，均 Ready，0 errors / 0 conflicts |
| 首次正式清理 | `deleted:6 missing:0 conflict:0 failed:0`：五份废弃 JSON 及一份未声明旧模型，对应 meta 随同清理，备份保留 |
| 第二次正式 CLI 导入 | `deleted:0 missing:5 conflict:0 failed:0`，`compileSummary=Skipped`；0 errors / 0 conflicts |
| Unity 正式构建 | `Status=Built`；Humanoid Avatar `ValidHuman`；材质绑定、CharacterAction Profile/ActionSet Ready；武器 Trace、Socket、Hurtbox 均 Ready、0 issue |
| 烘焙证据 | Weapon Trace 114 artifacts / 8,793 checked frames；Hurtbox 111 clips / 10 colliders / 86,170 checked frames。Socket 本包 0 请求，是合法空结果，不宣称生成了不存在的轨迹 |
| 成功回执 | `ready=true`；摘要、import report、receipt 的五项 compiler hash 一致；GUID 修复后重新通过正式 receipt 验证方法 |
| 真实配置消费 | 从 `000008` 正式目录执行 `CharacterImportedPackageJson` + `CharacterRuntimeSpawnResolver`，`spawnReady=true`；在隔离 Preview Scene 实例化正式 Prefab，Humanoid Avatar 有效，场景已关闭 |
| 动画资源 | 通过正式 `CharacterRuntimeAnimationResources` 解析并加载 **111 个 Clip，0 missing** |
| 真实暂存 | 对正式 `000008` 执行生产暂存方法到隔离目录，实际 **31 个 JSON/bytes，1,630,983 B**；摘要未复制；暂存包再次加载/Spawn Resolve 成功。验证后经 Unity 删除该临时目录 |
| 摘要幂等性 | 第一次 Unity 导入后记录，再次 CLI/Unity 导入后比较：JSON 与 meta 字节、修改时间均不变；摘要 GUID `e314fa9133d7cb340b96e93d18966c20` |
| 保留资产身份 | 最终 **58 个原有且保留的 meta GUID** 与迁移前清单一致；废弃五份 JSON 及已清理旧模型无孤立 meta |
| 共享写入保护 | 检查实际 `ResourceMappingProfileFileAggregator`，已有跨进程写锁、写前 revision 复核和原子替换；本次导入共享 profile 最终连字节都未变，不靠“五分钟无写入”推断安全 |
| 文档注册表守卫 | 52 行 / 33 类型名 / 0 不可解析，PASS |

运行时边界：`CharacterRuntimeSpawnResolver` 此入口返回生成/注册计划，记录的 `CHAR_RUNTIME_DEFERRED_BINDING` 是非错误信息；Preview Scene 实例化和资源加载不等于完整 Gameplay 实体战斗或 Player 实机运行。

#### 最终数据与变更边界

| 口径 | 迁移前正式目录 | 收尾后正式目录 |
| --- | ---: | ---: |
| JSON 数量 | 33 | 29 |
| JSON 字节 | 1,882,100 | **854,869（减少 54.6%）** |
| 非 meta 文件数 | 47 | 42 |
| 非 meta 总字节 | 35,688,548 | **17,817,442** |
| 摘要字节 | 无 | **2,578，低于 8 KiB** |
| 旧完整快照、三份配置镜像 | 5 个 | 0 |
| 未声明旧模型 `resources/models/character_00.fbx` | 16,900,732 B | 已备份清理 |

原有已声明模型、贴图、预览资源均保留。JSON 实测比 §9.4 在“其它文件不变”假设下的 A+B 上限 847,053 B 多 7,816 B；本轮使用当前生成器重新投影配置、更新 Unity catalog/receipt 并重烘焙，不能将这些变化归因于删除镜像，也不将约 0.815 MiB 写成严格通过 0.81 MiB。数量、摘要预算、废弃产物消失和行为验证均已通过。

受保护的 `000009`、公共武器源与身份目录清单未发生本轮变化。`000008` 作者源中的唯一区别是 Unity 生成的 `package_cache/hurtbox_fit_draft.json` 工具缓存更新；没有修改作者的 geometry/config 权威文件，不把缓存更新报告成“全部源文件逐字节不变”。

本机证据位于 `Temp/CharacterPackageExportCloseout/`：`baseline/manifest.json`、`final-manifest.json`、`change-list.json`、`pre-import/protected-manifest.json`、两次 CLI 导入日志与报告、`unity-import-1.json`、`unity-import-2.json`、`unity-final.json`、`staging-and-prefab-tests.xml`、`runtime-spawn.json`、`animation-resources.json`、`actual-player-staging.json`、`measurement-final.json`、`final-verification.json`。该目录为本地证据/恢复材料，不提交。

本轮没有构建 Player 可执行文件，也没有验证完整战斗流程；这两项不计入此次导出精简、Editor 导入和暂存修复的完成声明。仓库存在其它任务改动，本轮未提交、未推送，也未回退它们。
