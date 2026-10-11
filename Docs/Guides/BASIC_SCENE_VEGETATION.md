# 场景植被

> Status: Current。植被仅使用独立数据资产和既有场景渲染会话。

植被是装饰表现数据，不参与碰撞、遮挡玩法、成长或战斗命中。实例没有逐株
GameObject 或 MonoBehaviour。用户在原有场景完成视觉验收；以下工程规则不代表
新的帧时、GPU 收益或完整 Player 验收。

## 在 CharacterTestScene 从零创建

1. 在 Edit Mode 打开并保存 CharacterTestScene，进入 **MxFramework > Level Editor**，
   选择 **场景植被**。
2. 点击 **创建植被数据并绑定**。直接创建独立 CharacterTestVegetationData 资产和
   Vegetation 坐标锚点，并写入现有 GameManager 的 SceneRendering.Presentations。
   不扫描或导入场景中已有的草对象。
3. 资产默认写入
   `Assets/Config/MxFramework/Vegetation/<场景名>.asset`；重名使用唯一文件名。
   也可在 **植被数据资产** 字段选择已有资产并建立/替换当前绑定。重绑定保留
   owner ID、锚点、地面范围与启用状态，不复制或清空任何资产记录。
   选择同一资产表示共享实例和类型。
4. 在 **地面范围**指定实际地面，例如 CharacterTestScene 的 GridPlane_120m。
   地面与植被锚点必须同属当前场景且互相独立。网格地面要求启用的 MeshFilter/
   Renderer，Terrain 使用 TerrainCollider；未命中或坡度超限不会刷入，也没有空平面兜底。
5. 展开 **植被类型与几何 LOD**管理类型。默认已有基础草；可添加类型、设置名称、
   草 Prefab、LOD1/LOD2 网格、屏幕高度阈值、滞回、显示距离和固定方向阴影开关。
   类型 ID 不在 UI 中修改；已有实例引用的类型不能删除，须先擦除对应实例。
6. 选择类型，使用 **点放 / 刷放 / 擦除 / 选区**。左键操作，Shift 临时擦除，
   Esc 取消整笔；选区支持批量缩放与绕 Y 轴旋转。Undo/Redo 恢复资产实例、ID 和 seed counters。
7. 点击 Level Editor 顶部 **保存**。统一 Save 校验并保存独立数据资产、修改过的共享
   材质与场景绑定。资产会在 Project 中出现，但场景关联仍需统一保存。

已经有资产绑定的四个正式样区直接进入第 4～7 步，不再初始化。没有旧 Layout、
旧数据格式兼容、Prefab 实例导入或迁移菜单。

Project 菜单 **Assets > Create > WGame > Environment > Vegetation Data**只创建空资产，
不建立场景绑定；随后在 Level Editor 的资产字段选择它、添加类型。也可用
**定位数据资产**在 Project 中找到当前资产。

## 数据与生命周期

CharacterTestVegetationData 是 types、instances、ID/seed/stroke counters、空间块与
批处理/分类设置的唯一持久化权威。场景绑定只包含稳定 OwnerId、Data、Anchor、
SurfaceRoot 和 Enabled。修改类型或实例属于资产 Undo，地面与锚点属于场景 Undo。
资产、Prefab、Mesh、Material 使用 Unity 原生场景依赖加载；此路径不另造全局加载器
或 ResourceManager handle，显式 key-based 资源调用仍遵守原 ResourceManager 合同。

GameManager 驱动已有 MxSceneRenderingSession，创建、Tick、相机提交并释放普通
C# 植被 consumer。无玩法的独立 Meadow 样区由薄 MxSceneRenderingSceneRoot 驱动同一
会话。相机 begin/end 归 session，renderer 不自己注册全局回调。Editor preview
独立拥有可释放的派生缓存，读取相同资产，不回写记录。禁用绑定、替换数据、
移除绑定、相机删除、场景卸载与 Dispose 释放所属资源。

四个现有资产的实际记录：

| 场景 / 同名资产 | 实例数 | 类型数 |
| --- | ---: | ---: |
| MeadowWindSample | 851 | 4 |
| MeadowWindSample50k | 50000 | 4 |
| MeadowWindSample50k1 | 50000 | 4 |
| TacticalCardDemoScene | 744 | 1 |

这次架构清理保留它们的实例位置、旋转、缩放、seed/ID/counters、types、Prefab、
材质与 LOD 引用；不重新生成或清空草甸。CharacterTestScene 未被自动填充。
原生记录、保存与冷读回证据见 [植被任务回执](../Tasks/VegetationDataSession.md)。

## 几何 LOD、分类和批处理

有效的真实 LOD1/LOD2 网格启用屏幕高度选档与滞回；缺失、重复或无效的低档引用
保持原始 Prefab 几何。已有 Meadow 网格三档为 160/48/12 三角形；744/851 场景保留
实际既有引用，不能假设每个类型都已有低档。

**分类与批处理设置**可设置 ChunkSize、DrawBatchSize 和 ClassificationMode：

| 模式 | 行为 |
| --- | --- |
| Auto | 有有效低档并且设备、shader、相机支持时使用 GPU 分类，否则 CPU 路径。 |
| GpuInstances | 请求 GPU 分类；资源或设备不支持时安全回退到 CPU。 |
| CpuCoarse | CPU 格子粗裁剪与选档，边界格可能提交部分不可见实例。 |
| CpuInstanceReference | CPU 逐实例参照，与 GPU 使用相同 AABB、距离、屏幕占比判据。 |

GPU 共享常驻实例/分类 buffer，每相机独立 LOD history，每个嵌套 invocation
独立可见索引、group count 和 indirect arguments。静止相机复用分类；相机变化在 GPU
扫描 N 条记录，稳定记录不会每次提交重传。没有 HiZ 或世界流式加载。

跨格按真实 mesh/material/tier 合批，Indirect 默认每命令 8192；溢出切分，不能丢记录。
安全的 RenderMeshInstanced 回退实际容量为 min(DrawBatchSize, 256)。不同材质保持
独立；共享几何/材质也保留类型各自距离筛选。

GPU 模式可见数/各档/三角形为未知值，异步快照诊断需显式请求；预留命令槽含
instanceCount=0 的槽，不等于 Forward/DepthOnly/DepthNormals 的整帧 draw 数。
CPU 计时、GPU timestamp、buffer 字节与新收益结论需分开，不能据距离或硬件猜成本。

## 风、alpha、光照与阴影

Forward、DepthOnly、DepthNormals 共用风动与 R 通道 alpha cutout；双面法线保持一致。
Forward 走现有 WgSceneDiffuse 与 URP main-light shadow，框架高度雾生效时统一拥有雾，
否则用 Unity fog。间接实例矩阵仍为 128 字节，非均匀 owner/instance 变换与法线保持。

基础草共享材质的默认风参数：方向 (0.8, 0, 0.6)、强度 0.55、速度 0.85、
阵风 0.65、叶尖 flutter 0.3。强度为零关闭位移，速度为零冻结风 pose。
Material 是默认 Wind Source；GlobalFrame 使用既有全局风值但不改天气权威。
时间走 MxGameTime，UV.y=0 锚住根部，稳定世界 root hash 保持实例风相位。
风动边界扩张同时用于可见性和 draw bounds。

草接受环境主光阴影；材质 Environment Shadow Receive 控制接收强度。
Root Shade Strength/Height/Fade End 只控制草根 albedo tint，不给裸露地面自动增加 AO。
已有 exposed-ground density/contact AO 方案不在本次范围。

固定方向阴影从全部正式实例、原 Prefab LOD0/reference pose 独立采集；几何 LOD
不改变这份影源。改变资产实例、type cast flag、Prefab/材质/shader 等影源输入后，
需通过原方向阴影 authoring 明确重新烘焙，工具会校验 fingerprint；创建或编辑
植被不会自动 bake。风不增加动态草 caster。草 alpha cards 保持在 GI/static
occluder 几何提取之外，沿用既有显式草影语义。

## 生成样区与验证

**WGame > Environment > Create Independent Meadow Wind Sample**仅新建独立样区，
直接生成独立数据资产并绑定同一 runtime session，不重写当前场景。
**添加草花样区**向当前资产追加投影成功且不冲突的记录，不替换既有草。
这些明确的生成入口不代表自动导入旧对象。

编译、资产刷草/类型引用保护/绑定 Undo、GPU 与 CPU 参照、多相机、启停/释放、
统一 Save 与冷读回属于工程验证。实际草位置、外观、LOD 过渡、风动、alpha、GI
与阴影视觉由用户在原场景验收。没有追加未测的性能收益或 Player 构建结论。
