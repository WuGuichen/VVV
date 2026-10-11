# 烘焙 FBX 切线精简（2026-09-21）

## 实现与范围

`export_unity_fbx.py` 仅在烘焙分支调用 `fbx_tangent_pruning.py`，在 `baked_surface.finalize` 记录模型哈希之前完成处理。通过有边界检查的二进制节点读取器移除 LayerElementTangent/Binormal 的索引 1、2 及 Layer 内对应引用，重算节点偏移和文件尾对齐。所有保留节点的属性字节（包括压缩数组）原样保留；写后回读并比较完整保留树，再原子替换文件。

接受 Blender 二进制 FBX 7400/7500，要求 UV0/HairFlow/HairMeta 三通道及 UV0 原始方向数据完整。未知布局拒绝处理，已经精简的文件直接返回原始字节。精简器纳入预览内容指纹。未改 Shader、Unity 导入参数、制作源或纹理，也未批量重烘焙。

## 验证

- Python 3 项测试覆盖两种节点格式、除指定层/引用之外的完整树一致、重复执行字节不变、未知布局不覆盖原文件、截断及缺少主切线拒绝。
- Blender 5.2 独立回读精简后的 000009：23,282 顶点、44,097 三角形、三个 UV 通道、134 骨骼、最多四权重，发流合同通过；保留原有 2 个零面积三角形警告，没有因精简修改几何。
- Blender 预览工作流 9 项回归通过。
- Unity 以原 FBX 导入设置创建两份临时模型，通过 Editor 设置可读后逐项哈希对比：顶点、法线、切线、颜色、UV0～UV7、索引、权重、绑定姿势完全一致。两份均为 109,918 个导入顶点和 1 个 SubMesh。
- 使用相同正式角色材质、骨架、视角及灯光，在独立 PreviewRenderUtility 中对比正面、侧面、背面：三幅 768×768 图逐像素差异均为 0。运行中场景未保存或替换。

## 000009 结果

FBX 从 8,091,308 减到 5,705,036 字节，节省 2,386,272 字节（约 29.49%）。已验证的精简字节同步到 Final_Source、角色包，并通过现有 CLI 导入 Unity；两个烘焙描述和模型资源哈希更新，贴图、Work、制作清单与原 FBX `.meta` 保持不变。

旧模型哈希 `sha256:13fec31d619990313a259daed0c9bb6a73ac955a5b5677c677d9d732ab11f0cb`，新模型哈希 `sha256:5805ff466a95cde80b5b9f98c858d48aae20198262df93fa8967faac67ba553e`。迁移备份、优化回读和渲染对照保存在本地 `Temp/fbx-tangent-check/`；比较证据在 `.agent/fbx-tangent-unity-comparison.json` 和 `.agent/fbx-tangent-render-comparison.json`。

Unity 重建请求 `b53bdbb52fb947fc8065147128cdab50` 已 completed，材质 bodyMaterialBound；持久回执 ready=true，五项输入哈希与请求一致，Console 错误 0。三处正式 FBX 的新哈希相同，Work、制作清单、两张贴图及正式 FBX meta 哈希再次确认未变。测试用 Assets 模型目录由 Unity 资产工具清理。本次未修改 C#，未请求域重载；保持用户原有 Play Mode 状态。

导入网格没有变小，因此本次不声称运行内存或帧率提升。源 FBX 体积不能替代 Unity 导入网格或 Player 构建体积测量。
