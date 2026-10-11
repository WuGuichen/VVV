# 场景共享细节与联合烘焙

> Status: Current

当前范围是 Blender 制作工作流；用户自行绘制，贴花留在 Unity。

新模型先在物体模式选中，执行 **N → WGame 场景 → 登记选中模型**。登记保留已有几何、材质与 UV，
补齐制作数据；未登记网格会在面板提示，并阻止联合烘焙及拼装检查。参考／辅助网格可明确设为辅助模型。
批量登记、稳定组件 ID 和排除规则见 [场景工具说明](../../Tools/Blender/WGameEnvironmentTools/README.md#新模型登记)。

打开 `ArtSource/Environment/Base/Blender_Work/base_work.blend`，使用 **N → WGame 场景**。

1. 在「场景配色 Profile」选择条目，选中模型，点「开始画笔绘制」，直接在 3D 视图左键拖动上色；也可先选面再点「应用到选中面」。调整草稿颜色后先添加或更新 Profile 条目。画笔用法与「WGame 角色画板」一致，底色上色不需要展开 DetailUV。
2. 点「编辑细节 UV」。只选择需要细节的区域，使用「生成选区 UV」保持连续展开；不需要细节的面留空。
   整面 UV 收拢到图外同一点时烘焙按留空处理；图内退化面仍会报错，制作文件中的 UV 不被自动改写。
3. 将 `ArtSource/Environment/Base/Shared/stylized_environment_detail_stamps_v1_slices/` 的独立透明素材自行排入最终一至两张共享 RGBA 细节图，再给模型指定最终图。在 UV 编辑器里把需要细节的岛移到对应图案，点「让选中模型共用此图」。该操作只切换已展开细节 UV 的面，保留其余面的原纯色材质；各模型独立编辑 DetailUV，允许重叠复用。
4. 绘制共享画布。需要自发光时，在 **自发光（Emission）→ 创建 / 接入发光遮罩** 后使用 **绘制发光 / 擦除发光**；黑色不发光、白色最大、灰色较弱，颜色沿用底色，共用 DetailUV。最后保存制作文件与细节图，发光遮罩也会保存。
5. 联合烘焙、查看候选、确认发布。

烘焙分组由“实际引用的细节图”决定。同组网格调用角色已有合图烘焙器，一起生成 BaseColor 和
`aoRimEmissionProfile16` SurfaceData，再分别导出引用该组贴图的模型 UV0。
这是角色最终产物的多模型扩展，不是运行时叠加调色板与原始细节图，也不是逐模型整图拼接。
自发光遮罩同样在制作侧合入 `SurfaceData.B`，不增加 Unity 材质槽或额外贴图。
最终 UV0 允许重叠复用；接入发光节点不会自动拆开重复细节。烘焙器核对底色、发光函数与
表面控制值，同一 DetailUV 上输出相同的区域共享图集位置，不同底色或发光结果保持分区。
「发光强度（HDR）」按共享遮罩保存，独立于画笔的 0–1 遮罩值；新遮罩默认 8，旧遮罩保持 1，可在 1–64 内调整。导出清单携带 `groups[].emissionIntensity`，Unity 更新组件时同步到共享材质，以 HDR 亮度供现有 Bloom 使用。

完整约定、安装方式与测试见 [场景工具说明](../../Tools/Blender/WGameEnvironmentTools/README.md)。

验证边界：已使用实际 Blender 烘焙验证同图共享、异色分区和不同图分组；
场景源码的 PaletteUV、DetailUV 与 C001_1 文件不会被后台烘焙改写。
Unity 对新分组源包的材质接入和性能验收另行进行。

## 发光细节复用回归（2026-09-29）

- 使用已发布制作内容的隔离快照重烘焙：85 个模型、2 组贴图、89 个文件全部通过哈希校验，源快照未改变。
- 含碑的组从 5 个细节区域降为 2 个：4 份重复猫图共用 1 块区域，灯的发光区域独立保留；14 个面参与细节共享。
- 同底色／同发光函数的最终 UV0 重叠，不同底色分开；位置相关发光和其它变化 UV 不复用。共享烘焙器 20 项检查及实际发光烘焙回归通过。
- 该次回归产物在 Temp；不等于作者已查看、发布并同步新的正式源包。

## 初始空白细节基线

- 77 个源模型：各自仅有 PaletteUV、DetailUV；共用一个制作材质、一张 256×256 色板和一张空白 2048×2048 RGBA 细节图。
- 未承载细节的面保持 (-1,-1)，未继续代画。之前的自动 UV／绘画备份在 `Temp/EnvironmentSurface/before_shared_final.blend`。
- 三种真实烘焙测试通过：同图同结果复用、同图不同底色分区不串色、不同图分组。
- 单独的临时 UI 测试通过：选中两个连续面得到连续 UV，第三个未选面保持留空，PaletteUV 不变，选区取消可恢复留空。
- 全库联合烘焙与回读通过：77 个原始独立 FBX、1 组 BaseColor／SurfaceData、1 个预览材质；全部导出网格只有 UV0。正式 Unity 源包另从已发布墙 FBX 派生一件共用端柱墙段，因此包含 78 个模型文件。
- 当前未绘制状态下，角色烘焙器复用了 17 个恒定色块，输出尺寸为 512×512。这是空白细节基线，不是未来绘制完成后的固定尺寸或帧率结论。
- 已实际验证预览、返回制作和发布；产物在 `ArtSource/Environment/Base/Final_Source/environment_build.json`。

## Unity 组装前检查与更新

在 `base_work.blend` 的 **N → WGame 场景** 点击「检查 Unity 拼装组件」。
插件按实际网格（含 Geometry Nodes 结果）核对分类、底部原点、尺寸、三角面数与
地坪／围墙／栅栏的拼接步长；报告写到 `Temp/EnvironmentSurface/assembly_audit.json`，
不改 Work。尺寸复核提示是人工判断项，不自动缩放原始模型。

正式发布继续走「联合烘焙 → 查看 → 发布」。FBX 按稳定组件 ID 放在
`Final_Source/Models/<类别>/`，贴图按细节图稳定 ID 放在 `Final_Source/Textures/`；
`environment_build.json` 仍记录每组贴图、逐模型 UV0 和文件哈希，并增加拼装元数据。
重复发布只覆盖变化内容，旧版清单拥有且未被人工改写的过期文件才会清理。
随后点击「同步已发布源包到 Unity」，插件核对哈希并将 FBX、贴图与清单复制到
`Assets/Art/WGame/Environment/Base/Published`；后续更新仍用同一按钮，
Unity `.meta`／GUID 保持稳定。Unity 里旧版多余文件仅报告、不自动删除。
Unity 未自动刷新时执行 **Assets → Refresh**。发布清单导入完成后会自动更新共享材质和组件 Prefab，
包括模型换组后的材质重绑；失败时查看 Console，修复源包后可手动执行
**WGame → Environment → Update Base Module Prefabs** 重试。基地布局、Combat 碰撞与场景维护见
[基地场景组装](ENVIRONMENT_BASE_SCENE_ASSEMBLY.md)。
