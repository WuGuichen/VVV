# 基地场景组装

> Status: Current

`TacticalCardDemoScene` 是正式保存的 Unity 场景。基地美术布局的权威是
`Assets/Art/WGame/Environment/Base/Prefabs/BaseFacility_Exterior.prefab`；
关卡 JSON 和场景缓存拥有出生点、静态碰撞等玩法数据。不要重新生成整张场景。

## 模型更新

1. 在 Blender `ArtSource/Environment/Base/Blender_Work/base_work.blend` 中制作，使用 **N → WGame 场景 → 联合烘焙 → 查看 → 发布 → 同步已发布源包到 Unity**。
2. 等待 Unity 资源导入完成；未自动刷新时执行 **Assets → Refresh**。发布清单导入后会自动排队更新组件，等导入和编译结束再执行；Play Mode 期间延后到编辑模式。
3. Unity 从 `Published/environment_build.json` 读取分组，校验发布文件后更新 `Generated/Materials` 与 `Generated/Modules`，同时重绑换组模型的材质。模型和贴图的稳定路径及 `.meta` 保持不变；不重建手工布局。自动更新失败时查看 Console，修复源包后可执行 **WGame → Environment → Update Base Module Prefabs** 重试。

FBX 新增子网格或材质槽后也必须完成第 2 步的模块更新。新槽不能留为 Unity 默认 `Lit`：每个槽都要绑定发布清单指定的 `EnvironmentShared_<group>` 烘焙材质。若光传输构建报告 `No explicit albedo source for Lit`，先同步生成模块的材质绑定，再执行完整光传输构建；不要给构建器添加默认 Lit 降级路径，也不要把整座基地重新生成。

共享材质使用 `WGame/EnvironmentStylizedLit` 的烘焙模式。`BaseColor` 是合成底色，
`SurfaceData` 是 `aoRimEmissionProfile16`，不是连续 roughness/metallic 图。
`Generated` 下资源由导入器维护；手工组合只编辑 `Prefabs/BaseFacility_Exterior.prefab`。
自发光按 `BaseColor × SurfaceData.B × _WgEmissionIntensity` 输出。HDR 强度来自发布清单每组可选的 `emissionIntensity`（1–64），旧清单缺省仍为 1；在 Blender 调整后重新发布、同步并更新组件，不直接维护生成材质的倍率。该倍率只增强发光遮罩区域，不改全局 Bloom 配置。
场景光传输的高级材质映射同时设置 `EmissionIntensityProperty = _WgEmissionIntensity`，将相同 HDR 强度用于周围环境受光；自动扫描缺失参数可以补齐。强度变化会使旧光传输缓存失效，需要在 Level Editor 重建。该照明仍受现有表面分区与探针分辨率限制，不额外生成 Point／Spot Light。
放置时使用 `Generated/Modules`，不要直接拖 `Published/Models` 中的原始 FBX；
原始模型没有正式材质绑定。

## 场景布局与玩法

- 基地 Prefab 以场地中心为原点，围墙内范围约 136×96 米。`Ground/OuterConcrete` 是连续铺装面，采用 2×1.95 米世界空间板缝；作业区混凝土使用相同节距与更浅、更细的缝线，让近景人物有明确的尺度参照。148×120 米铺装同时覆盖围墙外缘和南侧入口前坪。入口在南侧、机库在西侧、油站在东侧。围墙使用 Blender 发布器从 `Sec_Wall_Straight_Long` 确定性派生的 `Sec_Wall_Straight_Long_SinglePost`：移除重复端柱，把墙板延到共用柱下方，并将宽墙面下沿直接修到地面。57 段墙在 Unity 中按 8 米节距拼接；东、西、北侧高约 6.3 米，南侧降至约 2.9 米以露出入口岗亭，保留城门开口。不要为墙根添加 Cube 或在拼接处重复放端柱。
- 外围服务环线宽 8 米，内缘直线位于 `X=±41`、`Z=±31`，外缘位于 `X=±49`、`Z=±39`；圆角半径分别为 1 米和 9 米。南侧入口接入环线，不再有穿过中央战区的车道。`Layout/` 下的正式网格与材质由 Unity Editor 创建，分别保存路面、黄色边线／停车线、白色箭头／场地角标；它们不属于导入器维护的 `Generated/`。
- 西侧四个机库中心的 `X=-59`，`Z=-35/-12.5/12.5/35`，维修车辆、工具、诊断设备、管线与屋顶通风件围绕工位成组摆放；立面编号为 01–04。北端维修库与东北侧仓储棚各配置一台龙门吊。东侧中部两段加油雨棚沿南北连接，储油罐和防护栏位于雨棚东侧，两只框架油罐在后侧形成转运储备区。东北侧 `CoveredLogistics` 配置货架、叉车和载货平板拖车；`CargoStaging` 保存货架箱体、分层货堆和维修备件。货架与拖车上的货物按真实网格支撑面高度摆放。东南侧 `UtilityCompound` 由围栏包围变电设备、发电机、控制柜和检修步道。
- 北侧车辆停在环线之外的两组停车位，中央 `NorthLandmark` 的坐标碑位于 `(0, 43.5)`，高 18 米，带四块正式地坪组成的低平台。全场只放这一座碑作为唯一地标。南侧 `EntryInspection` 包含车辆待检位、人员门禁、排队护栏和人行标线。南墙 `south_wall_10` 缩短，为人员门禁留出独立开口。大型灯杆均位于作业区或边缘，不占用环线。
- 中央场地保持无实体障碍，使用低对比度混凝土作业区、平面编号、双折线标记和浅色轮胎痕迹增加层次。低路缘位于 80×60 米场地之外，不伸入战区。当前发布包为 85 件制作模型和 1 件派生墙段，共 86 类正式组件；布局使用其中 80 类。坐标碑只使用已确认的底色，不包含已取消的细节试样。`Layout/` 同时保存地面、立面标记及外围地表网格，均为静态表现资产。
- 外围过渡地表和岩石属于场景的 `DistantMountains_Ring/PerimeterRockCuts`，复用已导入的 `Mountain_14/15` 顶点色模型。近景岩石使用独立的 `PerimeterRock` 材质变体，地表向远处降低以衔接原远山；不修改原远山材质或网格。该组保持在基地静态光传输根节点之外，且没有玩法碰撞体。南侧进场道路延伸到 `Z=-91`，与外围地表衔接。
- 服务车道、地面标线及靠边停放的车辆和设备用于分区与丰富画面，只是表现资产。它们目前位于可部署范围外，或仅覆盖地面，不增加玩法阻挡。
- 中央可部署区域为 `X=-39…39`、`Z=-29…29` 米；边缘留 1 米角色余量。场景中不可见的正式 Combat 地面尺寸为 80×60 米，四个边界阻挡通过关卡文档登记。
- 场景美术 Prefab 是顶层实例，不逐个进入关卡编辑器。出生点、地面与边界等玩法对象使用 **MxFramework → Level Editor** 校验和保存；`TacticalCardDemoScene.unity` 与 `tactical_card_demo.level.json` 的缓存 hash 需一致。
- 整备和战斗镜头均为透视。整备镜头按基地范围计算总览距离，支持鼠标滚轮和有边界的平移。建筑内部、车辆驾驶和设施交互不在当前切片。
- 当前场景使用独立的 `BaseIllustrativeLook` 与暖色侧光，具体配置见下节。建筑、车辆和设施继续使用已发布的组件材质，未完成的模型上色仍由 Blender 制作流程维护。移动静态设施或修改其反射材质后，必须重新构建完整光传输；仅改太阳或 Look 不需要重新构建。

中央双 V 单独保存为 `Layout/Court_CenterInsignia.asset` 与同名材质，位于基地 Prefab 的 `Ground/Court_CenterInsignia`，尺寸为 3.6×2.7 米、中心为 `(0, 0.012, 0.2)`。它使用低对比度砖红色，与建筑立面的红色编号分开维护，不改变地面碰撞或可部署范围。

## 插画式微缩景观渲染

风格通过现有漫反射、场景光传输与 URP 调色组合实现：暖色受光面、蓝灰暗部、可读的棚内设备和低对比度远山。当前模型轮廓与烘焙贴图仍是细节上限，概念图中的倒角、云层与额外细节不会由调色生成。

| 配置 | 所在资源与职责 |
| --- | --- |
| 场景灯光 | `Config/MxFramework/Rendering/SceneLighting/TacticalCardDemoScene/BaseIllustrativeLook.asset`；直接光 2.15、间接光 1.45、明暗分段 0.18、微遮蔽 0.6；天空 `(0.74,0.76,0.80)`、地平线 `(0.67,0.68,0.70)`、地面反射 `(0.61,0.56,0.48)`，颜色均按 sRGB 制作 |
| 材质受光 | 同一 Look 的 `SpecularStrength=2`、`LightRimStrength=0.65`；按发布 Surface Profile 计算局部柔和高光与朝向主光的掠射亮边，其它 Look 默认均为 0 |
| 太阳 | 场景 `Directional Light`；旋转 `(32,145,0)`、强度 1.8、暖色 `(1,0.88,0.67)` 与软阴影；用斜向受光面和阴影呈现光的方向 |
| 调色 | `Config/MxFramework/Rendering/Profiles/BaseIllustrativeGrading.asset`；场景 `Base Illustration Grading` 的全局 Volume 引用，优先级 10，权重 1 |
| 天空 | `Art/WGame/Environment/Base/Layout/BaseIllustrativeSky.mat`；标准 Procedural Skybox，High Quality Sun Disk，曝光 1、大气厚度 0.95 |
| 太阳光晕 | `Config/MxFramework/Rendering/Profiles/BaseSunHalo.asset`；主光上的标准 `LensFlareComponentSRP` 引用，只有一个程序化渐变圆形元素，缩放 10、强度 3，启用深度遮挡与 16 次遮挡采样；离开画面边缘后加快衰减，避免全景蒙白 |
| 远山 | `Art/WGame/Environment/Base/Layout/BaseIllustrativeMountains.mat`；只替换本场景 25 个远山 Renderer 的材质引用，保持原顶点色含义与共享原材质 |

以上资源路径均相对于 `Assets/`。Main Camera 开启 Post Processing，Volume 位于其默认 Layer Mask 内。调色 Profile 包含 Neutral 色调映射、曝光/对比度、冷暖分离与选择性高亮压暗；这些参数合入 URP 的调色 LUT，不为每个参数增加独立全屏效果。曝光为 0.1 EV、对比度 16、饱和度 5。`ShadowsMidtonesHighlights` 只覆盖 highlights `(1,1,1,-0.25)`、highlightsStart `0.3`、highlightsEnd `0.75`：逐渐压低高亮部分，暗部和中间调的色轮保持默认，不以降低全局曝光来处理大面积亮面刺眼。移除全局 LiftGammaGain：先前的 Gamma 0.24 与较强间接光叠加，会把深灰道路和设备一起抬亮。当前以全景的深灰道路、暖色地坪和建筑暗部为基准，并用同一设置检查近景，不根据镜头距离自动增减曝光。

同一 Profile 的 HDR Bloom 使用现有 URP Dual 滤波、Quarter 降采样（宽高各为原来的 1/4，起始像素量为 1/16）、最多 2 层、阈值 1.5、强度 2、扩散 0.65、Clamp 12，关闭高质量预滤波。小灯头在原来的 5 层扩散下光晕过于分散；收紧层数后，Emission 8 在同机位实景中呈现局部红橙光晕，提高阈值则减少道路标线和普通高光的泛光。该调整不改变发光制作数据，不要求重新烘焙模型或光传输。单元素太阳光晕负责朝向太阳时画面边缘的暖辉，并随屏幕位置衰减。后处理有必要的逐帧 GPU 开销，不能把世界光照缓存不更新解读为后处理免费。未另加屏幕空间光晕链、SSAO、景深、运动模糊或额外抗锯齿链，也没有逐帧修改材质的自定义脚本。共享 URP Asset、Renderer Feature、基地组件材质、几何布局和光传输源数据保持原有权威。

近岩 `PerimeterRock` 不在基地光传输根下，使用自身的顶点色漫反射；环境强度 0.9、轮廓提亮 0.025，保持岩石暗面可读，避免通过全局提亮去补偿它。其模式继续为 AlbedoAO，不改变顶点色内容。

`EnvironmentStylizedLit` 的烘焙分支从已有 `SurfaceData.a` 解码 16 类表面，复用 Catalog 的 specular strength、specular power 和 metallic；高光指数限制在 8～32，避免细碎、刺眼的尖锐反光。粗糙表面的响应较弱，漆面和金属更明显。掠射亮边由观察角度与主光朝向共同决定，制作端的 `SurfaceData.g` 可以增强它；镜面与亮边都乘已有太阳阴影、距离衰减、法线受光和微遮蔽，不能穿过棚顶在背光面自发光。发光仍由 `SurfaceData.b` 单独控制。

这条响应只增加局部受光部分，不改变底色、原漫反射、曝光或光传输数据。未烘焙的连续地坪、服务道路等不进入高光分支，因此不通过整体提亮地面来增加光感。零强度或没有绑定场景光照的相机保留原表现；新增参数在原有相机 resolve 中发布，修改它们不重算世界辐射或太阳可见性。渲染复用已有法线、主光、阴影结果和同一次 SurfaceData 采样，不增加贴图、材质实例、着色器变体或渲染通道。真实硬直角的轮廓仍由模型决定，这不是屏幕描边或虚拟倒角。

`MxSceneLightingProvider` 继续拥有光照缓存：太阳方向改变才更新太阳遮挡；太阳颜色/强度或天空、地平线、地面反射色改变才重新传播光照。只移动镜头、调整 Direct/Indirect/Band 等最终响应参数或调色不重建世界光照；每个相机仍执行必要的屏幕空间重建。原 `BaseServiceDaylightLook` 保留用于对照。要做视觉对照，应同时比较太阳、Look 和调色，不能只切换 Look 后认为恢复了完整旧效果。

### 基础照明与光晕分别校准

2026-09-28 按用户选定的“光照层次与材质反光”方向校准：当前太阳 1.8、DirectStrength 2.15、IndirectStrength 1.45，曝光仍为 0.1 EV。暖直射光与中性偏冷环境光分开控制，降低暖色环境填充对阴影层次的覆盖；局部高光沿用既有烘焙表面响应，连续地坪与服务道路保持哑光。`ShadowTint=(0.96,0.98,1)`，不以全屏染蓝来表现背光。

本次通过 Unity Editor 保存场景太阳与本场景独有的 Look，未新增渲染通道、贴图、材质实例或逐帧逻辑。主相机全景与机库近景均完成实际截图检查，Console error 为 0；连续定尺寸渲染中世界辐射、太阳可见性更新计数和持久缓存大小保持稳定。这是配置与视觉验证，不是 GPU 耗时基准。近景 PNG 使用 sRGB 渲染目标，避免把线性 HDR 读回当作显示颜色。未改场景布局、表面底色、磨损纹理和尘埃开关。

目标是看得见光的方向和局部光晕，同时大面积亮面不刺眼。太阳强度不是颜色存储上限；不要仅因强度大于 1 就降低它，也不要把最终光照硬截断到 0～1。HDR 中的少量高亮应留给浮点颜色缓冲、色调映射与 Bloom 正常处理。数值合法不能代替对画面舒适度的检查。

调整顺序：

1. 暂时关闭 Bloom 与太阳光晕，先在全景和近景中检查道路、地坪、建筑暗部和角色的基础明暗。确实需要改变照明时再调整太阳与 Look，并复查两个机位。
2. 基础照明确认后固定太阳、Look 与曝光。光晕不足时调整已有 Bloom 的阈值、强度和扩散，不用提高整个场景的太阳强度来补。
3. 朝向太阳的大范围暖辉由 `BaseSunHalo` 独立调整，保持遮挡和离屏衰减；不要通过全局 Gamma、Lift 或曝光去模拟这层光辉。灯具自身的亮度由正式材质的发光区域负责。
4. 修改后比较同机位光晕开关的画面，确认暗部与普通地坪没有一起泛白；必要时读取色调映射前的浮点颜色，而不是从 PNG 反推 HDR 数值。

2026-09-27 **上一版太阳 4、柔光修正前**的诊断采样使用 640×360、`R16G16B16A16_SFloat`，关闭相机后处理后读取颜色；包含当时的场景光照、天空与共享雾。按 **RGB 任一通道大于 1** 统计，全景约 0.633%，近景约 0.134%；这不是“过曝面积”的定义，也不是当前参数或所有机位的保证。全景中央地坪样区没有超过 1，RGB 中位数约为 `(0.682, 0.556, 0.465)`。两个机位均未发现 NaN/Infinity。恢复当时的调色后，同机位 Bloom 开关对比的全景平均线性亮度变化约为 +0.030%；太阳光晕在该全景机位没有额外贡献。Bloom 带软阈值过渡，不能把硬阈值以上像素比例当作完整光晕覆盖面积。

这次测量排除了“大片 HDR 溢出”的猜测；随后根据大面积亮面刺眼的视觉反馈，降低主光、调整照射方向并选择性压暗高亮，同时保留曝光和局部柔光。诊断读取是一次性离线检查，不加入运行时逐帧回读或额外渲染通道。

## 检查

运行时的网格、部署边界、角色标记和目标圈由 `TacticalPreparationGroundView` 创建，使用 `WorldMarkerUnlit` 的网格覆盖模式，保留原有几何轮廓和不受太阳影响的提示色。它们不属于基地美术 Prefab。项目自有粒子与线条使用 `MagicVfxUnlit` 的贴图/顶点颜色模式；该模式保留原贴图、HDR 颜色和生命周期透明度，不额外生成菱形、噪声或脉冲。运行时及 `Editor/CharacterTestVfx` 的生成器共用 `CharacterTestUnlitMaterials`，修改临时实例不能替代修改生成入口。

战术辅助网格仍为 8 米节距，线宽为 0.012 米、透明度约 18%；中央辅助线透明度约 24%。这些细线使用同一专用 Shader 的常规透明混合，不写入深度；不新增贴图、全屏效果或逐帧材质更新。角色圈、阵营边线及部署边界继续保持不透明。美术板缝、辅助网格和实际部署规则互相独立，不能通过缩放角色或整个战区来修正地面比例。

2026-09-28 已原位迁移 `Assets/VFX/MxFramework/Samples` 与 `Assets/VFX/MxFramework/CharacterTest` 下 49 个 URP 粒子材质和 3 个 Sprite 线条材质，保持资源 GUID 和 Prefab 引用。两个原有程序化 MagicVfx 材质保持原模式。`GraphicsSettings` 显式保留 WorldMarkerUnlit/MagicVfxUnlit，防止只有运行时 `Shader.Find` 的模式在打包时丢失。第三方 Shader Graph/VFX Graph 源素材没有批量换 Shader。

打开场景确认基地 Prefab 仍有连接，地坪没有明显断缝，且没有丢失材质或脚本。
进入 Play Mode，在战区内部放置两队角色并开始战斗；越过边界的放置必须被拒绝。
从关卡编辑器保存后重开场景，再检查静态碰撞、出生点与角色缓存。
