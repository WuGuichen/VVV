# Character Cloth 接口

> Status: Current
>
> 当前实现：`MxFramework.Character.Cloth` noEngine 控制契约 + 可选 `MxFramework.Character.Cloth.Magica.Unity` 表现适配。

## 1. 模块边界

| 程序集 | 依赖 | 职责 |
|---|---|---|
| `MxFramework.Character.Cloth` | 无 Unity、无第三方依赖 | 状态、系统覆盖、控制器接口与集中服务 |
| `MxFramework.Character.Cloth.Magica.Unity` | Character.Cloth、UnityEngine、MagicaClothV2 | 把框架状态映射为 Magica Cloth 组件生命周期 |

`MxFramework.Character.Cloth` 可以在服务器、noEngine 测试和未安装布料插件的 Consumer 中独立编译。Magica 适配程序集受 `MAGICACLOTH2` define constraint 保护；该标识由 Magica Cloth 2 在导入项目后设置。第三方 Magica 源码位于框架 package 根之外，不进入基础 UPM package。

Magica 使用自己的 Burst/Jobs 布料求解与碰撞数据，不以 Unity 内置 `Cloth`/PhysX 组件作为本接口的权威。无论采用哪种后端，布料都只属于 Unity 表现层。

## 2. 状态语义

| 状态 | Magica 映射 | 恢复行为 |
|---|---|---|
| `Running` | 组件启用，`timeScale=1` | 继续模拟并写回表现 |
| `Paused` | 组件启用，`timeScale=0` | 保持当前姿势，不推进模拟 |
| `DisabledReset` | `disableMode=Reset` 后禁用组件 | 停止团队模拟并恢复创作/动画姿势 |
| `DisabledKeep` | `disableMode=Keep` 后禁用组件 | 停止团队模拟，重新启用后从保留姿势恢复 |

控制器同时保存 `RequestedState` 与 `SystemOverride`。系统覆盖不会改写角色自身请求：

- `None`：使用角色自身请求。
- `Paused`：只把本来 `Running` 的控制器暂停；本来已禁用的控制器保持禁用。
- `DisabledReset` / `DisabledKeep`：强制覆盖全部已注册控制器。
- 清除覆盖后，每个控制器恢复到覆盖前的本地请求。

## 3. 公共 API

- `ICharacterClothController`：单个角色或服装组的后端无关入口。
- `ICharacterClothService` / `CharacterClothService`：显式注册控制器并广播覆盖、Reset。
- `CharacterClothStateResolver`：确定性解析本地请求和系统覆盖。
- `MagicaCharacterClothController`：管理一个或多个 `MagicaCloth`，并在异步 Build 完成后重新应用状态。
- `MagicaCharacterClothSystem`：Unity 组合根，可自动收集子级控制器，也支持运行时角色显式 Register/Unregister。
- `ICharacterImportedPackagePrefabPostprocessor`：Editor-only 低频扩展点；可选集成在生成角色 Prefab 保存和 Runtime Resources 镜像前投影表现组件。`CharacterImportedPackagePrefabPostprocessContext` 只暴露当前生成根、ModelRoot、BodyModel、导入根和已解析 package，不允许反向修改 Authoring 源包。

集中开关示例：

```csharp
using MxFramework.CharacterCloth.Magica.Unity;

MagicaCharacterClothSystem clothSystem = characterRoot.GetComponent<MagicaCharacterClothSystem>();

clothSystem.SetPaused(true);                         // 全局暂停，保留局部禁用状态
clothSystem.SetPaused(false);                        // 恢复各角色本地请求
clothSystem.SetEnabled(false, keepPose: false);      // 完全关闭并复位
clothSystem.SetEnabled(false, keepPose: true);       // 完全关闭并保留姿势
clothSystem.SetEnabled(true);                        // 恢复各角色本地请求
```

角色独立控制示例：

```csharp
using MxFramework.CharacterCloth;

controller.SetRequestedState(CharacterClothState.DisabledKeep);
controller.SetRequestedState(CharacterClothState.Running);
controller.Reset(keepPose: false);
```

## 4. Unity 组装约定

1. 在包含一组 `MagicaCloth` 的角色或服装根节点添加 `MagicaCharacterClothController`；默认自动包含 inactive 子节点。
2. 在角色根或场景布料组合根添加 `MagicaCharacterClothSystem`；默认自动收集其子级控制器。
3. 运行时生成的角色若不在系统层级下，由生成组合根调用 `Register`；销毁/回收前调用 `Unregister`。控制器销毁时也会从所属系统移除。
4. 同一控制器只允许归属一个系统，防止两个全局开关互相覆盖。
5. `RefreshClothComponents`、`RefreshControllers` 和 Register 是低频生命周期操作；帧循环中只使用状态切换，不重复做组件发现。

### 4.1 Character Package 正式 Magica 投影

角色包可在 `manifest.json.configFiles[]` 声明：

```text
config/character_cloth_magica.json
```

当前读取兼容 schema `1.0`；带 typed `parameterOverrides` 的当前写入版本为 `1.1`，`backend` 固定为 `MagicaCloth2`。配置保存 BoneCloth 条目、相对 `BodyModel` 的完整根骨路径、连接模式、项目内 Preset 路径与 `presetSha256`、可选物理参数 Override，以及 Magica Sphere/Capsule Collider。Override 只覆盖显式声明字段，未声明字段继续继承 hash-frozen Preset；标量 Override 会关闭该字段的 Preset 深度 Curve。Authoring CLI 在 `character import-unity` 时把该文件复制到生成包同名 `config/` 路径；`MxFramework.Character.Cloth.Magica.Unity.Editor` 的 Prefab 后处理器读取配置并完成：

1. 按完整路径唯一解析 BoneCloth 根骨和 Collider 父骨；缺失或重名时阻断 Prefab 生成。
2. 校验 Magica 参数 Preset 的 SHA-256 后加载；插件升级导致 Preset 内容变化时阻断生成，不静默改变角色物理参数。
3. 在 Preset 基线上应用基础运动、惯性、形状保持、碰撞、风和弹簧 typed Override，并执行范围校验。
4. 创建 `MagicaCloth`、Root Bones 和 Collider List。
5. 对 Collider 做当前导入模型等比缩放补偿，生成时要求最终 world scale 可用。
6. 在角色根添加 `MagicaCharacterClothController` 与 `MagicaCharacterClothSystem`。
7. 同一结果保存到 Preview Prefab，并镜像到 Runtime Resources Prefab。

该流程的事实源是 Authoring package JSON；`Assets/MxFrameworkGenerated/CharacterPackages/...` 是可重建投影，不得靠手工修改生成 Prefab 持久化 Cloth。

当前投影只接受 `BoneCloth`。MeshCloth 的顶点选择、paint map、renderer 写回和性能门禁尚未形成角色包合同，因此未知 `clothType` 必须阻断，不静默回退。

### 4.2 Cloth Authoring 当前入口

Unity 菜单：

```text
MxFramework → Character → Cloth Authoring
```

当前窗口直接从 Character Package 清单列出全部有效角色，不以 Cloth 配置是否已存在过滤角色。已配置角色直接加载 Authoring package JSON；未配置角色打开 schema `1.1` 空草稿，首次保存同时创建 `config/character_cloth_magica.json` 并登记到 `manifest.json.configFiles[]`。空草稿可执行“根据模型数据初始化”：扫描当前 Preview 的 `SkinnedMeshRenderer` 骨骼层级和实际顶点权重，按 WGame 基础骨命名规则识别 Custom 骨链，排除胸部链、单骨链和零权重链，并按共同基础身体区域合并 Root；同时按角色高度与真实基础骨段拟合最小防穿模 Collider 集。当前规则将骨盆、上胸、左右上臂候选整理为最多三个初始 Cloth：下半身 Cloth 只关联左右 thigh Capsule；每个单侧上半身 Cloth 关联一个从 `pelvis.x` 到 `neck.x` 的 `torso` Capsule，以及同侧 upper-arm Capsule。`000004` 完整初始化为三个 Cloth、六个 Collider，分配为 `2 + 2 + 2`。初始化后自动显示骨骼并选中首个 Collider；该操作不保存、不重建，Preset、参数和 Collider 继续由用户微调。已有空 Collider 草稿可执行“根据模型整理 Cloth + Collider”；当全部 Collider 为空时，需确认后按同一分组替换当前内存草稿。部分 Cloth 已有 Collider 时，“根据模型补全 Collider”只填空项，不覆盖已调校内容。已有完整配置始终显示“根据模型重新初始化…”；确认提示会同时展示当前与模型建议的 Cloth/Collider 数量，确认后替换内存草稿中的 Root 分组、Preset、参数和 Collider，支持 Undo，只有再次保存并重建才修改磁盘。左侧导航将固定高度 Cloth 列表与当前 Cloth 的 Collider 列表拆开，CRUD 操作条保持固定位置，选择变化不重排导航结构。窗口提供 Cloth/Collider CRUD、可搜索 Bone Picker、Sphere/Capsule 快捷模板、Scene View Handles、Undo/Redo、expected-hash 原子保存、Rebuild/parity/receipt 和 focused PlayMode 验收。Preview 使用生成 Prefab 的只读 Prefab Stage；窗口清理 Magica 初始化造成的假 dirtiness，不保存生成 Prefab。运动预览在 Play Mode 脚本重编译或 Domain Reload 后重新创建 Runtime 前，会清理专用 Layer 与 DontSave 标记匹配的遗留预览环境，避免旧 T Pose 角色与新实例重叠。

角色 Prefab 缓存键包含生成包 `config/**/*.json` 的稳定内容聚合 hash，Cloth 配置变化会使缓存失效。Cloth Authoring 的“保存并重建”默认使用 Cloth 专用快速路径：Authoring CLI 仍负责配置复制、写入计划和 package/import hash，但跳过 Unity 侧资源目录、公共武器、动作、材质、Socket、Hurtbox、场景与完整角色构建；Editor 只同步生成 Cloth JSON，分别打开现有 Preview/Runtime Prefab Contents，删除旧 Magica 投影、应用新文档、刷新控制器数组并保存，最后将 import report 的身份 hash 写回现有 Unity receipt 并执行 Cloth/Collider parity。基础 Prefab/receipt 缺失、快速投影失败或一致性不通过时自动回退完整角色导入。结果消息公开 `Fast path`/`Full path` 与耗时，避免把缓存命中误认为重建成功。

运动预览按 Character Package ID 在 EditorPrefs 中保存最后选中的稳定 `clipId`，不保存易受排序变化影响的列表索引。重新进入预览、重新打开窗口或脚本 Domain Reload 后，动画目录先过滤掉无法加载的 Clip，再按 `clipId` 恢复选择；只有记忆动作不再存在或其 AnimationClip 无法加载时才切换到第一个可加载默认动作，并将该默认动作写回记忆。运行时重建动画图失败时同样只回退默认动作。

所有模型初始化 Cloth 默认使用 `MC2_Preset_Cape.json`，不再按骨链位置自动套用 Tail。用户可在 Cloth 工作区的 Preset 下拉框中改选 `Assets/MagicaCloth2/Res/Preset/*.json` 标准 Preset；选择动作同时更新 `presetAssetPath` 与 `presetSha256`，保留现有参数 Override，并进入正常 Draft/Undo/校验/运动预览热应用链路。

框架在 `Character.Cloth.Magica.Unity/Presets` 提供 `WGame 柔软布料`、`WGame 中等布料`、`WGame 硬挺布料` 三个由 Cape 派生的完整快速 Preset，并与 Magica 原生 Preset 一同出现在下拉框。柔软档使用 `damping=0.07`、`world/local inertia=0.85/0.90`、`angle limit=70`；中等档使用 `0.10`、`0.70/0.80`、`60`，必须具备清晰的主动惯性拖尾；硬挺档使用 `0.16`、`0.45/0.60`、`40`，跟身更快但仍保留可见二级运动。三档同时逐级调整速度上限、形状恢复、速度衰减、弹簧和风影响，避免中等/硬挺档只在触发 Collider 时才显得有运动。为保证档位结果不被旧配置覆盖，应用 WGame 快速 Preset 时若当前存在参数 Override，窗口先确认再清除 Override；Root、Collider 和 Cloth 分组不变。切换 Magica 原生 Preset 继续保留 Override。

模型初始化会在 Cape 上叠加兼顾急停与柔软度的运动 Override：World Inertia 控制在中低范围以减少停止后的整体前冲，Local Inertia 保持更高以允许骨链自然弯曲。下装默认 `damping=0.14`、`world inertia=0.55`、`local inertia=0.70`、`movement smoothing=0.55`、移动/粒子速度上限 `3.5`、旋转速度上限 `540`；单侧上装默认 `damping=0.17`、`world inertia=0.40`、`local inertia=0.60`、`movement smoothing=0.60`、移动/粒子速度上限 `3.0`、旋转速度上限 `480`。Gravity、Angle Limit、形状恢复和碰撞参数继续继承 Cape，用户可以在结构化 Override UI 中逐项恢复 Preset 或微调。

Scene View 骨骼 Overlay 将完整的已配置 Root 子树视为 Cloth 模拟骨链，不再因子骨名称包含 `pelvis`、`arm` 等核心词而被次级骨过滤器隐藏；Cloth Root 使用亮青色，后续模拟骨链使用绿色粗线，Collider Parent 使用橙色。窗口 Cloth 行显式标注 `BoneCloth · Root 数 · Preset`，与下方独立 Collider 列表区分。

静态 Prefab Authoring 的骨骼 Overlay 与 Root 聚焦使用 `SkinnedMeshRenderer` 的 bind pose 计算显示位置，不直接信任可能被 Magica 编辑器初始化或上一帧模拟暂时改写的 `Transform.position`；运动预览处于 Play Mode 时继续显示实时骨骼位置。Overlay 绘制已处于世界空间，进入 Scene GUI 绘制段时必须显式把共享 `Handles.matrix` 设为单位矩阵，并在完成后恢复，避免其他 Collider/Editor Handle 遗留的父骨矩阵对骨点执行第二次变换。由此静态编辑不会因瞬时模拟姿态或回调顺序把骨链画到模型之外，同时运动预览仍能观察真实摆动和异常。

Scene View 的 Collider 线框与变换 Handle 只在 `Collider` 页显示和响应；切换到 `Cloth` 页时隐藏 Collider Handle，只显示 Cloth Root 与完整模拟骨链，避免粗碰撞体线框遮挡骨链可视化。

Collider Inspector 的 `Parent Bone` 提供相邻骨骼导航与完整骨架搜索两个入口。相邻导航只收集当前骨骼在 `SkinnedMeshRenderer` 实际骨架中的直接父骨和直接子骨，并用上一级/下一级标示方向；只有一个候选时直接切换，存在多个候选时显示下拉菜单，没有合法邻骨时提示改用“全部…”搜索。切换操作进入同一 Draft/Undo/校验/运动预览热应用链路。

Unity 角色导入若在当前 BodyModel 上检测到任意 `MAGCLOTH004` Root 路径或 `MAGCLOTH006` Collider 父骨路径失效，将旧 Cloth 文档整体视为模型版本失配：使用当前模型重新初始化完整 schema 1.1 Cloth/Collider 数据，同时 expected-hash 保存 Authoring 与 Generated 配置，并排队再次导入以刷新 package hash、receipt、Preview 与 Runtime Prefab。该恢复不保留旧 Root 分组、Preset、参数 Override 或 Collider 微调；非骨路径类配置错误继续阻断，不触发自动重置。

WGame Rig 的正式“发布 FBX 并更新 Unity”是主重置入口：FBX 回读 audit 保存由全部骨名、父级和 Deform 标记组成的稳定 hierarchy hash，并与上一版正式 audit 比较。只有 hierarchy hash 变化时，发布事务才会把旧 `character_cloth_magica.json` 备份到同次 `publish_backups/<timestamp>/`，随后将 Authoring 配置原子重置为 schema 1.1、`enabled=false`、`cloths=[]`，再继续模型同步与 Unity 导入；材质、贴图或仅权重变化且骨架层级未变时保留 Cloth 调校。Unity 导入期的路径失效全量重置继续作为绕过 WGame Rig 发布路径时的兜底。

左侧对象导航在 Cloth 列表与 Collider 列表之间显示当前 Cloth 的 Root 骨链子列表，每行包含 Root 名、展开后的骨骼节点数和选中状态。点击骨链会切换到 `Cloth` 页、开启骨骼 Overlay、聚焦该 Root 子树，并以黄色 Root 与亮绿色骨链突出显示；其他 Cloth 链降亮度。`Cloth 结构与 Preset → Root Bones` 同步提供选中/聚焦、替换和移除操作。单条 Root 的选择只控制导航与结构编辑，物理参数仍作用于所属整个 Cloth；需要独立参数时应拆成独立 Cloth。

CharacterStudio readiness 显示只读 Cloth/Collider/Preset/receipt 状态，并通过 `/api/character/cloth-magica/open` 请求 Unity 打开相同角色；未配置角色提供“在 Unity 中创建”入口。CharacterStudio 不复制 Magica Scene Handle 或求解器。

## 5. 权威与禁止事项

- 布料结果不得写入 Character/Gameplay/Combat 权威骨骼、命中挂点、Runtime hash、SaveState、Replay 或网络同步。
- 动画与权威 Character pose 先完成，Magica 只在表现阶段读取并修饰衣物、头发等次级骨骼或网格。
- 角色战斗碰撞、受击点和 projectile socket 不得绑定到会被布料求解改写的 Transform。
- Gameplay、Combat、Character Control 和 Runtime AI Planner 不得引用 Magica 类型；开关由 Unity 组合根或玩家画质设置适配器调用。
- 不修改 `Assets/MagicaCloth2` 内的供应商源码来实现框架行为。

## 6. 验证入口

- 纯状态与广播行为：`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterCloth/CharacterClothServiceTests.cs`。
- Unity 编译：`MxFramework.Character.Cloth`、`MxFramework.Character.Cloth.Magica.Unity`、`MxFramework.Tests`。
- 角色资源接入后，还需在 Play Mode 验证 Running、Paused、DisabledReset、DisabledKeep 往返，以及换装/对象池重新启用。
- `wgame_palette_mage_v1` 的资源级 Magica 验收：`MxFramework.Tests.PlayMode.CharacterCloth.Magica.WGamePaletteMageMagicaClothPlayModeTests.GeneratedPaletteMage_BoneClothBuildsMovesAndCompletesStateRoundTrip`；覆盖 Resources Prefab、异步 Build、头发骨运动响应和四态往返。

## 8. Gameplay 生命周期和运行时预算（2026-10）

`CharacterClothPresentationService` 只管理显式注册的 `ICharacterClothGroup`。CharacterTest 的 `ViewBridge.ClothPresentation` 是当前会话入口；角色生成时发现并缓存角色自己的 ClothSystem，Release 时先禁用，再解除注册，Destroyed 回调清除意外销毁的注册。不会重新抢占已有 controller，也不搜索全场景或注册动作预览实例。无 Magica 的角色不注册任何组，项目层只新增 noEngine Cloth 程序集依赖。

`GameManager.SyncClothPresentationState` 在战术控制命令消费后、Update 结束、LateUpdate，以及权威故障 SuspendAuthority 后执行。暂停条件为菜单/手动暂停或无法推进战术模拟（包含权威挂起）。网络后台 tick 的原规则保持不变。布料仍由 Magica 自己的 PlayerLoop 驱动，属于非权威视觉表现，不是 FixedTickDriver 的手动模拟，也不会写入战斗碰撞权威/存档。

会话 `SetEnabled(false, keepPose)` 与 `SetPaused` 分别保留自己的请求：恢复启用时仍遵守会话暂停；取消暂停不会解除禁用。角色系统的本地 override 和 controller 的本地 requested state 不被覆盖。暂停把每个已注册 cloth 的 timeScale 置零，恢复为一；暂停期间不积累游戏侧 cloth delta 或补帧队列。后端 TeamManager 在 addTime=0 时不推进模拟时间、不保留暂停墙钟时间债务。

异步 build 成功后重新应用最新有效状态；未完成 build 的 Reset 以每个 cloth 的最后一次请求暂存，成功后消费，失败时保留到后续成功或释放。更换控制对象和销毁会清理待执行请求。角色长距离位置突变/网络 snap、AI Lab 显式恢复位置与表现 checkpoint 恢复会请求 reset。短距离或仅旋转的传送请在新姿态应用后调用 `ViewBridge.ResetClothAfterTeleport(entityId, keepPose)`，不使用每帧距离猜测替代显式通知。

预算入口不改变任何默认角色资源：

| 入口 | 实际作用域 | 支持范围 |
|---|---|---|
| `MagicaClothSimulationBudget.TryAcquire(hz, maxSteps, out lease)` / `lease.SetBudget` | **全局所有 Magica teams** | Hz 30..150；每个渲染帧最多 1..5 次模拟；只在 Manager 已启动时获取，单 owner 独占；Dispose 恢复获取前值 |
| `controller.SetCollisionQuality(colliderMode, selfMode)` | 该 controller 的全部 cloth 实例 | Collider None/Point/Edge；SelfCollision None/FullMesh；调用受支持的 SetParameterChange，不改拓扑、不触发重建；互碰 syncMode 独立且不被此 API 更改 |

全局 lease 必须由会话 owner 在退出时 Dispose，主线程调用；与其他 MagicaSettings/直接全局 setter 互斥，EveryFrame 自动刷新不能竞争写入。没有默认获取 lease，调用方必须显式选择预算。`SuspendForIndependentPreview` 暂时恢复获取前全局值，预览期间的游戏预算更改只保留请求，预览关闭恢复最新请求；Cloth Motion Preview 已接此 scope。后端频率本来就是全局，预览期间游戏 teams 也共享恢复后的频率，无法伪装成逐角色 Hz。

没有提供逐角色频率/solver iteration、运行时网格简化或重建 API。项目层的首阶段离屏 LOD 暂停见第 9 节；它不获取全局频率 lease，也不改变碰撞质量。现有原生迭代常量、[NG] Runtime 拓扑/Reduction 字段不作为可调运行时能力。

集中验证：`CharacterClothServiceTests` 的 7 个 noEngine 测试可独立运行；`NumericCharacterMagicaClothPlayModeTests` 验证 000001 的异步 build 状态、暂停模拟时间/恢复无补债、禁用保留/复位、销毁注销、原生预算步长及 collision 模式不重建。静态编译不能代替这些实际 Editor 测试，也不构成帧率收益测量。

主合验证（2026-10-01）：Unity 6000.6.3f1 的主集成编译完成且无 C# 错误，最终集中编译返回 refresh/compile/domain reload/Editor ready 全部完成、Console error 0。集中 EditMode 7/7 与实际 Magica PlayMode 5/5 通过。新增 `MainGameMenuAndAuthorityPauseReachCachedCharacterCloth` 使用正常构造的隔离 GameSlice、未启动自动 bootstrap 的 GameManager、真实 MainMenuCoordinator 与 Character View，证明菜单冻结/恢复不补债、authority 挂起、传送 Reset 和释放前停用/注销。fixture 不启用 story bootstrap、角色权威模拟、trace 文件写入或存档加载/保存；角色与 profile 均为临时实例。测试结束已恢复原 TacticalCardDemoScene，无未保存场景改动。未测量 CPU/帧率收益或服装穿模美术效果。

## 9. 显式角色组的表现 LOD 暂停

`CharacterClothPresentationService.SetLodPaused(ICharacterClothGroup group, bool paused)` 返回该组是否仍由本服务注册。它只保存该组的 LOD 暂停请求，不改本地 RequestedState；未知或已释放组返回 `false`。会话禁用优先，其次会话暂停，最后才是 LOD 暂停。三个请求分别保存，由服务单点写入最终 override，只有有效状态切换才调用组 setter。解除 LOD 暂停不会解除菜单暂停、会话禁用或本地禁用；Unregister/Destroyed/Clear 忘记该组的 LOD 请求。正常恢复不执行 Reset，已有显式传送/恢复通知仍执行原有 Reset。

CharacterTest 的 ViewBridge 持有会话内表现观察服务，只有正式角色 View 注册；独立预览不注册。布料只在所有已登记 gameplay base 相机均确认角色离屏、没有玩家/锁定/现有短时战斗反馈保护、且没有阴影姿态需求时，经过稳定离屏延迟才请求暂停。默认观察间隔 0.25 秒、离屏延迟 0.75 秒是本阶段表现策略，不改变模拟时间轴或资源参数。常规到期评估每个渲染帧共用最多 32 个名额，多个 fixed presentation/late 入口不会扩张此配额；相机版本变更、传送/根变换、重要性升档直接撤销已降低状态。按需 UI 观察发现重新可见或未知边界时也立即撤销；没有 UI 请求的局部姿态边界变化按下次到期评估恢复。

本阶段保守保护所有 SkinnedMeshRenderer（包括当前 Casting Off、以后可能恢复投影的角色），以及任意 Casting 非 Off、未知/缺失 renderer 的角色。**当前蒙皮投影角色不会仅因离屏而暂停布料**；未接入动态阴影 receiver/caster 相关性合同前，不能声称这些角色已有自动布料降档。只对可证明不需要投影姿态的其他角色开放离屏暂停。动态几何阴影的当前 pose buffer/BakeMesh 路径、动画更新、权威 pose 和命中采样保持原样。

角色根直接子级变化触发注册组件的几何失效；更深层换装、原地替换 renderer/mesh 或修改投影模式的 owner 应调用 `InvalidatePresentationLodGeometry`。失效后先保守保护，后续按需观察低频刷新组件列表；帧循环不重新发现组件。不确定边界不能授权降低。

新增工程行为测试位于 `Assets/Scripts/WGameFrameworkProject/Tests/Repository/PresentationLod/`，通过 asmref 进入现有 `MxFramework.Tests`。P1隔离阶段24项新增无引擎测试与既有7项Cloth工程测试共31/31通过，覆盖缓存、多相机、滞回、恢复、释放、状态优先级与暖路径分配；这是历史隔离证据。2026-10-05现行P1/P2已主合，[Meadow50k记录](../Tasks/Meadow50kSample.md)另载41项原生focused EditMode通过，其中包含既有观察/布料测试，不能与31项相加，也不等于Magica视觉、全部动画/VFX/阴影消费者或性能收益验收。本次文档同步未重跑这些测试，植被后续拆分也不能复用该回执宣称重新验证。
