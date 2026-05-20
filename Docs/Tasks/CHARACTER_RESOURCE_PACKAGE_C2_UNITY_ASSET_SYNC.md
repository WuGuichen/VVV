# Character Resource Package C2：Unity 资产导入与同步系统

> 状态：草案
> 范围：CharacterStudio 源资源包、Unity 导入资产、运行时预览 Prefab、双端同步状态和冲突处理
> 交付等级：下一阶段任务
> 前置：#221 Character Resource Package C0、#222 Unity import bridge、#223 package-local resource catalog、#224 Authoring Compiler、#240-#246 CharacterStudio C1 MVP
> 资源库上游设计：`Docs/Tasks/CHARACTER_RESOURCE_LIBRARY_00_DESIGN.md`

## 背景

CharacterStudio 当前可以在浏览器侧用 Three.js 直接加载 `.glb` 并显示角色、骨骼、挂点、碰撞体和武器；Unity 侧的 `iron_vanguard_character_preview.prefab` 则依赖 Unity Editor 把同一批资源导入成可实例化的 `GameObject` / Prefab。

这暴露出一个必须在下一阶段解决的系统边界：

- CharacterStudio 能显示 `.glb`，不代表 Unity 已经能消费该 `.glb`。
- Unity Prefab 不能直接以包内源文件作为稳定运行时资源；必须引用 Unity 已导入资产。
- 角色、武器、动画、挂点、碰撞体是配置引用关系，不是“删引用就删资源”的生命周期关系。
- Unity 侧的手动调整需要能同步回 CharacterStudio，但不能任意覆盖源包权威数据。

C2 的目标是把导入和同步整理成一个可诊断、可重入、可跳过、可冲突处理的系统。

## 目标

建立三层资源链路：

```text
Character Source Package
  manifest.json / resource_catalog.json / geometry/*.json / 原始 glb|fbx|png|...
    ↓ import / convert / hash check
Unity Imported Assets
  imported_assets / generated materials / imported prefabs / unity_resource_catalog.json
    ↓ assemble / validate / preview
Runtime Preview Prefab
  character preview prefab / scene prefab / runtime spawn binding
```

C2 完成后：

- CharacterStudio 导出的资源在 Unity 中可以稳定导入、复用和更新。
- Unity 生成的角色 Prefab 只引用 Unity 可实例化资产，不直接假设 `.glb` 可作为 `GameObject` 加载。
- 导入资源重复执行时根据 hash 跳过、更新或报告冲突。
- Unity 手动编辑只回写明确的 authoring override，不直接改源包完整 JSON。
- CharacterStudio 可以显示 Unity 侧同步状态：已导入、源已变、Unity 资产缺失、导入失败、存在手动覆盖。

## 非目标

- 不把 Unity Editor 变成 CharacterStudio 的主创入口。
- 不让 Runtime 直接读取未导入的 `.mxchar` / 目录包。
- 不在 C2 做完整动画状态机、Playable Controller 或 Gameplay Controller。
- 不静默删除不再被角色引用的武器、动画或模型资源。
- 不做任意双向 JSON merge；Unity 只能输出受控 patch。

## 核心原则

- `resource_catalog.json` 是源资源库，不代表当前角色一定使用了全部资源。
- `config/character_application.json`、weapon attachment、装备配置等表达当前引用关系。
- 删除角色对武器的引用，只改变引用关系；武器资源仍由 catalog 管理。
- Unity importer 负责生成 Unity 资产和 Unity 映射，不解释玩法含义。
- Prefab builder 只消费 Unity imported asset catalog，不直接消费源 `.glb`。
- 所有导入状态必须可诊断，不允许只有 Console warning。
- 同 stable id + 同 import hash 的资源必须可跳过，保证重复导入幂等。
- 同 stable id + hash 变化必须按策略处理：更新、生成 variant、报告冲突或阻断。

## 目标产物

### 1. Unity Resource Import Catalog

新增或升级：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_resource_catalog.json
```

建议结构：

| 字段 | 说明 |
| --- | --- |
| `format` | 固定为 `mx.characterUnityResourceCatalog.v1` |
| `packageId` | 源包 ID |
| `generatedAtUtc` | 生成时间 |
| `entries[]` | Unity 资产映射条目 |

`entries[]` 字段：

| 字段 | 说明 |
| --- | --- |
| `packageResourceKey` | 源包资源 key |
| `stableId` | 长期稳定资源 ID |
| `usage` | `characterModel`、`weaponModel`、`animationClipGroup`、`previewThumbnail` 等 |
| `sourceRelativePath` | 源包内路径 |
| `sourceFormat` | `glb`、`gltf`、`fbx`、`png` 等 |
| `declaredContentHash` | catalog 声明 hash |
| `contentHash` | 本次实算 hash |
| `importHash` | 影响 Unity 导入语义的 hash |
| `unityAssetGuid` | Unity 主资产 GUID |
| `unityAssetPath` | Unity 项目内路径 |
| `unityMainObjectType` | `GameObject`、`Texture2D`、`AnimationClip` 等 |
| `importerKind` | `unity-fbx`、`gltfast`、`converted-glb`、`copy-only`、`placeholder` |
| `importStatus` | `Imported`、`Skipped`、`SourceChanged`、`UnityMissing`、`Failed`、`Conflict` |
| `diagnostics[]` | 结构化问题 |

### 2. Unity Authoring Overrides

新增：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_authoring_overrides.json
```

只允许 Unity 回写以下受控内容：

- model wrapper `position` / `rotation` / `scale`
- socket local pose
- collider local pose / shape / size
- weapon attachment local grip pose
- 当前资源引用替换
- preview prefab 生成状态和诊断

禁止 Unity 任意回写：

- `manifest.json` 包身份
- 源 `resource_catalog.json` 全量内容
- Runtime 当前状态，例如 HP、Buff、冷却、当前装备实例
- Gameplay / Combat 权威配置

CharacterStudio 打开包时应读取 override 并以 patch 形式展示，可选择接受、撤销或合并。

### 3. Unity Imported Assets 输出目录

Unity 导入产物固定放入：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/imported_assets/
  models/
  weapons/
  animations/
  textures/
  materials/
  prefabs/
```

源文件复制缓存继续放在：

```text
Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/resources/
```

区别：

- `resources/` 是源包资源镜像或缓存。
- `imported_assets/` 是 Unity 可消费资产。
- `prefabs/` 是按当前配置装配出的角色预览 / Runtime entry prefab。

## 导入策略

### GLB / glTF

C2 必须明确采用一个 Unity GLB 导入方案：

1. 首选：引入 glTFast / Unity 可用 glTF importer。
2. 备选：导入时转换为 FBX，再使用 Unity 原生 ModelImporter。
3. 兜底：生成占位体，但 `importStatus` 必须是 `Failed` 或 `Placeholder`，不能报告成功。

Prefab builder 不再执行：

```csharp
AssetDatabase.LoadAssetAtPath<GameObject>(".../*.glb")
```

而是读取 `unity_resource_catalog.json` 中 `unityAssetPath`，加载已导入的 Unity GameObject / prefab。

### FBX

FBX 可以由 Unity 原生 ModelImporter 导入。导入报告必须记录：

- scale factor
- animation import policy
- material import policy
- avatar / rig policy
- model root path
- generated prefab path

### 材质

项目使用 URP，因此生成材质默认使用：

- `Universal Render Pipeline/Lit`
- 必要时降级到 `Universal Render Pipeline/Unlit`

生成的预览材质必须是持久化 `.mat` 资产，不能只在内存中创建后保存 Prefab。

## 同步状态机

每个资源条目至少支持以下状态：

| 状态 | 含义 | 下一步 |
| --- | --- | --- |
| `Clean` | source hash、import hash 和 Unity asset 都匹配 | 跳过 |
| `SourceChanged` | 源文件或 import hint 变化 | 重新导入 |
| `UnityMissing` | catalog 有记录但 Unity asset 不存在 | 重新导入或报错 |
| `ImportFailed` | importer 执行失败 | 保留旧资产或占位体，输出 diagnostics |
| `ManualOverride` | Unity 资产被手动调整 | 生成 override patch，不静默覆盖 |
| `Conflict` | 同 stable id 但 hash / importer policy 不兼容 | 阻断或要求用户选择 |
| `OrphanedUnityAsset` | Unity 中存在但源 catalog 已删除 | 标记候选清理，不自动删除 |

重复导入规则：

```text
same stableId + same importHash + unity asset exists
  -> skip

same stableId + sourceHash changed + no manual override
  -> reimport in place

same stableId + sourceHash changed + manual override exists
  -> conflict, require user choice

source catalog no longer references resource
  -> mark orphan, do not delete automatically
```

## CharacterStudio UI 要求

CharacterStudio 应增加 Unity 同步视图：

- 资源列表显示源状态和 Unity 状态。
- 每个资源显示 source hash、Unity asset path、importer kind、last import status。
- 支持按状态筛选：失败、缺失、源已变、可跳过、冲突。
- 点击资源可看到：
  - 源文件预览
  - Unity 导入资产路径
  - 当前被哪些角色、武器、动画配置引用
  - 是否存在 Unity authoring override
- 导入按钮应明确区分：
  - 导入源资源
  - 重新生成角色 Prefab
  - 同步 Unity overrides 回 CharacterStudio

## Unity Editor 要求

Unity 侧应提供菜单或窗口：

```text
MxFramework/Character/Import Character Package...
MxFramework/Character/Reimport Last Character Package
MxFramework/Character/Rebuild Preview Prefab
MxFramework/Character/Open Import Report
MxFramework/Character/Export Authoring Overrides
```

Prefab builder 行为：

- 读取 `unity_resource_catalog.json`。
- 只加载 `unityAssetPath` 指向的 Unity asset。
- 模型缺失时显示可读占位体，并在 inspector / report 中指出对应 `packageResourceKey`。
- 生成的层级保留：
  - `ModelRoot`
  - `Sockets`
  - `AuthoringColliders`
  - `Weapons`
  - `Diagnostics`
- 挂点和碰撞体优先绑定到骨骼 / locator；无法解析时才降级到分组根节点，并输出 warning。

## 验收标准

- 在没有 GLB importer 时，导入结果不能伪装成功；必须在 `unity_resource_catalog.json` 和报告中显示 `ImportFailed` / `Placeholder`。
- 安装 GLB importer 后，`skeleton.glb` 能被导入为 Unity 可实例化资产，预览 Prefab 不再出现 `MissingModelPlaceholder`。
- 重复导入同一包不会重复生成同 stable id 资产。
- 修改 `katana.glb` 后，只重新导入 katana 相关 Unity asset，不重建无关资源。
- 从角色引用中移除武器后，武器资源仍保留在 catalog 和 Unity imported assets 中，只是当前 Prefab 不再挂载该武器。
- Unity 中调整武器 wrapper scale / rotation 后，能导出到 `unity_authoring_overrides.json`，CharacterStudio 打开后能显示该 override。
- 手动删除 Unity imported asset 后，重新导入能恢复，并报告 `UnityMissing -> Imported`。
- `git diff --check` 通过。
- Unity Editor 编译无错误。

## 建议拆分 Issue

### C2.1 Unity Resource Catalog Schema

- 定义 `unity_resource_catalog.json` DTO。
- 生成结构化 import status / diagnostics。
- 测试 hash、stable id、missing asset、orphan 标记。

### C2.2 GLB Importer Integration

- 选择并接入 glTFast 或等价 importer。
- 把 `.glb` 导成 Unity GameObject / prefab asset。
- 记录 importer kind、GUID、asset path。

### C2.3 Prefab Builder 改造

- Prefab builder 改为只消费 Unity imported asset catalog。
- 删除直接加载源 `.glb` 的逻辑。
- 失败时使用诊断占位体，并标注 resource key。

### C2.4 Import Idempotency and Conflict Policy

- 实现 `Clean` / `SourceChanged` / `UnityMissing` / `Conflict` 状态机。
- 同 hash 跳过，变更资源局部重导。
- orphan asset 只标记，不自动删除。

### C2.5 Unity Authoring Override Patch

- Unity 侧导出受控 override。
- CharacterStudio 读取并展示 override。
- 支持接受 / 撤销 / 重新应用。

### C2.6 CharacterStudio Sync Status UI

- 资源列表展示 Unity 同步状态。
- 资源详情展示 source、Unity asset、引用关系、override。
- 导入 / 重建 / 同步按钮拆分清晰。

### C2.7 End-to-End Iron Vanguard Validation

- 使用真实 `skeleton.glb`、`katana.glb`、`kite_shield.glb`。
- 从 CharacterStudio 保存，Unity 导入，生成预览 Prefab。
- 预览 Prefab 在 URP 场景中显示真实模型、武器、挂点和碰撞体。

## 风险和决策点

- GLB importer 选型会影响 package 依赖、license、Unity 版本兼容和导入 API。
- FBX 转换是否作为内置流程，需要确认是否引入外部工具链。
- Unity authoring override 的冲突合并必须保守，不能静默覆盖源包。
- 资源删除策略必须先做 orphan 标记，不直接删除用户资产。
- Prefab 只是 preview / scene entry，不等同于完整可玩角色 controller。
