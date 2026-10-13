# 任务分析：角色源数据包组织、Schema 契约与无引擎解耦流转实施规范（v1.4）

> 分析对象：[`Docs/CHARACTER_SOURCE_DATA_DECOUPLING_OPTIMIZATION_SPEC.md`](./CHARACTER_SOURCE_DATA_DECOUPLING_OPTIMIZATION_SPEC.md)
> 上游主文档：[`Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md`](./CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md) 第 3/4/5.7 章
> 分析基准：仓库当前工作区实测（`Authoring/CharacterPackages/*`、`Tools/MxFramework.Authoring/**`）
> 分析日期：2026-09-17

---

## 0. 结论摘要

**任务性质**：把角色源数据包从"约定式可跑通"升级为**契约化、可复现、可安全写入**的工程体系。v1.4 本身不引入新功能，而是对 v1.3 复审出的 P0/P1 缺陷做代码级、Schema 级、协议级的加固。

**三项实质性变化**

| 变化 | 内容 | 影响面 |
| :--- | :--- | :--- |
| ① 契约换代 | `manifest.json` 升至 `packageSchemaVersion 2.4.0`，新增 `assetEntries` / `featureFlags` / `hashes.hashStatus`，并开启 `additionalProperties: false` | 全部 9 个现有角色包、Authoring Core/Cli、Unity 侧导入桥 |
| ② 摘要重定义 | `sourcePackageHash` 从"语义文本拼接哈希"改为"物理文件二进制 SHA-256 有序清单 + 规范化 Manifest 摘要" | 导入凭据、Readyness 判定、增量/幂等语义全部重算 |
| ③ 安全与校验硬化 | 几何全维度校验、符号链接与路径逃逸封堵、程序集递归闭包扫描、WritePlan 状态机 + DAG 幂等执行器 | Core 校验器、Cli、Unity 导入执行器 |

**现状差距（最关键结论）**：规范依赖的 6 类核心构件在仓库中**完全不存在**，且现有数据会**被新门禁直接阻断**：

| 规范依赖 | 仓库实测 | 后果 |
| :--- | :--- | :--- |
| `assetEntries` / `featureFlags` | 代码与数据 0 命中（现为 `configFiles` / `authoringOnlyFiles`） | §1.1 动态展开算法无输入基础 |
| `geometry/skeleton_hierarchy.json` | 全仓 0 命中，无提取器 | `E-SRC-109` = Fatal/BlockAll，**任何包都无法编译** |
| `hashes.hashStatus` | `CharacterPackageHashSet` 无此字段 | 现有 manifest 全部 Schema 不合法 |
| 规范版 `resource_catalog.json` | 实际是 ResourceMapping V2 文档（`localId`/`stableId`/`typeId`/`variant`/`usage`/`importHints`/`hashes{contentHash,importHash,dependencyHash}`） | §1.4 Schema 与实现互斥 |
| `authoredSkeletonSnapshotId` + 快照仓库 | 0 命中，快照仓库未定义 | §4.1 重绑定协议无落地载体 |
| `package_cache/dependency_lock.json` | 全仓 0 命中 | §7.2 受控重建前置条件第 2 条当前不可满足 |

**建议路线**：`契约对齐与数据迁移` → `只读校验器（Preflight/Core）` → `摘要协议切换` → `WritePlan 执行器与安全沙箱`。**不要**直接从 §6 的写入侧开工——它会建立在尚未存在的契约之上。

---

## 1. 任务目标

| 编号 | 目标 | 可验收信号 |
| :--- | :--- | :--- |
| G1 | **输入闭包唯一化**：20 类注册输入由 `manifest.assetEntries` 确定性展开为物理文件清单，草稿文件不参与哈希 | 同一包目录多次运行得到同一 `FilePaths` 集合；未声明文件被排除 |
| G2 | **契约机器可校验**：manifest / resource_catalog / weapon_slots / skeleton_hierarchy 四份 Draft-07 Schema 可执行且语义门禁齐备 | Schema 校验器对 9 个现有包给出确定性诊断（预期先全红，迁移后全绿） |
| G3 | **摘要无歧义且自洽**：Manifest 规范化规则唯一，`hashStatus` 时序闭合，不产生二次漂移 | 写入后重算 `sourcePackageHash` 与磁盘内容一致（幂等回环测试） |
| G4 | **几何零缺陷**：Scale/Rotation/Position 非有限值拦截、胶囊恒等式 `H = 2R + 2h_cyl` 绝对成立、遗留 `size` 全维度审查 | `(NaN,1,1)` 被拒；`size=(0.44,-100,900)` 被拒；规范化后恒等式成立 |
| G5 | **骨骼双锚点可控**：以 `authoredSkeletonSnapshotId` 检索 `B_old`，容差内透明重绑定，超容差或快照丢失转人工 | `RebindCandidateReport` 可产出；无"猜测式"重绑定路径 |
| G6 | **无引擎边界可证**：递归闭包扫描证明 `MxFramework.Authoring.Core` 及其传递依赖零 `UnityEngine*` / `UnityEditor*` 引用 | 扫描器对 `Core -> Helper -> UnityEngine` 二级链能报错 |
| G7 | **写入安全且幂等**：路径逃逸、符号链接（含末端文件自身）、`packageId` 注入全封堵；WritePlan 具备状态机、覆写策略、DAG 拓扑与幂等重试 | 三类攻击用例全部抛 `SecurityException`；重复执行同一 Plan 结果为 `Skipped_Identical` |

**隐含目标**：消除"同一输入得到不同结果"的分歧，使规范达到 Implementation Candidate 状态。

---

## 2. 范围

### 2.1 范围内（In Scope）

- **源侧**：`Authoring/CharacterPackages/<character-XXXXXX>/` 的目录组织、`manifest.json` / `resource_catalog.json` / `geometry/*` / `config/*` 的契约与校验。
- **编译侧（noEngine）**：`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/**` 的校验、规范化、摘要、WritePlan 生成。
- **CLI / EditorServer 侧**：`Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/**` 的命令表面、就绪判定与导入触发。
- **Unity 消费侧**：`Packages/com.vincent.wgameframework/Editor/CharacterImport/**` 的导入执行与资源目录更新。
- **数据迁移**：现有 9 个角色包的契约与几何数据对齐。

### 2.2 范围外（Out of Scope，规范未覆盖或仅引用）

- 52 项导出产物的具体生成逻辑（属主文档第 4 章）。
- HBAK 烘焙格式与内存预算（属主文档 ADR-3）。
- 调色板顶点 Alpha 寻址方案（属主文档 ADR-6）。
- Runtime 侧的受击结算、动作状态机实现。
- CharacterStudio（Web）与 Blender 侧工具的实现改造——规范只定义其**输出契约**，未定义改造工作量。

### 2.3 边界约束（项目不变量，来自根 `AGENTS.md`）

- `MxFramework.Authoring.Core` 必须保持 **netstandard2.1 + 零 Unity 引用**。实测当前 `MxFramework.Authoring.Core.csproj` 仅引用 `System.Text.Json 8.0.5`，并通过 `Compile Include` 链接 `Packages/com.vincent.wgameframework/Editor/ResourceMapping/Core/**`（该目录已验证无 `using UnityEngine`）——**边界当前成立，新增代码不得破坏**。
- Authoring 工具链与 Authoring AI Assist 独立于 Unity Editor。
- Unity 序列化资产的写入权威是 Unity Editor / Unity MCP / 仓库生成器。

---

## 3. 涉及模块与改动点

### 3.1 模块地图

| 层 | 路径 | 本次角色 |
| :--- | :--- | :--- |
| 源数据 | `Authoring/CharacterPackages/character-0000XX/**` | 契约与数据迁移对象（9 个包） |
| 契约 DTO | `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterPackageManifest.cs` | manifest 模型改造 |
| 契约 DTO | `.../CharacterResourcePackageSchemas.cs` | 字段描述表（现有）与新 Schema 对齐 |
| 契约 DTO | `.../CharacterAuthoringGeometry.cs` | 碰撞体 DTO / 形状枚举改造 |
| 编译核心 | `.../CharacterAuthoringCompiler.cs`（4990 行） | 摘要、WritePlan、校验编排的主战场 |
| 校验 | `.../CharacterPackageValidation.cs`（1470 行）+ 4 个分部文件 | 门禁落点 |
| 迁移 | `.../CharacterResourcePackageMigration.cs`（1227 行） | 版本迁移入口 |
| 资源目录 | `.../CharacterPackageResourceCatalog.cs`、`CharacterResourceMappingV2Producer.cs` | catalog 契约对齐 |
| 导入桥 | `.../CharacterUnityImportBridge.cs` | WritePlan/哈希的跨进程契约 |
| CLI | `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs`、`EditorServer.Readiness.cs`、`EditorServer.CharacterUnityImportEditor.cs` | 哈希比对、就绪判定、导入执行 |
| Unity 侧 | `Packages/com.vincent.wgameframework/Editor/CharacterImport/**` | 导入落盘与 catalog 更新 |
| 测试 | `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.*.cs` | 门禁与幂等回归 |

### 3.2 改动点清单

| # | 改动点 | 类型 | 规范依据 | 现状 |
| :--- | :--- | :--- | :--- | :--- |
| C1 | `CharacterPackageManifest` 新增 `AssetEntries` / `FeatureFlags` / `Hashes.HashStatus`，移除或迁移 `ConfigFiles` / `AuthoringOnlyFiles` | 破坏性 | §2.1 | `ConfigFiles`/`AuthoringOnlyFiles` 为现有字段 |
| C2 | `packageSchemaVersion` 语义切换至 `2.4.0` 单值枚举 | 破坏性 | §2.1 | 实测数据为 `8.0` |
| C3 | 新增四份 Draft-07 Schema 资源与校验器 | 新增 | §1.2/1.4/1.5/2.1 | 无 JSON Schema 文件，仅有字段描述表 |
| C4 | 新增中立骨架提取器与 `skeleton_hierarchy.json` 生产/版本锁定 | 新增 | §1.2 | 完全不存在；无 `HumanoidBone` 类型 |
| C5 | 重写 `sourcePackageHash` 计算为文件级二进制闭包协议 | 破坏性 | §1.3 | 现为 `BuildSourcePackageCanonicalText` 语义文本哈希 |
| C6 | 几何校验与规范化：非有限值拦截、胶囊恒等式、遗留 `size` 全维度 | 增强 | §3.2 | 现有校验器无这些检查 |
| C7 | `CharacterColliderShape` 处理策略：`Convex`/`CustomMesh`/`Reserved*` 的合法性裁决 | 待定 | §3.2 `default: throw` | 现有枚举含这些值 |
| C8 | `body_colliders.json` 新增 `authoredSkeletonSnapshotId` 与快照仓库检索 | 新增 | §4.1 | 0 命中 |
| C9 | 伤害倍率区间与溢出断言 | 增强 | §5.1 | 现有数据 `1.35`/`0.9`/`1.0` 在区间内 |
| C10 | 递归程序集依赖闭包扫描器（`PEReader`） | 新增 | §6.1 | 0 命中；csproj 需新增 `System.Reflection.Metadata` |
| C11 | 路径沙箱：`packageId` 正则、目录边界前缀比对、末端文件+祖先目录符号链接检查 | 新增 | §6.2 | 0 命中 |
| C12 | `CharacterUnityImportWritePlan` 重构为状态机 + Operations + 覆写策略 + DAG | 破坏性 | §6.3 | 现有 DTO 为 `Writes[]` 扁平结构，被 3 处消费 |
| C13 | WritePlan 幂等执行器（`Skipped_Identical`、`E-PLAN-401`、Kahn 拓扑、`E-PLAN-501`） | 新增 | §6.3 | 无执行器，仅有 `WritePolicy="Recreate"` 字段 |
| C14 | 门禁码字典补齐（`E-SRC-102~108` 等）与诊断映射 | 新增 | §7.1 | 字典缺项 |
| C15 | 9 个现有包的数据迁移 | 数据 | 全篇 | 见 §7 风险 R1/R2 |

---

## 4. 关键约束与技术要点

### 4.1 摘要协议（§1.3）——最容易做错的点

```
FilePaths = 展开集合(manifest.assetEntries, featureFlags)   // 未声明文件一律排除
ManifestDigest = SHA256( CanonicalJson( manifest
      - hashes.sourcePackageHash        // 删除键值对，不得保留 null
      - hashes.hashStatus := "Valid"    // 强制覆写
      , 属性字典序 Ordinal, 紧凑 UTF-8 ) )
Entries = [ "manifest.json:" + ManifestDigest ]
        ∪ { lower(path) + ":" + SHA256(file bytes) | path ∈ FilePaths \ manifest.json }
sourcePackageHash = "sha256:" + SHA256( UTF8( join( sort(Entries, Ordinal), "\n" ) + "\n" ) )
```

要点：
1. **删除 vs 置 null** 是字节级差异，必须删除键值对。
2. `hashStatus` 在求值前固定为 `"Valid"`，写入后状态一致——这是消除时序回环的关键。
3. Manifest 只作为条目列表**首项参与一次**拼接，不得重复计入。
4. 路径统一小写 + 正斜杠；排序用 `Ordinal`（不能用 `InvariantCulture`）。
5. 末尾必须带 `\n`。

### 4.2 几何契约（§3.2）——语义源冲突

- 阈值：全长 `[0.005, 10.0]` m；半径 `[0.0025, 5.0]` m；遗留 `size` 容差 `10 mm`。
- 胶囊硬约束：`H >= 2R`（**无容差**，`H == 2R` 时圆柱段长为 0）。
- 规范化恒等式：`totalHeight ≡ 2R + 2*h_cylHalf`。
- **冲突点**：本规范以 `radius`/`height` 为权威、`size` 仅作遗留比对；但主文档 §3.2 明确以 `dimensions(x,y,z)` 全长为权威、`R = 0.5*x`、`h_half = max(0, 0.5*H - R)`。两套权威来源必须二选一并写死，否则 `E-GEO-303` 的判定基准不唯一。
- **姿态校验顺序**：先 `float.IsFinite` 全分量检查，再容差比对——顺序颠倒就是 NaN 穿透漏洞。

### 4.3 骨骼双锚点（§4.1）

- 公式：`P_new = B_new⁻¹ · B_old · P_old`。
- 容差：`Δ_B <= 1e-4` 自动透明重绑定；否则中断并输出 `RebindCandidateReport` 转人工。
- 待明确：`Δ_B` 的度量方式（逐元素最大绝对偏差 / 矩阵范数 / 位置+旋转分离）、比较对象（同名骨骼配对规则）、快照仓库的存储与检索协议。

### 4.4 写入安全（§6.1/6.2）

- 递归闭包：`visited` 集合 + 队列，前缀匹配 `UnityEngine*` / `UnityEditor*`，跳过 `System.*` / `Microsoft.*` / `mscorlib`。
- 路径：`packageId` 必须匹配 `^[0-9]{6}$`；授权根与目标路径**双向**追加目录分隔符后做 `Ordinal` 前缀比对（防 `/root/0000011` 误判）。
- 符号链接：**先查末端文件自身 `LinkTarget`，再逐级向上查祖先目录**，任一命中即抛异常。

### 4.5 WritePlan 执行语义（§6.3）

| 操作 | 幂等规则 |
| :--- | :--- |
| `CreateFile` | 不存在 → 写入并校验 `PayloadSha256`；已存在且 SHA 一致 → `Skipped_Identical`；已存在且不一致 → `FailIfExists` 抛 `E-PLAN-401`，`OverwriteAllowed` 则覆写 |
| `UpdateFile` | 目标必须存在且当前 SHA == `PreconditionExpectedSha256`；若当前 SHA == `PayloadSha256` 视为幂等完成 |
| `DeleteFile` | 目标不存在时幂等成功；存在且匹配前置摘要时删除 |
| DAG | Kahn 拓扑排序；有环抛 `E-PLAN-501`；无依赖节点按 `OperationId` 字典序稳定执行 |

状态机：`ReadyToApply` / `VerificationPending`（有待决项，禁止正式发布）/ `Failed`。

### 4.6 门禁严重性（§7.1）

- `Fatal` → `BlockAll`：`E-SRC-101`、`E-SRC-109`、`E-SEC-701`、`E-PLAN-501`
- `Error` → `BlockExport`：`E-GEO-301/302/303`、`E-BONE-202`、`E-SKEL-101`、`E-SRC-201`、`E-CAT-201`、`E-SLOT-301`
- `Error` → `BlockSpawn`：`E-BONE-201`、`E-DMG-401`
- `Error` → `BlockImport`：`E-PLAN-401`

---

## 5. 实施步骤

### 阶段 P0：契约冻结与前置裁决（必须先做，否则后续全部返工）

1. **裁决 manifest 版本号三方冲突**（本规范 `2.4.0` / 主文档 `2.1.0` / 实测 `8.0`），确定唯一版本号与迁移映射表。
2. **裁决几何权威来源**：`dimensions` vs `size` vs `radius`/`height`（§4.2 冲突点）。
3. **裁决 `resource_catalog.json` 形态**：是改造 §1.4 新 Schema 去适配现有 ResourceMapping V2，还是新建独立文件。
4. **裁决 `assetEntries` 与现有 `configFiles`/`authoringOnlyFiles` 的映射关系**（含 `previews/thumbnail` 是否入闭包）。
5. 输出一份《契约裁决记录》，作为后续所有编码的唯一依据。

### 阶段 P1：Schema 与模型层

6. 落地四份 Draft-07 Schema 文件（manifest / catalog / weapon_slots / skeleton_hierarchy），修正 §1.4 catalog 正则以覆盖 `previews/`。
7. 改造 DTO：`CharacterPackageManifest`（+`AssetEntries`/`FeatureFlags`/`HashStatus`）、几何 DTO、`CharacterPackageHashSet`。
8. 补齐门禁码字典（`E-SRC-102~108`、`E-CAT-201`、`E-SLOT-301` 等）并建立"码 → 阶段 → 严重性 → 目标操作"单一映射表。
9. 新增语义门禁：`resourceKey` 唯一、`slotId` 唯一、扩展名与 `contentType` 强绑定、`weapon_slots.attachSocketId` 存在性。

### 阶段 P2：中立骨架链路（阻塞项，优先级最高）

10. 定义 `HumanoidBone` 语义枚举（现有枚举仅 8 值，需覆盖下肢/手/脚等）。
11. 实现中立骨架提取器，产出 `geometry/skeleton_hierarchy.json`（含 `sourceMeshSha256`、`bindPoseWorldMatrix`）。
12. 建立 `sourceMeshSha256` 与 `resources/models/*.fbx` 的哈希绑定（支撑 `E-SKEL-101`）。
13. 定义快照仓库（存储位置、命名、检索 API、生命周期），支撑 §4.1 的 `B_old` 检索。

### 阶段 P3：摘要协议切换

14. 实现规范化 Manifest 序列化器（属性 `Ordinal` 升序、紧凑 UTF-8、删键而非置 null）。
15. 实现展开集合算法与有序摘要清单构建，替换 `BuildSourcePackageCanonicalText`。
16. 明确 `resourceHash` / `generatedConfigHash` / `GeometryBindingHash` / `ResourceMappingHash` / `WritePlanHash` 在新协议下的存废与规范化规则。
17. 同步更新所有哈希比对点：`EditorServer.Readiness.cs`、`EditorServer.CharacterUnityImportEditor.cs`、导入凭据（`CharacterUnityEditorImportReceipt`）。

### 阶段 P4：几何校验与规范化

18. 落地 `ValidatePose`（非有限值前置检查 → 容差比对）与 `ValidateRawCollider` 全维度检查。
19. 落地 `NormalizeValidCollider` 与胶囊恒等式保证。
20. 将遗留 `size` 检查正式接入主校验流程。
21. 裁决 `Convex`/`CustomMesh`/`Reserved*` 形状的处理策略。

### 阶段 P5：安全沙箱与程序集闭包

22. 为 `MxFramework.Authoring.Core.csproj` 增加 `System.Reflection.Metadata` 引用，实现递归闭包扫描器。
23. 修正候选程序集解析策略（不能只按 `refName + ".dll"` 同目录查找，否则漏判即静默放行）。
24. 实现 `SecurePathSandboxEnforcer`（`packageId` 正则 + 双向分隔符前缀比对 + 末端文件与祖先目录符号链接检查）。
25. 增加攻击用例回归（`../outside`、绝对路径、末端 symlink、祖先 symlink、`packageId="../../x"`）。

### 阶段 P6：WritePlan 重构与执行器

26. 重构 DTO（`PlanVersion`/`Status`/`PendingChecks`/`Operations[]`），同步改造 3 处消费方（Core Bridge、CLI、Unity 导入）。
27. 确定 `PayloadBytes` 的序列化形态（内联 base64 vs 外置 payload 引用 + `PayloadSha256` 校验）。
28. 实现幂等执行器与 Kahn 拓扑排序。
29. 定义 `PendingChecks` 与 `Status` 的联动规则（有待决项 → 必须 `VerificationPending`）。

### 阶段 P7：数据迁移与回归

30. 迁移 9 个现有包：manifest 结构、catalog 形态、几何 `size` 对齐、`authoredSkeletonSnapshotId` 补写。
31. 全链路回归：Schema 校验 → 摘要回环幂等 → 几何门禁 → 攻击用例 → WritePlan 幂等重试。
32. 按项目默认验证执行 Unity `refresh/compile` + Console error 检查（`validate_unity_scripts`）。

---

## 6. 依赖关系

```
P0 契约裁决 ──┬─> P1 Schema/模型 ──┬─> P3 摘要协议 ──> P7 迁移与回归
              │                    │
              ├─> P2 中立骨架 ─────┤  （E-SRC-109 阻塞全链路，必须早于 P7）
              │                    │
              └─> P4 几何校验 ─────┘
                                   │
P5 安全沙箱（独立） ────────────────┴─> P6 WritePlan ──> P7
```

| 依赖 | 说明 | 阻塞强度 |
| :--- | :--- | :--- |
| P2 → 全链路 | `skeleton_hierarchy.json` 缺失即 `E-SRC-109` Fatal/BlockAll | **硬阻塞** |
| P0 → 全部 | 版本号/几何权威/catalog 形态未定则所有实现都是猜测 | **硬阻塞** |
| P1 → P3 | 摘要协议的 Manifest 规范化依赖字段集合冻结 | 强 |
| P3 → P6 | `PayloadSha256` / `PreconditionExpectedSha256` 语义依赖新摘要 | 强 |
| P2 → P4 | `E-BONE-201/202` 骨骼语义校验依赖中立骨架的 Humanoid 标注 | 中 |
| P5 → P6 | 执行器落盘前必须过沙箱 | 中 |
| 外部依赖 | CharacterStudio(Web) / Blender 提取器需产出新契约 | 中（跨工具协同） |
| 外部依赖 | `package_cache/dependency_lock.json` 尚不存在，§7.2 重建前置条件第 2 条不可满足 | 中 |

---

## 7. 风险清单

### R1（P0）几何门禁将直接阻断现有数据

实测 `Authoring/CharacterPackages/character-000001/geometry/body_colliders.json`：

| 碰撞体 | `size.y` | `height` | 偏差 | 新门禁 |
| :--- | ---: | ---: | ---: | :--- |
| `torso_capsule` | 0.56 | 0.699276 | **0.139 m** | `E-GEO-303` 触发（阈值 10 mm） |
| `thigh_l/r_capsule` | 0.50 | 0.669706 | **0.170 m** | `E-GEO-303` 触发 |
| `shin_l/r_capsule` | 0.48 | 0.690882 | **0.211 m** | `E-GEO-303` 触发 |
| `upper_arm_l/r`、`forearm_l/r` | 0.46 / 0.42 | 0.46 / 0.42 | 0 | 通过 |
| `head_sphere` | 0.34 | 2R = 0.34 | 0 | 通过 |

→ "将遗留 `size` 检查正式接入主校验流程"会**立刻阻断至少 6 个碰撞体**。必须先做数据迁移或明确 `size` 废弃策略。注意规范给出的反例（0.44 vs 0.43995）比实际数据小两个数量级，说明规范作者未核对真实数据。

### R2（P0）`additionalProperties: false` 使全部现有包 Schema 不合法

现有 manifest 含规范未声明的属性：`sourceSchemaVersion`、`authoringSchemaVersion`、`configFiles`、`authoringOnlyFiles`、`hashes.resourceHash`、`hashes.generatedConfigHash`、`coordinateConvention.roundTripPositionTolerance`、`coordinateConvention.roundTripRotationToleranceDegrees`。在 `additionalProperties: false` 下全部触发 `E-SRC-101`（Fatal/BlockAll）。

### R3（P0）`resource_catalog.json` 契约互斥

- 实际文件是 ResourceMapping V2 文档：per-entry `packageId`、`localId`、`stableId`、`typeId`、`variant`、`usage`、`sourceFormat`、`hashes{contentHash,importHash,dependencyHash}`、`importHints`；**无顶层 `packageId`、无 `contentType`、无 `sizeBytes`**。
- 实测 9 个包共 47 条 entry，其中 **16 条使用 `previews/` 前缀**，被 §1.4 的 `^resources/(models|animations|textures)/...` 正则直接拒绝；而 §1.1 第 20 项又把 `previews/thumbnail.png` 注册为合法输入。规范内部自相矛盾。

### R4（P1）Manifest 规范化未覆盖非规范哈希字段

实测 `hashes.resourceHash` 存在占位值 `"sha256:pending-c0"`（非合法哈希）。§1.3 只规定删除 `sourcePackageHash`、覆写 `hashStatus`，未处理 `resourceHash`/`generatedConfigHash`。若这些字段保留在 Manifest 摘要中，会出现"哈希输入里含占位字符串"的非确定性来源。

### R5（P1）摘要协议切换会使全部已导入角色判定为 stale

现有 `SourcePackageHash` 由语义文本拼接产生，且伴随 `GeneratedConfigHash`/`GeometryBindingHash`/`ResourceMappingHash`/`WritePlanHash` 四个衍生哈希。切换协议后，`EditorServer.Readiness.cs` 与 `EditorServer.CharacterUnityImportEditor.cs` 中的哈希比对必然全部失配，需设计一次性重算/重新导入策略。

### R6（P1）WritePlan 重构是破坏性公共契约变更

`CharacterUnityImportWritePlan` / `CharacterUnityImportWriteEntry` 当前被 3 处消费：`CharacterAuthoringCompiler.cs`、`CharacterUnityImportBridge.cs`、`CharacterPackageCommands.cs`。规范提出的新 DTO 字段完全不同（`Operations[]` / `OverwritePolicy` / `PreconditionExpectedSha256` / `DependsOnOperations`），且 `PayloadBytes` 为 `byte[]`——嵌入 JSON 会导致体积膨胀，并与现有 `BuildWritePlanCanonicalText` 的文本化哈希方式冲突。需先定 payload 承载形态。

### R7（P1）程序集扫描器的静默漏判

§6.1 用 `Path.Combine(Path.GetDirectoryName(currentPath), refName + ".dll")` 解析传递依赖，**找不到就跳过**。在 NuGet 包缓存 / Unity `Library/ScriptAssemblies` / `obj` 混合布局下，大量传递依赖无法按此规则定位，扫描器会"通过"但未真正证明零引擎依赖——安全语义不闭合。同时需为 `netstandard2.1` 项目新增 `System.Reflection.Metadata` 包引用。

### R8（P2）骨骼语义与快照仓库无载体

`HumanoidBone` 类型在仓库中不存在；§1.2 的 `humanoidSemantic` 枚举仅 8 个值（`None/Hips/Spine/Chest/Neck/Head/LeftUpperArm/RightUpperArm`），缺少下肢、手、脚等，无法支撑 `E-BONE-201` 的"Humanoid 语义备用锚点"与 `E-BONE-202` 的冲突检测。快照仓库（`B_old` 检索）完全未定义。

### R9（P2）伤害区域配置缺失

`character-000001/config/` 下**无 `damage_zones.json`**，但 `body_colliders.json` 的每个碰撞体都声明了 `hitZoneId`（`hit.torso` / `hit.head` / `hit.left_arm` …）。一旦 `enableDamageZones` 落地为 true，`E-DMG-401`（命中区 ID 悬空）会阻断生成。

### R10（P2）受控重建前置条件当前不可满足

§7.2 要求 `package_cache/dependency_lock.json` 存在且版本/哈希一致，但全仓 0 命中。同时"基于 UUID v5 的 GUID/LocalFileIdentifier 派生算法"与"ModelImporter 预设参数矩阵"是否已实现并固定，需要核对 `CharacterContentIdentityCatalog` 相关实现后才能确认。

### R11（P3）文档内部一致性风险

整改矩阵的章节引用与实际章节错位（详见 §8），若实施者按矩阵索引入口会找不到内容，存在按错误章节实施的风险。

---

## 8. 现有文档不清晰或待确认之处

### 8.1 章节引用错位（可直接修正）

| 整改矩阵指向 | 实际位置 | 说明 |
| :--- | :--- | :--- |
| 第 **4.2** 节（骨骼基线） | 只有 §4.1 | §4.2 不存在 |
| 第 **3.3** 节（遗留 size 检查） | §3.2 | 代码在 §3.2 |
| 第 **6.4** 节（WritePlan DTO） | §6.3 | §6.4 不存在 |
| 第 **6.3** 节（符号链接、packageId） | §6.2 | 两项内容都在 §6.2；§6.3 是 WritePlan |

同时 §6.1 标题为"递归程序集依赖闭包扫描器实现"、§6.2 为"彻底消除路径逃逸与符号链接漏洞的安全沙箱"，但整改矩阵把这两项都标为 §6.3。

### 8.2 门禁码字典缺项与跨文档冲突

- §1.1 引用 `E-SRC-102` ~ `E-SRC-108`（7 个码），§7.1 字典中**全部未定义**。
- `E-SRC-104` 在两份文档中语义冲突：主文档 §3.1 定义为"clothConfig 缺失"，本规范 §1.1 定义为"`body_parts.json` 缺失"。
- §7.1 中 `E-SRC-201` 的 Phase 标注为 `Snapshot`，与其余 Preflight 项不一致，且规范未定义 Snapshot 阶段的执行时机与 Preflight 的边界。

### 8.3 需明确裁决的问题（阻塞实施）

1. **manifest 版本号**：`2.4.0` / `2.1.0` / 实测 `8.0` 三方冲突，谁是真源？迁移映射如何？
2. **几何权威来源**：`dimensions(x,y,z)` 全长 / `size` / `radius`+`height` 三者谁是权威？`E-GEO-303` 的基准是什么？
3. **`resource_catalog.json` 形态**：改造 §1.4 新 Schema 以适配现有 ResourceMapping V2，还是新增独立文件？`previews/` 条目如何处置？
4. **`assetEntries` 与 `configFiles`/`authoringOnlyFiles` 的映射**：`authoringOnlyFiles` 中的 `geometry/*.json` 是否进入输入闭包与哈希？
5. **`previews/thumbnail.png` 是否参与摘要**：§1.1 注册为输入，但 §1.3 的展开步骤 1–5 从未提及 previews，且 §2.1 manifest 无对应字段 → 缩略图变更不会改变 `sourcePackageHash`，属"静默漂移"缺口。
6. **非规范哈希字段的存废**：`resourceHash`/`generatedConfigHash`/`GeometryBindingHash`/`ResourceMappingHash`/`WritePlanHash` 是否纳入新摘要体系？`"sha256:pending-c0"` 占位值如何规范化？
7. **`Convex`/`CustomMesh`/`Reserved*` 形状**：§3.2 的 `default: throw` 会拒绝现有枚举值，是废弃还是显式禁用？
8. **`Δ_B` 的度量与比较对象**：逐元素最大绝对偏差还是矩阵范数？同名骨骼如何配对？"自动重绑定 `P_new = P_old`"与公式 `B_new⁻¹B_old P_old` 的等价条件是什么？
9. **快照仓库协议**：存储位置、命名、检索 API、生命周期、GC 策略均未定义。
10. **`PayloadBytes` 承载形态**：内联 base64、外置 payload 目录、还是仅存引用 + `PayloadSha256`？现有 `BuildWritePlanCanonicalText` 如何适配？
11. **`PendingChecks` 的触发源**：哪些校验失败会置 `VerificationPending` 而非 `Failed`？§6.3 只举例"语义重绑定"。
12. **摘要切换的兼容策略**：是否提供双哈希并存过渡期？已导入角色的 stale 判定如何避免全量重导？
13. **`HumanoidBone` 完整枚举**：§1.2 只列 8 值，是否需要覆盖全身语义？与 Unity HumanBodyBones 的映射关系？
14. **`authoredSkeletonSnapshotId` 的来源**：由谁写入？写 `sha256:` 前缀的骨架快照哈希还是快照仓库的逻辑 ID？§4.1 示例用 `sha256:7a9c8b...`，但 §1.2 骨架文档内没有自哈希字段。
15. **`unitScaleMeters` 的 `const: 1.0`**：现有 `coordinateConvention` 还带两个 round-trip 容差字段，且主文档 §3.3 定义双轨坐标 Profile（A/B），manifest 中却无 Profile 字段 → 坐标系 Profile 如何声明？

### 8.4 表述不够精确之处

- §1.1 步骤 2"必需项直接加入集合"——必需项清单是否包含 `previews/thumbnail.png`（第 20 项为选填）与 `resource_catalog.json`（第 2 项为必需）？展开算法未显式列出。
- §1.1 第 17 项示例路径 `resources/models/000001_lod0.fbx` 硬编码了 packageId，与 manifest 的通用正则不一致。
- §3.2 `NormalizeValidCollider` 未对 `collider.Size == null` 的 Box 分支做空值防护（`collider.Size!` 空断言），若规范化在校验之前被调用会 NRE；规范未声明"必须先校验后规范化"的调用契约。
- §6.3 `CreateFile` 的 `Skipped_Identical` 与 `FailIfExists` 的判定顺序正确，但未说明 `UpdateFile` 目标缺失时的错误码（只定义了 `E-PLAN-401`）。
- §7.2 第 3 条"Prefab 内部子对象名称匹配稳定"是前提还是待验证项，表述含糊。
- 结语称"已排除全部缺陷、进入 Implementation Candidate"，但未列出与现有实现/数据的兼容性论证，与 §7 风险中实测到的冲突不符。

---

## 9. 建议的落地顺序与验收要点

| 顺序 | 工作项 | 验收要点 |
| :---: | :--- | :--- |
| 1 | 输出《契约裁决记录》（§8.3 的 15 项 + §8.1 章节修正） | 每项有唯一结论与理由，无"待定" |
| 2 | 四份 Schema + DTO 改造 | Schema 校验器可运行；诊断含码/路径/对象路径/字段/建议修复 |
| 3 | 中立骨架链路 | `skeleton_hierarchy.json` 可生成；`E-SKEL-101` 可触发 |
| 4 | 摘要协议 + 哈希比对点同步 | 写入后重算哈希与磁盘一致（幂等回环）；`Readiness` 判定语义明确 |
| 5 | 几何校验与规范化 | `(NaN,1,1)` 被拒；`H<2R` 被拒；`size` 全维度偏差 > 10 mm 被拒 |
| 6 | 安全沙箱 + 程序集闭包 | 5 类攻击用例全部抛异常；`Core -> Helper -> UnityEngine` 二级链被拦截 |
| 7 | WritePlan 重构 + 执行器 | 重复执行结果为 `Skipped_Identical`；环依赖抛 `E-PLAN-501` |
| 8 | 9 个包数据迁移 | 全部包 Schema 校验通过；几何门禁全绿 |
| 9 | 全链路回归 | 按 `Docs/QUALITY_GATE.md` 与项目默认验证（Unity `refresh/compile` + Console 零 error） |

**关键提醒**：第 1 步不可跳过。规范 §8.3 的 15 项裁决中有 6 项是硬阻塞（版本号、几何权威、catalog 形态、assetEntries 映射、非规范哈希字段、摘要兼容策略），在裁决前开始编码会产生系统性返工。

---

## 10. 实施进展（2026-09-17）

按上述"可独立实施 / 需先裁决"的划分执行。**不依赖任何 P0 裁决**的五个模块已落地并通过回归测试；依赖裁决的契约替换部分未动。

### 10.1 已完成（新增，全部为增量，未改动任何既有契约）

| 规范章节 | 产出 | 位置 |
| :--- | :--- | :--- |
| §7.1 | 门禁字典 `CharacterGateCodes`（22 条定义，含补齐的 `E-SRC-102~108`）+ `CharacterDiagnosticException` | `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/SourceContract/` |
| §3.1 | `CharacterAuthoringPose`（可空 `EulerHint`）、`CharacterHumanoidBone` | 同上 |
| §3.2 | `RawColliderDto`、`CanonicalColliderGeometry`、`CharacterColliderValidatorAndNormalizer`、`CharacterColliderSourceAdapter` | 同上 |
| §1.1 | `CharacterSourcePackageClosureResolver`（展开集合 + 草稿隔离审计） | 同上 |
| §1.3 | `CharacterSourcePackageCanonicalDigest`（规范化 Manifest、有序摘要清单、回环校验） | 同上 |
| §6.1 | `RecursiveAssemblySecurityValidator`（递归闭包 + 依赖链 + 未解析引用失败关闭） | 同上 |
| §6.2 | `SecurePathSandboxEnforcer`（packageId 强正则、边界对齐前缀比对、末端文件与祖先目录链接检查） | 同上 |
| §6.3 | `CharacterWritePlan` / `CharacterWritePlanOperation` / `CharacterWritePlanExecutor`（状态机 + Kahn 拓扑 + 幂等执行） | 同上 |
| §1.2/§1.4/§1.5/§2.1 | 四份 Draft-07 JSON Schema | `Docs/Validation/CHARACTER_*.schema.json` |
| 回归 | `CharacterSourceContractTests`（1026 行）并接入 `Program.cs` 的 `character-source-contract` 入口与聚合套件 | `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/` |

改动既有文件仅两处：`MxFramework.Authoring.Core.csproj` 新增 `System.Reflection.Metadata 8.0.0` 包引用（§6.1 需要 `PEReader`），以及 `Program.cs` 注册测试入口。

### 10.2 实施中新发现的规范缺陷（已在代码中绕开并标注）

| # | 缺陷 | 处置 |
| :--- | :--- | :--- |
| N1 | §3.2 参考代码使用 `UnityEngine.Vector3`，违反 §6.1 自己要检查的 noEngine 边界 | 改用 `CharacterAuthoringVector3` |
| N2 | §6.2 参考代码使用 `FileInfo.LinkTarget`，该 API 为 .NET 6+，在 `netstandard2.1` 下**无法编译** | 改用 `FileAttributes.ReparsePoint`（netstandard2.0 起可用，且额外覆盖 Windows 目录联接） |
| N3 | §3.2 称胶囊恒等式"100% 绝对成立"。float32 下实测**不成立**：40 万组随机采样中 5.19% 不是比特级相等，最大偏差 1.19e-7 m | 引入 `CapsuleIdentityTolerance = 1e-6 m` 判定；仓库现有 6 组真实数据偏差均为 0 |
| N4 | §6.2 用 `StartsWith(root.FullName)` 判断祖先关系，会把 `...\0000011` 误判为 `...\000001` 的后代 | 改为分隔符对齐的边界比较，并加入回归用例 |
| N5 | §3.2 的 `NormalizeValidCollider` 用 `collider.Size!` 空断言，先规范化后校验会 NRE | 改为显式诊断异常，并固定"先校验后规范化"的调用契约 |
| N6 | §6.1 按 `refName + ".dll"` 同目录解析传递依赖，找不到即静默跳过 | 改为多探测目录解析 + 未解析引用显式记录，严格模式默认阻断 |
| N7 | 规范未定义 `UpdateFile` 目标缺失时的错误码 | 暂用 `E-PLAN-401` 并在代码注释中标注 |
| N8 | 规范未定义"可选条目 → featureFlag"的对应表（`paletteBinding` 等在 `featureFlags` 中无对应开关） | `CharacterSourcePackageClosureResolver` 要求调用方显式提供映射，不猜测 |
| N9 | 规范未固定 Canonical JSON 的字符串转义策略与数字格式，跨实现字节级一致性无保证 | 显式固定为 `UnsafeRelaxedJsonEscaping` + 保留源数字字面量，并在注释中标注待确认 |

### 10.3 实测数据（供裁决参考）

- **catalog 正则冲突量化**：规范 §1.4 的 `relativePath` 正则拒绝真实 catalog 的 **19/47** 条 entry —— `previews/` 16 条、`resources/materials/` 3 条。后者说明真实数据存在规范 20 项注册表之外的**第 21 类**输入。
- **几何门禁冲突量化**：`character-000001/geometry/body_colliders.json` 中 6 个碰撞体的 `size.y` 与权威 `height` 偏差 0.139~0.211 m，远超 10 mm 容差。
- **递归闭包验证**：以合成程序集构造 `Consumer -> Helper -> UnityEngine.CoreModule` 三级链，扫描器在 depth 1 检出并输出完整依赖链；单层扫描会漏掉该污染。`MxFramework.Authoring.Core.dll` 自身扫描零违规、零未解析引用，noEngine 边界成立。
- **摘要回环验证**：写入 `sourcePackageHash` 并将 `hashStatus` 置为 `Valid` 后重算，摘要完全一致；未声明的草稿文件不改变摘要；路径大小写与分隔符差异不改变摘要。

### 10.4 验证方式

```
# 依赖（本机 APPDATA 缺失会导致 NuGet 报 "Value cannot be null. (Parameter 'path1')"）
env APPDATA="C:\Users\WuGC\AppData\Roaming" LOCALAPPDATA="C:\Users\WuGC\AppData\Local" \
    "ProgramFiles=C:\Program Files" "ProgramFiles(x86)=C:\Program Files (x86)" \
    dotnet build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj

# 源契约测试（仓库测试工程依赖 Unity 生成的 csproj，当前环境无法直接构建）
# 已用临时宿主 Temp/source-contract-harness 链接同一测试文件执行，全部通过。
```

### 10.5 仍未开始（依赖 §8.3 裁决）

manifest v2.4 契约迁移、`resource_catalog.json` 形态对齐、中立骨架提取器与 `skeleton_hierarchy.json` 生产、快照仓库、摘要协议切换与哈希比对点同步、骨骼双锚点重绑定、伤害区域门禁、9 个现有包的数据迁移。这些均需先完成 §8.3 的 15 项裁决，其中 6 项为硬阻塞。

---

## 11. 实施进展（第二批：中立骨架链路与剩余语义门禁）

在第一批之后继续推进。选这一批的理由：中立骨架（§1.2/§4.1）是**全新契约**，不与任何既有契约冲突，因此不需要 §8.3 的裁决即可实施；语义门禁（§1.4/§1.5）同理，只要不绑定尚未裁决的 catalog 形态。

### 11.1 已完成（增量新增，11 个源文件）

| 规范章节 | 产出 | 说明 |
| :--- | :--- | :--- |
| §4.1 数学基础 | `CharacterMatrix4x4` | noEngine 4x4：乘法、Gauss-Jordan 求逆（带部分主元）、逐元素最大绝对偏差、姿态 TRS 互转 |
| §1.2 | `CharacterSkeletonHierarchy` / `CharacterSkeletonBone` | 中立骨架 DTO |
| §1.2 / §7.1 | `CharacterSkeletonHierarchyValidator` | 结构校验 + `E-SRC-109` + `E-SKEL-101` |
| §1.2 | `CharacterSkeletonTree` | 路径/名称/语义三重索引、祖先链、环检测 |
| §7.1 | `CharacterBoneAnchorResolver` | `E-BONE-201` / `E-BONE-202` |
| §4.1 | `CharacterBoneRebindSolver` / `CharacterRebindCandidateReport` | Δ_B 判定、候选姿态、人工审查闸门 |
| §1.4 / §7.1 | `CharacterCatalogSemanticValidator` | `E-CAT-201`、扩展名与 `contentType` 强绑定、`.tga` 移除 |
| §1.5 / §7.1 | `CharacterWeaponSlotSemanticValidator` | `E-SLOT-301`、`slotId` 唯一性、武器类型枚举 |

回归测试 `CharacterSkeletonContractTests`（新增），接入 `character-skeleton-contract` 入口与聚合套件。

### 11.2 实施中新发现的问题

| # | 问题 | 处置 |
| :--- | :--- | :--- |
| N10 | §1.2 未指定 `bindPoseWorldMatrix` 的**行列主序**，而 glTF/FBX/Blender 惯例是列主序 | 显式固定为行主序 + 列向量（`m[row*4+col]`，平移在 `[0,3][1,3][2,3]`）并标注待确认；错配会表现为姿态镜像 |
| N11 | §4.1 同时给出"Δ_B ≤ 1e-4 时 P_new = P_old"与公式 `P_new = B_new⁻¹B_old P_old`，二者并非同一件事 | 调和为：Δ_B 在容差内 → 姿态不变；超容差 → 报告中给出候选姿态供人工评估，**不自动应用** |
| N12 | §7.1 未为 §1.2 的**结构违规**定义门禁码（只有"文件缺失"和"哈希不匹配"两个） | 复用 `E-SRC-109`（同为 Preflight/Fatal/BlockAll），标注待确认 |
| N13 | 骨骼名规范化：仓库既有规则只剥离 `:` 前缀，未覆盖 Blender 的 `Armature\|Spine` 形式 | 扩展为同时剥离 `:` 与 `\|` 中最后一个前缀；歧义仍返回 null，不放松唯一性 |
| N14 | 候选姿态若分解出非单位缩放（旧/新绑定矩阵含非均匀缩放或剪切），会违反 §3.2 的缩放约束 | 检出即转人工，不静默产出非单位缩放 |

### 11.3 关键实测：`E-BONE-201` 会大面积阻断真实数据

统计 9 个包的 `geometry/body_colliders.json`（共 98 条 `parentPath`）：

- **具体路径 17 条，遗留别名 `bone.<name>` 81 条（占 83%）**。
- 11 种不同别名中，规范化后能对应到规范 `HumanoidBone` 枚举的**只有 2 种**（`spine`、`head`）。
- 9 种无语义兜底：`forearm.L/R`、`thigh.L/R`、`shin.L/R`、`Dragon15_tail_05`，以及 `upper_arm.L/R`——后者尤其值得注意：它规范化后是 `upperarml`，而枚举值是 `LeftUpperArm`（**数据用 `L` 后缀，枚举用 `Left` 前缀**），因此连"上臂"这种枚举已覆盖的部位也对不上。

结论：`E-BONE-201` 是 `BlockSpawn` 级门禁。若骨架树无法解析这些别名，将有约 83% 的碰撞体骨骼引用被阻断。这要求规范**要么**大幅扩充 `HumanoidBone` 枚举（至少补齐前臂、大腿、小腿、手、脚），**要么**定义别名到语义的映射规则（含 `L/R` 后缀 ↔ `Left/Right` 前缀的换算）。当前实现严格按规范落地，因此该风险是真实存在的。

### 11.4 验证

```
Core 构建: 0 警告 0 错误      Cli 构建: 0 警告 0 错误
CharacterSourceContractTests   PASSED
CharacterSkeletonContractTests PASSED
```

测试覆盖：矩阵求逆回环与奇异矩阵、姿态互转闭合、骨架结构违规（重复路径/悬空父路径/自父/环）、`E-SRC-109`、`E-SKEL-101`、`E-BONE-201`（含歧义与规范化歧义）、`E-BONE-202`、Δ_B 三档判定、候选姿态数值正确性（纯平移与绕 Z 轴 90° 两个可解析算例）、快照丢失强制人工、`E-CAT-201`、`E-SLOT-301`。
