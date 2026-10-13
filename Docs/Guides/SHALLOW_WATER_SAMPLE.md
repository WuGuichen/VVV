# 当前场景浅水样区

> Status: Current
> 状态：低/中/高档 SSPR 与独立交互涟漪已接入，镜像反射单独保留；Player 帧耗时待实测 | 2026-10-12
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Rendering/CharacterTestShallowWater*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevPreviewOverlay.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestShallowWaterAuthoring.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestShallowWaterPreview.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestSceneRenderingLevelEditorModule*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameManager.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameManager.ShallowWater.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Composition/GameManager.PresentationLod.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCharacterRuntimeModule.RenderingMotion.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestPhysicalAudioSystem.cs`
> Code-Doc: `Assets/Art/WGame/Shaders/Environment/ShallowWater.shader`
> Code-Doc: `Assets/Art/WGame/Shaders/Environment/ShallowWaterSSPR.compute`
> Code-Doc: `Assets/Art/WGame/Shaders/Environment/ShallowWaterRipples.compute`
> Code-Doc: `Assets/Config/MxFramework/Rendering/ShallowWater/*.asset`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Rendering/ShallowWaterTests.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxSceneRenderingAuxiliaryCamera.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxSceneLightingProvider.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxDirectionalShadowProvider.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxRenderingPipelineFeature.cs`

## 性能档与有界涟漪（2026-10-11）

现行入口为 **Mx 关卡编辑器 → 场景渲染 → 浅水 → 性能档位**。
这是关卡制作入口；玩家入口为 **游戏内设置 → 画面 → 水面反射 / 交互涟漪**，位于
语言行下方。两项偏好独立应用/保存到个人 settings profile，通过独立运行时副本
覆盖场景预算；场景默认还原关卡选择，退出不改参数资产。字段、草稿和保存
语义见 [Settings 接口](../Interfaces/Settings.md#游戏内水面质量2026-10-11)。
`CharacterTestShallowWaterSettings.QualityTier` 提供以下预算；切换预设只改变
派生预算，不覆盖原有自定义参数、水色、波形和场景开关。旧资产缺少字段时
以 `Custom=0` 保留旧参数；新建浅水通过 authoring 默认选择 Performance。
本次通过 Unity 保存的 CharacterTestScene、CharacterTestRuntimeAiLabScene、
TacticalCardDemoScene 三份正式参数均选择 Performance，原自定义数值保留。
TacticalCardDemoScene 同时由 Unity 补写此前缺省的水花字段默认值。三份参数
均通过 Unity 绑定正式 ShallowWaterSSPR.compute，原水花引用未变。

| 反射档位 | 实时倒影 | 折射 / 水面颜色复制 |
| --- | --- | --- |
| Performance 性能优先 | SSPR：1/4 宽高、最长边 256、3×3 深度加权柔和重建，逐绘制帧计算 | 无，预乘透明混合 |
| Balanced 中档 | SSPR：1/2 宽高、最长边 512、5×5 深度加权重建、最远 min(40, authored distance) 米 | 无 |
| SsprHigh 高档 | SSPR：3/4 宽高、最长边 1024、5×5 深度加权重建，沿用 authored distance | 无 |
| Quality 镜像反射 | 1/2 宽高、最长边 1024、逐帧；沿用 authored distance 和源相机阴影开关 | 开启，折射强度为 0 时也跳过复制 |
| Custom 自定义 | 镜像相机 / SSPR / 仅环境反光；SSPR 最大边硬限 1024；刷新率与阴影/层过滤仅用于镜像相机 | 独立开关 |

环境 SH 是环境色近似，不包含建筑、角色或天空贴图的精确镜像，作为 SSPR
缺失像素的回退。SSPR 复用本接收相机的不透明颜色和深度，不重新渲染场景；
不会包含原颜色/深度中缺失的屏幕外物体、遮挡背面、通常的透明物体。
ReflectionLayers 只过滤镜像相机，SSPR 沿用源相机可见内容。原配置的
ReflectionEnabled 为总反光开关，关闭后 SSPR 与环境近似也停用。
Scene 预览使用同一算法预算，各相机在自己的 RenderGraph 内生成临时纹理；
三个 SSPR 档均不保留镜像相机 RT，也不启动该相机；均按接收相机绘制帧计算。

自定义镜像相机的运行时刷新率从实际完成的 capture 计时。跳帧时纹理和 capture VP 成对保留；
首次/失效 RT、档位/反射预算变化、镜头移动超过 2 米、旋转超过 12 度、
镜头投影变化或时间倒退立即刷新，故切镜帧可突破稳定镜头的频率上限。
Edit 预览继续采用既有按需刷新，不能用它验证 Play 的限频调度。
反射强度为 0、视锥外或水面下方停止倒影；水面 pass 自身也按接收相机
视锥及水面朝向剔除，避免离屏仍复制全屏颜色。SSPR 模式要求 URP opaque
texture（当前项目原本已启用）及深度；复用同图资源，不新增水面专用全屏复制。
不能因此宣称整条管线没有颜色复制成本。仅环境反光模式只要求深度。

### SSPR 实现与边界

SSPR 在原 SurfacePass 内记录 Clear / Project / Resolve 三个 compute pass，
没有新增 RendererFeature。输入采样网格与输出使用相同有界分辨率，避免
只缩小输出、仍扫描 4K 全屏。Project 从深度重建世界位置，过滤水面下方与
天空，将位置关于水面镜像并投影回当前相机。使用 R32_UInt 原子最小值选取
最近倒影深度。输入像素数不超过 262144 时保留 14 位量化深度＋18 位源序号；
更大网格使用 12 位深度＋20 位源序号，支持最大 1024×1024，避免源索引溢出。
同深度确定性选取源序号，最高深度桶保留，故全 1 恒为空值。深度量化步长
分别约 far/16382、far/4094；65 m 范围下高分辨率路径约 1.59 cm，不能把
更高空间分辨率当作更高深度排序精度，也不承诺极近共面候选的精确排序。

重建只处理浅水范围内的接收区域。所有 SSPR 使用 `ResolveSoft`：低档读取
原投影表的 3×3 邻域，中/高档读取 5×5；以中心有效像素或最近有效邻居的倒影深度为基准，
拒绝深度差达到 max(0.15 m, 基准深度×4%) 的候选；其余候选按空间 tent
权重与深度接近程度合成。缺失样本降低覆盖率，已拒绝的其它深度层不参与
归一化，避免前后景串色及不必要的暗边。有效区域也进行轻微颜色过滤，
取舍为更柔和的轮廓和较低的高频清晰度，不能恢复 256 网格遗漏的细物体。
低/中/高的颜色柔化混合权重分别为 1 / .55 / .3，覆盖率抗锯齿始终完整保留；
有效中心像素在中/高档保留更多颜色细节，空洞仍使用加权结果。Custom 采用
.55 颜色混合与其补洞半径，半径 0 时不填缺失中心，仍过滤有效像素。
原 `Resolve` 仅保留为原始重建回归参考，不再由生产 SSPR pass 选择。
不迭代扩散、不存历史帧。源屏幕边界与扰动后屏幕边界淡出；
输出 HDR 颜色按置信度预乘，双线性过滤时不会把空洞黑色混入边缘。
无法补足的像素回到环境 SH。水面涟漪仍通过采样偏移
扰动倒影；这不是逐波纹几何法线的精确光线追踪。

每相机两张 transient 纹理为 4 B/px 的投影表和 8 B/px 的反射颜色，均由
RenderGraph 管理。16:9、最长边 256/512/1024 时合计约 0.422/1.688/6.75 MiB，未含
管线自身的颜色/深度资源及分配对齐。没有 compute、缺少绑定或必要 UAV
格式、任一生产内核在当前图形后端不受支持、立体相机/非 2D 目标、缺少 opaque
color 时不启动 SSPR，显示诊断并回退环境。内核名称存在不等于可执行，初始化
必须同时检查 Clear / Project / ResolveSoft 的 `ComputeShader.IsSupported`。
当前纹理原子最小值路径在 Unity 6000.6.3f1 / Apple M4 Metal 上的 Project 内核
不受支持，因此该后端回退环境反光；实时 SSPR 的 Metal/Vulkan/XR 验收仍未完成。
相机模式和 SSPR 互斥。

### 低档 SSPR 柔和重建验证（2026-10-11）

以下是先行低档阶段的历史证据；现行中/高档统一重建以本节前述实现为准。

该改动仅替换 Performance 的第三阶段 kernel，仍是 Clear/Project/Resolve 三个
pass、两张有界纹理，最长边仍为 256。没有增加全分辨率水面采样、时域历史
或独立模糊纹理。代价是低分辨率有效接收像素最多 9 次源颜色采样，原路径
最多 1 次；这不是免费抗锯齿，也不保证消除运动时的屏幕空间缺失/闪烁。

编译 job `a5ca27aadd7d4494a317065e22fea6a5` 通过 refresh、compile、domain
reload、Editor ready、Console errors=0。随后清理 HLSL 循环变量/整数除法
warning，强制导入后 compute messages 为空。ShallowWaterTests 原生 Runner
`03a1f446c44c42c68a233c8f6e382673` 为 22/22；最终 shader 的新旧 resolve GPU
定向复验 job `504ca93d8347410a8ef252183fa279e9` 为 2/2。覆盖高频棋盘颜色
平滑、前后深度层不串色、边界/空洞覆盖率 0.75/0.25、预乘颜色及下一帧归零。

1920×1080 CharacterTestScene 同一临时视角前后渲染均为 SSPR 256×144、
镜像相机渲染=0，场景与参数恢复 clean；已查看 before.png / after.png，墙体
和障碍倒影边缘更柔和。控制比较临时关闭背景坡度、倒影扰动与交互涟漪，
其它独立微尘仍可能变化，不作整帧逐像素差值验收。

RTX 3060 Ti 上以 256×144、半幅有效接收区测试 128 次 resolve 批次，三轮
均摊同步提交加回读等待时间：原 resolve 0.00825/0.00872/0.00780 ms，
柔和 resolve 0.01078/0.01929/0.01055 ms。它只说明该合成负载下的额外工作
量级，不是独立 GPU 时间或 Player 帧耗时承诺，且不含完整投影管线成本。
证据保存于本地 `Temp/SsprAA/{before,after,cost}.json` 及对应 PNG。

### 中/高档 SSPR 统一重建（2026-10-11）

三个预设现均使用深度加权颜色重建及覆盖率平滑，中/高档为有界 5×5 支持。
柔化权重只调节有效中心的颜色细节，不削弱覆盖率平滑；空洞继续使用完整
加权结果。没有增加计算阶段或纹理种类，高档提高输入/输出像素预算。
高档像素数达到中档的 4 倍、低档的 16 倍；每像素最多 25 次颜色采样，
不能把之前 256 的计时直接外推到 1024。

编译 job `d5a6368753c64523a985ba84bb68273d` 确认完整 refresh/compile/domain
reload/ready、Console errors=0，compute messages 为空。原生 Runner
`5534021a17e84a8796ad63c77247f24b` 为 65/65，覆盖两种位宽的清理/投影、
1024×1024 GPU 源像素索引超过 262143 后仍正确取色、3×3/5×5 的细节与
覆盖率、旧镜像偏好 4 保留及新增高档偏好 5 的 JSON roundtrip。

1920×1080 同一临时相机原生渲染确认：低/中/高 SSPR 分别 256×144、
512×288、1024×576，每档 SSPR=1/镜像相机=0；镜像选项为 SSPR=0/相机=1。
已查看中/高截图，恢复后 Scene/Settings dirty=false。截图与结构化回执在
`Temp/SsprTiers/`。尚无 Player GPU 计时、移动设备或跨后端验收。

实际 Play 设置页通过现有按钮循环验证：场景默认 → 低（SSPR）→ 中（SSPR）→
高（SSPR）→ 镜像反射；高档文字完整可见，交互涟漪仍独立保持低档。
`Temp/SsprTiers/player-settings-high.png` 为界面回执。取消后水面偏好恢复原有
Medium，未写入新偏好；退出 Play 后原场景 clean、Console errors=0。

### 独立交互涟漪与局部纹理场

`RippleQuality` 与反射 `QualityTier` 无关，默认 Low。所有启用档位共用
`MaximumRipples` 安全预算（默认/最大 32），不再按反射低/中/高限制为 8/16/32。
玩家的“场景默认”保留该 authoring 档位；关闭只停用交互波包，背景水纹和
独立水花粒子保持原有开关。新 compute 引用由 Unity 保存至三份正式参数资产。

| 涟漪档位 | 纹理 | 局部覆盖边长 | 基础刷新率 | 每视图颜色纹理 |
| --- | --- | --- | --- | --- |
| Low | 256×256 | 24 m | 30 Hz | 0.5 MiB |
| Medium | 512×512 | 32 m | 30 Hz | 2 MiB |
| High | 1024×1024 | 48 m | 60 Hz | 8 MiB |
| Custom | 128–1024，2 的幂 | 8–64 m | 15–60 Hz | 随分辨率 |

波包来自真实接触/移动/落地。同类事件在 80 ms、18 cm 内合并，保留较强
振幅与最初相位，避免密集重复接触叠亮。溢出优先淘汰移动尾波，其次普通
接触，最后落地；同优先级淘汰最旧。无空槽时低优先级不挤掉高优先级。
这只是本地表现，不改变 gameplay、物理接触、回放或网络事件。

CPU 预计算半径、衰减、波峰强度，为当前世界坐标局部区域构建 16×16
保守环带掩码；GPU 在有界纹理上计算解析波形，输出 RG 坡度和 B 波峰。
主水面 shader 只采样该纹理，不再逐像素循环波包。环带外先以距离平方拒绝，
4 个 Gaussian 宽度的尾部截断与原方案相同；计算结果存为 RGBA16F 后有
半精度量化、双线性滤波及范围边界淡出，不宣称最终像素完全相同。

区域跟随当前视图中心射线与水平水面的交点，并限制在水面矩形内；中心
按纹理格点对齐。焦点移动不超过覆盖边长的 1/8 时保留旧世界坐标区域，
避免纹理随着相机小幅运动滑动。切镜/重定位、预算变化、新有效事件、时钟
回退会立即重建，所以事件密集时可高于基础刷新率。暂停时间不前进时复用
缓存；没有波包、关闭交互涟漪、水面离屏、Scene 视图关闭或 session 释放时
释放对应目标。每次构建写满目标，包括空格，避免过期波纹残留。
只存在远处、尚不能传播到当前区域的波包时，也在分配纹理前跳过；允许之后
传播入界的环带再次激活该视图。

Game 和各可见 Scene 视图各自拥有 RT 与上传快照，不交叉覆盖排队的图数据。
这意味着打开多个 Scene 窗口会增加预算；上述 MiB 是单视图、未计分配对齐。
没有 compute/UAV 能力或缺少计算资源时报告诊断、停止交互涟漪纹理，背景
水纹继续显示。本轮未实现二维高度场传播、岸边反弹或真实流体模拟。

局部纹理场验证（2026-10-11）：

- 编译 job `6b3a7279c7cf40aab9564d0848685f87` 确认完整 refresh/compile/domain
  reload/ready 证据，Console errors=0。Ripple compute messages 为空。
- EditMode job `da7d91b368fa4309a51fe6531cc08679`：63/63、0 skipped。包括
  独立偏好复制/JSON 往返/运行时副本、合并与优先级溢出、传播入界、暂停/回退
  调度、原生 GPU 64×64 全像素对照原解析坡度与波峰（half 容差 .001），
  过期后的全图归零，以及正式发布设置面板的控件类型/binder/命令回归。
- 原生 RenderGraph 合成波包检查中，Low/Medium/High 实际产生 256/512/1024
  纹理，坡度数据非零、各首次更新一次；同一时钟重复绘制和 0.1 m 相机移动
  更新数为 0，10 m 切镜更新一次。关闭/清空波包后 RT 为 null。Game 相机及
  参数引用在 finally 恢复，scene/settings dirty=false。记录在
  `Temp/RippleField/capture.json`；field-data.png 是纹理通道诊断图，不是水面颜色图。
- RTX 3060 Ti、1920×1080、32 个固定合成波包进行了三轮同步 RenderRequest
  加 GPU readback 等待的对比。解析参考中位数为 5.68/7.00/7.17 ms，低档场
  为 5.82/7.13/7.48 ms，中档场为 5.71/5.93/7.62 ms。为避免把静止缓存误算
  成加速，场路径强制每次重建。计时包含 Editor 提交、场景渲染和回读阻塞，
  有明显轮间漂移；**没有证明稳定加速**，不是独立 GPU 或 Player 帧耗时。
  原解析 shader 来自本轮改动前快照，临时 shader/material 全部释放；原始数据
  在 `Temp/RippleField/comparison.json`。目前可确认的是工作量/刷新/内存预算
  与视觉反馈分离，不能据此宣称 FPS 提升。

本轮尚未覆盖 Player build、移动设备、XR、长时间动态角色场景及高档显存
压力；多 Scene 窗口预算应单独计入。正式性能收益仍需 Player GPU profiling。

SSPR 原生验证见本节末；以下是接入 SSPR **之前**的性能档/涟漪阶段证据，
其中 Performance/Balanced 算法及纹理数不能作为当前配置说明：

- `validate_unity_scripts` job `3b889e10c4514a7082e05b8a7c812684` 确认
  refresh completed、compilation started/finished、domain reload、Editor ready、Console errors=0。
- 原生 EditMode Runner job `a354504ae2a540f2a9b57ac669b7986a`：ShallowWaterTests
  11/11，通过档位往返、刷新节奏、切镜/时钟恢复、缩容保新、旋转环带保守覆盖、
  bit31、过期清空和稀疏筛选等断言。
- CharacterTestScene 临时检查视角、1280×720 RenderRequest 实际渲染：Performance
  surface=1/copy=0/reflection=0/owned RT=0；Balanced surface=1/copy=0，倒影
  320×180/0.879 MiB；Quality surface=1/copy=1，倒影 640×360/3.516 MiB。
  禁用水面和转向水面视锥外均为 surface=0/copy=0；离屏反射 RT=0。
  `SurfaceColorCopies` 是本 session 的 Game/Scene 合计实际执行计数，不是全管线复制总数。
- 使用临时合成波包验证 Quality→Balanced→Performance 后活动数量 32→16→8；
  原波包对象、相机、档位与开关在 finally 恢复，scene/settings dirty=false。
  已查看各档场景截图；合成波包不证明真实脚步接触链已重新验收。
- 独立 32 波包、72×50 m 均匀散布诊断：256 格候选对从朴素 8192 降为 200
  （97.56%）；4096 次变化时钟的 CPU 构建合计 117.65 ms，平均 28.72 µs，
  当前线程 managed allocation=0 B。这是 CPU 微基准和均匀网格工作量，
  不是 GPU 加速比例，也不代表密集重叠波包或真实画面覆盖率。
- 当前水面 shader 支持且无编译 error；共享方向阴影 include 有两条
  potentially-uninitialized warning，本轮未修改该模块。原生 Console 最后读取 errors=0。
  Code standards 两项 report-only 提示命中原有空 Execute 与项目水面 Tick 时间读取，
  本轮未新增同类路径。

该阶段证据在本地 `Temp/WaterPerformance/` 下的 capture.json、ripple-capture.json、
ripple-work.json 与各档 PNG。未进入 Play，未测 Player GPU/整帧耗时、15 Hz
实际相机循环或移动镜头缓存视觉连续性；Scene 多窗口与其它后端沿用历史边界。
下方历史计时不代表这些档位或优化后的耗时，实际 FPS 收益仍需代表性 Player 测量。

### SSPR 原生验证（2026-10-11）

- 最后源码验证 job `d1fee12793c4453f9813ad6edc5729ad` 确认 refresh completed、
  compilation started/finished、domain reload observed、Editor ready、Console errors=0。
  新 compute shader 的 GetComputeShaderMessages 返回空数组。
- EditMode Runner job `a586f34e41b6493eae443be39c1c716e`：14/14 passed、0 skipped。
  除原涟漪与镜像相机回归外，验证有界输入/输出、投影 key 最近深度与稳定 tie、
  空值保留；GPU fixture 在原生 compute 上对已知 8×8 深度/颜色执行三阶段，
  精确核对镜像源像素及反射颜色，然后把下一帧深度清空，确认 key 和 alpha
  不残留上一帧内容。
- CharacterTestScene 的临时 1280×720 检查视角：Performance 为 256×144，
  Balanced 为 512×288；两档均执行一次 SSPR、镜像相机渲染=0、水面专用
  color copy=0、镜像 RT=0。Quality 为 SSPR=0、镜像相机=1、copy=1。
  截图已查看，可见墙体/障碍倒影；低档边缘更粗糙，遮挡背面不保证完整。
- 缺失 compute 时 SSPR=0、surface=1，报告环境回退；关闭总反光时
  SSPR=0、surface=1；水面离屏时 SSPR=0、surface=0，均无镜像 RT。
  临时相机/设置在 finally 恢复，Scene 与 Settings 均未遗留 dirty 状态。
- 最后原生 Console errors=0，10 条 warning 均指向未改动的 Editor API
  弃用/程序集枚举位置；没有 SSPR warning。定向 Code standards 仍仅有
  上述两项既有 report-only 提示。

本地证据：`Temp/WaterSSPR/capture.json`、`fallback.json`、Performance.png、
Balanced.png、Quality.png。未跑 Player 构建/GPU 计时，没有 FPS 或毫秒收益
结论；未重验 Play 动态角色、Scene 多窗口、Metal/Vulkan/XR。SSPR 的屏幕
缺失信息和低分辨率细物体遗漏由环境回退处理，不能等同完整镜像相机。

## Metal 共享渲染初始化（2026-10-11）

`CharacterTestShallowWaterSession.PrepareView()` 读取 `Feature.Pipeline` 时会初始化
共享渲染 Feature。方向阴影备用资源现使用经过 `Render`／`LoadStore` 支持检查的
`R32_UInt RenderTexture`，避免 `Texture2D` 的 `Sample` 格式检查在 Metal 上使
浅水预览初始化报错。相机先绑定备用资源，正式阴影 Provider 再覆盖真实 atlas。
此修正没有改变浅水参数、反射、涟漪或场景资产；Metal 启动与编译证据及未覆盖
范围见 [方向阴影工程说明](DIRECTIONAL_TRIANGLE_SHADOWS.md#disabled-camera-integer-texture-binding-2026-10-11)，
不能把共享初始化检查当作浅水视觉或交互验收。

## Mx 关卡编辑器中的浅水编辑（2026-10-10）

现行编辑入口为 **Mx 关卡编辑器 → 场景渲染 → 浅水**，与间接光、雾和微尘
使用同一模块及保存事务。GameManager 序列化 `CharacterTestShallowWaterConfiguration`，
持有水平水面位置、朝向、三个开关和参数资产引用；材质、倒影预算、范围及涟漪
参数在面板内直接编辑。共享参数资产纳入 coordinator 的文件快照，GameManager
纳入 Scene 对象快照；统一保存后才能把 Scene 与参数作为同一提交交付。
此环境配置保存在 Scene，沿用场景渲染权威，不新建关卡玩法 JSON 模块。

GameManager 创建、Tick 和释放普通 C# `CharacterTestShallowWaterSession`，
Editor 更新只同步预览，不启动 gameplay。新关卡没有水时显示“添加浅水”；旧
场景显示“迁入现有水面配置”，保留参数资产、位置/朝向和开关。旧组件类型及
原 GUID 仅保留为迁移输入，不再执行渲染。迁移只移除旧水面组件或其无其它
内容的专用宿主，不删除用户添加的其它组件/后代。迁入动作可 Undo；保存由
关卡编辑器统一入口完成。Play 中编辑控件只读，Game/Scene 预览继续可用。
下方独立 Sample/Inspector 的创建、停用与图证描述属于迁移前阶段；现行入口
与生命周期以上述 GameManager/session 为准。

历史原生编译 job `d0bcc8ad0a23487cadef6d4f9f00cdc4` 在 2026-10-10
15:50:29 UTC 完成，确认 refresh、真实编译 started/finished、domain reload、当时
Editor ready 与 Console errors=0；此回执不证明最新 Editor ready。AI Lab
场景已通过 Unity 保值迁入并统一保存：旧水面组件数为 0，参数资产 SHA256
仍与迁移前一致。实际控件修改水色浓度会标记模块/资产 dirty，Undo 精确还原；
关闭浅水后 session 与 owned camera 数为 0，重开后自动恢复。实际统一保存
返回成功，Scene/Settings/module dirty 均为 false；clean OpenScene(Single)
重开仍读到相同配置和普通 C# session。

再次正常 Play 通过临时独立 AI Lab 草稿启动两个 ForestRanger，真实连续运行
1.2 秒后暂停，记录 Footstep=4、Movement=4、Landing=2、active=10，Game/Scene
均实际绘制且 Issue 为空。Landing 来自这次真实起场落地，没有另外重验跳跃。
`Temp/ShallowWaterEvidence/56_water_level_editor.png`、`57_migrated_water_scene_play.png`
与 `58_migrated_water_game_play.png` 已实际查看；结构化回执为 `59_water_panel_validation.json`。
本轮 Play 已退出并恢复原 Scene 视角。MCP Test Runner job
`dc82be53851448c0b8f5a032cf11b58a` 初始化超时，0 tests started；随后在原生
EditMode 直接调用无 setup 的六个 ShallowWaterTests 方法及其 NUnit 断言全部通过，
这只是六项直接 fixture 断言通过，不能描述为 Runner 6/6。已有一次只读 Console
检查为 0 errors；之后最终状态读取调用返回 success=false、message=null、data=null，
没有取得状态，不宣称最新 Editor ready。本轮没有新的 Unity 独占放行，存在与
关卡任务共享会话的重叠；历史编译与图证不构成无并行干扰证明。收到交接通知后
停止 Unity 调用，后续控制交回关卡草稿域重载任务，不修改其快照接缝。

Undo 已验证范围是旧水面迁入后的参数编辑。新建浅水参数资产走
AssetDatabase.CreateAsset，没有登记资产创建 Undo；本轮未走新建资产路径，也
未验证新建后的资产撤销。这里不声称新建浅水或资产创建 Undo 已通过原生验证。

## Scene 窗口独立预览（2026-10-10，D3D11 原生验证完成）

当前已支持直接在 Scene 窗口查看浅水、倒影和运行时涟漪，随视角与参数自动刷新。
源码原生验证 job `07c38126ce6c40618b5c9c073fa8d67f` 确认 refresh completed、
compilation started/finished、domain reload observed、Editor ready、Console errors=0。

实现沿用现有 `CharacterTest.ShallowWater` provider 和 shader，新增按实际
Scene Camera 索引的独立预览 context。每个可见 Scene 窗口持有自己的镜像
相机、HDR/depth RT、颜色采样 wrapper、投影矩阵和 MaterialPropertyBlock；
Game 的 RT、矩阵、计数和设置不被 Scene 预览覆盖。Scene pass 从当前相机
图读取 opaque color/depth，不跨相机复用 TextureHandle。镜像相机通过现有
辅助相机 marker 关联此场景的 Game lighting owner，世界照明资源仍共享。

Editor 只发现活动场景及可见的 Scene 窗口，跳过隔离 Prefab Stage；摄像机
移动、缩放、窗口变化和 Inspector 参数更改触发合并刷新。Edit 反射请求
仅从后续 Editor update 提交，GUI/render 回调只安排工作，不嵌套渲染请求。
稳定 Edit 视角复用反射缓存，背景水纹最多 15 次/秒刷新。Play 镜像相机进入
原有 depth 排序相机循环，并登记到同一 presentation pose/geometry 观察集合，
不额外调用 gameplay tick、角色动画推进或 presentation traversal。

关闭/隐藏 Scene 窗口、关闭 sample、切换场景、Play 转换或 domain reload
时释放对应 Scene context；关闭水面/倒影及离开水面视锥释放该 context RT。
镜像目标复制 perspective/orthographic lens，仍遵守最长边/距离/深度格式
配置。Scene 预览共用同一个运行时波包与时钟，不注入测试波纹。

原生窗口验证已完成：自由透视与第二个正交 Scene 窗口均显示水面及倒影；两个
Scene 反射 RT 彼此独立，且均与 Game RT 不同。Inspector 的 SerializedObject
临时修改 TintOpacity=.12 后，Game/Scene 反射计数从 4/8 自动增加到 6/10；
已按完整 JSON 恢复设置及原 clean 状态。关闭 ReflectionEnabled 后所有 RT
释放，相机禁用；关闭 component 后 Scene context 和 owned camera 均为 0。
恢复 component、关闭临时第二窗口后只保留一个 Scene context。

最终预设从 clean 状态 OpenScene(Single) 重开同一场景后，Game/Scene 自动
重建并绘制，Settings/Scene dirty=false，Issue 为空。正常 Play 通过 AI Lab
原 StartSession 创建两个 ForestRanger，仅使用独立临时草稿。初次 StepFrames
快进不作为接触事件证据；随后实际连续运行 1 秒并暂停，记录真实 Footstep=5、
Movement=6、Landing=0、active=11。暂停中改变 Scene 视角，两角色 pose version
均保持 134、frame 保持 6992，Clock 保持 `1.324635149911046`，波包计数和
Game 相机 `(0.36,3.28,-5.63)` 未变，未因 Scene 渲染重复推进姿态。
`49_scene_cold_reopen.png`、`50_game_cold_reopen.png`、
`53_scene_real_contact_ripples.png`、`54_game_real_contact_after_scene.png`
均已查看实际像素；Scene 中可见角色倒影与脚边波纹，黄色圆圈为原 AI 选中标记。
证据与结构化回执位于非版本化 `Temp/ShallowWaterEvidence/46_*` 至 `55_*`。

已退出 Play，恢复原 Scene 个人视角；最终 Game/Scene 反射均 ready，Issue 为空，
Scene/Settings dirty=false。原生 LogEntries 为 errors=0、warnings=10、logs=69；
MCP Exception 标签会误标普通 Dispose info，采用原生计数。未重跑长性能
benchmark，未重启 Unity，不外推 Player 或其它后端。Render request 使用 Unity 的
[URP SingleCameraRequest](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/User-Render-Requests.html)。

## 默认 Game 视角可见度调整（2026-10-10）

自动刷新修复已解决反射 RT 不更新的问题，但默认俯视下原有 Fresnel 权重仍低，
旧的 36×28 m 覆盖范围也没有覆盖完整工作区。本轮扩大并重新对齐水面，在
原实现中增加反射可见度下限、反射扰动、真实波包的有界波峰亮度和宽度。
没有增加反射相机、纹理或 pass，仍使用固定 32 波包容量。
`MinimumReflection` 是美术可见度控制，并非物理介质 F0；实际反射权重为
`saturate(lerp(MinimumReflection, 1, fresnel) * ReflectionStrength)`，再乘投影
有效性。`RippleSheen` 使用现有接触/移动/落地波包及其衰减，不虚构接触事件。

正式持久配置以 Unity 保存的
`Assets/Config/MxFramework/Rendering/ShallowWater/CharacterTestRuntimeAiLabScene.asset`
为准。以下是本轮保留值，不以新建资产的 C# 默认值覆盖此资产：

| 配置 | 此场景正式保存值 |
| --- | --- |
| Size / ReflectionStrength / MinimumReflection | `(72,50)` / `.9` / `.32` |
| ReflectionDistortion / RippleSheen / RippleWidth | `.03` / `.08` / `.35` |
| TintOpacity / Tint RGBA | `.05` / `(.31,.5,.57,1)` |
| BackgroundSlope / RefractionStrength / ContactSoftness / EdgeSoftness | `.012` / `.018` / `.014` / `1.2` |
| RippleLifetime / RippleSpeed / FootstepAmplitude / LandingAmplitude / WakeSpacing | `2.2` / `1.6` / `.027` / `.065` / `.45` |

`Assets/Scenes/CharacterTestRuntimeAiLabScene.unity` 的 Sample 已通过 Unity 保存为
position `(-1.3,.04,-1.01)`、水平 rotation、单位 scale，SurfaceEnabled /
ReflectionEnabled / RipplesEnabled 均开启。半分辨率、最长边 1024、反射距离
65 m 保持原值。Settings 新字段初始值为 MinimumReflection `.32`、
ReflectionDistortion `.03`、RippleSheen `.065`、RippleWidth `.35`，Size 默认
`(72,50)`；源码 RippleSheen 默认 `.065` 与此场景资产的 `.08` 分开。

源码验证 job `792c23bce6e346e4bc0be2aac461ce76` 确认 refresh completed、
compile started/finished、domain reload observed、Editor ready、Console errors=0。
保存正式配置后实际进入 Play，通过 AI Lab 原有 StartSession 启动两个
ForestRanger，使用独立临时草稿，没有保存或覆盖用户草稿。运行相机由既有
camera 模块自然选择 pitch 35°，没有人为降低相机。同一暂停 frame 103、
相机 `(-.41,3.28,-5.64)` / `(35,0,0)`、相同角色姿态的实际 Game 窗口 A/B：

- `Temp/ShallowWaterEvidence/40_normal_play_before.png`：临时旧视觉响应。
- `Temp/ShallowWaterEvidence/41_normal_play_restored_after.png`：恢复正式配置。

两张原图已实际查看，人物倒影明显增强，地砖及砖缝仍可见。两图均使用正式
水面尺寸与位置，仅比较视觉响应，不代表旧范围与新范围的完整比较。早先
直接读取 GameView RenderTexture 的截图存在上下翻转和显示颜色差异，不能
代替这组实际窗口证据。累计接触波 16、移动波 21、初始落地波 2，暂停时
有 7 个真实活动波包；黄色大圈是 AI 选择标记。本轮未重复显式跳跃验收。

“临时参数已恢复”指 A/B 旧响应（MinimumReflection `0`、ReflectionStrength
`.85`、TintOpacity `.018`、Tint `(.38,.52,.55,1)`、BackgroundSlope `.006`、
RefractionStrength `.006`、ContactSoftness `.018`、ReflectionDistortion `.012`、
RippleSheen `0`）已撤销，完整 JSON 相等确认回到上表正式配置；旧响应未保存。
性能比较临时关闭的 SurfaceEnabled 也已恢复。正式视觉增强仍保留。退出本次
Play 后 Scene/Settings dirty 均为 false，相机恢复编辑态 `(0,38,-32)` /
`(60,0,0)`，Issue 为空，没有遗留采样回调或对比快照。退出后的实际默认
Game 图为 `Temp/ShallowWaterEvidence/43_default_edit_after.png`，已查看，编辑态无角色。

新预设的 clean OpenScene(Single) 重开及再次正常 Play 已在首节 Scene 预览
验证中补齐，Game/Scene 均实际绘制，退出后设置与场景均 clean。未重启 Unity；
下方旧视觉配置的历史自动预览验证仍保留其阶段边界。

本轮性能数据为 D3D11 Editor、相同暂停角色和 7 活动波包，两轮关闭水面/
完整水面比较，各 15 个不同 timestamp 预热帧后采集 40 帧，原始数据为
`Temp/ShallowWaterEvidence/42_visual_frame_timings.csv` 与 `42_visual_frame_counts.csv`。
主线程中位数 baseline `2.7675 / 2.6052 ms`、完整水面 `3.8592 / 3.8588 ms`；
render thread baseline `1.0053 / .9869 ms`、完整水面 `1.2284 / 1.2302 ms`。
这是完整水面相对关闭水面的成本，不是本轮新控制相对旧 shader 的增量。
GPU 中位数 baseline `2.2052 / 2.1238 ms`、完整水面 `1.9190 / 1.9441 ms`，
采样噪声使其不能证明 GPU 增量。开启期间各 55 个绘制帧记录 surface/reflection
各 55 次，关闭时均为 0；owned RT 仍为 9 MiB。不宣称 Player 或 32 波包性能。
其余功能和生命周期边界见下方各阶段的历史记录。

## 编辑态正常刷新修复（2026-10-10，D3D11 原生验证完成）

用户按“打开场景 → Select Sample → Game”操作时没有明显水效。现场只读读取确认
Sample、Owner、Settings、Shader 和三个开关均已持久化，但当前反射 RT 全黑；
多次读取水面/反射绘制计数停在 5/2。此前测试显式调用 `RenderReflectionNow()`
所得图证不能证明正常打开场景的预览入口可用。

`CharacterTestShallowWaterPreview` 现在独立负责编辑态派生预览：正常 Editor update
发现当前场景样区，合并场景/资产变化、Undo、菜单选择及源相机/参数变更；
下一次 Editor update 在其他 preview owner 的前次更新后、渲染回调之外提交一次反射请求，
随后请求 player loop 与 Game 窗口重绘。不保存资产、场景或用户稿，不启动 gameplay。
编辑态反射相机保持 disabled，Play 保留原来按 depth 排序的启用相机；没有成功完成
反射绘制的 RT 不参与水面采样。渲染 context 与自身 request 的重入守卫防止递归提交。

Game 窗口可见时，连续水纹预览最多 15 次/秒并复用反射缓存；稳定场景不重复提交
反射。设置/相机指纹检测最多 4 次/秒，属于 Editor LowFreqAlloc 路径。
此阶段尚无 SceneView 任意视角水面与 Mx 面板集成，后续实现与原生证据见
前两节。本阶段修复以冷开场景、正常
Select Sample、参数变化自动触发后的实际 Game/反射像素和计数验证；不以测试
显式调用反射或主相机渲染请求代替。

原生验证 job `96d8970e8b6b4a31b75da2e9ec20123f` 确认 refresh completed、
compile started/finished、domain reload observed、Editor ready、Console errors=0。
第一版 delayCall 在未获焦点的 Editor 中一直待执行，已改为下一次 Editor update。
正常 OpenScene(Single) 冷开时旧 Sample/camera/RT 销毁，自动反射计数 1；
随后正常 Select Sample 自动增至 2。以 Inspector 的 SerializedObject 路径临时
改变 TintOpacity=.12，自动增至 3，Game 像素变化 517,421 个（任一通道差 >2）；
参数恢复 .018 后计数 4，Settings 完整 JSON 和原 dirty 状态恢复，未保存。
稳定期间 surface 从 834 增至 1984，反射保持 4；仅使 owned RT.Release() 后，
自动重建并绘制第 5 次，反射 576,192/589,824 像素非黑，Issue 为空。
全部图证直接读取现有 GameView/反射缓冲，测试未显式提交相机渲染。

原相机俯角为 60 度，当前无角色；与修复前相比，默认 Game 有 217,790 个像素
通道差 >2，最大差为 6/255，视觉仍偏淡，不能把自动刷新验证描述为强镜面视觉验收。
本轮未改持久相机、场景或 Settings，也未重验 Play、Player 或跨平台。
证据与当前实际图位于非版本化 `Temp/ShallowWaterEvidence/25_*` 至 `30_*`，
结构化回执为 `31_edit_preview_validation.json`。现有 CombatDesign 缓存写入及
弃用警告保留；没有新增水面 error。本段是自动 Game 预览阶段的历史记录；
SceneView 与 Mx 面板的后续实现见前两节。

本页记录 CharacterTest 当前场景的局部浅水样区，框架通用接口见
[Rendering](../Interfaces/Rendering.md)。这是表现层薄膜，保留原地面几何、
碰撞和运动 authority；不会新增游泳、浮力、涉水阻力或 Replay 状态。

## 创建、调整与停用

在 Edit 模式打开 **MxFramework → Level Editor → 场景渲染 → 浅水**，点击
“添加浅水”或“迁入现有水面配置”，调整面板字段后使用统一“保存”。
要求 GameManager 已绑定既有 `MxRenderingPipelineFeature` 和源相机，且
`WGame/ShallowWater` shader 已导入。通过 Unity 创建或复用本场景参数资产
`Assets/Config/MxFramework/Rendering/ShallowWater/<scene>.asset`，不创建水面
GameObject。旧 CreateSample/SelectSample 源码兼容入口改为打开关卡编辑器，
不再提供独立 Mono 的菜单编辑入口或自动保存 Scene。

配置 Position.y 是水平水面高度，Settings.Size 是世界米制矩形；只支持 Y 轴
朝向，不支持倾斜水面。默认尺寸 72×50 m、高度 0.04 m，需要按真实地面高程
调整。每个 GameManager/renderer pipeline 只配置一个样区。

| 入口 | 作用 |
| --- | --- |
| SurfaceEnabled | 整个薄膜开关；关闭后释放水面会话与所有 owned 资源 |
| ReflectionEnabled | 单独关闭反射绘制，保留薄膜颜色与折射 |
| RipplesEnabled | 停止新增交互包与 shader 涟漪；ClearRipples 清空历史 |
| GameManager disabled / inactive / scene unload | 释放 provider、反射相机、RT、mesh、material 与事件订阅 |
| ReflectionScale / MaximumEdge / Distance / Layers | 控制额外视图像素、距离与对象范围；UI layer 5 固定排除 |
| ReflectionStrength / BaseReflectivity / MinimumReflection | 控制倒影权重、Fresnel 及美术可见度下限，保留原地面纹理 |
| ReflectionDistortion / RippleSheen / RippleWidth | 反射 UV 扰动、真实波包的有界波峰亮度及波包宽度 |
| TintOpacity / ContactSoftness / EdgeSoftness | 保留地砖纹理，并柔化深度接触及矩形边界 |
| MaximumWaterDepth | 排除 clear depth 与远低于水面的底面，默认 0.15 m，仅覆盖浅水 |
| BackgroundSlope / RefractionStrength / HighlightStrength | 低幅度水面起伏、折射与日光高光 |
| RippleLifetime / Speed / FootstepAmplitude / LandingAmplitude / WakeSpacing | 控制交互持续、传播与运动包间距 |

## 当前实现

一个镜像 Base camera 在源相机之前渲染，拥有半分辨率（默认）RGBA16F
颜色目标，最长边默认 1024，距离默认 65 m，无 MSAA/post-processing。
Camera Output Texture 含平台支持的 depth-stencil；当前 D3D11 实测为
`R16G16B16A16_SFloat + D32_SFloat_S8_UInt`。创建、resize 与 re-enable
均检查深度格式可 render 且非 None；同尺寸缓存亦检查 IsCreated、
实际宽高与有效深度格式，失效则释放并重建；失败暂停并报告 Issue。
采样另用同一 RT 的 RenderTargetIdentifier wrapper，加显式颜色
RenderTargetInfo 导入图，沿用 URP 的 color/depth 视图约定，不复制颜色 RT，
也不把 mixed color/depth RTHandle 直接作为普通采样 import。
镜像矩阵反转 Y，oblique clip 排除水面以下内容。该 camera 显式关联源
camera，沿用既有 world GI、shadow provider 与植被 per-camera 链；每路仍有
自己的屏幕 resolve、culling 和绘制开销。World lighting 数据不复制。
自定义阴影若场景未启用，则不由水面隐式启用。

水面通过 `CharacterTest.ShallowWater` provider 进入既有 RenderGraph，
接纳源 Game camera 及已准备独立 context 的 Scene camera。在透明物之前复制当前 opaque color，使用同图
depth 作交界及轻微折射，再叠加倒影、Fresnel 和受控高光；不写 depth、
不投影水面阴影、不生成反射递归，也不采样跨图借用句柄。高光读取
WGame 既有 main-light visibility，沿用启用的自定义阴影或 URP 主光阴影。
Scene pass 使用独立反射 RT/矩阵和当前相机图的 color/depth；Game 绑定保持独立。

反射 view 在既有 presentation traversal 前显式登记到 pose/geometry
LOD 观察集合；原画面与倒影合用一次角色表现更新。此登记是表现需求，
不改动画 authority、接触求解、Runtime AI Planner 或伤害链。

脚部等 floor-facing BodyPartContact Started 事件可在接触点发包；
同一 motion tick 的落地事件发更强包；已落地角色每移动默认 0.55 m
发一小包。直接读取已有固定步进事实，不为水面另做 Physics.Raycast
或 GPU readback。接触严格限定在样区水平高度附近；floor-facing 身体部位
接触也可能触发，当前未增加单独“脚骨”语义过滤。
最多 32 个包，超量替换最老项；主体轨迹最多 128 个；瞬移超过 3 m、
离水/离地、时钟回退重置追踪，包按寿命衰减。它是低幅法线扰动，
没有流体仿真、泡沫或溅射几何。

GameManager OnDisable/OnDestroy 幂等释放水面会话全部 owned resources 和事件/相机观察；
场景退出、域重载依靠 Unity 生命周期重新绑定。尺寸变化只重建 RT。
反射关闭或样区离开视锥时停止 camera 并释放目标。绑定失效、跨场景源、
倾斜或非单位 scale 也暂停 provider/相机并报告 Issue。源相机的 URP camera
data 必须已有，不向用户源相机隐式添加组件。
源 camera 停用或切到 Overlay 会暂停反射；yaw 旋转使用旋转后的 AABB。
Play 表现水时钟在 GameManager pause 时冻结，不改固定步进 authority。

## 证据与验收边界

以下保留分阶段历史；当前 Scene 预览及最终预设补充验收见首节，原 Game 功能
范围与测试边界见末尾“本轮限定验收完成”记录。

源码与五项 focused EditMode fixture 已落地。完整编译 job
`d16986488cad4bb3ae81cf46127078de` 确认 refresh completed、compile
started/finished、domain reload、Editor ready 和 Console errors=0。
后续原生渲染发现 mixed color/depth import 错误，已改成 color-only；
修正源码实际加载后 surface 与 reflected view 均实际绘制，shader messages=0，
GI session 绑定成功，Console errors=0。修正后的完整编译调用在 reload
断连，不记为完整验证通过。focused job `d0a761fcba3a4cb39121c22557bc839c`
在 MCP 15 秒期限内没有 RunStarted，先被标为初始化超时；Unity 随后
实际启动并结束测试。2026-10-10 10:16:58Z 的原生 NUnit XML 确认
5 passed / 0 failed，覆盖反射矩阵、包数量上限/替换、寿命/时钟回退、
无效输入及辅助相机 marker 停用/销毁准入。已核对五个完整测试名并
复制回执到 `Temp/ShallowWaterEvidence/06_shallow_water_editmode_results.xml`。
此 XML 纠正工具超时状态，未扩展到运行期角色交互或完整样区资源退出。
当前基线来自 `CharacterTestRuntimeAiLabScene` Edit SceneView，画面有地砖、
道路、围栏、建筑与车辆，没有运行期角色。原图位于本地非版本化
`Temp/ShallowWaterEvidence/00_scene_baseline.png`。

原 Game camera 实图为 `02_water_reflection_native.png`；临时低角度
实图为 `05_water_tuned.png`，捕获后 camera transform 已恢复。后者能看到
围栏、车辆与塔体倒影并保留地砖纹理，当前没有角色。对应 Settings asset
已由 Unity 单独保存 ReflectionStrength=.85、BaseReflectivity=.055、
ContactSoftness=.018、TintOpacity=.018，其余沿用初始设置。
raw reflection 为 `03_reflection_raw.png`。首次失败黑图
`01_water_reflection_initial.png` 保留诊断，不作为验收图。

计划按同一 camera/input 对照基线、薄膜、反射、交互四档；后续将实际
编译/测试结果、PNG 和资源计数记录在本节。当前反射目标 1024×576，
理论 owned bytes 当前为 9,437,184（9 MiB，颜色8 + D32S8格式块8 bytes/像素），
不能替代 GPU 显存测量。额外 opaque copy、屏幕 depth/normals、
per-camera GI 与植被成本不包含在此值。当前没有完整 CPU/GPU 帧耗时
或 Player 性能结论，也没有 Player build/跨平台验收。

2026-10-10 后续原生验收：完整编译 `0e4f31e82e1946bb96c2ef7d8029ed17`
满足 refresh、compile start/finish、reload、ready 与 Console errors0。
用户报告的 Camera Output None-depth warning 已在真实反射 camera
`R16G16B16A16_SFloat / None` 上确认。此前 color-only output 仅解决了
普通 sampling import 检查，遗漏 camera output 深度要求；现按上述双视图
约定修复，旧目标已释放。
`08_depth_lifecycle.txt` 回执确认 1024→512 resize、disable→enable、
reflection off/on 与 source disable/re-enable 清理/恢复，输出均为支持的
D32S8；连续3次 source surface 与3次 reflected view 实际绘制，GI session
无 Issue，Console 无该深度警告及无 error。主相机实图为
`08_depth_fixed_repeat.png`。测试临时设置及 source enabled 均 finally 恢复。
角色已通过正式 AI Lab StartSession 部署既有 ForestRanger，后因深度修复
结束本次临时 Play 会话；尚未完成角色接触/涟漪或反射 pose 验收。

同尺寸故障注入 `09_same_size_recovery.txt` 确认：1024×576 RT 调用
Release 后重建；将同尺寸 RT 创建为 None depth 后也重建；最终输出
D32S8、IsCreated=true、camera target 一致、Issue 为空。重建后又实际
绘制 source surface 与 reflection 各1次，像素见 `09_recovery_rerender.png`。
编译 `a6bcfb8022904b5882b959dcd0320ea9` 满足完整
refresh/compile/reload/ready 证据且 Console errors0；未重复原五项基准测试。

后续角色像素检查发现 RT 投影 Y 与采样 origin 不一致，已按
UNITY_UV_STARTS_AT_TOP 修正；涟漪由波高的空间导数计算法线，并将坡度
限制为 ±0.25。编译 `57516c9925a1467eb1dc8e359da286d2` 通过完整默认验证。
仅新增 yaw 测试 `70ed886b5cf24956aa1dc4207d7d9fef`：1 passed / 0 failed。
shader 无 error，有共享方向阴影 include 的两个潜在未初始化 warning。

正式临时 AI Lab 会话部署既有 ForestRanger；真实运行曾累计 contact24、
movement25、landing2（初始落地，未完成显式跳跃）。第二会话稳定状态
为 contact3、movement4、7个活动包；同 camera/pose 的 baseline、film、
reflection、ripple 图为 `12_ab_0..3.png`，近景为 `14_close_ab_0..3.png`。
已查看像素：角色与塔体倒影方向正确，地砖仍可见；微涟漪克制，尚需
独立跳跃/落地与反射视图独占 pose LOD 验收。黄色范围环是既有 AI Lab
调试图形，当前 layer0，仍会进入反射，不能当作水面涟漪证据，后续须排除。

本机 D3D11 Editor、暂停相同角色状态、7活动包，2轮每档20帧 warmup，
每轮57–60个不同 FrameTiming timestamp 样本，见 `13_frame_timings.csv`
与 `13_frame_timing_summary.json`。baseline 主线程中位数2.592–2.789 ms，
薄膜2.553–2.725 ms，反射3.481–3.656 ms，完整3.518–3.663 ms；
反射相对 baseline 增量约0.87–0.89 ms，render thread 约0.26–0.29 ms。
GPU中位数baseline1.416–1.946 ms、完整1.416–1.696 ms，轮间噪声明显，
不能据此宣称无 GPU 成本；亦不代表 Player 或32包上限性能。逐档计数
确认 baseline draw/reflection均0，薄膜无reflection，反射及完整各每帧1次。
owned RT 为9MiB，关闭surface/reflection均释放为0。当前样区可编辑，
完整功能验收仍未结束；本次 Play 已退出，camera/toggles与原场景状态恢复。

## 本轮限定验收完成

最终 C# 编译 `c9170d6addba4d1c8ca69e870725c876` 满足 refresh completed、
compile started/finished、domain reload、Editor ready、Console errors0；本批
PreviewOverlay 使用 begin/endContextRendering 与 begin/endCameraRendering，
未引入旧 frame 事件警告。退出并重载后原生 LogEntries.GetCountsByType
为 errors0、warnings10、logs72；warning 包含既有 VFX profile 缺项、
CombatDesign 本地 owner.json 写入失败及临时本地输入授权拒绝，不是水面错误。
MCP read_console 将普通 Dispose info 显示为 Exception；此处按原生计数
报告，未清空或隐藏 Console。共享阴影 shader 的两个 warning 仍保留。

PreviewOverlay 是明确的 owner 标记，仅缓存它创建的五个调试 LineRenderer。
同场景、来源匹配且 Kind=Reflection 的相机作用域内暂设 forceRenderingOff，
随后恢复各 renderer 原值；普通相机（含嵌套相机）读取最外层原值。
八层固定 scope 容量内无热路径分配；超出容量会展开恢复后开始新的 scope。
end-camera、context 边界、disable/destroy 均展开恢复并解除对应订阅。
主相机本已排除 UI layer，故未改变 debug line layer、camera mask、
renderer feature 或 layer0 中其它对象。`15_overlay_scope_recovery.txt`
验证嵌套 reflection/main/reflection、原 mixed mask=2、缺失 end-camera 的
模拟异常及 disable/re-enable；`16_overlay_primary.png` 与
`16_overlay_reflection.png` 原生像素确认主视图保留黄色环、反射排除这五条线，
渲染后 force-off=0、scope=0。此例不声称过滤其它未标记的世界调试图形。

显式 RuntimeCommand 跳跃实际经历 grounded→airborne→grounded，快速松开
最高 y=810 mm、最终 y=0、landing ripple 增加1；逐帧记录为
`17_jump_authority.csv`。第二次持跳实图 `18_jump_airborne.png` 的
authority y=2125 mm。`19_explicit_landing.txt` 精确确认 entity3 落地，
frame275、y=0、grounded=true、向下速度13196 mm/s、landing ripple delta1。
再允许0.3秒实际运行传播，`19_landing_wave.png` 可见脚边克制的局部环波及
角色倒影。未移动表现 root 或直接伪造落地包。FootstepRipples 统计来自
朝上的 BodyPartContact Started，属于底面身体接触，并非逐脚动画事件计数。

反射独占视角：临时 source=(-14,.35,-7)、pitch45，真实角色 bounds 位于
source frustum 外、reflection frustum 内。既有观察器实际查询为 main
Visible=false / ScreenHeight0，reflection Visible=true / ScreenHeight=.955479，
两者 Known=true。`20_reflection_pose_demand.txt` 与两张 `20_pose_only_*.png`
记录真实反射 demand；pose version240→240、publication frame15316→15316、
policy evaluations827→827，camera 绘制没有再次发布 pose。此角色 cloth=true
且 shadow pose safeguard=true，所以仍完整 pose；未宣称 cadence 降频收益。
这验证需求准入与单份 pose 复用，不代表所有动作/Raw skin backend 的穷尽验收。

资源生命周期：`21_owned_resource_lifecycle.txt` 确认 component disable 与
GameObject inactive 后 owned camera/RT 为0，旧 camera/RT/mesh/material
均 Unity-null；恢复后恰好1 camera+1 D32S8 RT（9MiB）。
`22_scene_reload_lifecycle.txt` 在 clean Edit 中卸载原场景，期间 owned
camera/RT=0、旧 sample/camera/RT Unity-null；重开同一个保存的 AI Lab 场景
后恰好1 camera+1 RT、Issue空、isDirty=false。临时空 host 场景已关闭，
最终只有原场景，相机(0,38,-32)/rotation(60,0,0)、water(-8,.025,0)、
所有水面开关恢复。原场景序列化 diff 仍仅为样区90行新增。
重载后再次实际绘制 reflection/source surface 各1次，像素与最终状态见
`23_restored_scene.png`、`23_final_state.txt`；原生 Console errors0。

当前已交付的边界是本机 Unity6000.6.3f1、URP17.6、D3D11 Editor 的局部
水平浅水样区。本段原验收入口为 Select Sample/Inspector，现行入口见首节。
Settings asset 可调材质响应、尺寸、反射预算和波包参数；旧 component/SurfaceEnabled/ReflectionEnabled
均可停用。角色仍通过原 AI Lab 正式开始测试入口产生；样区不会另建角色或玩法。
尚无 Player build、其它图形后端、32包上限 GPU benchmark 或任意场景适配结论；
CPU/GPU 数据与不确定性继续按上一节读取，不重复扩大测试。
该 A/B 测量早于最后的五个 debug renderer 过滤改动，未重跑最终版本
benchmark，不能把数值当成最终版本的精确成本。


## 脚踩水花首版

入口仍是 **Mx 关卡编辑器 → 场景渲染 → 浅水 → 脚踩水花**。水花开关归
GameManager.ShallowWater，Prefab、大小、最小深度、速度、冷却和预算归同一参数资产。
“准备水花资源”生成正式 material / prefab 并绑定水面参数；绑定可 Undo，保存由
现有关卡编辑器水面参数事务执行。新建的可复用资源独立保留，Undo 绑定不删除资源。
生成器仅保存本次新建的材质；已有脏材质被复用时保留其未保存状态与磁盘版本。

物理层 CombatPhysicalContactEvent 经角色模块 PhysicalContactObserved 转发；
TryResolvePhysicalContactPresentation 复用既有 Combat→Character ID 转换，读取注册期
缓存的部位语义：优先已声明的 PartKind；导入 Bone 行为 Custom 时复用 geometry 中
已声明的 left_foot/right_foot 抽象骨骼角色及其 ParentPartId 关联，不猜测骨骼名称。
当前采样只覆盖既有 PresentationAudio / PresentationOnly 接触配置，
没有新增 collider、raycast、动画定时脚步或逐帧碰撞采样，也没有完整 surface router。
水花运动观察在现有 TickPresentation 的接触发布前执行，使用同一展示帧号；
模拟 CurrentFrame 已推进时不以模拟帧号与展示接触强行比较。原有涟漪运动观察保持不变。

仅足部 Started、向上地面法线、真正浸水、稳定运动且有近处可见 Game/Scene receiver
时发射。水深由真实 contact.Point 相对已知水平水面计算，要求 .006m 以上且不超过
MaximumWaterDepth；X/Z 使用旋转后水域矩形。发射 X/Z 保留真实接触点，Y 投影到
水面上方 .003m。落地事件只提供增强强度，须匹配同帧足部接触，并只发一次落地 burst。
不使用实体 root 作为假脚点，移动尾波不会发射水滴。

初次注册、静止低速、重复事件、冷却、未观察/已移除实体、时钟回退、明确位置召回
以及超出运动速度容许的位移均抑制喷溅。回退/世界清空清理粒子和接触缓存。

固定粒子池默认 12 个 burst（硬上限 16），每个最多 24 粒子；每帧最多 4 次、
默认每秒 16 次（可配置上限 32），默认距离 30m、每脚冷却 .18s。
粒子生命 .28–.5s，.85s 强制完成上限。饱和直接跳过，不动态扩容或排队。
池预热后发射/推进无托管分配；脚部 metadata 只在既有注册/刷新时编译。

水花为浅水 session 私有表现资源 scope，复用框架 ResourceManager / Catalog / Memory
Provider / typed Handle；不会挂载或修改已封存全局 Catalog。共享 Prefab 不归该 scope
销毁；临时 views 与 handle 在禁用、资源替换、session Dispose 和 Play 退出时清理。
ParticleSystem 使用 World 空间和单一手动表现时钟，暂停时不推进，Game/Scene 不重复发射。
Prefab stopAction 必须为 None，禁止自行 Destroy/Disable/Callback；对象已被 Scene
销毁时安全跳过。清理逐对象执行，handle/provider 通过 finally 收尾，未完成的对象销毁
可重试；清理错误进入诊断，不把失败当作完成。
随机只使用局部粒子随机序列，不消耗 UnityEngine.Random，也不进入权威 hash/save/replay。

材质复用已存在的 Water_drop_nomipmap.png 银白 glint，使用 WGame/ShallowWaterDroplets
Shader；没有新第三方包。水面仍为平面涟漪，水花为独立的向上粒子。

验收需分别记录：完整 Unity compile/ready/errors、正常 Unity focused runner 结果、
实际脚踩与落地像素、静止/干地/平台/瞬移反例、资源加载/回收、暂停/禁用/Play 退出。
隔离策略检查与语法检查不替代这些原生证据。

2026-10-11 在 Unity 6000.6.3f1 的 CharacterTestScene 完成最终源码编译与
正常 EditMode runner 28/28（失败/跳过均为 0）。原生验证已覆盖真实足部触地行走、
跳起后同帧落地足部接触、静止收尾、手动暂停、session 禁用再启用、真实 SceneManager
卸载和 GameManager Play 退出。行走与落地分别观察到 14/21 个银白水滴并检查实际像素；
最终退出资源句柄释放一次，粒子和临时对象为 0，原生 Console error 为 0。
当前原生验收边界：两条干地路线未越过现有障碍，平台与拥挤预算场景未执行；
该场景角色未绑定召回 StageRun，真实召回、世界清空和现场 Prefab 替换未执行。
对应策略/资源回归通过，不能替代上述原生场景验收。
