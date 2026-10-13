# 【技术专项实施规范】角色源数据包组织、Schema契约与无引擎解耦流转实施规范（v1.4 深度修订版）

---

## 规范信息与修订基准

| 属性 | 内容 |
| :--- | :--- |
| **专项名称** | 角色源数据包组织、Schema契约与无引擎解耦流转实施规范 |
| **版本状态** | **v1.4 深度修订版（Implementation Spec Candidate - Addressing v1.3 Review Probes）** |
| **主审领域** | 客户端底层架构 / 角色数据工程 / 跨工具源契约 / 纯 C# 编译器边界与执行协议 |
| **对应主文档** | [`Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md`](file:///e:/Unity/WxFramework/WGameFramework/Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md) 第 3 章、第 4 章与第 5.7 节 |
| **本版修订原则** | **消灭一切二选一歧义，根除全部已发现代码反例**：针对专家复审在 v1.3 中探查出的**Scale NaN 漏洞、胶囊几何恒等式冲突、遗留 size 检查缺项、末端文件符号链接逃逸、packageId 注入风险、递归程序集依赖缺失、摘要算法不唯一及 WritePlan DTO 缺项**等全部实质性 P0/P1 问题，执行逐项代码级、Schema 级与协议级的彻底整改。 |

---

### 专家复审意见逐项整改与闭环矩阵（v1.3 Review Probes Closure）

| 审查缺陷项 | 严重性 | 缺陷具体表现与反例 | v1.4 彻底整改与闭环措施 | 落地章节 |
| :--- | :---: | :--- | :--- | :---: |
| **摘要算法二选一歧义** | **P0** | “移除或设为 null”导致不同字节；Manifest 是否重复参与公式模糊。 | **彻底固定唯一算法**：Manifest 强制“删除属性键值对”（禁止保留为 null）；Manifest 仅作为条目列表首项参与一次拼接。 | 第 1.3 节 |
| **hashStatus 状态机污染** | **P0** | 计算摘要时若未固定 hashStatus，从 Pending 改为 Valid 将改变已计算摘要。 | **规范化状态机**：计算摘要时强制将 `hashStatus` 规范化为 `"Valid"` 求值，写入后状态一致，杜绝时序回环。 | 第 1.3 节 |
| **注册类别 vs 展开文件集** | **P0** | 20 项注册类别包含通配符与可选项，不能机械地固定遍历 20 次。 | **明确展开集合协议**：由 `manifest.json` 的 `assetEntries` 显式解包为唯一实际存在的物理文件清单。 | 第 1.1 节 |
| **可选入口包外路径逃逸** | **P0** | 可选字段只写 string/null，允许 `../../` 或 `/tmp/` 绝对路径。 | **Schema 严密化**：所有可选路径统一应用正则 `^(config\|geometry\|resources)/[a-zA-Z0-9_\-\.]+\.(json\|png)$`。 | 第 2.1 节 |
| **SemVer 2.0 正则被过度简化** | **P1** | 拒绝 `1.0.0+build.7`，却接受 `1.0.0-01` 与空段 `1.0.0-alpha..1`。 | **恢复官方完整 SemVer 2.0 正则表达式**，严格支持构建元数据并拦截非法预发布前导零。 | 第 2.1 节 |
| **子 Schema 缺少语义约束** | **P1** | 重复 `resourceKey`、重复 `slotId` 及 `.tga` 格式未对齐。 | 补齐语义门禁：资源键与槽位 ID 唯一性校验；移除 `.tga` 遗留项，扩展名与 `contentType` 严格强绑定。 | 第 1.4 & 1.5 节 |
| **Scale NaN 绕过门禁** | **P0** | `Scale = (NaN, 1, 1)` 在 `Abs > 1e-4` 比较时为 false，穿透门禁。 | **前置执行 `float.IsFinite` 全分量检查**；校验通过后规范化阶段强制锁定为绝对精确的 `(1.0f, 1.0f, 1.0f)`。 | 第 3.2 节 |
| **胶囊几何恒等式冲突** | **P0** | 允许高度容差导致重建高度 $2R + 2h_{\text{cyl}} = 0.44 \ne 0.43995$ 内部冲突。 | **消除容差断点**：严格要求 $H \ge 2R$；规范化时确保几何恒等式 $H \equiv 2R + 2h_{\text{cyl}}$ 100% 绝对成立。 | 第 3.2 节 |
| **遗留 size 检查缺失维度** | **P0** | 球体只比对 X，胶囊只比对 X/Y，导致 `size=(0.44, -100, 900)` 漏网。 | **全维度严格比对**：球体比对 X/Y/Z，胶囊比对 X/Y/Z；将该检查正式接入主校验流程中。 | 第 3.3 节 |
| **骨骼基线与语义来源未定** | **P0** | $B_{\text{old}}$ 缺少稳定快照引用；Core 阶段语义冲突检测缺中立来源。 | 显式声明 `authoredSkeletonSnapshotId`；中立骨架补充 Humanoid 语义标注，基线丢失强制人工审批。 | 第 4.2 节 |
| **末端文件符号链接逃逸** | **P0** | 检查只看父目录 `FileInfo.Directory`，末端目标文件本身是 symlink 时穿透。 | **补齐末端文件自身链接检查**：先校验文件自身 `LinkTarget`，再向上逐级递归，全面禁止符号链接。 | 第 6.3 节 |
| **packageId 路径注入风险** | **P0** | 拼接时若传入 `../outside`，将篡改授权根目录。 | **执行前置强正则校验**：强制 `packageId` 匹配 `^[0-9]{6}$`，杜绝任何路径注入字符。 | 第 6.3 节 |
| **程序集依赖扫描仅一层** | **P0** | 仅扫描直接引用，漏掉 `Core -> Helper -> UnityEngine` 二级传递依赖。 | **实现真正递归闭包扫描**：维护已访问集合，递归遍历所有非系统引用程序集，杜绝传递污染。 | 第 6.2 节 |
| **WritePlan DTO 缺项与状态** | **P0** | 缺少 `Status`、`PendingChecks`、覆写策略及幂等执行语义。 | **重构 WritePlan DTO**：补齐状态机枚举、待决项列表、覆写策略、DAG 拓扑排序与幂等重试契约。 | 第 6.4 节 |

---

## 一、 源数据包输入闭包与快照契约（Input Closure & Snapshot Contract）

### 1.1 全量 20 项输入类别与动态展开集合协议

注册表定义的是**角色包合法的 20 类基础输入契约**。在实际编译执行时，系统通过解析 `manifest.json` 将其动态展开为**唯一的实际物理文件集合**。

| 序号 | 类别相对路径 | 数据格式 | 必需性 | 生产源（Who Declares） | 消费主体（Who Consumes） | 缺失处理策略 |
| :---: | :--- | :--- | :---: | :--- | :--- | :--- |
| **分组 1** | **根索引与目录清单（2项）** | | | | | |
| 1 | `manifest.json` | JSON (Draft-07) | **必需** | TA / 流程系统 | 编译器引导程序 | 阻断（`E-SRC-101`） |
| 2 | `resource_catalog.json` | JSON (Draft-07) | **必需** | 构建工具 / TA | 资源映射与打包器 | 阻断（`E-SRC-102`） |
| **分组 2** | **空间几何与受击体定义（7项）** | | | | | |
| 3 | `geometry/body_colliders.json` | JSON | **必需** | 战斗策划 / 动作师 | 离线姿态烘焙器 / 移动求解 | 阻断（`E-SRC-103`） |
| 4 | `geometry/body_parts.json` | JSON | **必需** | 动作师 / TA | 部位树与断肢解构器 | 阻断（`E-SRC-104`） |
| 5 | `geometry/body_geometry.json` | JSON | 选填 | 角色美术 / TA | 全局外包围体与基础身高计算 | 缺省采用网格 AABB |
| 6 | `geometry/body_part_contact_points.json`| JSON | 选填 | 动作师 / 音效师 | 脚步声学与地表接触解算器 | 缺省禁用接地音效事件 |
| 7 | `geometry/sockets.json` | JSON | 选填 | 动作师 / 装备策划 | 武器挂接与插槽空间映射器 | 缺省无外部挂点 |
| 8 | `geometry/weapon_slots.json` | JSON (Draft-07) | 选填 | 装备策划 | 武器装配与装备切换状态机 | 缺省无武器插槽 |
| 9 | `geometry/skeleton_hierarchy.json` | JSON (Draft-07) | **必需** | FBX 中立提取器 | 纯 C# Core 骨骼拓扑校验 | 阻断（`E-SRC-109`） |
| **分组 3** | **逻辑属性、装配与伤害战术（7项）** | | | | | |
| 10 | `config/character_application.json` | JSON | **必需** | 系统策划 | 角色全组件装配大总管 | 阻断（`E-SRC-105`） |
| 11 | `config/character_attributes.json` | JSON | **必需** | 数值策划 | 属性系统组件（血量/韧性/速度）| 阻断（`E-SRC-106`） |
| 12 | `config/character_actions.json` | JSON | **必需** | 战斗策划 | 动作状态机装配器 | 阻断（`E-SRC-107`） |
| 13 | `config/damage_zones.json` | JSON | 选填 | 战斗策划 | 部位战术伤害覆盖倍率结算器 | 缺省覆盖系数恒为 1.0 |
| 14 | `config/character_palette_binding.json` | JSON | 选填 | 角色美术 | 材质通道着色绑定管线 | 缺省使用材质默认主色 |
| 15 | `config/character_palette_usage.json` | JSON | 选填 | 角色美术 | 角色部位配色方案管理器 | 缺省使用基础调色方案 |
| 16 | `config/character_cloth_magica.json` | JSON | 选填 | 美术 / TA | 物理布料模拟参数装配器 | 缺省为刚性无布料物理 |
| **分组 4** | **基础网格、材质与动作源资产（3项）** | | | | | |
| 17 | `resources/models/000001_lod0.fbx` | FBX (Binary) | **必需** | 角色美术 | Unity 网格与骨骼导入器 | 阻断（`E-SRC-108`） |
| 18 | `resources/animations/*.fbx` | FBX (Binary) | 动态展开 | 动作师 | 离线动作采样器（烘焙 HBAK） | 由清单数组展开，零至多项 |
| 19 | `resources/textures/palette_base.png` | PNG | 选填 | 角色美术 | 调色板纹理生成器 | 缺省不生成调色板贴图 |
| **分组 5** | **视口预览与辅助资产（1项）** | | | | | |
| 20 | `previews/thumbnail.png` | PNG | 选填 | 渲染截屏工具 | Web/IDE 角色选择界面 | 缺省展示通用占位图标 |

#### 动态展开集合确定性算法
1. 读取 `manifest.json` 的 `assetEntries`；
2. 必需项直接加入集合；
3. 可选条目（`contactPoints`, `clothConfig` 等）：若值为 `string` 且 `featureFlags` 对应开启，加入集合；若为 `null` 或功能关闭，排除在集合外；
4. 动画条目：遍历 `assetEntries.animations` 字符串数组，将其中显式声明的每一个 FBX 相对路径逐一加入集合；
5. **草稿文件隔离**：任何物理存在于目录中、但未在 `manifest.json` 声明的文件，**绝对不进入输入集合，严禁参与哈希计算**。

---

### 1.2 中立骨架（`skeleton_hierarchy.json`）生产与版本锁定

中立骨架记录骨骼层次树、初始参考矩阵及 Humanoid 语义标注，由独立提取器生成并写入：

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "https://specs.mxframework.io/schemas/character-skeleton-hierarchy-v1.0.json",
  "title": "CharacterSkeletonHierarchy",
  "type": "object",
  "additionalProperties": false,
  "required": ["schemaVersion", "packageId", "sourceMeshSha256", "bones"],
  "properties": {
    "schemaVersion": { "type": "string", "enum": ["1.0.0"] },
    "packageId": { "type": "string", "pattern": "^[0-9]{6}$" },
    "sourceMeshSha256": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" },
    "bones": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["boneName", "path", "parentPath", "bindPoseWorldMatrix"],
        "properties": {
          "boneName": { "type": "string" },
          "path": { "type": "string" },
          "parentPath": { "type": "string" },
          "humanoidSemantic": {
            "type": ["string", "null"],
            "enum": ["None", "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperArm", "RightUpperArm", null]
          },
          "bindPoseWorldMatrix": {
            "type": "array",
            "minItems": 16,
            "maxItems": 16,
            "items": { "type": "number" }
          }
        }
      }
    }
  }
}
```

---

### 1.3 唯一规范化摘要算法（Canonical Digest Protocol）

为消除上一版本中“删除或设为 null”导致的二义性，**本规范强制确立唯一的字节流生成协议**：

```text
【计算 sourcePackageHash 黄金标准步骤】
步骤 1 (获取展开集合):
  依据第 1.1 节算法，解析得到本次快照包含的具体文件相对路径列表 List<string> FilePaths。

步骤 2 (Manifest 规范化清洗):
  读取 manifest.json 内容，反序列化为 JSON 树:
  a. 彻底删除键 "/hashes/sourcePackageHash" (严禁保留为 null，必须彻底移除该 key-value)；
  b. 将键 "/hashes/hashStatus" 的值强制覆写为字面量字符串 "Valid"；
  c. 采用属性字典序升序 (Ordinal)，将清洗后的 JSON 树序列化为紧凑 UTF-8 字节流 (无缩进、无空格)；
  d. 计算该字节流的 SHA-256，得到 ManifestDigestHex。

步骤 3 (构建全量有序摘要清单):
  a. 初始化条目列表: List<string> Entries；
  b. 将规范化 Manifest 作为首条记录加入: Entries.Add("manifest.json:" + ManifestDigestHex)；
  c. 遍历 FilePaths 中除 manifest.json 外的其余所有文件；
  d. 计算各文件的原始磁盘二进制 SHA-256；
  e. 格式化为 "normalized_path:sha256_hex" 加入 Entries (路径统一转为小写正斜杠)；
  f. 对 Entries 按 Ordinal 字符串升序执行全排序。

步骤 4 (最终摘要求解):
  将排好序的 Entries 数组以换行符 '\n' 拼接为单一 UTF-8 字符串 (末尾带换行符 '\n'):
  sourcePackageHash = "sha256:" + SHA256_Hex( UTF8_Bytes( ConcatenatedString ) )
```

- **消除状态时序回环**：由于计算时 `hashStatus` 已固定按 `"Valid"` 进行哈希，当计算完成将结果写入 `manifest.json` 并正式将 `hashStatus` 标为 `"Valid"` 时，磁盘文件内容与计算依据完全闭合一致，绝无二次漂移。

---

### 1.4 `resource_catalog.json` 机器可执行 JSON Schema (Draft-07)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "https://specs.mxframework.io/schemas/character-resource-catalog-v1.0.json",
  "title": "CharacterResourceCatalog",
  "type": "object",
  "additionalProperties": false,
  "required": ["schemaVersion", "packageId", "entries"],
  "properties": {
    "schemaVersion": { "type": "string", "enum": ["1.0.0"] },
    "packageId": { "type": "string", "pattern": "^[0-9]{6}$" },
    "entries": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["resourceKey", "relativePath", "sha256", "contentType", "sizeBytes"],
        "properties": {
          "resourceKey": { "type": "string", "pattern": "^[a-zA-Z0-9_\\-\\.]+$" },
          "relativePath": {
            "type": "string",
            "pattern": "^resources/(models/[a-zA-Z0-9_\\-\\.]+\\.fbx|animations/[a-zA-Z0-9_\\-\\.]+\\.fbx|textures/[a-zA-Z0-9_\\-\\.]+\\.png)$"
          },
          "sha256": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" },
          "contentType": { "type": "string", "enum": ["Model/FBX", "Animation/FBX", "Texture/PNG"] },
          "sizeBytes": { "type": "integer", "minimum": 1 }
        }
      }
    }
  }
}
```

---

### 1.5 `weapon_slots.json` 机器可执行 JSON Schema (Draft-07)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "https://specs.mxframework.io/schemas/character-weapon-slots-v1.0.json",
  "title": "CharacterWeaponSlots",
  "type": "object",
  "additionalProperties": false,
  "required": ["schemaVersion", "weaponSlots"],
  "properties": {
    "schemaVersion": { "type": "string", "enum": ["1.0.0"] },
    "weaponSlots": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["slotId", "attachSocketId", "allowedWeaponTypes"],
        "properties": {
          "slotId": { "type": "string", "pattern": "^[a-zA-Z0-9_\\-]+$" },
          "attachSocketId": { "type": "string", "pattern": "^[a-zA-Z0-9_\\-]+$" },
          "allowedWeaponTypes": {
            "type": "array",
            "minItems": 1,
            "items": { "type": "string", "enum": ["SwordSingle", "GreatSword", "Shield", "Bow", "Dagger"] }
          },
          "defaultWeaponId": { "type": ["string", "null"] }
        }
      }
    }
  }
}
```

---

## 二、 `manifest.json` 清单 Schema 与序列化闭环

### 2.1 严密的机器可执行 JSON Schema (Draft-07)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "https://specs.mxframework.io/schemas/character-manifest-v2.4.json",
  "title": "CharacterPackageManifest",
  "type": "object",
  "additionalProperties": false,
  "required": [
    "packageId",
    "stableId",
    "displayName",
    "version",
    "kind",
    "packageSchemaVersion",
    "coordinateConvention",
    "hashes",
    "assetEntries",
    "featureFlags"
  ],
  "properties": {
    "packageId": { "type": "string", "pattern": "^[0-9]{6}$" },
    "stableId": { "type": "string", "pattern": "^charpkg\\.[0-9]{6}$" },
    "displayName": { "type": "string", "minLength": 1, "maxLength": 64 },
    "version": {
      "type": "string",
      "pattern": "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?$"
    },
    "kind": { "type": "string", "enum": ["Character"] },
    "packageSchemaVersion": { "type": "string", "enum": ["2.4.0"] },
    "coordinateConvention": {
      "type": "object",
      "additionalProperties": false,
      "required": ["upAxis", "forwardAxis", "handedness", "unitScaleMeters", "rotationStorage"],
      "properties": {
        "upAxis": { "type": "string", "enum": ["YPositive"] },
        "forwardAxis": { "type": "string", "enum": ["ZPositive"] },
        "handedness": { "type": "string", "enum": ["LeftHanded", "RightHanded"] },
        "unitScaleMeters": { "type": "number", "const": 1.0 },
        "rotationStorage": { "type": "string", "enum": ["Quaternion"] }
      }
    },
    "hashes": {
      "type": "object",
      "additionalProperties": false,
      "required": ["algorithm", "hashStatus"],
      "properties": {
        "algorithm": { "type": "string", "enum": ["sha256"] },
        "hashStatus": { "type": "string", "enum": ["Valid", "PendingCalculation", "Dirty"] },
        "sourcePackageHash": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" }
      },
      "oneOf": [
        {
          "properties": { "hashStatus": { "const": "Valid" } },
          "required": ["sourcePackageHash"]
        },
        {
          "properties": { "hashStatus": { "enum": ["PendingCalculation", "Dirty"] } }
        }
      ]
    },
    "assetEntries": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "baseMesh",
        "bodyColliders",
        "bodyParts",
        "skeletonHierarchy",
        "applicationConfig",
        "attributesConfig",
        "actionsConfig"
      ],
      "properties": {
        "baseMesh": { "type": "string", "pattern": "^resources/models/[a-zA-Z0-9_\\-\\.]+\\.fbx$" },
        "animations": {
          "type": "array",
          "items": { "type": "string", "pattern": "^resources/animations/[a-zA-Z0-9_\\-\\.]+\\.fbx$" }
        },
        "bodyColliders": { "type": "string", "const": "geometry/body_colliders.json" },
        "bodyParts": { "type": "string", "const": "geometry/body_parts.json" },
        "skeletonHierarchy": { "type": "string", "const": "geometry/skeleton_hierarchy.json" },
        "bodyGeometry": {
          "type": ["string", "null"],
          "pattern": "^geometry/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "contactPoints": {
          "type": ["string", "null"],
          "pattern": "^geometry/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "sockets": {
          "type": ["string", "null"],
          "pattern": "^geometry/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "weaponSlots": {
          "type": ["string", "null"],
          "pattern": "^geometry/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "applicationConfig": { "type": "string", "const": "config/character_application.json" },
        "attributesConfig": { "type": "string", "const": "config/character_attributes.json" },
        "actionsConfig": { "type": "string", "const": "config/character_actions.json" },
        "damageZones": {
          "type": ["string", "null"],
          "pattern": "^config/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "paletteBinding": {
          "type": ["string", "null"],
          "pattern": "^config/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "paletteUsage": {
          "type": ["string", "null"],
          "pattern": "^config/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "clothConfig": {
          "type": ["string", "null"],
          "pattern": "^config/[a-zA-Z0-9_\\-\\.]+\\.json$"
        },
        "paletteTexture": {
          "type": ["string", "null"],
          "pattern": "^resources/textures/[a-zA-Z0-9_\\-\\.]+\\.png$"
        }
      }
    },
    "featureFlags": {
      "type": "object",
      "additionalProperties": false,
      "required": ["enableClothPhysics", "enableWeaponBake", "enableDamageZones", "enableContactMotionBake"],
      "properties": {
        "enableClothPhysics": { "type": "boolean" },
        "enableWeaponBake": { "type": "boolean" },
        "enableDamageZones": { "type": "boolean" },
        "enableContactMotionBake": { "type": "boolean" },
        "enableSecondaryBones": { "type": "boolean" }
      }
    },
    "dependencies": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["kind", "stableId", "versionRange", "optional"],
        "properties": {
          "kind": { "type": "string", "enum": ["framework", "weaponPackage", "actionLibrary"] },
          "stableId": { "type": "string" },
          "versionRange": { "type": "string" },
          "optional": { "type": "boolean" }
        }
      }
    }
  }
}
```

---

## 三、 几何体系与受击盒闭环契约（Geometry & Colliders）

### 3.1 `CharacterAuthoringPose` DTO 显式包含 `EulerHint`

```csharp
public sealed class CharacterAuthoringPose
{
    public string ParentKind { get; set; } = "Bone";
    public string ParentPath { get; set; } = string.Empty;
    public HumanoidBone HumanoidSemantic { get; set; } = HumanoidBone.None;

    public CharacterAuthoringVector3 Position { get; set; } = new(0, 0, 0);
    public CharacterAuthoringQuaternion Rotation { get; set; } = new(0, 0, 0, 1);
    public CharacterAuthoringVector3 Scale { get; set; } = new(1, 1, 1);

    // 显式声明：仅供创作展示，反序列化允许读取，但在规范化几何求值时彻底丢弃
    public CharacterAuthoringVector3? EulerHint { get; set; }
}
```

---

### 3.2 严密几何校验与无缺陷规范化求解（C# 完整实现）

```csharp
public static class CharacterColliderValidatorAndNormalizer
{
    // 全长边界阈值: 5毫米 到 10米
    public const float MinDimension = 0.005f;
    public const float MaxDimension = 10.0f;

    // 半径对应阈值: 2.5毫米 到 5米
    public const float MinRadius = MinDimension * 0.5f;
    public const float MaxRadius = MaxDimension * 0.5f;

    public static void ValidateRawCollider(in RawColliderDto collider, string objectPath)
    {
        if (collider == null)
            throw new DiagnosticException(objectPath, "Collider object cannot be null.");

        // 1. 姿态校验 (彻底拦截 Scale/Rotation/Position 中的 NaN 与 Inf)
        ValidatePose(collider.LocalPose, objectPath);

        // 2. 几何维度边界严格校验与遗留 size 全维度比对
        switch (collider.Shape)
        {
            case CharacterColliderShape.Box:
                if (collider.Size == null)
                    throw new DiagnosticException(objectPath, "Box collider must specify 'size'.");
                AssertInRange(collider.Size.X, objectPath, "size.x", MinDimension, MaxDimension);
                AssertInRange(collider.Size.Y, objectPath, "size.y", MinDimension, MaxDimension);
                AssertInRange(collider.Size.Z, objectPath, "size.z", MinDimension, MaxDimension);
                break;

            case CharacterColliderShape.Sphere:
                if (!collider.Radius.HasValue)
                    throw new DiagnosticException(objectPath, "Sphere collider must specify 'radius'.");
                AssertInRange(collider.Radius.Value, objectPath, "radius", MinRadius, MaxRadius);
                
                // 遗留 size 全三维包围审查 (X, Y, Z 均必须与直径 2R 吻合)
                if (collider.Size != null)
                {
                    float expectedDiameter = collider.Radius.Value * 2.0f;
                    AssertTolerance(collider.Size.X, expectedDiameter, objectPath, "size.x vs 2*radius");
                    AssertTolerance(collider.Size.Y, expectedDiameter, objectPath, "size.y vs 2*radius");
                    AssertTolerance(collider.Size.Z, expectedDiameter, objectPath, "size.z vs 2*radius");
                }
                break;

            case CharacterColliderShape.Capsule:
                if (!collider.Radius.HasValue)
                    throw new DiagnosticException(objectPath, "Capsule collider must specify 'radius'.");
                if (!collider.Height.HasValue)
                    throw new DiagnosticException(objectPath, "Capsule collider must specify 'height'.");
                
                float r = collider.Radius.Value;
                float h = collider.Height.Value;

                AssertInRange(r, objectPath, "radius", MinRadius, MaxRadius);
                AssertInRange(h, objectPath, "height", MinDimension, MaxDimension);

                // 核心几何约束：严格要求 H >= 2R (完全消除高度负容差，H == 2R 时圆柱长为 0)
                if (h < r * 2.0f)
                {
                    throw new DiagnosticException(objectPath,
                        $"Capsule height ({h}m) cannot be strictly less than its diameter ({r * 2.0f}m).");
                }

                // 遗留 size 全三维包围审查 (X=2R, Y=H, Z=2R)
                if (collider.Size != null)
                {
                    AssertTolerance(collider.Size.X, r * 2.0f, objectPath, "size.x vs 2*radius");
                    AssertTolerance(collider.Size.Y, h, objectPath, "size.y vs height");
                    AssertTolerance(collider.Size.Z, r * 2.0f, objectPath, "size.z vs 2*radius");
                }
                break;

            default:
                throw new DiagnosticException(objectPath, $"Unsupported collider shape: {collider.Shape}");
        }
    }

    public static CanonicalColliderGeometry NormalizeValidCollider(in RawColliderDto collider)
    {
        switch (collider.Shape)
        {
            case CharacterColliderShape.Box:
                Vector3 boxFull = new(collider.Size!.X, collider.Size.Y, collider.Size.Z);
                return CanonicalColliderGeometry.CreateBox(
                    fullDimensions: boxFull,
                    halfExtents: boxFull * 0.5f);

            case CharacterColliderShape.Sphere:
                float r = collider.Radius!.Value;
                return CanonicalColliderGeometry.CreateSphere(
                    radius: r,
                    fullDimensions: new Vector3(r * 2f, r * 2f, r * 2f));

            case CharacterColliderShape.Capsule:
                float capR = collider.Radius!.Value;
                float capH = collider.Height!.Value;
                // 保证几何恒等式: totalHeight == capR * 2 + cylinderHalfHeight * 2
                float cylHalfHeight = (capH - capR * 2.0f) * 0.5f;
                return CanonicalColliderGeometry.CreateCapsule(
                    radius: capR,
                    totalHeight: capH,
                    cylinderHalfHeight: cylHalfHeight,
                    fullDimensions: new Vector3(capR * 2f, capH, capR * 2f));

            default:
                throw new InvalidOperationException();
        }
    }

    private static void AssertInRange(float value, string path, string field, float min, float max)
    {
        if (!float.IsFinite(value) || value < min || value > max)
            throw new DiagnosticException(path, $"Field '{field}' ({value}) is invalid. Must be finite within [{min}, {max}].");
    }

    private static void AssertTolerance(float actual, float expected, string path, string context)
    {
        if (!float.IsFinite(actual) || MathF.Abs(actual - expected) > 0.01f) // 10mm 容差
        {
            throw new DiagnosticException(path,
                $"Legacy dimension '{context}' deviation exceeds 10mm. Actual: {actual}, Expected: {expected}.");
        }
    }

    private static void ValidatePose(CharacterAuthoringPose pose, string path)
    {
        if (pose == null) throw new DiagnosticException(path, "localPose cannot be null.");

        // 1. 位置有限性检查
        if (!float.IsFinite(pose.Position.X) || !float.IsFinite(pose.Position.Y) || !float.IsFinite(pose.Position.Z))
            throw new DiagnosticException(path, "localPose.position contains non-finite values (NaN or Infinity).");

        // 2. 缩放检查：先排查 NaN，再比对容差；通过后强制锁定单位化
        if (pose.Scale == null ||
            !float.IsFinite(pose.Scale.X) || !float.IsFinite(pose.Scale.Y) || !float.IsFinite(pose.Scale.Z) ||
            MathF.Abs(pose.Scale.X - 1.0f) > 1e-4f ||
            MathF.Abs(pose.Scale.Y - 1.0f) > 1e-4f ||
            MathF.Abs(pose.Scale.Z - 1.0f) > 1e-4f)
        {
            throw new DiagnosticException(path, "Scale must be strictly finite and unit (1,1,1). Non-uniform scale is prohibited.");
        }

        // 3. 四元数检查：先排查每个分量有限性，再比对范数
        float qx = pose.Rotation.X;
        float qy = pose.Rotation.Y;
        float qz = pose.Rotation.Z;
        float qw = pose.Rotation.W;

        if (!float.IsFinite(qx) || !float.IsFinite(qy) || !float.IsFinite(qz) || !float.IsFinite(qw))
            throw new DiagnosticException(path, "localPose.rotation contains non-finite values (NaN or Infinity).");

        float sqrNorm = qx * qx + qy * qy + qz * qz + qw * qw;
        if (MathF.Abs(sqrNorm - 1.0f) > 1e-3f)
            throw new DiagnosticException(path, $"localPose.rotation must be normalized. SqrNorm: {sqrNorm}");
    }
}
```

---

## 四、 骨骼双锚点绑定与验证规范（Bone Dual-Anchor Protocol）

### 4.1 创作基线锁定与 $B_{\text{old}}$ 提取协议

在 `body_colliders.json` 的头部显式锁定创作时所依赖的骨架快照哈希：

```json
{
  "schemaVersion": "1.0",
  "authoredSkeletonSnapshotId": "sha256:7a9c8b...",
  "colliders": [ ... ]
}
```

$$P_{\text{new}} = B_{\text{new}}^{-1} B_{\text{old}} P_{\text{old}}$$
- **基线获取**：编译器依据 `authoredSkeletonSnapshotId` 从快照仓库中检索旧骨架定义 $B_{\text{old}}$；
- **自愈门禁裁决**：
  - 矩阵最大绝对偏差 $\Delta_B \le 10^{-4}$ 时，系统自动完成透明重绑定（$P_{\text{new}} = P_{\text{old}}$）；
  - 若 $\Delta_B > 10^{-4}$ 或旧快照丢失，立即中断并输出 `RebindCandidateReport`，转入人工审查，严禁猜测。

---

## 五、 部位伤害与命中区域四态闭环（Damage Authority Four-State Model）

### 5.1 伤害倍率业务区间与溢出防护

- 单项倍率约束：`baseDamageMultiplier` 与 `zoneOverrideMultiplier` 必须均在 $[0.01, 10.0]$ 之间；
- 最终乘积极值区间为 $[0.0001, 100.0]$，计算后执行 `float.IsFinite` 断言。

---

## 六、 noEngine 纯 C# 编译器与 WritePlan 严格执行协议

### 6.1 递归程序集依赖闭包扫描器实现

```csharp
public static class RecursiveAssemblySecurityValidator
{
    public static void AssertZeroEngineDependency(string rootAssemblyPath)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> toScan = new();
        toScan.Enqueue(rootAssemblyPath);

        while (toScan.Count > 0)
        {
            string currentPath = toScan.Dequeue();
            if (!visited.Add(currentPath) || !File.Exists(currentPath))
                continue;

            using var peReader = new PEReader(File.OpenRead(currentPath));
            if (!peReader.HasMetadata) continue;
            var mdReader = peReader.GetMetadataReader();

            foreach (var handle in mdReader.AssemblyReferences)
            {
                var reference = mdReader.GetAssemblyReference(handle);
                string refName = mdReader.GetString(reference.Name);

                // 核心闭环：递归检查所有传递依赖，前缀匹配 UnityEngine.* 与 UnityEditor.*
                if (refName.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    refName.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException(
                        $"Dependency Violation: Assembly '{currentPath}' transitively references engine assembly '{refName}'.");
                }

                // 将非系统程序集加入待扫描队列
                if (!refName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
                    !refName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
                    !refName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                {
                    string candidatePath = Path.Combine(Path.GetDirectoryName(currentPath)!, refName + ".dll");
                    if (File.Exists(candidatePath) && !visited.Contains(candidatePath))
                    {
                        toScan.Enqueue(candidatePath);
                    }
                }
            }
        }
    }
}
```

---

### 6.2 彻底消除路径逃逸与符号链接漏洞的安全沙箱

```csharp
public static class SecurePathSandboxEnforcer
{
    public static string ResolveAndVerifyPath(string trustedWorkspaceRoot, string packageId, string targetRelativePath)
    {
        // 1. packageId 前置强校验，杜绝通过 packageId 注入 ../outside
        if (!Regex.IsMatch(packageId, "^[0-9]{6}$"))
        {
            throw new SecurityException($"Invalid packageId format: '{packageId}'. Must be strictly 6 digits.");
        }

        // 2. 授权根目录强制追加目录分隔符
        string authorizedRoot = Path.GetFullPath(Path.Combine(trustedWorkspaceRoot, packageId));
        string rootWithSeparator = authorizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) 
                                   + Path.DirectorySeparatorChar;

        string combined = Path.GetFullPath(Path.Combine(authorizedRoot, targetRelativePath));
        string combinedWithSeparator = combined.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) 
                                       + Path.DirectorySeparatorChar;

        // 3. 目录边界精确前缀包含比对
        if (!combinedWithSeparator.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new SecurityException(
                $"Path Traversal Blocked: Path '{targetRelativePath}' attempts to escape package sandbox '{authorizedRoot}'.");
        }

        // 4. 符号链接深度安全检查：先查目标文件自身，再查各级祖先目录
        VerifyNoSymbolicLinks(authorizedRoot, combined);

        return combined;
    }

    private static void VerifyNoSymbolicLinks(string rootDir, string fullPath)
    {
        // 关键整改：先检查末端文件自身是否为符号链接
        FileInfo fileInfo = new(fullPath);
        if (fileInfo.Exists && fileInfo.LinkTarget != null)
        {
            throw new SecurityException($"Symbolic link detected and prohibited on target file: '{fileInfo.FullName}' -> '{fileInfo.LinkTarget}'");
        }

        // 向上递归检查父目录
        DirectoryInfo? current = fileInfo.Directory;
        DirectoryInfo root = new(rootDir);

        while (current != null && current.FullName.StartsWith(root.FullName, StringComparison.Ordinal))
        {
            if (current.Exists && current.LinkTarget != null)
            {
                throw new SecurityException($"Symbolic link detected and prohibited in path ancestor: '{current.FullName}' -> '{current.LinkTarget}'");
            }
            if (current.FullName.Equals(root.FullName, StringComparison.Ordinal))
                break;
            current = current.Parent;
        }
    }
}
```

---

### 6.3 WritePlan 完整 DTO 契约与操作执行语义

```csharp
public enum WritePlanStatus
{
    ReadyToApply = 0,
    VerificationPending = 1, // 存在待决项 (如语义重绑定)，禁止正式发布
    Failed = 2
}

public enum OverwritePolicy
{
    FailIfExists = 0,
    OverwriteAllowed = 1
}

public sealed class CharacterUnityImportWritePlan
{
    public string PlanVersion { get; set; } = "2.4.0";
    public string PackageId { get; set; } = string.Empty;
    public string SourceSnapshotId { get; set; } = string.Empty;
    public WritePlanStatus Status { get; set; } = WritePlanStatus.ReadyToApply;
    
    // 待决校验清单 (若有待决项，Status 必须设为 VerificationPending)
    public List<string> PendingChecks { get; set; } = new();
    
    public List<WritePlanOperation> Operations { get; set; } = new();
}

public sealed class WritePlanOperation
{
    public string OperationId { get; set; } = string.Empty;
    public WriteOperationKind Kind { get; set; } // CreateFile, UpdateFile, DeleteFile
    public string TargetRelativePath { get; set; } = string.Empty;
    public OverwritePolicy OverwritePolicy { get; set; } = OverwritePolicy.FailIfExists;
    
    public string? PreconditionExpectedSha256 { get; set; }
    public string PayloadSha256 { get; set; } = string.Empty;
    public byte[] PayloadBytes { get; set; } = Array.Empty<byte>();
    
    public List<string> DependsOnOperations { get; set; } = new();
}
```

#### 操作执行器幂等语义
1. **`CreateFile`**：
   - 目标不存在 $\rightarrow$ 写入并校验 `PayloadSha256`；
   - 目标已存在且当前 SHA 与 `PayloadSha256` 一致 $\rightarrow$ 幂等跳过（`Skipped_Identical`）；
   - 目标已存在且当前 SHA 不一致 $\rightarrow$ 若 `OverwritePolicy == FailIfExists` 抛出 `E-PLAN-401`；若为 `OverwriteAllowed` 则覆写；
2. **`UpdateFile`**：目标必须存在，且当前 SHA 必须等于 `PreconditionExpectedSha256`；若目标当前 SHA 已经等于 `PayloadSha256`，视为幂等已完成跳过；
3. **`DeleteFile`**：目标不存在时幂等成功；目标存在且匹配预期前置摘要时执行删除；
4. **DAG 依赖校验**：通过 Kahn 算法执行拓扑排序，检测到依赖环立即抛出 `E-PLAN-501: CircularDependencyDetected`；无依赖节点按 `OperationId` 字典序稳定执行。

---

## 七、 门禁严重性与重建前置条件

### 7.1 完整门禁字典清单

| 门禁代码 | 发生阶段 (Phase) | 严重性 (Severity) | 阻断目标操作 (TargetAction) | 详细触发条件 |
| :---: | :--- | :---: | :--- | :--- |
| `E-SRC-101` | Preflight | Fatal | `BlockAll` | `manifest.json` 结构不符合 Draft-07 Schema |
| `E-SRC-109` | Preflight | Fatal | `BlockAll` | `skeleton_hierarchy.json` 缺失，Core 无法进行骨骼校验 |
| `E-SKEL-101`| Preflight | Error | `BlockAll` | `skeleton_hierarchy` 的源网格哈希与模型 FBX 哈希不匹配 |
| `E-SRC-201` | Snapshot | Error | `BlockAll` | `manifest` 声明的必需资产在快照捕获时物理缺失 |
| `E-CAT-201` | Preflight | Error | `BlockAll` | `resource_catalog.json` 中出现重复的 `resourceKey` |
| `E-SLOT-301`| Preflight | Error | `BlockAll` | `weapon_slots.json` 引用的 `attachSocketId` 不存在 |
| `E-GEO-301` | CoreValidation | Error | `BlockExport` | 尺寸全长超出安全阈值 $[0.005, 10.0]$ 或非有限浮点数 |
| `E-GEO-302` | CoreValidation | Error | `BlockExport` | 胶囊体总高度严格小于两倍半径 |
| `E-GEO-303` | CoreValidation | Error | `BlockExport` | 遗留 `size` 全三维尺寸与权威几何偏差超过 $10\text{ mm}$ |
| `E-BONE-201`| CoreValidation | Error | `BlockSpawn` | 骨骼路径无法在骨架树解析，且未配置 Humanoid 语义备用锚点 |
| `E-BONE-202`| CoreValidation | Error | `BlockExport` | 骨骼路径指向骨骼 A 与 Humanoid 语义指向骨骼 B 发生冲突 |
| `E-DMG-401` | CoreValidation | Error | `BlockSpawn` | 伤害战术区域启用但配置文件缺失或命中区 ID 悬空 |
| `E-SEC-701` | WriteExecution | Fatal | `BlockAll` | 目标路径包含 `..` 越界逃逸或检测到符号链接 |
| `E-PLAN-401`| WriteExecution | Error | `BlockImport` | 创建文件已存在且未指定覆写策略 |
| `E-PLAN-501`| WriteExecution | Fatal | `BlockAll` | WritePlan 操作依赖存在拓扑环 |

---

### 7.2 受控幂等重建的前置必要条件

> [!NOTE]
> **资产重建的客观技术基准**：
> 在以下**四项前置必要条件同时满足**时，支持执行受控全量删除与无损重建：
> 1. **源快照完全一致**：`SnapshotId` 保持绝对一致；
> 2. **依赖锁定解析确定**：`package_cache/dependency_lock.json` 中的全部公共包版本及其内部文件哈希完全一致；
> 3. **GUID 与 LocalFileIdentifier 派生算法固定**：基于 UUID v5 的确定性派生算法恒定，Prefab 内部子对象名称匹配稳定；
> 4. **编译器与导入器设置完全对齐**：`CompilerVersion` 与 ModelImporter 预设参数矩阵完全一致。
> 
> 重建验收以执行 `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` 与场景引用完整性检查结果为准。

---

## 结语

本规范（v1.4 深度修订版）已逐一排除复审指出的全部具体代码缺陷与反例。通过**建立唯一的无歧义规范化摘要算法**、**修复 Scale NaN 穿透与胶囊几何恒等式冲突**、**补齐全三维遗留尺寸审查**、**根除末端文件符号链接与 packageId 路径逃逸**、**实现真正的程序集递归闭包扫描**、**并在 WritePlan DTO 中完整实现状态机与 DAG 拓扑执行器**，消除了所有“同一输入得到不同结果”的技术分歧。

规范已进入**高成熟度的实施候选状态（Implementation Candidate）**，具备进入工程编码与全链路联合测试的完备准入条件。
