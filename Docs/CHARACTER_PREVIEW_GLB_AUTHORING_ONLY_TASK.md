# 预览 GLB 仅保留在制作侧：实施任务

> Status: Design
> Implementation: Implemented for 000008; final closeout and validation evidence in §12.15
> Updated: 2026-09-20
> Baseline: `e5c554c88` 加当前未提交工作区；实测样本为 `000008`。
> Scope: 保留作者源包里的专用预览 GLB，停止向 Unity 生成包复制，移除对应运行投影，并安全迁移已经生成的副本。
> 第 1～11 节保留任务合同，第 12 节记录实际实施；最新状态以 §12.15 为准。实际 AssetBundle/Player 成品构建不在本轮完成声明内。

## 1. 目标与范围

让专用预览模型只承担制作用途：CharacterStudio 从作者源包读取它；正式角色的模型、Avatar、Prefab 和运行资源使用已有正式源与生成产物。

对 `000008`，最终保留：

```text
Authoring/CharacterPackages/character-000008/
  resource_catalog.json                         # 保留预览记录和身份
  previews/character_00.preview.glb              # 保留源文件

Assets/MxFrameworkGenerated/CharacterPackages/000008/
  resources/models/000008_lod0.fbx               # 保留正式模型
  previews/thumbnail.png                        # 保留缩略图
  prefabs/000008_character_preview.prefab        # 保留正式生成 Prefab
  runtime_resources/Resources/.../*.prefab       # 保留运行副本
  materials/、preview_materials/、generated/      # 保留当前需要的材质和烘焙产物
```

停止生成并迁移删除：

```text
Assets/MxFrameworkGenerated/CharacterPackages/000008/previews/character_00.preview.glb
Assets/MxFrameworkGenerated/CharacterPackages/000008/previews/character_00.preview.glb.meta
```

本任务不会删除作者源 GLB，不要求把它重新搬到 ArtSource，也不取消 GLB 格式支持。当前源文件已经位于制作侧，主要工作是停止生成第二份，并收敛运行引用。

### 1.1 明确不做

- 不把所有 `.glb`、所有 `preview*` 文件或整个 `previews/` 目录都判为可删。
- 不影响正式 `characterModel` / `weaponModel` GLB、GLTF 生物或静态武器资源。
- 不删除缩略图、名为 `*_character_preview.prefab` 的正式 Prefab、`preview_materials/` 或 Trace/Hurtbox/Socket 数据。
- 不强制网页改用 GLB，也不改变当前 FBX 优先预览与坐标适配规则。
- 不继续压缩 JSON、不改动画资源计划的 v1/v2 兼容、不重做全局资源系统。
- 不把制作侧 GLB 自动改成可随时丢弃的缓存；其生成来源可能已变更，源文件的删除/重生成需要另行任务。

## 2. 已核实基线与收益口径

### 2.1 000008 样本

| 项目 | 当前实测 |
| --- | ---: |
| 生成包非 meta 字节 | 17,694,158 B |
| 生成包非 meta 文件数 | 43 |
| 生成包 JSON 字节 | 731,065 B |
| 专用预览 GLB | **11,618,384 B** |
| GLB 占当前生成包非 meta 体积 | **65.66%** |
| 仅扣除这一份副本后的静态估算 | **6,075,774 B** |

生成副本与作者源逐字节一致，SHA-256 为：

`51edef58727ba0f1690d135998922d0ebcd47dfadf21f25cc84fc633f0b313b8`

**引用面（2026-09-20 实测，必须作为预期判据）**：该生成副本的 GUID `d223ba3063cfcad4abc10dafbec0e9fe` 在全仓只有**一个引用方**——`config/unity_resource_catalog.json`，也就是本任务要停止生成的那份 catalog。主 Prefab、Runtime 镜像 Prefab、`Assets/Scenes/` 下任何场景、其它角色包**均不引用**。推论：

- §7.1 的分阶段保护，实质风险是**时序**（不能在该 catalog 停止引用它之前先删文件，否则 catalog 与磁盘短暂不一致，Unity 侧缓存还可能把旧记录写回），而不是留下悬空引用。
- 实施人若在 Prefab 或场景中发现该 GUID，**应视为异常**（说明存在本任务未识别的引用路径），而不是当作预期内情况处理。

**同一内容被 7 个包共享（必须写明，避免误读收益）**：内容 hash `sha256:51edef58…` 在全局 Profile 中对应 **7 个 entry**——`charpkg.000002/000004/000005/000006/000007/000008/000009` 的 `resource.preview.body_mesh`；`previews/thumbnail.png`（`sha256:5442882e…`）同样在多个包重复。因此：

- 这 11,618,384 B 是**按包重复的同一份内容**，不是 `000008` 独有的角色数据；7 个包合计约 81 MB 的重复字节。
- 本文的收益口径按 `000008` 单包计量（正确），但不得据此认为这是该角色的专属资产，也不得据此认为同策略只适用于一个包。
- §8「第一批只迁移 `000008`」是刻意的保守选择，保持不变；其它包由后续批次按同一策略处理。

源目录记录：

| 字段 | 值 |
| --- | --- |
| `resourceKey` | `char.000008.preview.body_mesh` |
| `stableId` | `charpkg.000008.resource.preview.body_mesh` |
| `typeId` | `model` |
| `usage` | `previewMesh` |
| `relativePath` | `previews/character_00.preview.glb` |
| `provenance.origin` | `derived-preview-artifact` |
| 当前生成副本 GUID | `d223ba3063cfcad4abc10dafbec0e9fe` |

开工重新测量，记录 HEAD、dirty 状态、源与目标 hash；工作区还有其它任务，不能恢复旧数字来迎合本文基线。复现命令（与本文口径一致）：

```bash
python Tools/MxFramework.Authoring/scripts/measure_character_package_export.py \
  Assets/MxFrameworkGenerated/CharacterPackages/000008
```

### 2.2 不能混淆三种收益

1. **生成目录占用**：本任务可直接消除 11,618,384 B 的重复 GLB，加上它的 meta，另有小量 JSON 引用变化。
2. **Player 的 JSON/bytes 暂存**：`CharacterTestPackageTestCommand` 当前只复制 JSON 和 bytes，本来就不复制 GLB。不能把这 11.6 MB 宣称为该暂存目录的新收益。
3. **Resource Mapping / AssetBundle 构建**：该预览当前在全局 Profile 中已经是 `deliveryIntent=editorOnly`、`resourceKey=null`、`bundleIntent=null`；Build Plan 编译器跳过 EditorOnly 项。仍需核查 Unity 序列化依赖或其他打包路径是否间接携带它；没有真实构建数据，不承诺 Player 可执行包恰好减少 11.6 MB。

删除当前工作树的副本不回收旧 Git/LFS 历史。历史压缩或清理不属于本任务。

## 3. 当前链路与必须同时修改的出口

工程路径相对 `E:\Unity\WxFramework\WGameFramework`。实施前阅读根/子路径 AGENTS，以及 [SDK 边界](CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md)、[资源工作流](RESOURCE_SYSTEM_WORKFLOW.md)、[Character Application 接口](Interfaces/CharacterApplication.md)、[API 规范](API_STANDARDS.md)、[质量门禁](QUALITY_GATE.md)。

| 当前落点 | 已核实行为 | 本任务要求 |
| --- | --- | --- |
| `Tools/MxFramework.CharacterStudio/web/src/viewport3d.js`：`packageUrl`、`resolvePreviewModelRelativePaths` | 模型 URL 基于 `state.packageRelative`，即作者源包；支持 FBX/GLB/GLTF，通常先尝试正式 FBX | 保持从作者源读取，禁止回退到 Unity 生成副本才能预览 |
| `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`：`ServeStatic` | 从允许根读取文件，校验相对路径未逃逸 | 复用当前路径边界，不开放任意本机文件读取 |
| `.../Core/CharacterPackages/CharacterAuthoringCompiler.cs`：`BuildResourceMapping` | 当前遍历源 catalog，把预览 GLB 也映射到生成目录 | 源 catalog 保留；运行/Unity mapping 排除符合新策略的预览 GLB |
| 同文件：`BuildWritePlan` | 根据 mapping 生成 resourceFile 拷贝操作 | 不再产生该 GLB 的拷贝操作，不用先复制再删除 |
| 同文件：`BuildPresentationResourceKeys` | 将映射中的源资源键投影进角色表现配置 | 运行配置不再包含专用预览 GLB 键；不影响其它资源 |
| `.../Core/CharacterPackages/CharacterResourcePlanCompiler.cs`：`BuildReferencedKeys`、`IncludeDependencies`、`BuildRuntimeCatalog` | 从源 resourceKeys/catalog 重新收集资源，预览进入 UiDeferred | 同步排除专用预览 GLB，防止绕过 mapping 又被加入运行计划 |
| `.../Core/CharacterPackages/CharacterResourceMappingV2Producer.cs` | 独立遍历完整源 catalog；预览目前已归 EditorOnly | 保留制作索引语义，继续无 runtime key / bundle intent；不能因 mapping 缺失回退成运行资源 |
| `.../Cli/CharacterPackageCommands.cs` | 由 mapping 生成 Unity catalog，写独立 runtime catalog/resource plan | 不留下指向已取消副本的 Unity/runtime 条目 |
| `.../Cli/CharacterPackageExportCleanup.cs` | 将未出现在当次 resourceFile 集合里的资源当作孤立候选，可删除 previews 下 GLB | **需要对本任务候选显式延期保护**，不能在 Unity 重建和引用检查前直接删除 |
| `Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterPackageImportCommand.cs` / `CharacterPackageImportRequestBridge.cs` | 菜单与服务/MCP 路径各自完成 Unity 同步与 receipt | 两条路径复用同一个预览副本清理/验证步骤，防止只修其中一个入口 |
| `CharacterUnityResourceCatalogUpdater` 与 Prefab builder | 更新 Unity 资源快照、运行 Prefab、缓存和 receipt 证据 | 重建后不引用旧 GLB；清理后不让缓存恢复旧孤立记录 |

网页当前支持直接 FBX 预览，见 [FBX 预览说明](CHARACTERSTUDIO_FBX_DIRECT_PREVIEW_PLAN.md)。`000008` 的正式 FBX 名称与预览 GLB 的名称不相同，不能假定这个 GLB 一定是当前默认主视图实际选择的文件；须分别验证当前主视图和源 GLB 的读取路径。

## 4. 冻结的资源分类策略

### 4.1 判定条件

新增一个共享的 noEngine 导出分类入口（建议 `CharacterPackageResourceExportPolicy`，计划命名，需按公共 API 规则记录）：

- `usage` 明确为 `previewMesh`；
- 有效源格式为 GLB，规范化文件扩展名为 `.glb`，比较大小写不敏感；
- 源路径满足现有包内安全路径合同。

符合条件的条目分类为 **AuthoringPreviewGlb**。`typeId=model` 是合法输入，不能仅根据 typeId=preview 判断；`provenance` 用于追溯，不把某个导出工具字符串当作强制准入条件。

策略只表达导出用途，不修改源对象。Compiler mapping、资源计划、诊断/资源浏览和迁移候选生成必须复用此判定，不能各自按文件名猜测。

`previewMesh` 的 FBX/GLTF、`previewThumbnail`、正式 characterModel/weaponModel GLB 保持原行为。本任务不引入通用“所有预览不导入”的开关，也不修改所有源 schema。

### 4.2 源数据与运行投影的分离

必须保留：

- 作者源 `resource_catalog.json` 中该项的资源键、stableId、相对路径、内容 hash、provenance 和 preview 元数据。
- 作者源 GLB 本体及浏览器可访问路径。
- Authoring API / Resource Manager 对制作资源的浏览和检查能力。
- `package_cache/resource_catalog.json` 如果继续作为源快照保存，可以保留该项，但必须明确它不是运行时资源清单。

必须从运行/Unity 导入投影移除：

- `config/resource_catalog_mapping.json` 的该项；
- `config/unity_resource_catalog.json` 的该项与生成资产地址；
- `config/runtime_resource_catalog.json` 的该项；
- `config/character_resource_plan.json` 的 UiDeferred/其它组中的该键；
- `config/character_config_patch.json` 的运行表现资源列表中的该键；
- resolver verification 的必需运行资源集合、Prefab Bootstrap 及真实构建计划中的该运行资源引用。

当前源 `ApplicationConfig.ResourceKeys` 是混合制作输入，不直接改写或删除其中的预览键。先校验源键确实存在，再根据用途生成运行子集；不存在的普通资源键仍必须报错，不能因为 mapping 中缺项就全部忽略。

### 4.3 正式运行用途冲突必须阻断

如果一个被标为专用预览的 GLB 同时被以下位置实际用于运行，返回明确诊断（建议 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE`），不得静默删引用或换模型：

- 角色 body/model root、正式武器模型、附件或其它明确的运行资源选择；
- 保留运行资源的依赖边；
- 运行 Prefab/Scene/ScriptableObject 的 Unity 对象引用；
- 其它 producer 的运行 ResourceKey / Bundle / preload 依赖。

源 `preview.previewMeshResourceKey` 和上述混合制作资源列表不等同于正式运行用途；必须按字段合同分类，不用全仓字符串命中数量决定是否可删。

若同一物理文件也被正式 body/weapon 条目保留，必须保护文件并报告用途冲突；不能让“预览别名被排除”删除正式资源。解决冲突需要资源所有者明确分类，本任务不自动重标成 characterModel。

### 4.4 GLB 外部依赖与验证

不要假定所有 GLB 都内嵌了全部纹理/buffer。预检源 GLB 的 URI 与目录依赖：

- 制作预览可从允许的作者源范围读取外部依赖，不能使用系统绝对路径或越界 URI。
- 本任务只移除有证据的生成 GLB 副本；不递归删除同目录贴图、buffer 或共享材质。
- 预览引用的贴图/材质若也供正式模型使用，按正式资源继续导出。
- 源 GLB 缺失、hash 不一致或依赖丢失，沿现有作者源校验报告，不从 Unity 旧副本反向恢复并伪装来源正确。

## 5. CharacterStudio 与资源诊断

### 5.1 预览读取路径

复用现有作者源 URL，例如：

`/Authoring/CharacterPackages/character-000008/previews/character_00.preview.glb`

实际 Host/port 由当前服务决定，不在 JS 硬编码 4873。保留源模型 hash / URL version 的缓存更新语义；不能把已取消的生成路径塞入 URL。

网页当前优先 FBX 的行为保持。若现有源 GLB 没有被主视图选中，验证其独立制作预览路径即可，不为了测试而修改正式 body 配置或强制所有角色切换格式。

### 5.2 UI 与诊断合同

对符合策略的资源，界面显示“制作预览，无需 Unity 导入”或等义文案。应区分：

- 制作源可读取；
- 不期望生成 Unity 副本；
- 不具有运行加载资格。

沿用现有 `PreviewOnly` / 资源用途语义，不把该资源伪装成 RuntimeReady。缺少生成目录 GLB 不产生导入缺失/运行必需错误；但源文件真正损坏或被正式运行字段选中仍必须报错。

保留缩略图与实际模型的原校验，不把所有 preview 或 UiDeferred 诊断统一屏蔽。当前 EditorHub 诊断修复仍有在途改动，实施人需以当前代码和测试为准衔接，不能覆盖其修改，也不能把隐藏诊断当作本任务验收。

## 6. 编译、哈希和版本

### 6.1 单一分类、多个出口

先生成完整源 catalog 的只读用途分类，再派生 Unity/runtime 子集。不要从源 catalog 中 Remove 条目后复用被修改对象生成制作 API 或诊断。

`CharacterResourcePlanCompiler.BuildReferencedKeys` 在显式列表与“列表为空则选全部”两个分支都必须应用同一策略。依赖展开也必须检查，防止预览键在 IncludeDependencies 或 resource selection 合并时被加回。

全局 V2 Producer 当前已有 EditorOnly 分支，优先复用；允许保留 EditorOnly 制作记录，不要求其必须从全局 Profile 消失。要证明它仍无 runtime ResourceKey、无 BundleIntent，且编译出的可交付资源集合不包含该 GLB。不要直接文本修改共享 Profile。

### 6.2 本次哈希变化不同于上一项 JSON 精简

本任务确实改变运行资源集合，因此：

- 源文件和源配置不变时，不重定义源 canonical hash 算法；预览仍可留在源事实/hash 中。
- ResourceMappingHash、WritePlanHash、GeneratedConfigHash、角色资源计划 hash 等可因移除运行投影而变化；按现有计算器重新生成。
- 不强求所有哈希不变，不复制旧 receipt 的 hash，也不修改源版本号来强行刷新。
- Prefab/config 内容缓存必须失效并按正常路径更新；保留正式资产 `.meta` GUID。
- 当前动画资源计划 v2 的五字段合同不因本任务改变，完整动画编译对象与语义 planHash 仍由原算法负责。

目标摘要布局为 **exportLayoutVersion=4**，承接动画计划精简的布局 3。开工核对当前实际 producer/产物与上一任务状态；若已有其它任务占用该版本，先统一版本登记，禁止同号异义。summary format/schema 保持原版本，不把布局号变成 Runtime 就绪门禁。

## 7. 旧副本迁移：必须分阶段

### 7.1 为什么不能只改 mapping 后重导入

当前 `CharacterPackageExportCleanup` 在 CLI 导入后按 resourceFile 写入集合判断孤立资源。一旦本任务停止写 GLB，它会立即成为孤立候选，可能在 Unity 重建 Prefab 前被删除。

具体机制（实施时按符号核对当前源码）：

- 扫描范围：`CharacterPackageExportCleanup.OrphanScanRoots = { "resources", "previews" }`——**`previews` 已在扫描范围内**；
- 候选判定：不在本次导入 `import_report.resourceFile` 集合内的文件即候选；
- 执行时点：CLI 导入成功收口时，**早于** Unity 侧目录/Avatar/Prefab 重建。

因此必须先实现本任务候选的**延期保护和显式清理**，再打开生成过滤。不能让旧通用孤立扫描抢先删除新策略的候选文件。

既有清理器已具备可复用的机制，**不要另建平行体系**：

- `--migration-backup <dir>` 参数与默认易失备份目录；
- 结果词汇 `Deleted` / `Missing` / `Skipped` / `Conflict` / `Failed`；
- 写入前哈希前置校验、路径越界与 reparse point 拒绝、幂等跳过。

延期保护应作为该清理器的一类显式输入（受保护路径集合）实现，并在 import report 中以同一套结果词汇报告"已延期"状态。

### 7.2 阶段 A：准备与受保护的文件导入

1. 采集源/生成文件清单、各项 hash、meta GUID、旧映射和导入报告，保留当前用户改动。
2. 根据源用途、旧 mapping/resourceFile 记录和实际包根生成候选，记录 sourceKey、source path、旧 generated path、两侧内容 hash、meta hash/GUID、依赖检查结果。
3. 将候选及其历史目标作为受保护路径传给既有清理器；不要通过伪造 resourceFile“已复制”操作来规避删除。
4. 正常导入新的 config/catalog/write plan；全局映射通过 producer 完整快照、写锁和 revision 校验同步。
5. 将待清理计划与备份写到本地 `Temp/CharacterPreviewGlbMigration/<packageId>/<run>/`，或调用者明确指定的本地证据目录。正式包无需增加新的常驻大 JSON。
6. CLI 文件阶段完成时明确报告“Unity 引用验证/预览副本清理待完成”；不能仅凭 CLI Ready 就宣布整个任务完成。

计划丢失时可以从仍存在的源、旧记录与生成副本重新预检，不能把“找不到计划”当成可任意删除的理由。dry-run 只报告候选，不改项目文件/meta，也不触发清理。

### 7.3 阶段 B：Unity 验证与定向删除

1. 停止运行态后，经现有 Unity Import Bridge 更新目录、Avatar、主/Runtime Prefab、Registry 和必要缓存。
2. 核对正式 body/weapon 仍指向正确资源；检查重建后的 Prefab、运行 Scene/ScriptableObject 和构建依赖，不能仍引用待删 GLB 的 GUID。**基线预期：该 GUID 当前只有 `config/unity_resource_catalog.json` 一个引用方（§2.1 引用面）；在 Prefab 或场景中出现即为异常，需查明来源而不是直接删除。**
3. 核对候选仍与准备时的 source/generated/meta hash 相同；源 GLB 存在且可读，保留资源没有共享同一生成物理路径。
4. 任何角色/包外引用、路径越界、junction/symlink、用途或 hash 冲突均保留文件并返回明确诊断；不自动重定向引用。
5. 使用 Unity Editor 的定向资产删除入口处理该 GLB 和 meta；保留源 GLB及其身份。不递归删除 previews 文件夹。
6. 删除后刷新 Unity catalog 的孤立项与相关本地缓存，确认旧 cached catalog 不会把已删除的资产记录重新写回。必要时增加仅针对该清理阶段的显式刷新参数，不全量清空 Library。
7. 重验正式 receipt 并发布当前成功结果。菜单导入和 `CharacterPackageImportRequestBridge` 路径必须走同一完成逻辑。

若某个入口只执行 CLI 而没有 Unity，允许文件阶段完成且旧副本暂时保留，但必须保留 pending 状态；后续正常 Unity 导入完成清理。旧副本暂存期间不得被当作新的有效运行资源重新加入目录。

### 7.4 恢复与回退

- 删除前备份生成 GLB 与原始 meta，以及本次会更新的配置/报告/相关 Prefab；保留字节和 hash。
- 删除失败、缓存刷新失败或 receipt 验证失败时，报告未完成；恢复需要本次操作已删除的 GLB/meta，冲突时不得覆盖后来的外部写入。
- 恢复原资产必须保留原 GUID，不用重新复制源 GLB后随机生成新 meta 冒充恢复。
- 不宣称整个现有导入器具有跨进程整包事务。报告文件阶段、Unity 重建、定向删除、receipt 各阶段的实际结果。
- 回退代码时用原导出策略重新导入并重建相关配置/Prefab/receipt；不能只把 GLB 放回来而保留不匹配的资源计划。

## 8. 实施顺序与代码任务

| 阶段 | 交付内容 | 主要落点 |
| --- | --- | --- |
| P0 基线 | 当前用途、源 HTTP、GUID/运行依赖和真实 Build Plan 证据 | 000008 源/生成包、Resource Mapping、CharacterStudio |
| P1 分类与保护 | 单一 AuthoringPreviewGlb 判定、正式用途冲突诊断、旧副本延期保护 | Authoring Core、CLI 清理器与报告路径 |
| P2 运行投影 | mapping/write plan/catalog/resource plan/config patch 各出口一致排除 | CharacterAuthoringCompiler、CharacterResourcePlanCompiler、CharacterPackageCommands |
| P3 制作入口 | 源预览仍可用，资源状态明确“无需 Unity 导入” | CharacterStudio / Authoring Resource Provider 与命中 diagnostics |
| P4 Unity 收口 | 引用检查、定向删除、缓存更新、两条导入入口和 receipt | CharacterPackageImportCommand、CharacterPackageImportRequestBridge、CatalogUpdater |
| P5 正式迁移 | 000008 两次导入、旧副本不复活、源不变、对照和文档 | 正常 CLI + Unity 生产流程 |

P1 的保护必须先于 P2 的过滤生效，或作为同一个不可拆的变更交付。不要先提交“停止复制”，再让后续任务补删除安全。

第一批只迁移 `000008`。可用隔离 fixture 覆盖其它类型，但不要批量删除其它正式角色中的 GLB。

当前包中的动画计划已出现精简产物，而上一任务文档仍可能标为未实施；开工须核对实际代码、schema、导出布局和验收记录，不能只凭文件变小推断前置任务全部完成。

## 9. 验证矩阵

| 场景 | 通过标准 |
| --- | --- |
| previewMesh + GLB + typeId=model | 分类为制作侧；作者记录/文件完整保留，运行导出排除 |
| 正式 characterModel/weaponModel GLB | 正常导入、映射、加载，不受影响 |
| previewThumbnail / preview FBX/GLTF | 保持本任务前行为 |
| 同文件双重用途 | 文件受保护，返回可定位的用途冲突，不静默删正式资源 |
| 跨包分类一致性 | §2.1 的 7 个共享同一 content hash 的 `previewMesh` entry **必须分类一致**；任一个结果不同即说明判定条件在某条路径上未生效，需先查明再继续 |
| 角色 resourceKeys 为空 | “选全部”分支仍不把专用预览 GLB加入运行计划 |
| 缺失普通资源键 | 继续报错，不能因新的过滤逻辑而被忽略 |
| 运行依赖指向预览 | 编译/依赖验证拒绝，不在递归依赖展开时复活该键 |
| 全部运行出口 | mapping、Unity/runtime catalogs、角色 plan、config patch、verification、Bootstrap 中无该运行资源引用 |
| 全局映射 | EditorOnly 记录可以保留；无 runtime key/bundle intent；其它 producer 数据不变，真实 Build Plan 不包含预览载荷 |
| 作者源/API | resource catalog 和 GLB hash 不变；源 URL 能返回同一 GLB；不读取生成副本 |
| 浏览器 | 正式角色主视图仍可加载；源 GLB 通过实际 loader 读入；无生成路径 404、无被误当成功的占位/SVG fallback；挂点/受击体仍可编辑 |
| 合法外部依赖 | 作者范围内纹理/buffer 正常读取；越界 URI 拒绝；共享依赖不误删 |
| 旧副本初次迁移 | CLI 阶段受保护；Unity 重建与引用检查通过后才删除；source/meta 备份可核验 |
| 跨包/Scene GUID 引用 | 清理拒绝，报告引用位置；其它文件不被删除 |
| hash 在途变化 | 拒绝删除或覆盖，不用“超过几分钟无写入”代替一致性检查 |
| CLI-only / dry-run | 前者明确 pending 且旧副本仍在；后者项目文件/meta 完全不变 |
| 第二次导入 | 旧生成 GLB/meta 不复活，保留 GUID 不变，不重复制造迁移副本 |
| 清理/缓存/receipt 失败 | 明确未完成，有恢复路径，不发布假成功 |
| Runtime/动画/烘焙 | 正式加载/Spawn Resolve、Avatar、动作绑定和既有 bake 全部通过，原采样数据不因本任务改变 |

浏览器 loader 的自动化 fixture 不能代替真实页面视觉验收；如果当前工具无法进行真实浏览器检查，必须保留该项未验证，不关闭整个任务。

### 9.1 执行入口

- 在现有 Authoring 测试程序新增聚焦组 `character-preview-glb`（待实现），覆盖分类、引用、所有导出出口、共享路径及 deferred cleanup。复用已有 create/import/export fixture。
- 回归现有 `character-export-layout`、`character-animation-runtime-plan`、`character-unity-import-bridge`、`character-unity-import-receipt` 中命中的行为；先核对当前分组已经注册，不运行不存在的入口。
- 为 CharacterStudio 增加基于实际 helper/loader 的 source-only 预览测试，复用 FBX/GLB 与 palette/baked surface 的现有测试表面；不是仅检查源码是否含某个字符串。
- 修改 Unity C# 后优先执行 `validate_unity_scripts`；同时取得 refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count=0。
- 针对 Unity 引用阻断、删除/恢复、catalog cache 和正常 receipt 跑聚焦 EditMode 测试。测试根独立于真实 StreamingAssets/正式角色目录；`total=0` 不算通过。
- Authoring 的 .NET 构建与 Unity 编译分别记录，不用辅助 Unity csproj 构建冒充 Editor 验证，不追加无关全仓测试。

## 10. 正式 000008 验收与计量

必须证明：

1. 源 GLB 文件及源 catalog 的相关字节/身份不变；作者源 HTTP 可读，实际浏览器制作流程正常。
2. 生成 GLB 和同名 meta 不存在，重复导入也不恢复。
3. 正式 `000008_lod0.fbx`、缩略图、贴图、主/Runtime Prefab、材质和烘焙数据仍在；保留的 meta GUID 与基线一致。
4. 包内 Unity mapping/catalog 由当前 4 项变成预期 3 项，只有目标预览 GLB 被排除；作者源 catalog 和源快照仍保留 4 项。
5. runtime catalog 当前 116 项预计变为 115 项；UiDeferred 预览模型移除、缩略图保留。若其它任务改变了基线，按实际逐项对照，不能为了凑数量删资源。
6. 正常构建与真实 receipt 通过；新 config/resource mapping/write plan hash 一致且有来源，不手写旧回执。
7. 全局 Profile 的 EditorOnly 制作记录保留，继续无 runtime key / bundle intent；其它角色和 producer 不被改写。
8. 记录删除前后非 meta 字节和 JSON 引用变化。GLB 主收益为 **11,618,384 B**；静态估算 17,694,158 → 6,075,774 B，剩余小量变化逐文件说明。

JSON/bytes 暂存只计算配置减少的实际差异。若声称 AssetBundle/Player 下载体积减少，另外保存同输入同配置的构建前后报告；不能从 GLB 源文件体积直接推导压缩后的包体收益。

## 11. 文档与交付物

更新命中章节：

- `Docs/Interfaces/CharacterApplication.md`：专用预览 GLB 的源与运行投影边界。
- `Docs/Interfaces/Resources.md` / `Docs/RESOURCE_SYSTEM_WORKFLOW.md`：仅补本任务涉及的 EditorOnly 与 runtime mapping 解释，不重写资源系统。
- `Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md`、`Docs/CHARACTERSTUDIO_FBX_DIRECT_PREVIEW_PLAN.md`：制作源读取方式和格式边界。
- `Tools/MxFramework.Authoring/README.md`、`Tools/MxFramework.CharacterStudio/README.md`：布局 4、CLI pending 与完整 Unity 完成条件。
- `Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md` 的产物表：生成包不再普遍包含专用预览 GLB；明确运行 Prefab/preview_materials 仍有消费者。
- 本文：追加实际改动、测试名/结果、字节/GUID对照、未覆盖项。此前任务的历史测量不回填成新数字。

交付清单：

- [x] 分类和运行出口代码、UI说明、迁移保护与 Unity 收口已实现；最后补强见 §12.15。
- [x] 正式 000008 已迁移，生成 GLB/meta 已删除；保留资产 GUID 对照见 §12.14。
- [x] 浏览器主视图/源 GLB、图层交互、正常导入和删除后 receipt 已验证；完整 Player 运行不在此声明内。
- [x] 当前全局构建计划排除该载荷，跨目录/原生 Prefab 引用阻挡回归通过；实际成品构建尚未执行。
- [x] 源/目标文件清单、体积、hash、GUID 与重复导入对照已记录。
- [x] 本地备份与证据留在 Temp，不提交；正文记录来源和恢复边界。
- [x] 源 GLB 保留，临时测试资产已清理，其它任务修改未回退。

提交/推送遵循交接人的明确要求，不把当前工作区其它任务一并提交。

### 11.1 可直接交给实施人的任务说明

按本文让专用 previewMesh GLB 仅保留在作者源包。先实现单一用途分类和旧副本延期保护，再同步过滤 Compiler mapping/write plan、角色资源计划、运行目录和表现配置；保留源 catalog、源 GLB、缩略图、正式模型以及已有 EditorOnly 制作索引。网页继续从作者源读取，不靠 Unity 生成副本。正式迁移先重建并核查 Unity/跨包运行引用，再定向备份删除 GLB/meta、刷新目录缓存、重验 receipt。首批只做 000008，验证两次导入、源字节与保留 GUID、真实浏览器预览、正常角色加载及实际体积。禁止按扩展名全删 GLB，禁止让通用孤立清理抢先删除，也不将源目录节省冒充 Player 包体收益。

---

## 12. 实施记录（2026-09-20）

### 12.1 已实现

| 阶段 | 落点 | 内容 |
| :--- | :--- | :--- |
| P1 分类 | 新增 `Core/CharacterPackages/CharacterPackageResourceExportPolicy.cs` | 共享 noEngine 分类入口：`usage == previewMesh` **且** 有效源格式为 GLB（大小写不敏感，接受 `glb` / `.glb`）。`typeId` 不参与判定（`typeId=model` 是合法输入）。同时定义诊断码 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE`。**只读分类，不修改源对象。** |
| P1 保护 | `Cli/CharacterPackageExportCleanup.cs` | 新增 `protectedRelativePaths` 输入与结果词汇 `Deferred`。命中受保护的候选**保留文件与 `.meta`**，记录为 `Deferred`，不进入删除路径；`Succeeded` 不受影响。 |
| P1 保护 | `Cli/CharacterPackageCommands.ExportLayout.cs` | 新增 `CollectDeferredPreviewPaths(package)`：受保护路径直接取自源条目 `relativePath`（生成副本与源保持同一包内相对路径），不伪造 resourceFile 操作。 |
| P1 保护 | `Cli/CharacterPackageCommands.cs` | 清理器调用传入受保护集合；新增 `exportCleanupPending=<n>` 明确报告"Unity 引用验证与定向删除待完成"，CLI 阶段不宣称整任务完成。 |
| P2 投影 | `CharacterAuthoringCompiler.BuildResourceMapping` | 跳过 `AuthoringPreviewGlb` 条目。该映射是 **write plan、表现资源键（config patch）、Unity resource catalog、resolver verification plan** 的共同上游，过滤一次覆盖四个出口。 |
| P2 投影 | `CharacterResourcePlanCompiler.BuildReferencedKeys` | **两个分支**都应用分类：「显式列表」分支对解析得到的条目分类后剔除，「列表为空则选全部」分支逐个分类后跳过。 |
| P2 投影 | `CharacterResourcePlanCompiler.IncludeDependencies` | 依赖展开时不得把 `AuthoringPreviewGlb` 重新加回运行集合。`BuildRuntimeCatalog` 按 `referencedKeys` 迭代，自动继承排除。 |
| P2 冲突 | `CharacterAuthoringCompiler`（`CollectFormallyClaimedPaths` / `AddPreviewGlbRuntimeReferenceIssue`） | 同一物理文件同时被非预览条目声明时：**保留该条目**（使文件继续导出、不被孤立清理）+ 以 `ExportBlocked` 的 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE` 报告用途冲突。不静默删引用、不自动换模型、不自动重标成正式用途。 |
| 测试 | 新增 `tests/.../CharacterPackageTests.PreviewGlb.cs` | 套件 `character-preview-glb`（3 例），已注册到 `Program.cs` 与 `RunAll()`。 |

### 12.2 验证证据

| 项目 | 结果 |
| :--- | :--- |
| Authoring 构建 | 0 error（2 个 warning 均来自 Unity 自带 `com.unity.test-framework` 包） |
| 全量 Authoring 回归 | `MxFramework.Authoring.Tests passed.`（exit=0，含新套件） |
| 聚焦套件 `character-preview-glb` | 通过。断言覆盖：分类正/负样本（含大小写、前导点、扩展名回退、`typeId=model`、空值）、**六个运行出口全部无该键与路径**、源 catalog 保留该条目、生成副本不再产生、缩略图/正式模型/贴图继续导出、Unity catalog 由 4 项变 3 项 |
| 延期保护端到端 | 第二次导入日志出现 `- Deferred kind=exportCleanup source=previews/character_00.preview.glb`；断言旧 GLB 与其 `.meta` 均保留、报告记录 `Deferred` |
| 用途冲突保护 | 代码路径已实现；**未取得独立用例证据**（样例包不存在同一物理文件被双重声明的情形，需专门 fixture） |

### 12.3 未完成

- **P3 制作入口**：CharacterStudio 的"制作预览，无需 Unity 导入"文案与 source-only 预览测试未实现。
- **P4 Unity 收口**：引用检查、Unity Editor 定向删除、catalog 缓存刷新、两条导入入口的统一完成逻辑、receipt 重验均未实现。
- **P5 正式迁移**：`000008` 未迁移（生成副本仍在），因此 §10 的体积与条目数验收尚未发生。
- **§4.4 GLB 外部依赖预检**未实现。
- **用途冲突用例**未取得证据（见 12.2 末行）。
- 因此本次交付的是 **P1 + P2（含冲突保护）**，不构成整个任务的完成声明；`exportLayoutVersion` 仍为 3，**未升到 4**（布局 4 属于 P2 完成后的登记动作，本次未占用该版本号）。

### 12.4 环境陷阱（本次遇到，供实施人参考）

`dotnet build ... --no-restore` 在本机报 `NETSDK1004`：Unity 生成的 csproj 用 `BaseIntermediateOutputPath = Temp\obj\$(Configuration)\$(MSBuildProjectName)`，而构建期 `$(Configuration)` 为空、还原期默认为 `Debug`，两侧求值不一致，导致构建找 `Temp/obj/<Name>/project.assets.json` 而还原产出 `Temp/obj/Debug/<Name>/project.assets.json`。本次绕行：把 `Temp/obj/Debug/<Name>/` 的还原产物补齐到 `Temp/obj/<Name>/`。

另外 `bin/` 与 `bin_local/` 会被运行中的 Authoring 服务或测试进程锁定（`MSB3027`），构建需等进程结束；输出重定向必须保持 `<proj>/<单层>/Debug/<tfm>` 的深度，否则依赖 `AppContext.BaseDirectory` 向上 5 层定位样例的测试会失败。

### 12.5 续做（P3 部分 + P4 核心，2026-09-20 11:30）

| 阶段 | 落点 | 内容 |
| :--- | :--- | :--- |
| P3 | `Tools/MxFramework.CharacterStudio/web/src/resourcePicker.js` | 新增 `isAuthoringPreviewGlb(resource)`（与 Authoring 侧策略同一判定：`usage=previewMesh` + 有效源格式 GLB，源格式缺失时按扩展名回退，大小写不敏感）；`inferRuntimeBindingKind` 在 **`ResourceManagerAsset` 兜底之前**判定 `Model + previewMesh GLB` → `AuthoringPreviewOnly`，避免被当成运行资源并误报运行缺失；`inferRuntimeAvailability` 透传该状态；状态文案新增 `AuthoringPreviewOnly: "制作预览，无需 Unity 导入"` |
| P4 | 新增 `Packages/…/Editor/CharacterImport/CharacterPreviewGlbDeferredCleanup.cs` | Unity 收口：候选 = `<包根>/previews/*.glb`；逐个读 `.meta` GUID → 在 `Assets/MxFrameworkGenerated` 与 `Assets/Scenes` 的文本资产（`.prefab/.unity/.asset/.json/.mat/.controller/.overrideController/.playable`）中做引用核对 → 有引用则 `Blocked` 保留，无引用则 `AssetDatabase.DeleteAsset` 定向删除（同时移除 `.meta`）；核对无法完成（无 meta GUID / 读失败 / 超出 256 MiB 扫描预算）一律 `Unverified` 并保留文件；发生删除后 `AssetDatabase.Refresh(ForceUpdate)` 让目录与本地缓存丢弃旧记录。**不递归删目录** |
| P4 | `CharacterPackageImportCommand.SynchronizeImportedAssetsWithReport` | 在 Prefab 重建与 `ImportAsset` **之后**调用上述收口。两条导入入口（菜单/批处理 `ImportPackageInternal` 与 `CharacterPackageImportRequestBridge`）都经过本方法，因此**共享同一步骤**，不存在只修一个入口的情况 |

### 12.6 本轮验证

| 项目 | 结果 |
| :--- | :--- |
| Authoring 构建 + 全量回归 | `MxFramework.Authoring.Tests passed.`（exit=0，5m39s） |
| 聚焦套件 `character-preview-glb` | 通过 |
| Unity 侧 C# 编译 | `dotnet build MxFramework.Editor.csproj` → **0 error**。**注意：这不是 `validate_unity_scripts`**——本机 Unity Editor 未运行（只有 Unity Hub 进程），Unity MCP 工具不可用，因此改用 Unity 生成的 `MxFramework.Editor.csproj` 做编译验证，并把新文件临时加入该 csproj 的 `<Compile Include>` 列表（该文件被 `.gitignore` 忽略、由 Unity 重新生成）。**refresh / domain reload / Console 证据缺失。** |
| 前端语法 | `node --check web/src/resourcePicker.js` 通过 |
| P4 行为 | **未验证**：需要 Unity Editor 运行才能执行导入流程并观察引用核对/定向删除/缓存刷新/receipt |

### 12.7 未完成（更新）

- **P5 正式迁移 `000008` 未执行**。原因：§7.3 明确要求先完成 P4 的 Unity 引用验证与定向删除，而本机 Unity Editor 未运行，P4 既无法执行也无法验证；此时执行 P5 只会得到 CLI-only 的半迁移状态（旧副本仍保留），却已经改变正式包的六个运行出口，收益与风险不对等。**留待 Unity 可用时按 §7.2 → §7.3 → §7.4 顺序执行。**
- **P3 剩余**：CharacterStudio 的 source-only 预览自动化测试未实现；§5.2 要求的"区分三种状态"目前只落到运行可用性标签，未单独做状态明细面板。
- **P4 剩余**：`CharacterPackageImportRequestBridge` 自身的完成逻辑与 receipt 路径未单独核对（它复用共享方法，但未验证其后续步骤）；catalog 缓存回写的负向用例（"旧 cached catalog 把已删除资产写回"）未构造。
- **§4.4 GLB 外部依赖预检**未实现。
- **用途冲突独立用例**仍未取得证据。
- `exportLayoutVersion` 仍为 3，**未升 4**。

### 12.8 续做二（布局登记 + 冲突用例 + 文档同步，2026-09-20 11:40）

| 项目 | 内容 |
| :--- | :--- |
| 布局登记 | `CharacterPackageCompileSummaryBuilder` 新增 `LayoutVersionAuthoringOnlyPreviewGlb = 4` 并在调用点传入。P2 已完整（六个运行出口一致排除 + 用途冲突保护），因此占用布局 4 有据。`CharacterPackageTests.AnimationRuntimePlan.cs` 的 `exportLayoutVersion == 3` 断言相应放宽为 `>= 3`，`ExportLayout.cs` 的 `>= 2` 不变 |
| 冲突用例 | 新增 `PreviewGlb_SamePathClaimedTwiceIsProtectedAndReported`：构造"同一物理文件被 `previewMesh` 与 `characterModel` 双重声明"的包，断言 ① `GateReport` 出现 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE`，② 该路径**仍在 `ResourceMapping` 中**（文件继续导出 → 不会被孤立清理回收）。**12.2 中"用途冲突未取得独立证据"一项由此关闭** |
| 布局断言 | `PreviewGlb_IsExcludedFromEveryRuntimeExportExit` 追加 `exportLayoutVersion == 4` |
| 文档同步 | `Tools/MxFramework.Authoring/README.md`（布局 4、专用预览 GLB 策略、`Deferred` 与 `exportCleanupPending` 语义）、`Docs/Interfaces/CharacterApplication.md`（新增「专用预览 GLB 的制作侧边界」）、`Docs/Guides/CHARACTER_STUDIO_DATA_OWNERSHIP.md`（源保留 / 运行排除 / 状态标注 / 用途冲突）、`Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md` §4.2.1（迁移清理行 + 专用预览 GLB 行） |

验证：构建 0 error；**全量回归 `MxFramework.Authoring.Tests passed.`（exit=0，5m41s）**；`character-preview-glb` 四例全通过；注册表类名守卫 PASS（52 行 / 34 类名 / 0 不可解析）。

### 12.9 未完成（最终）

- **P5 正式迁移 `000008` 未执行**：Unity Editor 未运行，P4 的引用核对/定向删除/缓存刷新/receipt 无法执行与验证；此时执行 P5 只会得到 CLI-only 半迁移状态却已改变正式包六个出口。留待 Unity 可用。
- **P4 未在 Unity 内验证**：仅完成编译验证（`MxFramework.Editor.csproj` 0 error），缺 refresh / domain reload / Console 证据。
- **P3 剩余**：source-only 预览自动化测试与三态明细面板未实现。
- **§4.4 GLB 外部依赖预检**未实现（现有实现只删 `previews/*.glb` 顶层文件、不递归，故"不误删贴图/buffer/共享材质"在结构上已成立，但缺少对源 GLB 外部 URI/目录依赖的显式预检）。
- **P4 剩余**：`CharacterPackageImportRequestBridge` 自身后续步骤与 receipt 路径未单独核对；"旧 cached catalog 把已删除资产写回"负向用例未构造。

### 12.10 续做三（P3 source-only 测试，2026-09-20 11:55）

新增 `Tools/MxFramework.CharacterStudio/scripts/preview-glb-authoring-only-smoke.mjs`，并注册为 `npm run smoke:preview-glb`。

**做法**：不检查源码字符串。把真实的 `web/src/resourcePicker.js` 在桩全局（`document` / `window` / `self`）下用 `node:vm` 求值，**直接调用其生产 helper**：`isAuthoringPreviewGlb` / `inferRuntimeBindingKind` / `inferRuntimeAvailability` / `formatResourceAvailabilityLabel`；再用真实 three.js `GLTFLoader` 读取作者源 GLB。

**断言**（全部通过）：

| 断言 | 结果 |
| :--- | :--- |
| 真实 catalog 中的 `previewMesh` 条目被分类为制作侧 | `isAuthoringPreviewGlb === true` |
| 运行绑定类型与可用性 | `AuthoringPreviewOnly` / `AuthoringPreviewOnly` |
| **不是**运行就绪、**也不是**运行缺失 | 二者均已显式断言为否 |
| 界面文案 | `制作预览，无需 Unity 导入` |
| 其它用途不受影响 | `previewThumbnail` → `GeneratedPreviewOnly`；正式 `characterModel` 不落入 `AuthoringPreviewOnly` |
| 边界 | `previewMesh`+`.fbx` → false；`.GLB` 大小写不敏感 → true；`sourceFormat=".glb"` 前导点 → true；无路径无格式 → false |
| 制作源可读 | 源 GLB 11,618,384 B，经真实 `GLTFLoader` 解析出 **1 个 mesh** |
| 生成侧现状 | `generatedCopyPresent: true`（只作观察报告，用例不修改生成包） |

验证命令：`npm run smoke:preview-glb`（在 `Tools/MxFramework.CharacterStudio` 下）。P3 的 source-only 测试项由此关闭；三态明细面板仍未实现。

### 12.11 Unity 侧可用性（阻塞项，2026-09-20 11:55）

本会话的 Unity MCP 工具（`validate_unity_scripts` 等）**已不在工具索引中**，检索不到；本机只有 Unity Hub 进程，未见 Unity Editor 进程。因此：

- P4 的行为验证（引用核对 / 定向删除 / catalog 缓存刷新 / receipt）无法执行；
- P5 的正式迁移按 §7.3 的顺序要求继续暂缓；
- 现有的 Unity 侧证据仅为 `dotnet build MxFramework.Editor.csproj` 的 0 error 编译验证，**不含 refresh / domain reload / Console 证据**。

需要 Unity Editor 处于运行且已连接 MCP 的状态，才能继续 P4 验证与 P5 迁移。

### 12.12 续做四（§4.4 GLB 外部依赖预检，2026-09-20 12:00）

新增 `Core/CharacterPackages/CharacterGlbDependencyInspector.cs`：读取 GLB 容器（magic `glTF` + JSON chunk），收集 `buffers[].uri` 与 `images[].uri` 并逐条判定：

| URI 形态 | 判定 |
| :--- | :--- |
| 无 `uri`（使用内嵌二进制 chunk） | 通过，不计入外部依赖 |
| `data:` 内联 | 通过 |
| 包内相对路径且文件存在 | 通过，登记为 `ExternalDependencies` |
| 包内相对路径但文件缺失 | **违规**：依赖丢失 |
| Windows 盘符（`C:/…`、`C:/…`） | **违规**：绝对路径 |
| 以 `/` 或 `\` 开头、UNC `\\` | **违规**：绝对路径 |
| 含 `..` 段 | **违规**：越界 |
| 显式 scheme（`https:` / `file:` 等） | **违规**：超出作者源允许范围 |
| 解析后落在包根之外 | **违规**：越界 |
| 非 GLB 容器 / JSON chunk 非法 | 报告未通过预检，**不静默放过** |

接入点：`CharacterAuthoringCompiler.BuildResourceMapping` 对每个 `AuthoringPreviewGlb` 条目调用预检，违规以 `ExportBlocked` 的 `CHAR_PREVIEW_GLB_DEPENDENCY_INVALID` 报告。**只判定与报告，不修改文件、不递归删除同目录贴图/buffer/共享材质。**

现状核实：现有 7 个包的预览 GLB（`character-000002/000004/000005/000006/000007/000008/000009`）**全部自包含**（`buffers[].uri` 与 `images[].uri` 均为空），因此该预检对当前数据是纯预防性保护，不会产生新错误。

测试：`PreviewGlb_DependencyInspectorRejectsUnsafeUris` 用合成 GLB 容器覆盖上述全部形态（内嵌 / data uri / 合法相对依赖 / 缺失依赖 / 盘符 / 绝对路径 / UNC / `..` 越界 / `https:` / `file:` / 非 GLB 容器）。套件 `character-preview-glb` 现为 **5 例**。

### 12.13 续做五（P3 三态明细，2026-09-20 12:05）

`web/src/resourcePicker.js` 新增纯函数 `authoringOnlyPreviewDetailRows()`，返回三条明细行；资源卡片在 `runtimeAvailability === "AuthoringPreviewOnly"` 时追加它们：

| 明细行 | 含义（对应 §5.2 要求区分的三件事） |
| :--- | :--- |
| 制作源 **可读取** | 源可读取 |
| Unity 副本 **不生成** | 不期望生成 Unity 副本 |
| 运行加载 **不具备资格** | 不具有运行加载资格 |

**为什么抽成纯函数**：`resourcePicker.js` 虽然自身没有 `import`/`export`，但在 `web/app.js` 中是作为 ES module 引入的，依赖 `state.js` / `utils.js` 等模块挂到全局的符号（`state`、`escapeHtml`、`PROVIDER_LABELS`…）。直接渲染整张卡片需要拉起整条模块图；把三态明细抽为**无跨模块依赖**的纯函数后，smoke 测试可以直接调用并断言内容，无需为渲染补一长串桩。

smoke 测试新增断言：`authoringOnlyPreviewDetailRows()` 必须恰好返回三行、包含六个关键短语、且**不得**出现"Runtime 就绪"。`npm run smoke:preview-glb` 通过。P3 的三态明细项由此关闭。

### 12.14 P4 验证与 P5 正式迁移（2026-09-20 12:20）

Unity MCP 恢复后完成。**这是本任务的核心交付。**

#### 编译门禁

`validate_unity_scripts` 通过：refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error count = 0（修复后那次 `assembly_count=1`，确认修复确实被编译）。另经 `execute_code` 反射确认 `CharacterPreviewGlbDeferredCleanup` 与 `CharacterPackageImportCommand` 均存在于 `MxFramework.Editor` 程序集。

#### ★ 实施中发现并修复一个真实缺陷

第一次跑阶段 B 时清理器报告 `Unverified ()`（括号内为空），GLB **未被删除**。定位：`TryFindReference` 在"扫描完成且未找到引用"时把 `error` 置为 `string.Empty`，而调用处判断写成了 `scanError != null`——空串不等于 null，于是**永远走进 Unverified 分支，删除路径不可达**。修复为 `!string.IsNullOrEmpty(scanError)`，并顺手移除 `TryFindReference` 未被使用的 `importedRoot` 参数。

这个缺陷涉及 Unity Editor 类型，普通 Authoring .NET 测试没有覆盖它；csproj 编译也不验证其行为。真实 Editor 调用发现了它，聚焦 Unity EditMode 回归同样可以覆盖；不能推导为“只有手工真实运行才能发现”。对应自动化回归已在 §12.15 补充。

#### 阶段 A（CLI）

`exportCleanup=deleted:0 missing:5 **deferred:1** conflict:0 failed:0`、`exportCleanupPending=1`、`artifacts` 34 → 33。六个运行出口全部 0 引用。

| 项目 | 迁移前 | 迁移后 |
| :--- | ---: | ---: |
| `unity_resource_catalog.json` 条目 | 4 | **3** |
| `runtime_resource_catalog.json` 条目 | 116 | **115** |
| `character_resource_plan.uiDeferred` | 2 | **1**（保留缩略图） |
| 生成 GLB | 在 | **在**（延期保护生效，未删） |

#### 阶段 B（Unity）

`CharacterPreviewGlbDeferredCleanup.Execute` 在真实 Editor 内的返回：

```
log=mx.characterPreviewGlbCleanup.v1 candidates=1 deleted=1 blocked=0 unverified=0
msg=Deleted: Assets/MxFrameworkGenerated/CharacterPackages/000008/previews/character_00.preview.glb
glbStillExists=False   metaStillExists=False   thumbnailExists=True
```

引用核对通过（该 GUID 唯一引用方 `unity_resource_catalog.json` 已不再引用它），定向删除连带 `.meta`。

#### 验收（§10）

| 项目 | 结果 |
| :--- | :--- |
| 源 GLB 字节与身份 | **不变**：11,618,384 B、`sha256:51edef58727ba0f1690d135998922d0ebcd47dfadf21f25cc84fc633f0b313b8` |
| 生成 GLB 与同名 meta | **不存在**；二次导入未复活 |
| 正式模型 / 贴图 / 缩略图 / 主与 Runtime Prefab | 均在 |
| 保留的 `.meta` GUID | 主 Prefab `887aa36ccac525347b1fe357b39e5ec5`、Runtime Prefab `e8d56c5b69eb1e04692639aa0c5ef5b1`、缩略图 `a9604d4f0c767fa4cbb05017e11199da`，均与基线一致 |
| 包体 | 非 meta **43 → 42 个文件**；**17,694,158 → 6,065,812 B**，实际减少 **11,628,346 B**（GLB 本体 11,618,384 B + 配置/元数据 9,962 B）。**略优于 §2.1 的静态估算 6,075,774 B** |
| 孤立资源候选 | 无 |
| 二次导入 | `status=Ready`、`updated=0 skipped=33 conflicts=0 errors=0`、`deleted:0 deferred:0`、无 pending 行 → 完全幂等 |
| 制作源预览 | `npm run smoke:preview-glb` 通过，且 `generatedCopyPresent: false`（源可读、生成副本已不存在） |

#### 当时未完成（历史记录，当前收尾见 §12.15）

- **receipt 未在删除后重跑**：`package_cache/unity_editor_import_receipt.json` 写于 12:14:06（阶段 B 删除之前）。已核对它**不引用**被删资产，且其六个 hash 所覆盖的 config 在删除后未再变化（二次导入 `updated=0`），因此内容仍准确；但 §7.3 第 7 步"重验 receipt"这一步因 MCP 调用超时（同步约需 2 分钟，超出单次调用窗口）未能重跑。**需在 Unity 内补跑一次导入收口。**
- 未做真实浏览器视觉验收（§9 明确允许保留为未验证项）。
- 未验证 AssetBundle / Player 构建计划不包含该载荷（§2.2 第 3 条的进一步证据）。

### 12.15 最终收尾（2026-09-20）

**正式 `000008` 的删除后 receipt 重验、真实浏览器检查和当前构建计划核对均已完成。** 本节取代 §12.14 的“未完成”作为当前状态；不宣称已构建新的 AssetBundle 或 Player 可执行文件。

#### 正式 receipt 重验

复用现有 `CharacterPackageImportRequestBridge`：将当前 import report 的 packageId 与五项 compiler hash 写入正式 `rebuild_request.json` 协议，通过独立 status 文件取得执行结果。请求由 Unity 自身的更新循环消费，不依赖长时间保持 MCP execute_code 请求。

| 请求 | 完成时间（UTC） | 结果 |
| --- | --- | --- |
| `9ea96bdfb169402f8d1c93ea656e71ef` | 2026-09-20 04:25:51 | completed，正式同步与 receipt 验证完成 |
| `0252abd458394466849afe732b593f71`（收口代码补强后） | 2026-09-20 04:41:45 | completed，errorCode 为空，再次通过正式 receipt 验证 |

两次均在 GLB 删除之后运行了生产同步和 `TryValidateAndWrite`。receipt 为 `ready=true`，其五项 compiler hash 与当前 import report 一致。receipt 字节相同，写入器幂等跳过重写，所以文件 mtime 保持原值；**旧 mtime 不代表未重验，完成请求与验证路径才是本轮证据**。

不再把“MCP execute_code 超时”统一解释为“Unity 一定取消”或“一定继续”。必须读取 request status、执行历史或明确产物判定；本轮已通过正式请求桥规避这一不确定性。

#### 收口代码补强

审阅执行路径后补上以下实际缺口：

- 清理候选改为当前 import report 中 `exportCleanup / Deferred` 的明确目标，取消对目录中全部 GLB 的通配删除。没有延期操作的 GLB 保留。
- 对包根、previews 子路径、扩展名、绝对/越界路径、linked directory/file 做限制；目标不存在时保持幂等。
- `BlockedCount > 0` 与 `UnverifiedCount > 0` 都使清理不成功；导入命令将 build result 标记 Failed，两个 receipt 入口均不会发布成功回执。Prefab 构建本身失败时不进入清理。
- 扫描覆盖整个 Assets 的 Unity 依赖，能发现原有扫描范围之外的 Prefab/原生资产引用；对 JSON 中 Unity 不识别的 GUID 字符串补充有界文本检查。
- 初版扩大全量文本扫描时，真实工程的原生资产超过原 256 MiB 字节预算，正向测试因此正确拒绝删除。最终改用 AssetDatabase 检查原生/二进制资产依赖，JSON 保留 256 MiB 限额；没有调大预算或跳过错误。当前 JSON 扫描约 29.5 MB / 470 文件。
- 引用扫描前后复核 GLB/meta hash；删除前保存两者原始字节和包含路径/hash/GUID 的 manifest 到本地 `Temp/CharacterPreviewGlbMigration/unity-cleanup/`，保留恢复材料。

`CharacterPreviewGlbCleanupTests` 新增五个真实 EditMode 用例，最终 **5/5 passed、0 failed、0 skipped**，作业 `8392567e8f9040ae9df603c5d3fca7e3`：

1. 明确延期且没有引用的候选连同 meta 被删除（覆盖先前空错误串 bug）。
2. Generated/Scenes 之外的 JSON 引用会阻挡完成。
3. 原生 Prefab 依赖会阻挡删除。
4. 没有延期操作的 GLB 不删除。
5. 越界目标失败关闭。

最终 Unity 编译门禁作业 `f60f48761aea484a8812aa4230da653f`：refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error=0；2 assemblies，0 compile warnings。

#### 浏览器与构建计划

使用真实 Chromium 打开 4873 的 CharacterStudio `000008` 页面，实际观察并留图：

- 主视图从作者源 FBX 读取，显示 Maid_Costume 模型；没有依赖已删除的生成 GLB，无页面 JavaScript 异常、无请求已删除的生成 GLB。
- 挂点与受击体图层开关均可切换并恢复，未保存或改写角色配置。
- 在同一真实浏览器内以 GLTFLoader 从作者源 URL 加载 GLB，解析 1 mesh 并通过 WebGL 渲染。该 GLB 的既有内容是通用人形；当前正式主视图使用 FBX。本任务保留了源 GLB，没有声称它与当前女仆 FBX 外观相同。
- 页面仍有两条既有待机 FBX 候选路径返回 404（`source/unity/animations/...`），未改动该动画预览路径；主模型和源 GLB 检查成功，不将本次结论扩大为所有动画预览功能无问题。

当前正式 Resource Mapping Build Plan 通过：`success=true`、0 diagnostics、213 个运行 entry、23 个 bundle；计划正文既不含 `char.000008.preview.body_mesh`，也不含 `character_00.preview.glb`。正式 body 和 texture 条目仍在；EditorOnly 制作记录可以继续存在于源 Profile。

磁盘旧 build report 的状态为 `needsUnityBuild/stale`。这不改变当前计划的排除结论，但意味着**实际 AssetBundle/Player 成品尚未重新构建**，不宣称成品大小或平台运行结果。

#### 最终文件核对与证据

- 源 GLB 保持 11,618,384 B，SHA-256 仍为 `51edef58727ba0f1690d135998922d0ebcd47dfadf21f25cc84fc633f0b313b8`。
- 生成 GLB 与 meta 均不存在，正式模型、缩略图和 Prefab 保留。
- 本轮开始时的包体为 42 个非 meta 文件、6,064,986 B；与本轮结束一致。该数值比 §12.14 的阶段记录少 826 B，差异在本轮开始前已经存在，不计入本轮新增收益。
- 本轮收尾前后，对 `000008` 生成包及作者源的逐文件 hash 对照无变化；receipt 经验证后保持相同字节。测试临时资产由各 fixture 清理，未提交、未推送。

本机证据目录：`Temp/PreviewGlbCloseout/`。包含 request 与最终 completed status、前后清单/receipt、`cleanup-tests.xml`、`build-plan.json` / `build-status.json`、浏览器请求与图层交互记录、`character-studio.png` / `source-glb.png`、`final-verification.json`。完整日志与备份保持本地，不纳入正式版本化内容。


### 12.16 其它活跃角色包推广（2026-09-21）

本轮迁移 `000001 / 000002 / 000003 / 000009`，统一到 export layout 4。`000004 / 000008` 已完成迁移，本轮逐文件 hash 确认未改动；`000005 / 000006 / 000007` 已退役，未恢复或重新导入。

#### 实施与保护

- 先保存四个生成包的文件清单、SHA-256、完整本地备份和共享资源 Profile，再跑 `--check-files --check-hashes --dry-run`。实际导入使用最新源码构建的独立 CLI，未关闭制作服务器。
- 由正式 CLI 删除五项旧 JSON/镜像及对应 meta，输出精简动画计划和约 2.6 KiB 摘要。只对 `000002 / 000009` 的明确延期候选清理预览 GLB；引用检查、备份和删除由 Unity 清理器执行。
- 每包完成两次 CLI 导入及正式 `CharacterPackageImportRequestBridge` 同步，全部得到 `completed`，receipt 为 `ready=true`，五项 compiler hash 与当前 import report 一致。没有通过改写 receipt 绕过验证。
- 所有受保护 Authoring / ArtSource 文件、源模型/贴图/预览 GLB，以及未迁移的 `000004 / 000008` 保持原 hash；四个包所有保留下来的 meta GUID 均未变。共享 Profile 中非本轮四个 producer 的条目逐项相同。

#### 实测收益

统计生成包内所有非 `.meta` 文件，包含正式 Unity 同步重建的 Prefab 和 Bake；不是 Player/AssetBundle 成品大小。

| 角色包 | 迁移前（B） | 迁移后（B） | 净减少（B） | 最终状态 |
| --- | ---: | ---: | ---: | --- |
| 000001 | 7,592,640 | 6,510,546 | 1,082,094 | Ready |
| 000002 | 19,075,115 | 6,645,990 | 12,429,125 | Ready |
| 000003 | 96,170,866 | 96,093,739 | 77,127 | WarningOnly，原有四条警告 |
| 000009 | 25,853,394 | 14,100,964 | 11,752,430 | Ready |
| 合计 | 148,692,015 | 123,351,239 | **25,340,776** | 减少 **17.04% / 24.17 MiB** |

`000003` 没有生成预览 GLB，且本轮正式重建使 socket bake JSON 增加 421,920 B，因此净减少仅 77,127 B；不能把清理掉的文件大小直接宣称为最终净收益。当前约 10.88 MB 的 socket bake JSON 仍是正式消费数据，本轮没有删除或改为诊断文件。

#### 迁移暴露并修复的阻碍

1. Runtime 的接战锚点哈希仍使用标签，Compiler 已使用数字 ID。统一 Runtime 为数字身份，新增标签重命名不影响哈希、修改数字身份被拒绝的回归。
2. Windows 上 Runtime Prefab 已被 Unity 映射时，原地复制失败。沿用项目已有方法，在复制前调用 `AssetDatabase.ReleaseCachedFileHandles()`，继续保留目标 meta 和 GUID。
3. Compiler 的部位破坏导出遗漏数字 DTO 的 v3 包装和源内容编码版本，查询表仍按标签排序。补齐版本/编码字段并按数字 partId 排序，保留 Authoring 内容见证。
4. Compiler 的权威姿态骨架仍输出 Authoring v1 字符串 DTO。改为冻结目录解析的数字 v2 DTO，保留各标签、源路径、参数和 Authoring 内容哈希；外层几何哈希覆盖最终数字投影。缺失身份继续产生 ExportBlocked，不分配临时生产 ID。

以上修复没有修改 Dragon14 的骨架、接战锚点或部位破坏源配置。旧测试夹具同步到当前数字协议；未恢复旧字符串 Runtime 兼容入口。

#### 验证证据

- Unity 编译门禁 `726c40c54d664f61a346594c4e7bb394`：refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error=0。先前生产修改编译门禁 `e1342b60f1d8414d9960efa67a70a3b0` 编译 3 个程序集，0 error / warning。收尾再读 Console，仍为 0 error。
- Unity EditMode 作业 `7f84514070d5401889c3e73fa8981506`：**6/6 passed，0 failed / skipped**，覆盖数字锚点读取/篡改/标签独立性、无锚点 feature、Runtime Prefab GUID，以及正式 Dragon14 部位破坏读取。
- Authoring 聚焦套件 `character-body-part-damage`、`character-authority-pose` 均通过；后者包含真实 Dragon14 冻结目录编译、四肢 12 通道及确定性检查。CLI 构建成功；不将 .NET 测试项目构建作为 Unity 编译证据。
- 四个包均不再包含五个废弃 JSON/meta，两个生成预览 GLB/meta 均不存在，正式 config 中无其键/路径。
- 四个摘要的内容、mtime、GUID 在最后两次成功导入间稳定。最终只读预检：`000001 updated=0 skipped=32`、`000002 updated=0 skipped=33`、`000009 updated=0 skipped=36`。
- `000003` 最后预检为 `updated=1 skipped=43`，唯一更新是 `unity_resource_catalog.json`：提取动画使用外部 Avatar，现有保留策略明确要求由 Unity 重新验证观察值。两次完整同步后的结果一致；不宣称此包 CLI 全文件零写入。
- 正式资源构建计划：无错误、0 issues、211 entries / 23 bundles，计划中无两个预览键及 `previews/character_00.preview.glb`。**本轮未构建 AssetBundle/Player 成品，也未对四个角色逐一重新做浏览器视觉验收。**

| 角色包 | 最后成功同步 requestId |
| --- | --- |
| 000001 | `3bf3e7aaa39a4311a19c58b052e4276b` |
| 000002 | `3e62622a023a453c8acc4a3528763cef` |
| 000003 | `17eb91937a7d4ddea75fd1a498607d27` |
| 000009 | `c6983ffc44094c7c90c8b791aaaf0457` |

`000003` 原有警告保持：一条 `CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED`，三条 `LOCO_CAL_CLIP_METADATA_MISSING`（idle/walk/run）；不是本次新增，也未通过修改源数据消除。

本机证据位于 `Temp/CharacterPackageRollout/`：`before.json`、`protected-before.json`、`baseline/`、各次 import report/request/status/receipt、`results.json`、`verification.json`、`changes.json`、`unity-tests.xml`、`build-plan.json` 和 `final-dry-run/`。失败的中间尝试保留以便追踪；回滚材料不纳入版本化内容。本轮未提交或推送。
