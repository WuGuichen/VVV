# Config 接口

> Status: Current
>
> 版本 2026-10-13 | Issue #790
>
> Code-Doc: `Packages/com.vincent.wgameframework/Config/ConfigValidation.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Config/ConfigTableValidation.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Config/ConfigProviderTests.cs`

## 职责

Config 提供配置访问、表结构、校验、多语言 key、AI/Editor 友好 authoring 输出。不绑定 Excel、CSV、Json、Luban 或 ScriptableObject。

## 公开接口

| 接口/类型 | 用途 |
|-----------|------|
| `IConfigData` | 配置数据标记，要求 `Id` |
| `IConfigProvider` | 按类型和 ID 读取配置 |
| `IConfigRegistry` | 多 Provider 组合 |
| `IConfigTable<T>` / `ConfigTable<T>` | 配置表 |
| `ConfigSchema` / `ConfigField` | 表结构和字段元数据 |
| `ConfigReferenceRule` | 运行时类型引用和跨源引用规则 |
| `ConfigStructureKind` | 区分 Table、Graph、Localization、GeneratedRuntime |
| `ConfigEnumDomain` / `ConfigEnumRegistry` | 枚举和 flags 统一注册 |
| `ConfigSourceEntry` / `ConfigSourceIndex` | 配置源索引和跨源引用校验 |
| `IConfigEditorSourceIndexProvider` | Editor 配置源可选实现，用于接入 SourceIndex |
| `LocalizedTextKey` / `ILocalizationProvider` | 多语言 key 和文本提供者 |
| `LocalizationTsvLoader` / `LocalizationTsvLoadResult` | 读取 `key<TAB><locale...>` TSV，填充 `MemoryLocalizationProvider` 并输出 locales、keys 和结构化 issues |
| `ConfigTableValidator` | 表级校验 |
| `IConfigReferenceProvider` / `ConfigValidator` | 收集配置行引用并校验目标存在性 |
| `ConfigAuthoring` | TSV 模板和结构化报告 |
| Buff config factory | Retired in Buff v2; register `GameplayBuffDefinitionRegistry` from Gameplay composition roots instead |
| `ConfigModifierFactory<TConfig>` | 配置驱动 Modifier 创建 |
| `BasicAbilityConfig` | 最小 Ability 配置行，ID 范围 300000-399999 |
| `ConfigAbilityFactory` | 配置驱动 Gameplay Ability 创建 |
| `AbilityGraphConfig` | synthetic Ability Runtime Graph 配置 DTO，包含 nodes、edges 和 typed payload config |
| `AbilityGraphConfigMapper` | 将 Ability Graph config 映射为 Gameplay `AbilityGraphDefinition`，并输出带 source/field path 的 diagnostics |
| `ConfigLayerKind` | 运行时配置层：Base / Patch / Mod / Debug |
| `ConfigPatchEntry<T>` | 单条运行时配置增量操作 |
| `RuntimeConfigPatchMerger` | 合并 Base 行和 Patch/Mod 行，输出运行时表 |
| `ConfigChangeSet` | 描述运行时合并产生的新增、替换、删除和无变化 |
| `RuntimeConfigHotReloadRequest` | 显式运行时 patch reload 请求，包含路径、source name 和可选期望 hash |
| `RuntimeConfigHotReloadResult` | reload 结果，包含 source、source id、内容 hash、耗时、变更表、错误和新 provider |
| `RuntimeConfigPatchHotReloadService` | 读取 JSON patch、复用 patch loader / merger，成功时产出新的只读 registry |
| `RuntimeConfigHotReloadPoller` | 轻量文件变更探测器，只负责生成显式 reload request |
| `AuthoringWorkflow` | 可移植创作流程，描述目标、模式、当前步骤和步骤列表 |
| `AuthoringWorkflowStep` | 单个流程节点，描述输入、输出、校验、Actor、AI 提示和快捷动作 |
| `AuthoringQuickAction` | 抽象快捷动作，由 Unity Editor、外部 Mod Editor 或 CLI 解释执行 |
| `CharacterTestWorldObjectDefinitionAuthoringDocument` | CharacterTest 世界对象定义的可写 Authoring DTO；启动/创作低频分配 |
| `CharacterTestWorldObjectDefinitionJson` | strict JSON、canonical round-trip 与 SHA-256 hash |
| `CharacterTestWorldObjectDefinitionValidator` / `CharacterTestWorldObjectDefinitionCompiler` | 结构化校验并编译现有 gameplay catalog 与 sibling presentation catalog |
| `CharacterTestWorldObjectDefinitionCatalogLoader` | Editor generated / Player mirror 启动加载、hash 复验和 built-in fallback |
| `MxFramework.Gameplay.Triggers.Config` | Phase 19.3A 模块级 noEngine extension；把 Trigger authoring + trusted registries确定性编译为core runtime catalog plan，不让 `MxFramework.Config` 或 Trigger core反向依赖 |
| `GameplayTriggerAuthoringJson` | Trigger Config-owned strict canonical JSON round-trip；结构诊断与 immutable authoring materialization 均为 `AllocByDesign` |
| Buff authoring workflow template | Retired with the old external Buff editor compatibility path |

## 使用约定

- `id <= 0` 非法；`0` 保留为 invalid。
- `GetConfig<T>` 缺失时抛异常；无异常流程用 `TryGetConfig` 或 `GetConfigResult`。
- `ConfigTableValidator` 和 `ConfigValidator.ValidateReferences<T>` 的运行时类型引用通过 `IConfigProvider.ContainsConfig<TTarget>(id)` 点查询；`ConfigRegistry` 继续经当前已注册 provider 的 `TryGetConfig<TTarget>` 分派。不会为每条引用读取目标整表。
- 引用查询委托按 target type 在一次 Validate 内绑定并复用；不跨调用保留 provider、目标行或存在性结果。provider 替换、清空或行数据修改后，下一次校验读取当前内容。原有 required/optional、Severity、行/字段、诊断文本与顺序不变；动态类型绑定异常及 provider 调用的 `TargetInvocationException` 包装保持。
- 配置校验仍为低频 `AllocByDesign`：源行快照、字段反射/装箱、引用收集、报告及单次校验的类型查询绑定仍可能分配；该优化不声明整个校验零分配。`MemoryConfigProvider.GetAllConfigs<T>` 保持按调用返回快照的行为。
- 业务表中的多语言字段保存 `LocalizedTextKey`，具体语言文本由 `ILocalizationProvider` 提供。
- `LocalizationTsvLoader.LoadFromText(string)` 固定要求 header 第一列为 `key`，后续列为 locale id；空 key 输出 error，空文本输出 warning，重复 key/locale 输出 warning 且后值覆盖前值。
- 字段使用枚举或 flags 时只保存 `enumId`，枚举值由 `ConfigEnumRegistry` 统一解释。
- 表格引用 Graph、Localization 或运行时产物时，使用 `ConfigReferenceRule(fieldName, targetSchemaName, targetStructureKind)`。
- `ConfigSourceIndex` 只登记源和 key，不负责解析 TSV/JSON；具体导入器在项目层实现。
- Player runtime catalog 可以由项目层或模块层生成版本化 `.bytes` artifact；JSON 仍作为 authoring/debug/diff/compat 格式保留。二进制 catalog 不由 `MxFramework.Config` 统一解释，具体 importer 继续返回各模块自己的 registry/catalog，并声明启动期 `LowFreqAlloc`。
- Editor 配置源如果实现 `IConfigEditorSourceIndexProvider`，健康报告会自动统计索引源、索引 key，并合并跨源引用问题。
- 具体导入器在项目层实现，最后产出 `ConfigTable<T>`。
- `MxFramework.Config.Runtime` 是桥接层，不让 Buffs/Modifiers/Gameplay 反向依赖 Config。
- Gameplay Trigger config compiler属于`MxFramework.Gameplay.Triggers.Config`而不是通用`MxFramework.Config`：Compiler 本身仍只接收 in-memory typed authoring document；同程序集的 `GameplayTriggerAuthoringJson` 只负责低频 strict canonical Authoring round-trip，不是 Runtime JSON loader，也不提供 active-session hot reload、Unity adapter或domain bridge。
- Ability Graph config 当前是 synthetic runtime DTO，不读取真实 WGame JSON；项目层导入器应先映射到该 DTO，再进入 `AbilityGraphConfigMapper`。
- 运行时动态更新不修改 Authoring Source；应由 Base、Patch、Mod、Debug 等层合并成新的运行时只读表。
- Mod 或热更配置优先表达为 `ConfigPatchEntry<T>`，再由 `RuntimeConfigPatchMerger` 生成 merged view 和 `ConfigChangeSet`。
- Config Runtime hot reload 是显式操作：调用方先生成 `RuntimeConfigHotReloadRequest`，再决定是否把成功的 `RuntimeConfigHotReloadResult.Provider` 切换为当前 provider。
- `RuntimeConfigPatchHotReloadService` 当前只加载 Config.Runtime 已支持的 JSON patch bundle，并复用 `RuntimeConfigPatchJsonLoader`、`RuntimeConfigPatchMerger` 与 `ConfigChangeSet`；它不热重载 Unity 序列化资产。
- reload 失败时返回 `Success == false` 和 `Errors`，不会修改传入的 base provider；调用方不应在失败结果上继续切换运行时配置。
- 创作流程必须是 runtime-agnostic 数据描述，不依赖 Unity API，也不要求完整源码。
- AI 辅助优先读取 `AuthoringWorkflow.CreateStepAiContext(...)` 生成的步骤级上下文，不默认扫描全项目。
- CharacterTest 世界对象定义源使用 `mx.characterTest.worldObjectDefinitions.authoring.v1`；未知字段、重复 numeric/stable ID、非法 typed facet 或坏 `ResourceKey` 必须在编译前拒绝。
- 世界对象 gameplay catalog 继续使用 `CharacterTestWorldObjectDefinitionCatalog`。表现绑定是 sibling projection，不把 Prefab、Unity path 或 `UnityEngine.Object` 写入 gameplay definition。
- Editor 优先读取 generated runtime catalog，并用当前 Authoring source hash 拒绝 stale artifact；Player 优先读取 StreamingAssets mirror。候选缺失、坏 hash 或编译失败时显式返回诊断并回退 built-in catalog。
- 世界对象定义 JSON、hash、validator、compiler 和 loader 都是 `AllocByDesign` / `LowFreqAlloc` 的创作或启动路径，不能在 CharacterTest Tick 中调用。

## 最小示例

见 `Docs/USAGE.md` 的 Config、ConfigAuthoring、Localization TSV、Buff 创作流程、配置驱动 Buff/Modifier 章节。

## 测试入口

`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Config/`

`ConfigProviderTests`、`ConfigTableTests` 和同文件的 `ConfigReferenceValidationTests` 覆盖两个引用入口的重复引用无目标快照、不同目标类型（含 struct）、默认同类型表解析、provider 替换/清空与行增删、required/optional、Severity/HasErrors、精确诊断顺序/内容、null/非法目标类型和 provider 异常包装。

### Issue #790 的 Mac 离线对照

2026-10-13 在 macOS 26.6 / arm64、.NET SDK 9.0.306 / Runtime 9.0.10，使用基线 `1a67c7753c5f5f7ee3a0412d8a1e09ce23981803` 的原样 Config 根目录源码与本次候选分别构建同一独立 Release 探针。两个 validator 的基线源码与提报 commit `e5a10eaef8229ff0627bb69626770e69585f5377` 一致。每种输入含 N 个 owner、N 个 target（ID 1..N），所有 owner 引用 target ID=N；表入口使用 registry，接口入口使用默认 memory provider。构造在测量区间外，两轮均设置 `DOTNET_TieredCompilation=0`，预热 8 次，以下为 15 次样本的中位数。计时/分配使用无 observer 的真实路径，调用计数另用转发 observer 核对，不以 Windows 数字作为 Mac 基线。

| N | 表级分配：原实现 → 候选 | 表级耗时：原实现 → 候选 | 接口引用分配：原实现 → 候选 | 接口引用耗时：原实现 → 候选 |
|---:|---:|---:|---:|---:|
| 250 | 572,184 → 8,696 B | 0.610166 → 0.024584 ms | 582,576 → 19,088 B | 0.626625 → 0.012291 ms |
| 500 | 2,144,184 → 16,696 B | 2.282500 → 0.052959 ms | 2,164,984 → 37,496 B | 2.342208 → 0.025625 ms |
| 1,000 | 8,288,184 → 32,696 B | 8.768250 → 0.109125 ms | 8,329,776 → 74,288 B | 9.059584 → 0.048084 ms |

三个规模的两个入口，目标 `GetAllConfigs` 调用均由 N 次降至 0 次，目标快照物化行数由 N² 降至 0，目标点查询为 N 次。源行枚举仍保留一次快照；目标与源是同一类型时，该源快照也不会被当作可复用的目标数据缓存。38 个 NUnit fixture cases 已离线直接调用通过（既有 19 + 新增 19）；新测试对原样基线只有 4 个“拒绝目标快照”case 失败，其余 34 个通过，候选为 38/38。完整本地探针、基线快照/manifest、构建日志及两轮结果保留在 `.agent/issue-790/`，不作为版本化源码交付。

这些是本机离线逻辑和合成输入的测量，不据此承诺跨机耗时或实际帧率收益。本轮另在 Unity 6000.6.3f1 原生 EditMode Runner 执行上述三个 fixture，38/38 通过、0 failed、0 skipped；脚本门禁确认 refresh completed、compilation started/finished、domain reload observed、Editor ready 和 Console error count=0。回执 job 分别为 `217332da585d44ce9a9c6e1c7df6bb1a`（测试）与 `6573e621f29b43fcb16da795371e54ca`（编译）。Player/IL2CPP 和正式配置规模未覆盖；原 F06 独立轻量复核仍待执行，修复入 main 不代表 Issue 已验收关闭。
