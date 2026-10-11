# 远山纯顶点色光照

> Status: Current
> Scope: WGame 远山 Shader、顶点色与材质制作约定。

Shader：`Assets/Art/WGame/Shaders/Environment/DistantMountainVertexColor.shader`。
制作文件：`ArtSource/Environment/DistantMountains/Blender_Work/distant_mountains_work.blend`。

## 颜色与材质

| 材质 | Vertex Color Content | 顶点数据 |
| --- | --- | --- |
| `DistantMountains.mat` | BakedColor（0） | 旧山 RGB 已包含固定方向明暗；保留低强度补光，忽略 Alpha 遮蔽 |
| `DistantMountainsAlbedoAO.mat` | AlbedoAO（1） | RGB 为底色，Alpha 为线性遮蔽，0=遮蔽、1=无遮挡；完整法线光照 |

两种材质均位于 `Assets/Art/WGame/Environment/DistantMountains/`，共用同一个 Shader。
同一种颜色数据的模型共用对应材质；不要给含固定明暗的旧 RGB 直接使用 AlbedoAO 模式。

当前新导入的 `rocky cliff 3d model` 与 `.001` 已在 Blender 中转换为 AlbedoAO。
属性仍为 `DistantMountainColor`，使用 POINT / BYTE_COLOR。旧 13 座山的颜色保持原样。
新山的 `DistantMountain_AlbedoAO_Preview` 仅为 Blender 中的近似光照预览，不能作为 Unity Shader 导出。
两山现已通过 WGame 场景插件导出为 `Mountain_14.fbx`、`Mountain_15.fbx`，并在 Unity 中绑定
`DistantMountainsAlbedoAO.mat`。资源位于 `Assets/Art/WGame/Environment/DistantMountains/Pieces/`，可直接拖入场景。
本次没有修改场景摆放。

后续更新：在 Blender 物体模式选中山体，使用 **N → WGame 场景 → 导出选中顶点色远山到 Unity**。
插件复用物体上的稳定编号，保留 Unity `.meta` 与材质映射；源码和同步副本分别位于
`ArtSource/Environment/DistantMountains/Final_Source/Pieces/` 与上述 Unity 目录。
首次导出其他新山的 Unity 导入设置仍需按下节配置。

## 导入要求

- 保留顶点色及其 Alpha，不增加 UV 或贴图。
- 法线设为 Import；原来的 None 会丢弃光照所需的法线。
- 切线仍设为 None，不生成 Lightmap UV。
- RGB 数据进入 Unity 网格时必须为线性颜色；当前旧 FBX 的 Unity 顶点值与 Blender 线性值一致，后续导出应保持该约定。
- 法线可能在原有硬边处拆分渲染顶点，不增加三角面。当前旧山导入中 `Mountain_12` 和 Backdrop 各增加 2 个渲染顶点。

## 常用调节

- `Ambient Strength` / `Main Light Strength`：暗面可读性和主光体积感。
- `Cool Ambient Tint` / `Warm Light Tint`：冷暗面与暖亮面。
- `Light Wrap`：光照过渡宽度。过大会再次变平。
- `Vertex Alpha Occlusion`：沟槽遮蔽，只用于 AlbedoAO。
- `Exposed Ridge Strength`：朝上、朝光且掠视的边缘提亮，避免整圈描边。
- `Baked Color Relighting`：旧顶点色补光强度，当前 0.18；设为 0 可保留旧固定明暗。
- `Distance Haze Start/End/Strength`：当前材质为 80 / 350 / 0.45，距离单位为世界单位。
- `Valley Haze Base Height/Height/Strength`：山脚高度雾；只有远距离才逐渐介入。

## 成本与验证边界

单个 Forward Pass，零纹理采样，无额外灯光循环、实时阴影采样或切线空间法线贴图。
新增成本为法线顶点数据、插值及每像素光照运算；这不是零 GPU 成本，也未声称帧率提升。
材质参数放在 `UnityPerMaterial` CBUFFER，不新增运行时脚本或逐帧材质实例。

2026-09-23 已在 Unity 6000.5 / URP 17.5 下导入并检查 Shader 编译信息，使用两座新山的实际网格和
顶点数据做隔离预览，并验证两种主光方向。两山仍共计 4,617 个制作顶点 / 8,948 个三角面，UV 为 0。
新材质和旧材质均确认一个 Pass、零纹理属性。平台构建和 GPU 性能采样不在本次验证范围。

同日插件导出回读：Mountain_14/15 分别为 5,576 / 3,372 三角面，Unity 渲染顶点 2,869 / 1,746
（第一座合并了两个等价顶点），法线与 RGBA 顶点色完整，UV／切线均为 0。
两者位置／旋转为零、缩放为一，原点位于底部中心。Alpha 范围分别为 46–255 / 63–255；
源数据与 Unity RGB 均值差小于 0.0003，未出现 sRGB 重复转换。
已实际渲染导入后的 FBX；15 件制作网格／颜色／变换及原有 30 个 FBX 文件内容保持不变。
