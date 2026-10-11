# 当前场景浅水样区

> Status: Current
> 状态：historical D3D11 Game/Scene evidence retained; latest panel-integration Editor state and overlap isolation unconfirmed | 2026-10-10
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
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxSceneRenderingAuxiliaryCamera.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxSceneLightingProvider.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxDirectionalShadowProvider.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Rendering/MxRenderingPipelineFeature.cs`

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
