# 场景上色与共享细节工作流

当前入口：Blender 5.2，三维视图或 UV 编辑器的 **N → WGame 场景**。
安装：运行本目录 `install.ps1`，启用「WGame 场景调色板与细节 UV」。
更新已运行的插件后，保存制作文件并重启 Blender 使新版本生效。不要在保留 Scene 配色集合属性的同时
热注销其 PropertyGroup 类型；旧 RNA 引用可能导致 Blender 原生崩溃。

## 纯顶点色远山导出

打开 `ArtSource/Environment/DistantMountains/Blender_Work/distant_mountains_work.blend`，
在物体模式选中山体，使用 **N → WGame 场景 → 导出选中顶点色远山到 Unity**。
这是独立的纯顶点色入口，不进入下文的调色板／细节图联合烘焙。

- 要求 `DistantMountainColor` 为 POINT / BYTE_COLOR；拒绝 UV、未应用修改器和形态键。
- 新模型自动分配未占用的 `Mountain_XX` 编号，写在物体的 `wgame_mountain_export_id`，以后复用。
- `wgame_mountain_color_mode` 区分 BakedColor 与 AlbedoAO；现有两种远山预览材质也可识别。
- 仅在临时副本规范化旋转、缩放与底部中心原点；制作模型、顶点颜色及布局不改。
- 调用 Blender FBX 插件，导出线性 RGBA、法线与三角面，不生成 UV、切线或贴图。
- 源 FBX 位于 `DistantMountains/Final_Source/Pieces/`，同步至
  `Assets/Art/WGame/Environment/DistantMountains/Pieces/`；保持现有 Unity `.meta`。
- `distant_mountains_vertex_build.json` 保存稳定编号、哈希、尺寸、颜色模式和应绑定的材质。
  不覆盖清单未拥有或被外部修改的同名 FBX，不删除旧山。
- Unity 第一次导入新模型时，设置 Normals=Import、Tangents=None，按清单绑定
  `DistantMountains.mat` 或 `DistantMountainsAlbedoAO.mat`。这些 Unity 导入设置不由 Blender 按钮自动修改；
  本次已通过 Unity MCP 为 Mountain_14/15 设置并验证，后续同路径重导出保留导入设置。

远山 Shader 与顶点色约定见 `Docs/Guides/DISTANT_MOUNTAIN_VERTEX_COLOR.md`。

## 制作

### 新模型登记

在物体模式选中新加入的网格，打开 **N → WGame 场景 → 模型登记 → 登记选中模型**。
可以批量登记；已有 `Arch_`、`Ground_`、`Sec_`、`Veh_`、`Tool_`、`Prop_` 前缀的名称作为组件 ID，
其它名称使用对话框所选类别生成 ID。单选时也可填写明确的组件 ID。重复 ID 会拒绝登记；
已登记组件再次操作保持原稳定 ID 与数据不变。

登记保留几何、变换、材质槽、面颜色和已有 UV，只补充缺少的 `PaletteUV`、`DetailUV`、
调色板与表面类型数据。新 `DetailUV` 全面初始化为 `(-1,-1)`，不自动展开。
纯色材质可以直接登记；共享调色板材质需已有合法调色板数据。其它程序化底色先明确接入材质制作流程，
不会猜测或覆盖作者的节点图。批量登记先完整检查并准备副本，失败不留下部分登记的对象。

面板列出已登记、待登记、辅助网格数量及未登记名称，**选择未登记模型**可集中处理。
参考网格或布尔辅助件可显式**设为辅助模型**，以后用**恢复待登记**撤销排除；这不会隐藏或删除对象。
已登记组件不允许通过此按钮移出发布包。联合烘焙和拼装检查遇到未登记网格会阻止操作并列出名称，
避免新增模型被静默漏掉；明确排除的辅助网格不会参与烘焙。

- 制作文件：`ArtSource/Environment/Base/Blender_Work/base_work.blend`。
- 每个模型独立持有 **PaletteUV + DetailUV**。不增加 SurfaceUV，也不自动展开整个模型。
- 场景配色 Profile 独立于角色 Profile，但共用 `WGamePaletteTools` 的全局 RGB565 色板和 16 个表面类型。打开旧制作文件时，纯色 Principled 材质的实际 Base Color 是取色与 Profile 建表的颜色来源；表面类型优先读取材质的 `wgame_surface_profile`，否则沿用新模型登记的粗糙度／金属度分类（10–13）。已经切到共享调色板材质的面才读取 `PaletteToken` 与 `SurfaceProfile`。旧网格里残留的颜色或表面类型不能覆盖当前纯色材质，避免混凝土吸色后被涂成金属。更新旧 Profile 时仅补充缺失条目，保留已有条目及稳定分类编号，不重新涂色。新文件使用十个预设条目。
- 在 Profile 列表中选择颜色，再点「开始画笔绘制」；左键在 3D 视图逐面涂色，右键或 Esc 退出。Shift+左键填充同色连通区域，Shift+Alt+左键填充整个连通岛，Alt+左键吸色，F 或方括号调半径，0–9 切换前十个 Profile 条目，X 切换 X 轴镜像，Ctrl+Z 撤销上一笔。也可以点「应用到选中面」。底色画笔不要求先展开 DetailUV。
- 点「吸管吸色 (Alt)」后在 3D 视图点击任意场景模型的面；即使当前选中的是另一件模型，也会切到被点击面的 Profile 条目。从 UV 编辑器侧栏启动时同样要去旁边的 3D 视图点模型。取到当前已选条目时也会刷新颜色草稿；未收录的颜色会进入草稿并提示添加到 Profile。
- 「从模型导入已用」将已有面关联到 Profile 分类；展开「配置配色表」可以为草稿颜色添加条目、更新当前条目并同步引用它的场景面、删除未被使用的条目，以及将 Profile 保存／读取为独立 JSON。选快捷色只填写草稿，需要添加或更新条目后才能作为画笔颜色。普通 `.blend` 保存也会保留 Profile。
- 对原有纯色材质模型，底色画笔仅将涂到的面切到共享调色板／细节材质，未涂的面保留原材质。首次使用时自动选择唯一的共享 RGBA 细节图；存在多个候选时先在面板指定「细节图」。
- DetailUV 初始为 `(-1,-1)`，表示该面不使用细节。仅选择需要绘制的区域生成 UV。
- 已展开的 DetailUV 可以跨出图片边界或位于图片外，不强制打包到 0–1。共享细节材质按 CLIP 采样：图片内显示细节，图片外透明并保留底色；绑定、绘制和联合烘焙使用同一规则。整面 `(-1,-1)` 表示留空；整面收拢到图片范围外的同一点也按留空处理，例如 UV 整体平移后的 `(-0.58,-1.32)`。图片范围内的退化面、非有限坐标、及未收拢成一点的退化面仍会报错；最终导出 UV0 由烘焙器重新打包，制作 UV 不改。
- 「编辑细节 UV」接入现有 `WGameUVTools`，默认保持选区连续；封闭或非流形选区会提示处理，不偷偷自动切碎。
- 不同模型可以引用同一张 RGBA 细节图。每个模型的 UV 可以独立移动、缩放或重叠；共享图中重叠区域的绘画会同步显示。
- 选择另一张细节图，再点「让选中模型共用此图」，仅将已展开 DetailUV 的面切到这张图；未展开的面保留原纯色材质。切换前会把这些面的实际纯色材质底色转成最近的调色板颜色，不拿旧 PaletteToken 猜颜色。同一模型已有的共享细节面也会一起切图，避免一件模型实际引用多张细节图。
- 绑定纯色模型时复用现有技术色板，不删除其它共享材质正在使用的图片；复用旧共享材质时会补回缺失的色板连接，保证图片外的底色可用。
- 「绘制／擦除」只处理有有效 DetailUV 的面；擦除移除 RGBA 覆盖，露出调色板底色。
- 文字、编号、标识和路面导向线等定位贴花留给 Unity 中的独立物体；这张图只承载可复用的材质磨损与污迹。

### 使用透明细节切片

十五张独立透明 RGBA 素材保存在 `ArtSource/Environment/Base/Shared/stylized_environment_detail_stamps_v1_slices/`，包括八类磨损污迹、黄黑斜纹、黄环、卷帘门积灰、螺栓锈晕，以及罐体环焊缝积垢、黄漆剥落和加油口油迹。它们供作者自行排入最终一至两张共享细节图；`stylized_environment_detail_stamps_v1.png` 只保留早期预览排布，不作为最终 UV 布局。最终图的大小和各切片 UV 占比应按实际承载面、屏幕占比和清晰度检查确定，不由素材切片文件预先固定。

合图后在 Blender 图片编辑器打开最终 RGBA 图，在 **WGame 场景 → 细节图** 中选中它；再选模型，点 **编辑细节 UV**，只给需要图案的面点 **生成选区 UV**。在 UV 编辑器中把 UV 岛移到对应区域，点 **让选中模型共用此图**，切到材质预览检查，满意后点 **保存制作文件与细节图**。图案已经在最终图里时无需点「绘制」。同一切片可由多个模型重叠复用。

场景正式通道只有两套。角色旧版 `UV0 → PaletteUV → DetailUV` 的占位约定不是场景约定；
不要用角色的「整理为三个正式 UV」替代场景流程。临时 UV 备份须在正式发布前自行整理。

## 联合烘焙

### 自发光绘制（2.2）

1. 为需要发光的面展开 DetailUV，选中模型，在 **N → WGame 场景 → 自发光（Emission）** 点击 **创建 / 接入发光遮罩**。未绑定细节材质时会使用面板指定的细节图完成绑定；每张细节图共用一张独立遮罩，不覆盖已有绘画。
2. 点击 **绘制发光**，在模型或左侧图片上绘制。**画笔发光量**为 0–1：黑色不发光、白色最大、灰色较弱；发光颜色沿用底色与颜色细节的合成结果。**擦除发光**将区域画回黑色；原细节区的 **绘制 / 擦除**按钮可切回底色画布。
   **发光强度（HDR）**独立控制整体亮度，范围 1–64；新建遮罩默认 8，旧遮罩保持 1。灯头效果偏弱时可先设为 8，已画好的遮罩无需重画。强度按共享遮罩保存，同组模型一起变化，Blender 材质预览立即更新。
   笔画使用 Blender 原生撤销：在图片或三维绘制窗口按 **Ctrl+Z** 撤销、**Ctrl+Shift+Z** 重做。切换绘制、擦除和底色画布保留笔画历史；进入纹理绘制会退出此前的底色面画笔，避免快捷键被拦截。首次自动创建遮罩与后续笔画分别记录，撤销第一笔不会删除遮罩。
3. 遮罩使用 Non-Color、与底色共用 DetailUV；图外按透明处理，整面 `(-1,-1)` 不发光。使用相同细节图且 UV 重叠的模型会同步发光。不支持在不改变 UV 的情况下为这些重叠区域指定独立图案。
4. **保存制作文件与细节图**会保存并打包遮罩，默认文件名为 `Shared/emission_<稳定 ID>.png`。普通保存 Blend 也会打包像素；修改遮罩后旧烘焙候选失效，需重新烘焙。
5. 联合烘焙只将归一化遮罩写入 `SurfaceData.B`，HDR 强度单独写入发布清单每组的 `emissionIntensity`。BaseColor 与其余控制通道保持各自含义。遮罩原图留在制作侧，不增加 Unity 贴图、材质槽或 Shader 分支。制作预览与「查看本次烘焙」都使用相同 HDR 强度。

烘焙器仅在隔离副本中移除 HDR 倍率，并适配 `PaletteData.B × Emission Strength` 的现有遮罩合同，不改作者的 PaletteData；倍率不会被裁进 8-bit 遮罩。控制贴图以独立 RGBA 通道保存，Surface Profile 的 Alpha 不作为透明度去放大 AO、Rim 或发光值。Unity 材质计算 `底色 × SurfaceData.B × 发光强度`，HDR 输出可触发场景现有 Bloom；此入口不新增灯光或自动修改 Bloom，Blender 普通材质预览也不自动添加光晕。

`mx.environmentSharedBakedSurface.v2` 的 `groups[].emissionIntensity` 为可选正数（1–64）；旧清单缺省时 Unity 按 1 处理。更改 HDR 强度会使旧候选失效，需重新烘焙、发布、同步到 Unity，并执行组件 Prefab 更新，使生成的共享材质获得新值。

### 执行烘焙

点击「按共同细节图联合烘焙」：

可以从物体模式、编辑模式或纹理绘制模式开始。编辑模式的预检查直接读取实时编辑网格的 DetailUV，包含尚未保存的 UV 调整；不会为了读取数据切换制作模式、重置选区或改写制作文件。

1. 将当前网格与未保存的图像像素写入隔离快照，不覆盖 Work。
2. 按实际细节图资产及当前像素内容分组，而不是按物体名称或手工“维修区”分类。
3. 将同组的所有模型一次送入现有角色 `baked_surface.prepare` 和 `baked_atlas.compact`；共享贴图的同一轮烘焙按整组提交。
4. 在组内联合打包：相同材质函数、底色、表面数据且源 UV 重叠的细节可以复用；不同结果占用不同区域。
5. 保留每个模型的独立网格，输出自己的 UV0；同组共用最终贴图。

导出的 UV0 允许重叠。发光强度连接遮罩节点时，烘焙器同时检查底色和发光函数：
如果都只依赖同一 DetailUV，且其它底色、顶点控制值和表面类型相同，重叠细节仍然复用。
重复图案不会因为接入发光而复制成多个区域；依赖位置、法线或其它变化 UV 的结果仍独立烘焙。

纯色 Principled 材质也可以参与：当模型的 DetailUV 全部留空且实际使用的材质底色均为常量时，
模型进入独立的纯色联合组，无需先绑定空白细节图；烘焙读取当前材质底色，SurfaceData 继续使用
既有 PaletteData 合同。多材质槽不等于多张细节图，制作侧材质和 UV 不会被改写。
纯色底色直接写入合图，省去 BaseColor 的两轮 Cycles 烘焙；未绘制细节且使用自动尺寸时输出 512×512。
有已展开 DetailUV 却没有细节图、或单模型实际引用多张细节图时，烘焙会报告对应原因。

「烘焙设备」默认是自动：可直接读取底色的组使用 CPU，其余组优先使用 OptiX，次选 CUDA，
没有可用 NVIDIA 设备时回退 CPU。也可手动选 GPU 或 CPU；设备选择仅作用于隔离的后台进程。
GPU 只加速 Cycles 贴图计算，UV 排布与 FBX 导出仍由 CPU 完成。

没有“每模型先做整张图片，再把大图拼接”的步骤。空白区域不会被自动当作需要绘画的区域。
明确留空的面只在导出快照内走角色已有纯调色板优化；作者的 DetailUV 不变。
默认 AUTO 沿用角色的 512–4096 尺寸与细节密度检查；不能满足密度时给出错误，不静默降低清晰度。

## 产物

每组：

- `BaseColor.png`：调色板底色与细节已经合成，和角色一致。
- `SurfaceData.png`：角色原有 `aoRimEmissionProfile16` 布局，R=AO、G=Rim、B=Emission、A=16 类 Surface Profile。
- 每模型一个 FBX，正式贴图 UV 为 UV0。联合烘焙完成后，仅在隔离导出副本中统一为一个材质槽、所有面的材质索引为 0；Unity 导入为一个子网格，再绑定该组共享烘焙材质。制作源保留原有材质分区，不导出原始材质及其 PaletteUV／DetailUV 贴图引用。场景不携带角色 HairFlow/HairMeta 通道。
- 发布时在隔离的 Blender 后台进程里，从已烘焙的 `Sec_Wall_Straight_Long` 派生 `Sec_Wall_Straight_Long_SinglePost`：移除左端柱、延长墙板边缘，并把原本离地的宽墙面下沿压到铺装面；沿用 UV0 与共享贴图，不覆盖 Work。相邻段共用一根端柱，墙根和柱间都不得透天。不使用 Unity Cube 填缝。
- `environment_build.json`：`mx.environmentSharedBakedSurface.v2`，记录分组、模型引用、图片引用、角色打包统计和文件哈希。

同图不意味着无条件让所有面重叠。半透明细节叠在不同底色上会得到不同像素，必须在同组图集中分开。

## 查看与保存

「检查 Unity 拼装组件」读取当前制作文件及可见 Geometry Nodes 结果，在
`Temp/EnvironmentSurface/assembly_audit.json` 列出各件的实际尺寸、三角面数、底部原点、
地坪／墙／栅栏拼接步长和需要人工复核的比例。该检查不移动组件、不改 UV，
也不会把 `all.blend` 中的目录式摆放当成场景布局。

「查看本次烘焙」打开临时场景，使用同组共享材质；「返回制作」恢复制作场景与视角。
更改模型、UV、图片或相关制作参数后，旧候选拒绝发布。
「发布已查看的烘焙结果」保存源与画布，将已经查看的 FBX/PNG 字节写入 `Base/Final_Source`，不会重新烘焙。
「保存制作文件与细节图」保存共享图片并打包进 Work；无路径的画布保存到 `Base/Shared`。
保存不要求草稿先通过烘焙校验；纯色材质、未绑定的共享画布和多图草稿均可保存，烘焙校验只在烘焙阶段执行。

发布后的模型路径固定为 `Final_Source/Models/<类别>/<组件 ID>.fbx`，贴图路径为
`Final_Source/Textures/<细节图稳定 ID>/BaseColor.png` 和 `SurfaceData.png`。
`environment_build.json` 记录每件模型的分类、尺寸、原点、拼接提示、三角面数和文件哈希。
再次烘焙发布时，绘画像素变化不会改变模型或贴图路径；插件仅覆盖内容变化的文件，
并只清理上一版清单记录且哈希仍匹配的旧产物。

发布后点击「同步已发布源包到 Unity」：插件先验证清单中的每个文件哈希，
再将模型、贴图和 `environment_build.json` 复制到
`Assets/Art/WGame/Environment/Base/Published`。再次点击只更新内容变化的文件，
保留 Unity 为现有资源生成的 `.meta`／GUID。源包移除的组件不会自动从 Unity 删除；
按钮会报告旧版多余文件数量，以免误断已有引用。
Unity Editor 刷新资源后会导入文件；若未自动刷新，在 Unity 使用 **Assets → Refresh**。
发布清单导入完成后，Unity 自动按清单更新共享材质和组件 Prefab；模型从纯色组转入细节组时，
材质也会一起切换，避免新 UV 继续采样旧图集。自动更新先校验全部发布文件，失败会在 Console 提示。
**WGame → Environment → Update Base Module Prefabs** 保留为手动重试入口。
基地布局、玩法碰撞和场景仍在 Unity 中维护，步骤见
`Docs/Guides/ENVIRONMENT_BASE_SCENE_ASSEMBLY.md`。

Unity 的正式导入器使用 `WGame/EnvironmentStylizedLit` 烘焙模式消费此 v2 源包的
`SurfaceData` 合同，不把它解释为连续 roughness/metallic 通道。
默认最大导入尺寸与烘焙源图一致，避免 Unity 默认的 2048 上限缩小 4096 图集并混合表面类型编号；
项目明确设置的平台覆盖仍由该平台配置控制。

## 验证

`test_eyedropper_surface.py` 验证纯色材质取色时忽略旧网格表面类型、保留材质显式类型、编辑模式取色、旧 Profile 补充，以及取色后涂回的类型保持；共享调色板面仍保留作者明确设置的类型。实际 `Sec_Gate_ArmorWall_Left` 的混凝土、车身漆面和底盘钢材已通过射线取色与涂回检查，制作文件和 DetailUV 保持不变。

`test_group_bake.py` 在独立 Blender 场景实际烘焙，覆盖：同图同结果复用、同图不同底色不串色、
不同图分组、含 Geometry Nodes 修改器的导出、源文件不变、输出只保留 UV0。
多材质回归验证不同底色和 Surface Profile 经烘焙后仍保留，FBX 回读仅有一个材质槽，几何、UV 与法线不变；重新打开制作源仍保留原有材质分区。
跨边界回归实际烘焙穿过四边和完全位于图片外的 UV，检查图片内的细节、图片外的底色、最终 UV0 与源文件保持。
测试生成的图案只存在于 `Temp` 测试夹具，不写入用户的模型或共享画布。

`test_constant_save.py` 验证纯色材质保存、未绑定画布保存、纯色联合烘焙的颜色保持，以及
多图草稿可保存但仍拒绝单模型多图烘焙。2026-09-23 当前 base_work 的 77 个纯色模型已通过
保存和烘焙前置检查，保存前后制作内容指纹一致；该检查不等于全库烘焙／发布完成。

`test_emission.py` 覆盖发光遮罩创建与复用、绘制/擦除/底色切换、保存重开、候选失效、灰度/白色/黑色/越界/留空区域的实际烘焙，以及底色和其它 SurfaceData 通道保持。相同底色与发光函数的模型导出重叠 UV0，不同底色仍分区。共享烘焙器另验证控制 PNG 的 RGB 不受表面类型 Alpha 影响，并拒绝位置依赖或其它变化 UV 驱动的发光复用。
`test_emission_undo.py` 使用独立图形 Blender 窗口（`--factory-startup --enable-event-simulate`，不加 `--background`），执行原生笔画与真实 Ctrl+Z / Ctrl+Shift+Z 事件；默认验证图片编辑器，追加 `-- --view3d` 验证三维投射。覆盖旧底色画笔退出、首次遮罩创建、连续撤销/重做、擦除以及跨底色/发光画布的历史顺序；结果位于 `Temp/EnvironmentSurface/emission-undo-<模式>/result.json`。
`test_edit_mode_bake.py` 在普通 UV 数组为空的编辑模式下验证实时 UV 预检查，并执行「开始烘焙 → 隔离快照 → 实际后台烘焙 → 候选校验」，检查编辑模式、选区、未保存 UV、发光图片和已保存 Work 均保持不变。

角色烘焙器仅增加一个可选 `uv_initializer` 参数，默认仍执行原角色实现。
场景在隔离快照里为未承载细节的面准备临时色块 UV，不在 Work 中生成碎片 UV。

原始 `all.blend` 保留。早期自动 UV 和代画结果的完整备份位于
`Temp/EnvironmentSurface/before_shared_final.blend`；当前工作流不再运行代画脚本。
