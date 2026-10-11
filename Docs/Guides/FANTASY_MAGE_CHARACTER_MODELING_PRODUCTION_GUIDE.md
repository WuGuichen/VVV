# AI-first 风格化魔法师角色单 LOD0 生产指南

> Status: Guide
>
> 版本 3.1 | 2026-08-28
>
> 本文以“灰金低马尾、象牙白 / 炭灰 / 淡紫旅行法师服、腰包、长衣摆、棕色旅行靴”的年轻成年女性魔法师为例，定义 AI 渠道输出已拓扑低模后，在 Blender 中完成纯色板 UV、顶点色材质分类、Humanoid、Magica Cloth 2 和 Unity 接入的生产流程。首版只交付一个 `_LOD0`，不创建其他几何 LOD。

## 1. 文档目的

本文面向：

- 负责 AI 建模候选生成的美术或 Development Agent；
- Blender 角色美术；
- 材质美术；
- 绑定、蒙皮和技术动画；
- Unity 技术美术；
- 负责 Character Authoring 与导入验收的开发人员。

本文解决以下问题：

1. AI 已经提供拓扑、UV 和基础材质时，哪些传统步骤可以删除；
2. 哪些问题不能因为“AI 已拓扑”而跳过；
3. 如何快速判断 AI 候选应该精修还是重新生成；
4. 如何在 Blender 中把候选收敛为一个主体 Mesh、一个共享色板材质的固定服装角色，同时把几何统计保留为观察数据而不是阻断门禁；
5. 如何重建可信的 Humanoid、最小表情和可变形权重；
6. 如何用 Magica Cloth 2 管理马尾、披肩和长衣摆；
7. 如何在没有几何 LOD 的前提下，以 30 个以上不同角色同屏为移动端性能门禁；
8. 如何通过 Character Authoring 与 Unity Import Bridge 交付为可运行角色。

## 2. 与仓库权威的边界

本文只管理美术生产与验收方法，不创建新的 Character、Runtime 或 Magica Cloth 公共契约。

- Humanoid 角色主体以 FBX 为权威源；GLB / glTF 只用于非 Humanoid、静态模型或预览产物。见 [`../CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md`](../CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md)。
- CharacterStudio 是第一方参考客户端，不是 SDK；Schema、校验、编译和迁移不得在 Blender 脚本或 AI 工具中复制实现。
- Unity Prefab、Avatar、Registry、Trace / Socket Bake 等正式 Unity 资产写入只能由 Unity Import Bridge 完成。
- 角色静态配置、运行时装配和资源身份以 [`../Interfaces/CharacterApplication.md`](../Interfaces/CharacterApplication.md) 为准。
- 公共资源生命周期、ResourceKey 与导入修正以 [`../RESOURCE_SYSTEM_WORKFLOW.md`](../RESOURCE_SYSTEM_WORKFLOW.md) 为准。
- Magica Cloth 的状态、组装和权威边界以 [`../Interfaces/CharacterCloth.md`](../Interfaces/CharacterCloth.md) 为准。
- 不修改 `Assets/MagicaCloth2` 供应商源码；当前仓库安装版本为 Magica Cloth 2.18.2。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、工具缓存和个人状态不是版本化交付内容。

## 3. 已冻结的生产目标

### 3.1 角色视觉

角色是一名约 22 至 25 岁的年轻成年女性旅行魔法师：

- 灰金色中分前发和低马尾；
- 琥珀棕眼睛；
- 象牙白高领内衫；
- 炭灰色贴身躯干结构；
- 灰紫色短披肩；
- 象牙白和淡紫色分层长衣摆；
- 深色贴身打底裤；
- 棕色腰带、侧腰包和中筒旅行靴；
- 少量冷灰金属扣件和琥珀色点缀。

### 3.2 渲染语言

目标是低多边形几何、受控平滑法线与纯色板共同驱动的风格化渲染，而不是把三角面全部显露出来，也不是写实照片或贴图堆叠：

- 明亮中间调；
- 柔和、成组的阴影；
- 低噪声皮肤、头发和布料；
- `PaletteUV`、顶点色材质类别与 Stylized Lit 共同控制材质层级；
- 皮肤、脸、头发主体、布料、打底裤、靴身和木杖主体默认平滑着色；
- 服装拼片边界、披肩包边、袖口、鞋底、扣具、水晶切面和法杖分叉转折使用明确硬边；
- 平滑着色不改变 triangles、PaletteUV 或材质数量，不能靠增加角色专属 Normal Map 修复错误轮廓；
- 皮革、木材和布料不能依赖油亮高光制造完成度；
- 金属使用克制冷灰高光；
- 不使用强 Bloom、全局白边或统一材质反射。

### 3.3 平台与运行目标

- 平台：Snapdragon 8 Gen 2 级 Android；
- 画质：项目高画质档；
- 帧率：目标 60 FPS；
- 最坏场景：30 个以上不同的相近复杂度角色同时可见；
- 几何：只有一个 `_LOD0`；
- 首版不创建 `LODGroup`；
- 允许按距离关闭实时阴影、Animator 更新和 Magica Cloth；
- 服装固定，不做换装；
- 武器独立为公共资源，不嵌入角色主体 FBX。

## 4. 项目内基准：`combat_girl_sword`

### 4.1 基准路径

项目内参考角色位于：

```text
Assets/MxFrameworkGenerated/CharacterPackages/combat_girl_sword
```

该目录是 Unity Import Bridge 的生成结果。它用于测量和对照，不能作为手工编辑入口。

### 4.2 Blender 实测数据

使用 Blender 5.0.1 重新导入生成包中的 `CombatGirl_Sword.fbx` 后得到：

| 指标 | 数值 |
| --- | ---: |
| FBX Mesh 对象 | 16 |
| FBX 总 vertices | 29,679 |
| FBX 总 triangles | 55,751 |
| Armature | 1 |
| bones | 86 |
| FBX 总材质槽 | 30 |
| BlendShape | 0 |

总量包括备用 Body、五套 Face、Squire 服装和 Sword 服装，不能直接当作当前运行造型成本。

### 4.3 当前 Sword 造型的可见部分

`resource_catalog.json` 明确隐藏以下备用 Renderer：

- `Body_Full`；
- `Face_01Re`；
- `Face_02`；
- `Face_03`；
- `Face_05`；
- `Body_Squire`；
- `Squire_Cloth`。

当前 Sword 造型的角色可见部分约为：

| 内容 | triangles |
| --- | ---: |
| `Body_Sword` | 3,292 |
| `Face_04` | 2,908 |
| `Sword_Hair` | 4,254 |
| `Sword_Cloth_Top` | 5,251 |
| `Sword_Cloth_Cloak` | 1,124 |
| `Sword_Cloth_Belt` | 1,582 |
| `Sword_Cloth_Boots` | 4,616 |
| 可见角色小计 | 23,027 |
| `Weapon_Sword` | 2,440 |
| 含内嵌武器合计 | 25,467 |

本文保留“可见角色小计”与 FBX 总 triangles 作为项目内对照数据，但这些统计不构成新角色的面数门禁，也不单独决定候选拒绝、导出或发布。

### 4.4 骨骼与材质组织

基准角色的 86 骨包括：

- 标准身体与完整手指；
- 上下臂、大腿和小腿扭转骨；
- 头发骨链；
- 腰带骨；
- 武器附加层级。

当前 Sword 角色约有 7 个可见角色 Renderer、11 个材质槽；武器另有 1 个 Renderer / 材质槽。主要材质资源为：

- Body；
- Face；
- Eye；
- Sword Cloth；
- Sword Hair；
- Weapon。

新魔法师不照搬这些 Renderer 和材质槽数量，而是把性能和视觉语言收敛为一个主体 Mesh、一个项目共享色板材质。

### 4.5 贴图与导入证据

基准角色使用：

- 2K Body；
- 2K Face；
- 2K Face Mask；
- 2K Eye；
- 2K Cloth 与 Rim Mask；
- 2K Hair 与 Rim Mask；
- 512 MatCap；
- 2K Weapon 与 Rim Mask。

当前 `unity_editor_import_receipt.json` 报告：

- `ready: true`；
- `rigType: Humanoid`；
- `rigStatus: ValidHuman`；
- Prefab、材质、CharacterAction、Weapon Trace Bake 和必要骨骼均 Ready。

这组数据是新魔法师的项目内质量基线，但不是要求复制其无 BlendShape、多 Face Mesh 的具体实现。

## 5. 新魔法师的制作约束

### 5.1 几何统计（非门禁）

- 角色主体、法杖和附件仍须记录 vertices、polygons 与 triangulated triangles，服务版本回归、内容对照和性能定位；
- 不设置 triangles 目标区间或硬上限，面数不得单独作为 AI 候选拒绝、Blender 阶段阻断、FBX 导出、Unity 导入或发布判定；
- 只有已确认的拓扑问题、变形问题、视觉问题，或动作、渲染、内存和目标设备性能证据，才能触发针对性的几何优化；
- 统计值必须来自 Blender 三角化后的实际 Mesh，不使用四边面数量或 AI 平台显示的估算值。

### 5.2 骨骼与蒙皮

- 目标骨骼：65-80；
- 硬上限：85；
- 每顶点最多 4 个骨骼影响；
- 只有一个权威 Armature Root；
- AI 骨架不进入正式资产；
- 新骨架应接近 `combat_girl_sword` 的人体、扭转、头发和配件职责，但不复制无关骨名或备用造型层级。

### 5.3 运行网格与材质

最终角色主体只允许一个主要运行 Mesh / Renderer：

```text
CHR_TravelMage_Palette_LOD0
```

对应一个项目共享色板材质：

```text
MAT_WGame_CharacterPaletteShared
```

所有网格保留 `_LOD0` 后缀，但当前 Prefab 不添加 `LODGroup`。

## 6. 开工前的参考输入

AI 候选生成前至少准备：

- 正面 T Pose；
- 侧面 T Pose；
- 背面 T Pose；
- 正面和侧面头部近景；
- 无头发遮挡的耳朵、颈部与后脑参考；
- 披肩背面；
- 长外套背面分片；
- 袖口内部与内衬；
- 腰包侧面、背面和厚度；
- 靴子侧面、背面和鞋底；
- 服装层级爆炸图；
- 标准身高和关键尺寸；
- 32 色板槽语义、四行整套换色与材质类别高光参考。

如果参考缺失，不让 AI 或 Blender 美术自行猜完背面。缺口必须由概念设计或负责人明确批准。

## 7. AI 候选交付合同

### 7.1 必需输出

AI 建模渠道必须交付：

- T Pose 网格；
- 已拓扑模型；
- 基础 UV；
- 基础 Base Color / 材质；
- 原始导出文件；
- 预览图；
- 平台与模型版本；
- 完整提示词；
- 输入参考图清单；
- Seed 或等价复现标识；
- 许可证、商业使用与再分发条件；
- 文件 SHA-256。

骨架、蒙皮、表情、材质参数和 AI 自动绑定不视为可信交付。

### 7.2 文件分层

```text
AI_Raw/
├── candidate_original.*
├── preview.*
└── provenance.json

Blender_Work/
├── travel_mage_work.blend
└── audit/

Final_Source/
├── travel_mage_authoring.blend
├── textures/
└── travel_mage_LOD0.fbx
```

- `AI_Raw` 只读归档；
- Blender 工作文件可以迭代；
- Final Source 是美术权威；
- 正式 Unity 产物由 Unity Import Bridge 生成；
- 不手工编辑 `Assets/MxFrameworkGenerated`。
- 新角色的角色根与稳定身份使用编号池分配的六位数字 ID；完整规范见 `WGAME_NUMERIC_CHARACTER_AUTHORING.md`。示例中的文件 stem 必须替换为该数字 ID，而不是 displayName。
- `AI_Raw`、Blender 工作文件、Final Blender、模型图、audit 与 provenance 均为可选制作资产；未声明时不阻断角色编译或 Unity 导入。一旦通过 `authoring_manifest.json` 纳入管理，就必须使用标准目录、当前 SHA-256 和 Git LFS。
- Blender autosave、`.blend1/.blend2`、recovery 和时间戳备份只属于本地恢复状态，不得作为里程碑或 Final Source 提交。

## 8. AI 候选拒绝门禁

### 8.1 直接重新生成

出现任一情况时，不投入大规模手工修复：

- 身体、头发、披肩、衣摆和腰包大面积融合；
- 眼睑不能包住眼球；
- 嘴唇没有闭合环线；
- 肩、肘、腕、髋、膝、踝缺少可变形拓扑；
- 大量非流形、内部面、退化面或自相交；
- 角色正面、侧面或背面轮廓明显偏离设定；
- 成年比例被生成成儿童或夸张娃娃比例；
- 服装层级无法解释；
- UV 大面积无意义重叠或碎裂；
- 修复预计超过一个工作日；
- 预计需要重做超过约 20% 网格。

### 8.2 允许 Blender 局部修复

- 尺寸、单位、轴向和原点；
- Apply Transform；
- 重复点、孤立面和少量退化面；
- 局部反法线；
- 少量关节环线；
- 局部服装穿插；
- UV Padding 和局部密度；
- 材质槽重新分区；
- 隐藏身体面；
- 小配件合并；
- 已确认的几何问题修复与骨骼预算收敛；面数只记录，不作为局部修复或重新生成的判定条件。

## 9. Blender 首次导入与审计

当前工作站已验证 Blender 5.0.1 可用。首次导入按以下顺序执行：

1. 另存工作副本；
2. 设定真实尺寸；
3. 将原点放在两脚之间的地面中心；
4. 校正前方、上方和 T Pose；
5. Apply Rotation / Scale；
6. 统一法线方向；
7. 统计 Mesh、vertices、triangles、材质槽和 UV；
8. 检查非流形、重复点、孤立面和内部面；
9. 检查身体、服装和头发是否可语义分离；
10. 决定修复或重新生成。

### 9.1 自动审计输出

建议生成机器可读报告，至少包含：

```text
mesh_objects
vertices_per_mesh
triangles_per_mesh
material_slots_per_mesh
uv_layers
non_manifold_edge_count
degenerate_face_count
loose_vertex_count
armature_count
bone_count
max_bone_influences
shape_key_names
```

审计脚本只报告，不自动改写候选。

## 10. 工作对象拆分与单 Mesh 收敛

以下名称是 Blender 工作阶段的对象组，不代表最终增加 Renderer 或材质槽。最终导出时全部合并进一个主体 Mesh。

### 10.1 `BodyFace_Work`

包含：

- 头部；
- 颈部；
- 双手；
- 必要暴露皮肤；
- 睫毛；
- 眉毛；
- 口腔、牙齿和舌头。

不包含眼球、头发、服装、腰包和金属件。

### 10.2 `Eyes_Work`

- 双眼独立对象后合并为一个运行 Mesh；
- 保留左右眼骨；
- 允许共享 UV 和材质；
- 眼球和虹膜在极端视线下不能穿眼皮。

### 10.3 `Hair_Work`

- 头皮底层；
- 前刘海；
- 左右脸侧发束；
- 后脑主体；
- 低马尾；
- 必要的轮廓散发。

马尾和脸侧发束保留独立骨链与权重区，但导出时进入同一个主体 SkinnedMeshRenderer。

### 10.4 `OutfitAccessories_Work`

- 内衫；
- 躯干贴身层；
- 宽袖；
- 短披肩；
- 前后长衣摆；
- 打底裤；
- 腰带；
- 腰包；
- 靴子；
- 主要金属件。

服装和配件在 Blender 中可保持多个工作对象，但导出前合并为一个运行 Mesh，并通过 `PaletteUV` 色板槽和顶点色 A 材质类别区分皮肤、布料、皮革 / 木材与金属 / 宝石。

## 11. 身体、脸与手的 AI 拓扑验收

### 11.1 身体

- 锁骨和肩峰支持 T Pose 与双臂下垂；
- 腋下不能是极薄楔形；
- 肘、膝前后有足够环线；
- 手腕截面支持宽袖口；
- 骨盆、大腿根和臀部支持抬腿与下蹲；
- 靴口附近保留可用小腿表面；
- 不靠非 1 Root Scale 修正身高。

### 11.2 面部

- 正面、侧面和三分之四视角同时成立；
- 眼睑有闭合环线；
- 嘴部有完整闭合环线；
- 眉区、脸颊和下颌有变形缓冲区；
- 鼻子有体积但不过度写实；
- 颈部与头部边界法线连续；
- 表情区无长三角和高密度孤岛。

### 11.3 手

- 五指完整；
- 指节位置正确；
- 拇指掌骨可对握；
- 指缝没有焊死；
- 握杖姿势不会撕裂虎口；
- 手掌在近景仍有清楚轮廓。

## 12. 固定服装与隐藏身体

服装固定，因此动作验证后可以删除永远不可见的身体面。

保留：

- 头、颈和双手；
- 袖口可能露出的前臂；
- 领口可能露出的上胸；
- 衣摆开口附近可能露出的腿；
- 靴口附近必要腿部。

删除：

- 被上衣完全覆盖的躯干；
- 被袖子完全覆盖的手臂；
- 被打底裤和靴子完全覆盖的腿脚；
- 永远位于闭合衣摆内部的身体面。

至少完成双臂下垂、抬手、下蹲、大步、扭身和双手持杖测试后再删除。

## 13. 头发收敛

- 以 8-14 组主要发束建立整体轮廓，避免后脑成为没有发流变化的头盔壳；
- 删除被外层发束完全遮挡的内部面；
- 不使用大量透明细发堆完成度；
- 发根贴合头皮；
- 每束从宽到窄；
- 耳前发束与脸部保持表情空间；
- 前发使用 2-4 组长短不同的贴脸发束，保留耳朵和脸部轮廓，不做单块粗大斜刘海；
- 后脑发流汇入后颈束点，马尾根部有真实束口和下层承接体积；
- 马尾拆成 4-6 组可读的大束，形成轻微 S 形下垂和错落收尖，不做单个叶片状尖块；
- 每个主要马尾束内部平滑着色，相邻束边界允许拆法线并进入独立骨链权重区；
- 头发高光服从发束方向，不出现整头统一白带。

## 14. 服装与配件收敛

### 14.1 宽袖

- 袖窿围绕肩和上臂具有连续拓扑；
- 肘内侧预留压缩空间；
- 袖口有厚度；
- T Pose 不能成为袖子的永久形状；
- 首版使用普通骨骼和动画修正，不交给 Magica Cloth。

### 14.2 披肩与长衣摆

- 披肩外层和内衬在工作文件中可分开，运行时合并到 Outfit Mesh；
- 肩峰区域主要跟随锁骨，下缘使用独立骨链；
- 长衣摆工作阶段区分中央前片、左右前片、左右后 / 侧后片和内衬；
- 每个主要衣片保留独立骨链和权重区域；
- 抬手时披肩不与宽袖占据同一空间。

### 14.3 腰包

- 包体绑定刚性骨；
- 挂带可以混合骨盆和腰包骨权重；
- 不进入首版 Magica Cloth；
- 不能软蒙皮成随腰弯曲的布袋；
- 与手臂、披肩和衣摆通过动作矩阵验收。

### 14.4 小配件

保留改变轮廓的胸前挂件、主扣、腰包扣、靴子大扣和衣摆末端件。纯色板模式不提供角色专属细节贴图；小铆钉、细金属边和缝线只有在游戏距离可见时才做成简化几何，否则删除。

## 15. 法线、PaletteUV 与共享色板图集

### 15.1 受控平滑法线

- Blender 工作对象使用 `Shade Smooth by Angle` 或等价的自定义分裂法线流程，不使用全模型 `Shade Flat`；
- 皮肤与脸的可见区域必须连续平滑，鼻翼、唇线和眼睑靠真实轮廓与环线表达，不靠随机硬边；
- 头发的大束体内部平滑，相邻主发束可在交界处拆法线；
- 布料大面平滑，只有缝合拼片边界、厚度边、锐利压线和刻意折角保留硬边；
- 皮革和木材主体平滑，扣具、鞋底、水晶切面与金属折边保留硬边；
- 标记 Sharp 的边优先与 UV0 seam、PaletteUV 色块边界和真实结构边重合，避免无意义增加拆分顶点；
- 导出 FBX 前应用法线策略并重新导入空 Blender 场景，确认平滑法线、切线和硬边不丢失。

### 15.2 UV 通道

- `UV0` 保留普通连续展开，服务共享溶解噪声、调试和未来兼容；
- `UV1 / PaletteUV` 完整寻址 256×256 Color Palette，固定落在 `((x+0.5)/256, (y+0.5)/256)`；
- ColorIndex 是唯一 RGB565 地址：`(R5<<11)|(G6<<5)|B5`；任意输入颜色由工具映射到最近地址，不存在角色行替换或 RGB alias；
- 同一颜色区域每个三角面的三个 UV 角必须落在同一个颜色像素中心；
- 不允许把颜色区域跨在两个槽之间插值。
- 显式面部预设角色增加第三套 `UV2 / FaceDetailUV`；标准正脸与眼球表面映射到 canonical `0..1`，全部非面部 Loop 固定为 `(-1,-1)` 中性哨兵；
- UV 顺序固定为 `UV0`、`PaletteUV`、`FaceDetailUV`，不得依靠 Blender UV 名称替代导出通道顺序。

### 15.3 顶点色

- `R`：静态 AO 权重，`1` 表示无遮挡；
- `G`：Rim 权重；
- `B`：Emission 权重；
- `A`：Surface Profile；固定使用 `alphaByte = 8 + profileIndex×16` 的 16 个中心值，覆盖 Skin、Hair、Fabric、Leather、Wood、Matte、Metal 与 Crystal。Profile 边界必须拆顶点，禁止三角形内插值。

### 15.4 共享图集

- 全项目共享一张 `256×256 RGB24` Color Palette，16 套 Surface Profile 由 Catalog 生成 HLSL 常量表；
- sRGB、Point、Clamp、无 Mipmap、无压缩；
- 65,536 个颜色地址全部有效且 RGB8 唯一，任意颜色都可组合任意 Profile；原始 Atlas 只作为技术存储，DCC 通过颜色选择器取值；
- 首版没有角色专属 BaseMap、Normal、AO 或 Mask；
- 30 个以上角色共享同一 Atlas 与材质；颜色/Profile 是不可变 Mesh 数据，运行时不切换整行或 Profile。

### 15.5 角色独立面部细节

- 每个启用面部细节的角色在自身目录维护 `FaceDetail/face_detail_rgba.png`，固定为 512×512 Direct RGBA；眉毛、眼睛、眼线、睫毛和嘴唇都合成到这张角色专属图，不再依赖共享预设图集；
- RGB 是最终 sRGB 细节颜色，A 是覆盖透明度；Shader 使用完整 `FaceDetailUV` 直接采样并混合一次，不计算格子、Preset 或行翻转；
- Unity 使用 sRGB、Bilinear、Clamp、Mipmap、Alpha FromInput、最大 512 与 Android ASTC 6×6；非面部区域继续使用 `FaceDetailUV=(-1,-1)` 中性哨兵；
- `CharacterPaletteBinding` 通过 `MaterialPropertyBlock` 绑定当前角色的贴图和强度，不创建角色材质实例。关闭 FaceDetail 时不要求贴图与 UV2，启用时缺失或跨包资源必须阻断导入。

## 16. 风格化材质验收

- 皮肤没有摄影级毛孔、塑料高光或油膜；
- 头发由实体发束和深浅色板槽组成，不使用透明发片或发丝贴图；
- 布料整体高粗糙度，象牙白不过曝，炭灰仍能读出褶皱；
- 皮革没有锤纹、蜂窝纹或油亮；
- 金属通过 `metal_brushed / metal_polished` Surface Profile、冷灰颜色和窄高光表达；
- 布料、皮革和金属不能共享同一种白色高光。
- 模型不能为了证明“低模”而全局平面着色；轮廓复杂度由几何承担，表面连续性由平滑法线承担，颜色由 Color Palette、材质差异由 Surface Profile 承担。

## 17. 重建标准 Humanoid

### 17.1 不保留 AI 骨架

AI 骨架、自动权重和骨名只作为候选参考。Blender 中重建：

- `root`、`pelvis`、spine / chest / upper chest、neck / head；
- clavicle / upperarm / lowerarm / hand 和完整手指；
- thigh / calf / foot / ball；
- 上下臂、大腿和小腿扭转骨；
- 左右眼骨；
- 低马尾和脸侧发束骨；
- 披肩和长衣摆骨；
- 腰包刚性骨。

目标 65-80 骨，硬上限 85。

### 17.2 蒙皮

- 自动权重只作为起点；
- 每顶点最多 4 influences；
- 硬件扣件尽量刚性权重；
- 肩部同时考虑锁骨和上臂；
- 肘膝内侧不塌陷；
- 腰带不随腹部拉长；
- 披肩、宽袖和衣摆职责分开；
- 腰包主体保持刚性；
- 马尾根部稳定，末端逐级增加活动。

### 17.3 Unity Avatar

- FBX 使用 `Create From This Model`；
- Unity Import Bridge 输出必须为 `rigType: Humanoid`；
- `rigStatus` 必须为 `ValidHuman`；
- T Pose 校正无异常；
- 不依赖未验证 companion skeleton；
- 不手工修改生成 Prefab 中的 Avatar 事实。

## 18. 首版表情边界

纯色板 30+ 同屏首版不要求 BlendShape。眼球保留独立眼骨，眉毛、上睫毛、嘴唇与眼部颜色由不透明几何和色板槽表达。眼皮与嘴周拓扑仍应保留未来扩展所需的闭合环线，但表情、口型和捏脸不进入 v1 交付门禁。

## 19. T Pose 之外的蒙皮验收

至少测试 A Pose、双臂下垂、前举 90 度、上举、弯肘、双手握杖、深蹲、单腿抬高、大步前跨、躯干扭转、低头、抬头、跑步和施法关键帧。

T Pose 正确但上述姿势塌陷，仍视为未通过。

## 20. Magica Cloth 2.18.2

### 20.1 首版只使用 BoneCloth

移动端首版不使用 MeshCloth。Blender 中建立：

- Hair：低马尾 3-5 节主链，必要脸侧发束每侧 1-3 节；
- Garment：披肩下缘、左右前衣摆、左右后 / 侧后衣摆，每片 2-4 节。

宽袖使用普通骨骼和动画修正，腰包使用刚性骨。

### 20.2 角色组件

```text
CharacterRoot
├── MagicaCharacterClothSystem
├── Cloth_Hair
│   ├── MagicaCloth (BoneCloth)
│   └── MagicaCharacterClothController
└── Cloth_Garment
    ├── MagicaCloth (BoneCloth)
    └── MagicaCharacterClothController
```

两个 Controller 独立，允许 Hair 与 Garment 使用不同距离质量状态；同一 Controller 只归属一个 System。

### 20.3 碰撞体与权威

只保留头、颈、胸廓、骨盆、左右上臂和左右大腿的少量 Sphere / Capsule。

- Magica 只修饰表现骨；
- 战斗碰撞、命中点和 Socket 不绑定到 Cloth Transform；
- Cloth 不进入 Replay、SaveState、Runtime hash 或网络同步；
- 状态切换经现有 Character Cloth adapter 完成。

## 21. 30+ 人同屏布料分级

| 组 | 人数 | Hair | Garment |
| --- | ---: | --- | --- |
| Near | 2 | `Running` | `Running` |
| Mid | 4 | `Running` | `DisabledReset` |
| Far | 9 | `DisabledReset` | `DisabledReset` |

从 Far / Mid 进入更高质量组时先 Reset 到动画姿势，再恢复 `Running`。排序使用稳定 tie-breaker 和进出滞后距离，避免阈值抖动；离屏角色直接进入 Far。

## 22. 单 LOD0 策略

### 22.1 明确不做

- 不制作 LOD1 / LOD2 / LOD3；
- 不制作 Impostor；
- 不添加 `LODGroup`；
- 不额外放置隐藏替代网格制造伪 LOD。

### 22.2 允许的距离质量控制

- Magica Cloth 距离分级；
- 实时阴影开关；
- Animator 更新策略；
- 离屏剔除；
- 材质质量档；
- Texture Streaming；
- VFX 与后处理质量档。

所有角色几何始终使用同一个 `_LOD0`；其实际 triangles 写入审计报告，但不设面数门禁。

## 23. FBX 导出

导出前确认：

- 一个正式主体 Mesh、一个共享色板材质；
- 一个 Armature Root；
- 记录 Blender 三角化后的实际 triangles，统计异常需解释，但不得仅因面数阻断导出；
- 目标 65-80，绝不超过 85 bones；
- 每顶点最多 4 influences；
- 首版不要求 BlendShape；
- Scale 为 1，无负缩放；
- 原点、地面、法线、切线和 UV 正确；
- 皮肤、头发、布料和木杖主体平滑法线连续，结构硬边保持清楚；
- 不导出参考平面、AI 原始骨架、草稿 Mesh 或武器；
- 导出后重新导入空 Blender 场景并再次审计。

## 24. Character Authoring 与 Unity 导入

- Final FBX 进入角色 ArtSource / Character Authoring 流程；
- 武器进入公共武器资源流程；
- 不手工编辑生成 resource catalog、Prefab 或 receipt；
- Unity Import Bridge 拥有正式 Unity 资产写入。

Unity 门禁：

- Scale Factor 和实际身高正确；
- Rig 为 Humanoid，Avatar mapping 完整，`ValidHuman`；
- 只有 `_LOD0`，没有 `LODGroup`；
- 一个主体 SkinnedMeshRenderer、一个共享色板材质；
- Bounds 覆盖马尾、披肩和衣摆；
- 眼球骨、眉毛 / 睫毛几何和 PaletteUV 可见；
- Hair / Garment Cloth Controller 可发现；
- Prefab 重建引用稳定；
- Runtime Spawn Preflight 成功。

## 25. 动作与穿插矩阵

| 动作 | 重点检查 |
| --- | --- |
| 待机 | 披肩、马尾、衣摆自然下垂；腰包不穿腿 |
| 行走 | 前后衣摆不过度夹腿；靴筒不塌陷 |
| 跑步 | 马尾与披肩不持续穿插；腰包摆幅受控 |
| 急停 | 衣摆与马尾有回弹但不翻面 |
| 转身 | 左右衣片相位自然；披肩不锁死 |
| 跳跃 | 下摆不穿骨盆和大腿；落地不爆振 |
| 翻滚 | Cloth 降权或关闭，不发生全身穿插 |
| 抬手施法 | 宽袖不穿披肩；领口不切下巴 |
| 双手持杖 | 双袖、胸前挂件和腰包不阻挡手臂 |
| 受击 | 长衣摆不翻进身体；马尾不穿脸 |
| 下蹲 | 前衣摆与大腿有空间；腰带不撕裂 |
| 倒地 | 动态部件可回退，不持续震荡 |

## 26. Android 30+ 人性能场景

### 26.1 固定条件

- Snapdragon 8 Gen 2 级 Android；
- 项目高画质档和目标原生分辨率；
- 30 个以上不同角色，每人唯一 `_LOD0`；
- 每人一个主体 Renderer / 共享材质，武器另计；
- Near 2、Mid 4、Far 24+ Cloth 分级；
- 最近 4 人允许实时 Cast Shadow；
- 其余角色关闭实时 Cast Shadow 或使用项目既有低成本方案；
- 固定场景、镜头路径和动画序列；
- warm-up 后连续运行 60 秒。

### 26.2 记录与门禁

记录平均 FPS、P95 frame time、CPU Main Thread、Render Thread、GPU、Skinning、Animator、Magica Cloth、Draw Calls、SetPass、显存、Texture Streaming、GC Alloc 和大于 33 ms 的尖峰来源。

通过要求：

- 平均达到 60 FPS；
- P95 frame time 不高于 18 ms；
- 无规律性大于 33 ms 的 Cloth 尖峰；
- warm-up 后 Cloth adapter 和状态切换无每帧托管分配；
- 不出现持续 Streaming 模糊、抖动或 Bounds 闪烁。

### 26.3 优化顺序

1. 实时阴影；
2. Magica Cloth 状态、粒子和碰撞体；
3. Shader Pass 和 Variant；
4. Renderer 与材质；
5. 隐藏身体面和内部发束；
6. Outfit 内部面与小配件；
7. 当性能捕获确认几何处理为瓶颈时，再分析 `_LOD0` triangles、隐藏面和内部结构。

不通过新增 LOD 绕过首版约束。面数只作为性能归因数据；没有实测瓶颈时，不因 triangles 数量本身触发阻断或返工。

## 27. AI-first 里程碑

### M0：参考冻结

交付三视图、头部近景、服装爆炸图、尺寸表、Color Palette / 16 Surface Profile 选择说明和 AI 输入包。完成标准是不可见结构、角色比例、服装层级和商业授权清楚。

### M1：AI 候选门禁

交付 AI 原始网格、UV、材质、provenance、Blender 审计报告和修复 / 重生成决定。局部修复预计不超过一个工作日，不需要重做超过 20% 网格。

### M2：Blender 几何收敛

交付一个主体 Mesh、共享色板材质、可追踪的几何统计、已确认问题的隐藏面处理、受控平滑法线、合格 PaletteUV / 顶点色和灰模动作检查。面数不作为该里程碑的通过条件。

### M3：Humanoid 与表情

交付 65-80 骨 Humanoid、手工修正蒙皮和代表性姿势。Unity 必须可建立 `ValidHuman`。

### M4：材质 LookDev

交付共享 256×256 RGB565 Color Palette、65,536 唯一地址证明、Color×16 Profile 对照、PaletteUV / PaletteData 语义证据、Stylized Lit 结果和正侧背截图。结果无 RGB alias、统一油亮、Profile 串色或高频闪烁。

### M5：Magica Cloth 与 Unity 导入

交付 Hair / Garment BoneCloth、两个 Controller、一个 System、Avatar、Prefab、Bounds、Spawn 和穿插矩阵。

### M6：30+ 人实机验收

交付固定场景、60 秒性能捕获、2 / 4 / 24+ Cloth 分级证据、CPU / GPU / Cloth / Draw Call 报告和问题处理记录。

## 28. 工作量估算

AI 候选通过门禁且项目已有 Shader、Character Authoring 与 Magica Cloth 时：

| 阶段 | 建议工期 |
| --- | ---: |
| 参考和 AI 输入整理 | 1-2 天 |
| AI 候选生成与筛选 | 0.5-2 天 |
| Blender 几何、UV 和材质分区修复 | 2-5 天 |
| Humanoid、蒙皮和姿势修正 | 3-6 天 |
| PaletteUV、顶点色和色板分区 | 1-3 天 |
| 贴图清理与 LookDev | 2-5 天 |
| Magica Cloth | 2-4 天 |
| Unity 导入、动作和 30+ 人性能验收 | 2-5 天 |

总体约 2-5 周。AI 候选未通过 M1 时应重新生成，不把工期无限投入到坏拓扑修复。

## 29. 最终交付清单

### 29.1 AI 与 Blender 源

- [ ] AI 原始模型、UV 和材质；
- [ ] provenance、许可证和源文件 hash；
- [ ] Blender 工作文件与 Final 权威源；
- [ ] 自动审计报告；
- [ ] 一个主体 Mesh / 共享色板材质统计。

### 29.2 正式角色资产

- [ ] `_LOD0` Humanoid FBX；
- [ ] 已记录 Blender 三角化后的实际 triangles，且没有把面数本身作为交付门禁；
- [ ] 65-80 bones；
- [ ] 每顶点最多 4 influences；
- [ ] PaletteUV、顶点色语义和眼球骨；
- [ ] 共享色板 Atlas、PaletteUV 与顶点色；
- [ ] FaceDetailUV、共享 2K 彩色 FacePart Atlas 与 0–15 FacePreset；
- [ ] 受控平滑法线与结构硬边；
- [ ] 稳定材质槽；
- [ ] 骨骼、Cloth 和导出设置说明。

### 29.3 Unity 验收

- [ ] `ValidHuman`；
- [ ] 一个主体 Renderer / 共享材质；
- [ ] 无 `LODGroup`；
- [ ] Hair / Garment Cloth Controller；
- [ ] Bounds 正确；
- [ ] Prefab Build 与 Runtime Spawn 通过；
- [ ] 动作穿插矩阵；
- [ ] 30+ 人 Android 性能证据。

## 30. 最小执行顺序

1. 冻结三视图和服装层级；
2. 生成 AI 拓扑候选；
3. 在 Blender 做一次只读审计；
4. 未通过门禁则重新生成；
5. 通过后修复几何、受控平滑法线、UV 和材质分区；
6. 收敛到一个主体 Mesh、共享色板材质，并记录非门禁的几何统计；
7. 重建 Humanoid 和蒙皮；
8. 完成 PaletteUV、顶点色与彩色 FacePart 面部细节；
9. 完成 PaletteUV / 顶点色 / Stylized Lit LookDev；
10. 配置 Hair / Garment BoneCloth；
11. 导出并重新导入 Blender 验证；
12. 经 Character Authoring 和 Unity Import Bridge 导入；
13. 验证 Avatar、Prefab、动作、Cloth 和 Spawn；
14. 执行 30+ 人 Android 60 FPS 门禁；
15. 按固定优化顺序收敛单一 `_LOD0`。

这条路线的核心不是“AI 已拓扑，所以无需美术”，而是把传统高模和全量 Retopo 成本替换为严格的候选拒绝门禁、局部修复、绑定、材质、布料和实机验收。
