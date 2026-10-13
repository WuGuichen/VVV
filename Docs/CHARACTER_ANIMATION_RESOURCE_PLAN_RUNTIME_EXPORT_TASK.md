# 角色动画资源计划精简导出任务

> Status: Design
> Implementation: Not Started
> Updated: 2026-09-19
> Baseline: `3cd823848` 加当前未提交工作区；以已完成上一轮迁移的正式 `000008` 为样本。
> Scope: 仅精简正式角色包的 `config/animation_resource_plan.json`，同步其读取、版本、哈希绑定与验证。
> 文中的 v2、导出布局 3、新类型和测试入口均为待实施合同，不是当前已交付能力。

## 1. 任务目标与边界

将当前 126,382 字节的动画资源计划导出为仅含五个字段的运行用文档。完整动画资源目录、加载分组、音频清单和诊断继续保留在原编译对象及显式诊断输出中；运行时继续从已有独立配置获取实际资源与加载计划。

本任务只做此前建议中的第一项，不处理预览 GLB、不对所有 JSON 做紧凑序列化、不削减动画或资源数量、不更改烘焙格式，不扩展上一轮孤立资源清理范围。

前置成果为 [角色包导出精简方案 §12.10](CHARACTER_PACKAGE_EXPORT_SIZE_OPTIMIZATION_PLAN.md)：`000008` 已完成正式导入，摘要、镜像收敛、Player 过滤、暂存包隔离及 Runtime Prefab GUID 保留已经实现。本任务必须在这些成果之上执行，不能重新恢复废弃快照或三组镜像。

仓库根为 `E:\Unity\WxFramework\WGameFramework`。下文工程路径相对此根；行号只用于初次定位，实施时按符号确认当前源码。

### 1.1 必读入口

1. 根及修改路径的 `AGENTS.md`。
2. [SDK 与导入边界](CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md)、[数据归属](Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md)。
3. [Character Application 接口](Interfaces/CharacterApplication.md)、[Animation 接口](Interfaces/Animation.md)。
4. [API 规范](API_STANDARDS.md)、[质量门禁](QUALITY_GATE.md)的配置契约、角色导入和验证章节。
5. 本文列出的当前生产代码与测试；历史任务记录不能代替当前实现。

## 2. 已核实的现状

### 2.1 体积基线

统计原始 UTF-8 文件字节，不含 `.meta`、磁盘分配块或压缩包大小。**测量时点：2026-09-19 22:14（本文写作时）**。

| 项目 | 当前值 |
| --- | ---: |
| `000008/config/animation_resource_plan.json` | 126,382 B |
| 000008 全部 JSON | 854,869 B / 29 个 |
| 已验证的 Player 暂存 JSON + bytes | 1,630,983 B / 31 个 |
| 保留五个字段、使用本文 v2 示例格式 | 238 B（静态试算，须以实现实测复核） |
| 单文件预计减少 | 126,144 B，约 123.2 KiB |

当前文件 SHA-256：`0f311b2e08c9378a9d6dd7da887e20973588228ce6624b0dca492246030188ba`。实施前重新测量；若不同，保存新的基线，不恢复旧文件来迎合数字。

**基线漂移（2026-09-19 23:32 复核）**：写作后并行的战术卡牌任务于 23:16 向 `000008/config/` 新增了 `character_tactical_defaults.json`（453 B），使上述总量变为 **855,322 B / 30 个**、Player 暂存变为 **1,631,436 B / 32 个**。该文件不属于本任务范围，也不得由本任务删除或修改。目标文件自身仍为 126,382 B、SHA-256 不变，本任务的单文件收益不受影响。因此：**凡涉及总量、文件数与百分比的门槛，一律以 T0 现场测量为基线并只声明"本任务净变化"，不使用本文的绝对数**（见 §5 T0 与 §8）。

当前该文件在五个头字段之外包含 `runtimeResourceCatalog`、`characterResourcePlan`、`audioCueManifest`、`diagnostics`。这些是完整编译结果的一部分。本任务不推断它们与角色级独立资源文件完全相同，也不把角色级 plan 的 hash 当作动画 plan 的 hash。

### 2.2 真实生成与消费链

| 位置 | 当前职责 | 本次处理 |
| --- | --- | --- |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AnimationAuthoring/AnimationAuthoringCompiler.cs`：`AnimationResourcePlanDocument`、`Compile`、`BuildPlanHashText` | 生成完整 v1 对象，基于动画资源 ID/hash、分组 resourceKey、audio cue ID 计算语义 `PlanHash` | 完整对象与哈希算法保持 |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs`：`CreateUnityImportAdditionalWrites`，约 599、670 行 | 将完整对象直接序列化到生成包的 `config/animation_resource_plan.json` | 改为独立 v2 投影的唯一 writer |
| `.../Cli/AnimationPackageCommands.cs`：`WriteCompileOutputs` | 显式 `animation compile --out` 输出完整动画结果及独立资源产物 | 保持完整 v1，不改为精简版 |
| `.../Cli/EditorServer.Animation.cs`：`CompileAnimationPackage` 的响应 | 向制作工具提供完整编译对象 | 保持原 API 数据 |
| `.../Cli/CharacterPackageCommands.ExportLayout.cs`：`WriteCharacterPackageDiagnostics` | 保存同次导入的完整快照 | 保持嵌套完整 v1；不能被投影修改污染 |
| `Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterImportedPackageJson.cs`：`LoadAnimationResourcePlan` | 只读取五个头字段，当前仅接受 v1 format | 增加明确 v2 分支，同时保留旧 v1 读取 |
| `.../Character.RuntimeSpawn/CharacterRuntimeSpawnContracts.cs`：`CharacterAnimationResourcePlan` | 已经只有 Format、SchemaVersion、PackageId、StableId、PlanHash | 不改公共构造签名，不添加大对象 |
| `.../Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.PrefabComposition.cs`，约 2397–2409 行 | 给 Bootstrap 写文件路径和 `_animationResourcePlanContentHash` | 经正常导入重建后写入新文件哈希，不手改 Prefab |
| `.../Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.cs`：`ComputePrefabConfigAggregateHash` | 已把 `config/` 下 JSON 内容纳入 Prefab 输入缓存 | 用测试确认 v1→v2 会使缓存失效，优先复用现有机制 |
| `.../Character.RuntimeSpawn.Unity/CharacterRuntimeResourceBootstrap.cs` | 暴露计划路径/内容哈希，组合时加载正式包 | 保持既有入口；不重建完整 authoring 对象 |
| `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPackageTestCommand.cs` | 暂存已有 JSON/bytes，排除六项废弃/诊断路径 | 该文件必须继续复制，不能因变小而改成可选或排除项 |

“只读五个字段”不等于该文件可删：`CharacterCompiledAnimationArtifacts.HasRequiredArtifacts` 仍依赖计划对象存在，Prefab 也保存了它的路径与内容哈希。

## 3. 冻结的输出与版本合同

### 3.1 版本决策

采用新的运行用 wire version，避免在相同版本下把一个完整编译文档静默改成另一种形态：

| 表面 | format / version | 内容 |
| --- | --- | --- |
| 完整 `AnimationResourcePlanDocument`，包括制作 API、显式 compile 输出、完整诊断快照 | **继续** `mx.animationResourcePlan.v1` / `1.0` | 原有完整对象 |
| 正式角色包 `config/animation_resource_plan.json` | **新增** `mx.animationResourcePlan.v2` / `2.0` | 仅五个字段 |
| `compile_summary.json` | format/schema 仍 v1；`exportLayoutVersion` **3** | 用于说明新导出布局，不变成运行门禁 |

不修改 `AnimationAuthoringCompileFormats.AnimationResourcePlan` 原常量的取值，否则会连带改变完整编译对象。新增导出格式常量及独立 DTO。

源角色 schema、全局动画/武器 schema、CharacterAction ID、资源键、已有 compile summary schema 和 import report schema 均不随此任务升级。

### 3.2 五字段合同

| 字段 | 新文件要求 | 来源 |
| --- | --- | --- |
| `format` | 字符串，固定 `mx.animationResourcePlan.v2` | 投影格式常量 |
| `schemaVersion` | 字符串，固定 `2.0` | 投影版本常量 |
| `packageId` | 非空字符串 | 完整动画计划的 PackageId，原样复制 |
| `stableId` | 非空字符串 | 完整动画计划的 StableId，原样复制 |
| `planHash` | `sha256:` 加 64 位小写十六进制 | 完整动画计划的 PlanHash，原样复制，不重新计算 |

`000008` 的 `packageId` 是 **`animation.000008`**，`stableId` 是 **`charpkg.000008.animation`**。不能改成角色包的 `000008` / `charpkg.000008`，也不能对旧语义包强制六位数字校验。

实际样本的预期正文：

```json
{
  "format": "mx.animationResourcePlan.v2",
  "schemaVersion": "2.0",
  "packageId": "animation.000008",
  "stableId": "charpkg.000008.animation",
  "planHash": "sha256:296e3ec5508cd9173e56876c73a8789ba44288ee878dc8f263120daec1070b2a"
}
```

生产输出须恰好五个键；不包含上述四个大字段，不写 `null`/空对象占位，不添加计数、摘要、诊断、源路径或指向 authoring 数据的旁路字段。

本文件保持缩进以便审阅；UTF-8 无 BOM、LF、固定上述属性顺序、末尾一个换行。序列化规则只作用于该投影，不能修改整个 CLI 的公共 serializer options 来压缩其它产物。`000008` 正文验收上限 **512 B**；不为达标截断身份或 hash。其它包也必须固定五键、随 Clip 数量增长不扩大正文结构，不额外引入未经既有源合同支持的全局 ID 长度限制。

### 3.3 读取与兼容矩阵

| 输入 | 新 Reader 行为 |
| --- | --- |
| 原有完整 v1 | 保持当前解析语义 |
| 既有测试/旧工具产生的仅五头字段 v1 | 保持原兼容行为；不要求补齐嵌套对象 |
| 合法 v2 | 严格读取五字段，构造既有 `CharacterAnimationResourcePlan` |
| v2 缺字段、字段为非字符串、空身份、非法 hash、schemaVersion 非 2.0 | 通过现有异常/诊断路径拒绝，不静默创建空计划 |
| v2 含任何额外顶层字段，包括被裁掉的四个字段 | 拒绝；生产端同样测试精确键集合 |
| 未知 format 或未知 v2 后续版本 | 明确拒绝，不按字符串包含 v2 或数值范围猜版本 |
| 缺文件 | 沿用当前加载与诊断合同，不把缺失解释为优化成功 |

v2 的严格校验只加在 v2 分支，不能顺手收紧全部 v1 fixture。例如当前 v1 测试中有 `sha256:animation-plan` 占位值，不能将新校验施加到它并删除兼容测试。

**前向兼容成本（已接受的取舍）**：因为 v2 拒绝任何额外顶层字段，**未来任何新增字段都必须升到 v3**，不能做加法式扩展。这是为了杜绝"同版本静默改变形态"而有意接受的代价；实施时不要为将来方便而放宽为"忽略未知字段"。

加载集成继续使用现有独立动画 Set、Clip Registry、角色 runtime catalog 和 character resource plan；不得通过读取 Authoring 目录或临时诊断目录来补回被裁字段。

在 `LoadCompiledAnimationArtifacts` 的 v2 集成分支，计划的 PackageId/StableId 必须分别与同包 AnimationSetDefinition 的 PackageId/StableId 一致；不一致沿现有加载失败路径拒绝，不自动替换身份。比较的是两个动画产物，不是直接拿 `animation.000008` 与角色包 `000008` 比较。此跨产物检查仅作用于 v2，保留旧 v1 的兼容边界。

## 4. 关键执行细节

### 4.1 用独立投影，保持完整对象不变

新增 CLI 内部 `CharacterAnimationRuntimeResourcePlanDocument` 和投影/序列化 helper（建议独立文件 `CharacterAnimationRuntimeResourcePlan.cs`）。这些是计划命名；实现时按现有内部命名约定落地。

helper 只做：验证完整 v1 输入的必要元数据 → 创建新的五字段 DTO → 序列化。不进行资源推导，不重新遍历 Clip，不计算另一套 planHash。

严禁以下实现：

- 给原 `AnimationResourcePlanDocument` 删除字段、添加会影响所有输出的 JsonIgnore，或改变原 format 常量。
- 将原对象的 RuntimeResourceCatalog/CharacterResourcePlan/AudioCueManifest/Diagnostics 置 null 后再序列化。
- 先序列化大对象，再用正则/文本裁掉 JSON 字段。
- 通过修改全局 JsonSerializerOptions 影响完整快照或其它导出文件。

在 `CreateUnityImportAdditionalWrites` 中替换**该文件的**序列化表达式；保留原 AddContentWrite 的 kind、SourcePath 和目标路径。确保投影/版本校验在 `CharacterUnityImportBridge.Execute` 开始写任何项目文件之前完成。非法输入使用稳定诊断（建议 `CHARPKG_ANIMATION_RUNTIME_PLAN_INVALID`）返回非成功，目标旧文件和 meta 原样保留。

验证投影前后完整对象深度相同，并且随后产生的制作 API 与显式诊断仍包含完整嵌套数据。测试顺序必须包含“先生成精简文件，再导出完整诊断”，避免只证明不受污染的反向顺序。

### 4.2 更新摘要的布局标识

在 `CharacterPackageCompileSummaryBuilder` 新增独立布局常量，例如 `LayoutVersionAnimationRuntimePlan = 3`；新角色导入传入该常量。保留旧 A/B 常量用于历史 fixture，不重命名旧版本的含义。

摘要的 `artifactRefs` 仍引用同一计划路径，不能复制计划正文。Runtime/Readiness 不得以摘要缺失、布局 2/3 或未知布局决定角色是否可加载；文件 format 才是 Reader 的判据。

**顺带修正既有不一致（必需）**：`CharacterPackageCompileSummary.ExportLayoutVersion` 当前的属性默认值是 `LayoutVersionA`(1)，而唯一调用点 `CharacterPackageCommands.cs` 传的是 `LayoutVersionB`(2)。加入布局 3 后，这个默认值会成为一个"任何新构造路径静默得到错误布局"的入口。实施时把该属性改为**无默认值（必填）**，或默认指向当前最新布局常量；不得保留指向历史布局的默认值。

**既有断言冲突（必需）**：`Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.ExportLayout.cs` 第 56 行硬编码断言 `exportLayoutVersion == 2`。布局升到 3 后该断言必然失败，而 §7.2 又要求 `character-export-layout` 作为回归入口通过。处理方式：把该断言放宽为 `>= 2`（后续布局升级不再破测），由本任务新增的 `character-animation-runtime-plan` 套件断言 `== 3`。不允许通过删除该断言或降低断言强度来消除失败。

### 4.3 三类哈希必须分开

| 哈希/状态 | 同一源输入精简前后 | 正确处理 |
| --- | --- | --- |
| 动画语义 `planHash` | **相同** | 原样保留 Compiler 的 BuildPlanHashText 结果 |
| 五项 compiler hash：source/config/geometry/resource mapping/write plan | 预期**相同** | 此输出位于 CLI AdditionalWrites，既有 compiler plan 未变；不为本任务重定义 WritePlanHash |
| 该文件的字节 ContentHash | **不同** | 对最终 v2 正文计算，报告的 animationResourcePlan operation 使用新值 |
| Prefab config aggregate / build input hash | **不同** | 现有 config 内容汇总应自动使缓存失效，必须有回归证明 |
| Bootstrap `_animationResourcePlanContentHash` | **不同** | 由正常 Prefab 生成读取最终文件，主 Prefab 与 Runtime 镜像均更新 |
| receipt 的其它聚合 hash | 由现有公式决定 | 不强制某个值必须变化；必须重新通过正式验证生成当前 receipt |

不要用 `SHA256(五字段 JSON)` 覆盖 `planHash`。该值仍标识完整编译期动画资源计划；Runtime 不能从五字段正文独立重建这个语义 hash，本任务也不新增这种能力。

同样不能把 `config/character_resource_plan.json` 的 planHash 或角色级 runtime catalog 的 hash 填入这里：角色级与动画级计划不是同一集合。

目前 `ComputePrefabConfigAggregateHash` 已遍历 config JSON（CRLF 归一化后计算），而 Bootstrap 使用现有文件内容哈希函数。复用各自算法，不把它们混成一个值，不手工更改序列化 Prefab 字段。

### 4.4 利用现有缓存失效，不全量清缓存

先补测试确认：仅把动画计划从完整 v1 替换为等语义 v2，compiler hashes 和 planHash 不变，但 `ComputePrefabConfigAggregateHash` 与最终 Prefab build input hash 改变，旧 Prefab 缓存不能命中。

如当前缓存实现已经满足，不改缓存代码、不为了保险删除 Library、不强制所有角色重烘焙。若测试暴露缺口，只补该文件的依赖或缓存键。不要用升级 BuilderVersion 或无条件 forceRebuild 掩盖遗漏的内容依赖。

### 4.5 保持运行资源、诊断和打包边界

- 正式文件名、路径和 `.meta` GUID 不变；不新增大文件副本到 package_cache、generated 或 runtime_resources。
- `character compile --out` 中完整 compile result 不受影响。
- `animation compile --out` 的 `animation_resource_plan.json` 仍为完整 v1；它属于显式编译导出，不是角色包默认运行导出。
- `character import-unity --diagnostics-out` 的完整动画快照仍包含 v1 大对象，阶段标识保持真实；不把小文件称为完整计划快照。
- Player 暂存继续复制 v2 文件原字节；不能新增过滤项，也不能仅在复制阶段裁字段，否则 Prefab 保存的内容哈希会失配。
- 不更改资源加载分组、预热集合、音频 cue、Clip registry、Animation Set、骨骼或 Trace/Hurtbox/Socket 数据。

## 5. 实施步骤与迁移顺序

### T0：固定可比较基线

保存当前 HEAD、dirty 状态及目标文件原始字节；将证据写入本地 `Temp/CharacterAnimationRuntimePlan/`。

必须记录：作者源/公共库/身份目录 hash、五项 compiler hash、完整动画语义 planHash、目标计划字节/hash/meta GUID、全部 config 文件清单、主/Runtime Prefab meta GUID 与 Bootstrap 内容 hash、receipt、烘焙载荷 hash，以及 Player 暂存清单/字节数。

**并发指纹（必需）**：额外记录 `Assets/MxFrameworkGenerated/CharacterPackages/<pkg>/config/` 的**目录指纹**——按 ordinal 排序的「相对路径 + 文件字节数 + 内容 hash」清单，以及整个生成包的非 meta 文件计数。原因：本文写作后已发生一次并发写入（§2.1 漂移说明），而 `ComputePrefabConfigAggregateHash` 遍历整个 `config/` 目录，任何外部新增文件都会影响缓存失效归因、文件数门槛与"第二次相同输入"的可比性。T4 迁移前后必须各比对一次该指纹：

- 指纹一致 → 差异可归因于本任务；
- 出现本任务之外的**新增/删除/内容变化** → 作为冲突记录并请交接人裁决，不吸收进本任务差异清单，也不删除或修改对方文件。

用相同源码、相同依赖和相同工具链建立旧输出基线；如工作区有其它未完成任务，使用隔离 fixture 保持输入固定。禁止拿前一轮记录的 854,869 B 与任意不同源数据的新包比较后声称全部差异来自本任务。

### T1：先交付读取兼容

实现 v2 分支与 v1 回归；先在 Unity 中完成编译和 Reader 聚焦测试，再让新 producer 写正式包。

不更改已有 Runtime DTO 签名。合法 v1/v2 的 PackageId、StableId、PlanHash 和后续动画/Spawn 行为等价；Format/SchemaVersion 不同是预期，不以包含这两项的全对象相等作为验收。

### T2：增加投影与导出布局 3

实现第 4.1～4.2 节。追加 noEngine 聚焦入口 `character-animation-runtime-plan`（新增，须在 tests Program 注册），复用现有 Authoring 测试程序和临时项目设施，不新增 asmdef 或独立测试工程。

完整动画 Compiler 的资源计划计算与旧输出保持原状；这是导出形态优化，不是重新实现编译器。

### T3：验证缓存、Prefab、诊断与实际暂存

使用隔离已导入角色包，仅替换计划的导出形态，验证缓存失效、正常 Prefab 更新、原有 GUID 保留及 Bootstrap hash 与磁盘一致。不能只断言文件变小或当前 Reader “不报错”。

对同次导入启用诊断输出，检查完整快照没有丢失嵌套字段。用生产暂存方法复制到专用测试根，再从暂存目录加载 v2，核对计划原字节、动画资源与 Spawn Resolve。

默认角色暂存仍是 `000001`，参数化暂存 `000008` 必须到自己的目录；沿用上一轮已有隔离 helper，不在 finally 删除正式 StreamingAssets 根。

### T4：正式迁移 000008

1. 确认 Unity 已运行新 Reader、处于 Edit Mode 且无编译错误；源码与测试已通过，避免新文件配旧程序集。
2. 通过 CLI 正常 `character import-unity --dry-run --check-files --check-hashes`，确认没有新增 blocker、冲突及未预期写入。
3. 记录目标相关文件和 meta 的备份，执行正常导入；此任务不先删旧文件，现有 AddContentWrite 按内容 hash 将完整 v1 更新为 v2，保持 `.meta`。
4. 完成 `CharacterPackageImportCommand` 的正式 Unity 同步，更新主/Runtime Prefab 和 Bootstrap 内容 hash，并通过正式 receipt 校验。CLI Ready 不能代替 Unity Ready。
5. 不清空并发任务状态；共享 Resource Mapping 继续走现有跨进程锁、expected revision 和原子写入路径。冲突返回失败后读取新状态，不能覆盖对方内容；文件静默几分钟不是互斥证据。
6. 进行第二次相同输入导入，验证该文件与摘要跳过重写、GUID 保留、旧完整正文不复活。

首批只迁移 `000008`。其它包允许继续保留 v1，随后正常重导入自然升级；不批量用文本脚本改 JSON。未经此任务点名的旧快照/孤立资产清理规则不扩大。

### T5：失败、兼容与回退

- 新 Reader 同时接受 v1/v2，可支持包逐个升级。旧 Reader 只接受 v1，不能在旧 Player/旧 Editor 上分发 v2 文件；发布时 Reader 与新内容成套更新。
- 投影验证失败必须发生在写盘前；dry-run 不修改计划、摘要、Prefab、receipt、meta 或作者源。
- CLI 写入成功而 Unity 同步失败时，记录“文件更新成功 / Unity 验收未完成”，保留具体诊断；不得手工设置 receipt.ready 或只复制旧 receipt 关闭问题。
- 备份精确覆盖本次计划文件及其 meta、summary、import report、主/Runtime Prefab 及 meta、receipt，以及实际发生变化的相关文件；缓存证据独立保存，不宣称现有逐文件导入为整包事务。
- 恢复中断迁移时，优先用匹配版本的正常生成/Unity 同步重试。若需要恢复备份，逐文件核对当前 hash，出现本次之后的外部修改先保留冲突，不用整仓 reset。
- 回退 producer 到旧输出而保留新 Reader 是可读的 v1；仍须正常重建 Prefab hash/receipt。若回退 Reader，也必须先恢复完整 v1 内容及对应已验证 Prefab，不能只回退 C#。
- 不添加 Runtime 读 Authoring、根据文件缺失恢复大对象、或写回修复坏数据的兼容旁路。

## 6. 代码落点与预期改动

| 位置 | 必须做 | 不应做 |
| --- | --- | --- |
| CLI 新增 `CharacterAnimationRuntimeResourcePlan.cs` | 内部五字段 DTO、常量、投影和确定性序列化 | 新公共 SDK、第二个资源 resolver |
| `CharacterPackageCommands.CreateUnityImportAdditionalWrites` | 单独替换计划序列化，写入前完成投影检查 | 改其它配置字段或重写完整对象 |
| `CharacterPackageCompileSummary.cs` 与调用点 | 增加布局 3，保留已有引用路径 | 改 summary format 或将它变成必需 Runtime 输入 |
| `CharacterImportedPackageJson.LoadAnimationResourcePlan` | v2 严格分支，保留 v1 分支 | 修改全局 ReadString/全部 JSON 的解析策略 |
| `CharacterRuntimeSpawnContracts.CharacterAnimationResourcePlan` | 原则上无需修改 | 添加三套资源大对象或改变构造签名 |
| Prefab builder/cache/Bootstrap | 先用测试确认现有缓存、内容 hash 更新有效；仅修证实的缺口 | 全量清缓存、手改 YAML、删 meta 后重建 |
| `CharacterPackageTests.ExportLayout.cs`、新增同类 partial 测试文件、tests Program | noEngine 投影、未污染完整诊断、导入和 hash 回归 | 只测文件名或源码字符串 |
| `CharacterRuntimeSpawnFirstSliceTests.cs` | v1/v2、坏格式、集成加载与 Spawn 等价测试 | 删除已有 v1 fixture 来消除失败 |
| `CharacterImportedPackagePrefabBuilderTests.cs` / `CharacterRuntimeResourceBootstrapTests.cs` | 格式变化使缓存失效、两份 Prefab hash 更新、GUID 保留 | 仅调用 setter 后断言 setter 的值 |
| `CharacterTestProfilerGcRemediationTests.cs` | 按已有隔离暂存入口验证真实 v2 文件复制和加载 | 修改 Player 过滤清单以排除计划 |

CLI 和 Authoring tests 路径前缀为 `Tools/MxFramework.Authoring/`；Unity 测试前缀为 `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/`，按当前 `CharacterRuntimeSpawn/`、`CharacterImport/`、`Game/CharacterTest/` 子目录落地。不要将仓库回归移入框架 package 的最小测试目录。

## 7. 验证矩阵与命令

### 7.1 聚焦验证矩阵

| 场景 | 必须证明 |
| --- | --- |
| 正常五字段投影 | v2 / 2.0，身份与语义 hash 原样复制，精确五键，无四个大字段 |
| 完整对象不变 | 投影前后完整 v1 深度相等；投影之后的制作 API/diagnostics 仍含原嵌套数据 |
| 确定性 | 同输入输出字节相同，固定字段顺序和换行，无 BOM/时间戳/机器路径 |
| 来源变化 | 修改一个确实参与 BuildPlanHashText 的资源 hash 或预热 resourceKey，原 Compiler 的 planHash 与新文件同步变化；只改缩进不改变语义 hash |
| 五项 compiler hash | 同输入、仅改投影时保持不变；不能靠改 source hash 强行使缓存失效 |
| 缺失/非法投影元数据 | 在桥接写盘前拒绝，旧文件与 meta 不变，非成功退出 |
| v1 兼容 | 完整 v1 和既有头字段 v1 fixture 均维持现有结果 |
| v2 负样本 | 缺字段、非字符串、空身份、非法 hash、错版本、额外字段均拒绝 |
| 跨包计划 | 独立解析合法但身份与同包 AnimationSetDefinition 不一致的 v2，在集成加载时拒绝；不影响旧 v1 兼容 |
| 未知版本 | 不能落到 v1 宽松解析或返回空计划后继续 |
| 导入路径 | 默认新输出 v2；显式 animation compile/完整快照仍为完整 v1；只有一个正式计划 writer |
| dry-run 与二次导入 | dry-run 目标原样；第二次相同导入计划/摘要不重写，废弃文件不复活 |
| 缓存失效 | planHash 与 compiler hashes 相同，config aggregate/build input hash 因正文变化而不同；不会复用带旧 Bootstrap 内容 hash 的 Prefab |
| Prefab 与 GUID | 主/Runtime 两个 Bootstrap 的计划内容 hash 与最终文件一致；计划和保留 Prefab meta GUID 均不变 |
| 配置/行为不变 | 动画集合、Clip registry、加载分组、资源键、Spawn Resolve 与编译诊断不因投影改变；v1/v2 返回的身份/planHash 等价 |
| 烘焙隔离 | 输入相同的 Trace/Hurtbox/Socket 载荷及采样参数不变；验收读取仍成功 |
| 实际暂存 | 文件继续存在，目标字节与正式 v2 相同，暂存包可加载；六项旧排除规则保持 |
| 坏文件处理 | 对损坏 v2 能取得原错误/diagnostic，不以当前有效的独立 catalog 静默替代 |
| 读取失败的后果 | 两种失败模式必须分别断言：**缺文件** → `LoadOptionalAnimationArtifact` 追加 `MissingCompiledAnimationArtifact` 诊断并返回 null，`HasRequiredArtifacts == false`；**内容非法/身份不一致** → `CharacterImportedPackageJsonException` 从 `LoadFromDirectory` 抛出（现有 `LoadOptionalAnimationArtifact` 不捕获解析异常），整包加载失败，不产生半成品对象。两者都不得静默创建空计划；并确认没有调用方仅凭 `HasRuntimeAnimationContracts` 在计划缺失时继续（既有先例 `CharacterRuntimeAnimationProfileResolver.cs` 同时检查 document 与 `HasRuntimeAnimationContracts`） |
| 摘要布局必填 | `CharacterPackageCompileSummary.ExportLayoutVersion` 无指向历史布局的默认值；任何构造路径都显式给出当前布局 |

### 7.2 Authoring 验证

按 `Tools/MxFramework.Authoring/README.md` 构建最新独立工具后运行：

```powershell
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-animation-runtime-plan
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-export-layout
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-unity-import-bridge
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-unity-import-receipt
```

第一条是本任务新增入口，必须注册后再使用；其它入口已存在。构建 Authoring 工程不等于 Unity C# 编译通过，不追加无关 Unity csproj 构建或全仓测试。

### 7.3 Unity 验证

1. 优先 `execute_custom_tool(tool_name="validate_unity_scripts", parameters={})`；证据必须同时包含 refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count = 0。
2. 工具因 domain reload 断开时，读取已启动作业的状态，不能立即再启动重复作业。仅工具不可用或明确失败才回退分步；不要为此枚举 MCP resources。
3. 用测试类实际的**完整命名空间 + 类名 + 方法名**运行 Reader、Prefab/cache 和暂存的聚焦 EditMode 用例。`total=0` 是没有取得执行证据，不是通过。
4. 记录 Test Runner 的 total/passed/failed/skipped 和测试名，保存 XML。直接反射调用只能记录它实际覆盖的层，不代替缓存重建或完整文件复制测试。
5. 验证新文件从 AssetDatabase 与目录读取两条路径都有效；正式 `000008` 导入必须使用新 Reader 与当前脚本，不能依赖尚未重载的旧程序集。

不要求额外做完整 Player 构建或战斗手测来证明本次纯导出投影；若交付声称这两项通过，必须另提供对应证据。Player 暂存实际复制和加载属于本任务必需项。

## 8. 正式 000008 的完成标准

| 项目 | 门槛 |
| --- | --- |
| 目标文件 | v2 五字段、≤ 512 B；当前样本静态预期 238 B |
| 文件数量 | T0 现场基线计数 **+0**（本任务不新增、不删除文件）；不新增完整计划副本或侧车。不使用本文 §2.1 的绝对数 29，因为该数已被并发改动漂移 |
| 语义标识 | packageId/stableId/planHash 与同输入完整 v1 一致 |
| 完整诊断 | 完整 v1 仍有嵌套 catalog、resource plan、audio manifest 和 diagnostics |
| 运行数据 | 111 个 Clip、原有动画集合/加载分组与资源引用保持；零缺失资源 |
| Unity | 编译门禁通过；主/Runtime Prefab Bootstrap 指向同一新计划并保存正确内容 hash；原 GUID 保留；正式 receipt 验证通过 |
| 幂等 | 第二次导入计划与摘要字节、mtime、meta 不变 |
| 暂存 | 计划逐字节复制，实际暂存包能加载和 Spawn Resolve，31 个 JSON/bytes 的当前集合不因本任务新增/删减文件 |
| 隔离 | 作者权威源、其它角色和共享 profile 无本任务引起的未解释变化 |

在其它文件不变的假设下（基线取 §2.1 的 22:14 测量值；**实际交付须用 T0 现场基线重算**）：

- 全部 JSON：`854,869 − 126,382 + 238 = 728,725 B`，约减少 14.76%。
- Player 暂存：`1,630,983 − 126,382 + 238 = 1,504,839 B`，约减少 7.73%。

若 T0 基线因并发改动而不同于上表（§2.1 漂移说明），按同一公式代入 T0 实测值重算，并在交付记录中同时给出两套数字与差异来源；不得为了对齐本文的百分比而回退或删除并发任务的文件。

这是静态预算，不以它代替实测。摘要布局 3、导入报告等允许有必要的小量变化，必须按逐文件清单解释；不得删报告、减少 Clip 或把其它清理收益并入本任务。没有 CPU/分配/启动时长基线，不宣称性能加速。

## 9. 文档同步与交付清单

同步以下文档的命中章节，不把 Design 目标提前写成已交付事实：

- `Docs/Interfaces/CharacterApplication.md`：运行计划 v2 的五字段、v1/v2 兼容及文件必需性。
- `Docs/Interfaces/Animation.md`：完整编译计划仍 v1，运行投影为 v2，语义 planHash 的来源与区别。
- `Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md`：完整制作数据与正式运行投影的消费边界。
- `Tools/MxFramework.Authoring/README.md`：导出布局 3；不同命令下同名文件的完整/精简含义；正常导入、旧 Reader 限制和回退顺序。
- `Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md` §4.2 第 7 项：正式文件 format、真实 producer/consumer；避免恢复上一轮已纠正的发行剥离表述。
- 本文：追加实施 commit、具体用例结果、前后文件清单和实际未验证项。上一轮方案 §12.10 保持历史测量，不回填本任务后的新数字。

交付前逐项确认：

- [ ] 新 Reader 接受旧 v1 与新 v2，负样本拒绝有效。
- [ ] 正式导出只用独立 DTO，完整内存对象/API/诊断未被裁剪。
- [ ] 同输入语义 hash 不变、文件 hash 改变，缓存和两份 Prefab 绑定已验证。
- [ ] 000008 正常迁移完成，正式 receipt 已重验，二次导入与 meta GUID 对照通过。
- [ ] 真实暂存的字节、文件数和加载结果已记录。
- [ ] 原动画/资源/烘焙数据未被顺带修改，差异逐项可解释。
- [ ] 本地保存基线、对照清单、测试 XML、导入报告和恢复材料，未提交 Temp/Library。
- [ ] 当前文档与命令说明同步，明确未执行的 Player/完整战斗范围。

建议分三个可评审变更：① Reader 兼容与测试；② 独立投影、布局 3、诊断隔离及缓存/暂存回归；③ 000008 正式再生成和验证记录。提交/推送服从交接人的明确要求，不在收尾时把其它任务工作一并打包。

## 10. 可直接交给实施人的任务说明

按本文实现动画资源计划精简，只处理第一项优化。保留完整动画编译对象及完整 v1 诊断，在角色默认导入中用独立五字段 v2 投影替代原大文件；新 Reader 兼容 v1/v2，严格校验 v2，导出布局标为 3。原动画语义 planHash 和同输入 compiler hashes 保持，最终文件内容 hash、Prefab 缓存和 Bootstrap 绑定必须更新并真实验证。先通过聚焦测试，再经正常导入迁移 000008，复核两次导入的字节/GUID、实际 Player 暂存与成功回执。不要压缩其它 JSON、移动 GLB、减少资源引用或清空整个缓存目录。最终交付代码、文档、正式产物及逐文件证据，明确未验证范围。

---

## 11. 实施记录（2026-09-19）

### 11.1 已实现

| 步骤 | 落点 | 内容 |
| :--- | :--- | :--- |
| T1 | `Packages/…/Character.RuntimeSpawn/CharacterImportedPackageJson.cs` | `LoadAnimationResourcePlan` 拆为 v1/v2 双分支；v2 严格校验（精确五键、`schemaVersion` 2.0、身份非空且为字符串、`planHash` 为 `sha256:`+64 位小写十六进制、拒绝任何额外顶层字段）；新增 `RequireAnimationResourcePlanIdentity`，在 `LoadCompiledAnimationArtifacts` 中对 v2 比对同包 `AnimationSetDefinition` 的 `packageId`/`stableId`，不一致即抛 `CharacterImportedPackageJsonException` |
| T2 | 新增 `Tools/…/Cli/CharacterAnimationRuntimeResourcePlan.cs` | 内部五字段 DTO + `TryProject` 投影/确定性序列化 + ≤512 B 有界校验 + 稳定诊断 `CHARPKG_ANIMATION_RUNTIME_PLAN_INVALID` |
| T2 | `CharacterPackageCommands.CreateUnityImportAdditionalWrites` | 只替换该文件的序列化表达式（kind/SourcePath/目标路径不变），投影失败即不注册该写入 |
| T2 | `CharacterPackageCommands.ImportUnity` | 拆出 `additionalWrites` 变量，在 `CharacterUnityImportBridge.Execute` **之前**检查投影诊断；失败打印稳定诊断并返回 `ExitValidationBlocked`，不写任何项目文件 |
| T2 | `CharacterPackageCompileSummary.cs` | 新增 `LayoutVersionAnimationRuntimePlan = 3` 并在调用点传入；`ExportLayoutVersion` 去掉指向历史布局的默认值（改为必填） |
| 测试 | 新增 `CharacterPackageTests.AnimationRuntimePlan.cs` | 套件 `character-animation-runtime-plan`（4 例），已注册到 `Program.cs` 与 `RunAll()` |
| 测试 | `CharacterPackageTests.ExportLayout.cs` | `exportLayoutVersion == 2` 放宽为 `>= 2`，由新套件断言 `== 3` |

### 11.2 验证证据

| 项目 | 结果 |
| :--- | :--- |
| Authoring 构建 | 0 warning / 0 error（输出重定向到 `bin_local/`，见 11.3） |
| 全量 Authoring 回归 | `MxFramework.Authoring.Tests passed.`（exit=0，4m36s，含新套件） |
| 新套件 | `character-animation-runtime-plan` 通过：五键投影、完整 v1 未污染（诊断快照仍 9 键且嵌套非空）、二次导入字节一致、6 个负样本全部拒绝、摘要布局 3 |
| Unity 编译门禁 | `validate_unity_scripts` 通过（refresh completed、compilation finished、domain reload observed、Editor ready、Console 0 error） |
| Unity v2 Reader（真实 Editor 内直接调用） | 合法 v2 解析成功且身份正确；v1 宽松兼容保留（含 `sha256:animation-plan` 占位值）；6 个负样本全部按预期拒绝并给出正确消息 |
| 000008 dry-run | `status=Ready`、`canWrite=True`、`updated=3 skipped=31 conflicts=0 errors=0`、无 issue |
| 000008 正式迁移 | `status=Ready`、`updated=3`、`exportCleanup=deleted:0 missing:5`、`compileSummary` 已重写；无 issue |
| 迁移后目标文件 | `mx.animationResourcePlan.v2`、五键固定顺序、**244 B ≤ 512 B**、无 BOM、末尾恰好一个换行；`planHash` 与迁移前完整 v1 的语义哈希**完全一致**（`sha256:296e3ec5…`） |
| `.meta` GUID | `config/animation_resource_plan.json.meta` 保持 `3d5adf58d2a4fda46bcdea76f24d3c9a` |
| 摘要 | `exportLayoutVersion = 3` |
| Prefab 重建 | 主 Prefab 与 Runtime 镜像均于 00:15:15 重建；GUID 分别保持 `887aa36ccac525347b1fe357b39e5ec5` 与 `e8d56c5b69eb1e04692639aa0c5ef5b1` |

### 11.3 实施中发现的两处合同偏差（需要交接人裁决）

**偏差 1：换行风格实际为 CRLF，不是 §3.2 要求的 LF。**
迁移后正文 244 B，比 §3.2 的静态试算 238 B 多 6 B——差值恰好等于 6 个内部换行从 LF 变为 CRLF。根因是两项既有行为叠加：CLI 目标框架为 `net9.0`，System.Text.Json 的缩进自 .NET 9 起使用 `Environment.NewLine`（Windows 即 CRLF）；而 `EnsureTrailingNewline` 追加的是裸 `"\n"`，因此末尾换行仍是 LF（实测 6 个 CRLF + 1 个裸 LF）。**这是全仓既有行为，不是本任务引入**：`config/character_config_patch.json`（1420 CRLF）、`geometry_binding.json`（1450）、`animation_set_definition.json`（3933）、`character_resource_plan.json`（977）、`compile_summary.json`（85）全部为 CRLF。`ComputePrefabConfigAggregateHash` 也显式做 `Replace("\r\n","\n")` 归一化，说明下游已按 CRLF 现实设计。
处置建议：把 §3.2 的"LF"改为"与其它正式 config 产物一致的换行约定（当前 Windows + net9.0 下为 CRLF，无 BOM，末尾恰好一个换行）"，并把实测值 244 B 写入 §8。若确需 LF，应作为独立任务统一处理全部 JSON 产物，否则单独改本文件会让它与同目录产物不一致，并连带改变所有 ContentHash。

**偏差 2：`_animationResourcePlanContentHash` 不在生成的 Prefab 里。**
§2.2 说 `PrefabComposition.cs` "给 Bootstrap 写文件路径和 `_animationResourcePlanContentHash`"——这一点准确；但 §8 的门槛写成"主/Runtime Prefab Bootstrap 指向同一新计划并保存正确内容 hash"，暗示该值保存在 Prefab 中。实测：生成的 `prefabs/000008_character_preview.prefab` 含 `CharacterRuntimePrefabDescriptor`，**不含 `CharacterRuntimeResourceBootstrap`**，也没有 `animationResourcePlan` 字样；`PrefabComposition.cs` 中该字段是写在 `loader.AddComponent<CharacterRuntimeResourceBootstrap>()` 得到的 Bootstrap 上（`new SerializedObject(bootstrap)`），而该 Bootstrap 出现在场景（`Assets/Scenes/MxFramework/CharacterImportedPreview.unity` 等）而非 Prefab。
处置建议：把 §8 的该项改为"确认 Bootstrap 的 `_animationResourcePlanContentHash` 已按新文件内容更新（载体在 loader 对象/场景，不在生成 Prefab 内）"，或明确要求提供该值的实际读取位置；不要用两个 Prefab 文件的内容来验收这一项。

### 11.4 未完成

- 本次未构建 Player 可执行文件、未做完整战斗手测；不计入完成声明。
- 未验证 `ComputePrefabConfigAggregateHash` 的前后差值（缓存失效）作为独立数值证据；本次只确认了 Prefab 被重建且 GUID 保持。§7.1「缓存失效」一行仍需补充。
- 其它角色包仍为 v1，按 §5 T4 由正常重导入自然升级。

### 11.5 补充验证（2026-09-20 00:18）

| 项目 | 证据 |
| :--- | :--- |
| **二次导入幂等（§5 T4.6）** | 第二次相同导入：`status=Ready`、`updated=1 skipped=33`、`exportCleanup=deleted:0 missing:5`、`compileSummary=Skipped`。计划与摘要的 **sha256 前缀、mtime、`.meta` GUID 全部不变**（计划 `03392589c66b09d272e3acd6` / `3d5adf58d2a4fda46bcdea76f24d3c9a`；摘要 `2613fcaf4114bdb719b81a4a` / `e314fa9133d7cb340b96e93d18966c20`）；废弃文件未复活 |
| **缓存失效数值证据（§7.1）** | 在真实 Editor 内调用 `CharacterImportedPackagePrefabBuilder.ComputePrefabConfigAggregateHash`：迁移前（旧完整 v1 正文）`sha256:5a2338899ced0c44dcc99112736709577442b9c8b690d5d739b1cf3199578dc0` → 迁移后 `sha256:5ba31ae3a6373649b1c7f4fc49aeb73da7b6fee1983041f6ac4542f35e6d5828`，**已变化**。证明 §4.4「复用既有缓存失效、不改缓存代码、不清 Library」成立 |
| **真实 Player 暂存（§8 必需项）** | 调用生产方法 `StageGeneratedPlayerContent(源包, 隔离暂存根)`：`stagedJsonCount=29`、`stagedBytesCount=3`、`totalBytes=1,505,778`；计划**逐字节复制**（244 B，byte-equal）；`stagedHasSummary=False`、`stagedHasSnapshot=False`。验证后已删除隔离暂存根 |
| **暂存包加载** | 对暂存副本调用 `CharacterImportedPackageJson.LoadFromDirectory`：`loaded=True`、`packageId=000008`、`hasRequiredArtifacts=True`、`planFormat=mx.animationResourcePlan.v2`、身份 `animation.000008` / `charpkg.000008.animation`（跨产物校验通过）、`planHash=sha256:296e3ec5…`、`diagnosticCount=0` |

注：§8 写的"31 个 JSON/bytes"是 §12.10 时点的值；并发任务加入 `character_tactical_defaults.json` 后当前为 **32 个（29 JSON + 3 bytes）**，与本任务无关（§2.1 漂移说明）。

### 11.6 文档同步（§9）

已完成：`Docs/Interfaces/Animation.md`（新增「角色包动画资源计划的 v1 / v2 双形态」）、`Docs/Interfaces/CharacterApplication.md`（新增「角色包动画资源计划的运行投影（v2）」）、`Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md`（四层边界表的正式派生配置行 + 完整计划属制作数据的说明）、`Tools/MxFramework.Authoring/README.md`（布局 3、同名文件在不同命令下的含义、兼容与回退顺序）、`Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md` §4.2 第 7 项。

### 11.7 未完成（更新）

- 未构建 Player 可执行文件、未做完整战斗手测；不计入完成声明（§7.3 亦不要求）。
- 其它角色包仍为 v1，按 §5 T4 由正常重导入自然升级。
- 未单独验证「暂存包 Spawn Resolve」；本次验证到 `LoadFromDirectory` 成功且 `diagnosticCount=0`。
