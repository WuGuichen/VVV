# CharacterStudio 数据归属与制作入口

> Status: Current
> 本文维护角色数据的所有者、写入入口和消费边界；角色武器合同仍以 `Docs/CHARACTER_WEAPON_CONFIGURATION.md` 为准。

## 数据清单

| 数据 | 角色源位置 | 编辑 / 保存权威 | 消费方式 |
| --- | --- | --- | --- |
| 身份、依赖、格式版本 | `manifest.json` | 角色管理、Authoring Service | 包发现、编译、导入 |
| 资源引用 | `resource_catalog.json` | 原材料与资源绑定；公共资产由 Resource Manager 管理 | 资源编译与导入 |
| 身体、质量、基础碰撞尺寸 | `geometry/body_geometry.json` | 身体编辑 | 身体配置、物理绑定 |
| 部位、受击体、命中区 | `geometry/body_parts.json`、`body_colliders.json`、`damage_zones.json` | 对应身体 / 受击体编辑 | Combat 几何与目标语义 |
| Socket、语义骨骼、武器挂点 | `geometry/sockets.json`、`weapon_slots.json` | 角色挂点编辑 | 默认装备与运行装配 |
| 接战位置 | `geometry/engagement_anchors.json` | 接战锚点编辑 | 接近目标选择 |
| 部位伤害、Body Presence | 独立 geometry 文档 | 专用 API；整包保存保留已持久化文档 | 各自编译绑定；核心 Presence 体积当前只读 |
| 接触音频点 | `geometry/body_part_contact_points.json` | 接触音频点编辑 | 表现音频，不作为伤害权威 |
| 基础数值与聚限恢复参数 | `config/character_attributes.json` | CharacterStudio 基础属性、同一 Authoring Service 保存 | `CharacterAttributeProfileConfig` → Gameplay 属性与散势组件 |
| 默认组合、组合引用、角色覆盖、镜头 | `config/character_application.json` | 绑定 / 组合工作区、镜头编辑 | 编译期解析有效装配 |
| 公共武器、组合及行为默认 | `WeaponSource/global_weapon_library.json` | 公共库工作区 | 角色只保存引用与明确覆盖 |
| 公共 / 角色专属动作正文 | Common 动作源 / `config/character_actions.json` | Character Action Authoring 及动作源 API | 编译动作库；普通整包保存不覆盖旧动作副本 |
| 默认战术动作、行为卡及卡槽绑定 | `config/character_tactical_defaults.json` | 默认战术内容页面、Authoring Service | 编译与导入后仅初始化玩家清单；实际持有及绑定归单一玩法存档 |

`CharacterResourcePackage` 是加载与制作聚合对象，不代表所有成员都由整包保存拥有。`CombinedWeaponConfigurations`、动画解析集合、`WeaponAttachments`、`Traces` 是解析投影；显示这些集合不能创建第二个源文件权威。

## 基础属性合同

属性源为可选的 schema `1.0` 文档。正式字段为 `hp`、`attack`、`defense`、`baseCohesion`、`scatterRecoveryRate`、`scatterRecoveryDelayFrames`、`cohesionBreakRecoveryDelayFrames`。HP 和基础聚限必须为正整数，其余为非负整数；未知版本拒绝保存和编译。质量和几何不复制到此文档。

新属性档案默认值为 100 / 20 / 10 / 100 / 2 / 360 / 270。Stamina 保持现有初始值 100 和闪避机制，本轮不扩展其编辑入口。当前生命、散势债务、当前聚限、当前装备和动作进度属于运行时，不写回角色源。

HP / Attack / Defense 上限为 16777215，以保证现有 float 属性档案与整数 Gameplay 初始化之间的精确转换；超出范围拒绝编译。

角色数值编译为现有属性档案。HP、Attack、Defense 使用 CharacterTest 正式消费身份 1 / 2 / 3；Stamina 保持 920002，继续服务于现有闪避机制。聚限和恢复参数是档案中的初始化参数，运行后由独立 `GameplayScatterPressureComponent` 维护。旧包 HP 920001 仅在 CharacterTest 的生成适配边界映射，不产生第二份 HP 状态。

## 编辑入口与派生数据

普通入口区分基础属性、身体与受击、资源绑定、动画 / 动作引用、导入和验证；高级角色配置只展示源引用，解析结果明确只读。动作正文、公共库、部位伤害与 Presence 的原有专用保存路径继续保留。

「身体与受击」通过「整体身体 / 移动碰撞配置 / 部位与受击体」三个入口浏览现有身体源、部位与受击体；移动碰撞页面仍编辑同一 `bodyProfile.defaultCapsule`，不新增独立配置。部位与受击体列表选择联动属性面板和预览，父级分类保持高亮；预览范围单独控制可见性，不改变当前编辑对象，也不写入角色源。

`Assets/MxFrameworkGenerated` 中的配置、Prefab、烘焙和导入回执属于生成结果或验证证据。浏览器草稿、界面位置和工具缓存不属于角色定义。玩家选择的牌组由玩家装配流程维护；关卡只引用已发布角色并安排出生位置、队伍和控制归属。

## 生成产物的四层边界（2026-09-19）

导入产物的所有权按四层划分，不得互相替代：

| 层 | 载体 | 写入权威 | 消费边界 |
| --- | --- | --- | --- |
| 角色源 | `Authoring/CharacterPackages/<id>/`（`manifest.json`、`resource_catalog.json`、`geometry/`、`config/`） | CharacterStudio 与 Authoring Service | 编译与导入的唯一输入；不因导出精简而被改写 |
| 正式派生配置 | `config/character_config_patch.json`、`config/geometry_binding.json`、`config/resource_catalog_mapping.json`、`config/runtime_resource_catalog.json`、`config/character_resource_plan.json`、`config/animation_set_definition.json`、`config/animation_clip_registry.json`、`config/animation_resource_plan.json`（**v2 五字段运行投影**）等 | Authoring Compiler / Authoring CLI（唯一 writer） | Runtime `CharacterImportedPackageJson.LoadFromDirectory` 与 Unity 导入桥 |
| 轻量摘要 | `config/../package_cache/compile_summary.json`（`mx.characterPackageCompileSummary.v1`） | Authoring CLI，导入成功收口后发布 | **仅供工具展示**。不是加载清单、不是发布门禁、不是身份数据库：Runtime 不读、Readiness 不得依赖、Player 暂存不带。缺失或版本未知时降级为"未提供摘要"，不得因此判定包不可加载 |
| 外部诊断 | 由 `character import-unity --diagnostics-out <dir>` 显式导出的完整编译对象、动画对象、导入报告与阶段 manifest | Authoring CLI，仅在调用方显式请求时产生 | 排障与审计。不写入正式包、不进入 `Assets/` 等仓库目录、不参与门禁 |

配套约束：正式 `config/` 文件只写入 `config/`，`generated/` 不再保存同名镜像；废弃快照与未被本次导入声明的孤立资源由导入成功路径按白名单迁移删除，删除前备份并做哈希前置校验，导入未完整成功时不执行迁移。详见 `Docs/CHARACTER_PACKAGE_EXPORT_SIZE_OPTIMIZATION_PLAN.md`。

`config/animation_resource_plan.json` 有一处需要特别区分：正式包里它是 `mx.animationResourcePlan.v2` 的**五字段运行投影**（身份 + 语义 `planHash`），而完整 `mx.animationResourcePlan.v1`（含 runtime catalog、加载分组、audio manifest、diagnostics）属于**制作数据**，只存在于编译对象、制作 API 与显式诊断输出中。因此"完整动画资源计划"不是角色源，也不是正式运行配置；CharacterStudio 展示完整计划时应读取编译对象或显式诊断导出，不得从正式包文件反推被裁字段。详见 `Docs/CHARACTER_ANIMATION_RESOURCE_PLAN_RUNTIME_EXPORT_TASK.md`。

**专用预览 GLB 的边界（2026-09-20）**：`usage=previewMesh` 且有效源格式为 GLB 的资源属于**制作侧专用**：

- 源侧保留完整条目与文件：`resource_catalog.json` 的资源键、stableId、相对路径、内容 hash、provenance 与 preview 元数据都不删，CharacterStudio 继续从作者源读取它做预览。
- 运行侧一致排除：`resource_catalog_mapping.json`、`unity_resource_catalog.json`、`runtime_resource_catalog.json`、`character_resource_plan.json`、`character_config_patch.json` 的表现资源键、resolver verification plan 均不含该运行引用；生成包不再复制该 GLB。
- 因此界面对它的运行可用性标注为"制作预览，无需 Unity 导入"（`AuthoringPreviewOnly`），**既不伪装成 `RuntimeReady`，也不因缺少生成副本而报运行缺失**。
- 同一物理文件若同时被正式 body/weapon 条目声明，属用途冲突：文件受保护并报 `CHAR_PREVIEW_GLB_RUNTIME_REFERENCE`，需资源所有者显式分类。详见 `Docs/CHARACTER_PREVIEW_GLB_AUTHORING_ONLY_TASK.md`。

## 整体身体字段与预览（2026-09-13）

- CharacterTest `CharacterTestCharacterDefinitionRegistry.CreatePhysicsProfile` 优先读取 `defaultCapsule` 的有效高度、半径；仅在对应值无效时后备使用 `heightMeters` / `radiusMeters`。质量大于 0 时作为生成物理质量，否则使用游戏默认质量。
- `bodyScale` 会进入源与导入 DTO，但当前预览和 CharacterTest 没有缩放消费者。UI 保留已有值、只读展示并标明未启用，不通过界面整理引入新的模型或碰撞缩放语义。`profileId` 同样只读。
- 「整体身体」的高度标尺、地面半径圆明确表示声明尺寸，地面基准为角色空间 Y = 0；它们不表示实际模型边界或实际移动盒体。修改辅助线显示状态不改变角色源、模型变换或碰撞配置。
- 现有 FBX 预览单位适配仍可使用声明身高修正明显单位差异；该流程有比例阈值，且不适用于 GLB，因此界面不将参考身高描述成模型的精确缩放控制。移动胶囊代理、世界注册盒体与源胶囊在移动碰撞页分别展示。
- `smoke:body-overview` 验证字段分组、中文枚举显示、单位与帮助文案、只读身份与倍率、折叠偏好、3D 辅助几何的米制尺寸和释放、SVG 信息、源数据隔离及辅助线开关；真实浏览器视觉验收需单独完成。

## 移动碰撞的源值与生效值（2026-09-13）

源字段仍为 `defaultCapsule.height/radius/center`。当前 CharacterTest 按 `CreatePhysicsProfile`、物理组件毫米转换、`CharacterTestCollisionProfileResolverSystem.ResolveProfile(standing)` 的顺序处理源值：站立半径不超过半高，中心 Y 限于半径到高度减半径之间；中心 X/Z 不被移动消费者使用。`GetCharacterHalfExtents` 使用整数半高、半径；这些 half-extents 同时用于 `SyncCharacterCombatCollider` 注册 AABB，以及 `CombatMotionConfig` 通过 `CombatMotionCapsuleProxy.FromHalfExtents` 构建胶囊代理。奇数毫米高度会在整数半高转换时少 1 mm，代理与注册体共享转换后的外包围尺寸。

注册体与移动求解必须分开描述。`CombatKinematicMotor.TryBuildCollisionFromCandidate` 对 AABB 目标走 `TrySweepAabb`，对 OBB 目标走 `CombatMotionObbCast` 胶囊 cast，对 Sphere/Capsule 目标走水平圆形判定与高度重叠。因此不能把注册的 AABB 概括成所有移动的唯一实际形状，也不能把胶囊代理的存在概括成统一胶囊求解。对应当前合同见 `Docs/Interfaces/Combat.md` 的 Motion 章节。

CharacterStudio 的前端投影只用于即时对照与预览，不参与保存、编译校验或运行时决策。显示器以当前站立消费者为尺寸权威，相关规则变化时必须更新 Unity 对照样本与 `smoke:movement-collision`。2026-09-13 已从运行中的 Unity 只读执行站立规则并读取 8 组结果，包括半径超限、零/负/过高中心、最小尺寸和毫米取整；前端投影逐字段匹配。这些样本只验证参数计算，未验证移动扫掠或不同目标形状下的完整碰撞行为。

默认同时显示青色移动胶囊代理与紫色注册阻挡盒体，注册盒体可独立隐藏；橙色源胶囊是另一项可选参考。右侧说明当前不同障碍的检测分支，避免把任一预览形状标成统一的实际碰撞形状。源 X/Z 只读保留且不清零；原有 `RootCapsuleCenterXZNotConsumed` 校验继续保留。无效源胶囊明确提示、不绘制替代尺寸。底部对齐操作以代理和注册体共有的半高写入源中心 Y，一次包撤销事务完成，已有 X/Z、尺寸与其他角色字段均保持原值。

## 验证边界

### 来源操作的草稿边界

源清单检查只读取已保存来源的 SDK 状态，不替换未保存的来源选择；目录、文件/公共引用或目标声明变化后，旧检查结果失效，卡片保持待检查。源清单保存仅确认本次提交的文档，提交期间的新编辑仍为草稿。材质语义、表面模式、Alpha cutoff 和资源用途继续写入原有 target，不因准备保存文档而丢失。

同步将服务器包的前后变化合入当前编辑包，不调用重置整包的加载流程。干净基线与 Undo/Redo/当前事务中的包快照一起接收相同的资源变化，保留局部角色编辑。全局武器库和独立部位伤害草稿不替换；独立文档的前后读取必须都匹配包版本、且磁盘内容与状态相同，才前移当前草稿与历史中的版本见证。外部变动或读取失败保留原版本并显示待核对信息。

同字段冲突保持本地草稿值与原包版本，并显示具体路径；外部版本已变化时继续由现有保存版本检查处理。操作响应必须匹配发起时的包实例。失败或迟到响应不清空草稿，409 携带的部分同步状态和阻塞证据不得丢弃。前端回归用模拟接口验证这些控制流程；它不证明任意真实源文件已经导入成功。

编辑属性必须经过源校验、编译、导入和新角色实例初始化。属性改动进入编译摘要；非法属性不能写入源或替换已发布数据。已有文件缺失按默认值读取，首次在属性入口编辑并保存时形成明确源文件；其它编辑不得顺带复制质量、几何或公共动作数据。

### 部位与受击体编辑入口

常规新增、复制、删除和模板仍编辑 `geometry.bodyParts/colliders` 并使用现有包保存/撤销流程。新增受击体默认引用当前部位、默认命中区和骨骼（或定位点/部位空间），不创建另一份受击数据。模板生成仍由 Authoring SDK 处理，浏览器仅提交现有参考骨骼和映射、接收草稿增量。

部位 ID 和受击体 ID 作为固定引用只读展示。普通形状、归属编辑保留现有 ID，不再因 `_sphere/_capsule/_box` 后缀自动重命名。创建和复制仍生成唯一 ID。源 `physicsLayer` 原样保留并只读展示；当前 CharacterTest 受击注册固定使用 `CharacterHurtbox` 层，界面说明运行时用途而不覆盖源记录。

部位工作区的 `bodyPartsPreviewScope` 是唯一范围状态（selected/part/all），只控制可见性。空集合、选择部位和选择受击体分别显示可用模式，不写入角色源，也不覆盖其它工作区的 `hurtboxDisplayMode` 或图层偏好。前端回归覆盖当前部位新增与撤销、范围切换、固定 ID、源层显示，以及模拟模板 API 成功/失败时的就绪与选择反馈。

## 本轮验证记录（2026-09-12）

- Authoring `character-basic-attributes` 通过：默认与自定义属性、源和编译摘要变化、序列化重开、非法值、整数精度范围和未知版本。
- 隔离角色副本的真实保存接口通过：HP 240、基础聚限 140 保存重开并编译一致；HP 0 与 schema 99.0 返回 422，源文件未被修改。
- CharacterStudio `smoke:base-attributes` 通过：主入口、默认值只读不制造草稿、编辑数据进入角色属性且不改身体/武器引用。
- Unity 属性初始化、旧包 HP 映射、生成组件与 CharacterApplication Schema 聚焦回归 10/10 通过，作业 `53351e73cd504bb9ad0a39ce67839937`。
- 000001 / 000002 / 000003 / 000004 / 000007 / 000008 的角色属性源与已导入属性配置一致，CLI 数据导入零错误、零冲突。
- 浏览器集成初始化失败，桌面控制通道不可用；真实页面点击与视觉验收未完成，不以逻辑检查代替该项。
